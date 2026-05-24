using System.Windows.Input;
using StarlinkApp.Contracts;

namespace StarlinkApp.ViewModels;

public sealed class AdvancedSpeedPageViewModel : ViewModelBase
{
    private string _statusText = "Ready to test";
    private string _targetDeviceName = "No active target";
    private string _downloadText = "0.0 Mbps";
    private string _uploadText = "0.0 Mbps";
    private string _latencyText = "--";
    private string _jitterText = "--";
    private IReadOnlyList<SpeedSample> _samples = [];
    private IReadOnlyList<SpeedSampleBarViewModel> _sampleBars = [];
    private IReadOnlyList<SpeedSegment> _segments = [];

    public AdvancedSpeedPageViewModel(ICommand runAdvancedSpeedTestCommand)
    {
        RunAdvancedSpeedTestCommand = runAdvancedSpeedTestCommand;
    }

    public ICommand RunAdvancedSpeedTestCommand { get; }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string TargetDeviceName
    {
        get => _targetDeviceName;
        private set => SetProperty(ref _targetDeviceName, value);
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

    public string LatencyText
    {
        get => _latencyText;
        private set => SetProperty(ref _latencyText, value);
    }

    public string JitterText
    {
        get => _jitterText;
        private set => SetProperty(ref _jitterText, value);
    }

    public IReadOnlyList<SpeedSample> Samples
    {
        get => _samples;
        private set => SetProperty(ref _samples, value);
    }

    public IReadOnlyList<SpeedSampleBarViewModel> SampleBars
    {
        get => _sampleBars;
        private set => SetProperty(ref _sampleBars, value);
    }

    public IReadOnlyList<SpeedSegment> Segments
    {
        get => _segments;
        private set => SetProperty(ref _segments, value);
    }

    public void Update(TelemetrySnapshot snapshot)
    {
        var speedTest = snapshot.SpeedTest;

        StatusText = speedTest.Status switch
        {
            SpeedTestStatus.Running => "Testing network path",
            SpeedTestStatus.Complete => "Advanced speed test complete",
            _ => "Ready to test"
        };
        TargetDeviceName = speedTest.TargetDeviceName;
        DownloadText = $"{speedTest.DownloadMbps:0.0} Mbps";
        UploadText = $"{speedTest.UploadMbps:0.0} Mbps";
        LatencyText = speedTest.LatencyMs > 0 ? $"{speedTest.LatencyMs} ms" : "--";
        JitterText = speedTest.JitterMs > 0 ? $"{speedTest.JitterMs} ms jitter" : "--";
        Samples = speedTest.Samples;
        SampleBars = speedTest.Samples
            .Select(sample => new SpeedSampleBarViewModel(
                sample.Label,
                Math.Clamp(sample.DownloadMbps * 1.2, 2, 175),
                Math.Clamp(sample.UploadMbps * 5, 2, 175)))
            .ToArray();
        Segments = speedTest.Segments;
    }
}

public sealed record SpeedSampleBarViewModel(
    string Label,
    double DownloadBarWidth,
    double UploadBarWidth);
