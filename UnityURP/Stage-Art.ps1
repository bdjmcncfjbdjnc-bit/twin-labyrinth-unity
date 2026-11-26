$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $PSScriptRoot 'Assets/ArtReview/Source'

$sourceSets = @{
    Materials = 'ReferenceMaterials'
    Characters = 'ReferenceCharacters'
    Environment = 'ReferenceEnvironment'
}

foreach ($set in $sourceSets.GetEnumerator()) {
    $source = Join-Path $sourceRoot $set.Value
    if (!(Test-Path -LiteralPath $source -PathType Container)) {
        throw "Missing asset source directory: $source"
    }
}

foreach ($set in $sourceSets.GetEnumerator()) {
    $source = Join-Path $sourceRoot $set.Value
    $target = Join-Path $assetRoot $set.Key
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Get-ChildItem -LiteralPath $source | Where-Object Name -NotIn 'base_body.glb','Rocketbox' |
        Copy-Item -Destination $target -Recurse -Force
}

node (Join-Path $PSScriptRoot 'prepare-body-zones.cjs')
if ($LASTEXITCODE -ne 0) { throw 'Body-zone preparation failed.' }
