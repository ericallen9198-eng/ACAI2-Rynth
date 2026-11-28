# Survey installer. Adds its own companion plugin; never replaces any Rynth DLL or character data.
[CmdletBinding()]
param([string]$MetaFolder, [string]$ScriptsFolder, [string]$Destination, [switch]$NoLaunch)
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
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Choose-Folder 'TerrainMesh companion folder' (Join-Path (Split-Path $SuiteRoot -Parent) 'ACAI2-Terrain')
}
$Destination = [IO.Path]::GetFullPath($Destination)
if ($Destination.TrimEnd('\') -eq (Split-Path $MetaFolder -Parent).TrimEnd('\')) { throw 'Use a separate companion folder, not the RynthAi folder.' }
$backupRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('ACAI2-TerrainSurvey\Backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fffffff'))
$files = @(
    @{ Source='MetaFiles\Terrain Access Survey.af'; Target=(Join-Path $MetaFolder 'Terrain Access Survey.af'); Backup='Terrain Access Survey.af' },
    @{ Source='Scripts\TerrainSurvey\index.lua'; Target=(Join-Path $ScriptsFolder 'TerrainSurvey\index.lua'); Backup='index.lua' },
    @{ Source='Scripts\TerrainSurvey\probe-index.json'; Target=(Join-Path $ScriptsFolder 'TerrainSurvey\probe-index.json'); Backup='probe-index.json' },
    @{ Source='Scripts\TerrainSurvey\route-index.json'; Target=(Join-Path $ScriptsFolder 'TerrainSurvey\route-index.json'); Backup='route-index.json' }
)
$pluginSource = Join-Path $PSScriptRoot 'Plugin'
if (-not (Test-Path -LiteralPath (Join-Path $pluginSource 'ACAI2.Rynth.Terrain.Plugin.dll') -PathType Leaf)) { throw 'This package lacks the Windows companion. Download the Windows release ZIP and extract all files.' }
$checks = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'SHA256.json') -Raw | ConvertFrom-Json
foreach ($entry in $checks.PSObject.Properties) {
    $source = Join-Path $PSScriptRoot $entry.Name
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) { throw "Package checksum failed: $($entry.Name)" }
}
foreach ($pluginFile in @(Get-ChildItem -LiteralPath $pluginSource -File)) {
    $files += @{ Source=('Plugin\' + $pluginFile.Name); Target=(Join-Path $Destination $pluginFile.Name); Backup=('Plugin-' + $pluginFile.Name) }
}
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
Write-Host 'TerrainSurvey v0.4.0 and its companion installed and verified. Rynth DLLs and saved character data were not replaced.' -ForegroundColor Green
if (Test-Path -LiteralPath $backupRoot) { Write-Host "Previous survey files: $backupRoot" }
Write-Host 'In game: /lua start TerrainSurvey'
Write-Host 'Existing survey: /terrainsurvey resume (keeps progress)'
Write-Host 'First-ever survey: /terrainsurvey new'
Write-Host 'For reconnect/restart: /lua autostart TerrainSurvey character'

$installedDll = Join-Path $Destination 'ACAI2.Rynth.Terrain.Plugin.dll'
Write-Host 'REQUIRED ONCE: RynthCore > Plugins > Add Plugin, then select:' -ForegroundColor Cyan
Write-Host $installedDll -ForegroundColor Yellow
Write-Host 'Enable ACAI2.Terrain, RynthAi and RynthNav. Keep the other macro controllers stopped.'
if (-not $NoLaunch) {
    try { Set-Clipboard -Value $installedDll } catch { }
    Start-Process explorer.exe -ArgumentList ('/select,"' + $installedDll + '"')
}
