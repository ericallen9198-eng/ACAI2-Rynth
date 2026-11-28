$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $ProjectRoot
try {
    & "$PSScriptRoot\Bootstrap-RynthCore.ps1"
    dotnet build src\ACAI2.Rynth.Plugin\ACAI2.Rynth.Plugin.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
    dotnet run --project tests\ACAI2.Rynth.Tests -c Release -- src\ACAI2.Rynth.Plugin\bin\Release\net10.0-windows\win-x86\ACAI2.Rynth.Plugin.dll
    if ($LASTEXITCODE -ne 0) { throw 'Controlled tests failed.' }
    dotnet run --project tests\ACAI2.KeyferChest.Lua.Tests -c Release -- lua\KeyferChest\index.lua
    if ($LASTEXITCODE -ne 0) { throw 'Keyfer chest Lua checks failed.' }
    dotnet run --project tests\ACAI2.TerrainSurvey.Tests -c Release -- metas\terrain-survey\Scripts\TerrainSurvey\index.lua
    if ($LASTEXITCODE -ne 0) { throw 'Terrain survey Lua checks failed.' }
} finally { Pop-Location }
