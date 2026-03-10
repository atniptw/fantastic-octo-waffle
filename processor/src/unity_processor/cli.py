from __future__ import annotations

import argparse
import json
from pathlib import Path

from .metadata import build_metadata
from .parse import parse_unity_input
from .render import render_preview_images


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
    metadata_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    print(f"wrote {metadata_path}")
    if parsed.get("hhh_entries"):
        return 0

    print("no .hhh assets were discovered", flush=True)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
