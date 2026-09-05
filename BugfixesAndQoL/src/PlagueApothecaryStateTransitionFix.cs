// Feature: Complete Vanilla's apothecary building-exit transition after a target is found.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Abstractions.Hooks;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Hooks.Context;


namespace BugfixesAndQoL
{
    internal sealed unsafe class PlagueApothecaryStateTransitionFix : IDisposable
    {
        private const ushort WaitingState = 2;
        private const ushort VanillaTransitionState = 109;
        private const ushort LeavingBuildingTransition = 0xFE20;

        // c_game_unit_healer_update, successful ten-tick search in state 2,
        // reference RVA 0x14F8CC. The hook runs only after the preceding search
        // returned a disease object and before Vanilla writes next-state 5.
        private const string PeriodicDiseaseFoundPattern =
            "48 63 15 ?? ?? ?? ?? 41 BF 05 00 00 00 4C 69 C2 90 04 00 00 " +
            "41 BE 14 00 00 00 41 BC F0 D8 FF FF 66 47 89 BC 28 1A 09 00 00";

        // c_game_unit_healer_update, regular state-2 timeout exit, reference RVA
        // 0x14F768. This is the Vanilla model for the omitted transition writes.
        private const string WorkingBuildingExitReferencePattern =
            "B8 6D 00 00 00 66 42 89 84 2B 18 09 00 00 8B D5 " +
            "66 42 C7 84 2B 86 09 00 00 20 FE";
        private const int PeriodicDiseaseFoundRva = 0x14F8CC;
        private const int WorkingBuildingExitReferenceRva = 0x14F768;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> transitionHook = new HookHandle<X64InlineHook>();
        private bool correctionAvailable = true;
        private bool disposed;

        public PlagueApothecaryStateTransitionFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            int periodicDiseaseFoundRva = PlagueNativePatternValidator.Resolve(
                log,
                memory,
                PeriodicDiseaseFoundPattern,
                PeriodicDiseaseFoundRva,
                referenceHashMatches,
                "apothecary state-2 disease-found branch");
            PlagueNativePatternValidator.Resolve(
                log,
                memory,
                WorkingBuildingExitReferencePattern,
                WorkingBuildingExitReferenceRva,
                referenceHashMatches,
                "apothecary working state-2 building-exit reference");

            try
            {
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                BugfixesHookInfrastructure.AddContextHook(
                    transaction,
                    transitionHook,
                    libraryBase + unchecked((ulong)periodicDiseaseFoundRva),
                    CompleteVanillaStateTransition,
                    registers: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBP,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                CommitResult commitResult = transaction.Commit();

                if (!commitResult.IsCompleteSuccess || !transitionHook.Success)
                    throw new InvalidOperationException("The apothecary state-2 transition hook was not installed.");

                Shared.DebugLogHelper.LogDebug(
                    log,
                    "Bugfixes and QoL stuck-apothecary fix initialized from unique target and " +
                    "Vanilla building-exit reference signatures.");
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
            correctionAvailable = false;
            transaction?.Dispose();
            transaction = null;
        }

        private void CompleteVanillaStateTransition(NativePointer<X64SmartCPUContext> context)
        {
            if (!correctionAvailable || !settings.EnableMod || !settings.EnableStuckApothecaryFix)
                return;

            try
            {
                int unitId = checked((int)context.Pointer->RBP);
                if (!TryGetHealer(unitId, out GameUnit* healer))
                {
                    throw new InvalidOperationException(
                        $"The native healer ID did not resolve to a living apothecary: unit={unitId}.");
                }

                if (healer->r_AIState != WaitingState)
                {
                    throw new InvalidOperationException(
                        $"The matched branch was reached with unexpected AI state {healer->r_AIState}: " +
                        $"unit={unitId}, global={healer->r_GlobalId}.");
                }

                // Vanilla's parallel timeout branch performs both writes before
                // leaving the building. FE20 drives the transition outward; retaining
                // 0200 would unregister the healer spatially and make it invisible.
                healer->r_AIState = VanillaTransitionState;
                healer->UnknownRelevant2 = LeavingBuildingTransition;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL stuck-apothecary transition corrected: unit={unitId}, " +
                    $"global={healer->r_GlobalId}, position={healer->r_CurrentTilePositionX}," +
                    $"{healer->r_CurrentTilePositionY}, state=2->109, " +
                    $"buildingTransition=0x{healer->UnknownRelevant2:X4}, vanillaNextState=5.");
            }
            catch (Exception ex)
            {
                DisableCorrection(
                    "runtime validation of the matched Vanilla branch failed; Vanilla behavior remains active",
                    ex);
            }
        }

        private void DisableCorrection(string reason, Exception ex)
        {
            if (!correctionAvailable)
                return;

            correctionAvailable = false;
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL stuck-apothecary fix disabled for this process because {reason}: {ex}");
        }

        private static bool TryGetHealer(int unitId, out GameUnit* healer)
        {
            healer = null;
            return unitId > 0 &&
                GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out healer) &&
                healer != null &&
                healer->r_AliveState == AliveState.IsAlive &&
                healer->r_UnitChimp == eChimps.CHIMP_TYPE_HEALER;
        }
    }

}
