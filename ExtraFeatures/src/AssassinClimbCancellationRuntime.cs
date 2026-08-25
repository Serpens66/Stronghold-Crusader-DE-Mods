// Feature: Cancels active Assassin climbing through Vanilla's synchronized Stop command.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ExtraFeatures
{
    internal sealed unsafe class AssassinClimbCancellationRuntime : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int SelectedUnitCommandDelegate(
            IntPtr unitManager, int tribeId, int command, int argument1, int argument2, int argument3);

        private const int SelectedUnitCommandRva = 0x199C70;
        private const int SelectedUnitCommandImplementationRva = 0x11E960;
        private const int TribeManagerRva = 0x7CC6720;
        private const int SelectionBitmapWordCount = 625;
        private const int UnitIdBitsPerWord = 16;
        private const ushort NormalMovementState = 101;
        private const ushort StoppedMovementState = 8;
        private const int AssassinClimbVisualActiveOffset = 0x40F;
        private const int AssassinClimbProgressOffset = 0x414;
        private const int AssassinFacingOffset = 0x416;
        private const ushort CompletedClimbFacing = 0x20;
        private const string SelectedUnitCommandPattern =
            "48 8D 0D A9 CA B2 07 E9 E4 4C F8 FF";

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private SelectedUnitCommandDelegate original;
        private SelectedUnitCommandDelegate rootedDetour;
        private NativeDetour detour;
        private bool invalidTribeLogged;

        public AssassinClimbCancellationRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool fixedLayoutHashValidated)
        {
            if (detour != null)
                return;
            if (!fixedLayoutHashValidated)
                throw new InvalidOperationException("fixed native layout hash does not match the supported CrusaderDE.dll");
            if (libraryHandle == IntPtr.Zero || memory.Length <= SelectedUnitCommandRva + 12)
                throw new InvalidOperationException("native module memory does not cover the selected-unit command executor");

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                SelectedUnitCommandPattern,
                SelectedUnitCommandRva,
                referenceHashMatches: true,
                "selected-unit command executor",
                log);
            if (resolution.Rva != SelectedUnitCommandRva)
                throw new InvalidOperationException("selected-unit command executor resolved outside its validated RVA");

            int resolvedSelectionManagerRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, SelectedUnitCommandRva + 3, SelectedUnitCommandRva + 7);
            int resolvedImplementationRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, SelectedUnitCommandRva + 8, SelectedUnitCommandRva + 12);
            if (resolvedSelectionManagerRva != TribeManagerRva ||
                resolvedImplementationRva != SelectedUnitCommandImplementationRva)
            {
                throw new InvalidOperationException(
                    $"selected-unit command executor targets changed: tribeManager=0x{resolvedSelectionManagerRva:X}, implementation=0x{resolvedImplementationRva:X}");
            }

            rootedDetour = OnSelectedUnitCommand;
            IntPtr detourAddress = Marshal.GetFunctionPointerForDelegate(rootedDetour);
            NativeDetour installed = null;
            try
            {
                installed = new NativeDetour(
                    IntPtr.Add(libraryHandle, SelectedUnitCommandRva),
                    detourAddress,
                    new NativeDetourConfig { ManualApply = true });
                original = installed.GenerateTrampoline<SelectedUnitCommandDelegate>();
                installed.Apply();
                detour = installed;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Extra Features Assassin climb cancellation installed on Vanilla's synchronized selected-unit Stop processing.");
            }
            catch
            {
                installed?.Dispose();
                original = null;
                rootedDetour = null;
                throw;
            }
        }

        public void Dispose()
        {
            detour?.Undo();
            detour?.Dispose();
            detour = null;
            original = null;
            rootedDetour = null;
        }

        private int OnSelectedUnitCommand(
            IntPtr unitManager, int tribeId, int command, int argument1, int argument2, int argument3)
        {
            SelectedUnitCommandDelegate vanilla = original;
            if (vanilla == null)
                return 0;

            List<PendingCancellation> pending = null;
            if (AssassinClimbCancellationPolicy.ShouldHandleCommand(
                    settings.EnableMod,
                    settings.EnableImprovedAssassinPathfinding,
                    detour != null,
                    (uint)command))
            {
                try
                {
                    pending = CaptureClimbingAssassins(tribeId);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Extra Features could not capture climbing Assassins before Stop; Vanilla remains active: {ex}");
                }
            }

            // Vanilla deliberately ignores Stop while the Assassin is in states 126-129. Complete
            // only that state transition first, then let the original command clear paths and orders.
            if (pending != null && pending.Count > 0)
            {
                try
                {
                    ApplyCancellations(tribeId, pending);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Extra Features Assassin climb cancellation failed before Vanilla Stop: {ex}");
                }
            }

            return vanilla(unitManager, tribeId, command, argument1, argument2, argument3);
        }

        private List<PendingCancellation> CaptureClimbingAssassins(int tribeId)
        {
            GameTribeManagerAPI tribeApi = GameTribeManagerAPI.Instance;
            if (!tribeApi.IsValidId(tribeId) ||
                !tribeApi.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
            {
                LogInvalidTribeOnce(tribeId);
                return null;
            }

            List<PendingCancellation> pending = new List<PendingCancellation>();
            ushort* bitmap = &tribe->r_UnitIdsInGroupBitfield;
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            for (int wordIndex = 0; wordIndex < SelectionBitmapWordCount; wordIndex++)
            {
                ushort word = bitmap[wordIndex];
                if (word == 0)
                    continue;

                for (int bitIndex = 0; bitIndex < UnitIdBitsPerWord; bitIndex++)
                {
                    if ((word & (1 << bitIndex)) == 0)
                        continue;

                    int localUnitId = wordIndex * UnitIdBitsPerWord + bitIndex;
                    if (localUnitId <= 0 || !unitApi.TryGetUnitById(localUnitId, out GameUnit* unit) || unit == null)
                        continue;
                    if (unit->r_AliveState != AliveState.IsAlive ||
                        unit->r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN ||
                        !AssassinClimbCancellationPolicy.IsClimbingState(unit->r_AIState))
                        continue;

                    pending.Add(new PendingCancellation(
                        localUnitId,
                        unit->r_GlobalId,
                        unit->r_AIState));
                }
            }

            return pending;
        }

        private void ApplyCancellations(int tribeId, List<PendingCancellation> pending)
        {
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            int cancelled = 0;
            int state126 = 0;
            int state127 = 0;
            int state128 = 0;
            int state129 = 0;
            for (int index = 0; index < pending.Count; index++)
            {
                PendingCancellation cancellation = pending[index];
                if (!unitApi.TryGetUnitById(cancellation.LocalUnitId, out GameUnit* unit) || unit == null ||
                    unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_GlobalId != cancellation.GlobalUnitId ||
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN)
                    continue;

                ApplyCancellation(unit);
                cancelled++;
                switch (cancellation.OriginalState)
                {
                    case AssassinClimbCancellationPolicy.ThrowingHookState: state126++; break;
                    case AssassinClimbCancellationPolicy.ClimbingUpState: state127++; break;
                    case AssassinClimbCancellationPolicy.StartClimbingDownState: state128++; break;
                    case AssassinClimbCancellationPolicy.ClimbingDownState: state129++; break;
                }
            }

            if (cancelled > 0)
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Extra Features cancelled Assassin climbing through synchronized Stop: tribeId={tribeId}, " +
                    $"cancelled={cancelled}, states126-129=" +
                    $"{state126}/{state127}/{state128}/{state129}.");
            }
        }

        private static void ApplyCancellation(GameUnit* unit)
        {
            // Keep Vanilla's current tile and occupancy registration together. During state 129
            // Current may already be the lower tile; teleporting it back desynchronizes the tile list.
            unit->r_MovingRelevant = StoppedMovementState;
            unit->r_AIState = NormalMovementState;
            unit->r_AnimationTimer = 0;
            unit->r_CurrentSpriteAnimationFrame = 0;
            unit->N00000061 = 0;

            byte* raw = (byte*)unit;
            // These are the exact fields Vanilla clears when states 127/129 finish naturally.
            *(raw + AssassinClimbVisualActiveOffset) = 0;
            *(ushort*)(raw + AssassinClimbProgressOffset) = 0;
            *(ushort*)(raw + AssassinFacingOffset) = CompletedClimbFacing;
        }

        private void LogInvalidTribeOnce(int tribeId)
        {
            if (invalidTribeLogged)
                return;
            invalidTribeLogged = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Extra Features could not safely resolve Assassin Stop tribe {tribeId}; Vanilla behavior remains active.");
        }

        private readonly struct PendingCancellation
        {
            public PendingCancellation(int localUnitId, uint globalUnitId, ushort originalState)
            {
                LocalUnitId = localUnitId;
                GlobalUnitId = globalUnitId;
                OriginalState = originalState;
            }

            public int LocalUnitId { get; }
            public uint GlobalUnitId { get; }
            public ushort OriginalState { get; }
        }
    }
}
