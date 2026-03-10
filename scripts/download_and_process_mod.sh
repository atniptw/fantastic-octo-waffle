#!/usr/bin/env bash
set -euo pipefail

url="${1:-}"
if [[ -z "$url" ]]; then
  echo "usage: $0 <mod-zip-url>" >&2
  exit 2
fi

mkdir -p data data/outputs

out="$(URL="$url" python3 - <<'PY'
import os
import re
from urllib.parse import urlparse

url = os.environ["URL"]
path = urlparse(url).path
match = re.search(r"/package/download/([^/]+)/([^/]+)/([^/]+)/?$", path)

if match:
    name = f"{match.group(1)}-{match.group(2)}-{match.group(3)}.zip"
else:
    stem = path.rstrip("/").split("/")[-1]
    if not stem:
        stem = "download"
    name = stem if stem.lower().endswith(".zip") else f"{stem}.zip"

print(f"data/{name}")
PY
)"

echo "Downloading $url -> $out"
curl -fL "$url" -o "$out"

./scripts/process_mod_file.sh "$out"
