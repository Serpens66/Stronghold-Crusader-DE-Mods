enum AIDefensivePositionClass   : int
{
    Default = 0,

    OilSmelterEngineer = 1,

    Mangonel = 2,
    Ballista = 3,
    Trebuchet = 4,
    ArabBallista = 5,

    Archer = 6,
    Crossbowman = 7,
    Spearman = 8,
    Pikeman = 9,
    Maceman = 10,
    Swordsman = 11,
    Knight = 12,

    /// <summary>
    /// Normally Arabian slaves. Under a conditional override,
    /// additional unit types can also be assigned to this class.
    /// </summary>
    ArabSlaveOrConditionalOverride = 13,

    ArabSlinger = 14,
    Assassin = 15,
    ArabBow = 16,
    ArabHorseman = 17,
    ArabSwordsman = 18,
    ArabGrenadier = 19,

    /// <summary>No unit type is present in the normal lookup table.</summary>
    Reserved20 = 20,

    /// <summary>No unit type is present in the normal lookup table.</summary>
    Reserved21 = 21,

    BedouinCamelLancer = 22,
    BedouinHealer = 23,
    BedouinEunuch = 24,
    BedouinAmbusher = 25,
    BedouinSkirmisher = 26,
    BedouinHeavyCamel = 27,
    BedouinSapper = 28,
    BedouinDemolisher = 29
};