using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;

namespace AssassinCombatFix
{
    internal sealed unsafe class AssassinCombatResumeRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int ResumeOldOrderDelegate(IntPtr tribeManager, int nativeUnitIndex, int internalCommand);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CommonPathRequestDelegate(
            IntPtr unitBase,
            int nativeUnitIndex,
            int targetX,
            int targetY,
            int pathOption);

        private readonly ManualLogSource log;
        private ResumeOldOrderDelegate originalResumeOldOrder;
        private ResumeOldOrderDelegate rootedResumeOldOrderDetour;
        private CommonPathRequestDelegate originalCommonPathRequest;
        private CommonPathRequestDelegate rootedCommonPathRequestDetour;
        private NativeDetour resumeOldOrderDetour;
        private NativeDetour commonPathRequestDetour;
        private int* assassinPathContextFlag;

        #region TEMPORARY ASSASSIN_COMBAT_RESUME_DIAGNOSTICS - remove this entire region after validation
        private const int MaximumDiagnosticEventsPerMap = 64;
        private int diagnosticEventCount;
        #endregion

        public AssassinCombatResumeRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public bool IsInstalled => resumeOldOrderDetour != null && commonPathRequestDetour != null;

        public void InitializeNative(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool fixedLayoutHashValidated)
        {
            if (IsInstalled)
                return;
            if (!fixedLayoutHashValidated)
                throw new InvalidOperationException("fixed native layout hash does not match the supported CrusaderDE.dll");
            if (libraryHandle == IntPtr.Zero ||
                memory.Length <= AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva + sizeof(int))
                throw new InvalidOperationException("native module memory does not cover the Assassin path-context flag");

            Shared.NativeResolution resume = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinCombatResumeNativeDefinition.ResumeOldOrderPattern,
                AssassinCombatResumeNativeDefinition.ResumeOldOrderRva,
                referenceHashMatches: true,
                "Assassin post-combat movement-order resume",
                log);
            Shared.NativeResolution nativeUnitIndex = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinCombatResumeNativeDefinition.ResumeNativeUnitIndexAddressingPattern,
                AssassinCombatResumeNativeDefinition.ResumeNativeUnitIndexAddressingRva,
                referenceHashMatches: true,
                "Assassin resume native unit-index addressing",
                log);
            Shared.NativeResolution commonPath = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinCombatResumeNativeDefinition.CommonPathRequestPattern,
                AssassinCombatResumeNativeDefinition.CommonPathRequestRva,
                referenceHashMatches: true,
                "common path request used by Assassin post-combat repathing",
                log);
            if (resume.Rva != AssassinCombatResumeNativeDefinition.ResumeOldOrderRva ||
                nativeUnitIndex.Rva != AssassinCombatResumeNativeDefinition.ResumeNativeUnitIndexAddressingRva ||
                commonPath.Rva != AssassinCombatResumeNativeDefinition.CommonPathRequestRva)
                throw new InvalidOperationException("an Assassin combat-resume hook resolved outside its validated RVA");

            ValidatePostCombatNativeContracts(memory);
            assassinPathContextFlag = (int*)IntPtr.Add(
                libraryHandle,
                AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva).ToPointer();

            rootedResumeOldOrderDetour = ResumeOldOrderAfterCombat;
            rootedCommonPathRequestDetour = RequestPathWithAssassinCombatContext;
            NativeDetour installedResume = null;
            NativeDetour installedCommonPath = null;
            bool resumeApplied = false;
            bool commonPathApplied = false;
            try
            {
                installedResume = new NativeDetour(
                    IntPtr.Add(libraryHandle, resume.Rva),
                    Marshal.GetFunctionPointerForDelegate(rootedResumeOldOrderDetour),
                    new NativeDetourConfig { ManualApply = true });
                originalResumeOldOrder = installedResume.GenerateTrampoline<ResumeOldOrderDelegate>();
                installedCommonPath = new NativeDetour(
                    IntPtr.Add(libraryHandle, commonPath.Rva),
                    Marshal.GetFunctionPointerForDelegate(rootedCommonPathRequestDetour),
                    new NativeDetourConfig { ManualApply = true });
                originalCommonPathRequest = installedCommonPath.GenerateTrampoline<CommonPathRequestDelegate>();

                installedResume.Apply();
                resumeApplied = true;
                installedCommonPath.Apply();
                commonPathApplied = true;
                resumeOldOrderDetour = installedResume;
                commonPathRequestDetour = installedCommonPath;
                LogInfo(
                    $"installed Assassin combat-resume hooks at RVAs 0x{resume.Rva:X} and 0x{commonPath.Rva:X}.");
            }
            catch
            {
                if (commonPathApplied)
                    installedCommonPath?.Undo();
                installedCommonPath?.Dispose();
                if (resumeApplied)
                    installedResume?.Undo();
                installedResume?.Dispose();
                originalResumeOldOrder = null;
                rootedResumeOldOrderDetour = null;
                originalCommonPathRequest = null;
                rootedCommonPathRequestDetour = null;
                resumeOldOrderDetour = null;
                commonPathRequestDetour = null;
                assassinPathContextFlag = null;
                throw;
            }
        }

        public void BeginMap()
        {
            diagnosticEventCount = 0;
        }

        private int ResumeOldOrderAfterCombat(
            IntPtr tribeManager,
            int nativeUnitIndex,
            int internalCommand)
        {
            ResumeOldOrderDelegate vanilla = originalResumeOldOrder;
            if (vanilla == null)
                return 0;

            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            bool unitResolved = AssassinCombatResumePolicy.IsValidNativeUnitIndex(nativeUnitIndex, units.Length);
            AliveState aliveState = unitResolved ? units[nativeUnitIndex].r_AliveState : default;
            eChimps unitType = unitResolved ? units[nativeUnitIndex].r_UnitChimp : default;
            bool eligible = AssassinCombatResumePolicy.ShouldUseAssassinPathContext(
                true,
                true,
                IsInstalled,
                unitResolved,
                aliveState,
                unitType);
            int diagnosticId = BeginResumeDiagnostic(
                nativeUnitIndex,
                units.Length,
                unitResolved,
                aliveState,
                unitType,
                internalCommand,
                eligible);
            if (!eligible)
            {
                int vanillaResult = vanilla(tribeManager, nativeUnitIndex, internalCommand);
                LogDiagnostic(
                    diagnosticId,
                    $"resume-exit eligible=False, result={vanillaResult}, contextFlag={*assassinPathContextFlag}");
                return vanillaResult;
            }

            int previousPathContext = *assassinPathContextFlag;
            *assassinPathContextFlag = 1;
            int result = 0;
            int pathContextAfterVanilla = int.MinValue;
            bool completed = false;
            try
            {
                result = vanilla(tribeManager, nativeUnitIndex, internalCommand);
                pathContextAfterVanilla = *assassinPathContextFlag;
                completed = true;
                return result;
            }
            finally
            {
                *assassinPathContextFlag = previousPathContext;
                LogDiagnostic(
                    diagnosticId,
                    $"resume-exit eligible=True, completed={completed}, result={result}, flagBefore={previousPathContext}, flagAfterVanilla={pathContextAfterVanilla}, flagRestored={*assassinPathContextFlag}");
            }
        }

        private int RequestPathWithAssassinCombatContext(
            IntPtr unitBase,
            int nativeUnitIndex,
            int targetX,
            int targetY,
            int pathOption)
        {
            CommonPathRequestDelegate vanilla = originalCommonPathRequest;
            if (vanilla == null)
                return 0;

            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            bool unitResolved = AssassinCombatResumePolicy.IsValidNativeUnitIndex(nativeUnitIndex, units.Length);
            AliveState aliveState = unitResolved ? units[nativeUnitIndex].r_AliveState : default;
            eChimps unitType = unitResolved ? units[nativeUnitIndex].r_UnitChimp : default;
            ushort aiState = unitResolved ? units[nativeUnitIndex].r_AIState : (ushort)0;
            int previousPathContext = *assassinPathContextFlag;
            bool injectContext = AssassinCombatResumePolicy.ShouldInjectPostCombatPathContext(
                true,
                true,
                IsInstalled,
                unitResolved,
                aliveState,
                unitType,
                aiState,
                previousPathContext);
            int diagnosticId = BeginDirectRepathDiagnostic(
                nativeUnitIndex,
                units.Length,
                unitResolved,
                aliveState,
                unitType,
                aiState,
                targetX,
                targetY,
                pathOption,
                injectContext,
                previousPathContext);
            if (!injectContext)
            {
                int vanillaResult = vanilla(unitBase, nativeUnitIndex, targetX, targetY, pathOption);
                LogDiagnostic(
                    diagnosticId,
                    $"direct-repath-exit injected=False, result={vanillaResult}, flagAfterVanilla={*assassinPathContextFlag}");
                return vanillaResult;
            }

            *assassinPathContextFlag = 1;
            int result = 0;
            int pathContextAfterVanilla = int.MinValue;
            bool completed = false;
            try
            {
                result = vanilla(unitBase, nativeUnitIndex, targetX, targetY, pathOption);
                pathContextAfterVanilla = *assassinPathContextFlag;
                completed = true;
                return result;
            }
            finally
            {
                *assassinPathContextFlag = previousPathContext;
                LogDiagnostic(
                    diagnosticId,
                    $"direct-repath-exit injected=True, completed={completed}, result={result}, flagBefore={previousPathContext}, flagAfterVanilla={pathContextAfterVanilla}, flagRestored={*assassinPathContextFlag}");
            }
        }

        private void ValidatePostCombatNativeContracts(ReadOnlySpan<byte> memory)
        {
            Shared.NativeResolution remap = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinCombatResumeNativeDefinition.AssassinStateRemapSequence,
                AssassinCombatResumeNativeDefinition.AssassinStateRemapSequenceRva,
                referenceHashMatches: true,
                "Assassin AI-state remap around post-combat state 122",
                log);
            if (memory[remap.Rva + AssassinCombatResumeNativeDefinition.PostCombatStateRemapOffset] !=
                AssassinCombatResumeNativeDefinition.PostCombatStateRemapIndex)
                throw new InvalidOperationException("Assassin state 122 no longer maps to jump-table index 13");

            Shared.NativeResolution jumpTable = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinCombatResumeNativeDefinition.AssassinStateJumpTableSequence,
                AssassinCombatResumeNativeDefinition.AssassinStateJumpTableSequenceRva,
                referenceHashMatches: true,
                "Assassin AI-state jump table around post-combat state 122",
                log);
            int stateHandler = Shared.NativePatternResolver.ReadInt32(
                memory,
                jumpTable.Rva + AssassinCombatResumeNativeDefinition.PostCombatStateJumpTargetOffset);
            if (stateHandler != AssassinCombatResumeNativeDefinition.PostCombatStateHandlerRva)
                throw new InvalidOperationException("Assassin state 122 no longer targets its audited handler");

            Shared.NativeResolution directRepath = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequence,
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequenceRva,
                referenceHashMatches: true,
                "Assassin state-122 direct path request",
                log);
            int directPathTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                directRepath.Rva + AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallOffset + 1,
                directRepath.Rva + AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallOffset + 5);
            int nextState = Shared.NativePatternResolver.ReadInt32(
                memory,
                directRepath.Rva + AssassinCombatResumeNativeDefinition.PostCombatMovementStateLoadOffset + 1);
            if (directRepath.Rva != AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequenceRva ||
                directPathTarget != AssassinCombatResumeNativeDefinition.CommonPathRequestRva ||
                nextState != 101)
                throw new InvalidOperationException(
                    "Assassin state 122 no longer directly requests the audited path and enters state 101");
        }

        #region TEMPORARY ASSASSIN_COMBAT_RESUME_DIAGNOSTICS - remove this entire region after validation
        private int BeginResumeDiagnostic(
            int nativeUnitIndex,
            int unitCount,
            bool unitResolved,
            AliveState aliveState,
            eChimps unitType,
            int internalCommand,
            bool eligible)
        {
            if (diagnosticEventCount >= MaximumDiagnosticEventsPerMap)
                return 0;
            int diagnosticId = ++diagnosticEventCount;
            LogDiagnostic(
                diagnosticId,
                $"resume-enter nativeUnitIndex={nativeUnitIndex}, unitCount={unitCount}, resolved={unitResolved}, aliveState={aliveState}, unitType={unitType}, internalCommand={internalCommand}, eligible={eligible}, contextFlag={*assassinPathContextFlag}");
            return diagnosticId;
        }

        private int BeginDirectRepathDiagnostic(
            int nativeUnitIndex,
            int unitCount,
            bool unitResolved,
            AliveState aliveState,
            eChimps unitType,
            ushort aiState,
            int targetX,
            int targetY,
            int pathOption,
            bool injectContext,
            int pathContext)
        {
            if (aiState != AssassinCombatResumePolicy.PostCombatRepathState ||
                diagnosticEventCount >= MaximumDiagnosticEventsPerMap)
                return 0;
            int diagnosticId = ++diagnosticEventCount;
            LogDiagnostic(
                diagnosticId,
                $"direct-repath-enter nativeUnitIndex={nativeUnitIndex}, unitCount={unitCount}, resolved={unitResolved}, aliveState={aliveState}, unitType={unitType}, aiState={aiState}, target={targetX},{targetY}, pathOption={pathOption}, eligible={injectContext}, contextFlag={pathContext}");
            return diagnosticId;
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
