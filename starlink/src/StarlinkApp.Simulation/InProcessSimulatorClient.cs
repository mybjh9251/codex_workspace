using StarlinkApp.Contracts;

namespace StarlinkApp.Simulation;

public sealed class InProcessSimulatorClient : ISimulatorClient
{
    private readonly List<ScenarioDefinition> _scenarios =
    [
        new(
            "online",
            "Online",
            "Starlink is connected and primary rows are active.",
            ConnectionState.Online,
            139,
            18,
            31,
            3,
            "online"),
        new(
            "connecting",
            "Connecting",
            "Starlink is searching for connectivity.",
            ConnectionState.Connecting,
            0,
            0,
            0,
            0,
            "connecting"),
        new(
            "disconnected",
            "Disconnected",
            "Starlink is unreachable and needs user action.",
            ConnectionState.Disconnected,
            0,
            0,
            0,
            0,
            "disconnected")
    ];

    private string _scenarioKey = "online";
    private double _speedPulse;

    public IReadOnlyList<ScenarioDefinition> GetScenarios() => _scenarios;

    public TelemetrySnapshot GetSnapshot()
    {
        var scenario = FindScenario(_scenarioKey);
        _speedPulse = (_speedPulse + 0.15) % 1;

        var downloadOffset = scenario.ConnectionState == ConnectionState.Online
            ? Math.Sin(_speedPulse * Math.PI * 2) * 4
            : 0;

        return CreateSnapshot(scenario, downloadOffset);
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
            var snapshot = CreateSnapshot(FindScenario(_scenarioKey), 9);

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

    private static TelemetrySnapshot CreateSnapshot(ScenarioDefinition scenario, double downloadOffset)
    {
        var roundedDownload = Math.Max(0, Math.Round(scenario.DownloadMbps + downloadOffset, 1));

        return scenario.ConnectionState switch
        {
            ConnectionState.Online => new TelemetrySnapshot(
                DateTimeOffset.UtcNow,
                scenario.Key,
                "Starlink",
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
                "Select Starlink",
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
                "Select Starlink",
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
