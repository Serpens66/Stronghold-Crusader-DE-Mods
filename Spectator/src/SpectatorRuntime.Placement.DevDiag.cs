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

namespace Spectator
{
    // Development diagnostics of the placement tools: input forwarding, wall/pitch/moat costs,
    // building visuals and unit groups. Only build.bat /devdiag compiles this file.
    internal static partial class SpectatorRuntime
    {
        private sealed partial class BuildingRequest
        {
            internal bool Spawned;
        }

        private sealed class BuildingVisualSample
        {
            internal int BuildingId, X, Y, NextTick, Samples;
            internal string Source;
        }

        // Counters reset with each mission session; all others are process-wide.
        private static readonly string[] SessionDiagnosticCounters = {
            "wall-preview", "wall-input", "wall-press", "wall-release", "wall-order", "wall-cost",
            "special-input", "pitch-cost", "moat-outcome", "delete"
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
        }

        static partial void DiagSubscribe()
        {
            tribeSubscription = TribeR3EventHooks.OnTribeAssignUnit.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnTribeAssigned);
            buildingSpawnSubscription = BuildingR3EventHooks.OnBuildingSpawn.Observable
                .Subscribe(OnBuildingSpawn);
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
                if (buildingId > 0 && GameBuildingManagerAPI.Instance.TryGetBuildingById(
                    buildingId, out GameBuilding* building) && building != null)
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
