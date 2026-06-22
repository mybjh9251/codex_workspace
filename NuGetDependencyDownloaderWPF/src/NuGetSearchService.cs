using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetDependencyDownloaderWPF
{
    public class NuGetSearchService
    {
        private readonly ConcurrentDictionary<string, IReadOnlyList<string>> packageSearchCache = new(PackageKey.Comparer);
        private readonly ConcurrentDictionary<string, IReadOnlyList<string>> versionCache = new(PackageKey.Comparer);

        public async Task<IReadOnlyList<string>> SearchPackageIdsAsync(
            string source,
            string query,
            int take,
            CancellationToken cancellationToken)
        {
            var normalizedQuery = query.Trim();
            if (normalizedQuery.Length < 2)
            {
                return Array.Empty<string>();
            }

            var key = $"{source.Trim().ToLowerInvariant()}::search::{normalizedQuery.ToLowerInvariant()}::{take}";
            if (packageSearchCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var repository = Repository.Factory.GetCoreV3(source);
            var searchResource = await repository.GetResourceAsync<PackageSearchResource>(cancellationToken);
            var filter = new SearchFilter(includePrerelease: false);
            var results = await searchResource.SearchAsync(
                normalizedQuery,
                filter,
                skip: 0,
                take: take,
                log: NullLogger.Instance,
                cancellationToken: cancellationToken);

            var packageIds = results
                .Select(item => item.Identity.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(take)
                .ToList();

            packageSearchCache[key] = packageIds;
            return packageIds;
        }

        public async Task<IReadOnlyList<string>> GetVersionsAsync(
            string source,
            string packageId,
            string versionQuery,
            int take,
            CancellationToken cancellationToken)
        {
            var normalizedId = packageId.Trim();
            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                return Array.Empty<string>();
            }

            var key = $"{source.Trim().ToLowerInvariant()}::versions::{normalizedId.ToLowerInvariant()}";
            if (!versionCache.TryGetValue(key, out var versions))
            {
                var repository = Repository.Factory.GetCoreV3(source);
                var findResource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
                var allVersions = await findResource.GetAllVersionsAsync(
                    normalizedId,
                    new SourceCacheContext(),
                    NullLogger.Instance,
                    cancellationToken);

                versions = allVersions
                    .OrderBy(v => v.IsPrerelease ? 1 : 0)
                    .ThenByDescending(v => v)
                    .Select(v => v.ToNormalizedString())
                    .ToList();

                versionCache[key] = versions;
            }

            var normalizedVersionQuery = versionQuery.Trim();
            var filtered = string.IsNullOrWhiteSpace(normalizedVersionQuery)
                ? versions
                : versions.Where(v => v.StartsWith(normalizedVersionQuery, StringComparison.OrdinalIgnoreCase)
                    || v.Contains(normalizedVersionQuery, StringComparison.OrdinalIgnoreCase)).ToList();

            return filtered.Take(take).ToList();
        }
    }
}
