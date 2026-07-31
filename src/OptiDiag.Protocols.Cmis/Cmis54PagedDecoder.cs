using System.Text;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Cmis;

/// <summary>Semantic decoders for pages introduced by CMIS 5.4.</summary>
internal static class Cmis54PagedDecoder
{
    public static void Decode(MemoryRegionData region, CmisFieldSink sink)
    {
        switch (region.Page)
        {
            case 0x0C:
                DecodePage0C(region.Data, sink);
                break;
            case 0x0D:
                DecodePage0D(region.Data, sink);
                break;
            case 0x60:
                DecodePage60(region, sink);
                break;
            case 0x61:
                DecodePage61(region, sink);
                break;
            case 0x62:
                DecodePage62(region, sink);
                break;
            case 0x6D:
                DecodePage6D(region, sink);
                break;
        }
    }

    private static void DecodePage0C(byte[] page, CmisFieldSink sink)
    {
        var supported = new List<string>();
        for (var number = 0; number <= byte.MaxValue; number++)
        {
            if (CmisMemoryMap.IsPageSupported(page, (byte)number))
            {
                supported.Add($"{number:X2}h");
            }
        }

        sink.Add("5.4 页面能力", "MapOfSupportedPages", string.Join(", ", supported), "P0Ch.128-159",
            $"{supported.Count} 个受支持页面；CMIS 5.4 系统化位图");
        DecodeFeatureAdvertisement(page, 32, "ConsolidatedPerformanceManagement", sink);
        DecodeFeatureAdvertisement(page, 34, "ConsolidatedLoadManagement", sink);

        var na = page[64];
        AddBit(sink, "5.4 NA 行为", "NaSupported", na, 7, 192);
        AddBit(sink, "5.4 NA 行为", "NaFeedsSupervision", na, 6, 192);
        AddBit(sink, "5.4 NA 行为", "NaFeedsRangeStatistics", na, 5, 192);
        AddBit(sink, "5.4 NA 行为", "NaFeedsAverages", na, 4, 192);
        AddBit(sink, "5.4 NA 行为", "NaFeedsCounterStatistics", na, 3, 192);
        AddBit(sink, "5.4 NA 行为", "NaSaturatesTotals", na, 2, 192);
        AddBit(sink, "5.4 NA 行为", "NaFeedsDiagnostics", na, 1, 192);

        var firmware = page[66];
        AddBit(sink, "5.4 固件能力", "UniqueLoadVersionSupported", firmware, 7, 194);
        AddBit(sink, "5.4 固件能力", "DualBankSupported", firmware, 6, 194);
        AddBit(sink, "5.4 固件能力", "FirmwareLoadTagSupported", firmware, 5, 194);
        AddBit(sink, "5.4 固件能力", "AbnormalIndicationSupported", firmware, 4, 194);
        AddBit(sink, "5.4 固件能力", "TransferIsHarmless", firmware, 3, 194);
        AddBit(sink, "5.4 固件能力", "RejectUnsupportedActivation", firmware, 2, 194);
        AddBit(sink, "5.4 固件能力", "FixedFirmwareFallback", page[67], 7, 195);
    }

    private static void DecodeFeatureAdvertisement(byte[] page, int offset, string name, CmisFieldSink sink)
    {
        sink.Add("5.4 命名功能", $"{name}Revision", new CmisRevision(page[offset]).ToString(),
            $"P0Ch.{128 + offset}", page[offset] == 0 ? "不支持" : "功能定义所依据的 CMIS 版本");
        sink.Add("5.4 命名功能", $"{name}Profiles",
            $"Options={page[offset + 1] >> 4}; Requirements={page[offset + 1] & 0x0F}",
            $"P0Ch.{129 + offset}");
    }

    private static void DecodePage0D(byte[] page, CmisFieldSink sink)
    {
        var capability = page[0];
        AddBit(sink, "5.4 固件管理", "CdbDownloadSupported", capability, 7, 128, pageNumber: 0x0D);
        AddBit(sink, "5.4 固件管理", "FixedLoadProvidesService", capability, 3, 128, pageNumber: 0x0D);
        AddBit(sink, "5.4 固件管理", "FixedBankSupported", capability, 2, 128, pageNumber: 0x0D);
        AddBit(sink, "5.4 固件管理", "FirmwareBankBSupported", capability, 1, 128, pageNumber: 0x0D);
        AddBit(sink, "5.4 固件管理", "FirmwareBankASupported", capability, 0, 128, pageNumber: 0x0D);
        sink.Hex("5.4 固件管理", "FirmwareControls", page.AsSpan(4, 4), "P0Dh.132-135", writable: true);

        var status = page[8];
        AddBit(sink, "5.4 固件状态", "LoadBInvalid", status, 6, 136, pageNumber: 0x0D);
        AddBit(sink, "5.4 固件状态", "LoadBCommitted", status, 5, 136, pageNumber: 0x0D);
        AddBit(sink, "5.4 固件状态", "LoadBRunning", status, 4, 136, pageNumber: 0x0D);
        AddBit(sink, "5.4 固件状态", "LoadAInvalid", status, 2, 136, pageNumber: 0x0D);
        AddBit(sink, "5.4 固件状态", "LoadACommitted", status, 1, 136, pageNumber: 0x0D);
        AddBit(sink, "5.4 固件状态", "LoadARunning", status, 0, 136, pageNumber: 0x0D);
        AddVersion(page, 20, "LoadA", sink);
        AddVersion(page, 56, "LoadB", sink);
        AddVersion(page, 92, "FixedLoad", sink);
    }

    private static void AddVersion(byte[] page, int offset, string name, CmisFieldSink sink)
    {
        var extra = Encoding.ASCII.GetString(page, offset + 4, 32).Trim(' ', '\0', '\xFF');
        sink.Add("5.4 固件版本", name,
            $"{page[offset]}.{page[offset + 1]}.{CmisDecoderHelpers.U16(page, offset + 2)}",
            $"P0Dh.{128 + offset}-{131 + offset}", extra);
    }

    private static void DecodePage60(MemoryRegionData region, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        AddLaneBitmap(sink, "5.4 极性状态", "InputPolarityInvertedTx", page[0], bank, 0x60, 128);
        AddLaneBitmap(sink, "5.4 极性状态", "OutputPolarityInvertedRx", page[1], bank, 0x60, 129);
        AddBit(sink, "5.4 计数器能力", "RxLaneAcquisitionCounterSupported", page[2], 7, 130, bank);
        AddBit(sink, "5.4 计数器能力", "TxLaneAcquisitionCounterSupported", page[2], 6, 130, bank);
        AddBit(sink, "5.4 计数器能力", "RxDpAcquisitionCounterSupported", page[2], 5, 130, bank);
        AddBit(sink, "5.4 计数器能力", "TxDpAcquisitionCounterSupported", page[2], 4, 130, bank);
        AddLaneBitmap(sink, "5.4 计数器复位", "ResetRxLaneAcquisitionCounter", page[64], bank, 0x60, 192, true,
            "只写/自清除");
        AddLaneBitmap(sink, "5.4 计数器复位", "ResetTxLaneAcquisitionCounter", page[65], bank, 0x60, 193, true,
            "只写/自清除");
        AddLaneBitmap(sink, "5.4 计数器复位", "ResetRxDpAcquisitionCounter", page[66], bank, 0x60, 194, true,
            "只写/自清除");
        AddLaneBitmap(sink, "5.4 计数器复位", "ResetTxDpAcquisitionCounter", page[67], bank, 0x60, 195, true,
            "只写/自清除");
    }

    private static void DecodePage61(MemoryRegionData region, CmisFieldSink sink)
    {
        var names = new[] { "TxLane", "RxLane", "TxDataPath", "RxDataPath" };
        for (var group = 0; group < names.Length; group++)
        {
            for (var lane = 0; lane < 8; lane++)
            {
                var bank = region.Bank ?? 0;
                var absoluteLane = bank * 8 + lane + 1;
                var offset = group * 16 + lane * 2;
                sink.Add("5.4 获取计数器", $"{names[group]}AcquisitionCounter{absoluteLane}",
                    CmisDecoderHelpers.U16(region.Data, offset),
                    CmisDecoderHelpers.Source(0x61, 128 + offset, bank), "饱和 U16 计数器");
            }
        }
    }

    private static void DecodePage62(MemoryRegionData region, CmisFieldSink sink)
    {
        var labels = new[] { "HighAlarm", "LowAlarm", "HighWarning", "LowWarning" };
        var bank = region.Bank ?? 0;
        for (var lane = 0; lane < 8; lane++)
        {
            for (var threshold = 0; threshold < 4; threshold++)
            {
                var offset = lane * 8 + threshold * 2;
                sink.Add("5.4 相对输出功率阈值",
                    $"Lane{bank * 8 + lane + 1}TxPower{labels[threshold]}",
                    CmisDecoderHelpers.FormatNumber(CmisDecoderHelpers.S16(region.Data, offset) * 0.01, "dBm"),
                    CmisDecoderHelpers.Source(0x62, 128 + offset, bank));
            }
        }
    }

    private static void DecodePage6D(MemoryRegionData region, CmisFieldSink sink)
    {
        var page = region.Data;
        var bank = region.Bank ?? 0;
        sink.Enum("5.4 Media Lane 切换", "MaxRedirectionCommitDuration", page[0] >> 4,
            CmisCodeTables.StateDuration, CmisDecoderHelpers.Source(0x6D, 128, bank, "7-4"));
        for (var lane = 0; lane < 8; lane++)
        {
            var number = bank * 8 + lane + 1;
            sink.Add("5.4 Media Lane 切换", $"Lane{number}ProvisionedRedirection", page[8 + lane],
                CmisDecoderHelpers.Source(0x6D, 136 + lane, bank), writable: true);
            sink.Add("5.4 Media Lane 切换", $"Lane{number}CommitResult", RedirectionResult(page[40 + lane]),
                CmisDecoderHelpers.Source(0x6D, 168 + lane, bank));
            sink.Add("5.4 Media Lane 切换", $"Lane{number}CommittedRedirection", page[56 + lane],
                CmisDecoderHelpers.Source(0x6D, 184 + lane, bank));
        }

        sink.Bool("5.4 Media Lane 切换", "EnableMediaLaneRedirection", page[24], 0,
            CmisDecoderHelpers.Source(0x6D, 152, bank, "0"), writable: true);
        sink.Bool("5.4 Media Lane 切换", "CommitMediaLaneRedirection", page[32], 0,
            CmisDecoderHelpers.Source(0x6D, 160, bank, "0"), "只写/自清除", true);
    }

    private static string RedirectionResult(byte value) => value switch
    {
        0 => "No result",
        1 => "Success",
        2 => "In progress",
        3 => "Rejected",
        4 => "Invalid configuration",
        5 => "Inconsistent with active data path",
        6 => "Ordering unsupported",
        _ => $"Reserved 0x{value:X2}"
    };

    private static void AddBit(
        CmisFieldSink sink, string category, string name, byte value, int bit, int absoluteOffset,
        byte? bank = null, byte pageNumber = 0x0C) =>
        sink.Bool(category, name, value, bit, CmisDecoderHelpers.Source(bank.HasValue ? (byte)0x60 : pageNumber,
            absoluteOffset, bank, bit.ToString()));

    private static void AddLaneBitmap(
        CmisFieldSink sink, string category, string name, byte value, byte bank, byte page, int offset,
        bool writable = false, string description = "")
    {
        for (var lane = 0; lane < 8; lane++)
        {
            sink.Bool(category, $"Lane{bank * 8 + lane + 1}{name}", value, lane,
                CmisDecoderHelpers.Source(page, offset, bank, lane.ToString()), description, writable);
        }
    }
}
