namespace StarlinkApp.Contracts;

public sealed record NetworkDevice(
    string Name,
    string ConnectionType,
    string SignalQuality,
    string UsageText,
    bool IsOnline,
    string ConnectedDuration);
