param([string]$Output)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Publish on Windows: the upstream manifest requires Win32 UpdateResource.' }
$ProjectRoot = Split-Path $PSScriptRoot -Parent
if (-not $Output) { $Output = Join-Path $ProjectRoot 'artifacts\keyfer-managed' }
Push-Location $ProjectRoot
try {
    & "$PSScriptRoot\Test.ps1"
    dotnet publish src\ACAI2.Rynth.Plugin\ACAI2.Rynth.Plugin.csproj -c Release --no-self-contained -o $Output
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item keyfer.example.json $Output
    Copy-Item docs\KEYFER-SETUP.md $Output
    Copy-Item external\RynthCore\LICENSE (Join-Path $Output 'RynthCore-LICENSE.txt')
    Copy-Item THIRD-PARTY-NOTICES.md $Output
    Copy-Item THIRD-PARTY-NOTICES (Join-Path $Output 'THIRD-PARTY-NOTICES') -Recurse -Force
    Write-Host "Managed Keyfer package: $Output"
    Write-Host 'Requires RynthCore CoreCLR engine API 92+. Add ACAI2.Rynth.Plugin.dll to that launcher; keep this folder together.'
    Write-Host 'No files have been installed into the old ACAI2 tree or the game.'
} finally { Pop-Location }
