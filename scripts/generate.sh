#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
# Use -m directly (alias-free) — survives deps.edn CRLF / broken -M:alias on some WSL setups
clojure -M -m platform-mvp.compiler
