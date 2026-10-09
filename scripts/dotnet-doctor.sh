#!/usr/bin/env bash
# Diagnose .NET / dj-setup-list readiness
set -euo pipefail
cd "$(dirname "$0")/.."

echo "=== .NET / DJ Setup List — Environment Doctor ==="
echo

fail=0
warn() { echo "⚠  $1"; }
err()  { echo "✗  $1"; fail=1; }
ok()   { echo "✓  $1"; }

# Prefer user-local install from dotnet-install.sh
if [[ -z "${DOTNET_ROOT:-}" && -x "$HOME/.dotnet/dotnet" ]]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
fi

echo "--- .NET SDK ---"
if command -v dotnet >/dev/null; then
  ok "dotnet at $(command -v dotnet)"
  dotnet --info | sed -n '1,20p' | sed 's/^/    /'
else
  err "dotnet not found"
  echo "    Install: curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0"
  echo "    Then:    export DOTNET_ROOT=\"\$HOME/.dotnet\""
  echo "             export PATH=\"\$DOTNET_ROOT:\$DOTNET_ROOT/tools:\$PATH\""
fi

echo
echo "--- dj-setup-list ---"
if [[ -f dj-setup-list/DjSetupList.sln ]]; then
  ok "solution found"
else
  err "dj-setup-list/DjSetupList.sln missing"
fi

if command -v dotnet >/dev/null && [[ -f dj-setup-list/DjSetupList.sln ]]; then
  if (cd dj-setup-list && dotnet test --nologo -v q); then
    ok "dotnet test passed"
  else
    err "dotnet test failed"
  fi

  if (cd dj-setup-list && dotnet run --project src/DjSetupList --no-build -- doctor >/tmp/dj-doctor.out 2>&1) \
     || (cd dj-setup-list && dotnet run --project src/DjSetupList -- doctor >/tmp/dj-doctor.out 2>&1); then
    ok "dj-setup-list doctor"
    sed 's/^/    /' /tmp/dj-doctor.out | tail -n 20
  else
    err "dj-setup-list doctor failed"
    sed 's/^/    /' /tmp/dj-doctor.out || true
  fi
fi

echo
if [[ $fail -eq 0 ]]; then
  echo "=== All critical checks passed."
  echo "    Try: cd dj-setup-list && dotnet run --project src/DjSetupList -- templates"
else
  echo "=== Fix errors above, then retry."
  exit 1
fi
