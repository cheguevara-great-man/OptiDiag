using System.Windows;
using OptiDiag.Application;
using OptiDiag.App.Services;
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
        var preferencesService = new UserPreferencesService();
        var preferences = preferencesService.LoadAsync().GetAwaiter().GetResult();
        ThemeManager.Apply(preferences);
        var sff8472Simulator = new Sff8472Simulator();
        var cmis53Simulator = new CmisSimulator(CmisSimulatorRevision.Cmis53);
        var cmis54Simulator = new CmisSimulator(CmisSimulatorRevision.Cmis54);
        var adapter = new SwitchableI2cAdapter(
            new Dictionary<string, OptiDiag.I2c.Abstractions.II2cAdapter>
            {
                ["simulator-sff8472"] = sff8472Simulator,
                ["simulator-cmis53"] = cmis53Simulator,
                ["simulator-cmis54"] = cmis54Simulator
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
            new CsvExportService(),
            preferencesService,
            preferences,
            new GitHubReleaseUpdateService());

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
