using System.Collections.Generic;
using Noesis;

namespace CrusaderDE;

public class HUD_MPChatPanel : UserControl
{
	public int multiplayerChatNumInTeam;

	public int multiplayerChatNumTotal;

	public int[] multiplayerChatPlayers = new int[9];

	public int[] multiplayerChatTeams = new int[9];

	public bool[] multiplayerChatSelectedPlayers = new bool[9];

	public List<int> multiplayerIngameChatRecipients = new List<int>();

	public int[] multiplayerMapping = new int[8];

	public Button[] RefMPChatPlayers = (Button[])(object)new Button[8];

	public Button[] RefMPMutePlayers = (Button[])(object)new Button[8];

	public Button RefMPChatTeam;

	public Button RefMPChatSend;

	public TextBox RefMPChatMessageTextBox;

	public TextBlock RefMPChatInsultText;

	public CheckBox RefMuteInsultSpeechCheck;

	public CheckBox RefMuteInsultsCheck;

	public CheckBox RefChatMuteDisable;

	public bool panelActive;

	public HUD_MPChatPanel()
	{
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Expected O, but got Unknown
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Expected O, but got Unknown
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Expected O, but got Unknown
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Expected O, but got Unknown
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Expected O, but got Unknown
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Expected O, but got Unknown
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Expected O, but got Unknown
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Expected O, but got Unknown
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Expected O, but got Unknown
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Expected O, but got Unknown
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Expected O, but got Unknown
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Expected O, but got Unknown
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Expected O, but got Unknown
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Expected O, but got Unknown
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Expected O, but got Unknown
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Expected O, but got Unknown
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Expected O, but got Unknown
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Expected O, but got Unknown
		//IL_023d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Expected O, but got Unknown
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Expected O, but got Unknown
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Expected O, but got Unknown
		//IL_0282: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Expected O, but got Unknown
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Expected O, but got Unknown
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Expected O, but got Unknown
		//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ce: Expected O, but got Unknown
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Expected O, but got Unknown
		//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fc: Expected O, but got Unknown
		//IL_0308: Unknown result type (might be due to invalid IL or missing references)
		//IL_0312: Expected O, but got Unknown
		//IL_031f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0329: Expected O, but got Unknown
		//IL_0336: Unknown result type (might be due to invalid IL or missing references)
		//IL_0340: Expected O, but got Unknown
		//IL_034c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0356: Expected O, but got Unknown
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_036d: Expected O, but got Unknown
		//IL_037a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0384: Expected O, but got Unknown
		InitializeComponent();
		MainViewModel.Instance.HUDMPChatPanel = this;
		RefMPChatPlayers[0] = (Button)((FrameworkElement)this).FindName("MPChatPlayer1");
		RefMPChatPlayers[1] = (Button)((FrameworkElement)this).FindName("MPChatPlayer2");
		RefMPChatPlayers[2] = (Button)((FrameworkElement)this).FindName("MPChatPlayer3");
		RefMPChatPlayers[3] = (Button)((FrameworkElement)this).FindName("MPChatPlayer4");
		RefMPChatPlayers[4] = (Button)((FrameworkElement)this).FindName("MPChatPlayer5");
		RefMPChatPlayers[5] = (Button)((FrameworkElement)this).FindName("MPChatPlayer6");
		RefMPChatPlayers[6] = (Button)((FrameworkElement)this).FindName("MPChatPlayer7");
		RefMPChatPlayers[7] = (Button)((FrameworkElement)this).FindName("MPChatPlayer8");
		RefMPMutePlayers[0] = (Button)((FrameworkElement)this).FindName("MPChatPlayer1Mute");
		RefMPMutePlayers[1] = (Button)((FrameworkElement)this).FindName("MPChatPlayer2Mute");
		RefMPMutePlayers[2] = (Button)((FrameworkElement)this).FindName("MPChatPlayer3Mute");
		RefMPMutePlayers[3] = (Button)((FrameworkElement)this).FindName("MPChatPlayer4Mute");
		RefMPMutePlayers[4] = (Button)((FrameworkElement)this).FindName("MPChatPlayer5Mute");
		RefMPMutePlayers[5] = (Button)((FrameworkElement)this).FindName("MPChatPlayer6Mute");
		RefMPMutePlayers[6] = (Button)((FrameworkElement)this).FindName("MPChatPlayer7Mute");
		RefMPMutePlayers[7] = (Button)((FrameworkElement)this).FindName("MPChatPlayer8Mute");
		RefMPChatTeam = (Button)((FrameworkElement)this).FindName("MPChatTeam");
		RefMPChatMessageTextBox = (TextBox)((FrameworkElement)this).FindName("MPChatMessageTextBox");
		((UIElement)RefMPChatMessageTextBox).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		((TextBoxBase)RefMPChatMessageTextBox).TextChanged += new RoutedEventHandler(EditMessageTextChanged);
		((FrameworkElement)RefMPChatMessageTextBox).Loaded += new RoutedEventHandler(TextBoxLoaded);
		((UIElement)RefMPChatMessageTextBox).PreviewTextInput += new TextCompositionEventHandler(DetectEnterTextBox);
		((UIElement)RefMPChatMessageTextBox).KeyDown += new KeyEventHandler(DetectEscapeTextBox);
		RefMPChatSend = (Button)((FrameworkElement)this).FindName("MPChatSend");
		RefMPChatInsultText = (TextBlock)((FrameworkElement)this).FindName("MPChatInsultText");
		RefMuteInsultSpeechCheck = (CheckBox)((FrameworkElement)this).FindName("MuteInsultSpeechCheck");
		((ToggleButton)RefMuteInsultSpeechCheck).Checked += new RoutedEventHandler(MuteInsult_ValueChanged);
		((ToggleButton)RefMuteInsultSpeechCheck).Unchecked += new RoutedEventHandler(MuteInsult_ValueChanged);
		RefMuteInsultsCheck = (CheckBox)((FrameworkElement)this).FindName("MuteInsultsCheck");
		((ToggleButton)RefMuteInsultsCheck).Checked += new RoutedEventHandler(MuteInsult_ValueChanged);
		((ToggleButton)RefMuteInsultsCheck).Unchecked += new RoutedEventHandler(MuteInsult_ValueChanged);
		RefChatMuteDisable = (CheckBox)((FrameworkElement)this).FindName("ChatMuteDisable");
		((ToggleButton)RefChatMuteDisable).Checked += new RoutedEventHandler(MuteMPChat_ValueChanged);
		((ToggleButton)RefChatMuteDisable).Unchecked += new RoutedEventHandler(MuteMPChat_ValueChanged);
	}

	public void MuteInsult_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_MuteInsults = ((ToggleButton)RefMuteInsultsCheck).IsChecked.Value;
			ConfigSettings.Settings_MuteInsultSpeech = ((ToggleButton)RefMuteInsultSpeechCheck).IsChecked.Value;
			ConfigSettings.SaveSettings();
		}
	}

	public void MuteMPChat_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			Platform_Multiplayer.MPChatMuted = ((ToggleButton)RefChatMuteDisable).IsChecked.Value;
		}
	}

	public void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	public void TextBoxLoaded(object sender, RoutedEventArgs e)
	{
		((UIElement)RefMPChatMessageTextBox).Focus();
	}

	public void EditMessageTextChanged(object sender, RoutedEventArgs e)
	{
		UpdateButtons();
	}

	public void DetectEnterTextBox(object sender, TextCompositionEventArgs e)
	{
		if (e.Text == "\n")
		{
			((RoutedEventArgs)e).Handled = true;
			((UIElement)this).Keyboard.ClearFocus();
			if (RefMPChatMessageTextBox.Text.Length > 0 && !Platform_Multiplayer.MPChatMuted)
			{
				ButtonClicked("SEND");
			}
		}
	}

	public void DetectEscapeTextBox(object sender, KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		if ((int)e.Key == 13)
		{
			((UIElement)this).Keyboard.ClearFocus();
		}
	}

	public void UpdateButtons()
	{
		((UIElement)RefMPChatSend).IsEnabled = RefMPChatMessageTextBox.Text.Length > 0 && multiplayerIngameChatRecipients.Count > 0 && !Platform_Multiplayer.MPChatMuted;
		for (int i = 0; i < 8; i++)
		{
			int num = multiplayerMapping[i];
			if (num > 0)
			{
				if (multiplayerChatSelectedPlayers[num])
				{
					PropEx.SetBorderVisibility((UIElement)(object)RefMPChatPlayers[i], (Visibility)2);
				}
				else
				{
					PropEx.SetBorderVisibility((UIElement)(object)RefMPChatPlayers[i], (Visibility)1);
				}
				((UIElement)RefMPMutePlayers[i]).Visibility = (Visibility)2;
				if (Platform_Multiplayer.Instance.IsChatMute(num))
				{
					PropEx.SetSprite1((UIElement)(object)RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[695]);
					PropEx.SetSprite2((UIElement)(object)RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[696]);
					PropEx.SetSprite3((UIElement)(object)RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[696]);
					PropEx.SetSprite4((UIElement)(object)RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[695]);
				}
				else
				{
					PropEx.SetSprite1((UIElement)(object)RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[693]);
					PropEx.SetSprite2((UIElement)(object)RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[694]);
					PropEx.SetSprite3((UIElement)(object)RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[694]);
					PropEx.SetSprite4((UIElement)(object)RefMPMutePlayers[i], MainViewModel.Instance.GameSprites[693]);
				}
			}
			else
			{
				((UIElement)RefMPMutePlayers[i]).Visibility = (Visibility)1;
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
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Expected O, but got Unknown
		if (MainViewModel.Instance.MPChatVisible || !Director.instance.SimRunning)
		{
			MainViewModel.Instance.MPChatVisible = false;
			return;
		}
		panelActive = false;
		((ToggleButton)RefMuteInsultsCheck).IsChecked = ConfigSettings.Settings_MuteInsults;
		((ToggleButton)RefMuteInsultSpeechCheck).IsChecked = ConfigSettings.Settings_MuteInsultSpeech;
		((ToggleButton)RefChatMuteDisable).IsChecked = Platform_Multiplayer.MPChatMuted;
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
		((UIElement)RefMPChatSend).IsEnabled = false;
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
		((UIElement)RefMPChatInsultText).Visibility = (Visibility)1;
		((UIElement)RefMPChatMessageTextBox).Visibility = (Visibility)2;
		UpdateIngameChatRecipients();
		MainViewModel.Instance.MPChatVisible = true;
		MainViewModel.Instance.HUDMPChatMessages.OpenChatPanel();
		for (int l = 0; l < 8; l++)
		{
			int num2 = multiplayerMapping[l];
			if (num2 >= 0)
			{
				((UIElement)RefMPChatPlayers[l]).Visibility = (Visibility)2;
				PropEx.SetTextCentre((UIElement)(object)RefMPChatPlayers[l], Platform_Multiplayer.Instance.getPlayerName(num2));
				SolidColorBrush value = new SolidColorBrush(HUD_MPChatMessages.MPTeamColours[SpriteMapping.remapColours[SpriteMapping.RemapMPLoadedColour(num2)]]);
				PropEx.SetButtonTextColour((UIElement)(object)RefMPChatPlayers[l], value);
				MainViewModel.Instance.getTeamAlliesShield(multiplayerChatTeams[num2], large: false);
				setTeamSprite(GameData.Instance.lastGameState.team_shield[num2], l);
			}
			else
			{
				((UIElement)RefMPChatPlayers[l]).Visibility = (Visibility)1;
			}
		}
		if (multiplayerChatNumInTeam >= 2)
		{
			((UIElement)RefMPChatTeam).Visibility = (Visibility)2;
		}
		else
		{
			((UIElement)RefMPChatTeam).Visibility = (Visibility)1;
		}
		UpdateButtons();
		((UIElement)RefMPChatMessageTextBox).Focus();
		panelActive = true;
	}

	public void setTeamSprite(int team, int row)
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

	public void UpdateIngameChatRecipients()
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

	public void MouseEnterInsultHandler(object sender, MouseEventArgs e)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		if (((RoutedEventArgs)e).Source is Button)
		{
			string text = (string)((FrameworkElement)(Button)((RoutedEventArgs)e).Source).Tag;
			((UIElement)RefMPChatMessageTextBox).Visibility = (Visibility)1;
			RefMPChatInsultText.Text = text;
			((UIElement)RefMPChatInsultText).Visibility = (Visibility)2;
			((UIElement)RefMPChatSend).IsEnabled = false;
			MainViewModel.Instance.CommonRedButtonEnter(null, null);
		}
	}

	public void MouseLeaveInsultHandler(object sender, MouseEventArgs e)
	{
		((UIElement)RefMPChatMessageTextBox).Visibility = (Visibility)2;
		((UIElement)RefMPChatInsultText).Visibility = (Visibility)1;
		UpdateButtons();
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_MPChatPanel.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Expected O, but got Unknown
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected O, but got Unknown
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Expected O, but got Unknown
		if (eventName == "MouseEnter" && handlerName == "MouseEnterInsultHandler")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseEnter += new MouseEventHandler(MouseEnterInsultHandler);
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "MouseLeaveInsultHandler")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseLeave += new MouseEventHandler(MouseLeaveInsultHandler);
			}
			return true;
		}
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
