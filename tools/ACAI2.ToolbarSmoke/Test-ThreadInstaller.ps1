param([string]$Package)
$ErrorActionPreference='Stop'
$root=Join-Path $env:RUNNER_TEMP ('threads-toolbar-install-'+[guid]::NewGuid())
try {
    $target=Join-Path $root 'Custom Path\App'
    & (Join-Path $Package 'Support\Install-Threads.ps1') -InstallDirectory $target -Quiet
    if($LASTEXITCODE){throw 'Thread install failed'}
    $dll='ACAI2.Rynth.Threads.Plugin.dll'
    if((Get-FileHash (Join-Path $target $dll)).Hash -ne (Get-FileHash (Join-Path $Package $dll)).Hash){throw 'Thread DLL differs'}
    if((Get-Content (Join-Path $target 'PLUGIN-PATH.txt') -Raw).Trim() -ne (Join-Path $target $dll)){throw 'Wrong path hint'}
    if(Test-Path (Join-Path $target 'RynthCore.Plugin.RynthAi.dll')){throw 'Unexpected engine replacement'}
    Write-Host 'PASS: Threads installer copies the app to a custom path with spaces; no engine replacement.'
} finally {if(Test-Path $root){Remove-Item $root -Recurse -Force}}
