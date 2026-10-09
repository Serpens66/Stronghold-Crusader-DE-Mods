using BepInEx.Logging;
using APIShared;
using RedBird.Backends.NativeX64;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime : IDisposable
    {
        internal void ObserveRuntimeCadenceShadow(
            int tick, GameUnit* unit, MoatMoveTracker tracker)
        {
            if (!TryCaptureWeightedMovementCostProfile(
                    unit, out WeightedMovementCostProfile runtimeProfile,
                    out string rejectionReason))
            {
                string rejection = rejectionReason ?? "runtime-cadence-unavailable";
                if (!string.Equals(
                        tracker.LastRuntimeCadenceRejection, rejection,
                        StringComparison.Ordinal))
                {
                    tracker.LastRuntimeCadenceRejection = rejection;
                    LogDetailedInfo(
                        $"Bugfixes and QoL stage=friendly-moat-movement-weighted-shadow-runtime unit={tracker.UnitId} " +
                        $"tick={tick} decision=no-valid-shadow-route reason={rejection}.");
                }
                return;
            }

            if (tracker.RuntimeCadenceCaptured)
            {
                if (tracker.RuntimeCostProfile.HasSameNormalizedCadence(runtimeProfile))
                    return;

                // Some handlers initialize SpeedBonus only after path publication. If this
                // happens before the first tile transition, the new profile describes the
                // entire measurable route more accurately and can safely replace the first one.
                if (tracker.TileTransitionCount == 0 && !tracker.RuntimeCadenceChanged)
                {
                    WeightedMovementCostProfile previousProfile = tracker.RuntimeCostProfile;
                    tracker.RuntimeCostProfile = runtimeProfile;
                    tracker.RuntimeCadenceRebased = true;
                    LogDetailedInfo(
                        $"Bugfixes and QoL stage=friendly-moat-movement-weighted-shadow-runtime-rebase unit={tracker.UnitId} " +
                        $"tick={tick} previous={FormatCostProfile(previousProfile)} " +
                        $"current={FormatCostProfile(runtimeProfile)}.");
                    CalculateAndLogRuntimeCadenceShadow(tick, tracker, runtimeProfile, "rebase");
                    return;
                }

                if (tracker.TileTransitionCount <= 1 && !tracker.RuntimeCadenceChanged &&
                    tracker.RuntimeCostProfile.SpeedBonus == 0 &&
                    runtimeProfile.SpeedBonus > 0 &&
                    tracker.RuntimeCostProfile.HasSameBaseCadenceExceptSpeedBonus(runtimeProfile))
                {
                    WeightedMovementCostProfile previousProfile = tracker.RuntimeCostProfile;
                    tracker.RuntimeCostProfile = runtimeProfile;
                    tracker.RuntimeCadenceRebased = true;
                    LogDetailedInfo(
                        $"Bugfixes and QoL stage=friendly-moat-movement-weighted-shadow-runtime-rebase unit={tracker.UnitId} " +
                        $"tick={tick} kind=late-handler-speed-bonus " +
                        $"previous={FormatCostProfile(previousProfile)} " +
                        $"current={FormatCostProfile(runtimeProfile)}.");
                    CalculateAndLogRuntimeCadenceShadow(
                        tick, tracker, runtimeProfile, "late-handler-speed-bonus");
                    return;
                }

                if (!tracker.RuntimeCadenceChanged)
                {
                    tracker.RuntimeCadenceChanged = true;
                    tracker.Calibratable = false;
                    tracker.CalibrationReason = "runtime-cadence-changed-after-first-transition";
                    LogDetailedInfo(
                        $"Bugfixes and QoL stage=friendly-moat-movement-weighted-shadow-runtime-change unit={tracker.UnitId} " +
                        $"tick={tick} first={FormatCostProfile(tracker.RuntimeCostProfile)} " +
                        $"current={FormatCostProfile(runtimeProfile)}.");
                }
                return;
            }

            tracker.RuntimeCadenceCaptured = true;
            tracker.RuntimeCostProfile = runtimeProfile;
            tracker.LastRuntimeCadenceRejection = null;

            CalculateAndLogRuntimeCadenceShadow(tick, tracker, runtimeProfile, "initial");
        }

        internal void CalculateAndLogRuntimeCadenceShadow(
            int tick,
            MoatMoveTracker tracker,
            WeightedMovementCostProfile runtimeProfile,
            string captureKind)
        {
            tracker.LastRuntimeCadenceRejection = null;

            bool found = false;
            WeightedMoatRouteSummary runtimeCandidate = default;
            if (!weightedShadowBusy)
            {
                weightedShadowBusy = true;
                try
                {
                    found = weightedMoatRoutePlanner.TryBuild(
                        tracker.WeightedPlayerId,
                        tracker.InitialX,
                        tracker.InitialY,
                        tracker.TargetX,
                        tracker.TargetY,
                        runtimeProfile,
                        tracker.AllowReservedTarget,
                        out runtimeCandidate);
                }
                finally
                {
                    weightedShadowBusy = false;
                }
            }

            long runtimeNativeTicks = tracker.NativeRouteValid
                ? runtimeProfile.EstimateRouteTicks(
                    tracker.NativeRouteSummary.GroundEdges,
                    tracker.NativeRouteSummary.MoatEdges)
                : long.MaxValue;
            string decision;
            string reason;
            if (!found)
            {
                decision = "no-valid-shadow-route";
                reason = runtimeCandidate.Reason;
            }
            else if (!tracker.NativeRouteValid || runtimeNativeTicks == long.MaxValue)
            {
                decision = "no-valid-shadow-route";
                reason = "native-route-unavailable-for-runtime-cadence";
            }
            else if (runtimeCandidate.MoatEdges > 0 &&
                runtimeCandidate.EstimatedTicks < runtimeNativeTicks)
            {
                decision = "shadow-friendly-moat";
                reason = "runtime-cadence-shadow-faster";
            }
            else
            {
                decision = tracker.NativeRouteSummary.MoatEdges > 0
                    ? "native-friendly-moat"
                    : "native-ground";
                reason = runtimeCandidate.MoatEdges == 0
                    ? "runtime-cadence-ground-winner"
                    : "runtime-cadence-native-not-slower";
            }

            tracker.RuntimeNativeEstimatedTicks = runtimeNativeTicks;
            tracker.RuntimeShadowEstimatedTicks = tracker.WeightedPathPublished
                ? runtimeProfile.EstimateRouteTicks(
                    tracker.PublishedRouteSummary.GroundEdges,
                    tracker.PublishedRouteSummary.MoatEdges)
                : found ? runtimeCandidate.EstimatedTicks : long.MaxValue;
            tracker.RuntimeShadowDecision = decision;
            tracker.RuntimeShadowMatchesPublishedCostProfile =
                (tracker.WeightedPathPublished && tracker.PublishedRouteSummary.Found) ||
                (found && tracker.NativeRouteValid &&
                 tracker.NativeRouteSummary.RouteLength == runtimeCandidate.RouteLength &&
                 tracker.NativeRouteSummary.GroundEdges == runtimeCandidate.GroundEdges &&
                 tracker.NativeRouteSummary.MoatEdges == runtimeCandidate.MoatEdges);

            long saving = found && runtimeNativeTicks != long.MaxValue
                ? runtimeNativeTicks - runtimeCandidate.EstimatedTicks
                : 0;
            LogDetailedInfo(
                $"Bugfixes and QoL stage=friendly-moat-movement-weighted-shadow-runtime unit={tracker.UnitId} " +
                $"type={tracker.WeightedUnitType} player={tracker.WeightedPlayerId} " +
                $"command={tracker.WeightedCommand}({(uint)tracker.WeightedCommand}) tick={tick} " +
                $"start=({tracker.InitialX},{tracker.InitialY}) " +
                $"target=({tracker.TargetX},{tracker.TargetY}) " +
                $"capture={captureKind} " +
                $"planning={FormatCostProfile(tracker.PlanningCostProfile)} " +
                $"runtime={FormatCostProfile(runtimeProfile)} " +
                $"decision={decision} reason={reason} " +
                $"nativeLength={tracker.NativeRouteSummary.RouteLength} " +
                $"nativeGround={tracker.NativeRouteSummary.GroundEdges} " +
                $"nativeMoat={tracker.NativeRouteSummary.MoatEdges} " +
                $"nativeDiagonal={tracker.NativeRouteSummary.DiagonalEdges} " +
                $"nativeDirectionChanges={tracker.NativeRouteSummary.DirectionChanges} " +
                $"nativeFingerprint=0x{tracker.NativeRouteSummary.RouteFingerprint:X16} " +
                $"nativeTicks={(runtimeNativeTicks == long.MaxValue ? "n/a" : runtimeNativeTicks.ToString())} " +
                $"shadowFound={found} shadowLength={runtimeCandidate.RouteLength} " +
                $"shadowGround={runtimeCandidate.GroundEdges} " +
                $"shadowMoat={runtimeCandidate.MoatEdges} " +
                $"shadowDiagonal={runtimeCandidate.DiagonalEdges} " +
                $"shadowDirectionChanges={runtimeCandidate.DirectionChanges} " +
                $"shadowFingerprint=0x{runtimeCandidate.RouteFingerprint:X16} " +
                $"shadowTicks={(found ? runtimeCandidate.EstimatedTicks.ToString() : "n/a")} " +
                $"publishedWeighted={tracker.WeightedPathPublished} " +
                $"publishedTicks={(tracker.RuntimeShadowEstimatedTicks == long.MaxValue ? "n/a" : tracker.RuntimeShadowEstimatedTicks.ToString())} " +
                $"savingTicks={saving} searchMs={runtimeCandidate.SearchMilliseconds:F3} " +
                $"expanded={runtimeCandidate.ExpandedNodes}.");
        }

        internal static string FormatCostProfile(WeightedMovementCostProfile profile) =>
            $"speed={profile.CurrentSpeed}/effective={profile.CurrentSpeed2}/" +
            $"bonus={profile.SpeedBonus}/additionalSubsteps={profile.AdditionalSubsteps}/" +
            $"extraDelay={profile.ExtraDelay}/phase={profile.MoatPhase}/" +
            $"terrainPenalty={profile.CurrentTerrainPenalty}/" +
            $"normalized={profile.NormalizedDelay}/progress={profile.CadenceProgress}";

        internal static int EncodeDirectionDelta(int dx, int dy)
        {
            if (dx == 0 && dy == -1) return 0;
            if (dx == 1 && dy == -1) return 1;
            if (dx == 1 && dy == 0) return 2;
            if (dx == 1 && dy == 1) return 3;
            if (dx == 0 && dy == 1) return 4;
            if (dx == -1 && dy == 1) return 5;
            if (dx == -1 && dy == 0) return 6;
            if (dx == -1 && dy == -1) return 7;
            return -1;
        }

    }
}
