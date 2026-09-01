using System.Collections.Generic;
using Noesis;

namespace CrusaderDE;

public class HUD_MPChatPanel : UserControl
{
	private int multiplayerChatNumInTeam;

	private int multiplayerChatNumTotal;

	private int[] multiplayerChatPlayers = new int[9];

	private int[] multiplayerChatTeams = new int[9];

	private bool[] multiplayerChatSelectedPlayers = new bool[9];

	private List<int> multiplayerIngameChatRecipients = new List<int>();

	private int[] multiplayerMapping = new int[8];

	private Button[] RefMPChatPlayers = new Button[8];

	private Button[] RefMPMutePlayers = new Button[8];

	private Button RefMPChatTeam;

	private Button RefMPChatSend;

	private TextBox RefMPChatMessageTextBox;

	private TextBlock RefMPChatInsultText;

	private CheckBox RefMuteInsultSpeechCheck;

	private CheckBox RefMuteInsultsCheck;

	private CheckBox RefChatMuteDisable;

	private bool panelActive;

	public HUD_MPChatPanel()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDMPChatPanel = this;
		RefMPChatPlayers[0] = (Button)FindName("MPChatPlayer1");
		RefMPChatPlayers[1] = (Button)FindName("MPChatPlayer2");
		RefMPChatPlayers[2] = (Button)FindName("MPChatPlayer3");
		RefMPChatPlayers[3] = (Button)FindName("MPChatPlayer4");
		RefMPChatPlayers[4] = (Button)FindName("MPChatPlayer5");
		RefMPChatPlayers[5] = (Button)FindName("MPChatPlayer6");
		RefMPChatPlayers[6] = (Button)FindName("MPChatPlayer7");
		RefMPChatPlayers[7] = (Button)FindName("MPChatPlayer8");
		RefMPMutePlayers[0] = (Button)FindName("MPChatPlayer1Mute");
		RefMPMutePlayers[1] = (Button)FindName("MPChatPlayer2Mute");
		RefMPMutePlayers[2] = (Button)FindName("MPChatPlayer3Mute");
		RefMPMutePlayers[3] = (Button)FindName("MPChatPlayer4Mute");
		RefMPMutePlayers[4] = (Button)FindName("MPChatPlayer5Mute");
		RefMPMutePlayers[5] = (Button)FindName("MPChatPlayer6Mute");
		RefMPMutePlayers[6] = (Button)FindName("MPChatPlayer7Mute");
		RefMPMutePlayers[7] = (Button)FindName("MPChatPlayer8Mute");
		RefMPChatTeam = (Button)FindName("MPChatTeam");
		RefMPChatMessageTextBox = (TextBox)FindName("MPChatMessageTextBox");
		RefMPChatMessageTextBox.IsKeyboardFocusedChanged += TextInputFocus;
		RefMPChatMessageTextBox.TextChanged += EditMessageTextChanged;
		RefMPChatMessageTextBox.Loaded += TextBoxLoaded;
		RefMPChatMessageTextBox.PreviewTextInput += DetectEnterTextBox;
		RefMPChatMessageTextBox.KeyDown += DetectEscapeTextBox;
		RefMPChatSend = (Button)FindName("MPChatSend");
		RefMPChatInsultText = (TextBlock)FindName("MPChatInsultText");
		RefMuteInsultSpeechCheck = (CheckBox)FindName("MuteInsultSpeechCheck");
		RefMuteInsultSpeechCheck.Checked += MuteInsult_ValueChanged;
		RefMuteInsultSpeechCheck.Unchecked += MuteInsult_ValueChanged;
		RefMuteInsultsCheck = (CheckBox)FindName("MuteInsultsCheck");
		RefMuteInsultsCheck.Checked += MuteInsult_ValueChanged;
		RefMuteInsultsCheck.Unchecked += MuteInsult_ValueChanged;
		RefChatMuteDisable = (CheckBox)FindName("ChatMuteDisable");
		RefChatMuteDisable.Checked += MuteMPChat_ValueChanged;
		RefChatMuteDisable.Unchecked += MuteMPChat_ValueChanged;
	}

	private void MuteInsult_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_MuteInsults = RefMuteInsultsCheck.IsChecked.Value;
			ConfigSettings.Settings_MuteInsultSpeech = RefMuteInsultSpeechCheck.IsChecked.Value;
			ConfigSettings.SaveSettings();
		}
	}

	private void MuteMPChat_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			Platform_Multiplayer.MPChatMuted = RefChatMuteDisable.IsChecked.Value;
		}
	}

	private void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	private void TextBoxLoaded(object sender, RoutedEventArgs e)
	{
		RefMPChatMessageTextBox.Focus();
	}

	private void EditMessageTextChanged(object sender, RoutedEventArgs e)
	{
		UpdateButtons();
	}

	private void DetectEnterTextBox(object sender, TextCompositionEventArgs e)
	{
		if (e.Text == "\n")
		{
			e.Handled = true;
			base.Keyboard.ClearFocus();
			if (RefMPChatMessageTextBox.Text.Length > 0 && !Platform_Multiplayer.MPChatMuted)
			{
				ButtonClicked("SEND");
			}
		}
	}

	private void DetectEscapeTextBox(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			base.Keyboard.ClearFocus();
		}
	}

	private void UpdateButtons()
	{
		RefMPChatSend.IsEnabled = RefMPChatMessageTextBox.Text.Length > 0 && multiplayerIngameChatRecipients.Count > 0 && !Platform_Multiplayer.MPChatMuted;
		for (int i = 0; i < 8; i++)
		{
			int num = multiplayerMapping[i];
			if (num > 0)
			{
				if (multiplayerChatSelectedPlayers[num])
				{
					PropEx.SetBorderVisibility(RefMPChatPlayers[i], Visibility.Visible);
				}
				else
				{
					PropEx.SetBorderVisibility(RefMPChatPlayers[i], Visibility.Hidden);
				}
				RefMPMutePlayers[i].Visibility = Visibility.Visible;
				if (Platform_Multiplayer.Instance.IsChatMute(num))
				{
					PropEx.SetSprite1(RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[695]);
					PropEx.SetSprite2(RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[696]);
					PropEx.SetSprite3(RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[696]);
					PropEx.SetSprite4(RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[695]);
				}
				else
				{
					PropEx.SetSprite1(RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[693]);
					PropEx.SetSprite2(RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[694]);
					PropEx.SetSprite3(RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[694]);
					PropEx.SetSprite4(RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[693]);
				}
			}
			else
			{
				RefMPMutePlayers[i].Visibility = Visibility.Hidden;
			}
		}
	}

	public void InitNewMPGame()
	{
		for (int i = 1; i < 9; i++)
		{
			multiplayerChatSelectedPlayers[i] = true;
		}
	}

	public void ToggleMultiplayerChat()
	{
		if (MainViewModel.Instance.MPChatVisible || !Director.instance.SimRunning)
		{
			MainViewModel.Instance.MPChatVisible = false;
			return;
		}
		panelActive = false;
		RefMuteInsultsCheck.IsChecked = ConfigSettings.Settings_MuteInsults;
		RefMuteInsultSpeechCheck.IsChecked = ConfigSettings.Settings_MuteInsultSpeech;
		RefChatMuteDisable.IsChecked = Platform_Multiplayer.MPChatMuted;
		MainViewModel.Instance.MeritPanelVisible = false;
		MainViewModel.Instance.AlliesPanelVisible = false;
		MainViewModel.Instance.Show_HUD_IngameMenu = false;
		if (MainViewModel.Instance.Show_HUD_Options)
		{
			MainViewModel.Instance.HUDOptions.ButtonClicked(-1);
		}
		for (int i = 0; i < 9; i++)
		{
			multiplayerChatPlayers[i] = GameData.Instance.lastGameState.player_register[i];
			multiplayerChatTeams[i] = GameData.Instance.lastGameState.teams[i];
		}
		RefMPChatMessageTextBox.Text = "";
		RefMPChatSend.IsEnabled = false;
		multiplayerChatNumInTeam = 0;
		multiplayerChatNumTotal = 0;
		int playerID = GameData.Instance.playerID;
		int num = multiplayerChatTeams[playerID];
		for (int j = 0; j < 8; j++)
		{
			multiplayerMapping[j] = -1;
		}
		for (int k = 1; k < 9; k++)
		{
			if (multiplayerChatPlayers[k] > 0)
			{
				multiplayerMapping[multiplayerChatNumTotal] = k;
				multiplayerChatNumTotal++;
				if (multiplayerChatTeams[k] == num)
				{
					multiplayerChatNumInTeam++;
				}
			}
		}
		RefMPChatInsultText.Visibility = Visibility.Hidden;
		RefMPChatMessageTextBox.Visibility = Visibility.Visible;
		UpdateIngameChatRecipients();
		MainViewModel.Instance.MPChatVisible = true;
		MainViewModel.Instance.HUDMPChatMessages.OpenChatPanel();
		for (int l = 0; l < 8; l++)
		{
			int num2 = multiplayerMapping[l];
			if (num2 >= 0)
			{
				RefMPChatPlayers[l].Visibility = Visibility.Visible;
				PropEx.SetTextCentre(RefMPChatPlayers[l], Platform_Multiplayer.Instance.getPlayerName(num2));
				SolidColorBrush value = new SolidColorBrush(HUD_MPChatMessages.MPTeamColours[SpriteMapping.remapColours[SpriteMapping.RemapMPLoadedColour(num2)]]);
				PropEx.SetButtonTextColour(RefMPChatPlayers[l], value);
				MainViewModel.Instance.getTeamAlliesShield(multiplayerChatTeams[num2], large: false);
				setTeamSprite(GameData.Instance.lastGameState.team_shield[num2], l);
			}
			else
			{
				RefMPChatPlayers[l].Visibility = Visibility.Hidden;
			}
		}
		if (multiplayerChatNumInTeam >= 2)
		{
			RefMPChatTeam.Visibility = Visibility.Visible;
		}
		else
		{
			RefMPChatTeam.Visibility = Visibility.Hidden;
		}
		UpdateButtons();
		RefMPChatMessageTextBox.Focus();
		panelActive = true;
	}

	private void setTeamSprite(int team, int row)
	{
		ImageSource teamAlliesShield = MainViewModel.Instance.getTeamAlliesShield(team, large: false);
		switch (row)
		{
		case 0:
			MainViewModel.Instance.MeritAlly1 = teamAlliesShield;
			break;
		case 1:
			MainViewModel.Instance.MeritAlly2 = teamAlliesShield;
			break;
		case 2:
			MainViewModel.Instance.MeritAlly3 = teamAlliesShield;
			break;
		case 3:
			MainViewModel.Instance.MeritAlly4 = teamAlliesShield;
			break;
		case 4:
			MainViewModel.Instance.MeritAlly5 = teamAlliesShield;
			break;
		case 5:
			MainViewModel.Instance.MeritAlly6 = teamAlliesShield;
			break;
		case 6:
			MainViewModel.Instance.MeritAlly7 = teamAlliesShield;
			break;
		case 7:
			MainViewModel.Instance.MeritAlly8 = teamAlliesShield;
			break;
		}
	}

	private void UpdateIngameChatRecipients()
	{
		multiplayerIngameChatRecipients.Clear();
		for (int i = 1; i < 9; i++)
		{
			if (multiplayerChatSelectedPlayers[i] && multiplayerChatPlayers[i] > 0)
			{
				multiplayerIngameChatRecipients.Add(multiplayerChatPlayers[i]);
			}
		}
	}

	public void ButtonClicked(string param)
	{
		int playerID = GameData.Instance.playerID;
		switch (param)
		{
		case "P1":
		case "P2":
		case "P3":
		case "P4":
		case "P5":
		case "P6":
		case "P7":
		case "P8":
		{
			int num3 = -1;
			switch (param)
			{
			case "P1":
				num3 = multiplayerMapping[0];
				break;
			case "P2":
				num3 = multiplayerMapping[1];
				break;
			case "P3":
				num3 = multiplayerMapping[2];
				break;
			case "P4":
				num3 = multiplayerMapping[3];
				break;
			case "P5":
				num3 = multiplayerMapping[4];
				break;
			case "P6":
				num3 = multiplayerMapping[5];
				break;
			case "P7":
				num3 = multiplayerMapping[6];
				break;
			case "P8":
				num3 = multiplayerMapping[7];
				break;
			}
			multiplayerChatSelectedPlayers[num3] = !multiplayerChatSelectedPlayers[num3];
			UpdateIngameChatRecipients();
			break;
		}
		case "P1Mute":
		case "P2Mute":
		case "P3Mute":
		case "P4Mute":
		case "P5Mute":
		case "P6Mute":
		case "P7Mute":
		case "P8Mute":
		{
			int playerID2 = -1;
			switch (param)
			{
			case "P1Mute":
				playerID2 = multiplayerMapping[0];
				break;
			case "P2Mute":
				playerID2 = multiplayerMapping[1];
				break;
			case "P3Mute":
				playerID2 = multiplayerMapping[2];
				break;
			case "P4Mute":
				playerID2 = multiplayerMapping[3];
				break;
			case "P5Mute":
				playerID2 = multiplayerMapping[4];
				break;
			case "P6Mute":
				playerID2 = multiplayerMapping[5];
				break;
			case "P7Mute":
				playerID2 = multiplayerMapping[6];
				break;
			case "P8Mute":
				playerID2 = multiplayerMapping[7];
				break;
			}
			Platform_Multiplayer.Instance.ToggleChatMute(playerID2);
			UpdateButtons();
			break;
		}
		case "I1":
		case "I2":
		case "I3":
		case "I4":
		case "I5":
		case "I6":
		case "I7":
		case "I8":
		case "I9":
		case "I10":
		case "I11":
		case "I12":
		case "I13":
		case "I14":
		case "I15":
		case "I16":
		case "I17":
		case "I18":
		case "I19":
		case "I20":
		{
			if (multiplayerIngameChatRecipients.Count <= 0)
			{
				break;
			}
			int num4 = 0;
			switch (param)
			{
			case "I1":
				num4 = 1;
				break;
			case "I2":
				num4 = 2;
				break;
			case "I3":
				num4 = 3;
				break;
			case "I4":
				num4 = 4;
				break;
			case "I5":
				num4 = 5;
				break;
			case "I6":
				num4 = 6;
				break;
			case "I7":
				num4 = 7;
				break;
			case "I8":
				num4 = 8;
				break;
			case "I9":
				num4 = 9;
				break;
			case "I10":
				num4 = 10;
				break;
			case "I11":
				num4 = 11;
				break;
			case "I12":
				num4 = 12;
				break;
			case "I13":
				num4 = 13;
				break;
			case "I14":
				num4 = 14;
				break;
			case "I15":
				num4 = 15;
				break;
			case "I16":
				num4 = 16;
				break;
			case "I17":
				num4 = 17;
				break;
			case "I18":
				num4 = 18;
				break;
			case "I19":
				num4 = 19;
				break;
			case "I20":
				num4 = 20;
				break;
			}
			Platform_Multiplayer.Instance.SendIngameChatInsult(multiplayerIngameChatRecipients, num4);
			if (!ConfigSettings.Settings_MuteInsults)
			{
				MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getPlayerName(playerID), playerID, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_INSULTS, num4));
				if (!ConfigSettings.Settings_MuteInsultSpeech)
				{
					SFXManager.instance.playInsult(num4);
				}
			}
			break;
		}
		case "ALL":
		{
			for (int j = 1; j < 9; j++)
			{
				multiplayerChatSelectedPlayers[j] = true;
			}
			UpdateIngameChatRecipients();
			break;
		}
		case "TEAM":
		{
			int num2 = multiplayerChatTeams[playerID];
			for (int k = 1; k < 9; k++)
			{
				multiplayerChatSelectedPlayers[k] = multiplayerChatTeams[k] == num2;
			}
			UpdateIngameChatRecipients();
			break;
		}
		case "ENEMIES":
		{
			int num = multiplayerChatTeams[playerID];
			for (int i = 1; i < 9; i++)
			{
				multiplayerChatSelectedPlayers[i] = multiplayerChatTeams[i] != num;
			}
			UpdateIngameChatRecipients();
			break;
		}
		case "SEND":
			Platform_Multiplayer.Instance.SendIngameChat(multiplayerIngameChatRecipients, RefMPChatMessageTextBox.Text);
			MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getPlayerName(playerID), playerID, RefMPChatMessageTextBox.Text);
			RefMPChatMessageTextBox.Text = "";
			break;
		case "EXIT":
			MainViewModel.Instance.MPChatVisible = false;
			break;
		}
		UpdateButtons();
	}

	private void MouseEnterInsultHandler(object sender, MouseEventArgs e)
	{
		if (e.Source is Button)
		{
			string text = (string)((Button)e.Source).Tag;
			RefMPChatMessageTextBox.Visibility = Visibility.Hidden;
			RefMPChatInsultText.Text = text;
			RefMPChatInsultText.Visibility = Visibility.Visible;
			RefMPChatSend.IsEnabled = false;
			MainViewModel.Instance.CommonRedButtonEnter(null, null);
		}
	}

	private void MouseLeaveInsultHandler(object sender, MouseEventArgs e)
	{
		RefMPChatMessageTextBox.Visibility = Visibility.Visible;
		RefMPChatInsultText.Visibility = Visibility.Hidden;
		UpdateButtons();
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_MPChatPanel.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "MouseEnter" && handlerName == "MouseEnterInsultHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MouseEnterInsultHandler;
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "MouseLeaveInsultHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseLeave += MouseLeaveInsultHandler;
			}
			return true;
		}
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
