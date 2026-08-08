# Live ATF suite run against the instance in SN_INSTANCE_URL (see .env.example for the variables).
# Usage: scripts\run-suite.ps1 "[CSM] Smoke"
param([string]$Suite = "[CSM] Smoke")
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot ".." "runner")

uv sync --locked
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
uv run atf-run --suite $Suite --junit-out ../artifacts/atf-junit.xml
exit $LASTEXITCODE
