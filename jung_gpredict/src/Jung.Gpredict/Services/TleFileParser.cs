using System;
using System.Collections.Generic;
using System.IO;
using Jung.Gpredict.Models;

namespace Jung.Gpredict.Services;

public sealed class TleFileParser
{
    public IReadOnlyList<TleRecord> ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("TLE file was not found.", path);
        }

        var lines = File.ReadAllLines(path);
        var normalized = new List<string>();
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                normalized.Add(line.TrimEnd());
            }
        }

        if (normalized.Count % 3 != 0)
        {
            throw new FormatException("TLE_Download output must repeat satellite name, line 1, and line 2.");
        }

        var records = new List<TleRecord>();
        for (var i = 0; i < normalized.Count; i += 3)
        {
            var satName = normalized[i].Trim();
            var line1 = normalized[i + 1];
            var line2 = normalized[i + 2];

            if (!line1.StartsWith("1 ", StringComparison.Ordinal) ||
                !line2.StartsWith("2 ", StringComparison.Ordinal))
            {
                throw new FormatException($"Invalid TLE block near line {i + 1}.");
            }

            records.Add(new TleRecord(satName, line1, line2));
        }

        return records;
    }
}
