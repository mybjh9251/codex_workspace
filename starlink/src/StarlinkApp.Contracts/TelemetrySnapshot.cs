namespace StarlinkApp.Contracts;

public sealed record TelemetrySnapshot(
    DateTimeOffset Timestamp,
    string ScenarioKey,
    string AccountName,
    ConnectionState ConnectionState,
    double DownloadMbps,
    double UploadMbps,
    int LatencyMs,
    int DeviceCount,
    double PingSuccessPercent,
    string StatusTitle,
    string StatusSubtitle,
    string PrimaryActionLabel,
    string BackgroundHint);
