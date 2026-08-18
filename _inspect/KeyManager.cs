using System;
using System.Collections.Generic;
using System.IO;
using CrusaderDE;
using Noesis;
using UnityEngine;

public class KeyManager : MonoBehaviour
{
	public enum KeyState
	{
		Off = 0,
		Down = 1,
		Held = 2,
		Up = 3,
		Shift = 65536,
		Ctrl = 131072,
		Alt = 262144,
		MP = 524288,
		Mask = 65535
	}

	public class PlaceHotKey
	{
		public Enums.KeyFunctions function;

		public Enums.eMappers mapper;

		public PlaceHotKey(Enums.KeyFunctions f, Enums.eMappers m)
		{
			function = f;
			mapper = m;
		}
	}

	public static KeyManager instance;

	public int[] values;

	public int[] keyCodeMap;

	public int[] keys;

	public int leftShiftMap = -1;

	public int rightShiftMap = -1;

	public int leftCtrlMap = -1;

	public int rightCtrlMap = -1;

	public int altMap = -1;

	public int altGrMap = -1;

	public bool hotKeySelectorMode;

	public int hotKeyCurrentKey;

	public int hotKeyCurrentKeyPressed;

	public DateTime lastQuickSaveTime = DateTime.MinValue;

	public bool cursorUpHeld;

	public bool cursorDownHeld;

	public int[,] functionMap = new int[203, 2];

	public Enums.KeyFunctions[] directSentFunctions = new Enums.KeyFunctions[63]
	{
		Enums.KeyFunctions.HomeKeep,
		Enums.KeyFunctions.Market,
		Enums.KeyFunctions.Signpost,
		Enums.KeyFunctions.Barracks,
		Enums.KeyFunctions.MercPost,
		Enums.KeyFunctions.BedouinStockade,
		Enums.KeyFunctions.Granary,
		Enums.KeyFunctions.Armoury,
		Enums.KeyFunctions.EngineersGuild,
		Enums.KeyFunctions.TunnelersGuild,
		Enums.KeyFunctions.Cathedral,
		Enums.KeyFunctions.Lord,
		Enums.KeyFunctions.CycleLord,
		Enums.KeyFunctions.AllyToggle,
		Enums.KeyFunctions.MPPing,
		Enums.KeyFunctions.GroupTroops0,
		Enums.KeyFunctions.GroupTroops1,
		Enums.KeyFunctions.GroupTroops2,
		Enums.KeyFunctions.GroupTroops3,
		Enums.KeyFunctions.GroupTroops4,
		Enums.KeyFunctions.GroupTroops5,
		Enums.KeyFunctions.GroupTroops6,
		Enums.KeyFunctions.GroupTroops7,
		Enums.KeyFunctions.GroupTroops8,
		Enums.KeyFunctions.GroupTroops9,
		Enums.KeyFunctions.SelectClan0,
		Enums.KeyFunctions.SelectClan1,
		Enums.KeyFunctions.SelectClan2,
		Enums.KeyFunctions.SelectClan3,
		Enums.KeyFunctions.SelectClan4,
		Enums.KeyFunctions.SelectClan5,
		Enums.KeyFunctions.SelectClan6,
		Enums.KeyFunctions.SelectClan7,
		Enums.KeyFunctions.SelectClan8,
		Enums.KeyFunctions.SelectClan9,
		Enums.KeyFunctions.SetBookmark0,
		Enums.KeyFunctions.SetBookmark1,
		Enums.KeyFunctions.SetBookmark2,
		Enums.KeyFunctions.SetBookmark3,
		Enums.KeyFunctions.SetBookmark4,
		Enums.KeyFunctions.SetBookmark5,
		Enums.KeyFunctions.SetBookmark6,
		Enums.KeyFunctions.SetBookmark7,
		Enums.KeyFunctions.SetBookmark8,
		Enums.KeyFunctions.SetBookmark9,
		Enums.KeyFunctions.GotoBookmark0,
		Enums.KeyFunctions.GotoBookmark1,
		Enums.KeyFunctions.GotoBookmark2,
		Enums.KeyFunctions.GotoBookmark3,
		Enums.KeyFunctions.GotoBookmark4,
		Enums.KeyFunctions.GotoBookmark5,
		Enums.KeyFunctions.GotoBookmark6,
		Enums.KeyFunctions.GotoBookmark7,
		Enums.KeyFunctions.GotoBookmark8,
		Enums.KeyFunctions.GotoBookmark9,
		Enums.KeyFunctions.ExtremePower1,
		Enums.KeyFunctions.ExtremePower2,
		Enums.KeyFunctions.ExtremePower3,
		Enums.KeyFunctions.ExtremePower4,
		Enums.KeyFunctions.ExtremePower5,
		Enums.KeyFunctions.ExtremePower6,
		Enums.KeyFunctions.ExtremePower7,
		Enums.KeyFunctions.ExtremePower8
	};

	public PlaceHotKey[] buildingPlaceHotKeys = new PlaceHotKey[78]
	{
		new PlaceHotKey(Enums.KeyFunctions.PlaceWalls, Enums.eMappers.MAPPER_WALL),
		new PlaceHotKey(Enums.KeyFunctions.PlaceStairs, Enums.eMappers.MAPPER_STAIR),
		new PlaceHotKey(Enums.KeyFunctions.PlaceLowWalls, Enums.eMappers.MAPPER_WOODWALL),
		new PlaceHotKey(Enums.KeyFunctions.PlaceCrenal, Enums.eMappers.MAPPER_CRENAL),
		new PlaceHotKey(Enums.KeyFunctions.PlaceBarracks, Enums.eMappers.MAPPER_BARRACKS_STONE),
		new PlaceHotKey(Enums.KeyFunctions.PlaceMercPost, Enums.eMappers.MAPPER_BARRACKS_WOOD),
		new PlaceHotKey(Enums.KeyFunctions.PlaceBedouinStockade, Enums.eMappers.MAPPER_BEDOUIN_STOCKADE),
		new PlaceHotKey(Enums.KeyFunctions.PlaceArmoury, Enums.eMappers.MAPPER_ARMOURY),
		new PlaceHotKey(Enums.KeyFunctions.PlaceTower1, Enums.eMappers.MAPPER_TOWER1),
		new PlaceHotKey(Enums.KeyFunctions.PlaceTower2, Enums.eMappers.MAPPER_TOWER2),
		new PlaceHotKey(Enums.KeyFunctions.PlaceTower3, Enums.eMappers.MAPPER_TOWER3),
		new PlaceHotKey(Enums.KeyFunctions.PlaceTower4, Enums.eMappers.MAPPER_TOWER4),
		new PlaceHotKey(Enums.KeyFunctions.PlaceTower5, Enums.eMappers.MAPPER_TOWER5),
		new PlaceHotKey(Enums.KeyFunctions.PlaceEngineersGuild, Enums.eMappers.MAPPER_ENGINEERS_GUILD),
		new PlaceHotKey(Enums.KeyFunctions.PlaceTunnelGuild, Enums.eMappers.MAPPER_TUNNELERS_GUILD),
		new PlaceHotKey(Enums.KeyFunctions.PlaceBallista, Enums.eMappers.MAPPER_BALLISTA),
		new PlaceHotKey(Enums.KeyFunctions.PlaceMangonel, Enums.eMappers.MAPPER_MANGONEL),
		new PlaceHotKey(Enums.KeyFunctions.PlaceStables, Enums.eMappers.MAPPER_STABLES),
		new PlaceHotKey(Enums.KeyFunctions.PlaceSmelter, Enums.eMappers.MAPPER_OIL_SMELTER),
		new PlaceHotKey(Enums.KeyFunctions.PlaceSmallGatehouse, Enums.eMappers.MAPPER_GATE_STONE1A),
		new PlaceHotKey(Enums.KeyFunctions.PlaceLargeGatehouse, Enums.eMappers.MAPPER_GATE_STONE2A),
		new PlaceHotKey(Enums.KeyFunctions.PlaceDrawbridge, Enums.eMappers.MAPPER_DRAWBRIDGE),
		new PlaceHotKey(Enums.KeyFunctions.PlaceDogCage, Enums.eMappers.MAPPER_DOG_CAGE),
		new PlaceHotKey(Enums.KeyFunctions.PlacePitchDitch, Enums.eMappers.MAPPER_PITCH_DITCH),
		new PlaceHotKey(Enums.KeyFunctions.PlaceKillingPit, Enums.eMappers.MAPPER_KILLING_PIT),
		new PlaceHotKey(Enums.KeyFunctions.PlaceDigMoat, Enums.eMappers.MAPPER_MOAT),
		new PlaceHotKey(Enums.KeyFunctions.PlaceClearMoat, Enums.eMappers.MAPPER_ANTIMOAT),
		new PlaceHotKey(Enums.KeyFunctions.PlaceBrazier, Enums.eMappers.MAPPER_BRAZIER),
		new PlaceHotKey(Enums.KeyFunctions.PlaceStockpile, Enums.eMappers.MAPPER_STORES),
		new PlaceHotKey(Enums.KeyFunctions.PlaceWoodcutter, Enums.eMappers.MAPPER_WOODSMAN),
		new PlaceHotKey(Enums.KeyFunctions.PlaceQuarry, Enums.eMappers.MAPPER_QUARRY),
		new PlaceHotKey(Enums.KeyFunctions.PlaceOxen, Enums.eMappers.MAPPER_OXENBASE),
		new PlaceHotKey(Enums.KeyFunctions.PlaceIronMine, Enums.eMappers.MAPPER_IRON_MINE),
		new PlaceHotKey(Enums.KeyFunctions.PlacePitchRig, Enums.eMappers.MAPPER_PITCH_WORKINGS),
		new PlaceHotKey(Enums.KeyFunctions.PlaceMarket, Enums.eMappers.MAPPER_TRADEPOST),
		new PlaceHotKey(Enums.KeyFunctions.PlaceHunter, Enums.eMappers.MAPPER_HUNTER),
		new PlaceHotKey(Enums.KeyFunctions.PlaceDairyFarm, Enums.eMappers.MAPPER_CATTLEFARM),
		new PlaceHotKey(Enums.KeyFunctions.PlaceAppleFarm, Enums.eMappers.MAPPER_APPLEFARM),
		new PlaceHotKey(Enums.KeyFunctions.PlaceWheatFarm, Enums.eMappers.MAPPER_WHEATFARM),
		new PlaceHotKey(Enums.KeyFunctions.PlaceHopsFarm, Enums.eMappers.MAPPER_HOPSFARM),
		new PlaceHotKey(Enums.KeyFunctions.PlaceHouse, Enums.eMappers.MAPPER_HOVEL),
		new PlaceHotKey(Enums.KeyFunctions.PlaceChurchMosque1, Enums.eMappers.MAPPER_CHURCH1),
		new PlaceHotKey(Enums.KeyFunctions.PlaceChurchMosque2, Enums.eMappers.MAPPER_CHURCH2),
		new PlaceHotKey(Enums.KeyFunctions.PlaceChurchMosque3, Enums.eMappers.MAPPER_CHURCH3),
		new PlaceHotKey(Enums.KeyFunctions.PlaceApothecary, Enums.eMappers.MAPPER_HEALER),
		new PlaceHotKey(Enums.KeyFunctions.PlaceWell, Enums.eMappers.MAPPER_WELL),
		new PlaceHotKey(Enums.KeyFunctions.PlaceWaterpot, Enums.eMappers.MAPPER_WATERPOT),
		new PlaceHotKey(Enums.KeyFunctions.PlaceFletcher, Enums.eMappers.MAPPER_FLETCHER),
		new PlaceHotKey(Enums.KeyFunctions.PlacePoleturner, Enums.eMappers.MAPPER_POLETURNER),
		new PlaceHotKey(Enums.KeyFunctions.PlaceBlacksmith, Enums.eMappers.MAPPER_BLACKSMITH),
		new PlaceHotKey(Enums.KeyFunctions.PlaceTanner, Enums.eMappers.MAPPER_TANNER),
		new PlaceHotKey(Enums.KeyFunctions.PlaceArmourer, Enums.eMappers.MAPPER_ARMOURER),
		new PlaceHotKey(Enums.KeyFunctions.PlaceGranary, Enums.eMappers.MAPPER_GRANARY),
		new PlaceHotKey(Enums.KeyFunctions.PlaceBaker, Enums.eMappers.MAPPER_BAKER),
		new PlaceHotKey(Enums.KeyFunctions.PlaceMill, Enums.eMappers.MAPPER_MILL),
		new PlaceHotKey(Enums.KeyFunctions.PlaceBrewer, Enums.eMappers.MAPPER_BREWER),
		new PlaceHotKey(Enums.KeyFunctions.PlaceInn, Enums.eMappers.MAPPER_INN),
		new PlaceHotKey(Enums.KeyFunctions.PlaceMaypole, Enums.eMappers.MAPPER_MAYPOLE),
		new PlaceHotKey(Enums.KeyFunctions.PlaceDancingBear, Enums.eMappers.MAPPER_DANCING_BEAR),
		new PlaceHotKey(Enums.KeyFunctions.PlaceGardens1, Enums.eMappers.MAPPER_GARDEN1),
		new PlaceHotKey(Enums.KeyFunctions.PlaceGardens2, Enums.eMappers.MAPPER_GARDEN7),
		new PlaceHotKey(Enums.KeyFunctions.PlaceGardens3, Enums.eMappers.MAPPER_GARDEN10),
		new PlaceHotKey(Enums.KeyFunctions.PlaceStatue, Enums.eMappers.MAPPER_STATUE1),
		new PlaceHotKey(Enums.KeyFunctions.PlaceShrine, Enums.eMappers.MAPPER_SHRINE1),
		new PlaceHotKey(Enums.KeyFunctions.PlaceFlag1, Enums.eMappers.MAPPER_FLAG_TYPE0),
		new PlaceHotKey(Enums.KeyFunctions.PlaceFlag2, Enums.eMappers.MAPPER_FLAG_TYPE1),
		new PlaceHotKey(Enums.KeyFunctions.PlaceFlag3, Enums.eMappers.MAPPER_FLAG_TYPE2),
		new PlaceHotKey(Enums.KeyFunctions.PlaceFlag4, Enums.eMappers.MAPPER_FLAG_TYPE3),
		new PlaceHotKey(Enums.KeyFunctions.PlaceGallows, Enums.eMappers.MAPPER_GALLOWS),
		new PlaceHotKey(Enums.KeyFunctions.PlaceCesspit, Enums.eMappers.MAPPER_CESS_PIT1),
		new PlaceHotKey(Enums.KeyFunctions.PlaceStocks, Enums.eMappers.MAPPER_STOCKS),
		new PlaceHotKey(Enums.KeyFunctions.PlaceHeads, Enums.eMappers.MAPPER_HEADS),
		new PlaceHotKey(Enums.KeyFunctions.PlaceBurningStake, Enums.eMappers.MAPPER_BURNING_STAKE),
		new PlaceHotKey(Enums.KeyFunctions.PlaceDungeon, Enums.eMappers.MAPPER_DUNGEON),
		new PlaceHotKey(Enums.KeyFunctions.PlaceRack, Enums.eMappers.MAPPER_RACK_STRETCHING),
		new PlaceHotKey(Enums.KeyFunctions.PlaceGibbett, Enums.eMappers.MAPPER_GIBBET),
		new PlaceHotKey(Enums.KeyFunctions.PlaceChoppingBlock, Enums.eMappers.MAPPER_CHOPPING_BLOCK),
		new PlaceHotKey(Enums.KeyFunctions.PlaceDunkingStool, Enums.eMappers.MAPPER_DUNKING_STOOL)
	};

	public DateTime ignoreNextEscape = DateTime.MinValue;

	public float RadarHeldX;

	public float RadarHeldY;

	public bool CursorUpHeld => cursorUpHeld;

	public bool CursorDownHeld => cursorDownHeld;

	public bool HotKeySelectorMode
	{
		get
		{
			return hotKeySelectorMode;
		}
		set
		{
			hotKeySelectorMode = value;
			if (value)
			{
				hotKeyCurrentKeyPressed = 0;
				hotKeyCurrentKey = 0;
			}
		}
	}

	public int HotKeyCurrentKey => hotKeyCurrentKey;

	public void Awake()
	{
		instance = this;
		values = (int[])Enum.GetValues(typeof(KeyCode));
		keys = new int[values.Length];
		int num = 0;
		for (int i = 0; i < values.Length; i++)
		{
			if (values[i] > num)
			{
				num = values[i];
			}
		}
		keyCodeMap = new int[num + 1];
		for (int j = 0; j < values.Length; j++)
		{
			keyCodeMap[j] = -1;
		}
		for (int k = 0; k < values.Length; k++)
		{
			keyCodeMap[values[k]] = k;
			if (values[k] == 304)
			{
				leftShiftMap = k;
			}
			if (values[k] == 303)
			{
				rightShiftMap = k;
			}
			if (values[k] == 306)
			{
				leftCtrlMap = k;
			}
			if (values[k] == 305)
			{
				rightCtrlMap = k;
			}
			if (values[k] == 308)
			{
				altMap = k;
			}
			if (values[k] == 307)
			{
				altGrMap = k;
			}
		}
		SetDefaultFunctionsNew();
	}

	public void SetDefaultFunctionsNew()
	{
		for (int i = 0; i < 203; i++)
		{
			functionMap[i, 0] = -1;
			functionMap[i, 1] = -1;
		}
		functionMap[1, 0] = 97;
		functionMap[1, 1] = 276;
		functionMap[2, 0] = 100;
		functionMap[2, 1] = 275;
		functionMap[3, 0] = 119;
		functionMap[3, 1] = 273;
		functionMap[4, 0] = 115;
		functionMap[4, 1] = 274;
		functionMap[5, 0] = 112;
		functionMap[6, 0] = 104;
		functionMap[7, 0] = 109;
		functionMap[8, 0] = 111;
		functionMap[9, 0] = 98;
		functionMap[97, 0] = 110;
		functionMap[99, 0] = 118;
		functionMap[10, 0] = 103;
		functionMap[119, 0] = 117;
		functionMap[118, 0] = 105;
		functionMap[117, 0] = 262247;
		functionMap[201, 0] = 262242;
		functionMap[121, 0] = 99;
		functionMap[122, 0] = -1;
		functionMap[123, 0] = -1;
		functionMap[124, 0] = -1;
		functionMap[125, 0] = -1;
		functionMap[126, 0] = -1;
		functionMap[127, 0] = -1;
		functionMap[128, 0] = -1;
		functionMap[92, 0] = 108;
		functionMap[93, 0] = 65644;
		functionMap[98, 0] = 65650;
		functionMap[94, 0] = 9;
		functionMap[11, 0] = 113;
		functionMap[12, 0] = 101;
		functionMap[13, 0] = 32;
		functionMap[15, 0] = 122;
		functionMap[14, 0] = 120;
		functionMap[16, 0] = 114;
		functionMap[17, 0] = 116;
		functionMap[18, 0] = 121;
		functionMap[120, 0] = 8;
		functionMap[199, 0] = -1;
		functionMap[19, 0] = 131120;
		functionMap[20, 0] = 131121;
		functionMap[21, 0] = 131122;
		functionMap[22, 0] = 131123;
		functionMap[23, 0] = 131124;
		functionMap[24, 0] = 131125;
		functionMap[25, 0] = 131126;
		functionMap[26, 0] = 131127;
		functionMap[27, 0] = 131128;
		functionMap[28, 0] = 131129;
		functionMap[29, 0] = 48;
		functionMap[30, 0] = 49;
		functionMap[31, 0] = 50;
		functionMap[32, 0] = 51;
		functionMap[33, 0] = 52;
		functionMap[34, 0] = 53;
		functionMap[35, 0] = 54;
		functionMap[36, 0] = 55;
		functionMap[37, 0] = 56;
		functionMap[38, 0] = 57;
		functionMap[39, 0] = 393264;
		functionMap[40, 0] = 393265;
		functionMap[41, 0] = 393266;
		functionMap[42, 0] = 393267;
		functionMap[43, 0] = 393268;
		functionMap[44, 0] = 393269;
		functionMap[45, 0] = 393270;
		functionMap[46, 0] = 393271;
		functionMap[47, 0] = 393272;
		functionMap[48, 0] = 393273;
		functionMap[49, 0] = 262192;
		functionMap[50, 0] = 262193;
		functionMap[51, 0] = 262194;
		functionMap[52, 0] = 262195;
		functionMap[53, 0] = 262196;
		functionMap[54, 0] = 262197;
		functionMap[55, 0] = 262198;
		functionMap[56, 0] = 262199;
		functionMap[57, 0] = 262200;
		functionMap[58, 0] = 262201;
		functionMap[100, 0] = 65585;
		functionMap[101, 0] = 65586;
		functionMap[102, 0] = 65587;
		functionMap[103, 0] = 65588;
		functionMap[104, 0] = 65589;
		functionMap[105, 0] = 65590;
		functionMap[106, 0] = 65591;
		functionMap[107, 0] = 65592;
		functionMap[59, 0] = 65824;
		functionMap[60, 0] = 65819;
		functionMap[61, 0] = 65818;
		functionMap[62, 0] = 270;
		functionMap[63, 0] = 269;
		functionMap[62, 1] = 61;
		functionMap[63, 1] = 45;
		functionMap[64, 0] = 262266;
		functionMap[65, 0] = 102;
		functionMap[67, 0] = 262183;
		functionMap[68, 0] = 282;
		functionMap[69, 0] = 524301;
		functionMap[69, 1] = 524559;
		functionMap[70, 0] = 786548;
		functionMap[71, 0] = 27;
		functionMap[72, 0] = 524570;
		functionMap[73, 0] = 524571;
		functionMap[74, 0] = 524572;
		functionMap[75, 0] = 524573;
		functionMap[76, 0] = 524574;
		functionMap[77, 0] = 524575;
		functionMap[78, 0] = 524576;
		functionMap[79, 0] = 524577;
		functionMap[80, 0] = 524578;
		functionMap[81, 0] = 524579;
		functionMap[82, 0] = 655642;
		functionMap[83, 0] = 655643;
		functionMap[109, 0] = 655644;
		functionMap[110, 0] = 655645;
		functionMap[111, 0] = 655646;
		functionMap[112, 0] = 655647;
		functionMap[113, 0] = 655648;
		functionMap[114, 0] = 655649;
		functionMap[115, 0] = 655650;
		functionMap[116, 0] = 655651;
		functionMap[84, 0] = 262248;
		functionMap[85, 0] = 262262;
		functionMap[86, 0] = 262245;
		functionMap[200, 0] = 262243;
		functionMap[202, 0] = 262256;
		functionMap[87, 0] = 281;
		functionMap[88, 0] = 280;
		functionMap[89, 0] = 107;
		functionMap[90, 0] = 106;
		functionMap[91, 0] = 278;
		functionMap[96, 0] = 262251;
		functionMap[95, 0] = 262264;
		functionMap[108, 0] = 131176;
	}

	public void SetDefaultFunctionsSH1()
	{
		for (int i = 0; i < 203; i++)
		{
			functionMap[i, 0] = -1;
			functionMap[i, 1] = -1;
		}
		functionMap[1, 0] = 276;
		functionMap[2, 0] = 275;
		functionMap[3, 0] = 273;
		functionMap[4, 0] = 274;
		functionMap[5, 0] = 112;
		functionMap[6, 0] = 104;
		functionMap[7, 0] = 109;
		functionMap[8, 0] = 115;
		functionMap[9, 0] = 98;
		functionMap[97, 0] = 110;
		functionMap[99, 0] = 118;
		functionMap[10, 0] = 103;
		functionMap[119, 0] = 117;
		functionMap[118, 0] = 105;
		functionMap[117, 0] = 116;
		functionMap[201, 0] = 262242;
		functionMap[121, 0] = -1;
		functionMap[122, 0] = -1;
		functionMap[123, 0] = -1;
		functionMap[124, 0] = -1;
		functionMap[125, 0] = -1;
		functionMap[126, 0] = -1;
		functionMap[127, 0] = -1;
		functionMap[128, 0] = -1;
		functionMap[92, 0] = 108;
		functionMap[93, 0] = 65644;
		functionMap[98, 0] = 262258;
		functionMap[94, 0] = 9;
		functionMap[11, 0] = 120;
		functionMap[12, 0] = 99;
		functionMap[13, 0] = 32;
		functionMap[15, 0] = 122;
		functionMap[16, 0] = 113;
		functionMap[17, 0] = 119;
		functionMap[18, 0] = 101;
		functionMap[120, 0] = 8;
		functionMap[199, 0] = -1;
		functionMap[19, 0] = 131120;
		functionMap[20, 0] = 131121;
		functionMap[21, 0] = 131122;
		functionMap[22, 0] = 131123;
		functionMap[23, 0] = 131124;
		functionMap[24, 0] = 131125;
		functionMap[25, 0] = 131126;
		functionMap[26, 0] = 131127;
		functionMap[27, 0] = 131128;
		functionMap[28, 0] = 131129;
		functionMap[29, 0] = 48;
		functionMap[30, 0] = 49;
		functionMap[31, 0] = 50;
		functionMap[32, 0] = 51;
		functionMap[33, 0] = 52;
		functionMap[34, 0] = 53;
		functionMap[35, 0] = 54;
		functionMap[36, 0] = 55;
		functionMap[37, 0] = 56;
		functionMap[38, 0] = 57;
		functionMap[39, 0] = 393264;
		functionMap[40, 0] = 393265;
		functionMap[41, 0] = 393266;
		functionMap[42, 0] = 393267;
		functionMap[43, 0] = 393268;
		functionMap[44, 0] = 393269;
		functionMap[45, 0] = 393270;
		functionMap[46, 0] = 393271;
		functionMap[47, 0] = 393272;
		functionMap[48, 0] = 393273;
		functionMap[49, 0] = 262192;
		functionMap[50, 0] = 262193;
		functionMap[51, 0] = 262194;
		functionMap[52, 0] = 262195;
		functionMap[53, 0] = 262196;
		functionMap[54, 0] = 262197;
		functionMap[55, 0] = 262198;
		functionMap[56, 0] = 262199;
		functionMap[57, 0] = 262200;
		functionMap[58, 0] = 262201;
		functionMap[100, 0] = 65585;
		functionMap[101, 0] = 65586;
		functionMap[102, 0] = 65587;
		functionMap[103, 0] = 65588;
		functionMap[104, 0] = 65589;
		functionMap[105, 0] = 65590;
		functionMap[106, 0] = 65591;
		functionMap[107, 0] = 65592;
		functionMap[59, 0] = 65824;
		functionMap[60, 0] = 65819;
		functionMap[61, 0] = 65818;
		functionMap[62, 0] = 270;
		functionMap[63, 0] = 269;
		functionMap[64, 0] = 262266;
		functionMap[65, 0] = 102;
		functionMap[67, 0] = 262183;
		functionMap[68, 0] = 282;
		functionMap[69, 0] = 524301;
		functionMap[69, 1] = 524559;
		functionMap[70, 0] = 786548;
		functionMap[71, 0] = 27;
		functionMap[72, 0] = 524570;
		functionMap[73, 0] = 524571;
		functionMap[74, 0] = 524572;
		functionMap[75, 0] = 524573;
		functionMap[76, 0] = 524574;
		functionMap[77, 0] = 524575;
		functionMap[78, 0] = 524576;
		functionMap[79, 0] = 524577;
		functionMap[80, 0] = 524578;
		functionMap[81, 0] = 524579;
		functionMap[82, 0] = 655642;
		functionMap[83, 0] = 655643;
		functionMap[109, 0] = 655644;
		functionMap[110, 0] = 655645;
		functionMap[111, 0] = 655646;
		functionMap[112, 0] = 655647;
		functionMap[113, 0] = 655648;
		functionMap[114, 0] = 655649;
		functionMap[115, 0] = 655650;
		functionMap[116, 0] = 655651;
		functionMap[84, 0] = 262248;
		functionMap[85, 0] = 262262;
		functionMap[86, 0] = 262245;
		functionMap[200, 0] = 262243;
		functionMap[202, 0] = 262256;
		functionMap[87, 0] = 281;
		functionMap[88, 0] = 280;
		functionMap[89, 0] = 107;
		functionMap[90, 0] = 106;
		functionMap[91, 0] = 278;
		functionMap[108, 0] = 131176;
		functionMap[96, 0] = 262251;
		functionMap[95, 0] = 262264;
	}

	public void Update()
	{
		//IL_0450: Unknown result type (might be due to invalid IL or missing references)
		//IL_0455: Unknown result type (might be due to invalid IL or missing references)
		//IL_0457: Unknown result type (might be due to invalid IL or missing references)
		//IL_045c: Unknown result type (might be due to invalid IL or missing references)
		//IL_047b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1ad1: Unknown result type (might be due to invalid IL or missing references)
		//IL_1ad7: Invalid comparison between Unknown and I4
		//IL_1afd: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b03: Invalid comparison between Unknown and I4
		//IL_1904: Unknown result type (might be due to invalid IL or missing references)
		//IL_190a: Invalid comparison between Unknown and I4
		if (hotKeySelectorMode)
		{
			for (int i = 0; i < values.Length; i++)
			{
				if (Input.GetKey((KeyCode)values[i]))
				{
					if (keys[i] == 0 || keys[i] == 3)
					{
						if (i != leftShiftMap && i != rightShiftMap && i != leftCtrlMap && i != rightCtrlMap && i != altMap && i != altGrMap && values[i] < 330 && values[i] != 323 && values[i] != 324 && values[i] != 27 && values[i] != 316 && values[i] != 302 && values[i] != 310 && values[i] != 311 && values[i] != 312 && values[i] != 319 && values[i] != 19)
						{
							hotKeyCurrentKeyPressed = values[i];
							hotKeyCurrentKey = hotKeyCurrentKeyPressed;
							if (isShiftDown())
							{
								hotKeyCurrentKey |= 65536;
							}
							if (isCtrlDown())
							{
								hotKeyCurrentKey |= 131072;
							}
							if (isAltDown())
							{
								hotKeyCurrentKey |= 262144;
							}
						}
						keys[i] = 1;
					}
					else if (keys[i] == 1)
					{
						keys[i] = 2;
					}
				}
				else if (keys[i] == 1 || keys[i] == 2)
				{
					keys[i] = 3;
				}
				else if (keys[i] == 3)
				{
					keys[i] = 0;
				}
			}
			return;
		}
		bool noesisHasKeyboard = FatControler.instance.NoesisHasKeyboard;
		for (int j = 0; j < values.Length; j++)
		{
			if (!noesisHasKeyboard && Input.GetKey((KeyCode)values[j]))
			{
				if (keys[j] == 0 || keys[j] == 3)
				{
					keys[j] = 1;
				}
				else if (keys[j] == 1)
				{
					keys[j] = 2;
				}
			}
			else if (keys[j] == 1 || keys[j] == 2)
			{
				keys[j] = 3;
			}
			else if (keys[j] == 3)
			{
				keys[j] = 0;
			}
		}
		cursorUpHeld = IsKeyHeldDown((KeyCode)273, ignoreModifiers: true);
		cursorDownHeld = IsKeyHeldDown((KeyCode)274, ignoreModifiers: true);
		if ((Object)(object)Director.instance != (Object)null && Director.instance.SimRunning)
		{
			Enums.KeyFunctions[] array = directSentFunctions;
			foreach (Enums.KeyFunctions keyFunctions in array)
			{
				if (!IsActionPressed(keyFunctions) || GameData.Instance.lastGameState == null)
				{
					continue;
				}
				switch (keyFunctions)
				{
				case Enums.KeyFunctions.SetBookmark0:
				case Enums.KeyFunctions.SetBookmark1:
				case Enums.KeyFunctions.SetBookmark2:
				case Enums.KeyFunctions.SetBookmark3:
				case Enums.KeyFunctions.SetBookmark4:
				case Enums.KeyFunctions.SetBookmark5:
				case Enums.KeyFunctions.SetBookmark6:
				case Enums.KeyFunctions.SetBookmark7:
				case Enums.KeyFunctions.SetBookmark8:
				case Enums.KeyFunctions.SetBookmark9:
				{
					Vector3 mouseMapVector = Vector3.zero;
					Vector3Int mouseTileMapVector = Vector3Int.zero;
					int clickDepth = -1;
					GameMap.instance.CalcMapTileFromMousePos(new Vector3((float)(Screen.width / 2), (float)(Screen.height / 2), 0f), ref mouseMapVector, ref mouseTileMapVector, ref clickDepth, useBuildingHeight: false);
					int value = -1;
					int value2 = -1;
					GameMapTile mapTile = GameMap.instance.getMapTile(((Vector3Int)(ref mouseTileMapVector)).x, ((Vector3Int)(ref mouseTileMapVector)).y);
					if (mapTile != null)
					{
						value = mapTile.gameMapX;
						value2 = mapTile.gameMapY;
					}
					EngineInterface.GameAction(keyFunctions, value, value2);
					break;
				}
				case Enums.KeyFunctions.Barracks:
					if (ConfigSettings.Settings_SH1CentreControls || (GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 1))
					{
						EngineInterface.GameAction(keyFunctions);
					}
					else
					{
						EngineInterface.GameAction(Enums.GameActionCommand.SelectBuildingType, 9, -1);
					}
					break;
				case Enums.KeyFunctions.MercPost:
					if (ConfigSettings.Settings_SH1CentreControls || (GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 44))
					{
						EngineInterface.GameAction(keyFunctions);
					}
					else
					{
						EngineInterface.GameAction(Enums.GameActionCommand.SelectBuildingType, 8, -1);
					}
					break;
				case Enums.KeyFunctions.BedouinStockade:
					if (ConfigSettings.Settings_SH1CentreControls || (GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 63))
					{
						EngineInterface.GameAction(keyFunctions);
					}
					else
					{
						EngineInterface.GameAction(Enums.GameActionCommand.SelectBuildingType, 108, -1);
					}
					break;
				case Enums.KeyFunctions.HomeKeep:
					if (ConfigSettings.Settings_SH1CentreControls || (GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 2))
					{
						EngineInterface.GameAction(keyFunctions);
					}
					else
					{
						EngineInterface.GameAction(Enums.GameActionCommand.SelectBuildingType, 40, -1);
					}
					break;
				case Enums.KeyFunctions.Market:
					if (ConfigSettings.Settings_SH1CentreControls || (GameData.Instance.lastGameState.app_mode == 16 && (GameData.Instance.lastGameState.app_sub_mode == 25 || GameData.Instance.lastGameState.app_sub_mode == 56 || GameData.Instance.lastGameState.app_sub_mode == 55 || GameData.Instance.lastGameState.app_sub_mode == 57 || GameData.Instance.lastGameState.app_sub_mode == 54 || GameData.Instance.lastGameState.app_sub_mode == 53)))
					{
						EngineInterface.GameAction(keyFunctions);
					}
					else
					{
						EngineInterface.GameAction(Enums.GameActionCommand.SelectBuildingType, 26, -1);
					}
					break;
				case Enums.KeyFunctions.Granary:
					if (ConfigSettings.Settings_SH1CentreControls || (GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 4))
					{
						EngineInterface.GameAction(keyFunctions);
					}
					else
					{
						EngineInterface.GameAction(Enums.GameActionCommand.SelectBuildingType, 19, -1);
					}
					break;
				case Enums.KeyFunctions.EngineersGuild:
					if (ConfigSettings.Settings_SH1CentreControls || (GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 23))
					{
						EngineInterface.GameAction(keyFunctions);
					}
					else
					{
						EngineInterface.GameAction(Enums.GameActionCommand.SelectBuildingType, 24, -1);
					}
					break;
				case Enums.KeyFunctions.TunnelersGuild:
					if (ConfigSettings.Settings_SH1CentreControls || (GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 24))
					{
						EngineInterface.GameAction(keyFunctions);
					}
					else
					{
						EngineInterface.GameAction(Enums.GameActionCommand.SelectBuildingType, 25, -1);
					}
					break;
				case Enums.KeyFunctions.Armoury:
					if (ConfigSettings.Settings_SH1CentreControls || (GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 12))
					{
						EngineInterface.GameAction(keyFunctions);
					}
					else
					{
						EngineInterface.GameAction(Enums.GameActionCommand.SelectBuildingType, 11, -1);
					}
					break;
				case Enums.KeyFunctions.Cathedral:
					if (!ConfigSettings.Settings_SH1CentreControls && (GameData.Instance.lastGameState.app_mode != 16 || GameData.Instance.lastGameState.app_sub_mode != 96))
					{
						EngineInterface.GameAction(Enums.GameActionCommand.SelectBuildingType, 38, -1);
					}
					break;
				case Enums.KeyFunctions.SelectClan0:
				case Enums.KeyFunctions.SelectClan1:
				case Enums.KeyFunctions.SelectClan2:
				case Enums.KeyFunctions.SelectClan3:
				case Enums.KeyFunctions.SelectClan4:
				case Enums.KeyFunctions.SelectClan5:
				case Enums.KeyFunctions.SelectClan6:
				case Enums.KeyFunctions.SelectClan7:
				case Enums.KeyFunctions.SelectClan8:
				case Enums.KeyFunctions.SelectClan9:
					if (GameData.Instance.lastGameState.app_mode == 16)
					{
						int num2 = (int)(keyFunctions - 30);
						if (GameData.Instance.lastGameState.app_sub_mode == 1)
						{
							if (num2 >= 0 && num2 <= 6)
							{
								switch (num2)
								{
								case 0:
									num2 = 0;
									break;
								case 1:
									num2 = 2;
									break;
								case 2:
									num2 = 3;
									break;
								case 3:
									num2 = 1;
									break;
								case 4:
									num2 = 4;
									break;
								case 5:
									num2 = 5;
									break;
								case 6:
									num2 = 6;
									break;
								}
								EditorDirector.instance.placeBuildingInteraction((Enums.eMappers)(332 + num2));
							}
							break;
						}
						if (GameData.Instance.lastGameState.app_sub_mode == 44)
						{
							if (num2 >= 0 && num2 <= 6)
							{
								switch (num2)
								{
								case 0:
									num2 = 0;
									break;
								case 1:
									num2 = 2;
									break;
								case 2:
									num2 = 4;
									break;
								case 3:
									num2 = 3;
									break;
								case 4:
									num2 = 1;
									break;
								case 5:
									num2 = 6;
									break;
								case 6:
									num2 = 5;
									break;
								}
								EditorDirector.instance.placeBuildingInteraction((Enums.eMappers)(360 + num2));
							}
							break;
						}
						if (GameData.Instance.lastGameState.app_sub_mode == 63)
						{
							if (num2 >= 0)
							{
								switch (num2)
								{
								case 0:
									EditorDirector.instance.placeBuildingInteraction(Enums.eMappers.MAPPER_PLACE_ASSEMBLY_POINTBS5);
									break;
								case 1:
									EditorDirector.instance.placeBuildingInteraction(Enums.eMappers.MAPPER_PLACE_ASSEMBLY_POINTBS7);
									break;
								case 2:
									EditorDirector.instance.placeBuildingInteraction(Enums.eMappers.MAPPER_PLACE_ASSEMBLY_POINTBS1);
									break;
								case 3:
									EditorDirector.instance.placeBuildingInteraction(Enums.eMappers.MAPPER_PLACE_ASSEMBLY_POINTBS6);
									break;
								case 4:
									EditorDirector.instance.placeBuildingInteraction(Enums.eMappers.MAPPER_PLACE_ASSEMBLY_POINTBS8);
									break;
								case 5:
									EditorDirector.instance.placeBuildingInteraction(Enums.eMappers.MAPPER_PLACE_ASSEMBLY_POINTBS3);
									break;
								case 6:
									EditorDirector.instance.placeBuildingInteraction(Enums.eMappers.MAPPER_PLACE_ASSEMBLY_POINTBS2);
									break;
								case 7:
									EditorDirector.instance.placeBuildingInteraction(Enums.eMappers.MAPPER_PLACE_ASSEMBLY_POINTBS4);
									break;
								}
							}
							break;
						}
						if (GameData.Instance.lastGameState.app_sub_mode == 23)
						{
							if (num2 >= 0 && num2 <= 1)
							{
								EditorDirector.instance.placeBuildingInteraction((Enums.eMappers)(367 + num2));
							}
							break;
						}
						if (GameData.Instance.lastGameState.app_sub_mode == 24)
						{
							if (num2 == 0)
							{
								EditorDirector.instance.placeBuildingInteraction((Enums.eMappers)(369 + num2));
							}
							break;
						}
						if (GameData.Instance.lastGameState.app_sub_mode == 96)
						{
							if (num2 == 0)
							{
								EditorDirector.instance.placeBuildingInteraction((Enums.eMappers)(370 + num2));
							}
							break;
						}
					}
					EngineInterface.GameAction(keyFunctions);
					break;
				case Enums.KeyFunctions.ExtremePower1:
				case Enums.KeyFunctions.ExtremePower2:
				case Enums.KeyFunctions.ExtremePower3:
				case Enums.KeyFunctions.ExtremePower4:
				case Enums.KeyFunctions.ExtremePower5:
				case Enums.KeyFunctions.ExtremePower6:
				case Enums.KeyFunctions.ExtremePower7:
				case Enums.KeyFunctions.ExtremePower8:
				{
					int num = (int)(keyFunctions - 100);
					if (num >= 0 && num <= 7)
					{
						EngineInterface.GameAction(Enums.GameActionCommand.ExtremePower, num, num);
					}
					break;
				}
				default:
					EngineInterface.GameAction(keyFunctions);
					break;
				}
			}
			if (IsActionPressed(Enums.KeyFunctions.FlattenLandscape) && !Director.instance.Paused)
			{
				EngineInterface.toggleFlattenedLandscapeMode();
			}
			if (IsActionPressed(Enums.KeyFunctions.MapRotateLeft) && !Director.instance.Paused)
			{
				GameMap.instance.RotateMapLeft();
			}
			if (IsActionPressed(Enums.KeyFunctions.MapRotateRight) && !Director.instance.Paused)
			{
				GameMap.instance.RotateMapRight();
			}
			if (IsActionPressed(Enums.KeyFunctions.RotateBuilding) && !Director.instance.Paused && (MainControls.instance.CurrentAction == 5 || MainControls.instance.CurrentAction == 3))
			{
				EngineInterface.GameAction(Enums.GameActionCommand.RotateBuilding, 0, 0);
			}
			PlaceHotKey[] array2 = buildingPlaceHotKeys;
			foreach (PlaceHotKey placeHotKey in array2)
			{
				if (IsActionPressed(placeHotKey.function))
				{
					if (!Director.instance.Paused && EngineInterface.IsMapperAvailable((int)placeHotKey.mapper) && !MainViewModel.Instance.FreezeMainControls)
					{
						EditorDirector.instance.placeBuildingInteraction(placeHotKey.mapper);
					}
					break;
				}
			}
			if (IsActionPressed(Enums.KeyFunctions.ZoomOut) && !Director.instance.Paused)
			{
				if (doesActionKeyFunctionExist(Enums.KeyFunctions.ZoomIn))
				{
					if (ConfigSettings.Settings_ExtraZoom)
					{
						PerfectPixelWithZoom.instance.adjustZoom(-0.5f);
					}
					else
					{
						PerfectPixelWithZoom.instance.adjustZoom(-1f);
					}
				}
				else if (ConfigSettings.Settings_ExtraZoom)
				{
					PerfectPixelWithZoom.instance.adjustZoom(-0.5f, loop: true);
				}
				else
				{
					PerfectPixelWithZoom.instance.adjustZoom(-1f, loop: true);
				}
			}
			if (IsActionPressed(Enums.KeyFunctions.ZoomIn) && !Director.instance.Paused)
			{
				if (ConfigSettings.Settings_ExtraZoom)
				{
					PerfectPixelWithZoom.instance.adjustZoom(0.5f);
				}
				else
				{
					PerfectPixelWithZoom.instance.adjustZoom(1f);
				}
			}
			if (IsActionPressed(Enums.KeyFunctions.Patrol))
			{
				EditorDirector.instance.placeBuilding(Enums.eMappers.MAPPER_PATROL);
			}
			if (IsActionPressed(Enums.KeyFunctions.StanceStand))
			{
				int app_mode = GameData.Instance.lastGameState.app_mode;
				int app_sub_mode = GameData.Instance.lastGameState.app_sub_mode;
				if (app_mode == 14 && (app_sub_mode == 61 || app_sub_mode == 62))
				{
					EngineInterface.GameAction(Enums.GameActionCommand.Troops_ChangeStance, -1, 287);
				}
			}
			if (IsActionPressed(Enums.KeyFunctions.StanceDefensive))
			{
				int app_mode2 = GameData.Instance.lastGameState.app_mode;
				int app_sub_mode2 = GameData.Instance.lastGameState.app_sub_mode;
				if (app_mode2 == 14 && (app_sub_mode2 == 61 || app_sub_mode2 == 62))
				{
					EngineInterface.GameAction(Enums.GameActionCommand.Troops_ChangeStance, -1, 288);
				}
			}
			if (IsActionPressed(Enums.KeyFunctions.StanceAggressive))
			{
				int app_mode3 = GameData.Instance.lastGameState.app_mode;
				int app_sub_mode3 = GameData.Instance.lastGameState.app_sub_mode;
				if (app_mode3 == 14 && (app_sub_mode3 == 61 || app_sub_mode3 == 62))
				{
					EngineInterface.GameAction(Enums.GameActionCommand.Troops_ChangeStance, -1, 289);
				}
			}
			if (IsActionPressed(Enums.KeyFunctions.Stop))
			{
				int app_mode4 = GameData.Instance.lastGameState.app_mode;
				int app_sub_mode4 = GameData.Instance.lastGameState.app_sub_mode;
				if (app_mode4 == 14 && (app_sub_mode4 == 61 || app_sub_mode4 == 62))
				{
					EngineInterface.GameAction(Enums.GameActionCommand.Troops_Stop, 0, 0);
				}
			}
			if (IsActionPressed(Enums.KeyFunctions.AttackHere))
			{
				int app_mode5 = GameData.Instance.lastGameState.app_mode;
				int app_sub_mode5 = GameData.Instance.lastGameState.app_sub_mode;
				if (app_mode5 == 14 && (app_sub_mode5 == 61 || app_sub_mode5 == 62) && GameData.Instance.lastGameState.troops_show_attack_here_and_type > 0)
				{
					EngineInterface.GameAction(Enums.GameActionCommand.Troops_AttackHere, 0, 0);
				}
			}
			if (IsActionPressed(Enums.KeyFunctions.ToggleHealthBars))
			{
				GameMap.instance.ToggleHealthBars();
			}
			if (IsActionPressed(Enums.KeyFunctions.Load) && !MainViewModel.Instance.IsMapEditorMode && !MainViewModel.Instance.Show_HUD_Briefing && !Director.instance.MultiplayerGame)
			{
				bool wasPaused = Director.instance.Paused;
				if (!wasPaused)
				{
					Director.instance.SetPausedState(state: true);
				}
				HUD_LoadSaveRequester.OpenLoadSaveRequester(Enums.RequesterTypes.LoadSinglePlayerGame, delegate(string filename, FileHeader header)
				{
					Director.instance.SetPausedState(state: false);
					EditorDirector.instance.stopGameSim();
					Platform_Multiplayer.Instance.gameMembers = null;
					EditorDirector.instance.loadSaveGame(header.filePath, header.standAlone_filename, header);
				}, delegate
				{
					if (!wasPaused)
					{
						Director.instance.SetPausedState(state: false);
					}
				});
			}
			if (IsActionPressed(Enums.KeyFunctions.Save) && !MainViewModel.Instance.IsMapEditorMode && !MainViewModel.Instance.Show_HUD_Briefing && Director.instance.SimRunning)
			{
				if (!Director.instance.MultiplayerGame)
				{
					bool wasPaused2 = Director.instance.Paused;
					if (!wasPaused2)
					{
						Director.instance.SetPausedState(state: true);
					}
					HUD_LoadSaveRequester.OpenLoadSaveRequester(Enums.RequesterTypes.SaveSinglePlayerGame, delegate(string filename, FileHeader header)
					{
						string path3 = filename + ".sav";
						string path4 = Path.Combine(ConfigSettings.GetSavesPath(), path3);
						EditorDirector.instance.SaveSaveGameOrMap(path4, "");
						if (!wasPaused2)
						{
							Director.instance.SetPausedState(state: false);
						}
					}, delegate
					{
						if (!wasPaused2)
						{
							Director.instance.SetPausedState(state: false);
						}
					});
				}
				else
				{
					HUD_LoadSaveRequester.OpenLoadSaveRequester(Enums.RequesterTypes.SaveSinglePlayerGame, delegate(string filename, FileHeader header)
					{
						EngineInterface.TriggerMPSave(filename + ".msv");
					}, delegate
					{
					});
				}
			}
			if (!Director.instance.MultiplayerGame)
			{
				if (IsActionPressed(Enums.KeyFunctions.IncreaseEngineSpeed))
				{
					Director.instance.IncreaseFrameRate();
					if (MainViewModel.Instance.Show_HUD_Options)
					{
						MainViewModel.Instance.HUDOptions.RefreshGameSpeed();
					}
				}
				if (IsActionPressed(Enums.KeyFunctions.DecreaseEngineSpeed))
				{
					Director.instance.DecreaseFrameRate();
					if (MainViewModel.Instance.Show_HUD_Options)
					{
						MainViewModel.Instance.HUDOptions.RefreshGameSpeed();
					}
				}
				if (IsActionPressed(Enums.KeyFunctions.Pause) && !MainViewModel.Instance.Show_HUD_Briefing && GameData.Instance.lastGameState != null)
				{
					if (GameData.Instance.lastGameState.game_paused > 0)
					{
						EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, 0, 0);
						OnScreenText.Instance.addOSTEntry(Enums.eOnScreenText.OST_GAME_PAUSED, 0);
						SFXManager.instance.playGenieSpeech(3, "game_running.wav", 1f);
					}
					else
					{
						EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, 1, 1);
						OnScreenText.Instance.addOSTEntry(Enums.eOnScreenText.OST_GAME_PAUSED, 1);
						SFXManager.instance.playGenieSpeech(3, "game_paused.wav", 1f);
					}
				}
				if (ConfigSettings.Settings_CheatKeysEnabled)
				{
					if (IsActionPressed(Enums.KeyFunctions.Cheat_freestuff) && GameData.Instance.lastGameState != null)
					{
						if (GameData.Instance.lastGameState.free_buildingCheat == 0)
						{
							GameData.Instance.lastGameState.free_buildingCheat = 1;
							EngineInterface.GameAction(Enums.GameActionCommand.SH1Cheats, 1, 0);
							ConfigSettings.AchievementsDisabled = true;
						}
						else
						{
							GameData.Instance.lastGameState.free_buildingCheat = 0;
							EngineInterface.GameAction(Enums.GameActionCommand.SH1Cheats, 0, 0);
						}
					}
					if (IsActionPressed(Enums.KeyFunctions.Cheat_gold))
					{
						EngineInterface.GameAction(Enums.GameActionCommand.SH1Cheats, 3, 0);
						EngineInterface.GameAction(Enums.GameActionCommand.SH1Cheats, 2, 0);
						SFXManager.instance.playSound(326);
						ConfigSettings.AchievementsDisabled = true;
					}
				}
			}
			if (Director.instance.MultiplayerGame && IsActionPressed(Enums.KeyFunctions.ShowPings))
			{
				if (!OnScreenText.Instance.isOSTActive(Enums.eOnScreenText.OST_PINGS))
				{
					OnScreenText.Instance.addOSTEntry(Enums.eOnScreenText.OST_PINGS, 1);
				}
				else
				{
					OnScreenText.Instance.removeOSTEntry(Enums.eOnScreenText.OST_PINGS);
				}
			}
			if ((Director.instance.MultiplayerGame || Director.instance.SkirmishModeGame) && !MainViewModel.Instance.Show_HUD_Briefing)
			{
				int num3 = -1;
				if (IsActionPressed(Enums.KeyFunctions.Insult1))
				{
					num3 = 1;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult2))
				{
					num3 = 2;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult3))
				{
					num3 = 3;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult4))
				{
					num3 = 4;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult5))
				{
					num3 = 5;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult6))
				{
					num3 = 6;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult7))
				{
					num3 = 7;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult8))
				{
					num3 = 8;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult9))
				{
					num3 = 9;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult10))
				{
					num3 = 10;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult11))
				{
					num3 = 11;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult12))
				{
					num3 = 12;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult13))
				{
					num3 = 13;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult14))
				{
					num3 = 14;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult15))
				{
					num3 = 15;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult16))
				{
					num3 = 16;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult17))
				{
					num3 = 17;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult18))
				{
					num3 = 18;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult19))
				{
					num3 = 19;
				}
				if (IsActionPressed(Enums.KeyFunctions.Insult20))
				{
					num3 = 20;
				}
				if (num3 >= 0)
				{
					int playerID = GameData.Instance.playerID;
					if (Director.instance.SkirmishModeGame)
					{
						EngineInterface.GameAction(Enums.GameActionCommand.SkirmishInsult, 0, 0);
					}
					else
					{
						int[] players = new int[9];
						int[] teams = new int[9];
						EngineInterface.GetMultiplayerChatInfo(ref players, ref teams);
						List<int> list = new List<int>();
						list.Clear();
						for (int num4 = 1; num4 < 9; num4++)
						{
							if (players[num4] > 0)
							{
								list.Add(players[num4]);
							}
						}
						Platform_Multiplayer.Instance.SendIngameChatInsult(list, num3);
					}
					if (!ConfigSettings.Settings_MuteInsults)
					{
						MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getPlayerName(playerID), playerID, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_INSULTS, num3));
						if (!ConfigSettings.Settings_MuteInsultSpeech)
						{
							SFXManager.instance.playInsult(num3);
						}
					}
				}
			}
			if (IsActionPressed(Enums.KeyFunctions.ToggleUI))
			{
				MainControls.instance.toggleUIVisibility();
			}
			if (IsActionPressed(Enums.KeyFunctions.ToggleFrameRate))
			{
				EditorDirector.instance.toggleOSTFrameRate();
			}
			if (IsActionPressed(Enums.KeyFunctions.RadarZoomIn))
			{
				GameMap.instance.changeRadarMapSize(-64);
			}
			if (IsActionPressed(Enums.KeyFunctions.RadarZoomOut))
			{
				GameMap.instance.changeRadarMapSize(64);
			}
			if (!MainViewModel.Instance.IsMapEditorMode)
			{
				if (IsActionPressed(Enums.KeyFunctions.ToggleGoods) && !MainViewModel.Instance.Show_HUD_Goods_Button_Disabled)
				{
					MainViewModel.Instance.ButtonExtendedFeaturesFunction("Goods");
				}
				if (IsActionPressed(Enums.KeyFunctions.ToggleObjectives) && MainViewModel.Instance.Show_HUD_Extras_Button_Objectves)
				{
					MainViewModel.Instance.ButtonExtendedFeaturesFunction("Objectives");
				}
				if (!Director.instance.MultiplayerGame && IsActionPressed(Enums.KeyFunctions.QuickSave))
				{
					DateTime now = DateTime.Now;
					if (now > lastQuickSaveTime)
					{
						lastQuickSaveTime = now.AddSeconds(20.0);
						string path = "QuickSave " + now.Year + "-" + now.Month.ToString("D2") + "-" + now.Day.ToString("D2") + " " + now.ToLongTimeString().Replace(':', '-') + ".sav";
						string path2 = Path.Combine(ConfigSettings.GetSavesPath(), path);
						EditorDirector.instance.SaveSaveGameOrMap(path2, "");
					}
				}
			}
			if (!MainViewModel.Instance.IsMapEditorMode && GameData.Instance.game_type == 2 && GameData.Instance.mapType == Enums.GameModes.BUILD && IsActionPressed(Enums.KeyFunctions.FreeBuildEvents) && !MainViewModel.Instance.Show_HUD_Briefing)
			{
				HUD_FreebuildMenu.ToggleMenu();
			}
			if (IsActionPressed(Enums.KeyFunctions.OpenChat) && !MainViewModel.Instance.Show_HUD_Briefing)
			{
				if (Director.instance.MultiplayerGame)
				{
					MainViewModel.Instance.HUDMPChatPanel.ToggleMultiplayerChat();
				}
				else if (Director.instance.SkirmishModeGame && !MainViewModel.Instance.Show_HUD_LoadSaveRequester)
				{
					HUD_AlliesPanel.Open(state: true);
				}
			}
			if (MainViewModel.Instance.IsMapEditorMode)
			{
				if (IsActionPressed(Enums.KeyFunctions.Special) && GameData.Instance.scenarioOverview != null)
				{
					if (MainViewModel.Instance.ShowingScenario)
					{
						if (GameData.Instance.scenarioOverview.special_start > 0)
						{
							GameData.Instance.scenarioOverview.special_start = 0;
						}
						else
						{
							GameData.Instance.scenarioOverview.special_start = 1;
						}
						EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_Special, 0, GameData.Instance.scenarioOverview.special_start);
					}
					else
					{
						MainViewModel.Instance.Show_HUD_ScenarioSpecial = !MainViewModel.Instance.Show_HUD_ScenarioSpecial;
					}
				}
				if (IsActionPressed(Enums.KeyFunctions.EditorHoldTime))
				{
					EngineInterface.GameAction(Enums.GameActionCommand.HoldTime, 0, 0);
				}
				if (IsActionPressed(Enums.KeyFunctions.EditorRespawnLord))
				{
					EngineInterface.GameAction(Enums.GameActionCommand.RespawnLord, 0, 0);
				}
				if (IsActionPressed(Enums.KeyFunctions.EditorWipeAnimals))
				{
					EngineInterface.GameAction(Enums.GameActionCommand.WipeAnimals, 0, 0);
				}
				if (IsActionPressed(Enums.KeyFunctions.EditorShowConnections))
				{
					GameMap.instance.setupDebugRenderLayerMap(force: true);
					if (GameMap.instance.DebugLayerRendering < 0)
					{
						GameMap.instance.setDebugRendering(33, force: true);
					}
					else
					{
						GameMap.instance.setDebugRendering(-1, force: true);
					}
				}
			}
		}
		if (!IsActionPressed(Enums.KeyFunctions.OptionsMenu) || !(ignoreNextEscape < DateTime.UtcNow) || Director.instance.inPostCallbackPeriod)
		{
			return;
		}
		if (Director.instance.SimRunning)
		{
			if (GameData.scenario.InGameoverSituation || MainViewModel.Instance.EditorWarningPanelVisible)
			{
				return;
			}
			if (MainViewModel.Instance.Show_HUD_FreebuildMenu)
			{
				HUD_FreebuildMenu.ToggleMenu();
			}
			else if (MainViewModel.Instance.Show_HUD_ControlGroups)
			{
				HUD_ControlGroups.ToggleMenu();
			}
			else if (MainViewModel.Instance.Show_HUD_Confirmation)
			{
				MainViewModel.Instance.HUDConfirmationPopup.ConfirmationClicked(2);
			}
			else if (MainViewModel.Instance.Show_HUD_LoadSaveRequester || MainViewModel.Instance.Show_HUD_LoadSaveRequesterMP || MainViewModel.Instance.Show_HUD_LoadSaveRequesterTrails)
			{
				MainViewModel.Instance.HUDLoadSaveRequester.CloseRequester();
			}
			else if (MainViewModel.Instance.MPChatVisible)
			{
				MainViewModel.Instance.MPChatVisible = false;
			}
			else if (MainViewModel.Instance.AlliesPanelVisible)
			{
				MainViewModel.Instance.AlliesPanelVisible = false;
			}
			else if (MainViewModel.Instance.MeritPanelVisible)
			{
				MainViewModel.Instance.MeritPanelVisible = false;
			}
			else if (MainViewModel.Instance.Show_HUD_Briefing)
			{
				MainViewModel.Instance.ButtonBriefingResume(0, fromEscape: true);
			}
			else if (MainViewModel.Instance.Show_HUD_Help)
			{
				MainViewModel.Instance.HUDHelp.Close();
			}
			else if (MainViewModel.Instance.Show_HUD_Options || MainViewModel.Instance.Show_HUD_OptionsMP)
			{
				if ((int)((UIElement)MainViewModel.Instance.HUDOptions.RefOptionsHotKeyPanel).Visibility == 2)
				{
					MainViewModel.Instance.HUDOptions.ButtonClicked(-101);
				}
				else
				{
					MainViewModel.Instance.HUDOptions.ButtonClicked(-1);
				}
			}
			else if (MainViewModel.Instance.ShowingScenario)
			{
				if (MainViewModel.Instance.Show_HUD_IngameMenu)
				{
					MainViewModel.Instance.HUDmain.InGameOptions(null, null);
				}
				else
				{
					MainViewModel.Instance.HUDScenario.StartExitAnim();
				}
			}
			else if (MainControls.instance.CurrentAction == 5 || MainControls.instance.CurrentAction == 3 || MainControls.instance.CurrentAction == 6 || MainControls.instance.CurrentAction == 7)
			{
				MainControls.instance.CurrentAction = 0;
				EditorDirector.instance.EscapeCloseUIPanel();
			}
			else if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.app_mode == 16 || (GameData.Instance.lastGameState.app_mode == 14 && (GameData.Instance.lastGameState.app_sub_mode == 61 || GameData.Instance.lastGameState.app_sub_mode == 62))))
			{
				EditorDirector.instance.EscapeCloseUIPanel();
			}
			else if (MainViewModel.Instance.HUD_Markers_Vis)
			{
				MainViewModel.Instance.HUD_Markers_Vis = false;
			}
			else if (MainViewModel.Instance.Show_HUD_WorkshopUploader)
			{
				if (HUD_WorkshopUploader.CanClose)
				{
					MainViewModel.Instance.Show_HUD_WorkshopUploader = false;
					MainViewModel.Instance.Show_HUD_IngameMenu = true;
				}
			}
			else
			{
				MainViewModel.Instance.HUDmain.InGameOptions(null, null);
			}
		}
		else if (MainViewModel.Instance.Show_HUD_Confirmation || MainViewModel.Instance.Show_HUD_ConfirmationSands || MainViewModel.Instance.Show_HUD_ConfirmationMP)
		{
			MainViewModel.Instance.HUDConfirmationPopup.ConfirmationClicked(2);
		}
		else if (MainViewModel.Instance.Show_Leaderboard)
		{
			HUD_Leaderboard.CloseLeaderboard();
		}
		else if (MainViewModel.Instance.Show_HUD_Options || MainViewModel.Instance.Show_HUD_OptionsMP)
		{
			if ((int)MainViewModel.Instance.OptionsNewsletterVis == 2)
			{
				MainViewModel.Instance.HUDOptions.ButtonClicked(-20001);
			}
			else if ((int)((UIElement)MainViewModel.Instance.HUDOptions.RefOptionsHotKeyPanel).Visibility == 2)
			{
				MainViewModel.Instance.HUDOptions.ButtonClicked(-101);
			}
			else
			{
				MainViewModel.Instance.HUDOptions.ButtonClicked(-1);
			}
		}
		else if (MainViewModel.Instance.Show_HUD_LoadSaveRequester || MainViewModel.Instance.Show_HUD_LoadSaveRequesterMP || MainViewModel.Instance.Show_HUD_LoadSaveRequesterTrails)
		{
			MainViewModel.Instance.HUDLoadSaveRequester.CloseRequester();
		}
		else if (MainViewModel.Instance.Show_MapEditor)
		{
			if (FRONT_EditorSetup.canCloseWorkshop)
			{
				if (MainViewModel.Instance.Show_EditorWorkshop_Uploader)
				{
					MainViewModel.Instance.FRONTEditorSetup.ButtonClicked("CloseDoUpload");
				}
				else if (MainViewModel.Instance.Show_EditorWorkshop_Requester)
				{
					MainViewModel.Instance.FRONTEditorSetup.ButtonClicked("CloseUpload");
				}
				else
				{
					MainViewModel.Instance.FrontEndMenu.ButtonClicked("BackMain");
				}
			}
		}
		else if (MainViewModel.Instance.Show_HUD_Help)
		{
			MainViewModel.Instance.HUDHelp.Close();
		}
		else if (MainViewModel.Instance.Show_StandaloneSetup)
		{
			MainViewModel.Instance.FRONTStandaloneMission.ButtonClicked("Back");
		}
		else if (MainViewModel.Instance.Show_HUD_MissionOver)
		{
			MainViewModel.Instance.HUDMissionOver.ButtonClicked("Exit");
		}
		else if (MainViewModel.Instance.Show_SkirmishMasters)
		{
			MainViewModel.Instance.Show_SkirmishMasters = false;
		}
		else if (MainViewModel.Instance.Show_MultiplayerSetup)
		{
			MainViewModel.Instance.FRONTMultiplayer.ButtonClicked("Back");
		}
		else if (MainViewModel.Instance.Show_HUD_CustomTrails)
		{
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("Skirmish");
		}
		else if (MainViewModel.Instance.Show_Frontend_Roadmap)
		{
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("BackMain");
		}
		else if (MainViewModel.Instance.Show_Story)
		{
			MainViewModel.Instance.FRONTStory.EscapePressed();
		}
		else if (MainViewModel.Instance.Show_Credits)
		{
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("BackMain");
		}
		else if (MainViewModel.Instance.Show_ClassicTrailsOptions)
		{
			MainViewModel.Instance.Show_ClassicTrailsOptions = false;
		}
		else if (MainViewModel.Instance.Show_Sands_Intro_Text)
		{
			MainViewModel.Instance.Show_Sands_Intro_Text = false;
		}
		else if (MainViewModel.Instance.Show_Historical1CampaignMenu || MainViewModel.Instance.Show_Historical2CampaignMenu || MainViewModel.Instance.Show_Historical3CampaignMenu || MainViewModel.Instance.Show_Historical4CampaignMenu || MainViewModel.Instance.Show_Historical5CampaignMenu || MainViewModel.Instance.Show_Historical6CampaignMenu || MainViewModel.Instance.Show_Historical7CampaignMenu)
		{
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("Historical");
		}
		else if (MainViewModel.Instance.Show_Frontend_Coop)
		{
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("Skirmish");
		}
		else if (MainViewModel.Instance.Show_TrailCampaignMenu || MainViewModel.Instance.Show_Trail2CampaignMenu || MainViewModel.Instance.Show_Trail3CampaignMenu)
		{
			if (FrontendMenus.CurrentSelectedTrail >= 90)
			{
				MainViewModel.Instance.FrontEndMenu.ButtonClicked("CustomTrails");
			}
			else
			{
				MainViewModel.Instance.FrontEndMenu.ButtonClicked("Trails");
			}
		}
		else if (MainViewModel.Instance.Show_SandsTrail1Menu || MainViewModel.Instance.Show_SandsTrail2Menu || MainViewModel.Instance.Show_SandsTrail3Menu || MainViewModel.Instance.Show_SandsTrail4Menu || MainViewModel.Instance.Show_SandsTrail5Menu || MainViewModel.Instance.Show_SandsTrail6Menu || MainViewModel.Instance.Show_SandsTrail7Menu)
		{
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands");
		}
		else if (MainViewModel.Instance.Show_Frontend_Historical)
		{
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("BackMain");
		}
		else if (MainViewModel.Instance.Show_Frontend_Skirmish)
		{
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("BackMain");
		}
		else if (MainViewModel.Instance.Show_Frontend_Skirmish_Trails || MainViewModel.Instance.Show_Frontend_Skirmish_Sands)
		{
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("Skirmish");
		}
		else if (MainViewModel.Instance.Show_MultiplayerSetup)
		{
			MainViewModel.Instance.FRONTMultiplayer.ButtonClicked("Back");
		}
		else if (MainViewModel.Instance.Show_IntroSequence)
		{
			if (!MainViewModel.Instance.EnterYourNameVis)
			{
				MainViewModel.Instance.Intro_Sequence.ButtonClicked(fromClick: false);
			}
		}
		else if (MainViewModel.Instance.Show_Frontend_OtherModes || MainViewModel.Instance.Show_Frontend_Eco)
		{
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("BackMain");
		}
	}

	public void ignoreEscape()
	{
		ignoreNextEscape = DateTime.UtcNow.AddMilliseconds(200.0);
	}

	public bool isShiftDown()
	{
		if (keys[leftShiftMap] == 1 || keys[leftShiftMap] == 2)
		{
			return true;
		}
		if (keys[rightShiftMap] == 1 || keys[rightShiftMap] == 2)
		{
			return true;
		}
		return false;
	}

	public bool isCtrlDown()
	{
		if (keys[leftCtrlMap] == 1 || keys[leftCtrlMap] == 2)
		{
			return true;
		}
		if (keys[rightCtrlMap] == 1 || keys[rightCtrlMap] == 2)
		{
			return true;
		}
		return false;
	}

	public bool isAltDown()
	{
		if (keys[altMap] == 1 || keys[altMap] == 2)
		{
			return true;
		}
		if (keys[altGrMap] == 1 || keys[altGrMap] == 2)
		{
			return true;
		}
		return false;
	}

	public bool IsKeyPressed(KeyCode code, bool ignoreModifiers = false)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected I4, but got Unknown
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Invalid comparison between Unknown and I4
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Invalid comparison between Unknown and I4
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Invalid comparison between Unknown and I4
		int num = code & 0xFFFF;
		if (num >= 0 && num < keyCodeMap.Length)
		{
			int num2 = keyCodeMap[num];
			if (num2 >= 0 && keys[num2] == 1)
			{
				if (!ignoreModifiers)
				{
					bool num3 = (code & 0x10000) > 0;
					bool flag = (code & 0x20000) > 0;
					bool flag2 = (code & 0x40000) > 0;
					if (num3 != isShiftDown())
					{
						return false;
					}
					if (flag != isCtrlDown())
					{
						return false;
					}
					if (flag2 != isAltDown())
					{
						return false;
					}
				}
				return true;
			}
		}
		return false;
	}

	public bool IsKeyHeldDown(KeyCode code, bool ignoreModifiers = false)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected I4, but got Unknown
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Invalid comparison between Unknown and I4
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Invalid comparison between Unknown and I4
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Invalid comparison between Unknown and I4
		int num = code & 0xFFFF;
		if (num >= 0 && num < keyCodeMap.Length)
		{
			int num2 = keyCodeMap[num];
			if (num2 >= 0 && (keys[num2] == 1 || keys[num2] == 2))
			{
				if (!ignoreModifiers)
				{
					bool num3 = (code & 0x10000) > 0;
					bool flag = (code & 0x20000) > 0;
					bool flag2 = (code & 0x40000) > 0;
					if (num3 != isShiftDown())
					{
						return false;
					}
					if (flag != isCtrlDown())
					{
						return false;
					}
					if (flag2 != isAltDown())
					{
						return false;
					}
				}
				return true;
			}
		}
		return false;
	}

	public bool IsKeyReleased(KeyCode code)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected I4, but got Unknown
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Invalid comparison between Unknown and I4
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Invalid comparison between Unknown and I4
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Invalid comparison between Unknown and I4
		int num = code & 0xFFFF;
		if (num >= 0 && num < keyCodeMap.Length)
		{
			int num2 = keyCodeMap[num];
			if (num2 >= 0 && keys[num2] == 3)
			{
				bool num3 = (code & 0x10000) > 0;
				bool flag = (code & 0x20000) > 0;
				bool flag2 = (code & 0x40000) > 0;
				if (num3 != isShiftDown())
				{
					return false;
				}
				if (flag != isCtrlDown())
				{
					return false;
				}
				if (flag2 != isAltDown())
				{
					return false;
				}
				return true;
			}
		}
		return false;
	}

	public void LoadFromString(string settingsString)
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Expected I4, but got Unknown
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Expected I4, but got Unknown
		string[] array = settingsString.Split("||KEYS||\n", StringSplitOptions.None);
		if (array.Length != 3)
		{
			return;
		}
		string[] array2 = array[1].Split("\n", StringSplitOptions.None);
		foreach (string text in array2)
		{
			try
			{
				string[] array3 = text.Split(":", StringSplitOptions.None);
				if (array3.Length > 1 && array3[1].Length > 0)
				{
					Enums.KeyFunctions keyFunctions = (Enums.KeyFunctions)Enum.Parse(typeof(Enums.KeyFunctions), array3[0]);
					string[] array4 = array3[1].Split(",", StringSplitOptions.None);
					int num = (int)(KeyCode)Enum.Parse(typeof(KeyCode), array4[0]);
					int num2 = -1;
					for (int j = 1; j < array4.Length; j++)
					{
						switch (array4[j].ToLowerInvariant())
						{
						case "shift":
							num |= 0x10000;
							break;
						case "ctrl":
							num |= 0x20000;
							break;
						case "alt":
							num |= 0x40000;
							break;
						case "mp":
							num |= 0x80000;
							break;
						}
					}
					if (array3.Length > 2 && array3[2].Length > 0)
					{
						string[] array5 = array3[2].Split(",", StringSplitOptions.None);
						num2 = (int)(KeyCode)Enum.Parse(typeof(KeyCode), array5[0]);
						for (int k = 1; k < array5.Length; k++)
						{
							switch (array5[k].ToLowerInvariant())
							{
							case "shift":
								num2 |= 0x10000;
								break;
							case "ctrl":
								num2 |= 0x20000;
								break;
							case "alt":
								num2 |= 0x40000;
								break;
							}
						}
					}
					if (num >= 0)
					{
						for (int l = 0; l < 203; l++)
						{
							if (functionMap[l, 0] == num)
							{
								functionMap[l, 0] = -1;
							}
							if (functionMap[l, 1] == num)
							{
								functionMap[l, 1] = -1;
							}
						}
					}
					if (num2 >= 0)
					{
						for (int m = 0; m < 203; m++)
						{
							if (functionMap[m, 0] == num2)
							{
								functionMap[m, 0] = -1;
							}
							if (functionMap[m, 1] == num2)
							{
								functionMap[m, 1] = -1;
							}
						}
					}
					functionMap[(int)keyFunctions, 0] = num;
					functionMap[(int)keyFunctions, 1] = num2;
				}
				else
				{
					Enums.KeyFunctions keyFunctions2 = (Enums.KeyFunctions)Enum.Parse(typeof(Enums.KeyFunctions), array3[0]);
					int num3 = -1;
					int num4 = -1;
					functionMap[(int)keyFunctions2, 0] = num3;
					functionMap[(int)keyFunctions2, 1] = num4;
				}
			}
			catch (Exception)
			{
			}
		}
		Dictionary<int, bool> dictionary = new Dictionary<int, bool>();
		for (int n = 0; n < 203; n++)
		{
			if (functionMap[n, 0] >= 0)
			{
				if (dictionary.ContainsKey(functionMap[n, 0]))
				{
					functionMap[n, 0] = -1;
				}
				else
				{
					dictionary[functionMap[n, 0]] = true;
				}
			}
			if (functionMap[n, 1] >= 0)
			{
				if (dictionary.ContainsKey(functionMap[n, 1]))
				{
					functionMap[n, 1] = -1;
				}
				else
				{
					dictionary[functionMap[n, 1]] = true;
				}
			}
		}
		if (functionMap[16, 0] == -1 && !dictionary[114])
		{
			functionMap[16, 0] = 114;
		}
		if (functionMap[17, 0] == -1 && !dictionary[116])
		{
			functionMap[16, 0] = 116;
		}
		if (functionMap[18, 0] == -1 && !dictionary[121])
		{
			functionMap[16, 0] = 121;
		}
		if (functionMap[83, 0] == 524581)
		{
			functionMap[72, 0] = 524570;
			functionMap[73, 0] = 524571;
			functionMap[74, 0] = 524572;
			functionMap[75, 0] = 524573;
			functionMap[76, 0] = 524574;
			functionMap[77, 0] = 524575;
			functionMap[78, 0] = 524576;
			functionMap[79, 0] = 524577;
			functionMap[80, 0] = 524578;
			functionMap[81, 0] = 524579;
			functionMap[82, 0] = 655642;
			functionMap[83, 0] = 655643;
			functionMap[109, 0] = 655644;
			functionMap[110, 0] = 655645;
			functionMap[111, 0] = 655646;
			functionMap[112, 0] = 655647;
			functionMap[113, 0] = 655648;
			functionMap[114, 0] = 655649;
			functionMap[115, 0] = 655650;
			functionMap[116, 0] = 655651;
		}
	}

	public string SaveToString()
	{
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		string text = "||KEYS||\n";
		for (int i = 0; i < 203; i++)
		{
			if (functionMap[i, 0] != -1 || functionMap[i, 1] != -1)
			{
				Enums.KeyFunctions keyFunctions = (Enums.KeyFunctions)i;
				string text2 = keyFunctions.ToString();
				text = text + text2 + ":";
				if (functionMap[i, 0] != -1)
				{
					int num = functionMap[i, 0];
					int num2 = num & 0xFFFF;
					bool flag = (num & 0x10000) > 0;
					bool flag2 = (num & 0x20000) > 0;
					bool flag3 = (num & 0x40000) > 0;
					bool num3 = (num & 0x80000) > 0;
					string text3 = ((object)(KeyCode)num2/*cast due to constrained. prefix*/).ToString();
					text += text3;
					if (flag)
					{
						text += ",shift";
					}
					if (flag2)
					{
						text += ",ctrl";
					}
					if (flag3)
					{
						text += ",alt";
					}
					if (num3)
					{
						text += ",mp";
					}
					text += ":";
				}
				if (functionMap[i, 1] != -1)
				{
					int num4 = functionMap[i, 1];
					int num5 = num4 & 0xFFFF;
					bool flag4 = (num4 & 0x10000) > 0;
					bool flag5 = (num4 & 0x20000) > 0;
					bool num6 = (num4 & 0x40000) > 0;
					string text4 = ((object)(KeyCode)num5/*cast due to constrained. prefix*/).ToString();
					text += text4;
					if (flag4)
					{
						text += ",shift";
					}
					if (flag5)
					{
						text += ",ctrl";
					}
					if (num6)
					{
						text += ",alt";
					}
					text += ":";
				}
				text += "\n";
			}
			else
			{
				Enums.KeyFunctions keyFunctions = (Enums.KeyFunctions)i;
				string text5 = keyFunctions.ToString();
				text = text + text5 + ":\n";
			}
		}
		return text + "||KEYS||\n";
	}

	public bool doesActionKeyFunctionExist(Enums.KeyFunctions function)
	{
		if (functionMap[(int)function, 0] >= 0)
		{
			return true;
		}
		if (functionMap[(int)function, 1] >= 0)
		{
			return true;
		}
		return false;
	}

	public bool IsActionPressed(Enums.KeyFunctions function)
	{
		bool ignoreModifiers = false;
		int num = functionMap[(int)function, 0];
		if (num >= 0 && IsKeyPressed((KeyCode)num, ignoreModifiers))
		{
			return true;
		}
		int num2 = functionMap[(int)function, 1];
		if (num2 >= 0 && IsKeyPressed((KeyCode)num2, ignoreModifiers))
		{
			return true;
		}
		return false;
	}

	public bool IsActionHeldDown(Enums.KeyFunctions function, bool ignoreModifiers = false)
	{
		int num = functionMap[(int)function, 0];
		if (num >= 0 && IsKeyHeldDown((KeyCode)num, ignoreModifiers))
		{
			return true;
		}
		int num2 = functionMap[(int)function, 1];
		if (num2 >= 0 && IsKeyHeldDown((KeyCode)num2, ignoreModifiers))
		{
			return true;
		}
		return false;
	}

	public bool IsActionRelease(Enums.KeyFunctions function)
	{
		int num = functionMap[(int)function, 0];
		if (num >= 0 && IsKeyReleased((KeyCode)num))
		{
			return true;
		}
		int num2 = functionMap[(int)function, 1];
		if (num2 >= 0 && IsKeyReleased((KeyCode)num2))
		{
			return true;
		}
		return false;
	}

	public float HorizontalAxis()
	{
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		float radarHeldX = RadarHeldX;
		RadarHeldX = 0f;
		if (!Director.instance.SimRunning && CameraControls2D.instance.isMapLocked())
		{
			return 0f;
		}
		if (MainViewModel.Instance.Show_HUD_LoadSaveRequester)
		{
			return 0f;
		}
		if (Director.instance.Paused)
		{
			return 0f;
		}
		if (isCtrlDown() || isAltDown())
		{
			return 0f;
		}
		bool flag = IsActionHeldDown(Enums.KeyFunctions.Left, ignoreModifiers: true);
		bool flag2 = IsActionHeldDown(Enums.KeyFunctions.Right, ignoreModifiers: true);
		float num = ConfigSettings.GetScrollSpeed();
		if (isShiftDown())
		{
			num *= 2f;
		}
		if (flag2 && flag)
		{
			return 0f;
		}
		if (flag)
		{
			return -1f * num;
		}
		if (flag2)
		{
			return 1f * num;
		}
		if (ConfigSettings.Settings_PushMapScrolling)
		{
			if (Input.mousePosition.x <= 0f)
			{
				return -1f * num;
			}
			if (Input.mousePosition.x >= (float)(Screen.width - 1))
			{
				return 1f * num;
			}
		}
		return radarHeldX;
	}

	public float VerticalAxis()
	{
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		float radarHeldY = RadarHeldY;
		RadarHeldY = 0f;
		if (!Director.instance.SimRunning && CameraControls2D.instance.isMapLocked())
		{
			return 0f;
		}
		if (MainViewModel.Instance.Show_HUD_LoadSaveRequester)
		{
			return 0f;
		}
		if (Director.instance.Paused)
		{
			return 0f;
		}
		if (isCtrlDown() || isAltDown())
		{
			return 0f;
		}
		bool flag = IsActionHeldDown(Enums.KeyFunctions.Up, ignoreModifiers: true);
		bool flag2 = IsActionHeldDown(Enums.KeyFunctions.Down, ignoreModifiers: true);
		float num = ConfigSettings.GetScrollSpeed();
		if (isShiftDown())
		{
			num *= 2f;
		}
		if (flag && flag2)
		{
			return 0f;
		}
		if (flag)
		{
			return 1f * num;
		}
		if (flag2)
		{
			return -1f * num;
		}
		if (ConfigSettings.Settings_PushMapScrolling)
		{
			if (Input.mousePosition.y <= 0f)
			{
				return -1f * num;
			}
			if (Input.mousePosition.y >= (float)(Screen.height - 1))
			{
				return 1f * num;
			}
		}
		return radarHeldY;
	}

	public KeyCode GetKeyCode(Enums.KeyFunctions function, int keyID)
	{
		if (functionMap[(int)function, keyID] > 0)
		{
			return (KeyCode)functionMap[(int)function, keyID];
		}
		return (KeyCode)0;
	}

	public Enums.KeyFunctions GetHotKeyFunction()
	{
		if (hotKeyCurrentKeyPressed > 0)
		{
			for (int i = 0; i < 203; i++)
			{
				if (functionMap[i, 0] == hotKeyCurrentKey || functionMap[i, 1] == hotKeyCurrentKey)
				{
					return (Enums.KeyFunctions)i;
				}
			}
		}
		return Enums.KeyFunctions.NumActions;
	}

	public void SetNewKey(Enums.KeyFunctions func, int newKey, int column)
	{
		if (newKey > 0)
		{
			for (int i = 0; i < 203; i++)
			{
				if (functionMap[i, 0] == newKey)
				{
					functionMap[i, 0] = -1;
				}
				if (functionMap[i, 1] == newKey)
				{
					functionMap[i, 1] = -1;
				}
			}
		}
		functionMap[(int)func, column] = newKey;
	}
}
