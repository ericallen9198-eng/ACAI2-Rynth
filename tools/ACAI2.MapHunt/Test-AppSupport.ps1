param([string]$Package)
$ErrorActionPreference='Stop'
$root=Join-Path ([IO.Path]::GetTempPath()) ('MapHunt-AppSupport-Test-'+[Guid]::NewGuid().ToString('N'))
$oldLocal=$env:LOCALAPPDATA
function Check([bool]$Ok,[string]$Why){if(-not $Ok){throw $Why};Write-Host "PASS: $Why"}
try {
 New-Item -ItemType Directory $root -Force|Out-Null
 $env:LOCALAPPDATA=Join-Path $root 'Local';New-Item -ItemType Directory $env:LOCALAPPDATA -Force|Out-Null
 $installer=Join-Path $Package 'Compatibility\Install-Support.ps1';$dll=Join-Path $root 'active.dll'
 [IO.File]::WriteAllText($dll,'unreviewed active DLL')
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -PluginPath $dll -NoPause
 Check ($LASTEXITCODE -ne 0 -and (Get-Content $dll -Raw) -eq 'unreviewed active DLL') 'unknown active RynthAi is refused without replacement'
 $source=Join-Path $Package 'Compatibility\RynthCore.Plugin.RynthAi.dll';Copy-Item $source $dll -Force
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -PluginPath $dll -NoPause
 Check ($LASTEXITCODE -eq 0) 'already-compatible support is accepted without changes'
 Check (@(Get-ChildItem $env:LOCALAPPDATA -Recurse -Filter restore-info.json).Count -eq 0) 'no unnecessary backup is created for an unchanged DLL'
 $fixture=Join-Path $root 'fixture';New-Item -ItemType Directory $fixture|Out-Null
 Copy-Item $source (Join-Path $fixture 'RynthCore.Plugin.RynthAi.dll')
 [IO.File]::WriteAllText($dll,'reviewed fixture old DLL');$oldHash=(Get-FileHash $dll).Hash
 $text=(Get-Content $installer -Raw).Replace('97a917edf02e58c6938cdb866ca21cf1d1fd996eb103dc593671a2e8802b717c',$oldHash)
 $testScript=Join-Path $fixture 'Install-Support.ps1';[IO.File]::WriteAllText($testScript,$text)
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $testScript -PluginPath $dll -NoPause
 Check ($LASTEXITCODE -eq 0 -and (Get-FileHash $dll).Hash -eq (Get-FileHash $source).Hash) 'compatible fixture receives the exact reviewed support bytes'
 $records=@(Get-ChildItem $env:LOCALAPPDATA -Recurse -Filter restore-info.json)
 Check ($records.Count -eq 1) 'support replacement has exactly one restore record'
 $info=Get-Content $records[0].FullName -Raw|ConvertFrom-Json
 Check ((Get-FileHash $info.Backup).Hash -eq $oldHash) 'old active DLL is backed up before replacement'
 [IO.File]::WriteAllText((Join-Path $fixture 'RynthCore.Plugin.RynthAi.dll'),'tampered')
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $testScript -PluginPath $dll -NoPause
 Check ($LASTEXITCODE -ne 0 -and (Get-FileHash $dll).Hash -eq (Get-FileHash $source).Hash) 'tampered support package is refused without modifying the active DLL'
} finally {$env:LOCALAPPDATA=$oldLocal;if(Test-Path $root){Remove-Item $root -Recurse -Force}}
