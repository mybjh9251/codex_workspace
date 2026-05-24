namespace StarlinkApp.Contracts;

public sealed record AppSettings(
    string AccountName,
    string DefaultScenarioKey,
    int RefreshIntervalMs,
    bool EnableFileLogging,
    string SimulatorMode,
    string SimulatorEndpoint)
{
    public static AppSettings Default { get; } = new(
        "Starlink",
        "online",
        1000,
        true,
        "InProcess",
        "tcp://127.0.0.1:5517");
}
