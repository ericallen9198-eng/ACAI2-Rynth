param(
    [string]$PluginPath = 'C:\Games\RynthSuite\RynthAi\RynthCore.Plugin.RynthAi.dll',
    [switch]$NoPause
)
$ErrorActionPreference = 'Stop'
try {
    if (Get-Process acclient -ErrorAction SilentlyContinue) { throw 'Close all Asheron''s Call clients before installing this patch.' }
    Write-Host "RynthAi NPC fix target: $PluginPath"
    $PluginPath = $PluginPath.Trim().Trim('"')
    if (-not (Test-Path -LiteralPath $PluginPath -PathType Leaf)) { throw "DLL not found: $PluginPath" }
    $source = Join-Path $PSScriptRoot 'RynthCore.Plugin.RynthAi.dll'
    $expectedNew = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'RynthCore.Plugin.RynthAi.dll.sha256') -Raw).Trim().Split()[0]
    $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if ($sourceHash -ne $expectedNew) { throw 'Downloaded patch checksum mismatch; nothing installed.' }
    $oldHash = (Get-FileHash -LiteralPath $PluginPath -Algorithm SHA256).Hash
    if ($oldHash -eq $sourceHash) { Write-Host 'This patch is already installed.'; return }
    if ($oldHash -ne '97a917edf02e58c6938cdb866ca21cf1d1fd996eb103dc593671a2e8802b717c') {
        throw 'This is not the exact 2026.11.19.1 DLL reviewed for this patch. Nothing changed. Send the current DLL for matching.'
    }
    $backupDir = Join-Path $env:LOCALAPPDATA ('ACAI2\Backups\RynthAi-NPC\' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff'))
    New-Item -ItemType Directory -Path $backupDir -ErrorAction Stop | Out-Null
    $backup = Join-Path $backupDir 'RynthCore.Plugin.RynthAi.dll'
    Copy-Item -LiteralPath $PluginPath -Destination $backup -ErrorAction Stop
    if ((Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash -ne $oldHash) { throw 'Backup verification failed; original not replaced.' }
    @{ InstalledPath=$PluginPath; OriginalSha256=$oldHash; PatchSha256=$sourceHash; Backup=$backup } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backupDir 'restore-info.json') -Encoding UTF8
    try {
        Copy-Item -LiteralPath $source -Destination $PluginPath -Force -ErrorAction Stop
        if ((Get-FileHash -LiteralPath $PluginPath -Algorithm SHA256).Hash -ne $sourceHash) { throw 'Installed checksum mismatch.' }
    } catch {
        Copy-Item -LiteralPath $backup -Destination $PluginPath -Force -ErrorAction Stop
        throw 'Installation failed; original DLL restored.'
    }
    Write-Host "Installed NPC reply patch. Original backup: $backup"
    Write-Host 'No engine, Lua plugin, loot profile, meta, or character settings were replaced by this installer.'
} catch { Write-Host $_ -ForegroundColor Red; exit 1 }
finally { if (-not $NoPause) { Read-Host 'Press Enter to close' | Out-Null } }
