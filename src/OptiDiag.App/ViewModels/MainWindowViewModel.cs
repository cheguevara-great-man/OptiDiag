using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using OptiDiag.Application;
using OptiDiag.I2c.Abstractions;
using OptiDiag.Infrastructure;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.App.ViewModels;

public sealed record AppLogEntry(DateTimeOffset Timestamp, string Level, string Message)
{
    public string Time => Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
}

public sealed record TraceRow(I2cTraceEntry Entry)
{
    public string Time => Entry.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    public string Address => $"0x{Entry.DeviceAddress:X2}";
    public string WriteHex => string.Join(' ', Entry.WriteBuffer.Select(x => $"{x:X2}"));
    public string ReadHex => string.Join(' ', Entry.ReadBuffer.Select(x => $"{x:X2}"));
    public string Duration => $"{Entry.Elapsed.TotalMilliseconds:0.0} ms";
    public string Result => Entry.Success ? "成功" : Entry.ErrorMessage ?? "失败";
}

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ModuleSession _session;
    private readonly PollingEngine _poller;
    private readonly DumpFileService _dumpFiles;
    private readonly DumpComparisonService _dumpComparer;
    private readonly CsvExportService _csvExporter;
    private readonly SemaphoreSlim _logGate = new(1, 1);
    private readonly string _logPath;
    private SessionSnapshot? _latest;
    private ModuleInformation? _information;
    private string _connectionStatus = "未连接";
    private string _statusMessage = "使用内置 SFF-8472 模拟模块，可直接开始调试。";
    private bool _isBusy;
    private bool _writeUnlocked;
    private string _registerFilter = string.Empty;
    private string _selectedRegion = "全部";
    private RegisterValue? _selectedRegister;
    private int _pollingIntervalSeconds = 1;
    private IReadOnlyList<TrendSample> _trendSamples = [];

    public MainWindowViewModel(
        ModuleSession session,
        PollingEngine poller,
        DumpFileService dumpFiles,
        DumpComparisonService dumpComparer,
        CsvExportService csvExporter)
    {
        _session = session;
        _poller = poller;
        _dumpFiles = dumpFiles;
        _dumpComparer = dumpComparer;
        _csvExporter = csvExporter;
        _logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OptiDiag",
            "logs",
            $"OptiDiag-{DateTime.Now:yyyyMMdd}.log");

        RegisterView = CollectionViewSource.GetDefaultView(Registers);
        RegisterView.Filter = FilterRegister;
        _session.TransferCompleted += OnTransferCompleted;
        _poller.SnapshotReceived += OnPollingSnapshot;
        _poller.PollingFailed += OnPollingFailed;
        Regions.Add("全部");
        AddLog("INFO", "OptiDiag 已启动，硬件通信层当前使用标准模拟器。");
    }

    public ObservableCollection<Measurement> Measurements { get; } = [];
    public ObservableCollection<ThresholdRow> Thresholds { get; } = [];
    public ObservableCollection<AlarmFlag> Alarms { get; } = [];
    public ObservableCollection<StatusItem> StatusItems { get; } = [];
    public ObservableCollection<RegisterValue> Registers { get; } = [];
    public ObservableCollection<string> Regions { get; } = [];
    public ObservableCollection<DecodeDiagnostic> Diagnostics { get; } = [];
    public ObservableCollection<AppLogEntry> Logs { get; } = [];
    public ObservableCollection<TraceRow> Traces { get; } = [];
    public ObservableCollection<ByteDifference> DumpDifferences { get; } = [];

    public ICollectionView RegisterView { get; }

    public ModuleInformation? Information
    {
        get => _information;
        private set => SetProperty(ref _information, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetProperty(ref _connectionStatus, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsConnected => _session.IsConnected;

    public bool IsPolling => _poller.IsRunning;

    public bool WriteUnlocked
    {
        get => _writeUnlocked;
        set => SetProperty(ref _writeUnlocked, value);
    }

    public string RegisterFilter
    {
        get => _registerFilter;
        set
        {
            if (SetProperty(ref _registerFilter, value))
            {
                RegisterView.Refresh();
            }
        }
    }

    public string SelectedRegion
    {
        get => _selectedRegion;
        set
        {
            if (SetProperty(ref _selectedRegion, value))
            {
                RegisterView.Refresh();
            }
        }
    }

    public RegisterValue? SelectedRegister
    {
        get => _selectedRegister;
        set => SetProperty(ref _selectedRegister, value);
    }

    public int PollingIntervalSeconds
    {
        get => _pollingIntervalSeconds;
        set
        {
            var normalized = Math.Clamp(value, 1, 60);
            if (SetProperty(ref _pollingIntervalSeconds, normalized))
            {
                _poller.Interval = TimeSpan.FromSeconds(normalized);
            }
        }
    }

    public IReadOnlyList<TrendSample> TrendSamples
    {
        get => _trendSamples;
        private set => SetProperty(ref _trendSamples, value);
    }

    public string SnapshotTime => _latest?.Dump.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "--";

    public int ActiveAlarmCount => Alarms.Count(x => x.IsActive);

    public string DumpSummary { get; private set; } = "尚未进行 Dump 比较";

    public async Task ConnectAsync()
    {
        await RunBusyAsync(async () =>
        {
            await _session.ConnectAsync().ConfigureAwait(true);
            ConnectionStatus = $"已连接 · {_session.AdapterInfo.DisplayName}";
            OnPropertyChanged(nameof(IsConnected));
            AddLog("INFO", $"已连接 {_session.AdapterInfo.DisplayName}。");
            var snapshot = await _session.RefreshAsync().ConfigureAwait(true);
            ApplySnapshot(snapshot);
        }).ConfigureAwait(true);
    }

    public async Task DisconnectAsync()
    {
        await StopPollingAsync().ConfigureAwait(true);
        await _session.DisconnectAsync().ConfigureAwait(true);
        ConnectionStatus = "未连接";
        StatusMessage = "会话已断开。";
        OnPropertyChanged(nameof(IsConnected));
        AddLog("INFO", "会话已断开。");
    }

    public async Task RefreshAsync()
    {
        if (!_session.IsConnected)
        {
            StatusMessage = "请先连接模拟模块。";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var snapshot = await _session.RefreshAsync().ConfigureAwait(true);
            ApplySnapshot(snapshot);
        }).ConfigureAwait(true);
    }

    public void StartPolling()
    {
        if (!_session.IsConnected)
        {
            StatusMessage = "请先连接模拟模块。";
            return;
        }

        _poller.Interval = TimeSpan.FromSeconds(PollingIntervalSeconds);
        _poller.Start();
        OnPropertyChanged(nameof(IsPolling));
        StatusMessage = $"周期读取已启动，间隔 {PollingIntervalSeconds} 秒。";
        AddLog("INFO", StatusMessage);
    }

    public async Task StopPollingAsync()
    {
        await _poller.StopAsync().ConfigureAwait(true);
        OnPropertyChanged(nameof(IsPolling));
        StatusMessage = "周期读取已停止。";
    }

    public async Task SaveDumpAsync(string path)
    {
        if (_latest is null)
        {
            throw new InvalidOperationException("请先读取模块数据。 ");
        }

        await _dumpFiles.SaveAsync(path, _latest.Dump).ConfigureAwait(true);
        StatusMessage = $"Dump 已保存：{Path.GetFileName(path)}";
        AddLog("INFO", StatusMessage);
    }

    public async Task CompareDumpAsync(string path)
    {
        if (_latest is null)
        {
            throw new InvalidOperationException("请先读取模块数据。");
        }

        var other = await _dumpFiles.LoadAsync(path).ConfigureAwait(true);
        var comparison = _dumpComparer.Compare(_latest.Dump, other);
        Replace(DumpDifferences, comparison.Differences);
        DumpSummary = $"共 {comparison.Differences.Count} 处差异；静态 {comparison.StaticDifferenceCount}，动态 {comparison.VolatileDifferenceCount}";
        OnPropertyChanged(nameof(DumpSummary));
        StatusMessage = $"已与 {Path.GetFileName(path)} 比较。";
        AddLog("INFO", $"{StatusMessage} {DumpSummary}");
    }

    public Task ExportHistoryAsync(string path) => _csvExporter.ExportHistoryAsync(path, _poller.History);

    public Task ExportRegistersAsync(string path) => _csvExporter.ExportRegistersAsync(path, Registers);

    public async Task WriteSelectedRegisterAsync(string hexValue)
    {
        if (!WriteUnlocked)
        {
            throw new InvalidOperationException("请先开启写操作解锁。 ");
        }

        var register = SelectedRegister ?? throw new InvalidOperationException("请先选择一个寄存器。");
        if (register.Access is RegisterAccess.ReadOnly or RegisterAccess.Reserved)
        {
            throw new InvalidOperationException($"{register.Name} 是 {register.Access}，禁止写入。");
        }

        var normalized = hexValue.Trim().Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (!byte.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException("请输入 00-FF 范围内的十六进制字节。");
        }

        await _session.WriteByteAsync(
            register.DeviceAddress,
            register.Page,
            checked((byte)register.Offset),
            value).ConfigureAwait(true);
        AddLog("WARN", $"写寄存器 {register.AddressText}: {register.HexValue} -> {value:X2}");
        await RefreshAsync().ConfigureAwait(true);
    }

    public void ClearTraces() => Traces.Clear();

    private void ApplySnapshot(SessionSnapshot snapshot)
    {
        _latest = snapshot;
        Information = snapshot.Module.Information;
        Replace(Measurements, snapshot.Module.Measurements);
        Replace(Thresholds, snapshot.Module.Thresholds);
        Replace(Alarms, snapshot.Module.Alarms);
        Replace(StatusItems, snapshot.Module.Status);
        Replace(Registers, snapshot.Module.Registers);
        Replace(Diagnostics, snapshot.Module.Diagnostics);

        var regionNames = snapshot.Dump.Regions.Select(x => x.Id).Distinct().OrderBy(x => x).ToArray();
        Regions.Clear();
        Regions.Add("全部");
        foreach (var region in regionNames)
        {
            Regions.Add(region);
        }

        var desiredRegion = Regions.Contains(SelectedRegion) ? SelectedRegion : "全部";
        _selectedRegion = string.Empty;
        SelectedRegion = desiredRegion;

        TrendSamples = _poller.History;
        RegisterView.Refresh();
        OnPropertyChanged(nameof(SnapshotTime));
        OnPropertyChanged(nameof(ActiveAlarmCount));
        StatusMessage = $"读取完成：{snapshot.Dump.Regions.Count} 个区域，{snapshot.Module.Registers.Count} 字节。";
        if (snapshot.CaptureWarnings.Count > 0)
        {
            StatusMessage += $" {snapshot.CaptureWarnings.Count} 个可选区域失败。";
        }
    }

    private bool FilterRegister(object item)
    {
        if (item is not RegisterValue register)
        {
            return false;
        }

        if (SelectedRegion != "全部" && !string.Equals(register.RegionId, SelectedRegion, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(RegisterFilter))
        {
            return true;
        }

        var filter = RegisterFilter.Trim();
        return register.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || register.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || register.AddressText.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || register.HexValue.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddLog("ERROR", ex.ToString());
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnPollingSnapshot(object? sender, SessionSnapshot snapshot) =>
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => ApplySnapshot(snapshot));

    private void OnPollingFailed(object? sender, Exception exception) =>
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            StatusMessage = $"周期读取失败：{exception.Message}";
            AddLog("ERROR", StatusMessage);
        });

    private void OnTransferCompleted(object? sender, I2cTraceEntry entry) =>
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            Traces.Insert(0, new TraceRow(entry));
            while (Traces.Count > 1000)
            {
                Traces.RemoveAt(Traces.Count - 1);
            }
        });

    private void AddLog(string level, string message)
    {
        var entry = new AppLogEntry(DateTimeOffset.Now, level, message);
        Logs.Insert(0, entry);
        while (Logs.Count > 1000)
        {
            Logs.RemoveAt(Logs.Count - 1);
        }

        _ = PersistLogAsync(entry);
    }

    private async Task PersistLogAsync(AppLogEntry entry)
    {
        await _logGate.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            await File.AppendAllTextAsync(
                _logPath,
                $"{entry.Timestamp:O}\t{entry.Level}\t{entry.Message}{Environment.NewLine}").ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            _logGate.Release();
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _session.TransferCompleted -= OnTransferCompleted;
        _poller.SnapshotReceived -= OnPollingSnapshot;
        _poller.PollingFailed -= OnPollingFailed;
        await _poller.DisposeAsync().ConfigureAwait(false);
        await _session.DisposeAsync().ConfigureAwait(false);
        _logGate.Dispose();
    }
}
