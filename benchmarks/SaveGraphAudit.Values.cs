using System;
using System.IO;
using System.Text;

sealed partial class SaveGraphAudit
{
    void TypeName(Type type)
    {
        int size;
        if (!typeNames.TryGetValue(type, out size))
            typeNames.Add(type, size = StringSize(type.AssemblyQualifiedName));
        Bytes += size; TypeBytes += size; TypeOccurrences++;
    }
    internal static int StringSize(string value)
    {
        int length = Encoding.UTF8.GetByteCount(value), prefix = 1;
        for (int n = length; n >= 128; n >>= 7) prefix++;
        return prefix + length;
    }
    void Literal(object value, Type type)
    {
        if (type.IsEnum) { Bytes += 8; return; }
        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Boolean: case TypeCode.Byte: case TypeCode.SByte: Bytes++; break;
            case TypeCode.Int16: case TypeCode.UInt16: Bytes += 2; break;
            case TypeCode.Int32: case TypeCode.UInt32: case TypeCode.Single: Bytes += 4; break;
            case TypeCode.Int64: case TypeCode.UInt64: case TypeCode.Double: case TypeCode.DateTime: Bytes += 8; break;
            case TypeCode.Decimal: Bytes += 16; break;
            case TypeCode.Char: Bytes += Encoding.UTF8.GetByteCount(new[] { (char)value }); break;
            case TypeCode.String:
                string text = (string)value; int size;
                if (!strings.TryGetValue(text, out size))
                {
                    size = StringSize(text); strings.Add(text, size); UniqueStringBytes += size;
                }
                Bytes += size; StringBytes += size; StringOccurrences++;
                break;
            default:
                if (type == typeof(TimeSpan)) Bytes += 8;
                else if (type == typeof(Guid)) Bytes += 16;
                else throw new InvalidDataException("Unsupported audit literal " + type);
                break;
        }
    }
}
