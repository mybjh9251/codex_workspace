using StarlinkApp.Contracts;

namespace StarlinkApp.Simulation;

public sealed class InProcessSimulatorClient : ISimulatorClient
{
    private readonly SimulationEngine _engine;

    public InProcessSimulatorClient()
        : this(AppSettings.Default, SimulatorConfigurationLoader.CreateDefaultScenarios())
    {
    }

    public InProcessSimulatorClient(AppSettings settings, IReadOnlyList<ScenarioDefinition> scenarios)
    {
        _engine = new SimulationEngine(settings, scenarios);
    }

    public IReadOnlyList<ScenarioDefinition> GetScenarios() => _engine.GetScenarios();

    public TelemetrySnapshot GetSnapshot() => _engine.GetSnapshot();

    public TelemetrySnapshot SetScenario(string scenarioKey) => _engine.SetScenario(scenarioKey);

    public CommandAck SendCommand(CommandRequest request) => _engine.Execute(request);
}
