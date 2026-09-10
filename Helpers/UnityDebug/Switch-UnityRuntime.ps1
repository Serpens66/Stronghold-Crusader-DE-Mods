param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Normal', 'Debug')]
    [string] $Mode
)

$ErrorActionPreference = 'Stop'

$gameRoot = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$toolRoot = Split-Path -Parent $PSCommandPath
$sourceFolderName = if ($Mode -eq 'Debug') { 'DebugGameFiles' } else { 'OriginalGameFiles' }
$sourceRoot = Join-Path $toolRoot $sourceFolderName
$gameProcessName = 'Stronghold Crusader Definitive Edition'
$gameBinaryRelativePath = 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$expectedGameBinarySha256 = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'

$requiredRelativePaths = @(
    'Stronghold Crusader Definitive Edition.exe',
    'UnityPlayer.dll',
    'Stronghold Crusader Definitive Edition_Data\boot.config',
    'Stronghold Crusader Definitive Edition_Data\Managed\UnityEngine.dll',
    'Stronghold Crusader Definitive Edition_Data\Managed\UnityEngine.CoreModule.dll'
)

$debugOnlyRelativePaths = @(
    'UnityPlayer_Win64_player_development_mono_x64.pdb',
    'WindowsPlayer_player_Release_mono_x64.pdb'
)

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BasePath,
        [Parameter(Mandatory = $true)]
        [string] $FullPath
    )

    return $FullPath.Substring($BasePath.Length).TrimStart('\')
}

if (-not (Test-Path -LiteralPath $gameRoot -PathType Container)) {
    throw "Game directory not found: $gameRoot"
}

if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
    throw "Source directory not found: $sourceRoot"
}

if (Get-Process -Name $gameProcessName -ErrorAction SilentlyContinue) {
    throw 'The game is running. Close it before switching the Unity runtime.'
}

# A game update can make both the saved release files and the Unity debug overlay stale.
$gameBinaryPath = Join-Path $gameRoot $gameBinaryRelativePath
if (-not (Test-Path -LiteralPath $gameBinaryPath -PathType Leaf)) {
    throw "Game-version marker is missing: $gameBinaryPath"
}

$gameBinarySha256 = (Get-FileHash -LiteralPath $gameBinaryPath -Algorithm SHA256).Hash
if ($gameBinarySha256 -ne $expectedGameBinarySha256) {
    throw 'The installed game version has changed. Do not apply this saved Unity runtime until OriginalGameFiles and DebugGameFiles have been reviewed and refreshed.'
}

foreach ($relativePath in $requiredRelativePaths) {
    $requiredPath = Join-Path $sourceRoot $relativePath
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required source file is missing: $requiredPath"
    }
}

$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File)
if ($sourceFiles.Count -eq 0) {
    throw "No files found in source directory: $sourceRoot"
}

Write-Host "Switching SHCDE to Unity mode: $Mode"
Write-Host "Source: $sourceRoot"
Write-Host "Target: $gameRoot"
Write-Host "Files:  $($sourceFiles.Count)"
Write-Host

foreach ($sourceFile in $sourceFiles) {
    $relativePath = Get-RelativePath -BasePath $sourceRoot -FullPath $sourceFile.FullName
    $destinationPath = Join-Path $gameRoot $relativePath
    $destinationDirectory = Split-Path -Parent $destinationPath

    if (-not (Test-Path -LiteralPath $destinationDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    }

    Copy-Item -LiteralPath $sourceFile.FullName -Destination $destinationPath -Force
}

if ($Mode -eq 'Normal') {
    foreach ($relativePath in $debugOnlyRelativePaths) {
        $debugOnlyPath = Join-Path $gameRoot $relativePath
        if (Test-Path -LiteralPath $debugOnlyPath -PathType Leaf) {
            Remove-Item -LiteralPath $debugOnlyPath -Force
        }
    }
}

Write-Host 'Verifying copied files with SHA-256...'
foreach ($sourceFile in $sourceFiles) {
    $relativePath = Get-RelativePath -BasePath $sourceRoot -FullPath $sourceFile.FullName
    $destinationPath = Join-Path $gameRoot $relativePath

    if (-not (Test-Path -LiteralPath $destinationPath -PathType Leaf)) {
        throw "Copied file is missing: $destinationPath"
    }

    $sourceHash = (Get-FileHash -LiteralPath $sourceFile.FullName -Algorithm SHA256).Hash
    $destinationHash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
    if ($sourceHash -ne $destinationHash) {
        throw "Hash verification failed: $relativePath"
    }
}

if ($Mode -eq 'Normal') {
    foreach ($relativePath in $debugOnlyRelativePaths) {
        $debugOnlyPath = Join-Path $gameRoot $relativePath
        if (Test-Path -LiteralPath $debugOnlyPath) {
            throw "Debug-only file was not removed: $debugOnlyPath"
        }
    }
}

Write-Host
Write-Host "Success: SHCDE now uses the $Mode Unity runtime." -ForegroundColor Green

