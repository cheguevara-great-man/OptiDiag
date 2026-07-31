using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.Application;

public sealed record TrendSample(DateTimeOffset Timestamp, IReadOnlyDictionary<string, double> Values);

public sealed class PollingEngine : IAsyncDisposable
{
    private readonly ModuleSession _session;
    private readonly List<TrendSample> _history = [];
    private CancellationTokenSource? _cancellation;
    private Task? _worker;

    public PollingEngine(ModuleSession session)
    {
        _session = session;
    }

    public bool IsRunning => _worker is { IsCompleted: false };

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

    public int HistoryCapacity { get; set; } = 1800;

    public IReadOnlyList<TrendSample> History
    {
        get
        {
            lock (_history)
            {
                return _history.ToArray();
            }
        }
    }

    public event EventHandler<SessionSnapshot>? SnapshotReceived;

    public event EventHandler<Exception>? PollingFailed;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cancellation = new CancellationTokenSource();
        _worker = RunAsync(_cancellation.Token);
    }

    public async Task StopAsync()
    {
        if (_cancellation is null)
        {
            return;
        }

        await _cancellation.CancelAsync().ConfigureAwait(false);
        if (_worker is not null)
        {
            try
            {
                await _worker.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cancellation.Dispose();
        _cancellation = null;
        _worker = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var startedAt = DateTimeOffset.Now;
            try
            {
                var snapshot = await _session.RefreshAsync(cancellationToken).ConfigureAwait(false);
                AddHistory(snapshot);
                SnapshotReceived?.Invoke(this, snapshot);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                PollingFailed?.Invoke(this, ex);
            }

            var delay = Interval - (DateTimeOffset.Now - startedAt);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void AddHistory(SessionSnapshot snapshot)
    {
        var values = snapshot.Module.Measurements.ToDictionary(x => x.Id, x => x.Value);
        lock (_history)
        {
            _history.Add(new TrendSample(snapshot.Dump.CapturedAt, values));
            if (_history.Count > HistoryCapacity)
            {
                _history.RemoveRange(0, _history.Count - HistoryCapacity);
            }
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
