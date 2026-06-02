using System;

namespace Jung.Gpredict.Models;

public sealed class PassPredictionRow
{
    private static readonly TimeZoneInfo KoreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");

    public required string SatelliteName { get; init; }
    public required int PassIndex { get; init; }
    public required DateTime AosUtc { get; init; }
    public required DateTime TcaUtc { get; init; }
    public required DateTime LosUtc { get; init; }
    public required double MaxElevationDeg { get; init; }
    public required double AosAzimuthDeg { get; init; }
    public required double MaxElevationAzimuthDeg { get; init; }
    public required double LosAzimuthDeg { get; init; }
    public required double RangeKmAtMax { get; init; }
    public required double RangeRateKmPerSecAtMax { get; init; }

    public string AosLocalText => FormatKst(AosUtc);
    public string TcaLocalText => FormatKst(TcaUtc);
    public string LosLocalText => FormatKst(LosUtc);
    public string DurationText => FormatDuration(LosUtc - AosUtc);
    public string MaxElevationText => $"{MaxElevationDeg:F1} deg";
    public string AosAzimuthText => $"{AosAzimuthDeg:F1} deg";
    public string MaxElevationAzimuthText => $"{MaxElevationAzimuthDeg:F1} deg";
    public string LosAzimuthText => $"{LosAzimuthDeg:F1} deg";
    public string RangeKmText => $"{RangeKmAtMax:F1} km";
    public string RangeRateText => $"{RangeRateKmPerSecAtMax:F3} km/s";

    private static string FormatKst(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), KoreaTimeZone);
        return $"{local:yyyy-MM-dd HH:mm:ss}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{duration.Minutes:D2}:{duration.Seconds:D2}";
    }
}
