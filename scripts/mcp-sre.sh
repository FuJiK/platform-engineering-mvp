#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PLATFORM_ROLE=sre
exec clojure -M -m platform-mvp.mcp
