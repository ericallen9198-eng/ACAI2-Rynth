#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "${OSTYPE:-}" == linux* && -z "${XDG_RUNTIME_DIR:-}" ]]; then
  CombatRuntimeDir="${TMPDIR:-/tmp}/acai2-rynth-runtime-$(id -u)"
  mkdir -p "$CombatRuntimeDir"
  chmod 700 "$CombatRuntimeDir"
  export XDG_RUNTIME_DIR="$CombatRuntimeDir"
fi
dotnet build src/ACAI2.Rynth.Combat.Plugin -c Release
dotnet run --project tests/ACAI2.Rynth.Combat.Tests -c Release
