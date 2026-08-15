#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PLATFORM_ROLE=operator
exec clojure -M:mcp
