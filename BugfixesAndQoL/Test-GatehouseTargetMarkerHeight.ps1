[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$current = Get-Content -Raw -LiteralPath (Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\CURRENT.json') | ConvertFrom-Json
$native = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$assembly = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'
function Get-MarkerSha256([string]$path) {
    $stream = [IO.File]::OpenRead($path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $sha.Dispose(); $stream.Dispose() }
}
if ((Get-MarkerSha256 $native) -cne $current.currentNativeHash -or
    $current.currentNativeHash -cne 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2' -or
    (Get-MarkerSha256 $assembly) -cne 'BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789') {
    throw 'Marker native/managed audit does not match installed binaries.'
}

# Read the actual non-publicized metadata without loading Unity into the test process.
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\core\Mono.Cecil.dll')))
$module = [Mono.Cecil.ModuleDefinition]::ReadModule($assembly)
try {
    $map = @($module.Types | Where-Object { $_.FullName -ceq 'GameMap' })[0]
    $methods = @($map.Methods | Where-Object { $_.Name -ceq 'addUpdatePixie' })
    $signature = 'System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Single,System.Int32,System.Int32,System.Boolean,System.Int32,System.Int32,System.Int32'
    if ($methods.Count -ne 1 -or -not $methods[0].IsPublic -or $methods[0].IsStatic -or
        $methods[0].ReturnType.FullName -cne 'System.Void' -or
        (($methods[0].Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -cne $signature -or
        (($methods[0].Parameters | ForEach-Object { $_.Name }) -join ',') -cne '_objectID,x,y,tile_x,tile_y,heightAboveGround,file,image,hiMode,_transparency,_layerDelay,_colour') {
        throw 'Real GameMap.addUpdatePixie visibility/signature/parameter names changed.'
    }
    $lookup = @($map.Methods | Where-Object { $_.Name -ceq 'getMapTile' -and $_.Parameters.Count -eq 2 })
    if ($lookup.Count -ne 1 -or -not $lookup[0].IsPublic -or $lookup[0].IsStatic -or
        $lookup[0].ReturnType.FullName -cne 'GameMapTile' -or
        (($lookup[0].Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -cne 'System.Int32,System.Int32') {
        throw 'Real GameMap.getMapTile contract changed.'
    }
    $tile = @($module.Types | Where-Object { $_.FullName -ceq 'GameMapTile' })[0]
    foreach ($name in @('gameMapX', 'gameMapY')) {
        $field = @($tile.Fields | Where-Object { $_.Name -ceq $name })
        if ($field.Count -ne 1 -or -not $field[0].IsPublic -or $field[0].IsStatic -or $field[0].FieldType.FullName -cne 'System.Int32') {
            throw "Real GameMapTile.$name visibility/type changed."
        }
    }
} finally { $module.Dispose() }

$policy = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\GatehouseTargetMarkerHeightPolicy.cs'))
$tests = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'tests\GatehouseTargetMarkerHeightTests.cs'))
Add-Type -TypeDefinition ($tests + [Environment]::NewLine + $policy) -Language CSharp
$count = [BugfixesAndQoL.GatehouseTargetMarkerHeightTests]::Run()
# Execute the source-linked production prefix with controlled native flags and SE data.
# Separate processes also test the unknown-build installation guard without resetting static roots.
$prefixProject = Join-Path $PSScriptRoot 'tests\GatehouseMarkerPrefix.Tests\Tests.csproj'
& dotnet run --project $prefixProject --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Production marker prefix regression failed.' }
& dotnet run --project $prefixProject --configuration Release --no-build -- --unknown-native
if ($LASTEXITCODE -ne 0) { throw 'Unknown-build marker prefix regression failed.' }
$runtime = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\GatehouseTargetMarkerHeightHook.cs'))
foreach ($required in @('private static readonly Harmony harmony', 'Volatile.Read(ref enabled)',
    'Volatile.Write(ref enabled', 'GATEHOUSE_TARGET_MARKER_POST_STARTUP', 'IsTileInsideMapBounds',
    'IsValidTileId', 'IsValidId(buildingId)', 'TryGetBuildingById(buildingId', '__instance?.getMapTile(x, y)',
    'ref tile_y', 'tile.gameMapX, tile.gameMapY', 'CurrentNativeSha256 != GatehouseTargetMarkerHeightPolicy.NativeSha256',
    'GATEHOUSE_TARGET_MARKER_INSTALLED', 'GATEHOUSE_TARGET_MARKER_APPLIED',
    'IsMoveTarget(file, image, tile_x, hiMode)')) {
    if (-not $runtime.Contains($required)) { throw "Missing marker runtime guard: $required" }
}
if ($runtime -match '\b(?:OnDestroy|OnDisable|OnApplicationQuit|StartCoroutine|Update|LateUpdate|FixedUpdate|Unpatch|Undo|Dispose|Disable|VirtualProtect|CodePatch)\s*\(') {
    throw 'Marker runtime contains a callback/teardown/executable mutation outside permanent installation.'
}
$view = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\BugfixesAndQoLViewModel.cs'))
if ($view -notmatch '\[APIShared\.ModSettings\.PresetLocal\]\s+public bool EnableGatehouseTargetMarkerHeightFix' -or
    -not $view.Contains('private bool enableGatehouseTargetMarkerHeightFix = true;') -or
    -not $view.Contains('EnableGatehouseTargetMarkerHeightFix = true;')) { throw 'Local marker setting/default/reset missing.' }
[xml]$xaml = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml'))
if ($xaml.SelectNodes('//*[@IsChecked="{Binding EnableGatehouseTargetMarkerHeightFix, Mode=TwoWay}"]').Count -ne 1) {
    throw 'Marker checkbox must occur exactly once.'
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Locales') -Filter '*.txt') {
    $text = [IO.File]::ReadAllText($file.FullName)
    foreach ($key in @('BugfixesAndQoL.EnableGatehouseTargetMarkerHeightFix', 'BugfixesAndQoL.EnableGatehouseTargetMarkerHeightFixHelp')) {
        if ([regex]::Matches($text, '(?m)^' + [regex]::Escape($key) + '=').Count -ne 1) { throw "$($file.Name): missing/duplicate $key" }
    }
}
Write-Output "PASS: $count marker height cases; real managed member contracts, bounds/IDs, lifetime, setting, XAML and locales."
