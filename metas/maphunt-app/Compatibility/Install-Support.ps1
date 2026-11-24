param([string]$PluginPath='C:\Games\RynthSuite\RynthAi\RynthCore.Plugin.RynthAi.dll',[switch]$NoPause)
$ErrorActionPreference='Stop'
try {
 if(Get-Process acclient -ErrorAction SilentlyContinue){throw 'Close all AC clients first.'}
 $source=Join-Path $PSScriptRoot 'RynthCore.Plugin.RynthAi.dll'
 $expected='9b29173de8a04cba9ec6be3b86006f60923eef75de0949b947e2e6ea4804d9ed'
 if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $expected){throw 'Support package checksum mismatch. Nothing changed.'}
 if(-not(Test-Path -LiteralPath $PluginPath -PathType Leaf)){throw "Active RynthAi DLL not found: $PluginPath. Ask for help locating the active plugin. Nothing changed."}
 $current=(Get-FileHash -LiteralPath $PluginPath -Algorithm SHA256).Hash
 if($current -eq $expected){Write-Host 'Compatible support is already installed. Nothing changed.';return}
 $reviewed=@('36d9bb4419fe399438a0c6146d6d1286b98b144130ad0ab08d323f4d2efa93e7','b51fa9f3be6cc55004571ade8759087eb273473c9a9c788928dd45812b45f7ea','97a917edf02e58c6938cdb866ca21cf1d1fd996eb103dc593671a2e8802b717c','180158fe41f2a6df33e95ff140e4a78327c2e41d05f531996cb4ecb27f8e0816','0a98ea8ff556725aa4b19c5818a52e454c6f9d0c8f951ec9f2f7645003ffedf4','5aa9ff10b832edcab48d58751563877a4e4935ca9af164dd9613741c553a2f53','911b82784457de6a453478627cf2ab25d543c810a26e1bf555c7eb5b6db5769c','13dbadbb71a57b35a376175c8d79cb616f9699f2efaacaec7f2f1362b8800dd5')
 if($current -notin $reviewed){throw 'This RynthAi DLL is not a reviewed compatible build. Nothing changed. Send the exact message for help; do not bypass the check.'}
 $backup=Join-Path $env:LOCALAPPDATA ('ACAI2\Backups\MapHuntApp\'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff'))
 New-Item -ItemType Directory $backup -Force|Out-Null
 $old=Join-Path $backup 'RynthCore.Plugin.RynthAi.dll';Copy-Item -LiteralPath $PluginPath -Destination $old
 if((Get-FileHash -LiteralPath $old).Hash -ne $current){throw 'Backup verification failed. Nothing replaced.'}
 @{Target=$PluginPath;Backup=$old;PreviousSha256=$current}|ConvertTo-Json|Set-Content (Join-Path $backup 'restore-info.json') -Encoding UTF8
 try {Copy-Item -LiteralPath $source -Destination $PluginPath -Force;if((Get-FileHash -LiteralPath $PluginPath).Hash -ne $expected){throw 'Installed checksum mismatch.'}}
 catch {Copy-Item -LiteralPath $old -Destination $PluginPath -Force;throw 'Installation failed; the original DLL was restored.'}
 Write-Host "Installed reviewed mission API 6 support. Backup: $backup"
 Write-Host 'No meta, Lua script, nav, loot profile or character setting was installed or replaced.'
} catch {Write-Host $_ -ForegroundColor Red;exit 1}
finally {if(-not $NoPause){Read-Host 'Press Enter to close'|Out-Null}}
