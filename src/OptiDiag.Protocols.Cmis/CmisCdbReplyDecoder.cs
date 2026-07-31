using System.Buffers.Binary;
using System.Text;

namespace OptiDiag.Protocols.Cmis;

public sealed record CmisCdbDecodedValue(string Name, string Value, string Description = "");

/// <summary>
/// Semantic decoder for reply layouts defined in CMIS 5.3/5.4 chapter 9.
/// Empty-reply commands intentionally produce no payload fields.
/// </summary>
public static class CmisCdbReplyDecoder
{
    public static IReadOnlyList<CmisCdbDecodedValue> Decode(CmisCdbCommand command, CmisCdbReply reply)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(reply);
        var data = reply.LocalPayload.Length > 0 ? reply.LocalPayload : reply.ExtendedPayload;
        var values = new List<CmisCdbDecodedValue>();

        switch (command.CommandId)
        {
            case 0x0000:
                Add(values, data, 0, "Length", value => value.ToString());
                Add(values, data, 1, "UnlockStatus", value =>
                    (value & 0x80) != 0 ? "Module password accepted" :
                    value == 1 ? "Host password accepted" : "Module boot-up");
                break;
            case 0x0005:
            case 0x0006:
                DecodeModuleTime(values, data);
                break;
            case 0x0040:
                DecodeCommandBitmap(values, data, 2, 32, 0x0000);
                AddU16(values, data, 34, "MaxCompletionTime", "ms");
                break;
            case 0x0041:
                DecodeFirmwareFeatures(values, data);
                break;
            case 0x0042:
                DecodeCommandBitmap(values, data, 0, Math.Min(32, data.Length), 0x0200);
                break;
            case 0x0043:
                DecodeCommandBitmap(values, data, 0, Math.Min(32, data.Length), 0x0300);
                break;
            case 0x0044:
                DecodeSecurityFeatures(values, data);
                break;
            case 0x0045:
                Add(values, data, 0, "CMIS-VCS Supported", value => Bool((value & 1) != 0));
                break;
            case 0x0050:
                DecodeApplicationAttributes(values, command, data);
                break;
            case 0x0051:
                DecodeInterfaceDescription(values, data);
                break;
            case 0x0100:
                DecodeFirmwareInfo(values, data);
                break;
            case 0x0108:
                AddU32(values, data, 0, "BytesCopied");
                Add(values, data, 4, "CopyDirection", value => $"0x{value:X2}");
                Add(values, data, 5, "CopyStatus", value => value == 0 ? "Success" : $"0x{value:X2}");
                break;
            case 0x010B:
                DecodeFirmwareActivationOptions(values, data);
                break;
            case 0x010D:
                Add(values, data, 0, "FirmwareBank", value => value switch
                {
                    0 => "Bank A",
                    1 => "Bank B",
                    2 => "Fixed load",
                    _ => $"Reserved 0x{value:X2}"
                });
                AddAscii(values, data, 2, 64, "FirmwareLoadTag");
                break;
            case >= 0x0210 and <= 0x0217:
                DecodePerformanceRecords(values, command, data);
                break;
            case 0x0201:
                Add(values, data, 0, "HostSideMonitors", value => $"0x{value:X2}");
                Add(values, data, 1, "MediaSideMonitors", value => $"0x{value:X2}");
                break;
            case 0x0220:
                DecodeRmon(values, data);
                break;
            case 0x0231:
                DecodeFecHistogram(values, data);
                break;
            case 0x0233:
                AddU16(values, data, 2, "CorrectionCapability");
                for (var index = 0; index < 8; index++)
                {
                    AddU16(values, data, 4 + index * 2, $"MaxErrorWeightDPID{index + 1}");
                }
                break;
            case 0x0290:
                DecodeTemperatureHistogram(values, data);
                break;
            case 0x0400:
            case 0x0404:
                AddSecurityStatus(values, data);
                Add(values, data, 2, "SegmentIndex", value => value.ToString());
                if (data.Length > 3)
                {
                    values.Add(new("Segment", Hex(data.AsSpan(3))));
                }
                break;
            case 0x0401:
                AddSecurityStatus(values, reply.LocalPayload);
                AddU16(values, reply.LocalPayload, 2, "CertificateLength", "bytes");
                values.Add(new("Certificate", $"{reply.ExtendedPayload.Length} EPL bytes"));
                break;
            case 0x0402:
            case 0x0403:
                AddSecurityStatus(values, data);
                break;
            case 0x0405:
                AddSecurityStatus(values, reply.LocalPayload);
                AddU16(values, reply.LocalPayload, 2, "SignatureLength", "bytes");
                values.Add(new("Signature", $"{reply.ExtendedPayload.Length} EPL bytes"));
                break;
            default:
                if (data.Length > 0)
                {
                    values.Add(new("RawReply", Hex(data)));
                }
                break;
        }

        return values;
    }

    private static void DecodeFirmwareFeatures(ICollection<CmisCdbDecodedValue> values, byte[] data)
    {
        Add(values, data, 1, "ImageReadback", value => Bool((value & 0x80) != 0));
        Add(values, data, 1, "SkippingErasedBlocks", value => Bool((value & 0x04) != 0));
        Add(values, data, 1, "CopyCommand", value => Bool((value & 0x02) != 0));
        Add(values, data, 1, "AbortCommand", value => Bool((value & 0x01) != 0));
        Add(values, data, 2, "StartCmdPayloadSize", value => $"{value} bytes");
        Add(values, data, 3, "ErasedByte", value => $"0x{value:X2}");
        Add(values, data, 4, "ReadWriteLengthExtension", value => value.ToString());
        Add(values, data, 5, "WriteMechanism", Mechanism);
        Add(values, data, 6, "ReadMechanism", Mechanism);
        Add(values, data, 7, "HitlessRestart", value => Bool(value != 0));
        var multiplier = data.Length > 1 && (data[1] & 0x08) != 0 ? 10 : 1;
        var names = new[] { "MaxDurationStart", "MaxDurationAbort", "MaxDurationWrite", "MaxDurationComplete", "MaxDurationCopy" };
        for (var index = 0; index < names.Length; index++)
        {
            if (TryU16(data, 8 + index * 2, out var duration))
            {
                values.Add(new(names[index], $"{duration * multiplier} ms"));
            }
        }
    }

    private static void DecodeSecurityFeatures(ICollection<CmisCdbDecodedValue> values, byte[] data)
    {
        DecodeCommandBitmap(values, data, 0, Math.Min(32, data.Length), 0x0400);
        Add(values, data, 32, "NumCertificates", value => value.ToString());
        Add(values, data, 33, "CertificateChainSupported", value => Bool(value != 0));
        Add(values, data, 34, "CertificateFormat", value => value switch
        {
            0 => "Not supported",
            1 => "Custom",
            2 => "X.509 v3 DER",
            _ => $"Reserved 0x{value:X2}"
        });
        for (var index = 0; index < 4; index++)
        {
            AddU16(values, data, 36 + index * 2, $"CertificateLength{index + 1}", "bytes");
        }

        Add(values, data, 44, "DigestLength", value => value switch
        {
            0 => "Not supported",
            1 => "28 bytes (SHA-224)",
            2 => "32 bytes (SHA-256)",
            3 => "48 bytes (SHA-384)",
            4 => "64 bytes (SHA-512)",
            _ => $"Reserved 0x{value:X2}"
        });
        AddU16(values, data, 46, "SignatureTime", "ms");
        AddU16(values, data, 48, "SignatureLength", "bytes");
        Add(values, data, 50, "SignatureFormat", value => $"0x{value:X2}");
        Add(values, data, 51, "SignaturePadScheme", value => $"0x{value:X2}");
    }

    private static void DecodeApplicationAttributes(
        ICollection<CmisCdbDecodedValue> values,
        CmisCdbCommand command,
        byte[] data)
    {
        var requestsAllApplications = command.LocalPayload.Length >= 2
            && BinaryPrimitives.ReadUInt16BigEndian(command.LocalPayload.AsSpan(0, 2)) == 0;
        if (requestsAllApplications && data.Length > 120)
        {
            for (var page = 0; page < Math.Min(15, data.Length / 128); page++)
            {
                DecodeApplicationAttributeRecord(values, data.AsSpan(page * 128 + 8, 20), $"App{page + 1}.");
            }

            return;
        }

        DecodeApplicationAttributeRecord(values, data, string.Empty);
    }

    private static void DecodeApplicationAttributeRecord(
        ICollection<CmisCdbDecodedValue> values,
        ReadOnlySpan<byte> record,
        string prefix)
    {
        var data = record.ToArray();
        AddU16(values, data, 0, $"{prefix}ApplicationNumber");
        AddU16(values, data, 2, $"{prefix}MaxModulePower", "mW");
        AddS16(values, data, 4, $"{prefix}ProgOutputPowerMin", 0.01, "dBm");
        AddS16(values, data, 6, $"{prefix}ProgOutputPowerMax", 0.01, "dBm");
        AddHalf(values, data, 8, $"{prefix}PreFECBERThreshold");
        AddS16(values, data, 10, $"{prefix}RxLOSOpticalPowerThreshold", 0.01, "dBm");
        AddOpticalPower(values, data, 12, $"{prefix}RxPowerHighAlarmThreshold");
        AddOpticalPower(values, data, 14, $"{prefix}RxPowerLowAlarmThreshold");
        AddOpticalPower(values, data, 16, $"{prefix}RxPowerHighWarningThreshold");
        AddOpticalPower(values, data, 18, $"{prefix}RxPowerLowWarningThreshold");
    }

    private static void DecodeInterfaceDescription(ICollection<CmisCdbDecodedValue> values, byte[] data)
    {
        if (TryU16(data, 0, out var interfaceUid))
        {
            values.Add(new("InterfaceUID", $"0x{interfaceUid & 0x0FFF:X3}",
                $"GID={(interfaceUid >> 8) & 0x0F}; ID={interfaceUid & 0xFF}"));
        }
        Add(values, data, 2, "InterfaceLocation", value => value == 0 ? "Media side" : "Host side");
        AddAscii(values, data, 3, 16, "InterfaceName");
        AddAscii(values, data, 19, 48, "InterfaceDescription");
        AddHalf(values, data, 68, "InterfaceDataRate", "Gb/s");
        AddU16(values, data, 70, "InterfaceLaneCount");
        AddHalf(values, data, 72, "LaneSignalingRate", "GBd");
        AddAscii(values, data, 74, 16, "Modulation");
        AddU16(values, data, 90, "BitsPerSymbol");
        AddHalf(values, data, 92, "BitsPerSymbolExact");
        AddHalf(values, data, 94, "GridSpacingMin", "GHz");
    }

    private static void DecodeModuleTime(ICollection<CmisCdbDecodedValue> values, byte[] data)
    {
        if (data.Length < 8)
        {
            return;
        }

        var nanoseconds = BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(0, 8));
        values.Add(new("ModuleTimeNanoseconds", nanoseconds.ToString(), "Nanoseconds since POSIX epoch"));
        try
        {
            var seconds = Math.DivRem(nanoseconds, 1_000_000_000, out var remainder);
            var timestamp = DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(remainder / 100);
            values.Add(new("ModuleTimeUtc", timestamp.ToString("O")));
        }
        catch (ArgumentOutOfRangeException)
        {
            values.Add(new("ModuleTimeUtc", "Out of DateTimeOffset range"));
        }
    }

    private static void DecodeFirmwareActivationOptions(
        ICollection<CmisCdbDecodedValue> values,
        byte[] data)
    {
        Add(values, data, 0, "RestartActiveLoadHitlessAvailable", value => Bool((value & 0x01) != 0));
        Add(values, data, 0, "SwitchToInactiveLoadHitlessAvailable", value => Bool((value & 0x02) != 0));
        Add(values, data, 1, "RestartPreservesConfiguration", value => Bool((value & 0x01) != 0));
        Add(values, data, 1, "SwitchPreservesConfiguration", value => Bool((value & 0x02) != 0));
    }

    private static void DecodeFirmwareInfo(ICollection<CmisCdbDecodedValue> values, byte[] data)
    {
        Add(values, data, 0, "FirmwareStatus", value => $"0x{value:X2}",
            "A/B running, committed and invalid status bits");
        Add(values, data, 1, "ImageInformation", value => $"0x{value:X2}");
        AddFirmwareVersion(values, data, 2, "ImageA");
        AddFirmwareVersion(values, data, 38, "ImageB");
        AddFirmwareVersion(values, data, 74, "FactoryBoot");
    }

    private static void DecodePerformanceRecords(
        ICollection<CmisCdbDecodedValue> values,
        CmisCdbCommand command,
        byte[] data)
    {
        var includeCurrent = command.LocalPayload.Length > 0 && (command.LocalPayload[0] & 1) != 0;
        var valuesPerRecord = includeCurrent ? 4 : 3;
        var recordSize = valuesPerRecord * 2;
        for (var offset = 0; offset + recordSize <= data.Length; offset += recordSize)
        {
            var record = offset / recordSize + 1;
            var parts = new List<string>(valuesPerRecord);
            for (var index = 0; index < valuesPerRecord; index++)
            {
                parts.Add($"0x{BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + index * 2, 2)):X4}");
            }

            values.Add(new($"PMRecord{record}", string.Join(" / ", parts),
                includeCurrent ? "minimum / average / maximum / current (X16 raw)" : "minimum / average / maximum (X16 raw)"));
        }
    }

    private static void DecodeRmon(ICollection<CmisCdbDecodedValue> values, byte[] data)
    {
        var u48Names = new[]
        {
            "FrameCount", "OctetCount", "BadFrameCount", "BadOctetCount", "MulticastCount",
            "BroadcastCount", "Packets64B", "Packets64to127B", "Packets128to255B",
            "Packets256to511B", "Packets512to1023B", "Packets1024to1518B"
        };
        for (var index = 0; index < u48Names.Length; index++)
        {
            AddU48(values, data, index * 6, u48Names[index]);
        }

        var u32Names = new[]
        {
            "PacketsLargeNonJumbo", "PacketsJumbo", "BadMulticastCount", "BadBroadcastCount",
            "BadPackets64B", "BadPackets64to127B", "BadPackets128to255B",
            "BadPackets256to511B", "BadPackets512to1023B", "BadPackets1024to1518B",
            "BadPacketsLargeNonJumbo", "BadPacketsJumbo"
        };
        for (var index = 0; index < u32Names.Length; index++)
        {
            AddU32(values, data, 72 + index * 4, u32Names[index]);
        }
    }

    private static void DecodeFecHistogram(ICollection<CmisCdbDecodedValue> values, byte[] data)
    {
        Add(values, data, 0, "DPID", value => value.ToString());
        Add(values, data, 1, "DecoderLocation", value => (value & 1) == 0 ? "Media side" : "Host side");
        Add(values, data, 2, "CorrectionCapability", value => value.ToString());
        AddU48(values, data, 4, "UncorrectableCount");
        for (var index = 0; index <= 16; index++)
        {
            AddU48(values, data, 10 + index * 6, $"ErrorWeight{index}Count");
        }
        AddU48(values, data, 112, "HighErrorWeightCount");
        AddHalf(values, data, 118, "BER");
    }

    private static void DecodeTemperatureHistogram(ICollection<CmisCdbDecodedValue> values, byte[] data)
    {
        Add(values, data, 0, "SubCommands", value => $"0x{value:X2}");
        Add(values, data, 1, "NHoursToNextWrite", value => value.ToString());
        var names = new[]
        {
            "TotalSeconds", "BelowMinus5C", "Minus5To5C", "5To15C", "15To25C", "25To35C",
            "35To45C", "45To55C", "55To65C", "65To75C", "75To85C", "Above85C"
        };
        for (var index = 0; index < names.Length; index++)
        {
            AddU32(values, data, 4 + index * 4, names[index], "s");
        }
    }

    private static void AddFirmwareVersion(
        ICollection<CmisCdbDecodedValue> values,
        byte[] data,
        int offset,
        string prefix)
    {
        if (offset + 36 > data.Length)
        {
            return;
        }

        var build = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2, 2));
        values.Add(new($"{prefix}Version", $"{data[offset]}.{data[offset + 1]}.{build}"));
        AddAscii(values, data, offset + 4, 32, $"{prefix}Extra");
    }

    private static void DecodeCommandBitmap(
        ICollection<CmisCdbDecodedValue> values,
        byte[] data,
        int offset,
        int length,
        int baseCommand)
    {
        if (offset >= data.Length)
        {
            return;
        }

        length = Math.Min(length, data.Length - offset);
        var supported = new List<string>();
        for (var index = 0; index < length; index++)
        {
            for (var bit = 0; bit < 8; bit++)
            {
                if ((data[offset + index] & (1 << bit)) != 0)
                {
                    supported.Add($"0x{baseCommand + index * 8 + bit:X4}");
                }
            }
        }
        values.Add(new("SupportedCommands", supported.Count == 0 ? "None" : string.Join(", ", supported)));
    }

    private static void AddSecurityStatus(ICollection<CmisCdbDecodedValue> values, byte[] data)
    {
        if (!TryU16(data, 0, out var status))
        {
            return;
        }

        values.Add(new("ReturnStatus", SecurityStatus(status)));
    }

    private static string SecurityStatus(ushort value) => value switch
    {
        0x0000 => "OK",
        0x0001 => "COMMUNICATION_ERROR",
        0x0002 => "UNEXPECTED_ERROR",
        0x0010 => "USER_COMMAND_DATA_ERROR",
        0x0011 => "USER_NO_DIGEST_ERROR",
        0x0012 => "USER_NOT_READY_ERROR",
        0x0020 => "SEC_INVALID_PRIVATE_KEY",
        0x0021 => "SEC_INVALID_PUBLIC_KEY",
        0x0022 => "SEC_DEVICE_NOT_FOUND",
        0x0023 => "SEC_DEVICE_READ_ERROR",
        0x0024 => "SEC_DEVICE_WRITE_ERROR",
        >= 0x1000 and <= 0x1FFF => $"Custom 0x{value:X4}",
        _ => $"Reserved 0x{value:X4}"
    };

    private static string Mechanism(byte value) => value switch
    {
        0 => "None",
        1 => "LPL",
        2 => "EPL",
        3 => "LPL and EPL",
        _ => $"Reserved 0x{value:X2}"
    };

    private static void Add(
        ICollection<CmisCdbDecodedValue> values,
        byte[] data,
        int offset,
        string name,
        Func<byte, string> formatter,
        string description = "")
    {
        if (offset < data.Length)
        {
            values.Add(new(name, formatter(data[offset]), description));
        }
    }

    private static void AddU16(
        ICollection<CmisCdbDecodedValue> values,
        byte[] data,
        int offset,
        string name,
        string unit = "")
    {
        if (TryU16(data, offset, out var value))
        {
            values.Add(new(name, Unit(value, unit)));
        }
    }

    private static void AddS16(
        ICollection<CmisCdbDecodedValue> values,
        byte[] data,
        int offset,
        string name,
        double scale,
        string unit)
    {
        if (offset + 2 <= data.Length)
        {
            var raw = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(offset, 2));
            values.Add(new(name, Unit(raw * scale, unit)));
        }
    }

    private static void AddU32(
        ICollection<CmisCdbDecodedValue> values,
        byte[] data,
        int offset,
        string name,
        string unit = "")
    {
        if (offset + 4 <= data.Length)
        {
            values.Add(new(name, Unit(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4)), unit)));
        }
    }

    private static void AddU48(ICollection<CmisCdbDecodedValue> values, byte[] data, int offset, string name)
    {
        if (offset + 6 > data.Length)
        {
            return;
        }

        ulong value = 0;
        for (var index = 0; index < 6; index++)
        {
            value = (value << 8) | data[offset + index];
        }
        values.Add(new(name, value.ToString()));
    }

    private static void AddHalf(
        ICollection<CmisCdbDecodedValue> values,
        byte[] data,
        int offset,
        string name,
        string unit = "")
    {
        if (TryU16(data, offset, out var raw))
        {
            values.Add(new(name, Unit((double)BitConverter.UInt16BitsToHalf(raw), unit)));
        }
    }

    private static void AddOpticalPower(
        ICollection<CmisCdbDecodedValue> values,
        byte[] data,
        int offset,
        string name)
    {
        if (TryU16(data, offset, out var raw))
        {
            values.Add(new(name, Unit(raw * 0.0001, "mW")));
        }
    }

    private static void AddAscii(
        ICollection<CmisCdbDecodedValue> values,
        byte[] data,
        int offset,
        int length,
        string name)
    {
        if (offset + length <= data.Length)
        {
            values.Add(new(name, Encoding.ASCII.GetString(data, offset, length).Trim(' ', '\0', '\xFF')));
        }
    }

    private static bool TryU16(byte[] data, int offset, out ushort value)
    {
        if (offset + 2 <= data.Length)
        {
            value = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));
            return true;
        }

        value = 0;
        return false;
    }

    private static string Unit(double value, string unit) =>
        string.IsNullOrEmpty(unit) ? $"{value:0.########}" : $"{value:0.########} {unit}";

    private static string Unit(ulong value, string unit) =>
        string.IsNullOrEmpty(unit) ? value.ToString() : $"{value} {unit}";

    private static string Bool(bool value) => value ? "Yes" : "No";

    private static string Hex(ReadOnlySpan<byte> value) =>
        value.IsEmpty ? "(empty)" : string.Join(' ', value.ToArray().Select(item => $"{item:X2}"));
}
