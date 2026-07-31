using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Cmis;

/// <summary>
/// Byte-level access metadata for the CMIS 5.3/5.4 memory map.
/// This table is also used by the register panorama to prevent writes to
/// read-only, clear-on-read and reserved registers.
/// </summary>
public static class CmisRegisterMap
{
    public static (string Name, string Description, RegisterAccess Access) Describe(
        MemoryRegionData region,
        int offset,
        CmisRevision revision = default)
    {
        if (region.Id == CmisMemoryMap.LowerRegionId)
        {
            return DescribeLower(offset);
        }

        if (region.Page is not { } page)
        {
            return ("Unknown", "No CMIS page metadata is available.", RegisterAccess.Reserved);
        }

        return page switch
        {
            0x00 => Ro("Administrative Information", "CMIS 5.3 section 8.3"),
            0x01 => Ro("Advertising", "CMIS 5.3 section 8.4"),
            0x02 => Ro("Thresholds", "CMIS 5.3 section 8.5"),
            0x03 => Rw("User NV RAM", "Host non-volatile storage"),
            0x04 => Ro("Laser Capabilities", "Tunable laser capability advertising"),
            0x05 or 0x06 or 0x07 or >= 0x08 and <= 0x0B =>
                Mixed("External Supplement", "Semantics are defined by an external CMIS supplement; raw writes are blocked."),
            0x0C => DescribePage0C(offset),
            0x0D => DescribePage0D(offset),
            0x10 => DescribePage10(offset),
            0x11 => DescribePage11(offset),
            0x12 => DescribePage12(offset, revision),
            0x13 => DescribePage13(offset),
            0x14 => DescribePage14(offset),
            0x15 => offset >= 224 ? Ro("Timing Characteristics", "Per-lane latency results") : Reserved(),
            0x16 => DescribePage16(offset),
            0x17 => DescribePage17(offset),
            0x18 => offset <= 143
                ? Rw("NAD/VCS Configuration", "Normalized application selection or external VCS parameter selector")
                : Mixed("VCS Parameter Space", "Parameter semantics depend on the CMIS-VCS supplement."),
            0x19 => offset <= 207
                ? Ro("Configuration Extension Status", "Direction-specific data-path and NAD status")
                : Reserved(),
            0x1A or 0x1B => Mixed("Resource Module Page", "Defined by the external Resource Module specification."),
            0x1C => offset <= 247 ? Ro("Normalized Application Descriptor", "CMIS 5.3 section 8.20") : Reserved(),
            0x1D => DescribePage1D(offset),
            0x1E or 0x1F => Vendor(),
            >= 0x20 and <= 0x2B => Ro("VDM Data", "Observable descriptors, samples, or thresholds"),
            0x2C => offset <= 255
                ? ("VDM Flags", "Latched threshold crossing flags; read may clear.", RegisterAccess.ReadOnlyClearOnRead)
                : Reserved(),
            0x2D => Rw("VDM Masks", "Threshold crossing flag interrupt masks"),
            0x2E => Reserved(),
            0x2F => DescribePage2F(offset),
            >= 0x30 and <= 0x5F =>
                Mixed("External Supplement Page", "C-CMIS or CMIS-LT semantics require the corresponding supplement."),
            0x60 => DescribePage60(offset),
            0x61 => offset is >= 128 and <= 191
                ? Ro("Acquisition Counters", "CMIS 5.4 lane and data-path acquisition counters") : Reserved(),
            0x62 => offset is >= 128 and <= 191
                ? Ro("Lane Power Thresholds", "CMIS 5.4 absolute per-lane Tx output-power thresholds") : Reserved(),
            >= 0x63 and <= 0x6C => Reserved(),
            0x6D => DescribeLaneSwitching(offset, "Media"),
            >= 0x6E and <= 0x9E => Reserved(),
            0x9F => DescribePage9F(offset),
            >= 0xA0 and <= 0xAF =>
                Mixed("CDB Extended Payload", "Direction and layout depend on the active CDB command."),
            >= 0xB0 => Vendor(),
            _ => Reserved()
        };
    }

    private static (string, string, RegisterAccess) DescribeLower(int offset) => offset switch
    {
        0 => Ro("Identifier", "SFF-8024 module identifier"),
        1 => Ro("CMIS Revision", "Major and minor CMIS revision"),
        2 => Ro("Module Characteristics", "Memory model and management characteristics"),
        3 => Ro("Module State", "Module state and interrupt indication"),
        >= 4 and <= 11 => offset is 8 or 9
            ? ("Module Flags", "Latched flags; some bits clear on read.", RegisterAccess.ReadOnlyClearOnRead)
            : Ro("Module Flags Summary", "Per-page or per-bank flag summary"),
        12 or 13 => Reserved(),
        >= 14 and <= 25 => Ro("Module Monitors", "Temperature, supply and auxiliary monitors"),
        26 => ("Global Control", "Low-power, reset and management controls; contains self-clearing commands.", RegisterAccess.Mixed),
        >= 27 and <= 30 => Vendor(),
        >= 31 and <= 36 => Rw("Module Masks", "Module flag interrupt masks"),
        >= 37 and <= 63 => Ro("Module Status", "CDB, firmware, password and module status"),
        >= 64 and <= 84 => Vendor(),
        85 => Ro("Media Type", "Media interface technology family"),
        >= 86 and <= 117 => Ro("Application Descriptor", "Applications 1 through 8"),
        >= 118 and <= 121 => ("Password Change", "Write-only password change field.", RegisterAccess.WriteOnly),
        >= 122 and <= 125 => ("Password Entry", "Write-only password entry field.", RegisterAccess.WriteOnly),
        126 => Rw("Bank Select", "Selects the upper-memory bank"),
        127 => Rw("Page Select", "Selects the upper-memory page"),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) DescribePage10(int offset) => offset switch
    {
        128 => Rw("DPDeinit", "Per-lane data-path deinitialization"),
        >= 129 and <= 132 => Rw("Lane Direct Control", "Polarity, disable and squelch controls"),
        133 => Reserved(),
        134 => Rw("Adaptive EQ Freeze", "Freeze adaptive input equalization"),
        >= 135 and <= 136 => ("Adaptive EQ Store", "Write-one self-clearing store command.", RegisterAccess.WriteOnlySelfClearing),
        >= 137 and <= 139 => Rw("Rx Direct Control", "Receiver polarity, disable and squelch controls"),
        >= 140 and <= 142 => Reserved(),
        >= 143 and <= 144 => ("Apply Control Set", "Write-one self-clearing apply command.", RegisterAccess.WriteOnlySelfClearing),
        >= 145 and <= 212 => Rw("Staged Control Set", "Staged application and signal-integrity controls"),
        >= 213 and <= 232 => Rw("Lane Flag Mask", "Per-lane interrupt masks"),
        >= 233 and <= 239 => Reserved(),
        >= 240 and <= 255 => Vendor(),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) DescribePage11(int offset) => offset switch
    {
        >= 128 and <= 133 => Ro("Lane/Data Path Status", "Data-path states and output status"),
        >= 134 and <= 153 => ("Lane Flags", "Latched per-lane flags; read may clear.", RegisterAccess.ReadOnlyClearOnRead),
        >= 154 and <= 201 => Ro("Lane Monitors", "Optical power and bias monitors"),
        >= 202 and <= 239 => Ro("Configuration Status", "Configuration results, active controls and conditions"),
        >= 240 and <= 255 => Ro("Lane Mapping", "Media lane and wavelength mapping"),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) DescribePage12(int offset, CmisRevision revision) => offset switch
    {
        >= 128 and <= 167 => Rw("Tunable Laser Control", "Grid, channel, fine-tuning and target-power controls"),
        >= 168 and <= 199 => Ro("Tunable Laser Status", "Current grid, frequency and output power"),
        >= 200 and <= 215 => Rw("Tunable Laser Masks", "Per-lane tunable-laser flag masks"),
        >= 216 and <= 217 when revision.IsAtLeast54 =>
            Rw("Relative Tx Power Thresholds", "CMIS 5.4 programmable offsets against nominal Tx power"),
        >= 216 and <= 221 => Reserved(),
        >= 222 and <= 230 => Ro("Tunable Laser Status", "Per-lane status"),
        >= 231 and <= 238 => ("Tunable Laser Flags", "Latched tunable-laser flags; read may clear.", RegisterAccess.ReadOnlyClearOnRead),
        >= 239 and <= 246 => Rw("Tunable Laser Masks", "Latched flag masks"),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) DescribePage13(int offset) => offset switch
    {
        >= 128 and <= 142 => Ro("Diagnostics Capabilities", "Pattern, gating and loopback capabilities"),
        143 => Reserved(),
        >= 144 and <= 191 => Rw("Diagnostics Control", "Pattern generator/checker, gating and loopback controls"),
        >= 192 and <= 195 => Reserved(),
        >= 196 and <= 205 => Vendor(),
        >= 206 and <= 223 => Rw("Diagnostics Masks", "Diagnostics flag interrupt masks"),
        >= 224 and <= 255 => Rw("User Pattern", "Host and media user-defined test patterns"),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) DescribePage14(int offset) => offset switch
    {
        128 => Rw("Diagnostics Selector", "Selects the diagnostics data content"),
        >= 129 and <= 131 => Reserved(),
        >= 132 and <= 139 => ("Diagnostics Flags", "Latched diagnostics flags; read may clear.", RegisterAccess.ReadOnlyClearOnRead),
        >= 140 and <= 191 => Reserved(),
        >= 192 and <= 255 => Ro("Diagnostics Data", "Selector-dependent diagnostics result"),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) DescribePage16(int offset) => offset switch
    {
        >= 128 and <= 143 => Rw("Network Path Staged Configuration", "Staged network-path configuration"),
        >= 144 and <= 159 => Reserved(),
        160 => Rw("Network Path Control", "Network-path enable control"),
        161 => Reserved(),
        >= 162 and <= 163 => Rw("Network Path Control", "Network-path control parameters"),
        >= 164 and <= 175 => Reserved(),
        >= 176 and <= 177 => ("Network Path Apply", "Write-one self-clearing apply command.", RegisterAccess.WriteOnlySelfClearing),
        >= 178 and <= 255 => Ro("Network Path Status/Advertising", "Network-path status and capability data"),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) DescribePage17(int offset) => offset switch
    {
        128 => ("Network Path Flags", "Latched network-path flags; read may clear.", RegisterAccess.ReadOnlyClearOnRead),
        >= 129 and <= 191 => Reserved(),
        192 => Rw("Network Path Mask", "Network-path interrupt mask"),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) DescribePage1D(int offset) =>
        DescribeLaneSwitching(offset, "Host");

    private static (string, string, RegisterAccess) DescribeLaneSwitching(int offset, string side) => offset switch
    {
        >= 128 and <= 135 => Ro($"{side} Lane Switching Advertising", "Maximum commit duration and reserved advertising"),
        >= 136 and <= 143 => Rw("Lane Redirection", $"Proposed {side.ToLowerInvariant()}-lane permutation"),
        >= 144 and <= 151 => Reserved(),
        152 => Rw("Enable Lane Redirection", "Enables host-lane switching"),
        >= 153 and <= 159 => Reserved(),
        160 => ("Commit Redirection", "Write-one self-clearing lane-redirection commit.", RegisterAccess.WriteOnlySelfClearing),
        >= 161 and <= 167 => Reserved(),
        >= 168 and <= 175 => Ro("Redirection Commit Result", "Per-lane result of the last commit command"),
        >= 176 and <= 183 => Reserved(),
        >= 184 and <= 191 => Ro("Lane Redirection Status", "Active host-lane permutation"),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) DescribePage0C(int offset) => offset switch
    {
        >= 128 and <= 223 => Ro("Module Management Advertising", "CMIS 5.4 supported-page map and named-feature details"),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) DescribePage0D(int offset) => offset switch
    {
        128 => Ro("Firmware Management Capabilities", "CMIS 5.4 consolidated load-management capabilities"),
        >= 132 and <= 135 => Rw("Firmware Management Controls", "CMIS 5.4 consolidated load-management controls"),
        136 => Ro("Firmware Loads Status", "Running, committed and invalid state for firmware banks"),
        >= 148 and <= 255 => Ro("Firmware Version Descriptor", "Version descriptor for A, B or fixed load"),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) DescribePage60(int offset) => offset switch
    {
        >= 128 and <= 130 => Ro("Lane/DP Management Advertising", "CMIS 5.4 fixed polarity and counter-reset support"),
        >= 192 and <= 195 => ("Reset Acquisition Counters", "Write-one self-clearing reset bitmap.", RegisterAccess.WriteOnlySelfClearing),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) DescribePage2F(int offset) => offset switch
    {
        >= 128 and <= 143 => Ro("VDM Advertising", "VDM group count, timing and capability advertising"),
        144 => Rw("VDM Freeze Request", "Global freeze/unfreeze request"),
        145 => Ro("VDM Freeze Status", "Freeze and unfreeze completion status"),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) DescribePage9F(int offset) => offset switch
    {
        >= 128 and <= 133 => Rw("CDB Command Header", "Command ID, payload lengths and command check code"),
        >= 134 and <= 135 => Ro("CDB Reply Header", "Reply payload length and reply check code"),
        >= 136 and <= 255 => Mixed("CDB Local Payload", "Direction and layout depend on the active CDB command."),
        _ => Reserved()
    };

    private static (string, string, RegisterAccess) Ro(string name, string description) =>
        (name, description, RegisterAccess.ReadOnly);

    private static (string, string, RegisterAccess) Rw(string name, string description) =>
        (name, description, RegisterAccess.ReadWrite);

    private static (string, string, RegisterAccess) Mixed(string name, string description) =>
        (name, description, RegisterAccess.Mixed);

    private static (string, string, RegisterAccess) Reserved() =>
        ("Reserved", "Reserved by CMIS; the host must not write this byte.", RegisterAccess.Reserved);

    private static (string, string, RegisterAccess) Vendor() =>
        ("Custom", "Vendor-specific; write semantics require the vendor specification.", RegisterAccess.VendorSpecific);
}
