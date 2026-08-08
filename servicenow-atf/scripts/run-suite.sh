#!/usr/bin/env bash
# Live ATF suite run against the instance in SN_INSTANCE_URL (see .env.example for the variables).
# Usage: scripts/run-suite.sh "[CSM] Smoke" [extra atf-run flags...]
set -euo pipefail
SUITE="${1:-[CSM] Smoke}"
if [ "$#" -gt 0 ]; then shift; fi
cd "$(dirname "$0")/../runner"

uv sync --locked
uv run atf-run --suite "$SUITE" --junit-out ../artifacts/atf-junit.xml "$@"
