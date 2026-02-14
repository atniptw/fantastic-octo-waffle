#!/usr/bin/env python3
"""Dump UnityPy object trees for comparison with C# parser output.

Usage:
  python scripts/dump_unitypy_object_tree.py path/to/bundle.hhh
  python scripts/dump_unitypy_object_tree.py path/to/bundle.hhh -o output.json
"""
from __future__ import annotations

import argparse
import base64
import json
from typing import Any

import UnityPy


def _normalize(value: Any) -> Any:
    if value is None:
        return None
    if isinstance(value, (str, int, float, bool)):
        return value
    if isinstance(value, bytes):
        return {"__bytes__": base64.b64encode(value).decode("ascii")}
    if isinstance(value, bytearray):
        return {"__bytes__": base64.b64encode(bytes(value)).decode("ascii")}
    if isinstance(value, dict):
        return {str(k): _normalize(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return [_normalize(v) for v in value]
    # UnityPy objects or enums
    return str(value)


def dump_object_trees(input_path: str) -> dict:
    env = UnityPy.load(input_path)
    objects = []

    for obj in env.objects:
        try:
            tree = obj.read_typetree()
        except Exception as ex:  # UnityPy can fail on some objects; record error
            tree = {"__error__": str(ex)}

        objects.append(
            {
                "pathId": obj.path_id,
                "classId": obj.class_id,
                "type": obj.type.name if obj.type else None,
                "tree": _normalize(tree),
            }
        )

    return {
        "source": input_path,
        "unityVersion": getattr(env, "version", None),
        "objectCount": len(objects),
        "objects": objects,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Dump UnityPy object trees to JSON")
    parser.add_argument("input", help="Path to UnityFS bundle or serialized file")
    parser.add_argument("-o", "--output", help="Output JSON file path")
    args = parser.parse_args()

    output_path = args.output or f"{args.input}_unitypy_tree.json"
    result = dump_object_trees(args.input)

    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=2, ensure_ascii=False)

    print(f"Wrote UnityPy object tree: {output_path}")


if __name__ == "__main__":
    main()
