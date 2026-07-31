using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OptiDiag.I2c.Abstractions;

namespace OptiDiag.I2c.Simulator;

public sealed class Sff8472Simulator : II2cAdapter
{
    private readonly byte[] _a0;
    private readonly byte[] _a2Lower;
    private readonly Dictionary<byte, byte[]> _a2Pages;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private byte _selectedPage;
    private bool _disposed;

    public Sff8472Simulator(
        bool enableRemotePerformanceMonitoring = false,
        bool enableSff8690 = false)
    {
        (_a0, _a2Lower, _a2Pages) = SimulatorMemoryFactory.CreateSff8472Sample();
        SetRemotePerformanceMonitoringEnabledCore(enableRemotePerformanceMonitoring);
        SetSff8690EnabledCore(enableSff8690);
    }

    public I2cAdapterInfo Info => new(
        "sim-sff8472-01",
        $"{(Sff8690Enabled ? "SFF-8472 + SFF-8690 可调谐" : "SFF-8472 标准")}模拟模块"
        + (RemotePerformanceMonitoringEnabled ? "（含 RPM 远端）" : string.Empty),
        "OptiDiag Simulator",
        MaximumReadLength: 128,
        MaximumWriteLength: 128);

    public bool Sff8690Enabled { get; private set; }

    public bool RemotePerformanceMonitoringEnabled { get; private set; }

    public bool IsOpen { get; private set; }

    public event EventHandler<I2cTraceEntry>? TransferCompleted;

    public void SetSff8690Enabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _gate.Wait();
        try
        {
            SetSff8690EnabledCore(enabled);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void SetRemotePerformanceMonitoringEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _gate.Wait();
        try
        {
            SetRemotePerformanceMonitoringEnabledCore(enabled);
        }
        finally
        {
            _gate.Release();
        }
    }

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
            throw new InvalidOperationException("模拟适配器尚未连接。");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();
        byte[] readBuffer = [];
        try
        {
            await Task.Delay(4, cancellationToken).ConfigureAwait(false);
            if (transfer.DeviceAddress is not (0x50 or 0x51))
            {
                throw new IOException($"地址 0x{transfer.DeviceAddress:X2} 无应答（NACK）。");
            }

            if (transfer.ReadLength > 0)
            {
                if (transfer.WriteBuffer.Length != 1)
                {
                    throw new IOException("寄存器读取必须先写入一个字节的偏移地址。");
                }

                UpdateDynamicDiagnostics();
                var offset = transfer.WriteBuffer.Span[0];
                readBuffer = ReadMemory(transfer.DeviceAddress, offset, transfer.ReadLength);
            }
            else if (transfer.WriteBuffer.Length >= 2)
            {
                var span = transfer.WriteBuffer.Span;
                WriteMemory(transfer.DeviceAddress, span[0], span[1..]);
            }
            else
            {
                throw new IOException("写事务必须包含寄存器偏移和至少一个数据字节。");
            }

            stopwatch.Stop();
            TransferCompleted?.Invoke(this, new I2cTraceEntry(
                DateTimeOffset.Now,
                transfer.DeviceAddress,
                transfer.WriteBuffer.ToArray(),
                readBuffer,
                stopwatch.Elapsed,
                true));
            return new I2cTransferResult(readBuffer, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            TransferCompleted?.Invoke(this, new I2cTraceEntry(
                DateTimeOffset.Now,
                transfer.DeviceAddress,
                transfer.WriteBuffer.ToArray(),
                readBuffer,
                stopwatch.Elapsed,
                false,
                ex.Message));
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private byte[] ReadMemory(byte address, byte offset, int length)
    {
        var result = new byte[length];
        for (var index = 0; index < length; index++)
        {
            var location = offset + index;
            if (location > byte.MaxValue)
            {
                throw new IOException("读取越过了 256 字节地址边界。");
            }

            result[index] = address == 0x50
                ? _a0[location]
                : location < 128
                    ? _a2Lower[location]
                    : GetSelectedPage()[location - 128];
        }

        return result;
    }

    private void WriteMemory(byte address, byte offset, ReadOnlySpan<byte> data)
    {
        for (var index = 0; index < data.Length; index++)
        {
            var location = offset + index;
            if (location > byte.MaxValue)
            {
                throw new IOException("写入越过了 256 字节地址边界。");
            }

            if (address == 0x50)
            {
                _a0[location] = data[index];
            }
            else if (location < 128)
            {
                _a2Lower[location] = data[index];
                if (location == 127)
                {
                    _selectedPage = _a2Pages.ContainsKey(data[index]) ? data[index] : (byte)0;
                    _a2Lower[location] = _selectedPage;
                }
            }
            else
            {
                GetSelectedPage()[location - 128] = data[index];
            }
        }
    }

    private byte[] GetSelectedPage()
    {
        if (!_a2Pages.TryGetValue(_selectedPage, out var page))
        {
            page = new byte[128];
            _a2Pages[_selectedPage] = page;
        }

        return page;
    }

    private void SetRemotePerformanceMonitoringEnabledCore(bool enabled)
    {
        RemotePerformanceMonitoringEnabled = enabled;
        if (!enabled)
        {
            _a2Pages[0x02][1] &= 0xFB;
            return;
        }

        _a2Pages[0x02][1] |= 0x04; // Page 02h byte 129 bit 2: RPM supported.
        _a2Pages[0x20] = _a0[..128].ToArray();
        _a2Pages[0x21] = _a0[128..].ToArray();
        _a2Pages[0x22] = _a2Lower.ToArray();
        _a2Pages[0x23] = _a2Pages[0x00].ToArray();
        _a2Pages[0x24] = _a2Pages[0x02].ToArray();
        _a2Pages[0x25] = new byte[128];
        _a2Pages[0x26] = new byte[128];
        _a2Pages[0x27] = new byte[128];
    }

    private void SetSff8690EnabledCore(bool enabled)
    {
        Sff8690Enabled = enabled;
        var page = _a2Pages[0x02];
        page[0] = 0;
        page.AsSpan(4, 42).Clear(); // Absolute bytes 132-173, all SFF-8690 fields.
        if (enabled)
        {
            _a0[65] |= 0x40;
            page[0] = 0b0000_1111; // Self tuning, dither, channel and wavelength selection.
            SimulatorMemoryFactory.WriteUnsigned(page, 4, 191);    // First frequency: 191.3000 THz.
            SimulatorMemoryFactory.WriteUnsigned(page, 6, 3000);
            SimulatorMemoryFactory.WriteUnsigned(page, 8, 196);    // Last frequency: 196.1000 THz.
            SimulatorMemoryFactory.WriteUnsigned(page, 10, 1000);
            SimulatorMemoryFactory.WriteSigned(page, 12, 500);     // Grid: 50.0 GHz.
            SimulatorMemoryFactory.WriteUnsigned(page, 16, 45);    // Current channel.
            SimulatorMemoryFactory.WriteUnsigned(page, 18, 31002); // 1550.10 nm / 0.05 nm.
            page[23] = 0b0000_0010;          // Self tuning enabled; dither enabled (active low).
            SimulatorMemoryFactory.WriteSigned(page, 24, -2);       // -0.2 GHz.
            SimulatorMemoryFactory.WriteSigned(page, 26, 2);        // +0.010 nm.
            page[40] = 0x00;                 // Self tuning idle/locked.
            page[44] = 0x08;                 // New channel latched.
        }
        else
        {
            _a0[65] &= 0xBF;
        }

        _a0[95] = SimulatorMemoryFactory.ComputeChecksum(_a0.AsSpan(64, 31));
    }

    private void UpdateDynamicDiagnostics()
    {
        var seconds = _uptime.Elapsed.TotalSeconds;
        var temperature = 31.5 + Math.Sin(seconds / 7.0) * 2.5;
        var voltage = 3.300 + Math.Sin(seconds / 11.0) * 0.018;
        var biasMa = 6.20 + Math.Sin(seconds / 5.0) * 0.30;
        var txMilliwatts = 0.82 + Math.Sin(seconds / 9.0) * 0.06;
        var rxMilliwatts = 0.61 + Math.Sin(seconds / 6.0) * 0.08;

        SimulatorMemoryFactory.WriteSigned(_a2Lower, 96, (short)Math.Round(temperature * 256));
        SimulatorMemoryFactory.WriteUnsigned(_a2Lower, 98, (ushort)Math.Round(voltage / 0.0001));
        SimulatorMemoryFactory.WriteUnsigned(_a2Lower, 100, (ushort)Math.Round(biasMa / 0.002));
        SimulatorMemoryFactory.WriteUnsigned(_a2Lower, 102, (ushort)Math.Round(txMilliwatts / 0.0001));
        SimulatorMemoryFactory.WriteUnsigned(_a2Lower, 104, (ushort)Math.Round(rxMilliwatts / 0.0001));
    }

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

public static class SimulatorMemoryFactory
{
    public static (byte[] A0, byte[] A2Lower, Dictionary<byte, byte[]> A2Pages) CreateSff8472Sample()
    {
        var a0 = new byte[256];
        var a2 = new byte[128];

        a0[0] = 0x03; // SFP/SFP+
        a0[1] = 0x04;
        a0[2] = 0x07; // LC
        a0[3] = 0x10; // 10G Ethernet family
        a0[6] = 0x10;
        a0[11] = 0x06;
        a0[12] = 103; // 10.3 GBd
        a0[13] = 0x00;
        a0[14] = 10;
        WriteAscii(a0, 20, 16, "OPTIDIAG LAB");
        a0[37] = 0x00;
        a0[38] = 0x1B;
        a0[39] = 0x21;
        WriteAscii(a0, 40, 16, "OD-SFP-LR-10G");
        WriteAscii(a0, 56, 4, "A1");
        WriteUnsigned(a0, 60, 1310);
        a0[64] = 0x00;
        a0[65] = 0x1B;
        WriteAscii(a0, 68, 16, "SIM260730000001");
        WriteAscii(a0, 84, 6, "260730");
        WriteAscii(a0, 90, 2, "01");
        a0[92] = 0x68; // DDM、内部校准、平均接收功率
        a0[93] = 0xF0;
        a0[94] = 0x0A; // SFF-8472 12.5 family
        a0[63] = ComputeChecksum(a0.AsSpan(0, 63));
        a0[95] = ComputeChecksum(a0.AsSpan(64, 31));
        WriteAscii(a0, 96, 32, "OptiDiag simulator - editable");

        WriteThresholdSet(a2, 0, 85, 75, -5, -10, value => (ushort)(short)Math.Round(value * 256));
        WriteThresholdSet(a2, 8, 3.60, 3.50, 3.10, 3.00, value => (ushort)Math.Round(value / 0.0001));
        WriteThresholdSet(a2, 16, 12.0, 10.0, 2.0, 1.0, value => (ushort)Math.Round(value / 0.002));
        WriteThresholdSet(a2, 24, 1.60, 1.40, 0.25, 0.15, value => (ushort)Math.Round(value / 0.0001));
        WriteThresholdSet(a2, 32, 1.20, 1.00, 0.12, 0.06, value => (ushort)Math.Round(value / 0.0001));
        a2[95] = ComputeChecksum(a2.AsSpan(0, 95));
        a2[110] = 0x00;
        a2[111] = 0x00;
        a2[127] = 0x00;

        var page0 = new byte[128];
        WriteAscii(page0, 0, 64, "OptiDiag SFF-8472 Page 00h simulation data");

        var page2 = new byte[128];
        page2[0] = 0x01;
        page2[1] = 0x00;
        WriteAscii(page2, 4, 32, "RDT/RPM features not enabled");

        var page3 = new byte[128];
        page3[0] = 0xCA; // CA1Bh: optical-module timing calibration format
        page3[1] = 0x1B;
        page3[2] = 0x01;
        page3[3] = 26;   // Year = 2026.
        page3[4] = 0x7E; // July; upper four bits of (day - 1).
        page3[5] = 0x81; // Day-code LSB=1 (30th); calibration sequence 1.
        page3[6] = 0x00;
        page3[7] = 0x1B;
        page3[8] = 0x21;
        page3[9] = 0x00;
        page3[10] = 0x00;
        page3[11] = 0x01;
        page3[12] = 0;   // Highest precision stratum.
        page3[22] = 1;   // Nb_Lanes.
        page3[23] = 0;   // Single operational mode.
        WriteSigned24(page3, 24, 32768); // Rx_Pwr_Dly(0) = 0.5 ns.
        WriteSigned24(page3, 27, 655);   // Rx_Pwr_Dly(1) ~= 0.01 ns/dBm.
        WriteQ16_16(page3, 43, 0.50);    // Delta_Rx_Max.
        WriteQ16_16(page3, 47, 0.40);    // Delta_Tx_Max.
        WriteQ16_16(page3, 51, 25.25);   // Avg_Rx_Lane1.
        WriteQ16_16(page3, 55, 21.75);   // Avg_Tx_Lane1.
        page3[127] = ComputeChecksum(page3.AsSpan(0, 127));

        return (a0, a2, new Dictionary<byte, byte[]>
        {
            [0x00] = page0,
            [0x01] = page0,
            [0x02] = page2,
            [0x03] = page3
        });
    }

    private static void WriteThresholdSet(
        byte[] target,
        int offset,
        double highAlarm,
        double highWarning,
        double lowWarning,
        double lowAlarm,
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

    private static void WriteSigned24(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value >> 16);
        target[offset + 1] = (byte)(value >> 8);
        target[offset + 2] = (byte)value;
    }

    private static void WriteQ16_16(byte[] target, int offset, double value) =>
        BinaryPrimitives.WriteUInt32BigEndian(
            target.AsSpan(offset, 4),
            checked((uint)Math.Round(value * 65536)));

    private static void WriteAscii(byte[] target, int offset, int length, string value)
    {
        target.AsSpan(offset, length).Fill(0x20);
        Encoding.ASCII.GetBytes(value.AsSpan(0, Math.Min(value.Length, length)), target.AsSpan(offset, length));
    }

    internal static byte ComputeChecksum(ReadOnlySpan<byte> data)
    {
        var sum = 0;
        foreach (var value in data)
        {
            sum += value;
        }

        return (byte)(sum & 0xFF);
    }
}
