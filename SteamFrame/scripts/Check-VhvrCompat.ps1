# Checks that the VHVR and Valheim installed in your game folder still have everything this plugin reaches into,
# WITHOUT starting the game. Run it after updating VHVR or Valheim, before playing.
# Usage:  .\scripts\Check-VhvrCompat.ps1
#         .\scripts\Check-VhvrCompat.ps1 -ValheimDir "D:\Games\Valheim"
#         .\scripts\Check-VhvrCompat.ps1 -Describe FistCollision     (lists that type's members, to find a renamed one)
param(
    [string]$ValheimDir = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim',
    [string]$Describe = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'tools\VhvrCompatCheck'

Write-Host "Checking the installed VHVR and Valheim against the plugin's list of internals..."
$toolArgs = @('run', '--project', $project, '-c', 'Release', '--', '-ValheimDir', $ValheimDir)
if ($Describe -ne '') { $toolArgs += @('-Describe', $Describe) }
& dotnet @toolArgs
$code = $LASTEXITCODE

if ($Describe -ne '') { exit $code }
if ($code -eq 0) {
    Write-Host "Compatible: every module should start with this VHVR and Valheim." -ForegroundColor Green
} elseif ($code -eq 1) {
    Write-Host "NOT COMPATIBLE: see BROKEN / RISKY-PATCH above. docs\MAINTAINING.md ('When VHVR updates') says what to do." -ForegroundColor Red
} else {
    Write-Host "The check couldn't run (wrong game folder?)." -ForegroundColor Red
}
exit $code
