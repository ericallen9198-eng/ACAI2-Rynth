param([string]$InstallDirectory,[switch]$Quiet)
$ErrorActionPreference='Stop'
try {
    $source=Join-Path $PSScriptRoot 'Plugin'
    if(-not (Test-Path (Join-Path $source 'ACAI2.Rynth.Scrolls.Plugin.dll'))){throw 'Extract the complete ZIP before running the installer.'}
    $destination=if($InstallDirectory){$InstallDirectory}else{Join-Path $env:LOCALAPPDATA 'ACAI2\Scrolls\App'}
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    $dll=Join-Path $destination 'ACAI2.Rynth.Scrolls.Plugin.dll'
    if(Test-Path $dll){
        $backup=Join-Path $env:LOCALAPPDATA ('ACAI2\Backups\Scrolls\'+(Get-Date -Format 'yyyyMMdd-HHmmss-fffffff'))
        New-Item -ItemType Directory -Force -Path $backup | Out-Null
        Get-ChildItem -LiteralPath $destination -File | Copy-Item -Destination $backup
        Write-Host "Existing app files backed up to $backup"
    }
    Get-ChildItem -LiteralPath $source -File | Copy-Item -Destination $destination -Force
    $dll | Set-Content -LiteralPath (Join-Path $destination 'PLUGIN-PATH.txt') -Encoding utf8
    if(-not $Quiet){Set-Clipboard -Value $dll}
    Write-Host "Installed Scrollwork: $dll"
    Write-Host 'In RynthCore: Plugins -> Add Plugin -> paste the copied DLL path -> enable ACAI2.Scrolls.'
    Write-Host 'In game: /acai2scrolls'
    if(-not $Quiet){Start-Process explorer.exe -ArgumentList @('/select,',('"'+$dll+'"'))}
} catch {Write-Host ("Installation failed: "+$_.Exception.Message) -ForegroundColor Red;exit 1}
