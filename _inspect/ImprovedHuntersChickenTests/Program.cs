using ImprovedHunters;
using SHCDESE.Interop;

static class Program
{
    private static int assertions;

    private sealed class PendingSpawn
    {
        public int PlayerId { get; }
        public int UnitType { get; }
        public bool Matched { get; set; }

        public PendingSpawn(int playerId, int unitType)
        {
            PlayerId = playerId;
            UnitType = unitType;
        }
    }

    public static void Main()
    {
        TestLimits();
        TestSpawnMatching();
        TestSlotReuse();
        TestGranarySelection();
        TestHunterQueryActorPolicy();
        TestManualChickenAttackPolicy();
        Console.WriteLine($"ImprovedHunters chicken policy tests passed: assertions={assertions}.");
    }

    private static void TestLimits()
    {
        Assert(!GranaryChickenSpawnPolicy.TryGetNormalizedVanillaTarget(false, 0, 10, out _),
            "Disabled management must leave Vanilla's target untouched.");
        AssertTarget(0, 0, 0);
        AssertTarget(0, 1, int.MaxValue);
        AssertTarget(9, 10, int.MaxValue);
        AssertTarget(10, 10, 0);
        AssertTarget(11, 10, 0);
        AssertTarget(10, 5, 0);
        AssertTarget(-5, 10, int.MaxValue);
        Assert(GranaryChickenSpawnPolicy.ClampMaximum(-1) == 0, "Limit minimum clamp failed.");
        Assert(GranaryChickenSpawnPolicy.ClampMaximum(101) == 100, "Limit maximum clamp failed.");
    }

    private static void TestSpawnMatching()
    {
        const int Chicken = 62;
        const int Deer = 44;
        Stack<PendingSpawn> pending = new();
        pending.Push(new PendingSpawn(1, Chicken));

        PendingSpawn first = pending.Peek();
        Assert(!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, first.Matched, first.PlayerId, first.UnitType, 10, 20, 3, 2, Chicken, 160, 80, 3),
            "Another player's chicken must not match.");
        Assert(!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, first.Matched, first.PlayerId, first.UnitType, 10, 20, 3, 1, Deer, 160, 80, 3),
            "Another unit type must not match.");
        Assert(!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, first.Matched, first.PlayerId, first.UnitType, 10, 20, 3, 1, Chicken, 161, 80, 3),
            "A chicken at another position must not match.");
        Assert(!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, first.Matched, first.PlayerId, first.UnitType, 10, 20, 3, 1, Chicken, 160, 80, 4),
            "A chicken at another elevation must not match.");
        Assert(GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, first.Matched, first.PlayerId, first.UnitType, 10, 20, 3, 1, Chicken, 160, 80, 3),
            "The immediate matching granary chicken was not recognized.");
        first.Matched = true;
        Assert(!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, first.Matched, first.PlayerId, first.UnitType, 10, 20, 3, 1, Chicken, 160, 80, 3),
            "One granary event must not match twice.");

        pending.Push(new PendingSpawn(2, Chicken));
        PendingSpawn nested = pending.Peek();
        Assert(GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, nested.Matched, nested.PlayerId, nested.UnitType, 12, 21, 4, 2, Chicken, 168, 96, 4),
            "Nested top-of-stack spawn was not matched.");
        nested.Matched = true;
        pending.Pop();
        Assert(ReferenceEquals(pending.Peek(), first), "Nested spawn completion did not restore its parent context.");

        long failedReturnValue = 0;
        Assert(failedReturnValue <= 0, "Failed spawn test fixture is invalid.");
        Assert(GranaryChickenSpawnPolicy.CanAssignCompletedSpawn(true, 42, true, true, 9001, true, true, true),
            "A successful neutral chicken spawn must be assignable.");
        Assert(!GranaryChickenSpawnPolicy.CanAssignCompletedSpawn(true, failedReturnValue, false, false, 0, false, false, false),
            "A failed spawn must not create an assignment.");
        Assert(!GranaryChickenSpawnPolicy.CanAssignCompletedSpawn(true, 42, true, true, 9001, false, true, true),
            "A non-neutral owner must fail post-spawn validation.");
        Assert(!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(false, false, 1, Chicken, 10, 20, 3, 1, Chicken, 160, 80, 3),
            "Inactive safety guards must prevent neutralization.");
    }

    private static void TestSlotReuse()
    {
        Assert(GranaryChickenSpawnPolicy.IsTrackedIdentityValid(100, 100, true, true),
            "Stable slot/global identity should remain assigned.");
        Assert(!GranaryChickenSpawnPolicy.IsTrackedIdentityValid(100, 101, true, true),
            "A reused slot with a new global ID must be removed.");
        Assert(!GranaryChickenSpawnPolicy.IsTrackedIdentityValid(100, 100, false, true),
            "A reused slot with another type must be removed.");
        Assert(!GranaryChickenSpawnPolicy.IsTrackedIdentityValid(100, 100, true, false),
            "A dead chicken must be removed.");
        Assert(!GranaryChickenSpawnPolicy.IsTrackedIdentityValid(0, 0, true, true),
            "A zero global ID must never become a stable assignment.");
    }

    private static void TestGranarySelection()
    {
        Assert(GranaryChickenSpawnPolicy.ChebyshevDistance(10, 10, 13, 14) == 4,
            "Chebyshev distance is incorrect.");
        Assert(GranaryChickenSpawnPolicy.IsBetterGranaryCandidate(3, 9, 2, 4, 1, 1),
            "A nearer granary must win.");
        Assert(GranaryChickenSpawnPolicy.IsBetterGranaryCandidate(3, 4, 2, 3, 5, 1),
            "Building ID must break equal-distance ties.");
        Assert(GranaryChickenSpawnPolicy.IsBetterGranaryCandidate(3, 4, 1, 3, 4, 2),
            "Player ID must be the final deterministic tie-break.");
        Assert(!GranaryChickenSpawnPolicy.IsBetterGranaryCandidate(3, 4, 2, 3, 4, 1),
            "A worse player-ID tie must not replace the current candidate.");
    }

    private static void TestHunterQueryActorPolicy()
    {
        const ulong manager = 0x10000000;
        Assert(HunterQueryActorPolicy.TryReconstructHunterUnitId(
                manager + 96 * HunterQueryActorPolicy.NativeUnitSlotSize,
                manager,
                out int hunterUnitId) && hunterUnitId == 96,
            "The native Hunter ID reconstruction failed.");
        Assert(!HunterQueryActorPolicy.TryReconstructHunterUnitId(manager, manager, out _),
            "Hunter ID zero must be rejected.");
        Assert(!HunterQueryActorPolicy.TryReconstructHunterUnitId(manager - 1, manager, out _),
            "A Hunter base below the manager must be rejected.");
        Assert(!HunterQueryActorPolicy.TryReconstructHunterUnitId(manager + 1, manager, out _),
            "A non-slot-aligned Hunter base must be rejected.");
        Assert(HunterQueryActorPolicy.IsMatchingCapture(170, 170, 94),
            "An identity-matching query capture must be accepted.");
        Assert(!HunterQueryActorPolicy.IsMatchingCapture(170, 171, 94),
            "A capture from another candidate must be rejected.");
        Assert(!HunterQueryActorPolicy.IsMatchingCapture(170, 170, 0),
            "A capture without a reconstructed Hunter must be rejected.");
    }

    private static void TestManualChickenAttackPolicy()
    {
        eChimps[] supportedRangedTypes =
        {
            eChimps.CHIMP_TYPE_ARCHER,
            eChimps.CHIMP_TYPE_XBOWMAN,
            eChimps.CHIMP_TYPE_ARCHER_debug,
            eChimps.CHIMP_TYPE_CATAPULT,
            eChimps.CHIMP_TYPE_TREBUCHET,
            eChimps.CHIMP_TYPE_MANGONEL,
            eChimps.CHIMP_TYPE_BALLISTA,
            eChimps.CHIMP_TYPE_ARAB_BOW,
            eChimps.CHIMP_TYPE_ARAB_SLINGER,
            eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
            eChimps.CHIMP_TYPE_ARAB_GRENADIER,
            eChimps.CHIMP_TYPE_ARAB_BALLISTA,
            eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER,
            eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,
            eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
        };
        foreach (eChimps type in supportedRangedTypes)
        {
            Assert(ManualChickenAttackPolicy.CanOverrideCompatibilityRejection(type),
                $"Ranged attacker {type} must be eligible for an explicit chicken order.");
        }

        eChimps[] vanillaRejectedTypes =
        {
            eChimps.CHIMP_TYPE_HUNTER,
            eChimps.CHIMP_TYPE_PEASANT,
            eChimps.CHIMP_TYPE_SPEARMAN,
            eChimps.CHIMP_TYPE_PIKEMAN,
            eChimps.CHIMP_TYPE_MACEMAN,
            eChimps.CHIMP_TYPE_SWORDSMAN,
            eChimps.CHIMP_TYPE_KNIGHT,
            eChimps.CHIMP_TYPE_ENGINEER,
            eChimps.CHIMP_TYPE_MONK,
            eChimps.CHIMP_TYPE_BEDOUIN_HEALER,
            eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH,
            eChimps.CHIMP_TYPE_BEDOUIN_SAPPER,
            eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER,
        };
        foreach (eChimps type in vanillaRejectedTypes)
        {
            Assert(!ManualChickenAttackPolicy.CanOverrideCompatibilityRejection(type),
                $"Non-projectile attacker {type} must retain Vanilla's chicken rejection.");
        }
    }

    private static void AssertTarget(int liveCount, int limit, int expectedTarget)
    {
        Assert(GranaryChickenSpawnPolicy.TryGetNormalizedVanillaTarget(true, liveCount, limit, out int actualTarget),
            "Enabled management did not provide an override.");
        Assert(actualTarget == expectedTarget,
            $"Unexpected normalized target for live={liveCount}, limit={limit}: {actualTarget}.");
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
