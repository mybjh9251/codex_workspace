using System.Windows.Input;
using StarlinkApp.Contracts;

namespace StarlinkApp.ViewModels;

public sealed class HomePageViewModel : ViewModelBase
{
    private readonly ICommand _runSpeedTestCommand;
    private readonly ICommand _retryConnectionCommand;
    private bool _isOnline;
    private string _accountName = string.Empty;
    private string _statusTitle = string.Empty;
    private string _statusSubtitle = string.Empty;
    private string _primaryActionLabel = string.Empty;
    private string _pingSuccessText = "--";
    private string _latencyText = "--";
    private string _deviceCountText = "0 devices";

    public HomePageViewModel(
        ICommand navigateCommand,
        ICommand runSpeedTestCommand,
        ICommand retryConnectionCommand)
    {
        NavigateCommand = navigateCommand;
        _runSpeedTestCommand = runSpeedTestCommand;
        _retryConnectionCommand = retryConnectionCommand;
    }

    public ICommand NavigateCommand { get; }

    public ICommand PrimaryActionCommand => _isOnline ? _runSpeedTestCommand : _retryConnectionCommand;

    public string AccountName
    {
        get => _accountName;
        private set => SetProperty(ref _accountName, value);
    }

    public string StatusTitle
    {
        get => _statusTitle;
        private set => SetProperty(ref _statusTitle, value);
    }

    public string StatusSubtitle
    {
        get => _statusSubtitle;
        private set => SetProperty(ref _statusSubtitle, value);
    }

    public string PrimaryActionLabel
    {
        get => _primaryActionLabel;
        private set => SetProperty(ref _primaryActionLabel, value);
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

    public string DeviceCountText
    {
        get => _deviceCountText;
        private set => SetProperty(ref _deviceCountText, value);
    }

    public void Update(TelemetrySnapshot snapshot)
    {
        var isOnline = snapshot.ConnectionState == ConnectionState.Online;
        if (SetProperty(ref _isOnline, isOnline, nameof(IsOnline)))
        {
            OnPropertyChanged(nameof(PrimaryActionCommand));
        }

        AccountName = snapshot.AccountName;
        StatusTitle = snapshot.StatusTitle;
        StatusSubtitle = snapshot.StatusSubtitle;
        PrimaryActionLabel = snapshot.PrimaryActionLabel;
        PingSuccessText = snapshot.PingSuccessPercent > 0 ? $"{snapshot.PingSuccessPercent:0.00} %" : "--";
        LatencyText = snapshot.LatencyMs > 0 ? $"{snapshot.LatencyMs} ms" : "--";
        DeviceCountText = $"{snapshot.Network.ConnectedDeviceCount} devices";
    }

    public bool IsOnline => _isOnline;
}
