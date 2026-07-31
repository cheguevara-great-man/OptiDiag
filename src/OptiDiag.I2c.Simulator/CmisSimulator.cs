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

        var triggerCdb = _selectedPage == 0x9F
            && offset <= 129
            && offset + data.Length > 129;
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

        if (triggerCdb)
        {
            ProcessCdbCommand();
        }
    }

    private void ProcessCdbCommand()
    {
        var page = GetSelectedPage();
        var localLength = page[4];
        var sum = page[0] + page[1] + page[2] + page[3] + page[4];
        for (var index = 0; index < Math.Min(localLength, (byte)120); index++)
        {
            sum += page[8 + index];
        }

        _lower[8] &= 0xBF;
        _lower[37] = 0x02; // Busy checking/validating.
        if (localLength > 120 || page[5] != unchecked((byte)~sum))
        {
            _lower[37] = 0x45; // Failed: CdbChkCode error.
            page[6] = 0;
            page[7] = 0;
            _lower[8] |= 0x40;
            return;
        }

        var commandId = BinaryPrimitives.ReadUInt16BigEndian(page.AsSpan(0, 2));
        byte[] reply = commandId switch
        {
            0x0000 => [2, 1], // Length + Host Password Accepted.
            >= 0x0040 and <= 0x0045 => [2, 0x01], // Minimal supported-feature reply.
            _ => [] // Commands not semantically simulated complete with an empty reply.
        };
        page[6] = (byte)reply.Length;
        page[7] = ReplyCheckCode(reply);
        reply.CopyTo(page, 8);
        _lower[37] = 0x01; // Success.
        _lower[8] |= 0x40;
    }

    private static byte ReplyCheckCode(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            return 0;
        }

        var sum = 0;
        foreach (var value in payload)
        {
            sum += value;
        }

        return unchecked((byte)~sum);
    }

    private void ApplySelection(byte bank, byte page)
    {
        var key = _pages.ContainsKey((bank, page)) ? (bank, page) : ((byte)0, page);
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
        var key = _pages.ContainsKey((_selectedBank, _selectedPage))
            ? (_selectedBank, _selectedPage)
            : ((byte)0, _selectedPage);
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
        var page04 = new byte[128];
        var page10 = new byte[128];
        var page11 = new byte[128];
        var page12 = new byte[128];
        var page13 = new byte[128];
        var page14 = new byte[128];
        var page15 = new byte[128];
        var page16 = new byte[128];
        var page17 = new byte[128];
        var page18 = new byte[128];
        var page19 = new byte[128];
        var page1C = new byte[128];
        var page1D = new byte[128];
        var page20 = new byte[128];
        var page24 = new byte[128];
        var page28 = new byte[128];
        var page2C = new byte[128];
        var page2D = new byte[128];
        var page2F = new byte[128];
        var page9F = new byte[128];
        var pageA0 = new byte[128];

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
        page01[14] = 0xE4; // Network path, VDM, diagnostics, Page 03h; one lane bank.
        page01[17] = 0x08; // Page 15h timing characteristics supported.
        page01[18] = 85;
        page01[19] = unchecked((byte)-5);
        WriteUnsigned(page01, 20, 250); // 2.5 us propagation delay.
        page01[31] = 0x03; // Temperature and Vcc.
        page01[32] = 0x07; // Bias, Tx power and Rx power.
        page01[27] = 0xC0; // Tunable laser controls and Page 04h.
        page01[34] = 0x40; // Direction-independent Tx/Rx configuration extension.
        page01[35] = 0x71; // One CDB instance, background + auto paging, one EPL page.
        page01[47] = 0x01; // One NAD block on Page 1Ch.
        page01[124] = 0x80; // Host lane switching.
        page01[127] = Checksum(page01.AsSpan(2, 125));

        WriteThresholdSet(page02, 0, 85, -10, 75, -5, value => (ushort)(short)Math.Round(value * 256));
        WriteThresholdSet(page02, 8, 3.60, 3.00, 3.50, 3.10, value => (ushort)Math.Round(value / 0.0001));
        WriteThresholdSet(page02, 48, 1.60, 0.12, 1.40, 0.20, value => (ushort)Math.Round(value / 0.0001));
        WriteThresholdSet(page02, 56, 12.0, 1.0, 10.0, 2.0, value => (ushort)Math.Round(value / 0.002));
        WriteThresholdSet(page02, 64, 1.20, 0.06, 1.00, 0.12, value => (ushort)Math.Round(value / 0.0001));
        page02[127] = Checksum(page02.AsSpan(0, 127));

        WriteAscii(page03, 0, 64, "OptiDiag CMIS user EEPROM - writable");

        // Page 04h: one 50 GHz grid covering channel numbers -64 to +64.
        page04[0] = 0x10;
        WriteSigned(page04, 18, -64);
        WriteSigned(page04, 20, 64);
        page04[1] = 0x80;
        WriteUnsigned(page04, 62, 1);
        WriteSigned(page04, 64, -5000);
        WriteSigned(page04, 66, 5000);
        page04[68] = 0x80;
        WriteSigned(page04, 70, -1000);
        WriteSigned(page04, 72, 300);
        page04[127] = Checksum(page04.AsSpan(0, 127));

        // Page 10h/11h: direct controls, staged control set, initialized lanes and mapping.
        for (var lane = 0; lane < 8; lane++)
        {
            page10[17 + lane] = 0x11; // AppSel 1, DataPath 0, explicit control.
            page10[52 + lane] = 0x11;
            page11[112 + lane] = (byte)((lane << 4) | lane);
            page11[120 + lane] = (byte)((lane << 4) | lane);
        }

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

        // Page 12h: tunable controls/status at 193.1 THz.
        for (var lane = 0; lane < 8; lane++)
        {
            page12[lane] = 0x40; // 50 GHz grid.
            WriteSigned(page12, 8 + lane * 2, (short)lane);
            WriteSigned(page12, 24 + lane * 2, 0);
            WriteUnsigned32(page12, 40 + lane * 4, (uint)(193_100_000 + lane * 50_000));
            WriteSigned(page12, 72 + lane * 2, -100);
        }

        // Page 13h/14h: diagnostics with BER results.
        page13[0] = 0x7F;
        page13[1] = 0xFC;
        page13[2] = 0xF2;
        page13[3] = 0xFF;
        page13[4] = 0xFF;
        page13[5] = 0xFF;
        page13[49] = 0x14;
        page14[0] = 0x01;
        for (var lane = 0; lane < 16; lane++)
        {
            WriteHalf(page14, 64 + lane * 2, 1.0e-6f * (lane + 1));
        }

        // Page 15h: per-lane Rx and Tx latency.
        for (var lane = 0; lane < 8; lane++)
        {
            WriteUnsigned(page15, 96 + lane * 2, (ushort)(80 + lane));
            WriteUnsigned(page15, 112 + lane * 2, (ushort)(95 + lane));
        }

        // Page 16h/17h: Network Path controls, states, advertisements and masks.
        for (var lane = 0; lane < 8; lane++)
        {
            page16[lane] = (byte)((lane << 1) | 1);
            page16[8 + lane] = (byte)((lane << 1) | 1);
            page16[64 + lane] = (byte)((lane << 1) | 1);
            if (lane % 2 == 0)
            {
                page16[50 + lane / 2] = 0x11;
                page16[72 + lane / 2] = 0x22;
            }
        }
        page16[96] = 0x44;
        page16[97] = 0x33;
        page16[98] = 0x03;
        page17[64] = 0xFF;

        // Page 18h/19h and Page 1Ch: NAD base fields (VCS areas stay zero/raw).
        for (var lane = 0; lane < 8; lane++)
        {
            page18[lane] = 0;
            page18[8 + lane] = 0;
            page19[lane] = 0x11;
            page19[8 + lane] = 0x11;
            page19[16 + lane] = 0;
        }
        page1C[0] = 0x11;
        page1C[1] = 0x4A;
        page1C[2] = 0x88;
        page1C[3] = 0xFF;
        page1C[4] = 0xFF;
        page1C[5] = 0x80;
        page1C[8] = 0xFF;

        // Page 1Dh: identity permutation, enabled and successfully committed.
        page1D[0] = 0x40;
        page1D[24] = 0x01;
        for (var lane = 0; lane < 8; lane++)
        {
            page1D[8 + lane] = (byte)(lane + 1);
            page1D[40 + lane] = 1;
            page1D[56 + lane] = (byte)(lane + 1);
        }

        // One VDM group with laser temperature, media SNR, BER and supply voltage.
        page2F[0] = 0x04;
        WriteUnsigned(page2F, 1, 100); // 10 ms fine interval.
        page2F[17] = 0x40;
        page20[0] = 0x0F;
        page20[1] = 0x04;
        page20[2] = 0x10;
        page20[3] = 0x05;
        page20[4] = 0x20;
        page20[5] = 0x0F;
        page20[6] = 0x3F;
        page20[7] = 0x4D;
        WriteSigned(page24, 0, (short)(42.5 * 256));
        WriteUnsigned(page24, 2, (ushort)(31.25 * 256));
        WriteHalf(page24, 4, 2.5e-6f);
        WriteUnsigned(page24, 6, 33000);
        WriteSigned(page28, 0, (short)(90 * 256));
        WriteSigned(page28, 2, (short)(-10 * 256));
        WriteSigned(page28, 4, (short)(80 * 256));
        WriteSigned(page28, 6, (short)(-5 * 256));
        page2C[0] = 0x01;
        page2D[0] = 0xF0;

        // CDB instance 1, showing a completed Query Status reply in local payload.
        WriteUnsigned(page9F, 0, 0x0000);
        WriteUnsigned(page9F, 2, 0);
        page9F[4] = 2;
        page9F[5] = 0xFD; // Check code for original command with ResponseDelay=0.
        page9F[6] = 2;
        page9F[8] = 2;
        page9F[9] = 1;
        page9F[7] = 0xFC;

        return (lower, new Dictionary<(byte Bank, byte Page), byte[]>
        {
            [(0, 0x00)] = page00,
            [(0, 0x01)] = page01,
            [(0, 0x02)] = page02,
            [(0, 0x03)] = page03,
            [(0, 0x04)] = page04,
            [(0, 0x10)] = page10,
            [(0, 0x11)] = page11,
            [(0, 0x12)] = page12,
            [(0, 0x13)] = page13,
            [(0, 0x14)] = page14,
            [(0, 0x15)] = page15,
            [(0, 0x16)] = page16,
            [(0, 0x17)] = page17,
            [(0, 0x18)] = page18,
            [(0, 0x19)] = page19,
            [(0, 0x1C)] = page1C,
            [(0, 0x1D)] = page1D,
            [(0, 0x20)] = page20,
            [(0, 0x24)] = page24,
            [(0, 0x28)] = page28,
            [(0, 0x2C)] = page2C,
            [(0, 0x2D)] = page2D,
            [(0, 0x2F)] = page2F,
            [(0, 0x9F)] = page9F,
            [(0, 0xA0)] = pageA0
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

    private static void WriteUnsigned32(byte[] target, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32BigEndian(target.AsSpan(offset, 4), value);

    private static void WriteHalf(byte[] target, int offset, float value) =>
        WriteUnsigned(target, offset, BitConverter.HalfToUInt16Bits((Half)value));

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
