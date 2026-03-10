#!/usr/bin/env bash
set -euo pipefail

hook_path=".git/hooks/commit-msg"

cat > "$hook_path" <<'HOOK'
#!/usr/bin/env bash
set -euo pipefail
python3 scripts/validate_commit_message.py --message-file "$1"
HOOK

chmod +x "$hook_path"
echo "installed commit-msg hook at $hook_path"
