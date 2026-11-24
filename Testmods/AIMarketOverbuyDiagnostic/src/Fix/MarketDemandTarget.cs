namespace AIMarketOverbuyDiagnostic
{
    internal enum MarketDemandPolicy { FixedBatch, MaterialStockGoal, AutoAmmo }
    internal enum MarketDemandSourceFamily { Recruit, Food, Scarcity, Pitch, Emergency, Aiv, Ammo, Fixes }

    internal struct MarketDemandPolicySnapshot
    {
        internal int BuyWeapons, BuyFoodAmount, FoodThreshold, FixesFlag, FixesThreshold, FixesAmount;
        internal uint LastStandPhase;
    }

    // Exact packed relevant fields, not a hash of unrelated AIC configuration.
    // Family and AIC identity are checked separately by the target contract.
    internal static class MarketDemandPolicyStamp
    {
        internal static long Recruit(MarketDemandPolicySnapshot snapshot, bool combat)
        {
            int amount = combat && MarketDemandTarget.LastStandPolicyCode(snapshot.LastStandPhase) != 0
                ? 5 : snapshot.BuyWeapons;
            return unchecked((uint)amount);
        }
        internal static long Food(MarketDemandPolicySnapshot snapshot) => Pair(snapshot.BuyFoodAmount, snapshot.FoodThreshold);
        internal static long Literal(MarketDemandSourceFamily family, int package) =>
            unchecked((long)(((ulong)(uint)family << 32) | (uint)package));
        internal static long Material => 0;
        internal static bool Fixes(MarketDemandPolicySnapshot snapshot, out long stamp)
        {
            stamp = 0;
            if (snapshot.FixesFlag == 0) return false;
            stamp = Pair(snapshot.FixesAmount, snapshot.FixesThreshold);
            return true;
        }
        private static long Pair(int amount, int threshold) => unchecked((long)(((ulong)(uint)threshold << 32) | (uint)amount));
    }
    // Pure per-player/per-good package accounting. The caller must prove a new
    // package under its source-specific goal contract, not merely an executed STORE:
    // a repeated native assignment may refresh the same still-open intent.
    internal struct MarketDemandTarget
    {
        internal bool Active;
        internal int ExpectedPending, OriginalPackage, Credited, LastObservedStock, AicId;
        internal int MaterialCost, MaterialPadding;
        internal MarketDemandPolicy Policy;
        internal MarketDemandSourceFamily Family;
        internal long Goal, PolicyToken;

        // Native combat purchase quantities distinguish positive LastStand from
        // normal mode, not the countdown values8..1 inside the positive mode.
        internal static uint LastStandPolicyCode(uint phase) => phase > 0 ? 1u : 0u;

        internal static bool TryStartPackage(int sourceStock, int emittedAmount, int aicId,
            long policyToken, out MarketDemandTarget state)
            => TryStartFixed(sourceStock, emittedAmount, aicId, policyToken, MarketDemandSourceFamily.Recruit, out state);

        private static bool TryStartFixed(int sourceStock, int emittedAmount, int aicId,
            long policyToken, MarketDemandSourceFamily family, out MarketDemandTarget state)
        {
            state = default;
            if (sourceStock < 0 || emittedAmount <= 0 || aicId <= 0) return false;
            // Goal is retained as original-source evidence only. Delivered goods
            // remain credited even after consumption; Goal is not an inventory target.
            state = new MarketDemandTarget { Active = true, ExpectedPending = emittedAmount,
                OriginalPackage = emittedAmount, LastObservedStock = sourceStock,
                Goal = (long)sourceStock + emittedAmount,
                AicId = aicId, PolicyToken = policyToken, Policy = MarketDemandPolicy.FixedBatch, Family = family };
            return true;
        }

        // A matching positive-owned fixed batch is the same procurement goal,
        // even if Vanilla refreshes its cell with the original, larger amount.
        // An observed clear, new amount, policy or family starts a fresh package.
        internal bool ObserveFixedStore(int sourceStock, int beforePending, int amount, int aicId,
            long policyToken, MarketDemandSourceFamily family, out bool refreshed)
        {
            refreshed = false;
            if (Active && Policy == MarketDemandPolicy.FixedBatch && Family == family &&
                OriginalPackage == amount && PolicyToken == policyToken &&
                CheckOwner(beforePending, sourceStock, aicId))
            {
                CreditStockChange(sourceStock);
                if (Credited < OriginalPackage)
                { ExpectedPending = amount; refreshed = true; return true; }
            }
            bool valid = TryStartFixed(sourceStock, amount, aicId, policyToken, family, out var next);
            this = next;
            return valid;
        }

        // CC420 stores an actual current deficit. Its cost is derived only from
        // that executed store's operands, never from a max-retained snapshot.
        internal bool ObserveMaterialStore(int sourceStock, int beforePending, int amount,
            int padding, int aicId, long policyToken, out bool merged)
        {
            merged = false;
            long cost = (long)sourceStock + amount - padding;
            if (sourceStock < 0 || amount <= 0 || aicId <= 0 || (padding != 0 && padding != 2) ||
                cost <= sourceStock || cost > int.MaxValue)
            { this = default; return false; }
            int required = (int)cost;
            if (Active && Policy == MarketDemandPolicy.MaterialStockGoal && MaterialPadding == padding &&
                PolicyToken == policyToken && CheckOwner(beforePending, sourceStock, aicId))
            { if (MaterialCost > required) required = MaterialCost; merged = true; }
            this = new MarketDemandTarget { Active = true, Policy = MarketDemandPolicy.MaterialStockGoal,
                MaterialCost = required, MaterialPadding = padding, ExpectedPending = amount,
                OriginalPackage = amount, LastObservedStock = sourceStock, AicId = aicId,
                PolicyToken = policyToken, Goal = (long)sourceStock + amount };
            return true;
        }

        // The caller separately proves AUTO origin and stable unit identity.
        // This is an atomic live-stock requirement, never fractional ammo credit.
        internal bool ObserveAutoAmmoStore(int sourceStock, int beforePending, int amount, int cost,
            int aicId, long policyToken, bool sameOrigin, out bool refreshed)
        {
            refreshed = false;
            if (sourceStock < 0 || cost <= sourceStock || amount <= 0 || aicId <= 0)
            { this = default; return false; }
            refreshed = sameOrigin && Active && Policy == MarketDemandPolicy.AutoAmmo &&
                MaterialCost == cost && OriginalPackage == amount && PolicyToken == policyToken &&
                CheckOwner(beforePending, sourceStock, aicId);
            this = new MarketDemandTarget { Active = true, Policy = MarketDemandPolicy.AutoAmmo,
                Family = MarketDemandSourceFamily.Ammo, MaterialCost = cost, OriginalPackage = amount,
                ExpectedPending = amount, LastObservedStock = sourceStock, AicId = aicId,
                PolicyToken = policyToken, Goal = cost };
            return true;
        }

        // True authorizes exactly this write. Only positively observed deliveries
        // reduce an owed package; later consumption cannot undo their credit.
        internal bool TryReconcile(int currentPending, int currentStock, int currentAicId,
            long currentPolicyToken, out int adjusted)
        {
            adjusted = currentPending;
            if (Policy == MarketDemandPolicy.AutoAmmo)
            { Active = false; return false; } // A stable live-unit proof is mandatory.
            if (!CheckOwner(currentPending, currentStock, currentAicId)) return false;
            if (currentPolicyToken != PolicyToken)
            { Active = false; return false; }
            int needed;
            if (Policy == MarketDemandPolicy.FixedBatch)
            { CreditStockChange(currentStock); needed = OriginalPackage - Credited; }
            else
            {
                LastObservedStock = currentStock;
                long missing = currentStock >= MaterialCost ? 0 : (long)MaterialCost - currentStock + MaterialPadding;
                // An extreme modded cost can make the padded demand unrepresentable.
                // Keep Vanilla's cell unchanged rather than overflow into a buy.
                if (missing > int.MaxValue) { Active = false; return false; }
                needed = (int)missing;
            }
            if (needed == 0) Active = false;
            if (needed == currentPending) return false;
            ExpectedPending = adjusted = needed;
            return true;
        }

        internal bool TryReconcileAutoAmmo(int currentPending, int currentStock, int currentAicId,
            long currentPolicyToken, int liveDesired, out int adjusted)
        {
            adjusted = currentPending;
            if (Policy != MarketDemandPolicy.AutoAmmo || !CheckOwner(currentPending, currentStock, currentAicId)) return false;
            if (currentPolicyToken != PolicyToken || liveDesired < 0 || liveDesired > MaterialCost)
            { Active = false; return false; }
            LastObservedStock = currentStock;
            if (liveDesired == 0) Active = false;
            if (liveDesired == currentPending) return false;
            ExpectedPending = adjusted = liveDesired;
            return true;
        }

        // Every committed stock boundary must be observed, including decreases.
        // This changes metadata only. The owner checks policy before any game write.
        internal bool ObserveStock(int currentPending, int currentStock, int currentAicId)
        {
            if (!CheckOwner(currentPending, currentStock, currentAicId)) return false;
            if (Policy == MarketDemandPolicy.FixedBatch) CreditStockChange(currentStock);
            else LastObservedStock = currentStock;
            return true;
        }

        internal bool NeedsAdjustment(int stock)
        {
            if (!Active) return false;
            long wanted = Policy == MarketDemandPolicy.FixedBatch ? OriginalPackage - Credited :
                stock >= MaterialCost ? 0 : (long)MaterialCost - stock + MaterialPadding;
            return wanted != ExpectedPending;
        }

        // Native 3EC90 supplies only its selected purchase and then clears that
        // pending cell. Refresh surviving packages without counting our own buy
        // as an avoided external delivery; its cleared package is invalidated.
        internal void ObserveDecisionEnd(int currentPending, int currentStock, int currentAicId)
        {
            if (CheckOwner(currentPending, currentStock, currentAicId)) LastObservedStock = currentStock;
        }

        private bool CheckOwner(int pending, int stock, int aicId)
        {
            if (!Active) return false;
            if (pending <= 0 || pending != ExpectedPending || stock < 0 || aicId <= 0 || aicId != AicId)
            { Active = false; return false; }
            return true;
        }

        private void CreditStockChange(int stock)
        {
            long delivered = (long)stock - LastObservedStock;
            LastObservedStock = stock;
            if (delivered <= 0) return;
            long total = Credited + delivered;
            Credited = total >= OriginalPackage ? OriginalPackage : (int)total;
        }

        internal void ObservePending(int value)
        {
            if (value <= 0 || value != ExpectedPending) Active = false;
        }
    }
}
