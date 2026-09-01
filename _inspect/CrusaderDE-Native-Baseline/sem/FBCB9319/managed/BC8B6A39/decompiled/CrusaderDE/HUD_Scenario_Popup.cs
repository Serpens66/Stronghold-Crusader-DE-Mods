using Noesis;

namespace CrusaderDE;

public class HUD_Scenario_Popup : UserControl
{
	private Grid RefViewRoot;

	private Button RefBorder;

	private TextBlock RefTextType;

	private TextBlock RefTextName;

	private TextBlock RefTextSize;

	private TextBlock RefMapSizeLabel;

	private Button RefButtonEditMapPlayer;

	private Button RefButtonEditSettings;

	private Button RefButtonEditMapSize160;

	private Button RefButtonEditMapSize200;

	private Button RefButtonEditMapSize300;

	private Button RefButtonEditMapSize400;

	private Button RefButtonEditMapSize500;

	private Button RefButtonEditMapSize600;

	private Button RefButtonEditMapSize700;

	private Button RefButtonEditMapSize800;

	private Grid RefScenarioPopupNormalButtons;

	private bool settingsOpened;

	public HUD_Scenario_Popup()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDScenarioPopup = this;
		RefViewRoot = (Grid)FindName("LayoutRoot");
		RefBorder = (Button)FindName("ScenarioBorder");
		RefTextType = (TextBlock)FindName("MapType");
		RefTextName = (TextBlock)FindName("MapName");
		RefTextSize = (TextBlock)FindName("MapSize");
		RefMapSizeLabel = (TextBlock)FindName("MapSizeLabel");
		RefButtonEditMapPlayer = (Button)FindName("ButtonEditMapPlayer");
		((Storyboard)base.Resources["Outtro"]).Completed += delegate
		{
			RefViewRoot.Visibility = Visibility.Hidden;
		};
		RefScenarioPopupNormalButtons = (Grid)FindName("ScenarioPopupNormalButtons");
		RefButtonEditMapSize160 = (Button)FindName("ButtonEditMapSize160");
		RefButtonEditMapSize200 = (Button)FindName("ButtonEditMapSize200");
		RefButtonEditMapSize300 = (Button)FindName("ButtonEditMapSize300");
		RefButtonEditMapSize400 = (Button)FindName("ButtonEditMapSize400");
		RefButtonEditMapSize500 = (Button)FindName("ButtonEditMapSize500");
		RefButtonEditMapSize600 = (Button)FindName("ButtonEditMapSize600");
		RefButtonEditMapSize700 = (Button)FindName("ButtonEditMapSize700");
		RefButtonEditMapSize800 = (Button)FindName("ButtonEditMapSize800");
		RefButtonEditSettings = (Button)FindName("ButtonEditSettings");
		if (FatControler.italian || FatControler.french)
		{
			PropEx.SetGlowButtonFontSize(RefButtonEditSettings, 14);
			PropEx.SetGlowButtonTextHeight(RefButtonEditSettings, 20);
		}
		if (FatControler.ukrainian)
		{
			PropEx.SetGlowButtonFontSize(RefButtonEditSettings, 15);
			PropEx.SetGlowButtonTextHeight(RefButtonEditSettings, 22);
		}
	}

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_Scenario_Popup.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "MouseEnter" && handlerName == "CommonRedButtonEnter")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MainViewModel.Instance.CommonRedButtonEnter;
			}
			else if (source is RadioButton)
			{
				((RadioButton)source).MouseEnter += MainViewModel.Instance.CommonRedButtonEnter;
			}
			return true;
		}
		return false;
	}

	public void Update()
	{
		if (!settingsOpened && RefViewRoot.Visibility == Visibility.Visible)
		{
			UpdateEditorTimeButton();
		}
	}

	public void StartEntryAnim()
	{
		RefViewRoot.Visibility = Visibility.Visible;
		((Storyboard)base.Resources["Intro"]).Begin(this);
		UpdateText();
		SetState(opened: false);
	}

	public void StartExitAnim()
	{
		((Storyboard)base.Resources["Outtro"]).Begin(this);
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

	private void SetState(bool opened)
	{
		settingsOpened = opened;
		if (!opened)
		{
			RefScenarioPopupNormalButtons.Visibility = Visibility.Visible;
			Grid refViewRoot = RefViewRoot;
			Button refBorder = RefBorder;
			float num = (base.Height = 335f);
			float height = (refBorder.Height = num);
			refViewRoot.Height = height;
			RefMapSizeLabel.Visibility = Visibility.Hidden;
			RefScenarioPopupNormalButtons.Visibility = Visibility.Visible;
			RefButtonEditMapSize160.Visibility = Visibility.Hidden;
			RefButtonEditMapSize200.Visibility = Visibility.Hidden;
			RefButtonEditMapSize300.Visibility = Visibility.Hidden;
			RefButtonEditMapSize400.Visibility = Visibility.Hidden;
			RefButtonEditMapSize500.Visibility = Visibility.Hidden;
			RefButtonEditMapSize600.Visibility = Visibility.Hidden;
			RefButtonEditMapSize700.Visibility = Visibility.Hidden;
			RefButtonEditMapSize800.Visibility = Visibility.Hidden;
			MainViewModel.Instance.ButtonMapSettingsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 3);
			UpdateEditorTimeButton();
			return;
		}
		RefScenarioPopupNormalButtons.Visibility = Visibility.Collapsed;
		MainViewModel.Instance.ButtonMapSettingsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 11);
		RefMapSizeLabel.Visibility = Visibility.Visible;
		RefButtonEditMapSize160.Visibility = Visibility.Visible;
		RefButtonEditMapSize200.Visibility = Visibility.Visible;
		RefButtonEditMapSize300.Visibility = Visibility.Visible;
		RefButtonEditMapSize400.Visibility = Visibility.Visible;
		RefButtonEditMapSize500.Visibility = Visibility.Visible;
		RefButtonEditMapSize600.Visibility = Visibility.Visible;
		RefButtonEditMapSize700.Visibility = Visibility.Visible;
		RefButtonEditMapSize800.Visibility = Visibility.Visible;
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
			float num = (base.Height = 435f);
			float height = (refBorder2.Height = num);
			refViewRoot2.Height = height;
			MainViewModel.Instance.ButtonScenarioEditSPMP = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAPEDIT, Enums.eTextValues.TEXT_SCN_TAUNT);
			MainViewModel.Instance.ButtonScenarioEditSinglePlayerVis = false;
			MainViewModel.Instance.ButtonScenarioEditMultiPlayerVis = true;
		}
		else
		{
			Grid refViewRoot3 = RefViewRoot;
			Button refBorder3 = RefBorder;
			float num = (base.Height = 500f);
			float height = (refBorder3.Height = num);
			refViewRoot3.Height = height;
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
