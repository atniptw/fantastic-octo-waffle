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
        description="Phase 1 placeholder CLI for UnityPy image pipeline.",
    )
    parser.add_argument("input", nargs="?", default="sample.input", help="Unity input path")
    parser.add_argument("--output", default="outputs", help="Output directory")
    parser.add_argument("--metadata", default="metadata.json", help="Metadata file name")
    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    input_path = Path(args.input)
    output_dir = Path(args.output)
    parsed = parse_unity_input(input_path)
    images = render_preview_images(parsed, output_dir)
    metadata = build_metadata(parsed, images)

    metadata_path = output_dir / args.metadata
    metadata_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    print(f"wrote {metadata_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
