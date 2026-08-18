#!/usr/bin/env bash
# Usage: scripts/run-tests.sh [markerExpression]
#   scripts/run-tests.sh                       -> the whole suite
#   scripts/run-tests.sh smoke                 -> just the fast smoke subset
#   scripts/run-tests.sh 'quality or reconciliation'  -> a marker expression
#
# There is no worker-count argument (unlike the UI siblings): Spark parallelizes each job across the
# executor cores inside one JVM, so the suite runs single-process on purpose - see the README.
set -euo pipefail

MARKER_FILTER="${1:-}"

cd "$(dirname "$0")/.."

MARKER_ARGS=()
if [ -n "$MARKER_FILTER" ]; then
  MARKER_ARGS=(-m "$MARKER_FILTER")
fi

mkdir -p artifacts
uv run pytest \
  --alluredir=allure-results \
  --junitxml=artifacts/junit.xml \
  "${MARKER_ARGS[@]}"
