using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Cmis;

internal static class CmisVdmDecoder
{
    private sealed record Descriptor(
        int Instance,
        int ThresholdSet,
        int Resource,
        byte Type,
        byte Bank,
        byte Group,
        int LocalIndex);

    public static void Decode(ModuleDump dump, List<DecodedField> fields)
    {
        var sink = new CmisFieldSink(fields);
        var banks = dump.Regions
            .Where(x => x.Page == 0x2F)
            .Select(x => x.Bank ?? (byte)0)
            .Distinct()
            .OrderBy(x => x);

        foreach (var bank in banks)
        {
            var advertisement = Find(dump, bank, 0x2F);
            if (advertisement is null)
            {
                continue;
            }

            DecodeAdvertisement(advertisement, bank, sink);
            var groupCount = (advertisement[0] & 0x03) + 1;
            var descriptors = new List<Descriptor>();
            for (byte group = 0; group < groupCount; group++)
            {
                var descriptorPage = Find(dump, bank, (byte)(0x20 + group));
                if (descriptorPage is null)
                {
                    continue;
                }

                DecodeDescriptors(descriptorPage, bank, group, descriptors, sink);
            }

            foreach (var descriptor in descriptors.Where(x => x.Type != 0))
            {
                var samplePage = Find(dump, bank, (byte)(0x24 + descriptor.Group));
                if (samplePage is null)
                {
                    continue;
                }

                var offset = descriptor.LocalIndex * 2;
                sink.Add(
                    "VDM 样本",
                    $"VDM{descriptor.Instance} {ObservableName(descriptor.Type)}",
                    FormatValue(descriptor.Type, samplePage, offset),
                    CmisDecoderHelpers.Source(
                        (byte)(0x24 + descriptor.Group),
                        128 + offset,
                        bank),
                    $"Resource={ResourceName(descriptor.Resource, bank)}; ThresholdSet={descriptor.ThresholdSet}");
            }

            DecodeThresholds(dump, bank, descriptors, sink);
            DecodeFlagsAndMasks(dump, bank, groupCount, descriptors, sink);
        }
    }

    private static void DecodeAdvertisement(byte[] page, byte bank, CmisFieldSink sink)
    {
        sink.Add("VDM 能力", $"Bank{bank}VDMGroupsSupported", (page[0] & 0x03) + 1,
            CmisDecoderHelpers.Source(0x2F, 128, bank, "1-0"));
        sink.Bool("VDM 能力", $"Bank{bank}PowerSavingSupport", page[0], 2,
            CmisDecoderHelpers.Source(0x2F, 128, bank, "2"));
        sink.Add("VDM 能力", $"Bank{bank}FineIntervalLength",
            CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(page, 1) * 0.1, "ms"),
            CmisDecoderHelpers.Source(0x2F, 129, bank));
        sink.Bool("VDM 控制", $"Bank{bank}FreezeRequest", page[16], 7,
            CmisDecoderHelpers.Source(0x2F, 144, bank, "7"), writable: true);
        sink.Bool("VDM 控制", $"Bank{bank}PowerSavingMode", page[16], 6,
            CmisDecoderHelpers.Source(0x2F, 144, bank, "6"), writable: true);
        sink.Bool("VDM 状态", $"Bank{bank}FreezeDone", page[17], 7,
            CmisDecoderHelpers.Source(0x2F, 145, bank, "7"));
        sink.Bool("VDM 状态", $"Bank{bank}UnfreezeDone", page[17], 6,
            CmisDecoderHelpers.Source(0x2F, 145, bank, "6"));
    }

    private static void DecodeDescriptors(
        byte[] page,
        byte bank,
        byte group,
        List<Descriptor> descriptors,
        CmisFieldSink sink)
    {
        for (var index = 0; index < 64; index++)
        {
            var first = page[index * 2];
            var type = page[index * 2 + 1];
            var instance = group * 64 + index + 1;
            var thresholdSet = group * 16 + (first >> 4) + 1;
            var resource = first & 0x0F;
            var descriptor = new Descriptor(instance, thresholdSet, resource, type, bank, group, index);
            descriptors.Add(descriptor);

            if (type == 0)
            {
                continue;
            }

            sink.Add(
                "VDM 描述符",
                $"VDM{instance}",
                ObservableName(type),
                CmisDecoderHelpers.Source((byte)(0x20 + group), 128 + index * 2, bank),
                $"Type=0x{type:X2}; Resource={ResourceName(resource, bank)}; ThresholdSet={thresholdSet}");
        }
    }

    private static void DecodeThresholds(
        ModuleDump dump,
        byte bank,
        IReadOnlyList<Descriptor> descriptors,
        CmisFieldSink sink)
    {
        foreach (var group in descriptors
                     .Where(x => x.Type != 0)
                     .GroupBy(x => x.ThresholdSet)
                     .OrderBy(x => x.Key))
        {
            var first = group.First();
            var thresholdPageNumber = (byte)(0x28 + (first.ThresholdSet - 1) / 16);
            var page = Find(dump, bank, thresholdPageNumber);
            if (page is null)
            {
                continue;
            }

            var localSet = (first.ThresholdSet - 1) % 16;
            var offset = localSet * 8;
            var type = first.Type;
            var allTypes = group.Select(x => x.Type).Distinct().ToArray();
            var description = allTypes.Length == 1
                ? $"用于 VDM {string.Join(", ", group.Select(x => x.Instance))}"
                : $"描述符类型冲突：{string.Join(", ", allTypes.Select(x => $"0x{x:X2}"))}";
            var suffixes = new[] { "HighAlarm", "LowAlarm", "HighWarning", "LowWarning" };
            for (var index = 0; index < 4; index++)
            {
                sink.Add(
                    "VDM 阈值",
                    $"ThresholdSet{first.ThresholdSet}{suffixes[index]}",
                    FormatValue(type, page, offset + index * 2),
                    CmisDecoderHelpers.Source(thresholdPageNumber, 128 + offset + index * 2, bank),
                    description);
            }
        }
    }

    private static void DecodeFlagsAndMasks(
        ModuleDump dump,
        byte bank,
        int groupCount,
        IReadOnlyList<Descriptor> descriptors,
        CmisFieldSink sink)
    {
        var flags = Find(dump, bank, 0x2C);
        var masks = Find(dump, bank, 0x2D);
        var maximumInstances = groupCount * 64;
        for (var instance = 1; instance <= maximumInstances; instance++)
        {
            var descriptor = descriptors.FirstOrDefault(x => x.Instance == instance);
            if (descriptor is null || descriptor.Type == 0)
            {
                continue;
            }

            var byteIndex = (instance - 1) / 2;
            var shift = (instance - 1) % 2 * 4;
            if (flags is not null)
            {
                DecodeFlagNibble(
                    (flags[byteIndex] >> shift) & 0x0F,
                    instance,
                    descriptor,
                    bank,
                    0x2C,
                    128 + byteIndex,
                    false,
                    sink);
            }

            if (masks is not null)
            {
                DecodeFlagNibble(
                    (masks[byteIndex] >> shift) & 0x0F,
                    instance,
                    descriptor,
                    bank,
                    0x2D,
                    128 + byteIndex,
                    true,
                    sink);
            }
        }
    }

    private static void DecodeFlagNibble(
        int value,
        int instance,
        Descriptor descriptor,
        byte bank,
        byte page,
        int offset,
        bool mask,
        CmisFieldSink sink)
    {
        var names = new[] { "HighAlarm", "LowAlarm", "HighWarning", "LowWarning" };
        for (var bit = 0; bit < 4; bit++)
        {
            sink.Add(
                mask ? "VDM 屏蔽" : "VDM 标志",
                $"VDM{instance}{names[bit]}{(mask ? "Mask" : "Flag")}",
                CmisDecoderHelpers.Bool((value & (1 << bit)) != 0),
                CmisDecoderHelpers.Source(page, offset, bank),
                $"{ObservableName(descriptor.Type)}; {(mask ? "置位=屏蔽中断" : "锁存，只读/读清除")}",
                writable: mask);
        }
    }

    private static string FormatValue(byte type, byte[] source, int offset) => type switch
    {
        1 => CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(source, offset), "%"),
        2 => CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.S16(source, offset) * 100d / 32767, "%"),
        3 => CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.S16(source, offset) * 0.01, "GHz"),
        4 => CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.S16(source, offset) / 256d, "°C"),
        >= 5 and <= 8 => CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(source, offset) / 256d, "dB"),
        >= 9 and <= 26 => CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.F16(source, offset)),
        >= 27 and <= 34 => CmisDecoderHelpers.U16(source, offset).ToString(),
        >= 77 and <= 82 => CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(source, offset) * 0.0001, "V"),
        83 => CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(source, offset) * 0.00025, "V"),
        84 => CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.S16(source, offset) * 0.01, "dBm"),
        _ => $"0x{CmisDecoderHelpers.U16(source, offset):X4}"
    };

    private static string ObservableName(byte type) => type switch
    {
        0 => "Not Used",
        1 => "Laser Age",
        2 => "TEC Current",
        3 => "Laser Frequency Error",
        4 => "Laser Temperature",
        5 => "Media Input SNR",
        6 => "Host Input SNR",
        7 => "Media PAM4 Level Transition Parameter",
        8 => "Host PAM4 Level Transition Parameter",
        9 => "Media Pre-FEC BER Minimum",
        10 => "Host Pre-FEC BER Minimum",
        11 => "Media Pre-FEC BER Maximum",
        12 => "Host Pre-FEC BER Maximum",
        13 => "Media Pre-FEC BER Average",
        14 => "Host Pre-FEC BER Average",
        15 => "Media Pre-FEC BER Current",
        16 => "Host Pre-FEC BER Current",
        17 => "Media FERC Minimum",
        18 => "Host FERC Minimum",
        19 => "Media FERC Maximum",
        20 => "Host FERC Maximum",
        21 => "Media FERC Average",
        22 => "Host FERC Average",
        23 => "Media FERC Current",
        24 => "Host FERC Current",
        25 => "Media FERC Total Accumulated",
        26 => "Host FERC Total Accumulated",
        27 => "Media SEWmax Minimum",
        28 => "Host SEWmax Minimum",
        29 => "Media SEWmax Maximum",
        30 => "Host SEWmax Maximum",
        31 => "Media SEWmax Average",
        32 => "Host SEWmax Average",
        33 => "Media SEWmax Current",
        34 => "Host SEWmax Current",
        77 => "Vcc 2.6 V",
        78 => "Vcc 1.8 V",
        79 => "Vcc 1.2 V",
        80 => "Vcc 0.9 V",
        81 => "Vcc 0.7 A",
        82 => "Vcc 0.7 B",
        83 => "Vcc 12 V",
        84 => "ELS Input Power",
        >= 100 and <= 127 => $"Custom Observable 0x{type:X2}",
        >= 128 => $"OIF Restricted Observable 0x{type:X2}",
        _ => $"Reserved Observable 0x{type:X2}"
    };

    private static string ResourceName(int resource, byte bank) => resource switch
    {
        >= 0 and <= 7 => $"Lane/DataPath {bank * 8 + resource + 1}",
        15 => "Module",
        _ => $"Reserved resource {resource}"
    };

    private static byte[]? Find(ModuleDump dump, byte bank, byte page) =>
        dump.Regions.FirstOrDefault(x => x.Page == page && (x.Bank ?? 0) == bank)?.Data;
}
