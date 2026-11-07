param([Parameter(Mandatory = $true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$modDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$localDir = [IO.Path]::GetFullPath((Join-Path $modDir 'BepInEx\plugins\Pathfinding_Serp'))
$gameRoot = [IO.Path]::GetFullPath($GameDir)
$installedDir = [IO.Path]::GetFullPath((Join-Path $gameRoot 'BepInEx\plugins\Pathfinding_Serp'))
if ($installedDir -ne ($gameRoot.TrimEnd('\') + '\BepInEx\plugins\Pathfinding_Serp')) { throw 'Invalid package target.' }
if (-not (Test-Path -LiteralPath (Join-Path $localDir 'Pathfinding.dll') -PathType Leaf)) { throw 'Build the package first.' }
Copy-Item -LiteralPath (Join-Path $modDir 'info.json') -Destination (Join-Path $localDir 'info.json') -Force
[IO.Directory]::CreateDirectory($installedDir) | Out-Null
foreach ($source in [IO.Directory]::GetFiles($localDir, '*', [IO.SearchOption]::AllDirectories)) {
    $relative = $source.Substring($localDir.Length).TrimStart('\')
    $target = Join-Path $installedDir $relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    [IO.File]::Copy($source, $target, $true)
    $a = [Security.Cryptography.SHA256]::Create()
    try {
        $left = [BitConverter]::ToString($a.ComputeHash([IO.File]::ReadAllBytes($source)))
        $right = [BitConverter]::ToString($a.ComputeHash([IO.File]::ReadAllBytes($target)))
    } finally { $a.Dispose() }
    if ($left -cne $right) { throw "Copy hash mismatch: $relative" }
}
Write-Output "Pathfinding installed: $installedDir"
# The mod refuses to load next to its source mods until their code is removed (MIGRATION.md).
foreach ($other in 'BugfixesAndQoL_Serp', 'MoatMove_Serp') {
    if (Test-Path -LiteralPath (Join-Path $gameRoot "BepInEx\plugins\$other")) {
        Write-Warning "$other is installed; Pathfinding stays inactive until that folder is parked."
    }
}
