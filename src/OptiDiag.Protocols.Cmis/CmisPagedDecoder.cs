using System.Buffers.Binary;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Cmis;

internal static class CmisPagedDecoder
{
    public static void Decode(ModuleDump dump, List<DecodedField> fields)
    {
        var sink = new CmisFieldSink(fields);
        foreach (var region in dump.Regions.Where(x => x.Page.HasValue))
        {
            if (region.Data.Length < 128 || region.Page is not { } page)
            {
                continue;
            }

            switch (page)
            {
                case 0x04:
                    DecodeLaserCapabilities(region.Data, sink);
                    break;
                case 0x10:
                    DecodePage10(region, sink);
                    break;
                case 0x11:
                    DecodePage11(region, sink);
                    break;
                case 0x12:
                    DecodePage12(region, sink);
                    break;
                case 0x13:
                    DecodePage13(region, sink);
                    break;
                case 0x14:
                    DecodePage14(region, sink);
                    break;
                case 0x15:
                    DecodePage15(region, sink);
                    break;
                case 0x16:
                    DecodePage16(region, sink);
                    break;
                case 0x17:
                    DecodePage17(region, sink);
                    break;
                case 0x18:
                    DecodePage18(region, sink);
                    break;
                case 0x19:
                    DecodePage19(region, sink);
                    break;
                case 0x1C:
                    DecodePage1C(region, dump, sink);
                    break;
                case 0x1D:
                    DecodePage1D(region, sink);
                    break;
                case >= 0x05 and <= 0x0B or >= 0x1A and <= 0x1B or >= 0x30 and <= 0x5F:
                    DecodeExternalSupplement(region, sink);
                    break;
            }
        }
    }

    private static void DecodeLaserCapabilities(byte[] page, CmisFieldSink sink)
    {
        var grids = new[]
        {
            (0, "3.125", 0.003125),
            (1, "6.25", 0.00625),
            (2, "12.5", 0.0125),
            (3, "25", 0.025),
            (4, "50", 0.05),
            (5, "100", 0.1),
            (6, "33.333", 0.1 / 3),
            (7, "75", 0.025)
        };
        foreach (var grid in grids)
        {
            sink.Bool("可调谐能力", $"GridSupported{grid.Item2}GHz", page[0], grid.Item1,
                $"P04h.128.{grid.Item1}");
        }

        sink.Bool("可调谐能力", "FineTuningSupported", page[1], 7, "P04h.129.7");
        sink.Bool("可调谐能力", "GridSupported150GHz", page[1], 6, "P04h.129.6");

        var gridNames = new[] { "3.125", "6.25", "12.5", "25", "50", "100", "33.333", "75", "150" };
        var gridStepThz = new[] { 0.003125, 0.00625, 0.0125, 0.025, 0.05, 0.1, 0.1 / 3, 0.025, 0.025 };
        for (var index = 0; index < gridNames.Length; index++)
        {
            var offset = 2 + index * 4;
            var low = CmisDecoderHelpers.S16(page, offset);
            var high = CmisDecoderHelpers.S16(page, offset + 2);
            var lowFrequency = index == 8
                ? 193.1 + (low + 3) * gridStepThz[index]
                : 193.1 + low * gridStepThz[index];
            var highFrequency = index == 8
                ? 193.1 + (high + 3) * gridStepThz[index]
                : 193.1 + high * gridStepThz[index];
            sink.Add("可调谐能力", $"Grid{gridNames[index]}GHzRange",
                $"n={low}..{high}; {lowFrequency:0.######}..{highFrequency:0.######} THz",
                $"P04h.{130 + index * 4}-{133 + index * 4}");
        }

        sink.Add("可调谐能力", "FineTuningResolution",
            CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(page, 62) * 0.001, "GHz"),
            "P04h.190-191");
        sink.Add("可调谐能力", "FineTuningLowOffset",
            CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.S16(page, 64) * 0.001, "GHz"),
            "P04h.192-193");
        sink.Add("可调谐能力", "FineTuningHighOffset",
            CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.S16(page, 66) * 0.001, "GHz"),
            "P04h.194-195");
        sink.Bool("可调谐能力", "ProgOutputPowerPerLaneSupported", page[68], 7, "P04h.196.7");
        sink.Add("可调谐能力", "ProgOutputPowerMin",
            CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.S16(page, 70) * 0.01, "dBm"),
            "P04h.198-199");
        sink.Add("可调谐能力", "ProgOutputPowerMax",
            CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.S16(page, 72) * 0.01, "dBm"),
            "P04h.200-201");
        sink.Add("校验", "Page04Checksum", $"0x{page[127]:X2}", "P04h.255");
    }

    private static void DecodePage10(MemoryRegionData region, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        AddLaneBitmap(sink, "通道直接控制", "DPDeinit", page[0], bank, 0x10, 128, true);
        AddLaneBitmap(sink, "通道直接控制", "InputPolarityFlipTx", page[1], bank, 0x10, 129, true);
        AddLaneBitmap(sink, "通道直接控制", "OutputDisableTx", page[2], bank, 0x10, 130, true);
        AddLaneBitmap(sink, "通道直接控制", "AutoSquelchDisableTx", page[3], bank, 0x10, 131, true);
        AddLaneBitmap(sink, "通道直接控制", "OutputSquelchForceTx", page[4], bank, 0x10, 132, true);
        AddLaneBitmap(sink, "通道直接控制", "AdaptiveInputEqFreezeTx", page[6], bank, 0x10, 134, true);
        DecodeTwoBitLanes(page, 7, bank, 0x10, 135, "AdaptiveInputEqStoreTx", sink, true);
        DecodeTwoBitLanes(page, 8, bank, 0x10, 136, "AdaptiveInputEqStoreTx", sink, true, 4);
        AddLaneBitmap(sink, "通道直接控制", "OutputPolarityFlipRx", page[9], bank, 0x10, 137, true);
        AddLaneBitmap(sink, "通道直接控制", "OutputDisableRx", page[10], bank, 0x10, 138, true);
        AddLaneBitmap(sink, "通道直接控制", "AutoSquelchDisableRx", page[11], bank, 0x10, 139, true);

        DecodeStagedControlSet(page, bank, 0, 15, sink);
        DecodeStagedControlSet(page, bank, 1, 50, sink);

        var masks = new[]
        {
            "DPStateChangedMask", "FailureMaskTx", "LOSMaskTx", "CDRLOLMaskTx",
            "AdaptiveInputEqFailMaskTx", "TxPowerHighAlarmMask", "TxPowerLowAlarmMask",
            "TxPowerHighWarningMask", "TxPowerLowWarningMask", "TxBiasHighAlarmMask",
            "TxBiasLowAlarmMask", "TxBiasHighWarningMask", "TxBiasLowWarningMask",
            "LOSMaskRx", "CDRLOLMaskRx", "RxPowerHighAlarmMask", "RxPowerLowAlarmMask",
            "RxPowerHighWarningMask", "RxPowerLowWarningMask", "OutputStatusChangedMaskRx"
        };
        for (var index = 0; index < masks.Length; index++)
        {
            AddLaneBitmap(sink, "通道屏蔽", masks[index], page[85 + index], bank, 0x10,
                213 + index, true);
        }
    }

    private static void DecodeStagedControlSet(
        byte[] page,
        byte bank,
        int set,
        int start,
        CmisFieldSink sink)
    {
        AddLaneBitmap(sink, $"SCS{set}", "ApplyDPInit", page[start], bank, 0x10, 128 + start, true,
            "单字节写触发");
        AddLaneBitmap(sink, $"SCS{set}", "ApplyImmediate", page[start + 1], bank, 0x10,
            129 + start, true, "单字节写触发");
        for (var lane = 0; lane < 8; lane++)
        {
            var value = page[start + 2 + lane];
            var absoluteLane = bank * 8 + lane + 1;
            sink.Add($"SCS{set}", $"Lane{absoluteLane}DataPathConfiguration",
                $"AppSel={value >> 4}; DataPathID={(value >> 1) & 7}; ExplicitControl={value & 1}",
                CmisDecoderHelpers.Source(0x10, 130 + start + lane, bank), writable: true);
        }

        AddLaneBitmap(sink, $"SCS{set}", "AdaptiveInputEqEnableTx", page[start + 10], bank, 0x10,
            138 + start, true);
        DecodeTwoBitLanes(page, start + 11, bank, 0x10, 139 + start, $"SCS{set}AdaptiveEqRecall", sink, true);
        DecodeTwoBitLanes(page, start + 12, bank, 0x10, 140 + start, $"SCS{set}AdaptiveEqRecall", sink, true, 4);
        DecodeNibbleLanes(page, start + 13, bank, 0x10, 141 + start, $"SCS{set}TxInputEqTarget", sink, true);
        AddLaneBitmap(sink, $"SCS{set}", "CDREnableTx", page[start + 17], bank, 0x10,
            145 + start, true);
        AddLaneBitmap(sink, $"SCS{set}", "CDREnableRx", page[start + 18], bank, 0x10,
            146 + start, true);
        DecodeNibbleLanes(page, start + 19, bank, 0x10, 147 + start, $"SCS{set}RxPreCursor", sink, true);
        DecodeNibbleLanes(page, start + 23, bank, 0x10, 151 + start, $"SCS{set}RxPostCursor", sink, true);
        DecodeNibbleLanes(page, start + 27, bank, 0x10, 155 + start, $"SCS{set}RxAmplitude", sink, true);
        AddLaneBitmap(sink, $"SCS{set}", "ApplyImmediateTx", page[start + 33], bank, 0x10,
            161 + start, true, "单字节写触发");
        AddLaneBitmap(sink, $"SCS{set}", "ApplyImmediateRx", page[start + 34], bank, 0x10,
            162 + start, true, "单字节写触发");
    }

    private static void DecodePage11(MemoryRegionData region, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        for (var lane = 0; lane < 8; lane++)
        {
            var state = lane % 2 == 0 ? page[lane / 2] & 0x0F : page[lane / 2] >> 4;
            var absoluteLane = bank * 8 + lane + 1;
            sink.Add("数据通道状态", $"Lane{absoluteLane}DPState",
                CmisCodeTables.DataPathState((byte)state),
                CmisDecoderHelpers.Source(0x11, 128 + lane / 2, bank));
            sink.Add("输出状态", $"Lane{absoluteLane}OutputStatusRx",
                CmisDecoderHelpers.Bool(CmisDecoderHelpers.Bit(page[4], lane)),
                CmisDecoderHelpers.Source(0x11, 132, bank, lane.ToString()));
            sink.Add("输出状态", $"Lane{absoluteLane}OutputStatusTx",
                CmisDecoderHelpers.Bool(CmisDecoderHelpers.Bit(page[5], lane)),
                CmisDecoderHelpers.Source(0x11, 133, bank, lane.ToString()));
        }

        var flags = new[]
        {
            "DPStateChangedFlag", "FailureFlagTx", "LOSFlagTx", "CDRLOLFlagTx",
            "AdaptiveInputEqFailFlagTx", "TxPowerHighAlarmFlag", "TxPowerLowAlarmFlag",
            "TxPowerHighWarningFlag", "TxPowerLowWarningFlag", "TxBiasHighAlarmFlag",
            "TxBiasLowAlarmFlag", "TxBiasHighWarningFlag", "TxBiasLowWarningFlag",
            "LOSFlagRx", "CDRLOLFlagRx", "RxPowerHighAlarmFlag", "RxPowerLowAlarmFlag",
            "RxPowerHighWarningFlag", "RxPowerLowWarningFlag", "OutputStatusChangedFlagRx"
        };
        for (var index = 0; index < flags.Length; index++)
        {
            AddLaneBitmap(sink, "通道锁存标志", flags[index], page[6 + index], bank, 0x11,
                134 + index, false, "只读/读清除");
        }

        for (var lane = 0; lane < 8; lane++)
        {
            var absoluteLane = bank * 8 + lane + 1;
            sink.Add("通道监控", $"Lane{absoluteLane}TxPower",
                CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(page, 26 + lane * 2) * 0.0001, "mW"),
                CmisDecoderHelpers.Source(0x11, 154 + lane * 2, bank));
            sink.Add("通道监控", $"Lane{absoluteLane}TxBiasCurrentRaw",
                CmisDecoderHelpers.U16(page, 42 + lane * 2),
                CmisDecoderHelpers.Source(0x11, 170 + lane * 2, bank),
                "最终 mA 值还需乘以 Page 01h:160.4-3 声明的倍数");
            sink.Add("通道监控", $"Lane{absoluteLane}RxPower",
                CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(page, 58 + lane * 2) * 0.0001, "mW"),
                CmisDecoderHelpers.Source(0x11, 186 + lane * 2, bank));

            var config = lane % 2 == 0 ? page[74 + lane / 2] & 0x0F : page[74 + lane / 2] >> 4;
            sink.Enum("配置结果", $"Lane{absoluteLane}ConfigStatus", config,
                CmisCodeTables.ConfigurationStatus,
                CmisDecoderHelpers.Source(0x11, 202 + lane / 2, bank));

            var dp = page[78 + lane];
            sink.Add("活动控制集", $"Lane{absoluteLane}DPConfig",
                $"AppSel={dp >> 4}; DataPathID={(dp >> 1) & 7}; ExplicitControl={dp & 1}",
                CmisDecoderHelpers.Source(0x11, 206 + lane, bank));
        }

        AddLaneBitmap(sink, "活动控制集", "AdaptiveInputEqEnableTx", page[86], bank, 0x11, 214);
        AddLaneBitmap(sink, "活动控制集", "CDREnableTx", page[93], bank, 0x11, 221);
        AddLaneBitmap(sink, "活动控制集", "CDREnableRx", page[94], bank, 0x11, 222);
        AddLaneBitmap(sink, "数据通道条件", "DPInitPending", page[107], bank, 0x11, 235);
        AddLaneBitmap(sink, "数据通道条件", "TxOutputReady", page[108], bank, 0x11, 236);

        for (var lane = 0; lane < 8; lane++)
        {
            var absoluteLane = bank * 8 + lane + 1;
            var tx = page[112 + lane];
            var rx = page[120 + lane];
            sink.Add("媒体映射", $"Lane{absoluteLane}TxMapping",
                $"Wavelength={tx >> 4}; Fiber={tx & 0x0F}",
                CmisDecoderHelpers.Source(0x11, 240 + lane, bank));
            sink.Add("媒体映射", $"Lane{absoluteLane}RxMapping",
                $"Wavelength={rx >> 4}; Fiber={rx & 0x0F}",
                CmisDecoderHelpers.Source(0x11, 248 + lane, bank));
        }
    }

    private static void DecodePage12(MemoryRegionData region, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        for (var lane = 0; lane < 8; lane++)
        {
            var absoluteLane = bank * 8 + lane + 1;
            var grid = page[lane] >> 4;
            sink.Add("激光调谐", $"Lane{absoluteLane}GridSpacing",
                CmisCodeTables.GridSpacing(grid),
                CmisDecoderHelpers.Source(0x12, 128 + lane, bank), writable: true);
            sink.Bool("激光调谐", $"Lane{absoluteLane}FineTuningEnable", page[lane], 0,
                CmisDecoderHelpers.Source(0x12, 128 + lane, bank, "0"), writable: true);
            sink.Add("激光调谐", $"Lane{absoluteLane}ChannelNumber",
                CmisDecoderHelpers.S16(page, 8 + lane * 2),
                CmisDecoderHelpers.Source(0x12, 136 + lane * 2, bank), writable: true);
            sink.Add("激光调谐", $"Lane{absoluteLane}FineTuningOffset",
                CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.S16(page, 24 + lane * 2) * 0.001, "GHz"),
                CmisDecoderHelpers.Source(0x12, 152 + lane * 2, bank), writable: true);
            sink.Add("激光调谐", $"Lane{absoluteLane}CurrentLaserFrequency",
                CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U32(page, 40 + lane * 4) * 0.001, "GHz"),
                CmisDecoderHelpers.Source(0x12, 168 + lane * 4, bank));
            sink.Add("激光调谐", $"Lane{absoluteLane}TargetOutputPower",
                CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.S16(page, 72 + lane * 2) * 0.01, "dBm"),
                CmisDecoderHelpers.Source(0x12, 200 + lane * 2, bank), writable: true);
            sink.Bool("激光调谐状态", $"Lane{absoluteLane}TuningInProgress", page[94 + lane], 1,
                CmisDecoderHelpers.Source(0x12, 222 + lane, bank, "1"));
            sink.Bool("激光调谐状态", $"Lane{absoluteLane}WavelengthUnlocked", page[94 + lane], 0,
                CmisDecoderHelpers.Source(0x12, 222 + lane, bank, "0"));
            DecodeTuningFlags(page[103 + lane], absoluteLane, bank, 231 + lane, false, sink);
            DecodeTuningFlags(page[111 + lane], absoluteLane, bank, 239 + lane, true, sink);
        }

        AddLaneBitmap(sink, "激光调谐标志", "LaserTuningFlagSummary", page[102], bank, 0x12, 230);
    }

    private static void DecodePage13(MemoryRegionData region, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        var loopbacks = new[]
        {
            "MediaSideOutputLoopback", "MediaSideInputLoopback", "HostSideOutputLoopback",
            "HostSideInputLoopback", "PerLaneHostSideLoopbacks", "PerLaneMediaSideLoopbacks",
            "SimultaneousHostAndMediaSideLoopbacks"
        };
        for (var bit = 0; bit < loopbacks.Length; bit++)
        {
            sink.Bool("诊断能力", $"{loopbacks[bit]}Supported", page[0], bit,
                CmisDecoderHelpers.Source(0x13, 128, bank, bit.ToString()));
        }

        sink.Enum("诊断能力", "GatingSupport", page[1] >> 6,
            value => value switch
            {
                0 => "不支持（主机定义测量时间）",
                1 => "支持，时间精度 ≤2 ms",
                2 => "支持，时间精度 ≤20 ms",
                _ => "支持，时间精度 >20 ms"
            }, CmisDecoderHelpers.Source(0x13, 129, bank, "7-6"));
        sink.Bool("诊断能力", "GatingResultsSupported", page[1], 5,
            CmisDecoderHelpers.Source(0x13, 129, bank, "5"));
        sink.Bool("诊断能力", "PeriodicUpdatesSupported", page[1], 4,
            CmisDecoderHelpers.Source(0x13, 129, bank, "4"));
        sink.Bool("诊断能力", "PerLaneGatingTimersSupported", page[1], 3,
            CmisDecoderHelpers.Source(0x13, 129, bank, "3"));
        sink.Bool("诊断能力", "AutoRestartGatingSupported", page[1], 2,
            CmisDecoderHelpers.Source(0x13, 129, bank, "2"));
        sink.Bool("诊断能力", "MediaSideFEC", page[2], 7,
            CmisDecoderHelpers.Source(0x13, 130, bank, "7"));
        sink.Bool("诊断能力", "HostSideFEC", page[2], 6,
            CmisDecoderHelpers.Source(0x13, 130, bank, "6"));
        sink.Bool("诊断能力", "MediaSideInputSNRMeasurement", page[2], 5,
            CmisDecoderHelpers.Source(0x13, 130, bank, "5"));
        sink.Bool("诊断能力", "HostSideInputSNRMeasurement", page[2], 4,
            CmisDecoderHelpers.Source(0x13, 130, bank, "4"));
        sink.Bool("诊断能力", "BitsAndErrorsCountingSupported", page[2], 1,
            CmisDecoderHelpers.Source(0x13, 130, bank, "1"));

        sink.Hex("诊断能力", "PatternLocationCapabilities", page.AsSpan(3, 1),
            CmisDecoderHelpers.Source(0x13, 131, bank));
        sink.Hex("诊断能力", "PatternCapabilities", page.AsSpan(4, 11),
            CmisDecoderHelpers.Source(0x13, 132, bank) + "-142");

        var controlNames = new[]
        {
            "HostPatternGenerator", "MediaPatternGenerator", "HostPatternChecker", "MediaPatternChecker"
        };
        for (var group = 0; group < 4; group++)
        {
            for (var lane = 0; lane < 8; lane++)
            {
                var absoluteLane = bank * 8 + lane + 1;
                sink.Add("诊断控制", $"{controlNames[group]}Lane{absoluteLane}",
                    $"0x{page[16 + group * 8 + lane]:X2}",
                    CmisDecoderHelpers.Source(0x13, 144 + group * 8 + lane, bank),
                    "使能、交换/反相及 Pattern ID 字段", writable: true);
            }
        }

        sink.Enum("诊断控制", "HostPRBSGeneratorClockSource", page[48] >> 4,
            CmisCodeTables.PrbsGeneratorClockSource, CmisDecoderHelpers.Source(0x13, 176, bank, "7-4"),
            writable: true);
        sink.Enum("诊断控制", "MediaPRBSGeneratorClockSource", page[48] & 0x0F,
            CmisCodeTables.PrbsGeneratorClockSource, CmisDecoderHelpers.Source(0x13, 176, bank, "3-0"),
            writable: true);
        sink.Bool("诊断控制", "StartStopIsGlobal", page[49], 7,
            CmisDecoderHelpers.Source(0x13, 177, bank, "7"), writable: true);
        sink.Bool("诊断控制", "ResetErrorInformation", page[49], 5,
            CmisDecoderHelpers.Source(0x13, 177, bank, "5"), writable: true);
        sink.Bool("诊断控制", "AutoRestartGating", page[49], 4,
            CmisDecoderHelpers.Source(0x13, 177, bank, "4"), writable: true);
        sink.Enum("诊断控制", "MeasurementTime", (page[49] >> 1) & 7,
            CmisCodeTables.MeasurementTime, CmisDecoderHelpers.Source(0x13, 177, bank, "3-1"),
            writable: true);
        sink.Enum("诊断控制", "UpdatePeriod", page[49] & 1,
            value => value == 0 ? "1 s" : "5 s", CmisDecoderHelpers.Source(0x13, 177, bank, "0"),
            writable: true);
        sink.Hex("诊断控制", "CheckerClockSources", page.AsSpan(50, 1),
            CmisDecoderHelpers.Source(0x13, 178, bank), writable: true);

        var loopbackControls = new[]
        {
            "MediaSideOutputLoopbackEnable", "MediaSideInputLoopbackEnable",
            "HostSideOutputLoopbackEnable", "HostSideInputLoopbackEnable"
        };
        for (var index = 0; index < loopbackControls.Length; index++)
        {
            AddLaneBitmap(sink, "诊断控制", loopbackControls[index], page[52 + index], bank, 0x13,
                180 + index, true);
        }

        sink.Hex("诊断控制", "HostScratchPad", page.AsSpan(56, 8),
            CmisDecoderHelpers.Source(0x13, 184, bank) + "-191", writable: true);
        sink.Hex("诊断屏蔽", "DiagnosticsMasks", page.AsSpan(78, 18),
            CmisDecoderHelpers.Source(0x13, 206, bank) + "-223", writable: true);
        sink.Hex("诊断控制", "UserPattern", page.AsSpan(96, 32),
            CmisDecoderHelpers.Source(0x13, 224, bank) + "-255", writable: true);
    }

    private static void DecodePage14(MemoryRegionData region, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        var selector = page[0];
        sink.Enum("诊断结果", "DiagnosticsSelector", selector, CmisCodeTables.DiagnosticsSelector,
            CmisDecoderHelpers.Source(0x14, 128, bank), writable: true);
        sink.Bool("诊断标志", "LossOfReferenceClockFlag", page[4], 7,
            CmisDecoderHelpers.Source(0x14, 132, bank, "7"), "只读/读清除");

        var flagNames = new[]
        {
            "PatternCheckGatingCompleteFlagHost", "PatternCheckGatingCompleteFlagMedia",
            "PatternGeneratorLOLFlagHost", "PatternGeneratorLOLFlagMedia",
            "PatternCheckerLOLFlagHost", "PatternCheckerLOLFlagMedia"
        };
        for (var index = 0; index < flagNames.Length; index++)
        {
            AddLaneBitmap(sink, "诊断标志", flagNames[index], page[6 + index], bank, 0x14,
                134 + index, false, "只读/读清除");
        }

        DecodeDiagnosticsData(page, selector, bank, sink);
    }

    private static void DecodeDiagnosticsData(byte[] page, byte selector, byte bank, CmisFieldSink sink)
    {
        var dataOffset = 64;
        if (selector is 0x01 or 0x11)
        {
            for (var lane = 0; lane < 8; lane++)
            {
                var absoluteLane = bank * 8 + lane + 1;
                sink.Add("诊断结果", $"HostSideBERLane{absoluteLane}",
                    CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.F16(page, dataOffset + lane * 2)),
                    CmisDecoderHelpers.Source(0x14, 192 + lane * 2, bank));
                sink.Add("诊断结果", $"MediaSideBERLane{absoluteLane}",
                    CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.F16(page, dataOffset + 16 + lane * 2)),
                    CmisDecoderHelpers.Source(0x14, 208 + lane * 2, bank));
            }

            return;
        }

        if (selector == 0x06)
        {
            for (var lane = 0; lane < 8; lane++)
            {
                var absoluteLane = bank * 8 + lane + 1;
                sink.Add("诊断结果", $"HostSideSNRLane{absoluteLane}",
                    CmisDecoderHelpers.FormatNumber(ReadUInt16LittleEndian(page, dataOffset + lane * 2) / 256d, "dB"),
                    CmisDecoderHelpers.Source(0x14, 192 + lane * 2, bank));
                sink.Add("诊断结果", $"MediaSideSNRLane{absoluteLane}",
                    CmisDecoderHelpers.FormatNumber(ReadUInt16LittleEndian(page, dataOffset + 16 + lane * 2) / 256d, "dB"),
                    CmisDecoderHelpers.Source(0x14, 208 + lane * 2, bank));
            }

            return;
        }

        if (selector is >= 0x02 and <= 0x05 or >= 0x12 and <= 0x15)
        {
            var laneBase = selector is 0x03 or 0x05 or 0x13 or 0x15 ? 4 : 0;
            var side = selector is 0x04 or 0x05 or 0x14 or 0x15 ? "Media" : "Host";
            for (var lane = 0; lane < 4; lane++)
            {
                var absoluteLane = bank * 8 + laneBase + lane + 1;
                var errorCount = ReadUInt64LittleEndian(page, dataOffset + lane * 16);
                var bitsWithPsl = ReadUInt64LittleEndian(page, dataOffset + lane * 16 + 8);
                sink.Add("诊断结果", $"{side}SideErrorCountLane{absoluteLane}", errorCount,
                    CmisDecoderHelpers.Source(0x14, 192 + lane * 16, bank));
                sink.Add("诊断结果", $"{side}SideTotalBitsCountLane{absoluteLane}", bitsWithPsl & ~1UL,
                    CmisDecoderHelpers.Source(0x14, 200 + lane * 16, bank),
                    $"PatternSyncLoss={(bitsWithPsl & 1) != 0}");
            }
        }
    }

    private static void DecodePage15(MemoryRegionData region, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        for (var lane = 0; lane < 8; lane++)
        {
            var absoluteLane = bank * 8 + lane + 1;
            sink.Add("时延", $"DataPathRxLatencyLane{absoluteLane}",
                CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(page, 96 + lane * 2), "ns", 0),
                CmisDecoderHelpers.Source(0x15, 224 + lane * 2, bank));
            sink.Add("时延", $"DataPathTxLatencyLane{absoluteLane}",
                CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.U16(page, 112 + lane * 2), "ns", 0),
                CmisDecoderHelpers.Source(0x15, 240 + lane * 2, bank));
        }
    }

    private static void DecodePage16(MemoryRegionData region, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        DecodeNetworkPathConfiguration(page, bank, 0, 0, sink);
        DecodeNetworkPathConfiguration(page, bank, 1, 8, sink);
        AddLaneBitmap(sink, "网络通道控制", "NPDeinit", page[32], bank, 0x16, 160, true);
        sink.Hex("网络通道控制", "NetworkAndHostPathSourceSelectors", page.AsSpan(34, 2),
            CmisDecoderHelpers.Source(0x16, 162, bank) + "-163", writable: true);
        AddLaneBitmap(sink, "网络通道控制", "ApplyNPSCS0", page[48], bank, 0x16, 176, true,
            "写触发");
        AddLaneBitmap(sink, "网络通道控制", "ApplyNPSCS1", page[49], bank, 0x16, 177, true,
            "写触发");

        for (var lane = 0; lane < 8; lane++)
        {
            var absoluteLane = bank * 8 + lane + 1;
            var configStatus = lane % 2 == 0 ? page[50 + lane / 2] & 0x0F : page[50 + lane / 2] >> 4;
            sink.Enum("网络通道结果", $"Lane{absoluteLane}NPConfigStatus", configStatus,
                CmisCodeTables.NetworkConfigurationStatus,
                CmisDecoderHelpers.Source(0x16, 178 + lane / 2, bank));
            var active = page[64 + lane];
            sink.Add("网络通道状态", $"Lane{absoluteLane}ActiveNPConfig",
                $"NPID={(active >> 1) & 7}; InUse={active & 1}",
                CmisDecoderHelpers.Source(0x16, 192 + lane, bank));
            var state = lane % 2 == 0 ? page[72 + lane / 2] & 0x0F : page[72 + lane / 2] >> 4;
            sink.Enum("网络通道状态", $"Lane{absoluteLane}NPState", state,
                CmisCodeTables.NetworkPathState,
                CmisDecoderHelpers.Source(0x16, 200 + lane / 2, bank));
        }

        AddLaneBitmap(sink, "网络通道状态", "NPInitPending", page[76], bank, 0x16, 204);
        sink.Enum("网络通道时序", "MaxDurationNPInit", page[96] & 0x0F,
            CmisCodeTables.StateDuration, CmisDecoderHelpers.Source(0x16, 224, bank, "3-0"));
        sink.Enum("网络通道时序", "MaxDurationNPDeinit", page[96] >> 4,
            CmisCodeTables.StateDuration, CmisDecoderHelpers.Source(0x16, 224, bank, "7-4"));
        sink.Enum("网络通道时序", "MaxDurationNPTxTurnOn", page[97] & 0x0F,
            CmisCodeTables.StateDuration, CmisDecoderHelpers.Source(0x16, 225, bank, "3-0"));
        sink.Enum("网络通道时序", "MaxDurationNPTxTurnOff", page[97] >> 4,
            CmisCodeTables.StateDuration, CmisDecoderHelpers.Source(0x16, 225, bank, "7-4"));
        sink.Hex("网络通道能力", "NetworkPathOptions", page.AsSpan(98, 1),
            CmisDecoderHelpers.Source(0x16, 226, bank));
        sink.Hex("网络通道能力", "MixedMultiplexAdvertisement", page.AsSpan(100, 20),
            CmisDecoderHelpers.Source(0x16, 228, bank) + "-247");
        sink.Hex("网络通道能力", "NPApplicationAdvertisementExtension", page.AsSpan(120, 2),
            CmisDecoderHelpers.Source(0x16, 248, bank) + "-249");
    }

    private static void DecodePage17(MemoryRegionData region, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        AddLaneBitmap(sink, "网络通道标志", "NPStateChangedFlag", page[0], bank, 0x17, 128,
            false, "只读/读清除");
        AddLaneBitmap(sink, "网络通道屏蔽", "NPStateChangedMask", page[64], bank, 0x17, 192,
            true);
    }

    private static void DecodePage18(MemoryRegionData region, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        for (var lane = 0; lane < 8; lane++)
        {
            var absoluteLane = bank * 8 + lane + 1;
            sink.Add("NAD 控制集", $"SCS0Lane{absoluteLane}NADBlockIndex", page[lane] & 0x0F,
                CmisDecoderHelpers.Source(0x18, 128 + lane, bank), writable: true);
            sink.Add("NAD 控制集", $"SCS1Lane{absoluteLane}NADBlockIndex", page[8 + lane] & 0x0F,
                CmisDecoderHelpers.Source(0x18, 136 + lane, bank), writable: true);
        }

        sink.Hex("外部补充规范", "SCS0VersatileControlSetParameterSpace", page.AsSpan(16, 56),
            CmisDecoderHelpers.Source(0x18, 144, bank) + "-199",
            "CMIS-VCS 补充规范定义", true);
        sink.Hex("外部补充规范", "SCS1VersatileControlSetParameterSpace", page.AsSpan(72, 56),
            CmisDecoderHelpers.Source(0x18, 200, bank) + "-255",
            "CMIS-VCS 补充规范定义", true);
    }

    private static void DecodePage19(MemoryRegionData region, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        for (var lane = 0; lane < 8; lane++)
        {
            var absoluteLane = bank * 8 + lane + 1;
            DecodeDpConfig(page[lane], $"Lane{absoluteLane}DPConfigTx",
                CmisDecoderHelpers.Source(0x19, 128 + lane, bank), sink);
            DecodeDpConfig(page[8 + lane], $"Lane{absoluteLane}DPConfigRx",
                CmisDecoderHelpers.Source(0x19, 136 + lane, bank), sink);
            sink.Add("NAD 活动控制集", $"Lane{absoluteLane}NADBlockIndex", page[16 + lane] & 0x0F,
                CmisDecoderHelpers.Source(0x19, 144 + lane, bank));
        }

        sink.Hex("外部补充规范", "ActiveVersatileControlSetParameterSpace", page.AsSpan(24, 56),
            CmisDecoderHelpers.Source(0x19, 152, bank) + "-207",
            "CMIS-VCS 补充规范定义");
    }

    private static void DecodePage1C(MemoryRegionData region, ModuleDump dump, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        var mediaType = dump.FindRegion(CmisMemoryMap.LowerRegionId)?.Data.ElementAtOrDefault(85) ?? 0;
        for (var descriptor = 0; descriptor < 15; descriptor++)
        {
            var offset = descriptor * 8;
            var host = page[offset];
            if (host == 0xFF)
            {
                break;
            }

            var media = page[offset + 1];
            var laneCounts = page[offset + 2];
            var applicationNumber = bank * 15 + descriptor + 1;
            sink.Add("标准化应用描述符", $"ApplicationNumber{applicationNumber}",
                $"{CmisCodeTables.HostInterface(host)} ↔ {CmisCodeTables.MediaInterface(media, mediaType)}",
                CmisDecoderHelpers.Source(0x1C, 128 + offset, bank) + $"-{135 + offset}",
                $"Host lanes={laneCounts >> 4}; Media lanes={laneCounts & 0x0F}; "
                + $"Host starts=0x{page[offset + 3]:X2}; Media starts=0x{page[offset + 4]:X2}; "
                + $"NetworkPath={CmisDecoderHelpers.Bit(page[offset + 5], 7)}");
        }
    }

    private static void DecodePage1D(MemoryRegionData region, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        sink.Enum("Host Lane 切换", "MaxRedirectionCommitDuration", page[0] >> 4,
            CmisCodeTables.StateDuration, CmisDecoderHelpers.Source(0x1D, 128, bank, "7-4"));
        for (var lane = 0; lane < 8; lane++)
        {
            var absoluteLane = bank * 8 + lane + 1;
            sink.Add("Host Lane 切换", $"Lane{absoluteLane}ProvisionedRedirection",
                page[8 + lane], CmisDecoderHelpers.Source(0x1D, 136 + lane, bank), writable: true);
            sink.Add("Host Lane 切换", $"Lane{absoluteLane}CommitResult",
                CmisCodeTables.HostLaneRedirectionResult(page[40 + lane]),
                CmisDecoderHelpers.Source(0x1D, 168 + lane, bank));
            sink.Add("Host Lane 切换", $"Lane{absoluteLane}CommittedRedirection",
                page[56 + lane], CmisDecoderHelpers.Source(0x1D, 184 + lane, bank));
        }

        sink.Bool("Host Lane 切换", "EnableHostLaneRedirection", page[24], 0,
            CmisDecoderHelpers.Source(0x1D, 152, bank, "0"), writable: true);
        sink.Bool("Host Lane 切换", "CommitRedirection", page[32], 0,
            CmisDecoderHelpers.Source(0x1D, 160, bank, "0"), "只写/自清除", true);
    }

    private static void DecodeExternalSupplement(MemoryRegionData region, CmisFieldSink sink)
    {
        var definition = CmisMemoryMap.Definition(region.Page!.Value);
        sink.Add("外部补充规范", $"Page{region.Page:X2}h",
            "原始页已采集",
            CmisDecoderHelpers.Source(region.Page.Value, 128, region.Bank) + "-255",
            $"{definition?.Name}；具体字段不在 OIF-CMIS-05.3 基础文档中，需 {definition?.SupportAdvertisement}");
    }

    private static void DecodeNetworkPathConfiguration(
        byte[] page,
        byte bank,
        int set,
        int offset,
        CmisFieldSink sink)
    {
        for (var lane = 0; lane < 8; lane++)
        {
            var absoluteLane = bank * 8 + lane + 1;
            var value = page[offset + lane];
            sink.Add($"NP SCS{set}", $"Lane{absoluteLane}NPConfig",
                $"NPID={(value >> 1) & 7}; InUse={value & 1}",
                CmisDecoderHelpers.Source(0x16, 128 + offset + lane, bank), writable: true);
        }
    }

    private static void DecodeDpConfig(byte value, string name, string source, CmisFieldSink sink) =>
        sink.Add("方向独立活动控制集", name,
            $"AppSel={value >> 4}; DataPathID={(value >> 1) & 7}; ExplicitControl={value & 1}", source);

    private static void DecodeTuningFlags(
        byte value,
        int lane,
        byte bank,
        int offset,
        bool mask,
        CmisFieldSink sink)
    {
        var names = new[]
        {
            "TuningComplete", "WavelengthUnlocked", "InvalidChannelNumber",
            "TuningNotAccepted", "FineTuningOutOfRange", "TargetOutputPowerOutOfRange"
        };
        for (var bit = 0; bit < names.Length; bit++)
        {
            sink.Bool(mask ? "激光调谐屏蔽" : "激光调谐标志",
                $"Lane{lane}{names[bit]}{(mask ? "Mask" : "Flag")}",
                value,
                bit,
                CmisDecoderHelpers.Source(0x12, offset, bank, bit.ToString()),
                mask ? "" : "只读/读清除",
                mask);
        }
    }

    private static void AddLaneBitmap(
        CmisFieldSink sink,
        string category,
        string name,
        byte value,
        byte bank,
        byte page,
        int offset,
        bool writable = false,
        string description = "")
    {
        for (var lane = 0; lane < 8; lane++)
        {
            var absoluteLane = bank * 8 + lane + 1;
            sink.Bool(category, $"Lane{absoluteLane}{name}", value, lane,
                CmisDecoderHelpers.Source(page, offset, bank, lane.ToString()),
                description,
                writable);
        }
    }

    private static void DecodeTwoBitLanes(
        byte[] page,
        int index,
        byte bank,
        byte pageNumber,
        int offset,
        string name,
        CmisFieldSink sink,
        bool writable,
        int laneStart = 0)
    {
        for (var lane = 0; lane < 4; lane++)
        {
            var localLane = laneStart + lane;
            var absoluteLane = bank * 8 + localLane + 1;
            var value = (page[index] >> (lane * 2)) & 0x03;
            sink.Add("通道控制", $"Lane{absoluteLane}{name}", value,
                CmisDecoderHelpers.Source(pageNumber, offset, bank, $"{lane * 2 + 1}-{lane * 2}"),
                writable: writable);
        }
    }

    private static void DecodeNibbleLanes(
        byte[] page,
        int index,
        byte bank,
        byte pageNumber,
        int offset,
        string name,
        CmisFieldSink sink,
        bool writable)
    {
        for (var pair = 0; pair < 4; pair++)
        {
            for (var half = 0; half < 2; half++)
            {
                var lane = pair * 2 + half;
                var absoluteLane = bank * 8 + lane + 1;
                var value = half == 0 ? page[index + pair] & 0x0F : page[index + pair] >> 4;
                sink.Add("信号完整性控制", $"Lane{absoluteLane}{name}", value,
                    CmisDecoderHelpers.Source(pageNumber, offset + pair, bank, half == 0 ? "3-0" : "7-4"),
                    writable: writable);
            }
        }
    }

    private static ushort ReadUInt16LittleEndian(byte[] source, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset, 2));

    private static ulong ReadUInt64LittleEndian(byte[] source, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(source.AsSpan(offset, 8));
}
