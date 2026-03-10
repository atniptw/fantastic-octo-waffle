#!/usr/bin/env bash
set -euo pipefail

mod_file="${1:-}"
dependency_file="${2:-}"

if [[ -z "$mod_file" ]]; then
  echo "usage: $0 <mod-zip-file> [dependency-unitypackage]" >&2
  exit 2
fi

if [[ ! -f "$mod_file" ]]; then
  echo "error: file not found: $mod_file" >&2
  exit 2
fi

if [[ "$mod_file" == *.unitypackage ]]; then
  echo "error: '$mod_file' is a dependency unitypackage, not a mod zip input" >&2
  echo "hint: use a mod .zip as first argument and pass unitypackage as second argument" >&2
  exit 2
fi

if ! python3 - <<'PY' "$mod_file"
import sys
import zipfile
path = sys.argv[1]
sys.exit(0 if zipfile.is_zipfile(path) else 1)
PY
then
  echo "error: '$mod_file' is not a valid zip file" >&2
  exit 2
fi

cmd=(.venv/bin/python -m unity_processor.cli "$mod_file" --output data/outputs)
if [[ -z "$dependency_file" ]]; then
  mapfile -t detected_packages < <(find data -maxdepth 1 -type f -name "*.unitypackage" | sort)
  if [[ ${#detected_packages[@]} -eq 1 ]]; then
    dependency_file="${detected_packages[0]}"
    echo "info: auto-detected dependency unitypackage: $dependency_file"
  elif [[ ${#detected_packages[@]} -gt 1 ]]; then
    echo "info: multiple dependency unitypackages found in data/; pass one explicitly to avoid ambiguity" >&2
  fi
fi

if [[ -n "$dependency_file" ]]; then
  if [[ ! -f "$dependency_file" ]]; then
    echo "error: dependency file not found: $dependency_file" >&2
    exit 2
  fi
  cmd+=(--dependency-unitypackage "$dependency_file")
fi

mkdir -p data/outputs
PYTHONPATH=processor/src "${cmd[@]}"
echo "Processed $mod_file -> data/outputs"
