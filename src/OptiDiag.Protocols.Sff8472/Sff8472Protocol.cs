using System.Buffers.Binary;
using System.Text;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Sff8472;

public sealed class Sff8472Protocol : IOpticalModuleProtocol
{
    public const string A0RegionId = "a0";
    public const string A2LowerRegionId = "a2-lower";
    public const string A2Page00RegionId = "a2-page00";
    public const string A2Page01RegionId = "a2-page01";
    public const string A2Page02RegionId = "a2-page02";
    public const string A2Page03RegionId = "a2-page03";
    public const string RemotePage20RegionId = "remote-page20-a0-lower";
    public const string RemotePage21RegionId = "remote-page21-a0-upper";
    public const string RemotePage22RegionId = "remote-page22-a2-lower";
    public const string RemotePage23RegionId = "remote-page23-a2-page00";
    public const string RemotePage24RegionId = "remote-page24-a2-page02";
    public const string RemotePage25RegionId = "remote-page25-reserved";
    public const string RemotePage26RegionId = "remote-page26-vendor";
    public const string RemotePage27RegionId = "remote-page27-vendor";

    public string Id => "sff-8472";

    public string DisplayName => "SFF-8472 (SFP/SFP+)";

    public string Revision => "12.5a";

    public IReadOnlyList<MemoryCaptureRegion> CapturePlan { get; } =
    [
        new(A0RegionId, "A0h / 0x50 Serial ID", 0x50, 0, 256),
        new(A2LowerRegionId, "A2h / 0x51 Lower Memory", 0x51, 0, 128, Volatile: true),
        new(A2Page00RegionId, "A2h Page 00h", 0x51, 128, 128, 0x00, PageSelectOffset: 127, Optional: true),
        new(A2Page01RegionId, "A2h Page 01h (legacy alias)", 0x51, 128, 128, 0x01, PageSelectOffset: 127, Optional: true),
        new(A2Page02RegionId, "A2h Page 02h (RDT/RPM)", 0x51, 128, 128, 0x02, PageSelectOffset: 127, Optional: true, Volatile: true),
        new(A2Page03RegionId, "A2h Page 03h (Timing)", 0x51, 128, 128, 0x03, PageSelectOffset: 127, Optional: true),
        new(RemotePage20RegionId, "A2h Page 20h Remote A0h Lower", 0x51, 128, 128, 0x20, PageSelectOffset: 127, Optional: true, Volatile: true),
        new(RemotePage21RegionId, "A2h Page 21h Remote A0h Upper", 0x51, 128, 128, 0x21, PageSelectOffset: 127, Optional: true, Volatile: true),
        new(RemotePage22RegionId, "A2h Page 22h Remote A2h Lower", 0x51, 128, 128, 0x22, PageSelectOffset: 127, Optional: true, Volatile: true),
        new(RemotePage23RegionId, "A2h Page 23h Remote A2h Page 00h/01h", 0x51, 128, 128, 0x23, PageSelectOffset: 127, Optional: true, Volatile: true),
        new(RemotePage24RegionId, "A2h Page 24h Remote A2h Page 02h", 0x51, 128, 128, 0x24, PageSelectOffset: 127, Optional: true, Volatile: true),
        new(RemotePage25RegionId, "A2h Page 25h Remote Reserved", 0x51, 128, 128, 0x25, PageSelectOffset: 127, Optional: true, Volatile: true),
        new(RemotePage26RegionId, "A2h Page 26h Remote Vendor", 0x51, 128, 128, 0x26, PageSelectOffset: 127, Optional: true, Volatile: true),
        new(RemotePage27RegionId, "A2h Page 27h Remote Vendor", 0x51, 128, 128, 0x27, PageSelectOffset: 127, Optional: true, Volatile: true)
    ];

    public bool ShouldCaptureRegion(
        MemoryCaptureRegion region,
        IReadOnlyList<MemoryRegionData> capturedRegions)
    {
        if (region.Id == A2Page03RegionId)
        {
            var a0 = capturedRegions.FirstOrDefault(x => x.Id == A0RegionId)?.Data;
            return a0 is { Length: > 65 } && (a0[65] & 0x01) != 0;
        }

        if (region.Id.StartsWith("remote-page", StringComparison.Ordinal))
        {
            var page02 = capturedRegions.FirstOrDefault(x => x.Id == A2Page02RegionId)?.Data;
            return page02 is { Length: >= 2 } && (page02[1] & 0x04) != 0;
        }

        return true;
    }

    public bool CanDecode(ModuleDump dump)
    {
        var a0 = dump.FindRegion(A0RegionId)?.Data;
        return a0 is { Length: >= 96 } && a0[0] == 0x03;
    }

    public DecodedModule Decode(ModuleDump dump)
    {
        var a0 = RequireRegion(dump, A0RegionId, 96);
        var a2 = RequireRegion(dump, A2LowerRegionId, 128);
        var diagnostics = new List<DecodeDiagnostic>();

        ValidateChecksum(a0, 0, 63, 63, "CC_BASE", diagnostics, A0RegionId);
        ValidateChecksum(a0, 64, 95, 95, "CC_EXT", diagnostics, A0RegionId);
        ValidateChecksum(a2, 0, 95, 95, "CC_DMI", diagnostics, A2LowerRegionId);
        ValidateTimingPageChecksum(dump, diagnostics);

        var information = DecodeInformation(a0);
        var calibration = Calibration.Create(information, a2, diagnostics);
        var thresholds = DecodeThresholds(a2, calibration);
        var measurements = DecodeMeasurements(a2, calibration, thresholds);
        var alarms = DecodeAlarms(a2);
        var status = DecodeStatus(a0, a2);
        var registers = DecodeRegisters(dump);

        if (!information.DigitalDiagnosticsImplemented)
        {
            diagnostics.Add(new DecodeDiagnostic(
                DiagnosticSeverity.Warning,
                "模块未声明数字诊断功能，A2h 监控值可能无效。",
                A0RegionId));
        }

        diagnostics.Add(new DecodeDiagnostic(
            DiagnosticSeverity.Information,
            "A0h/A2h 为规范中的 8 位名称；I2C 事务使用 7 位地址 0x50/0x51。"));

        return new DecodedModule(information, measurements, thresholds, alarms, status, registers, diagnostics);
    }

    private static ModuleInformation DecodeInformation(byte[] a0)
    {
        var diagnosticType = a0[92];
        return new ModuleInformation(
            LookupIdentifier(a0[0]),
            LookupConnector(a0[2]),
            ReadAscii(a0, 20, 16),
            $"{a0[37]:X2}-{a0[38]:X2}-{a0[39]:X2}",
            ReadAscii(a0, 40, 16),
            ReadAscii(a0, 56, 4),
            ReadAscii(a0, 68, 16),
            FormatDateCode(a0),
            BinaryPrimitives.ReadUInt16BigEndian(a0.AsSpan(60, 2)),
            DecodeSignalingRate(a0),
            LookupEncoding(a0[11]),
            LookupCompliance(a0[94]),
            (diagnosticType & 0x40) != 0,
            (diagnosticType & 0x20) != 0,
            (diagnosticType & 0x10) != 0);
    }

    private static IReadOnlyList<ThresholdRow> DecodeThresholds(byte[] a2, Calibration calibration)
    {
        var result = new List<ThresholdRow>
        {
            Threshold("temperature", "模块温度", "°C", a2, 0, calibration.Temperature),
            Threshold("voltage", "供电电压", "V", a2, 8, calibration.Voltage),
            Threshold("bias", "激光器偏置电流", "mA", a2, 16, calibration.Bias),
            Threshold("tx-power", "发射光功率", "mW", a2, 24, calibration.TxPower),
            Threshold("rx-power", "接收光功率", "mW", a2, 32, calibration.RxPower)
        };

        // Optional DWDM monitors have no separate advertisement bit. A non-zero
        // threshold or live value is therefore used as the practical presence test.
        if (HasAnyNonZero(a2, 40, 8) || HasAnyNonZero(a2, 106, 2))
        {
            result.Add(Threshold("laser-temperature", "激光器温度", "°C", a2, 40, raw => (short)raw / 256d));
        }

        if (HasAnyNonZero(a2, 48, 8) || HasAnyNonZero(a2, 108, 2))
        {
            result.Add(Threshold("tec-current", "TEC 电流", "mA", a2, 48, raw => (short)raw * 0.1));
        }

        return result;
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
        byte[] a2,
        Calibration calibration,
        IReadOnlyList<ThresholdRow> thresholds)
    {
        var result = new List<Measurement>(7);
        Add("temperature", "模块温度", "°C", 96, calibration.Temperature);
        Add("voltage", "供电电压", "V", 98, calibration.Voltage);
        Add("bias", "激光器偏置电流", "mA", 100, calibration.Bias);
        Add("tx-power", "发射光功率", "mW", 102, calibration.TxPower);
        Add("rx-power", "接收光功率", "mW", 104, calibration.RxPower);
        if (thresholds.Any(x => x.Id == "laser-temperature"))
        {
            Add("laser-temperature", "激光器温度", "°C", 106, raw => (short)raw / 256d);
        }

        if (thresholds.Any(x => x.Id == "tec-current"))
        {
            Add("tec-current", "TEC 电流", "mA", 108, raw => (short)raw * 0.1);
        }

        return result;

        void Add(string id, string name, string unit, int offset, Func<ushort, double> converter)
        {
            var raw = ReadUInt16(a2, offset);
            var threshold = thresholds.First(x => x.Id == id);
            result.Add(new Measurement(
                id,
                name,
                converter(raw),
                unit,
                raw,
                threshold.HighAlarm,
                threshold.HighWarning,
                threshold.LowWarning,
                threshold.LowAlarm));
        }
    }

    private static IReadOnlyList<AlarmFlag> DecodeAlarms(byte[] a2)
    {
        var result = new List<AlarmFlag>();
        AddByte(a2[112], "Alarm", "A2h.112",
            (7, "temperature-high-alarm", "温度高告警"),
            (6, "temperature-low-alarm", "温度低告警"),
            (5, "voltage-high-alarm", "电压高告警"),
            (4, "voltage-low-alarm", "电压低告警"),
            (3, "bias-high-alarm", "偏置电流高告警"),
            (2, "bias-low-alarm", "偏置电流低告警"),
            (1, "tx-high-alarm", "发射光功率高告警"),
            (0, "tx-low-alarm", "发射光功率低告警"));
        AddByte(a2[113], "Alarm", "A2h.113",
            (7, "rx-high-alarm", "接收光功率高告警"),
            (6, "rx-low-alarm", "接收光功率低告警"),
            (5, "laser-temperature-high-alarm", "激光器温度高告警"),
            (4, "laser-temperature-low-alarm", "激光器温度低告警"),
            (3, "tec-current-high-alarm", "TEC 电流高告警"),
            (2, "tec-current-low-alarm", "TEC 电流低告警"));
        AddByte(a2[116], "Warning", "A2h.116",
            (7, "temperature-high-warning", "温度高预警"),
            (6, "temperature-low-warning", "温度低预警"),
            (5, "voltage-high-warning", "电压高预警"),
            (4, "voltage-low-warning", "电压低预警"),
            (3, "bias-high-warning", "偏置电流高预警"),
            (2, "bias-low-warning", "偏置电流低预警"),
            (1, "tx-high-warning", "发射光功率高预警"),
            (0, "tx-low-warning", "发射光功率低预警"));
        AddByte(a2[117], "Warning", "A2h.117",
            (7, "rx-high-warning", "接收光功率高预警"),
            (6, "rx-low-warning", "接收光功率低预警"),
            (5, "laser-temperature-high-warning", "激光器温度高预警"),
            (4, "laser-temperature-low-warning", "激光器温度低预警"),
            (3, "tec-current-high-warning", "TEC 电流高预警"),
            (2, "tec-current-low-warning", "TEC 电流低预警"));
        return result;

        void AddByte(byte value, string level, string source, params (int Bit, string Id, string Name)[] flags)
        {
            foreach (var flag in flags)
            {
                result.Add(new AlarmFlag(flag.Id, flag.Name, level, (value & (1 << flag.Bit)) != 0, source));
            }
        }
    }

    private static IReadOnlyList<StatusItem> DecodeStatus(byte[] a0, byte[] a2) =>
    [
        new("数据就绪", BitState(a2[110], 0, inverted: true), "A2h.110.0", "Data Ready Bar"),
        new("RX_LOS", BitState(a2[110], 1), "A2h.110.1", "接收信号丢失"),
        new("TX_FAULT", BitState(a2[110], 2), "A2h.110.2", "发射器故障"),
        new("软 RS0 选择", BitState(a2[110], 3), "A2h.110.3", "软件速率选择"),
        new("RS0 引脚状态", BitState(a2[110], 4), "A2h.110.4"),
        new("RS1 引脚状态", BitState(a2[110], 5), "A2h.110.5"),
        new("软 TX_DISABLE", BitState(a2[110], 6), "A2h.110.6"),
        new("TX_DISABLE 状态", BitState(a2[110], 7), "A2h.110.7"),
        new("自适应输入 EQ 失败", BitState(a2[118], 4), "A2h.118.4"),
        new("软 RS1 选择", BitState(a2[118], 3), "A2h.118.3"),
        new("功耗等级 4 使能", BitState(a2[118], 2), "A2h.118.2"),
        new("高功耗运行状态", BitState(a2[118], 1), "A2h.118.1"),
        new("高功耗选择", BitState(a2[118], 0), "A2h.118.0"),
        new("PAM4 TX 已配置", BitState(a2[119], 4), "A2h.119.4"),
        new("PAM4 RX 已配置", BitState(a2[119], 3), "A2h.119.3"),
        new("64GFC 模式", BitState(a2[119], 2), "A2h.119.2"),
        new("TX CDR 失锁", BitState(a2[119], 1), "A2h.119.1"),
        new("RX CDR 失锁", BitState(a2[119], 0), "A2h.119.0"),
        new("增强选项", $"0x{a0[93]:X2}", "A0h.93")
    ];

    private static IReadOnlyList<RegisterValue> DecodeRegisters(ModuleDump dump)
    {
        var result = new List<RegisterValue>();
        var a0 = dump.FindRegion(A0RegionId)?.Data;
        var externallyCalibrated = a0 is { Length: > 92 } && (a0[92] & 0x10) != 0;
        foreach (var region in dump.Regions)
        {
            for (var index = 0; index < region.Data.Length; index++)
            {
                var offset = region.Offset + index;
                var (name, description, access) = DescribeRegister(region.Id, offset, externallyCalibrated);
                result.Add(new RegisterValue(
                    region.Id,
                    region.DeviceAddress,
                    region.Page,
                    offset,
                    region.Data[index],
                    name,
                    description,
                    access,
                    region.Volatile));
            }
        }

        return result;
    }

    private static (string Name, string Description, RegisterAccess Access) DescribeRegister(
        string regionId,
        int offset,
        bool externallyCalibrated)
    {
        if (regionId == A0RegionId)
        {
            return offset switch
            {
                0 => ("Identifier", "模块类型标识", RegisterAccess.ReadOnly),
                1 => ("Ext. Identifier", "扩展标识", RegisterAccess.ReadOnly),
                2 => ("Connector", "连接器代码", RegisterAccess.ReadOnly),
                >= 3 and <= 10 => ("Transceiver compliance", "收发器兼容性代码", RegisterAccess.ReadOnly),
                11 => ("Encoding", "串行编码", RegisterAccess.ReadOnly),
                12 => ("Signaling rate", "标称信令速率，100 MBd", RegisterAccess.ReadOnly),
                13 => ("Rate identifier", "速率选择实现", RegisterAccess.ReadOnly),
                >= 14 and <= 19 => ("Length / attenuation", "链路长度或铜缆衰减", RegisterAccess.ReadOnly),
                >= 20 and <= 35 => ("Vendor name", "厂商名称 ASCII", RegisterAccess.ReadOnly),
                36 => ("Extended compliance", "扩展收发器兼容性代码", RegisterAccess.ReadOnly),
                >= 37 and <= 39 => ("Vendor OUI", "IEEE 公司标识", RegisterAccess.ReadOnly),
                >= 40 and <= 55 => ("Vendor PN", "产品料号 ASCII", RegisterAccess.ReadOnly),
                >= 56 and <= 59 => ("Vendor revision", "产品版本 ASCII", RegisterAccess.ReadOnly),
                >= 60 and <= 61 => ("Wavelength", "激光波长", RegisterAccess.ReadOnly),
                62 => ("Fibre Channel speed 2", "辅助 Fibre Channel 速率能力", RegisterAccess.ReadOnly),
                63 => ("CC_BASE", "基础字段校验", RegisterAccess.ReadOnly),
                >= 64 and <= 65 => ("Options", "可选硬件信号和附加页面声明", RegisterAccess.ReadOnly),
                66 => ("Signaling rate max", "信令速率上限余量", RegisterAccess.ReadOnly),
                67 => ("Signaling rate min", "信令速率下限余量", RegisterAccess.ReadOnly),
                >= 68 and <= 83 => ("Vendor SN", "序列号 ASCII", RegisterAccess.ReadOnly),
                >= 84 and <= 91 => ("Date code", "生产日期与批次", RegisterAccess.ReadOnly),
                92 => ("Diagnostic type", "数字诊断与校准类型", RegisterAccess.ReadOnly),
                93 => ("Enhanced options", "增强功能声明", RegisterAccess.ReadOnly),
                94 => ("SFF-8472 compliance", "协议版本代码", RegisterAccess.ReadOnly),
                95 => ("CC_EXT", "扩展字段校验", RegisterAccess.ReadOnly),
                >= 96 and <= 127 => ("Vendor specific", "厂商自定义区域", RegisterAccess.VendorSpecific),
                >= 128 and <= 255 => ("Reserved", "SFF-8472 保留区域", RegisterAccess.Reserved),
                _ => ("Standard field", "SFF-8472 标准字段", RegisterAccess.ReadOnly)
            };
        }

        if (regionId == A2LowerRegionId)
        {
            return offset switch
            {
                >= 0 and <= 55 => ("Alarm/Warning threshold", "告警或预警阈值", RegisterAccess.ReadOnly),
                >= 56 and <= 91 when externallyCalibrated => ("External calibration", "外部校准常数", RegisterAccess.ReadOnly),
                >= 56 and <= 65 => ("Enhanced advertisement", "增强控制、状态和信号完整性能力声明", RegisterAccess.ReadOnly),
                66 => ("Max power consumption", "最大功耗，LSB 0.1 W", RegisterAccess.ReadOnly),
                67 => ("Secondary compliance", "辅助扩展规范兼容代码", RegisterAccess.ReadOnly),
                >= 68 and <= 70 => ("Reserved", "增强功能保留", RegisterAccess.Reserved),
                >= 71 and <= 74 => ("Enhanced control", "增强均衡、幅度、速率和 Squelch 控制", RegisterAccess.ReadWrite),
                >= 75 and <= 94 => ("Reserved", "增强功能保留", RegisterAccess.Reserved),
                95 => ("CC_DMI", "诊断字段校验", RegisterAccess.ReadOnly),
                >= 96 and <= 105 => ("Real-time diagnostic", "实时诊断值", RegisterAccess.ReadOnly),
                >= 106 and <= 107 => ("Optional Laser Temperature", "可选激光器温度监控", RegisterAccess.ReadOnly),
                >= 108 and <= 109 => ("Optional TEC Current", "可选 TEC 电流监控", RegisterAccess.ReadOnly),
                110 => ("Status/Control", "模块状态与软控制", RegisterAccess.ReadWrite),
                111 => ("Reserved", "保留", RegisterAccess.Reserved),
                >= 112 and <= 113 => ("Alarm flags", "告警标志", RegisterAccess.ReadOnly),
                114 => ("Tx Input EQ", "发射输入均衡控制，高/低速率各 4 bit", RegisterAccess.ReadWrite),
                115 => ("Rx Output Emphasis", "接收输出加重控制，高/低速率各 4 bit", RegisterAccess.ReadWrite),
                >= 116 and <= 117 => ("Warning flags", "预警标志", RegisterAccess.ReadOnly),
                >= 118 and <= 119 => ("Extended status/control", "扩展状态与控制", RegisterAccess.ReadWrite),
                >= 120 and <= 126 => ("Vendor specific", "厂商自定义存储或控制", RegisterAccess.VendorSpecific),
                127 => ("Page Select", "上半区页面选择", RegisterAccess.ReadWrite),
                _ => ("Reserved", "保留", RegisterAccess.Reserved)
            };
        }

        if (regionId is A2Page00RegionId or A2Page01RegionId)
        {
            return offset >= 248
                ? ("Vendor control", "厂商自定义控制区", RegisterAccess.VendorSpecific)
                : ("User EEPROM", "用户可读写 EEPROM", RegisterAccess.ReadWrite);
        }

        return regionId switch
        {
            A2Page02RegionId => DescribePage02Register(offset),
            A2Page03RegionId => DescribePage03Register(offset),
            RemotePage20RegionId => ("Remote A0h lower", "远端模块 A0h 字节 0-127 的镜像", RegisterAccess.ReadOnly),
            RemotePage21RegionId => ("Remote A0h upper", "远端模块 A0h 字节 128-255 的镜像", RegisterAccess.ReadOnly),
            RemotePage22RegionId => ("Remote A2h lower", "远端模块 A2h 字节 0-127 的镜像", RegisterAccess.ReadOnly),
            RemotePage23RegionId => ("Remote A2h Page 00h/01h", "远端模块用户 EEPROM 的镜像", RegisterAccess.ReadOnly),
            RemotePage24RegionId => ("Remote A2h Page 02h", "远端模块 RDT/RPM 页的镜像", RegisterAccess.ReadOnly),
            RemotePage25RegionId => ("Remote reserved", "远端模块保留页", RegisterAccess.Reserved),
            RemotePage26RegionId or RemotePage27RegionId => ("Remote vendor data", "远端模块厂商自定义页", RegisterAccess.VendorSpecific),
            _ => ("Upper memory", "上半区或厂商数据", RegisterAccess.VendorSpecific)
        };
    }

    private static (string Name, string Description, RegisterAccess Access) DescribePage02Register(int offset) =>
        offset switch
        {
            128 => ("Tunability advertisement", "SFF-8690 可调谐能力声明", RegisterAccess.ReadOnly),
            129 => ("RDT/RPM advertisement", "SFF-8472 RDT/RPM 能力声明", RegisterAccess.ReadOnly),
            >= 130 and <= 131 => ("RDT control", "接收判决门限控制/当前值", RegisterAccess.ReadWrite),
            >= 132 and <= 141 => ("Tunable capabilities", "SFF-8690 模块能力声明", RegisterAccess.ReadOnly),
            >= 142 and <= 143 => ("Reserved", "SFF-8690 保留", RegisterAccess.Reserved),
            >= 144 and <= 147 => ("Channel tuning", "SFF-8690 频率/波长控制", RegisterAccess.ReadWrite),
            >= 148 and <= 150 => ("Reserved", "SFF-8690 保留", RegisterAccess.Reserved),
            151 => ("Module TX control", "SFF-8690 模块发射控制", RegisterAccess.ReadWrite),
            >= 152 and <= 155 => ("Wavelength error", "SFF-8690 频率或波长误差", RegisterAccess.ReadOnly),
            >= 156 and <= 167 => ("Reserved", "SFF-8690 可调谐区保留", RegisterAccess.Reserved),
            168 => ("Tunable status", "SFF-8690 当前状态", RegisterAccess.ReadOnly),
            >= 169 and <= 171 => ("Reserved", "SFF-8690 附加状态保留", RegisterAccess.Reserved),
            172 => ("Tunable latched status", "SFF-8690 锁存状态", RegisterAccess.ReadOnly),
            173 => ("Reserved", "SFF-8690 附加锁存状态保留", RegisterAccess.Reserved),
            >= 174 and <= 175 => ("RPM latched status", "远端性能监控清读锁存状态", RegisterAccess.ReadOnly),
            >= 176 and <= 191 => ("Reserved", "SFF-8472 保留", RegisterAccess.Reserved),
            192 => ("RPM status/reset", "RPM 状态；写 A5h 可复位错误计数器", RegisterAccess.ReadWrite),
            >= 193 and <= 197 => ("RPM debug", "最近接收的 RPM 消息调试数据", RegisterAccess.ReadOnly),
            >= 198 and <= 207 => ("RPM error counters", "RPM 帧错误计数器", RegisterAccess.ReadOnly),
            >= 208 and <= 210 => ("RPM remote command", "发送远端内存控制消息", RegisterAccess.ReadWrite),
            211 => ("RPM TX modulation index", "RPM 发射使能与调制指数", RegisterAccess.ReadWrite),
            >= 212 and <= 219 => ("RPM control/security", "RPM 控制与安全设置", RegisterAccess.ReadWrite),
            >= 220 and <= 239 => ("Reserved", "SFF-8472 保留", RegisterAccess.Reserved),
            >= 240 and <= 247 => ("RPM TX user data", "写入远端性能监控用户数据", RegisterAccess.ReadWrite),
            >= 248 and <= 255 => ("RPM RX user data", "接收的远端性能监控用户数据", RegisterAccess.ReadOnly),
            _ => ("Page 02h", "RDT/RPM 或 SFF-8690 数据", RegisterAccess.ReadOnly)
        };

    private static (string Name, string Description, RegisterAccess Access) DescribePage03Register(int offset) =>
        offset switch
        {
            >= 128 and <= 129 => ("Format ID", "CA1Bh=光模块，100Bh=回环模块", RegisterAccess.ReadOnly),
            130 => ("Calibration version", "高精度时延校准格式版本", RegisterAccess.ReadOnly),
            >= 131 and <= 133 => ("Calibration date", "校准日期", RegisterAccess.ReadOnly),
            >= 134 and <= 139 => ("Calibration unique ID", "校准唯一标识 CUI", RegisterAccess.ReadOnly),
            140 => ("Stratum", "校准链路层级", RegisterAccess.ReadOnly),
            >= 141 and <= 149 => ("Common header", "高精度时延公共头", RegisterAccess.ReadOnly),
            >= 150 and <= 186 => ("Timing calibration", "光模块或回环模块时延校准参数", RegisterAccess.ReadOnly),
            >= 187 and <= 254 => ("Reserved", "高精度时延页保留", RegisterAccess.Reserved),
            255 => ("CC_CALIB", "字节 128-254 校验码", RegisterAccess.ReadOnly),
            _ => ("Page 03h", "高精度时延校准", RegisterAccess.ReadOnly)
        };

    private static byte[] RequireRegion(ModuleDump dump, string id, int length)
    {
        var region = dump.FindRegion(id)
            ?? throw new InvalidDataException($"Dump 缺少区域 {id}。");
        if (region.Data.Length < length)
        {
            throw new InvalidDataException($"区域 {id} 长度不足：需要 {length}，实际 {region.Data.Length}。");
        }

        return region.Data;
    }

    private static void ValidateTimingPageChecksum(
        ModuleDump dump,
        ICollection<DecodeDiagnostic> diagnostics)
    {
        var page = dump.FindRegion(A2Page03RegionId)?.Data;
        if (page is not { Length: >= 128 })
        {
            return;
        }

        var formatId = BinaryPrimitives.ReadUInt16BigEndian(page.AsSpan(0, 2));
        if (formatId is not (0xCA1B or 0x100B))
        {
            diagnostics.Add(new DecodeDiagnostic(
                DiagnosticSeverity.Information,
                $"Page 03h 未声明有效的高精度时延格式（Format ID 0x{formatId:X4}）。",
                A2Page03RegionId));
            return;
        }

        ValidateChecksum(page, 0, 127, 127, "CC_CALIB", diagnostics, A2Page03RegionId);
    }

    private static void ValidateChecksum(
        byte[] data,
        int start,
        int endExclusive,
        int checksumOffset,
        string name,
        ICollection<DecodeDiagnostic> diagnostics,
        string regionId)
    {
        var expected = ComputeChecksum(data.AsSpan(start, endExclusive - start));
        var actual = data[checksumOffset];
        diagnostics.Add(expected == actual
            ? new DecodeDiagnostic(DiagnosticSeverity.Information, $"{name} 校验通过（0x{actual:X2}）。", regionId)
            : new DecodeDiagnostic(DiagnosticSeverity.Warning, $"{name} 校验失败：计算 0x{expected:X2}，读取 0x{actual:X2}。", regionId));
    }

    private static byte ComputeChecksum(ReadOnlySpan<byte> data)
    {
        var sum = 0;
        foreach (var value in data)
        {
            sum += value;
        }

        return (byte)sum;
    }

    private static bool HasAnyNonZero(byte[] source, int offset, int length) =>
        source.AsSpan(offset, length).IndexOfAnyExcept((byte)0) >= 0;

    private static string ReadAscii(byte[] data, int offset, int length) =>
        Encoding.ASCII.GetString(data, offset, length).Trim(' ', '\0', '\u00ff');

    private static string FormatDateCode(byte[] a0)
    {
        var date = ReadAscii(a0, 84, 6);
        var lot = ReadAscii(a0, 90, 2);
        if (date.Length == 6)
        {
            return $"20{date[..2]}-{date.Substring(2, 2)}-{date.Substring(4, 2)} / {lot}";
        }

        return $"{date} / {lot}";
    }

    private static double? DecodeSignalingRate(byte[] a0)
    {
        if (a0[12] == 0)
        {
            return null;
        }

        if (a0[12] != 0xFF)
        {
            return a0[12] * 100d;
        }

        return a0[66] == 0 ? null : a0[66] * 250d;
    }

    private static ushort ReadUInt16(byte[] source, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(source.AsSpan(offset, 2));

    private static short ReadInt16(byte[] source, int offset) =>
        BinaryPrimitives.ReadInt16BigEndian(source.AsSpan(offset, 2));

    private static string BitState(byte value, int bit, bool inverted = false)
    {
        var set = (value & (1 << bit)) != 0;
        if (inverted)
        {
            set = !set;
        }

        return set ? "是" : "否";
    }

    private static string LookupIdentifier(byte value) => value switch
    {
        0x03 => "SFP / SFP+ / SFP28",
        0x0B => "DWDM-SFP/SFP+",
        0x1A => "SFP-DD",
        0x20 => "SFP+ (CMIS)",
        _ => $"未知 (0x{value:X2})"
    };

    private static string LookupConnector(byte value) => value switch
    {
        0x00 => "未指定",
        0x01 => "SC",
        0x07 => "LC",
        0x21 => "铜缆尾纤",
        0x22 => "RJ45",
        0x23 => "无可分离连接器",
        _ => $"代码 0x{value:X2}"
    };

    private static string LookupEncoding(byte value) => value switch
    {
        0x00 => "未指定",
        0x01 => "8B/10B",
        0x02 => "4B/5B",
        0x03 => "NRZ",
        0x05 => "SONET Scrambled",
        0x06 => "64B/66B",
        0x07 => "Manchester",
        0x08 => "256B/257B",
        0x09 => "PAM4",
        _ => $"代码 0x{value:X2}"
    };

    private static string LookupCompliance(byte value) => value switch
    {
        0x00 => "未指定",
        0x01 => "Rev 9.3",
        0x02 => "Rev 9.5",
        0x03 => "Rev 10.2",
        0x04 => "Rev 10.4",
        0x05 => "Rev 11.0",
        0x06 => "Rev 11.3",
        0x07 => "Rev 11.4",
        0x08 => "Rev 12.3",
        0x09 => "Rev 12.4",
        0x0A => "Rev 12.5/12.5a",
        _ => $"代码 0x{value:X2}"
    };

    private sealed class Calibration
    {
        private Calibration(
            Func<ushort, double> temperature,
            Func<ushort, double> voltage,
            Func<ushort, double> bias,
            Func<ushort, double> txPower,
            Func<ushort, double> rxPower)
        {
            Temperature = temperature;
            Voltage = voltage;
            Bias = bias;
            TxPower = txPower;
            RxPower = rxPower;
        }

        public Func<ushort, double> Temperature { get; }
        public Func<ushort, double> Voltage { get; }
        public Func<ushort, double> Bias { get; }
        public Func<ushort, double> TxPower { get; }
        public Func<ushort, double> RxPower { get; }

        public static Calibration Create(
            ModuleInformation information,
            byte[] a2,
            ICollection<DecodeDiagnostic> diagnostics)
        {
            if (!information.ExternallyCalibrated)
            {
                return new Calibration(
                    raw => (short)raw / 256d,
                    raw => raw * 0.0001,
                    raw => raw * 0.002,
                    raw => raw * 0.0001,
                    raw => raw * 0.0001);
            }

            var rx = new[]
            {
                ReadSingle(a2, 72),
                ReadSingle(a2, 68),
                ReadSingle(a2, 64),
                ReadSingle(a2, 60),
                ReadSingle(a2, 56)
            };
            var txISlope = ReadUnsignedSlope(a2, 76);
            var txIOffset = ReadInt16(a2, 78);
            var txPowerSlope = ReadUnsignedSlope(a2, 80);
            var txPowerOffset = ReadInt16(a2, 82);
            var temperatureSlope = ReadUnsignedSlope(a2, 84);
            var temperatureOffset = ReadInt16(a2, 86);
            var voltageSlope = ReadUnsignedSlope(a2, 88);
            var voltageOffset = ReadInt16(a2, 90);

            diagnostics.Add(new DecodeDiagnostic(
                DiagnosticSeverity.Information,
                "已按 SFF-8472 外部校准常数换算实时值和阈值。",
                A2LowerRegionId));

            return new Calibration(
                raw => (temperatureSlope * (short)raw + temperatureOffset) / 256d,
                raw => (voltageSlope * raw + voltageOffset) * 0.0001,
                raw => (txISlope * raw + txIOffset) * 0.002,
                raw => (txPowerSlope * raw + txPowerOffset) * 0.0001,
                // The polynomial result is expressed in 0.1 µW. Convert to mW.
                raw => EvaluatePolynomial(rx, raw) * 0.0001);
        }

        private static double ReadUnsignedSlope(byte[] source, int offset) => ReadUInt16(source, offset) / 256d;

        private static float ReadSingle(byte[] source, int offset)
        {
            var bits = BinaryPrimitives.ReadInt32BigEndian(source.AsSpan(offset, 4));
            return BitConverter.Int32BitsToSingle(bits);
        }

        private static double EvaluatePolynomial(IReadOnlyList<float> coefficients, ushort raw)
        {
            var value = 0d;
            for (var index = coefficients.Count - 1; index >= 0; index--)
            {
                value = value * raw + coefficients[index];
            }

            return value;
        }
    }
}
