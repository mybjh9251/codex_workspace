using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using StarlinkApp.Contracts;

namespace StarlinkApp.Simulation;

public sealed class TcpSimulatorServer
{
    private readonly SimulationEngine _engine;
    private readonly SimulatorEndpoint _endpoint;
    private TcpListener? _listener;

    public TcpSimulatorServer(AppSettings settings, IReadOnlyList<ScenarioDefinition> scenarios)
        : this(settings, scenarios, SimulatorEndpoint.Parse(settings.SimulatorEndpoint))
    {
    }

    public TcpSimulatorServer(
        AppSettings settings,
        IReadOnlyList<ScenarioDefinition> scenarios,
        SimulatorEndpoint endpoint)
    {
        _engine = new SimulationEngine(settings, scenarios);
        _endpoint = endpoint;
    }

    public int BoundPort => _listener?.LocalEndpoint is IPEndPoint endpoint ? endpoint.Port : 0;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var address = IPAddress.TryParse(_endpoint.Host, out var parsedAddress)
            ? parsedAddress
            : IPAddress.Loopback;

        _listener = new TcpListener(address, _endpoint.Port);
        _listener.Start();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path for tests and console host stop.
        }
        finally
        {
            _listener.Stop();
        }
    }

    public SimulatorResponse Process(SimulatorRequest request)
    {
        if (request.Type.Equals(SimulatorProtocol.Health, StringComparison.OrdinalIgnoreCase))
        {
            return new SimulatorResponse(true, "Starlink simulator server is running.", _engine.GetSnapshot());
        }

        if (request.Type.Equals(SimulatorProtocol.GetScenarios, StringComparison.OrdinalIgnoreCase))
        {
            return new SimulatorResponse(true, "Scenario list loaded.", Scenarios: _engine.GetScenarios());
        }

        if (request.Type.Equals(SimulatorProtocol.GetSnapshot, StringComparison.OrdinalIgnoreCase))
        {
            return new SimulatorResponse(true, "Snapshot loaded.", _engine.GetSnapshot());
        }

        if (request.Type.Equals(SimulatorProtocol.SetScenario, StringComparison.OrdinalIgnoreCase))
        {
            var snapshot = _engine.SetScenario(request.Argument ?? string.Empty);
            return new SimulatorResponse(true, $"Scenario changed: {snapshot.StatusTitle}", snapshot);
        }

        if (request.Type.Equals(SimulatorProtocol.Command, StringComparison.OrdinalIgnoreCase))
        {
            var ack = _engine.Execute(new CommandRequest(request.Command ?? string.Empty, request.Argument));
            return new SimulatorResponse(ack.Accepted, ack.Message, ack.Snapshot);
        }

        return new SimulatorResponse(false, $"Unknown simulator request type: {request.Type}", _engine.GetSnapshot());
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var clientLease = client;

        using var reader = new StreamReader(client.GetStream());
        await using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

        var line = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        SimulatorResponse response;
        try
        {
            var request = JsonSerializer.Deserialize<SimulatorRequest>(line, SimulatorProtocol.JsonOptions);
            response = request is null
                ? new SimulatorResponse(false, "Empty simulator request.")
                : Process(request);
        }
        catch (JsonException ex)
        {
            response = new SimulatorResponse(false, $"Invalid simulator request JSON. {ex.Message}");
        }

        await writer.WriteLineAsync(JsonSerializer.Serialize(response, SimulatorProtocol.JsonOptions).AsMemory(), cancellationToken);
    }
}
