using System.Text.Json;
using System.Text.Json.Serialization;
using StarlinkApp.Contracts;

namespace StarlinkApp.Simulation;

public static class SimulatorConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static SimulatorConfiguration Load(string runtimeRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeRoot);

        var warnings = new List<string>();
        var settings = LoadSettings(runtimeRoot, warnings);
        var scenarios = LoadScenarios(runtimeRoot, warnings);

        if (!scenarios.Any(s => s.Key.Equals(settings.DefaultScenarioKey, StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add($"Default scenario '{settings.DefaultScenarioKey}' was not found; using '{scenarios[0].Key}'.");
            settings = settings with { DefaultScenarioKey = scenarios[0].Key };
        }

        return new SimulatorConfiguration(settings, scenarios, warnings);
    }

    public static IReadOnlyList<ScenarioDefinition> CreateDefaultScenarios()
    {
        return
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
                "disconnected"),
            new(
                "obstructed",
                "Obstructed",
                "A partial obstruction is reducing clear sky visibility.",
                ConnectionState.Online,
                82,
                9,
                58,
                2,
                "obstructed")
        ];
    }

    private static AppSettings LoadSettings(string runtimeRoot, ICollection<string> warnings)
    {
        var path = Path.Combine(runtimeRoot, "settings.json");

        if (!File.Exists(path))
        {
            warnings.Add("settings.json was not found; using built-in app settings.");
            return AppSettings.Default;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions);

            if (settings is null)
            {
                warnings.Add("settings.json was empty; using built-in app settings.");
                return AppSettings.Default;
            }

            return settings with
            {
                AccountName = string.IsNullOrWhiteSpace(settings.AccountName) ? AppSettings.Default.AccountName : settings.AccountName,
                DefaultScenarioKey = string.IsNullOrWhiteSpace(settings.DefaultScenarioKey) ? AppSettings.Default.DefaultScenarioKey : settings.DefaultScenarioKey,
                RefreshIntervalMs = settings.RefreshIntervalMs < 250 ? AppSettings.Default.RefreshIntervalMs : settings.RefreshIntervalMs
            };
        }
        catch (JsonException ex)
        {
            warnings.Add($"settings.json could not be parsed; using built-in app settings. {ex.Message}");
            return AppSettings.Default;
        }
    }

    private static IReadOnlyList<ScenarioDefinition> LoadScenarios(string runtimeRoot, ICollection<string> warnings)
    {
        var path = Path.Combine(runtimeRoot, "scenarios.json");

        if (!File.Exists(path))
        {
            warnings.Add("scenarios.json was not found; using built-in scenario presets.");
            return CreateDefaultScenarios();
        }

        try
        {
            var scenarios = JsonSerializer.Deserialize<List<ScenarioDefinition>>(File.ReadAllText(path), JsonOptions);

            if (scenarios is null || scenarios.Count == 0)
            {
                warnings.Add("scenarios.json did not contain scenarios; using built-in scenario presets.");
                return CreateDefaultScenarios();
            }

            var validScenarios = scenarios
                .Where(s => !string.IsNullOrWhiteSpace(s.Key))
                .ToList();

            if (validScenarios.Count == 0)
            {
                warnings.Add("scenarios.json did not contain valid scenario keys; using built-in scenario presets.");
                return CreateDefaultScenarios();
            }

            return validScenarios;
        }
        catch (JsonException ex)
        {
            warnings.Add($"scenarios.json could not be parsed; using built-in scenario presets. {ex.Message}");
            return CreateDefaultScenarios();
        }
    }
}
