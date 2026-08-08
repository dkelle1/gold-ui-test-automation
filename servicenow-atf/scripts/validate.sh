#!/usr/bin/env bash
# The no-instance gate, exactly what PR CI runs: lint, format, types, unit tests, spec validation.
set -euo pipefail
cd "$(dirname "$0")/../runner"

uv sync --locked
uv run ruff check .
uv run ruff format --check .
uv run mypy .
uv run pytest
uv run atf-validate --atf-dir ../atf
