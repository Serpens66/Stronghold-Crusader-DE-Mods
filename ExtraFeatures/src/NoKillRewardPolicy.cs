namespace ExtraFeatures
{
    internal static class NoKillRewardPolicy
    {
        internal const int VanillaMaximumGold = 100000;
        // Both halves of Vanilla's gold notification are signed 16-bit values.
        internal const int MaximumMessageChunk = 900000000;

        internal static int Gold(int defeatedGold, int percent, int maximum)
        {
            long amount = (long)System.Math.Max(0, defeatedGold) * (percent < 0 ? 25 : percent) / 100;
            if (percent < 0) amount = System.Math.Max(500, amount);
            return (int)System.Math.Min(amount, maximum < 0 ? VanillaMaximumGold : maximum);
        }

        internal static int Goods(int total, int percent) =>
            (int)((long)System.Math.Max(0, total) * (percent < 0 ? 25 : percent) / 100);

        internal static int LimitCredit(int amount, int current) =>
            (int)System.Math.Max(0, System.Math.Min((long)amount, (long)int.MaxValue - current));
    }

    internal sealed class KillRewardConfiguration
    {
        internal static readonly KillRewardConfiguration Vanilla = new KillRewardConfiguration(-1, -1, -1, -1, -1, -1);
        internal readonly int HumanGold, HumanGoods, HumanMaximum, AIGold, AIGoods, AIMaximum;
        internal KillRewardConfiguration(int humanGold, int humanGoods, int humanMaximum, int aiGold, int aiGoods, int aiMaximum)
        {
            HumanGold = humanGold; HumanGoods = humanGoods; HumanMaximum = humanMaximum;
            AIGold = aiGold; AIGoods = aiGoods; AIMaximum = aiMaximum;
        }
        internal bool IsVanilla(bool ai) => ai
            ? AIGold == -1 && AIGoods == -1 && AIMaximum == -1
            : HumanGold == -1 && HumanGoods == -1 && HumanMaximum == -1;
    }
}
