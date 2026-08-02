#!/usr/bin/env bash
# Usage: scripts/run-tests.sh [workers] [tagFilter]
#   scripts/run-tests.sh              -> 3 parallel workers, all scenarios
#   scripts/run-tests.sh 3 smoke      -> 3 parallel workers, @smoke only
set -euo pipefail

WORKERS="${1:-3}"
TAG_FILTER="${2:-}"

cd "$(dirname "$0")/.."

FILTER_ARGS=()
if [ -n "$TAG_FILTER" ]; then
  FILTER_ARGS=(--filter "Category=$TAG_FILTER")
fi

dotnet test UiTests.sln \
  -c Release \
  --logger "trx;LogFileName=test-results.trx" \
  --logger "nunit;LogFilePath=test-results.xml" \
  --results-directory artifacts \
  "${FILTER_ARGS[@]}" \
  -- NUnit.NumberOfTestWorkers="$WORKERS"
