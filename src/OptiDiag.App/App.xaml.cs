using System.Windows;
using OptiDiag.Application;
using OptiDiag.App.ViewModels;
using OptiDiag.I2c.Simulator;
using OptiDiag.Infrastructure;
using OptiDiag.Protocols.Abstractions;
using OptiDiag.Protocols.Cmis;
using OptiDiag.Protocols.Sff8472;

namespace OptiDiag.App;

public partial class App : System.Windows.Application
{
    private MainWindowViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var sff8472Simulator = new Sff8472Simulator();
        var cmisSimulator = new CmisSimulator();
        var adapter = new SwitchableI2cAdapter(
            new Dictionary<string, OptiDiag.I2c.Abstractions.II2cAdapter>
            {
                ["simulator-sff8472"] = sff8472Simulator,
                ["simulator-cmis"] = cmisSimulator
            },
            "simulator-sff8472");
        IOpticalModuleProtocol[] protocols =
        [
            new Sff8472Protocol(),
            new CmisProtocol()
        ];
        var session = new ModuleSession(adapter, protocols);
        var poller = new PollingEngine(session);
        _viewModel = new MainWindowViewModel(
            session,
            poller,
            adapter,
            sff8472Simulator,
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
