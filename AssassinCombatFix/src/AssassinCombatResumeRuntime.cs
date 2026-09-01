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
        private HookRef<X64InlineHook> shortResumeDecisionHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> fullRepathResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> state107TargetResultHook = new HookRef<X64InlineHook>();
        private int* assassinPathContextFlag;
        private int* currentContextUnitIndex;
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

        [ThreadStatic]
        private static Stack<PendingDiagnostic> pendingDiagnostics;

        private sealed class PendingDiagnostic
        {
            public int Id;
            public int NativeUnitIndex;
            public long ReturnRva;
            public int ShortcutResult;
            public int PreviousPathContext;
        }

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
            shortResumeDecisionHook.Success &&
            fullRepathResultHook.Success &&
            state107TargetResultHook.Success;

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
            int lastRequiredRva = Math.Max(
                AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva,
                AssassinCombatResumeNativeDefinition.CurrentContextUnitIndexRva);
            if (libraryHandle == IntPtr.Zero || memory.Length < lastRequiredRva + sizeof(int))
            {
                throw new InvalidOperationException(
                    "native module memory does not cover the Assassin path-context flag");
            }

            ValidateNativeContracts(memory);
            assassinPathContextFlag = (int*)IntPtr.Add(
                libraryHandle,
                AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva).ToPointer();
            currentContextUnitIndex = (int*)IntPtr.Add(
                libraryHandle,
                AssassinCombatResumeNativeDefinition.CurrentContextUnitIndexRva).ToPointer();
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
                    ref shortResumeDecisionHook,
                    libraryBase + unchecked((ulong)AssassinCombatResumeNativeDefinition.ShortResumeDecisionHookRva),
                    AfterShortResumeAttempt,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: AssassinCombatResumeNativeDefinition.ShortResumeDecisionHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                installedTransaction.AddContextHook(
                    ref fullRepathResultHook,
                    libraryBase + unchecked((ulong)AssassinCombatResumeNativeDefinition.FullRepathResultHookRva),
                    BeforeVanillaSuccessResult,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: AssassinCombatResumeNativeDefinition.FullRepathResultHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                installedTransaction.AddContextHook(
                    ref state107TargetResultHook,
                    libraryBase + unchecked((ulong)AssassinCombatResumeNativeDefinition.State107TargetResultHookRva),
                    AfterState107TargetCheck,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: AssassinCombatResumeNativeDefinition.State107TargetResultHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                installedTransaction.Commit();

                if (!shortResumeDecisionHook.Success ||
                    !fullRepathResultHook.Success ||
                    !state107TargetResultHook.Success)
                {
                    throw new InvalidOperationException(
                        "one or both Assassin combat-resume hooks were not installed");
                }

                transaction = installedTransaction;
                GameTimeManagerAPI.Instance.OnTick += ObserveAssassinStates;
                tickObserverSubscribed = true;
                LogInfo(
                    $"installed exact combat-resume hooks at RVAs " +
                    $"0x{AssassinCombatResumeNativeDefinition.ShortResumeDecisionHookRva:X} and " +
                    $"0x{AssassinCombatResumeNativeDefinition.FullRepathResultHookRva:X}, plus the passive " +
                    $"state-107 target-check hook at 0x{AssassinCombatResumeNativeDefinition.State107TargetResultHookRva:X}.");
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
                shortResumeDecisionHook = new HookRef<X64InlineHook>();
                fullRepathResultHook = new HookRef<X64InlineHook>();
                state107TargetResultHook = new HookRef<X64InlineHook>();
                assassinPathContextFlag = null;
                currentContextUnitIndex = null;
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
            pendingDiagnostics?.Clear();
            Shared.DebugLogHelper.LogDebug(
                log,
                $"[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC] trace lifecycle started: reason={reason}.");
        }

        public void EndMap()
        {
            bool wasActive = mapActive;
            mapActive = false;
            trackedAssassins.Clear();
            pendingDiagnostics?.Clear();
            if (wasActive)
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC] trace lifecycle ended: reason=OnUnloadMap(Post).");
            }
        }

        private void AfterShortResumeAttempt(NativePointer<X64SmartCPUContext> context)
        {
            bool contextChanged = false;
            bool diagnosticPushed = false;
            int previousPathContext = 0;
            ulong originalRax = context.Pointer->RAX;
            try
            {
                bool modEnabled = settings.EnableMod;
                bool improvedPathfindingEnabled = settings.EnableImprovedAssassinPathfinding;
                if (!modEnabled || !improvedPathfindingEnabled)
                    return;

                ulong stack50 = *(ulong*)(context.Pointer->RSP + 0x50);
                ulong stack58 = *(ulong*)(context.Pointer->RSP + 0x58);
                ulong stack60 = *(ulong*)(context.Pointer->RSP + 0x60);
                ulong returnAddress = stack58;
                long returnRva = returnAddress >= libraryBase
                    ? unchecked((long)(returnAddress - libraryBase))
                    : -1;
                bool knownCombatCaller =
                    AssassinCombatResumePolicy.IsKnownAssassinCombatReturnRva(returnRva);
                int nativeUnitIndex = unchecked((int)(uint)context.Pointer->RBX);
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                bool unitResolved = AssassinCombatResumePolicy.IsValidNativeUnitIndex(
                    nativeUnitIndex,
                    units.Length);
                AliveState aliveState = unitResolved ? units[nativeUnitIndex].r_AliveState : default;
                eChimps unitType = unitResolved ? units[nativeUnitIndex].r_UnitChimp : default;
                int shortcutResult = unchecked((int)(uint)context.Pointer->RAX);
                bool eligible = AssassinCombatResumePolicy.ShouldForceFullRepath(
                    modEnabled,
                    improvedPathfindingEnabled,
                    IsInstalled,
                    knownCombatCaller,
                    unitResolved,
                    aliveState,
                    unitType);

                int diagnosticId = BeginRawResumeDiagnostic(
                    unitResolved,
                    aliveState,
                    unitType);
                if (diagnosticId > 0)
                {
                    GameUnit unit = units[nativeUnitIndex];
                    LogDiagnostic(
                        diagnosticId,
                        $"raw-resume-entry returnAddress=0x{returnAddress:X16}, returnRva={FormatRva(returnRva)}, " +
                        $"stack50=0x{stack50:X16}, stack58=0x{stack58:X16}, stack60=0x{stack60:X16}, " +
                        $"knownCombatCaller={knownCombatCaller}, nativeUnitIndex={nativeUnitIndex}, " +
                        $"unitCount={units.Length}, resolved={unitResolved}, aliveState={aliveState}, " +
                        $"unitType={unitType}, shortcutResult={shortcutResult}, eligible={eligible}, " +
                        DescribeUnit(unit));
                }

                if (!eligible)
                    return;

                previousPathContext = *assassinPathContextFlag;
                Stack<PendingDiagnostic> stack = pendingDiagnostics ??
                    (pendingDiagnostics = new Stack<PendingDiagnostic>());
                stack.Push(new PendingDiagnostic
                {
                    Id = diagnosticId,
                    NativeUnitIndex = nativeUnitIndex,
                    ReturnRva = returnRva,
                    ShortcutResult = shortcutResult,
                    PreviousPathContext = previousPathContext
                });
                diagnosticPushed = true;

                // A combat interruption can leave a climbing path that the local
                // shortcut accepts but cannot restart. Force Vanilla's full request.
                *assassinPathContextFlag = 1;
                contextChanged = true;
                context.Pointer->RAX = 0;

                LogDiagnostic(
                    diagnosticId,
                    $"full-repath-forced shortcutResult={shortcutResult}, " +
                    $"flagBefore={previousPathContext}, flagForRequest={*assassinPathContextFlag}");
            }
            catch (Exception ex)
            {
                if (contextChanged && assassinPathContextFlag != null)
                    *assassinPathContextFlag = previousPathContext;
                context.Pointer->RAX = originalRax;
                if (diagnosticPushed && pendingDiagnostics != null && pendingDiagnostics.Count > 0)
                    pendingDiagnostics.Pop();
                Shared.DebugLogHelper.LogError(
                    log,
                    $"[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC] resume-decision validation failed; " +
                    $"Vanilla behavior remains active: {ex}");
            }
        }

        private void BeforeVanillaSuccessResult(NativePointer<X64SmartCPUContext> context)
        {
            Stack<PendingDiagnostic> stack = pendingDiagnostics;
            if (stack == null || stack.Count == 0)
                return;

            PendingDiagnostic diagnostic = stack.Pop();
            try
            {
                int fullRepathResult = unchecked((int)(uint)context.Pointer->RAX);
                int flagAfterVanilla = *assassinPathContextFlag;
                LogDiagnostic(
                    diagnostic.Id,
                    $"full-repath-result returnRva=0x{diagnostic.ReturnRva:X}, " +
                    $"nativeUnitIndex={diagnostic.NativeUnitIndex}, shortcutResult={diagnostic.ShortcutResult}, " +
                    $"fullRepathCalls=1, result={fullRepathResult}, " +
                    $"flagBefore={diagnostic.PreviousPathContext}, flagAfterVanilla={flagAfterVanilla}");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC] full-repath result logging failed: {ex}");
            }
        }

        private void AfterState107TargetCheck(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                if (!settings.EnableMod || !settings.EnableImprovedAssassinPathfinding ||
                    currentContextUnitIndex == null)
                {
                    return;
                }

                int nativeUnitIndex = *currentContextUnitIndex;
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                if (!AssassinCombatResumePolicy.IsValidNativeUnitIndex(nativeUnitIndex, units.Length))
                    return;

                GameUnit unit = units[nativeUnitIndex];
                if (!AssassinCombatResumePolicy.ShouldLogRawResumeDiagnostic(
                        true,
                        unit.r_AliveState,
                        unit.r_UnitChimp))
                {
                    return;
                }

                int diagnosticId = BeginRawResumeDiagnostic(true, unit.r_AliveState, unit.r_UnitChimp);
                LogDiagnostic(
                    diagnosticId,
                    $"state107-target-check nativeUnitIndex={nativeUnitIndex}, " +
                    $"result={unchecked((int)(uint)context.Pointer->RAX)}, {DescribeUnit(unit)}");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC] state-107 target-check logging failed: {ex}");
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
            Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.GeneralResumePrologueSequence,
                AssassinCombatResumeNativeDefinition.GeneralResumePrologueRva,
                "general unit-order resume prologue");
            if (AssassinCombatResumeNativeDefinition.GeneralResumeReturnAddressStackOffset !=
                (5 * sizeof(ulong)) + 0x30)
            {
                throw new InvalidOperationException(
                    "the audited resume return-address stack offset no longer matches its prologue");
            }

            ValidateCombatCallsite(
                memory,
                AssassinCombatResumeNativeDefinition.AssassinCombatResumeCall1Sequence,
                AssassinCombatResumeNativeDefinition.AssassinCombatResumeCall1SequenceRva,
                AssassinCombatResumeNativeDefinition.AssassinCombatResumeCall1Offset,
                AssassinCombatResumeNativeDefinition.AssassinCombatResumeCall1Rva,
                AssassinCombatResumeNativeDefinition.AssassinCombatResumeReturn1Rva,
                "first Assassin state-107 resume callsite");
            ValidateCombatCallsite(
                memory,
                AssassinCombatResumeNativeDefinition.AssassinCombatResumeCall2Sequence,
                AssassinCombatResumeNativeDefinition.AssassinCombatResumeCall2SequenceRva,
                AssassinCombatResumeNativeDefinition.AssassinCombatResumeCall2Offset,
                AssassinCombatResumeNativeDefinition.AssassinCombatResumeCall2Rva,
                AssassinCombatResumeNativeDefinition.AssassinCombatResumeReturn2Rva,
                "second Assassin state-107 resume callsite");
            Shared.NativeResolution contextIndexRead = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.AssassinCombatResumeCall1Sequence,
                AssassinCombatResumeNativeDefinition.AssassinCombatResumeCall1SequenceRva,
                "state-107 current-unit index read");
            int contextIndexTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                contextIndexRead.Rva + 3,
                contextIndexRead.Rva + 7);
            if (contextIndexTarget != AssassinCombatResumeNativeDefinition.CurrentContextUnitIndexRva)
            {
                throw new InvalidOperationException(
                    "the state-107 handler no longer reads the audited current-unit index global");
            }

            Shared.NativeResolution targetCheck = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.State107TargetCheckSequence,
                AssassinCombatResumeNativeDefinition.State107TargetCheckSequenceRva,
                "Assassin state-107 target check");
            int targetCheckCallRva = targetCheck.Rva +
                AssassinCombatResumeNativeDefinition.State107TargetCheckCallOffset;
            int targetCheckTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                targetCheckCallRva + 1,
                targetCheckCallRva + 5);
            if (targetCheckCallRva != AssassinCombatResumeNativeDefinition.State107TargetCheckCallRva ||
                targetCheckTarget != AssassinCombatResumeNativeDefinition.State107TargetCheckRva ||
                targetCheck.Rva + AssassinCombatResumeNativeDefinition.State107TargetResultHookOffset !=
                    AssassinCombatResumeNativeDefinition.State107TargetResultHookRva)
            {
                throw new InvalidOperationException(
                    "the Assassin state-107 target check no longer matches the audited call and result branch");
            }
            ValidateHookSpan(
                memory,
                AssassinCombatResumeNativeDefinition.State107TargetResultHookRva,
                AssassinCombatResumeNativeDefinition.State107TargetResultHookBytes,
                "state-107 target-result diagnostic");
            if (AssassinCombatResumeNativeDefinition.State107TargetCheckCallRva + 5 >
                AssassinCombatResumeNativeDefinition.State107TargetResultHookRva)
            {
                throw new InvalidOperationException(
                    "the state-107 diagnostic hook overlaps its native target-check call");
            }

            Shared.NativeResolution resumeDecision = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.ResumeDecisionSequence,
                AssassinCombatResumeNativeDefinition.ResumeDecisionSequenceRva,
                "general resume shortcut and full-repath branch");
            int shortResumeTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                resumeDecision.Rva + AssassinCombatResumeNativeDefinition.ShortResumeCallOffset + 1,
                resumeDecision.Rva + AssassinCombatResumeNativeDefinition.ShortResumeCallOffset + 5);
            int fullRepathTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                resumeDecision.Rva + AssassinCombatResumeNativeDefinition.FullRepathCallOffset + 1,
                resumeDecision.Rva + AssassinCombatResumeNativeDefinition.FullRepathCallOffset + 5);
            if (shortResumeTarget != AssassinCombatResumeNativeDefinition.ShortResumeRva ||
                fullRepathTarget != AssassinCombatResumeNativeDefinition.CommonPathRequestRva ||
                resumeDecision.Rva + AssassinCombatResumeNativeDefinition.FullRepathCallOffset !=
                    AssassinCombatResumeNativeDefinition.FullRepathCallRva)
            {
                throw new InvalidOperationException(
                    "the general resume helper no longer follows the audited shortcut/full-repath flow");
            }

            ValidateHookSpan(
                memory,
                AssassinCombatResumeNativeDefinition.ShortResumeDecisionHookRva,
                AssassinCombatResumeNativeDefinition.ShortResumeDecisionHookBytes,
                "short-resume decision");
            ValidateHookSpan(
                memory,
                AssassinCombatResumeNativeDefinition.FullRepathResultHookRva,
                AssassinCombatResumeNativeDefinition.FullRepathResultHookBytes,
                "full-repath result");
            if (AssassinCombatResumeNativeDefinition.ShortResumeDecisionHookRva !=
                    resumeDecision.Rva + AssassinCombatResumeNativeDefinition.ShortResumeDecisionHookOffset ||
                AssassinCombatResumeNativeDefinition.FullRepathResultHookRva !=
                    resumeDecision.Rva + AssassinCombatResumeNativeDefinition.FullRepathResultHookOffset ||
                AssassinCombatResumeNativeDefinition.ShortResumeDecisionHookRva +
                    AssassinCombatResumeNativeDefinition.ShortResumeDecisionHookLength >
                    AssassinCombatResumeNativeDefinition.FullRepathCallRva ||
                AssassinCombatResumeNativeDefinition.FullRepathCallRva + 5 >
                    AssassinCombatResumeNativeDefinition.FullRepathResultHookRva)
            {
                throw new InvalidOperationException(
                    "the Assassin combat-resume hooks overlap the native full-repath call");
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

        private void ValidateCombatCallsite(
            ReadOnlySpan<byte> memory,
            string pattern,
            int sequenceRva,
            int callOffset,
            int expectedCallRva,
            int expectedReturnRva,
            string description)
        {
            Shared.NativeResolution callsite = Resolve(memory, pattern, sequenceRva, description);
            int callRva = callsite.Rva + callOffset;
            int target = Shared.NativePatternResolver.ResolveRelativeTarget(memory, callRva + 1, callRva + 5);
            if (callRva != expectedCallRva ||
                callRva + 5 != expectedReturnRva ||
                target != AssassinCombatResumeNativeDefinition.GeneralResumeRva)
            {
                throw new InvalidOperationException($"{description} no longer calls the audited resume helper");
            }
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
