using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        [ThreadStatic] internal static bool nativeManualProbe;
        [ThreadStatic] private static Stack<ManualCommandContext> manualCommandContexts;
        private sealed class ManualCommandContext
        {
            internal MoveCommandScope Move;
            internal UnitMoveFrame Unit;
            internal PlanScope Plan, Pending;
        }

        internal void PushManualCommandContext()
        {
            if (manualCommandContexts == null) manualCommandContexts = new Stack<ManualCommandContext>();
            manualCommandContexts.Push(new ManualCommandContext {
                Move = activeMoveCommand, Unit = unitMoveFrame, Plan = activePlan, Pending = pendingPlan });
            unitMoveFrame = null;
        }

        internal void RestoreManualCommandContext()
        {
            ClearUnitMoveFrames();
            if (manualCommandContexts == null || manualCommandContexts.Count == 0)
            { activeMoveCommand = null; activePlan = pendingPlan = null; return; }
            ManualCommandContext previous = manualCommandContexts.Pop();
            activeMoveCommand = previous.Move; unitMoveFrame = previous.Unit;
            activePlan = previous.Plan; pendingPlan = previous.Pending;
        }

        private Action CaptureTargetCommandContext()
        {
            AttackCommandScope attack = activeAttackCommand;
            DirectFillCommandScope fill = activeDirectFillCommand;
            PlanScope plan = activePlan, pending = pendingPlan;
            UnitMoveFrame unit = unitMoveFrame;
            AttackApproachDiagnosticScope diagnostic = activeAttackApproachDiagnostic;
            BuildingApproachPerformanceScope approach = activeBuildingApproachPerformance;
            BuildingConsumerPerformanceScope consumer = activeBuildingConsumerPerformance;
            unitMoveFrame = null;
            return () => {
                ClearUnitMoveFrames();
                activeAttackCommand = attack; activeDirectFillCommand = fill;
                activePlan = plan; pendingPlan = pending; unitMoveFrame = unit;
                activeAttackApproachDiagnostic = diagnostic;
                activeBuildingApproachPerformance = approach; activeBuildingConsumerPerformance = consumer;
            };
        }

        // Complete FBCB9319 scratch closure of 18E1E0/E7C40/DF720/F4930:
        // context through its last 320800-entry queue, contiguous distance/stamp
        // grids, and F4930's rectangle helper. Cursor queries restore generations
        // together with their grids, so they cannot alter simulation state.
        internal const int NativeProbeManagerBytes = 0x3C886C;
        internal const int NativeProbeGridBytes = 0x139480;
        internal byte* nativeProbeGrid, nativeProbeRectangle;
        private readonly byte[] nativeProbeManagerBackup = new byte[NativeProbeManagerBytes];
        private readonly byte[] nativeProbeGridBackup = new byte[NativeProbeGridBytes];
        private readonly byte[] nativeProbeRectangleBackup = new byte[0x20];
        private readonly byte[] nativeProbeMemoryHelperBackup = new byte[0x18];
        // A probe batch covers consecutive probes with only managed code in between. Every probe
        // restores the exact scratch closure, so the closure captured before the batch's first
        // probe is still byte-identical before each later one and need not be copied again.
        // Never open a batch around native calls that may themselves change the closure.
        private int nativeProbeBatchDepth;
        private bool nativeProbeBatchSnapshot;
        internal void BeginNativeProbeBatch() => nativeProbeBatchDepth++;
        internal void EndNativeProbeBatch()
        {
            if (nativeProbeBatchDepth > 0 && --nativeProbeBatchDepth == 0)
                nativeProbeBatchSnapshot = false;
        }

        internal bool ProbeNativeManualPath(int unitId, int x, int y)
        {
            if (nativeManualProbe || (uint)x >= MapWidth || (uint)y >= MapWidth ||
                !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                nativePathManager == IntPtr.Zero || nativeProbeGrid == null ||
                nativeProbeRectangle == null || originalCentralMovementPlan == null)
                return false;
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
#if SERP_DEVDIAG
            var probeCapture = ManualProbeCapture.BeginProbe(FreezeProbeContext(unit, unitId, x, y), nativeProbeBatchSnapshot);
            try
            {
#endif
            byte* savedUnit = stackalloc byte[sizeof(GameUnit)];
            byte* manager = (byte*)nativePathManager;
            Buffer.MemoryCopy(unit, savedUnit, sizeof(GameUnit), sizeof(GameUnit));
#if SERP_DEVDIAG
            ManualProbeCapture.Copied(ref probeCapture, false, 0, 0, sizeof(GameUnit));
#endif
            if (!nativeProbeBatchSnapshot)
            {
                Marshal.Copy(nativePathManager, nativeProbeManagerBackup, 0, NativeProbeManagerBytes);
#if SERP_DEVDIAG
                ManualProbeCapture.Copied(ref probeCapture, false, NativeProbeManagerBytes, 0, 0);
#endif
                Marshal.Copy((IntPtr)nativeProbeGrid, nativeProbeGridBackup, 0, NativeProbeGridBytes);
#if SERP_DEVDIAG
                ManualProbeCapture.Copied(ref probeCapture, false, 0, NativeProbeGridBytes, 0);
#endif
                Marshal.Copy((IntPtr)nativeProbeRectangle, nativeProbeRectangleBackup, 0, 0x20);
#if SERP_DEVDIAG
                ManualProbeCapture.Copied(ref probeCapture, false, 0, 0, 0x20);
#endif
                Marshal.Copy(nativePathManager - 0x18, nativeProbeMemoryHelperBackup, 0, 0x18);
#if SERP_DEVDIAG
                ManualProbeCapture.Copied(ref probeCapture, false, 0, 0, 0x18);
                probeCapture.SnapshotCompleted = true;
#endif
                nativeProbeBatchSnapshot = nativeProbeBatchDepth > 0;
            }
            else Shared.DevDiag.Count("manual.probe.snapshotReused");
#if SERP_DEVDIAG
            ManualProbeCapture.Mark(ref probeCapture, ManualProbeCapture.Administration);
#endif
            bool previousProbe = nativeManualProbe;
            MoveCommandScope savedMove = activeMoveCommand;
            AttackCommandScope savedAttack = activeAttackCommand;
            DirectFillCommandScope savedFill = activeDirectFillCommand;
            MoatWorkSelectionScope savedWork = activeMoatWorkSelection;
            PlanScope savedPlan = activePlan, savedPending = pendingPlan;
            UnitMoveFrame savedFrame = unitMoveFrame;
            AttackApproachDiagnosticScope savedDiagnostic = activeAttackApproachDiagnostic;
            BuildingApproachPerformanceScope savedApproach = activeBuildingApproachPerformance;
            BuildingConsumerPerformanceScope savedConsumer = activeBuildingConsumerPerformance;
            nativeManualProbe = true;
            activeMoveCommand = null; activeAttackCommand = null; activeDirectFillCommand = null;
            activeMoatWorkSelection = null; activePlan = pendingPlan = null; unitMoveFrame = null;
            activeAttackApproachDiagnostic = null;
            activeBuildingApproachPerformance = null; activeBuildingConsumerPerformance = null;
            try
            {
                // Never inherit a parent's moat/Assassin mode into a different unit.
                *(int*)(manager + 0x80) = 0;
                *(int*)(manager + 0x84) = 0;
                *(int*)(manager + 0x88) = 0;
#if SERP_DEVDIAG
                ManualProbeCapture.Mark(ref probeCapture, ManualProbeCapture.Native);
                int probeResult = originalCentralMovementPlan((IntPtr)nativeUnitManager, unitId, x, y);
                ManualProbeCapture.Result(ref probeCapture, probeResult);
                ManualProbeCapture.Mark(ref probeCapture, ManualProbeCapture.Administration);
                return probeResult > 0;
#else
                return originalCentralMovementPlan((IntPtr)nativeUnitManager, unitId, x, y) > 0;
#endif
            }
            finally
            {
#if SERP_DEVDIAG
                ManualProbeCapture.Mark(ref probeCapture, ManualProbeCapture.Restore);
#endif
                Buffer.MemoryCopy(savedUnit, unit, sizeof(GameUnit), sizeof(GameUnit));
#if SERP_DEVDIAG
                ManualProbeCapture.Copied(ref probeCapture, true, 0, 0, sizeof(GameUnit));
#endif
                Marshal.Copy(nativeProbeManagerBackup, 0, nativePathManager, NativeProbeManagerBytes);
#if SERP_DEVDIAG
                ManualProbeCapture.Copied(ref probeCapture, true, NativeProbeManagerBytes, 0, 0);
#endif
                Marshal.Copy(nativeProbeGridBackup, 0, (IntPtr)nativeProbeGrid, NativeProbeGridBytes);
#if SERP_DEVDIAG
                ManualProbeCapture.Copied(ref probeCapture, true, 0, NativeProbeGridBytes, 0);
#endif
                Marshal.Copy(nativeProbeRectangleBackup, 0, (IntPtr)nativeProbeRectangle, 0x20);
#if SERP_DEVDIAG
                ManualProbeCapture.Copied(ref probeCapture, true, 0, 0, 0x20);
#endif
                Marshal.Copy(nativeProbeMemoryHelperBackup, 0, nativePathManager - 0x18, 0x18);
#if SERP_DEVDIAG
                ManualProbeCapture.Copied(ref probeCapture, true, 0, 0, 0x18);
                probeCapture.RestoreCompleted = true;
                ManualProbeCapture.Mark(ref probeCapture, ManualProbeCapture.Administration);
#endif
                activeMoveCommand = savedMove; activeAttackCommand = savedAttack;
                activeDirectFillCommand = savedFill; activeMoatWorkSelection = savedWork;
                activePlan = savedPlan; pendingPlan = savedPending; unitMoveFrame = savedFrame;
                activeAttackApproachDiagnostic = savedDiagnostic;
                activeBuildingApproachPerformance = savedApproach; activeBuildingConsumerPerformance = savedConsumer;
                nativeManualProbe = previousProbe;
                Shared.DevDiag.Stop(ref measured, "manual.probe");
            }
#if SERP_DEVDIAG
            }
            finally { ManualProbeCapture.EndProbe(ref probeCapture); }
#endif
        }

        private readonly Dictionary<NativeCursorProbeKey, bool> nativeCursorAnswers = new Dictionary<NativeCursorProbeKey, bool>();
        private int nativeCursorAnswerTick = int.MinValue;
        private readonly struct NativeCursorProbeKey : IEquatable<NativeCursorProbeKey>
        {
            private readonly int unit, x, y, epoch;
            private readonly long revision;
            private readonly ulong unitState, access;
            internal NativeCursorProbeKey(int unit, int x, int y, int epoch, long revision, ulong state, ulong access)
            { this.unit = unit; this.x = x; this.y = y; this.epoch = epoch; this.revision = revision; unitState = state; this.access = access; }
            public bool Equals(NativeCursorProbeKey other) => unit == other.unit && x == other.x && y == other.y &&
                epoch == other.epoch && revision == other.revision && unitState == other.unitState && access == other.access;
            public override bool Equals(object obj) => obj is NativeCursorProbeKey other && Equals(other);
            public override int GetHashCode() => unchecked(unit * 397 ^ x * 31 ^ y ^ (int)unitState ^ (int)revision ^ epoch ^ (int)access);
        }
        private bool ProbeNativeCursorPath(int id, int x, int y)
        {
            if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null) return false;
            int tick = CaptureCurrentGameTick();
            if (nativeCursorAnswerTick != tick || nativeCursorAnswers.Count >= 4096)
            {
#if SERP_DEVDIAG
                ManualProbeCapture.Invalidate(nativeCursorAnswerTick != tick, nativeCursorAnswers.Count >= 4096, false);
#endif
                nativeCursorAnswers.Clear(); nativeCursorAnswerTick = tick;
            }
            // Cache-key fingerprint of the complete unit record, mixed eight bytes at a time.
            ulong state = 14695981039346656037UL;
            ulong* words = (ulong*)unit;
            int wordCount = sizeof(GameUnit) / sizeof(ulong);
            for (int i = 0; i < wordCount; i++) state = unchecked((state ^ words[i]) * 1099511628211UL);
            for (int i = wordCount * sizeof(ulong); i < sizeof(GameUnit); i++)
                state = unchecked((state ^ ((byte*)unit)[i]) * 1099511628211UL);
            var key = new NativeCursorProbeKey(id, x, y, mapEpoch, placementRevision, state, cursorDiplomacy);
            if (nativeCursorAnswers.TryGetValue(key, out bool result))
            {
#if SERP_DEVDIAG
                ManualProbeCapture.Cache(true, FreezeProbeContext(unit, id, x, y), result);
#endif
                return result;
            }
#if SERP_DEVDIAG
            ManualProbeCapture.Cache(false, FreezeProbeContext(unit, id, x, y));
#endif
            result = ProbeCursorWithRegionReject(id, unit, x, y, tick);
            nativeCursorAnswers[key] = result;
            return result;
        }

        internal bool TryQualifyNativeMoatStart(PlanScope plan, out RouteProbeSummary summary)
        {
#if SERP_DEVDIAG
            using var probeOperation = ManualProbeCapture.BeginOperation(ManualProbeCapture.MoatStart);
#endif
            summary = default;
            if (nativeManualProbe || !ManualCommandsEnabled || plan == null ||
                !APIShared.UnitAccess.TryGetById(plan.UnitId, out GameUnit* unit, out _) || unit == null ||
                GamePlayerManagerAPI.Instance.IsAIPlayer(unit->r_ControllableForPlayerId) ||
                !IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId)) ||
                !ProbeNativeManualPath(plan.UnitId, plan.TargetX, plan.TargetY)) return false;
            plan.PlayerId = unit->r_ControllableForPlayerId;
            plan.UnitGlobalId = unit->r_GlobalId; plan.IdentityBound = true;
            summary = new RouteProbeSummary(plan.PlayerId) {
                RouteFound = true, AttackProbeEvaluated = true, ReachedWithMoat = true,
                StartRegion = pathRegionGrid[unit->r_CurrentPositionTileId],
                TargetRegion = pathRegionGrid[GameTileManagerAPI.Instance.GetTileId(plan.TargetX, plan.TargetY)] };
            return true;
        }

        internal bool ProbeNativeCursorConnectivity(int player, int start, int target, out RouteProbeSummary summary)
        {
#if SERP_DEVDIAG
            using var probeOperation = ManualProbeCapture.BeginOperation(ManualProbeCapture.BuildingCursor);
#endif
            summary = new RouteProbeSummary(player);
            if (!ManualCommandsEnabled || !IsValidTileId(start) || !IsValidTileId(target) ||
                GamePlayerManagerAPI.Instance.IsAIPlayer(player)) return false;
            // The exact unit binding is supplied by TryQualifyNativeSelection. Other
            // native callers retain their own result instead of a synthetic PCL answer.
            BuildingCursorConnectivityScope binding = activeBuildingCursorConnectivity;
            if (binding == null || binding.PlayerId != player || binding.StartTile != start) return false;
            var destination = GameTileManagerAPI.Instance.GetTileVectorFromId(target);
            bool reachable = ProbeNativeCursorPath(binding.UnitId, destination.X, destination.Y);
            summary.AttackProbeEvaluated = true; summary.RouteFound = summary.ReachedWithoutMoat = reachable;
            return true;
        }

        internal bool TryQualifyNativeSelection(AttackCursorPairScope template, int[] ids, string token,
            out AttackCursorPairScope bound, out CursorGroupRouteSummary group)
        {
#if SERP_DEVDIAG
            using var probeOperation = ManualProbeCapture.BeginOperation(
                template?.FallbackKind == CursorPairFallbackKind.BuildingApproach ? ManualProbeCapture.BuildingCursor : ManualProbeCapture.TileCursor,
                template?.BuildingId ?? 0);
#endif
            bound = null; group = default; group.SelectionSignature = token;
            if (!ManualCommandsEnabled || GamePlayerManagerAPI.Instance.IsAIPlayer(template.PlayerId)) return false;
            // Tile probes run back to back with only managed reads between them. The building
            // branch runs native B70C0 between its probes and therefore keeps per-probe copies.
            bool batch = template.FallbackKind != CursorPairFallbackKind.BuildingApproach;
            using var cursorRejectScope = BeginCursorRejectBatch(batch, ids.Length);
            if (batch) BeginNativeProbeBatch();
            try
            {
            foreach (int id in ids)
            {
                if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null ||
                    !APIShared.UnitAccess.IsReallyAlive(unit) || unit->r_ControllableForPlayerId != template.PlayerId) continue;
                group.SelectedUnits++;
                if (CanDigMoat(unit)) group.DiggerUnits++;
                var source = new SelectedCursorUnitSnapshot(id, unit->r_CurrentTilePositionX,
                    unit->r_CurrentTilePositionY, unchecked((int)unit->r_CurrentPositionTileId), CanDigMoat(unit));
                AttackCursorPairScope probe = CreateCursorScopeForSnapshot(template, source);
                bool reachable;
                if (template.FallbackKind == CursorPairFallbackKind.BuildingApproach)
                    reachable = originalBuildingCursorReachability != null &&
                        CallBuildingCursorWithRegions((IntPtr)GameBuildingManagerAPI.Instance.GetBuildingManager().Pointer, template.BuildingId, id) != 0;
                else
                    reachable = ProbeNativeCursorPath(id, template.TargetX, template.TargetY);
                if (!reachable) continue;
                group.LegallyReachableUnits++;
                if (bound == null)
                {
                    bound = probe; group.RepresentativeUnitId = id;
                    group.RepresentativeStartX = source.StartX; group.RepresentativeStartY = source.StartY;
                    group.RepresentativeStartTileId = source.StartTileId; group.RepresentativeCanDig = source.CanDig;
                }
                // Cursor permission needs one witness. Native dispatch subsequently
                // visits every member; UI probes never seed its route ownership.
                break;
            }
            }
            finally { if (batch) EndNativeProbeBatch(); }
            group.AllowFallback = bound != null;
            if (bound != null) { bound.GroupCursorAuthorized = true; bound.GroupSelectionSignature = token; }
            return group.SelectedUnits > 0;
        }

        internal void PrepareNativeManualGroup(TribeIssueOrderMoveHereEventArgs args)
        {
#if SERP_DEVDIAG
            using var probeOperation = ManualProbeCapture.BeginOperation(ManualProbeCapture.GroupFallback);
#endif
            if (!ManualCommandsEnabled || args.SkipOriginalFunction ||
                activeMoveCommand == null ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(args.TribeId, out GameTribe* tribe) || tribe == null ||
                GamePlayerManagerAPI.Instance.IsAIPlayer(tribe->r_PlayerIdOwner)) return;
            EnsureMoveCommandGroupSummary(activeMoveCommand);
            if (activeMoveCommand.UnitsOnMoatAtDispatch == 0 ||
                (uint)args.TileX >= MapWidth || (uint)args.TileY >= MapWidth) return;
            if (TraversalEnabled)
            {
                // Additional moat entry remains digger-only. A non-digger already
                // standing in a moat needs the independent native command path,
                // even while the traversal provider is active. Other addon groups
                // retain their existing precise/fast group routing.
                bool nativeOnlyMoatStarter = false;
                foreach (int unitId in activeMoveCommand.ActiveUnitIdsAtDispatch)
                    if (APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) &&
                        unit != null && APIShared.UnitAccess.IsReallyAlive(unit) &&
                        unit->r_ControllableForPlayerId == tribe->r_PlayerIdOwner &&
                        IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId)) &&
                        !CanDigMoat(unit))
                    {
                        nativeOnlyMoatStarter = true;
                        break;
                    }
                if (!nativeOnlyMoatStarter) return;
            }
            // Consecutive probes with only managed code in between share one closure snapshot.
            BeginNativeProbeBatch();
            try
            {
                foreach (int unitId in activeMoveCommand.ActiveUnitIdsAtDispatch)
                    if (ProbeNativeManualPath(unitId, args.TileX, args.TileY))
                    {
                        activeMoveCommand.NativeCommonFallback = true;
                        return;
                    }
            }
            finally { EndNativeProbeBatch(); }
        }
        internal bool IsNativeManualGroupFlood(IntPtr manager, int player, int targetRegion, int x, int y)
        {
            MoveCommandScope command = activeMoveCommand;
            if (nativeManualProbe || command?.NativeCommonFallback != true || manager != nativePathManager ||
                unitMoveFrame != null ||
                (activePlan != null && (manualCommandContexts == null || manualCommandContexts.Count == 0 ||
                    !ReferenceEquals(activePlan, manualCommandContexts.Peek().Plan))) ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(command.TribeId, out GameTribe* tribe) || tribe == null ||
                tribe->r_PlayerIdOwner != player ||
                !APIShared.UnitAccess.TryGetById(tribe->r_LeaderUnitId, out GameUnit* leader, out _) || leader == null)
                return false;
            int target = GameTileManagerAPI.Instance.GetTileId(command.TargetX, command.TargetY);
            return IsValidTileId(target) && targetRegion == pathRegionGrid[target] &&
                leader->r_CurrentTilePositionX == x && leader->r_CurrentTilePositionY == y;
        }
    }
}
