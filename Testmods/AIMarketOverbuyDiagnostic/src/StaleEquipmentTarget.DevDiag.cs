#if SERP_DEVDIAG
namespace AIMarketOverbuyDiagnostic
{
    // Pure ownership policy. Native callers prove goods/source/session identity.
    // The initial empty-stock request establishes a stock target, never a new queue.
    internal struct StaleEquipmentTarget
    {
        internal bool Active;
        internal int OriginalTarget, ExpectedPending, AicId;

        internal static bool TryCreate(int beforeStock, int afterStock, int beforePending, int afterPending,
            int nativeAicId, int buyWeapons, int lastStand, out StaleEquipmentTarget state)
        {
            state = default;
            if (beforeStock != 0 || afterStock != 0 || beforePending < 0 || afterPending <= 0 ||
                beforePending == afterPending || afterPending != buyWeapons || nativeAicId <= 0 || lastStand != 0)
                return false;
            state = new StaleEquipmentTarget { Active = true, OriginalTarget = afterPending,
                ExpectedPending = afterPending, AicId = nativeAicId };
            return true;
        }

        // True means a smaller quantity must be written. False never authorizes a write.
        internal bool TryReduce(int currentPending, int currentStock, int currentAicId,
            int currentBuyWeapons, int currentLastStand, out int adjusted)
        {
            adjusted = currentPending;
            if (!Active) return false;
            if (currentPending <= 0 || currentPending != ExpectedPending || currentStock < 0 ||
                currentAicId != AicId || currentBuyWeapons != OriginalTarget || currentLastStand != 0 ||
                OriginalTarget <= 0 || AicId <= 0)
            {
                Active = false;
                return false;
            }
            // Both operands are nonnegative; subtraction cannot overflow Int32.
            int needed = currentStock >= OriginalTarget ? 0 : OriginalTarget - currentStock;
            if (needed >= currentPending) return false;
            adjusted = needed;
            ExpectedPending = needed;
            if (needed == 0) Active = false;
            return true;
        }

        internal void ObservePending(int value)
        {
            // A clear irrevocably ends this ownership, including clear->same-value
            // renewal before the next purchase. Only a new confirmed source can rearm.
            if (value <= 0 || value != ExpectedPending) Active = false;
        }
    }
}
#endif
