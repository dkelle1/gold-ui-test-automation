#!/usr/bin/env bash
# Usage: scripts/run-tests.sh [suite]
#   scripts/run-tests.sh            -> unit + integration (the full run)
#   scripts/run-tests.sh unit       -> xUnit + NUnit unit tests only (no Docker needed)
#   scripts/run-tests.sh integration-> Testcontainers/Respawn integration tests only (Docker required)
#
# The integration suite needs a running Docker daemon: Testcontainers pulls
# mcr.microsoft.com/mssql/server and starts a throwaway SQL Server for the run.
set -euo pipefail

SUITE="${1:-all}"

cd "$(dirname "$0")/.."

dotnet restore Backend.sln
dotnet build Backend.sln -c Release --no-restore

run_unit() {
  dotnet test tests/OrderService.UnitTests.Xunit/OrderService.UnitTests.Xunit.csproj \
    -c Release --no-build \
    --logger "trx;LogFileName=unit-xunit.trx" \
    --results-directory artifacts
  dotnet test tests/OrderService.UnitTests.Nunit/OrderService.UnitTests.Nunit.csproj \
    -c Release --no-build \
    --logger "trx;LogFileName=unit-nunit.trx" \
    --results-directory artifacts
}

run_integration() {
  dotnet test tests/OrderService.IntegrationTests/OrderService.IntegrationTests.csproj \
    -c Release --no-build \
    --logger "trx;LogFileName=integration.trx" \
    --results-directory artifacts \
    --blame-hang-timeout 5min
}

case "$SUITE" in
  unit)        run_unit ;;
  integration) run_integration ;;
  all)         run_unit; run_integration ;;
  *)
    echo "Unknown suite '$SUITE' (expected: unit | integration | all)" >&2
    exit 2
    ;;
esac
