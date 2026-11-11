# Builds the Steam Frame plugin (and the test projects) in Release mode.
# Usage:  .\scripts\Build.ps1            (from the SteamFrame folder; works from anywhere)
#         .\scripts\Build.ps1 -ValheimDir "D:\Games\Valheim"
param(
    [string]$ValheimDir = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Write-Host "Building the Steam Frame plugin (Release)..."
$buildArgs = @('build', (Join-Path $root 'SteamFrame.sln'), '-c', 'Release', '-nologo', '-v', 'minimal', '-nowarn:MSB3277')
if ($ValheimDir -ne '') { $buildArgs += "-p:ValheimDir=$ValheimDir" }
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED. Read the first 'error' line above." -ForegroundColor Red
    exit 1
}

$dll = Join-Path $root 'src\VhvrSteamFrame\bin\Release\netstandard2.1\VhvrSteamFrame.dll'
if (-not (Test-Path $dll)) {
    Write-Host "Build reported success but $dll is missing." -ForegroundColor Red
    exit 1
}
$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion
Write-Host "Build OK: VhvrSteamFrame.dll $version" -ForegroundColor Green
Write-Host "  $dll"
Write-Host "Next: .\scripts\Test.ps1, then .\scripts\Install.ps1 (with Valheim closed)."
exit 0
