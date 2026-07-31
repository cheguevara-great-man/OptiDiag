using System.Diagnostics;

namespace OptiDiag.I2c.Abstractions;

public sealed record I2cAdapterInfo(
    string Id,
    string DisplayName,
    string Provider,
    int MaximumReadLength = 128,
    int MaximumWriteLength = 128,
    bool SupportsRepeatedStart = true);

public sealed record I2cTransfer
{
    public I2cTransfer(byte deviceAddress, ReadOnlyMemory<byte> writeBuffer, int readLength = 0)
    {
        if (deviceAddress > 0x7F)
        {
            throw new ArgumentOutOfRangeException(nameof(deviceAddress), "I2C 地址必须使用 7 位格式（0x00-0x7F）。");
        }

        if (readLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(readLength));
        }

        DeviceAddress = deviceAddress;
        WriteBuffer = writeBuffer;
        ReadLength = readLength;
    }

    public byte DeviceAddress { get; }

    public ReadOnlyMemory<byte> WriteBuffer { get; }

    public int ReadLength { get; }
}

public sealed record I2cTransferResult(ReadOnlyMemory<byte> ReadBuffer, TimeSpan Elapsed);

public sealed record I2cTraceEntry(
    DateTimeOffset Timestamp,
    byte DeviceAddress,
    byte[] WriteBuffer,
    byte[] ReadBuffer,
    TimeSpan Elapsed,
    bool Success,
    string? ErrorMessage = null)
{
    public string Operation => ReadBuffer.Length > 0
        ? (WriteBuffer.Length > 0 ? "写/读" : "读")
        : "写";
}

/// <summary>
/// 与具体 USB、HID、串口或厂商 SDK 无关的 I2C 适配器边界。
/// 地址始终采用 7 位表示。
/// </summary>
public interface II2cAdapter : IAsyncDisposable
{
    I2cAdapterInfo Info { get; }

    bool IsOpen { get; }

    event EventHandler<I2cTraceEntry>? TransferCompleted;

    Task OpenAsync(CancellationToken cancellationToken = default);

    Task CloseAsync(CancellationToken cancellationToken = default);

    Task<I2cTransferResult> TransferAsync(I2cTransfer transfer, CancellationToken cancellationToken = default);
}

public static class I2cAdapterExtensions
{
    public static async Task<byte[]> ReadRegistersAsync(
        this II2cAdapter adapter,
        byte address,
        byte offset,
        int length,
        CancellationToken cancellationToken = default)
    {
        var result = await adapter.TransferAsync(
            new I2cTransfer(address, new[] { offset }, length), cancellationToken).ConfigureAwait(false);
        return result.ReadBuffer.ToArray();
    }

    public static Task<I2cTransferResult> WriteRegistersAsync(
        this II2cAdapter adapter,
        byte address,
        byte offset,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[data.Length + 1];
        buffer[0] = offset;
        data.CopyTo(buffer.AsMemory(1));
        return adapter.TransferAsync(new I2cTransfer(address, buffer), cancellationToken);
    }

    internal static I2cTraceEntry CreateTrace(
        I2cTransfer transfer,
        ReadOnlyMemory<byte> readBuffer,
        Stopwatch stopwatch,
        Exception? exception = null) =>
        new(
            DateTimeOffset.Now,
            transfer.DeviceAddress,
            transfer.WriteBuffer.ToArray(),
            readBuffer.ToArray(),
            stopwatch.Elapsed,
            exception is null,
            exception?.Message);
}
