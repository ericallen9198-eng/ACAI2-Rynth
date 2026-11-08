$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $ProjectRoot
try {
    & "$PSScriptRoot\Bootstrap-RynthCore.ps1"
    dotnet build src\ACAI2.Rynth.Plugin\ACAI2.Rynth.Plugin.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
    dotnet run --project tests\ACAI2.Rynth.Tests -c Release -- src\ACAI2.Rynth.Plugin\bin\Release\net10.0-windows\win-x86\ACAI2.Rynth.Plugin.dll
    if ($LASTEXITCODE -ne 0) { throw 'Controlled tests failed.' }
} finally { Pop-Location }
