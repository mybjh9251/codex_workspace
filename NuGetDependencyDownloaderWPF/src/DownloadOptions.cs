namespace NuGetDependencyDownloaderWPF
{
    public class DownloadOptions
    {
        public string PackageSource { get; set; } = "https://api.nuget.org/v3/index.json";
        public string TargetFramework { get; set; } = "net8.0";
        public string OutputFolder { get; set; } = "packages";
        public bool OverwriteExisting { get; set; }
        public int MaxParallelism { get; set; } = 5;
        public int RetryCount { get; set; } = 2;
    }
}
