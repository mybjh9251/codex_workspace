namespace StarlinkApp.Contracts;

public sealed record SimulatorRequest(
    string Type,
    string? Command = null,
    string? Argument = null);
