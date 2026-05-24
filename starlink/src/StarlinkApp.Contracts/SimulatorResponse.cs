namespace StarlinkApp.Contracts;

public sealed record SimulatorResponse(
    bool Accepted,
    string Message,
    TelemetrySnapshot? Snapshot = null,
    IReadOnlyList<ScenarioDefinition>? Scenarios = null);
