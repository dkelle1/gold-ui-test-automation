"""atf-validate: consistency gate for the specs-as-code under atf/.

The specs are the reviewable source of truth for tests that physically live in a ServiceNow
instance, so the *only* thing protecting them from silent rot is this gate: schema shape, stable
keys, suite membership symmetry, contiguous step ordering, and personas that actually exist.
Runs on every PR; one of the runner's unit tests also points it at the shipped specs.
"""

from __future__ import annotations

import argparse
import re
import sys
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import yaml

KEY_PATTERN = re.compile(r"^[A-Z]+-[A-Z]+-\d{3}$")
TEST_KINDS = {"form", "server", "portal", "rest"}
REQUIRED_TEST_FIELDS = ("key", "name", "suite", "application", "kind", "description", "steps")
REQUIRED_SUITE_FIELDS = ("name", "description")


@dataclass(frozen=True)
class SpecError:
    file: Path
    message: str

    def __str__(self) -> str:
        return f"{self.file.name}: {self.message}"


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="atf-validate", description=__doc__)
    parser.add_argument("--atf-dir", default="atf", help="Path to the atf/ specs directory")
    args = parser.parse_args(argv)
    atf_dir = Path(args.atf_dir)
    if not atf_dir.is_dir():
        print(f"Spec directory not found: {atf_dir}", file=sys.stderr)
        return 1

    errors = validate(atf_dir)
    if errors:
        print(f"Spec validation FAILED with {len(errors)} error(s):", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1
    _print_inventory(atf_dir)
    return 0


def validate(atf_dir: Path) -> list[SpecError]:
    errors: list[SpecError] = []
    personas = _load_personas(atf_dir / "personas.yaml", errors)
    suites = _load_suites(atf_dir / "suites", errors)
    tests = _load_tests(atf_dir / "tests", personas, errors)
    _check_cross_references(suites, tests, errors)
    return errors


def _load_personas(path: Path, errors: list[SpecError]) -> set[str]:
    data = _load_yaml(path, errors)
    if data is None:
        return set()
    personas = data.get("personas")
    if not isinstance(personas, Mapping) or not personas:
        errors.append(SpecError(path, "expected a non-empty `personas:` mapping"))
        return set()
    for name, spec in personas.items():
        if not isinstance(spec, Mapping):
            errors.append(SpecError(path, f"persona {name!r} must be a mapping"))
            continue
        roles = spec.get("roles")
        if not isinstance(roles, list) or not roles or not all(isinstance(r, str) and r for r in roles):
            errors.append(SpecError(path, f"persona {name!r} needs a non-empty list of role names"))
        if not isinstance(spec.get("purpose"), str) or not str(spec.get("purpose")).strip():
            errors.append(SpecError(path, f"persona {name!r} needs a `purpose:` - personas are ACL coverage"))
    return {str(name) for name in personas}


def _load_suites(suites_dir: Path, errors: list[SpecError]) -> dict[str, tuple[Path, dict[str, Any]]]:
    suites: dict[str, tuple[Path, dict[str, Any]]] = {}
    files = sorted(suites_dir.glob("*.suite.yaml")) if suites_dir.is_dir() else []
    if not files:
        errors.append(SpecError(suites_dir, "no *.suite.yaml files found"))
        return suites
    for path in files:
        data = _load_yaml(path, errors)
        if data is None:
            continue
        for field in REQUIRED_SUITE_FIELDS:
            if not isinstance(data.get(field), str) or not str(data.get(field)).strip():
                errors.append(SpecError(path, f"missing/empty required field `{field}`"))
        tests = data.get("tests")
        if not isinstance(tests, list) or not all(isinstance(t, str) for t in tests):
            errors.append(SpecError(path, "`tests` must be a list of test keys (may be empty)"))
            data["tests"] = []
        child_suites = data.get("child_suites", [])
        if not isinstance(child_suites, list) or not all(isinstance(s, str) for s in child_suites):
            errors.append(SpecError(path, "`child_suites` must be a list of suite names"))
            data["child_suites"] = []
        if not data.get("tests") and not data.get("child_suites"):
            errors.append(SpecError(path, "a suite needs `tests` and/or `child_suites` - an empty gate"))
        name = str(data.get("name", ""))
        if name in suites:
            errors.append(SpecError(path, f"duplicate suite name {name!r} (also in {suites[name][0].name})"))
        elif name:
            suites[name] = (path, data)
    return suites


def _load_tests(
    tests_dir: Path, personas: set[str], errors: list[SpecError]
) -> dict[str, tuple[Path, dict[str, Any]]]:
    tests: dict[str, tuple[Path, dict[str, Any]]] = {}
    names_seen: dict[str, Path] = {}
    files = sorted(tests_dir.rglob("*.yaml")) if tests_dir.is_dir() else []
    if not files:
        errors.append(SpecError(tests_dir, "no test spec *.yaml files found"))
        return tests
    for path in files:
        data = _load_yaml(path, errors)
        if data is None:
            continue
        missing = [f for f in REQUIRED_TEST_FIELDS if f not in data]
        if missing:
            errors.append(SpecError(path, f"missing required field(s): {', '.join(missing)}"))
            continue
        key = str(data.get("key", ""))
        if not KEY_PATTERN.fullmatch(key):
            errors.append(SpecError(path, f"key {key!r} does not match <APP>-<TAG>-<NNN>"))
        if not path.name.startswith(f"{key}-"):
            errors.append(SpecError(path, f"filename must start with the key ({key}-...)"))
        if key in tests:
            errors.append(SpecError(path, f"duplicate key {key!r} (also in {tests[key][0].name})"))
        name = str(data.get("name", ""))
        if name in names_seen:
            errors.append(SpecError(path, f"duplicate name {name!r} (also in {names_seen[name].name})"))
        names_seen[name] = path
        if data.get("kind") not in TEST_KINDS:
            errors.append(SpecError(path, f"kind {data.get('kind')!r} not one of {sorted(TEST_KINDS)}"))
        impersonate = data.get("impersonate")
        if impersonate is not None and impersonate not in personas:
            errors.append(SpecError(path, f"impersonate {impersonate!r} is not declared in personas.yaml"))
        _check_steps(path, data.get("steps"), personas, errors)
        if key and key not in tests:
            tests[key] = (path, data)
    return tests


def _check_steps(path: Path, steps: object, personas: set[str], errors: list[SpecError]) -> None:
    if not isinstance(steps, list) or not steps:
        errors.append(SpecError(path, "`steps` must be a non-empty list"))
        return
    orders: list[int] = []
    for index, step in enumerate(steps, start=1):
        if not isinstance(step, Mapping):
            errors.append(SpecError(path, f"step #{index} must be a mapping"))
            continue
        order = step.get("order")
        if not isinstance(order, int):
            errors.append(SpecError(path, f"step #{index} needs an integer `order`"))
        else:
            orders.append(order)
        if not isinstance(step.get("type"), str) or not str(step.get("type")).strip():
            errors.append(SpecError(path, f"step #{index} needs a non-empty `type` (ATF step config name)"))
        if step.get("type") == "Impersonate":
            inputs = step.get("inputs")
            user = inputs.get("user") if isinstance(inputs, Mapping) else None
            if user not in personas:
                errors.append(
                    SpecError(path, f"Impersonate step #{index} user {user!r} is not in personas.yaml")
                )
    if orders and sorted(orders) != list(range(1, len(orders) + 1)):
        errors.append(
            SpecError(path, f"step orders must be contiguous 1..{len(orders)}, got {sorted(orders)}")
        )


def _check_cross_references(
    suites: Mapping[str, tuple[Path, dict[str, Any]]],
    tests: Mapping[str, tuple[Path, dict[str, Any]]],
    errors: list[SpecError],
) -> None:
    for suite_name, (suite_path, suite) in suites.items():
        listed = [str(k) for k in suite.get("tests", [])]
        for child in suite.get("child_suites", []):
            if child == suite_name:
                errors.append(SpecError(suite_path, "a suite cannot nest itself"))
            elif child not in suites:
                errors.append(SpecError(suite_path, f"child suite {child!r} has no spec file"))
        for key in listed:
            if key not in tests:
                errors.append(SpecError(suite_path, f"listed test {key!r} has no spec file"))
        # Membership symmetry: the suite's list and the tests' own `suite:` fields must agree, so
        # a test can't silently drop out of the gate that claims to include it.
        declared = {key for key, (_, test) in tests.items() if test.get("suite") == suite_name}
        missing = declared - set(listed)
        if missing:
            errors.append(
                SpecError(suite_path, f"tests declare this suite but are not listed: {sorted(missing)}")
            )
    for test_path, test in tests.values():
        if str(test.get("suite", "")) not in suites:
            errors.append(SpecError(test_path, f"suite {test.get('suite')!r} has no spec file"))


def _load_yaml(path: Path, errors: list[SpecError]) -> dict[str, Any] | None:
    if not path.is_file():
        errors.append(SpecError(path, "file not found"))
        return None
    try:
        data: Any = yaml.safe_load(path.read_text(encoding="utf-8"))
    except yaml.YAMLError as exc:
        errors.append(SpecError(path, f"invalid YAML: {exc}"))
        return None
    if not isinstance(data, dict):
        errors.append(SpecError(path, "top level must be a mapping"))
        return None
    return data


def _print_inventory(atf_dir: Path) -> None:
    suites = {
        str(data.get("name")): data
        for path in sorted((atf_dir / "suites").glob("*.suite.yaml"))
        if isinstance((data := yaml.safe_load(path.read_text(encoding="utf-8"))), dict)
    }
    total = 0
    print(f"Specs OK under {atf_dir}:")
    for name, data in suites.items():
        tests = data.get("tests") or []
        children = data.get("child_suites") or []
        total += len(tests)
        detail = f"{len(tests)} test(s)" + (f", nests {len(children)} suite(s)" if children else "")
        print(f"  - {name}: {detail}")
    print(f"  {total} test spec(s) across {len(suites)} suite(s).")


if __name__ == "__main__":
    sys.exit(main())
