namespace StarlinkApp.Contracts;

public sealed record CommandRequest(
    string Command,
    string? Argument = null);
