using StarlinkApp.Contracts;
using StarlinkApp.Simulation;

namespace StarlinkApp.Tests;

public sealed class SimulatorConfigurationLoaderTests
{
    [Fact]
    public void MissingFilesUseBuiltInDefaultsAndWarnings()
    {
        var runtimeRoot = CreateTempDirectory();

        try
        {
            var configuration = SimulatorConfigurationLoader.Load(runtimeRoot);

            Assert.Equal("online", configuration.Settings.DefaultScenarioKey);
            Assert.Contains(configuration.Scenarios, s => s.Key == "online");
            Assert.Contains(configuration.Warnings, w => w.Contains("settings.json", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(configuration.Warnings, w => w.Contains("scenarios.json", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(runtimeRoot, recursive: true);
        }
    }

    [Fact]
    public void JsonFilesOverrideBuiltInDefaults()
    {
        var runtimeRoot = CreateTempDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(runtimeRoot, "settings.json"),
                """
                {
                  "accountName": "Lab Starlink",
                  "defaultScenarioKey": "lab",
                  "refreshIntervalMs": 750,
                  "enableFileLogging": false,
                  "simulatorMode": "Tcp",
                  "simulatorEndpoint": "tcp://127.0.0.1:6600"
                }
                """);

            File.WriteAllText(
                Path.Combine(runtimeRoot, "scenarios.json"),
                """
                [
                  {
                    "key": "lab",
                    "displayName": "Lab Link",
                    "description": "A custom lab scenario.",
                    "connectionState": "Online",
                    "downloadMbps": 42,
                    "uploadMbps": 6,
                    "latencyMs": 44,
                    "deviceCount": 1,
                    "backgroundHint": "online"
                  }
                ]
                """);

            var configuration = SimulatorConfigurationLoader.Load(runtimeRoot);
            var simulator = new InProcessSimulatorClient(configuration.Settings, configuration.Scenarios);
            var snapshot = simulator.GetSnapshot();

            Assert.Empty(configuration.Warnings);
            Assert.Equal("Lab Starlink", configuration.Settings.AccountName);
            Assert.Equal(750, configuration.Settings.RefreshIntervalMs);
            Assert.False(configuration.Settings.EnableFileLogging);
            Assert.Equal("Tcp", configuration.Settings.SimulatorMode);
            Assert.Equal("tcp://127.0.0.1:6600", configuration.Settings.SimulatorEndpoint);
            Assert.Equal("lab", snapshot.ScenarioKey);
            Assert.Equal("Lab Starlink", snapshot.AccountName);
            Assert.Equal(ConnectionState.Online, snapshot.ConnectionState);
        }
        finally
        {
            Directory.Delete(runtimeRoot, recursive: true);
        }
    }

    [Fact]
    public void SaveSettingsWritesRuntimeSettingsJson()
    {
        var runtimeRoot = CreateTempDirectory();

        try
        {
            var settings = AppSettings.Default with
            {
                AccountName = "Saved Starlink",
                DefaultScenarioKey = "obstructed",
                RefreshIntervalMs = 1500,
                EnableFileLogging = false,
                SimulatorMode = "Tcp",
                SimulatorEndpoint = "tcp://127.0.0.1:5517"
            };

            SimulatorConfigurationLoader.SaveSettings(runtimeRoot, settings);
            var configuration = SimulatorConfigurationLoader.Load(runtimeRoot);

            Assert.Equal("Saved Starlink", configuration.Settings.AccountName);
            Assert.Equal("obstructed", configuration.Settings.DefaultScenarioKey);
            Assert.Equal(1500, configuration.Settings.RefreshIntervalMs);
            Assert.False(configuration.Settings.EnableFileLogging);
            Assert.Equal("Tcp", configuration.Settings.SimulatorMode);
        }
        finally
        {
            Directory.Delete(runtimeRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"starlink-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
