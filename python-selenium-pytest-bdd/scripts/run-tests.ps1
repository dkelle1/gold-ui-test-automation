# Usage: scripts/run-tests.ps1 [-Workers 3] [-MarkerFilter smoke]
param(
    [int]$Workers = 3,
    [string]$MarkerFilter = ""
)

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$markerArgs = @()
if ($MarkerFilter) {
    $markerArgs = @("-m", $MarkerFilter)
}

New-Item -ItemType Directory -Force -Path artifacts | Out-Null
uv run pytest `
    -n $Workers `
    --alluredir=allure-results `
    --junitxml=artifacts/junit.xml `
    @markerArgs
