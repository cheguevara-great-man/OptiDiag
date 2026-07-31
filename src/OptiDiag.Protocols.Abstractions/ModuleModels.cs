namespace OptiDiag.Protocols.Abstractions;

public enum RegisterAccess
{
    ReadOnly,
    ReadOnlyClearOnRead,
    ReadWrite,
    ReadWriteSelfClearing,
    WriteOnly,
    WriteOnlySelfClearing,
    Mixed,
    Reserved,
    VendorSpecific
}

public enum DiagnosticSeverity
{
    Information,
    Warning,
    Error
}

public sealed record MemoryCaptureRegion(
    string Id,
    string DisplayName,
    byte DeviceAddress,
    byte Offset,
    int Length,
    byte? Page = null,
    byte? Bank = null,
    byte? PageSelectOffset = null,
    byte? BankSelectOffset = null,
    bool Optional = false,
    bool Volatile = false);

public sealed record MemoryRegionData(
    string Id,
    string DisplayName,
    byte DeviceAddress,
    byte Offset,
    byte[] Data,
    byte? Page = null,
    byte? Bank = null,
    bool Volatile = false)
{
    public int EndOffset => Offset + Data.Length - 1;
}

public sealed record ModuleDump(
    int FormatVersion,
    string ProtocolId,
    string ProtocolRevision,
    DateTimeOffset CapturedAt,
    string Source,
    IReadOnlyList<MemoryRegionData> Regions,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public MemoryRegionData? FindRegion(string id) =>
        Regions.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
}

public sealed record ModuleInformation(
    string Identifier,
    string Connector,
    string VendorName,
    string VendorOui,
    string PartNumber,
    string Revision,
    string SerialNumber,
    string DateCode,
    double? WavelengthNm,
    double? NominalSignalingRateMbd,
    string Encoding,
    string Compliance,
    bool DigitalDiagnosticsImplemented,
    bool InternallyCalibrated,
    bool ExternallyCalibrated);

public sealed record Measurement(
    string Id,
    string Name,
    double Value,
    string Unit,
    ushort RawValue,
    double? HighAlarm = null,
    double? HighWarning = null,
    double? LowWarning = null,
    double? LowAlarm = null)
{
    public string FormattedValue => double.IsFinite(Value) ? $"{Value:0.###} {Unit}" : "无效";

    public string? SecondaryFormattedValue =>
        (Id.StartsWith("tx-power", StringComparison.Ordinal)
         || Id.StartsWith("rx-power", StringComparison.Ordinal))
        && double.IsFinite(Value) && Value > 0
            ? $"{10 * Math.Log10(Value):0.###} dBm"
            : null;
}

public sealed record ThresholdRow(
    string Id,
    string Name,
    string Unit,
    double HighAlarm,
    double HighWarning,
    double LowWarning,
    double LowAlarm);

public sealed record AlarmFlag(
    string Id,
    string Name,
    string Level,
    bool IsActive,
    string SourceRegister);

public sealed record StatusItem(string Name, string Value, string SourceRegister, string Description = "");

public sealed record ProtocolExtensionInfo(
    string Id,
    string DisplayName,
    string Revision,
    bool IsPresent,
    string Evidence);

public sealed record ProtocolIdentification(
    string BaseProtocolId,
    string BaseProtocolName,
    string Revision,
    string DetectionEvidence,
    IReadOnlyList<ProtocolExtensionInfo> Extensions)
{
    public string ExtensionSummary
    {
        get
        {
            var present = Extensions.Where(x => x.IsPresent).Select(x => x.DisplayName).ToArray();
            return present.Length == 0 ? "无扩展协议" : string.Join("、", present);
        }
    }
}

public sealed record DecodedField(
    string Category,
    string Name,
    string Value,
    string SourceRegister,
    string Description = "",
    string Unit = "",
    bool IsWritable = false);

public sealed record RegisterValue(
    string RegionId,
    byte DeviceAddress,
    byte? Page,
    int Offset,
    byte Value,
    string Name,
    string Description,
    RegisterAccess Access,
    bool IsVolatile = false,
    byte? Bank = null)
{
    public string AddressText => Page.HasValue && Bank.HasValue
        ? $"0x{DeviceAddress:X2} B{Bank:X2} P{Page:X2}:{Offset:X2}"
        : Page.HasValue
            ? $"0x{DeviceAddress:X2} P{Page:X2}:{Offset:X2}"
        : $"0x{DeviceAddress:X2}:{Offset:X2}";

    public string HexValue => $"{Value:X2}";

    public char Ascii => Value is >= 32 and <= 126 ? (char)Value : '.';
}

public sealed record DecodeDiagnostic(DiagnosticSeverity Severity, string Message, string? RegionId = null);

public sealed record DecodedModule(
    ModuleInformation Information,
    IReadOnlyList<Measurement> Measurements,
    IReadOnlyList<ThresholdRow> Thresholds,
    IReadOnlyList<AlarmFlag> Alarms,
    IReadOnlyList<StatusItem> Status,
    IReadOnlyList<RegisterValue> Registers,
    IReadOnlyList<DecodeDiagnostic> Diagnostics,
    ProtocolIdentification? Protocol = null,
    IReadOnlyList<DecodedField>? Fields = null,
    DecodedModule? RemoteModule = null);

public interface IOpticalModuleProtocol
{
    string Id { get; }

    string DisplayName { get; }

    string Revision { get; }

    IReadOnlyList<MemoryCaptureRegion> CapturePlan { get; }

    bool ShouldCaptureRegion(
        MemoryCaptureRegion region,
        IReadOnlyList<MemoryRegionData> capturedRegions) => true;

    bool CanDecode(ModuleDump dump);

    DecodedModule Decode(ModuleDump dump);
}

/// <summary>
/// Optional protocol capability for resolving a module-declared revision after
/// memory has been captured. This keeps Dump metadata accurate for protocols
/// whose decoder supports more than one revision.
/// </summary>
public interface ICapturedRevisionProvider
{
    string ResolveRevision(IReadOnlyList<MemoryRegionData> capturedRegions);
}
