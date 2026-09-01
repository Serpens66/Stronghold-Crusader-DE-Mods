using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Threading;
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
        private HookRef<X64InlineHook> postCombatPathContextHook = new HookRef<X64InlineHook>();
        private int* assassinPathContextFlag;
        private ulong libraryBase;
        private int callbackFailureLogged;

        public AssassinCombatResumeRuntime(ManualLogSource log, BugfixesAndQoL.BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public bool IsInstalled =>
            transaction != null &&
            postCombatPathContextHook.Success &&
            postCombatPathContextHook.Value.IsActive;

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool fixedLayoutHashValidated)
        {
            if (IsInstalled)
                return;
            if (!fixedLayoutHashValidated)
                throw new InvalidOperationException("fixed native layout hash does not match the supported CrusaderDE.dll");
            if (libraryHandle == IntPtr.Zero ||
                memory.Length < AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva + sizeof(int))
            {
                throw new InvalidOperationException("native module memory does not cover the Assassin path-context flag");
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
                    ref postCombatPathContextHook,
                    libraryBase + unchecked((ulong)AssassinCombatResumeNativeDefinition.PostCombatPathContextHookRva),
                    PreparePostCombatPathContext,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: AssassinCombatResumeNativeDefinition.PostCombatPathContextHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                installedTransaction.Commit();

                if (!postCombatPathContextHook.Success || !postCombatPathContextHook.Value.IsActive)
                    throw new InvalidOperationException("the Assassin post-combat path-context hook was not activated");

                transaction = installedTransaction;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"installed Assassin post-combat path-context hook at RVA " +
                    $"0x{AssassinCombatResumeNativeDefinition.PostCombatPathContextHookRva:X}.");
            }
            catch
            {
                installedTransaction?.Unload();
                installedTransaction?.Dispose();
                transaction = null;
                postCombatPathContextHook = new HookRef<X64InlineHook>();
                assassinPathContextFlag = null;
                libraryBase = 0;
                throw;
            }
        }

        private void PreparePostCombatPathContext(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                ulong returnAddress = *(ulong*)(context.Pointer->RSP +
                    AssassinCombatResumeNativeDefinition.PostCombatCallerReturnAddressStackOffset);
                bool expectedCaller = returnAddress == libraryBase +
                    unchecked((ulong)AssassinCombatResumeNativeDefinition.CombatFinishResumeReturnRva);
                if (!AssassinCombatResumePolicy.ShouldProcessPostCombatPathRequest(
                    settings.EnableMod,
                    settings.EnableImprovedAssassinPathfinding,
                    IsInstalled,
                    expectedCaller))
                {
                    return;
                }

                int unitId = unchecked((int)context.Pointer->RDI);
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                if (!AssassinCombatResumePolicy.TryConvertUnitIdToSpanIndex(
                        unitId,
                        units.Length,
                        out int spanIndex))
                {
                    return;
                }

                GameUnit unit = units[spanIndex];
                if (!AssassinCombatResumePolicy.IsEligibleAssassin(
                        true,
                        unit.r_AliveState,
                        unit.r_UnitChimp,
                        unit.r_AIState))
                {
                    return;
                }

                // This is deliberately the final operation: Vanilla's path request
                // consumes and clears the context flag on both audited exits.
                if (*assassinPathContextFlag == 0)
                    *assassinPathContextFlag = 1;
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref callbackFailureLogged, 1) == 0)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Assassin post-combat path-context callback failed; further callback errors are suppressed and Vanilla behavior continues: {ex}");
                }
            }
        }

        private void ValidateNativeContracts(ReadOnlySpan<byte> memory)
        {
            if ((int)eChimps.CHIMP_TYPE_ARAB_ASSASIN != AssassinCombatResumeNativeDefinition.AssassinUnitTypeValue)
                throw new InvalidOperationException("the Script Extender Assassin enum value changed");

            Shared.NativeResolution state106Callsite = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.State106CombatFinishCallSequence,
                AssassinCombatResumeNativeDefinition.State106CombatFinishCallSequenceRva,
                "Assassin state-106 combat-finish callsite");
            int state106CallRva = state106Callsite.Rva + AssassinCombatResumeNativeDefinition.State106CombatFinishCallOffset;
            int state106CallTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, state106CallRva + 1, state106CallRva + 5);
            if (state106CallRva != AssassinCombatResumeNativeDefinition.State106CombatFinishCallRva ||
                state106CallRva + 5 != AssassinCombatResumeNativeDefinition.State106CombatFinishReturnRva ||
                state106CallTarget != AssassinCombatResumeNativeDefinition.CombatFinishHelperRva)
            {
                throw new InvalidOperationException("Assassin state 106 no longer calls the audited combat-finish helper");
            }

            Shared.NativeResolution combatFinish = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.CombatFinishHelperSequence,
                AssassinCombatResumeNativeDefinition.CombatFinishHelperSequenceRva,
                "combat-finish resume helper callsite");
            int resumeCallRva = combatFinish.Rva + AssassinCombatResumeNativeDefinition.CombatFinishResumeCallOffset;
            int resumeCallTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, resumeCallRva + 1, resumeCallRva + 5);
            if (resumeCallRva != AssassinCombatResumeNativeDefinition.CombatFinishResumeCallRva ||
                resumeCallRva + 5 != AssassinCombatResumeNativeDefinition.CombatFinishResumeReturnRva ||
                resumeCallTarget != AssassinCombatResumeNativeDefinition.PostCombatRepathRva)
            {
                throw new InvalidOperationException("the combat-finish helper no longer calls the audited post-combat repath helper");
            }

            Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatRepathPrologueSequence,
                AssassinCombatResumeNativeDefinition.PostCombatRepathPrologueRva,
                "post-combat repath helper prologue");
            if (AssassinCombatResumeNativeDefinition.PostCombatCallerReturnAddressStackOffset != sizeof(ulong) + 0x30)
                throw new InvalidOperationException("the post-combat caller return-address stack offset no longer matches its prologue");

            Shared.NativeResolution pathRequest = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequence,
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequenceRva,
                "post-combat saved-state path request");
            int pathRequestCallRva = pathRequest.Rva + AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallOffset;
            int pathRequestTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, pathRequestCallRva + 1, pathRequestCallRva + 5);
            int finalizeCallRva = pathRequest.Rva + AssassinCombatResumeNativeDefinition.PostCombatFinalizeCallOffset;
            int finalizeTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, finalizeCallRva + 1, finalizeCallRva + 5);
            if (pathRequestCallRva != AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallRva ||
                pathRequestTarget != AssassinCombatResumeNativeDefinition.CommonPathRequestRva ||
                finalizeCallRva != AssassinCombatResumeNativeDefinition.PostCombatFinalizeCallRva ||
                finalizeTarget != AssassinCombatResumeNativeDefinition.PostPathRequestRva)
            {
                throw new InvalidOperationException("the post-combat helper no longer restores the saved request through the audited calls");
            }

            ValidateHookSpan(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatPathContextHookRva,
                AssassinCombatResumeNativeDefinition.PostCombatPathContextHookBytes,
                "post-combat path-context hook");
            ValidateHookSpan(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatRestoredStateWriteRva,
                AssassinCombatResumeNativeDefinition.PostCombatRestoredStateWriteBytes,
                "post-combat restored-state write");
            if (AssassinCombatResumeNativeDefinition.PostCombatPathContextHookLength !=
                    AssassinCombatResumeNativeDefinition.PostCombatPathContextHookBytes.Length ||
                AssassinCombatResumeNativeDefinition.PostCombatPathContextHookLength <
                    AssassinCombatResumeNativeDefinition.InlineHookMinimumOverwriteLength ||
                AssassinCombatResumeNativeDefinition.PostCombatPathContextHookRva +
                    AssassinCombatResumeNativeDefinition.PostCombatPathContextHookLength !=
                    AssassinCombatResumeNativeDefinition.PostCombatRestoredStateWriteRva ||
                AssassinCombatResumeNativeDefinition.PostCombatRestoredStateWriteRva +
                    AssassinCombatResumeNativeDefinition.PostCombatRestoredStateWriteBytes.Length !=
                    AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallRva)
            {
                throw new InvalidOperationException("the post-combat path-context hook no longer ends on audited instruction boundaries");
            }

            Shared.NativeResolution contextRead = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.CommonPathContextReadSequence,
                AssassinCombatResumeNativeDefinition.CommonPathContextReadRva,
                "common path request Assassin-context read");
            int readTarget = Shared.NativePatternResolver.ResolveRelativeTarget(memory, contextRead.Rva + 3, contextRead.Rva + 7);
            if (readTarget != AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva)
                throw new InvalidOperationException("the shared path request no longer reads the audited Assassin flag");

            Shared.NativeResolution successClear = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.CommonPathSuccessClearSequence,
                AssassinCombatResumeNativeDefinition.CommonPathSuccessClearSequenceRva,
                "common path request success-path context clear");
            int successClearInstruction = successClear.Rva + AssassinCombatResumeNativeDefinition.CommonPathSuccessFlagClearOffset;
            int successClearTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, successClearInstruction + 3, successClearInstruction + 7);
            Shared.NativeResolution failureClear = Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.CommonPathFailureClearSequence,
                AssassinCombatResumeNativeDefinition.CommonPathFailureClearRva,
                "common path request failure-path context clear");
            int failureClearTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, failureClear.Rva + 3, failureClear.Rva + 7);
            if (successClearTarget != AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva ||
                failureClearTarget != AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva)
            {
                throw new InvalidOperationException("the common path request no longer clears the Assassin context on both audited exits");
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

        private Shared.NativeResolution Resolve(ReadOnlySpan<byte> memory, string pattern, int expectedRva, string description)
        {
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory, pattern, expectedRva, referenceHashMatches: true, description);
            if (resolution.Rva != expectedRva)
                throw new InvalidOperationException($"{description} resolved outside its validated RVA");
            return resolution;
        }

        private static void ValidateHookSpan(ReadOnlySpan<byte> memory, int hookRva, byte[] expectedBytes, string description)
        {
            if (hookRva < 0 || hookRva + expectedBytes.Length > memory.Length ||
                !memory.Slice(hookRva, expectedBytes.Length).SequenceEqual(expectedBytes))
            {
                throw new InvalidOperationException(
                    $"the Assassin combat-resume {description} no longer matches the audited instructions");
            }
        }
    }
}
