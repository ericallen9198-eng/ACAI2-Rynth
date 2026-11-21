param([string]$Installer=(Join-Path $PSScriptRoot 'Update-Mission.ps1'))
$ErrorActionPreference='Stop'
$root=Join-Path ([IO.Path]::GetTempPath()) ('MaraeMission-Test-'+[Guid]::NewGuid().ToString('N'))
$oldLocal=$env:LOCALAPPDATA;$checks=0
function Check([bool]$Pass,[string]$Why) { if(-not $Pass){throw $Why};$script:checks++ }
function Hash([string]$File) { (Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash }
try {
 New-Item -ItemType Directory $root | Out-Null
 $env:LOCALAPPDATA=Join-Path $root 'Local';New-Item -ItemType Directory $env:LOCALAPPDATA | Out-Null
 $dll=Join-Path $root 'active.dll';$meta=Join-Path $root 'active.af';$lua=Join-Path $root 'active.lua'
 [IO.File]::WriteAllText($dll,'original dummy DLL');[IO.File]::WriteAllText($meta,'old meta');[IO.File]::WriteAllText($lua,'old script')
 $originalHash=Hash $dll
 $source=Get-Content $Installer -Raw
 $reviewed='0a98ea8ff556725aa4b19c5818a52e454c6f9d0c8f951ec9f2f7645003ffedf4'
 Check ($source.Contains($reviewed)) 'exact production DLL guard exists'
 $script=Join-Path $root 'fixture-install.ps1';$source.Replace($reviewed,$originalHash) | Set-Content $script -Encoding UTF8
 $sources=@('Mission\MetaFiles\Marae Lassel - Embedded.af','Mission\Scripts\MaraeLassel\index.lua')
 $files=@();$i=0
 foreach($rel in $sources) {
  $path=Join-Path $root $rel;New-Item -ItemType Directory (Split-Path -Parent $path) -Force | Out-Null
  [IO.File]::WriteAllText($path,('dummy replacement '+$i));$i++
  $files+=@{Source=$rel;Sha256=(Hash $path)}
 }
 @{Files=$files}|ConvertTo-Json -Depth 4|Set-Content (Join-Path $root 'checksums.json') -Encoding UTF8
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script -PluginPath $dll -MetaPath $meta -ScriptPath $lua -NoPause
 Check ($LASTEXITCODE -eq 0) 'two-file updater succeeds in Windows PowerShell 5.1'
 $targets=@($meta,$lua)
 for($i=0;$i -lt 2;$i++){Check ((Hash $targets[$i]) -eq $files[$i].Sha256) "replacement $i installed"}
 $records=@(Get-ChildItem $env:LOCALAPPDATA -Recurse -Filter restore-info.json)
 Check ($records.Count -eq 1) 'one transaction backup created'
 $info=Get-Content $records[0].FullName -Raw|ConvertFrom-Json
 Check ($info.Files.Count -eq 2) 'both previous files have restore records'
 Check ((Hash $dll) -eq $originalHash) 'original DLL bytes untouched'
 Check (([IO.File]::ReadAllText($info.Files[0].Backup)) -eq 'old meta') 'previous meta preserved'
 Check (([IO.File]::ReadAllText($info.Files[1].Backup)) -eq 'old script') 'previous panel preserved'
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script -PluginPath $dll -MetaPath $meta -ScriptPath $lua -NoPause
 Check ($LASTEXITCODE -eq 0) 'identical reinstall succeeds'
 Check (@(Get-ChildItem $env:LOCALAPPDATA -Recurse -Filter restore-info.json).Count -eq 1) 'identical reinstall does not replace original backups'
 [IO.File]::WriteAllText($dll,'unreviewed DLL');$unknown=Hash $dll
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script -PluginPath $dll -MetaPath $meta -ScriptPath $lua -NoPause
 Check ($LASTEXITCODE -ne 0) 'unreviewed version refused'
 Check ((Hash $dll) -eq $unknown -and (Hash $meta) -eq $files[0].Sha256 -and (Hash $lua) -eq $files[1].Sha256) 'refusal changes neither installed target'
 [IO.File]::WriteAllText($dll,'original dummy DLL');[IO.File]::WriteAllText((Join-Path $root $sources[1]),'damaged download')
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script -PluginPath $dll -MetaPath $meta -ScriptPath $lua -NoPause
 Check ($LASTEXITCODE -ne 0) 'damaged panel refuses entire install before DLL write'
 Check ((Hash $dll) -eq $originalHash) 'bad source leaves original DLL intact'
 Write-Host "PASS: $checks data-only updater checks with temporary dummy files only."
 $global:LASTEXITCODE=0
} finally {
 $env:LOCALAPPDATA=$oldLocal
 Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
