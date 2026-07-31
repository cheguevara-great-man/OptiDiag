namespace OptiDiag.Protocols.Cmis;

/// <summary>
/// Version byte used by CMIS Lower Memory byte 1. The high and low nibbles
/// are the major and minor revision respectively (for example 0x54 = 5.4).
/// </summary>
public readonly record struct CmisRevision(byte Raw) : IComparable<CmisRevision>
{
    public static CmisRevision V53 { get; } = new(0x53);
    public static CmisRevision V54 { get; } = new(0x54);

    public int Major => Raw >> 4;
    public int Minor => Raw & 0x0F;
    public bool IsAtLeast54 => CompareTo(V54) >= 0;

    public int CompareTo(CmisRevision other) => Raw.CompareTo(other.Raw);
    public override string ToString() => $"{Major}.{Minor}";

    public static CmisRevision FromLowerMemory(ReadOnlySpan<byte> lower) =>
        new(lower.Length > 1 ? lower[1] : (byte)0);
}
