from __future__ import annotations

import json
import subprocess
from pathlib import Path


def _run(*args: str, cwd: Path | None = None) -> subprocess.CompletedProcess[str]:
    return subprocess.run(args, cwd=cwd, capture_output=True, text=True, check=False)


def test_validate_commit_message_accepts_conventional_header() -> None:
    result = _run(
        "python3",
        "scripts/validate_commit_message.py",
        "feat(processor): add anomaly summary",
    )
    assert result.returncode == 0, result.stderr


def test_validate_commit_message_rejects_invalid_header() -> None:
    result = _run("python3", "scripts/validate_commit_message.py", "bad commit message")
    assert result.returncode == 1
    assert "invalid header format" in result.stderr


def test_check_processed_outputs_reports_missing_model(tmp_path: Path) -> None:
    output_dir = tmp_path / "outputs"
    output_dir.mkdir(parents=True)
    metadata = {
        "hhh_count": 1,
        "hhh_inspected": 1,
        "model_count": 1,
        "export_failed_count": 0,
        "images": [
            {
                "hhh_entry": "assets/sample.hhh",
                "model": "missing.glb",
                "mesh_bounds": [[0, 0, 0], [1, 1, 1]],
                "mesh": {"vertex_count": 10, "triangle_count": 5},
            }
        ],
    }
    metadata_path = output_dir / "metadata.json"
    metadata_path.write_text(json.dumps(metadata), encoding="utf-8")

    result = _run(
        "python3",
        "scripts/check_processed_outputs.py",
        "--metadata",
        str(metadata_path),
    )

    assert result.returncode == 0
    assert "referenced model missing" in result.stdout


def test_check_processed_outputs_strict_fails_on_warning(tmp_path: Path) -> None:
    output_dir = tmp_path / "outputs"
    output_dir.mkdir(parents=True)
    metadata = {
        "hhh_count": 2,
        "hhh_inspected": 1,
        "model_count": 0,
        "export_failed_count": 0,
        "images": [],
    }
    metadata_path = output_dir / "metadata.json"
    metadata_path.write_text(json.dumps(metadata), encoding="utf-8")

    result = _run(
        "python3",
        "scripts/check_processed_outputs.py",
        "--metadata",
        str(metadata_path),
        "--strict",
    )

    assert result.returncode == 1
    assert "count mismatch" in result.stdout
