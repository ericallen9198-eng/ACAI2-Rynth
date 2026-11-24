#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
: "${RynthSuiteRoot:?Set RynthSuiteRoot to the genuine compatible RynthSuite source checkout}"
MapHuntTestDir=$(mktemp -d /tmp/maphunt-checks.XXXXXX)
trap 'rm -rf "$MapHuntTestDir"' EXIT
python3 tools/ACAI2.MapHunt/build.py
dotnet run --project tools/ACAI2.MapHunt "-p:RynthSuiteRoot=$RynthSuiteRoot" -- metas/maphunt/MetaFiles/MapHunt.af "$MapHuntTestDir/rules.json"
python3 tools/ACAI2.MapHunt/test_meta.py "$MapHuntTestDir/rules.json"
dotnet run --project tools/ACAI2.MapHunt "-p:RynthSuiteRoot=$RynthSuiteRoot" -- --lua-test tools/ACAI2.MapHunt/lua_setup.lua metas/maphunt/Scripts/MapHunt/index.lua tools/ACAI2.MapHunt/lua_checks.lua "$MapHuntTestDir/lua-expressions.json"
echo 'Windows publication additionally audits the actual x86 expression engine and tests the installer.'
