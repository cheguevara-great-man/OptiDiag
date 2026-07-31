using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Cmis;

public enum CmisPageCoverage
{
    BaseSpecification,
    ExternalSupplement,
    Reserved,
    VendorSpecific
}

public sealed record CmisPageDefinition(
    byte FirstPage,
    byte LastPage,
    string Name,
    string Section,
    bool IsLaneBanked,
    RegisterAccess Access,
    CmisPageCoverage Coverage,
    string SupportAdvertisement);

/// <summary>
/// CMIS 5.3/5.4 Table 8-1 expressed as executable metadata. The capture rules deliberately
/// avoid reserved and vendor pages and only read optional pages when the module advertises
/// them.  Pages whose register semantics live in a separate OIF supplement are retained as
/// raw data and explicitly marked as external rather than guessed.
/// </summary>
public static class CmisMemoryMap
{
    public const string LowerRegionId = "cmis-lower";
    public const string Page00RegionId = "cmis-page00";
    public const string Page01RegionId = "cmis-page01";
    public const string Page02RegionId = "cmis-page02";

    public static IReadOnlyList<CmisPageDefinition> Pages { get; } =
    [
        new(0x00, 0x00, "Administrative Information", "8.3", false, RegisterAccess.ReadOnly,
            CmisPageCoverage.BaseSpecification, "Always present"),
        new(0x01, 0x01, "Advertising", "8.4", false, RegisterAccess.ReadOnly,
            CmisPageCoverage.BaseSpecification, "Mandatory for paged memory"),
        new(0x02, 0x02, "Thresholds Information", "8.5", false, RegisterAccess.ReadOnly,
            CmisPageCoverage.BaseSpecification, "Mandatory for paged memory"),
        new(0x03, 0x03, "User NV RAM", "8.6", false, RegisterAccess.ReadWrite,
            CmisPageCoverage.BaseSpecification, "01h:142.2"),
        new(0x04, 0x04, "Laser Capabilities Advertising", "8.7", false, RegisterAccess.ReadOnly,
            CmisPageCoverage.BaseSpecification, "01h:155.6"),
        new(0x05, 0x05, "Form Factor Specific Management", "8.8", false, RegisterAccess.Mixed,
            CmisPageCoverage.ExternalSupplement, "01h:142.3 / CMIS-FF"),
        new(0x06, 0x07, "Resource Module Pages", "8.1.2", false, RegisterAccess.Mixed,
            CmisPageCoverage.ExternalSupplement, "00h:57 / Resource Module specification"),
        new(0x08, 0x0B, "Link Training Supplement Pages", "8.1.2", false, RegisterAccess.Mixed,
            CmisPageCoverage.ExternalSupplement, "CMIS-LT supplement"),
        new(0x0C, 0x0C, "Module Management", "8.11 (5.4)", false, RegisterAccess.Mixed,
            CmisPageCoverage.BaseSpecification, "01h:173.7"),
        new(0x0D, 0x0D, "Firmware Management", "8.12 (5.4)", false, RegisterAccess.Mixed,
            CmisPageCoverage.BaseSpecification, "01h:173.6"),
        new(0x0E, 0x0F, "Reserved", "8.1.2", false, RegisterAccess.Reserved,
            CmisPageCoverage.Reserved, "Never access"),
        new(0x10, 0x10, "Lane and Data Path Configuration", "8.9", true, RegisterAccess.Mixed,
            CmisPageCoverage.BaseSpecification, "Mandatory for paged memory"),
        new(0x11, 0x11, "Lane and Data Path Status", "8.10", true, RegisterAccess.ReadOnly,
            CmisPageCoverage.BaseSpecification, "Mandatory for paged memory"),
        new(0x12, 0x12, "Tunable Laser Control and Status", "8.11", true, RegisterAccess.Mixed,
            CmisPageCoverage.BaseSpecification, "01h:155.6"),
        new(0x13, 0x13, "Module Performance Diagnostics Control", "8.12", true, RegisterAccess.Mixed,
            CmisPageCoverage.BaseSpecification, "01h:142.5"),
        new(0x14, 0x14, "Module Performance Diagnostics Results", "8.13", true,
            RegisterAccess.ReadOnlyClearOnRead, CmisPageCoverage.BaseSpecification, "01h:142.5"),
        new(0x15, 0x15, "Timing Characteristics", "8.14", true, RegisterAccess.ReadOnly,
            CmisPageCoverage.BaseSpecification, "01h:145.3"),
        new(0x16, 0x16, "Network Path Control and Status", "8.15", true, RegisterAccess.Mixed,
            CmisPageCoverage.BaseSpecification, "01h:142.7"),
        new(0x17, 0x17, "Network Path Flags and Masks", "8.16", true, RegisterAccess.Mixed,
            CmisPageCoverage.BaseSpecification, "01h:142.7"),
        new(0x18, 0x18, "Lane and Data Path Configuration Extensions", "8.17", true,
            RegisterAccess.ReadWrite, CmisPageCoverage.BaseSpecification, "01h:175.3-0 or 01h:162.7"),
        new(0x19, 0x19, "Lane and Data Path Status Extensions", "8.18", true, RegisterAccess.ReadOnly,
            CmisPageCoverage.BaseSpecification, "01h:162.7-6"),
        new(0x1A, 0x1B, "Resource Module Banked Pages", "8.19", true, RegisterAccess.Mixed,
            CmisPageCoverage.ExternalSupplement, "00h:57 / Resource Module specification"),
        new(0x1C, 0x1C, "Normalized Application Descriptors", "8.20", false, RegisterAccess.ReadOnly,
            CmisPageCoverage.BaseSpecification, "01h:175.3-0"),
        new(0x1D, 0x1D, "Host Lane Switching", "8.21", true, RegisterAccess.Mixed,
            CmisPageCoverage.BaseSpecification, "01h:252.7"),
        new(0x1E, 0x1F, "Custom Lane-Banked Pages", "8.1.2", true, RegisterAccess.VendorSpecific,
            CmisPageCoverage.VendorSpecific, "Vendor contract"),
        new(0x20, 0x23, "VDM Observable Descriptors", "8.22.1", true, RegisterAccess.ReadOnly,
            CmisPageCoverage.BaseSpecification, "01h:142.6 and 2Fh:128.1-0"),
        new(0x24, 0x27, "VDM Samples", "8.22.2", true, RegisterAccess.ReadOnly,
            CmisPageCoverage.BaseSpecification, "01h:142.6 and 2Fh:128.1-0"),
        new(0x28, 0x2B, "VDM Alarm/Warning Thresholds", "8.22.3", true, RegisterAccess.ReadOnly,
            CmisPageCoverage.BaseSpecification, "01h:142.6 and 2Fh:128.1-0"),
        new(0x2C, 0x2C, "VDM Threshold Crossing Flags", "8.22.4", true,
            RegisterAccess.ReadOnlyClearOnRead, CmisPageCoverage.BaseSpecification, "01h:142.6"),
        new(0x2D, 0x2D, "VDM Masks", "8.22.5", true, RegisterAccess.ReadWrite,
            CmisPageCoverage.BaseSpecification, "01h:142.6"),
        new(0x2E, 0x2E, "Reserved", "8.1.2", true, RegisterAccess.Reserved,
            CmisPageCoverage.Reserved, "Never access"),
        new(0x2F, 0x2F, "VDM Advertisement and Dynamic Controls", "8.22.6", true,
            RegisterAccess.Mixed, CmisPageCoverage.BaseSpecification, "01h:142.6"),
        new(0x30, 0x4F, "Coherent Module Management", "8.1.2", false, RegisterAccess.Mixed,
            CmisPageCoverage.ExternalSupplement, "01h:142.4 / C-CMIS"),
        new(0x50, 0x5F, "Link Training", "8.1.2", false, RegisterAccess.Mixed,
            CmisPageCoverage.ExternalSupplement, "01h:252.6 / CMIS-LT"),
        new(0x60, 0x60, "Lane and Datapath Management Extensions", "8.30 (5.4)", true,
            RegisterAccess.Mixed, CmisPageCoverage.BaseSpecification, "01h:174.7"),
        new(0x61, 0x61, "Lane and Datapath Monitoring", "8.31 (5.4)", true,
            RegisterAccess.ReadOnly, CmisPageCoverage.BaseSpecification, "01h:174.6"),
        new(0x62, 0x62, "Lane Supervision Thresholds", "8.32 (5.4)", true,
            RegisterAccess.ReadOnly, CmisPageCoverage.BaseSpecification, "01h:174.5"),
        new(0x63, 0x6C, "Reserved", "8.1.2", false, RegisterAccess.Reserved,
            CmisPageCoverage.Reserved, "Never access"),
        new(0x6D, 0x6D, "Media Lane Switching", "8.33 (5.4)", true,
            RegisterAccess.Mixed, CmisPageCoverage.BaseSpecification, "01h:252.5"),
        new(0x6E, 0x9E, "Reserved", "8.1.2", false, RegisterAccess.Reserved,
            CmisPageCoverage.Reserved, "Never access"),
        new(0x9F, 0x9F, "CDB Message", "8.23", false, RegisterAccess.Mixed,
            CmisPageCoverage.BaseSpecification, "01h:163.7-6"),
        new(0xA0, 0xAF, "CDB Extended Payload", "8.24", false, RegisterAccess.ReadWrite,
            CmisPageCoverage.BaseSpecification, "01h:163.7-6 and 01h:163.3-0"),
        new(0xB0, 0xFF, "Custom Pages", "8.1.2", false, RegisterAccess.VendorSpecific,
            CmisPageCoverage.VendorSpecific, "Vendor contract")
    ];

    public static IReadOnlyList<MemoryCaptureRegion> BuildCapturePlan()
    {
        var result = new List<MemoryCaptureRegion>
        {
            new(LowerRegionId, "CMIS Lower Memory", 0x50, 0, 128, Volatile: true),
            Page(Page00RegionId, "CMIS Upper Page 00h (paged)", 0x00),
            new(Page00RegionId, "CMIS Upper Page 00h (flat)", 0x50, 128, 128),
            Page(Page01RegionId, "CMIS Page 01h Advertising", 0x01),
            Page(Page02RegionId, "CMIS Page 02h Thresholds", 0x02)
        };

        foreach (var page in new byte[] { 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D })
        {
            result.Add(Page(RegionId(page), DisplayName(page), page, optional: true));
        }

        for (byte bank = 0; bank < 32; bank++)
        {
            foreach (var page in new byte[]
                     {
                         0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1D,
                         0x60, 0x61, 0x62, 0x6D
                     })
            {
                result.Add(Page(
                    RegionId(page, bank),
                    $"CMIS Bank {bank} Page {page:X2}h {DisplayName(page)}",
                    page,
                    bank,
                    optional: page is not 0x10 and not 0x11 || bank > 0,
                    isVolatile: IsVolatile(page)));
            }
        }

        // Page 1Ch uses its own advertised bank count. 5.3 defined 4 bits (15
        // banks); 5.4 widened the same byte to U8 (255 banks).
        for (var bankIndex = 0; bankIndex < 255; bankIndex++)
        {
            var bank = (byte)bankIndex;
            result.Add(Page(
                RegionId(0x1C, bank),
                $"CMIS NAD Block {bank} Page 1Ch",
                0x1C,
                bank,
                optional: true));
        }

        // Page 2Fh must be captured first because it advertises how many VDM groups exist.
        for (byte bank = 0; bank < 32; bank++)
        {
            result.Add(Page(
                RegionId(0x2F, bank),
                $"CMIS Bank {bank} Page 2Fh VDM Advertising",
                0x2F,
                bank,
                optional: true,
                isVolatile: true));
            foreach (var page in Enumerable.Range(0x20, 14).Select(x => (byte)x).Where(x => x != 0x2E))
            {
                result.Add(Page(
                    RegionId(page, bank),
                    $"CMIS Bank {bank} Page {page:X2}h {DisplayName(page)}",
                    page,
                    bank,
                    optional: true,
                    isVolatile: IsVolatile(page)));
            }
        }

        // The base CMIS document only advertises these supplement-owned page groups.
        // Reading Bank 0 gives the raw supplement payload without inventing its semantics.
        foreach (var page in Enumerable.Range(0x30, 0x20).Select(x => (byte)x))
        {
            result.Add(Page(RegionId(page), $"CMIS C-CMIS raw Page {page:X2}h", page, optional: true));
        }

        foreach (var page in Enumerable.Range(0x50, 0x10).Select(x => (byte)x))
        {
            result.Add(Page(RegionId(page), $"CMIS-LT raw Page {page:X2}h", page, optional: true));
        }

        for (byte instance = 0; instance < 2; instance++)
        {
            result.Add(Page(
                RegionId(0x9F, instance),
                $"CMIS CDB Instance {instance + 1} Message Page 9Fh",
                0x9F,
                instance,
                optional: true,
                isVolatile: true));
            foreach (var page in Enumerable.Range(0xA0, 16).Select(x => (byte)x))
            {
                result.Add(Page(
                    RegionId(page, instance),
                    $"CMIS CDB Instance {instance + 1} EPL Page {page:X2}h",
                    page,
                    instance,
                    optional: true,
                    isVolatile: true));
            }
        }

        return result;
    }

    public static bool ShouldCapture(
        MemoryCaptureRegion region,
        IReadOnlyList<MemoryRegionData> capturedRegions)
    {
        if (region.Id == LowerRegionId)
        {
            return true;
        }

        var lower = capturedRegions.FirstOrDefault(x => x.Id == LowerRegionId)?.Data;
        if (lower is not { Length: >= 128 })
        {
            return false;
        }

        var isFlat = (lower[2] & 0x80) != 0;
        if (region.Id == Page00RegionId)
        {
            return isFlat != region.Page.HasValue;
        }

        if (isFlat || region.Page is not { } page)
        {
            return false;
        }

        var page01 = capturedRegions.FirstOrDefault(x => x.Id == Page01RegionId)?.Data;
        if (page is 0x01 or 0x02)
        {
            return true;
        }

        if (page01 is not { Length: 128 })
        {
            return false;
        }

        var revision = CmisRevision.FromLowerMemory(lower);
        var laneBankCount = DecodeLaneBankCount(page01, revision);
        var bank = region.Bank ?? 0;

        // CMIS 5.4 Page 0Ch is a systematic page-support bitmap. Once it has
        // been captured it is the preferred authority for optional pages.
        var page0C = capturedRegions.FirstOrDefault(x => x.Page == 0x0C)?.Data;
        if (revision.IsAtLeast54 && page0C is { Length: 128 } && !IsPageSupported(page0C, page))
        {
            return false;
        }

        return page switch
        {
            0x03 => IsSet(page01[14], 2),
            0x04 => IsSet(page01[27], 6),
            0x05 => IsSet(page01[14], 3),
            0x06 or 0x07 => lower[57] == 1,
            >= 0x08 and <= 0x0B => IsSet(page01[124], 6),
            0x0C => revision.IsAtLeast54 && IsSet(page01[45], 7),
            0x0D => revision.IsAtLeast54 && IsSet(page01[45], 6),
            0x10 or 0x11 => bank < laneBankCount,
            0x12 => bank < laneBankCount && IsSet(page01[27], 6),
            0x13 or 0x14 => bank < laneBankCount && IsSet(page01[14], 5),
            0x15 => bank < laneBankCount && IsSet(page01[17], 3),
            0x16 or 0x17 => bank < laneBankCount && IsSet(page01[14], 7),
            0x18 => bank < laneBankCount && ((page01[47] & 0x0F) != 0 || IsSet(page01[34], 7)),
            0x19 => bank < laneBankCount && (page01[34] & 0xC0) != 0,
            0x1A or 0x1B => bank < laneBankCount && lower[57] == 1,
            0x1C => bank < DecodeNadBankCount(page01[47], revision),
            0x1D => bank < laneBankCount && IsSet(page01[124], 7),
            >= 0x20 and <= 0x2F => ShouldCaptureVdm(page, bank, laneBankCount, page01, capturedRegions),
            >= 0x30 and <= 0x4F => bank == 0 && IsSet(page01[14], 4),
            >= 0x50 and <= 0x5F => bank == 0 && IsSet(page01[124], 6),
            0x60 => revision.IsAtLeast54 && bank < laneBankCount && IsSet(page01[46], 7),
            0x61 => revision.IsAtLeast54 && bank < laneBankCount && IsSet(page01[46], 6),
            0x62 => revision.IsAtLeast54 && bank < laneBankCount && IsSet(page01[46], 5),
            0x6D => revision.IsAtLeast54 && bank < laneBankCount && IsSet(page01[124], 5),
            0x9F => bank < DecodeCdbInstanceCount(page01[35]),
            >= 0xA0 and <= 0xAF =>
                bank < DecodeCdbInstanceCount(page01[35])
                && page - 0xA0 < DecodeCdbEplPageCount(page01[35]),
            _ => false
        };
    }

    public static string RegionId(byte page, byte? bank = null) =>
        bank.HasValue ? $"cmis-b{bank.Value}-page{page:X2}" : $"cmis-page{page:X2}";

    public static CmisPageDefinition? Definition(byte page) =>
        Pages.FirstOrDefault(x => page >= x.FirstPage && page <= x.LastPage);

    public static int DecodeLaneBankCount(byte supportedPages) => (supportedPages & 0x03) switch
    {
        0 => 1,
        1 => 2,
        2 => 4,
        _ => 1
    };

    public static int DecodeLaneBankCount(ReadOnlySpan<byte> page01, CmisRevision revision)
    {
        if (page01.Length < 47)
        {
            return 1;
        }

        var legacy = DecodeLaneBankCount(page01[14]);
        return revision.IsAtLeast54 && (page01[14] & 0x03) == 0x03
            ? (page01[46] & 0x1F) + 1
            : legacy;
    }

    public static int DecodeNadBankCount(byte value, CmisRevision revision) =>
        revision.IsAtLeast54 ? value : value & 0x0F;

    public static bool IsPageSupported(ReadOnlySpan<byte> page0C, byte page) =>
        page0C.Length >= 32 && (page0C[page / 8] & (1 << (page % 8))) != 0;

    public static int DecodeCdbInstanceCount(byte value) => (value >> 6) switch
    {
        1 => 1,
        2 => 2,
        _ => 0
    };

    public static int DecodeCdbEplPageCount(byte value) => (value & 0x0F) switch
    {
        0 => 0,
        1 => 1,
        2 => 2,
        3 => 3,
        4 => 4,
        5 => 8,
        6 => 12,
        7 => 16,
        _ => 0
    };

    private static bool ShouldCaptureVdm(
        byte page,
        byte bank,
        int laneBankCount,
        byte[] page01,
        IReadOnlyList<MemoryRegionData> capturedRegions)
    {
        if (!IsSet(page01[14], 6) || bank >= laneBankCount || page == 0x2E)
        {
            return false;
        }

        if (page is 0x2C or 0x2D or 0x2F)
        {
            return true;
        }

        var advertisement = capturedRegions.FirstOrDefault(x =>
            x.Page == 0x2F && x.Bank == bank)?.Data;
        if (advertisement is not { Length: 128 })
        {
            return false;
        }

        var groups = (advertisement[0] & 0x03) + 1;
        var group = page switch
        {
            >= 0x20 and <= 0x23 => page - 0x20 + 1,
            >= 0x24 and <= 0x27 => page - 0x24 + 1,
            >= 0x28 and <= 0x2B => page - 0x28 + 1,
            _ => 5
        };
        return group <= groups;
    }

    private static MemoryCaptureRegion Page(
        string id,
        string name,
        byte page,
        byte bank = 0,
        bool optional = false,
        bool isVolatile = false) =>
        new(
            id,
            name,
            0x50,
            128,
            128,
            page,
            bank,
            PageSelectOffset: 127,
            BankSelectOffset: 126,
            Optional: optional,
            Volatile: isVolatile);

    private static string DisplayName(byte page) => Definition(page)?.Name ?? $"Page {page:X2}h";

    private static bool IsVolatile(byte page) =>
        page is 0x0D
            or >= 0x10 and <= 0x19
            or 0x1D
            or >= 0x24 and <= 0x2F
            or 0x60 or 0x61 or 0x6D
            or >= 0x9F and <= 0xAF;

    private static bool IsSet(byte value, int bit) => (value & (1 << bit)) != 0;
}
