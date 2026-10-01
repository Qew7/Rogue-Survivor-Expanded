using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine
{
    // V5: type/field/string tables, compact tiles, shared object IDs and cycles.
    static partial class ObjectGraphStore
    {
        const int MaxObjects = 8000000, MaxItems = 100000000;
        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) { return Object.ReferenceEquals(a, b); }
            public int GetHashCode(object value) { return RuntimeHelpers.GetHashCode(value); }
        }
        sealed class Schema
        {
            public int Id; public Type Type; public byte Kind;
            public MemberInfo[] Members; public MethodInfo Add; public PropertyInfo Count;
        }
        sealed class StoredValue
        {
            public byte Kind; public int Reference; public object Literal;
            public Schema Schema; public StoredValue[] Fields;
            public object Resolve(List<object> objects)
            {
                if (Kind == 0) return null;
                if (Kind == 1) return ReferenceAt(objects, Reference);
                if (Kind == 2) return Literal;
                object value = Activator.CreateInstance(Schema.Type);
                FillFields(value, Schema, Fields, objects); return value;
            }
        }
        sealed class Node
        {
            public object Value; public Schema Schema;
            public StoredValue[] Fields, Values, Keys; public int Decoration;
        }
        static readonly Assembly GameAssembly = typeof(Session).Assembly;
        static readonly MemberInfo[] TileMembers = {
            typeof(Tile).GetField("m_ModelID", BindingFlags.Instance | BindingFlags.NonPublic),
            typeof(Tile).GetField("m_Flags", BindingFlags.Instance | BindingFlags.NonPublic),
            typeof(Tile).GetField("m_Decorations", BindingFlags.Instance | BindingFlags.NonPublic) };

        sealed class Writer
        {
            readonly BinaryWriter output; readonly object root; readonly bool omitArchive;
            readonly Dictionary<object, int> ids = new Dictionary<object, int>(new ReferenceComparer());
            readonly List<object> nodes = new List<object>();
            readonly Dictionary<Type, Schema> schemas = new Dictionary<Type, Schema>();
            readonly Dictionary<string, int> strings = new Dictionary<string, int>(StringComparer.Ordinal);
            public Writer(Stream stream, object root, bool omitArchive)
            { output = new BinaryWriter(stream); this.root = root; this.omitArchive = omitArchive; }
            int Add(object value)
            {
                if (value == null) return 0;
                int id; if (ids.TryGetValue(value, out id)) return id;
                if (nodes.Count >= MaxObjects) throw new InvalidDataException("Too many saved objects.");
                id = nodes.Count + 1; ids.Add(value, id); nodes.Add(value); return id;
            }
            Schema TypeName(Type type)
            {
                Schema schema;
                if (schemas.TryGetValue(type, out schema)) { output.Write(schema.Id); return schema; }
                if (schemas.Count >= 4096 || !AllowedType(type)) throw new InvalidDataException("Unsupported save type: " + type);
                schema = new Schema { Id = schemas.Count + 1, Type = type, Kind = NodeKind(type) };
                schema.Members = schema.Kind == 0 ? FormatterServices.GetSerializableMembers(type) : new MemberInfo[0];
                if (schema.Kind == 2 || schema.Kind == 4 || schema.Kind == 5) schema.Count = type.GetProperty("Count");
                schemas.Add(type, schema);
                output.Write(-schema.Id); output.Write(type.AssemblyQualifiedName); output.Write(schema.Members.Length);
                foreach (MemberInfo member in schema.Members) output.Write(member.Name);
                return schema;
            }
            void Value(object value)
            {
                if (value == null) { output.Write((byte)0); return; }
                Type type = value.GetType();
                if (NodeKind(type) == 7)
                { output.Write((byte)2); TypeName(type); WriteLiteral(output, value, type, strings); }
                else if (type.IsValueType) { output.Write((byte)3); Fields(value, TypeName(type)); }
                else { output.Write((byte)1); output.Write(Add(value)); }
            }
            void Fields(object value, Schema schema)
            {
                object[] fields = FormatterServices.GetObjectData(value, schema.Members);
                for (int i = 0; i < fields.Length; i++)
                    Value(omitArchive && Object.ReferenceEquals(value, root) && schema.Type == typeof(Session) &&
                        schema.Members[i].Name == "m_ResidentRecords" ? null : fields[i]);
            }
            public void Run()
            {
                output.Write(Add(root));
                for (int i = 0; i < nodes.Count; i++)
                {
                    object value = nodes[i]; output.Write(i + 1); Schema schema = TypeName(value.GetType());
                    if (schema.Kind == 7) WriteLiteral(output, value, schema.Type, strings);
                    else if (schema.Kind == 8)
                    {
                        object[] fields = FormatterServices.GetObjectData(value, TileMembers);
                        output.Write((int)fields[0]); output.Write(Convert.ToInt32(fields[1])); output.Write(Add(fields[2]));
                    }
                    else if (schema.Kind == 6)
                    { byte[] bytes = (byte[])value; CheckCount(bytes.Length); output.Write(bytes.Length); output.Write(bytes); }
                    else if (schema.Kind == 1)
                    {
                        Array array = (Array)value; CheckCount(array.Length);
                        if (array.Rank > 4) throw new InvalidDataException("Invalid array rank.");
                        output.Write(array.Rank);
                        for (int axis = 0; axis < array.Rank; axis++)
                        {
                            if (array.GetLowerBound(axis) != 0) throw new InvalidDataException("Nonzero array bound.");
                            output.Write(array.GetLength(axis));
                        }
                        foreach (object item in array) Value(item);
                    }
                    else if (schema.Kind == 3)
                    {
                        IDictionary pairs = (IDictionary)value; CheckCount(pairs.Count); output.Write(pairs.Count);
                        foreach (DictionaryEntry pair in pairs) { Value(pair.Key); Value(pair.Value); }
                    }
                    else if (schema.Kind >= 2 && schema.Kind <= 5)
                    {
                        int count = (int)schema.Count.GetValue(value, null); CheckCount(count); output.Write(count);
                        foreach (object item in (IEnumerable)value) Value(item);
                    }
                    else Fields(value, schema);
                }
                output.Write(0);
            }
        }
        sealed class Reader
        {
            readonly BinaryReader input;
            readonly bool legacySettings;
            readonly List<Schema> schemas = new List<Schema> { null };
            readonly List<string> strings = new List<string> { null };
            public Reader(Stream stream, bool legacySettings = false) { input = new BinaryReader(stream); this.legacySettings = legacySettings; }
            Schema TypeName()
            {
                if (legacySettings) { Type oldType = ResolveType(input.ReadString());
                    return new Schema { Type = oldType, Kind = NodeKind(oldType), Add = oldType.GetMethod(NodeKind(oldType) == 4 ? "Add" : "Enqueue") }; }
                int id = input.ReadInt32();
                if (id > 0 && id < schemas.Count) return schemas[id];
                if (id != -schemas.Count || schemas.Count > 4096) throw new InvalidDataException("Invalid type id.");
                Type type = ResolveType(input.ReadString());
                Schema schema = new Schema { Id = schemas.Count, Type = type, Kind = NodeKind(type) };
                int count = input.ReadInt32();
                if (count < 0 || count > 10000 || (schema.Kind != 0 && count != 0)) throw new InvalidDataException("Invalid field schema.");
                schema.Members = new MemberInfo[count];
                Dictionary<string, MemberInfo> known = new Dictionary<string, MemberInfo>();
                if (schema.Kind == 0) foreach (MemberInfo field in FormatterServices.GetSerializableMembers(type)) known.Add(field.Name, field);
                HashSet<string> names = new HashSet<string>();
                for (int i = 0; i < count; i++)
                {
                    string name = input.ReadString();
                    if (!names.Add(name)) throw new InvalidDataException("Duplicate field name.");
                    known.TryGetValue(name, out schema.Members[i]);
                }
                if (schema.Kind == 4 || schema.Kind == 5) schema.Add = type.GetMethod(schema.Kind == 4 ? "Add" : "Enqueue");
                schemas.Add(schema); return schema;
            }
            StoredValue Value()
            {
                StoredValue value = new StoredValue { Kind = input.ReadByte() };
                if (value.Kind == 1) value.Reference = input.ReadInt32();
                else if (value.Kind == 2)
                {
                    value.Schema = TypeName();
                    if (value.Schema.Kind != 7) throw new InvalidDataException("Expected literal type.");
                    value.Literal = ReadLiteral(input, value.Schema.Type, legacySettings ? null : strings);
                }
                else if (value.Kind == 3)
                {
                    value.Schema = TypeName();
                    if (!value.Schema.Type.IsValueType || value.Schema.Kind != 0) throw new InvalidDataException("Expected value type.");
                    value.Fields = Fields(value.Schema);
                }
                else if (value.Kind != 0) throw new InvalidDataException("Invalid value kind.");
                return value;
            }
            StoredValue[] Fields(Schema schema)
            {
                if (!legacySettings) return Values(schema.Members.Length);
                int count = input.ReadInt32(); if (count < 0 || count > 10000) throw new InvalidDataException("Invalid settings fields.");
                schema.Members = new MemberInfo[count]; StoredValue[] values = new StoredValue[count];
                Dictionary<string, MemberInfo> known = new Dictionary<string, MemberInfo>();
                foreach (MemberInfo field in FormatterServices.GetSerializableMembers(schema.Type)) known.Add(field.Name, field);
                HashSet<string> names = new HashSet<string>();
                for (int i = 0; i < count; i++) { string name = input.ReadString();
                    if (!names.Add(name)) throw new InvalidDataException("Duplicate field name.");
                    known.TryGetValue(name, out schema.Members[i]); values[i] = Value(); }
                return values;
            }
            StoredValue[] Values(int count)
            {
                CheckCount(count); StoredValue[] values = new StoredValue[count];
                for (int i = 0; i < count; i++) values[i] = Value(); return values;
            }
            public object Run()
            {
                int root = input.ReadInt32();
                if (root != 1) throw new InvalidDataException("Invalid root object id.");
                List<object> objects = new List<object> { null }; List<Node> deferred = new List<Node>(); int id;
                while ((id = input.ReadInt32()) != 0)
                {
                    if (objects.Count > MaxObjects || id != objects.Count) throw new InvalidDataException("Invalid object id or object limit.");
                    Schema schema = TypeName();
                    if (legacySettings && ((id == 1 && !SettingsRoot(schema.Type)) || input.ReadByte() != schema.Kind || objects.Count > 20000))
                        throw new InvalidDataException("Older worlds are unsupported; start a new game.");
                    Node node = new Node { Schema = schema };
                    if (schema.Kind == 7) node.Value = ReadLiteral(input, schema.Type, legacySettings ? null : strings);
                    else if (schema.Kind == 8)
                    {
                        node.Value = FormatterServices.GetUninitializedObject(schema.Type);
                        FormatterServices.PopulateObjectMembers(node.Value, TileMembers, new object[] {
                            input.ReadInt32(), Enum.ToObject(((FieldInfo)TileMembers[1]).FieldType, input.ReadInt32()), null });
                        node.Decoration = input.ReadInt32();
                        if (node.Decoration < 0) throw new InvalidDataException("Invalid decoration reference.");
                    }
                    else if (schema.Kind == 6)
                    {
                        int count = input.ReadInt32(); CheckCount(count);
                        byte[] bytes = input.ReadBytes(count); if (bytes.Length != count) throw new EndOfStreamException(); node.Value = bytes;
                    }
                    else if (schema.Kind == 1)
                    {
                        int rank = input.ReadInt32();
                        if (rank < 1 || rank > 4 || rank != schema.Type.GetArrayRank()) throw new InvalidDataException("Invalid array rank.");
                        int[] lengths = new int[rank]; long count = 1;
                        for (int axis = 0; axis < rank; axis++)
                        { lengths[axis] = input.ReadInt32(); CheckCount(lengths[axis]); count *= lengths[axis]; if (count > MaxItems) throw new InvalidDataException("Array too large."); }
                        node.Value = Array.CreateInstance(schema.Type.GetElementType(), lengths); node.Values = Values((int)count);
                    }
                    else if (schema.Kind >= 2 && schema.Kind <= 5)
                    {
                        node.Value = Activator.CreateInstance(schema.Type); int count = input.ReadInt32(); CheckCount(count);
                        if (schema.Kind == 3)
                        {
                            node.Keys = new StoredValue[count]; node.Values = new StoredValue[count];
                            for (int i = 0; i < count; i++) { node.Keys[i] = Value(); node.Values[i] = Value(); }
                        }
                        else node.Values = Values(count);
                    }
                    else { node.Value = FormatterServices.GetUninitializedObject(schema.Type); node.Fields = Fields(schema); }
                    objects.Add(node.Value);
                    if (schema.Kind <= 5 || (schema.Kind == 8 && node.Decoration != 0)) deferred.Add(node);
                }
                if (input.BaseStream.ReadByte() != -1) throw new InvalidDataException("Trailing save data.");
                foreach (Node node in deferred)
                {
                    if (node.Schema.Kind == 0) FillFields(node.Value, node.Schema, node.Fields, objects);
                    if (node.Schema.Kind == 8)
                    {
                        object decorations = ReferenceAt(objects, node.Decoration);
                        if (!(decorations is List<string>)) throw new InvalidDataException("Invalid tile decorations.");
                        ((FieldInfo)TileMembers[2]).SetValue(node.Value, decorations);
                    }
                }
                foreach (Node node in deferred) if (node.Schema.Kind == 1) FillArray((Array)node.Value, node.Values, objects);
                foreach (Node node in deferred) if (node.Schema.Kind >= 2 && node.Schema.Kind <= 5) FillCollection(node, objects);
                return ReferenceAt(objects, root);
            }
        }
        public static void Write(Stream stream, object root) { Write(stream, root, false); }
        public static void Write(Stream stream, object root, bool omitSessionArchive)
        { if (root == null) throw new ArgumentNullException("root"); new Writer(stream, root, omitSessionArchive).Run(); }
        public static object Read(Stream stream) { return new Reader(stream).Run(); }
        internal static object ReadLegacySettings(Stream stream) { return new Reader(stream, true).Run(); }
        static bool SettingsRoot(Type type)
        { return type == typeof(GameOptions) || type == typeof(Keybindings) || type == typeof(GameHintsStatus) ||
            type == typeof(HiScoreTable) || type == typeof(GamePresetCollection); }
        static object ReferenceAt(List<object> objects, int id)
        { if (id <= 0 || id >= objects.Count) throw new InvalidDataException("Invalid object reference."); return objects[id]; }
        static void CheckCount(int count)
        { if (count < 0 || count > MaxItems) throw new InvalidDataException("Invalid collection size."); }
        static byte NodeKind(Type type)
        {
            if (type == typeof(Tile)) return 8;
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(Guid)) return 7;
            if (type == typeof(byte[])) return 6;
            if (type.IsArray) return 1;
            if (type.IsGenericType)
            {
                Type generic = type.GetGenericTypeDefinition();
                if (generic == typeof(List<>)) return 2;
                if (generic == typeof(Dictionary<,>)) return 3;
                if (generic == typeof(HashSet<>)) return 4;
                if (generic == typeof(Queue<>)) return 5;
            }
            return 0;
        }
        static void FillFields(object value, Schema schema, StoredValue[] fields, List<object> objects)
        {
            List<MemberInfo> members = new List<MemberInfo>(); List<object> values = new List<object>();
            for (int i = 0; i < fields.Length; i++)
                if (schema.Members[i] != null) { members.Add(schema.Members[i]); values.Add(fields[i].Resolve(objects)); }
            FormatterServices.PopulateObjectMembers(value, members.ToArray(), values.ToArray());
        }
        static void FillArray(Array array, StoredValue[] values, List<object> objects)
        {
            int[] indexes = new int[array.Rank];
            for (int i = 0; i < values.Length; i++)
            {
                int position = i;
                for (int axis = array.Rank - 1; axis >= 0; axis--) { indexes[axis] = position % array.GetLength(axis); position /= array.GetLength(axis); }
                array.SetValue(values[i].Resolve(objects), indexes);
            }
        }
        static void FillCollection(Node node, List<object> objects)
        {
            if (node.Schema.Kind == 2)
                foreach (StoredValue item in node.Values) ((IList)node.Value).Add(item.Resolve(objects));
            else if (node.Schema.Kind == 3)
                for (int i = 0; i < node.Values.Length; i++) ((IDictionary)node.Value).Add(node.Keys[i].Resolve(objects), node.Values[i].Resolve(objects));
            else foreach (StoredValue item in node.Values) node.Schema.Add.Invoke(node.Value, new object[] { item.Resolve(objects) });
        }
    }
}
