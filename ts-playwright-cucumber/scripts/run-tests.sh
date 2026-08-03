#!/usr/bin/env bash
# Usage: scripts/run-tests.sh [workers] [tagFilter]
#   scripts/run-tests.sh              -> 3 parallel workers, all scenarios
#   scripts/run-tests.sh 3 @smoke     -> 3 parallel workers, @smoke only
set -euo pipefail

WORKERS="${1:-3}"
TAG_FILTER="${2:-}"

cd "$(dirname "$0")/.."

TAG_ARGS=()
if [ -n "$TAG_FILTER" ]; then
  TAG_ARGS=(--tags "$TAG_FILTER")
fi

mkdir -p artifacts
# --import tsx (not plain `npx cucumber-js`): see cucumber.js's doc comment - Cucumber's own
# `--parallel` worker threads don't inherit tsx's loader hooks (only Node's native TypeScript stripping
# ends up active there), so `--import tsx` here is a version-independent safety net for the coordinator
# thread rather than something this specific codebase still strictly needs.
node --import tsx node_modules/@cucumber/cucumber/bin/cucumber-js \
  --parallel "$WORKERS" \
  --format json:artifacts/cucumber-report.json \
  "${TAG_ARGS[@]}"
