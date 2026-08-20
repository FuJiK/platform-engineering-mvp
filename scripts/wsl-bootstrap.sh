#!/usr/bin/env bash
# One-shot WSL environment fix: deps, clojure, CRLF, optional repo copy off /mnt/c
set -euo pipefail

CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

step() { echo -e "\n${CYAN}>>> $1${NC}"; }
ok()   { echo -e "${GREEN}✓${NC} $1"; }
warn() { echo -e "${YELLOW}⚠${NC}  $1"; }
die()  { echo -e "${RED}✗${NC} $1"; exit 1; }

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

step "1. WSL check"
grep -qiE 'microsoft|wsl' /proc/version 2>/dev/null \
  || die "Not WSL. Use ./scripts/demo.sh on macOS/Linux instead."
ok "WSL OK"

step "2. Repo location"
if [[ "$REPO_ROOT" == /mnt/* ]]; then
  warn "Repo is on Windows filesystem: $REPO_ROOT"
  TARGET="$HOME/platform-engineering-mvp"
  if [[ ! -d "$TARGET/.git" ]]; then
    step "2b. Copy repo to WSL home (recommended)"
    mkdir -p "$(dirname "$TARGET")"
    cp -a "$REPO_ROOT" "$TARGET"
    ok "Copied to $TARGET"
    echo
    echo "Re-run from WSL home:"
    echo "  cd $TARGET && ./scripts/wsl-bootstrap.sh"
    exit 0
  else
    warn "WSL copy exists at $TARGET — use that instead:"
    echo "  cd $TARGET && ./scripts/wsl-bootstrap.sh"
  fi
else
  ok "Repo on WSL filesystem: $REPO_ROOT"
fi

step "3. Git line endings (this repo only)"
git config core.autocrlf false 2>/dev/null || true
git config core.eol lf 2>/dev/null || true
if git rev-parse --git-dir >/dev/null 2>&1; then
  git add --renormalize . 2>/dev/null || true
fi
ok "core.autocrlf=false, core.eol=lf"

step "4. Fix CRLF on scripts and deps.edn"
if command -v dos2unix >/dev/null 2>&1; then
  dos2unix scripts/*.sh deps.edn 2>/dev/null || true
else
  sudo apt-get update -qq
  sudo apt-get install -y -qq dos2unix
  dos2unix scripts/*.sh deps.edn
fi
# Also fix any stray CRLF in clojure sources
find src test -name '*.clj' -print0 2>/dev/null | xargs -0 -r sed -i 's/\r$//' || true
ok "CRLF removed"

step "5. System packages"
export DEBIAN_FRONTEND=noninteractive
sudo apt-get update -qq
sudo apt-get install -y -qq openjdk-17-jdk curl unzip jq ca-certificates
ok "Java 17 + tools installed"

step "6. Clojure CLI (WSL-native)"
export PATH="/usr/local/bin:/usr/local/sbin:$PATH"
if [[ -x /usr/local/bin/clojure ]] && /usr/local/bin/clojure --version >/dev/null 2>&1; then
  ok "clojure already installed: $(/usr/local/bin/clojure --version 2>&1)"
else
  TMPDIR="$(mktemp -d)"
  curl -fsSL -o "$TMPDIR/linux-install.sh" \
    https://github.com/clojure/brew-install/releases/latest/download/linux-install.sh
  chmod +x "$TMPDIR/linux-install.sh"
  sudo "$TMPDIR/linux-install.sh"
  rm -rf "$TMPDIR"
  ok "Installed: $(/usr/local/bin/clojure --version 2>&1)"
fi

step "7. Verify generate"
export CLOJURE_BIN=/usr/local/bin/clojure
if ! "$CLOJURE_BIN" -M -m platform-mvp.compiler 2>/tmp/wsl-bootstrap-generate.err; then
  echo "--- error ---"
  cat /tmp/wsl-bootstrap-generate.err
  die "Generate still failing — paste output above when asking for help"
fi
ok "generated/main.tf.json and generated/ops-policy.json"

step "8. Docker"
if docker info >/dev/null 2>&1; then
  ok "Docker daemon reachable"
  echo
  echo -e "${GREEN}Bootstrap complete. Run full demo:${NC}"
  echo "  ./scripts/demo-wsl.sh"
else
  warn "Docker not running."
  echo "  - Start Docker Desktop → Settings → Resources → WSL Integration → enable your distro"
  echo "  - Or: sudo service docker start"
  echo
  echo -e "${GREEN}Bootstrap complete (without Docker). Run lite demo:${NC}"
  echo "  ./scripts/demo-wsl-lite.sh"
fi
