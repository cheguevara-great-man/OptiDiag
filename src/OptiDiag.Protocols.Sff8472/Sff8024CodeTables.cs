namespace OptiDiag.Protocols.Sff8472;

/// <summary>
/// SFF-8024 Rev 4.14 中由 SFF-8472 直接引用的完整码表。
/// 保留范围也在 Lookup 方法中按规范分类，未知值不会被误标成标准能力。
/// </summary>
public static class Sff8024CodeTables
{
    public static IReadOnlyDictionary<byte, string> Identifiers { get; } =
        new Dictionary<byte, string>
        {
            [0x00] = "未知或未指定",
            [0x01] = "GBIC",
            [0x02] = "板载模块/连接器（SFF-8472）",
            [0x03] = "SFP/SFP+/SFP28 及后续（SFF-8472）",
            [0x04] = "300-pin XBI",
            [0x05] = "XENPAK",
            [0x06] = "XFP",
            [0x07] = "XFF",
            [0x08] = "XFP-E",
            [0x09] = "XPAK",
            [0x0A] = "X2",
            [0x0B] = "DWDM-SFP/SFP+（不使用 SFF-8472）",
            [0x0C] = "QSFP（INF-8438）",
            [0x0D] = "QSFP+ 或后续（SFF-8436/SFF-8636）",
            [0x0E] = "CXP 或后续",
            [0x0F] = "Shielded Mini Multilane HD 4X",
            [0x10] = "Shielded Mini Multilane HD 8X",
            [0x11] = "QSFP28 或后续（SFF-8636）",
            [0x12] = "CXP2/CXP28 或后续",
            [0x13] = "CDFP Style 1/2",
            [0x14] = "Shielded Mini Multilane HD 4X Fanout",
            [0x15] = "Shielded Mini Multilane HD 8X Fanout",
            [0x16] = "CDFP Style 3",
            [0x17] = "microQSFP",
            [0x18] = "QSFP-DD 8X",
            [0x19] = "OSFP 8X",
            [0x1A] = "SFP-DD（SFP-DD 管理接口）",
            [0x1B] = "DSFP",
            [0x1C] = "x4 MiniLink/OcuLink",
            [0x1D] = "x8 MiniLink",
            [0x1E] = "QSFP+ 或后续（CMIS）",
            [0x1F] = "SFP-DD（CMIS）",
            [0x20] = "SFP+ 或后续（CMIS）",
            [0x21] = "OSFP-XD（CMIS）",
            [0x22] = "OIF-ELSFP（CMIS）",
            [0x23] = "CDFP x4 PCIe（CMIS）",
            [0x24] = "CDFP x8 PCIe（CMIS）",
            [0x25] = "CDFP x16 PCIe（CMIS）",
            [0x26] = "XPO"
        };

    public static IReadOnlyDictionary<byte, string> Encodings { get; } =
        new Dictionary<byte, string>
        {
            [0x00] = "未指定",
            [0x01] = "8B/10B",
            [0x02] = "4B/5B",
            [0x03] = "NRZ",
            [0x04] = "Manchester",
            [0x05] = "SONET Scrambled",
            [0x06] = "64B/66B",
            [0x07] = "256B/257B（FEC 转码数据）",
            [0x08] = "PAM4"
        };

    public static IReadOnlyDictionary<byte, string> Connectors { get; } =
        new Dictionary<byte, string>
        {
            [0x00] = "未知或未指定",
            [0x01] = "SC",
            [0x02] = "Fibre Channel Style 1 铜连接器",
            [0x03] = "Fibre Channel Style 2 铜连接器",
            [0x04] = "BNC/TNC",
            [0x05] = "Fibre Channel 同轴插头",
            [0x06] = "Fiber Jack",
            [0x07] = "LC",
            [0x08] = "MT-RJ",
            [0x09] = "MU",
            [0x0A] = "SG",
            [0x0B] = "光纤尾纤",
            [0x0C] = "MPO 1x12",
            [0x0D] = "MPO 2x16",
            [0x20] = "HSSDC II",
            [0x21] = "铜缆尾纤",
            [0x22] = "RJ45",
            [0x23] = "无可分离连接器",
            [0x24] = "MXC 2x16",
            [0x25] = "CS 光连接器",
            [0x26] = "SN 光连接器",
            [0x27] = "MPO 2x12",
            [0x28] = "MPO 1x16"
        };

    public static IReadOnlyDictionary<byte, string> ExtendedComplianceCodes { get; } =
        new Dictionary<byte, string>
        {
            [0x00] = "未指定",
            [0x01] = "100G AOC/25GAUI C2M AOC，最差 BER 5×10⁻⁵",
            [0x02] = "100GBASE-SR4 或 25GBASE-SR",
            [0x03] = "100GBASE-LR4 或 25GBASE-LR",
            [0x04] = "100GBASE-ER4 或 25GBASE-ER",
            [0x05] = "100GBASE-SR10",
            [0x06] = "100G CWDM4",
            [0x07] = "100G PSM4 Parallel SMF",
            [0x08] = "100G ACC/25GAUI C2M ACC，最差 BER 5×10⁻⁵",
            [0x09] = "已废弃（早期 100G CWDM4 无 FEC 分配）",
            [0x0A] = "保留",
            [0x0B] = "100GBASE-CR4/25GBASE-CR CA-25G-L/50GBASE-CR2，RS-FEC",
            [0x0C] = "25GBASE-CR CA-25G-S/50GBASE-CR2，BASE-R FEC",
            [0x0D] = "25GBASE-CR CA-25G-N/50GBASE-CR2，无 FEC",
            [0x0E] = "10 Mb/s 单对以太网，1000 m 铜缆",
            [0x0F] = "保留",
            [0x10] = "40GBASE-ER4",
            [0x11] = "4×10GBASE-SR",
            [0x12] = "40G PSM4 Parallel SMF",
            [0x13] = "G.959.1 P1I1-2D1，2 km，1310 nm",
            [0x14] = "G.959.1 P1S1-2D2，40 km，1550 nm",
            [0x15] = "G.959.1 P1L1-2D2，80 km，1550 nm",
            [0x16] = "10GBASE-T，SFI 电气接口",
            [0x17] = "100G CLR4",
            [0x18] = "100G AOC/25GAUI C2M AOC，最差 BER 10⁻¹²",
            [0x19] = "100G ACC/25GAUI C2M ACC，最差 BER 10⁻¹²",
            [0x1A] = "100GE-DWDM2，1550 nm 双波长，最长 80 km",
            [0x1B] = "100G 1550 nm WDM（4 波长）",
            [0x1C] = "10GBASE-T Short Reach（30 m）",
            [0x1D] = "5GBASE-T",
            [0x1E] = "2.5GBASE-T",
            [0x1F] = "40G SWDM4",
            [0x20] = "100G SWDM4",
            [0x21] = "100G PAM4 BiDi",
            [0x22] = "4WDM-10 MSA",
            [0x23] = "4WDM-20 MSA",
            [0x24] = "4WDM-40 MSA",
            [0x25] = "100GBASE-DR，CAUI-4 无 FEC",
            [0x26] = "100G-FR/100GBASE-FR1，CAUI-4 无 FEC",
            [0x27] = "100G-LR/100GBASE-LR1，CAUI-4 无 FEC",
            [0x28] = "100GBASE-SR1，CAUI-4 无 FEC",
            [0x29] = "100GBASE-SR1/200GBASE-SR2/400GBASE-SR4",
            [0x2A] = "100GBASE-FR1 或 400GBASE-DR4-2",
            [0x2B] = "100GBASE-LR1",
            [0x2C] = "100G-LR1-20 MSA，CAUI-4 无 FEC",
            [0x2D] = "100G-ER1-30 MSA，CAUI-4 无 FEC",
            [0x2E] = "100G-ER1-40 MSA，CAUI-4 无 FEC",
            [0x2F] = "100G-LR1-20 MSA",
            [0x30] = "50/100/200GAUI C2M ACC，最差 BER 10⁻⁶",
            [0x31] = "50/100/200GAUI C2M AOC，最差 BER 10⁻⁶",
            [0x32] = "50/100/200GAUI C2M ACC，最差 BER 2.6×10⁻⁴",
            [0x33] = "50/100/200GAUI C2M AOC，最差 BER 2.6×10⁻⁴",
            [0x34] = "100G-ER1-30 MSA",
            [0x35] = "100G-ER1-40 MSA",
            [0x36] = "100GBASE-VR1/200GBASE-VR2/400GBASE-VR4",
            [0x37] = "10GBASE-BR",
            [0x38] = "25GBASE-BR",
            [0x39] = "50GBASE-BR",
            [0x3A] = "100GBASE-VR1，CAUI-4 无 FEC",
            [0x3B] = "保留",
            [0x3C] = "保留",
            [0x3D] = "保留",
            [0x3E] = "保留",
            [0x3F] = "100GBASE-CR1/200GBASE-CR2/400GBASE-CR4",
            [0x40] = "50GBASE-CR/100GBASE-CR2/200GBASE-CR4",
            [0x41] = "50GBASE-SR/100GBASE-SR2/200GBASE-SR4",
            [0x42] = "50GBASE-FR 或 200GBASE-DR4",
            [0x43] = "200GBASE-FR4",
            [0x44] = "200G 1550 nm PSM4",
            [0x45] = "50GBASE-LR",
            [0x46] = "200GBASE-LR4",
            [0x47] = "400GBASE-DR4",
            [0x48] = "400GBASE-FR4",
            [0x49] = "400GBASE-LR4-6",
            [0x4A] = "50GBASE-ER",
            [0x4B] = "400G-LR4-10",
            [0x4C] = "400GBASE-ZR（已废弃）",
            [0x7F] = "256GFC-SW4",
            [0x80] = "64GFC（不用于 SFF-8472 单通道模块）",
            [0x81] = "128GFC（不用于 SFF-8472 单通道模块）"
        };

    public static string LookupIdentifier(byte value) =>
        Identifiers.TryGetValue(value, out var text)
            ? text
            : value >= 0x80
                ? $"厂商自定义标识 (0x{value:X2})"
                : $"保留标识 (0x{value:X2})";

    public static string LookupEncoding(byte value) =>
        Encodings.TryGetValue(value, out var text) ? text : $"保留编码 (0x{value:X2})";

    public static string LookupConnector(byte value) =>
        Connectors.TryGetValue(value, out var text)
            ? text
            : value >= 0x80
                ? $"厂商自定义连接器 (0x{value:X2})"
                : $"保留连接器 (0x{value:X2})";

    public static string LookupExtendedCompliance(byte value) =>
        ExtendedComplianceCodes.TryGetValue(value, out var text)
            ? text
            : value is >= 0x4D and <= 0x7E or >= 0x82
                ? $"保留兼容码 (0x{value:X2})"
                : $"未分配兼容码 (0x{value:X2})";
}
