# File-only installer. No Rynth DLL, loot profile, app or runtime data is replaced.
[CmdletBinding()]
param([string]$MetaFolder, [string]$ScriptsFolder)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Choose-Folder([string]$Prompt, [string]$Default) {
    $answer = Read-Host "$Prompt (Enter for $Default)"
    if ([string]::IsNullOrWhiteSpace($answer)) { return $Default }
    return $answer.Trim().Trim('"')
}
$SuiteRoot = 'C:\Games\RynthSuite'
foreach ($drive in @(Get-PSDrive -PSProvider FileSystem)) {
    $candidate = Join-Path $drive.Root 'Games\RynthSuite'
    if (Test-Path -LiteralPath (Join-Path $candidate 'RynthAi') -PathType Container) { $SuiteRoot = $candidate; break }
}
if ([string]::IsNullOrWhiteSpace($MetaFolder)) {
    $MetaFolder = Choose-Folder 'Your active RynthAi MetaFiles folder' (Join-Path $SuiteRoot 'RynthAi\MetaFiles')
}
if ([string]::IsNullOrWhiteSpace($ScriptsFolder)) {
    $ScriptsFolder = Choose-Folder 'Your RynthLua Scripts folder' (Join-Path $SuiteRoot 'RynthLua\Scripts')
}
$MetaFolder = [IO.Path]::GetFullPath($MetaFolder)
$ScriptsFolder = [IO.Path]::GetFullPath($ScriptsFolder)
# Avoid silently installing into a typo. The actual parent plugin folders must exist.
foreach ($folder in @($MetaFolder, $ScriptsFolder)) {
    $parent = Split-Path -Parent $folder
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw "Parent folder does not exist: $parent. Check the selected installation paths." }
}
$backupRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('ACAI2-TerrainSurvey\Backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fffffff'))
$files = @(
    @{ Source='MetaFiles\Terrain Access Survey.af'; Target=(Join-Path $MetaFolder 'Terrain Access Survey.af'); Backup='Terrain Access Survey.af' },
    @{ Source='Scripts\TerrainSurvey\index.lua'; Target=(Join-Path $ScriptsFolder 'TerrainSurvey\index.lua'); Backup='index.lua' },
    @{ Source='Scripts\TerrainSurvey\probe-index.json'; Target=(Join-Path $ScriptsFolder 'TerrainSurvey\probe-index.json'); Backup='probe-index.json' }
)
foreach ($file in $files) {
    $source = Join-Path $PSScriptRoot $file.Source
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Incomplete extracted ZIP: missing $source" }
}
Write-Host 'Stop the Marae run and any running TerrainSurvey script before installing.' -ForegroundColor Cyan
Write-Host "Meta: $MetaFolder"
Write-Host "Lua:  $ScriptsFolder\TerrainSurvey"
foreach ($file in $files) {
    $source = Join-Path $PSScriptRoot $file.Source
    if (Test-Path -LiteralPath $file.Target -PathType Leaf) {
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        Copy-Item -LiteralPath $file.Target -Destination (Join-Path $backupRoot $file.Backup)
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $file.Target) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $file.Target -Force
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $file.Target -Algorithm SHA256).Hash) { throw "Installed file verification failed: $($file.Target)" }
}
Write-Host 'TerrainSurvey v0.3.2 installed and verified. No DLLs or saved character data were changed.' -ForegroundColor Green
if (Test-Path -LiteralPath $backupRoot) { Write-Host "Previous survey files: $backupRoot" }
Write-Host 'In game: /lua start TerrainSurvey'
Write-Host 'Then:    /terrainsurvey new'
Write-Host 'For reconnect/restart: /lua autostart TerrainSurvey character'
