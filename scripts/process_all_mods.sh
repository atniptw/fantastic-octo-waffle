#!/usr/bin/env bash
set -euo pipefail

OUTPUT_DIR="data/outputs"
DEPENDENCY_FILE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --output)
      OUTPUT_DIR="$2"
      shift 2
      ;;
    --dependency-unitypackage)
      DEPENDENCY_FILE="$2"
      shift 2
      ;;
    *)
      echo "unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

mapfile -t mod_files < <(find data -maxdepth 1 -type f -name "*.zip" | sort)
if [[ ${#mod_files[@]} -eq 0 ]]; then
  echo "error: no mod zip files found under data/" >&2
  exit 2
fi

if [[ -z "$DEPENDENCY_FILE" ]]; then
  mapfile -t detected_packages < <(find data -maxdepth 1 -type f -name "*.unitypackage" | sort)
  if [[ ${#detected_packages[@]} -eq 1 ]]; then
    DEPENDENCY_FILE="${detected_packages[0]}"
    echo "info: auto-detected dependency unitypackage: $DEPENDENCY_FILE"
  elif [[ ${#detected_packages[@]} -gt 1 ]]; then
    echo "info: multiple dependency unitypackages found in data/; pass one explicitly to avoid ambiguity" >&2
  fi
fi

if [[ -n "$DEPENDENCY_FILE" && ! -f "$DEPENDENCY_FILE" ]]; then
  echo "error: dependency file not found: $DEPENDENCY_FILE" >&2
  exit 2
fi

mkdir -p "$OUTPUT_DIR"

run_processor() {
  local mod_file="$1"
  local metadata_name="$2"
  local -a cmd

  cmd=(.venv/bin/python -m unity_processor.cli "$mod_file" --output "$OUTPUT_DIR" --metadata "$metadata_name")
  if [[ -n "$DEPENDENCY_FILE" ]]; then
    cmd+=(--dependency-unitypackage "$DEPENDENCY_FILE")
  fi

  PYTHONPATH=processor/src "${cmd[@]}"
}

tmp_aggregate_list="$(mktemp)"
trap 'rm -f "$tmp_aggregate_list"' EXIT

for mod_file in "${mod_files[@]}"; do
  base_name="$(basename "$mod_file")"
  slug="$(echo "${base_name%.zip}" | tr '[:upper:]' '[:lower:]' | sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//')"
  metadata_name="metadata-${slug}.json"
  echo "processing $mod_file -> $OUTPUT_DIR/$metadata_name"
  run_processor "$mod_file" "$metadata_name"
  printf '%s\n' "$OUTPUT_DIR/$metadata_name" >> "$tmp_aggregate_list"
done

.venv/bin/python - "$tmp_aggregate_list" "$OUTPUT_DIR/metadata.json" <<'PY'
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

list_path = Path(sys.argv[1])
out_path = Path(sys.argv[2])

metadata_files = [Path(line.strip()) for line in list_path.read_text(encoding="utf-8").splitlines() if line.strip()]

combined_images: list[dict] = []
combined_warnings: list[str] = []
source_mods: list[dict] = []
dependency_summary = None
dependency_unitypackage = None

hhh_count = 0
hhh_inspected = 0
hhh_parse_errors = 0
hhh_with_materials = 0
hhh_with_local_textures = 0
hhh_with_dependency_texture_candidates = 0
model_count = 0
export_failed_count = 0

for metadata_file in metadata_files:
    data = json.loads(metadata_file.read_text(encoding="utf-8"))
    mod_source = data.get("source")
    mod_name = Path(mod_source).stem if isinstance(mod_source, str) and mod_source else "Unknown Mod"

    source_mods.append(
        {
            "source": mod_source,
            "mod_name": mod_name,
            "hhh_count": int(data.get("hhh_count", 0)),
            "model_count": int(data.get("model_count", 0)),
            "export_failed_count": int(data.get("export_failed_count", 0)),
            "metadata_file": str(metadata_file),
        }
    )

    for item in data.get("images", []):
      normalized = dict(item)
      normalized["mod_source"] = mod_source
      normalized["mod_name"] = mod_name
      combined_images.append(normalized)

    combined_warnings.extend(data.get("warnings", []))

    hhh_count += int(data.get("hhh_count", 0))
    hhh_inspected += int(data.get("hhh_inspected", 0))
    hhh_parse_errors += int(data.get("hhh_parse_errors", 0))
    hhh_with_materials += int(data.get("hhh_with_materials", 0))
    hhh_with_local_textures += int(data.get("hhh_with_local_textures", 0))
    hhh_with_dependency_texture_candidates += int(data.get("hhh_with_dependency_texture_candidates", 0))
    model_count += int(data.get("model_count", 0))
    export_failed_count += int(data.get("export_failed_count", 0))

    if dependency_summary is None and data.get("dependency_summary") is not None:
        dependency_summary = data.get("dependency_summary")
        dependency_unitypackage = data.get("dependency_unitypackage")

combined = {
    "schema_version": 2,
    "generated_at": datetime.now(timezone.utc).isoformat(),
    "source": "multiple-mods",
    "source_mods": source_mods,
    "dependency_unitypackage": dependency_unitypackage,
    "dependency_summary": dependency_summary,
    "hhh_count": hhh_count,
    "hhh_inspected": hhh_inspected,
    "hhh_parse_errors": hhh_parse_errors,
    "hhh_with_materials": hhh_with_materials,
    "hhh_with_local_textures": hhh_with_local_textures,
    "hhh_with_dependency_texture_candidates": hhh_with_dependency_texture_candidates,
    "model_count": model_count,
    "export_failed_count": export_failed_count,
    "status": "ok" if model_count else "no-exported-models",
    "warnings": combined_warnings,
    "images": combined_images,
}

out_path.write_text(json.dumps(combined, indent=2), encoding="utf-8")
print(f"wrote {out_path}")
PY

echo "Processed ${#mod_files[@]} mods -> $OUTPUT_DIR/metadata.json"