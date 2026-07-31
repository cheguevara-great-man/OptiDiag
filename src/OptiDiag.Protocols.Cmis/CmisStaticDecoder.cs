using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Cmis;

internal static class CmisStaticDecoder
{
    public static void Decode(
        ModuleDump dump,
        byte[] lower,
        byte[] page00,
        byte[]? page01,
        byte[]? page02,
        List<DecodedField> fields)
    {
        var sink = new CmisFieldSink(fields);
        DecodeLower(lower, page01, sink);
        DecodePage00(page00, sink);
        if (page01 is { Length: 128 })
        {
            DecodePage01(lower, page01, sink);
        }

        if (page02 is { Length: 128 })
        {
            DecodePage02(page01, page02, sink);
        }

        DecodeApplicationDescriptors(lower, page01, sink);
        AddCaptureSummary(dump, sink);
    }

    private static void DecodeLower(byte[] lower, byte[]? page01, CmisFieldSink sink)
    {
        sink.Add("协议", "SFF8024Identifier", CmisCodeTables.Identifier(lower[0]), "Lower.0");
        sink.Add("协议", "CmisRevision", $"{lower[1] >> 4}.{lower[1] & 0x0F}", "Lower.1");
        sink.Bool("管理特性", "MemoryModelFlat", lower[2], 7, "Lower.2.7",
            "否=分页内存；是=仅 Lower 与 Upper Page 00h");
        sink.Bool("管理特性", "SteppedConfigOnly", lower[2], 6, "Lower.2.6");
        sink.Enum("管理特性", "MciMaxSpeed", CmisDecoderHelpers.Bits(lower[2], 5, 2),
            CmisCodeTables.MciMaxSpeed, "Lower.2.5-2");
        sink.Enum("管理特性", "AutoCommissioning", CmisDecoderHelpers.Bits(lower[2], 1, 0),
            value => value switch
            {
                0 => CmisDecoderHelpers.Bit(lower[2], 6) ? "不支持自动配置" : "常规和热重配置",
                1 => "仅常规自动配置",
                2 => "仅热重配置",
                _ => "保留"
            }, "Lower.2.1-0");

        sink.Enum("模块状态", "ModuleState", CmisDecoderHelpers.Bits(lower[3], 3, 1),
            value => CmisCodeTables.ModuleState((byte)value), "Lower.3.3-1");
        sink.Bool("模块状态", "InterruptDeasserted", lower[3], 0, "Lower.3.0");

        for (var bank = 0; bank < 4; bank++)
        {
            var value = lower[4 + bank];
            sink.Bool("标志摘要", $"Bank{bank}Page11Flags", value, 0, $"Lower.{4 + bank}.0");
            sink.Bool("标志摘要", $"Bank{bank}Page12Flags", value, 1, $"Lower.{4 + bank}.1");
            sink.Bool("标志摘要", $"Bank{bank}Page14Flags", value, 2, $"Lower.{4 + bank}.2");
            sink.Bool("标志摘要", $"Bank{bank}Page2CFlags", value, 3, $"Lower.{4 + bank}.3");
        }

        AddModuleFlagBits(sink, lower[8], 8,
            (7, "CdbCmdCompleteFlag2"),
            (6, "CdbCmdCompleteFlag1"),
            (2, "DataPathFirmwareErrorFlag"),
            (1, "ModuleFirmwareErrorFlag"),
            (0, "ModuleStateChangedFlag"));
        AddThresholdFlagBits(sink, lower[9], 9, "Vcc", "Temp");
        AddThresholdFlagBits(sink, lower[10], 10, "Aux2", "Aux1");
        AddThresholdFlagBits(sink, lower[11], 11, "CustomMon", "Aux3");
        sink.Hex("模块标志", "CustomModuleFlags", lower.AsSpan(13, 1), "Lower.13");

        sink.Add("模块监控", "TempMonValue",
            CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.S16(lower, 14) / 256d, "°C"),
            "Lower.14-15");
        sink.Add("模块监控", "VccMonVoltage",
            CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(lower, 16) * 0.0001, "V"),
            "Lower.16-17");
        DecodeAuxMonitors(lower, page01, sink);

        sink.Bool("模块控制", "BankBroadcastEnable", lower[26], 7, "Lower.26.7", writable: true);
        sink.Bool("模块控制", "LowPwrAllowRequestHW", lower[26], 6, "Lower.26.6", writable: true);
        sink.Bool("模块控制", "SquelchMethodSelect", lower[26], 5, "Lower.26.5",
            "否=降低 OMA；是=降低平均功率", true);
        sink.Bool("模块控制", "LowPwrRequestSW", lower[26], 4, "Lower.26.4", writable: true);
        sink.Bool("模块控制", "SoftwareReset", lower[26], 3, "Lower.26.3",
            "只写、自清除触发位", true);
        sink.Add("模块控制", "CustomGlobalControl", $"0b{Convert.ToString(lower[26] & 0x07, 2).PadLeft(3, '0')}",
            "Lower.26.2-0", writable: true);
        sink.Enum("模块控制", "MciSpeedConfiguration", lower[27] & 0x0F,
            CmisCodeTables.MciConfiguredSpeed, "Lower.27.3-0", writable: true);
        sink.Hex("模块控制", "CustomControls", lower.AsSpan(29, 2), "Lower.29-30",
            writable: true);

        AddModuleMaskBits(sink, lower[31], 31,
            (7, "CdbCmdCompleteMask2"),
            (6, "CdbCmdCompleteMask1"),
            (2, "DataPathFirmwareErrorMask"),
            (1, "ModuleFirmwareErrorMask"),
            (0, "ModuleStateChangedMask"));
        AddThresholdMaskBits(sink, lower[32], 32, "Vcc", "Temp");
        AddThresholdMaskBits(sink, lower[33], 33, "Aux2", "Aux1");
        AddThresholdMaskBits(sink, lower[34], 34, "CustomMon", "Aux3");
        sink.Hex("模块屏蔽", "CustomModuleMasks", lower.AsSpan(36, 1), "Lower.36",
            writable: true);

        DecodeCdbStatus(lower[37], 1, sink);
        DecodeCdbStatus(lower[38], 2, sink);
        sink.Add("固件", "ModuleActiveFirmwareRevision", $"{lower[39]}.{lower[40]}", "Lower.39-40");
        sink.Enum("故障", "ModuleFaultCause", lower[41], CmisCodeTables.ModuleFaultCause, "Lower.41");
        sink.Enum("密码", "PasswordCmdResult", lower[42] & 0x0F, CmisCodeTables.PasswordResult,
            "Lower.42.3-0");

        sink.Enum("扩展信息", "CmisSmSupport", lower[56], CmisCodeTables.StateMachineSupport,
            "Lower.56");
        sink.Enum("扩展信息", "ModuleFunctionType", lower[57], CmisCodeTables.ModuleFunctionType,
            "Lower.57");
        sink.Add("扩展信息", "SFF8024ModuleSubtype", $"0x{lower[60] & 0x0F:X}", "Lower.60.3-0");
        sink.Enum("扩展信息", "SFF8024FiberFaceType", lower[61] & 0x03,
            value => value switch { 0 => "未知/不适用", 1 => "PC/UPC", 2 => "APC", _ => "保留" },
            "Lower.61.1-0");
        sink.Hex("扩展信息", "LowPowerRestrictions", lower.AsSpan(62, 1), "Lower.62");

        sink.Add("媒体", "MediaType", CmisCodeTables.MediaType(lower[85]), "Lower.85");
        sink.Hex("密码", "PasswordEntryAndChange", lower.AsSpan(118, 8), "Lower.118-125",
            "敏感只写区域；界面不解释或回显密码语义", true);
        sink.Add("页面映射", "BankSelect", $"0x{lower[126]:X2}", "Lower.126", writable: true);
        sink.Add("页面映射", "PageSelect", $"0x{lower[127]:X2}", "Lower.127", writable: true);
    }

    private static void DecodePage00(byte[] page, CmisFieldSink sink)
    {
        sink.Add("身份", "SFF8024IdentifierCopy", CmisCodeTables.Identifier(page[0]), "P00h.128");
        sink.Add("身份", "VendorName", CmisDecoderHelpers.Ascii(page, 1, 16), "P00h.129-144");
        sink.Add("身份", "VendorOUI", $"{page[17]:X2}-{page[18]:X2}-{page[19]:X2}", "P00h.145-147");
        sink.Add("身份", "VendorPN", CmisDecoderHelpers.Ascii(page, 20, 16), "P00h.148-163");
        sink.Add("身份", "VendorRev", CmisDecoderHelpers.Ascii(page, 36, 2), "P00h.164-165");
        sink.Add("身份", "VendorSN", CmisDecoderHelpers.Ascii(page, 38, 16), "P00h.166-181");
        sink.Add("身份", "DateCode", CmisDecoderHelpers.Ascii(page, 54, 8), "P00h.182-189",
            "YYMMDD + vendor lot code");
        sink.Add("身份", "CLEICode", CmisDecoderHelpers.Ascii(page, 62, 10), "P00h.190-199");

        sink.Enum("功耗", "ModulePowerClass", CmisDecoderHelpers.Bits(page[72], 7, 5),
            value => $"Class {value + 1}", "P00h.200.7-5");
        sink.Add("功耗", "MaximumPower", CmisDecoderHelpers.FormatNumber(page[73] * 0.25, "W"),
            "P00h.201");
        sink.Add("线缆", "CableAssemblyLinkLength", CmisCodeTables.CableLength(page[74]),
            "P00h.202");
        sink.Add("媒体", "ConnectorType", CmisCodeTables.Connector(page[75]), "P00h.203");

        var attenuationFrequencies = new[] { "5 GHz", "7 GHz", "12.9 GHz", "25.8 GHz", "53.1 GHz" };
        for (var index = 0; index < attenuationFrequencies.Length; index++)
        {
            sink.Add("线缆", $"CableAttenuation@{attenuationFrequencies[index]}",
                CmisDecoderHelpers.FormatNumber(page[76 + index], "dB", 0),
                $"P00h.{204 + index}");
        }

        sink.Hex("媒体", "MediaLaneSupport", page.AsSpan(82, 1), "P00h.210",
            "置位表示相应媒体通道不受支持");
        sink.Hex("媒体", "FarEndConfiguration", page.AsSpan(83, 1), "P00h.211");
        sink.Add("媒体", "MediaInterfaceTechnology", CmisCodeTables.MediaTechnology(page[84]),
            "P00h.212");
        sink.Bool("MCI", "MciFlowControlDurationEncoding", page[85], 7, "P00h.213.7",
            "否=静态字节数；是=速度相关时长");
        sink.Add("MCI", "MciFlowControlDuration", page[85] & 0x7F, "P00h.213.6-0");
        sink.Add("校验", "Page00Checksum", $"0x{page[94]:X2}", "P00h.222");
        sink.Hex("厂商", "CustomInformation", page.AsSpan(95, 33), "P00h.223-255");
    }

    private static void DecodePage01(byte[] lower, byte[] page, CmisFieldSink sink)
    {
        sink.Add("版本", "ModuleInactiveFirmwareRevision", $"{page[0]}.{page[1]}", "P01h.128-129");
        sink.Add("版本", "ModuleHardwareRevision", $"{page[2]}.{page[3]}", "P01h.130-131");

        var primaryMultiplierCode = page[4] >> 6;
        var smfMultiplier = primaryMultiplierCode switch
        {
            0 => 0.1,
            1 => 1,
            2 => 10,
            _ => (page[9] >> 6) switch { 0 => 50, 1 => 100, 2 => 200, _ => 500 }
        };
        sink.Add("链路", "SupportedLengthSMF",
            CmisDecoderHelpers.FormatNumber((page[4] & 0x3F) * smfMultiplier, "km"),
            "P01h.132,137");
        sink.Add("链路", "SupportedLengthOM5", CmisDecoderHelpers.FormatNumber(page[5] * 2, "m", 0),
            "P01h.133");
        sink.Add("链路", "SupportedLengthOM4", CmisDecoderHelpers.FormatNumber(page[6] * 2, "m", 0),
            "P01h.134");
        sink.Add("链路", "SupportedLengthOM3", CmisDecoderHelpers.FormatNumber(page[7] * 2, "m", 0),
            "P01h.135");
        sink.Add("链路", "SupportedLengthOM2", CmisDecoderHelpers.FormatNumber(page[8], "m", 0),
            "P01h.136");
        sink.Add("媒体", "NominalWavelength",
            CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(page, 10) * 0.05, "nm"),
            "P01h.138-139");
        sink.Add("媒体", "WavelengthTolerance",
            $"±{CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(page, 12) * 0.005, "nm")}",
            "P01h.140-141");

        var supportedPages = page[14];
        sink.Bool("页面能力", "NetworkPathPagesSupported", supportedPages, 7, "P01h.142.7");
        sink.Bool("页面能力", "VDMPagesSupported", supportedPages, 6, "P01h.142.6");
        sink.Bool("页面能力", "DiagnosticPagesSupported", supportedPages, 5, "P01h.142.5");
        sink.Bool("页面能力", "CoherentPagesSupported", supportedPages, 4, "P01h.142.4",
            "具体语义由 C-CMIS 补充规范定义");
        sink.Bool("页面能力", "CmisFfSupported", supportedPages, 3, "P01h.142.3",
            "Page 05h 具体语义由 CMIS-FF 补充规范定义");
        sink.Bool("页面能力", "Page03hSupported", supportedPages, 2, "P01h.142.2");
        sink.Add("页面能力", "BanksSupported", CmisMemoryMap.DecodeLaneBankCount(supportedPages),
            "P01h.142.1-0", "每个 Lane Bank 表示 8 条通道");

        var modSel = page[15];
        var modSelUs = (modSel & 0x1F) * Math.Pow(2, modSel >> 5);
        sink.Add("时序", "ModSelWaitTime", CmisDecoderHelpers.FormatNumber(modSelUs, "µs"),
            "P01h.143");
        sink.Enum("时序", "MaxDurationDPDeinit", page[16] >> 4, CmisCodeTables.StateDuration,
            "P01h.144.7-4");
        sink.Enum("时序", "MaxDurationDPInit", page[16] & 0x0F, CmisCodeTables.StateDuration,
            "P01h.144.3-0");

        sink.Bool("模块特性", "CoolingImplemented", page[17], 7, "P01h.145.7");
        sink.Enum("模块特性", "TxInputClockingCapabilities", CmisDecoderHelpers.Bits(page[17], 6, 5),
            value => value switch
            {
                0 => "Lane 1-8 同步",
                1 => "Lane 1-4 与 5-8 分组同步",
                2 => "每两条 Lane 分组同步",
                _ => "各 Lane 可异步"
            }, "P01h.145.6-5");
        sink.Bool("模块特性", "ePPSSupported", page[17], 4, "P01h.145.4");
        sink.Bool("模块特性", "TimingPage15hSupported", page[17], 3, "P01h.145.3");
        sink.Enum("模块特性", "Aux3MonObservable", CmisDecoderHelpers.Bits(page[17], 2, 2),
            value => value == 0 ? "激光器温度" : "第二路供电电压", "P01h.145.2");
        sink.Enum("模块特性", "Aux2MonObservable", CmisDecoderHelpers.Bits(page[17], 1, 1),
            value => value == 0 ? "激光器温度" : "TEC 电流", "P01h.145.1");
        sink.Enum("模块特性", "Aux1MonObservable", CmisDecoderHelpers.Bits(page[17], 0, 0),
            value => value == 0 ? "自定义" : "TEC 电流", "P01h.145.0");
        sink.Add("模块特性", "ModuleTempMax", $"{unchecked((sbyte)page[18])} °C", "P01h.146");
        sink.Add("模块特性", "ModuleTempMin", $"{unchecked((sbyte)page[19])} °C", "P01h.147");
        sink.Add("模块特性", "PropagationDelay",
            CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(page, 20) * 10, "ns", 0),
            "P01h.148-149");
        sink.Add("模块特性", "OperatingVoltageMin",
            CmisDecoderHelpers.FormatNumber(page[22] * 0.02, "V"),
            "P01h.150");
        DecodeReceiverCharacteristics(page, sink);

        DecodeControlAdvertisements(page, sink);
        DecodeFlagAndMonitorAdvertisements(page, sink);
        DecodeSignalIntegrityAdvertisements(page, sink);
        DecodeCdbAdvertisements(page, sink);
        DecodeAdditionalDurations(page, sink);

        var nadBanks = page[47] & 0x0F;
        sink.Add("应用能力", "NADBanksSupported", nadBanks, "P01h.175.3-0",
            $"{nadBanks * 15} 个 Normalized Application Descriptor 容量");
        for (var app = 0; app < 15; app++)
        {
            sink.Add("应用能力", $"MediaLaneAssignmentOptionsApp{app + 1}",
                $"0b{Convert.ToString(page[48 + app], 2).PadLeft(8, '0')}",
                $"P01h.{176 + app}");
        }

        sink.Enum("管理能力", "ScratchPadSupported", page[123] >> 6,
            CmisCodeTables.TernarySupport, "P01h.251.7-6");
        sink.Enum("管理能力", "PasswordEntrySupported", (page[123] >> 4) & 0x03,
            CmisCodeTables.TernarySupport, "P01h.251.5-4");
        sink.Enum("管理能力", "PasswordEntryResultSupported", (page[123] >> 2) & 0x03,
            CmisCodeTables.TernarySupport, "P01h.251.3-2");
        sink.Enum("管理能力", "FullPageReadSupported", page[123] & 0x03,
            CmisCodeTables.TernarySupport, "P01h.251.1-0");
        sink.Bool("页面能力", "HostLaneSwitchingSupported", page[124], 7, "P01h.252.7");
        sink.Bool("页面能力", "LinkTrainingSupported", page[124], 6, "P01h.252.6",
            "具体语义由 CMIS-LT 补充规范定义");
        sink.Add("校验", "Page01Checksum", $"0x{page[127]:X2}", "P01h.255");

        if (lower[85] is not 0x01 and not 0x02)
        {
            sink.Add("提示", "WavelengthApplicability", "不适用",
                "Lower.85 / P01h.138-141", "非光媒体不应把波长字段解释为有效光学参数");
        }
    }

    private static void DecodePage02(byte[]? page01, byte[] page, CmisFieldSink sink)
    {
        var biasMultiplier = page01 is { Length: 128 }
            ? ((page01[32] >> 3) & 0x03) switch { 0 => 1, 1 => 2, 2 => 4, _ => 1 }
            : 1;

        AddThresholdSet(sink, page, 0, "TempMon", raw => (short)raw / 256d, "°C");
        AddThresholdSet(sink, page, 8, "VccMon", raw => raw * 0.0001, "V");
        AddThresholdSet(sink, page, 16, "Aux1Mon", raw => (short)raw, "raw");
        AddThresholdSet(sink, page, 24, "Aux2Mon", raw => (short)raw, "raw");
        AddThresholdSet(sink, page, 32, "Aux3Mon", raw => (short)raw, "raw");
        AddThresholdSet(sink, page, 40, "CustomMon", raw => raw, "raw");
        AddThresholdSet(sink, page, 48, "TxPower", raw => raw * 0.0001, "mW");
        AddThresholdSet(sink, page, 56, "TxBias", raw => raw * 0.002 * biasMultiplier, "mA");
        AddThresholdSet(sink, page, 64, "RxPower", raw => raw * 0.0001, "mW");
        sink.Hex("阈值", "ReservedThresholdArea", page.AsSpan(72, 55), "P02h.200-254");
        sink.Add("校验", "Page02Checksum", $"0x{page[127]:X2}", "P02h.255");
    }

    private static void DecodeApplicationDescriptors(byte[] lower, byte[]? page01, CmisFieldSink sink)
    {
        for (var index = 0; index < 15; index++)
        {
            byte host;
            byte media;
            byte laneCounts;
            byte hostAssignments;
            byte mediaAssignments;
            int absoluteStart;

            if (index < 8)
            {
                var offset = 86 + index * 4;
                host = lower[offset];
                media = lower[offset + 1];
                laneCounts = lower[offset + 2];
                hostAssignments = lower[offset + 3];
                mediaAssignments = page01 is { Length: 128 } ? page01[48 + index] : (byte)0;
                absoluteStart = offset;
            }
            else if (page01 is { Length: 128 })
            {
                var offset = 95 + (index - 8) * 4;
                host = page01[offset];
                media = page01[offset + 1];
                laneCounts = page01[offset + 2];
                hostAssignments = page01[offset + 3];
                mediaAssignments = page01[48 + index];
                absoluteStart = 128 + offset;
            }
            else
            {
                break;
            }

            if (host == 0xFF)
            {
                break;
            }

            var source = index < 8
                ? $"Lower.{absoluteStart}-{absoluteStart + 3},P01h.{176 + index}"
                : $"P01h.{absoluteStart}-{absoluteStart + 3},P01h.{176 + index}";
            sink.Add(
                "应用描述符",
                $"Application {index + 1}",
                $"{CmisCodeTables.HostInterface(host)} ↔ {CmisCodeTables.MediaInterface(media, lower[85])}",
                source,
                $"Host lanes={laneCounts >> 4}; Media lanes={laneCounts & 0x0F}; "
                + $"Host starts=0x{hostAssignments:X2}; Media starts=0x{mediaAssignments:X2}");
        }
    }

    private static void DecodeAuxMonitors(byte[] lower, byte[]? page01, CmisFieldSink sink)
    {
        var characteristics = page01 is { Length: 128 } ? page01[17] : (byte)0;
        AddAux(1, 18, (characteristics & 0x01) != 0 ? "TEC 电流" : "自定义");
        AddAux(2, 20, (characteristics & 0x02) != 0 ? "TEC 电流" : "激光器温度");
        AddAux(3, 22, (characteristics & 0x04) != 0 ? "第二路供电电压" : "激光器温度");
        sink.Add("模块监控", "CustomMonValue", CmisDecoderHelpers.S16(lower, 24), "Lower.24-25",
            "有符号/无符号及缩放由厂商定义");
        return;

        void AddAux(int number, int offset, string observable)
        {
            var raw = CmisDecoderHelpers.S16(lower, offset);
            var value = observable switch
            {
                "激光器温度" => CmisDecoderHelpers.FormatNumber(raw / 256d, "°C"),
                "第二路供电电压" => CmisDecoderHelpers.FormatNumber((ushort)raw * 0.0001, "V"),
                "TEC 电流" => CmisDecoderHelpers.FormatNumber(raw / 32767d * 100, "%"),
                _ => raw.ToString()
            };
            sink.Add("模块监控", $"Aux{number}MonValue", value, $"Lower.{offset}-{offset + 1}",
                $"Observable={observable}");
        }
    }

    private static void DecodeReceiverCharacteristics(byte[] page, CmisFieldSink sink)
    {
        sink.Enum("模块特性", "OpticalDetectorType", page[23] >> 7,
            value => value == 0 ? "PIN" : "APD", "P01h.151.7");
        sink.Enum("模块特性", "RxOutputEqType", (page[23] >> 5) & 0x03,
            value => value switch
            {
                0 => "峰峰值保持/未实现",
                1 => "稳态幅度保持",
                2 => "峰峰与稳态幅度平均保持",
                _ => "保留"
            }, "P01h.151.6-5");
        sink.Enum("模块特性", "RxPowerMeasurementType", (page[23] >> 4) & 1,
            value => value == 0 ? "OMA" : "平均功率", "P01h.151.4");
        sink.Enum("模块特性", "RxLOSType", (page[23] >> 3) & 1,
            value => value == 0 ? "OMA" : "Pav", "P01h.151.3");
        sink.Bool("模块特性", "RxLOSIsFast", page[23], 2, "P01h.151.2");
        sink.Bool("模块特性", "TxDisableIsFast", page[23], 1, "P01h.151.1");
        sink.Bool("模块特性", "TxDisableIsModuleWide", page[23], 0, "P01h.151.0");
        sink.Add("功耗", "CDRPowerSavedPerLane",
            CmisDecoderHelpers.FormatNumber(page[24] * 0.01, "W"), "P01h.152");
        for (var code = 0; code < 4; code++)
        {
            sink.Bool("信号完整性", $"RxOutputLevel{code}Supported", page[25], 4 + code,
                $"P01h.153.{4 + code}");
        }

        sink.Add("信号完整性", "TxInputEqMax", page[25] & 0x0F, "P01h.153.3-0");
        sink.Add("信号完整性", "RxOutputEqPostCursorMax", page[26] >> 4, "P01h.154.7-4");
        sink.Add("信号完整性", "RxOutputEqPreCursorMax", page[26] & 0x0F, "P01h.154.3-0");
    }

    private static void DecodeControlAdvertisements(byte[] page, CmisFieldSink sink)
    {
        sink.Bool("控制能力", "WavelengthIsControllable", page[27], 7, "P01h.155.7");
        sink.Bool("控制能力", "TransmitterIsTunable", page[27], 6, "P01h.155.6");
        sink.Enum("控制能力", "SquelchMethodTx", (page[27] >> 4) & 0x03,
            value => value switch
            {
                0 => "不支持",
                1 => "降低 OMA",
                2 => "降低平均功率",
                _ => "主机选择 OMA/Pav"
            }, "P01h.155.5-4");
        sink.Bool("控制能力", "ForcedSquelchTxSupported", page[27], 3, "P01h.155.3");
        sink.Bool("控制能力", "AutoSquelchDisableTxSupported", page[27], 2, "P01h.155.2");
        sink.Bool("控制能力", "OutputDisableTxSupported", page[27], 1, "P01h.155.1");
        sink.Bool("控制能力", "InputPolarityFlipTxSupported", page[27], 0, "P01h.155.0");
        sink.Bool("控制能力", "BankBroadcastSupported", page[28], 7, "P01h.156.7");
        sink.Bool("控制能力", "AutoSquelchDisableRxSupported", page[28], 2, "P01h.156.2");
        sink.Bool("控制能力", "OutputDisableRxSupported", page[28], 1, "P01h.156.1");
        sink.Bool("控制能力", "OutputPolarityFlipRxSupported", page[28], 0, "P01h.156.0");
    }

    private static void DecodeFlagAndMonitorAdvertisements(byte[] page, CmisFieldSink sink)
    {
        sink.Bool("标志能力", "AdaptiveInputEqFailFlagTxSupported", page[29], 3, "P01h.157.3");
        sink.Bool("标志能力", "CDRLOLFlagTxSupported", page[29], 2, "P01h.157.2");
        sink.Bool("标志能力", "LOSFlagTxSupported", page[29], 1, "P01h.157.1");
        sink.Bool("标志能力", "FailureFlagTxSupported", page[29], 0, "P01h.157.0");
        sink.Bool("标志能力", "CDRLOLFlagRxSupported", page[30], 2, "P01h.158.2");
        sink.Bool("标志能力", "LOSFlagRxSupported", page[30], 1, "P01h.158.1");

        var moduleMonitors = new[] { "Temp", "Vcc", "Aux1", "Aux2", "Aux3", "Custom" };
        for (var bit = 0; bit < moduleMonitors.Length; bit++)
        {
            sink.Bool("监控能力", $"{moduleMonitors[bit]}MonSupported", page[31], bit,
                $"P01h.159.{bit}");
        }

        sink.Add("监控能力", "TxBiasCurrentScalingFactor",
            ((page[32] >> 3) & 0x03) switch { 0 => "×1", 1 => "×2", 2 => "×4", _ => "保留" },
            "P01h.160.4-3");
        sink.Bool("监控能力", "RxOpticalPowerMonSupported", page[32], 2, "P01h.160.2");
        sink.Bool("监控能力", "TxOpticalPowerMonSupported", page[32], 1, "P01h.160.1");
        sink.Bool("监控能力", "TxBiasMonSupported", page[32], 0, "P01h.160.0");
    }

    private static void DecodeSignalIntegrityAdvertisements(byte[] page, CmisFieldSink sink)
    {
        sink.Add("信号完整性", "TxInputEqRecallBuffersSupported",
            ((page[33] >> 5) & 0x03) switch { 0 => "0", 1 => "1", 2 => "2", _ => "保留" },
            "P01h.161.6-5");
        sink.Bool("信号完整性", "TxInputEqFreezeSupported", page[33], 4, "P01h.161.4");
        sink.Bool("信号完整性", "TxInputAdaptiveEqSupported", page[33], 3, "P01h.161.3");
        sink.Bool("信号完整性", "TxInputEqHostControlSupported", page[33], 2, "P01h.161.2");
        sink.Bool("信号完整性", "TxCDRBypassControlSupported", page[33], 1, "P01h.161.1");
        sink.Bool("信号完整性", "TxCDRSupported", page[33], 0, "P01h.161.0");
        sink.Bool("信号完整性", "VersatileControlSetSupported", page[34], 7, "P01h.162.7",
            "参数空间由 CMIS-VCS 补充规范定义");
        sink.Bool("信号完整性", "UnidirReconfigSupported", page[34], 6, "P01h.162.6");
        sink.Bool("信号完整性", "StagedSet1Supported", page[34], 5, "P01h.162.5");
        sink.Enum("信号完整性", "RxOutputEqControlSupported", (page[34] >> 3) & 0x03,
            value => value switch { 0 => "不支持", 1 => "仅 Pre-cursor", 2 => "仅 Post-cursor", _ => "Pre/Post-cursor" },
            "P01h.162.4-3");
        sink.Bool("信号完整性", "RxOutputAmplitudeControlSupported", page[34], 2, "P01h.162.2");
        sink.Bool("信号完整性", "RxCDRBypassControlSupported", page[34], 1, "P01h.162.1");
        sink.Bool("信号完整性", "RxCDRSupported", page[34], 0, "P01h.162.0");
    }

    private static void DecodeCdbAdvertisements(byte[] page, CmisFieldSink sink)
    {
        var value = page[35];
        sink.Add("CDB 能力", "CdbInstancesSupported", CmisMemoryMap.DecodeCdbInstanceCount(value),
            "P01h.163.7-6");
        sink.Bool("CDB 能力", "CdbBackgroundModeSupported", value, 5, "P01h.163.5");
        sink.Bool("CDB 能力", "CdbAutoPagingSupported", value, 4, "P01h.163.4");
        sink.Add("CDB 能力", "CdbMaxPagesEPL", CmisMemoryMap.DecodeCdbEplPageCount(value),
            "P01h.163.3-0");
        sink.Add("CDB 能力", "CdbReadWriteLengthExtension", page[36],
            "P01h.164", $"EPL 最大单次访问 {8 * (1 + page[36])} bytes；LPL 最大 {8 * (1 + Math.Min(page[36], (byte)15))} bytes");
        sink.Hex("CDB 能力", "CdbCommandTriggerMethod", page.AsSpan(37, 1), "P01h.165");
        sink.Hex("CDB 能力", "CdbFeaturesSupported", page.AsSpan(38, 1), "P01h.166");
    }

    private static void DecodeAdditionalDurations(byte[] page, CmisFieldSink sink)
    {
        sink.Enum("时序", "MaxDurationModulePwrUp", page[39] >> 4, CmisCodeTables.StateDuration,
            "P01h.167.7-4");
        sink.Enum("时序", "MaxDurationModulePwrDn", page[39] & 0x0F, CmisCodeTables.StateDuration,
            "P01h.167.3-0");
        sink.Enum("时序", "MaxDurationDPTxTurnOn", page[40] >> 4, CmisCodeTables.StateDuration,
            "P01h.168.7-4");
        sink.Enum("时序", "MaxDurationDPTxTurnOff", page[40] & 0x0F, CmisCodeTables.StateDuration,
            "P01h.168.3-0");
        sink.Add("时序", "MaxDurationBPCScaling", page[41] & 0x0F, "P01h.169.3-0",
            "Bank/Page Change 最大保持时间缩放指数");
    }

    private static void DecodeCdbStatus(byte value, int instance, CmisFieldSink sink)
    {
        sink.Bool("CDB 状态", $"CdbIsBusy{instance}", value, 7, $"Lower.{36 + instance}.7");
        sink.Bool("CDB 状态", $"CdbHasFailed{instance}", value, 6, $"Lower.{36 + instance}.6");
        sink.Enum("CDB 状态", $"CdbCommandResult{instance}", value & 0x3F,
            code => CmisCodeTables.CdbResult(CmisDecoderHelpers.Bit(value, 7), CmisDecoderHelpers.Bit(value, 6), code),
            $"Lower.{36 + instance}.5-0");
    }

    private static void AddModuleFlagBits(
        CmisFieldSink sink,
        byte value,
        int offset,
        params (int Bit, string Name)[] definitions)
    {
        foreach (var definition in definitions)
        {
            sink.Bool("模块标志", definition.Name, value, definition.Bit, $"Lower.{offset}.{definition.Bit}",
                "锁存，只读/读清除");
        }
    }

    private static void AddModuleMaskBits(
        CmisFieldSink sink,
        byte value,
        int offset,
        params (int Bit, string Name)[] definitions)
    {
        foreach (var definition in definitions)
        {
            sink.Bool("模块屏蔽", definition.Name, value, definition.Bit, $"Lower.{offset}.{definition.Bit}",
                writable: true);
        }
    }

    private static void AddThresholdFlagBits(
        CmisFieldSink sink,
        byte value,
        int offset,
        string upperObservable,
        string lowerObservable)
    {
        var suffixes = new[]
        {
            "HighAlarm", "LowAlarm", "HighWarning", "LowWarning"
        };
        for (var bit = 0; bit < 8; bit++)
        {
            var observable = bit < 4 ? lowerObservable : upperObservable;
            var suffix = suffixes[bit % 4];
            sink.Bool("模块标志", $"{observable}{suffix}Flag", value, bit, $"Lower.{offset}.{bit}",
                "锁存，只读/读清除");
        }
    }

    private static void AddThresholdMaskBits(
        CmisFieldSink sink,
        byte value,
        int offset,
        string upperObservable,
        string lowerObservable)
    {
        var suffixes = new[]
        {
            "HighAlarm", "LowAlarm", "HighWarning", "LowWarning"
        };
        for (var bit = 0; bit < 8; bit++)
        {
            var observable = bit < 4 ? lowerObservable : upperObservable;
            var suffix = suffixes[bit % 4];
            sink.Bool("模块屏蔽", $"{observable}{suffix}Mask", value, bit, $"Lower.{offset}.{bit}",
                writable: true);
        }
    }

    private static void AddThresholdSet(
        CmisFieldSink sink,
        byte[] page,
        int offset,
        string name,
        Func<ushort, double> converter,
        string unit)
    {
        var suffixes = new[] { "HighAlarm", "LowAlarm", "HighWarning", "LowWarning" };
        for (var index = 0; index < 4; index++)
        {
            var raw = CmisDecoderHelpers.U16(page, offset + index * 2);
            sink.Add("阈值", $"{name}{suffixes[index]}",
                CmisDecoderHelpers.FormatNumber(converter(raw), unit),
                $"P02h.{128 + offset + index * 2}-{129 + offset + index * 2}");
        }
    }

    private static void AddCaptureSummary(ModuleDump dump, CmisFieldSink sink)
    {
        var standard = dump.Regions.Count(x =>
            x.Page is { } page && CmisMemoryMap.Definition(page)?.Coverage == CmisPageCoverage.BaseSpecification);
        var supplement = dump.Regions.Count(x =>
            x.Page is { } page && CmisMemoryMap.Definition(page)?.Coverage == CmisPageCoverage.ExternalSupplement);
        sink.Add("采集", "CapturedRegions", dump.Regions.Count, "Dump.Regions");
        sink.Add("采集", "BaseCmisRegions", standard, "CMIS Table 8-1");
        sink.Add("采集", "ExternalSupplementRawRegions", supplement, "CMIS Table 8-1",
            "这些页由额外规范定义，只保留原始数据");
    }
}
