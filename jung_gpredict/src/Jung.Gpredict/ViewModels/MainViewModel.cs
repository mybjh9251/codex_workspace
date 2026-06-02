using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Jung.Gpredict.Models;
using Jung.Gpredict.Services;
using Microsoft.Win32;

namespace Jung.Gpredict.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly TleFileParser _tleFileParser = new();
    private readonly PassPredictionService _passPredictionService = new();
    private IReadOnlyList<TleRecord> _loadedTles = [];
    private GroundStation? _selectedGroundStation;
    private PassPredictionRow? _selectedPass;
    private string _statusMessage = "TLE_Download 출력 파일을 열거나 끌어 놓으세요.";
    private string _loadedSatelliteSummary = "Loaded: 0 satellites";
    private int _predictionDays = 7;
    private int _maxPassesPerSatellite = 3;
    private double _minElevationDeg;

    public MainViewModel()
    {
        GroundStations = new ObservableCollection<GroundStation>(GroundStationCatalog.KoreaStations);
        SelectedGroundStation = GroundStations.Count > 0 ? GroundStations[0] : null;
        BrowseTleCommand = new RelayCommand(BrowseTleFile);
        RecalculateCommand = new RelayCommand(RefreshPredictions);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<GroundStation> GroundStations { get; }
    public ObservableCollection<SatelliteSelection> SatelliteSelections { get; } = [];
    public ObservableCollection<PassPredictionRow> Passes { get; } = [];
    public ICommand BrowseTleCommand { get; }
    public ICommand RecalculateCommand { get; }

    public GroundStation? SelectedGroundStation
    {
        get => _selectedGroundStation;
        set
        {
            if (SetProperty(ref _selectedGroundStation, value))
            {
                RefreshPredictions();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string LoadedSatelliteSummary
    {
        get => _loadedSatelliteSummary;
        private set => SetProperty(ref _loadedSatelliteSummary, value);
    }

    public PassPredictionRow? SelectedPass
    {
        get => _selectedPass;
        set => SetProperty(ref _selectedPass, value);
    }

    public int PredictionDays
    {
        get => _predictionDays;
        set
        {
            var bounded = Math.Clamp(value, 1, 30);
            if (SetProperty(ref _predictionDays, bounded))
            {
                RefreshPredictions();
            }
        }
    }

    public int MaxPassesPerSatellite
    {
        get => _maxPassesPerSatellite;
        set
        {
            var bounded = Math.Clamp(value, 1, 10);
            if (SetProperty(ref _maxPassesPerSatellite, bounded))
            {
                RefreshPredictions();
            }
        }
    }

    public double MinElevationDeg
    {
        get => _minElevationDeg;
        set
        {
            var bounded = Math.Clamp(value, 0, 45);
            if (SetProperty(ref _minElevationDeg, bounded))
            {
                RefreshPredictions();
            }
        }
    }

    public bool ShowSatellite { get => _showSatellite; set { if (SetProperty(ref _showSatellite, value)) OnColumnVisibilityChanged(); } }
    public bool ShowPassIndex { get => _showPassIndex; set { if (SetProperty(ref _showPassIndex, value)) OnColumnVisibilityChanged(); } }
    public bool ShowAos { get => _showAos; set { if (SetProperty(ref _showAos, value)) OnColumnVisibilityChanged(); } }
    public bool ShowTca { get => _showTca; set { if (SetProperty(ref _showTca, value)) OnColumnVisibilityChanged(); } }
    public bool ShowLos { get => _showLos; set { if (SetProperty(ref _showLos, value)) OnColumnVisibilityChanged(); } }
    public bool ShowDuration { get => _showDuration; set { if (SetProperty(ref _showDuration, value)) OnColumnVisibilityChanged(); } }
    public bool ShowMaxElevation { get => _showMaxElevation; set { if (SetProperty(ref _showMaxElevation, value)) OnColumnVisibilityChanged(); } }
    public bool ShowAosAzimuth { get => _showAosAzimuth; set { if (SetProperty(ref _showAosAzimuth, value)) OnColumnVisibilityChanged(); } }
    public bool ShowMaxElevationAzimuth { get => _showMaxElevationAzimuth; set { if (SetProperty(ref _showMaxElevationAzimuth, value)) OnColumnVisibilityChanged(); } }
    public bool ShowLosAzimuth { get => _showLosAzimuth; set { if (SetProperty(ref _showLosAzimuth, value)) OnColumnVisibilityChanged(); } }
    public bool ShowRange { get => _showRange; set { if (SetProperty(ref _showRange, value)) OnColumnVisibilityChanged(); } }
    public bool ShowRangeRate { get => _showRangeRate; set { if (SetProperty(ref _showRangeRate, value)) OnColumnVisibilityChanged(); } }

    public Visibility SatelliteColumnVisibility => ToVisibility(ShowSatellite);
    public Visibility PassIndexColumnVisibility => ToVisibility(ShowPassIndex);
    public Visibility AosColumnVisibility => ToVisibility(ShowAos);
    public Visibility TcaColumnVisibility => ToVisibility(ShowTca);
    public Visibility LosColumnVisibility => ToVisibility(ShowLos);
    public Visibility DurationColumnVisibility => ToVisibility(ShowDuration);
    public Visibility MaxElevationColumnVisibility => ToVisibility(ShowMaxElevation);
    public Visibility AosAzimuthColumnVisibility => ToVisibility(ShowAosAzimuth);
    public Visibility MaxElevationAzimuthColumnVisibility => ToVisibility(ShowMaxElevationAzimuth);
    public Visibility LosAzimuthColumnVisibility => ToVisibility(ShowLosAzimuth);
    public Visibility RangeColumnVisibility => ToVisibility(ShowRange);
    public Visibility RangeRateColumnVisibility => ToVisibility(ShowRangeRate);

    private bool _showSatellite = true;
    private bool _showPassIndex = true;
    private bool _showAos = true;
    private bool _showTca = true;
    private bool _showLos = true;
    private bool _showDuration = true;
    private bool _showMaxElevation = true;
    private bool _showAosAzimuth = true;
    private bool _showMaxElevationAzimuth = true;
    private bool _showLosAzimuth = true;
    private bool _showRange = true;
    private bool _showRangeRate = true;

    public void LoadTleFile(string filePath)
    {
        try
        {
            _loadedTles = _tleFileParser.ParseFile(filePath);
            ReplaceSatelliteSelections(_loadedTles);
            UpdateLoadedSatelliteSummary();
            StatusMessage = $"{Path.GetFileName(filePath)} 파일을 불러왔습니다.";
            RefreshPredictions();
        }
        catch (Exception ex)
        {
            StatusMessage = $"TLE 파일을 읽지 못했습니다: {ex.Message}";
        }
    }

    private void BrowseTleFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "TLE files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "TLE_Download 출력 파일 선택"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadTleFile(dialog.FileName);
        }
    }

    private void RefreshPredictions()
    {
        Passes.Clear();
        SelectedPass = null;

        if (SelectedGroundStation is null || _loadedTles.Count == 0)
        {
            return;
        }

        var selectedTles = SatelliteSelections
            .Where(selection => selection.IsIncluded)
            .Select(selection => selection.Tle)
            .ToList();

        UpdateLoadedSatelliteSummary();

        if (selectedTles.Count == 0)
        {
            StatusMessage = "예측할 위성을 하나 이상 선택하세요.";
            return;
        }

        try
        {
            var rows = _passPredictionService.PredictFirstPasses(
                selectedTles,
                SelectedGroundStation,
                DateTime.UtcNow,
                PredictionDays,
                MinElevationDeg,
                MaxPassesPerSatellite);

            foreach (var row in rows)
            {
                Passes.Add(row);
            }

            SelectedPass = Passes.FirstOrDefault();
            StatusMessage = $"{SelectedGroundStation.Name} 기준으로 {Passes.Count}개 패스를 계산했습니다.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"패스 계산 중 오류가 발생했습니다: {ex.Message}";
        }
    }

    private void OnColumnVisibilityChanged()
    {
        OnPropertyChanged(nameof(SatelliteColumnVisibility));
        OnPropertyChanged(nameof(PassIndexColumnVisibility));
        OnPropertyChanged(nameof(AosColumnVisibility));
        OnPropertyChanged(nameof(TcaColumnVisibility));
        OnPropertyChanged(nameof(LosColumnVisibility));
        OnPropertyChanged(nameof(DurationColumnVisibility));
        OnPropertyChanged(nameof(MaxElevationColumnVisibility));
        OnPropertyChanged(nameof(AosAzimuthColumnVisibility));
        OnPropertyChanged(nameof(MaxElevationAzimuthColumnVisibility));
        OnPropertyChanged(nameof(LosAzimuthColumnVisibility));
        OnPropertyChanged(nameof(RangeColumnVisibility));
        OnPropertyChanged(nameof(RangeRateColumnVisibility));
    }

    private static Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private void ReplaceSatelliteSelections(IEnumerable<TleRecord> records)
    {
        foreach (var selection in SatelliteSelections)
        {
            selection.PropertyChanged -= SatelliteSelection_PropertyChanged;
        }

        SatelliteSelections.Clear();

        foreach (var selection in records.Select(record => new SatelliteSelection(record)))
        {
            selection.PropertyChanged += SatelliteSelection_PropertyChanged;
            SatelliteSelections.Add(selection);
        }
    }

    private void SatelliteSelection_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SatelliteSelection.IsIncluded))
        {
            RefreshPredictions();
        }
    }

    private void UpdateLoadedSatelliteSummary()
    {
        var selectedCount = SatelliteSelections.Count(selection => selection.IsIncluded);
        LoadedSatelliteSummary = $"Loaded: {_loadedTles.Count} satellites / Selected: {selectedCount}";
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
