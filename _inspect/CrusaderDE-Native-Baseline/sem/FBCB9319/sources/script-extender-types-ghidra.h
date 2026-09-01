#ifndef SERPS_SHCDE_GHIDRA_TYPES_H
#define SERPS_SHCDE_GHIDRA_TYPES_H
typedef signed char int8_t;
typedef unsigned char uint8_t;
typedef signed short int16_t;
typedef unsigned short uint16_t;
typedef signed int int32_t;
typedef unsigned int uint32_t;
typedef signed long long int64_t;
typedef unsigned long long uint64_t;


/* Derived from Enums.h; C++ syntax normalized for Ghidra CParser. */


typedef enum eGlobals
{
    eGlobals_maxColorPicks = 18,
    eGlobals_maxDefinedColours = 141
} eGlobals;


typedef enum eSortOrders
{
    eSortOrders_startingOrder = -20000,
    eSortOrders_chevronTile = 1,
    eSortOrders_tilemapTile = 0,
    eSortOrders_numSortLayers = 49,
    eSortOrders_tilemapLayer = 0,
    eSortOrders_tilemapAlphaLayer,
    eSortOrders_wallFillinLayer,
    eSortOrders_pixieLo,
    eSortOrders_orgLayer,
    eSortOrders_orgLayer2,
    eSortOrders_orgLayer3,
    eSortOrders_orgLayer4,
    eSortOrders_chimpLayer,
    eSortOrders_chimpLayer2,
    eSortOrders_chimpLayer3,
    eSortOrders_chimpLayer4,
    eSortOrders_chimpLayer5,
    eSortOrders_chimpLayer6,
    eSortOrders_chimpLayer7,
    eSortOrders_flyLayer,
    eSortOrders_buildingLayer1,
    eSortOrders_buildingLayer25 = 40,
    eSortOrders_pixieHi,
    eSortOrders_chimplayer_buffered_diff = 34,
    eSortOrders_chimpLayer_buffered = 42,
    eSortOrders_chimpLayer2_buffered,
    eSortOrders_chimpLayer3_buffered,
    eSortOrders_chimpLayer4_buffered,
    eSortOrders_chimpLayer5_buffered,
    eSortOrders_chimpLayer6_buffered,
    eSortOrders_chimpLayer7_buffered
} eSortOrders;


typedef enum eDefines
{
    eDefines_maxSpriteNameLength = 30,
    eDefines_maxTagNameLength = 30,
    eDefines_maxTitleLength = 30,
    eDefines_maxSprites = 4000,
    eDefines_randomSeed = 1066,
    eDefines_tileScale = 1
} eDefines;


typedef enum mode
{
    mode_temp,
    mode_final,
    mode_add = 0,
    mode_delete,
    mode_updateOn,
    mode_updateOff,
    mode_permenant = 1,
    mode_removable,
    mode_building = 1,
    mode_mapElement,
    mode_looping = 0,
    mode_oneShot,
    mode_oneShotHold,
    mode_directionBased,
    mode_running = 0,
    mode_stopped = -1,
    mode_advance = -2
} mode;


typedef enum eGameVars
{
    eGameVars_maxMapSize = 800
} eGameVars;


typedef enum Logic
{
    Logic_empty,
    Logic_open,
    Logic_wall,
    Logic_road,
    Logic_building_Logic = 11,
    Logic_sea = 23,
    Logic_river,
    Logic_lake,
    Logic_rocks
} Logic;


typedef enum Terrain
{
    Terrain_basic,
    Terrain_sea_Terrain,
    Terrain_grass
} Terrain;


typedef enum Structure
{
    Structure_realityEdge,
    Structure_empty_Structure,
    Structure_Occupied,
    Structure_open_Structure,
    Structure_NOGO,
    Structure_mapEdge
} Structure;


typedef enum RegionType
{
    RegionType_open_RegionType,
    RegionType_impassable,
    RegionType_special1,
    RegionType_special2
} RegionType;


typedef enum Operation
{
    Operation_getSize = 1,
    Operation_clear,
    Operation_save = 11,
    Operation_load
} Operation;


typedef enum Dircs
{
    Dircs_Invalid = -1,
    Dircs_Centre,
    Dircs_North,
    Dircs_NE,
    Dircs_East,
    Dircs_SE,
    Dircs_South,
    Dircs_SW,
    Dircs_West,
    Dircs_NW,
    Dircs_Base
} Dircs;


typedef enum eDebugData
{
    eDebugData_none,
    eDebugData_logic,
    eDebugData_Spreads,
    eDebugData_road_eDebugData,
    eDebugData_structure,
    eDebugData_random,
    eDebugData_scan = 7
} eDebugData;


typedef enum editorActions
{
    editorActions_none_editorActions,
    editorActions_changeMap = 3,
    editorActions_placingBuilding = 5,
    editorActions_deleteBuilding,
    editorActions_placeEditorTroop,
    editorActions_troopSelection,
    editorActions_troopSelectionEnding
} editorActions;


typedef enum editorItemTypes
{
    editorItemTypes_none_editorItemTypes,
    editorItemTypes_itemTerrain
} editorItemTypes;


typedef enum eTerrainModes
{
    eTerrainModes_Undefined,
    eTerrainModes_delete_eTerrainModes,
    eTerrainModes_raise,
    eTerrainModes_lower,
    eTerrainModes_landHi,
    eTerrainModes_landMid,
    eTerrainModes_landLo,
    eTerrainModes_seaHi,
    eTerrainModes_plataeu,
    eTerrainModes_smooth,
    eTerrainModes_paintGrass,
    eTerrainModes_paintRock,
    eTerrainModes_paintMegaRock,
    eTerrainModes_paintMegaGrass,
    eTerrainModes_paintLake
} eTerrainModes;


typedef enum eTints
{
    eTints_grass_eTints,
    eTints_rock = 16,
    eTints_lake_eTints = 20
} eTints;


typedef enum eCSSColors
{
    eCSSColors_ALICEBLUE,
    eCSSColors_ANTIQUEWHITE,
    eCSSColors_AQUA,
    eCSSColors_AQUAMARINE,
    eCSSColors_AZURE,
    eCSSColors_BEIGE,
    eCSSColors_BISQUE,
    eCSSColors_BLACK,
    eCSSColors_BLANCHEDALMOND,
    eCSSColors_BLUE,
    eCSSColors_BLUEVIOLET,
    eCSSColors_BROWN,
    eCSSColors_BURLYWOOD,
    eCSSColors_CADETBLUE,
    eCSSColors_CHARTREUSE,
    eCSSColors_CHOCOLATE,
    eCSSColors_CORAL = 6,
    eCSSColors_CORNFLOWERBLUE = 17,
    eCSSColors_CORNSILK,
    eCSSColors_CRIMSON,
    eCSSColors_CYAN,
    eCSSColors_DARKBLUE,
    eCSSColors_DARKCYAN,
    eCSSColors_DARKGOLDENROD,
    eCSSColors_DARKGRAY,
    eCSSColors_DARKGREY,
    eCSSColors_DARKGREEN,
    eCSSColors_DARKKHAKI,
    eCSSColors_DARKMAGENTA,
    eCSSColors_DARKOLIVEGREEN,
    eCSSColors_DARKORANGE,
    eCSSColors_DARKORCHID,
    eCSSColors_DARKRED,
    eCSSColors_DARKSALMON,
    eCSSColors_DARKSEAGREEN,
    eCSSColors_DARKSLATEBLUE,
    eCSSColors_DARKSLATEGRAY,
    eCSSColors_DARKSLATEGREY,
    eCSSColors_DARKTURQUOISE,
    eCSSColors_DARKVIOLET,
    eCSSColors_DEEPPINK,
    eCSSColors_DEEPSKYBLUE,
    eCSSColors_DIMGRAY,
    eCSSColors_DIMGREY,
    eCSSColors_DODGERBLUE,
    eCSSColors_FIREBRICK,
    eCSSColors_FLORALWHITE,
    eCSSColors_FORESTGREEN,
    eCSSColors_FUCHSIA,
    eCSSColors_GAINSBORO,
    eCSSColors_GHOSTWHITE,
    eCSSColors_GOLD,
    eCSSColors_GOLDENROD,
    eCSSColors_GRAY,
    eCSSColors_GREY,
    eCSSColors_GREEN,
    eCSSColors_GREENYELLOW,
    eCSSColors_HONEYDEW,
    eCSSColors_HOTPINK,
    eCSSColors_INDIANRED,
    eCSSColors_INDIGO,
    eCSSColors_IVORY,
    eCSSColors_KHAKI,
    eCSSColors_LAVENDER,
    eCSSColors_LAVENDERBLUSH,
    eCSSColors_LAWNGREEN,
    eCSSColors_LEMONCHIFFON,
    eCSSColors_LIGHTBLUE,
    eCSSColors_LIGHTCORAL,
    eCSSColors_LIGHTCYAN,
    eCSSColors_LIGHTGOLDENRODYELLOW,
    eCSSColors_LIGHTGRAY,
    eCSSColors_LIGHTGREY,
    eCSSColors_LIGHTGREEN,
    eCSSColors_LIGHTPINK,
    eCSSColors_LIGHTSALMON,
    eCSSColors_LIGHTSEAGREEN,
    eCSSColors_LIGHTSKYBLUE,
    eCSSColors_LIGHTSLATEGRAY,
    eCSSColors_LIGHTSLATEGREY,
    eCSSColors_LIGHTSTEELBLUE,
    eCSSColors_LIGHTYELLOW,
    eCSSColors_LIME,
    eCSSColors_LIMEGREEN,
    eCSSColors_LINEN,
    eCSSColors_MAGENTA,
    eCSSColors_MAROON,
    eCSSColors_MEDIUMAQUAMARINE,
    eCSSColors_MEDIUMBLUE,
    eCSSColors_MEDIUMORCHID,
    eCSSColors_MEDIUMPURPLE,
    eCSSColors_MEDIUMSEAGREEN,
    eCSSColors_MEDIUMSLATEBLUE,
    eCSSColors_MEDIUMSPRINGGREEN,
    eCSSColors_MEDIUMTURQUOISE,
    eCSSColors_MEDIUMVIOLETRED,
    eCSSColors_MIDNIGHTBLUE,
    eCSSColors_MINTCREAM,
    eCSSColors_MISTYROSE,
    eCSSColors_MOCCASIN,
    eCSSColors_NAVAJOWHITE,
    eCSSColors_NAVY,
    eCSSColors_OLDLACE,
    eCSSColors_OLIVE,
    eCSSColors_OLIVEDRAB,
    eCSSColors_ORANGE,
    eCSSColors_ORANGERED,
    eCSSColors_ORCHID,
    eCSSColors_PALEGOLDENROD,
    eCSSColors_PALEGREEN,
    eCSSColors_PALETURQUOISE,
    eCSSColors_PALEVIOLETRED,
    eCSSColors_PAPAYAWHIP,
    eCSSColors_PEACHPUFF,
    eCSSColors_PERU,
    eCSSColors_PINK,
    eCSSColors_PLUM,
    eCSSColors_POWDERBLUE,
    eCSSColors_PURPLE,
    eCSSColors_REBECCAPURPLE,
    eCSSColors_RED,
    eCSSColors_ROSYBROWN,
    eCSSColors_ROYALBLUE,
    eCSSColors_SADDLEBROWN,
    eCSSColors_SALMON,
    eCSSColors_SANDYBROWN,
    eCSSColors_SEAGREEN,
    eCSSColors_SEASHELL,
    eCSSColors_SIENNA,
    eCSSColors_SILVER,
    eCSSColors_SKYBLUE,
    eCSSColors_SLATEBLUE,
    eCSSColors_SLATEGRAY,
    eCSSColors_SLATEGREY,
    eCSSColors_SNOW,
    eCSSColors_SPRINGGREEN,
    eCSSColors_STEELBLUE,
    eCSSColors_TAN,
    eCSSColors_TEAL,
    eCSSColors_THISTLE,
    eCSSColors_TOMATO,
    eCSSColors_TURQUOISE,
    eCSSColors_VIOLET,
    eCSSColors_WHEAT,
    eCSSColors_WHITE,
    eCSSColors_WHITESMOKE,
    eCSSColors_YELLOW,
    eCSSColors_YELLOWGREEN
} eCSSColors;


typedef enum eGameColors
{
    eGameColors_white_eGameColors,
    eGameColors_black_eGameColors,
    eGameColors_blue_eGameColors,
    eGameColors_green_eGameColors,
    eGameColors_red_eGameColors,
    eGameColors_darkred_eGameColors,
    eGameColors_orange_eGameColors,
    eGameColors_yellow_eGameColors,
    eGameColors_maxCols
} eGameColors;


typedef enum eObjectDefines
{
    eObjectDefines_maxGroups = 2,
    eObjectDefines_maxPoolTypes = 1
} eObjectDefines;


typedef enum eObjectGroups
{
    eObjectGroups_general,
    eObjectGroups_orgs
} eObjectGroups;


typedef enum eSH1Defines
{
    eSH1Defines_FILENAME_MAX_LENGTH = 80,
    eSH1Defines_ENGINEER_COST = 30,
    eSH1Defines_TUNNELER_COST = 30,
    eSH1Defines_LADDERMAN_COST = 4,
    eSH1Defines_ARCHER_COST = 12,
    eSH1Defines_XBOWMAN_COST = 20,
    eSH1Defines_SPEARMAN_COST = 8,
    eSH1Defines_PIKEMAN_COST = 20,
    eSH1Defines_MACEMAN_COST = 20,
    eSH1Defines_SWORDSMAN_COST = 40,
    eSH1Defines_KNIGHT_COST = 40,
    eSH1Defines_LADDERMAN_COST_ADVANCED = 20
} eSH1Defines;


typedef enum eStructs
{
    eStructs_STRUCT_NULL,
    eStructs_STRUCT_HOVEL,
    eStructs_STRUCT_OUTPOST_BEDOUIN,
    eStructs_STRUCT_WOODCUTTERS_HUT,
    eStructs_STRUCT_OXEN_BASE,
    eStructs_STRUCT_IRON_MINE,
    eStructs_STRUCT_PITCH_DIGGER,
    eStructs_STRUCT_HUNTERS_HUT,
    eStructs_STRUCT_BARRACKS_WOOD,
    eStructs_STRUCT_BARRACKS_STONE,
    eStructs_STRUCT_GOODS_YARD,
    eStructs_STRUCT_ARMOURY,
    eStructs_STRUCT_FLETCHERS_WORKSHOP,
    eStructs_STRUCT_BLACKSMITHS_WORKSHOP,
    eStructs_STRUCT_POLETURNERS_WORKSHOP,
    eStructs_STRUCT_ARMOURERS_WORKSHOP,
    eStructs_STRUCT_TANNERS_WORKSHOP,
    eStructs_STRUCT_BAKERS_WORKSHOP,
    eStructs_STRUCT_BREWERS_WORKSHOP,
    eStructs_STRUCT_GRANARY,
    eStructs_STRUCT_QUARRY,
    eStructs_STRUCT_QUARRYPILE,
    eStructs_STRUCT_INN,
    eStructs_STRUCT_HEALER,
    eStructs_STRUCT_ENGINEERS_GUILD,
    eStructs_STRUCT_TUNNELLERS_GUILD,
    eStructs_STRUCT_TRADEPOST,
    eStructs_STRUCT_WELL,
    eStructs_STRUCT_OIL_SMELTER,
    eStructs_STRUCT_SIEGE_TENT,
    eStructs_STRUCT_WHEATFARM,
    eStructs_STRUCT_HOPSFARM,
    eStructs_STRUCT_APPLEFARM,
    eStructs_STRUCT_CATTLEFARM,
    eStructs_STRUCT_MILL,
    eStructs_STRUCT_STABLES,
    eStructs_STRUCT_CHURCH1,
    eStructs_STRUCT_CHURCH2,
    eStructs_STRUCT_CHURCH3,
    eStructs_STRUCT_RUINS,
    eStructs_STRUCT_KEEP_ONE,
    eStructs_STRUCT_KEEP_TWO,
    eStructs_STRUCT_KEEP_THREE,
    eStructs_STRUCT_KEEP_FOUR,
    eStructs_STRUCT_KEEP_FIVE,
    eStructs_STRUCT_GATE_MAIN,
    eStructs_STRUCT_GATE_INNER,
    eStructs_STRUCT_GATE_WOOD,
    eStructs_STRUCT_GATE_POSTERN,
    eStructs_STRUCT_DRAWBRIDGE,
    eStructs_STRUCT_TUNNEL_ENTERANCE,
    eStructs_STRUCT_PARADEGROUND_OIL,
    eStructs_STRUCT_SIGNPOST,
    eStructs_STRUCT_PARADEGROUND_ENG,
    eStructs_STRUCT_SIEGE_TENT_ARAB_BALLISTA,
    eStructs_STRUCT_CAMPGROUND,
    eStructs_STRUCT_PARADEGROUND_MISS,
    eStructs_STRUCT_PARADEGROUND_LGT,
    eStructs_STRUCT_PARADEGROUND_HVY,
    eStructs_STRUCT_PARADEGROUND_TUN,
    eStructs_STRUCT_GATEHOUSE,
    eStructs_STRUCT_TOWER,
    eStructs_STRUCT_GALLOWS,
    eStructs_STRUCT_STOCKS,
    eStructs_STRUCT_WITCH_HOIST,
    eStructs_STRUCT_MAYPOLE,
    eStructs_STRUCT_GARDEN,
    eStructs_STRUCT_KILLING_PIT,
    eStructs_STRUCT_PITCH_DITCH,
    eStructs_STRUCT_SIEGE_TOWER,
    eStructs_STRUCT_WATERPOT,
    eStructs_STRUCT_KEEPDOOR_LEFT,
    eStructs_STRUCT_KEEPDOOR_RIGHT,
    eStructs_STRUCT_KEEPDOOR,
    eStructs_STRUCT_TOWER1,
    eStructs_STRUCT_TOWER2,
    eStructs_STRUCT_TOWER3,
    eStructs_STRUCT_TOWER4,
    eStructs_STRUCT_TOWER5,
    eStructs_STRUCT_TOWER5_DESTROYED,
    eStructs_STRUCT_SIEGE_TENT_CATAPULT,
    eStructs_STRUCT_SIEGE_TENT_TREBUCHET,
    eStructs_STRUCT_SIEGE_TENT_SIEGE_TOWER,
    eStructs_STRUCT_SIEGE_TENT_BATTERING_RAM,
    eStructs_STRUCT_SIEGE_TENT_PORTABLE_SHIELD,
    eStructs_STRUCT_TUNNEL_CONSTRUCTION,
    eStructs_STRUCT_TOWER1_DESTROYED,
    eStructs_STRUCT_TOWER2_DESTROYED,
    eStructs_STRUCT_TOWER3_DESTROYED,
    eStructs_STRUCT_TOWER4_DESTROYED,
    eStructs_STRUCT_WAS_WALL,
    eStructs_STRUCT_CESS_PIT,
    eStructs_STRUCT_BURNING_STAKE,
    eStructs_STRUCT_GIBBET,
    eStructs_STRUCT_DUNGEON,
    eStructs_STRUCT_RACK_STRETCHING,
    eStructs_STRUCT_RACK_FLOGGING,
    eStructs_STRUCT_CHOPPING_BLOCK,
    eStructs_STRUCT_DUNKING_STOOL,
    eStructs_STRUCT_DOG_CAGE,
    eStructs_STRUCT_STATUE,
    eStructs_STRUCT_SHRINE,
    eStructs_STRUCT_BEE_HIVE,
    eStructs_STRUCT_DANCING_BEAR,
    eStructs_STRUCT_POND,
    eStructs_STRUCT_BEAR_CAVE,
    eStructs_STRUCT_OUTPOST,
    eStructs_STRUCT_OUTPOST_ARAB,
    eStructs_STRUCT_BEDOUIN_STOCKADE,
    eStructs_STRUCT_DOCK,
    eStructs_STRUCT_MAX,
    eStructs_STRUCT_WOOD_WALL = 110,
    eStructs_STRUCT_STONE_WALL,
    eStructs_STRUCT_CRENAL_WALL,
    eStructs_STRUCT_STAIRS,
    eStructs_STRUCT_BRAZIER,
    eStructs_STRUCT_MANGONEL,
    eStructs_STRUCT_BALLISTA,
    eStructs_STRUCT_HEAD_ON_SPIKE,
    eStructs_STRUCT_GARDEN_SMALL,
    eStructs_STRUCT_GARDEN_MED,
    eStructs_STRUCT_GARDEN_LARGE,
    eStructs_STRUCT_POND_SMALL,
    eStructs_STRUCT_POND_LARGE,
    eStructs_STRUCT_FLAG1,
    eStructs_STRUCT_FLAG2,
    eStructs_STRUCT_FLAG3,
    eStructs_STRUCT_FLAG4,
    eStructs_STRUCT_GATE_WOOD1A,
    eStructs_STRUCT_GATE_WOOD1B,
    eStructs_STRUCT_GATE_WOOD1C,
    eStructs_STRUCT_GATE_WOOD1D,
    eStructs_STRUCT_GATE_STONE1A,
    eStructs_STRUCT_GATE_STONE1B,
    eStructs_STRUCT_GATE_STONE2A,
    eStructs_STRUCT_GATE_STONE2B,
    eStructs_STRUCT_RUINS01,
    eStructs_STRUCT_RUINS02,
    eStructs_STRUCT_RUINS03,
    eStructs_STRUCT_RUINS04,
    eStructs_STRUCT_RUINS05,
    eStructs_STRUCT_RUINS06,
    eStructs_STRUCT_RUINS07,
    eStructs_STRUCT_RUINS08,
    eStructs_STRUCT_RUINS09,
    eStructs_STRUCT_RUINS10,
    eStructs_STRUCT_RUINS11,
    eStructs_STRUCT_RUINS12,
    eStructs_STRUCT_RUINS13,
    eStructs_STRUCT_PEOPLE_ARCHERS,
    eStructs_STRUCT_PEOPLE_SPEARMEN,
    eStructs_STRUCT_PEOPLE_PIKEMEN,
    eStructs_STRUCT_PEOPLE_MACEMEN,
    eStructs_STRUCT_PEOPLE_XBOWMEN,
    eStructs_STRUCT_PEOPLE_SWORDSMEN,
    eStructs_STRUCT_PEOPLE_KNIGHTS,
    eStructs_STRUCT_PEOPLE_LADDERMEN,
    eStructs_STRUCT_PEOPLE_ENGINEERS,
    eStructs_STRUCT_PEOPLE_ENGINEERS_POTS,
    eStructs_STRUCT_PEOPLE_MONKS,
    eStructs_STRUCT_PEOPLE_CATAPULTS,
    eStructs_STRUCT_PEOPLE_TREBUCHETS,
    eStructs_STRUCT_PEOPLE_BATTERING_RAMS,
    eStructs_STRUCT_PEOPLE_SIEGE_TOWERS,
    eStructs_STRUCT_PEOPLE_PORTABLE_SHIELDS,
    eStructs_STRUCT_PEOPLE_TUNNELERS,
    eStructs_STRUCT_NEW_DIG_MOAT = 168,
    eStructs_STRUCT_NEW_FILL_MOAT,
    eStructs_STRUCT_MARKER_POINT1,
    eStructs_STRUCT_MARKER_POINT2,
    eStructs_STRUCT_MARKER_POINT3,
    eStructs_STRUCT_MARKER_POINT4,
    eStructs_STRUCT_MARKER_POINT5,
    eStructs_STRUCT_MARKER_POINT6,
    eStructs_STRUCT_MARKER_POINT7,
    eStructs_STRUCT_MARKER_POINT8,
    eStructs_STRUCT_MARKER_POINT9,
    eStructs_STRUCT_MARKER_POINT10,
    eStructs_STRUCT_RUINS14,
    eStructs_STRUCT_RUINS15,
    eStructs_STRUCT_RUINS16,
    eStructs_STRUCT_RUINS17,
    eStructs_STRUCT_POND5,
    eStructs_STRUCT_POND6,
    eStructs_STRUCT_POND7,
    eStructs_STRUCT_POND8,
    eStructs_STRUCT_IN_REPORTS = 190,
    eStructs_STRUCT_SUB_MENU_TOWERS = 200,
    eStructs_STRUCT_SUB_MENU_MILITARY,
    eStructs_STRUCT_SUB_MENU_GATEHOUSES,
    eStructs_STRUCT_SUB_MENU_KEEPS,
    eStructs_STRUCT_SUB_MENU_GATEHOUSES_WOOD,
    eStructs_STRUCT_SUB_MENU_GATEHOUSES_STONESMALL,
    eStructs_STRUCT_SUB_MENU_GATEHOUSES_STONELARGE,
    eStructs_STRUCT_SUB_MENU_GOOD,
    eStructs_STRUCT_SUB_MENU_BAD,
    eStructs_STRUCT_NEW_EDITOR_DELETE,
    eStructs_STRUCT_MENU_RETURN_TOWERS,
    eStructs_STRUCT_MENU_RETURN_GATEHOUSES,
    eStructs_STRUCT_MENU_RETURN_MILITARY,
    eStructs_STRUCT_MENU_RETURN_KEEPS,
    eStructs_STRUCT_MENU_RETURN_GOOD,
    eStructs_STRUCT_MENU_RETURN_BAD,
    eStructs_STRUCT_NEW_DELETE,
    eStructs_STRUCT_PEOPLE_ARAB_BOW = 220,
    eStructs_STRUCT_PEOPLE_ARAB_SLAVE,
    eStructs_STRUCT_PEOPLE_ARAB_SLINGER,
    eStructs_STRUCT_PEOPLE_ARAB_ASSASIN,
    eStructs_STRUCT_PEOPLE_ARAB_HORSEMAN,
    eStructs_STRUCT_PEOPLE_ARAB_SWORDSMAN,
    eStructs_STRUCT_PEOPLE_ARAB_GRENADIER,
    eStructs_STRUCT_PEOPLE_ARAB_BALLISTA,
    eStructs_STRUCT_RUINS18 = 230,
    eStructs_STRUCT_RUINS19,
    eStructs_STRUCT_RUINS20,
    eStructs_STRUCT_RUINS21,
    eStructs_STRUCT_RUINS22,
    eStructs_STRUCT_RUINS23,
    eStructs_STRUCT_RUINS24,
    eStructs_STRUCT_RUINS25,
    eStructs_STRUCT_RUINS26,
    eStructs_STRUCT_RUINS27,
    eStructs_STRUCT_RUINS28,
    eStructs_STRUCT_RUINS29,
    eStructs_STRUCT_RUINS30,
    eStructs_STRUCT_RUINS31,
    eStructs_STRUCT_RUINS32,
    eStructs_STRUCT_RUINS33,
    eStructs_STRUCT_RUINS34,
    eStructs_STRUCT_PEOPLE_BEDOUIN_CAMEL_LANCER,
    eStructs_STRUCT_PEOPLE_BEDOUIN_HEALER,
    eStructs_STRUCT_PEOPLE_BEDOUIN_EUNUCH,
    eStructs_STRUCT_PEOPLE_BEDOUIN_AMBUSHER,
    eStructs_STRUCT_PEOPLE_BEDOUIN_SKIRMISHER,
    eStructs_STRUCT_PEOPLE_BEDOUIN_HEAVY_CAMEL,
    eStructs_STRUCT_PEOPLE_BEDOUIN_SAPPER,
    eStructs_STRUCT_PEOPLE_BEDOUIN_DEMOLISHER
} eStructs;


typedef enum eChimps
{
    eChimps_CHIMP_TYPE_NULL,
    eChimps_CHIMP_TYPE_PEASANT,
    eChimps_CHIMP_TYPE_BURNING_MAN,
    eChimps_CHIMP_TYPE_WOODCUTTER,
    eChimps_CHIMP_TYPE_FLETCHER,
    eChimps_CHIMP_TYPE_TUNNELER,
    eChimps_CHIMP_TYPE_HUNTER,
    eChimps_CHIMP_TYPE_QUARRY_MASON,
    eChimps_CHIMP_TYPE_QUARRY_GRUNT,
    eChimps_CHIMP_TYPE_QUARRY_OX,
    eChimps_CHIMP_TYPE_PITCHMAN,
    eChimps_CHIMP_TYPE_FARMER_WHEAT,
    eChimps_CHIMP_TYPE_FARMER_HOPS,
    eChimps_CHIMP_TYPE_FARMER_APPLE,
    eChimps_CHIMP_TYPE_FARMER_CATTLE,
    eChimps_CHIMP_TYPE_MILLER,
    eChimps_CHIMP_TYPE_BAKER,
    eChimps_CHIMP_TYPE_BREWER,
    eChimps_CHIMP_TYPE_POLETURNER,
    eChimps_CHIMP_TYPE_BLACKSMITH,
    eChimps_CHIMP_TYPE_ARMOURER,
    eChimps_CHIMP_TYPE_TANNER,
    eChimps_CHIMP_TYPE_ARCHER,
    eChimps_CHIMP_TYPE_XBOWMAN,
    eChimps_CHIMP_TYPE_SPEARMAN,
    eChimps_CHIMP_TYPE_PIKEMAN,
    eChimps_CHIMP_TYPE_MACEMAN,
    eChimps_CHIMP_TYPE_SWORDSMAN,
    eChimps_CHIMP_TYPE_KNIGHT,
    eChimps_CHIMP_TYPE_LADDERMAN,
    eChimps_CHIMP_TYPE_ENGINEER,
    eChimps_CHIMP_TYPE_MINER1,
    eChimps_CHIMP_TYPE_MINER2,
    eChimps_CHIMP_TYPE_PRIEST,
    eChimps_CHIMP_TYPE_HEALER,
    eChimps_CHIMP_TYPE_DRUNKARD,
    eChimps_CHIMP_TYPE_INNKEEPER,
    eChimps_CHIMP_TYPE_MONK,
    eChimps_CHIMP_TYPE_ARCHER_debug,
    eChimps_CHIMP_TYPE_CATAPULT,
    eChimps_CHIMP_TYPE_TREBUCHET,
    eChimps_CHIMP_TYPE_MANGONEL,
    eChimps_CHIMP_TYPE_TRADER,
    eChimps_CHIMP_TYPE_TRADER_HORSE,
    eChimps_CHIMP_TYPE_DEER,
    eChimps_CHIMP_TYPE_LION,
    eChimps_CHIMP_TYPE_RABBIT,
    eChimps_CHIMP_TYPE_CAMEL,
    eChimps_CHIMP_TYPE_CROW,
    eChimps_CHIMP_TYPE_SEAGULL,
    eChimps_CHIMP_SIEGE_TENT,
    eChimps_CHIMP_TYPE_COW,
    eChimps_CHIMP_TYPE_DOG,
    eChimps_CHIMP_TYPE_FIREMAN,
    eChimps_CHIMP_TYPE_GHOST,
    eChimps_CHIMP_TYPE_LORD,
    eChimps_CHIMP_TYPE_LADY,
    eChimps_CHIMP_TYPE_JESTER,
    eChimps_CHIMP_TYPE_SIEGE_TOWER,
    eChimps_CHIMP_TYPE_BATTERING_RAM,
    eChimps_CHIMP_TYPE_PORTABLE_SHIELD,
    eChimps_CHIMP_TYPE_BALLISTA,
    eChimps_CHIMP_TYPE_CHICKEN,
    eChimps_CHIMP_TYPE_MOTHER,
    eChimps_CHIMP_TYPE_CHILD,
    eChimps_CHIMP_TYPE_JUGGLER,
    eChimps_CHIMP_TYPE_FIREEATER,
    eChimps_CHIMP_TYPE_WAR_DOG,
    eChimps_CHIMP_TYPE_BURNING_ANIMAL_BIG,
    eChimps_CHIMP_TYPE_BURNING_ANIMAL_SMALL,
    eChimps_CHIMP_TYPE_ARAB_BOW,
    eChimps_CHIMP_TYPE_ARAB_SLAVE,
    eChimps_CHIMP_TYPE_ARAB_SLINGER,
    eChimps_CHIMP_TYPE_ARAB_ASSASIN,
    eChimps_CHIMP_TYPE_ARAB_HORSEMAN,
    eChimps_CHIMP_TYPE_ARAB_SWORDSMAN,
    eChimps_CHIMP_TYPE_ARAB_GRENADIER,
    eChimps_CHIMP_TYPE_ARAB_BALLISTA,
    eChimps_CHIMP_TYPE_BEDOUIN_CAMEL_LANCER,
    eChimps_CHIMP_TYPE_BEDOUIN_HEALER,
    eChimps_CHIMP_TYPE_BEDOUIN_EUNUCH,
    eChimps_CHIMP_TYPE_BEDOUIN_AMBUSHER,
    eChimps_CHIMP_TYPE_BEDOUIN_SKIRMISHER,
    eChimps_CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
    eChimps_CHIMP_TYPE_BEDOUIN_SAPPER,
    eChimps_CHIMP_TYPE_BEDOUIN_DEMOLISHER,
    eChimps_CHIMP_TYPE_GOAT,
    eChimps_CHIMP_TYPE_HYENA,
    eChimps_CHIMP_TYPE_CROCODILE,
    eChimps_CHIMP_NUM_TYPES
} eChimps;


typedef enum eMappers
{
    eMappers_MAPPER_NULL,
    eMappers_MAPPER_AREA,
    eMappers_MAPPER_RAISE,
    eMappers_MAPPER_LOWER,
    eMappers_MAPPER_SEA,
    eMappers_MAPPER_LAND,
    eMappers_MAPPER_FOREST,
    eMappers_MAPPER_SCRUB,
    eMappers_MAPPER_BEACH,
    eMappers_MAPPER_SHALLOWS,
    eMappers_MAPPER_ROCKY,
    eMappers_MAPPER_STONES,
    eMappers_MAPPER_BOULDERS,
    eMappers_MAPPER_PEBBLES,
    eMappers_MAPPER_RIVER,
    eMappers_MAPPER_FORD,
    eMappers_MAPPER_IRON,
    eMappers_MAPPER_MARSH,
    eMappers_MAPPER_DIRT,
    eMappers_MAPPER_GRASS,
    eMappers_MAPPER_BIGROCKS,
    eMappers_MAPPER_MIN,
    eMappers_MAPPER_MAX,
    eMappers_MAPPER_EQUALISE,
    eMappers_MAPPER_PLATEAU,
    eMappers_MAPPER_WALL,
    eMappers_MAPPER_CRENAL,
    eMappers_MAPPER_STAIR,
    eMappers_MAPPER_TOWER,
    eMappers_MAPPER_UP,
    eMappers_MAPPER_DOWN = 20,
    eMappers_MAPPER_EXIT = 31,
    eMappers_MAPPER_TOMAIN,
    eMappers_MAPPER_TOTEST,
    eMappers_MAPPER_PATROL,
    eMappers_MAPPER_CRENAL2,
    eMappers_MAPPER_MOUNTAIN,
    eMappers_MAPPER_HILL,
    eMappers_MAPPER_AFFECT_TYPE,
    eMappers_MAPPER_DELETE,
    eMappers_MAPPER_CHESTNUT,
    eMappers_MAPPER_OAK,
    eMappers_MAPPER_PINE,
    eMappers_MAPPER_BIRCH,
    eMappers_MAPPER_UNDUGMOAT,
    eMappers_MAPPER_DUGMOAT,
    eMappers_MAPPER_WOODWALL,
    eMappers_MAPPER_PLAIN1,
    eMappers_MAPPER_PLAIN2,
    eMappers_MAPPER_OIL,
    eMappers_MAPPER_FLETCHER,
    eMappers_MAPPER_WOODSMAN,
    eMappers_MAPPER_STORES,
    eMappers_MAPPER_OUTPOST_BEDOUIN,
    eMappers_MAPPER_HOVEL,
    eMappers_MAPPER_OXENBASE,
    eMappers_MAPPER_QUARRY,
    eMappers_MAPPER_TUNNEL,
    eMappers_MAPPER_CAMP_FIRE,
    eMappers_MAPPER_SIGNPOST,
    eMappers_MAPPER_KEEP1,
    eMappers_MAPPER_KEEP2,
    eMappers_MAPPER_KEEP3,
    eMappers_MAPPER_KEEP4,
    eMappers_MAPPER_KEEP5,
    eMappers_MAPPER_STABLES,
    eMappers_MAPPER_TUNNEL_CONSTRUCTION,
    eMappers_MAPPER_UNUSED_2 = 68,
    eMappers_MAPPER_UNUSED_3,
    eMappers_MAPPER_WHEATFARM,
    eMappers_MAPPER_HOPSFARM,
    eMappers_MAPPER_APPLEFARM,
    eMappers_MAPPER_CATTLEFARM,
    eMappers_MAPPER_MILL,
    eMappers_MAPPER_BAKER,
    eMappers_MAPPER_BREWER,
    eMappers_MAPPER_TRADEPOST,
    eMappers_MAPPER_HUNTER,
    eMappers_MAPPER_BEDOUIN_STOCKADE,
    eMappers_MAPPER_GRANARY,
    eMappers_MAPPER_ARMOURY,
    eMappers_MAPPER_POLETURNER,
    eMappers_MAPPER_BLACKSMITH,
    eMappers_MAPPER_ARMOURER,
    eMappers_MAPPER_TANNER,
    eMappers_MAPPER_BARRACKS_WOOD,
    eMappers_MAPPER_BARRACKS_STONE,
    eMappers_MAPPER_ENGINEERS_GUILD,
    eMappers_MAPPER_TUNNELERS_GUILD,
    eMappers_MAPPER_IRON_MINE,
    eMappers_MAPPER_PITCH_WORKINGS,
    eMappers_MAPPER_INN,
    eMappers_MAPPER_HEALER,
    eMappers_MAPPER_SIEGE_TOWER_BASE,
    eMappers_MAPPER_CHURCH1,
    eMappers_MAPPER_CHURCH2,
    eMappers_MAPPER_CHURCH3,
    eMappers_MAPPER_KILLING_PIT,
    eMappers_MAPPER_PITCH_DITCH,
    eMappers_MAPPER_GATEHOUSE,
    eMappers_MAPPER_GATE_MAIN,
    eMappers_MAPPER_GATE_INNER,
    eMappers_MAPPER_GATE_WOOD,
    eMappers_MAPPER_GATE_POSTERN,
    eMappers_MAPPER_DRAWBRIDGE,
    eMappers_MAPPER_MOAT,
    eMappers_MAPPER_ANTIMOAT,
    eMappers_MAPPER_GENERIC,
    eMappers_MAPPER_QUARRYPILE,
    eMappers_MAPPER_TOWER1,
    eMappers_MAPPER_TOWER2,
    eMappers_MAPPER_TOWER3,
    eMappers_MAPPER_TOWER4,
    eMappers_MAPPER_TOWER5,
    eMappers_MAPPER_TOWER1_DESTROYED,
    eMappers_MAPPER_TOWER2_DESTROYED,
    eMappers_MAPPER_TOWER3_DESTROYED,
    eMappers_MAPPER_TOWER4_DESTROYED,
    eMappers_MAPPER_TOWER5_DESTROYED,
    eMappers_MAPPER_FLAG_TYPE0,
    eMappers_MAPPER_FLAG_TYPE1,
    eMappers_MAPPER_FLAG_TYPE2,
    eMappers_MAPPER_FLAG_TYPE3,
    eMappers_MAPPER_FLAG_TYPE4,
    eMappers_MAPPER_FLAG_TYPE5,
    eMappers_MAPPER_FLAG_TYPE6,
    eMappers_MAPPER_FLAG_TYPE7,
    eMappers_MAPPER_FLAG_TYPE8,
    eMappers_MAPPER_HEADS,
    eMappers_MAPPER_SHRUB1A,
    eMappers_MAPPER_SHRUB1B,
    eMappers_MAPPER_SHRUB1C,
    eMappers_MAPPER_SHRUB1D,
    eMappers_MAPPER_SHRUB1E,
    eMappers_MAPPER_SHRUB2A,
    eMappers_MAPPER_SHRUB2B,
    eMappers_MAPPER_SHRUB2C,
    eMappers_MAPPER_SHRUB2D,
    eMappers_MAPPER_SHRUB2E,
    eMappers_MAPPER_GATE_WOOD1A,
    eMappers_MAPPER_GATE_WOOD1B,
    eMappers_MAPPER_GATE_WOOD1C,
    eMappers_MAPPER_GATE_WOOD1D,
    eMappers_MAPPER_GATE_STONE1A,
    eMappers_MAPPER_GATE_STONE1B,
    eMappers_MAPPER_GATE_STONE2A,
    eMappers_MAPPER_GATE_STONE2B,
    eMappers_MAPPER_BRAZIER,
    eMappers_MAPPER_UNUSED_7,
    eMappers_MAPPER_FOAM,
    eMappers_MAPPER_RIPPLE,
    eMappers_MAPPER_TO_MAP_EDIT,
    eMappers_MAPPER_SHRUB3A,
    eMappers_MAPPER_SHRUB3B,
    eMappers_MAPPER_SHRUB3C,
    eMappers_MAPPER_SHRUB3D,
    eMappers_MAPPER_UNUSED_12,
    eMappers_MAPPER_UNUSED_13,
    eMappers_MAPPER_UNUSED_14,
    eMappers_MAPPER_GARDEN1,
    eMappers_MAPPER_GARDEN2,
    eMappers_MAPPER_GARDEN3,
    eMappers_MAPPER_GARDEN4,
    eMappers_MAPPER_GARDEN5,
    eMappers_MAPPER_GARDEN6,
    eMappers_MAPPER_GARDEN7,
    eMappers_MAPPER_GARDEN8,
    eMappers_MAPPER_GARDEN9,
    eMappers_MAPPER_GARDEN10,
    eMappers_MAPPER_GARDEN11,
    eMappers_MAPPER_GARDEN12,
    eMappers_MAPPER_UNUSED_15,
    eMappers_MAPPER_UNUSED_16,
    eMappers_MAPPER_UNUSED_17,
    eMappers_MAPPER_MAYPOLE,
    eMappers_MAPPER_GALLOWS,
    eMappers_MAPPER_STOCKS,
    eMappers_MAPPER_OUTPOST,
    eMappers_MAPPER_OUTPOST_ARAB,
    eMappers_MAPPER_OIL_SMELTER,
    eMappers_MAPPER_STAIR1,
    eMappers_MAPPER_STAIR2,
    eMappers_MAPPER_STAIR3,
    eMappers_MAPPER_STAIR4,
    eMappers_MAPPER_STAIR5,
    eMappers_MAPPER_STAIR6,
    eMappers_MAPPER_UNUSED_26,
    eMappers_MAPPER_UNUSED_27,
    eMappers_MAPPER_UNUSED_28,
    eMappers_MAPPER_CATAPULT,
    eMappers_MAPPER_TREBUCHET,
    eMappers_MAPPER_SIEGE_TOWER,
    eMappers_MAPPER_BATTERING_RAM,
    eMappers_MAPPER_PORTABLE_SHIELD,
    eMappers_MAPPER_DOCK,
    eMappers_MAPPER_DOCK2,
    eMappers_MAPPER_DOCK3,
    eMappers_MAPPER_DOCK4,
    eMappers_MAPPER_UNUSED_33,
    eMappers_MAPPER_BACK,
    eMappers_MAPPER_CHECK_BOX,
    eMappers_MAPPER_TEST,
    eMappers_MAPPER_REBUILD,
    eMappers_MAPPER_SNAP_TO,
    eMappers_MAPPER_BIGROCK1,
    eMappers_MAPPER_BIGROCK2,
    eMappers_MAPPER_BIGROCK3,
    eMappers_MAPPER_BIGROCK4,
    eMappers_MAPPER_BIGROCK5,
    eMappers_MAPPER_MANGONEL,
    eMappers_MAPPER_BALLISTA,
    eMappers_MAPPER_UNUSED_34,
    eMappers_MAPPER_UNUSED_35,
    eMappers_MAPPER_UNUSED_36,
    eMappers_MAPPER_UNUSED_37,
    eMappers_MAPPER_UNUSED_38,
    eMappers_MAPPER_UNUSED_39,
    eMappers_MAPPER_UNUSED_40,
    eMappers_MAPPER_UNUSED_41,
    eMappers_MAPPER_DEER,
    eMappers_MAPPER_LION,
    eMappers_MAPPER_RABBIT,
    eMappers_MAPPER_BEAR,
    eMappers_MAPPER_CROW,
    eMappers_MAPPER_SEAGULL,
    eMappers_MAPPER_GOAT,
    eMappers_MAPPER_HYENA,
    eMappers_MAPPER_CONDOR,
    eMappers_MAPPER_CROCODILE,
    eMappers_MAPPER_MAP_SIZE,
    eMappers_MAPPER_SUB_MODE_HEIGHT,
    eMappers_MAPPER_SUB_MODE_TYPE,
    eMappers_MAPPER_SUB_MODE_OBJ = 234,
    eMappers_MAPPER_SUB_MODE_ANIMAL,
    eMappers_MAPPER_SUB_MODE_WATER,
    eMappers_MAPPER_SUB_MODE_FEATURE,
    eMappers_MAPPER_ESTUARY,
    eMappers_MAPPER_SUB_MODE_FEATURE_MP,
    eMappers_MAPPER_REPORT1,
    eMappers_MAPPER_REPORT2,
    eMappers_MAPPER_REPORT3,
    eMappers_MAPPER_REPORT4,
    eMappers_MAPPER_REPORT5,
    eMappers_MAPPER_REPORT6,
    eMappers_MAPPER_REPORT7,
    eMappers_MAPPER_REPORT8,
    eMappers_MAPPER_MP_KEEP1 = 240,
    eMappers_MAPPER_MP_KEEP2,
    eMappers_MAPPER_MP_KEEP3,
    eMappers_MAPPER_MP_KEEP4,
    eMappers_MAPPER_MP_KEEP5,
    eMappers_MAPPER_MP_KEEP6,
    eMappers_MAPPER_MP_KEEP7,
    eMappers_MAPPER_MP_KEEP8,
    eMappers_MAPPER_POND5 = 265,
    eMappers_MAPPER_POND6,
    eMappers_MAPPER_POND7,
    eMappers_MAPPER_POND8,
    eMappers_MAPPER_UNUSED_56,
    eMappers_MAPPER_PEOPLE_ARCHERS,
    eMappers_MAPPER_PEOPLE_SPEARMEN,
    eMappers_MAPPER_PEOPLE_PIKEMEN,
    eMappers_MAPPER_PEOPLE_MACEMEN,
    eMappers_MAPPER_PEOPLE_XBOWMEN,
    eMappers_MAPPER_PEOPLE_SWORDSMEN,
    eMappers_MAPPER_PEOPLE_KNIGHTS,
    eMappers_MAPPER_PEOPLE_LADDERMEN,
    eMappers_MAPPER_PEOPLE_ENGINEERS,
    eMappers_MAPPER_PEOPLE_ENGINEERS_POTS,
    eMappers_MAPPER_PEOPLE_MONKS,
    eMappers_MAPPER_PEOPLE_CATAPULTS,
    eMappers_MAPPER_PEOPLE_TREBUCHETS,
    eMappers_MAPPER_PEOPLE_BATTERING_RAMS,
    eMappers_MAPPER_PEOPLE_SIEGE_TOWERS,
    eMappers_MAPPER_PEOPLE_PORTABLE_SHIELDS,
    eMappers_MAPPER_PEOPLE_TUNNELERS,
    eMappers_MAPPER_STANCE_STAND,
    eMappers_MAPPER_STANCE_DEFENSIVE,
    eMappers_MAPPER_STANCE_AGGRESSIVE,
    eMappers_MAPPER_TROOP_STOP,
    eMappers_MAPPER_ENGINEER_BUILD,
    eMappers_MAPPER_BUILD_BACK,
    eMappers_MAPPER_BUY_AMMO,
    eMappers_MAPPER_UNUSED_57,
    eMappers_MAPPER_UNUSED_58,
    eMappers_MAPPER_UNUSED_59,
    eMappers_MAPPER_UNUSED_60,
    eMappers_MAPPER_UNUSED_61,
    eMappers_MAPPER_UNUSED_62,
    eMappers_MAPPER_UNUSED_63,
    eMappers_MAPPER_CESS_PIT1,
    eMappers_MAPPER_CESS_PIT2,
    eMappers_MAPPER_CESS_PIT3,
    eMappers_MAPPER_CESS_PIT4,
    eMappers_MAPPER_BURNING_STAKE,
    eMappers_MAPPER_GIBBET,
    eMappers_MAPPER_DUNGEON,
    eMappers_MAPPER_RACK_STRETCHING,
    eMappers_MAPPER_RACK_FLOGGING,
    eMappers_MAPPER_CHOPPING_BLOCK,
    eMappers_MAPPER_DUNKING_STOOL,
    eMappers_MAPPER_DOG_CAGE,
    eMappers_MAPPER_STATUE1,
    eMappers_MAPPER_STATUE2,
    eMappers_MAPPER_STATUE3,
    eMappers_MAPPER_STATUE4,
    eMappers_MAPPER_STATUE5,
    eMappers_MAPPER_SHRINE1,
    eMappers_MAPPER_SHRINE2,
    eMappers_MAPPER_SHRINE3,
    eMappers_MAPPER_SHRINE4,
    eMappers_MAPPER_SHRINE5,
    eMappers_MAPPER_BEE_HIVE,
    eMappers_MAPPER_DANCING_BEAR,
    eMappers_MAPPER_POND1,
    eMappers_MAPPER_POND2,
    eMappers_MAPPER_POND3,
    eMappers_MAPPER_POND4,
    eMappers_MAPPER_BEAR_CAVE,
    eMappers_MAPPER_WELL,
    eMappers_MAPPER_AREA_BACK,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINT1,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINT2,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINT3,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINT4,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINT5,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINT6,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINT7,
    eMappers_MAPPER_SUB_MENU_TOWERS = 377,
    eMappers_MAPPER_SUB_MENU_MILITARY,
    eMappers_MAPPER_SUB_MENU_GATEHOUSES,
    eMappers_MAPPER_SUB_MENU_KEEPS = 343,
    eMappers_MAPPER_SUB_MENU_GATEHOUSES_WOOD,
    eMappers_MAPPER_SUB_MENU_GATEHOUSES_STONESMALL,
    eMappers_MAPPER_SUB_MENU_GATEHOUSES_STONELARGE,
    eMappers_MAPPER_SUB_MENU_GOOD,
    eMappers_MAPPER_SUB_MENU_BAD,
    eMappers_MAPPER_DELETE_EDITOR,
    eMappers_MAPPER_DUNES = 340,
    eMappers_MAPPER_SCRUBGRASS,
    eMappers_MAPPER_WATERPOT,
    eMappers_MAPPER_PEOPLE_ARAB_BOW = 350,
    eMappers_MAPPER_PEOPLE_ARAB_SLAVE,
    eMappers_MAPPER_PEOPLE_ARAB_SLINGER,
    eMappers_MAPPER_PEOPLE_ARAB_ASSASIN,
    eMappers_MAPPER_PEOPLE_ARAB_HORSEMAN,
    eMappers_MAPPER_PEOPLE_ARAB_SWORDSMAN,
    eMappers_MAPPER_PEOPLE_ARAB_GRENADIER,
    eMappers_MAPPER_PEOPLE_ARAB_BALLISTA,
    eMappers_MAPPER_ARAB_BALLISTA,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTM1 = 360,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTM2,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTM3,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTM4,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTM5,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTM6,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTM7,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTE1,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTE2,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTT1,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTK1,
    eMappers_MAPPER_MARKER_POINT1 = 380,
    eMappers_MAPPER_MARKER_POINT2,
    eMappers_MAPPER_MARKER_POINT3,
    eMappers_MAPPER_MARKER_POINT4,
    eMappers_MAPPER_MARKER_POINT5,
    eMappers_MAPPER_MARKER_POINT6,
    eMappers_MAPPER_MARKER_POINT7,
    eMappers_MAPPER_MARKER_POINT8,
    eMappers_MAPPER_MARKER_POINT9,
    eMappers_MAPPER_MARKER_POINT10,
    eMappers_MAPPER_MENU_RETURN_TOWERS = 371,
    eMappers_MAPPER_MENU_RETURN_GATEHOUSES,
    eMappers_MAPPER_MENU_RETURN_MILITARY,
    eMappers_MAPPER_MENU_RETURN_KEEPS,
    eMappers_MAPPER_MENU_RETURN_GOOD,
    eMappers_MAPPER_MENU_RETURN_BAD,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTBS1 = 391,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTBS2,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTBS3,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTBS4,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTBS5,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTBS6,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTBS7,
    eMappers_MAPPER_PLACE_ASSEMBLY_POINTBS8,
    eMappers_MAPPER_PEOPLE_BEDOUIN_CAMEL_LANCER = 400,
    eMappers_MAPPER_PEOPLE_BEDOUIN_HEALER,
    eMappers_MAPPER_PEOPLE_BEDOUIN_EUNUCH,
    eMappers_MAPPER_PEOPLE_BEDOUIN_AMBUSHER,
    eMappers_MAPPER_PEOPLE_BEDOUIN_SKIRMISHER,
    eMappers_MAPPER_PEOPLE_BEDOUIN_HEAVY_CAMEL,
    eMappers_MAPPER_PEOPLE_BEDOUIN_SAPPER,
    eMappers_MAPPER_PEOPLE_BEDOUIN_DEMOLISHER,
    eMappers_MAPPER_RUINS1 = 410,
    eMappers_MAPPER_RUINS2,
    eMappers_MAPPER_RUINS3,
    eMappers_MAPPER_RUINS4,
    eMappers_MAPPER_RUINS5,
    eMappers_MAPPER_RUINS6,
    eMappers_MAPPER_RUINS7,
    eMappers_MAPPER_RUINS8,
    eMappers_MAPPER_RUINS9,
    eMappers_MAPPER_RUINS10,
    eMappers_MAPPER_RUINS11,
    eMappers_MAPPER_RUINS12,
    eMappers_MAPPER_RUINS13,
    eMappers_MAPPER_RUINS14,
    eMappers_MAPPER_RUINS15,
    eMappers_MAPPER_RUINS16,
    eMappers_MAPPER_RUINS17,
    eMappers_MAPPER_RUINS18,
    eMappers_MAPPER_RUINS19,
    eMappers_MAPPER_RUINS20,
    eMappers_MAPPER_RUINS21,
    eMappers_MAPPER_RUINS22,
    eMappers_MAPPER_RUINS23,
    eMappers_MAPPER_RUINS24,
    eMappers_MAPPER_RUINS25,
    eMappers_MAPPER_RUINS26,
    eMappers_MAPPER_RUINS27,
    eMappers_MAPPER_RUINS28,
    eMappers_MAPPER_RUINS29,
    eMappers_MAPPER_RUINS30,
    eMappers_MAPPER_RUINS31,
    eMappers_MAPPER_RUINS32,
    eMappers_MAPPER_RUINS33,
    eMappers_MAPPER_RUINS34,
    eMappers_MAPPER_POND9_RAVINE1A,
    eMappers_MAPPER_POND10_RAVINE1B,
    eMappers_MAPPER_POND11_RAVINE1C,
    eMappers_MAPPER_POND12_RAVINE1AR,
    eMappers_MAPPER_POND13_RAVINE1BR,
    eMappers_MAPPER_POND14_RAVINE1CR,
    eMappers_MAPPER_POND15_RAVINE2A,
    eMappers_MAPPER_POND16_RAVINE2B,
    eMappers_MAPPER_POND17_RAVINE2C,
    eMappers_MAPPER_POND18_RAVINE2AR,
    eMappers_MAPPER_POND19_RAVINE2BR,
    eMappers_MAPPER_POND20_RAVINE2CR,
    eMappers_END_OF_MAPPERS = 460
} eMappers;


typedef enum eIcons
{
    eIcons_ICON_NULL,
    eIcons_ICON_BLD_HOUSE,
    eIcons_ICON_BLD_WOODCUTTER,
    eIcons_ICON_BLD_STOREHOUSE,
    eIcons_ICON_BLD_GRANARY,
    eIcons_ICON_BLD_ARMOURY,
    eIcons_ICON_BLD_BARRACKS,
    eIcons_ICON_BLD_BARRACKS_WOOD,
    eIcons_ICON_BLD_BARRACKS_STONE,
    eIcons_ICON_BLD_WORKSHOP,
    eIcons_ICON_BLD_FLETCHER,
    eIcons_ICON_BLD_POLETURNER,
    eIcons_ICON_BLD_BLACKSMITH,
    eIcons_ICON_BLD_TANNER,
    eIcons_ICON_BLD_ARMOURER,
    eIcons_ICON_BLD_BAKER,
    eIcons_ICON_BLD_BREWER,
    eIcons_ICON_BLD_FARM,
    eIcons_ICON_BLD_FARM_WHEAT,
    eIcons_ICON_BLD_FARM_APPLES,
    eIcons_ICON_BLD_FARM_HOPS,
    eIcons_ICON_BLD_FARM_COWS,
    eIcons_ICON_BLD_FARM_HUNTER,
    eIcons_ICON_BLD_MILL,
    eIcons_ICON_BLD_QUARRY,
    eIcons_ICON_BLD_OX_TETHER,
    eIcons_ICON_BLD_IRON_MINE,
    eIcons_ICON_BLD_HOVEL,
    eIcons_ICON_BLD_PITCH_DUGOUT,
    eIcons_ICON_BLD_WOOD_GATEHOUSE_WEST,
    eIcons_ICON_BLD_TRADEPOST,
    eIcons_ICON_BLD_STABLES,
    eIcons_ICON_BLD_CHURCH,
    eIcons_ICON_BLD_INN,
    eIcons_ICON_BLD_HEALER,
    eIcons_ICON_BLD_ENGINEERS,
    eIcons_ICON_BLD_TUNNELER,
    eIcons_ICON_BLD_WOOD_GATEHOUSE_SOUTH,
    eIcons_ICON_BLD_WOOD_GATEHOUSE_EAST,
    eIcons_ICON_BLD_WOOD_GATEHOUSE_NORTH,
    eIcons_ICON_BLD_GALLOWS,
    eIcons_ICON_BLD_MAYPOLE,
    eIcons_ICON_BLD_FAIR,
    eIcons_ICON_BLD_JOUSTING,
    eIcons_ICON_BLD_STOCKS,
    eIcons_ICON_BLD_DWELLING,
    eIcons_ICON_BLD_SMELTER,
    eIcons_ICON_BLD_MANGONEL,
    eIcons_ICON_BLD_BALLISTA,
    eIcons_ICON_REPORTS,
    eIcons_ICON_BLD_WALL_SINGLE,
    eIcons_ICON_BLD_WALL_STAIRS,
    eIcons_ICON_BLD_WALL_CRENAL,
    eIcons_ICON_BLD_WALL_WOODEN,
    eIcons_ICON_BLD_PITCH_DITCH,
    eIcons_ICON_BLD_BRAZIER,
    eIcons_ICON_BLD_KILLING_PITS,
    eIcons_ICON_BLD_SIEGE_TENT_1,
    eIcons_ICON_BLD_SIEGE_TENT_2,
    eIcons_ICON_BLD_SIEGE_TENT_3,
    eIcons_ICON_TOWERS,
    eIcons_ICON_BLD_TOWER_A,
    eIcons_ICON_BLD_TOWER_B,
    eIcons_ICON_BLD_TOWER_C,
    eIcons_ICON_BLD_TOWER_D,
    eIcons_ICON_BLD_TOWER_E,
    eIcons_ICON_MOATS,
    eIcons_ICON_DECORATIONS,
    eIcons_ICON_MILITARY_BUILDINGS,
    eIcons_ICON_GARDENS,
    eIcons_ICON_GATES,
    eIcons_ICON_BLD_GATE_SMALL_WOODEN,
    eIcons_ICON_BLD_GATE_LARGE_WOODEN,
    eIcons_ICON_BLD_GATE_SMALL_STONE,
    eIcons_ICON_BLD_DRAWBRIDGE,
    eIcons_ICON_BLD_MOAT,
    eIcons_ICON_BLD_GATE_LARGE_STONE,
    eIcons_ICON_BLD_ANTIMOAT,
    eIcons_ICON_PUNISHMENTS,
    eIcons_ICON_KEEPS = 80,
    eIcons_ICON_BLD_KEEP_A,
    eIcons_ICON_BLD_KEEP_B,
    eIcons_ICON_BLD_KEEP_C,
    eIcons_ICON_BLD_OUTPOST_ARAB,
    eIcons_ICON_BLD_OUTPOST,
    eIcons_ICON_BLD_HEADSONSPIKES,
    eIcons_ICON_BLD_CHURCH_1,
    eIcons_ICON_BLD_CHURCH_2,
    eIcons_ICON_BLD_CHURCH_3,
    eIcons_ICON_TROOPS,
    eIcons_ICON_TROOP_SPEAR,
    eIcons_ICON_TROOP_PIKE,
    eIcons_ICON_TROOP_BOW,
    eIcons_ICON_TROOP_XBOW,
    eIcons_ICON_TROOP_MACE,
    eIcons_ICON_TROOP_SWORD,
    eIcons_ICON_TROOP_KNIGHT,
    eIcons_ICON_WEAPONS = 100,
    eIcons_ICON_WEAPON_SPEAR,
    eIcons_ICON_WEAPON_PIKE,
    eIcons_ICON_WEAPON_BOW,
    eIcons_ICON_WEAPON_XBOW,
    eIcons_ICON_WEAPON_MACE,
    eIcons_ICON_WEAPON_SWORD,
    eIcons_ICON_WEAPON_L_ARMOUR,
    eIcons_ICON_WEAPON_M_ARMOUR,
    eIcons_ICON_WEAPON_HORSE,
    eIcons_ICON_BACK,
    eIcons_ICON_TICK,
    eIcons_ICON_CROSS,
    eIcons_ICON_CHECKBOX,
    eIcons_ICON_ARMY,
    eIcons_ICON_ARMY2,
    eIcons_ICON_BRIEFING_TUTOR_BACK,
    eIcons_ICON_TUNGUILD_TUNNELER,
    eIcons_ICON_ENGGUILD_ENGINEER,
    eIcons_ICON_ENGGUILD_LADDERMAN,
    eIcons_ICON_PRODUCING_SWORD,
    eIcons_ICON_PRODUCING_MACE,
    eIcons_ICON_PRODUCING_BOW,
    eIcons_ICON_PRODUCING_CROSSBOW,
    eIcons_ICON_PRODUCING_SPEAR,
    eIcons_ICON_PRODUCING_PIKE,
    eIcons_ICON_DRAWBRIDGE_UP,
    eIcons_ICON_DRAWBRIDGE_DOWN,
    eIcons_ICON_GATE_CLOSE,
    eIcons_ICON_GATE_OPEN,
    eIcons_ICON_TRADEPOST_PRICES,
    eIcons_ICON_TRADEPOST_FOOD,
    eIcons_ICON_TRADEPOST_BULK,
    eIcons_ICON_TRADEPOST_ARMS,
    eIcons_ICON_TRADE_WOOD_PLANKS,
    eIcons_ICON_TRADE_RAW_HOPS,
    eIcons_ICON_TRADE_STONE_BLOCKS,
    eIcons_ICON_TRADE_IRON_INGOTS,
    eIcons_ICON_TRADE_PITCH_REFINED,
    eIcons_ICON_TRADE_RAW_WHEAT,
    eIcons_ICON_TRADE_FOOD_BREAD,
    eIcons_ICON_TRADE_FOOD_CHEESE,
    eIcons_ICON_TRADE_FOOD_MEAT,
    eIcons_ICON_TRADE_FOOD_FRUIT,
    eIcons_ICON_TRADE_FOOD_ALE,
    eIcons_ICON_TRADE_BOWS,
    eIcons_ICON_TRADE_CROSSBOWS,
    eIcons_ICON_TRADE_SPEARS,
    eIcons_ICON_TRADE_PIKES,
    eIcons_ICON_TRADE_MACES,
    eIcons_ICON_TRADE_SWORDS,
    eIcons_ICON_TRADE_LEATHER_ARMOUR,
    eIcons_ICON_TRADE_METAL_ARMOUR,
    eIcons_ICON_TRADE_FLOUR,
    eIcons_ICON_SUB_PEOPLE = 158,
    eIcons_ICON_SUB_RUINS,
    eIcons_ICON_SUB_INDUSTRY,
    eIcons_ICON_SUB_CASTLE,
    eIcons_ICON_SUB_FARMS,
    eIcons_ICON_SUB_TOWN,
    eIcons_ICON_OPTIONS,
    eIcons_ICON_SUB_WEAPONS,
    eIcons_ICON_UNDO,
    eIcons_ICON_DELETE,
    eIcons_ICON_HELP,
    eIcons_ICON_SUB_FOODPROCESS,
    eIcons_ICON_BLD_FLAG_1,
    eIcons_ICON_BLD_FLAG_2,
    eIcons_ICON_BLD_FLAG_3,
    eIcons_ICON_BLD_CREST,
    eIcons_ICON_BLD_CREST_2,
    eIcons_ICON_BLD_BANNER,
    eIcons_ICON_BLD_GARDEN_1,
    eIcons_ICON_BLD_GARDEN_2,
    eIcons_ICON_BLD_GARDEN_3,
    eIcons_ICON_BLD_GARDEN_4,
    eIcons_ICON_BLD_GARDEN_5,
    eIcons_ICON_BLD_FINGERPRESS,
    eIcons_ICON_BLD_THUMBSCREW,
    eIcons_ICON_BLD_DUNKINGSTOOL,
    eIcons_ICON_BLD_STAKE,
    eIcons_ICON_BLD_FLOGGINGHORSE,
    eIcons_ICON_BLD_GARDEN_6,
    eIcons_ICON_BLD_GARDEN_7,
    eIcons_ICON_BLD_GARDEN_8,
    eIcons_ICON_BLD_GARDEN_9,
    eIcons_ICON_BLD_GARDEN_10,
    eIcons_ICON_BLD_GARDEN_11,
    eIcons_ICON_BLD_GARDEN_12,
    eIcons_ICON_HELP_BARRACKS = 196,
    eIcons_ICON_BRIEFING,
    eIcons_ICON_RATIONS_EXTRA,
    eIcons_ICON_BLD_FLAG_4,
    eIcons_ICON_RATIONS_NONE,
    eIcons_ICON_RATIONS_HALF,
    eIcons_ICON_RATIONS_FULL,
    eIcons_ICON_RATIONS_DOUBLE,
    eIcons_ICON_SUB_REPORT,
    eIcons_ICON_SLEEP,
    eIcons_ICON_VSMALL_CHECK_ON,
    eIcons_ICON_VSMALL_CHECK_OFF,
    eIcons_ICON_SMALL_CHECK_ON,
    eIcons_ICON_SMALL_CHECK_OFF,
    eIcons_ICON_POUR_OIL,
    eIcons_ICON_CATAPULT,
    eIcons_ICON_TREBUCHET,
    eIcons_ICON_SIEGE_TOWER,
    eIcons_ICON_BATTERING_RAM,
    eIcons_ICON_PORTABLE_SHIELD,
    eIcons_ICON_PATROL,
    eIcons_ICON_DISBAND,
    eIcons_ICON_TUNNELHERE,
    eIcons_ICON_ATTACKHERE,
    eIcons_ICON_LAUNCH_COW,
    eIcons_ICON_AM_HEIGHT = 240,
    eIcons_ICON_AM_LANDTYPE,
    eIcons_ICON_AM_OBJ,
    eIcons_ICON_AM_ANIMAL,
    eIcons_ICON_AM_WATER,
    eIcons_ICON_AM_FEATURE,
    eIcons_ICON_AM_GAME,
    eIcons_ICON_AM_BRUSH,
    eIcons_ICON_AM_SNAP,
    eIcons_ICON_AM_DELETE,
    eIcons_ICON_AM_RAISE,
    eIcons_ICON_AM_LOWER,
    eIcons_ICON_AM_MIN,
    eIcons_ICON_AM_MAX,
    eIcons_ICON_AM_EQUALIZE,
    eIcons_ICON_AM_MOUNTAIN,
    eIcons_ICON_AM_HILL,
    eIcons_ICON_AM_MID_PLAIN,
    eIcons_ICON_AM_HI_PLAIN,
    eIcons_ICON_AM_LAND,
    eIcons_ICON_AM_GRASS,
    eIcons_ICON_AM_ROCKS,
    eIcons_ICON_AM_PEBBLES,
    eIcons_ICON_AM_BOULDERS,
    eIcons_ICON_AM_IRON,
    eIcons_ICON_AM_DIRT,
    eIcons_ICON_AM_STONES,
    eIcons_ICON_AM_CHESTNUT,
    eIcons_ICON_AM_OAK,
    eIcons_ICON_AM_PINE,
    eIcons_ICON_AM_BIRCH,
    eIcons_ICON_AM_SHRUB1,
    eIcons_ICON_AM_SHRUB2,
    eIcons_ICON_AM_DEER,
    eIcons_ICON_AM_WOLF,
    eIcons_ICON_AM_RABBIT,
    eIcons_ICON_AM_BEAR,
    eIcons_ICON_AM_SEAGULL,
    eIcons_ICON_AM_CROW,
    eIcons_ICON_AM_SEA,
    eIcons_ICON_AM_SHALLOW,
    eIcons_ICON_AM_BEACH,
    eIcons_ICON_AM_MARSH,
    eIcons_ICON_AM_OIL,
    eIcons_ICON_AM_RIVER,
    eIcons_ICON_AM_FORD,
    eIcons_ICON_AM_FOAM,
    eIcons_ICON_AM_RIPPLE,
    eIcons_ICON_AM_BIGROCK1,
    eIcons_ICON_AM_BIGROCK2,
    eIcons_ICON_AM_BIGROCK3,
    eIcons_ICON_AM_BIGROCK4,
    eIcons_ICON_AM_BIGROCK5,
    eIcons_ICON_AM_SIGNPOST,
    eIcons_ICON_AM_ESTUARY,
    eIcons_ICON_AM_SHRUB1A,
    eIcons_ICON_AM_SHRUB1B,
    eIcons_ICON_AM_SHRUB1C,
    eIcons_ICON_AM_SHRUB1D,
    eIcons_ICON_AM_SHRUB1E,
    eIcons_ICON_SMALL_ARCHERS,
    eIcons_ICON_SMALL_XBOWMEN,
    eIcons_ICON_SMALL_SPEARMEN,
    eIcons_ICON_SMALL_PIKEMEN,
    eIcons_ICON_SMALL_MACEMEN,
    eIcons_ICON_SMALL_SWORDSMEN,
    eIcons_ICON_SMALL_KNIGHTS,
    eIcons_ICON_SMALL_LADDERMEN,
    eIcons_ICON_SMALL_ENGINEERS,
    eIcons_ICON_SMALL_MONKS,
    eIcons_ICON_SMALL_CATAPULT,
    eIcons_ICON_SMALL_TREBUCHET,
    eIcons_ICON_SMALL_SIEGE_TOWER,
    eIcons_ICON_SMALL_BATTERING_RAM,
    eIcons_ICON_SMALL_PORTABLE_SHIELD,
    eIcons_ICON_SMALL_TUNNELERS,
    eIcons_ICON_TRADER_RESOURCES = 317,
    eIcons_ICON_TRADER_FOOD,
    eIcons_ICON_TRADER_WEAPONS,
    eIcons_ICON_AM_KEEP1,
    eIcons_ICON_AM_KEEP2,
    eIcons_ICON_AM_KEEP3,
    eIcons_ICON_AM_KEEP4,
    eIcons_ICON_AM_KEEP5,
    eIcons_ICON_AM_KEEP6,
    eIcons_ICON_AM_KEEP7,
    eIcons_ICON_AM_KEEP8,
    eIcons_ICON_STORY_NEXT = 330,
    eIcons_ICON_STORY_BUTTON,
    eIcons_ICON_REPORT,
    eIcons_ICON_BUYSELL_BUTTON,
    eIcons_ICON_STORY_PREV,
    eIcons_ICON_STORY_BRIEF,
    eIcons_ICON_STORY_HINTS,
    eIcons_ICON_STORY_TUTORIAL,
    eIcons_ICON_KEEPS_ARROWS_LEFT = 340,
    eIcons_ICON_KEEPS_ARROWS_RIGHT,
    eIcons_ICON_BUILDER_BACK,
    eIcons_ICON_BUILDER_SECTION_CAS_SEL = 345,
    eIcons_ICON_BUILDER_SECTION_LAN_SEL,
    eIcons_ICON_BUILDER_SECTION_CAS_NORM,
    eIcons_ICON_BUILDER_SECTION_LAN_NORM,
    eIcons_ICON_FRONTEND_BUILDER_BACK,
    eIcons_ICON_FRONTEND_SHIELD1,
    eIcons_ICON_FRONTEND_SHIELD2,
    eIcons_ICON_FRONTEND_SHIELD3,
    eIcons_ICON_FRONTEND_SHIELD4,
    eIcons_ICON_FRONTEND_BACK,
    eIcons_ICON_FRONTEND_COMBAT_SHIELD1,
    eIcons_ICON_FRONTEND_COMBAT_SHIELD2,
    eIcons_ICON_FRONTEND_COMBAT_SHIELD3,
    eIcons_ICON_FRONTEND_COMBAT_SHIELD4,
    eIcons_ICON_FRONTEND_SHIELD5,
    eIcons_ICON_FRONTEND_ECONOMICS_SHIELD1,
    eIcons_ICON_FRONTEND_ECONOMICS_SHIELD2,
    eIcons_ICON_FRONTEND_ECONOMICS_SHIELD3,
    eIcons_ICON_FRONTEND_ECONOMICS_SHIELD4,
    eIcons_ICON_FRONTEND_BUILDER_SHIELD1,
    eIcons_ICON_FRONTEND_BUILDER_SHIELD2,
    eIcons_ICON_FRONTEND_BUILDER_SHIELD3,
    eIcons_ICON_FRONTEND_BUILDER_SHIELD4,
    eIcons_ICON_FRONTEND_COMBAT_BACK,
    eIcons_ICON_FRONTEND_ECONOMICS_BACK,
    eIcons_ICON_STANCE_STAND,
    eIcons_ICON_STANCE_DEFENSIVE,
    eIcons_ICON_STANCE_AGGRESSIVE,
    eIcons_ICON_TROOP_STOP,
    eIcons_ICON_TROOP_BUILD,
    eIcons_ICON_TROOP_FIRE_HERE,
    eIcons_ICON_TROOP_BACK,
    eIcons_ICON_TROOP_AMMO,
    eIcons_ICON_FRONTEND_COMBAT_FORWARD,
    eIcons_ICON_FRONTEND_ECONOMICS_FORWARD,
    eIcons_ICON_SUB_KEEPS_RTN,
    eIcons_ICON_SUB_TOWERS_RTN,
    eIcons_ICON_SUB_GATES_RTN,
    eIcons_ICON_SUB_MILITARY_BUILDINGS_RTN,
    eIcons_ICON_SUB_BADSTUFF_RTN,
    eIcons_ICON_SUB_GOODSTUFF_RTN,
    eIcons_ICON_SUB_SUB_WOODENGATES_RTN,
    eIcons_ICON_SUB_SUB_SMALLGATES_RTN,
    eIcons_ICON_SUB_SUB_LARGEGATES_RTN,
    eIcons_ICON_BLD_SMALL_GATEHOUSE_NS,
    eIcons_ICON_BLD_SMALL_GATEHOUSE_EW,
    eIcons_ICON_BLD_LARGE_GATEHOUSE_NS,
    eIcons_ICON_BLD_LARGE_GATEHOUSE_EW,
    eIcons_ICON_BLDMODE_TERRAIN,
    eIcons_ICON_BLDMODE_BUILDINGS,
    eIcons_ICON_BLD_CESS_PIT = 400,
    eIcons_ICON_BLD_BURNING_STAKE,
    eIcons_ICON_BLD_GIBBET,
    eIcons_ICON_BLD_DUNGEON,
    eIcons_ICON_BLD_RACK_STRETCHING,
    eIcons_ICON_BLD_RACK_FLOGGING,
    eIcons_ICON_BLD_CHOPPING_BLOCK,
    eIcons_ICON_BLD_DUNKING_STOOL,
    eIcons_ICON_BLD_DOG_CAGE,
    eIcons_ICON_BLD_STATUE,
    eIcons_ICON_BLD_SHRINE,
    eIcons_ICON_BLD_BEE_HIVE,
    eIcons_ICON_BLD_DANCING_BEAR,
    eIcons_ICON_BLD_POND,
    eIcons_ICON_BEAR_CAVE,
    eIcons_ICON_BLD_WELL,
    eIcons_ICON_BLD_POND_LARGE,
    eIcons_ICON_BLD_CROSS,
    eIcons_ICON_CREDITS = 420,
    eIcons_ICON_TRADE_BULK_OFF,
    eIcons_ICON_TRADE_BULK_ON,
    eIcons_ICON_TRADE_FOOD_OFF,
    eIcons_ICON_TRADE_FOOD_ON,
    eIcons_ICON_TRADE_WEAP_OFF,
    eIcons_ICON_TRADE_WEAP_ON,
    eIcons_ICON_MP_LOAD,
    eIcons_ICON_MP_READY,
    eIcons_ICON_MP_UNREADY,
    eIcons_ICON_GAMESPY,
    eIcons_ICON_CROWN,
    eIcons_ICON_MP_ENEMY,
    eIcons_ICON_MP_YOUR,
    eIcons_ICON_MP_GOLD,
    eIcons_ICON_MP_POP,
    eIcons_ICON_MP_FEAR,
    eIcons_ICON_MP_BUILDING_DESTROYED,
    eIcons_ICON_MP_FOOD,
    eIcons_ICON_MP_WOOD,
    eIcons_ICON_EDIT_SHIELDS_1,
    eIcons_ICON_EDIT_SHIELDS_2,
    eIcons_ICON_EDIT_SHIELDS_3,
    eIcons_ICON_EDIT_SHIELDS_4,
    eIcons_ICON_EDIT_SHIELDS_5,
    eIcons_ICON_EDIT_SHIELDS_6,
    eIcons_ICON_EDIT_SHIELDS_7,
    eIcons_ICON_EDIT_SHIELDS_8,
    eIcons_ICON_MP_STONE = 450,
    eIcons_ICON_MP_IRON,
    eIcons_ICON_MP_PITCH,
    eIcons_ICON_MP_STAR,
    eIcons_ICON_MP_CROWN,
    eIcons_ICON_MP_FEARPIE,
    eIcons_ICON_WP_XBOW = 457,
    eIcons_ICON_WP_PIKE,
    eIcons_ICON_WP_SWORD,
    eIcons_ICON_ARMY_ARCHERS,
    eIcons_ICON_ARMY_XBOWMEN,
    eIcons_ICON_ARMY_SPEARMEN,
    eIcons_ICON_ARMY_PIKEMEN,
    eIcons_ICON_ARMY_MACEMEN,
    eIcons_ICON_ARMY_SWORDSMEN,
    eIcons_ICON_ARMY_KNIGHTS,
    eIcons_ICON_IF_ARCHER = 480,
    eIcons_ICON_IF_SPEARMAN,
    eIcons_ICON_IF_PIKEMAN,
    eIcons_ICON_IF_MACEMAN,
    eIcons_ICON_IF_CROSSBOWMAN,
    eIcons_ICON_IF_SWORDSMAN,
    eIcons_ICON_IF_KNIGHT,
    eIcons_ICON_IF_LADDERMAN,
    eIcons_ICON_IF_ENGINEER,
    eIcons_ICON_IF_MONK,
    eIcons_ICON_IF_CATAPULT,
    eIcons_ICON_IF_TREBUCHET,
    eIcons_ICON_IF_BATTERINGRAM,
    eIcons_ICON_IF_SIEGETOWER,
    eIcons_ICON_IF_PORTABLESHIELD,
    eIcons_ICON_IF_TUNNELER,
    eIcons_ICON_IF_ENGINEER_POT,
    eIcons_ICON_IF_RUINS1 = 500,
    eIcons_ICON_IF_RUINS2,
    eIcons_ICON_IF_RUINS3,
    eIcons_ICON_IF_RUINS4,
    eIcons_ICON_IF_RUINS5,
    eIcons_ICON_IF_RUINS6,
    eIcons_ICON_IF_RUINS7,
    eIcons_ICON_IF_RUINS8,
    eIcons_ICON_IF_RUINS9,
    eIcons_ICON_IF_RUINS10,
    eIcons_ICON_IF_RUINS11,
    eIcons_ICON_IF_RUINS12,
    eIcons_ICON_IF_RUINS13,
    eIcons_ICON_MINI_LEFT_HAND = 520,
    eIcons_ICON_MINI_RIGHT_HAND,
    eIcons_ICON_ACTION_POINT,
    eIcons_ICON_MOUSE_CURSOR1 = 530,
    eIcons_ICON_MOUSE_CURSOR2,
    eIcons_ICON_MOUSE_CURSOR3,
    eIcons_ICON_MOUSE_CURSOR4,
    eIcons_ICON_IF_ARAB_BOW,
    eIcons_ICON_IF_ARAB_SLAVE,
    eIcons_ICON_IF_ARAB_SLINGER,
    eIcons_ICON_IF_ARAB_ASSASSIN,
    eIcons_ICON_IF_ARAB_HORSEARCHER,
    eIcons_ICON_IF_ARAB_SWORDSMAN,
    eIcons_ICON_IF_ARAB_GRENDADIER,
    eIcons_ICON_IF_ARAB_BALLISTA,
    eIcons_ICON_BLD_WATERPOT,
    eIcons_ICON_BLD_BEDOUIN_STOCKADE,
    eIcons_ICON_IF_CAMEL_LANCER,
    eIcons_ICON_IF_HEALER,
    eIcons_ICON_IF_EUNUCH,
    eIcons_ICON_IF_AMBUSHER,
    eIcons_ICON_IF_SKIRMISHER,
    eIcons_ICON_IF_HEAVY_CAMEL,
    eIcons_ICON_IF_SAPPER,
    eIcons_ICON_IF_DEMOLISHER,
    eIcons_ICON_IF_RUINS14,
    eIcons_ICON_IF_RUINS15,
    eIcons_ICON_IF_RUINS16,
    eIcons_ICON_IF_RUINSToggle1,
    eIcons_ICON_IF_RUINSToggle2,
    eIcons_ICON_IF_RUINS17,
    eIcons_ICON_IF_DOCK,
    eIcons_ICON_IF_RUINS18,
    eIcons_ICON_IF_RUINS19,
    eIcons_ICON_IF_RUINS20,
    eIcons_ICON_IF_RUINS21,
    eIcons_ICON_IF_RUINS22,
    eIcons_ICON_IF_RUINS23,
    eIcons_ICON_IF_RUINS24,
    eIcons_ICON_IF_RUINS25,
    eIcons_ICON_IF_RUINS26,
    eIcons_ICON_IF_RUINS27,
    eIcons_ICON_IF_RUINS28,
    eIcons_ICON_IF_RUINSToggle3,
    eIcons_ICON_IF_RUINS29,
    eIcons_ICON_IF_RUINS30,
    eIcons_ICON_IF_RUINS31,
    eIcons_ICON_IF_RUINS32,
    eIcons_ICON_IF_RUINS33,
    eIcons_ICON_IF_RUINS34,
    eIcons_ICON_IF_RUINSToggle4,
    eIcons_ICON_IF_RUINSToggle1b,
    eIcons_ICON_IF_RUINSToggle2b,
    eIcons_ICON_IF_RUINSToggle3b,
    eIcons_ICON_IF_RUINSToggle4b,
    eIcons_ICON_BLD_OUTPOST_BEDOUIN,
    eIcons_ICON_BLD_CHURCH_1M,
    eIcons_ICON_BLD_CHURCH_2M,
    eIcons_ICON_BLD_CHURCH_3M,
    eIcons_ICON_BLD_STATUE_M,
    eIcons_ICON_BLD_FLAG_3A,
    eIcons_ICON_ARRAYSIZE
} eIcons;


typedef enum eImages
{
    eImages_IMAGE_FRONTEND_LOGO,
    eImages_IMAGE_FRONTEND_SH1LOGO,
    eImages_IMAGE_CREDITS1,
    eImages_IMAGE_CREDITS2,
    eImages_IMAGE_CREDITS3,
    eImages_IMAGE_CREDITS4,
    eImages_IMAGE_CREDITS5,
    eImages_IMAGE_CREDITS6,
    eImages_IMAGE_CREDITS7,
    eImages_IMAGE_CREDITS8,
    eImages_IMAGE_DEMOWISHLIST,
    eImages_IMAGE_DEMOSANDS,
    eImages_IMAGE_SKETCH_HOUSE,
    eImages_IMAGE_SKETCH_WOODCUTTERS_HUT,
    eImages_IMAGE_SKETCH_OXEN_BASE,
    eImages_IMAGE_SKETCH_IRON_MINE,
    eImages_IMAGE_SKETCH_PITCH_DIGGER,
    eImages_IMAGE_SKETCH_HUNTERS_HUT,
    eImages_IMAGE_SKETCH_FLETCHERS_WORKSHOP,
    eImages_IMAGE_SKETCH_BLACKSMITHS_WORKSHOP,
    eImages_IMAGE_SKETCH_POLETURNERS_WORKSHOP,
    eImages_IMAGE_SKETCH_ARMOURERS_WORKSHOP,
    eImages_IMAGE_SKETCH_TANNERS_WORKSHOP,
    eImages_IMAGE_SKETCH_BAKERS_WORKSHOP,
    eImages_IMAGE_SKETCH_BREWERS_WORKSHOP,
    eImages_IMAGE_SKETCH_QUARRY,
    eImages_IMAGE_SKETCH_INN,
    eImages_IMAGE_SKETCH_APOCATHERY,
    eImages_IMAGE_SKETCH_WELL,
    eImages_IMAGE_SKETCH_OIL_SMELTER,
    eImages_IMAGE_SKETCH_WHEATFARM,
    eImages_IMAGE_SKETCH_HOPSFARM,
    eImages_IMAGE_SKETCH_APPLEFARM,
    eImages_IMAGE_SKETCH_CATTLEFARM,
    eImages_IMAGE_SKETCH_MILL,
    eImages_IMAGE_SKETCH_STABLES,
    eImages_IMAGE_SKETCH_CHURCH1,
    eImages_IMAGE_SKETCH_KEEP_ONE,
    eImages_IMAGE_SKETCH_CAMPGROUND,
    eImages_IMAGE_SKETCH_TOWER,
    eImages_IMAGE_SKETCH_GALLOWS,
    eImages_IMAGE_SKETCH_STOCKS,
    eImages_IMAGE_SKETCH_MAYPOLE,
    eImages_IMAGE_SKETCH_GARDEN,
    eImages_IMAGE_SKETCH_KILLING_PIT,
    eImages_IMAGE_SKETCH_CESS_PIT,
    eImages_IMAGE_SKETCH_BURNING_STAKE,
    eImages_IMAGE_SKETCH_GIBBET,
    eImages_IMAGE_SKETCH_DUNGEON,
    eImages_IMAGE_SKETCH_RACK_STRETCHING,
    eImages_IMAGE_SKETCH_CHOPPING_BLOCK,
    eImages_IMAGE_SKETCH_DUNKING_STOOL,
    eImages_IMAGE_SKETCH_DOG_CAGE,
    eImages_IMAGE_SKETCH_STATUE,
    eImages_IMAGE_SKETCH_DANCING_BEAR,
    eImages_IMAGE_SKETCH_POND,
    eImages_IMAGE_SKETCH_ARMY,
    eImages_IMAGE_SKETCH_BAKER,
    eImages_IMAGE_SKETCH_BLACKSMITHX,
    eImages_IMAGE_SKETCH_CHICKEN,
    eImages_IMAGE_SKETCH_CHILD,
    eImages_IMAGE_SKETCH_CROSSBOWMAN,
    eImages_IMAGE_SKETCH_DRUNK,
    eImages_IMAGE_SKETCH_FARMER,
    eImages_IMAGE_SKETCH_FEARFACTOR,
    eImages_IMAGE_SKETCH_FIREWATCH,
    eImages_IMAGE_SKETCH_FOOD,
    eImages_IMAGE_SKETCH_GHOST,
    eImages_IMAGE_SKETCH_HEADS_ON_SPIKES,
    eImages_IMAGE_SKETCH_HEALER,
    eImages_IMAGE_SKETCH_HUNTER,
    eImages_IMAGE_SKETCH_INNKEEPER,
    eImages_IMAGE_SKETCH_IRON_MINER,
    eImages_IMAGE_SKETCH_JESTER,
    eImages_IMAGE_SKETCH_MOTHER,
    eImages_IMAGE_SKETCH_NULL,
    eImages_IMAGE_SKETCH_PITCHWORKER,
    eImages_IMAGE_SKETCH_POLETURNER,
    eImages_IMAGE_SKETCH_POPULARITY,
    eImages_IMAGE_SKETCH_POPULATION,
    eImages_IMAGE_SKETCH_PRIEST,
    eImages_IMAGE_SKETCH_RELIGION,
    eImages_IMAGE_SKETCH_SIEGE_ENGINEER,
    eImages_IMAGE_SKETCH_STOCKPILE,
    eImages_IMAGE_SKETCH_STONE_QUARRY,
    eImages_IMAGE_SKETCH_STONEMASON,
    eImages_IMAGE_SKETCH_TRADER,
    eImages_IMAGE_SKETCH_TUNNELOR,
    eImages_IMAGE_SKETCH_WEAPONS,
    eImages_IMAGE_SKETCH_WEDDING,
    eImages_IMAGE_SKETCH_LADY,
    eImages_IMAGE_SKETCH_BLACKSMITH,
    eImages_IMAGE_SKETCH_TANNER,
    eImages_IMAGE_SKETCH_WOODCUTTER,
    eImages_IMAGE_SKETCH_BREWER,
    eImages_IMAGE_SKETCH_FLETCHER,
    eImages_IMAGE_SKETCH_WATERPOT,
    eImages_IMAGE_SKETCH_TUNNELLERS_GUILD,
    eImages_IMAGE_SKETCH_MOSQUE,
    eImages_IMAGE_SKETCH_IMAM,
    eImages_IMAGE_SKETCH_ENGINEERS_GUILD,
    eImages_IMAGE_SKETCH_HUNTERS_DOG,
    eImages_IMAGE_SKETCH_TUNNEL,
    eImages_IMAGE_SKETCH_SIGNPOST,
    eImages_IMAGE_SKETCH_TENT,
    eImages_IMAGE_SKETCH_TRAVELLING_FAIR,
    eImages_IMAGE_SKETCH_JUGGLER,
    eImages_IMAGE_SKETCH_ARAB_STATUE,
    eImages_IMAGE_AD,
    eImages_IMAGE_LISTSIZE
} eImages;


typedef enum eUISprites
{
    eUISprites_SPRITE_GREEN_POP_HEAD,
    eUISprites_SPRITE_YELLOW_POP_HEAD,
    eUISprites_SPRITE_RED_POP_HEAD,
    eUISprites_SPRITE_GOODS_LARGE_WOOD,
    eUISprites_SPRITE_GOODS_LARGE_HOPS,
    eUISprites_SPRITE_GOODS_LARGE_STONE,
    eUISprites_SPRITE_GOODS_LARGE_IRON,
    eUISprites_SPRITE_GOODS_LARGE_PITCH,
    eUISprites_SPRITE_GOODS_LARGE_WHEAT,
    eUISprites_SPRITE_GOODS_LARGE_BREAD,
    eUISprites_SPRITE_GOODS_LARGE_CHEESE,
    eUISprites_SPRITE_GOODS_LARGE_MEAT,
    eUISprites_SPRITE_GOODS_LARGE_APPLES,
    eUISprites_SPRITE_GOODS_LARGE_ALE,
    eUISprites_SPRITE_GOODS_LARGE_GOLD,
    eUISprites_SPRITE_GOODS_LARGE_FLOUR,
    eUISprites_SPRITE_GOODS_LARGE_BOWS,
    eUISprites_SPRITE_GOODS_LARGE_XBOWS,
    eUISprites_SPRITE_GOODS_LARGE_SPEARS,
    eUISprites_SPRITE_GOODS_LARGE_PIKES,
    eUISprites_SPRITE_GOODS_LARGE_MACES,
    eUISprites_SPRITE_GOODS_LARGE_SWORDS,
    eUISprites_SPRITE_GOODS_LARGE_LEATHER_ARMOUR,
    eUISprites_SPRITE_GOODS_LARGE_ARMOUR,
    eUISprites_SPRITE_SCRIBE_001_C,
    eUISprites_SPRITE_SCRIBE_002_C,
    eUISprites_SPRITE_SCRIBE_003_C,
    eUISprites_SPRITE_SCRIBE_004_C,
    eUISprites_SPRITE_SCRIBE_005_C,
    eUISprites_SPRITE_SCRIBE_006_C,
    eUISprites_SPRITE_SCRIBE_007_C,
    eUISprites_SPRITE_SCRIBE_008_C,
    eUISprites_SPRITE_SCRIBE_009_C,
    eUISprites_SPRITE_SCRIBE_010_C,
    eUISprites_SPRITE_SCRIBE_011_C,
    eUISprites_SPRITE_SCRIBE_012_C,
    eUISprites_SPRITE_SCRIBE_013_C,
    eUISprites_SPRITE_SCRIBE_014_C,
    eUISprites_SPRITE_SCRIBE_015_C,
    eUISprites_SPRITE_SCRIBE_016_C,
    eUISprites_SPRITE_SCRIBE_017_C,
    eUISprites_SPRITE_SCRIBE_018_C,
    eUISprites_SPRITE_SCRIBE_019_C,
    eUISprites_SPRITE_SCRIBE_020_C,
    eUISprites_SPRITE_SCRIBE_021_C,
    eUISprites_SPRITE_SCRIBE_022_C,
    eUISprites_SPRITE_SCRIBE_023_C,
    eUISprites_SPRITE_SCRIBE_024_C,
    eUISprites_SPRITE_SCRIBE_025_C,
    eUISprites_SPRITE_SCRIBE_026_C,
    eUISprites_SPRITE_SCRIBE_027_C,
    eUISprites_SPRITE_SCRIBE_028_C,
    eUISprites_SPRITE_SCRIBE_029_C,
    eUISprites_SPRITE_SCRIBE_030_C,
    eUISprites_SPRITE_SCRIBE_031_C,
    eUISprites_SPRITE_SCRIBE_032_C,
    eUISprites_SPRITE_SCRIBE_033_C,
    eUISprites_SPRITE_SCRIBE_034_C,
    eUISprites_SPRITE_SCRIBE_035_C,
    eUISprites_SPRITE_SCRIBE_036_C,
    eUISprites_SPRITE_SCRIBE_037_C,
    eUISprites_SPRITE_SCRIBE_038_C,
    eUISprites_SPRITE_SCRIBE_039_C,
    eUISprites_SPRITE_SCRIBE_040_C,
    eUISprites_SPRITE_SCRIBE_041_C,
    eUISprites_SPRITE_SCRIBE_042_C,
    eUISprites_SPRITE_SCRIBE_043_C,
    eUISprites_SPRITE_SCRIBE_044_C,
    eUISprites_SPRITE_TUT_ARROW_1 = 73,
    eUISprites_SPRITE_TUT_ARROW_2,
    eUISprites_SPRITE_TUT_ARROW_3,
    eUISprites_SPRITE_TUT_ARROW_4,
    eUISprites_SPRITE_TUT_ARROW_5,
    eUISprites_SPRITE_TUT_ARROW_6,
    eUISprites_SPRITE_TUT_ARROW_7,
    eUISprites_SPRITE_TUT_ARROW_8,
    eUISprites_SPRITE_TUT_ARROW_9,
    eUISprites_SPRITE_TUT_ARROW_10,
    eUISprites_BUTTON_GATE_OPEN,
    eUISprites_BUTTON_GATE_OPEN_PRESSED,
    eUISprites_BUTTON_GATE_CLOSED,
    eUISprites_BUTTON_GATE_CLOSED_PRESSED,
    eUISprites_MAP_LOWER_EDGE_MASK,
    eUISprites_MAP_FF,
    eUISprites_MAP_STEAM,
    eUISprites_MAP_USER,
    eUISprites_RIGHTCLICK_FLATTEN_HIGH,
    eUISprites_RIGHTCLICK_FLATTEN_LOW,
    eUISprites_RIGHTCLICK_ROTATION_NORTH,
    eUISprites_RIGHTCLICK_ROTATION_EAST,
    eUISprites_RIGHTCLICK_ROTATION_SOUTH,
    eUISprites_RIGHTCLICK_ROTATION_WEST,
    eUISprites_RIGHTCLICK_ZOOM,
    eUISprites_RIGHTCLICK_ZOOMED_IN,
    eUISprites_RIGHTCLICK_ZOOMED_OUT,
    eUISprites_RIGHTCLICK_UI_VISIBLE,
    eUISprites_RIGHTCLICK_UI_HIDDEN,
    eUISprites_BALANCED,
    eUISprites_READYSTATE_NOTREADY,
    eUISprites_READYSTATE_NOTREADY_OVER,
    eUISprites_READYSTATE_READY,
    eUISprites_READYSTATE_READY_OVER,
    eUISprites_MP_TEAM_BLUE,
    eUISprites_MP_TEAM_ORANGE,
    eUISprites_MP_TEAM_YELLOW,
    eUISprites_MP_TEAM_RED,
    eUISprites_MP_TEAM_BLACK,
    eUISprites_MP_TEAM_PURPLE,
    eUISprites_MP_TEAM_CYAN,
    eUISprites_MP_TEAM_GREEN,
    eUISprites_PIE1,
    eUISprites_IMAGE_PARTNER_WOODCUTTER = 151,
    eUISprites_IMAGE_PARTNER_FLETCHER,
    eUISprites_IMAGE_PARTNER_TUNELLER,
    eUISprites_IMAGE_PARTNER_BREWER,
    eUISprites_IMAGE_PARTNER_TANNER,
    eUISprites_IMAGE_PARTNER_MOTHER,
    eUISprites_IMAGE_PARTNER_HUNTER,
    eUISprites_IMAGE_PARTNER_MASON,
    eUISprites_IMAGE_PARTNER_PITCHWORKER,
    eUISprites_IMAGE_PARTNER_FARMER,
    eUISprites_IMAGE_PARTNER_MILLER,
    eUISprites_IMAGE_PARTNER_BAKER,
    eUISprites_IMAGE_PARTNER_POLETURNER,
    eUISprites_IMAGE_PARTNER_BLACKSMITH,
    eUISprites_IMAGE_PARTNER_ARMOURER,
    eUISprites_IMAGE_PARTNER_MINER,
    eUISprites_IMAGE_PARTNER_PRIEST,
    eUISprites_IMAGE_PARTNER_HEALER,
    eUISprites_IMAGE_PARTNER_DRUNK,
    eUISprites_IMAGE_PARTNER_INNKEEPER,
    eUISprites_IMAGE_PARTNER_TRADER,
    eUISprites_IMAGE_PARTNER_JESTER,
    eUISprites_IMAGE_PARTNER_JUGGLER,
    eUISprites_IMAGE_PARTNER_PEASANT,
    eUISprites_IMAGE_MISC_REPORTSHIELD,
    eUISprites_IMAGE_SKETCH_HOUSE_eUISprites,
    eUISprites_IMAGE_SKETCH_WOODCUTTERS_HUT_eUISprites,
    eUISprites_IMAGE_SKETCH_OXEN_BASE_eUISprites,
    eUISprites_IMAGE_SKETCH_IRON_MINE_eUISprites,
    eUISprites_IMAGE_SKETCH_PITCH_DIGGER_eUISprites,
    eUISprites_IMAGE_SKETCH_HUNTERS_HUT_eUISprites,
    eUISprites_IMAGE_SKETCH_FLETCHERS_WORKSHOP_eUISprites,
    eUISprites_IMAGE_SKETCH_BLACKSMITHS_WORKSHOP_eUISprites,
    eUISprites_IMAGE_SKETCH_POLETURNERS_WORKSHOP_eUISprites,
    eUISprites_IMAGE_SKETCH_ARMOURERS_WORKSHOP_eUISprites,
    eUISprites_IMAGE_SKETCH_TANNERS_WORKSHOP_eUISprites,
    eUISprites_IMAGE_SKETCH_BAKERS_WORKSHOP_eUISprites,
    eUISprites_IMAGE_SKETCH_BREWERS_WORKSHOP_eUISprites,
    eUISprites_IMAGE_SKETCH_QUARRY_eUISprites,
    eUISprites_IMAGE_SKETCH_INN_eUISprites,
    eUISprites_IMAGE_SKETCH_APOCATHERY_eUISprites,
    eUISprites_IMAGE_SKETCH_WELL_eUISprites,
    eUISprites_IMAGE_SKETCH_OIL_SMELTER_eUISprites,
    eUISprites_IMAGE_SKETCH_WHEATFARM_eUISprites,
    eUISprites_IMAGE_SKETCH_HOPSFARM_eUISprites,
    eUISprites_IMAGE_SKETCH_APPLEFARM_eUISprites,
    eUISprites_IMAGE_SKETCH_CATTLEFARM_eUISprites,
    eUISprites_IMAGE_SKETCH_MILL_eUISprites,
    eUISprites_IMAGE_SKETCH_STABLES_eUISprites,
    eUISprites_IMAGE_SKETCH_CHURCH1_eUISprites,
    eUISprites_IMAGE_SKETCH_KEEP_ONE_eUISprites,
    eUISprites_IMAGE_SKETCH_CAMPGROUND_eUISprites,
    eUISprites_IMAGE_SKETCH_TOWER_eUISprites,
    eUISprites_IMAGE_SKETCH_GALLOWS_eUISprites,
    eUISprites_IMAGE_SKETCH_STOCKS_eUISprites,
    eUISprites_IMAGE_SKETCH_MAYPOLE_eUISprites,
    eUISprites_IMAGE_SKETCH_GARDEN_eUISprites,
    eUISprites_IMAGE_SKETCH_KILLING_PIT_eUISprites,
    eUISprites_IMAGE_SKETCH_CESS_PIT_eUISprites,
    eUISprites_IMAGE_SKETCH_BURNING_STAKE_eUISprites,
    eUISprites_IMAGE_SKETCH_GIBBET_eUISprites,
    eUISprites_IMAGE_SKETCH_DUNGEON_eUISprites,
    eUISprites_IMAGE_SKETCH_RACK_STRETCHING_eUISprites,
    eUISprites_IMAGE_SKETCH_CHOPPING_BLOCK_eUISprites,
    eUISprites_IMAGE_SKETCH_DUNKING_STOOL_eUISprites,
    eUISprites_IMAGE_SKETCH_DOG_CAGE_eUISprites,
    eUISprites_IMAGE_SKETCH_STATUE_eUISprites,
    eUISprites_IMAGE_SKETCH_DANCING_BEAR_eUISprites,
    eUISprites_IMAGE_SKETCH_POND_eUISprites,
    eUISprites_IMAGE_SKETCH_ARMY_eUISprites,
    eUISprites_IMAGE_SKETCH_BAKER_eUISprites,
    eUISprites_IMAGE_SKETCH_BLACKSMITHX_eUISprites,
    eUISprites_IMAGE_SKETCH_CHICKEN_eUISprites,
    eUISprites_IMAGE_SKETCH_CHILD_eUISprites,
    eUISprites_IMAGE_SKETCH_CROSSBOWMAN_eUISprites,
    eUISprites_IMAGE_SKETCH_DRUNK_eUISprites,
    eUISprites_IMAGE_SKETCH_FARMER_eUISprites,
    eUISprites_IMAGE_SKETCH_FEARFACTOR_eUISprites,
    eUISprites_IMAGE_SKETCH_FIREWATCH_eUISprites,
    eUISprites_IMAGE_SKETCH_FOOD_eUISprites,
    eUISprites_IMAGE_SKETCH_GHOST_eUISprites,
    eUISprites_IMAGE_SKETCH_HEADS_ON_SPIKES_eUISprites,
    eUISprites_IMAGE_SKETCH_HEALER_eUISprites,
    eUISprites_IMAGE_SKETCH_HUNTER_eUISprites,
    eUISprites_IMAGE_SKETCH_INNKEEPER_eUISprites,
    eUISprites_IMAGE_SKETCH_IRON_MINER_eUISprites,
    eUISprites_IMAGE_SKETCH_JESTER_eUISprites,
    eUISprites_IMAGE_SKETCH_MOTHER_eUISprites,
    eUISprites_IMAGE_SKETCH_NULL_eUISprites,
    eUISprites_IMAGE_SKETCH_PITCHWORKER_eUISprites,
    eUISprites_IMAGE_SKETCH_POLETURNER_eUISprites,
    eUISprites_IMAGE_SKETCH_POPULARITY_eUISprites,
    eUISprites_IMAGE_SKETCH_POPULATION_eUISprites,
    eUISprites_IMAGE_SKETCH_PRIEST_eUISprites,
    eUISprites_IMAGE_SKETCH_RELIGION_eUISprites,
    eUISprites_IMAGE_SKETCH_SIEGE_ENGINEER_eUISprites,
    eUISprites_IMAGE_SKETCH_STOCKPILE_eUISprites,
    eUISprites_IMAGE_SKETCH_STONE_QUARRY_eUISprites,
    eUISprites_IMAGE_SKETCH_STONEMASON_eUISprites,
    eUISprites_IMAGE_SKETCH_TRADER_eUISprites,
    eUISprites_IMAGE_SKETCH_TUNNELOR_eUISprites,
    eUISprites_IMAGE_SKETCH_WEAPONS_eUISprites,
    eUISprites_IMAGE_SKETCH_WEDDING_eUISprites,
    eUISprites_SPRITE_COW,
    eUISprites_IMAGE_SKETCH_LADY_eUISprites,
    eUISprites_IMAGE_SKETCH_BLACKSMITH_eUISprites,
    eUISprites_IMAGE_SKETCH_TANNER_eUISprites,
    eUISprites_IMAGE_SKETCH_WOODCUTTER_eUISprites,
    eUISprites_IMAGE_SKETCH_BREWER_eUISprites,
    eUISprites_IMAGE_SKETCH_FLETCHER_eUISprites,
    eUISprites_IMAGE_SUBTITLES_GREEN,
    eUISprites_IMAGE_SUBTITLES_RED,
    eUISprites_SPRITE_CURSOR_OVER,
    eUISprites_SPRITE_CURSOR_SELECTED,
    eUISprites_SPRITE_CURSOR_UP,
    eUISprites_SPRITE_SWORD_OVER,
    eUISprites_SPRITE_SWORD_SELECTED,
    eUISprites_SPRITE_SWORD_UP,
    eUISprites_SPRITE_AUTOTRADE_ON_NORM,
    eUISprites_SPRITE_AUTOTRADE_ON_OVER,
    eUISprites_SPRITE_AUTOTRADE_OFF_NORM,
    eUISprites_SPRITE_AUTOTRADE_OFF_OVER,
    eUISprites_SPRITE_AUTOTRADE_SELECT_ON_NORM,
    eUISprites_SPRITE_AUTOTRADE_SELECT_ON_OVER,
    eUISprites_SPRITE_AUTOTRADE_SELECT_OFF_NORM,
    eUISprites_SPRITE_AUTOTRADE_SELECT_OFF_OVER,
    eUISprites_SPRITE_TROOP_ARCHER,
    eUISprites_SPRITE_TROOP_SPEARMAN,
    eUISprites_SPRITE_TROOP_PIKEMAN,
    eUISprites_SPRITE_TROOP_MACEMAN,
    eUISprites_SPRITE_TROOP_XBOWMAN,
    eUISprites_SPRITE_TROOP_SWORDSMAN,
    eUISprites_SPRITE_TROOP_KNIGHT,
    eUISprites_SPRITE_TROOP_LADDERMAN,
    eUISprites_SPRITE_TROOP_ENGINEER,
    eUISprites_SPRITE_TROOP_ENGINEERPOT,
    eUISprites_SPRITE_TROOP_MONK,
    eUISprites_SPRITE_TROOP_CATAPULT,
    eUISprites_SPRITE_TROOP_TREBUCHET,
    eUISprites_SPRITE_TROOP_BATTERINGRAM,
    eUISprites_SPRITE_TROOP_SIEGETOWER,
    eUISprites_SPRITE_TROOP_SHIELD,
    eUISprites_SPRITE_TROOP_TUNNELLOR,
    eUISprites_SPRITE_TROOP_BALLISTA,
    eUISprites_SPRITE_TROOP_MANGONEL,
    eUISprites_SPRITE_SCRIBE_001_B,
    eUISprites_SPRITE_SCRIBE_002_B,
    eUISprites_SPRITE_SCRIBE_003_B,
    eUISprites_SPRITE_SCRIBE_004_B,
    eUISprites_SPRITE_SCRIBE_005_B,
    eUISprites_SPRITE_SCRIBE_006_B,
    eUISprites_SPRITE_SCRIBE_007_B,
    eUISprites_SPRITE_SCRIBE_008_B,
    eUISprites_SPRITE_SCRIBE_009_B,
    eUISprites_SPRITE_SCRIBE_010_B,
    eUISprites_SPRITE_SCRIBE_011_B,
    eUISprites_SPRITE_SCRIBE_012_B,
    eUISprites_SPRITE_SCRIBE_013_B,
    eUISprites_SPRITE_SCRIBE_014_B,
    eUISprites_SPRITE_SCRIBE_015_B,
    eUISprites_SPRITE_SCRIBE_016_B,
    eUISprites_SPRITE_SCRIBE_017_B,
    eUISprites_SPRITE_SCRIBE_018_B,
    eUISprites_SPRITE_SCRIBE_019_B,
    eUISprites_SPRITE_SCRIBE_020_B,
    eUISprites_SPRITE_SCRIBE_021_B,
    eUISprites_SPRITE_SCRIBE_022_B,
    eUISprites_SPRITE_SCRIBE_023_B,
    eUISprites_SPRITE_SCRIBE_024_B,
    eUISprites_SPRITE_SCRIBE_025_B,
    eUISprites_SPRITE_SCRIBE_026_B,
    eUISprites_SPRITE_SCRIBE_027_B,
    eUISprites_SPRITE_SCRIBE_028_B,
    eUISprites_SPRITE_SCRIBE_029_B,
    eUISprites_SPRITE_SCRIBE_030_B,
    eUISprites_SPRITE_SCRIBE_031_B,
    eUISprites_SPRITE_SCRIBE_032_B,
    eUISprites_SPRITE_SCRIBE_033_B,
    eUISprites_SPRITE_SCRIBE_034_B,
    eUISprites_SPRITE_SCRIBE_035_B,
    eUISprites_SPRITE_SCRIBE_036_B,
    eUISprites_SPRITE_SCRIBE_037_B,
    eUISprites_SPRITE_SCRIBE_038_B,
    eUISprites_SPRITE_SCRIBE_039_B,
    eUISprites_SPRITE_SCRIBE_040_B,
    eUISprites_SPRITE_SCRIBE_041_B,
    eUISprites_SPRITE_SCRIBE_042_B,
    eUISprites_SPRITE_SCRIBE_043_B,
    eUISprites_SPRITE_SCRIBE_044_B,
    eUISprites_SPRITE_SCRIBE_B_OVER,
    eUISprites_SPRITE_SCRIBE_B_SELECTED,
    eUISprites_SPRITE_SCRIBE_B_UP,
    eUISprites_SPRITE_SCRIBE_C_OVER,
    eUISprites_SPRITE_SCRIBE_C_SELECTED,
    eUISprites_SPRITE_SCRIBE_C_UP,
    eUISprites_SPRITE_CART1,
    eUISprites_SPRITE_CART2,
    eUISprites_SPRITE_CART3,
    eUISprites_SPRITE_CART4,
    eUISprites_SPRITE_CART5,
    eUISprites_SPRITE_CART6,
    eUISprites_MP_TEAM_BLUE_OVER,
    eUISprites_MP_TEAM_ORANGE_OVER,
    eUISprites_MP_TEAM_YELLOW_OVER,
    eUISprites_MP_TEAM_RED_OVER,
    eUISprites_MP_TEAM_BLACK_OVER,
    eUISprites_MP_TEAM_PURPLE_OVER,
    eUISprites_MP_TEAM_CYAN_OVER,
    eUISprites_MP_TEAM_GREEN_OVER,
    eUISprites_MP_TEAM_BLUE_SELECTED,
    eUISprites_MP_TEAM_ORANGE_SELECTED,
    eUISprites_MP_TEAM_YELLOW_SELECTED,
    eUISprites_MP_TEAM_RED_SELECTED,
    eUISprites_MP_TEAM_BLACK_SELECTED,
    eUISprites_MP_TEAM_PURPLE_SELECTED,
    eUISprites_MP_TEAM_CYAN_SELECTED,
    eUISprites_MP_TEAM_GREEN_SELECTED,
    eUISprites_MAP_COMPLETED,
    eUISprites_MAP_NOTCOMPLETED,
    eUISprites_IMAGE_SKETCH_WATERPOT_eUISprites,
    eUISprites_IMAGE_SKETCH_TUNNELLERS_GUILD_eUISprites,
    eUISprites_IMAGE_AI_FACE_01,
    eUISprites_IMAGE_AI_FACE_02,
    eUISprites_IMAGE_AI_FACE_03,
    eUISprites_IMAGE_AI_FACE_04,
    eUISprites_IMAGE_AI_FACE_05,
    eUISprites_IMAGE_AI_FACE_06,
    eUISprites_IMAGE_AI_FACE_07,
    eUISprites_IMAGE_AI_FACE_08,
    eUISprites_IMAGE_AI_FACE_09,
    eUISprites_IMAGE_AI_FACE_10,
    eUISprites_IMAGE_AI_FACE_11,
    eUISprites_IMAGE_AI_FACE_12,
    eUISprites_IMAGE_AI_FACE_13,
    eUISprites_IMAGE_AI_FACE_14,
    eUISprites_IMAGE_AI_FACE_15,
    eUISprites_IMAGE_AI_FACE_16,
    eUISprites_IMAGE_AI_FACE_BACKGROUND_RED,
    eUISprites_IMAGE_AI_FACE_BACKGROUND_ORANGE,
    eUISprites_IMAGE_AI_FACE_BACKGROUND_YELLOW,
    eUISprites_IMAGE_AI_FACE_BACKGROUND_BLUE,
    eUISprites_IMAGE_AI_FACE_BACKGROUND_BLACK,
    eUISprites_IMAGE_AI_FACE_BACKGROUND_PURPLE,
    eUISprites_IMAGE_AI_FACE_BACKGROUND_CYAN,
    eUISprites_IMAGE_AI_FACE_BACKGROUND_GREEN,
    eUISprites_SPRITE_CRUSADER_LORD,
    eUISprites_SPRITE_CRUSADER_LORD_OVER,
    eUISprites_SPRITE_ARABIC_LORD,
    eUISprites_SPRITE_ARABIC_LORD_OVER,
    eUISprites_SPRITE_TEAM_ALLIES_SHIELD_SMALL_1,
    eUISprites_SPRITE_TEAM_ALLIES_SHIELD_SMALL_2,
    eUISprites_SPRITE_TEAM_ALLIES_SHIELD_SMALL_3,
    eUISprites_SPRITE_TEAM_ALLIES_SHIELD_SMALL_4,
    eUISprites_SPRITE_TEAM_ALLIES_SHIELD_LARGE_1,
    eUISprites_SPRITE_TEAM_ALLIES_SHIELD_LARGE_2,
    eUISprites_SPRITE_TEAM_ALLIES_SHIELD_LARGE_3,
    eUISprites_SPRITE_TEAM_ALLIES_SHIELD_LARGE_4,
    eUISprites_SPRITE_TEAM_SHIELD_RED,
    eUISprites_SPRITE_TEAM_SHIELD_ORANGE,
    eUISprites_SPRITE_TEAM_SHIELD_YELLOW,
    eUISprites_SPRITE_TEAM_SHIELD_BLUE,
    eUISprites_SPRITE_TEAM_SHIELD_BLACK,
    eUISprites_SPRITE_TEAM_SHIELD_PURPLE,
    eUISprites_SPRITE_TEAM_SHIELD_CYAN,
    eUISprites_SPRITE_TEAM_SHIELD_GREEN,
    eUISprites_IMAGE_MISC_REPORTSHIELD2,
    eUISprites_SPRITE_ALLIES_GOODS1,
    eUISprites_SPRITE_ALLIES_GOODS2,
    eUISprites_SPRITE_ALLIES_GOODS3,
    eUISprites_SPRITE_ALLIES_GOODS4,
    eUISprites_SPRITE_ALLIES_GOODS5,
    eUISprites_SPRITE_ALLIES_GOODS6,
    eUISprites_SPRITE_ALLIES_GOODS7,
    eUISprites_SPRITE_ALLIES_GOODS8,
    eUISprites_SPRITE_ALLIES_GOODS9,
    eUISprites_SPRITE_ALLIES_GOODS10,
    eUISprites_SPRITE_ALLIES_GOODS11,
    eUISprites_SPRITE_ALLIES_GOODS12,
    eUISprites_SPRITE_ALLIES_GOODS13,
    eUISprites_SPRITE_ALLIES_GOODS14,
    eUISprites_SPRITE_ALLIES_GOODS15,
    eUISprites_SPRITE_ALLIES_GOODS16,
    eUISprites_SPRITE_ALLIES_GOODS17,
    eUISprites_SPRITE_ALLIES_GOODS18,
    eUISprites_SPRITE_ALLIES_GOODS19,
    eUISprites_SPRITE_ALLIES_GOODS20,
    eUISprites_SPRITE_ALLIES_GOODS21,
    eUISprites_SPRITE_ALLIES_GOODS22,
    eUISprites_SPRITE_ALLIES_GOODS23,
    eUISprites_SPRITE_ALLIES_GOODS24,
    eUISprites_SPRITE_ALLIES_GOODS25,
    eUISprites_SPRITE_GOODS_SMALL_WOOD,
    eUISprites_SPRITE_GOODS_SMALL_HOPS,
    eUISprites_SPRITE_GOODS_SMALL_STONE,
    eUISprites_SPRITE_GOODS_SMALL_IRON,
    eUISprites_SPRITE_GOODS_SMALL_PITCH,
    eUISprites_SPRITE_GOODS_SMALL_WHEAT,
    eUISprites_SPRITE_GOODS_SMALL_BREAD,
    eUISprites_SPRITE_GOODS_SMALL_CHEESE,
    eUISprites_SPRITE_GOODS_SMALL_MEAT,
    eUISprites_SPRITE_GOODS_SMALL_APPLES,
    eUISprites_SPRITE_GOODS_SMALL_ALE,
    eUISprites_SPRITE_GOODS_SMALL_GOLD,
    eUISprites_SPRITE_GOODS_SMALL_FLOUR,
    eUISprites_SPRITE_GOODS_SMALL_BOWS,
    eUISprites_SPRITE_GOODS_SMALL_XBOWS,
    eUISprites_SPRITE_GOODS_SMALL_SPEARS,
    eUISprites_SPRITE_GOODS_SMALL_PIKES,
    eUISprites_SPRITE_GOODS_SMALL_MACES,
    eUISprites_SPRITE_GOODS_SMALL_SWORDS,
    eUISprites_SPRITE_GOODS_SMALL_LEATHERARMOUR,
    eUISprites_SPRITE_GOODS_SMALL_METALARMOUR,
    eUISprites_MP_TEAM_BLUE_DARK,
    eUISprites_MP_TEAM_ORANGE_DARK,
    eUISprites_MP_TEAM_YELLOW_DARK,
    eUISprites_MP_TEAM_RED_DARK,
    eUISprites_MP_TEAM_BLACK_DARK,
    eUISprites_MP_TEAM_PURPLE_DARK,
    eUISprites_MP_TEAM_CYAN_DARK,
    eUISprites_MP_TEAM_GREEN_DARK,
    eUISprites_TRAIL_SWORD_1,
    eUISprites_TRAIL_SWORD_12 = 482,
    eUISprites_SPRITE_BODY_CHICKEN_1,
    eUISprites_SPRITE_BODY_CHICKEN_2,
    eUISprites_SPRITE_BODY_CHICKEN_3,
    eUISprites_SPRITE_BODY_CHICKEN_5,
    eUISprites_SPRITE_BODY_CHICKEN_6,
    eUISprites_SPRITE_BODY_CHICKEN_7,
    eUISprites_SPRITE_BODY_CHICKEN_73,
    eUISprites_SPRITE_BODY_CHICKEN_74,
    eUISprites_SPRITE_BODY_CHICKEN_75,
    eUISprites_SPRITE_BODY_CHICKEN_77,
    eUISprites_SPRITE_BODY_CHICKEN_78,
    eUISprites_SPRITE_BODY_CHICKEN_79,
    eUISprites_SPRITE_BODY_CHICKEN_83,
    eUISprites_SPRITE_BODY_CHICKEN_137,
    eUISprites_SPRITE_BODY_CHICKEN_138,
    eUISprites_SPRITE_BODY_CHICKEN_139,
    eUISprites_SPRITE_BODY_CHICKEN_141,
    eUISprites_SPRITE_BODY_CHICKEN_142,
    eUISprites_SPRITE_BODY_CHICKEN_143,
    eUISprites_SPRITE_BODY_CHICKEN_144,
    eUISprites_SPRITE_BODY_CHICKEN_261,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_2,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_3,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_5,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_6,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_7,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_73,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_78,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_79,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_81,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_137,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_138,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_139,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_141,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_142,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_143,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_147,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_261,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_1,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_74,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_75,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_77,
    eUISprites_SPRITE_BODY_CHICKEN_BROWN_82,
    eUISprites_SPRITE_TRAIL_KNIGHT1,
    eUISprites_SPRITE_TRAIL_KNIGHT50 = 575,
    eUISprites_MP_TEAM_BLANK_DARK,
    eUISprites_IMAGE_AI_FACE_17,
    eUISprites_IMAGE_AI_FACE_18,
    eUISprites_IMAGE_AI_FACE_19,
    eUISprites_IMAGE_AI_FACE_20,
    eUISprites_IMAGE_AI_FACE_21,
    eUISprites_IMAGE_AI_FACE_22,
    eUISprites_IMAGE_AI_FACE_23,
    eUISprites_IMAGE_AI_FACE_24,
    eUISprites_IMAGE_AI_FACE_25,
    eUISprites_IMAGE_SKIRMISHMASTERS_BAR1,
    eUISprites_IMAGE_SKIRMISHMASTERS_BAR2,
    eUISprites_IMAGE_SANDS_LEVEL1,
    eUISprites_IMAGE_SANDS_LEVEL2,
    eUISprites_IMAGE_SANDS_LEVEL3,
    eUISprites_IMAGE_SANDS_LEVEL4,
    eUISprites_IMAGE_SANDS_LEVEL5,
    eUISprites_SPRITE_LARGE_CHICKEN1,
    eUISprites_SPRITE_LARGE_CHICKEN47 = 639,
    eUISprites_SPRITE_CHECK_YES,
    eUISprites_SPRITE_CHECK_NO,
    eUISprites_SPRITE_SKIPPED_MISSION,
    eUISprites_SPRITE_TROOP_ARAB_BOW,
    eUISprites_SPRITE_TROOP_ARAB_SLAVE,
    eUISprites_SPRITE_TROOP_ARAB_SLINGER,
    eUISprites_SPRITE_TROOP_ARAB_ASSASIN,
    eUISprites_SPRITE_TROOP_ARAB_HORSEMAN,
    eUISprites_SPRITE_TROOP_ARAB_SWORDSMAN,
    eUISprites_SPRITE_TROOP_ARAB_GRENADIER,
    eUISprites_SPRITE_TROOP_ARAB_BALLISTA,
    eUISprites_SPRITE_TROOP_BEDOUIN_CAMEL_LANCER,
    eUISprites_SPRITE_TROOP_BEDOUIN_HEALER,
    eUISprites_SPRITE_TROOP_BEDOUIN_EUNUCH,
    eUISprites_SPRITE_TROOP_BEDOUIN_AMBUSHER,
    eUISprites_SPRITE_TROOP_BEDOUIN_SKIRMISHER,
    eUISprites_SPRITE_TROOP_BEDOUIN_HEAVY_CAMEL,
    eUISprites_SPRITE_TROOP_BEDOUIN_SAPPER,
    eUISprites_SPRITE_TROOP_BEDOUIN_DEMOLISHER,
    eUISprites_SPRITE_BEDOUIN_LORD,
    eUISprites_SPRITE_BEDOUIN_LORD_OVER,
    eUISprites_SPRITE_SCRIBE_LORD,
    eUISprites_SPRITE_SCRIBE_LORD_OVER,
    eUISprites_SPRITE_FEMALE_LORD,
    eUISprites_SPRITE_FEMALE_LORD_OVER,
    eUISprites_SPRITE_MP_OPTIONS,
    eUISprites_SPRITE_MP_OPTIONS_ADV,
    eUISprites_SPRITE_BESSY_LORD,
    eUISprites_SPRITE_BESSY_LORD_OVER,
    eUISprites_SPRITE_MP_OPTIONS_EXTREME,
    eUISprites_SPRITE_MP_OPTIONS_ADV_EXTREME,
    eUISprites_SPRITE_MERIT_KEEP1,
    eUISprites_SPRITE_MERIT_KEEP2,
    eUISprites_SPRITE_MERIT_KEEP3,
    eUISprites_SPRITE_MERIT_KEEP4,
    eUISprites_SPRITE_MERIT_KEEP5,
    eUISprites_SPRITE_MERIT_KEEP6,
    eUISprites_SPRITE_MERIT_KEEP7,
    eUISprites_SPRITE_MERIT_KEEP8,
    eUISprites_SPRITE_MERIT_OVER_KEEP1,
    eUISprites_SPRITE_MERIT_OVER_KEEP2,
    eUISprites_SPRITE_MERIT_OVER_KEEP3,
    eUISprites_SPRITE_MERIT_OVER_KEEP4,
    eUISprites_SPRITE_MERIT_OVER_KEEP5,
    eUISprites_SPRITE_MERIT_OVER_KEEP6,
    eUISprites_SPRITE_MERIT_OVER_KEEP7,
    eUISprites_SPRITE_MERIT_OVER_KEEP8,
    eUISprites_UNBALANCED,
    eUISprites_READYSTATE_LOCKED,
    eUISprites_READYSTATE_LOCKED_OVER,
    eUISprites_READYSTATE_UNLOCKED,
    eUISprites_READYSTATE_UNLOCKED_OVER,
    eUISprites_SPRITE_DEAD_LORD,
    eUISprites_SPRITE_CHAT_NOT_MUTE,
    eUISprites_SPRITE_CHAT_NOT_MUTE_OVER,
    eUISprites_SPRITE_CHAT_MUTE,
    eUISprites_SPRITE_CHAT_MUTE_OVER,
    eUISprites_IMAGE_SANDS_LEVEL1_GREY,
    eUISprites_IMAGE_SANDS_LEVEL2_GREY,
    eUISprites_IMAGE_SANDS_LEVEL3_GREY,
    eUISprites_IMAGE_SANDS_LEVEL4_GREY,
    eUISprites_IMAGE_SANDS_LEVEL5_GREY,
    eUISprites_IMAGE_SANDS_LEVEL1_LARGE,
    eUISprites_IMAGE_SANDS_LEVEL2_LARGE,
    eUISprites_IMAGE_SANDS_LEVEL3_LARGE,
    eUISprites_IMAGE_SANDS_LEVEL4_LARGE,
    eUISprites_IMAGE_SANDS_LEVEL5_LARGE,
    eUISprites_IMAGE_TRAIL_COMPLETE_KNIGHT,
    eUISprites_IMAGE_TRAIL_COMPLETE_CHICKEN,
    eUISprites_SPRITE_ARABIC_LORD_FEMALE,
    eUISprites_SPRITE_ARABIC_LORD_OVER_FEMALE,
    eUISprites_SPRITE_BEDOUIN_LORD_FEMALE,
    eUISprites_SPRITE_BEDOUIN_LORD_OVER_FEMALE,
    eUISprites_IMAGE_SANDS_LEVEL1_SMALL,
    eUISprites_IMAGE_SANDS_LEVEL2_SMALL,
    eUISprites_IMAGE_SANDS_LEVEL3_SMALL,
    eUISprites_IMAGE_SANDS_LEVEL4_SMALL,
    eUISprites_IMAGE_SANDS_LEVEL5_SMALL,
    eUISprites_SPRITE_TRAIL_COOP_KNIGHTS,
    eUISprites_IMAGE_MISC_REPORTSHIELD3,
    eUISprites_COA_LOCKED,
    eUISprites_COA_LOCKED_OVER,
    eUISprites_COA_UNLOCKED,
    eUISprites_COA_UNLOCKED_OVER,
    eUISprites_SHIELD_OPTIONS,
    eUISprites_SPRITE_MP_COOP_OPTIONS,
    eUISprites_SPRITE_MP_COOP_OPTIONS_ADV,
    eUISprites_SPRITE_MP_COOP_OPTIONS_EXTREME,
    eUISprites_SPRITE_MP_COOP_OPTIONS_ADV_EXTREME,
    eUISprites_SPRITE_MP_COOP,
    eUISprites_IMAGE_AI_FACE_26,
    eUISprites_IMAGE_AI_FACE_27,
    eUISprites_IMAGE_AI_FACE_28,
    eUISprites_IMAGE_AI_FACE_29,
    eUISprites_IMAGE_TRAIL_FLAG,
    eUISprites_IMAGE_TRAIL_CROSS,
    eUISprites_IMAGE_TRAIL_WORKSHOP_LARGE,
    eUISprites_IMAGE_WORKSHOP_INVERTED,
    eUISprites_IMAGE_CUSTOM_LORD_TYPE1,
    eUISprites_IMAGE_CUSTOM_LORD_TYPE2,
    eUISprites_IMAGE_CUSTOM_LORD_TYPE3,
    eUISprites_IMAGE_CUSTOM_LORD_TYPE4,
    eUISprites_IMAGE_CUSTOM_LORD_TYPE5,
    eUISprites_IMAGE_CUSTOM_LORD_TYPE6,
    eUISprites_IMAGE_CUSTOM_LORD_TYPE7,
    eUISprites_IMAGE_CUSTOM_LORD_TYPE8,
    eUISprites_IMAGE_WORKSHOP_UPLOADED,
    eUISprites_SPRITE_LISTSIZE
} eUISprites;


typedef enum eTextSections
{
    eTextSections_TEXT_MONTHS = 1,
    eTextSections_TEXT_GOODS,
    eTextSections_TEXT_POPULARITY_EFFECTS,
    eTextSections_TEXT_STARTUP,
    eTextSections_TEXT_MAINOPTIONS,
    eTextSections_TEXT_LANGUAGE,
    eTextSections_TEXT_BUBBLE_HELP_SUBTEXT,
    eTextSections_TEXT_BUBBLE_HELP_TEXT,
    eTextSections_TEXT_BUBBLE_HELP_DATA,
    eTextSections_TEXT_FEEDBACK,
    eTextSections_TEXT_MAPEDIT,
    eTextSections_TEXT_DEMOSCORE,
    eTextSections_TEXT_MAP_TITLES,
    eTextSections_TEXT_REPORTS,
    eTextSections_TEXT_IN_GENERAL_BUILDINGS = 18,
    eTextSections_TEXT_IN_KEEP,
    eTextSections_TEXT_IN_INN,
    eTextSections_TEXT_IN_BARRACKS = 22,
    eTextSections_TEXT_IN_GRANARY,
    eTextSections_TEXT_IN_HOUSE,
    eTextSections_TEXT_IN_WOODCUTTERS_HUT,
    eTextSections_TEXT_IN_OXEN_BASE,
    eTextSections_TEXT_IN_IRON_MINE,
    eTextSections_TEXT_IN_PITCH_DIGGER,
    eTextSections_TEXT_IN_HUNTERS_HUT,
    eTextSections_TEXT_IN_GOODS_YARD,
    eTextSections_TEXT_IN_ARMOURY,
    eTextSections_TEXT_IN_FLETCHERS_WORKSHOP,
    eTextSections_TEXT_IN_BLACKSMITHS_WORKSHOP,
    eTextSections_TEXT_IN_POLETURNERS_WORKSHOP,
    eTextSections_TEXT_IN_ARMOURERS_WORKSHOP,
    eTextSections_TEXT_IN_TANNERS_WORKSHOP,
    eTextSections_TEXT_IN_BAKERS_WORKSHOP,
    eTextSections_TEXT_IN_BREWERS_WORKSHOP,
    eTextSections_TEXT_IN_QUARRY,
    eTextSections_TEXT_IN_QUARRYPILE,
    eTextSections_TEXT_IN_HEALERS,
    eTextSections_TEXT_IN_ENGINEERS_GUILD,
    eTextSections_TEXT_IN_TUNNELLERS_GUILD,
    eTextSections_TEXT_IN_TRADEPOST,
    eTextSections_TEXT_IN_WELL,
    eTextSections_TEXT_IN_OIL_SMELTER,
    eTextSections_TEXT_IN_SIEGE_TENT,
    eTextSections_TEXT_IN_WHEATFARM,
    eTextSections_TEXT_IN_HOPSFARM,
    eTextSections_TEXT_IN_APPLEFARM,
    eTextSections_TEXT_IN_CATTLEFARM,
    eTextSections_TEXT_IN_MILL,
    eTextSections_TEXT_IN_STABLES,
    eTextSections_TEXT_IN_CHURCH,
    eTextSections_TEXT_IN_GATEHOUSE,
    eTextSections_TEXT_IN_DRAWBRIDGE,
    eTextSections_TEXT_IN_POSTERN_GATE,
    eTextSections_TEXT_IN_TUNNEL_ENTERANCE,
    eTextSections_TEXT_IN_CAMP_FIRE,
    eTextSections_TEXT_IN_SIGNPOST,
    eTextSections_TEXT_IN_KILLING_PIT,
    eTextSections_TEXT_IN_CATAPULT,
    eTextSections_TEXT_IN_TREBUCHET,
    eTextSections_TEXT_IN_OUTPOST,
    eTextSections_TEXT_IN_TOWER,
    eTextSections_TEXT_IN_GALLOWS,
    eTextSections_TEXT_IN_STOCKS,
    eTextSections_TEXT_IN_WITCH_HOIST,
    eTextSections_TEXT_IN_MAYPOLE,
    eTextSections_TEXT_IN_TRAINING_GROUND = 71,
    eTextSections_TEXT_IN_GARDEN,
    eTextSections_TEXT_GAME_OPTIONS = 74,
    eTextSections_TEXT_HELP,
    eTextSections_TEXT_MULTIPLAYER_CONNECTION,
    eTextSections_TEXT_PANEL_FEEDBACK,
    eTextSections_TEXT_STRUCTURE_WAS,
    eTextSections_TEXT_XPLAY_WAITING_ROOM,
    eTextSections_TEXT_MISSION_BUTTONS,
    eTextSections_TEXT_OBJECTIVES,
    eTextSections_TEXT_REPORT_BUTTONS,
    eTextSections_TEXT_PLAYER_DESC,
    eTextSections_TEXT_PEASANT_NAMES,
    eTextSections_TEXT_PEASANT_SURNAMES,
    eTextSections_TEXT_UNIT_ACTIONS,
    eTextSections_TEXT_MARRIAGE,
    eTextSections_TEXT_CHIMP_NAMES,
    eTextSections_TEXT_CHIMP_COMMENT,
    eTextSections_TEXT_NEWMAP_TYPES_HELP = 92,
    eTextSections_TEXT_INSULTS,
    eTextSections_TEXT_PREVIEW,
    eTextSections_TEXT_TUTORIAL,
    eTextSections_TEXT_TUTORIAL_BUTTONS,
    eTextSections_TEXT_MAP_SCREEN,
    eTextSections_TEXT_MISSION1_STORY = 99,
    eTextSections_TEXT_MISSION1_BRIEFING,
    eTextSections_TEXT_MISSION1_OBJECTIVES,
    eTextSections_TEXT_MISSION1_HINTS,
    eTextSections_TEXT_MISSION2_STORY,
    eTextSections_TEXT_MISSION2_BRIEFING,
    eTextSections_TEXT_MISSION2_OBJECTIVES,
    eTextSections_TEXT_MISSION2_HINTS,
    eTextSections_TEXT_MISSION3_STORY,
    eTextSections_TEXT_MISSION3_BRIEFING,
    eTextSections_TEXT_MISSION3_OBJECTIVES,
    eTextSections_TEXT_MISSION3_HINTS,
    eTextSections_TEXT_MISSION4_STORY,
    eTextSections_TEXT_MISSION4_BRIEFING,
    eTextSections_TEXT_MISSION4_OBJECTIVES,
    eTextSections_TEXT_MISSION4_HINTS,
    eTextSections_TEXT_MISSION5_STORY,
    eTextSections_TEXT_MISSION5_BRIEFING,
    eTextSections_TEXT_MISSION5_OBJECTIVES,
    eTextSections_TEXT_MISSION5_HINTS,
    eTextSections_TEXT_MISSION6_STORY,
    eTextSections_TEXT_MISSION6_BRIEFING,
    eTextSections_TEXT_MISSION6_OBJECTIVES,
    eTextSections_TEXT_MISSION6_HINTS,
    eTextSections_TEXT_MISSION7_STORY,
    eTextSections_TEXT_MISSION7_BRIEFING,
    eTextSections_TEXT_MISSION7_OBJECTIVES,
    eTextSections_TEXT_MISSION7_HINTS,
    eTextSections_TEXT_MISSION8_STORY,
    eTextSections_TEXT_MISSION8_BRIEFING,
    eTextSections_TEXT_MISSION8_OBJECTIVES,
    eTextSections_TEXT_MISSION8_HINTS,
    eTextSections_TEXT_MISSION9_STORY,
    eTextSections_TEXT_MISSION9_BRIEFING,
    eTextSections_TEXT_MISSION9_OBJECTIVES,
    eTextSections_TEXT_MISSION9_HINTS,
    eTextSections_TEXT_MISSION10_STORY,
    eTextSections_TEXT_MISSION10_BRIEFING,
    eTextSections_TEXT_MISSION10_OBJECTIVES,
    eTextSections_TEXT_MISSION10_HINTS,
    eTextSections_TEXT_MISSION11_STORY,
    eTextSections_TEXT_MISSION11_BRIEFING,
    eTextSections_TEXT_MISSION11_OBJECTIVES,
    eTextSections_TEXT_MISSION11_HINTS,
    eTextSections_TEXT_MISSION12_STORY,
    eTextSections_TEXT_MISSION12_BRIEFING,
    eTextSections_TEXT_MISSION12_OBJECTIVES,
    eTextSections_TEXT_MISSION12_HINTS,
    eTextSections_TEXT_MISSION13_STORY,
    eTextSections_TEXT_MISSION13_BRIEFING,
    eTextSections_TEXT_MISSION13_OBJECTIVES,
    eTextSections_TEXT_MISSION13_HINTS,
    eTextSections_TEXT_MISSION14_STORY,
    eTextSections_TEXT_MISSION14_BRIEFING,
    eTextSections_TEXT_MISSION14_OBJECTIVES,
    eTextSections_TEXT_MISSION14_HINTS,
    eTextSections_TEXT_MISSION15_STORY,
    eTextSections_TEXT_MISSION15_BRIEFING,
    eTextSections_TEXT_MISSION15_OBJECTIVES,
    eTextSections_TEXT_MISSION15_HINTS,
    eTextSections_TEXT_MISSION16_STORY,
    eTextSections_TEXT_MISSION16_BRIEFING,
    eTextSections_TEXT_MISSION16_OBJECTIVES,
    eTextSections_TEXT_MISSION16_HINTS,
    eTextSections_TEXT_MISSION17_STORY,
    eTextSections_TEXT_MISSION17_BRIEFING,
    eTextSections_TEXT_MISSION17_OBJECTIVES,
    eTextSections_TEXT_MISSION17_HINTS,
    eTextSections_TEXT_MISSION18_STORY,
    eTextSections_TEXT_MISSION18_BRIEFING,
    eTextSections_TEXT_MISSION18_OBJECTIVES,
    eTextSections_TEXT_MISSION18_HINTS,
    eTextSections_TEXT_MISSION19_STORY,
    eTextSections_TEXT_MISSION19_BRIEFING,
    eTextSections_TEXT_MISSION19_OBJECTIVES,
    eTextSections_TEXT_MISSION19_HINTS,
    eTextSections_TEXT_MISSION20_STORY,
    eTextSections_TEXT_MISSION20_BRIEFING,
    eTextSections_TEXT_MISSION20_OBJECTIVES,
    eTextSections_TEXT_MISSION20_HINTS,
    eTextSections_TEXT_CAMPAIGN_INFO,
    eTextSections_TEXT_DEMO_BRIEFINGS = 189,
    eTextSections_TEXT_HINTS,
    eTextSections_TEXT_ECO1_HINTS,
    eTextSections_TEXT_ECO2_HINTS,
    eTextSections_TEXT_ECO3_HINTS,
    eTextSections_TEXT_ECO4_HINTS,
    eTextSections_TEXT_ECO5_HINTS,
    eTextSections_TEXT_ECO_MISSION_BRIEFINGS,
    eTextSections_TEXT_MISSION_NAMES,
    eTextSections_TEXT_PREATTACK,
    eTextSections_TEXT_SCENARIO,
    eTextSections_TEXT_TRADER_NAMES,
    eTextSections_TEXT_ACTION,
    eTextSections_TEXT_IN_CESS_PIT,
    eTextSections_TEXT_IN_BURNING_STAKE,
    eTextSections_TEXT_IN_GIBBET,
    eTextSections_TEXT_IN_DUNGEON,
    eTextSections_TEXT_IN_STRETCHING_RACK,
    eTextSections_TEXT_IN_FLOGGING_RACK,
    eTextSections_TEXT_IN_CHOPPING_BLOCK,
    eTextSections_TEXT_IN_DUNKING_STOOL,
    eTextSections_TEXT_IN_DOG_CAGE,
    eTextSections_TEXT_IN_STATUE,
    eTextSections_TEXT_IN_SHRINE,
    eTextSections_TEXT_IN_BEEHIVE,
    eTextSections_TEXT_IN_DANCING_BEAR,
    eTextSections_TEXT_IN_POND,
    eTextSections_TEXT_IN_BEAR_CAVE,
    eTextSections_TEXT_IN_WATERPOT,
    eTextSections_TEXT_IN_CATHEDRAL,
    eTextSections_TEXT_MAP_NAMES,
    eTextSections_TEXT_ALLIES = 224,
    eTextSections_TEXT_MP_RANK,
    eTextSections_TEXT_SCENARIO_OPP = 220,
    eTextSections_TEXT_SKIRMISH_SPEECH,
    eTextSections_TEXT_CUSTOM_HOOKS = 223,
    eTextSections_TEXT_NEW_PRE_ATTACK = 226,
    eTextSections_TEXT_CUSTOMISATION,
    eTextSections_TEXT_NEW_CTEXT,
    eTextSections_TEXT_MP_VERSION_CONTROL,
    eTextSections_TEXT_NEW_TEXT,
    eTextSections_TEXT_HOT_KEYS,
    eTextSections_TEXT_NEW_DEMO,
    eTextSections_TEXT_NEW_TEXT2,
    eTextSections_TEXT_SANDS_OF_TIME,
    eTextSections_TEXT_BUILDING_DESCRIPTIONS,
    eTextSections_TEXT_CREDITS,
    eTextSections_TEXT_SUBTITLES,
    eTextSections_TEXT_ECOBRIEFINGS,
    eTextSections_TEXT_OTHER,
    eTextSections_TEXT_SKIRMISH_MASTERS,
    eTextSections_TEXT_GAME_TYPE,
    eTextSections_TEXT_SKIRMISH_CHOOSE,
    eTextSections_TEXT_ROADMAP,
    eTextSections_TEXT_TRAIL_NAMES_CRU = 245,
    eTextSections_TEXT_EXTREME_DEMO = 247,
    eTextSections_TEXT_EXTREME_POWERS,
    eTextSections_TEXT_DEMO_GAMENAMES,
    eTextSections_TEXT_SKIRMISH_CHOOSE2,
    eTextSections_TEXT_SHC_STANDALONE,
    eTextSections_TEXT_SKIRMISH_MISC,
    eTextSections_TEXT_SKTRAIL_WIN,
    eTextSections_TEXT_ALLIES2,
    eTextSections_TEXT_SKMASTERS,
    eTextSections_TEXT_CHEATS,
    eTextSections_TEXT_MISC2,
    eTextSections_TEXT_ISLAMIC = 259,
    eTextSections_TEXT_COOP,
    eTextSections_TEXT_TROOP_HELP,
    eTextSections_TEXT_AI_LORD_HELP,
    eTextSections_TEXT_MISSION21_STORY = 270,
    eTextSections_TEXT_MISSION21_BRIEFING,
    eTextSections_TEXT_MISSION21_OBJECTIVES,
    eTextSections_TEXT_MISSION21_HINTS,
    eTextSections_TEXT_MISSION22_STORY,
    eTextSections_TEXT_MISSION22_BRIEFING,
    eTextSections_TEXT_MISSION22_OBJECTIVES,
    eTextSections_TEXT_MISSION22_HINTS,
    eTextSections_TEXT_MISSION23_STORY,
    eTextSections_TEXT_MISSION23_BRIEFING,
    eTextSections_TEXT_MISSION23_OBJECTIVES,
    eTextSections_TEXT_MISSION23_HINTS,
    eTextSections_TEXT_MISSION24_STORY,
    eTextSections_TEXT_MISSION24_BRIEFING,
    eTextSections_TEXT_MISSION24_OBJECTIVES,
    eTextSections_TEXT_MISSION24_HINTS,
    eTextSections_TEXT_MISSION25_STORY,
    eTextSections_TEXT_MISSION25_BRIEFING,
    eTextSections_TEXT_MISSION25_OBJECTIVES,
    eTextSections_TEXT_MISSION25_HINTS,
    eTextSections_TEXT_MISSION26_STORY,
    eTextSections_TEXT_MISSION26_BRIEFING,
    eTextSections_TEXT_MISSION26_OBJECTIVES,
    eTextSections_TEXT_MISSION26_HINTS,
    eTextSections_TEXT_MISSION27_STORY,
    eTextSections_TEXT_MISSION27_BRIEFING,
    eTextSections_TEXT_MISSION27_OBJECTIVES,
    eTextSections_TEXT_MISSION27_HINTS,
    eTextSections_TEXT_MISSION28_STORY,
    eTextSections_TEXT_MISSION28_BRIEFING,
    eTextSections_TEXT_MISSION28_OBJECTIVES,
    eTextSections_TEXT_MISSION28_HINTS,
    eTextSections_TEXT_MISSION29_STORY,
    eTextSections_TEXT_MISSION29_BRIEFING,
    eTextSections_TEXT_MISSION29_OBJECTIVES,
    eTextSections_TEXT_MISSION29_HINTS,
    eTextSections_TEXT_MISSION30_STORY,
    eTextSections_TEXT_MISSION30_BRIEFING,
    eTextSections_TEXT_MISSION30_OBJECTIVES,
    eTextSections_TEXT_MISSION30_HINTS,
    eTextSections_TEXT_MISSION31_STORY,
    eTextSections_TEXT_MISSION31_BRIEFING,
    eTextSections_TEXT_MISSION31_OBJECTIVES,
    eTextSections_TEXT_MISSION31_HINTS,
    eTextSections_TEXT_MISSION32_STORY,
    eTextSections_TEXT_MISSION32_BRIEFING,
    eTextSections_TEXT_MISSION32_OBJECTIVES,
    eTextSections_TEXT_MISSION32_HINTS,
    eTextSections_TEXT_MISSION33_STORY,
    eTextSections_TEXT_MISSION33_BRIEFING,
    eTextSections_TEXT_MISSION33_OBJECTIVES,
    eTextSections_TEXT_MISSION33_HINTS,
    eTextSections_TEXT_MISSION34_STORY,
    eTextSections_TEXT_MISSION34_BRIEFING,
    eTextSections_TEXT_MISSION34_OBJECTIVES,
    eTextSections_TEXT_MISSION34_HINTS,
    eTextSections_TEXT_MISSION35_STORY,
    eTextSections_TEXT_MISSION35_BRIEFING,
    eTextSections_TEXT_MISSION35_OBJECTIVES,
    eTextSections_TEXT_MISSION35_HINTS
} eTextSections;


typedef enum eTextValues
{
    eTextValues_TEXT_SCN_MESSAGE_LIBRARY = 1,
    eTextValues_TEXT_SCN_SCENARIO_EDITOR,
    eTextValues_TEXT_SCN_EXIT,
    eTextValues_TEXT_SCN_HELP,
    eTextValues_TEXT_SCN_TITLES,
    eTextValues_TEXT_SCN_BRIEFINGS,
    eTextValues_TEXT_SCN_ADVISER_PROMPTS,
    eTextValues_TEXT_SCN_CHARACTERS,
    eTextValues_TEXT_SCN_RAT,
    eTextValues_TEXT_SCN_SNAKE,
    eTextValues_TEXT_SCN_PIG,
    eTextValues_TEXT_SCN_WOLF,
    eTextValues_TEXT_SCN_TAUNT,
    eTextValues_TEXT_SCN_ANGER,
    eTextValues_TEXT_SCN_MOOD3,
    eTextValues_TEXT_SCN_MOOD4,
    eTextValues_TEXT_SCN_NEW_MESSAGE,
    eTextValues_TEXT_SCN_SELECT,
    eTextValues_TEXT_SCN_DELETE,
    eTextValues_TEXT_SCN_RELOAD,
    eTextValues_TEXT_SCN_SAVE,
    eTextValues_TEXT_SCN_SAVEEXIT,
    eTextValues_TEXT_SCN_EVENTS,
    eTextValues_TEXT_SCN_CIVIL,
    eTextValues_TEXT_SCN_MILITARY,
    eTextValues_TEXT_SCN_NARRATIVE,
    eTextValues_TEXT_SCN_MAX_TITLE_LENGTH,
    eTextValues_TEXT_SCN_PLAYWAV,
    eTextValues_TEXT_SCN_PLAYBINK,
    eTextValues_TEXT_SCN_NEW,
    eTextValues_TEXT_SCN_LOAD,
    eTextValues_TEXT_SCN_NAME,
    eTextValues_TEXT_SCN_MAPFILE,
    eTextValues_TEXT_SCN_TITLE,
    eTextValues_TEXT_SCN_BRIEFING,
    eTextValues_TEXT_SCN_CANCEL,
    eTextValues_TEXT_SCN_OK,
    eTextValues_TEXT_SCN_TRADER,
    eTextValues_TEXT_SCN_STARTDATE,
    eTextValues_TEXT_SCN_SELECT_MESSAGE,
    eTextValues_TEXT_SCN_START_GOODS,
    eTextValues_TEXT_SCN_NEW_INVASION,
    eTextValues_TEXT_SCN_NEW_EVENTS,
    eTextValues_TEXT_SCN_EDIT,
    eTextValues_TEXT_SCN_LOAD_MAPFILE,
    eTextValues_TEXT_SCN_INVASION,
    eTextValues_TEXT_SCN_MESSAGE,
    eTextValues_TEXT_SCN_EVENT,
    eTextValues_TEXT_SCN_LOAD_SCN,
    eTextValues_TEXT_SCN_WOOD_PLANKS,
    eTextValues_TEXT_SCN_HOPS,
    eTextValues_TEXT_SCN_STONE_BLOCKS,
    eTextValues_TEXT_SCN_IRON_INGOTS,
    eTextValues_TEXT_SCN_PITCH,
    eTextValues_TEXT_SCN_WHEAT,
    eTextValues_TEXT_SCN_BREAD,
    eTextValues_TEXT_SCN_CHEESE,
    eTextValues_TEXT_SCN_MEAT,
    eTextValues_TEXT_SCN_FRUIT,
    eTextValues_TEXT_SCN_ALE,
    eTextValues_TEXT_SCN_GOLD,
    eTextValues_TEXT_SCN_BOWS,
    eTextValues_TEXT_SCN_CROSSBOWS,
    eTextValues_TEXT_SCN_SPEARS,
    eTextValues_TEXT_SCN_PIKES,
    eTextValues_TEXT_SCN_MACES,
    eTextValues_TEXT_SCN_SWORDS,
    eTextValues_TEXT_SCN_LEATHER_ARMOUR,
    eTextValues_TEXT_SCN_METAL_ARMOUR,
    eTextValues_TEXT_SCN_ARCHER,
    eTextValues_TEXT_SCN_XBOWMAN,
    eTextValues_TEXT_SCN_SPEARMAN,
    eTextValues_TEXT_SCN_PIKEMAN,
    eTextValues_TEXT_SCN_MACEMAN,
    eTextValues_TEXT_SCN_SWORDSMAN,
    eTextValues_TEXT_SCN_KNIGHT,
    eTextValues_TEXT_SCN_LADDERMAN,
    eTextValues_TEXT_SCN_ENGINEER,
    eTextValues_TEXT_SCN_POPULARITY,
    eTextValues_TEXT_SCN_BUY,
    eTextValues_TEXT_SCN_SELL,
    eTextValues_TEXT_SCN_WEEKSOFFMAP,
    eTextValues_TEXT_SCN_WEEKSATMARKET,
    eTextValues_TEXT_SCN_AVAILABLE,
    eTextValues_TEXT_SCN_NOTAVAILABLE,
    eTextValues_TEXT_SCN_SAVE_ALL,
    eTextValues_TEXT_SCN_OTHER,
    eTextValues_TEXT_SCN_TYPE,
    eTextValues_TEXT_SCN_FROM,
    eTextValues_TEXT_SCN_SIZE,
    eTextValues_TEXT_SCN_ARCHERS,
    eTextValues_TEXT_SCN_XBOWMEN,
    eTextValues_TEXT_SCN_SPEARMEN,
    eTextValues_TEXT_SCN_PIKEMEN,
    eTextValues_TEXT_SCN_MACEMEN,
    eTextValues_TEXT_SCN_SWORDSMEN,
    eTextValues_TEXT_SCN_KNIGHTS,
    eTextValues_TEXT_SCN_LADDERMEN,
    eTextValues_TEXT_SCN_ENGINEERS,
    eTextValues_TEXT_SCN_EDIT_CONDITIONS,
    eTextValues_TEXT_SCN_EDIT_ACTIONS,
    eTextValues_TEXT_SCN_EVENT_CONDITIONS,
    eTextValues_TEXT_SCN_EVENT_ACTIONS,
    eTextValues_TEXT_SCN_ANY_OF_THESE,
    eTextValues_TEXT_SCN_ALL_OF_THESE,
    eTextValues_TEXT_SCN_EVENT_CONDITION0,
    eTextValues_TEXT_SCN_EVENT_CONDITION1,
    eTextValues_TEXT_SCN_EVENT_CONDITION2,
    eTextValues_TEXT_SCN_EVENT_CONDITION3,
    eTextValues_TEXT_SCN_EVENT_CONDITION4,
    eTextValues_TEXT_SCN_EVENT_CONDITION5,
    eTextValues_TEXT_SCN_EVENT_CONDITION6,
    eTextValues_TEXT_SCN_EVENT_CONDITION7,
    eTextValues_TEXT_SCN_EVENT_CONDITION8,
    eTextValues_TEXT_SCN_EVENT_CONDITION9,
    eTextValues_TEXT_SCN_EVENT_CONDITION10,
    eTextValues_TEXT_SCN_EVENT_CONDITION11,
    eTextValues_TEXT_SCN_EVENT_CONDITION12,
    eTextValues_TEXT_SCN_EVENT_CONDITION13,
    eTextValues_TEXT_SCN_EVENT_CONDITION14,
    eTextValues_TEXT_SCN_EVENT_CONDITION15,
    eTextValues_TEXT_SCN_EVENT_CONDITION16,
    eTextValues_TEXT_SCN_EVENT_CONDITION17,
    eTextValues_TEXT_SCN_EVENT_CONDITION18,
    eTextValues_TEXT_SCN_EVENT_CONDITION19,
    eTextValues_TEXT_SCN_ACTIVE,
    eTextValues_TEXT_SCN_INACTIVE,
    eTextValues_TEXT_SCN_ACTION1,
    eTextValues_TEXT_SCN_ACTION2,
    eTextValues_TEXT_SCN_ACTION3,
    eTextValues_TEXT_SCN_ACTION4,
    eTextValues_TEXT_SCN_ACTION5,
    eTextValues_TEXT_SCN_ACTION6,
    eTextValues_TEXT_SCN_ACTION7,
    eTextValues_TEXT_SCN_ACTION8,
    eTextValues_TEXT_SCN_ACTION9,
    eTextValues_TEXT_SCN_ACTION10,
    eTextValues_TEXT_SCN_ACTION11,
    eTextValues_TEXT_SCN_ACTION12,
    eTextValues_TEXT_SCN_ACTION13,
    eTextValues_TEXT_SCN_ACTION14,
    eTextValues_TEXT_SCN_ACTION15,
    eTextValues_TEXT_SCN_ACTION16,
    eTextValues_TEXT_SCN_ACTION17,
    eTextValues_TEXT_SCN_ACTION18,
    eTextValues_TEXT_SCN_ACTION19,
    eTextValues_TEXT_SCN_ACTION20,
    eTextValues_TEXT_SCN_ACTION21,
    eTextValues_TEXT_SCN_ACTION22,
    eTextValues_TEXT_SCN_ACTION23,
    eTextValues_TEXT_SCN_ACTION24,
    eTextValues_TEXT_SCN_ACTION25,
    eTextValues_TEXT_SCN_ACTION26,
    eTextValues_TEXT_SCN_ACTION27,
    eTextValues_TEXT_SCN_ACTION28,
    eTextValues_TEXT_SCN_ANY,
    eTextValues_TEXT_SCN_CATAPULT,
    eTextValues_TEXT_SCN_TREBUCHET,
    eTextValues_TEXT_SCN_BATTERINGRAM,
    eTextValues_TEXT_SCN_SIEGETOWER,
    eTextValues_TEXT_SCN_PORTABLESHIELD,
    eTextValues_TEXT_SCN_MONKS,
    eTextValues_TEXT_SCN_REPEAT,
    eTextValues_TEXT_SCN_ATTACKING_FORCE,
    eTextValues_TEXT_SCN_TOTAL,
    eTextValues_TEXT_SCN_RATIONING,
    eTextValues_TEXT_SCN_TAXRATE,
    eTextValues_TEXT_SCN_STARTING_GOLD,
    eTextValues_TEXT_SCN_STARTING_PITCH,
    eTextValues_TEXT_SCN_TUNNELERS,
    eTextValues_TEXT_SCN_BUILDING_AVAILABILITY,
    eTextValues_TEXT_SCN_ON,
    eTextValues_TEXT_SCN_OFF,
    eTextValues_TEXT_SCN_RANDOMEVENTS,
    eTextValues_TEXT_SCN_REPEAT_MONTHS,
    eTextValues_TEXT_SCN_REPEAT_COUNT,
    eTextValues_TEXT_SCN_ACTION29,
    eTextValues_TEXT_SCN_ACTION30,
    eTextValues_TEXT_SCN_ACTION31,
    eTextValues_TEXT_SCN_ACTION32,
    eTextValues_TEXT_SCN_ACTION33,
    eTextValues_TEXT_SCN_ACTION34,
    eTextValues_TEXT_SCN_ACTION35,
    eTextValues_TEXT_SCN_ACTION36,
    eTextValues_TEXT_SCN_ACTION37,
    eTextValues_TEXT_SCN_ACTION38,
    eTextValues_TEXT_SCN_ACTION39,
    eTextValues_TEXT_SCN_TROOPS,
    eTextValues_TEXT_SCN_WEAPONS,
    eTextValues_TEXT_SCN_EVENT_CONDITION20,
    eTextValues_TEXT_SCN_EVENT_CONDITION21,
    eTextValues_TEXT_SCN_EVENT_CONDITION22,
    eTextValues_TEXT_SCN_EVENT_CONDITION23,
    eTextValues_TEXT_SCN_EVENT_CONDITION24,
    eTextValues_TEXT_SCN_EVENT_CONDITION25,
    eTextValues_TEXT_SCN_EVENT_CONDITION26,
    eTextValues_TEXT_SCN_EVENT_CONDITION27,
    eTextValues_TEXT_SCN_EVENT_CONDITION28,
    eTextValues_TEXT_SCN_EVENT_CONDITION29,
    eTextValues_TEXT_SCN_EVENT_CONDITION30,
    eTextValues_TEXT_SCN_EVENT_CONDITION31,
    eTextValues_TEXT_SCN_EVENT_CONDITION32,
    eTextValues_TEXT_SCN_EVENT_CONDITION33,
    eTextValues_TEXT_SCN_EVENT_CONDITION34,
    eTextValues_TEXT_SCN_EVENT_CONDITION35,
    eTextValues_TEXT_SCN_EVENT_CONDITION36,
    eTextValues_TEXT_SCN_EVENT_CONDITION37,
    eTextValues_TEXT_SCN_EVENT_CONDITION38,
    eTextValues_TEXT_SCN_EVENT_CONDITION39,
    eTextValues_TEXT_SCN_MONTH,
    eTextValues_TEXT_SCN_MONTHS,
    eTextValues_TEXT_SCN_INVADE_NOW,
    eTextValues_TEXT_SCN_ALL,
    eTextValues_TEXT_SCN_KILL_ALL_ENEMY_LORDS,
    eTextValues_TEXT_SCN_ARAB_ARCHER,
    eTextValues_TEXT_SCN_ARAB_SLAVE,
    eTextValues_TEXT_SCN_ARAB_SLINGER,
    eTextValues_TEXT_SCN_ARAB_ASSASIN,
    eTextValues_TEXT_SCN_ARAB_HORSEARCHER,
    eTextValues_TEXT_SCN_ARAB_SWORDSMAN,
    eTextValues_TEXT_SCN_ARAB_GRENADIER,
    eTextValues_TEXT_SCN_ARAB_BALLISTA,
    eTextValues_TEXT_SCN_CRUSADER,
    eTextValues_TEXT_SCN_ARABIC,
    eTextValues_FEEDBACK_NULL = 0,
    eTextValues_FEEDBACK_IRON_NEEDED,
    eTextValues_FEEDBACK_PITCH_NEEDED,
    eTextValues_FEEDBACK_GOLD_NEEDED,
    eTextValues_FEEDBACK_WOOD_AND_STONE_NEEDED,
    eTextValues_FEEDBACK_WOOD_NEEDED,
    eTextValues_FEEDBACK_STONE_NEEDED,
    eTextValues_FEEDBACK_NO_FOOD_DISTRIBUTED,
    eTextValues_FEEDBACK_REVOLUTION,
    eTextValues_FEEDBACK_WOODCUTTERS_REVOLT,
    eTextValues_FEEDBACK_PLACEKEEP,
    eTextValues_FEEDBACK_PLACEGRANARY,
    eTextValues_TEXT_MAIN_STARTGAME = 1,
    eTextValues_TEXT_MAIN_MULTIGAME,
    eTextValues_TEXT_MAIN_GAMESPYARCADE,
    eTextValues_TEXT_MAIN_LOADGAME,
    eTextValues_TEXT_MAIN_EXITSTRONGHOLD,
    eTextValues_TEXT_MAIN_NEWIDENTITY,
    eTextValues_TEXT_MAIN_WELCOME,
    eTextValues_TEXT_MAIN_OK,
    eTextValues_TEXT_MAIN_START_CAMPAIGN,
    eTextValues_TEXT_MAIN_START_BUILDER,
    eTextValues_TEXT_MAIN_START_MAP,
    eTextValues_TEXT_MAIN_MAINOPTIONS,
    eTextValues_TEXT_MAIN_START_MISSION,
    eTextValues_TEXT_MAIN_QUIT_GAME,
    eTextValues_TEXT_MAIN_MISSION,
    eTextValues_TEXT_VICTORY,
    eTextValues_TEXT_DEFEAT,
    eTextValues_TEXT_MAIN_BACK,
    eTextValues_TEXT_MAIN_EASY,
    eTextValues_TEXT_MAIN_NORMAL,
    eTextValues_TEXT_MAIN_HARD,
    eTextValues_TEXT_MAIN_VERYHARD,
    eTextValues_TEXT_STARTING_MISSION,
    eTextValues_TEXT_DIFFICULTY,
    eTextValues_TEXT_MAIN_CHANGETITLE,
    eTextValues_TEXT_MAIN_BUTTON1,
    eTextValues_TEXT_MAIN_BUTTON2,
    eTextValues_TEXT_MAIN_BUTTON3,
    eTextValues_TEXT_MAIN_BUTTON4,
    eTextValues_TEXT_MAIN_BUTTON5,
    eTextValues_TEXT_MAIN_BUTTONX1,
    eTextValues_TEXT_MAIN_BUTTONX2,
    eTextValues_TEXT_MAIN_QUITX,
    eTextValues_BHELP_TEXT_CASTLE_BUILDINGS = 1,
    eTextValues_BHELP_TEXT_INDUSTRY_BUILDINGS,
    eTextValues_BHELP_TEXT_OTHER_BUILDINGS,
    eTextValues_BHELP_TEXT_REPORTS,
    eTextValues_BHELP_TEXT_TOWERS,
    eTextValues_BHELP_TEXT_GATEHOUSES,
    eTextValues_BHELP_TEXT_KEEPS,
    eTextValues_BHELP_TEXT_ARMOURY,
    eTextValues_BHELP_TEXT_BARRACKS_STONE,
    eTextValues_BHELP_TEXT_BARRACKS_WOOD,
    eTextValues_BHELP_TEXT_STABLES,
    eTextValues_BHELP_TEXT_STONE_WALLS,
    eTextValues_BHELP_TEXT_CRENAL,
    eTextValues_BHELP_TEXT_STAIRS,
    eTextValues_BHELP_TEXT_WOODEN_WALLS,
    eTextValues_BHELP_TEXT_BRAZIER,
    eTextValues_BHELP_TEXT_KILLINGS_PITS,
    eTextValues_BHELP_TEXT_SIEGE_TENT_1,
    eTextValues_BHELP_TEXT_SIEGE_TENT_2,
    eTextValues_BHELP_TEXT_SIEGE_TENT_3,
    eTextValues_BHELP_TEXT_TOWER_A,
    eTextValues_BHELP_TEXT_TOWER_B,
    eTextValues_BHELP_TEXT_TOWER_C,
    eTextValues_BHELP_TEXT_TOWER_D,
    eTextValues_BHELP_TEXT_TOWER_E,
    eTextValues_BHELP_TEXT_MOATS,
    eTextValues_BHELP_TEXT_GATEHOUSE_A,
    eTextValues_BHELP_TEXT_GATEHOUSE_B,
    eTextValues_BHELP_TEXT_GATEHOUSE_C,
    eTextValues_BHELP_TEXT_DRAWBRIDGE,
    eTextValues_BHELP_TEXT_MOAT,
    eTextValues_BHELP_TEXT_KEEPS_A,
    eTextValues_BHELP_TEXT_KEEPS_B,
    eTextValues_BHELP_TEXT_KEEPS_C,
    eTextValues_BHELP_TEXT_KEEPS_D,
    eTextValues_BHELP_TEXT_KEEPS_E,
    eTextValues_BHELP_TEXT_FARMS,
    eTextValues_BHELP_TEXT_WORKSHOPS,
    eTextValues_BHELP_TEXT_HOVEL,
    eTextValues_BHELP_TEXT_OUTPOST_BEDOUIN,
    eTextValues_BHELP_TEXT_QUARRY,
    eTextValues_BHELP_TEXT_WOODCUTTER,
    eTextValues_BHELP_TEXT_IRON_MINE,
    eTextValues_BHELP_TEXT_PITCH_DUGOUT,
    eTextValues_BHELP_TEXT_STOCKPILE,
    eTextValues_BHELP_TEXT_GRANARY,
    eTextValues_BHELP_TEXT_WELL,
    eTextValues_BHELP_TEXT_MILL,
    eTextValues_BHELP_TEXT_TRADEPOST,
    eTextValues_BHELP_TEXT_OX_TETHER,
    eTextValues_BHELP_TEXT_BLACKSMITHS_WORKSHOP,
    eTextValues_BHELP_TEXT_ARMOURERS_WORKSHOP,
    eTextValues_BHELP_TEXT_TANNERS_WORKSHOP,
    eTextValues_BHELP_TEXT_FLETCHERS_WORKSHOP,
    eTextValues_BHELP_TEXT_POLETURNERS_WORKSHOP,
    eTextValues_BHELP_TEXT_BAKERY,
    eTextValues_BHELP_TEXT_BREWERY,
    eTextValues_BHELP_TEXT_WHEAT_FARM,
    eTextValues_BHELP_TEXT_APPLE_ORCHARD,
    eTextValues_BHELP_TEXT_HOPS_FARM,
    eTextValues_BHELP_TEXT_CATTLE_FARM,
    eTextValues_BHELP_TEXT_HUNTERS_LODGE,
    eTextValues_BHELP_TEXT_CHURCHS,
    eTextValues_BHELP_TEXT_INN,
    eTextValues_BHELP_TEXT_HEALERS,
    eTextValues_BHELP_TEXT_ENGINEERS_GUILD,
    eTextValues_BHELP_TEXT_TUNNELLERS_GUILD,
    eTextValues_BHELP_TEXT_LATRINES,
    eTextValues_BHELP_TEXT_BARRED_WINDOWS,
    eTextValues_BHELP_TEXT_PARAPETS,
    eTextValues_BHELP_TEXT_GALLOWS,
    eTextValues_BHELP_TEXT_MAYPOLE,
    eTextValues_BHELP_TEXT_ARCHERY_TARGETS,
    eTextValues_BHELP_TEXT_TROOP_TARGETS,
    eTextValues_BHELP_TEXT_STOCKS,
    eTextValues_BHELP_TEXT_SPEARMAN,
    eTextValues_BHELP_TEXT_PIKEMAN,
    eTextValues_BHELP_TEXT_ARCHER,
    eTextValues_BHELP_TEXT_CROSSBOWMAN,
    eTextValues_BHELP_TEXT_MACEMAN,
    eTextValues_BHELP_TEXT_SWORDSMAN,
    eTextValues_BHELP_TEXT_KNIGHT,
    eTextValues_BHELP_TEXT_SPEAR,
    eTextValues_BHELP_TEXT_PIKE,
    eTextValues_BHELP_TEXT_BOW,
    eTextValues_BHELP_TEXT_CROSSBOW,
    eTextValues_BHELP_TEXT_MACE,
    eTextValues_BHELP_TEXT_SWORD,
    eTextValues_BHELP_TEXT_LEATHER_ARMOUR,
    eTextValues_BHELP_TEXT_METAL_ARMOUR,
    eTextValues_BHELP_TEXT_GATEHOUSE_D,
    eTextValues_BHELP_TEXT_ANTIMOAT,
    eTextValues_BHELP_TEXT_CASTLE_DECORATIONS,
    eTextValues_BHELP_TEXT_DWELLINGS,
    eTextValues_BHELP_TEXT_FLAG_1,
    eTextValues_BHELP_TEXT_FLAG_2,
    eTextValues_BHELP_TEXT_FLAG_3,
    eTextValues_BHELP_TEXT_CREST,
    eTextValues_BHELP_TEXT_CREST_2,
    eTextValues_BHELP_TEXT_BANNER,
    eTextValues_BHELP_TEXT_WOODEN_CHURCH,
    eTextValues_BHELP_TEXT_STONE_CHURCH,
    eTextValues_BHELP_TEXT_CATHEDRAL,
    eTextValues_BHELP_TEXT_SHRUB_GARDEN,
    eTextValues_BHELP_TEXT_VEGETABLE_GARDEN,
    eTextValues_BHELP_TEXT_TOWN_GARDEN,
    eTextValues_BHELP_TEXT_LARGE_GARDEN,
    eTextValues_BHELP_TEXT_COMMUNIAL_GARDEN,
    eTextValues_BHELP_TEXT_FINGER_PRESS,
    eTextValues_BHELP_TEXT_THUMB_SCREW,
    eTextValues_BHELP_TEXT_STAKE,
    eTextValues_BHELP_TEXT_FLOGGING_HORSE,
    eTextValues_BHELP_TEXT_TRAVELLING_FAIR,
    eTextValues_BHELP_TEXT_JOUSTING,
    eTextValues_BHELP_TEXT_DUNKING_STOOL,
    eTextValues_BHELP_TEXT_GARDEN_6,
    eTextValues_BHELP_TEXT_GARDEN_7,
    eTextValues_BHELP_TEXT_GARDEN_8,
    eTextValues_BHELP_TEXT_GARDEN_9,
    eTextValues_BHELP_TEXT_GARDEN_10,
    eTextValues_BHELP_TEXT_GARDEN_11,
    eTextValues_BHELP_TEXT_GARDEN_12,
    eTextValues_BHELP_TEXT_MILITARY_BUILDINGS,
    eTextValues_BHELP_TEXT_GARDENS,
    eTextValues_BHELP_TEXT_PUNISHMENTS,
    eTextValues_BHELP_TEXT_AMUSEMENTS,
    eTextValues_BHELP_TEXT_FOOD_BUILDINGS,
    eTextValues_BHELP_TEXT_OPTIONS,
    eTextValues_BHELP_TEXT_BACKTOCASTLES,
    eTextValues_BHELP_TEXT_BACKTOINDUSTRY,
    eTextValues_BHELP_TEXT_BACKTOTOWN,
    eTextValues_BHELP_TEXT_BACKTOFOOD,
    eTextValues_BHELP_TEXT_MANGONEL,
    eTextValues_BHELP_TEXT_CATAPULT,
    eTextValues_BHELP_TEXT_TREBUCHET,
    eTextValues_BHELP_TEXT_SIEGE_TOWER,
    eTextValues_BHELP_TEXT_BATTERING_RAM,
    eTextValues_BHELP_TEXT_PORTABLE_SHIELD,
    eTextValues_BHELP_TEXT_OIL_SMELTER,
    eTextValues_BHELP_TEXT_OUTPOST_ARAB,
    eTextValues_BHELP_TEXT_OUTPOST,
    eTextValues_BHELP_TEXT_GAME,
    eTextValues_BHELP_TEXT_BRUSH_SIZE,
    eTextValues_BHELP_TEXT_SNAP,
    eTextValues_BHELP_TEXT_DELETE,
    eTextValues_BHELP_TEXT_HEIGHT_MODE,
    eTextValues_BHELP_TEXT_LAND_MODE,
    eTextValues_BHELP_TEXT_VEG_MODE,
    eTextValues_BHELP_TEXT_ANIMAL_MODE,
    eTextValues_BHELP_TEXT_WATER_MODE,
    eTextValues_BHELP_TEXT_FEATURE_MODE,
    eTextValues_BHELP_TEXT_RAISE,
    eTextValues_BHELP_TEXT_LOWER,
    eTextValues_BHELP_TEXT_MIN,
    eTextValues_BHELP_TEXT_MAX,
    eTextValues_BHELP_TEXT_EQUALIZE,
    eTextValues_BHELP_TEXT_MOUNTAIN,
    eTextValues_BHELP_TEXT_HILL,
    eTextValues_BHELP_TEXT_MID_PLAIN,
    eTextValues_BHELP_TEXT_HI_PLAIN,
    eTextValues_BHELP_TEXT_LAND,
    eTextValues_BHELP_TEXT_GRASS,
    eTextValues_BHELP_TEXT_ROCKS,
    eTextValues_BHELP_TEXT_PEBBLES,
    eTextValues_BHELP_TEXT_BOULDERS,
    eTextValues_BHELP_TEXT_IRON,
    eTextValues_BHELP_TEXT_DIRT,
    eTextValues_BHELP_TEXT_STONES,
    eTextValues_BHELP_TEXT_CHESTNUT,
    eTextValues_BHELP_TEXT_OAK,
    eTextValues_BHELP_TEXT_PINE,
    eTextValues_BHELP_TEXT_BIRCH,
    eTextValues_BHELP_TEXT_SHRUB,
    eTextValues_BHELP_TEXT_DEER,
    eTextValues_BHELP_TEXT_WOLF,
    eTextValues_BHELP_TEXT_RABBIT,
    eTextValues_BHELP_TEXT_BEAR,
    eTextValues_BHELP_TEXT_SEAGULL,
    eTextValues_BHELP_TEXT_CROW,
    eTextValues_BHELP_TEXT_SEA,
    eTextValues_BHELP_TEXT_SHALLOW,
    eTextValues_BHELP_TEXT_BEACH,
    eTextValues_BHELP_TEXT_MARSH,
    eTextValues_BHELP_TEXT_OIL,
    eTextValues_BHELP_TEXT_RIVER,
    eTextValues_BHELP_TEXT_FORD,
    eTextValues_BHELP_TEXT_FOAM,
    eTextValues_BHELP_TEXT_RIPPLE,
    eTextValues_BHELP_TEXT_BIGROCK1,
    eTextValues_BHELP_TEXT_BIGROCK2,
    eTextValues_BHELP_TEXT_BIGROCK3,
    eTextValues_BHELP_TEXT_BIGROCK4,
    eTextValues_BHELP_TEXT_BIGROCK5,
    eTextValues_BHELP_TEXT_BIGROCK = 189,
    eTextValues_BHELP_TEXT_ROCKSIZE,
    eTextValues_BHELP_TEXT_ROCKTYPE,
    eTextValues_BHELP_TEXT_ROCKDIR,
    eTextValues_BHELP_TEXT_SIGNPOST = 194,
    eTextValues_BHELP_TEXT_ESTUARY,
    eTextValues_BHELP_TEXT_UNDO,
    eTextValues_BHELP_TEXT_BALLISTA,
    eTextValues_BHELP_TEXT_POUR_OIL,
    eTextValues_BHELP_TEXT_PATROL,
    eTextValues_BHELP_TEXT_DISBAND,
    eTextValues_BHELP_TEXT_TUNNELHERE,
    eTextValues_BHELP_TEXT_ATTACKHERE,
    eTextValues_BHELP_TEXT_LAUNCH_COW,
    eTextValues_BHELP_TEXT_PITCH_DITCH,
    eTextValues_BHELP_TEXT_MP_KEEP1,
    eTextValues_BHELP_TEXT_MP_KEEP2,
    eTextValues_BHELP_TEXT_MP_KEEP3,
    eTextValues_BHELP_TEXT_MP_KEEP4,
    eTextValues_BHELP_TEXT_MP_KEEP5,
    eTextValues_BHELP_TEXT_MP_KEEP6,
    eTextValues_BHELP_TEXT_MP_KEEP7,
    eTextValues_BHELP_TEXT_MP_KEEP8,
    eTextValues_BHELP_TEXT_FOODPROCESS_BUILDINGS,
    eTextValues_BHELP_TEXT_WEAPONS_BUILDINGS,
    eTextValues_BHELP_TEXT_FLAG_4,
    eTextValues_BHELP_TEXT_REPORTS1,
    eTextValues_BHELP_TEXT_REPORTS2,
    eTextValues_BHELP_TEXT_REPORTS3,
    eTextValues_BHELP_TEXT_REPORTS4,
    eTextValues_BHELP_TEXT_REPORTS5,
    eTextValues_BHELP_TEXT_REPORTS6,
    eTextValues_BHELP_TEXT_REPORTS7,
    eTextValues_BHELP_TEXT_REPORTS8,
    eTextValues_BHELP_TEXT_HORSE,
    eTextValues_BHELP_TEXT_BACK_TO_BUILDER,
    eTextValues_BHELP_TEXT_FRONTEND_SHIELD1,
    eTextValues_BHELP_TEXT_FRONTEND_SHIELD2,
    eTextValues_BHELP_TEXT_FRONTEND_SHIELD3,
    eTextValues_BHELP_TEXT_FRONTEND_SHIELD4,
    eTextValues_BHELP_TEXT_FRONTEND_QUIT,
    eTextValues_BHELP_TEXT_RUINS1,
    eTextValues_BHELP_TEXT_PEOPLE,
    eTextValues_BHELP_TEXT_RUINS,
    eTextValues_BHELP_TEXT_SELECT_SPEARMEN,
    eTextValues_BHELP_TEXT_SELECT_ARCHERS,
    eTextValues_BHELP_TEXT_SELECT_ENGINEERS,
    eTextValues_BHELP_TEXT_SELECT_PIKEMEN,
    eTextValues_BHELP_TEXT_SELECT_MACEMEN,
    eTextValues_BHELP_TEXT_SELECT_SWORDSMEN,
    eTextValues_BHELP_TEXT_SELECT_XBOWMEN,
    eTextValues_BHELP_TEXT_SELECT_KNIGHTS,
    eTextValues_BHELP_TEXT_SELECT_MONKS,
    eTextValues_BHELP_TEXT_SELECT_LADDERMEN,
    eTextValues_BHELP_TEXT_ARCHERS,
    eTextValues_BHELP_TEXT_SPEARMEN,
    eTextValues_BHELP_TEXT_PIKEMEN,
    eTextValues_BHELP_TEXT_MACEMEN,
    eTextValues_BHELP_TEXT_XBOWMEN,
    eTextValues_BHELP_TEXT_SWORDSMEN,
    eTextValues_BHELP_TEXT_KNIGHTS,
    eTextValues_BHELP_TEXT_LADDERMEN,
    eTextValues_BHELP_TEXT_ENGINEERS,
    eTextValues_BHELP_TEXT_ENGINEERS_POTS,
    eTextValues_BHELP_TEXT_MONKS,
    eTextValues_BHELP_TEXT_TUNNELERS,
    eTextValues_BHELP_TEXT_SELECT_TUNNELERS,
    eTextValues_BHELP_TEXT_REPAIR,
    eTextValues_BHELP_TEXT_FRONTEND_COMBAT_SHIELD1,
    eTextValues_BHELP_TEXT_FRONTEND_COMBAT_SHIELD2,
    eTextValues_BHELP_TEXT_FRONTEND_COMBAT_SHIELD3,
    eTextValues_BHELP_TEXT_FRONTEND_COMBAT_SHIELD4,
    eTextValues_BHELP_TEXT_FRONTEND_ECONOMICS_SHIELD1,
    eTextValues_BHELP_TEXT_FRONTEND_ECONOMICS_SHIELD2,
    eTextValues_BHELP_TEXT_FRONTEND_ECONOMICS_SHIELD3,
    eTextValues_BHELP_TEXT_FRONTEND_ECONOMICS_SHIELD4,
    eTextValues_BHELP_TEXT_FRONTEND_BUILDER_SHIELD1,
    eTextValues_BHELP_TEXT_FRONTEND_BUILDER_SHIELD2,
    eTextValues_BHELP_TEXT_FRONTEND_BUILDER_SHIELD3,
    eTextValues_BHELP_TEXT_FRONTEND_BUILDER_SHIELD4,
    eTextValues_BHELP_TEXT_FRONTEND_BACK,
    eTextValues_BHELP_TEXT_CESSPIT,
    eTextValues_BHELP_TEXT_BURNING_STAKE,
    eTextValues_BHELP_TEXT_GIBBET,
    eTextValues_BHELP_TEXT_DUNGEON,
    eTextValues_BHELP_TEXT_STRETCHING_RACK,
    eTextValues_BHELP_TEXT_FLOGGING_RACK,
    eTextValues_BHELP_TEXT_CHOPPING_BLOCK,
    eTextValues_BHELP_TEXT_DUNKING_STOOL2,
    eTextValues_BHELP_TEXT_DOG_CAGE,
    eTextValues_BHELP_TEXT_STATUE,
    eTextValues_BHELP_TEXT_SHRINE,
    eTextValues_BHELP_TEXT_BEE_HIVE,
    eTextValues_BHELP_TEXT_DANCING_BEAR,
    eTextValues_BHELP_TEXT_POND,
    eTextValues_BHELP_TEXT_BEAR_CAVE,
    eTextValues_BHELP_TEXT_FRONTEND_TUTORIAL,
    eTextValues_BHELP_TEXT_STANCE_STAND,
    eTextValues_BHELP_TEXT_STANCE_DEFENSIVE,
    eTextValues_BHELP_TEXT_STANCE_AGGRESSIVE,
    eTextValues_BHELP_TEXT_STOP,
    eTextValues_BHELP_TEXT_BUILD,
    eTextValues_BHELP_TEXT_BUILD_BACK,
    eTextValues_BHELP_TEXT_SELECT_CATAPULTS,
    eTextValues_BHELP_TEXT_SELECT_TREBUCHETS,
    eTextValues_BHELP_TEXT_SELECT_BATTERINGRAMS,
    eTextValues_BHELP_TEXT_SELECT_SIEGETOWERS,
    eTextValues_BHELP_TEXT_SELECT_PORTABLESHIELDS,
    eTextValues_BHELP_TEXT_SELECT_MANGONELS,
    eTextValues_BHELP_TEXT_SELECT_BALLISTAS,
    eTextValues_BHELP_TEXT_POND_LARGE,
    eTextValues_BHELP_TEXT_FRONTEND_CREDITS,
    eTextValues_BHELP_TEXT_BRIEF_STARTGAME,
    eTextValues_BHELP_TEXT_BRIEF_TUTORIAL,
    eTextValues_BHELP_TEXT_BRIEF_HINTS,
    eTextValues_BHELP_TEXT_BRIEF_OBJECTIVES,
    eTextValues_BHELP_TEXT_BRIEF_REPLAYSTORY,
    eTextValues_BHELP_TEXT_NO_MAP_EDITOR,
    eTextValues_BHELP_TEXT_AMMO,
    eTextValues_BHELP_TEXT_HEADS_ON_SPIKES,
    eTextValues_BHELP_TEXT_FE_SECTION_1,
    eTextValues_BHELP_TEXT_FE_SECTION_2,
    eTextValues_BHELP_TEXT_FE_SECTION_3,
    eTextValues_BHELP_TEXT_FE_SECTION_4,
    eTextValues_BHELP_TEXT_PEOPLE2,
    eTextValues_BHELP_TEXT_ARAB_BOW,
    eTextValues_BHELP_TEXT_ARAB_SLAVE,
    eTextValues_BHELP_TEXT_ARAB_SLINGER,
    eTextValues_BHELP_TEXT_ARAB_ASSASIN,
    eTextValues_BHELP_TEXT_ARAB_HORSEMAN,
    eTextValues_BHELP_TEXT_ARAB_SWORDSMAN,
    eTextValues_BHELP_TEXT_ARAB_GRENADIER,
    eTextValues_BHELP_TEXT_ARAB_BALLISTA,
    eTextValues_BHELP_TEXT_SELECT_LORD,
    eTextValues_BHELP_TEXT_SELECT_ARAB_BOW,
    eTextValues_BHELP_TEXT_SELECT_ARAB_SLAVE,
    eTextValues_BHELP_TEXT_SELECT_ARAB_SLINGER,
    eTextValues_BHELP_TEXT_SELECT_ARAB_ASSASIN,
    eTextValues_BHELP_TEXT_SELECT_ARAB_HORSEMAN,
    eTextValues_BHELP_TEXT_SELECT_ARAB_SWORDSMAN,
    eTextValues_BHELP_TEXT_SELECT_ARAB_GRENADIER,
    eTextValues_BHELP_TEXT_SELECT_ARAB_BALLISTA,
    eTextValues_BHELP_TEXT_WATERPOT,
    eTextValues_BHELP_TEXT_MAX_LIMIT,
    eTextValues_BHELP_TEXT_TEAM_OK,
    eTextValues_BHELP_TEXT_TEAM_CANCEL,
    eTextValues_BHELP_TEXT_TEAM_PLAYER,
    eTextValues_BHELP_TEXT_TEAM_LEAVE,
    eTextValues_BHELP_TEXT_ADD_CPU,
    eTextValues_BHELP_TEXT_SETUP_TEAMS,
    eTextValues_BHELP_TEXT_XP_KICK,
    eTextValues_BHELP_TEXT_ALLIES,
    eTextValues_BHELP_TEXT_POSITIONS,
    eTextValues_BHELP_TEXT_RANDOM_CPU,
    eTextValues_BHELP_TEXT_DIRECTX,
    eTextValues_BHELP_TEXT_FRONTEND_SHIELDX1,
    eTextValues_BHELP_TEXT_FRONTEND_SHIELDX2,
    eTextValues_BHELP_TEXT_FRONTEND_SHIELDX3,
    eTextValues_BHELP_TEXT_BEDOUIN_STOCKADE,
    eTextValues_BHELP_TEXT_BEDOUIN_CAMEL_LANCER,
    eTextValues_BHELP_TEXT_BEDOUIN_HEALER,
    eTextValues_BHELP_TEXT_BEDOUIN_EUNUCH,
    eTextValues_BHELP_TEXT_BEDOUIN_AMBUSHER,
    eTextValues_BHELP_TEXT_BEDOUIN_SKIRMISHER,
    eTextValues_BHELP_TEXT_BEDOUIN_HEAVY_CAMEL,
    eTextValues_BHELP_TEXT_BEDOUIN_SAPPER,
    eTextValues_BHELP_TEXT_BEDOUIN_DEMOLISHER,
    eTextValues_TEXT_WR_LOAD = 1,
    eTextValues_TEXT_WR_CANCEL,
    eTextValues_TEXT_WR_READY,
    eTextValues_TEXT_WR_LAUNCHGAME,
    eTextValues_TEXT_WR_WAITING,
    eTextValues_TEXT_WR_NOTREADY,
    eTextValues_TEXT_WR_PREVIEW,
    eTextValues_TEXT_WR_CLICKTO,
    eTextValues_TEXT_WR_TRADING,
    eTextValues_TEXT_WR_WEAPONS,
    eTextValues_TEXT_WR_FOOD,
    eTextValues_TEXT_WR_GOODS,
    eTextValues_TEXT_WR_RESOURCES,
    eTextValues_TEXT_WR_GOLD,
    eTextValues_TEXT_WR_POPULARITY,
    eTextValues_TEXT_WR_START_GOODS,
    eTextValues_TEXT_WR_NONE,
    eTextValues_TEXT_WR_LOW,
    eTextValues_TEXT_WR_MED,
    eTextValues_TEXT_WR_HIGH,
    eTextValues_TEXT_WR_CUSTOM,
    eTextValues_TEXT_WR_START_TROOPS,
    eTextValues_TEXT_WR_NONE2,
    eTextValues_TEXT_WR_FEW,
    eTextValues_TEXT_WR_SOME,
    eTextValues_TEXT_WR_MANY,
    eTextValues_TEXT_WR_CUSTOM2,
    eTextValues_TEXT_WR_PLAYERS,
    eTextValues_TEXT_WR_STATUS,
    eTextValues_TEXT_WR_TEAM,
    eTextValues_TEXT_WR_SAXONHALL,
    eTextValues_TEXT_WR_WOODENKEEP,
    eTextValues_TEXT_WR_STONEKEEP,
    eTextValues_TEXT_WR_FORTRESS,
    eTextValues_TEXT_WR_STRONGHOLD,
    eTextValues_TEXT_WR_WINCONDITION1,
    eTextValues_TEXT_WR_WINCONDITION2,
    eTextValues_TEXT_WR_WINCONDITION3,
    eTextValues_TEXT_WR_WINCONDITION4,
    eTextValues_TEXT_WR_WINCONDITION5,
    eTextValues_TEXT_WR_WINCONDITION6,
    eTextValues_TEXT_WR_WINCONDITION7,
    eTextValues_TEXT_WR_WINCONDITION8,
    eTextValues_TEXT_WR_WINCONDITION9,
    eTextValues_TEXT_WR_WINCONDITION10,
    eTextValues_TEXT_WR_LSAXONHALL,
    eTextValues_TEXT_WR_LWOODENKEEP,
    eTextValues_TEXT_WR_LSTONEKEEP,
    eTextValues_TEXT_WR_LFORTRESS,
    eTextValues_TEXT_WR_LSTRONGHOLD,
    eTextValues_TEXT_WR_LWINCONDITION1,
    eTextValues_TEXT_WR_LWINCONDITION2,
    eTextValues_TEXT_WR_LWINCONDITION3,
    eTextValues_TEXT_WR_LWINCONDITION4,
    eTextValues_TEXT_WR_LWINCONDITION5,
    eTextValues_TEXT_WR_LWINCONDITION6,
    eTextValues_TEXT_WR_LWINCONDITION7,
    eTextValues_TEXT_WR_LWINCONDITION8,
    eTextValues_TEXT_WR_LWINCONDITION9,
    eTextValues_TEXT_WR_LWINCONDITION10,
    eTextValues_TEXT_WR_NO,
    eTextValues_TEXT_WR_NAME,
    eTextValues_TEXT_MP_GAME_OVER,
    eTextValues_TEXT_MP_WINS,
    eTextValues_TEXT_SA_STARTGAME,
    eTextValues_TEXT_SA_JUSTBUILD,
    eTextValues_TEXT_SA_NONCOMBAT,
    eTextValues_TEXT_SA_COMBATSIEGE,
    eTextValues_TEXT_SA_LEVEL,
    eTextValues_TEXT_SA_EASY,
    eTextValues_TEXT_SA_NORMAL,
    eTextValues_TEXT_SA_HARD,
    eTextValues_TEXT_SA_VERYHARD,
    eTextValues_TEXT_SA_ADVANCED,
    eTextValues_TEXT_SA_POINTS,
    eTextValues_TEXT_SA_COMBATINVASION,
    eTextValues_TEXT_SA_ATTACK,
    eTextValues_TEXT_SA_DEFEND,
    eTextValues_TEXT_WR_EARLY,
    eTextValues_TEXT_WR_MIDDLE,
    eTextValues_TEXT_WR_LATE,
    eTextValues_TEXT_WR_GAMESPEED,
    eTextValues_TEXT_WR_FREETROOPS,
    eTextValues_TEXT_WR_GOLDTROOPS,
    eTextValues_TEXT_WR_TECHLEVEL,
    eTextValues_TEXT_WR_TROOPS,
    eTextValues_TEXT_WR_SET_READY_STATE,
    eTextValues_TEXT_WR_POINTS,
    eTextValues_TEXT_WR_LEFTGAME,
    eTextValues_TEXT_WR_KINGPOINTS,
    eTextValues_TEXT_WR_EJECT,
    eTextValues_TEXT_WR_YOUWEREEJECTED,
    eTextValues_TEXT_WR_CONNECTION_OPTIONS,
    eTextValues_TEXT_WR_AUTOSAVE,
    eTextValues_TEXT_WR_OFF,
    eTextValues_TEXT_WR_5MINS,
    eTextValues_TEXT_WR_10MINS,
    eTextValues_TEXT_WR_20MINS,
    eTextValues_TEXT_WR_WALLS,
    eTextValues_TEXT_WR_ALLIES,
    eTextValues_TEXT_WR_ANYTIME,
    eTextValues_TEXT_WR_STARTONLY,
    eTextValues_TEXT_WR_PLAYOPTIONS,
    eTextValues_TEXT_WR_NO_ALLIANCES,
    eTextValues_TEXT_WR_SENDMAP,
    eTextValues_TEXT_WR_SENDMAPTO,
    eTextValues_TEXT_WR_RECEIVINGMAP,
    eTextValues_TEXT_WR_PLEASEWAIT,
    eTextValues_TEXT_WR_FOGOFWAR,
    eTextValues_TEXT_WR_REVEALMAP,
    eTextValues_TEXT_WR_RAT1,
    eTextValues_TEXT_WR_RAT2,
    eTextValues_TEXT_WR_RAT3,
    eTextValues_TEXT_WR_RAT4,
    eTextValues_TEXT_WR_RAT5,
    eTextValues_TEXT_WR_RAT6,
    eTextValues_TEXT_WR_RAT7,
    eTextValues_TEXT_WR_RAT8,
    eTextValues_TEXT_WR_SNAKE1,
    eTextValues_TEXT_WR_SNAKE2,
    eTextValues_TEXT_WR_SNAKE3,
    eTextValues_TEXT_WR_SNAKE4,
    eTextValues_TEXT_WR_SNAKE5,
    eTextValues_TEXT_WR_SNAKE6,
    eTextValues_TEXT_WR_SNAKE7,
    eTextValues_TEXT_WR_SNAKE8,
    eTextValues_TEXT_WR_PIG1,
    eTextValues_TEXT_WR_PIG2,
    eTextValues_TEXT_WR_PIG3,
    eTextValues_TEXT_WR_PIG4,
    eTextValues_TEXT_WR_PIG5,
    eTextValues_TEXT_WR_PIG6,
    eTextValues_TEXT_WR_PIG7,
    eTextValues_TEXT_WR_PIG8,
    eTextValues_TEXT_WR_WOLF1,
    eTextValues_TEXT_WR_WOLF2,
    eTextValues_TEXT_WR_WOLF3,
    eTextValues_TEXT_WR_WOLF4 = 118,
    eTextValues_TEXT_WR_WOLF5 = 139,
    eTextValues_TEXT_WR_WOLF6,
    eTextValues_TEXT_WR_WOLF7,
    eTextValues_TEXT_WR_WOLF8,
    eTextValues_TEXT_WR_AI5,
    eTextValues_TEXT_WR_AI6 = 151,
    eTextValues_TEXT_WR_AI7 = 159,
    eTextValues_TEXT_WR_AI8 = 167,
    eTextValues_TEXT_WR_AI9 = 175,
    eTextValues_TEXT_WR_AI10 = 183,
    eTextValues_TEXT_WR_AI11 = 191,
    eTextValues_TEXT_WR_AI12 = 199,
    eTextValues_TEXT_WR_AI13 = 207,
    eTextValues_TEXT_WR_AI14 = 215,
    eTextValues_TEXT_WR_AI15 = 223,
    eTextValues_TEXT_WR_AI16 = 231,
    eTextValues_TEXT_WR_AI_TYPE1 = 239,
    eTextValues_TEXT_WR_AI_NAME1,
    eTextValues_TEXT_WR_AI_TYPE2 = 248,
    eTextValues_TEXT_WR_AI_NAME2,
    eTextValues_TEXT_WR_AI_TYPE3 = 257,
    eTextValues_TEXT_WR_AI_NAME3,
    eTextValues_TEXT_WR_AI_TYPE4 = 266,
    eTextValues_TEXT_WR_AI_NAME4,
    eTextValues_TEXT_WR_AI_TYPE5 = 275,
    eTextValues_TEXT_WR_AI_NAME5,
    eTextValues_TEXT_WR_AI_TYPE6 = 284,
    eTextValues_TEXT_WR_AI_NAME6,
    eTextValues_TEXT_WR_AI_TYPE7 = 293,
    eTextValues_TEXT_WR_AI_NAME7,
    eTextValues_TEXT_WR_AI_TYPE8 = 302,
    eTextValues_TEXT_WR_AI_NAME8,
    eTextValues_TEXT_WR_AI_TYPE9 = 311,
    eTextValues_TEXT_WR_AI_NAME9,
    eTextValues_TEXT_WR_AI_TYPE10 = 320,
    eTextValues_TEXT_WR_AI_NAME10,
    eTextValues_TEXT_WR_AI_TYPE11 = 329,
    eTextValues_TEXT_WR_AI_NAME11,
    eTextValues_TEXT_WR_AI_TYPE12 = 338,
    eTextValues_TEXT_WR_AI_NAME12,
    eTextValues_TEXT_WR_AI_TYPE13 = 347,
    eTextValues_TEXT_WR_AI_NAME13,
    eTextValues_TEXT_WR_AI_TYPE14 = 356,
    eTextValues_TEXT_WR_AI_NAME14,
    eTextValues_TEXT_WR_AI_TYPE15 = 365,
    eTextValues_TEXT_WR_AI_NAME15,
    eTextValues_TEXT_WR_AI_TYPE16 = 374,
    eTextValues_TEXT_WR_AI_NAME16,
    eTextValues_TEXT_WR_AI_NAME = 383,
    eTextValues_TEXT_WR_AI_DESC,
    eTextValues_TEXT_WR_AI_DESC1,
    eTextValues_TEXT_WR_CHOOSE_OPP = 401,
    eTextValues_TEXT_WR_HUMAN,
    eTextValues_TEXT_WR_THIS_YOU,
    eTextValues_TEXT_WR_HUMAN_OPP,
    eTextValues_TEXT_WR_CPU_OPP,
    eTextValues_TEXT_WR_IN_TEAM1,
    eTextValues_TEXT_WR_IN_TEAM2,
    eTextValues_TEXT_WR_IN_TEAM3,
    eTextValues_TEXT_WR_IN_TEAM4,
    eTextValues_TEXT_WR_BALANCED_ABBR,
    eTextValues_TEXT_WR_BALANCED,
    eTextValues_TEXT_WR_UNBALANCED,
    eTextValues_TEXT_WR_SKIRMISH_MASTERS,
    eTextValues_TEXT_ALLY_TITLE = 0,
    eTextValues_TEXT_ALLY_GOODS_REQ,
    eTextValues_TEXT_ALLY_ORDERS,
    eTextValues_TEXT_ALLY_NONE,
    eTextValues_TEXT_ALLY_HELP_NEED,
    eTextValues_TEXT_ALLY_SET_ORDERS,
    eTextValues_TEXT_ALLY_SEND_GOODS,
    eTextValues_TEXT_ALLY_REQ_GOODS,
    eTextValues_TEXT_ALLY_NO_ORDER,
    eTextValues_TEXT_ALLY_DEFEND_ME,
    eTextValues_TEXT_ALLY_ATTACK,
    eTextValues_TEXT_ALLY_AMOUNT,
    eTextValues_TEXT_ALLY_REQUESTS,
    eTextValues_TEXT_ALLY_CHOOSE_GOODS,
    eTextValues_TEXT_ALLY_CHOOSE_AMOUNT,
    eTextValues_TEXT_ALLY_SEND_REQUESTED,
    eTextValues_TEXT_ALLY_ORDERSLABEL,
    eTextValues_TEXT_ALLY_DEFEND,
    eTextValues_TEXT_ALLY_YOU,
    eTextValues_TEXT_ALLY_NO_ALLIES,
    eTextValues_TEXT_MP_REQUEST_DEFEND = 70,
    eTextValues_TEXT_MP_REQUEST_ATTACK,
    eTextValues_TEXT_MP_REQUEST_GOODS,
    eTextValues_TEXT_MP_ONLYHAVE1ALLY = 47,
    eTextValues_TEXT_MP_ALLIANCENOTPOSSIBLE,
    eTextValues_TEXT_MP_CONNECTION_LOST,
    eTextValues_TEXT_MP_SENDINGGAMESTATE,
    eTextValues_TEXT_MP_RECEIVINGGAMESTATE,
    eTextValues_TEXT_MP_CANCELGAME,
    eTextValues_TEXT_MP_AUTOSAVEOFF,
    eTextValues_TEXT_MP_AUTOSAVEON,
    eTextValues_TEXT_MP_LASTSAVED,
    eTextValues_TEXT_MP_MINUTES,
    eTextValues_TEXT_MP_OFFERS_ALLIANCE,
    eTextValues_TEXT_MP_KILLGAME,
    eTextValues_TEXT_MAP_TITLE1 = 0,
    eTextValues_TEXT_MAP_TITLE2,
    eTextValues_TEXT_MAP_LOAD,
    eTextValues_TEXT_MAP_SAVE,
    eTextValues_TEXT_MAP_NEW,
    eTextValues_TEXT_MAP_BACK,
    eTextValues_TEXT_MAP_TOMAP,
    eTextValues_TEXT_MAPNEW_BACK,
    eTextValues_TEXT_MAPNEW_TOMAP,
    eTextValues_TEXT_MAP_EDIT,
    eTextValues_TEXT_MAP_NEXT,
    eTextValues_TEXT_MAP_DONE,
    eTextValues_TEXT_MAP_SINGLE_PLAYER,
    eTextValues_TEXT_MAP_MULTI_PLAYER,
    eTextValues_TEXT_MAP_SAXON_HALL,
    eTextValues_TEXT_MAP_WOODEN_KEEP,
    eTextValues_TEXT_MAP_STONE_KEEP,
    eTextValues_TEXT_MAP_FORTRESS,
    eTextValues_TEXT_MAP_STRONGHOLD,
    eTextValues_TEXT_MAP_SELECT_GAMETYPE,
    eTextValues_TEXT_MAP_SELECT_NEWMAPSIZE,
    eTextValues_TEXT_MAP_SELECT_KEEPTYPES,
    eTextValues_TEXT_MAP_NO_MAP_AVAILABLE,
    eTextValues_TEXT_MAP_NO_MAP_AVAILABLE2,
    eTextValues_TEXT_MAP_UNTITLED,
    eTextValues_TEXT_MAP_INVASIONS,
    eTextValues_TEXT_MAP_NEW_INVASION,
    eTextValues_TEXT_MAP_GAME_TYPE,
    eTextValues_TEXT_MAP_SIEGE,
    eTextValues_TEXT_MAP_INVASION,
    eTextValues_TEXT_MAP_ECONOMIC,
    eTextValues_TEXT_MAP_PLAYMAP,
    eTextValues_TEXT_MAP_MESSAGE,
    eTextValues_TEXT_MAP_JUSTBUILD,
    eTextValues_TEXT_MAP_SIEGE_THAT_BUILDER,
    eTextValues_TEXT_MAP_SAVE_WORKING_MAP,
    eTextValues_TEXT_MAP_SAVE_FINAL_MAP,
    eTextValues_TEXT_MAP_SIEGE_THAT_EXPLANATION,
    eTextValues_TEXT_MAP_BALANCED = 45,
    eTextValues_TEXT_MAP_UNBALANCED,
    eTextValues_TEXT_NEW_PEOPLE = 16,
    eTextValues_TEXT_NEW_STRUCTS,
    eTextValues_TEXT_NEW_TREES,
    eTextValues_TEXT_NEW_ROCKS,
    eTextValues_TEXT_NEW_TRIBES,
    eTextValues_TEXT_WR_JEWEL = 16,
    eTextValues_TEXT_WR_SENTINEL = 24,
    eTextValues_TEXT_WR_NOMAD = 32,
    eTextValues_TEXT_WR_KAHIN = 40,
    eTextValues_TEXT_WR_CANARY = 48,
    eTextValues_TEXT_WR_TRADER = 56,
    eTextValues_TEXT_WR_SERGEANT = 64,
    eTextValues_TEXT_WR_LIONESS = 72,
    eTextValues_TEXT_WR_CROCODILE = 80,
    eTextValues_TEXT_WR_AI_TYPE17 = 88,
    eTextValues_TEXT_WR_AI_NAME17,
    eTextValues_TEXT_WR_AI_TYPE18 = 97,
    eTextValues_TEXT_WR_AI_NAME18,
    eTextValues_TEXT_WR_AI_TYPE19 = 106,
    eTextValues_TEXT_WR_AI_NAME19,
    eTextValues_TEXT_WR_AI_TYPE20 = 115,
    eTextValues_TEXT_WR_AI_NAME20,
    eTextValues_TEXT_WR_AI_TYPE21 = 124,
    eTextValues_TEXT_WR_AI_NAME21,
    eTextValues_TEXT_WR_AI_TYPE22 = 133,
    eTextValues_TEXT_WR_AI_NAME22,
    eTextValues_TEXT_WR_AI_TYPE23 = 142,
    eTextValues_TEXT_WR_AI_NAME23,
    eTextValues_TEXT_WR_AI_TYPE24 = 151,
    eTextValues_TEXT_WR_AI_NAME24,
    eTextValues_TEXT_WR_AI_TYPE25 = 160,
    eTextValues_TEXT_WR_AI_NAME25,
    eTextValues_TEXT_WR_AI_DESC17 = 169,
    eTextValues_TEXT_WR_AI_TYPE26 = 453,
    eTextValues_TEXT_WR_AI_NAME26,
    eTextValues_TEXT_WR_AI_NICK26 = 462,
    eTextValues_TEXT_WR_AI_TYPE27 = 470,
    eTextValues_TEXT_WR_AI_NAME27,
    eTextValues_TEXT_WR_AI_NICK27 = 479,
    eTextValues_TEXT_WR_AI_TYPE28 = 487,
    eTextValues_TEXT_WR_AI_NAME28,
    eTextValues_TEXT_WR_AI_NICK28 = 496,
    eTextValues_TEXT_WR_AI_TYPE29 = 504,
    eTextValues_TEXT_WR_AI_NAME29,
    eTextValues_TEXT_WR_AI_NICK29 = 513,
    eTextValues_TEXT_WR_AI_DESC26 = 521,
    eTextValues_TEXT_SANDS_TRAIL_1 = 7,
    eTextValues_TEXT_SANDS_TRAIL_2 = 12,
    eTextValues_TEXT_SANDS_TRAIL_3 = 19,
    eTextValues_TEXT_SANDS_TRAIL_4 = 28,
    eTextValues_TEXT_SANDS_TRAIL_5 = 39,
    eTextValues_TEXT_SANDS_TRAIL_6 = 49,
    eTextValues_TEXT_SANDS_TRAIL_7 = 82,
    eTextValues_TEXT_SANDS_TRAIL_8 = 92
} eTextValues;


typedef enum Owner
{
    Owner_NONE_Owner,
    Owner_NEUTRAL,
    Owner_PLAYER,
    Owner_PIG,
    Owner_RAT,
    Owner_SNAKE,
    Owner_WOLF
} Owner;


typedef enum UIMode
{
    UIMode_BLANK,
    UIMode_CHOOSEBUILDING,
    UIMode_MAPEDITOR,
    UIMode_TROOPSSELECTED,
    UIMode_INBUILDING
} UIMode;


typedef enum AppModes
{
    AppModes_APP_MODE_MANAGER = 10,
    AppModes_APP_MODE_EDITOR,
    AppModes_APP_MODE_TEST_MAP,
    AppModes_APP_MODE_MAIN_GAME = 14,
    AppModes_APP_MODE_IN_BUILDING = 16,
    AppModes_APP_MODE_EDIT_MAP,
    AppModes_APP_MODE_NEW_MAP,
    AppModes_APP_MODE_XPLAY_CONNECT,
    AppModes_APP_MODE_XPLAY_WAITING_ROOM,
    AppModes_APP_MODE_XPLAY_CONNECT_GAMESPY,
    AppModes_APP_MODE_HELP,
    AppModes_APP_MODE_MAIN_OPTIONS,
    AppModes_APP_MODE_1PLAYER_OPTIONS,
    AppModes_APP_MODE_NEW_CAMPAIGN_LEVEL,
    AppModes_APP_MODE_NARRATIVE,
    AppModes_APP_MODE_MAP_SCREEN,
    AppModes_APP_MODE_BRIEFING,
    AppModes_APP_MODE_WIN_MISSION,
    AppModes_APP_MODE_FAIL_MISSION,
    AppModes_APP_MODE_NEW_MAP2,
    AppModes_APP_MODE_NEW_MAP3,
    AppModes_APP_MODE_XPLAY_WAIT_FOR_SYNC,
    AppModes_APP_MODE_STAND_ALONE_OPTIONS = 35,
    AppModes_APP_MODE_EDIT_INVASIONS,
    AppModes_APP_MODE_NEW_MAP4,
    AppModes_APP_MODE_DIFFICULTY_LEVEL,
    AppModes_APP_MODE_ECO_DIFFICULTY_LEVEL,
    AppModes_APP_MODE_NEW_FRONTEND1,
    AppModes_APP_MODE_NEW_FRONTEND2,
    AppModes_APP_MODE_NEW_FRONTEND_COMBAT,
    AppModes_APP_MODE_NEW_FRONTEND_ECONOMICS,
    AppModes_APP_MODE_NEW_FRONTEND_BUILDER,
    AppModes_APP_MODE_CREDITS,
    AppModes_APP_MODE_NET_DEMO_SCREEN,
    AppModes_APP_MODE_SIEGE_THAT,
    AppModes_APP_MODE_INTRO_BINK,
    AppModes_APP_MODE_DUMMY,
    AppModes_APP_MODE_MISSION_START1,
    AppModes_APP_MODE_MISSION_START2,
    AppModes_APP_MODE_CAMPAIGN_START,
    AppModes_APP_MODE_CAMPAIGN_END,
    AppModes_APP_MODE_SKIRMISH_OPP,
    AppModes_APP_MODE_SKIRMISH_CHOICE,
    AppModes_APP_MODE_SKIRMISH_BRIEF,
    AppModes_APP_MODE_SKIRMISH_TRAIL,
    AppModes_APP_MODE_SKIRMISH_MASTERS,
    AppModes_APP_MODE_NOT_SEEING,
    AppModes_APP_MODE_ASK_FOR_DISC,
    AppModes_APP_MODE_XPLAY_FAKE,
    AppModes_APP_MODE_SKTRAIL_WIN,
    AppModes_APP_MODE_SCN_SCENARIO_EDITOR = 1002
} AppModes;


typedef enum GameModes
{
    GameModes_SIEGE = 2,
    GameModes_INVASION,
    GameModes_ECO = 1,
    GameModes_BUILD = 0,
    GameModes_MAP_EDITOR = 10
} GameModes;


typedef enum SubModes
{
    SubModes_SUB_MODE_CASTLE = 10,
    SubModes_SUB_MODE_TOWERS,
    SubModes_SUB_MODE_GATEHOUSES,
    SubModes_SUB_MODE_KEEPS,
    SubModes_SUB_MODE_MILITARY,
    SubModes_SUB_MODE_CASTLE_DECORATIONS,
    SubModes_SUB_MODE_MOATS,
    SubModes_SUB_MODE_GATEDIRC_WOOD,
    SubModes_SUB_MODE_GATEDIRC_STONE1,
    SubModes_SUB_MODE_GATEDIRC_STONE2,
    SubModes_SUB_MODE_INDUSTRY,
    SubModes_SUB_MODE_WORKSHOPS,
    SubModes_SUB_MODE_FOODPROCESS = 25,
    SubModes_SUB_MODE_PEOPLE,
    SubModes_SUB_MODE_RUINS,
    SubModes_SUB_MODE_WEAPONS,
    SubModes_SUB_MODE_PEOPLE2,
    SubModes_SUB_MODE_TOWN,
    SubModes_SUB_MODE_CHURCHS,
    SubModes_SUB_MODE_GARDENS,
    SubModes_SUB_MODE_PUNISHMENTS,
    SubModes_SUB_MODE_AMUSEMENTS,
    SubModes_SUB_MODE_FOOD = 40,
    SubModes_SUB_MODE_FARMS,
    SubModes_SUB_MODE_BLANK = 48,
    SubModes_SUB_MODE_KEEPONLY,
    SubModes_SUB_MODE_PEOPLE3,
    SubModes_SUB_MODE_PEOPLE4,
    SubModes_SUB_MODE_FULLMAP = 60,
    SubModes_SUB_MODE_TROOP_INSTRUCTIONS,
    SubModes_SUB_MODE_TROOP_INSTRUCTIONS_FULLMAP
} SubModes;


typedef enum ForcedAppModes
{
    ForcedAppModes_none_ForcedAppModes,
    ForcedAppModes_keeps,
    ForcedAppModes_granary,
    ForcedAppModes_castle,
    ForcedAppModes_blank_ForcedAppModes,
    ForcedAppModes_refresh_current
} ForcedAppModes;


typedef enum InBuildingModes
{
    InBuildingModes_INSIDE_NULL,
    InBuildingModes_INSIDE_BARRACKS,
    InBuildingModes_INSIDE_KEEP,
    InBuildingModes_INSIDE_INN,
    InBuildingModes_INSIDE_GRANARY,
    InBuildingModes_INSIDE_HOUSE,
    InBuildingModes_INSIDE_WOODCUTTERS_HUT,
    InBuildingModes_INSIDE_OXEN_BASE,
    InBuildingModes_INSIDE_IRON_MINE,
    InBuildingModes_INSIDE_PITCH_DIGGER,
    InBuildingModes_INSIDE_HUNTERS_HUT,
    InBuildingModes_INSIDE_GOODS_YARD,
    InBuildingModes_INSIDE_ARMOURY,
    InBuildingModes_INSIDE_FLETCHERS_WORKSHOP,
    InBuildingModes_INSIDE_BLACKSMITHS_WORKSHOP,
    InBuildingModes_INSIDE_POLETURNERS_WORKSHOP,
    InBuildingModes_INSIDE_ARMOURERS_WORKSHOP,
    InBuildingModes_INSIDE_TANNERS_WORKSHOP,
    InBuildingModes_INSIDE_BAKERS_WORKSHOP,
    InBuildingModes_INSIDE_BREWERS_WORKSHOP,
    InBuildingModes_INSIDE_QUARRY,
    InBuildingModes_INSIDE_QUARRYPILE,
    InBuildingModes_INSIDE_HEALERS,
    InBuildingModes_INSIDE_ENGINEERS_GUILD,
    InBuildingModes_INSIDE_TUNNELLERS_GUILD,
    InBuildingModes_INSIDE_TRADEPOST,
    InBuildingModes_INSIDE_WELL,
    InBuildingModes_INSIDE_OIL_SMELTER,
    InBuildingModes_INSIDE_SIEGE_TENT,
    InBuildingModes_INSIDE_WHEATFARM,
    InBuildingModes_INSIDE_HOPSFARM,
    InBuildingModes_INSIDE_APPLEFARM,
    InBuildingModes_INSIDE_CATTLEFARM,
    InBuildingModes_INSIDE_MILL,
    InBuildingModes_INSIDE_STABLES,
    InBuildingModes_INSIDE_CHURCH,
    InBuildingModes_INSIDE_GATEHOUSE,
    InBuildingModes_INSIDE_DRAWBRIDGE,
    InBuildingModes_INSIDE_POSTERN_GATE,
    InBuildingModes_INSIDE_TUNNEL_ENTERANCE,
    InBuildingModes_INSIDE_WATERPOT,
    InBuildingModes_INSIDE_SIGNPOST,
    InBuildingModes_INSIDE_KILLING_PIT,
    InBuildingModes_INSIDE_CAMPGROUND,
    InBuildingModes_INSIDE_MERCPOST,
    InBuildingModes_INSIDE_OUTPOST,
    InBuildingModes_INSIDE_TOWER,
    InBuildingModes_INSIDE_GALLOWS,
    InBuildingModes_INSIDE_STOCKS,
    InBuildingModes_INSIDE_WITCH_HOIST,
    InBuildingModes_INSIDE_MAYPOLE,
    InBuildingModes_INSIDE_GARDEN,
    InBuildingModes_INSIDE_PARADEGROUND,
    InBuildingModes_INSIDE_TRADEPOST_PRICES,
    InBuildingModes_INSIDE_TRADEPOST_FOOD,
    InBuildingModes_INSIDE_TRADEPOST_BULK,
    InBuildingModes_INSIDE_TRADEPOST_ARMS,
    InBuildingModes_INSIDE_TRADEPOST_DO_THE_TRADE,
    InBuildingModes_INSIDE_CATAPULT,
    InBuildingModes_INSIDE_TREBUCHET,
    InBuildingModes_INSIDE_SIEGE_TOWER,
    InBuildingModes_INSIDE_BATTERING_RAM,
    InBuildingModes_INSIDE_PORTABLE_SHIELD,
    InBuildingModes_INSIDE_BEDOUIN_STOCKADE,
    InBuildingModes_INSIDE_PEACETIME,
    InBuildingModes_SUB_MODE_REPORTS_ARMY4 = 66,
    InBuildingModes_SUB_MODE_REPORTS_ARMY3,
    InBuildingModes_SUB_MODE_REPORTS_ARMY2,
    InBuildingModes_SUB_MODE_REPORTS_EVENTS,
    InBuildingModes_INSIDE_CHIMP,
    InBuildingModes_SUB_MODE_REPORTS,
    InBuildingModes_SUB_MODE_REPORTS_POPULARITY,
    InBuildingModes_SUB_MODE_REPORTS_FEARFACTOR,
    InBuildingModes_SUB_MODE_REPORTS_POPULATION,
    InBuildingModes_SUB_MODE_REPORTS_FOOD,
    InBuildingModes_SUB_MODE_REPORTS_ARMY,
    InBuildingModes_SUB_MODE_REPORTS_STORES,
    InBuildingModes_SUB_MODE_REPORTS_WEAPONS,
    InBuildingModes_SUB_MODE_REPORTS_RELIGION,
    InBuildingModes_INSIDE_CESS_PIT,
    InBuildingModes_INSIDE_BURNING_STAKE,
    InBuildingModes_INSIDE_GIBBET,
    InBuildingModes_INSIDE_DUNGEON,
    InBuildingModes_INSIDE_STRETCHING_RACK,
    InBuildingModes_INSIDE_FLOGGING_RACK,
    InBuildingModes_INSIDE_CHOPPING_BLOCK,
    InBuildingModes_INSIDE_DUNKING_STOOL,
    InBuildingModes_INSIDE_DOG_CAGE,
    InBuildingModes_INSIDE_STATUE,
    InBuildingModes_INSIDE_SHRINE,
    InBuildingModes_INSIDE_BEEHIVE,
    InBuildingModes_INSIDE_DANCING_BEAR,
    InBuildingModes_INSIDE_POND,
    InBuildingModes_INSIDE_BEAR_CAVE,
    InBuildingModes_INSIDE_ARAB_BALLISTA,
    InBuildingModes_INSIDE_CATHEDRAL
} InBuildingModes;


typedef enum eGameTypeModes
{
    eGameTypeModes_GAMETYPE_CAMPAIGN,
    eGameTypeModes_GAMETYPE_BUILDER,
    eGameTypeModes_GAMETYPE_MAP,
    eGameTypeModes_GAMETYPE_MULTIPLAYER,
    eGameTypeModes_GAMETYPE_TUTORIAL,
    eGameTypeModes_GAMETYPE_SIEGE_THAT_BUILDER = 6,
    eGameTypeModes_GAMETYPE_MAP_TRAIL = 11,
    eGameTypeModes_GAMETYPE_MAP_TRAIL2 = 13
} eGameTypeModes;


typedef enum Goods
{
    Goods_STORED_NULL,
    Goods_STORED_WOOD_LOGS,
    Goods_STORED_WOOD_PLANKS,
    Goods_STORED_RAW_HOPS,
    Goods_STORED_STONE_BLOCKS,
    Goods_STORED_COW_HIDES,
    Goods_STORED_IRON_INGOTS,
    Goods_STORED_PITCH_RAW,
    Goods_STORED_PITCH_REFINED,
    Goods_STORED_RAW_WHEAT,
    Goods_STORED_FOOD_BREAD,
    Goods_STORED_FOOD_CHEESE,
    Goods_STORED_FOOD_MEAT,
    Goods_STORED_FOOD_FRUIT,
    Goods_STORED_FOOD_ALE,
    Goods_STORED_GOLD,
    Goods_STORED_FLOUR,
    Goods_STORED_BOWS,
    Goods_STORED_CROSSBOWS,
    Goods_STORED_SPEARS,
    Goods_STORED_PIKES,
    Goods_STORED_MACES,
    Goods_STORED_SWORDS,
    Goods_STORED_LEATHER_ARMOUR,
    Goods_STORED_METAL_ARMOUR,
    Goods_Count
} Goods;


typedef enum Troops
{
    Troops_TROOP_NULL,
    Troops_TROOP_ARCHER,
    Troops_TROOP_SPEARMAN,
    Troops_TROOP_MACEMAN,
    Troops_TROOP_XBOWMAN,
    Troops_TROOP_PIKEMAN,
    Troops_TROOP_SWORDSMAN,
    Troops_TROOP_KNIGHT,
    Troops_TROOP_ENGINEER,
    Troops_TROOP_MONK,
    Troops_TROOP_LADDERMAN,
    Troops_TROOP_MANTLET,
    Troops_TROOP_RAM,
    Troops_TROOP_TOWER,
    Troops_TROOP_CATAPULT,
    Troops_TROOP_TREBUCHET,
    Troops_TROOP_MANGONEL,
    Troops_TROOP_BALISTA,
    Troops_TROOP_TUNNELER,
    Troops_TROOP_ARAB_BOW,
    Troops_TROOP_ARAB_SLAVE,
    Troops_TROOP_ARAB_SLINGER,
    Troops_TROOP_ARAB_ASSASIN,
    Troops_TROOP_ARAB_HORSEMAN,
    Troops_TROOP_ARAB_SWORDSMAN,
    Troops_TROOP_ARAB_GRENADIER,
    Troops_TROOP_ARAB_BALLISTA,
    Troops_TROOP_BEDOUIN_CAMEL_LANCER,
    Troops_TROOP_BEDOUIN_HEALER,
    Troops_TROOP_BEDOUIN_EUNUCH,
    Troops_TROOP_BEDOUIN_AMBUSHER,
    Troops_TROOP_BEDOUIN_SKIRMISHER,
    Troops_TROOP_BEDOUIN_HEAVY_CAMEL,
    Troops_TROOP_BEDOUIN_SAPPER,
    Troops_TROOP_BEDOUIN_DEMOLISHER,
    Troops_Count_Troops
} Troops;


typedef enum ScenarioEvents
{
    ScenarioEvents_WEEKS_TURN_TO_MONTHS = 4,
    ScenarioEvents_ECO2_TIMER = 19200,
    ScenarioEvents_SIEGE_THAT_GOLD = 1500,
    ScenarioEvents_SIEGE_THAT_STONE = 1000,
    ScenarioEvents_SIEGE_THAT_TROOP_POINTS = 2000,
    ScenarioEvents_SA_TROOP_DIVIDER = 7,
    ScenarioEvents_DIR_TYPE_CAS = 1,
    ScenarioEvents_DIR_TYPE_SCN,
    ScenarioEvents_ACTION_BINK_FAIR = 0,
    ScenarioEvents_ACTION_BINK_PLAGUE,
    ScenarioEvents_ACTION_BINK_WHEAT,
    ScenarioEvents_ACTION_BINK_HOPS,
    ScenarioEvents_ACTION_BINK_APPLES,
    ScenarioEvents_ACTION_BINK_TREES,
    ScenarioEvents_ACTION_BINK_RABBITS,
    ScenarioEvents_ACTION_BINK_WOLVES,
    ScenarioEvents_ACTION_BINK_BANDITS,
    ScenarioEvents_ACTION_BINK_MAD_COWS,
    ScenarioEvents_ACTION_BINK_ARCHERS,
    ScenarioEvents_ACTION_BINK_MARRIAGE,
    ScenarioEvents_ACTION_BINK_JESTER,
    ScenarioEvents_ACTION_BINK_GRANARY,
    ScenarioEvents_ACTION_BINK_FIRE,
    ScenarioEvents_MAX_ML_MESSAGES = 1000,
    ScenarioEvents_MAX_ML_MESSAGE_LENGTH = 1000,
    ScenarioEvents_ML_TYPE_HELP = 1,
    ScenarioEvents_ML_TYPE_TITLES,
    ScenarioEvents_ML_TYPE_BRIEFINGS,
    ScenarioEvents_ML_TYPE_RAT_TAUNT = 5,
    ScenarioEvents_ML_TYPE_RAT_ANGER,
    ScenarioEvents_ML_TYPE_RAT_MOOD3,
    ScenarioEvents_ML_TYPE_RAT_MOOD4,
    ScenarioEvents_ML_TYPE_SNAKE_TAUNT,
    ScenarioEvents_ML_TYPE_SNAKE_ANGER,
    ScenarioEvents_ML_TYPE_SNAKE_MOOD3,
    ScenarioEvents_ML_TYPE_SNAKE_MOOD4,
    ScenarioEvents_ML_TYPE_PIG_TAUNT,
    ScenarioEvents_ML_TYPE_PIG_ANGER,
    ScenarioEvents_ML_TYPE_PIG_MOOD3,
    ScenarioEvents_ML_TYPE_PIG_MOOD4,
    ScenarioEvents_ML_TYPE_WOLF_TAUNT,
    ScenarioEvents_ML_TYPE_WOLF_ANGER,
    ScenarioEvents_ML_TYPE_WOLF_MOOD3,
    ScenarioEvents_ML_TYPE_WOLF_MOOD4,
    ScenarioEvents_ML_TYPE_ADVISER_PROMPTS_EVENTS,
    ScenarioEvents_ML_TYPE_ADVISER_PROMPTS_CIVIL,
    ScenarioEvents_ML_TYPE_ADVISER_PROMPTS_MILITARY,
    ScenarioEvents_ML_TYPE_ADVISER_PROMPTS_NARRATIVE,
    ScenarioEvents_ML_TYPE_NEW_1,
    ScenarioEvents_ML_TYPE_NEW_2,
    ScenarioEvents_ML_TYPE_NEW_3,
    ScenarioEvents_ML_TYPE_NEW_4,
    ScenarioEvents_MAX_GROUPS_IN_INVASION = 17,
    ScenarioEvents_MAX_EVENT_OLD_TYPES = 20,
    ScenarioEvents_MAX_EVENT_TYPES = 40,
    ScenarioEvents_TL_INVASION = 1,
    ScenarioEvents_TL_MESSAGE,
    ScenarioEvents_TL_EVENT,
    ScenarioEvents_TL_EVENT_AUTO = 0,
    ScenarioEvents_TL_EVENT_POPULATION,
    ScenarioEvents_TL_EVENT_LORD_KILLED,
    ScenarioEvents_TL_EVENT_ENEMY_LORD_KILLED,
    ScenarioEvents_TL_EVENT_GOLD_ACQUIRED,
    ScenarioEvents_TL_EVENT_FOOD_ACQUIRED,
    ScenarioEvents_TL_EVENT_WEAPONS_ACQUIRED,
    ScenarioEvents_TL_EVENT_GOODS_ACQUIRED,
    ScenarioEvents_TL_EVENT_NO_ENEMY_ON_MAP,
    ScenarioEvents_TL_EVENT_KEEP_ENCLOSED,
    ScenarioEvents_TL_EVENT_NO_WOLVES_ON_MAP,
    ScenarioEvents_TL_EVENT_ENEMY_ON_MAP,
    ScenarioEvents_TL_EVENT_NO_PEOPLE_LEFT,
    ScenarioEvents_TL_EVENT_YOUR_TROOPS_KILLED,
    ScenarioEvents_TL_EVENT_ENEMY_TROOPS_KILLED,
    ScenarioEvents_TL_EVENT_NO_ENEMY_ON_MAP_ONLY,
    ScenarioEvents_TL_EVENT_WEAPONS_ACQUIRED2 = 17,
    ScenarioEvents_TL_EVENT_4_CATTLE_FARMS,
    ScenarioEvents_TL_EVENT_2_WORKING_INNS,
    ScenarioEvents_TL_EVENT_BLESSED,
    ScenarioEvents_TL_EVENT_ALE,
    ScenarioEvents_TL_EVENT_MAXFEAR,
    ScenarioEvents_TL_EVENT_MINFEAR,
    ScenarioEvents_TL_EVENT_MARKER_PROXIMITY,
    ScenarioEvents_TL_EVENT_NO_ENEMY_GRANARIES,
    ScenarioEvents_TL_EVENT_PROXIMITY_2,
    ScenarioEvents_TL_EVENT_PROXIMITY_3,
    ScenarioEvents_TL_EVENT_PROXIMITY_4,
    ScenarioEvents_TL_EVENT_NO_BUILDING_AT_MARKER,
    ScenarioEvents_TL_EVENT_PROXIMITY_5,
    ScenarioEvents_TL_EVENT_NO_ENEMY_ON_MAP_DE,
    ScenarioEvents_TL_EVENT_HAVESTONEWALLS,
    ScenarioEvents_TL_EVENT_HAVESTONETOWERS,
    ScenarioEvents_TL_EVENT_34,
    ScenarioEvents_TL_EVENT_35,
    ScenarioEvents_TL_EVENT_36,
    ScenarioEvents_TL_EVENT_37,
    ScenarioEvents_TL_EVENT_38,
    ScenarioEvents_TL_EVENT_WIN_TIMER,
    ScenarioEvents_TL_ACTION_WIN = 0,
    ScenarioEvents_TL_ACTION_LOSE,
    ScenarioEvents_TL_ACTION_MESSAGE,
    ScenarioEvents_TL_ACTION_WOLF_SLEEPS,
    ScenarioEvents_TL_ACTION_WOLF_AWAKES,
    ScenarioEvents_TL_ACTION_TRAVELLING_FAIR,
    ScenarioEvents_TL_ACTION_TIME_OFF,
    ScenarioEvents_TL_ACTION_TIME_ON,
    ScenarioEvents_TL_ACTION_CEDE_CASTLE,
    ScenarioEvents_TL_ACTION_CROSSBOW_AVAILABLE,
    ScenarioEvents_TL_ACTION_UMESSAGE,
    ScenarioEvents_TL_ACTION_PLAGUE,
    ScenarioEvents_TL_ACTION_WHEATDIE,
    ScenarioEvents_TL_ACTION_HOPSDIE,
    ScenarioEvents_TL_ACTION_APPLEDIE,
    ScenarioEvents_TL_ACTION_TREESDIE,
    ScenarioEvents_TL_ACTION_RABBITEXPLOSION,
    ScenarioEvents_TL_ACTION_WOLFATTACK,
    ScenarioEvents_TL_ACTION_BANDITS,
    ScenarioEvents_TL_ACTION_MAD_COWS,
    ScenarioEvents_TL_ACTION_ARCHERS,
    ScenarioEvents_TL_ACTION_MARRIAGE,
    ScenarioEvents_TL_ACTION_JESTER,
    ScenarioEvents_TL_ACTION_SPECIAL1,
    ScenarioEvents_TL_ACTION_AUTO_BINKS_ON,
    ScenarioEvents_TL_ACTION_AUTO_BINKS_OFF,
    ScenarioEvents_TL_ACTION_WIN_MESSAGE,
    ScenarioEvents_TL_ACTION_LOSE_MESSAGE,
    ScenarioEvents_TL_ACTION_TURNOFF_REPEATING_INVASIONS,
    ScenarioEvents_TL_ACTION_THEFTFROMGRANARY,
    ScenarioEvents_TL_ACTION_FIRE,
    ScenarioEvents_TL_ACTION_CHANGE_ALLEGIANCE,
    ScenarioEvents_TL_ACTION_REINFORCEMENTS,
    ScenarioEvents_TL_ACTION_INVASION_ROUTING,
    ScenarioEvents_TL_ACTION_35,
    ScenarioEvents_TL_ACTION_36,
    ScenarioEvents_TL_ACTION_37,
    ScenarioEvents_TL_ACTION_38,
    ScenarioEvents_TL_ACTION_39,
    ScenarioEvents_MAX_TOTAL_TROOPS_IN_INVASION = 500
} ScenarioEvents;


typedef enum GM
{
    GM_LAND = 2,
    GM_PILLARS,
    GM_SEA_CHEVRONS,
    GM_SEA,
    GM_BUILDINGS1,
    GM_BUILDINGS2,
    GM_WORKSHOPS,
    GM_CLIFFS,
    GM_WALLS,
    GM_SPECIAL_LAND,
    GM_MISC_LAND,
    GM_RIVERS,
    GM_FARMLAND,
    GM_GOODS,
    GM_FLOATS,
    GM_BODY_PEASANT,
    GM_BODY_ARCHER,
    GM_BODY_WOODCUTTER,
    GM_BODY_FLETCHER,
    GM_BODY_OXCART,
    GM_BUILDING_ANIMS2,
    GM_SMOKE_ANIMS,
    GM_55X55_ANIMS,
    GM_QUARRY_ANIMS,
    GM_WINDMILL_ANIMS,
    GM_FLETCHER_ANIMS,
    GM_GOODS_ANIMS,
    GM_TREE_BIRCH,
    GM_TREE_PINE,
    GM_TREE_CHESTNUT,
    GM_BODY_STONEMASON,
    GM_BODY_FARMER,
    GM_BODY_MISSILE,
    GM_BODY_LADDERMAN,
    GM_BODY_BAKER,
    GM_BODY_MILLER,
    GM_DATA,
    GM_BODY_SPEARMAN,
    GM_BODY_PIKEMAN,
    GM_BODY_CROSSBOWMAN,
    GM_BODY_SWORDSMAN,
    GM_BODY_MACEMAN,
    GM_BODY_KNIGHT,
    GM_INTERFACE_BUTTONS,
    GM_INTERFACE_ICONS2,
    GM_MINE_ANIMS,
    GM_TILE_BURNT,
    GM_CHURCHS,
    GM_INTERFACE_PANELS,
    GM_WORKSHOP_BREW_ANIMS,
    GM_CASTLES,
    GM_BODY_BREWER,
    GM_CASTLE_ANIMS,
    GM_MACRO_LAND,
    GM_ROCKS,
    GM_ROCKS_CHEVRONS,
    GM_WORKSHOP_SMITH_ANIMS,
    GM_BODY_BLACKSMITH,
    GM_LAND_AND_STONES,
    GM_BODY_IRONMINER,
    GM_BODY_CATAPULT,
    GM_BODY_COW,
    GM_WORKSHOP_POLE_ANIMS,
    GM_PITCH_ANIMS,
    GM_WORKSHOP_BAKER_ANIMS,
    GM_WOODCUTTER_ANIMS,
    GM_DRAWBRIDGE_ANIMS,
    GM_WORKSHOP_TANNER_ANIMS,
    GM_TREE_OAK,
    GM_TREE_SHRUB1,
    GM_TREE_SHRUB2,
    GM_BODY_PITCHWORKER,
    GM_BODY_POLETURNER,
    GM_BODY_TANNER,
    GM_FLAG_ANIMS,
    GM_BODY_TRADER_HORSE,
    GM_BODY_TRADER,
    GM_ICONS,
    GM_ICONS_ALPHA,
    GM_BODY_DRUNKARD,
    GM_BODY_TENT,
    GM_BODY_MANGONEL,
    GM_BODY_TREBUCHET,
    GM_FLOAT_POP_CIRC,
    GM_BODY_SIEGE_ENGINEER,
    GM_FONT_STRONGHOLD_AA,
    GM_FARMER_ANIMS,
    GM_BODY_HUNTER,
    GM_HUNTER_ANIMS,
    GM_BODY_DEER,
    GM_BODY_LION,
    GM_BODY_RABBIT,
    GM_BODY_CAMEL,
    GM_BODY_DOG,
    GM_BODY_PRIEST,
    GM_TREE_APPLE,
    GM_STABLE_ANIMS,
    GM_BODY_LADY,
    GM_BODY_LORD,
    GM_BODY_JESTER,
    GM_BODY_ARMOURER,
    GM_ARMOURER_ANIMS,
    GM_SHEILD_ANIMS,
    GM_ANIM_TUNNELERS_GUILD,
    GM_BODY_TUNNELER,
    GM_CURSORS,
    GM_MAPEDIT_BUTTONS,
    GM_BODY_FIGHTING_MONK,
    GM_OIL_ANIMS = 113,
    GM_GALLOWS_ANIMS,
    GM_MAYPOLE_ANIMS,
    GM_BODY_OIL,
    GM_FONT_STRONGHOLD,
    GM_BODY_FIRE,
    GM_BODY_BURNING_MAN,
    GM_BODY_BALLISTA,
    GM_BODY_SHIELD,
    GM_BODY_MISSILE_2,
    GM_BODY_BATTERING_RAM,
    GM_BODY_SIEGE_TOWER,
    GM_BODY_STEAM,
    GM_BODY_CHICKEN,
    GM_BODY_MOTHER,
    GM_BODY_BOY,
    GM_BODY_GIRL,
    GM_ANIM_TUNNELS,
    GM_BODY_JUGGLER,
    GM_BODY_FIREEATER,
    GM_BODY_HEALER,
    GM_BODY_DISEASE,
    GM_BODY_MISSILE_COW,
    GM_CRACKS,
    GM_BODY_GATE,
    GM_BODY_BRAZIER,
    GM_KILLING_PITS,
    GM_PITCH_DITCHES,
    GM_BLAST,
    GM_SCRIBE,
    GM_ANIM_ICON_KNIGHT,
    GM_BODY_FIRE2,
    GM_ANIM_MISSILE_FIRE,
    GM_FONT_SLANTED,
    GM_BODY_INNKEEPER,
    GM_ICONS_FRONT_END,
    GM_TILE_RUINS,
    GM_ICONS_FRONT_END_COMBAT,
    GM_ICONS_FRONT_END_ECONOMICS,
    GM_ICONS_FRONT_END_BUILDER,
    GM_MINI_CURSORS,
    GM_BODY_CHICKEN_BROWN,
    GM_ANIM_MARKET,
    GM_INTERFACE_ICONS3,
    GM_TILE_FLATTIES,
    GM_ROCK_CHIPS,
    GM_ANIM_DUNKING_STOOL,
    GM_ANIM_DUNGEON,
    GM_ANIM_GIBBET,
    GM_ANIM_HEALER,
    GM_ANIM_STOCKS,
    GM_INTERFACE_SLIDER,
    GM_MAP_FLAGS,
    GM_NEW_SEA,
    GM_BODY_SEAGULL,
    GM_BODY_CROW,
    GM_PUFF_OF_SMOKE,
    GM_BODY_SPLASH,
    GM_ANIM_INN,
    GM_FLOATS_NEW,
    GM_ANIM_WHITECAPS,
    GM_ARMY_UNITS,
    GM_ANIM_STAKE,
    GM_ANIM_KILLING_PITS,
    GM_ENEMY_FACES,
    GM_ANIM_RACK,
    GM_ANIM_DOG_CAGE,
    GM_ANIM_DANCING_BEAR,
    GM_ANIM_CHOPPING_BLOCK,
    GM_BODY_FIREMAN,
    GM_INTERFACE_ARMY,
    GM_INTERFACE_RUINS,
    GM_BODY_ANIMAL_BURNING_BIG,
    GM_BODY_ANIMAL_BURNING_SMALL,
    GM_ANIM_HEADS,
    GM_BODY_GHOST,
    GM_ANIM_FLAG_SMALL,
    GM_BODY_ARAB_BOW,
    GM_BODY_ARAB_SLAVE,
    GM_BODY_ARAB_SLINGER,
    GM_BODY_ARAB_ASSASIN,
    GM_BODY_ARAB_HORSEMAN,
    GM_BODY_ARAB_SWORDSMAN,
    GM_BODY_ARAB_GRENADIER,
    GM_BODY_ARAB_BALLISTA,
    GM_ASSASIN_ROPE,
    GM_BODY_ARAB_HORSE,
    GM_TREE_CACTII,
    GM_ANIM_CRUSADER_FLAG,
    GM_BODY_INFO,
    GM_BODY_WOLF,
    GM_BODY_ARABIC_LORD = 205,
    GM_ADDITIONAL_GFX = 207,
    GM_BODY_BEDOUIN_CAMEL_LANCER,
    GM_BODY_BEDOUIN_HEALER,
    GM_BODY_BEDOUIN_EUNUCH,
    GM_BODY_BEDOUIN_AMBUSHER,
    GM_BODY_BEDOUIN_SKIRMISHER,
    GM_BODY_BEDOUIN_HEAVY_CAMEL,
    GM_BODY_BEDOUIN_SAPPER,
    GM_BODY_BEDOUIN_DEMOLISHER,
    GM_FLOAT_POP_CIRC_2 = 218,
    GM_BODY_MISSILE_FIREPOT,
    GM_BODY_JAVELIN,
    GM_BODY_GOAT,
    GM_BODY_HYENA,
    GM_BODY_CONDOR,
    GM_BODY_CROCODILE,
    GM_BODY_IMAM,
    GM_BODY_BEDOUIN_LORD,
    GM_BODY_SCRIBE_LORD,
    GM_BODY_LORD_FEMALE,
    GM_BODY_TEMPLE_GUARD,
    GM_BODY_LORD_BESSY,
    GM_BODY_ARABIC_LORD_FEMALE,
    GM_BODY_BEDOUIN_LORD_FEMALE,
    GM_ANIM_ARAB_FLAG
} GM;


typedef enum GameActionCommand
{
    GameActionCommand_GateHouseState = 1001,
    GameActionCommand_RepairBuilding,
    GameActionCommand_ToggleSleep,
    GameActionCommand_SetTaxRate,
    GameActionCommand_SetRationing,
    GameActionCommand_SetFoodEaten,
    GameActionCommand_MakeTroop,
    GameActionCommand_DrawbridgeState,
    GameActionCommand_OpenDogCage,
    GameActionCommand_Troops_Stop,
    GameActionCommand_Troops_Disband,
    GameActionCommand_Troops_AttackHere,
    GameActionCommand_Troops_Cow,
    GameActionCommand_EngBuild_Catapult,
    GameActionCommand_EngBuild_Trebuchet,
    GameActionCommand_EngBuild_SiegeTower,
    GameActionCommand_EngBuild_Shield,
    GameActionCommand_EngBuild_BatteringRam,
    GameActionCommand_Troops_Patrol,
    GameActionCommand_Troops_ChangeStance,
    GameActionCommand_Troops_DeSelectType,
    GameActionCommand_Troops_DeSelectExceptType,
    GameActionCommand_SetNextWeaponMade,
    GameActionCommand_BuyGoods,
    GameActionCommand_SellGoods,
    GameActionCommand_CycleBookmarks,
    GameActionCommand_SetCurrentTradedGood,
    GameActionCommand_RadarClicked,
    GameActionCommand_AlignSprites,
    GameActionCommand_ResizeRadarMapBuffer,
    GameActionCommand_FreeBuild_Event,
    GameActionCommand_FreeBuild_InvasionCharSet,
    GameActionCommand_FreeBuild_InvasionCount,
    GameActionCommand_FreeBuild_InvasionStart,
    GameActionCommand_ActionPointClicked,
    GameActionCommand_Undo,
    GameActionCommand_SetStartingTeam,
    GameActionCommand_Set_AI_Patrolling,
    GameActionCommand_Fix_Tribes,
    GameActionCommand_CloseTroopsPanel,
    GameActionCommand_AmmoRecharge,
    GameActionCommand_AI_Target_Special,
    GameActionCommand_CentreMarker,
    GameActionCommand_SH1Cheats,
    GameActionCommand_HideObjectiveProgress,
    GameActionCommand_SetRadarMapRotationType,
    GameActionCommand_SelectBuildingType,
    GameActionCommand_Game_Paused,
    GameActionCommand_RotateBuilding,
    GameActionCommand_Autotrade_Pause,
    GameActionCommand_Autotrade_OnOff,
    GameActionCommand_Autotrade_SetBuy,
    GameActionCommand_Autotrade_SetSell,
    GameActionCommand_Autotrade_Apply,
    GameActionCommand_SetOutpostState,
    GameActionCommand_SetOutpostSize,
    GameActionCommand_SetOutpostDelay,
    GameActionCommand_GenieSpeech,
    GameActionCommand_LordType,
    GameActionCommand_EngBuild_ArabBallista,
    GameActionCommand_Ally_Orders,
    GameActionCommand_Ally_CancelGoodsReq,
    GameActionCommand_Ally_SendGoods,
    GameActionCommand_Ally_RequestGoods,
    GameActionCommand_Ally_ConfirmOrders,
    GameActionCommand_Ally_CancelOrders,
    GameActionCommand_ShowPlannedMoat,
    GameActionCommand_ExtremePower,
    GameActionCommand_SkirmishInsult,
    GameActionCommand_MPConfig,
    GameActionCommand_ResetTime,
    GameActionCommand_SandsCheatAllowed,
    GameActionCommand_SpectatorMode,
    GameActionCommand_Scenario_Set_Starting_Month = 2000,
    GameActionCommand_Scenario_Set_Starting_Year,
    GameActionCommand_Scenario_Set_Trading,
    GameActionCommand_Scenario_Set_Special,
    GameActionCommand_Scenario_Set_StartingGoods,
    GameActionCommand_Scenario_Set_Starting_Popularity,
    GameActionCommand_Scenario_Set_Starting_Special_Gold,
    GameActionCommand_Scenario_Set_Starting_Special_Rations,
    GameActionCommand_Scenario_Set_Starting_Special_Tax,
    GameActionCommand_Scenario_Set_BuildingAvailable,
    GameActionCommand_Scenario_Set_TroopAvailable,
    GameActionCommand_Scenario_Set_SwordAvailable,
    GameActionCommand_Scenario_Set_XbowAvailable,
    GameActionCommand_Scenario_Set_PikeAvailable,
    GameActionCommand_EditorWipeGoods = 2015,
    GameActionCommand_RespawnLord,
    GameActionCommand_WipeAnimals,
    GameActionCommand_HoldTime,
    GameActionCommand_SetMarkerState,
    GameActionCommand_FastGoodsFeedin,
    GameActionCommand_Scenario_Set_MaceAvailable,
    GameActionCommand_Scenario_Set_bowAvailable,
    GameActionCommand_Scenario_Set_SpearAvailable,
    GameActionCommand_Scenario_Set_MercTroopAvailable,
    GameActionCommand_Scenario_Set_BedouinTroopAvailable,
    GameActionCommand_Balanced_GameActionCommand
} GameActionCommand;


typedef enum GameActionValues
{
    GameActionValues_CLOSE_GATE = 10,
    GameActionValues_OPEN_GATE,
    GameActionValues_RAISE_DRAWBRIDGE = 10,
    GameActionValues_LOWER_DRAWBRIDGE
} GameActionValues;


typedef enum ScenarioViews
{
    ScenarioViews_Blank_ScenarioViews,
    ScenarioViews_Main,
    ScenarioViews_StartingGoods,
    ScenarioViews_TradedGoods,
    ScenarioViews_BuildingAvailibilty,
    ScenarioViews_Invasions,
    ScenarioViews_Events,
    ScenarioViews_EventsConditions,
    ScenarioViews_EventsActions,
    ScenarioViews_AttackingForce,
    ScenarioViews_EditMessage,
    ScenarioViews_EditTeams,
    ScenarioViews_AdjustDates
} ScenarioViews;


typedef enum StartUpUIPanels
{
    StartUpUIPanels_Off,
    StartUpUIPanels_Invasion_StartUpUIPanels,
    StartUpUIPanels_FreeBuild
} StartUpUIPanels;


typedef enum GameDifficulty
{
    GameDifficulty_DIFFICULTY_EASY,
    GameDifficulty_DIFFICULTY_NORMAL,
    GameDifficulty_DIFFICULTY_HARD,
    GameDifficulty_DIFFICULTY_VERYHARD,
    GameDifficulty_DIFFICULTY_EXTREME,
    GameDifficulty_DIFFICULTY_NA = -1
} GameDifficulty;


typedef enum KeyFunctions
{
    KeyFunctions_Left = 1,
    KeyFunctions_Right,
    KeyFunctions_Up,
    KeyFunctions_Down,
    KeyFunctions_Pause,
    KeyFunctions_HomeKeep,
    KeyFunctions_Market,
    KeyFunctions_Signpost,
    KeyFunctions_Barracks,
    KeyFunctions_Granary_KeyFunctions,
    KeyFunctions_MapRotateLeft,
    KeyFunctions_MapRotateRight,
    KeyFunctions_FlattenLandscape,
    KeyFunctions_ZoomIn,
    KeyFunctions_ZoomOut,
    KeyFunctions_StanceStand,
    KeyFunctions_StanceDefensive,
    KeyFunctions_StanceAggressive,
    KeyFunctions_GroupTroops0,
    KeyFunctions_GroupTroops1,
    KeyFunctions_GroupTroops2,
    KeyFunctions_GroupTroops3,
    KeyFunctions_GroupTroops4,
    KeyFunctions_GroupTroops5,
    KeyFunctions_GroupTroops6,
    KeyFunctions_GroupTroops7,
    KeyFunctions_GroupTroops8,
    KeyFunctions_GroupTroops9,
    KeyFunctions_SelectClan0,
    KeyFunctions_SelectClan1,
    KeyFunctions_SelectClan2,
    KeyFunctions_SelectClan3,
    KeyFunctions_SelectClan4,
    KeyFunctions_SelectClan5,
    KeyFunctions_SelectClan6,
    KeyFunctions_SelectClan7,
    KeyFunctions_SelectClan8,
    KeyFunctions_SelectClan9,
    KeyFunctions_SetBookmark0,
    KeyFunctions_SetBookmark1,
    KeyFunctions_SetBookmark2,
    KeyFunctions_SetBookmark3,
    KeyFunctions_SetBookmark4,
    KeyFunctions_SetBookmark5,
    KeyFunctions_SetBookmark6,
    KeyFunctions_SetBookmark7,
    KeyFunctions_SetBookmark8,
    KeyFunctions_SetBookmark9,
    KeyFunctions_GotoBookmark0,
    KeyFunctions_GotoBookmark1,
    KeyFunctions_GotoBookmark2,
    KeyFunctions_GotoBookmark3,
    KeyFunctions_GotoBookmark4,
    KeyFunctions_GotoBookmark5,
    KeyFunctions_GotoBookmark6,
    KeyFunctions_GotoBookmark7,
    KeyFunctions_GotoBookmark8,
    KeyFunctions_GotoBookmark9,
    KeyFunctions_Patrol,
    KeyFunctions_Load_KeyFunctions,
    KeyFunctions_Save_KeyFunctions,
    KeyFunctions_IncreaseEngineSpeed,
    KeyFunctions_DecreaseEngineSpeed,
    KeyFunctions_ToggleUI,
    KeyFunctions_ToggleFrameRate,
    KeyFunctions_ToggleMuteSounds,
    KeyFunctions_Special,
    KeyFunctions_FreeBuildEvents,
    KeyFunctions_OpenChat,
    KeyFunctions_ShowPings,
    KeyFunctions_OptionsMenu,
    KeyFunctions_Insult1,
    KeyFunctions_Insult2,
    KeyFunctions_Insult3,
    KeyFunctions_Insult4,
    KeyFunctions_Insult5,
    KeyFunctions_Insult6,
    KeyFunctions_Insult7,
    KeyFunctions_Insult8,
    KeyFunctions_Insult9,
    KeyFunctions_Insult10,
    KeyFunctions_Insult11,
    KeyFunctions_Insult12,
    KeyFunctions_EditorHoldTime,
    KeyFunctions_EditorRespawnLord,
    KeyFunctions_EditorWipeAnimals,
    KeyFunctions_RadarZoomIn,
    KeyFunctions_RadarZoomOut,
    KeyFunctions_ToggleGoods,
    KeyFunctions_ToggleObjectives,
    KeyFunctions_QuickSave,
    KeyFunctions_Lord,
    KeyFunctions_CycleLord,
    KeyFunctions_RotateBuilding_KeyFunctions,
    KeyFunctions_Cheat_gold,
    KeyFunctions_Cheat_freestuff,
    KeyFunctions_MercPost,
    KeyFunctions_AllyToggle,
    KeyFunctions_BedouinStockade,
    KeyFunctions_ExtremePower1,
    KeyFunctions_ExtremePower2,
    KeyFunctions_ExtremePower3,
    KeyFunctions_ExtremePower4,
    KeyFunctions_ExtremePower5,
    KeyFunctions_ExtremePower6,
    KeyFunctions_ExtremePower7,
    KeyFunctions_ExtremePower8,
    KeyFunctions_ToggleHealthBars,
    KeyFunctions_Insult13,
    KeyFunctions_Insult14,
    KeyFunctions_Insult15,
    KeyFunctions_Insult16,
    KeyFunctions_Insult17,
    KeyFunctions_Insult18,
    KeyFunctions_Insult19,
    KeyFunctions_Insult20,
    KeyFunctions_TunnelersGuild,
    KeyFunctions_EngineersGuild,
    KeyFunctions_Armoury,
    KeyFunctions_Stop,
    KeyFunctions_PlaceWalls,
    KeyFunctions_PlaceStairs,
    KeyFunctions_PlaceLowWalls,
    KeyFunctions_PlaceCrenal,
    KeyFunctions_PlaceBarracks,
    KeyFunctions_PlaceMercPost,
    KeyFunctions_PlaceBedouinStockade,
    KeyFunctions_PlaceArmoury,
    KeyFunctions_PlaceTower1,
    KeyFunctions_PlaceTower2,
    KeyFunctions_PlaceTower3,
    KeyFunctions_PlaceTower4,
    KeyFunctions_PlaceTower5,
    KeyFunctions_PlaceEngineersGuild,
    KeyFunctions_PlaceTunnelGuild,
    KeyFunctions_PlaceBallista,
    KeyFunctions_PlaceMangonel,
    KeyFunctions_PlaceStables,
    KeyFunctions_PlaceSmelter,
    KeyFunctions_PlaceSmallGatehouse,
    KeyFunctions_PlaceLargeGatehouse,
    KeyFunctions_PlaceDrawbridge,
    KeyFunctions_PlaceDogCage,
    KeyFunctions_PlacePitchDitch,
    KeyFunctions_PlaceKillingPit,
    KeyFunctions_PlaceDigMoat,
    KeyFunctions_PlaceClearMoat,
    KeyFunctions_PlaceBrazier,
    KeyFunctions_PlaceStockpile,
    KeyFunctions_PlaceWoodcutter,
    KeyFunctions_PlaceQuarry,
    KeyFunctions_PlaceOxen,
    KeyFunctions_PlaceIronMine,
    KeyFunctions_PlacePitchRig,
    KeyFunctions_PlaceMarket,
    KeyFunctions_PlaceHunter,
    KeyFunctions_PlaceDairyFarm,
    KeyFunctions_PlaceAppleFarm,
    KeyFunctions_PlaceWheatFarm,
    KeyFunctions_PlaceHopsFarm,
    KeyFunctions_PlaceHouse,
    KeyFunctions_PlaceChurchMosque1,
    KeyFunctions_PlaceChurchMosque2,
    KeyFunctions_PlaceChurchMosque3,
    KeyFunctions_PlaceApothecary,
    KeyFunctions_PlaceWell,
    KeyFunctions_PlaceWaterpot,
    KeyFunctions_PlaceFletcher,
    KeyFunctions_PlacePoleturner,
    KeyFunctions_PlaceBlacksmith,
    KeyFunctions_PlaceTanner,
    KeyFunctions_PlaceArmourer,
    KeyFunctions_PlaceGranary,
    KeyFunctions_PlaceBaker,
    KeyFunctions_PlaceMill,
    KeyFunctions_PlaceBrewer,
    KeyFunctions_PlaceInn,
    KeyFunctions_PlaceMaypole,
    KeyFunctions_PlaceDancingBear,
    KeyFunctions_PlaceGardens1,
    KeyFunctions_PlaceGardens2,
    KeyFunctions_PlaceGardens3,
    KeyFunctions_PlaceStatue,
    KeyFunctions_PlaceShrine,
    KeyFunctions_PlaceFlag1,
    KeyFunctions_PlaceFlag2,
    KeyFunctions_PlaceFlag3,
    KeyFunctions_PlaceFlag4,
    KeyFunctions_PlaceGallows,
    KeyFunctions_PlaceCesspit,
    KeyFunctions_PlaceStocks,
    KeyFunctions_PlaceHeads,
    KeyFunctions_PlaceBurningStake,
    KeyFunctions_PlaceDungeon,
    KeyFunctions_PlaceRack,
    KeyFunctions_PlaceGibbett,
    KeyFunctions_PlaceChoppingBlock,
    KeyFunctions_PlaceDunkingStool,
    KeyFunctions_AttackHere,
    KeyFunctions_EditorShowConnections,
    KeyFunctions_Cathedral,
    KeyFunctions_MPPing,
    KeyFunctions_NumActions
} KeyFunctions;


typedef enum eOnScreenText
{
    eOnScreenText_OST_CHAT,
    eOnScreenText_OST_DATE,
    eOnScreenText_OST_MULTI_CHAT = 3,
    eOnScreenText_OST_FEEDBACK_1,
    eOnScreenText_OST_FEEDBACK_2,
    eOnScreenText_OST_FRAMERATE,
    eOnScreenText_OST_POPULARITY = 11,
    eOnScreenText_OST_STARTING_GOODS,
    eOnScreenText_OST_MP_GAME_OVER = 16,
    eOnScreenText_OST_MISSION_FINISHED,
    eOnScreenText_OST_SPLIT_MESSAGE = 19,
    eOnScreenText_OST_KEEP_MESSAGE,
    eOnScreenText_OST_WHO_OWNS,
    eOnScreenText_OST_PINGS,
    eOnScreenText_OST_GAME_PAUSED,
    eOnScreenText_OST_GAME_SPEED,
    eOnScreenText_OST_KING_OF_THE_HILL,
    eOnScreenText_OST_WIN_TIMER,
    eOnScreenText_OST_TIMETODEFEAT,
    eOnScreenText_OST_PING_ERROR,
    eOnScreenText_OST_PEOPLE_LEFT,
    eOnScreenText_OST_MESSAGE_BAR,
    eOnScreenText_OST_PEACETIMER,
    eOnScreenText_NUM_OST
} eOnScreenText;


typedef enum eMusicIDS
{
    eMusicIDS_MUSIC_TUNE_OFF,
    eMusicIDS_MUSIC_TUNE_MAIN = 10,
    eMusicIDS_MUSIC_TUNE_BATTLE = 2,
    eMusicIDS_MUSIC_TUNE_INTRO,
    eMusicIDS_MUSIC_TUNE_NARR1,
    eMusicIDS_MUSIC_TUNE_NARR2,
    eMusicIDS_MUSIC_TUNE_MONK,
    eMusicIDS_MUSIC_TUNE_CHOIR,
    eMusicIDS_MUSIC_TUNE_CHOIR2,
    eMusicIDS_MUSIC_TUNE_TUTORIAL,
    eMusicIDS_MUSIC_TUNE_SAD,
    eMusicIDS_MUSIC_TUNE_SAD2,
    eMusicIDS_MUSIC_TUNE_AVG,
    eMusicIDS_MUSIC_TUNE_AVG2,
    eMusicIDS_MUSIC_TUNE_HAPPY,
    eMusicIDS_MUSIC_TUNE_HAPPY2,
    eMusicIDS_MUSIC_TUNE_GOOD,
    eMusicIDS_MUSIC_TUNE_BAD,
    eMusicIDS_MUSIC_TUNE_SAD3,
    eMusicIDS_MUSIC_TUNE_SAD4,
    eMusicIDS_MUSIC_TUNE_HAPPY3,
    eMusicIDS_MUSIC_GERMAN_EGG,
    eMusicIDS_MUSIC_TUNE_BATTLE1A,
    eMusicIDS_MUSIC_TUNE_BATTLE1B,
    eMusicIDS_MUSIC_TUNE_BATTLE2A,
    eMusicIDS_MUSIC_TUNE_BATTLE2B,
    eMusicIDS_MUSIC_TUNE_BATTLE2C,
    eMusicIDS_MUSIC_TUNE_BATTLE3,
    eMusicIDS_MUSIC_TUNE_BATTLE4,
    eMusicIDS_MUSIC_TUNE_BATTLE_L1A,
    eMusicIDS_MUSIC_TUNE_BATTLE_L1B,
    eMusicIDS_MUSIC_TUNE_BATTLE_L1C,
    eMusicIDS_MUSIC_TUNE_BATTLE_L1D,
    eMusicIDS_MUSIC_TUNE_BATTLE_L2_GLORY1,
    eMusicIDS_MUSIC_TUNE_BATTLE_L2_GLORY2,
    eMusicIDS_MUSIC_TUNE_BATTLE_L2_GLORY3,
    eMusicIDS_MUSIC_TUNE_BATTLE_L2_GLORY4,
    eMusicIDS_MUSIC_TUNE_BATTLE_L2_GLORY5,
    eMusicIDS_MUSIC_TUNE_BATTLE_L2_GLORY6,
    eMusicIDS_MUSIC_TUNE_BATTLE_L2_PLOOP,
    eMusicIDS_MUSIC_TUNE_BATTLE_L2_DLOOP1,
    eMusicIDS_MUSIC_TUNE_BATTLE_L2_DLOOP2,
    eMusicIDS_MUSIC_TUNE_WIN1,
    eMusicIDS_MUSIC_TUNE_WIN2,
    eMusicIDS_MUSIC_TUNE_WIN3,
    eMusicIDS_MUSIC_TUNE_LOSE1,
    eMusicIDS_MUSIC_TUNE_LOSE2,
    eMusicIDS_MUSIC_PRE_MISSION1,
    eMusicIDS_MUSIC_PRE_MISSION2,
    eMusicIDS_MUSIC_PRE_MISSION3,
    eMusicIDS_MUSIC_PRE_MISSION4,
    eMusicIDS_MUSIC_AFTERMATH = 67,
    eMusicIDS_MUSIC_FLUTE7 = 72,
    eMusicIDS_MUSIC_OUD1,
    eMusicIDS_MUSIC_FLUTE1 = 97,
    eMusicIDS_MUSIC_PRE_MISSION21 = 116,
    eMusicIDS_MUSIC_PRE_MISSION22,
    eMusicIDS_MUSIC_PRE_MISSION23,
    eMusicIDS_MUSIC_PRE_MISSION24,
    eMusicIDS_MUSIC_PRE_MISSION25,
    eMusicIDS_MUSIC_PRE_MISSION26,
    eMusicIDS_MUSIC_PRE_MISSION27,
    eMusicIDS_MUSIC_PRE_MISSION28,
    eMusicIDS_MUSIC_PRE_MISSION29,
    eMusicIDS_MUSIC_PRE_MISSION30,
    eMusicIDS_MUSIC_PRE_MISSION31,
    eMusicIDS_MUSIC_PRE_MISSION32,
    eMusicIDS_MUSIC_PRE_MISSION33,
    eMusicIDS_MUSIC_PRE_MISSION34,
    eMusicIDS_MUSIC_PRE_MISSION35,
    eMusicIDS_MUSIC_TRAILER
} eMusicIDS;


typedef enum eSFX
{
    eSFX_FX_NULL,
    eSFX_FX_CLICK,
    eSFX_FX_CHOP,
    eSFX_FX_SAW,
    eSFX_FX_STOCKS,
    eSFX_FX_ARROW_FIRE,
    eSFX_FX_ARROW_HIT_BODY,
    eSFX_FX_TABLE_CLICK,
    eSFX_FX_LITTLE_PLOP,
    eSFX_FX_MED_PLOP,
    eSFX_FX_DROP_PLANK,
    eSFX_FX_WINDMILL,
    eSFX_FX_INN,
    eSFX_FX_MASON_CHIP,
    eSFX_FX_MASON_CRUMBLE,
    eSFX_FX_PULLER_LOWER,
    eSFX_FX_PULLER_STRAIN,
    eSFX_FX_PULLER_ROCK,
    eSFX_FX_PULLER_IMPACT,
    eSFX_FX_PULLER_RETURN,
    eSFX_FX_ARMY_CHARGE,
    eSFX_FX_PRYER_LEVER,
    eSFX_FX_DRAWBRIDGE_LOWERING,
    eSFX_FX_DRAWBRIDGE_LOWERED,
    eSFX_FX_DRAWBRIDGE_RAISING,
    eSFX_FX_DRAWBRIDGE_RAISED,
    eSFX_FX_DRAWBRIDGE_CONTROL,
    eSFX_FX_IRON_DUMP,
    eSFX_FX_IRON_LDUMP,
    eSFX_FX_IRON_BOIL,
    eSFX_FX_IRON_POUR,
    eSFX_FX_IRON_PULL,
    eSFX_FX_IRON_STRAIN,
    eSFX_FX_STOCK_FOOD,
    eSFX_FX_STOCK_ALE,
    eSFX_FX_STOCK_HOPS,
    eSFX_FX_STOCK_IRON,
    eSFX_FX_STOCK_PITCH,
    eSFX_FX_STOCK_STONE,
    eSFX_FX_STOCK_WEAPON,
    eSFX_FX_STOCK_WHEAT,
    eSFX_FX_STOCK_WOOD,
    eSFX_FX_TREE_FALL,
    eSFX_FX_LILTREE_FALL,
    eSFX_FX_BS_ANVIL,
    eSFX_FX_BS_BELLOW,
    eSFX_FX_BS_COOL,
    eSFX_FX_BS_POUR,
    eSFX_FX_BS_OPEN,
    eSFX_FX_BS_FILE,
    eSFX_FX_BAKE,
    eSFX_FX_BAKE2,
    eSFX_FX_MUDBUB,
    eSFX_FX_PITCH_WATERLAP,
    eSFX_FX_PITCH_SCOOP,
    eSFX_FX_PITCH_POUR,
    eSFX_FX_TANNER_CUT,
    eSFX_FX_TANNER_BRUSH1,
    eSFX_FX_TANNER_BRUSH2,
    eSFX_FX_FLETCH_LONG,
    eSFX_FX_GHOST,
    eSFX_FX_CAULDRON,
    eSFX_FX_STIR,
    eSFX_FX_CAMPFIRE,
    eSFX_FX_ARROW_BOUNCE,
    eSFX_FX_STEEL1,
    eSFX_FX_STEEL2,
    eSFX_FX_POLE_TURN,
    eSFX_FX_POLE_GRIND,
    eSFX_FX_MOAT_DIG,
    eSFX_FX_XBOW_FIRE,
    eSFX_FX_XBOW_WIND,
    eSFX_FX_BEAR_ATTACK,
    eSFX_FX_BEAR_DIE,
    eSFX_FX_COW_SLAUGHTER,
    eSFX_FX_COW_MILK,
    eSFX_FX_COW_MOO,
    eSFX_FX_MILK_POUR,
    eSFX_FX_DOG_BARK,
    eSFX_FX_DOG_DIE,
    eSFX_FX_DOG_PANT,
    eSFX_FX_DOG_WHIMPER,
    eSFX_FX_BROOM,
    eSFX_FX_SHARPEN,
    eSFX_FX_DEER_FALL,
    eSFX_FX_HUNTER_CUT,
    eSFX_FX_HORSES_1,
    eSFX_FX_HORSES_3,
    eSFX_FX_HORSES_4,
    eSFX_FX_HORSE_WHINNY,
    eSFX_FX_HORSE_DIE,
    eSFX_FX_HORSE_FALL,
    eSFX_FX_RABBIT_DIE,
    eSFX_FX_WOLF_DIE,
    eSFX_FX_WOLF_ATTACK,
    eSFX_FX_LION_DIE,
    eSFX_FX_ARMOUR_HIT,
    eSFX_FX_MAN_BURN2,
    eSFX_FX_POT_FLARE_UP,
    eSFX_FX_POT_OPEN,
    eSFX_FX_MAN_BURN,
    eSFX_FX_FIRE_START,
    eSFX_FX_OIL_DUMP,
    eSFX_FX_MENU_SLIDE,
    eSFX_FX_SIEGE_ROLL,
    eSFX_FX_CA_LOAD,
    eSFX_FX_CA_FIRE,
    eSFX_FX_MA_LOAD,
    eSFX_FX_MA_FIRE,
    eSFX_FX_TR_LOAD,
    eSFX_FX_TR_FIRE,
    eSFX_FX_TR_DIE,
    eSFX_FX_SIEGE_DIE,
    eSFX_FX_ROCK_HIT_WALL,
    eSFX_FX_ROCK_HIT_GROUND,
    eSFX_FX_WOOD_HIT,
    eSFX_FX_DEATH_CLUB,
    eSFX_FX_DEATH_ARROW,
    eSFX_FX_DEATH_SPEAR,
    eSFX_FX_DEATH_SWORD,
    eSFX_FX_BODY_HIT,
    eSFX_FX_IGNITE_PITCH = 122,
    eSFX_FX_MET_PUSH1,
    eSFX_FX_MET_PUSH2,
    eSFX_FX_MET_PUSH3,
    eSFX_FX_MET_PUSH4,
    eSFX_FX_MET_PUSH5,
    eSFX_FX_MET_PUSH6,
    eSFX_FX_METAL_ROLLOVER1,
    eSFX_FX_METAL_ROLLOVER2,
    eSFX_FX_METAL_ROLLOVER3,
    eSFX_FX_METAL_ROLLOVER4,
    eSFX_FX_METAL_ROLLOVER5,
    eSFX_FX_METAL_ROLLOVER6,
    eSFX_FX_WOOD_PUSH,
    eSFX_FX_WOOD_ROLLOVER,
    eSFX_FX_CHICKEN_START,
    eSFX_FX_CHICKEN_FLAP,
    eSFX_FX_CHICKEN_CLUCK,
    eSFX_FX_PC_DROP_CLICK,
    eSFX_FX_PC_DROP,
    eSFX_FX_PC_LIFT_CLICK,
    eSFX_FX_PC_LIFT,
    eSFX_FX_MAYPOLE,
    eSFX_FX_SWISH,
    eSFX_FX_SHIELDROLLOVER,
    eSFX_FX_PC_SLAM,
    eSFX_FX_ARROW_HIT_ANIMAL,
    eSFX_FX_HORSE_SNORT,
    eSFX_FX_TOWER_SMASH,
    eSFX_FX_DEATH_CLUB2,
    eSFX_FX_DEATH_ARROW2,
    eSFX_FX_DEATH_SPEAR2,
    eSFX_FX_DEATH_SWORD2,
    eSFX_FX_BODY_HIT2,
    eSFX_FX_BODY_HIT3,
    eSFX_FX_BODY_HIT4,
    eSFX_FX_DIG1,
    eSFX_FX_DIG2,
    eSFX_FX_WALLDROP,
    eSFX_FX_DROP_LOG,
    eSFX_FX_BABY,
    eSFX_FX_ATTACK_STONE,
    eSFX_FX_ATTACK_WOOD,
    eSFX_FX_SPLAT,
    eSFX_FX_COW_SPLAT,
    eSFX_FX_DEER_RUN,
    eSFX_FX_BALLISTA_LOAD,
    eSFX_FX_BALLISTA_FIRE,
    eSFX_FX_BUILDING_SMASH,
    eSFX_FX_DEATH_SHIELD,
    eSFX_FX_FLAME_ARROW,
    eSFX_FX_SWORDWALK_1,
    eSFX_FX_SWORDWALK_2,
    eSFX_FX_SWORDWALK_3,
    eSFX_FX_ROCK_SPLASH,
    eSFX_FX_RAM_SWING,
    eSFX_FX_RAM_HIT,
    eSFX_FX_SHEATH_IN,
    eSFX_FX_SHEATH_OUT,
    eSFX_FX_BUILD_UNIT,
    eSFX_FX_GIRL_DIE,
    eSFX_FX_GIRL_SCREAM,
    eSFX_FX_ARROW_HIT,
    eSFX_FX_MACE_HIT,
    eSFX_FX_PIKE_HIT,
    eSFX_FX_SPEAR_HIT,
    eSFX_FX_SWORD_HIT,
    eSFX_FX_FLIES,
    eSFX_FX_HARVEST,
    eSFX_FX_HOE,
    eSFX_FX_WOLF_HOWL,
    eSFX_FX_DOG_CAGE,
    eSFX_FX_OX_DIE,
    eSFX_FX_LADDER_PLACE,
    eSFX_FX_LADDER_BREAK,
    eSFX_FX_JESTER_DIE,
    eSFX_FX_LORD_DIE,
    eSFX_FX_ENEMY_LORD_DIE,
    eSFX_FX_CROW,
    eSFX_FX_GULL,
    eSFX_FX_OIL_REFILL,
    eSFX_FX_SMALL_FLAG,
    eSFX_FX_LARGE_FLAG,
    eSFX_FX_SNAKE_LORD_DIE,
    eSFX_FX_WOLF_LORD_DIE,
    eSFX_FX_CHURCH1,
    eSFX_FX_CHURCH2,
    eSFX_FX_CHURCH3,
    eSFX_FX_STRETCH,
    eSFX_FX_GALLOWS,
    eSFX_FX_DUNGEON,
    eSFX_FX_GULL_DIVE,
    eSFX_FX_GULL_SURFACE,
    eSFX_FX_WH_BREATH1,
    eSFX_FX_WH_BREATH2,
    eSFX_FX_WH_LIFT,
    eSFX_FX_WH_DUNK,
    eSFX_FX_GIRL_GRUNT,
    eSFX_FX_FIRE_OUT,
    eSFX_FX_FIRE_POP,
    eSFX_FX_THROW_WATER,
    eSFX_FX_WITCH_BURN,
    eSFX_FX_WITCH_SCREAM,
    eSFX_FX_LION_ATTACK,
    eSFX_FX_GH_SWING,
    eSFX_FX_GH_CATCH,
    eSFX_FX_ROPE_SLIDE,
    eSFX_FX_ASS_LAND,
    eSFX_FX_ASS_SWISH,
    eSFX_FX_SLING_THROW,
    eSFX_FX_SLAVE_FIRE,
    eSFX_FX_LORD_SWING,
    eSFX_FX_HORSE_ARCHER1,
    eSFX_FX_HORSE_ARCHER2,
    eSFX_FX_HORSE_ARCHER3,
    eSFX_FX_FIRE_THROW,
    eSFX_FX_DEATH_SLINGSTONE,
    eSFX_FX_HIT_SLINGSTONE,
    eSFX_FX_LORD_KILL,
    eSFX_FX_ARAB_LORD_KILL,
    eSFX_FX_LORD_HIT,
    eSFX_FX_ARAB_BALLISTA_FIRE,
    eSFX_FX_DECIMATE,
    eSFX_FX_CAMEL_DIE,
    eSFX_FX_BODY_HIT5,
    eSFX_FX_BODY_HIT6,
    eSFX_FX_BODY_HIT7,
    eSFX_FX_BODY_HIT8,
    eSFX_FX_HORSE_ARMY_CHARGE,
    eSFX_FX_EXIT_ROLLOVER,
    eSFX_FX_DICE,
    eSFX_FX_SKMASTER,
    eSFX_FX_SKGOLD1,
    eSFX_FX_SKGOLD2,
    eSFX_FX_SKGOLD3,
    eSFX_FX_KEY,
    eSFX_FX_TRAIL_CHICKEN,
    eSFX_FX_EXTREME_TROOPS_CLICK = 260,
    eSFX_FX_EXTREME_ROCK_VOLLEY,
    eSFX_FX_BUILDING_PLACEMENT = 265,
    eSFX_FX_BUILDING_PLACEMENT_SMALL,
    eSFX_FX_APOTHECARY_EXPLOSION,
    eSFX_FX_MILLER_WORKING,
    eSFX_FX_MILLER_WORKING_LOOP,
    eSFX_FX_PICK_APPLE,
    eSFX_FX_PICK_HOPS,
    eSFX_FX_GIBBET,
    eSFX_FX_OX_SELECT,
    eSFX_FX_OX_WALK,
    eSFX_FX_MARKET_SELECT,
    eSFX_FX_SIEGE_DOCK,
    eSFX_FX_XBOW_SAND,
    eSFX_FX_XBOW_INSPECT,
    eSFX_FX_XBOW_PICKUP,
    eSFX_FX_XBOW_HAMMER1,
    eSFX_FX_XBOW_HAMMER2,
    eSFX_FX_XBOW_PUTDOWN,
    eSFX_FX_DUNGEON_WHIP,
    eSFX_FX_STOCKS_CLICK,
    eSFX_FX_WOODWALL_PLACEMENT,
    eSFX_FX_STONEWALL_PLACEMENT,
    eSFX_FX_WOODTOWER_PLACEMENT,
    eSFX_FX_STONETOWER_PLACEMENT,
    eSFX_FX_WOOD_ROLLOVER2,
    eSFX_FX_BUILDING_PLACEMENT_STONE,
    eSFX_FX_BATTLEHORN,
    eSFX_FX_SKIRMISHER_THROWSPEAR,
    eSFX_FX_CAMEL_TROT_SING,
    eSFX_FX_CAMEL_TROT_SEVERAL,
    eSFX_FX_CAMEL_TROT_MANY,
    eSFX_FX_HEAVYCAMEL_TROT_SING,
    eSFX_FX_HEAVYCAMEL_TROT_SEVERAL,
    eSFX_FX_HEAVYCAMEL_TROT_MANY,
    eSFX_FX_DEMOLISHER_WALL,
    eSFX_FX_DEMOLISHER_STRUCK,
    eSFX_FX_DEMOLISHER_SHIELDDEST,
    eSFX_FX_AMBUSH_THROW,
    eSFX_FX_AMBUSH_LANDS,
    eSFX_FX_EUNUCH_SWORD,
    eSFX_FX_EUNUCH_WALL,
    eSFX_FX_EUNUCH_FORWARDSWING,
    eSFX_FX_DANCING_BEAR,
    eSFX_FX_CROC_GENERAL,
    eSFX_FX_CROC_KILLBITE,
    eSFX_FX_HYENA_IDLE,
    eSFX_FX_HYENA_ATTACK,
    eSFX_FX_HYENA_DIE,
    eSFX_FX_GOAT_IDLE,
    eSFX_FX_GOAT_DIE,
    eSFX_FX_MOSQUE_SMALL,
    eSFX_FX_MOSQUE_MEDIUM,
    eSFX_FX_MOSQUE_LARGE,
    eSFX_FX_MP_JOINER,
    eSFX_FX_MP_LEAVER,
    eSFX_FX_VICTORY,
    eSFX_FX_DEFEAT,
    eSFX_FX_FEMALE_LORD_DIE,
    eSFX_FX_BESSY_LORD_DIE,
    eSFX_FX_ENEMY_FEMALE_LORD_DIE,
    eSFX_FX_ENEMY_BESSY_LORD_DIE,
    eSFX_FX_FREEBUILD_GOLD
} eSFX;


typedef enum TutorialActions
{
    TutorialActions_TUT_SCROLL = 1,
    TutorialActions_TUT_ROTATE,
    TutorialActions_TUT_ZOOM,
    TutorialActions_TUT_FLATTEN,
    TutorialActions_TUT_FLATTEN2,
    TutorialActions_TUT_CLICK_BUILD_ITEM,
    TutorialActions_TUT_BUILT,
    TutorialActions_TUT_CLICK_BUILDING,
    TutorialActions_TUT_NEW_PEASANT,
    TutorialActions_TUT_EXIT_PANEL_MODE = 11,
    TutorialActions_TUT_SET_RATIONS,
    TutorialActions_TUT_SET_TAXES,
    TutorialActions_TUT_REPORTS_MENU,
    TutorialActions_TUT_REPORT_SELECT,
    TutorialActions_TUT_FULLSCREEN = 17
} TutorialActions;


typedef enum VictoryScreens
{
    VictoryScreens_banquet,
    VictoryScreens_economic,
    VictoryScreens_military_big,
    VictoryScreens_military_small,
    VictoryScreens_pig_VictoryScreens,
    VictoryScreens_rat_VictoryScreens,
    VictoryScreens_resources,
    VictoryScreens_snake_VictoryScreens,
    VictoryScreens_stockpile
} VictoryScreens;


typedef enum DefeatScreens
{
    DefeatScreens_baddies,
    DefeatScreens_general_DefeatScreens,
    DefeatScreens_pig_DefeatScreens,
    DefeatScreens_rat_DefeatScreens,
    DefeatScreens_ruins,
    DefeatScreens_snake_DefeatScreens,
    DefeatScreens_stocks,
    DefeatScreens_wolf_DefeatScreens
} DefeatScreens;


typedef enum Achievements
{
    Achievements_Complete_Tutorial = 1,
    Achievements_Complete_Campaign_1 = 11,
    Achievements_Complete_Campaign_2,
    Achievements_Complete_Campaign_3,
    Achievements_Complete_Campaign_4,
    Achievements_Complete_Campaign_5,
    Achievements_Complete_Campaign_6,
    Achievements_Complete_Campaign_7,
    Achievements_Complete_FirstEdition_Trail = 21,
    Achievements_Complete_Warchest_Trail,
    Achievements_Complete_Extreme_Trail,
    Achievements_Complete_Sands_Trail_1 = 31,
    Achievements_Complete_Sands_Trail_2,
    Achievements_Complete_Sands_Trail_3,
    Achievements_Complete_Sands_Trail_4,
    Achievements_Complete_Sands_Trail_5,
    Achievements_Complete_Sands_Trail_6,
    Achievements_Complete_Sands_Trail_7,
    Achievements_Complete_Sands_Trail_8,
    Achievements_Kill_Units_10k = 41,
    Achievements_Kill_Units_100k,
    Achievements_Kill_Units_1M,
    Achievements_Complete_Sands_Warrior = 51,
    Achievements_Complete_Sands_Champion,
    Achievements_Complete_Sands_Prince,
    Achievements_Win_Skirmish_Game = 61,
    Achievements_Win_Skirmish_Game_vs_7,
    Achievements_Win_Skirmish_Game_vs_Team_of_7,
    Achievements_Win_Skirmish_Game_vs_New_Lords,
    Achievements_Skirmish_Beating_All_Lords,
    Achievements_Win_Skirmish_No_Ranged,
    Achievements_Win_Skirmish_All_Ranged,
    Achievements_Map_Uploaded_To_Workshop = 91,
    Achievements_Scribe_Unlock,
    Achievements_Kill_1000_Lions = 113,
    Achievements_Store_1000_Food,
    Achievements_Store_1000_Weapons,
    Achievements_Store_10000_Wood,
    Achievements_Amass_10000_Gold,
    Achievements_Population_300,
    Achievements_Place_Dairy_Farms
} Achievements;


typedef enum AchievementStat
{
    AchievementStat_UnitsKilled = 1,
    AchievementStat_LionsKilled,
    AchievementStat_DairyFarms
} AchievementStat;


typedef enum AchievementMessage
{
    AchievementMessage_Wood_Stored = 1,
    AchievementMessage_Food_Stored,
    AchievementMessage_Weapon_Stored,
    AchievementMessage_Gold_Level,
    AchievementMessage_Lion_Killed,
    AchievementMessage_Jester_Killed,
    AchievementMessage_Unit_Killed_By_Player,
    AchievementMessage_Dairy_Farms,
    AchievementMessage_Population = 10
} AchievementMessage;


typedef enum SandsRanks
{
    SandsRanks_Peasant,
    SandsRanks_Tribesman,
    SandsRanks_Warrior,
    SandsRanks_Champion,
    SandsRanks_Prince
} SandsRanks;


typedef enum MPFlags
{
    MPFlags_GamePacket = 1,
    MPFlags_InitialSeedPacket,
    MPFlags_InitialAcknowledgePacket,
    MPFlags_StartGamePacket,
    MPFlags_ChatPacket,
    MPFlags_ChatInsultPacket,
    MPFlags_KickPlayerPacket,
    MPFlags_LeaveGamePacket,
    MPFlags_playerRemapPacket,
    MPFlags_CoopContinuationPacket
} MPFlags;


typedef enum RequesterTypes
{
    RequesterTypes_LoadSinglePlayerGame,
    RequesterTypes_SaveSinglePlayerGame,
    RequesterTypes_LoadMultiplayerGame,
    RequesterTypes_SaveMultiplayerGame,
    RequesterTypes_LoadEditorMap,
    RequesterTypes_SaveEditorMap,
    RequesterTypes_LoadUserWorkshopMap,
    RequesterTypes_LoadSinglePlayerCoopGame,
    RequesterTypes_LoadMultiplayerCoopGame
} RequesterTypes;


typedef enum SceneIDS
{
    SceneIDS_ActualMainGame = 2,
    SceneIDS_Story = 10,
    SceneIDS_Intro = 94,
    SceneIDS_Tutorial,
    SceneIDS_MapEditor_SceneIDS = 97,
    SceneIDS_MainGame,
    SceneIDS_FrontEnd,
    SceneIDS_Options = 101
} SceneIDS;


typedef enum AILords
{
    AILords_SK_NULL,
    AILords_SK_RAT,
    AILords_SK_SNAKE,
    AILords_SK_PIG,
    AILords_SK_WOLF,
    AILords_SK_SALADIN,
    AILords_SK_CALIPH,
    AILords_SK_SULTAN,
    AILords_SK_RICHARD,
    AILords_SK_FREDERICK,
    AILords_SK_PHILLIP,
    AILords_SK_WAZIR,
    AILords_SK_EMIR,
    AILords_SK_NIZAR,
    AILords_SK_SHERIFF,
    AILords_SK_MARSHAL,
    AILords_SK_ABBOT,
    AILords_SK_JEWEL,
    AILords_SK_SENTINEL,
    AILords_SK_NOMAD,
    AILords_SK_KAHIN,
    AILords_SK_CANARY,
    AILords_SK_TRADER,
    AILords_SK_SERGEANT,
    AILords_SK_LIONESS,
    AILords_SK_CROCODILE,
    AILords_SK_BALDWIN,
    AILords_SK_BULLSEYE,
    AILords_SK_SURGEON,
    AILords_SK_BAIBARS,
    AILords_SK_X1,
    AILords_SK_X2,
    AILords_SK_X3,
    AILords_SK_X4,
    AILords_SK_X5,
    AILords_SK_X6,
    AILords_SK_X7,
    AILords_SK_X8,
    AILords_SK_TEMP
} AILords;


typedef enum AvatarItems
{
    AvatarItems_None_AvatarItems,
    AvatarItems_Colour_BLACK,
    AvatarItems_Colour_BLUE,
    AvatarItems_Colour_BROWN,
    AvatarItems_Colour_DARKBLUE,
    AvatarItems_Colour_DARKBROWN,
    AvatarItems_Colour_DARKGREEN,
    AvatarItems_Colour_DARKPURPLE,
    AvatarItems_Colour_DARKRED,
    AvatarItems_Colour_FLESH,
    AvatarItems_Colour_GOLD,
    AvatarItems_Colour_GREEN,
    AvatarItems_Colour_GREY,
    AvatarItems_Colour_LIGHTBLUE,
    AvatarItems_Colour_LIGHTGREEN,
    AvatarItems_Colour_LIGHTORANGE,
    AvatarItems_Colour_LIGHTPINK,
    AvatarItems_Colour_LIGHTPURPLE,
    AvatarItems_Colour_LIGHTYELLOW,
    AvatarItems_Colour_ORANGE,
    AvatarItems_Colour_PINK,
    AvatarItems_Colour_PURPLE,
    AvatarItems_Colour_RED,
    AvatarItems_Colour_TEAL,
    AvatarItems_Colour_TURQUOISE,
    AvatarItems_Colour_WHITE,
    AvatarItems_Colour_Alt_BLACK = 101,
    AvatarItems_Colour_Alt_BLUE,
    AvatarItems_Colour_Alt_BROWN,
    AvatarItems_Colour_Alt_DARKBLUE,
    AvatarItems_Colour_Alt_DARKBROWN,
    AvatarItems_Colour_Alt_DARKGREEN,
    AvatarItems_Colour_Alt_DARKPURPLE,
    AvatarItems_Colour_Alt_DARKRED,
    AvatarItems_Colour_Alt_FLESH,
    AvatarItems_Colour_Alt_GOLD,
    AvatarItems_Colour_Alt_GREEN,
    AvatarItems_Colour_Alt_GREY,
    AvatarItems_Colour_Alt_LIGHTBLUE,
    AvatarItems_Colour_Alt_LIGHTGREEN,
    AvatarItems_Colour_Alt_LIGHTORANGE,
    AvatarItems_Colour_Alt_LIGHTPINK,
    AvatarItems_Colour_Alt_LIGHTPURPLE,
    AvatarItems_Colour_Alt_LIGHTYELLOW,
    AvatarItems_Colour_Alt_ORANGE,
    AvatarItems_Colour_Alt_PINK,
    AvatarItems_Colour_Alt_PURPLE,
    AvatarItems_Colour_Alt_RED,
    AvatarItems_Colour_Alt_TEAL,
    AvatarItems_Colour_Alt_TURQUOISE,
    AvatarItems_Colour_Alt_WHITE,
    AvatarItems_BackMask_001 = 1001,
    AvatarItems_BackMask_002,
    AvatarItems_BackMask_003,
    AvatarItems_BackMask_004,
    AvatarItems_BackMask_005,
    AvatarItems_BackMask_006,
    AvatarItems_BackMask_007,
    AvatarItems_BackMask_008,
    AvatarItems_BackMask_009,
    AvatarItems_BackMask_010,
    AvatarItems_BackMask_011,
    AvatarItems_BackMask_012,
    AvatarItems_BackMask_013,
    AvatarItems_BackMask_014,
    AvatarItems_BackMask_015,
    AvatarItems_BackMask_016,
    AvatarItems_BackMask_017,
    AvatarItems_BackMask_018,
    AvatarItems_BackMask_019,
    AvatarItems_BackMask_020,
    AvatarItems_BackMask_021,
    AvatarItems_BackMask_022,
    AvatarItems_BackMask_023,
    AvatarItems_BackMask_024,
    AvatarItems_BackMask_025,
    AvatarItems_BackMask_026,
    AvatarItems_BackMask_027,
    AvatarItems_BackMask_028,
    AvatarItems_BackMask_029,
    AvatarItems_BackMask_030,
    AvatarItems_BackMask_031,
    AvatarItems_BackMask_032,
    AvatarItems_BackMask_033,
    AvatarItems_BackMask_034,
    AvatarItems_BackMask_035,
    AvatarItems_BackMask_036,
    AvatarItems_BackMask_037,
    AvatarItems_BackMask_038,
    AvatarItems_BackMask_039,
    AvatarItems_BackMask_040,
    AvatarItems_BackMask_041,
    AvatarItems_BackMask_042,
    AvatarItems_BackMask_043,
    AvatarItems_BackMask_044,
    AvatarItems_BackMask_045,
    AvatarItems_BackMask_046,
    AvatarItems_BackMask_047,
    AvatarItems_BackMask_048,
    AvatarItems_BackMask_049,
    AvatarItems_BackMask_050,
    AvatarItems_BackMask_051,
    AvatarItems_BackMask_052,
    AvatarItems_BackMask_053,
    AvatarItems_BackMask_054,
    AvatarItems_BackMask_055,
    AvatarItems_BackMask_056,
    AvatarItems_BackMask_057,
    AvatarItems_BackMask_058,
    AvatarItems_BackMask_059,
    AvatarItems_BackMask_060,
    AvatarItems_BackMask_061,
    AvatarItems_BackMask_062,
    AvatarItems_BackMask_063,
    AvatarItems_BackMask_064,
    AvatarItems_BackMask_065,
    AvatarItems_BackMask_066,
    AvatarItems_BackMask_067,
    AvatarItems_BackMask_068,
    AvatarItems_BackMask_069,
    AvatarItems_BackMask_070,
    AvatarItems_BackMask_071,
    AvatarItems_BackMask_072,
    AvatarItems_BackMask_073,
    AvatarItems_BackMask_074,
    AvatarItems_BackMask_075,
    AvatarItems_BackMask_076,
    AvatarItems_BackMask_077,
    AvatarItems_BackMask_078,
    AvatarItems_BackMask_079,
    AvatarItems_BackMask_080,
    AvatarItems_BackMask_081,
    AvatarItems_BackMask_082,
    AvatarItems_BackMask_083,
    AvatarItems_BackMask_084,
    AvatarItems_BackMask_085,
    AvatarItems_BackMask_086,
    AvatarItems_BackMask_087,
    AvatarItems_BackMask_088,
    AvatarItems_BackMask_089,
    AvatarItems_BackMask_090,
    AvatarItems_BackMask_091,
    AvatarItems_BackMask_END = 1091,
    AvatarItems_BackMask_COUNT = 91,
    AvatarItems_Item_AF = 2001,
    AvatarItems_Item_AM,
    AvatarItems_Item_BESSY = 2004,
    AvatarItems_Item_BF,
    AvatarItems_Item_BM,
    AvatarItems_Item_COTL,
    AvatarItems_Item_EF,
    AvatarItems_Item_EM,
    AvatarItems_Item_GOING,
    AvatarItems_Item_GUNGEON,
    AvatarItems_Item_INK,
    AvatarItems_Item_INSCRYPTION,
    AvatarItems_Item_KZERO,
    AvatarItems_Item_NEVA,
    AvatarItems_Item_NORTH,
    AvatarItems_Item_REIGNS = 2018,
    AvatarItems_Item_SCRIBE,
    AvatarItems_Item_STYX,
    AvatarItems_Item_TALOS,
    AvatarItems_Item_THRONE,
    AvatarItems_Item_VOLVY,
    AvatarItems_Item_KTC,
    AvatarItems_Item_BLANK,
    AvatarItems_Item_SABERS,
    AvatarItems_Item_SARROW,
    AvatarItems_Item_SCHECKER,
    AvatarItems_Item_SCROSS,
    AvatarItems_Item_SDRAGON,
    AvatarItems_Item_SFDL,
    AvatarItems_Item_SKULL,
    AvatarItems_Item_SKULL2,
    AvatarItems_Item_SLION,
    AvatarItems_Item_SMOON,
    AvatarItems_Item_SPHX,
    AvatarItems_Item_SSTRIPE,
    AvatarItems_Item_SSUN,
    AvatarItems_Item_SUNICORN,
    AvatarItems_Item_SWORDS,
    AvatarItems_Item_ABBOT = 2042,
    AvatarItems_Item_CALIPH,
    AvatarItems_Item_CANARY,
    AvatarItems_Item_CROCODILE,
    AvatarItems_Item_EMIR,
    AvatarItems_Item_FREDERICK,
    AvatarItems_Item_JEWEL,
    AvatarItems_Item_KAHINAH,
    AvatarItems_Item_MARSHAL,
    AvatarItems_Item_NIZAR,
    AvatarItems_Item_NOMAD,
    AvatarItems_Item_PHILIP,
    AvatarItems_Item_PIG,
    AvatarItems_Item_RAT,
    AvatarItems_Item_RICHARD,
    AvatarItems_Item_SALADIN,
    AvatarItems_Item_SHERIFF,
    AvatarItems_Item_SNAKE,
    AvatarItems_Item_SULTAN,
    AvatarItems_Item_TRADER,
    AvatarItems_Item_WAZIR,
    AvatarItems_Item_WOLF,
    AvatarItems_Item_SENTINEL,
    AvatarItems_Item_GOAT,
    AvatarItems_Item_JACKAL,
    AvatarItems_Item_LAMB,
    AvatarItems_Item_LEOPARD,
    AvatarItems_Item_FALCON,
    AvatarItems_Item_LIONESS,
    AvatarItems_Item_SERGEANT,
    AvatarItems_Item_COBRA,
    AvatarItems_Item_FISH,
    AvatarItems_Item_CROWN,
    AvatarItems_Item_ROSE,
    AvatarItems_Item_FLOWERS,
    AvatarItems_Item_CAT,
    AvatarItems_Item_PAW,
    AvatarItems_Item_SUN,
    AvatarItems_Item_ANKH,
    AvatarItems_Item_CROSSA,
    AvatarItems_Item_CROSSB,
    AvatarItems_Item_CROSSC,
    AvatarItems_Item_CROSSD,
    AvatarItems_Item_CIRCLE,
    AvatarItems_Item_CIRCLEB,
    AvatarItems_Item_CIRCLEC,
    AvatarItems_Item_BALDWIN,
    AvatarItems_Item_BULLSEYE,
    AvatarItems_Item_SURGEON,
    AvatarItems_Item_BAIBARS,
    AvatarItems_Item_VULTURE,
    AvatarItems_Item_FOOLKING,
    AvatarItems_Item_END = 2093
} AvatarItems;


typedef enum eSkirmishGameMode
{
    eSkirmishGameMode_SKIRMISH_GAME_CUSTOM,
    eSkirmishGameMode_SKIRMISH_GAME_TRAIL,
    eSkirmishGameMode_SKIRMISH_GAME_NOT_SKIRMISH = -1,
    eSkirmishGameMode_SKIRMISH_GAME_CUSTOM_TRAIL = 2,
    eSkirmishGameMode_SKIRMISH_GAME_TEST_MISSION
} eSkirmishGameMode;



/* Derived from Custom.h; C++ syntax normalized for Ghidra CParser. */
typedef enum eGoodStorageType
{
	eGoodStorageType_eMarketGoodType_None           = 0x0,
	eGoodStorageType_eMarketGoodType_Default        = 0xA,
	eGoodStorageType_eMarketGoodType_Food           = 0x13,
	eGoodStorageType_eMarketGoodType_Weapon         = 0xB,
	eGoodStorageType_eMarketGoodType_PlayerGold	   = 0x28
} eGoodStorageType;

typedef enum eAliveState
{
	eAliveState_NULL            = 0x0,
	eAliveState_NeedInit        = 0x1,
	eAliveState_Alive           = 0x2,
	eAliveState_MarkedForDelete = 0x3
} eAliveState;

typedef struct PlayerResources
{
	uint32_t N000039F7; //0x0000
	uint32_t N000044FF; //0x0004
	uint32_t N000039F8; //0x0008
	uint32_t N00004501; //0x000C
	uint32_t N000039F9; //0x0010
	uint32_t N00004503; //0x0014
	uint32_t N000039FA; //0x0018
	uint32_t N00004505; //0x001C
	uint32_t N000039FB; //0x0020
	uint32_t N00004507; //0x0024
	uint32_t N000039FC; //0x0028
	uint32_t N00004509; //0x002C
	uint16_t N000039FD; //0x0030
	uint16_t N000047DF; //0x0032
	uint32_t N0000450B; //0x0034
	uint32_t N000039FE; //0x0038
	uint32_t N0000450D; //0x003C
	uint32_t N000039FF; //0x0040
	uint32_t N0000450F; //0x0044
	uint32_t N00003A00; //0x0048
	uint32_t N00004511; //0x004C
	uint16_t N00003A01; //0x0050
	uint16_t N000047DA; //0x0052
	uint32_t N00004513; //0x0054
	uint32_t N00003A02; //0x0058
	uint32_t N00004515; //0x005C
	uint32_t r_CurrentPopularity; //0x0060
	uint32_t N00004517; //0x0064
	uint32_t N00003A04; //0x0068
	uint32_t r_PeasantSpawnTimer; //0x006C
	uint32_t N00003A05; //0x0070
	uint32_t r_CivilianHousingSpace; //0x0074
	uint32_t r_CiviliansTotal; //0x0078
	uint32_t N0000451D; //0x007C
	uint32_t N00003A07; //0x0080
	uint32_t N0000451F; //0x0084
	uint32_t r_ReadyPeasants; //0x0088
	uint32_t r_ExistingPeasants; //0x008C
	uint32_t N00003A09; //0x0090
	uint32_t N00004523; //0x0094
	uint32_t N00003A0A; //0x0098
	uint32_t N00004525; //0x009C
	uint32_t N00003A0B; //0x00A0
	uint32_t N00004527; //0x00A4
	uint32_t N00003A0C; //0x00A8
	uint32_t N00004529; //0x00AC
	uint32_t N00003A0D; //0x00B0
	uint32_t N0000452B; //0x00B4
	uint32_t N00003A0E; //0x00B8
	uint32_t N0000452D; //0x00BC
	uint32_t N00003A0F; //0x00C0
	uint32_t N0000452F; //0x00C4
	uint32_t N00003A10; //0x00C8
	uint32_t N00004531; //0x00CC
	uint32_t N00003A11; //0x00D0
	uint32_t N00004533; //0x00D4
	uint32_t N00003A12; //0x00D8
	uint32_t N00004535; //0x00DC
	uint32_t N00003A13; //0x00E0
	uint32_t N00004537; //0x00E4
	uint32_t N00003A14; //0x00E8
	uint32_t N00004539; //0x00EC
	uint32_t N00003A15; //0x00F0
	uint32_t N0000453B; //0x00F4
	uint32_t N00003A16; //0x00F8
	uint32_t N0000453D; //0x00FC
	uint32_t N00003A17; //0x0100
	uint32_t N0000453F; //0x0104
	uint32_t N00003A18; //0x0108
	uint32_t N00004541; //0x010C
	uint32_t N00003A19; //0x0110
	uint32_t N00004543; //0x0114
	uint32_t N00003A1A; //0x0118
	uint32_t N00004545; //0x011C
	uint32_t N00003A1B; //0x0120
	uint32_t N00004547; //0x0124
	uint32_t N00003A1C; //0x0128
	uint32_t N00004549; //0x012C
	uint32_t N00003A1D; //0x0130
	uint32_t N0000454B; //0x0134
	uint32_t N00003A1E; //0x0138
	uint32_t N0000454D; //0x013C
	uint32_t N00003A1F; //0x0140
	uint32_t N0000454F; //0x0144
	uint32_t N00003A20; //0x0148
	uint32_t N00004551; //0x014C
	uint32_t N00003A21; //0x0150
	uint32_t N00004553; //0x0154
	uint32_t N00003A22; //0x0158
	uint32_t N00004555; //0x015C
	uint32_t N00003A23; //0x0160
	uint32_t N00004557; //0x0164
	uint32_t N00003A24; //0x0168
	uint32_t N00004559; //0x016C
	uint32_t N00003A25; //0x0170
	uint32_t N0000455B; //0x0174
	uint32_t N00003A26; //0x0178
	uint32_t N0000455D; //0x017C
	uint32_t N00003A27; //0x0180
	uint32_t N0000455F; //0x0184
	uint32_t N00003A28; //0x0188
	uint32_t N00004561; //0x018C
	uint32_t N00003A29; //0x0190
	uint32_t N00004563; //0x0194
	uint32_t N00003A2A; //0x0198
	uint32_t N00004565; //0x019C
	uint32_t N00003A2B; //0x01A0
	uint32_t N00004567; //0x01A4
	uint32_t N00003A2C; //0x01A8
	uint32_t N00004569; //0x01AC
	uint32_t N00003A2D; //0x01B0
	uint32_t N0000456B; //0x01B4
	uint32_t N00003A2E; //0x01B8
	uint32_t N0000456D; //0x01BC
	uint32_t N00003A2F; //0x01C0
	uint32_t N0000456F; //0x01C4
	uint32_t N00003A30; //0x01C8
	uint32_t N00004571; //0x01CC
	uint32_t N00003A31; //0x01D0
	uint32_t N00004573; //0x01D4
	uint32_t N00003A32; //0x01D8
	uint32_t N00004575; //0x01DC
	uint32_t N00003A33; //0x01E0
	uint32_t N00004577; //0x01E4
	uint32_t N00003A34; //0x01E8
	uint32_t N00004579; //0x01EC
	uint32_t N00003A35; //0x01F0
	uint32_t N0000457B; //0x01F4
	uint32_t N00003A36; //0x01F8
	uint32_t N0000457D; //0x01FC
	uint32_t N00003A37; //0x0200
	uint32_t N0000457F; //0x0204
	uint32_t N00003A38; //0x0208
	uint32_t N00004581; //0x020C
	uint32_t N00003A39; //0x0210
	uint32_t N00004583; //0x0214
	uint32_t N00003A3A; //0x0218
	uint32_t N00004585; //0x021C
	uint32_t N00003A3B; //0x0220
	uint32_t N00004587; //0x0224
	uint32_t N00003A3C; //0x0228
	uint32_t N00004589; //0x022C
	uint32_t N00003A3D; //0x0230
	uint32_t N0000458B; //0x0234
	uint32_t N00003A3E; //0x0238
	uint32_t N0000458D; //0x023C
	uint32_t N00003A3F; //0x0240
	uint32_t N0000458F; //0x0244
	uint32_t N00003A40; //0x0248
	uint32_t N00004591; //0x024C
	uint32_t N00003A41; //0x0250
	uint32_t N00004593; //0x0254
	uint32_t N00003A42; //0x0258
	uint32_t N00004595; //0x025C
	uint32_t N00003A43; //0x0260
	uint32_t N00004597; //0x0264
	uint32_t N00003A44; //0x0268
	uint32_t N00004599; //0x026C
	uint32_t N00003A45; //0x0270
	uint32_t N0000459B; //0x0274
	uint32_t N00003A46; //0x0278
	uint32_t N0000459D; //0x027C
	uint32_t N00003A47; //0x0280
	uint32_t N0000459F; //0x0284
	uint32_t N00003A48; //0x0288
	uint32_t N000045A1; //0x028C
	uint32_t N00003A49; //0x0290
	uint32_t N000045A3; //0x0294
	uint32_t N00003A4A; //0x0298
	uint32_t N000045A5; //0x029C
	uint32_t N00003A4B; //0x02A0
	uint32_t N000045A7; //0x02A4
	uint32_t N00003A4C; //0x02A8
	uint32_t N000045A9; //0x02AC
	uint32_t N00003A4D; //0x02B0
	uint32_t N000045AB; //0x02B4
	uint32_t N00003A4E; //0x02B8
	uint32_t N000045AD; //0x02BC
	uint32_t N00003A4F; //0x02C0
	uint32_t N000045AF; //0x02C4
	uint32_t N00003A50; //0x02C8
	uint32_t N000045B1; //0x02CC
	uint32_t N00003A51; //0x02D0
	uint32_t N000045B3; //0x02D4
	uint32_t N00003A52; //0x02D8
	uint32_t N000045B5; //0x02DC
	uint32_t N00003A53; //0x02E0
	uint32_t N000045B7; //0x02E4
	uint32_t N00003A54; //0x02E8
	uint32_t N000045B9; //0x02EC
	uint32_t N00003A55; //0x02F0
	uint32_t N000045BB; //0x02F4
	uint32_t N00003A56; //0x02F8
	uint32_t N000045BD; //0x02FC
	uint32_t N00003A57; //0x0300
	uint32_t N000045BF; //0x0304
	uint32_t N00003A58; //0x0308
	uint32_t N000045C1; //0x030C
	uint32_t N00003A59; //0x0310
	uint32_t N000045C3; //0x0314
	uint32_t N00003A5A; //0x0318
	uint32_t N000045C5; //0x031C
	uint32_t N00003A5B; //0x0320
	uint32_t N000045C7; //0x0324
	uint32_t N00003A5C; //0x0328
	uint32_t N000045C9; //0x032C
	uint32_t N00003A5D; //0x0330
	uint32_t N000045CB; //0x0334
	uint32_t N00003A5E; //0x0338
	uint32_t N000045CD; //0x033C
	uint32_t N00003A5F; //0x0340
	uint32_t N000045CF; //0x0344
	uint32_t N00003A60; //0x0348
	uint32_t N000045D1; //0x034C
	uint32_t N00003A61; //0x0350
	uint32_t N000045D3; //0x0354
	uint32_t N00003A62; //0x0358
	uint32_t N000045D5; //0x035C
	uint32_t N00003A63; //0x0360
	uint32_t N000045D7; //0x0364
	uint32_t N00003A64; //0x0368
	uint32_t N000045D9; //0x036C
	uint32_t N00003A65; //0x0370
	uint32_t N000045DB; //0x0374
	uint32_t N00003A66; //0x0378
	uint32_t N000045DD; //0x037C
	uint32_t N00003A67; //0x0380
	uint32_t N000045DF; //0x0384
	uint32_t N00003A68; //0x0388
	uint32_t N000045E1; //0x038C
	uint32_t N00003A69; //0x0390
	uint32_t N000045E3; //0x0394
	uint32_t N00003A6A; //0x0398
	uint32_t N000045E5; //0x039C
	uint32_t N00003A6B; //0x03A0
	uint32_t N000045E7; //0x03A4
	uint32_t N00003A6C; //0x03A8
	uint32_t N000045E9; //0x03AC
	uint32_t N00003A6D; //0x03B0
	uint32_t r_PreviousPopularity; //0x03B4
	uint32_t N00003A6E; //0x03B8
	uint32_t N000045ED; //0x03BC
	uint32_t N00003A6F; //0x03C0
	uint32_t N000045EF; //0x03C4
	uint32_t N00003A70; //0x03C8
	uint32_t N000045F1; //0x03CC
	uint32_t N00003A71; //0x03D0
	uint32_t N000045F3; //0x03D4
	uint32_t N00003A72; //0x03D8
	uint32_t N000045F5; //0x03DC
	uint32_t N00003A73; //0x03E0
	uint32_t N000045F7; //0x03E4
	uint32_t N00003A74; //0x03E8
	uint32_t N000045F9; //0x03EC
	uint32_t N00003A75; //0x03F0
	uint32_t N000045FB; //0x03F4
	uint32_t N00003A76; //0x03F8
	uint32_t N000045FD; //0x03FC
	uint32_t N00003A77; //0x0400
	uint32_t N000045FF; //0x0404
	uint32_t N00003A78; //0x0408
	uint32_t N00004601; //0x040C
	uint32_t N00003A79; //0x0410
	uint32_t N00004603; //0x0414
	uint32_t N00003A7A; //0x0418
	uint32_t N00004605; //0x041C
	uint32_t N00003A7B; //0x0420
	uint32_t N00004607; //0x0424
	uint32_t N00003A7C; //0x0428
	uint32_t N00004609; //0x042C
	uint32_t N00003A7D; //0x0430
	uint32_t N0000460B; //0x0434
	uint32_t N00003A7E; //0x0438
	uint32_t N0000460D; //0x043C
	uint32_t N00003A7F; //0x0440
	uint32_t N0000460F; //0x0444
	uint32_t N00003A80; //0x0448
	uint32_t N00004611; //0x044C
	uint32_t N00003A81; //0x0450
	uint32_t N00004613; //0x0454
	uint32_t N00003A82; //0x0458
	uint32_t N00004615; //0x045C
	uint32_t N00003A83; //0x0460
	uint32_t N00004617; //0x0464
	uint32_t N00003A84; //0x0468
	uint32_t N00004619; //0x046C
	uint32_t N00003A85; //0x0470
	uint32_t N0000461B; //0x0474
	uint32_t N00003A86; //0x0478
	uint32_t N0000461D; //0x047C
	uint32_t N00003A87; //0x0480
	uint32_t N0000461F; //0x0484
	uint32_t N00003A88; //0x0488
	uint32_t N00004621; //0x048C
	uint32_t N00003A89; //0x0490
	uint32_t N00004623; //0x0494
	uint32_t N00003A8A; //0x0498
	uint32_t N00004625; //0x049C
	uint32_t N00003A8B; //0x04A0
	uint32_t N00004627; //0x04A4
	uint32_t N00003A8C; //0x04A8
	uint32_t N00004629; //0x04AC
	uint32_t N00003A8D; //0x04B0
	uint32_t N0000462B; //0x04B4
	uint32_t N00003A8E; //0x04B8
	uint32_t N0000462D; //0x04BC
	uint32_t N00003A8F; //0x04C0
	uint32_t N0000462F; //0x04C4
	uint32_t N00003A90; //0x04C8
	uint32_t N00004631; //0x04CC
	uint32_t N00003A91; //0x04D0
	uint32_t N00004633; //0x04D4
	uint32_t r_WoodPlanks; //0x04D8
	uint32_t r_RawHops; //0x04DC
	uint32_t r_StoneBlocks; //0x04E0
	uint32_t r_CowHides; //0x04E4
	uint32_t r_IronIngots; //0x04E8
	uint32_t r_PitchRaw; //0x04EC
	uint32_t r_PitchRefined; //0x04F0
	uint32_t r_RawWheat; //0x04F4
	uint32_t r_FoodBread; //0x04F8
	uint32_t r_FoodCheese; //0x04FC
	uint32_t r_FoodMeat; //0x0500
	uint32_t r_FoodFruit; //0x0504
	uint32_t r_FoodAle; //0x0508
	uint32_t r_Gold; //0x050C
	uint32_t r_Flour; //0x0510
	uint32_t r_Bows; //0x0514
	uint32_t r_Crossbows; //0x0518
	uint32_t r_Spears; //0x051C
	uint32_t r_Pikes; //0x0520
	uint32_t r_Maces; //0x0524
	uint32_t r_Swords; //0x0528
	uint32_t r_LeatherArmour; //0x052C
	uint32_t r_MetalArmour; //0x0530
	uint32_t N0000464B; //0x0534
	uint32_t N00003A9E; //0x0538
	uint32_t N0000464D; //0x053C
	uint32_t N00003A9F; //0x0540
	uint32_t N0000464F; //0x0544
	uint32_t N00003AA0; //0x0548
	uint32_t N00004651; //0x054C
	uint32_t N00003AA1; //0x0550
	uint32_t N00004653; //0x0554
	uint32_t N00003AA2; //0x0558
	uint32_t N00004655; //0x055C
	uint32_t N00003AA3; //0x0560
	uint32_t N00004657; //0x0564
	uint32_t N00003AA4; //0x0568
	uint32_t N00004659; //0x056C
	uint32_t N00003AA5; //0x0570
	uint32_t N0000465B; //0x0574
	uint32_t N00003AA6; //0x0578
	uint32_t N0000465D; //0x057C
	uint32_t N00003AA7; //0x0580
	uint32_t N0000465F; //0x0584
	uint32_t N00003AA8; //0x0588
	uint32_t N00004661; //0x058C
	uint32_t N00003AA9; //0x0590
	uint32_t N00004663; //0x0594
	uint32_t N00003AAA; //0x0598
	uint32_t N00004665; //0x059C
	uint32_t N00003AAB; //0x05A0
	uint32_t N00004667; //0x05A4
	uint32_t N00003AAC; //0x05A8
	uint32_t N00004669; //0x05AC
	uint32_t N00003AAD; //0x05B0
	uint32_t N0000466B; //0x05B4
	uint32_t N00003AAE; //0x05B8
	uint32_t N0000466D; //0x05BC
	uint32_t N00003AAF; //0x05C0
	uint32_t N0000466F; //0x05C4
	uint32_t N00003AB0; //0x05C8
	uint32_t N00004671; //0x05CC
	uint32_t N00003AB1; //0x05D0
	uint32_t N00004673; //0x05D4
	uint32_t N00003AB2; //0x05D8
	uint32_t N00004675; //0x05DC
	uint32_t N00003AB3; //0x05E0
	uint32_t N00004677; //0x05E4
	uint32_t N00003AB4; //0x05E8
	uint32_t N00004679; //0x05EC
	uint32_t N00003AB5; //0x05F0
	uint32_t N0000467B; //0x05F4
	uint32_t N00003AB6; //0x05F8
	uint32_t N0000467D; //0x05FC
	uint32_t N00003AB7; //0x0600
	uint32_t N0000467F; //0x0604
	uint32_t N00003AB8; //0x0608
	uint32_t N00004681; //0x060C
	uint32_t N00003AB9; //0x0610
	uint32_t N00004683; //0x0614
	uint32_t N00003ABA; //0x0618
	uint32_t N00004685; //0x061C
	uint32_t N00003ABB; //0x0620
	uint32_t N00004687; //0x0624
	uint32_t N00003ABC; //0x0628
	uint32_t N00004689; //0x062C
	uint32_t N00003ABD; //0x0630
	uint32_t N0000468B; //0x0634
	uint32_t N00003ABE; //0x0638
	uint32_t N0000468D; //0x063C
	uint32_t N00003ABF; //0x0640
	uint32_t N0000468F; //0x0644
	uint32_t N00003AC0; //0x0648
	uint32_t N00004691; //0x064C
	uint32_t N00003AC1; //0x0650
	uint32_t N00004693; //0x0654
	uint32_t N00003AC2; //0x0658
	uint32_t N00004695; //0x065C
	uint32_t N00003AC3; //0x0660
	uint32_t N00004697; //0x0664
	uint32_t N00003AC4; //0x0668
	uint32_t N00004699; //0x066C
	uint32_t N00003AC5; //0x0670
	uint32_t N0000469B; //0x0674
	uint32_t N00003AC6; //0x0678
	uint32_t N0000469D; //0x067C
	uint32_t N00003AC7; //0x0680
	uint32_t N0000469F; //0x0684
	uint32_t N00003AC8; //0x0688
	uint32_t N000046A1; //0x068C
	uint32_t N00003AC9; //0x0690
	uint32_t N000046A3; //0x0694
	uint32_t N00003ACA; //0x0698
	uint32_t N000046A5; //0x069C
	uint32_t N00003ACB; //0x06A0
	uint32_t N000046A7; //0x06A4
	uint32_t N00003ACC; //0x06A8
	uint32_t N000046A9; //0x06AC
	uint32_t N00003ACD; //0x06B0
	uint32_t N000046AB; //0x06B4
	uint32_t N00003ACE; //0x06B8
	uint32_t N000046AD; //0x06BC
	uint32_t N00003ACF; //0x06C0
	uint32_t N000046AF; //0x06C4
	uint32_t N00003AD0; //0x06C8
	uint32_t N000046B1; //0x06CC
	uint32_t N00003AD1; //0x06D0
	uint32_t N000046B3; //0x06D4
	uint32_t N00003AD2; //0x06D8
	uint32_t N000046B5; //0x06DC
	uint32_t N00003AD3; //0x06E0
	uint32_t N000046B7; //0x06E4
	uint32_t N00003AD4; //0x06E8
	uint32_t N000046B9; //0x06EC
	uint32_t N00003AD5; //0x06F0
	uint32_t N000046BB; //0x06F4
	uint32_t N00003AD6; //0x06F8
	uint32_t N000046BD; //0x06FC
	uint32_t N00003AD7; //0x0700
	uint32_t N000046BF; //0x0704
	uint32_t N00003AD8; //0x0708
	uint32_t N000046C1; //0x070C
	uint32_t N00003AD9; //0x0710
	uint32_t N000046C3; //0x0714
	uint32_t N00003ADA; //0x0718
	uint32_t N000046C5; //0x071C
	uint32_t N00003ADB; //0x0720
	uint32_t N000046C7; //0x0724
	uint32_t N00003ADC; //0x0728
	uint32_t N000046C9; //0x072C
	uint32_t N00003ADD; //0x0730
	uint32_t N000046CB; //0x0734
	uint32_t N00003ADE; //0x0738
	uint32_t N000046CD; //0x073C
	uint32_t N00003ADF; //0x0740
	uint32_t N000046CF; //0x0744
	uint32_t N00003AE0; //0x0748
	uint32_t N000046D1; //0x074C
	uint32_t N00003AE1; //0x0750
	uint32_t N000046D3; //0x0754
	uint32_t N00003AE2; //0x0758
	uint32_t N000046D5; //0x075C
	uint32_t N00003AE3; //0x0760
	uint32_t N000046D7; //0x0764
	uint32_t N00003AE4; //0x0768
	uint32_t N000046D9; //0x076C
	uint32_t N00003AE5; //0x0770
	uint32_t N000046DB; //0x0774
	uint32_t N00003AE6; //0x0778
	uint32_t N000046DD; //0x077C
	uint32_t N00003AE7; //0x0780
	uint32_t N000046DF; //0x0784
	uint32_t N00003AE8; //0x0788
	uint32_t N000046E1; //0x078C
	uint32_t N00003AE9; //0x0790
	uint32_t N000046E3; //0x0794
	uint32_t N00003AEA; //0x0798
	uint32_t N000046E5; //0x079C
	uint32_t N00003AEB; //0x07A0
	uint32_t N000046E7; //0x07A4
	uint32_t N00003AEC; //0x07A8
	uint32_t N000046E9; //0x07AC
	uint32_t N00003AED; //0x07B0
	uint32_t N000046EB; //0x07B4
	uint32_t N00003AEE; //0x07B8
	uint32_t N000046ED; //0x07BC
	uint32_t N00003AEF; //0x07C0
	uint32_t N000046EF; //0x07C4
	uint32_t N00003AF0; //0x07C8
	uint32_t N000046F1; //0x07CC
	uint32_t N00003AF1; //0x07D0
	uint32_t N000046F3; //0x07D4
	uint32_t N00003AF2; //0x07D8
	uint32_t N000046F5; //0x07DC
	uint32_t N00003AF3; //0x07E0
	uint32_t N000046F7; //0x07E4
	uint32_t N00003AF4; //0x07E8
	uint32_t N000046F9; //0x07EC
	uint32_t N00003AF5; //0x07F0
	uint32_t N000046FB; //0x07F4
	uint32_t N00003AF6; //0x07F8
	uint32_t N000046FD; //0x07FC
	uint32_t N00003AF7; //0x0800
	uint32_t N000046FF; //0x0804
	uint32_t N00003AF8; //0x0808
	uint32_t N00004701; //0x080C
	uint32_t N00003AF9; //0x0810
	uint32_t N00004703; //0x0814
	uint32_t N00003AFA; //0x0818
	uint32_t N00004705; //0x081C
	uint32_t N00003AFB; //0x0820
	uint32_t N00004707; //0x0824
	uint32_t N00003AFC; //0x0828
	uint32_t N00004709; //0x082C
	uint32_t N00003AFD; //0x0830
	uint32_t N0000470B; //0x0834
	uint32_t N00003AFE; //0x0838
	uint32_t N0000470D; //0x083C
	uint32_t N00003AFF; //0x0840
	uint32_t N0000470F; //0x0844
	uint32_t N00003B00; //0x0848
	uint32_t N00004711; //0x084C
	uint32_t N00003B01; //0x0850
	uint32_t N00004713; //0x0854
	uint32_t N00003B02; //0x0858
	uint32_t N00004715; //0x085C
	uint32_t N00003B03; //0x0860
	uint32_t N00004717; //0x0864
	uint32_t N00003B04; //0x0868
	uint32_t N00004719; //0x086C
	uint32_t N00003B05; //0x0870
	uint32_t N0000471B; //0x0874
	uint32_t N00003B06; //0x0878
	uint32_t N0000471D; //0x087C
	uint32_t N00003B07; //0x0880
	uint32_t N0000471F; //0x0884
	uint32_t N00003B08; //0x0888
	uint32_t N00004721; //0x088C
	uint32_t N00003B09; //0x0890
	uint32_t N00004723; //0x0894
	uint32_t N00003B0A; //0x0898
	uint32_t N00004725; //0x089C
	uint32_t N00003B0B; //0x08A0
	uint32_t N00004727; //0x08A4
	uint32_t N00003B0C; //0x08A8
	uint32_t N00004729; //0x08AC
	uint32_t N00003B0D; //0x08B0
	uint32_t N0000472B; //0x08B4
	uint32_t N00003B0E; //0x08B8
	uint32_t N0000472D; //0x08BC
	uint32_t N00003B0F; //0x08C0
	uint32_t N0000472F; //0x08C4
	uint32_t N00003B10; //0x08C8
	uint32_t N00004731; //0x08CC
	uint32_t N00003B11; //0x08D0
	uint32_t N00004733; //0x08D4
	uint32_t N00003B12; //0x08D8
	uint32_t N00004735; //0x08DC
	uint32_t N00003B13; //0x08E0
	uint32_t N00004737; //0x08E4
	uint32_t N00003B14; //0x08E8
	uint32_t N00004739; //0x08EC
	uint32_t N00003B15; //0x08F0
	uint32_t N0000473B; //0x08F4
	uint32_t N00003B16; //0x08F8
	uint32_t N0000473D; //0x08FC
	uint32_t N00003B17; //0x0900
	uint32_t N0000473F; //0x0904
	uint32_t N00003B18; //0x0908
	uint32_t N00004741; //0x090C
	uint32_t N00003B19; //0x0910
	uint32_t N00004743; //0x0914
	uint32_t N00003B1A; //0x0918
	uint32_t N00004745; //0x091C
	uint32_t N00003B1B; //0x0920
	uint32_t N00004747; //0x0924
	uint32_t N00003B1C; //0x0928
	uint32_t N00004749; //0x092C
	uint32_t N00003B1D; //0x0930
	uint32_t N0000474B; //0x0934
	uint32_t N00003B1E; //0x0938
	uint32_t N0000474D; //0x093C
	uint32_t N00003B1F; //0x0940
	uint32_t N0000474F; //0x0944
	uint32_t N00003B20; //0x0948
	uint32_t N00004751; //0x094C
	uint32_t N00003B21; //0x0950
	uint32_t N00004753; //0x0954
	uint32_t N00003B22; //0x0958
	uint32_t N00004755; //0x095C
	uint32_t N00003B23; //0x0960
	uint32_t N00004757; //0x0964
	uint32_t N00003B24; //0x0968
	uint32_t N00004759; //0x096C
	uint32_t N00003B25; //0x0970
	uint32_t N0000475B; //0x0974
	uint32_t N00003B26; //0x0978
	uint32_t N0000475D; //0x097C
	uint32_t N00003B27; //0x0980
	uint32_t N0000475F; //0x0984
	uint32_t N00003B28; //0x0988
	uint32_t N00004761; //0x098C
	uint32_t N00003B29; //0x0990
	uint32_t N00004763; //0x0994
	uint32_t N00003B2A; //0x0998
	uint32_t N00004765; //0x099C
	uint32_t N00003B2B; //0x09A0
	uint32_t N00004767; //0x09A4
	uint32_t N00003B2C; //0x09A8
	uint32_t N00004769; //0x09AC
	uint32_t N00003B2D; //0x09B0
	uint32_t N0000476B; //0x09B4
	uint32_t N00003B2E; //0x09B8
	uint32_t N0000476D; //0x09BC
	uint32_t N00003B2F; //0x09C0
	uint32_t N0000476F; //0x09C4
	uint32_t N00003B30; //0x09C8
	uint32_t N00004771; //0x09CC
	uint32_t N00003B31; //0x09D0
	uint32_t N00004773; //0x09D4
	uint32_t N00003B32; //0x09D8
	uint32_t N00004775; //0x09DC
	uint32_t N00003B33; //0x09E0
	uint32_t N00004777; //0x09E4
	uint32_t N00003B34; //0x09E8
	uint32_t N00004779; //0x09EC
	uint32_t N00003B35; //0x09F0
	uint32_t N0000477B; //0x09F4
	uint32_t N00003B36; //0x09F8
	uint32_t N0000477D; //0x09FC
	uint32_t N00003B37; //0x0A00
	uint32_t N0000477F; //0x0A04
	uint32_t N00003B38; //0x0A08
	uint32_t N00004781; //0x0A0C
	uint32_t N00003B39; //0x0A10
	uint32_t N00004783; //0x0A14
	uint32_t N00003B3A; //0x0A18
	uint32_t N00004785; //0x0A1C
	uint32_t N00003B3B; //0x0A20
	uint32_t N00004787; //0x0A24
	uint32_t N00003B3C; //0x0A28
	uint32_t N00004789; //0x0A2C
	uint32_t N00003B3D; //0x0A30
	uint32_t N0000478B; //0x0A34
	uint32_t N00003B3E; //0x0A38
	uint32_t N0000478D; //0x0A3C
	uint32_t N00003B3F; //0x0A40
	uint32_t N0000478F; //0x0A44
	uint32_t N00003B40; //0x0A48
	uint32_t N00004791; //0x0A4C
	uint32_t N00003B41; //0x0A50
	uint32_t N00004793; //0x0A54
	uint32_t N00003B42; //0x0A58
	uint32_t N00004795; //0x0A5C
	uint32_t N00003B43; //0x0A60
	uint32_t N00004797; //0x0A64
	uint32_t N00003B44; //0x0A68
	uint32_t N00004799; //0x0A6C
	uint32_t N00003B45; //0x0A70
	uint32_t N0000479B; //0x0A74
	uint32_t N00003B46; //0x0A78
	uint32_t N0000479D; //0x0A7C
	uint32_t N00003B47; //0x0A80
	uint32_t N0000479F; //0x0A84
	uint32_t N00003B48; //0x0A88
	uint32_t N000047A1; //0x0A8C
	uint32_t N00003B49; //0x0A90
	uint32_t N000047A3; //0x0A94
	uint32_t N00003B4A; //0x0A98
	uint32_t N000047A5; //0x0A9C
	uint32_t N00003B4B; //0x0AA0
	uint32_t N000047A7; //0x0AA4
	uint32_t N00003B4C; //0x0AA8
	uint32_t N000047A9; //0x0AAC
	uint32_t N00003B4D; //0x0AB0
	uint32_t N000047AB; //0x0AB4
	uint32_t N00003B4E; //0x0AB8
	uint32_t N000047AD; //0x0ABC
	uint32_t N00003B4F; //0x0AC0
	uint32_t N000047AF; //0x0AC4
	uint32_t N00003B50; //0x0AC8
	uint32_t N000047B1; //0x0ACC
	uint32_t N00003B51; //0x0AD0
	uint32_t N000047B3; //0x0AD4
	uint32_t N00003B52; //0x0AD8
	uint32_t N000047B5; //0x0ADC
	uint32_t N00003B53; //0x0AE0
	uint32_t N000047B7; //0x0AE4
	uint32_t N00003B54; //0x0AE8
	uint32_t N000047B9; //0x0AEC
	uint32_t N00003B55; //0x0AF0
	uint32_t N000047BB; //0x0AF4
	uint32_t N00003B56; //0x0AF8
	uint32_t N000047BD; //0x0AFC
	uint32_t N00003B57; //0x0B00
	uint32_t N000047BF; //0x0B04
	uint32_t N00003B58; //0x0B08
	uint32_t N000047C1; //0x0B0C
	uint32_t N00003B59; //0x0B10
	uint32_t N000047C3; //0x0B14
	uint32_t N00003B5A; //0x0B18
	uint32_t N000047C5; //0x0B1C
	uint32_t N00003B5B; //0x0B20
	uint32_t N000047C7; //0x0B24
	uint32_t N00003B5C; //0x0B28
	uint32_t N000047C9; //0x0B2C
	uint32_t N00003B5D; //0x0B30
	uint32_t N000047CB; //0x0B34
	uint32_t N00003B5E; //0x0B38
	uint32_t N000047CD; //0x0B3C
	uint32_t N00003B5F; //0x0B40
	uint32_t N000047CF; //0x0B44
	uint32_t N00003B60; //0x0B48
	uint32_t N000047D1; //0x0B4C
	uint32_t N00003B61; //0x0B50
	uint32_t N000047D3; //0x0B54
	uint32_t N00003B62; //0x0B58
	uint32_t N000047D5; //0x0B5C
	uint32_t N00003B63; //0x0B60
	uint32_t N000047D7; //0x0B64
	uint32_t N00003B64; //0x0B68
	uint32_t N000047D9; //0x0B6C
	uint32_t N00003B65; //0x0B70
	uint32_t N000047DB; //0x0B74
	uint32_t N00003B66; //0x0B78
	uint32_t N000047DD; //0x0B7C
	uint32_t N00003B67; //0x0B80
	uint32_t N000047DF_y; //0x0B84
	uint32_t N00003B68; //0x0B88
	uint32_t N000047E1; //0x0B8C
	uint32_t N00003B69; //0x0B90
	uint32_t N000047E3; //0x0B94
	uint32_t N00003B6A; //0x0B98
	uint32_t N000047E5; //0x0B9C
	uint32_t N00003B6B; //0x0BA0
	uint32_t N000047E7; //0x0BA4
	uint32_t N00003B6C; //0x0BA8
	uint32_t N000047E9; //0x0BAC
	uint32_t N00003B6D; //0x0BB0
	uint32_t N000047EB; //0x0BB4
	uint32_t N00003B6E; //0x0BB8
	uint32_t N000047ED; //0x0BBC
	uint32_t N00003B6F; //0x0BC0
	uint32_t N000047EF; //0x0BC4
	uint32_t N00003B70; //0x0BC8
	uint32_t N000047F1; //0x0BCC
	uint32_t N00003B71; //0x0BD0
	uint32_t N000047F3; //0x0BD4
	uint32_t N00003B72; //0x0BD8
	uint32_t N000047F5; //0x0BDC
	uint32_t N00003B73; //0x0BE0
	uint32_t N000047F7; //0x0BE4
	uint32_t N00003B74; //0x0BE8
	uint32_t N000047F9; //0x0BEC
	uint32_t N00003B75; //0x0BF0
	uint32_t N000047FB; //0x0BF4
	uint32_t N00003B76; //0x0BF8
	uint32_t N000047FD; //0x0BFC
	uint32_t N00003B77; //0x0C00
	uint32_t N000047FF; //0x0C04
	uint32_t N00003B78; //0x0C08
	uint32_t N00004801; //0x0C0C
	uint32_t N00003B79; //0x0C10
	uint32_t N00004803; //0x0C14
	uint32_t N00003B7A; //0x0C18
	uint32_t N00004805; //0x0C1C
	uint32_t N00003B7B; //0x0C20
	uint32_t N00004807; //0x0C24
	uint32_t N00003B7C; //0x0C28
	uint32_t N00004809; //0x0C2C
	uint32_t N00003B7D; //0x0C30
	uint32_t N0000480B; //0x0C34
	uint32_t N00003B7E; //0x0C38
	uint32_t N0000480D; //0x0C3C
	uint32_t N00003B7F; //0x0C40
	uint32_t N0000480F; //0x0C44
	uint32_t N00003B80; //0x0C48
	uint32_t N00004811; //0x0C4C
	uint32_t N00003B81; //0x0C50
	uint32_t N00004813; //0x0C54
	uint32_t N00003B82; //0x0C58
	uint32_t N00004815; //0x0C5C
	uint32_t N00003B83; //0x0C60
	uint32_t N00004817; //0x0C64
	uint32_t N00003B84; //0x0C68
	uint32_t N00004819; //0x0C6C
	uint32_t N00003B85; //0x0C70
	uint32_t N0000481B; //0x0C74
	uint32_t N00003B86; //0x0C78
	uint32_t N0000481D; //0x0C7C
	uint32_t N00003B87; //0x0C80
	uint32_t N0000481F; //0x0C84
	uint32_t N00003B88; //0x0C88
	uint32_t N00004821; //0x0C8C
	uint32_t N00003B89; //0x0C90
	uint32_t N00004823; //0x0C94
	uint32_t N00003B8A; //0x0C98
	uint32_t N00004825; //0x0C9C
	uint32_t N00003B8B; //0x0CA0
	uint32_t N00004827; //0x0CA4
	uint32_t N00003B8C; //0x0CA8
	uint32_t N00004829; //0x0CAC
	uint32_t N00003B8D; //0x0CB0
	uint32_t N0000482B; //0x0CB4
	uint32_t N00003B8E; //0x0CB8
	uint32_t N0000482D; //0x0CBC
	uint32_t N00003B8F; //0x0CC0
	uint32_t N0000482F; //0x0CC4
	uint32_t N00003B90; //0x0CC8
	uint32_t N00004831; //0x0CCC
	uint32_t N00003B91; //0x0CD0
	uint32_t N00004833; //0x0CD4
	uint32_t N00003B92; //0x0CD8
	uint32_t N00004835; //0x0CDC
	uint32_t N00003B93; //0x0CE0
	uint32_t N00004837; //0x0CE4
	uint32_t N00003B94; //0x0CE8
	uint32_t N00004839; //0x0CEC
	uint32_t N00003B95; //0x0CF0
	uint32_t N0000483B; //0x0CF4
	uint32_t N00003B96; //0x0CF8
	uint32_t N0000483D; //0x0CFC
	uint32_t N00003B97; //0x0D00
	uint32_t N0000483F; //0x0D04
	uint32_t N00003B98; //0x0D08
	uint32_t N00004841; //0x0D0C
	uint32_t N00003B99; //0x0D10
	uint32_t N00004843; //0x0D14
	uint32_t N00003B9A; //0x0D18
	uint32_t N00004845; //0x0D1C
	uint32_t N00003B9B; //0x0D20
	uint32_t N00004847; //0x0D24
	uint32_t N00003B9C; //0x0D28
	uint32_t N00004849; //0x0D2C
	uint32_t N00003B9D; //0x0D30
	uint32_t N0000484B; //0x0D34
	uint32_t N00003B9E; //0x0D38
	uint32_t N0000484D; //0x0D3C
	uint32_t N00003B9F; //0x0D40
	uint32_t N0000484F; //0x0D44
	uint32_t N00003BA0; //0x0D48
	uint32_t N00004851; //0x0D4C
	uint32_t N00003BA1; //0x0D50
	uint32_t N00004853; //0x0D54
	uint32_t N00003BA2; //0x0D58
	uint32_t N00004855; //0x0D5C
	uint32_t N00003BA3; //0x0D60
	uint32_t N00004857; //0x0D64
	uint32_t N00003BA4; //0x0D68
	uint32_t N00004859; //0x0D6C
	uint32_t N00003BA5; //0x0D70
	uint32_t N0000485B; //0x0D74
	uint32_t N00003BA6; //0x0D78
	uint32_t N0000485D; //0x0D7C
	uint32_t N00003BA7; //0x0D80
	uint32_t N0000485F; //0x0D84
	uint32_t N00003BA8; //0x0D88
	uint32_t N00004861; //0x0D8C
	uint32_t N00003BA9; //0x0D90
	uint32_t N00004863; //0x0D94
	uint32_t N00003BAA; //0x0D98
	uint32_t N00004865; //0x0D9C
	uint32_t N00003BAB; //0x0DA0
	uint32_t N00004867; //0x0DA4
	uint32_t N00003BAC; //0x0DA8
	uint32_t N00004869; //0x0DAC
	uint32_t N00003BAD; //0x0DB0
	uint32_t N0000486B; //0x0DB4
	uint32_t N00003BAE; //0x0DB8
	uint32_t N0000486D; //0x0DBC
	uint32_t N00003BAF; //0x0DC0
	uint32_t N0000486F; //0x0DC4
	uint32_t N00003BB0; //0x0DC8
	uint32_t N00004871; //0x0DCC
	uint32_t N00003BB1; //0x0DD0
	uint32_t N00004873; //0x0DD4
	uint32_t N00003BB2; //0x0DD8
	uint32_t N00004875; //0x0DDC
	uint32_t N00003BB3; //0x0DE0
	uint32_t N00004877; //0x0DE4
	uint32_t N00003BB4; //0x0DE8
	uint32_t N00004879; //0x0DEC
	uint32_t N00003BB5; //0x0DF0
	uint32_t N0000487B; //0x0DF4
	uint32_t N00003BB6; //0x0DF8
	uint32_t N0000487D; //0x0DFC
	uint32_t N00003BB7; //0x0E00
	uint32_t N0000487F; //0x0E04
	uint32_t N00003BB8; //0x0E08
	uint32_t N00004881; //0x0E0C
	uint32_t N00003BB9; //0x0E10
	uint32_t N00004883; //0x0E14
	uint32_t N00003BBA; //0x0E18
	uint32_t N00004885; //0x0E1C
	uint32_t N00003BBB; //0x0E20
	uint32_t N00004887; //0x0E24
	uint32_t N00003BBC; //0x0E28
	uint32_t N00004889; //0x0E2C
	uint32_t N00003BBD; //0x0E30
	uint32_t N0000488B; //0x0E34
	uint32_t N00003BBE; //0x0E38
	uint32_t N0000488D; //0x0E3C
	uint32_t N00003BBF; //0x0E40
	uint32_t N0000488F; //0x0E44
	uint32_t N00003BC0; //0x0E48
	uint32_t N00004891; //0x0E4C
	uint32_t N00003BC1; //0x0E50
	uint32_t N00004893; //0x0E54
	uint32_t N00003BC2; //0x0E58
	uint32_t N00004895; //0x0E5C
	uint32_t N00003BC3; //0x0E60
	uint32_t N00004897; //0x0E64
	uint32_t N00003BC4; //0x0E68
	uint32_t N00004899; //0x0E6C
	uint32_t N00003BC5; //0x0E70
	uint32_t N0000489B; //0x0E74
	uint32_t N00003BC6; //0x0E78
	uint32_t N0000489D; //0x0E7C
	uint32_t N00003BC7; //0x0E80
	uint32_t N0000489F; //0x0E84
	uint32_t N00003BC8; //0x0E88
	uint32_t N000048A1; //0x0E8C
	uint32_t N00003BC9; //0x0E90
	uint32_t N000048A3; //0x0E94
	uint32_t N00003BCA; //0x0E98
	uint32_t N000048A5; //0x0E9C
	uint32_t N00003BCB; //0x0EA0
	uint32_t N000048A7; //0x0EA4
	uint32_t N00003BCC; //0x0EA8
	uint32_t N000048A9; //0x0EAC
	uint32_t N00003BCD; //0x0EB0
	uint32_t N000048AB; //0x0EB4
	uint32_t N00003BCE; //0x0EB8
	uint32_t N000048AD; //0x0EBC
	uint32_t N00003BCF; //0x0EC0
	uint32_t N000048AF; //0x0EC4
	uint32_t N00003BD0; //0x0EC8
	uint32_t N000048B1; //0x0ECC
	uint32_t N00003BD1; //0x0ED0
	uint32_t N000048B3; //0x0ED4
	uint32_t N00003BD2; //0x0ED8
	uint32_t N000048B5; //0x0EDC
	uint32_t N00003BD3; //0x0EE0
	uint32_t N000048B7; //0x0EE4
	uint32_t N00003BD4; //0x0EE8
	uint32_t N000048B9; //0x0EEC
	uint32_t N00003BD5; //0x0EF0
	uint32_t N000048BB; //0x0EF4
	uint32_t N00003BD6; //0x0EF8
	uint32_t N000048BD; //0x0EFC
	uint32_t N00003BD7; //0x0F00
	uint32_t N000048BF; //0x0F04
	uint32_t N00003BD8; //0x0F08
	uint32_t N000048C1; //0x0F0C
	uint32_t N00003BD9; //0x0F10
	uint32_t N000048C3; //0x0F14
	uint32_t N00003BDA; //0x0F18
	uint32_t N000048C5; //0x0F1C
	uint32_t N00003BDB; //0x0F20
	uint32_t N000048C7; //0x0F24
	uint32_t N00003BDC; //0x0F28
	uint32_t N000048C9; //0x0F2C
	uint32_t N00003BDD; //0x0F30
	uint32_t N000048CB; //0x0F34
	uint32_t N00003BDE; //0x0F38
	uint32_t N000048CD; //0x0F3C
	uint32_t N00003BDF; //0x0F40
	uint32_t N000048CF; //0x0F44
	uint32_t N00003BE0; //0x0F48
	uint32_t N000048D1; //0x0F4C
	uint32_t N00003BE1; //0x0F50
	uint32_t N000048D3; //0x0F54
	uint32_t N00003BE2; //0x0F58
	uint32_t N000048D5; //0x0F5C
	uint32_t N00003BE3; //0x0F60
	uint32_t N000048D7; //0x0F64
	uint32_t N00003BE4; //0x0F68
	uint32_t N000048D9; //0x0F6C
	uint32_t N00003BE5; //0x0F70
	uint32_t N000048DB; //0x0F74
	uint32_t N00003BE6; //0x0F78
	uint32_t N000048DD; //0x0F7C
	uint32_t N00003BE7; //0x0F80
	uint32_t N000048DF; //0x0F84
	uint32_t N00003BE8; //0x0F88
	uint32_t N000048E1; //0x0F8C
	uint32_t N00003BE9; //0x0F90
	uint32_t N000048E3; //0x0F94
	uint32_t N00003BEA; //0x0F98
	uint32_t N000048E5; //0x0F9C
	uint32_t N00003BEB; //0x0FA0
	uint32_t N000048E7; //0x0FA4
	uint32_t N00003BEC; //0x0FA8
	uint32_t N000048E9; //0x0FAC
	uint32_t N00003BED; //0x0FB0
	uint32_t N000048EB; //0x0FB4
	uint32_t N00003BEE; //0x0FB8
	uint32_t N000048ED; //0x0FBC
	uint32_t N00003BEF; //0x0FC0
	uint32_t N000048EF; //0x0FC4
	uint32_t N00003BF0; //0x0FC8
	uint32_t N000048F1; //0x0FCC
	uint32_t N00003BF1; //0x0FD0
	uint32_t N000048F3; //0x0FD4
	uint32_t N00003BF2; //0x0FD8
	uint32_t N000048F5; //0x0FDC
	uint32_t N00003BF3; //0x0FE0
	uint32_t N000048F7; //0x0FE4
	uint32_t N00003BF4; //0x0FE8
	uint32_t N000048F9; //0x0FEC
	uint32_t N00003BF5; //0x0FF0
	uint32_t N000048FB; //0x0FF4
	uint32_t N00003BF6; //0x0FF8
	uint32_t N000048FD; //0x0FFC
	uint32_t N00003BF7; //0x1000
	uint32_t N000048FF; //0x1004
	uint32_t N00003BF8; //0x1008
	uint32_t N00004901; //0x100C
	uint32_t N00003BF9; //0x1010
	uint32_t N00004903; //0x1014
	uint32_t N00003BFA; //0x1018
	uint32_t N00004905; //0x101C
	uint32_t N00003BFB; //0x1020
	uint32_t N00004907; //0x1024
	uint32_t N00003BFC; //0x1028
	uint32_t N00004909; //0x102C
	uint32_t N00003BFD; //0x1030
	uint32_t N0000490B; //0x1034
	uint32_t N00003BFE; //0x1038
	uint32_t N0000490D; //0x103C
	uint32_t N00003BFF; //0x1040
	uint32_t N0000490F; //0x1044
	uint32_t N00003C00; //0x1048
	uint32_t N00004911; //0x104C
	uint32_t N00003C01; //0x1050
	uint32_t N00004913; //0x1054
	uint32_t N00003C02; //0x1058
	uint32_t N00004915; //0x105C
	uint32_t N00003C03; //0x1060
	uint32_t N00004917; //0x1064
	uint32_t N00003C04; //0x1068
	uint32_t N00004919; //0x106C
	uint32_t N00003C05; //0x1070
	uint32_t N0000491B; //0x1074
	uint32_t N00003C06; //0x1078
	uint32_t N0000491D; //0x107C
	uint32_t N00003C07; //0x1080
	uint32_t N0000491F; //0x1084
	uint32_t N00003C08; //0x1088
	uint32_t N00004921; //0x108C
	uint32_t N00003C09; //0x1090
	uint32_t N00004923; //0x1094
	uint32_t N00003C0A; //0x1098
	uint32_t N00004925; //0x109C
	uint32_t N00003C0B; //0x10A0
	uint32_t N00004927; //0x10A4
	uint32_t N00003C0C; //0x10A8
	uint32_t N00004929; //0x10AC
	uint32_t N00003C0D; //0x10B0
	uint32_t N0000492B; //0x10B4
	uint32_t N00003C0E; //0x10B8
	uint32_t N0000492D; //0x10BC
	uint32_t N00003C0F; //0x10C0
	uint32_t N0000492F; //0x10C4
	uint32_t N00003C10; //0x10C8
	uint32_t N00004931; //0x10CC
	uint32_t N00003C11; //0x10D0
	uint32_t N00004933; //0x10D4
	uint32_t N00003C12; //0x10D8
	uint32_t N00004935; //0x10DC
	uint32_t N00003C13; //0x10E0
	uint32_t N00004937; //0x10E4
	uint32_t N00003C14; //0x10E8
	uint32_t N00004939; //0x10EC
	uint32_t N00003C15; //0x10F0
	uint32_t N0000493B; //0x10F4
	uint32_t N00003C16; //0x10F8
	uint32_t N0000493D; //0x10FC
	uint32_t N00003C17; //0x1100
	uint32_t N0000493F; //0x1104
	uint32_t N00003C18; //0x1108
	uint32_t N00004941; //0x110C
	uint32_t N00003C19; //0x1110
	uint32_t N00004943; //0x1114
	uint32_t N00003C1A; //0x1118
	uint32_t N00004945; //0x111C
	uint32_t N00003C1B; //0x1120
	uint32_t N00004947; //0x1124
	uint32_t N00003C1C; //0x1128
	uint32_t N00004949; //0x112C
	uint32_t N00003C1D; //0x1130
	uint32_t N0000494B; //0x1134
	uint32_t N00003C1E; //0x1138
	uint32_t N0000494D; //0x113C
	uint32_t N00003C1F; //0x1140
	uint32_t N0000494F; //0x1144
	uint32_t N00003C20; //0x1148
	uint32_t N00004951; //0x114C
	uint32_t N00003C21; //0x1150
	uint32_t N00004953; //0x1154
	uint32_t N00003C22; //0x1158
	uint32_t N00004955; //0x115C
	uint32_t N00003C23; //0x1160
	uint32_t N00004957; //0x1164
	uint32_t N00003C24; //0x1168
	uint32_t N00004959; //0x116C
	uint32_t N00003C25; //0x1170
	uint32_t N0000495B; //0x1174
	uint32_t N00003C26; //0x1178
	uint32_t N0000495D; //0x117C
	uint32_t N00003C27; //0x1180
	uint32_t N0000495F; //0x1184
	uint32_t N00003C28; //0x1188
	uint32_t N00004961; //0x118C
	uint32_t N00003C29; //0x1190
	uint32_t N00004963; //0x1194
	uint32_t N00003C2A; //0x1198
	uint32_t N00004965; //0x119C
	uint32_t N00003C2B; //0x11A0
	uint32_t N00004967; //0x11A4
	uint32_t N00003C2C; //0x11A8
	uint32_t N00004969; //0x11AC
	uint32_t N00003C2D; //0x11B0
	uint32_t N0000496B; //0x11B4
	uint32_t N00003C2E; //0x11B8
	uint32_t N0000496D; //0x11BC
	uint32_t N00003C2F; //0x11C0
	uint32_t N0000496F; //0x11C4
	uint32_t N00003C30; //0x11C8
	uint32_t N00004971; //0x11CC
	uint32_t N00003C31; //0x11D0
	uint32_t N00004973; //0x11D4
	uint32_t N00003C32; //0x11D8
	uint32_t N00004975; //0x11DC
	uint32_t N00003C33; //0x11E0
	uint32_t N00004977; //0x11E4
	uint32_t N00003C34; //0x11E8
	uint32_t N00004979; //0x11EC
	uint32_t N00003C35; //0x11F0
	uint32_t N0000497B; //0x11F4
	uint32_t N00003C36; //0x11F8
	uint32_t N0000497D; //0x11FC
	uint32_t N00003C37; //0x1200
	uint32_t N0000497F; //0x1204
	uint32_t N00003C38; //0x1208
	uint32_t N00004981; //0x120C
	uint32_t N00003C39; //0x1210
	uint32_t N00004983; //0x1214
	uint32_t N00003C3A; //0x1218
	uint32_t N00004985; //0x121C
	uint32_t N00003C3B; //0x1220
	uint32_t N00004987; //0x1224
	uint32_t N00003C3C; //0x1228
	uint32_t N00004989; //0x122C
	uint32_t N00003C3D; //0x1230
	uint32_t N0000498B; //0x1234
	uint32_t N00003C3E; //0x1238
	uint32_t N0000498D; //0x123C
	uint32_t N00003C3F; //0x1240
	uint32_t N0000498F; //0x1244
	uint32_t N00003C40; //0x1248
	uint32_t N00004991; //0x124C
	uint32_t N00003C41; //0x1250
	uint32_t N00004993; //0x1254
	uint32_t N00003C42; //0x1258
	uint32_t N00004995; //0x125C
	uint32_t N00003C43; //0x1260
	uint32_t N00004997; //0x1264
	uint32_t N00003C44; //0x1268
	uint32_t N00004999; //0x126C
	uint32_t N00003C45; //0x1270
	uint32_t N0000499B; //0x1274
	uint32_t N00003C46; //0x1278
	uint32_t N0000499D; //0x127C
	uint32_t N00003C47; //0x1280
	uint32_t N0000499F; //0x1284
	uint32_t N00003C48; //0x1288
	uint32_t N000049A1; //0x128C
	uint32_t N00003C49; //0x1290
	uint32_t N000049A3; //0x1294
	uint32_t N00003C4A; //0x1298
	uint32_t N000049A5; //0x129C
	uint32_t N00003C4B; //0x12A0
	uint32_t N000049A7; //0x12A4
	uint32_t N00003C4C; //0x12A8
	uint32_t N000049A9; //0x12AC
	uint32_t N00003C4D; //0x12B0
	uint32_t N000049AB; //0x12B4
	uint32_t N00003C4E; //0x12B8
	uint32_t N000049AD; //0x12BC
	uint32_t N00003C4F; //0x12C0
	uint32_t N000049AF; //0x12C4
	uint32_t N00003C50; //0x12C8
	uint32_t N000049B1; //0x12CC
	uint32_t N00003C51; //0x12D0
	uint32_t N000049B3; //0x12D4
	uint32_t N00003C52; //0x12D8
	uint32_t N000049B5; //0x12DC
	uint32_t N00003C53; //0x12E0
	uint32_t N000049B7; //0x12E4
	uint32_t N00003C54; //0x12E8
	uint32_t N000049B9; //0x12EC
	uint32_t N00003C55; //0x12F0
	uint32_t N000049BB; //0x12F4
	uint32_t N00003C56; //0x12F8
	uint32_t N000049BD; //0x12FC
	uint32_t N00003C57; //0x1300
	uint32_t N000049BF; //0x1304
	uint32_t N00003C58; //0x1308
	uint32_t N000049C1; //0x130C
	uint32_t N00003C59; //0x1310
	uint32_t N000049C3; //0x1314
	uint32_t N00003C5A; //0x1318
	uint32_t N000049C5; //0x131C
	uint32_t N00003C5B; //0x1320
	uint32_t N000049C7; //0x1324
	uint32_t N00003C5C; //0x1328
	uint32_t N000049C9; //0x132C
	uint32_t N00003C5D; //0x1330
	uint32_t N000049CB; //0x1334
	uint32_t N00003C5E; //0x1338
	uint32_t N000049CD; //0x133C
	uint32_t N00003C5F; //0x1340
	uint32_t N000049CF; //0x1344
	uint32_t N00003C60; //0x1348
	uint32_t N000049D1; //0x134C
	uint32_t N00003C61; //0x1350
	uint32_t N000049D3; //0x1354
	uint32_t N00003C62; //0x1358
	uint32_t N000049D5; //0x135C
	uint32_t N00003C63; //0x1360
	uint32_t N000049D7; //0x1364
	uint32_t N00003C64; //0x1368
	uint32_t N000049D9; //0x136C
	uint32_t N00003C65; //0x1370
	uint32_t N000049DB; //0x1374
	uint32_t N00003C66; //0x1378
	uint32_t N000049DD; //0x137C
	uint32_t N00003C67; //0x1380
	uint32_t N000049DF; //0x1384
	uint32_t N00003C68; //0x1388
	uint32_t N000049E1; //0x138C
	uint32_t N00003C69; //0x1390
	uint32_t N000049E3; //0x1394
	uint32_t N00003C6A; //0x1398
	uint32_t N000049E5; //0x139C
	uint32_t N00003C6B; //0x13A0
	uint32_t N000049E7; //0x13A4
	uint32_t N00003C6C; //0x13A8
	uint32_t N000049E9; //0x13AC
	uint32_t N00003C6D; //0x13B0
	uint32_t N000049EB; //0x13B4
	uint32_t N00003C6E; //0x13B8
	uint32_t N000049ED; //0x13BC
	uint32_t N00003C6F; //0x13C0
	uint32_t N000049EF; //0x13C4
	uint32_t N00003C70; //0x13C8
	uint32_t N000049F1; //0x13CC
	uint32_t N00003C71; //0x13D0
	uint32_t N000049F3; //0x13D4
	uint32_t N00003C72; //0x13D8
	uint32_t N000049F5; //0x13DC
	uint32_t N00003C73; //0x13E0
	uint32_t N000049F7; //0x13E4
	uint32_t N00003C74; //0x13E8
	uint32_t N000049F9; //0x13EC
	uint32_t N00003C75; //0x13F0
	uint32_t N000049FB; //0x13F4
	uint32_t N00003C76; //0x13F8
	uint32_t N000049FD; //0x13FC
	uint32_t N00003C77; //0x1400
	uint32_t N000049FF; //0x1404
	uint32_t N00003C78; //0x1408
	uint32_t N00004A01; //0x140C
	uint32_t N00003C79; //0x1410
	uint32_t N00004A03; //0x1414
	uint32_t N00003C7A; //0x1418
	uint32_t N00004A05; //0x141C
	uint32_t N00003C7B; //0x1420
	uint32_t N00004A07; //0x1424
	uint32_t N00003C7C; //0x1428
	uint32_t N00004A09; //0x142C
	uint32_t N00003C7D; //0x1430
	uint32_t N00004A0B; //0x1434
	uint32_t N00003C7E; //0x1438
	uint32_t N00004A0D; //0x143C
	uint32_t N00003C7F; //0x1440
	uint32_t N00004A0F; //0x1444
	uint32_t N00003C80; //0x1448
	uint32_t N00004A11; //0x144C
	uint32_t N00003C81; //0x1450
	uint32_t N00004A13; //0x1454
	uint32_t N00003C82; //0x1458
	uint32_t N00004A15; //0x145C
	uint32_t N00003C83; //0x1460
	uint32_t N00004A17; //0x1464
	uint32_t N00003C84; //0x1468
	uint32_t N00004A19; //0x146C
	uint32_t N00003C85; //0x1470
	uint32_t N00004A1B; //0x1474
	uint32_t N00003C86; //0x1478
	uint32_t N00004A1D; //0x147C
	uint32_t N00003C87; //0x1480
	uint32_t N00004A1F; //0x1484
	uint32_t N00003C88; //0x1488
	uint32_t N00004A21; //0x148C
	uint32_t N00003C89; //0x1490
	uint32_t N00004A23; //0x1494
	uint32_t N00003C8A; //0x1498
	uint32_t N00004A25; //0x149C
	uint32_t N00003C8B; //0x14A0
	uint32_t N00004A27; //0x14A4
	uint32_t N00003C8C; //0x14A8
	uint32_t N00004A29; //0x14AC
	uint32_t N00003C8D; //0x14B0
	uint32_t N00004A2B; //0x14B4
	uint32_t N00003C8E; //0x14B8
	uint32_t N00004A2D; //0x14BC
	uint32_t N00003C8F; //0x14C0
	uint32_t N00004A2F; //0x14C4
	uint32_t N00003C90; //0x14C8
	uint32_t N00004A31; //0x14CC
	uint32_t N00003C91; //0x14D0
	uint32_t N00004A33; //0x14D4
	uint32_t N00003C92; //0x14D8
	uint32_t N00004A35; //0x14DC
	uint32_t N00003C93; //0x14E0
	uint32_t N00004A37; //0x14E4
	uint32_t N00003C94; //0x14E8
	uint32_t N00004A39; //0x14EC
	uint32_t N00003C95; //0x14F0
	uint32_t N00004A3B; //0x14F4
	uint32_t N00003C96; //0x14F8
	uint32_t N00004A3D; //0x14FC
	uint32_t N00003C97; //0x1500
	uint32_t N00004A3F; //0x1504
	uint32_t N00003C98; //0x1508
	uint32_t N00004A41; //0x150C
	uint32_t N00003C99; //0x1510
	uint32_t N00004A43; //0x1514
	uint32_t N00003C9A; //0x1518
	uint32_t N00004A45; //0x151C
	uint32_t N00003C9B; //0x1520
	uint32_t N00004A47; //0x1524
	uint32_t N00003C9C; //0x1528
	uint32_t N00004A49; //0x152C
	uint32_t N00003C9D; //0x1530
	uint32_t N00004A4B; //0x1534
	uint32_t N00003C9E; //0x1538
	uint32_t N00004A4D; //0x153C
	uint32_t N00003C9F; //0x1540
	uint32_t N00004A4F; //0x1544
	uint32_t N00003CA0; //0x1548
	uint32_t N00004A51; //0x154C
	uint32_t N00003CA1; //0x1550
	uint32_t N00004A53; //0x1554
	uint32_t N00003CA2; //0x1558
	uint32_t N00004A55; //0x155C
	uint32_t N00003CA3; //0x1560
	uint32_t N00004A57; //0x1564
	uint32_t N00003CA4; //0x1568
	uint32_t N00004A59; //0x156C
	uint32_t N00003CA5; //0x1570
	uint32_t N00004A5B; //0x1574
	uint32_t N00003CA6; //0x1578
	uint32_t N00004A5D; //0x157C
	uint32_t N00003CA7; //0x1580
	uint32_t N00004A5F; //0x1584
	uint32_t N00003CA8; //0x1588
	uint32_t N00004A61; //0x158C
	uint32_t N00003CA9; //0x1590
	uint32_t N00004A63; //0x1594
	uint32_t N00003CAA; //0x1598
	uint32_t N00004A65; //0x159C
	uint32_t N00003CAB; //0x15A0
	uint32_t N00004A67; //0x15A4
	uint32_t N00003CAC; //0x15A8
	uint32_t N00004A69; //0x15AC
	uint32_t N00003CAD; //0x15B0
	uint32_t N00004A6B; //0x15B4
	uint32_t N00003CAE; //0x15B8
	uint32_t N00004A6D; //0x15BC
	uint32_t N00003CAF; //0x15C0
	uint32_t N00004A6F; //0x15C4
	uint32_t N00003CB0; //0x15C8
	uint32_t N00004A71; //0x15CC
	uint32_t N00003CB1; //0x15D0
	uint32_t N00004A73; //0x15D4
	uint32_t N00003CB2; //0x15D8
	uint32_t N00004A75; //0x15DC
	uint32_t N00003CB3; //0x15E0
	uint32_t N00004A77; //0x15E4
	uint32_t N00003CB4; //0x15E8
	uint32_t N00004A79; //0x15EC
	uint32_t N00003CB5; //0x15F0
	uint32_t N00004A7B; //0x15F4
	uint32_t N00003CB6; //0x15F8
	uint32_t N00004A7D; //0x15FC
	uint32_t N00003CB7; //0x1600
	uint32_t N00004A7F; //0x1604
	uint32_t N00003CB8; //0x1608
	uint32_t N00004A81; //0x160C
	uint32_t N00003CB9; //0x1610
	uint32_t N00004A83; //0x1614
	uint32_t N00003CBA; //0x1618
	uint32_t N00004A85; //0x161C
	uint32_t N00003CBB; //0x1620
	uint32_t N00004A87; //0x1624
	uint32_t N00003CBC; //0x1628
	uint32_t N00004A89; //0x162C
	uint32_t N00003CBD; //0x1630
	uint32_t N00004A8B; //0x1634
	uint32_t N00003CBE; //0x1638
	uint32_t N00004A8D; //0x163C
	uint32_t N00003CBF; //0x1640
	uint32_t N00004A8F; //0x1644
	uint32_t N00003CC0; //0x1648
	uint32_t N00004A91; //0x164C
	uint32_t N00003CC1; //0x1650
	uint32_t N00004A93; //0x1654
	uint32_t N00003CC2; //0x1658
	uint32_t N00004A95; //0x165C
	uint32_t N00003CC3; //0x1660
	uint32_t N00004A97; //0x1664
	uint32_t N00003CC4; //0x1668
	uint32_t N00004A99; //0x166C
	uint32_t N00003CC5; //0x1670
	uint32_t N00004A9B; //0x1674
	uint32_t N00003CC6; //0x1678
	uint32_t N00004A9D; //0x167C
	uint32_t N00003CC7; //0x1680
	uint32_t N00004A9F; //0x1684
	uint32_t N00003CC8; //0x1688
	uint32_t N00004AA1; //0x168C
	uint32_t N00003CC9; //0x1690
	uint32_t N00004AA3; //0x1694
	uint32_t N00003CCA; //0x1698
	uint32_t N00004AA5; //0x169C
	uint32_t N00003CCB; //0x16A0
	uint32_t N00004AA7; //0x16A4
	uint32_t N00003CCC; //0x16A8
	uint32_t N00004AA9; //0x16AC
	uint32_t N00003CCD; //0x16B0
	uint32_t N00004AAB; //0x16B4
	uint32_t N00003CCE; //0x16B8
	uint32_t N00004AAD; //0x16BC
	uint32_t N00003CCF; //0x16C0
	uint32_t N00004AAF; //0x16C4
	uint32_t N00003CD0; //0x16C8
	uint32_t N00004AB1; //0x16CC
	uint32_t N00003CD1; //0x16D0
	uint32_t N00004AB3; //0x16D4
	uint32_t N00003CD2; //0x16D8
	uint32_t N00004AB5; //0x16DC
	uint32_t N00003CD3; //0x16E0
	uint32_t N00004AB7; //0x16E4
	uint32_t N00003CD4; //0x16E8
	uint32_t N00004AB9; //0x16EC
	uint32_t N00003CD5; //0x16F0
	uint32_t N00004ABB; //0x16F4
	uint32_t N00003CD6; //0x16F8
	uint32_t N00004ABD; //0x16FC
	uint32_t N00003CD7; //0x1700
	uint32_t N00004ABF; //0x1704
	uint32_t N00003CD8; //0x1708
	uint32_t N00004AC1; //0x170C
	uint32_t N00003CD9; //0x1710
	uint32_t N00004AC3; //0x1714
	uint32_t N00003CDA; //0x1718
	uint32_t N00004AC5; //0x171C
	uint32_t N00003CDB; //0x1720
	uint32_t N00004AC7; //0x1724
	uint32_t N00003CDC; //0x1728
	uint32_t N00004AC9; //0x172C
	uint32_t N00003CDD; //0x1730
	uint32_t N00004ACB; //0x1734
	uint32_t N00003CDE; //0x1738
	uint32_t N00004ACD; //0x173C
	uint32_t N00003CDF; //0x1740
	uint32_t N00004ACF; //0x1744
	uint32_t N00003CE0; //0x1748
	uint32_t N00004AD1; //0x174C
	uint32_t N00003CE1; //0x1750
	uint32_t N00004AD3; //0x1754
	uint32_t N00003CE2; //0x1758
	uint32_t N00004AD5; //0x175C
	uint32_t N00003CE3; //0x1760
	uint32_t N00004AD7; //0x1764
	uint32_t N00003CE4; //0x1768
	uint32_t N00004AD9; //0x176C
	uint32_t N00003CE5; //0x1770
	uint32_t N00004ADB; //0x1774
	uint32_t N00003CE6; //0x1778
	uint32_t N00004ADD; //0x177C
	uint32_t N00003CE7; //0x1780
	uint32_t N00004ADF; //0x1784
	uint32_t N00003CE8; //0x1788
	uint32_t N00004AE1; //0x178C
	uint32_t N00003CE9; //0x1790
	uint32_t N00004AE3; //0x1794
	uint32_t N00003CEA; //0x1798
	uint32_t N00004AE5; //0x179C
	uint32_t N00003CEB; //0x17A0
	uint32_t N00004AE7; //0x17A4
	uint32_t N00003CEC; //0x17A8
	uint32_t N00004AE9; //0x17AC
	uint32_t N00003CED; //0x17B0
	uint32_t N00004AEB; //0x17B4
	uint32_t N00003CEE; //0x17B8
	uint32_t N00004AED; //0x17BC
	uint32_t N00003CEF; //0x17C0
	uint32_t N00004AEF; //0x17C4
	uint32_t N00003CF0; //0x17C8
	uint32_t N00004AF1; //0x17CC
	uint32_t N00003CF1; //0x17D0
	uint32_t N00004AF3; //0x17D4
	uint32_t N00003CF2; //0x17D8
	uint32_t N00004AF5; //0x17DC
	uint32_t N00003CF3; //0x17E0
	uint32_t N00004AF7; //0x17E4
	uint32_t N00003CF4; //0x17E8
	uint32_t N00004AF9; //0x17EC
	uint32_t N00003CF5; //0x17F0
	uint32_t N00004AFB; //0x17F4
	uint32_t N00003CF6; //0x17F8
	uint32_t N00004AFD; //0x17FC
	uint32_t N00003CF7; //0x1800
	uint32_t N00004AFF; //0x1804
	uint32_t N00003CF8; //0x1808
	uint32_t N00004B01; //0x180C
	uint32_t N00003CF9; //0x1810
	uint32_t N00004B03; //0x1814
	uint32_t N00003CFA; //0x1818
	uint32_t N00004B05; //0x181C
	uint32_t N00003CFB; //0x1820
	uint32_t N00004B07; //0x1824
	uint32_t N00003CFC; //0x1828
	uint32_t N00004B09; //0x182C
	uint32_t N00003CFD; //0x1830
	uint32_t N00004B0B; //0x1834
	uint32_t N00003CFE; //0x1838
	uint32_t N00004B0D; //0x183C
	uint32_t N00003CFF; //0x1840
	uint32_t N00004B0F; //0x1844
	uint32_t N00003D00; //0x1848
	uint32_t N00004B11; //0x184C
	uint32_t N00003D01; //0x1850
	uint32_t N00004B13; //0x1854
	uint32_t N00003D02; //0x1858
	uint32_t N00004B15; //0x185C
	uint32_t N00003D03; //0x1860
	uint32_t N00004B17; //0x1864
	uint32_t N00003D04; //0x1868
	uint32_t N00004B19; //0x186C
	uint32_t N00003D05; //0x1870
	uint32_t N00004B1B; //0x1874
	uint32_t N00003D06; //0x1878
	uint32_t N00004B1D; //0x187C
	uint32_t N00003D07; //0x1880
	uint32_t N00004B1F; //0x1884
	uint32_t N00003D08; //0x1888
	uint32_t N00004B21; //0x188C
	uint32_t N00003D09; //0x1890
	uint32_t N00004B23; //0x1894
	uint32_t N00003D0A; //0x1898
	uint32_t N00004B25; //0x189C
	uint32_t N00003D0B; //0x18A0
	uint32_t N00004B27; //0x18A4
	uint32_t N00003D0C; //0x18A8
	uint32_t N00004B29; //0x18AC
	uint32_t N00003D0D; //0x18B0
	uint32_t N00004B2B; //0x18B4
	uint32_t N00003D0E; //0x18B8
	uint32_t N00004B2D; //0x18BC
	uint32_t N00003D0F; //0x18C0
	uint32_t N00004B2F; //0x18C4
	uint32_t N00003D10; //0x18C8
	uint32_t N00004B31; //0x18CC
	uint32_t N00003D11; //0x18D0
	uint32_t N00004B33; //0x18D4
	uint32_t N00003D12; //0x18D8
	uint32_t N00004B35; //0x18DC
	uint32_t N00003D13; //0x18E0
	uint32_t N00004B37; //0x18E4
	uint32_t N00003D14; //0x18E8
	uint32_t N00004B39; //0x18EC
	uint32_t N00003D15; //0x18F0
	uint32_t N00004B3B; //0x18F4
	uint32_t N00003D16; //0x18F8
	uint32_t N00004B3D; //0x18FC
	uint32_t N00003D17; //0x1900
	uint32_t N00004B3F; //0x1904
	uint32_t N00003D18; //0x1908
	uint32_t N00004B41; //0x190C
	uint32_t N00003D19; //0x1910
	uint32_t N00004B43; //0x1914
	uint32_t N00003D1A; //0x1918
	uint32_t N00004B45; //0x191C
	uint32_t N00003D1B; //0x1920
	uint32_t N00004B47; //0x1924
	uint32_t N00003D1C; //0x1928
	uint32_t N00004B49; //0x192C
	uint32_t N00003D1D; //0x1930
	uint32_t N00004B4B; //0x1934
	uint32_t N00003D1E; //0x1938
	uint32_t N00004B4D; //0x193C
	uint32_t N00003D1F; //0x1940
	uint32_t N00004B4F; //0x1944
	uint32_t N00003D20; //0x1948
	uint32_t N00004B51; //0x194C
	uint32_t N00003D21; //0x1950
	uint32_t N00004B53; //0x1954
	uint32_t N00003D22; //0x1958
	uint32_t N00004B55; //0x195C
	uint32_t N00003D23; //0x1960
	uint32_t N00004B57; //0x1964
	uint32_t N00003D24; //0x1968
	uint32_t N00004B59; //0x196C
	uint32_t N00003D25; //0x1970
	uint32_t N00004B5B; //0x1974
	uint32_t N00003D26; //0x1978
	uint32_t N00004B5D; //0x197C
	uint32_t N00003D27; //0x1980
	uint32_t N00004B5F; //0x1984
	uint32_t N00003D28; //0x1988
	uint32_t N00004B61; //0x198C
	uint32_t N00003D29; //0x1990
	uint32_t N00004B63; //0x1994
	uint32_t N00003D2A; //0x1998
	uint32_t N00004B65; //0x199C
	uint32_t N00003D2B; //0x19A0
	uint32_t N00004B67; //0x19A4
	uint32_t N00003D2C; //0x19A8
	uint32_t N00004B69; //0x19AC
	uint32_t N00003D2D; //0x19B0
	uint32_t N00004B6B; //0x19B4
	uint32_t N00003D2E; //0x19B8
	uint32_t N00004B6D; //0x19BC
	uint32_t N00003D2F; //0x19C0
	uint32_t N00004B6F; //0x19C4
	uint32_t N00003D30; //0x19C8
	uint32_t N00004B71; //0x19CC
	uint32_t N00003D31; //0x19D0
	uint32_t N00004B73; //0x19D4
	uint32_t N00003D32; //0x19D8
	uint32_t N00004B75; //0x19DC
	uint32_t N00003D33; //0x19E0
	uint32_t N00004B77; //0x19E4
	uint32_t N00003D34; //0x19E8
	uint32_t N00004B79; //0x19EC
	uint32_t N00003D35; //0x19F0
	uint32_t N00004B7B; //0x19F4
	uint32_t N00003D36; //0x19F8
	uint32_t N00004B7D; //0x19FC
	uint32_t N00003D37; //0x1A00
	uint32_t N00004B7F; //0x1A04
	uint32_t N00003D38; //0x1A08
	uint32_t N00004B81; //0x1A0C
	uint32_t N00003D39; //0x1A10
	uint32_t N00004B83; //0x1A14
	uint32_t N00003D3A; //0x1A18
	uint32_t N00004B85; //0x1A1C
	uint32_t N00003D3B; //0x1A20
	uint32_t N00004B87; //0x1A24
	uint32_t N00003D3C; //0x1A28
	uint32_t N00004B89; //0x1A2C
	uint32_t N00003D3D; //0x1A30
	uint32_t N00004B8B; //0x1A34
	uint32_t N00003D3E; //0x1A38
	uint32_t N00004B8D; //0x1A3C
	uint32_t N00003D3F; //0x1A40
	uint32_t N00004B8F; //0x1A44
	uint32_t N00003D40; //0x1A48
	uint32_t N00004B91; //0x1A4C
	uint32_t N00003D41; //0x1A50
	uint32_t N00004B93; //0x1A54
	uint32_t N00003D42; //0x1A58
	uint32_t N00004B95; //0x1A5C
	uint32_t N00003D43; //0x1A60
	uint32_t N00004B97; //0x1A64
	uint32_t N00003D44; //0x1A68
	uint32_t N00004B99; //0x1A6C
	uint32_t N00003D45; //0x1A70
	uint32_t N00004B9B; //0x1A74
	uint32_t N00003D46; //0x1A78
	uint32_t N00004B9D; //0x1A7C
	uint32_t N00003D47; //0x1A80
	uint32_t N00004B9F; //0x1A84
	uint32_t N00003D48; //0x1A88
	uint32_t N00004BA1; //0x1A8C
	uint32_t N00003D49; //0x1A90
	uint32_t N00004BA3; //0x1A94
	uint32_t N00003D4A; //0x1A98
	uint32_t N00004BA5; //0x1A9C
	uint32_t N00003D4B; //0x1AA0
	uint32_t N00004BA7; //0x1AA4
	uint32_t N00003D4C; //0x1AA8
	uint32_t N00004BA9; //0x1AAC
	uint32_t N00003D4D; //0x1AB0
	uint32_t N00004BAB; //0x1AB4
	uint32_t N00003D4E; //0x1AB8
	uint32_t N00004BAD; //0x1ABC
	uint32_t N00003D4F; //0x1AC0
	uint32_t N00004BAF; //0x1AC4
	uint32_t N00003D50; //0x1AC8
	uint32_t N00004BB1; //0x1ACC
	uint32_t N00003D51; //0x1AD0
	uint32_t N00004BB3; //0x1AD4
	uint32_t N00003D52; //0x1AD8
	uint32_t N00004BB5; //0x1ADC
	uint32_t N00003D53; //0x1AE0
	uint32_t N00004BB7; //0x1AE4
	uint32_t N00003D54; //0x1AE8
	uint32_t N00004BB9; //0x1AEC
	uint32_t N00003D55; //0x1AF0
	uint32_t N00004BBB; //0x1AF4
	uint32_t N00003D56; //0x1AF8
	uint32_t N00004BBD; //0x1AFC
	uint32_t N00003D57; //0x1B00
	uint32_t N00004BBF; //0x1B04
	uint32_t N00003D58; //0x1B08
	uint32_t N00004BC1; //0x1B0C
	uint32_t N00003D59; //0x1B10
	uint32_t N00004BC3; //0x1B14
	uint32_t N00003D5A; //0x1B18
	uint32_t N00004BC5; //0x1B1C
	uint32_t N00003D5B; //0x1B20
	uint32_t N00004BC7; //0x1B24
	uint32_t N00003D5C; //0x1B28
	uint32_t N00004BC9; //0x1B2C
	uint32_t N00003D5D; //0x1B30
	uint32_t N00004BCB; //0x1B34
	uint32_t N00003D5E; //0x1B38
	uint32_t N00004BCD; //0x1B3C
	uint32_t N00003D5F; //0x1B40
	uint32_t N00004BCF; //0x1B44
	uint32_t N00003D60; //0x1B48
	uint32_t N00004BD1; //0x1B4C
	uint32_t N00003D61; //0x1B50
	uint32_t N00004BD3; //0x1B54
	uint32_t N00003D62; //0x1B58
	uint32_t N00004BD5; //0x1B5C
	uint32_t N00003D63; //0x1B60
	uint32_t N00004BD7; //0x1B64
	uint32_t N00003D64; //0x1B68
	uint32_t N00004BD9; //0x1B6C
	uint32_t N00003D65; //0x1B70
	uint32_t N00004BDB; //0x1B74
	uint32_t N00003D66; //0x1B78
	uint32_t N00004BDD; //0x1B7C
	uint32_t N00003D67; //0x1B80
	uint32_t N00004BDF; //0x1B84
	uint32_t N00003D68; //0x1B88
	uint32_t N00004BE1; //0x1B8C
	uint32_t N00003D69; //0x1B90
	uint32_t N00004BE3; //0x1B94
	uint32_t N00003D6A; //0x1B98
	uint32_t N00004BE5; //0x1B9C
	uint32_t N00003D6B; //0x1BA0
	uint32_t N00004BE7; //0x1BA4
	uint32_t N00003D6C; //0x1BA8
	uint32_t N00004BE9; //0x1BAC
	uint32_t N00003D6D; //0x1BB0
	uint32_t N00004BEB; //0x1BB4
	uint32_t N00003D6E; //0x1BB8
	uint32_t N00004BED; //0x1BBC
	uint32_t N00003D6F; //0x1BC0
	uint32_t N00004BEF; //0x1BC4
	uint32_t N00003D70; //0x1BC8
	uint32_t N00004BF1; //0x1BCC
	uint32_t N00003D71; //0x1BD0
	uint32_t N00004BF3; //0x1BD4
	uint32_t N00003D72; //0x1BD8
	uint32_t N00004BF5; //0x1BDC
	uint32_t N00003D73; //0x1BE0
	uint32_t N00004BF7; //0x1BE4
	uint32_t N00003D74; //0x1BE8
	uint32_t N00004BF9; //0x1BEC
	uint32_t N00003D75; //0x1BF0
	uint32_t N00004BFB; //0x1BF4
	uint32_t N00003D76; //0x1BF8
	uint32_t N00004BFD; //0x1BFC
	uint32_t N00003D77; //0x1C00
	uint32_t N00004BFF; //0x1C04
	uint32_t N00003D78; //0x1C08
	uint32_t N00004C01; //0x1C0C
	uint32_t N00003D79; //0x1C10
	uint32_t N00004C03; //0x1C14
	uint32_t N00003D7A; //0x1C18
	uint32_t N00004C05; //0x1C1C
	uint32_t N00003D7B; //0x1C20
	uint32_t N00004C07; //0x1C24
	uint32_t N00003D7C; //0x1C28
	uint32_t N00004C09; //0x1C2C
	uint32_t N00003D7D; //0x1C30
	uint32_t N00004C0B; //0x1C34
	uint32_t N00003D7E; //0x1C38
	uint32_t N00004C0D; //0x1C3C
	uint32_t N00003D7F; //0x1C40
	uint32_t N00004C0F; //0x1C44
	uint32_t N00003D80; //0x1C48
	uint32_t N00004C11; //0x1C4C
	uint32_t N00003D81; //0x1C50
	uint32_t N00004C13; //0x1C54
	uint32_t N00003D82; //0x1C58
	uint32_t N00004C15; //0x1C5C
	uint32_t N00003D83; //0x1C60
	uint32_t N00004C17; //0x1C64
	uint32_t N00003D84; //0x1C68
	uint32_t N00004C19; //0x1C6C
	uint32_t N00003D85; //0x1C70
	uint32_t N00004C1B; //0x1C74
	uint32_t N00003D86; //0x1C78
	uint32_t N00004C1D; //0x1C7C
	uint32_t N00003D87; //0x1C80
	uint32_t N00004C1F; //0x1C84
	uint32_t N00003D88; //0x1C88
	uint32_t N00004C21; //0x1C8C
	uint32_t N00003D89; //0x1C90
	uint32_t N00004C23; //0x1C94
	uint32_t N00003D8A; //0x1C98
	uint32_t N00004C25; //0x1C9C
	uint32_t N00003D8B; //0x1CA0
	uint32_t N00004C27; //0x1CA4
	uint32_t N00003D8C; //0x1CA8
	uint32_t N00004C29; //0x1CAC
	uint32_t N00003D8D; //0x1CB0
	uint32_t N00004C2B; //0x1CB4
	uint32_t N00003D8E; //0x1CB8
	uint32_t N00004C2D; //0x1CBC
	uint32_t N00003D8F; //0x1CC0
	uint32_t N00004C2F; //0x1CC4
	uint32_t N00003D90; //0x1CC8
	uint32_t N00004C31; //0x1CCC
	uint32_t N00003D91; //0x1CD0
	uint32_t N00004C33; //0x1CD4
	uint32_t N00003D92; //0x1CD8
	uint32_t N00004C35; //0x1CDC
	uint32_t N00003D93; //0x1CE0
	uint32_t N00004C37; //0x1CE4
	uint32_t N00003D94; //0x1CE8
	uint32_t N00004C39; //0x1CEC
	uint32_t N00003D95; //0x1CF0
	uint32_t N00004C3B; //0x1CF4
	uint32_t N00003D96; //0x1CF8
	uint32_t N00004C3D; //0x1CFC
	uint32_t N00003D97; //0x1D00
	uint32_t N00004C3F; //0x1D04
	uint32_t N00003D98; //0x1D08
	uint32_t N00004C41; //0x1D0C
	uint32_t N00003D99; //0x1D10
	uint32_t N00004C43; //0x1D14
	uint32_t N00003D9A; //0x1D18
	uint32_t N00004C45; //0x1D1C
	uint32_t N00003D9B; //0x1D20
	uint32_t N00004C47; //0x1D24
	uint32_t N00003D9C; //0x1D28
	uint32_t N00004C49; //0x1D2C
	uint32_t N00003D9D; //0x1D30
	uint32_t N00004C4B; //0x1D34
	uint32_t N00003D9E; //0x1D38
	uint32_t N00004C4D; //0x1D3C
	uint32_t N00003D9F; //0x1D40
	uint32_t N00004C4F; //0x1D44
	uint32_t N00003DA0; //0x1D48
	uint32_t N00004C51; //0x1D4C
	uint32_t N00003DA1; //0x1D50
	uint32_t N00004C53; //0x1D54
	uint32_t N00003DA2; //0x1D58
	uint32_t N00004C55; //0x1D5C
	uint32_t N00003DA3; //0x1D60
	uint32_t N00004C57; //0x1D64
	uint32_t N00003DA4; //0x1D68
	uint32_t N00004C59; //0x1D6C
	uint32_t N00003DA5; //0x1D70
	uint32_t N00004C5B; //0x1D74
	uint32_t N00003DA6; //0x1D78
	uint32_t N00004C5D; //0x1D7C
	uint32_t N00003DA7; //0x1D80
	uint32_t N00004C5F; //0x1D84
	uint32_t N00003DA8; //0x1D88
	uint32_t N00004C61; //0x1D8C
	uint32_t N00003DA9; //0x1D90
	uint32_t N00004C63; //0x1D94
	uint32_t N00003DAA; //0x1D98
	uint32_t N00004C65; //0x1D9C
	uint32_t N00003DAB; //0x1DA0
	uint32_t N00004C67; //0x1DA4
	uint32_t N00003DAC; //0x1DA8
	uint32_t N00004C69; //0x1DAC
	uint32_t N00003DAD; //0x1DB0
	uint32_t N00004C6B; //0x1DB4
	uint32_t N00003DAE; //0x1DB8
	uint32_t N00004C6D; //0x1DBC
	uint32_t N00003DAF; //0x1DC0
	uint32_t N00004C6F; //0x1DC4
	uint32_t N00003DB0; //0x1DC8
	uint32_t N00004C71; //0x1DCC
	uint32_t N00003DB1; //0x1DD0
	uint32_t N00004C73; //0x1DD4
	uint32_t N00003DB2; //0x1DD8
	uint32_t N00004C75; //0x1DDC
	uint32_t N00003DB3; //0x1DE0
	uint32_t N00004C77; //0x1DE4
	uint32_t N00003DB4; //0x1DE8
	uint32_t N00004C79; //0x1DEC
	uint32_t N00003DB5; //0x1DF0
	uint32_t N00004C7B; //0x1DF4
	uint32_t N00003DB6; //0x1DF8
	uint32_t N00004C7D; //0x1DFC
	uint32_t N00003DB7; //0x1E00
	uint32_t N00004C7F; //0x1E04
	uint32_t N00003DB8; //0x1E08
	uint32_t N00004C81; //0x1E0C
	uint32_t N00003DB9; //0x1E10
	uint32_t N00004C83; //0x1E14
	uint32_t N00003DBA; //0x1E18
	uint32_t N00004C85; //0x1E1C
	uint32_t N00003DBB; //0x1E20
	uint32_t N00004C87; //0x1E24
	uint32_t N00003DBC; //0x1E28
	uint32_t N00004C89; //0x1E2C
	uint32_t N00003DBD; //0x1E30
	uint32_t N00004C8B; //0x1E34
	uint32_t N00003DBE; //0x1E38
	uint32_t N00004C8D; //0x1E3C
	uint32_t N00003DBF; //0x1E40
	uint32_t N00004C8F; //0x1E44
	uint32_t N00003DC0; //0x1E48
	uint32_t N00004C91; //0x1E4C
	uint32_t N00003DC1; //0x1E50
	uint32_t N00004C93; //0x1E54
	uint32_t N00003DC2; //0x1E58
	uint32_t N00004C95; //0x1E5C
	uint32_t N00003DC3; //0x1E60
	uint32_t N00004C97; //0x1E64
	uint32_t N00003DC4; //0x1E68
	uint32_t N00004C99; //0x1E6C
	uint32_t N00003DC5; //0x1E70
	uint32_t N00004C9B; //0x1E74
	uint32_t N00003DC6; //0x1E78
	uint32_t N00004C9D; //0x1E7C
	uint32_t N00003DC7; //0x1E80
	uint32_t N00004C9F; //0x1E84
	uint32_t N00003DC8; //0x1E88
	uint32_t N00004CA1; //0x1E8C
	uint32_t N00003DC9; //0x1E90
	uint32_t N00004CA3; //0x1E94
	uint32_t N00003DCA; //0x1E98
	uint32_t N00004CA5; //0x1E9C
	uint32_t N00003DCB; //0x1EA0
	uint32_t N00004CA7; //0x1EA4
	uint32_t N00003DCC; //0x1EA8
	uint32_t N00004CA9; //0x1EAC
	uint32_t N00003DCD; //0x1EB0
	uint32_t N00004CAB; //0x1EB4
	uint32_t N00003DCE; //0x1EB8
	uint32_t N00004CAD; //0x1EBC
	uint32_t N00003DCF; //0x1EC0
	uint32_t N00004CAF; //0x1EC4
	uint32_t N00003DD0; //0x1EC8
	uint32_t N00004CB1; //0x1ECC
	uint32_t N00003DD1; //0x1ED0
	uint32_t N00004CB3; //0x1ED4
	uint32_t N00003DD2; //0x1ED8
	uint32_t N00004CB5; //0x1EDC
	uint32_t N00003DD3; //0x1EE0
	uint32_t N00004CB7; //0x1EE4
	uint32_t N00003DD4; //0x1EE8
	uint32_t N00004CB9; //0x1EEC
	uint32_t N00003DD5; //0x1EF0
	uint32_t N00004CBB; //0x1EF4
	uint32_t N00003DD6; //0x1EF8
	uint32_t N00004CBD; //0x1EFC
	uint32_t N00003DD7; //0x1F00
	uint32_t N00004CBF; //0x1F04
	uint32_t N00003DD8; //0x1F08
	uint32_t N00004CC1; //0x1F0C
	uint32_t N00003DD9; //0x1F10
	uint32_t N00004CC3; //0x1F14
	uint32_t N00003DDA; //0x1F18
	uint32_t N00004CC5; //0x1F1C
	uint32_t N00003DDB; //0x1F20
	uint32_t N00004CC7; //0x1F24
	uint32_t N00003DDC; //0x1F28
	uint32_t N00004CC9; //0x1F2C
	uint32_t N00003DDD; //0x1F30
	uint32_t N00004CCB; //0x1F34
	uint32_t N00003DDE; //0x1F38
	uint32_t N00004CCD; //0x1F3C
	uint32_t N00003DDF; //0x1F40
	uint32_t N00004CCF; //0x1F44
	uint32_t N00003DE0; //0x1F48
	uint32_t N00004CD1; //0x1F4C
	uint32_t N00003DE1; //0x1F50
	uint32_t N00004CD3; //0x1F54
	uint32_t N00003DE2; //0x1F58
	uint32_t N00004CD5; //0x1F5C
	uint32_t N00003DE3; //0x1F60
	uint32_t N00004CD7; //0x1F64
	uint32_t N00003DE4; //0x1F68
	uint32_t N00004CD9; //0x1F6C
	uint32_t N00003DE5; //0x1F70
	uint32_t N00004CDB; //0x1F74
	uint32_t N00003DE6; //0x1F78
	uint32_t N00004CDD; //0x1F7C
	uint32_t N00003DE7; //0x1F80
	uint32_t N00004CDF; //0x1F84
	uint32_t N00003DE8; //0x1F88
	uint32_t N00004CE1; //0x1F8C
	uint32_t N00003DE9; //0x1F90
	uint32_t N00004CE3; //0x1F94
	uint32_t N00003DEA; //0x1F98
	uint32_t N00004CE5; //0x1F9C
	uint32_t N00003DEB; //0x1FA0
	uint32_t N00004CE7; //0x1FA4
	uint32_t N00003DEC; //0x1FA8
	uint32_t N00004CE9; //0x1FAC
	uint32_t N00003DED; //0x1FB0
	uint32_t N00004CEB; //0x1FB4
	uint32_t N00003DEE; //0x1FB8
	uint32_t N00004CED; //0x1FBC
	uint32_t N00003DEF; //0x1FC0
	uint32_t N00004CEF; //0x1FC4
	uint32_t N00003DF0; //0x1FC8
	uint32_t N00004CF1; //0x1FCC
	uint32_t N00003DF1; //0x1FD0
	uint32_t N00004CF3; //0x1FD4
	uint32_t N00003DF2; //0x1FD8
	uint32_t N00004CF5; //0x1FDC
	uint32_t N00003DF3; //0x1FE0
	uint32_t N00004CF7; //0x1FE4
	uint32_t N00003DF4; //0x1FE8
	uint32_t N00004CF9; //0x1FEC
	uint32_t N00003DF5; //0x1FF0
	uint32_t N00004CFB; //0x1FF4
	uint32_t N00003DF6; //0x1FF8
	uint32_t N00004CFD; //0x1FFC
	uint32_t N00003DF7; //0x2000
	uint32_t N00004CFF; //0x2004
	uint32_t N00003DF8; //0x2008
	uint32_t N00004D01; //0x200C
	uint32_t N00003DF9; //0x2010
	uint32_t N00004D03; //0x2014
	uint32_t N00003DFA; //0x2018
	uint32_t N00004D05; //0x201C
	uint32_t N00003DFB; //0x2020
	uint32_t N00004D07; //0x2024
	uint32_t N00003DFC; //0x2028
	uint32_t N00004D09; //0x202C
	uint32_t N00003DFD; //0x2030
	uint32_t N00004D0B; //0x2034
	uint32_t N00003DFE; //0x2038
	uint32_t N00004D0D; //0x203C
	uint32_t N00003DFF; //0x2040
	uint32_t N00004D0F; //0x2044
	uint32_t N00003E00; //0x2048
	uint32_t N00004D11; //0x204C
	uint32_t N00003E01; //0x2050
	uint32_t N00004D13; //0x2054
	uint32_t N00003E02; //0x2058
	uint32_t N00004D15; //0x205C
	uint32_t N00003E03; //0x2060
	uint32_t N00004D17; //0x2064
	uint32_t N00003E04; //0x2068
	uint32_t N00004D19; //0x206C
	uint32_t N00003E05; //0x2070
	uint32_t N00004D1B; //0x2074
	uint32_t N00003E06; //0x2078
	uint32_t N00004D1D; //0x207C
	uint32_t N00003E07; //0x2080
	uint32_t N00004D1F; //0x2084
	uint32_t N00003E08; //0x2088
	uint32_t N00004D21; //0x208C
	uint32_t N00003E09; //0x2090
	uint32_t N00004D23; //0x2094
	uint32_t N00003E0A; //0x2098
	uint32_t N00004D25; //0x209C
	uint32_t N00003E0B; //0x20A0
	uint32_t N00004D27; //0x20A4
	uint32_t N00003E0C; //0x20A8
	uint32_t N00004D29; //0x20AC
	uint32_t N00003E0D; //0x20B0
	uint32_t N00004D2B; //0x20B4
	uint32_t N00003E0E; //0x20B8
	uint32_t N00004D2D; //0x20BC
	uint32_t N00003E0F; //0x20C0
	uint32_t N00004D2F; //0x20C4
	uint32_t N00003E10; //0x20C8
	uint32_t N00004D31; //0x20CC
	uint32_t N00003E11; //0x20D0
	uint32_t N00004D33; //0x20D4
	uint32_t N00003E12; //0x20D8
	uint32_t N00004D35; //0x20DC
	uint32_t N00003E13; //0x20E0
	uint32_t N00004D37; //0x20E4
	uint32_t N00003E14; //0x20E8
	uint32_t N00004D39; //0x20EC
	uint32_t N00003E15; //0x20F0
	uint32_t N00004D3B; //0x20F4
	uint32_t N00003E16; //0x20F8
	uint32_t N00004D3D; //0x20FC
	uint32_t N00003E17; //0x2100
	uint32_t N00004D3F; //0x2104
	uint32_t N00003E18; //0x2108
	uint32_t N00004D41; //0x210C
	uint32_t N00003E19; //0x2110
	uint32_t N00004D43; //0x2114
	uint32_t N00003E1A; //0x2118
	uint32_t N00004D45; //0x211C
	uint32_t N00003E1B; //0x2120
	uint32_t N00004D47; //0x2124
	uint32_t N00003E1C; //0x2128
	uint32_t r_BadThingsNum; //0x212C
	uint32_t r_GoodThingsNum; //0x2130
	int32_t r_GoodBadThingBoost; //0x2134
	uint32_t r_GoodBadThingsForNextStage; //0x2138
	uint32_t N00004D4D; //0x213C
	uint32_t N00003E1F; //0x2140
	uint32_t N00004D4F; //0x2144
	uint32_t N00003E20; //0x2148
	uint32_t N00004D51; //0x214C
	uint32_t N00003E21; //0x2150
	uint32_t N00004D53; //0x2154
	uint32_t N00003E22; //0x2158
	uint32_t N00004D55; //0x215C
	uint32_t N00003E23; //0x2160
	int32_t N00004D57; //0x2164
	uint32_t N00003E24; //0x2168
	uint32_t N00004D59; //0x216C
	uint32_t N00003E25; //0x2170
	uint32_t N00004D5B; //0x2174
	uint32_t N00003E26; //0x2178
	uint32_t N00004D5D; //0x217C
	uint32_t N00003E27; //0x2180
	uint32_t N00004D5F; //0x2184
	uint32_t N00003E28; //0x2188
	uint32_t N00004D61; //0x218C
	uint32_t N00003E29; //0x2190
	uint32_t N00004D63; //0x2194
	uint32_t N00003E2A; //0x2198
	uint32_t N00004D65; //0x219C
	uint32_t N00003E2B; //0x21A0
	uint32_t N00004D67; //0x21A4
	uint32_t N00003E2C; //0x21A8
	uint32_t N00004D69; //0x21AC
	uint32_t N00003E2D; //0x21B0
	uint32_t N00004D6B; //0x21B4
	uint32_t N00003E2E; //0x21B8
	uint32_t N00004D6D; //0x21BC
	uint32_t N00003E2F; //0x21C0
	uint32_t N00004D6F; //0x21C4
	uint32_t N00003E30; //0x21C8
	uint32_t N00004D71; //0x21CC
	uint32_t N00003E31; //0x21D0
	uint32_t N00004D73; //0x21D4
	uint32_t N00003E32; //0x21D8
	uint32_t N00004D75; //0x21DC
	uint32_t N00003E33; //0x21E0
	uint32_t N00004D77; //0x21E4
	uint32_t N00003E34; //0x21E8
	uint32_t N00004D79; //0x21EC
	uint32_t N00003E35; //0x21F0
	uint32_t N00004D7B; //0x21F4
	uint32_t N00003E36; //0x21F8
	uint32_t N00004D7D; //0x21FC
	uint32_t N00003E37; //0x2200
	uint32_t N00004D7F; //0x2204
	uint32_t N00003E38; //0x2208
	uint32_t N00004D81; //0x220C
	uint32_t N00003E39; //0x2210
	uint32_t N00004D83; //0x2214
	uint32_t N00003E3A; //0x2218
	uint32_t N00004D85; //0x221C
	uint32_t N00003E3B; //0x2220
	uint32_t N00004D87; //0x2224
	uint32_t N00003E3C; //0x2228
	uint32_t N00004D89; //0x222C
	uint32_t N00003E3D; //0x2230
	uint32_t N00004D8B; //0x2234
	uint32_t N00003E3E; //0x2238
	uint32_t N00004D8D; //0x223C
	uint32_t N00003E3F; //0x2240
	uint32_t N00004D8F; //0x2244
	uint32_t N00003E40; //0x2248
	uint32_t N00004D91; //0x224C
	uint32_t N00003E41; //0x2250
	uint32_t N00004D93; //0x2254
	uint32_t N00003E42; //0x2258
	uint32_t N00004D95; //0x225C
	uint32_t N00003E43; //0x2260
	uint32_t N00004D97; //0x2264
	uint32_t N00003E44; //0x2268
	uint32_t N00004D99; //0x226C
	uint32_t N00003E45; //0x2270
	uint16_t N00004D9B; //0x2274
	uint16_t N000047E5_x; //0x2276
	uint32_t N00003E46; //0x2278
	uint16_t N00004D9D; //0x227C
	uint16_t r_BlessedCiviliansPercent; //0x227E
	uint32_t N00003E47; //0x2280
	uint32_t N00004D9F; //0x2284
	uint32_t r_Chapels; //0x2288
	uint32_t r_Churches; //0x228C
	uint32_t r_Cathedrals; //0x2290
	uint32_t N00004DA3; //0x2294
	uint32_t r_Priests; //0x2298
	uint32_t N00004DA5; //0x229C
	uint32_t N00003E4B; //0x22A0
	uint32_t N00004DA7; //0x22A4
	uint32_t N00003E4C; //0x22A8
	uint32_t N00004DA9; //0x22AC
	uint32_t N00003E4D; //0x22B0
	uint32_t N00004DAB; //0x22B4
	uint32_t N00003E4E; //0x22B8
	uint32_t N00004DAD; //0x22BC
	uint32_t N00003E4F; //0x22C0
	uint32_t N00004DAF; //0x22C4
	uint32_t N00003E50; //0x22C8
	uint32_t N00004DB1; //0x22CC
	uint32_t N00003E51; //0x22D0
	uint32_t N00004DB3; //0x22D4
	uint32_t N00003E52; //0x22D8
	uint32_t N00004DB5; //0x22DC
	uint32_t N00003E53; //0x22E0
	uint32_t N00004DB7; //0x22E4
	uint32_t N00003E54; //0x22E8
	uint32_t N00004DB9; //0x22EC
	uint32_t N00003E55; //0x22F0
	uint32_t N00004DBB; //0x22F4
	uint32_t N00003E56; //0x22F8
	uint32_t N00004DBD; //0x22FC
	uint32_t N00003E57; //0x2300
	uint32_t N00004DBF; //0x2304
	uint32_t N00003E58; //0x2308
	uint32_t N00004DC1; //0x230C
	uint32_t N00003E59; //0x2310
	uint32_t N00004DC3; //0x2314
	uint32_t N00003E5A; //0x2318
	uint32_t N00004DC5; //0x231C
	uint32_t N00003E5B; //0x2320
	uint32_t N00004DC7; //0x2324
	uint32_t N00003E5C; //0x2328
	uint32_t N00004DC9; //0x232C
	uint32_t N00003E5D; //0x2330
	uint32_t N00004DCB; //0x2334
	uint32_t N00003E5E; //0x2338
	uint32_t N00004DCD; //0x233C
	uint32_t N00003E5F; //0x2340
	uint32_t N00004DCF; //0x2344
	uint32_t N00003E60; //0x2348
	uint32_t N00004DD1; //0x234C
	uint32_t N00003E61; //0x2350
	uint32_t N00004DD3; //0x2354
	uint32_t N00003E62; //0x2358
	uint32_t N00004DD5; //0x235C
	uint32_t N00003E63; //0x2360
	uint32_t N00004DD7; //0x2364
	uint32_t N00003E64; //0x2368
	uint32_t N00004DD9; //0x236C
	uint32_t N00003E65; //0x2370
	uint32_t N00004DDB; //0x2374
	uint32_t N00003E66; //0x2378
	uint32_t N00004DDD; //0x237C
	uint32_t N00003E67; //0x2380
	uint32_t N00004DDF; //0x2384
	uint32_t N00003E68; //0x2388
	uint32_t N00004DE1; //0x238C
	uint32_t N00003E69; //0x2390
	uint32_t N00004DE3; //0x2394
	uint32_t N00003E6A; //0x2398
	uint32_t N00004DE5; //0x239C
	uint32_t N00003E6B; //0x23A0
	uint32_t N00004DE7; //0x23A4
	uint32_t N00003E6C; //0x23A8
	uint32_t N00004DE9; //0x23AC
	uint32_t N00003E6D; //0x23B0
	uint32_t N00004DEB; //0x23B4
	uint32_t N00003E6E; //0x23B8
	uint32_t N00004DED; //0x23BC
	uint32_t N00003E6F; //0x23C0
	uint32_t N00004DEF; //0x23C4
	uint32_t N00003E70; //0x23C8
	uint32_t N00004DF1; //0x23CC
	uint32_t N00003E71; //0x23D0
	uint32_t N00004DF3; //0x23D4
	uint32_t N00003E72; //0x23D8
	uint32_t N00004DF5; //0x23DC
	uint32_t N00003E73; //0x23E0
	uint32_t N00004DF7; //0x23E4
	uint32_t N00003E74; //0x23E8
	uint32_t N00004DF9; //0x23EC
	uint32_t N00003E75; //0x23F0
	uint32_t N00004DFB; //0x23F4
	uint32_t N00003E76; //0x23F8
	uint32_t N00004DFD; //0x23FC
	uint32_t N00003E77; //0x2400
	uint32_t N00004DFF; //0x2404
	uint32_t N00003E78; //0x2408
	uint32_t N00004E01; //0x240C
	uint32_t N00003E79; //0x2410
	uint32_t N00004E03; //0x2414
	uint32_t N00003E7A; //0x2418
	uint32_t N00004E05; //0x241C
	uint32_t N00003E7B; //0x2420
	uint32_t N00004E07; //0x2424
	uint32_t N00003E7C; //0x2428
	uint32_t N00004E09; //0x242C
	uint32_t N00003E7D; //0x2430
	uint32_t N00004E0B; //0x2434
	uint32_t N00003E7E; //0x2438
	uint32_t N00004E0D; //0x243C
	uint32_t N00003E7F; //0x2440
	uint32_t N00004E0F; //0x2444
	uint32_t N00003E80; //0x2448
	uint32_t N00004E11; //0x244C
	uint32_t N00003E81; //0x2450
	uint32_t N00004E13; //0x2454
	uint32_t N00003E82; //0x2458
	uint32_t N00004E15; //0x245C
	uint32_t N00003E83; //0x2460
	uint32_t N00004E17; //0x2464
	uint32_t N00003E84; //0x2468
	uint32_t N00004E19; //0x246C
	uint32_t N00003E85; //0x2470
	uint32_t N00004E1B; //0x2474
	uint32_t N00003E86; //0x2478
	uint32_t N00004E1D; //0x247C
	uint32_t N00003E87; //0x2480
	uint32_t N00004E1F; //0x2484
	uint32_t N00003E88; //0x2488
	uint32_t N00004E21; //0x248C
	uint32_t N00003E89; //0x2490
	uint32_t N00004E23; //0x2494
	uint32_t N00003E8A; //0x2498
	uint32_t N00004E25; //0x249C
	uint32_t N00003E8B; //0x24A0
	uint32_t N00004E27; //0x24A4
	uint32_t N00003E8C; //0x24A8
	uint32_t N00004E29; //0x24AC
	uint32_t N00003E8D; //0x24B0
	uint32_t N00004E2B; //0x24B4
	uint32_t N00003E8E; //0x24B8
	uint32_t N00004E2D; //0x24BC
	uint32_t N00003E8F; //0x24C0
	uint32_t N00004E2F; //0x24C4
	uint32_t N00003E90; //0x24C8
	uint32_t N00004E31; //0x24CC
	uint32_t N00003E91; //0x24D0
	uint32_t N00004E33; //0x24D4
	uint32_t N00003E92; //0x24D8
	uint32_t N00004E35; //0x24DC
	uint32_t N00003E93; //0x24E0
	uint32_t N00004E37; //0x24E4
	uint32_t N00003E94; //0x24E8
	uint32_t N00004E39; //0x24EC
	uint32_t N00003E95; //0x24F0
	uint32_t N00004E3B; //0x24F4
	uint32_t N00003E96; //0x24F8
	uint32_t N00004E3D; //0x24FC
	uint32_t N00003E97; //0x2500
	uint32_t N00004E3F; //0x2504
	uint32_t N00003E98; //0x2508
	uint32_t N00004E41; //0x250C
	uint32_t N00003E99; //0x2510
	uint32_t N00004E43; //0x2514
	uint32_t N00003E9A; //0x2518
	uint32_t N00004E45; //0x251C
	uint32_t N00003E9B; //0x2520
	uint32_t N00004E47; //0x2524
	uint32_t N00003E9C; //0x2528
	uint32_t N00004E49; //0x252C
	uint32_t N00003E9D; //0x2530
	uint32_t N00004E4B; //0x2534
	uint32_t N00003E9E; //0x2538
	uint32_t N00004E4D; //0x253C
	uint32_t N00003E9F; //0x2540
	uint32_t N00004E4F; //0x2544
	uint32_t N00003EA0; //0x2548
	uint32_t N00004E51; //0x254C
	uint32_t N00003EA1; //0x2550
	uint32_t N00004E53; //0x2554
	uint32_t N00003EA2; //0x2558
	uint32_t N00004E55; //0x255C
	uint32_t N00003EA3; //0x2560
	uint32_t N00004E57; //0x2564
	uint32_t N00003EA4; //0x2568
	uint32_t N00004E59; //0x256C
	uint32_t N00003EA5; //0x2570
	uint32_t N00004E5B; //0x2574
	uint32_t N00003EA6; //0x2578
	uint32_t N00004E5D; //0x257C
	uint32_t N00003EA7; //0x2580
	uint32_t N00004E5F; //0x2584
	uint32_t N00003EA8; //0x2588
	uint32_t N00004E61; //0x258C
	uint32_t N00003EA9; //0x2590
	uint32_t N00004E63; //0x2594
	uint32_t N00003EAA; //0x2598
	uint32_t N00004E65; //0x259C
	uint32_t N00003EAB; //0x25A0
	uint32_t N00004E67; //0x25A4
	uint32_t N00003EAC; //0x25A8
	uint32_t N00004E69; //0x25AC
	uint32_t N00003EAD; //0x25B0
	uint32_t N00004E6B; //0x25B4
	uint32_t N00003EAE; //0x25B8
	uint32_t N00004E6D; //0x25BC
	uint32_t N00003EAF; //0x25C0
	uint32_t N00004E6F; //0x25C4
	uint32_t N00003EB0; //0x25C8
	uint32_t N00004E71; //0x25CC
	uint32_t N00003EB1; //0x25D0
	uint32_t N00004E73; //0x25D4
	uint32_t N00003EB2; //0x25D8
	uint32_t N00004E75; //0x25DC
	uint32_t N00003EB3; //0x25E0
	uint32_t N00004E77; //0x25E4
	uint32_t N00003EB4; //0x25E8
	uint32_t N00004E79; //0x25EC
	uint32_t N00003EB5; //0x25F0
	uint32_t N00004E7B; //0x25F4
	uint32_t N00003EB6; //0x25F8
	uint32_t N00004E7D; //0x25FC
	uint32_t N00003EB7; //0x2600
	uint32_t N00004E7F; //0x2604
	uint32_t N00003EB8; //0x2608
	uint32_t N00004E81; //0x260C
	uint32_t N00003EB9; //0x2610
	uint32_t N00004E83; //0x2614
	uint32_t N00003EBA; //0x2618
	uint32_t N00004E85; //0x261C
	uint32_t N00003EBB; //0x2620
	uint32_t N00004E87; //0x2624
	uint32_t N00003EBC; //0x2628
	uint32_t N00004E89; //0x262C
	uint32_t N00003EBD; //0x2630
	uint32_t N00004E8B; //0x2634
	uint32_t N00003EBE; //0x2638
	uint32_t N00004E8D; //0x263C
	uint32_t N00003EBF; //0x2640
	uint32_t N00004E8F; //0x2644
	uint32_t N00003EC0; //0x2648
	uint32_t N00004E91; //0x264C
	uint32_t N00003EC1; //0x2650
	uint32_t N00004E93; //0x2654
	uint32_t N00003EC2; //0x2658
	uint32_t N00004E95; //0x265C
	uint32_t N00003EC3; //0x2660
	uint32_t N00004E97; //0x2664
	uint32_t N00003EC4; //0x2668
	uint32_t N00004E99; //0x266C
	uint32_t N00003EC5; //0x2670
	uint32_t N00004E9B; //0x2674
	uint32_t N00003EC6; //0x2678
	uint32_t N00004E9D; //0x267C
	uint32_t N00003EC7; //0x2680
	uint32_t N00004E9F; //0x2684
	uint32_t N00003EC8; //0x2688
	uint32_t N00004EA1; //0x268C
	uint32_t N00003EC9; //0x2690
	uint32_t N00004EA3; //0x2694
	uint32_t N00003ECA; //0x2698
	uint32_t N00004EA5; //0x269C
	uint32_t N00003ECB; //0x26A0
	uint32_t N00004EA7; //0x26A4
	uint32_t N00003ECC; //0x26A8
	uint32_t N00004EA9; //0x26AC
	uint32_t N00003ECD; //0x26B0
	uint32_t N00004EAB; //0x26B4
	uint32_t N00003ECE; //0x26B8
	uint32_t N00004EAD; //0x26BC
	uint32_t N00003ECF; //0x26C0
	uint32_t N00004EAF; //0x26C4
	uint32_t N00003ED0; //0x26C8
	uint32_t N00004EB1; //0x26CC
	uint32_t N00003ED1; //0x26D0
	uint32_t N00004EB3; //0x26D4
	uint32_t N00003ED2; //0x26D8
	uint32_t N00004EB5; //0x26DC
	uint32_t N00003ED3; //0x26E0
	uint32_t N00004EB7; //0x26E4
	uint32_t N00003ED4; //0x26E8
	uint32_t N00004EB9; //0x26EC
	uint32_t N00003ED5; //0x26F0
	uint32_t N00004EBB; //0x26F4
	uint32_t N00003ED6; //0x26F8
	uint32_t N00004EBD; //0x26FC
	uint32_t N00003ED7; //0x2700
	uint32_t N00004EBF; //0x2704
	uint32_t N00003ED8; //0x2708
	uint32_t N00004EC1; //0x270C
	uint32_t N00003ED9; //0x2710
	uint32_t N00004EC3; //0x2714
	uint32_t N00003EDA; //0x2718
	uint32_t N00004EC5; //0x271C
	uint32_t N00003EDB; //0x2720
	uint32_t N00004EC7; //0x2724
	uint32_t N00003EDC; //0x2728
	uint32_t N00004EC9; //0x272C
	uint32_t N00003EDD; //0x2730
	uint32_t N00004ECB; //0x2734
	uint32_t N00003EDE; //0x2738
	uint32_t N00004ECD; //0x273C
	uint32_t N00003EDF; //0x2740
	uint32_t N00004ECF; //0x2744
	uint32_t N00003EE0; //0x2748
	uint32_t N00004ED1; //0x274C
	uint32_t N00003EE1; //0x2750
	uint32_t N00004ED3; //0x2754
	uint32_t N00003EE2; //0x2758
	uint32_t N00004ED5; //0x275C
	uint32_t N00003EE3; //0x2760
	uint32_t N00004ED7; //0x2764
	uint32_t N00003EE4; //0x2768
	uint32_t N00004ED9; //0x276C
	uint32_t N00003EE5; //0x2770
	uint32_t N00004EDB; //0x2774
	uint32_t N00003EE6; //0x2778
	uint32_t N00004EDD; //0x277C
	uint32_t N00003EE7; //0x2780
	uint32_t N00004EDF; //0x2784
	uint32_t N00003EE8; //0x2788
	uint32_t N00004EE1; //0x278C
	uint32_t N00003EE9; //0x2790
	uint32_t N00004EE3; //0x2794
	uint32_t N00003EEA; //0x2798
	uint32_t N00004EE5; //0x279C
	uint32_t N00003EEB; //0x27A0
	uint32_t N00004EE7; //0x27A4
	uint32_t N00003EEC; //0x27A8
	uint32_t N00004EE9; //0x27AC
	uint32_t N00003EED; //0x27B0
	uint32_t N00004EEB; //0x27B4
	uint32_t N00003EEE; //0x27B8
	uint32_t N00004EED; //0x27BC
	uint32_t N00003EEF; //0x27C0
	uint32_t N00004EEF; //0x27C4
	uint32_t N00003EF0; //0x27C8
	uint32_t N00004EF1; //0x27CC
	uint32_t N00003EF1; //0x27D0
	uint32_t N00004EF3; //0x27D4
	uint32_t N00003EF2; //0x27D8
	uint32_t N00004EF5; //0x27DC
	uint32_t N00003EF3; //0x27E0
	uint32_t N00004EF7; //0x27E4
	uint32_t N00003EF4; //0x27E8
	uint32_t N00004EF9; //0x27EC
	uint32_t N00003EF5; //0x27F0
	uint32_t N00004EFB; //0x27F4
	uint32_t N00003EF6; //0x27F8
	uint32_t N00004EFD; //0x27FC
	uint32_t N00003EF7; //0x2800
	uint32_t N00004EFF; //0x2804
	uint32_t N00003EF8; //0x2808
	uint32_t N00004F01; //0x280C
	uint32_t N00003EF9; //0x2810
	uint32_t N00004F03; //0x2814
	uint32_t N00003EFA; //0x2818
	uint32_t N00004F05; //0x281C
	uint32_t N00003EFB; //0x2820
	uint32_t N00004F07; //0x2824
	uint32_t N00003EFC; //0x2828
	uint32_t N00004F09; //0x282C
	uint32_t N00003EFD; //0x2830
	uint32_t N00004F0B; //0x2834
	uint32_t N00003EFE; //0x2838
	uint32_t N00004F0D; //0x283C
	uint32_t N00003EFF; //0x2840
	uint32_t N00004F0F; //0x2844
	uint32_t N00003F00; //0x2848
	uint32_t N00004F11; //0x284C
	uint32_t N00003F01; //0x2850
	uint32_t N00004F13; //0x2854
	uint32_t N00003F02; //0x2858
	uint32_t N00004F15; //0x285C
	uint32_t N00003F03; //0x2860
	uint32_t N00004F17; //0x2864
	uint32_t N00003F04; //0x2868
	uint32_t N00004F19; //0x286C
	uint32_t N00003F05; //0x2870
	uint32_t N00004F1B; //0x2874
	uint32_t N00003F06; //0x2878
	uint32_t N00004F1D; //0x287C
	uint32_t N00003F07; //0x2880
	uint32_t N00004F1F; //0x2884
	uint32_t N00003F08; //0x2888
	uint32_t N00004F21; //0x288C
	uint32_t N00003F09; //0x2890
	uint32_t N00004F23; //0x2894
	uint32_t N00003F0A; //0x2898
	uint32_t N00004F25; //0x289C
	uint32_t N00003F0B; //0x28A0
	uint32_t N00004F27; //0x28A4
	uint32_t N00003F0C; //0x28A8
	uint32_t N00004F29; //0x28AC
	uint32_t N00003F0D; //0x28B0
	uint32_t N00004F2B; //0x28B4
	uint32_t N00003F0E; //0x28B8
	uint32_t N00004F2D; //0x28BC
	uint32_t N00003F0F; //0x28C0
	uint32_t N00004F2F; //0x28C4
	uint32_t N00003F10; //0x28C8
	uint32_t N00004F31; //0x28CC
	uint32_t N00003F11; //0x28D0
	uint32_t N00004F33; //0x28D4
	uint32_t N00003F12; //0x28D8
	uint32_t N00004F35; //0x28DC
	uint32_t N00003F13; //0x28E0
	uint32_t N00004F37; //0x28E4
	uint32_t N00003F14; //0x28E8
	uint32_t N00004F39; //0x28EC
	uint32_t N00003F15; //0x28F0
	uint32_t N00004F3B; //0x28F4
	uint32_t N00003F16; //0x28F8
	uint32_t N00004F3D; //0x28FC
	uint32_t N00003F17; //0x2900
	uint32_t N00004F3F; //0x2904
	uint32_t N00003F18; //0x2908
	uint32_t N00004F41; //0x290C
	uint32_t N00003F19; //0x2910
	uint32_t N00004F43; //0x2914
	uint32_t N00003F1A; //0x2918
	uint32_t N00004F45; //0x291C
	uint32_t N00003F1B; //0x2920
	uint32_t N00004F47; //0x2924
	uint32_t N00003F1C; //0x2928
	uint32_t N00004F49; //0x292C
	uint32_t N00003F1D; //0x2930
	uint32_t N00004F4B; //0x2934
	uint32_t N00003F1E; //0x2938
	uint32_t N00004F4D; //0x293C
	uint32_t N00003F1F; //0x2940
	uint32_t N00004F4F; //0x2944
	uint32_t N00003F20; //0x2948
	uint32_t N00004F51; //0x294C
	uint32_t N00003F21; //0x2950
	uint32_t N00004F53; //0x2954
	uint32_t N00003F22; //0x2958
	uint32_t N00004F55; //0x295C
	uint32_t N00003F23; //0x2960
	uint32_t N00004F57; //0x2964
	uint32_t N00003F24; //0x2968
	uint32_t N00004F59; //0x296C
	uint32_t N00003F25; //0x2970
	uint32_t N00004F5B; //0x2974
	uint32_t N00003F26; //0x2978
	uint32_t N00004F5D; //0x297C
	uint32_t N00003F27; //0x2980
	uint32_t N00004F5F; //0x2984
	uint32_t N00003F28; //0x2988
	uint32_t N00004F61; //0x298C
	uint32_t N00003F29; //0x2990
	uint32_t N00004F63; //0x2994
	uint32_t N00003F2A; //0x2998
	uint32_t N00004F65; //0x299C
	uint32_t N00003F2B; //0x29A0
	uint32_t N00004F67; //0x29A4
	uint32_t N00003F2C; //0x29A8
	uint32_t N00004F69; //0x29AC
	uint32_t N00003F2D; //0x29B0
	uint32_t N00004F6B; //0x29B4
	uint32_t N00003F2E; //0x29B8
	uint32_t N00004F6D; //0x29BC
	uint32_t N00003F2F; //0x29C0
	uint32_t N00004F6F; //0x29C4
	uint32_t N00003F30; //0x29C8
	uint32_t N00004F71; //0x29CC
	uint32_t N00003F31; //0x29D0
	uint32_t N00004F73; //0x29D4
	uint32_t N00003F32; //0x29D8
	uint32_t N00004F75; //0x29DC
	uint32_t N00003F33; //0x29E0
	uint32_t N00004F77; //0x29E4
	uint32_t N00003F34; //0x29E8
	uint32_t N00004F79; //0x29EC
	uint32_t N00003F35; //0x29F0
	uint32_t N00004F7B; //0x29F4
	uint32_t N00003F36; //0x29F8
	uint32_t N00004F7D; //0x29FC
	uint32_t N00003F37; //0x2A00
	uint32_t N00004F7F; //0x2A04
	uint32_t N00003F38; //0x2A08
	uint32_t N00004F81; //0x2A0C
	uint32_t N00003F39; //0x2A10
	uint32_t N00004F83; //0x2A14
	uint32_t N00003F3A; //0x2A18
	uint32_t N00004F85; //0x2A1C
	uint32_t N00003F3B; //0x2A20
	uint32_t N00004F87; //0x2A24
	uint32_t N00003F3C; //0x2A28
	uint32_t N00004F89; //0x2A2C
	uint32_t N00003F3D; //0x2A30
	uint32_t N00004F8B; //0x2A34
	uint32_t N00003F3E; //0x2A38
	uint32_t N00004F8D; //0x2A3C
	uint32_t N00003F3F; //0x2A40
	uint32_t N00004F8F; //0x2A44
	uint32_t N00003F40; //0x2A48
	uint32_t N00004F91; //0x2A4C
	uint32_t N00003F41; //0x2A50
	uint32_t N00004F93; //0x2A54
	uint32_t N00003F42; //0x2A58
	uint32_t N00004F95; //0x2A5C
	uint32_t N00003F43; //0x2A60
	uint32_t N00004F97; //0x2A64
	uint32_t N00003F44; //0x2A68
	uint32_t N00004F99; //0x2A6C
	uint32_t N00003F45; //0x2A70
	uint32_t N00004F9B; //0x2A74
	uint32_t N00003F46; //0x2A78
	uint32_t N00004F9D; //0x2A7C
	uint32_t N00003F47; //0x2A80
	uint32_t N00004F9F; //0x2A84
	uint32_t N00003F48; //0x2A88
	uint32_t N00004FA1; //0x2A8C
	uint32_t N00003F49; //0x2A90
	uint32_t N00004FA3; //0x2A94
	uint32_t N00003F4A; //0x2A98
	uint32_t N00004FA5; //0x2A9C
	uint32_t N00003F4B; //0x2AA0
	uint32_t N00004FA7; //0x2AA4
	uint32_t N00003F4C; //0x2AA8
	uint32_t N00004FA9; //0x2AAC
	uint32_t N00003F4D; //0x2AB0
	uint32_t N00004FAB; //0x2AB4
	uint32_t N00003F4E; //0x2AB8
	uint32_t N00004FAD; //0x2ABC
	uint32_t N00003F4F; //0x2AC0
	uint32_t N00004FAF; //0x2AC4
	uint32_t N00003F50; //0x2AC8
	uint32_t N00004FB1; //0x2ACC
	uint32_t N00003F51; //0x2AD0
	uint32_t N00004FB3; //0x2AD4
	uint32_t N00003F52; //0x2AD8
	uint32_t N00004FB5; //0x2ADC
	uint32_t N00003F53; //0x2AE0
	uint32_t N00004FB7; //0x2AE4
	uint32_t N00003F54; //0x2AE8
	uint32_t N00004FB9; //0x2AEC
	uint32_t N00003F55; //0x2AF0
	uint32_t N00004FBB; //0x2AF4
	uint32_t N00003F56; //0x2AF8
	uint32_t N00004FBD; //0x2AFC
	uint32_t N00003F57; //0x2B00
	uint32_t N00004FBF; //0x2B04
	uint32_t N00003F58; //0x2B08
	uint32_t N00004FC1; //0x2B0C
	uint32_t N00003F59; //0x2B10
	uint32_t N00004FC3; //0x2B14
	uint32_t N00003F5A; //0x2B18
	uint32_t N00004FC5; //0x2B1C
	uint32_t N00003F5B; //0x2B20
	uint32_t N00004FC7; //0x2B24
	uint32_t N00003F5C; //0x2B28
	uint32_t N00004FC9; //0x2B2C
	uint32_t N00003F5D; //0x2B30
	uint32_t N00004FCB; //0x2B34
	uint32_t N00003F5E; //0x2B38
	uint32_t N00004FCD; //0x2B3C
	uint32_t N00003F5F; //0x2B40
	uint32_t N00004FCF; //0x2B44
	uint32_t N00003F60; //0x2B48
	uint32_t N00004FD1; //0x2B4C
	uint32_t N00003F61; //0x2B50
	uint32_t N00004FD3; //0x2B54
	uint32_t N00003F62; //0x2B58
	uint32_t N00004FD5; //0x2B5C
	uint32_t N00003F63; //0x2B60
	uint32_t N00004FD7; //0x2B64
	uint32_t N00003F64; //0x2B68
	uint32_t N00004FD9; //0x2B6C
	uint32_t N00003F65; //0x2B70
	uint32_t N00004FDB; //0x2B74
	uint32_t N00003F66; //0x2B78
	uint32_t N00004FDD; //0x2B7C
	uint32_t N00003F67; //0x2B80
	uint32_t N00004FDF; //0x2B84
	uint32_t N00003F68; //0x2B88
	uint32_t N00004FE1; //0x2B8C
	uint32_t N00003F69; //0x2B90
	uint32_t N00004FE3; //0x2B94
	uint32_t N00003F6A; //0x2B98
	uint32_t N00004FE5; //0x2B9C
	uint32_t N00003F6B; //0x2BA0
	uint32_t N00004FE7; //0x2BA4
	uint32_t N00003F6C; //0x2BA8
	uint32_t N00004FE9; //0x2BAC
	uint32_t N00003F6D; //0x2BB0
	uint32_t N00004FEB; //0x2BB4
	uint32_t N00003F6E; //0x2BB8
	uint32_t N00004FED; //0x2BBC
	uint32_t N00003F6F; //0x2BC0
	uint32_t N00004FEF; //0x2BC4
	uint32_t N00003F70; //0x2BC8
	uint32_t N00004FF1; //0x2BCC
	uint32_t N00003F71; //0x2BD0
	uint32_t N00004FF3; //0x2BD4
	uint32_t N00003F72; //0x2BD8
	uint32_t N00004FF5; //0x2BDC
	uint32_t N00003F73; //0x2BE0
	uint32_t N00004FF7; //0x2BE4
	uint32_t N00003F74; //0x2BE8
	uint32_t N00004FF9; //0x2BEC
	uint32_t N00003F75; //0x2BF0
	uint32_t N00004FFB; //0x2BF4
	uint32_t N00003F76; //0x2BF8
	uint32_t N00004FFD; //0x2BFC
	uint32_t N00003F77; //0x2C00
	uint32_t N00004FFF; //0x2C04
	uint32_t N00003F78; //0x2C08
	uint32_t N00005001; //0x2C0C
	uint32_t N00003F79; //0x2C10
	uint32_t N00005003; //0x2C14
	uint32_t N00003F7A; //0x2C18
	uint32_t N00005005; //0x2C1C
	uint32_t N00003F7B; //0x2C20
	uint32_t N00005007; //0x2C24
	uint32_t N00003F7C; //0x2C28
	uint32_t N00005009; //0x2C2C
	uint32_t N00003F7D; //0x2C30
	uint32_t N0000500B; //0x2C34
	uint32_t N00003F7E; //0x2C38
	uint32_t N0000500D; //0x2C3C
	uint32_t N00003F7F; //0x2C40
	uint32_t N0000500F; //0x2C44
	uint32_t N00003F80; //0x2C48
	uint32_t N00005011; //0x2C4C
	uint32_t N00003F81; //0x2C50
	uint32_t N00005013; //0x2C54
	uint32_t N00003F82; //0x2C58
	uint32_t N00005015; //0x2C5C
	uint32_t N00003F83; //0x2C60
	uint32_t N00005017; //0x2C64
	uint32_t N00003F84; //0x2C68
	uint32_t N00005019; //0x2C6C
	uint32_t N00003F85; //0x2C70
	uint32_t N0000501B; //0x2C74
	uint32_t N00003F86; //0x2C78
	uint32_t N0000501D; //0x2C7C
	uint32_t N00003F87; //0x2C80
	uint32_t N0000501F; //0x2C84
	uint32_t N00003F88; //0x2C88
	uint32_t N00005021; //0x2C8C
	uint32_t N00003F89; //0x2C90
	uint32_t N00005023; //0x2C94
	uint32_t N00003F8A; //0x2C98
	uint32_t N00005025; //0x2C9C
	uint32_t N00003F8B; //0x2CA0
	uint32_t N00005027; //0x2CA4
	uint32_t N00003F8C; //0x2CA8
	uint32_t N00005029; //0x2CAC
	uint32_t N00003F8D; //0x2CB0
	uint32_t N0000502B; //0x2CB4
	uint32_t N00003F8E; //0x2CB8
	uint32_t N0000502D; //0x2CBC
	uint32_t N00003F8F; //0x2CC0
	uint32_t N0000502F; //0x2CC4
	uint32_t N00003F90; //0x2CC8
	uint32_t N00005031; //0x2CCC
	uint32_t N00003F91; //0x2CD0
	uint32_t N00005033; //0x2CD4
	uint32_t N00003F92; //0x2CD8
	uint32_t N00005035; //0x2CDC
	uint32_t N00003F93; //0x2CE0
	uint32_t N00005037; //0x2CE4
	uint32_t N00003F94; //0x2CE8
	uint32_t N00005039; //0x2CEC
	uint32_t N00003F95; //0x2CF0
	uint32_t N0000503B; //0x2CF4
	uint32_t N00003F96; //0x2CF8
	uint32_t N0000503D; //0x2CFC
	uint32_t N00003F97; //0x2D00
	uint32_t N0000503F; //0x2D04
	uint32_t N00003F98; //0x2D08
	uint32_t N00005041; //0x2D0C
	uint32_t N00003F99; //0x2D10
	uint32_t N00005043; //0x2D14
	uint32_t N00003F9A; //0x2D18
	uint32_t N00005045; //0x2D1C
	uint32_t N00003F9B; //0x2D20
	uint32_t N00005047; //0x2D24
	uint32_t N00003F9C; //0x2D28
	uint32_t N00005049; //0x2D2C
	uint32_t N00003F9D; //0x2D30
	uint32_t N0000504B; //0x2D34
	uint32_t N00003F9E; //0x2D38
	uint32_t N0000504D; //0x2D3C
	uint32_t N00003F9F; //0x2D40
	uint32_t N0000504F; //0x2D44
	uint32_t N00003FA0; //0x2D48
	uint32_t N00005051; //0x2D4C
	uint32_t N00003FA1; //0x2D50
	uint32_t N00005053; //0x2D54
	uint32_t N00003FA2; //0x2D58
	uint32_t N00005055; //0x2D5C
	uint32_t N00003FA3; //0x2D60
	uint32_t N00005057; //0x2D64
	uint32_t N00003FA4; //0x2D68
	uint32_t N00005059; //0x2D6C
	uint32_t N00003FA5; //0x2D70
	uint32_t N0000505B; //0x2D74
	uint32_t N00003FA6; //0x2D78
	uint32_t N0000505D; //0x2D7C
	uint32_t N00003FA7; //0x2D80
	uint32_t N0000505F; //0x2D84
	uint32_t N00003FA8; //0x2D88
	uint32_t N00005061; //0x2D8C
	uint32_t N00003FA9; //0x2D90
	uint32_t N00005063; //0x2D94
	uint32_t N00003FAA; //0x2D98
	uint32_t N00005065; //0x2D9C
	uint32_t N00003FAB; //0x2DA0
	uint32_t N00005067; //0x2DA4
	uint32_t N00003FAC; //0x2DA8
	uint32_t N00005069; //0x2DAC
	uint32_t N00003FAD; //0x2DB0
	uint32_t N0000506B; //0x2DB4
	uint32_t N00003FAE; //0x2DB8
	uint32_t N0000506D; //0x2DBC
	uint32_t N00003FAF; //0x2DC0
	uint32_t N0000506F; //0x2DC4
	uint32_t N00003FB0; //0x2DC8
	uint32_t N00005071; //0x2DCC
	uint32_t N00003FB1; //0x2DD0
	uint32_t N00005073; //0x2DD4
	uint32_t N00003FB2; //0x2DD8
	uint32_t N00005075; //0x2DDC
	uint32_t N00003FB3; //0x2DE0
	uint32_t N00005077; //0x2DE4
	uint32_t N00003FB4; //0x2DE8
	uint32_t N00005079; //0x2DEC
	uint32_t N00003FB5; //0x2DF0
	uint32_t N0000507B; //0x2DF4
	uint32_t N00003FB6; //0x2DF8
	uint32_t N0000507D; //0x2DFC
	uint32_t N00003FB7; //0x2E00
	uint32_t N0000507F; //0x2E04
	uint32_t N00003FB8; //0x2E08
	uint32_t N00005081; //0x2E0C
	uint32_t N00003FB9; //0x2E10
	uint32_t N00005083; //0x2E14
	uint32_t N00003FBA; //0x2E18
	uint32_t N00005085; //0x2E1C
	uint32_t N00003FBB; //0x2E20
	uint32_t N00005087; //0x2E24
	uint32_t N00003FBC; //0x2E28
	uint32_t N00005089; //0x2E2C
	uint32_t N00003FBD; //0x2E30
	uint32_t N0000508B; //0x2E34
	uint32_t N00003FBE; //0x2E38
	uint32_t N0000508D; //0x2E3C
	uint32_t N00003FBF; //0x2E40
	uint32_t N0000508F; //0x2E44
	uint32_t N00003FC0; //0x2E48
	uint32_t N00005091; //0x2E4C
	uint32_t N00003FC1; //0x2E50
	uint32_t N00005093; //0x2E54
	uint32_t N00003FC2; //0x2E58
	uint32_t N00005095; //0x2E5C
	uint32_t N00003FC3; //0x2E60
	uint32_t N00005097; //0x2E64
	uint32_t N00003FC4; //0x2E68
	uint32_t N00005099; //0x2E6C
	uint32_t N00003FC5; //0x2E70
	uint32_t N0000509B; //0x2E74
	uint32_t N00003FC6; //0x2E78
	uint32_t N0000509D; //0x2E7C
	uint32_t N00003FC7; //0x2E80
	uint32_t N0000509F; //0x2E84
	uint32_t N00003FC8; //0x2E88
	uint32_t N000050A1; //0x2E8C
	uint32_t N00003FC9; //0x2E90
	uint32_t N000050A3; //0x2E94
	uint32_t N00003FCA; //0x2E98
	uint32_t N000050A5; //0x2E9C
	uint32_t N00003FCB; //0x2EA0
	uint32_t N000050A7; //0x2EA4
	uint32_t N00003FCC; //0x2EA8
	uint32_t N000050A9; //0x2EAC
	uint32_t N00003FCD; //0x2EB0
	uint32_t N000050AB; //0x2EB4
	uint32_t N00003FCE; //0x2EB8
	uint32_t N000050AD; //0x2EBC
	uint32_t N00003FCF; //0x2EC0
	uint32_t N000050AF; //0x2EC4
	uint32_t N00003FD0; //0x2EC8
	uint32_t N000050B1; //0x2ECC
	uint32_t N00003FD1; //0x2ED0
	uint32_t N000050B3; //0x2ED4
	uint32_t N00003FD2; //0x2ED8
	uint32_t N000050B5; //0x2EDC
	uint32_t N00003FD3; //0x2EE0
	uint32_t N000050B7; //0x2EE4
	uint32_t N00003FD4; //0x2EE8
	uint32_t N000050B9; //0x2EEC
	uint32_t N00003FD5; //0x2EF0
	uint32_t N000050BB; //0x2EF4
	uint32_t N00003FD6; //0x2EF8
	uint32_t N000050BD; //0x2EFC
	uint32_t N00003FD7; //0x2F00
	uint32_t N000050BF; //0x2F04
	uint32_t N00003FD8; //0x2F08
	uint32_t N000050C1; //0x2F0C
	uint32_t N00003FD9; //0x2F10
	uint32_t N000050C3; //0x2F14
	uint32_t N00003FDA; //0x2F18
	uint32_t N000050C5; //0x2F1C
	uint32_t N00003FDB; //0x2F20
	uint32_t N000050C7; //0x2F24
	uint32_t N00003FDC; //0x2F28
	uint32_t N000050C9; //0x2F2C
	uint32_t N00003FDD; //0x2F30
	uint32_t N000050CB; //0x2F34
	uint32_t N00003FDE; //0x2F38
	uint32_t N000050CD; //0x2F3C
	uint32_t N00003FDF; //0x2F40
	uint32_t N000050CF; //0x2F44
	uint32_t N00003FE0; //0x2F48
	uint32_t N000050D1; //0x2F4C
	uint32_t N00003FE1; //0x2F50
	uint32_t N000050D3; //0x2F54
	uint32_t N00003FE2; //0x2F58
	uint32_t N000050D5; //0x2F5C
	uint32_t N00003FE3; //0x2F60
	uint32_t N000050D7; //0x2F64
	uint32_t N00003FE4; //0x2F68
	uint32_t N000050D9; //0x2F6C
	uint32_t N00003FE5; //0x2F70
	uint32_t N000050DB; //0x2F74
	uint32_t N00003FE6; //0x2F78
	uint32_t N000050DD; //0x2F7C
	uint32_t N00003FE7; //0x2F80
	uint32_t N000050DF; //0x2F84
	uint32_t N00003FE8; //0x2F88
	uint32_t N000050E1; //0x2F8C
	uint32_t N00003FE9; //0x2F90
	uint32_t N000050E3; //0x2F94
	uint32_t N00003FEA; //0x2F98
	uint32_t N000050E5; //0x2F9C
	uint32_t N00003FEB; //0x2FA0
	uint32_t N000050E7; //0x2FA4
	uint32_t N00003FEC; //0x2FA8
	uint32_t N000050E9; //0x2FAC
	uint32_t N00003FED; //0x2FB0
	uint32_t N000050EB; //0x2FB4
	uint32_t N00003FEE; //0x2FB8
	uint32_t N000050ED; //0x2FBC
	uint32_t N00003FEF; //0x2FC0
	uint32_t N000050EF; //0x2FC4
	uint32_t N00003FF0; //0x2FC8
	uint32_t N000050F1; //0x2FCC
	uint32_t N00003FF1; //0x2FD0
	uint32_t N000050F3; //0x2FD4
	uint32_t N00003FF2; //0x2FD8
	uint32_t N000050F5; //0x2FDC
	uint32_t N00003FF3; //0x2FE0
	uint32_t N000050F7; //0x2FE4
	uint32_t N00003FF4; //0x2FE8
	uint32_t N000050F9; //0x2FEC
	uint32_t N00003FF5; //0x2FF0
	uint32_t N000050FB; //0x2FF4
	uint32_t N00003FF6; //0x2FF8
	uint32_t N000050FD; //0x2FFC
	uint32_t N00003FF7; //0x3000
	uint32_t N000050FF; //0x3004
	uint32_t N00003FF8; //0x3008
	uint32_t N00005101; //0x300C
	uint32_t N00003FF9; //0x3010
	uint32_t N00005103; //0x3014
	uint32_t N00003FFA; //0x3018
	uint32_t N00005105; //0x301C
	uint32_t N00003FFB; //0x3020
	uint32_t N00005107; //0x3024
	uint32_t N00003FFC; //0x3028
	uint32_t N00005109; //0x302C
	uint32_t N00003FFD; //0x3030
	uint32_t N0000510B; //0x3034
	uint32_t N00003FFE; //0x3038
	uint32_t N0000510D; //0x303C
	uint32_t N00003FFF; //0x3040
	uint32_t N0000510F; //0x3044
	uint32_t N00004000; //0x3048
	uint32_t N00005111; //0x304C
	uint32_t N00004001; //0x3050
	uint32_t N00005113; //0x3054
	uint32_t N00004002; //0x3058
	uint32_t N00005115; //0x305C
	uint32_t N00004003; //0x3060
	uint32_t N00005117; //0x3064
	uint32_t N00004004; //0x3068
	uint32_t N00005119; //0x306C
	uint32_t N00004005; //0x3070
	uint32_t N0000511B; //0x3074
	uint32_t N00004006; //0x3078
	uint32_t N0000511D; //0x307C
	uint32_t N00004007; //0x3080
	uint32_t N0000511F; //0x3084
	uint32_t N00004008; //0x3088
	uint32_t N00005121; //0x308C
	uint32_t N00004009; //0x3090
	uint32_t N00005123; //0x3094
	uint32_t N0000400A; //0x3098
	uint32_t N00005125; //0x309C
	uint32_t N0000400B; //0x30A0
	uint32_t N00005127; //0x30A4
	uint32_t N0000400C; //0x30A8
	uint32_t N00005129; //0x30AC
	uint32_t N0000400D; //0x30B0
	uint32_t N0000512B; //0x30B4
	uint32_t N0000400E; //0x30B8
	uint32_t N0000512D; //0x30BC
	uint32_t N0000400F; //0x30C0
	uint32_t N0000512F; //0x30C4
	uint32_t N00004010; //0x30C8
	uint32_t N00005131; //0x30CC
	uint32_t N00004011; //0x30D0
	uint32_t N00005133; //0x30D4
	uint32_t N00004012; //0x30D8
	uint32_t N00005135; //0x30DC
	uint32_t N00004013; //0x30E0
	uint32_t N00005137; //0x30E4
	uint32_t N00004014; //0x30E8
	uint32_t N00005139; //0x30EC
	uint32_t N00004015; //0x30F0
	uint32_t N0000513B; //0x30F4
	uint32_t N00004016; //0x30F8
	uint32_t N0000513D; //0x30FC
	uint32_t N00004017; //0x3100
	uint32_t N0000513F; //0x3104
	uint32_t N00004018; //0x3108
	uint32_t N00005141; //0x310C
	uint32_t N00004019; //0x3110
	uint32_t N00005143; //0x3114
	uint32_t N0000401A; //0x3118
	uint32_t N00005145; //0x311C
	uint32_t N0000401B; //0x3120
	uint32_t N00005147; //0x3124
	uint32_t N0000401C; //0x3128
	uint32_t N00005149; //0x312C
	uint32_t N0000401D; //0x3130
	uint32_t N0000514B; //0x3134
	uint32_t N0000401E; //0x3138
	uint32_t N0000514D; //0x313C
	uint32_t N0000401F; //0x3140
	uint32_t N0000514F; //0x3144
	uint32_t N00004020; //0x3148
	uint32_t N00005151; //0x314C
	uint32_t N00004021; //0x3150
	uint32_t N00005153; //0x3154
	uint32_t N00004022; //0x3158
	uint32_t N00005155; //0x315C
	uint32_t N00004023; //0x3160
	uint32_t N00005157; //0x3164
	uint32_t N00004024; //0x3168
	uint32_t N00005159; //0x316C
	uint32_t N00004025; //0x3170
	uint32_t N0000515B; //0x3174
	uint32_t N00004026; //0x3178
	uint32_t N0000515D; //0x317C
	uint32_t N00004027; //0x3180
	uint32_t N0000515F; //0x3184
	uint32_t N00004028; //0x3188
	uint32_t N00005161; //0x318C
	uint32_t N00004029; //0x3190
	uint32_t N00005163; //0x3194
	uint32_t N0000402A; //0x3198
	uint32_t N00005165; //0x319C
	uint32_t N0000402B; //0x31A0
	uint32_t N00005167; //0x31A4
	uint32_t N0000402C; //0x31A8
	uint32_t N00005169; //0x31AC
	uint32_t N0000402D; //0x31B0
	uint32_t N0000516B; //0x31B4
	uint32_t N0000402E; //0x31B8
	uint32_t N0000516D; //0x31BC
	uint32_t N0000402F; //0x31C0
	uint32_t N0000516F; //0x31C4
	uint32_t N00004030; //0x31C8
	uint32_t N00005171; //0x31CC
	uint32_t N00004031; //0x31D0
	uint32_t N00005173; //0x31D4
	uint32_t N00004032; //0x31D8
	uint32_t N00005175; //0x31DC
	uint32_t N00004033; //0x31E0
	uint32_t N00005177; //0x31E4
	uint32_t N00004034; //0x31E8
	uint32_t N00005179; //0x31EC
	uint32_t N00004035; //0x31F0
	uint32_t N0000517B; //0x31F4
	uint32_t N00004036; //0x31F8
	uint32_t N0000517D; //0x31FC
	uint32_t N00004037; //0x3200
	uint32_t N0000517F; //0x3204
	uint32_t N00004038; //0x3208
	uint32_t N00005181; //0x320C
	uint32_t N00004039; //0x3210
	uint32_t N00005183; //0x3214
	uint32_t N0000403A; //0x3218
	uint32_t N00005185; //0x321C
	uint32_t N0000403B; //0x3220
	uint32_t N00005187; //0x3224
	uint32_t N0000403C; //0x3228
	uint32_t N00005189; //0x322C
	uint32_t N0000403D; //0x3230
	uint32_t N0000518B; //0x3234
	uint32_t N0000403E; //0x3238
	uint32_t N0000518D; //0x323C
	uint32_t N0000403F; //0x3240
	uint32_t N0000518F; //0x3244
	uint32_t N00004040; //0x3248
	uint32_t N00005191; //0x324C
	uint32_t N00004041; //0x3250
	uint32_t N00005193; //0x3254
	uint32_t N00004042; //0x3258
	uint32_t N00005195; //0x325C
	uint32_t N00004043; //0x3260
	uint32_t N00005197; //0x3264
	uint32_t N00004044; //0x3268
	uint32_t N00005199; //0x326C
	uint32_t N00004045; //0x3270
	uint32_t N0000519B; //0x3274
	uint32_t N00004046; //0x3278
	uint32_t N0000519D; //0x327C
	uint32_t N00004047; //0x3280
	uint32_t N0000519F; //0x3284
	uint32_t N00004048; //0x3288
	uint32_t N000051A1; //0x328C
	uint32_t N00004049; //0x3290
	uint32_t N000051A3; //0x3294
	uint32_t N0000404A; //0x3298
	uint32_t N000051A5; //0x329C
	uint32_t N0000404B; //0x32A0
	uint32_t N000051A7; //0x32A4
	uint32_t N0000404C; //0x32A8
	uint32_t N000051A9; //0x32AC
	uint32_t N0000404D; //0x32B0
	uint32_t N000051AB; //0x32B4
	uint32_t N0000404E; //0x32B8
	uint32_t N000051AD; //0x32BC
	uint32_t N0000404F; //0x32C0
	uint32_t N000051AF; //0x32C4
	uint32_t N00004050; //0x32C8
	uint32_t N000051B1; //0x32CC
	uint32_t N00004051; //0x32D0
	uint32_t N000051B3; //0x32D4
	uint32_t N00004052; //0x32D8
	uint32_t N000051B5; //0x32DC
	uint32_t N00004053; //0x32E0
	uint32_t N000051B7; //0x32E4
	uint32_t N00004054; //0x32E8
	uint32_t N000051B9; //0x32EC
	uint32_t N00004055; //0x32F0
	uint32_t N000051BB; //0x32F4
	uint32_t N00004056; //0x32F8
	uint32_t N000051BD; //0x32FC
	uint32_t N00004057; //0x3300
	uint32_t N000051BF; //0x3304
	uint32_t N00004058; //0x3308
	uint32_t N000051C1; //0x330C
	uint32_t N00004059; //0x3310
	uint32_t N000051C3; //0x3314
	uint32_t N0000405A; //0x3318
	uint32_t N000051C5; //0x331C
	uint32_t N0000405B; //0x3320
	uint32_t N000051C7; //0x3324
	uint32_t N0000405C; //0x3328
	uint32_t N000051C9; //0x332C
	uint32_t N0000405D; //0x3330
	uint32_t N000051CB; //0x3334
	uint32_t N0000405E; //0x3338
	uint32_t N000051CD; //0x333C
	uint32_t N0000405F; //0x3340
	uint32_t N000051CF; //0x3344
	uint32_t N00004060; //0x3348
	uint32_t N000051D1; //0x334C
	uint32_t N00004061; //0x3350
	uint32_t N000051D3; //0x3354
	uint32_t N00004062; //0x3358
	uint32_t N000051D5; //0x335C
	uint32_t N00004063; //0x3360
	uint32_t N000051D7; //0x3364
	uint32_t N00004064; //0x3368
	uint32_t N000051D9; //0x336C
	uint32_t N00004065; //0x3370
	uint32_t N000051DB; //0x3374
	uint32_t N00004066; //0x3378
	uint32_t N000051DD; //0x337C
	uint32_t N00004067; //0x3380
	uint32_t N000051DF; //0x3384
	uint32_t N00004068; //0x3388
	uint32_t N000051E1; //0x338C
	uint32_t N00004069; //0x3390
	uint32_t N000051E3; //0x3394
	uint32_t N0000406A; //0x3398
	uint32_t N000051E5; //0x339C
	uint32_t N0000406B; //0x33A0
	uint32_t N000051E7; //0x33A4
	uint32_t N0000406C; //0x33A8
	uint32_t N000051E9; //0x33AC
	uint32_t N0000406D; //0x33B0
	uint32_t N000051EB; //0x33B4
	uint32_t N0000406E; //0x33B8
	uint32_t N000051ED; //0x33BC
	uint32_t N0000406F; //0x33C0
	uint32_t N000051EF; //0x33C4
	uint32_t N00004070; //0x33C8
	uint32_t N000051F1; //0x33CC
	uint32_t N00004071; //0x33D0
	uint32_t N000051F3; //0x33D4
	uint32_t N00004072; //0x33D8
	uint32_t N000051F5; //0x33DC
	uint32_t N00004073; //0x33E0
	uint32_t N000051F7; //0x33E4
	uint32_t N00004074; //0x33E8
	uint32_t N000051F9; //0x33EC
	uint32_t N00004075; //0x33F0
	uint32_t N000051FB; //0x33F4
	uint32_t N00004076; //0x33F8
	uint32_t N000051FD; //0x33FC
	uint32_t N00004077; //0x3400
	uint32_t N000051FF; //0x3404
	uint32_t N00004078; //0x3408
	uint32_t N00005201; //0x340C
	uint32_t N00004079; //0x3410
	uint32_t N00005203; //0x3414
	uint32_t N0000407A; //0x3418
	uint32_t N00005205; //0x341C
	uint32_t N0000407B; //0x3420
	uint32_t N00005207; //0x3424
	uint32_t N0000407C; //0x3428
	uint32_t N00005209; //0x342C
	uint32_t N0000407D; //0x3430
	uint32_t N0000520B; //0x3434
	uint32_t N0000407E; //0x3438
	uint32_t N0000520D; //0x343C
	uint32_t N0000407F; //0x3440
	uint32_t N0000520F; //0x3444
	uint32_t N00004080; //0x3448
	uint32_t N00005211; //0x344C
	uint32_t N00004081; //0x3450
	uint32_t N00005213; //0x3454
	uint32_t N00004082; //0x3458
	uint32_t N00005215; //0x345C
	uint32_t N00004083; //0x3460
	uint32_t N00005217; //0x3464
	uint32_t N00004084; //0x3468
	uint32_t N00005219; //0x346C
	uint32_t N00004085; //0x3470
	uint32_t N0000521B; //0x3474
	uint32_t N00004086; //0x3478
	uint32_t N0000521D; //0x347C
	uint32_t N00004087; //0x3480
	uint32_t N0000521F; //0x3484
	uint32_t N00004088; //0x3488
	uint32_t N00005221; //0x348C
	uint32_t N00004089; //0x3490
	uint32_t N00005223; //0x3494
	uint32_t N0000408A; //0x3498
	uint32_t N00005225; //0x349C
	uint32_t N0000408B; //0x34A0
	uint32_t N00005227; //0x34A4
	uint32_t N0000408C; //0x34A8
	uint32_t N00005229; //0x34AC
	uint32_t N0000408D; //0x34B0
	uint32_t N0000522B; //0x34B4
	uint32_t N0000408E; //0x34B8
	uint32_t N0000522D; //0x34BC
	uint32_t N0000408F; //0x34C0
	uint32_t N0000522F; //0x34C4
	uint32_t N00004090; //0x34C8
	uint32_t N00005231; //0x34CC
	uint32_t N00004091; //0x34D0
	uint32_t N00005233; //0x34D4
	uint32_t N00004092; //0x34D8
	uint32_t N00005235; //0x34DC
	uint32_t N00004093; //0x34E0
	uint32_t N00005237; //0x34E4
	uint32_t N00004094; //0x34E8
	uint32_t N00005239; //0x34EC
	uint32_t N00004095; //0x34F0
	uint32_t N0000523B; //0x34F4
	uint32_t N00004096; //0x34F8
	uint32_t N0000523D; //0x34FC
	uint32_t N00004097; //0x3500
	uint32_t N0000523F; //0x3504
	uint32_t N00004098; //0x3508
	uint32_t N00005241; //0x350C
	uint32_t N00004099; //0x3510
	uint32_t N00005243; //0x3514
	uint32_t N0000409A; //0x3518
	uint32_t N00005245; //0x351C
	uint32_t N0000409B; //0x3520
	uint32_t N00005247; //0x3524
	uint32_t N0000409C; //0x3528
	uint32_t N00005249; //0x352C
	uint32_t N0000409D; //0x3530
	uint32_t N0000524B; //0x3534
	uint32_t N0000409E; //0x3538
	uint32_t N0000524D; //0x353C
	uint32_t N0000409F; //0x3540
	uint32_t N0000524F; //0x3544
	uint32_t N000040A0; //0x3548
	uint32_t N00005251; //0x354C
	uint32_t N000040A1; //0x3550
	uint32_t N00005253; //0x3554
	uint32_t N000040A2; //0x3558
	uint32_t N00005255; //0x355C
	uint32_t N000040A3; //0x3560
	uint32_t N00005257; //0x3564
	uint32_t N000040A4; //0x3568
	uint32_t N00005259; //0x356C
	uint32_t N000040A5; //0x3570
	uint32_t N0000525B; //0x3574
	uint32_t N000040A6; //0x3578
	uint32_t N0000525D; //0x357C
	uint32_t N000040A7; //0x3580
	uint32_t N0000525F; //0x3584
	uint32_t N000040A8; //0x3588
	uint32_t N00005261; //0x358C
	uint32_t N000040A9; //0x3590
	uint32_t N00005263; //0x3594
	uint32_t N000040AA; //0x3598
	uint32_t N00005265; //0x359C
	uint32_t N000040AB; //0x35A0
	uint32_t N00005267; //0x35A4
	uint32_t N000040AC; //0x35A8
	uint32_t N00005269; //0x35AC
	uint32_t N000040AD; //0x35B0
	uint32_t N0000526B; //0x35B4
	uint32_t N000040AE; //0x35B8
	uint32_t N0000526D; //0x35BC
	uint32_t N000040AF; //0x35C0
	uint32_t N0000526F; //0x35C4
	uint32_t N000040B0; //0x35C8
	uint32_t N00005271; //0x35CC
	uint32_t N000040B1; //0x35D0
	uint32_t N00005273; //0x35D4
	uint32_t N000040B2; //0x35D8
	uint32_t N00005275; //0x35DC
	uint32_t N000040B3; //0x35E0
	uint32_t N00005277; //0x35E4
	uint32_t N000040B4; //0x35E8
	uint32_t N00005279; //0x35EC
	uint32_t N000040B5; //0x35F0
	uint32_t N0000527B; //0x35F4
	uint32_t N000040B6; //0x35F8
	uint32_t N0000527D; //0x35FC
	uint32_t N000040B7; //0x3600
	uint32_t N0000527F; //0x3604
	uint32_t N000040B8; //0x3608
	uint32_t N00005281; //0x360C
	uint32_t N000040B9; //0x3610
	uint32_t N00005283; //0x3614
	uint32_t N000040BA; //0x3618
	uint32_t N00005285; //0x361C
	uint32_t N000040BB; //0x3620
	uint32_t N00005287; //0x3624
	uint32_t N000040BC; //0x3628
	uint32_t N00005289; //0x362C
	uint32_t N000040BD; //0x3630
	uint32_t N0000528B; //0x3634
	uint32_t N000040BE; //0x3638
	uint32_t N0000528D; //0x363C
	uint32_t N000040BF; //0x3640
	uint32_t N0000528F; //0x3644
	uint32_t N000040C0; //0x3648
	uint32_t N00005291; //0x364C
	uint32_t N000040C1; //0x3650
	uint32_t N00005293; //0x3654
	uint32_t N000040C2; //0x3658
	uint32_t N00005295; //0x365C
	uint32_t N000040C3; //0x3660
	uint32_t N00005297; //0x3664
	uint32_t N000040C4; //0x3668
	uint32_t N00005299; //0x366C
	uint32_t N000040C5; //0x3670
	uint32_t N0000529B; //0x3674
	uint32_t N000040C6; //0x3678
	uint32_t N0000529D; //0x367C
	uint32_t N000040C7; //0x3680
	uint32_t N0000529F; //0x3684
	uint32_t N000040C8; //0x3688
	uint32_t N000052A1; //0x368C
	uint32_t N000040C9; //0x3690
	uint32_t N000052A3; //0x3694
	uint32_t N000040CA; //0x3698
	uint32_t N000052A5; //0x369C
	uint32_t N000040CB; //0x36A0
	uint32_t N000052A7; //0x36A4
	uint32_t N000040CC; //0x36A8
	uint32_t N000052A9; //0x36AC
	uint32_t N000040CD; //0x36B0
	uint32_t N000052AB; //0x36B4
	uint32_t N000040CE; //0x36B8
	uint32_t N000052AD; //0x36BC
	uint32_t N000040CF; //0x36C0
	uint32_t N000052AF; //0x36C4
	uint32_t N000040D0; //0x36C8
	uint32_t N000052B1; //0x36CC
	uint32_t N000040D1; //0x36D0
	uint32_t N000052B3; //0x36D4
	uint32_t N000040D2; //0x36D8
	uint32_t N000052B5; //0x36DC
	uint32_t N000040D3; //0x36E0
	uint32_t N000052B7; //0x36E4
	uint32_t N000040D4; //0x36E8
	uint32_t N000052B9; //0x36EC
	uint32_t N000040D5; //0x36F0
	uint32_t N000052BB; //0x36F4
	uint32_t N000040D6; //0x36F8
	uint32_t N000052BD; //0x36FC
	uint32_t N000040D7; //0x3700
	uint32_t N000052BF; //0x3704
	uint32_t N000040D8; //0x3708
	uint32_t N000052C1; //0x370C
	uint32_t N000040D9; //0x3710
	uint32_t N000052C3; //0x3714
	uint32_t N000040DA; //0x3718
	uint32_t N000052C5; //0x371C
	uint32_t N000040DB; //0x3720
	uint32_t N000052C7; //0x3724
	uint32_t N000040DC; //0x3728
	uint32_t N000052C9; //0x372C
	uint32_t N000040DD; //0x3730
	uint32_t N000052CB; //0x3734
	uint32_t N000040DE; //0x3738
	uint32_t N000052CD; //0x373C
	uint32_t N000040DF; //0x3740
	uint32_t N000052CF; //0x3744
	uint32_t N000040E0; //0x3748
	uint32_t N000052D1; //0x374C
	uint32_t N000040E1; //0x3750
	uint32_t N000052D3; //0x3754
	uint32_t N000040E2; //0x3758
	uint32_t N000052D5; //0x375C
	uint32_t N000040E3; //0x3760
	uint32_t N000052D7; //0x3764
	uint32_t N000040E4; //0x3768
	uint32_t N000052D9; //0x376C
	uint32_t N000040E5; //0x3770
	uint32_t N000052DB; //0x3774
	uint32_t N000040E6; //0x3778
	uint32_t N000052DD; //0x377C
	uint32_t N000040E7; //0x3780
	uint32_t N000052DF; //0x3784
	uint32_t N000040E8; //0x3788
	uint32_t N000052E1; //0x378C
	uint32_t N000040E9; //0x3790
	uint32_t N000052E3; //0x3794
	uint32_t N000040EA; //0x3798
	uint32_t N000052E5; //0x379C
	uint32_t N000040EB; //0x37A0
	uint32_t N000052E7; //0x37A4
	uint32_t N000040EC; //0x37A8
	uint32_t N000052E9; //0x37AC
	uint32_t N000040ED; //0x37B0
	uint32_t N000052EB; //0x37B4
	uint32_t N000040EE; //0x37B8
	uint32_t N000052ED; //0x37BC
	uint32_t N000040EF; //0x37C0
	uint32_t N000052EF; //0x37C4
	uint32_t N000040F0; //0x37C8
	uint32_t N000052F1; //0x37CC
	uint32_t N000040F1; //0x37D0
	uint32_t N000052F3; //0x37D4
	uint32_t N000040F2; //0x37D8
	uint32_t N000052F5; //0x37DC
	uint32_t N000040F3; //0x37E0
	uint32_t N000052F7; //0x37E4
	uint32_t N000040F4; //0x37E8
	uint32_t N000052F9; //0x37EC
	uint32_t N000040F5; //0x37F0
	uint32_t N000052FB; //0x37F4
	uint32_t N000040F6; //0x37F8
	uint32_t N000052FD; //0x37FC
	uint32_t N000040F7; //0x3800
	uint32_t N000052FF; //0x3804
	uint32_t N000040F8; //0x3808
	uint32_t N00005301; //0x380C
	uint32_t N000040F9; //0x3810
	uint32_t N00005303; //0x3814
	uint32_t N000040FA; //0x3818
	uint32_t N00005305; //0x381C
	uint32_t N000040FB; //0x3820
	uint32_t N00005307; //0x3824
	uint32_t N000040FC; //0x3828
	uint32_t N00005309; //0x382C
	uint32_t N000040FD; //0x3830
	uint32_t N0000530B; //0x3834
	uint32_t N000040FE; //0x3838
	uint32_t N0000530D; //0x383C
	uint32_t N000040FF; //0x3840
	uint32_t N0000530F; //0x3844
	uint32_t N00004100; //0x3848
	uint32_t N00005311; //0x384C
	uint32_t N00004101; //0x3850
	uint32_t N00005313; //0x3854
	uint32_t N00004102; //0x3858
	uint32_t N00005315; //0x385C
	uint32_t N00004103; //0x3860
	uint32_t N00005317; //0x3864
	uint32_t N00004104; //0x3868
	uint32_t N00005319; //0x386C
	uint32_t N00004105; //0x3870
	uint32_t N0000531B; //0x3874
	uint32_t N00004106; //0x3878
	uint32_t N0000531D; //0x387C
	uint32_t N00004107; //0x3880
	uint32_t N0000531F; //0x3884
	uint32_t N00004108; //0x3888
	uint32_t N00005321; //0x388C
	uint32_t N00004109; //0x3890
	uint32_t N00005323; //0x3894
	uint32_t N0000410A; //0x3898
	uint32_t N00005325; //0x389C
	uint32_t N0000410B; //0x38A0
	uint32_t N00005327; //0x38A4
	uint32_t N0000410C; //0x38A8
	uint32_t N00005329; //0x38AC
	uint32_t N0000410D; //0x38B0
	uint32_t N0000532B; //0x38B4
	uint32_t N0000410E; //0x38B8
	uint32_t N0000532D; //0x38BC
	uint32_t N0000410F; //0x38C0
	uint32_t N0000532F; //0x38C4
	uint32_t N00004110; //0x38C8
	uint32_t N00005331; //0x38CC
	uint32_t N00004111; //0x38D0
	uint32_t N00005333; //0x38D4
	uint32_t N00004112; //0x38D8
	uint32_t N00005335; //0x38DC
	uint32_t N00004113; //0x38E0
	uint32_t N00005337; //0x38E4
	uint32_t N00004114; //0x38E8
	uint32_t N00005339; //0x38EC
	uint32_t N00004115; //0x38F0
	uint32_t N0000533B; //0x38F4
	uint32_t N00004116; //0x38F8
	uint32_t N0000533D; //0x38FC
	uint32_t N00004117; //0x3900
	uint32_t N0000533F; //0x3904
	uint32_t N00004118; //0x3908
	uint32_t N00005341; //0x390C
	uint32_t N00004119; //0x3910
	uint32_t N00005343; //0x3914
	uint32_t N0000411A; //0x3918
	uint32_t N00005345; //0x391C
	uint32_t N0000411B; //0x3920
	uint32_t N00005347; //0x3924
	uint32_t N0000411C; //0x3928
	uint32_t N00005349; //0x392C
	uint32_t N0000411D; //0x3930
	uint32_t N0000534B; //0x3934
	uint32_t N0000411E; //0x3938
	uint32_t N0000534D; //0x393C
	uint32_t N0000411F; //0x3940
	uint32_t N0000534F; //0x3944
	uint32_t N00004120; //0x3948
	uint32_t N00005351; //0x394C
	uint32_t N00004121; //0x3950
	uint32_t N00005353; //0x3954
	uint32_t N00004122; //0x3958
	uint32_t N00005355; //0x395C
	uint32_t N00004123; //0x3960
	uint32_t N00005357; //0x3964
	uint32_t N00004124; //0x3968
	uint32_t N00005359; //0x396C
	uint32_t N00004125; //0x3970
	uint32_t N0000535B; //0x3974
	uint32_t N00004126; //0x3978
	uint32_t N0000535D; //0x397C
	uint32_t N00004127; //0x3980
	uint32_t N0000535F; //0x3984
	uint32_t N00004128; //0x3988
	uint32_t N00005361; //0x398C
	uint32_t N00004129; //0x3990
	uint32_t N00005363; //0x3994
	uint32_t N0000412A; //0x3998
	uint32_t N00005365; //0x399C
	uint32_t N0000412B; //0x39A0
	uint32_t N00005367; //0x39A4
	uint32_t N0000412C; //0x39A8
	uint32_t N00005369; //0x39AC
	uint32_t N0000412D; //0x39B0
	uint32_t N0000536B; //0x39B4
	uint32_t N0000412E; //0x39B8
	uint32_t N0000536D; //0x39BC
	uint32_t N0000412F; //0x39C0
	uint32_t N0000536F; //0x39C4
	uint32_t N00004130; //0x39C8
	uint32_t N00005371; //0x39CC
	uint32_t N00004131; //0x39D0
	uint32_t N00005373; //0x39D4
	uint32_t N00004132; //0x39D8
	uint32_t N00005375; //0x39DC
	uint32_t N00004133; //0x39E0
	uint32_t N00005377; //0x39E4
	uint32_t N00004134; //0x39E8
	uint32_t N00005379; //0x39EC
	uint32_t N00004135; //0x39F0
	uint32_t N0000537B; //0x39F4
	uint32_t N00004136; //0x39F8
	uint32_t N0000537D; //0x39FC
	uint32_t N00004137; //0x3A00
	uint32_t N0000537F; //0x3A04
	uint32_t N00004138; //0x3A08
	uint32_t N00005381; //0x3A0C
	uint32_t N00004139; //0x3A10
	uint32_t N00005383; //0x3A14
	uint32_t N0000413A; //0x3A18
	uint32_t N00005385; //0x3A1C
	uint32_t N0000413B; //0x3A20
	uint32_t N00005387; //0x3A24
	uint32_t N0000413C; //0x3A28
	uint32_t N00005389; //0x3A2C
	uint32_t N0000413D; //0x3A30
	uint32_t N0000538B; //0x3A34
	uint32_t N0000413E; //0x3A38
	uint32_t N0000538D; //0x3A3C
	uint32_t N0000413F; //0x3A40
	uint32_t N0000538F; //0x3A44
	uint32_t N00004140; //0x3A48
	uint32_t N00005391; //0x3A4C
	uint32_t N00004141; //0x3A50
	uint32_t N00005393; //0x3A54
	uint32_t N00004142; //0x3A58
	uint32_t N00005395; //0x3A5C
	uint32_t N00004143; //0x3A60
	uint32_t N00005397; //0x3A64
	uint32_t N00004144; //0x3A68
	uint32_t N00005399; //0x3A6C
	uint32_t N00004145; //0x3A70
	uint32_t N0000539B; //0x3A74
	uint32_t N00004146; //0x3A78
	uint32_t N0000539D; //0x3A7C
	uint32_t N00004147; //0x3A80
	uint32_t N0000539F; //0x3A84
	uint32_t N00004148; //0x3A88
	uint32_t N000053A1; //0x3A8C
	uint32_t N00004149; //0x3A90
	uint32_t N000053A3; //0x3A94
	uint32_t N0000414A; //0x3A98
	uint32_t N000053A5; //0x3A9C
	uint32_t N0000414B; //0x3AA0
	uint32_t N000053A7; //0x3AA4
	uint32_t N0000414C; //0x3AA8
	uint32_t N000053A9; //0x3AAC
	uint32_t N0000414D; //0x3AB0
	uint32_t N000053AB; //0x3AB4
	uint32_t N0000414E; //0x3AB8
	uint32_t N000053AD; //0x3ABC
	uint32_t N0000414F; //0x3AC0
	uint32_t N000053AF; //0x3AC4
	uint32_t N00004150; //0x3AC8
	uint32_t N000053B1; //0x3ACC
	uint32_t N00004151; //0x3AD0
	uint32_t N000053B3; //0x3AD4
	uint32_t N00004152; //0x3AD8
	uint32_t N000053B5; //0x3ADC
	uint32_t N00004153; //0x3AE0
	uint32_t N000053B7; //0x3AE4
	uint32_t N00004154; //0x3AE8
	uint32_t N000053B9; //0x3AEC
	uint32_t N00004155; //0x3AF0
	uint32_t N000053BB; //0x3AF4
	uint32_t N00004156; //0x3AF8
	uint32_t N000053BD; //0x3AFC
	uint32_t N00004157; //0x3B00
	uint32_t N000053BF; //0x3B04
	uint32_t N00004158; //0x3B08
	uint32_t N000053C1; //0x3B0C
	uint32_t N00004159; //0x3B10
	uint32_t N000053C3; //0x3B14
	uint32_t N0000415A; //0x3B18
	uint32_t N000053C5; //0x3B1C
	uint32_t N0000415B; //0x3B20
	uint32_t N000053C7; //0x3B24
	uint32_t N0000415C; //0x3B28
	uint32_t N000053C9; //0x3B2C
	uint32_t N0000415D; //0x3B30
	uint32_t N000053CB; //0x3B34
	uint32_t N0000415E; //0x3B38
	uint32_t N000053CD; //0x3B3C
	uint32_t N0000415F; //0x3B40
	uint32_t N000053CF; //0x3B44
	uint32_t N00004160; //0x3B48
	uint32_t N000053D1; //0x3B4C
	uint32_t N00004161; //0x3B50
	uint32_t N000053D3; //0x3B54
	uint32_t N00004162; //0x3B58
	uint32_t N000053D5; //0x3B5C
	uint32_t N00004163; //0x3B60
	uint32_t N000053D7; //0x3B64
	uint32_t N00004164; //0x3B68
	uint32_t N000053D9; //0x3B6C
	uint32_t N00004165; //0x3B70
	uint32_t N000053DB; //0x3B74
	uint32_t N00004166; //0x3B78
	uint32_t N000053DD; //0x3B7C
	uint32_t N00004167; //0x3B80
	uint32_t N000053DF; //0x3B84
	uint32_t N00004168; //0x3B88
	uint32_t N000053E1; //0x3B8C
	uint32_t N00004169; //0x3B90
	uint32_t N000053E3; //0x3B94
	uint32_t N0000416A; //0x3B98
	uint32_t N000053E5; //0x3B9C
	uint32_t N0000416B; //0x3BA0
	uint32_t N000053E7; //0x3BA4
	uint32_t N0000416C; //0x3BA8
	uint32_t N000053E9; //0x3BAC
	uint32_t N0000416D; //0x3BB0
	uint32_t N000053EB; //0x3BB4
	uint32_t N0000416E; //0x3BB8
	uint32_t N000053ED; //0x3BBC
	uint32_t N0000416F; //0x3BC0
	uint32_t N000053EF; //0x3BC4
	uint32_t N00004170; //0x3BC8
	uint32_t N000053F1; //0x3BCC
	uint32_t N00004171; //0x3BD0
	uint32_t N000053F3; //0x3BD4
	uint32_t N00004172; //0x3BD8
	uint32_t N000053F5; //0x3BDC
	uint32_t N00004173; //0x3BE0
	uint32_t N000053F7; //0x3BE4
	uint32_t N00004174; //0x3BE8
	uint32_t N000053F9; //0x3BEC
	uint32_t N00004175; //0x3BF0
	uint32_t N000053FB; //0x3BF4
	uint32_t N00004176; //0x3BF8
	uint32_t N000053FD; //0x3BFC
	uint32_t N00004177; //0x3C00
	uint32_t N000053FF; //0x3C04
	uint32_t N00004178; //0x3C08
	uint32_t N00005401; //0x3C0C
	uint32_t N00004179; //0x3C10
	uint32_t N00005403; //0x3C14
	uint32_t N0000417A; //0x3C18
	uint32_t N00005405; //0x3C1C
	uint32_t N0000417B; //0x3C20
	uint32_t N00005407; //0x3C24
	uint32_t N0000417C; //0x3C28
	uint32_t N00005409; //0x3C2C
	uint32_t N0000417D; //0x3C30
	uint32_t N0000540B; //0x3C34
	uint32_t N0000417E; //0x3C38
	uint32_t N0000540D; //0x3C3C
	uint32_t N0000417F; //0x3C40
	uint32_t N0000540F; //0x3C44
	uint32_t N00004180; //0x3C48
	uint32_t N00005411; //0x3C4C
	uint32_t N00004181; //0x3C50
	uint32_t N00005413; //0x3C54
	uint32_t N00004182; //0x3C58
	uint32_t N00005415; //0x3C5C
	uint32_t N00004183; //0x3C60
	uint32_t N00005417; //0x3C64
	uint32_t N00004184; //0x3C68
	uint32_t N00005419; //0x3C6C
	uint32_t N00004185; //0x3C70
	uint32_t N0000541B; //0x3C74
	uint32_t N00004186; //0x3C78
	uint32_t N0000541D; //0x3C7C
	uint32_t N00004187; //0x3C80
	uint32_t N0000541F; //0x3C84
	uint32_t N00004188; //0x3C88
	uint32_t N00005421; //0x3C8C
	uint32_t N00004189; //0x3C90
	uint32_t N00005423; //0x3C94
	uint32_t N0000418A; //0x3C98
	uint32_t N00005425; //0x3C9C
	uint32_t N0000418B; //0x3CA0
	uint32_t N00005427; //0x3CA4
	uint32_t N0000418C; //0x3CA8
	uint32_t N00005429; //0x3CAC
	uint32_t N0000418D; //0x3CB0
	uint32_t N0000542B; //0x3CB4
	uint32_t N0000418E; //0x3CB8
	uint32_t N0000542D; //0x3CBC
	uint32_t N0000418F; //0x3CC0
	uint32_t N0000542F; //0x3CC4
	uint32_t N00004190; //0x3CC8
	uint32_t N00005431; //0x3CCC
	uint32_t N00004191; //0x3CD0
	uint32_t N00005433; //0x3CD4
	uint32_t N00004192; //0x3CD8
	uint32_t N00005435; //0x3CDC
	uint32_t N00004193; //0x3CE0
	uint32_t N00005437; //0x3CE4
	uint32_t N00004194; //0x3CE8
	uint32_t N00005439; //0x3CEC
	uint32_t N00004195; //0x3CF0
	uint32_t N0000543B; //0x3CF4
	uint32_t N00004196; //0x3CF8
	uint32_t N0000543D; //0x3CFC
	uint32_t N00004197; //0x3D00
	uint32_t N0000543F; //0x3D04
	uint32_t N00004198; //0x3D08
	uint32_t N00005441; //0x3D0C
	uint32_t N00004199; //0x3D10
	uint32_t N00005443; //0x3D14
	uint32_t N0000419A; //0x3D18
	uint32_t N00005445; //0x3D1C
	uint32_t N0000419B; //0x3D20
	uint32_t N00005447; //0x3D24
	uint32_t N0000419C; //0x3D28
	uint32_t N00005449; //0x3D2C
	uint32_t N0000419D; //0x3D30
	uint32_t N0000544B; //0x3D34
	uint32_t N0000419E; //0x3D38
	uint32_t N0000544D; //0x3D3C
	uint32_t N0000419F; //0x3D40
	uint32_t N0000544F; //0x3D44
	uint32_t N000041A0; //0x3D48
	uint32_t N00005451; //0x3D4C
	uint32_t N000041A1; //0x3D50
	uint32_t N00005453; //0x3D54
	uint32_t N000041A2; //0x3D58
	uint32_t N00005455; //0x3D5C
	uint32_t N000041A3; //0x3D60
	uint32_t N00005457; //0x3D64
	uint32_t N000041A4; //0x3D68
	uint32_t N00005459; //0x3D6C
	uint32_t N000041A5; //0x3D70
	uint32_t N0000545B; //0x3D74
	uint32_t N000041A6; //0x3D78
	uint32_t N0000545D; //0x3D7C
	uint32_t N000041A7; //0x3D80
	uint32_t N0000545F; //0x3D84
	uint32_t N000041A8; //0x3D88
	uint32_t N00005461; //0x3D8C
	uint32_t N000041A9; //0x3D90
	uint32_t N00005463; //0x3D94
	uint32_t N000041AA; //0x3D98
	uint32_t N00005465; //0x3D9C
	uint32_t N000041AB; //0x3DA0
	uint32_t N00005467; //0x3DA4
	uint32_t N000041AC; //0x3DA8
	uint32_t N00005469; //0x3DAC
	uint32_t N000041AD; //0x3DB0
	uint32_t N0000546B; //0x3DB4
	uint32_t N000041AE; //0x3DB8
	uint32_t N0000546D; //0x3DBC
	uint32_t N000041AF; //0x3DC0
	uint32_t N0000546F; //0x3DC4
	uint32_t N000041B0; //0x3DC8
	uint32_t N00005471; //0x3DCC
	uint32_t N000041B1; //0x3DD0
	uint32_t N00005473; //0x3DD4
	uint32_t N000041B2; //0x3DD8
	uint32_t N00005475; //0x3DDC
	uint32_t N000041B3; //0x3DE0
	uint32_t N00005477; //0x3DE4
	uint32_t N000041B4; //0x3DE8
	uint32_t N00005479; //0x3DEC
	uint32_t N000041B5; //0x3DF0
	uint32_t N0000547B; //0x3DF4
	uint32_t N000041B6; //0x3DF8
	uint32_t N0000547D; //0x3DFC
	uint32_t N000041B7; //0x3E00
	uint32_t N0000547F; //0x3E04
	uint32_t N000041B8; //0x3E08
	uint32_t N00005481; //0x3E0C
	uint32_t N000041B9; //0x3E10
	uint32_t N00005483; //0x3E14
	uint32_t N000041BA; //0x3E18
	uint32_t N00005485; //0x3E1C
	uint32_t N000041BB; //0x3E20
	uint32_t N00005487; //0x3E24
	uint32_t N000041BC; //0x3E28
	uint32_t N00005489; //0x3E2C
	uint32_t N000041BD; //0x3E30
	uint32_t N0000548B; //0x3E34
	uint32_t N000041BE; //0x3E38
	uint32_t N0000548D; //0x3E3C
	uint32_t N000041BF; //0x3E40
	uint32_t N0000548F; //0x3E44
	uint32_t N000041C0; //0x3E48
	uint32_t N00005491; //0x3E4C
	uint32_t N000041C1; //0x3E50
	uint32_t N00005493; //0x3E54
	uint32_t N000041C2; //0x3E58
	uint32_t N00005495; //0x3E5C
	uint32_t N000041C3; //0x3E60
	uint32_t N00005497; //0x3E64
	uint32_t N000041C4; //0x3E68
	uint32_t N00005499; //0x3E6C
	uint32_t N000041C5; //0x3E70
	uint32_t N0000549B; //0x3E74
	uint32_t N000041C6; //0x3E78
	uint32_t N0000549D; //0x3E7C
	uint32_t N000041C7; //0x3E80
	uint32_t N0000549F; //0x3E84
	uint32_t N000041C8; //0x3E88
	uint32_t N000054A1; //0x3E8C
	uint32_t N000041C9; //0x3E90
	uint32_t N000054A3; //0x3E94
	uint32_t N000041CA; //0x3E98
	uint32_t N000054A5; //0x3E9C
	uint32_t N000041CB; //0x3EA0
	uint32_t N000054A7; //0x3EA4
	uint32_t N000041CC; //0x3EA8
	uint32_t N000054A9; //0x3EAC
	uint32_t N000041CD; //0x3EB0
	uint32_t N000054AB; //0x3EB4
	uint32_t N000041CE; //0x3EB8
	uint32_t N000054AD; //0x3EBC
	uint32_t N000041CF; //0x3EC0
	uint32_t N000054AF; //0x3EC4
	uint32_t N000041D0; //0x3EC8
	uint32_t N000054B1; //0x3ECC
	uint32_t N000041D1; //0x3ED0
	uint32_t N000054B3; //0x3ED4
	uint32_t N000041D2; //0x3ED8
	uint32_t N000054B5; //0x3EDC
	uint32_t N000041D3; //0x3EE0
	uint32_t N000054B7; //0x3EE4
	uint32_t N000041D4; //0x3EE8
	uint32_t N000054B9; //0x3EEC
	uint32_t N000041D5; //0x3EF0
	uint32_t N000054BB; //0x3EF4
	uint32_t N000041D6; //0x3EF8
	uint32_t N000054BD; //0x3EFC
	uint32_t N000041D7; //0x3F00
	uint32_t N000054BF; //0x3F04
	uint32_t N000041D8; //0x3F08
	uint32_t N000054C1; //0x3F0C
	uint32_t N000041D9; //0x3F10
	uint32_t N000054C3; //0x3F14
	uint32_t N000041DA; //0x3F18
	uint32_t N000054C5; //0x3F1C
	uint32_t N000041DB; //0x3F20
	uint32_t N000054C7; //0x3F24
	uint32_t N000041DC; //0x3F28
	uint32_t N000054C9; //0x3F2C
	uint32_t N000041DD; //0x3F30
	uint32_t N000054CB; //0x3F34
	uint32_t N000041DE; //0x3F38
	uint32_t N000054CD; //0x3F3C
	uint32_t N000041DF; //0x3F40
	uint32_t N000054CF; //0x3F44
	uint32_t N000041E0; //0x3F48
	uint32_t N000054D1; //0x3F4C
	uint32_t N000041E1; //0x3F50
	uint32_t N000054D3; //0x3F54
	uint32_t N000041E2; //0x3F58
	uint32_t N000054D5; //0x3F5C
	uint32_t N000041E3; //0x3F60
	uint32_t N000054D7; //0x3F64
	uint32_t N000041E4; //0x3F68
	uint32_t N000054D9; //0x3F6C
	uint32_t N000041E5; //0x3F70
	uint32_t N000054DB; //0x3F74
	uint32_t N000041E6; //0x3F78
	uint32_t N000054DD; //0x3F7C
	uint32_t N000041E7; //0x3F80
	uint32_t N000054DF; //0x3F84
	uint32_t N000041E8; //0x3F88
	uint32_t N000054E1; //0x3F8C
	uint32_t N000041E9; //0x3F90
	uint32_t N000054E3; //0x3F94
	uint32_t N000041EA; //0x3F98
	uint32_t N000054E5; //0x3F9C
	uint32_t N000041EB; //0x3FA0
	uint32_t N000054E7; //0x3FA4
	uint32_t N000041EC; //0x3FA8
	uint32_t N000054E9; //0x3FAC
	uint32_t N000041ED; //0x3FB0
	uint32_t N000054EB; //0x3FB4
	uint32_t N000041EE; //0x3FB8
	uint32_t N000054ED; //0x3FBC
	uint32_t N000041EF; //0x3FC0
	uint32_t N000054EF; //0x3FC4
	uint32_t N000041F0; //0x3FC8
	uint32_t N000054F1; //0x3FCC
	uint32_t N000041F1; //0x3FD0
	uint32_t N000054F3; //0x3FD4
	uint32_t N000041F2; //0x3FD8
	uint32_t N000054F5; //0x3FDC
	uint32_t N000041F3; //0x3FE0
	uint32_t N000054F7; //0x3FE4
	uint32_t N000041F4; //0x3FE8
	uint32_t N000054F9; //0x3FEC
	uint32_t N000041F5; //0x3FF0
	uint32_t N000054FB; //0x3FF4
	uint32_t N000041F6; //0x3FF8
	uint32_t N000054FD; //0x3FFC
	uint32_t N000041F7; //0x4000
	uint32_t N000054FF; //0x4004
	uint32_t N000041F8; //0x4008
	uint32_t N00005501; //0x400C
	uint32_t N000041F9; //0x4010
	uint32_t N00005503; //0x4014
	uint32_t N000041FA; //0x4018
	uint32_t N00005505; //0x401C
	uint32_t N000041FB; //0x4020
	uint32_t N00005507; //0x4024
	uint32_t N000041FC; //0x4028
	uint32_t N00005509; //0x402C
	uint32_t N000041FD; //0x4030
	uint32_t N0000550B; //0x4034
	uint32_t N000041FE; //0x4038
	uint32_t N0000550D; //0x403C
	uint32_t N000041FF; //0x4040
	uint32_t N0000550F; //0x4044
	uint32_t N00004200; //0x4048
	uint32_t N00005511; //0x404C
	uint32_t N00004201; //0x4050
	uint32_t N00005513; //0x4054
	uint32_t N00004202; //0x4058
	uint32_t N00005515; //0x405C
	uint32_t N00004203; //0x4060
	uint32_t N00005517; //0x4064
	uint32_t N00004204; //0x4068
	uint32_t N00005519; //0x406C
	uint32_t N00004205; //0x4070
	uint32_t N0000551B; //0x4074
	uint32_t N00004206; //0x4078
	uint32_t N0000551D; //0x407C
	uint32_t N00004207; //0x4080
	uint32_t N0000551F; //0x4084
	uint32_t N00004208; //0x4088
	uint32_t N00005521; //0x408C
	uint32_t N00004209; //0x4090
	uint32_t N00005523; //0x4094
	uint32_t N0000420A; //0x4098
	uint32_t N00005525; //0x409C
	uint32_t N0000420B; //0x40A0
	uint32_t N00005527; //0x40A4
	uint32_t N0000420C; //0x40A8
	uint32_t N00005529; //0x40AC
	uint32_t N0000420D; //0x40B0
	uint32_t N0000552B; //0x40B4
	uint32_t N0000420E; //0x40B8
	uint32_t N0000552D; //0x40BC
	uint32_t N0000420F; //0x40C0
	uint32_t N0000552F; //0x40C4
	uint32_t N00004210; //0x40C8
	uint32_t N00005531; //0x40CC
	uint32_t N00004211; //0x40D0
	uint32_t N00005533; //0x40D4
	uint32_t N00004212; //0x40D8
	uint32_t N00005535; //0x40DC
	uint32_t N00004213; //0x40E0
	uint32_t N00005537; //0x40E4
	uint32_t N00004214; //0x40E8
	uint32_t N00005539; //0x40EC
	uint32_t N00004215; //0x40F0
	uint32_t N0000553B; //0x40F4
	uint32_t N00004216; //0x40F8
	uint32_t N0000553D; //0x40FC
	uint32_t N00004217; //0x4100
	uint32_t N0000553F; //0x4104
	uint32_t N00004218; //0x4108
	uint32_t N00005541; //0x410C
	uint32_t N00004219; //0x4110
	uint32_t N00005543; //0x4114
	uint32_t N0000421A; //0x4118
	uint32_t N00005545; //0x411C
	uint32_t N0000421B; //0x4120
	uint32_t N00005547; //0x4124
	uint32_t N0000421C; //0x4128
	uint32_t N00005549; //0x412C
	uint32_t N0000421D; //0x4130
	uint32_t N0000554B; //0x4134
	uint32_t N0000421E; //0x4138
	uint32_t N0000554D; //0x413C
	uint32_t N0000421F; //0x4140
	uint32_t N0000554F; //0x4144
	uint32_t N00004220; //0x4148
	uint32_t N00005551; //0x414C
	uint32_t N00004221; //0x4150
	uint32_t N00005553; //0x4154
	uint32_t N00004222; //0x4158
	uint32_t N00005555; //0x415C
	uint32_t N00004223; //0x4160
	uint32_t N00005557; //0x4164
	uint32_t N00004224; //0x4168
	uint32_t N00005559; //0x416C
	uint32_t N00004225; //0x4170
	uint32_t N0000555B; //0x4174
	uint32_t N00004226; //0x4178
	uint32_t N0000555D; //0x417C
	uint32_t N00004227; //0x4180
	uint32_t N0000555F; //0x4184
	uint32_t N00004228; //0x4188
	uint32_t N00005561; //0x418C
	uint32_t N00004229; //0x4190
	uint32_t N00005563; //0x4194
	uint32_t N0000422A; //0x4198
	uint32_t N00005565; //0x419C
	uint32_t N0000422B; //0x41A0
	uint32_t N00005567; //0x41A4
	uint32_t N0000422C; //0x41A8
	uint32_t N00005569; //0x41AC
	uint32_t N0000422D; //0x41B0
	uint32_t N0000556B; //0x41B4
	uint32_t N0000422E; //0x41B8
	uint32_t N0000556D; //0x41BC
	uint32_t N0000422F; //0x41C0
	uint32_t N0000556F; //0x41C4
	uint32_t N00004230; //0x41C8
	uint32_t N00005571; //0x41CC
	uint32_t N00004231; //0x41D0
	uint32_t N00005573; //0x41D4
	uint32_t N00004232; //0x41D8
	uint32_t N00005575; //0x41DC
	uint32_t N00004233; //0x41E0
	uint32_t N00005577; //0x41E4
	uint32_t N00004234; //0x41E8
	uint32_t N00005579; //0x41EC
	uint32_t N00004235; //0x41F0
	uint32_t N0000557B; //0x41F4
	uint32_t N00004236; //0x41F8
	uint32_t N0000557D; //0x41FC
	uint32_t N00004237; //0x4200
	uint32_t N0000557F; //0x4204
	uint32_t N00004238; //0x4208
	uint32_t N00005581; //0x420C
	uint32_t N00004239; //0x4210
	uint32_t N00005583; //0x4214
	uint32_t N0000423A; //0x4218
	uint32_t N00005585; //0x421C
	uint32_t N0000423B; //0x4220
	uint32_t N00005587; //0x4224
	uint32_t N0000423C; //0x4228
	uint32_t N00005589; //0x422C
	uint32_t N0000423D; //0x4230
	uint32_t N0000558B; //0x4234
	uint32_t N0000423E; //0x4238
	uint32_t N0000558D; //0x423C
	uint32_t N0000423F; //0x4240
	uint32_t N0000558F; //0x4244
	uint32_t N00004240; //0x4248
	uint32_t N00005591; //0x424C
	uint32_t N00004241; //0x4250
	uint32_t N00005593; //0x4254
	uint32_t N00004242; //0x4258
	uint32_t N00005595; //0x425C
	uint32_t N00004243; //0x4260
	uint32_t N00005597; //0x4264
	uint32_t N00004244; //0x4268
	uint32_t N00005599; //0x426C
	uint32_t N00004245; //0x4270
	uint32_t N0000559B; //0x4274
	uint32_t N00004246; //0x4278
	uint32_t N0000559D; //0x427C
	uint32_t N00004247; //0x4280
	uint32_t N0000559F; //0x4284
	uint32_t N00004248; //0x4288
	uint32_t N000055A1; //0x428C
	uint32_t N00004249; //0x4290
	uint32_t N000055A3; //0x4294
	uint32_t N0000424A; //0x4298
	uint32_t N000055A5; //0x429C
	uint32_t N0000424B; //0x42A0
	uint32_t N000055A7; //0x42A4
	uint32_t N0000424C; //0x42A8
	uint32_t N000055A9; //0x42AC
	uint32_t N0000424D; //0x42B0
	uint32_t N000055AB; //0x42B4
	uint32_t N0000424E; //0x42B8
	uint32_t N000055AD; //0x42BC
	uint32_t N0000424F; //0x42C0
	uint32_t N000055AF; //0x42C4
	uint32_t N00004250; //0x42C8
	uint32_t N000055B1; //0x42CC
	uint32_t N00004251; //0x42D0
	uint32_t N000055B3; //0x42D4
	uint32_t N00004252; //0x42D8
	uint32_t N000055B5; //0x42DC
	uint32_t N00004253; //0x42E0
	uint32_t N000055B7; //0x42E4
	uint32_t N00004254; //0x42E8
	uint32_t N000055B9; //0x42EC
	uint32_t N00004255; //0x42F0
	uint32_t N000055BB; //0x42F4
	uint32_t N00004256; //0x42F8
	uint32_t N000055BD; //0x42FC
	uint32_t N00004257; //0x4300
	uint32_t N000055BF; //0x4304
	uint32_t N00004258; //0x4308
	uint32_t N000055C1; //0x430C
	uint32_t N00004259; //0x4310
	uint32_t N000055C3; //0x4314
	uint32_t N0000425A; //0x4318
	uint32_t N000055C5; //0x431C
	uint32_t N0000425B; //0x4320
	uint32_t N000055C7; //0x4324
	uint32_t N0000425C; //0x4328
	uint32_t N000055C9; //0x432C
	uint32_t N0000425D; //0x4330
	uint32_t N000055CB; //0x4334
	uint32_t N0000425E; //0x4338
	uint32_t N000055CD; //0x433C
	uint32_t N0000425F; //0x4340
	uint32_t N000055CF; //0x4344
	uint32_t N00004260; //0x4348
	uint32_t N000055D1; //0x434C
	uint32_t N00004261; //0x4350
	uint32_t N000055D3; //0x4354
	uint32_t N00004262; //0x4358
	uint32_t N000055D5; //0x435C
	uint32_t N00004263; //0x4360
	uint32_t N000055D7; //0x4364
	uint32_t N00004264; //0x4368
	uint32_t N000055D9; //0x436C
	uint32_t N00004265; //0x4370
	uint32_t N000055DB; //0x4374
	uint32_t N00004266; //0x4378
	uint32_t N000055DD; //0x437C
	uint32_t N00004267; //0x4380
	uint32_t N000055DF; //0x4384
	uint32_t N00004268; //0x4388
	uint32_t N000055E1; //0x438C
	uint32_t N00004269; //0x4390
	uint32_t N000055E3; //0x4394
	uint32_t N0000426A; //0x4398
	uint32_t N000055E5; //0x439C
	uint32_t N0000426B; //0x43A0
	uint32_t N000055E7; //0x43A4
	uint32_t N0000426C; //0x43A8
	uint32_t N000055E9; //0x43AC
	uint32_t N0000426D; //0x43B0
	uint32_t N000055EB; //0x43B4
	uint32_t N0000426E; //0x43B8
	uint32_t N000055ED; //0x43BC
	uint32_t N0000426F; //0x43C0
	uint32_t N000055EF; //0x43C4
	uint32_t N00004270; //0x43C8
	uint32_t N000055F1; //0x43CC
	uint32_t N00004271; //0x43D0
	uint32_t N000055F3; //0x43D4
	uint32_t N00004272; //0x43D8
	uint32_t N000055F5; //0x43DC
	uint32_t N00004273; //0x43E0
	uint32_t N000055F7; //0x43E4
	uint32_t N00004274; //0x43E8
	uint32_t N000055F9; //0x43EC
	uint32_t N00004275; //0x43F0
	uint32_t N000055FB; //0x43F4
	uint32_t N00004276; //0x43F8
	uint32_t N000055FD; //0x43FC
	uint32_t N00004277; //0x4400
	uint32_t N000055FF; //0x4404
	uint32_t N00004278; //0x4408
	uint32_t N00005601; //0x440C
	uint32_t N00004279; //0x4410
	uint32_t N00005603; //0x4414
	uint32_t N0000427A; //0x4418
	uint32_t N00005605; //0x441C
	uint32_t N0000427B; //0x4420
	uint32_t N00005607; //0x4424
	uint32_t N0000427C; //0x4428
	uint32_t N00005609; //0x442C
	uint32_t N0000427D; //0x4430
	uint32_t N0000560B; //0x4434
	uint32_t N0000427E; //0x4438
	uint32_t N0000560D; //0x443C
	uint32_t N0000427F; //0x4440
	uint32_t N0000560F; //0x4444
	uint32_t N00004280; //0x4448
	uint32_t N00005611; //0x444C
	uint32_t N00004281; //0x4450
	uint32_t N00005613; //0x4454
	uint32_t N00004282; //0x4458
	uint32_t N00005615; //0x445C
	uint32_t N00004283; //0x4460
	uint32_t N00005617; //0x4464
	uint32_t N00004284; //0x4468
	uint32_t N00005619; //0x446C
	uint32_t N00004285; //0x4470
	uint32_t N0000561B; //0x4474
	uint32_t N00004286; //0x4478
	uint32_t N0000561D; //0x447C
	uint32_t N00004287; //0x4480
	uint32_t N0000561F; //0x4484
	uint32_t N00004288; //0x4488
	uint32_t N00005621; //0x448C
	uint32_t N00004289; //0x4490
	uint32_t N00005623; //0x4494
	uint32_t N0000428A; //0x4498
	uint32_t N00005625; //0x449C
	uint32_t N0000428B; //0x44A0
	uint32_t N00005627; //0x44A4
	uint32_t N0000428C; //0x44A8
	uint32_t N00005629; //0x44AC
	uint32_t N0000428D; //0x44B0
	uint32_t N0000562B; //0x44B4
	uint32_t N0000428E; //0x44B8
	uint32_t N0000562D; //0x44BC
	uint32_t N0000428F; //0x44C0
	uint32_t N0000562F; //0x44C4
	uint32_t N00004290; //0x44C8
	uint32_t N00005631; //0x44CC
	uint32_t N00004291; //0x44D0
	uint32_t N00005633; //0x44D4
	uint32_t N00004292; //0x44D8
	uint32_t N00005635; //0x44DC
	uint32_t N00004293; //0x44E0
	uint32_t N00005637; //0x44E4
	uint32_t N00004294; //0x44E8
	uint32_t N00005639; //0x44EC
	uint32_t N00004295; //0x44F0
	uint32_t N0000563B; //0x44F4
	uint32_t N00004296; //0x44F8
	uint32_t N0000563D; //0x44FC
	uint32_t N00004297; //0x4500
	uint32_t N0000563F; //0x4504
	uint32_t N00004298; //0x4508
	uint32_t N00005641; //0x450C
	uint32_t N00004299; //0x4510
	uint32_t N00005643; //0x4514
	uint32_t N0000429A; //0x4518
	uint32_t N00005645; //0x451C
	uint32_t N0000429B; //0x4520
	uint32_t N00005647; //0x4524
	uint32_t N0000429C; //0x4528
	uint32_t N00005649; //0x452C
	uint32_t N0000429D; //0x4530
	uint32_t N0000564B; //0x4534
	uint32_t N0000429E; //0x4538
	uint32_t N0000564D; //0x453C
	uint32_t N0000429F; //0x4540
	uint32_t N0000564F; //0x4544
	uint32_t N000042A0; //0x4548
	uint32_t N00005651; //0x454C
	uint32_t N000042A1; //0x4550
	uint32_t N00005653; //0x4554
	uint32_t N000042A2; //0x4558
	uint32_t N00005655; //0x455C
	uint32_t N000042A3; //0x4560
	uint32_t N00005657; //0x4564
	uint32_t N000042A4; //0x4568
	uint32_t N00005659; //0x456C
	uint32_t N000042A5; //0x4570
	uint32_t N0000565B; //0x4574
	uint32_t N000042A6; //0x4578
	uint32_t N0000565D; //0x457C
	uint32_t N000042A7; //0x4580
	uint32_t N0000565F; //0x4584
	uint32_t N000042A8; //0x4588
	uint32_t N00005661; //0x458C
	uint32_t N000042A9; //0x4590
	uint32_t N00005663; //0x4594
	uint32_t N000042AA; //0x4598
	uint32_t N00005665; //0x459C
	uint32_t N000042AB; //0x45A0
	uint32_t N00005667; //0x45A4
	uint32_t N000042AC; //0x45A8
	uint32_t N00005669; //0x45AC
	uint32_t N000042AD; //0x45B0
	uint32_t N0000566B; //0x45B4
	uint32_t N000042AE; //0x45B8
	uint32_t N0000566D; //0x45BC
	uint32_t N000042AF; //0x45C0
	uint32_t N0000566F; //0x45C4
	uint32_t N000042B0; //0x45C8
	uint32_t N00005671; //0x45CC
	uint32_t N000042B1; //0x45D0
	uint32_t N00005673; //0x45D4
	uint32_t N000042B2; //0x45D8
	uint32_t N00005675; //0x45DC
	uint32_t N000042B3; //0x45E0
	uint32_t N00005677; //0x45E4
	uint32_t N000042B4; //0x45E8
	uint32_t N00005679; //0x45EC
	uint32_t N000042B5; //0x45F0
	uint32_t N0000567B; //0x45F4
	uint32_t N000042B6; //0x45F8
	uint32_t N0000567D; //0x45FC
	uint32_t N000042B7; //0x4600
	uint32_t N0000567F; //0x4604
	uint32_t N000042B8; //0x4608
	uint32_t N00005681; //0x460C
	uint32_t N000042B9; //0x4610
	uint32_t N00005683; //0x4614
	uint32_t N000042BA; //0x4618
	uint32_t N00005685; //0x461C
	uint32_t N000042BB; //0x4620
	uint32_t N00005687; //0x4624
	uint32_t N000042BC; //0x4628
	uint32_t N00005689; //0x462C
	uint32_t N000042BD; //0x4630
	uint32_t N0000568B; //0x4634
	uint32_t N000042BE; //0x4638
	uint32_t N0000568D; //0x463C
	uint32_t N000042BF; //0x4640
	uint32_t N0000568F; //0x4644
	uint32_t N000042C0; //0x4648
	uint32_t N00005691; //0x464C
	uint32_t N000042C1; //0x4650
	uint32_t N00005693; //0x4654
	uint32_t N000042C2; //0x4658
	uint32_t N00005695; //0x465C
	uint32_t N000042C3; //0x4660
	uint32_t N00005697; //0x4664
	uint32_t N000042C4; //0x4668
	uint32_t N00005699; //0x466C
	uint32_t N000042C5; //0x4670
	uint32_t N0000569B; //0x4674
	uint32_t N000042C6; //0x4678
	uint32_t N0000569D; //0x467C
	uint32_t N000042C7; //0x4680
	uint32_t N0000569F; //0x4684
	uint32_t N000042C8; //0x4688
	uint32_t N000056A1; //0x468C
	uint32_t N000042C9; //0x4690
	uint32_t N000056A3; //0x4694
	uint32_t N000042CA; //0x4698
	uint32_t N000056A5; //0x469C
	uint32_t N000042CB; //0x46A0
	uint32_t N000056A7; //0x46A4
	uint32_t N000042CC; //0x46A8
	uint32_t N000056A9; //0x46AC
	uint32_t N000042CD; //0x46B0
	uint32_t N000056AB; //0x46B4
	uint32_t N000042CE; //0x46B8
	uint32_t N000056AD; //0x46BC
	uint32_t N000042CF; //0x46C0
	uint32_t N000056AF; //0x46C4
	uint32_t N000042D0; //0x46C8
	uint32_t N000056B1; //0x46CC
	uint32_t N000042D1; //0x46D0
	uint32_t N000056B3; //0x46D4
	uint32_t N000042D2; //0x46D8
	uint32_t N000056B5; //0x46DC
	uint32_t N000042D3; //0x46E0
	uint32_t N000056B7; //0x46E4
	uint32_t N000042D4; //0x46E8
	uint32_t N000056B9; //0x46EC
	uint32_t N000042D5; //0x46F0
	uint32_t N000056BB; //0x46F4
	uint32_t N000042D6; //0x46F8
	uint32_t N000056BD; //0x46FC
	uint32_t N000042D7; //0x4700
	uint32_t N000056BF; //0x4704
	uint32_t N000042D8; //0x4708
	uint32_t N000056C1; //0x470C
	uint32_t N000042D9; //0x4710
	uint32_t N000056C3; //0x4714
	uint32_t N000042DA; //0x4718
	uint32_t N000056C5; //0x471C
	uint32_t N000042DB; //0x4720
	uint32_t N000056C7; //0x4724
	uint32_t N000042DC; //0x4728
	uint32_t N000056C9; //0x472C
	uint32_t N000042DD; //0x4730
	uint32_t N000056CB; //0x4734
	uint32_t N000042DE; //0x4738
	uint32_t N000056CD; //0x473C
	uint32_t N000042DF; //0x4740
	uint32_t N000056CF; //0x4744
	uint32_t N000042E0; //0x4748
	uint32_t N000056D1; //0x474C
	uint32_t N000042E1; //0x4750
	uint32_t N000056D3; //0x4754
	uint32_t N000042E2; //0x4758
	uint32_t N000056D5; //0x475C
	uint32_t N000042E3; //0x4760
	uint32_t N000056D7; //0x4764
	uint32_t N000042E4; //0x4768
	uint32_t N000056D9; //0x476C
	uint32_t N000042E5; //0x4770
	uint32_t N000056DB; //0x4774
	uint32_t N000042E6; //0x4778
	uint32_t N000056DD; //0x477C
	uint32_t N000042E7; //0x4780
	uint32_t N000056DF; //0x4784
	uint32_t N000042E8; //0x4788
	uint32_t N000056E1; //0x478C
	uint32_t N000042E9; //0x4790
	uint32_t N000056E3; //0x4794
	uint32_t N000042EA; //0x4798
	uint32_t N000056E5; //0x479C
	uint32_t N000042EB; //0x47A0
	uint32_t N000056E7; //0x47A4
	uint32_t N000042EC; //0x47A8
	uint32_t N000056E9; //0x47AC
	uint32_t N000042ED; //0x47B0
	uint32_t N000056EB; //0x47B4
	uint32_t N000042EE; //0x47B8
	uint32_t N000056ED; //0x47BC
	uint32_t N000042EF; //0x47C0
	uint32_t N000056EF; //0x47C4
	uint32_t N000042F0; //0x47C8
	uint32_t N000056F1; //0x47CC
	uint32_t N000042F1; //0x47D0
	uint32_t N000056F3; //0x47D4
	uint32_t N000042F2; //0x47D8
	uint32_t N000056F5; //0x47DC
	uint32_t N000042F3; //0x47E0
	uint32_t N000056F7; //0x47E4
	uint32_t N000042F4; //0x47E8
	uint32_t N000056F9; //0x47EC
	uint32_t N000042F5; //0x47F0
	uint32_t N000056FB; //0x47F4
	uint32_t N000042F6; //0x47F8
	uint32_t N000056FD; //0x47FC
	uint32_t N000042F7; //0x4800
	uint32_t N000056FF; //0x4804
	uint32_t N000042F8; //0x4808
	uint32_t N00005701; //0x480C
	uint32_t N000042F9; //0x4810
	uint32_t N00005703; //0x4814
	uint32_t N000042FA; //0x4818
	uint32_t N00005705; //0x481C
	uint32_t N000042FB; //0x4820
	uint32_t N00005707; //0x4824
	uint32_t N000042FC; //0x4828
	uint32_t N00005709; //0x482C
	uint32_t N000042FD; //0x4830
	uint32_t N0000570B; //0x4834
	uint32_t N000042FE; //0x4838
	uint32_t N0000570D; //0x483C
	uint32_t N000042FF; //0x4840
	uint32_t N0000570F; //0x4844
	uint32_t N00004300; //0x4848
	uint32_t N00005711; //0x484C
	uint32_t N00004301; //0x4850
	uint32_t N00005713; //0x4854
	uint32_t N00004302; //0x4858
	uint32_t N00005715; //0x485C
	uint32_t N00004303; //0x4860
	uint32_t N00005717; //0x4864
	uint32_t N00004304; //0x4868
	uint32_t N00005719; //0x486C
	uint32_t N00004305; //0x4870
	uint32_t N0000571B; //0x4874
	uint32_t N00004306; //0x4878
	uint32_t N0000571D; //0x487C
	uint32_t N00004307; //0x4880
	uint32_t N0000571F; //0x4884
	uint32_t N00004308; //0x4888
	uint32_t N00005721; //0x488C
	uint32_t N00004309; //0x4890
	uint32_t N00005723; //0x4894
	uint32_t N0000430A; //0x4898
	uint32_t N00005725; //0x489C
	uint32_t N0000430B; //0x48A0
	uint32_t N00005727; //0x48A4
	uint32_t N0000430C; //0x48A8
	uint32_t N00005729; //0x48AC
	uint32_t N0000430D; //0x48B0
	uint32_t N0000572B; //0x48B4
	uint32_t N0000430E; //0x48B8
	uint32_t N0000572D; //0x48BC
	uint32_t N0000430F; //0x48C0
	uint32_t N0000572F; //0x48C4
	uint32_t N00004310; //0x48C8
	uint32_t N00005731; //0x48CC
	uint32_t N00004311; //0x48D0
	uint32_t N00005733; //0x48D4
	uint32_t N00004312; //0x48D8
	uint32_t N00005735; //0x48DC
	uint32_t N00004313; //0x48E0
	uint32_t N00005737; //0x48E4
	uint32_t N00004314; //0x48E8
	uint32_t N00005739; //0x48EC
	uint32_t N00004315; //0x48F0
	uint32_t N0000573B; //0x48F4
	uint32_t N00004316; //0x48F8
	uint32_t N0000573D; //0x48FC
	uint32_t N00004317; //0x4900
	uint32_t N0000573F; //0x4904
	uint32_t N00004318; //0x4908
	uint32_t N00005741; //0x490C
	uint32_t N00004319; //0x4910
	uint32_t N00005743; //0x4914
	uint32_t N0000431A; //0x4918
	uint32_t N00005745; //0x491C
	uint32_t N0000431B; //0x4920
	uint32_t N00005747; //0x4924
	uint32_t N0000431C; //0x4928
	uint32_t N00005749; //0x492C
	uint32_t N0000431D; //0x4930
	uint32_t N0000574B; //0x4934
	uint32_t N0000431E; //0x4938
	uint32_t N0000574D; //0x493C
	uint32_t N0000431F; //0x4940
	uint32_t N0000574F; //0x4944
	uint32_t N00004320; //0x4948
	uint32_t N00005751; //0x494C
	uint32_t N00004321; //0x4950
	uint32_t N00005753; //0x4954
	uint32_t N00004322; //0x4958
	uint32_t N00005755; //0x495C
	uint32_t N00004323; //0x4960
	uint32_t N00005757; //0x4964
	uint32_t N00004324; //0x4968
	uint32_t N00005759; //0x496C
	uint32_t N00004325; //0x4970
	uint32_t N0000575B; //0x4974
	uint32_t N00004326; //0x4978
	uint32_t N0000575D; //0x497C
	uint32_t N00004327; //0x4980
	uint32_t N0000575F; //0x4984
	uint32_t N00004328; //0x4988
	uint32_t N00005761; //0x498C
	uint32_t N00004329; //0x4990
	uint32_t N00005763; //0x4994
	uint32_t N0000432A; //0x4998
	uint32_t N00005765; //0x499C
	uint32_t N0000432B; //0x49A0
	uint32_t N00005767; //0x49A4
	uint32_t N0000432C; //0x49A8
	uint32_t N00005769; //0x49AC
	uint32_t N0000432D; //0x49B0
	uint32_t N0000576B; //0x49B4
	uint32_t N0000432E; //0x49B8
	uint32_t N0000576D; //0x49BC
	uint32_t N0000432F; //0x49C0
	uint32_t N0000576F; //0x49C4
	uint32_t N00004330; //0x49C8
	uint32_t N00005771; //0x49CC
	uint32_t N00004331; //0x49D0
	uint32_t N00005773; //0x49D4
	uint32_t N00004332; //0x49D8
	uint32_t N00005775; //0x49DC
	uint32_t N00004333; //0x49E0
	uint32_t N00005777; //0x49E4
	uint32_t N00004334; //0x49E8
	uint32_t N00005779; //0x49EC
	uint32_t N00004335; //0x49F0
	uint32_t N0000577B; //0x49F4
	uint32_t N00004336; //0x49F8
	uint32_t N0000577D; //0x49FC
	uint32_t N00004337; //0x4A00
	uint32_t N0000577F; //0x4A04
	uint32_t N00004338; //0x4A08
	uint32_t N00005781; //0x4A0C
	uint32_t N00004339; //0x4A10
	uint32_t N00005783; //0x4A14
	uint32_t N0000433A; //0x4A18
	uint32_t N00005785; //0x4A1C
	uint32_t N0000433B; //0x4A20
	uint32_t N00005787; //0x4A24
	uint32_t N0000433C; //0x4A28
	uint32_t N00005789; //0x4A2C
	uint32_t N0000433D; //0x4A30
	uint32_t N0000578B; //0x4A34
	uint32_t N0000433E; //0x4A38
	uint32_t N0000578D; //0x4A3C
	uint32_t N0000433F; //0x4A40
	uint32_t N0000578F; //0x4A44
	uint32_t N00004340; //0x4A48
	uint32_t N00005791; //0x4A4C
	uint32_t N00004341; //0x4A50
	uint32_t N00005793; //0x4A54
	uint32_t N00004342; //0x4A58
	uint32_t N00005795; //0x4A5C
	uint32_t N00004343; //0x4A60
	uint32_t N00005797; //0x4A64
	uint32_t N00004344; //0x4A68
	uint32_t N00005799; //0x4A6C
	uint32_t N00004345; //0x4A70
	uint32_t N0000579B; //0x4A74
	uint32_t N00004346; //0x4A78
	uint32_t N0000579D; //0x4A7C
	uint32_t N00004347; //0x4A80
	uint32_t N0000579F; //0x4A84
	uint32_t N00004348; //0x4A88
	uint32_t N000057A1; //0x4A8C
	uint32_t N00004349; //0x4A90
	uint32_t N000057A3; //0x4A94
	uint32_t N0000434A; //0x4A98
	uint32_t N000057A5; //0x4A9C
	uint32_t N0000434B; //0x4AA0
	uint32_t N000057A7; //0x4AA4
	uint32_t N0000434C; //0x4AA8
	uint32_t N000057A9; //0x4AAC
	uint32_t N0000434D; //0x4AB0
	uint32_t N000057AB; //0x4AB4
	uint32_t N0000434E; //0x4AB8
	uint32_t N000057AD; //0x4ABC
	uint32_t N0000434F; //0x4AC0
	uint32_t N000057AF; //0x4AC4
	uint32_t N00004350; //0x4AC8
	uint32_t N000057B1; //0x4ACC
	uint32_t N00004351; //0x4AD0
	uint32_t N000057B3; //0x4AD4
	uint32_t N00004352; //0x4AD8
	uint32_t N000057B5; //0x4ADC
	uint32_t N00004353; //0x4AE0
	uint32_t N000057B7; //0x4AE4
	uint32_t N00004354; //0x4AE8
	uint32_t N000057B9; //0x4AEC
	uint32_t N00004355; //0x4AF0
	uint32_t N000057BB; //0x4AF4
	uint32_t N00004356; //0x4AF8
	uint32_t N000057BD; //0x4AFC
	uint32_t N00004357; //0x4B00
	uint32_t N000057BF; //0x4B04
	uint32_t N00004358; //0x4B08
	uint32_t N000057C1; //0x4B0C
	uint32_t N00004359; //0x4B10
	uint32_t N000057C3; //0x4B14
	uint32_t N0000435A; //0x4B18
	uint32_t N000057C5; //0x4B1C
	uint32_t N0000435B; //0x4B20
	uint32_t N000057C7; //0x4B24
	uint32_t N0000435C; //0x4B28
	uint32_t N000057C9; //0x4B2C
	uint32_t N0000435D; //0x4B30
	uint32_t N000057CB; //0x4B34
	uint32_t N0000435E; //0x4B38
	uint32_t N000057CD; //0x4B3C
	uint32_t N0000435F; //0x4B40
	uint32_t N000057CF; //0x4B44
	uint32_t N00004360; //0x4B48
	uint32_t N000057D1; //0x4B4C
	uint32_t N00004361; //0x4B50
	uint32_t N000057D3; //0x4B54
	uint32_t N00004362; //0x4B58
	uint32_t N000057D5; //0x4B5C
	uint32_t N00004363; //0x4B60
	uint32_t N000057D7; //0x4B64
	uint32_t N00004364; //0x4B68
	uint32_t N000057D9; //0x4B6C
	uint32_t N00004365; //0x4B70
	uint32_t N000057DB; //0x4B74
	uint32_t N00004366; //0x4B78
	uint32_t N000057DD; //0x4B7C
	uint32_t N00004367; //0x4B80
	uint32_t N000057DF; //0x4B84
	uint32_t N00004368; //0x4B88
	uint32_t N000057E1; //0x4B8C
	uint32_t N00004369; //0x4B90
	uint32_t N000057E3; //0x4B94
	uint32_t N0000436A; //0x4B98
	uint32_t N000057E5; //0x4B9C
	uint32_t N0000436B; //0x4BA0
	uint32_t N000057E7; //0x4BA4
	uint32_t N0000436C; //0x4BA8
	uint32_t N000057E9; //0x4BAC
	uint32_t N0000436D; //0x4BB0
	uint32_t N000057EB; //0x4BB4
	uint32_t N0000436E; //0x4BB8
	uint32_t N000057ED; //0x4BBC
	uint32_t N0000436F; //0x4BC0
	uint32_t N000057EF; //0x4BC4
	uint32_t N00004370; //0x4BC8
	uint32_t N000057F1; //0x4BCC
	uint32_t N00004371; //0x4BD0
	uint32_t N000057F3; //0x4BD4
	uint32_t N00004372; //0x4BD8
	uint32_t N000057F5; //0x4BDC
	uint32_t N00004373; //0x4BE0
	uint32_t N000057F7; //0x4BE4
	uint32_t N00004374; //0x4BE8
	uint32_t N000057F9; //0x4BEC
	uint32_t N00004375; //0x4BF0
	uint32_t N000057FB; //0x4BF4
	uint32_t N00004376; //0x4BF8
	uint32_t N000057FD; //0x4BFC
	uint32_t N00004377; //0x4C00
	uint32_t N000057FF; //0x4C04
	uint32_t N00004378; //0x4C08
	uint32_t N00005801; //0x4C0C
	uint32_t N00004379; //0x4C10
	uint32_t N00005803; //0x4C14
	uint32_t N0000437A; //0x4C18
	uint32_t N00005805; //0x4C1C
	uint32_t N0000437B; //0x4C20
	uint32_t N00005807; //0x4C24
	uint32_t N0000437C; //0x4C28
	uint32_t N00005809; //0x4C2C
	uint32_t N0000437D; //0x4C30
	uint32_t N0000580B; //0x4C34
	uint32_t N0000437E; //0x4C38
	uint32_t N0000580D; //0x4C3C
	uint32_t N0000437F; //0x4C40
	uint32_t N0000580F; //0x4C44
	uint32_t N00004380; //0x4C48
	uint32_t N00005811; //0x4C4C
	uint32_t N00004381; //0x4C50
	uint32_t N00005813; //0x4C54
	uint32_t N00004382; //0x4C58
	uint32_t N00005815; //0x4C5C
	uint32_t N00004383; //0x4C60
	uint32_t N00005817; //0x4C64
	uint32_t N00004384; //0x4C68
	uint32_t N00005819; //0x4C6C
	uint32_t N00004385; //0x4C70
	uint32_t N0000581B; //0x4C74
	uint32_t N00004386; //0x4C78
	uint32_t N0000581D; //0x4C7C
	uint32_t N00004387; //0x4C80
	uint32_t N0000581F; //0x4C84
	uint32_t N00004388; //0x4C88
	uint32_t N00005821; //0x4C8C
	uint32_t N00004389; //0x4C90
	uint32_t N00005823; //0x4C94
	uint32_t N0000438A; //0x4C98
	uint32_t N00005825; //0x4C9C
	uint32_t N0000438B; //0x4CA0
	uint32_t N00005827; //0x4CA4
	uint32_t N0000438C; //0x4CA8
	uint32_t N00005829; //0x4CAC
	uint32_t N0000438D; //0x4CB0
	uint32_t N0000582B; //0x4CB4
	uint32_t N0000438E; //0x4CB8
	uint32_t N0000582D; //0x4CBC
	uint32_t N0000438F; //0x4CC0
	uint32_t N0000582F; //0x4CC4
	uint32_t N00004390; //0x4CC8
	uint32_t N00005831; //0x4CCC
	uint32_t N00004391; //0x4CD0
	uint32_t N00005833; //0x4CD4
	uint32_t N00004392; //0x4CD8
	uint32_t N00005835; //0x4CDC
	uint32_t N00004393; //0x4CE0
	uint32_t N00005837; //0x4CE4
	uint32_t N00004394; //0x4CE8
	uint32_t N00005839; //0x4CEC
	uint32_t N00004395; //0x4CF0
	uint32_t N0000583B; //0x4CF4
	uint32_t N00004396; //0x4CF8
	uint32_t N0000583D; //0x4CFC
	uint32_t N00004397; //0x4D00
	uint32_t N0000583F; //0x4D04
	uint32_t N00004398; //0x4D08
	uint32_t N00005841; //0x4D0C
	uint32_t N00004399; //0x4D10
	uint32_t N00005843; //0x4D14
	uint32_t N0000439A; //0x4D18
	uint32_t N00005845; //0x4D1C
	uint32_t N0000439B; //0x4D20
	uint32_t N00005847; //0x4D24
	uint32_t N0000439C; //0x4D28
	uint32_t N00005849; //0x4D2C
	uint32_t N0000439D; //0x4D30
	uint32_t N0000584B; //0x4D34
	uint32_t N0000439E; //0x4D38
	uint32_t N0000584D; //0x4D3C
	uint32_t N0000439F; //0x4D40
	uint32_t N0000584F; //0x4D44
	uint32_t N000043A0; //0x4D48
	uint32_t N00005851; //0x4D4C
	uint32_t N000043A1; //0x4D50
	uint32_t N00005853; //0x4D54
	uint32_t N000043A2; //0x4D58
	uint32_t N00005855; //0x4D5C
	uint32_t N000043A3; //0x4D60
	uint32_t N00005857; //0x4D64
	uint32_t N000043A4; //0x4D68
	uint32_t N00005859; //0x4D6C
	uint32_t N000043A5; //0x4D70
	uint32_t N0000585B; //0x4D74
	uint32_t N000043A6; //0x4D78
	uint32_t N0000585D; //0x4D7C
	uint32_t N000043A7; //0x4D80
	uint32_t N0000585F; //0x4D84
	uint32_t N000043A8; //0x4D88
	uint32_t N00005861; //0x4D8C
	uint32_t N000043A9; //0x4D90
	uint32_t N00005863; //0x4D94
	uint32_t N000043AA; //0x4D98
	uint32_t N00005865; //0x4D9C
	uint32_t N000043AB; //0x4DA0
	uint32_t N00005867; //0x4DA4
	uint32_t N000043AC; //0x4DA8
	uint32_t N00005869; //0x4DAC
	uint32_t N000043AD; //0x4DB0
	uint32_t N0000586B; //0x4DB4
	uint32_t N000043AE; //0x4DB8
	uint32_t N0000586D; //0x4DBC
	uint32_t N000043AF; //0x4DC0
	uint32_t N0000586F; //0x4DC4
	uint32_t N000043B0; //0x4DC8
	uint32_t N00005871; //0x4DCC
	uint32_t N000043B1; //0x4DD0
	uint32_t N00005873; //0x4DD4
	uint32_t N000043B2; //0x4DD8
	uint32_t N00005875; //0x4DDC
	uint32_t N000043B3; //0x4DE0
	uint32_t N00005877; //0x4DE4
	uint32_t N000043B4; //0x4DE8
	uint32_t N00005879; //0x4DEC
	uint32_t N000043B5; //0x4DF0
	uint32_t N0000587B; //0x4DF4
	uint32_t N000043B6; //0x4DF8
	uint32_t N0000587D; //0x4DFC
	uint32_t N000043B7; //0x4E00
	uint32_t N0000587F; //0x4E04
	uint32_t N000043B8; //0x4E08
	uint32_t N00005881; //0x4E0C
	uint32_t N000043B9; //0x4E10
	uint32_t N00005883; //0x4E14
	uint32_t N000043BA; //0x4E18
	uint32_t N00005885; //0x4E1C
	uint32_t N000043BB; //0x4E20
	uint32_t N00005887; //0x4E24
	uint32_t N000043BC; //0x4E28
	uint32_t N00005889; //0x4E2C
	uint32_t N000043BD; //0x4E30
	uint32_t N0000588B; //0x4E34
	uint32_t N000043BE; //0x4E38
	uint32_t N0000588D; //0x4E3C
	uint32_t N000043BF; //0x4E40
	uint32_t N0000588F; //0x4E44
	uint32_t N000043C0; //0x4E48
	uint32_t N00005891; //0x4E4C
	uint32_t N000043C1; //0x4E50
	uint32_t N00005893; //0x4E54
	uint32_t N000043C2; //0x4E58
	uint32_t N00005895; //0x4E5C
	uint32_t N000043C3; //0x4E60
	uint32_t N00005897; //0x4E64
	uint32_t N000043C4; //0x4E68
	uint32_t N00005899; //0x4E6C
	uint32_t N000043C5; //0x4E70
	uint32_t N0000589B; //0x4E74
	uint32_t N000043C6; //0x4E78
	uint32_t N0000589D; //0x4E7C
	uint32_t N000043C7; //0x4E80
	uint32_t N0000589F; //0x4E84
	uint32_t N000043C8; //0x4E88
	uint32_t N000058A1; //0x4E8C
	uint32_t N000043C9; //0x4E90
	uint32_t N000058A3; //0x4E94
	uint32_t N000043CA; //0x4E98
	uint32_t N000058A5; //0x4E9C
	uint32_t N000043CB; //0x4EA0
	uint32_t N000058A7; //0x4EA4
	uint32_t N000043CC; //0x4EA8
	uint32_t N000058A9; //0x4EAC
	uint32_t N000043CD; //0x4EB0
	uint32_t N000058AB; //0x4EB4
	uint32_t N000043CE; //0x4EB8
	uint32_t N000058AD; //0x4EBC
	uint32_t N000043CF; //0x4EC0
	uint32_t N000058AF; //0x4EC4
	uint32_t N000043D0; //0x4EC8
	uint32_t N000058B1; //0x4ECC
	uint32_t N000043D1; //0x4ED0
	uint32_t N000058B3; //0x4ED4
	uint32_t N000043D2; //0x4ED8
	uint32_t N000058B5; //0x4EDC
	uint32_t N000043D3; //0x4EE0
	uint32_t N000058B7; //0x4EE4
	uint32_t N000043D4; //0x4EE8
	uint32_t N000058B9; //0x4EEC
	uint32_t N000043D5; //0x4EF0
	uint32_t N000058BB; //0x4EF4
	uint32_t N000043D6; //0x4EF8
	uint32_t N000058BD; //0x4EFC
	uint32_t N000043D7; //0x4F00
	uint32_t N000058BF; //0x4F04
	uint32_t N000043D8; //0x4F08
	uint32_t N000058C1; //0x4F0C
	uint32_t N000043D9; //0x4F10
	uint32_t N000058C3; //0x4F14
	uint32_t N000043DA; //0x4F18
	uint32_t N000058C5; //0x4F1C
	uint32_t N000043DB; //0x4F20
	uint32_t N000058C7; //0x4F24
	uint32_t N000043DC; //0x4F28
	uint32_t N000058C9; //0x4F2C
	uint32_t N000043DD; //0x4F30
	uint32_t N000058CB; //0x4F34
	uint32_t N000043DE; //0x4F38
	uint32_t N000058CD; //0x4F3C
	uint32_t N000043DF; //0x4F40
	uint32_t N000058CF; //0x4F44
	uint32_t N000043E0; //0x4F48
	uint32_t N000058D1; //0x4F4C
	uint32_t N000043E1; //0x4F50
	uint32_t N000058D3; //0x4F54
	uint32_t N000043E2; //0x4F58
	uint32_t N000058D5; //0x4F5C
	uint32_t N000043E3; //0x4F60
	uint32_t N000058D7; //0x4F64
	uint32_t N000043E4; //0x4F68
	uint32_t N000058D9; //0x4F6C
	uint32_t N000043E5; //0x4F70
	uint32_t N000058DB; //0x4F74
	uint32_t N000043E6; //0x4F78
	uint32_t N000058DD; //0x4F7C
	uint32_t N000043E7; //0x4F80
	uint32_t N000058DF; //0x4F84
	uint32_t N000043E8; //0x4F88
	uint32_t N000058E1; //0x4F8C
	uint32_t N000043E9; //0x4F90
	uint32_t N000058E3; //0x4F94
	uint32_t N000043EA; //0x4F98
	uint32_t N000058E5; //0x4F9C
	uint32_t N000043EB; //0x4FA0
	uint32_t N000058E7; //0x4FA4
	uint32_t N000043EC; //0x4FA8
	uint32_t N000058E9; //0x4FAC
	uint32_t N000043ED; //0x4FB0
	uint32_t N000058EB; //0x4FB4
	uint32_t N000043EE; //0x4FB8
	uint32_t N000058ED; //0x4FBC
	uint32_t N000043EF; //0x4FC0
	uint32_t N000058EF; //0x4FC4
	uint32_t N000043F0; //0x4FC8
	uint32_t N000058F1; //0x4FCC
	uint32_t N000043F1; //0x4FD0
	uint32_t N000058F3; //0x4FD4
	uint32_t N000043F2; //0x4FD8
	uint32_t N000058F5; //0x4FDC
	uint32_t N000043F3; //0x4FE0
	uint32_t N000058F7; //0x4FE4
	uint32_t N000043F4; //0x4FE8
	uint32_t N000058F9; //0x4FEC
	uint32_t N000043F5; //0x4FF0
	uint32_t N000058FB; //0x4FF4
	uint32_t N000043F6; //0x4FF8
	uint32_t N000058FD; //0x4FFC
	uint32_t N000043F7; //0x5000
	uint32_t N000058FF; //0x5004
	uint32_t N000043F8; //0x5008
	uint32_t N00005901; //0x500C
	uint32_t N000043F9; //0x5010
	uint32_t N00005903; //0x5014
	uint32_t N000043FA; //0x5018
	uint32_t N00005905; //0x501C
	uint32_t N000043FB; //0x5020
	uint32_t N00005907; //0x5024
	uint32_t N000043FC; //0x5028
	uint32_t N00005909; //0x502C
	uint32_t N000043FD; //0x5030
	uint32_t N0000590B; //0x5034
	uint32_t N000043FE; //0x5038
	uint32_t N0000590D; //0x503C
	uint32_t N000043FF; //0x5040
	uint32_t N0000590F; //0x5044
	uint32_t N00004400; //0x5048
	uint32_t N00005911; //0x504C
	uint32_t N00004401; //0x5050
	uint32_t N00005913; //0x5054
	uint32_t N00004402; //0x5058
	uint32_t N00005915; //0x505C
	uint32_t N00004403; //0x5060
	uint32_t N00005917; //0x5064
	uint32_t N00004404; //0x5068
	uint32_t N00005919; //0x506C
	uint32_t N00004405; //0x5070
	uint32_t N0000591B; //0x5074
	uint32_t N00004406; //0x5078
	uint32_t N0000591D; //0x507C
	uint32_t N00004407; //0x5080
	uint32_t N0000591F; //0x5084
	uint32_t N00004408; //0x5088
	uint32_t N00005921; //0x508C
	uint32_t N00004409; //0x5090
	uint32_t N00005923; //0x5094
	uint32_t N0000440A; //0x5098
	uint32_t N00005925; //0x509C
	uint32_t N0000440B; //0x50A0
	uint32_t N00005927; //0x50A4
	uint32_t N0000440C; //0x50A8
	uint32_t N00005929; //0x50AC
	uint32_t N0000440D; //0x50B0
	uint32_t N0000592B; //0x50B4
	uint32_t N0000440E; //0x50B8
	uint32_t N0000592D; //0x50BC
	uint32_t N0000440F; //0x50C0
	uint32_t N0000592F; //0x50C4
	uint32_t N00004410; //0x50C8
	uint32_t N00005931; //0x50CC
	uint32_t N00004411; //0x50D0
	uint32_t N00005933; //0x50D4
	uint32_t N00004412; //0x50D8
	uint32_t N00005935; //0x50DC
	uint32_t N00004413; //0x50E0
	uint32_t N00005937; //0x50E4
	uint32_t N00004414; //0x50E8
	uint32_t N00005939; //0x50EC
	uint32_t N00004415; //0x50F0
	uint32_t N0000593B; //0x50F4
	uint32_t N00004416; //0x50F8
	uint32_t N0000593D; //0x50FC
	uint32_t N00004417; //0x5100
	uint32_t N0000593F; //0x5104
	uint32_t N00004418; //0x5108
	uint32_t N00005941; //0x510C
	uint32_t N00004419; //0x5110
	uint32_t N00005943; //0x5114
	uint32_t N0000441A; //0x5118
	uint32_t N00005945; //0x511C
	uint32_t N0000441B; //0x5120
	uint32_t N00005947; //0x5124
	uint32_t N0000441C; //0x5128
	uint32_t N00005949; //0x512C
	uint32_t N0000441D; //0x5130
	uint32_t N0000594B; //0x5134
	uint32_t N0000441E; //0x5138
	uint32_t N0000594D; //0x513C
	uint32_t N0000441F; //0x5140
	uint32_t N0000594F; //0x5144
	uint32_t N00004420; //0x5148
	uint32_t N00005951; //0x514C
	uint32_t N00004421; //0x5150
	uint32_t N00005953; //0x5154
	uint32_t N00004422; //0x5158
	uint32_t N00005955; //0x515C
	uint32_t N00004423; //0x5160
	uint32_t N00005957; //0x5164
	uint32_t N00004424; //0x5168
	uint32_t N00005959; //0x516C
	uint32_t N00004425; //0x5170
	uint32_t N0000595B; //0x5174
	uint32_t N00004426; //0x5178
	uint32_t N0000595D; //0x517C
	uint32_t N00004427; //0x5180
	uint32_t N0000595F; //0x5184
	uint32_t N00004428; //0x5188
	uint32_t N00005961; //0x518C
	uint32_t N00004429; //0x5190
	uint32_t N00005963; //0x5194
	uint32_t N0000442A; //0x5198
	uint32_t N00005965; //0x519C
	uint32_t N0000442B; //0x51A0
	uint32_t N00005967; //0x51A4
	uint32_t N0000442C; //0x51A8
	uint32_t N00005969; //0x51AC
	uint32_t N0000442D; //0x51B0
	uint32_t N0000596B; //0x51B4
	uint32_t N0000442E; //0x51B8
	uint32_t N0000596D; //0x51BC
	uint32_t N0000442F; //0x51C0
	uint32_t N0000596F; //0x51C4
	uint32_t N00004430; //0x51C8
	uint32_t N00005971; //0x51CC
	uint32_t N00004431; //0x51D0
	uint32_t N00005973; //0x51D4
	uint32_t N00004432; //0x51D8
	uint32_t N00005975; //0x51DC
	uint32_t N00004433; //0x51E0
	uint32_t N00005977; //0x51E4
	uint32_t N00004434; //0x51E8
	uint32_t N00005979; //0x51EC
	uint32_t N00004435; //0x51F0
	uint32_t N0000597B; //0x51F4
	uint32_t N00004436; //0x51F8
	uint32_t N0000597D; //0x51FC
	uint32_t N00004437; //0x5200
	uint32_t N0000597F; //0x5204
	uint32_t N00004438; //0x5208
	uint32_t N00005981; //0x520C
	uint32_t N00004439; //0x5210
	uint32_t N00005983; //0x5214
	uint32_t N0000443A; //0x5218
	uint32_t N00005985; //0x521C
	uint32_t N0000443B; //0x5220
	uint32_t N00005987; //0x5224
	uint32_t N0000443C; //0x5228
	uint32_t N00005989; //0x522C
	uint32_t N0000443D; //0x5230
	uint32_t N0000598B; //0x5234
	uint32_t N0000443E; //0x5238
	uint32_t N0000598D; //0x523C
	uint32_t N0000443F; //0x5240
	uint32_t N0000598F; //0x5244
	uint32_t N00004440; //0x5248
	uint32_t N00005991; //0x524C
	uint32_t N00004441; //0x5250
	uint32_t N00005993; //0x5254
	uint32_t N00004442; //0x5258
	uint32_t N00005995; //0x525C
	uint32_t N00004443; //0x5260
	uint32_t N00005997; //0x5264
	uint32_t N00004444; //0x5268
	uint32_t N00005999; //0x526C
	uint32_t N00004445; //0x5270
	uint32_t N0000599B; //0x5274
	uint32_t N00004446; //0x5278
	uint32_t N0000599D; //0x527C
	uint32_t N00004447; //0x5280
	uint32_t N0000599F; //0x5284
	uint32_t N00004448; //0x5288
	uint32_t N000059A1; //0x528C
	uint32_t N00004449; //0x5290
	uint32_t N000059A3; //0x5294
	uint32_t N0000444A; //0x5298
	uint32_t N000059A5; //0x529C
	uint32_t N0000444B; //0x52A0
	uint32_t N000059A7; //0x52A4
	uint32_t N0000444C; //0x52A8
	uint32_t N000059A9; //0x52AC
	uint32_t N0000444D; //0x52B0
	uint32_t N000059AB; //0x52B4
	uint32_t N0000444E; //0x52B8
	uint32_t N000059AD; //0x52BC
	uint32_t N0000444F; //0x52C0
	uint32_t N000059AF; //0x52C4
	uint32_t N00004450; //0x52C8
	uint32_t N000059B1; //0x52CC
	uint32_t N00004451; //0x52D0
	uint32_t N000059B3; //0x52D4
	uint32_t N00004452; //0x52D8
	uint32_t N000059B5; //0x52DC
	uint32_t N00004453; //0x52E0
	uint32_t N000059B7; //0x52E4
	uint32_t N00004454; //0x52E8
	uint32_t N000059B9; //0x52EC
	uint32_t N00004455; //0x52F0
	uint32_t N000059BB; //0x52F4
	uint32_t N00004456; //0x52F8
	uint32_t N000059BD; //0x52FC
	uint32_t N00004457; //0x5300
	uint32_t N000059BF; //0x5304
	uint32_t N00004458; //0x5308
	uint32_t N000059C1; //0x530C
	uint32_t N00004459; //0x5310
	uint32_t N000059C3; //0x5314
	uint32_t N0000445A; //0x5318
	uint32_t N000059C5; //0x531C
	uint32_t N0000445B; //0x5320
	uint32_t N000059C7; //0x5324
	uint32_t N0000445C; //0x5328
	uint32_t N000059C9; //0x532C
	uint32_t N0000445D; //0x5330
	uint32_t N000059CB; //0x5334
	uint32_t N0000445E; //0x5338
	uint32_t N000059CD; //0x533C
	uint32_t N0000445F; //0x5340
	uint32_t N000059CF; //0x5344
	uint32_t N00004460; //0x5348
	uint32_t N000059D1; //0x534C
	uint32_t N00004461; //0x5350
	uint32_t N000059D3; //0x5354
	uint32_t N00004462; //0x5358
	uint32_t N000059D5; //0x535C
	uint32_t N00004463; //0x5360
	uint32_t N000059D7; //0x5364
	uint32_t N00004464; //0x5368
	uint32_t N000059D9; //0x536C
	uint32_t N00004465; //0x5370
	uint32_t N000059DB; //0x5374
	uint32_t N00004466; //0x5378
	uint32_t N000059DD; //0x537C
	uint32_t N00004467; //0x5380
	uint32_t N000059DF; //0x5384
	uint32_t N00004468; //0x5388
	uint32_t N000059E1; //0x538C
	uint32_t N00004469; //0x5390
	uint32_t N000059E3; //0x5394
	uint32_t N0000446A; //0x5398
	uint32_t N000059E5; //0x539C
	uint32_t N0000446B; //0x53A0
	uint32_t N000059E7; //0x53A4
	uint32_t N0000446C; //0x53A8
	uint32_t N000059E9; //0x53AC
	uint32_t N0000446D; //0x53B0
	uint32_t N000059EB; //0x53B4
	uint32_t N0000446E; //0x53B8
	uint32_t N000059ED; //0x53BC
	uint32_t N0000446F; //0x53C0
	uint32_t N000059EF; //0x53C4
	uint32_t N00004470; //0x53C8
	uint32_t N000059F1; //0x53CC
	uint32_t N00004471; //0x53D0
	uint32_t N000059F3; //0x53D4
	uint32_t N00004472; //0x53D8
	uint32_t N000059F5; //0x53DC
	uint32_t N00004473; //0x53E0
	uint32_t N000059F7; //0x53E4
	uint32_t N00004474; //0x53E8
	uint32_t N000059F9; //0x53EC
	uint32_t N00004475; //0x53F0
	uint32_t N000059FB; //0x53F4
	uint32_t N00004476; //0x53F8
	uint32_t N000059FD; //0x53FC
	uint32_t N00004477; //0x5400
	uint32_t N000059FF; //0x5404
	uint32_t N00004478; //0x5408
	uint32_t N00005A01; //0x540C
	uint32_t N00004479; //0x5410
	uint32_t N00005A03; //0x5414
	uint32_t N0000447A; //0x5418
	uint32_t N00005A05; //0x541C
	uint32_t N0000447B; //0x5420
	uint32_t N00005A07; //0x5424
	uint32_t N0000447C; //0x5428
	uint32_t N00005A09; //0x542C
	uint32_t N0000447D; //0x5430
	uint32_t N00005A0B; //0x5434
	uint32_t N0000447E; //0x5438
	uint32_t N00005A0D; //0x543C
	uint32_t N0000447F; //0x5440
	uint32_t N00005A0F; //0x5444
	uint32_t N00004480; //0x5448
	uint32_t N00005A11; //0x544C
	uint32_t N00004481; //0x5450
	uint32_t N00005A13; //0x5454
	uint32_t N00004482; //0x5458
	uint32_t N00005A15; //0x545C
	uint32_t N00004483; //0x5460
	uint32_t N00005A17; //0x5464
	uint32_t N00004484; //0x5468
	uint32_t N00005A19; //0x546C
	uint32_t N00004485; //0x5470
	uint32_t N00005A1B; //0x5474
	uint32_t N00004486; //0x5478
	uint32_t N00005A1D; //0x547C
	uint32_t N00004487; //0x5480
	uint32_t N00005A1F; //0x5484
	uint32_t N00004488; //0x5488
	uint32_t N00005A21; //0x548C
	uint32_t N00004489; //0x5490
	uint32_t N00005A23; //0x5494
	uint32_t N0000448A; //0x5498
	uint32_t N00005A25; //0x549C
	uint32_t N0000448B; //0x54A0
	uint32_t N00005A27; //0x54A4
	uint32_t N0000448C; //0x54A8
	uint32_t N00005A29; //0x54AC
	uint32_t N0000448D; //0x54B0
	uint32_t N00005A2B; //0x54B4
	uint32_t N0000448E; //0x54B8
	uint32_t N00005A2D; //0x54BC
	uint32_t N0000448F; //0x54C0
	uint32_t N00005A2F; //0x54C4
	uint32_t N00004490; //0x54C8
	uint32_t N00005A31; //0x54CC
	uint32_t N00004491; //0x54D0
	uint32_t N00005A33; //0x54D4
	uint32_t N00004492; //0x54D8
	uint32_t N00005A35; //0x54DC
	uint32_t N00004493; //0x54E0
	uint32_t N00005A37; //0x54E4
	uint32_t N00004494; //0x54E8
	uint32_t N00005A39; //0x54EC
	uint32_t N00004495; //0x54F0
	uint32_t N00005A3B; //0x54F4
	uint32_t N00004496; //0x54F8
	uint32_t N00005A3D; //0x54FC
	uint32_t N00004497; //0x5500
	uint32_t N00005A3F; //0x5504
	uint32_t N00004498; //0x5508
	uint32_t N00005A41; //0x550C
	uint32_t N00004499; //0x5510
	uint32_t N00005A43; //0x5514
	uint32_t N0000449A; //0x5518
	uint32_t N00005A45; //0x551C
	uint32_t N0000449B; //0x5520
	uint32_t N00005A47; //0x5524
	uint32_t N0000449C; //0x5528
	uint32_t N00005A49; //0x552C
	uint32_t N0000449D; //0x5530
	uint32_t N00005A4B; //0x5534
	uint32_t N0000449E; //0x5538
	uint32_t N00005A4D; //0x553C
	uint32_t N0000449F; //0x5540
	uint32_t N00005A4F; //0x5544
	uint32_t N000044A0; //0x5548
	uint32_t N00005A51; //0x554C
	uint32_t N000044A1; //0x5550
	uint32_t N00005A53; //0x5554
	uint32_t N000044A2; //0x5558
	uint32_t N00005A55; //0x555C
	uint32_t N000044A3; //0x5560
	uint32_t N00005A57; //0x5564
	uint32_t N000044A4; //0x5568
	uint32_t N00005A59; //0x556C
	uint32_t N000044A5; //0x5570
	uint32_t N00005A5B; //0x5574
	uint32_t N000044A6; //0x5578
	uint32_t N00005A5D; //0x557C
	uint32_t N000044A7; //0x5580
	uint32_t N00005A5F; //0x5584
	uint32_t N000044A8; //0x5588
	uint32_t N00005A61; //0x558C
	uint32_t N000044A9; //0x5590
	uint32_t N00005A63; //0x5594
	uint32_t N000044AA; //0x5598
	uint32_t N00005A65; //0x559C
	uint32_t N000044AB; //0x55A0
	uint32_t N00005A67; //0x55A4
	uint32_t N000044AC; //0x55A8
	uint32_t N00005A69; //0x55AC
	uint32_t N000044AD; //0x55B0
	uint32_t N00005A6B; //0x55B4
	uint32_t N000044AE; //0x55B8
	uint32_t N00005A6D; //0x55BC
	uint32_t N000044AF; //0x55C0
	uint32_t N00005A6F; //0x55C4
	uint32_t N000044B0; //0x55C8
	uint32_t N00005A71; //0x55CC
	uint32_t N000044B1; //0x55D0
	uint32_t N00005A73; //0x55D4
	uint32_t N000044B2; //0x55D8
	uint32_t N00005A75; //0x55DC
	uint32_t N000044B3; //0x55E0
	uint32_t N00005A77; //0x55E4
	uint32_t N000044B4; //0x55E8
	uint32_t N00005A79; //0x55EC
	uint32_t N000044B5; //0x55F0
	uint32_t N00005A7B; //0x55F4
	uint32_t N000044B6; //0x55F8
	uint32_t N00005A7D; //0x55FC
	uint32_t N000044B7; //0x5600
	uint32_t N00005A7F; //0x5604
	uint32_t N000044B8; //0x5608
	uint32_t N00005A81; //0x560C
	uint32_t N000044B9; //0x5610
	uint32_t N00005A83; //0x5614
	uint32_t N000044BA; //0x5618
	uint32_t N00005A85; //0x561C
	uint32_t N000044BB; //0x5620
	uint32_t N00005A87; //0x5624
	uint32_t N000044BC; //0x5628
	uint32_t N00005A89; //0x562C
	uint32_t N000044BD; //0x5630
	uint32_t N00005A8B; //0x5634
	uint32_t N000044BE; //0x5638
	uint32_t N00005A8D; //0x563C
	uint32_t N000044BF; //0x5640
	uint32_t N00005A8F; //0x5644
	uint32_t N000044C0; //0x5648
	uint32_t N00005A91; //0x564C
	uint32_t N000044C1; //0x5650
	uint32_t N00005A93; //0x5654
	uint32_t N000044C2; //0x5658
	uint32_t N00005A95; //0x565C
	uint32_t N000044C3; //0x5660
	uint32_t N00005A97; //0x5664
	uint32_t N000044C4; //0x5668
	uint32_t N00005A99; //0x566C
	uint32_t N000044C5; //0x5670
	uint32_t N00005A9B; //0x5674
	uint32_t N000044C6; //0x5678
	uint32_t N00005A9D; //0x567C
	uint32_t N000044C7; //0x5680
	uint32_t N00005A9F; //0x5684
	uint32_t N000044C8; //0x5688
	uint32_t N00005AA1; //0x568C
	uint32_t N000044C9; //0x5690
	uint32_t N00005AA3; //0x5694
	uint32_t N000044CA; //0x5698
	uint32_t N00005AA5; //0x569C
	uint32_t N000044CB; //0x56A0
	uint32_t N00005AA7; //0x56A4
	uint32_t N000044CC; //0x56A8
	uint32_t N00005AA9; //0x56AC
	uint32_t N000044CD; //0x56B0
	uint32_t N00005AAB; //0x56B4
	uint32_t N000044CE; //0x56B8
	uint32_t N00005AAD; //0x56BC
	uint32_t N000044CF; //0x56C0
	uint32_t N00005AAF; //0x56C4
	uint32_t N000044D0; //0x56C8
	uint32_t N00005AB1; //0x56CC
	uint32_t N000044D1; //0x56D0
	uint32_t N00005AB3; //0x56D4
	uint32_t N000044D2; //0x56D8
	uint32_t N00005AB5; //0x56DC
	uint32_t N000044D3; //0x56E0
	uint32_t N00005AB7; //0x56E4
	uint32_t N000044D4; //0x56E8
	uint32_t N00005AB9; //0x56EC
	uint32_t N000044D5; //0x56F0
	uint32_t N00005ABB; //0x56F4
	uint32_t N000044D6; //0x56F8
	uint32_t N00005ABD; //0x56FC
	uint32_t N000044D7; //0x5700
	uint32_t N00005ABF; //0x5704
	uint32_t N000044D8; //0x5708
	uint32_t N00005AC1; //0x570C
	uint32_t N000044D9; //0x5710
	uint32_t N00005AC3; //0x5714
	uint32_t N000044DA; //0x5718
	uint32_t N00005AC5; //0x571C
	uint32_t N000044DB; //0x5720
	uint32_t N00005AC7; //0x5724
	uint32_t N000044DC; //0x5728
	uint32_t N00005AC9; //0x572C
	uint32_t N000044DD; //0x5730
	uint32_t N00005ACB; //0x5734
	uint32_t N000044DE; //0x5738
	uint32_t N00005ACD; //0x573C
	uint32_t N000044DF; //0x5740
	uint32_t N00005ACF; //0x5744
	uint32_t N000044E0; //0x5748
	uint32_t N00005AD1; //0x574C
	uint32_t N000044E1; //0x5750
	uint32_t N00005AD3; //0x5754
	uint32_t N000044E2; //0x5758
	uint32_t N00005AD5; //0x575C
	uint32_t N000044E3; //0x5760
	uint32_t N00005AD7; //0x5764
	uint32_t N000044E4; //0x5768
	uint32_t N00005AD9; //0x576C
	uint32_t N000044E5; //0x5770
	uint32_t N00005ADB; //0x5774
	uint32_t N000044E6; //0x5778
	uint32_t N00005ADD; //0x577C
	uint32_t N000044E7; //0x5780
	uint32_t N00005ADF; //0x5784
	uint32_t N000044E8; //0x5788
	uint32_t N00005AE1; //0x578C
	uint32_t N000044E9; //0x5790
	uint32_t N00005AE3; //0x5794
	uint32_t N000044EA; //0x5798
	uint32_t N00005AE5; //0x579C
	uint32_t N000044EB; //0x57A0
	uint32_t N00005AE7; //0x57A4
	uint32_t N000044EC; //0x57A8
	uint32_t N00005AE9; //0x57AC
	uint32_t N000044ED; //0x57B0
	uint32_t N00005AEB; //0x57B4
	uint32_t N000044EE; //0x57B8
	uint32_t N00005AED; //0x57BC
	uint32_t N000044EF; //0x57C0
	uint32_t N00005AEF; //0x57C4
	uint32_t N000044F0; //0x57C8
	uint32_t N00005AF1; //0x57CC
	uint32_t N000044F1; //0x57D0
	uint32_t N00005AF3; //0x57D4
	uint32_t N000044F2; //0x57D8
	uint32_t N00005AF5; //0x57DC
	uint32_t N000044F3; //0x57E0
	uint32_t N00005AF7; //0x57E4
	uint32_t N000044F4; //0x57E8
	uint32_t N00005AF9; //0x57EC
	uint32_t N000044F5; //0x57F0
	uint32_t N00005AFB; //0x57F4
	uint32_t N000044F6; //0x57F8
	uint32_t N00005AFD; //0x57FC
	uint32_t N000044F7; //0x5800
	uint32_t N00005AFF; //0x5804
	uint32_t N000044F8; //0x5808
	uint32_t N00005B01; //0x580C
	uint32_t N000044F9; //0x5810
	uint32_t N00005B03; //0x5814
	uint32_t N000044FA; //0x5818
	uint32_t N00005B05; //0x581C
	uint32_t N000044FB; //0x5820
	uint32_t N00005B07; //0x5824
	uint32_t N000044FC; //0x5828
	uint32_t N00005B09; //0x582C
	uint32_t N000044FD; //0x5830
	uint32_t N00003966; //0x5834
	uint32_t N00005B0C; //0x5838
}; //Size: 0x583C


struct CursorManager
{
	uint32_t r_ScreenCenterPosX; //0x0000
	uint32_t r_ScreenCenterPosY; //0x0004
	uint32_t r_IsCursorInGame; //0x0008
	uint32_t r_PlacingBuildingTileX; //0x000C
	uint32_t r_PlacingBuildingTileY; //0x0010
	uint32_t r_PlacingBuildingTileId; //0x0014
	uint32_t N00004CB4; //0x0018
	uint32_t N00004D48; //0x001C
	uint32_t N00004CB5; //0x0020
	uint32_t r_HoverOverBuildingId; //0x0024
	uint32_t N00004CB6; //0x0028
	uint32_t N00004D4C; //0x002C
	uint32_t r_HoverOverUnitId; //0x0030
	uint32_t N00004D4E; //0x0034
	uint32_t N00004CB8; //0x0038
	uint32_t N00004D50; //0x003C
	uint32_t N00004CB9; //0x0040
	uint32_t N00004D52; //0x0044
	uint32_t N00004CBA; //0x0048
	uint32_t N00004D54; //0x004C
	uint32_t N00004CBB; //0x0050
	uint32_t N00004D56; //0x0054
	uint32_t N00004CBC; //0x0058
	uint32_t r_HoverOverWallTileId; //0x005C
	uint32_t N00004CBD; //0x0060
	uint32_t N00004D5A; //0x0064
	uint32_t r_MouseTileId2; //0x0068
	uint32_t r_MouseTileX; //0x006C
	uint32_t r_MouseTileY; //0x0070
	uint32_t N00004D5E; //0x0074
	uint32_t r_MouseTileId; //0x0078
	uint32_t N00004D60; //0x007C
	uint32_t N00004CC1; //0x0080
	uint32_t N00004D62; //0x0084
	uint32_t N00004CC2; //0x0088
	uint32_t N00004D64; //0x008C
	uint32_t N00004CC3; //0x0090
	uint32_t N00004D66; //0x0094
	uint32_t N00004CC4; //0x0098
	uint32_t N00004D68; //0x009C
	uint32_t N00004CC5; //0x00A0
	uint32_t N00004D6A; //0x00A4
	uint32_t N00004CC6; //0x00A8
	uint32_t N00004D6C; //0x00AC
	uint32_t N00004CC7; //0x00B0
	uint32_t N00004D6E; //0x00B4
	uint32_t N00004CC8; //0x00B8
	uint32_t N00004D70; //0x00BC
	uint32_t N00004CC9; //0x00C0
	uint32_t N00004D72; //0x00C4
	uint32_t N00004CCA; //0x00C8
	uint32_t N00004D74; //0x00CC
	uint32_t N00004CCB; //0x00D0
	uint32_t N00004D76; //0x00D4
	uint32_t N00004CCC; //0x00D8
	uint32_t N00004D78; //0x00DC
	uint32_t N00004CCD; //0x00E0
	uint32_t N00004D7A; //0x00E4
	uint32_t N00004CCE; //0x00E8
	uint32_t N00004D7C; //0x00EC
	uint32_t N00004CCF; //0x00F0
	uint32_t N00004DA0; //0x00F4
	uint32_t N00004CD0; //0x00F8
	uint32_t N00004DA2; //0x00FC
	uint32_t N00004CD1; //0x0100
	uint32_t N00004DA4; //0x0104
	uint32_t N00004CD2; //0x0108
	uint32_t N00004DA6; //0x010C
	uint32_t N00004CD3; //0x0110
	uint32_t N00004DA8; //0x0114
	uint32_t N00004CD4; //0x0118
	uint32_t N00004DAA; //0x011C
	uint32_t N00004CD5; //0x0120
	uint32_t N00004DAC; //0x0124
	uint32_t N00004CD6; //0x0128
	uint32_t N00004DAE; //0x012C
	uint32_t N00004CD7; //0x0130
	uint32_t N00004DB0; //0x0134
	uint32_t N00004CD8; //0x0138
	uint32_t N00004DB2; //0x013C
	uint32_t N00004CD9; //0x0140
	uint32_t N00004DB4; //0x0144
	uint32_t N00004CDA; //0x0148
	uint32_t N00004DB6; //0x014C
	uint32_t N00004CDB; //0x0150
	uint32_t N00004DB8; //0x0154
	uint32_t N00004CDC; //0x0158
	uint32_t N00004DBA; //0x015C
	uint32_t N00004CDD; //0x0160
	uint32_t N00004DBC; //0x0164
	uint32_t N00004CDE; //0x0168
	uint32_t N00004DBE; //0x016C
	uint32_t N00004CDF; //0x0170
	uint32_t N00004DC0; //0x0174
	uint32_t N00004CE0; //0x0178
	uint32_t N00004DC2; //0x017C
	uint32_t N00004CE1; //0x0180
	uint32_t N00004DC4; //0x0184
	uint32_t N00004CE2; //0x0188
	uint32_t N00004DC6; //0x018C
	uint32_t N00004CE3; //0x0190
	uint32_t N00004DC8; //0x0194
	uint32_t N00004CE4; //0x0198
	uint32_t N00004DCA; //0x019C
	uint32_t N00004CE5; //0x01A0
	uint32_t N00004DCC; //0x01A4
	uint32_t N00004CE6; //0x01A8
	uint32_t N00004DCE; //0x01AC
	uint32_t N00004CE7; //0x01B0
	uint32_t N00004DD0; //0x01B4
	uint32_t N00004CE8; //0x01B8
	uint32_t N00004DD2; //0x01BC
	uint32_t N00004CE9; //0x01C0
	uint32_t N00004DD4; //0x01C4
	uint32_t N00004CEA; //0x01C8
	uint32_t N00004DD6; //0x01CC
	uint32_t N00004CEB; //0x01D0
	uint32_t N00004DD8; //0x01D4
	uint32_t N00004CEC; //0x01D8
	uint32_t N00004DDA; //0x01DC
	uint32_t N00004CED; //0x01E0
	uint32_t N00004DDC; //0x01E4
	uint32_t N00004CEE; //0x01E8
	uint32_t N00004DDE; //0x01EC
	uint32_t N00004CEF; //0x01F0
	uint32_t N00004DE0; //0x01F4
	uint32_t N00004CF0; //0x01F8
	uint32_t N00004DE2; //0x01FC
	uint32_t N00004CF1; //0x0200
	uint32_t N00004DE4; //0x0204
	uint32_t N00004CF2; //0x0208
	uint32_t N00004DE6; //0x020C
	uint32_t N00004CF3; //0x0210
	uint32_t N00004DE8; //0x0214
	uint32_t N00004CF4; //0x0218
	uint32_t N00004DEA; //0x021C
	uint32_t N00004CF5; //0x0220
	uint32_t N00004DEC; //0x0224
	uint32_t N00004CF6; //0x0228
	uint32_t N00004DEE; //0x022C
	uint32_t N00004CF7; //0x0230
	uint32_t N00004DF0; //0x0234
	uint32_t N00004CF8; //0x0238
	uint32_t N00004DF2; //0x023C
	uint32_t N00004CF9; //0x0240
	uint32_t N00004DF4; //0x0244
	uint32_t N00004CFA; //0x0248
	uint32_t N00004DF6; //0x024C
	uint32_t N00004CFB; //0x0250
	uint32_t N00004DF8; //0x0254
	uint32_t N00004CFC; //0x0258
	uint32_t N00004DFA; //0x025C
	uint32_t N00004CFD; //0x0260
	uint32_t N00004DFC; //0x0264
	uint32_t N00004CFE; //0x0268
	uint32_t N00004DFE; //0x026C
	uint32_t N00004CFF; //0x0270
	uint32_t N00004E00; //0x0274
	uint32_t N00004D00; //0x0278
	uint32_t N00004E33; //0x027C
	uint32_t N00004D01; //0x0280
	uint32_t N00004E35; //0x0284
	uint32_t N00004D02; //0x0288
	uint32_t N00004E37; //0x028C
	uint32_t N00004D03; //0x0290
	uint32_t N00004E39; //0x0294
	uint32_t N00004D04; //0x0298
	uint32_t N00004E3B; //0x029C
	uint32_t N00004D05; //0x02A0
	uint32_t N00004E3D; //0x02A4
	uint32_t N00004D06; //0x02A8
	uint32_t N00004E3F; //0x02AC
	uint32_t N00004D07; //0x02B0
	uint32_t N00004E41; //0x02B4
	uint32_t N00004D08; //0x02B8
	uint32_t N00004E43; //0x02BC
	uint32_t N00004D09; //0x02C0
	uint32_t N00004E45; //0x02C4
	uint32_t N00004D0A; //0x02C8
	uint32_t N00004E47; //0x02CC
	uint32_t N00004D0B; //0x02D0
	uint32_t N00004E49; //0x02D4
	uint32_t N00004D0C; //0x02D8
	uint32_t N00004E4B; //0x02DC
	uint32_t N00004D0D; //0x02E0
	uint32_t N00004E4D; //0x02E4
	uint32_t N00004D0E; //0x02E8
	uint32_t N00004E4F; //0x02EC
	uint32_t N00004D0F; //0x02F0
	uint32_t N00004E51; //0x02F4
	uint32_t N00004D10; //0x02F8
	uint32_t N00004E53; //0x02FC
	uint32_t N00004D11; //0x0300
	uint32_t N00004E55; //0x0304
	uint32_t N00004D12; //0x0308
	uint32_t N00004E57; //0x030C
	uint32_t N00004D13; //0x0310
	uint32_t N00004E59; //0x0314
	uint32_t N00004D14; //0x0318
	uint32_t N00004E5B; //0x031C
	uint32_t N00004D15; //0x0320
	uint32_t N00004E5D; //0x0324
	uint32_t N00004D16; //0x0328
	uint32_t N00004E5F; //0x032C
	uint32_t N00004D17; //0x0330
	uint32_t N00004E61; //0x0334
	uint32_t N00004D18; //0x0338
	uint32_t N00004E63; //0x033C
	uint32_t N00004D19; //0x0340
	uint32_t N00004E65; //0x0344
	uint32_t N00004D1A; //0x0348
	uint32_t N00004E67; //0x034C
	uint32_t N00004D1B; //0x0350
	uint32_t N00004E69; //0x0354
	uint32_t N00004D1C; //0x0358
	uint32_t N00004E6B; //0x035C
	uint32_t N00004D1D; //0x0360
	uint32_t N00004E6D; //0x0364
	uint32_t N00004D1E; //0x0368
	uint32_t N00004E6F; //0x036C
	uint32_t N00004D1F; //0x0370
	uint32_t N00004E71; //0x0374
	uint32_t N00004D20; //0x0378
	uint32_t N00004E73; //0x037C
	uint32_t N00004D21; //0x0380
	uint32_t N00004E75; //0x0384
	uint32_t N00004D22; //0x0388
	uint32_t N00004E77; //0x038C
	uint32_t N00004D23; //0x0390
	uint32_t N00004E79; //0x0394
	uint32_t N00004D24; //0x0398
	uint32_t N00004E7B; //0x039C
	uint32_t N00004D25; //0x03A0
	uint32_t N00004E7D; //0x03A4
	uint32_t N00004D26; //0x03A8
	uint32_t N00004E7F; //0x03AC
	uint32_t N00004D27; //0x03B0
	uint32_t N00004E81; //0x03B4
	uint32_t N00004D28; //0x03B8
	uint32_t N00004E83; //0x03BC
	uint32_t N00004D29; //0x03C0
	uint32_t N00004E85; //0x03C4
	uint32_t N00004D2A; //0x03C8
	uint32_t N00004E87; //0x03CC
	uint32_t N00004D2B; //0x03D0
	uint32_t N00004E89; //0x03D4
	uint32_t N00004D2C; //0x03D8
	uint32_t N00004E8B; //0x03DC
	uint32_t N00004D2D; //0x03E0
	uint32_t N00004E8D; //0x03E4
	uint32_t N00004D2E; //0x03E8
	uint32_t N00004E8F; //0x03EC
	uint32_t N00004D2F; //0x03F0
	uint32_t N00004E91; //0x03F4
	uint32_t N00004D30; //0x03F8
	uint32_t N00004E93; //0x03FC
	uint32_t N00004D31; //0x0400
	uint32_t N00004E95; //0x0404
	uint32_t N00004D32; //0x0408
	uint32_t N00004E97; //0x040C
	uint32_t N00004D33; //0x0410
	uint32_t N00004E99; //0x0414
	uint32_t N00004D34; //0x0418
	uint32_t N00004E9B; //0x041C
	uint32_t N00004D35; //0x0420
	uint32_t N00004E9D; //0x0424
	uint32_t N00004D36; //0x0428
	uint32_t N00004E9F; //0x042C
	uint32_t N00004D37; //0x0430
	uint32_t N00004EA1; //0x0434
	uint32_t N00004D38; //0x0438
	uint32_t N00004EA3; //0x043C
	uint32_t N00004D39; //0x0440
	uint32_t N00004EA5; //0x0444
	uint32_t N00004D3A; //0x0448
	uint32_t N00004EA7; //0x044C
	uint32_t N00004D3B; //0x0450
	uint32_t N00004EA9; //0x0454
	uint32_t N00004D3C; //0x0458
	uint32_t N00004EAB; //0x045C
	uint32_t N00004D3D; //0x0460
	uint32_t N00004EAD; //0x0464
	uint32_t N00004D3E; //0x0468
	uint32_t N00004EAF; //0x046C
	uint32_t N00004D3F; //0x0470
	uint32_t N00004EB1; //0x0474
	uint32_t N00004D40; //0x0478
	uint32_t N00004EB3; //0x047C
}; //Size: 0x0480

typedef enum VegType
{
	VegType_None             = 0x0,
	VegType_OliveTree        = 0x1,
	VegType_DatePalm         = 0x2,
	VegType_CocoPalm         = 0x3,
	VegType_CherryTree       = 0x4,
	VegType_DesertShrub1     = 0x5,
	VegType_DesertShrub1Var2 = 0x6,
	VegType_DesertShrub1Var3 = 0x7,
	VegType_DesertShrub1Var4 = 0x8,
	VegType_DesertShrub1Var5 = 0x9,
	VegType_Cactus1          = 0xA,
	VegType_Unused1          = 0xB,
	VegType_Unused2          = 0xC,
	VegType_Unused3          = 0xD,
	VegType_Unused4          = 0xE,
	VegType_BigBush          = 0xF,
	VegType_Cactus2          = 0x10,
	VegType_Cactus3          = 0x11,
	VegType_DesertShrub2     = 0x12,
	VegType_Cactus4          = 0x13
} VegType;

typedef enum ProjectileType
{
    ProjectileType_Unknown = 0x0,
    ProjectileType_ArcherArrow = 0x1,
    ProjectileType_CatapultRocks = 0x2,
    ProjectileType_TrebutchetRocks = 0x3,
    ProjectileType_MangongelRocks = 0x4,
    ProjectileType_Steam1 = 0x5,
    ProjectileType_AfterImage = 0x6,
    ProjectileType_CrossbowBolt = 0x7,
    ProjectileType_EngineerLava = 0x8,
    ProjectileType_StaticFire = 0x9,
    ProjectileType_Flag1 = 0xA,
    ProjectileType_Flag3 = 0xB,
    ProjectileType_Flag2 = 0xC,
    ProjectileType_CrusaderFlag = 0xD,
    ProjectileType_Brazier = 0xE,
    ProjectileType_Heads = 0xF,
    ProjectileType_UnkFlag1 = 0x10,
    ProjectileType_UnkFlag2 = 0x11,
    ProjectileType_UnkFlag3 = 0x12,
    ProjectileType_UnkFlag4 = 0x13,
    ProjectileType_BallistaBolt = 0x14,
    ProjectileType_Steam2 = 0x15,
    ProjectileType_Disease = 0x16,
    ProjectileType_Cow = 0x17,
    ProjectileType_UnkMissile24 = 0x18,
    ProjectileType_UnkMissile25 = 0x19,
    ProjectileType_UnkBlast = 0x1A,
    ProjectileType_CatapultOrTrebutchetRocksImpactDebris1 = 0x1B,
    ProjectileType_Crow = 0x1C,
    ProjectileType_Seagull = 0x1D,
    ProjectileType_CatapultOrTrebutchetRocksImpactDebris2 = 0x1E,
    ProjectileType_BodySplash = 0x1F,
    ProjectileType_RockChipsFire1 = 0x20,
    ProjectileType_SlingerStone = 0x21,
    ProjectileType_ArabGrenadierOrBedouinAmbusherOrDiseaseCloud = 0x22,
    ProjectileType_RockChips3 = 0x23,
    ProjectileType_GrenadierGrenade = 0x24,
    ProjectileType_ArabBallistaBolt = 0x25,
    ProjectileType_BedouinLance = 0x26,
    ProjectileType_UnkJavelin39 = 0x27,
    ProjectileType_UnkInfo1 = 0x28,
    ProjectileType_UnkInfo2 = 0x29,
    ProjectileType_UnkInfo3 = 0x2A,
    ProjectileType_UnkInfo4 = 0x2B,
    ProjectileType_Condor = 0x31
} ProjectileType;

/* Derived from AILordMessageType.h; C++ syntax normalized for Ghidra CParser. */
typedef enum AILordMessageType
{
    IncomingMessage = 0,
    WillAttack = 1,
    TauntSiege2 = 2,
    TauntSiege3 = 3,
    TauntSiege4 = 4,
    AngerSiegeFailed = 5,
    AngerFortressDamaged = 6,
    PleadDeath = 7,
    PleadOutsideWalls = 8,
    NervousInsideWalls = 9,
    Counterattack = 10,
    Unk11 = 11,
    Won = 12,
    Unk13 = 13,
    RequestGoods = 14,
    ReceivedGoods = 15,
    DefeatedAgain = 16,
    AllyNotificationCongratulations = 16,
    AllyNotificationHasDefeatedEnemy = 18,
    AllyNotificationRequestReinforcements = 19,
    AllyNotificationMerryChristmas = 20,
    Unk21 = 21,
    Unk22 = 22,
    AllyNotificationWillSiegeEnemySoon = 23,
    AllyNotificationCannotAttackEnemy = 24,
    AllyNotificationWillNotAttackToday = 25,
    AllyNotificationCannotNotHelp = 26,
    AllyNotificationWillNotHelp = 27,
    AllyNotificationWillNotSendRequestedGoods = 28,
    AllyNotificationHasSentRequestedGoods = 29,
    AllyNotificationConfidentInVictory = 30,
    AllyNotificationConfidentInLosing = 31,
    AllyNotificationSentReinforcements = 32,
    AllyNotificationAgree = 33
} AILordMessageType;

/* Derived from RationsMode.h; C++ syntax normalized for Ghidra CParser. */
typedef enum RationsMode
{
    None = 0,
	Half = 1,
	Full = 2,
	Extra = 3,
	Double = 4
} RationsMode;

/* Derived from TaxesMode.h; C++ syntax normalized for Ghidra CParser. */
typedef enum TaxesMode
{
    HugeDonation = 0,
	BigDonation = 1,
	SmallDonation = 2,
	None = 3,
	Low = 4,
	Moderate = 5,
	High = 6,
	Mean = 7,
	Usurious = 8,
	Cruel = 9,
	MostCruel = 10,
	Cruellest = 11
} TaxesMode;

/* Derived from TilePropertyFlags.h; C++ syntax normalized for Ghidra CParser. */
typedef enum TilePropertyFlag
{
    TilePropertyFlag_None = 0,
    TilePropertyFlag_Sea = 1 << 0,
    TilePropertyFlag_GoodsyardRelated = 1 << 1,
    TilePropertyFlag_IsFarm = 1 << 2,
    TilePropertyFlag_PitchTrap = 1 << 3,
    TilePropertyFlag_RealityEdge = 1 << 4,
    TilePropertyFlag_MapBorder = 1 << 5,
    TilePropertyFlag_ImpassableEdge = 1 << 7,
    TilePropertyFlag_IsWall = 1 << 8,
    TilePropertyFlag_CrenelationComponent = 1 << 9,
    TilePropertyFlag_IsBuilding = 1 << 10,
    TilePropertyFlag_IsStairs = 1 << 11,
    TilePropertyFlag_IsTree = 1 << 12,
    TilePropertyFlag_TreeProximity = 1 << 13,
    TilePropertyFlag_PlannedMoat = 1 << 14,
    TilePropertyFlag_IsLand = 1 << 15,
    TilePropertyFlag_IsLowWall = 1 << 16,
    TilePropertyFlag_HasStone = 1 << 17,
    TilePropertyFlag_HasIron = 1 << 19,
    TilePropertyFlag_River = 1 << 20,
    TilePropertyFlag_Ford = 1 << 21,
    TilePropertyFlag_CrenelationModifier = 1 << 22,
    TilePropertyFlag_IsWheat = 1 << 24,
    TilePropertyFlag_IsHops = 1 << 25,
    TilePropertyFlag_IsAppleFarm = 1 << 26,
    TilePropertyFlag_IsFarmFence = 1 << 27,
    TilePropertyFlag_IsElevated = 1 << 28,
    TilePropertyFlag_IsSwamp = 1 << 29,
    TilePropertyFlag_IsMoat = 1 << 30,
    TilePropertyFlag_IsPitch = 1u << 31
} TilePropertyFlag;


/* Derived from TileType.h; C++ syntax normalized for Ghidra CParser. */
typedef enum TileType
{
    TileType_NoneOrDirt = 0,
    TileType_Foliage = 1 << 0,
    TileType_DirtAndStones = 1 << 1,
    TileType_Elevation1 = 1 << 2,
    TileType_Elevation2 = 1 << 3,
    TileType_OasisGrass = 1 << 4,
    TileType_BeachOrWaves = 1 << 5,
    TileType_CoarseSand = 1 << 6,
    TileType_ThickFoliage = 1 << 7
} TileType;


/* Derived from TribeAICommand.h; C++ syntax normalized for Ghidra CParser. */

/// <summary>
/// Specifies the set of commands that can be issued to a unit's TribeAI, representing various actions such as movement,
/// attacking, building, and other unit behaviors.
/// </summary>
/// <remarks>This enumeration defines the possible instructions that may be assigned to units within the game AI
/// system. Some values correspond to specific actions, such as attacking a unit or building, moving to a location, or
/// performing engineering tasks. Several members are reserved or have unknown purposes and may be used internally still. 
/// The meaning and required parameters for each command may vary</remarks>
typedef enum TribeAICommand
{
    TribeAICommand_Unknown0 = 0,
    TribeAICommand_Unknown1 = 1,
    TribeAICommand_Unknown2 = 2,
    TribeAICommand_MoveHerePosition = 3,       // Move unit to tile position, r9 = tileX, Stack1 = tileY
    TribeAICommand_AttackUnit = 4,             // Attack Unit as Meele/or ranged) r9 = TargetUnitId, Stack1 = TargetTribeGlobalId
    TribeAICommand_AttackTilePosition = 5,     // Attack Here: Ranged -> Place r9 = tileX, Stack1 = tileY, <- forced attack for attackers (range/meele), a6 = ? (15 on catapults)
    TribeAICommand_DigMoatTileId = 6,          // Dig Moat as Spearman/etc r9 = TileX, Stack1 = TileY, a6 = ? (1000 most of the time)

    // TODO: (used at E8 ? ? ? ? E9 ? ? ? ? 0F B7 84 2F)
    TribeAICommand_Unknown7 = 7,

    TribeAICommand_Unknown8 = 8,
    TribeAICommand_AttackBuilding = 9,         // Attack Building as Meele/Ranged r9 = BuildingId, Stack1 = TargetBuildingGlobalId
    TribeAICommand_Unknonw10 = 10,
    TribeAICommand_Unknown11 = 11,
    TribeAICommand_Unknown12 = 12,
    TribeAICommand_Unknown13 = 13,
    TribeAICommand_Unknown14 = 14,
    TribeAICommand_ManPitchCauldronOrBuildTent = 15, // (Man pitch cauldron OR: Build a tent as engineer) r9 = BuildingId, Stack1 = TargetBuildingGlobalId
    TribeAICommand_ManSiegeEquipment = 16,     // (Man catapult) r9 = UnitId(of catapult), Stack1 = TargetUnitGlobalId
    TribeAICommand_DissolveSiegeEquipment = 17,// r9 = UnitId(of catapult), Stack1 = TargetUnitGlobalId, a6 = 0
    TribeAICommand_Unknown18 = 18,
    TribeAICommand_Unknown19 = 19,
    TribeAICommand_ThrowLava = 20,             // Throw out lava as engineer r9 = TargetTileX; Stack1 = TargetTileY
    TribeAICommand_BuildTunnel = 21,           // Build tunnel here r9 = BuildingId, Stack1 = TargetBuildingGlobalId, a1 = 0
    TribeAICommand_Unknown22 = 22,
    TribeAICommand_AttackWallTileId = 23,      // Attack Wall as Meele r9 = TileId  (also counts for siege tower -> wall attach), Stack1 = Unused
    TribeAICommand_AttachLadderToWall = 24,    // (Attach Ladder to Wall) r9 = TileId, Stack1 = Unused
    TribeAICommand_Unknown25 = 25,
    TribeAICommand_Unknown26 = 26,
    TribeAICommand_Unknown27 = 27,
    TribeAICommand_Unknown28 = 28,
    TribeAICommand_Unknown29 = 29,
    TribeAICommand_UnitDissolve = 30,          // Dissolve r9 = unused, a6 = 1
    TribeAICommand_UnitStop = 31,              // Stop r9 = unused, a6 = 1

    // TODO: (used at E8 ? ? ? ? 44 8B 7C 24 ? E9)
    TribeAICommand_Unknown32 = 32,

    // TODO: (used by AI?)
    // rdx = The TribeId of an Archer unit
    // r9 = r_RangedAttackTargetUnitId of the issuing unit (0x340)
    // Stack1 = The UnitOffset calculated by using the r_RangedAttackTargetUnitId (0x340) unit field of the issuing unit
    TribeAICommand_Unknown33 = 33,

    TribeAICommand_Unknown34 = 34,

    // TODO: (used at E8 ? ? ? ? E9 ? ? ? ? 8B 84 24 ? ? ? ? 41 B8)
    TribeAICommand_Unknown35 = 35,
    TribeAICommand_ForceAttackBuilding = 36,   // Attack Here: Meele/Ranged -> Building r9 = BuildingId, Stack1 = TargetGlobalId, a6 = -127

    // TODO: (used at E8 ? ? ? ? E9 ? ? ? ? 90) (maybe something to do with tiles?)
    TribeAICommand_Unknown37 = 37,

    // TODO: (used at E8 ? ? ? ? E9 ? ? ? ? 41 B8 ? ? ? ? 44 89 6C 24)
    TribeAICommand_Unknown38 = 38              // USED BY AI ? (maybe something to do with tiles?)
} TribeAICommand;

/* Derived from ReClassExports.h; C++ syntax normalized for Ghidra CParser. */
// Created with ReClass.NET 1.2 by KN4CK3R


typedef enum GM16
{
    GM16_GM_LAND = 2,
    GM16_GM_PILLARS,
    GM16_GM_SEA_CHEVRONS,
    GM16_GM_SEA,
    GM16_GM_BUILDINGS1,
    GM16_GM_BUILDINGS2,
    GM16_GM_WORKSHOPS,
    GM16_GM_CLIFFS,
    GM16_GM_WALLS,
    GM16_GM_SPECIAL_LAND,
    GM16_GM_MISC_LAND,
    GM16_GM_RIVERS,
    GM16_GM_FARMLAND,
    GM16_GM_GOODS,
    GM16_GM_FLOATS,
    GM16_GM_BODY_PEASANT,
    GM16_GM_BODY_ARCHER,
    GM16_GM_BODY_WOODCUTTER,
    GM16_GM_BODY_FLETCHER,
    GM16_GM_BODY_OXCART,
    GM16_GM_BUILDING_ANIMS2,
    GM16_GM_SMOKE_ANIMS,
    GM16_GM_55X55_ANIMS,
    GM16_GM_QUARRY_ANIMS,
    GM16_GM_WINDMILL_ANIMS,
    GM16_GM_FLETCHER_ANIMS,
    GM16_GM_GOODS_ANIMS,
    GM16_GM_TREE_BIRCH,
    GM16_GM_TREE_PINE,
    GM16_GM_TREE_CHESTNUT,
    GM16_GM_BODY_STONEMASON,
    GM16_GM_BODY_FARMER,
    GM16_GM_BODY_MISSILE,
    GM16_GM_BODY_LADDERMAN,
    GM16_GM_BODY_BAKER,
    GM16_GM_BODY_MILLER,
    GM16_GM_DATA,
    GM16_GM_BODY_SPEARMAN,
    GM16_GM_BODY_PIKEMAN,
    GM16_GM_BODY_CROSSBOWMAN,
    GM16_GM_BODY_SWORDSMAN,
    GM16_GM_BODY_MACEMAN,
    GM16_GM_BODY_KNIGHT,
    GM16_GM_INTERFACE_BUTTONS,
    GM16_GM_INTERFACE_ICONS2,
    GM16_GM_MINE_ANIMS,
    GM16_GM_TILE_BURNT,
    GM16_GM_CHURCHS,
    GM16_GM_INTERFACE_PANELS,
    GM16_GM_WORKSHOP_BREW_ANIMS,
    GM16_GM_CASTLES,
    GM16_GM_BODY_BREWER,
    GM16_GM_CASTLE_ANIMS,
    GM16_GM_MACRO_LAND,
    GM16_GM_ROCKS,
    GM16_GM_ROCKS_CHEVRONS,
    GM16_GM_WORKSHOP_SMITH_ANIMS,
    GM16_GM_BODY_BLACKSMITH,
    GM16_GM_LAND_AND_STONES,
    GM16_GM_BODY_IRONMINER,
    GM16_GM_BODY_CATAPULT,
    GM16_GM_BODY_COW,
    GM16_GM_WORKSHOP_POLE_ANIMS,
    GM16_GM_PITCH_ANIMS,
    GM16_GM_WORKSHOP_BAKER_ANIMS,
    GM16_GM_WOODCUTTER_ANIMS,
    GM16_GM_DRAWBRIDGE_ANIMS,
    GM16_GM_WORKSHOP_TANNER_ANIMS,
    GM16_GM_TREE_OAK,
    GM16_GM_TREE_SHRUB1,
    GM16_GM_TREE_SHRUB2,
    GM16_GM_BODY_PITCHWORKER,
    GM16_GM_BODY_POLETURNER,
    GM16_GM_BODY_TANNER,
    GM16_GM_FLAG_ANIMS,
    GM16_GM_BODY_TRADER_HORSE,
    GM16_GM_BODY_TRADER,
    GM16_GM_ICONS,
    GM16_GM_ICONS_ALPHA,
    GM16_GM_BODY_DRUNKARD,
    GM16_GM_BODY_TENT,
    GM16_GM_BODY_MANGONEL,
    GM16_GM_BODY_TREBUCHET,
    GM16_GM_FLOAT_POP_CIRC,
    GM16_GM_BODY_SIEGE_ENGINEER,
    GM16_GM_FONT_STRONGHOLD_AA,
    GM16_GM_FARMER_ANIMS,
    GM16_GM_BODY_HUNTER,
    GM16_GM_HUNTER_ANIMS,
    GM16_GM_BODY_DEER,
    GM16_GM_BODY_LION,
    GM16_GM_BODY_RABBIT,
    GM16_GM_BODY_CAMEL,
    GM16_GM_BODY_DOG,
    GM16_GM_BODY_PRIEST,
    GM16_GM_TREE_APPLE,
    GM16_GM_STABLE_ANIMS,
    GM16_GM_BODY_LADY,
    GM16_GM_BODY_LORD,
    GM16_GM_BODY_JESTER,
    GM16_GM_BODY_ARMOURER,
    GM16_GM_ARMOURER_ANIMS,
    GM16_GM_SHEILD_ANIMS,
    GM16_GM_ANIM_TUNNELERS_GUILD,
    GM16_GM_BODY_TUNNELER,
    GM16_GM_CURSORS,
    GM16_GM_MAPEDIT_BUTTONS,
    GM16_GM_BODY_FIGHTING_MONK,
    GM16_GM_OIL_ANIMS = 113,
    GM16_GM_GALLOWS_ANIMS,
    GM16_GM_MAYPOLE_ANIMS,
    GM16_GM_BODY_OIL,
    GM16_GM_FONT_STRONGHOLD,
    GM16_GM_BODY_FIRE,
    GM16_GM_BODY_BURNING_MAN,
    GM16_GM_BODY_BALLISTA,
    GM16_GM_BODY_SHIELD,
    GM16_GM_BODY_MISSILE_2,
    GM16_GM_BODY_BATTERING_RAM,
    GM16_GM_BODY_SIEGE_TOWER,
    GM16_GM_BODY_STEAM,
    GM16_GM_BODY_CHICKEN,
    GM16_GM_BODY_MOTHER,
    GM16_GM_BODY_BOY,
    GM16_GM_BODY_GIRL,
    GM16_GM_ANIM_TUNNELS,
    GM16_GM_BODY_JUGGLER,
    GM16_GM_BODY_FIREEATER,
    GM16_GM_BODY_HEALER,
    GM16_GM_BODY_DISEASE,
    GM16_GM_BODY_MISSILE_COW,
    GM16_GM_CRACKS,
    GM16_GM_BODY_GATE,
    GM16_GM_BODY_BRAZIER,
    GM16_GM_KILLING_PITS,
    GM16_GM_PITCH_DITCHES,
    GM16_GM_BLAST,
    GM16_GM_SCRIBE,
    GM16_GM_ANIM_ICON_KNIGHT,
    GM16_GM_BODY_FIRE2,
    GM16_GM_ANIM_MISSILE_FIRE,
    GM16_GM_FONT_SLANTED,
    GM16_GM_BODY_INNKEEPER,
    GM16_GM_ICONS_FRONT_END,
    GM16_GM_TILE_RUINS,
    GM16_GM_ICONS_FRONT_END_COMBAT,
    GM16_GM_ICONS_FRONT_END_ECONOMICS,
    GM16_GM_ICONS_FRONT_END_BUILDER,
    GM16_GM_MINI_CURSORS,
    GM16_GM_BODY_CHICKEN_BROWN,
    GM16_GM_ANIM_MARKET,
    GM16_GM_INTERFACE_ICONS3,
    GM16_GM_TILE_FLATTIES,
    GM16_GM_ROCK_CHIPS,
    GM16_GM_ANIM_DUNKING_STOOL,
    GM16_GM_ANIM_DUNGEON,
    GM16_GM_ANIM_GIBBET,
    GM16_GM_ANIM_HEALER,
    GM16_GM_ANIM_STOCKS,
    GM16_GM_INTERFACE_SLIDER,
    GM16_GM_MAP_FLAGS,
    GM16_GM_NEW_SEA,
    GM16_GM_BODY_SEAGULL,
    GM16_GM_BODY_CROW,
    GM16_GM_PUFF_OF_SMOKE,
    GM16_GM_BODY_SPLASH,
    GM16_GM_ANIM_INN,
    GM16_GM_FLOATS_NEW,
    GM16_GM_ANIM_WHITECAPS,
    GM16_GM_ARMY_UNITS,
    GM16_GM_ANIM_STAKE,
    GM16_GM_ANIM_KILLING_PITS,
    GM16_GM_ENEMY_FACES,
    GM16_GM_ANIM_RACK,
    GM16_GM_ANIM_DOG_CAGE,
    GM16_GM_ANIM_DANCING_BEAR,
    GM16_GM_ANIM_CHOPPING_BLOCK,
    GM16_GM_BODY_FIREMAN,
    GM16_GM_INTERFACE_ARMY,
    GM16_GM_INTERFACE_RUINS,
    GM16_GM_BODY_ANIMAL_BURNING_BIG,
    GM16_GM_BODY_ANIMAL_BURNING_SMALL,
    GM16_GM_ANIM_HEADS,
    GM16_GM_BODY_GHOST,
    GM16_GM_ANIM_FLAG_SMALL,
    GM16_GM_BODY_ARAB_BOW,
    GM16_GM_BODY_ARAB_SLAVE,
    GM16_GM_BODY_ARAB_SLINGER,
    GM16_GM_BODY_ARAB_ASSASIN,
    GM16_GM_BODY_ARAB_HORSEMAN,
    GM16_GM_BODY_ARAB_SWORDSMAN,
    GM16_GM_BODY_ARAB_GRENADIER,
    GM16_GM_BODY_ARAB_BALLISTA,
    GM16_GM_ASSASIN_ROPE,
    GM16_GM_BODY_ARAB_HORSE,
    GM16_GM_TREE_CACTII,
    GM16_GM_ANIM_CRUSADER_FLAG,
    GM16_GM_BODY_INFO,
    GM16_GM_BODY_WOLF,
    GM16_GM_BODY_ARABIC_LORD = 205,
    GM16_GM_ADDITIONAL_GFX = 207,
    GM16_GM_BODY_BEDOUIN_CAMEL_LANCER,
    GM16_GM_BODY_BEDOUIN_HEALER,
    GM16_GM_BODY_BEDOUIN_EUNUCH,
    GM16_GM_BODY_BEDOUIN_AMBUSHER,
    GM16_GM_BODY_BEDOUIN_SKIRMISHER,
    GM16_GM_BODY_BEDOUIN_HEAVY_CAMEL,
    GM16_GM_BODY_BEDOUIN_SAPPER,
    GM16_GM_BODY_BEDOUIN_DEMOLISHER,
    GM16_GM_FLOAT_POP_CIRC_2 = 218,
    GM16_GM_BODY_MISSILE_FIREPOT,
    GM16_GM_BODY_JAVELIN,
    GM16_GM_BODY_GOAT,
    GM16_GM_BODY_HYENA,
    GM16_GM_BODY_CONDOR,
    GM16_GM_BODY_CROCODILE,
    GM16_GM_BODY_IMAM,
    GM16_GM_BODY_BEDOUIN_LORD,
    GM16_GM_BODY_SCRIBE_LORD,
    GM16_GM_BODY_LORD_FEMALE,
    GM16_GM_BODY_TEMPLE_GUARD,
    GM16_GM_BODY_LORD_BESSY,
    GM16_GM_BODY_ARABIC_LORD_FEMALE,
    GM16_GM_BODY_BEDOUIN_LORD_FEMALE,
    GM16_GM_ANIM_ARAB_FLAG
} GM16;

typedef enum Dircs16
{
    Dircs16_Invalid = -1,
    Dircs16_Centre,
    Dircs16_North,
    Dircs16_NE,
    Dircs16_East,
    Dircs16_SE,
    Dircs16_South,
    Dircs16_SW,
    Dircs16_West,
    Dircs16_NW,
    Dircs16_Base
} Dircs16;

typedef enum eChimps16
{
    eChimps16_CHIMP_TYPE_NULL,
    eChimps16_CHIMP_TYPE_PEASANT,
    eChimps16_CHIMP_TYPE_BURNING_MAN,
    eChimps16_CHIMP_TYPE_WOODCUTTER,
    eChimps16_CHIMP_TYPE_FLETCHER,
    eChimps16_CHIMP_TYPE_TUNNELER,
    eChimps16_CHIMP_TYPE_HUNTER,
    eChimps16_CHIMP_TYPE_QUARRY_MASON,
    eChimps16_CHIMP_TYPE_QUARRY_GRUNT,
    eChimps16_CHIMP_TYPE_QUARRY_OX,
    eChimps16_CHIMP_TYPE_PITCHMAN,
    eChimps16_CHIMP_TYPE_FARMER_WHEAT,
    eChimps16_CHIMP_TYPE_FARMER_HOPS,
    eChimps16_CHIMP_TYPE_FARMER_APPLE,
    eChimps16_CHIMP_TYPE_FARMER_CATTLE,
    eChimps16_CHIMP_TYPE_MILLER,
    eChimps16_CHIMP_TYPE_BAKER,
    eChimps16_CHIMP_TYPE_BREWER,
    eChimps16_CHIMP_TYPE_POLETURNER,
    eChimps16_CHIMP_TYPE_BLACKSMITH,
    eChimps16_CHIMP_TYPE_ARMOURER,
    eChimps16_CHIMP_TYPE_TANNER,
    eChimps16_CHIMP_TYPE_ARCHER,
    eChimps16_CHIMP_TYPE_XBOWMAN,
    eChimps16_CHIMP_TYPE_SPEARMAN,
    eChimps16_CHIMP_TYPE_PIKEMAN,
    eChimps16_CHIMP_TYPE_MACEMAN,
    eChimps16_CHIMP_TYPE_SWORDSMAN,
    eChimps16_CHIMP_TYPE_KNIGHT,
    eChimps16_CHIMP_TYPE_LADDERMAN,
    eChimps16_CHIMP_TYPE_ENGINEER,
    eChimps16_CHIMP_TYPE_MINER1,
    eChimps16_CHIMP_TYPE_MINER2,
    eChimps16_CHIMP_TYPE_PRIEST,
    eChimps16_CHIMP_TYPE_HEALER,
    eChimps16_CHIMP_TYPE_DRUNKARD,
    eChimps16_CHIMP_TYPE_INNKEEPER,
    eChimps16_CHIMP_TYPE_MONK,
    eChimps16_CHIMP_TYPE_ARCHER_debug,
    eChimps16_CHIMP_TYPE_CATAPULT,
    eChimps16_CHIMP_TYPE_TREBUCHET,
    eChimps16_CHIMP_TYPE_MANGONEL,
    eChimps16_CHIMP_TYPE_TRADER,
    eChimps16_CHIMP_TYPE_TRADER_HORSE,
    eChimps16_CHIMP_TYPE_DEER,
    eChimps16_CHIMP_TYPE_LION,
    eChimps16_CHIMP_TYPE_RABBIT,
    eChimps16_CHIMP_TYPE_CAMEL,
    eChimps16_CHIMP_TYPE_CROW,
    eChimps16_CHIMP_TYPE_SEAGULL,
    eChimps16_CHIMP_SIEGE_TENT,
    eChimps16_CHIMP_TYPE_COW,
    eChimps16_CHIMP_TYPE_DOG,
    eChimps16_CHIMP_TYPE_FIREMAN,
    eChimps16_CHIMP_TYPE_GHOST,
    eChimps16_CHIMP_TYPE_LORD,
    eChimps16_CHIMP_TYPE_LADY,
    eChimps16_CHIMP_TYPE_JESTER,
    eChimps16_CHIMP_TYPE_SIEGE_TOWER,
    eChimps16_CHIMP_TYPE_BATTERING_RAM,
    eChimps16_CHIMP_TYPE_PORTABLE_SHIELD,
    eChimps16_CHIMP_TYPE_BALLISTA,
    eChimps16_CHIMP_TYPE_CHICKEN,
    eChimps16_CHIMP_TYPE_MOTHER,
    eChimps16_CHIMP_TYPE_CHILD,
    eChimps16_CHIMP_TYPE_JUGGLER,
    eChimps16_CHIMP_TYPE_FIREEATER,
    eChimps16_CHIMP_TYPE_WAR_DOG,
    eChimps16_CHIMP_TYPE_BURNING_ANIMAL_BIG,
    eChimps16_CHIMP_TYPE_BURNING_ANIMAL_SMALL,
    eChimps16_CHIMP_TYPE_ARAB_BOW,
    eChimps16_CHIMP_TYPE_ARAB_SLAVE,
    eChimps16_CHIMP_TYPE_ARAB_SLINGER,
    eChimps16_CHIMP_TYPE_ARAB_ASSASIN,
    eChimps16_CHIMP_TYPE_ARAB_HORSEMAN,
    eChimps16_CHIMP_TYPE_ARAB_SWORDSMAN,
    eChimps16_CHIMP_TYPE_ARAB_GRENADIER,
    eChimps16_CHIMP_TYPE_ARAB_BALLISTA,
    eChimps16_CHIMP_TYPE_BEDOUIN_CAMEL_LANCER,
    eChimps16_CHIMP_TYPE_BEDOUIN_HEALER,
    eChimps16_CHIMP_TYPE_BEDOUIN_EUNUCH,
    eChimps16_CHIMP_TYPE_BEDOUIN_AMBUSHER,
    eChimps16_CHIMP_TYPE_BEDOUIN_SKIRMISHER,
    eChimps16_CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
    eChimps16_CHIMP_TYPE_BEDOUIN_SAPPER,
    eChimps16_CHIMP_TYPE_BEDOUIN_DEMOLISHER,
    eChimps16_CHIMP_TYPE_GOAT,
    eChimps16_CHIMP_TYPE_HYENA,
    eChimps16_CHIMP_TYPE_CROCODILE,
    eChimps16_CHIMP_NUM_TYPES
} eChimps16;

typedef enum AISiegeRole16
{
    SiegeStormTribe = 15,
    SiegeCoverTribe = 186,
	SiegeReserveTribe = 190,
	SiegeWallTribe = 192
} AISiegeRole;

typedef struct GameUnitData
{
	uint32_t r_AnimationFrame; //0x0000
	uint32_t N000000F4; //0x0004
	GM16	r_GameMaterialIndex; //0x0008
	uint16_t N0000542F; //0x000A
	uint32_t r_SpritePlayerColorId; //0x000C
	uint16_t N00000055; //0x0010
	uint16_t N000002FB; //0x0012
	uint32_t N000000F8; //0x0014
	uint16_t N00000056; //0x0018
	uint16_t N000002F7; //0x001A
	uint16_t N000000FA; //0x001C
	uint16_t N00000301; //0x001E
	uint32_t N00000057; //0x0020
	uint32_t N000000FC; //0x0024
	uint32_t N00000058; //0x0028
	uint32_t N000000FE; //0x002C
	uint32_t r_UnitSelected; //0x0030
	uint32_t r_HealthBarBlocks; //0x0034
	uint32_t r_TicksAlive1; //0x0038
	uint32_t r_TicksAlive2; //0x003C
	uint32_t N0000005B; //0x0040
	uint32_t r_UnknownFrameRelated; //0x0044
	uint32_t N0000005C; //0x0048
	uint32_t N00000106; //0x004C
	Dircs16	r_Direction; //0x0050
	uint16_t Unknown; //0x0052
	uint32_t N00000108; //0x0054
	uint32_t N0000005E; //0x0058
	uint32_t N0000010A; //0x005C
	uint32_t N0000005F; //0x0060
	uint32_t r_IsInvisible; //0x0064
	uint32_t N00000060; //0x0068
	uint32_t N0000010E; //0x006C
	uint32_t N00000061; //0x0070
	uint32_t N00000110; //0x0074
	uint32_t N00000062; //0x0078
	uint32_t r_SpawnedForPlayerIndex; //0x007C
	uint32_t N00000063; //0x0080
	uint32_t N00000114; //0x0084
	eAliveState r_UnitState; //0x0088
	eChimps16 r_UnitChimp; //0x008A
	uint32_t N00000116; //0x008C
	uint8_t N00000573; //0x0090
	uint8_t N00000568; //0x0091
	uint8_t r_ControllableForPlayerId; //0x0092
	uint8_t N00000569; //0x0093
	uint32_t r_GlobalId; //0x0094
	uint32_t N00000066; //0x0098
	uint32_t N0000011A; //0x009C
	uint32_t N00000067; //0x00A0
	uint32_t r_WorkerTargetContextEntityGlobalId; //0x00A4
	uint32_t N00000068; //0x00A8
	uint16_t N0000011E; //0x00AC
	uint16_t r_UnitSelected2; //0x00AE
	uint16_t N00000069; //0x00B0
	uint16_t r_CurrentWorldPositionX; //0x00B2
	uint16_t r_CurrentWorldPositionY; //0x00B4
	uint16_t r_HeightElevation; //0x00B6
	int16_t N0000006A; //0x00B8
	uint16_t r_LookAtWorldPositionX; //0x00BA
	uint16_t r_LookAtWorldPositionY; //0x00BC
	uint16_t r_LookAtHeight; //0x00BE
	uint16_t r_CurrentTilePositionX; //0x00C0
	uint16_t r_CurrentTilePositionY; //0x00C2
	uint16_t r_TargetTilePositionX; //0x00C4
	uint16_t r_TargetTilePositionY; //0x00C6
	uint16_t r_PreviousTilePositionX; //0x00C8
	uint16_t r_PreviousTilePositionY; //0x00CA
	uint32_t N00000126; //0x00CC
	uint32_t r_CurrentPositionTileId; //0x00D0
	uint32_t r_TargetPositionTileId; //0x00D4
	uint32_t r_PreviousPositionTileId; //0x00D8
	uint16_t r_NextTilePositionX2; //0x00DC
	uint16_t r_NextTilePositionY2; //0x00DE
	uint32_t r_NextPositionTileId2; //0x00E0
	uint32_t N0000012C; //0x00E4
	uint16_t r_TargetTilePositionX2; //0x00E8
	uint16_t r_TargetTilePositionY2; //0x00EA
	uint32_t N0000012E; //0x00EC
	uint32_t r_PathingRelevant; //0x00F0
	uint16_t r_MovingRelevant; //0x00F4
	uint16_t p_CurrentPathPlanPosition; //0x00F6
	uint32_t p_PathPlanSize; //0x00F8
	uint32_t N00000132; //0x00FC
	uint32_t N00000073; //0x0100
	uint32_t N00000134; //0x0104
	uint32_t N00000074; //0x0108
	uint32_t N00000136; //0x010C
	uint32_t N00000075; //0x0110
	uint32_t N00000138; //0x0114
	uint32_t N00000076; //0x0118
	uint32_t N0000013A; //0x011C
	uint32_t N00000077; //0x0120
	uint32_t N0000013C; //0x0124
	uint32_t N00000078; //0x0128
	uint32_t N0000013E; //0x012C
	uint32_t N00000079; //0x0130
	uint32_t N00000140; //0x0134
	uint32_t N0000007A; //0x0138
	uint32_t N00000142; //0x013C
	uint32_t N0000007B; //0x0140
	uint32_t N00000144; //0x0144
	uint32_t N0000007C; //0x0148
	uint32_t N00000146; //0x014C
	uint32_t N0000007D; //0x0150
	uint32_t N00000148; //0x0154
	uint32_t N0000007E; //0x0158
	uint32_t N0000014A; //0x015C
	uint32_t N0000007F; //0x0160
	uint32_t N0000014C; //0x0164
	uint32_t N00000080; //0x0168
	uint32_t N0000014E; //0x016C
	uint32_t N00000081; //0x0170
	uint32_t N00000150; //0x0174
	uint32_t N00000082; //0x0178
	uint32_t N00000152; //0x017C
	uint32_t N00000083; //0x0180
	uint32_t N00000154; //0x0184
	uint32_t N00000084; //0x0188
	uint32_t N00000156; //0x018C
	uint32_t N00000085; //0x0190
	uint32_t N00000158; //0x0194
	uint32_t N00000086; //0x0198
	uint32_t N0000015A; //0x019C
	uint32_t N00000087; //0x01A0
	uint32_t N0000015C; //0x01A4
	uint32_t N00000088; //0x01A8
	uint32_t N0000015E; //0x01AC
	uint32_t N00000089; //0x01B0
	uint32_t N00000160; //0x01B4
	uint32_t N0000008A; //0x01B8
	uint32_t N00000162; //0x01BC
	uint32_t N0000008B; //0x01C0
	uint32_t N00000164; //0x01C4
	uint32_t N0000008C; //0x01C8
	uint32_t N00000166; //0x01CC
	uint32_t N0000008D; //0x01D0
	uint32_t N00000168; //0x01D4
	uint32_t N0000008E; //0x01D8
	uint32_t N0000016A; //0x01DC
	uint32_t N0000008F; //0x01E0
	uint32_t N0000016C; //0x01E4
	uint32_t N00000090; //0x01E8
	uint32_t N0000016E; //0x01EC
	uint32_t N00000091; //0x01F0
	uint32_t N00000170; //0x01F4
	uint32_t N00000092; //0x01F8
	uint32_t N00000172; //0x01FC
	uint32_t N00000093; //0x0200
	uint32_t N00000174; //0x0204
	uint32_t N00000094; //0x0208
	uint32_t N00000176; //0x020C
	uint32_t N00000095; //0x0210
	uint32_t N00000178; //0x0214
	uint32_t N00000096; //0x0218
	uint32_t N0000017A; //0x021C
	uint32_t N00000097; //0x0220
	uint32_t N0000017C; //0x0224
	uint32_t N00000098; //0x0228
	uint32_t N0000017E; //0x022C
	uint32_t N00000099; //0x0230
	uint32_t N00000180; //0x0234
	uint32_t N0000009A; //0x0238
	uint32_t N00000182; //0x023C
	uint32_t N0000009B; //0x0240
	uint32_t N00000184; //0x0244
	uint32_t N0000009C; //0x0248
	uint32_t N00000186; //0x024C
	uint32_t N0000009D; //0x0250
	uint32_t N00000188; //0x0254
	uint32_t N0000009E; //0x0258
	uint32_t N0000018A; //0x025C
	uint32_t N0000009F; //0x0260
	uint32_t N0000018C; //0x0264
	uint32_t N000000A0; //0x0268
	uint32_t N0000018E; //0x026C
	uint32_t N000000A1; //0x0270
	uint32_t N00000190; //0x0274
	uint32_t N000000A2; //0x0278
	uint32_t N00000192; //0x027C
	uint32_t N000000A3; //0x0280
	uint32_t N00000194; //0x0284
	uint32_t N000000A4; //0x0288
	uint32_t N00000196; //0x028C
	uint32_t N000000A5; //0x0290
	uint32_t N00000198; //0x0294
	uint32_t N000000A6; //0x0298
	uint16_t r_IsKilledByProjectile; //0x029C
	uint16_t N00008641; //0x029E
	uint16_t p_r_SelectionAllowed; //0x02A0
	uint16_t r_WorldDistanceToNearestEnemy; //0x02A2
	uint32_t N0000019C; //0x02A4
	uint32_t N000000A8; //0x02A8
	uint32_t N0000019E; //0x02AC
	uint32_t r_AutoTargetRelated; //0x02B0
	uint32_t N000001A0; //0x02B4
	uint16_t N000000AA; //0x02B8
	uint16_t r_SpeedBonus; //0x02BA
	uint32_t r_AIState; //0x02BC
	uint32_t N000000AB; //0x02C0
	int16_t r_TimeSinceDeathTicker; //0x02C4
	eChimps16 r_TransformIntoUnitOfType; //0x02C6
	uint32_t N000000AC; //0x02C8
	uint32_t N000001A6; //0x02CC
	uint16_t r_CurrentHealthPercentage; //0x02D0
	uint16_t r_TribeLeaderUnitId; //0x02D2
	uint16_t r_TribeId; //0x02D4
	uint16_t UnknownAIFlag; //0x02D6
	uint16_t r_AttackMoveToTargetTileX; //0x02D8
	uint16_t r_AttackMoveToTargetTileY; //0x02DA
	uint32_t N000001AA; //0x02DC
	uint32_t N000000AF; //0x02E0
	uint32_t N000001AC; //0x02E4
	uint32_t N000000B0; //0x02E8
	uint32_t N000001AE; //0x02EC
	uint32_t N000000B1; //0x02F0
	uint32_t N000001B0; //0x02F4
	uint32_t N000000B2; //0x02F8
	uint32_t N000001B2; //0x02FC
	uint16_t r_CarryOverGoodsAmount; //0x0300
	uint16_t r_CarryBonusYieldAmount; //0x0302
	uint32_t N000001B4; //0x0304
	uint32_t N000000B4; //0x0308
	uint32_t N000001B6; //0x030C
	uint32_t N000000B5; //0x0310
	uint32_t N000001B8; //0x0314
	uint32_t N000000B6; //0x0318
	uint32_t N000001BA; //0x031C
	uint32_t N000000B7; //0x0320
	uint32_t N000001BC; //0x0324
	uint16_t N000000B8; //0x0328
	uint16_t UnknownRelevant2; //0x032A
	uint32_t N000001BE; //0x032C
	uint32_t N000000B9; //0x0330
	uint16_t r_LinkedProductionBuildingId; //0x0334
	uint16_t N00008638; //0x0336
	uint16_t N000000BA; //0x0338
	uint16_t r_AttackingUnitId; //0x033A
	uint32_t N000001C2; //0x033C
	uint32_t r_RangedAttackTargetUnitId; //0x0340
	uint16_t N000001C4; //0x0344
	uint16_t r_CurrentSpeed2; //0x0346
	uint16_t r_CurrentSpeed; //0x0348
	uint16_t N00008635; //0x034A
	uint32_t r_AliveTicks1; //0x034C
	uint32_t r_AliveTicks2; //0x0350
	uint32_t r_SelectionRelevant3; //0x0354
	uint32_t N000000BE; //0x0358
	uint16_t N000001CA; //0x035C
	uint8_t r_StoneAmmoLeft; //0x035E
	uint8_t r_StoneAmmoStacksLeft; //0x035F
	uint32_t N000000BF; //0x0360
	uint32_t N000001CC; //0x0364
	uint32_t N000000C0; //0x0368
	uint8_t N000001CE; //0x036C
	uint8_t UnknownRelevant1; //0x036D
	uint16_t N0000026E; //0x036E
	uint32_t Unknown2; //0x0370
	uint32_t N000001D0; //0x0374
	uint32_t N000000C2; //0x0378
	uint32_t TimeUntilResting4thBit; //0x037C
	uint16_t TimeUntilResting; //0x0380
	uint16_t N0000026B; //0x0382
	uint16_t r_CarryGoodAmount; //0x0384
	uint16_t N0001184A; //0x0386
	uint32_t N000000C4; //0x0388
	uint32_t N000001D6; //0x038C
	uint32_t N000000C5; //0x0390
	uint32_t N000001D8; //0x0394
	uint16_t r_AI_LastIssuedTribeCommand; //0x0398
	uint16_t r_AI_ContextTargetUnitId; //0x039A
	uint32_t r_AI_ContextTargetUnitGlobalId; //0x039C
	uint32_t N000000C7; //0x03A0
	uint32_t r_AI_ContextTargetBuildingTileId; //0x03A4
	uint32_t N000000C8; //0x03A8
	uint32_t N000001DE; //0x03AC
	uint32_t N000000C9; //0x03B0
	uint32_t N000001E0; //0x03B4
	uint32_t N000000CA; //0x03B8
	uint32_t N000001E2; //0x03BC
	uint32_t N000000CB; //0x03C0
	uint32_t r_CurrentHealth; //0x03C4
	uint32_t r_MaxHealth; //0x03C8
	uint32_t N000001E6; //0x03CC
	uint32_t N000000CD; //0x03D0
	uint32_t r_ShootingSalvoLeft; //0x03D4
	uint32_t N000000CE; //0x03D8
	uint32_t N000001EA; //0x03DC
	uint32_t r_BlessedElapseTickTimer; //0x03E0
	uint16_t r_ContextTargetTileX; //0x03E4
	uint16_t r_ContextTargetTileY; //0x03E6
	uint16_t N000000D0; //0x03E8
	uint16_t r_UnknownAttackiterator; //0x03EA
	uint32_t N000001EE; //0x03EC
	uint32_t N000000D1; //0x03F0
	uint32_t N000001F0; //0x03F4
	uint16_t N000000D2; //0x03F8
	uint16_t r_AISiegeEngineRelatedMaybe; //0x03FA
	uint32_t N000001F2; //0x03FC
	uint32_t r_ContextCurrentPositionTileId; //0x0400
	uint32_t N000001F4; //0x0404
	uint32_t N000000D4; //0x0408
	uint32_t N000001F6; //0x040C
	uint32_t N000000D5; //0x0410
	uint32_t N000001F8; //0x0414
	uint32_t N000000D6; //0x0418
	uint32_t N000001FA; //0x041C
	uint32_t N000000D7; //0x0420
	uint16_t r_AITribeRoleRelatedUnknown; //0x0424
	AISiegeRole16	r_AITribeRole; //0x0426
	uint32_t N000000D8; //0x0428
	uint32_t N000001FE; //0x042C
	uint16_t N000000D9; //0x0430
	uint16_t r_FarmerAIRelatedUnknown; //0x0432
	uint32_t r_NearestEnemyWorldTiles; //0x0434
	uint32_t N000000DA; //0x0438
	uint32_t N00000202; //0x043C
	uint16_t r_TimeAlive; //0x0440
	uint16_t N00008AA0; //0x0442
	uint32_t N00000204; //0x0444
	uint32_t N000000DC; //0x0448
	uint32_t N00000206; //0x044C
	uint32_t N000000DD; //0x0450
	uint32_t N00000208; //0x0454
	uint16_t N000000DE; //0x0458
	uint16_t r_DemolisherShieldHealth; //0x045A
	uint32_t r_DemolisherShieldLastTakenDamageCooldown; //0x045C
	uint32_t N000000DF; //0x0460
	uint32_t N0000020C; //0x0464
	uint32_t N000000E3; //0x0468
	uint32_t N0000020E; //0x046C
	uint32_t N000000E4; //0x0470
	uint32_t N00000210; //0x0474
	uint32_t N000000E5; //0x0478
	uint32_t N00000212; //0x047C
	uint32_t N000000E6; //0x0480
	uint32_t N00000214; //0x0484
	uint32_t N000000E7; //0x0488
	uint32_t N00000216; //0x048C
}; //Size: 0x0490


typedef struct GameUnitManager
{
	uint32_t r_NextUnitId; //0x0000
	uint32_t r_TotalUnits; //0x0004
	uint32_t N000003DC; //0x0008
	uint32_t N00002C1F; //0x000C
	uint32_t N000003DD; //0x0010
	uint32_t N00002C21; //0x0014
	uint32_t N000003DE; //0x0018
	uint32_t N00002C23; //0x001C
	uint32_t r_HoveredChimpsCount; //0x0020
	uint32_t N00002C25; //0x0024
	uint32_t r_SelectedChimpsCount; //0x0028
	uint32_t N00002C27; //0x002C
	uint32_t N000003E1; //0x0030
	uint32_t N00002C29; //0x0034
	uint32_t N000003E2; //0x0038
	uint32_t N00002C2B; //0x003C
	uint32_t N000003E3; //0x0040
	uint32_t N00002C2D; //0x0044
	uint32_t N000003E4; //0x0048
	uint32_t N00002C2F; //0x004C
	uint32_t N000003E5; //0x0050
	uint32_t N00002C31; //0x0054
	uint32_t N000003E6; //0x0058
	uint32_t N00002C33; //0x005C
	uint32_t N000003E7; //0x0060
	uint32_t N00002C35; //0x0064
	uint32_t N000003E8; //0x0068
	uint32_t N00002C37; //0x006C
	uint32_t N000003E9; //0x0070
	uint32_t N00002C39; //0x0074
	uint32_t N000003EA; //0x0078
	uint32_t N00002C3B; //0x007C
	uint32_t N000003EB; //0x0080
	uint32_t N000091DA; //0x0084
	uint32_t N000003EC; //0x0088
	uint32_t N000091DD; //0x008C
	uint32_t N000003ED; //0x0090
	uint32_t N000091E0; //0x0094
	char pad_0098[1472]; //0x0098
	uint32_t EmptyUnitFillValue; //0x0658
	GameUnitData LastOrderedUnit; //0x065C
	GameUnitData GameUnitArray[10000]; //0x0AEC
}; //Size: 0x8F42C

/* Derived from engineinterface.h; C++ syntax normalized for Ghidra CParser. */
typedef struct LoadMapReturnData
{
    int32_t errorCode;
    int32_t mapSize;
    int32_t mapRotation;
    int32_t mapRotationCentreX;
    int32_t mapRotationCentreY;
    int32_t siege_or_invasion;
    int32_t multiplayerMap;
    int32_t multiplayerKOTHMap;
    int32_t game_type;
    int32_t mission_level;
    int32_t coopTrailID;
    int32_t coopMissionID;
    int32_t coopMissionAlly;
    int32_t textID;
    int32_t difficulty_level;
    int32_t playerID;
    int32_t skirmishTrail;
    int32_t skirmishTrailLevel;
    int32_t skirmishGameType;
    int32_t arabicLord;
    int16_t keep_positions0x;
    int16_t keep_positions0y;
    int16_t keep_positions1x;
    int16_t keep_positions1y;
    int16_t keep_positions2x;
    int16_t keep_positions2y;
    int16_t keep_positions3x;
    int16_t keep_positions3y;
    int16_t keep_positions4x;
    int16_t keep_positions4y;
    int16_t keep_positions5x;
    int16_t keep_positions5y;
    int16_t keep_positions6x;
    int16_t keep_positions6y;
    int16_t keep_positions7x;
    int16_t keep_positions7y;
    uint8_t start_keep_location_order0;
    uint8_t start_keep_location_order1;
    uint8_t start_keep_location_order2;
    uint8_t start_keep_location_order3;
    uint8_t start_keep_location_order4;
    uint8_t start_keep_location_order5;
    uint8_t start_keep_location_order6;
    uint8_t start_keep_location_order7;
    int32_t loadedVersion;
    uint8_t radar_colour_mapping0;
    uint8_t radar_colour_mapping1;
    uint8_t radar_colour_mapping2;
    uint8_t radar_colour_mapping3;
    uint8_t radar_colour_mapping4;
    uint8_t radar_colour_mapping5;
    uint8_t radar_colour_mapping6;
    uint8_t radar_colour_mapping7;
    uint8_t computer_register0;
    uint8_t computer_register1;
    uint8_t computer_register2;
    uint8_t computer_register3;
    uint8_t computer_register4;
    uint8_t computer_register5;
    uint8_t computer_register6;
    uint8_t computer_register7;
    uint8_t computer_name0;
    uint8_t computer_name1;
    uint8_t computer_name2;
    uint8_t computer_name3;
    uint8_t computer_name4;
    uint8_t computer_name5;
    uint8_t computer_name6;
    uint8_t computer_name7;
    uint8_t computer_extended_lords_names0;
    uint8_t computer_extended_lords_names1;
    uint8_t computer_extended_lords_names2;
    uint8_t computer_extended_lords_names3;
    uint8_t computer_extended_lords_names4;
    uint8_t computer_extended_lords_names5;
    uint8_t computer_extended_lords_names6;
    uint8_t computer_extended_lords_names7;
} LoadMapReturnData;

typedef struct MultiplayerSetupTransferData
{
    int32_t fairness;
    int32_t starting_gamespeed;
    int32_t starting_goods_level;
    int32_t win_condition;
    int32_t allow_autotrading;
    int32_t no_knockdown_walls;
    int32_t autosave;
    int32_t peacetime;
    int32_t no_cows;
    int32_t no_dogs;
    int32_t start_keep_location_order0;
    int32_t start_keep_location_order1;
    int32_t start_keep_location_order2;
    int32_t start_keep_location_order3;
    int32_t start_keep_location_order4;
    int32_t start_keep_location_order5;
    int32_t start_keep_location_order6;
    int32_t start_keep_location_order7;
    int32_t extreme_troops;
    int32_t extreme_powers;
    int32_t extreme_powers_around_lord;
    int32_t allow_outposts;
    int32_t advanced_options;
    int32_t advanced_skirmish_options;
    int32_t advopt_pre_build;
    int32_t advopt_improved_arabswordsmen;
    int32_t advopt_improved_laddermen;
    int32_t advopt_improved_spearmen;
    int32_t advopt_rebalanced_horsearchers;
    int32_t advopt_improved_fletchers;
    int32_t advopt_uncapped_peasants;
    int32_t advopt_faster_peasants;
    int32_t advopt_enemy_hps;
    int32_t global_improved_sieging;
    int32_t advopt_healers;
    int32_t advopt_eunuchs;
    int32_t advopt_nogold;
    int32_t MP_BuildingsAvailable0;
    int32_t MP_BuildingsAvailable1;
    int32_t MP_BuildingsAvailable2;
    int32_t MP_BuildingsAvailable3;
    int32_t MP_BuildingsAvailable4;
    int32_t MP_BuildingsAvailable5;
    int32_t MP_BuildingsAvailable6;
    int32_t MP_BuildingsAvailable7;
    int32_t MP_BuildingsAvailable8;
    int32_t MP_BuildingsAvailable9;
    int32_t MP_BuildingsAvailable10;
    int32_t MP_BuildingsAvailable11;
    int32_t MP_BuildingsAvailable12;
    int32_t MP_GoodsAvailable0;
    int32_t MP_GoodsAvailable1;
    int32_t MP_GoodsAvailable2;
    int32_t MP_GoodsAvailable3;
    int32_t MP_GoodsAvailable4;
    int32_t MP_GoodsAvailable5;
    int32_t MP_GoodsAvailable6;
    int32_t MP_GoodsAvailable7;
    int32_t MP_GoodsAvailable8;
    int32_t MP_GoodsAvailable9;
    int32_t MP_GoodsAvailable10;
    int32_t MP_GoodsAvailable11;
    int32_t MP_GoodsAvailable12;
    int32_t MP_GoodsAvailable13;
    int32_t MP_GoodsAvailable14;
    int32_t MP_GoodsAvailable15;
    int32_t MP_GoodsAvailable16;
    int32_t MP_GoodsAvailable17;
    int32_t MP_GoodsAvailable18;
    int32_t MP_GoodsAvailable19;
    int32_t MP_GoodsAvailable20;
    int32_t MP_GoodsAvailable21;
    int32_t MP_GoodsAvailable22;
    int32_t MP_GoodsAvailable23;
    int32_t MP_GoodsAvailable24;
    int32_t MP_TroopsAvailable0;
    int32_t MP_TroopsAvailable1;
    int32_t MP_TroopsAvailable2;
    int32_t MP_TroopsAvailable3;
    int32_t MP_TroopsAvailable4;
    int32_t MP_TroopsAvailable5;
    int32_t MP_TroopsAvailable6;
    int32_t MP_TroopsAvailable7;
    int32_t MP_TroopsAvailable8;
    int32_t MP_TroopsAvailable9;
    int32_t MP_TroopsAvailable10;
    int32_t MP_TroopsAvailable11;
    int32_t MP_TroopsAvailable12;
    int32_t MP_TroopsAvailable13;
    int32_t MP_TroopsAvailable14;
    int32_t MP_TroopsAvailable15;
    int32_t MP_TroopsAvailable16;
    int32_t MP_TroopsAvailable17;
    int32_t MP_TroopsAvailable18;
    int32_t MP_TroopsAvailable19;
    int32_t MP_TroopsAvailable20;
    int32_t MP_TroopsAvailable21;
    int32_t MP_TroopsAvailable22;
    int32_t MP_TroopsAvailable23;
    int32_t MP_TroopsAvailable24;
    int32_t MP_TroopsAvailable25;
    int32_t MP_TroopsAvailable26;
    int32_t MP_TroopsAvailable27;
    int32_t MP_TroopsAvailable28;
    int32_t MP_TroopsAvailable29;
    int32_t MP_TroopsAvailable30;
    int32_t MP_TroopsAvailable31;
    int32_t preferredAIVs0;
    int32_t preferredAIVs1;
    int32_t preferredAIVs2;
    int32_t preferredAIVs3;
    int32_t preferredAIVs4;
    int32_t preferredAIVs5;
    int32_t preferredAIVs6;
    int32_t preferredAIVs7;
    int32_t global_improved_sieging2;
} MultiplayerSetupTransferData;

typedef struct AILordConfigTransferData
{
    int32_t opponent_type;
    int32_t opponent_type_for_speech;
    int32_t lord_gfx_type;
    int32_t flag_type;
    int32_t use_of_religion;
    int32_t use_of_ale;
    int32_t vlow_popularity;
    int32_t low_popularity;
    int32_t high_popularity;
    int32_t min_tax;
    int32_t max_tax;
    int32_t farm_types1;
    int32_t farm_types2;
    int32_t farm_types3;
    int32_t farm_types4;
    int32_t farm_types5;
    int32_t farm_types6;
    int32_t farm_types7;
    int32_t farm_types8;
    int32_t people_to_farm_ratio;
    int32_t extract_wood_ratio;
    int32_t extract_stone_ratio;
    int32_t extract_iron_ratio;
    int32_t extract_pitch_ratio;
    int32_t max_quarries;
    int32_t max_mines;
    int32_t max_woodcutters;
    int32_t max_pitch_dugouts;
    int32_t max_farms;
    int32_t build_rate;
    int32_t crushed_building_delay;
    int32_t sell_food_at;
    int32_t buy_apples_at;
    int32_t buy_cheese_at;
    int32_t buy_bread_at;
    int32_t buy_wheat_at;
    int32_t buy_hops_at;
    int32_t buy_food_amount;
    int32_t buy_weapons;
    int32_t pester_for_goods_delay;
    int32_t send_goods_margin;
    int32_t ration_boost;
    int32_t trade_wood_at;
    int32_t trade_stone_at;
    int32_t trade_resources_at;
    int32_t trade_flour_at;
    int32_t trade_weapons_at;
    int32_t trade_ale_at;
    int32_t trade_pitch_at;
    int32_t trade_minimum;
    int32_t base_gold_reserves;
    int32_t blacksmiths_make;
    int32_t fletchers_make;
    int32_t poleturners_make;
    int32_t sell_all1;
    int32_t sell_all2;
    int32_t sell_all3;
    int32_t sell_all4;
    int32_t sell_all5;
    int32_t sell_all6;
    int32_t sell_all7;
    int32_t sell_all8;
    int32_t sell_all9;
    int32_t sell_all10;
    int32_t sell_all11;
    int32_t sell_all12;
    int32_t sell_all13;
    int32_t sell_all14;
    int32_t sell_all15;
    int32_t move_mobile_defenders;
    int32_t max_mobile_groups;
    int32_t buy_defense_machines_at;
    int32_t buy_defense_machines_delay;
    int32_t dog_release_timing;
    int32_t dog_points_count;
    int32_t chance_of_defensive1;
    int32_t chance_of_defensive2;
    int32_t chance_of_defensive3;
    int32_t chance_of_harrasment1;
    int32_t chance_of_harrasment2;
    int32_t chance_of_harrasment3;
    int32_t chance_of_seiging1;
    int32_t chance_of_seiging2;
    int32_t chance_of_seiging3;
    int32_t economy_protection_number;
    int32_t economy_protection_type;
    int32_t bodyguard_number;
    int32_t bodyguard_type;
    int32_t moat_diggers;
    int32_t moat_digger_type;
    int32_t troop_production_rate1;
    int32_t troop_production_rate2;
    int32_t troop_production_rate3;
    int32_t defense_patrol_trigger_level;
    int32_t defense_patrols;
    int32_t defense_patrol_style;
    int32_t defense_patrol_delay;
    int32_t defensive_trigger_level;
    int32_t defensive_troops1;
    int32_t defensive_troops2;
    int32_t defensive_troops3;
    int32_t defensive_troops4;
    int32_t defensive_troops5;
    int32_t defensive_troops6;
    int32_t defensive_troops7;
    int32_t defensive_troops8;
    int32_t harrasment_trigger_level;
    int32_t harrasment_trigger_variance;
    int32_t harrasment_troops1;
    int32_t harrasment_troops2;
    int32_t harrasment_troops3;
    int32_t harrasment_troops4;
    int32_t harrasment_troops5;
    int32_t harrasment_troops6;
    int32_t harrasment_troops7;
    int32_t harrasment_troops8;
    int32_t harrasment_machines1;
    int32_t harrasment_machines2;
    int32_t harrasment_machines3;
    int32_t harrasment_machines4;
    int32_t harrasment_machines5;
    int32_t harrasment_machines6;
    int32_t harrasment_machines7;
    int32_t harrasment_machines8;
    int32_t max_harrasment_machines;
    int32_t harrass_delay;
    int32_t siege_trigger_level;
    int32_t siege_trigger_variance;
    int32_t siege_troops_before_will_come_to_rescue;
    int32_t siege_troops_on_site_percent;
    int32_t siege_troops_at_home_percent;
    int32_t siege_soften_up_delay;
    int32_t siege_victory_delay;
    int32_t percent_chance_waiting_for_joint_attack;
    int32_t siege_machines1;
    int32_t siege_machines2;
    int32_t siege_machines3;
    int32_t siege_machines4;
    int32_t siege_machines5;
    int32_t siege_machines6;
    int32_t siege_machines7;
    int32_t siege_machines8;
    int32_t siege_cow_timer;
    int32_t siege_eng_amount;
    int32_t siege_moat_troop;
    int32_t siege_moat_amount;
    int32_t siege_herring_troop;
    int32_t siege_herring_amount;
    int32_t siege_assasin_amount;
    int32_t siege_ladder_amount;
    int32_t siege_tunnel_amount;
    int32_t siege_storm_troop;
    int32_t siege_storm_amount;
    int32_t siege_storm_tribes;
    int32_t siege_cover_troop;
    int32_t siege_cover_amount;
    int32_t siege_cover_tribes;
    int32_t siege_shock_troop;
    int32_t siege_shock_amount;
    int32_t siege_reserve_troop;
    int32_t siege_reserve_amount;
    int32_t siege_reserve_tribes;
    int32_t siege_wall_troops1;
    int32_t siege_wall_troops2;
    int32_t siege_wall_troops3;
    int32_t siege_wall_troops4;
    int32_t siege_wall_troops5;
    int32_t siege_wall_troops6;
    int32_t siege_wall_troops7;
    int32_t siege_wall_troops8;
    int32_t siege_wall_troops9;
    int32_t siege_wall_troops10;
    int32_t siege_wall_troops11;
    int32_t siege_wall_troops12;
    int32_t siege_wall_troops13;
    int32_t siege_wall_troops14;
    int32_t siege_wall_troops15;
    int32_t siege_wall_troops16;
    int32_t siege_wall_troops17;
    int32_t siege_wall_troops18;
    int32_t siege_wall_troops19;
    int32_t siege_wall_troops20;
    int32_t siege_wall_troops21;
    int32_t siege_wall_troops22;
    int32_t siege_wall_troops23;
    int32_t siege_wall_troops24;
    int32_t siege_wall_amount;
    int32_t siege_wall_tribes;
    int32_t who_to_pick_on;
    int32_t use_improved_sieging;
    int32_t starting_troops_normal1;
    int32_t starting_troops_normal2;
    int32_t starting_troops_normal3;
    int32_t starting_troops_normal4;
    int32_t starting_troops_normal5;
    int32_t starting_troops_normal6;
    int32_t starting_troops_normal7;
    int32_t starting_troops_normal8;
    int32_t starting_troops_normal9;
    int32_t starting_troops_normal10;
    int32_t starting_troops_normal11;
    int32_t starting_troops_normal12;
    int32_t starting_troops_normal13;
    int32_t starting_troops_normal14;
    int32_t starting_troops_normal15;
    int32_t starting_troops_normal16;
    int32_t starting_troops_normal17;
    int32_t starting_troops_normal18;
    int32_t starting_troops_normal19;
    int32_t starting_troops_normal20;
    int32_t starting_troops_normal21;
    int32_t starting_troops_normal22;
    int32_t starting_troops_normal23;
    int32_t starting_troops_normal24;
    int32_t starting_troops_normal25;
    int32_t starting_troops_normal26;
    int32_t starting_troops_normal27;
    int32_t starting_troops_normal28;
    int32_t starting_troops_deathmatch1;
    int32_t starting_troops_deathmatch2;
    int32_t starting_troops_deathmatch3;
    int32_t starting_troops_deathmatch4;
    int32_t starting_troops_deathmatch5;
    int32_t starting_troops_deathmatch6;
    int32_t starting_troops_deathmatch7;
    int32_t starting_troops_deathmatch8;
    int32_t starting_troops_deathmatch9;
    int32_t starting_troops_deathmatch10;
    int32_t starting_troops_deathmatch11;
    int32_t starting_troops_deathmatch12;
    int32_t starting_troops_deathmatch13;
    int32_t starting_troops_deathmatch14;
    int32_t starting_troops_deathmatch15;
    int32_t starting_troops_deathmatch16;
    int32_t starting_troops_deathmatch17;
    int32_t starting_troops_deathmatch18;
    int32_t starting_troops_deathmatch19;
    int32_t starting_troops_deathmatch20;
    int32_t starting_troops_deathmatch21;
    int32_t starting_troops_deathmatch22;
    int32_t starting_troops_deathmatch23;
    int32_t starting_troops_deathmatch24;
    int32_t starting_troops_deathmatch25;
    int32_t starting_troops_deathmatch26;
    int32_t starting_troops_deathmatch27;
    int32_t starting_troops_deathmatch28;
    int32_t starting_troops_crusader1;
    int32_t starting_troops_crusader2;
    int32_t starting_troops_crusader3;
    int32_t starting_troops_crusader4;
    int32_t starting_troops_crusader5;
    int32_t starting_troops_crusader6;
    int32_t starting_troops_crusader7;
    int32_t starting_troops_crusader8;
    int32_t starting_troops_crusader9;
    int32_t starting_troops_crusader10;
    int32_t starting_troops_crusader11;
    int32_t starting_troops_crusader12;
    int32_t starting_troops_crusader13;
    int32_t starting_troops_crusader14;
    int32_t starting_troops_crusader15;
    int32_t starting_troops_crusader16;
    int32_t starting_troops_crusader17;
    int32_t starting_troops_crusader18;
    int32_t starting_troops_crusader19;
    int32_t starting_troops_crusader20;
    int32_t starting_troops_crusader21;
    int32_t starting_troops_crusader22;
    int32_t starting_troops_crusader23;
    int32_t starting_troops_crusader24;
    int32_t starting_troops_crusader25;
    int32_t starting_troops_crusader26;
    int32_t starting_troops_crusader27;
    int32_t starting_troops_crusader28;
    int32_t lord_power_display_level;
    int32_t lord_hps_percent;
    int32_t extendedLordParent;
    int32_t siege_max_troops;
    int32_t siege_normal_wave_multiplier;
    int32_t siege_high_gold_wave_multiplier;
    int32_t free04;
    int32_t free05;
    int32_t free06;
    int32_t free07;
    int32_t free08;
    int32_t free09;
    int32_t free00;
    int32_t free11;
    int32_t free12;
    int32_t free13;
    int32_t free14;
    int32_t free15;
    int32_t free16;
    int32_t free17;
    int32_t free18;
    int32_t free19;
    int32_t free20;
    int32_t free21;
    int32_t free22;
    int32_t free23;
    int32_t free24;
    int32_t free25;
    int32_t free26;
    int32_t free27;
    int32_t free28;
    int32_t free29;
    int32_t free30;
    int32_t free31;
    int32_t free32;
    int32_t free33;
    int32_t free34;
    int32_t free35;
    int32_t free36;
    int32_t free37;
    int32_t free38;
    int32_t free39;
    int32_t free40;
    int32_t free41;
    int32_t free42;
    int32_t free43;
    int32_t free44;
    int32_t free45;
    int32_t free46;
    int32_t free47;
    int32_t free48;
    int32_t free49;
    int32_t free50;
    int32_t free51;
    int32_t free52;
    int32_t free53;
    int32_t free54;
    int32_t free55;
    int32_t free56;
    int32_t free57;
    int32_t free58;
    int32_t free59;
    int32_t free60;
    int32_t free61;
    int32_t free62;
    int32_t free63;
    int32_t free64;
    int32_t free65;
    int32_t free66;
    int32_t free67;
    int32_t free68;
    int32_t free69;
    int32_t free70;
    int32_t free71;
    int32_t free72;
    int32_t free73;
    int32_t free74;
    int32_t free75;
    int32_t free76;
    int32_t free77;
    int32_t free78;
    int32_t free79;
    int32_t free80;
    int32_t free81;
    int32_t free82;
    int32_t free83;
    int32_t free84;
    int32_t free85;
    int32_t free86;
    int32_t free87;
    int32_t free88;
    int32_t free89;
    int32_t free90;
    int32_t free91;
    int32_t free92;
    int32_t free93;
    int32_t free94;
    int32_t free95;
    int32_t free96;
    int32_t free97;
    int32_t free98;
    int32_t free99;
    int32_t free100;
} AILordConfigTransferData;

typedef struct evF
{
    int16_t value;
    uint8_t type;
    uint8_t onoff;
} evF;

typedef struct tl_eventF
{
    int32_t month;
    int32_t year;
    int32_t tl_type;
    int16_t done;
    int16_t pre_done;
    int32_t action_data;
    int32_t action;
    int16_t and_or;
    uint8_t repeat;
    uint8_t repeat_count;
} tl_eventF;

typedef struct tl_messageF
{
    int32_t month;
    int32_t year;
    int32_t tl_type;
    int16_t done;
    int16_t pre_done;
    int32_t message_id;
    int32_t action;
} tl_messageF;

typedef struct tl_invasionF
{
    int32_t month;
    int32_t year;
    int32_t tl_type;
    int16_t done;
    int16_t pre_done;
    int32_t total;
    int32_t invasion_point;
    int32_t start_year;
    int32_t repeat;
    int32_t from;
    int32_t markerID;
    int32_t FixedElementField;
} tl_invasionF;

typedef struct PlayStateReturnData
{
    int32_t numSelectedChimps;
    int32_t popularity;
    int32_t population;
    int32_t gold;
    int32_t housing_cap;
    int32_t upcoming_total_popularity;
    int32_t rationing_popularity;
    int32_t foodsEaten_popularity;
    int32_t food_popularity;
    int32_t tax_popularity;
    int32_t overcrowding_popularity;
    int32_t fearFactor_popularity;
    int32_t religion_popularity;
    int32_t fairs_popularity;
    int32_t plague_popularity;
    int32_t wolves_popularity;
    int32_t bandits_popularity;
    int32_t fire_popularity;
    int32_t marriage_popularity;
    int32_t jester_popularity;
    int32_t good_things;
    int32_t bad_things;
    int32_t fear_factor;
    int32_t fear_factor_next_level;
    int32_t efficiency;
    int16_t num_priests;
    int16_t blessed_percent;
    int16_t blessed_next_level_at;
    int32_t tax_rate;
    int16_t tax_amount;
    int16_t peasants_available_for_troops;
    int32_t rationing;
    int32_t food_clock;
    int32_t total_food;
    int32_t months_of_food;
    int32_t food_types_eaten;
    int32_t food_types_available;
    int32_t app_mode;
    int32_t app_sub_mode;
    int32_t debug_value1;
    int32_t game_time;
    int32_t in_structure;
    int32_t in_structure_type;
    int32_t completeSelectionBox;
    int32_t in_chimp;
    int32_t in_chimp_type;
    int16_t inchimp_name1;
    int16_t inchimp_name2;
    int16_t dog_cage_state;
    int16_t inchimp_n_text;
    int32_t in_chimp_goods;
    int32_t gatehouse_state;
    int16_t repairs_allowed;
    int16_t can_do_repairs;
    int16_t building_hps_for_repair;
    int16_t building_maxhps_for_repair;
    int16_t sleep_allowed;
    int16_t building_type_sleeping;
    int16_t have_building_stats;
    int16_t workers_have;
    int16_t job_vacancies;
    int16_t workers_needed;
    int16_t got_keep_access;
    int16_t turned_off;
    int16_t working;
    int16_t mill_message;
    int32_t pints_of_ale;
    int16_t barrels_of_ale;
    int16_t working_inns;
    int16_t total_inns;
    int16_t inn_coverage_percent;
    int16_t inn_coverage_popularity;
    int16_t inn_coverage_next;
    uint8_t troops_show_disband;
    uint8_t troops_show_build_menu;
    uint8_t troops_show_make_catapult;
    uint8_t troops_show_make_trebuchet;
    uint8_t troops_show_make_siege_tower;
    uint8_t troops_show_battering_ram;
    uint8_t troops_show_portable_shield;
    uint8_t troops_show_get_ammo;
    uint8_t troops_show_launch_cow_and_num_cows;
    uint8_t troops_show_attack_here_and_type;
    uint8_t troops_show_attack_here_number_rocks;
    uint8_t troops_show_stance;
    uint8_t troops_show_patrol;
    uint8_t troops_patrol_mode;
    uint8_t weapon_being_made_now;
    uint8_t game_type;
    uint8_t can_make_xbows;
    uint8_t can_make_sword;
    uint8_t can_make_pike;
    uint8_t weapon_being_made_next;
    uint8_t production_no_resources;
    uint8_t playerdesc_message;
    uint8_t playerdesc_message2;
    int16_t marry_status;
    int16_t marry_male_type;
    int16_t marry_female_type;
    int16_t marry_text;
    int16_t marry_m_name1;
    int16_t marry_m_name2;
    int16_t marry_f_name1;
    int16_t marry_f_name2;
    int16_t blessed_popularity;
    uint8_t church_adjustment;
    uint8_t church_missing;
    int16_t scribe_frame;
    int16_t total_horses_available;
    int32_t action_point_count;
    int16_t camera_target_x;
    int16_t camera_target_y;
    int16_t camera_target_z;
    int16_t rotateHappened;
    int16_t trading_current_goods;
    int16_t trading_next_goods;
    int16_t trading_prev_goods;
    int16_t force_app_mode;
    int16_t month;
    int16_t year;
    int16_t pop_months;
    int16_t chimp_comments;
    int16_t camera_target_flat;
    int16_t skirmish_map_num_keeps;
    int16_t inbuilding_help_id;
    int16_t MP_Ahead_By;
    int16_t MP_Behind_By;
    int16_t SkipFrame;
    int16_t undoAvailable;
    int16_t chimps_count;
    int16_t chimps_limit;
    int16_t structs_count;
    int16_t structs_limit;
    int16_t orgs_count;
    int16_t orgs_limit;
    int16_t minerals_count;
    int16_t minerals_limit;
    int16_t tribes_count;
    int16_t tribes_limit;
    uint8_t freeWoodcutter;
    uint8_t freeGranary;
    uint8_t gotSignpost;
    int32_t repair_wood_needed;
    int32_t repair_stone_needed;
    int16_t panel_text_group;
    int16_t panel_text_text;
    uint8_t free_buildingCheat;
    uint8_t editor_time_paused;
    int16_t bld_tiles_built;
    uint8_t game_paused;
    uint8_t numMPChatEntries;
    int16_t ai_clock;
    uint8_t lordOnlySelected;
    uint8_t gotMarket;
    uint8_t keep_enclosed;
    uint8_t can_make_bows;
    uint8_t can_make_mace;
    uint8_t can_make_spear;
    uint8_t messageFrom;
    uint8_t troops_show_make_arab_ballista;
    uint8_t starting_goods_level;
    uint8_t fairness;
    uint32_t elapsedTime;
    uint8_t balanced;
    uint8_t extremeEnabled;
    int16_t extremeCount;
    uint8_t mouse_selector_state;
    uint8_t flattenedHappened;
    uint8_t skirmishInsultFrom;
    uint8_t skirmishInsult;
    uint8_t lord_Type;
    uint8_t monk_available;
    uint8_t engineer_available;
    uint8_t ladderman_available;
    uint8_t team_shield1;
    uint8_t team_shield2;
    uint8_t team_shield3;
    uint8_t team_shield4;
    uint8_t team_shield5;
    uint8_t team_shield6;
    uint8_t team_shield7;
    uint8_t team_shield8;
    uint8_t resyncPercent;
    uint8_t messageFromcharacter;
    int16_t debug_value2;
    uint8_t laddermanCost;
    uint8_t eunuchCost;
    uint8_t spectatorMode;
    uint8_t customisedExtremeTrail;
    int32_t FixedElementField;
} PlayStateReturnData;

typedef struct ScoreReturnData
{
    int32_t score_weapons;
    int32_t score_weapons_points;
    int32_t score;
    int32_t levelPoints;
    int32_t score_months;
    int32_t score_months_points;
    int32_t items_count;
    int32_t items_extra1;
    int32_t items_extra2;
    int32_t items_extra3;
    int32_t items_extra4;
    int32_t items_extra5;
    int32_t items_extra6;
    int32_t items_extra7;
    int32_t items_extra_points1;
    int32_t items_extra_points2;
    int32_t items_extra_points3;
    int32_t items_extra_points4;
    int32_t items_extra_points5;
    int32_t items_extra_points6;
    int32_t items_extra_points7;
    int32_t items_extra_type1;
    int32_t items_extra_type2;
    int32_t items_extra_type3;
    int32_t items_extra_type4;
    int32_t items_extra_type5;
    int32_t items_extra_type6;
    int32_t items_extra_type7;
    int32_t score_troops;
    int32_t troops_percent_lost;
    int32_t siege_that_score;
    int32_t siege_defenders_score;
    int32_t siege_attackers_score;
    int32_t difficulty_level;
} ScoreReturnData;

typedef struct multiplayer_stats_export
{
    int32_t real_time;
    int32_t game_time;
    int32_t ranged_made;
    int32_t melee_made;
    uint64_t unique;
    int32_t FixedElementField;
} multiplayer_stats_export;

typedef struct LogicDebugInfo
{
    int32_t gfx_layer;
    int32_t gfx_layer_file;
    int32_t gfx_layer_id;
    int32_t alpha_gfx_layer;
    int32_t construction_gfx_layer;
    int32_t pillar_gfx_layer;
    int32_t pillar_gfx_layer_file;
    int32_t pillar_gfx_layer_id;
    int32_t wall_gfx_layer;
    int32_t wall_gfx_layer_file;
    int32_t wall_gfx_layer_id;
    int32_t floating_layer;
    int32_t random_layer;
    int32_t logic_layer;
    int32_t logic2_layer;
    int32_t changed_layer;
    int32_t organism_layer;
    int32_t structure_layer;
    int32_t structure_was_layer;
    int32_t chimp_layer;
    int32_t fly_layer;
    int32_t height_layer;
    int32_t default_height_layer;
    int32_t wall_owner_layer;
    int32_t luminesence_layer;
    int32_t show_hi_layer;
    int32_t misc_display_layer;
    int32_t damage_layer;
    int32_t macro_layer;
    int32_t path_connection_layer;
    int32_t path_linkage_layer;
    int32_t occupancy_layer;
    int32_t certain_path_layer;
    int32_t walk_layer;
    int32_t ai_zone_layer;
    int32_t ai_info_layer;
    int32_t ai_danger_layer;
    int32_t ai_proximity_layer;
    int32_t town_dz_spread_id;
    int32_t town_null_connects;
    int32_t town_dz_spread_count;
    int32_t town_stone_value;
    int32_t town_structure;
    int32_t town_oasis;
    int32_t town_farm;
    int32_t town_iron;
    int32_t problem_build;
    int32_t aiv_block_zone;
    int32_t delay_layer;
    int32_t aiv_block_layer;
    int32_t mapOfset;
} LogicDebugInfo;



/* Derived from InternalAIC.h; C++ syntax normalized for Ghidra CParser. */
// Created with ReClass.NET 1.2 by KN4CK3R

typedef struct InternalAIC
{
	int32_t opponent_type; //0x0000
	int32_t opponent_type_for_speech; //0x0004
	int32_t lord_gfx_type; //0x0008
	int32_t flag_type; //0x000C
	int32_t use_of_religion; //0x0010
	int32_t use_of_ale; //0x0014
	int32_t vlow_popularity; //0x0018
	int32_t low_popularity; //0x001C
	int32_t high_popularity; //0x0020
	int32_t min_tax; //0x0024
	int32_t max_tax; //0x0028
	int32_t farm_types1; //0x002C
	int32_t farm_types2; //0x0030
	int32_t farm_types3; //0x0034
	int32_t farm_types4; //0x0038
	int32_t farm_types5; //0x003C
	int32_t farm_types6; //0x0040
	int32_t farm_types7; //0x0044
	int32_t farm_types8; //0x0048
	int32_t people_to_farm_ratio; //0x004C
	int32_t extract_wood_ratio; //0x0050
	int32_t extract_stone_ratio; //0x0054
	int32_t extract_iron_ratio; //0x0058
	int32_t extract_pitch_ratio; //0x005C
	int32_t max_quarries; //0x0060
	int32_t max_mines; //0x0064
	int32_t max_woodcutters; //0x0068
	int32_t max_pitch_dugouts; //0x006C
	int32_t max_farms; //0x0070
	int32_t build_rate; //0x0074
	int32_t crushed_building_delay; //0x0078
	int32_t sell_food_at; //0x007C
	int32_t buy_apples_at; //0x0080
	int32_t buy_cheese_at; //0x0084
	int32_t buy_bread_at; //0x0088
	int32_t buy_wheat_at; //0x008C
	int32_t buy_hops_at; //0x0090
	int32_t buy_food_amount; //0x0094
	int32_t buy_weapons; //0x0098
	int32_t pester_for_goods_delay; //0x009C
	int32_t send_goods_margin; //0x00A0
	int32_t ration_boost; //0x00A4
	int32_t trade_wood_at; //0x00A8
	int32_t trade_stone_at; //0x00AC
	int32_t trade_resources_at; //0x00B0
	int32_t trade_flour_at; //0x00B4
	int32_t trade_weapons_at; //0x00B8
	int32_t trade_ale_at; //0x00BC
	int32_t trade_pitch_at; //0x00C0
	int32_t trade_minimum; //0x00C4
	int32_t base_gold_reserves; //0x00C8
	int32_t blacksmiths_make; //0x00CC
	int32_t fletchers_make; //0x00D0
	int32_t poleturners_make; //0x00D4
	int32_t sell_all1; //0x00D8
	int32_t sell_all2; //0x00DC
	int32_t sell_all3; //0x00E0
	int32_t sell_all4; //0x00E4
	int32_t sell_all5; //0x00E8
	int32_t sell_all6; //0x00EC
	int32_t sell_all7; //0x00F0
	int32_t sell_all8; //0x00F4
	int32_t sell_all9; //0x00F8
	int32_t sell_all10; //0x00FC
	int32_t sell_all11; //0x0100
	int32_t sell_all12; //0x0104
	int32_t sell_all13; //0x0108
	int32_t sell_all14; //0x010C
	int32_t sell_all15; //0x0110
	int32_t move_mobile_defenders; //0x0114
	int32_t max_mobile_groups; //0x0118
	int32_t buy_defense_machines_at; //0x011C
	int32_t buy_defense_machines_delay; //0x0120
	int32_t dog_release_timing; //0x0124
	int32_t dog_points_count; //0x0128
	int32_t chance_of_defensive1; //0x012C
	int32_t chance_of_defensive2; //0x0130
	int32_t chance_of_defensive3; //0x0134
	int32_t chance_of_harrasment1; //0x0138
	int32_t chance_of_harrasment2; //0x013C
	int32_t chance_of_harrasment3; //0x0140
	int32_t chance_of_seiging1; //0x0144
	int32_t chance_of_seiging2; //0x0148
	int32_t chance_of_seiging3; //0x014C
	int32_t economy_protection_number; //0x0150
	int32_t economy_protection_type; //0x0154
	int32_t bodyguard_number; //0x0158
	int32_t bodyguard_type; //0x015C
	int32_t moat_diggers; //0x0160
	int32_t moat_digger_type; //0x0164
	int32_t troop_production_rate1; //0x0168
	int32_t troop_production_rate2; //0x016C
	int32_t troop_production_rate3; //0x0170
	int32_t defense_patrol_trigger_level; //0x0174
	int32_t defense_patrols; //0x0178
	int32_t defense_patrol_style; //0x017C
	int32_t defense_patrol_delay; //0x0180
	int32_t defensive_trigger_level; //0x0184
	int32_t defensive_troops1; //0x0188
	int32_t defensive_troops2; //0x018C
	int32_t defensive_troops3; //0x0190
	int32_t defensive_troops4; //0x0194
	int32_t defensive_troops5; //0x0198
	int32_t defensive_troops6; //0x019C
	int32_t defensive_troops7; //0x01A0
	int32_t defensive_troops8; //0x01A4
	int32_t harrasment_trigger_level; //0x01A8
	int32_t harrasment_trigger_variance; //0x01AC
	int32_t harrasment_troops1; //0x01B0
	int32_t harrasment_troops2; //0x01B4
	int32_t harrasment_troops3; //0x01B8
	int32_t harrasment_troops4; //0x01BC
	int32_t harrasment_troops5; //0x01C0
	int32_t harrasment_troops6; //0x01C4
	int32_t harrasment_troops7; //0x01C8
	int32_t harrasment_troops8; //0x01CC
	int32_t harrasment_machines1; //0x01D0
	int32_t harrasment_machines2; //0x01D4
	int32_t harrasment_machines3; //0x01D8
	int32_t harrasment_machines4; //0x01DC
	int32_t harrasment_machines5; //0x01E0
	int32_t harrasment_machines6; //0x01E4
	int32_t harrasment_machines7; //0x01E8
	int32_t harrasment_machines8; //0x01EC
	int32_t max_harrasment_machines; //0x01F0
	int32_t harrass_delay; //0x01F4
	int32_t siege_trigger_level; //0x01F8
	int32_t siege_trigger_variance; //0x01FC
	int32_t siege_troops_before_will_come_to_rescue; //0x0200
	int32_t siege_troops_on_site_percent; //0x0204
	int32_t siege_troops_at_home_percent; //0x0208
	int32_t siege_soften_up_delay; //0x020C
	int32_t siege_victory_delay; //0x0210
	int32_t percent_chance_waiting_for_joint_attack; //0x0214
	int32_t siege_machines1; //0x0218
	int32_t siege_machines2; //0x021C
	int32_t siege_machines3; //0x0220
	int32_t siege_machines4; //0x0224
	int32_t siege_machines5; //0x0228
	int32_t siege_machines6; //0x022C
	int32_t siege_machines7; //0x0230
	int32_t siege_machines8; //0x0234
	int32_t siege_cow_timer; //0x0238
	int32_t siege_eng_amount; //0x023C
	int32_t siege_moat_troop; //0x0240
	int32_t siege_moat_amount; //0x0244
	int32_t siege_herring_troop; //0x0248
	int32_t siege_herring_amount; //0x024C
	int32_t siege_assasin_amount; //0x0250
	int32_t siege_ladder_amount; //0x0254
	int32_t siege_tunnel_amount; //0x0258
	int32_t siege_storm_troop; //0x025C
	int32_t siege_storm_amount; //0x0260
	int32_t siege_storm_tribes; //0x0264
	int32_t siege_cover_troop; //0x0268
	int32_t siege_cover_amount; //0x026C
	int32_t siege_cover_tribes; //0x0270
	int32_t siege_shock_troop; //0x0274
	int32_t siege_shock_amount; //0x0278
	int32_t siege_reserve_troop; //0x027C
	int32_t siege_reserve_amount; //0x0280
	int32_t siege_reserve_tribes; //0x0284
	int32_t siege_wall_troops1; //0x0288
	int32_t siege_wall_troops2; //0x028C
	int32_t siege_wall_troops3; //0x0290
	int32_t siege_wall_troops4; //0x0294
	int32_t siege_wall_troops5; //0x0298
	int32_t siege_wall_troops6; //0x029C
	int32_t siege_wall_troops7; //0x02A0
	int32_t siege_wall_troops8; //0x02A4
	int32_t siege_wall_troops9; //0x02A8
	int32_t siege_wall_troops10; //0x02AC
	int32_t siege_wall_troops11; //0x02B0
	int32_t siege_wall_troops12; //0x02B4
	int32_t siege_wall_troops13; //0x02B8
	int32_t siege_wall_troops14; //0x02BC
	int32_t siege_wall_troops15; //0x02C0
	int32_t siege_wall_troops16; //0x02C4
	int32_t siege_wall_troops17; //0x02C8
	int32_t siege_wall_troops18; //0x02CC
	int32_t siege_wall_troops19; //0x02D0
	int32_t siege_wall_troops20; //0x02D4
	int32_t siege_wall_troops21; //0x02D8
	int32_t siege_wall_troops22; //0x02DC
	int32_t siege_wall_troops23; //0x02E0
	int32_t siege_wall_troops24; //0x02E4
	int32_t siege_wall_amount; //0x02E8
	int32_t siege_wall_tribes; //0x02EC
	int32_t who_to_pick_on; //0x02F0
	int32_t use_improved_sieging; //0x02F4
	int32_t starting_troops_normal1; //0x02F8
	int32_t starting_troops_normal2; //0x02FC
	int32_t starting_troops_normal3; //0x0300
	int32_t starting_troops_normal4; //0x0304
	int32_t starting_troops_normal5; //0x0308
	int32_t starting_troops_normal6; //0x030C
	int32_t starting_troops_normal7; //0x0310
	int32_t starting_troops_normal8; //0x0314
	int32_t starting_troops_normal9; //0x0318
	int32_t starting_troops_normal10; //0x031C
	int32_t starting_troops_normal11; //0x0320
	int32_t starting_troops_normal12; //0x0324
	int32_t starting_troops_normal13; //0x0328
	int32_t starting_troops_normal14; //0x032C
	int32_t starting_troops_normal15; //0x0330
	int32_t starting_troops_normal16; //0x0334
	int32_t starting_troops_normal17; //0x0338
	int32_t starting_troops_normal18; //0x033C
	int32_t starting_troops_normal19; //0x0340
	int32_t starting_troops_normal20; //0x0344
	int32_t starting_troops_normal21; //0x0348
	int32_t starting_troops_normal22; //0x034C
	int32_t starting_troops_normal23; //0x0350
	int32_t starting_troops_normal24; //0x0354
	int32_t starting_troops_normal25; //0x0358
	int32_t starting_troops_normal26; //0x035C
	int32_t starting_troops_normal27; //0x0360
	int32_t starting_troops_normal28; //0x0364
	int32_t starting_troops_deathmatch1; //0x0368
	int32_t starting_troops_deathmatch2; //0x036C
	int32_t starting_troops_deathmatch3; //0x0370
	int32_t starting_troops_deathmatch4; //0x0374
	int32_t starting_troops_deathmatch5; //0x0378
	int32_t starting_troops_deathmatch6; //0x037C
	int32_t starting_troops_deathmatch7; //0x0380
	int32_t starting_troops_deathmatch8; //0x0384
	int32_t starting_troops_deathmatch9; //0x0388
	int32_t starting_troops_deathmatch10; //0x038C
	int32_t starting_troops_deathmatch11; //0x0390
	int32_t starting_troops_deathmatch12; //0x0394
	int32_t starting_troops_deathmatch13; //0x0398
	int32_t starting_troops_deathmatch14; //0x039C
	int32_t starting_troops_deathmatch15; //0x03A0
	int32_t starting_troops_deathmatch16; //0x03A4
	int32_t starting_troops_deathmatch17; //0x03A8
	int32_t starting_troops_deathmatch18; //0x03AC
	int32_t starting_troops_deathmatch19; //0x03B0
	int32_t starting_troops_deathmatch20; //0x03B4
	int32_t starting_troops_deathmatch21; //0x03B8
	int32_t starting_troops_deathmatch22; //0x03BC
	int32_t starting_troops_deathmatch23; //0x03C0
	int32_t starting_troops_deathmatch24; //0x03C4
	int32_t starting_troops_deathmatch25; //0x03C8
	int32_t starting_troops_deathmatch26; //0x03CC
	int32_t starting_troops_deathmatch27; //0x03D0
	int32_t starting_troops_deathmatch28; //0x03D4
	int32_t starting_troops_crusader1; //0x03D8
	int32_t starting_troops_crusader2; //0x03DC
	int32_t starting_troops_crusader3; //0x03E0
	int32_t starting_troops_crusader4; //0x03E4
	int32_t starting_troops_crusader5; //0x03E8
	int32_t starting_troops_crusader6; //0x03EC
	int32_t starting_troops_crusader7; //0x03F0
	int32_t starting_troops_crusader8; //0x03F4
	int32_t starting_troops_crusader9; //0x03F8
	int32_t starting_troops_crusader10; //0x03FC
	int32_t starting_troops_crusader11; //0x0400
	int32_t starting_troops_crusader12; //0x0404
	int32_t starting_troops_crusader13; //0x0408
	int32_t starting_troops_crusader14; //0x040C
	int32_t starting_troops_crusader15; //0x0410
	int32_t starting_troops_crusader16; //0x0414
	int32_t starting_troops_crusader17; //0x0418
	int32_t starting_troops_crusader18; //0x041C
	int32_t starting_troops_crusader19; //0x0420
	int32_t starting_troops_crusader20; //0x0424
	int32_t starting_troops_crusader21; //0x0428
	int32_t starting_troops_crusader22; //0x042C
	int32_t starting_troops_crusader23; //0x0430
	int32_t starting_troops_crusader24; //0x0434
	int32_t starting_troops_crusader25; //0x0438
	int32_t starting_troops_crusader26; //0x043C
	int32_t starting_troops_crusader27; //0x0440
	int32_t starting_troops_crusader28; //0x0444
	int32_t lord_power_display_level; //0x0448
	int32_t lord_hps_percent; //0x044C
	int32_t extendedLordParent; //0x0450
	int32_t siege_max_troops; //0x0454
	int32_t siege_normal_wave_multiplier; //0x0458
	int32_t siege_high_gold_wave_multiplier; //0x045C
	int32_t free04; //0x0460
	int32_t free05; //0x0464
	int32_t free06; //0x0468
	int32_t free07; //0x046C
	int32_t free08; //0x0470
	int32_t free09; //0x0474
	int32_t free00; //0x0478
	int32_t free11; //0x047C
	int32_t free12; //0x0480
	int32_t free13; //0x0484
	int32_t free14; //0x0488
	int32_t free15; //0x048C
	int32_t free16; //0x0490
	int32_t free17; //0x0494
	int32_t free18; //0x0498
	int32_t free19; //0x049C
	int32_t free20; //0x04A0
	int32_t free21; //0x04A4
	int32_t free22; //0x04A8
	int32_t free23; //0x04AC
	int32_t free24; //0x04B0
	int32_t free25; //0x04B4
	int32_t free26; //0x04B8
	int32_t free27; //0x04BC
	int32_t free28; //0x04C0
	int32_t free29; //0x04C4
	int32_t free30; //0x04C8
	int32_t free31; //0x04CC
	int32_t free32; //0x04D0
	int32_t free33; //0x04D4
	int32_t free34; //0x04D8
	int32_t free35; //0x04DC
	int32_t free36; //0x04E0
	int32_t free37; //0x04E4
	int32_t free38; //0x04E8
	int32_t free39; //0x04EC
	int32_t free40; //0x04F0
	int32_t free41; //0x04F4
	int32_t free42; //0x04F8
	int32_t free43; //0x04FC
	int32_t free44; //0x0500
	int32_t free45; //0x0504
	int32_t free46; //0x0508
	int32_t free47; //0x050C
	int32_t free48; //0x0510
	int32_t free49; //0x0514
	int32_t free50; //0x0518
	int32_t free51; //0x051C
	int32_t free52; //0x0520
	int32_t free53; //0x0524
	int32_t free54; //0x0528
	int32_t free55; //0x052C
	int32_t free56; //0x0530
	int32_t free57; //0x0534
	int32_t free58; //0x0538
	int32_t free59; //0x053C
	int32_t free60; //0x0540
	int32_t free61; //0x0544
	int32_t free62; //0x0548
	int32_t free63; //0x054C
	int32_t free64; //0x0550
	int32_t free65; //0x0554
	int32_t free66; //0x0558
	int32_t free67; //0x055C
	int32_t free68; //0x0560
	int32_t free69; //0x0564
	int32_t free70; //0x0568
	int32_t free71; //0x056C
	int32_t free72; //0x0570
	int32_t free73; //0x0574
	int32_t free74; //0x0578
	int32_t free75; //0x057C
	int32_t free76; //0x0580
	int32_t free77; //0x0584
	int32_t free78; //0x0588
	int32_t free79; //0x058C
	int32_t free80; //0x0590
	int32_t free81; //0x0594
	int32_t free82; //0x0598
	int32_t free83; //0x059C
	int32_t free84; //0x05A0
	int32_t free85; //0x05A4
	int32_t free86; //0x05A8
	int32_t free87; //0x05AC
	int32_t free88; //0x05B0
	int32_t free89; //0x05B4
	int32_t free90; //0x05B8
	int32_t free91; //0x05BC
	int32_t free92; //0x05C0
	int32_t free93; //0x05C4
	int32_t free94; //0x05C8
	int32_t free95; //0x05CC
	int32_t free96; //0x05D0
	int32_t free97; //0x05D4
	int32_t free98; //0x05D8
	int32_t free99; //0x05DC
	int32_t free100; //0x05E0
}; //Size: 0x05E4

/* Derived from MessageManager.h; C++ syntax normalized for Ghidra CParser. */
typedef struct MessageManager
{
	uint32_t IsQueueActive; //0x0000
	uint32_t ImmediateCommandId; //0x0004
	uint32_t ImmediateMessageType; //0x0008
	char pad_000C[200]; //0x000C
	uint32_t ImmediatePlayerId; //0x00D4
	char pad_00D8[4]; //0x00D8
	uint32_t QueueCommandIds[10]; //0x00DC
	uint32_t QueueMessageTypes[10]; //0x0104
	uint32_t QueueFlags[10]; //0x012C
	char QueueVideoPaths[10][100]; //0x0154
	char QueueAudioPaths[10][100]; //0x053C
	uint32_t QueuePlayerIds[10]; //0x0924
	uint32_t CurrentQueueCount; //0x094C
	char pad_0950[904]; //0x0950
}; //Size: 0x0CD8

/* Derived from PlayerResources.h; C++ syntax normalized for Ghidra CParser. */
typedef struct PlayerResources
{
	uint32_t N000039F7; //0x0000
	uint32_t N000044FF; //0x0004
	uint32_t N000039F8; //0x0008
	uint32_t N00004501; //0x000C
	uint32_t N000039F9; //0x0010
	uint32_t N00004503; //0x0014
	uint32_t r_RationsMode2; //0x0018
	uint32_t N00004505; //0x001C
	uint32_t N000039FB; //0x0020
	uint32_t N00004507; //0x0024
	uint32_t N000039FC; //0x0028
	uint32_t N00004509; //0x002C
	uint16_t N000039FD; //0x0030
	uint16_t N000047DF; //0x0032
	uint32_t N0000450B; //0x0034
	uint32_t N000039FE; //0x0038
	uint32_t N0000450D; //0x003C
	uint16_t Unknown2; //0x0040
	uint16_t N0002B783; //0x0042
	uint32_t N0000450F; //0x0044
	uint32_t N00003A00; //0x0048
	uint32_t N00004511; //0x004C
	uint16_t N00003A01; //0x0050
	uint16_t N000047DA; //0x0052
	int32_t N00004513; //0x0054
	uint32_t N00003A02; //0x0058
	uint32_t N00004515; //0x005C
	uint32_t r_CurrentPopularity; //0x0060
	uint32_t N00004517; //0x0064
	uint32_t r_VacantWorkBuildings; //0x0068
	uint32_t r_PeasantSpawnProgressRequired; //0x006C
	int32_t r_PeasantSpawnProgress; //0x0070
	uint32_t r_CivilianHousingSpace; //0x0074
	uint32_t r_TotalPeasants; //0x0078
	uint32_t N0000451D; //0x007C
	uint32_t N00003A07; //0x0080
	uint32_t r_TotalCivilians; //0x0084
	uint32_t r_ReadyPeasants; //0x0088
	uint32_t r_ExistingPeasants; //0x008C
	uint32_t N00003A09; //0x0090
	uint32_t r_KeepId; //0x0094
	uint32_t r_KeepTilePositionX; //0x0098
	uint32_t r_KeepTilePositionY; //0x009C
	uint32_t r_KeepTileId; //0x00A0
	uint32_t N00004527; //0x00A4
	uint32_t N00003A0C; //0x00A8
	uint32_t N00004529; //0x00AC
	uint32_t N00003A0D; //0x00B0
	uint32_t N0000452B; //0x00B4
	uint32_t N00003A0E; //0x00B8
	uint32_t r_FirstGoodsyardId; //0x00BC
	uint32_t r_FirstGoodsyardTilePositionY; //0x00C0
	uint32_t r_FirstGoodsyardTilePositionX; //0x00C4
	uint32_t r_FirstGoodsyardTileId; //0x00C8
	uint32_t N00004531; //0x00CC
	uint32_t N00003A11; //0x00D0
	uint32_t N00004533; //0x00D4
	uint32_t N00003A12; //0x00D8
	uint32_t N00004535; //0x00DC
	uint32_t N00003A13; //0x00E0
	uint32_t r_FirstGranaryId; //0x00E4
	uint32_t r_FirstGranaryTilePositionY; //0x00E8
	uint32_t r_FirstGranaryTilePositionX; //0x00EC
	uint32_t r_FirstGranaryTileId; //0x00F0
	uint32_t N0000453B; //0x00F4
	uint32_t N00003A16; //0x00F8
	uint32_t N0000453D; //0x00FC
	uint32_t N00003A17; //0x0100
	uint32_t N0000453F; //0x0104
	uint32_t N00003A18; //0x0108
	uint32_t r_FirstArmouryId; //0x010C
	uint32_t r_FirstArmouryTilePositionX; //0x0110
	uint32_t r_FirstArmouryTilePositionY; //0x0114
	uint32_t r_FirstArmouryTileId; //0x0118
	uint32_t N00004545; //0x011C
	uint32_t N00003A1B; //0x0120
	uint32_t N00004547; //0x0124
	uint32_t N00003A1C; //0x0128
	uint32_t N00004549; //0x012C
	uint32_t N00003A1D; //0x0130
	uint32_t N0000454B; //0x0134
	uint32_t N00003A1E; //0x0138
	uint32_t N0000454D; //0x013C
	uint32_t N00003A1F; //0x0140
	uint32_t N0000454F; //0x0144
	uint32_t N00003A20; //0x0148
	uint32_t N00004551; //0x014C
	uint32_t N00003A21; //0x0150
	uint32_t N00004553; //0x0154
	uint32_t N00003A22; //0x0158
	uint32_t r_EuroMercPostId; //0x015C
	uint32_t r_EuroMercPostTilePositionX; //0x0160
	uint32_t r_EuroMercPostTilePositionY; //0x0164
	uint32_t r_EuroMercPostTileId; //0x0168
	uint32_t N00004559; //0x016C
	uint32_t N00003A25; //0x0170
	uint32_t N0000455B; //0x0174
	uint32_t N00003A26; //0x0178
	uint32_t N0000455D; //0x017C
	uint32_t N00003A27; //0x0180
	uint32_t N0000455F; //0x0184
	uint32_t N00003A28; //0x0188
	uint32_t N00004561; //0x018C
	uint32_t N00003A29; //0x0190
	uint32_t N00004563; //0x0194
	uint32_t N00003A2A; //0x0198
	uint32_t N00004565; //0x019C
	uint32_t N00003A2B; //0x01A0
	uint32_t N00004567; //0x01A4
	uint32_t N00003A2C; //0x01A8
	uint32_t r_TradePostId; //0x01AC
	uint32_t r_TradePostTilePositionX; //0x01B0
	uint32_t r_TradePostTilePositionY; //0x01B4
	uint32_t r_TradePostTileId; //0x01B8
	uint32_t N0000456D; //0x01BC
	uint32_t N00003A2F; //0x01C0
	uint32_t N0000456F; //0x01C4
	uint32_t N00003A30; //0x01C8
	uint32_t N00004571; //0x01CC
	uint32_t N00003A31; //0x01D0
	uint32_t r_KeepDoorId; //0x01D4
	uint32_t r_KeepDoorTilePositionX; //0x01D8
	uint32_t r_KeepDoorTilePositionY; //0x01DC
	uint32_t r_KeepDoorTileId; //0x01E0
	uint32_t N00004577; //0x01E4
	uint32_t N00003A34; //0x01E8
	uint32_t N00004579; //0x01EC
	uint32_t N00003A35; //0x01F0
	uint32_t N0000457B; //0x01F4
	uint32_t N00003A36; //0x01F8
	uint32_t r_EngineersGuildId; //0x01FC
	uint32_t r_EngineersGuildTilePositionX; //0x0200
	uint32_t r_EngineersGuildTilePositionY; //0x0204
	uint32_t r_EngineersGuildTileId; //0x0208
	uint32_t N00004581; //0x020C
	uint32_t N00003A39; //0x0210
	uint32_t N00004583; //0x0214
	uint32_t N00003A3A; //0x0218
	uint32_t N00004585; //0x021C
	uint32_t N00003A3B; //0x0220
	uint32_t r_TunnelersGuildId; //0x0224
	uint32_t r_TunnelersGuildTilePositionX; //0x0228
	uint32_t r_TunnelersGuildTilePositionY; //0x022C
	uint32_t r_TunnelersGuildTileId; //0x0230
	uint32_t N0000458B; //0x0234
	uint32_t N00003A3E; //0x0238
	uint32_t N0000458D; //0x023C
	uint32_t N00003A3F; //0x0240
	uint32_t N0000458F; //0x0244
	uint32_t N00003A40; //0x0248
	uint32_t r_ArabMercPostId; //0x024C
	uint32_t r_ArabMercPostTilePositionX; //0x0250
	uint32_t r_ArabMercPostTilePositionY; //0x0254
	uint32_t r_ArabMercPostTileId; //0x0258
	uint32_t N00004595; //0x025C
	uint32_t N00003A43; //0x0260
	uint32_t N00004597; //0x0264
	uint32_t N00003A44; //0x0268
	uint32_t N00004599; //0x026C
	uint32_t N00003A45; //0x0270
	uint32_t r_LastBuildOilSmelterId; //0x0274
	uint32_t r_LastBuildOilSmelterTilePositionX; //0x0278
	uint32_t r_LastBuildOilSmelterTilePositionY; //0x027C
	uint32_t r_LastBuildOilSmelterTileId; //0x0280
	uint32_t N0000459F; //0x0284
	uint32_t N00003A48; //0x0288
	uint32_t N000045A1; //0x028C
	uint32_t N00003A49; //0x0290
	uint32_t N000045A3; //0x0294
	uint32_t N00003A4A; //0x0298
	uint32_t r_BedouinMercPostId; //0x029C
	uint32_t r_BedouinMercPostTilePositionX; //0x02A0
	uint32_t r_BedouinMercPostTilePositionY; //0x02A4
	uint32_t r_BedouinMercPostTileId; //0x02A8
	uint32_t N000045A9; //0x02AC
	uint32_t N00003A4D; //0x02B0
	uint32_t N000045AB; //0x02B4
	uint32_t N00003A4E; //0x02B8
	uint32_t N000045AD; //0x02BC
	uint32_t N00003A4F; //0x02C0
	uint32_t N000045AF; //0x02C4
	uint32_t N00003A50; //0x02C8
	uint32_t N000045B1; //0x02CC
	uint32_t N00003A51; //0x02D0
	uint32_t N000045B3; //0x02D4
	uint32_t N00003A52; //0x02D8
	uint32_t N000045B5; //0x02DC
	uint32_t N00003A53; //0x02E0
	uint32_t N000045B7; //0x02E4
	uint32_t N00003A54; //0x02E8
	uint32_t N000045B9; //0x02EC
	uint32_t N00003A55; //0x02F0
	uint32_t N000045BB; //0x02F4
	uint32_t N00003A56; //0x02F8
	uint32_t N000045BD; //0x02FC
	uint32_t N00003A57; //0x0300
	uint32_t N000045BF; //0x0304
	uint32_t N00003A58; //0x0308
	uint32_t N000045C1; //0x030C
	uint32_t N00003A59; //0x0310
	uint32_t N000045C3; //0x0314
	uint32_t N00003A5A; //0x0318
	uint32_t N000045C5; //0x031C
	uint32_t N00003A5B; //0x0320
	uint32_t N000045C7; //0x0324
	uint32_t N00003A5C; //0x0328
	uint32_t N000045C9; //0x032C
	uint32_t N00003A5D; //0x0330
	uint32_t N000045CB; //0x0334
	uint32_t N00003A5E; //0x0338
	uint32_t N000045CD; //0x033C
	uint32_t N00003A5F; //0x0340
	uint32_t N000045CF; //0x0344
	uint32_t N00003A60; //0x0348
	uint32_t N000045D1; //0x034C
	uint32_t N00003A61; //0x0350
	uint32_t N000045D3; //0x0354
	uint32_t N00003A62; //0x0358
	uint32_t N000045D5; //0x035C
	uint32_t N00003A63; //0x0360
	uint32_t N000045D7; //0x0364
	uint32_t N00003A64; //0x0368
	uint32_t N000045D9; //0x036C
	uint32_t N00003A65; //0x0370
	uint32_t N000045DB; //0x0374
	uint32_t N00003A66; //0x0378
	uint32_t N000045DD; //0x037C
	uint32_t N00003A67; //0x0380
	uint32_t N000045DF; //0x0384
	uint32_t N00003A68; //0x0388
	uint32_t N000045E1; //0x038C
	uint32_t N00003A69; //0x0390
	uint32_t N000045E3; //0x0394
	uint32_t N00003A6A; //0x0398
	uint32_t N000045E5; //0x039C
	uint32_t N00003A6B; //0x03A0
	uint32_t N000045E7; //0x03A4
	uint32_t N00003A6C; //0x03A8
	uint32_t N000045E9; //0x03AC
	uint32_t N00003A6D; //0x03B0
	uint32_t r_PreviousPopularity; //0x03B4
	uint32_t r_TotalArmy; //0x03B8
	uint32_t N000045ED; //0x03BC
	uint32_t N00003A6F; //0x03C0
	uint32_t N000045EF; //0x03C4
	uint32_t N00003A70; //0x03C8
	uint32_t N000045F1; //0x03CC
	uint32_t N00003A71; //0x03D0
	uint32_t N000045F3; //0x03D4
	uint32_t N00003A72; //0x03D8
	uint32_t N000045F5; //0x03DC
	uint32_t N00003A73; //0x03E0
	uint32_t N000045F7; //0x03E4
	uint32_t N00003A74; //0x03E8
	uint32_t N000045F9; //0x03EC
	uint32_t N00003A75; //0x03F0
	uint32_t N000045FB; //0x03F4
	uint32_t N00003A76; //0x03F8
	uint32_t N000045FD; //0x03FC
	uint32_t N00003A77; //0x0400
	uint32_t N000045FF; //0x0404
	uint32_t N00003A78; //0x0408
	uint32_t N00004601; //0x040C
	uint32_t N00003A79; //0x0410
	uint32_t N00004603; //0x0414
	uint32_t N00003A7A; //0x0418
	uint32_t N00004605; //0x041C
	uint32_t N00003A7B; //0x0420
	uint32_t N00004607; //0x0424
	uint32_t N00003A7C; //0x0428
	uint32_t N00004609; //0x042C
	uint32_t N00003A7D; //0x0430
	uint32_t N0000460B; //0x0434
	uint32_t N00003A7E; //0x0438
	uint32_t N0000460D; //0x043C
	uint32_t N00003A7F; //0x0440
	uint32_t N0000460F; //0x0444
	uint32_t p_GoldPreviousMonth; //0x0448
	uint32_t N00004611; //0x044C
	uint32_t N00003A81; //0x0450
	uint32_t N00004613; //0x0454
	uint32_t N00003A82; //0x0458
	uint32_t N00004615; //0x045C
	uint32_t N00003A83; //0x0460
	uint32_t N00004617; //0x0464
	uint32_t N00003A84; //0x0468
	uint32_t r_incomingNull; //0x046C
	uint32_t r_incomingWoodLogs; //0x0470
	uint32_t r_incomingWoodPlanks; //0x0474
	uint32_t r_incomingRawHops; //0x0478
	uint32_t r_incomingStoneBlocks; //0x047C
	uint32_t r_incomingCowHides; //0x0480
	uint32_t r_incomingIronIngots; //0x0484
	uint32_t r_incomingPitchRaw; //0x0488
	uint32_t r_incomingPitchRefined; //0x048C
	uint32_t r_incomingRawWheat; //0x0490
	uint32_t r_incomingFoodBread; //0x0494
	uint32_t r_incomingFoodCheese; //0x0498
	uint32_t r_incomingFoodMeat; //0x049C
	uint32_t r_incomingFoodFruit; //0x04A0
	uint32_t r_incomingFoodAle; //0x04A4
	uint32_t r_incomingGold; //0x04A8
	uint32_t r_incomingFlour; //0x04AC
	uint32_t r_incomingBows; //0x04B0
	uint32_t r_incomingCrossbows; //0x04B4
	uint32_t r_incomingSpears; //0x04B8
	uint32_t r_incomingPikes; //0x04BC
	uint32_t r_incomingMaces; //0x04C0
	uint32_t r_incomingSwords; //0x04C4
	uint32_t r_incomingLeatherArmour; //0x04C8
	uint32_t r_incomingMetalArmour; //0x04CC
	uint32_t r_TotalGoodsNull; //0x04D0
	uint32_t r_TotalGoodsWoodLogs; //0x04D4
	uint32_t r_TotalGoodsWoodPlanks; //0x04D8
	uint32_t r_TotalGoodsRawHops; //0x04DC
	uint32_t r_TotalGoodsStoneBlocks; //0x04E0
	uint32_t r_TotalGoodsCowHides; //0x04E4
	uint32_t r_TotalGoodsIronIngots; //0x04E8
	uint32_t r_TotalGoodsPitchRaw; //0x04EC
	uint32_t r_TotalGoodsPitchRefined; //0x04F0
	uint32_t r_TotalGoodsRawWheat; //0x04F4
	uint32_t r_TotalGoodsFoodBread; //0x04F8
	uint32_t r_TotalGoodsFoodCheese; //0x04FC
	uint32_t r_TotalGoodsFoodMeat; //0x0500
	uint32_t r_TotalGoodsFoodFruit; //0x0504
	uint32_t r_TotalGoodsFoodAle; //0x0508
	uint32_t r_TotalGoodsGold; //0x050C
	uint32_t r_TotalGoodsFlour; //0x0510
	uint32_t r_TotalGoodsBows; //0x0514
	uint32_t r_TotalGoodsCrossbows; //0x0518
	uint32_t r_TotalGoodsSpears; //0x051C
	uint32_t r_TotalGoodsPikes; //0x0520
	uint32_t r_TotalGoodsMaces; //0x0524
	uint32_t r_TotalGoodsSwords; //0x0528
	uint32_t r_TotalGoodsLeatherArmour; //0x052C
	uint32_t r_TotalGoodsMetalArmour; //0x0530
	uint32_t N0000464B; //0x0534
	uint32_t N00003A9E; //0x0538
	uint32_t N0000464D; //0x053C
	uint32_t N00003A9F; //0x0540
	uint32_t N0000464F; //0x0544
	uint32_t N00003AA0; //0x0548
	uint32_t N00004651; //0x054C
	uint32_t N00003AA1; //0x0550
	uint32_t N00004653; //0x0554
	uint32_t N00003AA2; //0x0558
	uint32_t N00004655; //0x055C
	uint32_t N00003AA3; //0x0560
	uint32_t N00004657; //0x0564
	uint32_t N00003AA4; //0x0568
	uint32_t N00004659; //0x056C
	uint32_t N00003AA5; //0x0570
	uint32_t N0000465B; //0x0574
	uint32_t N00003AA6; //0x0578
	uint32_t N0000465D; //0x057C
	uint32_t N00003AA7; //0x0580
	uint32_t N0000465F; //0x0584
	uint32_t N00003AA8; //0x0588
	uint32_t N00004661; //0x058C
	uint32_t N00003AA9; //0x0590
	uint32_t N00004663; //0x0594
	uint32_t N00003AAA; //0x0598
	uint32_t N00004665; //0x059C
	uint32_t N00003AAB; //0x05A0
	uint32_t N00004667; //0x05A4
	uint32_t N00003AAC; //0x05A8
	uint32_t N00004669; //0x05AC
	uint32_t N00003AAD; //0x05B0
	uint32_t N0000466B; //0x05B4
	uint32_t N00003AAE; //0x05B8
	uint32_t N0000466D; //0x05BC
	uint32_t N00003AAF; //0x05C0
	uint32_t N0000466F; //0x05C4
	uint32_t N00003AB0; //0x05C8
	uint32_t N00004671; //0x05CC
	uint32_t N00003AB1; //0x05D0
	uint32_t N00004673; //0x05D4
	uint32_t N00003AB2; //0x05D8
	uint32_t N00004675; //0x05DC
	uint32_t N00003AB3; //0x05E0
	uint32_t N00004677; //0x05E4
	uint32_t N00003AB4; //0x05E8
	uint32_t N00004679; //0x05EC
	uint32_t N00003AB5; //0x05F0
	uint32_t N0000467B; //0x05F4
	uint32_t N00003AB6; //0x05F8
	uint32_t N0000467D; //0x05FC
	uint32_t N00003AB7; //0x0600
	uint32_t N0000467F; //0x0604
	uint32_t N00003AB8; //0x0608
	uint32_t N00004681; //0x060C
	uint32_t N00003AB9; //0x0610
	uint32_t N00004683; //0x0614
	uint32_t N00003ABA; //0x0618
	uint32_t N00004685; //0x061C
	uint32_t N00003ABB; //0x0620
	uint32_t N00004687; //0x0624
	uint32_t N00003ABC; //0x0628
	uint32_t N00004689; //0x062C
	uint32_t N00003ABD; //0x0630
	uint32_t N0000468B; //0x0634
	uint32_t N00003ABE; //0x0638
	uint32_t N0000468D; //0x063C
	uint32_t N00003ABF; //0x0640
	uint32_t N0000468F; //0x0644
	uint32_t N00003AC0; //0x0648
	uint32_t N00004691; //0x064C
	uint32_t N00003AC1; //0x0650
	uint32_t N00004693; //0x0654
	uint32_t N00003AC2; //0x0658
	uint32_t N00004695; //0x065C
	uint32_t N00003AC3; //0x0660
	uint32_t N00004697; //0x0664
	uint32_t N00003AC4; //0x0668
	uint32_t N00004699; //0x066C
	uint32_t N00003AC5; //0x0670
	uint32_t N0000469B; //0x0674
	uint32_t N00003AC6; //0x0678
	uint32_t N0000469D; //0x067C
	uint32_t N00003AC7; //0x0680
	uint32_t N0000469F; //0x0684
	uint32_t N00003AC8; //0x0688
	uint32_t N000046A1; //0x068C
	uint32_t N00003AC9; //0x0690
	uint32_t N000046A3; //0x0694
	uint32_t N00003ACA; //0x0698
	uint32_t N000046A5; //0x069C
	uint32_t N00003ACB; //0x06A0
	uint32_t N000046A7; //0x06A4
	uint32_t N00003ACC; //0x06A8
	uint32_t N000046A9; //0x06AC
	uint32_t N00003ACD; //0x06B0
	uint32_t N000046AB; //0x06B4
	uint32_t N00003ACE; //0x06B8
	uint32_t N000046AD; //0x06BC
	uint32_t N00003ACF; //0x06C0
	uint32_t N000046AF; //0x06C4
	uint32_t N00003AD0; //0x06C8
	uint32_t N000046B1; //0x06CC
	uint32_t N00003AD1; //0x06D0
	uint32_t N000046B3; //0x06D4
	uint32_t N00003AD2; //0x06D8
	uint32_t N000046B5; //0x06DC
	uint32_t N00003AD3; //0x06E0
	uint32_t N000046B7; //0x06E4
	uint32_t N00003AD4; //0x06E8
	uint32_t N000046B9; //0x06EC
	uint32_t N00003AD5; //0x06F0
	uint32_t N000046BB; //0x06F4
	uint32_t N00003AD6; //0x06F8
	uint32_t N000046BD; //0x06FC
	uint32_t N00003AD7; //0x0700
	uint32_t N000046BF; //0x0704
	uint32_t N00003AD8; //0x0708
	uint32_t N000046C1; //0x070C
	uint32_t N00003AD9; //0x0710
	uint32_t N000046C3; //0x0714
	uint32_t N00003ADA; //0x0718
	uint32_t N000046C5; //0x071C
	uint32_t N00003ADB; //0x0720
	uint32_t N000046C7; //0x0724
	uint32_t N00003ADC; //0x0728
	uint32_t N000046C9; //0x072C
	uint32_t N00003ADD; //0x0730
	uint32_t N000046CB; //0x0734
	uint32_t N00003ADE; //0x0738
	uint32_t N000046CD; //0x073C
	uint32_t N00003ADF; //0x0740
	uint32_t N000046CF; //0x0744
	uint32_t N00003AE0; //0x0748
	uint32_t N000046D1; //0x074C
	uint32_t N00003AE1; //0x0750
	uint32_t N000046D3; //0x0754
	uint32_t N00003AE2; //0x0758
	uint32_t N000046D5; //0x075C
	uint32_t N00003AE3; //0x0760
	uint32_t N000046D7; //0x0764
	uint32_t N00003AE4; //0x0768
	uint32_t N000046D9; //0x076C
	uint32_t N00003AE5; //0x0770
	uint32_t N000046DB; //0x0774
	uint32_t N00003AE6; //0x0778
	uint32_t N000046DD; //0x077C
	uint32_t N00003AE7; //0x0780
	uint32_t N000046DF; //0x0784
	uint32_t N00003AE8; //0x0788
	uint32_t N000046E1; //0x078C
	uint32_t N00003AE9; //0x0790
	uint32_t N000046E3; //0x0794
	uint32_t N00003AEA; //0x0798
	uint32_t N000046E5; //0x079C
	uint32_t N00003AEB; //0x07A0
	uint32_t N000046E7; //0x07A4
	uint32_t N00003AEC; //0x07A8
	uint32_t N000046E9; //0x07AC
	uint32_t N00003AED; //0x07B0
	uint32_t N000046EB; //0x07B4
	uint32_t N00003AEE; //0x07B8
	uint32_t N000046ED; //0x07BC
	uint32_t N00003AEF; //0x07C0
	uint32_t N000046EF; //0x07C4
	uint32_t N00003AF0; //0x07C8
	uint32_t N000046F1; //0x07CC
	uint32_t N00003AF1; //0x07D0
	uint32_t N000046F3; //0x07D4
	uint32_t N00003AF2; //0x07D8
	uint32_t N000046F5; //0x07DC
	uint32_t N00003AF3; //0x07E0
	uint32_t N000046F7; //0x07E4
	uint32_t N00003AF4; //0x07E8
	uint32_t N000046F9; //0x07EC
	uint32_t N00003AF5; //0x07F0
	uint32_t N000046FB; //0x07F4
	uint32_t N00003AF6; //0x07F8
	uint32_t N000046FD; //0x07FC
	uint32_t N00003AF7; //0x0800
	uint32_t N000046FF; //0x0804
	uint32_t N00003AF8; //0x0808
	uint32_t N00004701; //0x080C
	uint32_t N00003AF9; //0x0810
	uint32_t N00004703; //0x0814
	uint32_t N00003AFA; //0x0818
	uint32_t N00004705; //0x081C
	uint32_t N00003AFB; //0x0820
	uint32_t N00004707; //0x0824
	uint32_t N00003AFC; //0x0828
	uint32_t N00004709; //0x082C
	uint32_t N00003AFD; //0x0830
	uint32_t N0000470B; //0x0834
	uint32_t N00003AFE; //0x0838
	uint32_t N0000470D; //0x083C
	uint32_t N00003AFF; //0x0840
	uint32_t N0000470F; //0x0844
	uint32_t N00003B00; //0x0848
	uint32_t N00004711; //0x084C
	uint32_t N00003B01; //0x0850
	uint32_t N00004713; //0x0854
	uint32_t N00003B02; //0x0858
	uint32_t N00004715; //0x085C
	uint32_t N00003B03; //0x0860
	uint32_t N00004717; //0x0864
	uint32_t N00003B04; //0x0868
	uint32_t N00004719; //0x086C
	uint32_t N00003B05; //0x0870
	uint32_t N0000471B; //0x0874
	uint32_t N00003B06; //0x0878
	uint32_t N0000471D; //0x087C
	uint32_t N00003B07; //0x0880
	uint32_t N0000471F; //0x0884
	uint32_t N00003B08; //0x0888
	uint32_t N00004721; //0x088C
	uint32_t N00003B09; //0x0890
	uint32_t N00004723; //0x0894
	uint32_t N00003B0A; //0x0898
	uint32_t N00004725; //0x089C
	uint32_t N00003B0B; //0x08A0
	uint32_t N00004727; //0x08A4
	uint32_t N00003B0C; //0x08A8
	uint32_t N00004729; //0x08AC
	uint32_t N00003B0D; //0x08B0
	uint32_t N0000472B; //0x08B4
	uint32_t N00003B0E; //0x08B8
	uint32_t N0000472D; //0x08BC
	uint32_t N00003B0F; //0x08C0
	uint32_t N0000472F; //0x08C4
	uint32_t N00003B10; //0x08C8
	uint32_t N00004731; //0x08CC
	uint32_t N00003B11; //0x08D0
	uint32_t N00004733; //0x08D4
	uint32_t N00003B12; //0x08D8
	uint32_t N00004735; //0x08DC
	uint32_t N00003B13; //0x08E0
	uint32_t N00004737; //0x08E4
	uint32_t N00003B14; //0x08E8
	uint32_t N00004739; //0x08EC
	uint32_t N00003B15; //0x08F0
	uint32_t N0000473B; //0x08F4
	uint32_t N00003B16; //0x08F8
	uint32_t N0000473D; //0x08FC
	uint32_t N00003B17; //0x0900
	uint32_t N0000473F; //0x0904
	uint32_t N00003B18; //0x0908
	uint32_t N00004741; //0x090C
	uint32_t N00003B19; //0x0910
	uint32_t N00004743; //0x0914
	uint32_t N00003B1A; //0x0918
	uint32_t N00004745; //0x091C
	uint32_t N00003B1B; //0x0920
	uint32_t N00004747; //0x0924
	uint32_t N00003B1C; //0x0928
	uint32_t N00004749; //0x092C
	uint32_t N00003B1D; //0x0930
	uint32_t N0000474B; //0x0934
	uint32_t N00003B1E; //0x0938
	uint32_t N0000474D; //0x093C
	uint32_t N00003B1F; //0x0940
	uint32_t N0000474F; //0x0944
	uint32_t N00003B20; //0x0948
	uint32_t N00004751; //0x094C
	uint32_t N00003B21; //0x0950
	uint32_t N00004753; //0x0954
	uint32_t N00003B22; //0x0958
	uint32_t N00004755; //0x095C
	uint32_t N00003B23; //0x0960
	uint32_t N00004757; //0x0964
	uint32_t N00003B24; //0x0968
	uint32_t N00004759; //0x096C
	uint32_t N00003B25; //0x0970
	uint32_t N0000475B; //0x0974
	uint32_t N00003B26; //0x0978
	uint32_t N0000475D; //0x097C
	uint32_t N00003B27; //0x0980
	uint32_t N0000475F; //0x0984
	uint32_t N00003B28; //0x0988
	uint32_t N00004761; //0x098C
	uint32_t N00003B29; //0x0990
	uint32_t N00004763; //0x0994
	uint32_t N00003B2A; //0x0998
	uint32_t N00004765; //0x099C
	uint32_t N00003B2B; //0x09A0
	uint32_t N00004767; //0x09A4
	uint32_t N00003B2C; //0x09A8
	uint32_t N00004769; //0x09AC
	uint32_t N00003B2D; //0x09B0
	uint32_t N0000476B; //0x09B4
	uint32_t N00003B2E; //0x09B8
	uint32_t N0000476D; //0x09BC
	uint32_t N00003B2F; //0x09C0
	uint32_t N0000476F; //0x09C4
	uint32_t N00003B30; //0x09C8
	uint32_t N00004771; //0x09CC
	uint32_t N00003B31; //0x09D0
	uint32_t N00004773; //0x09D4
	uint32_t N00003B32; //0x09D8
	uint32_t N00004775; //0x09DC
	uint32_t N00003B33; //0x09E0
	uint32_t N00004777; //0x09E4
	uint32_t N00003B34; //0x09E8
	uint32_t N00004779; //0x09EC
	uint32_t N00003B35; //0x09F0
	uint32_t N0000477B; //0x09F4
	uint32_t N00003B36; //0x09F8
	uint32_t N0000477D; //0x09FC
	uint32_t N00003B37; //0x0A00
	uint32_t N0000477F; //0x0A04
	uint32_t N00003B38; //0x0A08
	uint32_t N00004781; //0x0A0C
	uint32_t N00003B39; //0x0A10
	uint32_t N00004783; //0x0A14
	uint32_t N00003B3A; //0x0A18
	uint32_t N00004785; //0x0A1C
	uint32_t N00003B3B; //0x0A20
	uint32_t N00004787; //0x0A24
	uint32_t N00003B3C; //0x0A28
	uint32_t N00004789; //0x0A2C
	uint32_t N00003B3D; //0x0A30
	uint32_t N0000478B; //0x0A34
	uint32_t N00003B3E; //0x0A38
	uint32_t N0000478D; //0x0A3C
	uint32_t N00003B3F; //0x0A40
	uint32_t N0000478F; //0x0A44
	uint32_t N00003B40; //0x0A48
	uint32_t N00004791; //0x0A4C
	uint32_t N00003B41; //0x0A50
	uint32_t N00004793; //0x0A54
	uint32_t N00003B42; //0x0A58
	uint32_t N00004795; //0x0A5C
	uint32_t N00003B43; //0x0A60
	uint32_t N00004797; //0x0A64
	uint32_t N00003B44; //0x0A68
	uint32_t N00004799; //0x0A6C
	uint32_t N00003B45; //0x0A70
	uint32_t N0000479B; //0x0A74
	uint32_t N00003B46; //0x0A78
	uint32_t N0000479D; //0x0A7C
	uint32_t N00003B47; //0x0A80
	uint32_t N0000479F; //0x0A84
	uint32_t N00003B48; //0x0A88
	uint32_t N000047A1; //0x0A8C
	uint32_t N00003B49; //0x0A90
	uint32_t N000047A3; //0x0A94
	uint32_t N00003B4A; //0x0A98
	uint32_t N000047A5; //0x0A9C
	uint32_t N00003B4B; //0x0AA0
	uint32_t N000047A7; //0x0AA4
	uint32_t N00003B4C; //0x0AA8
	uint32_t N000047A9; //0x0AAC
	uint32_t N00003B4D; //0x0AB0
	uint32_t N000047AB; //0x0AB4
	uint32_t N00003B4E; //0x0AB8
	uint32_t N000047AD; //0x0ABC
	uint32_t N00003B4F; //0x0AC0
	uint32_t N000047AF; //0x0AC4
	uint32_t N00003B50; //0x0AC8
	uint32_t N000047B1; //0x0ACC
	uint32_t N00003B51; //0x0AD0
	uint32_t N000047B3; //0x0AD4
	uint32_t N00003B52; //0x0AD8
	uint32_t N000047B5; //0x0ADC
	uint32_t N00003B53; //0x0AE0
	uint32_t N000047B7; //0x0AE4
	uint32_t N00003B54; //0x0AE8
	uint32_t N000047B9; //0x0AEC
	uint32_t N00003B55; //0x0AF0
	uint32_t N000047BB; //0x0AF4
	uint32_t N00003B56; //0x0AF8
	uint32_t N000047BD; //0x0AFC
	uint32_t N00003B57; //0x0B00
	uint32_t N000047BF; //0x0B04
	uint32_t N00003B58; //0x0B08
	uint32_t N000047C1; //0x0B0C
	uint32_t N00003B59; //0x0B10
	uint32_t N000047C3; //0x0B14
	uint32_t N00003B5A; //0x0B18
	uint32_t N000047C5; //0x0B1C
	uint32_t N00003B5B; //0x0B20
	uint32_t N000047C7; //0x0B24
	uint32_t N00003B5C; //0x0B28
	uint32_t N000047C9; //0x0B2C
	uint32_t N00003B5D; //0x0B30
	uint32_t N000047CB; //0x0B34
	uint32_t N00003B5E; //0x0B38
	uint32_t N000047CD; //0x0B3C
	uint32_t N00003B5F; //0x0B40
	uint32_t N000047CF; //0x0B44
	uint32_t N00003B60; //0x0B48
	uint32_t N000047D1; //0x0B4C
	uint32_t N00003B61; //0x0B50
	uint32_t N000047D3; //0x0B54
	uint32_t N00003B62; //0x0B58
	uint32_t N000047D5; //0x0B5C
	uint32_t N00003B63; //0x0B60
	uint32_t N000047D7; //0x0B64
	uint32_t N00003B64; //0x0B68
	uint32_t N000047D9; //0x0B6C
	uint32_t N00003B65; //0x0B70
	uint32_t N000047DB; //0x0B74
	uint32_t N00003B66; //0x0B78
	uint32_t N000047DD; //0x0B7C
	uint32_t N00003B67; //0x0B80
	uint32_t N000047DF_2; //0x0B84
	uint32_t N00003B68; //0x0B88
	uint32_t N000047E1; //0x0B8C
	uint32_t N00003B69; //0x0B90
	uint32_t N000047E3; //0x0B94
	uint32_t N00003B6A; //0x0B98
	uint32_t N000047E5; //0x0B9C
	uint32_t N00003B6B; //0x0BA0
	uint32_t N000047E7; //0x0BA4
	uint32_t N00003B6C; //0x0BA8
	uint32_t N000047E9; //0x0BAC
	uint32_t N00003B6D; //0x0BB0
	uint32_t N000047EB; //0x0BB4
	uint32_t N00003B6E; //0x0BB8
	uint32_t N000047ED; //0x0BBC
	uint32_t N00003B6F; //0x0BC0
	uint32_t N000047EF; //0x0BC4
	uint32_t N00003B70; //0x0BC8
	uint32_t N000047F1; //0x0BCC
	uint32_t N00003B71; //0x0BD0
	uint32_t N000047F3; //0x0BD4
	uint32_t N00003B72; //0x0BD8
	uint32_t N000047F5; //0x0BDC
	uint32_t N00003B73; //0x0BE0
	uint32_t N000047F7; //0x0BE4
	uint32_t N00003B74; //0x0BE8
	uint32_t N000047F9; //0x0BEC
	uint32_t N00003B75; //0x0BF0
	uint32_t N000047FB; //0x0BF4
	uint32_t N00003B76; //0x0BF8
	uint32_t N000047FD; //0x0BFC
	uint32_t N00003B77; //0x0C00
	uint32_t N000047FF; //0x0C04
	uint32_t N00003B78; //0x0C08
	uint32_t N00004801; //0x0C0C
	uint32_t N00003B79; //0x0C10
	uint32_t N00004803; //0x0C14
	uint32_t N00003B7A; //0x0C18
	uint32_t N00004805; //0x0C1C
	uint32_t N00003B7B; //0x0C20
	uint32_t N00004807; //0x0C24
	uint32_t N00003B7C; //0x0C28
	uint32_t N00004809; //0x0C2C
	uint32_t N00003B7D; //0x0C30
	uint32_t N0000480B; //0x0C34
	uint32_t N00003B7E; //0x0C38
	uint32_t N0000480D; //0x0C3C
	uint32_t N00003B7F; //0x0C40
	uint32_t N0000480F; //0x0C44
	uint32_t N00003B80; //0x0C48
	uint32_t N00004811; //0x0C4C
	uint32_t N00003B81; //0x0C50
	uint32_t N00004813; //0x0C54
	uint32_t N00003B82; //0x0C58
	uint32_t N00004815; //0x0C5C
	uint32_t N00003B83; //0x0C60
	uint32_t N00004817; //0x0C64
	uint32_t N00003B84; //0x0C68
	uint32_t N00004819; //0x0C6C
	uint32_t N00003B85; //0x0C70
	uint32_t N0000481B; //0x0C74
	uint32_t N00003B86; //0x0C78
	uint32_t N0000481D; //0x0C7C
	uint32_t N00003B87; //0x0C80
	uint32_t N0000481F; //0x0C84
	uint32_t N00003B88; //0x0C88
	uint32_t N00004821; //0x0C8C
	uint32_t N00003B89; //0x0C90
	uint32_t N00004823; //0x0C94
	uint32_t N00003B8A; //0x0C98
	uint32_t N00004825; //0x0C9C
	uint32_t N00003B8B; //0x0CA0
	uint32_t N00004827; //0x0CA4
	uint32_t N00003B8C; //0x0CA8
	uint32_t N00004829; //0x0CAC
	uint32_t N00003B8D; //0x0CB0
	uint32_t N0000482B; //0x0CB4
	uint32_t N00003B8E; //0x0CB8
	uint32_t N0000482D; //0x0CBC
	uint32_t N00003B8F; //0x0CC0
	uint32_t N0000482F; //0x0CC4
	uint32_t N00003B90; //0x0CC8
	uint32_t N00004831; //0x0CCC
	uint32_t N00003B91; //0x0CD0
	uint32_t N00004833; //0x0CD4
	uint32_t N00003B92; //0x0CD8
	uint32_t N00004835; //0x0CDC
	uint32_t N00003B93; //0x0CE0
	uint32_t N00004837; //0x0CE4
	uint32_t N00003B94; //0x0CE8
	uint32_t N00004839; //0x0CEC
	uint32_t N00003B95; //0x0CF0
	uint32_t N0000483B; //0x0CF4
	uint32_t N00003B96; //0x0CF8
	uint32_t N0000483D; //0x0CFC
	uint32_t N00003B97; //0x0D00
	uint32_t N0000483F; //0x0D04
	uint32_t N00003B98; //0x0D08
	uint32_t N00004841; //0x0D0C
	uint32_t N00003B99; //0x0D10
	uint32_t N00004843; //0x0D14
	uint32_t N00003B9A; //0x0D18
	uint32_t N00004845; //0x0D1C
	uint32_t N00003B9B; //0x0D20
	uint32_t N00004847; //0x0D24
	uint32_t N00003B9C; //0x0D28
	uint32_t N00004849; //0x0D2C
	uint32_t N00003B9D; //0x0D30
	uint32_t N0000484B; //0x0D34
	uint32_t N00003B9E; //0x0D38
	uint32_t N0000484D; //0x0D3C
	uint32_t N00003B9F; //0x0D40
	uint32_t N0000484F; //0x0D44
	uint32_t N00003BA0; //0x0D48
	uint32_t N00004851; //0x0D4C
	uint32_t N00003BA1; //0x0D50
	uint32_t N00004853; //0x0D54
	uint32_t N00003BA2; //0x0D58
	uint32_t N00004855; //0x0D5C
	uint32_t N00003BA3; //0x0D60
	uint32_t N00004857; //0x0D64
	uint32_t N00003BA4; //0x0D68
	uint32_t N00004859; //0x0D6C
	uint32_t N00003BA5; //0x0D70
	uint32_t N0000485B; //0x0D74
	uint32_t N00003BA6; //0x0D78
	uint32_t N0000485D; //0x0D7C
	uint32_t N00003BA7; //0x0D80
	uint32_t N0000485F; //0x0D84
	uint32_t N00003BA8; //0x0D88
	uint32_t N00004861; //0x0D8C
	uint32_t N00003BA9; //0x0D90
	uint32_t N00004863; //0x0D94
	uint32_t N00003BAA; //0x0D98
	uint32_t N00004865; //0x0D9C
	uint32_t N00003BAB; //0x0DA0
	uint32_t N00004867; //0x0DA4
	uint32_t N00003BAC; //0x0DA8
	uint32_t N00004869; //0x0DAC
	uint32_t N00003BAD; //0x0DB0
	uint32_t N0000486B; //0x0DB4
	uint32_t N00003BAE; //0x0DB8
	uint32_t N0000486D; //0x0DBC
	uint32_t N00003BAF; //0x0DC0
	uint32_t N0000486F; //0x0DC4
	uint32_t N00003BB0; //0x0DC8
	uint32_t N00004871; //0x0DCC
	uint32_t N00003BB1; //0x0DD0
	uint32_t N00004873; //0x0DD4
	uint32_t N00003BB2; //0x0DD8
	uint32_t N00004875; //0x0DDC
	uint32_t N00003BB3; //0x0DE0
	uint32_t N00004877; //0x0DE4
	uint32_t N00003BB4; //0x0DE8
	uint32_t N00004879; //0x0DEC
	uint32_t N00003BB5; //0x0DF0
	uint32_t N0000487B; //0x0DF4
	uint32_t N00003BB6; //0x0DF8
	uint32_t N0000487D; //0x0DFC
	uint32_t N00003BB7; //0x0E00
	uint32_t N0000487F; //0x0E04
	uint32_t N00003BB8; //0x0E08
	uint32_t N00004881; //0x0E0C
	uint32_t N00003BB9; //0x0E10
	uint32_t N00004883; //0x0E14
	uint32_t N00003BBA; //0x0E18
	uint32_t N00004885; //0x0E1C
	uint32_t N00003BBB; //0x0E20
	uint32_t N00004887; //0x0E24
	uint32_t N00003BBC; //0x0E28
	uint32_t N00004889; //0x0E2C
	uint32_t N00003BBD; //0x0E30
	uint32_t N0000488B; //0x0E34
	uint32_t N00003BBE; //0x0E38
	uint32_t N0000488D; //0x0E3C
	uint32_t N00003BBF; //0x0E40
	uint32_t N0000488F; //0x0E44
	uint32_t N00003BC0; //0x0E48
	uint32_t N00004891; //0x0E4C
	uint32_t N00003BC1; //0x0E50
	uint32_t N00004893; //0x0E54
	uint32_t N00003BC2; //0x0E58
	uint32_t N00004895; //0x0E5C
	uint32_t N00003BC3; //0x0E60
	uint32_t N00004897; //0x0E64
	uint32_t N00003BC4; //0x0E68
	uint32_t N00004899; //0x0E6C
	uint32_t N00003BC5; //0x0E70
	uint32_t N0000489B; //0x0E74
	uint32_t N00003BC6; //0x0E78
	uint32_t N0000489D; //0x0E7C
	uint32_t N00003BC7; //0x0E80
	uint32_t N0000489F; //0x0E84
	uint32_t N00003BC8; //0x0E88
	uint32_t N000048A1; //0x0E8C
	uint32_t N00003BC9; //0x0E90
	uint32_t N000048A3; //0x0E94
	uint32_t N00003BCA; //0x0E98
	uint32_t N000048A5; //0x0E9C
	uint32_t N00003BCB; //0x0EA0
	uint32_t N000048A7; //0x0EA4
	uint32_t N00003BCC; //0x0EA8
	uint32_t N000048A9; //0x0EAC
	uint32_t N00003BCD; //0x0EB0
	uint32_t N000048AB; //0x0EB4
	uint32_t N00003BCE; //0x0EB8
	uint32_t N000048AD; //0x0EBC
	uint32_t N00003BCF; //0x0EC0
	uint32_t N000048AF; //0x0EC4
	uint32_t N00003BD0; //0x0EC8
	uint32_t N000048B1; //0x0ECC
	uint32_t N00003BD1; //0x0ED0
	uint32_t N000048B3; //0x0ED4
	uint32_t N00003BD2; //0x0ED8
	uint32_t N000048B5; //0x0EDC
	uint32_t N00003BD3; //0x0EE0
	uint32_t N000048B7; //0x0EE4
	uint32_t N00003BD4; //0x0EE8
	uint32_t N000048B9; //0x0EEC
	uint32_t N00003BD5; //0x0EF0
	uint32_t N000048BB; //0x0EF4
	uint32_t N00003BD6; //0x0EF8
	uint32_t N000048BD; //0x0EFC
	uint32_t N00003BD7; //0x0F00
	uint32_t N000048BF; //0x0F04
	uint32_t N00003BD8; //0x0F08
	uint32_t N000048C1; //0x0F0C
	uint32_t N00003BD9; //0x0F10
	uint32_t N000048C3; //0x0F14
	uint32_t N00003BDA; //0x0F18
	uint32_t N000048C5; //0x0F1C
	uint32_t N00003BDB; //0x0F20
	uint32_t N000048C7; //0x0F24
	uint32_t N00003BDC; //0x0F28
	uint32_t N000048C9; //0x0F2C
	uint32_t N00003BDD; //0x0F30
	uint32_t N000048CB; //0x0F34
	uint32_t N00003BDE; //0x0F38
	uint32_t N000048CD; //0x0F3C
	uint32_t N00003BDF; //0x0F40
	uint32_t N000048CF; //0x0F44
	uint32_t N00003BE0; //0x0F48
	uint32_t N000048D1; //0x0F4C
	uint32_t N00003BE1; //0x0F50
	uint32_t N000048D3; //0x0F54
	uint32_t N00003BE2; //0x0F58
	uint32_t N000048D5; //0x0F5C
	uint32_t N00003BE3; //0x0F60
	uint32_t N000048D7; //0x0F64
	uint32_t N00003BE4; //0x0F68
	uint32_t N000048D9; //0x0F6C
	uint32_t N00003BE5; //0x0F70
	uint32_t N000048DB; //0x0F74
	uint32_t N00003BE6; //0x0F78
	uint32_t N000048DD; //0x0F7C
	uint32_t N00003BE7; //0x0F80
	uint32_t N000048DF; //0x0F84
	uint32_t N00003BE8; //0x0F88
	uint32_t N000048E1; //0x0F8C
	uint32_t N00003BE9; //0x0F90
	uint32_t N000048E3; //0x0F94
	uint32_t N00003BEA; //0x0F98
	uint32_t N000048E5; //0x0F9C
	uint32_t N00003BEB; //0x0FA0
	uint32_t N000048E7; //0x0FA4
	uint32_t N00003BEC; //0x0FA8
	uint32_t N000048E9; //0x0FAC
	uint32_t N00003BED; //0x0FB0
	uint32_t N000048EB; //0x0FB4
	uint32_t N00003BEE; //0x0FB8
	uint32_t N000048ED; //0x0FBC
	uint32_t N00003BEF; //0x0FC0
	uint32_t N000048EF; //0x0FC4
	uint32_t N00003BF0; //0x0FC8
	uint32_t N000048F1; //0x0FCC
	uint32_t N00003BF1; //0x0FD0
	uint32_t N000048F3; //0x0FD4
	uint32_t N00003BF2; //0x0FD8
	uint32_t N000048F5; //0x0FDC
	uint32_t N00003BF3; //0x0FE0
	uint32_t N000048F7; //0x0FE4
	uint32_t N00003BF4; //0x0FE8
	uint32_t N000048F9; //0x0FEC
	uint32_t N00003BF5; //0x0FF0
	uint32_t N000048FB; //0x0FF4
	uint32_t N00003BF6; //0x0FF8
	uint32_t N000048FD; //0x0FFC
	uint32_t N00003BF7; //0x1000
	uint32_t N000048FF; //0x1004
	uint32_t N00003BF8; //0x1008
	uint32_t N00004901; //0x100C
	uint32_t N00003BF9; //0x1010
	uint32_t N00004903; //0x1014
	uint32_t N00003BFA; //0x1018
	uint32_t N00004905; //0x101C
	uint32_t N00003BFB; //0x1020
	uint32_t N00004907; //0x1024
	uint32_t N00003BFC; //0x1028
	uint32_t N00004909; //0x102C
	uint32_t N00003BFD; //0x1030
	uint32_t N0000490B; //0x1034
	uint32_t N00003BFE; //0x1038
	uint32_t N0000490D; //0x103C
	uint32_t N00003BFF; //0x1040
	uint32_t N0000490F; //0x1044
	uint32_t N00003C00; //0x1048
	uint32_t N00004911; //0x104C
	uint32_t N00003C01; //0x1050
	uint32_t N00004913; //0x1054
	uint32_t N00003C02; //0x1058
	uint32_t N00004915; //0x105C
	uint32_t N00003C03; //0x1060
	uint32_t N00004917; //0x1064
	uint32_t N00003C04; //0x1068
	uint32_t N00004919; //0x106C
	uint32_t N00003C05; //0x1070
	uint32_t N0000491B; //0x1074
	uint32_t N00003C06; //0x1078
	uint32_t N0000491D; //0x107C
	uint32_t N00003C07; //0x1080
	uint32_t N0000491F; //0x1084
	uint32_t N00003C08; //0x1088
	uint32_t N00004921; //0x108C
	uint32_t N00003C09; //0x1090
	uint32_t N00004923; //0x1094
	uint32_t N00003C0A; //0x1098
	uint32_t N00004925; //0x109C
	uint32_t N00003C0B; //0x10A0
	uint32_t N00004927; //0x10A4
	uint32_t N00003C0C; //0x10A8
	uint32_t N00004929; //0x10AC
	uint32_t N00003C0D; //0x10B0
	uint32_t N0000492B; //0x10B4
	uint32_t N00003C0E; //0x10B8
	uint32_t N0000492D; //0x10BC
	uint32_t N00003C0F; //0x10C0
	uint32_t N0000492F; //0x10C4
	uint32_t N00003C10; //0x10C8
	uint32_t N00004931; //0x10CC
	uint32_t N00003C11; //0x10D0
	uint32_t N00004933; //0x10D4
	uint32_t N00003C12; //0x10D8
	uint32_t N00004935; //0x10DC
	uint32_t N00003C13; //0x10E0
	uint32_t N00004937; //0x10E4
	uint32_t N00003C14; //0x10E8
	uint32_t N00004939; //0x10EC
	uint32_t N00003C15; //0x10F0
	uint32_t N0000493B; //0x10F4
	uint32_t N00003C16; //0x10F8
	uint32_t N0000493D; //0x10FC
	uint32_t N00003C17; //0x1100
	uint32_t N0000493F; //0x1104
	uint32_t N00003C18; //0x1108
	uint32_t N00004941; //0x110C
	uint32_t N00003C19; //0x1110
	uint32_t N00004943; //0x1114
	uint32_t N00003C1A; //0x1118
	uint32_t N00004945; //0x111C
	uint32_t N00003C1B; //0x1120
	uint32_t N00004947; //0x1124
	uint32_t N00003C1C; //0x1128
	uint32_t N00004949; //0x112C
	uint32_t N00003C1D; //0x1130
	uint32_t N0000494B; //0x1134
	uint32_t N00003C1E; //0x1138
	uint32_t N0000494D; //0x113C
	uint32_t N00003C1F; //0x1140
	uint32_t N0000494F; //0x1144
	uint32_t N00003C20; //0x1148
	uint32_t N00004951; //0x114C
	uint32_t N00003C21; //0x1150
	uint32_t N00004953; //0x1154
	uint32_t N00003C22; //0x1158
	uint32_t N00004955; //0x115C
	uint32_t N00003C23; //0x1160
	uint32_t N00004957; //0x1164
	uint32_t N00003C24; //0x1168
	uint32_t N00004959; //0x116C
	uint32_t N00003C25; //0x1170
	uint32_t N0000495B; //0x1174
	uint32_t N00003C26; //0x1178
	uint32_t N0000495D; //0x117C
	uint32_t N00003C27; //0x1180
	uint32_t N0000495F; //0x1184
	uint32_t N00003C28; //0x1188
	uint32_t N00004961; //0x118C
	uint32_t N00003C29; //0x1190
	uint32_t N00004963; //0x1194
	uint32_t N00003C2A; //0x1198
	uint32_t N00004965; //0x119C
	uint32_t N00003C2B; //0x11A0
	uint32_t N00004967; //0x11A4
	uint32_t N00003C2C; //0x11A8
	uint32_t N00004969; //0x11AC
	uint32_t N00003C2D; //0x11B0
	uint32_t N0000496B; //0x11B4
	uint32_t N00003C2E; //0x11B8
	uint32_t N0000496D; //0x11BC
	uint32_t N00003C2F; //0x11C0
	uint32_t N0000496F; //0x11C4
	uint32_t N00003C30; //0x11C8
	uint32_t N00004971; //0x11CC
	uint32_t N00003C31; //0x11D0
	uint32_t N00004973; //0x11D4
	uint32_t N00003C32; //0x11D8
	uint32_t N00004975; //0x11DC
	uint32_t N00003C33; //0x11E0
	uint32_t N00004977; //0x11E4
	uint32_t N00003C34; //0x11E8
	uint32_t N00004979; //0x11EC
	uint32_t N00003C35; //0x11F0
	uint32_t N0000497B; //0x11F4
	uint32_t N00003C36; //0x11F8
	uint32_t N0000497D; //0x11FC
	uint32_t N00003C37; //0x1200
	uint32_t N0000497F; //0x1204
	uint32_t N00003C38; //0x1208
	uint32_t N00004981; //0x120C
	uint32_t N00003C39; //0x1210
	uint32_t N00004983; //0x1214
	uint32_t N00003C3A; //0x1218
	uint32_t N00004985; //0x121C
	uint32_t N00003C3B; //0x1220
	uint32_t N00004987; //0x1224
	uint32_t N00003C3C; //0x1228
	uint32_t N00004989; //0x122C
	uint32_t N00003C3D; //0x1230
	uint32_t N0000498B; //0x1234
	uint32_t N00003C3E; //0x1238
	uint32_t N0000498D; //0x123C
	uint32_t N00003C3F; //0x1240
	uint32_t N0000498F; //0x1244
	uint32_t N00003C40; //0x1248
	uint32_t N00004991; //0x124C
	uint32_t N00003C41; //0x1250
	uint32_t N00004993; //0x1254
	uint32_t N00003C42; //0x1258
	uint32_t N00004995; //0x125C
	uint32_t N00003C43; //0x1260
	uint32_t N00004997; //0x1264
	uint32_t N00003C44; //0x1268
	uint32_t N00004999; //0x126C
	uint32_t N00003C45; //0x1270
	uint32_t N0000499B; //0x1274
	uint32_t N00003C46; //0x1278
	uint32_t N0000499D; //0x127C
	uint32_t N00003C47; //0x1280
	uint32_t N0000499F; //0x1284
	uint32_t N00003C48; //0x1288
	uint32_t N000049A1; //0x128C
	uint32_t N00003C49; //0x1290
	uint32_t N000049A3; //0x1294
	uint32_t N00003C4A; //0x1298
	uint32_t N000049A5; //0x129C
	uint32_t N00003C4B; //0x12A0
	uint32_t N000049A7; //0x12A4
	uint32_t N00003C4C; //0x12A8
	uint32_t N000049A9; //0x12AC
	uint32_t N00003C4D; //0x12B0
	uint32_t N000049AB; //0x12B4
	uint32_t N00003C4E; //0x12B8
	uint32_t N000049AD; //0x12BC
	uint32_t N00003C4F; //0x12C0
	uint32_t N000049AF; //0x12C4
	uint32_t N00003C50; //0x12C8
	uint32_t N000049B1; //0x12CC
	uint32_t N00003C51; //0x12D0
	uint32_t N000049B3; //0x12D4
	uint32_t N00003C52; //0x12D8
	uint32_t N000049B5; //0x12DC
	uint32_t N00003C53; //0x12E0
	uint32_t N000049B7; //0x12E4
	uint32_t N00003C54; //0x12E8
	uint32_t N000049B9; //0x12EC
	uint32_t N00003C55; //0x12F0
	uint32_t N000049BB; //0x12F4
	uint32_t N00003C56; //0x12F8
	uint32_t N000049BD; //0x12FC
	uint32_t N00003C57; //0x1300
	uint32_t N000049BF; //0x1304
	uint32_t N00003C58; //0x1308
	uint32_t N000049C1; //0x130C
	uint32_t N00003C59; //0x1310
	uint32_t N000049C3; //0x1314
	uint32_t N00003C5A; //0x1318
	uint32_t N000049C5; //0x131C
	uint32_t N00003C5B; //0x1320
	uint32_t N000049C7; //0x1324
	uint32_t N00003C5C; //0x1328
	uint32_t N000049C9; //0x132C
	uint32_t N00003C5D; //0x1330
	uint32_t N000049CB; //0x1334
	uint32_t N00003C5E; //0x1338
	uint32_t N000049CD; //0x133C
	uint32_t N00003C5F; //0x1340
	uint32_t N000049CF; //0x1344
	uint32_t N00003C60; //0x1348
	uint32_t N000049D1; //0x134C
	uint32_t N00003C61; //0x1350
	uint32_t N000049D3; //0x1354
	uint32_t N00003C62; //0x1358
	uint32_t N000049D5; //0x135C
	uint32_t N00003C63; //0x1360
	uint32_t N000049D7; //0x1364
	uint32_t N00003C64; //0x1368
	uint32_t N000049D9; //0x136C
	uint32_t N00003C65; //0x1370
	uint32_t N000049DB; //0x1374
	uint32_t N00003C66; //0x1378
	uint32_t N000049DD; //0x137C
	uint32_t N00003C67; //0x1380
	uint32_t N000049DF; //0x1384
	uint32_t N00003C68; //0x1388
	uint32_t N000049E1; //0x138C
	uint32_t N00003C69; //0x1390
	uint32_t N000049E3; //0x1394
	uint32_t N00003C6A; //0x1398
	uint32_t N000049E5; //0x139C
	uint32_t N00003C6B; //0x13A0
	uint32_t N000049E7; //0x13A4
	uint32_t N00003C6C; //0x13A8
	uint32_t N000049E9; //0x13AC
	uint32_t N00003C6D; //0x13B0
	uint32_t N000049EB; //0x13B4
	uint32_t N00003C6E; //0x13B8
	uint32_t N000049ED; //0x13BC
	uint32_t N00003C6F; //0x13C0
	uint32_t N000049EF; //0x13C4
	uint32_t N00003C70; //0x13C8
	uint32_t N000049F1; //0x13CC
	uint32_t N00003C71; //0x13D0
	uint32_t N000049F3; //0x13D4
	uint32_t N00003C72; //0x13D8
	uint32_t N000049F5; //0x13DC
	uint32_t N00003C73; //0x13E0
	uint32_t N000049F7; //0x13E4
	uint32_t N00003C74; //0x13E8
	uint32_t N000049F9; //0x13EC
	uint32_t N00003C75; //0x13F0
	uint32_t N000049FB; //0x13F4
	uint32_t N00003C76; //0x13F8
	uint32_t N000049FD; //0x13FC
	uint32_t N00003C77; //0x1400
	uint32_t N000049FF; //0x1404
	uint32_t N00003C78; //0x1408
	uint32_t N00004A01; //0x140C
	uint32_t N00003C79; //0x1410
	uint32_t N00004A03; //0x1414
	uint32_t N00003C7A; //0x1418
	uint32_t N00004A05; //0x141C
	uint32_t N00003C7B; //0x1420
	uint32_t N00004A07; //0x1424
	uint32_t N00003C7C; //0x1428
	uint32_t N00004A09; //0x142C
	uint32_t N00003C7D; //0x1430
	uint32_t N00004A0B; //0x1434
	uint32_t N00003C7E; //0x1438
	uint32_t N00004A0D; //0x143C
	uint32_t N00003C7F; //0x1440
	uint32_t N00004A0F; //0x1444
	uint32_t N00003C80; //0x1448
	uint32_t N00004A11; //0x144C
	uint32_t N00003C81; //0x1450
	uint32_t N00004A13; //0x1454
	uint32_t N00003C82; //0x1458
	uint32_t N00004A15; //0x145C
	uint32_t N00003C83; //0x1460
	uint32_t N00004A17; //0x1464
	uint32_t N00003C84; //0x1468
	uint32_t N00004A19; //0x146C
	uint32_t N00003C85; //0x1470
	uint32_t N00004A1B; //0x1474
	uint32_t N00003C86; //0x1478
	uint32_t N00004A1D; //0x147C
	uint32_t N00003C87; //0x1480
	uint32_t N00004A1F; //0x1484
	uint32_t N00003C88; //0x1488
	uint32_t N00004A21; //0x148C
	uint32_t N00003C89; //0x1490
	uint32_t N00004A23; //0x1494
	uint32_t N00003C8A; //0x1498
	uint32_t N00004A25; //0x149C
	uint32_t N00003C8B; //0x14A0
	uint32_t N00004A27; //0x14A4
	uint32_t N00003C8C; //0x14A8
	uint32_t N00004A29; //0x14AC
	uint32_t N00003C8D; //0x14B0
	uint32_t N00004A2B; //0x14B4
	uint32_t N00003C8E; //0x14B8
	uint32_t N00004A2D; //0x14BC
	uint32_t N00003C8F; //0x14C0
	uint32_t N00004A2F; //0x14C4
	uint32_t N00003C90; //0x14C8
	uint32_t N00004A31; //0x14CC
	uint32_t N00003C91; //0x14D0
	uint32_t N00004A33; //0x14D4
	uint32_t N00003C92; //0x14D8
	uint32_t N00004A35; //0x14DC
	uint32_t N00003C93; //0x14E0
	uint32_t N00004A37; //0x14E4
	uint32_t N00003C94; //0x14E8
	uint32_t N00004A39; //0x14EC
	uint32_t N00003C95; //0x14F0
	uint32_t N00004A3B; //0x14F4
	uint32_t N00003C96; //0x14F8
	uint32_t N00004A3D; //0x14FC
	uint32_t N00003C97; //0x1500
	uint32_t N00004A3F; //0x1504
	uint32_t N00003C98; //0x1508
	uint32_t N00004A41; //0x150C
	uint32_t N00003C99; //0x1510
	uint32_t N00004A43; //0x1514
	uint32_t N00003C9A; //0x1518
	uint32_t N00004A45; //0x151C
	uint32_t N00003C9B; //0x1520
	uint32_t N00004A47; //0x1524
	uint32_t N00003C9C; //0x1528
	uint32_t N00004A49; //0x152C
	uint32_t N00003C9D; //0x1530
	uint32_t N00004A4B; //0x1534
	uint32_t N00003C9E; //0x1538
	uint32_t N00004A4D; //0x153C
	uint32_t N00003C9F; //0x1540
	uint32_t N00004A4F; //0x1544
	uint32_t N00003CA0; //0x1548
	uint32_t N00004A51; //0x154C
	uint32_t N00003CA1; //0x1550
	uint32_t N00004A53; //0x1554
	uint32_t N00003CA2; //0x1558
	uint32_t N00004A55; //0x155C
	uint32_t N00003CA3; //0x1560
	uint32_t N00004A57; //0x1564
	uint32_t N00003CA4; //0x1568
	uint32_t N00004A59; //0x156C
	uint32_t N00003CA5; //0x1570
	uint32_t N00004A5B; //0x1574
	uint32_t N00003CA6; //0x1578
	uint32_t N00004A5D; //0x157C
	uint32_t N00003CA7; //0x1580
	uint32_t N00004A5F; //0x1584
	uint32_t N00003CA8; //0x1588
	uint32_t N00004A61; //0x158C
	uint32_t N00003CA9; //0x1590
	uint32_t N00004A63; //0x1594
	uint32_t N00003CAA; //0x1598
	uint32_t N00004A65; //0x159C
	uint32_t N00003CAB; //0x15A0
	uint32_t N00004A67; //0x15A4
	uint32_t N00003CAC; //0x15A8
	uint32_t N00004A69; //0x15AC
	uint32_t N00003CAD; //0x15B0
	uint32_t N00004A6B; //0x15B4
	uint32_t N00003CAE; //0x15B8
	uint32_t N00004A6D; //0x15BC
	uint32_t N00003CAF; //0x15C0
	uint32_t N00004A6F; //0x15C4
	uint32_t N00003CB0; //0x15C8
	uint32_t N00004A71; //0x15CC
	uint32_t N00003CB1; //0x15D0
	uint32_t N00004A73; //0x15D4
	uint32_t N00003CB2; //0x15D8
	uint32_t N00004A75; //0x15DC
	uint32_t N00003CB3; //0x15E0
	uint32_t N00004A77; //0x15E4
	uint32_t N00003CB4; //0x15E8
	uint32_t N00004A79; //0x15EC
	uint32_t N00003CB5; //0x15F0
	uint32_t N00004A7B; //0x15F4
	uint32_t N00003CB6; //0x15F8
	uint32_t N00004A7D; //0x15FC
	uint32_t N00003CB7; //0x1600
	uint32_t N00004A7F; //0x1604
	uint32_t N00003CB8; //0x1608
	uint32_t N00004A81; //0x160C
	uint32_t N00003CB9; //0x1610
	uint32_t N00004A83; //0x1614
	uint32_t N00003CBA; //0x1618
	uint32_t N00004A85; //0x161C
	uint32_t N00003CBB; //0x1620
	uint32_t N00004A87; //0x1624
	uint32_t N00003CBC; //0x1628
	uint32_t N00004A89; //0x162C
	uint32_t N00003CBD; //0x1630
	uint32_t N00004A8B; //0x1634
	uint32_t N00003CBE; //0x1638
	uint32_t N00004A8D; //0x163C
	uint32_t N00003CBF; //0x1640
	uint32_t N00004A8F; //0x1644
	uint32_t N00003CC0; //0x1648
	uint32_t N00004A91; //0x164C
	uint32_t N00003CC1; //0x1650
	uint32_t N00004A93; //0x1654
	uint32_t N00003CC2; //0x1658
	uint32_t N00004A95; //0x165C
	uint32_t N00003CC3; //0x1660
	uint32_t N00004A97; //0x1664
	uint32_t N00003CC4; //0x1668
	uint32_t N00004A99; //0x166C
	uint32_t N00003CC5; //0x1670
	uint32_t N00004A9B; //0x1674
	uint32_t N00003CC6; //0x1678
	uint32_t N00004A9D; //0x167C
	uint32_t N00003CC7; //0x1680
	uint32_t N00004A9F; //0x1684
	uint32_t N00003CC8; //0x1688
	uint32_t N00004AA1; //0x168C
	uint32_t N00003CC9; //0x1690
	uint32_t N00004AA3; //0x1694
	uint32_t N00003CCA; //0x1698
	uint32_t N00004AA5; //0x169C
	uint32_t N00003CCB; //0x16A0
	uint32_t N00004AA7; //0x16A4
	uint32_t N00003CCC; //0x16A8
	uint32_t N00004AA9; //0x16AC
	uint32_t N00003CCD; //0x16B0
	uint32_t N00004AAB; //0x16B4
	uint32_t N00003CCE; //0x16B8
	uint32_t N00004AAD; //0x16BC
	uint32_t N00003CCF; //0x16C0
	uint32_t N00004AAF; //0x16C4
	uint32_t N00003CD0; //0x16C8
	uint32_t N00004AB1; //0x16CC
	uint32_t N00003CD1; //0x16D0
	uint32_t N00004AB3; //0x16D4
	uint32_t N00003CD2; //0x16D8
	uint32_t N00004AB5; //0x16DC
	uint32_t N00003CD3; //0x16E0
	uint32_t N00004AB7; //0x16E4
	uint32_t N00003CD4; //0x16E8
	uint32_t N00004AB9; //0x16EC
	uint32_t N00003CD5; //0x16F0
	uint32_t N00004ABB; //0x16F4
	uint32_t N00003CD6; //0x16F8
	uint32_t N00004ABD; //0x16FC
	uint32_t N00003CD7; //0x1700
	uint32_t N00004ABF; //0x1704
	uint32_t N00003CD8; //0x1708
	uint32_t N00004AC1; //0x170C
	uint32_t N00003CD9; //0x1710
	uint32_t N00004AC3; //0x1714
	uint32_t N00003CDA; //0x1718
	uint32_t N00004AC5; //0x171C
	uint32_t N00003CDB; //0x1720
	uint32_t N00004AC7; //0x1724
	uint32_t N00003CDC; //0x1728
	uint32_t N00004AC9; //0x172C
	uint32_t N00003CDD; //0x1730
	uint32_t N00004ACB; //0x1734
	uint32_t N00003CDE; //0x1738
	uint32_t N00004ACD; //0x173C
	uint32_t N00003CDF; //0x1740
	uint32_t N00004ACF; //0x1744
	uint32_t N00003CE0; //0x1748
	uint32_t N00004AD1; //0x174C
	uint32_t N00003CE1; //0x1750
	uint32_t N00004AD3; //0x1754
	uint32_t N00003CE2; //0x1758
	uint32_t N00004AD5; //0x175C
	uint32_t N00003CE3; //0x1760
	uint32_t N00004AD7; //0x1764
	uint32_t N00003CE4; //0x1768
	uint32_t N00004AD9; //0x176C
	uint32_t N00003CE5; //0x1770
	uint32_t N00004ADB; //0x1774
	uint32_t N00003CE6; //0x1778
	uint32_t N00004ADD; //0x177C
	uint32_t N00003CE7; //0x1780
	uint32_t N00004ADF; //0x1784
	uint32_t N00003CE8; //0x1788
	uint32_t N00004AE1; //0x178C
	uint32_t N00003CE9; //0x1790
	uint32_t N00004AE3; //0x1794
	uint32_t N00003CEA; //0x1798
	uint32_t N00004AE5; //0x179C
	uint32_t N00003CEB; //0x17A0
	uint32_t N00004AE7; //0x17A4
	uint32_t N00003CEC; //0x17A8
	uint32_t N00004AE9; //0x17AC
	uint32_t N00003CED; //0x17B0
	uint32_t N00004AEB; //0x17B4
	uint32_t N00003CEE; //0x17B8
	uint32_t N00004AED; //0x17BC
	uint32_t N00003CEF; //0x17C0
	uint32_t N00004AEF; //0x17C4
	uint32_t N00003CF0; //0x17C8
	uint32_t N00004AF1; //0x17CC
	uint32_t N00003CF1; //0x17D0
	uint32_t N00004AF3; //0x17D4
	uint32_t N00003CF2; //0x17D8
	uint32_t N00004AF5; //0x17DC
	uint32_t N00003CF3; //0x17E0
	uint32_t N00004AF7; //0x17E4
	uint32_t N00003CF4; //0x17E8
	uint32_t N00004AF9; //0x17EC
	uint32_t N00003CF5; //0x17F0
	uint32_t N00004AFB; //0x17F4
	uint32_t N00003CF6; //0x17F8
	uint32_t N00004AFD; //0x17FC
	uint32_t N00003CF7; //0x1800
	uint32_t N00004AFF; //0x1804
	uint32_t N00003CF8; //0x1808
	uint32_t N00004B01; //0x180C
	uint32_t N00003CF9; //0x1810
	uint32_t N00004B03; //0x1814
	uint32_t N00003CFA; //0x1818
	uint32_t N00004B05; //0x181C
	uint32_t N00003CFB; //0x1820
	uint32_t N00004B07; //0x1824
	uint32_t N00003CFC; //0x1828
	uint32_t N00004B09; //0x182C
	uint32_t N00003CFD; //0x1830
	uint32_t N00004B0B; //0x1834
	uint32_t N00003CFE; //0x1838
	uint32_t N00004B0D; //0x183C
	uint32_t N00003CFF; //0x1840
	uint32_t N00004B0F; //0x1844
	uint32_t N00003D00; //0x1848
	uint32_t N00004B11; //0x184C
	uint32_t N00003D01; //0x1850
	uint32_t N00004B13; //0x1854
	uint32_t N00003D02; //0x1858
	uint32_t N00004B15; //0x185C
	uint32_t N00003D03; //0x1860
	uint32_t N00004B17; //0x1864
	uint32_t N00003D04; //0x1868
	uint32_t N00004B19; //0x186C
	uint32_t N00003D05; //0x1870
	uint32_t N00004B1B; //0x1874
	uint32_t N00003D06; //0x1878
	uint32_t N00004B1D; //0x187C
	uint32_t N00003D07; //0x1880
	uint32_t N00004B1F; //0x1884
	uint32_t N00003D08; //0x1888
	uint32_t N00004B21; //0x188C
	uint32_t N00003D09; //0x1890
	uint32_t N00004B23; //0x1894
	uint32_t N00003D0A; //0x1898
	uint32_t N00004B25; //0x189C
	uint32_t N00003D0B; //0x18A0
	uint32_t N00004B27; //0x18A4
	uint32_t N00003D0C; //0x18A8
	uint32_t N00004B29; //0x18AC
	uint32_t N00003D0D; //0x18B0
	uint32_t N00004B2B; //0x18B4
	uint32_t N00003D0E; //0x18B8
	uint32_t N00004B2D; //0x18BC
	uint32_t N00003D0F; //0x18C0
	uint32_t N00004B2F; //0x18C4
	uint32_t N00003D10; //0x18C8
	uint32_t N00004B31; //0x18CC
	uint32_t N00003D11; //0x18D0
	uint32_t N00004B33; //0x18D4
	uint32_t N00003D12; //0x18D8
	uint32_t N00004B35; //0x18DC
	uint32_t N00003D13; //0x18E0
	uint32_t N00004B37; //0x18E4
	uint32_t N00003D14; //0x18E8
	uint32_t N00004B39; //0x18EC
	uint32_t N00003D15; //0x18F0
	uint32_t N00004B3B; //0x18F4
	uint32_t N00003D16; //0x18F8
	uint32_t N00004B3D; //0x18FC
	uint32_t N00003D17; //0x1900
	uint32_t N00004B3F; //0x1904
	uint32_t N00003D18; //0x1908
	uint32_t N00004B41; //0x190C
	uint32_t N00003D19; //0x1910
	uint32_t N00004B43; //0x1914
	uint32_t N00003D1A; //0x1918
	uint32_t N00004B45; //0x191C
	uint32_t N00003D1B; //0x1920
	uint32_t N00004B47; //0x1924
	uint32_t N00003D1C; //0x1928
	uint32_t N00004B49; //0x192C
	uint32_t N00003D1D; //0x1930
	uint32_t N00004B4B; //0x1934
	uint32_t N00003D1E; //0x1938
	uint32_t N00004B4D; //0x193C
	uint32_t N00003D1F; //0x1940
	uint32_t N00004B4F; //0x1944
	uint32_t N00003D20; //0x1948
	uint32_t N00004B51; //0x194C
	uint32_t N00003D21; //0x1950
	uint32_t N00004B53; //0x1954
	uint32_t N00003D22; //0x1958
	uint32_t N00004B55; //0x195C
	uint32_t N00003D23; //0x1960
	uint32_t N00004B57; //0x1964
	uint32_t N00003D24; //0x1968
	uint32_t N00004B59; //0x196C
	uint32_t N00003D25; //0x1970
	uint32_t N00004B5B; //0x1974
	uint32_t N00003D26; //0x1978
	uint32_t N00004B5D; //0x197C
	uint32_t N00003D27; //0x1980
	uint32_t N00004B5F; //0x1984
	uint32_t N00003D28; //0x1988
	uint32_t N00004B61; //0x198C
	uint32_t N00003D29; //0x1990
	uint32_t N00004B63; //0x1994
	uint32_t N00003D2A; //0x1998
	uint32_t N00004B65; //0x199C
	uint32_t N00003D2B; //0x19A0
	uint32_t N00004B67; //0x19A4
	uint32_t N00003D2C; //0x19A8
	uint32_t N00004B69; //0x19AC
	uint32_t N00003D2D; //0x19B0
	uint32_t N00004B6B; //0x19B4
	uint32_t N00003D2E; //0x19B8
	uint32_t N00004B6D; //0x19BC
	uint32_t N00003D2F; //0x19C0
	uint32_t N00004B6F; //0x19C4
	uint32_t N00003D30; //0x19C8
	uint32_t N00004B71; //0x19CC
	uint32_t N00003D31; //0x19D0
	uint32_t N00004B73; //0x19D4
	uint32_t N00003D32; //0x19D8
	uint32_t N00004B75; //0x19DC
	uint32_t N00003D33; //0x19E0
	uint32_t N00004B77; //0x19E4
	uint32_t N00003D34; //0x19E8
	uint32_t N00004B79; //0x19EC
	uint32_t N00003D35; //0x19F0
	uint32_t N00004B7B; //0x19F4
	uint32_t N00003D36; //0x19F8
	uint32_t N00004B7D; //0x19FC
	uint32_t N00003D37; //0x1A00
	uint32_t N00004B7F; //0x1A04
	uint32_t N00003D38; //0x1A08
	uint32_t N00004B81; //0x1A0C
	uint32_t N00003D39; //0x1A10
	uint32_t N00004B83; //0x1A14
	uint32_t N00003D3A; //0x1A18
	uint32_t N00004B85; //0x1A1C
	uint32_t N00003D3B; //0x1A20
	uint32_t N00004B87; //0x1A24
	uint32_t N00003D3C; //0x1A28
	uint32_t N00004B89; //0x1A2C
	uint32_t N00003D3D; //0x1A30
	uint32_t N00004B8B; //0x1A34
	uint32_t N00003D3E; //0x1A38
	uint32_t N00004B8D; //0x1A3C
	uint32_t N00003D3F; //0x1A40
	uint32_t N00004B8F; //0x1A44
	uint32_t N00003D40; //0x1A48
	uint32_t N00004B91; //0x1A4C
	uint32_t N00003D41; //0x1A50
	uint32_t N00004B93; //0x1A54
	uint32_t N00003D42; //0x1A58
	uint32_t N00004B95; //0x1A5C
	uint32_t N00003D43; //0x1A60
	uint32_t N00004B97; //0x1A64
	uint32_t N00003D44; //0x1A68
	uint32_t N00004B99; //0x1A6C
	uint32_t N00003D45; //0x1A70
	uint32_t N00004B9B; //0x1A74
	uint32_t N00003D46; //0x1A78
	uint32_t N00004B9D; //0x1A7C
	uint32_t N00003D47; //0x1A80
	uint32_t N00004B9F; //0x1A84
	uint32_t N00003D48; //0x1A88
	uint32_t N00004BA1; //0x1A8C
	uint32_t N00003D49; //0x1A90
	uint32_t N00004BA3; //0x1A94
	uint32_t N00003D4A; //0x1A98
	uint32_t N00004BA5; //0x1A9C
	uint32_t N00003D4B; //0x1AA0
	uint32_t N00004BA7; //0x1AA4
	uint32_t N00003D4C; //0x1AA8
	uint32_t N00004BA9; //0x1AAC
	uint32_t N00003D4D; //0x1AB0
	uint32_t N00004BAB; //0x1AB4
	uint32_t N00003D4E; //0x1AB8
	uint32_t N00004BAD; //0x1ABC
	uint32_t N00003D4F; //0x1AC0
	uint32_t N00004BAF; //0x1AC4
	uint32_t N00003D50; //0x1AC8
	uint32_t N00004BB1; //0x1ACC
	uint32_t N00003D51; //0x1AD0
	uint32_t N00004BB3; //0x1AD4
	uint32_t N00003D52; //0x1AD8
	uint32_t N00004BB5; //0x1ADC
	uint32_t N00003D53; //0x1AE0
	uint32_t N00004BB7; //0x1AE4
	uint32_t N00003D54; //0x1AE8
	uint32_t N00004BB9; //0x1AEC
	uint32_t N00003D55; //0x1AF0
	uint32_t N00004BBB; //0x1AF4
	uint32_t N00003D56; //0x1AF8
	uint32_t N00004BBD; //0x1AFC
	uint32_t N00003D57; //0x1B00
	uint32_t N00004BBF; //0x1B04
	uint32_t N00003D58; //0x1B08
	uint32_t N00004BC1; //0x1B0C
	uint32_t N00003D59; //0x1B10
	uint32_t N00004BC3; //0x1B14
	uint32_t N00003D5A; //0x1B18
	uint32_t N00004BC5; //0x1B1C
	uint32_t N00003D5B; //0x1B20
	uint32_t N00004BC7; //0x1B24
	uint32_t N00003D5C; //0x1B28
	uint32_t N00004BC9; //0x1B2C
	uint32_t N00003D5D; //0x1B30
	uint32_t N00004BCB; //0x1B34
	uint32_t N00003D5E; //0x1B38
	uint32_t N00004BCD; //0x1B3C
	uint32_t N00003D5F; //0x1B40
	uint32_t N00004BCF; //0x1B44
	uint32_t N00003D60; //0x1B48
	uint32_t N00004BD1; //0x1B4C
	uint32_t N00003D61; //0x1B50
	uint32_t N00004BD3; //0x1B54
	uint32_t N00003D62; //0x1B58
	uint32_t N00004BD5; //0x1B5C
	uint32_t N00003D63; //0x1B60
	uint32_t N00004BD7; //0x1B64
	uint32_t N00003D64; //0x1B68
	uint32_t N00004BD9; //0x1B6C
	uint32_t N00003D65; //0x1B70
	uint32_t N00004BDB; //0x1B74
	uint32_t N00003D66; //0x1B78
	uint32_t N00004BDD; //0x1B7C
	uint32_t N00003D67; //0x1B80
	uint32_t N00004BDF; //0x1B84
	uint32_t N00003D68; //0x1B88
	uint32_t N00004BE1; //0x1B8C
	uint32_t N00003D69; //0x1B90
	uint32_t N00004BE3; //0x1B94
	uint32_t N00003D6A; //0x1B98
	uint32_t N00004BE5; //0x1B9C
	uint32_t N00003D6B; //0x1BA0
	uint32_t N00004BE7; //0x1BA4
	uint32_t N00003D6C; //0x1BA8
	uint32_t N00004BE9; //0x1BAC
	uint32_t N00003D6D; //0x1BB0
	uint32_t N00004BEB; //0x1BB4
	uint32_t N00003D6E; //0x1BB8
	uint32_t N00004BED; //0x1BBC
	uint32_t N00003D6F; //0x1BC0
	uint32_t N00004BEF; //0x1BC4
	uint32_t N00003D70; //0x1BC8
	uint32_t N00004BF1; //0x1BCC
	uint32_t N00003D71; //0x1BD0
	uint32_t N00004BF3; //0x1BD4
	uint32_t N00003D72; //0x1BD8
	uint32_t N00004BF5; //0x1BDC
	uint32_t N00003D73; //0x1BE0
	uint32_t N00004BF7; //0x1BE4
	uint32_t N00003D74; //0x1BE8
	uint32_t N00004BF9; //0x1BEC
	uint32_t N00003D75; //0x1BF0
	uint32_t N00004BFB; //0x1BF4
	uint32_t N00003D76; //0x1BF8
	uint32_t N00004BFD; //0x1BFC
	uint32_t N00003D77; //0x1C00
	uint32_t N00004BFF; //0x1C04
	uint32_t N00003D78; //0x1C08
	uint32_t N00004C01; //0x1C0C
	uint32_t N00003D79; //0x1C10
	uint32_t N00004C03; //0x1C14
	uint32_t N00003D7A; //0x1C18
	uint32_t N00004C05; //0x1C1C
	uint32_t N00003D7B; //0x1C20
	uint32_t N00004C07; //0x1C24
	uint32_t N00003D7C; //0x1C28
	uint32_t N00004C09; //0x1C2C
	uint32_t N00003D7D; //0x1C30
	uint32_t N00004C0B; //0x1C34
	uint32_t N00003D7E; //0x1C38
	uint32_t N00004C0D; //0x1C3C
	uint32_t N00003D7F; //0x1C40
	uint32_t N00004C0F; //0x1C44
	uint32_t N00003D80; //0x1C48
	uint32_t N00004C11; //0x1C4C
	uint32_t N00003D81; //0x1C50
	uint32_t N00004C13; //0x1C54
	uint32_t N00003D82; //0x1C58
	uint32_t N00004C15; //0x1C5C
	uint32_t N00003D83; //0x1C60
	uint32_t N00004C17; //0x1C64
	uint32_t N00003D84; //0x1C68
	uint32_t N00004C19; //0x1C6C
	uint32_t N00003D85; //0x1C70
	uint32_t N00004C1B; //0x1C74
	uint32_t N00003D86; //0x1C78
	uint32_t N00004C1D; //0x1C7C
	uint32_t N00003D87; //0x1C80
	uint32_t N00004C1F; //0x1C84
	uint32_t N00003D88; //0x1C88
	uint32_t N00004C21; //0x1C8C
	uint32_t N00003D89; //0x1C90
	uint32_t N00004C23; //0x1C94
	uint32_t N00003D8A; //0x1C98
	uint32_t N00004C25; //0x1C9C
	uint32_t N00003D8B; //0x1CA0
	uint32_t N00004C27; //0x1CA4
	uint32_t N00003D8C; //0x1CA8
	uint32_t N00004C29; //0x1CAC
	uint32_t N00003D8D; //0x1CB0
	uint32_t N00004C2B; //0x1CB4
	uint32_t N00003D8E; //0x1CB8
	uint32_t N00004C2D; //0x1CBC
	uint32_t N00003D8F; //0x1CC0
	uint32_t N00004C2F; //0x1CC4
	uint32_t N00003D90; //0x1CC8
	uint32_t N00004C31; //0x1CCC
	uint32_t N00003D91; //0x1CD0
	uint32_t N00004C33; //0x1CD4
	uint32_t N00003D92; //0x1CD8
	uint32_t N00004C35; //0x1CDC
	uint32_t N00003D93; //0x1CE0
	uint32_t N00004C37; //0x1CE4
	uint32_t N00003D94; //0x1CE8
	uint32_t N00004C39; //0x1CEC
	uint32_t N00003D95; //0x1CF0
	uint32_t N00004C3B; //0x1CF4
	uint32_t N00003D96; //0x1CF8
	uint32_t N00004C3D; //0x1CFC
	uint32_t N00003D97; //0x1D00
	uint32_t N00004C3F; //0x1D04
	uint32_t N00003D98; //0x1D08
	uint32_t N00004C41; //0x1D0C
	uint32_t N00003D99; //0x1D10
	uint32_t N00004C43; //0x1D14
	uint32_t N00003D9A; //0x1D18
	uint32_t N00004C45; //0x1D1C
	uint32_t N00003D9B; //0x1D20
	uint32_t N00004C47; //0x1D24
	uint32_t N00003D9C; //0x1D28
	uint32_t N00004C49; //0x1D2C
	uint32_t N00003D9D; //0x1D30
	uint32_t N00004C4B; //0x1D34
	uint32_t N00003D9E; //0x1D38
	uint32_t N00004C4D; //0x1D3C
	uint32_t N00003D9F; //0x1D40
	uint32_t N00004C4F; //0x1D44
	uint32_t N00003DA0; //0x1D48
	uint32_t N00004C51; //0x1D4C
	uint32_t N00003DA1; //0x1D50
	uint32_t N00004C53; //0x1D54
	uint32_t N00003DA2; //0x1D58
	uint32_t N00004C55; //0x1D5C
	uint32_t N00003DA3; //0x1D60
	uint32_t N00004C57; //0x1D64
	uint32_t N00003DA4; //0x1D68
	uint32_t N00004C59; //0x1D6C
	uint32_t N00003DA5; //0x1D70
	uint32_t N00004C5B; //0x1D74
	uint32_t N00003DA6; //0x1D78
	uint32_t N00004C5D; //0x1D7C
	uint32_t N00003DA7; //0x1D80
	uint32_t N00004C5F; //0x1D84
	uint32_t N00003DA8; //0x1D88
	uint32_t N00004C61; //0x1D8C
	uint32_t N00003DA9; //0x1D90
	uint32_t N00004C63; //0x1D94
	uint32_t N00003DAA; //0x1D98
	uint32_t N00004C65; //0x1D9C
	uint32_t N00003DAB; //0x1DA0
	uint32_t N00004C67; //0x1DA4
	uint32_t N00003DAC; //0x1DA8
	uint32_t N00004C69; //0x1DAC
	uint32_t N00003DAD; //0x1DB0
	uint32_t N00004C6B; //0x1DB4
	uint32_t N00003DAE; //0x1DB8
	uint32_t N00004C6D; //0x1DBC
	uint32_t N00003DAF; //0x1DC0
	uint32_t N00004C6F; //0x1DC4
	uint32_t N00003DB0; //0x1DC8
	uint32_t N00004C71; //0x1DCC
	uint32_t N00003DB1; //0x1DD0
	uint32_t N00004C73; //0x1DD4
	uint32_t N00003DB2; //0x1DD8
	uint32_t N00004C75; //0x1DDC
	uint32_t N00003DB3; //0x1DE0
	uint32_t N00004C77; //0x1DE4
	uint32_t N00003DB4; //0x1DE8
	uint32_t N00004C79; //0x1DEC
	uint32_t N00003DB5; //0x1DF0
	uint32_t N00004C7B; //0x1DF4
	uint32_t N00003DB6; //0x1DF8
	uint32_t N00004C7D; //0x1DFC
	uint32_t N00003DB7; //0x1E00
	uint32_t N00004C7F; //0x1E04
	uint32_t N00003DB8; //0x1E08
	uint32_t N00004C81; //0x1E0C
	uint32_t N00003DB9; //0x1E10
	uint32_t N00004C83; //0x1E14
	uint32_t N00003DBA; //0x1E18
	uint32_t N00004C85; //0x1E1C
	uint32_t N00003DBB; //0x1E20
	uint32_t N00004C87; //0x1E24
	uint32_t N00003DBC; //0x1E28
	uint32_t N00004C89; //0x1E2C
	uint32_t N00003DBD; //0x1E30
	uint32_t N00004C8B; //0x1E34
	uint32_t N00003DBE; //0x1E38
	uint32_t N00004C8D; //0x1E3C
	uint32_t N00003DBF; //0x1E40
	uint32_t N00004C8F; //0x1E44
	uint32_t N00003DC0; //0x1E48
	uint32_t N00004C91; //0x1E4C
	uint32_t N00003DC1; //0x1E50
	uint32_t N00004C93; //0x1E54
	uint32_t N00003DC2; //0x1E58
	uint32_t N00004C95; //0x1E5C
	uint32_t N00003DC3; //0x1E60
	uint32_t N00004C97; //0x1E64
	uint32_t N00003DC4; //0x1E68
	uint32_t N00004C99; //0x1E6C
	uint32_t N00003DC5; //0x1E70
	uint32_t N00004C9B; //0x1E74
	uint32_t N00003DC6; //0x1E78
	uint32_t N00004C9D; //0x1E7C
	uint32_t N00003DC7; //0x1E80
	uint32_t N00004C9F; //0x1E84
	uint32_t N00003DC8; //0x1E88
	uint32_t N00004CA1; //0x1E8C
	uint32_t N00003DC9; //0x1E90
	uint32_t N00004CA3; //0x1E94
	uint32_t N00003DCA; //0x1E98
	uint32_t N00004CA5; //0x1E9C
	uint32_t N00003DCB; //0x1EA0
	uint32_t N00004CA7; //0x1EA4
	uint32_t N00003DCC; //0x1EA8
	uint32_t N00004CA9; //0x1EAC
	uint32_t N00003DCD; //0x1EB0
	uint32_t N00004CAB; //0x1EB4
	uint32_t N00003DCE; //0x1EB8
	uint32_t N00004CAD; //0x1EBC
	uint32_t N00003DCF; //0x1EC0
	uint32_t N00004CAF; //0x1EC4
	uint32_t N00003DD0; //0x1EC8
	uint32_t N00004CB1; //0x1ECC
	uint32_t N00003DD1; //0x1ED0
	uint32_t N00004CB3; //0x1ED4
	uint32_t N00003DD2; //0x1ED8
	uint32_t N00004CB5; //0x1EDC
	uint32_t N00003DD3; //0x1EE0
	uint32_t N00004CB7; //0x1EE4
	uint32_t N00003DD4; //0x1EE8
	uint32_t N00004CB9; //0x1EEC
	uint32_t N00003DD5; //0x1EF0
	uint32_t N00004CBB; //0x1EF4
	uint32_t N00003DD6; //0x1EF8
	uint32_t N00004CBD; //0x1EFC
	uint32_t N00003DD7; //0x1F00
	uint32_t N00004CBF; //0x1F04
	uint32_t N00003DD8; //0x1F08
	uint32_t N00004CC1; //0x1F0C
	uint32_t N00003DD9; //0x1F10
	uint32_t N00004CC3; //0x1F14
	uint32_t N00003DDA; //0x1F18
	uint32_t N00004CC5; //0x1F1C
	uint32_t N00003DDB; //0x1F20
	uint32_t N00004CC7; //0x1F24
	uint32_t N00003DDC; //0x1F28
	uint32_t N00004CC9; //0x1F2C
	uint32_t N00003DDD; //0x1F30
	uint32_t N00004CCB; //0x1F34
	uint32_t N00003DDE; //0x1F38
	uint32_t N00004CCD; //0x1F3C
	uint32_t N00003DDF; //0x1F40
	uint32_t N00004CCF; //0x1F44
	uint32_t N00003DE0; //0x1F48
	uint32_t N00004CD1; //0x1F4C
	uint32_t N00003DE1; //0x1F50
	uint32_t N00004CD3; //0x1F54
	uint32_t N00003DE2; //0x1F58
	uint32_t N00004CD5; //0x1F5C
	uint32_t N00003DE3; //0x1F60
	uint32_t N00004CD7; //0x1F64
	uint32_t N00003DE4; //0x1F68
	uint32_t N00004CD9; //0x1F6C
	uint32_t N00003DE5; //0x1F70
	uint32_t N00004CDB; //0x1F74
	uint32_t N00003DE6; //0x1F78
	uint32_t N00004CDD; //0x1F7C
	uint32_t N00003DE7; //0x1F80
	uint32_t N00004CDF; //0x1F84
	uint32_t N00003DE8; //0x1F88
	uint32_t N00004CE1; //0x1F8C
	uint32_t N00003DE9; //0x1F90
	uint32_t N00004CE3; //0x1F94
	uint32_t N00003DEA; //0x1F98
	uint32_t N00004CE5; //0x1F9C
	uint32_t N00003DEB; //0x1FA0
	uint32_t N00004CE7; //0x1FA4
	uint32_t N00003DEC; //0x1FA8
	uint32_t N00004CE9; //0x1FAC
	uint32_t N00003DED; //0x1FB0
	uint32_t N00004CEB; //0x1FB4
	uint32_t N00003DEE; //0x1FB8
	uint32_t N00004CED; //0x1FBC
	uint32_t N00003DEF; //0x1FC0
	uint32_t N00004CEF; //0x1FC4
	uint32_t N00003DF0; //0x1FC8
	uint32_t N00004CF1; //0x1FCC
	uint32_t N00003DF1; //0x1FD0
	uint32_t N00004CF3; //0x1FD4
	uint32_t N00003DF2; //0x1FD8
	uint32_t N00004CF5; //0x1FDC
	uint32_t N00003DF3; //0x1FE0
	uint32_t N00004CF7; //0x1FE4
	uint32_t N00003DF4; //0x1FE8
	uint32_t N00004CF9; //0x1FEC
	uint32_t N00003DF5; //0x1FF0
	uint32_t N00004CFB; //0x1FF4
	uint32_t N00003DF6; //0x1FF8
	uint32_t N00004CFD; //0x1FFC
	uint32_t N00003DF7; //0x2000
	uint32_t N00004CFF; //0x2004
	uint32_t N00003DF8; //0x2008
	uint32_t N00004D01; //0x200C
	uint32_t N00003DF9; //0x2010
	uint32_t N00004D03; //0x2014
	uint32_t N00003DFA; //0x2018
	uint32_t N00004D05; //0x201C
	uint32_t N00003DFB; //0x2020
	uint32_t N00004D07; //0x2024
	uint32_t N00003DFC; //0x2028
	uint32_t N00004D09; //0x202C
	uint32_t N00003DFD; //0x2030
	uint32_t N00004D0B; //0x2034
	uint32_t N00003DFE; //0x2038
	uint32_t N00004D0D; //0x203C
	uint32_t N00003DFF; //0x2040
	uint32_t N00004D0F; //0x2044
	uint32_t N00003E00; //0x2048
	uint32_t N00004D11; //0x204C
	uint32_t N00003E01; //0x2050
	uint32_t N00004D13; //0x2054
	uint32_t N00003E02; //0x2058
	uint32_t N00004D15; //0x205C
	uint32_t N00003E03; //0x2060
	uint32_t N00004D17; //0x2064
	uint32_t N00003E04; //0x2068
	uint32_t N00004D19; //0x206C
	uint32_t N00003E05; //0x2070
	uint32_t N00004D1B; //0x2074
	uint32_t N00003E06; //0x2078
	uint32_t N00004D1D; //0x207C
	uint32_t N00003E07; //0x2080
	uint32_t N00004D1F; //0x2084
	uint32_t N00003E08; //0x2088
	uint32_t r_FoodStockBread; //0x208C
	uint32_t r_FoodStockCheese; //0x2090
	uint32_t r_FoodStockMeat; //0x2094
	uint32_t r_FoodStockFruit; //0x2098
	uint32_t r_FoodStockTotal; //0x209C
	uint32_t r_PreferredFoodType; //0x20A0
	uint32_t N00004D27; //0x20A4
	uint32_t r_LastConsumedFoodType; //0x20A8
	uint32_t r_FoodTypeConsumptionIndex; //0x20AC
	uint32_t r_ConsumptionRateThisTick; //0x20B0
	uint32_t r_ConsumptionAccumulator; //0x20B4
	uint32_t Unknown; //0x20B8
	uint32_t N00004D2D; //0x20BC
	uint32_t N00003E0F; //0x20C0
	uint32_t N00004D2F; //0x20C4
	uint32_t N00003E10; //0x20C8
	uint32_t N00004D31; //0x20CC
	uint32_t N00003E11; //0x20D0
	uint32_t N00004D33; //0x20D4
	uint32_t N00003E12; //0x20D8
	uint32_t N00004D35; //0x20DC
	uint32_t N00003E13; //0x20E0
	uint32_t N00004D37; //0x20E4
	uint32_t N00003E14; //0x20E8
	uint32_t N00004D39; //0x20EC
	uint32_t N00003E15; //0x20F0
	uint32_t N00004D3B; //0x20F4
	uint32_t N00003E16; //0x20F8
	uint32_t N00004D3D; //0x20FC
	uint32_t N00003E17; //0x2100
	uint32_t N00004D3F; //0x2104
	uint32_t N00003E18; //0x2108
	uint32_t N00004D41; //0x210C
	uint32_t r_AleBonus; //0x2110
	uint32_t N00004D43; //0x2114
	uint32_t N00003E1A; //0x2118
	uint32_t r_ProductivityPercentage; //0x211C
	uint32_t N00003E1B; //0x2120
	uint32_t r_WorkingInns; //0x2124
	uint32_t N00003E1C; //0x2128
	uint32_t r_BadThingsNum; //0x212C
	uint32_t r_GoodThingsNum; //0x2130
	int32_t r_GoodBadThingBoost; //0x2134
	uint32_t r_GoodBadThingsForNextStage; //0x2138
	uint32_t N00004D4D; //0x213C
	uint32_t N00003E1F; //0x2140
	uint32_t N00004D4F; //0x2144
	uint32_t N00003E20; //0x2148
	uint32_t N00004D51; //0x214C
	uint32_t r_LastBoughtWallStoneCost; //0x2150
	uint32_t r_NextIncomeGoldAmount; //0x2154
	uint32_t r_NextIncomeProgress; //0x2158
	int32_t r_TaxPopularityModifier; //0x215C
	int32_t r_RationsPopularityModifier; //0x2160
	int32_t N00004D57; //0x2164
	uint32_t N00003E24; //0x2168
	uint32_t N00004D59; //0x216C
	uint32_t N00003E25; //0x2170
	uint32_t N00004D5B; //0x2174
	uint32_t N00003E26; //0x2178
	uint32_t N00004D5D; //0x217C
	uint32_t r_TotalPopulation; //0x2180
	uint32_t N00004D5F; //0x2184
	uint32_t r_TaxesMode; //0x2188
	uint32_t r_RationMode; //0x218C
	int32_t r_DaysUntilStarvation; //0x2190
	uint32_t N00004D63; //0x2194
	uint32_t N00003E2A; //0x2198
	uint32_t N00004D65; //0x219C
	uint32_t N00003E2B; //0x21A0
	uint32_t N00004D67; //0x21A4
	uint32_t N00003E2C; //0x21A8
	uint32_t N00004D69; //0x21AC
	uint32_t N00003E2D; //0x21B0
	uint32_t N00004D6B; //0x21B4
	uint32_t N00003E2E; //0x21B8
	uint32_t N00004D6D; //0x21BC
	uint32_t N00003E2F; //0x21C0
	uint32_t N00004D6F; //0x21C4
	uint32_t N00003E30; //0x21C8
	uint32_t N00004D71; //0x21CC
	uint32_t N00003E31; //0x21D0
	uint32_t N00004D73; //0x21D4
	uint32_t N00003E32; //0x21D8
	uint32_t N00004D75; //0x21DC
	uint32_t N00003E33; //0x21E0
	uint32_t N00004D77; //0x21E4
	uint32_t N00003E34; //0x21E8
	uint32_t N00004D79; //0x21EC
	uint32_t N00003E35; //0x21F0
	uint32_t N00004D7B; //0x21F4
	uint32_t r_LordUnitId; //0x21F8
	uint32_t r_LordUnitGlobalId; //0x21FC
	uint32_t N00003E37; //0x2200
	uint32_t N00004D7F; //0x2204
	uint32_t N00003E38; //0x2208
	uint32_t N00004D81; //0x220C
	uint32_t r_IsPaused; //0x2210
	uint32_t N00004D83; //0x2214
	uint32_t N00003E3A; //0x2218
	uint32_t r_GranaryChickens; //0x221C
	uint32_t N00003E3B; //0x2220
	uint32_t N00004D87; //0x2224
	uint32_t N00003E3C; //0x2228
	uint32_t N00004D89; //0x222C
	uint32_t N00003E3D; //0x2230
	uint32_t N00004D8B; //0x2234
	uint32_t r_BlessedPeople; //0x2238
	uint32_t r_NotBlessedPeople; //0x223C
	uint32_t r_BlessedCiviliansPercent2; //0x2240
	uint32_t r_WinLossState; //0x2244
	uint16_t N00003E40; //0x2248
	uint16_t N0000488D_2; //0x224A
	uint16_t N00004D91; //0x224C
	uint16_t N00004890; //0x224E
	uint16_t N00003E41; //0x2250
	uint16_t N0000924C; //0x2252
	uint32_t N00004D93; //0x2254
	uint32_t N00003E42; //0x2258
	uint32_t N00004D95; //0x225C
	uint32_t N00003E43; //0x2260
	uint32_t N00004D97; //0x2264
	uint32_t N00003E44; //0x2268
	uint32_t N00004D99; //0x226C
	uint32_t N00003E45; //0x2270
	uint16_t N00004D9B; //0x2274
	uint16_t N000047E5_2; //0x2276
	uint32_t N00003E46; //0x2278
	uint16_t N00004D9D; //0x227C
	uint16_t r_BlessedCiviliansPercent; //0x227E
	uint32_t N00003E47; //0x2280
	uint32_t N00004D9F; //0x2284
	uint32_t r_Chapels; //0x2288
	uint32_t r_Churches; //0x228C
	uint32_t r_Cathedrals; //0x2290
	uint32_t N00004DA3; //0x2294
	uint32_t r_Priests; //0x2298
	uint32_t N00004DA5; //0x229C
	uint32_t N00003E4B; //0x22A0
	uint32_t r_DrunkCiviliansPercent; //0x22A4
	uint32_t N00003E4C; //0x22A8
	uint32_t N00004DA9; //0x22AC
	uint32_t N00003E4D; //0x22B0
	uint32_t N00004DAB; //0x22B4
	uint32_t N00003E4E; //0x22B8
	uint32_t N00004DAD; //0x22BC
	uint32_t N00003E4F; //0x22C0
	uint32_t N00004DAF; //0x22C4
	uint32_t N00003E50; //0x22C8
	uint32_t N00004DB1; //0x22CC
	uint32_t r_InnsAmount; //0x22D0
	uint32_t N00004DB3; //0x22D4
	uint32_t N00003E52; //0x22D8
	uint32_t N00004DB5; //0x22DC
	uint32_t N00003E53; //0x22E0
	uint32_t N00004DB7; //0x22E4
	uint32_t N00003E54; //0x22E8
	uint32_t N00004DB9; //0x22EC
	uint32_t N00003E55; //0x22F0
	uint32_t N00004DBB; //0x22F4
	uint32_t N00003E56; //0x22F8
	uint32_t N00004DBD; //0x22FC
	uint32_t r_AILordMinusOne; //0x2300
	uint32_t N00004DBF; //0x2304
	uint32_t N00003E58; //0x2308
	uint32_t N00004DC1; //0x230C
	uint32_t N00003E59; //0x2310
	uint32_t N00004DC3; //0x2314
	uint32_t N00003E5A; //0x2318
	uint32_t N00004DC5; //0x231C
	uint32_t N00003E5B; //0x2320
	uint32_t N00004DC7; //0x2324
	uint32_t N00003E5C; //0x2328
	uint32_t N00004DC9; //0x232C
	uint32_t N00003E5D; //0x2330
	uint32_t N00004DCB; //0x2334
	uint32_t N00003E5E; //0x2338
	uint32_t N00004DCD; //0x233C
	uint32_t N00003E5F; //0x2340
	uint32_t N00004DCF; //0x2344
	uint32_t N00003E60; //0x2348
	uint32_t N00004DD1; //0x234C
	uint32_t N00003E61; //0x2350
	uint32_t N00004DD3; //0x2354
	uint32_t N00003E62; //0x2358
	uint32_t N00004DD5; //0x235C
	uint32_t N00003E63; //0x2360
	uint32_t N00004DD7; //0x2364
	uint32_t N00003E64; //0x2368
	uint32_t N00004DD9; //0x236C
	uint32_t N00003E65; //0x2370
	uint32_t N00004DDB; //0x2374
	uint32_t N00003E66; //0x2378
	uint32_t N00004DDD; //0x237C
	uint32_t N00003E67; //0x2380
	uint32_t N00004DDF; //0x2384
	uint32_t N00003E68; //0x2388
	uint32_t N00004DE1; //0x238C
	uint32_t N00003E69; //0x2390
	uint32_t N00004DE3; //0x2394
	uint32_t N00003E6A; //0x2398
	uint32_t N00004DE5; //0x239C
	uint32_t N00003E6B; //0x23A0
	uint32_t N00004DE7; //0x23A4
	uint32_t N00003E6C; //0x23A8
	uint32_t N00004DE9; //0x23AC
	uint32_t N00003E6D; //0x23B0
	uint32_t N00004DEB; //0x23B4
	uint32_t N00003E6E; //0x23B8
	uint32_t N00004DED; //0x23BC
	uint32_t N00003E6F; //0x23C0
	uint32_t N00004DEF; //0x23C4
	uint32_t N00003E70; //0x23C8
	uint32_t N00004DF1; //0x23CC
	uint32_t N00003E71; //0x23D0
	uint32_t N00004DF3; //0x23D4
	uint32_t N00003E72; //0x23D8
	uint32_t N00004DF5; //0x23DC
	uint32_t N00003E73; //0x23E0
	uint32_t N00004DF7; //0x23E4
	uint32_t N00003E74; //0x23E8
	uint32_t N00004DF9; //0x23EC
	uint32_t N00003E75; //0x23F0
	uint32_t N00004DFB; //0x23F4
	uint32_t N00003E76; //0x23F8
	uint32_t N00004DFD; //0x23FC
	uint32_t N00003E77; //0x2400
	uint32_t N00004DFF; //0x2404
	uint32_t N00003E78; //0x2408
	uint32_t N00004E01; //0x240C
	uint32_t N00003E79; //0x2410
	uint32_t N00004E03; //0x2414
	uint32_t N00003E7A; //0x2418
	uint32_t N00004E05; //0x241C
	uint32_t N00003E7B; //0x2420
	uint32_t N00004E07; //0x2424
	uint32_t N00003E7C; //0x2428
	uint32_t N00004E09; //0x242C
	uint32_t N00003E7D; //0x2430
	uint32_t N00004E0B; //0x2434
	uint32_t N00003E7E; //0x2438
	uint32_t N00004E0D; //0x243C
	uint32_t N00003E7F; //0x2440
	uint32_t N00004E0F; //0x2444
	uint32_t N00003E80; //0x2448
	uint32_t N00004E11; //0x244C
	uint32_t N00003E81; //0x2450
	uint32_t N00004E13; //0x2454
	uint32_t N00003E82; //0x2458
	uint32_t N00004E15; //0x245C
	uint32_t N00003E83; //0x2460
	uint32_t N00004E17; //0x2464
	uint32_t N00003E84; //0x2468
	uint32_t N00004E19; //0x246C
	uint32_t N00003E85; //0x2470
	uint32_t N00004E1B; //0x2474
	uint32_t N00003E86; //0x2478
	uint32_t N00004E1D; //0x247C
	uint32_t N00003E87; //0x2480
	uint32_t N00004E1F; //0x2484
	uint32_t N00003E88; //0x2488
	uint32_t N00004E21; //0x248C
	uint32_t N00003E89; //0x2490
	uint32_t N00004E23; //0x2494
	uint32_t N00003E8A; //0x2498
	uint32_t N00004E25; //0x249C
	uint32_t N00003E8B; //0x24A0
	uint32_t N00004E27; //0x24A4
	uint32_t N00003E8C; //0x24A8
	uint32_t N00004E29; //0x24AC
	uint32_t N00003E8D; //0x24B0
	uint32_t N00004E2B; //0x24B4
	uint32_t N00003E8E; //0x24B8
	uint32_t N00004E2D; //0x24BC
	uint32_t N00003E8F; //0x24C0
	uint32_t N00004E2F; //0x24C4
	uint32_t N00003E90; //0x24C8
	uint32_t N00004E31; //0x24CC
	uint32_t N00003E91; //0x24D0
	uint32_t N00004E33; //0x24D4
	uint32_t N00003E92; //0x24D8
	uint32_t N00004E35; //0x24DC
	uint32_t N00003E93; //0x24E0
	uint32_t N00004E37; //0x24E4
	uint32_t N00003E94; //0x24E8
	uint32_t N00004E39; //0x24EC
	uint32_t N00003E95; //0x24F0
	uint32_t N00004E3B; //0x24F4
	uint32_t N00003E96; //0x24F8
	uint32_t N00004E3D; //0x24FC
	uint32_t N00003E97; //0x2500
	uint32_t N00004E3F; //0x2504
	uint32_t N00003E98; //0x2508
	uint32_t N00004E41; //0x250C
	uint32_t N00003E99; //0x2510
	uint32_t N00004E43; //0x2514
	uint32_t N00003E9A; //0x2518
	uint32_t N00004E45; //0x251C
	uint32_t N00003E9B; //0x2520
	uint32_t N00004E47; //0x2524
	uint32_t N00003E9C; //0x2528
	uint32_t N00004E49; //0x252C
	uint32_t N00003E9D; //0x2530
	uint32_t N00004E4B; //0x2534
	uint32_t N00003E9E; //0x2538
	uint32_t N00004E4D; //0x253C
	uint32_t N00003E9F; //0x2540
	uint32_t N00004E4F; //0x2544
	uint32_t N00003EA0; //0x2548
	uint32_t N00004E51; //0x254C
	uint32_t N00003EA1; //0x2550
	uint32_t N00004E53; //0x2554
	uint32_t N00003EA2; //0x2558
	uint32_t N00004E55; //0x255C
	uint32_t N00003EA3; //0x2560
	uint32_t N00004E57; //0x2564
	uint32_t N00003EA4; //0x2568
	uint32_t N00004E59; //0x256C
	uint32_t N00003EA5; //0x2570
	uint32_t N00004E5B; //0x2574
	uint32_t N00003EA6; //0x2578
	uint32_t N00004E5D; //0x257C
	uint32_t N00003EA7; //0x2580
	uint32_t N00004E5F; //0x2584
	uint32_t N00003EA8; //0x2588
	uint32_t N00004E61; //0x258C
	uint32_t N00003EA9; //0x2590
	uint32_t N00004E63; //0x2594
	uint32_t N00003EAA; //0x2598
	uint32_t N00004E65; //0x259C
	uint32_t N00003EAB; //0x25A0
	uint32_t N00004E67; //0x25A4
	uint32_t N00003EAC; //0x25A8
	uint32_t N00004E69; //0x25AC
	uint32_t N00003EAD; //0x25B0
	uint32_t N00004E6B; //0x25B4
	uint32_t N00003EAE; //0x25B8
	uint32_t N00004E6D; //0x25BC
	uint32_t N00003EAF; //0x25C0
	uint32_t N00004E6F; //0x25C4
	uint32_t N00003EB0; //0x25C8
	uint32_t N00004E71; //0x25CC
	uint32_t N00003EB1; //0x25D0
	uint32_t N00004E73; //0x25D4
	uint32_t N00003EB2; //0x25D8
	uint32_t N00004E75; //0x25DC
	uint32_t N00003EB3; //0x25E0
	uint32_t N00004E77; //0x25E4
	uint32_t N00003EB4; //0x25E8
	uint32_t N00004E79; //0x25EC
	uint32_t N00003EB5; //0x25F0
	uint32_t N00004E7B; //0x25F4
	uint32_t N00003EB6; //0x25F8
	uint32_t N00004E7D; //0x25FC
	uint32_t N00003EB7; //0x2600
	uint32_t N00004E7F; //0x2604
	uint32_t N00003EB8; //0x2608
	uint32_t N00004E81; //0x260C
	uint32_t N00003EB9; //0x2610
	uint32_t N00004E83; //0x2614
	uint32_t N00003EBA; //0x2618
	uint32_t N00004E85; //0x261C
	uint32_t N00003EBB; //0x2620
	uint32_t N00004E87; //0x2624
	uint32_t N00003EBC; //0x2628
	uint32_t N00004E89; //0x262C
	uint32_t N00003EBD; //0x2630
	uint32_t N00004E8B; //0x2634
	uint32_t N00003EBE; //0x2638
	uint32_t N00004E8D; //0x263C
	uint32_t N00003EBF; //0x2640
	uint32_t N00004E8F; //0x2644
	uint32_t N00003EC0; //0x2648
	uint32_t N00004E91; //0x264C
	uint32_t N00003EC1; //0x2650
	uint32_t N00004E93; //0x2654
	uint32_t N00003EC2; //0x2658
	uint32_t N00004E95; //0x265C
	uint32_t N00003EC3; //0x2660
	uint32_t N00004E97; //0x2664
	uint32_t N00003EC4; //0x2668
	uint32_t N00004E99; //0x266C
	uint32_t N00003EC5; //0x2670
	uint32_t N00004E9B; //0x2674
	uint32_t N00003EC6; //0x2678
	uint32_t N00004E9D; //0x267C
	uint32_t N00003EC7; //0x2680
	uint32_t N00004E9F; //0x2684
	uint32_t N00003EC8; //0x2688
	uint32_t N00004EA1; //0x268C
	uint32_t N00003EC9; //0x2690
	uint32_t N00004EA3; //0x2694
	uint32_t N00003ECA; //0x2698
	uint32_t N00004EA5; //0x269C
	uint32_t N00003ECB; //0x26A0
	uint32_t N00004EA7; //0x26A4
	uint32_t N00003ECC; //0x26A8
	uint32_t N00004EA9; //0x26AC
	uint32_t N00003ECD; //0x26B0
	uint32_t N00004EAB; //0x26B4
	uint32_t N00003ECE; //0x26B8
	uint32_t N00004EAD; //0x26BC
	uint32_t N00003ECF; //0x26C0
	uint32_t N00004EAF; //0x26C4
	uint32_t N00003ED0; //0x26C8
	uint32_t N00004EB1; //0x26CC
	uint32_t N00003ED1; //0x26D0
	uint32_t N00004EB3; //0x26D4
	uint32_t N00003ED2; //0x26D8
	uint32_t N00004EB5; //0x26DC
	uint32_t N00003ED3; //0x26E0
	uint32_t N00004EB7; //0x26E4
	uint32_t N00003ED4; //0x26E8
	uint32_t N00004EB9; //0x26EC
	uint32_t N00003ED5; //0x26F0
	uint32_t N00004EBB; //0x26F4
	uint32_t N00003ED6; //0x26F8
	uint32_t N00004EBD; //0x26FC
	uint32_t N00003ED7; //0x2700
	uint32_t N00004EBF; //0x2704
	uint32_t N00003ED8; //0x2708
	uint32_t N00004EC1; //0x270C
	uint32_t N00003ED9; //0x2710
	uint32_t N00004EC3; //0x2714
	uint32_t N00003EDA; //0x2718
	uint32_t N00004EC5; //0x271C
	uint32_t N00003EDB; //0x2720
	uint32_t N00004EC7; //0x2724
	uint32_t N00003EDC; //0x2728
	uint32_t N00004EC9; //0x272C
	uint32_t N00003EDD; //0x2730
	uint32_t N00004ECB; //0x2734
	uint32_t N00003EDE; //0x2738
	uint32_t N00004ECD; //0x273C
	uint32_t N00003EDF; //0x2740
	uint32_t N00004ECF; //0x2744
	uint32_t N00003EE0; //0x2748
	uint32_t N00004ED1; //0x274C
	uint32_t N00003EE1; //0x2750
	uint32_t N00004ED3; //0x2754
	uint32_t N00003EE2; //0x2758
	uint32_t N00004ED5; //0x275C
	uint32_t N00003EE3; //0x2760
	uint32_t N00004ED7; //0x2764
	uint32_t N00003EE4; //0x2768
	uint32_t N00004ED9; //0x276C
	uint32_t N00003EE5; //0x2770
	uint32_t N00004EDB; //0x2774
	uint32_t N00003EE6; //0x2778
	uint32_t N00004EDD; //0x277C
	uint32_t N00003EE7; //0x2780
	uint32_t N00004EDF; //0x2784
	uint32_t N00003EE8; //0x2788
	uint32_t N00004EE1; //0x278C
	uint32_t N00003EE9; //0x2790
	uint32_t N00004EE3; //0x2794
	uint32_t N00003EEA; //0x2798
	uint32_t N00004EE5; //0x279C
	uint32_t N00003EEB; //0x27A0
	uint32_t N00004EE7; //0x27A4
	uint32_t N00003EEC; //0x27A8
	uint32_t N00004EE9; //0x27AC
	uint32_t N00003EED; //0x27B0
	uint32_t N00004EEB; //0x27B4
	uint32_t N00003EEE; //0x27B8
	uint32_t N00004EED; //0x27BC
	uint32_t N00003EEF; //0x27C0
	uint32_t N00004EEF; //0x27C4
	uint32_t N00003EF0; //0x27C8
	uint32_t N00004EF1; //0x27CC
	uint32_t N00003EF1; //0x27D0
	uint32_t N00004EF3; //0x27D4
	uint32_t N00003EF2; //0x27D8
	uint32_t N00004EF5; //0x27DC
	uint32_t N00003EF3; //0x27E0
	uint32_t N00004EF7; //0x27E4
	uint32_t N00003EF4; //0x27E8
	uint32_t N00004EF9; //0x27EC
	uint32_t N00003EF5; //0x27F0
	uint32_t N00004EFB; //0x27F4
	uint32_t N00003EF6; //0x27F8
	uint32_t N00004EFD; //0x27FC
	uint32_t N00003EF7; //0x2800
	uint32_t N00004EFF; //0x2804
	uint32_t N00003EF8; //0x2808
	uint32_t N00004F01; //0x280C
	uint32_t N00003EF9; //0x2810
	uint32_t N00004F03; //0x2814
	uint32_t N00003EFA; //0x2818
	uint32_t N00004F05; //0x281C
	uint32_t N00003EFB; //0x2820
	uint32_t N00004F07; //0x2824
	uint32_t N00003EFC; //0x2828
	uint32_t N00004F09; //0x282C
	uint32_t N00003EFD; //0x2830
	uint32_t N00004F0B; //0x2834
	uint32_t N00003EFE; //0x2838
	uint32_t N00004F0D; //0x283C
	uint32_t N00003EFF; //0x2840
	uint32_t N00004F0F; //0x2844
	uint32_t N00003F00; //0x2848
	uint32_t N00004F11; //0x284C
	uint32_t N00003F01; //0x2850
	uint32_t N00004F13; //0x2854
	uint32_t N00003F02; //0x2858
	uint32_t N00004F15; //0x285C
	uint32_t N00003F03; //0x2860
	uint32_t N00004F17; //0x2864
	uint32_t N00003F04; //0x2868
	uint32_t N00004F19; //0x286C
	uint32_t N00003F05; //0x2870
	uint32_t N00004F1B; //0x2874
	uint32_t N00003F06; //0x2878
	uint32_t N00004F1D; //0x287C
	uint32_t N00003F07; //0x2880
	uint32_t N00004F1F; //0x2884
	uint32_t N00003F08; //0x2888
	uint32_t N00004F21; //0x288C
	uint32_t N00003F09; //0x2890
	uint32_t N00004F23; //0x2894
	uint32_t N00003F0A; //0x2898
	uint32_t N00004F25; //0x289C
	uint32_t N00003F0B; //0x28A0
	uint32_t N00004F27; //0x28A4
	uint32_t N00003F0C; //0x28A8
	uint32_t N00004F29; //0x28AC
	uint32_t N00003F0D; //0x28B0
	uint32_t N00004F2B; //0x28B4
	uint32_t N00003F0E; //0x28B8
	uint32_t N00004F2D; //0x28BC
	uint32_t N00003F0F; //0x28C0
	uint32_t N00004F2F; //0x28C4
	uint32_t N00003F10; //0x28C8
	uint32_t N00004F31; //0x28CC
	uint32_t N00003F11; //0x28D0
	uint32_t N00004F33; //0x28D4
	uint32_t N00003F12; //0x28D8
	uint32_t N00004F35; //0x28DC
	uint32_t N00003F13; //0x28E0
	uint32_t N00004F37; //0x28E4
	uint32_t N00003F14; //0x28E8
	uint32_t N00004F39; //0x28EC
	uint32_t N00003F15; //0x28F0
	uint32_t N00004F3B; //0x28F4
	uint32_t N00003F16; //0x28F8
	uint32_t N00004F3D; //0x28FC
	uint32_t N00003F17; //0x2900
	uint32_t N00004F3F; //0x2904
	uint32_t N00003F18; //0x2908
	uint32_t N00004F41; //0x290C
	uint32_t N00003F19; //0x2910
	uint32_t N00004F43; //0x2914
	uint32_t N00003F1A; //0x2918
	uint32_t N00004F45; //0x291C
	uint32_t N00003F1B; //0x2920
	uint32_t N00004F47; //0x2924
	uint32_t N00003F1C; //0x2928
	uint32_t N00004F49; //0x292C
	uint32_t N00003F1D; //0x2930
	uint32_t N00004F4B; //0x2934
	uint32_t N00003F1E; //0x2938
	uint32_t N00004F4D; //0x293C
	uint32_t N00003F1F; //0x2940
	uint32_t N00004F4F; //0x2944
	uint32_t N00003F20; //0x2948
	uint32_t N00004F51; //0x294C
	uint32_t N00003F21; //0x2950
	uint32_t N00004F53; //0x2954
	uint32_t N00003F22; //0x2958
	uint32_t N00004F55; //0x295C
	uint32_t N00003F23; //0x2960
	uint32_t N00004F57; //0x2964
	uint32_t N00003F24; //0x2968
	uint32_t N00004F59; //0x296C
	uint32_t N00003F25; //0x2970
	uint32_t N00004F5B; //0x2974
	uint32_t N00003F26; //0x2978
	uint32_t N00004F5D; //0x297C
	uint32_t N00003F27; //0x2980
	uint32_t N00004F5F; //0x2984
	uint32_t N00003F28; //0x2988
	uint32_t N00004F61; //0x298C
	uint32_t N00003F29; //0x2990
	uint32_t N00004F63; //0x2994
	uint32_t N00003F2A; //0x2998
	uint32_t N00004F65; //0x299C
	uint32_t N00003F2B; //0x29A0
	uint32_t N00004F67; //0x29A4
	uint32_t N00003F2C; //0x29A8
	uint32_t N00004F69; //0x29AC
	uint32_t N00003F2D; //0x29B0
	uint32_t N00004F6B; //0x29B4
	uint32_t N00003F2E; //0x29B8
	uint32_t N00004F6D; //0x29BC
	uint32_t N00003F2F; //0x29C0
	uint32_t N00004F6F; //0x29C4
	uint32_t N00003F30; //0x29C8
	uint32_t N00004F71; //0x29CC
	uint32_t N00003F31; //0x29D0
	uint32_t N00004F73; //0x29D4
	uint32_t N00003F32; //0x29D8
	uint32_t N00004F75; //0x29DC
	uint32_t N00003F33; //0x29E0
	uint32_t N00004F77; //0x29E4
	uint32_t N00003F34; //0x29E8
	uint32_t N00004F79; //0x29EC
	uint32_t N00003F35; //0x29F0
	uint32_t N00004F7B; //0x29F4
	uint32_t N00003F36; //0x29F8
	uint32_t N00004F7D; //0x29FC
	uint32_t N00003F37; //0x2A00
	uint32_t N00004F7F; //0x2A04
	uint32_t N00003F38; //0x2A08
	uint32_t N00004F81; //0x2A0C
	uint32_t N00003F39; //0x2A10
	uint32_t N00004F83; //0x2A14
	uint32_t N00003F3A; //0x2A18
	uint32_t N00004F85; //0x2A1C
	uint32_t N00003F3B; //0x2A20
	uint32_t N00004F87; //0x2A24
	uint32_t N00003F3C; //0x2A28
	uint32_t N00004F89; //0x2A2C
	uint32_t N00003F3D; //0x2A30
	uint32_t N00004F8B; //0x2A34
	uint32_t N00003F3E; //0x2A38
	uint32_t N00004F8D; //0x2A3C
	uint32_t N00003F3F; //0x2A40
	uint32_t N00004F8F; //0x2A44
	uint32_t N00003F40; //0x2A48
	uint32_t N00004F91; //0x2A4C
	uint32_t N00003F41; //0x2A50
	uint32_t r_AICurrentFarmType; //0x2A54
	uint32_t N00003F42; //0x2A58
	uint32_t N00004F95; //0x2A5C
	uint32_t N00003F43; //0x2A60
	uint32_t N00004F97; //0x2A64
	uint32_t N00003F44; //0x2A68
	uint32_t N00004F99; //0x2A6C
	uint32_t N00003F45; //0x2A70
	uint32_t N00004F9B; //0x2A74
	uint32_t N00003F46; //0x2A78
	uint32_t N00004F9D; //0x2A7C
	uint32_t N00003F47; //0x2A80
	uint32_t N00004F9F; //0x2A84
	uint32_t N00003F48; //0x2A88
	uint32_t N00004FA1; //0x2A8C
	uint32_t N00003F49; //0x2A90
	uint32_t N00004FA3; //0x2A94
	uint32_t N00003F4A; //0x2A98
	uint32_t N00004FA5; //0x2A9C
	uint32_t N00003F4B; //0x2AA0
	uint32_t N00004FA7; //0x2AA4
	uint32_t N00003F4C; //0x2AA8
	uint32_t N00004FA9; //0x2AAC
	uint32_t N00003F4D; //0x2AB0
	uint32_t N00004FAB; //0x2AB4
	uint32_t N00003F4E; //0x2AB8
	uint32_t N00004FAD; //0x2ABC
	uint32_t N00003F4F; //0x2AC0
	uint32_t N00004FAF; //0x2AC4
	uint32_t N00003F50; //0x2AC8
	uint32_t N00004FB1; //0x2ACC
	uint32_t N00003F51; //0x2AD0
	uint32_t N00004FB3; //0x2AD4
	uint32_t N00003F52; //0x2AD8
	uint32_t N00004FB5; //0x2ADC
	uint32_t N00003F53; //0x2AE0
	uint32_t N00004FB7; //0x2AE4
	uint32_t r_IdlePeasantAverageP1; //0x2AE8
	uint32_t r_IdlePeasantAverageP2; //0x2AEC
	uint32_t r_IdlePeasantAverageP3; //0x2AF0
	uint32_t r_IdlePeasantAverageP4; //0x2AF4
	uint32_t r_IdlePeasantAverageP5; //0x2AF8
	uint32_t r_IdlePeasantAverageP6; //0x2AFC
	uint32_t r_IdlePeasantAverageP7; //0x2B00
	uint32_t r_IdlePeasantAverageP8; //0x2B04
	uint32_t r_IdlePeasantAverage; //0x2B08
	uint32_t r_IdlePeasantCurrent; //0x2B0C
	uint32_t N00003F59; //0x2B10
	uint32_t N00004FC3; //0x2B14
	uint32_t r_WoodcuttersHutsInactive; //0x2B18
	uint32_t r_WoodcuttersHutsActive; //0x2B1C
	uint32_t r_WoodcutterWorstDistance; //0x2B20
	uint32_t r_WoodcutterWorstBuildingId; //0x2B24
	uint32_t N00003F5C; //0x2B28
	uint32_t N00004FC9; //0x2B2C
	uint32_t N00003F5D; //0x2B30
	uint32_t N00004FCB; //0x2B34
	uint32_t N00003F5E; //0x2B38
	uint32_t N00004FCD; //0x2B3C
	uint32_t N00003F5F; //0x2B40
	uint32_t N00004FCF; //0x2B44
	uint32_t N00003F60; //0x2B48
	uint32_t N00004FD1; //0x2B4C
	uint32_t N00003F61; //0x2B50
	uint32_t N00004FD3; //0x2B54
	uint32_t N00003F62; //0x2B58
	uint32_t N00004FD5; //0x2B5C
	uint32_t N00003F63; //0x2B60
	uint32_t N00004FD7; //0x2B64
	uint32_t N00003F64; //0x2B68
	uint32_t N00004FD9; //0x2B6C
	uint32_t N00003F65; //0x2B70
	uint32_t N00004FDB; //0x2B74
	uint32_t N00003F66; //0x2B78
	uint32_t r_AISiegeRetreatTileId; //0x2B7C
	uint32_t r_AISiegeRetreatTileX; //0x2B80
	uint32_t r_AISiegeRetreatTileY; //0x2B84
	uint32_t N00003F68; //0x2B88
	uint32_t N00004FE1; //0x2B8C
	uint32_t N00003F69; //0x2B90
	uint32_t N00004FE3; //0x2B94
	uint32_t N00003F6A; //0x2B98
	uint32_t N00004FE5; //0x2B9C
	uint32_t N00003F6B; //0x2BA0
	uint32_t N00004FE7; //0x2BA4
	uint32_t N00003F6C; //0x2BA8
	uint32_t N00004FE9; //0x2BAC
	uint32_t N00003F6D; //0x2BB0
	uint32_t N00004FEB; //0x2BB4
	uint32_t N00003F6E; //0x2BB8
	uint32_t N00004FED; //0x2BBC
	uint32_t N00003F6F; //0x2BC0
	uint32_t N00004FEF; //0x2BC4
	uint32_t N00003F70; //0x2BC8
	uint32_t N00004FF1; //0x2BCC
	uint32_t N00003F71; //0x2BD0
	uint32_t N00004FF3; //0x2BD4
	uint32_t r_AISiegePlayerIdTarget; //0x2BD8
	uint32_t N00004FF5; //0x2BDC
	uint32_t N00003F73; //0x2BE0
	uint32_t N00004FF7; //0x2BE4
	uint32_t N00003F74; //0x2BE8
	uint32_t N00004FF9; //0x2BEC
	uint32_t N00003F75; //0x2BF0
	uint32_t N00004FFB; //0x2BF4
	uint32_t N00003F76; //0x2BF8
	uint32_t N00004FFD; //0x2BFC
	uint32_t N00003F77; //0x2C00
	uint32_t N00004FFF; //0x2C04
	uint32_t N00003F78; //0x2C08
	uint32_t N00005001; //0x2C0C
	uint32_t N00003F79; //0x2C10
	uint32_t N00005003; //0x2C14
	uint32_t N00003F7A; //0x2C18
	uint32_t N00005005; //0x2C1C
	uint32_t N00003F7B; //0x2C20
	uint32_t N00005007; //0x2C24
	uint32_t N00003F7C; //0x2C28
	uint32_t N00005009; //0x2C2C
	uint32_t N00003F7D; //0x2C30
	uint32_t N0000500B; //0x2C34
	uint32_t N00003F7E; //0x2C38
	uint32_t N0000500D; //0x2C3C
	uint32_t N00003F7F; //0x2C40
	uint32_t N0000500F; //0x2C44
	uint32_t N00003F80; //0x2C48
	uint32_t N00005011; //0x2C4C
	uint32_t N00003F81; //0x2C50
	uint32_t N00005013; //0x2C54
	uint32_t N00003F82; //0x2C58
	uint32_t N00005015; //0x2C5C
	uint32_t N00003F83; //0x2C60
	uint32_t N00005017; //0x2C64
	uint32_t N00003F84; //0x2C68
	uint32_t N00005019; //0x2C6C
	uint32_t N00003F85; //0x2C70
	uint32_t N0000501B; //0x2C74
	uint32_t N00003F86; //0x2C78
	uint32_t N0000501D; //0x2C7C
	uint32_t N00003F87; //0x2C80
	uint32_t N0000501F; //0x2C84
	uint32_t N00003F88; //0x2C88
	uint32_t N00005021; //0x2C8C
	uint32_t N00003F89; //0x2C90
	uint32_t N00005023; //0x2C94
	uint32_t N00003F8A; //0x2C98
	uint32_t N00005025; //0x2C9C
	uint32_t N00003F8B; //0x2CA0
	uint32_t N00005027; //0x2CA4
	uint32_t N00003F8C; //0x2CA8
	uint32_t N00005029; //0x2CAC
	uint32_t N00003F8D; //0x2CB0
	uint32_t N0000502B; //0x2CB4
	uint32_t N00003F8E; //0x2CB8
	uint32_t N0000502D; //0x2CBC
	uint32_t N00003F8F; //0x2CC0
	uint32_t N0000502F; //0x2CC4
	uint32_t N00003F90; //0x2CC8
	uint32_t N00005031; //0x2CCC
	uint32_t N00003F91; //0x2CD0
	uint32_t N00005033; //0x2CD4
	uint32_t N00003F92; //0x2CD8
	uint32_t N00005035; //0x2CDC
	uint32_t N00003F93; //0x2CE0
	uint32_t N00005037; //0x2CE4
	uint32_t N00003F94; //0x2CE8
	uint32_t N00005039; //0x2CEC
	uint32_t N00003F95; //0x2CF0
	uint32_t N0000503B; //0x2CF4
	uint32_t N00003F96; //0x2CF8
	uint32_t N0000503D; //0x2CFC
	uint32_t N00003F97; //0x2D00
	uint32_t N0000503F; //0x2D04
	uint32_t N00003F98; //0x2D08
	uint32_t N00005041; //0x2D0C
	uint32_t N00003F99; //0x2D10
	uint32_t N00005043; //0x2D14
	uint32_t N00003F9A; //0x2D18
	uint32_t N00005045; //0x2D1C
	uint32_t N00003F9B; //0x2D20
	uint32_t N00005047; //0x2D24
	uint32_t N00003F9C; //0x2D28
	uint32_t N00005049; //0x2D2C
	uint32_t N00003F9D; //0x2D30
	uint32_t N0000504B; //0x2D34
	uint32_t N00003F9E; //0x2D38
	uint32_t N0000504D; //0x2D3C
	uint32_t N00003F9F; //0x2D40
	uint32_t N0000504F; //0x2D44
	uint32_t N00003FA0; //0x2D48
	uint32_t N00005051; //0x2D4C
	uint32_t N00003FA1; //0x2D50
	uint32_t N00005053; //0x2D54
	uint32_t N00003FA2; //0x2D58
	uint32_t N00005055; //0x2D5C
	uint32_t N00003FA3; //0x2D60
	uint32_t N00005057; //0x2D64
	uint32_t N00003FA4; //0x2D68
	uint32_t N00005059; //0x2D6C
	uint32_t N00003FA5; //0x2D70
	uint32_t N0000505B; //0x2D74
	uint32_t N00003FA6; //0x2D78
	uint32_t N0000505D; //0x2D7C
	uint32_t N00003FA7; //0x2D80
	uint32_t N0000505F; //0x2D84
	uint32_t N00003FA8; //0x2D88
	uint32_t N00005061; //0x2D8C
	uint32_t N00003FA9; //0x2D90
	uint32_t N00005063; //0x2D94
	uint32_t N00003FAA; //0x2D98
	uint32_t N00005065; //0x2D9C
	uint32_t N00003FAB; //0x2DA0
	uint32_t N00005067; //0x2DA4
	uint32_t N00003FAC; //0x2DA8
	uint32_t N00005069; //0x2DAC
	uint32_t N00003FAD; //0x2DB0
	uint32_t N0000506B; //0x2DB4
	uint32_t N00003FAE; //0x2DB8
	uint32_t N0000506D; //0x2DBC
	uint32_t N00003FAF; //0x2DC0
	uint32_t N0000506F; //0x2DC4
	uint32_t N00003FB0; //0x2DC8
	uint32_t N00005071; //0x2DCC
	uint32_t N00003FB1; //0x2DD0
	uint32_t N00005073; //0x2DD4
	uint32_t N00003FB2; //0x2DD8
	uint32_t N00005075; //0x2DDC
	uint32_t N00003FB3; //0x2DE0
	uint32_t N00005077; //0x2DE4
	uint32_t N00003FB4; //0x2DE8
	uint32_t N00005079; //0x2DEC
	uint32_t N00003FB5; //0x2DF0
	uint32_t N0000507B; //0x2DF4
	uint32_t N00003FB6; //0x2DF8
	uint32_t N0000507D; //0x2DFC
	uint32_t N00003FB7; //0x2E00
	uint32_t N0000507F; //0x2E04
	uint32_t N00003FB8; //0x2E08
	uint32_t N00005081; //0x2E0C
	uint32_t N00003FB9; //0x2E10
	uint32_t N00005083; //0x2E14
	uint32_t N00003FBA; //0x2E18
	uint32_t N00005085; //0x2E1C
	uint32_t N00003FBB; //0x2E20
	uint32_t N00005087; //0x2E24
	uint32_t N00003FBC; //0x2E28
	uint32_t N00005089; //0x2E2C
	uint32_t N00003FBD; //0x2E30
	uint32_t N0000508B; //0x2E34
	uint32_t N00003FBE; //0x2E38
	uint32_t N0000508D; //0x2E3C
	uint32_t N00003FBF; //0x2E40
	uint32_t N0000508F; //0x2E44
	uint32_t N00003FC0; //0x2E48
	uint32_t N00005091; //0x2E4C
	uint32_t N00003FC1; //0x2E50
	uint32_t N00005093; //0x2E54
	uint32_t N00003FC2; //0x2E58
	uint32_t N00005095; //0x2E5C
	uint32_t N00003FC3; //0x2E60
	uint32_t N00005097; //0x2E64
	uint32_t N00003FC4; //0x2E68
	uint32_t N00005099; //0x2E6C
	uint32_t N00003FC5; //0x2E70
	uint32_t N0000509B; //0x2E74
	uint32_t N00003FC6; //0x2E78
	uint32_t N0000509D; //0x2E7C
	uint32_t N00003FC7; //0x2E80
	uint32_t N0000509F; //0x2E84
	uint32_t N00003FC8; //0x2E88
	uint32_t N000050A1; //0x2E8C
	uint32_t N00003FC9; //0x2E90
	uint32_t N000050A3; //0x2E94
	uint32_t N00003FCA; //0x2E98
	uint32_t N000050A5; //0x2E9C
	uint32_t N00003FCB; //0x2EA0
	uint32_t N000050A7; //0x2EA4
	uint32_t N00003FCC; //0x2EA8
	uint32_t N000050A9; //0x2EAC
	uint32_t N00003FCD; //0x2EB0
	uint32_t N000050AB; //0x2EB4
	uint32_t N00003FCE; //0x2EB8
	uint32_t N000050AD; //0x2EBC
	uint32_t N00003FCF; //0x2EC0
	uint32_t N000050AF; //0x2EC4
	uint32_t N00003FD0; //0x2EC8
	uint32_t N000050B1; //0x2ECC
	uint32_t N00003FD1; //0x2ED0
	uint32_t N000050B3; //0x2ED4
	uint32_t N00003FD2; //0x2ED8
	uint32_t N000050B5; //0x2EDC
	uint32_t N00003FD3; //0x2EE0
	uint32_t N000050B7; //0x2EE4
	uint32_t N00003FD4; //0x2EE8
	uint32_t N000050B9; //0x2EEC
	uint32_t N00003FD5; //0x2EF0
	uint32_t N000050BB; //0x2EF4
	uint32_t N00003FD6; //0x2EF8
	uint32_t N000050BD; //0x2EFC
	uint32_t N00003FD7; //0x2F00
	uint32_t N000050BF; //0x2F04
	uint32_t N00003FD8; //0x2F08
	uint32_t N000050C1; //0x2F0C
	uint32_t N00003FD9; //0x2F10
	uint32_t N000050C3; //0x2F14
	uint32_t N00003FDA; //0x2F18
	uint32_t N000050C5; //0x2F1C
	uint32_t N00003FDB; //0x2F20
	uint32_t N000050C7; //0x2F24
	uint32_t N00003FDC; //0x2F28
	uint32_t N000050C9; //0x2F2C
	uint32_t N00003FDD; //0x2F30
	uint32_t N000050CB; //0x2F34
	uint32_t N00003FDE; //0x2F38
	uint32_t N000050CD; //0x2F3C
	uint32_t N00003FDF; //0x2F40
	uint32_t N000050CF; //0x2F44
	uint32_t N00003FE0; //0x2F48
	uint32_t N000050D1; //0x2F4C
	uint32_t N00003FE1; //0x2F50
	uint32_t N000050D3; //0x2F54
	uint32_t N00003FE2; //0x2F58
	uint32_t N000050D5; //0x2F5C
	uint32_t N00003FE3; //0x2F60
	uint32_t N000050D7; //0x2F64
	uint32_t N00003FE4; //0x2F68
	uint32_t N000050D9; //0x2F6C
	uint32_t N00003FE5; //0x2F70
	uint32_t N000050DB; //0x2F74
	uint32_t N00003FE6; //0x2F78
	uint32_t N000050DD; //0x2F7C
	uint32_t N00003FE7; //0x2F80
	uint32_t N000050DF; //0x2F84
	uint32_t N00003FE8; //0x2F88
	uint32_t N000050E1; //0x2F8C
	uint32_t N00003FE9; //0x2F90
	uint32_t N000050E3; //0x2F94
	uint32_t N00003FEA; //0x2F98
	uint32_t N000050E5; //0x2F9C
	uint32_t N00003FEB; //0x2FA0
	uint32_t N000050E7; //0x2FA4
	uint32_t N00003FEC; //0x2FA8
	uint32_t N000050E9; //0x2FAC
	uint32_t N00003FED; //0x2FB0
	uint32_t N000050EB; //0x2FB4
	uint32_t N00003FEE; //0x2FB8
	uint32_t N000050ED; //0x2FBC
	uint32_t N00003FEF; //0x2FC0
	uint32_t N000050EF; //0x2FC4
	uint32_t N00003FF0; //0x2FC8
	uint32_t N000050F1; //0x2FCC
	uint32_t N00003FF1; //0x2FD0
	uint32_t N000050F3; //0x2FD4
	uint32_t N00003FF2; //0x2FD8
	uint32_t N000050F5; //0x2FDC
	uint32_t N00003FF3; //0x2FE0
	uint32_t N000050F7; //0x2FE4
	uint32_t N00003FF4; //0x2FE8
	uint32_t N000050F9; //0x2FEC
	uint32_t N00003FF5; //0x2FF0
	uint32_t N000050FB; //0x2FF4
	uint32_t N00003FF6; //0x2FF8
	uint32_t N000050FD; //0x2FFC
	uint32_t N00003FF7; //0x3000
	uint32_t N000050FF; //0x3004
	uint32_t N00003FF8; //0x3008
	uint32_t N00005101; //0x300C
	uint32_t N00003FF9; //0x3010
	uint32_t N00005103; //0x3014
	uint32_t N00003FFA; //0x3018
	uint32_t N00005105; //0x301C
	uint32_t N00003FFB; //0x3020
	uint32_t N00005107; //0x3024
	uint32_t N00003FFC; //0x3028
	uint32_t N00005109; //0x302C
	uint32_t N00003FFD; //0x3030
	uint32_t N0000510B; //0x3034
	uint32_t N00003FFE; //0x3038
	uint32_t N0000510D; //0x303C
	uint32_t N00003FFF; //0x3040
	uint32_t N0000510F; //0x3044
	uint32_t N00004000; //0x3048
	uint32_t N00005111; //0x304C
	uint32_t N00004001; //0x3050
	uint32_t N00005113; //0x3054
	uint32_t N00004002; //0x3058
	uint32_t N00005115; //0x305C
	uint32_t N00004003; //0x3060
	uint32_t N00005117; //0x3064
	uint32_t N00004004; //0x3068
	uint32_t N00005119; //0x306C
	uint32_t N00004005; //0x3070
	uint32_t N0000511B; //0x3074
	uint32_t N00004006; //0x3078
	uint32_t N0000511D; //0x307C
	uint32_t N00004007; //0x3080
	uint32_t N0000511F; //0x3084
	uint32_t N00004008; //0x3088
	uint32_t N00005121; //0x308C
	uint32_t N00004009; //0x3090
	uint32_t N00005123; //0x3094
	uint32_t N0000400A; //0x3098
	uint32_t N00005125; //0x309C
	uint32_t N0000400B; //0x30A0
	uint32_t N00005127; //0x30A4
	uint32_t N0000400C; //0x30A8
	uint32_t N00005129; //0x30AC
	uint32_t N0000400D; //0x30B0
	uint32_t N0000512B; //0x30B4
	uint32_t N0000400E; //0x30B8
	uint32_t N0000512D; //0x30BC
	uint32_t N0000400F; //0x30C0
	uint32_t N0000512F; //0x30C4
	uint32_t N00004010; //0x30C8
	uint32_t N00005131; //0x30CC
	uint32_t N00004011; //0x30D0
	uint32_t N00005133; //0x30D4
	uint32_t N00004012; //0x30D8
	uint32_t N00005135; //0x30DC
	uint32_t N00004013; //0x30E0
	uint32_t r_UnitsWithTribeRole0; //0x30E4
	uint32_t r_UnitsWithTribeRole1Or4; //0x30E8
	uint32_t r_UnitsWithTribeRole2; //0x30EC
	uint32_t N00004015; //0x30F0
	uint32_t N0000513B; //0x30F4
	uint32_t N00004016; //0x30F8
	uint32_t N0000513D; //0x30FC
	uint32_t N00004017; //0x3100
	uint32_t N0000513F; //0x3104
	uint32_t N00004018; //0x3108
	uint32_t N00005141; //0x310C
	uint32_t N00004019; //0x3110
	uint32_t N00005143; //0x3114
	uint32_t N0000401A; //0x3118
	uint32_t N00005145; //0x311C
	uint32_t N0000401B; //0x3120
	uint32_t N00005147; //0x3124
	uint32_t N0000401C; //0x3128
	uint32_t N00005149; //0x312C
	uint32_t N0000401D; //0x3130
	uint32_t N0000514B; //0x3134
	uint32_t N0000401E; //0x3138
	uint32_t N0000514D; //0x313C
	uint32_t N0000401F; //0x3140
	uint32_t N0000514F; //0x3144
	uint32_t N00004020; //0x3148
	uint32_t N00005151; //0x314C
	uint32_t N00004021; //0x3150
	uint32_t N00005153; //0x3154
	uint32_t N00004022; //0x3158
	uint32_t N00005155; //0x315C
	uint32_t N00004023; //0x3160
	uint32_t N00005157; //0x3164
	uint32_t N00004024; //0x3168
	uint32_t N00005159; //0x316C
	uint32_t N00004025; //0x3170
	uint32_t N0000515B; //0x3174
	uint32_t N00004026; //0x3178
	uint32_t N0000515D; //0x317C
	uint32_t N00004027; //0x3180
	uint32_t N0000515F; //0x3184
	uint32_t N00004028; //0x3188
	uint32_t N00005161; //0x318C
	uint32_t N00004029; //0x3190
	uint32_t N00005163; //0x3194
	uint32_t N0000402A; //0x3198
	uint32_t N00005165; //0x319C
	uint32_t N0000402B; //0x31A0
	uint32_t N00005167; //0x31A4
	uint32_t N0000402C; //0x31A8
	uint32_t N00005169; //0x31AC
	uint32_t N0000402D; //0x31B0
	uint32_t N0000516B; //0x31B4
	uint32_t N0000402E; //0x31B8
	uint32_t N0000516D; //0x31BC
	uint32_t N0000402F; //0x31C0
	uint32_t N0000516F; //0x31C4
	uint32_t N00004030; //0x31C8
	uint32_t N00005171; //0x31CC
	uint32_t N00004031; //0x31D0
	uint32_t N00005173; //0x31D4
	uint32_t N00004032; //0x31D8
	uint32_t N00005175; //0x31DC
	uint32_t N00004033; //0x31E0
	uint32_t N00005177; //0x31E4
	uint32_t N00004034; //0x31E8
	uint32_t N00005179; //0x31EC
	uint32_t N00004035; //0x31F0
	uint32_t N0000517B; //0x31F4
	uint32_t N00004036; //0x31F8
	uint32_t N0000517D; //0x31FC
	uint32_t N00004037; //0x3200
	uint32_t N0000517F; //0x3204
	uint32_t N00004038; //0x3208
	uint32_t N00005181; //0x320C
	uint32_t N00004039; //0x3210
	uint32_t N00005183; //0x3214
	uint32_t N0000403A; //0x3218
	uint32_t N00005185; //0x321C
	uint32_t N0000403B; //0x3220
	uint32_t N00005187; //0x3224
	uint32_t N0000403C; //0x3228
	uint32_t N00005189; //0x322C
	uint32_t N0000403D; //0x3230
	uint32_t N0000518B; //0x3234
	uint32_t N0000403E; //0x3238
	uint32_t N0000518D; //0x323C
	uint32_t N0000403F; //0x3240
	uint32_t N0000518F; //0x3244
	uint32_t N00004040; //0x3248
	uint32_t N00005191; //0x324C
	uint32_t N00004041; //0x3250
	uint32_t N00005193; //0x3254
	uint32_t N00004042; //0x3258
	uint32_t N00005195; //0x325C
	uint32_t N00004043; //0x3260
	uint32_t N00005197; //0x3264
	uint32_t N00004044; //0x3268
	uint32_t N00005199; //0x326C
	uint32_t N00004045; //0x3270
	uint32_t N0000519B; //0x3274
	uint32_t N00004046; //0x3278
	uint32_t N0000519D; //0x327C
	uint32_t N00004047; //0x3280
	uint32_t N0000519F; //0x3284
	uint32_t N00004048; //0x3288
	uint32_t N000051A1; //0x328C
	uint32_t N00004049; //0x3290
	uint32_t N000051A3; //0x3294
	uint32_t N0000404A; //0x3298
	uint32_t N000051A5; //0x329C
	uint32_t N0000404B; //0x32A0
	uint32_t N000051A7; //0x32A4
	uint32_t N0000404C; //0x32A8
	uint32_t N000051A9; //0x32AC
	uint32_t N0000404D; //0x32B0
	uint32_t N000051AB; //0x32B4
	uint32_t N0000404E; //0x32B8
	uint32_t N000051AD; //0x32BC
	uint32_t N0000404F; //0x32C0
	uint32_t N000051AF; //0x32C4
	uint32_t N00004050; //0x32C8
	uint32_t N000051B1; //0x32CC
	uint32_t N00004051; //0x32D0
	uint32_t N000051B3; //0x32D4
	uint32_t N00004052; //0x32D8
	uint32_t N000051B5; //0x32DC
	uint32_t N00004053; //0x32E0
	uint32_t N000051B7; //0x32E4
	uint32_t N00004054; //0x32E8
	uint32_t N000051B9; //0x32EC
	uint32_t N00004055; //0x32F0
	uint32_t N000051BB; //0x32F4
	uint32_t N00004056; //0x32F8
	uint32_t N000051BD; //0x32FC
	uint32_t N00004057; //0x3300
	uint32_t N000051BF; //0x3304
	uint32_t N00004058; //0x3308
	uint32_t N000051C1; //0x330C
	uint32_t N00004059; //0x3310
	uint32_t N000051C3; //0x3314
	uint32_t N0000405A; //0x3318
	uint32_t N000051C5; //0x331C
	uint32_t N0000405B; //0x3320
	uint32_t N000051C7; //0x3324
	uint32_t N0000405C; //0x3328
	uint32_t N000051C9; //0x332C
	uint32_t N0000405D; //0x3330
	uint32_t N000051CB; //0x3334
	uint32_t N0000405E; //0x3338
	uint32_t N000051CD; //0x333C
	uint32_t N0000405F; //0x3340
	uint32_t N000051CF; //0x3344
	uint32_t N00004060; //0x3348
	uint32_t N000051D1; //0x334C
	uint32_t N00004061; //0x3350
	uint32_t N000051D3; //0x3354
	uint32_t N00004062; //0x3358
	uint32_t N000051D5; //0x335C
	uint32_t N00004063; //0x3360
	uint32_t N000051D7; //0x3364
	uint32_t N00004064; //0x3368
	uint32_t N000051D9; //0x336C
	uint32_t N00004065; //0x3370
	uint32_t N000051DB; //0x3374
	uint32_t N00004066; //0x3378
	uint32_t N000051DD; //0x337C
	uint32_t N00004067; //0x3380
	uint32_t N000051DF; //0x3384
	uint32_t N00004068; //0x3388
	uint32_t N000051E1; //0x338C
	uint32_t N00004069; //0x3390
	uint32_t N000051E3; //0x3394
	uint32_t N0000406A; //0x3398
	uint32_t N000051E5; //0x339C
	uint32_t N0000406B; //0x33A0
	uint32_t N000051E7; //0x33A4
	uint32_t N0000406C; //0x33A8
	uint32_t N000051E9; //0x33AC
	uint32_t N0000406D; //0x33B0
	uint32_t N000051EB; //0x33B4
	uint32_t N0000406E; //0x33B8
	uint32_t N000051ED; //0x33BC
	uint32_t N0000406F; //0x33C0
	uint32_t N000051EF; //0x33C4
	uint32_t N00004070; //0x33C8
	uint32_t N000051F1; //0x33CC
	uint32_t N00004071; //0x33D0
	uint32_t N000051F3; //0x33D4
	uint32_t N00004072; //0x33D8
	uint32_t N000051F5; //0x33DC
	uint32_t N00004073; //0x33E0
	uint32_t N000051F7; //0x33E4
	uint32_t N00004074; //0x33E8
	uint32_t N000051F9; //0x33EC
	uint32_t N00004075; //0x33F0
	uint32_t N000051FB; //0x33F4
	uint32_t N00004076; //0x33F8
	uint32_t N000051FD; //0x33FC
	uint32_t N00004077; //0x3400
	uint32_t N000051FF; //0x3404
	uint32_t N00004078; //0x3408
	uint32_t N00005201; //0x340C
	uint32_t N00004079; //0x3410
	uint32_t N00005203; //0x3414
	uint32_t N0000407A; //0x3418
	uint32_t N00005205; //0x341C
	uint32_t N0000407B; //0x3420
	uint32_t N00005207; //0x3424
	uint32_t N0000407C; //0x3428
	uint32_t N00005209; //0x342C
	uint32_t N0000407D; //0x3430
	uint32_t N0000520B; //0x3434
	uint32_t N0000407E; //0x3438
	uint32_t N0000520D; //0x343C
	uint32_t N0000407F; //0x3440
	uint32_t N0000520F; //0x3444
	uint32_t N00004080; //0x3448
	uint32_t N00005211; //0x344C
	uint32_t N00004081; //0x3450
	uint32_t N00005213; //0x3454
	uint32_t N00004082; //0x3458
	uint32_t N00005215; //0x345C
	uint32_t N00004083; //0x3460
	uint32_t N00005217; //0x3464
	uint32_t N00004084; //0x3468
	uint32_t N00005219; //0x346C
	uint32_t N00004085; //0x3470
	uint32_t N0000521B; //0x3474
	uint32_t N00004086; //0x3478
	uint32_t N0000521D; //0x347C
	uint32_t N00004087; //0x3480
	uint32_t N0000521F; //0x3484
	uint32_t N00004088; //0x3488
	uint32_t N00005221; //0x348C
	uint32_t N00004089; //0x3490
	uint32_t N00005223; //0x3494
	uint32_t N0000408A; //0x3498
	uint32_t N00005225; //0x349C
	uint32_t N0000408B; //0x34A0
	uint32_t N00005227; //0x34A4
	uint32_t N0000408C; //0x34A8
	uint32_t N00005229; //0x34AC
	uint32_t N0000408D; //0x34B0
	uint32_t N0000522B; //0x34B4
	uint32_t N0000408E; //0x34B8
	uint32_t N0000522D; //0x34BC
	uint32_t N0000408F; //0x34C0
	uint32_t N0000522F; //0x34C4
	uint32_t N00004090; //0x34C8
	uint32_t N00005231; //0x34CC
	uint32_t N00004091; //0x34D0
	uint32_t N00005233; //0x34D4
	uint32_t N00004092; //0x34D8
	uint32_t N00005235; //0x34DC
	uint32_t N00004093; //0x34E0
	uint32_t N00005237; //0x34E4
	uint32_t N00004094; //0x34E8
	uint32_t N00005239; //0x34EC
	uint32_t N00004095; //0x34F0
	uint32_t N0000523B; //0x34F4
	uint32_t N00004096; //0x34F8
	uint32_t N0000523D; //0x34FC
	uint32_t N00004097; //0x3500
	uint32_t N0000523F; //0x3504
	uint32_t N00004098; //0x3508
	uint32_t N00005241; //0x350C
	uint32_t N00004099; //0x3510
	uint32_t N00005243; //0x3514
	uint32_t N0000409A; //0x3518
	uint32_t N00005245; //0x351C
	uint32_t N0000409B; //0x3520
	uint32_t N00005247; //0x3524
	uint32_t N0000409C; //0x3528
	uint32_t N00005249; //0x352C
	uint32_t N0000409D; //0x3530
	uint32_t N0000524B; //0x3534
	uint32_t N0000409E; //0x3538
	uint32_t N0000524D; //0x353C
	uint32_t N0000409F; //0x3540
	uint32_t N0000524F; //0x3544
	uint32_t N000040A0; //0x3548
	uint32_t N00005251; //0x354C
	uint32_t N000040A1; //0x3550
	uint32_t N00005253; //0x3554
	uint32_t N000040A2; //0x3558
	uint32_t N00005255; //0x355C
	uint32_t N000040A3; //0x3560
	uint32_t N00005257; //0x3564
	uint32_t N000040A4; //0x3568
	uint32_t N00005259; //0x356C
	uint32_t N000040A5; //0x3570
	uint32_t N0000525B; //0x3574
	uint32_t N000040A6; //0x3578
	uint32_t N0000525D; //0x357C
	uint32_t N000040A7; //0x3580
	uint32_t N0000525F; //0x3584
	uint32_t N000040A8; //0x3588
	uint32_t N00005261; //0x358C
	uint32_t N000040A9; //0x3590
	uint32_t N00005263; //0x3594
	uint32_t N000040AA; //0x3598
	uint32_t N00005265; //0x359C
	uint32_t N000040AB; //0x35A0
	uint32_t N00005267; //0x35A4
	uint32_t N000040AC; //0x35A8
	uint32_t N00005269; //0x35AC
	uint32_t N000040AD; //0x35B0
	uint32_t N0000526B; //0x35B4
	uint32_t N000040AE; //0x35B8
	uint32_t N0000526D; //0x35BC
	uint32_t N000040AF; //0x35C0
	uint32_t N0000526F; //0x35C4
	uint32_t N000040B0; //0x35C8
	uint32_t N00005271; //0x35CC
	uint32_t N000040B1; //0x35D0
	uint32_t N00005273; //0x35D4
	uint32_t N000040B2; //0x35D8
	uint32_t N00005275; //0x35DC
	uint32_t N000040B3; //0x35E0
	uint32_t N00005277; //0x35E4
	uint32_t N000040B4; //0x35E8
	uint32_t N00005279; //0x35EC
	uint32_t N000040B5; //0x35F0
	uint32_t N0000527B; //0x35F4
	uint32_t N000040B6; //0x35F8
	uint32_t N0000527D; //0x35FC
	uint32_t N000040B7; //0x3600
	uint32_t N0000527F; //0x3604
	uint32_t N000040B8; //0x3608
	uint32_t N00005281; //0x360C
	uint32_t N000040B9; //0x3610
	uint32_t N00005283; //0x3614
	uint32_t N000040BA; //0x3618
	uint32_t N00005285; //0x361C
	uint32_t N000040BB; //0x3620
	uint32_t N00005287; //0x3624
	uint32_t N000040BC; //0x3628
	uint32_t N00005289; //0x362C
	uint32_t N000040BD; //0x3630
	uint32_t N0000528B; //0x3634
	uint32_t N000040BE; //0x3638
	uint32_t N0000528D; //0x363C
	uint32_t N000040BF; //0x3640
	uint32_t N0000528F; //0x3644
	uint32_t N000040C0; //0x3648
	uint32_t N00005291; //0x364C
	uint32_t N000040C1; //0x3650
	uint32_t N00005293; //0x3654
	uint32_t N000040C2; //0x3658
	uint32_t N00005295; //0x365C
	uint32_t N000040C3; //0x3660
	uint32_t N00005297; //0x3664
	uint32_t N000040C4; //0x3668
	uint32_t N00005299; //0x366C
	uint32_t N000040C5; //0x3670
	uint32_t N0000529B; //0x3674
	uint32_t N000040C6; //0x3678
	uint32_t N0000529D; //0x367C
	uint32_t N000040C7; //0x3680
	uint32_t N0000529F; //0x3684
	uint32_t N000040C8; //0x3688
	uint32_t N000052A1; //0x368C
	uint32_t N000040C9; //0x3690
	uint32_t N000052A3; //0x3694
	uint32_t N000040CA; //0x3698
	uint32_t N000052A5; //0x369C
	uint32_t N000040CB; //0x36A0
	uint32_t N000052A7; //0x36A4
	uint32_t N000040CC; //0x36A8
	uint32_t N000052A9; //0x36AC
	uint32_t N000040CD; //0x36B0
	uint32_t N000052AB; //0x36B4
	uint32_t N000040CE; //0x36B8
	uint32_t N000052AD; //0x36BC
	uint32_t N000040CF; //0x36C0
	uint32_t N000052AF; //0x36C4
	uint32_t N000040D0; //0x36C8
	uint32_t N000052B1; //0x36CC
	uint32_t N000040D1; //0x36D0
	uint32_t N000052B3; //0x36D4
	uint32_t N000040D2; //0x36D8
	uint32_t N000052B5; //0x36DC
	uint32_t N000040D3; //0x36E0
	uint32_t N000052B7; //0x36E4
	uint32_t N000040D4; //0x36E8
	uint32_t N000052B9; //0x36EC
	uint32_t N000040D5; //0x36F0
	uint32_t N000052BB; //0x36F4
	uint32_t N000040D6; //0x36F8
	uint32_t N000052BD; //0x36FC
	uint32_t N000040D7; //0x3700
	uint32_t N000052BF; //0x3704
	uint32_t N000040D8; //0x3708
	uint32_t N000052C1; //0x370C
	uint32_t N000040D9; //0x3710
	uint32_t N000052C3; //0x3714
	uint32_t N000040DA; //0x3718
	uint32_t N000052C5; //0x371C
	uint32_t N000040DB; //0x3720
	uint32_t N000052C7; //0x3724
	uint32_t N000040DC; //0x3728
	uint32_t N000052C9; //0x372C
	uint32_t N000040DD; //0x3730
	uint32_t N000052CB; //0x3734
	uint32_t N000040DE; //0x3738
	uint32_t N000052CD; //0x373C
	uint32_t N000040DF; //0x3740
	uint32_t N000052CF; //0x3744
	uint32_t N000040E0; //0x3748
	uint32_t N000052D1; //0x374C
	uint32_t N000040E1; //0x3750
	uint32_t N000052D3; //0x3754
	uint32_t N000040E2; //0x3758
	uint32_t N000052D5; //0x375C
	uint32_t N000040E3; //0x3760
	uint32_t N000052D7; //0x3764
	uint32_t N000040E4; //0x3768
	uint32_t N000052D9; //0x376C
	uint32_t N000040E5; //0x3770
	uint32_t N000052DB; //0x3774
	uint32_t N000040E6; //0x3778
	uint32_t N000052DD; //0x377C
	uint32_t N000040E7; //0x3780
	uint32_t N000052DF; //0x3784
	uint32_t N000040E8; //0x3788
	uint32_t N000052E1; //0x378C
	uint32_t N000040E9; //0x3790
	uint32_t N000052E3; //0x3794
	uint32_t N000040EA; //0x3798
	uint32_t N000052E5; //0x379C
	uint32_t N000040EB; //0x37A0
	uint32_t N000052E7; //0x37A4
	uint32_t N000040EC; //0x37A8
	uint32_t N000052E9; //0x37AC
	uint32_t N000040ED; //0x37B0
	uint32_t N000052EB; //0x37B4
	uint32_t N000040EE; //0x37B8
	uint32_t N000052ED; //0x37BC
	uint32_t N000040EF; //0x37C0
	uint32_t N000052EF; //0x37C4
	uint32_t N000040F0; //0x37C8
	uint32_t N000052F1; //0x37CC
	uint32_t N000040F1; //0x37D0
	uint32_t N000052F3; //0x37D4
	uint32_t N000040F2; //0x37D8
	uint32_t N000052F5; //0x37DC
	uint32_t N000040F3; //0x37E0
	uint32_t N000052F7; //0x37E4
	uint32_t N000040F4; //0x37E8
	uint32_t N000052F9; //0x37EC
	uint32_t N000040F5; //0x37F0
	uint32_t N000052FB; //0x37F4
	uint32_t N000040F6; //0x37F8
	uint32_t N000052FD; //0x37FC
	uint32_t N000040F7; //0x3800
	uint32_t N000052FF; //0x3804
	uint32_t N000040F8; //0x3808
	uint32_t N00005301; //0x380C
	uint32_t N000040F9; //0x3810
	uint32_t N00005303; //0x3814
	uint32_t N000040FA; //0x3818
	uint32_t N00005305; //0x381C
	uint32_t N000040FB; //0x3820
	uint32_t N00005307; //0x3824
	uint32_t N000040FC; //0x3828
	uint32_t N00005309; //0x382C
	uint32_t N000040FD; //0x3830
	uint32_t N0000530B; //0x3834
	uint32_t N000040FE; //0x3838
	uint32_t N0000530D; //0x383C
	uint32_t N000040FF; //0x3840
	uint32_t N0000530F; //0x3844
	uint32_t N00004100; //0x3848
	uint32_t N00005311; //0x384C
	uint32_t N00004101; //0x3850
	uint32_t N00005313; //0x3854
	uint32_t r_AICowUsageTimer; //0x3858
	uint32_t N00005315; //0x385C
	uint32_t N00004103; //0x3860
	uint32_t N00005317; //0x3864
	uint32_t r_AIUnknownTimer; //0x3868
	uint32_t N00005319; //0x386C
	uint32_t N00004105; //0x3870
	uint32_t N0000531B; //0x3874
	uint32_t N00004106; //0x3878
	uint32_t N0000531D; //0x387C
	uint32_t N00004107; //0x3880
	uint32_t N0000531F; //0x3884
	uint32_t N00004108; //0x3888
	uint32_t N00005321; //0x388C
	uint32_t N00004109; //0x3890
	uint32_t N00005323; //0x3894
	uint32_t N0000410A; //0x3898
	uint32_t N00005325; //0x389C
	uint32_t r_AITargetAttackForceSize; //0x38A0
	uint32_t N00005327; //0x38A4
	uint32_t N0000410C; //0x38A8
	uint32_t N00005329; //0x38AC
	uint32_t N0000410D; //0x38B0
	uint32_t N0000532B; //0x38B4
	uint32_t N0000410E; //0x38B8
	uint32_t N0000532D; //0x38BC
	uint32_t N0000410F; //0x38C0
	uint32_t N0000532F; //0x38C4
	uint32_t N00004110; //0x38C8
	uint32_t N00005331; //0x38CC
	uint32_t N00004111; //0x38D0
	uint32_t N00005333; //0x38D4
	uint32_t N00004112; //0x38D8
	uint32_t N00005335; //0x38DC
	uint32_t N00004113; //0x38E0
	uint32_t N00005337; //0x38E4
	uint32_t N00004114; //0x38E8
	uint32_t N00005339; //0x38EC
	uint32_t N00004115; //0x38F0
	uint32_t N0000533B; //0x38F4
	uint32_t N00004116; //0x38F8
	uint32_t N0000533D; //0x38FC
	uint32_t N00004117; //0x3900
	uint32_t N0000533F; //0x3904
	uint32_t N00004118; //0x3908
	uint32_t N00005341; //0x390C
	uint32_t N00004119; //0x3910
	uint32_t r_HarassEngineTypeIndex; //0x3914
	uint32_t r_HarassDeploymentTimer; //0x3918
	uint32_t r_HarassMachineCount; //0x391C
	uint32_t N0000411B; //0x3920
	uint32_t r_AISiegeAttempts; //0x3924
	uint32_t r_FarmsAmount; //0x3928
	uint32_t N00005349; //0x392C
	uint32_t N0000411D; //0x3930
	uint32_t r_EconomyProtectionRequiredPositionTileX; //0x3934
	uint32_t r_EconomyProtectionRequiredPositionTileY; //0x3938
	uint32_t N0000534D; //0x393C
	uint32_t N0000411F; //0x3940
	uint32_t N0000534F; //0x3944
	uint32_t N00004120; //0x3948
	uint32_t N00005351; //0x394C
	uint32_t N00004121; //0x3950
	uint32_t N00005353; //0x3954
	uint32_t N00004122; //0x3958
	uint32_t N00005355; //0x395C
	uint32_t N00004123; //0x3960
	uint32_t N00005357; //0x3964
	uint32_t N00004124; //0x3968
	uint32_t N00005359; //0x396C
	uint32_t N00004125; //0x3970
	uint32_t N0000535B; //0x3974
	uint32_t N00004126; //0x3978
	uint32_t N0000535D; //0x397C
	uint32_t N00004127; //0x3980
	uint32_t N0000535F; //0x3984
	uint32_t N00004128; //0x3988
	uint32_t r_IronMineAmount; //0x398C
	uint32_t N00004129; //0x3990
	uint32_t r_QuarryAmount; //0x3994
	uint32_t r_DistanceToNearestAI; //0x3998
	uint32_t N00005365; //0x399C
	uint32_t N0000412B; //0x39A0
	uint32_t N00005367; //0x39A4
	uint32_t N0000412C; //0x39A8
	uint32_t N00005369; //0x39AC
	uint32_t N0000412D; //0x39B0
	uint32_t N0000536B; //0x39B4
	uint32_t N0000412E; //0x39B8
	uint32_t N0000536D; //0x39BC
	uint32_t N0000412F; //0x39C0
	uint32_t N0000536F; //0x39C4
	uint32_t N00004130; //0x39C8
	uint32_t N00005371; //0x39CC
	uint32_t N00004131; //0x39D0
	uint32_t r_ExtremePowersMana; //0x39D4
	uint32_t N00004132; //0x39D8
	uint32_t N00005375; //0x39DC
	uint32_t N00004133; //0x39E0
	uint32_t N00005377; //0x39E4
	uint32_t N00004134; //0x39E8
	uint32_t N00005379; //0x39EC
	uint32_t N00004135; //0x39F0
	uint32_t N0000537B; //0x39F4
	uint32_t N00004136; //0x39F8
	uint32_t N0000537D; //0x39FC
	uint32_t N00004137; //0x3A00
	uint32_t N0000537F; //0x3A04
	uint32_t N00004138; //0x3A08
	uint32_t N00005381; //0x3A0C
	uint32_t N00004139; //0x3A10
	uint32_t N00005383; //0x3A14
	uint32_t N0000413A; //0x3A18
	uint32_t N00005385; //0x3A1C
	uint32_t N0000413B; //0x3A20
	uint32_t N00005387; //0x3A24
	uint32_t N0000413C; //0x3A28
	uint32_t N00005389; //0x3A2C
	uint32_t N0000413D; //0x3A30
	uint32_t N0000538B; //0x3A34
	uint32_t N0000413E; //0x3A38
	uint32_t N0000538D; //0x3A3C
	uint32_t N0000413F; //0x3A40
	uint32_t N0000538F; //0x3A44
	uint32_t N00004140; //0x3A48
	uint32_t N00005391; //0x3A4C
	uint32_t N00004141; //0x3A50
	uint32_t N00005393; //0x3A54
	uint32_t N00004142; //0x3A58
	uint32_t N00005395; //0x3A5C
	uint32_t N00004143; //0x3A60
	uint32_t N00005397; //0x3A64
	uint32_t N00004144; //0x3A68
	uint32_t N00005399; //0x3A6C
	uint32_t N00004145; //0x3A70
	uint32_t N0000539B; //0x3A74
	uint32_t N00004146; //0x3A78
	uint32_t N0000539D; //0x3A7C
	uint32_t N00004147; //0x3A80
	uint32_t N0000539F; //0x3A84
	uint32_t N00004148; //0x3A88
	uint32_t N000053A1; //0x3A8C
	uint32_t N00004149; //0x3A90
	uint32_t N000053A3; //0x3A94
	uint32_t N0000414A; //0x3A98
	uint32_t N000053A5; //0x3A9C
	uint32_t N0000414B; //0x3AA0
	uint32_t N000053A7; //0x3AA4
	uint32_t N0000414C; //0x3AA8
	uint32_t N000053A9; //0x3AAC
	uint32_t N0000414D; //0x3AB0
	uint32_t N000053AB; //0x3AB4
	uint32_t N0000414E; //0x3AB8
	uint32_t N000053AD; //0x3ABC
	uint32_t N0000414F; //0x3AC0
	uint32_t N000053AF; //0x3AC4
	uint32_t N00004150; //0x3AC8
	uint32_t N000053B1; //0x3ACC
	uint32_t N00004151; //0x3AD0
	uint32_t N000053B3; //0x3AD4
	uint32_t N00004152; //0x3AD8
	uint32_t N000053B5; //0x3ADC
	uint32_t N00004153; //0x3AE0
	uint32_t N000053B7; //0x3AE4
	uint32_t N00004154; //0x3AE8
	uint32_t N000053B9; //0x3AEC
	uint32_t N00004155; //0x3AF0
	uint32_t N000053BB; //0x3AF4
	uint32_t N00004156; //0x3AF8
	uint32_t N000053BD; //0x3AFC
	uint32_t N00004157; //0x3B00
	uint32_t N000053BF; //0x3B04
	uint32_t N00004158; //0x3B08
	uint32_t N000053C1; //0x3B0C
	uint32_t N00004159; //0x3B10
	uint32_t N000053C3; //0x3B14
	uint32_t N0000415A; //0x3B18
	uint32_t N000053C5; //0x3B1C
	uint32_t N0000415B; //0x3B20
	uint32_t N000053C7; //0x3B24
	uint32_t N0000415C; //0x3B28
	uint32_t N000053C9; //0x3B2C
	uint32_t N0000415D; //0x3B30
	uint32_t N000053CB; //0x3B34
	uint32_t N0000415E; //0x3B38
	uint32_t N000053CD; //0x3B3C
	uint32_t N0000415F; //0x3B40
	uint32_t N000053CF; //0x3B44
	uint32_t N00004160; //0x3B48
	uint32_t N000053D1; //0x3B4C
	uint32_t N00004161; //0x3B50
	uint32_t N000053D3; //0x3B54
	uint32_t N00004162; //0x3B58
	uint32_t N000053D5; //0x3B5C
	uint32_t N00004163; //0x3B60
	uint32_t N000053D7; //0x3B64
	uint32_t N00004164; //0x3B68
	uint32_t N000053D9; //0x3B6C
	uint32_t N00004165; //0x3B70
	uint32_t N000053DB; //0x3B74
	uint32_t N00004166; //0x3B78
	uint32_t N000053DD; //0x3B7C
	uint32_t N00004167; //0x3B80
	uint32_t N000053DF; //0x3B84
	uint32_t N00004168; //0x3B88
	uint32_t N000053E1; //0x3B8C
	uint32_t N00004169; //0x3B90
	uint32_t N000053E3; //0x3B94
	uint32_t N0000416A; //0x3B98
	uint32_t N000053E5; //0x3B9C
	uint32_t N0000416B; //0x3BA0
	uint32_t N000053E7; //0x3BA4
	uint32_t N0000416C; //0x3BA8
	uint32_t N000053E9; //0x3BAC
	uint32_t N0000416D; //0x3BB0
	uint32_t N000053EB; //0x3BB4
	uint32_t N0000416E; //0x3BB8
	uint32_t N000053ED; //0x3BBC
	uint32_t N0000416F; //0x3BC0
	uint32_t N000053EF; //0x3BC4
	uint32_t N00004170; //0x3BC8
	uint32_t N000053F1; //0x3BCC
	uint32_t N00004171; //0x3BD0
	uint32_t N000053F3; //0x3BD4
	uint32_t N00004172; //0x3BD8
	uint32_t N000053F5; //0x3BDC
	uint32_t N00004173; //0x3BE0
	uint32_t N000053F7; //0x3BE4
	uint32_t N00004174; //0x3BE8
	uint32_t N000053F9; //0x3BEC
	uint32_t N00004175; //0x3BF0
	uint32_t N000053FB; //0x3BF4
	uint32_t N00004176; //0x3BF8
	uint32_t N000053FD; //0x3BFC
	uint32_t N00004177; //0x3C00
	uint32_t N000053FF; //0x3C04
	uint32_t N00004178; //0x3C08
	uint32_t N00005401; //0x3C0C
	uint32_t N00004179; //0x3C10
	uint32_t N00005403; //0x3C14
	uint32_t N0000417A; //0x3C18
	uint32_t N00005405; //0x3C1C
	uint32_t N0000417B; //0x3C20
	uint32_t N00005407; //0x3C24
	uint32_t N0000417C; //0x3C28
	uint32_t N00005409; //0x3C2C
	uint32_t N0000417D; //0x3C30
	uint32_t N0000540B; //0x3C34
	uint32_t N0000417E; //0x3C38
	uint32_t N0000540D; //0x3C3C
	uint32_t N0000417F; //0x3C40
	uint32_t N0000540F; //0x3C44
	uint32_t N00004180; //0x3C48
	uint32_t N00005411; //0x3C4C
	uint32_t N00004181; //0x3C50
	uint32_t N00005413; //0x3C54
	uint32_t N00004182; //0x3C58
	uint32_t N00005415; //0x3C5C
	uint32_t N00004183; //0x3C60
	uint32_t N00005417; //0x3C64
	uint32_t N00004184; //0x3C68
	uint32_t N00005419; //0x3C6C
	uint32_t N00004185; //0x3C70
	uint32_t N0000541B; //0x3C74
	uint32_t N00004186; //0x3C78
	uint32_t N0000541D; //0x3C7C
	uint32_t N00004187; //0x3C80
	uint32_t N0000541F; //0x3C84
	uint32_t N00004188; //0x3C88
	uint32_t N00005421; //0x3C8C
	uint32_t N00004189; //0x3C90
	uint32_t N00005423; //0x3C94
	uint32_t N0000418A; //0x3C98
	uint32_t N00005425; //0x3C9C
	uint32_t N0000418B; //0x3CA0
	uint32_t N00005427; //0x3CA4
	uint32_t N0000418C; //0x3CA8
	uint32_t N00005429; //0x3CAC
	uint32_t N0000418D; //0x3CB0
	uint32_t N0000542B; //0x3CB4
	uint32_t N0000418E; //0x3CB8
	uint32_t N0000542D; //0x3CBC
	uint32_t N0000418F; //0x3CC0
	uint32_t N0000542F; //0x3CC4
	uint32_t N00004190; //0x3CC8
	uint32_t N00005431; //0x3CCC
	uint32_t N00004191; //0x3CD0
	uint32_t N00005433; //0x3CD4
	uint32_t N00004192; //0x3CD8
	uint32_t N00005435; //0x3CDC
	uint32_t N00004193; //0x3CE0
	uint32_t N00005437; //0x3CE4
	uint32_t N00004194; //0x3CE8
	uint32_t N00005439; //0x3CEC
	uint32_t N00004195; //0x3CF0
	uint32_t N0000543B; //0x3CF4
	uint32_t N00004196; //0x3CF8
	uint32_t N0000543D; //0x3CFC
	uint32_t N00004197; //0x3D00
	uint32_t N0000543F; //0x3D04
	uint32_t N00004198; //0x3D08
	uint32_t N00005441; //0x3D0C
	uint32_t N00004199; //0x3D10
	uint32_t N00005443; //0x3D14
	uint32_t N0000419A; //0x3D18
	uint32_t N00005445; //0x3D1C
	uint32_t N0000419B; //0x3D20
	uint32_t N00005447; //0x3D24
	uint32_t N0000419C; //0x3D28
	uint32_t N00005449; //0x3D2C
	uint32_t N0000419D; //0x3D30
	uint32_t N0000544B; //0x3D34
	uint32_t N0000419E; //0x3D38
	uint32_t N0000544D; //0x3D3C
	uint32_t N0000419F; //0x3D40
	uint32_t N0000544F; //0x3D44
	uint32_t N000041A0; //0x3D48
	uint32_t N00005451; //0x3D4C
	uint32_t N000041A1; //0x3D50
	uint32_t N00005453; //0x3D54
	uint32_t N000041A2; //0x3D58
	uint32_t N00005455; //0x3D5C
	uint32_t N000041A3; //0x3D60
	uint32_t N00005457; //0x3D64
	uint32_t N000041A4; //0x3D68
	uint32_t N00005459; //0x3D6C
	uint32_t N000041A5; //0x3D70
	uint32_t N0000545B; //0x3D74
	uint32_t N000041A6; //0x3D78
	uint32_t N0000545D; //0x3D7C
	uint32_t N000041A7; //0x3D80
	uint32_t N0000545F; //0x3D84
	uint32_t N000041A8; //0x3D88
	uint32_t N00005461; //0x3D8C
	uint32_t N000041A9; //0x3D90
	uint32_t N00005463; //0x3D94
	uint32_t N000041AA; //0x3D98
	uint32_t N00005465; //0x3D9C
	uint32_t N000041AB; //0x3DA0
	uint32_t N00005467; //0x3DA4
	uint32_t N000041AC; //0x3DA8
	uint32_t N00005469; //0x3DAC
	uint32_t N000041AD; //0x3DB0
	uint32_t N0000546B; //0x3DB4
	uint32_t N000041AE; //0x3DB8
	uint32_t N0000546D; //0x3DBC
	uint32_t N000041AF; //0x3DC0
	uint32_t N0000546F; //0x3DC4
	uint32_t N000041B0; //0x3DC8
	uint32_t N00005471; //0x3DCC
	uint32_t N000041B1; //0x3DD0
	uint32_t N00005473; //0x3DD4
	uint32_t N000041B2; //0x3DD8
	uint32_t N00005475; //0x3DDC
	uint32_t N000041B3; //0x3DE0
	uint32_t N00005477; //0x3DE4
	uint32_t N000041B4; //0x3DE8
	uint32_t N00005479; //0x3DEC
	uint32_t N000041B5; //0x3DF0
	uint32_t N0000547B; //0x3DF4
	uint32_t N000041B6; //0x3DF8
	uint32_t N0000547D; //0x3DFC
	uint32_t N000041B7; //0x3E00
	uint32_t N0000547F; //0x3E04
	uint32_t N000041B8; //0x3E08
	uint32_t N00005481; //0x3E0C
	uint32_t N000041B9; //0x3E10
	uint32_t N00005483; //0x3E14
	uint32_t N000041BA; //0x3E18
	uint32_t N00005485; //0x3E1C
	uint32_t N000041BB; //0x3E20
	uint32_t N00005487; //0x3E24
	uint32_t N000041BC; //0x3E28
	uint32_t N00005489; //0x3E2C
	uint32_t N000041BD; //0x3E30
	uint32_t N0000548B; //0x3E34
	uint32_t N000041BE; //0x3E38
	uint32_t N0000548D; //0x3E3C
	uint32_t N000041BF; //0x3E40
	uint32_t N0000548F; //0x3E44
	uint32_t N000041C0; //0x3E48
	uint32_t N00005491; //0x3E4C
	uint32_t N000041C1; //0x3E50
	uint32_t N00005493; //0x3E54
	uint32_t N000041C2; //0x3E58
	uint32_t N00005495; //0x3E5C
	uint32_t N000041C3; //0x3E60
	uint32_t N00005497; //0x3E64
	uint32_t N000041C4; //0x3E68
	uint32_t N00005499; //0x3E6C
	uint32_t N000041C5; //0x3E70
	uint32_t N0000549B; //0x3E74
	uint32_t N000041C6; //0x3E78
	uint32_t N0000549D; //0x3E7C
	uint32_t N000041C7; //0x3E80
	uint32_t N0000549F; //0x3E84
	uint32_t N000041C8; //0x3E88
	uint32_t N000054A1; //0x3E8C
	uint32_t N000041C9; //0x3E90
	uint32_t N000054A3; //0x3E94
	uint32_t N000041CA; //0x3E98
	uint32_t N000054A5; //0x3E9C
	uint32_t N000041CB; //0x3EA0
	uint32_t N000054A7; //0x3EA4
	uint32_t N000041CC; //0x3EA8
	uint32_t N000054A9; //0x3EAC
	uint32_t N000041CD; //0x3EB0
	uint32_t N000054AB; //0x3EB4
	uint32_t N000041CE; //0x3EB8
	uint32_t N000054AD; //0x3EBC
	uint32_t N000041CF; //0x3EC0
	uint32_t N000054AF; //0x3EC4
	uint32_t N000041D0; //0x3EC8
	uint32_t N000054B1; //0x3ECC
	uint32_t N000041D1; //0x3ED0
	uint32_t N000054B3; //0x3ED4
	uint32_t N000041D2; //0x3ED8
	uint32_t N000054B5; //0x3EDC
	uint32_t N000041D3; //0x3EE0
	uint32_t N000054B7; //0x3EE4
	uint32_t N000041D4; //0x3EE8
	uint32_t N000054B9; //0x3EEC
	uint32_t N000041D5; //0x3EF0
	uint32_t N000054BB; //0x3EF4
	uint32_t N000041D6; //0x3EF8
	uint32_t N000054BD; //0x3EFC
	uint32_t N000041D7; //0x3F00
	uint32_t N000054BF; //0x3F04
	uint32_t N000041D8; //0x3F08
	uint32_t N000054C1; //0x3F0C
	uint32_t N000041D9; //0x3F10
	uint32_t N000054C3; //0x3F14
	uint32_t N000041DA; //0x3F18
	uint32_t N000054C5; //0x3F1C
	uint32_t N000041DB; //0x3F20
	uint32_t N000054C7; //0x3F24
	uint32_t N000041DC; //0x3F28
	uint32_t N000054C9; //0x3F2C
	uint32_t N000041DD; //0x3F30
	uint32_t N000054CB; //0x3F34
	uint32_t N000041DE; //0x3F38
	uint32_t N000054CD; //0x3F3C
	uint32_t N000041DF; //0x3F40
	uint32_t N000054CF; //0x3F44
	uint32_t N000041E0; //0x3F48
	uint32_t N000054D1; //0x3F4C
	uint32_t N000041E1; //0x3F50
	uint32_t N000054D3; //0x3F54
	uint32_t N000041E2; //0x3F58
	uint32_t N000054D5; //0x3F5C
	uint32_t N000041E3; //0x3F60
	uint32_t N000054D7; //0x3F64
	uint32_t N000041E4; //0x3F68
	uint32_t N000054D9; //0x3F6C
	uint32_t N000041E5; //0x3F70
	uint32_t N000054DB; //0x3F74
	uint32_t N000041E6; //0x3F78
	uint32_t N000054DD; //0x3F7C
	uint32_t N000041E7; //0x3F80
	uint32_t N000054DF; //0x3F84
	uint32_t N000041E8; //0x3F88
	uint32_t N000054E1; //0x3F8C
	uint32_t N000041E9; //0x3F90
	uint32_t N000054E3; //0x3F94
	uint32_t N000041EA; //0x3F98
	uint32_t N000054E5; //0x3F9C
	uint32_t N000041EB; //0x3FA0
	uint32_t N000054E7; //0x3FA4
	uint32_t N000041EC; //0x3FA8
	uint32_t N000054E9; //0x3FAC
	uint32_t N000041ED; //0x3FB0
	uint32_t N000054EB; //0x3FB4
	uint32_t N000041EE; //0x3FB8
	uint32_t N000054ED; //0x3FBC
	uint32_t N000041EF; //0x3FC0
	uint32_t N000054EF; //0x3FC4
	uint32_t N000041F0; //0x3FC8
	uint32_t N000054F1; //0x3FCC
	uint32_t N000041F1; //0x3FD0
	uint32_t N000054F3; //0x3FD4
	uint32_t N000041F2; //0x3FD8
	uint32_t N000054F5; //0x3FDC
	uint32_t N000041F3; //0x3FE0
	uint32_t N000054F7; //0x3FE4
	uint32_t N000041F4; //0x3FE8
	uint32_t N000054F9; //0x3FEC
	uint32_t N000041F5; //0x3FF0
	uint32_t N000054FB; //0x3FF4
	uint32_t N000041F6; //0x3FF8
	uint32_t N000054FD; //0x3FFC
	uint32_t N000041F7; //0x4000
	uint32_t N000054FF; //0x4004
	uint32_t N000041F8; //0x4008
	uint32_t N00005501; //0x400C
	uint32_t N000041F9; //0x4010
	uint32_t N00005503; //0x4014
	uint32_t N000041FA; //0x4018
	uint32_t N00005505; //0x401C
	uint32_t N000041FB; //0x4020
	uint32_t N00005507; //0x4024
	uint32_t N000041FC; //0x4028
	uint32_t N00005509; //0x402C
	uint32_t N000041FD; //0x4030
	uint32_t N0000550B; //0x4034
	uint32_t N000041FE; //0x4038
	uint32_t N0000550D; //0x403C
	uint32_t N000041FF; //0x4040
	uint32_t N0000550F; //0x4044
	uint32_t N00004200; //0x4048
	uint32_t N00005511; //0x404C
	uint32_t N00004201; //0x4050
	uint32_t N00005513; //0x4054
	uint32_t N00004202; //0x4058
	uint32_t N00005515; //0x405C
	uint32_t N00004203; //0x4060
	uint32_t N00005517; //0x4064
	uint32_t N00004204; //0x4068
	uint32_t N00005519; //0x406C
	uint32_t N00004205; //0x4070
	uint32_t N0000551B; //0x4074
	uint32_t N00004206; //0x4078
	uint32_t N0000551D; //0x407C
	uint32_t N00004207; //0x4080
	uint32_t N0000551F; //0x4084
	uint32_t N00004208; //0x4088
	uint32_t N00005521; //0x408C
	uint32_t N00004209; //0x4090
	uint32_t N00005523; //0x4094
	uint32_t N0000420A; //0x4098
	uint32_t N00005525; //0x409C
	uint32_t N0000420B; //0x40A0
	uint32_t N00005527; //0x40A4
	uint32_t N0000420C; //0x40A8
	uint32_t N00005529; //0x40AC
	uint32_t N0000420D; //0x40B0
	uint32_t N0000552B; //0x40B4
	uint32_t N0000420E; //0x40B8
	uint32_t N0000552D; //0x40BC
	uint32_t N0000420F; //0x40C0
	uint32_t N0000552F; //0x40C4
	uint32_t N00004210; //0x40C8
	uint32_t N00005531; //0x40CC
	uint32_t N00004211; //0x40D0
	uint32_t N00005533; //0x40D4
	uint32_t N00004212; //0x40D8
	uint32_t N00005535; //0x40DC
	uint32_t N00004213; //0x40E0
	uint32_t N00005537; //0x40E4
	uint32_t N00004214; //0x40E8
	uint32_t N00005539; //0x40EC
	uint32_t N00004215; //0x40F0
	uint32_t N0000553B; //0x40F4
	uint32_t N00004216; //0x40F8
	uint32_t N0000553D; //0x40FC
	uint32_t N00004217; //0x4100
	uint32_t N0000553F; //0x4104
	uint32_t N00004218; //0x4108
	uint32_t N00005541; //0x410C
	uint32_t N00004219; //0x4110
	uint32_t N00005543; //0x4114
	uint32_t N0000421A; //0x4118
	uint32_t N00005545; //0x411C
	uint32_t N0000421B; //0x4120
	uint32_t N00005547; //0x4124
	uint32_t N0000421C; //0x4128
	uint32_t N00005549; //0x412C
	uint32_t N0000421D; //0x4130
	uint32_t N0000554B; //0x4134
	uint32_t N0000421E; //0x4138
	uint32_t N0000554D; //0x413C
	uint32_t N0000421F; //0x4140
	uint32_t N0000554F; //0x4144
	uint32_t N00004220; //0x4148
	uint32_t N00005551; //0x414C
	uint32_t N00004221; //0x4150
	uint32_t N00005553; //0x4154
	uint32_t N00004222; //0x4158
	uint32_t N00005555; //0x415C
	uint32_t N00004223; //0x4160
	uint32_t N00005557; //0x4164
	uint32_t N00004224; //0x4168
	uint32_t N00005559; //0x416C
	uint32_t N00004225; //0x4170
	uint32_t N0000555B; //0x4174
	uint32_t N00004226; //0x4178
	uint32_t N0000555D; //0x417C
	uint32_t N00004227; //0x4180
	uint32_t N0000555F; //0x4184
	uint32_t N00004228; //0x4188
	uint32_t N00005561; //0x418C
	uint32_t N00004229; //0x4190
	uint32_t N00005563; //0x4194
	uint32_t N0000422A; //0x4198
	uint32_t N00005565; //0x419C
	uint32_t N0000422B; //0x41A0
	uint32_t N00005567; //0x41A4
	uint32_t N0000422C; //0x41A8
	uint32_t N00005569; //0x41AC
	uint32_t N0000422D; //0x41B0
	uint32_t N0000556B; //0x41B4
	uint32_t N0000422E; //0x41B8
	uint32_t N0000556D; //0x41BC
	uint32_t N0000422F; //0x41C0
	uint32_t N0000556F; //0x41C4
	uint32_t N00004230; //0x41C8
	uint32_t N00005571; //0x41CC
	uint32_t N00004231; //0x41D0
	uint32_t N00005573; //0x41D4
	uint32_t N00004232; //0x41D8
	uint32_t N00005575; //0x41DC
	uint32_t N00004233; //0x41E0
	uint32_t N00005577; //0x41E4
	uint32_t N00004234; //0x41E8
	uint32_t N00005579; //0x41EC
	uint32_t N00004235; //0x41F0
	uint32_t N0000557B; //0x41F4
	uint32_t N00004236; //0x41F8
	uint32_t N0000557D; //0x41FC
	uint32_t N00004237; //0x4200
	uint32_t N0000557F; //0x4204
	uint32_t N00004238; //0x4208
	uint32_t N00005581; //0x420C
	uint32_t N00004239; //0x4210
	uint32_t N00005583; //0x4214
	uint32_t N0000423A; //0x4218
	uint32_t N00005585; //0x421C
	uint32_t N0000423B; //0x4220
	uint32_t N00005587; //0x4224
	uint32_t N0000423C; //0x4228
	uint32_t N00005589; //0x422C
	uint32_t N0000423D; //0x4230
	uint32_t N0000558B; //0x4234
	uint32_t N0000423E; //0x4238
	uint32_t N0000558D; //0x423C
	uint32_t N0000423F; //0x4240
	uint32_t N0000558F; //0x4244
	uint32_t N00004240; //0x4248
	uint32_t N00005591; //0x424C
	uint32_t N00004241; //0x4250
	uint32_t N00005593; //0x4254
	uint32_t N00004242; //0x4258
	uint32_t N00005595; //0x425C
	uint32_t N00004243; //0x4260
	uint32_t N00005597; //0x4264
	uint32_t N00004244; //0x4268
	uint32_t N00005599; //0x426C
	uint32_t N00004245; //0x4270
	uint32_t N0000559B; //0x4274
	uint32_t N00004246; //0x4278
	uint32_t N0000559D; //0x427C
	uint32_t N00004247; //0x4280
	uint32_t N0000559F; //0x4284
	uint32_t N00004248; //0x4288
	uint32_t N000055A1; //0x428C
	uint32_t N00004249; //0x4290
	uint32_t N000055A3; //0x4294
	uint32_t N0000424A; //0x4298
	uint32_t N000055A5; //0x429C
	uint32_t N0000424B; //0x42A0
	uint32_t N000055A7; //0x42A4
	uint32_t N0000424C; //0x42A8
	uint32_t N000055A9; //0x42AC
	uint32_t N0000424D; //0x42B0
	uint32_t N000055AB; //0x42B4
	uint32_t N0000424E; //0x42B8
	uint32_t N000055AD; //0x42BC
	uint32_t N0000424F; //0x42C0
	uint32_t N000055AF; //0x42C4
	uint32_t N00004250; //0x42C8
	uint32_t N000055B1; //0x42CC
	uint32_t N00004251; //0x42D0
	uint32_t N000055B3; //0x42D4
	uint32_t N00004252; //0x42D8
	uint32_t N000055B5; //0x42DC
	uint32_t N00004253; //0x42E0
	uint32_t N000055B7; //0x42E4
	uint32_t N00004254; //0x42E8
	uint32_t N000055B9; //0x42EC
	uint32_t N00004255; //0x42F0
	uint32_t N000055BB; //0x42F4
	uint32_t N00004256; //0x42F8
	uint32_t N000055BD; //0x42FC
	uint32_t N00004257; //0x4300
	uint32_t N000055BF; //0x4304
	uint32_t N00004258; //0x4308
	uint32_t N000055C1; //0x430C
	uint32_t N00004259; //0x4310
	uint32_t N000055C3; //0x4314
	uint32_t N0000425A; //0x4318
	uint32_t N000055C5; //0x431C
	uint32_t N0000425B; //0x4320
	uint32_t N000055C7; //0x4324
	uint32_t N0000425C; //0x4328
	uint32_t N000055C9; //0x432C
	uint32_t N0000425D; //0x4330
	uint32_t N000055CB; //0x4334
	uint32_t N0000425E; //0x4338
	uint32_t N000055CD; //0x433C
	uint32_t N0000425F; //0x4340
	uint32_t N000055CF; //0x4344
	uint32_t N00004260; //0x4348
	uint32_t N000055D1; //0x434C
	uint32_t N00004261; //0x4350
	uint32_t N000055D3; //0x4354
	uint32_t N00004262; //0x4358
	uint32_t N000055D5; //0x435C
	uint32_t N00004263; //0x4360
	uint32_t N000055D7; //0x4364
	uint32_t N00004264; //0x4368
	uint32_t N000055D9; //0x436C
	uint32_t N00004265; //0x4370
	uint32_t N000055DB; //0x4374
	uint32_t N00004266; //0x4378
	uint32_t N000055DD; //0x437C
	uint32_t N00004267; //0x4380
	uint32_t N000055DF; //0x4384
	uint32_t N00004268; //0x4388
	uint32_t N000055E1; //0x438C
	uint32_t N00004269; //0x4390
	uint32_t N000055E3; //0x4394
	uint32_t N0000426A; //0x4398
	uint32_t N000055E5; //0x439C
	uint32_t N0000426B; //0x43A0
	uint32_t N000055E7; //0x43A4
	uint32_t N0000426C; //0x43A8
	uint32_t N000055E9; //0x43AC
	uint32_t N0000426D; //0x43B0
	uint32_t N000055EB; //0x43B4
	uint32_t N0000426E; //0x43B8
	uint32_t N000055ED; //0x43BC
	uint32_t N0000426F; //0x43C0
	uint32_t N000055EF; //0x43C4
	uint32_t N00004270; //0x43C8
	uint32_t N000055F1; //0x43CC
	uint32_t N00004271; //0x43D0
	uint32_t N000055F3; //0x43D4
	uint32_t N00004272; //0x43D8
	uint32_t N000055F5; //0x43DC
	uint32_t N00004273; //0x43E0
	uint32_t N000055F7; //0x43E4
	uint32_t N00004274; //0x43E8
	uint32_t N000055F9; //0x43EC
	uint32_t N00004275; //0x43F0
	uint32_t N000055FB; //0x43F4
	uint32_t N00004276; //0x43F8
	uint32_t N000055FD; //0x43FC
	uint32_t N00004277; //0x4400
	uint32_t N000055FF; //0x4404
	uint32_t N00004278; //0x4408
	uint32_t N00005601; //0x440C
	uint32_t N00004279; //0x4410
	uint32_t N00005603; //0x4414
	uint32_t N0000427A; //0x4418
	uint32_t N00005605; //0x441C
	uint32_t N0000427B; //0x4420
	uint32_t N00005607; //0x4424
	uint32_t N0000427C; //0x4428
	uint32_t N00005609; //0x442C
	uint32_t N0000427D; //0x4430
	uint32_t N0000560B; //0x4434
	uint32_t N0000427E; //0x4438
	uint32_t N0000560D; //0x443C
	uint32_t N0000427F; //0x4440
	uint32_t N0000560F; //0x4444
	uint32_t N00004280; //0x4448
	uint32_t N00005611; //0x444C
	uint32_t N00004281; //0x4450
	uint32_t N00005613; //0x4454
	uint32_t N00004282; //0x4458
	uint32_t N00005615; //0x445C
	uint32_t N00004283; //0x4460
	uint32_t N00005617; //0x4464
	uint32_t N00004284; //0x4468
	uint32_t N00005619; //0x446C
	uint32_t N00004285; //0x4470
	uint32_t N0000561B; //0x4474
	uint32_t N00004286; //0x4478
	uint32_t N0000561D; //0x447C
	uint32_t N00004287; //0x4480
	uint32_t N0000561F; //0x4484
	uint32_t N00004288; //0x4488
	uint32_t N00005621; //0x448C
	uint32_t N00004289; //0x4490
	uint32_t N00005623; //0x4494
	uint32_t N0000428A; //0x4498
	uint32_t N00005625; //0x449C
	uint32_t N0000428B; //0x44A0
	uint32_t N00005627; //0x44A4
	uint32_t N0000428C; //0x44A8
	uint32_t N00005629; //0x44AC
	uint32_t N0000428D; //0x44B0
	uint32_t N0000562B; //0x44B4
	uint32_t N0000428E; //0x44B8
	uint32_t N0000562D; //0x44BC
	uint32_t N0000428F; //0x44C0
	uint32_t N0000562F; //0x44C4
	uint32_t N00004290; //0x44C8
	uint32_t N00005631; //0x44CC
	uint32_t N00004291; //0x44D0
	uint32_t N00005633; //0x44D4
	uint32_t N00004292; //0x44D8
	uint32_t N00005635; //0x44DC
	uint32_t N00004293; //0x44E0
	uint32_t N00005637; //0x44E4
	uint32_t N00004294; //0x44E8
	uint32_t N00005639; //0x44EC
	uint32_t N00004295; //0x44F0
	uint32_t N0000563B; //0x44F4
	uint32_t N00004296; //0x44F8
	uint32_t N0000563D; //0x44FC
	uint32_t N00004297; //0x4500
	uint32_t N0000563F; //0x4504
	uint32_t N00004298; //0x4508
	uint32_t N00005641; //0x450C
	uint32_t N00004299; //0x4510
	uint32_t N00005643; //0x4514
	uint32_t N0000429A; //0x4518
	uint32_t N00005645; //0x451C
	uint32_t N0000429B; //0x4520
	uint32_t N00005647; //0x4524
	uint32_t N0000429C; //0x4528
	uint32_t N00005649; //0x452C
	uint32_t N0000429D; //0x4530
	uint32_t N0000564B; //0x4534
	uint32_t N0000429E; //0x4538
	uint32_t N0000564D; //0x453C
	uint32_t N0000429F; //0x4540
	uint32_t N0000564F; //0x4544
	uint32_t N000042A0; //0x4548
	uint32_t N00005651; //0x454C
	uint32_t N000042A1; //0x4550
	uint32_t N00005653; //0x4554
	uint32_t N000042A2; //0x4558
	uint32_t N00005655; //0x455C
	uint32_t N000042A3; //0x4560
	uint32_t N00005657; //0x4564
	uint32_t N000042A4; //0x4568
	uint32_t N00005659; //0x456C
	uint32_t N000042A5; //0x4570
	uint32_t N0000565B; //0x4574
	uint32_t N000042A6; //0x4578
	uint32_t N0000565D; //0x457C
	uint32_t N000042A7; //0x4580
	uint32_t N0000565F; //0x4584
	uint32_t N000042A8; //0x4588
	uint32_t N00005661; //0x458C
	uint32_t N000042A9; //0x4590
	uint32_t N00005663; //0x4594
	uint32_t N000042AA; //0x4598
	uint32_t N00005665; //0x459C
	uint32_t N000042AB; //0x45A0
	uint32_t N00005667; //0x45A4
	uint32_t N000042AC; //0x45A8
	uint32_t N00005669; //0x45AC
	uint32_t N000042AD; //0x45B0
	uint32_t N0000566B; //0x45B4
	uint32_t N000042AE; //0x45B8
	uint32_t N0000566D; //0x45BC
	uint32_t N000042AF; //0x45C0
	uint32_t N0000566F; //0x45C4
	uint32_t N000042B0; //0x45C8
	uint32_t N00005671; //0x45CC
	uint32_t N000042B1; //0x45D0
	uint32_t N00005673; //0x45D4
	uint32_t N000042B2; //0x45D8
	uint32_t N00005675; //0x45DC
	uint32_t N000042B3; //0x45E0
	uint32_t N00005677; //0x45E4
	uint32_t N000042B4; //0x45E8
	uint32_t N00005679; //0x45EC
	uint32_t N000042B5; //0x45F0
	uint32_t N0000567B; //0x45F4
	uint32_t N000042B6; //0x45F8
	uint32_t N0000567D; //0x45FC
	uint32_t N000042B7; //0x4600
	uint32_t N0000567F; //0x4604
	uint32_t N000042B8; //0x4608
	uint32_t N00005681; //0x460C
	uint32_t N000042B9; //0x4610
	uint32_t N00005683; //0x4614
	uint32_t N000042BA; //0x4618
	uint32_t N00005685; //0x461C
	uint32_t N000042BB; //0x4620
	uint32_t N00005687; //0x4624
	uint32_t N000042BC; //0x4628
	uint32_t N00005689; //0x462C
	uint32_t N000042BD; //0x4630
	uint32_t N0000568B; //0x4634
	uint32_t N000042BE; //0x4638
	uint32_t N0000568D; //0x463C
	uint32_t N000042BF; //0x4640
	uint32_t N0000568F; //0x4644
	uint32_t N000042C0; //0x4648
	uint32_t N00005691; //0x464C
	uint32_t N000042C1; //0x4650
	uint32_t N00005693; //0x4654
	uint32_t N000042C2; //0x4658
	uint32_t N00005695; //0x465C
	uint32_t N000042C3; //0x4660
	uint32_t N00005697; //0x4664
	uint32_t N000042C4; //0x4668
	uint32_t N00005699; //0x466C
	uint32_t N000042C5; //0x4670
	uint32_t N0000569B; //0x4674
	uint32_t N000042C6; //0x4678
	uint32_t N0000569D; //0x467C
	uint32_t N000042C7; //0x4680
	uint32_t N0000569F; //0x4684
	uint32_t N000042C8; //0x4688
	uint32_t N000056A1; //0x468C
	uint32_t N000042C9; //0x4690
	uint32_t N000056A3; //0x4694
	uint32_t N000042CA; //0x4698
	uint32_t N000056A5; //0x469C
	uint32_t N000042CB; //0x46A0
	uint32_t N000056A7; //0x46A4
	uint32_t N000042CC; //0x46A8
	uint32_t N000056A9; //0x46AC
	uint32_t N000042CD; //0x46B0
	uint32_t N000056AB; //0x46B4
	uint32_t N000042CE; //0x46B8
	uint32_t N000056AD; //0x46BC
	uint32_t N000042CF; //0x46C0
	uint32_t N000056AF; //0x46C4
	uint32_t N000042D0; //0x46C8
	uint32_t N000056B1; //0x46CC
	uint32_t N000042D1; //0x46D0
	uint32_t N000056B3; //0x46D4
	uint32_t N000042D2; //0x46D8
	uint32_t N000056B5; //0x46DC
	uint32_t N000042D3; //0x46E0
	uint32_t N000056B7; //0x46E4
	uint32_t N000042D4; //0x46E8
	uint32_t N000056B9; //0x46EC
	uint32_t N000042D5; //0x46F0
	uint32_t N000056BB; //0x46F4
	uint32_t N000042D6; //0x46F8
	uint32_t N000056BD; //0x46FC
	uint32_t N000042D7; //0x4700
	uint32_t N000056BF; //0x4704
	uint32_t N000042D8; //0x4708
	uint32_t N000056C1; //0x470C
	uint32_t N000042D9; //0x4710
	uint32_t N000056C3; //0x4714
	uint32_t N000042DA; //0x4718
	uint32_t N000056C5; //0x471C
	uint32_t N000042DB; //0x4720
	uint32_t N000056C7; //0x4724
	uint32_t N000042DC; //0x4728
	uint32_t N000056C9; //0x472C
	uint32_t N000042DD; //0x4730
	uint32_t N000056CB; //0x4734
	uint32_t N000042DE; //0x4738
	uint32_t N000056CD; //0x473C
	uint32_t N000042DF; //0x4740
	uint32_t N000056CF; //0x4744
	uint32_t N000042E0; //0x4748
	uint32_t N000056D1; //0x474C
	uint32_t N000042E1; //0x4750
	uint32_t N000056D3; //0x4754
	uint32_t N000042E2; //0x4758
	uint32_t N000056D5; //0x475C
	uint32_t N000042E3; //0x4760
	uint32_t N000056D7; //0x4764
	uint32_t N000042E4; //0x4768
	uint32_t N000056D9; //0x476C
	uint32_t N000042E5; //0x4770
	uint32_t N000056DB; //0x4774
	uint32_t N000042E6; //0x4778
	uint32_t N000056DD; //0x477C
	uint32_t N000042E7; //0x4780
	uint32_t N000056DF; //0x4784
	uint32_t N000042E8; //0x4788
	uint32_t N000056E1; //0x478C
	uint32_t N000042E9; //0x4790
	uint32_t N000056E3; //0x4794
	uint32_t N000042EA; //0x4798
	uint32_t N000056E5; //0x479C
	uint32_t N000042EB; //0x47A0
	uint32_t N000056E7; //0x47A4
	uint32_t N000042EC; //0x47A8
	uint32_t N000056E9; //0x47AC
	uint32_t N000042ED; //0x47B0
	uint32_t N000056EB; //0x47B4
	uint32_t N000042EE; //0x47B8
	uint32_t N000056ED; //0x47BC
	uint32_t N000042EF; //0x47C0
	uint32_t N000056EF; //0x47C4
	uint32_t N000042F0; //0x47C8
	uint32_t N000056F1; //0x47CC
	uint32_t N000042F1; //0x47D0
	uint32_t N000056F3; //0x47D4
	uint32_t N000042F2; //0x47D8
	uint32_t N000056F5; //0x47DC
	uint32_t N000042F3; //0x47E0
	uint32_t N000056F7; //0x47E4
	uint32_t N000042F4; //0x47E8
	uint32_t N000056F9; //0x47EC
	uint32_t N000042F5; //0x47F0
	uint32_t N000056FB; //0x47F4
	uint32_t N000042F6; //0x47F8
	uint32_t N000056FD; //0x47FC
	uint32_t N000042F7; //0x4800
	uint32_t N000056FF; //0x4804
	uint32_t N000042F8; //0x4808
	uint32_t N00005701; //0x480C
	uint32_t N000042F9; //0x4810
	uint32_t N00005703; //0x4814
	uint32_t N000042FA; //0x4818
	uint32_t N00005705; //0x481C
	uint32_t N000042FB; //0x4820
	uint32_t N00005707; //0x4824
	uint32_t N000042FC; //0x4828
	uint32_t N00005709; //0x482C
	uint32_t N000042FD; //0x4830
	uint32_t N0000570B; //0x4834
	uint32_t N000042FE; //0x4838
	uint32_t N0000570D; //0x483C
	uint32_t N000042FF; //0x4840
	uint32_t N0000570F; //0x4844
	uint32_t N00004300; //0x4848
	uint32_t N00005711; //0x484C
	uint32_t N00004301; //0x4850
	uint32_t N00005713; //0x4854
	uint32_t N00004302; //0x4858
	uint32_t N00005715; //0x485C
	uint32_t N00004303; //0x4860
	uint32_t N00005717; //0x4864
	uint32_t N00004304; //0x4868
	uint32_t N00005719; //0x486C
	uint32_t N00004305; //0x4870
	uint32_t N0000571B; //0x4874
	uint32_t N00004306; //0x4878
	uint32_t N0000571D; //0x487C
	uint32_t N00004307; //0x4880
	uint32_t N0000571F; //0x4884
	uint32_t N00004308; //0x4888
	uint32_t N00005721; //0x488C
	uint32_t N00004309; //0x4890
	uint32_t N00005723; //0x4894
	uint32_t N0000430A; //0x4898
	uint32_t N00005725; //0x489C
	uint32_t N0000430B; //0x48A0
	uint32_t N00005727; //0x48A4
	uint32_t N0000430C; //0x48A8
	uint32_t N00005729; //0x48AC
	uint32_t N0000430D; //0x48B0
	uint16_t N0000572B; //0x48B4
	uint16_t N000091EF; //0x48B6
	uint16_t N0000430E; //0x48B8
	uint16_t N000091F2; //0x48BA
	uint16_t N0000572D; //0x48BC
	uint16_t N000091F5; //0x48BE
	uint16_t N0000430F; //0x48C0
	uint16_t N000091F8; //0x48C2
	uint16_t N0000572F; //0x48C4
	uint16_t N000091FB; //0x48C6
	uint16_t N00004310; //0x48C8
	uint16_t N000091FE; //0x48CA
	uint16_t N00005731; //0x48CC
	uint16_t N00009201; //0x48CE
	uint16_t N00004311; //0x48D0
	uint16_t N00009204; //0x48D2
	uint16_t N00005733; //0x48D4
	uint16_t N00009207; //0x48D6
	uint16_t N00004312; //0x48D8
	uint16_t N0000920A; //0x48DA
	uint16_t N00005735; //0x48DC
	uint16_t N0000920D; //0x48DE
	uint16_t N00004313; //0x48E0
	uint16_t N00009210; //0x48E2
	uint16_t N00005737; //0x48E4
	uint16_t N00009213; //0x48E6
	uint16_t N00004314; //0x48E8
	uint16_t N00009216; //0x48EA
	uint32_t N00005739; //0x48EC
	uint32_t N00004315; //0x48F0
	uint32_t N0000573B; //0x48F4
	uint32_t N00004316; //0x48F8
	uint32_t N0000573D; //0x48FC
	uint32_t N00004317; //0x4900
	uint32_t N0000573F; //0x4904
	uint32_t N00004318; //0x4908
	uint32_t N00005741; //0x490C
	uint32_t N00004319; //0x4910
	uint32_t N00005743; //0x4914
	uint32_t N0000431A; //0x4918
	uint32_t N00005745; //0x491C
	uint32_t N0000431B; //0x4920
	uint32_t N00005747; //0x4924
	uint32_t N0000431C; //0x4928
	uint32_t N00005749; //0x492C
	uint32_t N0000431D; //0x4930
	uint32_t N0000574B; //0x4934
	uint32_t N0000431E; //0x4938
	uint32_t N0000574D; //0x493C
	uint32_t N0000431F; //0x4940
	uint32_t N0000574F; //0x4944
	uint32_t N00004320; //0x4948
	uint32_t N00005751; //0x494C
	uint32_t N00004321; //0x4950
	uint32_t N00005753; //0x4954
	uint32_t N00004322; //0x4958
	uint32_t N00005755; //0x495C
	uint32_t N00004323; //0x4960
	uint32_t N00005757; //0x4964
	uint32_t N00004324; //0x4968
	uint32_t N00005759; //0x496C
	uint32_t N00004325; //0x4970
	uint32_t N0000575B; //0x4974
	uint32_t N00004326; //0x4978
	uint32_t N0000575D; //0x497C
	uint32_t N00004327; //0x4980
	uint32_t N0000575F; //0x4984
	uint32_t N00004328; //0x4988
	uint32_t N00005761; //0x498C
	uint32_t N00004329; //0x4990
	uint32_t N00005763; //0x4994
	uint32_t N0000432A; //0x4998
	uint32_t N00005765; //0x499C
	uint32_t N0000432B; //0x49A0
	uint32_t N00005767; //0x49A4
	uint32_t N0000432C; //0x49A8
	uint32_t N00005769; //0x49AC
	uint32_t N0000432D; //0x49B0
	uint32_t N0000576B; //0x49B4
	uint32_t N0000432E; //0x49B8
	uint32_t N0000576D; //0x49BC
	uint32_t N0000432F; //0x49C0
	uint32_t N0000576F; //0x49C4
	uint32_t N00004330; //0x49C8
	uint32_t N00005771; //0x49CC
	uint32_t N00004331; //0x49D0
	uint32_t N00005773; //0x49D4
	uint32_t N00004332; //0x49D8
	uint32_t N00005775; //0x49DC
	uint32_t N00004333; //0x49E0
	uint32_t N00005777; //0x49E4
	uint16_t N00004334; //0x49E8
	uint16_t r_AITribe_Engineers; //0x49EA
	uint16_t r_AITribe_EconomyProtection; //0x49EC
	uint16_t r_AITribe_Bodyguards; //0x49EE
	uint16_t N00004335; //0x49F0
	uint16_t N000091F2_2; //0x49F2
	uint16_t N0000577B; //0x49F4
	uint16_t N000091F5_3; //0x49F6
	uint16_t N00004336; //0x49F8
	uint16_t N000091F8_2; //0x49FA
	uint16_t N0000577D; //0x49FC
	uint16_t N000091FB_2; //0x49FE
	uint16_t N00004337; //0x4A00
	uint16_t N000091FE_2; //0x4A02
	uint16_t N0000577F; //0x4A04
	uint16_t N00009201_2; //0x4A06
	uint16_t N00004338; //0x4A08
	uint16_t N00009204_2; //0x4A0A
	uint16_t N00005781; //0x4A0C
	uint16_t N000091E7_2; //0x4A0E
	uint16_t N00004339; //0x4A10
	uint16_t N00009207_2; //0x4A12
	uint16_t N00005783; //0x4A14
	uint16_t N000091EF_2; //0x4A16
	uint16_t N0000433A; //0x4A18
	uint16_t N0000920A_2; //0x4A1A
	uint16_t N00005785; //0x4A1C
	uint16_t N0000920D_2; //0x4A1E
	uint16_t N0000433B; //0x4A20
	uint16_t N00009210_2; //0x4A22
	uint16_t N00005787; //0x4A24
	uint16_t N00009213_2; //0x4A26
	uint16_t N0000433C; //0x4A28
	uint16_t N00009216_2; //0x4A2A
	uint32_t N00005789; //0x4A2C
	uint32_t N0000433D_2; //0x4A30
	uint32_t N0000578B; //0x4A34
	uint32_t N0000433E_2; //0x4A38
	uint32_t N0000578D; //0x4A3C
	uint32_t N0000433F_2; //0x4A40
	uint32_t N0000578F; //0x4A44
	uint32_t N00004340_2; //0x4A48
	uint32_t N00005791; //0x4A4C
	uint32_t N00004341_2; //0x4A50
	uint32_t N00005793; //0x4A54
	uint32_t N00004342_2; //0x4A58
	uint32_t N00005795; //0x4A5C
	uint32_t N00004343_2; //0x4A60
	uint32_t N00005797; //0x4A64
	uint32_t N00004344_2; //0x4A68
	uint32_t N00005799; //0x4A6C
	uint32_t N00004345_2; //0x4A70
	uint32_t N0000579B; //0x4A74
	uint32_t N00004346_2; //0x4A78
	uint32_t N0000579D; //0x4A7C
	uint32_t N00004347_2; //0x4A80
	uint32_t N0000579F; //0x4A84
	uint32_t N00004348_2; //0x4A88
	uint32_t N000057A1; //0x4A8C
	uint32_t N00004349_2; //0x4A90
	uint32_t N000057A3; //0x4A94
	uint32_t N0000434A_2; //0x4A98
	uint32_t N000057A5; //0x4A9C
	uint32_t N0000434B_2; //0x4AA0
	uint32_t N000057A7; //0x4AA4
	uint32_t N0000434C_2; //0x4AA8
	uint32_t N000057A9; //0x4AAC
	uint32_t N0000434D; //0x4AB0
	uint32_t N000057AB; //0x4AB4
	uint32_t N0000434E; //0x4AB8
	uint32_t N000057AD; //0x4ABC
	uint32_t N0000434F; //0x4AC0
	uint32_t N000057AF; //0x4AC4
	uint32_t N00004350; //0x4AC8
	uint32_t N000057B1; //0x4ACC
	uint32_t N00004351; //0x4AD0
	uint32_t N000057B3; //0x4AD4
	uint32_t N00004352; //0x4AD8
	uint32_t N000057B5; //0x4ADC
	uint32_t N00004353; //0x4AE0
	uint32_t N000057B7; //0x4AE4
	uint32_t N00004354; //0x4AE8
	uint32_t N000057B9; //0x4AEC
	uint32_t N00004355; //0x4AF0
	uint32_t N000057BB; //0x4AF4
	uint32_t N00004356; //0x4AF8
	uint32_t N000057BD; //0x4AFC
	uint32_t N00004357; //0x4B00
	uint32_t N000057BF; //0x4B04
	uint32_t N00004358; //0x4B08
	uint32_t N000057C1; //0x4B0C
	uint32_t N00004359; //0x4B10
	uint32_t N000057C3; //0x4B14
	uint32_t N0000435A; //0x4B18
	uint32_t N000057C5; //0x4B1C
	uint32_t N0000435B; //0x4B20
	uint32_t N000057C7; //0x4B24
	uint32_t N0000435C; //0x4B28
	uint32_t N000057C9; //0x4B2C
	uint32_t N0000435D; //0x4B30
	uint32_t N000057CB; //0x4B34
	uint32_t N0000435E; //0x4B38
	uint32_t N000057CD; //0x4B3C
	uint16_t N0000435F; //0x4B40
	uint16_t N00009219; //0x4B42
	uint32_t N000057CF; //0x4B44
	uint32_t N00004360; //0x4B48
	uint32_t N000057D1; //0x4B4C
	uint32_t N00004361; //0x4B50
	uint32_t N000057D3; //0x4B54
	uint32_t N00004362; //0x4B58
	uint32_t N000057D5; //0x4B5C
	uint32_t N00004363; //0x4B60
	uint32_t N000057D7; //0x4B64
	uint32_t N00004364; //0x4B68
	uint32_t N000057D9; //0x4B6C
	uint32_t N00004365; //0x4B70
	uint32_t N000057DB; //0x4B74
	uint32_t N00004366; //0x4B78
	uint32_t N000057DD; //0x4B7C
	uint32_t N00004367; //0x4B80
	uint32_t N000057DF; //0x4B84
	uint32_t N00004368; //0x4B88
	uint32_t N000057E1; //0x4B8C
	uint32_t N00004369; //0x4B90
	uint32_t N000057E3; //0x4B94
	uint32_t N0000436A; //0x4B98
	uint32_t N000057E5; //0x4B9C
	uint32_t N0000436B; //0x4BA0
	uint32_t N000057E7; //0x4BA4
	uint32_t N0000436C; //0x4BA8
	uint32_t N000057E9; //0x4BAC
	uint32_t N0000436D; //0x4BB0
	uint32_t N000057EB; //0x4BB4
	uint32_t N0000436E; //0x4BB8
	uint32_t N000057ED; //0x4BBC
	uint32_t N0000436F; //0x4BC0
	uint32_t N000057EF; //0x4BC4
	uint32_t N00004370; //0x4BC8
	uint32_t N000057F1; //0x4BCC
	uint32_t N00004371; //0x4BD0
	uint32_t N000057F3; //0x4BD4
	uint32_t N00004372; //0x4BD8
	uint32_t N000057F5; //0x4BDC
	uint32_t N00004373; //0x4BE0
	uint32_t N000057F7; //0x4BE4
	uint32_t N00004374; //0x4BE8
	uint32_t N000057F9; //0x4BEC
	uint32_t N00004375; //0x4BF0
	uint32_t N000057FB; //0x4BF4
	uint32_t N00004376; //0x4BF8
	uint32_t N000057FD; //0x4BFC
	uint32_t N00004377; //0x4C00
	uint32_t N000057FF; //0x4C04
	uint32_t N00004378; //0x4C08
	uint32_t N00005801; //0x4C0C
	uint32_t N00004379; //0x4C10
	uint32_t N00005803; //0x4C14
	uint32_t N0000437A; //0x4C18
	uint32_t N00005805; //0x4C1C
	uint32_t N0000437B; //0x4C20
	uint32_t N00005807; //0x4C24
	uint32_t N0000437C; //0x4C28
	uint32_t N00005809; //0x4C2C
	uint32_t N0000437D; //0x4C30
	uint32_t N0000580B; //0x4C34
	uint32_t N0000437E; //0x4C38
	uint32_t N0000580D; //0x4C3C
	uint32_t N0000437F; //0x4C40
	uint32_t N0000580F; //0x4C44
	uint32_t N00004380; //0x4C48
	uint32_t N00005811; //0x4C4C
	uint32_t N00004381; //0x4C50
	uint32_t N00005813; //0x4C54
	uint32_t N00004382; //0x4C58
	uint32_t N00005815; //0x4C5C
	uint32_t N00004383; //0x4C60
	uint32_t N00005817; //0x4C64
	uint32_t N00004384; //0x4C68
	uint32_t N00005819; //0x4C6C
	uint32_t N00004385; //0x4C70
	uint32_t N0000581B; //0x4C74
	uint32_t N00004386; //0x4C78
	uint32_t N0000581D; //0x4C7C
	uint32_t N00004387; //0x4C80
	uint32_t N0000581F; //0x4C84
	uint32_t N00004388; //0x4C88
	uint32_t N00005821; //0x4C8C
	uint32_t N00004389; //0x4C90
	uint32_t N00005823; //0x4C94
	uint32_t N0000438A; //0x4C98
	uint32_t N00005825; //0x4C9C
	uint32_t N0000438B; //0x4CA0
	uint32_t N00005827; //0x4CA4
	uint32_t N0000438C; //0x4CA8
	uint32_t N00005829; //0x4CAC
	uint32_t N0000438D; //0x4CB0
	uint32_t N0000582B; //0x4CB4
	uint32_t N0000438E; //0x4CB8
	uint32_t N0000582D; //0x4CBC
	uint32_t N0000438F; //0x4CC0
	uint32_t N0000582F; //0x4CC4
	uint32_t N00004390; //0x4CC8
	uint32_t N00005831; //0x4CCC
	uint32_t N00004391; //0x4CD0
	uint32_t N00005833; //0x4CD4
	uint32_t N00004392; //0x4CD8
	uint32_t N00005835; //0x4CDC
	uint32_t N00004393; //0x4CE0
	uint32_t N00005837; //0x4CE4
	uint32_t N00004394; //0x4CE8
	uint32_t N00005839; //0x4CEC
	uint32_t N00004395; //0x4CF0
	uint32_t N0000583B; //0x4CF4
	uint32_t N00004396; //0x4CF8
	uint32_t N0000583D; //0x4CFC
	uint32_t N00004397; //0x4D00
	uint32_t N0000583F; //0x4D04
	uint32_t N00004398; //0x4D08
	uint32_t N00005841; //0x4D0C
	uint32_t N00004399; //0x4D10
	uint32_t N00005843; //0x4D14
	uint32_t N0000439A; //0x4D18
	uint32_t N00005845; //0x4D1C
	uint32_t N0000439B; //0x4D20
	uint32_t N00005847; //0x4D24
	uint32_t N0000439C; //0x4D28
	uint32_t N00005849; //0x4D2C
	uint32_t N0000439D; //0x4D30
	uint32_t N0000584B; //0x4D34
	uint32_t N0000439E; //0x4D38
	uint32_t N0000584D; //0x4D3C
	uint32_t N0000439F; //0x4D40
	uint32_t N0000584F; //0x4D44
	uint32_t N000043A0; //0x4D48
	uint32_t N00005851; //0x4D4C
	uint32_t N000043A1; //0x4D50
	uint32_t N00005853; //0x4D54
	uint32_t N000043A2; //0x4D58
	uint32_t N00005855; //0x4D5C
	uint32_t N000043A3; //0x4D60
	uint32_t N00005857; //0x4D64
	uint32_t N000043A4; //0x4D68
	uint32_t N00005859; //0x4D6C
	uint32_t N000043A5; //0x4D70
	uint32_t N0000585B; //0x4D74
	uint32_t N000043A6; //0x4D78
	uint32_t N0000585D; //0x4D7C
	uint32_t N000043A7; //0x4D80
	uint32_t N0000585F; //0x4D84
	uint32_t N000043A8; //0x4D88
	uint32_t r_HarassmentEngineerTribeGlobalId1; //0x4D8C
	uint32_t N000043A9; //0x4D90
	uint32_t N00005863; //0x4D94
	uint32_t N000043AA; //0x4D98
	uint32_t N00005865; //0x4D9C
	uint32_t N000043AB; //0x4DA0
	uint32_t N00005867; //0x4DA4
	uint32_t N000043AC; //0x4DA8
	uint32_t N00005869; //0x4DAC
	uint32_t N000043AD; //0x4DB0
	uint32_t N0000586B; //0x4DB4
	uint32_t N000043AE; //0x4DB8
	uint32_t N0000586D; //0x4DBC
	uint32_t N000043AF; //0x4DC0
	uint32_t N0000586F; //0x4DC4
	uint32_t N000043B0; //0x4DC8
	uint32_t N00005871; //0x4DCC
	uint32_t N000043B1; //0x4DD0
	uint32_t N00005873; //0x4DD4
	uint32_t N000043B2; //0x4DD8
	uint32_t N00005875; //0x4DDC
	uint32_t N000043B3; //0x4DE0
	uint32_t N00005877; //0x4DE4
	uint32_t N000043B4; //0x4DE8
	uint32_t N00005879; //0x4DEC
	uint32_t N000043B5; //0x4DF0
	uint32_t N0000587B; //0x4DF4
	uint32_t N000043B6; //0x4DF8
	uint32_t N0000587D; //0x4DFC
	uint32_t N000043B7; //0x4E00
	uint32_t N0000587F; //0x4E04
	uint32_t N000043B8; //0x4E08
	uint32_t N00005881; //0x4E0C
	uint32_t N000043B9; //0x4E10
	uint32_t N00005883; //0x4E14
	uint32_t N000043BA; //0x4E18
	uint32_t N00005885; //0x4E1C
	uint32_t N000043BB; //0x4E20
	uint32_t N00005887; //0x4E24
	uint32_t N000043BC; //0x4E28
	uint32_t N00005889; //0x4E2C
	uint32_t N000043BD; //0x4E30
	uint32_t N0000588B; //0x4E34
	uint32_t N000043BE; //0x4E38
	uint32_t N0000588D; //0x4E3C
	uint32_t N000043BF; //0x4E40
	uint32_t N0000588F; //0x4E44
	uint32_t N000043C0; //0x4E48
	uint32_t N00005891; //0x4E4C
	uint32_t N000043C1; //0x4E50
	uint32_t N00005893; //0x4E54
	uint32_t N000043C2; //0x4E58
	uint32_t N00005895; //0x4E5C
	uint32_t N000043C3; //0x4E60
	uint32_t N00005897; //0x4E64
	uint32_t N000043C4; //0x4E68
	uint32_t N00005899; //0x4E6C
	uint32_t N000043C5; //0x4E70
	uint32_t N0000589B; //0x4E74
	uint32_t N000043C6; //0x4E78
	uint32_t N0000589D; //0x4E7C
	uint32_t N000043C7; //0x4E80
	uint32_t N0000589F; //0x4E84
	uint32_t N000043C8; //0x4E88
	uint32_t N000058A1; //0x4E8C
	uint32_t N000043C9; //0x4E90
	uint32_t N000058A3; //0x4E94
	uint32_t N000043CA; //0x4E98
	uint32_t N000058A5; //0x4E9C
	uint32_t N000043CB; //0x4EA0
	uint32_t N000058A7; //0x4EA4
	uint32_t N000043CC; //0x4EA8
	uint32_t N000058A9; //0x4EAC
	uint32_t N000043CD; //0x4EB0
	uint32_t N000058AB; //0x4EB4
	uint32_t N000043CE; //0x4EB8
	uint32_t N000058AD; //0x4EBC
	uint32_t N000043CF; //0x4EC0
	uint32_t N000058AF; //0x4EC4
	uint32_t N000043D0; //0x4EC8
	uint32_t N000058B1; //0x4ECC
	uint32_t N000043D1; //0x4ED0
	uint32_t N000058B3; //0x4ED4
	uint32_t N000043D2; //0x4ED8
	uint32_t N000058B5; //0x4EDC
	uint32_t N000043D3; //0x4EE0
	uint32_t N000058B7; //0x4EE4
	uint32_t N000043D4; //0x4EE8
	uint32_t N000058B9; //0x4EEC
	uint32_t N000043D5; //0x4EF0
	uint32_t N000058BB; //0x4EF4
	uint32_t N000043D6; //0x4EF8
	uint32_t N000058BD; //0x4EFC
	uint32_t N000043D7; //0x4F00
	uint32_t N000058BF; //0x4F04
	uint32_t N000043D8; //0x4F08
	uint32_t N000058C1; //0x4F0C
	uint32_t N000043D9; //0x4F10
	uint32_t N000058C3; //0x4F14
	uint32_t N000043DA; //0x4F18
	uint32_t N000058C5; //0x4F1C
	uint32_t N000043DB; //0x4F20
	uint32_t N000058C7; //0x4F24
	uint32_t N000043DC; //0x4F28
	uint32_t N000058C9; //0x4F2C
	uint32_t N000043DD; //0x4F30
	uint32_t N000058CB; //0x4F34
	uint32_t N000043DE; //0x4F38
	uint32_t N000058CD; //0x4F3C
	uint32_t N000043DF; //0x4F40
	uint32_t N000058CF; //0x4F44
	uint32_t N000043E0; //0x4F48
	uint32_t N000058D1; //0x4F4C
	uint32_t N000043E1; //0x4F50
	uint32_t N000058D3; //0x4F54
	uint32_t N000043E2; //0x4F58
	uint32_t N000058D5; //0x4F5C
	uint32_t N000043E3; //0x4F60
	uint32_t N000058D7; //0x4F64
	uint32_t N000043E4; //0x4F68
	uint32_t N000058D9; //0x4F6C
	uint32_t N000043E5; //0x4F70
	uint32_t N000058DB; //0x4F74
	uint32_t N000043E6; //0x4F78
	uint32_t N000058DD; //0x4F7C
	uint32_t N000043E7; //0x4F80
	uint32_t N000058DF; //0x4F84
	uint32_t N000043E8; //0x4F88
	uint32_t N000058E1; //0x4F8C
	uint32_t N000043E9; //0x4F90
	uint32_t N000058E3; //0x4F94
	uint32_t N000043EA; //0x4F98
	uint32_t N000058E5; //0x4F9C
	uint32_t N000043EB; //0x4FA0
	uint32_t N000058E7; //0x4FA4
	uint32_t N000043EC; //0x4FA8
	uint32_t N000058E9; //0x4FAC
	uint32_t N000043ED; //0x4FB0
	uint32_t N000058EB; //0x4FB4
	uint32_t N000043EE; //0x4FB8
	uint32_t N000058ED; //0x4FBC
	uint32_t N000043EF; //0x4FC0
	uint32_t N000058EF; //0x4FC4
	uint32_t N000043F0; //0x4FC8
	uint32_t N000058F1; //0x4FCC
	uint32_t N000043F1; //0x4FD0
	uint32_t N000058F3; //0x4FD4
	uint32_t N000043F2; //0x4FD8
	uint32_t N000058F5; //0x4FDC
	uint32_t N000043F3; //0x4FE0
	uint32_t N000058F7; //0x4FE4
	uint32_t N000043F4; //0x4FE8
	uint32_t N000058F9; //0x4FEC
	uint32_t N000043F5; //0x4FF0
	uint32_t N000058FB; //0x4FF4
	uint32_t N000043F6; //0x4FF8
	uint32_t N000058FD; //0x4FFC
	uint32_t N000043F7; //0x5000
	uint32_t N000058FF; //0x5004
	uint32_t N000043F8; //0x5008
	uint32_t N00005901; //0x500C
	uint32_t N000043F9; //0x5010
	uint32_t N00005903; //0x5014
	uint32_t N000043FA; //0x5018
	uint32_t N00005905; //0x501C
	uint32_t N000043FB; //0x5020
	uint32_t N00005907; //0x5024
	uint32_t N000043FC; //0x5028
	uint32_t N00005909; //0x502C
	uint32_t N000043FD; //0x5030
	uint32_t N0000590B; //0x5034
	uint32_t N000043FE; //0x5038
	uint32_t N0000590D; //0x503C
	uint32_t N000043FF; //0x5040
	uint32_t N0000590F; //0x5044
	uint32_t N00004400; //0x5048
	uint32_t N00005911; //0x504C
	uint32_t N00004401; //0x5050
	uint32_t N00005913; //0x5054
	uint32_t N00004402; //0x5058
	uint32_t N00005915; //0x505C
	uint32_t N00004403; //0x5060
	uint32_t N00005917; //0x5064
	uint32_t N00004404; //0x5068
	uint32_t N00005919; //0x506C
	uint32_t N00004405; //0x5070
	uint32_t N0000591B; //0x5074
	uint32_t N00004406; //0x5078
	uint32_t N0000591D; //0x507C
	uint32_t N00004407; //0x5080
	uint32_t N0000591F; //0x5084
	uint32_t N00004408; //0x5088
	uint32_t N00005921; //0x508C
	uint32_t N00004409; //0x5090
	uint32_t N00005923; //0x5094
	uint32_t N0000440A; //0x5098
	uint32_t N00005925; //0x509C
	uint32_t N0000440B; //0x50A0
	uint32_t N00005927; //0x50A4
	uint32_t N0000440C; //0x50A8
	uint32_t N00005929; //0x50AC
	uint32_t N0000440D; //0x50B0
	uint32_t N0000592B; //0x50B4
	uint32_t N0000440E; //0x50B8
	uint32_t N0000592D; //0x50BC
	uint32_t N0000440F; //0x50C0
	uint32_t N0000592F; //0x50C4
	uint32_t N00004410; //0x50C8
	uint32_t N00005931; //0x50CC
	uint32_t N00004411; //0x50D0
	uint32_t N00005933; //0x50D4
	uint32_t N00004412; //0x50D8
	uint32_t N00005935; //0x50DC
	uint32_t N00004413; //0x50E0
	uint32_t N00005937; //0x50E4
	uint32_t N00004414; //0x50E8
	uint32_t N00005939; //0x50EC
	uint32_t N00004415; //0x50F0
	uint32_t N0000593B; //0x50F4
	uint32_t N00004416; //0x50F8
	uint32_t N0000593D; //0x50FC
	uint32_t N00004417; //0x5100
	uint32_t N0000593F; //0x5104
	uint32_t N00004418; //0x5108
	uint32_t N00005941; //0x510C
	uint32_t N00004419; //0x5110
	uint32_t N00005943; //0x5114
	uint32_t N0000441A; //0x5118
	uint32_t N00005945; //0x511C
	uint32_t N0000441B; //0x5120
	uint32_t N00005947; //0x5124
	uint32_t N0000441C; //0x5128
	uint32_t N00005949; //0x512C
	uint32_t N0000441D; //0x5130
	uint32_t N0000594B; //0x5134
	uint32_t N0000441E; //0x5138
	uint32_t N0000594D; //0x513C
	uint32_t N0000441F; //0x5140
	uint32_t N0000594F; //0x5144
	uint32_t N00004420; //0x5148
	uint32_t N00005951; //0x514C
	uint32_t N00004421; //0x5150
	uint32_t N00005953; //0x5154
	uint32_t N00004422; //0x5158
	uint32_t N00005955; //0x515C
	uint32_t N00004423; //0x5160
	uint32_t N00005957; //0x5164
	uint32_t N00004424; //0x5168
	uint32_t N00005959; //0x516C
	uint32_t N00004425; //0x5170
	uint32_t N0000595B; //0x5174
	uint32_t N00004426; //0x5178
	uint32_t N0000595D; //0x517C
	uint32_t N00004427; //0x5180
	uint32_t N0000595F; //0x5184
	uint32_t N00004428; //0x5188
	uint32_t N00005961; //0x518C
	uint32_t N00004429; //0x5190
	uint32_t N00005963; //0x5194
	uint32_t N0000442A; //0x5198
	uint32_t N00005965; //0x519C
	uint32_t N0000442B; //0x51A0
	uint32_t N00005967; //0x51A4
	uint32_t N0000442C; //0x51A8
	uint32_t N00005969; //0x51AC
	uint32_t N0000442D; //0x51B0
	uint32_t N0000596B; //0x51B4
	uint32_t N0000442E; //0x51B8
	uint32_t N0000596D; //0x51BC
	uint32_t N0000442F; //0x51C0
	uint32_t N0000596F; //0x51C4
	uint32_t N00004430; //0x51C8
	uint32_t N00005971; //0x51CC
	uint32_t N00004431; //0x51D0
	uint32_t N00005973; //0x51D4
	uint32_t N00004432; //0x51D8
	uint32_t N00005975; //0x51DC
	uint32_t N00004433; //0x51E0
	uint32_t N00005977; //0x51E4
	uint32_t N00004434; //0x51E8
	uint32_t N00005979; //0x51EC
	uint32_t N00004435; //0x51F0
	uint32_t N0000597B; //0x51F4
	uint32_t N00004436; //0x51F8
	uint32_t N0000597D; //0x51FC
	uint32_t N00004437; //0x5200
	uint32_t N0000597F; //0x5204
	uint32_t N00004438; //0x5208
	uint32_t N00005981; //0x520C
	uint32_t N00004439; //0x5210
	uint32_t N00005983; //0x5214
	uint32_t N0000443A; //0x5218
	uint32_t N00005985; //0x521C
	uint32_t N0000443B; //0x5220
	uint32_t N00005987; //0x5224
	uint32_t N0000443C; //0x5228
	uint32_t N00005989; //0x522C
	uint32_t N0000443D; //0x5230
	uint32_t N0000598B; //0x5234
	uint32_t N0000443E; //0x5238
	uint32_t N0000598D; //0x523C
	uint32_t N0000443F; //0x5240
	uint32_t N0000598F; //0x5244
	uint32_t N00004440; //0x5248
	uint32_t N00005991; //0x524C
	uint32_t N00004441; //0x5250
	uint32_t N00005993; //0x5254
	uint32_t N00004442; //0x5258
	uint32_t N00005995; //0x525C
	uint32_t N00004443; //0x5260
	uint32_t N00005997; //0x5264
	uint32_t N00004444; //0x5268
	uint32_t N00005999; //0x526C
	uint32_t N00004445; //0x5270
	uint32_t N0000599B; //0x5274
	uint32_t N00004446; //0x5278
	uint32_t N0000599D; //0x527C
	uint32_t N00004447; //0x5280
	uint32_t N0000599F; //0x5284
	uint32_t N00004448; //0x5288
	uint32_t N000059A1; //0x528C
	uint32_t N00004449; //0x5290
	uint32_t N000059A3; //0x5294
	uint32_t N0000444A; //0x5298
	uint32_t N000059A5; //0x529C
	uint32_t N0000444B; //0x52A0
	uint32_t N000059A7; //0x52A4
	uint32_t N0000444C; //0x52A8
	uint32_t N000059A9; //0x52AC
	uint32_t N0000444D; //0x52B0
	uint32_t N000059AB; //0x52B4
	uint32_t N0000444E; //0x52B8
	uint32_t N000059AD; //0x52BC
	uint32_t N0000444F; //0x52C0
	uint32_t N000059AF; //0x52C4
	uint32_t N00004450; //0x52C8
	uint32_t N000059B1; //0x52CC
	uint32_t N00004451; //0x52D0
	uint32_t N000059B3; //0x52D4
	uint32_t N00004452; //0x52D8
	uint32_t N000059B5; //0x52DC
	uint32_t N00004453; //0x52E0
	uint32_t N000059B7; //0x52E4
	uint32_t N00004454; //0x52E8
	uint32_t N000059B9; //0x52EC
	uint32_t N00004455; //0x52F0
	uint32_t N000059BB; //0x52F4
	uint32_t N00004456; //0x52F8
	uint32_t N000059BD; //0x52FC
	uint32_t N00004457; //0x5300
	uint32_t N000059BF; //0x5304
	uint32_t N00004458; //0x5308
	uint32_t N000059C1; //0x530C
	uint32_t N00004459; //0x5310
	uint32_t N000059C3; //0x5314
	uint32_t N0000445A; //0x5318
	uint32_t N000059C5; //0x531C
	uint32_t N0000445B; //0x5320
	uint32_t N000059C7; //0x5324
	uint32_t N0000445C; //0x5328
	uint32_t N000059C9; //0x532C
	uint32_t N0000445D; //0x5330
	uint32_t N000059CB; //0x5334
	uint32_t N0000445E; //0x5338
	uint32_t N000059CD; //0x533C
	uint32_t N0000445F; //0x5340
	uint32_t N000059CF; //0x5344
	uint32_t N00004460; //0x5348
	uint32_t N000059D1; //0x534C
	uint32_t N00004461; //0x5350
	uint32_t N000059D3; //0x5354
	uint32_t N00004462; //0x5358
	uint32_t N000059D5; //0x535C
	uint32_t N00004463; //0x5360
	uint32_t N000059D7; //0x5364
	uint32_t N00004464; //0x5368
	uint32_t N000059D9; //0x536C
	uint32_t N00004465; //0x5370
	uint32_t N000059DB; //0x5374
	uint32_t N00004466; //0x5378
	uint32_t N000059DD; //0x537C
	uint32_t N00004467; //0x5380
	uint32_t N000059DF; //0x5384
	uint32_t N00004468; //0x5388
	uint32_t N000059E1; //0x538C
	uint32_t N00004469; //0x5390
	uint32_t N000059E3; //0x5394
	uint32_t N0000446A; //0x5398
	uint32_t N000059E5; //0x539C
	uint32_t N0000446B; //0x53A0
	uint32_t N000059E7; //0x53A4
	uint32_t N0000446C; //0x53A8
	uint32_t N000059E9; //0x53AC
	uint32_t N0000446D; //0x53B0
	uint32_t N000059EB; //0x53B4
	uint32_t N0000446E; //0x53B8
	uint32_t N000059ED; //0x53BC
	uint32_t N0000446F; //0x53C0
	uint32_t N000059EF; //0x53C4
	uint32_t N00004470; //0x53C8
	uint32_t N000059F1; //0x53CC
	uint32_t N00004471; //0x53D0
	uint32_t N000059F3; //0x53D4
	uint32_t N00004472; //0x53D8
	uint32_t N000059F5; //0x53DC
	uint32_t N00004473; //0x53E0
	uint32_t N000059F7; //0x53E4
	uint32_t N00004474; //0x53E8
	uint32_t N000059F9; //0x53EC
	uint32_t N00004475; //0x53F0
	uint32_t N000059FB; //0x53F4
	uint32_t N00004476; //0x53F8
	uint32_t N000059FD; //0x53FC
	uint32_t N00004477; //0x5400
	uint32_t N000059FF; //0x5404
	uint32_t N00004478; //0x5408
	uint32_t N00005A01; //0x540C
	uint32_t N00004479; //0x5410
	uint32_t N00005A03; //0x5414
	uint32_t N0000447A; //0x5418
	uint32_t N00005A05; //0x541C
	uint32_t N0000447B; //0x5420
	uint32_t N00005A07; //0x5424
	uint32_t N0000447C; //0x5428
	uint32_t N00005A09; //0x542C
	uint32_t N0000447D; //0x5430
	uint32_t N00005A0B; //0x5434
	uint32_t N0000447E; //0x5438
	uint32_t N00005A0D; //0x543C
	uint32_t N0000447F; //0x5440
	uint32_t N00005A0F; //0x5444
	uint32_t N00004480; //0x5448
	uint32_t N00005A11; //0x544C
	uint32_t N00004481; //0x5450
	uint32_t N00005A13; //0x5454
	uint32_t N00004482; //0x5458
	uint32_t N00005A15; //0x545C
	uint32_t N00004483; //0x5460
	uint32_t N00005A17; //0x5464
	uint32_t N00004484; //0x5468
	uint32_t N00005A19; //0x546C
	uint32_t N00004485; //0x5470
	uint32_t N00005A1B; //0x5474
	uint32_t N00004486; //0x5478
	uint32_t N00005A1D; //0x547C
	uint32_t N00004487; //0x5480
	uint32_t N00005A1F; //0x5484
	uint32_t N00004488; //0x5488
	uint32_t N00005A21; //0x548C
	uint32_t N00004489; //0x5490
	uint32_t N00005A23; //0x5494
	uint32_t N0000448A; //0x5498
	uint32_t N00005A25; //0x549C
	uint32_t N0000448B; //0x54A0
	uint32_t N00005A27; //0x54A4
	uint32_t N0000448C; //0x54A8
	uint32_t N00005A29; //0x54AC
	uint32_t N0000448D; //0x54B0
	uint32_t N00005A2B; //0x54B4
	uint32_t N0000448E; //0x54B8
	uint32_t N00005A2D; //0x54BC
	uint32_t N0000448F; //0x54C0
	uint32_t N00005A2F; //0x54C4
	uint32_t N00004490; //0x54C8
	uint32_t N00005A31; //0x54CC
	uint32_t N00004491; //0x54D0
	uint32_t N00005A33; //0x54D4
	uint32_t N00004492; //0x54D8
	uint32_t N00005A35; //0x54DC
	uint32_t N00004493; //0x54E0
	uint32_t N00005A37; //0x54E4
	uint32_t N00004494; //0x54E8
	uint32_t N00005A39; //0x54EC
	uint32_t N00004495; //0x54F0
	uint32_t N00005A3B; //0x54F4
	uint32_t N00004496; //0x54F8
	uint32_t N00005A3D; //0x54FC
	uint32_t N00004497; //0x5500
	uint32_t N00005A3F; //0x5504
	uint32_t N00004498; //0x5508
	uint32_t N00005A41; //0x550C
	uint32_t N00004499; //0x5510
	uint32_t N00005A43; //0x5514
	uint32_t N0000449A; //0x5518
	uint32_t N00005A45; //0x551C
	uint32_t N0000449B; //0x5520
	uint32_t N00005A47; //0x5524
	uint32_t N0000449C; //0x5528
	uint32_t N00005A49; //0x552C
	uint32_t N0000449D; //0x5530
	uint32_t N00005A4B; //0x5534
	uint32_t N0000449E; //0x5538
	uint32_t N00005A4D; //0x553C
	uint32_t N0000449F; //0x5540
	uint32_t N00005A4F; //0x5544
	uint32_t N000044A0; //0x5548
	uint32_t N00005A51; //0x554C
	uint32_t N000044A1; //0x5550
	uint32_t N00005A53; //0x5554
	uint32_t N000044A2; //0x5558
	uint32_t N00005A55; //0x555C
	uint32_t N000044A3; //0x5560
	uint32_t N00005A57; //0x5564
	uint32_t N000044A4; //0x5568
	uint32_t N00005A59; //0x556C
	uint32_t N000044A5; //0x5570
	uint32_t N00005A5B; //0x5574
	uint32_t N000044A6; //0x5578
	uint32_t N00005A5D; //0x557C
	uint32_t N000044A7; //0x5580
	uint32_t N00005A5F; //0x5584
	uint32_t N000044A8; //0x5588
	uint32_t N00005A61; //0x558C
	uint32_t N000044A9; //0x5590
	uint32_t N00005A63; //0x5594
	uint32_t N000044AA; //0x5598
	uint32_t N00005A65; //0x559C
	uint32_t N000044AB; //0x55A0
	uint32_t N00005A67; //0x55A4
	uint32_t N000044AC; //0x55A8
	uint32_t N00005A69; //0x55AC
	uint32_t N000044AD; //0x55B0
	uint32_t N00005A6B; //0x55B4
	uint32_t N000044AE; //0x55B8
	uint32_t N00005A6D; //0x55BC
	uint32_t N000044AF; //0x55C0
	uint32_t N00005A6F; //0x55C4
	uint32_t N000044B0; //0x55C8
	uint32_t N00005A71; //0x55CC
	uint32_t N000044B1; //0x55D0
	uint32_t N00005A73; //0x55D4
	uint32_t N000044B2; //0x55D8
	uint32_t N00005A75; //0x55DC
	uint32_t N000044B3; //0x55E0
	uint32_t N00005A77; //0x55E4
	uint32_t N000044B4; //0x55E8
	uint32_t N00005A79; //0x55EC
	uint32_t N000044B5; //0x55F0
	uint32_t N00005A7B; //0x55F4
	uint32_t N000044B6; //0x55F8
	uint32_t N00005A7D; //0x55FC
	uint32_t N000044B7; //0x5600
	uint32_t N00005A7F; //0x5604
	uint32_t N000044B8; //0x5608
	uint32_t N00005A81; //0x560C
	uint32_t N000044B9; //0x5610
	uint32_t N00005A83; //0x5614
	uint32_t N000044BA; //0x5618
	uint32_t N00005A85; //0x561C
	uint32_t N000044BB; //0x5620
	uint32_t N00005A87; //0x5624
	uint32_t N000044BC; //0x5628
	uint32_t N00005A89; //0x562C
	uint32_t N000044BD; //0x5630
	uint32_t N00005A8B; //0x5634
	uint32_t N000044BE; //0x5638
	uint32_t N00005A8D; //0x563C
	uint32_t N000044BF; //0x5640
	uint32_t N00005A8F; //0x5644
	uint32_t N000044C0; //0x5648
	uint32_t N00005A91; //0x564C
	uint32_t N000044C1; //0x5650
	uint32_t N00005A93; //0x5654
	uint32_t N000044C2; //0x5658
	uint32_t N00005A95; //0x565C
	uint32_t N000044C3; //0x5660
	uint32_t N00005A97; //0x5664
	uint32_t N000044C4; //0x5668
	uint32_t N00005A99; //0x566C
	uint32_t N000044C5; //0x5670
	uint32_t N00005A9B; //0x5674
	uint32_t N000044C6; //0x5678
	uint32_t N00005A9D; //0x567C
	uint32_t N000044C7; //0x5680
	uint32_t N00005A9F; //0x5684
	uint32_t N000044C8; //0x5688
	uint32_t N00005AA1; //0x568C
	uint32_t N000044C9; //0x5690
	uint32_t N00005AA3; //0x5694
	uint32_t N000044CA; //0x5698
	uint32_t N00005AA5; //0x569C
	uint32_t N000044CB; //0x56A0
	uint32_t N00005AA7; //0x56A4
	uint32_t N000044CC; //0x56A8
	uint32_t N00005AA9; //0x56AC
	uint32_t N000044CD; //0x56B0
	uint32_t N00005AAB; //0x56B4
	uint32_t N000044CE; //0x56B8
	uint32_t N00005AAD; //0x56BC
	uint32_t N000044CF; //0x56C0
	uint32_t N00005AAF; //0x56C4
	uint32_t N000044D0; //0x56C8
	uint32_t N00005AB1; //0x56CC
	uint32_t N000044D1; //0x56D0
	uint32_t N00005AB3; //0x56D4
	uint32_t N000044D2; //0x56D8
	uint32_t N00005AB5; //0x56DC
	uint32_t N000044D3; //0x56E0
	uint32_t N00005AB7; //0x56E4
	uint32_t N000044D4; //0x56E8
	uint32_t N00005AB9; //0x56EC
	uint32_t N000044D5; //0x56F0
	uint32_t N00005ABB; //0x56F4
	uint32_t N000044D6; //0x56F8
	uint32_t N00005ABD; //0x56FC
	uint32_t N000044D7; //0x5700
	uint32_t N00005ABF; //0x5704
	uint32_t N000044D8; //0x5708
	uint32_t N00005AC1; //0x570C
	uint32_t N000044D9; //0x5710
	uint32_t N00005AC3; //0x5714
	uint32_t N000044DA; //0x5718
	uint32_t N00005AC5; //0x571C
	uint32_t N000044DB; //0x5720
	uint32_t N00005AC7; //0x5724
	uint32_t N000044DC; //0x5728
	uint32_t N00005AC9; //0x572C
	uint32_t N000044DD; //0x5730
	uint32_t N00005ACB; //0x5734
	uint32_t N000044DE; //0x5738
	uint32_t N00005ACD; //0x573C
	uint32_t N000044DF; //0x5740
	uint32_t N00005ACF; //0x5744
	uint32_t N000044E0; //0x5748
	uint32_t N00005AD1; //0x574C
	uint32_t N000044E1; //0x5750
	uint32_t N00005AD3; //0x5754
	uint32_t N000044E2; //0x5758
	uint32_t N00005AD5; //0x575C
	uint32_t N000044E3; //0x5760
	uint32_t N00005AD7; //0x5764
	uint32_t N000044E4; //0x5768
	uint32_t N00005AD9; //0x576C
	uint32_t N000044E5; //0x5770
	uint32_t N00005ADB; //0x5774
	uint32_t N000044E6; //0x5778
	uint32_t N00005ADD; //0x577C
	uint32_t N000044E7; //0x5780
	uint32_t N00005ADF; //0x5784
	uint32_t N000044E8; //0x5788
	uint32_t N00005AE1; //0x578C
	uint32_t N000044E9; //0x5790
	uint32_t N00005AE3; //0x5794
	uint32_t N000044EA; //0x5798
	uint32_t N00005AE5; //0x579C
	uint32_t N000044EB; //0x57A0
	uint32_t N00005AE7; //0x57A4
	uint32_t N000044EC; //0x57A8
	uint32_t N00005AE9; //0x57AC
	uint32_t N000044ED; //0x57B0
	uint32_t N00005AEB; //0x57B4
	uint32_t N000044EE; //0x57B8
	uint32_t N00005AED; //0x57BC
	uint32_t N000044EF; //0x57C0
	uint32_t N00005AEF; //0x57C4
	uint32_t N000044F0; //0x57C8
	uint32_t N00005AF1; //0x57CC
	uint32_t N000044F1; //0x57D0
	uint32_t N00005AF3; //0x57D4
	uint32_t N000044F2; //0x57D8
	uint32_t N00005AF5; //0x57DC
	uint32_t N000044F3; //0x57E0
	uint32_t N00005AF7; //0x57E4
	uint32_t N000044F4; //0x57E8
	uint32_t N00005AF9; //0x57EC
	uint32_t N000044F5; //0x57F0
	uint32_t N00005AFB; //0x57F4
	uint32_t N000044F6; //0x57F8
	uint32_t N00005AFD; //0x57FC
	uint32_t N000044F7; //0x5800
	uint32_t N00005AFF; //0x5804
	uint32_t N000044F8; //0x5808
	uint32_t N00005B01; //0x580C
	uint32_t N000044F9; //0x5810
	uint32_t N00005B03; //0x5814
	uint32_t N000044FA; //0x5818
	uint32_t N00005B05; //0x581C
	uint32_t N000044FB; //0x5820
	uint32_t N00005B07; //0x5824
	uint32_t N000044FC; //0x5828
	uint32_t N00005B09; //0x582C
	uint32_t N000044FD; //0x5830
	uint32_t N00003966; //0x5834
	uint32_t N00005B0C; //0x5838
}; //Size: 0x583C


#endif
