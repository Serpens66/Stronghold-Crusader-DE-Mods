// Created with ReClass.NET 1.2 by KN4CK3R

class GameVegetation
{
public:
	uint32_t N0000486B; //0x0000
	uint32_t N00004896; //0x0004
	uint32_t N0000486C; //0x0008
	uint32_t N00004898; //0x000C
	GM16	r_GameMaterialIndex; //0x0010
	uint16_t N0000542C; //0x0012
	uint32_t r_SpriteHueLevel; //0x0014
	uint32_t N0000486E; //0x0018
	uint32_t r_AnimationPlaybackSpeed; //0x001C
	uint16_t N0000486F; //0x0020
	uint16_t N00004951; //0x0022
	uint32_t N0000489E; //0x0024
	uint32_t N00004870; //0x0028
	uint32_t N000048A0; //0x002C
	uint32_t N00004871; //0x0030
	uint32_t N000048A2; //0x0034
	uint32_t N00004872; //0x0038
	uint32_t N000048A4; //0x003C
	uint32_t N00004873; //0x0040
	uint32_t N000048A6; //0x0044
	uint32_t N00004874; //0x0048
	uint32_t N000048A8; //0x004C
	eAliveState r_AliveState; //0x0050
	VegType	r_VegetationType; //0x0052
	uint32_t N000048AA; //0x0054
	uint32_t r_GlobalId; //0x0058
	uint32_t N000048AC; //0x005C
	uint32_t N0000487B; //0x0060
	uint32_t N000048AE; //0x0064
	uint16_t WorldPositionX; //0x0068
	uint16_t WorldPositionY; //0x006A
	uint16_t N000048B0; //0x006C
	uint16_t TilePositionX; //0x006E
	uint16_t TilePositionY; //0x0070
	uint16_t N00009C64; //0x0072
	uint32_t TileId; //0x0074
	uint32_t N0000488F; //0x0078
	uint16_t N000048B4; //0x007C
	uint16_t r_Health; //0x007E
	uint32_t N00004890; //0x0080
	uint16_t r_ResourceState; //0x0084
	uint16_t N00004958; //0x0086
	uint32_t N00004892; //0x0088
	uint32_t r_GrowthStage; //0x008C
	uint32_t r_GrowthProgress; //0x0090
	uint32_t N000048BA; //0x0094
	uint32_t N00004894; //0x0098
}; //Size: 0x009C

class GameVegetationManager
{
public:
	uint32_t N000047CF; //0x0000
	uint32_t N00004810; //0x0004
	uint32_t TotalActive; //0x0008
	uint32_t N00004812; //0x000C
	uint32_t TotalAllocated; //0x0010
	uint16_t N0002227E; //0x0014
	uint16_t N00022280; //0x0016
	class GameVegetation VegetationArray[5000]; //0x0018
}; //Size: 0xBE990