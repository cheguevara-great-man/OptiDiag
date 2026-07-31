using System.Windows;
using OptiDiag.Application;
using OptiDiag.App.ViewModels;
using OptiDiag.I2c.Simulator;
using OptiDiag.Infrastructure;
using OptiDiag.Protocols.Sff8472;

namespace OptiDiag.App;

public partial class App : System.Windows.Application
{
    private MainWindowViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var adapter = new Sff8472Simulator();
        var protocol = new Sff8472Protocol();
        var session = new ModuleSession(adapter, protocol);
        var poller = new PollingEngine(session);
        _viewModel = new MainWindowViewModel(
            session,
            poller,
            adapter,
            new DumpFileService(),
            new DumpComparisonService(),
            new CsvExportService());

        var window = new MainWindow
        {
            DataContext = _viewModel
        };
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }
}
