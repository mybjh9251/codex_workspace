using System.Windows.Input;
using StarlinkApp.Contracts;

namespace StarlinkApp.ViewModels;

public sealed class SpeedPageViewModel : ViewModelBase
{
    private string _downloadText = "0 Mbps";
    private string _uploadText = "0 Mbps";
    private string _latencyText = "--";
    private string _speedTargetText = "No active target";
    private double _downloadBarWidth;
    private double _uploadBarWidth;

    public SpeedPageViewModel(ICommand runAdvancedSpeedTestCommand)
    {
        RunAdvancedSpeedTestCommand = runAdvancedSpeedTestCommand;
    }

    public ICommand RunAdvancedSpeedTestCommand { get; }

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

    public string LatencyText
    {
        get => _latencyText;
        private set => SetProperty(ref _latencyText, value);
    }

    public string SpeedTargetText
    {
        get => _speedTargetText;
        private set => SetProperty(ref _speedTargetText, value);
    }

    public double DownloadBarWidth
    {
        get => _downloadBarWidth;
        private set => SetProperty(ref _downloadBarWidth, value);
    }

    public double UploadBarWidth
    {
        get => _uploadBarWidth;
        private set => SetProperty(ref _uploadBarWidth, value);
    }

    public void Update(TelemetrySnapshot snapshot)
    {
        DownloadText = $"{snapshot.SpeedTest.DownloadMbps:0} Mbps";
        UploadText = $"{snapshot.SpeedTest.UploadMbps:0} Mbps";
        LatencyText = snapshot.SpeedTest.LatencyMs > 0 ? $"{snapshot.SpeedTest.LatencyMs} ms" : "--";
        SpeedTargetText = snapshot.SpeedTest.TargetDeviceName;
        DownloadBarWidth = Math.Clamp(snapshot.SpeedTest.DownloadMbps, 0, 180);
        UploadBarWidth = Math.Clamp(snapshot.SpeedTest.UploadMbps * 5, 0, 180);
    }
}
