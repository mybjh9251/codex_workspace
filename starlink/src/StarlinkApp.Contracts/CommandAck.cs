namespace StarlinkApp.Contracts;

public sealed record CommandAck(
    string Command,
    bool Accepted,
    string Message,
    TelemetrySnapshot Snapshot);
