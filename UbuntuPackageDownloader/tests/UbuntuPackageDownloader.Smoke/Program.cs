using UbuntuPackageDownloader;

var architecture = args.Length > 0 ? args[0] : "amd64";
var repository = args.Length > 1 && args[1].Equals("official", StringComparison.OrdinalIgnoreCase)
    ? "Ubuntu Official"
    : "Kakao Mirror (Ubuntu fallback)";
var requestedPackages = (args.Length > 2 ? args[2] : "curl")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var download = args.Length > 3 && args[3].Equals("download", StringComparison.OrdinalIgnoreCase);
var outputFolder = Path.Combine(
    Path.GetTempPath(),
    $"UbuntuPackageDownloaderNativeSmoke_{Guid.NewGuid():N}");

try
{
    var request = new DownloadRequest(
        "24.04 noble",
        "noble",
        architecture,
        ["main", "universe"],
        ["release", "updates", "security"],
        repository,
        requestedPackages,
        outputFolder,
        IncludeRecommends: false);

    var result = await new AptResolver().ResolveAsync(
        request,
        Console.WriteLine,
        CancellationToken.None);

    var missingRequestedPackages = requestedPackages
        .Where(requestedPackage => !result.Packages.Any(package => package.Package == requestedPackage))
        .ToArray();
    if (missingRequestedPackages.Length > 0)
    {
        throw new InvalidOperationException(
            $"Requested package(s) not resolved: {string.Join(", ", missingRequestedPackages)}.");
    }

    if (result.Unresolved.Count > 0)
    {
        var details = string.Join(
            Environment.NewLine,
            result.Unresolved.Select(item => $"{item.Package}: {item.Reason}"));
        throw new InvalidOperationException($"Unresolved dependencies were found:{Environment.NewLine}{details}");
    }

    Console.WriteLine(
        $"PASS Architecture={architecture}, Repository={repository}, Packages={result.Packages.Count}, OfficialFallback={result.UsedOfficialFallback}");

    if (download)
    {
        var rows = await new DebDownloader().DownloadAsync(
            request,
            result.Packages,
            _ => { },
            Console.WriteLine,
            CancellationToken.None);

        if (rows.Any(row => row.Status == "Failed"))
        {
            throw new InvalidOperationException("One or more .deb files failed to download.");
        }

        if (rows.Any(row => !File.Exists(Path.Combine(outputFolder, row.FileName))))
        {
            throw new InvalidOperationException("A downloaded .deb file was not written directly under the output folder.");
        }

        var checksumPath = Path.Combine(outputFolder, "checksums.sha256");
        ReportWriter.WriteChecksums(checksumPath, rows);
        if (File.ReadAllText(checksumPath).Contains("debs/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Checksum output still contains the removed debs/ subfolder.");
        }

        Console.WriteLine($"DOWNLOAD_PASS Files={rows.Count}, Output={outputFolder}");
    }
}
finally
{
    if (Directory.Exists(outputFolder))
    {
        Directory.Delete(outputFolder, recursive: true);
    }
}
