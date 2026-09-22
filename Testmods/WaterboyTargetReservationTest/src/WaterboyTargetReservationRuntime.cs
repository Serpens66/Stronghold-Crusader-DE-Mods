using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using R3;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WaterboyTargetReservationTest
{
    internal sealed unsafe class WaterboyTargetReservationRuntime
    {
        private const int MaximumDetailedLogs = 200;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int FindNearestBurningBuildingDelegate(IntPtr buildingManager, int nativeUnitId);

        private readonly ManualLogSource log;
        private readonly ReservationLedger ledger = new ReservationLedger();
        private readonly List<FireReservation> reservationScratch = new List<FireReservation>();
        private readonly List<MaskedFire> maskedFireScratch = new List<MaskedFire>();
        private readonly DetourHandle<FindNearestBurningBuildingDelegate> targetSearchHook =
            new DetourHandle<FindNearestBurningBuildingDelegate>();
        private readonly HookTransaction transaction;
        private readonly IDisposable mapUnloadSubscription;
        private readonly int targetSearchDisplacedByteCount;
        private bool correctionAvailable = true;
        private int detailedLogCount;
        private bool detailLimitLogged;

        internal WaterboyTargetReservationRuntime(
            ManualLogSource log,
            CrusaderLibraryLoadContext context,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!referenceHashMatches ||
                !string.Equals(
                    WaterboyNativeDefinition.ReferenceSha256,
                    Shared.DebugLogHelper.CurrentNativeSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The waterboy native contract is not validated for this CrusaderDE.dll.");
            }

            ValidateManagedLayout();
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                context.Memory,
                WaterboyNativeDefinition.FindNearestBurningBuildingPattern,
                WaterboyNativeDefinition.FindNearestBurningBuildingRva,
                referenceHashMatches,
                "waterboy nearest-burning-building search",
                log);
            if (resolution.Rva != WaterboyNativeDefinition.FindNearestBurningBuildingRva)
                throw new InvalidOperationException("The waterboy target search resolved outside its audited RVA.");

            HookTransaction pending = null;
            IDisposable pendingMapUnloadSubscription = null;
            bool pendingGameTickSubscription = false;
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
                    targetSearchHook,
                    HookTarget.FromAddress(
                        unchecked((ulong)context.ModuleHandle.ToInt64()) + unchecked((ulong)resolution.Rva)),
                    FindNearestUnreservedFire);
                CommitResult result = pending.Commit();
                NativeDetour<FindNearestBurningBuildingDelegate> committedDetour =
                    targetSearchHook.Hook as NativeDetour<FindNearestBurningBuildingDelegate>;
                if (!result.IsCompleteSuccess || !targetSearchHook.Success)
                    throw new InvalidOperationException($"The waterboy target-search detour was not installed: {result}.");
                if (committedDetour == null ||
                    committedDetour.DisplacedByteCount !=
                        WaterboyNativeDefinition.FindNearestBurningBuildingDisplacedLength)
                {
                    throw new InvalidOperationException(
                        $"The waterboy target-search detour displaced {committedDetour?.DisplacedByteCount ?? 0} bytes; " +
                        $"expected {WaterboyNativeDefinition.FindNearestBurningBuildingDisplacedLength}.");
                }

                GameTimeManagerAPI.Instance.GetFrameProvider().OnGameTick += OnGameTick;
                pendingGameTickSubscription = true;
                pendingMapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(_ => OnMapUnload());

                transaction = pending;
                mapUnloadSubscription = pendingMapUnloadSubscription;
                targetSearchDisplacedByteCount = committedDetour.DisplacedByteCount;
                pending = null;
                pendingMapUnloadSubscription = null;
                pendingGameTickSubscription = false;
            }
            catch
            {
                if (pendingGameTickSubscription)
                    GameTimeManagerAPI.Instance.GetFrameProvider().OnGameTick -= OnGameTick;
                pendingMapUnloadSubscription?.Dispose();
                pending?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Waterboy target reservations active: method={resolution.Method}, rva=0x{resolution.Rva:X}, " +
                $"displaced={targetSearchDisplacedByteCount}, stationaryTimeoutTicks=" +
                $"{ReservationLedger.StationaryTimeoutTicks}, baseGameSpeed=40, linkedCompounds=true.");
        }

        private int FindNearestUnreservedFire(IntPtr buildingManager, int nativeUnitId)
        {
            if (!correctionAvailable)
                return targetSearchHook.Original(buildingManager, nativeUnitId);

            List<MaskedFire> masked = maskedFireScratch;
            masked.Clear();
            NativeIdentity requester;
            try
            {
                requester = ResolveLivingFiremanIdentity(nativeUnitId);
                int currentTick = CurrentGameTick();
                PruneInvalidReservations(currentTick);
                GameUnit* requesterUnit = ResolveLivingFireman(requester);
                bool hasSuppressedCoverage = TryGetValidSuppressedCoverage(
                    requester,
                    requesterUnit->r_CurrentTilePositionX,
                    requesterUnit->r_CurrentTilePositionY,
                    out NativeIdentity suppressedTarget,
                    out uint suppressedCompoundKey);
                MaskUnavailableFires(
                    requester.GlobalId,
                    hasSuppressedCoverage,
                    suppressedTarget,
                    suppressedCompoundKey,
                    masked);
            }
            catch (Exception exception)
            {
                RestoreMaskedFires(masked, disableOnFailure: false);
                masked.Clear();
                DisableCorrection("pre-search validation or masking failed", exception);
                return targetSearchHook.Original(buildingManager, nativeUnitId);
            }

            int selectedBuildingId;
            try
            {
                selectedBuildingId = targetSearchHook.Original(buildingManager, nativeUnitId);
            }
            finally
            {
                RestoreMaskedFires(masked, disableOnFailure: true);
                masked.Clear();
            }

            if (!correctionAvailable)
                return selectedBuildingId;

            try
            {
                if (selectedBuildingId == 0)
                {
                    ledger.Release(requester.GlobalId, clearSuppression: false);
                    return 0;
                }

                GameUnit* owner = ResolveLivingFireman(requester);
                GameBuilding* target = ResolveBurningBuilding(selectedBuildingId);
                NativeIdentity targetIdentity = new NativeIdentity(selectedBuildingId, target->r_GlobalId);
                uint compoundKey = ReadCompoundKey(target);
                ReservationClaimResult result = ledger.Claim(
                    requester,
                    targetIdentity,
                    compoundKey,
                    owner->r_CurrentTilePositionX,
                    owner->r_CurrentTilePositionY,
                    CurrentGameTick());
                if (result == ReservationClaimResult.Conflict)
                {
                    throw new InvalidOperationException(
                        $"Vanilla selected an already reserved fire: owner={requester}, target={targetIdentity}, compound={compoundKey}.");
                }

                if (result == ReservationClaimResult.Claimed)
                {
                    LogDetail(
                        $"waterboy reservation created: owner={requester}, target={targetIdentity}, compound={compoundKey}.");
                }
            }
            catch (Exception exception)
            {
                DisableCorrection("post-search reservation failed", exception);
            }

            return selectedBuildingId;
        }

        private void OnGameTick(int currentTick)
        {
            if (!correctionAvailable)
                return;

            try
            {
                PruneInvalidReservations(currentTick);
            }
            catch (Exception exception)
            {
                DisableCorrection("per-tick reservation validation failed", exception);
            }
        }

        private void OnMapUnload()
        {
            ledger.Clear();
            reservationScratch.Clear();
            maskedFireScratch.Clear();
        }

        private void PruneInvalidReservations(int currentTick)
        {
            ledger.CopyReservationsTo(reservationScratch);
            foreach (FireReservation reservation in reservationScratch)
            {
                bool ownerValid = TryResolveLivingFireman(reservation.Owner, out GameUnit* owner);
                NativeIdentity currentTarget = default;
                int ownerState = 0;
                ushort x = 0;
                ushort y = 0;
                if (ownerValid)
                {
                    ownerState = owner->r_AIState;
                    currentTarget = new NativeIdentity(
                        *(ushort*)((byte*)owner + WaterboyNativeDefinition.FiremanTargetSlotOffset),
                        *(uint*)((byte*)owner + WaterboyNativeDefinition.FiremanTargetGlobalIdOffset));
                    x = owner->r_CurrentTilePositionX;
                    y = owner->r_CurrentTilePositionY;
                }

                bool targetBurning = TryResolveBurningBuilding(
                    reservation.Target,
                    out GameBuilding* target);
                uint compoundKey = targetBurning ? ReadCompoundKey(target) : 0;
                ReservationReconcileResult result = ledger.Reconcile(
                    reservation,
                    ownerValid,
                    ownerState,
                    currentTarget,
                    targetBurning,
                    compoundKey,
                    x,
                    y,
                    currentTick);
                if (result == ReservationReconcileResult.Released)
                {
                    LogDetail($"waterboy reservation released as invalid: owner={reservation.Owner}, target={reservation.Target}.");
                }
                else if (result == ReservationReconcileResult.Stalled)
                {
                    LogDetail($"waterboy reservation released after stationary timeout: owner={reservation.Owner}, target={reservation.Target}.");
                }
            }
        }

        private bool TryGetValidSuppressedCoverage(
            NativeIdentity requester,
            ushort x,
            ushort y,
            out NativeIdentity targetIdentity,
            out uint compoundKey)
        {
            if (!ledger.TryGetSuppressedCoverage(requester, x, y, out targetIdentity, out compoundKey))
                return false;

            if (!TryResolveBurningBuilding(targetIdentity, out GameBuilding* target) ||
                ReadCompoundKey(target) != compoundKey)
            {
                ledger.ClearSuppression(requester.GlobalId);
                targetIdentity = default;
                compoundKey = 0;
                return false;
            }

            return true;
        }

        private void MaskUnavailableFires(
            uint requesterGlobalId,
            bool hasSuppressedCoverage,
            NativeIdentity suppressedTarget,
            uint suppressedCompoundKey,
            List<MaskedFire> masked)
        {
            int buildingCount = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan().Length;
            for (int buildingId = 1; buildingId <= buildingCount; buildingId++)
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                    building == null || building->r_AliveState != AliveState.IsAlive || building->r_OnFireTicks == 0)
                {
                    continue;
                }

                NativeIdentity identity = new NativeIdentity(buildingId, building->r_GlobalId);
                uint compoundKey = ReadCompoundKey(building);
                bool coveredByForeignOwner =
                    ledger.IsCoveredByForeignOwner(identity, compoundKey, requesterGlobalId);
                bool coveredBySuppression = hasSuppressedCoverage &&
                    ReservationLedger.CoversSuppression(
                        identity,
                        compoundKey,
                        suppressedTarget,
                        suppressedCompoundKey);
                if (!coveredByForeignOwner && !coveredBySuppression)
                    continue;

                masked.Add(new MaskedFire(identity, building->r_OnFireTicks));
                building->r_OnFireTicks = 0;
            }
        }

        private void RestoreMaskedFires(List<MaskedFire> masked, bool disableOnFailure)
        {
            Exception firstFailure = null;
            foreach (MaskedFire item in masked)
            {
                try
                {
                    if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(item.Identity.Slot, out GameBuilding* building) ||
                        building == null || building->r_GlobalId != item.Identity.GlobalId)
                    {
                        throw new InvalidOperationException($"Masked building identity changed: {item.Identity}.");
                    }
                    if (building->r_OnFireTicks != 0)
                    {
                        throw new InvalidOperationException(
                            $"Masked building fire counter changed unexpectedly: {item.Identity}, ticks={building->r_OnFireTicks}.");
                    }
                    building->r_OnFireTicks = item.OriginalFireTicks;
                }
                catch (Exception exception)
                {
                    if (firstFailure == null)
                        firstFailure = exception;
                }
            }

            if (firstFailure == null)
                return;
            if (disableOnFailure)
                DisableCorrection("temporary fire counters could not be restored safely", firstFailure);
            else
                Shared.DebugLogHelper.LogError(log, $"Waterboy reservation mask rollback failed: {firstFailure}");
        }

        private NativeIdentity ResolveLivingFiremanIdentity(int unitId)
        {
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive ||
                unit->r_UnitChimp != eChimps.CHIMP_TYPE_FIREMAN || unit->r_GlobalId == 0)
            {
                throw new InvalidOperationException($"Target search received an invalid fireman unit: {unitId}.");
            }
            return new NativeIdentity(unitId, unit->r_GlobalId);
        }

        private static GameUnit* ResolveLivingFireman(NativeIdentity identity)
        {
            if (!TryResolveLivingFireman(identity, out GameUnit* unit))
                throw new InvalidOperationException($"Fireman identity is no longer valid: {identity}.");
            return unit;
        }

        private static bool TryResolveLivingFireman(NativeIdentity identity, out GameUnit* unit)
        {
            return GameUnitManagerAPI.Instance.TryGetUnitById(identity.Slot, out unit) &&
                unit != null && unit->r_GlobalId == identity.GlobalId &&
                unit->r_AliveState == AliveState.IsAlive &&
                unit->r_UnitChimp == eChimps.CHIMP_TYPE_FIREMAN;
        }

        private static GameBuilding* ResolveBurningBuilding(int buildingId)
        {
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building == null || building->r_AliveState != AliveState.IsAlive ||
                building->r_GlobalId == 0 || building->r_OnFireTicks == 0)
            {
                throw new InvalidOperationException($"Vanilla returned a building that is no longer burning: {buildingId}.");
            }
            return building;
        }

        private static bool TryResolveBurningBuilding(NativeIdentity identity, out GameBuilding* building)
        {
            return GameBuildingManagerAPI.Instance.TryGetBuildingById(identity.Slot, out building) &&
                building != null && building->r_GlobalId == identity.GlobalId &&
                building->r_AliveState == AliveState.IsAlive && building->r_OnFireTicks != 0;
        }

        private static uint ReadCompoundKey(GameBuilding* building) =>
            *(uint*)((byte*)building + WaterboyNativeDefinition.ManagedBuildingCompoundKeyOffset);

        private static int CurrentGameTick() =>
            GameTimeManagerAPI.Instance.GetFrameProvider().CurrentGameTick;

        private static void ValidateManagedLayout()
        {
            if (Marshal.SizeOf(typeof(GameBuilding)) != 0x32C ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_AliveState)).ToInt32() != 0xD0 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_GlobalId)).ToInt32() != 0xD8 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_OnFireTicks)).ToInt32() !=
                    WaterboyNativeDefinition.ManagedBuildingFireTicksOffset ||
                Marshal.SizeOf(typeof(GameUnit)) != 0x490 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_GlobalId)).ToInt32() != 0x94 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AIState)).ToInt32() != 0x2BC)
            {
                throw new InvalidOperationException("The installed Script Extender layouts differ from the audited waterboy contract.");
            }
        }

        private void DisableCorrection(string reason, Exception exception)
        {
            if (!correctionAvailable)
                return;

            correctionAvailable = false;
            ledger.Clear();
            Shared.DebugLogHelper.LogError(
                log,
                $"Waterboy target reservations disabled; Vanilla remains active. reason={reason}; exception={exception}");
        }

        private void LogDetail(string message)
        {
            if (detailedLogCount < MaximumDetailedLogs)
            {
                detailedLogCount++;
                Shared.DebugLogHelper.LogDebug(log, message);
                return;
            }
            if (detailLimitLogged)
                return;

            detailLimitLogged = true;
            Shared.DebugLogHelper.LogDebug(log, "Waterboy reservation detailed-log limit reached; repeated details are suppressed.");
        }

        private readonly struct MaskedFire
        {
            internal MaskedFire(NativeIdentity identity, ushort originalFireTicks)
            {
                Identity = identity;
                OriginalFireTicks = originalFireTicks;
            }

            internal NativeIdentity Identity { get; }
            internal ushort OriginalFireTicks { get; }
        }
    }
}
