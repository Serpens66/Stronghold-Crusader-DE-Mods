using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SHCDESE.API;
using RedBird.X64.Hooks.Transaction;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void FormationSlotDelegate(IntPtr manager, int spacing, int x, int y);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AssassinGroundFormationSlotDelegate(
            IntPtr manager, int spacing, int x, int y);
        private FormationSlotDelegate originalFormationSlot;
        private AssassinGroundFormationSlotDelegate originalAssassinGroundFormationSlot;
        private RedBirdDetour<FormationSlotDelegate> formationSlotDetour;
        private RedBirdDetour<AssassinGroundFormationSlotDelegate> assassinGroundFormationSlotDetour;
        private MoveCommandScope formationOwner;
        private int formationEpoch, formationTick, formationStamp, formationPlayer, formationSpacing;
        private long formationRevision;
        private bool formationExhausted;
        private long formationRejected, formationReplaced, formationFallbacks;
        private MoveFormationPreviewPlanner managedFormationPlanner;
        private readonly List<MoveFormationDestination> managedFormationDestinations =
            new List<MoveFormationDestination>();
        private MoveCommandScope managedFormationOwner;
        private MoveCommandScope managedFormationFailedOwner;
        private MoveFormationSelector managedFormationSelector;
        private MoveFormationPlanMetrics managedFormationMetrics;
        private int managedFormationCursor;
        private int managedFormationRequired;

        private sealed class OriginalFormationSlotException : Exception
        {
            internal OriginalFormationSlotException(Exception innerException)
                : base("The native formation selector delegate failed.", innerException) { }
        }

        private void InstallFormationSlotAdapter(
            HookTransaction transaction, ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            // FBCB9319 E1D30..E1D3F: three complete nonvolatile-register saves.
            // No patch of the search field or its terrain/visit stamps is needed.
            formationSlotDetour = InstallConnectivityObserver(transaction, memory, libraryBase, 0xE1D30,
                "48 89 5C 24 08 48 89 6C 24 18 48 89 74 24 20 89 54 24 10 57 41 54 41 55 41 56 41 57 4C 63 1D E1 49 BE 07 4C 8B F1 48 63",
                (FormationSlotDelegate)ChooseOwnerSafeFormationSlot);
            // FBCB9319 E0970 handles only the pure-Assassin ground candidate list.
            // Its sibling E0AC0 retains Vanilla spacing 1 and all structure checks.
            assassinGroundFormationSlotDetour = InstallConnectivityObserver(
                transaction, memory, libraryBase, 0xE0970,
                "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 54 41 55 41 56 41 57 4C 63 1D A1 5D BE 07 4C 8B F1 48 63 81 6C 5F 15 00 45 8B F9",
                (AssassinGroundFormationSlotDelegate)ChooseAssassinGroundFormationSlot);
        }

        private bool IsForbiddenFormationMoat(int player, int x, int y)
        {
            if ((uint)x >= MapWidth || (uint)y >= MapWidth) return true;
            int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
            return !IsValidTileId(tile) || (IsCompletedMoatTile(tile) &&
                ResolveCompletedMoatRelationship(player, tile) != CompletedMoatRelationship.Friendly);
        }

        internal bool IsMoveFormationTargetAvailable(int x, int y)
        {
            return !disposed && movementTargetAvailability != null &&
                (uint)x < MapWidth && (uint)y < MapWidth &&
                movementTargetAvailability[y * MapWidth + x] != 0;
        }

        private void ChooseOwnerSafeFormationSlot(IntPtr manager, int spacing, int x, int y)
        {
            int effectiveSpacing = spacing;
            try
            {
                effectiveSpacing = ResolveMoveFormationSpacing(
                    manager, spacing, x, y, MoveFormationSelector.Standard);
                if (TryChooseManagedFormationSlot(
                        manager,
                        effectiveSpacing,
                        x,
                        y,
                        MoveFormationSelector.Standard,
                        out _))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                RejectManagedFormationPlanForCurrentCommand();
                TryLogDiagnosticFailure("formation-spacing", ex);
            }
            if (nativeTribeManager == IntPtr.Zero)
            { InvokeOriginalFormationSlot(manager, effectiveSpacing, x, y); return; }
            int* state = (int*)((byte*)nativeTribeManager + 0x0C);
            int oldX = state[0], oldY = state[1], oldIndex = state[2];
            try { ChooseOwnerSafeFormationSlotCore(manager, effectiveSpacing, x, y); }
            catch (OriginalFormationSlotException)
            {
                // Never replay a native selector that already threw.
                throw;
            }
            catch (Exception ex)
            {
                // E1D30 only changes this output triple. Restore it before replaying
                // the unmodified selector; the individual owner audit remains active.
                state[0] = oldX; state[1] = oldY; state[2] = oldIndex;
                formationOwner = null;
                TryLogDiagnosticFailure("formation-slot", ex);
                InvokeOriginalFormationSlot(manager, effectiveSpacing, x, y);
            }
        }

        private int ChooseAssassinGroundFormationSlot(
            IntPtr manager, int spacing, int x, int y)
        {
            int effectiveSpacing = spacing;
            try
            {
                effectiveSpacing = ResolveMoveFormationSpacing(
                    manager, spacing, x, y, MoveFormationSelector.AssassinGround);
                if (TryChooseManagedFormationSlot(
                        manager,
                        effectiveSpacing,
                        x,
                        y,
                        MoveFormationSelector.AssassinGround,
                        out int tileId))
                {
                    return tileId;
                }
            }
            catch (Exception ex)
            {
                RejectManagedFormationPlanForCurrentCommand();
                TryLogDiagnosticFailure("assassin-ground-spacing", ex);
            }
            return originalAssassinGroundFormationSlot(manager, effectiveSpacing, x, y);
        }

        private bool TryChooseManagedFormationSlot(
            IntPtr manager,
            int spacing,
            int x,
            int y,
            MoveFormationSelector selector,
            out int tileId)
        {
            tileId = 0;
            MoveCommandScope command = activeMoveCommand;
            if (!IsScopedPureMoveFormationCall(manager, x, y, command) ||
                command == null || !command.HasFormationSpacing ||
                ReferenceEquals(managedFormationFailedOwner, command) ||
                command.ActiveUnitsAtDispatch <= 0 ||
                !settings.EnableMod || !settings.EnableMoveFormationEnhancements ||
                nativeTribeManager == IntPtr.Zero)
            {
                return false;
            }

            byte* tribeManager = (byte*)nativeTribeManager;
            if (command.TribeId < 0 || command.TribeId >= MaximumTribeCount)
                return false;
            int player = *(int*)(tribeManager + command.TribeId * TribeRecordSize + 0x2C);
            bool playerValid = GamePlayerManagerAPI.Instance.IsPlayerIdValid(player);
            if (!playerValid && !Shared.GameModeHelper.IsMapEditor())
                return false;

            int tick = CaptureCurrentGameTick();
            int nativeStamp = *(int*)((byte*)manager + 4);
            bool planChanged = !ReferenceEquals(managedFormationOwner, command) ||
                managedFormationSelector != selector ||
                formationEpoch != mapEpoch || formationTick != tick ||
                formationStamp != nativeStamp || formationPlayer != player ||
                formationRevision != placementRevision || formationSpacing != spacing;
            if (planChanged)
            {
                if (managedFormationOwner != null &&
                    ReferenceEquals(managedFormationOwner, command) &&
                    managedFormationCursor != 0)
                {
                    // Vanilla selects one formation algorithm for a group. A selector
                    // transition after assignment would make ordering ambiguous.
                    return false;
                }

                if (managedFormationPlanner == null)
                {
                    managedFormationPlanner = new MoveFormationPreviewPlanner(
                        IsMoveFormationTargetAvailable);
                }
                managedFormationOwner = null;
                managedFormationDestinations.Clear();
                managedFormationCursor = 0;
                managedFormationRequired = command.ActiveUnitsAtDispatch;
                managedFormationMetrics = managedFormationPlanner.Plan(
                    x,
                    y,
                    spacing,
                    managedFormationRequired,
                    selector == MoveFormationSelector.AssassinGround,
                    managedFormationDestinations,
                    playerValid
                        ? (Func<int, int, bool>)((candidateX, candidateY) =>
                            !IsForbiddenFormationMoat(player, candidateX, candidateY))
                        : null);
                managedFormationOwner = command;
                managedFormationSelector = selector;
                managedFormationCursor = 0;
                formationEpoch = mapEpoch;
                formationTick = tick;
                formationStamp = nativeStamp;
                formationPlayer = player;
                formationRevision = placementRevision;
                formationSpacing = spacing;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"MOVE_FORMATION_DRAG: formation-plan; tribe={command.TribeId}; " +
                    $"target={x},{y}; spacing={spacing}; selector={selector}; " +
                    $"required={managedFormationRequired}; visited={managedFormationMetrics.VisitedTiles}; " +
                    $"exact={managedFormationMetrics.ExactDestinations}; " +
                    $"relaxed={managedFormationMetrics.RelaxedDestinations}; " +
                    $"reused={managedFormationMetrics.ReusedDestinations}; " +
                    $"unique={managedFormationMetrics.UniqueDestinations}.");
            }

            if (managedFormationCursor < 0 ||
                managedFormationCursor >= managedFormationDestinations.Count)
            {
                return false;
            }

            MoveFormationDestination destination =
                managedFormationDestinations[managedFormationCursor++];
            int* state = (int*)((byte*)nativeTribeManager + 0x0C);
            state[0] = destination.X;
            state[1] = destination.Y;
            // MoveHere increments this value and aborts the remaining group above
            // 3999. The managed cursor is authoritative for this scoped command.
            state[2] = 0;
            tileId = destination.TileId;
            return true;
        }

        private void RejectManagedFormationPlanForCurrentCommand()
        {
            MoveCommandScope command = activeMoveCommand;
            if (command != null && command.HasFormationSpacing)
                managedFormationFailedOwner = command;
            managedFormationOwner = null;
            managedFormationDestinations.Clear();
            managedFormationCursor = 0;
            managedFormationRequired = 0;
            managedFormationMetrics = default;
        }

        private void CompleteManagedFormationPlan(MoveCommandScope command)
        {
            if (managedFormationOwner != null &&
                (command == null || ReferenceEquals(managedFormationOwner, command)))
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"MOVE_FORMATION_DRAG: formation-assigned; tribe={managedFormationOwner.TribeId}; " +
                    $"target={managedFormationOwner.TargetX},{managedFormationOwner.TargetY}; " +
                    $"spacing={managedFormationOwner.FormationSpacing}; " +
                    $"required={managedFormationRequired}; assigned={managedFormationCursor}; " +
                    $"unique={managedFormationMetrics.UniqueDestinations}; " +
                    $"relaxed={managedFormationMetrics.RelaxedDestinations}; " +
                    $"reused={managedFormationMetrics.ReusedDestinations}.");
            }
            managedFormationOwner = null;
            managedFormationFailedOwner = null;
            managedFormationDestinations.Clear();
            managedFormationCursor = 0;
            managedFormationRequired = 0;
            managedFormationMetrics = default;
        }

        private int ResolveMoveFormationSpacing(
            IntPtr manager,
            int vanillaSpacing,
            int x,
            int y,
            MoveFormationSelector selector)
        {
            MoveCommandScope command = activeMoveCommand;
            bool scopedPureMove = IsScopedPureMoveFormationCall(manager, x, y, command);
            int commandSpacing = command?.FormationSpacing ?? MoveFormationSpacingPolicy.Default;
            bool overrideEnabled = scopedPureMove && command != null && command.HasFormationSpacing &&
                settings.EnableMod && settings.EnableMoveFormationEnhancements;
            int effectiveSpacing = MoveFormationSpacingPolicy.ResolveEffectiveSpacing(
                vanillaSpacing, commandSpacing, overrideEnabled);
            if (overrideEnabled)
            {
                MoveFormationCommandSnapshotStore.Observe(
                    command,
                    selector,
                    vanillaSpacing,
                    effectiveSpacing);
            }
            return effectiveSpacing;
        }

        private void InvokeOriginalFormationSlot(IntPtr manager, int spacing, int x, int y)
        {
            try
            {
                originalFormationSlot(manager, spacing, x, y);
            }
            catch (Exception ex)
            {
                throw new OriginalFormationSlotException(ex);
            }
        }

        private bool IsScopedPureMoveFormationCall(
            IntPtr manager, int x, int y, MoveCommandScope command)
        {
            return !disposed && manager != IntPtr.Zero &&
                manager == nativePathManager && command != null &&
                command.TargetX == x && command.TargetY == y && !command.IsPatrolPath &&
                activeAttackCommand == null && activeMoatWorkSelection == null &&
                activeAttackApproachDiagnostic == null;
        }

        private void ChooseOwnerSafeFormationSlotCore(IntPtr manager, int spacing, int x, int y)
        {
            MoveCommandScope command = activeMoveCommand;
            if (disposed || !ExtensionsEnabled || manager == IntPtr.Zero || manager != nativePathManager || nativeTribeManager == IntPtr.Zero ||
                command == null || command.TargetX != x || command.TargetY != y ||
                command.TribeId < 0 || command.TribeId >= MaximumTribeCount || spacing <= 0 ||
                GetCurrentUnitMoveFrame() != null || placementBatch != null ||
                activeAttackCommand != null || activeMoatWorkSelection != null || activeAttackApproachDiagnostic != null)
            { InvokeOriginalFormationSlot(manager, spacing, x, y); return; }
            byte* tribeManager = (byte*)nativeTribeManager;
            int player = *(int*)(tribeManager + command.TribeId * TribeRecordSize + 0x2C);
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(player))
            { InvokeOriginalFormationSlot(manager, spacing, x, y); return; }
            int* slot = (int*)(tribeManager + 0x14);
            int* outputX = (int*)(tribeManager + 0x0C), outputY = (int*)(tribeManager + 0x10);
            int tick = CaptureCurrentGameTick(), stamp = *(int*)((byte*)manager + 4);
            if (!ReferenceEquals(formationOwner, command) || formationEpoch != mapEpoch ||
                formationTick != tick || formationStamp != stamp || formationPlayer != player || formationRevision != placementRevision ||
                formationSpacing != spacing)
            {
                formationOwner = command; formationEpoch = mapEpoch; formationTick = tick;
                formationStamp = stamp; formationPlayer = player; formationRevision = placementRevision;
                formationSpacing = spacing;
                formationExhausted = false;
            }
            bool rejected = false;
            if (*slot < 0 || *slot >= 3999) formationExhausted = true;
            // E1D30 returns index zero when no further candidate exists. Never retry
            // that reset as a fresh list: it would cycle through the same enemy tiles.
            if (!formationExhausted)
            {
                for (int attempts = 0; attempts <= 4000; attempts++)
                {
                    int requested = *slot;
                    InvokeOriginalFormationSlot(manager, spacing, x, y);
                    // The caller increments the same index and aborts at 4000 before
                    // assigning the unit. Use the native common-target fallback there.
                    if (*slot < requested || *slot < 0 || *slot >= 3999) break;
                    if (!IsForbiddenFormationMoat(player, *outputX, *outputY))
                    {
                        if (rejected) formationReplaced++;
                        return;
                    }
                    rejected = true; formationRejected++;
                    command.MoatRelevant = true;
                    *slot = *slot + 1;
                }
                formationExhausted = true;
            }
            bool validClick = (uint)x < MapWidth && (uint)y < MapWidth &&
                movementTargetAvailability[y * MapWidth + x] != 0 &&
                !IsForbiddenFormationMoat(player, x, y) &&
                (tileFlags[GameTileManagerAPI.Instance.GetTileId(x, y)] & MovementBlockedLowTileFlagMask) == 0;
            // Keep Vanilla's common-click fallback. An invalid click uses its reserved
            // (0,0) failure endpoint; it cannot publish a movement path.
            *slot = 0; *outputX = validClick ? x : 0; *outputY = validClick ? y : 0;
            formationFallbacks++;
        }
    }
}
