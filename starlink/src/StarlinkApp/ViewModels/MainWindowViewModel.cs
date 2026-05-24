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

        Scenarios = _simulator.GetScenarios();
        NavigationItems =
        [
            new PageNavigationItem("home", "Home", "Connection status"),
            new PageNavigationItem("setup", "Setup", "Install flow"),
            new PageNavigationItem("obstructions", "Obstructions", "Sky view"),
            new PageNavigationItem("speed", "Speed", "Speed test")
        ];

        ActivityLog =
        [
            "Simulator attached: in-process adapter",
            $"Scenario loaded: {_snapshot.StatusTitle}"
        ];

        ChangeScenarioCommand = new RelayCommand(ChangeScenario);
        NavigateCommand = new RelayCommand(Navigate);
        RunSpeedTestCommand = new RelayCommand(_ => SendCommand("speed.run"));
        RunAdvancedSpeedTestCommand = new RelayCommand(_ => SendCommand("speed.runAdvanced"));
        RetryConnectionCommand = new RelayCommand(_ => SendCommand("connection.retry"));
        ContinueSetupCommand = new RelayCommand(_ => SendCommand("setup.continue"));
        CheckObstructionsCommand = new RelayCommand(_ => SendCommand("obstruction.scan"));

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

    public ICommand PrimaryActionCommand => IsOnline ? RunSpeedTestCommand : RetryConnectionCommand;

    public string AccountName => _snapshot.AccountName;

    public string CurrentPageTitle => NavigationItems.First(i => i.Key == _currentPageKey).DisplayName;

    public string CurrentPageDescription => NavigationItems.First(i => i.Key == _currentPageKey).Description;

    public string StatusTitle => _snapshot.StatusTitle;

    public string StatusSubtitle => _snapshot.StatusSubtitle;

    public string PrimaryActionLabel => _snapshot.PrimaryActionLabel;

    public string DownloadText => $"{_snapshot.DownloadMbps:0} Mbps";

    public string UploadText => $"{_snapshot.UploadMbps:0} Mbps upload";

    public string LatencyText => _snapshot.LatencyMs > 0 ? $"{_snapshot.LatencyMs} ms" : "--";

    public string DeviceCountText => $"{_snapshot.DeviceCount} devices";

    public string PingSuccessText => _snapshot.PingSuccessPercent > 0 ? $"{_snapshot.PingSuccessPercent:0.00} %" : "--";

    public string RefreshIntervalText => $"{_settings.RefreshIntervalMs} ms refresh";

    public string SetupStepText => IsOnline ? "CONNECTED" : "CONNECTING";

    public string SetupHintText => IsOnline
        ? "Starlink is online. Setup can continue with Wi-Fi and obstruction checks."
        : "Plug in Starlink and keep the app open while the simulator searches for a link.";

    public string ObstructionStatusText => _snapshot.BackgroundHint.Equals("obstructed", StringComparison.OrdinalIgnoreCase)
        ? "Obstruction detected"
        : "Clear view";

    public string ObstructionDetailText => _snapshot.BackgroundHint.Equals("obstructed", StringComparison.OrdinalIgnoreCase)
        ? "A portion of the sky view is blocked. Move Starlink for a wider clear area."
        : "Starlink has a clear view of the sky.";

    public string ObstructionPercentText => _snapshot.BackgroundHint.Equals("obstructed", StringComparison.OrdinalIgnoreCase)
        ? "7.4 % obstructed"
        : "0.0 % obstructed";

    public string SpeedTargetText => IsOnline ? "This device" : "No active target";

    public double DownloadBarWidth => Math.Clamp(_snapshot.DownloadMbps, 0, 180);

    public double UploadBarWidth => Math.Clamp(_snapshot.UploadMbps * 5, 0, 180);

    public bool IsOnline => _snapshot.ConnectionState == ConnectionState.Online;

    public bool IsDisconnected => _snapshot.ConnectionState == ConnectionState.Disconnected;

    public bool IsHomePageVisible => _currentPageKey == "home";

    public bool IsSetupPageVisible => _currentPageKey == "setup";

    public bool IsObstructionsPageVisible => _currentPageKey == "obstructions";

    public bool IsSpeedPageVisible => _currentPageKey == "speed";

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
        OnPropertyChanged(nameof(AccountName));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusSubtitle));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        OnPropertyChanged(nameof(DownloadText));
        OnPropertyChanged(nameof(UploadText));
        OnPropertyChanged(nameof(LatencyText));
        OnPropertyChanged(nameof(DeviceCountText));
        OnPropertyChanged(nameof(PingSuccessText));
        OnPropertyChanged(nameof(PrimaryActionCommand));
        OnPropertyChanged(nameof(SetupStepText));
        OnPropertyChanged(nameof(SetupHintText));
        OnPropertyChanged(nameof(ObstructionStatusText));
        OnPropertyChanged(nameof(ObstructionDetailText));
        OnPropertyChanged(nameof(ObstructionPercentText));
        OnPropertyChanged(nameof(SpeedTargetText));
        OnPropertyChanged(nameof(DownloadBarWidth));
        OnPropertyChanged(nameof(UploadBarWidth));
        OnPropertyChanged(nameof(IsOnline));
        OnPropertyChanged(nameof(IsDisconnected));
    }
}
