// Feature: Reserve an apothecary's complete plague-treatment area during target selection.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.Components.Timer;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;


namespace BugfixesAndQoL
{
    internal sealed unsafe class PlagueTargetReservationFix : IDisposable
    {
        private const int MaximumSelectablePhase = 1000;
        private const ushort TemporarilyMaskedPhase = 1001;
        private const int AreaTreatmentDistance = 7;
        private const int StationaryTimeoutMilliseconds = 10_000;
        private const int HealerTargetSlotOffset = 0x39A;
        private const int HealerTargetGlobalIdOffset = 0x39C;
        private const int HealerNextStateOffset = 0x2BE;
        private const ushort WalkingState = 5;
        private const ushort TreatingState = 6;
        private const ushort TransitionState = 109;
        private const int MaximumDetailedLogs = 200;

        // c_game_projectile_disease_find_nearest_for_healer, reference RVA 0x9F6B0.
        private const string DiseaseSearchPattern =
            "48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 30 4C 63 E2 45 33 C0 4D 69 EC 90 04 00 00 " +
            "48 8D 15 ?? ?? ?? ?? BF 01 00 00 00";

        // Common c_game_unit_healer_update epilogue, reference RVA 0x150107.
        // The following padding and next prologue make this otherwise generic epilogue unique.
        private const string HealerUpdateExitPattern =
            "48 8B 5C 24 60 48 8B 6C 24 68 48 8B 74 24 70 48 83 C4 30 " +
            "41 5F 41 5E 41 5D 41 5C 5F C3 " +
            "CC CC CC CC CC CC CC CC CC CC CC CC " +
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 30";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int DiseaseSearchDelegate(IntPtr projectileManager, int nativeUnitId);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Dictionary<uint, ReservationGroup> groupsByOwner =
            new Dictionary<uint, ReservationGroup>();
        private readonly Dictionary<DiseaseIdentity, uint> ownersByDisease =
            new Dictionary<DiseaseIdentity, uint>();
        private readonly Dictionary<uint, SuppressedAssignment> suppressedByOwner =
            new Dictionary<uint, SuppressedAssignment>();
        // Native healer updates are single-threaded; reuse scratch lists to avoid hot-path allocations.
        private readonly List<MaskedDisease> maskedDiseases = new List<MaskedDisease>();
        private readonly List<KeyValuePair<uint, string>> pendingReleases =
            new List<KeyValuePair<uint, string>>();
        private readonly List<uint> pendingOwnerSlots = new List<uint>();
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<DiseaseSearchDelegate>> diseaseSearchHook =
            new HookRef<X64ManagedFunctionDetourAOB<DiseaseSearchDelegate>>();
        private HookRef<X64InlineHook> healerExitHook = new HookRef<X64InlineHook>();
        private bool correctionAvailable = true;
        private int detailedLogCount;
        private bool detailLimitLogged;
        private bool disposed;

        public PlagueTargetReservationFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            PlagueNativePatternValidator.ValidateUnique(
                memory,
                DiseaseSearchPattern,
                "apothecary nearest-disease search function");
            PlagueNativePatternValidator.ValidateUnique(
                memory,
                HealerUpdateExitPattern,
                "apothecary update common exit");

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddDetour(
                    ref diseaseSearchHook,
                    DiseaseSearchPattern,
                    FindNearestUnreservedDisease);
                transaction.AddContextHook(
                    ref healerExitHook,
                    HealerUpdateExitPattern,
                    ObserveCompletedHealerUpdate,
                    regs: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBP,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();

                if (!diseaseSearchHook.Success || !healerExitHook.Success)
                    throw new InvalidOperationException("The plague target-reservation hooks were not installed.");

                Shared.DebugLogHelper.LogDebug(
                    log,
                    "Bugfixes and QoL plague target-reservation fix initialized from unique " +
                    "disease-search and healer-update-exit signatures; radius=<7, " +
                    "stationaryTimeoutGameMs=10000.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void ApplySetting()
        {
            if (!IsEnabled)
            {
                ClearReservations("mod disabled", logRelease: false);
            }
        }

        public void OnTreatmentCompleted(int unitId)
        {
            if (!correctionAvailable || !IsEnabled)
                return;

            try
            {
                uint ownerGlobalId = ResolveLivingHealerGlobalId(unitId);
                if (ownerGlobalId != 0)
                {
                    ReleaseOwner(ownerGlobalId, "treatment completed", logRelease: true);
                    suppressedByOwner.Remove(ownerGlobalId);
                }
                else
                {
                    ReleaseOwnersUsingUnitSlot(unitId, "treatment owner no longer valid");
                }
            }
            catch (Exception ex)
            {
                DisableCorrection("treatment cleanup failed", ex);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            correctionAvailable = false;
            ClearReservations("feature disposed", logRelease: false);
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
        }

        private int FindNearestUnreservedDisease(IntPtr projectileManager, int nativeUnitId)
        {
            if (!correctionAvailable || !IsEnabled)
            {
                return diseaseSearchHook.Value.Hook.Trampoline(projectileManager, nativeUnitId);
            }

            List<MaskedDisease> masked = maskedDiseases;
            masked.Clear();
            uint requesterGlobalId = 0;
            try
            {
                requesterGlobalId = ResolveLivingHealerGlobalId(nativeUnitId);
                if (requesterGlobalId == 0)
                    throw new InvalidOperationException($"Disease search received an invalid healer: unit={nativeUnitId}.");

                PruneInvalidReservations();
                MaskForeignReservations(requesterGlobalId, masked);
            }
            catch (Exception ex)
            {
                RestoreMaskedDiseases(masked, disableOnFailure: false);
                masked.Clear();
                DisableCorrection("pre-search validation or masking failed", ex);
                return diseaseSearchHook.Value.Hook.Trampoline(projectileManager, nativeUnitId);
            }

            int selectedSlot;
            try
            {
                // Reserved clouds are ineligible only during this exact Vanilla call.
                selectedSlot = diseaseSearchHook.Value.Hook.Trampoline(projectileManager, nativeUnitId);
            }
            finally
            {
                RestoreMaskedDiseases(masked, disableOnFailure: true);
                masked.Clear();
            }

            return selectedSlot;
        }

        private void ObserveCompletedHealerUpdate(NativePointer<X64SmartCPUContext> context)
        {
            if (!correctionAvailable || !IsEnabled)
                return;

            try
            {
                int unitId = checked((int)context.Pointer->RBP);
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* healer) || healer == null)
                {
                    ReleaseOwnersUsingUnitSlot(unitId, "owner slot unavailable");
                    return;
                }

                uint ownerGlobalId = healer->r_GlobalId;
                PruneInvalidReservations();
                if (healer->r_AliveState != AliveState.IsAlive ||
                    healer->r_UnitChimp != eChimps.CHIMP_TYPE_HEALER)
                {
                    ReleaseOwner(ownerGlobalId, "owner dead, deleted, or no longer an apothecary", true);
                    suppressedByOwner.Remove(ownerGlobalId);
                    return;
                }

                ushort targetSlot = *(ushort*)((byte*)healer + HealerTargetSlotOffset);
                uint targetGlobalId = *(uint*)((byte*)healer + HealerTargetGlobalIdOffset);
                DiseaseIdentity targetIdentity = new DiseaseIdentity(targetSlot, targetGlobalId);
                if (!IsActiveTargetState(healer) ||
                    !TryResolveSelectableDisease(targetIdentity, out GameProjectile* target))
                {
                    ReleaseOwner(ownerGlobalId, "target or working state no longer valid", true);
                    suppressedByOwner.Remove(ownerGlobalId);
                    return;
                }

                ushort x = healer->r_CurrentTilePositionX;
                ushort y = healer->r_CurrentTilePositionY;
                if (groupsByOwner.TryGetValue(ownerGlobalId, out ReservationGroup group))
                {
                    if (!group.Target.Equals(targetIdentity))
                    {
                        ReleaseOwner(ownerGlobalId, "target changed", true);
                        suppressedByOwner.Remove(ownerGlobalId);
                    }
                    else if (group.LastX != x || group.LastY != y)
                    {
                        group.LastX = x;
                        group.LastY = y;
                        group.LastProgress = GameTimeManagerAPI.Instance.CaptureTimeStamp();
                        return;
                    }
                    else if (GameTimeManagerAPI.Instance.HasMillisecondsElapsed(
                        group.LastProgress,
                        StationaryTimeoutMilliseconds))
                    {
                        ReleaseOwner(ownerGlobalId, "stationary timeout after 10000 game ms", true);
                        suppressedByOwner[ownerGlobalId] =
                            new SuppressedAssignment(targetIdentity, x, y);
                        return;
                    }
                    else
                    {
                        return;
                    }
                }

                if (suppressedByOwner.TryGetValue(ownerGlobalId, out SuppressedAssignment suppressed))
                {
                    if (suppressed.Target.Equals(targetIdentity) &&
                        suppressed.X == x && suppressed.Y == y)
                    {
                        return;
                    }
                    suppressedByOwner.Remove(ownerGlobalId);
                }

                CreateReservationGroup(unitId, healer, targetIdentity, target);
            }
            catch (Exception ex)
            {
                DisableCorrection("completed-healer-update validation failed", ex);
            }
        }

        private void CreateReservationGroup(
            int unitId,
            GameUnit* healer,
            DiseaseIdentity targetIdentity,
            GameProjectile* target)
        {
            uint ownerGlobalId = healer->r_GlobalId;
            if (ownersByDisease.TryGetValue(targetIdentity, out uint existingOwner) &&
                existingOwner != ownerGlobalId)
            {
                LogDetail(
                    $"plague reservation could not claim already reserved target: unit={unitId}, " +
                    $"global={ownerGlobalId}, target={targetIdentity}, ownerGlobal={existingOwner}.");
                return;
            }

            var reserved = new List<DiseaseIdentity>();
            Span<GameProjectile> projectiles = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            for (int index = 0; index < projectiles.Length; index++)
            {
                ref GameProjectile projectile = ref projectiles[index];
                if (projectile.r_AliveState != AliveState.IsAlive ||
                    projectile.r_ProjectileType != ProjectileType.Disease ||
                    projectile.r_Unknown4 > MaximumSelectablePhase ||
                    VanillaAreaDistance(
                        projectile.r_CurrentTileX,
                        projectile.r_CurrentTileY,
                        target->r_CurrentTileX,
                        target->r_CurrentTileY) >= AreaTreatmentDistance)
                {
                    continue;
                }

                DiseaseIdentity identity =
                    new DiseaseIdentity(checked((ushort)(index + 1)), projectile.r_GlobalId);
                if (ownersByDisease.TryGetValue(identity, out uint owner) && owner != ownerGlobalId)
                {
                    continue;
                }

                reserved.Add(identity);
            }

            if (!reserved.Contains(targetIdentity))
                throw new InvalidOperationException($"The selected target was not reservable: {targetIdentity}.");

            // Publish only after the complete group passed validation, avoiding partial claims.
            foreach (DiseaseIdentity identity in reserved)
                ownersByDisease[identity] = ownerGlobalId;

            var group = new ReservationGroup(
                unitId,
                ownerGlobalId,
                targetIdentity,
                reserved,
                healer->r_CurrentTilePositionX,
                healer->r_CurrentTilePositionY,
                GameTimeManagerAPI.Instance.CaptureTimeStamp());
            groupsByOwner[ownerGlobalId] = group;
            LogDetail(
                $"plague reservation created: unit={unitId}, global={ownerGlobalId}, " +
                $"target={targetIdentity}@{target->r_CurrentTileX},{target->r_CurrentTileY}, " +
                $"reserved={reserved.Count}.");
        }

        private void MaskForeignReservations(
            uint requesterGlobalId,
            List<MaskedDisease> masked)
        {
            foreach (KeyValuePair<DiseaseIdentity, uint> entry in ownersByDisease)
            {
                if (entry.Value == requesterGlobalId ||
                    !TryResolveSelectableDisease(entry.Key, out GameProjectile* projectile))
                {
                    continue;
                }

                ushort originalPhase = projectile->r_Unknown4;
                // Record first so even an allocation failure cannot leave an untracked mask.
                masked.Add(new MaskedDisease(entry.Key, originalPhase));
                projectile->r_Unknown4 = TemporarilyMaskedPhase;
            }
        }

        private void RestoreMaskedDiseases(List<MaskedDisease> masked, bool disableOnFailure)
        {
            if (masked == null)
                return;

            Exception firstFailure = null;
            foreach (MaskedDisease item in masked)
            {
                try
                {
                    if (!TryResolveDiseaseIdentity(item.Identity, out GameProjectile* projectile))
                        throw new InvalidOperationException($"Masked disease identity changed: {item.Identity}.");
                    if (projectile->r_Unknown4 != TemporarilyMaskedPhase)
                    {
                        throw new InvalidOperationException(
                            $"Masked disease phase changed unexpectedly: {item.Identity}, " +
                            $"phase={projectile->r_Unknown4}.");
                    }
                    projectile->r_Unknown4 = item.OriginalPhase;
                }
                catch (Exception ex)
                {
                    // Continue restoring every other entry before reporting the first failure.
                    if (firstFailure == null)
                        firstFailure = ex;
                }
            }

            if (firstFailure == null)
                return;
            if (disableOnFailure)
                DisableCorrection("temporary disease phases could not be restored safely", firstFailure);
            else
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL plague reservation mask rollback failed: {firstFailure}");
        }

        private void PruneInvalidReservations()
        {
            if (groupsByOwner.Count == 0)
                return;

            List<KeyValuePair<uint, string>> releases = pendingReleases;
            releases.Clear();
            foreach (KeyValuePair<uint, ReservationGroup> entry in groupsByOwner)
            {
                ReservationGroup group = entry.Value;
                if (!TryResolveLivingHealer(group.OwnerUnitId, group.OwnerGlobalId, out GameUnit* healer))
                {
                    releases.Add(new KeyValuePair<uint, string>(entry.Key, "owner dead, deleted, or slot reused"));
                    continue;
                }
                if (!IsActiveTargetState(healer) ||
                    *(ushort*)((byte*)healer + HealerTargetSlotOffset) != group.Target.Slot ||
                    *(uint*)((byte*)healer + HealerTargetGlobalIdOffset) != group.Target.GlobalId)
                {
                    releases.Add(new KeyValuePair<uint, string>(entry.Key, "owner target or state changed"));
                    continue;
                }
                if (!TryResolveSelectableDisease(group.Target, out _))
                    releases.Add(new KeyValuePair<uint, string>(entry.Key, "target removed or no longer selectable"));
            }

            foreach (KeyValuePair<uint, string> release in releases)
                ReleaseOwner(release.Key, release.Value, true);
            releases.Clear();
        }

        private void ReleaseOwnersUsingUnitSlot(int unitId, string reason)
        {
            List<uint> owners = pendingOwnerSlots;
            owners.Clear();
            foreach (KeyValuePair<uint, ReservationGroup> entry in groupsByOwner)
            {
                if (entry.Value.OwnerUnitId == unitId)
                    owners.Add(entry.Key);
            }
            foreach (uint owner in owners)
            {
                ReleaseOwner(owner, reason, true);
                suppressedByOwner.Remove(owner);
            }
            owners.Clear();
        }

        private void ReleaseOwner(uint ownerGlobalId, string reason, bool logRelease)
        {
            if (!groupsByOwner.TryGetValue(ownerGlobalId, out ReservationGroup group))
                return;

            groupsByOwner.Remove(ownerGlobalId);
            foreach (DiseaseIdentity identity in group.Diseases)
            {
                if (ownersByDisease.TryGetValue(identity, out uint owner) && owner == ownerGlobalId)
                    ownersByDisease.Remove(identity);
            }
            if (logRelease)
            {
                LogDetail(
                    $"plague reservation released: unit={group.OwnerUnitId}, global={ownerGlobalId}, " +
                    $"target={group.Target}, released={group.Diseases.Count}, reason={reason}.");
            }
        }

        private void ClearReservations(string reason, bool logRelease)
        {
            if (logRelease && groupsByOwner.Count > 0)
                LogDetail($"plague reservations cleared: groups={groupsByOwner.Count}, reason={reason}.");
            groupsByOwner.Clear();
            ownersByDisease.Clear();
            suppressedByOwner.Clear();
        }

        private void DisableCorrection(string reason, Exception ex)
        {
            if (!correctionAvailable)
                return;

            correctionAvailable = false;
            ClearReservations("feature disabled", logRelease: false);
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL plague target-reservation fix disabled for this process because " +
                $"{reason}; other plague fixes remain active: {ex}");
        }

        private void LogDetail(string message)
        {
            if (detailedLogCount < MaximumDetailedLogs)
            {
                detailedLogCount++;
                Shared.DebugLogHelper.LogDebug(log, $"Bugfixes and QoL {message}");
                return;
            }
            if (detailLimitLogged)
                return;

            detailLimitLogged = true;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Bugfixes and QoL plague reservation detailed-log limit of " +
                $"{MaximumDetailedLogs} reached; repeated details are suppressed.");
        }

        private bool IsEnabled => settings.EnableMod && settings.EnablePlagueTargetReservationFix;

        private static bool IsActiveTargetState(GameUnit* healer)
        {
            ushort state = healer->r_AIState;
            if (state == WalkingState || state == TreatingState)
                return true;
            return state == TransitionState &&
                *(ushort*)((byte*)healer + HealerNextStateOffset) == WalkingState;
        }

        private static uint ResolveLivingHealerGlobalId(int unitId)
        {
            return TryResolveLivingHealer(unitId, 0, out GameUnit* healer)
                ? healer->r_GlobalId
                : 0;
        }

        private static bool TryResolveLivingHealer(
            int unitId,
            uint expectedGlobalId,
            out GameUnit* healer)
        {
            healer = null;
            return unitId > 0 &&
                GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out healer) &&
                healer != null &&
                healer->r_AliveState == AliveState.IsAlive &&
                healer->r_UnitChimp == eChimps.CHIMP_TYPE_HEALER &&
                (expectedGlobalId == 0 || healer->r_GlobalId == expectedGlobalId);
        }

        private static bool TryResolveSelectableDisease(
            DiseaseIdentity identity,
            out GameProjectile* projectile)
        {
            return TryResolveDiseaseIdentity(identity, out projectile) &&
                projectile->r_Unknown4 <= MaximumSelectablePhase;
        }

        private static bool TryResolveDiseaseIdentity(
            DiseaseIdentity identity,
            out GameProjectile* projectile)
        {
            projectile = null;
            return identity.Slot > 0 && identity.GlobalId != 0 &&
                GameProjectileManagerAPI.Instance.TryGetProjectileById(identity.Slot, out projectile) &&
                projectile != null &&
                projectile->r_AliveState == AliveState.IsAlive &&
                projectile->r_ProjectileType == ProjectileType.Disease &&
                projectile->r_GlobalId == identity.GlobalId;
        }

        private static int VanillaAreaDistance(int firstX, int firstY, int secondX, int secondY)
        {
            return Math.Max(Math.Abs(firstX - secondX), Math.Abs(firstY - secondY));
        }

        private readonly struct DiseaseIdentity : IEquatable<DiseaseIdentity>
        {
            public DiseaseIdentity(ushort slot, uint globalId)
            {
                Slot = slot;
                GlobalId = globalId;
            }

            public ushort Slot { get; }
            public uint GlobalId { get; }

            public bool Equals(DiseaseIdentity other)
            {
                return Slot == other.Slot && GlobalId == other.GlobalId;
            }

            public override bool Equals(object obj)
            {
                return obj is DiseaseIdentity other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Slot * 397) ^ (int)GlobalId;
                }
            }

            public override string ToString()
            {
                return $"{Slot}/{GlobalId}";
            }
        }

        private sealed class ReservationGroup
        {
            public ReservationGroup(
                int ownerUnitId,
                uint ownerGlobalId,
                DiseaseIdentity target,
                List<DiseaseIdentity> diseases,
                ushort lastX,
                ushort lastY,
                GameTimeStamp lastProgress)
            {
                OwnerUnitId = ownerUnitId;
                OwnerGlobalId = ownerGlobalId;
                Target = target;
                Diseases = diseases;
                LastX = lastX;
                LastY = lastY;
                LastProgress = lastProgress;
            }

            public int OwnerUnitId { get; }
            public uint OwnerGlobalId { get; }
            public DiseaseIdentity Target { get; }
            public List<DiseaseIdentity> Diseases { get; }
            public ushort LastX { get; set; }
            public ushort LastY { get; set; }
            public GameTimeStamp LastProgress { get; set; }
        }

        private readonly struct MaskedDisease
        {
            public MaskedDisease(DiseaseIdentity identity, ushort originalPhase)
            {
                Identity = identity;
                OriginalPhase = originalPhase;
            }

            public DiseaseIdentity Identity { get; }
            public ushort OriginalPhase { get; }
        }

        private readonly struct SuppressedAssignment
        {
            public SuppressedAssignment(DiseaseIdentity target, ushort x, ushort y)
            {
                Target = target;
                X = x;
                Y = y;
            }

            public DiseaseIdentity Target { get; }
            public ushort X { get; }
            public ushort Y { get; }
        }
    }

}
