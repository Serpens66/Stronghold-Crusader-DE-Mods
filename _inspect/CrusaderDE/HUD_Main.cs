using System;
using Noesis;

namespace CrusaderDE;

public class HUD_Main : UserControl
{
	public int scrollPosition;

	public DateTime lastBuildingRollover = DateTime.MinValue;

	public Button[] buildButtons = (Button[])(object)new Button[588];

	public RadioButton[] sheildButtons = (RadioButton[])(object)new RadioButton[8];

	public Button[] MEBrushSizeButtons = (Button[])(object)new Button[9];

	public Button[] MERulerButtons = (Button[])(object)new Button[3];

	public Rectangle RefGuideRect;

	public StackPanel RefMapEditorModeToggle;

	public Grid RefLayoutRoot;

	public Grid RefMapEditorSheilds;

	public Grid RefFrameBuildings;

	public Grid RefFrameTerrain;

	public Grid RefBuildMenuGrid;

	public Grid RefMEMenuGrid;

	public StackPanel RefBottomTabs1;

	public StackPanel RefSideTabs;

	public RadioButton RefBottomTabs2a;

	public RadioButton RefBottomTabs2b;

	public RadioButton RefBottomTabs2c;

	public RadioButton RefBottomTabs2d;

	public RadioButton RefBottomTabs2e;

	public Button RefGameInfoButton;

	public Button RefGameUndoButton;

	public RadioButton RefButtonBuildModeBuildings;

	public RadioButton RefButtonBuildModeTerrain;

	public RadioButton RefButtonMEHeightControls;

	public RadioButton RefButtonMERocksSignpost;

	public Grid RefMETerrainMenu;

	public Grid RefMEAnimalsMenu;

	public Grid RefMETextureMenu;

	public Grid RefMEWaterMenu;

	public Grid RefMEVegetationMenu;

	public Grid RefMERocksMenu;

	public RadioButton RefTabBuildCastle;

	public RadioButton RefTabBuildIndustry;

	public RadioButton RefTabBuildFarms;

	public RadioButton RefTabBuildTown;

	public RadioButton RefTabBuildWeapons;

	public RadioButton RefTabBuildFood;

	public Image RefTutorialArrow1;

	public Image RefTutorialArrow2;

	public Image RefTutorialArrow3;

	public Image RefTutorialArrow4;

	public Image RefTutorialArrow5;

	public Image RefTutorialArrow6;

	public Image RefTutorialArrow7;

	public Image RefTutorialArrow8;

	public Image RefTutorialArrow9;

	public Image RefTutorialArrow10;

	public Image RefTutorialArrow11;

	public Image RefTutorialArrow12;

	public Image RefTutorialArrow13;

	public Image RefTutorialArrow14;

	public Image RefTutorialArrow15;

	public Image RefTutorialArrow16;

	public Image RefTutorialArrow17;

	public Image RefTutorialArrow18;

	public Image RefTutorialArrow19;

	public Image RefTutorialArrow20;

	public Image RefTutorialArrow21;

	public int[,] BuildIconLists = new int[23, 17]
	{
		{
			380, 81, 82, 83, 84, 85, 582, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			51, 53, 50, 52, 8, 7, 5, 60, 70, 68,
			80, 543, 0, 0, 0, 0, 0
		},
		{
			3, 2, 24, 25, 26, 28, 30, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			22, 21, 19, 18, 20, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			27, 87, 88, 89, 34, 415, 67, 78, 542, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			10, 11, 12, 13, 14, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			4, 15, 23, 16, 33, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			381, 61, 62, 63, 64, 65, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			382, 73, 76, 74, 408, 54, 56, 55, 75, 77,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			383, 35, 47, 48, 31, 36, 46, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			384, 40, 400, 44, 86, 401, 403, 404, 402, 406,
			183, 0, 0, 0, 0, 0, 0
		},
		{
			385, 41, 412, 176, 180, 189, 417, 410, 170, 171,
			172, 199, 0, 0, 0, 0, 0
		},
		{
			534, 535, 536, 537, 538, 539, 540, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			387, 389, 390, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			388, 391, 392, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			500, 501, 504, 505, 508, 509, 510, 511, 557, 555,
			578, 0, 0, 0, 0, 0, 0
		},
		{
			480, 481, 482, 483, 484, 485, 486, 487, 488, 496,
			489, 495, 0, 0, 0, 0, 0
		},
		{
			502, 503, 506, 507, 512, 552, 553, 554, 558, 556,
			579, 0, 0, 0, 0, 0, 0
		},
		{
			559, 560, 561, 562, 563, 564, 565, 566, 567, 568,
			569, 570, 580, 0, 0, 0, 0
		},
		{
			490, 491, 492, 493, 494, 541, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			544, 545, 546, 547, 548, 549, 550, 551, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			571, 572, 573, 574, 575, 576, 577, 581, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		},
		{
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		}
	};

	public int[] MEsheildIconLists = new int[8] { 440, 441, 442, 443, 444, 445, 446, 447 };

	public string HoverString = "";

	public int HoverStruct;

	public string SelectedString = "";

	public int SelectedStruct;

	public string OtherString = "";

	public string OtherString2 = "";

	public int OtherData1 = -1;

	public int OtherData2 = -1;

	public bool OtherHighestPri;

	public bool OtherVisible;

	public int lastPTBgroup = -1;

	public int lastPTBtext = -1;

	public int lastBuildingTooltipType = -1;

	public int currentTutorialArrow = -1;

	public DateTime tutArrowFrameTime = DateTime.MinValue;

	public int tutArrowFrame;

	public HUD_Main()
	{
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Expected O, but got Unknown
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Expected O, but got Unknown
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Expected O, but got Unknown
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Expected O, but got Unknown
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Expected O, but got Unknown
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Expected O, but got Unknown
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Expected O, but got Unknown
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Expected O, but got Unknown
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Expected O, but got Unknown
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Expected O, but got Unknown
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Expected O, but got Unknown
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Expected O, but got Unknown
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Expected O, but got Unknown
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Expected O, but got Unknown
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_027f: Expected O, but got Unknown
		//IL_0292: Unknown result type (might be due to invalid IL or missing references)
		//IL_0298: Expected O, but got Unknown
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Expected O, but got Unknown
		//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ca: Expected O, but got Unknown
		//IL_02dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Expected O, but got Unknown
		//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fa: Expected O, but got Unknown
		//IL_030d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0313: Expected O, but got Unknown
		//IL_0326: Unknown result type (might be due to invalid IL or missing references)
		//IL_032c: Expected O, but got Unknown
		//IL_033f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0345: Expected O, but got Unknown
		//IL_0358: Unknown result type (might be due to invalid IL or missing references)
		//IL_035e: Expected O, but got Unknown
		//IL_0371: Unknown result type (might be due to invalid IL or missing references)
		//IL_0377: Expected O, but got Unknown
		//IL_038a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0390: Expected O, but got Unknown
		//IL_03a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a9: Expected O, but got Unknown
		//IL_03bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c2: Expected O, but got Unknown
		//IL_03d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03db: Expected O, but got Unknown
		//IL_03ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f4: Expected O, but got Unknown
		//IL_0407: Unknown result type (might be due to invalid IL or missing references)
		//IL_040d: Expected O, but got Unknown
		//IL_0420: Unknown result type (might be due to invalid IL or missing references)
		//IL_0426: Expected O, but got Unknown
		//IL_0439: Unknown result type (might be due to invalid IL or missing references)
		//IL_043f: Expected O, but got Unknown
		//IL_0452: Unknown result type (might be due to invalid IL or missing references)
		//IL_0458: Expected O, but got Unknown
		//IL_046e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0474: Expected O, but got Unknown
		//IL_048a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0490: Expected O, but got Unknown
		//IL_04a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ac: Expected O, but got Unknown
		//IL_04bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c5: Expected O, but got Unknown
		//IL_04db: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e1: Expected O, but got Unknown
		//IL_04f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04fd: Expected O, but got Unknown
		//IL_0510: Unknown result type (might be due to invalid IL or missing references)
		//IL_0516: Expected O, but got Unknown
		//IL_0529: Unknown result type (might be due to invalid IL or missing references)
		//IL_052f: Expected O, but got Unknown
		//IL_0542: Unknown result type (might be due to invalid IL or missing references)
		//IL_0548: Expected O, but got Unknown
		//IL_055b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0561: Expected O, but got Unknown
		//IL_0574: Unknown result type (might be due to invalid IL or missing references)
		//IL_057a: Expected O, but got Unknown
		//IL_058d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0593: Expected O, but got Unknown
		//IL_05a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ac: Expected O, but got Unknown
		//IL_05be: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c4: Expected O, but got Unknown
		//IL_05d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_05dd: Expected O, but got Unknown
		//IL_05f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f6: Expected O, but got Unknown
		//IL_0609: Unknown result type (might be due to invalid IL or missing references)
		//IL_060f: Expected O, but got Unknown
		//IL_0622: Unknown result type (might be due to invalid IL or missing references)
		//IL_0628: Expected O, but got Unknown
		//IL_063e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0644: Expected O, but got Unknown
		//IL_0657: Unknown result type (might be due to invalid IL or missing references)
		//IL_065d: Expected O, but got Unknown
		//IL_0670: Unknown result type (might be due to invalid IL or missing references)
		//IL_0676: Expected O, but got Unknown
		//IL_0689: Unknown result type (might be due to invalid IL or missing references)
		//IL_068f: Expected O, but got Unknown
		//IL_06a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a8: Expected O, but got Unknown
		//IL_06bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c1: Expected O, but got Unknown
		//IL_06d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_06dd: Expected O, but got Unknown
		//IL_06f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f6: Expected O, but got Unknown
		//IL_0709: Unknown result type (might be due to invalid IL or missing references)
		//IL_070f: Expected O, but got Unknown
		//IL_0722: Unknown result type (might be due to invalid IL or missing references)
		//IL_0728: Expected O, but got Unknown
		//IL_073e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0744: Expected O, but got Unknown
		//IL_0757: Unknown result type (might be due to invalid IL or missing references)
		//IL_075d: Expected O, but got Unknown
		//IL_0770: Unknown result type (might be due to invalid IL or missing references)
		//IL_0776: Expected O, but got Unknown
		//IL_0789: Unknown result type (might be due to invalid IL or missing references)
		//IL_078f: Expected O, but got Unknown
		//IL_07a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_07a8: Expected O, but got Unknown
		//IL_07bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_07c1: Expected O, but got Unknown
		//IL_07d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_07dd: Expected O, but got Unknown
		//IL_07f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_07f6: Expected O, but got Unknown
		//IL_0809: Unknown result type (might be due to invalid IL or missing references)
		//IL_080f: Expected O, but got Unknown
		//IL_0822: Unknown result type (might be due to invalid IL or missing references)
		//IL_0828: Expected O, but got Unknown
		//IL_083b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0841: Expected O, but got Unknown
		//IL_0854: Unknown result type (might be due to invalid IL or missing references)
		//IL_085a: Expected O, but got Unknown
		//IL_086d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0873: Expected O, but got Unknown
		//IL_0889: Unknown result type (might be due to invalid IL or missing references)
		//IL_088f: Expected O, but got Unknown
		//IL_08a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_08a8: Expected O, but got Unknown
		//IL_08be: Unknown result type (might be due to invalid IL or missing references)
		//IL_08c4: Expected O, but got Unknown
		//IL_08d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_08dd: Expected O, but got Unknown
		//IL_08f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_08f6: Expected O, but got Unknown
		//IL_090c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0912: Expected O, but got Unknown
		//IL_0928: Unknown result type (might be due to invalid IL or missing references)
		//IL_092e: Expected O, but got Unknown
		//IL_0944: Unknown result type (might be due to invalid IL or missing references)
		//IL_094a: Expected O, but got Unknown
		//IL_0960: Unknown result type (might be due to invalid IL or missing references)
		//IL_0966: Expected O, but got Unknown
		//IL_097c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0982: Expected O, but got Unknown
		//IL_0998: Unknown result type (might be due to invalid IL or missing references)
		//IL_099e: Expected O, but got Unknown
		//IL_09b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_09ba: Expected O, but got Unknown
		//IL_09cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_09d3: Expected O, but got Unknown
		//IL_09e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_09ef: Expected O, but got Unknown
		//IL_0a05: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a0b: Expected O, but got Unknown
		//IL_0a21: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a27: Expected O, but got Unknown
		//IL_0a3d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a43: Expected O, but got Unknown
		//IL_0a59: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a5f: Expected O, but got Unknown
		//IL_0a75: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a7b: Expected O, but got Unknown
		//IL_0a91: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a97: Expected O, but got Unknown
		//IL_0aad: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ab3: Expected O, but got Unknown
		//IL_0ac9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0acf: Expected O, but got Unknown
		//IL_0ae5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0aeb: Expected O, but got Unknown
		//IL_0b01: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b07: Expected O, but got Unknown
		//IL_0b1d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b23: Expected O, but got Unknown
		//IL_0b39: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b3f: Expected O, but got Unknown
		//IL_0b55: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b5b: Expected O, but got Unknown
		//IL_0b71: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b77: Expected O, but got Unknown
		//IL_0b8d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b93: Expected O, but got Unknown
		//IL_0ba9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0baf: Expected O, but got Unknown
		//IL_0bc5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bcb: Expected O, but got Unknown
		//IL_0be1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0be7: Expected O, but got Unknown
		//IL_0bfd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c03: Expected O, but got Unknown
		//IL_0c19: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c1f: Expected O, but got Unknown
		//IL_0c35: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c3b: Expected O, but got Unknown
		//IL_0c51: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c57: Expected O, but got Unknown
		//IL_0c6d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c73: Expected O, but got Unknown
		//IL_0c89: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c8f: Expected O, but got Unknown
		//IL_0ca5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cab: Expected O, but got Unknown
		//IL_0cc1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cc7: Expected O, but got Unknown
		//IL_0cdd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ce3: Expected O, but got Unknown
		//IL_0cf9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cff: Expected O, but got Unknown
		//IL_0d15: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d1b: Expected O, but got Unknown
		//IL_0d31: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d37: Expected O, but got Unknown
		//IL_0d4d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d53: Expected O, but got Unknown
		//IL_0d69: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d6f: Expected O, but got Unknown
		//IL_0d85: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d8b: Expected O, but got Unknown
		//IL_0da1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0da7: Expected O, but got Unknown
		//IL_0dbd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dc3: Expected O, but got Unknown
		//IL_0dd9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ddf: Expected O, but got Unknown
		//IL_0df5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dfb: Expected O, but got Unknown
		//IL_0e11: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e17: Expected O, but got Unknown
		//IL_0e2d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e33: Expected O, but got Unknown
		//IL_0e49: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e4f: Expected O, but got Unknown
		//IL_0e65: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e6b: Expected O, but got Unknown
		//IL_0e81: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e87: Expected O, but got Unknown
		//IL_0e9d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ea3: Expected O, but got Unknown
		//IL_0eb9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ebf: Expected O, but got Unknown
		//IL_0ed5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0edb: Expected O, but got Unknown
		//IL_0ef1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ef7: Expected O, but got Unknown
		//IL_0f0d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f13: Expected O, but got Unknown
		//IL_0f29: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f2f: Expected O, but got Unknown
		//IL_0f45: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f4b: Expected O, but got Unknown
		//IL_0f61: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f67: Expected O, but got Unknown
		//IL_0f7d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f83: Expected O, but got Unknown
		//IL_0f99: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f9f: Expected O, but got Unknown
		//IL_0fb5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fbb: Expected O, but got Unknown
		//IL_0fd1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fd7: Expected O, but got Unknown
		//IL_0fed: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ff3: Expected O, but got Unknown
		//IL_1009: Unknown result type (might be due to invalid IL or missing references)
		//IL_100f: Expected O, but got Unknown
		//IL_1025: Unknown result type (might be due to invalid IL or missing references)
		//IL_102b: Expected O, but got Unknown
		//IL_1041: Unknown result type (might be due to invalid IL or missing references)
		//IL_1047: Expected O, but got Unknown
		//IL_105d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1063: Expected O, but got Unknown
		//IL_1079: Unknown result type (might be due to invalid IL or missing references)
		//IL_107f: Expected O, but got Unknown
		//IL_1095: Unknown result type (might be due to invalid IL or missing references)
		//IL_109b: Expected O, but got Unknown
		//IL_10b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_10b7: Expected O, but got Unknown
		//IL_10cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_10d3: Expected O, but got Unknown
		//IL_10e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_10ef: Expected O, but got Unknown
		//IL_1105: Unknown result type (might be due to invalid IL or missing references)
		//IL_110b: Expected O, but got Unknown
		//IL_1121: Unknown result type (might be due to invalid IL or missing references)
		//IL_1127: Expected O, but got Unknown
		//IL_113d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1143: Expected O, but got Unknown
		//IL_1159: Unknown result type (might be due to invalid IL or missing references)
		//IL_115f: Expected O, but got Unknown
		//IL_1175: Unknown result type (might be due to invalid IL or missing references)
		//IL_117b: Expected O, but got Unknown
		//IL_1191: Unknown result type (might be due to invalid IL or missing references)
		//IL_1197: Expected O, but got Unknown
		//IL_11ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_11b3: Expected O, but got Unknown
		//IL_11c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_11cf: Expected O, but got Unknown
		//IL_11e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_11eb: Expected O, but got Unknown
		//IL_1201: Unknown result type (might be due to invalid IL or missing references)
		//IL_1207: Expected O, but got Unknown
		//IL_121d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1223: Expected O, but got Unknown
		//IL_1239: Unknown result type (might be due to invalid IL or missing references)
		//IL_123f: Expected O, but got Unknown
		//IL_1255: Unknown result type (might be due to invalid IL or missing references)
		//IL_125b: Expected O, but got Unknown
		//IL_1271: Unknown result type (might be due to invalid IL or missing references)
		//IL_1277: Expected O, but got Unknown
		//IL_128d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1293: Expected O, but got Unknown
		//IL_12a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_12af: Expected O, but got Unknown
		//IL_12c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_12cb: Expected O, but got Unknown
		//IL_12e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_12e7: Expected O, but got Unknown
		//IL_12fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_1303: Expected O, but got Unknown
		//IL_1319: Unknown result type (might be due to invalid IL or missing references)
		//IL_131f: Expected O, but got Unknown
		//IL_1335: Unknown result type (might be due to invalid IL or missing references)
		//IL_133b: Expected O, but got Unknown
		//IL_1351: Unknown result type (might be due to invalid IL or missing references)
		//IL_1357: Expected O, but got Unknown
		//IL_136d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1373: Expected O, but got Unknown
		//IL_1389: Unknown result type (might be due to invalid IL or missing references)
		//IL_138f: Expected O, but got Unknown
		//IL_13a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_13ab: Expected O, but got Unknown
		//IL_13c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_13c7: Expected O, but got Unknown
		//IL_13dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_13e3: Expected O, but got Unknown
		//IL_13f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_13ff: Expected O, but got Unknown
		//IL_1415: Unknown result type (might be due to invalid IL or missing references)
		//IL_141b: Expected O, but got Unknown
		//IL_1480: Unknown result type (might be due to invalid IL or missing references)
		//IL_1486: Expected O, but got Unknown
		//IL_1498: Unknown result type (might be due to invalid IL or missing references)
		//IL_149e: Expected O, but got Unknown
		//IL_14b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_14b6: Expected O, but got Unknown
		//IL_14c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_14ce: Expected O, but got Unknown
		//IL_14e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_14e6: Expected O, but got Unknown
		//IL_14f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_14fe: Expected O, but got Unknown
		//IL_1510: Unknown result type (might be due to invalid IL or missing references)
		//IL_1516: Expected O, but got Unknown
		//IL_1528: Unknown result type (might be due to invalid IL or missing references)
		//IL_152e: Expected O, but got Unknown
		//IL_1540: Unknown result type (might be due to invalid IL or missing references)
		//IL_1546: Expected O, but got Unknown
		//IL_1558: Unknown result type (might be due to invalid IL or missing references)
		//IL_155e: Expected O, but got Unknown
		//IL_1570: Unknown result type (might be due to invalid IL or missing references)
		//IL_1576: Expected O, but got Unknown
		//IL_1588: Unknown result type (might be due to invalid IL or missing references)
		//IL_158e: Expected O, but got Unknown
		//IL_15a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_15a6: Expected O, but got Unknown
		//IL_15b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_15be: Expected O, but got Unknown
		//IL_15d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_15d6: Expected O, but got Unknown
		//IL_15e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_15ee: Expected O, but got Unknown
		//IL_1600: Unknown result type (might be due to invalid IL or missing references)
		//IL_1606: Expected O, but got Unknown
		//IL_1618: Unknown result type (might be due to invalid IL or missing references)
		//IL_161e: Expected O, but got Unknown
		//IL_1630: Unknown result type (might be due to invalid IL or missing references)
		//IL_1636: Expected O, but got Unknown
		//IL_1648: Unknown result type (might be due to invalid IL or missing references)
		//IL_164e: Expected O, but got Unknown
		//IL_165a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1664: Expected O, but got Unknown
		//IL_1670: Unknown result type (might be due to invalid IL or missing references)
		//IL_167a: Expected O, but got Unknown
		//IL_1686: Unknown result type (might be due to invalid IL or missing references)
		//IL_1690: Expected O, but got Unknown
		//IL_169c: Unknown result type (might be due to invalid IL or missing references)
		//IL_16a6: Expected O, but got Unknown
		//IL_16b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_16bc: Expected O, but got Unknown
		//IL_16c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_16d2: Expected O, but got Unknown
		//IL_16de: Unknown result type (might be due to invalid IL or missing references)
		//IL_16e8: Expected O, but got Unknown
		//IL_16f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_16fe: Expected O, but got Unknown
		//IL_170a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1714: Expected O, but got Unknown
		//IL_1720: Unknown result type (might be due to invalid IL or missing references)
		//IL_172a: Expected O, but got Unknown
		//IL_1736: Unknown result type (might be due to invalid IL or missing references)
		//IL_1740: Expected O, but got Unknown
		//IL_174c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1756: Expected O, but got Unknown
		//IL_1762: Unknown result type (might be due to invalid IL or missing references)
		//IL_176c: Expected O, but got Unknown
		//IL_1778: Unknown result type (might be due to invalid IL or missing references)
		//IL_1782: Expected O, but got Unknown
		//IL_178e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1798: Expected O, but got Unknown
		//IL_17a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_17ae: Expected O, but got Unknown
		//IL_17ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_17c4: Expected O, but got Unknown
		//IL_17d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_17da: Expected O, but got Unknown
		//IL_17e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_17f0: Expected O, but got Unknown
		//IL_17fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_1806: Expected O, but got Unknown
		//IL_1812: Unknown result type (might be due to invalid IL or missing references)
		//IL_181c: Expected O, but got Unknown
		//IL_1828: Unknown result type (might be due to invalid IL or missing references)
		//IL_1832: Expected O, but got Unknown
		//IL_183e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1848: Expected O, but got Unknown
		//IL_1854: Unknown result type (might be due to invalid IL or missing references)
		//IL_185e: Expected O, but got Unknown
		//IL_186a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1874: Expected O, but got Unknown
		//IL_1880: Unknown result type (might be due to invalid IL or missing references)
		//IL_188a: Expected O, but got Unknown
		//IL_1896: Unknown result type (might be due to invalid IL or missing references)
		//IL_18a0: Expected O, but got Unknown
		//IL_18ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_18b6: Expected O, but got Unknown
		//IL_18c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_18cc: Expected O, but got Unknown
		//IL_18d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_18e2: Expected O, but got Unknown
		//IL_18ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_18f8: Expected O, but got Unknown
		//IL_1904: Unknown result type (might be due to invalid IL or missing references)
		//IL_190e: Expected O, but got Unknown
		//IL_191a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1924: Expected O, but got Unknown
		//IL_1930: Unknown result type (might be due to invalid IL or missing references)
		//IL_193a: Expected O, but got Unknown
		//IL_1946: Unknown result type (might be due to invalid IL or missing references)
		//IL_1950: Expected O, but got Unknown
		//IL_195c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1966: Expected O, but got Unknown
		//IL_1972: Unknown result type (might be due to invalid IL or missing references)
		//IL_197c: Expected O, but got Unknown
		//IL_1988: Unknown result type (might be due to invalid IL or missing references)
		//IL_1992: Expected O, but got Unknown
		//IL_199e: Unknown result type (might be due to invalid IL or missing references)
		//IL_19a8: Expected O, but got Unknown
		//IL_19b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_19be: Expected O, but got Unknown
		//IL_19ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_19d4: Expected O, but got Unknown
		//IL_19e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_19ea: Expected O, but got Unknown
		//IL_19f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a00: Expected O, but got Unknown
		//IL_1a0c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a16: Expected O, but got Unknown
		//IL_1a22: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a2c: Expected O, but got Unknown
		//IL_1a38: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a42: Expected O, but got Unknown
		//IL_1a4e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a58: Expected O, but got Unknown
		//IL_1a64: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a6e: Expected O, but got Unknown
		InitializeComponent();
		MainViewModel.Instance.HUDmain = this;
		buildButtons[380] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubKeepsReturn");
		buildButtons[81] = (Button)((FrameworkElement)this).FindName("ButtonBuildKeep1");
		buildButtons[82] = (Button)((FrameworkElement)this).FindName("ButtonBuildKeep2");
		buildButtons[83] = (Button)((FrameworkElement)this).FindName("ButtonBuildKeep3");
		buildButtons[84] = (Button)((FrameworkElement)this).FindName("ButtonBuildOutpostArab");
		buildButtons[85] = (Button)((FrameworkElement)this).FindName("ButtonBuildOutpost");
		buildButtons[582] = (Button)((FrameworkElement)this).FindName("ButtonBuildOutpostBedouin");
		buildButtons[51] = (Button)((FrameworkElement)this).FindName("ButtonBuildStairs");
		buildButtons[53] = (Button)((FrameworkElement)this).FindName("ButtonBuildWoodenWall");
		buildButtons[50] = (Button)((FrameworkElement)this).FindName("ButtonBuildStoneWall");
		buildButtons[52] = (Button)((FrameworkElement)this).FindName("ButtonBuildCrenalatedWall");
		buildButtons[543] = (Button)((FrameworkElement)this).FindName("ButtonBuildWoodenBedouinStockade");
		buildButtons[7] = (Button)((FrameworkElement)this).FindName("ButtonBuildWoodenBarracks");
		buildButtons[8] = (Button)((FrameworkElement)this).FindName("ButtonBuildStoneBarracks");
		buildButtons[5] = (Button)((FrameworkElement)this).FindName("ButtonBuildArmoury");
		buildButtons[60] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubTowers");
		buildButtons[70] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubGates");
		buildButtons[68] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubMilitaryBuildings");
		buildButtons[80] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubKeeps");
		buildButtons[3] = (Button)((FrameworkElement)this).FindName("ButtonBuildStockpile");
		buildButtons[2] = (Button)((FrameworkElement)this).FindName("ButtonBuildWoodcutter");
		buildButtons[24] = (Button)((FrameworkElement)this).FindName("ButtonBuildQuarry");
		buildButtons[25] = (Button)((FrameworkElement)this).FindName("ButtonBuildOxTether");
		buildButtons[26] = (Button)((FrameworkElement)this).FindName("ButtonBuildIronMine");
		buildButtons[28] = (Button)((FrameworkElement)this).FindName("ButtonBuildPitchRig");
		buildButtons[30] = (Button)((FrameworkElement)this).FindName("ButtonBuildMarket");
		buildButtons[22] = (Button)((FrameworkElement)this).FindName("ButtonBuildHunter");
		buildButtons[21] = (Button)((FrameworkElement)this).FindName("ButtonBuildDairyFarm");
		buildButtons[19] = (Button)((FrameworkElement)this).FindName("ButtonBuildAppleFarm");
		buildButtons[18] = (Button)((FrameworkElement)this).FindName("ButtonBuildWheatFarm");
		buildButtons[20] = (Button)((FrameworkElement)this).FindName("ButtonBuildHopsFarm");
		buildButtons[27] = (Button)((FrameworkElement)this).FindName("ButtonBuildHovel");
		buildButtons[87] = (Button)((FrameworkElement)this).FindName("ButtonBuildSmallChurch");
		buildButtons[88] = (Button)((FrameworkElement)this).FindName("ButtonBuildMedChurch");
		buildButtons[89] = (Button)((FrameworkElement)this).FindName("ButtonBuildLargeChurch");
		buildButtons[583] = (Button)((FrameworkElement)this).FindName("ButtonBuildSmallMosque");
		buildButtons[584] = (Button)((FrameworkElement)this).FindName("ButtonBuildMedMosque");
		buildButtons[585] = (Button)((FrameworkElement)this).FindName("ButtonBuildLargeMosque");
		buildButtons[34] = (Button)((FrameworkElement)this).FindName("ButtonBuildApocathery");
		buildButtons[415] = (Button)((FrameworkElement)this).FindName("ButtonBuildWell");
		buildButtons[542] = (Button)((FrameworkElement)this).FindName("ButtonBuildWaterpot");
		buildButtons[67] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubGoodStuff");
		buildButtons[78] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubBadStuff");
		buildButtons[10] = (Button)((FrameworkElement)this).FindName("ButtonBuildFletcher");
		buildButtons[11] = (Button)((FrameworkElement)this).FindName("ButtonBuildPoleturner");
		buildButtons[12] = (Button)((FrameworkElement)this).FindName("ButtonBuildBlacksmith");
		buildButtons[13] = (Button)((FrameworkElement)this).FindName("ButtonBuildTanner");
		buildButtons[14] = (Button)((FrameworkElement)this).FindName("ButtonBuildArmourer");
		buildButtons[4] = (Button)((FrameworkElement)this).FindName("ButtonBuildGranary");
		buildButtons[15] = (Button)((FrameworkElement)this).FindName("ButtonBuildBaker");
		buildButtons[23] = (Button)((FrameworkElement)this).FindName("ButtonBuildMill");
		buildButtons[16] = (Button)((FrameworkElement)this).FindName("ButtonBuildBrewer");
		buildButtons[33] = (Button)((FrameworkElement)this).FindName("ButtonBuildInn");
		buildButtons[381] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubTowersReturn");
		buildButtons[61] = (Button)((FrameworkElement)this).FindName("ButtonBuildTowerA");
		buildButtons[62] = (Button)((FrameworkElement)this).FindName("ButtonBuildTowerB");
		buildButtons[63] = (Button)((FrameworkElement)this).FindName("ButtonBuildTowerC");
		buildButtons[64] = (Button)((FrameworkElement)this).FindName("ButtonBuildTowerD");
		buildButtons[65] = (Button)((FrameworkElement)this).FindName("ButtonBuildTowerE");
		buildButtons[382] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubGatesReturn");
		buildButtons[73] = (Button)((FrameworkElement)this).FindName("ButtonBuildSmallGate");
		buildButtons[76] = (Button)((FrameworkElement)this).FindName("ButtonBuildLargeGate");
		buildButtons[74] = (Button)((FrameworkElement)this).FindName("ButtonBuildDrawbridge");
		buildButtons[408] = (Button)((FrameworkElement)this).FindName("ButtonBuildDogCage");
		buildButtons[54] = (Button)((FrameworkElement)this).FindName("ButtonBuildPitchDitch");
		buildButtons[56] = (Button)((FrameworkElement)this).FindName("ButtonBuildKillingPit");
		buildButtons[55] = (Button)((FrameworkElement)this).FindName("ButtonBuildBrazier");
		buildButtons[75] = (Button)((FrameworkElement)this).FindName("ButtonBuildMoat");
		buildButtons[77] = (Button)((FrameworkElement)this).FindName("ButtonBuildAntiMoat");
		buildButtons[383] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubMilitaryBuildingsReturn");
		buildButtons[35] = (Button)((FrameworkElement)this).FindName("ButtonBuildEngineersGuild");
		buildButtons[47] = (Button)((FrameworkElement)this).FindName("ButtonBuildMangonel");
		buildButtons[48] = (Button)((FrameworkElement)this).FindName("ButtonBuildBalista");
		buildButtons[31] = (Button)((FrameworkElement)this).FindName("ButtonBuildStables");
		buildButtons[36] = (Button)((FrameworkElement)this).FindName("ButtonBuildTunnelersGuild");
		buildButtons[46] = (Button)((FrameworkElement)this).FindName("ButtonBuildCauldron");
		buildButtons[384] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubBadStuffReturn");
		buildButtons[40] = (Button)((FrameworkElement)this).FindName("ButtonBuildGallows");
		buildButtons[400] = (Button)((FrameworkElement)this).FindName("ButtonBuildCessPit");
		buildButtons[44] = (Button)((FrameworkElement)this).FindName("ButtonBuildStocks");
		buildButtons[86] = (Button)((FrameworkElement)this).FindName("ButtonBuildHeadOnSpike");
		buildButtons[401] = (Button)((FrameworkElement)this).FindName("ButtonBuildBurningPost");
		buildButtons[403] = (Button)((FrameworkElement)this).FindName("ButtonBuildDungeon");
		buildButtons[404] = (Button)((FrameworkElement)this).FindName("ButtonBuildStretchingRack");
		buildButtons[402] = (Button)((FrameworkElement)this).FindName("ButtonBuildGibbet");
		buildButtons[406] = (Button)((FrameworkElement)this).FindName("ButtonBuildChoppingBlock");
		buildButtons[183] = (Button)((FrameworkElement)this).FindName("ButtonBuildDunkingStool");
		buildButtons[385] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubGoodStuffReturn");
		buildButtons[41] = (Button)((FrameworkElement)this).FindName("ButtonBuildMaypole");
		buildButtons[412] = (Button)((FrameworkElement)this).FindName("ButtonBuildDancingBear");
		buildButtons[176] = (Button)((FrameworkElement)this).FindName("ButtonBuildGardenSmall");
		buildButtons[180] = (Button)((FrameworkElement)this).FindName("ButtonBuildGardenMed");
		buildButtons[189] = (Button)((FrameworkElement)this).FindName("ButtonBuildGardenLarge");
		buildButtons[417] = (Button)((FrameworkElement)this).FindName("ButtonBuildPilgrimsCross");
		buildButtons[586] = (Button)((FrameworkElement)this).FindName("ButtonBuildStatueA");
		buildButtons[410] = (Button)((FrameworkElement)this).FindName("ButtonBuildShrine");
		buildButtons[170] = (Button)((FrameworkElement)this).FindName("ButtonBuildFlag1");
		buildButtons[171] = (Button)((FrameworkElement)this).FindName("ButtonBuildFlag2");
		buildButtons[172] = (Button)((FrameworkElement)this).FindName("ButtonBuildFlag3");
		buildButtons[587] = (Button)((FrameworkElement)this).FindName("ButtonBuildFlag3A");
		buildButtons[199] = (Button)((FrameworkElement)this).FindName("ButtonBuildFlag4");
		buildButtons[387] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubSubSmallGatesReturn");
		buildButtons[389] = (Button)((FrameworkElement)this).FindName("ButtonBuildSmallGatehouseNS");
		buildButtons[390] = (Button)((FrameworkElement)this).FindName("ButtonBuildSmallGatehouseEW");
		buildButtons[388] = (Button)((FrameworkElement)this).FindName("ButtonBuildSubSubLargeGatesReturn");
		buildButtons[391] = (Button)((FrameworkElement)this).FindName("ButtonBuildLargeGatehouseNS");
		buildButtons[392] = (Button)((FrameworkElement)this).FindName("ButtonBuildLargeGatehouseEW");
		buildButtons[555] = (Button)((FrameworkElement)this).FindName("ButtonBuildRuinsPageToggle1");
		buildButtons[556] = (Button)((FrameworkElement)this).FindName("ButtonBuildRuinsPageToggle2");
		buildButtons[570] = (Button)((FrameworkElement)this).FindName("ButtonBuildRuinsPageToggle3");
		buildButtons[577] = (Button)((FrameworkElement)this).FindName("ButtonBuildRuinsPageToggle4");
		buildButtons[578] = (Button)((FrameworkElement)this).FindName("ButtonBuildRuinsPageToggle1b");
		buildButtons[579] = (Button)((FrameworkElement)this).FindName("ButtonBuildRuinsPageToggle2b");
		buildButtons[580] = (Button)((FrameworkElement)this).FindName("ButtonBuildRuinsPageToggle3b");
		buildButtons[581] = (Button)((FrameworkElement)this).FindName("ButtonBuildRuinsPageToggle4b");
		buildButtons[500] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins1");
		buildButtons[501] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins2");
		buildButtons[502] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins3");
		buildButtons[503] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins4");
		buildButtons[504] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins5");
		buildButtons[505] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins6");
		buildButtons[506] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins7");
		buildButtons[507] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins8");
		buildButtons[508] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins9");
		buildButtons[509] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins10");
		buildButtons[510] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins11");
		buildButtons[511] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins12");
		buildButtons[512] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins13");
		buildButtons[552] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins14");
		buildButtons[553] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins15");
		buildButtons[554] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins16");
		buildButtons[557] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins17");
		buildButtons[558] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEDock");
		buildButtons[559] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins18");
		buildButtons[560] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins19");
		buildButtons[561] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins20");
		buildButtons[562] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins21");
		buildButtons[563] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins22");
		buildButtons[564] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins23");
		buildButtons[565] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins24");
		buildButtons[566] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins25");
		buildButtons[567] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins26");
		buildButtons[568] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins27");
		buildButtons[569] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins28");
		buildButtons[571] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins29");
		buildButtons[572] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins30");
		buildButtons[573] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins31");
		buildButtons[574] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins32");
		buildButtons[575] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins33");
		buildButtons[576] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERuins34");
		buildButtons[480] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEArcher");
		buildButtons[481] = (Button)((FrameworkElement)this).FindName("ButtonBuildMESpearman");
		buildButtons[482] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEPikeman");
		buildButtons[483] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEMaceman");
		buildButtons[484] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEXBowman");
		buildButtons[485] = (Button)((FrameworkElement)this).FindName("ButtonBuildMESwordsman");
		buildButtons[486] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEKnight");
		buildButtons[487] = (Button)((FrameworkElement)this).FindName("ButtonBuildMELadderman");
		buildButtons[488] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEEngineer");
		buildButtons[496] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEOilguy");
		buildButtons[489] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEMonk");
		buildButtons[495] = (Button)((FrameworkElement)this).FindName("ButtonBuildMETunneler");
		buildButtons[490] = (Button)((FrameworkElement)this).FindName("ButtonBuildMECatapult");
		buildButtons[491] = (Button)((FrameworkElement)this).FindName("ButtonBuildMETrebuchet");
		buildButtons[492] = (Button)((FrameworkElement)this).FindName("ButtonBuildMERam");
		buildButtons[493] = (Button)((FrameworkElement)this).FindName("ButtonBuildMESiegeTower");
		buildButtons[494] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEMantlet");
		buildButtons[534] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEArabBow");
		buildButtons[535] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEArabSlave");
		buildButtons[536] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEArabSlinger");
		buildButtons[537] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEArabAssassin");
		buildButtons[538] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEArabHorseArcher");
		buildButtons[539] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEArabSwordsman");
		buildButtons[540] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEArabGrenadier");
		buildButtons[541] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEArabBallista");
		buildButtons[544] = (Button)((FrameworkElement)this).FindName("ButtonBuildMECamelLancer");
		buildButtons[545] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEHealer");
		buildButtons[546] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEEunuch");
		buildButtons[547] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEAmbusher");
		buildButtons[548] = (Button)((FrameworkElement)this).FindName("ButtonBuildMESkirmisher");
		buildButtons[549] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEHeavyCamel");
		buildButtons[550] = (Button)((FrameworkElement)this).FindName("ButtonBuildMESapper");
		buildButtons[551] = (Button)((FrameworkElement)this).FindName("ButtonBuildMEDemolisher");
		Button[] array = buildButtons;
		foreach (Button val in array)
		{
			if ((BaseComponent)(object)val != (BaseComponent)null && ((FrameworkElement)val).Width != 36f)
			{
				((FrameworkElement)val).Width = ((FrameworkElement)val).Width + 8f;
				((FrameworkElement)val).Height = ((FrameworkElement)val).Height + 8f;
			}
		}
		sheildButtons[0] = (RadioButton)((FrameworkElement)this).FindName("ButtonMESheild1");
		sheildButtons[1] = (RadioButton)((FrameworkElement)this).FindName("ButtonMESheild2");
		sheildButtons[2] = (RadioButton)((FrameworkElement)this).FindName("ButtonMESheild3");
		sheildButtons[3] = (RadioButton)((FrameworkElement)this).FindName("ButtonMESheild4");
		sheildButtons[4] = (RadioButton)((FrameworkElement)this).FindName("ButtonMESheild5");
		sheildButtons[5] = (RadioButton)((FrameworkElement)this).FindName("ButtonMESheild6");
		sheildButtons[6] = (RadioButton)((FrameworkElement)this).FindName("ButtonMESheild7");
		sheildButtons[7] = (RadioButton)((FrameworkElement)this).FindName("ButtonMESheild8");
		MEBrushSizeButtons[0] = (Button)((FrameworkElement)this).FindName("ButtonBrushSize1");
		MEBrushSizeButtons[1] = (Button)((FrameworkElement)this).FindName("ButtonBrushSize2");
		MEBrushSizeButtons[2] = (Button)((FrameworkElement)this).FindName("ButtonBrushSize3");
		MEBrushSizeButtons[3] = (Button)((FrameworkElement)this).FindName("ButtonBrushSize4");
		MEBrushSizeButtons[4] = (Button)((FrameworkElement)this).FindName("ButtonBrushSize5");
		MEBrushSizeButtons[5] = (Button)((FrameworkElement)this).FindName("ButtonBrushSize6");
		MEBrushSizeButtons[6] = (Button)((FrameworkElement)this).FindName("ButtonBrushSize7");
		MEBrushSizeButtons[7] = (Button)((FrameworkElement)this).FindName("ButtonBrushSize8");
		MEBrushSizeButtons[8] = (Button)((FrameworkElement)this).FindName("ButtonBrushSize9");
		MERulerButtons[0] = (Button)((FrameworkElement)this).FindName("ButtonRuler1");
		MERulerButtons[1] = (Button)((FrameworkElement)this).FindName("ButtonRuler2");
		MERulerButtons[2] = (Button)((FrameworkElement)this).FindName("ButtonRuler3");
		RefGuideRect = (Rectangle)((FrameworkElement)this).FindName("GuideRect");
		RefMapEditorModeToggle = (StackPanel)((FrameworkElement)this).FindName("MEModeToggle");
		RefLayoutRoot = (Grid)((FrameworkElement)this).FindName("LayoutRoot");
		RefMapEditorSheilds = (Grid)((FrameworkElement)this).FindName("MESheildPanel");
		RefFrameBuildings = (Grid)((FrameworkElement)this).FindName("MainFrameBuildings");
		RefFrameTerrain = (Grid)((FrameworkElement)this).FindName("MainFrameTerrain");
		RefBuildMenuGrid = (Grid)((FrameworkElement)this).FindName("BuildMenuGrid");
		RefMEMenuGrid = (Grid)((FrameworkElement)this).FindName("MEMenuGrid");
		RefBottomTabs1 = (StackPanel)((FrameworkElement)this).FindName("BottomTabs1");
		RefSideTabs = (StackPanel)((FrameworkElement)this).FindName("SideTabs");
		RefBottomTabs2a = (RadioButton)((FrameworkElement)this).FindName("RadioButtonMERuins");
		RefBottomTabs2b = (RadioButton)((FrameworkElement)this).FindName("RadioButtonMETroops");
		RefBottomTabs2c = (RadioButton)((FrameworkElement)this).FindName("RadioButtonMEArabTroops");
		RefBottomTabs2d = (RadioButton)((FrameworkElement)this).FindName("RadioButtonMESiege");
		RefBottomTabs2e = (RadioButton)((FrameworkElement)this).FindName("RadioButtonMEBedouin");
		RefButtonBuildModeBuildings = (RadioButton)((FrameworkElement)this).FindName("ButtonBuildModeBuildings");
		RefButtonBuildModeTerrain = (RadioButton)((FrameworkElement)this).FindName("ButtonBuildModeTerrain");
		RefButtonMEHeightControls = (RadioButton)((FrameworkElement)this).FindName("ButtonMEHeightControls");
		RefButtonMERocksSignpost = (RadioButton)((FrameworkElement)this).FindName("ButtonMERocksSignpost");
		RefMETerrainMenu = (Grid)((FrameworkElement)this).FindName("METerrainMenu");
		RefMEAnimalsMenu = (Grid)((FrameworkElement)this).FindName("MEAnimalsMenu");
		RefMETextureMenu = (Grid)((FrameworkElement)this).FindName("METextureMenu");
		RefMEWaterMenu = (Grid)((FrameworkElement)this).FindName("MEWaterMenu");
		RefMEVegetationMenu = (Grid)((FrameworkElement)this).FindName("MEVegetationMenu");
		RefMERocksMenu = (Grid)((FrameworkElement)this).FindName("MERocksMenu");
		RefTabBuildCastle = (RadioButton)((FrameworkElement)this).FindName("TabBuildCastle");
		RefTabBuildIndustry = (RadioButton)((FrameworkElement)this).FindName("TabBuildIndustry");
		RefTabBuildFarms = (RadioButton)((FrameworkElement)this).FindName("TabBuildFarms");
		RefTabBuildTown = (RadioButton)((FrameworkElement)this).FindName("TabBuildTown");
		RefTabBuildWeapons = (RadioButton)((FrameworkElement)this).FindName("TabBuildWeapons");
		RefTabBuildFood = (RadioButton)((FrameworkElement)this).FindName("TabBuildFood");
		RefGameInfoButton = (Button)((FrameworkElement)this).FindName("GameInfoButton");
		RefGameUndoButton = (Button)((FrameworkElement)this).FindName("GameUndoButton");
		RefTutorialArrow1 = (Image)((FrameworkElement)this).FindName("TutorialArrow1");
		RefTutorialArrow2 = (Image)((FrameworkElement)this).FindName("TutorialArrow2");
		RefTutorialArrow7 = (Image)((FrameworkElement)this).FindName("TutorialArrow7");
		RefTutorialArrow8 = (Image)((FrameworkElement)this).FindName("TutorialArrow8");
		RefTutorialArrow9 = (Image)((FrameworkElement)this).FindName("TutorialArrow9");
		RefTutorialArrow10 = (Image)((FrameworkElement)this).FindName("TutorialArrow10");
		RefTutorialArrow11 = (Image)((FrameworkElement)this).FindName("TutorialArrow11");
		RefTutorialArrow12 = (Image)((FrameworkElement)this).FindName("TutorialArrow12");
		RefTutorialArrow13 = (Image)((FrameworkElement)this).FindName("TutorialArrow13");
		RefTutorialArrow14 = (Image)((FrameworkElement)this).FindName("TutorialArrow14");
		RefTutorialArrow15 = (Image)((FrameworkElement)this).FindName("TutorialArrow15");
		RefTutorialArrow16 = (Image)((FrameworkElement)this).FindName("TutorialArrow16");
		RefTutorialArrow17 = (Image)((FrameworkElement)this).FindName("TutorialArrow17");
		RefTutorialArrow18 = (Image)((FrameworkElement)this).FindName("TutorialArrow18");
		RefTutorialArrow20 = (Image)((FrameworkElement)this).FindName("TutorialArrow20");
		scrollPosition = 0;
		SetupNewBuildScreen(-1);
		findUIlowerPoint();
	}

	public void SetEditorModeButtonVisibilityForSiegeThatMode(bool visible)
	{
		for (int i = 0; i < 8; i++)
		{
			((UIElement)MainViewModel.Instance.HUDmain.sheildButtons[i]).Visibility = (Visibility)1;
		}
		if (visible)
		{
			((UIElement)MainViewModel.Instance.HUDmain.RefButtonBuildModeTerrain).Visibility = (Visibility)2;
			((UIElement)MainViewModel.Instance.HUDmain.RefButtonBuildModeBuildings).Visibility = (Visibility)2;
			if (GameData.Instance.mapType == Enums.GameModes.INVASION || GameData.Instance.multiplayerMap)
			{
				for (int j = 0; j < 8; j++)
				{
					((UIElement)MainViewModel.Instance.HUDmain.sheildButtons[j]).Visibility = (Visibility)2;
				}
			}
			else
			{
				((UIElement)MainViewModel.Instance.HUDmain.sheildButtons[0]).Visibility = (Visibility)2;
			}
		}
		else
		{
			((UIElement)MainViewModel.Instance.HUDmain.RefButtonBuildModeTerrain).Visibility = (Visibility)1;
			((UIElement)MainViewModel.Instance.HUDmain.RefButtonBuildModeBuildings).Visibility = (Visibility)1;
		}
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_Main.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Expected O, but got Unknown
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Expected O, but got Unknown
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Expected O, but got Unknown
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Expected O, but got Unknown
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Expected O, but got Unknown
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Expected O, but got Unknown
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Expected O, but got Unknown
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Expected O, but got Unknown
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_022f: Expected O, but got Unknown
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0258: Unknown result type (might be due to invalid IL or missing references)
		//IL_0262: Expected O, but got Unknown
		//IL_027f: Unknown result type (might be due to invalid IL or missing references)
		//IL_028b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Expected O, but got Unknown
		//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02be: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Expected O, but got Unknown
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fb: Expected O, but got Unknown
		//IL_0318: Unknown result type (might be due to invalid IL or missing references)
		//IL_0324: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Expected O, but got Unknown
		//IL_034b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0357: Unknown result type (might be due to invalid IL or missing references)
		//IL_0361: Expected O, but got Unknown
		//IL_037e: Unknown result type (might be due to invalid IL or missing references)
		//IL_038a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0394: Expected O, but got Unknown
		//IL_03b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c7: Expected O, but got Unknown
		//IL_03e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fa: Expected O, but got Unknown
		//IL_0417: Unknown result type (might be due to invalid IL or missing references)
		//IL_0423: Unknown result type (might be due to invalid IL or missing references)
		//IL_042d: Expected O, but got Unknown
		//IL_044a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0456: Unknown result type (might be due to invalid IL or missing references)
		//IL_0460: Expected O, but got Unknown
		//IL_047d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0489: Unknown result type (might be due to invalid IL or missing references)
		//IL_0493: Expected O, but got Unknown
		//IL_04b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c6: Expected O, but got Unknown
		//IL_04e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f9: Expected O, but got Unknown
		//IL_0516: Unknown result type (might be due to invalid IL or missing references)
		//IL_0522: Unknown result type (might be due to invalid IL or missing references)
		//IL_052c: Expected O, but got Unknown
		//IL_0549: Unknown result type (might be due to invalid IL or missing references)
		//IL_0555: Unknown result type (might be due to invalid IL or missing references)
		//IL_055f: Expected O, but got Unknown
		//IL_057c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0588: Unknown result type (might be due to invalid IL or missing references)
		//IL_0592: Expected O, but got Unknown
		//IL_05af: Unknown result type (might be due to invalid IL or missing references)
		//IL_05bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c5: Expected O, but got Unknown
		//IL_05e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f8: Expected O, but got Unknown
		//IL_0615: Unknown result type (might be due to invalid IL or missing references)
		//IL_0621: Unknown result type (might be due to invalid IL or missing references)
		//IL_062b: Expected O, but got Unknown
		//IL_0648: Unknown result type (might be due to invalid IL or missing references)
		//IL_0654: Unknown result type (might be due to invalid IL or missing references)
		//IL_065e: Expected O, but got Unknown
		//IL_067b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0687: Unknown result type (might be due to invalid IL or missing references)
		//IL_0691: Expected O, but got Unknown
		//IL_06ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c4: Expected O, but got Unknown
		//IL_06e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f7: Expected O, but got Unknown
		//IL_0714: Unknown result type (might be due to invalid IL or missing references)
		//IL_0720: Unknown result type (might be due to invalid IL or missing references)
		//IL_072a: Expected O, but got Unknown
		//IL_0747: Unknown result type (might be due to invalid IL or missing references)
		//IL_0753: Unknown result type (might be due to invalid IL or missing references)
		//IL_075d: Expected O, but got Unknown
		//IL_077a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0786: Unknown result type (might be due to invalid IL or missing references)
		//IL_0790: Expected O, but got Unknown
		//IL_07ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_07b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_07c3: Expected O, but got Unknown
		//IL_07e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_07f6: Expected O, but got Unknown
		//IL_081b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0827: Unknown result type (might be due to invalid IL or missing references)
		//IL_0831: Expected O, but got Unknown
		//IL_0877: Unknown result type (might be due to invalid IL or missing references)
		//IL_0883: Unknown result type (might be due to invalid IL or missing references)
		//IL_088d: Expected O, but got Unknown
		//IL_083c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0848: Unknown result type (might be due to invalid IL or missing references)
		//IL_0852: Expected O, but got Unknown
		//IL_0898: Unknown result type (might be due to invalid IL or missing references)
		//IL_08a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_08ae: Expected O, but got Unknown
		if (eventName == "Click" && handlerName == "InGameOptions")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(InGameOptions);
			return true;
		}
		if (eventName == "Click" && handlerName == "UndoLastAction")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(UndoLastAction);
			return true;
		}
		if (eventName == "Click" && handlerName == "ReturnToBriefingScreen")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(ReturnToBriefingScreen);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenKeeps")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(NewBuildScreenKeeps);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenCastle")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewBuildScreenCastle);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenIndustry")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewBuildScreenIndustry);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenFarms")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewBuildScreenFarms);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenTown")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewBuildScreenTown);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenWeapons")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewBuildScreenWeapons);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenFood")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewBuildScreenFood);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenTowers")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(NewBuildScreenTowers);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenGates")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(NewBuildScreenGates);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenMilitaryBuildings")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(NewBuildScreenMilitaryBuildings);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenBadStuff")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(NewBuildScreenBadStuff);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenGoodStuff")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(NewBuildScreenGoodStuff);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenSubTowersRtn")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(NewBuildScreenSubTowersRtn);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenSubTownRtn")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(NewBuildScreenSubTownRtn);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenSubSubSmallGates")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(NewBuildScreenSubSubSmallGates);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenSubSubLargeGates")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(NewBuildScreenSubSubLargeGates);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenSubGatesRtn")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(NewBuildScreenSubGatesRtn);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenMETroops")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewBuildScreenMETroops);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenMERuins")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewBuildScreenMERuins);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenMESiege")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewBuildScreenMESiege);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenMEBedouin")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewBuildScreenMEBEdouin);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenMEArabTroops")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewBuildScreenMEArabTroops);
			return true;
		}
		if (eventName == "Click" && handlerName == "BuildScreenAllies")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(BuildScreenAllies);
			return true;
		}
		if (eventName == "Click" && handlerName == "BuildScreenMerit")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(BuildScreenMerit);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewMEScreenTerrain")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewMEScreenTerrain);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewMEScreenAnimals")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewMEScreenAnimals);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewMEScreenTexture")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewMEScreenTexture);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewMEScreenWater")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewMEScreenWater);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewMEScreenVegetation")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewMEScreenVegetation);
			return true;
		}
		if (eventName == "Click" && handlerName == "NewMEScreenRocks")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(NewMEScreenRocks);
			return true;
		}
		if (eventName == "Click" && handlerName == "METoggleToBuildings")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(METoggleToBuildings);
			return true;
		}
		if (eventName == "Click" && handlerName == "METoggleToTerrain")
		{
			((ButtonBase)(RadioButton)source).Click += new RoutedEventHandler(METoggleToTerrain);
			return true;
		}
		if (eventName == "Click" && handlerName == "CycleMEDrawingSize")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(CycleMEDrawingSize);
			return true;
		}
		if (eventName == "Click" && handlerName == "CycleMERuler")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(CycleMERuler);
			return true;
		}
		if (eventName == "Click" && handlerName == "EnterMEDeleteMode")
		{
			((ButtonBase)(Button)source).Click += new RoutedEventHandler(EnterMEDeleteMode);
			return true;
		}
		if (eventName == "Loaded" && handlerName == "OnLoadMainUIGrid")
		{
			((FrameworkElement)(Rectangle)source).Loaded += new RoutedEventHandler(OnLoadMainUIGrid);
			return true;
		}
		if (eventName == "Unloaded" && handlerName == "OnUnLoadMainUIGrid")
		{
			((FrameworkElement)(Rectangle)source).Unloaded += new RoutedEventHandler(OnUnLoadMainUIGrid);
			return true;
		}
		if (eventName == "MouseEnter" && handlerName == "MouseEnterBuildingIconHandler")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseEnter += new MouseEventHandler(MouseEnterBuildingIconHandler);
			}
			else if (source is RadioButton)
			{
				((UIElement)(RadioButton)source).MouseEnter += new MouseEventHandler(MouseEnterBuildingIconHandler);
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "MouseLeaveBuildingIconHandler")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseLeave += new MouseEventHandler(MouseLeaveBuildingIconHandler);
			}
			else if (source is RadioButton)
			{
				((UIElement)(RadioButton)source).MouseLeave += new MouseEventHandler(MouseLeaveBuildingIconHandler);
			}
			return true;
		}
		return false;
	}

	public void SetEnginePanelText(int group, int text, bool force = false)
	{
		if (lastPTBgroup != group || lastPTBtext != text || force)
		{
			lastPTBgroup = group;
			lastPTBtext = text;
			if (group == 0)
			{
				OtherString = "";
				OtherData1 = 0;
				OtherData2 = 0;
				OtherVisible = false;
			}
			else
			{
				OtherString = Translate.Instance.lookUpText((Enums.eTextSections)group, text);
				OtherData1 = 0;
				OtherData2 = 0;
				OtherHighestPri = true;
				OtherVisible = true;
			}
		}
	}

	public void SetRolloverOtherString(string message, int data1 = -1, int data2 = -1, string message2 = "")
	{
		OtherString = message;
		OtherString2 = message2;
		OtherData1 = data1;
		OtherData2 = data2;
		OtherHighestPri = false;
		OtherVisible = message.Length > 0;
	}

	public void SetRolloverSelected(int structType, string message)
	{
		if (structType == 0)
		{
			SelectedStruct = 0;
			SelectedString = message;
		}
		else if (SelectedStruct != structType)
		{
			SelectedStruct = structType;
			SelectedString = message;
		}
	}

	public void UpdateRollover()
	{
		int num = 0;
		string text = "";
		int wood = 0;
		int stone = 0;
		int iron = 0;
		int pitch = 0;
		int gold = 0;
		bool flag = false;
		bool flag2 = false;
		bool rolloverBuilding_MakeVis = false;
		if (HoverStruct != 0 || HoverString.Length > 0)
		{
			num = HoverStruct;
			text = HoverString;
			flag2 = CreateBuildingTooltip(HoverStruct);
		}
		else if (SelectedStruct != 0 || SelectedString.Length > 0)
		{
			num = SelectedStruct;
			text = SelectedString;
		}
		MainViewModel.Instance.RolloverBuilding_TooltipVis = flag2;
		MainViewModel.Instance.RolloverBuilding_MakeVis = false;
		if (!flag2)
		{
			MainViewModel.Instance.RolloverBuilding_ProducesImage = null;
			MainViewModel.Instance.RolloverBuilding_ProducesImage2 = null;
			MainViewModel.Instance.RolloverBuilding_ConsumesImage2 = null;
			MainViewModel.Instance.RolloverBuilding_ConsumesImage = null;
			lastBuildingTooltipType = -1;
		}
		if ((text == "" || (OtherVisible && OtherHighestPri && !flag2)) && OtherVisible && GameData.Instance.lastGameState != null)
		{
			num = 0;
			text = OtherString;
			MainViewModel.Instance.RollOverText2 = OtherString2;
			if (OtherData1 == 1)
			{
				wood = GameData.Instance.lastGameState.repair_wood_needed;
				stone = GameData.Instance.lastGameState.repair_stone_needed;
				if (wood > 0 || stone > 0)
				{
					flag = true;
				}
			}
			else if (OtherData1 == 2)
			{
				rolloverBuilding_MakeVis = true;
			}
		}
		MainViewModel.Instance.RolloverBuilding_MakeVis = rolloverBuilding_MakeVis;
		if (num != 0)
		{
			if ((!MainViewModel.Instance.IsMapEditorMode || GameData.Instance.game_type == 6) && GameData.Instance.lastGameState != null)
			{
				GameData.getStructureCosts(num, ref wood, ref stone, ref iron, ref pitch, ref gold);
				flag = true;
			}
			if ((uint)(num - 110) <= 3u)
			{
				flag = true;
				if (SelectedStruct == 0)
				{
					stone = -12345;
				}
				else
				{
					stone = GameData.Instance.lastGameState.bld_tiles_built;
					if (stone == 0)
					{
						stone = -123456;
					}
				}
			}
		}
		if (flag)
		{
			int num2 = 0;
			if (wood != 0)
			{
				int gotAmount = GameData.Instance.lastGameState.resources[2];
				SetRolloverData(wood, gotAmount, MainViewModel.Instance.GameSprites[442], num2);
				num2++;
			}
			if (stone != 0)
			{
				int gotAmount2 = GameData.Instance.lastGameState.resources[4];
				if (GameData.Instance.game_type == 6)
				{
					gotAmount2 = GameData.Instance.lastGameState.keep_storage[4];
				}
				SetRolloverData(stone, gotAmount2, MainViewModel.Instance.GameSprites[444], num2);
				num2++;
			}
			if (iron > 0)
			{
				int gotAmount3 = GameData.Instance.lastGameState.resources[6];
				SetRolloverData(iron, gotAmount3, MainViewModel.Instance.GameSprites[445], num2);
				num2++;
			}
			if (pitch > 0)
			{
				int gotAmount4 = GameData.Instance.lastGameState.resources[7];
				SetRolloverData(pitch, gotAmount4, MainViewModel.Instance.GameSprites[446], num2);
				num2++;
			}
			if (gold != 0)
			{
				int gotAmount5 = GameData.Instance.lastGameState.resources[15];
				if (GameData.Instance.game_type == 6)
				{
					gotAmount5 = GameData.Instance.lastGameState.keep_storage[15];
				}
				SetRolloverData(gold, gotAmount5, MainViewModel.Instance.GameSprites[453], num2);
				num2++;
			}
			if (num2 < 2)
			{
				MainViewModel.Instance.RollOverText_AmountGot2 = "  ";
				MainViewModel.Instance.RollOverText_AmountReq2 = "";
				MainViewModel.Instance.RollOverText_GoodsImage2 = null;
			}
			if (num2 < 1)
			{
				MainViewModel.Instance.RollOverText_AmountGot1 = "";
				MainViewModel.Instance.RollOverText_AmountReq1 = "";
				MainViewModel.Instance.RollOverText_GoodsImage1 = null;
			}
			if (text.Length > 0)
			{
				MainViewModel.Instance.RollOverText = "  " + text;
			}
			else
			{
				MainViewModel.Instance.RollOverText = "";
			}
		}
		else
		{
			MainViewModel.Instance.RollOverText_AmountGot1 = "";
			MainViewModel.Instance.RollOverText_AmountReq1 = "";
			MainViewModel.Instance.RollOverText_AmountReq2 = "";
			MainViewModel.Instance.RollOverText_GoodsImage1 = null;
			MainViewModel.Instance.RollOverText_GoodsImage2 = null;
			if (text.Length > 0)
			{
				MainViewModel.Instance.RollOverText = "  " + text;
				MainViewModel.Instance.RollOverText_AmountGot2 = "  ";
			}
			else
			{
				MainViewModel.Instance.RollOverText = "";
				MainViewModel.Instance.RollOverText_AmountGot2 = "";
			}
		}
	}

	public bool CreateBuildingTooltip(int buildingType)
	{
		if (buildingType == 122 || buildingType == 121)
		{
			return false;
		}
		if (!ConfigSettings.Settings_ShowBuildingTooltips)
		{
			return false;
		}
		if (lastBuildingTooltipType == buildingType)
		{
			return true;
		}
		lastBuildingTooltipType = buildingType;
		MainViewModel.Instance.RolloverBuilding_ProducesImage = null;
		MainViewModel.Instance.RolloverBuilding_ProducesImage2 = null;
		MainViewModel.Instance.RolloverBuilding_ConsumesImage2 = null;
		MainViewModel.Instance.RolloverBuilding_ConsumesImage = null;
		MainViewModel.Instance.RolloverBuilding_TooltipConsumesVis = false;
		MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = false;
		switch (buildingType)
		{
		case 3:
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[442];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_NEW_INVASION);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 22);
			MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true;
			return true;
		case 10:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_LOAD_MAPFILE);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 2);
			return true;
		case 20:
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[444];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_START_GOODS);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 23);
			MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true;
			return true;
		case 4:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_WOOD_PLANKS);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 24);
			return true;
		case 5:
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[445];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_NEW_EVENTS);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 25);
			MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true;
			return true;
		case 6:
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[446];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_EDIT);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 26);
			MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true;
			return true;
		case 26:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_LOAD_SCN);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 27);
			return true;
		case 7:
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[11];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_BOWS);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 28);
			MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true;
			return true;
		case 33:
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[10];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_GOLD);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 63);
			MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true;
			return true;
		case 32:
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[12];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_FRUIT);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 60);
			MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true;
			return true;
		case 30:
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[8];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_MEAT);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 61);
			MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true;
			return true;
		case 31:
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[4];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_ALE);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 62);
			MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true;
			return true;
		case 1:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_STARTDATE);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 33);
			return true;
		case 36:
			if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
			{
				MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 0);
				MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 3);
			}
			else
			{
				MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_EDIT_ACTIONS);
				MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 34);
			}
			return true;
		case 37:
			if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
			{
				MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 1);
				MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 4);
			}
			else
			{
				MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_EVENT_CONDITIONS);
				MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 34);
			}
			return true;
		case 38:
			if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
			{
				MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 2);
				MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 5);
			}
			else
			{
				MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_EVENT_ACTIONS);
				MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 64);
			}
			return true;
		case 23:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_PIKES);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 35);
			return true;
		case 27:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_MESSAGE);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 36);
			return true;
		case 70:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.BHELP_TEXT_WATERPOT);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 56);
			return true;
		case 62:
		case 63:
		case 91:
		case 92:
		case 93:
		case 94:
		case 95:
		case 97:
		case 98:
		{
			Enums.eTextValues index3 = Enums.eTextValues.FEEDBACK_NULL;
			switch (buildingType)
			{
			case 62:
				index3 = Enums.eTextValues.TEXT_SCN_XBOWMAN;
				break;
			case 91:
				index3 = Enums.eTextValues.BHELP_TEXT_CESSPIT;
				break;
			case 63:
				index3 = Enums.eTextValues.TEXT_SCN_SWORDSMAN;
				break;
			case 92:
				index3 = Enums.eTextValues.BHELP_TEXT_BURNING_STAKE;
				break;
			case 94:
				index3 = Enums.eTextValues.BHELP_TEXT_DUNGEON;
				break;
			case 95:
				index3 = Enums.eTextValues.BHELP_TEXT_STRETCHING_RACK;
				break;
			case 93:
				index3 = Enums.eTextValues.BHELP_TEXT_GIBBET;
				break;
			case 97:
				index3 = Enums.eTextValues.BHELP_TEXT_CHOPPING_BLOCK;
				break;
			case 98:
				index3 = Enums.eTextValues.TEXT_SCN_EVENT_CONDITION9;
				break;
			}
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, index3);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 37);
			return true;
		}
		case 117:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.BHELP_TEXT_HEADS_ON_SPIKES);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 38);
			return true;
		case 65:
		case 100:
		case 101:
		case 103:
		case 118:
		case 119:
		case 120:
		case 121:
		case 122:
		case 123:
		case 124:
		case 125:
		case 126:
		{
			Enums.eTextValues index = Enums.eTextValues.FEEDBACK_NULL;
			int index2 = 39;
			switch (buildingType)
			{
			case 65:
				index = Enums.eTextValues.TEXT_SCN_SPEARMAN;
				break;
			case 103:
				index = Enums.eTextValues.BHELP_TEXT_DANCING_BEAR;
				break;
			case 118:
				index = Enums.eTextValues.TEXT_SCN_ANY_OF_THESE;
				break;
			case 119:
				index = Enums.eTextValues.TEXT_SCN_EVENT_CONDITION1;
				break;
			case 120:
				index = Enums.eTextValues.TEXT_SCN_EVENT_CONDITION0;
				break;
			case 100:
				index = Enums.eTextValues.BHELP_TEXT_STATUE;
				break;
			case 101:
				index = Enums.eTextValues.BHELP_TEXT_SHRINE;
				break;
			case 122:
				index = Enums.eTextValues.BHELP_TEXT_POND_LARGE;
				break;
			case 121:
				index = Enums.eTextValues.BHELP_TEXT_POND;
				break;
			case 123:
				index = Enums.eTextValues.TEXT_SCN_MACEMEN;
				index2 = 40;
				break;
			case 124:
				index = Enums.eTextValues.TEXT_SCN_ARAB_ARCHER;
				index2 = 40;
				break;
			case 125:
				index = Enums.eTextValues.TEXT_SCN_SWORDSMEN;
				index2 = 40;
				break;
			case 126:
				index = Enums.eTextValues.TEXT_SCN_KNIGHTS;
				index2 = 40;
				break;
			}
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, index);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, index2);
			return true;
		}
		case 12:
		{
			MainViewModel.Instance.RolloverBuilding_ProducesImage2 = MainViewModel.Instance.GameSprites[17];
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[16];
			MainViewModel.Instance.RolloverBuilding_ConsumesImage = MainViewModel.Instance.GameSprites[442];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_PITCH);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 41);
			MainViewModel instance8 = MainViewModel.Instance;
			bool rolloverBuilding_TooltipConsumesVis = (MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true);
			instance8.RolloverBuilding_TooltipConsumesVis = rolloverBuilding_TooltipConsumesVis;
			return true;
		}
		case 14:
		{
			MainViewModel.Instance.RolloverBuilding_ProducesImage2 = MainViewModel.Instance.GameSprites[18];
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[19];
			MainViewModel.Instance.RolloverBuilding_ConsumesImage = MainViewModel.Instance.GameSprites[442];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_WHEAT);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 42);
			MainViewModel instance7 = MainViewModel.Instance;
			bool rolloverBuilding_TooltipConsumesVis = (MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true);
			instance7.RolloverBuilding_TooltipConsumesVis = rolloverBuilding_TooltipConsumesVis;
			return true;
		}
		case 13:
		{
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[20];
			MainViewModel.Instance.RolloverBuilding_ProducesImage2 = MainViewModel.Instance.GameSprites[21];
			MainViewModel.Instance.RolloverBuilding_ConsumesImage = MainViewModel.Instance.GameSprites[445];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_HOPS);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 43);
			MainViewModel instance6 = MainViewModel.Instance;
			bool rolloverBuilding_TooltipConsumesVis = (MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true);
			instance6.RolloverBuilding_TooltipConsumesVis = rolloverBuilding_TooltipConsumesVis;
			return true;
		}
		case 16:
		{
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[22];
			MainViewModel.Instance.RolloverBuilding_ConsumesImage = MainViewModel.Instance.GameSprites[254];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_IRON_INGOTS);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 44);
			MainViewModel instance5 = MainViewModel.Instance;
			bool rolloverBuilding_TooltipConsumesVis = (MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true);
			instance5.RolloverBuilding_TooltipConsumesVis = rolloverBuilding_TooltipConsumesVis;
			return true;
		}
		case 15:
		{
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[23];
			MainViewModel.Instance.RolloverBuilding_ConsumesImage = MainViewModel.Instance.GameSprites[445];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_STONE_BLOCKS);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 45);
			MainViewModel instance4 = MainViewModel.Instance;
			bool rolloverBuilding_TooltipConsumesVis = (MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true);
			instance4.RolloverBuilding_TooltipConsumesVis = rolloverBuilding_TooltipConsumesVis;
			return true;
		}
		case 19:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_INVASION);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 46);
			return true;
		case 17:
		{
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[9];
			MainViewModel.Instance.RolloverBuilding_ConsumesImage = MainViewModel.Instance.GameSprites[15];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_BREAD);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 47);
			MainViewModel instance3 = MainViewModel.Instance;
			bool rolloverBuilding_TooltipConsumesVis = (MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true);
			instance3.RolloverBuilding_TooltipConsumesVis = rolloverBuilding_TooltipConsumesVis;
			return true;
		}
		case 34:
		{
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[15];
			MainViewModel.Instance.RolloverBuilding_ConsumesImage = MainViewModel.Instance.GameSprites[8];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_EVENT);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 48);
			MainViewModel instance2 = MainViewModel.Instance;
			bool rolloverBuilding_TooltipConsumesVis = (MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true);
			instance2.RolloverBuilding_TooltipConsumesVis = rolloverBuilding_TooltipConsumesVis;
			return true;
		}
		case 18:
		{
			MainViewModel.Instance.RolloverBuilding_ProducesImage = MainViewModel.Instance.GameSprites[13];
			MainViewModel.Instance.RolloverBuilding_ConsumesImage = MainViewModel.Instance.GameSprites[4];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_CHEESE);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 49);
			MainViewModel instance = MainViewModel.Instance;
			bool rolloverBuilding_TooltipConsumesVis = (MainViewModel.Instance.RolloverBuilding_TooltipProducesVis = true);
			instance.RolloverBuilding_TooltipConsumesVis = rolloverBuilding_TooltipConsumesVis;
			return true;
		}
		case 22:
			MainViewModel.Instance.RolloverBuilding_ConsumesImage = MainViewModel.Instance.GameSprites[13];
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_SPEARS);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 50);
			MainViewModel.Instance.RolloverBuilding_TooltipConsumesVis = true;
			return true;
		case 24:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_MACES);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 18);
			return true;
		case 35:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_PIG);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 19);
			return true;
		case 25:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_SWORDS);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 20);
			return true;
		case 28:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_ACTION12);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 21);
			return true;
		case 47:
		case 127:
		case 128:
		case 129:
		case 130:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_MAX_TITLE_LENGTH);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 12);
			return true;
		case 46:
		case 131:
		case 132:
		case 205:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_PLAYBINK);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 12);
			return true;
		case 45:
		case 133:
		case 134:
		case 206:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_ARCHERS);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 12);
			return true;
		case 49:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_NEW);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 13);
			return true;
		case 99:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.BHELP_TEXT_DOG_CAGE);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 14);
			return true;
		case 68:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_EVENT_CONDITION34);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 15);
			return true;
		case 67:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_NEW_MESSAGE);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 16);
			return true;
		case 114:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_MOOD4);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 17);
			return true;
		case 110:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_MOOD3);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 3);
			return true;
		case 111:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_WOLF);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 4);
			return true;
		case 112:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_TAUNT);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 5);
			return true;
		case 9:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_RAT);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 6);
			return true;
		case 8:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_SNAKE);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 57);
			return true;
		case 108:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.BHELP_TEXT_BEDOUIN_STOCKADE);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 58);
			return true;
		case 11:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_CHARACTERS);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 7);
			return true;
		case 74:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_SAVE);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 8);
			return true;
		case 75:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_SAVEEXIT);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 9);
			return true;
		case 76:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_EVENTS);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 10);
			return true;
		case 77:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_CIVIL);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 11);
			return true;
		case 78:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_MILITARY);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 11);
			return true;
		case 40:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_NAME);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 1);
			return true;
		case 41:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_MAPFILE);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 1);
			return true;
		case 42:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_TITLE);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 1);
			return true;
		case 43:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_BRIEFING);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 1);
			return true;
		case 44:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_CANCEL);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 1);
			return true;
		case 115:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_ACTION6);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 52);
			return true;
		case 116:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_EVENT_CONDITION27);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 51);
			return true;
		case 113:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_ANGER);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 53);
			return true;
		case 168:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_LOAD);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 54);
			return true;
		case 169:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_XBOWMEN);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 55);
			return true;
		case 106:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_ACTION14);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 59);
			return true;
		case 107:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_ACTION13);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 59);
			return true;
		case 2:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_SELECT_MESSAGE);
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 59);
			return true;
		case 148:
		case 149:
		case 150:
		case 151:
		case 152:
		case 153:
		case 154:
		case 155:
		case 156:
		case 157:
		case 158:
		case 159:
		case 160:
		case 161:
		case 162:
		case 163:
		case 164:
		case 220:
		case 221:
		case 222:
		case 223:
		case 224:
		case 225:
		case 226:
		case 227:
		case 247:
		case 248:
		case 249:
		case 250:
		case 251:
		case 252:
		case 253:
		case 254:
			MainViewModel.Instance.RolloverBuilding_TooltipTitle = HoverString;
			MainViewModel.Instance.RolloverBuilding_TooltipBody = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 332);
			return true;
		default:
			lastBuildingTooltipType = -1;
			return false;
		}
	}

	public void MouseEnterBuildingIconHandler(object sender, MouseEventArgs e)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		if (((RoutedEventArgs)e).Source is Button)
		{
			if (((ButtonBase)(Button)((RoutedEventArgs)e).Source).CommandParameter != null && ((ButtonBase)(Button)((RoutedEventArgs)e).Source).CommandParameter is string)
			{
				num = MainViewModel.Instance.getStructEnum((string)((ButtonBase)(Button)((RoutedEventArgs)e).Source).CommandParameter);
				if (num == 0)
				{
					num = MainViewModel.Instance.getStructEnumExtra((string)((ButtonBase)(Button)((RoutedEventArgs)e).Source).CommandParameter);
				}
				if (num != 107)
				{
					switch ((string)((ButtonBase)(Button)((RoutedEventArgs)e).Source).CommandParameter)
					{
					case "STRUCT_SUB_MENU_TOWERS":
					case "STRUCT_SUB_MENU_MILITARY":
					case "STRUCT_SUB_MENU_GATEHOUSES":
					case "STRUCT_SUB_MENU_KEEPS":
					case "STRUCT_SUB_MENU_GOOD":
					case "STRUCT_SUB_MENU_BAD":
						SFXManager.instance.playUISound(289);
						break;
					default:
					{
						DateTime utcNow = DateTime.UtcNow;
						if (utcNow > lastBuildingRollover)
						{
							lastBuildingRollover = utcNow.AddMilliseconds(200.0);
							SFXManager.instance.playUISound(136);
						}
						break;
					}
					}
				}
				else
				{
					SFXManager.instance.playUISound(131);
				}
			}
			if (((FrameworkElement)(Button)((RoutedEventArgs)e).Source).Tag != null && ((FrameworkElement)(Button)((RoutedEventArgs)e).Source).Tag is string)
			{
				HoverString = (string)((FrameworkElement)(Button)((RoutedEventArgs)e).Source).Tag;
				switch (((FrameworkElement)(Button)((RoutedEventArgs)e).Source).Name)
				{
				case "GameInfoButton":
				case "GameKeyButton":
				case "GameUndoButton":
					SFXManager.instance.playUISound(131);
					break;
				}
			}
			else
			{
				HoverString = "";
			}
		}
		else if (((RoutedEventArgs)e).Source is RadioButton)
		{
			if (((ButtonBase)(RadioButton)((RoutedEventArgs)e).Source).CommandParameter != null && ((ButtonBase)(RadioButton)((RoutedEventArgs)e).Source).CommandParameter is string)
			{
				num = MainViewModel.Instance.getStructEnum((string)((ButtonBase)(RadioButton)((RoutedEventArgs)e).Source).CommandParameter);
				if (num == 0)
				{
					num = MainViewModel.Instance.getStructEnumExtra((string)((ButtonBase)(RadioButton)((RoutedEventArgs)e).Source).CommandParameter);
				}
			}
			if (((FrameworkElement)(RadioButton)((RoutedEventArgs)e).Source).Tag != null && ((FrameworkElement)(RadioButton)((RoutedEventArgs)e).Source).Tag is string)
			{
				HoverString = (string)((FrameworkElement)(RadioButton)((RoutedEventArgs)e).Source).Tag;
				switch (((FrameworkElement)(RadioButton)((RoutedEventArgs)e).Source).Name)
				{
				case "TabBuildCastle":
					SFXManager.instance.playUISound(129);
					break;
				case "TabBuildIndustry":
					SFXManager.instance.playUISound(130);
					break;
				case "TabBuildFarms":
					SFXManager.instance.playUISound(131);
					break;
				case "TabBuildTown":
					SFXManager.instance.playUISound(132);
					break;
				case "TabBuildWeapons":
					SFXManager.instance.playUISound(133);
					break;
				case "TabBuildFood":
					SFXManager.instance.playUISound(134);
					break;
				case "RadioButtonMETroops":
					SFXManager.instance.playUISound(129);
					break;
				case "RadioButtonMERuins":
					SFXManager.instance.playUISound(130);
					break;
				}
			}
			else
			{
				HoverString = "";
			}
		}
		HoverStruct = num;
	}

	public void SetRolloverData(int amountNeeded, int gotAmount, ImageSource image, int column)
	{
		if (column == 0)
		{
			if (amountNeeded == -123456)
			{
				amountNeeded = 0;
			}
			if (amountNeeded != -12345)
			{
				MainViewModel.Instance.RollOverText_AmountReq1 = "   " + amountNeeded + " ";
			}
			else
			{
				MainViewModel.Instance.RollOverText_AmountReq1 = "   ";
			}
			MainViewModel.Instance.RollOverText_AmountGot1 = "(" + gotAmount + ")";
			MainViewModel.Instance.RollOverText_GoodsImage1 = image;
		}
		if (column == 1)
		{
			MainViewModel.Instance.RollOverText_AmountReq2 = "   " + amountNeeded + " ";
			MainViewModel.Instance.RollOverText_AmountGot2 = "(" + gotAmount + ")  ";
			MainViewModel.Instance.RollOverText_GoodsImage2 = image;
		}
	}

	public void MouseLeaveBuildingIconHandler(object sender, MouseEventArgs e)
	{
		HoverStruct = 0;
		HoverString = "";
	}

	public void InGameOptions(object sender, RoutedEventArgs e)
	{
		if (!MainViewModel.Instance.Show_HUD_IngameMenu)
		{
			if (!GameData.scenario.InGameoverSituation)
			{
				if ((BaseComponent)(object)MainViewModel.Instance.HUDScenario != (BaseComponent)null && MainViewModel.Instance.ShowingScenario)
				{
					((UIElement)MainViewModel.Instance.HUDScenario).IsEnabled = false;
					((UIElement)MainViewModel.Instance.HUDScenarioPopup).IsEnabled = false;
				}
				MainViewModel.Instance.Show_HUD_LoadSaveRequester = false;
				MainViewModel.Instance.HUD_Markers_Vis = false;
				MainViewModel.Instance.AlliesPanelVisible = false;
				MainViewModel.Instance.MeritPanelVisible = false;
				MainViewModel.Instance.HUDIngameMenu.Init();
				MainViewModel.Instance.Show_HUD_IngameMenu = true;
			}
		}
		else
		{
			MainViewModel.Instance.HUDIngameMenu.Close();
		}
	}

	public void UndoLastAction(object sender, RoutedEventArgs e)
	{
		EngineInterface.GameAction(Enums.GameActionCommand.Undo, 0, 0);
	}

	public void ReturnToBriefingScreen(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.GoToScreen(Enums.SceneIDS.FrontEnd);
	}

	public void NewBuildScreenKeeps(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 13;
		if (SetupNewBuildScreen(0))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenKeeps()
	{
		MainViewModel.Instance.SubMode = 13;
		if (SetupNewBuildScreen(-1000))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenCastle(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 10;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(1))
		{
			StartScrollSwish();
			SFXManager.instance.playUISound(123);
		}
	}

	public void NewBuildScreenCastle(bool force = false)
	{
		MainViewModel.Instance.SubMode = 10;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (!force)
		{
			if (!SetupNewBuildScreen(1))
			{
				return;
			}
		}
		else if (!SetupNewBuildScreen(-1))
		{
			return;
		}
		StartScrollSwish();
	}

	public void NewBuildScreenIndustry(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 20;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(2))
		{
			StartScrollSwish();
			SFXManager.instance.playUISound(124);
		}
	}

	public void NewBuildScreenIndustry(bool force = false)
	{
		MainViewModel.Instance.SubMode = 20;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (!force)
		{
			if (!SetupNewBuildScreen(2))
			{
				return;
			}
		}
		else if (!SetupNewBuildScreen(-2))
		{
			return;
		}
		StartScrollSwish();
	}

	public void NewBuildScreenFarms(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 40;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(3))
		{
			StartScrollSwish();
			SFXManager.instance.playUISound(127);
		}
	}

	public void NewBuildScreenTown(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 30;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(4))
		{
			StartScrollSwish();
			SFXManager.instance.playUISound(125);
		}
	}

	public void NewBuildScreenWeapons(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 28;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(5))
		{
			StartScrollSwish();
			SFXManager.instance.playUISound(128);
		}
	}

	public void NewBuildScreenFood(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 25;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(6))
		{
			StartScrollSwish();
			SFXManager.instance.playUISound(126);
		}
	}

	public void NewBuildScreenFood(bool updateAppMode = true)
	{
		if (updateAppMode)
		{
			MainViewModel.Instance.SubMode = 25;
			EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		}
		if (SetupNewBuildScreen(6))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenTowers(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 11;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(7))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenGates(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 12;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(8))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenMilitaryBuildings(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 14;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(9))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenBadStuff(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 33;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(10))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenGoodStuff(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 34;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(11))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenSubTowersRtn(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 10;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(1))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenSubTownRtn(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 30;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(4))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenSubSubSmallGates(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 18;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(13))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenSubSubLargeGates(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 19;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(14))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenSubGatesRtn(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 12;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(8))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenMERuins(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 27;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(15))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenToggleRuins1()
	{
		MainViewModel.Instance.SubMode = 27;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		int newScreenID = 17;
		if (SetupNewBuildScreen(newScreenID))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenToggleRuins2()
	{
		MainViewModel.Instance.SubMode = 27;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		int newScreenID = 18;
		if (SetupNewBuildScreen(newScreenID))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenToggleRuins3()
	{
		MainViewModel.Instance.SubMode = 27;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		int newScreenID = 21;
		if (SetupNewBuildScreen(newScreenID))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenToggleRuins4()
	{
		MainViewModel.Instance.SubMode = 27;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		int newScreenID = 15;
		if (SetupNewBuildScreen(newScreenID))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenMETroops(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 26;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(16))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenMESiege(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 50;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(19))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenMEArabTroops(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 29;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(12))
		{
			StartScrollSwish();
		}
	}

	public void NewBuildScreenMEBEdouin(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.SubMode = 51;
		EditorDirector.instance.reportSubModeTabChange(MainViewModel.Instance.SubMode);
		if (SetupNewBuildScreen(20))
		{
			StartScrollSwish();
		}
	}

	public void BuildScreenAllies(object sender, RoutedEventArgs e)
	{
		HUD_AlliesPanel.Open(!MainViewModel.Instance.AlliesPanelVisible);
	}

	public void BuildScreenMerit(object sender, RoutedEventArgs e)
	{
		HUD_MeritPanel.Open(!MainViewModel.Instance.MeritPanelVisible);
	}

	public void NewBuildScreenBlank()
	{
		MainViewModel.Instance.SubMode = 48;
		SetupNewBuildScreen(22);
	}

	public void NewMEScreenTerrain(object sender, RoutedEventArgs e)
	{
		SetupNewMEScreen(1);
	}

	public void NewMEScreenAnimals(object sender, RoutedEventArgs e)
	{
		SetupNewMEScreen(2);
	}

	public void NewMEScreenTexture(object sender, RoutedEventArgs e)
	{
		SetupNewMEScreen(3);
	}

	public void NewMEScreenWater(object sender, RoutedEventArgs e)
	{
		SetupNewMEScreen(4);
	}

	public void NewMEScreenVegetation(object sender, RoutedEventArgs e)
	{
		SetupNewMEScreen(5);
	}

	public void NewMEScreenRocks(object sender, RoutedEventArgs e)
	{
		SetupNewMEScreen(6);
	}

	public void METoggleToBuildings(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.MEMode = 1;
		MainViewModel.Instance.RadarMargin = "0,0,103,8";
		MainViewModel.Instance.RadarPlusMargin = "0,0,94,135";
		MainViewModel.Instance.RadarMinusMargin = "0,0,94,125";
		SetupModeDependantUI();
	}

	public void METoggleToTerrain(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.MEMode = 0;
		MainViewModel.Instance.RadarMargin = "0,0,10,6";
		MainViewModel.Instance.RadarPlusMargin = "0,0,-1,135";
		MainViewModel.Instance.RadarMinusMargin = "0,0,-1,125";
		SetupModeDependantUI();
	}

	public void CycleMEDrawingSize(object sender, RoutedEventArgs e)
	{
		if (++MainViewModel.Instance.MEBrushSize >= 9)
		{
			MainViewModel.Instance.MEBrushSize = 0;
		}
		SetupMEDrawingBrush(MainViewModel.Instance.MEBrushSize);
		MainViewModel.Instance.MENewBrushSize();
	}

	public void CycleMEDrawingSizeSmaller()
	{
		if (--MainViewModel.Instance.MEBrushSize < 0)
		{
			MainViewModel.Instance.MEBrushSize = 8;
		}
		SetupMEDrawingBrush(MainViewModel.Instance.MEBrushSize);
		MainViewModel.Instance.MENewBrushSize(rightClick: true);
	}

	public void UpdateMEDrawingSize(int newSize)
	{
		MainViewModel.Instance.MEBrushSize = newSize;
		SetupMEDrawingBrush(MainViewModel.Instance.MEBrushSize);
	}

	public void CycleMERuler(object sender, RoutedEventArgs e)
	{
		if (++MainViewModel.Instance.MERulerMode >= 3)
		{
			MainViewModel.Instance.MERulerMode = 0;
		}
		SetupMERuler(MainViewModel.Instance.MERulerMode);
	}

	public void CycleMERulerBack()
	{
		if (--MainViewModel.Instance.MERulerMode < 0)
		{
			MainViewModel.Instance.MERulerMode = 2;
		}
		SetupMERuler(MainViewModel.Instance.MERulerMode);
	}

	public void EnterMEDeleteMode(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.MEDeleteMode = true;
	}

	public void StartScrollSwish()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		if (scrollPosition == 0)
		{
			scrollPosition = 1;
			((Storyboard)((FrameworkElement)this).Resources[(object)"Swish2"]).Begin((FrameworkElement)(object)this);
		}
		else
		{
			scrollPosition = 0;
			((Storyboard)((FrameworkElement)this).Resources[(object)"Swish1"]).Begin((FrameworkElement)(object)this);
		}
		SFXManager.instance.playUISound(103);
	}

	public void findUIlowerPoint()
	{
	}

	public void SetupModeDependantUI()
	{
		MainViewModel.Instance.HUD_Markers_Vis = false;
		if (MainViewModel.Instance.IsMapEditorMode)
		{
			MainViewModel.Instance.SkirmishModeVisAlly = false;
			MainViewModel.Instance.SkirmishModeVisMerit = false;
			((UIElement)RefMapEditorModeToggle).IsEnabled = true;
			((UIElement)RefMapEditorModeToggle).Visibility = (Visibility)2;
			if (!GameData.Instance.multiplayerMap)
			{
				((UIElement)RefButtonMERocksSignpost).Visibility = (Visibility)2;
			}
			else
			{
				((UIElement)RefButtonMERocksSignpost).Visibility = (Visibility)1;
			}
			if (MainViewModel.Instance.MEMode == 0)
			{
				MainViewModel.Instance.RollOverText_Margin = "0,0,-20,154";
				((UIElement)RefFrameBuildings).Visibility = (Visibility)1;
				((UIElement)RefFrameTerrain).Visibility = (Visibility)2;
				((UIElement)RefBuildMenuGrid).Visibility = (Visibility)1;
				((UIElement)RefMEMenuGrid).Visibility = (Visibility)2;
				((UIElement)RefBottomTabs1).Visibility = (Visibility)1;
				((UIElement)RefBottomTabs2a).Visibility = (Visibility)1;
				((UIElement)RefBottomTabs2b).Visibility = (Visibility)1;
				((UIElement)RefBottomTabs2c).Visibility = (Visibility)1;
				((UIElement)RefBottomTabs2d).Visibility = (Visibility)1;
				((UIElement)RefBottomTabs2e).Visibility = (Visibility)1;
				((UIElement)RefSideTabs).Visibility = (Visibility)1;
				((UIElement)RefMapEditorSheilds).Visibility = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.RollOverText_Margin = "0,0,-20,130";
				((UIElement)RefFrameBuildings).Visibility = (Visibility)2;
				((UIElement)RefFrameTerrain).Visibility = (Visibility)1;
				((UIElement)RefBuildMenuGrid).Visibility = (Visibility)2;
				((UIElement)RefMEMenuGrid).Visibility = (Visibility)1;
				((UIElement)RefBottomTabs1).Visibility = (Visibility)2;
				((UIElement)RefBottomTabs2a).Visibility = (Visibility)2;
				((UIElement)RefBottomTabs2b).Visibility = (Visibility)2;
				((UIElement)RefBottomTabs2c).Visibility = (Visibility)2;
				((UIElement)RefBottomTabs2d).Visibility = (Visibility)2;
				((UIElement)RefBottomTabs2e).Visibility = (Visibility)2;
				((UIElement)RefSideTabs).Visibility = (Visibility)2;
				((UIElement)RefMapEditorSheilds).Visibility = (Visibility)2;
			}
		}
		else
		{
			MainViewModel.Instance.SkirmishModeVisAlly = (Director.instance.SkirmishModeGame || Director.instance.MultiplayerGame) && GameData.Instance.game_type != 0;
			MainViewModel.Instance.SkirmishModeVisMerit = Director.instance.SkirmishModeGame || Director.instance.MultiplayerGame;
			((UIElement)RefMapEditorModeToggle).IsEnabled = false;
			((UIElement)RefMapEditorModeToggle).Visibility = (Visibility)1;
			((UIElement)buildButtons[380]).IsEnabled = false;
			MainViewModel.Instance.RollOverText_Margin = "0,0,-20,130";
			((UIElement)RefFrameBuildings).Visibility = (Visibility)2;
			((UIElement)RefFrameTerrain).Visibility = (Visibility)1;
			((UIElement)RefBuildMenuGrid).Visibility = (Visibility)2;
			((UIElement)RefMEMenuGrid).Visibility = (Visibility)1;
			((UIElement)RefBottomTabs1).Visibility = (Visibility)2;
			((UIElement)RefBottomTabs2a).Visibility = (Visibility)1;
			((UIElement)RefBottomTabs2b).Visibility = (Visibility)1;
			((UIElement)RefBottomTabs2c).Visibility = (Visibility)1;
			((UIElement)RefBottomTabs2d).Visibility = (Visibility)1;
			((UIElement)RefBottomTabs2e).Visibility = (Visibility)1;
			((UIElement)RefSideTabs).Visibility = (Visibility)2;
			((UIElement)RefMapEditorSheilds).Visibility = (Visibility)1;
		}
		SetupMEDrawingBrush(MainViewModel.Instance.MEBrushSize);
		SetupMERuler(MainViewModel.Instance.MERulerMode);
	}

	public bool RefreshBuildScreen()
	{
		return SetupNewBuildScreen(-MainViewModel.Instance.buildScreenID);
	}

	public bool SetupNewBuildScreen(int newScreenID)
	{
		//IL_0435: Unknown result type (might be due to invalid IL or missing references)
		//IL_043a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0444: Unknown result type (might be due to invalid IL or missing references)
		SetupModeDependantUI();
		if (MainViewModel.Instance.buildScreenID == newScreenID)
		{
			return false;
		}
		bool flag = false;
		if (newScreenID < 0)
		{
			flag = true;
			newScreenID = ((newScreenID != -1000) ? (-newScreenID) : 0);
		}
		MainViewModel.Instance.buildScreenID = newScreenID;
		for (int i = 0; i < 23; i++)
		{
			for (int j = 0; j < 17; j++)
			{
				int num = BuildIconLists[i, j];
				if (num <= 0 || (BaseComponent)(object)buildButtons[num] == (BaseComponent)null)
				{
					break;
				}
				switch (num)
				{
				case 87:
					if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
					{
						((UIElement)buildButtons[num]).IsEnabled = false;
						if (flag)
						{
							((UIElement)buildButtons[num]).Visibility = (Visibility)1;
						}
						num = 583;
					}
					else
					{
						((UIElement)buildButtons[583]).IsEnabled = false;
						if (flag)
						{
							((UIElement)buildButtons[583]).Visibility = (Visibility)1;
						}
					}
					break;
				case 88:
					if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
					{
						((UIElement)buildButtons[num]).IsEnabled = false;
						if (flag)
						{
							((UIElement)buildButtons[num]).Visibility = (Visibility)1;
						}
						num = 584;
					}
					else
					{
						((UIElement)buildButtons[584]).IsEnabled = false;
						if (flag)
						{
							((UIElement)buildButtons[584]).Visibility = (Visibility)1;
						}
					}
					break;
				case 89:
					if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
					{
						((UIElement)buildButtons[num]).IsEnabled = false;
						if (flag)
						{
							((UIElement)buildButtons[num]).Visibility = (Visibility)1;
						}
						num = 585;
					}
					else
					{
						((UIElement)buildButtons[585]).IsEnabled = false;
						if (flag)
						{
							((UIElement)buildButtons[585]).Visibility = (Visibility)1;
						}
					}
					break;
				case 417:
					if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
					{
						((UIElement)buildButtons[num]).IsEnabled = false;
						if (flag)
						{
							((UIElement)buildButtons[num]).Visibility = (Visibility)1;
						}
						num = 586;
					}
					else
					{
						((UIElement)buildButtons[586]).IsEnabled = false;
						if (flag)
						{
							((UIElement)buildButtons[586]).Visibility = (Visibility)1;
						}
					}
					break;
				case 172:
					if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
					{
						((UIElement)buildButtons[num]).IsEnabled = false;
						if (flag)
						{
							((UIElement)buildButtons[num]).Visibility = (Visibility)1;
						}
						num = 587;
					}
					else
					{
						((UIElement)buildButtons[587]).IsEnabled = false;
						if (flag)
						{
							((UIElement)buildButtons[587]).Visibility = (Visibility)1;
						}
					}
					break;
				}
				if (i == newScreenID)
				{
					((UIElement)buildButtons[num]).IsEnabled = MainViewModel.Instance.CanPlaceMapper(((ButtonBase)buildButtons[num]).CommandParameter);
					bool replaceMargin = false;
					Thickness margin = getMargin((string)((ButtonBase)buildButtons[num]).CommandParameter, ref replaceMargin);
					((FrameworkElement)buildButtons[num]).Margin = margin;
				}
				else
				{
					((UIElement)buildButtons[num]).IsEnabled = false;
				}
				if (flag)
				{
					((UIElement)buildButtons[num]).Visibility = (Visibility)1;
				}
			}
		}
		if (((UIElement)buildButtons[80]).IsEnabled)
		{
			if (MainViewModel.Instance.IsMapEditorMode)
			{
				((UIElement)buildButtons[80]).IsEnabled = true;
			}
			else
			{
				((UIElement)buildButtons[80]).IsEnabled = false;
			}
		}
		if (MainViewModel.Instance.FreezeMainControls)
		{
			((UIElement)buildButtons[15]).IsEnabled = false;
			((UIElement)buildButtons[23]).IsEnabled = false;
			((UIElement)buildButtons[16]).IsEnabled = false;
			((UIElement)buildButtons[33]).IsEnabled = false;
		}
		return true;
	}

	public void SetupNewMEScreen(int newScreenID, bool ignoreSetupCall = false)
	{
		if (!ignoreSetupCall)
		{
			SetupModeDependantUI();
		}
		MainViewModel.Instance.MEScreenID = newScreenID;
		((UIElement)RefMETerrainMenu).Visibility = (Visibility)1;
		((UIElement)RefMEAnimalsMenu).Visibility = (Visibility)1;
		((UIElement)RefMETextureMenu).Visibility = (Visibility)1;
		((UIElement)RefMEWaterMenu).Visibility = (Visibility)1;
		((UIElement)RefMEVegetationMenu).Visibility = (Visibility)1;
		((UIElement)RefMERocksMenu).Visibility = (Visibility)1;
		switch (newScreenID)
		{
		case 1:
			((UIElement)RefMETerrainMenu).Visibility = (Visibility)2;
			break;
		case 2:
			((UIElement)RefMEAnimalsMenu).Visibility = (Visibility)2;
			break;
		case 3:
			((UIElement)RefMETextureMenu).Visibility = (Visibility)2;
			break;
		case 4:
			((UIElement)RefMEWaterMenu).Visibility = (Visibility)2;
			break;
		case 5:
			((UIElement)RefMEVegetationMenu).Visibility = (Visibility)2;
			break;
		case 6:
			((UIElement)RefMERocksMenu).Visibility = (Visibility)2;
			break;
		}
	}

	public void SetupMEDrawingBrush(int newSize)
	{
		for (int i = 0; i < 9; i++)
		{
			if (i == newSize)
			{
				((UIElement)MEBrushSizeButtons[i]).Visibility = (Visibility)2;
			}
			else
			{
				((UIElement)MEBrushSizeButtons[i]).Visibility = (Visibility)1;
			}
		}
	}

	public void SetupMERuler(int newMode)
	{
		for (int i = 0; i < 3; i++)
		{
			if (i == newMode)
			{
				((UIElement)MERulerButtons[i]).Visibility = (Visibility)2;
			}
			else
			{
				((UIElement)MERulerButtons[i]).Visibility = (Visibility)1;
			}
		}
	}

	public void OnLoadMainUIGrid(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.MainUILoaded = true;
	}

	public void OnUnLoadMainUIGrid(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.MainUILoaded = false;
	}

	public void ResetTutorialArrows()
	{
		((UIElement)RefTutorialArrow1).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow2).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow3).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow4).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow5).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow6).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow7).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow8).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow9).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow10).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow11).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow12).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow13).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow14).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow15).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow16).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow17).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow18).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow19).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow20).Visibility = (Visibility)1;
		((UIElement)RefTutorialArrow21).Visibility = (Visibility)1;
		currentTutorialArrow = -1;
	}

	public void ShowTutorialArrow(int arrowID, bool state)
	{
		ResetTutorialArrows();
		if (state)
		{
			currentTutorialArrow = arrowID;
		}
	}

	public void monitorTutorialArrows()
	{
		Image val = null;
		switch (currentTutorialArrow)
		{
		case 1:
			if (((UIElement)buildButtons[81]).IsVisible)
			{
				((UIElement)RefTutorialArrow1).Visibility = (Visibility)2;
				val = RefTutorialArrow1;
			}
			else
			{
				((UIElement)RefTutorialArrow1).Visibility = (Visibility)1;
			}
			break;
		case 2:
			if (((UIElement)buildButtons[4]).IsVisible)
			{
				((UIElement)RefTutorialArrow2).Visibility = (Visibility)2;
				val = RefTutorialArrow2;
			}
			else
			{
				((UIElement)RefTutorialArrow2).Visibility = (Visibility)1;
			}
			break;
		case 3:
			if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 4)
			{
				((UIElement)RefTutorialArrow3).Visibility = (Visibility)2;
				val = RefTutorialArrow3;
			}
			else
			{
				((UIElement)RefTutorialArrow3).Visibility = (Visibility)1;
			}
			break;
		case 4:
			if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 2)
			{
				((UIElement)RefTutorialArrow4).Visibility = (Visibility)2;
				val = RefTutorialArrow4;
			}
			else
			{
				((UIElement)RefTutorialArrow4).Visibility = (Visibility)1;
			}
			break;
		case 5:
			((UIElement)RefTutorialArrow5).Visibility = (Visibility)2;
			val = RefTutorialArrow5;
			break;
		case 6:
			((UIElement)RefTutorialArrow6).Visibility = (Visibility)2;
			val = RefTutorialArrow6;
			break;
		case 7:
			((UIElement)RefTutorialArrow7).Visibility = (Visibility)2;
			val = RefTutorialArrow7;
			break;
		case 8:
			if (GameData.Instance.lastGameState.app_sub_mode != 20)
			{
				((UIElement)RefTutorialArrow8).Visibility = (Visibility)2;
				val = RefTutorialArrow8;
				((UIElement)RefTutorialArrow9).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefTutorialArrow9).Visibility = (Visibility)2;
				val = RefTutorialArrow9;
				((UIElement)RefTutorialArrow8).Visibility = (Visibility)1;
			}
			break;
		case 10:
			if (GameData.Instance.lastGameState.app_sub_mode != 40)
			{
				((UIElement)RefTutorialArrow10).Visibility = (Visibility)2;
				val = RefTutorialArrow10;
				((UIElement)RefTutorialArrow11).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefTutorialArrow11).Visibility = (Visibility)2;
				val = RefTutorialArrow11;
				((UIElement)RefTutorialArrow10).Visibility = (Visibility)1;
			}
			break;
		case 12:
			if (GameData.Instance.lastGameState.app_sub_mode != 30)
			{
				((UIElement)RefTutorialArrow12).Visibility = (Visibility)2;
				val = RefTutorialArrow12;
				((UIElement)RefTutorialArrow13).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefTutorialArrow13).Visibility = (Visibility)2;
				val = RefTutorialArrow13;
				((UIElement)RefTutorialArrow12).Visibility = (Visibility)1;
			}
			break;
		case 13:
			if (GameData.Instance.lastGameState.app_sub_mode != 28)
			{
				((UIElement)RefTutorialArrow14).Visibility = (Visibility)2;
				val = RefTutorialArrow14;
				((UIElement)RefTutorialArrow15).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefTutorialArrow15).Visibility = (Visibility)2;
				val = RefTutorialArrow15;
				((UIElement)RefTutorialArrow14).Visibility = (Visibility)1;
			}
			break;
		case 14:
			if (GameData.Instance.lastGameState.app_sub_mode != 10)
			{
				((UIElement)RefTutorialArrow16).Visibility = (Visibility)2;
				val = RefTutorialArrow16;
				((UIElement)RefTutorialArrow17).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefTutorialArrow17).Visibility = (Visibility)2;
				val = RefTutorialArrow17;
				((UIElement)RefTutorialArrow16).Visibility = (Visibility)1;
			}
			break;
		case 15:
			if (GameData.Instance.lastGameState.app_sub_mode != 10)
			{
				((UIElement)RefTutorialArrow16).Visibility = (Visibility)2;
				val = RefTutorialArrow16;
				((UIElement)RefTutorialArrow18).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefTutorialArrow18).Visibility = (Visibility)2;
				val = RefTutorialArrow18;
				((UIElement)RefTutorialArrow16).Visibility = (Visibility)1;
			}
			break;
		case 16:
			if (GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 1)
			{
				((UIElement)RefTutorialArrow19).Visibility = (Visibility)2;
				val = RefTutorialArrow19;
			}
			break;
		case 17:
			if (GameData.Instance.lastGameState.app_sub_mode != 10)
			{
				((UIElement)RefTutorialArrow16).Visibility = (Visibility)2;
				val = RefTutorialArrow16;
				((UIElement)RefTutorialArrow20).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)RefTutorialArrow20).Visibility = (Visibility)2;
				val = RefTutorialArrow20;
				((UIElement)RefTutorialArrow16).Visibility = (Visibility)1;
			}
			break;
		case 18:
			if (GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 44)
			{
				((UIElement)RefTutorialArrow21).Visibility = (Visibility)2;
				val = RefTutorialArrow21;
			}
			break;
		}
		if ((BaseComponent)(object)val != (BaseComponent)null && DateTime.UtcNow > tutArrowFrameTime)
		{
			tutArrowFrameTime = DateTime.UtcNow.AddMilliseconds(60.0);
			tutArrowFrame++;
			if (tutArrowFrame >= 10)
			{
				tutArrowFrame = 0;
			}
			val.Source = MainViewModel.Instance.GameSprites[73 + tutArrowFrame];
		}
	}

	public Thickness getMargin(string commandParam, ref bool replaceMargin)
	{
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		//IL_08d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_19d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_035b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0509: Unknown result type (might be due to invalid IL or missing references)
		//IL_0415: Unknown result type (might be due to invalid IL or missing references)
		//IL_0381: Unknown result type (might be due to invalid IL or missing references)
		//IL_03aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_052a: Unknown result type (might be due to invalid IL or missing references)
		//IL_049f: Unknown result type (might be due to invalid IL or missing references)
		//IL_047c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0459: Unknown result type (might be due to invalid IL or missing references)
		//IL_0436: Unknown result type (might be due to invalid IL or missing references)
		bool flag = false;
		switch (commandParam)
		{
		case "STRUCT_GATE_WOOD1D":
		case "STRUCT_GATE_WOOD1A":
		case "STRUCT_GATE_WOOD1B":
		case "STRUCT_GATE_WOOD1C":
		case "STRUCT_GATE_STONE1A":
		case "STRUCT_GATE_STONE1B":
		case "STRUCT_GATE_STONE2A":
		case "STRUCT_GATE_STONE2B":
			flag = true;
			break;
		case "RUINSTOGGLE1":
		case "RUINSTOGGLE2":
		case "RUINSTOGGLE3":
		case "RUINSTOGGLE4":
			return new Thickness(437f, 2f, 0f, 0f);
		case "RUINSTOGGLE1b":
		case "RUINSTOGGLE2b":
		case "RUINSTOGGLE3b":
		case "RUINSTOGGLE4b":
			return new Thickness(437f, 42f, 0f, 0f);
		}
		if (!flag)
		{
			Enums.eMappers structToMapperEnum = MainViewModel.Instance.getStructToMapperEnum(commandParam);
			if (structToMapperEnum != Enums.eMappers.MAPPER_NULL)
			{
				int x = 0;
				int y = 0;
				if (EngineInterface.GetMapperCoords((int)structToMapperEnum, ref x, ref y))
				{
					return (Thickness)(structToMapperEnum switch
					{
						Enums.eMappers.MAPPER_FLETCHER => new Thickness((float)(x - 55), (float)(y - 486 - 2 - 14), 0f, 0f), 
						Enums.eMappers.MAPPER_POLETURNER => new Thickness((float)(x - 55 - 4), (float)(y - 486 - 2 - 13), 0f, 0f), 
						Enums.eMappers.MAPPER_BLACKSMITH => new Thickness((float)(x - 55 - 10), (float)(y - 486 - 2 - 13 + 5), 0f, 0f), 
						Enums.eMappers.MAPPER_TANNER => new Thickness((float)(x - 55 - 6), (float)(y - 486 - 2 - 13), 0f, 0f), 
						Enums.eMappers.MAPPER_ARMOURER => new Thickness((float)(x - 55), (float)(y - 486 - 2 - 13), 0f, 0f), 
						Enums.eMappers.MAPPER_HEALER => new Thickness((float)(x - 35), (float)(y - 486 - 8), 0f, 0f), 
						Enums.eMappers.MAPPER_INN => new Thickness((float)(x - 35), (float)(y - 486 - 8), 0f, 0f), 
						Enums.eMappers.MAPPER_PEOPLE_ENGINEERS_POTS => new Thickness((float)(x - 35 - 3), (float)(y - 486 - 2), 0f, 0f), 
						Enums.eMappers.MAPPER_PEOPLE_ENGINEERS => new Thickness((float)(x - 35 - 6), (float)(y - 486 - 2), 0f, 0f), 
						Enums.eMappers.MAPPER_PEOPLE_KNIGHTS => new Thickness((float)(x - 35 + 6), (float)(y - 486 - 2), 0f, 0f), 
						Enums.eMappers.MAPPER_PEOPLE_PORTABLE_SHIELDS => new Thickness((float)(x - 35 + 9), (float)(y - 486 - 2), 0f, 0f), 
						Enums.eMappers.MAPPER_PEOPLE_XBOWMEN => new Thickness((float)(x - 35 - 4), (float)(y - 486 - 2), 0f, 0f), 
						Enums.eMappers.MAPPER_PEOPLE_BATTERING_RAMS => new Thickness((float)(x - 35 - 8), (float)(y - 486 - 6), 0f, 0f), 
						_ => new Thickness((float)(x - 35), (float)(y - 486 - 2), 0f, 0f), 
					});
				}
			}
		}
		replaceMargin = true;
		Thickness result = default(Thickness);
		switch (MainViewModel.Instance.getStructEnum(commandParam))
		{
		default:
			result = default(Thickness);
			replaceMargin = false;
			break;
		case 40:
			((Thickness)(ref result))._002Ector(80f, 18f, 0f, 0f);
			break;
		case 41:
			((Thickness)(ref result))._002Ector(156f, 8f, 0f, 0f);
			break;
		case 42:
			((Thickness)(ref result))._002Ector(234f, 12f, 0f, 0f);
			break;
		case 43:
			((Thickness)(ref result))._002Ector(310f, 0f, 0f, 0f);
			break;
		case 44:
			((Thickness)(ref result))._002Ector(400f, -10f, 0f, 0f);
			break;
		case 113:
			((Thickness)(ref result))._002Ector(-10f, 12f, 0f, 0f);
			break;
		case 110:
			((Thickness)(ref result))._002Ector(36f, 5f, 0f, 0f);
			break;
		case 111:
			((Thickness)(ref result))._002Ector(96f, 5f, 0f, 0f);
			break;
		case 112:
			((Thickness)(ref result))._002Ector(158f, 5f, 0f, 0f);
			break;
		case 9:
			((Thickness)(ref result))._002Ector(230f, -4f, 0f, 0f);
			break;
		case 8:
			((Thickness)(ref result))._002Ector(280f, 28f, 0f, 0f);
			break;
		case 11:
			((Thickness)(ref result))._002Ector(340f, 10f, 0f, 0f);
			break;
		case 200:
			((Thickness)(ref result))._002Ector(399f, 5f, 0f, 0f);
			break;
		case 202:
			((Thickness)(ref result))._002Ector(399f, 43f, 0f, 0f);
			break;
		case 201:
			((Thickness)(ref result))._002Ector(439f, 5f, 0f, 0f);
			break;
		case 203:
			((Thickness)(ref result))._002Ector(439f, 43f, 0f, 0f);
			break;
		case 10:
			((Thickness)(ref result))._002Ector(35f, 5f, 0f, 0f);
			break;
		case 3:
			((Thickness)(ref result))._002Ector(2f, 30f, 0f, 0f);
			break;
		case 20:
			((Thickness)(ref result))._002Ector(100f, -5f, 0f, 0f);
			break;
		case 4:
			((Thickness)(ref result))._002Ector(175f, 5f, 0f, 0f);
			break;
		case 5:
			((Thickness)(ref result))._002Ector(225f, 15f, 0f, 0f);
			break;
		case 6:
			((Thickness)(ref result))._002Ector(300f, 15f, 0f, 0f);
			break;
		case 26:
			((Thickness)(ref result))._002Ector(400f, 10f, 0f, 0f);
			break;
		case 7:
			((Thickness)(ref result))._002Ector(10f, 12f, 0f, 0f);
			break;
		case 33:
			((Thickness)(ref result))._002Ector(110f, 15f, 15f, 0f);
			break;
		case 32:
			((Thickness)(ref result))._002Ector(210f, 15f, 0f, 0f);
			break;
		case 30:
			((Thickness)(ref result))._002Ector(310f, 15f, 0f, 0f);
			break;
		case 31:
			((Thickness)(ref result))._002Ector(410f, 10f, 0f, 0f);
			break;
		case 1:
			((Thickness)(ref result))._002Ector(-5f, 12f, 0f, 0f);
			break;
		case 36:
			((Thickness)(ref result))._002Ector(70f, 15f, 15f, 0f);
			break;
		case 37:
			((Thickness)(ref result))._002Ector(127f, 5f, 0f, 0f);
			break;
		case 38:
			((Thickness)(ref result))._002Ector(207f, -12f, 0f, 0f);
			break;
		case 23:
			((Thickness)(ref result))._002Ector(305f, 5f, 0f, 0f);
			break;
		case 27:
			((Thickness)(ref result))._002Ector(380f, 20f, 0f, 0f);
			break;
		case 207:
			((Thickness)(ref result))._002Ector(438f, 4f, 0f, 0f);
			break;
		case 208:
			((Thickness)(ref result))._002Ector(438f, 43f, 0f, 0f);
			break;
		case 12:
			((Thickness)(ref result))._002Ector(10f, 11f, 0f, 0f);
			break;
		case 14:
			((Thickness)(ref result))._002Ector(110f, 10f, 15f, 0f);
			break;
		case 13:
			((Thickness)(ref result))._002Ector(210f, 2f, 0f, 0f);
			break;
		case 16:
			((Thickness)(ref result))._002Ector(310f, 10f, 0f, 0f);
			break;
		case 15:
			((Thickness)(ref result))._002Ector(410f, 13f, 0f, 0f);
			break;
		case 19:
			((Thickness)(ref result))._002Ector(10f, 14f, 0f, 0f);
			break;
		case 17:
			((Thickness)(ref result))._002Ector(110f, 12f, 15f, 0f);
			break;
		case 34:
			((Thickness)(ref result))._002Ector(212f, 0f, 0f, 0f);
			break;
		case 18:
			((Thickness)(ref result))._002Ector(290f, 7f, 0f, 0f);
			break;
		case 22:
			((Thickness)(ref result))._002Ector(390f, 1f, 0f, 0f);
			break;
		case 74:
			((Thickness)(ref result))._002Ector(80f, 20f, 0f, 0f);
			break;
		case 75:
			((Thickness)(ref result))._002Ector(160f, 15f, 0f, 0f);
			break;
		case 76:
			((Thickness)(ref result))._002Ector(240f, 7f, 0f, 0f);
			break;
		case 77:
			((Thickness)(ref result))._002Ector(320f, 0f, 0f, 0f);
			break;
		case 78:
			((Thickness)(ref result))._002Ector(400f, 3f, 0f, 0f);
			break;
		case 204:
			((Thickness)(ref result))._002Ector(54f, 16f, 0f, 0f);
			break;
		case 205:
			((Thickness)(ref result))._002Ector(120f, 10f, 0f, 0f);
			break;
		case 206:
			((Thickness)(ref result))._002Ector(170f, 7f, 0f, 0f);
			break;
		case 49:
			((Thickness)(ref result))._002Ector(240f, 5f, 0f, 0f);
			break;
		case 99:
			((Thickness)(ref result))._002Ector(304f, 15f, 0f, 0f);
			break;
		case 68:
			((Thickness)(ref result))._002Ector(368f, 5f, 0f, 0f);
			break;
		case 67:
			((Thickness)(ref result))._002Ector(368f, 45f, 0f, 0f);
			break;
		case 114:
			((Thickness)(ref result))._002Ector(414f, 20f, 0f, 0f);
			break;
		case 168:
			((Thickness)(ref result))._002Ector(440f, 4f, 0f, 0f);
			break;
		case 169:
			((Thickness)(ref result))._002Ector(440f, 43f, 0f, 0f);
			break;
		case 24:
			((Thickness)(ref result))._002Ector(54f, 16f, 0f, 0f);
			break;
		case 115:
			((Thickness)(ref result))._002Ector(120f, 7f, 0f, 0f);
			break;
		case 116:
			((Thickness)(ref result))._002Ector(195f, 3f, 0f, 0f);
			break;
		case 35:
			((Thickness)(ref result))._002Ector(260f, 9f, 0f, 0f);
			break;
		case 25:
			((Thickness)(ref result))._002Ector(335f, 15f, 0f, 0f);
			break;
		case 28:
			((Thickness)(ref result))._002Ector(400f, 5f, 0f, 0f);
			break;
		case 62:
			((Thickness)(ref result))._002Ector(28f, 0f, 0f, 0f);
			break;
		case 91:
			((Thickness)(ref result))._002Ector(62f, 5f, 0f, 0f);
			break;
		case 63:
			((Thickness)(ref result))._002Ector(100f, 45f, 0f, 0f);
			break;
		case 117:
			((Thickness)(ref result))._002Ector(144f, 5f, 0f, 0f);
			break;
		case 92:
			((Thickness)(ref result))._002Ector(162f, 12f, 0f, 0f);
			break;
		case 94:
			((Thickness)(ref result))._002Ector(202f, -5f, 0f, 0f);
			break;
		case 95:
			((Thickness)(ref result))._002Ector(260f, 30f, 0f, 0f);
			break;
		case 93:
			((Thickness)(ref result))._002Ector(324f, 8f, 0f, 0f);
			break;
		case 97:
			((Thickness)(ref result))._002Ector(354f, 0f, 0f, 0f);
			break;
		case 98:
			((Thickness)(ref result))._002Ector(402f, 25f, 0f, 0f);
			break;
		case 65:
			((Thickness)(ref result))._002Ector(30f, 0f, 0f, 0f);
			break;
		case 103:
			((Thickness)(ref result))._002Ector(90f, 25f, 0f, 0f);
			break;
		case 118:
			((Thickness)(ref result))._002Ector(210f, 10f, 0f, 0f);
			break;
		case 119:
			((Thickness)(ref result))._002Ector(160f, 0f, 0f, 0f);
			break;
		case 120:
			((Thickness)(ref result))._002Ector(200f, 30f, 0f, 0f);
			break;
		case 100:
			((Thickness)(ref result))._002Ector(255f, -5f, 0f, 0f);
			break;
		case 101:
			((Thickness)(ref result))._002Ector(280f, 45f, 0f, 0f);
			break;
		case 121:
			((Thickness)(ref result))._002Ector(295f, 0f, 0f, 0f);
			break;
		case 122:
			((Thickness)(ref result))._002Ector(320f, 35f, 0f, 0f);
			break;
		case 123:
			((Thickness)(ref result))._002Ector(412f, 5f, 0f, 0f);
			break;
		case 126:
			((Thickness)(ref result))._002Ector(446f, 52f, 0f, 0f);
			break;
		case 125:
			((Thickness)(ref result))._002Ector(442f, 5f, 0f, 0f);
			break;
		case 124:
			((Thickness)(ref result))._002Ector(412f, 45f, 0f, 0f);
			break;
		case 130:
			((Thickness)(ref result))._002Ector(80f, 10f, 0f, 0f);
			break;
		case 127:
			((Thickness)(ref result))._002Ector(180f, 3f, 0f, 0f);
			break;
		case 128:
			((Thickness)(ref result))._002Ector(300f, -3f, 0f, 0f);
			break;
		case 129:
			((Thickness)(ref result))._002Ector(374f, -3f, 0f, 0f);
			break;
		case 131:
			((Thickness)(ref result))._002Ector(100f, 6f, 0f, 0f);
			break;
		case 132:
			((Thickness)(ref result))._002Ector(200f, 6f, 0f, 0f);
			break;
		case 133:
			((Thickness)(ref result))._002Ector(100f, 2f, 0f, 0f);
			break;
		case 134:
			((Thickness)(ref result))._002Ector(220f, 3f, 0f, 0f);
			break;
		case 135:
			((Thickness)(ref result))._002Ector(4f, -4f, 0f, 0f);
			break;
		case 136:
			((Thickness)(ref result))._002Ector(-14f, 23f, 0f, 0f);
			break;
		case 137:
			((Thickness)(ref result))._002Ector(20f, 28f, 0f, 0f);
			break;
		case 138:
			((Thickness)(ref result))._002Ector(4f, 55f, 0f, 0f);
			break;
		case 139:
			((Thickness)(ref result))._002Ector(52f, 27f, 0f, 0f);
			break;
		case 140:
			((Thickness)(ref result))._002Ector(40f, -12f, 0f, 0f);
			break;
		case 141:
			((Thickness)(ref result))._002Ector(100f, -12f, 0f, 0f);
			break;
		case 142:
			((Thickness)(ref result))._002Ector(160f, 14f, 0f, 0f);
			break;
		case 143:
			((Thickness)(ref result))._002Ector(210f, -12f, 0f, 0f);
			break;
		case 144:
			((Thickness)(ref result))._002Ector(260f, 26f, 0f, 0f);
			break;
		case 145:
			((Thickness)(ref result))._002Ector(310f, -12f, 0f, 0f);
			break;
		case 146:
			((Thickness)(ref result))._002Ector(360f, 27f, 0f, 0f);
			break;
		case 147:
			((Thickness)(ref result))._002Ector(414f, -12f, 0f, 0f);
			break;
		case 148:
			((Thickness)(ref result))._002Ector(-16f, 2f, 0f, 0f);
			break;
		case 149:
			((Thickness)(ref result))._002Ector(8f, -2f, 0f, 0f);
			break;
		case 150:
			((Thickness)(ref result))._002Ector(40f, 0f, 0f, 0f);
			break;
		case 151:
			((Thickness)(ref result))._002Ector(67f, 0f, 0f, 0f);
			break;
		case 152:
			((Thickness)(ref result))._002Ector(107f, 20f, 0f, 0f);
			break;
		case 153:
			((Thickness)(ref result))._002Ector(132f, 0f, 0f, 0f);
			break;
		case 154:
			((Thickness)(ref result))._002Ector(166f, 7f, 0f, 0f);
			break;
		case 155:
			((Thickness)(ref result))._002Ector(206f, -2f, 0f, 0f);
			break;
		case 156:
			((Thickness)(ref result))._002Ector(242f, 24f, 0f, 0f);
			break;
		case 157:
			((Thickness)(ref result))._002Ector(258f, -2f, 0f, 0f);
			break;
		case 164:
			((Thickness)(ref result))._002Ector(290f, 11f, 0f, 0f);
			break;
		case 158:
			((Thickness)(ref result))._002Ector(316f, -4f, 0f, 0f);
			break;
		case 162:
			((Thickness)(ref result))._002Ector(354f, 24f, 0f, 0f);
			break;
		case 159:
			((Thickness)(ref result))._002Ector(388f, 2f, 0f, 0f);
			break;
		case 160:
			((Thickness)(ref result))._002Ector(396f, 34f, 0f, 0f);
			break;
		case 161:
			((Thickness)(ref result))._002Ector(436f, -2f, 0f, 0f);
			break;
		case 163:
			((Thickness)(ref result))._002Ector(442f, 36f, 0f, 0f);
			break;
		case 220:
			((Thickness)(ref result))._002Ector(442f, 36f, 0f, 0f);
			break;
		case 221:
			((Thickness)(ref result))._002Ector(442f, 36f, 0f, 0f);
			break;
		case 222:
			((Thickness)(ref result))._002Ector(442f, 36f, 0f, 0f);
			break;
		case 223:
			((Thickness)(ref result))._002Ector(442f, 36f, 0f, 0f);
			break;
		case 224:
			((Thickness)(ref result))._002Ector(442f, 36f, 0f, 0f);
			break;
		case 225:
			((Thickness)(ref result))._002Ector(442f, 36f, 0f, 0f);
			break;
		case 226:
			((Thickness)(ref result))._002Ector(442f, 36f, 0f, 0f);
			break;
		case 227:
			((Thickness)(ref result))._002Ector(442f, 36f, 0f, 0f);
			break;
		}
		return result;
	}
}
