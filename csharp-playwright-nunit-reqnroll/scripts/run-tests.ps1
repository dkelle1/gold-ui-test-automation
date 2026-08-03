# Usage: scripts/run-tests.ps1 [-Workers 3] [-TagFilter smoke]
param(
    [int]$Workers = 3,
    [string]$TagFilter = ""
)

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$filterArgs = @()
if ($TagFilter) {
    $filterArgs = @("--filter", "Category=$TagFilter")
}

dotnet test UiTests.sln `
    -c Release `
    --logger "trx;LogFileName=test-results.trx" `
    --logger "nunit;LogFilePath=test-results.xml" `
    --results-directory artifacts `
    @filterArgs `
    -- NUnit.NumberOfTestWorkers=$Workers
