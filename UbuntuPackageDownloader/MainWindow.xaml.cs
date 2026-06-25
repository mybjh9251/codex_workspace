using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace UbuntuPackageDownloader;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ReportRow> _reportRows = [];
    private readonly AptResolver _aptResolver = new();
    private readonly DebDownloader _downloader = new();
    private bool _isRunning;

    public ObservableCollection<ReportRow> ReportRows => _reportRows;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        InitializeDefaults();
    }

    private void InitializeDefaults()
    {
        UbuntuVersionComboBox.ItemsSource = new[]
        {
            "24.04 noble",
            "22.04 jammy",
            "20.04 focal",
            "18.04 bionic",
            "16.04 xenial"
        };
        UbuntuVersionComboBox.SelectedIndex = 0;

        ArchitectureComboBox.ItemsSource = new[]
        {
            "amd64",
            "i386",
            "arm64",
            "armhf",
            "ppc64el",
            "s390x",
            "riscv64"
        };
        ArchitectureComboBox.SelectedIndex = 0;

        ComponentsComboBox.ItemsSource = new[] { "main universe" };
        ComponentsComboBox.SelectedIndex = 0;
        PocketsComboBox.ItemsSource = new[] { "release updates security" };
        PocketsComboBox.SelectedIndex = 0;
        ServerRepositoryComboBox.ItemsSource = new[]
        {
            "Kakao Mirror (Ubuntu fallback)",
            "Ubuntu Official"
        };
        ServerRepositoryComboBox.SelectedIndex = 0;
        OutputFolderTextBox.Text = Path.Combine(AppContext.BaseDirectory, "Download");
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync();
    }

    private void OpenOutputFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var outputFolder = OutputFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            System.Windows.MessageBox.Show(
                this,
                "Output Folder를 입력하세요.",
                "Output Folder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            Directory.CreateDirectory(outputFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Path.GetFullPath(outputFolder)}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                $"폴더를 열 수 없습니다.{Environment.NewLine}{ex.Message}",
                "Output Folder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import package list",
            Filter = "Package list (*.txt;*.csv)|*.txt;*.csv|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            PackageListTextBox.Text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export package list",
            Filter = "Text file (*.txt)|*.txt|CSV file (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = "packages.txt"
        };

        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, PackageListTextBox.Text, Encoding.UTF8);
        }
    }

    private async Task RunAsync()
    {
        if (_isRunning)
        {
            return;
        }

        var request = BuildRequest();
        if (request.Packages.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "Package를 한 개 이상 입력하세요.", "Input required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isRunning = true;
        SetBusy(true);
        _reportRows.Clear();
        ProgressBar.Value = 0;
        LogTextBox.Clear();

        try
        {
            AppendLog("Resolving packages from Ubuntu repository metadata.");
            AppendLog($"Ubuntu={request.Codename}, Arch={request.Architecture}, Repository={request.ServerRepository}, Components={string.Join(' ', request.Components)}, Pockets={string.Join(' ', request.Pockets)}");

            var result = await _aptResolver.ResolveAsync(request, AppendLog, CancellationToken.None);
            foreach (var package in result.Packages)
            {
                AddReportRow(ReportRow.FromPackage("Resolved", package));
            }

            WriteResolutionReports(request, result, downloadStarted: false);
            ProgressBar.Value = 35;
            StatusTextBlock.Text = $"Resolved {result.Packages.Count} .deb file(s).";

            if (result.Unresolved.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{result.Unresolved.Count} dependency group(s) could not be resolved. No .deb files were downloaded. See unresolved-dependencies.csv.");
            }

            AppendLog("Downloading .deb files.");
            var downloadRows = await _downloader.DownloadAsync(
                request,
                result.Packages,
                row => Dispatcher.Invoke(() =>
                {
                    AddReportRow(row);
                    ProgressBar.Value = Math.Min(100, ProgressBar.Value + (65.0 / Math.Max(1, result.Packages.Count)));
                }),
                AppendLog,
                CancellationToken.None);

            WriteDownloadReports(request, result, downloadRows);
            ProgressBar.Value = 100;
            StatusTextBlock.Text = $"Download complete. {downloadRows.Count(r => r.Status is "Downloaded" or "Skipped" or "Verified")} handled, {downloadRows.Count(r => r.Status == "Failed")} failed.";
            AppendLog("Download complete.");
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "Failed";
            AppendLog($"ERROR: {ex.Message}");
            System.Windows.MessageBox.Show(this, ex.Message, "Ubuntu Package Downloader", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isRunning = false;
            SetBusy(false);
        }
    }

    private DownloadRequest BuildRequest()
    {
        var versionText = UbuntuVersionComboBox.Text.Trim();
        var codename = ParseCodename(versionText);
        var architecture = ArchitectureComboBox.Text.Trim();
        var components = SplitWords(ComponentsComboBox.Text);
        var pockets = SplitWords(PocketsComboBox.Text);
        var serverRepository = ServerRepositoryComboBox.Text.Trim();
        var packages = ParsePackageList(PackageListTextBox.Text);
        var outputFolder = Path.Combine(AppContext.BaseDirectory, "Download");

        return new DownloadRequest(
            versionText,
            codename,
            architecture,
            components,
            pockets,
            serverRepository,
            packages,
            outputFolder,
            IncludeRecommendsCheckBox.IsChecked == true);
    }

    private static string ParseCodename(string versionText)
    {
        var parts = versionText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 1 ? parts[0] : parts[^1];
    }

    private static IReadOnlyList<string> SplitWords(string value)
    {
        return value
            .Split([' ', ',', ';', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ParsePackageList(string value)
    {
        return value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('#')[0].Trim())
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private void WriteResolutionReports(DownloadRequest request, ResolveResult result, bool downloadStarted)
    {
        Directory.CreateDirectory(request.OutputFolder);
        ReportWriter.WriteManifest(request, result, request.OutputFolder, downloadStarted, []);
        ReportWriter.WriteDownloadReport(Path.Combine(request.OutputFolder, "download-report.csv"), _reportRows);
        ReportWriter.WriteUnresolved(Path.Combine(request.OutputFolder, "unresolved-dependencies.csv"), result.Unresolved);
    }

    private void WriteDownloadReports(DownloadRequest request, ResolveResult result, IReadOnlyList<ReportRow> downloadRows)
    {
        Directory.CreateDirectory(request.OutputFolder);
        ReportWriter.WriteManifest(request, result, request.OutputFolder, downloadStarted: true, downloadRows);
        ReportWriter.WriteDownloadReport(Path.Combine(request.OutputFolder, "download-report.csv"), downloadRows);
        ReportWriter.WriteChecksums(Path.Combine(request.OutputFolder, "checksums.sha256"), downloadRows);
        ReportWriter.WriteUnresolved(Path.Combine(request.OutputFolder, "unresolved-dependencies.csv"), result.Unresolved);
    }

    private void SetBusy(bool isBusy)
    {
        DownloadButton.IsEnabled = !isBusy;
    }

    private void AddReportRow(ReportRow row)
    {
        row.Number = _reportRows.Count + 1;
        _reportRows.Add(row);
    }

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogTextBox.ScrollToEnd();
        });
    }
}

public sealed record DownloadRequest(
    string UbuntuVersion,
    string Codename,
    string Architecture,
    IReadOnlyList<string> Components,
    IReadOnlyList<string> Pockets,
    string ServerRepository,
    IReadOnlyList<string> Packages,
    string OutputFolder,
    bool IncludeRecommends);

public sealed record ResolvedPackage(
    string Package,
    string Version,
    string Architecture,
    string FileName,
    string SourceUrl,
    long Size,
    string Reason);

public sealed record ResolveResult(
    IReadOnlyList<ResolvedPackage> Packages,
    IReadOnlyList<UnresolvedDependency> Unresolved,
    IReadOnlyList<RepositorySource> Sources,
    bool UsedOfficialFallback);

public sealed record RepositorySource(string Name, string BaseUrl, string Suite, string Components, string Architecture);

public sealed record UnresolvedDependency(string Package, string RequiredBy, string Constraint, string Reason, string Candidates);

public sealed class ReportRow
{
    public int Number { get; set; }
    public string Status { get; init; } = "";
    public string Package { get; init; } = "";
    public string Version { get; init; } = "";
    public string Architecture { get; init; } = "";
    public string Reason { get; init; } = "";
    public string FileName { get; init; } = "";
    public string SourceUrl { get; init; } = "";
    public long Size { get; init; }
    public string SHA256 { get; init; } = "";
    public string Message { get; init; } = "";

    public static ReportRow FromPackage(string status, ResolvedPackage package)
    {
        return new ReportRow
        {
            Status = status,
            Package = package.Package,
            Version = package.Version,
            Architecture = package.Architecture,
            Reason = package.Reason,
            FileName = package.FileName,
            SourceUrl = package.SourceUrl,
            Size = package.Size,
            Message = "native repository metadata resolver"
        };
    }
}

public sealed class AptResolver
{
    private static readonly Regex DependencyRegex = new(
        "^(?<name>[a-z0-9][a-z0-9+.-]*)(?::[a-z0-9-]+)?\\s*(?:\\((?<op>>=|<=|=|>>|<<|>|<)\\s*(?<version>[^)]+)\\))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly HttpClient _httpClient = new(new SocketsHttpHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.None,
        MaxConnectionsPerServer = 8,
        PooledConnectionLifetime = TimeSpan.FromMinutes(10)
    });

    public async Task<ResolveResult> ResolveAsync(
        DownloadRequest request,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(request.OutputFolder);
        var isPortsArchitecture = IsPortsArchitecture(request.Architecture);
        var kakaoSources = BuildSources(request, "https://mirror.kakao.com/ubuntu/", official: false);
        var officialSources = BuildSources(
            request,
            isPortsArchitecture
                ? "https://ports.ubuntu.com/ubuntu-ports/"
                : "https://archive.ubuntu.com/ubuntu/",
            official: true,
            usePortsRepository: isPortsArchitecture);
        var oldReleaseSources = BuildSources(
            request,
            isPortsArchitecture
                ? "https://old-releases.ports.ubuntu.com/ubuntu-ports/"
                : "https://old-releases.ubuntu.com/ubuntu/",
            official: true,
            usePortsRepository: isPortsArchitecture);

        var preferKakao = request.ServerRepository.StartsWith("Kakao", StringComparison.OrdinalIgnoreCase);
        var usedOfficial = false;
        var usedOldReleases = false;
        MetadataLoadResult? metadata = null;
        if (preferKakao)
        {
            metadata = await TryLoadMetadataAsync(request, kakaoSources, log, cancellationToken);
        }

        if (metadata is null)
        {
            log(preferKakao
                ? "Kakao mirror metadata failed. Retrying with Ubuntu official servers."
                : "Loading metadata from the selected Ubuntu official server.");
            usedOfficial = preferKakao;
            metadata = await TryLoadMetadataAsync(request, officialSources, log, cancellationToken);
        }

        if (metadata is null)
        {
            log("Ubuntu official metadata failed. Retrying with Ubuntu old-releases servers.");
            usedOfficial = true;
            usedOldReleases = true;
            metadata = await TryLoadMetadataAsync(request, oldReleaseSources, log, cancellationToken);
        }

        if (metadata is null)
        {
            throw new InvalidOperationException(
                "Ubuntu repository metadata could not be downloaded from Kakao, Ubuntu official, or Ubuntu old-releases servers.");
        }

        var (packages, unresolved) = ResolveDependencyClosure(request, metadata.Packages);
        var activeSources = usedOldReleases
            ? oldReleaseSources
            : preferKakao && !usedOfficial
                ? kakaoSources
                : officialSources;
        var sources = activeSources
            .Select(source => new RepositorySource(source.Name, source.BaseUrl, source.Suite, source.Components, request.Architecture))
            .ToArray();

        log($"Resolved {packages.Count} package file(s); unresolved dependency group(s): {unresolved.Count}.");
        return new ResolveResult(packages, unresolved, sources, usedOfficial);
    }

    private static bool IsPortsArchitecture(string architecture)
    {
        return architecture.Equals("arm64", StringComparison.OrdinalIgnoreCase)
            || architecture.Equals("armhf", StringComparison.OrdinalIgnoreCase)
            || architecture.Equals("ppc64el", StringComparison.OrdinalIgnoreCase)
            || architecture.Equals("s390x", StringComparison.OrdinalIgnoreCase)
            || architecture.Equals("riscv64", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<AptSource> BuildSources(
        DownloadRequest request,
        string baseUrl,
        bool official,
        bool usePortsRepository = false)
    {
        var components = string.Join(' ', request.Components);
        var sources = new List<AptSource>();
        foreach (var pocket in request.Pockets)
        {
            var suite = pocket.Equals("release", StringComparison.OrdinalIgnoreCase)
                ? request.Codename
                : $"{request.Codename}-{pocket}";
            var sourceBaseUrl = official
                && !usePortsRepository
                && pocket.Equals("security", StringComparison.OrdinalIgnoreCase)
                ? "https://security.ubuntu.com/ubuntu/"
                : baseUrl;
            var name = official
                ? $"ubuntu-official-{pocket}"
                : $"kakao-{pocket}";
            sources.Add(new AptSource(name, sourceBaseUrl, suite, components));
        }

        return sources;
    }

    private async Task<MetadataLoadResult?> TryLoadMetadataAsync(
        DownloadRequest request,
        IReadOnlyList<AptSource> sources,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var packages = new List<PackageMetadata>();
        foreach (var source in sources)
        {
            foreach (var component in request.Components)
            {
                var url = $"{source.BaseUrl}dists/{source.Suite}/{component}/binary-{request.Architecture}/Packages.gz";
                log($"Loading metadata: {source.Name}/{component}/{request.Architecture}");
                try
                {
                    using var response = await _httpClient.GetAsync(
                        url,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        log($"Metadata unavailable ({(int)response.StatusCode}): {url}");
                        return null;
                    }

                    await using var compressed = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
                    packages.AddRange(await ParsePackagesAsync(gzip, source.BaseUrl, cancellationToken));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log($"Metadata download failed: {ex.Message}");
                    return null;
                }
            }
        }

        if (packages.Count == 0)
        {
            return null;
        }

        return new MetadataLoadResult(packages
            .GroupBy(package => (package.Package, package.Version, package.Architecture, package.FileName))
            .Select(group => group.First())
            .ToArray());
    }

    private static async Task<IReadOnlyList<PackageMetadata>> ParsePackagesAsync(
        Stream stream,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var result = new List<PackageMetadata>();
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? lastField = null;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                AddPackage(fields, baseUrl, result);
                fields.Clear();
                lastField = null;
                continue;
            }

            if ((line[0] == ' ' || line[0] == '\t') && lastField is not null)
            {
                fields[lastField] = $"{fields[lastField]} {line.Trim()}";
                continue;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            lastField = line[..separator];
            fields[lastField] = line[(separator + 1)..].Trim();
        }

        AddPackage(fields, baseUrl, result);
        return result;
    }

    private static void AddPackage(
        IReadOnlyDictionary<string, string> fields,
        string baseUrl,
        ICollection<PackageMetadata> result)
    {
        if (!fields.TryGetValue("Package", out var package)
            || !fields.TryGetValue("Version", out var version)
            || !fields.TryGetValue("Architecture", out var architecture)
            || !fields.TryGetValue("Filename", out var fileName))
        {
            return;
        }

        _ = long.TryParse(fields.GetValueOrDefault("Size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size);
        result.Add(new PackageMetadata(
            package,
            version,
            architecture,
            fileName,
            new Uri(new Uri(baseUrl), fileName).ToString(),
            size,
            fields.GetValueOrDefault("Pre-Depends") ?? "",
            fields.GetValueOrDefault("Depends") ?? "",
            fields.GetValueOrDefault("Recommends") ?? "",
            fields.GetValueOrDefault("Provides") ?? ""));
    }

    private static (IReadOnlyList<ResolvedPackage> Packages, IReadOnlyList<UnresolvedDependency> Unresolved)
        ResolveDependencyClosure(DownloadRequest request, IReadOnlyList<PackageMetadata> metadata)
    {
        var byName = metadata
            .GroupBy(package => package.Package, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var providers = BuildProviderIndex(metadata);
        var selected = new Dictionary<string, SelectedPackage>(StringComparer.Ordinal);
        var unresolved = new List<UnresolvedDependency>();
        var queue = new Queue<DependencyWorkItem>();

        foreach (var requested in request.Packages)
        {
            var separator = requested.IndexOf('=');
            var name = separator > 0 ? requested[..separator] : requested;
            var version = separator > 0 ? requested[(separator + 1)..] : "";
            queue.Enqueue(new DependencyWorkItem(
                [new DependencyAlternative(name, version.Length > 0 ? "=" : "", version)],
                "(requested)",
                requested,
                true));
        }

        while (queue.Count > 0)
        {
            var item = queue.Dequeue();
            var alreadySatisfied = item.Alternatives.Any(alternative =>
                selected.Values.Any(selectedPackage =>
                    SatisfiesName(selectedPackage.Metadata, alternative.Name)
                    && VersionMatches(selectedPackage.Metadata.Version, alternative.Operator, alternative.Version)));
            if (alreadySatisfied)
            {
                continue;
            }

            var candidate = SelectCandidate(item.Alternatives, request.Architecture, byName, providers);
            if (candidate is null)
            {
                unresolved.Add(new UnresolvedDependency(
                    string.Join(" | ", item.Alternatives.Select(alternative => alternative.Name)),
                    item.RequiredBy,
                    item.Constraint,
                    "No matching package candidate was found in the selected repositories.",
                    ""));
                continue;
            }

            if (selected.TryGetValue(candidate.Package, out var existing))
            {
                if (!item.Alternatives.Any(alternative =>
                        SatisfiesName(existing.Metadata, alternative.Name)
                        && VersionMatches(existing.Metadata.Version, alternative.Operator, alternative.Version)))
                {
                    unresolved.Add(new UnresolvedDependency(
                        candidate.Package,
                        item.RequiredBy,
                        item.Constraint,
                        $"Selected version {existing.Metadata.Version} does not satisfy the additional constraint.",
                        candidate.Version));
                }

                continue;
            }

            selected[candidate.Package] = new SelectedPackage(candidate, item.Requested ? "Requested" : "Dependency");
            foreach (var dependency in ParseDependencyGroups(candidate.PreDepends, request.Architecture)
                         .Concat(ParseDependencyGroups(candidate.Depends, request.Architecture)))
            {
                queue.Enqueue(new DependencyWorkItem(dependency, candidate.Package, FormatConstraint(dependency), false));
            }

            if (request.IncludeRecommends)
            {
                foreach (var dependency in ParseDependencyGroups(candidate.Recommends, request.Architecture))
                {
                    queue.Enqueue(new DependencyWorkItem(dependency, candidate.Package, FormatConstraint(dependency), false));
                }
            }
        }

        var resolved = selected.Values
            .Select(item => new ResolvedPackage(
                item.Metadata.Package,
                item.Metadata.Version,
                item.Metadata.Architecture,
                Path.GetFileName(item.Metadata.FileName),
                item.Metadata.SourceUrl,
                item.Metadata.Size,
                item.Reason))
            .OrderBy(package => package.Package, StringComparer.Ordinal)
            .ThenBy(package => package.Version, DebianVersionComparer.Instance)
            .ToArray();

        return (resolved, unresolved);
    }

    private static IReadOnlyDictionary<string, PackageMetadata[]> BuildProviderIndex(
        IReadOnlyList<PackageMetadata> metadata)
    {
        return metadata
            .SelectMany(package => ParseProvides(package.Provides).Select(provided => (provided, package)))
            .GroupBy(item => item.provided, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.package).ToArray(),
                StringComparer.Ordinal);
    }

    private static IEnumerable<string> ParseProvides(string value)
    {
        return SplitTopLevel(value, ',')
            .Select(item => DependencyRegex.Match(item.Trim()))
            .Where(match => match.Success)
            .Select(match => match.Groups["name"].Value);
    }

    private static PackageMetadata? SelectCandidate(
        IReadOnlyList<DependencyAlternative> alternatives,
        string architecture,
        IReadOnlyDictionary<string, PackageMetadata[]> byName,
        IReadOnlyDictionary<string, PackageMetadata[]> providers)
    {
        foreach (var alternative in alternatives)
        {
            var candidates = new List<PackageMetadata>();
            if (byName.TryGetValue(alternative.Name, out var direct))
            {
                candidates.AddRange(direct);
            }

            if (string.IsNullOrEmpty(alternative.Operator)
                && providers.TryGetValue(alternative.Name, out var provided))
            {
                candidates.AddRange(provided);
            }

            var selected = candidates
                .Where(candidate => candidate.Architecture.Equals(architecture, StringComparison.OrdinalIgnoreCase)
                    || candidate.Architecture.Equals("all", StringComparison.OrdinalIgnoreCase))
                .Where(candidate => VersionMatches(candidate.Version, alternative.Operator, alternative.Version))
                .OrderByDescending(candidate => candidate.Version, DebianVersionComparer.Instance)
                .FirstOrDefault();
            if (selected is not null)
            {
                return selected;
            }
        }

        return null;
    }

    private static IReadOnlyList<IReadOnlyList<DependencyAlternative>> ParseDependencyGroups(
        string value,
        string architecture)
    {
        var groups = new List<IReadOnlyList<DependencyAlternative>>();
        foreach (var groupText in SplitTopLevel(value, ','))
        {
            var alternatives = new List<DependencyAlternative>();
            foreach (var alternativeText in SplitTopLevel(groupText, '|'))
            {
                if (!AppliesToArchitecture(alternativeText, architecture))
                {
                    continue;
                }

                var match = DependencyRegex.Match(alternativeText.Trim());
                if (!match.Success)
                {
                    continue;
                }

                alternatives.Add(new DependencyAlternative(
                    match.Groups["name"].Value,
                    match.Groups["op"].Value,
                    match.Groups["version"].Value.Trim()));
            }

            if (alternatives.Count > 0)
            {
                groups.Add(alternatives);
            }
        }

        return groups;
    }

    private static bool AppliesToArchitecture(string value, string architecture)
    {
        var start = value.IndexOf('[');
        var end = value.IndexOf(']', start + 1);
        if (start < 0 || end <= start)
        {
            return true;
        }

        var restrictions = value[(start + 1)..end]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var positives = restrictions.Where(item => !item.StartsWith('!')).ToArray();
        if (restrictions.Any(item => item.Equals($"!{architecture}", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return positives.Length == 0
            || positives.Any(item => item.Equals(architecture, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SplitTopLevel(string value, char separator)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        var start = 0;
        var parentheses = 0;
        var brackets = 0;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '(':
                    parentheses++;
                    break;
                case ')':
                    parentheses--;
                    break;
                case '[':
                    brackets++;
                    break;
                case ']':
                    brackets--;
                    break;
            }

            if (value[index] == separator && parentheses == 0 && brackets == 0)
            {
                yield return value[start..index];
                start = index + 1;
            }
        }

        yield return value[start..];
    }

    private static bool SatisfiesName(PackageMetadata package, string name)
    {
        return package.Package.Equals(name, StringComparison.Ordinal)
            || ParseProvides(package.Provides).Contains(name, StringComparer.Ordinal);
    }

    private static bool VersionMatches(string candidate, string @operator, string required)
    {
        if (string.IsNullOrEmpty(@operator))
        {
            return true;
        }

        var comparison = DebianVersionComparer.Instance.Compare(candidate, required);
        return @operator switch
        {
            "=" => comparison == 0,
            ">=" => comparison >= 0,
            "<=" => comparison <= 0,
            ">>" or ">" => comparison > 0,
            "<<" or "<" => comparison < 0,
            _ => false
        };
    }

    private static string FormatConstraint(IEnumerable<DependencyAlternative> alternatives)
    {
        return string.Join(" | ", alternatives.Select(alternative =>
            string.IsNullOrEmpty(alternative.Operator)
                ? alternative.Name
                : $"{alternative.Name} ({alternative.Operator} {alternative.Version})"));
    }

    private sealed record AptSource(string Name, string BaseUrl, string Suite, string Components);
    private sealed record MetadataLoadResult(IReadOnlyList<PackageMetadata> Packages);
    private sealed record PackageMetadata(
        string Package,
        string Version,
        string Architecture,
        string FileName,
        string SourceUrl,
        long Size,
        string PreDepends,
        string Depends,
        string Recommends,
        string Provides);
    private sealed record DependencyAlternative(string Name, string Operator, string Version);
    private sealed record DependencyWorkItem(
        IReadOnlyList<DependencyAlternative> Alternatives,
        string RequiredBy,
        string Constraint,
        bool Requested);
    private sealed record SelectedPackage(PackageMetadata Metadata, string Reason);
}

public sealed class DebianVersionComparer : IComparer<string>
{
    public static DebianVersionComparer Instance { get; } = new();

    public int Compare(string? left, string? right)
    {
        left ??= "";
        right ??= "";
        var leftParts = SplitVersion(left);
        var rightParts = SplitVersion(right);
        var epochComparison = leftParts.Epoch.CompareTo(rightParts.Epoch);
        if (epochComparison != 0)
        {
            return epochComparison;
        }

        var upstreamComparison = ComparePart(leftParts.Upstream, rightParts.Upstream);
        return upstreamComparison != 0
            ? upstreamComparison
            : ComparePart(leftParts.Revision, rightParts.Revision);
    }

    private static (long Epoch, string Upstream, string Revision) SplitVersion(string version)
    {
        var epoch = 0L;
        var epochSeparator = version.IndexOf(':');
        if (epochSeparator >= 0)
        {
            _ = long.TryParse(version[..epochSeparator], NumberStyles.Integer, CultureInfo.InvariantCulture, out epoch);
            version = version[(epochSeparator + 1)..];
        }

        var revisionSeparator = version.LastIndexOf('-');
        return revisionSeparator >= 0
            ? (epoch, version[..revisionSeparator], version[(revisionSeparator + 1)..])
            : (epoch, version, "0");
    }

    private static int ComparePart(string left, string right)
    {
        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < left.Length || rightIndex < right.Length)
        {
            while ((leftIndex < left.Length && !char.IsDigit(left[leftIndex]))
                   || (rightIndex < right.Length && !char.IsDigit(right[rightIndex])))
            {
                var leftOrder = CharacterOrder(leftIndex < left.Length ? left[leftIndex] : '\0');
                var rightOrder = CharacterOrder(rightIndex < right.Length ? right[rightIndex] : '\0');
                if (leftOrder != rightOrder)
                {
                    return leftOrder.CompareTo(rightOrder);
                }

                if (leftIndex < left.Length)
                {
                    leftIndex++;
                }

                if (rightIndex < right.Length)
                {
                    rightIndex++;
                }
            }

            while (leftIndex < left.Length && left[leftIndex] == '0')
            {
                leftIndex++;
            }

            while (rightIndex < right.Length && right[rightIndex] == '0')
            {
                rightIndex++;
            }

            var leftDigits = leftIndex;
            var rightDigits = rightIndex;
            while (leftDigits < left.Length && char.IsDigit(left[leftDigits]))
            {
                leftDigits++;
            }

            while (rightDigits < right.Length && char.IsDigit(right[rightDigits]))
            {
                rightDigits++;
            }

            var leftLength = leftDigits - leftIndex;
            var rightLength = rightDigits - rightIndex;
            if (leftLength != rightLength)
            {
                return leftLength.CompareTo(rightLength);
            }

            var digitComparison = string.Compare(
                left,
                leftIndex,
                right,
                rightIndex,
                leftLength,
                StringComparison.Ordinal);
            if (digitComparison != 0)
            {
                return digitComparison;
            }

            leftIndex = leftDigits;
            rightIndex = rightDigits;
        }

        return 0;
    }

    private static int CharacterOrder(char value)
    {
        if (value == '~')
        {
            return -1;
        }

        if (value == '\0')
        {
            return 0;
        }

        if (char.IsDigit(value))
        {
            return 0;
        }

        return char.IsLetter(value) ? value : value + 256;
    }
}

public sealed class DebDownloader
{
    private readonly HttpClient _httpClient = new(new SocketsHttpHandler
    {
        MaxConnectionsPerServer = int.MaxValue,
        PooledConnectionLifetime = TimeSpan.FromMinutes(10)
    });

    public async Task<IReadOnlyList<ReportRow>> DownloadAsync(
        DownloadRequest request,
        IReadOnlyList<ResolvedPackage> packages,
        Action<ReportRow> report,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(request.OutputFolder);

        var rows = new List<ReportRow>();
        var lockObject = new object();
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(4, Environment.ProcessorCount * 4)
        };

        await Parallel.ForEachAsync(packages, parallelOptions, async (package, token) =>
        {
            var row = await DownloadOneAsync(request.OutputFolder, package, log, token);
            lock (lockObject)
            {
                rows.Add(row);
            }

            report(row);
        });

        return rows
            .OrderBy(row => row.Package, StringComparer.Ordinal)
            .ThenBy(row => row.Version, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<ReportRow> DownloadOneAsync(
        string outputFolder,
        ResolvedPackage package,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var destination = Path.Combine(outputFolder, package.FileName);
        if (File.Exists(destination))
        {
            var skippedHash = await ComputeSha256Async(destination, cancellationToken);
            log($"Skipped existing {package.FileName}.");
            return ToRow("Skipped", package, skippedHash, "Already exists");
        }

        var urls = BuildFallbackUrls(package.SourceUrl).Distinct(StringComparer.Ordinal).ToArray();
        var errors = new List<string>();

        foreach (var url in urls)
        {
            try
            {
                await using var input = await _httpClient.GetStreamAsync(url, cancellationToken);
                await using (var output = File.Create(destination))
                {
                    await input.CopyToAsync(output, cancellationToken);
                }

                var hash = await ComputeSha256Async(destination, cancellationToken);
                log($"Downloaded {package.FileName}.");
                return ToRow("Downloaded", package with { SourceUrl = url }, hash, "OK");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"{url}: {ex.Message}");
                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }
            }
        }

        log($"Failed {package.FileName}.");
        return ToRow("Failed", package, "", string.Join(" | ", errors));
    }

    private static IEnumerable<string> BuildFallbackUrls(string sourceUrl)
    {
        yield return sourceUrl;

        const string kakao = "https://mirror.kakao.com/ubuntu/";
        if (!sourceUrl.StartsWith(kakao, StringComparison.OrdinalIgnoreCase))
        {
            if (sourceUrl.StartsWith("https://archive.ubuntu.com/ubuntu/", StringComparison.OrdinalIgnoreCase))
            {
                yield return "https://old-releases.ubuntu.com/ubuntu/" + sourceUrl["https://archive.ubuntu.com/ubuntu/".Length..];
            }
            else if (sourceUrl.StartsWith("https://ports.ubuntu.com/ubuntu-ports/", StringComparison.OrdinalIgnoreCase))
            {
                yield return "https://old-releases.ports.ubuntu.com/ubuntu-ports/"
                    + sourceUrl["https://ports.ubuntu.com/ubuntu-ports/".Length..];
            }

            yield break;
        }

        var relative = sourceUrl[kakao.Length..];
        if (relative.Contains("-security/", StringComparison.OrdinalIgnoreCase))
        {
            yield return "https://security.ubuntu.com/ubuntu/" + relative;
        }

        yield return "https://archive.ubuntu.com/ubuntu/" + relative;
        yield return "https://old-releases.ubuntu.com/ubuntu/" + relative;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static ReportRow ToRow(string status, ResolvedPackage package, string sha256, string message)
    {
        return new ReportRow
        {
            Status = status,
            Package = package.Package,
            Version = package.Version,
            Architecture = package.Architecture,
            Reason = package.Reason,
            FileName = package.FileName,
            SourceUrl = package.SourceUrl,
            Size = package.Size,
            SHA256 = sha256,
            Message = message
        };
    }
}

public static class ReportWriter
{
    public static void WriteManifest(
        DownloadRequest request,
        ResolveResult result,
        string outputFolder,
        bool downloadStarted,
        IReadOnlyList<ReportRow> downloadRows)
    {
        var manifest = new
        {
            createdAt = DateTimeOffset.Now,
            ubuntuVersion = request.UbuntuVersion,
            codename = request.Codename,
            architecture = request.Architecture,
            components = request.Components,
            pockets = request.Pockets,
            serverRepository = request.ServerRepository,
            includeRecommends = request.IncludeRecommends,
            repositoryFallbacks = new[]
            {
                "https://mirror.kakao.com/ubuntu/",
                "https://archive.ubuntu.com/ubuntu/",
                "https://security.ubuntu.com/ubuntu/",
                "https://old-releases.ubuntu.com/ubuntu/",
                "https://ports.ubuntu.com/ubuntu-ports/",
                "https://old-releases.ports.ubuntu.com/ubuntu-ports/"
            },
            usedOfficialFallback = result.UsedOfficialFallback,
            sources = result.Sources,
            requestedPackages = request.Packages,
            resolvedPackages = result.Packages,
            downloadStarted,
            downloads = downloadRows
        };

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(outputFolder, "manifest.json"), json, Encoding.UTF8);
    }

    public static void WriteDownloadReport(string path, IEnumerable<ReportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Status,Package,Version,Architecture,Reason,FileName,SourceUrl,Size,SHA256,Message");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Csv(row.Status),
                Csv(row.Package),
                Csv(row.Version),
                Csv(row.Architecture),
                Csv(row.Reason),
                Csv(row.FileName),
                Csv(row.SourceUrl),
                row.Size.ToString(CultureInfo.InvariantCulture),
                Csv(row.SHA256),
                Csv(row.Message)
            }));
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

    public static void WriteChecksums(string path, IEnumerable<ReportRow> rows)
    {
        var lines = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.SHA256))
            .OrderBy(row => row.FileName, StringComparer.Ordinal)
            .Select(row => $"{row.SHA256}  {row.FileName}");
        File.WriteAllLines(path, lines, Encoding.UTF8);
    }

    public static void WriteUnresolved(string path, IEnumerable<UnresolvedDependency> unresolved)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Package,RequiredBy,Constraint,Reason,Candidates");
        foreach (var item in unresolved)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Csv(item.Package),
                Csv(item.RequiredBy),
                Csv(item.Constraint),
                Csv(item.Reason),
                Csv(item.Candidates)
            }));
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
