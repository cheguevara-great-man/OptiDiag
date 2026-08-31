using System.Text.Json;
using OptiDiag.Application;
using OptiDiag.I2c.Simulator;
using OptiDiag.Infrastructure;
using OptiDiag.Protocols.Abstractions;
using OptiDiag.Protocols.Sff8472;

namespace OptiDiag.Core.Tests;

public sealed class ProductizationTests
{
    [Theory]
    [InlineData(RegisterAccess.ReadOnly, RegisterWriteRisk.Blocked)]
    [InlineData(RegisterAccess.ReadOnlyClearOnRead, RegisterWriteRisk.Blocked)]
    [InlineData(RegisterAccess.Mixed, RegisterWriteRisk.Blocked)]
    [InlineData(RegisterAccess.Reserved, RegisterWriteRisk.Blocked)]
    [InlineData(RegisterAccess.ReadWrite, RegisterWriteRisk.Elevated)]
    [InlineData(RegisterAccess.WriteOnly, RegisterWriteRisk.Critical)]
    [InlineData(RegisterAccess.WriteOnlySelfClearing, RegisterWriteRisk.Critical)]
    [InlineData(RegisterAccess.ReadWriteSelfClearing, RegisterWriteRisk.Critical)]
    [InlineData(RegisterAccess.VendorSpecific, RegisterWriteRisk.Critical)]
    public void WritePolicy_ClassifiesAccess(RegisterAccess access, RegisterWriteRisk expected)
    {
        var register = Register(access: access);

        var assessment = RegisterWritePolicy.Assess(register);

        Assert.Equal(expected, assessment.Risk);
        Assert.Equal(expected != RegisterWriteRisk.Blocked, assessment.IsAllowed);
    }

    [Theory]
    [InlineData(126)]
    [InlineData(127)]
    public void WritePolicy_BlocksRawBankAndPageSelectors(int offset)
    {
        var register = Register(offset: offset, access: RegisterAccess.ReadWrite);

        var assessment = RegisterWritePolicy.Assess(register);

        Assert.Equal(RegisterWriteRisk.Blocked, assessment.Risk);
        Assert.Contains("选择", assessment.Title);
    }

    [Fact]
    public void WritePolicy_DangerousSemanticRequiresTypedConfirmation()
    {
        var register = Register(name: "Software Reset", access: RegisterAccess.ReadWrite);

        var assessment = RegisterWritePolicy.Assess(register);

        Assert.Equal(RegisterWriteRisk.Critical, assessment.Risk);
        Assert.True(assessment.RequiresTypedConfirmation);
    }

    [Fact]
    public void WritePolicy_RequiresUnlockAndCriticalConfirmation()
    {
        var ordinary = Register();
        var critical = Register(access: RegisterAccess.WriteOnlySelfClearing);

        Assert.Throws<InvalidOperationException>(() =>
            RegisterWritePolicy.EnsureAuthorized(ordinary, writeUnlocked: false, criticalConfirmed: false));
        Assert.Throws<InvalidOperationException>(() =>
            RegisterWritePolicy.EnsureAuthorized(critical, writeUnlocked: true, criticalConfirmed: false));
        RegisterWritePolicy.EnsureAuthorized(critical, writeUnlocked: true, criticalConfirmed: true);
    }

    [Fact]
    public async Task SafeWriter_BlockedSelectorSendsNoI2cWrite()
    {
        await using var session = new ModuleSession(new Sff8472Simulator(), new Sff8472Protocol());
        var traces = new List<OptiDiag.I2c.Abstractions.I2cTraceEntry>();
        session.TransferCompleted += (_, entry) => traces.Add(entry);
        await session.ConnectAsync(CancellationToken.None);
        var selector = Register(offset: 127, value: 0, access: RegisterAccess.ReadWrite);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SafeRegisterWriter(session).WriteAsync(
                selector,
                2,
                writeUnlocked: true,
                criticalConfirmed: true,
                CancellationToken.None));

        Assert.Empty(traces);
    }

    [Fact]
    public async Task SafeWriter_VerifiesOrdinaryReadWriteRegister()
    {
        await using var session = new ModuleSession(new Sff8472Simulator(), new Sff8472Protocol());
        await session.ConnectAsync(CancellationToken.None);
        var current = await session.ReadByteAsync(0x51, null, 110, CancellationToken.None);
        var register = Register(
            deviceAddress: 0x51,
            offset: 110,
            value: current,
            access: RegisterAccess.ReadWrite);

        var result = await new SafeRegisterWriter(session).WriteAsync(
            register,
            (byte)(current ^ 0x40),
            writeUnlocked: true,
            criticalConfirmed: false,
            CancellationToken.None);

        Assert.True(result.WasReadBackVerified);
        Assert.Equal((byte)(current ^ 0x40), result.ReadBackValue);
    }

    [Fact]
    public async Task SafeWriter_RejectsStaleUiValue()
    {
        await using var session = new ModuleSession(new Sff8472Simulator(), new Sff8472Protocol());
        await session.ConnectAsync(CancellationToken.None);
        var current = await session.ReadByteAsync(0x51, null, 110, CancellationToken.None);
        var stale = Register(
            deviceAddress: 0x51,
            offset: 110,
            value: (byte)(current ^ 0x01),
            access: RegisterAccess.ReadWrite);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SafeRegisterWriter(session).WriteAsync(
                stale,
                0x40,
                writeUnlocked: true,
                criticalConfirmed: false,
                CancellationToken.None));

        Assert.Contains("已从", error.Message);
        Assert.Contains("取消", error.Message);
    }

    [Fact]
    public async Task Preferences_RoundTripAndNormalize()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"OptiDiag-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var service = new UserPreferencesService(path);
            await service.SaveAsync(new UserPreferences("Unknown", 99, 0, false));

            var loaded = await service.LoadAsync();

            Assert.Equal("Dark", loaded.Theme);
            Assert.Equal(18, loaded.FontSize);
            Assert.Equal(1, loaded.PollingIntervalSeconds);
            Assert.False(loaded.CheckUpdatesOnStartup);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Preferences_CorruptJsonFallsBackSafely()
    {
        var path = Path.Combine(Path.GetTempPath(), $"OptiDiag-settings-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, "{broken");

            var loaded = await new UserPreferencesService(path).LoadAsync();

            Assert.Equal(new UserPreferences(), loaded);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UpdateParser_RecognizesNewVersionAndDigest()
    {
        using var json = JsonDocument.Parse("""
            {
              "tag_name": "v0.7.0",
              "name": "OptiDiag v0.7.0",
              "html_url": "https://github.com/example/OptiDiag/releases/tag/v0.7.0",
              "assets": [
                {
                  "name": "OptiDiag.zip",
                  "browser_download_url": "https://github.com/example/OptiDiag/releases/download/v0.7.0/OptiDiag.zip",
                  "size": 1234,
                  "digest": "sha256:abcd"
                }
              ]
            }
            """);

        var result = GitHubReleaseUpdateService.Parse(json.RootElement, new Version(0, 6, 0));

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(new Version(0, 7, 0), result.LatestVersion);
        Assert.Equal("sha256:abcd", Assert.Single(result.Assets).Digest);
    }

    [Fact]
    public void UpdateParser_SameVersionIsNotAnUpdate()
    {
        using var json = JsonDocument.Parse("""
            {
              "tag_name": "v0.6.0",
              "name": "OptiDiag v0.6.0",
              "html_url": "https://github.com/example/OptiDiag/releases/tag/v0.6.0",
              "assets": []
            }
            """);

        var result = GitHubReleaseUpdateService.Parse(json.RootElement, new Version(0, 6, 0));

        Assert.False(result.IsUpdateAvailable);
    }

    private static RegisterValue Register(
        string name = "User Control",
        byte deviceAddress = 0x50,
        int offset = 42,
        byte value = 0,
        RegisterAccess access = RegisterAccess.ReadWrite) =>
        new("test", deviceAddress, null, offset, value, name, "test register", access);
}
