namespace OptiDiag.Protocols.Cmis;

public static class CmisCodeTables
{
    private static readonly IReadOnlyDictionary<byte, string> HostInterfaces =
        new Dictionary<byte, string>
        {
            [0x00] = "Undefined",
            [0x01] = "1000BASE-CX",
            [0x02] = "XAUI",
            [0x03] = "XFI",
            [0x04] = "SFI",
            [0x05] = "25GAUI C2M",
            [0x06] = "XLAUI C2M",
            [0x07] = "XLPPI",
            [0x08] = "LAUI-2 C2M",
            [0x09] = "50GAUI-2 C2M",
            [0x0A] = "50GAUI-1 C2M",
            [0x0B] = "CAUI-4 C2M (legacy)",
            [0x0C] = "100GAUI-4 C2M",
            [0x0D] = "100GAUI-2 C2M",
            [0x0E] = "200GAUI-8 C2M",
            [0x0F] = "200GAUI-4 C2M",
            [0x10] = "400GAUI-16 C2M",
            [0x11] = "400GAUI-8 C2M",
            [0x13] = "10GBASE-CX4",
            [0x14] = "25GBASE-CR CA-25G-L",
            [0x15] = "25GBASE-CR/CR-S CA-25G-S",
            [0x16] = "25GBASE-CR/CR-S CA-25G-N",
            [0x17] = "40GBASE-CR4",
            [0x18] = "50GBASE-CR",
            [0x19] = "100GBASE-CR10",
            [0x1A] = "100GBASE-CR4",
            [0x1B] = "100GBASE-CR2",
            [0x1C] = "200GBASE-CR4",
            [0x1D] = "400G CR8",
            [0x1E] = "200GBASE-CR1",
            [0x1F] = "400GBASE-CR2",
            [0x20] = "LEI-100G-PAM4-1",
            [0x21] = "LEI-200G-PAM4-2",
            [0x22] = "LEI-400G-PAM4-4",
            [0x23] = "LEI-800G-PAM4-8",
            [0x25] = "8GFC",
            [0x26] = "10GFC",
            [0x27] = "16GFC",
            [0x28] = "32GFC",
            [0x29] = "64GFC",
            [0x2A] = "128GFC",
            [0x2B] = "256GFC",
            [0x2C] = "InfiniBand SDR",
            [0x2D] = "InfiniBand DDR",
            [0x2E] = "InfiniBand QDR",
            [0x2F] = "InfiniBand FDR",
            [0x30] = "InfiniBand EDR",
            [0x31] = "InfiniBand HDR",
            [0x32] = "InfiniBand NDR",
            [0x33] = "CPRI E.96",
            [0x34] = "CPRI E.99",
            [0x35] = "CPRI E.119",
            [0x36] = "CPRI E.238",
            [0x37] = "OTL3.4",
            [0x38] = "OTL4.10",
            [0x39] = "OTL4.4",
            [0x3A] = "OTLC.4",
            [0x3B] = "FOIC1.4-MFI",
            [0x3C] = "FOIC1.2-MFI",
            [0x3D] = "FOIC2.8-MFI",
            [0x3E] = "FOIC2.4-MFI",
            [0x3F] = "FOIC4.16-MFI",
            [0x40] = "FOIC4.8-MFI",
            [0x41] = "CAUI-4 C2M without FEC",
            [0x42] = "CAUI-4 C2M with RS(528,514) FEC",
            [0x43] = "50GBASE-CR2 with RS-FEC",
            [0x44] = "50GBASE-CR2 with BASE-R FEC",
            [0x45] = "50GBASE-CR2 without FEC",
            [0x46] = "100GBASE-CR1",
            [0x47] = "200GBASE-CR2",
            [0x48] = "400GBASE-CR4",
            [0x49] = "800GBASE-CR8",
            [0x4A] = "128GFC",
            [0x4B] = "100GAUI-1-S C2M",
            [0x4C] = "100GAUI-1-L C2M",
            [0x4D] = "200GAUI-2-S C2M",
            [0x4E] = "200GAUI-2-L C2M",
            [0x4F] = "400GAUI-4-S C2M",
            [0x50] = "400GAUI-4-L C2M",
            [0x51] = "800GAUI-8-S C2M",
            [0x52] = "800GAUI-8-L C2M",
            [0x53] = "OTL4.2",
            [0x55] = "1.6TAUI-16-S C2M",
            [0x56] = "1.6TAUI-16-L C2M",
            [0x57] = "800GBASE-CR4",
            [0x58] = "1.6TBASE-CR8",
            [0x70] = "PCIe 4.0",
            [0x71] = "PCIe 5.0",
            [0x72] = "PCIe 6.0",
            [0x73] = "PCIe 7.0",
            [0x74] = "CEI-112G-LINEAR-PAM4",
            [0x80] = "200GAUI-1 C2M",
            [0x81] = "400GAUI-2 C2M",
            [0x82] = "800GAUI-4 C2M",
            [0x83] = "1.6TAUI-8 C2M",
            [0x90] = "EEI-100G-RTLR-1-S",
            [0x91] = "EEI-100G-RTLR-1-L",
            [0x92] = "EEI-200G-RTLR-2-S",
            [0x93] = "EEI-200G-RTLR-2-L",
            [0x94] = "EEI-400G-RTLR-4-S",
            [0x95] = "EEI-400G-RTLR-4-L",
            [0x96] = "EEI-800G-RTLR-8-S",
            [0x97] = "EEI-800G-RTLR-8-L",
            [0x98] = "EEI-200G-RTLR-1",
            [0x99] = "EEI-400G-RTLR-2",
            [0x9A] = "EEI-800G-RTLR-4",
            [0x9B] = "EEI-1.6T-RTLR-8",
            [0xA0] = "InfiniBand XDR",
            [0xB0] = "FOIC1.1-MFI",
            [0xB1] = "FOIC4.4-MFI",
            [0xB2] = "FOIC8.8-MFI",
            [0xB3] = "FOIC1e.1-MFI",
            [0xB4] = "FOIC4e.4-MFI",
            [0xB5] = "FOIC1o.1-MFI",
            [0xB6] = "FOIC4o.4-MFI",
            [0xB7] = "ITU-T G.9804.3",
            [0xBE] = "Interface Unique ID escape",
            [0xFF] = "End of list"
        };

    private static readonly IReadOnlyDictionary<byte, string> MmfMediaInterfaces =
        new Dictionary<byte, string>
        {
            [0x00] = "Undefined",
            [0x01] = "10GBASE-SW",
            [0x02] = "10GBASE-SR",
            [0x03] = "25GBASE-SR",
            [0x04] = "40GBASE-SR4",
            [0x05] = "40GE SWDM4",
            [0x06] = "40GE BiDi",
            [0x07] = "50GBASE-SR",
            [0x08] = "100GBASE-SR10",
            [0x09] = "100GBASE-SR4",
            [0x0A] = "100GE SWDM4",
            [0x0B] = "100GE BiDi",
            [0x0C] = "100GBASE-SR2",
            [0x0D] = "100GBASE-SR1",
            [0x0E] = "200GBASE-SR4",
            [0x0F] = "400GBASE-SR16",
            [0x10] = "400GBASE-SR8",
            [0x11] = "400GBASE-SR4",
            [0x12] = "800GBASE-SR8",
            [0x13] = "8GFC-MM",
            [0x14] = "10GFC-MM",
            [0x15] = "16GFC-MM",
            [0x16] = "32GFC-MM",
            [0x17] = "64GFC-MM",
            [0x18] = "128GFC-MM4",
            [0x19] = "256GFC-MM4",
            [0x1A] = "400GBASE-SR4.2",
            [0x1B] = "200GBASE-SR2",
            [0x1C] = "128GFC-MM",
            [0x1D] = "100GBASE-VR1",
            [0x1E] = "200GBASE-VR2",
            [0x1F] = "400GBASE-VR4",
            [0x20] = "800GBASE-VR8",
            [0x21] = "800G-VR4.2",
            [0x22] = "800G-SR4.2",
            [0x23] = "1.6T-VR8.2",
            [0x24] = "1.6T-SR8.2",
            [0xBE] = "Interface Unique ID escape"
        };

    private static readonly IReadOnlyDictionary<byte, string> SmfMediaInterfaces =
        new Dictionary<byte, string>
        {
            [0x00] = "Undefined",
            [0x01] = "10GBASE-LW",
            [0x02] = "10GBASE-EW",
            [0x03] = "10G-ZW",
            [0x04] = "10GBASE-LR",
            [0x05] = "10GBASE-ER",
            [0x06] = "10G-ZR",
            [0x07] = "25GBASE-LR",
            [0x08] = "25GBASE-ER",
            [0x09] = "40GBASE-LR4",
            [0x0A] = "40GBASE-FR",
            [0x0B] = "50GBASE-FR",
            [0x0C] = "50GBASE-LR",
            [0x0D] = "100GBASE-LR4",
            [0x0E] = "100GBASE-ER4",
            [0x0F] = "100G PSM4",
            [0x10] = "100G CWDM4",
            [0x11] = "100G 4WDM-10",
            [0x12] = "100G 4WDM-20",
            [0x13] = "100G 4WDM-40",
            [0x14] = "100GBASE-DR",
            [0x15] = "100GBASE-FR1",
            [0x16] = "100GBASE-LR1",
            [0x17] = "200GBASE-DR4",
            [0x18] = "200GBASE-FR4",
            [0x19] = "200GBASE-LR4",
            [0x1A] = "400GBASE-FR8",
            [0x1B] = "400GBASE-LR8",
            [0x1C] = "400GBASE-DR4",
            [0x1D] = "400GBASE-FR4",
            [0x1E] = "400G-LR4-10",
            [0x1F] = "8GFC-SM",
            [0x20] = "10GFC-SM",
            [0x21] = "16GFC-SM",
            [0x22] = "32GFC-SM",
            [0x23] = "64GFC-SM",
            [0x24] = "128GFC-PSM4",
            [0x26] = "128GFC-CWDM4",
            [0x2C] = "4I1-9D1F",
            [0x2D] = "4L1-9C1F",
            [0x2E] = "4L1-9D1F",
            [0x2F] = "C4S1-9D1F",
            [0x30] = "C4S1-4D1F",
            [0x31] = "4I1-4D1F",
            [0x32] = "8R1-4D1F",
            [0x33] = "8I1-4D1F",
            [0x34] = "100G CWDM4-OCP",
            [0x35] = "ZR400-OFEC-16QAM-HA",
            [0x36] = "ZR400-OFEC-16QAM-HB",
            [0x37] = "ZR400-OFEC-8QAM-HA",
            [0x38] = "CPRI 10G-SR",
            [0x39] = "CPRI 10G-LR",
            [0x3A] = "CPRI 25G-SR",
            [0x3B] = "CPRI 25G-LR",
            [0x3C] = "CPRI 10G-LR-BiDi",
            [0x3D] = "CPRI 25G-LR-BiDi",
            [0x3E] = "400ZR amplified",
            [0x3F] = "400ZR unamplified",
            [0x40] = "50GBASE-ER",
            [0x41] = "200GBASE-ER4",
            [0x42] = "400GBASE-ER8",
            [0x43] = "400GBASE-LR4-6",
            [0x44] = "100GBASE-ZR",
            [0x45] = "128GFC-SM",
            [0x46] = "ZR400-OFEC-16QAM",
            [0x47] = "ZR300-OFEC-8QAM",
            [0x48] = "ZR200-OFEC-QPSK",
            [0x49] = "ZR100-OFEC-QPSK",
            [0x4A] = "100G-LR1-20",
            [0x4B] = "100G-ER1-30",
            [0x4C] = "100G-ER1-40",
            [0x4D] = "400GBASE-ZR",
            [0x4E] = "10GBASE-BR",
            [0x4F] = "25GBASE-BR",
            [0x50] = "50GBASE-BR",
            [0x51] = "FOIC1.4-DO",
            [0x52] = "FOIC2.8-DO",
            [0x53] = "FOIC4.8-DO",
            [0x54] = "FOIC2.4-DO",
            [0x55] = "400GBASE-DR4-2",
            [0x56] = "800GBASE-DR8",
            [0x57] = "800GBASE-DR8-2",
            [0x58] = "ZR400-OFEC-8QAM-HB",
            [0x59] = "ZR300-OFEC-8QAM-HA",
            [0x5A] = "ZR300-OFEC-8QAM-HB",
            [0x5B] = "ZR200-OFEC-QPSK-HA",
            [0x5C] = "ZR200-OFEC-QPSK-HB",
            [0x5D] = "ZR100-OFEC-QPSK-HA",
            [0x5E] = "ZR100-OFEC-QPSK-HB",
            [0x5F] = "FLEXO-4-DO-16QAM",
            [0x60] = "FLEXO-3-DO-8QAM",
            [0x61] = "FLEXO-2-DO-QPSK",
            [0x62] = "FLEXO-2-DO-16QAM",
            [0x63] = "FLEXO-1-DO-QPSK",
            [0x64] = "FLEXO-4e-DO-QPSK",
            [0x65] = "FLEXO-4-DO-QPSK",
            [0x66] = "FLEXO-8e-DO-16QAM",
            [0x67] = "FLEXO-8-DO-16QAM",
            [0x68] = "FLEXO-8e-DPO-16QAM",
            [0x69] = "FLEXO-8-DPO-16QAM",
            [0x6A] = "FLEXO-6e-DPO-16QAM",
            [0x6B] = "FLEXO-6-DPO-16QAM",
            [0x6C] = "800ZR-A",
            [0x6D] = "800ZR-B",
            [0x6E] = "800ZR-C",
            [0x6F] = "400G-ER4-30",
            [0x70] = "1I1-5D1F",
            [0x71] = "1R1-5D1F",
            [0x72] = "FOIC1.1-RS",
            [0x73] = "200GBASE-DR1",
            [0x74] = "200GBASE-DR1-2",
            [0x75] = "400GBASE-DR2",
            [0x76] = "400GBASE-DR2-2",
            [0x77] = "800GBASE-DR4",
            [0x78] = "800GBASE-DR4-2",
            [0x79] = "800GBASE-FR4-500",
            [0x7A] = "800GBASE-FR4",
            [0x7B] = "800GBASE-LR4",
            [0x7C] = "800GBASE-LR1",
            [0x7D] = "800GBASE-ER1-20",
            [0x7E] = "800GBASE-ER1",
            [0x7F] = "1.6TBASE-DR8",
            [0x80] = "1.6TBASE-DR8-2",
            [0x81] = "XR400-16QAM",
            [0x82] = "XR300-8QAM",
            [0x83] = "XR200-QPSK",
            [0x84] = "XR200-16QAM",
            [0x85] = "XR100-QPSK",
            [0x86] = "XR100-16QAM",
            [0x87] = "XR400-WS-16QAM",
            [0x88] = "XR200-WS-QPSK",
            [0x89] = "XR200-WS-16QAM",
            [0x8A] = "XR100-WS-QPSK",
            [0x8B] = "XR100-WS-16QAM",
            [0x8C] = "XR200-WS-BIDI-16QAM",
            [0x8D] = "XR100-WS-BIDI-QPSK",
            [0x8E] = "XR100-WS-BIDI-16QAM",
            [0x8F] = "100G-DR1-LPO",
            [0x90] = "200G-DR2-LPO",
            [0x91] = "400G-DR4-LPO",
            [0x92] = "800G-DR8-LPO",
            [0x93] = "400G-FR4-LPO",
            [0xBE] = "Interface Unique ID escape"
        };

    public static string Identifier(byte value) => value switch
    {
        0x18 => "QSFP-DD 8X",
        0x19 => "OSFP 8X",
        0x1E => "QSFP+/QSFP28（CMIS）",
        0x1F => "SFP-DD（CMIS）",
        0x20 => "SFP+/SFP28（CMIS）",
        0x21 => "OSFP-XD（CMIS）",
        0x22 => "OIF-ELSFP（CMIS）",
        0x23 => "CDFP x4 PCIe（CMIS）",
        0x24 => "CDFP x8 PCIe（CMIS）",
        0x25 => "CDFP x16 PCIe（CMIS）",
        0x26 => "XPO",
        _ => $"CMIS Identifier 0x{value:X2}"
    };

    public static string Connector(byte value) => value switch
    {
        0x00 => "未知或未指定",
        0x01 => "SC",
        0x07 => "LC",
        0x0B => "光纤尾纤",
        0x0C => "MPO 1x12",
        0x0D => "MPO 2x16",
        0x21 => "铜缆尾纤",
        0x23 => "无可分离连接器",
        0x24 => "MXC 2x16",
        0x25 => "CS",
        0x26 => "SN",
        0x27 => "MPO 2x12",
        0x28 => "MPO 1x16",
        _ => $"连接器代码 0x{value:X2}"
    };

    public static string MediaType(byte value) => value switch
    {
        0x00 => "未定义",
        0x01 => "多模光纤",
        0x02 => "单模光纤",
        0x03 => "无源/线性铜缆",
        0x04 => "有源铜缆",
        0x05 => "BASE-T",
        >= 0x40 and <= 0x8F => $"厂商自定义媒体类型 0x{value:X2}",
        _ => $"保留媒体类型 0x{value:X2}"
    };

    public static string MediaTechnology(byte value) => value switch
    {
        0x00 => "850 nm VCSEL",
        0x01 => "1310 nm VCSEL",
        0x02 => "1550 nm VCSEL",
        0x03 => "1310 nm FP",
        0x04 => "1310 nm DFB",
        0x05 => "1550 nm DFB",
        0x06 => "1310 nm EML",
        0x07 => "1550 nm EML",
        0x08 => "其他激光器",
        0x09 => "1490 nm DFB",
        0x0A => "无源铜缆（未均衡）",
        0x0B => "无源铜缆（均衡）",
        0x0C => "近端/远端限幅有源均衡",
        0x0D => "远端限幅有源均衡",
        0x0E => "近端限幅有源均衡",
        0x0F => "线性有源铜缆（已废弃）",
        0x10 => "C 波段可调谐激光器",
        0x11 => "L 波段可调谐激光器",
        0x12 => "近端/远端线性有源均衡",
        0x13 => "远端线性有源均衡",
        0x14 => "近端线性有源均衡",
        _ => $"媒体技术代码 0x{value:X2}"
    };

    public static string ModuleState(byte value) => value switch
    {
        1 => "ModuleLowPwr",
        2 => "ModulePwrUp",
        3 => "ModuleReady",
        4 => "ModulePwrDn",
        5 => "ModuleFault",
        _ => $"保留状态 {value}"
    };

    public static string DataPathState(byte value) => value switch
    {
        1 => "DPDeactivated",
        2 => "DPInit",
        3 => "DPDeinit",
        4 => "DPActivated",
        5 => "DPTxTurnOn",
        6 => "DPTxTurnOff",
        7 => "DPInitialized",
        _ => $"保留状态 {value}"
    };

    public static string HostInterface(byte value) =>
        HostInterfaces.TryGetValue(value, out var name)
            ? name
            : value is >= 0xC0 and <= 0xFE
                ? $"Vendor-specific host interface 0x{value:X2}"
                : $"Reserved host interface 0x{value:X2}";

    public static string MediaInterface(byte value, byte mediaType)
    {
        IReadOnlyDictionary<byte, string>? table = mediaType switch
        {
            0x01 => MmfMediaInterfaces,
            0x02 => SmfMediaInterfaces,
            _ => null
        };
        if (table is not null)
        {
            return table.TryGetValue(value, out var name)
                ? name
                : value >= 0xC0
                    ? $"Vendor-specific media interface 0x{value:X2}"
                    : $"Reserved media interface 0x{value:X2}";
        }

        return mediaType switch
        {
            0x03 => value switch
            {
                0 => "Undefined",
                1 => "Copper cable",
                0xBF => "Passive loopback module",
                0xC0 => "Linear active copper loopback module",
                >= 0xC1 => $"Vendor-specific passive/linear cable 0x{value:X2}",
                _ => $"Reserved passive/linear cable 0x{value:X2}"
            },
            0x04 => value switch
            {
                0 => "Undefined",
                1 => "Active cable, BER < 10^-12",
                2 => "Active cable, BER < 5×10^-5",
                3 => "Active cable, BER < 2.6×10^-4",
                4 => "Active cable, BER < 10^-6",
                0xBF => "Active loopback module",
                >= 0xC0 => $"Vendor-specific active cable 0x{value:X2}",
                _ => $"Reserved active cable 0x{value:X2}"
            },
            0x05 => value switch
            {
                0 => "Undefined",
                1 => "1000BASE-T",
                2 => "2.5GBASE-T",
                3 => "5GBASE-T",
                4 => "10GBASE-T",
                5 => "25GBASE-T",
                6 => "40GBASE-T",
                7 => "50GBASE-T",
                >= 0xC0 => $"Vendor-specific BASE-T 0x{value:X2}",
                _ => $"Reserved BASE-T 0x{value:X2}"
            },
            _ => $"Media interface 0x{value:X2}"
        };
    }

    public static string MciMaxSpeed(int value) => value switch
    {
        0 => "I²C 400 kHz / SPI 1 MHz",
        1 => "I²C 1 MHz / SPI 2 MHz",
        2 => "I²C 3.4 MHz / SPI 4 MHz",
        3 => "SPI 8 MHz",
        4 => "SPI 12 MHz",
        5 => "SPI 16 MHz",
        6 => "SPI 20 MHz",
        7 => "SPI 30 MHz",
        8 => "SPI 40 MHz",
        9 => "SPI 50 MHz",
        _ => "保留"
    };

    public static string MciConfiguredSpeed(int value) => value switch
    {
        0 => "SPI 1 MHz",
        1 => "SPI 2 MHz",
        2 => "SPI 4 MHz",
        3 => "SPI 8 MHz",
        4 => "SPI 12 MHz",
        5 => "SPI 16 MHz",
        6 => "SPI 20 MHz",
        7 => "SPI 30 MHz",
        8 => "SPI 40 MHz",
        9 => "SPI 50 MHz",
        _ => "保留"
    };

    public static string StateDuration(int value) => value switch
    {
        0x0 => "< 1 ms",
        0x1 => "1 ms – 5 ms",
        0x2 => "5 ms – 10 ms",
        0x3 => "10 ms – 50 ms",
        0x4 => "50 ms – 100 ms",
        0x5 => "100 ms – 500 ms",
        0x6 => "500 ms – 1 s",
        0x7 => "1 s – 5 s",
        0x8 => "5 s – 10 s",
        0x9 => "10 s – 1 min",
        0xA => "1 min – 5 min",
        0xB => "5 min – 10 min",
        0xC => "10 min – 50 min",
        0xD => "≥ 50 min",
        _ => "保留"
    };

    public static string ModuleFaultCause(int value) => value switch
    {
        0 => "未检测到故障/不支持",
        1 => "TEC 失控",
        2 => "数据存储器损坏",
        3 => "程序存储器损坏",
        4 => "发射器故障",
        5 => "接收器故障",
        6 => "温度相关故障",
        >= 32 and <= 63 => "厂商自定义故障",
        _ => "保留"
    };

    public static string PasswordResult(int value) => value switch
    {
        0 => "不支持（旧版）",
        1 => "模块密码已接受",
        2 => "主机密码已接受",
        3 => "密码未接受",
        8 => "密码验证中",
        _ => "保留"
    };

    public static string StateMachineSupport(int value) => value switch
    {
        0 => "未定义（旧版收发器/复用器）",
        1 => "无状态机",
        2 => "仅 MSM",
        3 => "MSM + DPSM",
        4 => "MSM + DPSM + NPSM",
        _ => "保留"
    };

    public static string ModuleFunctionType(int value) => value switch
    {
        0 => "传输模块",
        1 => "ELSFP Resource Module",
        >= 128 => "厂商自定义",
        _ => "保留"
    };

    public static string TernarySupport(int value) => value switch
    {
        0 => "未知（CMIS 5.2 或更早）",
        1 => "不支持",
        2 => "支持",
        _ => "保留"
    };

    public static string CdbResult(bool busy, bool failed, int code)
    {
        if (busy)
        {
            return code switch
            {
                1 => "命令已捕获，尚未处理",
                2 => "正在检查命令",
                3 => "正在执行命令",
                >= 0x30 and <= 0x3F => "厂商自定义进行中状态",
                _ => "保留的进行中状态"
            };
        }

        if (!failed)
        {
            return code switch
            {
                1 => "命令成功完成",
                3 => "上一命令已被 Abort 中止",
                >= 0x30 and <= 0x3F => "厂商自定义成功状态",
                _ => "保留的成功状态"
            };
        }

        return code switch
        {
            1 => "未知 CMDID",
            2 => "参数越界或不支持",
            3 => "上一命令未正确中止",
            4 => "命令检查超时",
            5 => "CdbChkCode 错误",
            6 => "密码相关错误",
            7 => "命令与当前运行状态不兼容",
            >= 0x20 and <= 0x2F => "命令/任务特定错误",
            >= 0x30 and <= 0x3F => "厂商自定义错误",
            _ => "保留的失败状态"
        };
    }

    public static string CableLength(byte value)
    {
        var exponent = value >> 6;
        var mantissa = value & 0x3F;
        var multiplier = exponent switch { 0 => 0.1, 1 => 1, 2 => 10, _ => 100 };
        return $"{mantissa * multiplier:0.###} m (m={mantissa}, e={exponent})";
    }

    public static string ConfigurationStatus(int value) => value switch
    {
        0x0 => "ConfigUndefined",
        0x1 => "ConfigSuccess",
        0x2 => "ConfigRejected",
        0x3 => "ConfigRejectedInvalidAppSel",
        0x4 => "ConfigRejectedInvalidDataPath",
        0x5 => "ConfigRejectedInvalidSI",
        0x6 => "ConfigRejectedLanesInUse",
        0x7 => "ConfigRejectedPartialDataPath",
        0xC => "ConfigInProgress",
        0xD or 0xE or 0xF => "Custom rejection",
        _ => "Reserved rejection"
    };

    public static string GridSpacing(int value) => value switch
    {
        0 => "3.125 GHz",
        1 => "6.25 GHz",
        2 => "12.5 GHz",
        3 => "25 GHz",
        4 => "50 GHz",
        5 => "100 GHz",
        6 => "33.333 GHz",
        7 => "75 GHz",
        8 => "150 GHz",
        9 => "300 GHz",
        15 => "不可用",
        _ => "保留"
    };

    public static string PrbsGeneratorClockSource(int value) => value switch
    {
        0 => "内部时钟",
        >= 1 and <= 8 => $"参考 Lane {value}",
        15 => "每通道/数据通道恢复时钟",
        _ => "保留"
    };

    public static string MeasurementTime(int value) => value switch
    {
        0 => "无限/不门控",
        1 => "5 s",
        2 => "10 s",
        3 => "30 s",
        4 => "60 s",
        5 => "120 s",
        6 => "300 s",
        _ => "自定义"
    };

    public static string DiagnosticsSelector(int value) => value switch
    {
        0x00 => "无（数据区全零）",
        0x01 => "实时 Host/Media Lane 1-8 BER",
        0x02 => "实时 Host Lane 1-4 错误/比特计数",
        0x03 => "实时 Host Lane 5-8 错误/比特计数",
        0x04 => "实时 Media Lane 1-4 错误/比特计数",
        0x05 => "实时 Media Lane 5-8 错误/比特计数",
        0x06 => "实时 Host/Media Lane 1-8 SNR",
        0x11 => "最近门控周期 Host/Media Lane 1-8 BER",
        0x12 => "最近门控周期 Host Lane 1-4 错误/比特计数",
        0x13 => "最近门控周期 Host Lane 5-8 错误/比特计数",
        0x14 => "最近门控周期 Media Lane 1-4 错误/比特计数",
        0x15 => "最近门控周期 Media Lane 5-8 错误/比特计数",
        >= 0xC0 => "厂商自定义",
        _ => "保留"
    };

    public static string NetworkConfigurationStatus(int value) => ConfigurationStatus(value);

    public static string NetworkPathState(int value) => value switch
    {
        0 => "Reserved",
        1 => "NPDeactivated",
        2 => "NPInit",
        3 => "NPDeinit",
        4 => "NPActivated",
        5 => "NPTxTurnOn",
        6 => "NPTxTurnOff",
        7 => "NPInitialized",
        _ => "Reserved"
    };

    public static string HostLaneRedirectionResult(int value) => value switch
    {
        0 => "Undefined",
        1 => "Success",
        2 => "Rejected",
        3 => "Rejected: invalid lane mapping",
        4 => "Rejected: lanes in use",
        0xC => "In progress",
        >= 0xD and <= 0xF => "Custom rejection",
        _ => $"Reserved (0x{value:X2})"
    };
}
