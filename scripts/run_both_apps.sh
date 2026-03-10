#!/usr/bin/env bash
set -euo pipefail

MOD_ZIP=""
DEPENDENCY_UNITYPACKAGE=""
PORT="8000"
OUTPUT_DIR="data/outputs"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --mod-zip)
      MOD_ZIP="$2"
      shift 2
      ;;
    --dependency-unitypackage)
      DEPENDENCY_UNITYPACKAGE="$2"
      shift 2
      ;;
    --port)
      PORT="$2"
      shift 2
      ;;
    --output)
      OUTPUT_DIR="$2"
      shift 2
      ;;
    *)
      echo "unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

if [[ -z "$MOD_ZIP" ]]; then
  echo "error: --mod-zip is required" >&2
  exit 2
fi

if [[ ! -f "$MOD_ZIP" ]]; then
  echo "error: mod zip not found: $MOD_ZIP" >&2
  exit 2
fi

if [[ -f ".venv/bin/activate" ]]; then
  # shellcheck disable=SC1091
  source .venv/bin/activate
fi

cmd=(python3 -m unity_processor.cli "$MOD_ZIP" --output "$OUTPUT_DIR")
if [[ -n "$DEPENDENCY_UNITYPACKAGE" ]]; then
  cmd+=(--dependency-unitypackage "$DEPENDENCY_UNITYPACKAGE")
fi

PYTHONPATH=processor/src "${cmd[@]}"

python3 scripts/check_processed_outputs.py --metadata "$OUTPUT_DIR/metadata.json"

echo "starting viewer at http://localhost:$PORT/viewer/"
cd /workspaces/fantastic-octo-waffle
exec python3 -m http.server "$PORT"
