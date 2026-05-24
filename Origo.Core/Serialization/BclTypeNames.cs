namespace Origo.Core.Serialization;

/// <summary>
///     Stable JSON type discriminator strings for built-in CLR types in <see cref="TypeStringMapping" />.
/// </summary>
internal static class BclTypeNames
{
    internal const string Byte = "Byte";
    internal const string SByte = "SByte";
    internal const string Int16 = "Int16";
    internal const string UInt16 = "UInt16";
    internal const string Int32 = "Int32";
    internal const string UInt32 = "UInt32";
    internal const string Int64 = "Int64";
    internal const string UInt64 = "UInt64";
    internal const string Boolean = "Boolean";
    internal const string Single = "Single";
    internal const string Double = "Double";
    internal const string Decimal = "Decimal";
    internal const string Char = "Char";
    internal const string String = "String";

    // Array types
    internal const string ArrayByte = "ArrayByte";
    internal const string ArraySByte = "ArraySByte";
    internal const string ArrayInt16 = "ArrayInt16";
    internal const string ArrayUInt16 = "ArrayUInt16";
    internal const string ArrayInt32 = "ArrayInt32";
    internal const string ArrayUInt32 = "ArrayUInt32";
    internal const string ArrayInt64 = "ArrayInt64";
    internal const string ArrayUInt64 = "ArrayUInt64";
    internal const string ArraySingle = "ArraySingle";
    internal const string ArrayDouble = "ArrayDouble";
    internal const string ArrayDecimal = "ArrayDecimal";
    internal const string ArrayBoolean = "ArrayBoolean";
    internal const string ArrayChar = "ArrayChar";
    internal const string ArrayString = "ArrayString";
}
