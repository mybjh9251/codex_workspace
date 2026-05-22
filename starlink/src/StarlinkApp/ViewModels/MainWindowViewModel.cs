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
    private TelemetrySnapshot _snapshot;

    public MainWindowViewModel(ISimulatorClient simulator, IAppLogService logService)
    {
        _simulator = simulator;
        _logService = logService;
        _snapshot = simulator.GetSnapshot();

        Scenarios = _simulator.GetScenarios();
        ActivityLog =
        [
            "Simulator attached: in-process adapter",
            $"Scenario loaded: {_snapshot.StatusTitle}"
        ];

        ChangeScenarioCommand = new RelayCommand(ChangeScenario);
        RunSpeedTestCommand = new RelayCommand(_ => SendCommand("speed.run"));
        RetryConnectionCommand = new RelayCommand(_ => SendCommand("connection.retry"));
    }

    public IReadOnlyList<ScenarioDefinition> Scenarios { get; }

    public ObservableCollection<string> ActivityLog { get; }

    public ICommand ChangeScenarioCommand { get; }

    public ICommand RunSpeedTestCommand { get; }

    public ICommand RetryConnectionCommand { get; }

    public string AccountName => _snapshot.AccountName;

    public string StatusTitle => _snapshot.StatusTitle;

    public string StatusSubtitle => _snapshot.StatusSubtitle;

    public string PrimaryActionLabel => _snapshot.PrimaryActionLabel;

    public string DownloadText => $"{_snapshot.DownloadMbps:0} Mbps";

    public string UploadText => $"{_snapshot.UploadMbps:0} Mbps upload";

    public string LatencyText => _snapshot.LatencyMs > 0 ? $"{_snapshot.LatencyMs} ms" : "--";

    public string DeviceCountText => $"{_snapshot.DeviceCount} devices";

    public string PingSuccessText => _snapshot.PingSuccessPercent > 0 ? $"{_snapshot.PingSuccessPercent:0.00} %" : "--";

    public bool IsOnline => _snapshot.ConnectionState == ConnectionState.Online;

    public bool IsDisconnected => _snapshot.ConnectionState == ConnectionState.Disconnected;

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
        OnPropertyChanged(nameof(IsOnline));
        OnPropertyChanged(nameof(IsDisconnected));
    }
}
