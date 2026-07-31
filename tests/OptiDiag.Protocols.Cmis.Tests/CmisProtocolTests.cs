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
    public async Task Detector_RecognizesOfficialCmis54Revision()
    {
        await using var adapter = new CmisSimulator(CmisSimulatorRevision.Cmis54);
        await adapter.OpenAsync();

        var detection = await new ProtocolDetectionService().DetectAsync(adapter);

        Assert.Equal("cmis", detection.ProtocolId);
        Assert.Contains("5.4", detection.Evidence);
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

    [Fact]
    public void MemoryMap_CoversEveryPageExactlyOnce()
    {
        for (var page = 0; page <= byte.MaxValue; page++)
        {
            var definitions = CmisMemoryMap.Pages
                .Where(item => page >= item.FirstPage && page <= item.LastPage)
                .ToArray();
            Assert.Single(definitions);
        }

        Assert.DoesNotContain(
            new CmisProtocol().CapturePlan,
            region => region.Page is >= 0x0E and <= 0x0F or 0x2E
                or >= 0x63 and <= 0x6C or >= 0x6E and <= 0x9E);
        Assert.Contains(new CmisProtocol().CapturePlan, region => region.Page == 0x0C);
        Assert.Contains(new CmisProtocol().CapturePlan, region => region.Page == 0x6D);
    }

    [Fact]
    public void CdbCatalog_ContainsAllBaseSpecificationCommands()
    {
        Assert.Equal(53, CmisCdbCommandCatalog.Commands.Count);
        Assert.Equal(53, CmisCdbCommandCatalog.Commands.Select(command => command.Id).Distinct().Count());
        Assert.Equal(53, CmisCdbPayloadRules.Rules.Count);
        Assert.Equal(
            CmisCdbCommandCatalog.Commands.Select(command => command.Id).Order(),
            CmisCdbPayloadRules.Rules.Select(rule => rule.CommandId).Order());
        Assert.Equal("Query Status", CmisCdbCommandCatalog.Find(0x0000)?.Title);
        Assert.Equal("Commit Firmware Image", CmisCdbCommandCatalog.Find(0x010A)?.Title);
        Assert.Equal("Get Digest Signature in EPL", CmisCdbCommandCatalog.Find(0x0405)?.Title);
        Assert.Equal(48, CmisCdbCommandCatalog.ForRevision(CmisRevision.V53).Count);
        Assert.Equal(52, CmisCdbCommandCatalog.ForRevision(CmisRevision.V54).Count);
        Assert.DoesNotContain(CmisCdbCommandCatalog.ForRevision(CmisRevision.V53), item => item.Id == 0x0005);
        Assert.DoesNotContain(CmisCdbCommandCatalog.ForRevision(CmisRevision.V54), item => item.Id == 0x0281);
        Assert.Null(CmisCdbCommandCatalog.Find(0x4000)); // CMIS-VCS supplement, not base CMIS.
    }

    [Fact]
    public void CdbCodec_RejectsPayloadThatViolatesCommandContract()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            CmisCdbCodec.EncodeMessagePage(new CmisCdbCommand(0x0001, [0x12, 0x34])));

        Assert.Contains("must be 4", error.Message);
    }

    [Fact]
    public void CdbReplyDecoder_DecodesApplicationAttributes()
    {
        var payload = new byte[20];
        payload[1] = 1;
        payload[2] = 0x2E;
        payload[3] = 0xE0; // 12000 mW.
        var command = new CmisCdbCommand(0x0050, [0x00, 0x01]);
        var reply = new CmisCdbReply(
            0x0050,
            20,
            payload,
            [],
            0,
            0,
            true,
            CmisCdbCommandCatalog.Find(0x0050));

        var decoded = CmisCdbReplyDecoder.Decode(command, reply);

        Assert.Contains(decoded, item => item.Name == "ApplicationNumber" && item.Value == "1");
        Assert.Contains(decoded, item => item.Name == "MaxModulePower" && item.Value == "12000 mW");
    }

    [Fact]
    public void CdbCodec_EncodesLengthsPayloadAndChecksums()
    {
        var command = new CmisCdbCommand(0x0000, [0x00, 0x00]);
        var page = CmisCdbCodec.EncodeMessagePage(command);

        Assert.Equal(128, page.Length);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x02, 0xFD }, page[..6]);
        Assert.Equal(new byte[] { 0x00, 0x00 }, page[8..10]);

        page[6] = 2;
        page[7] = 0xFC;
        page[8] = 2;
        page[9] = 1;
        var reply = CmisCdbCodec.DecodeReply(page);

        Assert.True(reply.CheckCodeValid);
        Assert.Equal(new byte[] { 2, 1 }, reply.LocalPayload);
        Assert.Equal("Query Status", reply.Definition?.Title);
    }

    [Fact]
    public async Task Simulator_CapturesAndDecodesOptionalBasePages()
    {
        await using var session = new ModuleSession(new CmisSimulator(), new CmisProtocol());
        await session.ConnectAsync();

        var snapshot = await session.RefreshAsync();
        var pages = snapshot.Dump.Regions
            .Where(region => region.Page.HasValue)
            .Select(region => region.Page!.Value)
            .ToHashSet();

        foreach (var expected in new byte[]
                 {
                     0x04, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
                     0x18, 0x19, 0x1C, 0x1D, 0x20, 0x24, 0x28, 0x2C, 0x2D, 0x2F, 0x9F, 0xA0
                 })
        {
            Assert.Contains(expected, pages);
        }

        var fields = snapshot.Module.Fields ?? [];
        Assert.Contains(fields, field => field.Name == "GridSupported50GHz");
        Assert.Contains(fields, field => field.Name == "HostSideBERLane1");
        Assert.Contains(fields, field => field.Name == "DataPathRxLatencyLane1" && field.Value.Contains("80"));
        Assert.Contains(fields, field => field.Name == "Bank0VDMGroupsSupported" && field.Value == "1");
        Assert.Contains(fields, field => field.Name == "VDM1 Laser Temperature");
        Assert.Contains(fields, field => field.Name == "CMDID" && field.Value.Contains("Query Status"));
    }

    [Fact]
    public async Task Cmis54Simulator_CapturesAndDecodesNewPagesAndFields()
    {
        await using var session = new ModuleSession(
            new CmisSimulator(CmisSimulatorRevision.Cmis54),
            new CmisProtocol());
        await session.ConnectAsync();

        var snapshot = await session.RefreshAsync();
        var pages = snapshot.Dump.Regions.Where(region => region.Page.HasValue)
            .Select(region => region.Page!.Value).ToHashSet();
        var fields = snapshot.Module.Fields ?? [];

        Assert.Equal("5.4", snapshot.Module.Protocol?.Revision);
        Assert.Equal("5.4", snapshot.Dump.ProtocolRevision);
        foreach (var page in new byte[] { 0x0C, 0x0D, 0x60, 0x61, 0x62, 0x6D })
        {
            Assert.Contains(page, pages);
        }

        Assert.Contains(fields, field => field.Name == "GridSupported300GHz" && field.Value == "是");
        Assert.Contains(fields, field => field.Name == "SFF8024HeatsinkType" && field.Value.Contains("RHS"));
        Assert.Contains(fields, field => field.Name == "ConsolidatedLoadManagementRevision" && field.Value == "5.4");
        Assert.Contains(fields, field => field.Name == "TxLaneAcquisitionCounter1" && field.Value == "12");
        Assert.Contains(fields, field => field.Name == "Lane1TxPowerHighAlarm" && field.Value.Contains("2"));
        Assert.Contains(fields, field => field.Name == "Lane1CommitResult" && field.Value == "Success");
        Assert.Contains(snapshot.Module.Registers, register => register.Page == 0x60 && register.Offset == 192
            && register.Access == RegisterAccess.WriteOnlySelfClearing);
        Assert.Contains(snapshot.Module.Registers, register => register.Page == 0x12 && register.Offset == 216
            && register.Access == RegisterAccess.ReadWrite);
    }

    [Fact]
    public async Task Cmis54Cdb_ModuleTimeAndFirmwareLoadTagRoundTrip()
    {
        await using var session = new ModuleSession(
            new CmisSimulator(CmisSimulatorRevision.Cmis54),
            new CmisProtocol());
        await session.ConnectAsync();
        await session.RefreshAsync();
        var executor = new CmisCdbExecutor(new SessionCdbMemoryAccess(session));

        var timeResult = await executor.ExecuteAsync(new CmisCdbCommand(0x0005));
        var timeFields = CmisCdbReplyDecoder.Decode(new CmisCdbCommand(0x0005), timeResult.Reply);
        Assert.True(timeResult.Success);
        Assert.Contains(timeFields, field => field.Name == "ModuleTimeUtc");

        var updater = new CmisFirmwareUpdateService(executor);
        Assert.True((await updater.StoreLoadTagAsync(0, "release-5.4")).Success);
        var tagResult = await updater.RetrieveLoadTagAsync(0);
        var tagFields = CmisCdbReplyDecoder.Decode(new CmisCdbCommand(0x010D, [0]), tagResult.Reply);
        Assert.Contains(tagFields, field => field.Name == "FirmwareLoadTag" && field.Value == "release-5.4");
    }

    [Fact]
    public async Task Cmis53Simulator_RejectsCmis54OnlyCommand()
    {
        await using var session = new ModuleSession(new CmisSimulator(), new CmisProtocol());
        await session.ConnectAsync();
        await session.RefreshAsync();
        var executor = new CmisCdbExecutor(new SessionCdbMemoryAccess(session));

        var result = await executor.ExecuteAsync(new CmisCdbCommand(0x0005));

        Assert.False(result.Success);
        Assert.Equal(0x42, result.Status);
    }

    [Fact]
    public void Cmis54_ExtendedLaneAndNadCountsUseEscapeEncoding()
    {
        var page01 = new byte[128];
        page01[14] = 0x03;
        page01[46] = 31;
        page01[47] = 255;

        Assert.Equal(32, CmisMemoryMap.DecodeLaneBankCount(page01, CmisRevision.V54));
        Assert.Equal(1, CmisMemoryMap.DecodeLaneBankCount(page01, CmisRevision.V53));
        Assert.Equal(255, CmisMemoryMap.DecodeNadBankCount(page01[47], CmisRevision.V54));
        Assert.Equal(15, CmisMemoryMap.DecodeNadBankCount(page01[47], CmisRevision.V53));
    }

    [Fact]
    public void Cmis54_InterfaceDescriptionUsesCorrectedOffsetsAndExactFields()
    {
        var payload = Enumerable.Repeat((byte)0x20, 96).ToArray();
        payload[1] = 0x11;
        payload[2] = 0;
        System.Text.Encoding.ASCII.GetBytes("400ZR").CopyTo(payload, 3);
        payload[90] = 0;
        payload[91] = 2;
        payload[92] = 0x40;
        payload[93] = 0;
        var command = new CmisCdbCommand(0x0051, [0, 0x11, 0]);
        var reply = new CmisCdbReply(0x0051, 96, payload, [], 0, 0, true,
            CmisCdbCommandCatalog.Find(0x0051));

        var decoded = CmisCdbReplyDecoder.Decode(command, reply);

        Assert.Contains(decoded, item => item.Name == "InterfaceName" && item.Value.StartsWith("400ZR"));
        Assert.Contains(decoded, item => item.Name == "BitsPerSymbol" && item.Value == "2");
        Assert.Contains(decoded, item => item.Name == "BitsPerSymbolExact" && item.Value == "2");
    }

    [Fact]
    public async Task RegisterMap_ProtectsCommandsFlagsReservedAndCdbPayload()
    {
        await using var session = new ModuleSession(new CmisSimulator(), new CmisProtocol());
        await session.ConnectAsync();
        var snapshot = await session.RefreshAsync();

        Assert.Contains(snapshot.Module.Registers, register =>
            register.Page == 0x10 && register.Offset == 143
            && register.Access == RegisterAccess.WriteOnlySelfClearing);
        Assert.Contains(snapshot.Module.Registers, register =>
            register.Page == 0x14 && register.Offset == 132
            && register.Access == RegisterAccess.ReadOnlyClearOnRead);
        Assert.Contains(snapshot.Module.Registers, register =>
            register.Page == 0x1D && register.Offset == 160
            && register.Access == RegisterAccess.WriteOnlySelfClearing);
        Assert.Contains(snapshot.Module.Registers, register =>
            register.Page == 0x9F && register.Offset == 136
            && register.Access == RegisterAccess.Mixed);
        Assert.Contains(snapshot.Module.Registers, register =>
            register.Page == 0xA0 && register.Access == RegisterAccess.Mixed);
    }

    [Fact]
    public async Task CdbExecutor_CompletesQueryStatusAgainstSimulator()
    {
        await using var session = new ModuleSession(new CmisSimulator(), new CmisProtocol());
        await session.ConnectAsync();
        await session.RefreshAsync();
        var executor = new CmisCdbExecutor(new SessionCdbMemoryAccess(session));

        var result = await executor.ExecuteAsync(new CmisCdbCommand(0x0000, [0x00, 0x00]));

        Assert.True(result.Success);
        Assert.Equal(0x01, result.Status);
        Assert.True(result.Reply.CheckCodeValid);
        Assert.Equal(new byte[] { 2, 1 }, result.Reply.LocalPayload);
    }

    [Fact]
    public async Task FirmwareUpdater_TransfersBlocksAndCompletesWithoutRunningImage()
    {
        await using var session = new ModuleSession(new CmisSimulator(), new CmisProtocol());
        await session.ConnectAsync();
        await session.RefreshAsync();
        var executor = new CmisCdbExecutor(new SessionCdbMemoryAccess(session));
        var updater = new CmisFirmwareUpdateService(executor);
        var image = Enumerable.Range(0, 300).Select(value => (byte)value).ToArray();

        var result = await updater.DownloadAsync(
            image,
            new CmisFirmwareDownloadOptions(UseExtendedPayload: true, BlockSize: 128));

        Assert.Equal(300, result.ImageSize);
        Assert.Equal(3, result.BlocksWritten);
        Assert.Equal(0, result.BlocksSkipped);
    }

    private sealed class SessionCdbMemoryAccess(ModuleSession session) : ICmisCdbMemoryAccess
    {
        public Task WriteAsync(
            byte? page,
            byte bank,
            byte offset,
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken) =>
            session.WriteBytesAsync(
                0x50,
                page,
                offset,
                data,
                cancellationToken,
                page.HasValue ? bank : null,
                page.HasValue ? (byte)126 : null);

        public Task<byte[]> ReadAsync(
            byte? page,
            byte bank,
            byte offset,
            int length,
            CancellationToken cancellationToken) =>
            session.ReadBytesAsync(
                0x50,
                page,
                offset,
                length,
                cancellationToken,
                page.HasValue ? bank : null,
                page.HasValue ? (byte)126 : null);
    }
}
