using System.Buffers.Binary;
using System.Text;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Cmis;

public sealed class CmisProtocol : IOpticalModuleProtocol, ICapturedRevisionProvider
{
    public const string LowerRegionId = CmisMemoryMap.LowerRegionId;
    public const string Page00RegionId = CmisMemoryMap.Page00RegionId;
    public const string Page01RegionId = CmisMemoryMap.Page01RegionId;
    public const string Page02RegionId = CmisMemoryMap.Page02RegionId;
    public const string Page03RegionId = "cmis-page03";

    private static readonly HashSet<byte> CmisIdentifiers =
        [0x18, 0x19, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26];

    public string Id => "cmis";

    public string DisplayName => "CMIS";

    public string Revision => "5.3/5.4";

    public IReadOnlyList<MemoryCaptureRegion> CapturePlan { get; } = CmisMemoryMap.BuildCapturePlan();

    public bool ShouldCaptureRegion(
        MemoryCaptureRegion region,
        IReadOnlyList<MemoryRegionData> capturedRegions)
        => CmisMemoryMap.ShouldCapture(region, capturedRegions);

    public bool CanDecode(ModuleDump dump)
    {
        var lower = dump.FindRegion(LowerRegionId)?.Data;
        return lower is { Length: >= 64 } && CmisIdentifiers.Contains(lower[0]);
    }

    public string ResolveRevision(IReadOnlyList<MemoryRegionData> capturedRegions)
    {
        var lower = capturedRegions.FirstOrDefault(region => region.Id == LowerRegionId)?.Data;
        return lower is { Length: > 1 } ? new CmisRevision(lower[1]).ToString() : Revision;
    }

    public DecodedModule Decode(ModuleDump dump)
    {
        var lower = RequireRegion(dump, LowerRegionId, 128);
        var page00 = RequireRegion(dump, Page00RegionId, 128);
        var page01 = dump.FindRegion(Page01RegionId)?.Data;
        var page02 = dump.FindRegion(Page02RegionId)?.Data;
        var diagnostics = DecodeDiagnostics(dump, lower, page00, page01, page02);
        var information = DecodeInformation(lower, page00, page01);
        var thresholds = DecodeThresholds(page01, page02);
        var measurements = DecodeMeasurements(dump, lower, page01, thresholds);
        var alarms = DecodeAlarms(dump, lower);
        var status = DecodeStatus(dump, lower);
        var registers = DecodeRegisters(dump);
        var fields = DecodeFields(dump, lower, page00, page01);
        var revision = FormatRevision(lower[1]);

        return new DecodedModule(
            information,
            measurements,
            thresholds,
            alarms,
            status,
            registers,
            diagnostics,
            new ProtocolIdentification(
                Id,
                "CMIS",
                revision,
                $"Identifier=0x{lower[0]:X2}，CMISRevision=0x{lower[1]:X2}（{revision}）。",
                []),
            fields);
    }

    private static ModuleInformation DecodeInformation(byte[] lower, byte[] page00, byte[]? page01)
    {
        var date = ReadAscii(page00, 54, 8);
        var wavelength = page01 is { Length: 128 } && IsOptical(lower[85])
            ? ReadUInt16(page01, 10) * 0.05
            : (double?)null;
        var technology = CmisCodeTables.MediaTechnology(page00[84]);

        return new ModuleInformation(
            CmisCodeTables.Identifier(lower[0]),
            CmisCodeTables.Connector(page00[75]),
            ReadAscii(page00, 1, 16),
            $"{page00[17]:X2}-{page00[18]:X2}-{page00[19]:X2}",
            ReadAscii(page00, 20, 16),
            ReadAscii(page00, 36, 2),
            ReadAscii(page00, 38, 16),
            date,
            wavelength,
            null,
            technology,
            $"CMIS {FormatRevision(lower[1])} · {CmisCodeTables.MediaType(lower[85])}",
            true,
            true,
            false);
    }

    private static IReadOnlyList<ThresholdRow> DecodeThresholds(byte[]? page01, byte[]? page02)
    {
        if (page02 is not { Length: 128 })
        {
            return [];
        }

        var biasMultiplier = DecodeBiasMultiplier(page01);
        return
        [
            Threshold("temperature", "模块温度", "°C", page02, 0, raw => (short)raw / 256d),
            Threshold("voltage", "供电电压", "V", page02, 8, raw => raw * 0.0001),
            Threshold("tx-power", "各通道发射光功率", "mW", page02, 48, raw => raw * 0.0001),
            Threshold("bias", "各通道激光器偏置电流", "mA", page02, 56, raw => raw * 0.002 * biasMultiplier),
            Threshold("rx-power", "各通道接收光功率", "mW", page02, 64, raw => raw * 0.0001)
        ];
    }

    private static ThresholdRow Threshold(
        string id,
        string name,
        string unit,
        byte[] source,
        int offset,
        Func<ushort, double> converter) =>
        new(
            id,
            name,
            unit,
            converter(ReadUInt16(source, offset)),
            converter(ReadUInt16(source, offset + 4)),
            converter(ReadUInt16(source, offset + 6)),
            converter(ReadUInt16(source, offset + 2)));

    private static IReadOnlyList<Measurement> DecodeMeasurements(
        ModuleDump dump,
        byte[] lower,
        byte[]? page01,
        IReadOnlyList<ThresholdRow> thresholds)
    {
        var result = new List<Measurement>();
        Add(
            "temperature",
            "模块温度",
            "°C",
            ReadUInt16(lower, 14),
            raw => (short)raw / 256d,
            "temperature");
        Add(
            "voltage",
            "供电电压",
            "V",
            ReadUInt16(lower, 16),
            raw => raw * 0.0001,
            "voltage");

        var biasMultiplier = DecodeBiasMultiplier(page01);
        foreach (var region in dump.Regions
                     .Where(x => x.Id.EndsWith("-page11", StringComparison.Ordinal))
                     .OrderBy(x => x.Bank))
        {
            var bank = region.Bank ?? 0;
            for (var lane = 0; lane < 8; lane++)
            {
                var absoluteLane = bank * 8 + lane + 1;
                Add(
                    $"tx-power-lane-{absoluteLane}",
                    $"通道 {absoluteLane} 发射光功率",
                    "mW",
                    ReadUInt16(region.Data, 26 + lane * 2),
                    raw => raw * 0.0001,
                    "tx-power");
                Add(
                    $"bias-lane-{absoluteLane}",
                    $"通道 {absoluteLane} 偏置电流",
                    "mA",
                    ReadUInt16(region.Data, 42 + lane * 2),
                    raw => raw * 0.002 * biasMultiplier,
                    "bias");
                Add(
                    $"rx-power-lane-{absoluteLane}",
                    $"通道 {absoluteLane} 接收光功率",
                    "mW",
                    ReadUInt16(region.Data, 58 + lane * 2),
                    raw => raw * 0.0001,
                    "rx-power");
            }
        }

        return result;

        void Add(
            string id,
            string name,
            string unit,
            ushort raw,
            Func<ushort, double> converter,
            string thresholdId)
        {
            var threshold = thresholds.FirstOrDefault(x => x.Id == thresholdId);
            result.Add(new Measurement(
                id,
                name,
                converter(raw),
                unit,
                raw,
                threshold?.HighAlarm,
                threshold?.HighWarning,
                threshold?.LowWarning,
                threshold?.LowAlarm));
        }
    }

    private static IReadOnlyList<AlarmFlag> DecodeAlarms(ModuleDump dump, byte[] lower)
    {
        var result = new List<AlarmFlag>();
        AddByte(lower[8], "故障", "Lower.8",
            (2, "datapath-fw-error", "数据通道固件故障"),
            (1, "module-fw-error", "模块固件故障"),
            (0, "module-state-changed", "模块状态已变化"));
        if (new CmisRevision(lower[1]).IsAtLeast54)
        {
            AddByte(lower[8], "故障", "Lower.8.3",
                (3, "abnormal-fw-indication", "固件异常指示"));
        }
        AddByte(lower[9], "告警/预警", "Lower.9",
            (7, "vcc-low-warning", "电压低预警"),
            (6, "vcc-high-warning", "电压高预警"),
            (5, "vcc-low-alarm", "电压低告警"),
            (4, "vcc-high-alarm", "电压高告警"),
            (3, "temperature-low-warning", "温度低预警"),
            (2, "temperature-high-warning", "温度高预警"),
            (1, "temperature-low-alarm", "温度低告警"),
            (0, "temperature-high-alarm", "温度高告警"));

        var laneFlagRegisters = new (int Offset, string Id, string Name, string Level)[]
        {
            (7, "tx-failure", "Tx 内部故障", "告警"),
            (8, "tx-los", "Tx 信号丢失", "告警"),
            (9, "tx-cdr-lol", "Tx CDR 失锁", "告警"),
            (10, "tx-eq-fail", "Tx 自适应均衡失败", "告警"),
            (11, "tx-power-high-alarm", "Tx 光功率高告警", "告警"),
            (12, "tx-power-low-alarm", "Tx 光功率低告警", "告警"),
            (13, "tx-power-high-warning", "Tx 光功率高预警", "预警"),
            (14, "tx-power-low-warning", "Tx 光功率低预警", "预警"),
            (15, "bias-high-alarm", "偏置电流高告警", "告警"),
            (16, "bias-low-alarm", "偏置电流低告警", "告警"),
            (17, "bias-high-warning", "偏置电流高预警", "预警"),
            (18, "bias-low-warning", "偏置电流低预警", "预警"),
            (19, "rx-los", "Rx 信号丢失", "告警"),
            (20, "rx-cdr-lol", "Rx CDR 失锁", "告警"),
            (21, "rx-power-high-alarm", "Rx 光功率高告警", "告警"),
            (22, "rx-power-low-alarm", "Rx 光功率低告警", "告警"),
            (23, "rx-power-high-warning", "Rx 光功率高预警", "预警"),
            (24, "rx-power-low-warning", "Rx 光功率低预警", "预警"),
            (25, "rx-output-changed", "Rx 输出状态变化", "事件")
        };

        foreach (var region in dump.Regions
                     .Where(x => x.Id.EndsWith("-page11", StringComparison.Ordinal))
                     .OrderBy(x => x.Bank))
        {
            var bank = region.Bank ?? 0;
            foreach (var flagRegister in laneFlagRegisters)
            {
                for (var lane = 0; lane < 8; lane++)
                {
                    var absoluteLane = bank * 8 + lane + 1;
                    result.Add(new AlarmFlag(
                        $"{flagRegister.Id}-lane-{absoluteLane}",
                        $"通道 {absoluteLane} {flagRegister.Name}",
                        flagRegister.Level,
                        (region.Data[flagRegister.Offset] & (1 << lane)) != 0,
                        $"B{bank:X2}/P11h.{flagRegister.Offset + 128}"));
                }
            }
        }

        return result;

        void AddByte(byte value, string level, string source, params (int Bit, string Id, string Name)[] flags)
        {
            foreach (var flag in flags)
            {
                result.Add(new AlarmFlag(
                    flag.Id,
                    flag.Name,
                    level,
                    (value & (1 << flag.Bit)) != 0,
                    source));
            }
        }
    }

    private static IReadOnlyList<StatusItem> DecodeStatus(ModuleDump dump, byte[] lower)
    {
        var result = new List<StatusItem>
        {
            new("内存模型", (lower[2] & 0x80) == 0 ? "分页内存" : "平面内存", "Lower.2.7"),
            new("模块状态", CmisCodeTables.ModuleState((byte)((lower[3] >> 1) & 0x07)), "Lower.3.3-1"),
            new("中断信号", (lower[3] & 0x01) != 0 ? "已释放" : "已断言", "Lower.3.0"),
            new("仅分步配置", BitState(lower[2], 6), "Lower.2.6"),
            new("软件低功耗请求", BitState(lower[26], 4), "Lower.26.4"),
            new("固件版本", $"{lower[39]}.{lower[40]}", "Lower.39-40")
        };

        foreach (var region in dump.Regions
                     .Where(x => x.Id.EndsWith("-page11", StringComparison.Ordinal))
                     .OrderBy(x => x.Bank))
        {
            var bank = region.Bank ?? 0;
            for (var lane = 0; lane < 8; lane++)
            {
                var stateByte = region.Data[lane / 2];
                var state = (byte)(lane % 2 == 0 ? stateByte & 0x0F : stateByte >> 4);
                var absoluteLane = bank * 8 + lane + 1;
                result.Add(new StatusItem(
                    $"通道 {absoluteLane} 数据通道状态",
                    CmisCodeTables.DataPathState(state),
                    $"B{bank:X2}/P11h.{128 + lane / 2}"));
                result.Add(new StatusItem(
                    $"通道 {absoluteLane} Rx 输出",
                    (region.Data[4] & (1 << lane)) != 0 ? "有效" : "无效/静默",
                    $"B{bank:X2}/P11h.132"));
                result.Add(new StatusItem(
                    $"通道 {absoluteLane} Tx 输出",
                    (region.Data[5] & (1 << lane)) != 0 ? "有效" : "无效/静默",
                    $"B{bank:X2}/P11h.133"));
            }
        }

        return result;
    }

    private static IReadOnlyList<RegisterValue> DecodeRegisters(ModuleDump dump)
    {
        var result = new List<RegisterValue>();
        var revision = new CmisRevision(
            dump.FindRegion(CmisMemoryMap.LowerRegionId)?.Data.ElementAtOrDefault(1) ?? 0);
        foreach (var region in dump.Regions)
        {
            for (var index = 0; index < region.Data.Length; index++)
            {
                var offset = region.Offset + index;
                var (name, description, access) = CmisRegisterMap.Describe(region, offset, revision);
                result.Add(new RegisterValue(
                    region.Id,
                    region.DeviceAddress,
                    region.Page,
                    offset,
                    region.Data[index],
                    name,
                    description,
                    access,
                    region.Volatile,
                    region.Bank));
            }
        }

        return result;
    }

    private static (string Name, string Description, RegisterAccess Access) DescribeRegister(
        string regionId,
        int offset)
    {
        if (regionId == LowerRegionId)
        {
            return offset switch
            {
                0 => ("Identifier", "模块形态标识", RegisterAccess.ReadOnly),
                1 => ("CMIS Revision", "高四位主版本、低四位次版本", RegisterAccess.ReadOnly),
                2 => ("Module Characteristics", "内存模型与配置能力", RegisterAccess.ReadOnly),
                3 => ("Module State", "模块状态机与中断状态", RegisterAccess.ReadOnly),
                >= 4 and <= 11 => ("Module Flags", "模块和监控量锁存标志", RegisterAccess.ReadOnly),
                >= 14 and <= 25 => ("Module Monitors", "温度、电压及辅助实时监控值", RegisterAccess.ReadOnly),
                26 => ("Global Control", "低功耗请求、复位和广播控制", RegisterAccess.ReadWrite),
                >= 27 and <= 30 => ("Custom Control", "厂商自定义控制", RegisterAccess.VendorSpecific),
                >= 31 and <= 36 => ("Module Masks", "模块标志中断屏蔽", RegisterAccess.ReadWrite),
                >= 37 and <= 63 => ("Module Status", "CDB、固件和模块能力状态", RegisterAccess.ReadOnly),
                >= 64 and <= 84 => ("Custom", "厂商自定义", RegisterAccess.VendorSpecific),
                85 => ("Media Type", "媒体类型", RegisterAccess.ReadOnly),
                >= 86 and <= 117 => ("Application Descriptor", "应用描述符 1-8", RegisterAccess.ReadOnly),
                >= 118 and <= 125 => ("Password Entry", "密码变更/输入寄存器", RegisterAccess.WriteOnly),
                126 => ("Bank Select", "CMIS 银行选择", RegisterAccess.ReadWrite),
                127 => ("Page Select", "CMIS 页面选择", RegisterAccess.ReadWrite),
                _ => ("Reserved", "CMIS 保留字段", RegisterAccess.Reserved)
            };
        }

        if (regionId == Page00RegionId)
        {
            return offset switch
            {
                128 => ("Identifier Copy", "Identifier 副本", RegisterAccess.ReadOnly),
                >= 129 and <= 144 => ("Vendor Name", "厂商名称 ASCII", RegisterAccess.ReadOnly),
                >= 145 and <= 147 => ("Vendor OUI", "IEEE 公司标识", RegisterAccess.ReadOnly),
                >= 148 and <= 163 => ("Vendor PN", "产品料号 ASCII", RegisterAccess.ReadOnly),
                >= 164 and <= 165 => ("Vendor Revision", "产品版本 ASCII", RegisterAccess.ReadOnly),
                >= 166 and <= 181 => ("Vendor SN", "序列号 ASCII", RegisterAccess.ReadOnly),
                >= 182 and <= 189 => ("Date Code", "日期及批次 ASCII", RegisterAccess.ReadOnly),
                >= 190 and <= 199 => ("CLEI", "CLEI 代码", RegisterAccess.ReadOnly),
                200 => ("Power Class", "模块功耗等级", RegisterAccess.ReadOnly),
                201 => ("Max Power", "最大功耗，0.25 W", RegisterAccess.ReadOnly),
                202 => ("Cable Length", "线缆长度编码", RegisterAccess.ReadOnly),
                203 => ("Connector", "连接器类型", RegisterAccess.ReadOnly),
                >= 204 and <= 208 => ("Cable Attenuation", "不同频率下的铜缆衰减", RegisterAccess.ReadOnly),
                210 => ("Media Lane Support", "不支持的媒体通道位图", RegisterAccess.ReadOnly),
                211 => ("Far-end Configuration", "远端配置能力", RegisterAccess.ReadOnly),
                212 => ("Media Technology", "媒体接口技术", RegisterAccess.ReadOnly),
                222 => ("Page Checksum", "Page 00h 校验和", RegisterAccess.ReadOnly),
                >= 223 and <= 255 => ("Custom", "厂商自定义区域", RegisterAccess.VendorSpecific),
                _ => ("Static Advertising", "静态能力字段", RegisterAccess.ReadOnly)
            };
        }

        if (regionId == Page01RegionId)
        {
            return ("Capability Advertising", "CMIS 模块和通道能力声明", RegisterAccess.ReadOnly);
        }

        if (regionId == Page02RegionId)
        {
            return offset == 255
                ? ("Page Checksum", "Page 02h 校验和", RegisterAccess.ReadOnly)
                : ("Monitor Threshold", "模块级或通道级监控阈值", RegisterAccess.ReadOnly);
        }

        if (regionId == Page03RegionId)
        {
            return ("User EEPROM", "主机可读写的非易失用户区", RegisterAccess.ReadWrite);
        }

        if (regionId.EndsWith("-page10", StringComparison.Ordinal))
        {
            return offset switch
            {
                128 => ("DPDeinit", "数据通道初始化/去初始化控制", RegisterAccess.ReadWrite),
                >= 129 and <= 132 => ("Lane Direct Control", "通道极性、输出禁用或 Squelch 控制", RegisterAccess.ReadWrite),
                133 => ("Reserved", "CMIS 保留字段", RegisterAccess.Reserved),
                134 => ("Adaptive EQ Freeze", "冻结 Tx 自适应输入均衡", RegisterAccess.ReadWrite),
                >= 135 and <= 136 => ("Adaptive EQ Store", "写触发：保存 Tx 自适应均衡结果", RegisterAccess.WriteOnly),
                >= 137 and <= 139 => ("Rx Direct Control", "Rx 极性、输出禁用和 Squelch 控制", RegisterAccess.ReadWrite),
                >= 140 and <= 142 => ("Reserved", "CMIS 保留字段", RegisterAccess.Reserved),
                >= 143 and <= 144 => ("Apply Control Set", "写触发：应用暂存控制集", RegisterAccess.WriteOnly),
                >= 145 and <= 212 => ("Staged Control Set", "应用选择及 Tx/Rx 信号完整性配置", RegisterAccess.ReadWrite),
                >= 213 and <= 232 => ("Lane Flag Mask", "通道标志中断屏蔽", RegisterAccess.ReadWrite),
                >= 233 and <= 239 => ("Reserved", "CMIS 保留字段", RegisterAccess.Reserved),
                >= 240 and <= 255 => ("Custom", "厂商自定义控制区", RegisterAccess.VendorSpecific),
                _ => ("Lane Control", "分银行通道控制", RegisterAccess.ReadWrite)
            };
        }

        if (regionId.EndsWith("-page11", StringComparison.Ordinal))
        {
            return offset switch
            {
                >= 128 and <= 133 => ("Lane/Data Path Status", "数据通道状态与输出有效性", RegisterAccess.ReadOnly),
                >= 134 and <= 153 => ("Lane Flags", "通道锁存标志，部分为读清除", RegisterAccess.ReadOnly),
                >= 154 and <= 201 => ("Lane Monitors", "Tx/Rx 光功率及偏置电流", RegisterAccess.ReadOnly),
                >= 202 and <= 239 => ("Configuration Status", "配置执行结果、活动控制集和条件", RegisterAccess.ReadOnly),
                >= 240 and <= 255 => ("Lane Mapping", "媒体通道与波长/光纤映射", RegisterAccess.ReadOnly),
                _ => ("Lane Status", "分银行通道状态", RegisterAccess.ReadOnly)
            };
        }

        return ("CMIS Field", "CMIS 内存字段", RegisterAccess.ReadOnly);
    }

    private static IReadOnlyList<DecodedField> DecodeFields(
        ModuleDump dump,
        byte[] lower,
        byte[] page00,
        byte[]? page01)
    {
        var result = new List<DecodedField>();
        CmisStaticDecoder.Decode(
            dump,
            lower,
            page00,
            page01,
            dump.FindRegion(Page02RegionId)?.Data,
            result);
        CmisPagedDecoder.Decode(dump, result);
        CmisVdmDecoder.Decode(dump, result);
        CmisCdbDecoder.Decode(dump, result);
        return result;
    }

    private static List<DecodeDiagnostic> DecodeDiagnostics(
        ModuleDump dump,
        byte[] lower,
        byte[] page00,
        byte[]? page01,
        byte[]? page02)
    {
        var result = new List<DecodeDiagnostic>();
        if (page00[0] == lower[0])
        {
            result.Add(new DecodeDiagnostic(
                DiagnosticSeverity.Information,
                "Lower.0 与 Page 00h.128 的 Identifier 副本一致。",
                Page00RegionId));
        }
        else
        {
            result.Add(new DecodeDiagnostic(
                DiagnosticSeverity.Error,
                $"Identifier 副本不一致：Lower=0x{lower[0]:X2}，Page00=0x{page00[0]:X2}。",
                Page00RegionId));
        }

        ValidateChecksum(page00, 0, 93, 94, "Page 00h", Page00RegionId, result);
        if (page01 is not null)
        {
            ValidateChecksum(page01, 2, 126, 127, "Page 01h", Page01RegionId, result);
        }

        if (page02 is not null)
        {
            ValidateChecksum(page02, 0, 126, 127, "Page 02h", Page02RegionId, result);
        }

        var page04 = dump.Regions.FirstOrDefault(region => region.Page == 0x04)?.Data;
        if (page04 is not null)
        {
            ValidateChecksum(page04, 0, 126, 127, "Page 04h", CmisMemoryMap.RegionId(0x04), result);
        }

        result.Add(new DecodeDiagnostic(
            DiagnosticSeverity.Information,
            "CMIS 使用 7 位 I²C 地址 0x50；Bank Select=126，Page Select=127。",
            LowerRegionId));
        return result;
    }

    private static void ValidateChecksum(
        byte[] data,
        int start,
        int endInclusive,
        int checksumIndex,
        string name,
        string regionId,
        ICollection<DecodeDiagnostic> diagnostics)
    {
        var sum = 0;
        for (var index = start; index <= endInclusive; index++)
        {
            sum += data[index];
        }

        var expected = (byte)sum;
        diagnostics.Add(new DecodeDiagnostic(
            expected == data[checksumIndex] ? DiagnosticSeverity.Information : DiagnosticSeverity.Warning,
            expected == data[checksumIndex]
                ? $"{name} 校验和有效（0x{expected:X2}）。"
                : $"{name} 校验和无效：存储 0x{data[checksumIndex]:X2}，计算 0x{expected:X2}。",
            regionId));
    }

    private static byte[] RequireRegion(ModuleDump dump, string id, int minimumLength)
    {
        var data = dump.FindRegion(id)?.Data;
        if (data is null || data.Length < minimumLength)
        {
            throw new InvalidDataException($"CMIS Dump 缺少区域 {id} 或长度不足 {minimumLength} 字节。");
        }

        return data;
    }

    private static ushort ReadUInt16(byte[] source, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(source.AsSpan(offset, 2));

    private static string ReadAscii(byte[] source, int offset, int length) =>
        Encoding.ASCII.GetString(source, offset, length).Trim(' ', '\0', '\xFF');

    private static string FormatRevision(byte raw) => new CmisRevision(raw).ToString();

    private static int DecodeBankCount(byte supportedPages) =>
        CmisMemoryMap.DecodeLaneBankCount(supportedPages);

    private static int DecodeBiasMultiplier(byte[]? page01) =>
        page01 is not { Length: 128 }
            ? 1
            : ((page01[32] >> 3) & 0x03) switch
            {
                0 => 1,
                1 => 2,
                2 => 4,
                _ => 1
            };

    private static bool IsOptical(byte mediaType) => mediaType is 0x01 or 0x02;

    private static string BitState(byte value, int bit) =>
        (value & (1 << bit)) != 0 ? "1 / 是" : "0 / 否";

    private static string Bool(byte value, int bit) =>
        (value & (1 << bit)) != 0 ? "True" : "False";

    private static DecodedField Field(
        string category,
        string name,
        string value,
        string source,
        string description = "",
        bool writable = false) =>
        new(category, name, value, source, description, IsWritable: writable);
}
