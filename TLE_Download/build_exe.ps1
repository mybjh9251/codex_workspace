param(
    [string]$Python = "python"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSCommandPath
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
$machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
$env:Path = "$userPath;$machinePath"

Set-Location $projectRoot

function Remove-ProjectChild {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ChildPath
    )

    $candidate = Join-Path $projectRoot $ChildPath
    if (-not (Test-Path -LiteralPath $candidate)) {
        return
    }

    $rootPath = (Resolve-Path -LiteralPath $projectRoot).Path.TrimEnd('\')
    $resolvedPath = (Resolve-Path -LiteralPath $candidate).Path
    if (-not $resolvedPath.StartsWith($rootPath + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove path outside project root: $resolvedPath"
    }

    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
}

Remove-ProjectChild "build"
Remove-ProjectChild "dist"

& $Python -m PyInstaller `
  --clean `
  --noconfirm `
  --onefile `
  --name TLE_Download `
  --paths src `
  main.py
if ($LASTEXITCODE -ne 0) {
    throw "PyInstaller build failed."
}

$distDir = Join-Path $projectRoot "dist"
$templateProfile = Join-Path $distDir "profile.template.xml"
Copy-Item -LiteralPath (Join-Path $projectRoot "dist.README.md") -Destination (Join-Path $distDir "README.md") -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "profile.template.xml") -Destination $templateProfile -Force

Write-Host ""
Write-Host "Build complete." -ForegroundColor Green
Write-Host "Executable: $(Join-Path $distDir 'TLE_Download.exe')"
Write-Host "Template profile: $templateProfile"
