param([Parameter(Mandatory = $true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
function Get-PackageHash([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $sha.Dispose(); $stream.Dispose() }
}
$modDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$localDir = [IO.Path]::GetFullPath((Join-Path $modDir 'BepInEx\plugins\MoatMove_Serp'))
$gameRoot = [IO.Path]::GetFullPath($GameDir).TrimEnd('\')
$installedDir = [IO.Path]::GetFullPath((Join-Path $gameRoot 'BepInEx\plugins\MoatMove_Serp'))
if (-not $localDir.StartsWith($modDir + '\', [StringComparison]::OrdinalIgnoreCase) -or $installedDir -ne ($gameRoot + '\BepInEx\plugins\MoatMove_Serp')) { throw 'Invalid package target.' }
if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Game is running; installation aborted.' }
Copy-Item -LiteralPath (Join-Path $modDir 'info.json') -Destination (Join-Path $localDir 'info.json') -Force
$files = @('MoatMove.dll','MoatMove.pdb','info.json')
foreach ($name in $files) { if (-not (Test-Path -LiteralPath (Join-Path $localDir $name) -PathType Leaf)) { throw "Package missing $name" } }
$version = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $localDir 'MoatMove.dll')).Version
if ($version.ToString() -ne '0.1.0.0') { throw "Unexpected assembly version: $version" }
[IO.Directory]::CreateDirectory($installedDir) | Out-Null
foreach ($name in $files) {
    $source = Join-Path $localDir $name
    $destination = Join-Path $installedDir $name
    Copy-Item -LiteralPath $source -Destination $destination -Force
    if ((Get-PackageHash $source) -ne (Get-PackageHash $destination)) { throw "Installation hash mismatch: $name" }
}
Write-Output "PASS installed MoatMove 0.1.0; DLL/PDB/manifest hashes match: $installedDir"
