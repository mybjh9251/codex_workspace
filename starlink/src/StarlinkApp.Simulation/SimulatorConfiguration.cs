using StarlinkApp.Contracts;

namespace StarlinkApp.Simulation;

public sealed record SimulatorConfiguration(
    AppSettings Settings,
    IReadOnlyList<ScenarioDefinition> Scenarios,
    IReadOnlyList<string> Warnings);
