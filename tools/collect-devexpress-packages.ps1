<#
.SYNOPSIS
  Copies every DevExpress.* package the API restored into ./dxpackages so the Docker build can restore offline.
  Run after a successful "dotnet restore" on a machine that has DevExpress (installer or NuGet feed).
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$assetsPath = Join-Path $root 'API.CRM_DotNET\obj\project.assets.json'
if (-not (Test-Path $assetsPath)) { throw "Run 'dotnet restore' first ($assetsPath not found)." }

$assets = Get-Content $assetsPath -Raw | ConvertFrom-Json
$target = Join-Path $root 'dxpackages'
# Package folders (extracted, hierarchical) and local package sources (flat *.nupkg files).
$folders = @($assets.packageFolders.PSObject.Properties.Name) +
           @($assets.project.restore.sources.PSObject.Properties.Name | Where-Object { (Test-Path $_ -PathType Container) -and ((Resolve-Path $_).Path -ne (Resolve-Path $target -ErrorAction SilentlyContinue).Path) })
New-Item -ItemType Directory -Force $target | Out-Null

$copied = 0
foreach ($lib in $assets.libraries.PSObject.Properties) {
    if ($lib.Name -notlike 'DevExpress.*' -or $lib.Value.type -ne 'package') { continue }
    $id, $version = $lib.Name -split '/'
    $found = $null
    foreach ($folder in $folders) {
        $found = Get-ChildItem (Join-Path $folder $lib.Value.path) -Filter '*.nupkg' -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $found) { $found = Get-Item (Join-Path $folder "$id.$version.nupkg") -ErrorAction SilentlyContinue }
        if ($found) { break }
    }
    if (-not $found) { Write-Warning "Not found: $($lib.Name)"; continue }
    Copy-Item $found.FullName $target -Force
    $copied++
}
Write-Host "Copied $copied DevExpress packages to $target"
