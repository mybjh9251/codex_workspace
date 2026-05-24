using StarlinkApp.Contracts;

namespace StarlinkApp.Simulation;

public sealed class SimulationEngine
{
    private readonly IReadOnlyList<ScenarioDefinition> _scenarios;
    private readonly string _accountName;
    private string _scenarioKey;
    private double _speedPulse;
    private SpeedTestStatus _speedStatus = SpeedTestStatus.Idle;
    private int _scanProgress;

    public SimulationEngine(AppSettings settings, IReadOnlyList<ScenarioDefinition> scenarios)
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

        if (_scanProgress is > 0 and < 100)
        {
            _scanProgress = Math.Min(100, _scanProgress + 12);
        }

        return CreateSnapshot(scenario, downloadOffset);
    }

    public TelemetrySnapshot SetScenario(string scenarioKey)
    {
        _scenarioKey = FindScenario(scenarioKey).Key;
        _speedStatus = SpeedTestStatus.Idle;
        return GetSnapshot();
    }

    public CommandAck Execute(CommandRequest request)
    {
        if (request.Command.Equals("speed.run", StringComparison.OrdinalIgnoreCase))
        {
            _speedStatus = SpeedTestStatus.Complete;
            var snapshot = CreateOnlineSnapshot(9);

            return new CommandAck(request.Command, true, "Speed test completed from the in-process simulator.", snapshot);
        }

        if (request.Command.Equals("speed.runAdvanced", StringComparison.OrdinalIgnoreCase))
        {
            _speedStatus = SpeedTestStatus.Complete;
            var snapshot = CreateOnlineSnapshot(15);

            return new CommandAck(request.Command, true, "Advanced speed test completed.", snapshot);
        }

        if (request.Command.Equals("connection.retry", StringComparison.OrdinalIgnoreCase))
        {
            _scenarioKey = "connecting";
            return new CommandAck(request.Command, true, "Connection retry started.", GetSnapshot());
        }

        if (request.Command.Equals("setup.continue", StringComparison.OrdinalIgnoreCase))
        {
            _scenarioKey = "connecting";
            return new CommandAck(request.Command, true, "Setup step advanced.", GetSnapshot());
        }

        if (request.Command.Equals("obstruction.scan", StringComparison.OrdinalIgnoreCase))
        {
            _scanProgress = 100;
            var snapshot = GetSnapshot();
            var message = snapshot.Obstruction.Severity == ObstructionSeverity.Clear
                ? "Obstruction scan completed with a clear view."
                : "Obstruction scan found an area to improve.";

            return new CommandAck(request.Command, true, message, snapshot);
        }

        return new CommandAck(request.Command, false, $"Unknown simulator command: {request.Command}", GetSnapshot());
    }

    private ScenarioDefinition FindScenario(string key)
    {
        return _scenarios.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            ?? _scenarios[0];
    }

    private TelemetrySnapshot CreateOnlineSnapshot(double downloadOffset)
    {
        var scenario = _scenarios.FirstOrDefault(s => s.ConnectionState == ConnectionState.Online) ?? _scenarios[0];
        _scenarioKey = scenario.Key;
        return CreateSnapshot(scenario, downloadOffset);
    }

    private TelemetrySnapshot CreateSnapshot(ScenarioDefinition scenario, double downloadOffset)
    {
        var roundedDownload = Math.Max(0, Math.Round(scenario.DownloadMbps + downloadOffset, 1));
        var obstruction = CreateObstruction(scenario);
        var speed = CreateSpeedTest(scenario, roundedDownload);
        var network = CreateNetwork(scenario);

        return scenario.ConnectionState switch
        {
            ConnectionState.Online => new TelemetrySnapshot(
                DateTimeOffset.UtcNow,
                scenario.Key,
                _accountName,
                scenario.ConnectionState,
                roundedDownload,
                scenario.UploadMbps,
                scenario.LatencyMs,
                scenario.DeviceCount,
                obstruction.Severity == ObstructionSeverity.Clear ? 99.88 : 96.21,
                "Online",
                $"{network.ConnectedDeviceCount} devices connected",
                "Run speed test",
                scenario.BackgroundHint,
                obstruction,
                speed,
                network),
            ConnectionState.Connecting => new TelemetrySnapshot(
                DateTimeOffset.UtcNow,
                scenario.Key,
                _accountName,
                scenario.ConnectionState,
                0,
                0,
                0,
                0,
                0,
                "Connecting",
                "Starlink is searching for a link",
                "Connect to WiFi",
                scenario.BackgroundHint,
                obstruction,
                speed,
                network),
            _ => new TelemetrySnapshot(
                DateTimeOffset.UtcNow,
                scenario.Key,
                _accountName,
                scenario.ConnectionState,
                0,
                0,
                0,
                0,
                0,
                "Disconnected",
                "Starlink unreachable",
                "Connect to WiFi",
                scenario.BackgroundHint,
                obstruction,
                speed,
                network)
        };
    }

    private ObstructionSnapshot CreateObstruction(ScenarioDefinition scenario)
    {
        var isObstructed = scenario.BackgroundHint.Equals("obstructed", StringComparison.OrdinalIgnoreCase);
        var severity = isObstructed ? ObstructionSeverity.Partial : ObstructionSeverity.Clear;
        var percent = isObstructed ? 7.4 : 0;
        var cells = CreateObstructionCells(isObstructed);

        return new ObstructionSnapshot(
            severity,
            percent,
            _scanProgress,
            _scanProgress == 100 ? "Just now" : "Not scanned this session",
            isObstructed ? "Move Starlink for a wider clear area." : "Starlink has a clear view of the sky.",
            cells);
    }

    private static IReadOnlyList<ObstructionCell> CreateObstructionCells(bool isObstructed)
    {
        var cells = new List<ObstructionCell>();

        for (var row = 0; row < 5; row++)
        {
            for (var column = 0; column < 5; column++)
            {
                var level = isObstructed && row < 2 && column > 2 ? 2 : 0;
                cells.Add(new ObstructionCell(row, column, level));
            }
        }

        return cells;
    }

    private SpeedTestSnapshot CreateSpeedTest(ScenarioDefinition scenario, double downloadMbps)
    {
        var isOnline = scenario.ConnectionState == ConnectionState.Online;
        var status = isOnline ? _speedStatus : SpeedTestStatus.Idle;
        var upload = isOnline ? scenario.UploadMbps : 0;
        var latency = isOnline ? scenario.LatencyMs : 0;

        return new SpeedTestSnapshot(
            status,
            isOnline ? "This device" : "No active target",
            isOnline ? downloadMbps : 0,
            upload,
            latency,
            isOnline ? Math.Max(1, latency / 5) : 0,
            CreateSpeedSamples(downloadMbps, upload),
            CreateSpeedSegments(downloadMbps, upload, latency));
    }

    private static IReadOnlyList<SpeedSample> CreateSpeedSamples(double downloadMbps, double uploadMbps)
    {
        return
        [
            new("1s", Math.Round(downloadMbps * 0.62, 1), Math.Round(uploadMbps * 0.58, 1)),
            new("2s", Math.Round(downloadMbps * 0.82, 1), Math.Round(uploadMbps * 0.72, 1)),
            new("3s", Math.Round(downloadMbps * 1.03, 1), Math.Round(uploadMbps * 0.94, 1)),
            new("4s", Math.Round(downloadMbps * 0.96, 1), Math.Round(uploadMbps * 1.04, 1)),
            new("5s", Math.Round(downloadMbps, 1), Math.Round(uploadMbps, 1))
        ];
    }

    private static IReadOnlyList<SpeedSegment> CreateSpeedSegments(double downloadMbps, double uploadMbps, int latencyMs)
    {
        return
        [
            new("Device to router", "Local Wi-Fi link", Math.Round(downloadMbps * 1.7, 1), Math.Round(uploadMbps * 2.4, 1), Math.Max(2, latencyMs / 8)),
            new("Router to Starlink", "Dish uplink", Math.Round(downloadMbps * 1.12, 1), Math.Round(uploadMbps * 1.18, 1), Math.Max(8, latencyMs / 3)),
            new("Starlink to internet", "Network edge", Math.Round(downloadMbps, 1), Math.Round(uploadMbps, 1), latencyMs)
        ];
    }

    private static NetworkSnapshot CreateNetwork(ScenarioDefinition scenario)
    {
        if (scenario.ConnectionState != ConnectionState.Online)
        {
            return new NetworkSnapshot(0, "No connected devices", []);
        }

        var devices = new List<NetworkDevice>
        {
            new("This device", "Wi-Fi", "Excellent", "Active now", true, "2h 14m")
        };

        if (scenario.DeviceCount > 1)
        {
            devices.Add(new("Office laptop", "Wi-Fi", scenario.BackgroundHint == "obstructed" ? "Good" : "Excellent", "14 Mbps", true, "48m"));
        }

        if (scenario.DeviceCount > 2)
        {
            devices.Add(new("Living room TV", "Wi-Fi", "Good", "Streaming", true, "1h 02m"));
        }

        return new NetworkSnapshot(devices.Count, $"{devices.Count} devices connected", devices);
    }
}
