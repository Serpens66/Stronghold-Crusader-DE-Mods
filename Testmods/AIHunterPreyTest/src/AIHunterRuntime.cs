using System;
using System.Collections.Concurrent;
using APIShared;
using APIShared.Simulation;
using APIShared.Wildlife;
using SHCDESE.API;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AIHunterPreyTest
{
    internal sealed unsafe class AIHunterRuntime
    {
        private readonly AIHunterSettings settings;
        private readonly HunterRetirementNative retirement;
        private readonly AutomaticHunterBuilder builder;
        private readonly ImprovedHuntersSettingsBridge bridge = new ImprovedHuntersSettingsBridge();
        private readonly ConcurrentQueue<string> output = new ConcurrentQueue<string>();
        private readonly int[] preyCounts = new int[GamePlayerManagerAPI.MAX_PLAYERS + 1];
        private ImprovedHuntersSettingsSnapshot hunting;
        private MissionContext mission;
        private bool enabled, controlAiv, automatic, retire, started, known, scanned, automaticCall, faulted, initializationDisabled;
        private int lastScan, presentMask, otherPreyMask, blocked;
        internal Action OutputQueued;

        internal AIHunterRuntime(AIHunterSettings settings, HunterRetirementNative retirement)
        { this.settings = settings; this.retirement = retirement; builder = new AutomaticHunterBuilder(retirement.AuditedImage); }

        internal void InitializeMission(MissionLifecycleNotification notification)
        {
            if (notification.Phase == MissionInitializationPhase.BeforeLoad)
            {
                Reset(); mission = notification.Context;
            }
            if (notification.Phase == MissionInitializationPhase.BeforeNativeStart ||
                notification.Phase == MissionInitializationPhase.NativeLoaded)
            {
                if (mission?.SessionId != notification.Context.SessionId) Reset();
                mission = notification.Context;
                CaptureSettings();
                known = scanned = false;
            }
        }

        internal void Start(MissionLifecycleNotification notification)
        {
            if (mission?.SessionId != notification.Context.SessionId) Reset();
            mission = notification.Context;
            CaptureSettings();
            started = true; scanned = known = false;
            Queue("SESSION id=" + mission.SessionId + "; enabled=" + enabled + "; aiv=" + controlAiv +
                "; automatic=" + automatic + "; retire=" + retire + "; IH=" + hunting.Status +
                "; mode=" + mission.Mode.ToDiagnosticString());
        }

        private void CaptureSettings()
        {
            enabled = settings.EnableMod;
            controlAiv = settings.ControlAivHunters;
            automatic = settings.AutoBuildHunters;
            retire = settings.RetireEmptyHunters;
            hunting = bridge.Capture(mission.Mode.AllowsCustomGameMods || mission.IsEditor);
            if (hunting.Status == ImprovedHuntersSettingsStatus.Unavailable)
                Queue("PREY_UNKNOWN " + hunting.Diagnostic + "; no build or removal decisions");
        }

        internal void End(MissionLifecycleNotification notification) { Reset(); }
        internal void Disable() { Reset(); initializationDisabled = true; }
        private void Reset()
        {
            mission = null; started = enabled = known = scanned = automaticCall = faulted = false;
            presentMask = otherPreyMask = blocked = 0; hunting = null;
            Array.Clear(preyCounts, 0, preyCounts.Length);
        }

        internal void Tick(int tick)
        {
            if (initializationDisabled || !enabled || !started || faulted || mission == null || !PreyPresencePolicy.Due(scanned, lastScan, tick)) return;
            try
            {
                RefreshPrey(tick);
                if (!known) return;
                int removed = 0, deferred = 0, built = 0;
                var baselineFood = new bool[GamePlayerManagerAPI.MAX_PLAYERS + 1];
                for (int player = 1; player < baselineFood.Length; player++)
                    if (IsAI(player)) baselineFood[player] = HasBaselineFood(player);
                if (retire && retirement.Supported)
                {
                    var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                    var baselines = new uint[GamePlayerManagerAPI.MAX_PLAYERS + 1];
                    for (int player = 1; player < baselines.Length; player++)
                    {
                        int granaryId = GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out var resources)
                            ? checked((int)resources->r_FirstGranaryId) : 0;
                        baselines[player] = HunterHutPolicy.SelectBaseline(buildings, player, granaryId);
                    }
                    for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
                    {
                        ref readonly GameBuilding building = ref buildings[spanIndex];
                        int owner = building.r_PlayerIdOwner;
                        if (building.r_AliveState != AliveState.IsAlive || building.r_BuildingType != eStructs.STRUCT_HUNTERS_HUT ||
                            !IsAI(owner) || HunterHutPolicy.Keep(HasOtherPrey(owner), baselineFood[owner], building.r_GlobalId, baselines[owner])) continue;
                        if (retirement.TryRetire(spanIndex + 1, building.r_GlobalId, owner, out string reason))
                        { removed++; Queue("RETIRED id=" + (spanIndex + 1) + "; player=" + owner + "; " + reason); }
                        else
                        {
                            deferred++;
                            if (!retirement.Supported) { Queue("RETIREMENT_DISABLED " + reason); break; }
                        }
                    }
                }
                if (automatic)
                    for (int player = 1; player <= GamePlayerManagerAPI.MAX_PLAYERS; player++)
                    {
                        if (!IsAI(player) || !baselineFood[player]) continue;
                        automaticCall = true;
                        try
                        {
                            if (builder.TryBuild(player, 1, out string reason))
                            { built++; Queue("AUTOMATIC " + reason); }
                            else if (reason != "hut-limit") Queue("AUTOMATIC_WAIT player=" + player + "; " + reason);
                        }
                        finally { automaticCall = false; }
                    }
                Queue("SCAN tick=" + tick + "; preyMask=" + presentMask + "; otherPreyMask=" + otherPreyMask + "; playerCounts=" + string.Join(",", preyCounts) +
                    "; retired=" + removed + "; deferred=" + deferred + "; built=" + built + "; aivBlocked=" + blocked +
                    "; catalog=" + HuntablePrey.Current.Digest);
                blocked = 0;
            }
            catch (Exception error) { Fault(error); }
        }

        internal void ValidatePlacement(BuildingPlacementValidationEventArgs args)
        {
            if (initializationDisabled || !enabled || faulted || mission == null || args.Mappers != eMappers.MAPPER_HUNTER ||
                (!controlAiv && !automaticCall) || !IsAI(args.PlayerId)) return;
            try
            {
                // At prebuild the native archive/unit normalization already finished. No old
                // map snapshot is reused; OnStart forces a new scan after all start features.
                if (!scanned)
                {
                    int tick = 0;
                    SimulationClock.TryGetCurrentTick(mission, out tick);
                    RefreshPrey(tick);
                }
                if (!known) return;
                // Fresh hut count for every synchronous constructor attempt, including AIV prebuild.
                bool allowed = HunterHutPolicy.AllowsBuild(HasOtherPrey(args.PlayerId), HasBaselineFood(args.PlayerId),
                    HunterHutPolicy.Count(GameBuildingManagerAPI.Instance.GetBuildingsAsSpan(), args.PlayerId));
                if (!PreyPresencePolicy.ShouldBlock(enabled, controlAiv, automaticCall, true, known, allowed)) return;
                // The SE wrapper ignores Post args overrides. Its global override has already
                // run; only add a rejection to the result the constructor reads next.
                GameTileManagerAPI.Instance.TileManager.IsPlacementBlocked = true;
                if (++blocked <= 3) Queue("BUILD_BLOCKED player=" + args.PlayerId + "; tile=" + args.TileX + "," + args.TileY);
            }
            catch (Exception error) { Fault(error); }
        }

        private void RefreshPrey(int tick)
        {
            scanned = true; lastScan = tick; known = false; presentMask = otherPreyMask = 0;
            Array.Clear(preyCounts, 0, preyCounts.Length);
            if (!HuntablePrey.IsAvailable || hunting == null || hunting.Status == ImprovedHuntersSettingsStatus.Unavailable) return;
            HuntablePreyCatalog catalog = HuntablePrey.Current;
            var units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            fixed (GameUnit* first = units)
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref readonly GameUnit unit = ref units[spanIndex];
                if (unit.r_AliveState != AliveState.IsAlive) continue;
                int type = (int)unit.r_UnitChimp;
                if (!catalog.TryGet(type, out HuntablePreyEntry entry) || !hunting.AllowsType(type, entry)) continue;
                bool live = unit.r_IsKilledByProjectile == 0 && unit.r_CurrentHealth > 0;
                // Existing collectable corpses preserve resource presence, while the drain
                // predicate separately protects every hunter already processing/delivering.
                bool corpse = !live && hunting.IsCollectibleCorpse(first + spanIndex);
                int mask = PreyPresencePolicy.OwnerMask(true, live, corpse, unit.r_ControllableForPlayerId, entry.AllowsOwnPlayerPrey);
                presentMask |= mask;
                if (type != (int)eChimps.CHIMP_TYPE_CHICKEN) otherPreyMask |= mask;
                for (int player = 1; player <= GamePlayerManagerAPI.MAX_PLAYERS; player++)
                    if ((mask & (1 << player)) != 0) preyCounts[player]++;
            }
            known = true;
        }

        private bool HasPrey(int player) => player >= 1 && player <= GamePlayerManagerAPI.MAX_PLAYERS && (presentMask & (1 << player)) != 0;
        private bool HasOtherPrey(int player) => player >= 1 && player <= GamePlayerManagerAPI.MAX_PLAYERS && (otherPreyMask & (1 << player)) != 0;
        private bool HasBaselineFood(int player) => HasPrey(player) ||
            (!mission.IsEditor && hunting.CanRenewChickens &&
             HuntablePrey.Current.TryGet((int)eChimps.CHIMP_TYPE_CHICKEN, out var chicken) && chicken.AllowsOwnPlayerPrey &&
             hunting.AllowsType((int)eChimps.CHIMP_TYPE_CHICKEN, chicken) &&
             HunterHutPolicy.HasSpawnSite(GameBuildingManagerAPI.Instance.GetBuildingsAsSpan(), player));
        private static bool IsAI(int player) => player >= 1 && player <= GamePlayerManagerAPI.MAX_PLAYERS && GamePlayerManagerAPI.Instance.IsAIPlayer(player);
        private void Fault(Exception error)
        { faulted = true; known = false; Queue("FAILED feature suspended for this mission: " + error); }
        private void Queue(string text)
        {
            if (output.Count >= 256) return;
            output.Enqueue(text); OutputQueued?.Invoke();
        }
        internal void Drain()
        { while (output.TryDequeue(out string text)) AIHunterPreyPlugin.Info(text); }
    }
}
