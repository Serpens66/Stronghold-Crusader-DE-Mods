using System;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class HUD_AlliesPanel : UserControl
{
	public Button RefMPChatTeam;

	public Button RefMPChatSend;

	public TextBox RefMPChatMessageTextBox;

	public const int ORDER_NONE = 0;

	public const int ORDER_ATTACK = 1;

	public const int ORDER_DEFEND = 2;

	public int selectedGoods = -1;

	public int selectedGoodsAmount = -1;

	public int req_goods_asked_for;

	public DateTime nextAllyCheck = DateTime.MinValue;

	public int curr_ally;

	public int num_allies;

	public int[] ally = new int[6];

	public int numEnemies;

	public int[] sk_enemy = new int[6];

	public bool lastAlliesPip;

	public DateTime lastAlliesPipTime = DateTime.MinValue;

	public HUD_AlliesPanel()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDAlliesPanel = this;
	}

	public void GetAllyList(int[,] lordData)
	{
		if (GameData.Instance.lastGameState == null)
		{
			return;
		}
		int num = GameData.Instance.lastGameState.teams[GameData.Instance.playerID];
		num_allies = 0;
		if (lordData[GameData.Instance.playerID, 1] < 0)
		{
			return;
		}
		for (int i = 1; i < 9; i++)
		{
			if (i != GameData.Instance.playerID && (GameData.Instance.lastGameState.is_valid_player(i) || GameData.Instance.lastGameState.is_skirmish_player(i)) && lordData[i, 1] >= 0 && num == GameData.Instance.lastGameState.teams[i])
			{
				ally[num_allies++] = i;
				if (num_allies >= 6)
				{
					break;
				}
			}
		}
		if (curr_ally >= num_allies)
		{
			curr_ally = num_allies - 1;
		}
		if (curr_ally < 0)
		{
			curr_ally = 0;
		}
	}

	public void GetEnemyList(int[,] lordData)
	{
		int num = GameData.Instance.lastGameState.teams[GameData.Instance.playerID];
		numEnemies = 0;
		for (int i = 1; i < 9; i++)
		{
			if (i != GameData.Instance.playerID && (GameData.Instance.lastGameState.is_valid_player(i) || GameData.Instance.lastGameState.is_skirmish_player(i)) && lordData[i, 1] >= 0 && num != GameData.Instance.lastGameState.teams[i])
			{
				sk_enemy[numEnemies++] = i;
				if (numEnemies >= 6)
				{
					break;
				}
			}
		}
	}

	public static void Open(bool state)
	{
		MainViewModel.Instance.AlliesPanelVisible = state;
		MainViewModel.Instance.MeritPanelVisible = false;
		MainViewModel.Instance.MPChatVisible = false;
		if (state)
		{
			SFXManager.instance.playGenieSpeech(3, "Genie_11.wav", 1f);
			MainViewModel.Instance.HUDAlliesPanel.Open();
		}
	}

	public void CloseAllSubPanels()
	{
		MainViewModel.Instance.Allies_OrdersViewVis = false;
		MainViewModel.Instance.Allies_GoodsViewVis = false;
		MainViewModel.Instance.Allies_SendGoodsViewVis = false;
		MainViewModel.Instance.Allies_RequestGoodsViewVis = false;
	}

	public void Open()
	{
		int[,] meritData = EngineInterface.GetMeritData();
		GetAllyList(meritData);
		GetEnemyList(meritData);
		for (int i = 0; i < 6; i++)
		{
			MainViewModel.Instance.setAlliesFace(i, null, null);
			MainViewModel.Instance.AlliesHumanFaceVis[i] = false;
		}
		CloseAllSubPanels();
		if (num_allies < 1)
		{
			MainViewModel.Instance.Allies_MainViewVis = false;
			MainViewModel.Instance.Allies_NoAlliesViewVis = true;
		}
		else
		{
			MainViewModel.Instance.Allies_MainViewVis = true;
			MainViewModel.Instance.Allies_NoAlliesViewVis = false;
			UpdateAllies();
		}
		nextAllyCheck = DateTime.UtcNow.AddSeconds(2.0);
	}

	public void Update()
	{
		if (MainViewModel.Instance.Allies_MainViewVis)
		{
			UpdateAllies();
		}
		if (MainViewModel.Instance.Allies_OrdersViewVis)
		{
			UpdateEnemies();
		}
	}

	public bool ShowAlliesPip()
	{
		if (MainViewModel.Instance.SkirmishModeVisAlly)
		{
			if (!MainViewModel.Instance.AlliesPanelVisible && DateTime.UtcNow > lastAlliesPipTime)
			{
				EngineInterface.PlayState lastGameState = GameData.Instance.lastGameState;
				lastAlliesPipTime = DateTime.UtcNow.AddSeconds(1.0);
				int[,] meritData = EngineInterface.GetMeritData();
				GetAllyList(meritData);
				lastAlliesPip = false;
				for (int i = 0; i < num_allies; i++)
				{
					int num = ally[i];
					if (lastGameState.skirmish_player_requesting_type[num] > 0)
					{
						lastAlliesPip = true;
					}
					else if (lastGameState.skirmish_needs_help[num] != 0)
					{
						lastAlliesPip = true;
					}
					else if (lastGameState.skirmish_order[num] == 1)
					{
						lastAlliesPip = true;
					}
					else if (lastGameState.skirmish_order[num] == 2)
					{
						lastAlliesPip = true;
					}
				}
			}
			return lastAlliesPip;
		}
		return false;
	}

	public void UpdateAllies()
	{
		//IL_029d: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Expected O, but got Unknown
		//IL_056e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0689: Unknown result type (might be due to invalid IL or missing references)
		EngineInterface.PlayState lastGameState = GameData.Instance.lastGameState;
		int num = -1;
		if (num_allies > 0 && nextAllyCheck < DateTime.UtcNow)
		{
			nextAllyCheck = DateTime.UtcNow.AddSeconds(2.0);
			if (EngineInterface.GetMeritData()[GameData.Instance.playerID, 1] < 0)
			{
				num_allies = 0;
				MainViewModel.Instance.Allies_MainViewVis = false;
				MainViewModel.Instance.Allies_NoAlliesViewVis = true;
			}
		}
		lastAlliesPip = false;
		for (int i = 0; i < num_allies; i++)
		{
			num = ally[i];
			ImageSource requestedGoods = null;
			ImageSource requestedhelp = null;
			ImageSource requestedOrders = null;
			if (lastGameState.skirmish_player_requesting_type[num] > 0)
			{
				requestedGoods = MainViewModel.Instance.getAlliesGoodsIcon(lastGameState.skirmish_player_requesting_type[num]);
				lastAlliesPip = true;
			}
			if (lastGameState.skirmish_needs_help[num] != 0)
			{
				requestedhelp = MainViewModel.Instance.getAlliesGoodsIcon(-2);
				lastAlliesPip = true;
			}
			if (lastGameState.skirmish_order[num] == 1)
			{
				requestedOrders = MainViewModel.Instance.getAlliesGoodsIcon(-3);
				lastAlliesPip = true;
			}
			if (lastGameState.skirmish_order[num] == 2)
			{
				requestedOrders = MainViewModel.Instance.getAlliesGoodsIcon(-4);
				lastAlliesPip = true;
			}
			if (lastGameState.is_skirmish_player(num))
			{
				if (curr_ally == i)
				{
					MainViewModel.Instance.setAlliesFace(i, MainViewModel.Instance.getAIFace(lastGameState.computer_register[num], allowExtendedRemapping: true), MainViewModel.Instance.getAIFaceBackground(num), requestedGoods, requestedhelp, requestedOrders);
					MainViewModel.Instance.AlliesSelectedLord = OnScreenText.getComputerName(lastGameState.computer_register[num], lastGameState.computer_names[num]);
					Color val = GameMap.Lighten(OnScreenText.Instance.MPTeamColours[SpriteMapping.remapColours[SpriteMapping.RemapMPLoadedColour(num)]], 0.3f);
					MainViewModel.Instance.AlliesSelectedLordColour = new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)(val.r * 255f), (byte)(val.g * 255f), (byte)(val.b * 255f)));
				}
				else
				{
					MainViewModel.Instance.setAlliesFace(i, MainViewModel.Instance.getAIFace(lastGameState.computer_register[num], allowExtendedRemapping: true), null, requestedGoods, requestedhelp, requestedOrders, 0.5f);
				}
			}
			else
			{
				if (curr_ally == i)
				{
					MainViewModel.Instance.setAlliesFace(i, Platform_Multiplayer.Instance.GetUserAvatar(Platform_Multiplayer.Instance.getPlayerSteamID(num)), MainViewModel.Instance.getAIFaceBackground(num), requestedGoods, requestedhelp, requestedOrders);
					MainViewModel.Instance.AlliesSelectedLord = Platform_Multiplayer.Instance.getPlayerName(num);
				}
				else
				{
					MainViewModel.Instance.setAlliesFace(i, Platform_Multiplayer.Instance.GetUserAvatar(Platform_Multiplayer.Instance.getPlayerSteamID(num)), null, requestedGoods, requestedhelp, requestedOrders, 0.5f);
				}
				MainViewModel.Instance.AlliesHumanFaceVis[i] = true;
			}
		}
		num = ally[curr_ally];
		if (lastGameState.skirmish_player_requesting_type[num] <= 0)
		{
			MainViewModel.Instance.AlliesSelectedGoods = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_MESSAGE_LIBRARY);
			MainViewModel.Instance.AlliesRequestedGoodsCurrent = null;
		}
		else
		{
			MainViewModel.Instance.AlliesRequestedGoodsCurrent = MainViewModel.Instance.getAlliesGoodsIcon(lastGameState.skirmish_player_requesting_type[num]);
			if (FatControler.arabic && ConfigSettings.Settings_ArabicL2R)
			{
				MainViewModel.Instance.AlliesSelectedGoods = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_WOLF) + " " + lastGameState.skirmish_player_requesting_amount[num] + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, lastGameState.skirmish_player_requesting_type[num]) + "       ";
			}
			else
			{
				MainViewModel.Instance.AlliesSelectedGoods = "       " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_WOLF) + " " + lastGameState.skirmish_player_requesting_amount[num] + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, lastGameState.skirmish_player_requesting_type[num]);
			}
		}
		if (lastGameState.skirmish_order[num] == 0)
		{
			MainViewModel.Instance.AlliesSelectedOrder = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_SCENARIO_EDITOR);
			MainViewModel.Instance.AlliesOrderCurrentBackground = null;
			MainViewModel.Instance.AlliesOrderCurrentFace = null;
		}
		else
		{
			string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_MOOD4);
			if (lastGameState.skirmish_order[num] == 2)
			{
				text = text + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_NEW_MESSAGE);
				MainViewModel.Instance.AlliesOrderCurrentBackground = null;
				MainViewModel.Instance.AlliesOrderCurrentFace = null;
			}
			else
			{
				int num2 = lastGameState.skirmish_order_player[num];
				text = ((!FatControler.arabic || !ConfigSettings.Settings_ArabicL2R) ? ("       " + text + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_SNAKE)) : (text + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_SNAKE) + "       "));
				MainViewModel.Instance.AlliesOrderCurrentBackground = MainViewModel.Instance.getAIFaceBackground(num2);
				if (lastGameState.is_skirmish_player(num2))
				{
					MainViewModel.Instance.AlliesOrderCurrentFace = MainViewModel.Instance.getAIFace(lastGameState.computer_register[num2], allowExtendedRemapping: true);
				}
				else
				{
					MainViewModel.Instance.AlliesOrderCurrentFace = Platform_Multiplayer.Instance.GetUserAvatar(Platform_Multiplayer.Instance.getPlayerSteamID(num2));
				}
			}
			MainViewModel.Instance.AlliesSelectedOrder = text;
		}
		string alliesSelectedHelp = "";
		if (lastGameState.skirmish_needs_help[num] != 0)
		{
			alliesSelectedHelp = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_HELP);
		}
		int playerID = GameData.Instance.playerID;
		if (lastGameState.skirmish_order[playerID] == 1 && lastGameState.is_valid_player(lastGameState.skirmish_order_from_player[playerID]) && lastGameState.skirmish_order_from_player[playerID] == num)
		{
			alliesSelectedHelp = ((!FatControler.arabic || !ConfigSettings.Settings_ArabicL2R) ? ("       " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_SNAKE)) : (Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_SNAKE) + "       "));
			int num3 = lastGameState.skirmish_order_player[playerID];
			MainViewModel.Instance.AlliesHelpCurrentBackground = MainViewModel.Instance.getAIFaceBackground(num3);
			if (lastGameState.is_skirmish_player(num3))
			{
				MainViewModel.Instance.AlliesHelpCurrentFace = MainViewModel.Instance.getAIFace(lastGameState.computer_register[num3], allowExtendedRemapping: true);
			}
			else
			{
				MainViewModel.Instance.AlliesHelpCurrentFace = Platform_Multiplayer.Instance.GetUserAvatar(Platform_Multiplayer.Instance.getPlayerSteamID(num3));
			}
		}
		else
		{
			MainViewModel.Instance.AlliesHelpCurrentBackground = null;
			MainViewModel.Instance.AlliesHelpCurrentFace = null;
		}
		MainViewModel.Instance.AlliesSelectedHelp = alliesSelectedHelp;
		if ((lastGameState.skirmish_order[playerID] == 1 && lastGameState.is_valid_player(lastGameState.skirmish_order_from_player[playerID]) && lastGameState.skirmish_order_from_player[playerID] == num) || lastGameState.skirmish_needs_help[num] > 0)
		{
			MainViewModel.Instance.Allies_OrdersConfirmVis = true;
		}
		else
		{
			MainViewModel.Instance.Allies_OrdersConfirmVis = false;
		}
	}

	public void UpdateEnemies()
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		EngineInterface.PlayState lastGameState = GameData.Instance.lastGameState;
		int num = -1;
		for (int i = 0; i < numEnemies; i++)
		{
			num = sk_enemy[i];
			if (lastGameState.is_skirmish_player(num))
			{
				MainViewModel.Instance.setAlliesFace(i, MainViewModel.Instance.getAIFace(lastGameState.computer_register[num], allowExtendedRemapping: true), MainViewModel.Instance.getAIFaceBackground(num));
				continue;
			}
			MainViewModel.Instance.setAlliesFace(i, Platform_Multiplayer.Instance.GetUserAvatar(Platform_Multiplayer.Instance.getPlayerSteamID(num)), MainViewModel.Instance.getAIFaceBackground(num));
			MainViewModel.Instance.AlliesHumanFaceVis[i] = true;
		}
	}

	public void UpdateGoods()
	{
		if (selectedGoods < 0)
		{
			MainViewModel.Instance.Allies_GoodConfirmVis = false;
			MainViewModel.Instance.AlliesGoodsSelected = null;
			MainViewModel.Instance.AlliesGoods_Info = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_TAUNT);
			return;
		}
		MainViewModel.Instance.AlliesGoodsSelected = MainViewModel.Instance.getSmallGoodsIcon(selectedGoods);
		if (selectedGoodsAmount < 0)
		{
			MainViewModel.Instance.Allies_GoodConfirmVis = false;
			MainViewModel.Instance.AlliesGoods_Info = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_ANGER);
			return;
		}
		if (MainViewModel.Instance.Allies_RequestGoodsViewVis)
		{
			MainViewModel.Instance.Allies_GoodConfirmVis = selectedGoodsAmount > 0;
		}
		else
		{
			MainViewModel.Instance.Allies_GoodConfirmVis = GameData.Instance.lastGameState.resources[selectedGoods] >= selectedGoodsAmount && selectedGoodsAmount > 0;
		}
		MainViewModel.Instance.AlliesGoods_Info = selectedGoodsAmount + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, selectedGoods);
	}

	public void ButtonClicked(string param)
	{
		switch (param)
		{
		case "0":
		case "1":
		case "2":
		case "3":
		case "4":
		case "5":
		{
			int num2 = int.Parse(param);
			if (num2 < num_allies)
			{
				curr_ally = num2;
				UpdateAllies();
			}
			break;
		}
		case "Exit":
			MainViewModel.Instance.AlliesPanelVisible = false;
			break;
		case "Orders":
		{
			for (int j = 0; j < 6; j++)
			{
				MainViewModel.Instance.setAlliesFace(j, null, null);
				MainViewModel.Instance.AlliesHumanFaceVis[j] = false;
			}
			MainViewModel.Instance.Allies_MainViewVis = false;
			UpdateEnemies();
			MainViewModel.Instance.Allies_OrdersViewVis = true;
			break;
		}
		case "Send":
		{
			MainViewModel.Instance.Allies_MainViewVis = false;
			MainViewModel.Instance.Allies_GoodsViewVis = true;
			MainViewModel.Instance.Allies_SendGoodsViewVis = true;
			selectedGoods = -1;
			selectedGoodsAmount = -1;
			req_goods_asked_for = 0;
			int num4 = ally[curr_ally];
			if (GameData.Instance.lastGameState.skirmish_player_requesting_type[num4] > 0)
			{
				req_goods_asked_for = 1;
				selectedGoods = GameData.Instance.lastGameState.skirmish_player_requesting_type[num4];
				selectedGoodsAmount = GameData.Instance.lastGameState.skirmish_player_requesting_amount[num4];
			}
			UpdateGoods();
			break;
		}
		case "Request":
			MainViewModel.Instance.Allies_MainViewVis = false;
			MainViewModel.Instance.Allies_GoodsViewVis = true;
			MainViewModel.Instance.Allies_RequestGoodsViewVis = true;
			selectedGoods = -1;
			selectedGoodsAmount = -1;
			UpdateGoods();
			break;
		case "Back":
		{
			for (int i = 0; i < 6; i++)
			{
				MainViewModel.Instance.setAlliesFace(i, null, null);
				MainViewModel.Instance.AlliesHumanFaceVis[i] = false;
			}
			UpdateAllies();
			CloseAllSubPanels();
			MainViewModel.Instance.Allies_MainViewVis = true;
			break;
		}
		case "Cancel":
			EngineInterface.GameAction(Enums.GameActionCommand.Ally_Orders, 10, ally[curr_ally]);
			ButtonClicked("Back");
			break;
		case "Defend":
			EngineInterface.GameAction(Enums.GameActionCommand.Ally_Orders, 11, ally[curr_ally]);
			ButtonClicked("Back");
			break;
		case "ConfirmOrders":
			EngineInterface.GameAction(Enums.GameActionCommand.Ally_ConfirmOrders, ally[curr_ally], 0);
			break;
		case "CancelOrders":
			EngineInterface.GameAction(Enums.GameActionCommand.Ally_CancelOrders, ally[curr_ally], 0);
			break;
		case "Attack0":
		case "Attack1":
		case "Attack2":
		case "Attack3":
		case "Attack4":
		case "Attack5":
		{
			int num3 = int.Parse(param[6].ToString());
			EngineInterface.GameAction(Enums.GameActionCommand.Ally_Orders, num3 + 1, ally[curr_ally], sk_enemy[num3]);
			ButtonClicked("Back");
			break;
		}
		case "X5":
			if (selectedGoodsAmount < 0)
			{
				selectedGoodsAmount = 0;
			}
			selectedGoodsAmount += 5;
			UpdateGoods();
			break;
		case "X10":
			if (selectedGoodsAmount < 0)
			{
				selectedGoodsAmount = 0;
			}
			selectedGoodsAmount += 10;
			UpdateGoods();
			break;
		case "X25":
			if (selectedGoodsAmount < 0)
			{
				selectedGoodsAmount = 0;
			}
			selectedGoodsAmount += 25;
			UpdateGoods();
			break;
		case "X100":
			if (selectedGoodsAmount < 0)
			{
				selectedGoodsAmount = 0;
			}
			selectedGoodsAmount += 100;
			UpdateGoods();
			break;
		case "X500":
			if (selectedGoodsAmount < 0)
			{
				selectedGoodsAmount = 0;
			}
			selectedGoodsAmount += 500;
			UpdateGoods();
			break;
		case "X5-":
			selectedGoodsAmount -= 5;
			if (selectedGoodsAmount < 0)
			{
				selectedGoodsAmount = 0;
			}
			UpdateGoods();
			break;
		case "X10-":
			selectedGoodsAmount -= 10;
			if (selectedGoodsAmount < 0)
			{
				selectedGoodsAmount = 0;
			}
			UpdateGoods();
			break;
		case "X25-":
			selectedGoodsAmount -= 25;
			if (selectedGoodsAmount < 0)
			{
				selectedGoodsAmount = 0;
			}
			UpdateGoods();
			break;
		case "X100-":
			selectedGoodsAmount -= 100;
			if (selectedGoodsAmount < 0)
			{
				selectedGoodsAmount = 0;
			}
			UpdateGoods();
			break;
		case "X500-":
			selectedGoodsAmount -= 500;
			if (selectedGoodsAmount < 0)
			{
				selectedGoodsAmount = 0;
			}
			UpdateGoods();
			break;
		case "G01":
		case "G02":
		case "G03":
		case "G04":
		case "G05":
		case "G06":
		case "G07":
		case "G08":
		case "G09":
		case "G10":
		case "G11":
		case "G12":
		case "G13":
		case "G14":
		case "G15":
		case "G16":
		case "G17":
		case "G18":
		case "G19":
		case "G20":
		case "G21":
		case "G22":
		case "G23":
		case "G24":
		{
			int num = int.Parse(param.Substring(1));
			selectedGoods = num;
			UpdateGoods();
			break;
		}
		case "ConfirmGoods":
			if (MainViewModel.Instance.Allies_SendGoodsViewVis)
			{
				EngineInterface.GameAction(Enums.GameActionCommand.Ally_SendGoods, ally[curr_ally], selectedGoods, selectedGoodsAmount);
				ButtonClicked("Exit");
			}
			else
			{
				EngineInterface.GameAction(Enums.GameActionCommand.Ally_RequestGoods, ally[curr_ally], selectedGoods, selectedGoodsAmount);
				ButtonClicked("Exit");
			}
			break;
		case "CancelGoods":
			if (req_goods_asked_for > 0 && MainViewModel.Instance.Allies_SendGoodsViewVis)
			{
				EngineInterface.GameAction(Enums.GameActionCommand.Ally_CancelGoodsReq, ally[curr_ally], ally[curr_ally], ally[curr_ally]);
				ButtonClicked("Back");
			}
			selectedGoods = -1;
			selectedGoodsAmount = -1;
			UpdateGoods();
			break;
		}
	}

	public void MouseEnterRolloverHandler(object sender, MouseEventArgs e)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		if (!(((RoutedEventArgs)e).Source is Button) || ((ButtonBase)(Button)((RoutedEventArgs)e).Source).CommandParameter == null || !(((ButtonBase)(Button)((RoutedEventArgs)e).Source).CommandParameter is string))
		{
			return;
		}
		string text = (string)((ButtonBase)(Button)((RoutedEventArgs)e).Source).CommandParameter;
		switch (text)
		{
		case "Orders":
			MainViewModel.Instance.AlliesCommandsRolloverText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_TITLES);
			break;
		case "Send":
			MainViewModel.Instance.AlliesCommandsRolloverText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_BRIEFINGS);
			break;
		case "Request":
			MainViewModel.Instance.AlliesCommandsRolloverText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_ADVISER_PROMPTS);
			break;
		case "Cancel":
			MainViewModel.Instance.AlliesCommandsRolloverText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_CHARACTERS);
			break;
		case "Defend":
			MainViewModel.Instance.AlliesCommandsRolloverText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_RAT);
			break;
		case "Attack0":
		case "Attack1":
		case "Attack2":
		case "Attack3":
		case "Attack4":
		case "Attack5":
		{
			int num = int.Parse(text[6].ToString());
			if (numEnemies > num)
			{
				MainViewModel.Instance.AlliesCommandsRolloverText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES, Enums.eTextValues.TEXT_SCN_SNAKE) + " " + Platform_Multiplayer.Instance.getSkirmishName(sk_enemy[num]);
			}
			break;
		}
		}
	}

	public void MouseLeaveRolloverHandler(object sender, MouseEventArgs e)
	{
		MainViewModel.Instance.AlliesCommandsRolloverText = "";
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_AlliesPanel.xaml");
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
		if (eventName == "MouseEnter" && handlerName == "MouseEnterRolloverHandler")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseEnter += new MouseEventHandler(MouseEnterRolloverHandler);
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "MouseLeaveRolloverHandler")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseLeave += new MouseEventHandler(MouseLeaveRolloverHandler);
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
