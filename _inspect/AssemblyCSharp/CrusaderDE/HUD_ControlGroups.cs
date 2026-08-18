using Noesis;

namespace CrusaderDE;

public class HUD_ControlGroups : UserControl
{
	private Button[] RefSelectButtons = new Button[10];

	private Button[] RefCreateButtons = new Button[10];

	private Button[] RefAddButtons = new Button[10];

	private Button[] RefDeleteButtons = new Button[10];

	private Image[,] RefTroopImages = new Image[10, 4];

	private TextBlock[,] RefTroopValues = new TextBlock[10, 4];

	private TextBlock[] RefTroopExtraValues = new TextBlock[10];

	private TextBlock[] RefTroopRowID = new TextBlock[10];

	private static SolidColorBrush RowIDColour_Black = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 0, 0, 0));

	private static SolidColorBrush RowIDColour_Light = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 239, 243, 198));

	public HUD_ControlGroups()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDControlGroups = this;
		RefSelectButtons[0] = (Button)FindName("CG0_Select");
		RefSelectButtons[1] = (Button)FindName("CG1_Select");
		RefSelectButtons[2] = (Button)FindName("CG2_Select");
		RefSelectButtons[3] = (Button)FindName("CG3_Select");
		RefSelectButtons[4] = (Button)FindName("CG4_Select");
		RefSelectButtons[5] = (Button)FindName("CG5_Select");
		RefSelectButtons[6] = (Button)FindName("CG6_Select");
		RefSelectButtons[7] = (Button)FindName("CG7_Select");
		RefSelectButtons[8] = (Button)FindName("CG8_Select");
		RefSelectButtons[9] = (Button)FindName("CG9_Select");
		RefCreateButtons[0] = (Button)FindName("CG0_Create");
		RefCreateButtons[1] = (Button)FindName("CG1_Create");
		RefCreateButtons[2] = (Button)FindName("CG2_Create");
		RefCreateButtons[3] = (Button)FindName("CG3_Create");
		RefCreateButtons[4] = (Button)FindName("CG4_Create");
		RefCreateButtons[5] = (Button)FindName("CG5_Create");
		RefCreateButtons[6] = (Button)FindName("CG6_Create");
		RefCreateButtons[7] = (Button)FindName("CG7_Create");
		RefCreateButtons[8] = (Button)FindName("CG8_Create");
		RefCreateButtons[9] = (Button)FindName("CG9_Create");
		RefAddButtons[0] = (Button)FindName("CG0_Add");
		RefAddButtons[1] = (Button)FindName("CG1_Add");
		RefAddButtons[2] = (Button)FindName("CG2_Add");
		RefAddButtons[3] = (Button)FindName("CG3_Add");
		RefAddButtons[4] = (Button)FindName("CG4_Add");
		RefAddButtons[5] = (Button)FindName("CG5_Add");
		RefAddButtons[6] = (Button)FindName("CG6_Add");
		RefAddButtons[7] = (Button)FindName("CG7_Add");
		RefAddButtons[8] = (Button)FindName("CG8_Add");
		RefAddButtons[9] = (Button)FindName("CG9_Add");
		RefDeleteButtons[0] = (Button)FindName("CG0_Delete");
		RefDeleteButtons[1] = (Button)FindName("CG1_Delete");
		RefDeleteButtons[2] = (Button)FindName("CG2_Delete");
		RefDeleteButtons[3] = (Button)FindName("CG3_Delete");
		RefDeleteButtons[4] = (Button)FindName("CG4_Delete");
		RefDeleteButtons[5] = (Button)FindName("CG5_Delete");
		RefDeleteButtons[6] = (Button)FindName("CG6_Delete");
		RefDeleteButtons[7] = (Button)FindName("CG7_Delete");
		RefDeleteButtons[8] = (Button)FindName("CG8_Delete");
		RefDeleteButtons[9] = (Button)FindName("CG9_Delete");
		RefTroopImages[0, 0] = (Image)FindName("CG0_TroopImage1");
		RefTroopImages[0, 1] = (Image)FindName("CG0_TroopImage2");
		RefTroopImages[0, 2] = (Image)FindName("CG0_TroopImage3");
		RefTroopImages[0, 3] = (Image)FindName("CG0_TroopImage4");
		RefTroopImages[1, 0] = (Image)FindName("CG1_TroopImage1");
		RefTroopImages[1, 1] = (Image)FindName("CG1_TroopImage2");
		RefTroopImages[1, 2] = (Image)FindName("CG1_TroopImage3");
		RefTroopImages[1, 3] = (Image)FindName("CG1_TroopImage4");
		RefTroopImages[2, 0] = (Image)FindName("CG2_TroopImage1");
		RefTroopImages[2, 1] = (Image)FindName("CG2_TroopImage2");
		RefTroopImages[2, 2] = (Image)FindName("CG2_TroopImage3");
		RefTroopImages[2, 3] = (Image)FindName("CG2_TroopImage4");
		RefTroopImages[3, 0] = (Image)FindName("CG3_TroopImage1");
		RefTroopImages[3, 1] = (Image)FindName("CG3_TroopImage2");
		RefTroopImages[3, 2] = (Image)FindName("CG3_TroopImage3");
		RefTroopImages[3, 3] = (Image)FindName("CG3_TroopImage4");
		RefTroopImages[4, 0] = (Image)FindName("CG4_TroopImage1");
		RefTroopImages[4, 1] = (Image)FindName("CG4_TroopImage2");
		RefTroopImages[4, 2] = (Image)FindName("CG4_TroopImage3");
		RefTroopImages[4, 3] = (Image)FindName("CG4_TroopImage4");
		RefTroopImages[5, 0] = (Image)FindName("CG5_TroopImage1");
		RefTroopImages[5, 1] = (Image)FindName("CG5_TroopImage2");
		RefTroopImages[5, 2] = (Image)FindName("CG5_TroopImage3");
		RefTroopImages[5, 3] = (Image)FindName("CG5_TroopImage4");
		RefTroopImages[6, 0] = (Image)FindName("CG6_TroopImage1");
		RefTroopImages[6, 1] = (Image)FindName("CG6_TroopImage2");
		RefTroopImages[6, 2] = (Image)FindName("CG6_TroopImage3");
		RefTroopImages[6, 3] = (Image)FindName("CG6_TroopImage4");
		RefTroopImages[7, 0] = (Image)FindName("CG7_TroopImage1");
		RefTroopImages[7, 1] = (Image)FindName("CG7_TroopImage2");
		RefTroopImages[7, 2] = (Image)FindName("CG7_TroopImage3");
		RefTroopImages[7, 3] = (Image)FindName("CG7_TroopImage4");
		RefTroopImages[8, 0] = (Image)FindName("CG8_TroopImage1");
		RefTroopImages[8, 1] = (Image)FindName("CG8_TroopImage2");
		RefTroopImages[8, 2] = (Image)FindName("CG8_TroopImage3");
		RefTroopImages[8, 3] = (Image)FindName("CG8_TroopImage4");
		RefTroopImages[9, 0] = (Image)FindName("CG9_TroopImage1");
		RefTroopImages[9, 1] = (Image)FindName("CG9_TroopImage2");
		RefTroopImages[9, 2] = (Image)FindName("CG9_TroopImage3");
		RefTroopImages[9, 3] = (Image)FindName("CG9_TroopImage4");
		RefTroopValues[0, 0] = (TextBlock)FindName("CG0_TroopCount1");
		RefTroopValues[0, 1] = (TextBlock)FindName("CG0_TroopCount2");
		RefTroopValues[0, 2] = (TextBlock)FindName("CG0_TroopCount3");
		RefTroopValues[0, 3] = (TextBlock)FindName("CG0_TroopCount4");
		RefTroopValues[1, 0] = (TextBlock)FindName("CG1_TroopCount1");
		RefTroopValues[1, 1] = (TextBlock)FindName("CG1_TroopCount2");
		RefTroopValues[1, 2] = (TextBlock)FindName("CG1_TroopCount3");
		RefTroopValues[1, 3] = (TextBlock)FindName("CG1_TroopCount4");
		RefTroopValues[2, 0] = (TextBlock)FindName("CG2_TroopCount1");
		RefTroopValues[2, 1] = (TextBlock)FindName("CG2_TroopCount2");
		RefTroopValues[2, 2] = (TextBlock)FindName("CG2_TroopCount3");
		RefTroopValues[2, 3] = (TextBlock)FindName("CG2_TroopCount4");
		RefTroopValues[3, 0] = (TextBlock)FindName("CG3_TroopCount1");
		RefTroopValues[3, 1] = (TextBlock)FindName("CG3_TroopCount2");
		RefTroopValues[3, 2] = (TextBlock)FindName("CG3_TroopCount3");
		RefTroopValues[3, 3] = (TextBlock)FindName("CG3_TroopCount4");
		RefTroopValues[4, 0] = (TextBlock)FindName("CG4_TroopCount1");
		RefTroopValues[4, 1] = (TextBlock)FindName("CG4_TroopCount2");
		RefTroopValues[4, 2] = (TextBlock)FindName("CG4_TroopCount3");
		RefTroopValues[4, 3] = (TextBlock)FindName("CG4_TroopCount4");
		RefTroopValues[5, 0] = (TextBlock)FindName("CG5_TroopCount1");
		RefTroopValues[5, 1] = (TextBlock)FindName("CG5_TroopCount2");
		RefTroopValues[5, 2] = (TextBlock)FindName("CG5_TroopCount3");
		RefTroopValues[5, 3] = (TextBlock)FindName("CG5_TroopCount4");
		RefTroopValues[6, 0] = (TextBlock)FindName("CG6_TroopCount1");
		RefTroopValues[6, 1] = (TextBlock)FindName("CG6_TroopCount2");
		RefTroopValues[6, 2] = (TextBlock)FindName("CG6_TroopCount3");
		RefTroopValues[6, 3] = (TextBlock)FindName("CG6_TroopCount4");
		RefTroopValues[7, 0] = (TextBlock)FindName("CG7_TroopCount1");
		RefTroopValues[7, 1] = (TextBlock)FindName("CG7_TroopCount2");
		RefTroopValues[7, 2] = (TextBlock)FindName("CG7_TroopCount3");
		RefTroopValues[7, 3] = (TextBlock)FindName("CG7_TroopCount4");
		RefTroopValues[8, 0] = (TextBlock)FindName("CG8_TroopCount1");
		RefTroopValues[8, 1] = (TextBlock)FindName("CG8_TroopCount2");
		RefTroopValues[8, 2] = (TextBlock)FindName("CG8_TroopCount3");
		RefTroopValues[8, 3] = (TextBlock)FindName("CG8_TroopCount4");
		RefTroopValues[9, 0] = (TextBlock)FindName("CG9_TroopCount1");
		RefTroopValues[9, 1] = (TextBlock)FindName("CG9_TroopCount2");
		RefTroopValues[9, 2] = (TextBlock)FindName("CG9_TroopCount3");
		RefTroopValues[9, 3] = (TextBlock)FindName("CG9_TroopCount4");
		RefTroopExtraValues[0] = (TextBlock)FindName("CG0_TroopRemainder");
		RefTroopExtraValues[1] = (TextBlock)FindName("CG1_TroopRemainder");
		RefTroopExtraValues[2] = (TextBlock)FindName("CG2_TroopRemainder");
		RefTroopExtraValues[3] = (TextBlock)FindName("CG3_TroopRemainder");
		RefTroopExtraValues[4] = (TextBlock)FindName("CG4_TroopRemainder");
		RefTroopExtraValues[5] = (TextBlock)FindName("CG5_TroopRemainder");
		RefTroopExtraValues[6] = (TextBlock)FindName("CG6_TroopRemainder");
		RefTroopExtraValues[7] = (TextBlock)FindName("CG7_TroopRemainder");
		RefTroopExtraValues[8] = (TextBlock)FindName("CG8_TroopRemainder");
		RefTroopExtraValues[9] = (TextBlock)FindName("CG9_TroopRemainder");
		RefTroopRowID[0] = (TextBlock)FindName("CG0_Number");
		RefTroopRowID[1] = (TextBlock)FindName("CG1_Number");
		RefTroopRowID[2] = (TextBlock)FindName("CG2_Number");
		RefTroopRowID[3] = (TextBlock)FindName("CG3_Number");
		RefTroopRowID[4] = (TextBlock)FindName("CG4_Number");
		RefTroopRowID[5] = (TextBlock)FindName("CG5_Number");
		RefTroopRowID[6] = (TextBlock)FindName("CG6_Number");
		RefTroopRowID[7] = (TextBlock)FindName("CG7_Number");
		RefTroopRowID[8] = (TextBlock)FindName("CG8_Number");
		RefTroopRowID[9] = (TextBlock)FindName("CG9_Number");
	}

	public static void ToggleMenu()
	{
		if (MainViewModel.Instance.Show_HUD_ControlGroups)
		{
			MainViewModel.Instance.Show_HUD_ControlGroups = false;
			return;
		}
		if (MainViewModel.Instance.Show_HUD_LoadSaveRequester)
		{
			MainViewModel.Instance.HUDLoadSaveRequester.CloseRequester();
		}
		if (MainViewModel.Instance.Show_HUD_Confirmation)
		{
			MainViewModel.Instance.HUDConfirmationPopup.ConfirmationClicked(2);
		}
		if (MainViewModel.Instance.Show_HUD_IngameMenu)
		{
			MainViewModel.Instance.HUDmain.InGameOptions(null, null);
		}
		MainViewModel.Instance.HUDControlGroups.Init();
	}

	public void Init()
	{
		MainViewModel.Instance.Show_HUD_ControlGroups = true;
		populate();
	}

	public void Update()
	{
		populate();
	}

	private ImageSource GetTroopSprite(int type)
	{
		return type switch
		{
			0 => MainViewModel.Instance.UIBuildingsO001, 
			1 => MainViewModel.Instance.UIBuildingsO009, 
			2 => MainViewModel.Instance.UIBuildingsO003, 
			3 => MainViewModel.Instance.UIBuildingsO005, 
			4 => MainViewModel.Instance.UIBuildingsO007, 
			5 => MainViewModel.Instance.UIBuildingsO011, 
			6 => MainViewModel.Instance.UIBuildingsO013, 
			7 => MainViewModel.Instance.UIBuildingsO015, 
			8 => MainViewModel.Instance.UIBuildingsO017, 
			9 => MainViewModel.Instance.UIBuildingsO021, 
			10 => MainViewModel.Instance.UIBuildingsO023, 
			11 => MainViewModel.Instance.UIBuildingsO025, 
			12 => MainViewModel.Instance.GameSprites[295], 
			13 => MainViewModel.Instance.UIBuildingsO029, 
			14 => MainViewModel.Instance.UIBuildingsO027, 
			15 => MainViewModel.Instance.UIBuildingsO031, 
			16 => MainViewModel.Instance.UIBuildingsM005, 
			17 => MainViewModel.Instance.UIBuildingsO035, 
			18 => MainViewModel.Instance.UIBuildingsO037, 
			19 => MainViewModel.Instance.UIBuildingsO039, 
			20 => MainViewModel.Instance.UIBuildingsO041, 
			21 => MainViewModel.Instance.UIBuildingsO043, 
			22 => MainViewModel.Instance.UIBuildingsO045, 
			23 => MainViewModel.Instance.UIBuildingsO047, 
			24 => MainViewModel.Instance.UIBuildingsO049, 
			25 => MainViewModel.Instance.UIBuildingsO051, 
			26 => MainViewModel.Instance.UIBuildingsO053, 
			27 => MainViewModel.Instance.UIBuildingsO055, 
			28 => MainViewModel.Instance.UIBuildingsO057, 
			29 => MainViewModel.Instance.UIBuildingsO059, 
			30 => MainViewModel.Instance.UIBuildingsO061, 
			31 => MainViewModel.Instance.UIBuildingsO063, 
			32 => MainViewModel.Instance.UIBuildingsO065, 
			33 => MainViewModel.Instance.UIBuildingsO033, 
			_ => null, 
		};
	}

	private void populate()
	{
		if (GameData.Instance.lastGameState == null)
		{
			return;
		}
		EngineInterface.PlayState lastGameState = GameData.Instance.lastGameState;
		if (lastGameState.control_groups_total.Length <= 1)
		{
			return;
		}
		for (int i = 0; i < 10; i++)
		{
			if (lastGameState.control_groups_total[i] > 0)
			{
				PropEx.SetButtonVisibility(RefDeleteButtons[i], Visibility.Visible);
				PropEx.SetButtonVisibility(RefSelectButtons[i], Visibility.Visible);
				RefTroopRowID[i].Foreground = RowIDColour_Black;
				int num = lastGameState.control_groups_count[i * 4] + lastGameState.control_groups_count[i * 4 + 1] + lastGameState.control_groups_count[i * 4 + 2] + lastGameState.control_groups_count[i * 4 + 3];
				if (num != lastGameState.control_groups_total[i])
				{
					RefTroopExtraValues[i].Text = "+" + (lastGameState.control_groups_total[i] - num);
					RefTroopExtraValues[i].Visibility = Visibility.Visible;
				}
				else
				{
					RefTroopExtraValues[i].Visibility = Visibility.Hidden;
				}
				for (int j = 0; j < 4; j++)
				{
					if (lastGameState.control_groups_count[i * 4 + j] > 0)
					{
						RefTroopImages[i, j].Visibility = Visibility.Visible;
						RefTroopImages[i, j].Source = GetTroopSprite(lastGameState.control_groups_type[i * 4 + j]);
						RefTroopValues[i, j].Text = lastGameState.control_groups_count[i * 4 + j].ToString();
						RefTroopValues[i, j].Visibility = Visibility.Visible;
					}
					else
					{
						RefTroopImages[i, j].Visibility = Visibility.Hidden;
						RefTroopValues[i, j].Visibility = Visibility.Hidden;
					}
				}
			}
			else
			{
				RefTroopImages[i, 0].Visibility = Visibility.Hidden;
				RefTroopImages[i, 1].Visibility = Visibility.Hidden;
				RefTroopImages[i, 2].Visibility = Visibility.Hidden;
				RefTroopImages[i, 3].Visibility = Visibility.Hidden;
				RefTroopValues[i, 0].Visibility = Visibility.Hidden;
				RefTroopValues[i, 1].Visibility = Visibility.Hidden;
				RefTroopValues[i, 2].Visibility = Visibility.Hidden;
				RefTroopValues[i, 3].Visibility = Visibility.Hidden;
				PropEx.SetButtonVisibility(RefDeleteButtons[i], Visibility.Hidden);
				PropEx.SetButtonVisibility(RefSelectButtons[i], Visibility.Hidden);
				RefTroopExtraValues[i].Visibility = Visibility.Hidden;
				RefTroopRowID[i].Foreground = RowIDColour_Light;
			}
		}
	}

	public void ButtonClicked(string command)
	{
		switch (command)
		{
		case "Select_1":
		case "Select_2":
		case "Select_3":
		case "Select_4":
		case "Select_5":
		case "Select_6":
		case "Select_7":
		case "Select_8":
		case "Select_9":
		case "Select_0":
			switch (command)
			{
			case "Select_1":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan1);
				break;
			case "Select_2":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan2);
				break;
			case "Select_3":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan3);
				break;
			case "Select_4":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan4);
				break;
			case "Select_5":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan5);
				break;
			case "Select_6":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan6);
				break;
			case "Select_7":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan7);
				break;
			case "Select_8":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan8);
				break;
			case "Select_9":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan9);
				break;
			case "Select_0":
				EngineInterface.GameAction(Enums.KeyFunctions.SelectClan0);
				break;
			}
			break;
		case "Create_1":
		case "Create_2":
		case "Create_3":
		case "Create_4":
		case "Create_5":
		case "Create_6":
		case "Create_7":
		case "Create_8":
		case "Create_9":
		case "Create_0":
			switch (command)
			{
			case "Create_1":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops1);
				break;
			case "Create_2":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops2);
				break;
			case "Create_3":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops3);
				break;
			case "Create_4":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops4);
				break;
			case "Create_5":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops5);
				break;
			case "Create_6":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops6);
				break;
			case "Create_7":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops7);
				break;
			case "Create_8":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops8);
				break;
			case "Create_9":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops9);
				break;
			case "Create_0":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops0);
				break;
			}
			break;
		case "Add_1":
		case "Add_2":
		case "Add_3":
		case "Add_4":
		case "Add_5":
		case "Add_6":
		case "Add_7":
		case "Add_8":
		case "Add_9":
		case "Add_0":
			switch (command)
			{
			case "Add_1":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops1, 10);
				break;
			case "Add_2":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops2, 10);
				break;
			case "Add_3":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops3, 10);
				break;
			case "Add_4":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops4, 10);
				break;
			case "Add_5":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops5, 10);
				break;
			case "Add_6":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops6, 10);
				break;
			case "Add_7":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops7, 10);
				break;
			case "Add_8":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops8, 10);
				break;
			case "Add_9":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops9, 10);
				break;
			case "Add_0":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops0, 10);
				break;
			}
			break;
		case "Delete_1":
		case "Delete_2":
		case "Delete_3":
		case "Delete_4":
		case "Delete_5":
		case "Delete_6":
		case "Delete_7":
		case "Delete_8":
		case "Delete_9":
		case "Delete_0":
			switch (command)
			{
			case "Delete_1":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops1, 20);
				break;
			case "Delete_2":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops2, 20);
				break;
			case "Delete_3":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops3, 20);
				break;
			case "Delete_4":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops4, 20);
				break;
			case "Delete_5":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops5, 20);
				break;
			case "Delete_6":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops6, 20);
				break;
			case "Delete_7":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops7, 20);
				break;
			case "Delete_8":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops8, 20);
				break;
			case "Delete_9":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops9, 20);
				break;
			case "Delete_0":
				EngineInterface.GameAction(Enums.KeyFunctions.GroupTroops0, 20);
				break;
			}
			break;
		}
	}

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_ControlGroups.xaml");
	}
}
