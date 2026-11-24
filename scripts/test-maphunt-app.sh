#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "${OSTYPE:-}" == linux* && -z "${XDG_RUNTIME_DIR:-}" ]]; then
  MapHuntRuntimeDir="${TMPDIR:-/tmp}/acai2-maphunt-runtime-$(id -u)"
  mkdir -p "$MapHuntRuntimeDir"
  chmod 700 "$MapHuntRuntimeDir"
  export XDG_RUNTIME_DIR="$MapHuntRuntimeDir"
fi
python3 tools/ACAI2.MapHunt/build_app.py
dotnet build src/ACAI2.Rynth.MapHunt.Plugin/ACAI2.Rynth.MapHunt.Plugin.csproj -c Release
dotnet run --project tests/ACAI2.Rynth.MapHunt.Tests -c Release
