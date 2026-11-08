$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$Pin = Get-Content (Join-Path $ProjectRoot 'origin\RynthCore.lock.json') -Raw | ConvertFrom-Json
$Dependency = Join-Path $ProjectRoot 'external\RynthCore'
$SdkProject = Join-Path $Dependency 'src\RynthCore.PluginSdk\RynthCore.PluginSdk.csproj'
$Marker = Join-Path $Dependency '.acai2-upstream-pin.json'

# Normal Git clone: preserve the pinned submodule and all existing work.
if ((Test-Path (Join-Path $ProjectRoot '.git')) -and (Get-Command git -ErrorAction SilentlyContinue)) {
    git -C $ProjectRoot submodule update --init --recursive
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the pinned RynthCore submodule.' }
    git -C $Dependency diff --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Dependency has local tracked changes; preserve/review them before building.' }
    git -C $Dependency diff --cached --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Dependency has staged changes; preserve/review them before building.' }
    $ActualCommit = (git -C $Dependency rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $ActualCommit -ne $Pin.commit) { throw 'RynthCore differs from the reviewed pin; no files were overwritten.' }
    return
}
# GitHub Download ZIP excludes submodule contents. Fetch a checksum-pinned source archive.
if (Test-Path $SdkProject) {
    if (-not (Test-Path $Marker)) { throw 'An unverified dependency already exists. Preserve it and resolve its origin before bootstrap.' }
    $Saved = Get-Content $Marker -Raw | ConvertFrom-Json
    if ($Saved.commit -ne $Pin.commit -or $Saved.archiveSha256 -ne $Pin.archiveSha256) { throw 'Existing dependency marker differs from the reviewed pin.' }
    return
}
if ((Test-Path $Dependency) -and @(Get-ChildItem -Force $Dependency).Count -gt 0) { throw 'Dependency folder is not empty; refusing to overwrite it.' }
$RynthScratch = Join-Path ([IO.Path]::GetTempPath()) ('acai2-rynth-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $RynthScratch | Out-Null
try {
    $Archive = Join-Path $RynthScratch 'RynthCore.zip'
    Invoke-WebRequest -Uri $Pin.archiveUrl -OutFile $Archive -UseBasicParsing
    if ((Get-FileHash $Archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Pin.archiveSha256) { throw 'RynthCore archive checksum mismatch; dependency was not installed.' }
    Expand-Archive $Archive (Join-Path $RynthScratch 'unpacked')
    $Extracted = Join-Path $RynthScratch ('unpacked\RynthCore-' + $Pin.commit)
    if (-not (Test-Path (Join-Path $Extracted 'src\RynthCore.PluginSdk\RynthCore.PluginSdk.csproj'))) { throw 'Verified archive has an unexpected layout.' }
    New-Item -ItemType Directory (Split-Path $Dependency -Parent) -Force | Out-Null
    if (Test-Path $Dependency) { Remove-Item $Dependency } # verified empty above
    Move-Item $Extracted $Dependency
    $Pin | ConvertTo-Json | Set-Content $Marker -Encoding UTF8
} finally { Remove-Item $RynthScratch -Recurse -Force }
