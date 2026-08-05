namespace TrailEditor.Core;

public readonly record struct StartingGoldValues(int Human, int Computer, int Multiplier);

public static class SetupSemantics
{
    // The fourth row exists in GameData.starting_gold_table but has no normal UI button.
    private static readonly int[,,] StartingGoldTable =
    {
        { { 8000, 2000 }, { 4000, 2000 }, { 2000, 2000 }, { 2000, 4000 }, { 2000, 8000 } },
        { { 8000, 2000 }, { 4000, 2000 }, { 2000, 2000 }, { 2000, 4000 }, { 2000, 8000 } },
        { { 40000, 3000 }, { 20000, 7000 }, { 10000, 10000 }, { 7000, 20000 }, { 3000, 40000 } },
        { { 4000, 500 }, { 2000, 500 }, { 500, 500 }, { 500, 2000 }, { 500, 4000 } }
    };

    public static StartingGoldValues GetStartingGold(TrailData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        int level = data.Setup.StartingGoodsLevel;
        int fairness = data.Setup.Fairness;
        if (level is < 1 or > 4)
            throw new InvalidDataException("StartingGoodsLevel must be between 1 and 4; level 4 is the hidden 500-gold preset.");
        if (fairness is < 1 or > 5)
            throw new InvalidDataException("Fairness must be between 1 and 5.");

        int multiplier = data.CustomisedExtremeTrail ? 3 : 1;
        int human = data.Setup.NoGold > 0 ? 0 : StartingGoldTable[level - 1, fairness - 1, 0] * multiplier;
        int computer = StartingGoldTable[level - 1, fairness - 1, 1] * multiplier;
        return new StartingGoldValues(human, computer, multiplier);
    }
}
