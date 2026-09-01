using System;
using Noesis;

namespace CrusaderDE;

public class HUD_Main : UserControl
{
	private int scrollPosition;

	private DateTime lastBuildingRollover = DateTime.MinValue;

	private Button[] buildButtons = new Button[588];

	public RadioButton[] sheildButtons = new RadioButton[8];

	private Button[] MEBrushSizeButtons = new Button[9];

	private Button[] MERulerButtons = new Button[3];

	private Rectangle RefGuideRect;

	private StackPanel RefMapEditorModeToggle;

	private Grid RefLayoutRoot;

	private Grid RefMapEditorSheilds;

	private Grid RefFrameBuildings;

	private Grid RefFrameTerrain;

	private Grid RefBuildMenuGrid;

	private Grid RefMEMenuGrid;

	private StackPanel RefBottomTabs1;

	private StackPanel RefSideTabs;

	private RadioButton RefBottomTabs2a;

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

	private Grid RefMETerrainMenu;

	private Grid RefMEAnimalsMenu;

	private Grid RefMETextureMenu;

	private Grid RefMEWaterMenu;

	private Grid RefMEVegetationMenu;

	private Grid RefMERocksMenu;

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

	private int[,] BuildIconLists = new int[23, 17]
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

	private int[] MEsheildIconLists = new int[8] { 440, 441, 442, 443, 444, 445, 446, 447 };

	public string HoverString = "";

	private int HoverStruct;

	private string SelectedString = "";

	private int SelectedStruct;

	public string OtherString = "";

	public string OtherString2 = "";

	private int OtherData1 = -1;

	private int OtherData2 = -1;

	private bool OtherHighestPri;

	private bool OtherVisible;

	private int lastPTBgroup = -1;

	private int lastPTBtext = -1;

	private int lastBuildingTooltipType = -1;

	public int currentTutorialArrow = -1;

	private DateTime tutArrowFrameTime = DateTime.MinValue;

	private int tutArrowFrame;

	public HUD_Main()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDmain = this;
		buildButtons[380] = (Button)FindName("ButtonBuildSubKeepsReturn");
		buildButtons[81] = (Button)FindName("ButtonBuildKeep1");
		buildButtons[82] = (Button)FindName("ButtonBuildKeep2");
		buildButtons[83] = (Button)FindName("ButtonBuildKeep3");
		buildButtons[84] = (Button)FindName("ButtonBuildOutpostArab");
		buildButtons[85] = (Button)FindName("ButtonBuildOutpost");
		buildButtons[582] = (Button)FindName("ButtonBuildOutpostBedouin");
		buildButtons[51] = (Button)FindName("ButtonBuildStairs");
		buildButtons[53] = (Button)FindName("ButtonBuildWoodenWall");
		buildButtons[50] = (Button)FindName("ButtonBuildStoneWall");
		buildButtons[52] = (Button)FindName("ButtonBuildCrenalatedWall");
		buildButtons[543] = (Button)FindName("ButtonBuildWoodenBedouinStockade");
		buildButtons[7] = (Button)FindName("ButtonBuildWoodenBarracks");
		buildButtons[8] = (Button)FindName("ButtonBuildStoneBarracks");
		buildButtons[5] = (Button)FindName("ButtonBuildArmoury");
		buildButtons[60] = (Button)FindName("ButtonBuildSubTowers");
		buildButtons[70] = (Button)FindName("ButtonBuildSubGates");
		buildButtons[68] = (Button)FindName("ButtonBuildSubMilitaryBuildings");
		buildButtons[80] = (Button)FindName("ButtonBuildSubKeeps");
		buildButtons[3] = (Button)FindName("ButtonBuildStockpile");
		buildButtons[2] = (Button)FindName("ButtonBuildWoodcutter");
		buildButtons[24] = (Button)FindName("ButtonBuildQuarry");
		buildButtons[25] = (Button)FindName("ButtonBuildOxTether");
		buildButtons[26] = (Button)FindName("ButtonBuildIronMine");
		buildButtons[28] = (Button)FindName("ButtonBuildPitchRig");
		buildButtons[30] = (Button)FindName("ButtonBuildMarket");
		buildButtons[22] = (Button)FindName("ButtonBuildHunter");
		buildButtons[21] = (Button)FindName("ButtonBuildDairyFarm");
		buildButtons[19] = (Button)FindName("ButtonBuildAppleFarm");
		buildButtons[18] = (Button)FindName("ButtonBuildWheatFarm");
		buildButtons[20] = (Button)FindName("ButtonBuildHopsFarm");
		buildButtons[27] = (Button)FindName("ButtonBuildHovel");
		buildButtons[87] = (Button)FindName("ButtonBuildSmallChurch");
		buildButtons[88] = (Button)FindName("ButtonBuildMedChurch");
		buildButtons[89] = (Button)FindName("ButtonBuildLargeChurch");
		buildButtons[583] = (Button)FindName("ButtonBuildSmallMosque");
		buildButtons[584] = (Button)FindName("ButtonBuildMedMosque");
		buildButtons[585] = (Button)FindName("ButtonBuildLargeMosque");
		buildButtons[34] = (Button)FindName("ButtonBuildApocathery");
		buildButtons[415] = (Button)FindName("ButtonBuildWell");
		buildButtons[542] = (Button)FindName("ButtonBuildWaterpot");
		buildButtons[67] = (Button)FindName("ButtonBuildSubGoodStuff");
		buildButtons[78] = (Button)FindName("ButtonBuildSubBadStuff");
		buildButtons[10] = (Button)FindName("ButtonBuildFletcher");
		buildButtons[11] = (Button)FindName("ButtonBuildPoleturner");
		buildButtons[12] = (Button)FindName("ButtonBuildBlacksmith");
		buildButtons[13] = (Button)FindName("ButtonBuildTanner");
		buildButtons[14] = (Button)FindName("ButtonBuildArmourer");
		buildButtons[4] = (Button)FindName("ButtonBuildGranary");
		buildButtons[15] = (Button)FindName("ButtonBuildBaker");
		buildButtons[23] = (Button)FindName("ButtonBuildMill");
		buildButtons[16] = (Button)FindName("ButtonBuildBrewer");
		buildButtons[33] = (Button)FindName("ButtonBuildInn");
		buildButtons[381] = (Button)FindName("ButtonBuildSubTowersReturn");
		buildButtons[61] = (Button)FindName("ButtonBuildTowerA");
		buildButtons[62] = (Button)FindName("ButtonBuildTowerB");
		buildButtons[63] = (Button)FindName("ButtonBuildTowerC");
		buildButtons[64] = (Button)FindName("ButtonBuildTowerD");
		buildButtons[65] = (Button)FindName("ButtonBuildTowerE");
		buildButtons[382] = (Button)FindName("ButtonBuildSubGatesReturn");
		buildButtons[73] = (Button)FindName("ButtonBuildSmallGate");
		buildButtons[76] = (Button)FindName("ButtonBuildLargeGate");
		buildButtons[74] = (Button)FindName("ButtonBuildDrawbridge");
		buildButtons[408] = (Button)FindName("ButtonBuildDogCage");
		buildButtons[54] = (Button)FindName("ButtonBuildPitchDitch");
		buildButtons[56] = (Button)FindName("ButtonBuildKillingPit");
		buildButtons[55] = (Button)FindName("ButtonBuildBrazier");
		buildButtons[75] = (Button)FindName("ButtonBuildMoat");
		buildButtons[77] = (Button)FindName("ButtonBuildAntiMoat");
		buildButtons[383] = (Button)FindName("ButtonBuildSubMilitaryBuildingsReturn");
		buildButtons[35] = (Button)FindName("ButtonBuildEngineersGuild");
		buildButtons[47] = (Button)FindName("ButtonBuildMangonel");
		buildButtons[48] = (Button)FindName("ButtonBuildBalista");
		buildButtons[31] = (Button)FindName("ButtonBuildStables");
		buildButtons[36] = (Button)FindName("ButtonBuildTunnelersGuild");
		buildButtons[46] = (Button)FindName("ButtonBuildCauldron");
		buildButtons[384] = (Button)FindName("ButtonBuildSubBadStuffReturn");
		buildButtons[40] = (Button)FindName("ButtonBuildGallows");
		buildButtons[400] = (Button)FindName("ButtonBuildCessPit");
		buildButtons[44] = (Button)FindName("ButtonBuildStocks");
		buildButtons[86] = (Button)FindName("ButtonBuildHeadOnSpike");
		buildButtons[401] = (Button)FindName("ButtonBuildBurningPost");
		buildButtons[403] = (Button)FindName("ButtonBuildDungeon");
		buildButtons[404] = (Button)FindName("ButtonBuildStretchingRack");
		buildButtons[402] = (Button)FindName("ButtonBuildGibbet");
		buildButtons[406] = (Button)FindName("ButtonBuildChoppingBlock");
		buildButtons[183] = (Button)FindName("ButtonBuildDunkingStool");
		buildButtons[385] = (Button)FindName("ButtonBuildSubGoodStuffReturn");
		buildButtons[41] = (Button)FindName("ButtonBuildMaypole");
		buildButtons[412] = (Button)FindName("ButtonBuildDancingBear");
		buildButtons[176] = (Button)FindName("ButtonBuildGardenSmall");
		buildButtons[180] = (Button)FindName("ButtonBuildGardenMed");
		buildButtons[189] = (Button)FindName("ButtonBuildGardenLarge");
		buildButtons[417] = (Button)FindName("ButtonBuildPilgrimsCross");
		buildButtons[586] = (Button)FindName("ButtonBuildStatueA");
		buildButtons[410] = (Button)FindName("ButtonBuildShrine");
		buildButtons[170] = (Button)FindName("ButtonBuildFlag1");
		buildButtons[171] = (Button)FindName("ButtonBuildFlag2");
		buildButtons[172] = (Button)FindName("ButtonBuildFlag3");
		buildButtons[587] = (Button)FindName("ButtonBuildFlag3A");
		buildButtons[199] = (Button)FindName("ButtonBuildFlag4");
		buildButtons[387] = (Button)FindName("ButtonBuildSubSubSmallGatesReturn");
		buildButtons[389] = (Button)FindName("ButtonBuildSmallGatehouseNS");
		buildButtons[390] = (Button)FindName("ButtonBuildSmallGatehouseEW");
		buildButtons[388] = (Button)FindName("ButtonBuildSubSubLargeGatesReturn");
		buildButtons[391] = (Button)FindName("ButtonBuildLargeGatehouseNS");
		buildButtons[392] = (Button)FindName("ButtonBuildLargeGatehouseEW");
		buildButtons[555] = (Button)FindName("ButtonBuildRuinsPageToggle1");
		buildButtons[556] = (Button)FindName("ButtonBuildRuinsPageToggle2");
		buildButtons[570] = (Button)FindName("ButtonBuildRuinsPageToggle3");
		buildButtons[577] = (Button)FindName("ButtonBuildRuinsPageToggle4");
		buildButtons[578] = (Button)FindName("ButtonBuildRuinsPageToggle1b");
		buildButtons[579] = (Button)FindName("ButtonBuildRuinsPageToggle2b");
		buildButtons[580] = (Button)FindName("ButtonBuildRuinsPageToggle3b");
		buildButtons[581] = (Button)FindName("ButtonBuildRuinsPageToggle4b");
		buildButtons[500] = (Button)FindName("ButtonBuildMERuins1");
		buildButtons[501] = (Button)FindName("ButtonBuildMERuins2");
		buildButtons[502] = (Button)FindName("ButtonBuildMERuins3");
		buildButtons[503] = (Button)FindName("ButtonBuildMERuins4");
		buildButtons[504] = (Button)FindName("ButtonBuildMERuins5");
		buildButtons[505] = (Button)FindName("ButtonBuildMERuins6");
		buildButtons[506] = (Button)FindName("ButtonBuildMERuins7");
		buildButtons[507] = (Button)FindName("ButtonBuildMERuins8");
		buildButtons[508] = (Button)FindName("ButtonBuildMERuins9");
		buildButtons[509] = (Button)FindName("ButtonBuildMERuins10");
		buildButtons[510] = (Button)FindName("ButtonBuildMERuins11");
		buildButtons[511] = (Button)FindName("ButtonBuildMERuins12");
		buildButtons[512] = (Button)FindName("ButtonBuildMERuins13");
		buildButtons[552] = (Button)FindName("ButtonBuildMERuins14");
		buildButtons[553] = (Button)FindName("ButtonBuildMERuins15");
		buildButtons[554] = (Button)FindName("ButtonBuildMERuins16");
		buildButtons[557] = (Button)FindName("ButtonBuildMERuins17");
		buildButtons[558] = (Button)FindName("ButtonBuildMEDock");
		buildButtons[559] = (Button)FindName("ButtonBuildMERuins18");
		buildButtons[560] = (Button)FindName("ButtonBuildMERuins19");
		buildButtons[561] = (Button)FindName("ButtonBuildMERuins20");
		buildButtons[562] = (Button)FindName("ButtonBuildMERuins21");
		buildButtons[563] = (Button)FindName("ButtonBuildMERuins22");
		buildButtons[564] = (Button)FindName("ButtonBuildMERuins23");
		buildButtons[565] = (Button)FindName("ButtonBuildMERuins24");
		buildButtons[566] = (Button)FindName("ButtonBuildMERuins25");
		buildButtons[567] = (Button)FindName("ButtonBuildMERuins26");
		buildButtons[568] = (Button)FindName("ButtonBuildMERuins27");
		buildButtons[569] = (Button)FindName("ButtonBuildMERuins28");
		buildButtons[571] = (Button)FindName("ButtonBuildMERuins29");
		buildButtons[572] = (Button)FindName("ButtonBuildMERuins30");
		buildButtons[573] = (Button)FindName("ButtonBuildMERuins31");
		buildButtons[574] = (Button)FindName("ButtonBuildMERuins32");
		buildButtons[575] = (Button)FindName("ButtonBuildMERuins33");
		buildButtons[576] = (Button)FindName("ButtonBuildMERuins34");
		buildButtons[480] = (Button)FindName("ButtonBuildMEArcher");
		buildButtons[481] = (Button)FindName("ButtonBuildMESpearman");
		buildButtons[482] = (Button)FindName("ButtonBuildMEPikeman");
		buildButtons[483] = (Button)FindName("ButtonBuildMEMaceman");
		buildButtons[484] = (Button)FindName("ButtonBuildMEXBowman");
		buildButtons[485] = (Button)FindName("ButtonBuildMESwordsman");
		buildButtons[486] = (Button)FindName("ButtonBuildMEKnight");
		buildButtons[487] = (Button)FindName("ButtonBuildMELadderman");
		buildButtons[488] = (Button)FindName("ButtonBuildMEEngineer");
		buildButtons[496] = (Button)FindName("ButtonBuildMEOilguy");
		buildButtons[489] = (Button)FindName("ButtonBuildMEMonk");
		buildButtons[495] = (Button)FindName("ButtonBuildMETunneler");
		buildButtons[490] = (Button)FindName("ButtonBuildMECatapult");
		buildButtons[491] = (Button)FindName("ButtonBuildMETrebuchet");
		buildButtons[492] = (Button)FindName("ButtonBuildMERam");
		buildButtons[493] = (Button)FindName("ButtonBuildMESiegeTower");
		buildButtons[494] = (Button)FindName("ButtonBuildMEMantlet");
		buildButtons[534] = (Button)FindName("ButtonBuildMEArabBow");
		buildButtons[535] = (Button)FindName("ButtonBuildMEArabSlave");
		buildButtons[536] = (Button)FindName("ButtonBuildMEArabSlinger");
		buildButtons[537] = (Button)FindName("ButtonBuildMEArabAssassin");
		buildButtons[538] = (Button)FindName("ButtonBuildMEArabHorseArcher");
		buildButtons[539] = (Button)FindName("ButtonBuildMEArabSwordsman");
		buildButtons[540] = (Button)FindName("ButtonBuildMEArabGrenadier");
		buildButtons[541] = (Button)FindName("ButtonBuildMEArabBallista");
		buildButtons[544] = (Button)FindName("ButtonBuildMECamelLancer");
		buildButtons[545] = (Button)FindName("ButtonBuildMEHealer");
		buildButtons[546] = (Button)FindName("ButtonBuildMEEunuch");
		buildButtons[547] = (Button)FindName("ButtonBuildMEAmbusher");
		buildButtons[548] = (Button)FindName("ButtonBuildMESkirmisher");
		buildButtons[549] = (Button)FindName("ButtonBuildMEHeavyCamel");
		buildButtons[550] = (Button)FindName("ButtonBuildMESapper");
		buildButtons[551] = (Button)FindName("ButtonBuildMEDemolisher");
		Button[] array = buildButtons;
		foreach (Button button in array)
		{
			if (button != null && button.Width != 36f)
			{
				button.Width += 8f;
				button.Height += 8f;
			}
		}
		sheildButtons[0] = (RadioButton)FindName("ButtonMESheild1");
		sheildButtons[1] = (RadioButton)FindName("ButtonMESheild2");
		sheildButtons[2] = (RadioButton)FindName("ButtonMESheild3");
		sheildButtons[3] = (RadioButton)FindName("ButtonMESheild4");
		sheildButtons[4] = (RadioButton)FindName("ButtonMESheild5");
		sheildButtons[5] = (RadioButton)FindName("ButtonMESheild6");
		sheildButtons[6] = (RadioButton)FindName("ButtonMESheild7");
		sheildButtons[7] = (RadioButton)FindName("ButtonMESheild8");
		MEBrushSizeButtons[0] = (Button)FindName("ButtonBrushSize1");
		MEBrushSizeButtons[1] = (Button)FindName("ButtonBrushSize2");
		MEBrushSizeButtons[2] = (Button)FindName("ButtonBrushSize3");
		MEBrushSizeButtons[3] = (Button)FindName("ButtonBrushSize4");
		MEBrushSizeButtons[4] = (Button)FindName("ButtonBrushSize5");
		MEBrushSizeButtons[5] = (Button)FindName("ButtonBrushSize6");
		MEBrushSizeButtons[6] = (Button)FindName("ButtonBrushSize7");
		MEBrushSizeButtons[7] = (Button)FindName("ButtonBrushSize8");
		MEBrushSizeButtons[8] = (Button)FindName("ButtonBrushSize9");
		MERulerButtons[0] = (Button)FindName("ButtonRuler1");
		MERulerButtons[1] = (Button)FindName("ButtonRuler2");
		MERulerButtons[2] = (Button)FindName("ButtonRuler3");
		RefGuideRect = (Rectangle)FindName("GuideRect");
		RefMapEditorModeToggle = (StackPanel)FindName("MEModeToggle");
		RefLayoutRoot = (Grid)FindName("LayoutRoot");
		RefMapEditorSheilds = (Grid)FindName("MESheildPanel");
		RefFrameBuildings = (Grid)FindName("MainFrameBuildings");
		RefFrameTerrain = (Grid)FindName("MainFrameTerrain");
		RefBuildMenuGrid = (Grid)FindName("BuildMenuGrid");
		RefMEMenuGrid = (Grid)FindName("MEMenuGrid");
		RefBottomTabs1 = (StackPanel)FindName("BottomTabs1");
		RefSideTabs = (StackPanel)FindName("SideTabs");
		RefBottomTabs2a = (RadioButton)FindName("RadioButtonMERuins");
		RefBottomTabs2b = (RadioButton)FindName("RadioButtonMETroops");
		RefBottomTabs2c = (RadioButton)FindName("RadioButtonMEArabTroops");
		RefBottomTabs2d = (RadioButton)FindName("RadioButtonMESiege");
		RefBottomTabs2e = (RadioButton)FindName("RadioButtonMEBedouin");
		RefButtonBuildModeBuildings = (RadioButton)FindName("ButtonBuildModeBuildings");
		RefButtonBuildModeTerrain = (RadioButton)FindName("ButtonBuildModeTerrain");
		RefButtonMEHeightControls = (RadioButton)FindName("ButtonMEHeightControls");
		RefButtonMERocksSignpost = (RadioButton)FindName("ButtonMERocksSignpost");
		RefMETerrainMenu = (Grid)FindName("METerrainMenu");
		RefMEAnimalsMenu = (Grid)FindName("MEAnimalsMenu");
		RefMETextureMenu = (Grid)FindName("METextureMenu");
		RefMEWaterMenu = (Grid)FindName("MEWaterMenu");
		RefMEVegetationMenu = (Grid)FindName("MEVegetationMenu");
		RefMERocksMenu = (Grid)FindName("MERocksMenu");
		RefTabBuildCastle = (RadioButton)FindName("TabBuildCastle");
		RefTabBuildIndustry = (RadioButton)FindName("TabBuildIndustry");
		RefTabBuildFarms = (RadioButton)FindName("TabBuildFarms");
		RefTabBuildTown = (RadioButton)FindName("TabBuildTown");
		RefTabBuildWeapons = (RadioButton)FindName("TabBuildWeapons");
		RefTabBuildFood = (RadioButton)FindName("TabBuildFood");
		RefGameInfoButton = (Button)FindName("GameInfoButton");
		RefGameUndoButton = (Button)FindName("GameUndoButton");
		RefTutorialArrow1 = (Image)FindName("TutorialArrow1");
		RefTutorialArrow2 = (Image)FindName("TutorialArrow2");
		RefTutorialArrow7 = (Image)FindName("TutorialArrow7");
		RefTutorialArrow8 = (Image)FindName("TutorialArrow8");
		RefTutorialArrow9 = (Image)FindName("TutorialArrow9");
		RefTutorialArrow10 = (Image)FindName("TutorialArrow10");
		RefTutorialArrow11 = (Image)FindName("TutorialArrow11");
		RefTutorialArrow12 = (Image)FindName("TutorialArrow12");
		RefTutorialArrow13 = (Image)FindName("TutorialArrow13");
		RefTutorialArrow14 = (Image)FindName("TutorialArrow14");
		RefTutorialArrow15 = (Image)FindName("TutorialArrow15");
		RefTutorialArrow16 = (Image)FindName("TutorialArrow16");
		RefTutorialArrow17 = (Image)FindName("TutorialArrow17");
		RefTutorialArrow18 = (Image)FindName("TutorialArrow18");
		RefTutorialArrow20 = (Image)FindName("TutorialArrow20");
		scrollPosition = 0;
		SetupNewBuildScreen(-1);
		findUIlowerPoint();
	}

	public void SetEditorModeButtonVisibilityForSiegeThatMode(bool visible)
	{
		for (int i = 0; i < 8; i++)
		{
			MainViewModel.Instance.HUDmain.sheildButtons[i].Visibility = Visibility.Hidden;
		}
		if (visible)
		{
			MainViewModel.Instance.HUDmain.RefButtonBuildModeTerrain.Visibility = Visibility.Visible;
			MainViewModel.Instance.HUDmain.RefButtonBuildModeBuildings.Visibility = Visibility.Visible;
			if (GameData.Instance.mapType == Enums.GameModes.INVASION || GameData.Instance.multiplayerMap)
			{
				for (int j = 0; j < 8; j++)
				{
					MainViewModel.Instance.HUDmain.sheildButtons[j].Visibility = Visibility.Visible;
				}
			}
			else
			{
				MainViewModel.Instance.HUDmain.sheildButtons[0].Visibility = Visibility.Visible;
			}
		}
		else
		{
			MainViewModel.Instance.HUDmain.RefButtonBuildModeTerrain.Visibility = Visibility.Hidden;
			MainViewModel.Instance.HUDmain.RefButtonBuildModeBuildings.Visibility = Visibility.Hidden;
		}
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_Main.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "Click" && handlerName == "InGameOptions")
		{
			((Button)source).Click += InGameOptions;
			return true;
		}
		if (eventName == "Click" && handlerName == "UndoLastAction")
		{
			((Button)source).Click += UndoLastAction;
			return true;
		}
		if (eventName == "Click" && handlerName == "ReturnToBriefingScreen")
		{
			((Button)source).Click += ReturnToBriefingScreen;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenKeeps")
		{
			((Button)source).Click += NewBuildScreenKeeps;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenCastle")
		{
			((RadioButton)source).Click += NewBuildScreenCastle;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenIndustry")
		{
			((RadioButton)source).Click += NewBuildScreenIndustry;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenFarms")
		{
			((RadioButton)source).Click += NewBuildScreenFarms;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenTown")
		{
			((RadioButton)source).Click += NewBuildScreenTown;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenWeapons")
		{
			((RadioButton)source).Click += NewBuildScreenWeapons;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenFood")
		{
			((RadioButton)source).Click += NewBuildScreenFood;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenTowers")
		{
			((Button)source).Click += NewBuildScreenTowers;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenGates")
		{
			((Button)source).Click += NewBuildScreenGates;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenMilitaryBuildings")
		{
			((Button)source).Click += NewBuildScreenMilitaryBuildings;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenBadStuff")
		{
			((Button)source).Click += NewBuildScreenBadStuff;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenGoodStuff")
		{
			((Button)source).Click += NewBuildScreenGoodStuff;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenSubTowersRtn")
		{
			((Button)source).Click += NewBuildScreenSubTowersRtn;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenSubTownRtn")
		{
			((Button)source).Click += NewBuildScreenSubTownRtn;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenSubSubSmallGates")
		{
			((Button)source).Click += NewBuildScreenSubSubSmallGates;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenSubSubLargeGates")
		{
			((Button)source).Click += NewBuildScreenSubSubLargeGates;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenSubGatesRtn")
		{
			((Button)source).Click += NewBuildScreenSubGatesRtn;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenMETroops")
		{
			((RadioButton)source).Click += NewBuildScreenMETroops;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenMERuins")
		{
			((RadioButton)source).Click += NewBuildScreenMERuins;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenMESiege")
		{
			((RadioButton)source).Click += NewBuildScreenMESiege;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenMEBedouin")
		{
			((RadioButton)source).Click += NewBuildScreenMEBEdouin;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewBuildScreenMEArabTroops")
		{
			((RadioButton)source).Click += NewBuildScreenMEArabTroops;
			return true;
		}
		if (eventName == "Click" && handlerName == "BuildScreenAllies")
		{
			((Button)source).Click += BuildScreenAllies;
			return true;
		}
		if (eventName == "Click" && handlerName == "BuildScreenMerit")
		{
			((Button)source).Click += BuildScreenMerit;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewMEScreenTerrain")
		{
			((RadioButton)source).Click += NewMEScreenTerrain;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewMEScreenAnimals")
		{
			((RadioButton)source).Click += NewMEScreenAnimals;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewMEScreenTexture")
		{
			((RadioButton)source).Click += NewMEScreenTexture;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewMEScreenWater")
		{
			((RadioButton)source).Click += NewMEScreenWater;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewMEScreenVegetation")
		{
			((RadioButton)source).Click += NewMEScreenVegetation;
			return true;
		}
		if (eventName == "Click" && handlerName == "NewMEScreenRocks")
		{
			((RadioButton)source).Click += NewMEScreenRocks;
			return true;
		}
		if (eventName == "Click" && handlerName == "METoggleToBuildings")
		{
			((RadioButton)source).Click += METoggleToBuildings;
			return true;
		}
		if (eventName == "Click" && handlerName == "METoggleToTerrain")
		{
			((RadioButton)source).Click += METoggleToTerrain;
			return true;
		}
		if (eventName == "Click" && handlerName == "CycleMEDrawingSize")
		{
			((Button)source).Click += CycleMEDrawingSize;
			return true;
		}
		if (eventName == "Click" && handlerName == "CycleMERuler")
		{
			((Button)source).Click += CycleMERuler;
			return true;
		}
		if (eventName == "Click" && handlerName == "EnterMEDeleteMode")
		{
			((Button)source).Click += EnterMEDeleteMode;
			return true;
		}
		if (eventName == "Loaded" && handlerName == "OnLoadMainUIGrid")
		{
			((Rectangle)source).Loaded += OnLoadMainUIGrid;
			return true;
		}
		if (eventName == "Unloaded" && handlerName == "OnUnLoadMainUIGrid")
		{
			((Rectangle)source).Unloaded += OnUnLoadMainUIGrid;
			return true;
		}
		if (eventName == "MouseEnter" && handlerName == "MouseEnterBuildingIconHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MouseEnterBuildingIconHandler;
			}
			else if (source is RadioButton)
			{
				((RadioButton)source).MouseEnter += MouseEnterBuildingIconHandler;
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "MouseLeaveBuildingIconHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseLeave += MouseLeaveBuildingIconHandler;
			}
			else if (source is RadioButton)
			{
				((RadioButton)source).MouseLeave += MouseLeaveBuildingIconHandler;
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

	private bool CreateBuildingTooltip(int buildingType)
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

	private void MouseEnterBuildingIconHandler(object sender, MouseEventArgs e)
	{
		int num = 0;
		if (e.Source is Button)
		{
			if (((Button)e.Source).CommandParameter != null && ((Button)e.Source).CommandParameter is string)
			{
				num = MainViewModel.Instance.getStructEnum((string)((Button)e.Source).CommandParameter);
				if (num == 0)
				{
					num = MainViewModel.Instance.getStructEnumExtra((string)((Button)e.Source).CommandParameter);
				}
				if (num != 107)
				{
					switch ((string)((Button)e.Source).CommandParameter)
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
			if (((Button)e.Source).Tag != null && ((Button)e.Source).Tag is string)
			{
				HoverString = (string)((Button)e.Source).Tag;
				switch (((Button)e.Source).Name)
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
		else if (e.Source is RadioButton)
		{
			if (((RadioButton)e.Source).CommandParameter != null && ((RadioButton)e.Source).CommandParameter is string)
			{
				num = MainViewModel.Instance.getStructEnum((string)((RadioButton)e.Source).CommandParameter);
				if (num == 0)
				{
					num = MainViewModel.Instance.getStructEnumExtra((string)((RadioButton)e.Source).CommandParameter);
				}
			}
			if (((RadioButton)e.Source).Tag != null && ((RadioButton)e.Source).Tag is string)
			{
				HoverString = (string)((RadioButton)e.Source).Tag;
				switch (((RadioButton)e.Source).Name)
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

	private void SetRolloverData(int amountNeeded, int gotAmount, ImageSource image, int column)
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

	private void MouseLeaveBuildingIconHandler(object sender, MouseEventArgs e)
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
				if (MainViewModel.Instance.HUDScenario != null && MainViewModel.Instance.ShowingScenario)
				{
					MainViewModel.Instance.HUDScenario.IsEnabled = false;
					MainViewModel.Instance.HUDScenarioPopup.IsEnabled = false;
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
		if (scrollPosition == 0)
		{
			scrollPosition = 1;
			((Storyboard)base.Resources["Swish2"]).Begin(this);
		}
		else
		{
			scrollPosition = 0;
			((Storyboard)base.Resources["Swish1"]).Begin(this);
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
			RefMapEditorModeToggle.IsEnabled = true;
			RefMapEditorModeToggle.Visibility = Visibility.Visible;
			if (!GameData.Instance.multiplayerMap)
			{
				RefButtonMERocksSignpost.Visibility = Visibility.Visible;
			}
			else
			{
				RefButtonMERocksSignpost.Visibility = Visibility.Hidden;
			}
			if (MainViewModel.Instance.MEMode == 0)
			{
				MainViewModel.Instance.RollOverText_Margin = "0,0,-20,154";
				RefFrameBuildings.Visibility = Visibility.Hidden;
				RefFrameTerrain.Visibility = Visibility.Visible;
				RefBuildMenuGrid.Visibility = Visibility.Hidden;
				RefMEMenuGrid.Visibility = Visibility.Visible;
				RefBottomTabs1.Visibility = Visibility.Hidden;
				RefBottomTabs2a.Visibility = Visibility.Hidden;
				RefBottomTabs2b.Visibility = Visibility.Hidden;
				RefBottomTabs2c.Visibility = Visibility.Hidden;
				RefBottomTabs2d.Visibility = Visibility.Hidden;
				RefBottomTabs2e.Visibility = Visibility.Hidden;
				RefSideTabs.Visibility = Visibility.Hidden;
				RefMapEditorSheilds.Visibility = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.RollOverText_Margin = "0,0,-20,130";
				RefFrameBuildings.Visibility = Visibility.Visible;
				RefFrameTerrain.Visibility = Visibility.Hidden;
				RefBuildMenuGrid.Visibility = Visibility.Visible;
				RefMEMenuGrid.Visibility = Visibility.Hidden;
				RefBottomTabs1.Visibility = Visibility.Visible;
				RefBottomTabs2a.Visibility = Visibility.Visible;
				RefBottomTabs2b.Visibility = Visibility.Visible;
				RefBottomTabs2c.Visibility = Visibility.Visible;
				RefBottomTabs2d.Visibility = Visibility.Visible;
				RefBottomTabs2e.Visibility = Visibility.Visible;
				RefSideTabs.Visibility = Visibility.Visible;
				RefMapEditorSheilds.Visibility = Visibility.Visible;
			}
		}
		else
		{
			MainViewModel.Instance.SkirmishModeVisAlly = (Director.instance.SkirmishModeGame || Director.instance.MultiplayerGame) && GameData.Instance.game_type != 0;
			MainViewModel.Instance.SkirmishModeVisMerit = Director.instance.SkirmishModeGame || Director.instance.MultiplayerGame;
			RefMapEditorModeToggle.IsEnabled = false;
			RefMapEditorModeToggle.Visibility = Visibility.Hidden;
			buildButtons[380].IsEnabled = false;
			MainViewModel.Instance.RollOverText_Margin = "0,0,-20,130";
			RefFrameBuildings.Visibility = Visibility.Visible;
			RefFrameTerrain.Visibility = Visibility.Hidden;
			RefBuildMenuGrid.Visibility = Visibility.Visible;
			RefMEMenuGrid.Visibility = Visibility.Hidden;
			RefBottomTabs1.Visibility = Visibility.Visible;
			RefBottomTabs2a.Visibility = Visibility.Hidden;
			RefBottomTabs2b.Visibility = Visibility.Hidden;
			RefBottomTabs2c.Visibility = Visibility.Hidden;
			RefBottomTabs2d.Visibility = Visibility.Hidden;
			RefBottomTabs2e.Visibility = Visibility.Hidden;
			RefSideTabs.Visibility = Visibility.Visible;
			RefMapEditorSheilds.Visibility = Visibility.Hidden;
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
				if (num <= 0 || buildButtons[num] == null)
				{
					break;
				}
				switch (num)
				{
				case 87:
					if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
					{
						buildButtons[num].IsEnabled = false;
						if (flag)
						{
							buildButtons[num].Visibility = Visibility.Hidden;
						}
						num = 583;
					}
					else
					{
						buildButtons[583].IsEnabled = false;
						if (flag)
						{
							buildButtons[583].Visibility = Visibility.Hidden;
						}
					}
					break;
				case 88:
					if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
					{
						buildButtons[num].IsEnabled = false;
						if (flag)
						{
							buildButtons[num].Visibility = Visibility.Hidden;
						}
						num = 584;
					}
					else
					{
						buildButtons[584].IsEnabled = false;
						if (flag)
						{
							buildButtons[584].Visibility = Visibility.Hidden;
						}
					}
					break;
				case 89:
					if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
					{
						buildButtons[num].IsEnabled = false;
						if (flag)
						{
							buildButtons[num].Visibility = Visibility.Hidden;
						}
						num = 585;
					}
					else
					{
						buildButtons[585].IsEnabled = false;
						if (flag)
						{
							buildButtons[585].Visibility = Visibility.Hidden;
						}
					}
					break;
				case 417:
					if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
					{
						buildButtons[num].IsEnabled = false;
						if (flag)
						{
							buildButtons[num].Visibility = Visibility.Hidden;
						}
						num = 586;
					}
					else
					{
						buildButtons[586].IsEnabled = false;
						if (flag)
						{
							buildButtons[586].Visibility = Visibility.Hidden;
						}
					}
					break;
				case 172:
					if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
					{
						buildButtons[num].IsEnabled = false;
						if (flag)
						{
							buildButtons[num].Visibility = Visibility.Hidden;
						}
						num = 587;
					}
					else
					{
						buildButtons[587].IsEnabled = false;
						if (flag)
						{
							buildButtons[587].Visibility = Visibility.Hidden;
						}
					}
					break;
				}
				if (i == newScreenID)
				{
					buildButtons[num].IsEnabled = MainViewModel.Instance.CanPlaceMapper(buildButtons[num].CommandParameter);
					bool replaceMargin = false;
					Thickness margin = getMargin((string)buildButtons[num].CommandParameter, ref replaceMargin);
					buildButtons[num].Margin = margin;
				}
				else
				{
					buildButtons[num].IsEnabled = false;
				}
				if (flag)
				{
					buildButtons[num].Visibility = Visibility.Hidden;
				}
			}
		}
		if (buildButtons[80].IsEnabled)
		{
			if (MainViewModel.Instance.IsMapEditorMode)
			{
				buildButtons[80].IsEnabled = true;
			}
			else
			{
				buildButtons[80].IsEnabled = false;
			}
		}
		if (MainViewModel.Instance.FreezeMainControls)
		{
			buildButtons[15].IsEnabled = false;
			buildButtons[23].IsEnabled = false;
			buildButtons[16].IsEnabled = false;
			buildButtons[33].IsEnabled = false;
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
		RefMETerrainMenu.Visibility = Visibility.Hidden;
		RefMEAnimalsMenu.Visibility = Visibility.Hidden;
		RefMETextureMenu.Visibility = Visibility.Hidden;
		RefMEWaterMenu.Visibility = Visibility.Hidden;
		RefMEVegetationMenu.Visibility = Visibility.Hidden;
		RefMERocksMenu.Visibility = Visibility.Hidden;
		switch (newScreenID)
		{
		case 1:
			RefMETerrainMenu.Visibility = Visibility.Visible;
			break;
		case 2:
			RefMEAnimalsMenu.Visibility = Visibility.Visible;
			break;
		case 3:
			RefMETextureMenu.Visibility = Visibility.Visible;
			break;
		case 4:
			RefMEWaterMenu.Visibility = Visibility.Visible;
			break;
		case 5:
			RefMEVegetationMenu.Visibility = Visibility.Visible;
			break;
		case 6:
			RefMERocksMenu.Visibility = Visibility.Visible;
			break;
		}
	}

	public void SetupMEDrawingBrush(int newSize)
	{
		for (int i = 0; i < 9; i++)
		{
			if (i == newSize)
			{
				MEBrushSizeButtons[i].Visibility = Visibility.Visible;
			}
			else
			{
				MEBrushSizeButtons[i].Visibility = Visibility.Hidden;
			}
		}
	}

	public void SetupMERuler(int newMode)
	{
		for (int i = 0; i < 3; i++)
		{
			if (i == newMode)
			{
				MERulerButtons[i].Visibility = Visibility.Visible;
			}
			else
			{
				MERulerButtons[i].Visibility = Visibility.Hidden;
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
		RefTutorialArrow1.Visibility = Visibility.Hidden;
		RefTutorialArrow2.Visibility = Visibility.Hidden;
		RefTutorialArrow3.Visibility = Visibility.Hidden;
		RefTutorialArrow4.Visibility = Visibility.Hidden;
		RefTutorialArrow5.Visibility = Visibility.Hidden;
		RefTutorialArrow6.Visibility = Visibility.Hidden;
		RefTutorialArrow7.Visibility = Visibility.Hidden;
		RefTutorialArrow8.Visibility = Visibility.Hidden;
		RefTutorialArrow9.Visibility = Visibility.Hidden;
		RefTutorialArrow10.Visibility = Visibility.Hidden;
		RefTutorialArrow11.Visibility = Visibility.Hidden;
		RefTutorialArrow12.Visibility = Visibility.Hidden;
		RefTutorialArrow13.Visibility = Visibility.Hidden;
		RefTutorialArrow14.Visibility = Visibility.Hidden;
		RefTutorialArrow15.Visibility = Visibility.Hidden;
		RefTutorialArrow16.Visibility = Visibility.Hidden;
		RefTutorialArrow17.Visibility = Visibility.Hidden;
		RefTutorialArrow18.Visibility = Visibility.Hidden;
		RefTutorialArrow19.Visibility = Visibility.Hidden;
		RefTutorialArrow20.Visibility = Visibility.Hidden;
		RefTutorialArrow21.Visibility = Visibility.Hidden;
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
		Image image = null;
		switch (currentTutorialArrow)
		{
		case 1:
			if (buildButtons[81].IsVisible)
			{
				RefTutorialArrow1.Visibility = Visibility.Visible;
				image = RefTutorialArrow1;
			}
			else
			{
				RefTutorialArrow1.Visibility = Visibility.Hidden;
			}
			break;
		case 2:
			if (buildButtons[4].IsVisible)
			{
				RefTutorialArrow2.Visibility = Visibility.Visible;
				image = RefTutorialArrow2;
			}
			else
			{
				RefTutorialArrow2.Visibility = Visibility.Hidden;
			}
			break;
		case 3:
			if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 4)
			{
				RefTutorialArrow3.Visibility = Visibility.Visible;
				image = RefTutorialArrow3;
			}
			else
			{
				RefTutorialArrow3.Visibility = Visibility.Hidden;
			}
			break;
		case 4:
			if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 2)
			{
				RefTutorialArrow4.Visibility = Visibility.Visible;
				image = RefTutorialArrow4;
			}
			else
			{
				RefTutorialArrow4.Visibility = Visibility.Hidden;
			}
			break;
		case 5:
			RefTutorialArrow5.Visibility = Visibility.Visible;
			image = RefTutorialArrow5;
			break;
		case 6:
			RefTutorialArrow6.Visibility = Visibility.Visible;
			image = RefTutorialArrow6;
			break;
		case 7:
			RefTutorialArrow7.Visibility = Visibility.Visible;
			image = RefTutorialArrow7;
			break;
		case 8:
			if (GameData.Instance.lastGameState.app_sub_mode != 20)
			{
				RefTutorialArrow8.Visibility = Visibility.Visible;
				image = RefTutorialArrow8;
				RefTutorialArrow9.Visibility = Visibility.Hidden;
			}
			else
			{
				RefTutorialArrow9.Visibility = Visibility.Visible;
				image = RefTutorialArrow9;
				RefTutorialArrow8.Visibility = Visibility.Hidden;
			}
			break;
		case 10:
			if (GameData.Instance.lastGameState.app_sub_mode != 40)
			{
				RefTutorialArrow10.Visibility = Visibility.Visible;
				image = RefTutorialArrow10;
				RefTutorialArrow11.Visibility = Visibility.Hidden;
			}
			else
			{
				RefTutorialArrow11.Visibility = Visibility.Visible;
				image = RefTutorialArrow11;
				RefTutorialArrow10.Visibility = Visibility.Hidden;
			}
			break;
		case 12:
			if (GameData.Instance.lastGameState.app_sub_mode != 30)
			{
				RefTutorialArrow12.Visibility = Visibility.Visible;
				image = RefTutorialArrow12;
				RefTutorialArrow13.Visibility = Visibility.Hidden;
			}
			else
			{
				RefTutorialArrow13.Visibility = Visibility.Visible;
				image = RefTutorialArrow13;
				RefTutorialArrow12.Visibility = Visibility.Hidden;
			}
			break;
		case 13:
			if (GameData.Instance.lastGameState.app_sub_mode != 28)
			{
				RefTutorialArrow14.Visibility = Visibility.Visible;
				image = RefTutorialArrow14;
				RefTutorialArrow15.Visibility = Visibility.Hidden;
			}
			else
			{
				RefTutorialArrow15.Visibility = Visibility.Visible;
				image = RefTutorialArrow15;
				RefTutorialArrow14.Visibility = Visibility.Hidden;
			}
			break;
		case 14:
			if (GameData.Instance.lastGameState.app_sub_mode != 10)
			{
				RefTutorialArrow16.Visibility = Visibility.Visible;
				image = RefTutorialArrow16;
				RefTutorialArrow17.Visibility = Visibility.Hidden;
			}
			else
			{
				RefTutorialArrow17.Visibility = Visibility.Visible;
				image = RefTutorialArrow17;
				RefTutorialArrow16.Visibility = Visibility.Hidden;
			}
			break;
		case 15:
			if (GameData.Instance.lastGameState.app_sub_mode != 10)
			{
				RefTutorialArrow16.Visibility = Visibility.Visible;
				image = RefTutorialArrow16;
				RefTutorialArrow18.Visibility = Visibility.Hidden;
			}
			else
			{
				RefTutorialArrow18.Visibility = Visibility.Visible;
				image = RefTutorialArrow18;
				RefTutorialArrow16.Visibility = Visibility.Hidden;
			}
			break;
		case 16:
			if (GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 1)
			{
				RefTutorialArrow19.Visibility = Visibility.Visible;
				image = RefTutorialArrow19;
			}
			break;
		case 17:
			if (GameData.Instance.lastGameState.app_sub_mode != 10)
			{
				RefTutorialArrow16.Visibility = Visibility.Visible;
				image = RefTutorialArrow16;
				RefTutorialArrow20.Visibility = Visibility.Hidden;
			}
			else
			{
				RefTutorialArrow20.Visibility = Visibility.Visible;
				image = RefTutorialArrow20;
				RefTutorialArrow16.Visibility = Visibility.Hidden;
			}
			break;
		case 18:
			if (GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 44)
			{
				RefTutorialArrow21.Visibility = Visibility.Visible;
				image = RefTutorialArrow21;
			}
			break;
		}
		if (image != null && DateTime.UtcNow > tutArrowFrameTime)
		{
			tutArrowFrameTime = DateTime.UtcNow.AddMilliseconds(60.0);
			tutArrowFrame++;
			if (tutArrowFrame >= 10)
			{
				tutArrowFrame = 0;
			}
			image.Source = MainViewModel.Instance.GameSprites[73 + tutArrowFrame];
		}
	}

	public Thickness getMargin(string commandParam, ref bool replaceMargin)
	{
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
					return structToMapperEnum switch
					{
						Enums.eMappers.MAPPER_FLETCHER => new Thickness(x - 55, y - 486 - 2 - 14, 0f, 0f), 
						Enums.eMappers.MAPPER_POLETURNER => new Thickness(x - 55 - 4, y - 486 - 2 - 13, 0f, 0f), 
						Enums.eMappers.MAPPER_BLACKSMITH => new Thickness(x - 55 - 10, y - 486 - 2 - 13 + 5, 0f, 0f), 
						Enums.eMappers.MAPPER_TANNER => new Thickness(x - 55 - 6, y - 486 - 2 - 13, 0f, 0f), 
						Enums.eMappers.MAPPER_ARMOURER => new Thickness(x - 55, y - 486 - 2 - 13, 0f, 0f), 
						Enums.eMappers.MAPPER_HEALER => new Thickness(x - 35, y - 486 - 8, 0f, 0f), 
						Enums.eMappers.MAPPER_INN => new Thickness(x - 35, y - 486 - 8, 0f, 0f), 
						Enums.eMappers.MAPPER_PEOPLE_ENGINEERS_POTS => new Thickness(x - 35 - 3, y - 486 - 2, 0f, 0f), 
						Enums.eMappers.MAPPER_PEOPLE_ENGINEERS => new Thickness(x - 35 - 6, y - 486 - 2, 0f, 0f), 
						Enums.eMappers.MAPPER_PEOPLE_KNIGHTS => new Thickness(x - 35 + 6, y - 486 - 2, 0f, 0f), 
						Enums.eMappers.MAPPER_PEOPLE_PORTABLE_SHIELDS => new Thickness(x - 35 + 9, y - 486 - 2, 0f, 0f), 
						Enums.eMappers.MAPPER_PEOPLE_XBOWMEN => new Thickness(x - 35 - 4, y - 486 - 2, 0f, 0f), 
						Enums.eMappers.MAPPER_PEOPLE_BATTERING_RAMS => new Thickness(x - 35 - 8, y - 486 - 6, 0f, 0f), 
						_ => new Thickness(x - 35, y - 486 - 2, 0f, 0f), 
					};
				}
			}
		}
		replaceMargin = true;
		Thickness result;
		switch (MainViewModel.Instance.getStructEnum(commandParam))
		{
		default:
			result = default(Thickness);
			replaceMargin = false;
			break;
		case 40:
			result = new Thickness(80f, 18f, 0f, 0f);
			break;
		case 41:
			result = new Thickness(156f, 8f, 0f, 0f);
			break;
		case 42:
			result = new Thickness(234f, 12f, 0f, 0f);
			break;
		case 43:
			result = new Thickness(310f, 0f, 0f, 0f);
			break;
		case 44:
			result = new Thickness(400f, -10f, 0f, 0f);
			break;
		case 113:
			result = new Thickness(-10f, 12f, 0f, 0f);
			break;
		case 110:
			result = new Thickness(36f, 5f, 0f, 0f);
			break;
		case 111:
			result = new Thickness(96f, 5f, 0f, 0f);
			break;
		case 112:
			result = new Thickness(158f, 5f, 0f, 0f);
			break;
		case 9:
			result = new Thickness(230f, -4f, 0f, 0f);
			break;
		case 8:
			result = new Thickness(280f, 28f, 0f, 0f);
			break;
		case 11:
			result = new Thickness(340f, 10f, 0f, 0f);
			break;
		case 200:
			result = new Thickness(399f, 5f, 0f, 0f);
			break;
		case 202:
			result = new Thickness(399f, 43f, 0f, 0f);
			break;
		case 201:
			result = new Thickness(439f, 5f, 0f, 0f);
			break;
		case 203:
			result = new Thickness(439f, 43f, 0f, 0f);
			break;
		case 10:
			result = new Thickness(35f, 5f, 0f, 0f);
			break;
		case 3:
			result = new Thickness(2f, 30f, 0f, 0f);
			break;
		case 20:
			result = new Thickness(100f, -5f, 0f, 0f);
			break;
		case 4:
			result = new Thickness(175f, 5f, 0f, 0f);
			break;
		case 5:
			result = new Thickness(225f, 15f, 0f, 0f);
			break;
		case 6:
			result = new Thickness(300f, 15f, 0f, 0f);
			break;
		case 26:
			result = new Thickness(400f, 10f, 0f, 0f);
			break;
		case 7:
			result = new Thickness(10f, 12f, 0f, 0f);
			break;
		case 33:
			result = new Thickness(110f, 15f, 15f, 0f);
			break;
		case 32:
			result = new Thickness(210f, 15f, 0f, 0f);
			break;
		case 30:
			result = new Thickness(310f, 15f, 0f, 0f);
			break;
		case 31:
			result = new Thickness(410f, 10f, 0f, 0f);
			break;
		case 1:
			result = new Thickness(-5f, 12f, 0f, 0f);
			break;
		case 36:
			result = new Thickness(70f, 15f, 15f, 0f);
			break;
		case 37:
			result = new Thickness(127f, 5f, 0f, 0f);
			break;
		case 38:
			result = new Thickness(207f, -12f, 0f, 0f);
			break;
		case 23:
			result = new Thickness(305f, 5f, 0f, 0f);
			break;
		case 27:
			result = new Thickness(380f, 20f, 0f, 0f);
			break;
		case 207:
			result = new Thickness(438f, 4f, 0f, 0f);
			break;
		case 208:
			result = new Thickness(438f, 43f, 0f, 0f);
			break;
		case 12:
			result = new Thickness(10f, 11f, 0f, 0f);
			break;
		case 14:
			result = new Thickness(110f, 10f, 15f, 0f);
			break;
		case 13:
			result = new Thickness(210f, 2f, 0f, 0f);
			break;
		case 16:
			result = new Thickness(310f, 10f, 0f, 0f);
			break;
		case 15:
			result = new Thickness(410f, 13f, 0f, 0f);
			break;
		case 19:
			result = new Thickness(10f, 14f, 0f, 0f);
			break;
		case 17:
			result = new Thickness(110f, 12f, 15f, 0f);
			break;
		case 34:
			result = new Thickness(212f, 0f, 0f, 0f);
			break;
		case 18:
			result = new Thickness(290f, 7f, 0f, 0f);
			break;
		case 22:
			result = new Thickness(390f, 1f, 0f, 0f);
			break;
		case 74:
			result = new Thickness(80f, 20f, 0f, 0f);
			break;
		case 75:
			result = new Thickness(160f, 15f, 0f, 0f);
			break;
		case 76:
			result = new Thickness(240f, 7f, 0f, 0f);
			break;
		case 77:
			result = new Thickness(320f, 0f, 0f, 0f);
			break;
		case 78:
			result = new Thickness(400f, 3f, 0f, 0f);
			break;
		case 204:
			result = new Thickness(54f, 16f, 0f, 0f);
			break;
		case 205:
			result = new Thickness(120f, 10f, 0f, 0f);
			break;
		case 206:
			result = new Thickness(170f, 7f, 0f, 0f);
			break;
		case 49:
			result = new Thickness(240f, 5f, 0f, 0f);
			break;
		case 99:
			result = new Thickness(304f, 15f, 0f, 0f);
			break;
		case 68:
			result = new Thickness(368f, 5f, 0f, 0f);
			break;
		case 67:
			result = new Thickness(368f, 45f, 0f, 0f);
			break;
		case 114:
			result = new Thickness(414f, 20f, 0f, 0f);
			break;
		case 168:
			result = new Thickness(440f, 4f, 0f, 0f);
			break;
		case 169:
			result = new Thickness(440f, 43f, 0f, 0f);
			break;
		case 24:
			result = new Thickness(54f, 16f, 0f, 0f);
			break;
		case 115:
			result = new Thickness(120f, 7f, 0f, 0f);
			break;
		case 116:
			result = new Thickness(195f, 3f, 0f, 0f);
			break;
		case 35:
			result = new Thickness(260f, 9f, 0f, 0f);
			break;
		case 25:
			result = new Thickness(335f, 15f, 0f, 0f);
			break;
		case 28:
			result = new Thickness(400f, 5f, 0f, 0f);
			break;
		case 62:
			result = new Thickness(28f, 0f, 0f, 0f);
			break;
		case 91:
			result = new Thickness(62f, 5f, 0f, 0f);
			break;
		case 63:
			result = new Thickness(100f, 45f, 0f, 0f);
			break;
		case 117:
			result = new Thickness(144f, 5f, 0f, 0f);
			break;
		case 92:
			result = new Thickness(162f, 12f, 0f, 0f);
			break;
		case 94:
			result = new Thickness(202f, -5f, 0f, 0f);
			break;
		case 95:
			result = new Thickness(260f, 30f, 0f, 0f);
			break;
		case 93:
			result = new Thickness(324f, 8f, 0f, 0f);
			break;
		case 97:
			result = new Thickness(354f, 0f, 0f, 0f);
			break;
		case 98:
			result = new Thickness(402f, 25f, 0f, 0f);
			break;
		case 65:
			result = new Thickness(30f, 0f, 0f, 0f);
			break;
		case 103:
			result = new Thickness(90f, 25f, 0f, 0f);
			break;
		case 118:
			result = new Thickness(210f, 10f, 0f, 0f);
			break;
		case 119:
			result = new Thickness(160f, 0f, 0f, 0f);
			break;
		case 120:
			result = new Thickness(200f, 30f, 0f, 0f);
			break;
		case 100:
			result = new Thickness(255f, -5f, 0f, 0f);
			break;
		case 101:
			result = new Thickness(280f, 45f, 0f, 0f);
			break;
		case 121:
			result = new Thickness(295f, 0f, 0f, 0f);
			break;
		case 122:
			result = new Thickness(320f, 35f, 0f, 0f);
			break;
		case 123:
			result = new Thickness(412f, 5f, 0f, 0f);
			break;
		case 126:
			result = new Thickness(446f, 52f, 0f, 0f);
			break;
		case 125:
			result = new Thickness(442f, 5f, 0f, 0f);
			break;
		case 124:
			result = new Thickness(412f, 45f, 0f, 0f);
			break;
		case 130:
			result = new Thickness(80f, 10f, 0f, 0f);
			break;
		case 127:
			result = new Thickness(180f, 3f, 0f, 0f);
			break;
		case 128:
			result = new Thickness(300f, -3f, 0f, 0f);
			break;
		case 129:
			result = new Thickness(374f, -3f, 0f, 0f);
			break;
		case 131:
			result = new Thickness(100f, 6f, 0f, 0f);
			break;
		case 132:
			result = new Thickness(200f, 6f, 0f, 0f);
			break;
		case 133:
			result = new Thickness(100f, 2f, 0f, 0f);
			break;
		case 134:
			result = new Thickness(220f, 3f, 0f, 0f);
			break;
		case 135:
			result = new Thickness(4f, -4f, 0f, 0f);
			break;
		case 136:
			result = new Thickness(-14f, 23f, 0f, 0f);
			break;
		case 137:
			result = new Thickness(20f, 28f, 0f, 0f);
			break;
		case 138:
			result = new Thickness(4f, 55f, 0f, 0f);
			break;
		case 139:
			result = new Thickness(52f, 27f, 0f, 0f);
			break;
		case 140:
			result = new Thickness(40f, -12f, 0f, 0f);
			break;
		case 141:
			result = new Thickness(100f, -12f, 0f, 0f);
			break;
		case 142:
			result = new Thickness(160f, 14f, 0f, 0f);
			break;
		case 143:
			result = new Thickness(210f, -12f, 0f, 0f);
			break;
		case 144:
			result = new Thickness(260f, 26f, 0f, 0f);
			break;
		case 145:
			result = new Thickness(310f, -12f, 0f, 0f);
			break;
		case 146:
			result = new Thickness(360f, 27f, 0f, 0f);
			break;
		case 147:
			result = new Thickness(414f, -12f, 0f, 0f);
			break;
		case 148:
			result = new Thickness(-16f, 2f, 0f, 0f);
			break;
		case 149:
			result = new Thickness(8f, -2f, 0f, 0f);
			break;
		case 150:
			result = new Thickness(40f, 0f, 0f, 0f);
			break;
		case 151:
			result = new Thickness(67f, 0f, 0f, 0f);
			break;
		case 152:
			result = new Thickness(107f, 20f, 0f, 0f);
			break;
		case 153:
			result = new Thickness(132f, 0f, 0f, 0f);
			break;
		case 154:
			result = new Thickness(166f, 7f, 0f, 0f);
			break;
		case 155:
			result = new Thickness(206f, -2f, 0f, 0f);
			break;
		case 156:
			result = new Thickness(242f, 24f, 0f, 0f);
			break;
		case 157:
			result = new Thickness(258f, -2f, 0f, 0f);
			break;
		case 164:
			result = new Thickness(290f, 11f, 0f, 0f);
			break;
		case 158:
			result = new Thickness(316f, -4f, 0f, 0f);
			break;
		case 162:
			result = new Thickness(354f, 24f, 0f, 0f);
			break;
		case 159:
			result = new Thickness(388f, 2f, 0f, 0f);
			break;
		case 160:
			result = new Thickness(396f, 34f, 0f, 0f);
			break;
		case 161:
			result = new Thickness(436f, -2f, 0f, 0f);
			break;
		case 163:
			result = new Thickness(442f, 36f, 0f, 0f);
			break;
		case 220:
			result = new Thickness(442f, 36f, 0f, 0f);
			break;
		case 221:
			result = new Thickness(442f, 36f, 0f, 0f);
			break;
		case 222:
			result = new Thickness(442f, 36f, 0f, 0f);
			break;
		case 223:
			result = new Thickness(442f, 36f, 0f, 0f);
			break;
		case 224:
			result = new Thickness(442f, 36f, 0f, 0f);
			break;
		case 225:
			result = new Thickness(442f, 36f, 0f, 0f);
			break;
		case 226:
			result = new Thickness(442f, 36f, 0f, 0f);
			break;
		case 227:
			result = new Thickness(442f, 36f, 0f, 0f);
			break;
		}
		return result;
	}
}
