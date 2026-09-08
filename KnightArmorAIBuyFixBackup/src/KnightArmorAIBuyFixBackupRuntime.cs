using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;

namespace KnightArmorAIBuyFixBackup
{
    internal sealed unsafe class KnightArmorAIBuyFixBackupRuntime : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RecruitEuropeanUnitDelegate(
            GameUnitManager* unitManager,
            int unitType,
            int spawnContext,
            int playerId,
            int validationOnly);

        private readonly ManualLogSource log;
        private readonly DetourHandle<RecruitEuropeanUnitDelegate> recruitHook =
            new DetourHandle<RecruitEuropeanUnitDelegate>();
        private HookTransaction transaction;
        private bool disposed;

        internal KnightArmorAIBuyFixBackupRuntime(
            ManualLogSource log,
            SHCDESE.API.LowLevel.CrusaderLibraryLoadContext context,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!referenceHashMatches)
                throw new InvalidOperationException("The fixed recruitment-result layout is not validated for this CrusaderDE.dll.");
            if (!string.Equals(
                    KnightArmorAIBuyFixNativeDefinition.ReferenceSha256,
                    Shared.DebugLogHelper.CurrentNativeSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The backup mod and shared native hash contracts disagree.");
            }

            ValidateManagedLayout();
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                context.Memory,
                KnightArmorAIBuyFixNativeDefinition.RecruitEuropeanUnitPattern,
                KnightArmorAIBuyFixNativeDefinition.RecruitEuropeanUnitRva,
                referenceHashMatches,
                "European troop recruitment",
                log);
            if (resolution.Rva != KnightArmorAIBuyFixNativeDefinition.RecruitEuropeanUnitRva)
                throw new InvalidOperationException("The recruitment function resolved outside its audited RVA.");

            HookTransaction pending = null;
            try
            {
                pending = new HookTransaction(
                    context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                pending.AddDetour(
                    recruitHook,
                    HookTarget.FromAddress(
                        unchecked((ulong)context.ModuleHandle.ToInt64()) + unchecked((ulong)resolution.Rva)),
                    RecruitEuropeanUnit);
                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess || !recruitHook.Success)
                    throw new InvalidOperationException($"The recruitment detour was not installed: {result}.");

                transaction = pending;
                pending = null;
            }
            catch
            {
                pending?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Knight Armor AI Buy Fix Backup active: method={resolution.Method}, rva=0x{resolution.Rva:X}, " +
                "missingGoodReset=STORED_NULL, coverage=sharedEuropeanRecruitmentEntry.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            transaction?.Dispose();
            transaction = null;
        }

        private int RecruitEuropeanUnit(
            GameUnitManager* unitManager,
            int unitType,
            int spawnContext,
            int playerId,
            int validationOnly)
        {
            if (unitManager != null)
            {
                // This is an output of each recruitment attempt. Vanilla overwrites it for
                // real goods shortages but not for the special horse-only failure.
                unitManager->r_RecruitmentResultMissingGoodId = (int)eGoods.STORED_NULL;
            }

            return recruitHook.Original(
                unitManager,
                unitType,
                spawnContext,
                playerId,
                validationOnly);
        }

        private static void ValidateManagedLayout()
        {
            if (Marshal.OffsetOf(typeof(GameUnitManager), nameof(GameUnitManager.r_RecruitmentResultFailureReason)).ToInt32() != 0x650 ||
                Marshal.OffsetOf(typeof(GameUnitManager), nameof(GameUnitManager.r_RecruitmentResultMissingGoodId)).ToInt32() != 0x654 ||
                Marshal.OffsetOf(typeof(GameUnitManager), nameof(GameUnitManager.EmptyUnitFillValue)).ToInt32() != 0x658 ||
                Marshal.OffsetOf(typeof(GameUnitManager), nameof(GameUnitManager.LastOrderedUnit)).ToInt32() != 0x65C ||
                Marshal.SizeOf(typeof(GameUnitManager)) != 0xF7C)
            {
                throw new InvalidOperationException("The Script Extender GameUnitManager layout differs from the audited 2.3.0 contract.");
            }
        }
    }
}
