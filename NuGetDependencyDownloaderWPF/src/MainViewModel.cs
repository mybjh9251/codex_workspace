using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NuGetDependencyDownloaderWPF
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string outputFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "packages");
        private string packageSource = "https://api.nuget.org/v3/index.json";
        private string packageIdInput = string.Empty;
        private string versionInput = string.Empty;
        private string targetFramework = "net8.0";
        private bool overwriteExisting;
        private bool isDownloading;
        private bool isPackageIdSuggestionsOpen;
        private bool isVersionSuggestionsOpen;
        private bool isPackageIdInputActive;
        private bool isVersionInputActive;
        private int retryCount = 2;
        private string summary = "Ready";
        private readonly Downloader downloader = new();
        private readonly NuGetSearchService searchService = new();
        private CancellationTokenSource? downloadCancellation;
        private CancellationTokenSource? packageSuggestionCancellation;
        private CancellationTokenSource? versionSuggestionCancellation;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<PackageModel> Packages { get; } = new();
        public ObservableCollection<string> PackageIdSuggestions { get; } = new();
        public ObservableCollection<string> VersionSuggestions { get; } = new();

        public string OutputFolder
        {
            get => outputFolder;
            set => SetField(ref outputFolder, value);
        }

        public string PackageSource
        {
            get => packageSource;
            set
            {
                if (SetField(ref packageSource, value))
                {
                    _ = RefreshPackageIdSuggestionsAsync();
                    if (isVersionInputActive)
                    {
                        _ = RefreshVersionSuggestionsAsync();
                    }
                }
            }
        }

        public string PackageIdInput
        {
            get => packageIdInput;
            set
            {
                if (SetField(ref packageIdInput, value))
                {
                    _ = RefreshPackageIdSuggestionsAsync();
                }
            }
        }

        public string VersionInput
        {
            get => versionInput;
            set
            {
                if (SetField(ref versionInput, value) && isVersionInputActive)
                {
                    _ = RefreshVersionSuggestionsAsync();
                }
            }
        }

        public string TargetFramework
        {
            get => targetFramework;
            set => SetField(ref targetFramework, value);
        }

        public bool OverwriteExisting
        {
            get => overwriteExisting;
            set => SetField(ref overwriteExisting, value);
        }

        public bool IsDownloading
        {
            get => isDownloading;
            set => SetField(ref isDownloading, value);
        }

        public bool IsPackageIdSuggestionsOpen
        {
            get => isPackageIdSuggestionsOpen;
            set => SetField(ref isPackageIdSuggestionsOpen, value);
        }

        public bool IsVersionSuggestionsOpen
        {
            get => isVersionSuggestionsOpen;
            set => SetField(ref isVersionSuggestionsOpen, value);
        }

        public int RetryCount
        {
            get => retryCount;
            set => SetField(ref retryCount, Math.Max(0, value));
        }

        public string Summary
        {
            get => summary;
            set => SetField(ref summary, value);
        }

        public void AddPackage(string id, string version)
        {
            var normalizedId = id.Trim();
            var normalizedVersion = version.Trim();
            if (string.IsNullOrWhiteSpace(normalizedId) || string.IsNullOrWhiteSpace(normalizedVersion))
            {
                return;
            }

            if (Packages.Any(p => PackageKey.Create(p.Id, p.Version) == PackageKey.Create(normalizedId, normalizedVersion)))
            {
                Summary = "Package already exists in the list.";
                return;
            }

            Packages.Add(new PackageModel { Id = normalizedId, Version = normalizedVersion, Kind = "Root", Status = "Pending" });
            Summary = $"{Packages.Count(p => p.Kind == "Root")} root package(s)";
        }

        public void AddCurrentPackage()
        {
            AddPackage(PackageIdInput, VersionInput);
            PackageIdInput = string.Empty;
            VersionInput = string.Empty;
            IsPackageIdSuggestionsOpen = false;
            IsVersionSuggestionsOpen = false;
        }

        public void SelectPackageIdSuggestion(string packageId)
        {
            packageSuggestionCancellation?.Cancel();
            UpdateSuggestions(PackageIdSuggestions, Array.Empty<string>());
            SetField(ref packageIdInput, packageId, nameof(PackageIdInput));
            IsPackageIdSuggestionsOpen = false;
        }

        public void SelectVersionSuggestion(string version)
        {
            versionSuggestionCancellation?.Cancel();
            UpdateSuggestions(VersionSuggestions, Array.Empty<string>());
            SetField(ref versionInput, version, nameof(VersionInput));
            IsVersionSuggestionsOpen = false;
        }

        public void ActivatePackageIdInput()
        {
            isPackageIdInputActive = true;
            isVersionInputActive = false;
            IsVersionSuggestionsOpen = false;
            _ = RefreshPackageIdSuggestionsAsync();
        }

        public void ActivateVersionInput()
        {
            isPackageIdInputActive = false;
            isVersionInputActive = true;
            IsPackageIdSuggestionsOpen = false;
            _ = RefreshVersionSuggestionsAsync();
        }

        public void DeactivateAutocomplete()
        {
            isPackageIdInputActive = false;
            isVersionInputActive = false;
            IsPackageIdSuggestionsOpen = false;
            IsVersionSuggestionsOpen = false;
        }

        public void RemovePackage(PackageModel package)
        {
            Packages.Remove(package);
            Summary = $"{Packages.Count(p => p.Kind == "Root")} root package(s)";
        }

        public async Task StartDownloadAsync()
        {
            if (IsDownloading)
            {
                Summary = "Download is already running.";
                return;
            }

            var roots = Packages
                .Where(p => p.Kind == "Root")
                .Select(p => p.CloneAsRoot())
                .ToList();

            await StartDownloadAsync(roots);
        }

        public async Task RetryFailedAsync()
        {
            if (IsDownloading)
            {
                Summary = "Download is already running.";
                return;
            }

            var roots = Packages
                .Where(p => p.Kind == "Root")
                .Select(p => p.CloneAsRoot())
                .ToList();

            await StartDownloadAsync(roots);
        }

        public void CancelDownload()
        {
            downloadCancellation?.Cancel();
            Summary = "Cancelling...";
        }

        public void SetOutputFolder(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                OutputFolder = path;
            }
        }

        private async Task StartDownloadAsync(IReadOnlyCollection<PackageModel> roots)
        {
            if (string.IsNullOrWhiteSpace(PackageSource))
            {
                Summary = "Package source is required.";
                return;
            }

            if (roots.Count == 0)
            {
                Summary = "Add at least one root package.";
                return;
            }

            Packages.Clear();
            foreach (var root in roots)
            {
                Packages.Add(root);
            }

            var options = new DownloadOptions
            {
                PackageSource = PackageSource.Trim(),
                TargetFramework = TargetFramework.Trim(),
                OutputFolder = OutputFolder.Trim(),
                OverwriteExisting = OverwriteExisting,
                RetryCount = RetryCount
            };

            downloadCancellation = new CancellationTokenSource();
            IsDownloading = true;
            Summary = "Resolving dependencies...";

            try
            {
                var result = await downloader.DownloadPackagesAsync(
                    roots,
                    options,
                    AddOrUpdatePackageOnUiThread,
                    UpdatePackageOnUiThread,
                    downloadCancellation.Token);

                Summary = result.ToString();
            }
            catch (OperationCanceledException)
            {
                Summary = "Cancelled.";
            }
            finally
            {
                IsDownloading = false;
                downloadCancellation.Dispose();
                downloadCancellation = null;
            }
        }

        public void ImportPackageList(string path)
        {
            var imported = 0;
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = trimmed.Split(new[] { ',', ';', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || parts[0].Equals("Id", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var beforeCount = Packages.Count;
                AddPackage(parts[0], parts[1]);
                if (Packages.Count > beforeCount)
                {
                    imported++;
                }
            }

            Summary = $"Imported {imported} package(s).";
        }

        public void ExportPackageList(string path)
        {
            var lines = new List<string> { "Id,Version" };
            lines.AddRange(Packages
                .Where(p => p.Kind == "Root")
                .Select(p => $"{EscapeCsv(p.Id)},{EscapeCsv(p.Version)}"));
            File.WriteAllLines(path, lines, Encoding.UTF8);
            Summary = $"Exported {lines.Count - 1} package(s).";
        }

        private void AddOrUpdatePackageOnUiThread(PackageModel package)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var existing = Packages.FirstOrDefault(p => p.Key == package.Key);
                if (existing == null)
                {
                    Packages.Add(package);
                }
                else
                {
                    CopyPackageState(package, existing);
                }
            });
        }

        private void UpdatePackageOnUiThread(PackageModel package)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var existing = Packages.FirstOrDefault(p => p.Key == package.Key);
                if (existing == null)
                {
                    Packages.Add(package);
                    return;
                }

                CopyPackageState(package, existing);
            });
        }

        private static void CopyPackageState(PackageModel source, PackageModel target)
        {
            target.Kind = source.Kind;
            target.Status = source.Status;
            target.Message = source.Message;
            target.Progress = source.Progress;
        }

        private async Task RefreshPackageIdSuggestionsAsync()
        {
            packageSuggestionCancellation?.Cancel();
            packageSuggestionCancellation = new CancellationTokenSource();
            var token = packageSuggestionCancellation.Token;

            try
            {
                var suggestions = await searchService.SearchPackageIdsAsync(PackageSource, PackageIdInput, take: 12, token);
                UpdateSuggestions(PackageIdSuggestions, suggestions);
                IsPackageIdSuggestionsOpen = isPackageIdInputActive && suggestions.Count > 0;
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                UpdateSuggestions(PackageIdSuggestions, Array.Empty<string>());
                IsPackageIdSuggestionsOpen = false;
            }
        }

        private async Task RefreshVersionSuggestionsAsync()
        {
            versionSuggestionCancellation?.Cancel();
            versionSuggestionCancellation = new CancellationTokenSource();
            var token = versionSuggestionCancellation.Token;

            try
            {
                var suggestions = await searchService.GetVersionsAsync(PackageSource, PackageIdInput, VersionInput, take: 12, token);
                UpdateSuggestions(VersionSuggestions, suggestions);
                IsVersionSuggestionsOpen = isVersionInputActive && suggestions.Count > 0;
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                UpdateSuggestions(VersionSuggestions, Array.Empty<string>());
                IsVersionSuggestionsOpen = false;
            }
        }

        private static void UpdateSuggestions(ObservableCollection<string> target, IReadOnlyList<string> suggestions)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                target.Clear();
                foreach (var suggestion in suggestions)
                {
                    target.Add(suggestion);
                }
            });
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
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
