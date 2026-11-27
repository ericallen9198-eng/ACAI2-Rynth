param([string]$Output)
$ErrorActionPreference='Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Publish on Windows to embed the native Rynth manifest.' }
$ProjectRoot=Split-Path $PSScriptRoot -Parent
if (-not $Output) { $Output=Join-Path $ProjectRoot 'artifacts\threads-managed' }
Push-Location $ProjectRoot
try {
    dotnet run --project tests\ACAI2.Rynth.Threads.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Thread checks failed.' }
    dotnet publish src\ACAI2.Rynth.Threads.Plugin\ACAI2.Rynth.Threads.Plugin.csproj -c Release --no-self-contained -o $Output
    if ($LASTEXITCODE -ne 0) { throw 'Thread publish failed.' }
    # Bindings resolve the engine-owned cimgui; never ship a second native module.
    Remove-Item (Join-Path $Output "cimgui.dll") -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $Output "ImGui.NET.dll") -Force -ErrorAction SilentlyContinue
    Copy-Item docs\THREADS-SETUP.md $Output
    Copy-Item external\RynthCore\LICENSE (Join-Path $Output 'RynthCore-LICENSE.txt')
    Copy-Item THIRD-PARTY-NOTICES.md $Output
    Copy-Item THIRD-PARTY-NOTICES $Output -Recurse -Force
    New-Item -ItemType Directory -Force -Path (Join-Path $Output 'Support') | Out-Null
    Copy-Item packaging/threads/INSTALL-THREADS.cmd $Output
    Copy-Item packaging/threads/Install-Threads.ps1 (Join-Path $Output 'Support')
    Write-Host "Standalone Thread package: $Output"
} finally { Pop-Location }
