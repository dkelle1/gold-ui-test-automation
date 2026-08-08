# Usage: scripts/run-tests.ps1 [-Workers 3] [-TagFilter '@smoke']
param(
    [int]$Workers = 3,
    [string]$TagFilter = ""
)

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$extraArgs = @()
if ($TagFilter) {
    $extraArgs = @("-Dcucumber.filter.tags=$TagFilter")
}

mvn -B --no-transfer-progress test `
    "-Dcucumber.execution.parallel.config.fixed.parallelism=$Workers" `
    @extraArgs
