param([string]$Output)
$ErrorActionPreference='Stop'
if($env:OS -ne 'Windows_NT'){throw 'Publish on Windows to embed the Rynth plugin manifest.'}
$root=Split-Path $PSScriptRoot -Parent
if(-not $Output){$Output=Join-Path $root 'artifacts\scrolls'}
Push-Location $root
try {
    dotnet run --project tests/ACAI2.Rynth.Scrolls.Tests -c Release
    if($LASTEXITCODE){throw 'Scroll recipe/controller checks failed'}
    $support=Join-Path $Output 'Support';$plugin=Join-Path $support 'Plugin'
    New-Item -ItemType Directory -Force -Path $plugin | Out-Null
    dotnet publish src/ACAI2.Rynth.Scrolls.Plugin -c Release --no-self-contained -o $plugin
    if($LASTEXITCODE){throw 'Scroll plugin publish failed'}
    Remove-Item (Join-Path $plugin 'cimgui.dll') -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $plugin 'ImGui.NET.dll') -ErrorAction SilentlyContinue
    Copy-Item packaging/scrolls/INSTALL-SCROLLS.cmd $Output
    Copy-Item packaging/scrolls/Install-Scrolls.ps1 $support
    Copy-Item docs/SCROLLS-SETUP.md (Join-Path $Output 'README.md')
    Copy-Item src/ACAI2.Rynth.Scrolls.Core/catalog.json (Join-Path $Output 'RECIPE-CATALOG.json')
    Copy-Item external/RynthCore/LICENSE (Join-Path $Output 'RynthCore-LICENSE.txt')
    Copy-Item THIRD-PARTY-NOTICES.md $Output
    Copy-Item THIRD-PARTY-NOTICES $Output -Recurse -Force
    Copy-Item THIRD-PARTY-NOTICES $Output -Recurse -Force
} finally { Pop-Location }
