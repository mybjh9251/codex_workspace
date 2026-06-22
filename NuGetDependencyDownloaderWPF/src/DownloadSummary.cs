namespace NuGetDependencyDownloaderWPF
{
    public class DownloadSummary
    {
        public int RootCount { get; set; }
        public int DependencyCount { get; set; }
        public int DownloadedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }

        public override string ToString()
        {
            return $"Root: {RootCount}, Dependency: {DependencyCount}, Downloaded: {DownloadedCount}, Skipped: {SkippedCount}, Failed: {FailedCount}";
        }
    }
}
