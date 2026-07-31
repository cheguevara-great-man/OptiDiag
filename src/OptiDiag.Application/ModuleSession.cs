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
    private readonly IOpticalModuleProtocol _protocol;
    private readonly ModuleMemoryService _memory;
    private readonly ProtocolDetectionService _detector;
    private bool _disposed;

    public ModuleSession(
        II2cAdapter adapter,
        IOpticalModuleProtocol protocol,
        ProtocolDetectionService? detector = null)
    {
        _adapter = adapter;
        _protocol = protocol;
        _memory = new ModuleMemoryService(adapter);
        _detector = detector ?? new ProtocolDetectionService();
        _adapter.TransferCompleted += OnTransferCompleted;
    }

    public bool IsConnected => _adapter.IsOpen;

    public I2cAdapterInfo AdapterInfo => _adapter.Info;

    public IOpticalModuleProtocol Protocol => _protocol;

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
        if (!detection.IsSupportedBy(_protocol.Id))
        {
            throw new NotSupportedException(
                $"自动检测到 {detection.ProtocolName}，当前会话加载的是 {_protocol.DisplayName}。"
                + " 请安装对应协议模块后再读取。");
        }

        var capture = await _memory.CaptureAsync(_protocol, cancellationToken).ConfigureAwait(false);
        if (!_protocol.CanDecode(capture.Dump))
        {
            throw new InvalidDataException($"当前数据无法按 {_protocol.DisplayName} 解码。");
        }

        var snapshot = new SessionSnapshot(
            capture.Dump,
            _protocol.Decode(capture.Dump),
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
        CancellationToken cancellationToken = default) =>
        _memory.WriteByteAsync(deviceAddress, page, offset, value, cancellationToken: cancellationToken);

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
