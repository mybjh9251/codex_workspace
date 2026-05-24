namespace StarlinkApp.Simulation;

public sealed record SimulatorEndpoint(string Host, int Port)
{
    public static SimulatorEndpoint Parse(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return new SimulatorEndpoint(
                string.IsNullOrWhiteSpace(uri.Host) ? "127.0.0.1" : uri.Host,
                uri.Port > 0 ? uri.Port : 5517);
        }

        var parts = value.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[1], out var port))
        {
            return new SimulatorEndpoint(parts[0], port);
        }

        return new SimulatorEndpoint("127.0.0.1", 5517);
    }

    public Uri ToUri() => new($"tcp://{Host}:{Port}");
}
