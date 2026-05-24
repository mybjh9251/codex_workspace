using System.Windows;
using System.Windows.Threading;
using StarlinkApp.Contracts;
using StarlinkApp.Services;
using StarlinkApp.Simulation;
using StarlinkApp.ViewModels;

namespace StarlinkApp;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();

        var runtimeRoot = AppContext.BaseDirectory;
        var configuration = SimulatorConfigurationLoader.Load(runtimeRoot);
        IAppLogService logService = configuration.Settings.EnableFileLogging
            ? new FileLogService(runtimeRoot)
            : NullLogService.Instance;

        foreach (var warning in configuration.Warnings)
        {
            logService.Write("config.warning", warning);
        }

        _viewModel = new MainWindowViewModel(
            new InProcessSimulatorClient(configuration.Settings, configuration.Scenarios),
            logService,
            configuration.Settings,
            configuration.Warnings);

        DataContext = _viewModel;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(configuration.Settings.RefreshIntervalMs)
        };
        _timer.Tick += (_, _) => _viewModel.RefreshSnapshot();
        _timer.Start();
    }
}
