namespace BugfixesAndQoL
{
    internal static class MarketGoldPolicy
    {
        internal static bool CanAfford(int availableGold, int cost)
        {
            // The Script Extender exposes gold as signed int; invalid negative values fail closed.
            return availableGold >= 0 && cost >= 0 && availableGold >= cost;
        }
    }
}
