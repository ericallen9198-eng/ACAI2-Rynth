param([Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference='Stop'
$work=Join-Path $env:TEMP ('Marae App Installer É Test '+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $work|Out-Null
$drive=$null
function Run-Installer([string]$Target,[string]$Ai,[string]$Pkg=$Package){
 $args=@('-NoProfile','-ExecutionPolicy','Bypass','-File',"$Pkg/Support/Install-Marae.ps1",'-Destination',$Target,'-NoPause','-NoLaunch','-DiscoveryFolder',(Join-Path $work 'Discovery'))
 if($Ai){$args+=@('-RynthAiFolder',$Ai)}
 & powershell.exe @args
 if($LASTEXITCODE){throw 'Installer failed.'}
}
try {
 $dest=Join-Path $work 'App';$ai=Join-Path $work 'Rynth Suite\RynthAi'
 New-Item -ItemType Directory (Join-Path $ai 'MetaFiles') -Force|Out-Null
 New-Item -ItemType Directory (Join-Path $ai 'NavProfiles') -Force|Out-Null
 $dll=Join-Path $ai 'RynthCore.Plugin.RynthAi.dll';[IO.File]::WriteAllText($dll,'unchanged-stock-engine-test-fixture')
 $before=(Get-FileHash -LiteralPath $dll).Hash
 Run-Installer $dest $ai
 if(-not(Test-Path "$dest/ACAI2.Rynth.Marae.Plugin.dll")){throw 'App DLL missing.'}
 $hint=Get-Content "$dest/installation.json" -Raw|ConvertFrom-Json
 $sharedHint=Get-Content (Join-Path $work 'Discovery\installation.json') -Raw|ConvertFrom-Json
 if($sharedHint.PluginFolder -ne $dest -or $sharedHint.RynthAiFolder -ne $ai){throw 'Stable stream-load discovery hint missing.'}
 if($hint.RynthAiFolder -ne $ai){throw 'Custom RynthAi folder not remembered.'}
 if(-not(Test-Path "$dest/Marae-Quick-Start.pdf")){throw 'Installed guide missing.'}
 if([IO.File]::ReadAllText("$dest/PLUGIN-PATH.txt") -ne "$dest\ACAI2.Rynth.Marae.Plugin.dll"){throw 'Saved plugin path wrong.'}
 if((Get-FileHash -LiteralPath $dll).Hash -ne $before){throw 'Installer changed RynthAi.'}
 if(Test-Path "$dest/RynthCore.Plugin.RynthAi.dll"){throw 'Installer copied the engine into app folder.'}
 # The suite root is also accepted; no typing of a RynthAi suffix is necessary.
 Run-Installer (Join-Path $work 'SuiteSelected') (Split-Path $ai -Parent)
 # Auto-detect the same environment override used by Rynth, without drive assumptions.
 $oldSuite=$env:RYNTHSUITE_DIR
 try{$env:RYNTHSUITE_DIR=Split-Path $ai -Parent;Run-Installer (Join-Path $work 'AutoDetected') ''}finally{$env:RYNTHSUITE_DIR=$oldSuite}
 # Exercise a real non-C DOS drive, not a PowerShell-only provider drive.
 foreach($letter in @('Z','Y','X','W')){if(-not(Test-Path ($letter+':\'))){$drive=$letter+':';break}}
 if(-not $drive){throw 'No free drive letter for custom-drive check.'}
 & subst.exe $drive $work
 if($LASTEXITCODE){throw 'Could not create the temporary custom drive.'}
 Run-Installer ($drive+'\Installed app') ($drive+'\Rynth Suite\RynthAi')
 if(-not(Test-Path ($drive+'\Installed app\ACAI2.Rynth.Marae.Plugin.dll'))){throw 'Non-C drive installation failed.'}
 & subst.exe $drive /D;$drive=$null
 # Back up before replacing an existing app DLL.
 [IO.File]::WriteAllText("$dest/ACAI2.Rynth.Marae.Plugin.dll",'old-app-test-fixture')
 Run-Installer $dest $ai
 $backups=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ACAI2\Backups\Marae-App'
 if(-not(Get-ChildItem $backups -Filter ACAI2.Rynth.Marae.Plugin.dll -Recurse|Where-Object{[IO.File]::ReadAllText($_.FullName) -eq 'old-app-test-fixture'})){throw 'Previous app DLL not backed up.'}
 $bad=Join-Path $work 'Tampered';Copy-Item $Package $bad -Recurse
 [IO.File]::AppendAllText("$bad/Support/Plugin/ACAI2.Rynth.Marae.Plugin.dll",'tamper')
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$bad/Support/Install-Marae.ps1" -Destination (Join-Path $work 'Rejected') -RynthAiFolder $ai -NoPause -NoLaunch -DiscoveryFolder (Join-Path $work 'Discovery') *> $null
 if($LASTEXITCODE -eq 0){throw 'Tampered payload was accepted.'}
 if(Test-Path (Join-Path $work 'Rejected')){throw 'Tampered package wrote files before validation.'}
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$Package/Support/Install-Marae.ps1" -Destination (Join-Path $work 'BadPath') -RynthAiFolder $work -NoPause -NoLaunch -DiscoveryFolder (Join-Path $work 'Discovery') *> $null
 if($LASTEXITCODE -eq 0 -or (Test-Path (Join-Path $work 'BadPath'))){throw 'Invalid RynthAi folder was accepted.'}
 if((Get-FileHash -LiteralPath $dll).Hash -ne $before){throw 'RynthAi changed during installer checks.'}
 $global:LASTEXITCODE=0
 Write-Host 'PASS: PowerShell 5.1 installer: spaces, Unicode, non-C drive, environment discovery, suite-root selection, saved paths/PDF, backups, invalid payload/path refusal, and unchanged RynthAi.'
} finally {if($drive){& subst.exe $drive /D};Remove-Item -LiteralPath $work -Recurse -Force}
