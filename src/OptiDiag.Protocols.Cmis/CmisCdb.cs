using System.Buffers.Binary;

namespace OptiDiag.Protocols.Cmis;

public enum CmisCdbSupport
{
    Required,
    Advertised,
    Optional
}

public sealed record CmisCdbCommandDefinition(
    ushort Id,
    string Title,
    string Group,
    CmisCdbSupport Support,
    string Section);

/// <summary>
/// All command IDs explicitly defined by the CMIS 5.3 base document.
/// Reserved ranges, custom commands and supplement-defined VCS commands are
/// intentionally not presented as base CMIS commands.
/// </summary>
public static class CmisCdbCommandCatalog
{
    public static IReadOnlyList<CmisCdbCommandDefinition> Commands { get; } =
    [
        Cmd(0x0000, "Query Status", "Module", CmisCdbSupport.Required, "9.3.1"),
        Cmd(0x0001, "Enter Password", "Module", CmisCdbSupport.Advertised, "9.3.2"),
        Cmd(0x0002, "Change Password", "Module", CmisCdbSupport.Advertised, "9.3.3"),
        Cmd(0x0004, "Abort Processing", "Module", CmisCdbSupport.Advertised, "9.3.4"),

        Cmd(0x0040, "Module Features", "Capabilities Inquiry", CmisCdbSupport.Required, "9.4.1"),
        Cmd(0x0041, "Firmware Management Features", "Capabilities Inquiry", CmisCdbSupport.Required, "9.4.2"),
        Cmd(0x0042, "Performance Monitoring Features", "Capabilities Inquiry", CmisCdbSupport.Required, "9.4.3"),
        Cmd(0x0043, "BERT and Diagnostics Features", "Capabilities Inquiry", CmisCdbSupport.Required, "9.4.4"),
        Cmd(0x0044, "Security Features and Capabilities", "Capabilities Inquiry", CmisCdbSupport.Required, "9.4.5"),
        Cmd(0x0045, "Externally Defined Features", "Capabilities Inquiry", CmisCdbSupport.Required, "9.4.6"),
        Cmd(0x0050, "Get Application Attributes", "Capabilities Inquiry", CmisCdbSupport.Advertised, "9.4.7"),
        Cmd(0x0051, "Get Interface Code Description", "Capabilities Inquiry", CmisCdbSupport.Advertised, "9.4.8"),

        Cmd(0x0100, "Get Firmware Info", "Firmware Management", CmisCdbSupport.Advertised, "9.7.1"),
        Cmd(0x0101, "Start Firmware Download", "Firmware Management", CmisCdbSupport.Advertised, "9.7.2"),
        Cmd(0x0102, "Abort Firmware Download", "Firmware Management", CmisCdbSupport.Advertised, "9.7.3"),
        Cmd(0x0103, "Write Firmware Block LPL", "Firmware Management", CmisCdbSupport.Advertised, "9.7.4"),
        Cmd(0x0104, "Write Firmware Block EPL", "Firmware Management", CmisCdbSupport.Advertised, "9.7.5"),
        Cmd(0x0105, "Read Firmware Block LPL", "Firmware Management", CmisCdbSupport.Advertised, "9.7.6"),
        Cmd(0x0106, "Read Firmware Block EPL", "Firmware Management", CmisCdbSupport.Advertised, "9.7.7"),
        Cmd(0x0107, "Complete Firmware Download", "Firmware Management", CmisCdbSupport.Advertised, "9.7.8"),
        Cmd(0x0108, "Copy Firmware Image", "Firmware Management", CmisCdbSupport.Advertised, "9.7.9"),
        Cmd(0x0109, "Run Firmware Image", "Firmware Management", CmisCdbSupport.Advertised, "9.7.10"),
        Cmd(0x010A, "Commit Firmware Image", "Firmware Management", CmisCdbSupport.Advertised, "9.7.11"),

        Cmd(0x0200, "Control PM", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.1"),
        Cmd(0x0201, "Get PM Feature Information", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.2"),
        Cmd(0x0210, "Get Module PM LPL", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.3"),
        Cmd(0x0211, "Get Module PM EPL", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.3"),
        Cmd(0x0212, "Get PM Host Side LPL", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.4"),
        Cmd(0x0213, "Get PM Host Side EPL", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.4"),
        Cmd(0x0214, "Get PM Media Side LPL", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.5"),
        Cmd(0x0215, "Get PM Media Side EPL", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.5"),
        Cmd(0x0216, "Get Data Path PM LPL", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.6"),
        Cmd(0x0217, "Get Data Path PM EPL", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.6"),
        Cmd(0x0220, "Get Data Path RMON Statistics", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.7"),
        Cmd(0x0230, "Control FEC Symbol Error Weight Histogram", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.8"),
        Cmd(0x0231, "Get FEC Symbol Error Weight Histogram", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.9"),
        Cmd(0x0232, "Control Max FEC Symbol Error Weight", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.10"),
        Cmd(0x0233, "Get Max FEC Symbol Error Weight", "Performance Monitoring", CmisCdbSupport.Advertised, "9.8.11"),

        Cmd(0x0280, "Data Monitoring and Recording Controls", "Data Recording", CmisCdbSupport.Advertised, "9.9.1"),
        Cmd(0x0281, "Data Monitoring and Recording Advertisement", "Data Recording", CmisCdbSupport.Advertised, "9.9.2"),
        Cmd(0x0290, "Temperature Histogram", "Data Recording", CmisCdbSupport.Advertised, "9.9.3"),
        Cmd(0x0380, "Loopbacks", "Diagnostics and Debug", CmisCdbSupport.Advertised, "9.11.1"),

        Cmd(0x0400, "Get Initial Device ID Certificate in LPL", "Security", CmisCdbSupport.Advertised, "9.12.2"),
        Cmd(0x0401, "Get Initial Device ID Certificate in EPL", "Security", CmisCdbSupport.Advertised, "9.12.3"),
        Cmd(0x0402, "Set Digest To Sign given in LPL", "Security", CmisCdbSupport.Advertised, "9.12.4"),
        Cmd(0x0403, "Set Digest To Sign given in EPL", "Security", CmisCdbSupport.Advertised, "9.12.5"),
        Cmd(0x0404, "Get Digest Signature in LPL", "Security", CmisCdbSupport.Advertised, "9.12.6"),
        Cmd(0x0405, "Get Digest Signature in EPL", "Security", CmisCdbSupport.Advertised, "9.12.7")
    ];

    public static CmisCdbCommandDefinition? Find(ushort id) =>
        Commands.FirstOrDefault(command => command.Id == id);

    private static CmisCdbCommandDefinition Cmd(
        ushort id,
        string title,
        string group,
        CmisCdbSupport support,
        string section) =>
        new(id, title, group, support, section);
}

public sealed record CmisCdbCommand(
    ushort CommandId,
    byte[] LocalPayload,
    byte[] ExtendedPayload)
{
    public CmisCdbCommand(ushort commandId, byte[]? localPayload = null)
        : this(commandId, localPayload ?? [], [])
    {
    }
}

public sealed record CmisCdbReply(
    ushort CommandId,
    byte EncodedReplyLength,
    byte[] LocalPayload,
    byte[] ExtendedPayload,
    byte StoredCheckCode,
    byte ComputedCheckCode,
    bool CheckCodeValid,
    CmisCdbCommandDefinition? Definition);

/// <summary>
/// Pure CMIS 5.3 CDB wire codec. Hardware transports can write EPL first and
/// Page 9Fh last; writing CMDID is the command trigger.
/// </summary>
public static class CmisCdbCodec
{
    public const int PageSize = 128;
    public const int LocalPayloadCapacity = 120;
    public const int MaximumExtendedPayloadLength = 2048;

    public static byte[] EncodeMessagePage(CmisCdbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        CmisCdbPayloadRules.Validate(command);
        if (command.LocalPayload.Length > LocalPayloadCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                $"CDB local payload cannot exceed {LocalPayloadCapacity} bytes.");
        }

        if (command.ExtendedPayload.Length > MaximumExtendedPayloadLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                $"CDB extended payload cannot exceed {MaximumExtendedPayloadLength} bytes.");
        }

        var page = new byte[PageSize];
        BinaryPrimitives.WriteUInt16BigEndian(page.AsSpan(0, 2), command.CommandId);
        BinaryPrimitives.WriteUInt16BigEndian(page.AsSpan(2, 2), (ushort)command.ExtendedPayload.Length);
        page[4] = (byte)command.LocalPayload.Length;
        command.LocalPayload.CopyTo(page, 8);
        page[5] = ComputeCommandCheckCode(page);
        return page;
    }

    public static IReadOnlyList<byte[]> EncodeExtendedPayloadPages(ReadOnlySpan<byte> extendedPayload)
    {
        if (extendedPayload.Length > MaximumExtendedPayloadLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(extendedPayload),
                $"CDB extended payload cannot exceed {MaximumExtendedPayloadLength} bytes.");
        }

        var count = (extendedPayload.Length + PageSize - 1) / PageSize;
        var pages = new List<byte[]>(count);
        for (var index = 0; index < count; index++)
        {
            var page = new byte[PageSize];
            var source = extendedPayload.Slice(index * PageSize, Math.Min(PageSize, extendedPayload.Length - index * PageSize));
            source.CopyTo(page);
            pages.Add(page);
        }

        return pages;
    }

    public static byte ComputeCommandCheckCode(ReadOnlySpan<byte> messagePage)
    {
        EnsureMessagePage(messagePage);
        var localLength = messagePage[4];
        if (localLength > LocalPayloadCapacity)
        {
            throw new InvalidDataException($"Invalid CDB LPLLength {localLength}.");
        }

        var sum = messagePage[0] + messagePage[1] + messagePage[2] + messagePage[3] + messagePage[4];
        for (var index = 0; index < localLength; index++)
        {
            sum += messagePage[8 + index];
        }

        return unchecked((byte)~sum);
    }

    public static CmisCdbReply DecodeReply(
        ReadOnlySpan<byte> messagePage,
        ReadOnlySpan<byte> extendedPayload = default)
    {
        EnsureMessagePage(messagePage);
        var commandId = BinaryPrimitives.ReadUInt16BigEndian(messagePage[..2]);
        var encodedLength = messagePage[6];
        var storedCheckCode = messagePage[7];
        byte[] local;
        byte[] extended;

        if (encodedLength <= LocalPayloadCapacity)
        {
            local = messagePage.Slice(8, encodedLength).ToArray();
            extended = [];
        }
        else if (encodedLength >= 240)
        {
            var pageCount = encodedLength - 239;
            var advertisedCapacity = pageCount * PageSize;
            if (extendedPayload.Length == 0 || extendedPayload.Length > advertisedCapacity)
            {
                throw new InvalidDataException(
                    $"Reply advertises {pageCount} EPL page(s); provide between 1 and {advertisedCapacity} payload bytes.");
            }

            local = [];
            extended = extendedPayload.ToArray();
        }
        else
        {
            throw new InvalidDataException($"Reserved CDB RPLLength encoding 0x{encodedLength:X2}.");
        }

        var checkInput = local.Length > 0 ? local : extended;
        var computed = ComputeReplyCheckCode(checkInput);
        return new CmisCdbReply(
            commandId,
            encodedLength,
            local,
            extended,
            storedCheckCode,
            computed,
            storedCheckCode == computed,
            CmisCdbCommandCatalog.Find(commandId));
    }

    public static byte ComputeReplyCheckCode(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            return 0;
        }

        var sum = 0;
        foreach (var value in payload)
        {
            sum += value;
        }

        return unchecked((byte)~sum);
    }

    private static void EnsureMessagePage(ReadOnlySpan<byte> messagePage)
    {
        if (messagePage.Length < PageSize)
        {
            throw new ArgumentException($"CDB Page 9Fh must contain {PageSize} bytes.", nameof(messagePage));
        }
    }
}
