// Created with ReClass.NET 1.2 by KN4CK3R

enum AivBuildStepState : __int8
{
  Inactive = 0,
  Pending = 1,
  Unknown2 = 2,
  Built = 3,
  Abandoned = 4,
  RetryPending = 5
};

enum AivLayoutSelectionState : __int32
{
  None = 0,
  Usable = 1,
  Perfect = 2
};

enum AivMiscItemType : __int32
{
    None = 0,
    OilPourer = 1,
    Mangonel = 2,
    TowerMountedBallista = 3,
    Trebuchet = 4,
    FireBallista = 5,
    Archer = 6,
    Crossbowman = 7,
    Spearman = 8,
    Pikeman = 9,
    Maceman = 10,
    Swordsman = 11,
    Knight = 12,
    Slave = 13,
    Slinger = 14,
    Assassin = 15,
    ArabianArcher = 16,
    HorseArcher = 17,
    ArabianSwordsman = 18,
    FireThrower = 19,
    Brazier = 20,
    Flag = 21
};

enum AivRotation : __int32
{
    North = 0,
    East = 2,
    South = 4,
    West = 6
};

class AivBuildStep
{
public:
	AivBuildStepState	State; //0x0000
	uint8_t RebuildDelay; //0x0001
	eMappers BuildingType; //0x0002
	uint16_t TileCount; //0x0004
	uint16_t Unknown06; //0x0006
	uint32_t MapTileIdOrBufferIndex; //0x0008
}; //Size: 0x000C

class AivVillageState
{
public:
	uint32_t OwnerPlayerId; //0x0000
AILords AILord; //0x0004
AivRotation Rotation; //0x0008
	uint32_t SelectedVariantIndex; //0x000C
AivLayoutSelectionState LayoutSelectionState; //0x0010
	uint32_t UnlockedBuildStep; //0x0014
	uint32_t LowGoldBuildDelayElapsed; //0x0018
	uint32_t BuildRate; //0x001C
	uint32_t MaximumBuildStep; //0x0020
	uint32_t LayoutOriginX; //0x0024
	uint32_t LayoutOriginY; //0x0028
	uint32_t KeepX; //0x002C
	uint32_t KeepY; //0x0030
	class AivBuildStep BuildStepsBuffer[1000]; //0x0034
	uint32_t OrderedMapTileIds[4000]; //0x2F14
	uint32_t OrderedMapTileCount; //0x6D94
}; //Size: 0x6D98

class AivCoarseCell
{
public:
	uint32_t CoarseSearchGeneration; //0x0000
	uint8_t ForeignPathComponentTileCount; //0x0004
	uint8_t CoarseSearchDepth; //0x0005
	uint8_t Unknown06; //0x0006
	uint8_t TreeObstructionWeight; //0x0007
	uint16_t StoneTileCount; //0x0008
	uint8_t IronTileCount; //0x000A
	uint8_t PitchTileCount; //0x000B
	uint8_t SwampTileCount; //0x000C
	uint8_t MinimumHeight; //0x000D
	uint8_t MaximumHeight; //0x000E
	uint8_t HeightRangeExceeds12; //0x000F
	uint8_t StructureOrReservationCount; //0x0010
	uint8_t OutsideUsableMap; //0x0011
	uint8_t UnknownFlags91Count; //0x0012
	uint8_t UnknownFlags90Count; //0x0013
	uint8_t WoodcutterRetryDelay; //0x0014
	uint8_t Unknown14; //0x0015
	uint8_t OccupyingPlayerId; //0x0016
	uint8_t ImpassableEdgeTileCount; //0x0017
	uint8_t WoodcutterRemovalCount; //0x0018
	uint8_t CombinedUnknownFlags; //0x0019
	uint8_t N00006BD3[22]; //0x001A
}; //Size: 0x0030

class AivSystem
{
public:
	uint32_t Unknown000000; //0x0000
	class AivVillageState ReservedVillageSlot[9]; //0x0004
	uint32_t KeepPlacementX; //0x3DA5C
	uint32_t KeepPlacementY; //0x3DA60
	uint32_t Unknown03DA64; //0x3DA64
	uint32_t Unknown03DA68; //0x3DA68
	eMappers BuildingTypeGrid[10000]; //0x3DA6C
	int32_t BuildStepGrid[10000]; //0x4288C
	eMappers RotatedBuildingTypeGrid[10000]; //0x4C4CC
	int32_t RotatedBuildStepGrid[10000]; //0x512EC
	int32_t PauseFrameIndices[50]; //0x5AF2C
	int32_t PauseDelay; //0x5AFF4
	int32_t MiscItemPositions[320]; //0x5AFF8
	int32_t PlacementEvaluatedTileCount; //0x5B4F8
	int32_t PlacementObstructedTileCount; //0x5B4FC
	int32_t TotalTreeObstructionWeight; //0x5B500
	int32_t DominantPathComponentId; //0x5B504
	int32_t AiUpdatePhase; //0x5B508
	int32_t CoarseSearchGeneration; //0x5B50C
	uint8_t UnknownCoarseGridState[800]; //0x5B510
	class AivCoarseCell CoarseGridBuffer[25600]; //0x5B830
	int32_t CoarseSearchDepth; //0x187830
	int32_t CoarseSearchQueueReadIndex; //0x187834
	int32_t CoarseSearchQueueWriteIndex; //0x187838
	int32_t CoarseSearchQueueX[25600]; //0x18783C
	int32_t CoarseSearchQueueY[25600]; //0x1A083C
	int32_t CoarseSearchResultX; //0x1B983C
	int32_t CoarseSearchResultY; //0x1B9840
	uint8_t PlacementVisitedMask[10000]; //0x1B9844
	int32_t ActiveVillageCount; //0x1BBF54
}; //Size: 0x1BBF58