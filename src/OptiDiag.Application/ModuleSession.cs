using OptiDiag.I2c.Abstractions;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Application;

public sealed record SessionSnapshot(
    ModuleDump Dump,
    DecodedModule Module,
    IReadOnlyList<string> CaptureWarnings,
    ProtocolDetectionResult Detection);

public sealed class ModuleSession : IAsyncDisposable
{
    private readonly II2cAdapter _adapter;
    private readonly IReadOnlyDictionary<string, IOpticalModuleProtocol> _protocols;
    private readonly ModuleMemoryService _memory;
    private readonly ProtocolDetectionService _detector;
    private bool _disposed;

    public ModuleSession(
        II2cAdapter adapter,
        IOpticalModuleProtocol protocol,
        ProtocolDetectionService? detector = null)
        : this(adapter, new[] { protocol }, detector)
    {
    }

    public ModuleSession(
        II2cAdapter adapter,
        IEnumerable<IOpticalModuleProtocol> protocols,
        ProtocolDetectionService? detector = null)
    {
        _adapter = adapter;
        _protocols = protocols.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        if (_protocols.Count == 0)
        {
            throw new ArgumentException("至少需要注册一个光模块协议。", nameof(protocols));
        }

        _memory = new ModuleMemoryService(adapter);
        _detector = detector ?? new ProtocolDetectionService();
        _adapter.TransferCompleted += OnTransferCompleted;
    }

    public bool IsConnected => _adapter.IsOpen;

    public I2cAdapterInfo AdapterInfo => _adapter.Info;

    public IOpticalModuleProtocol Protocol =>
        ActiveProtocol ?? _protocols.Values.First();

    public IOpticalModuleProtocol? ActiveProtocol { get; private set; }

    public SessionSnapshot? Latest { get; private set; }

    public event EventHandler<I2cTraceEntry>? TransferCompleted;

    public event EventHandler<SessionSnapshot>? SnapshotUpdated;

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _adapter.OpenAsync(cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        _adapter.CloseAsync(cancellationToken);

    public async Task<SessionSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!_adapter.IsOpen)
        {
            throw new InvalidOperationException("请先连接模块会话。");
        }

        var detection = await _detector.DetectAsync(_adapter, cancellationToken).ConfigureAwait(false);
        if (!_protocols.TryGetValue(detection.ProtocolId, out var protocol))
        {
            throw new NotSupportedException(
                $"自动检测到 {detection.ProtocolName}，但当前会话未注册对应协议模块。");
        }

        ActiveProtocol = protocol;
        var capture = await _memory.CaptureAsync(protocol, cancellationToken).ConfigureAwait(false);
        if (!protocol.CanDecode(capture.Dump))
        {
            throw new InvalidDataException($"当前数据无法按 {protocol.DisplayName} 解码。");
        }

        var snapshot = new SessionSnapshot(
            capture.Dump,
            protocol.Decode(capture.Dump),
            capture.Warnings,
            detection);
        Latest = snapshot;
        SnapshotUpdated?.Invoke(this, snapshot);
        return snapshot;
    }

    public Task WriteByteAsync(
        byte deviceAddress,
        byte? page,
        byte offset,
        byte value,
        CancellationToken cancellationToken = default,
        byte? bank = null,
        byte? bankSelectOffset = null) =>
        _memory.WriteByteAsync(
            deviceAddress,
            page,
            offset,
            value,
            bank: bank,
            bankSelectOffset: bankSelectOffset,
            cancellationToken: cancellationToken);

    public Task<byte> ReadByteAsync(
        byte deviceAddress,
        byte? page,
        byte offset,
        CancellationToken cancellationToken = default,
        byte? bank = null,
        byte? bankSelectOffset = null) =>
        _memory.ReadByteAsync(
            deviceAddress,
            page,
            offset,
            bank: bank,
            bankSelectOffset: bankSelectOffset,
            cancellationToken: cancellationToken);

    public Task<byte[]> ReadBytesAsync(
        byte deviceAddress,
        byte? page,
        byte offset,
        int length,
        CancellationToken cancellationToken = default,
        byte? bank = null,
        byte? bankSelectOffset = null) =>
        _memory.ReadBytesAsync(
            deviceAddress,
            page,
            offset,
            length,
            bank: bank,
            bankSelectOffset: bankSelectOffset,
            cancellationToken: cancellationToken);

    public Task WriteBytesAsync(
        byte deviceAddress,
        byte? page,
        byte offset,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default,
        byte? bank = null,
        byte? bankSelectOffset = null) =>
        _memory.WriteBytesAsync(
            deviceAddress,
            page,
            offset,
            data,
            bank: bank,
            bankSelectOffset: bankSelectOffset,
            cancellationToken: cancellationToken);

    private void OnTransferCompleted(object? sender, I2cTraceEntry entry) => TransferCompleted?.Invoke(this, entry);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _adapter.TransferCompleted -= OnTransferCompleted;
        await _adapter.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }
}
