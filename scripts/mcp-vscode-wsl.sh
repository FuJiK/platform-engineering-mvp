#!/usr/bin/env bash
# VS Code (WSL) launches MCP with a minimal environment.
# This wrapper restores PATH and validates prerequisites before starting.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

export PATH="/usr/local/bin:/usr/local/sbin:$HOME/.local/bin:$PATH"

if ! command -v clojure >/dev/null 2>&1; then
  echo "clojure not found. Install Clojure CLI inside WSL, then retry." >&2
  echo "  curl -L -O https://github.com/clojure/brew-install/releases/latest/download/linux-install.sh" >&2
  echo "  chmod +x linux-install.sh && sudo ./linux-install.sh" >&2
  exit 127
fi

if [[ ! -f "$REPO_ROOT/generated/ops-policy.json" ]]; then
  echo "Missing generated/ops-policy.json. Run from repo root:" >&2
  echo "  clojure -M:generate" >&2
  exit 1
fi

export PLATFORM_ROLE="${PLATFORM_ROLE:-operator}"
exec clojure -M:mcp
