using StarlinkApp.Contracts;
using System.Windows.Input;

namespace StarlinkApp.ViewModels;

public sealed class NetworkPageViewModel : ViewModelBase
{
    private string _summary = "No connected devices";
    private string _deviceCountText = "0 devices";
    private string _selectedDeviceName = "No device selected";
    private string _selectedDeviceDetailText = "Select a device to inspect local simulator details.";
    private string _deviceActionStatusText = "Device actions are local simulator mock actions.";
    private IReadOnlyList<NetworkDevice> _devices = [];

    public NetworkPageViewModel()
    {
        SelectDeviceCommand = new RelayCommand(SelectDevice);
        PauseDeviceCommand = new RelayCommand(_ => DeviceActionStatusText = $"{SelectedDeviceName} paused in simulator view.");
        RenameDeviceCommand = new RelayCommand(_ => DeviceActionStatusText = $"{SelectedDeviceName} rename flow opened.");
    }

    public ICommand SelectDeviceCommand { get; }

    public ICommand PauseDeviceCommand { get; }

    public ICommand RenameDeviceCommand { get; }

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

    public string SelectedDeviceName
    {
        get => _selectedDeviceName;
        private set => SetProperty(ref _selectedDeviceName, value);
    }

    public string SelectedDeviceDetailText
    {
        get => _selectedDeviceDetailText;
        private set => SetProperty(ref _selectedDeviceDetailText, value);
    }

    public string DeviceActionStatusText
    {
        get => _deviceActionStatusText;
        private set => SetProperty(ref _deviceActionStatusText, value);
    }

    public void Update(TelemetrySnapshot snapshot)
    {
        Summary = snapshot.Network.Summary;
        DeviceCountText = $"{snapshot.Network.ConnectedDeviceCount} connected devices";
        Devices = snapshot.Network.Devices;

        if (Devices.Count > 0 && Devices.All(device => device.Name != SelectedDeviceName))
        {
            SelectDevice(Devices[0]);
        }
    }

    private void SelectDevice(object? parameter)
    {
        if (parameter is not NetworkDevice device)
        {
            return;
        }

        SelectedDeviceName = device.Name;
        SelectedDeviceDetailText = $"{device.ConnectionType} / {device.SignalQuality} / {device.UsageText} / connected {device.ConnectedDuration}";
        DeviceActionStatusText = $"{device.Name} selected.";
    }
}
