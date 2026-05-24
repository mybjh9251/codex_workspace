# StarlinkApp

`StarlinkApp` is a WPF desktop simulator that recreates the observable UX, screen flow, and state behavior of the Starlink mobile app for learning and implementation practice.

This app does not connect to real Starlink accounts, devices, private APIs, or device-control protocols. It uses local scenarios and an in-process simulator.

## Project Layout

```text
starlink/
├─ StarlinkApp.sln
├─ src/
│  ├─ StarlinkApp/
│  ├─ StarlinkApp.Contracts/
│  └─ StarlinkApp.Simulation/
└─ tests/
   └─ StarlinkApp.Tests/
```

## Run

```powershell
dotnet build StarlinkApp.sln --no-restore
dotnet test StarlinkApp.sln --no-build
dotnet run --project .\src\StarlinkApp\StarlinkApp.csproj
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
- `settings.json` / `scenarios.json` fallback behavior
- simulator contract tests

Next implementation candidates are `AdvancedSpeed`, `Network`, and `Settings`.
