param([string]$Package)
$ErrorActionPreference='Stop'
$testRoot=Join-Path $env:RUNNER_TEMP ('scroll-installer-'+[guid]::NewGuid())
try {
    $target=Join-Path $testRoot 'Different Drive Layout\App'
    & (Join-Path $Package 'Support\Install-Scrolls.ps1') -InstallDirectory $target -Quiet
    if($LASTEXITCODE){throw 'Installer failed'}
    $name='ACAI2.Rynth.Scrolls.Plugin.dll'
    $expected=(Get-FileHash (Join-Path $Package "Support\Plugin\$name")).Hash
    if((Get-FileHash (Join-Path $target $name)).Hash -ne $expected){throw 'Installed DLL differs'}
    if((Get-Content (Join-Path $target 'PLUGIN-PATH.txt') -Raw).Trim() -ne (Join-Path $target $name)){throw 'Wrong plugin hint'}
    if(Test-Path (Join-Path $target 'RynthCore.Plugin.RynthAi.dll')){throw 'Must not install RynthAi'}
    Write-Host 'PASS: isolated installer handles nonstandard paths and spaces, exact DLL copy, plugin hint, no RynthAi replacement.'
} finally { if(Test-Path $testRoot){Remove-Item $testRoot -Recurse -Force} }
