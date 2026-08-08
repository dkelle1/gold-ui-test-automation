# The no-instance gate, exactly what PR CI runs: lint, format, types, unit tests, spec validation.
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot ".." "runner")

foreach ($step in @(
        "uv sync --locked",
        "uv run ruff check .",
        "uv run ruff format --check .",
        "uv run mypy .",
        "uv run pytest",
        "uv run atf-validate --atf-dir ../atf"
    )) {
    Write-Host ">> $step"
    Invoke-Expression $step
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
