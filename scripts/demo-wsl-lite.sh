#!/usr/bin/env bash
# WSL lite demo: no Docker required (tests + generate + MCP initialize/tools/list)
set -euo pipefail
cd "$(dirname "$0")/.."

CYAN='\033[0;36m'
GREEN='\033[0;32m'
NC='\033[0m'
step() { echo -e "\n${CYAN}========== $1 ==========${NC}"; }
ok()   { echo -e "${GREEN}✓${NC} $1"; }

export PATH="/usr/local/bin:/usr/local/sbin:$HOME/.local/bin:$PATH"
export CLOJURE_BIN="${CLOJURE_BIN:-/usr/local/bin/clojure}"
[[ -x "$CLOJURE_BIN" ]] || CLOJURE_BIN="$(command -v clojure)"

step "1. Tests"
"$CLOJURE_BIN" -M:test
ok "tests passed"

step "2. Generate"
"$CLOJURE_BIN" -M -m platform-mvp.compiler
ok "terraform JSON + ops-policy generated"

echo
echo "--- generated/main.tf.json ---"
jq . generated/main.tf.json 2>/dev/null || cat generated/main.tf.json
echo
echo "--- generated/ops-policy.json ---"
jq . generated/ops-policy.json 2>/dev/null || cat generated/ops-policy.json

step "3. MCP smoke (no Docker needed for initialize/tools/list)"
printf '%s\n' \
  '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"wsl-lite","version":"0.1.0"}}}' \
  '{"jsonrpc":"2.0","method":"notifications/initialized"}' \
  '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' \
| PLATFORM_ROLE=operator "$CLOJURE_BIN" -M -m platform-mvp.mcp 2>/dev/null
echo
ok "MCP server starts and lists operator tools"

step "DONE (lite)"
echo "Docker/terraform steps skipped. After Docker is up, run: ./scripts/demo-wsl.sh"
