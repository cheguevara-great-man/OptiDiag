using OptiDiag.I2c.Abstractions;

namespace OptiDiag.I2c.Simulator;

/// <summary>
/// Lets the desktop application switch between independent simulated modules
/// without coupling protocol implementations to one another.
/// </summary>
public sealed class SwitchableI2cAdapter : II2cAdapter
{
    private readonly IReadOnlyDictionary<string, II2cAdapter> _adapters;
    private II2cAdapter _current;
    private bool _disposed;

    public SwitchableI2cAdapter(
        IReadOnlyDictionary<string, II2cAdapter> adapters,
        string initialKey)
    {
        if (adapters.Count == 0)
        {
            throw new ArgumentException("至少需要一个模拟适配器。", nameof(adapters));
        }

        _adapters = adapters;
        _current = GetAdapter(initialKey);
        foreach (var adapter in _adapters.Values.Distinct())
        {
            adapter.TransferCompleted += ForwardTransferCompleted;
        }
    }

    public string CurrentKey { get; private set; } = string.Empty;

    public I2cAdapterInfo Info => _current.Info;

    public bool IsOpen => _current.IsOpen;

    public event EventHandler<I2cTraceEntry>? TransferCompleted;

    public async Task SelectAsync(string key, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var next = GetAdapter(key);
        if (ReferenceEquals(next, _current))
        {
            CurrentKey = key;
            return;
        }

        if (_current.IsOpen)
        {
            await _current.CloseAsync(cancellationToken).ConfigureAwait(false);
        }

        _current = next;
        CurrentKey = key;
    }

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _current.OpenAsync(cancellationToken);
    }

    public Task CloseAsync(CancellationToken cancellationToken = default) =>
        _current.CloseAsync(cancellationToken);

    public Task<I2cTransferResult> TransferAsync(
        I2cTransfer transfer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _current.TransferAsync(transfer, cancellationToken);
    }

    private II2cAdapter GetAdapter(string key)
    {
        if (!_adapters.TryGetValue(key, out var adapter))
        {
            throw new ArgumentOutOfRangeException(nameof(key), $"未知模拟数据源：{key}");
        }

        CurrentKey = key;
        return adapter;
    }

    private void ForwardTransferCompleted(object? sender, I2cTraceEntry entry)
    {
        if (ReferenceEquals(sender, _current))
        {
            TransferCompleted?.Invoke(this, entry);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var adapter in _adapters.Values.Distinct())
        {
            adapter.TransferCompleted -= ForwardTransferCompleted;
            await adapter.DisposeAsync().ConfigureAwait(false);
        }

        _disposed = true;
    }
}
