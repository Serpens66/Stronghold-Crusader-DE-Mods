using System;
using System.Collections.Concurrent;
using APIShared;
using APIShared.Presentation;
using APIShared.Wildlife;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace ImprovedHunters
{
    internal sealed partial class ImprovedHuntersRuntime
    {
        private AIHunterEconomy aiHunterEconomy;

        private void InitializeAIHunterEconomy(CrusaderLibraryLoadContext context, bool knownImage)
        {
            if (aiHunterEconomy != null) return;
            aiHunterEconomy = new AIHunterEconomy(this, context, knownImage);
            ApiShared.ForMod(ImprovedHuntersPlugin.PluginGuid).WhenReady(client =>
            {
                if (!client.Ui.TryPost(aiHunterEconomy.Initialize))
                    Shared.DebugLogHelper.LogError(log, "AI hunter economy initialization could not reach Unity.");
            });
        }

        // Process-rooted by persistentRuntime. Only compact log messages leave the simulation thread.
        private sealed unsafe class AIHunterEconomy
        {
            private readonly ImprovedHuntersRuntime owner;
            private readonly HunterRetirementNative retirement;
            private readonly AutomaticHunterBuilder builder;
            private readonly AIHunterWorldIndex world = new AIHunterWorldIndex();
            private readonly ConcurrentQueue<string> output = new ConcurrentQueue<string>();
            private readonly CompositeDisposable subscriptions = new CompositeDisposable();
            private readonly string[] lastWait = new string[AIHunterWorldIndex.PlayerSlots];
            private IUiFrameWorkRegistration outputWork;
            private MissionContext mission;
            private HunterCheckSchedule schedule;
            private bool initialized, initializationFailed, active, started, faulted, controlAiv, automatic, retire;
            private bool preyKnown, preyCaptured, worldReady, cycleActive, automaticCall;
            private int preyMask, otherPreyMask, baselineFoodMask, retireCursor, buildCursor, aivBlockedPlayers;

            internal AIHunterEconomy(ImprovedHuntersRuntime owner, CrusaderLibraryLoadContext context, bool knownImage)
            {
                this.owner = owner;
                retirement = new HunterRetirementNative(owner.log, context, knownImage);
                builder = new AutomaticHunterBuilder(retirement.AuditedImage);
            }

            internal void Initialize()
            {
                if (initialized || initializationFailed) return;
                try
                {
                    var api = ApiShared.ForMod(ImprovedHuntersPlugin.PluginGuid);
                    if (!api.Ui.TryRegisterBeforeRender("ai-hunter-output", Drain, out outputWork, out string reason))
                        throw new InvalidOperationException(reason);
                    if (!api.TryGetMissionLifecycle(out IMissionLifecycleCapability lifecycle, out var diagnostic) ||
                        !lifecycle.TryRegisterObserver("ai-hunter-economy", Start, End, InitializeMission, out diagnostic))
                        throw new InvalidOperationException(diagnostic?.Reason);
                    BuildingR3EventHooks.OnPlacementValidation.Observable.Where(a => a.Phase == EventHookPhase.Post)
                        .Subscribe(ValidatePlacement).AddTo(subscriptions);
                    BuildingR3EventHooks.OnBuildingSpawn.Observable.Where(a => a.Phase == EventHookPhase.Post)
                        .Subscribe(ObserveSpawn).AddTo(subscriptions);
                    initialized = true;
                    outputWork.Request();
                    Shared.DebugLogHelper.LogInfo(owner.log, "AI_HUNTER READY firstCheckDelay=200; interval=4800; perTickOperationLimit=1; processRooted=true.");
                }
                catch (Exception error)
                {
                    initializationFailed = true; active = false;
                    Shared.DebugLogHelper.LogError(owner.log, "AI_HUNTER INIT_FAILED " + error);
                }
            }

            private void Reset()
            {
                active = started = faulted = preyKnown = preyCaptured = worldReady = cycleActive = automaticCall = false;
                mission = null; schedule = default; preyMask = otherPreyMask = baselineFoodMask = 0;
                retireCursor = buildCursor = aivBlockedPlayers = 0; world.Clear(); Array.Clear(lastWait, 0, lastWait.Length);
            }

            private void CaptureSettings()
            {
                controlAiv = owner.settings.ControlAivHunters;
                automatic = owner.settings.AutoBuildHunters;
                retire = owner.settings.RetireEmptyHunters;
                active = Shared.GameplayModActivationGate.IsEnabled(owner.settings.EnableMod) && (controlAiv || automatic || retire);
            }

            private void InitializeMission(MissionLifecycleNotification notification)
            {
                if (notification.Phase == MissionInitializationPhase.BeforeLoad)
                { Reset(); mission = notification.Context; }
                if (notification.Phase == MissionInitializationPhase.BeforeNativeStart || notification.Phase == MissionInitializationPhase.NativeLoaded)
                {
                    if (mission?.SessionId != notification.Context.SessionId) Reset();
                    mission = notification.Context; CaptureSettings(); preyKnown = preyCaptured = worldReady = false;
                }
            }
            private void Start(MissionLifecycleNotification notification)
            {
                Reset(); mission = notification.Context; CaptureSettings(); started = true;
                Queue("SESSION id=" + mission.SessionId + "; active=" + active + "; aiv=" + controlAiv + "; automatic=" + automatic +
                    "; retire=" + retire + "; mode=" + mission.Mode.ToDiagnosticString());
            }
            private void End(MissionLifecycleNotification notification) { Reset(); }

            internal void OnTick(int tick)
            {
                if (!initialized || !active || !started || faulted || mission == null) return;
                if (!cycleActive && !schedule.TryBeginCheck(tick)) return;
                long measured = 0;
                Shared.DevDiag.Start(ref measured);
                try
                {
                    if (!cycleActive)
                    {
                        CaptureBuildings(); CapturePreyAndWorkers();
                        if (!preyKnown) return;
                        world.SelectBaselines();
                        baselineFoodMask = preyMask;
                        if (CanRenewChickens()) baselineFoodMask |= CurrentSpawnSites();
                        retireCursor = 0; buildCursor = 1; cycleActive = true;
                        Shared.DevDiag.Count("hunters.ai.cycles");
                    }
                    ProcessOneOperation();
                }
                catch (Exception error) { Fail(error); }
                finally { Shared.DevDiag.StopSpike(ref measured, "hunters.ai.tick", 2000); }
            }

            private bool AllowsType(int type, HuntablePreyEntry entry)
            {
                if (entry == null || entry.UnitType != type) return false;
                return !owner.settings.IsKnownAnimal((eChimps)type) || owner.IsRuntimeHuntingEnabled((eChimps)type);
            }
            private bool CanRenewChickens() => !mission.IsEditor && owner.IsOwnedChickenHuntingEnabled &&
                owner.settings.ChickensPerPlayer > 0 && owner.chickenTargetModifier?.IsEnabled == true &&
                HuntablePrey.Current.TryGet((int)eChimps.CHIMP_TYPE_CHICKEN, out var chicken) && chicken.AllowsOwnPlayerPrey;

            private void CapturePreyAndWorkers()
            {
                preyCaptured = true; preyKnown = false; preyMask = otherPreyMask = 0; world.BusyHuts.Clear();
                if (!HuntablePrey.IsAvailable || !owner.targetPolicyRegistered) return;
                var catalog = HuntablePrey.Current;
                var units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                fixed (GameUnit* first = units)
                for (int i = 0; i < units.Length; i++)
                {
                    ref readonly GameUnit unit = ref units[i];
                    world.ObserveWorker(in unit);
                    if (unit.r_AliveState != AliveState.IsAlive) continue;
                    int type = (int)unit.r_UnitChimp;
                    if (!catalog.TryGet(type, out var entry) || !AllowsType(type, entry)) continue;
                    bool live = unit.r_IsKilledByProjectile == 0 && unit.r_CurrentHealth > 0;
                    bool corpse = !live && owner.settings.CollectFreeCorpses && owner.targetSelectionModeAllowed &&
                        owner.hunterCorpseChoicePatch?.IsAvailable == true && owner.IsFreeUsableCorpse(first + i);
                    int mask = PreyPresencePolicy.OwnerMask(true, live, corpse, unit.r_ControllableForPlayerId, entry.AllowsOwnPlayerPrey);
                    preyMask |= mask;
                    if (type != (int)eChimps.CHIMP_TYPE_CHICKEN) otherPreyMask |= mask;
                }
                preyKnown = true;
            }

            private void CaptureBuildings()
            {
                world.Clear();
                var players = GamePlayerManagerAPI.Instance;
                for (int player = 1; player < AIHunterWorldIndex.PlayerSlots; player++)
                {
                    bool ai = players.IsAIPlayer(player);
                    int granary = ai && players.TryGetPlayerResourcesById(player, out var resources) ? checked((int)resources->r_FirstGranaryId) : 0;
                    world.SetPlayer(player, ai, granary);
                }
                var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++) world.ObserveBuilding(spanIndex + 1, in buildings[spanIndex]);
                worldReady = true;
            }

            // Late AIV creations join the small hut index; every count revalidates current slot/global identity.
            private void ObserveSpawn(BuildingSpawnEventArgs args)
            {
                if (!initialized || !active || faulted || !worldReady ||
                    (args.Building != eStructs.STRUCT_HUNTERS_HUT && !AIHunterWorldIndex.IsSpawnType(args.Building)) ||
                    args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue) return;
                try
                {
                    int id = (int)args.ReturnValue;
                    if (BuildingAccess.TryGetById(id, out GameBuilding* building, out _))
                    {
                        int player = building->r_PlayerIdOwner;
                        if (player > 0 && player < AIHunterWorldIndex.PlayerSlots && world.AI[player] &&
                            GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out var resources))
                            world.SetPlayer(player, true, checked((int)resources->r_FirstGranaryId));
                        world.ObserveBuilding(id, in *building);
                    }
                }
                catch (Exception error) { Fail(error); }
            }
            private static bool Current(in AIHunterHutRecord hut, out GameBuilding* building) =>
                BuildingAccess.TryGetById(hut.Id, out building, out _) && building->r_GlobalId == hut.GlobalId &&
                building->r_PlayerIdOwner == hut.Owner && HunterHutPolicy.IsHut(in *building, hut.Owner);
            private int CountHuts(int player)
            {
                int count = 0;
                for (int i = 0; i < world.Huts.Count; i++)
                { var hut = world.Huts[i]; if (hut.Owner == player && Current(in hut, out _)) count++; }
                return count;
            }

            private uint CurrentBaseline(int player)
            {
                if (world.TryGetHut(world.Baseline[player], out var current) && Current(in current, out _)) return current.GlobalId;
                uint chosen = 0; long best = long.MaxValue;
                for (int i = 0; i < world.Huts.Count; i++)
                {
                    var hut = world.Huts[i];
                    if (hut.Owner != player || !Current(in hut, out _)) continue;
                    long distance = world.Distance(in hut);
                    if (distance < best || (distance == best && (chosen == 0 || hut.GlobalId < chosen)))
                    { chosen = hut.GlobalId; best = distance; }
                }
                world.Baseline[player] = chosen; return chosen;
            }

            private int CurrentSpawnSites()
            {
                int mask = 0;
                for (int i = 0; i < world.SpawnBuildings.Count; i++)
                {
                    var site = world.SpawnBuildings[i];
                    if (BuildingAccess.TryGetById(site.Id, out GameBuilding* building, out _) && building->r_GlobalId == site.GlobalId &&
                        building->r_PlayerIdOwner == site.Owner && building->r_AliveState == AliveState.IsAlive && AIHunterWorldIndex.IsSpawnType(building->r_BuildingType))
                        mask |= 1 << site.Owner;
                }
                return mask;
            }

            private void ValidatePlacement(BuildingPlacementValidationEventArgs args)
            {
                if (!initialized || !active || faulted || mission == null || args.Mappers != eMappers.MAPPER_HUNTER ||
                    (!controlAiv && !automaticCall) || args.PlayerId < 1 || args.PlayerId >= AIHunterWorldIndex.PlayerSlots ||
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(args.PlayerId)) return;
                try
                {
                    if (!worldReady) CaptureBuildings();
                    if (!preyCaptured) CapturePreyAndWorkers();
                    if (!preyKnown) return;
                    bool baselineFood = (preyMask & (1 << args.PlayerId)) != 0 || (CanRenewChickens() && (CurrentSpawnSites() & (1 << args.PlayerId)) != 0);
                    if (!HunterHutPolicy.AllowsBuild((otherPreyMask & (1 << args.PlayerId)) != 0, baselineFood, CountHuts(args.PlayerId)))
                        GameTileManagerAPI.Instance.TileManager.IsPlacementBlocked = true;
                }
                catch (Exception error) { Fail(error); }
            }

            private void ProcessOneOperation()
            {
                // One expensive native attempt per tick; skipped candidates only traverse the small hut list.
                if (retire && retirement.Supported)
                while (retireCursor < world.Huts.Count)
                {
                    var hut = world.Huts[retireCursor++];
                    if ((otherPreyMask & (1 << hut.Owner)) != 0 || world.BusyHuts.Contains(hut.GlobalId) ||
                        HunterHutPolicy.Keep((otherPreyMask & (1 << hut.Owner)) != 0, (baselineFoodMask & (1 << hut.Owner)) != 0, hut.GlobalId,
                            (baselineFoodMask & (1 << hut.Owner)) != 0 ? CurrentBaseline(hut.Owner) : 0) ||
                        !Current(in hut, out var building) || building->r_AliveState != AliveState.IsAlive) continue;
                    bool removed = retirement.TryRetire(hut.Id, hut.GlobalId, hut.Owner, out string reason);
                    if (removed) Queue("RETIRED id=" + hut.Id + "; global=" + hut.GlobalId + "; player=" + hut.Owner);
                    else if (!retirement.Supported) Queue("RETIREMENT_DISABLED " + reason);
                    return;
                }
                if (automatic)
                while (buildCursor < AIHunterWorldIndex.PlayerSlots)
                {
                    int player = buildCursor++;
                    if (!world.AI[player] || (aivBlockedPlayers & (1 << player)) != 0 ||
                        (baselineFoodMask & (1 << player)) == 0 || CountHuts(player) != 0) continue;
                    automaticCall = true;
                    try
                    {
                        bool built = builder.TryBuild(player, CountHuts(player), out string reason, out bool aivFullyReserved);
                        if (aivFullyReserved)
                        {
                            aivBlockedPlayers |= 1 << player;
                            Queue("AUTOMATIC_DISABLED player=" + player + "; " + reason + "; until=mission-end");
                        }
                        if (built) { lastWait[player] = null; Queue("AUTOMATIC " + reason); }
                        else if (!aivFullyReserved && !string.Equals(lastWait[player], reason, StringComparison.Ordinal))
                        { lastWait[player] = reason; Queue("AUTOMATIC_WAIT player=" + player + "; " + reason); }
                    }
                    finally { automaticCall = false; }
                    return;
                }
                cycleActive = false;
            }

            private void Fail(Exception error) { faulted = true; preyKnown = false; cycleActive = false; Queue("FAILED mission suspended: " + error); }
            private void Queue(string message)
            {
                if (output.Count >= 256) return;
                output.Enqueue(message); outputWork?.Request();
            }
            private void Drain()
            {
                while (output.TryDequeue(out string message))
                {
                    if (message.StartsWith("FAILED ", StringComparison.Ordinal) || message.StartsWith("RETIREMENT_DISABLED ", StringComparison.Ordinal))
                        Shared.DebugLogHelper.LogError(owner.log, "AI_HUNTER " + message);
                    else Shared.DebugLogHelper.LogInfo(owner.log, "AI_HUNTER " + message);
                }
            }
        }
    }
}
