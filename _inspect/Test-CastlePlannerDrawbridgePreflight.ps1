$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$modRoot = Join-Path $workspace 'CastlePlanner'
$json = '\b(System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft(?:\.Json)?|DataContractJsonSerializer|JsonUtility)\b'
$files = @(Get-ChildItem (Join-Path $modRoot 'src') -Recurse -Filter '*.cs') +
    @(Get-ChildItem $modRoot -File -Filter '*.csproj')
foreach ($file in $files) {
    $source = [IO.File]::ReadAllText($file.FullName)
    if ($source -match $json) { throw "Forbidden runtime JSON: $($file.FullName)" }
    if ($source -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF: $($file.FullName)" }
    if ($source -match '\b(OnDisable|OnApplicationQuit|OnApplicationPause)\s*\(') { throw "Review lifecycle: $($file.FullName)" }
    if ($source -match '\bOnDestroy\s*\(' -and $file.Name -ne 'CastlePlannerPlugin.cs') { throw "Review teardown: $($file.FullName)" }
    if ($source -match ':\s*BaseUnityPlugin' -and $source -match '\b(?:void\s+(?:Update|LateUpdate|FixedUpdate)|StartCoroutine)\s*\(') { throw "Plugin frame runner: $($file.FullName)" }
    if ($source -match '\bStartCoroutine\s*\(' -and $file.Name -ne 'FreeCastlePreviewRuntime.cs') { throw "Review coroutine host: $($file.FullName)" }
}
$plugin = [IO.File]::ReadAllText((Join-Path $modRoot 'src/CastlePlannerPlugin.cs'))
$destroy = [regex]::Match($plugin, '(?s)private void OnDestroy\(\)\s*\{(.*?)\n        \}').Groups[1].Value
if (-not $destroy -or $destroy -match 'Dispose|Undo|-=|Disable\(') { throw 'Unsafe plugin teardown' }
if (-not $plugin.Contains('private static BlueprintRuntimeController blueprintRuntime;')) { throw 'Blueprint runtime lost its static root' }
$controller = [IO.File]::ReadAllText((Join-Path $modRoot 'src/BlueprintRuntimeController.cs'))
if (-not $controller.Contains('Application.onBeforeRender += OnBeforeRender;')) { throw 'Persistent render publisher missing' }
$preview = [IO.File]::ReadAllText((Join-Path $modRoot 'src/FreeCastlePreviewRuntime.cs'))
if (-not $preview.Contains('private void DelayShowDisconnectHook(Director self)') -or
    -not $preview.Contains('self.StartCoroutine(ShowLoadingWarningAfterDelay(generation));') -or
    [regex]::Matches($preview, '\bStartCoroutine\s*\(').Count -ne 1) { throw 'Director coroutine contract changed' }
$project = [IO.File]::ReadAllText((Join-Path $modRoot 'CastlePlanner.csproj'))
if (-not $project.Contains('..\Shared\Runtime\Persistence\DependencyFreeJson.cs') -or $project.Contains('Assembly-CSharp-publicized')) { throw 'Runtime reference contract changed' }
foreach ($file in Get-ChildItem $modRoot -Recurse -Filter '*.xaml' | Where-Object { $_.FullName -match '\\Patches\\' }) {
    [xml]$xml = [IO.File]::ReadAllText($file.FullName)
    foreach ($content in $xml.SelectNodes('//*[local-name()="Content"]')) {
        if (@($content.ChildNodes | Where-Object NodeType -eq Element).Count -ne 1) { throw "XAML Content root count: $($file.FullName)" }
    }
}
$renderer = [IO.File]::ReadAllText((Join-Path $modRoot 'src/BlueprintRenderer.cs'))
$direction = [regex]::Match($renderer, '(?s)private BlueprintDrawbridgePosition ResolveDrawbridgePosition\(.*?(?=        private BlueprintStairDirection)').Value
if ([regex]::Matches($direction, '\bTryGetPlanarPosition\(').Count -ne 2 -or
    $direction -match '\bTryGetGroundPosition\(' -or
    -not $direction.Contains('(int)eMappers.MAPPER_DRAWBRIDGE')) { throw 'Drawbridge direction is not planar/enum-based' }
$planar = [regex]::Match($renderer, '(?s)private bool TryGetPlanarPosition\(.*?(?=        private bool TryGetGroundPosition)').Value
if (-not $planar.Contains('mapTile.tilemapRef.GetCellCenterWorld(tilePosition)') -or
    $planar -match 'TryGetGround|renderedFlattened|mapTile\.height|terrainHeight|GetTileDefaultHeight') { throw 'Height-dependent direction regression' }
$library = [IO.File]::ReadAllText((Join-Path $modRoot 'src/BlueprintBuildingImageLibrary.cs'))
if ([regex]::Matches($library, 'BlueprintCaptureRequest request = BlueprintBuildingCaptureCatalog\.ResolveRequest\(').Count -ne 2) { throw 'Composite/depth capture routing changed' }
$capture = [IO.File]::ReadAllText((Join-Path $modRoot 'src/BlueprintBuildingCaptureCatalog.cs'))
if (-not $capture.Contains('.ResolveDrawbridgeImage(drawbridgePosition).FlipHorizontally')) { throw 'Fallback/composite/depth mirror mapping diverged' }

Add-Type -Path (Join-Path $workspace '_inspect/StatsTweakerApiTests/bin/Mono.Cecil.dll')
$enumSource = [IO.File]::ReadAllText((Join-Path $workspace 'shcde-script-extender/src/SHCDESE.BepInEx/Interop/Enums.cs'))
$enumBody = [regex]::Match($enumSource, '(?s)public enum eMappers\s*:\s*Int16\s*\{(.*?)\}').Groups[1].Value
$withoutComments = [regex]::Replace($enumBody, '//[^\r\n]*', '')
$sourceValues = @{}
$enumValue = -1
foreach ($entry in $withoutComments.Split(',')) {
    $entry = $entry.Trim()
    if (-not $entry) { continue }
    if ($entry -notmatch '^((?:MAPPER_\w+|END_OF_MAPPERS))(?:\s*=\s*(\d+))?$') { throw "Unrecognized source enum entry: $entry" }
    $name = $Matches[1]
    if ($Matches[2]) { $enumValue = [int]$Matches[2] } else { $enumValue++ }
    $sourceValues[$name] = $enumValue
}
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$assemblies = @(
    (Join-Path $game 'BepInEx/plugins/000shcdese/SHCDESE.dll'),
    (Join-Path $workspace 'shcde-script-extender/src/SHCDESE.BepInEx/bin/net481/SHCDESE.dll')
)
foreach ($path in $assemblies) {
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
    try {
        $enum = $assembly.MainModule.Types | Where-Object FullName -eq 'SHCDESE.Interop.eMappers'
        foreach ($name in 'MAPPER_DRAWBRIDGE','MAPPER_GATE_STONE1A','MAPPER_GATE_STONE1B','MAPPER_GATE_STONE2A','MAPPER_GATE_STONE2B') {
            $field = $enum.Fields | Where-Object Name -eq $name
            if ($null -eq $field -or $field.Constant -ne $sourceValues[$name]) { throw "Enum source/assembly mismatch: $name in $path" }
        }
    } finally { $assembly.Dispose() }
}
$baselineRoot = Join-Path $workspace '_inspect/CrusaderDE-Native-Baseline'
$current = Get-Content (Join-Path $baselineRoot 'CURRENT.json') -Raw | ConvertFrom-Json
$nativeHash = (Get-FileHash -LiteralPath (Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll')).Hash
if ($nativeHash -ne $current.currentNativeHash) { throw 'Installed native baseline identity changed' }
$changedTextPaths = @(
    'CastlePlanner/src/BlueprintBuildingIconCatalog.cs',
    'CastlePlanner/src/BlueprintBuildingCaptureCatalog.cs',
    'CastlePlanner/src/BlueprintRenderer.cs',
    'Helpers/AIVParser/AIVParser.Tests/Program.cs',
    ('_inspect/CrusaderDE-Native-Baseline/' + $current.semanticDirectory + '/knowledge/DRAWBRIDGES.md'),
    '_inspect/Test-CastlePlannerDrawbridgePreflight.ps1'
)
foreach ($relative in $changedTextPaths) {
    $text = [IO.File]::ReadAllText((Join-Path $workspace $relative))
    if ($text -match '(?<!\r)\n|\r(?!\n)') { throw "Changed text is not CRLF: $relative" }
}
& (Join-Path $workspace 'Shared/Tools/Validation/Test-PermanentNativeRuntimePatches.ps1')
$addedRuntimeLines = @(git -C $workspace diff --unified=0 -- '*.cs' | Where-Object {
    $_.StartsWith('+') -and -not $_.StartsWith('+++')
})
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect workspace source changes' }
foreach ($line in $addedRuntimeLines) {
    if ($line -match '\b(?:Marshal\.Write\w*|CodePatch\.Write|VirtualProtect|FlushInstructionCache)\s*\(' -or
        $line -match '\.\s*(?:Enable|Disable|Apply|Undo|Dispose)\s*\(' -or
        $line -match '\b(?:OnDestroy|OnDisable|OnApplicationQuit)\s*\(') {
        throw "Review new workspace mutation/teardown before building: $line"
    }
}
Write-Output 'PASS: CastlePlanner JSON, lifecycle/publishers, plugin callbacks, XAML, enum parity, native identity and CRLF.'
Write-Output 'PASS: planar bridge direction; shared composite/depth/fallback mirroring; no new game members or native hooks.'
