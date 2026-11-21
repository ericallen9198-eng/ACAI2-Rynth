param(
 [string]$PluginPath='C:\Games\RynthSuite\RynthAi\RynthCore.Plugin.RynthAi.dll',
 [string]$MetaPath='C:\Games\RynthSuite\RynthAi\MetaFiles\Marae Lassel - Embedded.af',
 [string]$ScriptPath='C:\Games\RynthSuite\RynthLua\Scripts\MaraeLassel\index.lua',
 [switch]$NoPause
)
$ErrorActionPreference='Stop'
try {
 if (Get-Process acclient -ErrorAction SilentlyContinue) { throw 'Close all AC clients first.' }
 if (-not (Test-Path -LiteralPath $PluginPath -PathType Leaf)) { throw "Active RynthAi DLL not found: $PluginPath" }
 $manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'checksums.json') -Raw | ConvertFrom-Json
 $targets=@(
  @{Source='RynthCore.Plugin.RynthAi.dll';Target=$PluginPath},
  @{Source='Mission\MetaFiles\Marae Lassel - Embedded.af';Target=$MetaPath},
  @{Source='Mission\Scripts\MaraeLassel\index.lua';Target=$ScriptPath}
 )
 if (@($manifest.files).Count -ne 3) { throw 'Invalid package manifest; nothing changed.' }
 foreach($t in $targets) {
  $entry=@($manifest.files | Where-Object { $_.Source -eq $t.Source })
  if ($entry.Count -ne 1 -or $entry[0].Sha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'Invalid package checksum entry.' }
  $t.Path=Join-Path $PSScriptRoot $t.Source
  $t.Hash=(Get-FileHash -LiteralPath $t.Path -Algorithm SHA256).Hash
  if ($t.Hash -ne $entry[0].Sha256) { throw "Package checksum mismatch: $($t.Source). Nothing changed." }
 }
 $current=(Get-FileHash -LiteralPath $PluginPath -Algorithm SHA256).Hash
 $reviewed=@('97a917edf02e58c6938cdb866ca21cf1d1fd996eb103dc593671a2e8802b717c','180158fe41f2a6df33e95ff140e4a78327c2e41d05f531996cb4ecb27f8e0816','0a98ea8ff556725aa4b19c5818a52e454c6f9d0c8f951ec9f2f7645003ffedf4','5aa9ff10b832edcab48d58751563877a4e4935ca9af164dd9613741c553a2f53','911b82784457de6a453478627cf2ab25d543c810a26e1bf555c7eb5b6db5769c')
 if ($current -ne $targets[0].Hash -and $current -notin $reviewed) { throw 'This DLL differs from the reviewed November 19 original/NPC patch. Nothing changed; send the current DLL for matching.' }
 $allMatch=$true
 foreach($t in $targets) {
  if (Test-Path -LiteralPath $t.Target -PathType Container) { throw "Target is a folder, not a file: $($t.Target)" }
  if (-not (Test-Path -LiteralPath $t.Target -PathType Leaf) -or (Get-FileHash -LiteralPath $t.Target -Algorithm SHA256).Hash -ne $t.Hash) { $allMatch=$false }
 }
 if ($allMatch) { Write-Host 'Marae mission is already installed; no files changed.'; return }
 $backupDir=Join-Path $env:LOCALAPPDATA ('ACAI2\Backups\MaraeMission\'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff'))
 New-Item -ItemType Directory -Path $backupDir | Out-Null
 $records=@();$i=0
 foreach($t in $targets) {
  $exists=Test-Path -LiteralPath $t.Target -PathType Leaf
  $backup=Join-Path $backupDir (($i.ToString())+'-'+[IO.Path]::GetFileName($t.Target));$i++
  if ($exists) {
   Copy-Item -LiteralPath $t.Target -Destination $backup
   if ((Get-FileHash -LiteralPath $t.Target -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash) { throw 'Backup verification failed; nothing replaced.' }
  }
  $records+=@{Target=$t.Target;Existed=$exists;Backup=$backup;NewSha256=$t.Hash}
 }
 @{Files=$records;CreatedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $backupDir 'restore-info.json') -Encoding UTF8
 try {
  foreach($t in $targets) {
   New-Item -ItemType Directory -Path (Split-Path -Parent $t.Target) -Force | Out-Null
   Copy-Item -LiteralPath $t.Path -Destination $t.Target -Force
   if ((Get-FileHash -LiteralPath $t.Target -Algorithm SHA256).Hash -ne $t.Hash) { throw "Installed checksum mismatch: $($t.Target)" }
  }
 } catch {
  $installError=$_
  foreach($r in $records) {
   if($r.Existed) { Copy-Item -LiteralPath $r.Backup -Destination $r.Target -Force }
   elseif(Test-Path -LiteralPath $r.Target) { Remove-Item -LiteralPath $r.Target -Force }
  }
  throw "Installation failed; previous files restored: $installError"
 }
 Write-Host 'Installed Marae mission: RynthAi compatibility DLL, embedded meta, and Lua panel.'
 Write-Host "Backup: $backupDir"
 Write-Host 'Start AC, then type /lua start MaraeLassel and press Resume run in the panel (or Start run if no saved mission).'
} catch { Write-Host $_ -ForegroundColor Red;exit 1 }
finally { if(-not $NoPause) { Read-Host 'Press Enter to close' | Out-Null } }
