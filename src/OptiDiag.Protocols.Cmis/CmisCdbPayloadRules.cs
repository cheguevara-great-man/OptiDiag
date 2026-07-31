namespace OptiDiag.Protocols.Cmis;

public sealed record CmisCdbPayloadRule(
    ushort CommandId,
    int MinimumLocalLength,
    int MaximumLocalLength,
    int MinimumExtendedLength,
    int MaximumExtendedLength,
    string CommandPayload,
    string ReplyPayload)
{
    public bool IsValid(CmisCdbCommand command, out string error)
    {
        if (command.LocalPayload.Length < MinimumLocalLength
            || command.LocalPayload.Length > MaximumLocalLength)
        {
            error =
                $"CMD 0x{CommandId:X4} LPL length must be {FormatRange(MinimumLocalLength, MaximumLocalLength)} byte(s), "
                + $"but received {command.LocalPayload.Length}.";
            return false;
        }

        if (command.ExtendedPayload.Length < MinimumExtendedLength
            || command.ExtendedPayload.Length > MaximumExtendedLength)
        {
            error =
                $"CMD 0x{CommandId:X4} EPL length must be {FormatRange(MinimumExtendedLength, MaximumExtendedLength)} byte(s), "
                + $"but received {command.ExtendedPayload.Length}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string FormatRange(int minimum, int maximum) =>
        minimum == maximum ? minimum.ToString() : $"{minimum}..{maximum}";
}

/// <summary>
/// Request/reply payload contracts from CMIS 5.3/5.4 chapter 9.
/// Variable lengths remain constrained by the base specification and by the
/// CDB LPL/EPL capacities; capability-query values may further restrict them.
/// </summary>
public static class CmisCdbPayloadRules
{
    public static IReadOnlyList<CmisCdbPayloadRule> Rules { get; } =
    [
        Exact(0x0000, 2, "ResponseDelay U16 (ms)", "Length U8; unlock Status U8"),
        Exact(0x0001, 4, "Password U32", "Empty"),
        Exact(0x0002, 4, "NewPassword U32", "Empty"),
        Empty(0x0004, "Empty"),
        Empty(0x0005, "Module time S64 in nanoseconds since POSIX epoch"),
        Exact(0x0006, 9, "TimeSpec S64; IsIncrement U8", "New module time S64"),

        Empty(0x0040, "CDB flags, command-support bitmap and MaxCompletionTime"),
        Empty(0x0041, "Firmware transfer mechanisms, sizes and maximum durations"),
        Empty(0x0042, "Performance/data-recording command-support bitmap"),
        Empty(0x0043, "BERT/diagnostics features and thresholds"),
        Empty(0x0044, "Certificate, digest and signature capabilities"),
        Empty(0x0045, "Externally defined feature advertisements"),
        Exact(0x0050, 2, "ApplicationNumber U16", "Application attributes and thresholds"),
        Exact(0x0051, 3, "InterfaceUID U12 in U16; InterfaceLocation U8",
            "Name, description, rate, modulation, exact bits/symbol and minimum grid"),

        Empty(0x0100, "Firmware status and information for images A, B and factory/boot"),
        Variable(0x0101, 8, 120, 0, 0, "ImageSize U32; reserved U32; VendorData[0..112]", "Empty"),
        Empty(0x0102, "Empty"),
        Variable(0x0103, 5, 120, 0, 0, "BlockAddress U32; FirmwareBlock[1..116]", "Empty"),
        Variable(0x0104, 4, 4, 1, 2048, "LPL BlockAddress U32; EPL FirmwareBlock", "Empty"),
        Exact(0x0105, 6, "BlockAddress U32; Length U16", "BlockAddress U32; ImageData[0..116] in LPL"),
        Exact(0x0106, 6, "BlockAddress U32; Length U16", "ImageData in EPL"),
        Empty(0x0107, "Empty"),
        Exact(0x0108, 1, "CopyDirection U8", "Length U32; CopyDirection U8; CopyStatus U8"),
        Exact(0x0109, 4, "Reserved U8; ImageToRun U8; DelayToReset U16", "Empty"),
        Empty(0x010A, "Empty"),
        Empty(0x010B, "TrafficImpact U8; ConfigImpact U8"),
        Exact(0x010C, 66, "FirmwareBank U8; Control U8; LoadTag ASCII[64]", "Empty"),
        Exact(0x010D, 1, "FirmwareBank U8", "FirmwareBank U8; reserved U8; LoadTag ASCII[64]"),

        Exact(0x0200, 4, "LinkMode and ClearAllStatistics controls", "Empty"),
        Empty(0x0201, "Host-side and media-side PM monitor advertisements"),
        Exact(0x0210, 5, "RecordType/ClearOnRead and module observable masks", "X16 PM records in LPL"),
        Exact(0x0211, 5, "RecordType/ClearOnRead and module observable masks", "X16 PM records in EPL"),
        Exact(0x0212, 20, "RecordType/ClearOnRead, host lane mask and observable masks", "Host-lane X16 PM records in LPL"),
        Exact(0x0213, 20, "RecordType/ClearOnRead, host lane mask and observable masks", "Host-lane X16 PM records in EPL"),
        Exact(0x0214, 20, "RecordType/ClearOnRead, media lane mask and observable masks", "Media-lane X16 PM records in LPL"),
        Exact(0x0215, 20, "RecordType/ClearOnRead, media lane mask and observable masks", "Media-lane X16 PM records in EPL"),
        Exact(0x0216, 20, "RecordType/ClearOnRead, data-path mask and observable masks", "Data-path X16 PM records in LPL"),
        Exact(0x0217, 20, "RecordType/ClearOnRead, data-path mask and observable masks", "Data-path X16 PM records in EPL"),
        Exact(0x0220, 2, "DPID U8; direction/location U8", "RMON frame/octet/packet counters"),
        Exact(0x0230, 2, "DPID U8; decoder location and histogram command", "Empty"),
        Exact(0x0231, 2, "DPID U8; decoder location", "FEC error-weight histogram and BER"),
        Exact(0x0232, 2, "DPID U8; decoder location and max-SEW command", "Empty"),
        Exact(0x0233, 2, "Reserved U8; decoder location", "Correction capability and per-DP maximum error weight"),

        Exact(0x0280, 4, "ClearAllStatistics control", "Empty"),
        Empty(0x0281, "Data monitoring/recording advertisement"),
        Exact(0x0290, 1, "Temperature histogram subcommands", "NVR timing and 10-degree temperature histogram"),
        Empty(0x0380, "Empty (reserved loopback extension command)"),

        Exact(0x0400, 2, "CertificateIndex U8; SegmentIndex U8", "ReturnStatus, SegmentIndex and certificate segment in LPL"),
        Exact(0x0401, 1, "CertificateIndex U8", "ReturnStatus and CertificateLength in LPL; certificate in EPL"),
        Variable(0x0402, 1, 120, 0, 0, "Digest data in LPL", "ReturnStatus U16"),
        Variable(0x0403, 0, 0, 1, 2048, "Digest data in EPL", "ReturnStatus U16"),
        Exact(0x0404, 1, "SegmentIndex U8", "ReturnStatus, SegmentIndex and signature segment in LPL"),
        Empty(0x0405, "ReturnStatus and SignatureLength in LPL; signature in EPL")
    ];

    public static CmisCdbPayloadRule? Find(ushort commandId) =>
        Rules.FirstOrDefault(rule => rule.CommandId == commandId);

    public static void Validate(CmisCdbCommand command)
    {
        var rule = Find(command.CommandId);
        if (rule is not null && !rule.IsValid(command, out var error))
        {
            throw new ArgumentException(error, nameof(command));
        }
    }

    private static CmisCdbPayloadRule Empty(ushort id, string reply) =>
        new(id, 0, 0, 0, 0, "Empty", reply);

    private static CmisCdbPayloadRule Exact(ushort id, int localLength, string command, string reply) =>
        new(id, localLength, localLength, 0, 0, command, reply);

    private static CmisCdbPayloadRule Variable(
        ushort id,
        int minimumLocalLength,
        int maximumLocalLength,
        int minimumExtendedLength,
        int maximumExtendedLength,
        string command,
        string reply) =>
        new(id, minimumLocalLength, maximumLocalLength, minimumExtendedLength, maximumExtendedLength, command, reply);
}
