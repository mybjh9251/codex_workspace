using System;
using System.Collections.Generic;
using System.Linq;
using Jung.Gpredict.Models;
using SGPdotNET.CoordinateSystem;
using SGPdotNET.Observation;
using SGPdotNET.Util;

namespace Jung.Gpredict.Services;

public sealed class PassPredictionService
{
    private static readonly TimeSpan CoarseStep = TimeSpan.FromSeconds(30);

    public IReadOnlyList<PassPredictionRow> PredictFirstPasses(
        IEnumerable<TleRecord> tleRecords,
        Models.GroundStation selectedStation,
        DateTime startUtc,
        int predictionDays,
        double minElevationDeg,
        int passCountPerSatellite = 3)
    {
        var observer = new GeodeticCoordinate(
            Angle.FromDegrees(selectedStation.LatitudeDeg),
            Angle.FromDegrees(selectedStation.LongitudeDeg),
            selectedStation.AltitudeMeters / 1000.0);

        var groundStation = new SGPdotNET.Observation.GroundStation(observer);
        startUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        var endUtc = startUtc + TimeSpan.FromDays(predictionDays);
        var rows = new List<PassPredictionRow>();

        foreach (var tle in tleRecords)
        {
            Satellite satellite;
            try
            {
                satellite = new Satellite(tle.SatelliteName, tle.Line1, tle.Line2);
            }
            catch
            {
                continue;
            }

            var periods = groundStation
                .Observe(
                    satellite,
                    startUtc,
                    endUtc,
                    CoarseStep,
                    minElevation: Angle.FromDegrees(minElevationDeg),
                    clipToStartTime: true,
                    clipToEndTime: true,
                    resolution: 1)
                .Take(passCountPerSatellite)
                .ToList();

            for (var i = 0; i < periods.Count; i++)
            {
                var period = periods[i];
                var aosObservation = groundStation.Observe(satellite, period.Start);
                var losObservation = groundStation.Observe(satellite, period.End);
                var maxObservation = groundStation.Observe(satellite, period.MaxElevationTime);

                rows.Add(new PassPredictionRow
                {
                    SatelliteName = tle.SatelliteName,
                    PassIndex = i + 1,
                    AosUtc = period.Start,
                    TcaUtc = period.MaxElevationTime,
                    LosUtc = period.End,
                    MaxElevationDeg = period.MaxElevation.Degrees,
                    AosAzimuthDeg = aosObservation.Azimuth.Degrees,
                    MaxElevationAzimuthDeg = maxObservation.Azimuth.Degrees,
                    LosAzimuthDeg = losObservation.Azimuth.Degrees,
                    RangeKmAtMax = maxObservation.Range,
                    RangeRateKmPerSecAtMax = maxObservation.RangeRate
                });
            }
        }

        return rows
            .OrderBy(row => row.AosUtc)
            .ThenBy(row => row.SatelliteName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
