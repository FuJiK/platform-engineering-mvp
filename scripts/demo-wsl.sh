#!/usr/bin/env bash
# WSL demo: bootstrap if needed, then full demo.
set -euo pipefail
cd "$(dirname "$0")/.."

CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

step() { echo -e "\n${CYAN}========== $1 ==========${NC}"; }
ok()   { echo -e "${GREEN}✓${NC} $1"; }
warn() { echo -e "${YELLOW}⚠${NC}  $1"; }
err()  { echo -e "${RED}✗${NC} $1"; exit 1; }

if ! grep -qiE 'microsoft|wsl' /proc/version 2>/dev/null; then
  warn "Not WSL — using ./scripts/demo.sh"
  exec ./scripts/demo.sh
fi

# Auto-bootstrap if clojure missing or on Windows PATH
export PATH="/usr/local/bin:/usr/local/sbin:$HOME/.local/bin:$PATH"
NEED_BOOTSTRAP=0
if [[ ! -x /usr/local/bin/clojure ]]; then
  NEED_BOOTSTRAP=1
elif [[ "$(command -v clojure 2>/dev/null || true)" == /mnt/* ]]; then
  NEED_BOOTSTRAP=1
elif ! /usr/local/bin/clojure -M -m platform-mvp.compiler >/dev/null 2>&1; then
  NEED_BOOTSTRAP=1
fi

if [[ "$NEED_BOOTSTRAP" -eq 1 ]]; then
  step "Auto-bootstrap (first run or broken env)"
  ./scripts/wsl-bootstrap.sh
fi

export CLOJURE_BIN=/usr/local/bin/clojure

step "CRLF fix"
./scripts/fix-line-endings.sh
ok "line endings OK"

step "Docker"
if ! docker info >/dev/null 2>&1; then
  warn "Docker not available — running lite demo instead"
  exec ./scripts/demo-wsl-lite.sh
fi
ok "Docker OK"

echo
ok "Starting full demo..."
exec env CLOJURE_BIN="$CLOJURE_BIN" ./scripts/demo.sh
