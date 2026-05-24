using StarlinkApp.Contracts;

namespace StarlinkApp.ViewModels;

public sealed class SettingsPageViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly int _scenarioCount;
    private string _currentScenarioText = string.Empty;
    private string _connectionStateText = string.Empty;

    public SettingsPageViewModel(AppSettings settings, int scenarioCount)
    {
        _settings = settings;
        _scenarioCount = scenarioCount;
    }

    public string AccountName => _settings.AccountName;

    public string DefaultScenarioText => _settings.DefaultScenarioKey;

    public string RefreshIntervalText => $"{_settings.RefreshIntervalMs} ms";

    public string FileLoggingText => _settings.EnableFileLogging ? "Enabled" : "Disabled";

    public string ScenarioCountText => $"{_scenarioCount} scenarios";

    public string CurrentScenarioText
    {
        get => _currentScenarioText;
        private set => SetProperty(ref _currentScenarioText, value);
    }

    public string ConnectionStateText
    {
        get => _connectionStateText;
        private set => SetProperty(ref _connectionStateText, value);
    }

    public void Update(TelemetrySnapshot snapshot)
    {
        CurrentScenarioText = snapshot.ScenarioKey;
        ConnectionStateText = snapshot.ConnectionState.ToString();
    }
}
