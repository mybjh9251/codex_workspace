using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetDependencyDownloaderWPF
{
    public class Downloader
    {
        private readonly ConcurrentDictionary<string, SourcePackageDependencyInfo?> metadataCache = new(PackageKey.Comparer);
        private readonly ConcurrentDictionary<string, NuGetVersion?> versionCache = new(PackageKey.Comparer);

        public async Task<DownloadSummary> DownloadPackagesAsync(
            IReadOnlyCollection<PackageModel> rootPackages,
            DownloadOptions options,
            Action<PackageModel>? packageDiscovered,
            Action<PackageModel>? packageUpdated,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(options.OutputFolder);

            var repository = Repository.Factory.GetCoreV3(options.PackageSource);
            using var cache = new SourceCacheContext();

            var framework = NuGetFramework.ParseFolder(options.TargetFramework);
            var allPackages = await ResolveClosureAsync(rootPackages, options, repository, framework, cache, packageDiscovered, packageUpdated, cancellationToken);

            await DownloadResolvedPackagesAsync(allPackages, options, repository, cache, packageUpdated, cancellationToken);
            WriteNuGetConfig(options);
            WriteReport(options, allPackages);

            return new DownloadSummary
            {
                RootCount = allPackages.Count(p => p.Kind == "Root"),
                DependencyCount = allPackages.Count(p => p.Kind == "Dependency"),
                DownloadedCount = allPackages.Count(p => p.Status == "Done"),
                SkippedCount = allPackages.Count(p => p.Status == "Skipped"),
                FailedCount = allPackages.Count(p => p.Status == "Failed")
            };
        }

        private async Task<List<PackageModel>> ResolveClosureAsync(
            IReadOnlyCollection<PackageModel> rootPackages,
            DownloadOptions options,
            SourceRepository repository,
            NuGetFramework framework,
            SourceCacheContext cache,
            Action<PackageModel>? packageDiscovered,
            Action<PackageModel>? packageUpdated,
            CancellationToken cancellationToken)
        {
            var dependencyResource = await repository.GetResourceAsync<DependencyInfoResource>(cancellationToken);
            var resolved = new ConcurrentDictionary<string, PackageModel>(PackageKey.Comparer);
            var queue = new ConcurrentQueue<PackageModel>();

            foreach (var root in rootPackages)
            {
                var normalized = new PackageModel
                {
                    Id = root.Id.Trim(),
                    Version = root.Version.Trim(),
                    Kind = "Root",
                    Status = "Pending"
                };

                if (resolved.TryAdd(normalized.Key, normalized))
                {
                    queue.Enqueue(normalized);
                }
            }

            while (!queue.IsEmpty)
            {
                var batch = new List<PackageModel>();
                while (queue.TryDequeue(out var package))
                {
                    batch.Add(package);
                }

                await ParallelForEachAsync(batch, options.MaxParallelism, async package =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ResolveOnePackageAsync(package, resolved, queue, repository, dependencyResource, framework, cache, packageDiscovered, packageUpdated, cancellationToken);
                });
            }

            return resolved.Values
                .OrderBy(p => p.Kind == "Root" ? 0 : 1)
                .ThenBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Version, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task ResolveOnePackageAsync(
            PackageModel package,
            ConcurrentDictionary<string, PackageModel> resolved,
            ConcurrentQueue<PackageModel> queue,
            SourceRepository repository,
            DependencyInfoResource dependencyResource,
            NuGetFramework framework,
            SourceCacheContext cache,
            Action<PackageModel>? packageDiscovered,
            Action<PackageModel>? packageUpdated,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                package.Status = "Resolving";
                package.Message = "Reading metadata";
                packageUpdated?.Invoke(package);

                var version = NuGetVersion.Parse(package.Version);
                var metadata = await GetDependencyInfoAsync(dependencyResource, package.Id, version, framework, cache, cancellationToken);
                if (metadata == null)
                {
                    package.Status = "Failed";
                    package.Message = "Package metadata not found";
                    packageUpdated?.Invoke(package);
                    return;
                }

                foreach (var dependency in metadata.Dependencies)
                {
                    var dependencyVersion = await ResolveBestVersionAsync(repository, dependency.Id, dependency.VersionRange, cache, cancellationToken);
                    if (dependencyVersion == null)
                    {
                        continue;
                    }

                    var dependencyPackage = new PackageModel
                    {
                        Id = dependency.Id,
                        Version = dependencyVersion.ToNormalizedString(),
                        Kind = "Dependency",
                        Status = "Pending"
                    };

                    if (resolved.TryAdd(dependencyPackage.Key, dependencyPackage))
                    {
                        packageDiscovered?.Invoke(dependencyPackage);
                        queue.Enqueue(dependencyPackage);
                    }
                }

                package.Status = "Resolved";
                package.Message = $"{metadata.Dependencies.Count()} dependency item(s)";
                packageUpdated?.Invoke(package);
            }
            catch (OperationCanceledException)
            {
                package.Status = "Cancelled";
                package.Message = "Cancelled";
                packageUpdated?.Invoke(package);
                throw;
            }
            catch (Exception ex)
            {
                package.Status = "Failed";
                package.Message = ex.Message;
                packageUpdated?.Invoke(package);
            }
        }

        private async Task<SourcePackageDependencyInfo?> GetDependencyInfoAsync(
            DependencyInfoResource dependencyResource,
            string id,
            NuGetVersion version,
            NuGetFramework framework,
            SourceCacheContext cache,
            CancellationToken cancellationToken)
        {
            var key = PackageKey.Create($"dependency-metadata:{framework.GetShortFolderName()}", id, version.ToNormalizedString());
            if (metadataCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var identity = new PackageIdentity(id, version);
            var metadata = await ExecuteWithRetryAsync(
                () => dependencyResource.ResolvePackage(identity, framework, cache, NullLogger.Instance, cancellationToken),
                retryCount: 2,
                cancellationToken);
            metadataCache[key] = metadata;
            return metadata;
        }

        private async Task<NuGetVersion?> ResolveBestVersionAsync(
            SourceRepository repository,
            string id,
            VersionRange range,
            SourceCacheContext cache,
            CancellationToken cancellationToken)
        {
            var key = PackageKey.Create(repository.PackageSource.Source, id, range.ToNormalizedString());
            if (versionCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var findResource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
            var versions = await ExecuteWithRetryAsync(
                () => findResource.GetAllVersionsAsync(id, cache, NullLogger.Instance, cancellationToken),
                retryCount: 2,
                cancellationToken);
            var candidateVersions = versions.Where(range.Satisfies);
            if (!AllowsPrerelease(range))
            {
                candidateVersions = candidateVersions.Where(v => !v.IsPrerelease);
            }

            var bestVersion = candidateVersions
                .OrderBy(v => v)
                .FirstOrDefault();

            versionCache[key] = bestVersion;
            return bestVersion;
        }

        private async Task DownloadResolvedPackagesAsync(
            IReadOnlyCollection<PackageModel> packages,
            DownloadOptions options,
            SourceRepository repository,
            SourceCacheContext cache,
            Action<PackageModel>? packageUpdated,
            CancellationToken cancellationToken)
        {
            var findResource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);

            await ParallelForEachAsync(packages, options.MaxParallelism, async package =>
            {
                if (package.Status == "Failed")
                {
                    return;
                }

                var outputPath = Path.Combine(options.OutputFolder, $"{package.Id}.{package.Version}.nupkg");
                if (File.Exists(outputPath) && !options.OverwriteExisting)
                {
                    package.Status = "Skipped";
                    package.Progress = 100;
                    package.Message = "Already exists";
                    packageUpdated?.Invoke(package);
                    return;
                }

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    package.Status = "Downloading";
                    package.Progress = 10;
                    package.Message = string.Empty;
                    packageUpdated?.Invoke(package);

                    await using var packageStream = new MemoryStream();
                    var success = await ExecuteWithRetryAsync(
                        () =>
                        {
                            packageStream.SetLength(0);
                            packageStream.Position = 0;
                            return findResource.CopyNupkgToStreamAsync(
                            package.Id,
                            NuGetVersion.Parse(package.Version),
                            packageStream,
                            cache,
                            NullLogger.Instance,
                            cancellationToken);
                        },
                        options.RetryCount,
                        cancellationToken);

                    if (!success)
                    {
                        package.Status = "Failed";
                        package.Message = "Download failed";
                        packageUpdated?.Invoke(package);
                        return;
                    }

                    await File.WriteAllBytesAsync(outputPath, packageStream.ToArray(), cancellationToken);
                    package.Status = "Done";
                    package.Progress = 100;
                    package.Message = Path.GetFileName(outputPath);
                    packageUpdated?.Invoke(package);
                }
                catch (OperationCanceledException)
                {
                    package.Status = "Cancelled";
                    package.Message = "Cancelled";
                    packageUpdated?.Invoke(package);
                    throw;
                }
                catch (Exception ex)
                {
                    package.Status = "Failed";
                    package.Message = ex.Message;
                    packageUpdated?.Invoke(package);
                }
            });
        }

        private static async Task ParallelForEachAsync<T>(IEnumerable<T> items, int maxParallelism, Func<T, Task> action)
        {
            using var semaphore = new SemaphoreSlim(Math.Max(1, maxParallelism));
            var tasks = items.Select(async item =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await action(item);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        private static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int retryCount, CancellationToken cancellationToken)
        {
            var attempts = Math.Max(0, retryCount) + 1;
            Exception? lastError = null;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return await action();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < attempts)
                {
                    lastError = ex;
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            throw lastError ?? new InvalidOperationException("Retry operation failed.");
        }

        private static bool AllowsPrerelease(VersionRange range)
        {
            return (range.MinVersion?.IsPrerelease ?? false)
                || (range.MaxVersion?.IsPrerelease ?? false)
                || (range.OriginalString?.Contains('-', StringComparison.Ordinal) ?? false);
        }

        private static void WriteNuGetConfig(DownloadOptions options)
        {
            var path = Path.Combine(options.OutputFolder, "nuget.config");
            var absolutePackagesPath = Path.GetFullPath(options.OutputFolder);
            var xml =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""offline-bundle"" value=""{System.Security.SecurityElement.Escape(absolutePackagesPath)}"" />
  </packageSources>
</configuration>
";
            File.WriteAllText(path, xml, Encoding.UTF8);
        }

        private static void WriteReport(DownloadOptions options, IReadOnlyCollection<PackageModel> packages)
        {
            var path = Path.Combine(options.OutputFolder, "download-report.csv");
            var lines = new List<string> { "Id,Version,Kind,Status,Message" };
            lines.AddRange(packages.Select(p => $"{EscapeCsv(p.Id)},{EscapeCsv(p.Version)},{EscapeCsv(p.Kind)},{EscapeCsv(p.Status)},{EscapeCsv(p.Message)}"));
            File.WriteAllLines(path, lines, Encoding.UTF8);
        }

        private static string EscapeCsv(string value)
        {
            if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n'))
            {
                return value;
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
