using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using APIShared;
using APIShared.Automation;
using APIShared.Units.AiMilitary;
using CrusaderDE;
using HarmonyLib;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;

namespace AiGroupAdmissionTest
{
    internal sealed partial class Fixture
    {
        internal const string OriginalScenario = "original-save";
        internal string OriginalSave;
        private static Fixture originalActive;
        private static IDisposable originalAssignObserver;
        private static Harmony originalHarmony;
        private Type spectatorRuntime;
        private OriginalCase originalCase;
        private readonly List<OriginalCase> originalCases = new List<OriginalCase>();
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

        private sealed class OriginalCase
        {
            internal AiMilitaryAssignment Wanted;
            internal AiUnitRef Unit;
            internal AiMilitaryAssignmentResult Result;
            internal int GroupBefore, MembersBefore, Callbacks, X, Y, TargetX, TargetY;
            internal uint GroupGlobalBefore;
            internal Dictionary<AiUnitRef,string> OldOrders;
            internal Dictionary<int,uint> ExistingUnits;
            internal Dictionary<AiUnitRef,Tuple<int,int>> OldPositions;
            internal bool OldOrdersUnchanged, Moved, TargetChanged, SawMark;
        }

        internal IEnumerable<AutomationStep> OriginalSteps()
        {
            yield return new AutomationStep("original-load", TimeSpan.FromSeconds(240), OriginalLoad);
            yield return new AutomationStep("original-tools", TimeSpan.FromSeconds(60), OriginalTools, true);
            foreach (var role in new[] { AiMilitaryRole.CastleDefense, AiMilitaryRole.DefensePatrol, AiMilitaryRole.EconomyProtection })
                foreach (var mode in new[] { AiRecruitmentMode.Regular, AiRecruitmentMode.Additional })
                {
                    var assignment = new AiMilitaryAssignment(role, mode);
                    yield return new AutomationStep("original-" + role + "-" + mode, TimeSpan.FromSeconds(180),
                        run => OriginalPlaceCase(run, assignment), true);
                }
            yield return new AutomationStep("original-verdict", TimeSpan.FromSeconds(10), OriginalVerdict, true);
        }

        private IEnumerable<AutomationWait> OriginalLoad(AutomationRun run)
        {
            yield return AutomationWait.Until("original fixture main menu", MainMenuReady);
            var plugin = BepInEx.Bootstrap.Chainloader.PluginInfos["Spectator_Serp"].Instance;
            object settings = plugin.GetType().GetProperty("Settings").GetValue(plugin, null);
            // Personal settings only, before entering a mission. The external runner restores bytes.
            foreach (string key in new[] { "EnableClientFeatures", "EnableSingleplayerTestMode", "EnableTestTools" })
                settings.GetType().GetProperty(key).SetValue(settings, true, null);
            log("ORIGINAL_SETTINGS beforeLoad client="+Property(settings,"EnableClientFeatures")+" singleplayer="+Property(settings,"EnableSingleplayerTestMode")+
                " tools="+Property(settings,"EnableTestTools"));
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(300));
            foreach (var wait in run.LoadSave(OriginalSave)) yield return wait;
            foreach (var wait in EnterMission(run)) yield return wait;
            Require(run.CurrentMission.IsSave && !run.CurrentMission.IsEditor && !run.CurrentMission.Mode.IsRealMultiplayer,
                "original fixture must be a local save");
            owner = 7;
            Require(ApiShared.ForMod(Plugin.Guid).TryGetAiMilitary(out api, out var diagnostic), diagnostic.Reason);
            Require(run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.IsAIPlayer(owner)), "original player7 is not AI");
            log("ORIGINAL_LOADED path=" + OriginalSave + " owner=7 session=" + run.CurrentMission.SessionId);
            log("ORIGINAL_SETTINGS afterLoad client="+Property(settings,"EnableClientFeatures")+" singleplayer="+Property(settings,"EnableSingleplayerTestMode")+
                " tools="+Property(settings,"EnableTestTools"));
        }

        private object OriginalField(string name) => spectatorRuntime.GetField(name, PrivateStatic)?.GetValue(null)
            ?? throw new InvalidOperationException("Spectator field contract missing: " + name);
        private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target, null);
        private void OriginalCall(string name, params object[] args)
        {
            var method = spectatorRuntime.GetMethod(name, PrivateStatic);
            Require(method != null && method.IsPrivate, "Spectator private method contract missing: " + name);
            method.Invoke(null, args);
        }

        private IEnumerable<AutomationWait> OriginalTools(AutomationRun run)
        {
            spectatorRuntime = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Spectator.SpectatorRuntime")).FirstOrDefault(t => t != null);
            Require(spectatorRuntime != null, "Spectator runtime not loaded");
            yield return AutomationWait.Until("Spectator tools model", () => spectatorRuntime.GetField("toolsModel", PrivateStatic)?.GetValue(null) != null);
            // Use the same selector as the portrait: an observer's view would otherwise
            // restore the old owner during SyncSpectatorSelection when placement opens.
            OriginalCall("SelectHumanAi", owner);
            object tools = OriginalField("toolsModel");
            if (!(bool)Property(tools, "PlacementActive"))
            {
                var command = (ICommand)Property(tools, "ActionCommand");
                var clock=Stopwatch.StartNew(); long nextDiagnostic=0;
                yield return AutomationWait.Until("actual enabled Spectator placement command", () => {
                    if(command.CanExecute("Placement")) return true;
                    if(clock.ElapsedMilliseconds>=nextDiagnostic)
                    {
                        nextDiagnostic=clock.ElapsedMilliseconds+5000;
                        var method=spectatorRuntime.GetMethod("IsToolsSession",PrivateStatic);
                        var allowed=spectatorRuntime.GetProperty("AllowsGameplayActions",PrivateStatic);
                        log("ORIGINAL_READINESS IsToolsSession="+(method?.Invoke(null,null) ?? "missing")+" AllowsGameplayActions="+
                            (allowed?.GetValue(null,null) ?? "missing")+" ToolsAvailable="+Property(tools,"ToolsAvailable")+
                            " selectedOwner="+OriginalField("humanSelectedOwner")+" localPlayer="+EditorDirector.instance.ActivePlayerID);
                    }
                    return false;
                });
                command.Execute("Placement");
            }
            yield return AutomationWait.Until("Spectator placement mode", () => (bool)Property(tools, "PlacementActive"));
            var completed = spectatorRuntime.GetMethod("AiPlacementCompleted", PrivateStatic);
            Require(completed != null && completed.ReturnType == typeof(void) &&
                completed.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(long), typeof(int), typeof(eChimps), typeof(AiMilitaryAssignmentResult) }),
                "Spectator completion contract differs");
            originalActive = this;
            if (originalHarmony == null)
            {
                originalHarmony = new Harmony(Plugin.Guid + ".original-save");
                originalHarmony.Patch(completed, postfix: new HarmonyMethod(typeof(Fixture), nameof(OriginalCompleted)));
                originalAssignObserver = TribeR3EventHooks.OnTribeAssignUnit.Observable.Subscribe(args => originalActive?.OriginalAssigning(args));
            }
            log("ORIGINAL_PATH owner/intent=checked-reflection+real-viewmodel; mapper=public-PlaceMapperItem; apiHarnessScope=False; no-OS-input; no-Noesis-click-claim");
        }

        private IEnumerable<AutomationWait> OriginalPlaceCase(AutomationRun run, AiMilitaryAssignment wanted)
        {
            // Native allocation can select an empty slot even in this populated save.
            // Fill only slots it naturally selects, then measure a genuinely occupied target.
            // The first unprepared run is retained separately; these are in-memory fixture units.
            for (int attempt = 0; attempt < 32; attempt++)
            {
                foreach (var wait in OriginalPlaceAttempt(run, wanted)) yield return wait;
                if (originalCase.MembersBefore > 0)
                {
                    originalCases.Add(originalCase);
                    originalCase = null;
                    yield break;
                }
                log("ORIGINAL_SEED_EMPTY role=" + wanted.Role + " mode=" + wanted.RecruitmentMode +
                    " attempt=" + (attempt + 1) + " unit=" + originalCase.Unit + " group=" + originalCase.Result.Group.TribeId);
                originalCase = null;
            }
            Require(false, "coverage incomplete: native allocation selected no occupied target within 32 placements: " + wanted.Role + "/" + wanted.RecruitmentMode);
        }

        private IEnumerable<AutomationWait> OriginalPlaceAttempt(AutomationRun run, AiMilitaryAssignment wanted)
        {
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            var mapperMethod = typeof(EditorDirector).GetMethod("placeEditorTroop", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(mapperMethod != null && mapperMethod.IsPrivate && mapperMethod.ReturnType == typeof(void) &&
                mapperMethod.GetParameters().Length == 1 && mapperMethod.GetParameters()[0].ParameterType.FullName == "Enums+eMappers",
                "actual private Vanilla troop mapper command differs");
            mapperMethod.Invoke(EditorDirector.instance, new[] { Enum.ToObject(mapperMethod.GetParameters()[0].ParameterType,
                (int)eMappers.MAPPER_PEOPLE_XBOWMEN) });
            yield return AutomationWait.Until("visible current AI placement box", () => spectatorRuntime.GetField("aiPlacementPanel",PrivateStatic)?.GetValue(null) is FrameworkElement panel && panel.IsVisible &&
                (int)OriginalField("aiShownMapper")== (int)eMappers.MAPPER_PEOPLE_XBOWMEN && (int)OriginalField("aiShownOwner")==7);
            object model = OriginalField("aiPlacement");
            var choice = ((IEnumerable)Property(model, "Choices")).Cast<object>().FirstOrDefault(c =>
                Property(c, "Role").ToString() == wanted.Role.ToString() && (bool)Property(c, "Available"));
            Require(choice != null, "original XBow role unavailable: " + wanted.Role);
            model.GetType().GetProperty("Selected").SetValue(model, choice, null);
            model.GetType().GetProperty("Additional").SetValue(model, wanted.RecruitmentMode == AiRecruitmentMode.Additional, null);
            originalCase = new OriginalCase { Wanted = wanted };
            run.InSimulation(run.CurrentMission, () => { OriginalNativePlace(); return true; });
            yield return AutomationWait.Until("Spectator actual terminal callback " + wanted.Role + "/" + wanted.RecruitmentMode,
                () => originalCase.Result != null);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            run.InSimulation(run.CurrentMission, () => { OriginalCheck(); return true; });
            if (originalCase.MembersBefore == 0) yield break;
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            for (int ticks = 0; ticks < 600; ticks += 25)
            {
                yield return run.WaitForTicks(run.CurrentMission, 25);
                run.InSimulation(run.CurrentMission, () => { OriginalObserve(); return true; });
            }
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            log("ORIGINAL_BEHAVIOR role=" + wanted.Role + " mode=" + wanted.RecruitmentMode + " unit=" + originalCase.Unit +
                " moved=" + originalCase.Moved + " targetChanged=" + originalCase.TargetChanged + " aiMarkSeen=" + originalCase.SawMark +
                " observedTicks>=600 behaviorEvidenceOnly=True");
            run.InSimulation(run.CurrentMission, () => { OriginalOldBehavior(); return true; });
        }

        private unsafe void OriginalNativePlace()
        {
            Require(TrySpawnTile(out int x, out int y, out _), "no free original-save spawn tile");
            var before = new List<int>();
            GameUnitManagerAPI.Instance.QueryUnits().Where((in GameUnit u) => u.r_GlobalId != 0).ToIdList(before);
            var globals = new Dictionary<int,uint>();
            foreach (int id in before) if (UnitAccess.TryGetById(id, out GameUnit* u, out _)) globals[id] = u->r_GlobalId;
            originalCase.ExistingUnits = globals;
            EngineInterface.PlaceMapperItem((int)eMappers.MAPPER_PEOPLE_XBOWMEN, x, y, 0,
                EditorDirector.instance.ActivePlayerID, true, false, 3);
            var after = new List<int>();
            GameUnitManagerAPI.Instance.QueryUnits().Where((in GameUnit u) => u.r_ControllableForPlayerId == 7 && u.r_UnitChimp == eChimps.CHIMP_TYPE_XBOWMAN).ToIdList(after);
            var created = new List<AiUnitRef>();
            foreach (int id in after)
                if (UnitAccess.TryGetById(id, out GameUnit* u, out _) && (!globals.TryGetValue(id, out uint old) || old != u->r_GlobalId))
                    created.Add(new AiUnitRef(id,u->r_GlobalId));
            Require(created.Count == 1, "Spectator native mapper did not create exactly one player7 XBow: " + created.Count);
            originalCase.Unit = created[0];
            Require(api.TryGetState(originalCase.Unit, out var state) && state.Assignment.Role == originalCase.Wanted.Role &&
                state.Assignment.RecruitmentMode == originalCase.Wanted.RecruitmentMode, "Spectator did not capture actual mapper intent");
            log("ORIGINAL_CAPTURE role=" + originalCase.Wanted.Role + " mode=" + originalCase.Wanted.RecruitmentMode + " unit=" + originalCase.Unit + " status=" + state.Status);
        }

        private unsafe void OriginalAssigning(TribeAssignUnitEventArgs args)
        {
            var c = originalCase;
            if (c == null || args.Phase != EventHookPhase.Pre || !UnitAccess.TryGetById(args.UnitId, out GameUnit* unit, out _)) return;
            if (c.Unit.GlobalId==0 && c.ExistingUnits!=null && unit->r_ControllableForPlayerId==7 && unit->r_UnitChimp==eChimps.CHIMP_TYPE_XBOWMAN &&
                (!c.ExistingUnits.TryGetValue(args.UnitId,out uint previous) || previous!=unit->r_GlobalId)) c.Unit=new AiUnitRef(args.UnitId,unit->r_GlobalId);
            if(args.UnitId!=c.Unit.UnitId || unit->r_GlobalId!=c.Unit.GlobalId) return;
            if (!GameTribeManagerAPI.Instance.TryGetTribeById(args.TribeId, out GameTribe* group) || group->r_PlayerIdOwner != 7) return;
            c.GroupBefore = args.TribeId; c.GroupGlobalBefore = group->r_GlobalId;
            c.OldOrders = OriginalOldOrders(args.TribeId, c.Unit.UnitId); c.MembersBefore = c.OldOrders.Count;
            c.OldPositions = new Dictionary<AiUnitRef,Tuple<int,int>>();
            foreach(var identity in c.OldOrders.Keys)
                if(UnitAccess.TryGetById(identity.UnitId,out GameUnit* old,out _) && old->r_GlobalId==identity.GlobalId)
                    c.OldPositions[identity]=Tuple.Create((int)old->r_CurrentTilePositionX,(int)old->r_CurrentTilePositionY);
        }

        private static void OriginalCompleted(long session, int owner, AiMilitaryAssignmentResult result)
        {
            var f = originalActive; var c = f?.originalCase;
            if (c == null || result.Status==AiMilitaryAssignmentStatus.Queued || !c.Unit.Equals(result.Unit)) return;
            c.Result = result; c.Callbacks++;
            try { c.OldOrdersUnchanged = c.OldOrders != null && c.OldOrders.All(pair => f.OriginalSameOrder(pair.Key,pair.Value)); }
            catch (Exception error) { f.Fail("ORIGINAL_SNAPSHOT " + error); }
        }

        private unsafe Dictionary<AiUnitRef,string> OriginalOldOrders(int group, int recruit)
        {
            var ids = new List<int>(); var result = new Dictionary<AiUnitRef,string>();
            GameUnitManagerAPI.Instance.QueryUnits().Where((in GameUnit u) => u.r_TribeId == group && u.r_GlobalId != 0).ToIdList(ids);
            foreach (int id in ids) if (id != recruit && UnitAccess.TryGetById(id,out GameUnit* u,out _))
                result[new AiUnitRef(id,u->r_GlobalId)] = OriginalOrder(u);
            return result;
        }
        private unsafe bool OriginalSameOrder(AiUnitRef unit,string expected) => UnitAccess.TryGetById(unit.UnitId,out GameUnit* u,out _) &&
            u->r_GlobalId == unit.GlobalId && OriginalOrder(u) == expected;
        private static unsafe string OriginalOrder(GameUnit* u) => string.Join("/",u->r_TribeId,u->r_AIState,u->r_ResumeAIState,
            u->r_AI_LastIssuedTribeCommand,u->r_AI_ContextTargetUnitId,u->r_AI_ContextTargetUnitGlobalId,u->r_AI_ContextTargetBuildingTileId,
            u->r_TargetTilePositionX,u->r_TargetTilePositionY,u->r_CurrentPathPlanIndex,u->r_PathPlanLength,u->r_PathPlanStateBitFlags);

        private unsafe void OriginalCheck()
        {
            var c = originalCase; var result = c.Result;
            string label = " role=" + c.Wanted.Role + " mode=" + c.Wanted.RecruitmentMode + " unit=" + c.Unit;
            log("ORIGINAL_RESULT" + label + " status=" + result.Status + " reason=" + result.Reason + " detail=" + result.Detail +
                " group=" + result.Group.TribeId + "/" + result.Group.GlobalId + " oldMembers=" + c.MembersBefore + " oldOrdersUnchanged=" + c.OldOrdersUnchanged);
            Require(result.Status == AiMilitaryAssignmentStatus.Applied && c.Callbacks == 1, "original admission failed" + label);
            Require(UnitAccess.TryGetById(c.Unit.UnitId,out GameUnit* u,out _) && u->r_GlobalId == c.Unit.GlobalId && UnitAccess.IsReallyAlive(u) &&
                u->r_ControllableForPlayerId == 7 && u->r_AITribeRole == NativeRole(c.Wanted.Role) && u->r_TribeId == result.Group.TribeId &&
                IsMember(result.Group.TribeId,result.Group.GlobalId,c.Unit.UnitId), "original native role/membership failed" + label);
            Require(c.GroupBefore == result.Group.TribeId && c.GroupGlobalBefore == result.Group.GlobalId && c.OldOrdersUnchanged,
                "original old-member orders changed or no atomic snapshot" + label);
            Require(api.IsExcludedFromRecruitmentQuota(c.Unit) == (c.Wanted.RecruitmentMode == AiRecruitmentMode.Additional), "original quota marker differs" + label);
            c.X=u->r_CurrentTilePositionX;c.Y=u->r_CurrentTilePositionY;c.TargetX=u->r_TargetTilePositionX;c.TargetY=u->r_TargetTilePositionY;
        }

        private unsafe void OriginalObserve()
        {
            var c=originalCase;
            if (!UnitAccess.TryGetById(c.Unit.UnitId,out GameUnit* u,out _) || u->r_GlobalId!=c.Unit.GlobalId || !UnitAccess.IsReallyAlive(u)) return;
            c.Moved |= Math.Abs(u->r_CurrentTilePositionX-c.X)>1 || Math.Abs(u->r_CurrentTilePositionY-c.Y)>1;
            c.TargetChanged |= u->r_TargetTilePositionX!=c.TargetX || u->r_TargetTilePositionY!=c.TargetY;
            c.SawMark |= u->N000000D8!=0;
        }
        private unsafe void OriginalOldBehavior()
        {
            var c=originalCase; int alive=0,sameGroup=0,moved=0,changedOrders=0;
            foreach(var pair in c.OldOrders)
                if(UnitAccess.TryGetById(pair.Key.UnitId,out GameUnit* u,out _) && u->r_GlobalId==pair.Key.GlobalId && UnitAccess.IsReallyAlive(u))
                {
                    alive++; if(u->r_TribeId==c.Result.Group.TribeId) sameGroup++;
                    var start=c.OldPositions[pair.Key];
                    if(Math.Abs(u->r_CurrentTilePositionX-start.Item1)>1 || Math.Abs(u->r_CurrentTilePositionY-start.Item2)>1) moved++;
                    if(OriginalOrder(u)!=pair.Value) changedOrders++;
                }
            log("ORIGINAL_OLD_BEHAVIOR role="+c.Wanted.Role+" mode="+c.Wanted.RecruitmentMode+" original="+c.MembersBefore+
                " alive="+alive+" sameGroup="+sameGroup+" moved="+moved+" changedOrdersNaturally="+changedOrders+" evidenceOnly=True");
        }
        private IEnumerable<AutomationWait> OriginalVerdict(AutomationRun run)
        {
            Require(originalCases.Count==6 && originalCases.All(c=>c.Result?.Status==AiMilitaryAssignmentStatus.Applied && c.OldOrdersUnchanged), "original six-case matrix incomplete");
            log("ORIGINAL_SUMMARY applied=6 oldOrdersUnchanged=6 occupiedTargets="+originalCases.Count(c=>c.MembersBefore>0)+
                " behaviorObserved=6 behaviorIsEvidenceOnly=True noNoesisClickCoverage=True");
            Require(originalCases.All(c=>c.MembersBefore>0), "coverage incomplete: not all six selected target groups had existing members");
            originalActive=null;
            foreach(var wait in Verdict(run)) yield return wait;
        }
    }
}
