#!/usr/bin/env bash
# Usage: scripts/run-tests.sh [workers] [tagExpression]
#   scripts/run-tests.sh                    -> 3 scenarios in parallel, all of them
#   scripts/run-tests.sh 3 @smoke           -> 3 in parallel, @smoke only
#   scripts/run-tests.sh 1 'not @known-issue'  -> serial, skipping the known-issue scenario
set -euo pipefail

WORKERS="${1:-3}"
TAG_FILTER="${2:-}"

cd "$(dirname "$0")/.."

EXTRA_ARGS=()
if [ -n "$TAG_FILTER" ]; then
  EXTRA_ARGS=("-Dcucumber.filter.tags=$TAG_FILTER")
fi

mvn -B --no-transfer-progress test \
  "-Dcucumber.execution.parallel.config.fixed.parallelism=$WORKERS" \
  "${EXTRA_ARGS[@]}"
