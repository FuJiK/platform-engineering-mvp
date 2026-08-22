#!/usr/bin/env bash
# End-to-end demo: tests → generate → terraform → MCP (operator + SRE)
set -euo pipefail
cd "$(dirname "$0")/.."

CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

step() { echo -e "\n${CYAN}========== $1 ==========${NC}"; }
ok()   { echo -e "${GREEN}✓${NC} $1"; }

CLOJURE_BIN="${CLOJURE_BIN:-clojure}"

step "0. Prerequisites"
command -v java >/dev/null || { echo "Java 17+ required"; exit 1; }
command -v "$CLOJURE_BIN" >/dev/null || { echo "Clojure CLI required ($CLOJURE_BIN)"; exit 1; }
command -v terraform >/dev/null || { echo "Terraform required"; exit 1; }
command -v docker >/dev/null || { echo "Docker required"; exit 1; }
command -v jq >/dev/null || { echo "jq recommended (brew install jq / apt install jq)"; }
ok "java $(java -version 2>&1 | head -1)"
ok "clojure $($CLOJURE_BIN --version 2>&1)"
ok "terraform $(terraform version | head -1)"
ok "docker $(docker --version)"

step "1. Unit tests"
"$CLOJURE_BIN" -M:test
ok "5 tests passed"

step "2. Generate from domain/service.edn"
"$CLOJURE_BIN" -M -m platform-mvp.compiler
ok "generated/main.tf.json"
ok "generated/ops-policy.json"

echo
echo "--- domain/service.edn (input) ---"
cat domain/service.edn
echo
echo "--- generated/main.tf.json ---"
jq . generated/main.tf.json
echo
echo "--- generated/ops-policy.json ---"
jq . generated/ops-policy.json

step "3. Terraform apply (nginx on :8080)"
cd generated
if ! terraform init -input=false >/dev/null; then
  echo
  echo "Terraform init failed."
  echo "Recovery:"
  echo "  1. Ensure Docker is installed and running (./scripts/doctor.sh)"
  echo "  2. cd generated && rm -rf .terraform .terraform.lock.hcl && terraform init"
  exit 1
fi
if ! terraform apply -auto-approve -input=false; then
  echo
  echo "Terraform apply failed."
  echo "Recovery:"
  echo "  1. Check Docker: docker info"
  echo "  2. Inspect state: cd generated && terraform plan"
  echo "  3. Clean up partial resources: cd generated && terraform destroy"
  echo "  4. Regenerate inputs: clojure -M:generate"
  exit 1
fi
cd ..
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8080)
ok "curl http://localhost:8080 → HTTP ${HTTP_CODE}"
docker ps --filter name=platform-mvp-web --format "  container={{.Names}} status={{.Status}}"

step "4. MCP Operator (get_status, get_logs)"
printf '%s\n' \
  '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"demo","version":"0.1.0"}}}' \
  '{"jsonrpc":"2.0","method":"notifications/initialized"}' \
  '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' \
  '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_status","arguments":{}}}' \
  '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"get_logs","arguments":{"lines":3}}}' \
| PLATFORM_ROLE=operator "$CLOJURE_BIN" -M -m platform-mvp.mcp 2>/dev/null \
| tee /tmp/mcp-demo-operator.jsonl
echo
ok "Operator tools: get_status, get_logs (no restart_service)"

step "5. MCP SRE (restart_service with approval)"
printf '%s\n' \
  '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"demo","version":"0.1.0"}}}' \
  '{"jsonrpc":"2.0","method":"notifications/initialized"}' \
  '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' \
  '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"restart_service","arguments":{"approved":false}}}' \
  '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"restart_service","arguments":{"approved":true}}}' \
  '{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"get_status","arguments":{}}}' \
| PLATFORM_ROLE=sre "$CLOJURE_BIN" -M -m platform-mvp.mcp 2>/dev/null \
| tee /tmp/mcp-demo-sre.jsonl

step "DONE"
echo -e "${GREEN}Demo complete.${NC}"
echo "MCP outputs saved to /tmp/mcp-demo-operator.jsonl and /tmp/mcp-demo-sre.jsonl"
