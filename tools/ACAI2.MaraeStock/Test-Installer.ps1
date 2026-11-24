param([Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference='Stop'
$work=Join-Path $env:TEMP ('Marae-App-Installer-Test-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $work|Out-Null
try {
 $dest=Join-Path $work 'App';$ai=Join-Path $work 'RynthAi'
 New-Item -ItemType Directory $ai|Out-Null
 $dll=Join-Path $ai 'RynthCore.Plugin.RynthAi.dll';[IO.File]::WriteAllText($dll,'unchanged-stock-engine-test-fixture')
 $before=(Get-FileHash $dll).Hash
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$Package/Install-Marae.ps1" -Destination $dest -RynthAiFolder $ai -NoPause
 if($LASTEXITCODE){throw 'Installer failed.'}
 if(-not(Test-Path "$dest/ACAI2.Rynth.Marae.Plugin.dll")){throw 'App DLL missing.'}
 if((Get-FileHash $dll).Hash -ne $before){throw 'Installer changed RynthAi.'}
 if(Test-Path "$dest/RynthCore.Plugin.RynthAi.dll"){throw 'Installer copied the engine into app folder.'}
 # A normal second installation must back up an existing app DLL before overwriting.
 [IO.File]::WriteAllText("$dest/ACAI2.Rynth.Marae.Plugin.dll",'old-app-test-fixture')
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$Package/Install-Marae.ps1" -Destination $dest -RynthAiFolder $ai -NoPause
 if($LASTEXITCODE){throw 'Update installer failed.'}
 $backups=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ACAI2\Backups\Marae-App'
 if(-not(Get-ChildItem $backups -Filter ACAI2.Rynth.Marae.Plugin.dll -Recurse|Where-Object{[IO.File]::ReadAllText($_.FullName) -eq 'old-app-test-fixture'})){throw 'Previous app DLL not backed up.'}
 $bad=Join-Path $work 'Tampered';Copy-Item $Package $bad -Recurse
 [IO.File]::AppendAllText("$bad/Plugin/ACAI2.Rynth.Marae.Plugin.dll",'tamper')
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$bad/Install-Marae.ps1" -Destination (Join-Path $work 'Rejected') -RynthAiFolder $ai -NoPause *> $null
 if($LASTEXITCODE -eq 0){throw 'Tampered payload was accepted.'}
 if(Test-Path (Join-Path $work 'Rejected')){throw 'Tampered package wrote files before validation.'}
 if((Get-FileHash $dll).Hash -ne $before){throw 'RynthAi changed during installer checks.'}
 $global:LASTEXITCODE=0
 Write-Host 'PASS: Windows PowerShell 5.1 installation, update backup, payload rejection, and unchanged stock RynthAi.'
} finally {Remove-Item $work -Recurse -Force}
