using StarlinkApp.Contracts;

namespace StarlinkApp.ViewModels;

public sealed class NetworkPageViewModel : ViewModelBase
{
    private string _summary = "No connected devices";
    private string _deviceCountText = "0 devices";
    private IReadOnlyList<NetworkDevice> _devices = [];

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string DeviceCountText
    {
        get => _deviceCountText;
        private set => SetProperty(ref _deviceCountText, value);
    }

    public IReadOnlyList<NetworkDevice> Devices
    {
        get => _devices;
        private set => SetProperty(ref _devices, value);
    }

    public void Update(TelemetrySnapshot snapshot)
    {
        Summary = snapshot.Network.Summary;
        DeviceCountText = $"{snapshot.Network.ConnectedDeviceCount} connected devices";
        Devices = snapshot.Network.Devices;
    }
}
