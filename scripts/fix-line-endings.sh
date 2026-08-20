#!/usr/bin/env bash
# Convert CRLF -> LF for WSL/Linux after cloning on Windows.
set -euo pipefail
cd "$(dirname "$0")/.."

if command -v dos2unix >/dev/null 2>&1; then
  dos2unix scripts/*.sh deps.edn 2>/dev/null || true
else
  sed -i 's/\r$//' scripts/*.sh deps.edn
fi

echo "Fixed line endings for:"
echo " - scripts/*.sh"
echo " - deps.edn"
echo
echo "Retry:"
echo "  ./scripts/generate.sh"
