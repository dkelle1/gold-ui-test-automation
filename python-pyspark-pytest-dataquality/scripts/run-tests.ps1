# Usage: scripts/run-tests.ps1 [-MarkerFilter 'smoke']
# No worker-count argument (unlike the UI siblings): Spark parallelizes across executor cores inside one
# JVM, so the suite runs single-process on purpose - see the README.
param(
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
    --alluredir=allure-results `
    --junitxml=artifacts/junit.xml `
    @markerArgs
