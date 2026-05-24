using StarlinkApp.Contracts;
using System.Net.Sockets;
using System.Text.Json;

namespace StarlinkApp.Simulation;

public sealed class TcpSimulatorClient : ISimulatorClient
{
    private readonly SimulatorEndpoint _endpoint;
    private readonly InProcessSimulatorClient _fallback;

    public TcpSimulatorClient(Uri endpoint, AppSettings settings, IReadOnlyList<ScenarioDefinition> scenarios)
        : this(SimulatorEndpoint.Parse(endpoint.ToString()), settings, scenarios)
    {
    }

    public TcpSimulatorClient(SimulatorEndpoint endpoint, AppSettings settings, IReadOnlyList<ScenarioDefinition> scenarios)
    {
        _endpoint = endpoint;
        _fallback = new InProcessSimulatorClient(settings, scenarios);
    }

    public IReadOnlyList<ScenarioDefinition> GetScenarios()
    {
        var response = TrySend(new SimulatorRequest(SimulatorProtocol.GetScenarios));
        return response?.Scenarios ?? _fallback.GetScenarios();
    }

    public TelemetrySnapshot GetSnapshot()
    {
        var response = TrySend(new SimulatorRequest(SimulatorProtocol.GetSnapshot));
        return response?.Snapshot ?? _fallback.GetSnapshot();
    }

    public TelemetrySnapshot SetScenario(string scenarioKey)
    {
        var response = TrySend(new SimulatorRequest(SimulatorProtocol.SetScenario, Argument: scenarioKey));
        return response?.Snapshot ?? _fallback.SetScenario(scenarioKey);
    }

    public CommandAck SendCommand(CommandRequest request)
    {
        var response = TrySend(new SimulatorRequest(SimulatorProtocol.Command, request.Command, request.Argument));
        if (response?.Snapshot is not null)
        {
            return new CommandAck(request.Command, response.Accepted, response.Message, response.Snapshot);
        }

        var ack = _fallback.SendCommand(request);

        return ack with
        {
            Message = $"TCP simulator adapter is not connected to {_endpoint.ToUri()}; fallback handled command. {ack.Message}"
        };
    }

    private SimulatorResponse? TrySend(SimulatorRequest request)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(_endpoint.Host, _endpoint.Port);
            if (!connectTask.Wait(TimeSpan.FromMilliseconds(350)))
            {
                return null;
            }

            using var reader = new StreamReader(client.GetStream());
            using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

            writer.WriteLine(JsonSerializer.Serialize(request, SimulatorProtocol.JsonOptions));
            var line = reader.ReadLine();

            return string.IsNullOrWhiteSpace(line)
                ? null
                : JsonSerializer.Deserialize<SimulatorResponse>(line, SimulatorProtocol.JsonOptions);
        }
        catch (IOException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (AggregateException)
        {
            return null;
        }
    }
}
