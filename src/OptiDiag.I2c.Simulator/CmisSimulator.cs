using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OptiDiag.I2c.Abstractions;

namespace OptiDiag.I2c.Simulator;

public sealed class CmisSimulator : II2cAdapter
{
    private readonly byte[] _lower;
    private readonly Dictionary<(byte Bank, byte Page), byte[]> _pages;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private byte _selectedBank;
    private byte _selectedPage;
    private bool _disposed;

    public CmisSimulator()
    {
        (_lower, _pages) = CmisSimulatorMemoryFactory.CreateSample();
    }

    public I2cAdapterInfo Info => new(
        "sim-cmis53-01",
        "CMIS 5.3 QSFP-DD 模拟模块",
        "OptiDiag Simulator",
        MaximumReadLength: 128,
        MaximumWriteLength: 128);

    public bool IsOpen { get; private set; }

    public event EventHandler<I2cTraceEntry>? TransferCompleted;

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        IsOpen = true;
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsOpen = false;
        return Task.CompletedTask;
    }

    public async Task<I2cTransferResult> TransferAsync(
        I2cTransfer transfer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsOpen)
        {
            throw new InvalidOperationException("CMIS 模拟适配器尚未连接。");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();
        byte[] readBuffer = [];
        try
        {
            await Task.Delay(3, cancellationToken).ConfigureAwait(false);
            if (transfer.DeviceAddress != 0x50)
            {
                throw new IOException($"CMIS 模拟模块只响应 7 位地址 0x50，0x{transfer.DeviceAddress:X2} NACK。");
            }

            if (transfer.ReadLength > 0)
            {
                if (transfer.WriteBuffer.Length != 1)
                {
                    throw new IOException("寄存器读取必须先写入一个字节的偏移地址。");
                }

                UpdateDynamicMonitors();
                readBuffer = ReadMemory(transfer.WriteBuffer.Span[0], transfer.ReadLength);
            }
            else if (transfer.WriteBuffer.Length >= 2)
            {
                WriteMemory(transfer.WriteBuffer.Span[0], transfer.WriteBuffer.Span[1..]);
            }
            else
            {
                throw new IOException("写事务必须包含偏移和至少一个数据字节。");
            }

            stopwatch.Stop();
            RaiseTrace(transfer, readBuffer, stopwatch.Elapsed, null);
            return new I2cTransferResult(readBuffer, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RaiseTrace(transfer, readBuffer, stopwatch.Elapsed, ex);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private byte[] ReadMemory(byte offset, int length)
    {
        if (offset + length > 256)
        {
            throw new IOException("读取越过 256 字节地址边界。");
        }

        var result = new byte[length];
        for (var index = 0; index < length; index++)
        {
            var location = offset + index;
            result[index] = location < 128
                ? _lower[location]
                : GetSelectedPage()[location - 128];
        }

        return result;
    }

    private void WriteMemory(byte offset, ReadOnlySpan<byte> data)
    {
        if (offset + data.Length > 256)
        {
            throw new IOException("写入越过 256 字节地址边界。");
        }

        if (offset == 126 && data.Length >= 2)
        {
            ApplySelection(data[0], data[1]);
            data = data[2..];
            offset = 128;
        }

        for (var index = 0; index < data.Length; index++)
        {
            var location = offset + index;
            if (location < 126)
            {
                _lower[location] = data[index];
            }
            else if (location == 126)
            {
                ApplySelection(data[index], _selectedPage);
            }
            else if (location == 127)
            {
                ApplySelection(_selectedBank, data[index]);
            }
            else
            {
                GetSelectedPage()[location - 128] = data[index];
            }
        }
    }

    private void ApplySelection(byte bank, byte page)
    {
        var isBanked = page is 0x10 or 0x11;
        var key = (isBanked ? bank : (byte)0, page);
        if (!_pages.ContainsKey(key))
        {
            _selectedBank = 0;
            _selectedPage = 0;
        }
        else
        {
            _selectedBank = bank;
            _selectedPage = page;
        }

        _lower[126] = _selectedBank;
        _lower[127] = _selectedPage;
    }

    private byte[] GetSelectedPage()
    {
        var key = (_selectedPage is 0x10 or 0x11 ? _selectedBank : (byte)0, _selectedPage);
        return _pages.TryGetValue(key, out var page) ? page : _pages[(0, 0)];
    }

    private void UpdateDynamicMonitors()
    {
        var seconds = _uptime.Elapsed.TotalSeconds;
        var temperature = 37.0 + Math.Sin(seconds / 8.0) * 2.0;
        var voltage = 3.300 + Math.Sin(seconds / 11.0) * 0.012;
        CmisSimulatorMemoryFactory.WriteSigned(
            _lower,
            14,
            (short)Math.Round(temperature * 256));
        CmisSimulatorMemoryFactory.WriteUnsigned(
            _lower,
            16,
            (ushort)Math.Round(voltage / 0.0001));

        var page11 = _pages[(0, 0x11)];
        for (var lane = 0; lane < 8; lane++)
        {
            var phase = seconds / (5.5 + lane * 0.35);
            var txPower = 0.72 + lane * 0.018 + Math.Sin(phase) * 0.035;
            var bias = 6.1 + lane * 0.12 + Math.Sin(phase * 0.8) * 0.22;
            var rxPower = 0.55 + lane * 0.015 + Math.Sin(phase * 1.15) * 0.045;
            CmisSimulatorMemoryFactory.WriteUnsigned(
                page11,
                26 + lane * 2,
                (ushort)Math.Round(txPower / 0.0001));
            CmisSimulatorMemoryFactory.WriteUnsigned(
                page11,
                42 + lane * 2,
                (ushort)Math.Round(bias / 0.002));
            CmisSimulatorMemoryFactory.WriteUnsigned(
                page11,
                58 + lane * 2,
                (ushort)Math.Round(rxPower / 0.0001));
        }
    }

    private void RaiseTrace(
        I2cTransfer transfer,
        byte[] readBuffer,
        TimeSpan elapsed,
        Exception? exception) =>
        TransferCompleted?.Invoke(this, new I2cTraceEntry(
            DateTimeOffset.Now,
            transfer.DeviceAddress,
            transfer.WriteBuffer.ToArray(),
            readBuffer,
            elapsed,
            exception is null,
            exception?.Message));

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await CloseAsync().ConfigureAwait(false);
        _gate.Dispose();
        _disposed = true;
    }
}

internal static class CmisSimulatorMemoryFactory
{
    public static (byte[] Lower, Dictionary<(byte Bank, byte Page), byte[]> Pages) CreateSample()
    {
        var lower = new byte[128];
        var page00 = new byte[128];
        var page01 = new byte[128];
        var page02 = new byte[128];
        var page03 = new byte[128];
        var page10 = new byte[128];
        var page11 = new byte[128];

        lower[0] = 0x18; // QSFP-DD
        lower[1] = 0x53; // CMIS 5.3
        lower[2] = 0x00; // Paged memory, normal configuration.
        lower[3] = 0x07; // ModuleReady, interrupt deasserted.
        lower[39] = 2;
        lower[40] = 7;
        lower[56] = 0x04; // MSM + DPSM + NPSM.
        lower[57] = 0x00; // Transmission module.
        lower[60] = 0x00;
        lower[61] = 0x01;
        lower[85] = 0x02; // Single-mode optical.
        lower[86] = 0x11;
        lower[87] = 0x4A;
        lower[88] = 0x88;
        lower[89] = 0xFF;
        lower[90] = 0xFF; // End of application descriptors.

        page00[0] = lower[0];
        WriteAscii(page00, 1, 16, "OPTIDIAG LAB");
        page00[17] = 0x00;
        page00[18] = 0x1B;
        page00[19] = 0x21;
        WriteAscii(page00, 20, 16, "OD-QDD-FR4-400G");
        WriteAscii(page00, 36, 2, "B2");
        WriteAscii(page00, 38, 16, "CMIS26073100001");
        WriteAscii(page00, 54, 8, "260731A1");
        WriteAscii(page00, 62, 10, "IPRVMOCK01");
        page00[72] = 0x60; // Power class 3.
        page00[73] = 48;   // 12 W.
        page00[74] = 20;   // 20 m.
        page00[75] = 0x0D; // MPO 2x16.
        page00[84] = 0x06; // 1310 nm EML.
        page00[94] = Checksum(page00.AsSpan(0, 94));

        page01[0] = 2;
        page01[1] = 6;
        page01[2] = 1;
        page01[3] = 1;
        page01[4] = 10;
        page01[5] = 35;
        page01[6] = 25;
        page01[7] = 10;
        page01[8] = 20;
        page01[9] = 1;
        WriteUnsigned(page01, 10, (ushort)Math.Round(1310 / 0.05));
        WriteUnsigned(page01, 12, (ushort)Math.Round(6 / 0.005));
        page01[14] = 0x04; // Page 03h; one bank.
        page01[18] = 85;
        page01[19] = unchecked((byte)-5);
        WriteUnsigned(page01, 20, 250); // 2.5 us propagation delay.
        page01[31] = 0x03; // Temperature and Vcc.
        page01[32] = 0x07; // Bias, Tx power and Rx power.
        page01[127] = Checksum(page01.AsSpan(2, 125));

        WriteThresholdSet(page02, 0, 85, -10, 75, -5, value => (ushort)(short)Math.Round(value * 256));
        WriteThresholdSet(page02, 8, 3.60, 3.00, 3.50, 3.10, value => (ushort)Math.Round(value / 0.0001));
        WriteThresholdSet(page02, 48, 1.60, 0.12, 1.40, 0.20, value => (ushort)Math.Round(value / 0.0001));
        WriteThresholdSet(page02, 56, 12.0, 1.0, 10.0, 2.0, value => (ushort)Math.Round(value / 0.002));
        WriteThresholdSet(page02, 64, 1.20, 0.06, 1.00, 0.12, value => (ushort)Math.Round(value / 0.0001));
        page02[127] = Checksum(page02.AsSpan(0, 127));

        WriteAscii(page03, 0, 64, "OptiDiag CMIS user EEPROM - writable");

        page11[0] = 0x77;
        page11[1] = 0x77;
        page11[2] = 0x77;
        page11[3] = 0x77;
        page11[4] = 0xFF;
        page11[5] = 0xFF;
        page11[74] = 0x11;
        page11[75] = 0x11;
        page11[76] = 0x11;
        page11[77] = 0x11;

        return (lower, new Dictionary<(byte Bank, byte Page), byte[]>
        {
            [(0, 0x00)] = page00,
            [(0, 0x01)] = page01,
            [(0, 0x02)] = page02,
            [(0, 0x03)] = page03,
            [(0, 0x10)] = page10,
            [(0, 0x11)] = page11
        });
    }

    private static void WriteThresholdSet(
        byte[] target,
        int offset,
        double highAlarm,
        double lowAlarm,
        double highWarning,
        double lowWarning,
        Func<double, ushort> convert)
    {
        WriteUnsigned(target, offset, convert(highAlarm));
        WriteUnsigned(target, offset + 2, convert(lowAlarm));
        WriteUnsigned(target, offset + 4, convert(highWarning));
        WriteUnsigned(target, offset + 6, convert(lowWarning));
    }

    internal static void WriteUnsigned(byte[] target, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16BigEndian(target.AsSpan(offset, 2), value);

    internal static void WriteSigned(byte[] target, int offset, short value) =>
        BinaryPrimitives.WriteInt16BigEndian(target.AsSpan(offset, 2), value);

    private static void WriteAscii(byte[] target, int offset, int length, string value)
    {
        target.AsSpan(offset, length).Fill(0x20);
        Encoding.ASCII.GetBytes(value.AsSpan(0, Math.Min(length, value.Length)), target.AsSpan(offset, length));
    }

    private static byte Checksum(ReadOnlySpan<byte> data)
    {
        var sum = 0;
        foreach (var value in data)
        {
            sum += value;
        }

        return (byte)sum;
    }
}
