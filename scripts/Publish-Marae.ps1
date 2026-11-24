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
 $support=Join-Path $Output 'Support'
 $plugin=Join-Path $support 'Plugin'
 dotnet publish src/ACAI2.Rynth.Marae.Plugin -c Release --no-self-contained -o $plugin
 if($LASTEXITCODE){throw 'App publish failed.'}
 Remove-Item (Join-Path $plugin 'cimgui.dll') -Force -ErrorAction SilentlyContinue
 Remove-Item (Join-Path $plugin 'ImGui.NET.dll') -Force -ErrorAction SilentlyContinue
 Copy-Item packaging/marae-stock/INSTALL-MARAE.cmd $Output
 Copy-Item packaging/marae-stock/Install-Marae.ps1 $support
 Copy-Item packaging/marae-stock/CREDITS.txt $Output
 python tools/ACAI2.MaraeStock/build_guide.py --output $Output
 if($LASTEXITCODE){throw 'Guide/PDF generation failed.'}
 $notices=Join-Path $support 'Notices'
 New-Item -ItemType Directory $notices -Force|Out-Null
 Copy-Item external/RynthCore/LICENSE (Join-Path $notices 'RynthCore-LICENSE.txt')
 Copy-Item THIRD-PARTY-NOTICES.md $notices
 Copy-Item THIRD-PARTY-NOTICES $notices -Recurse -Force
 $files=@(Get-ChildItem $plugin -File|ForEach-Object{@{Name=$_.Name;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}})
 ConvertTo-Json -InputObject $files -Depth 3|Set-Content (Join-Path $support 'checksums.json') -Encoding UTF8
 if(Test-Path (Join-Path $plugin 'RynthCore.Plugin.RynthAi.dll')){throw 'The normal RynthAi DLL must not ship in this package.'}
 $bytes=[IO.File]::ReadAllBytes((Join-Path $plugin 'ACAI2.Rynth.Marae.Plugin.dll'))
 if(-not [Text.Encoding]::Unicode.GetString($bytes).Contains('RYNTH_PLUGIN_MANIFEST')){throw 'Windows plugin manifest missing.'}
 Write-Host "Marae app: $Output"
} finally {Pop-Location}
