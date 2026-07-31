using OptiDiag.I2c.Abstractions;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Application;

public sealed record ProtocolDetectionResult(
    byte Identifier,
    string ProtocolId,
    string ProtocolName,
    string Evidence,
    IReadOnlyList<ProtocolExtensionInfo> Extensions)
{
    public bool IsSupportedBy(string protocolId) =>
        string.Equals(ProtocolId, protocolId, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 只读取协议识别所需的最小内存，不依赖任何具体 USB-I2C 硬件。
/// 新协议加入后在此登记探测规则，并由各协议解码器负责完整捕获。
/// </summary>
public sealed class ProtocolDetectionService
{
    public async Task<ProtocolDetectionResult> DetectAsync(
        II2cAdapter adapter,
        CancellationToken cancellationToken = default)
    {
        if (!adapter.IsOpen)
        {
            throw new InvalidOperationException("协议探测前必须先打开 I²C 适配器。");
        }

        var header = await adapter.ReadRegistersAsync(0x50, 0, 96, cancellationToken).ConfigureAwait(false);
        if (header.Length < 96)
        {
            throw new InvalidDataException($"协议探测需要 96 字节，实际只读取到 {header.Length} 字节。");
        }

        var identifier = header[0];
        return identifier switch
        {
            0x02 or 0x03 => DetectSff8472(header),
            0x0D or 0x11 => DetectQsfp(header),
            0x18 or 0x19 or 0x1E or 0x1F or 0x20 or 0x21 or 0x22 or 0x26 =>
                new ProtocolDetectionResult(
                    identifier,
                    "cmis",
                    "CMIS",
                    $"Identifier=0x{identifier:X2} 指向采用 CMIS 的模块类型。",
                    []),
            _ => new ProtocolDetectionResult(
                identifier,
                "unknown",
                "未知管理接口",
                $"Identifier=0x{identifier:X2}，当前探测规则无法确定协议。",
                [])
        };
    }

    private static ProtocolDetectionResult DetectSff8472(byte[] header)
    {
        var tunable = (header[65] & 0x40) != 0;
        return new ProtocolDetectionResult(
            header[0],
            "sff-8472",
            "SFF-8472",
            $"Identifier=0x{header[0]:X2}；A0h.94=0x{header[94]:X2}。",
            [
                new ProtocolExtensionInfo(
                    "sff-8690",
                    "SFF-8690 可调谐扩展",
                    "1.5",
                    tunable,
                    tunable
                        ? "A0h.65.6=1，模块声明可调谐发射器。"
                        : "A0h.65.6=0，模块未声明 SFF-8690。")
            ]);
    }

    private static ProtocolDetectionResult DetectQsfp(byte[] header)
    {
        var revision = header[1];
        var legacy = revision <= 0x01;
        return new ProtocolDetectionResult(
            header[0],
            legacy ? "sff-8436" : "sff-8636",
            legacy ? "SFF-8436" : "SFF-8636",
            $"Identifier=0x{header[0]:X2}；Revision Compliance=0x{revision:X2}。",
            []);
    }
}
