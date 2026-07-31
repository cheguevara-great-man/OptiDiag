using OptiDiag.Application;
using OptiDiag.I2c.Abstractions;
using OptiDiag.I2c.Simulator;
using OptiDiag.Protocols.Abstractions;
using OptiDiag.Protocols.Cmis;
using OptiDiag.Protocols.Sff8472;

namespace OptiDiag.Protocols.Cmis.Tests;

public sealed class CmisProtocolTests
{
    [Fact]
    public async Task Detector_RecognizesCmis53QsfPdd()
    {
        await using var adapter = new CmisSimulator();
        await adapter.OpenAsync();

        var detection = await new ProtocolDetectionService().DetectAsync(adapter);

        Assert.Equal(0x18, detection.Identifier);
        Assert.Equal("cmis", detection.ProtocolId);
        Assert.Equal("CMIS", detection.ProtocolName);
        Assert.Contains("5.3", detection.Evidence);
        Assert.Contains("分页内存", detection.Evidence);
    }

    [Fact]
    public async Task Session_DecodesIdentityThresholdsAndEightLanes()
    {
        await using var session = new ModuleSession(new CmisSimulator(), new CmisProtocol());
        await session.ConnectAsync();

        var snapshot = await session.RefreshAsync();

        Assert.Equal("cmis", snapshot.Dump.ProtocolId);
        Assert.Equal("OPTIDIAG LAB", snapshot.Module.Information.VendorName);
        Assert.Equal("OD-QDD-FR4-400G", snapshot.Module.Information.PartNumber);
        Assert.Equal("5.3", snapshot.Module.Protocol?.Revision);
        Assert.Equal(5, snapshot.Module.Thresholds.Count);
        Assert.Equal(26, snapshot.Module.Measurements.Count);
        Assert.Contains(snapshot.Module.Measurements, x => x.Id == "tx-power-lane-8" && x.Value > 0);
        Assert.Contains(snapshot.Module.Status, x => x.Name == "通道 8 数据通道状态" && x.Value == "DPInitialized");
        Assert.Contains(snapshot.Module.Registers, x => x.Bank == 0 && x.Page == 0x11 && x.Offset == 154);
    }

    [Fact]
    public async Task Capture_UsesSingleBankPageSelectionTransaction()
    {
        await using var session = new ModuleSession(new CmisSimulator(), new CmisProtocol());
        var traces = new List<I2cTraceEntry>();
        session.TransferCompleted += (_, entry) => traces.Add(entry);
        await session.ConnectAsync();

        await session.RefreshAsync();

        Assert.Contains(traces, entry =>
            entry.Success
            && entry.WriteBuffer.SequenceEqual(new byte[] { 126, 0, 0x11 })
            && entry.ReadBuffer.Length == 0);
        Assert.Contains(traces, entry =>
            entry.Success
            && entry.WriteBuffer.SequenceEqual(new byte[] { 126 })
            && entry.ReadBuffer.SequenceEqual(new byte[] { 0, 0x11 }));
    }

    [Fact]
    public async Task BankedRegister_CanBeWrittenAndReadBack()
    {
        await using var session = new ModuleSession(new CmisSimulator(), new CmisProtocol());
        await session.ConnectAsync();

        await session.WriteByteAsync(
            0x50,
            0x10,
            128,
            0x5A,
            bank: 0,
            bankSelectOffset: 126);
        var value = await session.ReadByteAsync(
            0x50,
            0x10,
            128,
            bank: 0,
            bankSelectOffset: 126);

        Assert.Equal(0x5A, value);
    }

    [Fact]
    public async Task MultiProtocolSession_FollowsSelectedSimulator()
    {
        var sff = new Sff8472Simulator();
        var cmis = new CmisSimulator();
        await using var adapter = new SwitchableI2cAdapter(
            new Dictionary<string, II2cAdapter>
            {
                ["simulator-sff8472"] = sff,
                ["simulator-cmis"] = cmis
            },
            "simulator-sff8472");
        await using var session = new ModuleSession(
            adapter,
            new IOpticalModuleProtocol[] { new Sff8472Protocol(), new CmisProtocol() });
        await session.ConnectAsync();

        var sffSnapshot = await session.RefreshAsync();
        await session.DisconnectAsync();
        await adapter.SelectAsync("simulator-cmis");
        await session.ConnectAsync();
        var cmisSnapshot = await session.RefreshAsync();

        Assert.Equal("sff-8472", sffSnapshot.Detection.ProtocolId);
        Assert.Equal("cmis", cmisSnapshot.Detection.ProtocolId);
        Assert.IsType<CmisProtocol>(session.ActiveProtocol);
    }
}
