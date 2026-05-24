using StarlinkApp.Simulation;

var runtimeRoot = AppContext.BaseDirectory;
var configuration = SimulatorConfigurationLoader.Load(runtimeRoot);
var endpoint = SimulatorEndpoint.Parse(configuration.Settings.SimulatorEndpoint);
var server = new TcpSimulatorServer(configuration.Settings, configuration.Scenarios, endpoint);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"StarlinkSimulator listening on {endpoint.ToUri()}");
foreach (var warning in configuration.Warnings)
{
    Console.WriteLine($"warning: {warning}");
}

await server.RunAsync(cts.Token);
