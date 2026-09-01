using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace AssassinCombatFix
{
    internal sealed unsafe class AssassinCombatResumeRuntime
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoL.BugfixesAndQoLViewModel settings;
        private HookTransaction transaction;
        private HookRef<X64InlineHook> postCombatPathPreparationHook = new HookRef<X64InlineHook>();
        private int* assassinPathContextFlag;
        private ulong libraryBase;
        private bool tickObserverSubscribed;
        private bool mapActive;

        #region TEMPORARY ASSASSIN_COMBAT_RESUME_DIAGNOSTICS - remove this entire region after validation
        private const int MaximumDiagnosticEventsPerMap = 128;
        private const int MaximumStateTraceEventsPerMap = 256;
        private int diagnosticEventCount;
        private int stateTraceEventCount;
        private readonly Dictionary<int, AssassinTraceSnapshot> trackedAssassins =
            new Dictionary<int, AssassinTraceSnapshot>();

        private sealed class AssassinTraceSnapshot
        {
            public uint GlobalId;
            public ushort AiState;
            public string Signature;
            public int LastLogTick;
        }
        #endregion

        public AssassinCombatResumeRuntime(
            ManualLogSource log,
            BugfixesAndQoL.BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public bool IsInstalled =>
            transaction != null &&
            postCombatPathPreparationHook.Success;

        public void InitializeNative(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool fixedLayoutHashValidated)
        {
            if (IsInstalled)
                return;
            if (!fixedLayoutHashValidated)
                throw new InvalidOperationException(
                    "fixed native layout hash does not match the supported CrusaderDE.dll");
            int lastRequiredRva = AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva;
            if (libraryHandle == IntPtr.Zero || memory.Length < lastRequiredRva + sizeof(int))
            {
                throw new InvalidOperationException(
                    "native module memory does not cover the Assassin path-context flag");
            }

            ValidateNativeContracts(memory);
            assassinPathContextFlag = (int*)IntPtr.Add(
                libraryHandle,
                AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva).ToPointer();
            libraryBase = unchecked((ulong)libraryHandle.ToInt64());

            HookTransaction installedTransaction = null;
            try
            {
                installedTransaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                installedTransaction.AddContextHook(
                    ref postCombatPathPreparationHook,
                    libraryBase + unchecked((ulong)AssassinCombatResumeNativeDefinition.PostCombatPathPreparationHookRva),
                    BeforePostCombatPathRequest,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: AssassinCombatResumeNativeDefinition.PostCombatPathPreparationHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                installedTransaction.Commit();

                if (!postCombatPathPreparationHook.Success)
                {
                    throw new InvalidOperationException(
                        "the exact Assassin post-combat path preparation hook was not installed");
                }

                transaction = installedTransaction;
                GameTimeManagerAPI.Instance.OnTick += ObserveAssassinStates;
                tickObserverSubscribed = true;
                LogInfo(
                    $"installed exact state-106 post-combat path preparation hook at RVA " +
                    $"0x{AssassinCombatResumeNativeDefinition.PostCombatPathPreparationHookRva:X}.");
            }
            catch
            {
                installedTransaction?.Unload();
                installedTransaction?.Dispose();
                if (tickObserverSubscribed)
                {
                    GameTimeManagerAPI.Instance.OnTick -= ObserveAssassinStates;
                    tickObserverSubscribed = false;
                }
                transaction = null;
                postCombatPathPreparationHook = new HookRef<X64InlineHook>();
                assassinPathContextFlag = null;
                libraryBase = 0;
                throw;
            }
        }

        public void BeginMap()
        {
            BeginMap("OnStartMap(Post)");
        }

        private void BeginMap(string reason)
        {
            mapActive = true;
            diagnosticEventCount = 0;
            stateTraceEventCount = 0;
            trackedAssassins.Clear();
            Shared.DebugLogHelper.LogDebug(
                log,
                $"[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC] trace lifecycle started: reason={reason}.");
        }

        public void EndMap()
        {
            bool wasActive = mapActive;
            mapActive = false;
            trackedAssassins.Clear();
            if (wasActive)
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC] trace lifecycle ended: reason=OnUnloadMap(Post).");
            }
        }

        private void BeforePostCombatPathRequest(NativePointer<X64SmartCPUContext> context)
        {
            bool contextChanged = false;
            int previousPathContext = 0;
            try
            {
                bool modEnabled = settings.EnableMod;
                bool improvedPathfindingEnabled = settings.EnableImprovedAssassinPathfinding;
                if (!modEnabled || !improvedPathfindingEnabled)
                    return;

                ulong returnAddress = *(ulong*)(context.Pointer->RSP +
                    AssassinCombatResumeNativeDefinition.PostCombatCallerReturnAddressStackOffset);
                long returnRva = returnAddress >= libraryBase
                    ? unchecked((long)(returnAddress - libraryBase))
                    : -1;
                bool confirmedCombatFinishCaller =
                    AssassinCombatResumePolicy.IsConfirmedCombatFinishCallerRva(returnRva);
                int nativeUnitIndex = unchecked((int)(uint)context.Pointer->RDI);
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                bool unitResolved = AssassinCombatResumePolicy.IsValidNativeUnitIndex(
                    nativeUnitIndex,
                    units.Length);
                AliveState aliveState = unitResolved ? units[nativeUnitIndex].r_AliveState : default;
                eChimps unitType = unitResolved ? units[nativeUnitIndex].r_UnitChimp : default;
                int targetX = unchecked((int)(uint)context.Pointer->R8);
                int targetY = unchecked((int)(uint)context.Pointer->R9);
                bool eligible = AssassinCombatResumePolicy.IsEligiblePostCombatPathRequest(
                    modEnabled,
                    improvedPathfindingEnabled,
                    IsInstalled,
                    confirmedCombatFinishCaller,
                    unitResolved,
                    aliveState,
                    unitType);

                int diagnosticId = BeginRawResumeDiagnostic(unitResolved, aliveState, unitType);
                if (diagnosticId > 0)
                {
                    LogDiagnostic(
                        diagnosticId,
                        $"post-combat-path-entry returnAddress=0x{returnAddress:X16}, " +
                        $"returnRva={FormatRva(returnRva)}, confirmedCombatFinishCaller={confirmedCombatFinishCaller}, " +
                        $"nativeUnitIndex={nativeUnitIndex}, unitCount={units.Length}, resolved={unitResolved}, " +
                        $"aliveState={aliveState}, unitType={unitType}, target={targetX},{targetY}, " +
                        $"eligible={eligible}" +
                        (unitResolved ? $", {DescribeUnit(units[nativeUnitIndex])}" : string.Empty));
                }

                if (!eligible)
                    return;

                previousPathContext = *assassinPathContextFlag;
                bool shouldSetContext = AssassinCombatResumePolicy.ShouldSetAssassinPathContext(
                    eligible,
                    previousPathContext);

                // 0x196280 owns normal cleanup of this one-shot context on both exits.
                if (shouldSetContext)
                {
                    *assassinPathContextFlag = 1;
                    contextChanged = true;
                }

                LogDiagnostic(
                    diagnosticId,
                    $"post-combat-path-context flagBefore={previousPathContext}, " +
                    $"contextSet={shouldSetContext}, flagForRequest={*assassinPathContextFlag}, " +
                    $"weightedBuilderExpected=True");
            }
            catch (Exception ex)
            {
                // The native Vanilla call has not run yet. Restore only this exceptional
                // partial write so a failed callback cannot leak context to another request.
                if (contextChanged && assassinPathContextFlag != null)
                    *assassinPathContextFlag = previousPathContext;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC] post-combat path-context validation failed; " +
                    $"Vanilla behavior remains active: {ex}");
            }
        }

        private void ObserveAssassinStates(int tick)
        {
            if (!settings.EnableMod || !settings.EnableImprovedAssassinPathfinding)
                return;

            if (!mapActive && AssassinCombatResumePolicy.ShouldBeginEditorTrace(
                    mapActive,
                    Shared.GameModeHelper.IsMapEditor()))
            {
                // The map editor creates a playable simulation without raising OnStartMap.
                // Its first simulation tick is the narrow point where unit data is ready.
                BeginMap($"first-map-editor-simulation-tick, tick={tick}");
            }
            if (!mapActive)
                return;

            try
            {
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                for (int nativeUnitIndex = 0; nativeUnitIndex < units.Length; nativeUnitIndex++)
                {
                    GameUnit unit = units[nativeUnitIndex];
                    if (!AssassinCombatResumePolicy.ShouldLogRawResumeDiagnostic(
                            true,
                            unit.r_AliveState,
                            unit.r_UnitChimp))
                    {
                        continue;
                    }

                    string signature = BuildUnitSignature(unit);
                    bool hasTrackedUnit = trackedAssassins.TryGetValue(
                        nativeUnitIndex,
                        out AssassinTraceSnapshot tracked);
                    bool isNewUnit = AssassinCombatResumePolicy.ShouldTreatAsNewTrackedUnit(
                        hasTrackedUnit,
                        hasTrackedUnit ? tracked.GlobalId : 0,
                        unit.r_GlobalId);
                    if (isNewUnit)
                    {
                        tracked = new AssassinTraceSnapshot
                        {
                            GlobalId = unit.r_GlobalId,
                            AiState = unit.r_AIState,
                            Signature = signature,
                            LastLogTick = tick
                        };
                        trackedAssassins[nativeUnitIndex] = tracked;
                    }

                    bool aiStateChanged = !isNewUnit && tracked.AiState != unit.r_AIState;
                    bool signatureChanged = !isNewUnit && !string.Equals(tracked.Signature, signature, StringComparison.Ordinal);
                    int ticksSinceLastLog = tick >= tracked.LastLogTick
                        ? tick - tracked.LastLogTick
                        : int.MaxValue;
                    bool shouldLog = stateTraceEventCount < MaximumStateTraceEventsPerMap &&
                        AssassinCombatResumePolicy.ShouldLogStateTrace(
                            isNewUnit,
                            aiStateChanged,
                            signatureChanged,
                            unit.r_AIState != 0,
                            ticksSinceLastLog);

                    tracked.GlobalId = unit.r_GlobalId;
                    tracked.AiState = unit.r_AIState;
                    tracked.Signature = signature;
                    if (!shouldLog)
                        continue;

                    tracked.LastLogTick = tick;
                    string reason = isNewUnit
                        ? "new-unit"
                        : aiStateChanged
                            ? "state-change"
                            : signatureChanged
                                ? "changed"
                                : "stalled-interval";
                    int traceId = ++stateTraceEventCount;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC trace={traceId}] state-trace " +
                        $"tick={tick}, reason={reason}, nativeUnitIndex={nativeUnitIndex}, {DescribeUnit(unit)}");
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC] state observer failed: {ex}");
            }
        }

        private void ValidateNativeContracts(ReadOnlySpan<byte> memory)
        {
            Shared.NativeResolution state106Callsite = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.State106CombatFinishCallSequence,
                AssassinCombatResumeNativeDefinition.State106CombatFinishCallSequenceRva,
                "Assassin state-106 combat-finish callsite");
            int state106CallRva = state106Callsite.Rva +
                AssassinCombatResumeNativeDefinition.State106CombatFinishCallOffset;
            int state106CallTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                state106CallRva + 1,
                state106CallRva + 5);
            if (state106CallRva != AssassinCombatResumeNativeDefinition.State106CombatFinishCallRva ||
                state106CallTarget != AssassinCombatResumeNativeDefinition.CombatFinishHelperRva)
            {
                throw new InvalidOperationException(
                    "Assassin state 106 no longer calls the audited combat-finish helper");
            }

            Shared.NativeResolution combatFinish = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.CombatFinishHelperSequence,
                AssassinCombatResumeNativeDefinition.CombatFinishHelperSequenceRva,
                "combat-finish resume helper callsite");
            int resumeCallRva = combatFinish.Rva +
                AssassinCombatResumeNativeDefinition.CombatFinishResumeCallOffset;
            int resumeCallTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                resumeCallRva + 1,
                resumeCallRva + 5);
            if (resumeCallRva != AssassinCombatResumeNativeDefinition.CombatFinishResumeCallRva ||
                resumeCallRva + 5 != AssassinCombatResumeNativeDefinition.CombatFinishResumeReturnRva ||
                resumeCallTarget != AssassinCombatResumeNativeDefinition.PostCombatRepathRva)
            {
                throw new InvalidOperationException(
                    "the combat-finish helper no longer calls the audited post-combat repath helper");
            }

            Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatRepathPrologueSequence,
                AssassinCombatResumeNativeDefinition.PostCombatRepathPrologueRva,
                "post-combat repath helper prologue");
            if (AssassinCombatResumeNativeDefinition.PostCombatCallerReturnAddressStackOffset !=
                sizeof(ulong) + 0x30)
            {
                throw new InvalidOperationException(
                    "the post-combat caller return-address stack offset no longer matches its prologue");
            }

            Shared.NativeResolution pathRequest = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequence,
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequenceRva,
                "post-combat saved-state path request");
            int pathRequestCallRva = pathRequest.Rva +
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallOffset;
            int pathRequestTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                pathRequestCallRva + 1,
                pathRequestCallRva + 5);
            int finalizeCallRva = pathRequest.Rva +
                AssassinCombatResumeNativeDefinition.PostCombatFinalizeCallOffset;
            int finalizeTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                finalizeCallRva + 1,
                finalizeCallRva + 5);
            if (pathRequestCallRva != AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallRva ||
                pathRequestTarget != AssassinCombatResumeNativeDefinition.CommonPathRequestRva ||
                finalizeCallRva != AssassinCombatResumeNativeDefinition.PostCombatFinalizeCallRva ||
                finalizeTarget != AssassinCombatResumeNativeDefinition.PostPathRequestRva)
            {
                throw new InvalidOperationException(
                    "the post-combat helper no longer restores the saved request through the audited calls");
            }

            ValidateHookSpan(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatPathPreparationHookRva,
                AssassinCombatResumeNativeDefinition.PostCombatPathPreparationHookBytes,
                "post-combat path preparation");
            ValidateHookSpan(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatStateRestoreRva,
                AssassinCombatResumeNativeDefinition.PostCombatStateRestoreBytes,
                "post-combat state restore");
            if (AssassinCombatResumeNativeDefinition.PostCombatPathPreparationHookLength !=
                    AssassinCombatResumeNativeDefinition.InlineHookMinimumOverwriteLength ||
                pathRequest.Rva + AssassinCombatResumeNativeDefinition.PostCombatPathPreparationHookOffset !=
                    AssassinCombatResumeNativeDefinition.PostCombatPathPreparationHookRva ||
                AssassinCombatResumeNativeDefinition.PostCombatPathPreparationHookRva +
                    AssassinCombatResumeNativeDefinition.PostCombatPathPreparationHookLength !=
                    AssassinCombatResumeNativeDefinition.PostCombatStateRestoreRva ||
                AssassinCombatResumeNativeDefinition.PostCombatStateRestoreRva +
                    AssassinCombatResumeNativeDefinition.PostCombatStateRestoreLength !=
                    AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallRva ||
                !AssassinCombatResumePolicy.IsSafePreparationHookSpan(
                    AssassinCombatResumeNativeDefinition.PostCombatPathPreparationHookRva,
                    AssassinCombatResumeNativeDefinition.PostCombatPathPreparationHookLength,
                    AssassinCombatResumeNativeDefinition.InlineHookMinimumOverwriteLength,
                    AssassinCombatResumeNativeDefinition.PostCombatStateRestoreRva))
            {
                throw new InvalidOperationException(
                    "the post-combat preparation hook no longer ends safely before the native path call");
            }

            Shared.NativeResolution contextRead = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.CommonPathContextReadSequence,
                AssassinCombatResumeNativeDefinition.CommonPathContextReadRva,
                "common path request Assassin-context read");
            int readTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                contextRead.Rva + 3,
                contextRead.Rva + 7);
            if (readTarget != AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva)
                throw new InvalidOperationException("the shared path request no longer reads the audited Assassin flag");

            Shared.NativeResolution successClear = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.CommonPathSuccessClearSequence,
                AssassinCombatResumeNativeDefinition.CommonPathSuccessClearSequenceRva,
                "common path request success-path context clear");
            int successClearInstruction = successClear.Rva +
                AssassinCombatResumeNativeDefinition.CommonPathSuccessFlagClearOffset;
            int successClearTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                successClearInstruction + 3,
                successClearInstruction + 7);
            Shared.NativeResolution failureClear = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.CommonPathFailureClearSequence,
                AssassinCombatResumeNativeDefinition.CommonPathFailureClearRva,
                "common path request failure-path context clear");
            int failureClearTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                failureClear.Rva + 3,
                failureClear.Rva + 7);
            if (successClearTarget != AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva ||
                failureClearTarget != AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva)
            {
                throw new InvalidOperationException(
                    "the common path request no longer clears the Assassin context on both audited exits");
            }

            Shared.NativeResolution dispatcher = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.DispatcherAssassinBranchPattern,
                AssassinCombatResumeNativeDefinition.DispatcherAssassinBranchRva,
                "Assassin path-builder dispatcher branch");
            int assassinBuilderTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                dispatcher.Rva + AssassinCombatResumeNativeDefinition.DispatcherAssassinBuilderCallOffset + 1,
                dispatcher.Rva + AssassinCombatResumeNativeDefinition.DispatcherAssassinBuilderCallOffset + 5);
            if (assassinBuilderTarget != AssassinCombatResumeNativeDefinition.AssassinPathBuilderRva)
                throw new InvalidOperationException("the dispatcher no longer selects the audited Assassin path builder");
        }

        private Shared.NativeResolution Resolve(
            ReadOnlySpan<byte> memory,
            string pattern,
            int expectedRva,
            string description)
        {
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                pattern,
                expectedRva,
                referenceHashMatches: true,
                description,
                log);
            if (resolution.Rva != expectedRva)
                throw new InvalidOperationException($"{description} resolved outside its validated RVA");
            return resolution;
        }

        private static void ValidateHookSpan(
            ReadOnlySpan<byte> memory,
            int hookRva,
            byte[] expectedBytes,
            string description)
        {
            if (hookRva < 0 || hookRva + expectedBytes.Length > memory.Length ||
                !memory.Slice(hookRva, expectedBytes.Length).SequenceEqual(expectedBytes))
            {
                throw new InvalidOperationException(
                    $"the Assassin combat-resume {description} hook span no longer matches audited instructions");
            }
        }

        #region TEMPORARY ASSASSIN_COMBAT_RESUME_DIAGNOSTICS - remove this entire region after validation
        private int BeginRawResumeDiagnostic(
            bool unitResolved,
            AliveState aliveState,
            eChimps unitType)
        {
            if (!AssassinCombatResumePolicy.ShouldLogRawResumeDiagnostic(
                    unitResolved,
                    aliveState,
                    unitType) ||
                diagnosticEventCount >= MaximumDiagnosticEventsPerMap)
            {
                return 0;
            }

            return ++diagnosticEventCount;
        }

        private static string FormatRva(long rva)
        {
            return rva >= 0 ? $"0x{rva:X}" : "outside-module";
        }

        private static string BuildUnitSignature(GameUnit unit)
        {
            return string.Join(
                ":",
                unit.r_AIState,
                unit.r_CurrentTilePositionX,
                unit.r_CurrentTilePositionY,
                unit.r_TargetTilePositionX,
                unit.r_TargetTilePositionY,
                unit.r_TargetTilePositionX2,
                unit.r_TargetTilePositionY2,
                unit.r_AttackMoveToTargetTileX,
                unit.r_AttackMoveToTargetTileY,
                unit.r_ContextTargetTileX,
                unit.r_ContextTargetTileY,
                unit.r_PathPlanRelated1,
                unit.r_PathPlanStateBitFlags,
                unit.r_PathPlanRelated3,
                unit.r_MovingRelevant,
                unit.p_CurrentPathPlanPosition,
                unit.p_PathPlanSize,
                unit.r_CurrentSpeed,
                unit.r_CurrentSpeed2,
                unit.r_AI_LastIssuedTribeCommand,
                unit.r_AI_ContextTargetUnitId,
                unit.r_AI_ContextTargetUnitGlobalId,
                unit.r_AI_ContextTargetBuildingTileId,
                unit.r_ContextCurrentPositionTileId);
        }

        private static string DescribeUnit(GameUnit unit)
        {
            return $"globalId={unit.r_GlobalId}, aiState={unit.r_AIState}, " +
                $"position={unit.r_CurrentTilePositionX},{unit.r_CurrentTilePositionY}, " +
                $"target={unit.r_TargetTilePositionX},{unit.r_TargetTilePositionY}, " +
                $"secondaryTarget={unit.r_TargetTilePositionX2},{unit.r_TargetTilePositionY2}, " +
                $"attackMoveTarget={unit.r_AttackMoveToTargetTileX},{unit.r_AttackMoveToTargetTileY}, " +
                $"contextTarget={unit.r_ContextTargetTileX},{unit.r_ContextTargetTileY}, " +
                $"contextUnit={unit.r_AI_ContextTargetUnitId}/{unit.r_AI_ContextTargetUnitGlobalId}, " +
                $"contextBuildingTile={unit.r_AI_ContextTargetBuildingTileId}, " +
                $"contextCurrentTile={unit.r_ContextCurrentPositionTileId}, " +
                $"lastCommand={unit.r_AI_LastIssuedTribeCommand}, " +
                $"pathRelated1={unit.r_PathPlanRelated1}, pathFlags={unit.r_PathPlanStateBitFlags}, " +
                $"pathRelated3={unit.r_PathPlanRelated3}, moving={unit.r_MovingRelevant}, " +
                $"pathPosition={unit.p_CurrentPathPlanPosition}, pathLength={unit.p_PathPlanSize}, " +
                $"speed={unit.r_CurrentSpeed}/{unit.r_CurrentSpeed2}";
        }

        private void LogDiagnostic(int diagnosticId, string message)
        {
            if (diagnosticId <= 0)
                return;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC event={diagnosticId}] {message}");
        }
        #endregion

        private void LogInfo(string message)
        {
            Shared.DebugLogHelper.LogInfo(log, $"Assassin Combat Fix {message}");
        }
    }
}
