namespace StarlinkApp.Contracts;

public sealed record SpeedSegment(
    string Name,
    string Description,
    double DownloadMbps,
    double UploadMbps,
    int LatencyMs);
