using Noesis;

namespace CrusaderDE;

public class HUD_Scenario_Popup : UserControl
{
	public Grid RefViewRoot;

	public Button RefBorder;

	public TextBlock RefTextType;

	public TextBlock RefTextName;

	public TextBlock RefTextSize;

	public TextBlock RefMapSizeLabel;

	public Button RefButtonEditMapPlayer;

	public Button RefButtonEditSettings;

	public Button RefButtonEditMapSize160;

	public Button RefButtonEditMapSize200;

	public Button RefButtonEditMapSize300;

	public Button RefButtonEditMapSize400;

	public Button RefButtonEditMapSize500;

	public Button RefButtonEditMapSize600;

	public Button RefButtonEditMapSize700;

	public Button RefButtonEditMapSize800;

	public Grid RefScenarioPopupNormalButtons;

	public bool settingsOpened;

	public HUD_Scenario_Popup()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected O, but got Unknown
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected O, but got Unknown
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Expected O, but got Unknown
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Expected O, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Expected O, but got Unknown
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Expected O, but got Unknown
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Expected O, but got Unknown
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Expected O, but got Unknown
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Expected O, but got Unknown
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Expected O, but got Unknown
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Expected O, but got Unknown
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Expected O, but got Unknown
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Expected O, but got Unknown
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Expected O, but got Unknown
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Expected O, but got Unknown
		InitializeComponent();
		MainViewModel.Instance.HUDScenarioPopup = this;
		RefViewRoot = (Grid)((FrameworkElement)this).FindName("LayoutRoot");
		RefBorder = (Button)((FrameworkElement)this).FindName("ScenarioBorder");
		RefTextType = (TextBlock)((FrameworkElement)this).FindName("MapType");
		RefTextName = (TextBlock)((FrameworkElement)this).FindName("MapName");
		RefTextSize = (TextBlock)((FrameworkElement)this).FindName("MapSize");
		RefMapSizeLabel = (TextBlock)((FrameworkElement)this).FindName("MapSizeLabel");
		RefButtonEditMapPlayer = (Button)((FrameworkElement)this).FindName("ButtonEditMapPlayer");
		((Timeline)(Storyboard)((FrameworkElement)this).Resources[(object)"Outtro"]).Completed += (CompletedHandler)delegate
		{
			((UIElement)RefViewRoot).Visibility = (Visibility)1;
		};
		RefScenarioPopupNormalButtons = (Grid)((FrameworkElement)this).FindName("ScenarioPopupNormalButtons");
		RefButtonEditMapSize160 = (Button)((FrameworkElement)this).FindName("ButtonEditMapSize160");
		RefButtonEditMapSize200 = (Button)((FrameworkElement)this).FindName("ButtonEditMapSize200");
		RefButtonEditMapSize300 = (Button)((FrameworkElement)this).FindName("ButtonEditMapSize300");
		RefButtonEditMapSize400 = (Button)((FrameworkElement)this).FindName("ButtonEditMapSize400");
		RefButtonEditMapSize500 = (Button)((FrameworkElement)this).FindName("ButtonEditMapSize500");
		RefButtonEditMapSize600 = (Button)((FrameworkElement)this).FindName("ButtonEditMapSize600");
		RefButtonEditMapSize700 = (Button)((FrameworkElement)this).FindName("ButtonEditMapSize700");
		RefButtonEditMapSize800 = (Button)((FrameworkElement)this).FindName("ButtonEditMapSize800");
		RefButtonEditSettings = (Button)((FrameworkElement)this).FindName("ButtonEditSettings");
		if (FatControler.italian || FatControler.french)
		{
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefButtonEditSettings, 14);
			PropEx.SetGlowButtonTextHeight((UIElement)(object)RefButtonEditSettings, 20);
		}
		if (FatControler.ukrainian)
		{
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefButtonEditSettings, 15);
			PropEx.SetGlowButtonTextHeight((UIElement)(object)RefButtonEditSettings, 22);
		}
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_Scenario_Popup.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected O, but got Unknown
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		if (eventName == "MouseEnter" && handlerName == "CommonRedButtonEnter")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseEnter += new MouseEventHandler(MainViewModel.Instance.CommonRedButtonEnter);
			}
			else if (source is RadioButton)
			{
				((UIElement)(RadioButton)source).MouseEnter += new MouseEventHandler(MainViewModel.Instance.CommonRedButtonEnter);
			}
			return true;
		}
		return false;
	}

	public void Update()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		if (!settingsOpened && (int)((UIElement)RefViewRoot).Visibility == 2)
		{
			UpdateEditorTimeButton();
		}
	}

	public void StartEntryAnim()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		((UIElement)RefViewRoot).Visibility = (Visibility)2;
		((Storyboard)((FrameworkElement)this).Resources[(object)"Intro"]).Begin((FrameworkElement)(object)this);
		UpdateText();
		SetState(opened: false);
	}

	public void StartExitAnim()
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		((Storyboard)((FrameworkElement)this).Resources[(object)"Outtro"]).Begin((FrameworkElement)(object)this);
	}

	public void UpdateText()
	{
		RefTextName.Text = GameData.Instance.currentFileName;
		switch (GameMap.tilemapSize)
		{
		case 160:
			RefTextSize.Text = "160 x 160";
			break;
		case 200:
			RefTextSize.Text = "200 x 200";
			break;
		case 300:
			RefTextSize.Text = "300 x 300";
			break;
		case 400:
			RefTextSize.Text = "400 x 400";
			break;
		case 500:
			RefTextSize.Text = "500 x 500";
			break;
		case 600:
			RefTextSize.Text = "600 x 600";
			break;
		case 700:
			RefTextSize.Text = "700 x 700";
			break;
		case 800:
			RefTextSize.Text = "800 x 800";
			break;
		}
		string text = "";
		if (GameData.Instance.multiplayerMap)
		{
			text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 9);
		}
		else
		{
			switch (GameData.Instance.mapType)
			{
			case Enums.GameModes.BUILD:
				text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 4);
				break;
			case Enums.GameModes.ECO:
				text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 5);
				break;
			case Enums.GameModes.INVASION:
				text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 6);
				break;
			case Enums.GameModes.SIEGE:
				text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 7);
				break;
			}
		}
		RefTextType.Text = text;
	}

	public void EditSettingsClicked()
	{
		SetState(!settingsOpened);
	}

	public void Refresh()
	{
		SetState(settingsOpened);
	}

	public void SetState(bool opened)
	{
		settingsOpened = opened;
		if (!opened)
		{
			((UIElement)RefScenarioPopupNormalButtons).Visibility = (Visibility)2;
			Grid refViewRoot = RefViewRoot;
			Button refBorder = RefBorder;
			float num = (((FrameworkElement)this).Height = 335f);
			float height = (((FrameworkElement)refBorder).Height = num);
			((FrameworkElement)refViewRoot).Height = height;
			((UIElement)RefMapSizeLabel).Visibility = (Visibility)1;
			((UIElement)RefScenarioPopupNormalButtons).Visibility = (Visibility)2;
			((UIElement)RefButtonEditMapSize160).Visibility = (Visibility)1;
			((UIElement)RefButtonEditMapSize200).Visibility = (Visibility)1;
			((UIElement)RefButtonEditMapSize300).Visibility = (Visibility)1;
			((UIElement)RefButtonEditMapSize400).Visibility = (Visibility)1;
			((UIElement)RefButtonEditMapSize500).Visibility = (Visibility)1;
			((UIElement)RefButtonEditMapSize600).Visibility = (Visibility)1;
			((UIElement)RefButtonEditMapSize700).Visibility = (Visibility)1;
			((UIElement)RefButtonEditMapSize800).Visibility = (Visibility)1;
			MainViewModel.Instance.ButtonMapSettingsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 3);
			UpdateEditorTimeButton();
			return;
		}
		((UIElement)RefScenarioPopupNormalButtons).Visibility = (Visibility)0;
		MainViewModel.Instance.ButtonMapSettingsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 11);
		((UIElement)RefMapSizeLabel).Visibility = (Visibility)2;
		((UIElement)RefButtonEditMapSize160).Visibility = (Visibility)2;
		((UIElement)RefButtonEditMapSize200).Visibility = (Visibility)2;
		((UIElement)RefButtonEditMapSize300).Visibility = (Visibility)2;
		((UIElement)RefButtonEditMapSize400).Visibility = (Visibility)2;
		((UIElement)RefButtonEditMapSize500).Visibility = (Visibility)2;
		((UIElement)RefButtonEditMapSize600).Visibility = (Visibility)2;
		((UIElement)RefButtonEditMapSize700).Visibility = (Visibility)2;
		((UIElement)RefButtonEditMapSize800).Visibility = (Visibility)2;
		MainViewModel.Instance.ButBordScenEdit160HL = GameMap.tilemapSize == 160;
		MainViewModel.Instance.ButBordScenEdit200HL = GameMap.tilemapSize == 200;
		MainViewModel.Instance.ButBordScenEdit300HL = GameMap.tilemapSize == 300;
		MainViewModel.Instance.ButBordScenEdit400HL = GameMap.tilemapSize == 400;
		MainViewModel.Instance.ButBordScenEdit500HL = GameMap.tilemapSize == 500;
		MainViewModel.Instance.ButBordScenEdit600HL = GameMap.tilemapSize == 600;
		MainViewModel.Instance.ButBordScenEdit700HL = GameMap.tilemapSize == 700;
		MainViewModel.Instance.ButBordScenEdit800HL = GameMap.tilemapSize == 800;
		if (GameData.Instance.multiplayerMap)
		{
			Grid refViewRoot2 = RefViewRoot;
			Button refBorder2 = RefBorder;
			float num = (((FrameworkElement)this).Height = 435f);
			float height = (((FrameworkElement)refBorder2).Height = num);
			((FrameworkElement)refViewRoot2).Height = height;
			MainViewModel.Instance.ButtonScenarioEditSPMP = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAPEDIT, Enums.eTextValues.TEXT_SCN_TAUNT);
			MainViewModel.Instance.ButtonScenarioEditSinglePlayerVis = false;
			MainViewModel.Instance.ButtonScenarioEditMultiPlayerVis = true;
		}
		else
		{
			Grid refViewRoot3 = RefViewRoot;
			Button refBorder3 = RefBorder;
			float num = (((FrameworkElement)this).Height = 500f);
			float height = (((FrameworkElement)refBorder3).Height = num);
			((FrameworkElement)refViewRoot3).Height = height;
			MainViewModel.Instance.ButtonScenarioEditSPMP = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAPEDIT, Enums.eTextValues.TEXT_SCN_WOLF);
			MainViewModel.Instance.ButtonScenarioEditSinglePlayerVis = true;
			MainViewModel.Instance.ButtonScenarioEditMultiPlayerVis = false;
			MainViewModel.Instance.ButBordScenEditSPInvasion = GameData.Instance.mapType == Enums.GameModes.INVASION;
			MainViewModel.Instance.ButBordScenEditSPFreeBuild = GameData.Instance.mapType == Enums.GameModes.BUILD;
		}
		UpdateText();
	}

	public void UpdateEditorTimeButton()
	{
		if (GameData.Instance.lastGameState != null)
		{
			if (GameData.Instance.lastGameState.editor_time_paused == 0)
			{
				MainViewModel.Instance.ScenarioPopup_GameTimeText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 82);
			}
			else
			{
				MainViewModel.Instance.ScenarioPopup_GameTimeText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 83);
			}
			if (GameData.Instance.lastGameState.balanced == 0)
			{
				MainViewModel.Instance.ScenarioPopup_BalancedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 411);
			}
			else
			{
				MainViewModel.Instance.ScenarioPopup_BalancedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 412);
			}
		}
	}
}
