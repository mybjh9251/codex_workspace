using StarlinkApp.Contracts;
using StarlinkApp.Simulation;

namespace StarlinkApp.Tests;

public sealed class SimulationEngineTests
{
    [Fact]
    public void ObstructedScenarioExposesObstructionModel()
    {
        var engine = new SimulationEngine(AppSettings.Default, SimulatorConfigurationLoader.CreateDefaultScenarios());

        var snapshot = engine.SetScenario("obstructed");

        Assert.Equal(ObstructionSeverity.Partial, snapshot.Obstruction.Severity);
        Assert.True(snapshot.Obstruction.ObstructedPercent > 0);
        Assert.Contains(snapshot.Obstruction.Cells, cell => cell.Level > 0);
    }

    [Fact]
    public void AdvancedSpeedCommandPopulatesSamplesAndSegments()
    {
        var engine = new SimulationEngine(AppSettings.Default, SimulatorConfigurationLoader.CreateDefaultScenarios());

        var ack = engine.Execute(new CommandRequest("speed.runAdvanced"));

        Assert.True(ack.Accepted);
        Assert.Equal(SpeedTestStatus.Running, ack.Snapshot.SpeedTest.Status);
        Assert.NotEmpty(ack.Snapshot.SpeedTest.Samples);
        Assert.True(ack.Snapshot.SpeedTest.Segments.Count >= 3);

        engine.GetSnapshot();
        var completedSnapshot = engine.GetSnapshot();

        Assert.Equal(SpeedTestStatus.Complete, completedSnapshot.SpeedTest.Status);
    }

    [Fact]
    public void OnlineSnapshotExposesNetworkDevices()
    {
        var engine = new SimulationEngine(AppSettings.Default, SimulatorConfigurationLoader.CreateDefaultScenarios());

        var snapshot = engine.GetSnapshot();

        Assert.Equal(snapshot.DeviceCount, snapshot.Network.ConnectedDeviceCount);
        Assert.Contains(snapshot.Network.Devices, device => device.Name == "This device");
    }

    [Fact]
    public void OnlineSnapshotLatencyVariesAcrossRefreshes()
    {
        var engine = new SimulationEngine(AppSettings.Default, SimulatorConfigurationLoader.CreateDefaultScenarios());

        var latencies = Enumerable
            .Range(0, 5)
            .Select(_ => engine.GetSnapshot().LatencyMs)
            .ToArray();

        Assert.True(latencies.Distinct().Count() > 1);
        Assert.All(latencies, latency => Assert.InRange(latency, 28, 34));
    }

    [Fact]
    public void OnlineSnapshotVariesScenarioBasedRuntimeMetrics()
    {
        var engine = new SimulationEngine(AppSettings.Default, SimulatorConfigurationLoader.CreateDefaultScenarios());

        var snapshots = Enumerable
            .Range(0, 6)
            .Select(_ => engine.GetSnapshot())
            .ToArray();

        var uploadValues = snapshots.Select(snapshot => snapshot.UploadMbps).ToArray();
        var speedTestUploadValues = snapshots.Select(snapshot => snapshot.SpeedTest.UploadMbps).ToArray();
        var pingValues = snapshots.Select(snapshot => snapshot.PingSuccessPercent).ToArray();
        var officeUsageValues = snapshots
            .Select(snapshot => snapshot.Network.Devices.First(device => device.Name == "Office laptop").UsageText)
            .ToArray();

        Assert.True(uploadValues.Distinct().Count() > 1);
        Assert.True(speedTestUploadValues.Distinct().Count() > 1);
        Assert.True(pingValues.Distinct().Count() > 1);
        Assert.True(officeUsageValues.Distinct().Count() > 1);
        Assert.All(uploadValues, upload => Assert.InRange(upload, 14, 22));
    }

    [Fact]
    public void TcpSimulatorClientFallsBackUntilWireProtocolExists()
    {
        var client = new TcpSimulatorClient(
            new Uri("tcp://127.0.0.1:5517"),
            AppSettings.Default,
            SimulatorConfigurationLoader.CreateDefaultScenarios());

        var ack = client.SendCommand(new CommandRequest("speed.run"));

        Assert.True(ack.Accepted);
        Assert.Contains("fallback handled command", ack.Message);
    }

    [Fact]
    public async Task TcpSimulatorClientUsesServerWhenAvailable()
    {
        var settings = AppSettings.Default with { SimulatorEndpoint = "tcp://127.0.0.1:0" };
        var server = new TcpSimulatorServer(
            settings,
            SimulatorConfigurationLoader.CreateDefaultScenarios(),
            new SimulatorEndpoint("127.0.0.1", 0));
        using var cts = new CancellationTokenSource();

        var serverTask = server.RunAsync(cts.Token);
        SpinWait.SpinUntil(() => server.BoundPort > 0, TimeSpan.FromSeconds(3));

        var client = new TcpSimulatorClient(
            new SimulatorEndpoint("127.0.0.1", server.BoundPort),
            AppSettings.Default,
            SimulatorConfigurationLoader.CreateDefaultScenarios());

        var ack = client.SendCommand(new CommandRequest("feedback.submit", "Looks good."));

        cts.Cancel();
        await serverTask;

        Assert.True(ack.Accepted);
        Assert.DoesNotContain("fallback handled command", ack.Message);
    }
}
