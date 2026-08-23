// Feature: Prevent a horse-only recruitment failure from reusing a stale missing-good id.
using BepInEx.Logging;
using System;
using System.Runtime.InteropServices;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AiRecruitmentHorseDemandFix : IDisposable
    {
        private const int PlayerSlotCount = 9;
        private const int PeriodicHorseBlockLogInterval = 100;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly int[] horseBlockCountByPlayer = new int[PlayerSlotCount];
        private readonly int[] staleGoodPreventionCountByPlayer = new int[PlayerSlotCount];
        private readonly bool[] swordShortageLoggedByPlayer = new bool[PlayerSlotCount];
        private readonly bool[] armourShortageLoggedByPlayer = new bool[PlayerSlotCount];
        private readonly bool[] postHorseBlockSuccessLoggedByPlayer = new bool[PlayerSlotCount];
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<RecruitEuropeanUnitDelegate>> recruitHook =
            new HookRef<X64ManagedFunctionDetourAOB<RecruitEuropeanUnitDelegate>>();
        private bool disposed;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RecruitEuropeanUnitDelegate(
            IntPtr unitManager,
            int unitType,
            int spawnContext,
            int playerId,
            int validationOnly);

        public AiRecruitmentHorseDemandFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AiRecruitmentHorseDemandNativeDefinition.RecruitEuropeanUnitPattern,
                AiRecruitmentHorseDemandNativeDefinition.RecruitEuropeanUnitRva,
                referenceHashMatches,
                "European troop recruitment",
                log);

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddDetour(
                    ref recruitHook,
                    libraryBase + unchecked((ulong)resolution.Rva),
                    RecruitEuropeanUnit);
                transaction.Commit();

                if (!recruitHook.Success)
                    throw new InvalidOperationException("The European troop recruitment hook was not installed.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Bugfixes and QoL AI recruitment horse-demand hook installed: " +
                    $"method={resolution.Method}, rva=0x{resolution.Rva:X}, enabled={IsEnabled}. " +
                    "Diagnostics log the first relevant result per player and summarize repeated horse blocks every 100 occurrences.");

                if (!referenceHashMatches)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Bugfixes and QoL AI recruitment horse-demand fix is running on an unknown CrusaderDE.dll because the European troop recruitment signature was uniquely validated.");
                }
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
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
        }

        private int RecruitEuropeanUnit(
            IntPtr unitManager,
            int unitType,
            int spawnContext,
            int playerId,
            int validationOnly)
        {
            bool enabled = unitManager != IntPtr.Zero && IsEnabled;
            int staleMissingGoodId = 0;
            if (enabled)
            {
                // Vanilla clears the error code but not this companion output. A horse-only
                // failure would otherwise leave the AI reading an earlier weapon id.
                staleMissingGoodId = ReadManagerInt(
                    unitManager,
                    AiRecruitmentHorseDemandNativeDefinition.MissingGoodIdOffset);
                WriteManagerInt(
                    unitManager,
                    AiRecruitmentHorseDemandNativeDefinition.MissingGoodIdOffset,
                    0);
            }

            int vanillaResult = recruitHook.Value.Hook.Trampoline(
                unitManager,
                unitType,
                spawnContext,
                playerId,
                validationOnly);

            if (enabled && unitType == AiRecruitmentHorseDemandNativeDefinition.KnightUnitType)
                LogKnightResult(unitManager, playerId, validationOnly, vanillaResult, staleMissingGoodId);

            return vanillaResult;
        }

        public void LogSettingState(string reason)
        {
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Bugfixes and QoL AI recruitment horse-demand fix state: enabled={IsEnabled}, " +
                $"EnableMod={settings.EnableMod}, EnableAiFixes={settings.EnableAiFixes}, reason={reason}.");
        }

        private bool IsEnabled => settings.EnableMod && settings.EnableAiFixes;

        private void LogKnightResult(
            IntPtr unitManager,
            int playerId,
            int validationOnly,
            int vanillaResult,
            int staleMissingGoodId)
        {
            int resultCode = ReadManagerInt(
                unitManager,
                AiRecruitmentHorseDemandNativeDefinition.ResultCodeOffset);
            int missingGoodId = ReadManagerInt(
                unitManager,
                AiRecruitmentHorseDemandNativeDefinition.MissingGoodIdOffset);
            int slot = playerId >= 0 && playerId < PlayerSlotCount ? playerId : 0;

            if (AiRecruitmentHorseDemandNativeDefinition.IsKnightHorseOnlyFailure(
                    AiRecruitmentHorseDemandNativeDefinition.KnightUnitType,
                    resultCode,
                    missingGoodId))
            {
                int count = ++horseBlockCountByPlayer[slot];
                bool preventedStaleEquipment =
                    staleMissingGoodId == AiRecruitmentHorseDemandNativeDefinition.SwordGoodId ||
                    staleMissingGoodId == AiRecruitmentHorseDemandNativeDefinition.MetalArmourGoodId;
                if (preventedStaleEquipment)
                    staleGoodPreventionCountByPlayer[slot]++;

                if (count == 1 || preventedStaleEquipment || count % PeriodicHorseBlockLogInterval == 0)
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Bugfixes and QoL AI fix observed horse-blocked knight recruitment: " +
                        $"player={playerId}, validationOnly={validationOnly}, vanillaReturn={vanillaResult}, " +
                        $"resultCode={resultCode}, missingGoodAfter={missingGoodId}, " +
                        $"staleGoodBefore={staleMissingGoodId}, preventedStaleEquipment={preventedStaleEquipment}, " +
                        $"horseBlocks={count}, staleEquipmentPrevented={staleGoodPreventionCountByPlayer[slot]}.");
                }

                return;
            }

            if (AiRecruitmentHorseDemandNativeDefinition.IsKnightEquipmentFailure(
                    AiRecruitmentHorseDemandNativeDefinition.KnightUnitType,
                    resultCode,
                    missingGoodId))
            {
                bool alreadyLogged = missingGoodId == AiRecruitmentHorseDemandNativeDefinition.SwordGoodId
                    ? swordShortageLoggedByPlayer[slot]
                    : armourShortageLoggedByPlayer[slot];
                if (!alreadyLogged)
                {
                    if (missingGoodId == AiRecruitmentHorseDemandNativeDefinition.SwordGoodId)
                        swordShortageLoggedByPlayer[slot] = true;
                    else
                        armourShortageLoggedByPlayer[slot] = true;

                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Bugfixes and QoL AI fix preserved a genuine knight equipment shortage: " +
                        $"player={playerId}, validationOnly={validationOnly}, vanillaReturn={vanillaResult}, " +
                        $"resultCode={resultCode}, missingGood={missingGoodId}.");
                }

                return;
            }

            if (resultCode == 0 &&
                horseBlockCountByPlayer[slot] > 0 &&
                !postHorseBlockSuccessLoggedByPlayer[slot])
            {
                postHorseBlockSuccessLoggedByPlayer[slot] = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Bugfixes and QoL AI fix observed a knight recruitment check without an error after earlier horse blocks: " +
                    $"player={playerId}, validationOnly={validationOnly}, vanillaReturn={vanillaResult}, " +
                    $"earlierHorseBlocks={horseBlockCountByPlayer[slot]}.");
            }
        }

        private static int ReadManagerInt(IntPtr unitManager, int offset) =>
            *(int*)((byte*)unitManager.ToPointer() + offset);

        private static void WriteManagerInt(IntPtr unitManager, int offset, int value) =>
            *(int*)((byte*)unitManager.ToPointer() + offset) = value;
    }
}
