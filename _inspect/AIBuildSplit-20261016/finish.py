from pathlib import Path
root=Path.cwd();base=root/'Testmods/AIBuildDiagnoseTest';special=root/'Testmods/CanarariWoodTest'
def read(p):return p.read_text(encoding='utf-8-sig')
def write(p,s):
    p.parent.mkdir(parents=True,exist_ok=True)
    with p.open('w',encoding='utf-8',newline='') as f:f.write(s.replace('\r\n','\n').replace('\n','\r\n'))
old=read(root/'_inspect/AIBuildSplit-20261016/head-runtime.txt')
reset=old[old.index('            firstTick = false;'):old.index('            Array.Clear(hutSpawns')]
reset=reset.replace('            firstWoodShadowDone = false;','            Array.Clear(woodShadowDone, 0, woodShadowDone.Length);')
p=base/'src/AIBuildDiagnoseRuntime.cs';s=read(p)
needle='            active = !session.IsEditor && !session.IsReplay;\n'
s=s.replace(needle,needle+reset)
s=s.replace('vanillaPlacement=unobserved-before-probe','vanillaPlacement=unobserved')
write(p,s)

# Scope source validation to the active mod. Workspace-wide validation was also
# attempted; historical preset fixtures refer to development-only APIShared files.
for mod in [base,special]:
    p=mod/'build.bat';s=read(p)
    s=s.replace('Test-SharedBoundaries.ps1"','Test-SharedBoundaries.ps1" -ModDirectory "Testmods\\'+mod.name+'"')
    write(p,s)

write(special/'src/DisposableProbePolicy.cs','''using System;
using System.IO;
namespace CanarariWoodTest
{
    internal static class DisposableProbePolicy
    {
        internal static bool IsNamedCopy(bool loadedSave, bool editor, bool replay, string path, string expectedName) =>
            loadedSave && !editor && !replay && !string.IsNullOrEmpty(path) &&
            string.Equals(Path.GetFileName(path), expectedName, StringComparison.OrdinalIgnoreCase);
    }
}
''')
p=special/'src/CanarariWoodRuntime.cs';s=read(p)
s=s.replace('''            bool namedCopy = active && session.IsLoadedSave && string.Equals(Path.GetFileName(session.SaveFileName ?? ""),
                ProbeSaveName, StringComparison.OrdinalIgnoreCase);''','''            bool namedCopy = active && DisposableProbePolicy.IsNamedCopy(session.IsLoadedSave, session.IsEditor,
                session.IsReplay, session.SaveFileName, ProbeSaveName);''')
# Only fields actually needed by the companion's independently tracked attempts.
start=s.index('        private sealed class Attempt');end=s.index('        private void PrepareNearbyWoodShadow',start)
s=s[:start]+'''        private sealed class Attempt
        {
            internal Attempt(int playerId) { PlayerId = playerId; }
            internal readonly int PlayerId;
            internal bool RouteSeen, NearbyOverlayApplied;
            internal int RouteResult;
            internal AiNearbyPathEvidence NearbyBefore, NearbyAfter;
            internal NearbyWoodShadow NormalNearby, AlternativeNearby;
        }

'''+s[end:]
write(p,s)
p=special/'verify.ps1';s=read(p).replace('namedCopy = active && session.IsLoadedSave','namedCopy = active && DisposableProbePolicy.IsNamedCopy')
write(p,s)
p=base/'tests/Diagnostics.Tests/Diagnostics.Tests.csproj';s=read(p).replace('    <Compile Include="../../../CanarariWoodTest/src/CanarariWoodRuntime.cs"',
    '    <Compile Include="../../../CanarariWoodTest/src/DisposableProbePolicy.cs" Link="Companion/DisposableProbePolicy.cs" />\n    <Compile Include="../../../CanarariWoodTest/src/CanarariWoodRuntime.cs"')
write(p,s)
write(base/'tests/Diagnostics.Tests/DisposableProbeTests.cs','''using CanarariWoodTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace AIBuildDiagnoseTest.Tests
{
    [TestClass]
    public sealed class DisposableProbeTests
    {
        [TestMethod]
        public void OnlyNamedLoadedCopyCanPassSessionGate()
        {
            const string name = "test_canari_nowoodcutters_probe.sav";
            Assert.IsTrue(DisposableProbePolicy.IsNamedCopy(true, false, false, @"C:\\saves\\" + name, name));
            Assert.IsTrue(DisposableProbePolicy.IsNamedCopy(true, false, false, name.ToUpperInvariant(), name));
            Assert.IsFalse(DisposableProbePolicy.IsNamedCopy(false, false, false, name, name));
            Assert.IsFalse(DisposableProbePolicy.IsNamedCopy(true, true, false, name, name));
            Assert.IsFalse(DisposableProbePolicy.IsNamedCopy(true, false, true, name, name));
            Assert.IsFalse(DisposableProbePolicy.IsNamedCopy(true, false, false, "test_canari_nowoodcutters.sav", name));
            Assert.IsFalse(DisposableProbePolicy.IsNamedCopy(true, false, false, null, name));
        }
    }
}
''')

# Native provenance remains test-owned; no new addresses or game members.
write(base/'docs/DIAGNOSTICS.md','''# General AI construction diagnostics

AIBuildDiagnoseTest observes new and loaded games for all AI player slots,
including custom Lords. Editor and replay sessions are excluded. It records
AIV steps, resource/site searches, routes, construction events, positive-ID spawns,
walls and farm/path-grid evidence. Woodsman and orchard diagnostics remain more
detailed than other building types. No complete automatic cause detector is claimed.

The mod never places/deletes buildings, changes search data or suppresses Fixes
settings by itself. Legacy probe settings in its old configuration are ignored.
CanarariWoodTest is an optional companion requiring this mod; its settings belong
to its own new configuration and default to false.

## Ownership and lifetime

The diagnostic owns scheduler, route and general-site observation hooks and the
single BugfixesAndQoL observation sink. APIShared supplies mission lifecycle and
AIV build-step observation. Existing native validation and permanent hook lifetime
remain unchanged. Missing capabilities are logged independently.

The assembly-private companion bridge accepts only CanarariWoodTest_Serp. It
forwards records synchronously on the native caller thread after general capture,
with independent exception handling. Its optional gate and scoped overlay are
inert without companion registration. Only the companion performs mutations.
An overlay restoration callback runs after the single original native call,
including failure paths. Neither mod unhooks at startup cleanup or map changes.

Runtime objects and subscriptions are process-rooted; session state resets for
each mission. GameTimeManagerAPI.OnTick provides post-startup execution. Logs
include session, player and attempt identities; unknown attribution, missing
observations and budget omissions are not evidence of successful construction.

## Verification

The build driver selects APIShared main or development explicitly, checks source
boundaries and native contracts, runs Diagnostics.Tests and builds/installs the
plugin. The tests compile production observation, bridge and companion sources;
native backend probes use private copied code, never the installed process.

Game acceptance remains separate: general-only runs, multiple Lords, new/save
sessions, consecutive maps and opt-in disposable-copy experiments must be checked
in game. Source/build tests do not establish that these game runs occurred.
''')
write(special/'docs/DIAGNOSTICS.md','''# Disposable AI wood and farm experiments

CanarariWoodTest requires AIBuildDiagnoseTest, APIShared, Script Extender and
BugfixesAndQoL. Install both diagnostic mods for these experiments. The companion
installs no native hooks; it uses the general mod's private integration bridge.
Its own BepInEx configuration is CanarariWoodTest_Serp.cfg. All settings default
to false; settings from AIBuildDiagnoseTest_Serp.cfg are not migrated or activated.

| Setting | Scope and effect |
| --- | --- |
| PlacementProbe.Enabled | One placement at player 6, (345,485), on test_canari_nowoodcutters_probe.sav after the expected failed wood attempt. |
| NearbyWoodTest.Enabled | On that named copy only: calibrate against unmodified Vanilla, predict exclusions, revalidate live data, temporarily change candidates for at most twelve searches and restore every touched byte. |
| FarmContractProbe.Enabled | On byte-verified rat_farm_site_contract_probe.sav, player 2: create four farms and perform controlled wood-site tests. |
| CanariFarmSwap.Enabled | On byte-verified test_canari_farm_swap_cattle.sav, test_canari_farm_swap_wheat.sav or test_canari_farm_swap_hops.sav: replace the orchard at (344,480), await cleanup/spawn/path generation and hold player 6 wood calls during preparation. |
| FixesFarmFilterOverride.Enabled | Explicit startup-only, process-wide suppression of the external Fixes farmland filter for Vanilla comparisons. Restart the game when changing this setting. The Fixes configuration file is preserved and its in-memory setting restored after native load. |

Never save experiment results over the original saves. Use the exact disposable
copies and retain their recorded hashes. Wrong names, hashes or calibration data
do not arm their corresponding experiment. Placement/Nearby retain their existing
name/calibration guards; they do not gain a fictitious source-hash guarantee.

With suppression disabled, the companion does not change Fixes settings. Active
experiments require the validated native Vanilla candidate gate; if Fixes is
already absent/disabled that comparison may be available without suppression.
Otherwise the experiments stay disabled and explain why in the log. Enabling
suppression affects the whole game process, including ordinary sessions, until
restart; it is never enabled automatically with another experiment.

Build AIBuildDiagnoseTest first, then this companion, with the same APIShared
selection. Native version guards, Vanilla placement arguments, timeout behavior,
copy-only gates and finally-based data restoration remain part of the tests.
''')
