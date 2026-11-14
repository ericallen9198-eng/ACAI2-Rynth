param([string]$Output)
$ErrorActionPreference='Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Publish on Windows to embed the native Rynth manifest.' }
$ProjectRoot=Split-Path $PSScriptRoot -Parent
if (-not $Output) { $Output=Join-Path $ProjectRoot 'artifacts\combat-managed' }
Push-Location $ProjectRoot
try {
    dotnet build src\ACAI2.Rynth.Combat.Plugin -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Combat build failed.' }
    dotnet run --project tests\ACAI2.Rynth.Combat.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Combat checks failed.' }
    dotnet publish src\ACAI2.Rynth.Combat.Plugin -c Release --no-self-contained -o $Output
    if ($LASTEXITCODE -ne 0) { throw 'Combat publish failed.' }
    dotnet run --project tools\ACAI2.Combat.DesignPreview -c Release -- $Output
    if ($LASTEXITCODE -ne 0) { throw 'Shared vector preview export failed.' }
    Remove-Item (Join-Path $Output 'cimgui.dll') -Force -ErrorAction SilentlyContinue
    Copy-Item docs\COMBAT-SETUP.md $Output
    Copy-Item external\RynthCore\LICENSE (Join-Path $Output 'RynthCore-LICENSE.txt')
    Copy-Item THIRD-PARTY-NOTICES.md $Output
    Copy-Item THIRD-PARTY-NOTICES $Output -Recurse -Force
    Write-Host "Standalone Combat package: $Output"
} finally { Pop-Location }
