using OptiDiag.Application;
using OptiDiag.I2c.Abstractions;
using OptiDiag.I2c.Simulator;
using OptiDiag.Infrastructure;
using OptiDiag.Protocols.Abstractions;
using OptiDiag.Protocols.Sff8472;

namespace OptiDiag.Core.Tests;

public sealed class CoreWorkflowTests
{
    [Fact]
    public void I2cTransfer_RejectsEightBitAddressNotation()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new I2cTransfer(0xA0, new byte[] { 0x00 }, 1));

        Assert.Contains("7 位", exception.Message);
    }

    [Fact]
    public async Task Session_ProducesTraceAndDecodedSnapshot()
    {
        await using var session = new ModuleSession(new Sff8472Simulator(), new Sff8472Protocol());
        var traces = new List<I2cTraceEntry>();
        session.TransferCompleted += (_, entry) => traces.Add(entry);
        await session.ConnectAsync(CancellationToken.None);

        var snapshot = await session.RefreshAsync(CancellationToken.None);

        Assert.Equal("OPTIDIAG LAB", snapshot.Module.Information.VendorName);
        Assert.True(traces.Count >= 9);
        Assert.All(traces, x => Assert.True(x.Success));
        Assert.Contains(traces, x => x.DeviceAddress == 0x50);
        Assert.Contains(traces, x => x.DeviceAddress == 0x51 && x.WriteBuffer.FirstOrDefault() == 127);
    }

    [Fact]
    public async Task Dump_RoundTripsWithHashVerification()
    {
        var path = Path.Combine(Path.GetTempPath(), $"OptiDiag-{Guid.NewGuid():N}.omodump");
        try
        {
            await using var session = new ModuleSession(new Sff8472Simulator(), new Sff8472Protocol());
            await session.ConnectAsync(CancellationToken.None);
            var snapshot = await session.RefreshAsync(CancellationToken.None);
            var service = new DumpFileService();

            await service.SaveAsync(path, snapshot.Dump, CancellationToken.None);
            var loaded = await service.LoadAsync(path, CancellationToken.None);

            Assert.Equal(snapshot.Dump.ProtocolId, loaded.ProtocolId);
            Assert.Equal(snapshot.Dump.Regions.Count, loaded.Regions.Count);
            Assert.Equal(
                snapshot.Dump.FindRegion("a0")!.Data,
                loaded.FindRegion("a0")!.Data);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task Comparison_SeparatesVolatileAndStaticDifferences()
    {
        await using var session = new ModuleSession(new Sff8472Simulator(), new Sff8472Protocol());
        await session.ConnectAsync(CancellationToken.None);
        var first = await session.RefreshAsync(CancellationToken.None);
        await Task.Delay(20);
        var second = await session.RefreshAsync(CancellationToken.None);
        second.Dump.FindRegion("a0")!.Data[20] ^= 0x01;

        var comparison = new DumpComparisonService().Compare(first.Dump, second.Dump);

        Assert.True(comparison.StaticDifferenceCount >= 1);
        Assert.True(comparison.VolatileDifferenceCount >= 0);
        Assert.Contains(comparison.Differences, x => x.RegionId == "a0" && x.Offset == 20 && !x.IsVolatile);
    }

    [Fact]
    public async Task RegisterWrite_ChangesWritableSimulatorByte()
    {
        await using var session = new ModuleSession(new Sff8472Simulator(), new Sff8472Protocol());
        await session.ConnectAsync(CancellationToken.None);
        await session.WriteByteAsync(0x51, null, 110, 0x40, CancellationToken.None);
        var snapshot = await session.RefreshAsync(CancellationToken.None);

        var directlyRead = await session.ReadByteAsync(0x51, null, 110, CancellationToken.None);
        var pagedRead = await session.ReadByteAsync(0x51, 0x02, 128, CancellationToken.None);

        Assert.Equal(0x40, directlyRead);
        Assert.Equal(snapshot.Dump.FindRegion("a2-page02")!.Data[0], pagedRead);
    }

    [Fact]
    public async Task ProtocolDetection_DistinguishesPlain8472AndSff8690Extension()
    {
        await using var adapter = new Sff8472Simulator();
        var detector = new ProtocolDetectionService();
        await adapter.OpenAsync(CancellationToken.None);

        var plain = await detector.DetectAsync(adapter, CancellationToken.None);
        adapter.SetSff8690Enabled(true);
        var tunable = await detector.DetectAsync(adapter, CancellationToken.None);

        Assert.Equal("sff-8472", plain.ProtocolId);
        Assert.False(Assert.Single(plain.Extensions).IsPresent);
        Assert.Equal("sff-8472", tunable.ProtocolId);
        Assert.True(Assert.Single(tunable.Extensions).IsPresent);
        Assert.Contains("A0h.65.6=1", tunable.Extensions[0].Evidence);
    }
}
