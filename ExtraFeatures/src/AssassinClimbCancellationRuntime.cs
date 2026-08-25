// Feature: Synchronized Stop-command cancellation for Assassin climbing without polling or position history.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;

namespace ExtraFeatures
{
    internal sealed unsafe class AssassinClimbCancellationRuntime : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int SelectedUnitCommandDelegate(
            IntPtr unitManager,
            int tribeId,
            int command,
            int argument1,
            int argument2,
            int argument3);

        private const int SelectedUnitCommandRva = 0x199C70;
        private const int SelectedUnitCommandImplementationRva = 0x11E960;
        private const int TribeManagerRva = 0x7CC6720;
        private const int SelectionBitmapWordCount = 625;
        private const int UnitIdBitsPerWord = 16;
        private const ushort NormalTransitionState = 1;
        private const int AssassinAssignedTargetOffset = 0x413;
        private const int AssassinHeightDifferenceOffset = 0x414;
        private const int AssassinPreviousFacingOffset = 0x416;
        private const int AssassinDecayCounterOffset = 0x418;
        private const int AssassinTimerOffset = 0x41A;
        private const string SelectedUnitCommandPattern =
            "48 8D 0D A9 CA B2 07 E9 E4 4C F8 FF";

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private SelectedUnitCommandDelegate original;
        private SelectedUnitCommandDelegate rootedDetour;
        private NativeDetour detour;
        private bool invalidTribeLogged;
        private bool invalidRollbackLogged;

        public AssassinClimbCancellationRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void InitializeNative(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool fixedLayoutHashValidated)
        {
            if (detour != null)
                return;
            if (!fixedLayoutHashValidated)
                throw new InvalidOperationException("fixed native layout hash does not match the supported CrusaderDE.dll");
            if (libraryHandle == IntPtr.Zero || memory.Length <= SelectedUnitCommandRva + 12)
            {
                throw new InvalidOperationException("native module memory does not cover the selected-unit command executor");
            }

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
                memory,
                SelectedUnitCommandRva + 3,
                SelectedUnitCommandRva + 7);
            int resolvedImplementationRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                SelectedUnitCommandRva + 8,
                SelectedUnitCommandRva + 12);
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
                    "Extra Features Assassin climb cancellation installed on Vanilla's synchronized selected-unit UnitStop executor.");
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
            IntPtr unitManager,
            int tribeId,
            int command,
            int argument1,
            int argument2,
            int argument3)
        {
            SelectedUnitCommandDelegate vanilla = original;
            if (vanilla == null)
                return 0;

            if (AssassinClimbCancellationPolicy.ShouldHandleCommand(
                    settings.EnableMod,
                    settings.EnableImprovedAssassinPathfinding,
                    detour != null,
                    (uint)command))
            {
                try
                {
                    CancelClimbingAssassins(tribeId, command, argument1, argument2, argument3);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Extra Features Assassin climb cancellation failed; Vanilla's Stop command will still run: {ex}");
                }
            }

            return vanilla(unitManager, tribeId, command, argument1, argument2, argument3);
        }

        private void CancelClimbingAssassins(int tribeId, int command, int argument1, int argument2, int argument3)
        {
            GameTribeManagerAPI tribeApi = GameTribeManagerAPI.Instance;
            if (!tribeApi.IsValidId(tribeId) ||
                !tribeApi.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
            {
                LogInvalidTribeOnce(
                    $"raw arguments were tribeOrGroupId={tribeId}, command={command}, args=({argument1},{argument2},{argument3})");
                return;
            }

            int cancelled = 0;
            int currentRollbacks = 0;
            int previousRollbacks = 0;
            ushort* bitmap = &tribe->r_UnitIdsInGroupBitfield;
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            // Scan the complete proven bitfield instead of trusting a possibly transient member count.
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

                    int state = unit->r_AIState;
                    bool usePrevious = AssassinClimbCancellationPolicy.UsesPreviousTileForRollback(state);
                    ushort rollbackX = usePrevious ? unit->r_PreviousTilePositionX : unit->r_CurrentTilePositionX;
                    ushort rollbackY = usePrevious ? unit->r_PreviousTilePositionY : unit->r_CurrentTilePositionY;
                    if (!TryNormalizeClimbState(unitApi, localUnitId, unit, rollbackX, rollbackY))
                        continue;

                    cancelled++;
                    if (usePrevious)
                        previousRollbacks++;
                    else
                        currentRollbacks++;
                }
            }

            if (cancelled > 0)
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Extra Features cancelled Assassin climbing through synchronized Stop: tribeId={tribeId}, " +
                    $"cancelled={cancelled}, currentTileRollbacks={currentRollbacks}, previousTileRollbacks={previousRollbacks}.");
            }
        }

        private bool TryNormalizeClimbState(
            GameUnitManagerAPI unitApi,
            int localUnitId,
            GameUnit* unit,
            ushort rollbackX,
            ushort rollbackY)
        {
            if (rollbackX >= GameTileManagerAPI.MAX_WIDTH || rollbackY >= GameTileManagerAPI.MAX_HEIGHT)
            {
                LogInvalidRollbackOnce(localUnitId, rollbackX, rollbackY, "coordinates are outside the map");
                return false;
            }

            int tileId = GameTileManagerAPI.Instance.GetTileId(rollbackX, rollbackY);
            if (tileId < 0 || tileId >= AssassinClimbCancellationPolicy.TileCount)
            {
                LogInvalidRollbackOnce(localUnitId, rollbackX, rollbackY, $"derived tile ID {tileId} is invalid");
                return false;
            }

            int tileHeight = GameTileManagerAPI.Instance.GetTileHeight(tileId);
            if (tileHeight < 0 || tileHeight > ushort.MaxValue)
            {
                LogInvalidRollbackOnce(localUnitId, rollbackX, rollbackY, $"tile height {tileHeight} is invalid");
                return false;
            }

            unitApi.SetCurrentLocalTilePosition(
                localUnitId,
                new UnmanagedVector2<ushort>(rollbackX, rollbackY));

            byte* raw = (byte*)unit;
            unit->r_HeightElevation = (ushort)tileHeight;
            unit->r_AIState = NormalTransitionState;
            unit->r_AnimationTimer = 0;
            unit->r_CurrentSpriteAnimationFrame = 0;
            unit->N00000061 = 0;
            *(raw + AssassinAssignedTargetOffset) = 0;
            *(short*)(raw + AssassinHeightDifferenceOffset) = 0;
            *(ushort*)(raw + AssassinPreviousFacingOffset) = (ushort)unit->r_Direction;
            *(short*)(raw + AssassinDecayCounterOffset) = 0;
            *(ushort*)(raw + AssassinTimerOffset) = 0;
            return true;
        }

        private void LogInvalidTribeOnce(string reason)
        {
            if (invalidTribeLogged)
                return;
            invalidTribeLogged = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Extra Features could not safely dereference an Assassin climb-stop tribe; {reason}. Vanilla behavior remains active.");
        }

        private void LogInvalidRollbackOnce(int localUnitId, ushort x, ushort y, string reason)
        {
            if (invalidRollbackLogged)
                return;
            invalidRollbackLogged = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Extra Features skipped an Assassin climb cancellation for unit {localUnitId} at ({x},{y}) because {reason}; Vanilla's Stop command will still run.");
        }
    }
}
