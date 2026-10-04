[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$projectPath = Join-Path $PSScriptRoot 'EnemyBridgePathTest.csproj'
[xml]$project = [IO.File]::ReadAllText($projectPath)
$sources = @($project.SelectNodes("//*[local-name()='Compile']") | ForEach-Object {
    (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot $_.Include)).Path
})
$forbidden = 'JavaScriptSerializer|System\.Web\.Extensions|System\.Text\.Json|Newtonsoft|DataContractJsonSerializer|JsonUtility'
foreach ($path in @($projectPath) + $sources) {
    $text = [IO.File]::ReadAllText($path)
    if ($text -match $forbidden) { throw "Forbidden runtime JSON dependency: $path" }
    if ($text -match '\b(?:void|IEnumerator)\s+(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|Start|OnEnable)\s*\(' -or
        $text -match '\bStartCoroutine\s*\(') { throw "Runtime lifecycle callback requires audit: $path" }
    if ($text -match '\b(?:VirtualProtect|FlushInstructionCache)\s*\(|\bCodePatch\.Write\s*\(|\bMarshal\.Write\w*\s*\(|\.(?:Disable|Undo)\s*\(') {
        throw "Runtime executable mutation or published hook teardown requires audit: $path"
    }
}
foreach ($path in $sources) {
    if ($path -like '*EnemyBridgePathTest\src\*' -and [IO.File]::ReadAllText($path) -match '\.(?:Dispose|Apply)\s*\(') { throw "Published diagnostic runtime teardown: $path" }
}
# Shared lifecycle Pair.Dispose only releases observer tokens; the static plugin retains its Pair.
$hookSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgeNativeHooks.cs'))
if ([regex]::Matches($hookSource, '\.Enable\s*\(').Count -ne 1 -or
    $hookSource -notmatch 'foreach \(var hook in permanent\) hook\.Enable\(\);' -or
    $hookSource -notmatch 'AllowedSchemes\s*=\s*DetourScheme\.Absolute,') { throw 'Native hook publication contract changed; re-audit required.' }
$traceSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgeDecisionTrace.cs'))
$indexSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/BridgeBuildingIndex.cs'))
if ($traceSource -match 'params\s+int\[\]|Drain\(true\)|DescribeLiveParent' -or
    $indexSource -match 'TryGetBuildingById' -or
    $traceSource -notmatch 'count<64' -or $traceSource -notmatch 'backgroundQueued\)>4096') { throw 'Lean trace allocation/lookup/drain contract regression' }
if ($traceSource -match '667E8E00|SplitLiveState|Regex|PlanCell' -or
    $traceSource -notmatch 'ReadAssignedTask\(GameUnit\* unit\) => unit->r_AI_ContextTargetBuildingTileId' -or
    $traceSource -notmatch 'unit->r_AIState==0x65\|\|unit->r_AIState==0x67' -or
    $traceSource -notmatch 'WriteRecord\(StampRecord\(new Record \{Kind="session-end"') { throw 'Numeric capture/task-state/end-delivery contract regression' }
$installed = [Reflection.Assembly]::LoadFrom('E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\SHCDESE.dll')
$unitType = $installed.GetType('SHCDESE.Interop.GameUnit',$true)
foreach ($member in @(@('r_AI_ContextTargetBuildingTileId',0x3A4,'UInt32'),@('r_AIState',0x2BC,'UInt16'),@('r_AI_LastIssuedTribeCommand',0x398,'UInt16'))) {
    $field = $unitType.GetField($member[0])
    if (!$field -or !$field.IsPublic -or $field.FieldType.Name -ne $member[2] -or [Runtime.InteropServices.Marshal]::OffsetOf($unitType,$member[0]).ToInt32() -ne $member[1]) { throw ('Installed assignment member mismatch: '+$member[0]) }
}
$textFiles = @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch '\\(?:bin|obj|BepInEx)\\' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.md','.json','.xaml')
})
foreach ($file in $textFiles) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF file: $($file.FullName)" }
}
foreach ($file in @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File -Filter '*.xaml')) {
    [xml]$xaml = [IO.File]::ReadAllText($file.FullName)
    foreach ($content in $xaml.SelectNodes("//*[local-name()='Content']")) {
        $elements = @($content.ChildNodes | Where-Object NodeType -eq Element)
        if ($elements.Count -ne 1) { throw "XAML Content requires exactly one root: $($file.FullName)" }
    }
}
& (Join-Path $workspace 'Shared/Test-PermanentNativeRuntimePatches.ps1')
Write-Host "PASS: EnemyBridge runtime JSON/lifecycle, permanent hooks, XAML and CRLF ($($sources.Count) runtime sources)."
