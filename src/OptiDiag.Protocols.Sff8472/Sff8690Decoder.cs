using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Sff8472;

public sealed partial class Sff8472Protocol
{
    private static void DecodeSff8690Fields(
        byte[] a0,
        ModuleDump dump,
        ICollection<DecodedField> fields)
    {
        if (!IsSet(a0[65], 6))
        {
            return;
        }

        var page = dump.FindRegion(A2Page02RegionId)?.Data;
        if (page is not { Length: >= 128 })
        {
            Add(fields, "SFF-8690", "扩展状态", "已声明但 Page 02h 不可用", "A0h.65.6");
            return;
        }

        byte P(int absoluteOffset) => page[absoluteOffset - 128];
        AddBit8690(fields, page, "Vendor/Other Tunability", 128, 4);
        AddBit8690(fields, page, "Smart Tunable MSA Self Tuning", 128, 3);
        AddBit8690(fields, page, "TX Dither", 128, 2);
        AddBit8690(fields, page, "DWDM Channel Number Selection", 128, 1);
        AddBit8690(fields, page, "DWDM 50 pm Wavelength Selection", 128, 0);

        var firstFrequencyGhz = ReadUInt16P02(page, 132) * 1000d + ReadUInt16P02(page, 134) * 0.1;
        var lastFrequencyGhz = ReadUInt16P02(page, 136) * 1000d + ReadUInt16P02(page, 138) * 0.1;
        var gridSpacingGhz = unchecked((short)ReadUInt16P02(page, 140)) * 0.1;
        Add(fields, "SFF-8690/能力", "Laser First Frequency", $"{firstFrequencyGhz / 1000d:0.0000} THz",
            "A2h P02.132-135", $"精确值 {firstFrequencyGhz:0.0} GHz");
        Add(fields, "SFF-8690/能力", "Laser Last Frequency", $"{lastFrequencyGhz / 1000d:0.0000} THz",
            "A2h P02.136-139", $"精确值 {lastFrequencyGhz:0.0} GHz");
        Add(fields, "SFF-8690/能力", "Minimum Grid Spacing", $"{gridSpacingGhz:0.0} GHz",
            "A2h P02.140-141");
        if (gridSpacingGhz != 0)
        {
            var channelCount = (int)Math.Floor(Math.Abs(lastFrequencyGhz - firstFrequencyGhz) / Math.Abs(gridSpacingGhz)) + 1;
            Add(fields, "SFF-8690/能力", "Calculated Channel Count", channelCount.ToString(),
                "A2h P02.132-141", "1 + |Last-First| / |Grid|");
        }

        var channel = ReadUInt16P02(page, 144);
        var wavelengthNm = ReadUInt16P02(page, 146) * 0.05;
        Add(fields, "SFF-8690/控制", "Channel Number", channel.ToString(),
            "A2h P02.144-145", "建议以单次 2 字节事务写入。", writable: true);
        Add(fields, "SFF-8690/控制", "Wavelength Setpoint", $"{wavelengthNm:0.00} nm",
            "A2h P02.146-147", "LSB=0.05 nm；建议以单次 2 字节事务写入。", writable: true);
        if (channel > 0 && gridSpacingGhz != 0)
        {
            Add(fields, "SFF-8690/控制", "Channel-derived Frequency",
                $"{(firstFrequencyGhz + (channel - 1) * gridSpacingGhz) / 1000d:0.0000} THz",
                "A2h P02.132-145");
        }

        AddBit8690(fields, page, "Disable Self Tuning Restart on LOS Timeout", 151, 2, writable: true);
        AddBit8690(fields, page, "Enable/Restart Self Tuning", 151, 1, writable: true);
        Add(fields, "SFF-8690/控制", "TX Dither",
            IsSet(P(151), 0) ? "禁用" : "启用", "A2h P02.151.0",
            "该位逻辑为 1=禁用、0=启用。", writable: true);

        Add(fields, "SFF-8690/诊断", "Frequency Error",
            $"{unchecked((short)ReadUInt16P02(page, 152)) * 0.1:0.0} GHz",
            "A2h P02.152-153", "Measured - Target");
        Add(fields, "SFF-8690/诊断", "Wavelength Error",
            $"{unchecked((short)ReadUInt16P02(page, 154)) * 0.005:0.000} nm",
            "A2h P02.154-155", "Measured - Target");

        Add(fields, "SFF-8690/当前状态", "Self Tuning",
            IsSet(P(168), 7) ? "进行中" : "空闲/已锁定", "A2h P02.168.7");
        AddBit8690(fields, page, "Temperature Controller Fault", 168, 6, category: "SFF-8690/当前状态");
        AddBit8690(fields, page, "Wavelength Unlocked", 168, 5, category: "SFF-8690/当前状态");
        AddBit8690(fields, page, "TX Tuning/Not Ready", 168, 4, category: "SFF-8690/当前状态");
        AddBit8690(fields, page, "Latched Self Tuning", 172, 7, category: "SFF-8690/锁存状态");
        AddBit8690(fields, page, "Latched TEC Fault", 172, 6, category: "SFF-8690/锁存状态");
        AddBit8690(fields, page, "Latched Wavelength Unlocked", 172, 5, category: "SFF-8690/锁存状态");
        AddBit8690(fields, page, "Latched Bad Channel", 172, 4, category: "SFF-8690/锁存状态");
        AddBit8690(fields, page, "Latched New Channel", 172, 3, category: "SFF-8690/锁存状态");
        AddBit8690(fields, page, "Latched Unsupported TX Dither", 172, 2, category: "SFF-8690/锁存状态");
    }

    private static void AddBit8690(
        ICollection<DecodedField> fields,
        byte[] page,
        string name,
        int absoluteOffset,
        int bit,
        bool writable = false,
        string category = "SFF-8690/能力") =>
        Add(fields, category, name, YesNo(IsSet(page[absoluteOffset - 128], bit)),
            $"A2h P02.{absoluteOffset}.{bit}", writable: writable);
}
