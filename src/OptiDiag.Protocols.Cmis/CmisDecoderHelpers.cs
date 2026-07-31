using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Cmis;

internal static class CmisDecoderHelpers
{
    public static ushort U16(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(source.Slice(offset, 2));

    public static short S16(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadInt16BigEndian(source.Slice(offset, 2));

    public static uint U32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(source.Slice(offset, 4));

    public static int S32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadInt32BigEndian(source.Slice(offset, 4));

    public static string Ascii(byte[] source, int offset, int length) =>
        Encoding.ASCII.GetString(source, offset, length).Trim(' ', '\0', '\xFF');

    public static bool Bit(byte value, int bit) => (value & (1 << bit)) != 0;

    public static int Bits(byte value, int high, int low)
    {
        var width = high - low + 1;
        var mask = (1 << width) - 1;
        return (value >> low) & mask;
    }

    public static string Bool(bool value) => value ? "是" : "否";

    public static string Hex(ReadOnlySpan<byte> value) =>
        string.Join(' ', value.ToArray().Select(x => $"{x:X2}"));

    /// <summary>
    /// CMIS uses IEEE-754 binary16 for many VDM observables.
    /// System.Half performs the required conversion without losing NaN/Infinity semantics.
    /// </summary>
    public static double F16(ReadOnlySpan<byte> source, int offset)
    {
        var bits = U16(source, offset);
        return (double)BitConverter.UInt16BitsToHalf(bits);
    }

    public static string FormatNumber(double value, string unit = "", int digits = 4)
    {
        var number = double.IsFinite(value)
            ? value.ToString($"0.{new string('#', digits)}", CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(unit) ? number : $"{number} {unit}";
    }

    public static string Source(byte page, int absoluteOffset, byte? bank = null, string? bits = null)
    {
        var prefix = bank.HasValue ? $"B{bank:X2}/P{page:X2}h" : $"P{page:X2}h";
        return bits is null ? $"{prefix}.{absoluteOffset}" : $"{prefix}.{absoluteOffset}.{bits}";
    }
}

internal sealed class CmisFieldSink
{
    private readonly List<DecodedField> _fields;

    public CmisFieldSink(List<DecodedField> fields)
    {
        _fields = fields;
    }

    public void Add(
        string category,
        string name,
        object? value,
        string source,
        string description = "",
        string unit = "",
        bool writable = false) =>
        _fields.Add(new DecodedField(
            category,
            name,
            value?.ToString() ?? "",
            source,
            description,
            unit,
            writable));

    public void Bool(
        string category,
        string name,
        byte value,
        int bit,
        string source,
        string description = "",
        bool writable = false) =>
        Add(category, name, CmisDecoderHelpers.Bool(CmisDecoderHelpers.Bit(value, bit)), source, description, writable: writable);

    public void Enum(
        string category,
        string name,
        int value,
        Func<int, string> formatter,
        string source,
        string description = "",
        bool writable = false) =>
        Add(category, name, $"{formatter(value)} (0x{value:X})", source, description, writable: writable);

    public void Hex(
        string category,
        string name,
        ReadOnlySpan<byte> value,
        string source,
        string description = "",
        bool writable = false) =>
        Add(category, name, CmisDecoderHelpers.Hex(value), source, description, writable: writable);
}
