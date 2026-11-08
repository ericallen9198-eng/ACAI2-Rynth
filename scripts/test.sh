#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [ ! -f external/RynthCore/src/RynthCore.PluginSdk/RynthCore.PluginSdk.csproj ]; then
  git submodule update --init --recursive
fi
# .NET 10 named mutexes need a writable, user-owned XDG runtime directory in a
# restricted cloud workspace. Preserve an already configured directory.
if [[ "${OSTYPE:-}" == linux* && -z "${XDG_RUNTIME_DIR:-}" ]]; then
  PilotRuntimeDir="${TMPDIR:-/tmp}/acai2-rynth-runtime-$(id -u)"
  mkdir -p "$PilotRuntimeDir"
  chmod 700 "$PilotRuntimeDir"
  export XDG_RUNTIME_DIR="$PilotRuntimeDir"
fi
dotnet build src/ACAI2.Rynth.Plugin/ACAI2.Rynth.Plugin.csproj -c Release
dotnet run --project tests/ACAI2.Rynth.Tests -c Release -- src/ACAI2.Rynth.Plugin/bin/Release/net10.0-windows/win-x86/ACAI2.Rynth.Plugin.dll
