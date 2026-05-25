using StarlinkApp.Contracts;

namespace StarlinkApp.Simulation;

public sealed class SimulationEngine
{
    private readonly IReadOnlyList<ScenarioDefinition> _scenarios;
    private readonly string _accountName;
    private string _scenarioKey;
    private double _speedPulse;
    private SpeedTestStatus _speedStatus = SpeedTestStatus.Idle;
    private int _speedTicksRemaining;
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

        if (_scanProgress is > 0 and < 100)
        {
            _scanProgress = Math.Min(100, _scanProgress + 12);
        }

        if (_speedStatus == SpeedTestStatus.Running && _speedTicksRemaining > 0)
        {
            _speedTicksRemaining--;
            if (_speedTicksRemaining == 0)
            {
                _speedStatus = SpeedTestStatus.Complete;
            }
        }

        return CreateSnapshot(scenario, CreateRuntimeMetrics(scenario));
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
            _speedStatus = SpeedTestStatus.Running;
            _speedTicksRemaining = 2;
            var snapshot = CreateOnlineSnapshot(6, 1.2);

            return new CommandAck(request.Command, true, "Speed test started.", snapshot);
        }

        if (request.Command.Equals("speed.runAdvanced", StringComparison.OrdinalIgnoreCase))
        {
            _speedStatus = SpeedTestStatus.Running;
            _speedTicksRemaining = 2;
            var snapshot = CreateOnlineSnapshot(11, 2.1);

            return new CommandAck(request.Command, true, "Advanced speed test started.", snapshot);
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
            _scanProgress = 15;
            var snapshot = GetSnapshot();
            var message = snapshot.Obstruction.Severity == ObstructionSeverity.Clear
                ? "Obstruction scan started for a clear view check."
                : "Obstruction scan started for a blocked area check.";

            return new CommandAck(request.Command, true, message, snapshot);
        }

        if (request.Command.Equals("feedback.submit", StringComparison.OrdinalIgnoreCase))
        {
            return new CommandAck(request.Command, true, "Feedback submitted to the local simulator.", GetSnapshot());
        }

        return new CommandAck(request.Command, false, $"Unknown simulator command: {request.Command}", GetSnapshot());
    }

    private ScenarioDefinition FindScenario(string key)
    {
        return _scenarios.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            ?? _scenarios[0];
    }

    private TelemetrySnapshot CreateOnlineSnapshot(double downloadBoost, double uploadBoost)
    {
        var scenario = _scenarios.FirstOrDefault(s => s.ConnectionState == ConnectionState.Online) ?? _scenarios[0];
        _scenarioKey = scenario.Key;
        return CreateSnapshot(scenario, CreateRuntimeMetrics(scenario, downloadBoost, uploadBoost));
    }

    private TelemetrySnapshot CreateSnapshot(ScenarioDefinition scenario, RuntimeMetrics metrics)
    {
        var obstruction = CreateObstruction(scenario, metrics.ObstructedPercent);
        var speed = CreateSpeedTest(scenario, metrics);
        var network = CreateNetwork(scenario, metrics);

        return scenario.ConnectionState switch
        {
            ConnectionState.Online => new TelemetrySnapshot(
                DateTimeOffset.UtcNow,
                scenario.Key,
                _accountName,
                scenario.ConnectionState,
                metrics.DownloadMbps,
                metrics.UploadMbps,
                metrics.LatencyMs,
                scenario.DeviceCount,
                metrics.PingSuccessPercent,
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

    private RuntimeMetrics CreateRuntimeMetrics(
        ScenarioDefinition scenario,
        double downloadBoost = 0,
        double uploadBoost = 0)
    {
        if (scenario.ConnectionState != ConnectionState.Online)
        {
            return new RuntimeMetrics(0, 0, 0, 0, 0, CreateObstructedPercent(scenario), 0, 0);
        }

        var download = CreateVariableMetric(scenario.DownloadMbps, 0.035, 2.0, 0, downloadBoost);
        var upload = CreateVariableMetric(scenario.UploadMbps, 0.08, 0.8, 0.37, uploadBoost);
        var latency = CreateVariableLatency(scenario.LatencyMs);
        var jitter = CreateVariableJitter(latency);
        var pingSuccess = CreateVariablePingSuccess(scenario);
        var obstructedPercent = CreateObstructedPercent(scenario);
        var officeLaptopUsage = CreateVariableMetric(14, 0.18, 1.2, 0.68);
        var tvUsage = CreateVariableMetric(21, 0.16, 1.5, 0.91);

        return new RuntimeMetrics(
            download,
            upload,
            latency,
            jitter,
            pingSuccess,
            obstructedPercent,
            officeLaptopUsage,
            tvUsage);
    }

    private double CreateVariableMetric(
        double scenarioValue,
        double amplitudeRatio,
        double minimumAmplitude,
        double phaseOffset,
        double boost = 0)
    {
        if (scenarioValue <= 0)
        {
            return 0;
        }

        var amplitude = Math.Max(minimumAmplitude, scenarioValue * amplitudeRatio);
        var wave = Math.Sin((_speedPulse + phaseOffset) * Math.PI * 2);

        return Math.Max(0, Math.Round(scenarioValue + boost + (wave * amplitude), 1));
    }

    private int CreateVariableLatency(int scenarioLatencyMs)
    {
        if (scenarioLatencyMs <= 0)
        {
            return 0;
        }

        var amplitude = Math.Max(2, scenarioLatencyMs * 0.1);
        var wave = Math.Cos((_speedPulse + 0.17) * Math.PI * 2);

        return Math.Max(1, (int)Math.Round(scenarioLatencyMs + (wave * amplitude)));
    }

    private int CreateVariableJitter(int latencyMs)
    {
        if (latencyMs <= 0)
        {
            return 0;
        }

        var baseJitter = Math.Max(1, latencyMs / 6.0);
        var wave = Math.Sin((_speedPulse + 0.54) * Math.PI * 2);

        return Math.Max(1, (int)Math.Round(baseJitter + (wave * 1.4)));
    }

    private double CreateVariablePingSuccess(ScenarioDefinition scenario)
    {
        var isObstructed = scenario.BackgroundHint.Equals("obstructed", StringComparison.OrdinalIgnoreCase);
        var baseline = isObstructed ? 96.2 : 99.86;
        var amplitude = isObstructed ? 0.55 : 0.08;
        var wave = Math.Sin((_speedPulse + 0.22) * Math.PI * 2);

        return Math.Clamp(Math.Round(baseline + (wave * amplitude), 2), 0, 99.99);
    }

    private double CreateObstructedPercent(ScenarioDefinition scenario)
    {
        var isObstructed = scenario.BackgroundHint.Equals("obstructed", StringComparison.OrdinalIgnoreCase);
        if (!isObstructed)
        {
            return 0;
        }

        var wave = Math.Sin((_speedPulse + 0.41) * Math.PI * 2);

        return Math.Max(0, Math.Round(7.4 + (wave * 0.65), 1));
    }

    private ObstructionSnapshot CreateObstruction(ScenarioDefinition scenario, double obstructedPercent)
    {
        var isObstructed = scenario.BackgroundHint.Equals("obstructed", StringComparison.OrdinalIgnoreCase);
        var severity = isObstructed ? ObstructionSeverity.Partial : ObstructionSeverity.Clear;
        var cells = CreateObstructionCells(isObstructed);

        return new ObstructionSnapshot(
            severity,
            obstructedPercent,
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

    private SpeedTestSnapshot CreateSpeedTest(ScenarioDefinition scenario, RuntimeMetrics metrics)
    {
        var isOnline = scenario.ConnectionState == ConnectionState.Online;
        var status = isOnline ? _speedStatus : SpeedTestStatus.Idle;
        var download = isOnline ? metrics.DownloadMbps : 0;
        var upload = isOnline ? metrics.UploadMbps : 0;
        var latency = isOnline ? metrics.LatencyMs : 0;
        var jitter = isOnline ? metrics.JitterMs : 0;

        return new SpeedTestSnapshot(
            status,
            isOnline ? "This device" : "No active target",
            download,
            upload,
            latency,
            jitter,
            CreateSpeedSamples(download, upload),
            CreateSpeedSegments(download, upload, latency));
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

    private static NetworkSnapshot CreateNetwork(ScenarioDefinition scenario, RuntimeMetrics metrics)
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
            devices.Add(new(
                "Office laptop",
                "Wi-Fi",
                scenario.BackgroundHint == "obstructed" ? "Good" : "Excellent",
                $"{metrics.OfficeLaptopUsageMbps:0.0} Mbps",
                true,
                "48m"));
        }

        if (scenario.DeviceCount > 2)
        {
            devices.Add(new("Living room TV", "Wi-Fi", "Good", $"Streaming {metrics.TvUsageMbps:0.0} Mbps", true, "1h 02m"));
        }

        return new NetworkSnapshot(devices.Count, $"{devices.Count} devices connected", devices);
    }

    private sealed record RuntimeMetrics(
        double DownloadMbps,
        double UploadMbps,
        int LatencyMs,
        int JitterMs,
        double PingSuccessPercent,
        double ObstructedPercent,
        double OfficeLaptopUsageMbps,
        double TvUsageMbps);
}
