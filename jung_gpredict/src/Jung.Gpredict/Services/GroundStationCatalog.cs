using System.Collections.Generic;
using Jung.Gpredict.Models;

namespace Jung.Gpredict.Services;

public static class GroundStationCatalog
{
    public static IReadOnlyList<GroundStation> KoreaStations { get; } =
    [
        new("서울(연세대)", 37.5664, 126.9387, 70),
        new("용인(한화시스템)", 37.2411, 127.1776, 120),
        new("대전(한화시스템)", 36.3741, 127.3917, 90),
        new("대전(국방과학연구소)", 36.4204, 127.3980, 80),
        new("제주도(한화시스템)", 33.4996, 126.5312, 60),
        new("부산", 35.1796, 129.0756, 40),
        new("광주", 35.1595, 126.8526, 45)
    ];
}
