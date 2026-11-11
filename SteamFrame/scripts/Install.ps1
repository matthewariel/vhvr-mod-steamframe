# Copies the built plugin into Valheim. Run .\scripts\Build.ps1 first, and close Valheim.
# The previous DLL is backed up to %LOCALAPPDATA%\VhvrSteamFrame\backups\<timestamp>\ so you can always go back.
# Usage:  .\scripts\Install.ps1
#         .\scripts\Install.ps1 -ValheimDir "D:\Games\Valheim"
param(
    [string]$ValheimDir = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$built = Join-Path $root 'src\VhvrSteamFrame\bin\Release\netstandard2.1\VhvrSteamFrame.dll'
$pluginDir = Join-Path $ValheimDir 'BepInEx\plugins'
$target = Join-Path $pluginDir 'VhvrSteamFrame.dll'

function Fail([string]$message) {
    Write-Host "INSTALL FAILED: $message" -ForegroundColor Red
    exit 1
}

if (Get-Process -Name 'valheim' -ErrorAction SilentlyContinue) {
    Fail "Valheim is running. Close the game, then run this again."
}
if (-not (Test-Path $built)) {
    Fail "No built plugin at $built. Run .\scripts\Build.ps1 first."
}
if (-not (Test-Path $pluginDir)) {
    Fail "Can't find $pluginDir. Is BepInEx installed? Pass -ValheimDir if the game is somewhere else."
}
if (-not (Test-Path (Join-Path $pluginDir 'ValheimVRMod.dll'))) {
    Write-Host "Warning: ValheimVRMod.dll isn't in $pluginDir. This plugin needs VHVR installed." -ForegroundColor Yellow
}

if (Test-Path $target) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backupDir = Join-Path $env:LOCALAPPDATA "VhvrSteamFrame\backups\$stamp"
    New-Item -ItemType Directory -Force $backupDir | Out-Null
    Copy-Item $target $backupDir
    $oldVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($target).ProductVersion
    Write-Host "Backed up the installed plugin ($oldVersion) to $backupDir"
} else {
    Write-Host "No plugin installed yet; nothing to back up."
}

Write-Host "Copying the new plugin to $target ..."
Copy-Item $built $target -Force

$want = (Get-FileHash $built -Algorithm SHA256).Hash
$have = (Get-FileHash $target -Algorithm SHA256).Hash
if ($want -ne $have) {
    Fail "The copied file doesn't match the built one. Check the folder's permissions and try again."
}

$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($target).ProductVersion
Write-Host "Installed Steam Frame for VHVR $version" -ForegroundColor Green
Write-Host "Start Valheim, then check BepInEx\LogOutput.log for the 'modules active' line."
exit 0
