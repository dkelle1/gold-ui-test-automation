# Usage: scripts/run-tests.ps1 [-Workers 3] [-TagFilter @smoke]
param(
    [int]$Workers = 3,
    [string]$TagFilter = ""
)

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$tagArgs = @()
if ($TagFilter) {
    $tagArgs = @("--tags", $TagFilter)
}

New-Item -ItemType Directory -Force -Path artifacts | Out-Null
# --import tsx (not plain `npx cucumber-js`): see cucumber.js's doc comment - this is a version-
# independent safety net for the coordinator thread, not something this codebase's worker threads
# still strictly need (they run on Node's native TypeScript stripping regardless - see that file).
node --import tsx node_modules/@cucumber/cucumber/bin/cucumber-js `
    --parallel $Workers `
    --format json:artifacts/cucumber-report.json `
    @tagArgs
