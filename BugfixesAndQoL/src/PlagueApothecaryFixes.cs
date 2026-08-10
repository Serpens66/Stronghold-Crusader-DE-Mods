// Feature: Correct Vanilla plague treatment and the apothecary state-2 transition.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace BugfixesAndQoL
{
    internal sealed unsafe class PlagueTreatmentFadeFix : IDisposable
    {
        private const int TreatmentTransitionPhase = 1016;
        private const int FirstFadePhase = 1017;
        private const int AreaTreatmentDistance = 7;
        private const int HealerTargetSlotOffset = 0x39A;
        private const int HealerTargetGlobalIdOffset = 0x39C;

        // c_game_projectile_disease_treat_near_healer, reference RVA 0xA0420.
        // Wildcards cover addresses and branch distances which commonly move after updates.
        private const string AreaTreatmentPattern =
            "40 55 56 57 48 83 EC 30 BF 01 00 00 00 48 63 EA 48 8B F1 " +
            "39 79 04 0F 8E ?? ?? ?? ?? 48 89 5C 24 50 48 8D 99 26 01 00 00 " +
            "4C 89 64 24 58 4C 8D 25 ?? ?? ?? ?? 4C 89 74 24 60 " +
            "41 BE E8 03 00 00 4C 89 7C 24 68 45 8D 7E 10";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AreaTreatmentDelegate(IntPtr projectileManager, int nativeUnitId);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<AreaTreatmentDelegate>> areaTreatmentHook =
            new HookRef<X64ManagedFunctionDetourAOB<AreaTreatmentDelegate>>();
        private bool correctionAvailable = true;
        private bool disposed;

        public PlagueTreatmentFadeFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            PlagueNativePatternValidator.ValidateUnique(
                memory,
                AreaTreatmentPattern,
                "plague area-treatment function");

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddDetour(
                    ref areaTreatmentHook,
                    AreaTreatmentPattern,
                    TreatNearHealer);
                transaction.Commit();

                if (!areaTreatmentHook.Success)
                    throw new InvalidOperationException("The plague area-treatment hook was not installed.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Bugfixes and QoL plague-cloud removal fix initialized from a unique native signature.");
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
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
        }

        private void TreatNearHealer(IntPtr projectileManager, int nativeUnitId)
        {
            // Vanilla must always run exactly once, including when this fix is disabled.
            areaTreatmentHook.Value.Hook.Trampoline(projectileManager, nativeUnitId);
            if (!correctionAvailable || !settings.EnableMod)
                return;

            try
            {
                TreatmentResult result = AdvanceTreatedDiseasesToFade(nativeUnitId);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Bugfixes and QoL plague-cloud removal applied: unit={nativeUnitId}, " +
                    $"directTargetAdvancedTo1017={result.DirectTargetAdvanced}, " +
                    $"nearbyAdvancedTo1017={result.NearbyAdvancedCount}.");
            }
            catch (Exception ex)
            {
                DisableCorrection(
                    "runtime validation or phase correction failed; Vanilla treatment remains active",
                    ex);
            }
        }

        private static TreatmentResult AdvanceTreatedDiseasesToFade(int unitId)
        {
            if (!TryGetHealer(unitId, out GameUnit* healer))
                throw new InvalidOperationException($"The treating apothecary could not be resolved: unit={unitId}.");

            ushort healerX = healer->r_CurrentTilePositionX;
            ushort healerY = healer->r_CurrentTilePositionY;
            ushort targetSlot = *(ushort*)((byte*)healer + HealerTargetSlotOffset);
            uint targetGlobalId = *(uint*)((byte*)healer + HealerTargetGlobalIdOffset);
            bool directTargetAdvanced = false;

            if (targetSlot > 0 &&
                GameProjectileManagerAPI.Instance.TryGetProjectileById(targetSlot, out GameProjectile* target) &&
                target != null &&
                target->r_AliveState == AliveState.IsAlive &&
                target->r_ProjectileType == ProjectileType.Disease &&
                target->r_GlobalId == targetGlobalId &&
                ReadPhase(target) == TreatmentTransitionPhase)
            {
                WritePhase(target, FirstFadePhase);
                directTargetAdvanced = true;
            }

            int nearbyAdvancedCount = 0;
            Span<GameProjectile> projectiles = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            for (int index = 0; index < projectiles.Length; index++)
            {
                ref GameProjectile projectile = ref projectiles[index];
                if (projectile.r_AliveState != AliveState.IsAlive ||
                    projectile.r_ProjectileType != ProjectileType.Disease ||
                    ReadPhase(ref projectile) != TreatmentTransitionPhase ||
                    VanillaAreaDistance(
                        projectile.r_CurrentTileX,
                        projectile.r_CurrentTileY,
                        healerX,
                        healerY) >= AreaTreatmentDistance)
                {
                    continue;
                }

                WritePhase(ref projectile, FirstFadePhase);
                nearbyAdvancedCount++;
            }

            return new TreatmentResult(directTargetAdvanced, nearbyAdvancedCount);
        }

        private void DisableCorrection(string reason, Exception ex)
        {
            if (!correctionAvailable)
                return;

            correctionAvailable = false;
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL plague-cloud removal fix disabled for this process because {reason}: {ex}");
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

        private static ushort ReadPhase(GameProjectile* projectile)
        {
            // The Script Extender owns the packed projectile layout; using its field
            // keeps this access aligned when that interop struct is updated.
            return projectile->r_Unknown4;
        }

        private static ushort ReadPhase(ref GameProjectile projectile)
        {
            fixed (GameProjectile* pointer = &projectile)
                return ReadPhase(pointer);
        }

        private static void WritePhase(GameProjectile* projectile, int phase)
        {
            projectile->r_Unknown4 = checked((ushort)phase);
        }

        private static void WritePhase(ref GameProjectile projectile, int phase)
        {
            fixed (GameProjectile* pointer = &projectile)
                WritePhase(pointer, phase);
        }

        private static int VanillaAreaDistance(int firstX, int firstY, int secondX, int secondY)
        {
            return Math.Max(Math.Abs(firstX - secondX), Math.Abs(firstY - secondY));
        }

        private readonly struct TreatmentResult
        {
            public TreatmentResult(bool directTargetAdvanced, int nearbyAdvancedCount)
            {
                DirectTargetAdvanced = directTargetAdvanced;
                NearbyAdvancedCount = nearbyAdvancedCount;
            }

            public bool DirectTargetAdvanced { get; }
            public int NearbyAdvancedCount { get; }
        }
    }

    internal sealed unsafe class PlagueApothecaryStateTransitionFix : IDisposable
    {
        private const ushort WaitingState = 2;
        private const ushort VanillaTransitionState = 109;
        private const ushort LeavingBuildingTransition = 0xFE20;

        // c_game_unit_healer_update, successful ten-tick search in state 2,
        // reference RVA 0x14F82C. The hook runs only after the preceding search
        // returned a disease object and before Vanilla writes next-state 5.
        private const string PeriodicDiseaseFoundPattern =
            "48 63 15 ?? ?? ?? ?? 41 BF 05 00 00 00 4C 69 C2 90 04 00 00 " +
            "41 BE 14 00 00 00 41 BC F0 D8 FF FF 66 47 89 BC 28 1A 09 00 00";

        // c_game_unit_healer_update, regular state-2 timeout exit, reference RVA
        // 0x14F6C8. This is the Vanilla model for the omitted transition writes.
        private const string WorkingBuildingExitReferencePattern =
            "B8 6D 00 00 00 66 42 89 84 2B 18 09 00 00 8B D5 " +
            "66 42 C7 84 2B 86 09 00 00 20 FE";

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private HookTransaction transaction;
        private HookRef<X64InlineHook> transitionHook = new HookRef<X64InlineHook>();
        private bool correctionAvailable = true;
        private bool disposed;

        public PlagueApothecaryStateTransitionFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            PlagueNativePatternValidator.ValidateUnique(
                memory,
                PeriodicDiseaseFoundPattern,
                "apothecary state-2 disease-found branch");
            PlagueNativePatternValidator.ValidateUnique(
                memory,
                WorkingBuildingExitReferencePattern,
                "apothecary working state-2 building-exit reference");

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref transitionHook,
                    PeriodicDiseaseFoundPattern,
                    CompleteVanillaStateTransition,
                    regs: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBP,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();

                if (!transitionHook.Success)
                    throw new InvalidOperationException("The apothecary state-2 transition hook was not installed.");

                Shared.DebugLogHelper.LogInfo(
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
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
        }

        private void CompleteVanillaStateTransition(NativePointer<X64SmartCPUContext> context)
        {
            if (!correctionAvailable || !settings.EnableMod)
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
                Shared.DebugLogHelper.LogInfo(
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

    internal static class PlagueNativePatternValidator
    {
        public static void ValidateUnique(ReadOnlySpan<byte> memory, string pattern, string name)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int[] expected = new int[tokens.Length];
            for (int index = 0; index < tokens.Length; index++)
            {
                if (tokens[index] == "??")
                {
                    expected[index] = -1;
                    continue;
                }

                if (!byte.TryParse(
                        tokens[index],
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out byte value))
                {
                    throw new InvalidOperationException($"Invalid AOB token '{tokens[index]}' in {name}.");
                }
                expected[index] = value;
            }

            int matchCount = 0;
            for (int offset = 0; offset <= memory.Length - expected.Length; offset++)
            {
                bool matches = true;
                for (int index = 0; index < expected.Length; index++)
                {
                    if (expected[index] >= 0 && memory[offset + index] != expected[index])
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches)
                    continue;

                matchCount++;
                if (matchCount > 1)
                    break;
            }

            if (matchCount != 1)
            {
                throw new InvalidOperationException(
                    $"The {name} signature matched {matchCount} times instead of exactly once.");
            }
        }
    }
}
