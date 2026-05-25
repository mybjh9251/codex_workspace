using System.Windows.Input;
using StarlinkApp.Contracts;

namespace StarlinkApp.ViewModels;

public sealed class ObstructionsPageViewModel : ViewModelBase
{
    private string _obstructionStatusText = "Clear view";
    private string _obstructionDetailText = string.Empty;
    private string _obstructionPercentText = "0.0 % obstructed";
    private string _scanProgressText = "Scan progress 0 %";
    private string _lastScanText = "Not scanned this session";
    private string _scanButtonText = "Check for obstructions";
    private string _selectedCellTitle = "Sky sector";
    private string _selectedCellDetail = "Select a sector to inspect obstruction detail.";
    private double _scanProgressBarWidth;
    private int _selectedRow = 2;
    private int _selectedColumn = 2;
    private IReadOnlyList<ObstructionCellViewModel> _cells = [];

    public ObstructionsPageViewModel(ICommand checkObstructionsCommand)
    {
        CheckObstructionsCommand = checkObstructionsCommand;
        SelectCellCommand = new RelayCommand(SelectCell);
    }

    public ICommand CheckObstructionsCommand { get; }

    public ICommand SelectCellCommand { get; }

    public string ObstructionStatusText
    {
        get => _obstructionStatusText;
        private set => SetProperty(ref _obstructionStatusText, value);
    }

    public string ObstructionDetailText
    {
        get => _obstructionDetailText;
        private set => SetProperty(ref _obstructionDetailText, value);
    }

    public string ObstructionPercentText
    {
        get => _obstructionPercentText;
        private set => SetProperty(ref _obstructionPercentText, value);
    }

    public string ScanProgressText
    {
        get => _scanProgressText;
        private set => SetProperty(ref _scanProgressText, value);
    }

    public string LastScanText
    {
        get => _lastScanText;
        private set => SetProperty(ref _lastScanText, value);
    }

    public string ScanButtonText
    {
        get => _scanButtonText;
        private set => SetProperty(ref _scanButtonText, value);
    }

    public string SelectedCellTitle
    {
        get => _selectedCellTitle;
        private set => SetProperty(ref _selectedCellTitle, value);
    }

    public string SelectedCellDetail
    {
        get => _selectedCellDetail;
        private set => SetProperty(ref _selectedCellDetail, value);
    }

    public double ScanProgressBarWidth
    {
        get => _scanProgressBarWidth;
        private set => SetProperty(ref _scanProgressBarWidth, value);
    }

    public IReadOnlyList<ObstructionCellViewModel> Cells
    {
        get => _cells;
        private set => SetProperty(ref _cells, value);
    }

    public void Update(TelemetrySnapshot snapshot)
    {
        var obstruction = snapshot.Obstruction;

        ObstructionStatusText = obstruction.Severity switch
        {
            ObstructionSeverity.Heavy => "Heavy obstruction detected",
            ObstructionSeverity.Partial => "Obstruction detected",
            _ => "Clear view"
        };
        ObstructionDetailText = obstruction.Recommendation;
        ObstructionPercentText = $"{obstruction.ObstructedPercent:0.0} % obstructed";
        ScanProgressText = $"Scan progress {obstruction.ScanProgressPercent} %";
        LastScanText = obstruction.LastScanLabel;
        ScanButtonText = obstruction.ScanProgressPercent is > 0 and < 100
            ? "Scanning sky..."
            : "Check for obstructions";
        ScanProgressBarWidth = Math.Clamp(obstruction.ScanProgressPercent * 2.2, 0, 220);

        var cells = obstruction.Cells
            .Select(cell => ObstructionCellViewModel.FromCell(
                cell,
                cell.Row == _selectedRow && cell.Column == _selectedColumn))
            .ToArray();

        Cells = cells;
        UpdateSelectedCellText(cells);
    }

    private void SelectCell(object? parameter)
    {
        if (parameter is not ObstructionCellViewModel selected)
        {
            return;
        }

        _selectedRow = selected.Row;
        _selectedColumn = selected.Column;
        Cells = Cells
            .Select(cell => cell with { IsSelected = cell.Row == selected.Row && cell.Column == selected.Column })
            .ToArray();
        UpdateSelectedCellText(Cells);
    }

    private void UpdateSelectedCellText(IReadOnlyList<ObstructionCellViewModel> cells)
    {
        var selected = cells.FirstOrDefault(cell => cell.IsSelected) ?? cells.FirstOrDefault();

        if (selected is null)
        {
            SelectedCellTitle = "Sky sector";
            SelectedCellDetail = "Select a sector to inspect obstruction detail.";
            return;
        }

        SelectedCellTitle = selected.CoordinateText;
        SelectedCellDetail = selected.DetailText;
    }
}
