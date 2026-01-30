#!/usr/bin/env python3
"""Compare UnityPy and C# object tree JSON outputs.

Usage:
  python scripts/compare_object_trees.py unitypy.json csharp.json
"""
from __future__ import annotations

import argparse
import json
from typing import Any, Dict, List, Tuple


def _normalize_bytes(obj: Any) -> Any:
    if isinstance(obj, dict):
        if "__bytes__" in obj and len(obj) == 1:
            return obj["__bytes__"]
        return {k: _normalize_bytes(v) for k, v in obj.items()}
    if isinstance(obj, list):
        return [_normalize_bytes(v) for v in obj]
    return obj


def _diff(a: Any, b: Any, path: str, out: List[str], max_diffs: int) -> None:
    if len(out) >= max_diffs:
        return
    if type(a) != type(b):
        out.append(f"TYPE {path}: {type(a).__name__} != {type(b).__name__}")
        return
    if isinstance(a, dict):
        keys = set(a.keys()) | set(b.keys())
        for k in sorted(keys):
            if k not in a:
                out.append(f"MISSING {path}.{k}: only in C#")
            elif k not in b:
                out.append(f"MISSING {path}.{k}: only in UnityPy")
            else:
                _diff(a[k], b[k], f"{path}.{k}", out, max_diffs)
            if len(out) >= max_diffs:
                return
        return
    if isinstance(a, list):
        if len(a) != len(b):
            out.append(f"LEN {path}: {len(a)} != {len(b)}")
            return
        for i, (x, y) in enumerate(zip(a, b)):
            _diff(x, y, f"{path}[{i}]", out, max_diffs)
            if len(out) >= max_diffs:
                return
        return
    if a != b:
        out.append(f"VAL {path}: {a} != {b}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Compare UnityPy vs C# object tree JSON")
    parser.add_argument("unitypy", help="UnityPy JSON output")
    parser.add_argument("csharp", help="C# JSON output")
    parser.add_argument("--max", type=int, default=200, help="Max diffs to print")
    args = parser.parse_args()

    with open(args.unitypy, "r", encoding="utf-8") as f:
        unitypy = _normalize_bytes(json.load(f))
    with open(args.csharp, "r", encoding="utf-8") as f:
        csharp = _normalize_bytes(json.load(f))

    u_objs = {o["pathId"]: o for o in unitypy.get("objects", [])}
    c_objs = {o["pathId"]: o for o in csharp.get("objects", [])}

    only_u = sorted(set(u_objs.keys()) - set(c_objs.keys()))
    only_c = sorted(set(c_objs.keys()) - set(u_objs.keys()))

    print(f"Objects: UnityPy={len(u_objs)}, C#={len(c_objs)}")
    if only_u:
        print(f"Only in UnityPy: {len(only_u)}")
    if only_c:
        print(f"Only in C#: {len(only_c)}")

    diffs: List[str] = []
    for path_id in sorted(set(u_objs.keys()) & set(c_objs.keys())):
        _diff(u_objs[path_id], c_objs[path_id], f"object[{path_id}]", diffs, args.max)
        if len(diffs) >= args.max:
            break

    if not diffs:
        print("No diffs found (within compared objects).")
    else:
        print("Diffs:")
        for d in diffs:
            print(d)


if __name__ == "__main__":
    main()
