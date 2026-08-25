// Feature: Stop-command diagnostics for Assassin climbing without polling or position history.
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
                    "Extra Features Assassin climb-stop diagnostics installed on Vanilla's synchronized selected-unit UnitStop executor.");
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

            if (AssassinClimbCancellationPolicy.ShouldInspectCommand(
                    settings.EnableMod,
                    settings.EnableImprovedAssassinPathfinding,
                    detour != null,
                    (uint)command))
            {
                try
                {
                    InspectTribe(tribeId, command, argument1, argument2, argument3);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Extra Features Assassin climb-stop diagnostics failed without changing Vanilla behavior: {ex}");
                }
            }

            return vanilla(unitManager, tribeId, command, argument1, argument2, argument3);
        }

        private void InspectTribe(int tribeId, int command, int argument1, int argument2, int argument3)
        {
            GameTribeManagerAPI tribeApi = GameTribeManagerAPI.Instance;
            if (!tribeApi.IsValidId(tribeId) ||
                !tribeApi.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
            {
                LogInvalidTribeOnce(
                    $"raw arguments were tribeOrGroupId={tribeId}, command={command}, args=({argument1},{argument2},{argument3})");
                return;
            }

            int bitmapMembers = 0;
            int resolvedUnits = 0;
            int assassinCandidates = 0;
            int climbingAssassins = 0;
            ushort* bitmap = &tribe->r_UnitIdsInGroupBitfield;
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            // Scan the complete proven bitfield. r_UnitsInGroup is logged as evidence, not used as
            // an assumption that could hide later members when Vanilla's data differs unexpectedly.
            for (int wordIndex = 0; wordIndex < SelectionBitmapWordCount; wordIndex++)
            {
                ushort word = bitmap[wordIndex];
                if (word == 0)
                    continue;

                for (int bitIndex = 0; bitIndex < UnitIdBitsPerWord; bitIndex++)
                {
                    if ((word & (1 << bitIndex)) == 0)
                        continue;

                    bitmapMembers++;
                    int localUnitId = wordIndex * UnitIdBitsPerWord + bitIndex;
                    if (localUnitId <= 0 || !unitApi.TryGetUnitById(localUnitId, out GameUnit* unit) || unit == null)
                        continue;

                    resolvedUnits++;
                    if (unit->r_AliveState != AliveState.IsAlive ||
                        unit->r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN)
                        continue;

                    assassinCandidates++;
                    bool knownClimbingState = AssassinClimbCancellationPolicy.IsClimbingState(unit->r_AIState);
                    if (knownClimbingState)
                        climbingAssassins++;
                    // During diagnosis the expected 126-129 range is evidence, not a logging gate.
                    // This keeps previously unknown Assassin states observable on the Stop path.
                    LogSnapshot(tribeId, localUnitId, unit);
                }
            }

            if (assassinCandidates > 0)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Extra Features Assassin climb-stop diagnostic summary: tribeId={tribeId}, " +
                    $"tribeGlobalId={tribe->r_GlobalId}, owner={tribe->r_PlayerIdOwner}, aliveState={(int)tribe->r_AliveState}, " +
                    $"declaredUnits={tribe->r_UnitsInGroup}, bitmapMembers={bitmapMembers}, resolvedUnits={resolvedUnits}, " +
                    $"assassinCandidates={assassinCandidates}, knownClimbingStates={climbingAssassins}, " +
                    $"commandArgs=({argument1},{argument2},{argument3}); no state was changed.");
            }
        }

        private void LogSnapshot(int tribeId, int localUnitId, GameUnit* unit)
        {
            byte* raw = (byte*)unit;
            byte ownerOfScaledObject = *(raw + 0x412);
            byte hasAssignedTarget = *(raw + 0x413);
            short heightDifference = *(short*)(raw + 0x414);
            ushort previousFacing = *(ushort*)(raw + 0x416);
            short decayCounter = *(short*)(raw + 0x418);
            ushort assassinTimer = *(ushort*)(raw + 0x41A);

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Extra Features Assassin climb-stop snapshot: tribeId={tribeId}, localUnitId={localUnitId}, " +
                $"globalId={unit->r_GlobalId}, state={unit->r_AIState}, " +
                $"current={FormatTile(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY, unit->r_CurrentPositionTileId)}, " +
                $"previous={FormatTile(unit->r_PreviousTilePositionX, unit->r_PreviousTilePositionY, unit->r_PreviousPositionTileId)}, " +
                $"next={FormatTile(unit->r_NextTilePositionX2, unit->r_NextTilePositionY2, unit->r_NextPositionTileId2)}, " +
                $"target={FormatTile(unit->r_TargetTilePositionX, unit->r_TargetTilePositionY, unit->r_TargetPositionTileId)}, " +
                $"world=({unit->r_CurrentWorldPositionX},{unit->r_CurrentWorldPositionY}), elevation={unit->r_HeightElevation}, " +
                $"animationTimer={unit->r_AnimationTimer}, animationFrame={unit->r_CurrentSpriteAnimationFrame}, " +
                $"animationField70={unit->N00000061}, scaledObjectOwner={ownerOfScaledObject}, assignedTarget={hasAssignedTarget}, " +
                $"heightDifference={heightDifference}, previousFacing={previousFacing}, decayCounter={decayCounter}, assassinTimer={assassinTimer}.");
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

        private static string FormatTile(ushort x, ushort y, uint storedTileId)
        {
            int derivedTileId = -1;
            int height = -1;
            if (x < GameTileManagerAPI.MAX_WIDTH && y < GameTileManagerAPI.MAX_HEIGHT)
            {
                derivedTileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (derivedTileId >= 0 && derivedTileId < AssassinClimbCancellationPolicy.TileCount)
                    height = GameTileManagerAPI.Instance.GetTileHeight(derivedTileId);
            }

            int storedHeight = AssassinClimbCancellationPolicy.IsValidTileId(storedTileId)
                ? GameTileManagerAPI.Instance.GetTileHeight((int)storedTileId)
                : -1;
            return $"({x},{y};storedId={storedTileId};derivedId={derivedTileId};storedHeight={storedHeight};derivedHeight={height})";
        }
    }
}
