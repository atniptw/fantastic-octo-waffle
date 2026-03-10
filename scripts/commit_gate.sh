#!/usr/bin/env bash
set -euo pipefail

RUN_MYPY="0"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --strict-types)
      RUN_MYPY="1"
      shift 1
      ;;
    *)
      echo "unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

if [[ -f ".venv/bin/activate" ]]; then
  # shellcheck disable=SC1091
  source .venv/bin/activate
fi

ruff check processor/src tests
if [[ "$RUN_MYPY" == "1" ]]; then
  mypy processor/src tests
else
  echo "info: skipping mypy; pass --strict-types to include type checks"
fi
pytest -q
