using StarlinkApp.Contracts;

namespace StarlinkApp.ViewModels;

public sealed class StatisticsPageViewModel : ViewModelBase
{
    private string _stateText = "Online";
    private string _pingSuccessText = "--";
    private string _latencyText = "--";
    private string _downloadText = "0.0 Mbps";
    private string _uploadText = "0.0 Mbps";
    private string _devicesText = "0 devices";
    private string _obstructionText = "Clear";
    private string _scenarioText = "online";
    private string _summaryText = "Waiting for simulator telemetry.";

    public string StateText
    {
        get => _stateText;
        private set => SetProperty(ref _stateText, value);
    }

    public string PingSuccessText
    {
        get => _pingSuccessText;
        private set => SetProperty(ref _pingSuccessText, value);
    }

    public string LatencyText
    {
        get => _latencyText;
        private set => SetProperty(ref _latencyText, value);
    }

    public string DownloadText
    {
        get => _downloadText;
        private set => SetProperty(ref _downloadText, value);
    }

    public string UploadText
    {
        get => _uploadText;
        private set => SetProperty(ref _uploadText, value);
    }

    public string DevicesText
    {
        get => _devicesText;
        private set => SetProperty(ref _devicesText, value);
    }

    public string ObstructionText
    {
        get => _obstructionText;
        private set => SetProperty(ref _obstructionText, value);
    }

    public string ScenarioText
    {
        get => _scenarioText;
        private set => SetProperty(ref _scenarioText, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public void Update(TelemetrySnapshot snapshot)
    {
        StateText = snapshot.StatusTitle;
        PingSuccessText = snapshot.PingSuccessPercent > 0 ? $"{snapshot.PingSuccessPercent:0.00} %" : "--";
        LatencyText = snapshot.LatencyMs > 0 ? $"{snapshot.LatencyMs} ms" : "--";
        DownloadText = $"{snapshot.DownloadMbps:0.0} Mbps";
        UploadText = $"{snapshot.UploadMbps:0.0} Mbps";
        DevicesText = $"{snapshot.DeviceCount} devices";
        ObstructionText = snapshot.Obstruction.Severity == ObstructionSeverity.Clear
            ? "Clear view"
            : $"{snapshot.Obstruction.ObstructedPercent:0.0} % obstructed";
        ScenarioText = snapshot.ScenarioKey;
        SummaryText = snapshot.ConnectionState == ConnectionState.Online
            ? "Connection quality is being sampled by the local simulator."
            : "Statistics will populate after Starlink reconnects.";
    }
}
