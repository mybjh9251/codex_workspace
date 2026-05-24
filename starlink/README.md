# StarlinkApp

`StarlinkApp` is a WPF desktop simulator that recreates the observable UX, screen flow, and state behavior of the Starlink mobile app for learning and implementation practice.

This app does not connect to real Starlink accounts, devices, private APIs, or device-control protocols. It uses local scenarios and an in-process simulator.
The simulator contract is separated so the WPF client can later talk to an external simulator process.

## Project Layout

```text
starlink/
├─ StarlinkApp.sln
├─ scripts/
├─ src/
│  ├─ StarlinkApp/
│  ├─ StarlinkApp.Contracts/
│  ├─ StarlinkApp.Simulation/
│  └─ StarlinkSimulator/
└─ tests/
   └─ StarlinkApp.Tests/
```

## Run

```powershell
dotnet build StarlinkApp.sln --no-restore
dotnet test StarlinkApp.sln --no-build
dotnet run --project .\src\StarlinkApp\StarlinkApp.csproj
```

Run the optional simulator server:

```powershell
dotnet run --project .\src\StarlinkSimulator\StarlinkSimulator.csproj
```

Run publish smoke:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-publish.ps1
```

## Runtime Files

Runtime root is the folder that contains the executable.

The app looks for these optional files beside the executable:

- `settings.json`
- `scenarios.json`

If either file is missing, the app runs with built-in defaults and writes a warning to the activity log and daily file log.

Templates are copied to the output folder:

- `settings.template.json`
- `scenarios.default.json`

`settings.json` can select the simulator transport:

```json
{
  "simulatorMode": "InProcess",
  "simulatorEndpoint": "tcp://127.0.0.1:5517"
}
```

Use `simulatorMode: "Tcp"` after starting `StarlinkSimulator`.

## Logging

File logs are written under:

```text
logs/starlink_yyyy-MM-dd.log
```

Do not commit real runtime logs, generated settings, credentials, or account/device data.

## Current Slice

The current slice includes:

- `Home`
- `Setup`
- `Obstructions`
- `Speed`
- `AdvancedSpeed`
- `Network`
- `Settings`
- `Support`
- `settings.json` / `scenarios.json` fallback behavior
- `settings.json` save/update flow
- obstruction, speed test, and network state snapshots
- page-specific view models
- `SimulationEngine`
- TCP simulator adapter and standalone `StarlinkSimulator` process
- publish smoke script
- simulator contract tests

Next implementation candidates are richer chart visuals, a more reference-like obstruction heatmap, persisted device actions, and UDP telemetry streaming.
