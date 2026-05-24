using StarlinkApp.Contracts;
using StarlinkApp.Simulation;

namespace StarlinkApp.Tests;

public sealed class InProcessSimulatorClientTests
{
    [Fact]
    public void DefaultSnapshotIsOnline()
    {
        var simulator = new InProcessSimulatorClient();

        var snapshot = simulator.GetSnapshot();

        Assert.Equal(ConnectionState.Online, snapshot.ConnectionState);
        Assert.Equal("Online", snapshot.StatusTitle);
        Assert.True(snapshot.DownloadMbps > 0);
        Assert.Equal(ObstructionSeverity.Clear, snapshot.Obstruction.Severity);
        Assert.NotEmpty(snapshot.SpeedTest.Samples);
        Assert.NotEmpty(snapshot.Network.Devices);
    }

    [Fact]
    public void ScenarioCanSwitchToDisconnected()
    {
        var simulator = new InProcessSimulatorClient();

        var snapshot = simulator.SetScenario("disconnected");

        Assert.Equal(ConnectionState.Disconnected, snapshot.ConnectionState);
        Assert.Equal("Disconnected", snapshot.StatusTitle);
        Assert.Equal("Connect to WiFi", snapshot.PrimaryActionLabel);
    }

    [Fact]
    public void SpeedRunCommandReturnsAcceptedAck()
    {
        var simulator = new InProcessSimulatorClient();

        var ack = simulator.SendCommand(new CommandRequest("speed.run"));

        Assert.True(ack.Accepted);
        Assert.Equal("speed.run", ack.Command);
        Assert.Equal(ConnectionState.Online, ack.Snapshot.ConnectionState);
        Assert.Equal(SpeedTestStatus.Running, ack.Snapshot.SpeedTest.Status);

        simulator.GetSnapshot();
        var completedSnapshot = simulator.GetSnapshot();

        Assert.Equal(SpeedTestStatus.Complete, completedSnapshot.SpeedTest.Status);
    }

    [Fact]
    public void ObstructionScenarioCanRunScanCommand()
    {
        var simulator = new InProcessSimulatorClient();
        simulator.SetScenario("obstructed");

        var ack = simulator.SendCommand(new CommandRequest("obstruction.scan"));

        Assert.True(ack.Accepted);
        Assert.Equal("obstruction.scan", ack.Command);
        Assert.Equal("obstructed", ack.Snapshot.BackgroundHint);
        Assert.InRange(ack.Snapshot.Obstruction.ScanProgressPercent, 1, 99);

        TelemetrySnapshot completedSnapshot = ack.Snapshot;
        for (var i = 0; i < 8; i++)
        {
            completedSnapshot = simulator.GetSnapshot();
        }

        Assert.Equal(100, completedSnapshot.Obstruction.ScanProgressPercent);
    }
}
