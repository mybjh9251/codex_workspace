param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishRoot = Join-Path $root "artifacts\publish-smoke"
$appPublish = Join-Path $publishRoot "StarlinkApp"
$simPublish = Join-Path $publishRoot "StarlinkSimulator"

if (Test-Path $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

dotnet publish (Join-Path $root "src\StarlinkApp\StarlinkApp.csproj") -c $Configuration -o $appPublish --no-restore
dotnet publish (Join-Path $root "src\StarlinkSimulator\StarlinkSimulator.csproj") -c $Configuration -o $simPublish --no-restore

$exe = Join-Path $appPublish "StarlinkApp.exe"
$process = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 3

if ($process.HasExited) {
    throw "StarlinkApp exited early with code $($process.ExitCode)."
}

Stop-Process -Id $process.Id -Force
"PUBLISH_SMOKE_OK"
