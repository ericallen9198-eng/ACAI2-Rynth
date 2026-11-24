param(
 [string]$RynthAiFolder='C:\Games\RynthSuite\RynthAi',
 [string]$LuaScripts='C:\Games\RynthSuite\RynthLua\Scripts',
 [switch]$InstallCompatibility,
 [switch]$NoPause
)
$ErrorActionPreference='Stop'
try {
 if(Get-Process acclient -ErrorAction SilentlyContinue){throw 'Close all AC clients before installing.'}
 $manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'checksums.json') -Raw|ConvertFrom-Json
 $targets=@(
  @{Source='MetaFiles\MapHunt.af';Target=(Join-Path $RynthAiFolder 'MetaFiles\MapHunt.af')},
  @{Source='Scripts\MapHunt\index.lua';Target=(Join-Path $LuaScripts 'MapHunt\index.lua')}
 )
 if($InstallCompatibility){$targets+=@{Source='Compatibility\RynthCore.Plugin.RynthAi.dll';Target=(Join-Path $RynthAiFolder 'RynthCore.Plugin.RynthAi.dll')}}
 foreach($t in $targets){
  $entry=@($manifest.Files|Where-Object {$_.Source -eq $t.Source})
  if($entry.Count -ne 1 -or $entry[0].Sha256 -notmatch '^[a-fA-F0-9]{64}$'){throw 'Invalid package checksum manifest. Nothing changed.'}
  $t.Path=Join-Path $PSScriptRoot $t.Source
  $t.Hash=(Get-FileHash -LiteralPath $t.Path -Algorithm SHA256).Hash
  if($t.Hash -ne $entry[0].Sha256){throw "Package checksum mismatch: $($t.Source). Nothing changed."}
  if(Test-Path -LiteralPath $t.Target -PathType Container){throw "Target is a directory: $($t.Target). Nothing changed."}
 }
 if($InstallCompatibility){
  $dll=$targets[-1]
  if(-not(Test-Path -LiteralPath $dll.Target -PathType Leaf)){throw 'Active RynthAi DLL was not found. Check -RynthAiFolder. Nothing changed.'}
  $current=(Get-FileHash -LiteralPath $dll.Target -Algorithm SHA256).Hash
  $reviewed=@('9b29173de8a04cba9ec6be3b86006f60923eef75de0949b947e2e6ea4804d9ed','36d9bb4419fe399438a0c6146d6d1286b98b144130ad0ab08d323f4d2efa93e7','b51fa9f3be6cc55004571ade8759087eb273473c9a9c788928dd45812b45f7ea','97a917edf02e58c6938cdb866ca21cf1d1fd996eb103dc593671a2e8802b717c','180158fe41f2a6df33e95ff140e4a78327c2e41d05f531996cb4ecb27f8e0816','0a98ea8ff556725aa4b19c5818a52e454c6f9d0c8f951ec9f2f7645003ffedf4','5aa9ff10b832edcab48d58751563877a4e4935ca9af164dd9613741c553a2f53','911b82784457de6a453478627cf2ab25d543c810a26e1bf555c7eb5b6db5769c','13dbadbb71a57b35a376175c8d79cb616f9699f2efaacaec7f2f1362b8800dd5')
  if($current -ne $dll.Hash -and $current -notin $reviewed){throw 'This RynthAi DLL is not a reviewed compatible build. Nothing changed; use a matching support build rather than overwriting a newer engine plugin.'}
 }
 $backup=Join-Path $env:LOCALAPPDATA ('ACAI2\Backups\MapHunt\'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff'))
 New-Item -ItemType Directory -Path $backup -Force|Out-Null
 $records=@();$i=0
 foreach($t in $targets){
  $exists=Test-Path -LiteralPath $t.Target -PathType Leaf
  $copy=Join-Path $backup ($i.ToString()+'-'+[IO.Path]::GetFileName($t.Target));$i++
  if($exists){
   Copy-Item -LiteralPath $t.Target -Destination $copy
   if((Get-FileHash -LiteralPath $t.Target).Hash -ne (Get-FileHash -LiteralPath $copy).Hash){throw 'Backup verification failed. Nothing replaced.'}
  }
  $records+=@{Target=$t.Target;Existed=$exists;Backup=$copy;NewSha256=$t.Hash}
 }
 @{Files=$records;CreatedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $backup 'restore-info.json') -Encoding UTF8
 try {
  foreach($t in $targets){
   New-Item -ItemType Directory -Path (Split-Path -Parent $t.Target) -Force|Out-Null
   Copy-Item -LiteralPath $t.Path -Destination $t.Target -Force
   if((Get-FileHash -LiteralPath $t.Target).Hash -ne $t.Hash){throw 'Installed checksum mismatch.'}
  }
 } catch {
  $failure=$_
  foreach($r in $records){if($r.Existed){Copy-Item -LiteralPath $r.Backup -Destination $r.Target -Force}elseif(Test-Path -LiteralPath $r.Target){Remove-Item -LiteralPath $r.Target -Force}}
  throw "Installation failed; original files restored: $failure"
 }
 Write-Host "Installed MapHunt meta and helper. Backup: $backup"
 if($InstallCompatibility){Write-Host 'Installed the same reviewed mission API 6 compatibility DLL used by Marae v1.14.'}
 else {Write-Host 'RynthAi DLL was not replaced. MapHunt requires the documented mission API 6 support.'}
 Write-Host 'Put your own maphunt.nav in RynthAi\NavProfiles. Start AC, /lua start MapHunt, then press Start / Resume.'
 Write-Host 'No nav, loot profile, Marae mission, Keyfer or character pvars were replaced.'
} catch {Write-Host $_ -ForegroundColor Red;exit 1}
finally {if(-not $NoPause){Read-Host 'Press Enter to close'|Out-Null}}
