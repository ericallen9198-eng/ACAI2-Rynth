# Offline filesystem tests: temporary dummy bytes only, never an installed plugin.
param([string]$Installer = (Join-Path $PSScriptRoot 'Install-NpcPatch.ps1'))
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('ACAI2-NpcInstallerTest-' + [Guid]::NewGuid().ToString('N'))
$originalLocal = $env:LOCALAPPDATA
$checks = 0
function Check([bool]$Pass, [string]$What) {
    if (-not $Pass) { throw $What }
    $script:checks++
}
function Hash([string]$File) { (Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash }
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $env:LOCALAPPDATA = Join-Path $root 'LocalAppData'
    New-Item -ItemType Directory $env:LOCALAPPDATA | Out-Null
    $target = Join-Path $root 'installed.dll'
    $patch = Join-Path $root 'RynthCore.Plugin.RynthAi.dll'
    [IO.File]::WriteAllText($target, 'offline original')
    [IO.File]::WriteAllText($patch, 'offline replacement')
    $oldHash = Hash $target; $newHash = Hash $patch
    $source = Get-Content -LiteralPath $Installer -Raw
    $reviewedHash = '97a917edf02e58c6938cdb866ca21cf1d1fd996eb103dc593671a2e8802b717c'
    Check ($source.Contains($reviewedHash)) 'production installer retains exact reviewed original hash'
    $fixtureScript = Join-Path $root 'fixture-install.ps1'
    $source.Replace($reviewedHash, $oldHash) | Set-Content -LiteralPath $fixtureScript -Encoding UTF8
    Check ($source.Contains($reviewedHash)) 'installer guards the exact reviewed original'
    Check ($source.Contains('C:\Games\RynthSuite\RynthAi\RynthCore.Plugin.RynthAi.dll')) 'default path matches user installation'
    $fixtureScript = Join-Path $root 'Install-NpcPatch.ps1'
    # Substitute only the original-byte fingerprint for this temporary fixture.
    [IO.File]::WriteAllText($fixtureScript, $source.Replace($reviewedHash, $oldHash))
    $sum = Join-Path $root 'RynthCore.Plugin.RynthAi.dll.sha256'
    "$newHash  RynthCore.Plugin.RynthAi.dll" | Set-Content $sum -Encoding ascii
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $fixtureScript -PluginPath $target -NoPause
    Check ($LASTEXITCODE -eq 0) 'Windows PowerShell 5.1 installation succeeds'
    Check ((Hash $target) -eq $newHash) 'target contains replacement'
    $backup = @(Get-ChildItem -LiteralPath $env:LOCALAPPDATA -Filter 'restore-info.json' -Recurse)
    Check ($backup.Count -eq 1) 'one restore record written'
    $info = Get-Content $backup[0].FullName -Raw | ConvertFrom-Json
    Check ((Hash $info.Backup) -eq $oldHash) 'original bytes preserved in backup'
    Check ($info.InstalledPath -eq $target) 'restore record names actual target'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $fixtureScript -PluginPath $target -NoPause
    Check ($LASTEXITCODE -eq 0) 'already installed exits successfully'
    Check (@(Get-ChildItem -LiteralPath $env:LOCALAPPDATA -Filter 'restore-info.json' -Recurse).Count -eq 1) 'already installed does not replace original backup'
    [IO.File]::WriteAllText($target, 'unreviewed plugin')
    $unknown = Hash $target
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $fixtureScript -PluginPath $target -NoPause
    Check ($LASTEXITCODE -ne 0) 'different source version is refused'
    Check ((Hash $target) -eq $unknown) 'refused target is unchanged'
    [IO.File]::WriteAllText($target, 'offline original')
    [IO.File]::WriteAllText($patch, 'damaged patch download')
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $fixtureScript -PluginPath $target -NoPause
    Check ($LASTEXITCODE -ne 0) 'damaged patch is refused'
    Check ((Hash $target) -eq $oldHash) 'damaged patch cannot change target'
    Check (@(Get-ChildItem -LiteralPath $env:LOCALAPPDATA -Filter 'restore-info.json' -Recurse).Count -eq 1) 'refused installs create no additional backups'
    Write-Host "PASS: $checks installer checks using temporary dummy bytes and Windows PowerShell 5.1."
} finally {
    $env:LOCALAPPDATA = $originalLocal
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
