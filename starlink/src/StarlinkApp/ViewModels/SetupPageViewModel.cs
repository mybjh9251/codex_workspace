using System.Windows.Input;
using StarlinkApp.Contracts;

namespace StarlinkApp.ViewModels;

public sealed class SetupPageViewModel : ViewModelBase
{
    private string _setupStepText = "CONNECTING";
    private string _setupHintText = string.Empty;
    private string _connectionStateText = string.Empty;

    public SetupPageViewModel(ICommand continueSetupCommand)
    {
        ContinueSetupCommand = continueSetupCommand;
    }

    public ICommand ContinueSetupCommand { get; }

    public string SetupStepText
    {
        get => _setupStepText;
        private set => SetProperty(ref _setupStepText, value);
    }

    public string SetupHintText
    {
        get => _setupHintText;
        private set => SetProperty(ref _setupHintText, value);
    }

    public string ConnectionStateText
    {
        get => _connectionStateText;
        private set => SetProperty(ref _connectionStateText, value);
    }

    public void Update(TelemetrySnapshot snapshot)
    {
        ConnectionStateText = snapshot.ConnectionState.ToString();

        if (snapshot.ConnectionState == ConnectionState.Online)
        {
            SetupStepText = "CONNECTED";
            SetupHintText = "Starlink is online. Setup can continue with Wi-Fi and obstruction checks.";
            return;
        }

        if (snapshot.ConnectionState == ConnectionState.Connecting)
        {
            SetupStepText = "SEARCHING";
            SetupHintText = "Keep Starlink powered while the simulator searches for a link.";
            return;
        }

        SetupStepText = "DISCONNECTED";
        SetupHintText = "Plug in Starlink and keep the app open while the simulator searches for a link.";
    }
}
