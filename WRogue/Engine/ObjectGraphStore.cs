using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace djack.RogueSurvivor.Engine
{
    // A field-based graph format. References are numbered so shared objects and cycles survive.
    // Only game types and a small set of framework collection/value types may be read.
    static class ObjectGraphStore
    {
        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) { return Object.ReferenceEquals(a, b); }
            public int GetHashCode(object value) { return RuntimeHelpers.GetHashCode(value); }
        }

        sealed class StoredValue
        {
            public byte Kind;
            public int Reference;
            public object Literal;
            public Type Type;
            public Dictionary<string, StoredValue> Fields;

            public object Resolve(Dictionary<int, object> objects)
            {
                if (Kind == 0) return null;
                if (Kind == 1) return objects[Reference];
                if (Kind == 2) return Literal;
                object value = Activator.CreateInstance(Type);
                FillFields(value, Fields, objects);
                return value;
            }
        }

        sealed class Node
        {
            public object Value;
            public Type Type;
            public byte Kind;
            public Dictionary<string, StoredValue> Fields;
            public List<StoredValue> Values;
            public List<StoredValue> Keys;
        }

        static readonly ReferenceComparer Comparer = new ReferenceComparer();
        static readonly Assembly GameAssembly = typeof(Session).Assembly;

        public static void Write(Stream stream, object root)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            Dictionary<object, int> ids = new Dictionary<object, int>(Comparer);
            List<object> nodes = new List<object>();
            int rootId = Add(root, ids, nodes);
            writer.Write(rootId);
            for (int i = 0; i < nodes.Count; i++)
            {
                object value = nodes[i];
                Type type = value.GetType();
                writer.Write(i + 1);
                writer.Write(type.AssemblyQualifiedName);
                byte kind = NodeKind(type);
                writer.Write(kind);
                if (kind == 7)
                    WriteLiteral(writer, value, type);
                else if (kind == 1)
                {
                    Array array = (Array)value;
                    writer.Write(array.Rank);
                    for (int axis = 0; axis < array.Rank; axis++) writer.Write(array.GetLength(axis));
                    foreach (object item in array) WriteValue(writer, item, ids, nodes);
                }
                else if (kind == 2 || kind == 4 || kind == 5)
                {
                    IEnumerable items = (IEnumerable)value;
                    writer.Write((int)type.GetProperty("Count").GetValue(value, null));
                    foreach (object item in items) WriteValue(writer, item, ids, nodes);
                }
                else if (kind == 3)
                {
                    IDictionary pairs = (IDictionary)value;
                    writer.Write(pairs.Count);
                    foreach (DictionaryEntry pair in pairs)
                    {
                        WriteValue(writer, pair.Key, ids, nodes);
                        WriteValue(writer, pair.Value, ids, nodes);
                    }
                }
                else if (kind == 6)
                {
                    byte[] bytes = (byte[])value;
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }
                else
                    WriteFields(writer, value, ids, nodes);
            }
            writer.Write(0);
        }

        public static object Read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            int root = reader.ReadInt32();
            Dictionary<int, object> objects = new Dictionary<int, object>();
            List<Node> nodes = new List<Node>();
            int id;
            while ((id = reader.ReadInt32()) != 0)
            {
                if (nodes.Count >= 2000000) throw new InvalidDataException("Too many saved objects.");
                if (id != nodes.Count + 1) throw new InvalidDataException("Invalid object id.");
                Type type = ResolveType(reader.ReadString());
                byte kind = reader.ReadByte();
                if (kind != NodeKind(type)) throw new InvalidDataException("Invalid object kind.");
                Node node = new Node { Type = type, Kind = kind };
                if (kind == 7)
                    node.Value = ReadLiteral(reader, type);
                else if (kind == 1)
                {
                    int rank = reader.ReadInt32();
                    if (rank < 1 || rank > 4) throw new InvalidDataException("Invalid array rank.");
                    int[] lengths = new int[rank];
                    for (int axis = 0; axis < rank; axis++) lengths[axis] = reader.ReadInt32();
                    Array array = Array.CreateInstance(type.GetElementType(), lengths);
                    node.Value = array;
                    node.Values = ReadValues(reader, array.Length);
                }
                else if (kind == 6)
                {
                    int length = reader.ReadInt32();
                    if (length < 0 || length > 100000000) throw new InvalidDataException("Invalid byte array.");
                    byte[] bytes = reader.ReadBytes(length);
                    if (bytes.Length != length) throw new EndOfStreamException();
                    node.Value = bytes;
                }
                else if (kind == 2 || kind == 4 || kind == 5)
                {
                    node.Value = Activator.CreateInstance(type);
                    node.Values = ReadValues(reader, reader.ReadInt32());
                }
                else if (kind == 3)
                {
                    node.Value = Activator.CreateInstance(type);
                    int count = reader.ReadInt32();
                    node.Keys = new List<StoredValue>(count);
                    node.Values = new List<StoredValue>(count);
                    for (int pair = 0; pair < count; pair++)
                    {
                        node.Keys.Add(ReadValue(reader));
                        node.Values.Add(ReadValue(reader));
                    }
                }
                else
                {
                    node.Value = FormatterServices.GetUninitializedObject(type);
                    node.Fields = ReadFields(reader);
                }
                objects.Add(id, node.Value);
                nodes.Add(node);
            }
            if (reader.BaseStream.ReadByte() != -1)
                throw new InvalidDataException("Trailing save data.");
            foreach (Node node in nodes)
                if (node.Kind == 0) FillFields(node.Value, node.Fields, objects);
            foreach (Node node in nodes)
                if (node.Kind == 1) FillArray((Array)node.Value, node.Values, objects);
            foreach (Node node in nodes)
                if (node.Kind >= 2 && node.Kind <= 5) FillCollection(node, objects);
            return objects[root];
        }

        static int Add(object value, Dictionary<object, int> ids, List<object> nodes)
        {
            int id;
            if (ids.TryGetValue(value, out id)) return id;
            id = nodes.Count + 1;
            ids.Add(value, id);
            nodes.Add(value);
            return id;
        }

        static byte NodeKind(Type type)
        {
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum ||
                type == typeof(decimal) || type == typeof(DateTime) ||
                type == typeof(TimeSpan) || type == typeof(Guid)) return 7;
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

        static void WriteValue(BinaryWriter writer, object value,
            Dictionary<object, int> ids, List<object> nodes)
        {
            if (value == null) { writer.Write((byte)0); return; }
            Type type = value.GetType();
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum ||
                type == typeof(decimal) || type == typeof(DateTime) ||
                type == typeof(TimeSpan) || type == typeof(Guid))
            {
                writer.Write((byte)2);
                writer.Write(type.AssemblyQualifiedName);
                WriteLiteral(writer, value, type);
            }
            else if (type.IsValueType)
            {
                writer.Write((byte)3);
                writer.Write(type.AssemblyQualifiedName);
                WriteFields(writer, value, ids, nodes);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(Add(value, ids, nodes));
            }
        }

        static StoredValue ReadValue(BinaryReader reader)
        {
            StoredValue value = new StoredValue { Kind = reader.ReadByte() };
            if (value.Kind == 1) value.Reference = reader.ReadInt32();
            else if (value.Kind == 2)
            {
                value.Type = ResolveType(reader.ReadString());
                value.Literal = ReadLiteral(reader, value.Type);
            }
            else if (value.Kind == 3)
            {
                value.Type = ResolveType(reader.ReadString());
                if (!value.Type.IsValueType) throw new InvalidDataException("Expected value type.");
                value.Fields = ReadFields(reader);
            }
            else if (value.Kind != 0) throw new InvalidDataException("Invalid value kind.");
            return value;
        }

        static void WriteFields(BinaryWriter writer, object value,
            Dictionary<object, int> ids, List<object> nodes)
        {
            MemberInfo[] members = FormatterServices.GetSerializableMembers(value.GetType());
            object[] values = FormatterServices.GetObjectData(value, members);
            writer.Write(members.Length);
            for (int i = 0; i < members.Length; i++)
            {
                writer.Write(members[i].Name);
                WriteValue(writer, values[i], ids, nodes);
            }
        }

        static Dictionary<string, StoredValue> ReadFields(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 10000) throw new InvalidDataException("Invalid field count.");
            Dictionary<string, StoredValue> fields = new Dictionary<string, StoredValue>(count);
            for (int i = 0; i < count; i++) fields.Add(reader.ReadString(), ReadValue(reader));
            return fields;
        }

        static void FillFields(object value, Dictionary<string, StoredValue> fields,
            Dictionary<int, object> objects)
        {
            MemberInfo[] members = FormatterServices.GetSerializableMembers(value.GetType());
            List<MemberInfo> present = new List<MemberInfo>();
            List<object> values = new List<object>();
            foreach (MemberInfo member in members)
            {
                StoredValue stored;
                if (!fields.TryGetValue(member.Name, out stored)) continue;
                present.Add(member);
                values.Add(stored.Resolve(objects));
            }
            FormatterServices.PopulateObjectMembers(value, present.ToArray(), values.ToArray());
        }

        static List<StoredValue> ReadValues(BinaryReader reader, int count)
        {
            if (count < 0 || count > 100000000) throw new InvalidDataException("Invalid collection size.");
            List<StoredValue> values = new List<StoredValue>(count);
            for (int i = 0; i < count; i++) values.Add(ReadValue(reader));
            return values;
        }

        static void FillArray(Array array, List<StoredValue> values,
            Dictionary<int, object> objects)
        {
            int[] indexes = new int[array.Rank];
            for (int i = 0; i < values.Count; i++)
            {
                int position = i;
                for (int axis = array.Rank - 1; axis >= 0; axis--)
                {
                    indexes[axis] = position % array.GetLength(axis);
                    position /= array.GetLength(axis);
                }
                array.SetValue(values[i].Resolve(objects), indexes);
            }
        }

        static void FillCollection(Node node, Dictionary<int, object> objects)
        {
            if (node.Kind == 2)
            {
                IList list = (IList)node.Value;
                foreach (StoredValue item in node.Values) list.Add(item.Resolve(objects));
            }
            else if (node.Kind == 3)
            {
                IDictionary dict = (IDictionary)node.Value;
                for (int i = 0; i < node.Keys.Count; i++)
                    dict.Add(node.Keys[i].Resolve(objects), node.Values[i].Resolve(objects));
            }
            else
            {
                MethodInfo add = node.Type.GetMethod(node.Kind == 4 ? "Add" : "Enqueue");
                foreach (StoredValue item in node.Values)
                    add.Invoke(node.Value, new object[] { item.Resolve(objects) });
            }
        }

        static Type ResolveType(string name)
        {
            Type type = Type.GetType(name,
                delegate(AssemblyName requested)
                {
                    if (requested.Name == GameAssembly.GetName().Name) return GameAssembly;
                    foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
                        if (loaded.GetName().Name == requested.Name &&
                            (requested.Name == "mscorlib" || requested.Name == "System" ||
                             requested.Name == "System.Core" || requested.Name == "System.Drawing" ||
                             requested.Name == "System.Windows.Forms"))
                            return loaded;
                    return null;
                },
                delegate(Assembly assembly, string typeName, bool ignoreCase)
                {
                    return assembly == null ? null : assembly.GetType(typeName, false, ignoreCase);
                }, false);
            if (type == null || !AllowedType(type))
                throw new InvalidDataException("Unsupported save type: " + name);
            return type;
        }

        static bool AllowedType(Type type)
        {
            if (type.Assembly == GameAssembly) return type.IsSerializable;
            if (type.IsArray) return AllowedType(type.GetElementType());
            if (type.IsGenericType)
            {
                Type generic = type.GetGenericTypeDefinition();
                if (generic != typeof(List<>) && generic != typeof(Dictionary<,>) &&
                    generic != typeof(HashSet<>) && generic != typeof(Queue<>) &&
                    generic != typeof(KeyValuePair<,>)) return false;
                foreach (Type argument in type.GetGenericArguments())
                    if (!AllowedType(argument)) return false;
                return true;
            }
            return type == typeof(object) || type == typeof(string) ||
                type.IsPrimitive || type.IsEnum ||
                type == typeof(decimal) || type == typeof(DateTime) ||
                type == typeof(TimeSpan) || type == typeof(Guid) ||
                type == typeof(System.Drawing.Point) || type == typeof(System.Drawing.Size) ||
                type == typeof(System.Drawing.Rectangle) || type == typeof(System.Drawing.Color);
        }

        static void WriteLiteral(BinaryWriter writer, object value, Type type)
        {
            if (type.IsEnum) { writer.Write(Convert.ToInt64(value)); return; }
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean: writer.Write((bool)value); break;
                case TypeCode.Byte: writer.Write((byte)value); break;
                case TypeCode.SByte: writer.Write((sbyte)value); break;
                case TypeCode.Int16: writer.Write((short)value); break;
                case TypeCode.UInt16: writer.Write((ushort)value); break;
                case TypeCode.Int32: writer.Write((int)value); break;
                case TypeCode.UInt32: writer.Write((uint)value); break;
                case TypeCode.Int64: writer.Write((long)value); break;
                case TypeCode.UInt64: writer.Write((ulong)value); break;
                case TypeCode.Single: writer.Write((float)value); break;
                case TypeCode.Double: writer.Write((double)value); break;
                case TypeCode.Char: writer.Write((char)value); break;
                case TypeCode.String: writer.Write((string)value); break;
                case TypeCode.Decimal:
                    foreach (int part in Decimal.GetBits((decimal)value)) writer.Write(part);
                    break;
                case TypeCode.DateTime: writer.Write(((DateTime)value).ToBinary()); break;
                default:
                    if (type == typeof(TimeSpan)) writer.Write(((TimeSpan)value).Ticks);
                    else if (type == typeof(Guid)) writer.Write(((Guid)value).ToByteArray());
                    else throw new InvalidDataException("Unsupported literal: " + type);
                    break;
            }
        }

        static object ReadLiteral(BinaryReader reader, Type type)
        {
            if (type.IsEnum) return Enum.ToObject(type, reader.ReadInt64());
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean: return reader.ReadBoolean();
                case TypeCode.Byte: return reader.ReadByte();
                case TypeCode.SByte: return reader.ReadSByte();
                case TypeCode.Int16: return reader.ReadInt16();
                case TypeCode.UInt16: return reader.ReadUInt16();
                case TypeCode.Int32: return reader.ReadInt32();
                case TypeCode.UInt32: return reader.ReadUInt32();
                case TypeCode.Int64: return reader.ReadInt64();
                case TypeCode.UInt64: return reader.ReadUInt64();
                case TypeCode.Single: return reader.ReadSingle();
                case TypeCode.Double: return reader.ReadDouble();
                case TypeCode.Char: return reader.ReadChar();
                case TypeCode.String: return reader.ReadString();
                case TypeCode.Decimal:
                    return new Decimal(new int[] { reader.ReadInt32(), reader.ReadInt32(),
                        reader.ReadInt32(), reader.ReadInt32() });
                case TypeCode.DateTime: return DateTime.FromBinary(reader.ReadInt64());
                default:
                    if (type == typeof(TimeSpan)) return new TimeSpan(reader.ReadInt64());
                    if (type == typeof(Guid)) return new Guid(reader.ReadBytes(16));
                    throw new InvalidDataException("Unsupported literal: " + type);
            }
        }
    }
}
