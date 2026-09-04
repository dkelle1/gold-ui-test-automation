#!/usr/bin/env bash
# Usage: scripts/run-tests.sh [project] [grepFilter]
#   scripts/run-tests.sh                  -> chromium, all examples
#   scripts/run-tests.sh chromium Frames  -> chromium, matching titles only
#   scripts/run-tests.sh all              -> chromium + firefox + webkit
#
# No server to start first: playwright.config.ts's webServer block launches fixtures-app and shuts it
# down again around the run.
set -euo pipefail

PROJECT="${1:-chromium}"
GREP_FILTER="${2:-}"

cd "$(dirname "$0")/.."

ARGS=()
if [ "$PROJECT" != "all" ]; then
  ARGS+=(--project "$PROJECT")
fi
if [ -n "$GREP_FILTER" ]; then
  ARGS+=(--grep "$GREP_FILTER")
fi

npx playwright test "${ARGS[@]}"
