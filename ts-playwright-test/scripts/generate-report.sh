#!/usr/bin/env bash
# Opens the Allure report with the Allure CLI.
# Requires the allure CLI: npm i -g allure-commandline, brew install allure, or scoop install allure.
#
# For the Playwright HTML report - which needs no external CLI at all - use `npm run report` instead.
set -euo pipefail

cd "$(dirname "$0")/.."

RESULTS_DIR="allure-results"

if [ ! -d "$RESULTS_DIR" ]; then
  echo "No allure-results found at $RESULTS_DIR - run scripts/run-tests.sh first." >&2
  exit 1
fi

# Normally already present: playwright.config.ts's globalSetup copies categories.json into the results
# directory at the start of every run, so CI uploads carry it too. Repeated here so an older results
# directory still gets the defect classification.
cp categories.json "$RESULTS_DIR/categories.json"

if ! command -v allure >/dev/null 2>&1; then
  echo "allure CLI not found. Install it, e.g.:" >&2
  echo "  npm i -g allure-commandline" >&2
  echo "  brew install allure" >&2
  exit 1
fi

allure serve "$RESULTS_DIR"
