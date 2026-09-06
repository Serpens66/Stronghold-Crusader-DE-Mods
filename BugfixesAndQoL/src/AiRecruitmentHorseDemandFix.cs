// Feature: Prevent a horse-only recruitment failure from reusing a stale missing-good id.
using BepInEx.Logging;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AiRecruitmentHorseDemandFix : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private HookTransaction transaction;
        private readonly DetourHandle<RecruitEuropeanUnitDelegate> recruitHook =
            new DetourHandle<RecruitEuropeanUnitDelegate>();
        private bool disposed;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RecruitEuropeanUnitDelegate(
            GameUnitManager* unitManager,
            int unitType,
            int spawnContext,
            int playerId,
            int validationOnly);

        public AiRecruitmentHorseDemandFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // The entry signature proves the recruitment-result layout, while later
            // branches write the companion missing-good field. Keep the typed write
            // hash-gated because the manager pointer originates in native code.
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The AI recruitment horse-demand fix remains inactive because its fixed recruitment-result layout is not validated for this CrusaderDE.dll.");
            }

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AiRecruitmentHorseDemandNativeDefinition.RecruitEuropeanUnitPattern,
                AiRecruitmentHorseDemandNativeDefinition.RecruitEuropeanUnitRva,
                referenceHashMatches,
                "European troop recruitment",
                log);

            try
            {
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                transaction.AddDetour(
                    recruitHook,
                    HookTarget.FromAddress(libraryBase + unchecked((ulong)resolution.Rva)),
                    RecruitEuropeanUnit);
                CommitResult commitResult = transaction.Commit();

                if (!commitResult.IsCompleteSuccess || !recruitHook.Success)
                    throw new InvalidOperationException("The European troop recruitment hook was not installed.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Bugfixes and QoL AI recruitment horse-demand hook installed: " +
                    $"method={resolution.Method}, rva=0x{resolution.Rva:X}, enabled={IsEnabled}.");

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
            if (unitManager != null && IsEnabled)
            {
                // Vanilla clears the error code but not this companion output. A horse-only
                // failure would otherwise leave the AI reading an earlier weapon id.
                unitManager->r_RecruitmentResultMissingGoodId = 0;
            }

            return recruitHook.Original(
                unitManager,
                unitType,
                spawnContext,
                playerId,
                validationOnly);
        }

        private bool IsEnabled => settings.EnableMod && settings.EnableAiFixes;
    }
}
