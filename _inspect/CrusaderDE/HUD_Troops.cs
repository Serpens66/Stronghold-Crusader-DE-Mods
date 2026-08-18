using System;
using Noesis;

namespace CrusaderDE;

public class HUD_Troops : UserControl
{
	public Button RefUnitDisband;

	public Button RefUnitBuildCat;

	public Button RefUnitStop;

	public Button RefUnitBuildTreb;

	public Button RefUnitPatrol;

	public Button RefUnitPatrolActive;

	public bool PatrolShouldBeVisible;

	public Button RefUnitBuildTower;

	public Grid RefUnitAttackHere;

	public Button RefUnitTunnelHere;

	public Button RefUnitPourOil;

	public Button RefUnitBuildRam;

	public Button RefUnitBuild;

	public Grid RefUnitReload;

	public Button RefUnitbuildMantlet;

	public Button RefUnitbuildArabBallista;

	public Grid RefUnitFireCow;

	public Button RefUnitBack;

	public Button RefSelArchers;

	public Button RefSelSpearmen;

	public Button RefSelMacemen;

	public Button RefSelXBowmen;

	public Button RefSelPikemen;

	public Button RefSelSwordsmen;

	public Button RefSelKnights;

	public Button RefSelEngineers;

	public Button RefSelMonks;

	public Button RefSelLaddermen;

	public Button RefSelTunnelers;

	public Button RefSelCatapults;

	public Button RefSelTrebuchets;

	public Button RefSelRams;

	public Button RefSelSiegeTowers;

	public Button RefSelMantlets;

	public Button RefSelMangonels;

	public Button RefSelBalistae;

	public Button RefSelArabBow;

	public Button RefSelArabSlave;

	public Button RefSelArabSlinger;

	public Button RefSelArabAssassin;

	public Button RefSelArabHorseArcher;

	public Button RefSelArabSwordsman;

	public Button RefSelArabGrenadier;

	public Button RefSelArabBallista;

	public Button RefSelBedouinCamelLancerSelected;

	public Button RefSelBedouinHealerSelected;

	public Button RefSelBedouinEunuchSelected;

	public Button RefSelBedouinAmbusherSelected;

	public Button RefSelBedouinSkirmisherSelected;

	public Button RefSelBedouinHeavyCamelSelected;

	public Button RefSelBedouinSapperSelected;

	public Button RefSelBedouinDemolisherSelected;

	public TextBlock RefSelTroopNo1;

	public TextBlock RefSelTroopNo2;

	public TextBlock RefSelTroopNo3;

	public TextBlock RefSelTroopNo4;

	public TextBlock RefSelTroopNo5;

	public TextBlock RefSelTroopNo6;

	public TextBlock RefSelTroopNo7;

	public TextBlock RefSelTroopNo8;

	public TextBlock RefTroopsPanelRollover;

	public TextBlock RefTroopsPanelRollover2;

	public Button RefButtonTroopPanelPage1;

	public Button RefButtonTroopPanelPage2;

	public int[] SelectedChimpArray = new int[89];

	public int NoSelectedChimpTypes;

	public int currentPage;

	public TranslateTransform[] SelTroopPositions = (TranslateTransform[])(object)new TranslateTransform[8]
	{
		new TranslateTransform(-181f, 0f),
		new TranslateTransform(-129f, 0f),
		new TranslateTransform(-77f, 0f),
		new TranslateTransform(-25f, 0f),
		new TranslateTransform(27f, 0f),
		new TranslateTransform(79f, 0f),
		new TranslateTransform(131f, 0f),
		new TranslateTransform(183f, 0f)
	};

	public int pages = 1;

	public HUD_Troops()
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Expected O, but got Unknown
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Expected O, but got Unknown
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Expected O, but got Unknown
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Expected O, but got Unknown
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Expected O, but got Unknown
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Expected O, but got Unknown
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Expected O, but got Unknown
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Expected O, but got Unknown
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Expected O, but got Unknown
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Expected O, but got Unknown
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Expected O, but got Unknown
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Expected O, but got Unknown
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Expected O, but got Unknown
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Expected O, but got Unknown
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Expected O, but got Unknown
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Expected O, but got Unknown
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Expected O, but got Unknown
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Expected O, but got Unknown
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Expected O, but got Unknown
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_023d: Expected O, but got Unknown
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Expected O, but got Unknown
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Expected O, but got Unknown
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_027f: Expected O, but got Unknown
		//IL_028b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Expected O, but got Unknown
		//IL_02a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Expected O, but got Unknown
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c1: Expected O, but got Unknown
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Expected O, but got Unknown
		//IL_02e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ed: Expected O, but got Unknown
		//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0303: Expected O, but got Unknown
		//IL_030f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0319: Expected O, but got Unknown
		//IL_0325: Unknown result type (might be due to invalid IL or missing references)
		//IL_032f: Expected O, but got Unknown
		//IL_033b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0345: Expected O, but got Unknown
		//IL_0351: Unknown result type (might be due to invalid IL or missing references)
		//IL_035b: Expected O, but got Unknown
		//IL_0367: Unknown result type (might be due to invalid IL or missing references)
		//IL_0371: Expected O, but got Unknown
		//IL_037d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0387: Expected O, but got Unknown
		//IL_0393: Unknown result type (might be due to invalid IL or missing references)
		//IL_039d: Expected O, but got Unknown
		//IL_03a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b3: Expected O, but got Unknown
		//IL_03bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c9: Expected O, but got Unknown
		//IL_03d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03df: Expected O, but got Unknown
		//IL_03eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f5: Expected O, but got Unknown
		//IL_0401: Unknown result type (might be due to invalid IL or missing references)
		//IL_040b: Expected O, but got Unknown
		//IL_0417: Unknown result type (might be due to invalid IL or missing references)
		//IL_0421: Expected O, but got Unknown
		//IL_042d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0437: Expected O, but got Unknown
		//IL_0443: Unknown result type (might be due to invalid IL or missing references)
		//IL_044d: Expected O, but got Unknown
		//IL_0459: Unknown result type (might be due to invalid IL or missing references)
		//IL_0463: Expected O, but got Unknown
		//IL_046f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0479: Expected O, but got Unknown
		//IL_0485: Unknown result type (might be due to invalid IL or missing references)
		//IL_048f: Expected O, but got Unknown
		//IL_049b: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a5: Expected O, but got Unknown
		//IL_04b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04bb: Expected O, but got Unknown
		//IL_04c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d1: Expected O, but got Unknown
		//IL_04dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e7: Expected O, but got Unknown
		//IL_04f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_04fd: Expected O, but got Unknown
		//IL_0509: Unknown result type (might be due to invalid IL or missing references)
		//IL_0513: Expected O, but got Unknown
		//IL_051f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0529: Expected O, but got Unknown
		//IL_0535: Unknown result type (might be due to invalid IL or missing references)
		//IL_053f: Expected O, but got Unknown
		//IL_054b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0555: Expected O, but got Unknown
		//IL_0561: Unknown result type (might be due to invalid IL or missing references)
		//IL_056b: Expected O, but got Unknown
		//IL_0577: Unknown result type (might be due to invalid IL or missing references)
		//IL_0581: Expected O, but got Unknown
		//IL_058d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0597: Expected O, but got Unknown
		//IL_05a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ad: Expected O, but got Unknown
		//IL_05b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c3: Expected O, but got Unknown
		//IL_05cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d9: Expected O, but got Unknown
		//IL_05e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ef: Expected O, but got Unknown
		//IL_05fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0605: Expected O, but got Unknown
		//IL_0611: Unknown result type (might be due to invalid IL or missing references)
		//IL_061b: Expected O, but got Unknown
		//IL_0627: Unknown result type (might be due to invalid IL or missing references)
		//IL_0631: Expected O, but got Unknown
		InitializeComponent();
		MainViewModel.Instance.HUDTroopPanel = this;
		RefUnitDisband = (Button)((FrameworkElement)this).FindName("UnitDisband");
		RefUnitBuildCat = (Button)((FrameworkElement)this).FindName("UnitBuildCat");
		RefUnitStop = (Button)((FrameworkElement)this).FindName("UnitStop");
		RefUnitBuildTreb = (Button)((FrameworkElement)this).FindName("UnitBuildTreb");
		RefUnitPatrol = (Button)((FrameworkElement)this).FindName("UnitPatrol");
		RefUnitPatrolActive = (Button)((FrameworkElement)this).FindName("UnitPatrolActive");
		RefUnitBuildTower = (Button)((FrameworkElement)this).FindName("UnitBuildTower");
		RefUnitAttackHere = (Grid)((FrameworkElement)this).FindName("UnitAttackHere");
		RefUnitTunnelHere = (Button)((FrameworkElement)this).FindName("UnitTunnelHere");
		RefUnitPourOil = (Button)((FrameworkElement)this).FindName("UnitPourOil");
		RefUnitBuildRam = (Button)((FrameworkElement)this).FindName("UnitBuildRam");
		RefUnitBuild = (Button)((FrameworkElement)this).FindName("UnitBuild");
		RefUnitReload = (Grid)((FrameworkElement)this).FindName("UnitReload");
		RefUnitbuildMantlet = (Button)((FrameworkElement)this).FindName("UnitbuildMantlet");
		RefUnitbuildArabBallista = (Button)((FrameworkElement)this).FindName("UnitbuildArabBallista");
		RefUnitFireCow = (Grid)((FrameworkElement)this).FindName("UnitFireCow");
		RefUnitBack = (Button)((FrameworkElement)this).FindName("UnitBack");
		RefSelArchers = (Button)((FrameworkElement)this).FindName("ArchersSelected");
		RefSelSpearmen = (Button)((FrameworkElement)this).FindName("SpearmenSelected");
		RefSelMacemen = (Button)((FrameworkElement)this).FindName("MacemenSelected");
		RefSelXBowmen = (Button)((FrameworkElement)this).FindName("XBowmenSelected");
		RefSelPikemen = (Button)((FrameworkElement)this).FindName("PikemenSelected");
		RefSelSwordsmen = (Button)((FrameworkElement)this).FindName("SwordsmenSelected");
		RefSelKnights = (Button)((FrameworkElement)this).FindName("KnightsSelected");
		RefSelEngineers = (Button)((FrameworkElement)this).FindName("EngineersSelected");
		RefSelMonks = (Button)((FrameworkElement)this).FindName("MonksSelected");
		RefSelLaddermen = (Button)((FrameworkElement)this).FindName("LaddermenSelected");
		RefSelTunnelers = (Button)((FrameworkElement)this).FindName("TunnelersSelected");
		RefSelCatapults = (Button)((FrameworkElement)this).FindName("CatapultsSelected");
		RefSelTrebuchets = (Button)((FrameworkElement)this).FindName("TrebuchetsSelected");
		RefSelRams = (Button)((FrameworkElement)this).FindName("RamsSelected");
		RefSelSiegeTowers = (Button)((FrameworkElement)this).FindName("SiegeTowersSelected");
		RefSelMantlets = (Button)((FrameworkElement)this).FindName("MantletsSelected");
		RefSelMangonels = (Button)((FrameworkElement)this).FindName("MangonelsSelected");
		RefSelBalistae = (Button)((FrameworkElement)this).FindName("BalistaeSelected");
		RefSelArabBow = (Button)((FrameworkElement)this).FindName("ArabBowSelected");
		RefSelArabSlave = (Button)((FrameworkElement)this).FindName("ArabSlaveSelected");
		RefSelArabSlinger = (Button)((FrameworkElement)this).FindName("ArabSlingerSelected");
		RefSelArabAssassin = (Button)((FrameworkElement)this).FindName("ArabAssassinSelected");
		RefSelArabHorseArcher = (Button)((FrameworkElement)this).FindName("ArabHorseArcherSelected");
		RefSelArabSwordsman = (Button)((FrameworkElement)this).FindName("ArabSwordsmanSelected");
		RefSelArabGrenadier = (Button)((FrameworkElement)this).FindName("ArabGrenadierSelected");
		RefSelArabBallista = (Button)((FrameworkElement)this).FindName("ArabBallistaSelected");
		RefSelBedouinCamelLancerSelected = (Button)((FrameworkElement)this).FindName("BedouinCamelLancerSelected");
		RefSelBedouinHealerSelected = (Button)((FrameworkElement)this).FindName("BedouinHealerSelected");
		RefSelBedouinEunuchSelected = (Button)((FrameworkElement)this).FindName("BedouinEunuchSelected");
		RefSelBedouinAmbusherSelected = (Button)((FrameworkElement)this).FindName("BedouinAmbusherSelected");
		RefSelBedouinSkirmisherSelected = (Button)((FrameworkElement)this).FindName("BedouinSkirmisherSelected");
		RefSelBedouinHeavyCamelSelected = (Button)((FrameworkElement)this).FindName("BedouinHeavyCamelSelected");
		RefSelBedouinSapperSelected = (Button)((FrameworkElement)this).FindName("BedouinSapperSelected");
		RefSelBedouinDemolisherSelected = (Button)((FrameworkElement)this).FindName("BedouinDemolisherSelected");
		RefButtonTroopPanelPage1 = (Button)((FrameworkElement)this).FindName("ButtonTroopPanelPage1");
		RefButtonTroopPanelPage2 = (Button)((FrameworkElement)this).FindName("ButtonTroopPanelPage2");
		RefSelTroopNo1 = (TextBlock)((FrameworkElement)this).FindName("SelectedTroopCount1");
		RefSelTroopNo2 = (TextBlock)((FrameworkElement)this).FindName("SelectedTroopCount2");
		RefSelTroopNo3 = (TextBlock)((FrameworkElement)this).FindName("SelectedTroopCount3");
		RefSelTroopNo4 = (TextBlock)((FrameworkElement)this).FindName("SelectedTroopCount4");
		RefSelTroopNo5 = (TextBlock)((FrameworkElement)this).FindName("SelectedTroopCount5");
		RefSelTroopNo6 = (TextBlock)((FrameworkElement)this).FindName("SelectedTroopCount6");
		RefSelTroopNo7 = (TextBlock)((FrameworkElement)this).FindName("SelectedTroopCount7");
		RefSelTroopNo8 = (TextBlock)((FrameworkElement)this).FindName("SelectedTroopCount8");
		RefTroopsPanelRollover = (TextBlock)((FrameworkElement)this).FindName("TroopsPanelRollover");
		RefTroopsPanelRollover2 = (TextBlock)((FrameworkElement)this).FindName("TroopsPanelRollover2");
		SelectedChimpArray[22] = 3;
		SelectedChimpArray[28] = 4;
		SelectedChimpArray[26] = 1;
		SelectedChimpArray[61] = 2;
		SelectedChimpArray[59] = 5;
		SelectedChimpArray[41] = 6;
		SelectedChimpArray[60] = 7;
		SelectedChimpArray[29] = 9;
		SelectedChimpArray[30] = 12;
		SelectedChimpArray[37] = 22;
		SelectedChimpArray[39] = 34;
		SelectedChimpArray[40] = 56;
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_Troops.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		return false;
	}

	public void SelectedTroops(bool fromOpen = false)
	{
		if (fromOpen)
		{
			currentPage = 0;
		}
		SetupSelectedTroops();
		SetuptroopActionsUI(fromOpen);
	}

	public void CountSelectedChimpTypes()
	{
		SelectedChimpArray = EditorDirector.instance.getSelectedChimpTypes();
		NoSelectedChimpTypes = 0;
		for (int i = 0; i < 89; i++)
		{
			if (i != 55 && SelectedChimpArray[i] > 0)
			{
				NoSelectedChimpTypes++;
			}
		}
	}

	public void RemoveSelectedChimpTypes(Enums.eChimps type, int mode)
	{
		for (Enums.eChimps eChimps = Enums.eChimps.CHIMP_TYPE_NULL; eChimps < Enums.eChimps.CHIMP_NUM_TYPES; eChimps++)
		{
			switch (mode)
			{
			case 0:
				if (eChimps != type)
				{
					SelectedChimpArray[(int)eChimps] = 0;
				}
				break;
			case 1:
				if (eChimps == type)
				{
					SelectedChimpArray[(int)eChimps] = 0;
				}
				break;
			}
		}
	}

	public void TogglePages(string command)
	{
		if (command == "0" && currentPage > 0)
		{
			currentPage--;
			SetupSelectedTroops();
		}
		if (command == "1" && currentPage < pages - 1)
		{
			currentPage++;
			SetupSelectedTroops();
		}
	}

	public void SetupSelectedTroops()
	{
		try
		{
			CountSelectedChimpTypes();
			HideAllSelectedTroops();
			HideAllSelectedTroopsNumbers();
			int num = -1;
			int num2 = -1;
			int num3 = -1;
			int num4 = 0;
			for (int i = 0; i < 89; i++)
			{
				if (SelectedChimpArray[i] > 0 && i != 55)
				{
					num4++;
					switch (num4)
					{
					case 9:
						num = i;
						pages = 2;
						break;
					case 18:
						num2 = i;
						pages = 3;
						break;
					case 27:
						num3 = i;
						pages = 4;
						break;
					}
				}
			}
			if (currentPage >= pages)
			{
				currentPage = pages - 1;
			}
			int num5 = 0;
			if (currentPage == 1)
			{
				num5 = num;
			}
			else if (currentPage == 2)
			{
				num5 = num2;
			}
			else if (currentPage == 3)
			{
				num5 = num3;
			}
			if (pages > 1)
			{
				if (currentPage == 0)
				{
					PropEx.SetButtonVisibility((UIElement)(object)RefButtonTroopPanelPage1, (Visibility)2);
					PropEx.SetButtonVisibility((UIElement)(object)RefButtonTroopPanelPage2, (Visibility)1);
				}
				else if (currentPage == pages - 1)
				{
					PropEx.SetButtonVisibility((UIElement)(object)RefButtonTroopPanelPage1, (Visibility)1);
					PropEx.SetButtonVisibility((UIElement)(object)RefButtonTroopPanelPage2, (Visibility)2);
				}
				else
				{
					PropEx.SetButtonVisibility((UIElement)(object)RefButtonTroopPanelPage1, (Visibility)2);
					PropEx.SetButtonVisibility((UIElement)(object)RefButtonTroopPanelPage2, (Visibility)2);
				}
			}
			else
			{
				PropEx.SetButtonVisibility((UIElement)(object)RefButtonTroopPanelPage1, (Visibility)1);
				PropEx.SetButtonVisibility((UIElement)(object)RefButtonTroopPanelPage2, (Visibility)1);
			}
			int num6 = 0;
			for (int j = num5; j < 89; j++)
			{
				if (SelectedChimpArray[j] > 0 && j != 55)
				{
					SelTroopPositions[num6].Y = SetSelectedTroopVisible(j);
					SetSelectedTroopPosition(j, num6);
					ShowSelectedTroopsNumber(num6, SelectedChimpArray[j]);
					if (++num6 >= 8)
					{
						break;
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public void SetSelectedTroopPosition(int type, int slot)
	{
		switch (type)
		{
		case 22:
			((UIElement)RefSelArchers).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 24:
			((UIElement)RefSelSpearmen).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 26:
			((UIElement)RefSelMacemen).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 23:
			((UIElement)RefSelXBowmen).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 25:
			((UIElement)RefSelPikemen).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 27:
			((UIElement)RefSelSwordsmen).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 28:
			((UIElement)RefSelKnights).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 30:
			((UIElement)RefSelEngineers).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 37:
			((UIElement)RefSelMonks).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 29:
			((UIElement)RefSelLaddermen).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 5:
			((UIElement)RefSelTunnelers).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 39:
			((UIElement)RefSelCatapults).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 40:
			((UIElement)RefSelTrebuchets).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 59:
			((UIElement)RefSelRams).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 58:
			((UIElement)RefSelSiegeTowers).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 60:
			((UIElement)RefSelMantlets).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 41:
			((UIElement)RefSelMangonels).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 61:
			((UIElement)RefSelBalistae).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 70:
			((UIElement)RefSelArabBow).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 71:
			((UIElement)RefSelArabSlave).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 72:
			((UIElement)RefSelArabSlinger).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 73:
			((UIElement)RefSelArabAssassin).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 74:
			((UIElement)RefSelArabHorseArcher).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 75:
			((UIElement)RefSelArabSwordsman).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 76:
			((UIElement)RefSelArabGrenadier).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 77:
			((UIElement)RefSelArabBallista).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 78:
			((UIElement)RefSelBedouinCamelLancerSelected).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 79:
			((UIElement)RefSelBedouinHealerSelected).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 80:
			((UIElement)RefSelBedouinEunuchSelected).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 81:
			((UIElement)RefSelBedouinAmbusherSelected).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 82:
			((UIElement)RefSelBedouinSkirmisherSelected).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 83:
			((UIElement)RefSelBedouinHeavyCamelSelected).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 84:
			((UIElement)RefSelBedouinSapperSelected).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		case 85:
			((UIElement)RefSelBedouinDemolisherSelected).RenderTransform = (Transform)(object)SelTroopPositions[slot];
			break;
		}
	}

	public int SetSelectedTroopVisible(int type)
	{
		switch (type)
		{
		case 22:
			((UIElement)RefSelArchers).Visibility = (Visibility)2;
			break;
		case 24:
			((UIElement)RefSelSpearmen).Visibility = (Visibility)2;
			break;
		case 26:
			((UIElement)RefSelMacemen).Visibility = (Visibility)2;
			break;
		case 23:
			((UIElement)RefSelXBowmen).Visibility = (Visibility)2;
			break;
		case 25:
			((UIElement)RefSelPikemen).Visibility = (Visibility)2;
			break;
		case 27:
			((UIElement)RefSelSwordsmen).Visibility = (Visibility)2;
			break;
		case 28:
			((UIElement)RefSelKnights).Visibility = (Visibility)2;
			break;
		case 30:
			((UIElement)RefSelEngineers).Visibility = (Visibility)2;
			break;
		case 37:
			((UIElement)RefSelMonks).Visibility = (Visibility)2;
			break;
		case 29:
			((UIElement)RefSelLaddermen).Visibility = (Visibility)2;
			break;
		case 5:
			((UIElement)RefSelTunnelers).Visibility = (Visibility)2;
			break;
		case 39:
			((UIElement)RefSelCatapults).Visibility = (Visibility)2;
			break;
		case 40:
			((UIElement)RefSelTrebuchets).Visibility = (Visibility)2;
			break;
		case 59:
			((UIElement)RefSelRams).Visibility = (Visibility)2;
			break;
		case 58:
			((UIElement)RefSelSiegeTowers).Visibility = (Visibility)2;
			break;
		case 60:
			((UIElement)RefSelMantlets).Visibility = (Visibility)2;
			break;
		case 41:
			((UIElement)RefSelMangonels).Visibility = (Visibility)2;
			break;
		case 61:
			((UIElement)RefSelBalistae).Visibility = (Visibility)2;
			break;
		case 70:
			((UIElement)RefSelArabBow).Visibility = (Visibility)2;
			break;
		case 71:
			((UIElement)RefSelArabSlave).Visibility = (Visibility)2;
			break;
		case 72:
			((UIElement)RefSelArabSlinger).Visibility = (Visibility)2;
			break;
		case 73:
			((UIElement)RefSelArabAssassin).Visibility = (Visibility)2;
			break;
		case 74:
			((UIElement)RefSelArabHorseArcher).Visibility = (Visibility)2;
			break;
		case 75:
			((UIElement)RefSelArabSwordsman).Visibility = (Visibility)2;
			break;
		case 76:
			((UIElement)RefSelArabGrenadier).Visibility = (Visibility)2;
			break;
		case 77:
			((UIElement)RefSelArabBallista).Visibility = (Visibility)2;
			break;
		case 78:
			((UIElement)RefSelBedouinCamelLancerSelected).Visibility = (Visibility)2;
			break;
		case 79:
			((UIElement)RefSelBedouinHealerSelected).Visibility = (Visibility)2;
			break;
		case 80:
			((UIElement)RefSelBedouinEunuchSelected).Visibility = (Visibility)2;
			break;
		case 81:
			((UIElement)RefSelBedouinAmbusherSelected).Visibility = (Visibility)2;
			break;
		case 82:
			((UIElement)RefSelBedouinSkirmisherSelected).Visibility = (Visibility)2;
			break;
		case 83:
			((UIElement)RefSelBedouinHeavyCamelSelected).Visibility = (Visibility)2;
			break;
		case 84:
			((UIElement)RefSelBedouinSapperSelected).Visibility = (Visibility)2;
			break;
		case 85:
			((UIElement)RefSelBedouinDemolisherSelected).Visibility = (Visibility)2;
			break;
		}
		return -13;
	}

	public void HideAllSelectedTroops()
	{
		((UIElement)RefSelArchers).Visibility = (Visibility)1;
		((UIElement)RefSelSpearmen).Visibility = (Visibility)1;
		((UIElement)RefSelMacemen).Visibility = (Visibility)1;
		((UIElement)RefSelXBowmen).Visibility = (Visibility)1;
		((UIElement)RefSelPikemen).Visibility = (Visibility)1;
		((UIElement)RefSelSwordsmen).Visibility = (Visibility)1;
		((UIElement)RefSelKnights).Visibility = (Visibility)1;
		((UIElement)RefSelEngineers).Visibility = (Visibility)1;
		((UIElement)RefSelMonks).Visibility = (Visibility)1;
		((UIElement)RefSelLaddermen).Visibility = (Visibility)1;
		((UIElement)RefSelTunnelers).Visibility = (Visibility)1;
		((UIElement)RefSelCatapults).Visibility = (Visibility)1;
		((UIElement)RefSelTrebuchets).Visibility = (Visibility)1;
		((UIElement)RefSelRams).Visibility = (Visibility)1;
		((UIElement)RefSelSiegeTowers).Visibility = (Visibility)1;
		((UIElement)RefSelMantlets).Visibility = (Visibility)1;
		((UIElement)RefSelMangonels).Visibility = (Visibility)1;
		((UIElement)RefSelBalistae).Visibility = (Visibility)1;
		((UIElement)RefSelArabBow).Visibility = (Visibility)1;
		((UIElement)RefSelArabSlave).Visibility = (Visibility)1;
		((UIElement)RefSelArabSlinger).Visibility = (Visibility)1;
		((UIElement)RefSelArabAssassin).Visibility = (Visibility)1;
		((UIElement)RefSelArabHorseArcher).Visibility = (Visibility)1;
		((UIElement)RefSelArabSwordsman).Visibility = (Visibility)1;
		((UIElement)RefSelArabGrenadier).Visibility = (Visibility)1;
		((UIElement)RefSelArabBallista).Visibility = (Visibility)1;
		((UIElement)RefSelBedouinCamelLancerSelected).Visibility = (Visibility)1;
		((UIElement)RefSelBedouinHealerSelected).Visibility = (Visibility)1;
		((UIElement)RefSelBedouinEunuchSelected).Visibility = (Visibility)1;
		((UIElement)RefSelBedouinAmbusherSelected).Visibility = (Visibility)1;
		((UIElement)RefSelBedouinSkirmisherSelected).Visibility = (Visibility)1;
		((UIElement)RefSelBedouinHeavyCamelSelected).Visibility = (Visibility)1;
		((UIElement)RefSelBedouinSapperSelected).Visibility = (Visibility)1;
		((UIElement)RefSelBedouinDemolisherSelected).Visibility = (Visibility)1;
	}

	public void ShowSelectedTroopsNumber(int slot, int value)
	{
		switch (slot)
		{
		case 0:
			((UIElement)RefSelTroopNo1).Visibility = (Visibility)2;
			RefSelTroopNo1.Text = value.ToString();
			break;
		case 1:
			((UIElement)RefSelTroopNo2).Visibility = (Visibility)2;
			RefSelTroopNo2.Text = value.ToString();
			break;
		case 2:
			((UIElement)RefSelTroopNo3).Visibility = (Visibility)2;
			RefSelTroopNo3.Text = value.ToString();
			break;
		case 3:
			((UIElement)RefSelTroopNo4).Visibility = (Visibility)2;
			RefSelTroopNo4.Text = value.ToString();
			break;
		case 4:
			((UIElement)RefSelTroopNo5).Visibility = (Visibility)2;
			RefSelTroopNo5.Text = value.ToString();
			break;
		case 5:
			((UIElement)RefSelTroopNo6).Visibility = (Visibility)2;
			RefSelTroopNo6.Text = value.ToString();
			break;
		case 6:
			((UIElement)RefSelTroopNo7).Visibility = (Visibility)2;
			RefSelTroopNo7.Text = value.ToString();
			break;
		case 7:
			((UIElement)RefSelTroopNo8).Visibility = (Visibility)2;
			RefSelTroopNo8.Text = value.ToString();
			break;
		}
	}

	public void HideAllSelectedTroopsNumbers()
	{
		((UIElement)RefSelTroopNo1).Visibility = (Visibility)1;
		((UIElement)RefSelTroopNo2).Visibility = (Visibility)1;
		((UIElement)RefSelTroopNo3).Visibility = (Visibility)1;
		((UIElement)RefSelTroopNo4).Visibility = (Visibility)1;
		((UIElement)RefSelTroopNo5).Visibility = (Visibility)1;
		((UIElement)RefSelTroopNo6).Visibility = (Visibility)1;
		((UIElement)RefSelTroopNo7).Visibility = (Visibility)1;
		((UIElement)RefSelTroopNo8).Visibility = (Visibility)1;
	}

	public void SetuptroopActionsUI(bool fromInitialOpening = false)
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Expected O, but got Unknown
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Expected O, but got Unknown
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Invalid comparison between Unknown and I4
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.troops_show_disband > 0)
		{
			((UIElement)RefUnitDisband).Visibility = (Visibility)2;
		}
		else
		{
			((UIElement)RefUnitDisband).Visibility = (Visibility)1;
		}
		((UIElement)RefUnitStop).Visibility = (Visibility)2;
		((UIElement)RefUnitPatrol).Visibility = (Visibility)2;
		((UIElement)RefUnitPatrolActive).Visibility = (Visibility)1;
		PatrolShouldBeVisible = true;
		RefUnitReload = (Grid)((FrameworkElement)this).FindName("UnitReload");
		RefUnitFireCow = (Grid)((FrameworkElement)this).FindName("UnitFireCow");
		if (MainViewModel.Instance.IsMapEditorMode)
		{
			((UIElement)RefUnitAttackHere).Visibility = (Visibility)2;
			((UIElement)RefUnitTunnelHere).Visibility = (Visibility)1;
			((UIElement)RefUnitPourOil).Visibility = (Visibility)1;
			((UIElement)RefUnitBuild).Visibility = (Visibility)1;
			((UIElement)RefUnitFireCow).Visibility = (Visibility)1;
			((UIElement)RefUnitReload).Visibility = (Visibility)1;
			ShownEngineerbuildUI((Visibility)1);
			return;
		}
		ShownAttackHereOrder();
		ShownBuildSiegeKitOrder();
		ShowAmmoOrders();
		if (fromInitialOpening)
		{
			ShownEngineerbuildUI((Visibility)1);
			return;
		}
		if ((int)((UIElement)RefUnitBack).Visibility == 2)
		{
			((UIElement)RefUnitDisband).Visibility = (Visibility)1;
			((UIElement)RefUnitStop).Visibility = (Visibility)1;
			((UIElement)RefUnitPatrol).Visibility = (Visibility)1;
			((UIElement)RefUnitPatrolActive).Visibility = (Visibility)1;
			PatrolShouldBeVisible = false;
			((UIElement)RefUnitAttackHere).Visibility = (Visibility)1;
			((UIElement)RefUnitTunnelHere).Visibility = (Visibility)1;
			((UIElement)RefUnitPourOil).Visibility = (Visibility)1;
			((UIElement)RefUnitBuild).Visibility = (Visibility)1;
			((UIElement)RefUnitFireCow).Visibility = (Visibility)1;
			((UIElement)RefUnitReload).Visibility = (Visibility)1;
		}
		ShownEngineerbuildUI(((UIElement)RefUnitBack).Visibility);
	}

	public void SelectedEngiBuild(bool state)
	{
		if (state)
		{
			((UIElement)RefUnitDisband).Visibility = (Visibility)1;
			((UIElement)RefUnitStop).Visibility = (Visibility)1;
			((UIElement)RefUnitPatrol).Visibility = (Visibility)1;
			((UIElement)RefUnitPatrolActive).Visibility = (Visibility)1;
			PatrolShouldBeVisible = false;
			((UIElement)RefUnitAttackHere).Visibility = (Visibility)1;
			((UIElement)RefUnitTunnelHere).Visibility = (Visibility)1;
			((UIElement)RefUnitPourOil).Visibility = (Visibility)1;
			((UIElement)RefUnitBuild).Visibility = (Visibility)1;
			((UIElement)RefUnitFireCow).Visibility = (Visibility)1;
			((UIElement)RefUnitReload).Visibility = (Visibility)1;
			ShownEngineerbuildUI((Visibility)2);
		}
		else
		{
			SetuptroopActionsUI(fromInitialOpening: true);
		}
	}

	public void ShownAttackHereOrder()
	{
		if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.troops_show_attack_here_and_type > 0)
		{
			if (GameData.Instance.lastGameState.troops_show_attack_here_and_type == 1)
			{
				((UIElement)RefUnitAttackHere).Visibility = (Visibility)1;
				((UIElement)RefUnitPourOil).Visibility = (Visibility)2;
				((UIElement)RefUnitTunnelHere).Visibility = (Visibility)1;
			}
			else if (GameData.Instance.lastGameState.troops_show_attack_here_and_type == 2)
			{
				((UIElement)RefUnitAttackHere).Visibility = (Visibility)1;
				((UIElement)RefUnitPourOil).Visibility = (Visibility)1;
				((UIElement)RefUnitTunnelHere).Visibility = (Visibility)2;
			}
			else
			{
				((UIElement)RefUnitAttackHere).Visibility = (Visibility)2;
				((UIElement)RefUnitPourOil).Visibility = (Visibility)1;
				((UIElement)RefUnitTunnelHere).Visibility = (Visibility)1;
			}
		}
		else
		{
			((UIElement)RefUnitAttackHere).Visibility = (Visibility)1;
			((UIElement)RefUnitPourOil).Visibility = (Visibility)1;
			((UIElement)RefUnitTunnelHere).Visibility = (Visibility)1;
		}
	}

	public void ShownBuildSiegeKitOrder()
	{
		if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.troops_show_build_menu > 0)
		{
			((UIElement)RefUnitBuild).Visibility = (Visibility)2;
		}
		else
		{
			((UIElement)RefUnitBuild).Visibility = (Visibility)1;
		}
	}

	public void ShowAmmoOrders()
	{
		bool flag = true;
		if (Director.instance.MultiplayerGame && GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.MP_No_Cows)
		{
			flag = false;
		}
		if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.troops_show_launch_cow_and_num_cows > 0 && GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.troops_show_launch_cow_and_num_cows < 250 && flag)
		{
			((UIElement)RefUnitFireCow).Visibility = (Visibility)2;
			MainViewModel.Instance.CowAmmoLeft = GameData.Instance.lastGameState.troops_show_launch_cow_and_num_cows.ToString();
		}
		else
		{
			((UIElement)RefUnitFireCow).Visibility = (Visibility)1;
		}
		if (GameData.Instance.lastGameState.troops_show_attack_here_and_type == 3)
		{
			if (GameData.Instance.lastGameState.troops_show_attack_here_number_rocks == byte.MaxValue)
			{
				MainViewModel.Instance.AmmoLeft = "250+";
			}
			else
			{
				MainViewModel.Instance.AmmoLeft = GameData.Instance.lastGameState.troops_show_attack_here_number_rocks.ToString();
			}
		}
		else
		{
			MainViewModel.Instance.AmmoLeft = "";
		}
		if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.troops_show_get_ammo > 0)
		{
			((UIElement)RefUnitReload).Visibility = (Visibility)2;
		}
		else
		{
			((UIElement)RefUnitReload).Visibility = (Visibility)1;
		}
	}

	public void ShownEngineerbuildUI(Visibility thisVisibility)
	{
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		if (GameData.Instance.lastGameState != null)
		{
			if (GameData.Instance.lastGameState.troops_show_make_catapult > 0)
			{
				((UIElement)RefUnitBuildCat).Visibility = thisVisibility;
			}
			else
			{
				((UIElement)RefUnitBuildCat).Visibility = (Visibility)1;
			}
			if (GameData.Instance.lastGameState.troops_show_make_trebuchet > 0)
			{
				((UIElement)RefUnitBuildTreb).Visibility = thisVisibility;
			}
			else
			{
				((UIElement)RefUnitBuildTreb).Visibility = (Visibility)1;
			}
			if (GameData.Instance.lastGameState.troops_show_make_siege_tower > 0)
			{
				((UIElement)RefUnitBuildTower).Visibility = thisVisibility;
			}
			else
			{
				((UIElement)RefUnitBuildTower).Visibility = (Visibility)1;
			}
			if (GameData.Instance.lastGameState.troops_show_battering_ram > 0)
			{
				((UIElement)RefUnitBuildRam).Visibility = thisVisibility;
			}
			else
			{
				((UIElement)RefUnitBuildRam).Visibility = (Visibility)1;
			}
			if (GameData.Instance.lastGameState.troops_show_portable_shield > 0)
			{
				((UIElement)RefUnitbuildMantlet).Visibility = thisVisibility;
			}
			else
			{
				((UIElement)RefUnitbuildMantlet).Visibility = (Visibility)1;
			}
			if (GameData.Instance.lastGameState.troops_show_make_arab_ballista > 0)
			{
				((UIElement)RefUnitbuildArabBallista).Visibility = thisVisibility;
			}
			else
			{
				((UIElement)RefUnitbuildArabBallista).Visibility = (Visibility)1;
			}
		}
		else
		{
			((UIElement)RefUnitBuildCat).Visibility = (Visibility)1;
			((UIElement)RefUnitBuildTreb).Visibility = (Visibility)1;
			((UIElement)RefUnitBuildTower).Visibility = (Visibility)1;
			((UIElement)RefUnitBuildRam).Visibility = (Visibility)1;
			((UIElement)RefUnitbuildMantlet).Visibility = (Visibility)1;
			((UIElement)RefUnitbuildArabBallista).Visibility = (Visibility)1;
		}
		((UIElement)RefUnitBack).Visibility = thisVisibility;
	}
}
