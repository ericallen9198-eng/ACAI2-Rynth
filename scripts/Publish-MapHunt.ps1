param([string]$Output,[string]$ExpressionLog)
$ErrorActionPreference='Stop'
if($env:OS -ne 'Windows_NT'){throw 'Publish on Windows to embed the native Rynth manifest.'}
$root=Split-Path $PSScriptRoot -Parent
if(-not $Output){$Output=Join-Path $root 'artifacts\maphunt-app'}
Push-Location $root
try {
 python tools/ACAI2.MapHunt/build_app.py
 if($LASTEXITCODE){throw 'App workflow generation failed.'}
 if($ExpressionLog){dotnet run --project tests/ACAI2.Rynth.MapHunt.Tests -c Release -- $ExpressionLog}
 else {dotnet run --project tests/ACAI2.Rynth.MapHunt.Tests -c Release}
 if($LASTEXITCODE){throw 'MapHunt app checks failed.'}
 dotnet publish src/ACAI2.Rynth.MapHunt.Plugin/ACAI2.Rynth.MapHunt.Plugin.csproj -c Release --no-self-contained -o $Output
 if($LASTEXITCODE){throw 'MapHunt app publish failed.'}
 Remove-Item (Join-Path $Output 'cimgui.dll') -Force -ErrorAction SilentlyContinue
 Copy-Item docs/MAPHUNT-APP-SETUP.md $Output
 Copy-Item external/RynthCore/LICENSE (Join-Path $Output 'RynthCore-LICENSE.txt')
 Copy-Item THIRD-PARTY-NOTICES.md $Output
 Copy-Item THIRD-PARTY-NOTICES $Output -Recurse -Force
 New-Item -ItemType Directory (Join-Path $Output 'Compatibility') -Force|Out-Null
 Copy-Item metas/maphunt-app/Compatibility/* (Join-Path $Output 'Compatibility') -Force
 Write-Host "MapHunt app package: $Output"
} finally {Pop-Location}
