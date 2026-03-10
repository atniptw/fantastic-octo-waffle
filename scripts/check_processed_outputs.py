#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any


@dataclass
class Finding:
    severity: str
    message: str
    item: str | None = None


def _is_numeric_triplet(value: Any) -> bool:
    return isinstance(value, list) and len(value) == 3 and all(isinstance(x, (int, float)) for x in value)


def _validate_bounds(bounds: Any) -> bool:
    return isinstance(bounds, list) and len(bounds) == 2 and _is_numeric_triplet(bounds[0]) and _is_numeric_triplet(bounds[1])


def _load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def analyze(metadata_path: Path) -> list[Finding]:
    data = _load_json(metadata_path)
    findings: list[Finding] = []

    hhh_count = int(data.get("hhh_count", 0))
    hhh_inspected = int(data.get("hhh_inspected", 0))
    model_count = int(data.get("model_count", 0))
    export_failed = int(data.get("export_failed_count", 0))

    if hhh_count != hhh_inspected:
        findings.append(
            Finding(
                severity="warning",
                message=f"count mismatch: hhh_count={hhh_count} hhh_inspected={hhh_inspected}",
            )
        )

    if model_count + export_failed != hhh_inspected:
        findings.append(
            Finding(
                severity="warning",
                message=(
                    "export accounting mismatch: "
                    f"model_count + export_failed_count = {model_count + export_failed} != {hhh_inspected}"
                ),
            )
        )

    images = data.get("images", [])
    metadata_dir = metadata_path.parent
    for item in images:
        entry = str(item.get("hhh_entry", "<unknown>"))
        model_name = item.get("model")
        if model_name:
            model_path = metadata_dir / str(model_name)
            if not model_path.exists():
                findings.append(
                    Finding("warning", f"referenced model missing: {model_name}", item=entry)
                )
            elif model_path.stat().st_size == 0:
                findings.append(
                    Finding("warning", f"referenced model is empty: {model_name}", item=entry)
                )

        bounds = item.get("mesh_bounds")
        if bounds is not None and not _validate_bounds(bounds):
            findings.append(Finding("warning", "mesh_bounds has invalid structure", item=entry))

        mesh = item.get("mesh") or {}
        vertices = int(mesh.get("vertex_count", 0)) if isinstance(mesh, dict) else 0
        triangles = int(mesh.get("triangle_count", 0)) if isinstance(mesh, dict) else 0
        if model_name and vertices <= 0:
            findings.append(
                Finding("warning", "model exported but mesh vertex_count <= 0", item=entry)
            )
        if model_name and triangles <= 0:
            findings.append(
                Finding("warning", "model exported but mesh triangle_count <= 0", item=entry)
            )

    return findings


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Detect suspicious processed outputs")
    parser.add_argument("--metadata", default="data/outputs/metadata.json")
    parser.add_argument("--strict", action="store_true", help="Exit non-zero when warnings exist")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    metadata_path = Path(args.metadata)
    if not metadata_path.exists():
        print(f"error: metadata not found at {metadata_path}")
        return 2

    findings = analyze(metadata_path)
    if not findings:
        print("no suspicious output signals found")
        return 0

    print(f"found {len(findings)} suspicious signal(s):")
    for finding in findings:
        prefix = f"[{finding.severity}]"
        if finding.item:
            print(f"{prefix} {finding.item}: {finding.message}")
        else:
            print(f"{prefix} {finding.message}")

    if args.strict:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
