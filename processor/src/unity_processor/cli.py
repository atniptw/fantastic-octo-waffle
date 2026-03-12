from __future__ import annotations

import argparse
import json
from pathlib import Path

from .metadata import build_metadata
from .parse import parse_unity_input
from .render import render_preview_images


def _source_key(source: str | None) -> str | None:
    if not source:
        return None
    return Path(source).name


def _load_mod_metadata(metadata_path: Path) -> dict[str, dict]:
    if not metadata_path.exists():
        return {}

    try:
        existing = json.loads(metadata_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}

    if not isinstance(existing, dict):
        return {}

    loaded: dict[str, dict] = {}

    mods = existing.get("mods")
    if isinstance(mods, list):
        for item in mods:
            if not isinstance(item, dict):
                continue
            source_key = _source_key(item.get("source"))
            if source_key:
                loaded[source_key] = item

    if not loaded and isinstance(existing.get("images"), list):
        source_key = _source_key(existing.get("source"))
        if source_key:
            loaded[source_key] = existing

    return loaded


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="unity-processor",
        description="Unity model pipeline CLI.",
    )
    parser.add_argument(
        "mod_zip",
        nargs="?",
        default="data/incoming/mods/sample.zip",
        help="Path to mod zip containing .hhh entries",
    )
    parser.add_argument(
        "--dependency-unitypackage",
        default=None,
        help="Optional path to a dependency .unitypackage file",
    )
    parser.add_argument("--output", default="data/outputs", help="Output directory")
    parser.add_argument("--metadata", default="metadata.json", help="Metadata file name")
    parser.add_argument(
        "--overwrite",
        action="store_true",
        help="Reserved for future behavior; currently informational only",
    )
    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    input_path = Path(args.mod_zip)
    dependency_path = Path(args.dependency_unitypackage) if args.dependency_unitypackage else None
    output_dir = Path(args.output)
    parsed = parse_unity_input(input_path, dependency_path)
    images = render_preview_images(parsed, output_dir)
    metadata = build_metadata(parsed, images)

    metadata_path = output_dir / args.metadata
    merged = _load_mod_metadata(metadata_path)
    source_key = _source_key(metadata.get("source"))
    if source_key:
        merged[source_key] = metadata

    combined_metadata = dict(metadata)
    combined_metadata["mods"] = list(merged.values())
    combined_metadata["mod_count"] = len(merged)

    metadata_path.write_text(json.dumps(combined_metadata, indent=2), encoding="utf-8")
    print(f"wrote {metadata_path}")
    if parsed.get("hhh_entries"):
        return 0

    print("no .hhh assets were discovered", flush=True)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
