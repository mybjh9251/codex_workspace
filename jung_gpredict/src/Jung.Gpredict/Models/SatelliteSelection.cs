using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Jung.Gpredict.Models;

public sealed class SatelliteSelection : INotifyPropertyChanged
{
    private bool _isIncluded = true;

    public SatelliteSelection(TleRecord tle)
    {
        Tle = tle;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TleRecord Tle { get; }
    public string SatelliteName => Tle.SatelliteName;

    public bool IsIncluded
    {
        get => _isIncluded;
        set
        {
            if (_isIncluded == value)
            {
                return;
            }

            _isIncluded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIncluded)));
        }
    }
}
