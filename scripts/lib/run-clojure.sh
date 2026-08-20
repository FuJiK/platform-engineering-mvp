#!/usr/bin/env bash
# Run Clojure entrypoints reliably on WSL (alias-free, WSL-native clojure preferred).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

export PATH="/usr/local/bin:/usr/local/sbin:$HOME/.local/bin:$PATH"

resolve_clojure() {
  if [[ -n "${CLOJURE_BIN:-}" && -x "$CLOJURE_BIN" ]]; then
    echo "$CLOJURE_BIN"
    return 0
  fi
  if [[ -x /usr/local/bin/clojure ]]; then
    echo /usr/local/bin/clojure
    return 0
  fi
  local found
  found="$(command -v clojure 2>/dev/null || true)"
  if [[ -n "$found" && "$found" != /mnt/* ]]; then
    echo "$found"
    return 0
  fi
  return 1
}

run_clojure() {
  local bin
  if ! bin="$(resolve_clojure)"; then
    echo "ERROR: No WSL-native clojure found." >&2
    echo "Run: ./scripts/wsl-bootstrap.sh" >&2
    return 127
  fi
  exec "$bin" "$@"
}

run_clojure "$@"
