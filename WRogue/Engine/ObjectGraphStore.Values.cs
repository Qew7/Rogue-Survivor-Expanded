using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace djack.RogueSurvivor.Engine
{
    static partial class ObjectGraphStore
    {
        static void WriteString(BinaryWriter writer, string value, Dictionary<string, int> strings)
        {
            int id;
            if (strings.TryGetValue(value, out id)) { writer.Write(id); return; }
            if (strings.Count >= MaxObjects) throw new InvalidDataException("Too many saved strings.");
            id = strings.Count + 1; strings.Add(value, id);
            writer.Write(-id); writer.Write(value);
        }
        static string ReadString(BinaryReader reader, List<string> strings)
        {
            int id = reader.ReadInt32();
            if (id > 0 && id < strings.Count) return strings[id];
            if (id != -strings.Count || strings.Count > MaxObjects)
                throw new InvalidDataException("Invalid string id.");
            string value = reader.ReadString(); strings.Add(value); return value;
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

        static void WriteLiteral(BinaryWriter writer, object value, Type type, Dictionary<string, int> strings)
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
                case TypeCode.String: WriteString(writer, (string)value, strings); break;
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

        static object ReadLiteral(BinaryReader reader, Type type, List<string> strings)
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
                case TypeCode.String: return strings == null ? reader.ReadString() : ReadString(reader, strings);
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
