param([string]$Output)
$ErrorActionPreference='Stop'
if($env:OS -ne 'Windows_NT'){throw 'Publish on Windows to embed the native Rynth manifest.'}
$root=Split-Path $PSScriptRoot -Parent
if(-not $Output){$Output=Join-Path $root 'artifacts\marae-stock'}
Push-Location $root
try {
 python tools/ACAI2.MaraeStock/build_stock.py
 if($LASTEXITCODE){throw 'Generation failed.'}
 dotnet run --project tests/ACAI2.MaraeStock.Tests -c Release
 if($LASTEXITCODE){throw 'Marae policy checks failed.'}
 $plugin=Join-Path $Output 'Plugin'
 dotnet publish src/ACAI2.Rynth.Marae.Plugin -c Release --no-self-contained -o $plugin
 if($LASTEXITCODE){throw 'App publish failed.'}
 Remove-Item (Join-Path $plugin 'cimgui.dll') -Force -ErrorAction SilentlyContinue
 Remove-Item (Join-Path $plugin 'ImGui.NET.dll') -Force -ErrorAction SilentlyContinue
 Copy-Item packaging/marae-stock/* $Output
 Copy-Item docs/MARAE-APP-SETUP.md (Join-Path $Output 'README.md')
 Copy-Item external/RynthCore/LICENSE (Join-Path $Output 'RynthCore-LICENSE.txt')
 Copy-Item THIRD-PARTY-NOTICES.md $Output
 Copy-Item THIRD-PARTY-NOTICES $Output -Recurse -Force
 $files=@(Get-ChildItem $plugin -File|ForEach-Object{@{Name=$_.Name;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}})
 ConvertTo-Json -InputObject $files -Depth 3|Set-Content (Join-Path $Output 'checksums.json') -Encoding UTF8
 if(Test-Path (Join-Path $plugin 'RynthCore.Plugin.RynthAi.dll')){throw 'The normal RynthAi DLL must not ship in this package.'}
 $bytes=[IO.File]::ReadAllBytes((Join-Path $plugin 'ACAI2.Rynth.Marae.Plugin.dll'))
 if(-not [Text.Encoding]::Unicode.GetString($bytes).Contains('RYNTH_PLUGIN_MANIFEST')){throw 'Windows plugin manifest missing.'}
 Write-Host "Marae app: $Output"
} finally {Pop-Location}
