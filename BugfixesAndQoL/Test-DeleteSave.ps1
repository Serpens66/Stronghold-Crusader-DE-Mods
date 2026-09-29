[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$policyPath = Join-Path $PSScriptRoot 'src\SaveDeletionPolicy.cs'
$bridge = @'
public static class SaveDeletionPolicyTestBridge
{
    public static bool Resolve(string selected, string root, bool multiplayer, out string safePath)
    {
        return BugfixesAndQoL.SaveDeletionPolicy.TryResolveDeletableSavePath(
            selected, root, multiplayer, System.IO.File.Exists, out safePath);
    }
}
'@
Add-Type -TypeDefinition ([IO.File]::ReadAllText($policyPath) + [Environment]::NewLine + $bridge)

function Assert-PathResult {
    param([string]$Path, [string]$Root, [bool]$Multiplayer, [bool]$Expected, [string]$Name)
    $safePath = $null
    $actual = [SaveDeletionPolicyTestBridge]::Resolve($Path, $Root, $Multiplayer, [ref]$safePath)
    if ($actual -ne $Expected) { throw "${Name}: expected $Expected, got $actual." }
    if ($actual -and -not [string]::Equals(
        $safePath, [IO.Path]::GetFullPath($Path), [StringComparison]::OrdinalIgnoreCase)) {
        throw "${Name}: safe path differs from the selected file."
    }
}

$fixture = Join-Path ([IO.Path]::GetTempPath()) ('BugfixesAndQoL-DeleteSave-' + [guid]::NewGuid().ToString('N'))
$saves = Join-Path $fixture 'Saves'
$nested = Join-Path $saves 'Nested'
[IO.Directory]::CreateDirectory($nested) | Out-Null
$single = Join-Path $saves 'Regular.sav'
$quicksave = Join-Path $saves 'Quicksave 2026.sav'
$multi = Join-Path $saves 'Coop.msv'
$wrong = Join-Path $saves 'Map.map'
$nestedSave = Join-Path $nested 'Nested.sav'
$outsideSave = Join-Path $fixture 'Outside.sav'
try {
    foreach ($path in @($single, $quicksave, $multi, $wrong, $nestedSave, $outsideSave)) {
        [IO.File]::WriteAllBytes($path, [byte[]]@(1))
    }

    Assert-PathResult $single $saves $false $true 'singleplayer save'
    Assert-PathResult $quicksave $saves $false $true 'visible quicksave'
    Assert-PathResult $multi $saves $true $true 'multiplayer save'
    Assert-PathResult $single $saves $true $false 'singleplayer file in multiplayer menu'
    Assert-PathResult $multi $saves $false $false 'multiplayer file in singleplayer menu'
    Assert-PathResult $wrong $saves $false $false 'wrong extension'
    Assert-PathResult $nestedSave $saves $false $false 'nested save'
    Assert-PathResult $outsideSave $saves $false $false 'outside save'
    Assert-PathResult $null $saves $false $false 'missing selection'

    [IO.File]::Delete($single)
    Assert-PathResult $single $saves $false $false 'file removed after confirmation'
}
finally {
    foreach ($path in @($single, $quicksave, $multi, $wrong, $nestedSave, $outsideSave)) {
        if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) }
    }
    if ([IO.Directory]::Exists($nested)) { [IO.Directory]::Delete($nested) }
    if ([IO.Directory]::Exists($saves)) { [IO.Directory]::Delete($saves) }
    if ([IO.Directory]::Exists($fixture)) { [IO.Directory]::Delete($fixture) }
}

Write-Output 'Delete Save path policy tests passed.'
