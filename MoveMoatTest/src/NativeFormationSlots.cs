using System;
using System.Runtime.InteropServices;
using SHCDESE.API;

namespace MoveMoatTest
{
    internal sealed unsafe partial class MoveMoatPathTest
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void FormationSlotDelegate(IntPtr manager, int spacing, int x, int y);
        private FormationSlotDelegate originalFormationSlot;
        private MoveCommandScope formationOwner;
        private int formationEpoch, formationTick, formationStamp, formationPlayer, formationSpacing;
        private long formationRevision;
        private bool formationExhausted;
        private long formationRejected, formationReplaced, formationFallbacks;

        private void InstallFormationSlotAdapter(ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            // FBCB9319 E1D30..E1D3F: three complete nonvolatile-register saves.
            // No patch of the search field or its terrain/visit stamps is needed.
            InstallConnectivityObserver(memory, libraryBase, 0xE1D30,
                "48 89 5C 24 08 48 89 6C 24 18 48 89 74 24 20 89 54 24 10 57 41 54 41 55 41 56 41 57 4C 63 1D E1 49 BE 07 4C 8B F1 48 63",
                (FormationSlotDelegate)ChooseOwnerSafeFormationSlot, out originalFormationSlot);
        }

        private bool IsForbiddenFormationMoat(int player, int x, int y)
        {
            if ((uint)x >= MapWidth || (uint)y >= MapWidth) return true;
            int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
            return !IsValidTileId(tile) || (IsCompletedMoatTile(tile) &&
                ResolveCompletedMoatRelationship(player, tile) != CompletedMoatRelationship.Friendly);
        }

        private void ChooseOwnerSafeFormationSlot(IntPtr manager, int spacing, int x, int y)
        {
            if (nativeTribeManager == IntPtr.Zero)
            { originalFormationSlot(manager, spacing, x, y); return; }
            int* state = (int*)((byte*)nativeTribeManager + 0x0C);
            int oldX = state[0], oldY = state[1], oldIndex = state[2];
            try { ChooseOwnerSafeFormationSlotCore(manager, spacing, x, y); }
            catch (Exception ex)
            {
                // E1D30 only changes this output triple. Restore it before replaying
                // the unmodified selector; the individual owner audit remains active.
                state[0] = oldX; state[1] = oldY; state[2] = oldIndex;
                formationOwner = null;
                TryLogDiagnosticFailure("formation-slot", ex);
                originalFormationSlot(manager, spacing, x, y);
            }
        }

        private void ChooseOwnerSafeFormationSlotCore(IntPtr manager, int spacing, int x, int y)
        {
            MoveCommandScope command = activeMoveCommand;
            if (disposed || !ExtensionsEnabled || manager == IntPtr.Zero || manager != nativePathManager || nativeTribeManager == IntPtr.Zero ||
                command == null || command.TargetX != x || command.TargetY != y ||
                command.TribeId < 0 || command.TribeId >= MaximumTribeCount || spacing <= 0 ||
                GetCurrentUnitMoveFrame() != null || placementBatch != null ||
                activeAttackCommand != null || activeMoatWorkSelection != null || activeAttackApproachDiagnostic != null)
            { originalFormationSlot(manager, spacing, x, y); return; }
            byte* tribeManager = (byte*)nativeTribeManager;
            int player = *(int*)(tribeManager + command.TribeId * TribeRecordSize + 0x2C);
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(player))
            { originalFormationSlot(manager, spacing, x, y); return; }
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
                    originalFormationSlot(manager, spacing, x, y);
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
