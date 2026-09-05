// Feature: Ensure all plague clouds affected by treatment enter Vanilla's fade path.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;


namespace BugfixesAndQoL
{
    internal sealed unsafe class PlagueTreatmentFadeFix : IDisposable
    {
        private const int TreatmentTransitionPhase = 1016;
        private const int FirstFadePhase = 1017;
        private const int AreaTreatmentDistance = 7;
        private const int HealerTargetSlotOffset = 0x39A;
        private const int HealerTargetGlobalIdOffset = 0x39C;

        // c_game_projectile_disease_treat_near_healer, reference RVA 0xA0470.
        // Wildcards cover addresses and branch distances which commonly move after updates.
        private const string AreaTreatmentPattern =
            "40 55 56 57 48 83 EC 30 BF 01 00 00 00 48 63 EA 48 8B F1 " +
            "39 79 04 0F 8E ?? ?? ?? ?? 48 89 5C 24 50 48 8D 99 26 01 00 00 " +
            "4C 89 64 24 58 4C 8D 25 ?? ?? ?? ?? 4C 89 74 24 60 " +
            "41 BE E8 03 00 00 4C 89 7C 24 68 45 8D 7E 10";
        private const int AreaTreatmentRva = 0xA0470;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AreaTreatmentDelegate(IntPtr projectileManager, int nativeUnitId);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private Action<int> treatmentCompletedObserver;
        private HookTransaction transaction;
        private readonly DetourHandle<AreaTreatmentDelegate> areaTreatmentHook =
            new DetourHandle<AreaTreatmentDelegate>();
        private bool correctionAvailable = true;
        private bool disposed;

        public PlagueTreatmentFadeFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            int areaTreatmentRva = PlagueNativePatternValidator.Resolve(
                log,
                memory,
                AreaTreatmentPattern,
                AreaTreatmentRva,
                referenceHashMatches,
                "plague area-treatment function");

            try
            {
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                transaction.AddDetour(
                    areaTreatmentHook,
                    HookTarget.FromAddress(libraryBase + unchecked((ulong)areaTreatmentRva)),
                    TreatNearHealer);
                CommitResult commitResult = transaction.Commit();

                if (!commitResult.IsCompleteSuccess || !areaTreatmentHook.Success)
                    throw new InvalidOperationException("The plague area-treatment hook was not installed.");

                Shared.DebugLogHelper.LogDebug(
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
            transaction?.Dispose();
            transaction = null;
        }

        public void SetTreatmentCompletedObserver(Action<int> observer)
        {
            treatmentCompletedObserver = observer;
        }

        private void TreatNearHealer(IntPtr projectileManager, int nativeUnitId)
        {
            // Vanilla must always run exactly once, including when this fix is disabled.
            areaTreatmentHook.Original(projectileManager, nativeUnitId);

            if (correctionAvailable && settings.EnableMod && settings.EnablePlagueCloudRemovalFix)
            {
                try
                {
                    TreatmentResult result = AdvanceTreatedDiseasesToFade(nativeUnitId);
                    Shared.DebugLogHelper.LogDebug(
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

            // Reservation cleanup is isolated from both Vanilla and the fade correction.
            try
            {
                treatmentCompletedObserver?.Invoke(nativeUnitId);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL plague reservation treatment notification failed; " +
                    $"Vanilla and plague-cloud removal already completed: {ex}");
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

}
