# Makes the zip players download: dist\VhvrSteamFrame-<version>.zip
# Contents: BepInEx\plugins\VhvrSteamFrame.dll, README.md, CHANGELOG.md, LICENSE (VHVR's GPL-3.0).
# Run .\scripts\Build.ps1 first. dist\ is ignored by Git.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$repo = Split-Path -Parent $root
$built = Join-Path $root 'src\VhvrSteamFrame\bin\Release\netstandard2.1\VhvrSteamFrame.dll'

if (-not (Test-Path $built)) {
    Write-Host "PACKAGE FAILED: no built plugin at $built. Run .\scripts\Build.ps1 first." -ForegroundColor Red
    exit 1
}

$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($built).ProductVersion
if ($version -match '^([0-9]+\.[0-9]+\.[0-9]+)') { $version = $Matches[1] }

$dist = Join-Path $root 'dist'
$stage = Join-Path $dist "stage-$version"
$zip = Join-Path $dist "VhvrSteamFrame-$version.zip"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'BepInEx\plugins') | Out-Null

Write-Host "Packaging Steam Frame for VHVR $version ..."
Copy-Item $built (Join-Path $stage 'BepInEx\plugins')
Copy-Item (Join-Path $root 'README.md') $stage
Copy-Item (Join-Path $root 'docs\CHANGELOG.md') $stage
Copy-Item (Join-Path $repo 'LICENSE') $stage

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Remove-Item $stage -Recurse -Force

if (-not (Test-Path $zip)) {
    Write-Host "PACKAGE FAILED: the zip wasn't created." -ForegroundColor Red
    exit 1
}
Write-Host "Package OK: $zip" -ForegroundColor Green
exit 0
