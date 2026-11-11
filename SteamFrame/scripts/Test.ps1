# Runs the unit tests for the pure rules, then checks those rules still compile the way VHVR would compile them
# (.NET Framework 4.6, C# 7.3). Needs no game files.
# Usage:  .\scripts\Test.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Write-Host "Running the unit tests..."
& dotnet test (Join-Path $root 'tests\SteamFrame.Tests') -c Release --nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "TESTS FAILED. Each failed test is listed above with what it expected." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Checking the rules still compile for VHVR (.NET Framework 4.6, C# 7.3)..."
& dotnet build (Join-Path $root 'tests\PoliciesCompat') -c Release --nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "COMPAT BUILD FAILED: something in src\Policies uses newer C# than VHVR's compiler accepts." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "All tests passed and the rules are VHVR-compatible." -ForegroundColor Green
exit 0
