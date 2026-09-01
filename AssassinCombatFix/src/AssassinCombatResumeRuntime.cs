using BepInEx.Logging;
using Iced.Intel;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Extensions;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using static Iced.Intel.AssemblerRegisters;

namespace AssassinCombatFix
{
    internal sealed unsafe class AssassinCombatResumeRuntime
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoL.BugfixesAndQoLViewModel settings;
        private HookTransaction transaction;
        private HookRef<X64FunctionCloneHook> assassinStateMachineDiagnosticHook =
            new HookRef<X64FunctionCloneHook>();
        private int* assassinPathContextFlag;
        private ulong libraryBase;
        private bool tickObserverSubscribed;
        private IDisposable moveHereSubscription;
        private IDisposable killedByMeleeSubscription;
        private readonly List<Delegate> stateWriteDiagnosticCallbacks = new List<Delegate>();
        private int clonedStateWriteSiteCount;
        private bool mapActive;

        #region TEMPORARY ASSASSIN_COMBAT_RESUME_DIAGNOSTICS - remove this entire region after validation
        private const int MaximumDiagnosticEventsPerMap = 256;
        private const int MaximumStateTraceEventsPerMap = 256;
        private int diagnosticEventCount;
        private int stateTraceEventCount;
        private readonly Dictionary<int, AssassinTraceSnapshot> trackedAssassins =
            new Dictionary<int, AssassinTraceSnapshot>();
        private int currentTick;

        private sealed class AssassinTraceSnapshot
        {
            public uint GlobalId;
            public ushort AiState;
            public string Signature;
            public int LastLogTick;
            public int LastState106Tick = -1;
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
            assassinStateMachineDiagnosticHook.Success &&
            moveHereSubscription != null &&
            killedByMeleeSubscription != null;

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
            ValidateLiveStateMachineEntry(libraryHandle);

            HookTransaction installedTransaction = null;
            try
            {
                installedTransaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                installedTransaction.AddCloneHook(
                    ref assassinStateMachineDiagnosticHook,
                    libraryBase + unchecked((ulong)AssassinCombatResumeNativeDefinition.AssassinStateMachineRva),
                    new[] { CreateStateWriteDiagnosticPatch() });
                installedTransaction.Commit();

                if (!assassinStateMachineDiagnosticHook.Success ||
                    clonedStateWriteSiteCount != AssassinCombatResumeNativeDefinition.AssassinAiStateWriteRvas.Length)
                {
                    throw new InvalidOperationException(
                        $"the passive Assassin state diagnostic clone was incomplete: " +
                        $"expectedSites={AssassinCombatResumeNativeDefinition.AssassinAiStateWriteRvas.Length}, " +
                        $"actualSites={clonedStateWriteSiteCount}");
                }

                transaction = installedTransaction;
                moveHereSubscription = UnitR3EventHooks.OnUnitMoveHere.Observable
                    .Subscribe(TraceMoveHere);
                killedByMeleeSubscription = UnitR3EventHooks.OnUnitKilledByMelee.Observable
                    .Subscribe(TraceKilledByMelee);
                GameTimeManagerAPI.Instance.OnTick += ObserveAssassinStates;
                tickObserverSubscribed = true;
                LogInfo(
                    $"installed passive Assassin state-machine clone diagnostics at RVA " +
                    $"0x{AssassinCombatResumeNativeDefinition.AssassinStateMachineRva:X} with " +
                    $"{clonedStateWriteSiteCount} audited state-write sites; subscribed to Script Extender " +
                    $"MoveHere and melee-kill events.");
            }
            catch
            {
                moveHereSubscription?.Dispose();
                moveHereSubscription = null;
                killedByMeleeSubscription?.Dispose();
                killedByMeleeSubscription = null;
                installedTransaction?.Unload();
                installedTransaction?.Dispose();
                if (tickObserverSubscribed)
                {
                    GameTimeManagerAPI.Instance.OnTick -= ObserveAssassinStates;
                    tickObserverSubscribed = false;
                }
                transaction = null;
                assassinStateMachineDiagnosticHook = new HookRef<X64FunctionCloneHook>();
                stateWriteDiagnosticCallbacks.Clear();
                clonedStateWriteSiteCount = 0;
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
            currentTick = 0;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC] trace lifecycle started: reason={reason}.");
        }

        public void EndMap()
        {
            bool wasActive = mapActive;
            mapActive = false;
            currentTick = 0;
            trackedAssassins.Clear();
            if (wasActive)
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC] trace lifecycle ended: reason=OnUnloadMap(Post).");
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void StateWriteDiagnosticDelegate(ulong proposedState, ulong siteRva);

        private FunctionClonePatch CreateStateWriteDiagnosticPatch()
        {
            return new FunctionClonePatch
            {
                Predicate = instruction =>
                    instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.GetOpKind(0) == OpKind.Memory &&
                    instruction.MemoryDisplacement32 ==
                        AssassinCombatResumeNativeDefinition.AssassinAiStateFieldOffset,
                Generator = (Assembler assembler, Instruction original, ref bool suppressOriginal) =>
                {
                    int siteRva = checked((int)(original.IP - libraryBase));
                    if (!AssassinCombatResumeNativeDefinition.AssassinAiStateWriteRvas.Contains(siteRva))
                    {
                        throw new InvalidOperationException(
                            $"unexpected Assassin AI-state write discovered at RVA 0x{siteRva:X}");
                    }

                    Register sourceRegister = original.GetOpRegister(1);
                    bool isImmediate = sourceRegister == Register.None;
                    StateWriteDiagnosticDelegate callback =
                        (proposedState, callbackSiteRva) =>
                            TraceStateWrite(unchecked((ushort)proposedState), unchecked((int)callbackSiteRva));
                    stateWriteDiagnosticCallbacks.Add(callback);
                    ulong callbackAddress = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(callback).ToInt64());

                    // A state write is otherwise flag-neutral. Preserve RFLAGS as well as
                    // registers so this passive trace cannot alter a following Vanilla branch.
                    assembler.pushfq();
                    assembler.X64FastcallSafeEx(
                        callbackAddress,
                        totalArgumentCount: 2,
                        prepareArgumentsAction: arguments =>
                        {
                            if (isImmediate)
                                arguments.mov(rcx, original.GetImmediate(1));
                            else
                                arguments.movzx(rcx, new AssemblerRegister16(sourceRegister));
                            arguments.mov(rdx, unchecked((ulong)siteRva));
                        },
                        preserveRAX: true);
                    assembler.popfq();
                    clonedStateWriteSiteCount++;
                    suppressOriginal = false;
                }
            };
        }

        private void TraceStateWrite(ushort proposedState, int siteRva)
        {
            try
            {
                int nativeUnitIndex = GameUnitManagerAPI.Instance.GetCurrentContextUnitId();
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                if (!TryGetLoggableAssassin(nativeUnitIndex, units, out GameUnit unit))
                    return;

                int diagnosticId = BeginRawResumeDiagnostic(true, unit.r_AliveState, unit.r_UnitChimp);
                LogDiagnostic(
                    diagnosticId,
                    $"state-write tick={currentTick}, siteRva=0x{siteRva:X}, " +
                    $"nativeUnitIndex={nativeUnitIndex}, oldState={unit.r_AIState}, " +
                    $"proposedState={proposedState}, ticksSinceState106={GetTicksSinceState106(nativeUnitIndex)}, " +
                    DescribeUnit(unit));
            }
            catch (Exception ex)
            {
                LogDiagnosticFailure("state-write", ex);
            }
        }

        private void TraceMoveHere(UnitMoveHereEventArgs args)
        {
            try
            {
                int nativeUnitIndex = args.UnitId;
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                if (!TryGetLoggableAssassin(nativeUnitIndex, units, out GameUnit unit))
                    return;

                int diagnosticId = BeginRawResumeDiagnostic(true, unit.r_AliveState, unit.r_UnitChimp);
                LogDiagnostic(
                    diagnosticId,
                    $"move-here phase={args.Phase}, tick={currentTick}, nativeUnitIndex={nativeUnitIndex}, " +
                    $"requestTarget={args.TileX},{args.TileY}, pathOption={args.Unknown}, " +
                    $"returnValue={args.ReturnValue}, skipOriginal={args.SkipOriginalFunction}, " +
                    $"assassinContextFlag={*assassinPathContextFlag}, " +
                    $"ticksSinceState106={GetTicksSinceState106(nativeUnitIndex)}, {DescribeUnit(unit)}");
            }
            catch (Exception ex)
            {
                LogDiagnosticFailure("move-here", ex);
            }
        }

        private void TraceKilledByMelee(UnitKilledByMeleeEventArgs args)
        {
            try
            {
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                if (!TryResolveAssassinEventIndex(args.AttackingUnitId, units, out int nativeUnitIndex, out string basis) ||
                    !TryGetLoggableAssassin(nativeUnitIndex, units, out GameUnit unit))
                {
                    return;
                }

                int diagnosticId = BeginRawResumeDiagnostic(true, unit.r_AliveState, unit.r_UnitChimp);
                LogDiagnostic(
                    diagnosticId,
                    $"melee-kill phase={args.Phase}, tick={currentTick}, rawAttacker={args.AttackingUnitId}, " +
                    $"rawVictim={args.AttackedUnitId}, attackerResolution={basis}, " +
                    $"nativeUnitIndex={nativeUnitIndex}, assassinContextFlag={*assassinPathContextFlag}, " +
                    $"ticksSinceState106={GetTicksSinceState106(nativeUnitIndex)}, {DescribeUnit(unit)}");
            }
            catch (Exception ex)
            {
                LogDiagnosticFailure("melee-kill", ex);
            }
        }

        private void ObserveAssassinStates(int tick)
        {
            currentTick = tick;
            if (!settings.EnableMod || !settings.EnableImprovedAssassinPathfinding)
                return;

            if (!mapActive && AssassinCombatResumePolicy.ShouldBeginEditorTrace(
                    mapActive,
                    Shared.GameModeHelper.IsMapEditor()))
            {
                // The map editor creates a playable simulation without raising OnStartMap.
                // Its first simulation tick is the narrow point where unit data is ready.
                BeginMap($"first-map-editor-simulation-tick, tick={tick}");
                currentTick = tick;
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

                    if (unit.r_AIState == 106)
                        tracked.LastState106Tick = tick;

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
            if ((int)eChimps.CHIMP_TYPE_ARAB_ASSASIN !=
                AssassinCombatResumeNativeDefinition.AssassinUnitTypeValue)
            {
                throw new InvalidOperationException("the Script Extender Assassin enum value changed");
            }

            Resolve(
                memory,
                AssassinCombatResumeNativeDefinition.AssassinStateMachineEntryPattern,
                AssassinCombatResumeNativeDefinition.AssassinStateMachineRva,
                "Assassin state-machine entry");
            ValidateStateMachineWrites(memory);

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
                state106CallRva + 5 != AssassinCombatResumeNativeDefinition.State106CombatFinishReturnRva ||
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

        private static void ValidateStateMachineWrites(ReadOnlySpan<byte> memory)
        {
            int startRva = AssassinCombatResumeNativeDefinition.AssassinStateMachineRva;
            int size = AssassinCombatResumeNativeDefinition.AssassinStateMachineSize;
            if (startRva < 0 || startRva + size > memory.Length)
                throw new InvalidOperationException("native memory does not cover the complete Assassin state machine");

            byte[] functionBytes = memory.Slice(startRva, size).ToArray();
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(functionBytes));
            decoder.IP = unchecked((ulong)startRva);
            List<int> writeRvas = new List<int>();
            while (decoder.IP < unchecked((ulong)(startRva + size)) && decoder.LastError == DecoderError.None)
            {
                Instruction instruction = decoder.Decode();
                if (decoder.LastError != DecoderError.None)
                    break;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.GetOpKind(0) == OpKind.Memory &&
                    instruction.MemoryDisplacement32 ==
                        AssassinCombatResumeNativeDefinition.AssassinAiStateFieldOffset)
                {
                    writeRvas.Add(unchecked((int)instruction.IP));
                }
            }

            if (decoder.LastError != DecoderError.None ||
                decoder.IP != unchecked((ulong)(startRva + size)) ||
                !writeRvas.SequenceEqual(AssassinCombatResumeNativeDefinition.AssassinAiStateWriteRvas))
            {
                throw new InvalidOperationException(
                    $"Assassin AI-state write contract changed: expected=" +
                    $"{string.Join(",", AssassinCombatResumeNativeDefinition.AssassinAiStateWriteRvas.Select(rva => $"0x{rva:X}"))}, " +
                    $"actual={string.Join(",", writeRvas.Select(rva => $"0x{rva:X}"))}");
            }
        }

        private static void ValidateLiveStateMachineEntry(IntPtr libraryHandle)
        {
            IntPtr vtableEntry = IntPtr.Add(
                libraryHandle,
                AssassinCombatResumeNativeDefinition.UnitFunctionsVTableRva +
                AssassinCombatResumeNativeDefinition.AssassinUnitTypeValue * sizeof(ulong));
            long liveHandler = Marshal.ReadInt64(vtableEntry);
            long expectedHandler = libraryHandle.ToInt64() +
                AssassinCombatResumeNativeDefinition.AssassinStateMachineRva;
            if (liveHandler != expectedHandler)
            {
                throw new InvalidOperationException(
                    "the live unit-function VTable no longer selects the canonical Assassin state machine");
            }

            byte[] actual = new byte[AssassinCombatResumeNativeDefinition.AssassinStateMachineEntryBytes.Length];
            Marshal.Copy(
                IntPtr.Add(libraryHandle, AssassinCombatResumeNativeDefinition.AssassinStateMachineRva),
                actual,
                0,
                actual.Length);
            if (!actual.SequenceEqual(AssassinCombatResumeNativeDefinition.AssassinStateMachineEntryBytes))
            {
                throw new InvalidOperationException(
                    "the live Assassin state-machine entry is already modified; clone diagnostics remain inactive");
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
                !AssassinCombatResumePolicy.IsWithinDiagnosticLimit(
                    diagnosticEventCount,
                    MaximumDiagnosticEventsPerMap))
            {
                return 0;
            }

            return ++diagnosticEventCount;
        }

        private bool TryGetLoggableAssassin(
            int nativeUnitIndex,
            Span<GameUnit> units,
            out GameUnit unit)
        {
            bool resolved = AssassinCombatResumePolicy.IsValidNativeUnitIndex(nativeUnitIndex, units.Length);
            unit = resolved ? units[nativeUnitIndex] : default;
            return AssassinCombatResumePolicy.ShouldLogPassiveDiagnostic(
                settings.EnableMod,
                settings.EnableImprovedAssassinPathfinding,
                IsInstalled,
                mapActive,
                resolved,
                resolved ? unit.r_AliveState : default,
                resolved ? unit.r_UnitChimp : default);
        }

        private static bool TryResolveAssassinEventIndex(
            int rawUnitId,
            Span<GameUnit> units,
            out int nativeUnitIndex,
            out string basis)
        {
            if (AssassinCombatResumePolicy.IsValidNativeUnitIndex(rawUnitId, units.Length) &&
                units[rawUnitId].r_UnitChimp == eChimps.CHIMP_TYPE_ARAB_ASSASIN)
            {
                nativeUnitIndex = rawUnitId;
                basis = "zero-based";
                return true;
            }

            int oneBasedCandidate = rawUnitId - 1;
            if (AssassinCombatResumePolicy.IsValidNativeUnitIndex(oneBasedCandidate, units.Length) &&
                units[oneBasedCandidate].r_UnitChimp == eChimps.CHIMP_TYPE_ARAB_ASSASIN)
            {
                nativeUnitIndex = oneBasedCandidate;
                basis = "one-based";
                return true;
            }

            nativeUnitIndex = -1;
            basis = "unresolved";
            return false;
        }

        private void LogDiagnosticFailure(string source, Exception ex)
        {
            Shared.DebugLogHelper.LogError(
                log,
                $"[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC] passive {source} trace failed: {ex}");
        }

        private static string BuildUnitSignature(GameUnit unit)
        {
            return string.Join(
                ":",
                unit.r_AIState,
                GetSavedAiState(unit),
                unit.r_AttackingUnitId,
                unit.N000001C2,
                unit.N0000019A,
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
                $"savedAiState={GetSavedAiState(unit)}, attackingUnit={unit.r_AttackingUnitId}/{unit.N000001C2}, " +
                $"unitStatus029C=0x{unit.N0000019A:X8}, " +
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

        private static ushort GetSavedAiState(GameUnit unit)
        {
            // Native +0x91E is the upper word of the field at GameUnit offset 0x2C0.
            return unchecked((ushort)(unit.N000000AB >> 16));
        }

        private int GetTicksSinceState106(int nativeUnitIndex)
        {
            if (!trackedAssassins.TryGetValue(nativeUnitIndex, out AssassinTraceSnapshot tracked) ||
                tracked.LastState106Tick < 0 ||
                currentTick < tracked.LastState106Tick)
            {
                return -1;
            }

            return currentTick - tracked.LastState106Tick;
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
