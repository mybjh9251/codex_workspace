namespace StarlinkApp.Contracts;

public sealed record ObstructionSnapshot(
    ObstructionSeverity Severity,
    double ObstructedPercent,
    int ScanProgressPercent,
    string LastScanLabel,
    string Recommendation,
    IReadOnlyList<ObstructionCell> Cells);
