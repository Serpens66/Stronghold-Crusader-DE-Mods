using Noesis;

namespace CrusaderDE;

public class FRONT_CoopTrail1 : UserControl
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
			RefRow.Visibility = Visibility.Hidden;
		}

		public void Update(FRONT_Multiplayer parent, Platform_Multiplayer.MPLobbyMember member, int row, int player)
		{
			playerID = player;
			if (member == null)
			{
				Clear();
				return;
			}
			FRONT_Multiplayer.SetVisibility(RefRow, Visibility.Visible);
			string combinedName = member.CombinedName;
			if (RefName.Text != combinedName)
			{
				RefName.Text = combinedName;
			}
			if (RefReadyState != null)
			{
				if (member.ready || MainViewModel.Instance.FRONTMultiplayer.singlePlayerCoop)
				{
					ImageSource imageSource = MainViewModel.Instance.GameSprites[105];
					if (RefReadyState.Source != imageSource)
					{
						RefReadyState.Source = imageSource;
					}
				}
				else
				{
					ImageSource imageSource2 = MainViewModel.Instance.GameSprites[103];
					if (RefReadyState.Source != imageSource2)
					{
						RefReadyState.Source = imageSource2;
					}
				}
			}
			ImageSource colourShield = FRONT_Multiplayer.GetColourShield(member.colourID);
			if (RefColour.Source != colourShield)
			{
				RefColour.Source = colourShield;
			}
			parent.currentLobby.getTeam(member).ToString();
		}
	}

	public static FRONT_CoopTrail1 Instance;

	private Point MousePosition;

	private Grid refmapgrid;

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

	private TextBox RefMP_EnterShareCodeText;

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

	private UIElement SelectedObject { get; set; }

	public FRONT_CoopTrail1()
	{
		Instance = this;
		InitializeComponent();
		refmapgrid = (Grid)FindName("mapgrid");
		refmapgrid.MouseLeftButtonDown += MapImage_MouseLeftClick;
		RefMP_ChatInput = (TextBox)FindName("MP_ChatInput");
		RefMP_ChatInput.IsKeyboardFocusedChanged += TextInputFocus;
		RefMP_ChatInput.PreviewKeyUp += DetectChatEnter;
		RefMP_ChatDisplay = (TextBlock)FindName("MP_ChatDisplay");
		RefMP_ChatScrollView = (ScrollViewer)FindName("MP_ChatScrollView");
		RefMP_Settings_GameSpeed_Slider = (Slider)FindName("MP_Settings_GameSpeed_Slider");
		RefMP_Settings_GameSpeed_Slider.ValueChanged += MainViewModel.Instance.FRONTMultiplayer.MP_Settings_GameSpeed_Slider_ValueChanged;
		RefRadarShield1 = (Button)FindName("RadarShield1");
		RefRadarShield2 = (Button)FindName("RadarShield2");
		RefRadarShield3 = (Button)FindName("RadarShield3");
		RefRadarShield4 = (Button)FindName("RadarShield4");
		RefRadarShield5 = (Button)FindName("RadarShield5");
		RefRadarShield6 = (Button)FindName("RadarShield6");
		RefRadarShield7 = (Button)FindName("RadarShield7");
		RefRadarShield8 = (Button)FindName("RadarShield8");
		for (int i = 0; i < 8; i++)
		{
			playerRows[i] = new PlayerRow();
		}
		playerRows[0].RefRow = (Grid)FindName("Player1_Row");
		playerRows[0].RefColour = (Image)FindName("Player1_Colour");
		playerRows[0].RefName = (TextBlock)FindName("Player1_Name");
		playerRows[1].RefRow = (Grid)FindName("Player2_Row");
		playerRows[1].RefColour = (Image)FindName("Player2_Colour");
		playerRows[1].RefName = (TextBlock)FindName("Player2_Name");
		playerRows[1].RefReadyState = (Image)FindName("Player2_ReadyState");
		playerRows[2].RefRow = (Grid)FindName("Player3_Row");
		playerRows[2].RefColour = (Image)FindName("Player3_Colour");
		playerRows[2].RefName = (TextBlock)FindName("Player3_Name");
		playerRows[3].RefRow = (Grid)FindName("Player4_Row");
		playerRows[3].RefColour = (Image)FindName("Player4_Colour");
		playerRows[3].RefName = (TextBlock)FindName("Player4_Name");
		playerRows[4].RefRow = (Grid)FindName("Player5_Row");
		playerRows[4].RefColour = (Image)FindName("Player5_Colour");
		playerRows[4].RefName = (TextBlock)FindName("Player5_Name");
		playerRows[5].RefRow = (Grid)FindName("Player6_Row");
		playerRows[5].RefColour = (Image)FindName("Player6_Colour");
		playerRows[5].RefName = (TextBlock)FindName("Player6_Name");
		playerRows[6].RefRow = (Grid)FindName("Player7_Row");
		playerRows[6].RefColour = (Image)FindName("Player7_Colour");
		playerRows[6].RefName = (TextBlock)FindName("Player7_Name");
		playerRows[7].RefRow = (Grid)FindName("Player8_Row");
		playerRows[7].RefColour = (Image)FindName("Player8_Colour");
		playerRows[7].RefName = (TextBlock)FindName("Player8_Name");
		RefMP_EnterShareCodeText = (TextBox)FindName("MP_EnterShareCodeText");
		RefMP_EnterShareCodeText.IsKeyboardFocusedChanged += TextInputFocus;
		RefMP_EnterShareCodeText.TextChanged += EnterShareTextChangedHandler;
		RefShareJoinButton = (Button)FindName("ShareJoinButton");
		RefCoopCustomGame = (Button)FindName("CoopCustomGame");
		RefMultiplayerPlayButton = (Button)FindName("Coop1ResumeButtonX");
		RefReadyButton = (Button)FindName("ReadyButton");
		RefReadyButtonLock = (Button)FindName("ReadyButtonLock");
		RefLoadButton = (Button)FindName("CoopLoadButton");
		RefOptionsButton = (Button)FindName("OptionsButton");
		RefShowHidden = (CheckBox)FindName("ShowHidden");
		RefShowHidden.Checked += ShowHidden_ValueChanged;
		RefShowHidden.Unchecked += ShowHidden_ValueChanged;
		if (FatControler.polish)
		{
			PropEx.SetGlowButtonLFontSize(RefCoopCustomGame, 20);
		}
		if (FatControler.spanish)
		{
			PropEx.SetGlowButtonLFontSize(RefCoopCustomGame, 21);
		}
		if (FatControler.italian)
		{
			PropEx.SetGlowButtonLFontSize(RefCoopCustomGame, 20);
		}
	}

	public void ShowHidden_ValueChanged(object sender, RoutedEventArgs e)
	{
		MainViewModel.Instance.FRONTMultiplayer.ShowHidden_ValueChanged(sender, e);
	}

	private void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	private void DetectChatEnter(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			MainViewModel.Instance.FRONTMultiplayer.ButtonClicked("SendChat");
		}
	}

	private void EnterShareTextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (!MainViewModel.Instance.FRONTMultiplayer.panelActive)
		{
			return;
		}
		if (RefMP_EnterShareCodeText.Text.Length < 3)
		{
			RefShareJoinButton.IsEnabled = false;
			return;
		}
		ulong num = Platform_Multiplayer.Instance.DecodeShareCode(RefMP_EnterShareCodeText.Text);
		if (num != 0)
		{
			MainViewModel.Instance.FRONTMultiplayer.LatestSharedCode = num;
			RefShareJoinButton.IsEnabled = true;
		}
		else
		{
			RefShareJoinButton.IsEnabled = false;
		}
	}

	public void MapImage_MouseLeftClick(object sender, MouseEventArgs e)
	{
		SelectedObject = e.Source as UIElement;
		if (SelectedObject.GetType() == typeof(Rectangle) || SelectedObject.GetType() == typeof(Image))
		{
			MousePosition = e.GetPosition(SelectedObject);
			MainViewModel.Instance.FrontEndMenu.TrailMapClicked((int)MousePosition.X, (int)MousePosition.Y);
		}
	}

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/FRONT_CoopTrail1.xaml");
	}

	public void UpdateRadarShieldPositions()
	{
		RefRadarShield1.Margin = GameData.Instance.getKeepPosition(0, scaled: true);
		RefRadarShield2.Margin = GameData.Instance.getKeepPosition(1, scaled: true);
		RefRadarShield3.Margin = GameData.Instance.getKeepPosition(2, scaled: true);
		RefRadarShield4.Margin = GameData.Instance.getKeepPosition(3, scaled: true);
		RefRadarShield5.Margin = GameData.Instance.getKeepPosition(4, scaled: true);
		RefRadarShield6.Margin = GameData.Instance.getKeepPosition(5, scaled: true);
		RefRadarShield7.Margin = GameData.Instance.getKeepPosition(6, scaled: true);
		RefRadarShield8.Margin = GameData.Instance.getKeepPosition(7, scaled: true);
		ImageSource keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(0);
		PropEx.SetSprite1(RefRadarShield1, keepShield);
		PropEx.SetSprite2(RefRadarShield1, keepShield);
		PropEx.SetSprite3(RefRadarShield1, keepShield);
		PropEx.SetSprite4(RefRadarShield1, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(1);
		PropEx.SetSprite1(RefRadarShield2, keepShield);
		PropEx.SetSprite2(RefRadarShield2, keepShield);
		PropEx.SetSprite3(RefRadarShield2, keepShield);
		PropEx.SetSprite4(RefRadarShield2, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(2);
		PropEx.SetSprite1(RefRadarShield3, keepShield);
		PropEx.SetSprite2(RefRadarShield3, keepShield);
		PropEx.SetSprite3(RefRadarShield3, keepShield);
		PropEx.SetSprite4(RefRadarShield3, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(3);
		PropEx.SetSprite1(RefRadarShield4, keepShield);
		PropEx.SetSprite2(RefRadarShield4, keepShield);
		PropEx.SetSprite3(RefRadarShield4, keepShield);
		PropEx.SetSprite4(RefRadarShield4, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(4);
		PropEx.SetSprite1(RefRadarShield5, keepShield);
		PropEx.SetSprite2(RefRadarShield5, keepShield);
		PropEx.SetSprite3(RefRadarShield5, keepShield);
		PropEx.SetSprite4(RefRadarShield5, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(5);
		PropEx.SetSprite1(RefRadarShield6, keepShield);
		PropEx.SetSprite2(RefRadarShield6, keepShield);
		PropEx.SetSprite3(RefRadarShield6, keepShield);
		PropEx.SetSprite4(RefRadarShield6, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(6);
		PropEx.SetSprite1(RefRadarShield7, keepShield);
		PropEx.SetSprite2(RefRadarShield7, keepShield);
		PropEx.SetSprite3(RefRadarShield7, keepShield);
		PropEx.SetSprite4(RefRadarShield7, keepShield);
		keepShield = MainViewModel.Instance.FRONTMultiplayer.getKeepShield(7);
		PropEx.SetSprite1(RefRadarShield8, keepShield);
		PropEx.SetSprite2(RefRadarShield8, keepShield);
		PropEx.SetSprite3(RefRadarShield8, keepShield);
		PropEx.SetSprite4(RefRadarShield8, keepShield);
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
}
