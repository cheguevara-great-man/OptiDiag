using System.Buffers.Binary;
using System.Text;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Protocols.Sff8472;

public sealed partial class Sff8472Protocol
{
    private static void DecodePage02Fields(ModuleDump dump, ICollection<DecodedField> fields)
    {
        var page = dump.FindRegion(A2Page02RegionId)?.Data;
        if (page is not { Length: >= 128 })
        {
            return;
        }

        byte P(int absoluteOffset) => page[absoluteOffset - 128];
        var advertisement = P(129);
        Add(fields, "Page 02h/RDT", "RDT Behavior",
            IsSet(advertisement, 0) ? "Rev 12.5 模式" : "Rev 12.4 及以前兼容模式",
            "A2h P02.129.0");
        Add(fields, "Page 02h/RDT", "Current RDT Value Readable",
            YesNo(IsSet(advertisement, 1)), "A2h P02.129.1");
        Add(fields, "Page 02h/RDT", "RDT Control Mode",
            IsSet(P(130), 0) ? "自动（模块控制环）" : "手动", "A2h P02.130.0",
            writable: IsSet(advertisement, 0));
        var signedRdt = unchecked((sbyte)P(131));
        Add(fields, "Page 02h/RDT", "Receiver Decision Threshold",
            $"{50 + signedRdt / 256d * 100:0.###}%", "A2h P02.131",
            $"原始有符号值 {signedRdt}；公式 50% + value/256×100%。",
            writable: !IsSet(P(130), 0));

        var rpmSupported = IsSet(advertisement, 2);
        Add(fields, "Page 02h/RPM", "RPM Supported", YesNo(rpmSupported), "A2h P02.129.2");
        if (!rpmSupported)
        {
            return;
        }

        AddBitP02(fields, page, "RPM 状态", "S1 Data Ready", 192, 7);
        AddBitP02(fields, page, "RPM 状态", "High Clock Detected", 192, 5);
        AddBitP02(fields, page, "RPM 状态", "Low Clock Detected", 192, 4);
        AddBitP02(fields, page, "RPM 状态", "Frame Locked", 192, 3);
        AddBitP02(fields, page, "RPM 状态", "Phase Locked", 192, 2);
        AddBitP02(fields, page, "RPM 状态", "Bit Boundaries Locked", 192, 1);
        AddBitP02(fields, page, "RPM 状态", "Clock Locked", 192, 0);

        var msg = (P(193) << 16) | (P(194) << 8) | P(195);
        var tom = ((P(196) & 0x07) << 8) | P(197);
        Add(fields, "RPM 调试", "Most Recent RX MSG", $"0x{msg:X6}", "A2h P02.193-195");
        Add(fields, "RPM 调试", "Most Recent RX TOM", $"0x{tom:X3}", "A2h P02.196-197");
        Add(fields, "RPM 计数器", "Frame Loss-of-Lock Count",
            ReadUInt16P02(page, 198).ToString(), "A2h P02.198-199");
        Add(fields, "RPM 计数器", "Frame Count",
            ReadUInt32P02(page, 200).ToString(), "A2h P02.200-203");
        Add(fields, "RPM 计数器", "TOM Error Count",
            ReadUInt16P02(page, 204).ToString(), "A2h P02.204-205");
        Add(fields, "RPM 计数器", "MSG Error Count",
            ReadUInt16P02(page, 206).ToString(), "A2h P02.206-207");

        var remoteCommand = P(208) | (P(209) << 8) | (P(210) << 16);
        Add(fields, "RPM 控制", "Remote Control MSG", $"0x{remoteCommand:X6}",
            "A2h P02.208-210", "写 Byte 210 触发 TOM=2A0h 消息发送。", writable: true);
        var modulation = P(211) & 0x7F;
        Add(fields, "RPM 控制", "TX Modulation Index",
            modulation switch
            {
                0 => "关闭",
                < 10 => "1.0%（保留写值按 1% 处理）",
                <= 100 => $"{modulation / 10d:0.0}%",
                _ => $"规范范围外 ({modulation})"
            }, "A2h P02.211.6-0", writable: true);
        Add(fields, "RPM 控制", "TX RPM State", $"0x{P(212) >> 4:X1}", "A2h P02.212.7-4");
        AddBitP02(fields, page, "RPM 控制", "Enable Local and Request Remote RPM", 212, 3, writable: true);
        AddBitP02(fields, page, "RPM 控制", "Remote Requested Data", 212, 2);
        AddBitP02(fields, page, "RPM 控制", "TX RPM Enable", 212, 0, writable: true);
        Add(fields, "RPM 控制", "RX RPM State", $"0x{P(213) >> 4:X1}", "A2h P02.213.7-4");
        AddBitP02(fields, page, "RPM 控制", "RX RPM Enable", 213, 0, writable: true);

        var transferGroups = new (int Bit, string Name)[]
        {
            (6, "A2h Page 03h 128-255"),
            (5, "A2h Page 02h 208-239"),
            (4, "A2h Page 02h 128-191"),
            (3, "A2h Page 00h 128-255"),
            (2, "A2h 0-95,120-127"),
            (1, "A0h 128-255"),
            (0, "A0h 96-127")
        };
        foreach (var group in transferGroups)
        {
            AddBitP02(fields, page, "RPM 发送内容", group.Name, 214, group.Bit, writable: true);
        }

        var txProtectionGroups = new (int Bit, string Name)[]
        {
            (7, "Inhibit A2h Page 03h"),
            (6, "Inhibit A2h Page 02h 208-239"),
            (5, "Inhibit A2h Page 02h 128-191"),
            (4, "Inhibit A2h Page 00h"),
            (3, "Inhibit A2h 120-127"),
            (2, "Inhibit A2h 0-95"),
            (1, "Inhibit A0h 128-255"),
            (0, "Inhibit A0h 96-127")
        };
        foreach (var protection in txProtectionGroups)
        {
            AddBitP02(fields, page, "RPM TX 安全", protection.Name, 216, protection.Bit, writable: true);
        }

        AddBitP02(fields, page, "RPM TX 安全", "Global TX RPM Disable", 217, 7, writable: true);
        AddBitP02(fields, page, "RPM RX 安全", "Global Write Protect", 218, 7, writable: true);
        AddBitP02(fields, page, "RPM RX 安全", "Protect TX Modulation Index", 218, 2, writable: true);
        AddBitP02(fields, page, "RPM RX 安全", "Protect Receiver Controls", 218, 1, writable: true);
        AddBitP02(fields, page, "RPM RX 安全", "Protect Tunable Registers", 218, 0, writable: true);

        Add(fields, "RPM 用户数据", "TX User Data", FormatBytes(page.AsSpan(112, 8)),
            "A2h P02.240-247", "写 Byte 247 后触发发送全部 8 字节。", writable: true);
        Add(fields, "RPM 用户数据", "RX User Data", FormatBytes(page.AsSpan(120, 8)),
            "A2h P02.248-255");
    }

    private static void AppendPage02Alarms(ModuleDump dump, ICollection<AlarmFlag> alarms)
    {
        var page = dump.FindRegion(A2Page02RegionId)?.Data;
        if (page is not { Length: >= 128 })
        {
            return;
        }

        AddAlarmByte(174, "RPM 锁存",
            (7, "rpm-rx-user-data", "收到新的 RPM 用户数据"),
            (6, "rpm-rx-user-changed", "RPM 用户数据内容变化"),
            (5, "rpm-tx-user-sending", "RPM 用户数据正在发送"),
            (4, "rpm-tx-user-overrun", "RPM 用户数据发送溢出"),
            (3, "rpm-global-rx-error", "RPM 全局接收错误"),
            (2, "rpm-msg-error", "RPM MSG Hamming 错误"),
            (1, "rpm-tom-error", "RPM TOM Hamming 错误"),
            (0, "rpm-frame-unlock", "RPM 帧失锁"));
        AddAlarmByte(175, "RPM 锁存",
            (3, "rpm-security-idle-tx", "发送 RPM 安全空闲消息"),
            (2, "rpm-security-idle-rx", "收到 RPM 安全空闲消息"),
            (1, "rpm-control-nack", "收到 RPM 控制 NACK"),
            (0, "rpm-control-ack", "收到 RPM 控制 ACK"));
        AddAlarmByte(172, "SFF-8690 锁存",
            (7, "tunable-self-tuning", "自调谐进行中"),
            (6, "tunable-tec-fault", "TEC/温控故障"),
            (5, "tunable-wavelength-unlocked", "波长失锁"),
            (4, "tunable-bad-channel", "请求了无效通道"),
            (3, "tunable-new-channel", "已获得新通道"),
            (2, "tunable-dither-unsupported", "请求了不支持的 TX Dither"));

        void AddAlarmByte(
            int absoluteOffset,
            string level,
            params (int Bit, string Id, string Name)[] definitions)
        {
            var value = page[absoluteOffset - 128];
            foreach (var definition in definitions)
            {
                alarms.Add(new AlarmFlag(
                    definition.Id,
                    definition.Name,
                    level,
                    IsSet(value, definition.Bit),
                    $"A2h P02.{absoluteOffset}.{definition.Bit}"));
            }
        }
    }

    private static void AddBitP02(
        ICollection<DecodedField> fields,
        byte[] page,
        string category,
        string name,
        int absoluteOffset,
        int bit,
        string description = "",
        bool writable = false) =>
        Add(fields, category, name, YesNo(IsSet(page[absoluteOffset - 128], bit)),
            $"A2h P02.{absoluteOffset}.{bit}", description, writable: writable);

    private static ushort ReadUInt16P02(byte[] page, int absoluteOffset) =>
        BinaryPrimitives.ReadUInt16BigEndian(page.AsSpan(absoluteOffset - 128, 2));

    private static uint ReadUInt32P02(byte[] page, int absoluteOffset) =>
        BinaryPrimitives.ReadUInt32BigEndian(page.AsSpan(absoluteOffset - 128, 4));

    private static string FormatBytes(ReadOnlySpan<byte> bytes)
    {
        var hex = string.Join(' ', bytes.ToArray().Select(x => $"{x:X2}"));
        var ascii = new string(bytes.ToArray().Select(x => x is >= 32 and <= 126 ? (char)x : '.').ToArray());
        return $"{hex}  |{ascii}|";
    }
}
