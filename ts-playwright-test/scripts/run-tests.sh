#!/usr/bin/env bash
# Usage: scripts/run-tests.sh [project] [grepFilter]
#   scripts/run-tests.sh                  -> chromium, all tests
#   scripts/run-tests.sh chromium @smoke  -> chromium, @smoke only
#   scripts/run-tests.sh all              -> chromium + firefox + webkit
#
# No worker-count argument, unlike the Cucumber sibling's equivalent: parallelism is owned by
# playwright.config.ts's `workers`, which imports MAX_PARALLEL_WORKERS directly, so there is no way to
# ask for more workers than the user pool has accounts.
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
