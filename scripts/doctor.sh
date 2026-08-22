#!/usr/bin/env bash
# Diagnose common WSL / Windows clone issues
set -euo pipefail
cd "$(dirname "$0")/.."

echo "=== Platform Engineering MVP — Environment Doctor ==="
echo

fail=0
warn() { echo "⚠  $1"; }
err()  { echo "✗  $1"; fail=1; }
ok()   { echo "✓  $1"; }

echo "--- Location ---"
pwd
if [[ "$PWD" == /mnt/* ]]; then
  warn "Repo is on Windows filesystem ($PWD). Prefer ~/platform-engineering-mvp in WSL."
fi
[[ -f deps.edn ]] && ok "deps.edn found" || err "deps.edn missing — run from repo root"

echo
echo "--- Line endings (CRLF breaks WSL scripts) ---"
if file scripts/generate.sh | grep -q CRLF; then
  err "scripts/generate.sh has CRLF — run: ./scripts/fix-line-endings.sh"
else
  ok "scripts/generate.sh uses LF"
fi
if file deps.edn 2>/dev/null | grep -q CRLF; then
  warn "deps.edn has CRLF — run: dos2unix deps.edn"
else
  ok "deps.edn uses LF"
fi

echo
echo "--- Clojure CLI ---"
if command -v clojure >/dev/null; then
  ok "clojure at $(which clojure)"
  clojure --version 2>&1 | sed 's/^/    /'
  if [[ "$(which clojure)" == /mnt/* ]]; then
    warn "clojure is on Windows PATH — install inside WSL: /usr/local/bin/clojure"
  fi
else
  err "clojure not found"
  echo "    Install: curl -L -O https://github.com/clojure/brew-install/releases/latest/download/linux-install.sh"
  echo "             chmod +x linux-install.sh && sudo ./linux-install.sh"
fi

echo
echo "--- Quick generate test ---"
if clojure -M -m platform-mvp.compiler 2>/tmp/doctor-generate.err; then
  ok "clojure -M -m platform-mvp.compiler works"
  [[ -f generated/ops-policy.json ]] && ok "generated/ops-policy.json exists"
else
  err "generate failed:"
  sed 's/^/    /' /tmp/doctor-generate.err
  echo
  echo "    If you see '-M:generate (No such file or directory)':"
  echo "      → Wrong/broken clojure on PATH, or CRLF in deps.edn"
  echo "      → Use: clojure -M -m platform-mvp.compiler"
fi

echo
echo "--- Docker ---"
if command -v docker >/dev/null 2>&1; then
  ok "docker CLI installed at $(command -v docker)"
  if docker info >/dev/null 2>&1; then
    ok "docker daemon running"
  else
    warn "docker CLI found but daemon is not running (needed for terraform apply + MCP ops)"
    echo "    Start Docker Desktop or run: sudo service docker start"
  fi
else
  err "docker CLI not found (needed for terraform apply + MCP ops)"
  echo "    Install: https://docs.docker.com/engine/install/"
  echo "    Or on Ubuntu: sudo apt-get update && sudo apt-get install -y docker.io"
fi

echo
if [[ $fail -eq 0 ]]; then
  echo "=== All critical checks passed."
  if grep -qiE 'microsoft|wsl' /proc/version 2>/dev/null; then
    echo "    WSL: ./scripts/demo-wsl.sh"
  else
    echo "    Run: ./scripts/demo.sh"
  fi
else
  echo "=== Fix errors above, then retry."
  if grep -qiE 'microsoft|wsl' /proc/version 2>/dev/null; then
    echo "    WSL: ./scripts/demo-wsl.sh"
  else
    echo "    Run: ./scripts/demo.sh"
  fi
  exit 1
fi
