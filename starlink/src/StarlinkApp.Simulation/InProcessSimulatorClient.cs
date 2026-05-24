using StarlinkApp.Contracts;

namespace StarlinkApp.Simulation;

public sealed class InProcessSimulatorClient : ISimulatorClient
{
    private readonly IReadOnlyList<ScenarioDefinition> _scenarios;
    private readonly string _accountName;
    private string _scenarioKey;
    private double _speedPulse;

    public InProcessSimulatorClient()
        : this(AppSettings.Default, SimulatorConfigurationLoader.CreateDefaultScenarios())
    {
    }

    public InProcessSimulatorClient(AppSettings settings, IReadOnlyList<ScenarioDefinition> scenarios)
    {
        _accountName = string.IsNullOrWhiteSpace(settings.AccountName)
            ? AppSettings.Default.AccountName
            : settings.AccountName;

        _scenarios = scenarios.Count > 0
            ? scenarios
            : SimulatorConfigurationLoader.CreateDefaultScenarios();

        _scenarioKey = _scenarios.Any(s => s.Key.Equals(settings.DefaultScenarioKey, StringComparison.OrdinalIgnoreCase))
            ? settings.DefaultScenarioKey
            : _scenarios[0].Key;
    }

    public IReadOnlyList<ScenarioDefinition> GetScenarios() => _scenarios;

    public TelemetrySnapshot GetSnapshot()
    {
        var scenario = FindScenario(_scenarioKey);
        _speedPulse = (_speedPulse + 0.15) % 1;

        var downloadOffset = scenario.ConnectionState == ConnectionState.Online
            ? Math.Sin(_speedPulse * Math.PI * 2) * 4
            : 0;

        return CreateSnapshot(scenario, _accountName, downloadOffset);
    }

    public TelemetrySnapshot SetScenario(string scenarioKey)
    {
        _scenarioKey = FindScenario(scenarioKey).Key;
        return GetSnapshot();
    }

    public CommandAck SendCommand(CommandRequest request)
    {
        if (request.Command.Equals("speed.run", StringComparison.OrdinalIgnoreCase))
        {
            _scenarioKey = "online";
            var snapshot = CreateSnapshot(FindScenario(_scenarioKey), _accountName, 9);

            return new CommandAck(
                request.Command,
                true,
                "Speed test completed from the in-process simulator.",
                snapshot);
        }

        if (request.Command.Equals("connection.retry", StringComparison.OrdinalIgnoreCase))
        {
            _scenarioKey = "connecting";
            var snapshot = GetSnapshot();

            return new CommandAck(
                request.Command,
                true,
                "Connection retry started.",
                snapshot);
        }

        if (request.Command.Equals("setup.continue", StringComparison.OrdinalIgnoreCase))
        {
            _scenarioKey = "connecting";
            var snapshot = GetSnapshot();

            return new CommandAck(
                request.Command,
                true,
                "Setup step advanced.",
                snapshot);
        }

        if (request.Command.Equals("obstruction.scan", StringComparison.OrdinalIgnoreCase))
        {
            var snapshot = GetSnapshot();

            return new CommandAck(
                request.Command,
                true,
                snapshot.BackgroundHint.Equals("obstructed", StringComparison.OrdinalIgnoreCase)
                    ? "Obstruction scan found an area to improve."
                    : "Obstruction scan completed with a clear view.",
                snapshot);
        }

        if (request.Command.Equals("speed.runAdvanced", StringComparison.OrdinalIgnoreCase))
        {
            _scenarioKey = "online";
            var snapshot = CreateSnapshot(FindScenario(_scenarioKey), _accountName, 15);

            return new CommandAck(
                request.Command,
                true,
                "Advanced speed test completed.",
                snapshot);
        }

        return new CommandAck(
            request.Command,
            false,
            $"Unknown simulator command: {request.Command}",
            GetSnapshot());
    }

    private ScenarioDefinition FindScenario(string key)
    {
        return _scenarios.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            ?? _scenarios[0];
    }

    private static TelemetrySnapshot CreateSnapshot(ScenarioDefinition scenario, string accountName, double downloadOffset)
    {
        var roundedDownload = Math.Max(0, Math.Round(scenario.DownloadMbps + downloadOffset, 1));

        return scenario.ConnectionState switch
        {
            ConnectionState.Online => new TelemetrySnapshot(
                DateTimeOffset.UtcNow,
                scenario.Key,
                accountName,
                scenario.ConnectionState,
                roundedDownload,
                scenario.UploadMbps,
                scenario.LatencyMs,
                scenario.DeviceCount,
                99.88,
                "Online",
                $"{scenario.DeviceCount} devices connected",
                "Run speed test",
                scenario.BackgroundHint),
            ConnectionState.Connecting => new TelemetrySnapshot(
                DateTimeOffset.UtcNow,
                scenario.Key,
                accountName,
                scenario.ConnectionState,
                0,
                0,
                0,
                0,
                0,
                "Connecting",
                "Starlink is searching for a link",
                "Connect to WiFi",
                scenario.BackgroundHint),
            _ => new TelemetrySnapshot(
                DateTimeOffset.UtcNow,
                scenario.Key,
                accountName,
                scenario.ConnectionState,
                0,
                0,
                0,
                0,
                0,
                "Disconnected",
                "Starlink unreachable",
                "Connect to WiFi",
                scenario.BackgroundHint)
        };
    }
}
