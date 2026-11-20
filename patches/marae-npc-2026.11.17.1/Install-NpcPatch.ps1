param([string]$PluginPath)
$ErrorActionPreference = 'Stop'
try {
    if (Get-Process acclient -ErrorAction SilentlyContinue) { throw 'Close all Asheron''s Call clients before installing this patch.' }
    if (-not $PluginPath) {
        $PluginPath = Read-Host 'Paste the FULL RynthAi DLL path shown in RynthCore Plugins (Enter for C:\Games\RynthSuite\RynthAi\RynthCore.Plugin.RynthAi.dll)'
        if (-not $PluginPath) { $PluginPath = 'C:\Games\RynthSuite\RynthAi\RynthCore.Plugin.RynthAi.dll' }
    }
    $PluginPath = $PluginPath.Trim().Trim('"')
    if (-not (Test-Path -LiteralPath $PluginPath -PathType Leaf)) { throw "DLL not found: $PluginPath" }
    $source = Join-Path $PSScriptRoot 'RynthCore.Plugin.RynthAi.dll'
    $expectedNew = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'RynthCore.Plugin.RynthAi.dll.sha256') -Raw).Trim().Split()[0]
    $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if ($sourceHash -ne $expectedNew) { throw 'Downloaded patch checksum mismatch; nothing installed.' }
    $oldHash = (Get-FileHash -LiteralPath $PluginPath -Algorithm SHA256).Hash
    if ($oldHash -eq $sourceHash) { Write-Host 'This patch is already installed.'; return }
    if ($oldHash -ne '84eb07ef0d31e026351af092d227cf136cdf6557fb23a8df8dfb1ac60b318102') {
        throw 'This is not the exact 2026.11.17.1 DLL reviewed for this patch. Nothing changed. Send the current DLL for matching.'
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
finally { if (-not $PluginPath -or $Host.Name -eq 'ConsoleHost') { Read-Host 'Press Enter to close' | Out-Null } }
