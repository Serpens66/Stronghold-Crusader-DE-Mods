using Noesis;

namespace CrusaderDE;

public class FRONT_CoopTrail4 : UserControl
{
	public class PlayerRow
	{
		public Grid RefRow;

		public Image RefColour;

		public TextBlock RefName;

		public Image RefReadyState;

		public int playerID;

		public void Clear()
		{
			((UIElement)RefRow).Visibility = (Visibility)1;
		}

		public void Update(FRONT_Multiplayer parent, Platform_Multiplayer.MPLobbyMember member, int row, int player)
		{
			playerID = player;
			if (member == null)
			{
				Clear();
				return;
			}
			FRONT_Multiplayer.SetVisibility((UIElement)(object)RefRow, (Visibility)2);
			string combinedName = member.CombinedName;
			if (RefName.Text != combinedName)
			{
				RefName.Text = combinedName;
			}
			if ((BaseComponent)(object)RefReadyState != (BaseComponent)null)
			{
				if (member.ready || MainViewModel.Instance.FRONTMultiplayer.singlePlayerCoop)
				{
					ImageSource val = MainViewModel.Instance.GameSprites[105];
					if ((BaseComponent)(object)RefReadyState.Source != (BaseComponent)(object)val)
					{
						RefReadyState.Source = val;
					}
				}
				else
				{
					ImageSource val2 = MainViewModel.Instance.GameSprites[103];
					if ((BaseComponent)(object)RefReadyState.Source != (BaseComponent)(object)val2)
					{
						RefReadyState.Source = val2;
					}
				}
			}
			ImageSource colourShield = FRONT_Multiplayer.GetColourShield(member.colourID);
			if ((BaseComponent)(object)RefColour.Source != (BaseComponent)(object)colourShield)
			{
				RefColour.Source = colourShield;
			}
			parent.currentLobby.getTeam(member).ToString();
		}
	}

	public static FRONT_CoopTrail4 Instance;

	public Point MousePosition;

	public Grid refmapgrid;

	public TextBox RefMP_ChatInput;

	public TextBlock RefMP_ChatDisplay;

	public ScrollViewer RefMP_ChatScrollView;

	public Button RefRadarShield1;

	public Button RefRadarShield2;

	public Button RefRadarShield3;

	public Button RefRadarShield4;

	public Button RefRadarShield5;

	public Button RefRadarShield6;

	public Button RefRadarShield7;

	public Button RefRadarShield8;

	public TextBox RefMP_EnterShareCodeText;

	public Button RefShareJoinButton;

	public Button RefCoopCustomGame;

	public Button RefMultiplayerPlayButton;

	public Button RefReadyButton;

	public Button RefReadyButtonLock;

	public Button RefOptionsButton;

	public Button RefLoadButton;

	public Slider RefMP_Settings_GameSpeed_Slider;

	public PlayerRow[] playerRows = new PlayerRow[8];

	public CheckBox RefShowHidden;

	public UIElement SelectedObject { get; set; }

	public FRONT_CoopTrail4()
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Expected O, but got Unknown
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Expected O, but got Unknown
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Expected O, but got Unknown
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Expected O, but got Unknown
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Expected O, but got Unknown
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Expected O, but got Unknown
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Expected O, but got Unknown
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Expected O, but got Unknown
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Expected O, but got Unknown
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Expected O, but got Unknown
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Expected O, but got Unknown
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Expected O, but got Unknown
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Expected O, but got Unknown
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Expected O, but got Unknown
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Expected O, but got Unknown
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Expected O, but got Unknown
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Expected O, but got Unknown
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Expected O, but got Unknown
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Expected O, but got Unknown
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Expected O, but got Unknown
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Expected O, but got Unknown
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0285: Expected O, but got Unknown
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Expected O, but got Unknown
		//IL_02b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Expected O, but got Unknown
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dc: Expected O, but got Unknown
		//IL_02ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f9: Expected O, but got Unknown
		//IL_030c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0316: Expected O, but got Unknown
		//IL_0329: Unknown result type (might be due to invalid IL or missing references)
		//IL_0333: Expected O, but got Unknown
		//IL_0346: Unknown result type (might be due to invalid IL or missing references)
		//IL_0350: Expected O, but got Unknown
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_036d: Expected O, but got Unknown
		//IL_0380: Unknown result type (might be due to invalid IL or missing references)
		//IL_038a: Expected O, but got Unknown
		//IL_039d: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a7: Expected O, but got Unknown
		//IL_03ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c4: Expected O, but got Unknown
		//IL_03d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e1: Expected O, but got Unknown
		//IL_03f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fe: Expected O, but got Unknown
		//IL_0411: Unknown result type (might be due to invalid IL or missing references)
		//IL_041b: Expected O, but got Unknown
		//IL_042e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0438: Expected O, but got Unknown
		//IL_044b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0455: Expected O, but got Unknown
		//IL_0468: Unknown result type (might be due to invalid IL or missing references)
		//IL_0472: Expected O, but got Unknown
		//IL_0485: Unknown result type (might be due to invalid IL or missing references)
		//IL_048f: Expected O, but got Unknown
		//IL_049b: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a5: Expected O, but got Unknown
		//IL_04b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_04bc: Expected O, but got Unknown
		//IL_04c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d3: Expected O, but got Unknown
		//IL_04df: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e9: Expected O, but got Unknown
		//IL_04f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ff: Expected O, but got Unknown
		//IL_050b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0515: Expected O, but got Unknown
		//IL_0521: Unknown result type (might be due to invalid IL or missing references)
		//IL_052b: Expected O, but got Unknown
		//IL_0537: Unknown result type (might be due to invalid IL or missing references)
		//IL_0541: Expected O, but got Unknown
		//IL_054d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0557: Expected O, but got Unknown
		//IL_0563: Unknown result type (might be due to invalid IL or missing references)
		//IL_056d: Expected O, but got Unknown
		//IL_0579: Unknown result type (might be due to invalid IL or missing references)
		//IL_0583: Expected O, but got Unknown
		//IL_0590: Unknown result type (might be due to invalid IL or missing references)
		//IL_059a: Expected O, but got Unknown
		//IL_05a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b1: Expected O, but got Unknown
		Instance = this;
		InitializeComponent();
		refmapgrid = (Grid)((FrameworkElement)this).FindName("mapgrid");
		((UIElement)refmapgrid).MouseLeftButtonDown += new MouseButtonEventHandler(MapImage_MouseLeftClick);
		RefMP_ChatInput = (TextBox)((FrameworkElement)this).FindName("MP_ChatInput");
		((UIElement)RefMP_ChatInput).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		((UIElement)RefMP_ChatInput).PreviewKeyUp += new KeyEventHandler(DetectChatEnter);
		RefMP_ChatDisplay = (TextBlock)((FrameworkElement)this).FindName("MP_ChatDisplay");
		RefMP_ChatScrollView = (ScrollViewer)((FrameworkElement)this).FindName("MP_ChatScrollView");
		RefMP_Settings_GameSpeed_Slider = (Slider)((FrameworkElement)this).FindName("MP_Settings_GameSpeed_Slider");
		((RangeBase)RefMP_Settings_GameSpeed_Slider).ValueChanged += MainViewModel.Instance.FRONTMultiplayer.MP_Settings_GameSpeed_Slider_ValueChanged;
		RefRadarShield1 = (Button)((FrameworkElement)this).FindName("RadarShield1");
		RefRadarShield2 = (Button)((FrameworkElement)this).FindName("RadarShield2");
		RefRadarShield3 = (Button)((FrameworkElement)this).FindName("RadarShield3");
		RefRadarShield4 = (Button)((FrameworkElement)this).FindName("RadarShield4");
		RefRadarShield5 = (Button)((FrameworkElement)this).FindName("RadarShield5");
		RefRadarShield6 = (Button)((FrameworkElement)this).FindName("RadarShield6");
		RefRadarShield7 = (Button)((FrameworkElement)this).FindName("RadarShield7");
		RefRadarShield8 = (Button)((FrameworkElement)this).FindName("RadarShield8");
		for (int i = 0; i < 8; i++)
		{
			playerRows[i] = new PlayerRow();
		}
		playerRows[0].RefRow = (Grid)((FrameworkElement)this).FindName("Player1_Row");
		playerRows[0].RefColour = (Image)((FrameworkElement)this).FindName("Player1_Colour");
		playerRows[0].RefName = (TextBlock)((FrameworkElement)this).FindName("Player1_Name");
		playerRows[1].RefRow = (Grid)((FrameworkElement)this).FindName("Player2_Row");
		playerRows[1].RefColour = (Image)((FrameworkElement)this).FindName("Player2_Colour");
		playerRows[1].RefName = (TextBlock)((FrameworkElement)this).FindName("Player2_Name");
		playerRows[1].RefReadyState = (Image)((FrameworkElement)this).FindName("Player2_ReadyState");
		playerRows[2].RefRow = (Grid)((FrameworkElement)this).FindName("Player3_Row");
		playerRows[2].RefColour = (Image)((FrameworkElement)this).FindName("Player3_Colour");
		playerRows[2].RefName = (TextBlock)((FrameworkElement)this).FindName("Player3_Name");
		playerRows[3].RefRow = (Grid)((FrameworkElement)this).FindName("Player4_Row");
		playerRows[3].RefColour = (Image)((FrameworkElement)this).FindName("Player4_Colour");
		playerRows[3].RefName = (TextBlock)((FrameworkElement)this).FindName("Player4_Name");
		playerRows[4].RefRow = (Grid)((FrameworkElement)this).FindName("Player5_Row");
		playerRows[4].RefColour = (Image)((FrameworkElement)this).FindName("Player5_Colour");
		playerRows[4].RefName = (TextBlock)((FrameworkElement)this).FindName("Player5_Name");
		playerRows[5].RefRow = (Grid)((FrameworkElement)this).FindName("Player6_Row");
		playerRows[5].RefColour = (Image)((FrameworkElement)this).FindName("Player6_Colour");
		playerRows[5].RefName = (TextBlock)((FrameworkElement)this).FindName("Player6_Name");
		playerRows[6].RefRow = (Grid)((FrameworkElement)this).FindName("Player7_Row");
		playerRows[6].RefColour = (Image)((FrameworkElement)this).FindName("Player7_Colour");
		playerRows[6].RefName = (TextBlock)((FrameworkElement)this).FindName("Player7_Name");
		playerRows[7].RefRow = (Grid)((FrameworkElement)this).FindName("Player8_Row");
		playerRows[7].RefColour = (Image)((FrameworkElement)this).FindName("Player8_Colour");
		playerRows[7].RefName = (TextBlock)((FrameworkElement)this).FindName("Player8_Name");
		RefMP_EnterShareCodeText = (TextBox)((FrameworkElement)this).FindName("MP_EnterShareCodeText");
		((UIElement)RefMP_EnterShareCodeText).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		((TextBoxBase)RefMP_EnterShareCodeText).TextChanged += new RoutedEventHandler(EnterShareTextChangedHandler);
		RefShareJoinButton = (Button)((FrameworkElement)this).FindName("ShareJoinButton");
		RefCoopCustomGame = (Button)((FrameworkElement)this).FindName("CoopCustomGame");
		RefMultiplayerPlayButton = (Button)((FrameworkElement)this).FindName("Coop1ResumeButtonX");
		RefReadyButton = (Button)((FrameworkElement)this).FindName("ReadyButton");
		RefReadyButtonLock = (Button)((FrameworkElement)this).FindName("ReadyButtonLock");
		RefLoadButton = (Button)((FrameworkElement)this).FindName("CoopLoadButton");
		RefOptionsButton = (Button)((FrameworkElement)this).FindName("OptionsButton");
		RefShowHidden = (CheckBox)((FrameworkElement)this).FindName("ShowHidden");
		((ToggleButton)RefShowHidden).Checked += new RoutedEventHandler(ShowHidden_ValueChanged);
		((ToggleButton)RefShowHidden).Unchecked += new RoutedEventHandler(ShowHidden_ValueChanged);
		if (FatControler.polish)
		{
			PropEx.SetGlowButtonLFontSize((UIElement)(object)RefCoopCustomGame, 20);
		}
		if (FatControler.spanish)
		{
			PropEx.SetGlowButtonLFontSize((UIElement)(object)RefCoopCustomGame, 21);
		}
		if (FatControler.italian)
		{
			PropEx.SetGlowButtonLFontSize((UIElement)(object)RefCoopCustomGame, 20);
		}
	}

	public void ShowHidden_ValueChanged(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.FRONTMultiplayer.ShowHidden_ValueChanged(sender, e);
	}

	public void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	public void DetectChatEnter(object sender, KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		if ((int)e.Key == 6)
		{
			MainViewModel.Instance.FRONTMultiplayer.ButtonClicked("SendChat");
		}
	}

	public void EnterShareTextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (!MainViewModel.Instance.FRONTMultiplayer.panelActive)
		{
			return;
		}
		if (RefMP_EnterShareCodeText.Text.Length < 3)
		{
			((UIElement)RefShareJoinButton).IsEnabled = false;
			return;
		}
		ulong num = Platform_Multiplayer.Instance.DecodeShareCode(RefMP_EnterShareCodeText.Text);
		if (num != 0)
		{
			MainViewModel.Instance.FRONTMultiplayer.LatestSharedCode = num;
			((UIElement)RefShareJoinButton).IsEnabled = true;
		}
		else
		{
			((UIElement)RefShareJoinButton).IsEnabled = false;
		}
	}

	public void MapImage_MouseLeftClick(object sender, MouseEventArgs e)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		object source = ((RoutedEventArgs)e).Source;
		SelectedObject = (UIElement)((source is UIElement) ? source : null);
		if (((object)SelectedObject).GetType() == typeof(Rectangle) || ((object)SelectedObject).GetType() == typeof(Image))
		{
			MousePosition = e.GetPosition(SelectedObject);
			MainViewModel.Instance.FrontEndMenu.TrailMapClicked((int)((Point)(ref MousePosition)).X, (int)((Point)(ref MousePosition)).Y);
		}
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/FRONT_CoopTrail4.xaml");
	}

	public void UpdateRadarShieldPositions()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		((FrameworkElement)RefRadarShield1).Margin = GameData.Instance.getKeepPosition(0, scaled: true);
		((FrameworkElement)RefRadarShield2).Margin = GameData.Instance.getKeepPosition(1, scaled: true);
		((FrameworkElement)RefRadarShield3).Margin = GameData.Instance.getKeepPosition(2, scaled: true);
		((FrameworkElement)RefRadarShield4).Margin = GameData.Instance.getKeepPosition(3, scaled: true);
		((FrameworkElement)RefRadarShield5).Margin = GameData.Instance.getKeepPosition(4, scaled: true);
		((FrameworkElement)RefRadarShield6).Margin = GameData.Instance.getKeepPosition(5, scaled: true);
		((FrameworkElement)RefRadarShield7).Margin = GameData.Instance.getKeepPosition(6, scaled: true);
		((FrameworkElement)RefRadarShield8).Margin = GameData.Instance.getKeepPosition(7, scaled: true);
		ImageSource keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(0, hightlighted: false, hideBlank: true);
		PropEx.SetSprite1((UIElement)(object)RefRadarShield1, keepShield);
		PropEx.SetSprite2((UIElement)(object)RefRadarShield1, keepShield);
		PropEx.SetSprite3((UIElement)(object)RefRadarShield1, keepShield);
		PropEx.SetSprite4((UIElement)(object)RefRadarShield1, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(1, hightlighted: false, hideBlank: true);
		PropEx.SetSprite1((UIElement)(object)RefRadarShield2, keepShield);
		PropEx.SetSprite2((UIElement)(object)RefRadarShield2, keepShield);
		PropEx.SetSprite3((UIElement)(object)RefRadarShield2, keepShield);
		PropEx.SetSprite4((UIElement)(object)RefRadarShield2, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(2, hightlighted: false, hideBlank: true);
		PropEx.SetSprite1((UIElement)(object)RefRadarShield3, keepShield);
		PropEx.SetSprite2((UIElement)(object)RefRadarShield3, keepShield);
		PropEx.SetSprite3((UIElement)(object)RefRadarShield3, keepShield);
		PropEx.SetSprite4((UIElement)(object)RefRadarShield3, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(3, hightlighted: false, hideBlank: true);
		PropEx.SetSprite1((UIElement)(object)RefRadarShield4, keepShield);
		PropEx.SetSprite2((UIElement)(object)RefRadarShield4, keepShield);
		PropEx.SetSprite3((UIElement)(object)RefRadarShield4, keepShield);
		PropEx.SetSprite4((UIElement)(object)RefRadarShield4, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(4, hightlighted: false, hideBlank: true);
		PropEx.SetSprite1((UIElement)(object)RefRadarShield5, keepShield);
		PropEx.SetSprite2((UIElement)(object)RefRadarShield5, keepShield);
		PropEx.SetSprite3((UIElement)(object)RefRadarShield5, keepShield);
		PropEx.SetSprite4((UIElement)(object)RefRadarShield5, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(5, hightlighted: false, hideBlank: true);
		PropEx.SetSprite1((UIElement)(object)RefRadarShield6, keepShield);
		PropEx.SetSprite2((UIElement)(object)RefRadarShield6, keepShield);
		PropEx.SetSprite3((UIElement)(object)RefRadarShield6, keepShield);
		PropEx.SetSprite4((UIElement)(object)RefRadarShield6, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(6, hightlighted: false, hideBlank: true);
		PropEx.SetSprite1((UIElement)(object)RefRadarShield7, keepShield);
		PropEx.SetSprite2((UIElement)(object)RefRadarShield7, keepShield);
		PropEx.SetSprite3((UIElement)(object)RefRadarShield7, keepShield);
		PropEx.SetSprite4((UIElement)(object)RefRadarShield7, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(7, hightlighted: false, hideBlank: true);
		PropEx.SetSprite1((UIElement)(object)RefRadarShield8, keepShield);
		PropEx.SetSprite2((UIElement)(object)RefRadarShield8, keepShield);
		PropEx.SetSprite3((UIElement)(object)RefRadarShield8, keepShield);
		PropEx.SetSprite4((UIElement)(object)RefRadarShield8, keepShield);
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
}
