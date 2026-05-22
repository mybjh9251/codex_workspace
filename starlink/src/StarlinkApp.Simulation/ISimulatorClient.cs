using StarlinkApp.Contracts;

namespace StarlinkApp.Simulation;

public interface ISimulatorClient
{
    IReadOnlyList<ScenarioDefinition> GetScenarios();

    TelemetrySnapshot GetSnapshot();

    TelemetrySnapshot SetScenario(string scenarioKey);

    CommandAck SendCommand(CommandRequest request);
}
