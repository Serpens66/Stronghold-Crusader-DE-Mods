using System;
using CrusaderDE;
using Noesis;
using UnityEngine;

public class OnScreenText
{
	public class OST
	{
		public int ostID;

		public bool active;

		public bool activeThisFrame;

		public bool wasTurnedOnOrChanged;

		public bool wasTurnedOff;

		public DateTime timedEnd = DateTime.MinValue;

		public int data1;

		public int data2;

		public int data3;

		public int data4;

		public int data5;

		public int month => data1;

		public int year => data2;

		public int gameSpeed => data1;

		public int message => data1;

		public int curValue => data1;

		public int maxValue => data2;

		public int peopleLeft => data1;
	}

	public static readonly OnScreenText instance;

	public readonly int[] MP_orig_remap_colour_order = new int[9] { 0, 1, 3, 4, 2, 6, 5, 7, 8 };

	public readonly Color[] MPTeamColours = (Color[])(object)new Color[9]
	{
		new Color(1f, 1f, 1f),
		new Color(0.76862746f, 0.007843138f, 0.007843138f),
		new Color(14f / 51f, 14f / 51f, 40f / 51f),
		new Color(40f / 51f, 0.38039216f, 2f / 85f),
		new Color(66f / 85f, 0.7647059f, 0f),
		new Color(48f / 85f, 0f, 48f / 85f),
		new Color(0.5019608f, 0.5019608f, 0.5019608f),
		new Color(3f / 85f, 0.75686276f, 0.7490196f),
		new Color(0.007843138f, 40f / 51f, 0.007843138f)
	};

	public bool inPeaceTime;

	public OST[] OSTs = new OST[32];

	public DateTime startingGoodsExpiry = DateTime.MinValue;

	public DateTime startingGoodsStaticTime = DateTime.MinValue;

	public int[] lastStartingGoodsValues = new int[26];

	public bool startingGoodsPlayedStockpileLine;

	public bool startingGoodsPlayedGranaryLine;

	public bool startingGoodsPlayedArmouryLine;

	public float WhoOwns_Opacity;

	public int WhoOwns_Data1;

	public int WhoOwns_Data2;

	public int WhoOwns_Data3;

	public int WhoOwns_Data4;

	public int WhoOwns_Data5;

	public DateTime nextCartFrame = DateTime.MinValue;

	public int cartFrame;

	public const int BarLength = 494;

	public int lastSoTRank = -1;

	public static OnScreenText Instance => instance;

	static OnScreenText()
	{
		instance = new OnScreenText();
	}

	public void initOST()
	{
		for (int i = 0; i < OSTs.Length; i++)
		{
			OST oST = new OST();
			oST.ostID = i;
			oST.activeThisFrame = false;
			oST.wasTurnedOnOrChanged = false;
			oST.wasTurnedOff = false;
			oST.active = false;
			oST.timedEnd = DateTime.MinValue;
			OSTs[i] = oST;
		}
		for (int j = 0; j < 25; j++)
		{
			lastStartingGoodsValues[j] = -1;
		}
		startingGoodsStaticTime = DateTime.MinValue;
		startingGoodsPlayedStockpileLine = false;
		startingGoodsPlayedGranaryLine = false;
		startingGoodsPlayedArmouryLine = false;
		MainViewModel.Instance.OST_Date_Vis = false;
		MainViewModel.Instance.OST_Game_Paused_Vis_Set = false;
		MainViewModel.Instance.OST_Keep_Message_Vis = false;
		MainViewModel.Instance.OST_Feedback_1_Vis = false;
		MainViewModel.Instance.OST_Message_Bar_Vis = false;
		MainViewModel.Instance.OST_Framerate_Vis = false;
		MainViewModel.Instance.OST_GameSpeed_Vis = false;
		MainViewModel.Instance.OST_Starting_goods_Vis = false;
		MainViewModel.Instance.OST_Time_Until_Vis = false;
		MainViewModel.Instance.OST_PeopleLeft_Vis = false;
		MainViewModel.Instance.OST_WhoOwns_Vis = false;
		MainViewModel.Instance.OST_WhoOwns_VisEditor = false;
		MainViewModel.Instance.OST_Ping_Vis = false;
		MainViewModel.Instance.OST_KOTH_Vis = false;
		MainViewModel.Instance.OST_MissionFinished_Vis = false;
		MainViewModel.Instance.OST_MP_Game_Over_Vis = false;
		MainViewModel.Instance.OST_MessageFrom_Vis = false;
		for (int k = 0; k < 25; k++)
		{
			MainViewModel.Instance.FeedInGoodsVisible[k] = false;
		}
		inPeaceTime = false;
		if (GameData.Instance.IsSandsOfTime())
		{
			lastSoTRank = -1;
			UpdateSandsOfTimer();
			MainViewModel.Instance.Show_OST_SandsOfTimeVis = ConfigSettings.Settings_ShowSandsTimer && !ConfigSettings.Settings_HideSoTTiming;
		}
		else
		{
			MainViewModel.Instance.Show_OST_SandsOfTimeVis = false;
		}
		MainViewModel.Instance.Show_OST_SandsOfTimeVis_Target = false;
		MainViewModel.Instance.Show_OST_SandsOfTimeVis_Time = true;
	}

	public void addOSTEntry(Enums.eOnScreenText ostID, int data1, int data2 = 2, int data3 = 0, int data4 = 0, int data5 = 0)
	{
		if ((ostID == Enums.eOnScreenText.OST_KEEP_MESSAGE && GameData.Instance.game_type == 4) || ostID < Enums.eOnScreenText.OST_CHAT || (int)ostID >= OSTs.Length)
		{
			return;
		}
		OST oST = OSTs[(int)ostID];
		if (!oST.active)
		{
			oST.wasTurnedOnOrChanged = true;
		}
		oST.activeThisFrame = true;
		switch (ostID)
		{
		case Enums.eOnScreenText.OST_GAME_SPEED:
			if (oST.data1 != data1)
			{
				oST.wasTurnedOnOrChanged = true;
			}
			oST.data1 = data1;
			oST.timedEnd = DateTime.UtcNow.AddSeconds(5.0);
			break;
		case Enums.eOnScreenText.OST_GAME_PAUSED:
			if (data1 == 1)
			{
				oST.timedEnd = DateTime.UtcNow.AddYears(5);
				oST.active = (oST.activeThisFrame = true);
			}
			else
			{
				oST.timedEnd = DateTime.UtcNow.AddSeconds(-1.0);
			}
			break;
		case Enums.eOnScreenText.OST_KEEP_MESSAGE:
			oST.timedEnd = DateTime.UtcNow.AddYears(5);
			if (oST.data1 != data1)
			{
				oST.wasTurnedOnOrChanged = true;
			}
			oST.data1 = data1;
			oST.data2 = data2;
			break;
		case Enums.eOnScreenText.OST_STARTING_GOODS:
		case Enums.eOnScreenText.OST_PEOPLE_LEFT:
			oST.data1 = data1;
			oST.data2 = data2;
			break;
		case Enums.eOnScreenText.OST_FEEDBACK_1:
		case Enums.eOnScreenText.OST_FEEDBACK_2:
			oST.data1 = data1;
			oST.data2 = data2;
			oST.timedEnd = DateTime.UtcNow.AddSeconds(5.0);
			break;
		case Enums.eOnScreenText.OST_MISSION_FINISHED:
			oST.data1 = data1;
			oST.timedEnd = DateTime.UtcNow.AddYears(5);
			break;
		case Enums.eOnScreenText.OST_WIN_TIMER:
		case Enums.eOnScreenText.OST_TIMETODEFEAT:
			oST.data1 = data1;
			oST.data2 = data2;
			oST.timedEnd = DateTime.UtcNow.AddYears(5);
			break;
		case Enums.eOnScreenText.OST_PEACETIMER:
			oST.data1 = data1;
			oST.data2 = data2;
			oST.timedEnd = DateTime.UtcNow.AddSeconds(5.0);
			break;
		case Enums.eOnScreenText.OST_FRAMERATE:
			oST.timedEnd = DateTime.UtcNow.AddYears(5);
			break;
		case Enums.eOnScreenText.OST_MESSAGE_BAR:
		{
			oST.data1 = data1;
			oST.data2 = data2;
			int num = 10;
			oST.timedEnd = DateTime.UtcNow.AddSeconds(num);
			break;
		}
		case Enums.eOnScreenText.OST_PINGS:
		case Enums.eOnScreenText.OST_KING_OF_THE_HILL:
			oST.timedEnd = DateTime.UtcNow.AddYears(5);
			break;
		case Enums.eOnScreenText.OST_WHO_OWNS:
			oST.data1 = data1;
			oST.data2 = data2;
			oST.data3 = data3;
			oST.data4 = data4;
			oST.data5 = data5;
			oST.timedEnd = DateTime.UtcNow.AddYears(5);
			break;
		case Enums.eOnScreenText.OST_MP_GAME_OVER:
			oST.timedEnd = DateTime.UtcNow.AddYears(5);
			oST.data1 = data1;
			oST.data2 = data2;
			break;
		case (Enums.eOnScreenText)7:
		case (Enums.eOnScreenText)8:
		case (Enums.eOnScreenText)9:
		case (Enums.eOnScreenText)10:
		case Enums.eOnScreenText.OST_POPULARITY:
		case (Enums.eOnScreenText)13:
		case (Enums.eOnScreenText)14:
		case (Enums.eOnScreenText)15:
		case (Enums.eOnScreenText)18:
		case Enums.eOnScreenText.OST_SPLIT_MESSAGE:
		case Enums.eOnScreenText.OST_PING_ERROR:
			break;
		}
	}

	public void removeOSTEntry(Enums.eOnScreenText ostID)
	{
		if (ostID >= Enums.eOnScreenText.OST_CHAT && (int)ostID < OSTs.Length)
		{
			OST oST = OSTs[(int)ostID];
			switch (ostID)
			{
			case Enums.eOnScreenText.OST_FRAMERATE:
			case Enums.eOnScreenText.OST_KEEP_MESSAGE:
			case Enums.eOnScreenText.OST_WHO_OWNS:
			case Enums.eOnScreenText.OST_PINGS:
			case Enums.eOnScreenText.OST_KING_OF_THE_HILL:
			case Enums.eOnScreenText.OST_WIN_TIMER:
			case Enums.eOnScreenText.OST_TIMETODEFEAT:
			case Enums.eOnScreenText.OST_PEACETIMER:
				oST.timedEnd = DateTime.UtcNow.AddSeconds(-1.0);
				break;
			}
		}
	}

	public void Update()
	{
		if (MainViewModel.Instance.OST_Cart_Vis && DateTime.UtcNow > nextCartFrame)
		{
			nextCartFrame = DateTime.UtcNow.AddMilliseconds(200.0);
			switch (cartFrame)
			{
			case 0:
				OST_StartingGoods.refCart.Source = MainViewModel.Instance.GameSprites[346];
				break;
			case 1:
				OST_StartingGoods.refCart.Source = MainViewModel.Instance.GameSprites[347];
				break;
			case 2:
				OST_StartingGoods.refCart.Source = MainViewModel.Instance.GameSprites[348];
				break;
			case 3:
				OST_StartingGoods.refCart.Source = MainViewModel.Instance.GameSprites[349];
				break;
			case 4:
				OST_StartingGoods.refCart.Source = MainViewModel.Instance.GameSprites[350];
				break;
			case 5:
				OST_StartingGoods.refCart.Source = MainViewModel.Instance.GameSprites[351];
				break;
			}
			cartFrame++;
			if (cartFrame >= 6)
			{
				cartFrame = 0;
			}
		}
		if (MainViewModel.Instance.Show_OST_SandsOfTimeVis)
		{
			UpdateSandsOfTimer();
		}
	}

	public void updateOST(EngineInterface.PlayState gameState, bool allowExpire = true)
	{
		//IL_06fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0700: Unknown result type (might be due to invalid IL or missing references)
		//IL_070a: Expected O, but got Unknown
		//IL_06d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_06e0: Expected O, but got Unknown
		//IL_139b: Unknown result type (might be due to invalid IL or missing references)
		//IL_15b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_15b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_15ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_15c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_15d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_15e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_15e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_15f0: Expected O, but got Unknown
		//IL_190c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1911: Unknown result type (might be due to invalid IL or missing references)
		//IL_1913: Unknown result type (might be due to invalid IL or missing references)
		//IL_1921: Unknown result type (might be due to invalid IL or missing references)
		//IL_192f: Unknown result type (might be due to invalid IL or missing references)
		//IL_193d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1942: Unknown result type (might be due to invalid IL or missing references)
		//IL_1949: Expected O, but got Unknown
		if (!MainViewModel.Instance.IsMapEditorMode)
		{
			OST oST = OSTs[1];
			oST.data1 = gameState.month;
			oST.data2 = gameState.year;
			if (!oST.active)
			{
				oST.wasTurnedOnOrChanged = true;
				MainViewModel.Instance.OST_Date_Vis = true;
			}
			oST.activeThisFrame = true;
		}
		if (allowExpire)
		{
			OST[] oSTs = OSTs;
			foreach (OST oST2 in oSTs)
			{
				if (oST2.timedEnd != DateTime.MinValue)
				{
					if (oST2.timedEnd < DateTime.UtcNow)
					{
						oST2.timedEnd = DateTime.MinValue;
						oST2.activeThisFrame = false;
					}
					else
					{
						oST2.activeThisFrame = true;
					}
				}
				if (oST2.active && !oST2.activeThisFrame)
				{
					oST2.wasTurnedOff = true;
				}
				oST2.active = oST2.activeThisFrame;
				oST2.activeThisFrame = false;
			}
		}
		bool wasTurnedOff = false;
		bool wasTurnedOnOrChanged = false;
		bool handleStatChange = true;
		OST oST3 = getOST(Enums.eOnScreenText.OST_DATE, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
		if (oST3 != null)
		{
			MainViewModel.Instance.OST_Date_Text = " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, oST3.month) + " " + oST3.year + " ";
		}
		else if (wasTurnedOff)
		{
			MainViewModel.Instance.OST_Date_Vis = false;
		}
		oST3 = getOST(Enums.eOnScreenText.OST_GAME_PAUSED, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
		if (oST3 != null)
		{
			if (wasTurnedOnOrChanged)
			{
				MainViewModel.Instance.OST_Game_Paused_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_FEEDBACK, 22);
				MainViewModel.Instance.OST_Game_Paused_Vis_Set = true;
			}
		}
		else if (wasTurnedOff)
		{
			MainViewModel.Instance.OST_Game_Paused_Vis_Set = false;
		}
		bool flag = false;
		oST3 = getOST(Enums.eOnScreenText.OST_KEEP_MESSAGE, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
		if (oST3 != null)
		{
			flag = true;
			if (wasTurnedOnOrChanged)
			{
				if (oST3.message == 1)
				{
					SFXManager.instance.playSpeech(1, "other_warning1.wav", 1f);
				}
				else
				{
					SFXManager.instance.playSpeech(1, "other_warning2.wav", 1f);
				}
				MainViewModel.Instance.OST_Keep_Message_Text = "  " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_FEEDBACK, 10 + oST3.message - 1) + "  ";
				MainViewModel.Instance.OST_Keep_Message_Vis = true;
			}
		}
		else if (wasTurnedOff)
		{
			MainViewModel.Instance.OST_Keep_Message_Vis = false;
		}
		if (MainViewModel.Instance.IsMapEditorMode && GameData.Instance.mapType == Enums.GameModes.BUILD && GameData.Instance.lastGameState != null)
		{
			if (GameData.Instance.lastGameState.gotSignpost > 0 || GameData.Instance.multiplayerMap)
			{
				MainViewModel.Instance.OST_Keep_Message_Vis = false;
			}
			else
			{
				MainViewModel.Instance.OST_Keep_Message_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 86);
				MainViewModel.Instance.OST_Keep_Message_Vis = true;
			}
		}
		oST3 = getOST(Enums.eOnScreenText.OST_FEEDBACK_1, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
		if (oST3 != null)
		{
			if (wasTurnedOnOrChanged)
			{
				if (oST3.message == 99)
				{
					MainViewModel.Instance.OST_Feedback_1_Text = "  " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 440) + "  ";
				}
				else
				{
					MainViewModel.Instance.OST_Feedback_1_Text = "  " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_FEEDBACK, oST3.message) + "  ";
				}
				MainViewModel.Instance.OST_Feedback_1_Vis = true;
			}
		}
		else if (wasTurnedOff)
		{
			MainViewModel.Instance.OST_Feedback_1_Vis = false;
		}
		oST3 = getOST(Enums.eOnScreenText.OST_MESSAGE_BAR, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
		if (oST3 != null)
		{
			if (MainViewModel.Instance.Show_HUD_Briefing)
			{
				oST3.timedEnd = DateTime.UtcNow.AddSeconds(10.0);
			}
			if (wasTurnedOnOrChanged)
			{
				MainViewModel.Instance.OST_Message_Bar_Vis = true;
				if (oST3.data1 > 0)
				{
					MainViewModel.Instance.OST_Message_Bar_Text = Translate.Instance.lookUpText((Enums.eTextSections)oST3.data1, oST3.data2);
				}
				else
				{
					bool flag2 = true;
					int num = -oST3.data1;
					if (CustomisationFileManager.CustomMediaExists && ((MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo != null && MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.aivs != null) || MainViewModel.Instance.HUDIngameMenu.restartMPInfo != null))
					{
						string lordName = ((MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo == null) ? MapFileManager.SplitCustomTrailName(MainViewModel.Instance.HUDIngameMenu.restartMPInfo.LordNames[num - 1]).ToLower() : MapFileManager.SplitCustomTrailName(MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.aivs[num - 1].lordName).ToLower());
						string customLordText = CustomisationFileManager.Instance.GetCustomLordText(lordName, oST3.data2);
						if (customLordText != null)
						{
							MainViewModel.Instance.OST_Message_Bar_Text = customLordText;
							flag2 = false;
						}
					}
					if (flag2)
					{
						MainViewModel.Instance.OST_Message_Bar_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKIRMISH_SPEECH, oST3.data2 + 986);
					}
				}
			}
			if (GameData.Instance.lastGameState.app_mode == 16 && GameData.Instance.lastGameState.app_sub_mode == 1)
			{
				MainViewModel.Instance.OST_Message_Bar_Margin = "0,0,70,210";
			}
			else if (!flag)
			{
				MainViewModel.Instance.OST_Message_Bar_Margin = "0,0,70,160";
			}
			else
			{
				MainViewModel.Instance.OST_Message_Bar_Margin = "0,0,70,200";
			}
		}
		else if (wasTurnedOff)
		{
			MainViewModel.Instance.OST_Message_Bar_Vis = false;
		}
		oST3 = getOST(Enums.eOnScreenText.OST_FRAMERATE, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
		if (oST3 != null)
		{
			MainViewModel.Instance.OST_Framerate_Vis = true;
			MainViewModel.Instance.OST_Framerate_Text = " " + EditorDirector.instance.CurrentFPS + " fps ";
			MainViewModel.Instance.OST_Simrate_Text = " " + EditorDirector.instance.CurrentTicks + " sim ";
			if (MainViewModel.Instance.Show_HUD_Scenario_Button)
			{
				MainViewModel.Instance.OST_Framerate_Margin = "0,7,240,0";
				MainViewModel.Instance.OST_Simrate_Margin = "0,35,240,0";
			}
			else if (ConfigSettings.Settings_Compass)
			{
				MainViewModel.Instance.OST_Framerate_Margin = "0,7,90,0";
				MainViewModel.Instance.OST_Simrate_Margin = "0,35,90,0";
			}
			else
			{
				MainViewModel.Instance.OST_Framerate_Margin = "0,7,10,0";
				MainViewModel.Instance.OST_Simrate_Margin = "0,35,10,0";
			}
		}
		else if (wasTurnedOff)
		{
			MainViewModel.Instance.OST_Framerate_Vis = false;
		}
		oST3 = getOST(Enums.eOnScreenText.OST_GAME_SPEED, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
		if (oST3 != null)
		{
			if (wasTurnedOnOrChanged)
			{
				MainViewModel.Instance.OST_GameSpeed_Vis = true;
				MainViewModel.Instance.OST_GameSpeed_Text = " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_FEEDBACK, 23) + " " + oST3.gameSpeed + " ";
			}
			if (oST3.gameSpeed == 40)
			{
				MainViewModel.Instance.OST_GameSpeed_Colour = new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)218, (byte)165, (byte)32));
			}
			else
			{
				MainViewModel.Instance.OST_GameSpeed_Colour = new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)239, (byte)243, (byte)198));
			}
			if (MainViewModel.Instance.Show_HUD_Scenario_Button)
			{
				MainViewModel.Instance.OST_GameSpeed_Margin = "0,60,240,-21";
			}
			else if (ConfigSettings.Settings_Compass)
			{
				MainViewModel.Instance.OST_GameSpeed_Margin = "0,60,90,-21";
			}
			else
			{
				MainViewModel.Instance.OST_GameSpeed_Margin = "0,60,10,-21";
			}
		}
		else if (wasTurnedOff)
		{
			MainViewModel.Instance.OST_GameSpeed_Vis = false;
		}
		oST3 = getOST(Enums.eOnScreenText.OST_STARTING_GOODS, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
		if (oST3 != null)
		{
			if (wasTurnedOnOrChanged)
			{
				if (!MainViewModel.Instance.OST_Starting_goods_Vis)
				{
					startCart();
				}
				MainViewModel.Instance.OST_Starting_goods_Vis = true;
				string text = "  " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_STARTUP, oST3.message) + "  ";
				if (MainViewModel.Instance.FeedInGoodsAmountList[0] != text)
				{
					MainViewModel.Instance.FeedInGoodsAmountList[0] = text;
				}
				MainViewModel.Instance.FeedInGoodsVisible[0] = true;
				startingGoodsExpiry = DateTime.UtcNow.AddSeconds(20.0);
				startingGoodsStaticTime = DateTime.UtcNow.AddSeconds(10.0);
				for (int j = 1; j < 25; j++)
				{
					MainViewModel.Instance.FeedInGoodsVisible[j] = GameData.Instance.lastGameState.keep_storage[j] > 0;
				}
			}
			if (oST3.message == 0 && startingGoodsExpiry < DateTime.UtcNow)
			{
				startingGoodsExpiry = DateTime.MinValue;
				MainViewModel.Instance.FeedInGoodsVisible[0] = false;
			}
			if (oST3.message == 0)
			{
				for (int k = 1; k < 25; k++)
				{
					if (lastStartingGoodsValues[k] != GameData.Instance.lastGameState.keep_storage[k])
					{
						startingGoodsStaticTime = DateTime.UtcNow.AddSeconds(10.0);
					}
					lastStartingGoodsValues[k] = GameData.Instance.lastGameState.keep_storage[k];
				}
				if ((GameData.Instance.lastGameState.app_mode == 14 && (GameData.Instance.lastGameState.app_sub_mode == 49 || GameData.Instance.lastGameState.app_sub_mode == 13 || GameData.Instance.lastGameState.app_sub_mode == 48)) || Director.instance.Paused || MainViewModel.Instance.OST_Game_Paused_Vis_Set)
				{
					startingGoodsStaticTime = DateTime.UtcNow.AddSeconds(10.0);
				}
				if (startingGoodsStaticTime < DateTime.UtcNow)
				{
					MainViewModel.Instance.FeedInGoodsVisible[0] = true;
					string text2 = "";
					bool flag3 = false;
					bool flag4 = false;
					bool flag5 = false;
					if (lastStartingGoodsValues[1] > 0 || lastStartingGoodsValues[2] > 0 || lastStartingGoodsValues[3] > 0 || lastStartingGoodsValues[4] > 0 || lastStartingGoodsValues[5] > 0 || lastStartingGoodsValues[6] > 0 || lastStartingGoodsValues[7] > 0 || lastStartingGoodsValues[8] > 0 || lastStartingGoodsValues[9] > 0 || lastStartingGoodsValues[16] > 0)
					{
						flag3 = true;
					}
					if (lastStartingGoodsValues[10] > 0 || lastStartingGoodsValues[11] > 0 || lastStartingGoodsValues[13] > 0 || lastStartingGoodsValues[12] > 0)
					{
						flag4 = true;
					}
					if (lastStartingGoodsValues[17] > 0 || lastStartingGoodsValues[18] > 0 || lastStartingGoodsValues[19] > 0 || lastStartingGoodsValues[20] > 0 || lastStartingGoodsValues[21] > 0 || lastStartingGoodsValues[22] > 0 || lastStartingGoodsValues[23] > 0 || lastStartingGoodsValues[24] > 0)
					{
						flag5 = true;
					}
					if (flag3)
					{
						text2 = "  " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 132) + "  ";
						if (!startingGoodsPlayedStockpileLine)
						{
							SFXManager.instance.playSpeech(1, "Placement_Warning11.wav", 1f);
						}
						startingGoodsPlayedStockpileLine = true;
					}
					else if (flag4)
					{
						text2 = "  " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 133) + "  ";
						if (!startingGoodsPlayedGranaryLine)
						{
							if ((GameData.Instance.lastGameState.gotMarket & 4) > 0)
							{
								SFXManager.instance.playSpeech(1, "Placement_Warning10.wav", 1f);
							}
							else
							{
								SFXManager.instance.playSpeech(1, "Space_Warning1.wav", 1f);
							}
						}
						startingGoodsPlayedGranaryLine = true;
					}
					else if (flag5)
					{
						text2 = "  " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 134) + "  ";
						if (!startingGoodsPlayedArmouryLine)
						{
							if ((GameData.Instance.lastGameState.gotMarket & 8) > 0)
							{
								SFXManager.instance.playSpeech(1, "Placement_Warning12.wav", 1f);
							}
							else
							{
								SFXManager.instance.playSpeech(1, "Space_Warning3.wav", 1f);
							}
						}
						startingGoodsPlayedArmouryLine = true;
					}
					if (!flag3)
					{
						startingGoodsPlayedStockpileLine = false;
					}
					if (!flag4)
					{
						startingGoodsPlayedGranaryLine = false;
					}
					if (!flag5)
					{
						startingGoodsPlayedArmouryLine = false;
					}
					if (MainViewModel.Instance.FeedInGoodsAmountList[0] != text2)
					{
						MainViewModel.Instance.FeedInGoodsAmountList[0] = text2;
					}
				}
				else
				{
					startingGoodsPlayedStockpileLine = false;
					startingGoodsPlayedGranaryLine = false;
					startingGoodsPlayedArmouryLine = false;
				}
			}
			for (int l = 1; l < 25; l++)
			{
				if (GameData.Instance.lastGameState.keep_storage[l] > 0)
				{
					string text3 = "  " + GameData.Instance.lastGameState.keep_storage[l];
					if (MainViewModel.Instance.FeedInGoodsAmountList[l] != text3)
					{
						MainViewModel.Instance.FeedInGoodsAmountList[l] = text3;
					}
					if (!MainViewModel.Instance.FeedInGoodsVisible[l])
					{
						MainViewModel.Instance.FeedInGoodsVisible[l] = true;
					}
				}
				else if (MainViewModel.Instance.FeedInGoodsVisible[l])
				{
					MainViewModel.Instance.FeedInGoodsVisible[l] = false;
				}
			}
		}
		else if (wasTurnedOff)
		{
			for (int m = 0; m < 25; m++)
			{
				MainViewModel.Instance.FeedInGoodsVisible[m] = false;
			}
			MainViewModel.Instance.OST_Starting_goods_Vis = false;
		}
		bool flag6 = false;
		oST3 = getOST(Enums.eOnScreenText.OST_TIMETODEFEAT, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
		if (oST3 != null)
		{
			flag6 = true;
			if (wasTurnedOnOrChanged)
			{
				MainViewModel.Instance.OST_Time_Until_Vis = true;
				MainViewModel.Instance.OST_Time_Until_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_OBJECTIVES, 17);
			}
			if (oST3.maxValue > 0)
			{
				MainViewModel.Instance.OST_Time_Until_Width = Math.Min((oST3.maxValue - oST3.curValue) * 158 / oST3.maxValue, 158);
			}
			else
			{
				MainViewModel.Instance.OST_Time_Until_Width = 0;
			}
		}
		else if (wasTurnedOff)
		{
			MainViewModel.Instance.OST_Time_Until_Vis = false;
		}
		if (!flag6)
		{
			oST3 = getOST(Enums.eOnScreenText.OST_WIN_TIMER, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
			if (oST3 != null)
			{
				flag6 = true;
				if (wasTurnedOnOrChanged)
				{
					MainViewModel.Instance.OST_Time_Until_Vis = true;
					MainViewModel.Instance.OST_Time_Until_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_OBJECTIVES, 25);
				}
				if (oST3.maxValue > 0)
				{
					MainViewModel.Instance.OST_Time_Until_Width = Math.Min((oST3.maxValue - oST3.curValue) * 158 / oST3.maxValue, 158);
				}
				else
				{
					MainViewModel.Instance.OST_Time_Until_Width = 0;
				}
			}
			else if (wasTurnedOff)
			{
				MainViewModel.Instance.OST_Time_Until_Vis = false;
			}
		}
		inPeaceTime = false;
		if (!flag6)
		{
			oST3 = getOST(Enums.eOnScreenText.OST_PEACETIMER, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
			if (oST3 != null)
			{
				inPeaceTime = true;
				if (wasTurnedOnOrChanged)
				{
					MainViewModel.Instance.OST_Time_Until_Vis = true;
					MainViewModel.Instance.OST_Time_Until_Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 99);
				}
				if (oST3.maxValue > 0)
				{
					MainViewModel.Instance.OST_Time_Until_Width = Math.Min((oST3.maxValue - oST3.curValue) * 158 / oST3.maxValue, 158);
				}
				else
				{
					MainViewModel.Instance.OST_Time_Until_Width = 0;
				}
			}
			else if (wasTurnedOff)
			{
				MainViewModel.Instance.OST_Time_Until_Vis = false;
			}
		}
		oST3 = getOST(Enums.eOnScreenText.OST_PEOPLE_LEFT, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
		if (oST3 != null)
		{
			if (wasTurnedOnOrChanged)
			{
				MainViewModel.Instance.OST_PeopleLeft_Vis = true;
			}
			MainViewModel.Instance.OST_PeopleLeft_Text = GameData.Instance.lastGameState.chimps_count + "/" + GameData.Instance.lastGameState.chimps_limit;
			MainViewModel.Instance.OST_StructsLeft_Text = GameData.Instance.lastGameState.structs_count + "/" + GameData.Instance.lastGameState.structs_limit;
			MainViewModel.Instance.OST_TreesLeft_Text = GameData.Instance.lastGameState.orgs_count + "/" + GameData.Instance.lastGameState.orgs_limit;
			MainViewModel.Instance.OST_RocksLeft_Text = GameData.Instance.lastGameState.minerals_count + "/" + GameData.Instance.lastGameState.minerals_limit;
			MainViewModel.Instance.OST_TribesLeft_Text = GameData.Instance.lastGameState.tribes_count + "/" + GameData.Instance.lastGameState.tribes_limit;
		}
		else if (wasTurnedOff)
		{
			MainViewModel.Instance.OST_PeopleLeft_Vis = false;
		}
		if (GameData.Instance.lastGameState.messageFromcharacter > 0 && GameData.Instance.lastGameState.messageFromcharacter < 9)
		{
			MainViewModel.Instance.OST_WhoOwns_Vis = false;
			MainViewModel.Instance.OST_WhoOwns_Opacity = 0f;
			MainViewModel.Instance.OST_WhoOwns_Text = "  " + getComputerName(GameData.Instance.lastGameState.computer_register[GameData.Instance.lastGameState.messageFromcharacter], GameData.Instance.lastGameState.computer_names[GameData.Instance.lastGameState.messageFromcharacter]) + "  ";
			MainViewModel.Instance.OST_WhoOwns_FaceBackground = MainViewModel.Instance.getAIFaceBackground(GameData.Instance.lastGameState.messageFromcharacter);
			MainViewModel.Instance.OST_WhoOwns_Face = MainViewModel.Instance.getAIFace(GameData.Instance.lastGameState.computer_register[GameData.Instance.lastGameState.messageFromcharacter], allowExtendedRemapping: true);
			MainViewModel.Instance.OST_MessageFrom_Vis = true;
		}
		else
		{
			MainViewModel.Instance.OST_MessageFrom_Vis = false;
			bool flag7 = false;
			oST3 = getOST(Enums.eOnScreenText.OST_WHO_OWNS, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
			if (MainViewModel.Instance.IsMapEditorMode)
			{
				if (oST3 != null)
				{
					int data = oST3.data1;
					string text4 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 205 + data - 1);
					if (text4.Length > 0)
					{
						MainViewModel.Instance.OST_WhoOwns_VisEditor = true;
						MainViewModel.Instance.OST_WhoOwns_Text = text4;
					}
					else
					{
						MainViewModel.Instance.OST_WhoOwns_VisEditor = false;
					}
				}
				else if (wasTurnedOff)
				{
					MainViewModel.Instance.OST_WhoOwns_VisEditor = false;
				}
			}
			else
			{
				if (oST3 != null)
				{
					flag7 = true;
					WhoOwns_Data1 = oST3.data1;
					WhoOwns_Data2 = oST3.data2;
					WhoOwns_Data3 = oST3.data3;
					WhoOwns_Data4 = oST3.data4;
					WhoOwns_Data5 = oST3.data5;
					if (WhoOwns_Opacity < 2f)
					{
						WhoOwns_Opacity += Time.deltaTime * 5f;
					}
				}
				else if (WhoOwns_Opacity > 0f)
				{
					WhoOwns_Opacity -= Time.deltaTime * 5f;
					if (WhoOwns_Opacity <= 0f)
					{
						WhoOwns_Opacity = -1f;
					}
					flag7 = true;
				}
				else
				{
					MainViewModel.Instance.OST_WhoOwns_Vis = false;
				}
				if (flag7)
				{
					if (WhoOwns_Opacity <= 0f)
					{
						MainViewModel.Instance.OST_WhoOwns_Opacity = 0f;
					}
					else if (WhoOwns_Opacity >= 1f)
					{
						MainViewModel.Instance.OST_WhoOwns_Opacity = 1f;
					}
					else
					{
						MainViewModel.Instance.OST_WhoOwns_Opacity = WhoOwns_Opacity;
					}
					int whoOwns_Data = WhoOwns_Data1;
					string text5;
					if (WhoOwns_Data2 < 1)
					{
						if (!Director.instance.SkirmishModeGame)
						{
							text5 = "  " + Platform_Multiplayer.Instance.getPlayerName(whoOwns_Data) + "  ";
							MainViewModel.Instance.OST_WhoOwns_Face = Platform_Multiplayer.Instance.GetUserAvatar(Platform_Multiplayer.Instance.getPlayerSteamID(whoOwns_Data));
						}
						else
						{
							text5 = "  " + ConfigSettings.Settings_UserName + "  ";
							MainViewModel.Instance.OST_WhoOwns_Face = Platform_Multiplayer.Instance.GetLocalAvatar();
						}
						MainViewModel.Instance.OST_WhoOwns_FaceBackground = MainViewModel.Instance.getAIFaceBackground(whoOwns_Data);
						MainViewModel.Instance.OST_WhoOwns_Allies = MainViewModel.Instance.getTeamAlliesShield(WhoOwns_Data3);
					}
					else
					{
						text5 = "  " + getComputerName(WhoOwns_Data2, WhoOwns_Data4) + "  ";
						MainViewModel.Instance.OST_WhoOwns_FaceBackground = MainViewModel.Instance.getAIFaceBackground(whoOwns_Data);
						MainViewModel.Instance.OST_WhoOwns_Face = MainViewModel.Instance.getAIFace(WhoOwns_Data2, allowExtendedRemapping: true);
						MainViewModel.Instance.OST_WhoOwns_Allies = MainViewModel.Instance.getTeamAlliesShield(WhoOwns_Data3);
					}
					if (text5.Length > 0)
					{
						MainViewModel.Instance.OST_WhoOwns_Vis = true;
						MainViewModel.Instance.OST_WhoOwns_Text = text5;
					}
					else
					{
						MainViewModel.Instance.OST_WhoOwns_Vis = false;
					}
					string text6 = MainViewModel.Instance.HUDBuildingPanel.GetBuildingName(WhoOwns_Data5);
					if (text6 != "")
					{
						text6 = "  " + Translate.Instance.lookUpText(text6) + "  ";
					}
					MainViewModel.Instance.OST_WhoOwns_Building = text6;
				}
			}
		}
		oST3 = getOST(Enums.eOnScreenText.OST_PINGS, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
		if (oST3 != null)
		{
			if (wasTurnedOnOrChanged)
			{
				MainViewModel.Instance.OST_Ping_Vis = true;
				for (int n = 1; n < 9; n++)
				{
					MainViewModel.Instance.OST_Ping_Visible[n - 1] = false;
				}
				for (int num2 = 1; num2 < 9; num2++)
				{
					string playerName = Platform_Multiplayer.Instance.getPlayerName(num2, activeOnly: true, excludeSkirmish: true);
					if (playerName.Length > 0 && num2 != GameData.Instance.playerID)
					{
						MainViewModel.Instance.OST_Ping_Visible[num2 - 1] = true;
						MainViewModel.Instance.OST_Ping_Name[num2 - 1] = playerName;
						Color val = MPTeamColours[SpriteMapping.remapColours[SpriteMapping.RemapMPLoadedColour(num2)]];
						SolidColorBrush value = new SolidColorBrush(Color.FromRgb((byte)(val.r * 255f), (byte)(val.g * 255f), (byte)(val.b * 255f)));
						MainViewModel.Instance.OST_Ping_Color[num2 - 1] = value;
					}
				}
			}
			for (int num3 = 1; num3 < 9; num3++)
			{
				MainViewModel.Instance.OST_Ping_Value[num3 - 1] = GameData.Instance.lastGameState.pingtimes[num3 - 1] + "ms";
				if (GameData.Instance.lastGameState.pingtimes[num3 - 1] < 70)
				{
					MainViewModel.Instance.OST_Ping_Value_Color[num3 - 1] = MainViewModel.Instance._OST_PingLow_Colour;
				}
				else if (GameData.Instance.lastGameState.pingtimes[num3 - 1] < 150)
				{
					MainViewModel.Instance.OST_Ping_Value_Color[num3 - 1] = MainViewModel.Instance._OST_PingMid_Colour;
				}
				else
				{
					MainViewModel.Instance.OST_Ping_Value_Color[num3 - 1] = MainViewModel.Instance._OST_PingHigh_Colour;
				}
			}
		}
		else if (wasTurnedOff)
		{
			MainViewModel.Instance.OST_Ping_Vis = false;
		}
		oST3 = getOST(Enums.eOnScreenText.OST_MP_GAME_OVER, ref wasTurnedOff, ref wasTurnedOnOrChanged, handleStatChange);
		if (oST3 != null)
		{
			if (!wasTurnedOnOrChanged)
			{
				return;
			}
			MainViewModel.Instance.OST_MP_Game_Over_Vis = true;
			MainViewModel.Instance.SetObjectivePopupState(visible: false);
			MainViewModel.Instance.SetGoodsPopupState(visible: false);
			MainViewModel.Instance.Show_HUD_Extras = false;
			string text7 = ((oST3.message != 1) ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, Enums.eTextValues.TEXT_SCN_NEW_MESSAGE) : Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, Enums.eTextValues.TEXT_SCN_MOOD4));
			MainViewModel.Instance.OST_MP_Game_Over_Text = text7;
			int lord_Type = GameData.Instance.lastGameState.lord_Type;
			int faction = 0;
			switch (lord_Type)
			{
			case 1:
			case 6:
				faction = 1;
				break;
			case 2:
			case 7:
				faction = 2;
				break;
			}
			MainViewModel.Instance.IngameUI.triggerVideos(text7, faction, oST3.message == 1);
			if (Director.instance.MultiplayerGame && GameData.Instance.coopTrailID > 0)
			{
				ulong steamID = MainViewModel.Instance.FRONTMultiplayer.PreCreateCoopLobby(GameData.Instance.coopTrailID - 1, GameData.Instance.coopMissionID);
				if (oST3.message == 1)
				{
					ConfigSettings.CoopCompleted(steamID, GameData.Instance.coopTrailID - 1, GameData.Instance.coopMissionID, Platform_Multiplayer.Instance.LastCoAString);
				}
			}
			else if (Director.instance.SkirmishModeGame && GameData.Instance.coopTrailID > 0 && oST3.message == 1)
			{
				ConfigSettings.CoopCompleted((ulong)(999 + GameData.Instance.coopMissionAlly), GameData.Instance.coopTrailID - 1, GameData.Instance.coopMissionID);
			}
			for (int num4 = 0; num4 < 8; num4++)
			{
				int num5 = num4 + 1;
				if ((oST3.data2 & (1 << num4)) != 0)
				{
					MainViewModel.Instance.OST_MP_Game_Over_Row_Text[num5] = Platform_Multiplayer.Instance.getSkirmishName(num5) + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_SPEARS);
					Color val2 = MPTeamColours[SpriteMapping.remapColours[SpriteMapping.RemapMPLoadedColour(num5)]];
					SolidColorBrush value2 = new SolidColorBrush(Color.FromRgb((byte)(val2.r * 255f), (byte)(val2.g * 255f), (byte)(val2.b * 255f)));
					MainViewModel.Instance.OST_MP_Game_Over_Row_Colour[num5] = value2;
					MainViewModel.Instance.OST_MP_Game_Over_Row_Vis[num5] = (Visibility)2;
				}
				else
				{
					MainViewModel.Instance.OST_MP_Game_Over_Row_Vis[num5] = (Visibility)0;
				}
			}
		}
		else if (wasTurnedOff)
		{
			MainViewModel.Instance.OST_MP_Game_Over_Vis = false;
		}
	}

	public OST getOST(Enums.eOnScreenText ostID, ref bool wasTurnedOff, ref bool wasTurnedOnOrChanged, bool handleStatChange = false)
	{
		wasTurnedOnOrChanged = (wasTurnedOff = false);
		if (isOSTActive(ostID))
		{
			wasTurnedOff = OSTs[(int)ostID].wasTurnedOff;
			wasTurnedOnOrChanged = OSTs[(int)ostID].wasTurnedOnOrChanged;
			if (handleStatChange)
			{
				OSTs[(int)ostID].wasTurnedOff = false;
				OSTs[(int)ostID].wasTurnedOnOrChanged = false;
			}
			return OSTs[(int)ostID];
		}
		if (ostID >= Enums.eOnScreenText.OST_CHAT && (int)ostID < OSTs.Length)
		{
			wasTurnedOff = OSTs[(int)ostID].wasTurnedOff;
			if (handleStatChange)
			{
				OSTs[(int)ostID].wasTurnedOff = false;
			}
		}
		return null;
	}

	public bool isOSTActive(Enums.eOnScreenText ostID)
	{
		if (ostID >= Enums.eOnScreenText.OST_CHAT && (int)ostID < OSTs.Length)
		{
			return OSTs[(int)ostID].active;
		}
		return false;
	}

	public void startCart()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		MainViewModel.Instance.CartDistance = MainViewModel.iUIScaleValueWidth;
		MainViewModel.Instance.CartDuration = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 8, 0));
		OST_StartingGoods.refCartStory.Stop();
		OST_StartingGoods.refCartStory.Begin();
		MainViewModel.Instance.OST_Cart_Vis = true;
	}

	public static string getComputerName(int computerOpponent, int computerName)
	{
		if (computerOpponent >= 30 && computerOpponent <= 37 && GameData.Instance.extendedLordMapping[computerOpponent - 30] > 0)
		{
			computerOpponent = GameData.Instance.extendedLordMapping[computerOpponent - 30];
		}
		switch (computerOpponent)
		{
		case 1:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 111 + computerName);
		case 2:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 119 + computerName);
		case 3:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 127 + computerName);
		case 4:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 135 + computerName);
		case 5:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 143 + computerName);
		case 6:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 151 + computerName);
		case 7:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 159 + computerName);
		case 8:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 167 + computerName);
		case 9:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 175 + computerName);
		case 10:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 183 + computerName);
		case 11:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 191 + computerName);
		case 12:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 199 + computerName);
		case 13:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 207 + computerName);
		case 14:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 215 + computerName);
		case 15:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 223 + computerName);
		case 16:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 231 + computerName);
		case 17:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 16 + computerName);
		case 18:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 24 + computerName);
		case 19:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 32 + computerName);
		case 20:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 40 + computerName);
		case 21:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 48 + computerName);
		case 22:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 56 + computerName);
		case 23:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 64 + computerName);
		case 24:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 72 + computerName);
		case 25:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 80 + computerName);
		case 26:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 454 + computerName);
		case 27:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 471 + computerName);
		case 28:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 488 + computerName);
		case 29:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 505 + computerName);
		case 30:
		case 31:
		case 32:
		case 33:
		case 34:
		case 35:
		case 36:
		case 37:
			if (MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo != null)
			{
				return MapFileManager.SplitCustomTrailName(MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.aivs[computerOpponent - 30].lordName) + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 23 + computerName);
			}
			if (MainViewModel.Instance.HUDIngameMenu.restartMPInfo != null)
			{
				return MapFileManager.SplitCustomTrailName(MainViewModel.Instance.HUDIngameMenu.restartMPInfo.LordNames[computerOpponent - 30]) + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 23 + computerName);
			}
			break;
		}
		return "";
	}

	public void UpdateSandsOfTimer()
	{
		int seconds = 0;
		MainViewModel.Instance.OST_sands_time_target = GameData.Instance.GetSandsOfTimeTargetTime(GameData.Instance.SkirmishTrailType, GameData.Instance.SkirmishTrailLevel, ref seconds);
		int num = 0;
		if (GameData.Instance.lastGameState != null)
		{
			num = GameData.Instance.lastGameState.game_time;
		}
		int rank = 0;
		int num2 = 0;
		MainViewModel.Instance.OST_sands_time_image = GameData.Instance.GetSandsOfTimeRankImage(num, seconds, ref rank);
		num /= 40;
		switch ((Enums.SandsRanks)rank)
		{
		case Enums.SandsRanks.Prince:
		{
			int sandsOfTimeRankTime7 = GameData.GetSandsOfTimeRankTime(seconds, (Enums.SandsRanks)rank);
			num2 = num * 494 / sandsOfTimeRankTime7;
			if (rank != lastSoTRank)
			{
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince, greyedImage: false, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant, greyedImage: true, large: false, small: true);
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_1).Opacity = 1f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_2).Opacity = 0.5f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_3).Opacity = 0.5f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_4).Opacity = 0.5f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_5).Opacity = 0.5f;
				MainViewModel.Instance.OST_same_time_prince = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Prince));
				MainViewModel.Instance.OST_same_time_champion = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Champion));
				MainViewModel.Instance.OST_same_time_warrior = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Warrior));
				MainViewModel.Instance.OST_same_time_tribesman = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Tribesman));
			}
			break;
		}
		case Enums.SandsRanks.Champion:
		{
			int sandsOfTimeRankTime5 = GameData.GetSandsOfTimeRankTime(seconds, (Enums.SandsRanks)rank);
			int sandsOfTimeRankTime6 = GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Prince);
			num2 = (num - sandsOfTimeRankTime6) * 494 / (sandsOfTimeRankTime5 - sandsOfTimeRankTime6);
			if (rank != lastSoTRank)
			{
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion, greyedImage: false, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant, greyedImage: true, large: false, small: true);
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_1).Opacity = 0.75f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_2).Opacity = 1f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_3).Opacity = 0.5f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_4).Opacity = 0.5f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_5).Opacity = 0.5f;
				MainViewModel.Instance.OST_same_time_prince = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Prince));
				MainViewModel.Instance.OST_same_time_champion = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Champion));
				MainViewModel.Instance.OST_same_time_warrior = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Warrior));
				MainViewModel.Instance.OST_same_time_tribesman = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Tribesman));
			}
			break;
		}
		case Enums.SandsRanks.Warrior:
		{
			int sandsOfTimeRankTime3 = GameData.GetSandsOfTimeRankTime(seconds, (Enums.SandsRanks)rank);
			int sandsOfTimeRankTime4 = GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Champion);
			num2 = (num - sandsOfTimeRankTime4) * 494 / (sandsOfTimeRankTime3 - sandsOfTimeRankTime4);
			if (rank != lastSoTRank)
			{
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior, greyedImage: false, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant, greyedImage: true, large: false, small: true);
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_1).Opacity = 0.75f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_2).Opacity = 0.75f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_3).Opacity = 1f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_4).Opacity = 0.5f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_5).Opacity = 0.5f;
				MainViewModel.Instance.OST_same_time_prince = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Prince));
				MainViewModel.Instance.OST_same_time_champion = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Champion));
				MainViewModel.Instance.OST_same_time_warrior = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Warrior));
				MainViewModel.Instance.OST_same_time_tribesman = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Tribesman));
			}
			break;
		}
		case Enums.SandsRanks.Tribesman:
		{
			int sandsOfTimeRankTime8 = GameData.GetSandsOfTimeRankTime(seconds, (Enums.SandsRanks)rank);
			int sandsOfTimeRankTime9 = GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Warrior);
			num2 = (num - sandsOfTimeRankTime9) * 494 / (sandsOfTimeRankTime8 - sandsOfTimeRankTime9);
			if (rank != lastSoTRank)
			{
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman, greyedImage: false, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant, greyedImage: true, large: false, small: true);
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_1).Opacity = 0.75f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_2).Opacity = 0.75f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_3).Opacity = 0.75f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_4).Opacity = 1f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_5).Opacity = 0.5f;
				MainViewModel.Instance.OST_same_time_prince = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Prince));
				MainViewModel.Instance.OST_same_time_champion = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Champion));
				MainViewModel.Instance.OST_same_time_warrior = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Warrior));
				MainViewModel.Instance.OST_same_time_tribesman = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Tribesman));
			}
			break;
		}
		case Enums.SandsRanks.Peasant:
		{
			int sandsOfTimeRankTime = GameData.GetSandsOfTimeRankTime(seconds, (Enums.SandsRanks)rank);
			int sandsOfTimeRankTime2 = GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Tribesman);
			num2 = (num - sandsOfTimeRankTime2) * 494 / (sandsOfTimeRankTime - sandsOfTimeRankTime2);
			if (num2 > 494)
			{
				num2 = 494;
			}
			if (rank != lastSoTRank)
			{
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman, greyedImage: true, large: false, small: true);
				MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant, greyedImage: false, large: false, small: true);
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_1).Opacity = 0.75f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_2).Opacity = 0.75f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_3).Opacity = 0.75f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_4).Opacity = 0.75f;
				((UIElement)MainViewModel.Instance.IngameUI.RefOST_Sands_Target_Bar_5).Opacity = 1f;
				MainViewModel.Instance.OST_same_time_prince = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Prince));
				MainViewModel.Instance.OST_same_time_champion = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Champion));
				MainViewModel.Instance.OST_same_time_warrior = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Warrior));
				MainViewModel.Instance.OST_same_time_tribesman = GameData.GetTimeString(GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Tribesman));
			}
			break;
		}
		}
		lastSoTRank = rank;
		MainViewModel.Instance.OST_sands_progress_bar = 494 - num2;
		MainViewModel.Instance.OST_sands_time_elapsed = GameData.GetTimeString(num);
		int sandsOfTimeRankTime10 = GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Peasant);
		int num3 = num;
		if (num3 > sandsOfTimeRankTime10)
		{
			num3 = sandsOfTimeRankTime10;
		}
		int num4 = 880;
		num3 = num3 * num4 / sandsOfTimeRankTime10;
		if (num3 > 494)
		{
			num3 = 494;
		}
		MainViewModel.Instance.OST_sands_overall_progress_bar = 494 - num3;
		MainViewModel.Instance.OST_sands_time_elapsed_colour = MainViewModel.OST_sands_time_elapsed_colour_Green;
	}
}
