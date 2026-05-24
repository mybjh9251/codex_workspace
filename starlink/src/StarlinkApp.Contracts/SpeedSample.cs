namespace StarlinkApp.Contracts;

public sealed record SpeedSample(
    string Label,
    double DownloadMbps,
    double UploadMbps);
