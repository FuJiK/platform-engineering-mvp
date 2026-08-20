#!/usr/bin/env bash
# WSL demo: CRLF fix, PATH normalization, then full end-to-end demo.
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

step "WSL preflight"

if ! grep -qiE 'microsoft|wsl' /proc/version 2>/dev/null; then
  warn "Not running under WSL — falling back to ./scripts/demo.sh"
  exec ./scripts/demo.sh
fi
ok "WSL detected: $(grep -oP 'PRETTY_NAME="\K[^"]+' /etc/os-release 2>/dev/null || uname -r)"

if [[ "$PWD" == /mnt/* ]]; then
  warn "Repo is on Windows drive ($PWD)"
  warn "If you hit errors, copy to WSL home: cp -r \"$PWD\" ~/platform-engineering-mvp"
else
  ok "Repo is on WSL filesystem ($PWD)"
fi

step "Fix CRLF line endings (Windows clone)"
./scripts/fix-line-endings.sh
ok "scripts/*.sh and deps.edn normalized to LF"

step "Normalize PATH for non-interactive shells"
export PATH="/usr/local/bin:/usr/local/sbin:$HOME/.local/bin:$PATH"

if [[ -x /usr/local/bin/clojure ]]; then
  export CLOJURE_BIN=/usr/local/bin/clojure
  ok "Using WSL clojure: $CLOJURE_BIN"
elif command -v clojure >/dev/null 2>&1; then
  CLOJURE_PATH="$(command -v clojure)"
  if [[ "$CLOJURE_PATH" == /mnt/* ]]; then
    err "clojure resolves to Windows path ($CLOJURE_PATH). Install inside WSL:
  curl -L -O https://github.com/clojure/brew-install/releases/latest/download/linux-install.sh
  chmod +x linux-install.sh && sudo ./linux-install.sh"
  fi
  export CLOJURE_BIN="$CLOJURE_PATH"
  ok "Using clojure: $CLOJURE_BIN"
else
  err "clojure not found in WSL. Install:
  curl -L -O https://github.com/clojure/brew-install/releases/latest/download/linux-install.sh
  chmod +x linux-install.sh && sudo ./linux-install.sh"
fi

"$CLOJURE_BIN" --version | sed 's/^/  /'

step "Verify generate (alias-free)"
if ! "$CLOJURE_BIN" -M -m platform-mvp.compiler >/dev/null 2>&1; then
  err "clojure -M -m platform-mvp.compiler failed. Run ./scripts/doctor.sh for details."
fi
ok "Generate works with: clojure -M -m platform-mvp.compiler"

step "Docker check"
if docker info >/dev/null 2>&1; then
  ok "Docker daemon reachable"
else
  warn "Docker is not running."
  warn "Start Docker Desktop and enable WSL integration, or run: sudo service docker start"
  err "Docker required for terraform apply and MCP operations demo"
fi

echo
echo -e "${GREEN}WSL preflight passed. Starting full demo...${NC}"
echo

exec env CLOJURE_BIN="$CLOJURE_BIN" ./scripts/demo.sh
