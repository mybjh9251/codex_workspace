namespace StarlinkApp.Contracts;

public sealed record ScenarioDefinition(
    string Key,
    string DisplayName,
    string Description,
    ConnectionState ConnectionState,
    double DownloadMbps,
    double UploadMbps,
    int LatencyMs,
    int DeviceCount,
    string BackgroundHint);
