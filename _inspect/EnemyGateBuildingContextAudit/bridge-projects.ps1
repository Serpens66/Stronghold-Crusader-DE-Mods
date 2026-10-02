$ErrorActionPreference = 'Stop'
function Read-Source($path) { [IO.File]::ReadAllText((Join-Path (Get-Location).Path $path)).Replace("`r`n","`n") }
function Write-Source($path,$value) {
    $target = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $path))
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    $expected = $value.Replace("`r`n","`n").Replace("`n","`r`n")
    [IO.File]::WriteAllText($target,$expected,[Text.UTF8Encoding]::new($false))
    if (![string]::Equals([IO.File]::ReadAllText($target),$expected,[StringComparison]::Ordinal)) { throw 'Mismatch' }
}
$path = 'Testmods/EnemyGatePathfindingTest/src/GateTopologySnapshotProvider.cs'; $text = Read-Source $path
$text = $text.Replace('                    if (building.r_BuildingType == eStructs.STRUCT_DRAWBRIDGE)' + "`n                    {`n                        signature = MixSignature(signature, (uint)(closure >> 32));`n                    }", '')
Write-Source $path $text
$path = 'BugfixesAndQoL/src/FriendlyMoatMovementRuntime.cs'; $text = Read-Source $path
$text = $text.Replace('BeginSearch("Attack", player)', 'BeginSearch("Attack", movementClass)')
Write-Source $path $text
$path = 'BugfixesAndQoL/src/AssassinPathfindingRuntime.cs'; $text = Read-Source $path
$text = $text.Replace('            internal object Token;', "            internal object Token;`n            internal IEnemyBridgePathObserver BridgeObserver;`n            internal object BridgeToken;")
$text = $text.Replace('            AssassinObservation previous = activeObservation;', "            IEnemyBridgePathObserver bridgeObserver = EnemyBridgeDiagnosticBridge.Current;`n            AssassinObservation previous = activeObservation;")
$text = $text.Replace('                if (observer != null)' + "`n                {", '                if (observer != null || bridgeObserver != null)' + "`n                {")
$text = $text.Replace('                            Token = observer.BeginAssassinSearch', '                            BridgeObserver = bridgeObserver,' + "`n                            Token = observer?.BeginAssassinSearch")
$needle = '                activeObservation = observation;'
$addition = @'
                if (observation != null && bridgeObserver != null)
                    try { observation.BridgeToken = bridgeObserver.BeginAssassinSearch(startX, startY, targetX, targetY,
                        maximumNodes, continuation, DescribeNativeAssassinState(context)); }
                    catch (Exception ex) { LogWarning("Bridge Assassin begin failed: " + ex.GetType().Name); }

'@
$text = $text.Replace($needle,$addition + $needle)
$text = $text.Replace('if (observation != null)' + "`n                    try { observer.", 'if (observation != null && observer != null)' + "`n                    try { observer.")
$text = $text.Replace('                activeObservation = previous;', @'
                activeObservation = previous;
                if (observation != null && bridgeObserver != null)
                    try { bridgeObserver.ObserveAssassinPolicyFiltering(observation.BridgeToken, observation.Player,
                        observation.FilteredGround, observation.FilteredClimb);
                        bridgeObserver.EndAssassinSearch(observation.BridgeToken, observation.Player,
                        observation.NativeResult, observation.EffectiveResult, observation.Outcome + observation.Error,
                        observation.CacheHit, observation.RouteLength); }
                    catch (Exception ex) { LogWarning("Bridge Assassin end failed: " + ex.GetType().Name); }
'@)
$text = $text.Replace('                    observation.Observer.ObserveAssassinEdge(observation.Token, player,', '                    observation.Observer?.ObserveAssassinEdge(observation.Token, player,')
$needle = '                        fromTile, toTile, direction, climb);'
$text = $text.Replace($needle, $needle + "`n                    try { observation.BridgeObserver?.ObserveAssassinEdge(observation.BridgeToken, player, fromTile, toTile, direction, climb); }`n                    catch (Exception ex) { LogWarning(`"Bridge Assassin edge failed: `" + ex.GetType().Name); }")
Write-Source $path $text
# New project has the same installed reference/lifecycle contracts, but no RedBird/native sources.
$bridge = 'Testmods/EnemyBridgePathTest'; $gate = 'Testmods/EnemyGatePathfindingTest'
$project = Read-Source "$gate/EnemyGatePathfindingTest.csproj"
$project = $project.Replace('EnemyGatePathfindingTest','EnemyBridgePathTest').Replace('E89FE701-3AD9-4F19-91D8-D85F7084F7D1','E0D1D26C-96B4-45D4-901E-910AC78FACB5')
$start = $project.IndexOf('    <Compile Include='); $end = $project.IndexOf('  </ItemGroup>', $start)
$sources = @'
    <Compile Include="src\EnemyBridgePathTestPlugin.cs" />
    <Compile Include="src\BridgeDiagnostics.cs" />
    <Compile Include="src\BridgeSnapshot.cs" />
    <Compile Include="src\DrawbridgeClosurePolicy.cs" />
    <Compile Include="src\GateEdgeOwnership.cs" />
    <Compile Include="..\..\Shared\PathDecisionAggregate.cs"><Link>Shared\PathDecisionAggregate.cs</Link></Compile>
    <Compile Include="..\..\Shared\DebugLogHelper.cs"><Link>Shared\DebugLogHelper.cs</Link></Compile>
    <Compile Include="..\..\Shared\GameplaySessionLifecycle.cs"><Link>Shared\GameplaySessionLifecycle.cs</Link></Compile>

'@
$project = $project.Remove($start,$end-$start).Insert($start,$sources)
Write-Source "$bridge/EnemyBridgePathTest.csproj" $project
$tests = @'
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" />
  <PropertyGroup>
    <OutputType>Exe</OutputType><AssemblyName>EnemyBridgePathTest.PolicyTests</AssemblyName>
    <TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion><LangVersion>latest</LangVersion>
    <OutputPath>tests\bin\</OutputPath>
  </PropertyGroup>
  <ItemGroup><Reference Include="System" /><Reference Include="System.Core" /></ItemGroup>
  <ItemGroup>
    <Compile Include="tests\Program.cs" /><Compile Include="tests\DrawbridgeClosureTests.cs" />
    <Compile Include="src\DrawbridgeClosurePolicy.cs" /><Compile Include="src\GateEdgeOwnership.cs" />
    <Compile Include="..\..\Shared\PathDecisionAggregate.cs" />
    <Compile Include="..\..\APIShared\src\EnemyGatePathPolicyBridge.cs" />
    <Compile Include="..\..\APIShared\src\EnemyBridgeDiagnosticBridge.cs" />
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
'@
Write-Source "$bridge/EnemyBridgePathTest.PolicyTests.csproj" $tests
Write-Source "$bridge/build.bat" (Read-Source "$gate/build.bat").Replace('EnemyGatePathfindingTest','EnemyBridgePathTest').Replace('Enemy Gate Pathfinding Test','Enemy Bridge Path Test')
$verify = Read-Source "$gate/verify.ps1"
$verify = $verify.Replace('EnemyGatePathfindingTest','EnemyBridgePathTest')
$start = $verify.IndexOf('$runtime ='); $end = $verify.IndexOf('$textFiles =', $start)
$verify = $verify.Remove($start,$end-$start)
Write-Source "$bridge/verify.ps1" $verify
Write-Source "$bridge/info.json" @'
{
  "GUID": "EnemyBridgePathTest_Serp",
  "Author": "Serpens66",
  "Name": "Enemy Bridge Path Test",
  "Description": "Read-only diagnosis of independent drawbridge crossings and AI work access. No movement policy or native hooks.",
  "Version": "0.1.0",
  "MinimumScriptExtenderVersion": "2.7.1",
  "MaximumScriptExtenderVersion": "",
  "Manifest": 1,
  "NetworkMode": 0,
  "SupportedGameVersions": ["2.8.0.1"]
}
'@
$path = '_inspect/APISharedTests/Program.cs'; $text = Read-Source $path
$text = $text.Replace('                "APIShared.EnemyGatePathPolicyBridge",', '                "APIShared.EnemyGatePathPolicyBridge",' + "`n                `"APIShared.IEnemyBridgePathObserver`",`n                `"APIShared.EnemyBridgeDiagnosticBridge`",")
Write-Source $path $text
# Exact targeted CRLF normalization of newly authored files.
foreach ($path in @('APIShared/src/EnemyBridgeDiagnosticBridge.cs', "$bridge/src/BridgeDiagnostics.cs", "$bridge/src/BridgeSnapshot.cs", "$bridge/src/EnemyBridgePathTestPlugin.cs", '_inspect/EnemyGateBuildingContextAudit/split-bridge.ps1', '_inspect/EnemyGateBuildingContextAudit/finish-bridge-split.ps1', '_inspect/EnemyGateBuildingContextAudit/bridge-projects.ps1')) {
    Write-Source $path (Read-Source $path)
}
