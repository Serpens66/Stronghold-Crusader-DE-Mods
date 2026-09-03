// Feature: Permit cavalry movement commands onto passable stockpile tiles.
//
// CrusaderDE.dll SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2:
// the Goodsyard JNEs at RVAs 0x8E6EC and 0x8EB50 deliberately route stockpile
// building hovers into Vanilla's ordinary-movement checks. Vanilla then classifies
// cavalry as requiring an additional wall/elevated-tile check. Because stockpile
// tiles deliberately carry IsWall, that branch rejects them before its exact flood.
// For Goodsyard targets only, make the cursor and its order-feedback path retain
// their already-computed ordinary movement result while leaving every other
// special-unit target unchanged.
// The same four mounted types have movement-class table value zero. MoveHere therefore
// applies one additional endpoint-only IsWall|IsElevated test at RVA 0x19648D.
// For an already validated Goodsyard command, redirect only that test's read to a
// private zero word; keep both the original test/branch and Vanilla's cavalry path
// builder unchanged.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace BugfixesAndQoL
{
    internal sealed unsafe class MountedStockpileMovementPatch : IDisposable
    {
        internal const string CursorMountedClassificationPattern =
            "E8 ?? ?? ?? ?? 85 C0 74 66 4C 63 0D ?? ?? ?? ?? 48 8D 15 ?? ?? ?? ?? 4E 0F BF 94 4A E4 C4 09 00";
        internal const string FeedbackMountedClassificationPattern =
            "E8 ?? ?? ?? ?? 85 C0 74 6A 4C 63 0D ?? ?? ?? ?? 48 8D 15 ?? ?? ?? ?? 4E 0F BF 94 4A A4 E2 AA 03";
        internal const string MountedEndpointWallGatePattern =
            "44 39 94 24 80 00 00 00 75 11 F7 84 8A B0 71 8F 04 00 01 00 10 " +
            "0F 85 CE 02 00 00 44 8B AC 24 90 00 00 00 44 3B E3 0F 84 D6 00 00 00";

        private const int CursorClassificationCallRva = 0x8F209;
        private const int FeedbackClassificationCallRva = 0x195F5E;
        private const int CursorClassificationFunctionRva = 0x196B20;
        private const int FeedbackClassificationFunctionRva = 0x180B60;
        private const int ClassificationHookOffset = 5;
        private const int ClassificationHookSize = 18;
        private const int MountedEndpointWallGatePatternRva = 0x196483;
        private const int MountedEndpointWallGateHookOffset = 0xA;
        private const int MountedEndpointWallGateHookSize = 17;
        private const int MountedEndpointWallGateJumpOffset = 11;
        private const int MountedEndpointWallGateFailureRva = 0x19676C;
        private const int CursorTargetTileRva = 0x3A11E38;
        private const int FeedbackTargetTileRva = 0x3A11E28;
        private const int MovementTargetAvailabilityRva = 0x3A11EA4;
        private const int TileYLookupRva = 0x3AAE2A4;
        private const int RowLookupRva = 0x402FF2C;
        private const int TileFlagsRva = 0x48F71B0;
        private const int MapWidth = 800;
        private const int NativeTileCount = 0x4E520;
        private const int NativeUnitCount = 10000;
        private const int UnitFromManagerRelativeBaseOffset = 0x65C;
        private static readonly byte[] CursorClassificationHookBytes =
        {
            0x85, 0xC0, 0x74, 0x66, 0x4C, 0x63, 0x0D, 0x1F, 0x2C,
            0x98, 0x03, 0x48, 0x8D, 0x15, 0xA0, 0x2B, 0x98, 0x03
        };
        private static readonly byte[] FeedbackClassificationHookBytes =
        {
            0x85, 0xC0, 0x74, 0x6A, 0x4C, 0x63, 0x0D, 0xBA, 0xBE,
            0x87, 0x03, 0x48, 0x8D, 0x15, 0x8B, 0xA0, 0xE6, 0xFF
        };
        private static readonly byte[] MountedEndpointWallGateHookBytes =
        {
            0xF7, 0x84, 0x8A, 0xB0, 0x71, 0x8F, 0x04, 0x00, 0x01, 0x00, 0x10,
            0x0F, 0x85, 0xCE, 0x02, 0x00, 0x00
        };
        private static readonly byte[] PrimaryGoodsyardJumpBytes =
            { 0x0F, 0x85, 0x96, 0x08, 0x00, 0x00 };
        private static readonly byte[] AlternateGoodsyardJumpBytes =
            { 0x0F, 0x85, 0xF4, 0x01, 0x00, 0x00 };

        private readonly ManualLogSource log;
        private readonly int* cursorTargetTile;
        private readonly int* feedbackTargetTile;
        private readonly byte* movementTargetAvailability;
        private readonly short* tileYLookup;
        private readonly int* rowLookup;
        private readonly uint* tileFlags;
        private HookTransaction transaction;
        private HookRef<X64InlineHook> cursorClassificationHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> feedbackClassificationHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> mountedEndpointWallGateHook = new HookRef<X64InlineHook>();
        private IntPtr endpointZeroFlags;
        private bool classificationCallbackFailureLogged;
        private bool endpointCallbackFailureLogged;
        private bool disposed;

        public MountedStockpileMovementPatch(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The mounted-stockpile movement hooks require the audited CrusaderDE.dll layout.");
            }

            Shared.NativeResolution cursorResolution = Shared.NativePatternResolver.ResolveUnique(
                memory, CursorMountedClassificationPattern, CursorClassificationCallRva, referenceHashMatches,
                "mounted-stockpile cursor mounted classification", log);
            Shared.NativeResolution feedbackResolution = Shared.NativePatternResolver.ResolveUnique(
                memory, FeedbackMountedClassificationPattern, FeedbackClassificationCallRva, referenceHashMatches,
                "mounted-stockpile order-feedback mounted classification", log);
            Shared.NativeResolution endpointResolution = Shared.NativePatternResolver.ResolveUnique(
                memory, MountedEndpointWallGatePattern, MountedEndpointWallGatePatternRva, referenceHashMatches,
                "mounted-stockpile per-unit endpoint wall gate", log);

            ValidateClassificationCallAndSpan(
                memory, cursorResolution.Rva, CursorClassificationFunctionRva,
                CursorClassificationHookBytes, "cursor");
            ValidateClassificationCallAndSpan(
                memory, feedbackResolution.Rva, FeedbackClassificationFunctionRva,
                FeedbackClassificationHookBytes, "order-feedback");
            ValidateExactBytes(
                memory, endpointResolution.Rva + MountedEndpointWallGateHookOffset,
                MountedEndpointWallGateHookBytes, "mounted per-unit endpoint wall-gate hook span");
            ValidateRelativeJump(
                memory, endpointResolution.Rva + MountedEndpointWallGateHookOffset +
                    MountedEndpointWallGateJumpOffset,
                MountedEndpointWallGateFailureRva, "mounted per-unit endpoint rejection");
            ValidateExactBytes(memory, 0x8E6EC, PrimaryGoodsyardJumpBytes, "primary Goodsyard movement jump");
            ValidateExactBytes(memory, 0x8EB50, AlternateGoodsyardJumpBytes, "alternate Goodsyard movement jump");

            cursorTargetTile = (int*)(libraryBase + CursorTargetTileRva);
            feedbackTargetTile = (int*)(libraryBase + FeedbackTargetTileRva);
            movementTargetAvailability = (byte*)(libraryBase + MovementTargetAvailabilityRva);
            tileYLookup = (short*)(libraryBase + TileYLookupRva);
            rowLookup = (int*)(libraryBase + RowLookupRva);
            tileFlags = (uint*)(libraryBase + TileFlagsRva);

            try
            {
                endpointZeroFlags = Marshal.AllocHGlobal(sizeof(uint));
                *(uint*)endpointZeroFlags = 0;
                transaction = new HookTransaction(
                    memory, libraryBase, loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref cursorClassificationHook,
                    libraryBase + unchecked((ulong)(cursorResolution.Rva + ClassificationHookOffset)),
                    CorrectCursorMountedClassification,
                    regs: X64SmartCPUContextRegs.RAX,
                    hookSize: ClassificationHookSize,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref feedbackClassificationHook,
                    libraryBase + unchecked((ulong)(feedbackResolution.Rva + ClassificationHookOffset)),
                    CorrectFeedbackMountedClassification,
                    regs: X64SmartCPUContextRegs.RAX,
                    hookSize: ClassificationHookSize,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref mountedEndpointWallGateHook,
                    libraryBase + unchecked((ulong)(endpointResolution.Rva + MountedEndpointWallGateHookOffset)),
                    PermitMountedGoodsyardEndpoint,
                    regs: X64SmartCPUContextRegs.Volatile |
                        X64SmartCPUContextRegs.RSI | X64SmartCPUContextRegs.RDI |
                        X64SmartCPUContextRegs.RBP | X64SmartCPUContextRegs.R14,
                    hookSize: MountedEndpointWallGateHookSize,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();
                if (!cursorClassificationHook.Success || !feedbackClassificationHook.Success ||
                    !mountedEndpointWallGateHook.Success)
                {
                    throw new InvalidOperationException(
                        "The mounted-stockpile cursor, order-feedback, and endpoint hooks were not installed atomically.");
                }
            }
            catch
            {
                transaction?.Unload();
                transaction?.Dispose();
                transaction = null;
                FreeEndpointZeroFlags();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Bugfixes and QoL mounted-stockpile movement hooks installed; " +
                $"cursor=0x{cursorResolution.Rva + ClassificationHookOffset:X}, " +
                $"orderFeedback=0x{feedbackResolution.Rva + ClassificationHookOffset:X}, " +
                $"endpoint=0x{endpointResolution.Rva + MountedEndpointWallGateHookOffset:X}.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
            FreeEndpointZeroFlags();
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL mounted-stockpile movement hooks disposed.");
        }

        private void CorrectCursorMountedClassification(NativePointer<X64SmartCPUContext> context) =>
            CorrectMountedClassification(context, cursorTargetTile);

        private void CorrectFeedbackMountedClassification(NativePointer<X64SmartCPUContext> context) =>
            CorrectMountedClassification(context, feedbackTargetTile);

        private void CorrectMountedClassification(
            NativePointer<X64SmartCPUContext> context,
            int* targetTilePointer)
        {
            if (disposed || unchecked((long)context.Pointer->RAX) <= 0)
                return;

            try
            {
                long vanillaClassificationResult = unchecked((long)context.Pointer->RAX);
                int targetTileId = *targetTilePointer;
                bool coordinateValid = TryResolveTileCoordinates(targetTileId, out int targetX, out int targetY);
                bool targetAvailable = coordinateValid &&
                    movementTargetAvailability[targetY * MapWidth + targetX] != 0;
                uint targetFlags = coordinateValid ? tileFlags[targetTileId] : 0;
                if (!coordinateValid || !targetAvailable ||
                    (targetFlags & MountedStockpileMovementPolicy.GoodsyardRelated) == 0)
                {
                    return;
                }

                // Selection enumeration is comparatively expensive and the cursor invokes
                // this path repeatedly, so only perform it for an eligible Goodsyard tile.
                CaptureMountedSelection(
                    out int selectedCount,
                    out int mountedCount,
                    out bool allResolved);

                if (!MountedStockpileMovementPolicy.ShouldUseNormalMovementClassification(
                        vanillaClassificationResult, coordinateValid, targetAvailable, targetFlags,
                        selectedCount, mountedCount, allResolved))
                {
                    return;
                }

                // The displaced test/je now takes the same branch used by ordinary units,
                // retaining Vanilla's already-computed region result in EBX or EDI.
                context.Pointer->RAX = 0;
            }
            catch (Exception ex)
            {
                if (!classificationCallbackFailureLogged)
                {
                    classificationCallbackFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Mounted-stockpile classification callback failed closed; Vanilla result is retained: {ex}");
                }
            }
        }

        private void PermitMountedGoodsyardEndpoint(NativePointer<X64SmartCPUContext> context)
        {
            // This callback runs immediately before the displaced test/jne pair and
            // only on Vanilla's movement-class-zero path.
            if (disposed || endpointZeroFlags == IntPtr.Zero)
                return;

            try
            {
                int targetTileId = unchecked((int)context.Pointer->RCX);
                bool coordinateValid = TryResolveTileCoordinates(targetTileId, out int targetX, out int targetY) &&
                    targetX == unchecked((int)context.Pointer->R14) &&
                    targetY == unchecked((int)context.Pointer->RBP);
                bool targetAvailable = coordinateValid &&
                    movementTargetAvailability[targetY * MapWidth + targetX] != 0;
                uint targetFlags = coordinateValid ? tileFlags[targetTileId] : 0;
                bool vanillaWallGateRejected =
                    (targetFlags & MountedStockpileMovementPolicy.IsWallOrElevated) != 0;
                if (!coordinateValid || !targetAvailable || !vanillaWallGateRejected ||
                    (targetFlags & MountedStockpileMovementPolicy.GoodsyardRelated) == 0)
                {
                    return;
                }

                // FUN_180196280 receives a 1-based unit ID. Its RDI is not a
                // GameUnit pointer: it is managerBase + unitId * 0x490, while the
                // corresponding GameUnit starts another 0x65C bytes later.
                int unitId = unchecked((int)context.Pointer->RSI);
                GameUnit* currentUnit = null;
                bool currentUnitResolved = (uint)unitId - 1u < NativeUnitCount &&
                    GameUnitManagerAPI.Instance != null &&
                    GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out currentUnit) &&
                    currentUnit != null &&
                    (ulong)currentUnit == context.Pointer->RDI + UnitFromManagerRelativeBaseOffset &&
                    currentUnit->r_AliveState == AliveState.IsAlive &&
                    IsPlayableMountedType(currentUnit->r_UnitChimp);

                if (!MountedStockpileMovementPolicy.ShouldBypassMountedEndpointWallGate(
                        vanillaWallGateRejected,
                        coordinateValid, targetAvailable, targetFlags,
                        currentUnitResolved))
                {
                    return;
                }

                // RCX and the image-base value in RDX are dead after the displaced
                // test/jne pair. Redirect that one read to our zero word so the
                // original TEST produces ZF=1 and the original JNE falls through.
                context.Pointer->RCX = 0;
                context.Pointer->RDX = unchecked(
                    (ulong)endpointZeroFlags.ToInt64() - (ulong)TileFlagsRva);
            }
            catch (Exception ex)
            {
                if (!endpointCallbackFailureLogged)
                {
                    endpointCallbackFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Mounted-stockpile endpoint callback failed closed; Vanilla rejection is retained: {ex}");
                }
            }
        }

        private void FreeEndpointZeroFlags()
        {
            if (endpointZeroFlags == IntPtr.Zero)
                return;

            Marshal.FreeHGlobal(endpointZeroFlags);
            endpointZeroFlags = IntPtr.Zero;
        }

        private bool TryResolveTileCoordinates(int tileId, out int x, out int y)
        {
            x = -1;
            y = -1;
            if ((uint)tileId >= NativeTileCount)
                return false;

            y = tileYLookup[tileId];
            if ((uint)y >= MapWidth)
                return false;

            x = tileId - rowLookup[y * 3];
            return (uint)x < MapWidth;
        }

        private static void CaptureMountedSelection(
            out int selectedCount,
            out int mountedCount,
            out bool allResolved)
        {
            selectedCount = 0;
            mountedCount = 0;
            allResolved = true;
            int[] selected = GamePlayerManagerAPI.Instance?.GetSelectedChimps();
            if (selected == null || selected.Length == 0 || GameUnitManagerAPI.Instance == null)
            {
                allResolved = false;
                return;
            }

            selectedCount = selected.Length;
            for (int index = 0; index < selected.Length; index++)
            {
                int unitId = selected[index];
                if (unitId <= 0 ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    allResolved = false;
                    continue;
                }

                eChimps type = unit->r_UnitChimp;
                if (IsPlayableMountedType(type))
                    mountedCount++;
            }
        }

        private static bool IsPlayableMountedType(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_KNIGHT:
                case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                    return true;
                default:
                    return false;
            }
        }

        private static void ValidateClassificationCallAndSpan(
            ReadOnlySpan<byte> memory,
            int callRva,
            int expectedFunctionRva,
            byte[] expectedSpan,
            string stage)
        {
            ValidateExactBytes(
                memory, callRva + ClassificationHookOffset, expectedSpan,
                stage + " mounted-classification hook span");
            if (callRva < 0 || callRva > memory.Length - ClassificationHookOffset || memory[callRva] != 0xE8)
                throw new InvalidOperationException(
                    "The mounted-stockpile " + stage + " classification call is not CALL rel32.");

            int displacement = memory[callRva + 1] |
                memory[callRva + 2] << 8 |
                memory[callRva + 3] << 16 |
                memory[callRva + 4] << 24;
            int targetRva = checked(callRva + ClassificationHookOffset + displacement);
            if (targetRva != expectedFunctionRva)
            {
                throw new InvalidOperationException(
                    $"The mounted-stockpile {stage} call targets RVA 0x{targetRva:X}, " +
                    $"not the audited mounted classifier 0x{expectedFunctionRva:X}.");
            }
        }

        private static void ValidateExactBytes(
            ReadOnlySpan<byte> memory,
            int rva,
            byte[] expected,
            string label)
        {
            if (rva < 0 || rva > memory.Length - expected.Length ||
                !memory.Slice(rva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException("The native " + label + " bytes do not match the audited DLL.");
            }
        }

        private static void ValidateRelativeJump(
            ReadOnlySpan<byte> memory,
            int jumpRva,
            int expectedTargetRva,
            string label)
        {
            if (jumpRva < 0 || jumpRva > memory.Length - 6 ||
                memory[jumpRva] != 0x0F || memory[jumpRva + 1] != 0x85)
            {
                throw new InvalidOperationException("The native " + label + " is not JNE rel32.");
            }

            int displacement = memory[jumpRva + 2] |
                memory[jumpRva + 3] << 8 |
                memory[jumpRva + 4] << 16 |
                memory[jumpRva + 5] << 24;
            int targetRva = checked(jumpRva + 6 + displacement);
            if (targetRva != expectedTargetRva)
            {
                throw new InvalidOperationException(
                    $"The native {label} targets RVA 0x{targetRva:X}, not 0x{expectedTargetRva:X}.");
            }
        }
    }
}
