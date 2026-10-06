[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceDirectory,
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$GameDirectory,
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$expectedPackage = [IO.Path]::GetFullPath((Join-Path $workspace 'shcde-script-extender\mod_output\patchers\SHCDESE.KillSwitch'))
$package = [IO.Path]::GetFullPath($PackageDirectory).TrimEnd('\')
if ($package -ne $expectedPackage.TrimEnd('\')) { throw 'Unexpected preloader package target' }
$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
$temporaryPrefix = (Resolve-Path -LiteralPath $env:TEMP).Path.TrimEnd('\') + '\shcdese-local-build-'
if (-not $source.StartsWith($temporaryPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected preloader build source' }
$required = @('SHCDESE.KillSwitch.Preloader.dll','Blake3.dll','blake3_dotnet.dll',
    'BouncyCastle.Cryptography.dll','System.Text.Json.dll','System.Text.Encodings.Web.dll',
    'System.Memory.dll','System.Buffers.dll','System.Runtime.CompilerServices.Unsafe.dll',
    'Microsoft.Bcl.AsyncInterfaces.dll','System.Threading.Tasks.Extensions.dll')
foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $file) -PathType Leaf)) { throw "Preloader dependency missing: $file" }
}
function Get-Sha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)) }
    finally { $sha.Dispose(); $stream.Dispose() }
}
function Sync-VerifiedTree([string]$From, [string]$To) {
    [IO.Directory]::CreateDirectory($To) | Out-Null
    & robocopy $From $To /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Preloader synchronization failed: $To ($LASTEXITCODE)" }
    $sourceFiles = @(Get-ChildItem -LiteralPath $From -Recurse -File)
    $targetFiles = @(Get-ChildItem -LiteralPath $To -Recurse -File)
    if ($sourceFiles.Count -ne $targetFiles.Count) { throw "Preloader file-set mismatch: $To" }
    foreach ($file in $sourceFiles) {
        $relative = $file.FullName.Substring($From.TrimEnd('\').Length + 1)
        $other = Join-Path $To $relative
        if (-not (Test-Path -LiteralPath $other -PathType Leaf) -or
            (Get-Sha256 $file.FullName) -ne (Get-Sha256 $other)) {
            throw "Preloader hash mismatch: $relative"
        }
    }
    Write-Output "Verified preloader package: $($sourceFiles.Count) files at $To"
}
if (-not $Install) { Sync-VerifiedTree $source $package; exit 0 }
$expectedGame = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
if ([IO.Path]::GetFullPath($GameDirectory).TrimEnd('\') -ne $expectedGame) { throw 'Unexpected game installation target' }
$target = [IO.Path]::GetFullPath((Join-Path $GameDirectory 'BepInEx\patchers\SHCDESE.KillSwitch'))
Sync-VerifiedTree $package $target
