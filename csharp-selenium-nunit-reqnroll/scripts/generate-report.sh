#!/usr/bin/env bash
# Copies categories.json into the Allure results and opens the report with the Allure CLI.
# Requires the allure CLI: npm i -g allure-commandline, brew install allure, or scoop install allure.
set -euo pipefail

cd "$(dirname "$0")/.."

RESULTS_DIR="SauceDemo.UiTests/bin/Release/net10.0/allure-results"

if [ ! -d "$RESULTS_DIR" ]; then
  echo "No allure-results found at $RESULTS_DIR - run scripts/run-tests.sh first." >&2
  exit 1
fi

cp SauceDemo.UiTests/categories.json "$RESULTS_DIR/categories.json"

if ! command -v allure >/dev/null 2>&1; then
  echo "allure CLI not found. Install it, e.g.:" >&2
  echo "  npm i -g allure-commandline" >&2
  echo "  brew install allure" >&2
  exit 1
fi

allure serve "$RESULTS_DIR"
