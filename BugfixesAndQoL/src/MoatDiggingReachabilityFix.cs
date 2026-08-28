// Feature: Let valid moat-digging orders traverse already completed friendly moats.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.GameGlobals;
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
    internal sealed unsafe class MoatDiggingReachabilityFix : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int FindNearestFriendlyMoatDelegate(
            IntPtr tileManager,
            int playerId,
            int unitId,
            int relationshipMode);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetMoatIdAtTileDelegate(IntPtr tileManager, int tileId);

        private const int DigMoatModePatternRva = 0x8D3C2;
        private const int CursorReachabilityPatternRva = 0x8F3A8;
        private const int GetMoatIdAtTilePatternRva = 0x69560;
        private const int FindNearestFriendlyMoatPatternRva = 0x69D60;
        private const int Command6PrecheckHookRva = 0x120E6C;
        private const int MoatMovementResultHookRva = 0x13F7A9;
        private const int MoatBfsResultHookRva = 0x1964D6;
        private const int PathRegionGridRva = 0x50EC690;
        private const int MoatMovementTargetXRva = 0x6097BE8;
        private const int MoatMovementTargetYRva = 0x6097BEC;
        private const int MoatPathModeRva = 0x60AD6E4;
        private const int CurrentUnitIdRva = 0x9302C4;
        private const int CursorReachabilityHookOffset = 29;
        private const int CursorReachabilityHookLength = 16;
        private const int Command6PrecheckHookLength = 21;
        private const int MoatMovementResultHookLength = 14;
        private const int MoatBfsResultHookLength = 18;
        private const int GameUnitStride = 0x490;
        private const int MoatRecordArrayOffset = 0x1F3EE30;
        private const int MoatRecordCountOffset = 0x2038E30;
        private const int MoatRecordSize = 0x10;
        private const int MoatOwnerOffset = 0x0C;
        private const int MoatReservationOffset = 0x0F;
        private const int MoatReservationIncrement = 20;
        private const int MaximumFunctionalLogEntries = 128;

        private const string DigMoatModePattern =
            "44 39 25 ?? ?? ?? ?? 74 3C 48 8B CE E8 ?? ?? ?? ?? " +
            "85 C0 74 30 B8 01 00 00 00 44 8B E8 89 44 24 54";

        private const string CursorReachabilityPattern =
            "44 8B 0D ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? " +
            "44 8B 05 ?? ?? ?? ?? 41 8B D6 E8 ?? ?? ?? ?? " +
            "85 C0 74 11 44 8B BC 24 C0 00 00 00";

        private const string GetMoatIdAtTilePattern =
            "48 63 C2 0F B7 84 41 ?? ?? ?? ?? C3 CC CC CC";

        private const string FindNearestFriendlyMoatPattern =
            "44 89 44 24 18 89 54 24 10 55 56 57 41 54 41 55 41 56 " +
            "48 83 EC 68 48 8B E9 48 8D 3D ?? ?? ?? ?? 45 8B F1 " +
            "48 8D 87 1C 07 00 00 4D 63 C8 45 33 E4";

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly HookTransaction transaction;
        private readonly int* digMoatMode;
        private readonly int* targetTileX;
        private readonly int* targetTileY;
        private readonly int* currentUnitId;
        private readonly int* moatPathMode;
        private readonly int* moatMovementTargetX;
        private readonly int* moatMovementTargetY;
        private readonly short* pathRegions;
        private HookRef<X64InlineHook> cursorReachabilityHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> command6PrecheckHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatMovementResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatBfsResultHook = new HookRef<X64InlineHook>();
        private GetMoatIdAtTileDelegate getMoatIdAtTile;
        private FindNearestFriendlyMoatDelegate originalFindNearestFriendlyMoat;
        private FindNearestFriendlyMoatDelegate rootedFindNearestFriendlyMoat;
        private NativeDetour findNearestFriendlyMoatDetour;
        private readonly object functionalLogLock = new object();
        private int functionalLogEntryCount;
        private bool functionalLogLimitLogged;
        private bool commandPrecheckFailureLogged;
        private bool directedTargetFailureLogged;
        private bool cursorFailureLogged;
        private bool disposed;

        public MoatDiggingReachabilityFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // The moat record array is not exposed by the Script Extender. Its fixed
            // offsets below are validated for the canonical DLL and must fail closed
            // instead of being guessed for a later game version.
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The moat-digging reachability fix requires the validated CrusaderDE.dll layout.");
            }

            Shared.NativeResolution modeResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                DigMoatModePattern,
                DigMoatModePatternRva,
                referenceHashMatches,
                "DigMoat cursor mode",
                log: null);
            Shared.NativeResolution cursorResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                CursorReachabilityPattern,
                CursorReachabilityPatternRva,
                referenceHashMatches,
                "DigMoat cursor reachability check",
                log: null);
            Shared.NativeResolution moatLookupResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                GetMoatIdAtTilePattern,
                GetMoatIdAtTilePatternRva,
                referenceHashMatches,
                "moat ID lookup by tile",
                log: null);
            Shared.NativeResolution moatSearchResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                FindNearestFriendlyMoatPattern,
                FindNearestFriendlyMoatPatternRva,
                referenceHashMatches,
                "nearest friendly moat search",
                log: null);

            int modeRva = ResolveGlobalRva(
                memory,
                modeResolution.Rva + 3,
                modeResolution.Rva + 7,
                "DigMoat cursor mode");
            int targetYRva = ResolveGlobalRva(
                memory,
                cursorResolution.Rva + 3,
                cursorResolution.Rva + 7,
                "cursor target Y");
            int targetXRva = ResolveGlobalRva(
                memory,
                cursorResolution.Rva + 17,
                cursorResolution.Rva + 21,
                "cursor target X");
            int hookRva = checked(cursorResolution.Rva + CursorReachabilityHookOffset);
            ValidateCursorHookSpan(memory, hookRva);
            ValidateCommand6PrecheckHookSpan(memory);
            ValidateDiagnosticHookSpans(memory);

            digMoatMode = (int*)(libraryBase + unchecked((ulong)modeRva));
            targetTileX = (int*)(libraryBase + unchecked((ulong)targetXRva));
            targetTileY = (int*)(libraryBase + unchecked((ulong)targetYRva));
            currentUnitId = (int*)(libraryBase + CurrentUnitIdRva);
            moatPathMode = (int*)(libraryBase + MoatPathModeRva);
            moatMovementTargetX = (int*)(libraryBase + MoatMovementTargetXRva);
            moatMovementTargetY = (int*)(libraryBase + MoatMovementTargetYRva);
            pathRegions = (short*)(libraryBase + PathRegionGridRva);
            getMoatIdAtTile = Marshal.GetDelegateForFunctionPointer<GetMoatIdAtTileDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)moatLookupResolution.Rva)));

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref cursorReachabilityHook,
                    libraryBase + unchecked((ulong)hookRva),
                    AllowFriendlyPlannedMoatCursor,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: CursorReachabilityHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref command6PrecheckHook,
                    libraryBase + Command6PrecheckHookRva,
                    PreserveFriendlyPlannedMoatCommandTarget,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: Command6PrecheckHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref moatMovementResultHook,
                    libraryBase + MoatMovementResultHookRva,
                    RecordMoatMovementResult,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MoatMovementResultHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref moatBfsResultHook,
                    libraryBase + MoatBfsResultHookRva,
                    RecordMoatBfsResult,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MoatBfsResultHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();

                if (!cursorReachabilityHook.Success || !command6PrecheckHook.Success ||
                    !moatMovementResultHook.Success || !moatBfsResultHook.Success)
                {
                    throw new InvalidOperationException(
                        "The DigMoat functional and diagnostic hooks were not installed atomically.");
                }

                rootedFindNearestFriendlyMoat = DirectCommandedMoatTarget;
                IntPtr moatSearchAddress = (IntPtr)(libraryBase + unchecked((ulong)moatSearchResolution.Rva));
                findNearestFriendlyMoatDetour = new NativeDetour(
                    moatSearchAddress,
                    Marshal.GetFunctionPointerForDelegate(rootedFindNearestFriendlyMoat),
                    new NativeDetourConfig { ManualApply = true });
                originalFindNearestFriendlyMoat =
                    findNearestFriendlyMoatDetour.GenerateTrampoline<FindNearestFriendlyMoatDelegate>();
                findNearestFriendlyMoatDetour.Apply();

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL moat-digging reachability fix installed: " +
                    $"modeMethod={modeResolution.Method}, cursorMethod={cursorResolution.Method}, " +
                    $"lookupMethod={moatLookupResolution.Method}, searchMethod={moatSearchResolution.Method}, " +
                    $"modeRva=0x{modeRva:X}, targetXRva=0x{targetXRva:X}, " +
                    $"targetYRva=0x{targetYRva:X}, hookRva=0x{hookRva:X}, " +
                    $"lookupRva=0x{moatLookupResolution.Rva:X}, searchRva=0x{moatSearchResolution.Rva:X}, " +
                    $"command6PrecheckRva=0x{Command6PrecheckHookRva:X}, " +
                    $"movementResultRva=0x{MoatMovementResultHookRva:X}, " +
                    $"bfsResultRva=0x{MoatBfsResultHookRva:X}.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            findNearestFriendlyMoatDetour?.Dispose();
            findNearestFriendlyMoatDetour = null;
            originalFindNearestFriendlyMoat = null;
            rootedFindNearestFriendlyMoat = null;
            getMoatIdAtTile = null;
            transaction?.Unload();
            transaction?.Dispose();
        }

        private int DirectCommandedMoatTarget(
            IntPtr tileManager,
            int playerId,
            int unitId,
            int relationshipMode)
        {
            try
            {
                if (IsEnabled && relationshipMode == 1 &&
                    GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) &&
                    unit != null &&
                    unit->r_AI_LastIssuedTribeCommand == (ushort)TribeAICommand.DigMoatTileId &&
                    TryGetFriendlyPlannedMoatId(
                        tileManager,
                        playerId,
                        unit->r_ContextTargetTileX,
                        unit->r_ContextTargetTileY,
                        out int commandedMoatId))
                {
                    byte reservationBefore = ReserveMoat(tileManager, commandedMoatId);
                    LogFunctional(
                        $"stage=selection player={playerId} unit={unitId} " +
                        $"target=({unit->r_ContextTargetTileX},{unit->r_ContextTargetTileY}) " +
                        $"moat={commandedMoatId} reservation={reservationBefore}->" +
                        $"{unchecked((byte)(reservationBefore + MoatReservationIncrement))}");

                    // Vanilla reserves every positive result before returning it. Mirroring the
                    // +20 here keeps its later success/release and failure/-20 paths symmetric.
                    return commandedMoatId;
                }
            }
            catch (Exception ex)
            {
                if (!directedTargetFailureLogged)
                {
                    directedTargetFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL directed moat selection failed once; Vanilla selection remains active: {ex}");
                }
            }

            return originalFindNearestFriendlyMoat(tileManager, playerId, unitId, relationshipMode);
        }

        private void PreserveFriendlyPlannedMoatCommandTarget(
            NativePointer<X64SmartCPUContext> context)
        {
            if (!IsEnabled)
                return;

            X64SmartCPUContext* registers = context.Pointer;
            int tribeId = unchecked((int)(uint)registers->R13);
            int targetX = unchecked((int)(uint)registers->R14);
            int targetY = unchecked((int)(uint)registers->R9);

            try
            {
                ulong unitOffset = registers->RDX;
                if (unitOffset % GameUnitStride != 0)
                    return;

                ulong representativeUnitValue = unitOffset / GameUnitStride;
                if (representativeUnitValue == 0 || representativeUnitValue > int.MaxValue)
                    return;

                int representativeUnitId = (int)representativeUnitValue;
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                        representativeUnitId,
                        out GameUnit* representativeUnit) ||
                    representativeUnit == null)
                {
                    return;
                }

                int playerId = representativeUnit->r_ControllableForPlayerId;
                if (!TryGetFriendlyPlannedMoatId(
                    GameTileManagerPointer,
                    playerId,
                    targetX,
                    targetY,
                    out int moatId))
                {
                    return;
                }

                // The overwritten Vanilla test/branch follows this callback. Clearing R8
                // selects its existing direct path, so the ordinary reachability search at
                // 0xE7F60 cannot reject or replace the commanded moat coordinates.
                registers->R8 = 0;
                LogFunctional(
                    $"stage=direct-command tribe={tribeId} player={playerId} " +
                    $"representativeUnit={representativeUnitId} target=({targetX},{targetY}) moat={moatId}");
            }
            catch (Exception ex)
            {
                if (!commandPrecheckFailureLogged)
                {
                    commandPrecheckFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL moat Command-6 validation failed once; " +
                        $"Vanilla command validation remains active: {ex}");
                }
            }
        }

        private void RecordMoatBfsResult(NativePointer<X64SmartCPUContext> context)
        {
            if (!TryGetCurrentMoatUnit(out int unitId, out GameUnit* unit))
                return;

            X64SmartCPUContext* registers = context.Pointer;
            int startRegion = unchecked((int)(uint)registers->R12);
            int targetRegion = unchecked((int)(uint)registers->RBX);
            int bfsResult = unchecked((int)(uint)registers->RAX);
            int targetX = *moatMovementTargetX;
            int targetY = *moatMovementTargetY;
            TileDiagnostic start = GetTileDiagnostic(
                unit->r_CurrentTilePositionX,
                unit->r_CurrentTilePositionY);
            TileDiagnostic target = GetTileDiagnostic(targetX, targetY);

            LogFunctional(
                $"stage=bfs-result unit={unitId} start=({unit->r_CurrentTilePositionX}," +
                $"{unit->r_CurrentTilePositionY}) target=({targetX},{targetY}) " +
                $"startTile={start.TileId} startFlags=0x{start.Flags:X8} startGridRegion={start.Region} " +
                $"targetTile={target.TileId} targetFlags=0x{target.Flags:X8} targetGridRegion={target.Region} " +
                $"startRegion={startRegion} targetRegion={targetRegion} bfsResult={bfsResult} " +
                $"pathMode={*moatPathMode}");
        }

        private void RecordMoatMovementResult(NativePointer<X64SmartCPUContext> context)
        {
            if (!TryGetCurrentMoatUnit(out int unitId, out GameUnit* unit))
                return;

            X64SmartCPUContext* registers = context.Pointer;
            int moveResult = unchecked((int)(uint)registers->RAX);
            int moatId = unchecked((int)(uint)registers->RDI);
            int targetX = *moatMovementTargetX;
            int targetY = *moatMovementTargetY;
            TileDiagnostic start = GetTileDiagnostic(
                unit->r_CurrentTilePositionX,
                unit->r_CurrentTilePositionY);
            TileDiagnostic target = GetTileDiagnostic(targetX, targetY);
            string reservation = TryGetMoatReservation(moatId, out byte reservationValue)
                ? reservationValue.ToString()
                : "invalid";

            LogFunctional(
                $"stage=movement-result unit={unitId} start=({unit->r_CurrentTilePositionX}," +
                $"{unit->r_CurrentTilePositionY}) target=({targetX},{targetY}) " +
                $"storedTarget=({unit->r_ContextTargetTileX},{unit->r_ContextTargetTileY}) " +
                $"startTile={start.TileId} startFlags=0x{start.Flags:X8} startRegion={start.Region} " +
                $"targetTile={target.TileId} targetFlags=0x{target.Flags:X8} targetRegion={target.Region} " +
                $"moveResult={moveResult} pathMode={*moatPathMode} " +
                $"pathState=0x{unit->r_PathPlanStateBitFlags:X4} " +
                $"pathPosition={unit->p_CurrentPathPlanPosition} pathSize={unit->p_PathPlanSize} " +
                $"aiState={unit->r_AIState} moat={moatId} reservation={reservation}");
        }

        private bool TryGetCurrentMoatUnit(out int unitId, out GameUnit* unit)
        {
            unitId = 0;
            unit = null;
            if (!IsEnabled)
                return false;

            unitId = *currentUnitId;
            return unitId > 0 &&
                GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out unit) &&
                unit != null &&
                unit->r_AI_LastIssuedTribeCommand == (ushort)TribeAICommand.DigMoatTileId;
        }

        private TileDiagnostic GetTileDiagnostic(int tileX, int tileY)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsTileInsideMapBounds(tileX, tileY))
                return TileDiagnostic.Invalid;

            int tileId = tileApi.GetTileId(tileX, tileY);
            if (!tileApi.IsValidTileId(tileId))
                return TileDiagnostic.Invalid;

            return new TileDiagnostic(
                tileId,
                unchecked((uint)tileApi.GetTilePropertyFlag(tileId)),
                pathRegions[tileId]);
        }

        private bool TryGetMoatReservation(int moatId, out byte reservation)
        {
            reservation = 0;
            IntPtr tileManager = GameTileManagerPointer;
            if (tileManager == IntPtr.Zero)
                return false;

            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (moatId <= 0 || moatId >= moatCount)
                return false;

            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            reservation = moatRecord[MoatReservationOffset];
            return true;
        }

        private void LogFunctional(string details)
        {
            bool shouldLog;
            bool logLimit;
            lock (functionalLogLock)
            {
                shouldLog = functionalLogEntryCount < MaximumFunctionalLogEntries;
                if (shouldLog)
                    functionalLogEntryCount++;
                logLimit = !shouldLog && !functionalLogLimitLogged;
                if (logLimit)
                    functionalLogLimitLogged = true;
            }

            if (shouldLog)
            {
                Shared.DebugLogHelper.LogDebug(log, $"Bugfixes and QoL MoatCommand {details}.");
            }
            else if (logLimit)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Bugfixes and QoL MoatCommand reached its {MaximumFunctionalLogEntries}-entry limit; " +
                    "further moat-command logs are suppressed.");
            }
        }

        private static byte ReserveMoat(IntPtr tileManager, int moatId)
        {
            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            byte previous = moatRecord[MoatReservationOffset];
            moatRecord[MoatReservationOffset] =
                unchecked((byte)(previous + MoatReservationIncrement));
            return previous;
        }

        private void AllowFriendlyPlannedMoatCursor(NativePointer<X64SmartCPUContext> context)
        {
            X64SmartCPUContext* registers = context.Pointer;
            if (unchecked((uint)registers->RAX) != 0 || !IsEnabled || *digMoatMode == 0)
                return;

            try
            {
                int playerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                if (TryGetFriendlyPlannedMoatId(
                    GameTileManagerPointer,
                    playerId,
                    *targetTileX,
                    *targetTileY,
                    out _))
                    registers->RAX = 1;
            }
            catch (Exception ex)
            {
                if (cursorFailureLogged)
                    return;

                cursorFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL moat cursor validation failed once; Vanilla cursor behavior remains active: {ex}");
            }
        }

        private bool IsEnabled =>
            !disposed && settings.EnableMod && settings.EnableMoatDiggingReachabilityFix;

        private IntPtr GameTileManagerPointer =>
            (IntPtr)GameGlobalsManager.Instance.GameTileManagerVA;

        private bool TryGetFriendlyPlannedMoatId(
            IntPtr tileManager,
            int playerId,
            int tileX,
            int tileY,
            out int moatId)
        {
            moatId = 0;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (tileManager == IntPtr.Zero || !playerApi.IsPlayerIdValid(playerId))
                return false;

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsTileInsideMapBounds(tileX, tileY))
                return false;

            int tileId = tileApi.GetTileId(tileX, tileY);
            if (!tileApi.IsValidTileId(tileId) ||
                !tileApi.HasTilePropertyFlag(tileId, TilePropertyFlag.PlannedMoat))
            {
                return false;
            }

            moatId = getMoatIdAtTile(tileManager, tileId);
            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            // Record zero is Vanilla's dummy/sentinel. Its own owner lookup rejects it,
            // and its nearest-moat search only reserves strictly positive results.
            if (moatId <= 0 || moatId >= moatCount)
            {
                moatId = 0;
                return false;
            }

            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            int moatOwnerId = moatRecord[MoatOwnerOffset];
            if (!playerApi.IsPlayerIdValid(moatOwnerId))
            {
                moatId = 0;
                return false;
            }

            return moatOwnerId == playerId || playerApi.IsPlayerAlliedTo(playerId, moatOwnerId);
        }

        private static int ResolveGlobalRva(
            ReadOnlySpan<byte> memory,
            int displacementRva,
            int nextInstructionRva,
            string label)
        {
            int resolvedRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                displacementRva,
                nextInstructionRva);
            if (resolvedRva < 0 || resolvedRva > memory.Length - sizeof(int))
                throw new InvalidOperationException($"The native {label} global is outside CrusaderDE.dll.");
            return resolvedRva;
        }

        private static void ValidateCursorHookSpan(ReadOnlySpan<byte> memory, int hookRva)
        {
            byte[] expected =
            {
                0x85, 0xC0, 0x74, 0x11,
                0x44, 0x8B, 0xBC, 0x24, 0xC0, 0x00, 0x00, 0x00,
                0x44, 0x8D, 0x6B, 0x02
            };
            if (hookRva < 0 || hookRva > memory.Length - expected.Length ||
                !memory.Slice(hookRva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException("The native DigMoat cursor hook span did not match the validated instructions.");
            }
        }

        private static void ValidateCommand6PrecheckHookSpan(ReadOnlySpan<byte> memory)
        {
            ValidateHookSpan(
                memory,
                Command6PrecheckHookRva,
                new byte[]
                {
                    0x45, 0x85, 0xC0,
                    0x74, 0x54,
                    0x0F, 0xBF, 0x8C, 0x1A, 0x1E, 0x07, 0x00, 0x00,
                    0x0F, 0xBF, 0x84, 0x1A, 0x1C, 0x07, 0x00, 0x00
                },
                "Command-6 precheck");
        }

        private static void ValidateDiagnosticHookSpans(ReadOnlySpan<byte> memory)
        {
            ValidateHookSpan(
                memory,
                MoatMovementResultHookRva,
                new byte[]
                {
                    0x85, 0xC0,
                    0x74, 0x5E,
                    0x48, 0x63, 0x1D, 0x10, 0x0B, 0x7F, 0x00,
                    0x44, 0x8B, 0xC5
                },
                "Moat movement result");
            ValidateHookSpan(
                memory,
                MoatBfsResultHookRva,
                new byte[]
                {
                    0x44, 0x8B, 0xE0,
                    0x3B, 0xC3,
                    0x0F, 0x84, 0xA4, 0x00, 0x00, 0x00,
                    0x0F, 0xBF, 0x8F, 0xB8, 0x09, 0x00, 0x00
                },
                "Moat BFS result");
        }

        private static void ValidateHookSpan(
            ReadOnlySpan<byte> memory,
            int hookRva,
            byte[] expected,
            string label)
        {
            if (hookRva < 0 || hookRva > memory.Length - expected.Length ||
                !memory.Slice(hookRva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    $"The native {label} hook span did not match the validated instructions.");
            }
        }

        private readonly struct TileDiagnostic
        {
            public static readonly TileDiagnostic Invalid = new TileDiagnostic(-1, 0, -1);

            public TileDiagnostic(int tileId, uint flags, int region)
            {
                TileId = tileId;
                Flags = flags;
                Region = region;
            }

            public int TileId { get; }
            public uint Flags { get; }
            public int Region { get; }
        }

    }
}
