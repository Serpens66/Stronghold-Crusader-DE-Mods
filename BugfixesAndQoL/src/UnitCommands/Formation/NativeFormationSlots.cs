using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SHCDESE.API;
using RedBird.X64.Hooks.Transaction;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void FormationSlotDelegate(IntPtr manager, int spacing, int x, int y);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int AssassinGroundFormationSlotDelegate(
            IntPtr manager, int spacing, int x, int y);
        internal FormationSlotDelegate originalFormationSlot;
        internal AssassinGroundFormationSlotDelegate originalAssassinGroundFormationSlot;
        internal RedBirdDetour<FormationSlotDelegate> formationSlotDetour;
        internal RedBirdDetour<AssassinGroundFormationSlotDelegate> assassinGroundFormationSlotDetour;
        internal MoveCommandScope formationOwner;
        internal int formationEpoch, formationTick, formationStamp, formationPlayer, formationSpacing;
        internal long formationRevision;
        internal bool formationExhausted;
        internal long formationRejected, formationReplaced, formationFallbacks;
        internal IFormationCommandHandler formationRuntime;
        internal bool FormationHooksAvailable => formationSlotDetour != null && formationSlotDetour.Committed &&
            assassinGroundFormationSlotDetour != null && assassinGroundFormationSlotDetour.Committed &&
            commonGroupMoveDetour != null && commonGroupMoveDetour.Committed;

        internal sealed class OriginalFormationSlotException : Exception
        {
            internal OriginalFormationSlotException(Exception innerException)
                : base("The native formation selector delegate failed.", innerException) { }
        }

        internal void InstallFormationSlotAdapter(
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

        internal bool IsForbiddenFormationMoat(int player, int x, int y)
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

        internal void ChooseOwnerSafeFormationSlot(IntPtr manager, int spacing, int x, int y)
        {
            int* formationOutput = nativeTribeManager == IntPtr.Zero ? null : (int*)((byte*)nativeTribeManager + 0x0C);
            int beforeX = formationOutput == null ? 0 : formationOutput[0];
            int beforeY = formationOutput == null ? 0 : formationOutput[1];
            int beforeIndex = formationOutput == null ? 0 : formationOutput[2];
            try
            {
                if (formationRuntime != null && formationRuntime.TryChooseStandardFormationSlot(manager, spacing, x, y)) return;
            }
            catch (Exception ex)
            {
                if (formationOutput != null)
                { formationOutput[0] = beforeX; formationOutput[1] = beforeY; formationOutput[2] = beforeIndex; }
                formationRuntime.DisableForProcess("standard-selector", ex);
            }
            int effectiveSpacing = spacing;
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

        internal int ChooseAssassinGroundFormationSlot(
            IntPtr manager, int spacing, int x, int y)
        {
            int* formationOutput = nativeTribeManager == IntPtr.Zero ? null : (int*)((byte*)nativeTribeManager + 0x0C);
            int beforeX = formationOutput == null ? 0 : formationOutput[0];
            int beforeY = formationOutput == null ? 0 : formationOutput[1];
            int beforeIndex = formationOutput == null ? 0 : formationOutput[2];
            try
            {
                if (formationRuntime != null && formationRuntime.TryChooseAssassinFormationSlot(manager, spacing, x, y, out int tileId))
                    return tileId;
            }
            catch (Exception ex)
            {
                if (formationOutput != null)
                { formationOutput[0] = beforeX; formationOutput[1] = beforeY; formationOutput[2] = beforeIndex; }
                formationRuntime.DisableForProcess("assassin-selector", ex);
            }
            return originalAssassinGroundFormationSlot(manager, spacing, x, y);
        }

        internal void InvokeOriginalFormationSlot(IntPtr manager, int spacing, int x, int y)
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

        internal bool IsScopedPureMoveFormationCall(
            IntPtr manager, int x, int y, MoveCommandScope command)
        {
            return !disposed && manager != IntPtr.Zero &&
                manager == nativePathManager && command != null &&
                command.TargetX == x && command.TargetY == y && !command.IsPatrolPath &&
                activeAttackCommand == null && activeMoatWorkSelection == null &&
                activeAttackApproachDiagnostic == null;
        }

        internal void ChooseOwnerSafeFormationSlotCore(IntPtr manager, int spacing, int x, int y)
        {
            MoveCommandScope command = activeMoveCommand;
            if (TryChooseFastFormation(manager, x, y, out _)) return;
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
