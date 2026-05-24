using StarlinkApp.Contracts;

namespace StarlinkApp.Simulation;

public sealed class TcpSimulatorClient : ISimulatorClient
{
    private readonly Uri _endpoint;
    private readonly InProcessSimulatorClient _fallback;

    public TcpSimulatorClient(Uri endpoint, AppSettings settings, IReadOnlyList<ScenarioDefinition> scenarios)
    {
        _endpoint = endpoint;
        _fallback = new InProcessSimulatorClient(settings, scenarios);
    }

    public IReadOnlyList<ScenarioDefinition> GetScenarios() => _fallback.GetScenarios();

    public TelemetrySnapshot GetSnapshot() => _fallback.GetSnapshot();

    public TelemetrySnapshot SetScenario(string scenarioKey) => _fallback.SetScenario(scenarioKey);

    public CommandAck SendCommand(CommandRequest request)
    {
        var ack = _fallback.SendCommand(request);

        return ack with
        {
            Message = $"TCP simulator adapter is not connected to {_endpoint}; fallback handled command. {ack.Message}"
        };
    }
}
