// Real SP/editor acceptance through the public AI military capability.
// Runs only inside an explicitly enabled fresh-world scenario; no extra hooks or timers.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using APIShared;
using APIShared.Units.AiMilitary;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    internal static class RuntimeAiSteps
    {
        private static IAiMilitaryCapability api;
        private static object engine;
        private static readonly List<Probe> probes = new List<Probe>();
        private static long session, savedSession;
        private static long ownerReadySession;
        private static AiUnitRef ownerLord;
        private static int owner;
        private static bool editor;
        private static string failure, savePath;
        // FBCB9319 native unit-role ABI, not AITribeStorageRole16 (pool storage slot is 18).
        private const ushort SiegeEngineerNativeRole = 10;
        private sealed class Probe
        {
            internal AiUnitRef Unit;
            internal AiMilitaryAssignment Wanted;
            internal eChimps Type;
            internal AiMilitaryAssignmentResult Final;
            internal int Callbacks;
        }

        internal static void Register()
        {
            Runner.Register("AiMilitary.ResumeBriefing", ResumeBriefing);
            Runner.Register("AiMilitary.WaitOwner", WaitOwner);
            Runner.Register("AiMilitary.Arm", Arm);
            Runner.Register("AiMilitary.Complete", Complete);
            Runner.Register("AiMilitary.Save", Save);
            Runner.Register("AiMilitary.Reload", Reload);
            Runner.Register("AiMilitary.Cleared", Cleared);
        }

        private static IEnumerable<Wait> ResumeBriefing(Step step)
        {
            yield return new Until("deferred vanilla briefing", () => MainViewModel.Instance.Show_HUD_Briefing);
            MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return new Until("briefing resumed", () => !MainViewModel.Instance.Show_HUD_Briefing);
        }

        private static IEnumerable<Wait> Arm(Step step)
        {
            var context = UiAcceptancePlugin.LastContext;
            editor = step.Arg(0, "sp") == "editor";
            Runner.Require(context != null && !context.IsSave && !context.Mode.IsRealMultiplayer && context.IsEditor == editor,
                "fixture requires a fresh explicitly selected SP/editor world");
            Runner.Require(probes.Count == 0, "previous fixture must pass AiMilitary.Cleared first");
            string diagnostic = null;
            yield return new Until("native AI military service", () =>
            {
                bool available = ApiShared.ForMod(UiAcceptancePlugin.Guid).TryGetAiMilitary(out api, out var report);
                diagnostic = report.Reason;
                return available;
            });
            AcceptLog.Info("AI_NATIVE_AVAILABLE available=True diagnostic=" + diagnostic);
            ResolveEngineMonitor();
            session = context.SessionId; failure = null;
            if (!editor) Runner.Require(GamePlayerManagerAPI.Instance.IsLocalPaused(), "pause before creation to exercise queued semantics");
            CreateFixture();
            // Render service continues while paused; native initialization may also advance.
            // Applied requires alive native state plus the verified completion callback.
            yield return new Delay(500);
            lock (engine)
            {
                foreach (var probe in probes)
                {
                    Runner.Require(api.TryGetState(probe.Unit, out var state), "captured identity was not tracked " + probe.Unit);
                    CheckInitialState(probe, state);
                }
            }
            Runner.Require(failure == null, failure ?? "callback failure");
            AcceptLog.Info("AI_PAUSED_INITIAL count=5 editor=" + editor + " noPrematureApplied=True");
        }

        private static void ResolveEngineMonitor()
        {
            if (engine != null) return;
            // Real assembly's private monitor: validate once, never direct-access a publicized field.
            FieldInfo field = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
            Runner.Require(field != null && field.IsPrivate && field.FieldType == typeof(object), "engine monitor signature differs");
            engine = field.GetValue(null);
            Runner.Require(engine != null, "engine monitor is null");
        }

        private static IEnumerable<Wait> WaitOwner(Step step)
        {
            var context = UiAcceptancePlugin.LastContext;
            Runner.Require(context != null && !context.IsSave && !context.IsEditor && !context.Mode.IsRealMultiplayer &&
                context.LocalPlayerId.HasValue && !GamePlayerManagerAPI.Instance.IsLocalPaused(),
                "owner initialization wait requires the running fresh SP fixture");
            long expectedSession = context.SessionId;
            var candidates = new List<int>();
            var roster = Platform_Multiplayer.Instance?.gameMembers;
            if (roster != null) foreach (var member in roster)
                // This fresh SP lobby has one human; native AI readiness is checked separately.
                if (member != null && !member.kicked && member.playerID >= 1 && member.playerID <= GamePlayerManagerAPI.MAX_PLAYERS &&
                    member.playerID != context.LocalPlayerId.Value && !candidates.Contains(member.playerID)) candidates.Add(member.playerID);
            Runner.Require(candidates.Count > 0, "fresh SP roster contains no AI candidate");
            ResolveEngineMonitor();
            bool reported = false;
            yield return new Until("native AI lord identity and siege-engineer AIC readiness", () =>
            {
                Runner.Require(UiAcceptancePlugin.StartedSession == expectedSession && UiAcceptancePlugin.EndedSession != expectedSession,
                    "owner initialization belongs to an ended world");
                if (!ApiShared.ForMod(UiAcceptancePlugin.Guid).TryGetAiMilitary(out api, out _)) return false;
                lock (engine)
                {
                    foreach (int candidate in candidates)
                    {
                        if (!reported) AcceptLog.Info("AI_OWNER_WAIT player=" + candidate + " ai=" + GamePlayerManagerAPI.Instance.IsAIPlayer(candidate) +
                            " lord=" + GamePlayerManagerAPI.Instance.GetLordUnitId(candidate) + "/" + GamePlayerManagerAPI.Instance.GetLordUnitGlobalId(candidate));
                        if (!TryReadyLord(candidate, out AiUnitRef lord)) continue;
                        owner = candidate; ownerLord = lord; ownerReadySession = expectedSession;
                        AcceptLog.Info("AI_OWNER_READY session=" + expectedSession + " owner=" + owner + " lord=" + ownerLord + " nativeAIAndAic=True");
                        return true;
                    }
                }
                reported = true;
                return false;
            });
        }

        private static unsafe bool TryReadyLord(int candidate, out AiUnitRef identity)
        {
            identity = default;
            var players = GamePlayerManagerAPI.Instance;
            int id = players.GetLordUnitId(candidate);
            uint global = unchecked((uint)players.GetLordUnitGlobalId(candidate));
            if (!players.IsAIPlayer(candidate) || global == 0 || !UnitAccess.TryGetById(id, out GameUnit* lord, out _) ||
                lord->r_GlobalId != global || lord->r_ControllableForPlayerId != candidate || lord->r_UnitChimp != eChimps.CHIMP_TYPE_LORD ||
                !UnitAccess.IsReallyAlive(lord)) return false;
            foreach (var option in api.GetOptions(candidate, eChimps.CHIMP_TYPE_ENGINEER, false))
                if (option.Role == AiMilitaryRole.SiegeEngineers && option.IsAvailable)
                { identity = new AiUnitRef(id, global); return true; }
            return false;
        }

        private static unsafe void CheckInitialState(Probe probe, AiMilitaryAssignmentResult state)
        {
            if (editor)
            {
                Runner.Require(state.Status == AiMilitaryAssignmentStatus.Declared && probe.Callbacks == 1,
                    "editor declaration/callback mismatch " + probe.Unit);
                return;
            }
            Runner.Require(UnitAccess.TryGetById(probe.Unit.UnitId, out GameUnit* unit, out _) &&
                unit->r_GlobalId == probe.Unit.GlobalId, "paused identity mismatch " + probe.Unit);
            bool initialized = UnitAccess.IsReallyAlive(unit);
            Runner.Require(initialized || unit->r_AliveState == AliveState.NeedsInit, "fixture failed native initialization " + probe.Unit);
            Runner.Require(state.Status == AiMilitaryAssignmentStatus.Queued || state.Status == AiMilitaryAssignmentStatus.Applied,
                "paused request rejected " + state.Reason + ": " + state.Detail);
            if (state.Status == AiMilitaryAssignmentStatus.Applied)
                Runner.Require(initialized && probe.Callbacks == 1 && probe.Final != null, "premature Applied before alive/verified callback " + probe.Unit);
            else
                Runner.Require(probe.Callbacks == 0, "queued request already completed " + probe.Unit);
            // Alive+Queued remains legal until the completed engine-run safe point.
            AcceptLog.Info("AI_PAUSED_STATE unit=" + probe.Unit + " life=" + unit->r_AliveState +
                " status=" + state.Status + " callbacks=" + probe.Callbacks);
        }

        private static unsafe void CreateFixture()
        {
            lock (engine)
            {
                Runner.Require(Monitor.IsEntered(engine), "spawn must own the validated engine monitor");
                int x = 100, y = 100, height = 0;
                if (editor) owner = 2; // Authored owner; final AI/AIC deliberately deferred to map start.
                else
                {
                    Runner.Require(ownerReadySession == session && TryReadyLord(owner, out AiUnitRef current) && current.Equals(ownerLord),
                        "run AiMilitary.WaitOwner before pausing; native owner/lord identity must remain ready");
                    Runner.Require(UnitAccess.TryGetById(ownerLord.UnitId, out GameUnit* lord, out _), "selected lord disappeared");
                    x = lord->r_CurrentTilePositionX; y = lord->r_CurrentTilePositionY; height = lord->r_HeightElevation;
                }
                for (int index = 0; index < 5; index++)
                {
                    var probe = new Probe
                    {
                        Type = index == 0 ? eChimps.CHIMP_TYPE_ARCHER : eChimps.CHIMP_TYPE_ENGINEER,
                        Wanted = new AiMilitaryAssignment(index == 0 ? AiMilitaryRole.None : AiMilitaryRole.SiegeEngineers,
                            index >= 3 ? AiRecruitmentMode.Additional : AiRecruitmentMode.Regular)
                    };
                    bool available = false;
                    foreach (var option in api.GetOptions(owner, probe.Type, editor))
                        if (option.Role == probe.Wanted.Role) { available = option.IsAvailable; Runner.Require(available, option.Reason + ": " + option.Detail); }
                    Runner.Require(available, "expected fixture role missing");
                    probes.Add(probe);
                    Runner.Require(api.TryBeginSpawnScope(owner, probe.Type, probe.Wanted, editor, false,
                        result => OnCompleted(probe, result), out IAiMilitarySpawnScope scope, out string why), why);
                    try
                    {
                        int created = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, x, y, height, probe.Type));
                        GameUnit* unit = null;
                        Runner.Require(created > 0 && UnitAccess.TryGetById(created, out unit, out _) && unit->r_GlobalId != 0,
                            "native create returned no exact identity");
                        probe.Unit = new AiUnitRef(created, unit->r_GlobalId);
                        var initial = scope.Complete();
                        Runner.Require(initial.Count == 1 && initial[0].Unit.Equals(probe.Unit), "scope primary identity differs from actual native return");
                        Runner.Require(editor ? initial[0].Status == AiMilitaryAssignmentStatus.Declared :
                            initial[0].Status == AiMilitaryAssignmentStatus.Queued || initial[0].Status == AiMilitaryAssignmentStatus.Applied,
                            "initial result " + initial[0].Status + ": " + initial[0].Detail);
                    }
                    finally { scope.Dispose(); }
                }
            }
        }

        private static void OnCompleted(Probe probe, AiMilitaryAssignmentResult result)
        {
            // Called synchronously under the engine monitor; no Unity/Noesis work here.
            probe.Callbacks++; probe.Final = result;
            try
            {
                Runner.Require(Monitor.IsEntered(engine), "completion lacks engine monitor");
                Runner.Require(probe.Callbacks == 1 && result.Unit.Equals(probe.Unit), "callback identity/exactly-once mismatch");
                Runner.Require(result.Status == (editor ? AiMilitaryAssignmentStatus.Declared : AiMilitaryAssignmentStatus.Applied),
                    result.Status + "/" + result.Reason + ": " + result.Detail);
                if (!editor) CheckNative(probe, true);
            }
            catch (Exception error) { failure = error.ToString(); }
        }

        private static IEnumerable<Wait> Complete(Step step)
        {
            Runner.Require(!editor && session == UiAcceptancePlugin.StartedSession, "completion belongs to another world");
            yield return new Until("all five native admissions", () =>
            {
                lock (engine)
                {
                    if (failure != null) return true;
                    foreach (var probe in probes) if (probe.Final == null) return false;
                    return true;
                }
            });
            lock (engine)
            {
                Runner.Require(failure == null, failure ?? "callback failure");
                foreach (var probe in probes) Runner.Require(probe.Callbacks == 1, "callback missing/duplicated");
                CheckNative(probes[0], true); // None remains excluded from military AI after actual simulation work.
            }
            AcceptLog.Info("AI_APPLIED count=5 regularEngineers=2 additionalEngineers=2 noneProtected=True");
        }

        private static unsafe void CheckNative(Probe probe, bool admission)
        {
            Runner.Require(UnitAccess.TryGetById(probe.Unit.UnitId, out GameUnit* unit, out _) &&
                unit->r_GlobalId == probe.Unit.GlobalId && UnitAccess.IsReallyAlive(unit) && unit->r_ControllableForPlayerId == owner,
                "native unit identity/owner/alive differs " + probe.Unit);
            Runner.Require(api.TryGetState(probe.Unit, out var state) && state.Status == AiMilitaryAssignmentStatus.Applied &&
                state.Assignment.Role == probe.Wanted.Role && state.Assignment.RecruitmentMode == probe.Wanted.RecruitmentMode,
                "tracked result differs " + probe.Unit);
            bool additional = api.IsExcludedFromRecruitmentQuota(probe.Unit);
            Runner.Require(additional == (probe.Wanted.RecruitmentMode == AiRecruitmentMode.Additional), "quota identity marker differs " + probe.Unit);
            int tribeId = unit->r_TribeId;
            GameTribe* tribe = null;
            bool needsGroup = admission || probe.Wanted.Role == AiMilitaryRole.None;
            Runner.Require(!needsGroup || tribeId > 0, "required private/admission group absent " + probe.Unit);
            Runner.Require(tribeId == 0 || GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out tribe) &&
                tribe->r_GlobalId != 0 && tribe->r_AliveState == AliveState.IsAlive && tribe->r_PlayerIdOwner == owner,
                "actual current group invalid " + probe.Unit);
            // FBCB9319 native GameTribe member bitset ABI; same diagnostic contract as Spectator.
            bool member = tribe != null && ((((ushort*)((byte*)tribe + 0x36))[probe.Unit.UnitId >> 4] >> (probe.Unit.UnitId & 15)) & 1) != 0;
            Runner.Require(tribeId == 0 || member, "native member bit absent " + probe.Unit);
            if (admission)
                Runner.Require(unit->r_AITribeRole == (probe.Wanted.Role == AiMilitaryRole.None ? ushort.MaxValue : SiegeEngineerNativeRole) &&
                    tribeId == state.Group.TribeId && tribe->r_GlobalId == state.Group.GlobalId, "native admission role/group differs " + probe.Unit);
            // Later engine work may legitimately turn a free engineer into a construction worker.
            // Restore checks current physical membership, identity, saved semantic result and marker;
            // it deliberately does not replay or demand the original engineer role/group.
            AcceptLog.Info("AI_NATIVE_CHECK admission=" + admission + " unit=" + probe.Unit + " actualGID=" + unit->r_GlobalId +
                " owner=" + owner + " role=" + unit->r_AITribeRole + " group=" + tribeId + "/" + (tribe == null ? 0 : tribe->r_GlobalId) +
                " member=" + member + " state=" + state.Status + " additional=" + additional);
        }

        private static IEnumerable<Wait> Save(Step step)
        {
            Runner.Require(!editor && session == UiAcceptancePlugin.StartedSession && !GamePlayerManagerAPI.Instance.IsLocalPaused(), "save fixture needs its own running SP world (generic LoadSave waits for simulation ticks)");
            lock (engine) foreach (var probe in probes) CheckNative(probe, false);
            string directory = Path.GetFullPath(ConfigSettings.GetSavesPath());
            savePath = Path.Combine(directory, "AiMilitaryCheck_" + System.Guid.NewGuid().ToString("N") + ".sav");
            Runner.Require(Directory.Exists(directory) && !File.Exists(savePath), "save path missing or already exists");
            var location = CameraControls2D.instance.getScreenXYForSaveCentring(
                (int)((float)(UnityEngine.Screen.width / 64) / PerfectPixelWithZoom.instance.getZoom()),
                (int)((float)(UnityEngine.Screen.height / 16) / PerfectPixelWithZoom.instance.getZoom()));
            int cx = -1, cy = -1; GameMap.instance.getRotationCentre(ref cx, ref cy);
            Runner.Require(EngineInterface.SaveSaveGame(savePath, location.x, location.y, cx, cy, false, false, false), "native save failed");
            Runner.Require(File.Exists(savePath) && new FileInfo(savePath).Length > 0, "save file missing/empty");
            savedSession = session;
            AcceptLog.Info("AI_SAVE path=" + savePath + " count=" + probes.Count);
            yield break;
        }

        private static IEnumerable<Wait> Reload(Step step)
        {
            Runner.Require(GameSteps.MenuReady() && savePath != null && File.Exists(savePath) &&
                Path.GetFileName(savePath).StartsWith("AiMilitaryCheck_", StringComparison.Ordinal), "no owned fixture save");
            // Existing generic native-load step; no AutoSendTest reflection or runtime dependency.
            MethodInfo load = typeof(GameSteps).GetMethod("LoadSave", BindingFlags.Static | BindingFlags.NonPublic);
            Runner.Require(load != null, "runner generic LoadSave missing");
            var call = new Step { Id = step.Id + "-native", Args = new[] { savePath } };
            foreach (Wait wait in (IEnumerable<Wait>)load.Invoke(null, new object[] { call })) yield return wait;
            var context = UiAcceptancePlugin.LastContext;
            Runner.Require(context != null && context.IsSave && context.SessionId != savedSession && !context.Mode.IsRealMultiplayer, "restore did not enter a distinct SP save world");
            session = context.SessionId;
            lock (engine)
            {
                foreach (var probe in probes) { Runner.Require(probe.Callbacks == 1, "load replayed completed callback"); CheckNative(probe, false); }
                CheckNative(probes[0], true);
            }
            AcceptLog.Info("AI_RESTORED count=5 identitiesMarkersAndCurrentMembership=True callbackReplay=False");
        }

        private static IEnumerable<Wait> Cleared(Step step)
        {
            Runner.Require(GameSteps.MenuReady() && UiAcceptancePlugin.EndedSession == session, "world-end check must follow fixture quit");
            lock (engine)
                foreach (var probe in probes)
                {
                    Runner.Require(!api.TryGetState(probe.Unit, out _) && !api.IsExcludedFromRecruitmentQuota(probe.Unit), "old-world state/marker leaked " + probe.Unit);
                    Runner.Require(probe.Callbacks == 1, "completed request callback repeated at world end");
                }
            AcceptLog.Info("AI_WORLD_CLEARED count=" + probes.Count + " statesAndMarkersEmpty=True");
            probes.Clear(); session = 0;
            yield break;
        }
    }
}
