#!/usr/bin/env bash
set -euo pipefail

workspace=""

while IFS= read -r file; do
  workspace="$file"
  break
done < <(find . -maxdepth 3 -type f \( -name "*.sln" -o -name "*.csproj" \))

if [[ -z "$workspace" ]]; then
  echo "No .NET solution or project found; skipping dotnet format."
  exit 0
fi

echo "Running dotnet format on $workspace"
dotnet format "$workspace" --verify-no-changes
