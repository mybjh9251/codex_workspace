using System.Windows.Input;
using StarlinkApp.Contracts;

namespace StarlinkApp.ViewModels;

public sealed class SettingsPageViewModel : ViewModelBase
{
    private readonly int _scenarioCount;
    private readonly Func<AppSettings, string> _saveSettings;
    private AppSettings _settings;
    private string _accountNameInput = string.Empty;
    private string _defaultScenarioInput = string.Empty;
    private string _refreshIntervalInput = string.Empty;
    private bool _enableFileLogging;
    private string _simulatorModeInput = string.Empty;
    private string _simulatorEndpointInput = string.Empty;
    private string _currentScenarioText = string.Empty;
    private string _connectionStateText = string.Empty;
    private string _saveStatusText = "Changes are saved to settings.json beside the executable.";

    public SettingsPageViewModel(AppSettings settings, int scenarioCount, Func<AppSettings, string> saveSettings)
    {
        _saveSettings = saveSettings;
        _settings = settings;
        _scenarioCount = scenarioCount;
        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        ApplySettings(settings);
    }

    public ICommand SaveSettingsCommand { get; }

    public string AccountNameInput
    {
        get => _accountNameInput;
        set => SetProperty(ref _accountNameInput, value);
    }

    public string DefaultScenarioInput
    {
        get => _defaultScenarioInput;
        set => SetProperty(ref _defaultScenarioInput, value);
    }

    public string RefreshIntervalInput
    {
        get => _refreshIntervalInput;
        set => SetProperty(ref _refreshIntervalInput, value);
    }

    public bool EnableFileLogging
    {
        get => _enableFileLogging;
        set => SetProperty(ref _enableFileLogging, value);
    }

    public string SimulatorModeInput
    {
        get => _simulatorModeInput;
        set => SetProperty(ref _simulatorModeInput, value);
    }

    public string SimulatorEndpointInput
    {
        get => _simulatorEndpointInput;
        set => SetProperty(ref _simulatorEndpointInput, value);
    }

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

    public string SaveStatusText
    {
        get => _saveStatusText;
        private set => SetProperty(ref _saveStatusText, value);
    }

    public void Update(TelemetrySnapshot snapshot)
    {
        CurrentScenarioText = snapshot.ScenarioKey;
        ConnectionStateText = snapshot.ConnectionState.ToString();
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        AccountNameInput = settings.AccountName;
        DefaultScenarioInput = settings.DefaultScenarioKey;
        RefreshIntervalInput = settings.RefreshIntervalMs.ToString();
        EnableFileLogging = settings.EnableFileLogging;
        SimulatorModeInput = settings.SimulatorMode;
        SimulatorEndpointInput = settings.SimulatorEndpoint;
    }

    private void SaveSettings()
    {
        var refreshInterval = int.TryParse(RefreshIntervalInput, out var parsedRefreshInterval)
            ? parsedRefreshInterval
            : _settings.RefreshIntervalMs;

        var updatedSettings = _settings with
        {
            AccountName = AccountNameInput,
            DefaultScenarioKey = DefaultScenarioInput,
            RefreshIntervalMs = refreshInterval,
            EnableFileLogging = EnableFileLogging,
            SimulatorMode = SimulatorModeInput,
            SimulatorEndpoint = SimulatorEndpointInput
        };

        SaveStatusText = _saveSettings(updatedSettings);
        ApplySettings(updatedSettings);
    }
}
