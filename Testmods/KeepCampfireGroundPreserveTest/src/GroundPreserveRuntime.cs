using System;
using System.Collections.Generic;
using APIShared;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace KeepCampfireGroundPreserveTest
{
    internal sealed class GroundPreserveRuntime
    {
        private const int SampleSide = 7;
        private const int MaxTargets = 24;
        private readonly Action<string> log;
        private readonly Action<string> error;
        private readonly List<CampObservation> camps = new List<CampObservation>();
        private readonly object gate = new object();
        private CampgroundNativeHook hook;
        private TerrainPhaseDiagnostic terrainPhase;
        private bool missionAllowed;
        private bool firstTick;
        private long sessionId;
        private long lastSkipCount;
        private long lastFirePatchCount;

        internal GroundPreserveRuntime(Action<string> log, Action<string> error)
        {
            this.log = log;
            this.error = error;
        }

        internal void SetHook(CampgroundNativeHook value) => hook = value;
        internal void SetTerrainPhase(TerrainPhaseDiagnostic value) => terrainPhase = value;

        internal void OnInitialization(MissionLifecycleNotification notification)
        {
            if (notification.Phase != MissionInitializationPhase.BeforeLoad) return;
            lock (gate)
            {
                hook?.SetEnabled(false);
                terrainPhase?.SetEnabled(false);
                camps.Clear();
                firstTick = false;
                sessionId = notification.Context.SessionId;
                missionAllowed = !notification.Context.Mode.IsRealMultiplayer &&
                    !notification.Context.Mode.MultiplayerSave;
                if (missionAllowed && hook != null && hook.IsPublished)
                    hook.SetEnabled(true);
                if (missionAllowed && terrainPhase != null)
                    terrainPhase.SetEnabled(true);
                lastSkipCount = hook?.SuppressedStores ?? 0;
                lastFirePatchCount = hook?.FirePatchStores ?? 0;
                log("init session=" + sessionId + " kind=" + notification.Context.StartKind +
                    " allowed=" + missionAllowed + " hook=" + (hook?.IsPublished ?? false) +
                    " mode=" + notification.Context.Mode.ToDiagnosticString());
                log("terrain-phase diagnosis read-only: watching initial Keep recalculation and fill");
            }
        }

        internal void OnStart(MissionLifecycleNotification notification)
        {
            lock (gate)
            {
                sessionId = notification.Context.SessionId;
                if (missionAllowed && hook != null && hook.IsPublished)
                    hook.SetEnabled(true);
                if (missionAllowed && terrainPhase != null)
                    terrainPhase.SetEnabled(true);
                try { DiscoverExistingCampgrounds(); }
                catch (Exception ex) { error("existing-camp discovery failed: " + ex); }
                log("START session=" + sessionId + " kind=" +
                    notification.Context.StartKind + " targets=" + camps.Count +
                    " suppressedStores=" + (hook?.SuppressedStores ?? 0));
            }
        }

        internal void OnEnd(MissionLifecycleNotification notification)
        {
            lock (gate)
            {
                hook?.SetEnabled(false);
                terrainPhase?.SetEnabled(false);
                log("END session=" + notification.Context.SessionId + " reason=" +
                    notification.EndReason + " targets=" + camps.Count +
                    " suppressedStores=" + (hook?.SuppressedStores ?? 0));
                camps.Clear();
                missionAllowed = false;
            }
        }

        internal void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre &&
                (int)args.Building >= 0x28 && (int)args.Building <= 0x2C)
            {
                try { terrainPhase?.ArmKeep(args.PlayerId, args.TileX, args.TileY); }
                catch (Exception ex) { error("terrain-phase arming failed: " + ex); }
            }
            if (args.Building != eStructs.STRUCT_CAMPGROUND) return;
            lock (gate)
            {
                try
                {
                    if (args.Phase == EventHookPhase.Pre)
                    {
                        if (camps.Count >= MaxTargets) {
                            error("target cap reached at campground spawn");
                            return;
                        }
                        CampObservation camp = new CampObservation(args.PlayerId, args.TileX, args.TileY);
                        camps.Add(camp);
                        Capture(camp, "spawn-pre", true);
                    }
                    else if (args.Phase == EventHookPhase.Post)
                    {
                        CampObservation camp = FindPending(args.PlayerId, args.TileX, args.TileY);
                        if (camp == null) {
                            if (camps.Count >= MaxTargets) return;
                            camp = new CampObservation(args.PlayerId, args.TileX, args.TileY);
                            camps.Add(camp);
                            log("spawn POST without PRE; original floor unavailable");
                        }
                        camp.BuildingId = (int)args.ReturnValue;
                        camp.PostSeen = true;
                        camp.RemainingChecks = 4;
                        Capture(camp, "spawn-post", false);
                    }
                }
                catch (Exception ex) { error("spawn diagnosis failed: " + ex); }
            }
        }

        internal void OnBuildingDelete(BuildingDeleteEventArgs args)
        {
            lock (gate)
            {
                CampObservation camp = FindById(args.BuildingId);
                if (camp == null) return;
                try
                {
                    if (args.Phase == EventHookPhase.Pre)
                    {
                        camp.Deleting = true;
                        camp.RemainingChecks = 3;
                        Capture(camp, "delete-pre", false);
                    }
                    else
                        Capture(camp, "delete-post", false);
                }
                catch (Exception ex) { error("delete diagnosis failed: " + ex); }
            }
        }

        internal void OnTick(int tick)
        {
            terrainPhase?.FlushCompleted();
            lock (gate)
            {
                if (!firstTick) {
                    firstTick = true;
                    log("post-cleanup runtime tick session=" + sessionId + " tick=" + tick);
                }
                long skipped = hook?.SuppressedStores ?? 0;
                long firePatch = hook?.FirePatchStores ?? 0;
                if (skipped != lastSkipCount) {
                    log("native hook confirmed: suppressed graphic-store iterations=" +
                        (skipped - lastSkipCount) + " total=" + skipped + " tick=" + tick);
                    lastSkipCount = skipped;
                }
                if (firePatch != lastFirePatchCount) {
                    log("native fire-patch stores=" + (firePatch - lastFirePatchCount) +
                        " total=" + firePatch + " tick=" + tick);
                    lastFirePatchCount = firePatch;
                }
                foreach (CampObservation camp in camps)
                {
                    if (camp.RemainingChecks <= 0) continue;
                    try { Capture(camp, "tick-" + tick, false); }
                    catch (Exception ex) { error("tick diagnosis failed: " + ex); }
                    camp.RemainingChecks--;
                }
            }
        }

        private void DiscoverExistingCampgrounds()
        {
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (building.r_BuildingType != eStructs.STRUCT_CAMPGROUND ||
                    building.r_AliveState != AliveState.IsAlive) continue;
                int buildingId = spanIndex + 1;
                if (FindById(buildingId) != null) continue;
                if (camps.Count >= MaxTargets) {
                    error("target cap reached while scanning loaded campgrounds");
                    return;
                }
                CampObservation camp = new CampObservation(building.r_PlayerIdOwner,
                    building.r_TilePositionXBegin, building.r_TilePositionYBegin) {
                        BuildingId = buildingId, PostSeen = true, RemainingChecks = 3
                    };
                camps.Add(camp);
                Capture(camp, "loaded-first-observed", true);
                log("loaded campground has no pre-spawn terrain reference: " + camp.Label);
            }
        }

        private CampObservation FindPending(int playerId, int x, int y)
        {
            for (int index = camps.Count - 1; index >= 0; index--)
                if (!camps[index].PostSeen && camps[index].PlayerId == playerId &&
                    camps[index].X == x && camps[index].Y == y) return camps[index];
            return null;
        }

        private CampObservation FindById(int id)
        {
            foreach (CampObservation camp in camps)
                if (id > 0 && camp.BuildingId == id) return camp;
            return null;
        }

        private void Capture(CampObservation camp, string stage, bool logCore)
        {
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            Span<int> gfx = api.GetGfxLayer();
            Span<int> alpha = api.GetAlphaGfxLayer();
            Span<ushort> structure = api.GetStructureLayer();
            int valid = 0, occupied = 0, unchanged = 0, campGraphics = 0, changed = 0;
            int examples = 0;
            bool detailed = stage == "spawn-pre" || stage == "loaded-first-observed" ||
                stage == "delete-post" ||
                (stage.StartsWith("tick-", StringComparison.Ordinal) &&
                    camp.RemainingChecks == 1);
            for (int dy = 0; dy < SampleSide; dy++)
            for (int dx = 0; dx < SampleSide; dx++)
            {
                int x = camp.X + dx, y = camp.Y + dy;
                if (!api.IsTileInsideMapBounds(x, y)) continue;
                int tileId = api.GetTileId(x, y);
                if (tileId < 0 || tileId >= gfx.Length || tileId >= alpha.Length ||
                    tileId >= structure.Length) continue;
                valid++;
                int slot = dy * SampleSide + dx;
                int value = gfx[tileId], alphaValue = alpha[tileId];
                ushort structureValue = structure[tileId];
                if (camp.BuildingId > 0 && structureValue == camp.BuildingId) occupied++;
                if ((value >> 16 & 0xFFFF) == 6) campGraphics++;
                if (camp.OriginalValid[slot] && value == camp.OriginalGfx[slot] &&
                    alphaValue == camp.OriginalAlpha[slot]) unchanged++;
                if (camp.PreviousValid[slot] && (value != camp.PreviousGfx[slot] ||
                    alphaValue != camp.PreviousAlpha[slot] ||
                    structureValue != camp.PreviousStructure[slot])) changed++;
                bool differsFromOriginal = camp.OriginalValid[slot] &&
                    (value != camp.OriginalGfx[slot] || alphaValue != camp.OriginalAlpha[slot]);
                if (detailed || (logCore && dx < 2 && dy < 2) ||
                    (differsFromOriginal && examples++ < 3))
                    log("TILE stage=" + stage + " " + camp.Label + " xy=" + x + "," + y +
                        " id=" + tileId + " gfx=0x" + value.ToString("X8") +
                        " alpha=0x" + alphaValue.ToString("X8") +
                        " structure=" + structureValue +
                        (camp.OriginalValid[slot] ? " original=0x" +
                            camp.OriginalGfx[slot].ToString("X8") + "/0x" +
                            camp.OriginalAlpha[slot].ToString("X8") : ""));
                if (stage == "spawn-pre") {
                    camp.OriginalValid[slot] = true;
                    camp.OriginalGfx[slot] = value;
                    camp.OriginalAlpha[slot] = alphaValue;
                }
                camp.PreviousValid[slot] = true;
                camp.PreviousGfx[slot] = value;
                camp.PreviousAlpha[slot] = alphaValue;
                camp.PreviousStructure[slot] = structureValue;
            }
            log("SUMMARY stage=" + stage + " " + camp.Label +
                " valid=" + valid + " occupied=" + occupied +
                " originalGraphicIntact=" + unchanged + " campGraphicFile6=" + campGraphics +
                " changedSincePrevious=" + changed + " deleting=" + camp.Deleting +
                " suppressedStores=" + (hook?.SuppressedStores ?? 0) +
                " firePatchStores=" + (hook?.FirePatchStores ?? 0));
        }

        private sealed class CampObservation
        {
            internal readonly int PlayerId, X, Y;
            internal readonly int[] OriginalGfx = new int[SampleSide * SampleSide];
            internal readonly int[] OriginalAlpha = new int[SampleSide * SampleSide];
            internal readonly bool[] OriginalValid = new bool[SampleSide * SampleSide];
            internal readonly int[] PreviousGfx = new int[SampleSide * SampleSide];
            internal readonly int[] PreviousAlpha = new int[SampleSide * SampleSide];
            internal readonly ushort[] PreviousStructure = new ushort[SampleSide * SampleSide];
            internal readonly bool[] PreviousValid = new bool[SampleSide * SampleSide];
            internal int BuildingId, RemainingChecks;
            internal bool PostSeen, Deleting;
            internal string Label => "player=" + PlayerId + "/id=" + BuildingId +
                "/origin=" + X + "," + Y;
            internal CampObservation(int playerId, int x, int y)
            { PlayerId = playerId; X = x; Y = y; }
        }
    }
}
