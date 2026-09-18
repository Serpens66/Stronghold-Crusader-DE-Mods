// Created with ReClass.NET 1.2 by KN4CK3R

enum TileType16 : __int8
{
    NoneOrDirt = 0,
    Foliage = 1 << 0, // 0x01
    DirtAndStones = 1 << 1, // 0x02
    Elevation1 = 1 << 2, // 0x04
    Elevation2 = 1 << 3, // 0x08
    OasisGrass = 1 << 4, // 0x10
    BeachOrWaves = 1 << 5, // 0x20
    CoarseSand = 1 << 6, // 0x40
    ThickFoliage = 1 << 7, // 0x80
};

class GameMoatWorkTask
{
public:
	int32_t PackedTileId; //0x0000
	int16_t TileX; //0x0004
	int16_t TileY; //0x0006
	int32_t TargetMode; //0x0008
	uint8_t OwnerPlayerId; //0x000C
	uint8_t Progress; //0x000D
	uint8_t WorkType; //0x000E
	uint8_t ReservationPentalty; //0x000F
}; //Size: 0x0010

class GamePitchDescriptor
{
public:
	uint32_t r_GlobalId; //0x0000
	int16_t r_PlayerOwneIdd; //0x0004
	uint16_t r_TileX; //0x0006
	uint16_t r_TileY; //0x0008
	uint16_t r_RandomSeed; //0x000A
	int16_t r_State; //0x000C
	int16_t r_FireTimer; //0x000E
	uint32_t r_Reserved; //0x0010
}; //Size: 0x0014

class GameTileManager
{
public:
	int32_t PackedNeighborTileDeltas[800][8]; //0x0000
	int32_t PackedRowTileCounts[800]; //0x6400
	uint8_t Unknown00007080[1024]; //0x7080
	int16_t PackedTileCoordinateLookup0[320800]; //0x7480
	int16_t PackedTileCoordinateLookup1[320800]; //0xA3EC0
	int32_t GfxLayer[320800]; //0x140900
	int32_t AlphaGfxLayer[320800]; //0x279D80
	int32_t ConstructionGfxLayer[320800]; //0x3B3200
	int32_t PillarGfxLayer[320800]; //0x4EC680
	int32_t WallGfxLayer[320800]; //0x625B00
	int16_t FloatingLayer[320800]; //0x75EF80
	uint16_t RandomLayer[320800]; //0x7FB9C0
	TilePropertyFlag LogicLayer[320800]; //0x898400
	int32_t Unknown009D1880[800]; //0x9D1880
	TileType16 Logic2Layer[320800]; //0x9D2500
	uint8_t Unknown00A20A20[800]; //0xA20A20
	uint8_t ChangedLayer[320800]; //0xA20D40
	uint16_t OrganismLayer[320800]; //0xA6F260
	uint16_t StructureLayer[320800]; //0xB0BCA0
	uint8_t StructureWasLayer[320800]; //0xBA86E0
	int16_t ChimpLayer[320800]; //0xBF6C00
	ProjectileType FlyLayer[320800]; //0xC93640
	uint8_t Unknown00D30080[320800]; //0xD30080
	uint8_t HeightLayer[320800]; //0xD7E5A0
	uint8_t DefaultHeightLayer[320800]; //0xDCCAC0
	uint8_t WallOwnerLayer[320800]; //0xE1AFE0
	uint8_t LuminescenceLayer[320800]; //0xE69500
	uint8_t ShowHiLayer[320800]; //0xEB7A20
	uint16_t MiscDisplayLayer[320800]; //0xF05F40
	uint8_t DamageLayer[320800]; //0xFA2980
	int16_t MacroLayer[320800]; //0xFF0EA0
	uint16_t PathConnectionLayer[320800]; //0x108D8E0
	uint8_t PathLinkageLayer[320800]; //0x112A320
	uint8_t OccupancyLayer[320800]; //0x1178840
	uint16_t CertainPathLayer[320800]; //0x11C6D60
	uint16_t WalkLayer[320800]; //0x12637A0
	uint8_t AiZoneLayer[320800]; //0x13001E0
	uint8_t AiInfoLayer[320800]; //0x134E700
	uint8_t AiDangerLayer[320800]; //0x139CC20
	uint8_t AiProximityLayer[320800]; //0x13EB140
	uint8_t TownDzSpreadIdLayer[320800]; //0x1439660
	uint8_t TownNullConnectsLayer[320800]; //0x1487B80
	uint8_t TownDzSpreadCountLayer[320800]; //0x14D60A0
	uint8_t TownStoneValueLayer[320800]; //0x15245C0
	uint8_t TownStructureLayer[320800]; //0x1572AE0
	uint8_t TownOasisLayer[320800]; //0x15C1000
	uint8_t TownFarmLayer[320800]; //0x160F520
	uint8_t TownIronLayer[320800]; //0x165DA40
	uint8_t ProblemBuildLayer[320800]; //0x16ABF60
	uint8_t Unknown016FA480[5774400]; //0x16FA480
	uint8_t AivBlockLayer[320800]; //0x1C7C0C0
	uint8_t Unknown01CCA5E0[16]; //0x1CCA5E0
	uint8_t AivBlockZone[6400]; //0x1CCA5F0
	uint8_t DelayLayer[320800]; //0x1CCBEF0
	uint8_t Unknown01D1A410[800]; //0x1D1A410
	uint8_t GatePathLayer[320800]; //0x1D1A730
	uint8_t Unknown01D68C50[800]; //0x1D68C50
	int32_t Unknown01D68F70[320800]; //0x1D68F70
	int16_t MoatWorkTaskIndexLayer[320800]; //0x1EA23F0
	class GameMoatWorkTask MoatWorkTasks[64000]; //0x1F3EE30
	int32_t MoatWorkTaskSlotLimit; //0x2038E30
	int32_t MoatWorkTaskActiveCount; //0x2038E34
	uint8_t Unknown02038E38[16]; //0x2038E38
	class GamePitchDescriptor PitchSlots[3999]; //0x2038E48
	uint32_t Unknown0204C6B4; //0x204C6B4
	uint8_t Unknown0204C6B8[12]; //0x204C6B8
	uint32_t PitchIdLimit; //0x204C6C4
	uint32_t Unknown0204C6C8; //0x204C6C8
	uint16_t PitchSlotLookup[4000]; //0x204C6CC
	uint8_t Unknown0204E60C[32]; //0x204E60C
	int32_t LayerInvalidationPending[3]; //0x204E62C
	int32_t Unknown0204E638; //0x204E638
	int32_t Unknown0204E63C; //0x204E63C
	int32_t Unknown0204E640; //0x204E640
	int32_t Unknown0204E644; //0x204E644
	int32_t Unknown0204E648; //0x204E648
	int32_t Unknown0204E64C; //0x204E64C
	int64_t Unknown0204E650; //0x204E650
	int32_t LastUpdateTimestamp; //0x204E658
	int32_t MapRotation; //0x204E65C
	int32_t DirectionCount; //0x204E660
	int32_t CameraAnchorTileX; //0x204E664
	int32_t CameraAnchorTileY; //0x204E668
	int32_t RotatedDirectionMap[5]; //0x204E66C
	int32_t Unknown0204E680[8]; //0x204E680
	int32_t Unknown0204E6A0; //0x204E6A0
	int32_t Unknown0204E6A4; //0x204E6A4
	int32_t Unknown0204E6A8; //0x204E6A8
	int32_t Unknown0204E6AC; //0x204E6AC
	int32_t Unknown0204E6B0; //0x204E6B0
	int32_t Unknown0204E6B4; //0x204E6B4
	int32_t Unknown0204E6B8; //0x204E6B8
	int32_t Unknown0204E6BC; //0x204E6BC
	int32_t Unknown0204E6C0; //0x204E6C0
	int32_t Unknown0204E6C4; //0x204E6C4
	int32_t Unknown0204E6C8; //0x204E6C8
	int32_t Unknown0204E6CC; //0x204E6CC
	uint8_t Unknown0204E6D0[40]; //0x204E6D0
	int32_t Unknown0204E6F8; //0x204E6F8
	int32_t IsPlacementBlocked; //0x204E6FC
	int32_t Unknown0204E700; //0x204E700
	int32_t Unknown0204E704; //0x204E704
	int32_t PlacementFailureReason; //0x204E708
	int32_t DetectedAdjacentGateRotation; //0x204E70C
	int32_t DrawbridgePlacementVariant; //0x204E710
	uint8_t Unknown0204E714[16]; //0x204E714
	int32_t PlacementHeightLimit; //0x204E724
	int32_t FootprintMinimumTerrainHeight; //0x204E728
	int32_t FootprintMaximumTerrainHeight; //0x204E72C
	int32_t FootprintMinimumEffectiveHeight; //0x204E730
	int32_t FootprintMaximumEffectiveHeight; //0x204E734
	int32_t MaximumFootprintHeightDelta; //0x204E738
	int32_t Unknown0204E73C; //0x204E73C
	int32_t PlacementAllowsLogicFlag00000080; //0x204E740
	int32_t Unknown0204E744; //0x204E744
	int32_t PlacementMode; //0x204E748
	int32_t PlacementAllowsLogicFlag40000000; //0x204E74C
	int32_t Unknown0204E750; //0x204E750
	int32_t Unknown0204E754; //0x204E754
	int32_t BuildingFootprintCellCount; //0x204E758
	int32_t Unknown0204E75C; //0x204E75C
	int32_t BuildingFootprintCellOffsetX; //0x204E760
	int32_t BuildingFootprintCellOffsetY; //0x204E764
	int32_t BuildingFootprintRotatedCellIndex; //0x204E768
	uint8_t Unknown0204E76C[36]; //0x204E76C
	uint32_t PlacementUpdateTimestamp; //0x204E790
	int32_t Unknown0204E794; //0x204E794
	uint8_t Unknown0204E798[76]; //0x204E798
	int32_t CurrentMapSize; //0x204E7E4
	int32_t Unknown0204E7E8; //0x204E7E8
	int32_t Unknown0204E7EC; //0x204E7EC
	uint8_t Unknown0204E7F0[64]; //0x204E7F0
	uint64_t LogicLayerPointer; //0x204E830
	uint64_t Logic2LayerPointer; //0x204E838
	uint64_t HeightLayerPointer; //0x204E840
	uint64_t ChangedLayerPointer; //0x204E848
	uint64_t Unknown0204E850Pointer; //0x204E850
	uint64_t OccupancyLayerPointer; //0x204E858
	uint64_t DamageLayerPointer; //0x204E860
	uint64_t PathConnectionLayerPointer; //0x204E868
	uint64_t AiZoneLayerPointer; //0x204E870
	uint64_t PitchSlotLookupPointer; //0x204E878
	uint64_t MiscDisplayLayerPointer; //0x204E880
}; //Size: 0x204E888