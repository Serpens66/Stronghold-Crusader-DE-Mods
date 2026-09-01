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
        private HookRef<X64InlineHook> prePathRequestHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> postPathRequestHook = new HookRef<X64InlineHook>();
        private int* assassinPathContextFlag;

        #region TEMPORARY ASSASSIN_COMBAT_RESUME_DIAGNOSTICS - remove this entire region after validation
        private const int MaximumDiagnosticEventsPerMap = 64;
        private int diagnosticEventCount;

        [ThreadStatic]
        private static Stack<PendingDiagnostic> pendingDiagnostics;

        private sealed class PendingDiagnostic
        {
            public int Id;
            public bool ContextInjected;
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
            prePathRequestHook.Success &&
            postPathRequestHook.Success;

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
            if (libraryHandle == IntPtr.Zero ||
                memory.Length <= AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva + sizeof(int))
            {
                throw new InvalidOperationException(
                    "native module memory does not cover the Assassin path-context flag");
            }

            Shared.NativeResolution callsite = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequence,
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequenceRva,
                referenceHashMatches: true,
                "Assassin state-122 path-request callsite",
                log);
            if (callsite.Rva != AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequenceRva)
            {
                throw new InvalidOperationException(
                    "the Assassin state-122 path-request callsite resolved outside its validated RVA");
            }

            ValidateNativeContracts(memory);
            assassinPathContextFlag = (int*)IntPtr.Add(
                libraryHandle,
                AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva).ToPointer();

            ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
            HookTransaction installedTransaction = null;
            try
            {
                installedTransaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                installedTransaction.AddContextHook(
                    ref prePathRequestHook,
                    libraryBase + unchecked((ulong)AssassinCombatResumeNativeDefinition.PostCombatPreHookRva),
                    BeforePostCombatPathRequest,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: AssassinCombatResumeNativeDefinition.PostCombatPreHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                installedTransaction.AddContextHook(
                    ref postPathRequestHook,
                    libraryBase + unchecked((ulong)AssassinCombatResumeNativeDefinition.PostCombatPostHookRva),
                    AfterPostCombatPathRequest,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: AssassinCombatResumeNativeDefinition.PostCombatPostHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                installedTransaction.Commit();

                if (!prePathRequestHook.Success || !postPathRequestHook.Success)
                {
                    throw new InvalidOperationException(
                        "one or both Assassin state-122 callsite hooks were not installed");
                }

                transaction = installedTransaction;
                LogInfo(
                    $"installed exact Assassin state-122 callsite hooks at RVAs " +
                    $"0x{AssassinCombatResumeNativeDefinition.PostCombatPreHookRva:X} and " +
                    $"0x{AssassinCombatResumeNativeDefinition.PostCombatPostHookRva:X}.");
            }
            catch
            {
                installedTransaction?.Unload();
                installedTransaction?.Dispose();
                transaction = null;
                prePathRequestHook = new HookRef<X64InlineHook>();
                postPathRequestHook = new HookRef<X64InlineHook>();
                assassinPathContextFlag = null;
                throw;
            }
        }

        public void BeginMap()
        {
            diagnosticEventCount = 0;
            pendingDiagnostics?.Clear();
        }

        private void BeforePostCombatPathRequest(NativePointer<X64SmartCPUContext> context)
        {
            Stack<PendingDiagnostic> stack = pendingDiagnostics ??
                (pendingDiagnostics = new Stack<PendingDiagnostic>());
            stack.Push(null);

            bool contextInjected = false;
            try
            {
                bool modEnabled = settings.EnableMod;
                bool improvedPathfindingEnabled = settings.EnableImprovedAssassinPathfinding;
                if (!modEnabled || !improvedPathfindingEnabled)
                    return;

                int nativeUnitIndex = unchecked((int)(uint)context.Pointer->RDX);
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                bool unitResolved = AssassinCombatResumePolicy.IsValidNativeUnitIndex(
                    nativeUnitIndex,
                    units.Length);
                AliveState aliveState = unitResolved ? units[nativeUnitIndex].r_AliveState : default;
                eChimps unitType = unitResolved ? units[nativeUnitIndex].r_UnitChimp : default;
                ushort aiState = unitResolved ? units[nativeUnitIndex].r_AIState : (ushort)0;
                int previousPathContext = *assassinPathContextFlag;
                bool eligible = AssassinCombatResumePolicy.ShouldInjectPostCombatPathContext(
                    modEnabled,
                    improvedPathfindingEnabled,
                    IsInstalled,
                    unitResolved,
                    aliveState,
                    unitType,
                    aiState,
                    previousPathContext);

                int diagnosticId = BeginCallsiteDiagnostic(unitResolved, unitType, aiState);
                PendingDiagnostic diagnostic = null;
                if (diagnosticId > 0)
                {
                    diagnostic = new PendingDiagnostic
                    {
                        Id = diagnosticId,
                        ContextInjected = eligible
                    };
                    stack.Pop();
                    stack.Push(diagnostic);

                    uint packedTarget = units[nativeUnitIndex].N000001AA;
                    short targetX = unchecked((short)(packedTarget & 0xFFFF));
                    short targetY = unchecked((short)(packedTarget >> 16));
                    LogDiagnostic(
                        diagnosticId,
                        $"callsite-pre nativeUnitIndex={nativeUnitIndex}, unitCount={units.Length}, " +
                        $"resolved={unitResolved}, aliveState={aliveState}, unitType={unitType}, " +
                        $"aiState={aiState}, target={targetX},{targetY}, eligible={eligible}, " +
                        $"flagBefore={previousPathContext}");
                }

                // This is the single Vanilla omission: the working Assassin branch at
                // 0x16CFE2 performs the same write before calling the same path routine.
                if (eligible)
                {
                    *assassinPathContextFlag = 1;
                    contextInjected = true;
                }
            }
            catch (Exception ex)
            {
                if (contextInjected && assassinPathContextFlag != null)
                    *assassinPathContextFlag = 0;
                stack.Pop();
                stack.Push(null);
                Shared.DebugLogHelper.LogError(
                    log,
                    $"[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC] pre-call validation failed; " +
                    $"Vanilla behavior remains active: {ex}");
            }
        }

        private void AfterPostCombatPathRequest(NativePointer<X64SmartCPUContext> context)
        {
            Stack<PendingDiagnostic> stack = pendingDiagnostics;
            if (stack == null || stack.Count == 0)
                return;

            PendingDiagnostic diagnostic = stack.Pop();
            if (diagnostic == null)
                return;

            int vanillaResult = unchecked((int)(uint)context.Pointer->RAX);
            int flagAfterVanilla = *assassinPathContextFlag;
            LogDiagnostic(
                diagnostic.Id,
                $"callsite-post injected={diagnostic.ContextInjected}, result={vanillaResult}, " +
                $"flagAfterVanilla={flagAfterVanilla}");
        }

        private void ValidateNativeContracts(ReadOnlySpan<byte> memory)
        {
            Shared.NativeResolution remap = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.AssassinStateRemapSequence,
                AssassinCombatResumeNativeDefinition.AssassinStateRemapSequenceRva,
                "Assassin AI-state remap around post-combat state 122");
            if (memory[remap.Rva + AssassinCombatResumeNativeDefinition.PostCombatStateRemapOffset] !=
                AssassinCombatResumeNativeDefinition.PostCombatStateRemapIndex)
            {
                throw new InvalidOperationException(
                    "Assassin state 122 no longer maps to jump-table index 13");
            }

            Shared.NativeResolution jumpTable = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.AssassinStateJumpTableSequence,
                AssassinCombatResumeNativeDefinition.AssassinStateJumpTableSequenceRva,
                "Assassin AI-state jump table around post-combat state 122");
            int stateHandler = Shared.NativePatternResolver.ReadInt32(
                memory,
                jumpTable.Rva + AssassinCombatResumeNativeDefinition.PostCombatStateJumpTargetOffset);
            if (stateHandler != AssassinCombatResumeNativeDefinition.PostCombatStateHandlerRva)
                throw new InvalidOperationException("Assassin state 122 no longer targets its audited handler");

            int callRva = AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallRva;
            int directPathTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                callRva + 1,
                callRva + 5);
            int nextState = Shared.NativePatternResolver.ReadInt32(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequenceRva +
                    AssassinCombatResumeNativeDefinition.PostCombatMovementStateLoadOffset + 1);
            if (directPathTarget != AssassinCombatResumeNativeDefinition.CommonPathRequestRva ||
                nextState != 101)
            {
                throw new InvalidOperationException(
                    "Assassin state 122 no longer directly requests the audited path and enters state 101");
            }

            ValidateHookSpan(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatPreHookRva,
                AssassinCombatResumeNativeDefinition.PostCombatPreHookBytes,
                "pre-call");
            ValidateHookSpan(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatPostHookRva,
                AssassinCombatResumeNativeDefinition.PostCombatPostHookBytes,
                "post-call");
            if (AssassinCombatResumeNativeDefinition.PostCombatPreHookRva +
                    AssassinCombatResumeNativeDefinition.PostCombatPreHookLength > callRva ||
                callRva + 5 > AssassinCombatResumeNativeDefinition.PostCombatPostHookRva)
            {
                throw new InvalidOperationException(
                    "Assassin state-122 hook spans overlap the native path-request call");
            }

            Shared.NativeResolution workingContext = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.WorkingAssassinContextSequence,
                AssassinCombatResumeNativeDefinition.WorkingAssassinContextSequenceRva,
                "working Vanilla Assassin path context");
            int workingFlagTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                workingContext.Rva + 3,
                workingContext.Rva + 7);
            int workingPathTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                workingContext.Rva + AssassinCombatResumeNativeDefinition.WorkingAssassinContextCallOffset + 1,
                workingContext.Rva + AssassinCombatResumeNativeDefinition.WorkingAssassinContextCallOffset + 5);
            if (workingFlagTarget != AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva ||
                workingPathTarget != AssassinCombatResumeNativeDefinition.CommonPathRequestRva)
            {
                throw new InvalidOperationException(
                    "Vanilla's working Assassin branch no longer sets the audited flag before the shared path request");
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
                    "the shared path request no longer clears the Assassin context on both audited exits");
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
                    $"the Assassin state-122 {description} hook span no longer matches audited instruction boundaries");
            }
        }

        #region TEMPORARY ASSASSIN_COMBAT_RESUME_DIAGNOSTICS - remove this entire region after validation
        private int BeginCallsiteDiagnostic(
            bool unitResolved,
            eChimps unitType,
            ushort aiState)
        {
            if (!AssassinCombatResumePolicy.ShouldLogCallsiteDiagnostic(
                    unitResolved,
                    unitType,
                    aiState) ||
                diagnosticEventCount >= MaximumDiagnosticEventsPerMap)
            {
                return 0;
            }

            return ++diagnosticEventCount;
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
