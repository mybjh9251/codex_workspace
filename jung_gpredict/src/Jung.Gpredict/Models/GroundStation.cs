namespace Jung.Gpredict.Models;

public sealed record GroundStation(
    string Name,
    double LatitudeDeg,
    double LongitudeDeg,
    double AltitudeMeters);
