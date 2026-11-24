param([string]$Destination='C:\Games\ACAI2-Rynth-Marae',[string]$RynthAiFolder='C:\Games\RynthSuite\RynthAi',[switch]$NoPause)
$ErrorActionPreference='Stop'
try {
 $payload=Join-Path $PSScriptRoot 'Plugin'
 if(-not(Test-Path (Join-Path $payload 'ACAI2.Rynth.Marae.Plugin.dll'))){throw 'Extract the entire ZIP before running this installer.'}
 $expected=Get-Content (Join-Path $PSScriptRoot 'checksums.json') -Raw|ConvertFrom-Json
 foreach($entry in $expected){
  $source=Join-Path $payload $entry.Name
  if(-not(Test-Path $source) -or (Get-FileHash $source -Algorithm SHA256).Hash -ne $entry.Sha256){throw "Package checksum mismatch: $($entry.Name)"}
 }
 $engine=Join-Path $RynthAiFolder 'RynthCore.Plugin.RynthAi.dll'
 $before=if(Test-Path $engine){(Get-FileHash $engine -Algorithm SHA256).Hash}else{$null}
 $backup=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('ACAI2\Backups\Marae-App\'+(Get-Date -Format 'yyyyMMdd-HHmmss-fffffff'))
 New-Item -ItemType Directory $Destination -Force|Out-Null
 foreach($entry in $expected){
  $target=Join-Path $Destination $entry.Name
  if(Test-Path $target){New-Item -ItemType Directory $backup -Force|Out-Null;Copy-Item $target (Join-Path $backup $entry.Name)}
  Copy-Item (Join-Path $payload $entry.Name) $target -Force
 }
 if($before -and (Get-FileHash $engine -Algorithm SHA256).Hash -ne $before){throw 'The RynthAi DLL changed during installation. This installer does not write to it.'}
 Write-Host "Installed the Marae app to $Destination" -ForegroundColor Green
 Write-Host 'No RynthAi DLL, loot profile, character settings, or Lua script was replaced.'
 if(Test-Path $backup){Write-Host "Previous app files backed up to $backup"}
 Write-Host 'Next: enable ACAI2.Rynth.Marae.Plugin.dll in RynthCore Plugins, then type /acai2marae in game.'
 Write-Host 'Read README.md for the first-run options and old Lua shutdown commands.'
} catch {
 Write-Host $_.Exception.Message -ForegroundColor Red
 if(-not $NoPause){[void](Read-Host 'Press Enter to close')}
 throw
}
if(-not $NoPause){[void](Read-Host 'Press Enter to close')}
