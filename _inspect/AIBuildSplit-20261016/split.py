from pathlib import Path
import re, shutil

root = Path.cwd()
base = root / 'Testmods/AIBuildDiagnoseTest'
special = root / 'Testmods/CanarariWoodTest'
def read(p): return p.read_text(encoding='utf-8-sig')
def write(p, s):
    p.parent.mkdir(parents=True, exist_ok=True)
    with p.open('w', encoding='utf-8', newline='') as f: f.write(s.replace('\r\n','\n').replace('\n','\r\n'))
original = read(base/'src/AIBuildDiagnoseRuntime.cs')
write(root/'_inspect/AIBuildSplit-20261016/original-runtime.cs.txt',original)
def section(s, start, end): return s[s.index(start):s.index(end,s.index(start))]
def method(s, name):
    # All selected methods end at their class-level closing brace.
    m = re.search(r'^        (?:private|internal|public) [^\n]*\b'+name+r'\(',s,re.M)
    assert m, name
    end=s.index('\n        }',m.start())+len('\n        }')
    return s[m.start():end]+'\n\n'
def remove_method(s, name): return s.replace(method(s,name),'')

# Read-only evidence is shared by the observer and the opt-in causal experiment.
helper = section(original,'        private static readonly int[] NearbyDx','        private void PrepareNearbyWoodShadow')
helper = helper.replace('private sealed class','internal sealed class').replace('private static','internal static')
helper = helper.replace('private NearbyWoodShadow ReplayNearbyWood','internal static NearbyWoodShadow ReplayNearbyWood')
helper += method(original,'ParseCoarseBytes').replace('private static','internal static')
helper += method(original,'CoarseFailure').replace('private static','internal static')
write(base/'src/Diagnostics/NearbyWoodEvidence.cs', '''using System;
using System.Collections.Generic;
using SHCDESE.Interop.Enums;
namespace AIBuildDiagnoseTest
{
    // Copied snapshots and read-only replay; never writes game data or installs hooks.
    internal static class NearbyWoodEvidence
    {
'''+helper+'    }\n}\n')

# Move complete standalone probes, retaining their native safeguards.
for name in ['FarmSiteContractProbe.cs','CanariFarmSwapProbe.cs','FixesFarmFilterOverride.cs']:
    text=read(base/'src'/name).replace('namespace AIBuildDiagnoseTest','namespace CanarariWoodTest')
    text='using AIBuildDiagnoseTest;\n'+text
    write(special/'src'/name,text)
    (base/'src'/name).unlink()

# General runtime: keep observations, remove active experiments and fixed scenarios.
s=original
s=s.replace('using APIShared;','using APIShared;\nusing static AIBuildDiagnoseTest.NearbyWoodEvidence;')
s=s.replace(section(s,'        private const string ProbeSaveName','        private const int GridHistoryLimit'),'')
s=s.replace(section(s,'        private readonly bool placementProbeEnabled','        private readonly bool[] nearbyDetailedByPlayer'),'')
s=s.replace(section(s,'        private bool probeSession;','        private int lastPathGeneration;'),'')
s=s.replace('''        internal AIBuildDiagnoseRuntime(ManualLogSource logger, bool hasFixes,
            bool enablePlacementProbe, bool enableNearbyWoodTest,
            bool enableFarmContractProbe, bool enableCanariFarmSwap, ulong moduleBase)''',
'''        internal AIBuildDiagnoseRuntime(ManualLogSource logger, bool hasFixes, ulong moduleBase)''')
s=s.replace(section(s,'            placementProbeEnabled = enablePlacementProbe;','            buildingSubscription ='),'')
for line in ['            nearbyCopySession = nearbyCalibrated = false;','            nearbyCopyApplications = 0;',
             '            nearbyTestDisabled = false;','            nearbyCalibrationAttempts = 0;',
             '            nearbyAlternativeTraces = 0;',
             '            farmContractProbe.OnSessionStarted(session);','            canariFarmSwapProbe.OnSessionStarted(session);',
             '            farmContractProbe.OnSessionEnded();','            canariFarmSwapProbe.OnSessionEnded();',
             '            farmContractProbe.OnTick(tick, ReadPathGeneration());','            canariFarmSwapProbe.OnTick(tick, ReadPathGeneration());',
             '            farmContractProbe.OnBuildingSpawn(args);','            canariFarmSwapProbe.OnBuildingSpawn(args);',
             '            canariFarmSwapProbe.OnNativeRecord(record);']:
    s=s.replace(line+'\n','')
s=s.replace(section(s,'            probeSession = active','            Array.Clear(hutSpawns'),'            Array.Clear(nearbyDetailedByPlayer, 0, nearbyDetailedByPlayer.Length);\n')
s=s.replace(section(s,'            Log($"AI_BUILD_PROBE_ARMED:','            if (active)'),'')
s=s.replace(section(s,'            if (probePending)','            if (!firstTick)'),'')
s=s.replace(section(s,'        internal bool ShouldDeferCanariWoodBuild','        internal void OnNativeRecord'),'')
s=s.replace('''                if (!firstWoodShadowDone &&
                    ((loadingSaveName ?? "").IndexOf("test_canari_nowoodcutters",
                        StringComparison.OrdinalIgnoreCase) < 0 || record.PlayerId == 6))''',
'''                if (!woodShadowDone[record.PlayerId])''')
s=s.replace('        private bool firstWoodShadowDone;','        private readonly bool[] woodShadowDone = new bool[9];')
s=s.replace('            firstWoodShadowDone = false;','            Array.Clear(woodShadowDone, 0, woodShadowDone.Length);')
s=s.replace('                firstWoodShadowDone = true;','                woodShadowDone[playerId] = true;')
s=s.replace(section(s,'                if (probeSession &&','                if (attempt.RouteSeen && attempt.RouteResult == 0)'),'')
s=s.replace(section(s,'            if (probeRunning && args.PlayerId == ProbePlayer &&','            if (!active || args.Mappers'),'')
s=s.replace(section(s,'            if (probeRunning && args.PlayerId == ProbePlayer)','            long id = 0;'),'')
s=s.replace(section(s,'        private static readonly int[] NearbyDx','        private void PrepareNearbyWoodShadow'),'')
s=remove_method(s,'ParseCoarseBytes'); s=remove_method(s,'CoarseFailure')
s=remove_method(s,'RunPlacementProbe'); s=remove_method(s,'BeginNearbyWoodOverlay'); s=remove_method(s,'RestoreNearbyWoodOverlay')
s=s.replace('            internal NearbyWoodShadow AlternativeNearby;\n','').replace('            internal bool NearbyOverlayApplied;\n','')
s=s.replace(section(s,'        private void PrepareNearbyWoodShadow','        private void LogPlayers'),'''        private void PrepareNearbyWoodShadow(long attemptId, Attempt attempt)
        {
            int player = attempt.PlayerId;
            if ((uint)(player - 1) >= 8 || nearbyDetailedByPlayer[player]) return;
            nearbyDetailedByPlayer[player] = true;
            attempt.NormalNearby = ReplayNearbyWood(attempt.NearbyBefore, false, true);
            foreach (string line in attempt.NormalNearby.Trace)
                QueueDiagnostic($"AI_BUILD_NEARBY_SHADOW_VISIT: session={sessionId}, attempt={attemptId}, player={player}, " + line);
            QueueDiagnostic($"AI_BUILD_NEARBY_SHADOW_FIRST: session={sessionId}, attempt={attemptId}, player={player}, " +
                $"normal=({attempt.NormalNearby.X},{attempt.NormalNearby.Y}), valid={attempt.NormalNearby.Valid}, " +
                $"error={attempt.NormalNearby.Error}, traceOmitted={attempt.NormalNearby.TraceOmitted}, " +
                $"footprintsOmitted={attempt.NormalNearby.FootprintOmitted}; readOnly=true.");
        }

        private void CompleteNearbyWoodShadow(long attemptId, Attempt attempt)
        {
            NearbyWoodShadow expected = attempt.NormalNearby;
            if (expected == null) return;
            AiNearbyPathEvidence after = attempt.NearbyAfter;
            bool match = expected.Valid && after != null && expected.X == after.ResultX && expected.Y == after.ResultY;
            QueueDiagnostic($"AI_BUILD_NEARBY_SHADOW_COMPARE: session={sessionId}, attempt={attemptId}, player={attempt.PlayerId}, " +
                $"expected=({expected.X},{expected.Y}), vanilla=({after?.ResultX},{after?.ResultY}), match={match}; readOnly=true.");
        }

''')
s=s.replace('var cells = new HashSet<int> { 69 * 160 + 97 };','var cells = new HashSet<int>();')
write(base/'src/AIBuildDiagnoseRuntime.cs',s)

# Special runtime uses the same captured records without claiming another native hook.
parts = [method(original,n) for n in ['PrepareNearbyWoodShadow','CompleteNearbyWoodShadow','BeginNearbyWoodOverlay',
         'RestoreNearbyWoodOverlay','RunPlacementProbe','ReadResources','ReadPathGeneration','LogFootprintArea','LogSamples']]
parts.append(section(original,'        private static string FormatSample','        private static string FormatCoarse'))
attempt=section(original,'        private sealed class Attempt','        private static readonly int[] NearbyDx')
header='''using AIBuildDiagnoseTest;
using static AIBuildDiagnoseTest.NearbyWoodEvidence;
using APIShared;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
namespace CanarariWoodTest
{
    internal sealed unsafe class CanarariWoodRuntime
    {
        private const string ProbeSaveName = "test_canari_nowoodcutters_probe.sav";
        private const int ProbePlayer = 6, ProbeX = 345, ProbeY = 485;
        private const int NearbyCopyMaxApplications = 12;
        private readonly ManualLogSource log;
        private readonly ulong nativeModuleBase;
        private readonly bool placementProbeEnabled, nearbyWoodTestEnabled;
        private readonly FarmSiteContractProbe farmContractProbe;
        private readonly CanariFarmSwapProbe canariFarmSwapProbe;
        private readonly Dictionary<long, Attempt> attempts = new Dictionary<long, Attempt>();
        private readonly bool[] nearbyDetailedByPlayer = new bool[9];
        private bool active, probeSession, probePending, probeDone, probeRunning;
        private bool nearbyCopySession, nearbyCalibrated, nearbyTestDisabled;
        private int nearbyCopyApplications, nearbyAlternativeTraces, nearbyCalibrationAttempts, lastTick;
        private long sessionId, probeAttemptId, probeSpawnId;
        internal CanarariWoodRuntime(ManualLogSource logger, ulong moduleBase, bool placement, bool nearby, bool farms, bool swap)
        {
            log = logger;
            nativeModuleBase = string.Equals(Shared.DebugLogHelper.CurrentNativeSha256,
                NativeDiagnosticContracts.SupportedHash, StringComparison.OrdinalIgnoreCase) ? moduleBase : 0;
            placementProbeEnabled = placement && nativeModuleBase != 0;
            nearbyWoodTestEnabled = nearby && nativeModuleBase != 0;
            farmContractProbe = new FarmSiteContractProbe(farms, nativeModuleBase, Log, ReadResources);
            canariFarmSwapProbe = new CanariFarmSwapProbe(swap && nativeModuleBase != 0, Log);
        }
        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            sessionId = session.SessionId;
            active = !session.IsEditor && !session.IsReplay;
            attempts.Clear();
            probePending = probeDone = probeRunning = false;
            probeAttemptId = probeSpawnId = 0;
            nearbyCalibrated = nearbyTestDisabled = false;
            nearbyCopyApplications = nearbyAlternativeTraces = nearbyCalibrationAttempts = 0;
            Array.Clear(nearbyDetailedByPlayer, 0, nearbyDetailedByPlayer.Length);
            bool namedCopy = active && session.IsLoadedSave && string.Equals(Path.GetFileName(session.SaveFileName ?? ""),
                ProbeSaveName, StringComparison.OrdinalIgnoreCase);
            probeSession = namedCopy && placementProbeEnabled;
            nearbyCopySession = namedCopy && nearbyWoodTestEnabled;
            farmContractProbe.OnSessionStarted(session);
            canariFarmSwapProbe.OnSessionStarted(session);
            Log($"AI_BUILD_PROBE_ARMED: session={sessionId}, armed={probeSession}, requiredSave={ProbeSaveName}, player={ProbePlayer}, target=({ProbeX},{ProbeY}).");
            Log($"AI_BUILD_NEARBY_TEST_ARMED: session={sessionId}, armed={nearbyCopySession}, calibrationRequired=true.");
        }
        internal void OnSessionEnded()
        {
            farmContractProbe.OnSessionEnded(); canariFarmSwapProbe.OnSessionEnded();
            active = probeSession = nearbyCopySession = probePending = false;
            attempts.Clear();
        }
        internal void OnTick(int tick)
        {
            if (!active) return;
            lastTick = tick;
            farmContractProbe.OnTick(tick, ReadPathGeneration());
            canariFarmSwapProbe.OnTick(tick, ReadPathGeneration());
            if (probePending)
                try { RunPlacementProbe(); }
                catch (Exception ex) { probePending = false; probeDone = true; probeRunning = false; Log("AI_BUILD_PROBE_FAILED_CLOSED: " + ex); }
        }
        internal bool ShouldDeferWoodBuild(int player) => canariFarmSwapProbe.ShouldDeferWoodBuild(player);
        internal void OnNativeRecord(AiBuildDiagnosticRecord record)
        {
            canariFarmSwapProbe.OnNativeRecord(record);
            if (!active || record.PlayerId != ProbePlayer || record.AttemptId == 0 || (!probeSession && !nearbyCopySession)) return;
            if (record.Stage == "wood-build-before") attempts[record.AttemptId] = new Attempt(record.PlayerId);
            if (!attempts.TryGetValue(record.AttemptId, out Attempt attempt)) return;
            if (record.Stage == "route-result") { attempt.RouteSeen = true; attempt.RouteResult = (int)record.A; }
            if (record.Stage == "wood-nearby-path-before")
            { attempt.NearbyBefore = record.NearbyPathEvidence; PrepareNearbyWoodShadow(record.AttemptId, attempt); }
            if (record.Stage == "wood-nearby-path-after")
            { attempt.NearbyAfter = record.NearbyPathEvidence; CompleteNearbyWoodShadow(record.AttemptId, attempt); }
            if (record.Stage != "wood-build-after") return;
            if (probeSession && !probeDone && !probePending && attempt.RouteSeen && attempt.RouteResult == 0 &&
                attempt.NearbyAfter != null && attempt.NearbyAfter.ResultX * 5 == ProbeX && attempt.NearbyAfter.ResultY * 5 == ProbeY)
            {
                probeAttemptId = record.AttemptId; probePending = true;
                Log($"AI_BUILD_PROBE_QUEUED: session={sessionId}, attempt={probeAttemptId}, nextTick=true.");
            }
            attempts.Remove(record.AttemptId);
        }
        internal void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            farmContractProbe.OnBuildingSpawn(args); canariFarmSwapProbe.OnBuildingSpawn(args);
            if (probeRunning && args.PlayerId == ProbePlayer && args.Building == eStructs.STRUCT_WOODCUTTERS_HUT)
            {
                if (args.Phase == EventHookPhase.Post) probeSpawnId = args.ReturnValue;
                Log($"AI_BUILD_PROBE_SPAWN: session={sessionId}, attempt={probeAttemptId}, phase={args.Phase}, tile=({args.TileX},{args.TileY}), buildingId={args.ReturnValue}.");
            }
        }
        internal void OnBuildStructure(BuildStructureEventArgs args)
        {
            if (probeRunning && args.PlayerId == ProbePlayer && args.Mappers == eMappers.MAPPER_WOODSMAN)
                Log($"AI_BUILD_PROBE_STRUCTURE: session={sessionId}, attempt={probeAttemptId}, phase={args.Phase}, tile=({args.TileX},{args.TileY}), scale={args.BuildingScaleUnknown}, free={args.IsFree}.");
        }
        private void Log(string value) => Shared.DebugLogHelper.LogInfo(log, value);
        private void QueueDiagnostic(string value, bool preserve = false) => Log(value);
'''
write(special/'src/CanarariWoodRuntime.cs',header+attempt+''.join(parts)+'    }\n}\n')
