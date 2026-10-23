from pathlib import Path
import re
root=Path.cwd(); base=root/'Testmods/AIBuildDiagnoseTest'; special=root/'Testmods/CanarariWoodTest'
def read(p): return p.read_text(encoding='utf-8-sig')
def write(p,s):
    with p.open('w',encoding='utf-8',newline='') as f:f.write(s.replace('\r\n','\n').replace('\n','\r\n'))
def method(s,name):
    m=re.search(r'^        (?:private|internal|public) [^\n]*\b'+name+r'\(',s,re.M);assert m,name
    end=s.index('\n        }',m.start())+len('\n        }');return s[m.start():end]+'\n\n'
p=base/'src/AIBuildDiagnoseRuntime.cs';s=read(p).replace('            probeSession = probePending = probeRunning = false;\n','')
wall=method(s,'IsActualWall').replace('private static','internal static')
write(p,s)
p=base/'src/Diagnostics/NearbyWoodEvidence.cs';s=read(p).replace('using System;','using System;\nusing System.Text;')
s=s.replace('        internal static readonly int[] NearbyDx','        private const uint NativePlacementReservationFlag = 0x4;\n'+wall+'        internal static readonly int[] NearbyDx')
write(p,s)
p=special/'src/CanarariWoodRuntime.cs';s=read(p).replace('using SHCDESE.Interop.Enums;','using SHCDESE.Interop.Enums;\nusing SHCDESE.Interop;')
write(p,s)

# Keep the established native and source contract checks, assigning experiment
# contracts to the companion verifier. The general verifier is independent of it.
old=read(base/'verify.ps1')
write(root/'_inspect/AIBuildSplit-20261016/original-verify.ps1.txt',old)
def block(start,end):return old[old.index(start):old.index(end,old.index(start))]
general=old[:old.index("if ($runtimeText -match 'Marshal\\.Write(?!")]
general+='''if ($runtimeText -match 'Marshal\\.Write|CreatePrefab\\(|DeleteBuildingSafe\\(|FixesFarmFilterOverride\\.Begin|Config\\.Bind\\("(?:PlacementProbe|NearbyWoodTest|FarmContractProbe|CanariFarmSwap)' ) {
    throw 'The general diagnostic must not contain active probe actions or settings.'
}
$generalRuntime = [IO.File]::ReadAllText((Join-Path $project 'src\\AIBuildDiagnoseRuntime.cs'))
if ($generalRuntime -match 'test_canari|rat_farm_site_contract|ProbePlayer|ProbeX|ProbeY|69 \\* 160 \\+ 97' -or
    $generalRuntime -notmatch 'woodShadowDone\\[record.PlayerId\\]' -or
    $generalRuntime -notmatch 'active = !session.IsEditor && !session.IsReplay') {
    throw 'General observation contains a scenario-specific restriction or lost per-player coverage.'
}
'''
grid=block("if ($runtimeText -notmatch 'MissionEvents", "if ($runtimeText -notmatch 'WoodSearchDx")
grid=re.sub(r"    \$runtimeText -notmatch '(?:probeSession|placementProbeEnabled|Config).*? -or\n",'',grid)
general+=grid
general+=block("if ($runtimeText -notmatch 'WoodSearchDx","if ($runtimeText -notmatch 'NearbyDx")
raw=block("if ($runtimeText -notmatch 'NativePlacementReservationFlag",'$farmProbe =')
raw=raw.replace("    $runtimeText -notmatch 'nearbyDetailedByPlayer\\[playerId\\]' -or\n",'')
general+=raw
general+='''if ($runtimeText -notmatch 'lock \\(appleFarmsSync\\) return appleFarms\\.ToArray\\(\\)' -or
    $runtimeText -notmatch 'initialAppleFarmScanComplete = true' -or
    $runtimeText -notmatch '!initialAppleFarmScanComplete\\) return' -or
    $runtimeText -match 'foreach \\(AppleFarmWatch farm in appleFarms\\)') { throw 'Farm observer concurrency contract differs.' }
'''
publisher=block('$coarseNative =','foreach ($path in $textFiles)')
publisher=publisher.replace("    $runtimeText -notmatch 'candidate\\.ShouldDeferCanariWoodBuild' -or\n",'')
general+=publisher
general+=old[old.index('foreach ($path in $textFiles)'):]
write(base/'verify.ps1',general)

companion='''$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = [IO.Path]::GetFullPath((Join-Path $project '..\\..'))
$general = Join-Path $workspace 'Testmods\\AIBuildDiagnoseTest'
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -File -Recurse -Filter '*.cs')
$runtimeText = (($sourceFiles | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) +
    [IO.File]::ReadAllText((Join-Path $general 'src\\Diagnostics\\NearbyWoodEvidence.cs'))) -join "`n"
$functions = Join-Path $workspace '_inspect\\CrusaderDE-Native-Baseline\\sem\\FBCB9319\\exports\\semantic-functions.jsonl'
$plugin = [IO.File]::ReadAllText((Join-Path $project 'src\\CanarariWoodPlugin.cs'))
if ($runtimeText -match 'HookTransaction|NativeDetour|VirtualProtect|CodePatch\\.Write|X64InlineHook|Newtonsoft|System\\.Text\\.Json|JavaScriptSerializer|JsonUtility' -or
    $runtimeText -match '\\b(OnDestroy|OnDisable|Update|LateUpdate|FixedUpdate|StartCoroutine)\\s*\\(') { throw 'Unexpected companion hook, JSON or lifecycle ownership.' }
if ($runtimeText -notmatch 'namedCopy = active && session.IsLoadedSave' -or
    $runtimeText -notmatch 'probeSession = namedCopy && placementProbeEnabled' -or
    $runtimeText -notmatch 'nearbyCopySession = namedCopy && nearbyWoodTestEnabled') { throw 'Disposable copy gate missing.' }
'''
companion+=block("if ($runtimeText -notmatch 'NearbyDx", "if ($runtimeText -notmatch 'NativePlacementReservationFlag")
companion+=block('$farmProbe =','$coarseNative =')
companion=companion.replace("$plugin = [IO.File]::ReadAllText((Join-Path $project 'src\\AIBuildDiagnosePlugin.cs'))", "$plugin = [IO.File]::ReadAllText((Join-Path $project 'src\\CanarariWoodPlugin.cs'))")
companion=companion.replace("fixesFarmFilterOverride = FixesFarmFilterOverride\\.Begin\\(log\\)","comparison = FixesFarmFilterOverride\\.Begin\\(log, suppress\\)")
companion=companion.replace("fixesFarmFilterOverride\\.Complete", "comparison\\.Complete")
companion=companion.replace('vanillaComparisonReady && canariFarmSwapEnabled','ready && swap').replace('vanillaComparisonReady && farmContractProbeEnabled','ready && farms')
for pattern in ["    $runtimeText -notmatch 'lock \\(appleFarmsSync\\) return appleFarms\\.ToArray\\(\\)' -or\n",
                "    $runtimeText -notmatch 'initialAppleFarmScanComplete = true' -or\n",
                "    $runtimeText -notmatch '!initialAppleFarmScanComplete\\) return' -or\n"]:
    companion=companion.replace(pattern,'')
companion+='''if ($plugin -notmatch 'Config\\.Bind\\("FixesFarmFilterOverride", "Enabled", false' -or
    $fixesOverride -notmatch 'if \\(!suppress && original\\)') { throw 'Fixes suppression must be explicitly enabled.' }
foreach ($file in @($sourceFiles) + @(Get-Item (Join-Path $project 'CanarariWoodTest.csproj'), (Join-Path $project 'info.json'), (Join-Path $project 'build.bat'), $MyInvocation.MyCommand.Path)) {
    $content = [IO.File]::ReadAllText($file.FullName)
    if ($content -match '(?<!\\r)\\n|\\r(?!\\n)') { throw "Non-CRLF: $($file.FullName)" }
}
Write-Host 'PASS: companion owns opt-in copy-only experiments, bounded overlay/restoration and explicit Fixes override; no native hooks.'
'''
write(special/'verify.ps1',companion)

# Tests compile the real companion runtime alongside the diagnostic contracts.
p=base/'tests/Diagnostics.Tests/Diagnostics.Tests.csproj';s=read(p)
s=s.replace('    <Reference Include="BepInEx">','    <Reference Include="APIShared"><HintPath>$(ApiSharedDir)\\APIShared.dll</HintPath></Reference>\n    <Reference Include="R3"><HintPath>$(ExtenderDir)\\R3.dll</HintPath></Reference>\n    <Reference Include="BepInEx">')
s=s.replace('    <Compile Include="../../src/Diagnostics/*.cs"','    <Compile Include="../../../CanarariWoodTest/src/CanarariWoodRuntime.cs" Link="Companion/CanarariWoodRuntime.cs" />\n    <Compile Include="../../../CanarariWoodTest/src/FarmSiteContractProbe.cs" Link="Companion/FarmSiteContractProbe.cs" />\n    <Compile Include="../../../CanarariWoodTest/src/CanariFarmSwapProbe.cs" Link="Companion/CanariFarmSwapProbe.cs" />\n    <Compile Include="../../../../Shared/Adapters/APIShared/MissionEventsAdapter.cs" Link="Production/MissionEventsAdapter.cs" />\n    <Compile Include="../../src/Diagnostics/*.cs"')
s=s.replace('    <ExtenderDir Condition=', '    <ApiSharedDir Condition="\'$(ApiSharedDir)\' == \'\'">$(GameDir)\\BepInEx\\plugins\\APIShared_Serp</ApiSharedDir>\n    <ExtenderDir Condition=')
write(p,s)
p=base/'build.bat';s=read(p).replace('/p:ExtenderDir="%EXTENDER_DIR%" --logger','/p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%" --logger')
write(p,s)
p=base/'tests/Diagnostics.Tests/ObservationTests.cs';s=read(p)
s=s.replace('TryRegisterWoodBuildGate(AIBuildDiagnosePluginGuid,','TryRegisterWoodBuildGate("CanarariWoodTest_Serp",')
s=s.replace('            typeof(AiBuildObservation).GetField','            typeof(AiBuildExperimentBridge).GetField("observer", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);\n            typeof(AiBuildObservation).GetField')
insertion='''        [TestMethod]
        public void CompanionIsOptionalExclusiveAndCannotClaimGeneralObserverOwnership()
        {
            Action<AiBuildDiagnosticRecord> callback = _ => { };
            Assert.IsFalse(AiBuildExperimentBridge.TryRegister("CanarariWoodTest_Serp", callback, out _));
            Observe(_ => { });
            Assert.IsFalse(AiBuildExperimentBridge.TryRegister(AIBuildDiagnosePluginGuid, callback, out _));
            Assert.IsTrue(AiBuildExperimentBridge.TryRegister("CanarariWoodTest_Serp", callback, out _));
            Assert.IsTrue(AiBuildExperimentBridge.TryRegister("CanarariWoodTest_Serp", callback, out _));
            Assert.IsFalse(AiBuildExperimentBridge.TryRegister("CanarariWoodTest_Serp", _ => { }, out _));
            Assert.IsFalse(AiBuildDiagnostic.TryRegister("CanarariWoodTest_Serp", callback, out _));
            Assert.IsFalse(AiBuildDiagnostic.TryRegisterWoodBuildGate(AIBuildDiagnosePluginGuid, _ => true, out _));
            Assert.IsFalse(AiBuildDiagnostic.TryRegisterNearbyWoodOverlay(AIBuildDiagnosePluginGuid, (s,p,x,y) => null, out _));
        }
        [TestMethod]
        public void CompanionDeliveryIsIndependentAndContainsExceptions()
        {
            Observe(_ => { });
            int calls = 0;
            Assert.IsTrue(AiBuildExperimentBridge.TryRegister("CanarariWoodTest_Serp", _ =>
                { calls++; throw new InvalidOperationException("companion"); }, out _));
            AiBuildExperimentBridge.Deliver(new AiBuildDiagnosticRecord("test", 2, 0, 0, 0, 0, 0), null);
            AiBuildExperimentBridge.Deliver(new AiBuildDiagnosticRecord("test", 3, 0, 0, 0, 0, 0), null);
            Assert.AreEqual(2, calls);
            Assert.IsFalse(AiBuildDiagnostic.ShouldDeferWoodBuild(2));
        }
'''
s=s.replace('        private const string AIBuildDiagnosePluginGuid',insertion+'        private const string AIBuildDiagnosePluginGuid')
write(p,s)
