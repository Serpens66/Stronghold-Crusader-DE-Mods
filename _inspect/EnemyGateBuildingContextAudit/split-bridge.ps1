$ErrorActionPreference = 'Stop'
$workspace = (Get-Location).Path
function Write-Source([string]$path, [string]$value) {
    $target = [IO.Path]::GetFullPath((Join-Path $workspace $path))
    if (!$target.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Outside workspace' }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    $expected = $value.Replace("`r`n", "`n").Replace("`n", "`r`n")
    [IO.File]::WriteAllText($target, $expected, [Text.UTF8Encoding]::new($false))
    if (![string]::Equals([IO.File]::ReadAllText($target), $expected, [StringComparison]::Ordinal)) { throw "Write mismatch $target" }
    if ($expected -match '(?<!\r)\n|\r(?!\n)') { throw "Invalid newline $target" }
}
function Read-Source([string]$path) { [IO.File]::ReadAllText((Join-Path $workspace $path)).Replace("`r`n", "`n") }
function Replace-One([string]$path, [string]$old, [string]$new) {
    $text = Read-Source $path
    $old = $old.Replace("`r`n", "`n"); $new = $new.Replace("`r`n", "`n")
    if (!$text.Contains($old)) { throw "Missing replacement in $path : $old" }
    Write-Source $path $text.Replace($old, $new)
}
function Remove-Method([string]$text, [string]$name) {
    $start = $text.LastIndexOf('        private ', $text.IndexOf($name + '('), [StringComparison]::Ordinal)
    $brace = $text.IndexOf('{', $start); $depth = 1; $end = $brace + 1
    while ($depth -gt 0) { if ($text[$end] -eq '{') { $depth++ }; if ($text[$end] -eq '}') { $depth-- }; $end++ }
    return $text.Remove($start, $end - $start)
}
# Transfer the experiment before removing the production linkage.
$gate = 'Testmods/EnemyGatePathfindingTest'
$bridge = 'Testmods/EnemyBridgePathTest'
Write-Source "$bridge/src/DrawbridgeClosurePolicy.cs" (Read-Source "$gate/src/DrawbridgeClosurePolicy.cs").Replace('namespace EnemyGatePathfindingTest','namespace EnemyBridgePathTest')
$ownership = Read-Source "$gate/src/GateEdgeOwnership.cs"
Write-Source "$bridge/src/GateEdgeOwnership.cs" ($ownership.Substring(0,$ownership.IndexOf('    // All edges count;')).Replace('namespace EnemyGatePathfindingTest','namespace EnemyBridgePathTest').TrimEnd() + "`n}`n")
# Share the pure aggregate source; no assembly dependency on the gate test.
Write-Source 'Shared/PathDecisionAggregate.cs' (Read-Source "$gate/src/AiGateDecisionAggregate.cs")
foreach ($project in @('EnemyGatePathfindingTest.csproj','EnemyGatePathfindingTest.PolicyTests.csproj')) {
    $text = Read-Source "$gate/$project"
    $text = $text -replace '    <Compile Include="(?:src\\DrawbridgeClosurePolicy|tests\\DrawbridgeClosureTests)\.cs" />\n', ''
    $text = $text.Replace('<Compile Include="src\AiGateDecisionAggregate.cs" />','<Compile Include="..\..\Shared\PathDecisionAggregate.cs"><Link>Shared\PathDecisionAggregate.cs</Link></Compile>')
    Write-Source "$gate/$project" $text
}
$path = "$gate/src/GateTopologySnapshotProvider.cs"
$text = Read-Source $path
$text = Remove-Method $text 'ClearDrawbridgePassageDirections'
$text = Remove-Method $text 'TryReadDrawbridgeClosure'
$text = Remove-Method $text 'BuildBridgeCandidates'
$text = [regex]::Replace($text, '        internal string DescribeBridgePclCandidates\([\s\S]*?\n        }\n', '')
$text = $text.Replace('if (!isGate && !isBridge)', 'if (!isGate)')
$text = $text.Replace('out bool horizontalPassage, out PassageAxisSource axisSource) || isBridge)', 'out bool horizontalPassage, out PassageAxisSource axisSource))')
$text = [regex]::Replace($text, '                        else\n                            changedEdges = ClearDrawbridgePassageDirections\([\s\S]*?out sampleFrom2, out sampleTo2, out sampleDirection2\);\n', '')
$text = $text.Replace('bool isBridge = info.BridgeId > 0 && info.GateId > 0;', '// Bridge linkage resolves gate axes; it never contributes masked edges.')
$text = $text.Replace('isGate ? "entry-exit-outer" : "native-closure-cells"', '"entry-exit-outer"')
$text = [regex]::Replace($text, '            HashSet<int> closedCells = null;[\s\S]*?diagnostics = BuildTileDiagnostics\(tiles, footprint, closedCells\);', '            diagnostics = BuildTileDiagnostics(tiles, footprint);')
$text = [regex]::Replace($text, '                        bool validClosure = TryReadDrawbridgeClosure[^\n]*\n[^\n]*\n[^\n]*\n', '')
$text = $text.Replace(', HashSet<int> closedCells = null', '')
$text = $text.Replace(', closedCells?.Contains(tileId) == true', '')
$text = $text.Replace('                        hash = (hash ^ (tile.ClosedBridgeCell ? 1u : 0u)) * 1099511628211UL;' + "`n", '')
$text = $text.Replace('tile.TileId * 2 + (tile.ClosedBridgeCell ? 1 : 0)', 'tile.TileId')
$text = $text.Replace('; BuildBridgeCandidates();', ';')
$text = [regex]::Replace($text, '            internal readonly Dictionary<long, string> BridgeCandidates =[^\n]*\n', '')
$text = $text.Replace(', bool closedBridgeCell = false', '')
$text = $text.Replace('; ClosedBridgeCell = closedBridgeCell', '')
$text = $text.Replace('            internal bool ClosedBridgeCell { get; }' + "`n", '')
$text = $text.Replace(' + ":closureCell=" + (ClosedBridgeCell ? 1 : 0)', '')
Write-Source $path $text
$path = "$gate/src/AttackOrderCorrelationDiagnostics.cs"
$text = Read-Source $path
$text = $text.Replace('            internal long SamePclAccepts, RegionAccepts;' + "`n", '')
$text = [regex]::Replace($text, '            totals.Record\(player, 0, "route-after-reachability",[\s\S]*?"attribution=short-lived-order-context,rejectedEdges=" \+ rejectedEdges\);\n', '')
$text = [regex]::Replace($text, '                if \(effectiveResult > 0 && frame != null\)[\s\S]*?                totals.Record\(player, 0, "region-pair",', '                totals.Record(player, 0, "region-pair",')
$text = [regex]::Replace($text, '                parent\.(?:SamePclAccepts|RegionAccepts) \+= frame\.(?:SamePclAccepts|RegionAccepts);\n', '')
$text = [regex]::Replace($text, '\+ ",bridge=" \+\n\s*\(frame.Probe.Snapshot.EdgeOwners\?\[player\]\?\.ResolveBridge\(fromTile, direction\) \?\? 0\)', '')
Write-Source $path $text
$ownership = $ownership -replace '        private readonly Dictionary<long, int> bridges =[^\n]*\n',''
$ownership = $ownership.Replace(', int bridgeId = 0','')
$ownership = [regex]::Replace($ownership, '            if \(bridges.TryGetValue[\s\S]*?else if \(!bridges.ContainsKey\(key\)\) bridges.Add\(key, bridgeId\);\n', '')
$ownership = [regex]::Replace($ownership, '        internal int ResolveBridge[^;]*;\n', '')
Write-Source "$gate/src/GateEdgeOwnership.cs" $ownership
$tests = Read-Source "$gate/tests/DrawbridgeClosureTests.cs"
# Only pure hypothetical bridge tests migrate; active gate-policy cache tests remain covered in gate suite.
$tests = $tests.Replace('namespace EnemyGatePathfindingTest','namespace EnemyBridgePathTest')
$start = $tests.IndexOf('                var snapshots =')
$end = $tests.IndexOf('                // Several bridges', $start)
$tests = $tests.Remove($start,$end-$start).Insert($start,@'
                foreach (int destination in new[] { Tile(6,6), Tile(10,6), Tile(2,10) })
                    Check(Cost(Tile(2,6), destination, (a,b,d) => !closed.Contains(a) && !closed.Contains(b)) ==
                        Cost(Tile(2,6), destination, (a,b,d) => (masks[a] & (1 << d)) != 0),
                        "hypothetical mask matches independent physical closure");

'@)
$start = $tests.IndexOf('            string provider =')
$end = $tests.IndexOf('            var aggregate =', $start)
$tests = $tests.Remove($start,$end-$start)
$tests = $tests.Replace('new AiGateDecisionAggregate()', 'new EnemyGatePathfindingTest.AiGateDecisionAggregate()')
Write-Source "$bridge/tests/DrawbridgeClosureTests.cs" $tests
$path = "$gate/tests/Program.cs"
$text = Read-Source $path
$text = $text.Replace('                assertions += DrawbridgeClosureTests.Run();' + "`n", '')
$text = [regex]::Replace($text, '            string bridge = ExtractMethodBody\(topology, "ClearDrawbridgePassageDirections"\);[\s\S]*?"drawbridge isolates native closure cells independently of gate axis"\);', @'
            Assert(!topology.Contains("ClearDrawbridgePassageDirections") &&
                !topology.Contains("ClosedBridgeCell") && topology.Contains("if (!isGate)"),
                "gate policy cannot mask any bridge edges, including the old center seam");
'@)
Write-Source $path $text
Replace-One "$gate/info.json" 'enemy gatehouses and drawbridges' 'enemy gatehouse passages'
# Authorized migration: remove replaced runtime/test sources, no backup/fallback copies.
foreach ($relative in @("$gate/src/DrawbridgeClosurePolicy.cs", "$gate/tests/DrawbridgeClosureTests.cs", "$gate/src/AiGateDecisionAggregate.cs")) {
    $target = [IO.Path]::GetFullPath((Join-Path $workspace $relative))
    if (!$target.StartsWith((Join-Path $workspace $gate),[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid removal' }
    Remove-Item -LiteralPath $target
}
Replace-One 'APIShared/APIShared.csproj' '<Compile Include="src\Pathfinding\EnemyGatePathPolicyBridge.cs" />' ('<Compile Include="src\Pathfinding\EnemyGatePathPolicyBridge.cs" />' + "`n    <Compile Include=`"src\Pathfinding\EnemyBridgeDiagnosticBridge.cs`" />")
Replace-One 'BugfixesAndQoL/src/FriendlyMoatMovementRuntime.cs' '            return result;' '            return result;'
Write-Host 'PASS: source migration and gate-only policy edits completed'
