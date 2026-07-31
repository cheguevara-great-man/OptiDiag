using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Sff8472;

public sealed partial class Sff8472Protocol
{
    private static readonly (int Byte, int Bit, string Name)[] LegacyComplianceBits =
    [
        (3, 7, "10GBASE-ER"), (3, 6, "10GBASE-LRM"), (3, 5, "10GBASE-LR"), (3, 4, "10GBASE-SR"),
        (3, 3, "InfiniBand 1X SX"), (3, 2, "InfiniBand 1X LX"), (3, 1, "InfiniBand 1X Copper Active"),
        (3, 0, "InfiniBand 1X Copper Passive"),
        (4, 7, "ESCON MMF, 1310 nm LED"), (4, 6, "ESCON SMF, 1310 nm Laser"),
        (4, 5, "SONET OC-192 short reach"), (4, 4, "SONET reach specifier 1"),
        (4, 3, "SONET reach specifier 2"), (4, 2, "SONET OC-48 long reach"),
        (4, 1, "SONET OC-48 intermediate reach"), (4, 0, "SONET OC-48 short reach"),
        (5, 6, "SONET OC-12 single-mode long reach"), (5, 5, "SONET OC-12 single-mode intermediate reach"),
        (5, 4, "SONET OC-12 short reach"), (5, 2, "SONET OC-3 single-mode long reach"),
        (5, 1, "SONET OC-3 single-mode intermediate reach"), (5, 0, "SONET OC-3 short reach"),
        (6, 7, "BASE-PX"), (6, 6, "BASE-BX10"), (6, 5, "100BASE-FX"),
        (6, 4, "100BASE-LX/LX10"), (6, 3, "1000BASE-T"), (6, 2, "1000BASE-CX"),
        (6, 1, "1000BASE-LX"), (6, 0, "1000BASE-SX"),
        (7, 7, "Fibre Channel very long distance (V)"), (7, 6, "Fibre Channel short distance (S)"),
        (7, 5, "Fibre Channel intermediate distance (I)"), (7, 4, "Fibre Channel long distance (L)"),
        (7, 3, "Fibre Channel medium distance (M)"), (7, 2, "FC shortwave laser, linear Rx (SA)"),
        (7, 1, "FC longwave laser (LC)"), (7, 0, "FC electrical inter-enclosure (EL)"),
        (8, 7, "FC electrical intra-enclosure (EL)"), (8, 6, "FC shortwave laser without OFC (SN)"),
        (8, 5, "FC shortwave laser with OFC (SL)"), (8, 4, "FC longwave laser (LL)"),
        (8, 3, "SFP+ active cable"), (8, 2, "SFP+ passive cable"),
        (9, 7, "FC Twin Axial Pair"), (9, 6, "FC Twisted Pair"), (9, 5, "FC Miniature Coax"),
        (9, 4, "FC Video Coax"), (9, 3, "FC MMF 62.5 µm"), (9, 2, "FC MMF 50 µm"),
        (9, 0, "FC Single Mode"),
        (10, 7, "FC 1200 MB/s"), (10, 6, "FC 800 MB/s"), (10, 5, "FC 1600 MB/s"),
        (10, 4, "FC 400 MB/s"), (10, 3, "FC 3200 MB/s"), (10, 2, "FC 200 MB/s"),
        (10, 1, "FC Speed 2 字段有效"), (10, 0, "FC 100 MB/s"),
        (62, 0, "64GFC")
    ];

    private static ProtocolIdentification DecodeProtocolIdentification(byte[] a0, ModuleDump dump)
    {
        var tunable = (a0[65] & 0x40) != 0;
        var page02Present = dump.FindRegion(A2Page02RegionId) is not null;
        return new ProtocolIdentification(
            "sff-8472",
            "SFF-8472",
            LookupCompliance(a0[94]),
            $"Identifier=A0h.0=0x{a0[0]:X2}；Revision=A0h.94=0x{a0[94]:X2}",
            [
                new ProtocolExtensionInfo(
                    "sff-8690",
                    "SFF-8690 可调谐扩展",
                    "1.5",
                    tunable,
                    tunable
                        ? $"A0h.65.6=1；Page 02h {(page02Present ? "已读取" : "未读取")}。"
                        : "A0h.65.6=0，未声明可调谐扩展。")
            ]);
    }

    private static IReadOnlyList<DecodedField> DecodeSemanticFields(
        ModuleDump dump,
        ModuleInformation information,
        IReadOnlyList<Measurement> measurements,
        ICollection<DecodeDiagnostic> diagnostics)
    {
        var a0 = dump.FindRegion(A0RegionId)!.Data;
        var a2 = dump.FindRegion(A2LowerRegionId)!.Data;
        var fields = new List<DecodedField>(180);

        Add(fields, "协议识别", "Identifier", Sff8024CodeTables.LookupIdentifier(a0[0]), "A0h.0",
            $"原始值 0x{a0[0]:X2}；管理接口类型是解释其余内存的首要依据。");
        Add(fields, "协议识别", "Extended Identifier", LookupExtendedIdentifier(a0[1]), "A0h.1",
            $"原始值 0x{a0[1]:X2}");
        Add(fields, "协议识别", "Connector", Sff8024CodeTables.LookupConnector(a0[2]), "A0h.2",
            $"SFF-8024 Rev 4.14，原始值 0x{a0[2]:X2}");
        Add(fields, "协议识别", "Encoding", Sff8024CodeTables.LookupEncoding(a0[11]), "A0h.11",
            $"SFF-8024 Rev 4.14，原始值 0x{a0[11]:X2}");
        Add(fields, "协议识别", "SFF-8472 Revision Compliance", LookupCompliance(a0[94]), "A0h.94");
        Add(fields, "协议识别", "SFF-8690", YesNo((a0[65] & 0x40) != 0), "A0h.65.6",
            "此位是 SFF-8690 的规范检测依据。");

        foreach (var item in LegacyComplianceBits)
        {
            Add(fields, "兼容性/传统位图", item.Name, YesNo(IsSet(a0[item.Byte], item.Bit)),
                $"A0h.{item.Byte}.{item.Bit}");
        }

        Add(fields, "兼容性/扩展码", "Primary Extended Compliance",
            Sff8024CodeTables.LookupExtendedCompliance(a0[36]), "A0h.36",
            $"原始值 0x{a0[36]:X2}");
        Add(fields, "兼容性/扩展码", "Secondary Extended Compliance",
            Sff8024CodeTables.LookupExtendedCompliance(a2[67]), "A2h.67",
            $"原始值 0x{a2[67]:X2}；仅在非外部校准布局中有效。");

        DecodeRateAndLengthFields(a0, fields);
        DecodeA0CapabilityFields(a0, fields);
        if (information.ExternallyCalibrated)
        {
            DecodeExternalCalibrationFields(a2, fields);
        }
        else
        {
            DecodeEnhancedFeatureFields(a2, fields);
        }

        DecodeRuntimeControlFields(a2, fields);
        DecodePage02Fields(dump, fields);
        DecodeSff8690Fields(a0, dump, fields);
        DecodeTimingFields(dump, measurements, fields, diagnostics);
        return fields;
    }

    private static void DecodeRateAndLengthFields(byte[] a0, ICollection<DecodedField> fields)
    {
        var cable = (a0[8] & 0x0C) != 0;
        Add(fields, "链路与速率", "Nominal Signaling Rate",
            a0[12] == 0 ? "未指定" : a0[12] == 0xFF ? $"{a0[66] * 250:N0} MBd" : $"{a0[12] * 100:N0} MBd",
            a0[12] == 0xFF ? "A0h.12,66" : "A0h.12");
        Add(fields, "链路与速率", "Rate Identifier", LookupRateIdentifier(a0[13]), "A0h.13",
            $"原始值 0x{a0[13]:X2}");

        if (a0[12] != 0xFF)
        {
            Add(fields, "链路与速率", "Signaling Rate Upper Margin",
                a0[66] == 0 ? "未指定" : $"+{a0[66]}%", "A0h.66");
            Add(fields, "链路与速率", "Signaling Rate Lower Margin",
                a0[67] == 0 ? "未指定" : $"-{a0[67]}%", "A0h.67");
        }
        else
        {
            Add(fields, "链路与速率", "Signaling Rate Tolerance",
                a0[67] == 0 ? "未指定" : $"±{a0[67]}%", "A0h.67");
        }

        if (cable)
        {
            Add(fields, "链路与速率", "Copper Attenuation @ 12.9 GHz",
                a0[14] == 0 ? "未知" : $"{a0[14]} dB", "A0h.14");
            Add(fields, "链路与速率", "Copper Attenuation @ 25.78 GHz",
                a0[15] == 0 ? "未知" : $"{a0[15]} dB", "A0h.15");
            Add(fields, "链路与速率", "Copper/Active Cable Length",
                FormatLimit(a0[18], 1, "m"), "A0h.18");
            var multiplier = new[] { 0.1, 1.0, 10.0, 100.0 }[a0[19] >> 6];
            var baseLength = a0[19] & 0x3F;
            Add(fields, "链路与速率", "Additional Cable Length",
                baseLength == 0 ? "未指定" : $"{baseLength * multiplier:0.###} m", "A0h.19",
                $"Base={baseLength}，Multiplier={multiplier:0.###}");
            DecodeCableCompliance(a0, fields);
        }
        else
        {
            Add(fields, "链路与速率", "SMF Length (km)", FormatLimit(a0[14], 1, "km"), "A0h.14");
            Add(fields, "链路与速率", "SMF Length (100 m)", FormatLimit(a0[15], 0.1, "km"), "A0h.15");
            Add(fields, "链路与速率", "OM2 Length", FormatLimit(a0[16], 10, "m"), "A0h.16");
            Add(fields, "链路与速率", "OM1 Length", FormatLimit(a0[17], 10, "m"), "A0h.17");
            Add(fields, "链路与速率", "OM4 Length", FormatLimit(a0[18], 10, "m"), "A0h.18");
            Add(fields, "链路与速率", "OM3 Length", FormatLimit(a0[19], 10, "m"), "A0h.19");
            Add(fields, "链路与速率", "Nominal Wavelength",
                $"{ReadUInt16(a0, 60)} nm", "A0h.60-61");
        }
    }

    private static void DecodeA0CapabilityFields(byte[] a0, ICollection<DecodedField> fields)
    {
        AddBit(fields, "模块能力/Options", "Power Level 4 Required", a0, 64, 6);
        AddBit(fields, "模块能力/Options", "Power Level 3/4 Required", a0, 64, 5);
        AddBit(fields, "模块能力/Options", "Paging Implemented", a0, 64, 4);
        AddBit(fields, "模块能力/Options", "Retimer/CDR Present", a0, 64, 3);
        AddBit(fields, "模块能力/Options", "Cooled Transceiver", a0, 64, 2);
        AddBit(fields, "模块能力/Options", "Power Level 2 Required", a0, 64, 1);
        AddBit(fields, "模块能力/Options", "Linear Receiver Output", a0, 64, 0);
        AddBit(fields, "模块能力/Options", "Receiver Decision Threshold", a0, 65, 7);
        AddBit(fields, "模块能力/Options", "Tunable Transmitter (SFF-8690)", a0, 65, 6);
        AddBit(fields, "模块能力/Options", "RATE_SELECT", a0, 65, 5);
        AddBit(fields, "模块能力/Options", "TX_DISABLE", a0, 65, 4);
        AddBit(fields, "模块能力/Options", "TX_FAULT", a0, 65, 3);
        AddBit(fields, "模块能力/Options", "Inverted LOS", a0, 65, 2);
        AddBit(fields, "模块能力/Options", "RX_LOS", a0, 65, 1);
        AddBit(fields, "模块能力/Options", "Additional Pages Require Discovery", a0, 65, 0);

        AddBit(fields, "模块能力/诊断", "Digital Diagnostic Monitoring", a0, 92, 6);
        AddBit(fields, "模块能力/诊断", "Internally Calibrated", a0, 92, 5);
        AddBit(fields, "模块能力/诊断", "Externally Calibrated", a0, 92, 4);
        Add(fields, "模块能力/诊断", "RX Power Measurement",
            IsSet(a0[92], 3) ? "Average Power" : "OMA", "A0h.92.3");
        AddBit(fields, "模块能力/诊断", "Address Change Required", a0, 92, 2);
        AddBit(fields, "模块能力/诊断", "Remote Performance Monitoring", a0, 92, 1);

        AddBit(fields, "模块能力/增强选项", "Alarm/Warning Flags", a0, 93, 7);
        AddBit(fields, "模块能力/增强选项", "Soft TX_DISABLE", a0, 93, 6);
        AddBit(fields, "模块能力/增强选项", "Soft TX_FAULT", a0, 93, 5);
        AddBit(fields, "模块能力/增强选项", "Soft RX_LOS", a0, 93, 4);
        AddBit(fields, "模块能力/增强选项", "Soft RATE_SELECT", a0, 93, 3);
        AddBit(fields, "模块能力/增强选项", "Legacy Application Select", a0, 93, 2);
        AddBit(fields, "模块能力/增强选项", "Soft Rate Select (SFF-8431)", a0, 93, 1);
    }

    private static void DecodeEnhancedFeatureFields(byte[] a2, ICollection<DecodedField> fields)
    {
        Add(fields, "增强功能/保留位", "Byte 56 Reserved Bits",
            $"0b{Convert.ToString((a2[56] >> 5) & 0x07, 2).PadLeft(3, '0')}",
            "A2h.56.7-5", "规范保留；非零值只原样显示，不推断厂商含义。");
        AddBit(fields, "增强功能/声明", "RS0/RS1 Pin State Ignore", a2, 56, 4);
        Add(fields, "增强功能/声明", "TX Squelch Method",
            ((a2[56] >> 2) & 0x03) switch
            {
                0 => "未实现",
                1 => "降低 OMA",
                2 => "降低平均功率 Pave",
                _ => "用户可选择 OMA/Pave"
            }, "A2h.56.3-2");
        AddBit(fields, "增强功能/声明", "TX Force Squelch", a2, 56, 1);
        AddBit(fields, "增强功能/声明", "TX Squelch Disable", a2, 56, 0);
        Add(fields, "增强功能/保留位", "Byte 57 Reserved Bits",
            $"0x{a2[57] >> 2:X2}", "A2h.57.7-2");
        AddBit(fields, "增强功能/声明", "RX Force Squelch", a2, 57, 1);
        AddBit(fields, "增强功能/声明", "RX Squelch Disable", a2, 57, 0);
        Add(fields, "增强功能/保留位", "Byte 58 Reserved Bits",
            $"0x{a2[58] >> 1:X2}", "A2h.58.7-1");
        AddBit(fields, "增强功能/声明", "TX Adaptive Input EQ Fail Flag", a2, 58, 0);
        Add(fields, "增强功能/保留位", "Byte 59 Reserved",
            $"0x{a2[59]:X2}", "A2h.59");
        Add(fields, "增强功能/保留位", "Byte 60 Reserved Bits",
            $"0b{Convert.ToString((a2[60] >> 5) & 0x07, 2).PadLeft(3, '0')}",
            "A2h.60.7-5");
        Add(fields, "增强功能/声明", "TX Input EQ Store/Recall",
            ((a2[60] >> 3) & 0x03) == 1 ? "已实现" : "未实现/保留", "A2h.60.4-3");
        AddBit(fields, "增强功能/声明", "TX Input EQ Freeze", a2, 60, 2);
        AddBit(fields, "增强功能/声明", "Adaptive TX Input EQ", a2, 60, 1);
        AddBit(fields, "增强功能/声明", "Manual TX Input EQ", a2, 60, 0);
        Add(fields, "增强功能/声明", "Adaptive TX EQ Max Settling Time",
            $"{a2[61] * 100} ms", "A2h.61");
        Add(fields, "增强功能/保留位", "Byte 62 Reserved Bits",
            $"0b{Convert.ToString((a2[62] >> 5) & 0x07, 2).PadLeft(3, '0')}",
            "A2h.62.7-5");
        Add(fields, "增强功能/声明", "RX Output EQ Type",
            ((a2[62] >> 3) & 0x03) switch
            {
                0 => "未实现/固定峰峰值",
                1 => "固定稳态幅度",
                2 => "固定峰峰值与稳态幅度平均",
                _ => "保留"
            }, "A2h.62.4-3");
        Add(fields, "增强功能/声明", "RX Enhanced Output EQ",
            ((a2[62] >> 1) & 0x03) switch
            {
                0 => "未实现",
                1 => "Pre-cursor",
                2 => "Post-cursor",
                _ => "Pre-cursor + Post-cursor"
            }, "A2h.62.2-1");
        AddBit(fields, "增强功能/声明", "RX Output Amplitude Control", a2, 62, 0);
        for (var bit = 7; bit >= 4; bit--)
        {
            AddBit(fields, "增强功能/声明", $"RX Amplitude Code {bit - 4:X1}", a2, 63, bit);
        }

        Add(fields, "增强功能/声明", "Max Manual TX Input EQ", $"{a2[63] & 0x0F} dB", "A2h.63.3-0");
        Add(fields, "增强功能/声明", "Max RX Output EQ Post-cursor", $"{a2[64] >> 4}", "A2h.64.7-4");
        Add(fields, "增强功能/声明", "Max RX Output EQ Pre-cursor", $"{a2[64] & 0x0F}", "A2h.64.3-0");
        Add(fields, "增强功能/保留位", "Byte 65 Reserved",
            $"0x{a2[65]:X2}", "A2h.65");
        Add(fields, "增强功能/声明", "Maximum Power Consumption", $"{a2[66] * 0.1:0.0} W", "A2h.66");
        Add(fields, "增强功能/保留位", "Future Advertisement",
            $"0x{a2[68]:X2} {a2[69]:X2}", "A2h.68-69");
        Add(fields, "增强功能/保留位", "Future Status",
            $"0x{a2[70]:X2}", "A2h.70");

        Add(fields, "增强功能/保留位", "Byte 71 Reserved Bits",
            $"0x{a2[71] >> 4:X1}", "A2h.71.7-4");
        AddBit(fields, "增强功能/控制", "TX EQ Adaptation Recall", a2, 71, 3, writable: true);
        AddBit(fields, "增强功能/控制", "TX EQ Adaptation Store", a2, 71, 2, writable: true);
        AddBit(fields, "增强功能/控制", "TX EQ Adaptation Freeze", a2, 71, 1, writable: true);
        AddBit(fields, "增强功能/控制", "TX EQ Adaptation Enable", a2, 71, 0, writable: true);
        Add(fields, "增强功能/控制", "RX Output EQ Pre-cursor", $"{a2[72] >> 5}", "A2h.72.7-5", writable: true);
        AddBit(fields, "增强功能/控制", "RX Enhanced EQ Override", a2, 72, 4, writable: true);
        Add(fields, "增强功能/控制", "RX Output EQ Post-cursor", $"{a2[72] & 0x0F}", "A2h.72.3-0", writable: true);
        Add(fields, "增强功能/保留位", "Byte 73 Reserved Bits",
            $"0b{Convert.ToString((a2[73] >> 5) & 0x07, 2).PadLeft(3, '0')}",
            "A2h.73.7-5");
        AddBit(fields, "增强功能/控制", "Ignore RS0/RS1 Pin State", a2, 73, 4, writable: true);
        Add(fields, "增强功能/控制", "RX Output Amplitude", $"{a2[73] & 0x0F}", "A2h.73.3-0", writable: true);
        Add(fields, "增强功能/保留位", "Byte 74 Reserved Bits 7-6",
            $"0b{Convert.ToString((a2[74] >> 6) & 0x03, 2).PadLeft(2, '0')}",
            "A2h.74.7-6");
        AddBit(fields, "增强功能/控制", "RX Force Squelch", a2, 74, 5, writable: true);
        AddBit(fields, "增强功能/控制", "RX Squelch Disable", a2, 74, 4, writable: true);
        Add(fields, "增强功能/保留位", "Byte 74 Reserved Bit 3",
            YesNo(IsSet(a2[74], 3)), "A2h.74.3");
        AddBit(fields, "增强功能/控制", "TX Squelch Uses Pave", a2, 74, 2, writable: true);
        AddBit(fields, "增强功能/控制", "TX Force Squelch", a2, 74, 1, writable: true);
        AddBit(fields, "增强功能/控制", "TX Squelch Disable", a2, 74, 0, writable: true);
    }

    private static void DecodeExternalCalibrationFields(byte[] a2, ICollection<DecodedField> fields)
    {
        for (var power = 4; power >= 0; power--)
        {
            var offset = 56 + (4 - power) * 4;
            Add(fields, "外部校准", $"Rx_PWR({power})",
                ReadSingle(a2, offset).ToString("G9", CultureInfo.InvariantCulture),
                $"A2h.{offset}-{offset + 3}", "IEEE 754 单精度多项式系数");
        }

        Add(fields, "外部校准", "Tx_I slope", $"{ReadUInt16(a2, 76) / 256d:G9}", "A2h.76-77");
        Add(fields, "外部校准", "Tx_I offset", $"{ReadInt16(a2, 78)}", "A2h.78-79");
        Add(fields, "外部校准", "Tx_PWR slope", $"{ReadUInt16(a2, 80) / 256d:G9}", "A2h.80-81");
        Add(fields, "外部校准", "Tx_PWR offset", $"{ReadInt16(a2, 82)}", "A2h.82-83");
        Add(fields, "外部校准", "T slope", $"{ReadUInt16(a2, 84) / 256d:G9}", "A2h.84-85");
        Add(fields, "外部校准", "T offset", $"{ReadInt16(a2, 86)}", "A2h.86-87");
        Add(fields, "外部校准", "V slope", $"{ReadUInt16(a2, 88) / 256d:G9}", "A2h.88-89");
        Add(fields, "外部校准", "V offset", $"{ReadInt16(a2, 90)}", "A2h.90-91");
    }

    private static void DecodeRuntimeControlFields(byte[] a2, ICollection<DecodedField> fields)
    {
        Add(fields, "实时控制", "TX Input EQ (High Rate)", DecodeInputEq(a2[114] >> 4), "A2h.114.7-4", writable: true);
        Add(fields, "实时控制", "TX Input EQ (Low Rate)", DecodeInputEq(a2[114] & 0x0F), "A2h.114.3-0", writable: true);
        Add(fields, "实时控制", "RX Output Emphasis (High Rate)", DecodeOutputEmphasis(a2[115] >> 4), "A2h.115.7-4", writable: true);
        Add(fields, "实时控制", "RX Output Emphasis (Low Rate)", DecodeOutputEmphasis(a2[115] & 0x0F), "A2h.115.3-0", writable: true);
    }

    private static void DecodeCableCompliance(byte[] a0, ICollection<DecodedField> fields)
    {
        if (IsSet(a0[8], 2))
        {
            AddBit(fields, "电缆规范", "FC-PI-4 Appendix H", a0, 60, 1);
            AddBit(fields, "电缆规范", "SFF-8431 Appendix E", a0, 60, 0);
        }

        if (IsSet(a0[8], 3))
        {
            AddBit(fields, "电缆规范", "FC-PI-4 Limiting", a0, 60, 3);
            AddBit(fields, "电缆规范", "SFF-8431 Limiting", a0, 60, 2);
            AddBit(fields, "电缆规范", "FC-PI-4 Appendix H", a0, 60, 1);
            AddBit(fields, "电缆规范", "SFF-8431 Appendix E", a0, 60, 0);
        }
    }

    private DecodedModule? DecodeRemoteModule(
        ModuleDump dump,
        ICollection<DecodeDiagnostic> diagnostics)
    {
        var a0Lower = dump.FindRegion(RemotePage20RegionId)?.Data;
        var a0Upper = dump.FindRegion(RemotePage21RegionId)?.Data;
        var a2Lower = dump.FindRegion(RemotePage22RegionId)?.Data;
        if (a0Lower is not { Length: >= 128 }
            || a0Upper is not { Length: >= 128 }
            || a2Lower is not { Length: >= 128 })
        {
            return null;
        }

        var a0 = new byte[256];
        a0Lower.AsSpan(0, 128).CopyTo(a0);
        a0Upper.AsSpan(0, 128).CopyTo(a0.AsSpan(128));
        if (a0[0] is not (0x02 or 0x03))
        {
            diagnostics.Add(new DecodeDiagnostic(
                DiagnosticSeverity.Warning,
                $"RPM 远端页已读取，但远端 Identifier=0x{a0[0]:X2}，无法按 SFF-8472 解码。",
                RemotePage20RegionId));
            return null;
        }

        var regions = new List<MemoryRegionData>
        {
            new(A0RegionId, "远端 A0h", 0x50, 0, a0),
            new(A2LowerRegionId, "远端 A2h Lower", 0x51, 0, a2Lower[..128], Volatile: true)
        };
        AddRemoteUpper(RemotePage23RegionId, A2Page00RegionId, "远端 A2h Page 00h", 0x00);
        AddRemoteUpper(RemotePage24RegionId, A2Page02RegionId, "远端 A2h Page 02h", 0x02);

        try
        {
            return DecodeCore(
                new ModuleDump(
                    dump.FormatVersion,
                    Id,
                    Revision,
                    dump.CapturedAt,
                    $"{dump.Source} / RPM 远端模块",
                    regions,
                    new Dictionary<string, string> { ["transport"] = "SFF-8472 RPM mirror" }),
                includeRemoteModule: false);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            diagnostics.Add(new DecodeDiagnostic(
                DiagnosticSeverity.Warning,
                $"RPM 远端模块解码失败：{ex.Message}",
                RemotePage20RegionId));
            return null;
        }

        void AddRemoteUpper(string sourceId, string targetId, string name, byte page)
        {
            var data = dump.FindRegion(sourceId)?.Data;
            if (data is { Length: >= 128 })
            {
                regions.Add(new MemoryRegionData(targetId, name, 0x51, 128, data[..128], page, Volatile: true));
            }
        }
    }

    private static void Add(
        ICollection<DecodedField> fields,
        string category,
        string name,
        string value,
        string source,
        string description = "",
        string unit = "",
        bool writable = false) =>
        fields.Add(new DecodedField(category, name, value, source, description, unit, writable));

    private static void AddBit(
        ICollection<DecodedField> fields,
        string category,
        string name,
        byte[] source,
        int offset,
        int bit,
        string description = "",
        bool writable = false) =>
        Add(fields, category, name, YesNo(IsSet(source[offset], bit)),
            $"{(source.Length <= 128 ? "A2h" : "A0h")}.{offset}.{bit}",
            description, writable: writable);

    private static bool IsSet(byte value, int bit) => (value & (1 << bit)) != 0;

    private static string YesNo(bool value) => value ? "是" : "否";

    private static string FormatLimit(byte raw, double multiplier, string unit) =>
        raw switch
        {
            0 => "未指定/不支持",
            0xFF => $">{254 * multiplier:0.###} {unit}",
            _ => $"{raw * multiplier:0.###} {unit}"
        };

    private static string LookupExtendedIdentifier(byte value) => value switch
    {
        0x00 => "GBIC 未指定/不符合已定义 MOD_DEF",
        0x01 => "GBIC MOD_DEF 1",
        0x02 => "GBIC MOD_DEF 2",
        0x03 => "GBIC MOD_DEF 3",
        0x04 => "GBIC/SFP 由 2-wire interface ID 定义",
        0x05 => "GBIC MOD_DEF 5",
        0x06 => "GBIC MOD_DEF 6",
        0x07 => "GBIC MOD_DEF 7",
        _ => $"保留 (0x{value:X2})"
    };

    private static string LookupRateIdentifier(byte value) => value switch
    {
        0x00 => "未指定",
        0x01 => "SFF-8079 4/2/1G Rate Select",
        0x02 => "SFF-8431 8/4/2G RX Rate Select",
        0x04 => "SFF-8431 8/4/2G TX Rate Select",
        0x06 => "SFF-8431 8/4/2G 独立 RX/TX Rate Select",
        0x08 => "FC-PI-5 16/8/4G RX Rate Select",
        0x0A => "FC-PI-5 16/8/4G 独立 RX/TX Rate Select",
        0x0C => "FC-PI-6 32/16/8G 独立 RX/TX Rate Select",
        0x0E => "10/8G Retimer/CDR Rate Select",
        0x10 => "FC-PI-7 64/32/16G 独立 RX/TX Rate Select",
        0x20 => "按 A0h.36/A2h.67 PMD 选择速率",
        >= 0x12 and <= 0x1F or >= 0x21 => $"保留 (0x{value:X2})",
        _ => $"未指定的兼容值 (0x{value:X2})"
    };

    private static string DecodeInputEq(int code) =>
        code <= 10 ? $"{code} dB" : "保留";

    private static string DecodeOutputEmphasis(int code) =>
        code <= 7 ? $"{code} dB" : $"厂商自定义 (0x{code:X1})";

    private static float ReadSingle(byte[] source, int offset)
    {
        var bits = BinaryPrimitives.ReadInt32BigEndian(source.AsSpan(offset, 4));
        return BitConverter.Int32BitsToSingle(bits);
    }
}
