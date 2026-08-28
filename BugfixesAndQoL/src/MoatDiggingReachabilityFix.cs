// Feature: Let valid moat-digging orders traverse already completed friendly moats.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
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
        private const int Command6EntryHookRva = 0x120EC5;
        private const int EligibleUnitStoredHookRva = 0x120FCC;
        private const int MoatMovementCoordinatesHookRva = 0x13F783;
        private const int MoatMovementCallHookRva = 0x13F7A4;
        private const int MoatMovementSuccessHookRva = 0x13F7FD;
        private const int MoatCommandResetHookRva = 0x13F83E;
        private const int CurrentUnitIdRva = 0x9302C4;
        private const int MoatPathModeRva = 0x60AD6E4;
        private const int CursorReachabilityHookOffset = 29;
        private const int CursorReachabilityHookLength = 12;
        private const int Command6EntryHookLength = 5;
        private const int EligibleUnitStoredHookLength = 7;
        private const int MoatMovementCoordinatesHookLength = 7;
        private const int MoatMovementCallHookLength = 5;
        private const int MoatMovementSuccessHookLength = 9;
        private const int MoatCommandResetHookLength = 8;
        private const int MoatRecordArrayOffset = 0x1F3EE30;
        private const int MoatRecordCountOffset = 0x2038E30;
        private const int MoatRecordSize = 0x10;
        private const int MoatOwnerOffset = 0x0C;
        private const int MoatReservationOffset = 0x0F;
        private const int MoatReservationIncrement = 20;
        private const int Command6TargetYStackOffset = 0xF0;
        private const int MaximumDiagnosticEntries = 256;

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
        private HookRef<X64InlineHook> cursorReachabilityHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> command6EntryHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> eligibleUnitStoredHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatMovementCoordinatesHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatMovementCallHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatMovementSuccessHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatCommandResetHook = new HookRef<X64InlineHook>();
        private GetMoatIdAtTileDelegate getMoatIdAtTile;
        private FindNearestFriendlyMoatDelegate originalFindNearestFriendlyMoat;
        private FindNearestFriendlyMoatDelegate rootedFindNearestFriendlyMoat;
        private NativeDetour findNearestFriendlyMoatDetour;
        private readonly object diagnosticLock = new object();
        private readonly Dictionary<int, DiagnosticCommand> diagnosticCommandsByUnit =
            new Dictionary<int, DiagnosticCommand>();
        private DiagnosticCommand activeDiagnosticCommand;
        private int nextDiagnosticCommandId;
        private int diagnosticEntryCount;
        private bool diagnosticLimitLogged;
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
            ValidateHookSpan(memory, hookRva);
            ValidateDiagnosticHookSpans(memory);

            digMoatMode = (int*)(libraryBase + unchecked((ulong)modeRva));
            targetTileX = (int*)(libraryBase + unchecked((ulong)targetXRva));
            targetTileY = (int*)(libraryBase + unchecked((ulong)targetYRva));
            currentUnitId = (int*)(libraryBase + CurrentUnitIdRva);
            moatPathMode = (int*)(libraryBase + MoatPathModeRva);

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
                    ref command6EntryHook,
                    libraryBase + Command6EntryHookRva,
                    RecordAcceptedCommand6,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: Command6EntryHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref eligibleUnitStoredHook,
                    libraryBase + EligibleUnitStoredHookRva,
                    RecordEligibleUnitCommand,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: EligibleUnitStoredHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref moatMovementCoordinatesHook,
                    libraryBase + MoatMovementCoordinatesHookRva,
                    RecordGeneratedMovementCoordinates,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MoatMovementCoordinatesHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref moatMovementCallHook,
                    libraryBase + MoatMovementCallHookRva,
                    RecordMovementResult,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MoatMovementCallHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                transaction.AddContextHook(
                    ref moatMovementSuccessHook,
                    libraryBase + MoatMovementSuccessHookRva,
                    RecordMovementSuccessState,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MoatMovementSuccessHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                transaction.AddContextHook(
                    ref moatCommandResetHook,
                    libraryBase + MoatCommandResetHookRva,
                    RecordCommandResetState,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MoatCommandResetHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                transaction.Commit();

                if (!cursorReachabilityHook.Success || !command6EntryHook.Success ||
                    !eligibleUnitStoredHook.Success || !moatMovementCoordinatesHook.Success ||
                    !moatMovementCallHook.Success || !moatMovementSuccessHook.Success ||
                    !moatCommandResetHook.Success)
                {
                    throw new InvalidOperationException(
                        "The DigMoat cursor and diagnostic hooks were not installed atomically.");
                }

                getMoatIdAtTile = Marshal.GetDelegateForFunctionPointer<GetMoatIdAtTileDelegate>(
                    (IntPtr)(libraryBase + unchecked((ulong)moatLookupResolution.Rva)));
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
                    $"command6Rva=0x{Command6EntryHookRva:X}, unitStoredRva=0x{EligibleUnitStoredHookRva:X}, " +
                    $"coordinatesRva=0x{MoatMovementCoordinatesHookRva:X}, " +
                    $"movementCallRva=0x{MoatMovementCallHookRva:X}, " +
                    $"successRva=0x{MoatMovementSuccessHookRva:X}, resetRva=0x{MoatCommandResetHookRva:X}.");
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
                    LogMoatSelection(
                        unitId,
                        playerId,
                        relationshipMode,
                        commandedMoatId,
                        "direct",
                        reservationBefore,
                        unchecked((byte)(reservationBefore + MoatReservationIncrement)));

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

            int vanillaResult = originalFindNearestFriendlyMoat(
                tileManager, playerId, unitId, relationshipMode);
            LogMoatSelection(
                unitId,
                playerId,
                relationshipMode,
                vanillaResult,
                "vanilla",
                null,
                null);
            return vanillaResult;
        }

        private void RecordAcceptedCommand6(NativePointer<X64SmartCPUContext> context)
        {
            if (!IsEnabled)
                return;

            X64SmartCPUContext* registers = context.Pointer;
            int tribeId = unchecked((int)(uint)registers->R13);
            int targetX = unchecked((int)(uint)registers->R14);
            int targetY = *(int*)(registers->RSP + Command6TargetYStackOffset);
            DiagnosticCommand command;
            lock (diagnosticLock)
            {
                command = new DiagnosticCommand(
                    ++nextDiagnosticCommandId,
                    tribeId,
                    targetX,
                    targetY);
                activeDiagnosticCommand = command;
            }

            LogDiagnostic(
                command,
                $"stage=accepted tribe={tribeId} requested=({targetX},{targetY})");
        }

        private void RecordEligibleUnitCommand(NativePointer<X64SmartCPUContext> context)
        {
            if (!IsEnabled)
                return;

            X64SmartCPUContext* registers = context.Pointer;
            int unitId = unchecked((int)(uint)registers->RDX);
            DiagnosticCommand command;
            lock (diagnosticLock)
            {
                command = activeDiagnosticCommand;
                if (command != null)
                    diagnosticCommandsByUnit[unitId] = command;
            }

            if (command == null)
                return;

            if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) && unit != null)
            {
                LogDiagnostic(
                    command,
                    $"stage=eligible-unit unit={unitId} storedCommand={unit->r_AI_LastIssuedTribeCommand} " +
                    $"storedTarget=({unit->r_ContextTargetTileX},{unit->r_ContextTargetTileY}) " +
                    $"aiState={unit->r_AIState}");
            }
            else
            {
                LogDiagnostic(command, $"stage=eligible-unit unit={unitId} unitLookup=failed");
            }
        }

        private void RecordGeneratedMovementCoordinates(NativePointer<X64SmartCPUContext> context)
        {
            if (!TryGetCurrentDiagnostic(out int unitId, out DiagnosticCommand command))
                return;

            X64SmartCPUContext* registers = context.Pointer;
            LogDiagnostic(
                command,
                $"stage=coordinates unit={unitId} moat={unchecked((int)registers->RDI)} " +
                $"generated=({*targetTileX},{*targetTileY}) source=0x6AF60");
        }

        private void RecordMovementResult(NativePointer<X64SmartCPUContext> context)
        {
            if (!TryGetCurrentDiagnostic(out int unitId, out DiagnosticCommand command))
                return;

            X64SmartCPUContext* registers = context.Pointer;
            string unitState = "unitLookup=failed";
            if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) && unit != null)
            {
                unitState =
                    $"start=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                    $"aiState={unit->r_AIState} storedCommand={unit->r_AI_LastIssuedTribeCommand}";
            }

            LogDiagnostic(
                command,
                $"stage=movement-return unit={unitId} pathMode={*moatPathMode} " +
                $"target=({*targetTileX},{*targetTileY}) result={unchecked((int)(uint)registers->RAX)} " +
                unitState);
        }

        private void RecordMovementSuccessState(NativePointer<X64SmartCPUContext> context) =>
            RecordFinalUnitState("success", removeCommand: true);

        private void RecordCommandResetState(NativePointer<X64SmartCPUContext> context) =>
            RecordFinalUnitState("reset", removeCommand: true);

        private void RecordFinalUnitState(string stage, bool removeCommand)
        {
            if (!TryGetCurrentDiagnostic(out int unitId, out DiagnosticCommand command))
                return;

            if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) && unit != null)
            {
                LogDiagnostic(
                    command,
                    $"stage={stage} unit={unitId} aiState={unit->r_AIState} " +
                    $"storedCommand={unit->r_AI_LastIssuedTribeCommand} " +
                    $"storedTarget=({unit->r_ContextTargetTileX},{unit->r_ContextTargetTileY})");
            }
            else
            {
                LogDiagnostic(command, $"stage={stage} unit={unitId} unitLookup=failed");
            }

            if (removeCommand)
            {
                lock (diagnosticLock)
                    diagnosticCommandsByUnit.Remove(unitId);
            }
        }

        private bool TryGetCurrentDiagnostic(out int unitId, out DiagnosticCommand command)
        {
            unitId = *currentUnitId;
            lock (diagnosticLock)
                return diagnosticCommandsByUnit.TryGetValue(unitId, out command);
        }

        private void LogMoatSelection(
            int unitId,
            int playerId,
            int relationshipMode,
            int moatId,
            string source,
            byte? reservationBefore,
            byte? reservationAfter)
        {
            DiagnosticCommand command;
            lock (diagnosticLock)
            {
                if (!diagnosticCommandsByUnit.TryGetValue(unitId, out command))
                    return;
            }

            string reservation = reservationBefore.HasValue
                ? $" reservation={reservationBefore.Value}->{reservationAfter.Value}"
                : string.Empty;
            LogDiagnostic(
                command,
                $"stage=selection unit={unitId} player={playerId} relationshipMode={relationshipMode} " +
                $"result={moatId} source={source}{reservation}");
        }

        private void LogDiagnostic(DiagnosticCommand command, string details)
        {
            bool shouldLog;
            bool logLimit;
            lock (diagnosticLock)
            {
                shouldLog = diagnosticEntryCount < MaximumDiagnosticEntries;
                if (shouldLog)
                    diagnosticEntryCount++;
                logLimit = !shouldLog && !diagnosticLimitLogged;
                if (logLimit)
                    diagnosticLimitLogged = true;
            }

            if (shouldLog)
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL MoatDiag command={command.Id} {details} " +
                    $"requested=({command.TargetX},{command.TargetY}).");
            }
            else if (logLimit)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Bugfixes and QoL MoatDiag reached its {MaximumDiagnosticEntries}-entry limit; " +
                    "further moat-command diagnostics are suppressed.");
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

        private static void ValidateHookSpan(ReadOnlySpan<byte> memory, int hookRva)
        {
            byte[] expected =
            {
                0x85, 0xC0, 0x74, 0x11,
                0x44, 0x8B, 0xBC, 0x24, 0xC0, 0x00, 0x00, 0x00
            };
            if (hookRva < 0 || hookRva > memory.Length - expected.Length ||
                !memory.Slice(hookRva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException("The native DigMoat cursor hook span did not match the validated instructions.");
            }
        }

        private static void ValidateDiagnosticHookSpans(ReadOnlySpan<byte> memory)
        {
            ValidateHookSpan(
                memory,
                Command6EntryHookRva,
                new byte[] { 0xB8, 0x01, 0x00, 0x00, 0x00 },
                "Command-6 entry");
            ValidateHookSpan(
                memory,
                EligibleUnitStoredHookRva,
                new byte[] { 0x89, 0xB4, 0x03, 0x00, 0x0A, 0x00, 0x00 },
                "eligible Command-6 unit");
            ValidateHookSpan(
                memory,
                MoatMovementCoordinatesHookRva,
                new byte[] { 0x44, 0x8B, 0x0D, 0x62, 0x84, 0xF5, 0x05 },
                "moat movement coordinates");
            ValidateHookSpan(
                memory,
                MoatMovementCallHookRva,
                new byte[] { 0xE8, 0xD7, 0x6A, 0x05, 0x00 },
                "moat movement call");
            ValidateHookSpan(
                memory,
                MoatMovementSuccessHookRva,
                new byte[] { 0x66, 0x42, 0x89, 0x84, 0x31, 0x18, 0x09, 0x00, 0x00 },
                "moat movement success state");
            ValidateHookSpan(
                memory,
                MoatCommandResetHookRva,
                new byte[] { 0x66, 0x44, 0x89, 0x00, 0x41, 0x0F, 0xB7, 0xC0 },
                "moat command reset");
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

        private sealed class DiagnosticCommand
        {
            public DiagnosticCommand(int id, int tribeId, int targetX, int targetY)
            {
                Id = id;
                TribeId = tribeId;
                TargetX = targetX;
                TargetY = targetY;
            }

            public int Id { get; }
            public int TribeId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
        }
    }
}
