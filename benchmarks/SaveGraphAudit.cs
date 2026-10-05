using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

// Mirrors the current graph writer to account for bytes without writing a save.
// It reads serialized fields only: no game getters, lazy initialization, or cleanup.
sealed partial class SaveGraphAudit
{
    internal sealed class References : IEqualityComparer<object>
    {
        public new bool Equals(object a, object b) { return Object.ReferenceEquals(a, b); }
        public int GetHashCode(object value) { return RuntimeHelpers.GetHashCode(value); }
    }
    internal sealed class TypeCount { public long Nodes, Bytes; }
    public readonly List<object> Nodes = new List<object>();
    public readonly Dictionary<Type, TypeCount> Types = new Dictionary<Type, TypeCount>();
    readonly HashSet<object> seen = new HashSet<object>(new References());
    readonly Dictionary<Type, MemberInfo[]> members = new Dictionary<Type, MemberInfo[]>();
    readonly Dictionary<Type, int> typeNames = new Dictionary<Type, int>();
    readonly Dictionary<string, int> strings = new Dictionary<string, int>(StringComparer.Ordinal);
    public long Bytes = 8, TypeBytes, TypeOccurrences, FieldBytes, FieldGroups;
    public long StringBytes, StringOccurrences, UniqueStringBytes, ReferencesWritten;
    public int UniqueStrings { get { return strings.Count; } }
    public long UniqueTypeBytes { get { long n = 0; foreach (int size in typeNames.Values) n += size; return n; } }
    public int UniqueTypes { get { return typeNames.Count; } }
    public long UniqueFieldBytes { get; private set; }

    public SaveGraphAudit(object root)
    {
        Add(root);
        for (int i = 0; i < Nodes.Count; i++)
        {
            object value = Nodes[i]; Type type = value.GetType();
            long before = Bytes;
            Bytes += 5; TypeName(type);
            byte kind = Kind(type);
            if (kind == 7) Literal(value, type);
            else if (kind == 6) Bytes += 4 + ((byte[])value).Length;
            else if (kind == 1)
            {
                Array array = (Array)value; Bytes += 4 + 4 * array.Rank;
                foreach (object item in array) Value(item);
            }
            else if (kind == 3)
            {
                Bytes += 4;
                foreach (DictionaryEntry pair in (IDictionary)value) { Value(pair.Key); Value(pair.Value); }
            }
            else if (kind >= 2 && kind <= 5)
            {
                Bytes += 4;
                foreach (object item in (IEnumerable)value) Value(item);
            }
            else Fields(value, type);
            TypeCount count;
            if (!Types.TryGetValue(type, out count)) Types.Add(type, count = new TypeCount());
            count.Nodes++; count.Bytes += Bytes - before;
        }
    }

    void Add(object value) { if (seen.Add(value)) Nodes.Add(value); }
    void Value(object value)
    {
        Bytes++;
        if (value == null) return;
        Type type = value.GetType();
        if (Kind(type) == 7) { TypeName(type); Literal(value, type); }
        else if (type.IsValueType) { TypeName(type); Fields(value, type); }
        else { Bytes += 4; ReferencesWritten++; Add(value); }
    }
    void Fields(object value, Type type)
    {
        MemberInfo[] fields;
        if (!members.TryGetValue(type, out fields))
        {
            fields = FormatterServices.GetSerializableMembers(type); members.Add(type, fields);
            UniqueFieldBytes += 4;
            foreach (MemberInfo field in fields) UniqueFieldBytes += StringSize(field.Name);
        }
        Bytes += 4; FieldBytes += 4; FieldGroups++;
        object[] values = FormatterServices.GetObjectData(value, fields);
        for (int i = 0; i < fields.Length; i++)
        {
            int size = StringSize(fields[i].Name); Bytes += size; FieldBytes += size;
            Value(values[i]);
        }
    }
    static byte Kind(Type type)
    {
        if (type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(Guid)) return 7;
        if (type == typeof(byte[])) return 6;
        if (type.IsArray) return 1;
        if (type.IsGenericType)
        {
            Type definition = type.GetGenericTypeDefinition();
            if (definition == typeof(List<>)) return 2;
            if (definition == typeof(Dictionary<,>)) return 3;
            if (definition == typeof(HashSet<>)) return 4;
            if (definition == typeof(Queue<>)) return 5;
        }
        return 0;
    }
}
