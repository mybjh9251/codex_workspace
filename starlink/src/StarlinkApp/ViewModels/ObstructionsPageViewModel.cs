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
    private IReadOnlyList<ObstructionCellViewModel> _cells = [];

    public ObstructionsPageViewModel(ICommand checkObstructionsCommand)
    {
        CheckObstructionsCommand = checkObstructionsCommand;
    }

    public ICommand CheckObstructionsCommand { get; }

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
        Cells = obstruction.Cells.Select(ObstructionCellViewModel.FromCell).ToArray();
    }
}
