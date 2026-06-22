using System.Windows;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Media;

namespace NuGetDependencyDownloaderWPF
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();
            viewModel = new MainViewModel();
            DataContext = viewModel;
            ApplyTheme("Light");
        }

        private void AddPackage_Click(object sender, RoutedEventArgs e)
        {
            viewModel.AddCurrentPackage();
        }

        private void RemovePackage_Click(object sender, RoutedEventArgs e)
        {
            if (PackageListView.SelectedItem is PackageModel selected)
            {
                viewModel.RemovePackage(selected);
            }
        }

        private async void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await viewModel.StartDownloadAsync();
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, "Download failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RetryFailed_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await viewModel.RetryFailedAsync();
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, "Retry failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            viewModel.CancelDownload();
        }

        private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select package output folder",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                viewModel.SetOutputFolder(dialog.SelectedPath);
            }
        }

        private void ImportPackageList_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Package list (*.csv;*.txt)|*.csv;*.txt|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) == true)
            {
                viewModel.ImportPackageList(dialog.FileName);
            }
        }

        private void ExportPackageList_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "package-list.csv",
                Filter = "CSV (*.csv)|*.csv|Text (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) == true)
            {
                viewModel.ExportPackageList(dialog.FileName);
            }
        }

        private void PackageIdSuggestion_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is string packageId)
            {
                viewModel.SelectPackageIdSuggestion(packageId);
                listBox.SelectedItem = null;
                PackageVersionTextBox.Focus();
            }
        }

        private void VersionSuggestion_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is string version)
            {
                viewModel.SelectVersionSuggestion(version);
                listBox.SelectedItem = null;
                PackageVersionTextBox.Focus();
            }
        }

        private void PackageIdTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            viewModel.ActivatePackageIdInput();
        }

        private void PackageVersionTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            viewModel.ActivateVersionInput();
        }

        private void Window_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!PackageIdTextBox.IsMouseOver && !PackageIdSuggestionsPopup.IsMouseOver)
            {
                viewModel.IsPackageIdSuggestionsOpen = false;
            }

            if (!PackageVersionTextBox.IsMouseOver && !VersionSuggestionsPopup.IsMouseOver)
            {
                viewModel.IsVersionSuggestionsOpen = false;
            }

            if (!PackageIdTextBox.IsMouseOver
                && !PackageVersionTextBox.IsMouseOver
                && !PackageIdSuggestionsPopup.IsMouseOver
                && !VersionSuggestionsPopup.IsMouseOver)
            {
                viewModel.DeactivateAutocomplete();
            }
        }

        private void PackageIdSuggestionsPopup_Closed(object sender, System.EventArgs e)
        {
            viewModel.IsPackageIdSuggestionsOpen = false;
        }

        private void VersionSuggestionsPopup_Closed(object sender, System.EventArgs e)
        {
            viewModel.IsVersionSuggestionsOpen = false;
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox?.SelectedItem is ComboBoxItem selected && selected.Content is string themeName)
            {
                ApplyTheme(themeName);
            }
        }

        private void ApplyTheme(string themeName)
        {
            if (themeName == "Black")
            {
                SetBrush("WindowBackgroundBrush", "#000000");
                SetBrush("SurfaceBrush", "#1C1C1E");
                SetBrush("CardBorderBrush", "#38383A");
                SetBrush("ButtonBackgroundBrush", "#2C2C2E");
                SetBrush("ButtonHoverBrush", "#3A3A3C");
                SetBrush("ButtonPressedBrush", "#48484A");
                SetBrush("TableHeaderBrush", "#2C2C2E");
                SetBrush("ProgressTrackBrush", "#3A3A3C");
                SetBrush("SuggestionHoverBrush", "#26364D");
                SetBrush("SuggestionSelectedBrush", "#174A7C");
                SetBrush("ControlBorderBrush", "#48484A");
                SetBrush("ControlHoverBorderBrush", "#636366");
                SetBrush("PrimaryBrush", "#0A84FF");
                SetBrush("PrimaryHoverBrush", "#409CFF");
                SetBrush("TextBrush", "#F5F5F7");
                SetBrush("MutedTextBrush", "#A1A1A6");
                return;
            }

            SetBrush("WindowBackgroundBrush", "#F5F5F7");
            SetBrush("SurfaceBrush", "#FFFFFF");
            SetBrush("CardBorderBrush", "#E5E5EA");
            SetBrush("ButtonBackgroundBrush", "#FFFFFF");
            SetBrush("ButtonHoverBrush", "#F2F2F4");
            SetBrush("ButtonPressedBrush", "#E8E8ED");
            SetBrush("TableHeaderBrush", "#F5F5F7");
            SetBrush("ProgressTrackBrush", "#E8E8ED");
            SetBrush("SuggestionHoverBrush", "#EEF5FF");
            SetBrush("SuggestionSelectedBrush", "#DCEBFF");
            SetBrush("ControlBorderBrush", "#D2D2D7");
            SetBrush("ControlHoverBorderBrush", "#A7A7AD");
            SetBrush("PrimaryBrush", "#007AFF");
            SetBrush("PrimaryHoverBrush", "#006EDB");
            SetBrush("TextBrush", "#1D1D1F");
            SetBrush("MutedTextBrush", "#6E6E73");
        }

        private void SetBrush(string key, string color)
        {
            Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }
    }
}
