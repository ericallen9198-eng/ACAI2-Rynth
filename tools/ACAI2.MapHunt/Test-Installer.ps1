param([string]$Package)
$ErrorActionPreference='Stop'
$root=Join-Path ([IO.Path]::GetTempPath()) ('MapHunt-test-'+[Guid]::NewGuid().ToString('N'))
$oldLocal=$env:LOCALAPPDATA
function Check([bool]$Ok,[string]$Why){if(-not $Ok){throw $Why};Write-Host "PASS: $Why"}
try {
 $ai=Join-Path $root 'RynthAi';$lua=Join-Path $root 'Scripts';$env:LOCALAPPDATA=Join-Path $root 'Local'
 New-Item -ItemType Directory -Path $ai,$lua,$env:LOCALAPPDATA -Force|Out-Null
 $dll=Join-Path $ai 'RynthCore.Plugin.RynthAi.dll';[IO.File]::WriteAllText($dll,'unreviewed installed DLL')
 $nav=Join-Path $ai 'NavProfiles\maphunt.nav';New-Item -ItemType Directory (Split-Path $nav) -Force|Out-Null;[IO.File]::WriteAllText($nav,'my nav')
 $installer=Join-Path $Package 'Install-MapHunt.ps1'
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -RynthAiFolder $ai -LuaScripts $lua -NoPause
 Check ($LASTEXITCODE -eq 0) 'meta/helper-only installation works without modifying an unknown DLL'
 Check ((Get-Content $dll -Raw) -eq 'unreviewed installed DLL') 'default installer leaves engine plugin alone'
 Check ((Get-Content $nav -Raw) -eq 'my nav') 'user nav is never replaced'
 Check (Test-Path (Join-Path $ai 'MetaFiles\MapHunt.af')) 'meta installed in MetaFiles'
 Check (Test-Path (Join-Path $lua 'MapHunt\index.lua')) 'Lua script installed inside the named folder'
 $meta=Join-Path $ai 'MetaFiles\MapHunt.af';[IO.File]::WriteAllText($meta,'old meta')
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -RynthAiFolder $ai -LuaScripts $lua -InstallCompatibility -NoPause
 Check ($LASTEXITCODE -ne 0) 'unreviewed DLL refuses compatibility installation'
 Check ((Get-Content $meta -Raw) -eq 'old meta') 'compatibility refusal happens before any target replacement'
 Copy-Item (Join-Path $Package 'Compatibility\RynthCore.Plugin.RynthAi.dll') $dll -Force
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -RynthAiFolder $ai -LuaScripts $lua -InstallCompatibility -NoPause
 Check ($LASTEXITCODE -eq 0) 'reviewed compatibility installation succeeds'
 $records=@(Get-ChildItem $env:LOCALAPPDATA -Recurse -Filter restore-info.json)
 Check ($records.Count -eq 2) 'successful transactions have restore records, refusal creates none'
 $found=@($records|ForEach-Object{(Get-Content $_.FullName -Raw|ConvertFrom-Json).Files}|Where-Object{$_.Target -eq $meta -and $_.Existed})
 Check ($found.Count -eq 1 -and (Get-Content $found[0].Backup -Raw) -eq 'old meta') 'previous meta is backed up before replacement'
} finally {$env:LOCALAPPDATA=$oldLocal;if(Test-Path $root){Remove-Item $root -Recurse -Force}}
