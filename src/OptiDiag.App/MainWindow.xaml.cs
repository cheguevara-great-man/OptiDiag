using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using OptiDiag.App.ViewModels;

namespace OptiDiag.App;

public partial class MainWindow : Window
{
    private bool _loaded;

    public MainWindow()
    {
        InitializeComponent();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await ExecuteAsync(ViewModel.ConnectAsync);
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsConnected)
        {
            await ExecuteAsync(ViewModel.DisconnectAsync);
        }
        else
        {
            await ExecuteAsync(ViewModel.ConnectAsync);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await ExecuteAsync(ViewModel.RefreshAsync);

    private async void PollingButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsPolling)
        {
            await ExecuteAsync(ViewModel.StopPollingAsync);
        }
        else
        {
            ViewModel.StartPolling();
        }
    }

    private void PollingIntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || sender is not ComboBox { SelectedItem: ComboBoxItem item }
            || !int.TryParse(item.Tag?.ToString(), out var seconds))
        {
            return;
        }

        viewModel.PollingIntervalSeconds = seconds;
    }

    private async void DataSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel
            && sender is ComboBox { SelectedItem: DataSourceOption option })
        {
            await ExecuteAsync(() => viewModel.SelectDataSourceAsync(option));
        }
    }

    private async void SimulationSff8690CheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            await ExecuteAsync(() => ViewModel.SetSimulationSff8690Async(checkBox.IsChecked == true));
        }
    }

    private async void SimulationRemotePerformanceMonitoringCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            await ExecuteAsync(
                () => ViewModel.SetSimulationRemotePerformanceMonitoringAsync(checkBox.IsChecked == true));
        }
    }

    private async void SaveDumpButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存光模块 Dump",
            Filter = "OptiDiag Dump (*.omodump)|*.omodump",
            DefaultExt = ".omodump",
            AddExtension = true,
            FileName = $"SFF8472-{DateTime.Now:yyyyMMdd-HHmmss}.omodump"
        };
        if (dialog.ShowDialog(this) == true)
        {
            await ExecuteAsync(() => ViewModel.SaveDumpAsync(dialog.FileName));
        }
    }

    private async void CompareDumpButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要比较的光模块 Dump",
            Filter = "OptiDiag Dump (*.omodump)|*.omodump"
        };
        if (dialog.ShowDialog(this) == true)
        {
            await ExecuteAsync(() => ViewModel.CompareDumpAsync(dialog.FileName));
        }
    }

    private async void ExportHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var path = SelectCsvPath($"Measurements-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        if (path is not null)
        {
            await ExecuteAsync(() => ViewModel.ExportHistoryAsync(path));
        }
    }

    private async void ExportRegistersButton_Click(object sender, RoutedEventArgs e)
    {
        var path = SelectCsvPath($"Registers-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        if (path is not null)
        {
            await ExecuteAsync(() => ViewModel.ExportRegistersAsync(path));
        }
    }

    private async void WriteRegisterButton_Click(object sender, RoutedEventArgs e)
    {
        var register = ViewModel.SelectedRegister;
        if (register is null)
        {
            MessageBox.Show(this, "请先选择一个寄存器。", "OptiDiag", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"确认写入 {register.AddressText}？\n旧值：{register.HexValue}\n新值：{WriteValueBox.Text}\n\n模拟器中的写入也会记录到审计日志。",
            "确认寄存器写入",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Yes)
        {
            await ExecuteAsync(() => ViewModel.WriteSelectedRegisterAsync(WriteValueBox.Text));
        }
    }

    private async void RegisterMapGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && GetSelectedRegisterMapCell() is { } cell)
        {
            await OpenRegisterAccessDialogAsync(cell.Register);
        }
    }

    private void RegisterMapGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject) is not { } cell)
        {
            return;
        }

        RegisterMapGrid.CurrentCell = new DataGridCellInfo(cell.DataContext, cell.Column);
        RegisterMapGrid.SelectedCells.Clear();
        cell.IsSelected = true;
        cell.Focus();
    }

    private async void RegisterMapReadMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedRegisterMapCell() is { } cell)
        {
            await ExecuteAsync(async () => await ViewModel.ReadRegisterAsync(cell.Register));
        }
    }

    private async void RegisterMapWriteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedRegisterMapCell() is { } cell)
        {
            await OpenRegisterAccessDialogAsync(cell.Register);
        }
    }

    private RegisterMapCell? GetSelectedRegisterMapCell()
    {
        if (RegisterMapGrid.CurrentItem is not RegisterMapRow row
            || RegisterMapGrid.CurrentCell.Column is not { } column)
        {
            return null;
        }

        return row.GetCell(column.DisplayIndex - 1);
    }

    private async Task OpenRegisterAccessDialogAsync(OptiDiag.Protocols.Abstractions.RegisterValue register)
    {
        var dialog = new RegisterAccessDialog(register)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (dialog.RequestedAction == RegisterDialogAction.Read)
        {
            await ExecuteAsync(async () => await ViewModel.ReadRegisterAsync(register));
            return;
        }

        if (dialog.RequestedAction != RegisterDialogAction.Write)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"确认写入 {register.AddressText}？\n"
            + $"旧值：0x{register.HexValue}\n"
            + $"新值：0x{dialog.RequestedHexValue}\n\n"
            + "该操作会立即发送 I²C 写事务，并记录到审计日志。",
            "确认寄存器写入",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Yes)
        {
            await ExecuteAsync(
                () => ViewModel.WriteRegisterAsync(
                    register,
                    dialog.RequestedHexValue,
                    dialog.WriteUnlocked));
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child)
        where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T parent)
            {
                return parent;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private void ClearTracesButton_Click(object sender, RoutedEventArgs e) => ViewModel.ClearTraces();

    private string? SelectCsvPath(string fileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出 CSV",
            Filter = "CSV 文件 (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = fileName
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
