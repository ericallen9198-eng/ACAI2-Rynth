param([Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference='Stop'
$work=Join-Path $env:TEMP ('Terrain Installer Test '+[guid]::NewGuid().ToString('N'))
try {
 $ai=Join-Path $work 'Custom Drive\Rynth Suite\RynthAi'
 $meta=Join-Path $ai 'MetaFiles';$lua=Join-Path $work 'Elsewhere\RynthLua\Scripts';$dest=Join-Path $work 'Companion'
 New-Item -ItemType Directory $meta -Force|Out-Null
 New-Item -ItemType Directory (Split-Path $lua -Parent) -Force|Out-Null
 $engine=Join-Path $ai 'RynthCore.Plugin.RynthAi.dll';'unchanged stock DLL fixture'|Set-Content $engine
 $hash=(Get-FileHash $engine).Hash
 $data=Join-Path (Split-Path $lua -Parent) 'Data\TerrainSurvey'
 New-Item -ItemType Directory $data -Force|Out-Null
 'keep this progress'|Set-Content (Join-Path $data 'checkpoint-fixture.json')
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$Package/Install-TerrainSurvey.ps1" -MetaFolder $meta -ScriptsFolder $lua -Destination $dest -NoLaunch
 if($LASTEXITCODE){throw 'Installer failed'}
 foreach($file in @((Join-Path $meta 'Terrain Access Survey.af'),(Join-Path $lua 'TerrainSurvey\index.lua'),(Join-Path $dest 'ACAI2.Rynth.Terrain.Plugin.dll'))){if(-not(Test-Path $file)){throw "Missing $file"}}
 if((Get-FileHash $engine).Hash -ne $hash){throw 'Stock DLL changed'}
 if((Get-Content (Join-Path $data 'checkpoint-fixture.json') -Raw).Trim() -ne 'keep this progress'){throw 'Progress changed'}
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$Package/Install-TerrainSurvey.ps1" -MetaFolder $meta -ScriptsFolder $lua -Destination $dest -NoLaunch
 if($LASTEXITCODE){throw 'Repeat install failed'}
 Write-Host 'PASS Windows installer: custom paths/spaces, first and repeat installation; engine DLL and progress unchanged.'
} finally {if(Test-Path $work){Remove-Item $work -Recurse -Force}}
