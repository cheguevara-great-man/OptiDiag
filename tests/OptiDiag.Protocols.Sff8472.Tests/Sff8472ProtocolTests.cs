using System.Buffers.Binary;
using OptiDiag.Application;
using OptiDiag.I2c.Simulator;
using OptiDiag.Protocols.Abstractions;
using OptiDiag.Protocols.Sff8472;

namespace OptiDiag.Protocols.Sff8472.Tests;

public sealed class Sff8472ProtocolTests
{
    [Fact]
    public async Task SimulatorCapture_DecodesIdentityAndDiagnostics()
    {
        await using var adapter = new Sff8472Simulator();
        var protocol = new Sff8472Protocol();
        var memory = new ModuleMemoryService(adapter);
        await adapter.OpenAsync(CancellationToken.None);

        var capture = await memory.CaptureAsync(protocol, CancellationToken.None);
        var decoded = protocol.Decode(capture.Dump);

        Assert.True(protocol.CanDecode(capture.Dump));
        Assert.Equal("OPTIDIAG LAB", decoded.Information.VendorName);
        Assert.Equal("OD-SFP-LR-10G", decoded.Information.PartNumber);
        Assert.Equal("Rev 12.5/12.5a", decoded.Information.Compliance);
        Assert.True(decoded.Information.DigitalDiagnosticsImplemented);
        Assert.True(decoded.Information.InternallyCalibrated);
        Assert.False(decoded.Information.ExternallyCalibrated);
        Assert.InRange(decoded.Measurements.Single(x => x.Id == "temperature").Value, 28.0, 35.0);
        Assert.InRange(decoded.Measurements.Single(x => x.Id == "voltage").Value, 3.2, 3.4);
        Assert.Equal(5, decoded.Thresholds.Count);
        Assert.Equal(896, decoded.Registers.Count);
        Assert.DoesNotContain(decoded.Diagnostics, x => x.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task Capture_ReadsLegacyAndOptionalPages()
    {
        await using var adapter = new Sff8472Simulator();
        var protocol = new Sff8472Protocol();
        var memory = new ModuleMemoryService(adapter);
        await adapter.OpenAsync(CancellationToken.None);

        var result = await memory.CaptureAsync(protocol, CancellationToken.None);

        Assert.Empty(result.Warnings);
        Assert.Equal(6, result.Dump.Regions.Count);
        Assert.Equal(256, result.Dump.FindRegion(Sff8472Protocol.A0RegionId)!.Data.Length);
        Assert.Equal(0xCA, result.Dump.FindRegion(Sff8472Protocol.A2Page03RegionId)!.Data[0]);
        Assert.Equal(
            result.Dump.FindRegion(Sff8472Protocol.A2Page00RegionId)!.Data,
            result.Dump.FindRegion(Sff8472Protocol.A2Page01RegionId)!.Data);
    }

    [Fact]
    public async Task InvalidChecksum_IsReportedWithoutBlockingDecode()
    {
        await using var adapter = new Sff8472Simulator();
        var protocol = new Sff8472Protocol();
        var memory = new ModuleMemoryService(adapter);
        await adapter.OpenAsync(CancellationToken.None);
        var capture = await memory.CaptureAsync(protocol, CancellationToken.None);
        capture.Dump.FindRegion(Sff8472Protocol.A0RegionId)!.Data[20] ^= 0x01;

        var decoded = protocol.Decode(capture.Dump);

        Assert.Contains(decoded.Diagnostics, x => x.Severity == DiagnosticSeverity.Warning && x.Message.Contains("CC_BASE"));
        Assert.NotEmpty(decoded.Measurements);
    }

    [Fact]
    public async Task Thresholds_AreOrderedBySeverity()
    {
        await using var adapter = new Sff8472Simulator();
        var protocol = new Sff8472Protocol();
        var memory = new ModuleMemoryService(adapter);
        await adapter.OpenAsync(CancellationToken.None);
        var capture = await memory.CaptureAsync(protocol, CancellationToken.None);

        var temperature = protocol.Decode(capture.Dump).Thresholds.Single(x => x.Id == "temperature");

        Assert.Equal(85, temperature.HighAlarm);
        Assert.Equal(75, temperature.HighWarning);
        Assert.Equal(-5, temperature.LowWarning);
        Assert.Equal(-10, temperature.LowAlarm);
    }

    [Fact]
    public async Task OptionalDwdmDiagnostics_AreDecodedWhenPresent()
    {
        var (dump, protocol) = await CaptureAsync();
        var a2 = dump.FindRegion(Sff8472Protocol.A2LowerRegionId)!.Data;
        WriteSigned(a2, 40, 60 * 256);
        WriteSigned(a2, 42, 0);
        WriteSigned(a2, 44, 55 * 256);
        WriteSigned(a2, 46, 5 * 256);
        WriteSigned(a2, 48, 200);
        WriteSigned(a2, 50, -200);
        WriteSigned(a2, 52, 150);
        WriteSigned(a2, 54, -150);
        WriteSigned(a2, 106, 35 * 256);
        WriteSigned(a2, 108, -25);
        a2[113] = 0b0010_1000;
        a2[95] = ComputeChecksum(a2.AsSpan(0, 95));

        var decoded = protocol.Decode(dump);

        Assert.Equal(35, decoded.Measurements.Single(x => x.Id == "laser-temperature").Value);
        Assert.Equal(-2.5, decoded.Measurements.Single(x => x.Id == "tec-current").Value);
        Assert.Equal(20, decoded.Thresholds.Single(x => x.Id == "tec-current").HighAlarm);
        Assert.True(decoded.Alarms.Single(x => x.Id == "laser-temperature-high-alarm").IsActive);
        Assert.True(decoded.Alarms.Single(x => x.Id == "tec-current-high-alarm").IsActive);
    }

    [Fact]
    public async Task ExternalCalibration_RxPolynomialUsesPointOneMicrowattUnits()
    {
        var (dump, protocol) = await CaptureAsync();
        var a0 = dump.FindRegion(Sff8472Protocol.A0RegionId)!.Data;
        var a2 = dump.FindRegion(Sff8472Protocol.A2LowerRegionId)!.Data;
        a0[92] = 0x58; // DDM + external calibration + average RX power.
        WriteSingle(a2, 68, 1.0f); // Rx_PWR(1); output equals raw in 0.1 µW.
        WriteUnsigned(a2, 76, 0x0100);
        WriteUnsigned(a2, 80, 0x0100);
        WriteUnsigned(a2, 84, 0x0100);
        WriteUnsigned(a2, 88, 0x0100);
        a0[95] = ComputeChecksum(a0.AsSpan(64, 31));
        a2[95] = ComputeChecksum(a2.AsSpan(0, 95));

        var rx = protocol.Decode(dump).Measurements.Single(x => x.Id == "rx-power");

        Assert.InRange(rx.Value, 0.4, 0.8);
    }

    [Fact]
    public async Task RemotePages_AreCapturedOnlyWhenRpmIsAdvertised()
    {
        await using var adapter = new Sff8472Simulator(enableRemotePerformanceMonitoring: true);
        var protocol = new Sff8472Protocol();
        var memory = new ModuleMemoryService(adapter);
        await adapter.OpenAsync(CancellationToken.None);

        var capture = await memory.CaptureAsync(protocol, CancellationToken.None);
        var decoded = protocol.Decode(capture.Dump);

        Assert.Empty(capture.Warnings);
        Assert.Equal(14, capture.Dump.Regions.Count);
        Assert.NotNull(capture.Dump.FindRegion(Sff8472Protocol.RemotePage20RegionId));
        Assert.Equal(1920, decoded.Registers.Count);
    }

    private static async Task<(ModuleDump Dump, Sff8472Protocol Protocol)> CaptureAsync()
    {
        await using var adapter = new Sff8472Simulator();
        var protocol = new Sff8472Protocol();
        var memory = new ModuleMemoryService(adapter);
        await adapter.OpenAsync(CancellationToken.None);
        return ((await memory.CaptureAsync(protocol, CancellationToken.None)).Dump, protocol);
    }

    private static void WriteSigned(byte[] target, int offset, int value) =>
        BinaryPrimitives.WriteInt16BigEndian(target.AsSpan(offset, 2), checked((short)value));

    private static void WriteUnsigned(byte[] target, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16BigEndian(target.AsSpan(offset, 2), value);

    private static void WriteSingle(byte[] target, int offset, float value) =>
        BinaryPrimitives.WriteInt32BigEndian(target.AsSpan(offset, 4), BitConverter.SingleToInt32Bits(value));

    private static byte ComputeChecksum(ReadOnlySpan<byte> data)
    {
        var sum = 0;
        foreach (var value in data)
        {
            sum += value;
        }

        return (byte)sum;
    }
}
