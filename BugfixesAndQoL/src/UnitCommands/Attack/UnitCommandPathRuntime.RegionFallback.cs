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
        internal int ObserveScopedRegionPairReachability(
            IntPtr pathManager,
            int movementClass,
            int sourceRegion,
            int targetRegion,
            int routeKind)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            int result = ObserveScopedRegionPairReachabilityCore(
                pathManager, movementClass, sourceRegion, targetRegion, routeKind,
                out int vanillaResult);
            Shared.DevDiag.StopSpike(ref measured, "uc.regionPair", 20000, () =>
                $"player={movementClass} regions={sourceRegion}->{targetRegion} routeKind={routeKind} " +
                $"vanilla={vanillaResult} effective={result} ladderProbe={activeLadderAttackProbe != null} " +
                $"moatWork={(activeMoatWorkSelection != null ? activeMoatWorkSelection.RelationshipMode.ToString() : "none")} " +
                $"attackScope={activeAttackApproachDiagnostic != null} directFill={activeDirectFillCommand != null} " +
                $"directCursor={activeDirectCursorMove != null} moveCommand={activeMoveCommand != null}");
            EnemyBridgeDiagnosticBridge.ObserveRegion(movementClass, sourceRegion, targetRegion, routeKind, vanillaResult, result);
            // The already-owned E2610 detour can report its result to the optional
            // test policy. Without that policy, the existing result is unchanged.
            try
            {
                IEnemyGatePathPolicy policy = EnemyGatePathPolicyBridge.Current;
                if (policy != null && policy.HasPublishedMask &&
                    policy is IEnemyGateRegionPairObserver observer &&
                    movementClass > 0 && movementClass <= 8 &&
                    pathManager == nativePathManager)
                {
                    string source = activeDirectFillCommand != null ? "direct-fill" :
                        activeAttackApproachDiagnostic != null ? "attack-approach" :
                        activeMoveCommand != null ? "tribe-move" : "other";
                    observer.ObserveRegionPair(movementClass, sourceRegion,
                        targetRegion, routeKind, vanillaResult, result, source);
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("enemy-gate-region-observer", ex);
            }
            return result;
        }

        internal int ObserveScopedRegionPairReachabilityCore(
            IntPtr pathManager,
            int movementClass,
            int sourceRegion,
            int targetRegion,
            int routeKind,
            out int vanillaResult)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            vanillaResult = originalRegionPairReachability(
                pathManager, movementClass, sourceRegion, targetRegion, routeKind);
            Shared.DevDiag.StopSpike(ref measured, "uc.vanilla.regionPair", 20000);
            if (TryHandleVanillaLadderRegionPair(
                    pathManager,
                    movementClass,
                    sourceRegion,
                    targetRegion,
                    routeKind,
                    vanillaResult,
                    out int ladderResult))
            {
                return ladderResult;
            }
            // E2610 uses argument 2 as player ID. Keep the established delegate ABI/name, but
            // pass the value to the work selector according to the confirmed native semantics.
            if (TryAllowDigWorkRegionPair(
                    pathManager, movementClass, sourceRegion, targetRegion, routeKind,
                    vanillaResult))
            {
                return 1;
            }
            if (TryAllowDirectCursorMoveRegionPair(
                    pathManager, movementClass, sourceRegion, targetRegion, vanillaResult) ||
                TryAllowDirectFillRegionPair(
                    pathManager, movementClass, sourceRegion, targetRegion, vanillaResult))
            {
                return 1;
            }
            MoveCommandScope moveCommand = activeMoveCommand;
            if (moveCommand != null && pathManager == nativePathManager)
            {
                moveCommand.RegionCalls++;
#if SERP_DEVDIAG
                // Runs for every native E2610 query of a command: diagnostic build only.
                try
                {
                    string signature = $"E2610-observed:{movementClass}:{sourceRegion}:" +
                        $"{targetRegion}:{routeKind}:{vanillaResult}";
                    if (moveCommand.EarlyRegionLogSignatures.Add(signature))
                    {
                        LogCommandDiagnostic(
                            $"stage=movehere-region-pair-observed " +
                            $"commandSeq={moveCommand.Sequence} tribe={moveCommand.TribeId} " +
                            $"player={movementClass} regions={sourceRegion}->{targetRegion} " +
                            $"routeKind={routeKind} vanilla={vanillaResult} " +
                            $"effective={vanillaResult} " +
                            "decision=vanilla-graph-state-preserved");
                    }
                }
                catch
                {
                    // Observation must never alter the native reachability result.
                }
#endif
            }
            // Attack scopes exist only with the friendly-moat provider (MoatMove).
            AttackApproachDiagnosticScope scope = activeAttackApproachDiagnostic;
            if (scope == null || disposed || !TraversalEnabled)
                return vanillaResult;
            return Traversal.ResolveAttackScopeRegionPairTraversal(
                scope, movementClass, sourceRegion, targetRegion, routeKind, vanillaResult);
        }

    }
}
