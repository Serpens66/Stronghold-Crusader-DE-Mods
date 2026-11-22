using System;
using AIMarketOverbuyDiagnostic;

internal static class MarketDemandTargetTests
{
    private static void Check(bool value, string detail)
    { if (!value) throw new Exception("Market delivery-credit contract failed: " + detail); }

    private static MarketDemandTarget Package(int stock = 0, int amount = 5)
    {
        Check(MarketDemandTarget.TryStartPackage(stock, amount, 32, 100, out var state), "confirmed native store accepted");
        return state;
    }

    internal static void Run()
    {
        var state = Package();
        Check(!state.TryReconcile(5, 0, 32, 100, out int adjusted) && adjusted == 5, "no delivery preserves native package");
        Check(state.TryReconcile(5, 1, 32, 100, out adjusted) && adjusted == 4 && state.Credited == 1,
            "one delivered leaves four");
        Check(!state.TryReconcile(4, 0, 32, 100, out adjusted) && adjusted == 4 && state.Credited == 1,
            "consuming delivered ware never undelivers it");
        Check(state.TryReconcile(4, 1, 32, 100, out adjusted) && adjusted == 3 && state.Credited == 2,
            "observed loss refreshes baseline for the next receipt");
        Check(state.TryReconcile(3, 4, 32, 100, out adjusted) && adjusted == 0 && !state.Active,
            "separate deliveries complete the package without requiring five in stock");
        Check(!state.TryReconcile(0, 0, 32, 100, out adjusted) && adjusted == 0, "complete credit is terminal after consumption");

        state = Package(12, 10);
        Check(!state.TryReconcile(10, 0, 32, 100, out adjusted) && adjusted == 10 && state.LastObservedStock == 0,
            "losing old stock does not increase or cover a package");
        Check(state.TryReconcile(10, 8, 32, 100, out adjusted) && adjusted == 2 && state.Credited == 8,
            "eight delivered after losing original twelve still leaves two");
        state = Package(12, 10);
        Check(state.TryReconcile(10, 20, 32, 100, out adjusted) && adjusted == 2 && state.Goal == 22,
            "eight delivered leaves two even though historical stock target was twenty-two");
        Check(!state.TryReconcile(2, 0, 32, 100, out adjusted) && adjusted == 2 && state.Credited == 8,
            "building consumes all twenty without reviving ten already credited units");

        // Pure receipt bookkeeping is separate from policy-validated game-write
        // authorization. No observation may credit the same stock snapshot twice.
        state = Package();
        Check(state.ObserveStock(5, 1, 32) && state.ExpectedPending == 5 && state.Credited == 1,
            "partial receipt observation leaves actual pending ownership unchanged");
        Check(state.NeedsAdjustment(1), "actual partial receipt signals an immediate pending correction");
        Check(state.ObserveStock(5, 0, 32) && state.LastObservedStock == 0 && state.Credited == 1,
            "negative boundary refreshes stock without refunding credit");
        Check(state.ObserveStock(5, 1, 32) && state.Credited == 2,
            "second receipt is counted despite identical endpoint stock");
        Check(state.TryReconcile(5, 1, 32, 100, out adjusted) && adjusted == 3,
            "validated correction publishes cumulative delivery credit");
        Check(!state.NeedsAdjustment(1), "already published quantity does not trigger redundant game writes");
        Check(!state.TryReconcile(3, 1, 32, 100, out adjusted) && adjusted == 3,
            "same stock snapshot cannot credit the same receipt twice");

        state = Package();
        Check(state.ObserveStock(5, 5, 32) && state.Active && state.Credited == 5,
            "full receipt remains owned until its validated game write");
        Check(state.TryReconcile(5, 5, 32, 100, out adjusted) && adjusted == 0 && !state.Active,
            "completed receipt closes before trading");
        Check(!state.TryReconcile(0, 0, 32, 100, out adjusted), "later consumption cannot recreate fulfilled package");

        state = Package();
        state.ObserveDecisionEnd(0, 5, 32);
        Check(!state.Active && state.Credited == 0, "our successful native buy clears ownership without avoided-delivery credit");
        state = Package(12, 10);
        state.ObserveDecisionEnd(10, 12, 32);
        Check(state.Active && state.LastObservedStock == 12 && state.Credited == 0,
            "unselected surviving package receives no artificial own-purchase credit");

        state = Package();
        state.TryReconcile(5, 1, 32, 100, out _);
        Check(MarketDemandTarget.TryStartPackage(0, 5, 32, 100, out state) && state.ExpectedPending == 5 && state.Credited == 0,
            "an explicitly confirmed new package renews its five-unit allowance");
        Check(MarketDemandTarget.TryStartPackage(4, 5, 32, 100, out state) && state.Goal == 9 && state.LastObservedStock == 4,
            "a confirmed new package with the same amount establishes a fresh receipt baseline");
        state.ObservePending(0); state.ObservePending(5);
        Check(!state.TryReconcile(5, 9, 32, 100, out adjusted), "clear then same-value foreign order stays unknown");
        state = Package(); state.ObservePending(4); state.ObservePending(5);
        Check(!state.Active, "foreign quantity cannot restore ownership");

        AssertInvalid(4, 0, 32, 100, "foreign smaller pending");
        AssertInvalid(6, 0, 32, 100, "foreign larger pending");
        AssertInvalid(0, 0, 32, 100, "observed clear");
        AssertInvalid(-1, 0, 32, 100, "negative pending");
        AssertInvalid(5, -1, 32, 100, "negative stock");
        AssertInvalid(5, 0, 31, 100, "different AIC");
        AssertInvalid(5, 0, 32, 101, "different policy");
        state = Package();
        Check(!state.ObserveStock(4, 1, 32) && !state.Active && state.Credited == 0, "foreign cell cannot gain receipt credit");
        state = default;
        Check(!state.TryReconcile(5, 4, 32, 100, out adjusted), "unknown saved order is untouched");

        state = Package(0, 10);
        // Native max(old10, current deficit2) skips its STORE: do not renew.
        Check(state.TryReconcile(10, 5, 32, 100, out adjusted) && adjusted == 5 && state.Goal == 10,
            "max-retained value is not invented as a new fifteen-unit target");
        state = Package(5, 2);
        Check(state.TryReconcile(2, 6, 32, 100, out adjusted) && adjusted == 1,
            "actual deficit store has its own correct nonempty baseline");
        state = Package(0, 12);
        Check(state.TryReconcile(12, 10, 32, 100, out adjusted) && adjusted == 2 && state.Active,
            "native wood buffer remains owed until its delivery");
        Check(!state.TryReconcile(2, 0, 32, 100, out adjusted) && adjusted == 2,
            "using ten wood does not undo its ten credited units");
        Check(state.TryReconcile(2, 2, 32, 100, out adjusted) && adjusted == 0,
            "later delivery of remaining buffer completes the native package");

        Check(!MarketDemandTarget.TryStartPackage(-1, 5, 32, 100, out _) &&
            !MarketDemandTarget.TryStartPackage(0, 0, 32, 100, out _) &&
            !MarketDemandTarget.TryStartPackage(0, -1, 32, 100, out _) &&
            !MarketDemandTarget.TryStartPackage(0, 5, 0, 100, out _), "invalid source inputs rejected");
        state = Package(int.MaxValue, int.MaxValue);
        Check(state.Goal == 2L * int.MaxValue && !state.TryReconcile(int.MaxValue, 0, 32, 100, out adjusted),
            "original goal evidence addition and stock loss avoid overflow");
        Check(state.TryReconcile(int.MaxValue, int.MaxValue, 32, 100, out adjusted) && adjusted == 0 && state.Credited == int.MaxValue,
            "maximum positive receipt is capped without credit overflow");

        SourcePolicies();

        var targets = new MarketDemandTarget[9 * 25];
        for (int player = 1; player <= 8; player++)
            for (int good = 1; good < 25; good++) targets[player * 25 + good] = Package();
        for (int player = 1; player <= 8; player++)
            for (int good = 1; good < 25; good++)
                Check(targets[player * 25 + good].TryReconcile(5, 1, 32, 100, out adjusted) && adjusted == 4,
                    "fixed independent cells handle all goods slots");
        for (int i = 0; i < 1000; i++) ExerciseWarmPath();
        long allocationStart = GC.GetAllocatedBytesForCurrentThread();
        bool correct = true;
        for (int i = 0; i < 100000; i++) correct &= ExerciseWarmPath();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        Check(correct && allocated == 0, "100000 warm renew/receipt/consume/reconcile sequences allocate zero bytes");
        Console.WriteLine("PASS: production delivery credit, same-goal refresh, separate material stock goals, ownership and zero warm allocations.");
    }

    private static void SourcePolicies()
    {
        var state = Package();
        state.TryReconcile(5, 1, 32, 100, out _);
        state.TryReconcile(4, 0, 32, 100, out _);
        Check(state.ObserveFixedStore(0, 4, 5, 32, 100, MarketDemandSourceFamily.Recruit, out bool refresh) && refresh &&
            state.Credited == 1 && state.ExpectedPending == 5,
            "same owned recruit batch refresh retains already consumed delivery credit");
        Check(state.TryReconcile(5, 0, 32, 100, out int adjusted) && adjusted == 4,
            "Vanilla refresh cannot raise owed procurement back from four to five");
        Check(state.ObserveFixedStore(0, 0, 5, 32, 100, MarketDemandSourceFamily.Recruit, out refresh) && !refresh && state.Credited == 0,
            "observed live pre-store clear ends previous goal before a new same-value store");
        state.TryReconcile(5, 1, 32, 100, out _);
        Check(state.ObserveFixedStore(1, 4, 6, 32, 100, MarketDemandSourceFamily.Recruit, out refresh) && !refresh &&
            state.OriginalPackage == 6 && state.Credited == 0,
            "different native batch starts a new procurement allowance");
        Check(state.ObserveFixedStore(1, 6, 6, 32, 101, MarketDemandSourceFamily.Recruit, out refresh) && !refresh && state.PolicyToken == 101,
            "changed source policy starts a fresh goal");

        var combatPolicy = new MarketDemandPolicySnapshot { BuyWeapons = 4, LastStandPhase = 8 };
        long wavePolicy = MarketDemandPolicyStamp.Recruit(combatPolicy, true);
        combatPolicy.LastStandPhase = 7;
        long nextWavePolicy = MarketDemandPolicyStamp.Recruit(combatPolicy, true);
        Check(MarketDemandTarget.TryStartPackage(0, 5, 32, wavePolicy, out state), "LastStand fixed package starts");
        state.TryReconcile(5, 1, 32, wavePolicy, out _);
        Check(state.ObserveFixedStore(0, 4, 5, 32, nextWavePolicy, MarketDemandSourceFamily.Recruit, out refresh) && refresh &&
            state.TryReconcile(5, 0, 32, nextWavePolicy, out adjusted) && adjusted == 4,
            "actual production phase normalization retains delivery credit through countdown8to7");
        combatPolicy.LastStandPhase = 0;
        Check(!state.TryReconcile(4, 0, 32, MarketDemandPolicyStamp.Recruit(combatPolicy, true), out adjusted) && adjusted == 4 && !state.Active,
            "effective recruit amount changes from LastStand five to normal four without an unsafe write");
        RelevantPolicyStamps();

        state = default;
        Check(state.ObserveFixedStore(0, 0, 20, 32, 100, MarketDemandSourceFamily.Emergency, out refresh) && !refresh,
            "emergency source starts its own fixed family");
        state.TryReconcile(20, 1, 32, 100, out _);
        Check(state.ObserveFixedStore(1, 19, 20, 32, 100, MarketDemandSourceFamily.Fixes, out refresh) && !refresh && state.Credited == 0,
            "same quantity from another fixed family cannot silently inherit credit");
        state = Package();
        state.ObserveStock(5, 5, 32);
        Check(state.ObserveFixedStore(5, 5, 5, 32, 100, MarketDemandSourceFamily.Recruit, out refresh) && !refresh && state.Credited == 0,
            "completed procurement followed by an actual store starts the next goal");

        state = default;
        Check(state.ObserveMaterialStore(5, 0, 7, 2, 32, 100, out bool merged) && !merged &&
            state.MaterialCost == 10 && state.MaterialPadding == 2,
            "actual wood deficit derives cost ten separately from native padding two");
        Check(state.TryReconcile(7, 6, 32, 100, out adjusted) && adjusted == 6,
            "partial wood supply reduces the live construction deficit");
        Check(state.TryReconcile(6, 2, 32, 100, out adjusted) && adjusted == 10,
            "loss may increase a still-unfulfilled construction stock requirement");
        Check(state.TryReconcile(10, 10, 32, 100, out adjusted) && adjusted == 0 && !state.Active,
            "sufficient actual construction cost cancels the no-longer-needed wood padding");
        Check(!state.TryReconcile(0, 0, 32, 100, out adjusted), "completed material goal does not revive after building consumption");

        state = default;
        state.ObserveMaterialStore(5, 0, 5, 0, 32, 100, out _);
        state.TryReconcile(5, 8, 32, 100, out _);
        Check(state.ObserveMaterialStore(0, 2, 9, 0, 32, 100, out merged) && merged && state.MaterialCost == 10,
            "new lower material requirement retains known larger unfulfilled cost");
        Check(state.TryReconcile(9, 0, 32, 100, out adjusted) && adjusted == 10,
            "merged material stock goal is not confused with a capped procurement package");
        Check(state.ObserveMaterialStore(0, 10, 20, 0, 32, 100, out merged) && merged && state.MaterialCost == 20,
            "larger material cost replaces the maximum without appending orders");

        state = Package();
        state.TryReconcile(5, 2, 32, 100, out _);
        Check(state.ObserveMaterialStore(1, 3, 4, 0, 32, 100, out merged) && !merged &&
            state.Policy == MarketDemandPolicy.MaterialStockGoal && state.MaterialCost == 5 && state.Credited == 0,
            "material writer supersedes a fixed batch instead of combining different purposes");
        Check(state.ObserveFixedStore(1, 4, 2, 32, 100, MarketDemandSourceFamily.Food, out refresh) && !refresh &&
            state.Policy == MarketDemandPolicy.FixedBatch && state.OriginalPackage == 2 && state.MaterialCost == 0,
            "explicit fixed writer supersedes material goal without retaining it secretly");

        state = default;
        Check(!state.ObserveMaterialStore(int.MaxValue, 0, int.MaxValue, 0, 32, 100, out _),
            "impossible derived native cost is rejected without overflow");
        Check(state.ObserveMaterialStore(2, 0, int.MaxValue, 2, 32, 100, out _) && state.MaterialCost == int.MaxValue,
            "largest representable padded source cost is accepted");
        Check(!state.TryReconcile(int.MaxValue, 0, 32, 100, out adjusted) && adjusted == int.MaxValue && !state.Active,
            "unrepresentable later padded deficit fails closed instead of wrapping");
    }

    private static void RelevantPolicyStamps()
    {
        var config = new MarketDemandPolicySnapshot { BuyWeapons = 5, BuyFoodAmount = 1, FoodThreshold = 1,
            LastStandPhase = 0, FixesFlag = 1, FixesThreshold = 7, FixesAmount = 5 };
        long weapon = MarketDemandPolicyStamp.Recruit(config, false), food = MarketDemandPolicyStamp.Food(config);
        Check(MarketDemandPolicyStamp.Fixes(config, out long fixes), "enabled Fixes snapshot has a relevant stamp");
        config.BuyFoodAmount = 8;
        Check(MarketDemandPolicyStamp.Recruit(config, false) == weapon && MarketDemandPolicyStamp.Food(config) != food &&
            MarketDemandPolicyStamp.Fixes(config, out long unchangedFixes) && unchangedFixes == fixes,
            "actual stress-driver food batch1to8 changes food policy without discarding weapon or Fixes credit");
        config.FoodThreshold = 2;
        long secondFood = MarketDemandPolicyStamp.Food(config);
        config.BuyWeapons = 6;
        Check(MarketDemandPolicyStamp.Food(config) == secondFood && MarketDemandPolicyStamp.Recruit(config, false) != weapon,
            "weapon policy is affected only by relevant effective weapon amount");

        config.BuyWeapons = 5;
        config.LastStandPhase = 8;
        Check(MarketDemandPolicyStamp.Recruit(config, true) == weapon,
            "normal five and combat LastStand five are the same effective procurement policy");
        config.BuyWeapons = 4;
        Check(MarketDemandPolicyStamp.Recruit(config, false) == 4 && MarketDemandPolicyStamp.Recruit(config, true) == 5,
            "bodyguard and economic recruits retain configured four even during combat LastStand five");
        config.LastStandPhase = 7;
        Check(MarketDemandPolicyStamp.Recruit(config, true) == 5, "LastStand countdown does not create a new procurement goal");

        config.FixesThreshold = 8;
        Check(MarketDemandPolicyStamp.Fixes(config, out long differentFixes) && differentFixes != fixes,
            "Fixes threshold belongs to that channel's policy");
        config.FixesFlag = 0;
        Check(!MarketDemandPolicyStamp.Fixes(config, out _), "disabled Fixes source cannot validate an old correction");
        Check(MarketDemandPolicyStamp.Literal(MarketDemandSourceFamily.Pitch, 10) !=
            MarketDemandPolicyStamp.Literal(MarketDemandSourceFamily.Aiv, 10) &&
            MarketDemandPolicyStamp.Literal(MarketDemandSourceFamily.Pitch, 10) !=
            MarketDemandPolicyStamp.Literal(MarketDemandSourceFamily.Pitch, 5),
            "fixed literal policies retain their family and actual package");
    }

    private static bool ExerciseWarmPath()
    {
        bool result = MarketDemandTarget.TryStartPackage(0, 5, 32, 100, out var state);
        result &= state.ObserveStock(5, 1, 32);
        result &= state.ObserveStock(5, 0, 32);
        result &= state.TryReconcile(5, 0, 32, 100, out int adjusted) && adjusted == 4;
        result &= state.ObserveFixedStore(0, 4, 5, 32, 100, MarketDemandSourceFamily.Recruit, out bool refreshed) && refreshed;
        result &= state.TryReconcile(5, 0, 32, 100, out adjusted) && adjusted == 4;
        result &= state.ObserveMaterialStore(2, 4, 3, 0, 32, 100, out bool merged) && !merged;
        result &= state.TryReconcile(3, 3, 32, 100, out adjusted) && adjusted == 2;
        result &= state.TryReconcile(2, 1, 32, 100, out adjusted) && adjusted == 4;
        return result;
    }

    private static void AssertInvalid(int pending, int stock, int aic, long policy, string detail)
    {
        var state = Package();
        Check(!state.TryReconcile(pending, stock, aic, policy, out int adjusted) && adjusted == pending && !state.Active, detail);
    }
}
