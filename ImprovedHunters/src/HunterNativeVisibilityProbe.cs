using APIShared.Sight;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace ImprovedHunters
{
    internal readonly struct HunterNearVisibilityGeometry
    {
        public readonly int HunterTileX;
        public readonly int HunterTileY;
        public readonly int PreyTileX;
        public readonly int PreyTileY;
        public readonly int HunterWorldX;
        public readonly int HunterWorldY;
        public readonly int HunterHeight;
        public readonly int PreyWorldX;
        public readonly int PreyWorldY;
        public readonly int PreyHeight;

        public HunterNearVisibilityGeometry(
            int hunterTileX,
            int hunterTileY,
            int preyTileX,
            int preyTileY,
            int hunterWorldX,
            int hunterWorldY,
            int hunterHeight,
            int preyWorldX,
            int preyWorldY,
            int preyHeight)
        {
            HunterTileX = hunterTileX;
            HunterTileY = hunterTileY;
            PreyTileX = preyTileX;
            PreyTileY = preyTileY;
            HunterWorldX = hunterWorldX;
            HunterWorldY = hunterWorldY;
            HunterHeight = hunterHeight;
            PreyWorldX = preyWorldX;
            PreyWorldY = preyWorldY;
            PreyHeight = preyHeight;
        }

        public int TileManhattanDistance =>
            Math.Abs(PreyTileX - HunterTileX) + Math.Abs(PreyTileY - HunterTileY);

        public int WorldChebyshevDistance =>
            Math.Max(Math.Abs(PreyWorldX - HunterWorldX), Math.Abs(PreyWorldY - HunterWorldY));
    }

    /// <summary>
    /// Improved Hunters' own gate around APIShared's <see cref="LineOfSight"/>: settings, Hunter and prey identity
    /// checks, crash breadcrumbs and bounded diagnostics. The native sight call, its hash-bound validation and its
    /// private-context guard belong to APIShared (APIShared.Sight). Installs no hook and changes no unit or order.
    /// </summary>
    internal sealed unsafe class HunterNativeVisibilityProbe : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private bool reentrancyLogged;
        private bool failureLogged;
        private bool disposed;

        public HunterNativeVisibilityProbe(ManualLogSource log, ImprovedHuntersViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (!LineOfSight.IsAvailable)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    "Improved Hunters native visibility is unavailable; Hunter behavior remains unchanged: " +
                    LineOfSight.UnavailableReason);
                return;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                "Improved Hunters native visibility initialized through APIShared.Sight.LineOfSight: " +
                "nativeHooks=False, behaviorNeutral=True.");
        }

        public bool IsAvailable => !disposed && LineOfSight.IsAvailable;

        public bool TryEvaluateDirectVisibility(
            int hunterUnitId,
            uint hunterGlobalId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            out int result)
        {
            result = 0;
            if (!IsAvailable ||
                !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) ||
                !settings.ImprovedPathfinding ||
                !settings.IsHuntingEnabled(preyType) ||
                hunterUnitId <= 0 ||
                preyUnitId <= 0 ||
                hunterGlobalId == 0 ||
                preyGlobalId == 0)
            {
                return false;
            }

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!APIShared.UnitAccess.TryGetById(unitApi, hunterUnitId, out GameUnit* hunter, out _) ||
                !APIShared.UnitAccess.TryGetById(unitApi, preyUnitId, out GameUnit* prey, out _) ||
                hunter == null ||
                prey == null ||
                hunter->r_GlobalId != hunterGlobalId ||
                prey->r_GlobalId != preyGlobalId ||
                hunter->r_AliveState != AliveState.IsAlive ||
                prey->r_AliveState != AliveState.IsAlive ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                prey->r_UnitChimp != preyType)
            {
                return false;
            }

            return TryInvokeVisibility(hunter, prey, out result);
        }

        /// <summary>Vanilla's target-query sight rule (<see cref="LineOfSight.IsVisibleForTargetQuery"/>); a failed query counts as seen.</summary>
        public bool SeesFromUnit(GameUnit* viewer, GameUnit* target)
        {
            return !IsAvailable ||
                !TryInvokeVisibility(viewer, target, out int result) ||
                LineOfSight.IsVisibleForTargetQuery(result);
        }

        public bool TryEvaluateNearVisibility(
            int hunterUnitId,
            uint hunterGlobalId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            out int wrapperResult,
            out int hunterToPreyResult,
            out int preyToHunterResult,
            out HunterNearVisibilityGeometry geometry)
        {
            wrapperResult = 0;
            hunterToPreyResult = -1;
            preyToHunterResult = -1;
            geometry = default;
            if (!IsAvailable ||
                !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) ||
                !settings.ImprovedPathfinding ||
                !settings.IsHuntingEnabled(preyType) ||
                hunterUnitId <= 0 ||
                preyUnitId <= 0 ||
                hunterGlobalId == 0 ||
                preyGlobalId == 0)
            {
                return false;
            }

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!APIShared.UnitAccess.TryGetById(unitApi, hunterUnitId, out GameUnit* hunter, out _) ||
                !APIShared.UnitAccess.TryGetById(unitApi, preyUnitId, out GameUnit* prey, out _) ||
                hunter == null ||
                prey == null ||
                hunter->r_GlobalId != hunterGlobalId ||
                prey->r_GlobalId != preyGlobalId ||
                hunter->r_AliveState != AliveState.IsAlive ||
                prey->r_AliveState != AliveState.IsAlive ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                prey->r_UnitChimp != preyType)
            {
                return false;
            }

            return TryInvokeNearVisibility(
                hunter,
                prey,
                out wrapperResult,
                out hunterToPreyResult,
                out preyToHunterResult,
                out geometry);
        }

        public void ResetForMap()
        {
            reentrancyLogged = false;
            failureLogged = false;
        }

        public void Dispose()
        {
            disposed = true;
        }

        /// <summary>
        /// Sight from a walkable tile to a prey, for choosing a place to shoot from, by the same rule as the
        /// near-target handoff: Vanilla's sight core must see in both directions, Hunter to prey and back.
        /// The viewer stands on the tile centre at the tile's height plus a Hunter's eye height
        /// (<see cref="LineOfSight.TryGetTileViewpoint"/>, <see cref="LineOfSight.ViewerEyeHeight"/>).
        /// Returns false when the call itself could not run; <paramref name="visible"/> then stays false.
        /// </summary>
        public bool TryEvaluateFromTile(int tileX, int tileY, GameUnit* prey, out bool visible)
        {
            visible = false;
            if (!IsAvailable || prey == null ||
                !LineOfSight.TryGetTileViewpoint(tileX, tileY, LineOfSight.ViewerEyeHeight, out SightPoint from) ||
                !LineOfSight.TryGetUnitPoint(in *prey, LineOfSight.TargetHeight, out SightPoint to))
            {
                return false;
            }

            if (!Evaluate(from, to, LineOfSightRoutine.Directional, "firing-position sight check", out int forward))
                return false;

            // The second direction only matters when the first one sees.
            if (forward <= 0)
                return true;

            if (!Evaluate(to, from, LineOfSightRoutine.Directional, "firing-position sight check", out int backward))
                return false;

            visible = backward > 0;
            return true;
        }

        private bool TryInvokeVisibility(GameUnit* hunter, GameUnit* prey, out int result)
        {
            result = 0;
            if (hunter == null || prey == null ||
                !LineOfSight.TryGetUnitPoint(in *hunter, LineOfSight.ViewerEyeHeight, out SightPoint from) ||
                !LineOfSight.TryGetUnitPoint(in *prey, LineOfSight.TargetHeight, out SightPoint to))
            {
                return false;
            }

            using (Shared.CrashBreadcrumbScope diagnostic =
                Shared.CrashBreadcrumbDiagnostics.Enter(
                    "HunterVisibilityNative",
                    hunter->r_CurrentTilePositionX,
                    hunter->r_CurrentTilePositionY,
                    prey->r_CurrentTilePositionX,
                    prey->r_CurrentTilePositionY))
            {
                if (!Evaluate(from, to, LineOfSightRoutine.Bidirectional, "native invocation", out result))
                    return false;

                diagnostic.Complete(result);
                return true;
            }
        }

        private bool TryInvokeNearVisibility(
            GameUnit* hunter,
            GameUnit* prey,
            out int wrapperResult,
            out int hunterToPreyResult,
            out int preyToHunterResult,
            out HunterNearVisibilityGeometry geometry)
        {
            wrapperResult = 0;
            hunterToPreyResult = -1;
            preyToHunterResult = -1;
            geometry = default;
            if (hunter == null || prey == null ||
                !LineOfSight.TryGetUnitPoint(in *hunter, LineOfSight.ViewerEyeHeight, out SightPoint hunterPoint) ||
                !LineOfSight.TryGetUnitPoint(in *prey, LineOfSight.TargetHeight, out SightPoint preyPoint))
            {
                return false;
            }

            using (Shared.CrashBreadcrumbScope diagnostic =
                Shared.CrashBreadcrumbDiagnostics.Enter(
                    "HunterNearVisibilityNative",
                    hunter->r_CurrentTilePositionX,
                    hunter->r_CurrentTilePositionY,
                    prey->r_CurrentTilePositionX,
                    prey->r_CurrentTilePositionY))
            {
                geometry = new HunterNearVisibilityGeometry(
                    hunter->r_CurrentTilePositionX,
                    hunter->r_CurrentTilePositionY,
                    prey->r_CurrentTilePositionX,
                    prey->r_CurrentTilePositionY,
                    hunterPoint.X,
                    hunterPoint.Y,
                    hunterPoint.Height,
                    preyPoint.X,
                    preyPoint.Y,
                    preyPoint.Height);

                if (!Evaluate(hunterPoint, preyPoint, LineOfSightRoutine.Bidirectional,
                        "near-visibility native invocation", out wrapperResult))
                {
                    return false;
                }

                // The validated wrapper calls both core directions before it
                // can return zero. Preserve those exact results without two
                // redundant native calls on the new 250-ms near-target path.
                if (wrapperResult == 0)
                {
                    hunterToPreyResult = 0;
                    preyToHunterResult = 0;
                    diagnostic.Complete(0);
                    return true;
                }

                if (!Evaluate(hunterPoint, preyPoint, LineOfSightRoutine.Directional,
                        "near-visibility native invocation", out hunterToPreyResult) ||
                    !Evaluate(preyPoint, hunterPoint, LineOfSightRoutine.Directional,
                        "near-visibility native invocation", out preyToHunterResult))
                {
                    return false;
                }

                diagnostic.Complete(wrapperResult);
                return true;
            }
        }

        private bool Evaluate(SightPoint from, SightPoint to, LineOfSightRoutine routine, string operation, out int result)
        {
            if (LineOfSight.TryEvaluate(from, to, routine, out result, out LineOfSightFailure failure))
                return true;

            LogFailureOnce(operation, failure);
            return false;
        }

        private void LogFailureOnce(string operation, LineOfSightFailure failure)
        {
            if (failure == LineOfSightFailure.Reentrant)
            {
                if (reentrancyLogged)
                    return;
                reentrancyLogged = true;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Improved Hunters native visibility skipped a reentrant {operation}; behavior remains unchanged.");
                return;
            }

            Shared.CrashBreadcrumbDiagnostics.Record("HunterVisibilityFailure", (int)failure, outcome: -1);
            if (failureLogged)
                return;
            failureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"Improved Hunters native visibility {operation} failed ({failure}); " +
                $"available={LineOfSight.IsAvailable}, Hunter behavior remains unchanged. {LineOfSight.UnavailableReason}");
        }
    }
}
