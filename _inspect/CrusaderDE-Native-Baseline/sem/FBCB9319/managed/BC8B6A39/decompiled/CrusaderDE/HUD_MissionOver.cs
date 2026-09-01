using System;
using System.Collections.ObjectModel;
using Noesis;
using NoesisApp;
using UnityEngine;

namespace CrusaderDE;

public class HUD_MissionOver : UserControl
{
	private static HUD_MissionOver instance1 = null;

	private static HUD_MissionOver instance2 = null;

	public MediaElement refMissionOverVideo;

	public MediaElement refMissionOverVideo1;

	public MediaElement refMissionOverVideo2;

	public RadioButton refRankButton1;

	public RadioButton refRankButton2;

	private bool initialised;

	private bool Victory;

	private bool SkirmishMasters;

	private bool SandsTrailOutro;

	private bool ShownWeaselTurd;

	private bool loopVideo;

	private Storyboard refSandsOutro;

	private Image RefMO_Sands_Mission_Bar_1;

	private Image RefMO_Sands_Mission_Bar_2;

	private Image RefMO_Sands_Mission_Bar_3;

	private Image RefMO_Sands_Mission_Bar_4;

	private Image RefMO_Sands_Mission_Bar_5;

	private Noesis.Grid RefMO_SandsCurProgress;

	private Image RefMO_Sands_Trail_Bar_1;

	private Image RefMO_Sands_Trail_Bar_2;

	private Image RefMO_Sands_Trail_Bar_3;

	private Image RefMO_Sands_Trail_Bar_4;

	private Image RefMO_Sands_Trail_Bar_5;

	private Noesis.Grid RefMO_SandsTrailProgress;

	private TextBlock RefWeaselTurdText1;

	private TextBlock RefWeaselTurdText2;

	private TextBlock RefWeaselTurdText3;

	private TextBlock RefWeaselTurdText4;

	private TextBlock RefWeaselTurdText5;

	private Noesis.Grid RefCoopCompletion;

	private Storyboard refWeaselTurd1;

	private Storyboard refWeaselTurd2;

	private string[] victory_videos = new string[3] { "crusader_win.webm", "arabian_win.webm", "bedouin_win.webm" };

	private string[] defeat_videos = new string[3] { "crusader_lose.webm", "arabian_lose.webm", "bedouin_lose.webm" };

	private int numActivePlayers;

	private int this_player;

	private int id;

	private int temp;

	private int total;

	private int[] ranking = new int[9];

	private int width;

	private int pie;

	private int star_x;

	private int[][] individual_ranking;

	private int[] points = new int[9];

	private int[] enemy_killed = new int[9];

	private int[] yours_killed = new int[9];

	private int[] stars = new int[9];

	private int num_players;

	private const int MAX_HUMANS = 9;

	private int mp_sortType;

	private bool sortReversed;

	private EngineInterface.MPScoreData last_stats;

	private const int RANK_ENEMY = 0;

	private const int RANK_BUILDINGS = 1;

	private const int RANK_FOOD = 2;

	private const int RANK_GOLD = 3;

	private const int RANK_POP = 4;

	private const int RANK_WOOD = 5;

	private const int RANK_STONE = 6;

	private const int RANK_IRON = 7;

	private const int RANK_PITCH = 8;

	private const int RANK_KOTH = 9;

	private const int RANK_YOUR = 10;

	private const int RANK_WEAPONS = 11;

	private const int RANK_BUILDLOST = 12;

	private const int RANK_TROOPSMADE = 13;

	private const int RANK_GOODSRECEIVED = 14;

	private const int RANK_GOODSSENT = 15;

	public static string FutureVoiceLine = "";

	private static DateTime FutureVoiceLineStart = DateTime.MinValue;

	private int[,] mp_help_text = new int[20, 2]
	{
		{ 225, 1 },
		{ 12, 18 },
		{ 12, 25 },
		{ 12, 19 },
		{ 12, 20 },
		{ 12, 26 },
		{ 12, 27 },
		{ 12, 28 },
		{ 12, 29 },
		{ 12, 30 },
		{ 12, 21 },
		{ 250, 19 },
		{ 250, 20 },
		{ 252, 0 },
		{ 252, 1 },
		{ 252, 2 },
		{ 199, 25 },
		{ 230, 59 },
		{ 12, 22 },
		{ 75, 2 }
	};

	public HUD_MissionOver()
	{
		InitializeComponent();
		if (instance1 == null)
		{
			instance1 = this;
		}
		else if (instance2 == null)
		{
			instance2 = this;
		}
		refRankButton1 = (RadioButton)FindName("RankButtonPage1");
		refRankButton2 = (RadioButton)FindName("RankButtonPage2");
		refSandsOutro = (Storyboard)TryFindResource("SandsOutro");
		RefMO_Sands_Mission_Bar_1 = (Image)FindName("MO_Sands_Mission_Bar_1");
		RefMO_Sands_Mission_Bar_2 = (Image)FindName("MO_Sands_Mission_Bar_2");
		RefMO_Sands_Mission_Bar_3 = (Image)FindName("MO_Sands_Mission_Bar_3");
		RefMO_Sands_Mission_Bar_4 = (Image)FindName("MO_Sands_Mission_Bar_4");
		RefMO_Sands_Mission_Bar_5 = (Image)FindName("MO_Sands_Mission_Bar_5");
		RefMO_SandsCurProgress = (Noesis.Grid)FindName("MO_SandsCurProgress");
		RefMO_Sands_Trail_Bar_1 = (Image)FindName("MO_Sands_Trail_Bar_1");
		RefMO_Sands_Trail_Bar_2 = (Image)FindName("MO_Sands_Trail_Bar_2");
		RefMO_Sands_Trail_Bar_3 = (Image)FindName("MO_Sands_Trail_Bar_3");
		RefMO_Sands_Trail_Bar_4 = (Image)FindName("MO_Sands_Trail_Bar_4");
		RefMO_Sands_Trail_Bar_5 = (Image)FindName("MO_Sands_Trail_Bar_5");
		RefMO_SandsTrailProgress = (Noesis.Grid)FindName("MO_SandsTrailProgress");
		RefWeaselTurdText1 = (TextBlock)FindName("WeaselTurdText1");
		RefWeaselTurdText2 = (TextBlock)FindName("WeaselTurdText2");
		RefWeaselTurdText3 = (TextBlock)FindName("WeaselTurdText3");
		RefWeaselTurdText4 = (TextBlock)FindName("WeaselTurdText4");
		RefWeaselTurdText5 = (TextBlock)FindName("WeaselTurdText5");
		refWeaselTurd1 = (Storyboard)TryFindResource("WeaselTurd1");
		refWeaselTurd2 = (Storyboard)TryFindResource("WeaselTurd2");
		RefCoopCompletion = (Noesis.Grid)FindName("CoopCompletion");
		MainViewModel.Instance.HUDMissionOver = this;
	}

	public void init()
	{
		if (!initialised || refMissionOverVideo1 == null || refMissionOverVideo2 == null)
		{
			refMissionOverVideo1 = MainViewModel.Instance.IngameUI.refMissionOverVideo;
			refMissionOverVideo1.MediaEnded += FrontendBackVideo_Ended;
			refMissionOverVideo2 = MainViewModel.Instance.FrontEndMenu.refMissionOverVideo;
			refMissionOverVideo2.MediaEnded += FrontendBackVideo_Ended;
			initialised = true;
		}
	}

	public static void ShowVictory(Enums.VictoryScreens screen, EngineInterface.ScoreData scoreData)
	{
		MainViewModel.Instance.Show_HUD_MissionOver = true;
		if (instance1.IsVisible)
		{
			MainViewModel.Instance.HUDMissionOver = instance1;
		}
		else if (instance2.IsVisible)
		{
			MainViewModel.Instance.HUDMissionOver = instance2;
		}
		MainViewModel.Instance.HUDMissionOver.LoadVictoryScreen(screen, scoreData);
	}

	public static void ShowDefeat(Enums.DefeatScreens screen)
	{
		MainViewModel.Instance.Show_HUD_MissionOver = true;
		if (instance1.IsVisible)
		{
			MainViewModel.Instance.HUDMissionOver = instance1;
		}
		else if (instance2.IsVisible)
		{
			MainViewModel.Instance.HUDMissionOver = instance2;
		}
		MainViewModel.Instance.HUDMissionOver.LoadDefeatScreen(screen);
	}

	public static void ShowMPVictory(Enums.VictoryScreens screen, EngineInterface.MPScoreData scoreData, bool sandsTrail)
	{
		MainViewModel.Instance.Show_HUD_MissionOver = true;
		if (instance1.IsVisible)
		{
			MainViewModel.Instance.HUDMissionOver = instance1;
		}
		else if (instance2.IsVisible)
		{
			MainViewModel.Instance.HUDMissionOver = instance2;
		}
		MainViewModel.Instance.HUDMissionOver.LoadMPVictoryScreen(screen, scoreData, skirmishMasters: false, sandsTrail);
	}

	public static void ShowSkirmishMasters(Enums.VictoryScreens screen, EngineInterface.MPScoreData scoreData)
	{
		MainViewModel.Instance.Show_HUD_MissionOver = true;
		if (instance1.IsVisible)
		{
			MainViewModel.Instance.HUDMissionOver = instance1;
		}
		else if (instance2.IsVisible)
		{
			MainViewModel.Instance.HUDMissionOver = instance2;
		}
		MainViewModel.Instance.HUDMissionOver.LoadMPVictoryScreen(screen, scoreData, skirmishMasters: true, sandsTrail: false);
	}

	public static void ShowMPDefeat(Enums.DefeatScreens screen, EngineInterface.MPScoreData scoreData)
	{
		MainViewModel.Instance.Show_HUD_MissionOver = true;
		if (instance1.IsVisible)
		{
			MainViewModel.Instance.HUDMissionOver = instance1;
		}
		else if (instance2.IsVisible)
		{
			MainViewModel.Instance.HUDMissionOver = instance2;
		}
		MainViewModel.Instance.HUDMissionOver.LoadMPDefeatScreen(screen, scoreData);
	}

	private void LoadVictoryScreen(Enums.VictoryScreens screen, EngineInterface.ScoreData lastScores)
	{
		SkirmishMasters = false;
		SandsTrailOutro = false;
		LoadVideoBackground("Victory/" + victory_videos[(int)screen]);
		MainViewModel.Instance.MO_SP_Score = true;
		MainViewModel.Instance.MO_MP_Score = Visibility.Hidden;
		MainViewModel.Instance.MO_SandsOutro1 = Visibility.Hidden;
		Victory = true;
		if (GameData.Instance.currentMapName.Length > 0)
		{
			ConfigSettings.ManageScores(GameData.Instance.currentMapName, lastScores.difficulty_level);
		}
	}

	private void LoadDefeatScreen(Enums.DefeatScreens screen)
	{
		SkirmishMasters = false;
		SandsTrailOutro = false;
		LoadVideoBackground("Defeat/" + defeat_videos[(int)screen]);
		MainViewModel.Instance.MO_SP_Score = true;
		MainViewModel.Instance.MO_MP_Score = Visibility.Hidden;
		MainViewModel.Instance.MO_SandsOutro1 = Visibility.Hidden;
		Victory = false;
	}

	private void LoadMPVictoryScreen(Enums.VictoryScreens screen, EngineInterface.MPScoreData scoreData, bool skirmishMasters, bool sandsTrail)
	{
		SkirmishMasters = skirmishMasters;
		SandsTrailOutro = sandsTrail;
		ShownWeaselTurd = false;
		ClearWeaselText();
		if (!sandsTrail)
		{
			if (skirmishMasters)
			{
				switch (scoreData.lord_type)
				{
				default:
					screen = Enums.VictoryScreens.banquet;
					break;
				case 1:
				case 6:
					screen = Enums.VictoryScreens.economic;
					break;
				case 2:
				case 7:
					screen = Enums.VictoryScreens.military_big;
					break;
				}
			}
			LoadVideoBackground("Victory/" + victory_videos[(int)screen], skirmishMasters);
		}
		else
		{
			MainViewModel.Instance.Show_HUD_MissionOver_Video = false;
		}
		ShowMPScore(scoreData, prepScores: true, skirmishMasters);
		MainViewModel.Instance.MO_SP_Score = false;
		if (sandsTrail && !ConfigSettings.Settings_HideSoTTiming)
		{
			InitSands();
			MainViewModel.Instance.Show_HUD_MissionOver_SandsBackground = true;
			MainViewModel.Instance.MO_SandsOutro1 = Visibility.Visible;
			MainViewModel.Instance.MO_MP_Score = Visibility.Hidden;
		}
		else
		{
			SandsTrailOutro = false;
			MainViewModel.Instance.MO_SandsOutro1 = Visibility.Hidden;
			MainViewModel.Instance.MO_MP_Score = Visibility.Visible;
		}
		MainViewModel.Instance.MO_MP_Victory = Visibility.Visible;
		MainViewModel.Instance.MO_MP_Defeat = Visibility.Collapsed;
		Victory = true;
	}

	private void LoadMPDefeatScreen(Enums.DefeatScreens screen, EngineInterface.MPScoreData scoreData)
	{
		SkirmishMasters = false;
		SandsTrailOutro = false;
		LoadVideoBackground("Defeat/" + defeat_videos[(int)screen]);
		ShowMPScore(scoreData);
		MainViewModel.Instance.MO_SP_Score = false;
		MainViewModel.Instance.MO_SandsOutro1 = Visibility.Hidden;
		MainViewModel.Instance.MO_MP_Score = Visibility.Visible;
		MainViewModel.Instance.MO_MP_Victory = Visibility.Collapsed;
		MainViewModel.Instance.MO_MP_Defeat = Visibility.Visible;
		Victory = false;
	}

	private void ShowMPScore(EngineInterface.MPScoreData lastMPScores, bool prepScores = true, bool fromSkirmishMasters = false)
	{
		if (prepScores)
		{
			last_stats = lastMPScores;
			prepMPScores(lastMPScores);
			MainViewModel.Instance.MO_ShowPage1 = Visibility.Visible;
			MainViewModel.Instance.MO_ShowPage2 = Visibility.Collapsed;
			mp_sortType = 0;
			sortReversed = false;
			MainViewModel.Instance.HUDMissionOver.refRankButton1.IsChecked = true;
			UpdateHelpText(mp_sortType);
			if (Director.instance.WasSkirmishModeGame && !fromSkirmishMasters)
			{
				lastMPScores.score = calc_greatest_lord_points(lastMPScores, EditorDirector.instance.ActivePlayerID);
				lastMPScores.numPlayers = numActivePlayers;
				if (GameData.Instance.coopTrailID > 0)
				{
					if (GameData.Instance.coopTrailID == 1)
					{
						lastMPScores.trailLevel = 20000 + GameData.Instance.coopMissionID;
					}
					else if (GameData.Instance.coopTrailID == 2)
					{
						lastMPScores.trailLevel = 21000 + GameData.Instance.coopMissionID;
					}
					else if (GameData.Instance.coopTrailID == 3)
					{
						lastMPScores.trailLevel = 22000 + GameData.Instance.coopMissionID;
					}
					else if (GameData.Instance.coopTrailID == 4)
					{
						lastMPScores.trailLevel = 23000 + GameData.Instance.coopMissionID;
					}
				}
				else if (GameData.Instance.SkirmishGameType == 1)
				{
					if (GameData.Instance.SkirmishTrailType == 2)
					{
						lastMPScores.trailLevel = GameData.Instance.SkirmishTrailLevel + 80;
					}
					else if (GameData.Instance.SkirmishTrailType == 1)
					{
						lastMPScores.trailLevel = GameData.Instance.SkirmishTrailLevel + 50;
					}
					else if (GameData.Instance.SkirmishTrailType == 0)
					{
						lastMPScores.trailLevel = GameData.Instance.SkirmishTrailLevel;
					}
					else
					{
						lastMPScores.trailLevel = GameData.Instance.SkirmishTrailLevel + GameData.Instance.SkirmishTrailType * 1000;
					}
				}
				else if (GameData.Instance.SkirmishGameType == 2 && MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo != null)
				{
					lastMPScores.trailLevel = MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrailLevel;
					string trailName = MapFileManager.SplitCustomTrailName(MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrailName);
					lastMPScores.trailName = trailName;
				}
				else
				{
					lastMPScores.trailLevel = -1;
					lastMPScores.mapName = GameData.Instance.cachedMissionName;
				}
			}
			if (!fromSkirmishMasters && lastMPScores.unique != 0L && Director.instance.WasSkirmishModeGame)
			{
				ConfigSettings.AddSkirmishMastersGame(lastMPScores);
			}
			if (fromSkirmishMasters)
			{
				if (lastMPScores.colourMap1[0] == -1)
				{
					SpriteMapping.mpLoadRemapping = null;
				}
				else
				{
					SpriteMapping.mpLoadRemapping = new int[9];
					for (int i = 0; i < 9; i++)
					{
						SpriteMapping.mpLoadRemapping[i] = lastMPScores.colourMap1[i];
					}
				}
				for (int j = 0; j < 9; j++)
				{
					SpriteMapping.remapColours[j] = lastMPScores.colourMap2[j];
				}
				GameData.Instance.SkirmishGameType = 1;
				GameData.Instance.coopTrailID = 0;
				int num = 0;
				int skirmishTrailLevel = 0;
				if (lastMPScores.trailLevel < 0)
				{
					num = -1;
					GameData.Instance.SkirmishGameType = 0;
				}
				else if (lastMPScores.trailLevel < 50)
				{
					num = 0;
					skirmishTrailLevel = lastMPScores.trailLevel;
				}
				else if (lastMPScores.trailLevel < 80)
				{
					num = 1;
					skirmishTrailLevel = lastMPScores.trailLevel - 50;
				}
				else if (lastMPScores.trailLevel < 100)
				{
					num = 2;
					skirmishTrailLevel = lastMPScores.trailLevel - 80;
				}
				else if (lastMPScores.trailLevel >= 20000)
				{
					num = ((lastMPScores.trailLevel >= 23000) ? 103 : ((lastMPScores.trailLevel >= 22000) ? 102 : ((lastMPScores.trailLevel < 21000) ? 100 : 101)));
					skirmishTrailLevel = lastMPScores.trailLevel % 1000;
					GameData.Instance.coopTrailID = num;
				}
				else
				{
					num = lastMPScores.trailLevel / 1000;
					skirmishTrailLevel = lastMPScores.trailLevel % 1000;
				}
				GameData.Instance.SkirmishTrailType = num;
				GameData.Instance.SkirmishTrailLevel = skirmishTrailLevel;
				Director.instance.WasSkirmishModeGame = true;
				GameData.Instance.game_type = 3;
			}
		}
		for (int k = 0; k < numActivePlayers; k++)
		{
			MainViewModel.Instance.MO_MP_PlayersVisible[k] = Visibility.Visible;
		}
		for (int l = numActivePlayers; l < 8; l++)
		{
			MainViewModel.Instance.MO_MP_PlayersVisible[l] = Visibility.Collapsed;
		}
		int num2 = 0;
		for (int m = 1; m < 9; m++)
		{
			int num3 = m;
			if (sortReversed)
			{
				num3 = 9 - m;
			}
			int num4 = ranking[num3];
			switch (mp_sortType)
			{
			case 0:
				num4 = ranking[num3];
				break;
			case 1:
				num4 = individual_ranking[0][num3];
				break;
			case 2:
				num4 = individual_ranking[1][num3];
				break;
			case 3:
				num4 = individual_ranking[10][num3];
				break;
			case 4:
				num4 = individual_ranking[3][num3];
				break;
			case 5:
				num4 = individual_ranking[2][num3];
				break;
			case 6:
				num4 = individual_ranking[5][num3];
				break;
			case 7:
				num4 = individual_ranking[6][num3];
				break;
			case 8:
				num4 = individual_ranking[7][num3];
				break;
			case 9:
				num4 = individual_ranking[8][num3];
				break;
			case 10:
				num4 = individual_ranking[4][num3];
				break;
			case 11:
				num4 = individual_ranking[11][num3];
				break;
			case 12:
				num4 = individual_ranking[12][num3];
				break;
			case 13:
				num4 = individual_ranking[13][num3];
				break;
			case 14:
				num4 = individual_ranking[14][num3];
				break;
			case 15:
				num4 = individual_ranking[15][num3];
				break;
			}
			if (lastMPScores.valid[num4] <= 0)
			{
				continue;
			}
			ObservableCollection<string> playerValuesRow = GetPlayerValuesRow(num2);
			playerValuesRow[0] = lastMPScores.playerName[num4];
			playerValuesRow[1] = enemy_killed[num4].ToString();
			playerValuesRow[2] = yours_killed[num4].ToString();
			playerValuesRow[3] = lastMPScores.gold_acquired[num4].ToString();
			playerValuesRow[4] = lastMPScores.food_produced[num4].ToString();
			playerValuesRow[5] = lastMPScores.wood_produced[num4].ToString();
			playerValuesRow[6] = lastMPScores.stone_produced[num4].ToString();
			playerValuesRow[7] = lastMPScores.iron_produced[num4].ToString();
			playerValuesRow[8] = lastMPScores.pitch_produced[num4].ToString();
			playerValuesRow[9] = lastMPScores.weapons_produced[num4].ToString();
			playerValuesRow[10] = lastMPScores.max_population[num4].ToString();
			playerValuesRow[11] = lastMPScores.enemy_buildings_destroyed[num4].ToString();
			playerValuesRow[12] = lastMPScores.buildings_lost[num4].ToString();
			playerValuesRow[13] = num2.ToString();
			playerValuesRow[14] = lastMPScores.troops_produced[num4].ToString();
			playerValuesRow[15] = lastMPScores.goods_received[num4].ToString();
			playerValuesRow[16] = lastMPScores.goods_sent[num4].ToString();
			ObservableCollection<Visibility> playerIconsRow = GetPlayerIconsRow(num2);
			for (int n = 0; n < 14; n++)
			{
				playerIconsRow[n] = Visibility.Collapsed;
			}
			int num5 = lastMPScores.lords_killed[num4];
			int num6 = 0;
			if (lastMPScores.winners[num4] > 0)
			{
				num6++;
			}
			num6 += stars[num4];
			for (int num7 = 0; num7 < num6 && num7 < 7; num7++)
			{
				playerIconsRow[num7] = Visibility.Visible;
			}
			for (int num8 = 0; num8 < num5 && num8 < 7; num8++)
			{
				playerIconsRow[num8 + 7] = Visibility.Visible;
			}
			SetPlayerShieldsImageSource(num2, FRONT_Multiplayer.GetColourShield(num4, 0, ingameRemap: true));
			int fearPie = lastMPScores.fearfactor[num4] * 6 + -lastMPScores.minfearfactor[num4];
			SetPlayerFearImageSource(num2, GetPlayerFear(fearPie));
			if (lastMPScores.time_lord_killed[num4] > 0)
			{
				playerIconsRow[14] = Visibility.Visible;
				if (FatControler.arabic && ConfigSettings.Settings_ArabicL2R)
				{
					playerValuesRow[17] = lastMPScores.time_lord_killed[num4] / 12 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, lastMPScores.time_lord_killed[num4] % 12);
				}
				else
				{
					playerValuesRow[17] = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, lastMPScores.time_lord_killed[num4] % 12) + " " + lastMPScores.time_lord_killed[num4] / 12;
				}
			}
			else
			{
				playerIconsRow[14] = Visibility.Collapsed;
			}
			num2++;
		}
	}

	private void InitSands()
	{
		int seconds = 0;
		MainViewModel.Instance.OST_sands_time_target = GameData.Instance.GetSandsOfTimeTargetTime(GameData.Instance.SkirmishTrailType, GameData.Instance.SkirmishTrailLevel, ref seconds);
		int skirmishMissionDuration = GameData.scenario.skirmishMissionDuration;
		int rank = 0;
		GameData.Instance.GetSandsOfTimeRankImage(skirmishMissionDuration, seconds, ref rank);
		skirmishMissionDuration /= 40;
		MainViewModel.Instance.MO_Sands_CurMissionTime = GameData.GetTimeString(skirmishMissionDuration);
		if (GameData.Instance.SkirmishTrailType <= 16)
		{
			MainViewModel.Instance.MO_Sands_Trail_Name = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 1 + (GameData.Instance.SkirmishTrailType - 11));
		}
		if (GameData.Instance.SkirmishTrailType == 17)
		{
			MainViewModel.Instance.MO_Sands_Trail_Name = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 102);
		}
		if (GameData.Instance.SkirmishTrailType == 18)
		{
			MainViewModel.Instance.MO_Sands_Trail_Name = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 103);
		}
		int num = 0;
		int num2 = 0;
		bool flag = true;
		switch (GameData.Instance.SkirmishTrailType)
		{
		case 11:
		{
			MainViewModel.Instance.MO_Sands_Mission_Name = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 7 + GameData.Instance.SkirmishTrailLevel);
			for (int m = 0; m < 5; m++)
			{
				int seconds6 = 0;
				GameData.Instance.GetSandsOfTimeTargetTime(GameData.Instance.SkirmishTrailType, m, ref seconds6);
				int sandsOfTimeRankTime5 = GameData.GetSandsOfTimeRankTime(seconds6, Enums.SandsRanks.Peasant);
				int displayTime;
				int num7 = ((ConfigSettings.Settings_Trail_Sands1_Times[m] >= int.MaxValue) ? (displayTime = 0) : (displayTime = ConfigSettings.Settings_Trail_Sands1_Times[m] / 40));
				if (num7 <= 0)
				{
					num7 = sandsOfTimeRankTime5;
					flag = false;
				}
				else
				{
					num += seconds6;
					num2 += num7;
					if (num7 > sandsOfTimeRankTime5)
					{
						num7 = sandsOfTimeRankTime5;
					}
				}
				setProgressBar(m, 400 - num7 * 400 / sandsOfTimeRankTime5, displayTime);
			}
			MainViewModel.Instance.MO_Sands_Show_67 = false;
			MainViewModel.Instance.MO_Sands_Show_89 = false;
			MainViewModel.Instance.MO_Sands_Show_1011 = false;
			break;
		}
		case 12:
		{
			MainViewModel.Instance.MO_Sands_Mission_Name = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 12 + GameData.Instance.SkirmishTrailLevel);
			for (int j = 0; j < 7; j++)
			{
				int seconds3 = 0;
				GameData.Instance.GetSandsOfTimeTargetTime(GameData.Instance.SkirmishTrailType, j, ref seconds3);
				int sandsOfTimeRankTime2 = GameData.GetSandsOfTimeRankTime(seconds3, Enums.SandsRanks.Peasant);
				int displayTime;
				int num4 = (displayTime = ConfigSettings.Settings_Trail_Sands2_Times[j] / 40);
				if (num4 <= 0)
				{
					num4 = sandsOfTimeRankTime2;
					flag = false;
				}
				else
				{
					num += seconds3;
					num2 += num4;
					if (num4 > sandsOfTimeRankTime2)
					{
						num4 = sandsOfTimeRankTime2;
					}
				}
				setProgressBar(j, 400 - num4 * 400 / sandsOfTimeRankTime2, displayTime);
			}
			MainViewModel.Instance.MO_Sands_Show_67 = true;
			MainViewModel.Instance.MO_Sands_Show_89 = false;
			MainViewModel.Instance.MO_Sands_Show_1011 = false;
			break;
		}
		case 13:
		{
			MainViewModel.Instance.MO_Sands_Mission_Name = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 19 + GameData.Instance.SkirmishTrailLevel);
			for (int n = 0; n < 9; n++)
			{
				int seconds7 = 0;
				GameData.Instance.GetSandsOfTimeTargetTime(GameData.Instance.SkirmishTrailType, n, ref seconds7);
				int sandsOfTimeRankTime6 = GameData.GetSandsOfTimeRankTime(seconds7, Enums.SandsRanks.Peasant);
				int displayTime;
				int num8 = (displayTime = ConfigSettings.Settings_Trail_Sands3_Times[n] / 40);
				if (num8 <= 0)
				{
					num8 = sandsOfTimeRankTime6;
					flag = false;
				}
				else
				{
					num += seconds7;
					num2 += num8;
					if (num8 > sandsOfTimeRankTime6)
					{
						num8 = sandsOfTimeRankTime6;
					}
				}
				setProgressBar(n, 400 - num8 * 400 / sandsOfTimeRankTime6, displayTime);
			}
			MainViewModel.Instance.MO_Sands_Show_67 = true;
			MainViewModel.Instance.MO_Sands_Show_89 = true;
			MainViewModel.Instance.MO_Sands_Show_1011 = false;
			break;
		}
		case 14:
		{
			MainViewModel.Instance.MO_Sands_Mission_Name = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 28 + GameData.Instance.SkirmishTrailLevel);
			for (int num11 = 0; num11 < 11; num11++)
			{
				int seconds9 = 0;
				GameData.Instance.GetSandsOfTimeTargetTime(GameData.Instance.SkirmishTrailType, num11, ref seconds9);
				int sandsOfTimeRankTime8 = GameData.GetSandsOfTimeRankTime(seconds9, Enums.SandsRanks.Peasant);
				int displayTime;
				int num12 = (displayTime = ConfigSettings.Settings_Trail_Sands4_Times[num11] / 40);
				if (num12 <= 0)
				{
					num12 = sandsOfTimeRankTime8;
					flag = false;
				}
				else
				{
					num += seconds9;
					num2 += num12;
					if (num12 > sandsOfTimeRankTime8)
					{
						num12 = sandsOfTimeRankTime8;
					}
				}
				setProgressBar(num11, 400 - num12 * 400 / sandsOfTimeRankTime8, displayTime);
			}
			MainViewModel.Instance.MO_Sands_Show_67 = true;
			MainViewModel.Instance.MO_Sands_Show_89 = true;
			MainViewModel.Instance.MO_Sands_Show_1011 = true;
			break;
		}
		case 15:
		{
			MainViewModel.Instance.MO_Sands_Mission_Name = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 39 + GameData.Instance.SkirmishTrailLevel);
			for (int k = 0; k < 9; k++)
			{
				int seconds4 = 0;
				GameData.Instance.GetSandsOfTimeTargetTime(GameData.Instance.SkirmishTrailType, k, ref seconds4);
				int sandsOfTimeRankTime3 = GameData.GetSandsOfTimeRankTime(seconds4, Enums.SandsRanks.Peasant);
				int displayTime;
				int num5 = (displayTime = ConfigSettings.Settings_Trail_Sands5_Times[k] / 40);
				if (num5 <= 0)
				{
					num5 = sandsOfTimeRankTime3;
					flag = false;
				}
				else
				{
					num += seconds4;
					num2 += num5;
					if (num5 > sandsOfTimeRankTime3)
					{
						num5 = sandsOfTimeRankTime3;
					}
				}
				setProgressBar(k, 400 - num5 * 400 / sandsOfTimeRankTime3, displayTime);
			}
			MainViewModel.Instance.MO_Sands_Show_67 = true;
			MainViewModel.Instance.MO_Sands_Show_89 = true;
			MainViewModel.Instance.MO_Sands_Show_1011 = false;
			break;
		}
		case 16:
		{
			MainViewModel.Instance.MO_Sands_Mission_Name = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 49 + GameData.Instance.SkirmishTrailLevel);
			for (int num9 = 0; num9 < 9; num9++)
			{
				int seconds8 = 0;
				GameData.Instance.GetSandsOfTimeTargetTime(GameData.Instance.SkirmishTrailType, num9, ref seconds8);
				int sandsOfTimeRankTime7 = GameData.GetSandsOfTimeRankTime(seconds8, Enums.SandsRanks.Peasant);
				int displayTime;
				int num10 = (displayTime = ConfigSettings.Settings_Trail_Sands6_Times[num9] / 40);
				if (num10 <= 0)
				{
					num10 = sandsOfTimeRankTime7;
					flag = false;
				}
				else
				{
					num += seconds8;
					num2 += num10;
					if (num10 > sandsOfTimeRankTime7)
					{
						num10 = sandsOfTimeRankTime7;
					}
				}
				setProgressBar(num9, 400 - num10 * 400 / sandsOfTimeRankTime7, displayTime);
			}
			MainViewModel.Instance.MO_Sands_Show_67 = true;
			MainViewModel.Instance.MO_Sands_Show_89 = true;
			MainViewModel.Instance.MO_Sands_Show_1011 = false;
			break;
		}
		case 17:
		{
			MainViewModel.Instance.MO_Sands_Mission_Name = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 82 + GameData.Instance.SkirmishTrailLevel);
			for (int l = 0; l < 9; l++)
			{
				int seconds5 = 0;
				GameData.Instance.GetSandsOfTimeTargetTime(GameData.Instance.SkirmishTrailType, l, ref seconds5);
				int sandsOfTimeRankTime4 = GameData.GetSandsOfTimeRankTime(seconds5, Enums.SandsRanks.Peasant);
				int displayTime;
				int num6 = (displayTime = ConfigSettings.Settings_Trail_Sands7_Times[l] / 40);
				if (num6 <= 0)
				{
					num6 = sandsOfTimeRankTime4;
					flag = false;
				}
				else
				{
					num += seconds5;
					num2 += num6;
					if (num6 > sandsOfTimeRankTime4)
					{
						num6 = sandsOfTimeRankTime4;
					}
				}
				setProgressBar(l, 400 - num6 * 400 / sandsOfTimeRankTime4, displayTime);
			}
			MainViewModel.Instance.MO_Sands_Show_67 = true;
			MainViewModel.Instance.MO_Sands_Show_89 = true;
			MainViewModel.Instance.MO_Sands_Show_1011 = false;
			break;
		}
		case 18:
		{
			MainViewModel.Instance.MO_Sands_Mission_Name = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 92 + GameData.Instance.SkirmishTrailLevel);
			for (int i = 0; i < 9; i++)
			{
				int seconds2 = 0;
				GameData.Instance.GetSandsOfTimeTargetTime(GameData.Instance.SkirmishTrailType, i, ref seconds2);
				int sandsOfTimeRankTime = GameData.GetSandsOfTimeRankTime(seconds2, Enums.SandsRanks.Peasant);
				int displayTime;
				int num3 = (displayTime = ConfigSettings.Settings_Trail_Sands8_Times[i] / 40);
				if (num3 <= 0)
				{
					num3 = sandsOfTimeRankTime;
					flag = false;
				}
				else
				{
					num += seconds2;
					num2 += num3;
					if (num3 > sandsOfTimeRankTime)
					{
						num3 = sandsOfTimeRankTime;
					}
				}
				setProgressBar(i, 400 - num3 * 400 / sandsOfTimeRankTime, displayTime);
			}
			MainViewModel.Instance.MO_Sands_Show_67 = true;
			MainViewModel.Instance.MO_Sands_Show_89 = true;
			MainViewModel.Instance.MO_Sands_Show_1011 = false;
			break;
		}
		}
		switch (rank)
		{
		case 4:
			MainViewModel.Instance.MO_Sands_CurMissionRank = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 67);
			break;
		case 3:
			MainViewModel.Instance.MO_Sands_CurMissionRank = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 66);
			break;
		case 2:
			MainViewModel.Instance.MO_Sands_CurMissionRank = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 65);
			break;
		case 1:
			MainViewModel.Instance.MO_Sands_CurMissionRank = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 64);
			break;
		case 0:
			MainViewModel.Instance.MO_Sands_CurMissionRank = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 63);
			break;
		}
		MainViewModel.Instance.MO_Sands_Mission_Rank_Image = GameData.GetSandsOfTimeImage((Enums.SandsRanks)rank, greyedImage: false, large: true);
		int num13 = GameData.GetSandsOfTimeRankTime(seconds, Enums.SandsRanks.Peasant);
		if (skirmishMissionDuration > num13)
		{
			skirmishMissionDuration = num13;
		}
		if (num13 <= 0)
		{
			num13 = 1;
		}
		MainViewModel.Instance.MO_Sands_CurProgressBar = 1500 - skirmishMissionDuration * 1500 / num13;
		MainViewModel.Instance.MO_Sands_Trail_TotalTime = GameData.GetTimeString(num2);
		num *= 4;
		if (num2 > num)
		{
			num2 = num;
		}
		if (num <= 0)
		{
			num = 1;
		}
		MainViewModel.Instance.MO_Sands_TrailProgressBar = 1700 - num2 * 1700 / num;
		if (flag)
		{
			MainViewModel.Instance.MO_Sands_Trail_Rank_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 75);
			MainViewModel.Instance.MO_Sands_Trail_Time_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 78);
		}
		else
		{
			MainViewModel.Instance.MO_Sands_Trail_Rank_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 74);
			MainViewModel.Instance.MO_Sands_Trail_Time_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 79);
		}
		int rank2 = 0;
		GameData.Instance.GetSandsOfTimeRankImage(num2 * 40, num, ref rank2);
		switch (rank2)
		{
		case 4:
			MainViewModel.Instance.MO_Sands_Trail_Rank = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 67);
			break;
		case 3:
			MainViewModel.Instance.MO_Sands_Trail_Rank = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 66);
			break;
		case 2:
			MainViewModel.Instance.MO_Sands_Trail_Rank = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 65);
			break;
		case 1:
			MainViewModel.Instance.MO_Sands_Trail_Rank = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 64);
			break;
		case 0:
			MainViewModel.Instance.MO_Sands_Trail_Rank = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 63);
			break;
		}
		refSandsOutro.Begin();
	}

	private void setProgressBar(int bar, int progress, int displayTime)
	{
		string text = ((displayTime > 0) ? GameData.GetTimeString(displayTime) : "--:--");
		switch (bar)
		{
		case 0:
			MainViewModel.Instance.MO_Sands_Mission_1_Progress = progress;
			MainViewModel.Instance.MO_Sands_Mission_1_Time = text;
			break;
		case 1:
			MainViewModel.Instance.MO_Sands_Mission_2_Progress = progress;
			MainViewModel.Instance.MO_Sands_Mission_2_Time = text;
			break;
		case 2:
			MainViewModel.Instance.MO_Sands_Mission_3_Progress = progress;
			MainViewModel.Instance.MO_Sands_Mission_3_Time = text;
			break;
		case 3:
			MainViewModel.Instance.MO_Sands_Mission_4_Progress = progress;
			MainViewModel.Instance.MO_Sands_Mission_4_Time = text;
			break;
		case 4:
			MainViewModel.Instance.MO_Sands_Mission_5_Progress = progress;
			MainViewModel.Instance.MO_Sands_Mission_5_Time = text;
			break;
		case 5:
			MainViewModel.Instance.MO_Sands_Mission_6_Progress = progress;
			MainViewModel.Instance.MO_Sands_Mission_6_Time = text;
			break;
		case 6:
			MainViewModel.Instance.MO_Sands_Mission_7_Progress = progress;
			MainViewModel.Instance.MO_Sands_Mission_7_Time = text;
			break;
		case 7:
			MainViewModel.Instance.MO_Sands_Mission_8_Progress = progress;
			MainViewModel.Instance.MO_Sands_Mission_8_Time = text;
			break;
		case 8:
			MainViewModel.Instance.MO_Sands_Mission_9_Progress = progress;
			MainViewModel.Instance.MO_Sands_Mission_9_Time = text;
			break;
		case 9:
			MainViewModel.Instance.MO_Sands_Mission_10_Progress = progress;
			MainViewModel.Instance.MO_Sands_Mission_10_Time = text;
			break;
		case 10:
			MainViewModel.Instance.MO_Sands_Mission_11_Progress = progress;
			MainViewModel.Instance.MO_Sands_Mission_11_Time = text;
			break;
		}
	}

	private ObservableCollection<string> GetPlayerValuesRow(int rowID)
	{
		return rowID switch
		{
			0 => MainViewModel.Instance.MO_MP_Players1Values, 
			1 => MainViewModel.Instance.MO_MP_Players2Values, 
			2 => MainViewModel.Instance.MO_MP_Players3Values, 
			3 => MainViewModel.Instance.MO_MP_Players4Values, 
			4 => MainViewModel.Instance.MO_MP_Players5Values, 
			5 => MainViewModel.Instance.MO_MP_Players6Values, 
			6 => MainViewModel.Instance.MO_MP_Players7Values, 
			7 => MainViewModel.Instance.MO_MP_Players8Values, 
			_ => null, 
		};
	}

	private ObservableCollection<Visibility> GetPlayerIconsRow(int rowID)
	{
		return rowID switch
		{
			0 => MainViewModel.Instance.MO_MP_Players1Icons, 
			1 => MainViewModel.Instance.MO_MP_Players2Icons, 
			2 => MainViewModel.Instance.MO_MP_Players3Icons, 
			3 => MainViewModel.Instance.MO_MP_Players4Icons, 
			4 => MainViewModel.Instance.MO_MP_Players5Icons, 
			5 => MainViewModel.Instance.MO_MP_Players6Icons, 
			6 => MainViewModel.Instance.MO_MP_Players7Icons, 
			7 => MainViewModel.Instance.MO_MP_Players8Icons, 
			_ => null, 
		};
	}

	private void SetPlayerShieldsImageSource(int rowID, ImageSource image)
	{
		switch (rowID)
		{
		case 0:
			MainViewModel.Instance.MO_MP_PlayersShields0 = image;
			break;
		case 1:
			MainViewModel.Instance.MO_MP_PlayersShields1 = image;
			break;
		case 2:
			MainViewModel.Instance.MO_MP_PlayersShields2 = image;
			break;
		case 3:
			MainViewModel.Instance.MO_MP_PlayersShields3 = image;
			break;
		case 4:
			MainViewModel.Instance.MO_MP_PlayersShields4 = image;
			break;
		case 5:
			MainViewModel.Instance.MO_MP_PlayersShields5 = image;
			break;
		case 6:
			MainViewModel.Instance.MO_MP_PlayersShields6 = image;
			break;
		case 7:
			MainViewModel.Instance.MO_MP_PlayersShields7 = image;
			break;
		}
	}

	private void SetPlayerFearImageSource(int rowID, ImageSource image)
	{
		switch (rowID)
		{
		case 0:
			MainViewModel.Instance.MO_MP_PlayersFear0 = image;
			break;
		case 1:
			MainViewModel.Instance.MO_MP_PlayersFear1 = image;
			break;
		case 2:
			MainViewModel.Instance.MO_MP_PlayersFear2 = image;
			break;
		case 3:
			MainViewModel.Instance.MO_MP_PlayersFear3 = image;
			break;
		case 4:
			MainViewModel.Instance.MO_MP_PlayersFear4 = image;
			break;
		case 5:
			MainViewModel.Instance.MO_MP_PlayersFear5 = image;
			break;
		case 6:
			MainViewModel.Instance.MO_MP_PlayersFear6 = image;
			break;
		case 7:
			MainViewModel.Instance.MO_MP_PlayersFear7 = image;
			break;
		}
	}

	private ImageSource GetPlayerFear(int fearPie)
	{
		return MainViewModel.Instance.GameSprites[115 + fearPie];
	}

	private int calc_greatest_lord_points(EngineInterface.MPScoreData mp_stats, int this_player)
	{
		if (this_player < 0)
		{
			return 0;
		}
		int num = mp_stats.troop_points_killed[this_player] + mp_stats.gold_acquired[this_player] / 10 + mp_stats.enemy_buildings_razed_points[this_player] * 100;
		int num2 = num + num * mp_stats.lords_killed[this_player] / 4;
		int num3 = mp_stats.blank2[1] + mp_stats.blank2[0] * 12;
		int num4 = mp_stats.blank2[3] + mp_stats.blank2[2] * 12 - num3;
		if (num4 < 1)
		{
			num4 = 1;
		}
		return num2 * 200 / (200 + num4);
	}

	private void prepMPScores(EngineInterface.MPScoreData mp_stats)
	{
		numActivePlayers = 0;
		if (individual_ranking == null)
		{
			individual_ranking = new int[16][];
			for (int i = 0; i < 16; i++)
			{
				individual_ranking[i] = new int[15];
			}
		}
		for (this_player = 1; this_player < 9; this_player++)
		{
			total = 0;
			points[this_player] = 0;
			for (int i = 1; i < 9; i++)
			{
				if (i != this_player)
				{
					total += mp_stats.who_killed_who[this_player * 9 + i];
				}
			}
			enemy_killed[this_player] = total;
			total = 0;
			for (int i = 0; i < 9; i++)
			{
				if (i != this_player)
				{
					total += mp_stats.who_killed_who[i * 9 + this_player];
				}
			}
			yours_killed[this_player] = total;
		}
		for (int i = 1; i < 9; i++)
		{
			ranking[i] = i;
			for (int j = 0; j < 16; j++)
			{
				individual_ranking[j][i] = i;
			}
			stars[i] = 0;
		}
		for (int i = 1; i < 9; i++)
		{
			for (int j = 1; j < 9 - i; j++)
			{
				int num = calc_greatest_lord_points(mp_stats, ranking[j]);
				int num2 = calc_greatest_lord_points(mp_stats, ranking[j + 1]);
				if (num < num2)
				{
					temp = ranking[j];
					ranking[j] = ranking[j + 1];
					ranking[j + 1] = temp;
				}
				else if (num == num2)
				{
					if (mp_stats.time_deceased[ranking[j]] < mp_stats.time_deceased[ranking[j + 1]])
					{
						temp = ranking[j];
						ranking[j] = ranking[j + 1];
						ranking[j + 1] = temp;
					}
					else if (mp_stats.time_deceased[ranking[j]] == mp_stats.time_deceased[ranking[j + 1]] && mp_stats.winners[ranking[j]] < mp_stats.winners[ranking[j + 1]])
					{
						temp = ranking[j];
						ranking[j] = ranking[j + 1];
						ranking[j + 1] = temp;
					}
				}
				if (enemy_killed[individual_ranking[0][j]] < enemy_killed[individual_ranking[0][j + 1]])
				{
					temp = individual_ranking[0][j];
					individual_ranking[0][j] = individual_ranking[0][j + 1];
					individual_ranking[0][j + 1] = temp;
				}
				if (yours_killed[individual_ranking[10][j]] > yours_killed[individual_ranking[10][j + 1]])
				{
					temp = individual_ranking[10][j];
					individual_ranking[10][j] = individual_ranking[10][j + 1];
					individual_ranking[10][j + 1] = temp;
				}
				if (mp_stats.enemy_buildings_destroyed[individual_ranking[1][j]] < mp_stats.enemy_buildings_destroyed[individual_ranking[1][j + 1]])
				{
					temp = individual_ranking[1][j];
					individual_ranking[1][j] = individual_ranking[1][j + 1];
					individual_ranking[1][j + 1] = temp;
				}
				if (mp_stats.food_produced[individual_ranking[2][j]] < mp_stats.food_produced[individual_ranking[2][j + 1]])
				{
					temp = individual_ranking[2][j];
					individual_ranking[2][j] = individual_ranking[2][j + 1];
					individual_ranking[2][j + 1] = temp;
				}
				if (mp_stats.gold_acquired[individual_ranking[3][j]] < mp_stats.gold_acquired[individual_ranking[3][j + 1]])
				{
					temp = individual_ranking[3][j];
					individual_ranking[3][j] = individual_ranking[3][j + 1];
					individual_ranking[3][j + 1] = temp;
				}
				if (mp_stats.max_population[individual_ranking[4][j]] < mp_stats.max_population[individual_ranking[4][j + 1]])
				{
					temp = individual_ranking[4][j];
					individual_ranking[4][j] = individual_ranking[4][j + 1];
					individual_ranking[4][j + 1] = temp;
				}
				if (mp_stats.wood_produced[individual_ranking[5][j]] < mp_stats.wood_produced[individual_ranking[5][j + 1]])
				{
					temp = individual_ranking[5][j];
					individual_ranking[5][j] = individual_ranking[5][j + 1];
					individual_ranking[5][j + 1] = temp;
				}
				if (mp_stats.stone_produced[individual_ranking[6][j]] < mp_stats.stone_produced[individual_ranking[6][j + 1]])
				{
					temp = individual_ranking[6][j];
					individual_ranking[6][j] = individual_ranking[6][j + 1];
					individual_ranking[6][j + 1] = temp;
				}
				if (mp_stats.iron_produced[individual_ranking[7][j]] < mp_stats.iron_produced[individual_ranking[7][j + 1]])
				{
					temp = individual_ranking[7][j];
					individual_ranking[7][j] = individual_ranking[7][j + 1];
					individual_ranking[7][j + 1] = temp;
				}
				if (mp_stats.pitch_produced[individual_ranking[8][j]] < mp_stats.pitch_produced[individual_ranking[8][j + 1]])
				{
					temp = individual_ranking[8][j];
					individual_ranking[8][j] = individual_ranking[8][j + 1];
					individual_ranking[8][j + 1] = temp;
				}
				if (mp_stats.weapons_produced[individual_ranking[11][j]] < mp_stats.weapons_produced[individual_ranking[11][j + 1]])
				{
					temp = individual_ranking[11][j];
					individual_ranking[11][j] = individual_ranking[11][j + 1];
					individual_ranking[11][j + 1] = temp;
				}
				if (mp_stats.buildings_lost[individual_ranking[12][j]] > mp_stats.buildings_lost[individual_ranking[12][j + 1]])
				{
					temp = individual_ranking[12][j];
					individual_ranking[12][j] = individual_ranking[12][j + 1];
					individual_ranking[12][j + 1] = temp;
				}
				if (mp_stats.troops_produced[individual_ranking[13][j]] < mp_stats.troops_produced[individual_ranking[13][j + 1]])
				{
					temp = individual_ranking[13][j];
					individual_ranking[13][j] = individual_ranking[13][j + 1];
					individual_ranking[13][j + 1] = temp;
				}
				if (mp_stats.goods_received[individual_ranking[14][j]] < mp_stats.goods_received[individual_ranking[14][j + 1]])
				{
					temp = individual_ranking[14][j];
					individual_ranking[14][j] = individual_ranking[14][j + 1];
					individual_ranking[14][j + 1] = temp;
				}
				if (mp_stats.goods_sent[individual_ranking[15][j]] < mp_stats.goods_sent[individual_ranking[15][j + 1]])
				{
					temp = individual_ranking[15][j];
					individual_ranking[15][j] = individual_ranking[15][j + 1];
					individual_ranking[15][j + 1] = temp;
				}
			}
		}
		points[ranking[1]] += 3;
		if (enemy_killed[individual_ranking[0][1]] > 10 && enemy_killed[individual_ranking[0][1]] > enemy_killed[individual_ranking[0][2]] * 2)
		{
			points[individual_ranking[0][1]] += 3;
		}
		if (mp_stats.enemy_buildings_destroyed[individual_ranking[1][1]] > 2 && mp_stats.enemy_buildings_destroyed[individual_ranking[1][1]] > mp_stats.enemy_buildings_destroyed[individual_ranking[1][2]] * 2)
		{
			points[individual_ranking[1][1]] += 2;
		}
		if (mp_stats.food_produced[individual_ranking[2][1]] > 50 && mp_stats.food_produced[individual_ranking[2][1]] > mp_stats.food_produced[individual_ranking[2][2]] * 2)
		{
			points[individual_ranking[2][1]] += 2;
		}
		if (mp_stats.gold_acquired[individual_ranking[3][1]] > 100 && mp_stats.gold_acquired[individual_ranking[3][1]] > mp_stats.gold_acquired[individual_ranking[3][2]] * 2)
		{
			points[individual_ranking[3][1]] += 3;
		}
		if (mp_stats.max_population[individual_ranking[4][1]] > 24 && mp_stats.max_population[individual_ranking[4][1]] > mp_stats.max_population[individual_ranking[4][2]] * 2)
		{
			points[individual_ranking[0][1]]++;
		}
		if (mp_stats.wood_produced[individual_ranking[5][1]] > 100 && mp_stats.wood_produced[individual_ranking[5][1]] > mp_stats.wood_produced[individual_ranking[5][2]] * 2)
		{
			points[individual_ranking[5][1]]++;
		}
		if (mp_stats.stone_produced[individual_ranking[6][1]] > 50 && mp_stats.stone_produced[individual_ranking[6][1]] > mp_stats.stone_produced[individual_ranking[6][2]] * 2)
		{
			points[individual_ranking[6][1]]++;
		}
		if (mp_stats.iron_produced[individual_ranking[7][1]] > 10 && mp_stats.iron_produced[individual_ranking[7][1]] > mp_stats.iron_produced[individual_ranking[7][2]] * 2)
		{
			points[individual_ranking[7][1]]++;
		}
		if (mp_stats.pitch_produced[individual_ranking[8][1]] > 20 && mp_stats.pitch_produced[individual_ranking[8][1]] > mp_stats.pitch_produced[individual_ranking[8][2]] * 2)
		{
			points[individual_ranking[8][1]]++;
		}
		if (mp_stats.weapons_produced[individual_ranking[11][1]] > 20 && mp_stats.weapons_produced[individual_ranking[11][1]] > mp_stats.weapons_produced[individual_ranking[11][2]] * 2)
		{
			points[individual_ranking[11][1]]++;
		}
		num_players = 0;
		for (id = 1; id < 9; id++)
		{
			if (mp_stats.valid[1] > 0)
			{
				num_players++;
			}
		}
		for (this_player = 1; this_player < 9; this_player++)
		{
			stars[this_player] = points[this_player] / 3;
			if (stars[this_player] > 5)
			{
				stars[this_player] = 5;
			}
			if (num_players < 4 && stars[this_player] == 5)
			{
				stars[this_player] = 4;
			}
		}
		for (int k = 1; k < 9; k++)
		{
			int num3 = ranking[k];
			if (mp_stats.valid[num3] > 0)
			{
				numActivePlayers++;
			}
		}
	}

	private void ClearWeaselText()
	{
		RefCoopCompletion.Visibility = Visibility.Hidden;
		RefWeaselTurdText1.Opacity = 0f;
		RefWeaselTurdText2.Opacity = 0f;
		RefWeaselTurdText3.Opacity = 0f;
		RefWeaselTurdText4.Opacity = 0f;
		RefWeaselTurdText5.Opacity = 0f;
		refWeaselTurd1.Stop();
		refWeaselTurd2.Stop();
	}

	public void LoadVideoBackground(string fileName, bool skirmishMasters = false, bool loop = false)
	{
		ClearWeaselText();
		ShownWeaselTurd = false;
		init();
		if (!skirmishMasters)
		{
			refMissionOverVideo = refMissionOverVideo1;
		}
		else
		{
			refMissionOverVideo = refMissionOverVideo2;
		}
		refMissionOverVideo.Stop();
		refMissionOverVideo.Source = null;
		refMissionOverVideo.Close();
		string text = "Assets/GUI/Video/" + fileName;
		if (!loop)
		{
			text += "**";
		}
		Uri source = new Uri(text, UriKind.Relative);
		refMissionOverVideo.Source = source;
		refMissionOverVideo.Volume = ConfigSettings.Settings_SpeechVolume * MyAudioManager.GetMasterVolume();
		MainViewModel.Instance.Show_HUD_MissionOver_SandsBackground = false;
		loopVideo = loop;
		PlayBackgroundVideo(state: true);
	}

	public static void setFutureVoiceLine(string wav)
	{
		FutureVoiceLine = wav;
		FutureVoiceLineStart = DateTime.UtcNow.AddSeconds(2.0);
	}

	public void Update()
	{
		if (FutureVoiceLine.Length > 0)
		{
			if (!MyAudioManager.Instance.isSpeechPlaying(1))
			{
				if (DateTime.UtcNow > FutureVoiceLineStart)
				{
					SFXManager.instance.playAdditionalSpeech(FutureVoiceLine, ignorePauseState: true);
					FutureVoiceLine = "";
				}
			}
			else
			{
				FutureVoiceLineStart = DateTime.UtcNow.AddSeconds(1.0);
			}
		}
		if (MainViewModel.Instance.MO_SandsOutro1 == Visibility.Visible)
		{
			int num = 1500 - (int)RefMO_SandsCurProgress.Width;
			if (num < 188)
			{
				RefMO_Sands_Mission_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince);
				RefMO_Sands_Mission_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion, greyedImage: true);
				RefMO_Sands_Mission_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior, greyedImage: true);
				RefMO_Sands_Mission_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman, greyedImage: true);
				RefMO_Sands_Mission_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant, greyedImage: true);
				RefMO_Sands_Mission_Bar_1.Opacity = 1f;
				RefMO_Sands_Mission_Bar_2.Opacity = 1f;
				RefMO_Sands_Mission_Bar_3.Opacity = 1f;
				RefMO_Sands_Mission_Bar_4.Opacity = 1f;
				RefMO_Sands_Mission_Bar_5.Opacity = 1f;
			}
			else if (num < 281)
			{
				RefMO_Sands_Mission_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince, greyedImage: true);
				RefMO_Sands_Mission_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion);
				RefMO_Sands_Mission_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior, greyedImage: true);
				RefMO_Sands_Mission_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman, greyedImage: true);
				RefMO_Sands_Mission_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant, greyedImage: true);
				RefMO_Sands_Mission_Bar_1.Opacity = 0.5f;
				RefMO_Sands_Mission_Bar_2.Opacity = 1f;
				RefMO_Sands_Mission_Bar_3.Opacity = 1f;
				RefMO_Sands_Mission_Bar_4.Opacity = 1f;
				RefMO_Sands_Mission_Bar_5.Opacity = 1f;
			}
			else if (num < 375)
			{
				RefMO_Sands_Mission_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince, greyedImage: true);
				RefMO_Sands_Mission_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion, greyedImage: true);
				RefMO_Sands_Mission_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior);
				RefMO_Sands_Mission_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman, greyedImage: true);
				RefMO_Sands_Mission_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant, greyedImage: true);
				RefMO_Sands_Mission_Bar_1.Opacity = 0.5f;
				RefMO_Sands_Mission_Bar_2.Opacity = 0.5f;
				RefMO_Sands_Mission_Bar_3.Opacity = 1f;
				RefMO_Sands_Mission_Bar_4.Opacity = 1f;
				RefMO_Sands_Mission_Bar_5.Opacity = 1f;
			}
			else if (num < 750)
			{
				RefMO_Sands_Mission_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince, greyedImage: true);
				RefMO_Sands_Mission_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion, greyedImage: true);
				RefMO_Sands_Mission_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior, greyedImage: true);
				RefMO_Sands_Mission_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman);
				RefMO_Sands_Mission_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant, greyedImage: true);
				RefMO_Sands_Mission_Bar_1.Opacity = 0.5f;
				RefMO_Sands_Mission_Bar_2.Opacity = 0.5f;
				RefMO_Sands_Mission_Bar_3.Opacity = 0.5f;
				RefMO_Sands_Mission_Bar_4.Opacity = 1f;
				RefMO_Sands_Mission_Bar_5.Opacity = 1f;
			}
			else
			{
				RefMO_Sands_Mission_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince, greyedImage: true);
				RefMO_Sands_Mission_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion, greyedImage: true);
				RefMO_Sands_Mission_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior, greyedImage: true);
				RefMO_Sands_Mission_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman, greyedImage: true);
				RefMO_Sands_Mission_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant);
				RefMO_Sands_Mission_Bar_1.Opacity = 0.5f;
				RefMO_Sands_Mission_Bar_2.Opacity = 0.5f;
				RefMO_Sands_Mission_Bar_3.Opacity = 0.5f;
				RefMO_Sands_Mission_Bar_4.Opacity = 0.5f;
				RefMO_Sands_Mission_Bar_5.Opacity = 1f;
			}
			int num2 = 1700 - (int)RefMO_SandsTrailProgress.Width;
			if (num2 < 213)
			{
				RefMO_Sands_Trail_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince);
				RefMO_Sands_Trail_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion, greyedImage: true);
				RefMO_Sands_Trail_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior, greyedImage: true);
				RefMO_Sands_Trail_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman, greyedImage: true);
				RefMO_Sands_Trail_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant, greyedImage: true);
				RefMO_Sands_Trail_Bar_1.Opacity = 1f;
				RefMO_Sands_Trail_Bar_2.Opacity = 1f;
				RefMO_Sands_Trail_Bar_3.Opacity = 1f;
				RefMO_Sands_Trail_Bar_4.Opacity = 1f;
				RefMO_Sands_Trail_Bar_5.Opacity = 1f;
			}
			else if (num2 < 318)
			{
				RefMO_Sands_Trail_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince, greyedImage: true);
				RefMO_Sands_Trail_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion);
				RefMO_Sands_Trail_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior, greyedImage: true);
				RefMO_Sands_Trail_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman, greyedImage: true);
				RefMO_Sands_Trail_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant, greyedImage: true);
				RefMO_Sands_Trail_Bar_1.Opacity = 0.5f;
				RefMO_Sands_Trail_Bar_2.Opacity = 1f;
				RefMO_Sands_Trail_Bar_3.Opacity = 1f;
				RefMO_Sands_Trail_Bar_4.Opacity = 1f;
				RefMO_Sands_Trail_Bar_5.Opacity = 1f;
			}
			else if (num2 < 425)
			{
				RefMO_Sands_Trail_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince, greyedImage: true);
				RefMO_Sands_Trail_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion, greyedImage: true);
				RefMO_Sands_Trail_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior);
				RefMO_Sands_Trail_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman, greyedImage: true);
				RefMO_Sands_Trail_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant, greyedImage: true);
				RefMO_Sands_Trail_Bar_1.Opacity = 0.5f;
				RefMO_Sands_Trail_Bar_2.Opacity = 0.5f;
				RefMO_Sands_Trail_Bar_3.Opacity = 1f;
				RefMO_Sands_Trail_Bar_4.Opacity = 1f;
				RefMO_Sands_Trail_Bar_5.Opacity = 1f;
			}
			else if (num2 < 850)
			{
				RefMO_Sands_Trail_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince, greyedImage: true);
				RefMO_Sands_Trail_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion, greyedImage: true);
				RefMO_Sands_Trail_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior, greyedImage: true);
				RefMO_Sands_Trail_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman);
				RefMO_Sands_Trail_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant, greyedImage: true);
				RefMO_Sands_Trail_Bar_1.Opacity = 0.5f;
				RefMO_Sands_Trail_Bar_2.Opacity = 0.5f;
				RefMO_Sands_Trail_Bar_3.Opacity = 0.5f;
				RefMO_Sands_Trail_Bar_4.Opacity = 1f;
				RefMO_Sands_Trail_Bar_5.Opacity = 1f;
			}
			else
			{
				RefMO_Sands_Trail_Bar_1.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Prince, greyedImage: true);
				RefMO_Sands_Trail_Bar_2.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Champion, greyedImage: true);
				RefMO_Sands_Trail_Bar_3.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Warrior, greyedImage: true);
				RefMO_Sands_Trail_Bar_4.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Tribesman, greyedImage: true);
				RefMO_Sands_Trail_Bar_5.Source = GameData.GetSandsOfTimeImage(Enums.SandsRanks.Peasant);
				RefMO_Sands_Trail_Bar_1.Opacity = 0.5f;
				RefMO_Sands_Trail_Bar_2.Opacity = 0.5f;
				RefMO_Sands_Trail_Bar_3.Opacity = 0.5f;
				RefMO_Sands_Trail_Bar_4.Opacity = 0.5f;
				RefMO_Sands_Trail_Bar_5.Opacity = 1f;
			}
		}
	}

	public void ButtonClicked(string param)
	{
		switch (param)
		{
		case "Troops":
			MainViewModel.Instance.MO_ShowPage1 = Visibility.Visible;
			MainViewModel.Instance.MO_ShowPage2 = Visibility.Collapsed;
			mp_sortType = 0;
			sortReversed = false;
			MainViewModel.Instance.HUDMissionOver.refRankButton1.IsChecked = true;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "Eco":
			MainViewModel.Instance.MO_ShowPage1 = Visibility.Collapsed;
			MainViewModel.Instance.MO_ShowPage2 = Visibility.Visible;
			mp_sortType = 0;
			sortReversed = false;
			MainViewModel.Instance.HUDMissionOver.refRankButton2.IsChecked = true;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "Rank":
			if (mp_sortType != 0)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 0;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "EnemyKilled":
			if (mp_sortType != 1)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 1;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "YoursKilled":
			if (mp_sortType != 3)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 3;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "BuildingsDestroyed":
			if (mp_sortType != 2)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 2;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "BuildingsLost":
			if (mp_sortType != 12)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 12;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "TroopsMade":
			if (mp_sortType != 13)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 13;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "Gold":
			if (mp_sortType != 4)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 4;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "Food":
			if (mp_sortType != 5)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 5;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "Wood":
			if (mp_sortType != 6)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 6;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "Stone":
			if (mp_sortType != 7)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 7;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "Iron":
			if (mp_sortType != 8)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 8;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "Pitch":
			if (mp_sortType != 9)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 9;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "Weapons":
			if (mp_sortType != 11)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 11;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "Peasants":
			if (mp_sortType != 10)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 10;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "TradeIn":
			if (mp_sortType != 14)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 14;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "TradeOut":
			if (mp_sortType != 15)
			{
				sortReversed = false;
			}
			else
			{
				sortReversed = !sortReversed;
			}
			mp_sortType = 15;
			ShowMPScore(last_stats, prepScores: false);
			return;
		case "Stats":
			MainViewModel.Instance.MO_SandsOutro1 = Visibility.Hidden;
			MainViewModel.Instance.MO_MP_Score = Visibility.Visible;
			return;
		}
		if (SandsTrailOutro && MainViewModel.Instance.MO_MP_Score == Visibility.Visible)
		{
			MainViewModel.Instance.MO_SandsOutro1 = Visibility.Visible;
			MainViewModel.Instance.MO_MP_Score = Visibility.Hidden;
			return;
		}
		if (GameData.Instance.game_type == 3 && Director.instance.WasSkirmishModeGame && !ShownWeaselTurd)
		{
			if (Victory && GameData.Instance.coopTrailID == 0 && GameData.Instance.SkirmishGameType == 1)
			{
				bool flag = false;
				bool flag2 = false;
				int num = 0;
				switch (GameData.Instance.SkirmishTrailType)
				{
				case 0:
					if (GameData.Instance.SkirmishTrailLevel >= 49)
					{
						flag = true;
					}
					break;
				case 1:
					if (GameData.Instance.SkirmishTrailLevel >= 29)
					{
						flag = true;
					}
					break;
				case 2:
					if (GameData.Instance.SkirmishTrailLevel >= 19)
					{
						flag = true;
					}
					num = 1;
					break;
				case 11:
					if (GameData.Instance.SkirmishTrailLevel >= 4)
					{
						flag2 = true;
					}
					break;
				case 12:
					if (GameData.Instance.SkirmishTrailLevel >= 6)
					{
						flag2 = true;
					}
					break;
				case 13:
					if (GameData.Instance.SkirmishTrailLevel >= 8)
					{
						flag2 = true;
					}
					break;
				case 14:
					if (GameData.Instance.SkirmishTrailLevel >= 10)
					{
						flag2 = true;
					}
					break;
				case 15:
					if (GameData.Instance.SkirmishTrailLevel >= 8)
					{
						flag2 = true;
					}
					break;
				case 16:
					if (GameData.Instance.SkirmishTrailLevel >= 8)
					{
						flag2 = true;
					}
					break;
				case 17:
					if (GameData.Instance.SkirmishTrailLevel >= 8)
					{
						flag2 = true;
					}
					break;
				case 18:
					if (GameData.Instance.SkirmishTrailLevel >= 8)
					{
						flag2 = true;
					}
					break;
				}
				if (flag)
				{
					MainViewModel.Instance.MO_SandsOutro1 = Visibility.Hidden;
					MainViewModel.Instance.MO_MP_Score = Visibility.Hidden;
					MainViewModel.Instance.MO_SP_Score = true;
					MainViewModel.Instance.MO_MP_Victory = Visibility.Hidden;
					MainViewModel.Instance.MO_MP_Defeat = Visibility.Collapsed;
					if (Screen.width <= 1920 && Screen.height <= 1080)
					{
						LoadVideoBackground("WeaselTurd_low.webm", SkirmishMasters);
					}
					else
					{
						LoadVideoBackground("WeaselTurd.webm", SkirmishMasters);
					}
					ShownWeaselTurd = true;
					if (num == 0)
					{
						refWeaselTurd1.Seek(TimeSpan.Zero);
						refWeaselTurd1.Begin();
					}
					else
					{
						refWeaselTurd2.Seek(TimeSpan.Zero);
						refWeaselTurd2.Begin();
					}
					return;
				}
				if (flag2)
				{
					MainViewModel.Instance.MO_SandsOutro1 = Visibility.Hidden;
					MainViewModel.Instance.MO_MP_Score = Visibility.Hidden;
					MainViewModel.Instance.MO_SP_Score = true;
					MainViewModel.Instance.MO_MP_Victory = Visibility.Hidden;
					MainViewModel.Instance.MO_MP_Defeat = Visibility.Collapsed;
					LoadVideoBackground("Victory\\Desert Background.webm", skirmishMasters: false, loop: true);
					SFXManager.instance.playMusic(131, fadePrevious: false, 1f, restartOnSamePiece: false);
					ShownWeaselTurd = true;
					RefCoopCompletion.Visibility = Visibility.Visible;
					if (FatControler.english)
					{
						MainViewModel.Instance.MissionOver_CoopTrailTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 533);
						if (GameData.Instance.SkirmishTrailType == 17)
						{
							MainViewModel.Instance.MissionOver_CoopTrail = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 102);
						}
						else if (GameData.Instance.SkirmishTrailType == 18)
						{
							MainViewModel.Instance.MissionOver_CoopTrail = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 103);
						}
						else
						{
							MainViewModel.Instance.MissionOver_CoopTrail = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, -10 + GameData.Instance.SkirmishTrailType);
						}
					}
					else
					{
						MainViewModel.Instance.MissionOver_CoopTrailTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 523 + GameData.Instance.SkirmishTrailType);
						MainViewModel.Instance.MissionOver_CoopTrail = "";
					}
					return;
				}
			}
			if (Victory && GameData.Instance.coopTrailID > 0 && GameData.Instance.coopMissionID >= 9)
			{
				MainViewModel.Instance.MO_SandsOutro1 = Visibility.Hidden;
				MainViewModel.Instance.MO_MP_Score = Visibility.Hidden;
				MainViewModel.Instance.MO_SP_Score = true;
				MainViewModel.Instance.MO_MP_Victory = Visibility.Hidden;
				MainViewModel.Instance.MO_MP_Defeat = Visibility.Collapsed;
				SFXManager.instance.playMusic(131, fadePrevious: false, 1f, restartOnSamePiece: false);
				LoadVideoBackground("Victory\\Desert Background.webm", skirmishMasters: false, loop: true);
				ShownWeaselTurd = true;
				RefCoopCompletion.Visibility = Visibility.Visible;
				if (FatControler.english)
				{
					MainViewModel.Instance.MissionOver_CoopTrailTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 527);
					MainViewModel.Instance.MissionOver_CoopTrail = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_COOP, 21 + GameData.Instance.coopTrailID);
				}
				else
				{
					MainViewModel.Instance.MissionOver_CoopTrailTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 528 + GameData.Instance.coopTrailID);
					MainViewModel.Instance.MissionOver_CoopTrail = "";
				}
				return;
			}
		}
		MainViewModel.Instance.Show_HUD_MissionOver = false;
		if (SkirmishMasters)
		{
			return;
		}
		if (GameData.Instance.game_type == 0)
		{
			if (Victory)
			{
				int mission_level = GameData.Instance.mission_level;
				if (mission_level == 5 || mission_level == 10 || mission_level == 15 || mission_level == 20 || mission_level == 25 || mission_level == 30 || mission_level == 35)
				{
					FRONT_Story.OpenStory(mission_level, afterMath: true);
				}
				else
				{
					FRONT_Story.OpenStory(mission_level + 1);
				}
			}
			else
			{
				int mission_level2 = GameData.Instance.mission_level;
				MainViewModel.Instance.StartCampaignMission(mission_level2);
			}
		}
		else if (GameData.Instance.game_type == 3 && Director.instance.WasSkirmishModeGame)
		{
			if (GameData.Instance.coopTrailID > 0)
			{
				if (Director.instance.MultiplayerGame)
				{
					FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
					if (GameData.Instance.coopMissionID < 9)
					{
						if (GameData.Instance.coopTrailID == 1)
						{
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop");
						}
						else if (GameData.Instance.coopTrailID == 2)
						{
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop2");
						}
						else if (GameData.Instance.coopTrailID == 3)
						{
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop3");
						}
						else if (GameData.Instance.coopTrailID == 4)
						{
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop4");
						}
					}
					return;
				}
				FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
				if (GameData.Instance.coopMissionID < 9)
				{
					if (GameData.Instance.coopTrailID == 1)
					{
						MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop");
					}
					else if (GameData.Instance.coopTrailID == 2)
					{
						MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop2");
					}
					else if (GameData.Instance.coopTrailID == 3)
					{
						MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop3");
					}
					else if (GameData.Instance.coopTrailID == 4)
					{
						MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop4");
					}
					MainViewModel.Instance.FRONTMultiplayer.SkirmishAIAddClick((GameData.Instance.coopMissionAlly - 1).ToString());
				}
			}
			else if (Victory)
			{
				if (GameData.Instance.SkirmishGameType == 1)
				{
					switch (GameData.Instance.SkirmishTrailType)
					{
					case 0:
						if (GameData.Instance.SkirmishTrailLevel < 49)
						{
							MainViewModel.Instance.FrontEndMenu.StartTrailMission(GameData.Instance.SkirmishTrailLevel + 1, GameData.Instance.SkirmishTrailType);
						}
						else
						{
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						}
						break;
					case 1:
						if (GameData.Instance.SkirmishTrailLevel < 29)
						{
							MainViewModel.Instance.FrontEndMenu.StartTrailMission(GameData.Instance.SkirmishTrailLevel + 1, GameData.Instance.SkirmishTrailType);
						}
						else
						{
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						}
						break;
					case 2:
						if (GameData.Instance.SkirmishTrailLevel < 19)
						{
							MainViewModel.Instance.FrontEndMenu.StartTrailMission(GameData.Instance.SkirmishTrailLevel + 1, GameData.Instance.SkirmishTrailType);
						}
						else
						{
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						}
						break;
					case 11:
						if (GameData.Instance.SkirmishTrailLevel < 4)
						{
							MainViewModel.Instance.FrontEndMenu.StartTrailMission(GameData.Instance.SkirmishTrailLevel + 1, GameData.Instance.SkirmishTrailType);
						}
						else
						{
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						}
						break;
					case 12:
						if (GameData.Instance.SkirmishTrailLevel < 6)
						{
							MainViewModel.Instance.FrontEndMenu.StartTrailMission(GameData.Instance.SkirmishTrailLevel + 1, GameData.Instance.SkirmishTrailType);
						}
						else
						{
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						}
						break;
					case 13:
						if (GameData.Instance.SkirmishTrailLevel < 8)
						{
							MainViewModel.Instance.FrontEndMenu.StartTrailMission(GameData.Instance.SkirmishTrailLevel + 1, GameData.Instance.SkirmishTrailType);
						}
						else
						{
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						}
						break;
					case 14:
						if (GameData.Instance.SkirmishTrailLevel < 10)
						{
							MainViewModel.Instance.FrontEndMenu.StartTrailMission(GameData.Instance.SkirmishTrailLevel + 1, GameData.Instance.SkirmishTrailType);
						}
						else
						{
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						}
						break;
					case 15:
						if (GameData.Instance.SkirmishTrailLevel < 8)
						{
							MainViewModel.Instance.FrontEndMenu.StartTrailMission(GameData.Instance.SkirmishTrailLevel + 1, GameData.Instance.SkirmishTrailType);
						}
						else
						{
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						}
						break;
					case 16:
						if (GameData.Instance.SkirmishTrailLevel < 8)
						{
							MainViewModel.Instance.FrontEndMenu.StartTrailMission(GameData.Instance.SkirmishTrailLevel + 1, GameData.Instance.SkirmishTrailType);
						}
						else
						{
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						}
						break;
					case 17:
						if (GameData.Instance.SkirmishTrailLevel < 8)
						{
							MainViewModel.Instance.FrontEndMenu.StartTrailMission(GameData.Instance.SkirmishTrailLevel + 1, GameData.Instance.SkirmishTrailType);
						}
						else
						{
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						}
						break;
					case 18:
						if (GameData.Instance.SkirmishTrailLevel < 8)
						{
							MainViewModel.Instance.FrontEndMenu.StartTrailMission(GameData.Instance.SkirmishTrailLevel + 1, GameData.Instance.SkirmishTrailType);
						}
						else
						{
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						}
						break;
					case 3:
					case 4:
					case 5:
					case 6:
					case 7:
					case 8:
					case 9:
					case 10:
						break;
					}
				}
				else if (GameData.Instance.SkirmishGameType == 2)
				{
					MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
					if (MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo != null && MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrail)
					{
						MainViewModel.Instance.StartCustomTrailMission(MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrailName, MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrailLevel + 1, 1);
					}
				}
				else
				{
					MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
					if (MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo != null)
					{
						FRONT_Multiplayer.Open(skirmishSetup: true, MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo);
						MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
						MainViewModel.Instance.Show_Frontend_MainMenu = false;
						MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo = null;
						MainViewModel.Instance.HUDIngameMenu.restartMPInfo = null;
					}
				}
			}
			else if (GameData.Instance.SkirmishGameType == 1)
			{
				MainViewModel.Instance.FrontEndMenu.StartTrailMission(GameData.Instance.SkirmishTrailLevel, GameData.Instance.SkirmishTrailType);
			}
			else if (GameData.Instance.SkirmishGameType == 2)
			{
				MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
				if (MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo != null && MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrail)
				{
					MainViewModel.Instance.FrontEndMenu.OpenCustomTrail(MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrailName, MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrailLevel);
				}
			}
			else
			{
				MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
				if (MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo != null)
				{
					FRONT_Multiplayer.Open(skirmishSetup: true, MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo);
					MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
					MainViewModel.Instance.Show_Frontend_MainMenu = false;
					MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo = null;
					MainViewModel.Instance.HUDIngameMenu.restartMPInfo = null;
				}
			}
		}
		else if (GameData.Instance.game_type == 3 && !Director.instance.WasSkirmishModeGame && GameData.Instance.coopTrailID > 0)
		{
			FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
			if (MainViewModel.Instance.FRONTMultiplayer.CoopPendingReadyHost())
			{
				if (GameData.Instance.coopTrailID == 1)
				{
					MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop");
				}
				else if (GameData.Instance.coopTrailID == 2)
				{
					MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop2");
				}
				else if (GameData.Instance.coopTrailID == 3)
				{
					MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop3");
				}
				else if (GameData.Instance.coopTrailID == 4)
				{
					MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop4");
				}
			}
			else
			{
				MainViewModel.Instance.FRONTMultiplayer.ResumeCoop();
			}
		}
		else
		{
			MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
		}
	}

	public void ButtonMouseEnter(string param)
	{
		switch (param)
		{
		case "Troops":
			UpdateHelpText(16);
			break;
		case "Eco":
			UpdateHelpText(17);
			break;
		case "Rank":
			UpdateHelpText(0);
			break;
		case "EnemyKilled":
			UpdateHelpText(1);
			break;
		case "YoursKilled":
			UpdateHelpText(3);
			break;
		case "BuildingsDestroyed":
			UpdateHelpText(2);
			break;
		case "BuildingsLost":
			UpdateHelpText(12);
			break;
		case "TroopsMade":
			UpdateHelpText(13);
			break;
		case "Gold":
			UpdateHelpText(4);
			break;
		case "Food":
			UpdateHelpText(5);
			break;
		case "Wood":
			UpdateHelpText(6);
			break;
		case "Stone":
			UpdateHelpText(7);
			break;
		case "Iron":
			UpdateHelpText(8);
			break;
		case "Pitch":
			UpdateHelpText(9);
			break;
		case "Weapons":
			UpdateHelpText(11);
			break;
		case "Peasants":
			UpdateHelpText(10);
			break;
		case "TradeIn":
			UpdateHelpText(14);
			break;
		case "TradeOut":
			UpdateHelpText(15);
			break;
		case "Fear":
			UpdateHelpText(18);
			break;
		case "Exit":
			UpdateHelpText(19);
			break;
		}
	}

	public void ButtonMouseLeave(string param)
	{
		UpdateHelpText(mp_sortType);
	}

	private void UpdateHelpText(int helpType)
	{
		MainViewModel.Instance.MO_HelpRollover = Translate.Instance.lookUpText((Enums.eTextSections)mp_help_text[helpType, 0], mp_help_text[helpType, 1]);
		if (helpType == 0 && last_stats != null && last_stats.blank2[0] != 0)
		{
			if (FatControler.arabic && ConfigSettings.Settings_ArabicL2R)
			{
				MainViewModel.Instance.MO_HelpGreatestLord = last_stats.blank2[0] + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, last_stats.blank2[1]) + " - " + last_stats.blank2[2] + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, last_stats.blank2[3]);
			}
			else
			{
				MainViewModel.Instance.MO_HelpGreatestLord = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, last_stats.blank2[1]) + " " + last_stats.blank2[0] + " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, last_stats.blank2[3]) + " " + last_stats.blank2[2];
			}
		}
		else
		{
			MainViewModel.Instance.MO_HelpGreatestLord = "";
		}
	}

	public void PlayBackgroundVideo(bool state)
	{
		if (state)
		{
			MainViewModel.Instance.Show_HUD_MissionOver_Video = true;
			FrontendMenus.UpdateVideoScale();
			refMissionOverVideo.Play();
			return;
		}
		MainViewModel.Instance.Show_HUD_MissionOver_Video = false;
		if (refMissionOverVideo != null)
		{
			refMissionOverVideo.Stop();
			refMissionOverVideo.Source = null;
			refMissionOverVideo.Close();
		}
	}

	private void FrontendBackVideo_Ended(object sender, RoutedEventArgs args)
	{
		if (!loopVideo)
		{
			refMissionOverVideo.Stop();
			if (GameData.Instance.game_type != 3 && (!Director.instance.WasSkirmishModeGame || GameData.Instance.game_type == 0) && !Director.instance.MultiplayerGame && !SkirmishMasters)
			{
				ButtonClicked("Exit");
			}
		}
	}

	private void InitializeComponent()
	{
		Noesis.GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_MissionOver.xaml");
	}
}
