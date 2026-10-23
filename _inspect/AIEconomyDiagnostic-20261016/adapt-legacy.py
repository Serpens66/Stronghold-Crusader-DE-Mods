from pathlib import Path

root = Path.cwd()
def edit(path, fn):
    p = root / path
    text = fn(p.read_text(encoding='utf-8-sig'))
    p.write_bytes(text.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8'))

def economy(s):
    s = s.replace('using BugfixesAndQoL.Diagnostics;\n', '')
    s = s.replace('        private readonly Dictionary<uint, string> pendingRemoval = new Dictionary<uint, string>();\n', '')
    s = s.replace('        private EconomyNativeHooks native;','        private readonly SpectatorAvailabilityConnection availability;\n        internal bool ResourceOwnerReady => availability?.Ready == true;\n        private EconomyNativeHooks native;')
    start = s.index('            bool registered = AiEconomyObservation.TryRegister(')
    end = s.index('            if (!MarketPriceEvents.TryRegister(', start)
    s = s[:start] + '''            availability = new SpectatorAvailabilityConnection((player, mapper, feedback) =>
            {
                var op = SafeBegin("build-resources", player, mapper, feedback);
                return op == null ? null : (Action<int?, bool>)((result, evaluated) =>
                { op.NativeOriginalEvaluated = evaluated; SafeEnd(op, result); });
            });
            if (Chainloader.PluginInfos.ContainsKey("SpectatorEditorBuildTest_Serp"))
                Coverage("resource-owner-subscriber", availability.Registered, availability.Detail);
            Coverage("protection-decisions", false,
                "unchanged installed Bugfix 1.0.192 has no per-decision publisher; emergency/hover protection decisions and accessibility classification unavailable; hovel removal context unavailable");
''' + s[end:]
    s = s.replace('pendingRemoval.Clear(); ', '')
    s = s.replace('AiEconomyObservation.AvailabilityReady', 'ResourceOwnerReady')
    s = s.replace('AiEconomyObservation.Status()', 'ProtectionSnapshot.Capture()')
    s = s.replace('AiEconomyObservation.CallbackFailures', 'availability.CallbackFailures')
    s = s.replace('pendingRemoval={pendingRemoval.Count}', 'pendingDeletionCalls={delayedDeletion.Count}')
    start = s.index('        private void OnProtection(')
    end = s.index('        private void OnPrice(', start)
    s = s[:start] + s[end:]
    s = s.replace('''                    Emit("INVENTORY_REMOVAL", old.Value.Player, 0, $"{old.Value}, observedBetweenSnapshots=true, cause=unknown, " +
                        $"priorDecision={(pendingRemoval.TryGetValue(old.Value.GlobalId, out string mark) ? mark : "none")}; priorDecisionIsNotCausalProof=true");
                    pendingRemoval.Remove(old.Value.GlobalId);''', '''                    Emit("INVENTORY_REMOVAL", old.Value.Player, 0, $"{old.Value}, observedBetweenSnapshots=true, cause=unknown; protectionDecisionCoverage=unavailable");''')
    s = s.replace('''$"{id}, cause={cause}, priorMark={(pendingRemoval.TryGetValue(id.GlobalId, out string mark) ? mark : "none")}; priorMarkIsNotCausalProof=true"''', '''$"{id}, cause={cause}; protectionDecisionCoverage=unavailable"''')
    s = s.replace('''                $"priorDecision={(pendingRemoval.TryGetValue(id.GlobalId, out string prior) ? prior : "none")}; " +
''', '')
    s = s.replace('delayedDeletion.Remove(id.GlobalId); pendingRemoval.Remove(id.GlobalId);', 'delayedDeletion.Remove(id.GlobalId);')
    s = s.replace('            Snapshot(phase);', '            Change("protection-settings", "SETTINGS", 0, 0, ProtectionSnapshot.Capture());\n            Snapshot(phase);')
    assert 'AiEconomyObservation' not in s and 'pendingRemoval' not in s
    return s.replace('emergency/hover protection', 'emergency/hovel protection')

edit('Testmods/AIBuildDiagnoseTest/src/Diagnostics/EconomyDiagnostics.cs', economy)
edit('Testmods/AIBuildDiagnoseTest/src/Diagnostics/EconomyNativeHooks.cs', lambda s: s.replace('BugfixesAndQoL.Diagnostics.AiEconomyObservation.AvailabilityReady','owner.ResourceOwnerReady'))
edit('Testmods/SpectatorEditorBuildTest/src/SpectatorEditorBuildTestPlugin.Native.cs', lambda s: s.replace('BugfixesAndQoL.Diagnostics.AiEconomyObservation.ReportAvailabilityReady()', 'AvailabilityObservation.ReportReady()').replace('BugfixesAndQoL.Diagnostics.AiEconomyObservation.BeginAvailability(', 'AvailabilityObservation.Begin('))
edit('Testmods/SpectatorEditorBuildTest/SpectatorEditorBuildTest.csproj', lambda s: '\n'.join(line for line in s.split('\n') if '<Reference Include="BugfixesAndQoL">' not in line))
for path in ['Testmods/AIBuildDiagnoseTest/info.json','Testmods/AIBuildDiagnoseTest/src/AIBuildDiagnosePlugin.cs','Testmods/SpectatorEditorBuildTest/info.json','Testmods/SpectatorEditorBuildTest/src/SpectatorEditorBuildTestPlugin.cs']:
    edit(path, lambda s:s.replace('1.0.193','1.0.192'))

def project(s):
    s=s.replace('  <ItemGroup>\n    <Reference Include="System" />', '  <PropertyGroup Condition="\'$(BugfixesDir)\' == \'\'"><BugfixesDir>$(GameDir)\\BepInEx\\plugins\\SerpsMods_Serp\\Mods\\BugfixesAndQoL_Serp</BugfixesDir></PropertyGroup>\n  <ItemGroup>\n    <Reference Include="System" />',1)
    s=s.replace('..\\..\\BugfixesAndQoL\\BepInEx\\plugins\\BugfixesAndQoL_Serp\\BugfixesAndQoL.dll', '$(BugfixesDir)\\BugfixesAndQoL.dll')
    s=s.replace('    <Error Condition="!Exists(\'$(ExtenderDir)', '    <Error Condition="!Exists(\'$(BugfixesDir)\\BugfixesAndQoL.dll\')" Text="The unchanged installed BugfixesAndQoL 1.0.192 package is required." />\n    <Error Condition="!Exists(\'$(ExtenderDir)',1)
    return s
edit('Testmods/AIBuildDiagnoseTest/AIBuildDiagnoseTest.csproj', project)

def tests(s):
    s=s.replace('    <TargetFramework>net481</TargetFramework>', '    <TargetFramework>net481</TargetFramework>\n    <AssemblyName>AIBuildDiagnoseTest</AssemblyName>')
    s=s.replace('    <Reference Include="APIShared">', '    <Reference Include="BugfixesAndQoL"><HintPath>$(GameDir)\\BepInEx\\plugins\\SerpsMods_Serp\\Mods\\BugfixesAndQoL_Serp\\BugfixesAndQoL.dll</HintPath></Reference>\n    <Reference Include="APIShared">',1)
    s='\n'.join(line for line in s.split('\n') if '../../../../BugfixesAndQoL/src/Diagnostics/*.cs' not in line and '../../../../BugfixesAndQoL/src/TemporaryGateBlockagePolicy.cs' not in line)
    s=s.replace('    <Compile Include="../../src/Diagnostics/*.cs"', '    <Compile Include="../../../SpectatorEditorBuildTest/src/AvailabilityObservation.cs" Link="Owner/AvailabilityObservation.cs" />\n    <Compile Include="../../src/Diagnostics/*.cs"',1)
    return s
edit('Testmods/AIBuildDiagnoseTest/tests/Diagnostics.Tests/Diagnostics.Tests.csproj', tests)

def cases(s):
    s=s.replace('using BugfixesAndQoL;','using BugfixesAndQoL;\nusing SpectatorEditorBuildTest;')
    s=s.replace('AiEconomyObservation', 'AvailabilityObservation').replace('.RegisterAvailability(', '.Register(').replace('.BeginAvailability(', '.Begin(')
    start=s.index('        [TestMethod]\n        public void ThrowingProtectionObserver')
    s=s[:start] + '''        [TestMethod]
        public void TestsUseTheUnchangedInstalledBugfixObservationContract()
        {
            Assert.AreEqual(new Version(1, 0, 192, 0), typeof(AiBuildObservation).Assembly.GetName().Version);
            StringAssert.Contains(typeof(AiBuildObservation).Assembly.Location, "BugfixesAndQoL.dll");
        }
    }
}
'''
    return s
edit('Testmods/AIBuildDiagnoseTest/tests/Diagnostics.Tests/EconomyEvidenceTests.cs', cases)

# Undo only temporary prerequisites of the now-cancelled Bugfix build path.
def climb(s):
    s=s.replace(". (Join-Path $workspace 'Shared\\Tools\\ApiSharedRepository\\MainSource.Common.ps1')\n$apiRoot = Get-WorkshopApiRoot $workspace\n$roots = @($apiRoot, (Join-Path $workspace 'BugfixesAndQoL'))\nforeach ($root in $roots) {", "$roots = @('APIShared', 'BugfixesAndQoL')\nforeach ($relative in $roots) {\n    $root = Join-Path $workspace $relative")
    return s.replace("Join-Path $apiRoot 'src\\Pathfinding\\Assassin\\AssassinPathAPI.cs'", "Join-Path $workspace 'APIShared\\src\\Pathfinding\\Assassin\\AssassinPathAPI.cs'")
edit('_inspect/AssassinGateClimb/verify.ps1',climb)
def attack(s):
    s=s.replace(". (Join-Path $workspace 'Shared\\Tools\\ApiSharedRepository\\MainSource.Common.ps1')\n$apiRoot=Get-WorkshopApiRoot $workspace\n$roots=@($apiRoot,$mod)", "$roots=@((Join-Path $workspace 'APIShared'),$mod)")
    for file in ['AssassinAttackControlAPI.cs','AssassinObstacleCompletion.cs']:
        s=s.replace("Join-Path $apiRoot 'src\\Pathfinding\\Assassin\\"+file+"'", "Join-Path $workspace 'APIShared\\src\\Pathfinding\\Assassin\\"+file+"'")
    return s
edit('_inspect/AssassinAttackControlAudit/verify-integrated.ps1',attack)
def preset(s):
    s=s.replace('''        // IsReplay is replayed lifecycle delivery to a late observer, not a recorded game replay.
        // Current activation is guarded centrally and must also initialize a late subscriber.
        Check(extraFeaturesRuntime.Contains("Shared.GameplaySessionLifecycle.SubscribeStarted(log, OnSessionStarted)") &&
              extraFeaturesRuntime.Contains("if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))") &&''', '''        Check(extraFeaturesRuntime.Contains("!context.IsReplay") &&''')
    return s.replace('activation-gated shared lifecycle','replay-safe shared lifecycle')
edit('_inspect/HostClientPresetTests/Program.cs',preset)
print('Legacy installed-owner adaptation applied; independent Bugfix edits preserved.')
