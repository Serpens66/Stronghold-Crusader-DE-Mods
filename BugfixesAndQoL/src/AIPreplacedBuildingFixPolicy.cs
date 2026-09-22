using System;
using System.Collections.Generic;
using System.Linq;

namespace BugfixesAndQoL
{
    internal static class WallTileBaselineCollector
    {
        internal static void Collect(ReadOnlySpan<int> logicGrid, int wallFlag, ISet<int> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            for (int tileId = 0; tileId < logicGrid.Length; tileId++)
                if ((logicGrid[tileId] & wallFlag) != 0)
                    destination.Add(tileId);
        }
    }

    internal sealed class LegacyRuinTimerFix
    {
        public string ClassifyTransfer(bool isAi, bool isSave, int mapVersion, int legacyVersionExclusive,
            int sourceBefore, int sourceAfter, int destinationBefore, int destinationAfter) =>
            LegacyTimerFixEligibility.Classify(isAi, isSave, mapVersion, legacyVersionExclusive,
                sourceBefore, sourceAfter, destinationBefore, destinationAfter);

        public string ClassifyApplication(string transferClassification, bool hasMatchingDestroyedTower,
            bool damageWriterObserved, int currentTimer) =>
            LegacyTimerFixEligibility.ClassifyAtApplication(transferClassification,
                hasMatchingDestroyedTower, damageWriterObserved, currentTimer);

        public bool IsApplicationEligible(string classification) =>
            LegacyTimerFixEligibility.IsApplicationEligible(classification);
    }

    internal sealed class PreplacedEconomyAccessFix
    {
        public EconomyFixActivationState InitialState(bool isSave, WallAccessRole role,
            bool hasFriendlyPreplacedPortal) =>
            EconomyFixActivationModel.Initial(isSave, role, hasFriendlyPreplacedPortal);

        public EconomyFixActivationState Activate(EconomyFixActivationState state) =>
            EconomyFixActivationModel.Activate(state);

        public EconomyFixActivationState Resume(bool confirmedBreach,
            bool hasFriendlyPreplacedPortal, WallAccessRole role) =>
            EconomyFixActivationModel.Resume(confirmedBreach, hasFriendlyPreplacedPortal, role);

        public bool IsPortalOwnerSynchronizationEligible(bool isSave, bool isPreplaced,
            bool identityMatches, bool isLiving, bool recordActive, bool recordOpen,
            bool buildingIdMatches, bool subjectGlobalMatches, bool entryPclValid,
            bool exitPclValid, bool ownerValid) =>
            PortalOwnerSynchronizationModel.IsEligible(isSave, isPreplaced, identityMatches,
                isLiving, recordActive, recordOpen, buildingIdMatches, subjectGlobalMatches,
                entryPclValid, exitPclValid, ownerValid);

        public bool IsConfirmedBreach(bool baselineWallLost, int oldInsidePcl, int oldOutsidePcl,
            int newInsidePcl, int newOutsidePcl) =>
            WallBreachConfirmation.IsConfirmed(baselineWallLost, oldInsidePcl, oldOutsidePcl,
                newInsidePcl, newOutsidePcl);
    }

    internal static class CrushedTimerTransition
    {
        public static bool IsActivation(int before, int after) => before == 0 && after == 1;
    }

    internal static class DamageObservationModel
    {
        public static bool IsLethalInput(int currentHealth, int damage) =>
            currentHealth > 0 && damage >= currentHealth;
    }

    internal readonly struct PreplacedIdentity : IEquatable<PreplacedIdentity>
    {
        public PreplacedIdentity(int buildingId, uint globalId, int ownerId, int structureType)
        {
            BuildingId = buildingId;
            GlobalId = globalId;
            OwnerId = ownerId;
            StructureType = structureType;
        }

        public int BuildingId { get; }
        public uint GlobalId { get; }
        public int OwnerId { get; }
        public int StructureType { get; }

        public bool Matches(int buildingId, uint globalId, int ownerId, int structureType) =>
            BuildingId == buildingId && GlobalId == globalId && OwnerId == ownerId &&
            StructureType == structureType;

        public bool MatchesStableRecord(int buildingId, uint globalId, int structureType) =>
            BuildingId == buildingId && GlobalId == globalId && StructureType == structureType;

        public bool Equals(PreplacedIdentity other) =>
            Matches(other.BuildingId, other.GlobalId, other.OwnerId, other.StructureType);

        public override bool Equals(object obj) => obj is PreplacedIdentity other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BuildingId;
                hash = hash * 397 ^ (int)GlobalId;
                hash = hash * 397 ^ OwnerId;
                return hash * 397 ^ StructureType;
            }
        }
    }

    internal enum WallAccessRole
    {
        None,
        GatedWallCandidate,
        ClosedWallCandidate
    }

    internal static class WallAccessRoleClassifier
    {
        public static WallAccessRole Classify(int livingWallCount, int livingPortalCount) =>
            livingWallCount <= 0 ? WallAccessRole.None :
            livingPortalCount > 0 ? WallAccessRole.GatedWallCandidate : WallAccessRole.ClosedWallCandidate;
    }

    internal enum EconomyFixActivationState
    {
        None,
        PendingPortal,
        PendingBreach,
        ActivePortal,
        ActiveBreach,
        Suspended
    }

    internal static class EconomyFixActivationModel
    {
        public static EconomyFixActivationState Initial(bool isSave, WallAccessRole role,
            bool hasFriendlyPreplacedPortal)
        {
            if (isSave || role == WallAccessRole.None) return EconomyFixActivationState.None;
            if (hasFriendlyPreplacedPortal) return EconomyFixActivationState.PendingPortal;
            return role == WallAccessRole.ClosedWallCandidate
                ? EconomyFixActivationState.PendingBreach
                : EconomyFixActivationState.None;
        }

        public static EconomyFixActivationState Activate(EconomyFixActivationState state) =>
            state == EconomyFixActivationState.PendingPortal
                ? EconomyFixActivationState.ActivePortal
                : state == EconomyFixActivationState.PendingBreach
                    ? EconomyFixActivationState.ActiveBreach
                    : state;

        public static EconomyFixActivationState Resume(bool confirmedBreach,
            bool hasFriendlyPreplacedPortal, WallAccessRole role) =>
            confirmedBreach ? EconomyFixActivationState.PendingBreach :
            role != WallAccessRole.None && hasFriendlyPreplacedPortal ? EconomyFixActivationState.PendingPortal :
            role == WallAccessRole.ClosedWallCandidate ? EconomyFixActivationState.PendingBreach :
            EconomyFixActivationState.None;
    }

    internal static class PortalOwnerSynchronizationModel
    {
        public static bool IsEligible(bool isSave, bool isPreplaced, bool identityMatches,
            bool isLiving, bool recordActive, bool recordOpen, bool buildingIdMatches,
            bool subjectGlobalMatches, bool entryPclValid, bool exitPclValid, bool ownerValid) =>
            !isSave && isPreplaced && identityMatches && isLiving && recordActive && recordOpen &&
            buildingIdMatches && subjectGlobalMatches && entryPclValid && exitPclValid && ownerValid;
    }

    internal enum WallOwnerEncoding
    {
        Unresolved,
        OneBased,
        ZeroBased
    }

    internal static class WallOwnerEncodingResolver
    {
        public static WallOwnerEncoding Resolve(int oneBasedMatches, int zeroBasedMatches)
        {
            if (oneBasedMatches <= 0 && zeroBasedMatches <= 0) return WallOwnerEncoding.Unresolved;
            if (oneBasedMatches == zeroBasedMatches) return WallOwnerEncoding.Unresolved;
            return oneBasedMatches > zeroBasedMatches ? WallOwnerEncoding.OneBased : WallOwnerEncoding.ZeroBased;
        }

        public static int Decode(byte rawOwner, WallOwnerEncoding encoding) =>
            encoding == WallOwnerEncoding.OneBased ? rawOwner :
            encoding == WallOwnerEncoding.ZeroBased ? rawOwner + 1 : 0;
    }

    internal static class WallBreachConfirmation
    {
        public static bool IsConfirmed(bool baselineWallLost, int oldInsidePcl, int oldOutsidePcl,
            int newInsidePcl, int newOutsidePcl) =>
            baselineWallLost && oldInsidePcl > 0 && oldOutsidePcl > 0 &&
            oldInsidePcl != oldOutsidePcl && newInsidePcl > 0 && newInsidePcl == newOutsidePcl;
    }

    internal static class LegacyTimerFixEligibility
    {
        public static string Classify(bool isAi, bool isSave, int mapVersion, int legacyVersionExclusive,
            int sourceBefore, int sourceAfter, int destinationBefore, int destinationAfter)
        {
            if (!isAi) return "ineligible-non-ai";
            if (isSave) return "ineligible-loaded-save";
            if (mapVersion < 0 || mapVersion >= legacyVersionExclusive) return "ineligible-not-legacy-conversion";
            if (destinationBefore != 0) return "ineligible-runtime-timer-already-active";
            if (sourceBefore != 1 || sourceAfter != 1) return "ineligible-no-stable-single-activation-source";
            if (destinationAfter != sourceAfter) return "ineligible-not-copied-exclusively-from-serialized-source";
            return "eligible-fresh-map-legacy-transfer";
        }

        public static bool IsEligible(string classification) =>
            string.Equals(classification, "eligible-fresh-map-legacy-transfer", StringComparison.Ordinal);

        public static string ClassifyAtApplication(string transferClassification,
            bool hasMatchingDestroyedTower, bool damageWriterObserved, int currentTimer)
        {
            if (!IsEligible(transferClassification)) return transferClassification;
            if (!hasMatchingDestroyedTower) return "ineligible-no-matching-preplaced-destroyed-tower";
            if (damageWriterObserved) return "ineligible-later-damage-activation";
            if (currentTimer != 1) return "ineligible-runtime-timer-changed-before-application";
            return "eligible-verified-preplaced-tower-transfer";
        }

        public static bool IsApplicationEligible(string classification) =>
            string.Equals(classification, "eligible-verified-preplaced-tower-transfer", StringComparison.Ordinal);
    }

    internal static class EconomyGridOverlayProjection
    {
        public static byte ProjectOutsideCount(IEnumerable<int> cellPcls, ISet<int> reachablePcls)
        {
            if (cellPcls == null) throw new ArgumentNullException(nameof(cellPcls));
            if (reachablePcls == null) throw new ArgumentNullException(nameof(reachablePcls));
            int outside = cellPcls.Count(pcl => pcl <= 0 || !reachablePcls.Contains(pcl));
            if ((uint)outside > 25U)
                throw new InvalidOperationException("A 5x5 economy cell must contain at most 25 PCL samples.");
            return checked((byte)outside);
        }

        public static bool RestoredExactly(byte[] expected, byte[] actual) =>
            expected != null && actual != null && expected.SequenceEqual(actual);
    }
}
