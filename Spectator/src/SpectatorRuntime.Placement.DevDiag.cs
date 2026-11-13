#if SERP_DEVDIAG
using CrusaderDE;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Spectator
{
    // Development diagnostics of the placement tools: input forwarding, wall/pitch/moat costs,
    // building visuals and unit groups. Only build.bat /devdiag compiles this file.
    internal static partial class SpectatorRuntime
    {
        private sealed partial class BuildingRequest
        {
            internal bool Spawned;
            internal StoragePlacementInput TraceInput;
        }

        internal struct StoragePlacementInput
        {
            internal long Session;
            internal int Owner, CursorX, CursorY, ControlledHuman, RawBefore, MouseState;
            internal eMappers Mapper;
            internal bool Active, Known, Preview;
        }

        private struct StorageValidationSample
        {
            internal long Session, NextLog;
            internal int Owner, X, Y, Flags, Suppressed;
            internal eMappers Mapper;
            internal bool Seen;
        }

        // Fixed storage families x player slots, not a growing set of hovered map tiles.
        private const int StorageValidationPlayerSlots = GamePlayerManagerAPI.MAX_PLAYERS + 1;
        private static readonly StorageValidationSample[] storageValidationSamples = new StorageValidationSample[3 * StorageValidationPlayerSlots];
        private static readonly object storageValidationGate = new object();
        private static int storageValidationLines;
        private static IDisposable storageValidationSubscription;
        [ThreadStatic] private static StoragePlacementInput storageInput;
        [ThreadStatic] private static BuildingPlacementValidationEventArgs storageValidationPre;
        [ThreadStatic] private static StoragePlacementInput storageValidationInput;
        [ThreadStatic] private static int storageValidationDepth, storageValidationRawPre;

        private sealed class BuildingVisualSample
        {
            internal int BuildingId, X, Y, NextTick, Samples;
            internal string Source;
        }

        // Counters reset with each mission session; all others are process-wide.
        private static readonly string[] SessionDiagnosticCounters = {
            "wall-preview", "wall-input", "wall-press", "wall-release", "wall-order", "wall-cost",
            "special-input", "pitch-cost", "moat-outcome", "delete", "storage-click"
        };
        private static readonly Dictionary<string, int> diagnosticCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly List<BuildingVisualSample> visualSamples = new List<BuildingVisualSample>();
        private static readonly object visualSamplesLock = new object();
        private static IDisposable tribeSubscription, buildingSpawnSubscription;
        [ThreadStatic] private static int placingUnitOwner;
        private static HUD_Main pendingVisibilityHud;
        private static int pendingVisibilityScreen, pendingVisibilityOwner;
        private static DateTime pendingVisibilityAt = DateTime.MaxValue;

        private static int NextDiagnostic(string counter)
        {
            lock (diagnosticCounts)
            {
                diagnosticCounts.TryGetValue(counter, out int count);
                diagnosticCounts[counter] = count + 1;
                return count;
            }
        }

        private static int DiagnosticCount(string counter)
        {
            lock (diagnosticCounts) return diagnosticCounts.TryGetValue(counter, out int count) ? count : 0;
        }

        static partial void DiagLimited(string counter, int limit, Func<string> message)
        {
            if (NextDiagnostic(counter) < limit) Shared.DevDiag.Log(message);
        }

        static partial void DiagResetSession()
        {
            lock (diagnosticCounts)
                foreach (string counter in SessionDiagnosticCounters) diagnosticCounts.Remove(counter);
            lock (storageValidationGate)
            {
                Array.Clear(storageValidationSamples, 0, storageValidationSamples.Length);
                storageValidationLines = 0;
            }
            DiagPlacementInputEnd();
        }

        static partial void DiagSubscribe()
        {
            tribeSubscription = TribeR3EventHooks.OnTribeAssignUnit.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnTribeAssigned);
            buildingSpawnSubscription = BuildingR3EventHooks.OnBuildingSpawn.Observable
                .Subscribe(OnBuildingSpawn);
            storageValidationSubscription = BuildingR3EventHooks.OnPlacementValidation.Observable
                .Subscribe(OnStoragePlacementValidation);
        }

        static partial void DiagPlacementInput(eMappers mapper, int owner, int x, int y, bool constructingOnly, int mouseState)
        {
            storageInput = default;
            if (StorageMapperSlot(mapper) < 0) return;
            try
            {
                storageInput = new StoragePlacementInput {
                    Session = CurrentSessionId, Owner = owner, CursorX = x, CursorY = y, Mapper = mapper,
                    ControlledHuman = APIShared.PlayerPerspectiveAPI.GetControlledPlayerId(),
                    RawBefore = APIShared.PlayerPerspectiveAPI.GetRawNativeViewPlayerId(),
                    Active = true, Known = true, Preview = constructingOnly, MouseState = mouseState
                };
            }
            catch (Exception error)
            {
                DiagLimited("storage-validation-error", 4, () => "SPECTATOR_STORAGE_INPUT_ERROR: " + error);
            }
        }

        static partial void DiagPlacementInputEnd()
        {
            storageInput = default;
            storageValidationPre = null;
            storageValidationInput = default;
            storageValidationDepth = 0;
        }

        private static int StorageMapperSlot(eMappers mapper) => mapper == eMappers.MAPPER_STORES ? 0 :
            mapper == eMappers.MAPPER_GRANARY ? 1 : mapper == eMappers.MAPPER_ARMOURY ? 2 : -1;

        private static bool MatchStorageRequest(BuildingRequest request, BuildingPlacementValidationEventArgs args) =>
            request != null && IsCurrentSession(request.SessionId) && DateTime.UtcNow <= request.ExpiresAtUtc &&
            request.Mapper == args.Mappers && request.X - request.Scale / 2 == args.TileX &&
            request.Y - request.Scale / 2 == args.TileY;

        private static void OnStoragePlacementValidation(BuildingPlacementValidationEventArgs args)
        {
            try
            {
                if (args.Phase == EventHookPhase.Pre)
                {
                    if (storageValidationPre != null && !IsCurrentSession(storageValidationInput.Session))
                        DiagPlacementInputEnd();
                    if (storageValidationPre != null) { storageValidationDepth++; return; }
                    StoragePlacementInput input = storageInput;
                    BuildingRequest request = pendingBuilding;
                    bool matchingRequest = MatchStorageRequest(request, args);
                    if (!input.Active)
                    {
                        if (!matchingRequest || StorageMapperSlot(args.Mappers) < 0) return;
                        input = request.TraceInput;
                        input.Active = false; // Queued construction, outside the managed input wrapper.
                        if (!input.Known) input = new StoragePlacementInput {
                            Session = request.SessionId, Owner = request.Owner, Mapper = request.Mapper,
                            CursorX = request.X, CursorY = request.Y, ControlledHuman = -1, RawBefore = -1,
                            MouseState = -1
                        };
                    }
                    else if (matchingRequest && !input.Preview) request.TraceInput = input;
                    if (!IsCurrentSession(input.Session) || StorageMapperSlot(input.Mapper) < 0) return;
                    storageValidationPre = args;
                    storageValidationInput = input;
                    storageValidationDepth = 1;
                    storageValidationRawPre = APIShared.PlayerPerspectiveAPI.GetRawNativeViewPlayerId();
                    return;
                }
                if (args.Phase != EventHookPhase.Post || storageValidationPre == null || --storageValidationDepth > 0) return;
                BuildingPlacementValidationEventArgs before = storageValidationPre;
                StoragePlacementInput captured = storageValidationInput;
                storageValidationPre = null;
                storageValidationInput = default;
                var tiles = GameTileManagerAPI.Instance.TileManager;
                bool blocked = tiles.IsPlacementBlocked, overridden = tiles.UsePlacementBlockedOverride;
                bool overrideValue = tiles.PlacementBlockedOverrideValue;
                int rawPost = APIShared.PlayerPerspectiveAPI.GetRawNativeViewPlayerId();
                int flags = (blocked ? 1 : 0) | (overridden ? 2 : 0) | (overrideValue ? 4 : 0) |
                    (before.CustomValidationRules ? 8 : 0) | (before.ForceBlockPlacementState ? 16 : 0) |
                    (captured.Active ? 32 : 0) | (captured.Preview ? 64 : 0);
                int mapperSlot = StorageMapperSlot(captured.Mapper), ownerSlot = before.PlayerId;
                if (mapperSlot < 0 || !IsCurrentSession(captured.Session)) return;
                if (captured.Active && !captured.Preview && captured.MouseState == 1)
                    DiagStorageClickSnapshot(captured, before);
                if (ownerSlot < 1 || ownerSlot > GamePlayerManagerAPI.MAX_PLAYERS) ownerSlot = 0; // Cache slot only; logged raw input stays unchanged.
                long now = Stopwatch.GetTimestamp();
                int suppressed;
                lock (storageValidationGate)
                {
                    int slot = mapperSlot * StorageValidationPlayerSlots + ownerSlot;
                    StorageValidationSample last = storageValidationSamples[slot];
                    bool changed = !last.Seen || last.Session != captured.Session || last.Owner != before.PlayerId ||
                        last.X != before.TileX || last.Y != before.TileY || last.Mapper != before.Mappers || last.Flags != flags;
                    bool click = captured.Active && !captured.Preview && captured.MouseState == 1;
                    if (storageValidationLines >= 256 || (!changed && !click)) return;
                    if (now < last.NextLog && !click)
                    {
                        last.Suppressed++;
                        storageValidationSamples[slot] = last;
                        return;
                    }
                    suppressed = last.Suppressed;
                    storageValidationSamples[slot] = new StorageValidationSample {
                        Seen = true, Session = captured.Session, Owner = before.PlayerId,
                        X = before.TileX, Y = before.TileY, Mapper = before.Mappers, Flags = flags,
                        NextLog = now + Stopwatch.Frequency / 4
                    };
                    storageValidationLines++;
                }
                // The retained Pre object now holds the final mutable fields that the wrapper
                // passed to Original. The separate Post args deliberately carry ORIGINAL inputs.
                Shared.DevDiag.Log(() => "SPECTATOR_STORAGE_VALIDATION session=" + captured.Session +
                    " selectedOwnerAtInput=" + captured.Owner + " inputKnown=" + captured.Known +
                    " insideInput=" + captured.Active + " mapper=" + captured.Mapper +
                    " cursor=" + captured.CursorX + "," + captured.CursorY +
                    " previewAtInput=" + captured.Preview + " mouseAtInput=" + captured.MouseState +
                    " humanAtInput=" + captured.ControlledHuman + " rawBeforeInput=" + captured.RawBefore +
                    " rawPre=" + storageValidationRawPre + " rawPost=" + rawPost +
                    " effectiveOwner=" + before.PlayerId + " effectiveMapper=" + before.Mappers +
                    " effectiveOrigin=" + before.TileX + "," + before.TileY +
                    " effectiveA6=" + before.Unknown1 + " effectiveA7=" + before.Unknown2 +
                    " postOriginalOwner=" + args.PlayerId + " postOriginalMapper=" + args.Mappers +
                    " postOriginalOrigin=" + args.TileX + "," + args.TileY +
                    " blocked=" + blocked + " customRules=" + before.CustomValidationRules +
                    " forceBlock=" + before.ForceBlockPlacementState + " overrideEnabled=" + overridden +
                    " overrideValue=" + overrideValue + " suppressedTransitions=" + suppressed);
            }
            catch (Exception error)
            {
                storageValidationPre = null; storageValidationDepth = 0;
                DiagLimited("storage-validation-error", 4, () => "SPECTATOR_STORAGE_VALIDATION_ERROR: " + error);
            }
        }

        private static void DiagStorageClickSnapshot(StoragePlacementInput input,
            BuildingPlacementValidationEventArgs effective)
        {
            if (NextDiagnostic("storage-click") >= 12) return;
            try
            {
                string selected = DescribeStorageOwner(input.Owner, input.Mapper);
                string forwarded = effective.PlayerId == input.Owner && effective.Mappers == input.Mapper
                    ? "same-as-selected" : DescribeStorageOwner(effective.PlayerId, effective.Mappers);
                Shared.DevDiag.Log(() => "SPECTATOR_STORAGE_CLICK session=" + input.Session +
                    " cursor=" + input.CursorX + "," + input.CursorY +
                    " effectiveOrigin=" + effective.TileX + "," + effective.TileY +
                    " selected={" + selected + "} forwarded={" + forwarded + "}");
            }
            catch (Exception error)
            {
                DiagLimited("storage-validation-error", 4, () => "SPECTATOR_STORAGE_SNAPSHOT_ERROR: " + error);
            }
        }

        private static unsafe string DescribeStorageOwner(int owner, eMappers mapper)
        {
            if (owner < 1 || owner > GamePlayerManagerAPI.MAX_PLAYERS || StorageMapperSlot(mapper) < 0)
                return "owner=" + owner + " mapper=" + mapper + " unsupported";
            eStructs family = mapper == eMappers.MAPPER_STORES ? eStructs.STRUCT_GOODS_YARD :
                mapper == eMappers.MAPPER_GRANARY ? eStructs.STRUCT_GRANARY : eStructs.STRUCT_ARMOURY;
            bool resourcesFound = GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner, out GamePlayerResources* resources);
            uint first = !resourcesFound || resources == null ? 0 : mapper == eMappers.MAPPER_STORES
                ? resources->r_FirstGoodsyardId : mapper == eMappers.MAPPER_GRANARY
                ? resources->r_FirstGranaryId : resources->r_FirstArmouryId;
            var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            int alive = 0, nativeCountCandidates = 0;
            // Actual clicks only, bounded by the SDK span. B8140 additionally checks
            // signed building+184 <=0. Its native high-water bound is not exposed here,
            // so these are observed span counts, not a fabricated native return value.
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (building.r_AliveState != AliveState.IsAlive || building.r_PlayerIdOwner != owner ||
                    building.r_BuildingType != family) continue;
                alive++;
                if (unchecked((int)building.r_CurrentGoodStackAmount) <= 0) nativeCountCandidates++;
            }
            string firstRecord = !resourcesFound ? "resources-unavailable" : first == 0 ? "none" : "id-outside-sdk-span";
            if (first > 0 && first <= buildings.Length)
            {
                ref GameBuilding building = ref buildings[(int)first - 1];
                firstRecord = "owner:" + building.r_PlayerIdOwner + ",type:" + building.r_BuildingType +
                    ",alive:" + building.r_AliveState + ",tile:" + building.r_TilePositionXBegin +
                    "," + building.r_TilePositionYBegin + ",size:" + building.r_OccupyTileGridSize +
                    ",stack184:" + building.r_CurrentGoodStackAmount;
            }
            return "owner=" + owner + " family=" + family + " slotsScanned=" + buildings.Length +
                " observedAlive=" + alive + " observedNativeCountCandidates=" + nativeCountCandidates +
                " firstId=" + first + " firstRecord=[" + firstRecord + "]";
        }

        static partial void DiagPlacingUnit(int owner) => placingUnitOwner = owner;

        static partial void DiagIconsChanged(HUD_Main hud, string reason, int screen, int owner)
        {
            Shared.DevDiag.Log(() => "SPECTATOR_EDITOR_ICONS reason=" + reason + " screen=" + screen +
                " owner=" + owner + " " + DescribeIconVisibility(hud, screen));
            if (DiagnosticCount("visibility") >= 32) return;
            pendingVisibilityHud = hud;
            pendingVisibilityScreen = screen;
            pendingVisibilityOwner = owner;
            pendingVisibilityAt = DateTime.UtcNow.AddMilliseconds(650);
        }

        static partial void DiagIconsSettled(HUD_Main hud)
        {
            if (!ReferenceEquals(hud, pendingVisibilityHud) || DateTime.UtcNow < pendingVisibilityAt) return;
            pendingVisibilityHud = null;
            pendingVisibilityAt = DateTime.MaxValue;
            if (MainViewModel.Instance.buildScreenID != pendingVisibilityScreen ||
                CurrentAiOwner() != pendingVisibilityOwner) return;
            NextDiagnostic("visibility");
            Shared.DevDiag.Log(() => "SPECTATOR_EDITOR_ICONS_SETTLED screen=" + pendingVisibilityScreen +
                " owner=" + pendingVisibilityOwner + " " + DescribeIconVisibility(hud, pendingVisibilityScreen) +
                " frozen=" + MainViewModel.Instance.FreezeMainControls +
                " blankSuppressed=" + DiagnosticCount("blank-suppressed"));
        }

        static partial void DiagForgetIcons()
        {
            pendingVisibilityHud = null;
            pendingVisibilityAt = DateTime.MaxValue;
        }

        private static string DescribeIconVisibility(HUD_Main hud, int screen)
        {
            int[,] lists = (int[,])buildIconListsField.GetValue(hud);
            Button[] buttons = (Button[])buildButtonsField.GetValue(hud);
            int selected = 0, visible = 0, rendered = 0, enabled = 0;
            for (int slot = 0; slot < lists.GetLength(1); slot++)
            {
                int index = lists[screen, slot];
                if (index <= 0) break;
                Button button = buttons[AlternateBuildingButton(index)];
                if (button == null) continue;
                selected++;
                if (button.Visibility == Visibility.Visible) visible++;
                if (button.IsVisible) rendered++;
                if (button.IsEnabled) enabled++;
            }
            return "selected=" + selected + " visible=" + visible + " rendered=" + rendered + " enabled=" + enabled;
        }

        static partial void DiagSimulationTick(int tick)
        {
            // The existing simulation callback supplies a throughput counter alongside render time.
            Shared.DevDiag.Count("spectator.sim.tick");
            lock (visualSamplesLock)
            {
                for (int index = visualSamples.Count - 1; index >= 0; index--)
                {
                    BuildingVisualSample sample = visualSamples[index];
                    if (tick < sample.NextTick) continue;
                    Shared.DevDiag.Log(() => "SPECTATOR_EDITOR_BUILD_VISUAL_LATER tick=" + tick +
                        " source=" + sample.Source + " buildingId=" + sample.BuildingId +
                        " " + DescribeBuildingVisual(sample.BuildingId, sample.X, sample.Y));
                    sample.Samples++;
                    if (sample.Samples >= 3) visualSamples.RemoveAt(index);
                    else sample.NextTick = tick + (sample.Samples == 1 ? 4 : 16);
                }
            }
        }

        static partial void DiagBuildExpired(int tick, BuildingRequest request) =>
            DiagLimited("building", 20, () => "SPECTATOR_EDITOR_BUILD_EXPIRED tick=" + tick +
                " owner=" + request.Owner + " mapper=" + request.Mapper + " spawned=" + request.Spawned);

        static partial void DiagBuildFinished(BuildStructureEventArgs args, BuildingRequest request) =>
            DiagLimited("building", 24, () => "SPECTATOR_EDITOR_BUILD_FINISHED owner=" + args.PlayerId +
                " mapper=" + args.Mappers + " spawned=" + request.Spawned);

        private static void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (args.Building != eStructs.STRUCT_WELL && pendingBuilding == null) return;
            BuildingRequest request = pendingBuilding;
            bool ours = request != null && request.BuildStarted &&
                request.Owner == args.PlayerId &&
                request.X - request.Scale / 2 == args.TileX &&
                request.Y - request.Scale / 2 == args.TileY;
            if (!ours && (args.Building != eStructs.STRUCT_WELL || NextDiagnostic("vanilla-well") >= 8)) return;
            if (args.Phase == EventHookPhase.Post && ours)
                request.Spawned = args.ReturnValue > 0;
            Shared.DevDiag.Log(() => "SPECTATOR_EDITOR_BUILD_VISUAL source=" + (ours ? "spectator" : "vanilla") +
                " phase=" + args.Phase + " owner=" + args.PlayerId +
                " building=" + args.Building + " tile=" + args.TileX + "," + args.TileY +
                " buildingId=" + args.ReturnValue + " visualOwner=" + args.VisualPlayerId +
                " variation=" + args.SpriteVariationIndex + " " +
                DescribeBuildingVisual((int)args.ReturnValue, args.TileX, args.TileY));
            if (args.Phase == EventHookPhase.Post && args.ReturnValue > 0 &&
                args.Building == eStructs.STRUCT_WELL)
                lock (visualSamplesLock)
                    visualSamples.Add(new BuildingVisualSample {
                        BuildingId = (int)args.ReturnValue, X = args.TileX, Y = args.TileY,
                        Source = ours ? "spectator" : "vanilla"
                    });
        }

        private static unsafe string DescribeBuildingVisual(int buildingId, int x, int y)
        {
            try
            {
                int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                var gfx = GameTileManagerAPI.Instance.GetGfxLayer();
                var alpha = GameTileManagerAPI.Instance.GetAlphaGfxLayer();
                var structure = GameTileManagerAPI.Instance.GetStructureLayer();
                string tiles = tileId >= 0 && tileId < gfx.Length &&
                    tileId < alpha.Length && tileId < structure.Length ?
                    "gfx=" + gfx[tileId] + " alpha=" + alpha[tileId] +
                    " structure=" + structure[tileId] : "tile=invalid";
                if (buildingId > 0 && global::APIShared.BuildingAccess.TryGetById(
                    buildingId, out GameBuilding* building, out _) && building != null)
                    return tiles + " record=" + building->r_BuildingType + "/" +
                        building->r_SpriteVariationIndex + "/" +
                        building->r_SpritePlayerColorId + "/" +
                        building->r_TilePositionXBegin + "," + building->r_TilePositionYBegin;
                return tiles + " record=none";
            }
            catch (Exception error)
            {
                LogOnce("SPECTATOR_EDITOR_VISUAL_DIAGNOSTIC_ERROR: " + error);
                return "visual=unavailable";
            }
        }

        private static unsafe void OnTribeAssigned(TribeAssignUnitEventArgs args)
        {
            int owner = placingUnitOwner;
            if (owner <= 0 || DiagnosticCount("unit-group") >= 32) return;
            try
            {
                if (!APIShared.UnitAccess.TryGetById(args.UnitId, out GameUnit* unit, out _) ||
                    unit == null || unit->r_ControllableForPlayerId != owner)
                    return;
                NextDiagnostic("unit-group");
                var role = unit->r_AITribeRole;
                Shared.DevDiag.Log(() => "SPECTATOR_EDITOR_UNIT_GROUP owner=" + owner + " unitId=" +
                    args.UnitId + " tribeId=" + args.TribeId + " aiRole=" + role);
            }
            catch (Exception error) { LogOnce("SPECTATOR_EDITOR_UNIT_DIAGNOSTIC_ERROR: " + error); }
        }
    }
}
#endif
