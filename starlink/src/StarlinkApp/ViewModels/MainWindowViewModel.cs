using System.Collections.ObjectModel;
using System.Windows.Input;
using StarlinkApp.Contracts;
using StarlinkApp.Services;
using StarlinkApp.Simulation;

namespace StarlinkApp.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ISimulatorClient _simulator;
    private readonly IAppLogService _logService;
    private readonly AppSettings _settings;
    private TelemetrySnapshot _snapshot;
    private string _currentPageKey = "home";

    public MainWindowViewModel(
        ISimulatorClient simulator,
        IAppLogService logService,
        AppSettings settings,
        IReadOnlyList<string>? startupWarnings = null)
    {
        _simulator = simulator;
        _logService = logService;
        _settings = settings;
        _snapshot = simulator.GetSnapshot();

        ChangeScenarioCommand = new RelayCommand(ChangeScenario);
        NavigateCommand = new RelayCommand(Navigate);
        RunSpeedTestCommand = new RelayCommand(_ => SendCommand("speed.run"));
        RunAdvancedSpeedTestCommand = new RelayCommand(_ => SendCommand("speed.runAdvanced"));
        RetryConnectionCommand = new RelayCommand(_ => SendCommand("connection.retry"));
        ContinueSetupCommand = new RelayCommand(_ => SendCommand("setup.continue"));
        CheckObstructionsCommand = new RelayCommand(_ => SendCommand("obstruction.scan"));

        Scenarios = _simulator.GetScenarios();
        NavigationItems =
        [
            new PageNavigationItem("home", "Home", "Connection status"),
            new PageNavigationItem("setup", "Setup", "Install flow"),
            new PageNavigationItem("obstructions", "Obstructions", "Sky view"),
            new PageNavigationItem("speed", "Speed", "Speed test"),
            new PageNavigationItem("advancedSpeed", "Advanced Speed", "Network path"),
            new PageNavigationItem("network", "Network", "Connected devices"),
            new PageNavigationItem("settings", "Settings", "Runtime config")
        ];

        Home = new HomePageViewModel(NavigateCommand, RunSpeedTestCommand, RetryConnectionCommand);
        Setup = new SetupPageViewModel(ContinueSetupCommand);
        Obstructions = new ObstructionsPageViewModel(CheckObstructionsCommand);
        Speed = new SpeedPageViewModel(RunAdvancedSpeedTestCommand);
        AdvancedSpeed = new AdvancedSpeedPageViewModel(RunAdvancedSpeedTestCommand);
        Network = new NetworkPageViewModel();
        Settings = new SettingsPageViewModel(settings, Scenarios.Count);
        UpdatePages();

        ActivityLog =
        [
            "Simulator attached: in-process adapter",
            $"Scenario loaded: {_snapshot.StatusTitle}"
        ];

        if (startupWarnings is not null)
        {
            foreach (var warning in startupWarnings)
            {
                AddActivity($"Config fallback: {warning}");
            }
        }
    }

    public IReadOnlyList<ScenarioDefinition> Scenarios { get; }

    public IReadOnlyList<PageNavigationItem> NavigationItems { get; }

    public ObservableCollection<string> ActivityLog { get; }

    public ICommand ChangeScenarioCommand { get; }

    public ICommand NavigateCommand { get; }

    public ICommand RunSpeedTestCommand { get; }

    public ICommand RunAdvancedSpeedTestCommand { get; }

    public ICommand RetryConnectionCommand { get; }

    public ICommand ContinueSetupCommand { get; }

    public ICommand CheckObstructionsCommand { get; }

    public HomePageViewModel Home { get; }

    public SetupPageViewModel Setup { get; }

    public ObstructionsPageViewModel Obstructions { get; }

    public SpeedPageViewModel Speed { get; }

    public AdvancedSpeedPageViewModel AdvancedSpeed { get; }

    public NetworkPageViewModel Network { get; }

    public SettingsPageViewModel Settings { get; }

    public string CurrentPageTitle => NavigationItems.First(i => i.Key == _currentPageKey).DisplayName;

    public string CurrentPageDescription => NavigationItems.First(i => i.Key == _currentPageKey).Description;

    public string RefreshIntervalText => $"{_settings.RefreshIntervalMs} ms refresh";

    public bool IsHomePageVisible => _currentPageKey == "home";

    public bool IsSetupPageVisible => _currentPageKey == "setup";

    public bool IsObstructionsPageVisible => _currentPageKey == "obstructions";

    public bool IsSpeedPageVisible => _currentPageKey == "speed";

    public bool IsAdvancedSpeedPageVisible => _currentPageKey == "advancedSpeed";

    public bool IsNetworkPageVisible => _currentPageKey == "network";

    public bool IsSettingsPageVisible => _currentPageKey == "settings";

    public void RefreshSnapshot()
    {
        _snapshot = _simulator.GetSnapshot();
        RaiseSnapshotProperties();
    }

    private void ChangeScenario(object? parameter)
    {
        if (parameter is not string scenarioKey)
        {
            return;
        }

        _snapshot = _simulator.SetScenario(scenarioKey);
        AddActivity($"Scenario changed: {_snapshot.StatusTitle}");
        _logService.Write("scenario.changed", scenarioKey);
        RaiseSnapshotProperties();
    }

    private void Navigate(object? parameter)
    {
        if (parameter is not string pageKey || NavigationItems.All(i => i.Key != pageKey))
        {
            return;
        }

        _currentPageKey = pageKey;
        AddActivity($"Page selected: {CurrentPageTitle}");
        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(CurrentPageDescription));
        OnPropertyChanged(nameof(IsHomePageVisible));
        OnPropertyChanged(nameof(IsSetupPageVisible));
        OnPropertyChanged(nameof(IsObstructionsPageVisible));
        OnPropertyChanged(nameof(IsSpeedPageVisible));
        OnPropertyChanged(nameof(IsAdvancedSpeedPageVisible));
        OnPropertyChanged(nameof(IsNetworkPageVisible));
        OnPropertyChanged(nameof(IsSettingsPageVisible));
    }

    private void SendCommand(string command)
    {
        var ack = _simulator.SendCommand(new CommandRequest(command));
        _snapshot = ack.Snapshot;

        AddActivity(ack.Message);
        _logService.Write(ack.Accepted ? "command.accepted" : "command.rejected", ack.Message);
        RaiseSnapshotProperties();
    }

    private void AddActivity(string message)
    {
        ActivityLog.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");

        while (ActivityLog.Count > 8)
        {
            ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }
    }

    private void RaiseSnapshotProperties()
    {
        UpdatePages();
    }

    private void UpdatePages()
    {
        Home.Update(_snapshot);
        Setup.Update(_snapshot);
        Obstructions.Update(_snapshot);
        Speed.Update(_snapshot);
        AdvancedSpeed.Update(_snapshot);
        Network.Update(_snapshot);
        Settings.Update(_snapshot);
    }
}
