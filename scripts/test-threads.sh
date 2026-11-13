#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "${OSTYPE:-}" == linux* && -z "${XDG_RUNTIME_DIR:-}" ]]; then
  ThreadsRuntimeDir="${TMPDIR:-/tmp}/acai2-rynth-runtime-$(id -u)"
  mkdir -p "$ThreadsRuntimeDir"
  chmod 700 "$ThreadsRuntimeDir"
  export XDG_RUNTIME_DIR="$ThreadsRuntimeDir"
fi
dotnet build src/ACAI2.Rynth.Threads.Plugin/ACAI2.Rynth.Threads.Plugin.csproj -c Release
dotnet run --project tests/ACAI2.Rynth.Threads.Tests -c Release
