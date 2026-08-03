#!/usr/bin/env bash
# Usage: scripts/run-tests.sh [workers] [markerFilter]
#   scripts/run-tests.sh              -> 3 parallel workers, all scenarios
#   scripts/run-tests.sh 3 smoke      -> 3 parallel workers, the "smoke" marker only
set -euo pipefail

WORKERS="${1:-3}"
MARKER_FILTER="${2:-}"

cd "$(dirname "$0")/.."

MARKER_ARGS=()
if [ -n "$MARKER_FILTER" ]; then
  MARKER_ARGS=(-m "$MARKER_FILTER")
fi

mkdir -p artifacts
uv run pytest \
  -n "$WORKERS" \
  --alluredir=allure-results \
  --junitxml=artifacts/junit.xml \
  "${MARKER_ARGS[@]}"
