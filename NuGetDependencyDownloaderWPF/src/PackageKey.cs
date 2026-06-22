using System;

namespace NuGetDependencyDownloaderWPF
{
    public static class PackageKey
    {
        public static string Create(string id, string version)
        {
            return $"{id.Trim().ToLowerInvariant()}::{version.Trim()}";
        }

        public static string Create(string source, string id, string version)
        {
            return $"{source.Trim().ToLowerInvariant()}::{Create(id, version)}";
        }

        public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;
    }
}
