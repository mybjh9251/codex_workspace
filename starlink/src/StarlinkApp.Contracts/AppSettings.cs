namespace StarlinkApp.Contracts;

public sealed record AppSettings(
    string AccountName,
    string DefaultScenarioKey,
    int RefreshIntervalMs,
    bool EnableFileLogging)
{
    public static AppSettings Default { get; } = new(
        "Starlink",
        "online",
        1000,
        true);
}
