$ErrorActionPreference = 'Stop'
$workspace = (Get-Location).Path
function Write-Source([string]$path, [string]$value) {
    $target = [IO.Path]::GetFullPath((Join-Path $workspace $path))
    if (!$target.StartsWith($workspace + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Outside workspace' }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    $expected = $value.Replace("`r`n","`n").Replace("`n","`r`n")
    [IO.File]::WriteAllText($target,$expected,[Text.UTF8Encoding]::new($false))
    if (![string]::Equals([IO.File]::ReadAllText($target),$expected,[StringComparison]::Ordinal)) { throw 'Write mismatch' }
}
function Read-Source($path) { [IO.File]::ReadAllText((Join-Path $workspace $path)).Replace("`r`n","`n") }
function Method($text,$name) {
    $match = [regex]::Match($text, '(?m)^        private [^\n]*\b' + $name + '\(')
    if (!$match.Success) { throw "No definition $name" }
    $brace = $text.IndexOf('{',$match.Index); $end = $brace+1; $depth=1
    while ($depth -gt 0) { if ($text[$end] -eq '{') { $depth++ }; if ($text[$end] -eq '}') { $depth-- }; $end++ }
    return $text.Substring($match.Index,$end-$match.Index)
}
$path = 'Testmods/EnemyGatePathfindingTest/src/GateTopologySnapshotProvider.cs'
$original = (git show HEAD:Testmods/EnemyGatePathfindingTest/src/GateTopologySnapshotProvider.cs) -join "`n"
$text = Read-Source $path
foreach ($name in @('ComputeTopologySignature','BuildRoutePolicySnapshot')) {
    $method = Method $original $name
    if ($name -eq 'ComputeTopologySignature') {
        $method = [regex]::Replace($method,'                        bool validClosure = TryReadDrawbridgeClosure[^\n]*\n[^\n]*\n[^\n]*\n','')
    } else {
        $method = $method.Replace('bool isBridge = info.BridgeId > 0 && info.GateId > 0;','// Bridge linkage supplies gate identity and axis only.')
        $method = $method.Replace('if (!isGate && !isBridge)','if (!isGate)')
        $method = $method.Replace('out bool horizontalPassage, out PassageAxisSource axisSource) || isBridge)','out bool horizontalPassage, out PassageAxisSource axisSource))')
        $method = [regex]::Replace($method,'                        else\n                            changedEdges = ClearDrawbridgePassageDirections\([\s\S]*?out sampleFrom2, out sampleTo2, out sampleDirection2\);\n','')
        $method = $method.Replace('isGate ? "entry-exit-outer" : "native-closure-cells"','"entry-exit-outer"')
        $method = $method.Replace('                        if (isGate)' + "`n",'')
    }
    $text = $text.Replace('        private static bool TryResolvePassageAxis(', $method + "`n`n        private static bool TryResolvePassageAxis(")
}
foreach ($name in @('ClearDrawbridgePassageDirections','TryReadDrawbridgeClosure')) { $text = $text.Replace((Method $text $name),'') }
Write-Source $path $text
$path = 'Testmods/EnemyGatePathfindingTest/src/AttackOrderCorrelationDiagnostics.cs'
$text = Read-Source $path
$text = $text.Replace('frame.TargetX, frame.TargetY, "bridgeId=" +' + "`n" + '                (frame.Probe.Snapshot.EdgeOwners?[player]?.ResolveBridge(fromTile, direction) ?? 0) +' + "`n" + '                ",from="', 'frame.TargetX, frame.TargetY, "from="')
Write-Source $path $text
# Read-only observations use the existing mainmod detours.
$path = 'BugfixesAndQoL/src/MovementPathPublication.cs'; $text = Read-Source $path
$text = $text.Replace('            int result = 0;', '            object bridgeSearch = EnemyBridgeDiagnosticBridge.BeginSearch("builder", movementClass);' + "`n            int result = 0;")
$text = $text.Replace('                    completed, result > 0);','                    completed, result > 0);' + "`n                EnemyBridgeDiagnosticBridge.EndSearch(bridgeSearch, completed, result);")
$text = $text.Replace('originalPathBuilder(pathManager, movementClass, movementProfile)','EnemyBridgeDiagnosticBridge.NativeResult(originalPathBuilder(pathManager, movementClass, movementProfile))')
Write-Source $path $text
$path = 'BugfixesAndQoL/src/MovementSearchContext.cs'; $text = Read-Source $path
$text = $text.Replace('            int gateResult = 0;', '            object bridgeSearch = EnemyBridgeDiagnosticBridge.BeginSearch("reconstructed-builder", plan?.PlayerId ?? -1);' + "`n            int gateResult = 0;")
$text = $text.Replace('                    gateCompleted, gateResult > 0);','                    gateCompleted, gateResult > 0);' + "`n                EnemyBridgeDiagnosticBridge.EndSearch(bridgeSearch, gateCompleted, gateResult);")
Write-Source $path $text
$path = 'BugfixesAndQoL/src/FriendlyMoatMovementRuntime.cs'; $text = Read-Source $path
$text = $text.Replace('                out int vanillaResult);' + "`n            // The already-owned", '                out int vanillaResult);' + "`n            EnemyBridgeDiagnosticBridge.ObserveRegion(movementClass, sourceRegion, targetRegion, routeKind, vanillaResult, result);`n            // The already-owned")
# The void approach functions report completion, never an invented route success.
foreach ($row in @(@('ObserveAttackApproachFloodBuilder','Attack','player'),@('ObserveBuildingApproachBuilder','BuildingApproach','movementClass'),@('ObserveBuildingCandidateConsumer','BuildingConsumer','-1'))) {
    $method = Method $text $row[0]
    $changed = $method.Replace('            try' + "`n            {", '            object bridgeSearch = EnemyBridgeDiagnosticBridge.BeginSearch("' + $row[1] + '", ' + $row[2] + ');' + "`n            bool bridgeCompleted = false;`n            try`n            {")
    # Set completion just before the wrapper's finally, after the existing call returned.
    $changed = $changed.Replace("            }`n            finally", "                bridgeCompleted = true;`n            }`n            finally")
    $changed = $changed.Replace('            finally' + "`n            {", '            finally' + "`n            {`n                EnemyBridgeDiagnosticBridge.EndSearch(bridgeSearch, bridgeCompleted, 0);")
    $text = $text.Replace($method,$changed)
}
Write-Source $path $text
