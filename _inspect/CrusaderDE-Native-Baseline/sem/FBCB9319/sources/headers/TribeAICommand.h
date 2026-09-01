
/// <summary>
/// Specifies the set of commands that can be issued to a unit's TribeAI, representing various actions such as movement,
/// attacking, building, and other unit behaviors.
/// </summary>
/// <remarks>This enumeration defines the possible instructions that may be assigned to units within the game AI
/// system. Some values correspond to specific actions, such as attacking a unit or building, moving to a location, or
/// performing engineering tasks. Several members are reserved or have unknown purposes and may be used internally still. 
/// The meaning and required parameters for each command may vary</remarks>
enum TribeAICommand : uint32_t
{
    Unknown0 = 0,
    Unknown1 = 1,
    Unknown2 = 2,
    MoveHerePosition = 3,       // Move unit to tile position, r9 = tileX, Stack1 = tileY
    AttackUnit = 4,             // Attack Unit as Meele/or ranged) r9 = TargetUnitId, Stack1 = TargetTribeGlobalId
    AttackTilePosition = 5,     // Attack Here: Ranged -> Place r9 = tileX, Stack1 = tileY, <- forced attack for attackers (range/meele), a6 = ? (15 on catapults)
    DigMoatTileId = 6,          // Dig Moat as Spearman/etc r9 = TileX, Stack1 = TileY, a6 = ? (1000 most of the time)

    // TODO: (used at E8 ? ? ? ? E9 ? ? ? ? 0F B7 84 2F)
    Unknown7 = 7,

    Unknown8 = 8,
    AttackBuilding = 9,         // Attack Building as Meele/Ranged r9 = BuildingId, Stack1 = TargetBuildingGlobalId
    Unknonw10 = 10,
    Unknown11 = 11,
    Unknown12 = 12,
    Unknown13 = 13,
    Unknown14 = 14,
    ManPitchCauldronOrBuildTent = 15, // (Man pitch cauldron OR: Build a tent as engineer) r9 = BuildingId, Stack1 = TargetBuildingGlobalId
    ManSiegeEquipment = 16,     // (Man catapult) r9 = UnitId(of catapult), Stack1 = TargetUnitGlobalId
    DissolveSiegeEquipment = 17,// r9 = UnitId(of catapult), Stack1 = TargetUnitGlobalId, a6 = 0
    Unknown18 = 18,
    Unknown19 = 19,
    ThrowLava = 20,             // Throw out lava as engineer r9 = TargetTileX; Stack1 = TargetTileY
    BuildTunnel = 21,           // Build tunnel here r9 = BuildingId, Stack1 = TargetBuildingGlobalId, a1 = 0
    Unknown22 = 22,
    AttackWallTileId = 23,      // Attack Wall as Meele r9 = TileId  (also counts for siege tower -> wall attach), Stack1 = Unused
    AttachLadderToWall = 24,    // (Attach Ladder to Wall) r9 = TileId, Stack1 = Unused
    Unknown25 = 25,
    Unknown26 = 26,
    Unknown27 = 27,
    Unknown28 = 28,
    Unknown29 = 29,
    UnitDissolve = 30,          // Dissolve r9 = unused, a6 = 1
    UnitStop = 31,              // Stop r9 = unused, a6 = 1

    // TODO: (used at E8 ? ? ? ? 44 8B 7C 24 ? E9)
    Unknown32 = 32,

    // TODO: (used by AI?)
    // rdx = The TribeId of an Archer unit
    // r9 = r_RangedAttackTargetUnitId of the issuing unit (0x340)
    // Stack1 = The UnitOffset calculated by using the r_RangedAttackTargetUnitId (0x340) unit field of the issuing unit
    Unknown33 = 33,

    Unknown34 = 34,

    // TODO: (used at E8 ? ? ? ? E9 ? ? ? ? 8B 84 24 ? ? ? ? 41 B8)
    Unknown35 = 35,
    ForceAttackBuilding = 36,   // Attack Here: Meele/Ranged -> Building r9 = BuildingId, Stack1 = TargetGlobalId, a6 = -127

    // TODO: (used at E8 ? ? ? ? E9 ? ? ? ? 90) (maybe something to do with tiles?)
    Unknown37 = 37,

    // TODO: (used at E8 ? ? ? ? E9 ? ? ? ? 41 B8 ? ? ? ? 44 89 6C 24)
    Unknown38 = 38              // USED BY AI ? (maybe something to do with tiles?)
};