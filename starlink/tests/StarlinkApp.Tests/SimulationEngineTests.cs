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
