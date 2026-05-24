using StarlinkApp.Contracts;

namespace StarlinkApp.ViewModels;

public sealed record ObstructionCellViewModel(int Row, int Column, int Level, string Marker)
{
    public static ObstructionCellViewModel FromCell(ObstructionCell cell)
    {
        return new ObstructionCellViewModel(cell.Row, cell.Column, cell.Level, cell.Level > 0 ? "!" : string.Empty);
    }
}
