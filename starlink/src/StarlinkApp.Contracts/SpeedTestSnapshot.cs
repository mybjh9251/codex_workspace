namespace StarlinkApp.Contracts;

public sealed record SpeedTestSnapshot(
    SpeedTestStatus Status,
    string TargetDeviceName,
    double DownloadMbps,
    double UploadMbps,
    int LatencyMs,
    int JitterMs,
    IReadOnlyList<SpeedSample> Samples,
    IReadOnlyList<SpeedSegment> Segments);
