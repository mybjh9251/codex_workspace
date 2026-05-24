using System.Text.Json;
using System.Text.Json.Serialization;

namespace StarlinkApp.Simulation;

public static class SimulatorProtocol
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public const string GetScenarios = "scenarios";
    public const string GetSnapshot = "snapshot";
    public const string SetScenario = "setScenario";
    public const string Command = "command";
    public const string Health = "health";
}
