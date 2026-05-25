using StarlinkApp.Contracts;

namespace StarlinkApp.ViewModels;

public sealed record ObstructionCellViewModel(
    int Row,
    int Column,
    int Level,
    string Marker,
    string CoordinateText,
    string DetailText,
    bool IsSelected)
{
    public static ObstructionCellViewModel FromCell(ObstructionCell cell, bool isSelected)
    {
        var detail = cell.Level switch
        {
            2 => "Heavy blockage in this sky sector.",
            1 => "Partial blockage in this sky sector.",
            _ => "Clear sky sector."
        };

        return new ObstructionCellViewModel(
            cell.Row,
            cell.Column,
            cell.Level,
            cell.Level > 0 ? "!" : string.Empty,
            $"Row {cell.Row + 1}, Column {cell.Column + 1}",
            detail,
            isSelected);
    }
}
