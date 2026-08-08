"""validate_specs: the shipped specs must pass, and each guard must actually catch its drift."""

from __future__ import annotations

from pathlib import Path
from typing import Any

import yaml

from atf_runner.validate_specs import main, validate

REPO_ATF = Path(__file__).resolve().parents[2] / "atf"


def test_shipped_specs_are_valid() -> None:
    """The real atf/ directory is itself a fixture: a PR that breaks a spec fails here too."""
    assert main(["--atf-dir", str(REPO_ATF)]) == 0


def _base_test_spec(key: str = "CSM-SMK-001", suite: str = "[CSM] Smoke") -> dict[str, Any]:
    return {
        "key": key,
        "name": f"[CSM][Smoke] Sample {key}",
        "suite": suite,
        "application": "Customer Service Management",
        "kind": "server",
        "description": "sample",
        "impersonate": "atf.csm.agent",
        "steps": [
            {"order": 1, "type": "Impersonate", "inputs": {"user": "atf.csm.agent"}},
            {"order": 2, "type": "Record Insert", "inputs": {"table": "sn_customerservice_case"}},
        ],
    }


def _write_specs(
    root: Path,
    *,
    tests: dict[str, dict[str, Any]] | None = None,
    suite_tests: list[str] | None = None,
    suite_extra: dict[str, Any] | None = None,
) -> Path:
    atf_dir = root / "atf"
    (atf_dir / "suites").mkdir(parents=True)
    (atf_dir / "tests" / "csm").mkdir(parents=True)
    (atf_dir / "personas.yaml").write_text(
        yaml.safe_dump(
            {"personas": {"atf.csm.agent": {"roles": ["sn_customerservice_agent"], "purpose": "agent"}}}
        ),
        encoding="utf-8",
    )
    suite: dict[str, Any] = {
        "name": "[CSM] Smoke",
        "description": "sample suite",
        "tests": suite_tests if suite_tests is not None else ["CSM-SMK-001"],
    }
    suite.update(suite_extra or {})
    (atf_dir / "suites" / "csm-smoke.suite.yaml").write_text(yaml.safe_dump(suite), encoding="utf-8")
    if tests is None:
        tests = {"CSM-SMK-001-sample.yaml": _base_test_spec()}
    for filename, spec in tests.items():
        (atf_dir / "tests" / "csm" / filename).write_text(yaml.safe_dump(spec), encoding="utf-8")
    return atf_dir


def _messages(atf_dir: Path) -> str:
    return " | ".join(str(error) for error in validate(atf_dir))


def test_minimal_valid_fixture_passes(tmp_path: Path) -> None:
    assert validate(_write_specs(tmp_path)) == []


def test_duplicate_key_is_caught(tmp_path: Path) -> None:
    duplicate = _base_test_spec()
    duplicate["name"] = "[CSM][Smoke] Different name, same key"
    atf_dir = _write_specs(
        tmp_path,
        tests={"CSM-SMK-001-sample.yaml": _base_test_spec(), "CSM-SMK-001-duplicate.yaml": duplicate},
    )
    assert "duplicate key" in _messages(atf_dir)


def test_unknown_suite_reference_is_caught(tmp_path: Path) -> None:
    orphan = _base_test_spec(suite="[CSM] Nope")
    atf_dir = _write_specs(tmp_path, tests={"CSM-SMK-001-sample.yaml": orphan})
    assert "has no spec file" in _messages(atf_dir)


def test_unlisted_test_breaks_membership_symmetry(tmp_path: Path) -> None:
    extra = _base_test_spec(key="CSM-SMK-002")
    atf_dir = _write_specs(
        tmp_path,
        tests={"CSM-SMK-001-sample.yaml": _base_test_spec(), "CSM-SMK-002-extra.yaml": extra},
        suite_tests=["CSM-SMK-001"],  # CSM-SMK-002 declares the suite but is not listed
    )
    assert "not listed" in _messages(atf_dir)


def test_unknown_persona_is_caught(tmp_path: Path) -> None:
    spec = _base_test_spec()
    spec["impersonate"] = "atf.nobody"
    spec["steps"][0]["inputs"]["user"] = "atf.nobody"
    atf_dir = _write_specs(tmp_path, tests={"CSM-SMK-001-sample.yaml": spec})
    assert "not declared in personas.yaml" in _messages(atf_dir)


def test_non_contiguous_step_orders_are_caught(tmp_path: Path) -> None:
    spec = _base_test_spec()
    spec["steps"][1]["order"] = 3
    atf_dir = _write_specs(tmp_path, tests={"CSM-SMK-001-sample.yaml": spec})
    assert "contiguous" in _messages(atf_dir)


def test_filename_must_start_with_key(tmp_path: Path) -> None:
    atf_dir = _write_specs(tmp_path, tests={"wrong-name.yaml": _base_test_spec()})
    assert "filename must start with the key" in _messages(atf_dir)


def test_suite_cannot_nest_itself(tmp_path: Path) -> None:
    atf_dir = _write_specs(tmp_path, suite_extra={"child_suites": ["[CSM] Smoke"]})
    assert "cannot nest itself" in _messages(atf_dir)


def test_empty_suite_is_an_empty_gate(tmp_path: Path) -> None:
    atf_dir = _write_specs(
        tmp_path,
        tests={"CSM-SMK-001-sample.yaml": _base_test_spec(suite="[CSM] Other")},
        suite_tests=[],
    )
    messages = _messages(atf_dir)
    assert "empty gate" in messages
