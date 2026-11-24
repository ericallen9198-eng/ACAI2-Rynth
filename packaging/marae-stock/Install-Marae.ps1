param([string]$Destination,[string]$RynthAiFolder,[switch]$NoPause,[switch]$NoLaunch,[string]$DiscoveryFolder)
$ErrorActionPreference='Stop'
# Defaults follow this Windows user's actual location; there is no fixed drive letter.
if(-not $DiscoveryFolder){$DiscoveryFolder=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ACAI2\Marae'}
if(-not $Destination){$Destination=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ACAI2\Marae'}
function Get-DataRoot([string]$Path) {
 if(-not $Path){return $null}
 $path=[Environment]::ExpandEnvironmentVariables($Path.Trim().Trim('"'))
 if(-not [IO.Path]::IsPathRooted($path)){return $null}
 foreach($candidate in @($path,(Join-Path $path 'RynthAi'))){
  if((Test-Path -LiteralPath $candidate -PathType Container) -and (@('MetaFiles','NavProfiles','SettingsProfiles','LootProfiles')|Where-Object{Test-Path -LiteralPath (Join-Path $candidate $_) -PathType Container})) {return [IO.Path]::GetFullPath($candidate)}
 }
 return $null
}
function Find-DataRoot {
 $roots=New-Object System.Collections.Generic.List[string]
 if($env:RYNTHSUITE_DIR){$roots.Add($env:RYNTHSUITE_DIR)}
 foreach($hive in @([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryHive]::LocalMachine)){
  $key=$null;$base=$null
  try{
   $base=[Microsoft.Win32.RegistryKey]::OpenBaseKey($hive,[Microsoft.Win32.RegistryView]::Registry32)
   $key=$base.OpenSubKey('Software\Rynth');if($key){$value=$key.GetValue('SuiteDir');if($value){$roots.Add([string]$value)}}
  }catch{}finally{if($key){$key.Dispose()};if($base){$base.Dispose()}}
 }
 $roots.Add('C:\Games\RynthSuite')
 foreach($root in $roots){$found=Get-DataRoot $root;if($found){return $found}}
 return $null
}
try {
 Write-Host 'Marae Lassel - Allegiance installer' -ForegroundColor Cyan
 Write-Host 'Nav paths and original mission logic: Soot (xSoot / SootAC).'
 $packageRoot=Split-Path $PSScriptRoot -Parent
 $payload=Join-Path $PSScriptRoot 'Plugin'
 if(-not(Test-Path -LiteralPath (Join-Path $payload 'ACAI2.Rynth.Marae.Plugin.dll'))){throw 'Right-click the downloaded ZIP, choose Extract All, then run INSTALL-MARAE.cmd from the extracted folder.'}
 $expected=@(Get-Content (Join-Path $PSScriptRoot 'checksums.json') -Raw|ConvertFrom-Json)
 foreach($entry in $expected){
  if([IO.Path]::GetFileName($entry.Name) -ne $entry.Name){throw 'Invalid package filename.'}
  $source=Join-Path $payload $entry.Name
  if(-not(Test-Path -LiteralPath $source) -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.Sha256){throw "Package checksum mismatch: $($entry.Name). Download and extract the ZIP again."}
 }
 if($RynthAiFolder){$root=Get-DataRoot $RynthAiFolder;if(-not $root){throw 'Choose the RynthAi data folder containing MetaFiles or NavProfiles. Launch RynthAi once first if these folders do not exist.'}}
 else{$root=Find-DataRoot}
 if(-not $root){
  if($NoLaunch){throw 'RynthAi was not found. Supply -RynthAiFolder with its data folder.'}
  Add-Type -AssemblyName System.Windows.Forms
  $dialog=New-Object System.Windows.Forms.FolderBrowserDialog
  $dialog.Description='Choose your RynthAi folder (contains MetaFiles or NavProfiles). Your RynthSuite folder also works.'
  $dialog.ShowNewFolderButton=$false
  try{
   if($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK){throw 'Installation cancelled. Run the installer again when you have located your RynthAi folder.'}
   $root=Get-DataRoot $dialog.SelectedPath
   if(-not $root){throw 'That folder does not contain RynthAi data. Find the folder with MetaFiles or NavProfiles, then run the installer again.'}
  }finally{$dialog.Dispose()}
 }
 $RynthAiFolder=$root
 $engine=Join-Path $RynthAiFolder 'RynthCore.Plugin.RynthAi.dll'
 $before=if(Test-Path -LiteralPath $engine){(Get-FileHash -LiteralPath $engine -Algorithm SHA256).Hash}else{$null}
 $backup=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('ACAI2\Backups\Marae-App\'+(Get-Date -Format 'yyyyMMdd-HHmmss-fffffff'))
 New-Item -ItemType Directory $Destination -Force|Out-Null
 # Only our app files are copied. Existing Rynth and character settings remain in place.
 foreach($entry in $expected){
  $target=Join-Path $Destination $entry.Name
  if(Test-Path -LiteralPath $target){New-Item -ItemType Directory $backup -Force|Out-Null;Copy-Item -LiteralPath $target -Destination (Join-Path $backup $entry.Name)}
  Copy-Item -LiteralPath (Join-Path $payload $entry.Name) -Destination $target -Force
 }
 $installation=@{RynthAiFolder=$RynthAiFolder;PluginFolder=[IO.Path]::GetFullPath($Destination);InstallerVersion='1.16.0'}|ConvertTo-Json
 $installation|Set-Content (Join-Path $Destination 'installation.json') -Encoding UTF8
 # CoreCLR loads managed plugins from streams: Assembly.Location can be empty.
 # A stable per-user hint also supports a custom plugin destination on another drive.
 New-Item -ItemType Directory $DiscoveryFolder -Force|Out-Null
 $installation|Set-Content (Join-Path $DiscoveryFolder 'installation.json') -Encoding UTF8
 foreach($name in @('Marae-Quick-Start.pdf','README.txt','CREDITS.txt')){
  if(Test-Path -LiteralPath (Join-Path $packageRoot $name)){Copy-Item -LiteralPath (Join-Path $packageRoot $name) -Destination $Destination -Force}
 }
 if($before -and (Get-FileHash -LiteralPath $engine -Algorithm SHA256).Hash -ne $before){throw 'The RynthAi DLL changed during installation. This installer does not write to it.'}
 $installedDll=Join-Path ([IO.Path]::GetFullPath($Destination)) 'ACAI2.Rynth.Marae.Plugin.dll'
 Write-Host "Installed. RynthAi data: $RynthAiFolder" -ForegroundColor Green
 Write-Host 'Now open RynthCore > Plugins > Add Plugin and select:'
 Write-Host $installedDll -ForegroundColor Cyan
 [IO.File]::WriteAllText((Join-Path $Destination 'PLUGIN-PATH.txt'),$installedDll)
 if(-not $NoLaunch){
  try{Set-Clipboard -Value $installedDll;Write-Host 'The full plugin path is copied. Press Ctrl+V in the Add Plugin file box.'}catch{Write-Host 'Select the highlighted DLL in the folder opening next.'}
  try{Start-Process explorer.exe -ArgumentList ('/select,"'+$installedDll+'"')}catch{}
 }
 Write-Host 'Enable ACAI2.Marae, keep RynthAi enabled, then type /acai2marae in game.'
 Write-Host 'Open Marae-Quick-Start.pdf for the short guide. No Lua setup or manual nav copying is needed.'
} catch {
 Write-Host $_.Exception.Message -ForegroundColor Red
 if(-not $NoPause){[void](Read-Host 'Press Enter to close')}
 throw
}
if(-not $NoPause){[void](Read-Host 'Press Enter to close')}
