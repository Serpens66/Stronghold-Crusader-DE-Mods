using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeStage.AdvancedFPSCounter;
using CodeStage.AdvancedFPSCounter.CountersData;
using CrusaderDE;
using Noesis;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vuplex.WebView;

public class FatControler : MonoBehaviour
{
	public static FatControler instance = null;

	public bool exiting;

	public Camera m_MainCamera;

	public NoesisView NGview;

	public bool overUI;

	public bool overBuildingMenu;

	public bool mouseIsDown;

	public static bool mouseIsUpStroke = false;

	public static bool mouseIsDownStroke = false;

	public string lastUIHit = "None";

	public static int firstScene = -1;

	public static Enums.SceneIDS currentScene = (Enums.SceneIDS)0;

	public float SHLowerUIPoint;

	public int SHRadarRectSize;

	public float SHRadarScalar = 1f;

	public DateTime lastRadarMapMove = DateTime.MinValue;

	public DateTime binkPlayDelay = DateTime.MinValue;

	public Point SHMapStartPoint = new Point(0f, 0f);

	public Point NGMousePoint = new Point(0f, 0f);

	public Point LastNGMousePoint = new Point(0f, 0f);

	public Point BriefingHelpMousePoint = new Point(0f, 0f);

	public int setupFileList = 3;

	public bool binkPlayWait;

	public bool binkPaused;

	public int lastPopularity = 100;

	public string last_richPresenceString = "";

	public int last_richPresenceMissionID = -1;

	public static string locale;

	public static bool english = false;

	public static bool german = false;

	public static bool french = false;

	public static bool spanish = false;

	public static bool italian = false;

	public static bool polish = false;

	public static bool russian = false;

	public static bool portuguese = false;

	public static bool japanese = false;

	public static bool korean = false;

	public static bool simplified_chinese = false;

	public static bool traditional_chinese = false;

	public static bool czech = false;

	public static bool turkish = false;

	public static bool hungarian = false;

	public static bool thai = false;

	public static bool ukrainian = false;

	public static bool greek = false;

	public static bool arabic = false;

	public static bool dutch = false;

	public static bool swedish = false;

	public static SolidColorBrush BookColour_Green = new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)0, (byte)136, (byte)0));

	public static SolidColorBrush BookColour_Red = new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, (byte)0, (byte)0));

	public bool radarClickDelay;

	public DateTime radarClickDelayTime = DateTime.MinValue;

	public bool radarScrollTrigged;

	public bool temp_intro_speech_played;

	public DateTime DelayedSwitchToScene2Time = DateTime.MinValue;

	public int lastScreenWidth = -1;

	public int lastScreenHeight = -1;

	public bool screenModeSet;

	public FullScreenMode lastFullscreenMode = (FullScreenMode)3;

	public DateTime saveWindowSizeChange = DateTime.MinValue;

	public bool firstScreenChange = true;

	public bool noesisHasKeyboard = true;

	public static bool MouseIsUpStroke
	{
		get
		{
			return mouseIsUpStroke;
		}
		set
		{
			mouseIsUpStroke = value;
			if (value)
			{
				HUD_Briefing.mouseIsUpStroke = true;
				HUD_Help.mouseIsUpStroke = true;
			}
		}
	}

	public static bool MouseIsDownStroke
	{
		get
		{
			return mouseIsDownStroke;
		}
		set
		{
			mouseIsDownStroke = value;
			if (value)
			{
				HUD_Briefing.mouseIsDownStroke = true;
				HUD_Help.mouseIsDownStroke = true;
			}
		}
	}

	public bool NoesisHasKeyboard => noesisHasKeyboard;

	public string GetLocale(string filePath)
	{
		try
		{
			string[] array = File.ReadAllLines(filePath);
			if (array.Length != 0)
			{
				return array[0];
			}
		}
		catch (Exception)
		{
		}
		return "";
	}

	public static bool UsesEnglishSpeechFolder()
	{
		if (german || french || spanish || italian || polish || russian || portuguese || hungarian)
		{
			return false;
		}
		return true;
	}

	public void Awake()
	{
		StandaloneWebView.SetChromiumLogLevel((ChromiumLogLevel)5);
		locale = GetLocale("Assets/Text/crusader.txt").ToLowerInvariant();
		Application.runInBackground = true;
		if ((Object)(object)instance == (Object)null)
		{
			instance = this;
		}
		else if ((Object)(object)instance != (Object)(object)this)
		{
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		Object.DontDestroyOnLoad((Object)(object)((Component)this).gameObject);
		MainViewModel.setupImages();
	}

	public void LoadFonts()
	{
		GUI.SetFontFallbacks(Array.Empty<string>());
		List<string> list = new List<string>();
		string[] array;
		switch (locale)
		{
		case "zhcn":
			array = new string[8] { "NotoSansSC-Regular", "JunicodeTwoBeta-Medium", "NotoSansTC-Regular", "NotoSansJP-Regular", "NotoSansArabic-Regular", "NotoSansKR-Regular", "NotoSansThaiLooped-Regular", "NotoSerif-Regular" };
			break;
		case "zhhk":
			array = new string[8] { "NotoSansTC-Regular", "JunicodeTwoBeta-Medium", "NotoSansSC-Regular", "NotoSansJP-Regular", "NotoSansArabic-Regular", "NotoSansKR-Regular", "NotoSansThaiLooped-Regular", "NotoSerif-Regular" };
			break;
		case "jajp":
			array = new string[8] { "NotoSansJP-Regular", "JunicodeTwoBeta-Medium", "NotoSansSC-Regular", "NotoSansTC-Regular", "NotoSansArabic-Regular", "NotoSansKR-Regular", "NotoSansThaiLooped-Regular", "NotoSerif-Regular" };
			break;
		case "ruru":
		case "ukua":
			array = new string[8] { "NotoSerif-Regular", "JunicodeTwoBeta-Medium", "NotoSansArabic-Regular", "NotoSansKR-Regular", "NotoSansSC-Regular", "NotoSansTC-Regular", "NotoSansJP-Regular", "NotoSansThaiLooped-Regular" };
			break;
		case "ar":
			array = new string[8] { "JunicodeTwoBeta-Medium", "NotoSansArabic-Regular", "NotoSansKR-Regular", "NotoSansSC-Regular", "NotoSansTC-Regular", "NotoSansJP-Regular", "NotoSansThaiLooped-Regular", "NotoSerif-Regular" };
			break;
		case "kokr":
			array = new string[8] { "NotoSansKR-Regular", "JunicodeTwoBeta-Medium", "NotoSansSC-Regular", "NotoSansTC-Regular", "NotoSansJP-Regular", "NotoSansArabic-Regular", "NotoSansThaiLooped-Regular", "NotoSerif-Regular" };
			break;
		case "thth":
			array = new string[8] { "JunicodeTwoBeta-Medium", "NotoSansThaiLooped-Regular", "NotoSansSC-Regular", "NotoSansTC-Regular", "NotoSansJP-Regular", "NotoSansArabic-Regular", "NotoSansKR-Regular", "NotoSerif-Regular" };
			break;
		default:
			array = new string[8] { "JunicodeTwoBeta-Medium", "NotoSansSC-Regular", "NotoSansTC-Regular", "NotoSansJP-Regular", "NotoSansArabic-Regular", "NotoSansKR-Regular", "NotoSansThaiLooped-Regular", "NotoSerif-Regular" };
			break;
		}
		switch (locale)
		{
		case "enus":
			english = true;
			break;
		case "dede":
			german = true;
			break;
		case "frfr":
			french = true;
			break;
		case "eses":
			spanish = true;
			break;
		case "itit":
			italian = true;
			break;
		case "plpl":
			polish = true;
			break;
		case "ruru":
			russian = true;
			break;
		case "ptbr":
			portuguese = true;
			break;
		case "jajp":
			japanese = true;
			break;
		case "kokr":
			korean = true;
			break;
		case "zhcn":
			simplified_chinese = true;
			break;
		case "zhhk":
			traditional_chinese = true;
			break;
		case "cscz":
			czech = true;
			break;
		case "trtr":
			turkish = true;
			break;
		case "huhu":
			hungarian = true;
			break;
		case "thth":
			thai = true;
			break;
		case "ukua":
			ukrainian = true;
			break;
		case "elgr":
			greek = true;
			break;
		case "ar":
			arabic = true;
			break;
		case "nlnl":
			dutch = true;
			break;
		case "svse":
			swedish = true;
			break;
		}
		string[] array2 = array;
		foreach (string fontResourceName in array2)
		{
			list.AddRange(addFont(fontResourceName));
		}
		GUI.SetFontFallbacks(list.Distinct().ToArray());
		NoesisSettings.Get().defaultFont = null;
	}

	public List<string> addFont(string fontResourceName)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected O, but got Unknown
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		List<string> newFonts = new List<string>();
		NoesisFont val = (NoesisFont)Resources.Load(fontResourceName);
		NoesisFontProvider.instance.Register(val.uri, val);
		string baseUri = Path.GetDirectoryName(val.uri).Replace('\\', '/');
		using (MemoryStream memoryStream = new MemoryStream(val.content))
		{
			GUI.EnumFontFaces((Stream)memoryStream, (FontFaceInfoCallback)delegate(int idx, string family, FontWeight weight, FontStyle style, FontStretch stretch)
			{
				newFonts.Add(baseUri + "/#" + family);
			});
		}
		return newFonts;
	}

	public void Start()
	{
		ConfigSettings.LoadSettings();
		SFXManager.InitSoundFX();
		SFXManager.instance.playMusic(8);
		AchievementsCommon.Instance.Init();
		string altWindowTitle = "";
		if (locale == "plpl")
		{
			altWindowTitle = "Twierdza\u00a0Krzyżowiec: Edycja Ostateczna";
		}
		MinimumWindowSize.Set(1280, 768, altWindowTitle);
		SceneManager.LoadSceneAsync("SampleScene");
	}

	public void Update()
	{
		if (setupFileList > 0)
		{
			if (MainViewModel.viewModelLoaded)
			{
				setupFileList--;
			}
			if (setupFileList == 2)
			{
				AIVLoader.LoadAIVs();
				MapFileManager.Instance.BuildFileList();
			}
			if (setupFileList == 0)
			{
				EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
				Director.instance.SetEngineFrameRate(ConfigSettings.Settings_GameSpeed);
				if (ConfigSettings.Settings_GenieSpeech)
				{
					EngineInterface.GameAction(Enums.GameActionCommand.GenieSpeech, 1, 1);
				}
				else
				{
					EngineInterface.GameAction(Enums.GameActionCommand.GenieSpeech, 0, 0);
				}
				EngineInterface.SetTrailTimes(0, ConfigSettings.Settings_Trail1Times);
				EngineInterface.SetTrailTimes(1, ConfigSettings.Settings_Trail2Times);
				EngineInterface.SetTrailTimes(2, ConfigSettings.Settings_Trail3Times);
				EngineInterface.SetTrailTimes(11, ConfigSettings.Settings_Trail_Sands1_Times);
				EngineInterface.SetTrailTimes(12, ConfigSettings.Settings_Trail_Sands2_Times);
				EngineInterface.SetTrailTimes(13, ConfigSettings.Settings_Trail_Sands3_Times);
				EngineInterface.SetTrailTimes(14, ConfigSettings.Settings_Trail_Sands4_Times);
				EngineInterface.SetTrailTimes(15, ConfigSettings.Settings_Trail_Sands5_Times);
				EngineInterface.SetTrailTimes(16, ConfigSettings.Settings_Trail_Sands6_Times);
				EngineInterface.SetTrailTimes(17, ConfigSettings.Settings_Trail_Sands7_Times);
				EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
				if (ConfigSettings.Settings_ShowPlannedMoat)
				{
					EngineInterface.GameAction(Enums.GameActionCommand.ShowPlannedMoat, 1, 1);
				}
				MainViewModel.Instance.Show_LeaderboardOptIn = !ConfigSettings.Settings_HideSoTTiming;
				Director.instance.setCursor(0, force: true);
				if (Platform_Multiplayer.Instance.PendingMPLobby && Platform_Multiplayer.Instance.PendingMPLobby_DelayedMPEnter)
				{
					SFXManager.instance.init2();
					Avatars.InitAvatars();
					Platform_Multiplayer.Instance.PendingMPLobby_DelayedMPEnter = false;
					MainViewModel.Instance.FrontEndMenu.ButtonClicked("Multiplayer");
				}
			}
		}
		else if (!MapFileManager.Instance.fileListComplete && MapFileManager.Instance.fileListLoaded && CustomisationFileManager.Instance.CustomisationLoaded)
		{
			MapFileManager.Instance.fileListComplete = true;
			if (MapFileManager.Instance.debugOutput.Length > 0)
			{
				Debug.Log((object)MapFileManager.Instance.debugOutput.ToString());
			}
			if (CustomisationFileManager.Instance.debugOutput.Length > 0)
			{
				Debug.Log((object)CustomisationFileManager.Instance.debugOutput.ToString());
			}
		}
		MonitorScreenResolutions();
		mouseIsDown = Input.GetMouseButton(0);
		if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.app_mode == 14 && MainViewModel.Instance.Show_HUD_Briefing)
		{
			MainViewModel.Instance.HUDBriefingPanel.Update();
		}
		if (MainViewModel.viewModelLoaded)
		{
			if (!FrontendMenus.newsletterSignUp)
			{
				FrontendMenus.MonitorNewsLetter();
			}
			if (MainViewModel.Instance.Show_HUD_Help)
			{
				MainViewModel.Instance.HUDHelp.Update();
			}
			if (MainViewModel.Instance.Show_HUD_RightClick)
			{
				MainViewModel.Instance.HUDRightClick.Update();
			}
			if (MainViewModel.Instance.Show_HUD_ControlGroups)
			{
				MainViewModel.Instance.HUDControlGroups.Update();
			}
			if (MainViewModel.Instance.Show_HUD_MissionOver)
			{
				MainViewModel.Instance.HUDMissionOver.Update();
			}
			if (MainViewModel.Instance.AlliesPanelVisible)
			{
				MainViewModel.Instance.HUDAlliesPanel.Update();
			}
			if (MainViewModel.Instance.MeritPanelVisible)
			{
				MainViewModel.Instance.HUDMeritPanel.Update();
			}
			if (MainViewModel.Instance.Show_HUD_LoadSaveRequester || MainViewModel.Instance.Show_HUD_LoadSaveRequesterMP || MainViewModel.Instance.Show_HUD_LoadSaveRequesterTrails)
			{
				MainViewModel.Instance.HUDLoadSaveRequester.Update();
			}
			if (MainViewModel.Instance.Show_StandaloneSetup)
			{
				MainViewModel.Instance.FRONTStandaloneMission.Update();
			}
			if (MainViewModel.Instance.Show_MultiplayerSetup)
			{
				MainViewModel.Instance.FRONTMultiplayer.Update();
			}
			if (MainViewModel.Instance.Show_HUD_Scenario || MainViewModel.Instance.Show_HUD_Scenario_Button)
			{
				MainViewModel.Instance.HUDScenarioPopup.Update();
			}
			if (MainViewModel.Instance.Show_HUD_FreebuildMenu)
			{
				MainViewModel.Instance.HUDFreebuildMenu.Update();
			}
			if (MainViewModel.Instance.Show_Credits)
			{
				MainViewModel.Instance.FRONTCredits.Update();
			}
			if (MainViewModel.Instance.Show_IntroSequence)
			{
				MainViewModel.Instance.Intro_Sequence.Update();
			}
			if (MainViewModel.Instance.Show_Historical1CampaignMenu)
			{
				FRONT_Extra1Campaign.Update();
			}
			if (MainViewModel.Instance.Show_Historical2CampaignMenu)
			{
				FRONT_Extra2Campaign.Update();
			}
			if (MainViewModel.Instance.Show_Historical3CampaignMenu)
			{
				FRONT_Extra3Campaign.Update();
			}
			if (MainViewModel.Instance.Show_Historical4CampaignMenu)
			{
				FRONT_Extra4Campaign.Update();
			}
			if (MainViewModel.Instance.Show_TrailCampaignMenu)
			{
				FRONT_Trail.Update();
			}
			if (MainViewModel.Instance.Show_Leaderboard)
			{
				HUD_Leaderboard.Instance.Update();
			}
			if (MainViewModel.Instance.Show_Story)
			{
				MainViewModel.Instance.FRONTStory.Update();
			}
			if (Director.instance.MultiplayerGame)
			{
				MainViewModel.Instance.HUDMPResync.Update();
			}
			MainViewModel.Instance.CrossThreadRolloverUpdate();
			OnScreenText.Instance.Update();
			MainViewModel.Instance.FrontEndMenu.Update();
			int num = -1;
			string text;
			if (Director.instance.SimRunning)
			{
				SFXManager.instance.Update();
				text = "#StatusTrail_Classic";
				if (MainViewModel.Instance.IsMapEditorMode)
				{
					text = "#StatusMapEditor";
				}
				else if (!GameData.Instance.multiplayerMap)
				{
					if (GameData.Instance.game_type == 0)
					{
						switch ((GameData.Instance.mission_level - 1) / 5)
						{
						case 0:
							text = "#StatusHist_1";
							break;
						case 1:
							text = "#StatusHist_2";
							break;
						case 2:
							text = "#StatusHist_3";
							break;
						case 3:
							text = "#StatusHist_4";
							break;
						case 4:
							text = "#StatusHist_5";
							break;
						case 5:
							text = "#StatusHist_6";
							break;
						case 6:
							text = "#StatusHist_7";
							break;
						}
						num = (GameData.Instance.mission_level - 1) % 5 + 1;
					}
					else
					{
						switch (GameData.Instance.mapType)
						{
						case Enums.GameModes.INVASION:
							text = "#StatusCustomScenario";
							break;
						case Enums.GameModes.BUILD:
							text = "#StatusFreebuild";
							break;
						}
					}
				}
				else if (GameData.Instance.game_type == 3 && Director.instance.SkirmishModeGame)
				{
					if (GameData.Instance.SkirmishGameType == 3)
					{
						text = "#StatusCustom_Skirmish";
					}
					else if (GameData.Instance.coopTrailID > 0)
					{
						if (GameData.Instance.coopTrailID == 1)
						{
							text = "#StatusCoop_1";
						}
						else if (GameData.Instance.coopTrailID == 2)
						{
							text = "#StatusCoop_2";
						}
						else if (GameData.Instance.coopTrailID == 3)
						{
							text = "#StatusCoop_3";
						}
						num = GameData.Instance.coopMissionID + 1;
					}
					else if (GameData.Instance.SkirmishGameType == 1)
					{
						switch (GameData.Instance.SkirmishTrailType)
						{
						case 0:
							text = "#StatusTrail_Classic";
							break;
						case 1:
							text = "#StatusTrail_Warchest";
							break;
						case 2:
							text = "#StatusTrail_Extreme";
							break;
						case 11:
							text = "#StatusSoT_1";
							break;
						case 12:
							text = "#StatusSoT_2";
							break;
						case 13:
							text = "#StatusSoT_3";
							break;
						case 14:
							text = "#StatusSoT_4";
							break;
						case 15:
							text = "#StatusSoT_5";
							break;
						case 16:
							text = "#StatusSoT_6";
							break;
						case 17:
							text = "#StatusSoT_7";
							break;
						}
						num = GameData.Instance.SkirmishTrailLevel + 1;
					}
					else if (GameData.Instance.SkirmishGameType == 2)
					{
						text = "#StatusCustom_Trail";
						num = MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrailLevel;
					}
					else
					{
						text = "#StatusCustom_Skirmish";
					}
				}
				else
				{
					text = "#StatusMP";
				}
			}
			else
			{
				text = "#StatusMenu";
			}
			if (text != last_richPresenceString || last_richPresenceMissionID != num)
			{
				last_richPresenceString = text;
				last_richPresenceMissionID = num;
				SteamFriends.SetRichPresence("steam_display", text);
				SteamFriends.SetRichPresence("mission", num.ToString());
			}
			Platform_Achievements.Instance.monitorStats();
		}
		RadarScrollMap();
	}

	public void NoesisGUIUpdateChecksComplete()
	{
		if (MainViewModel.viewModelLoaded && MainViewModel.Instance.Show_HUD_Options && (BaseComponent)(object)MainViewModel.Instance.HUDOptions != (BaseComponent)null)
		{
			MainViewModel.Instance.HUDOptions.Update();
		}
	}

	public void NoesisGUIUpdateChecksInGame()
	{
		//IL_14d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_14db: Invalid comparison between Unknown and I4
		//IL_4c61: Unknown result type (might be due to invalid IL or missing references)
		//IL_4c66: Unknown result type (might be due to invalid IL or missing references)
		//IL_4e38: Unknown result type (might be due to invalid IL or missing references)
		//IL_4e3d: Unknown result type (might be due to invalid IL or missing references)
		//IL_4f32: Unknown result type (might be due to invalid IL or missing references)
		//IL_4f37: Unknown result type (might be due to invalid IL or missing references)
		//IL_502c: Unknown result type (might be due to invalid IL or missing references)
		//IL_5031: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		bool isEnabled = true;
		bool isEnabled2 = true;
		string line = "";
		string line2 = "";
		double buildingTitleFontSize = 32.0;
		EngineInterface.ScenarioOverview scenarioOverview = null;
		MainViewModel.Instance.IngameUI.findUIlowerPoint();
		if (GameData.Instance.lastGameState == null)
		{
			MouseIsUpStroke = false;
			MouseIsDownStroke = false;
			return;
		}
		MainViewModel.Instance.UpdateSHGoodsData();
		MainViewModel.Instance.UpdateSHTroopsData();
		MainViewModel.Instance.Show_AlliesPip = MainViewModel.Instance.HUDAlliesPanel.ShowAlliesPip();
		MainViewModel.Instance.AvailablePeasantText = GameData.Instance.lastGameState.peasants_available_for_troops.ToString();
		MainViewModel.Instance.BarracksHorsesAvailableText = GameData.Instance.lastGameState.total_horses_available.ToString();
		int popularity = GameData.Instance.lastGameState.popularity;
		MainViewModel.Instance.BookPopularityText = popularity.ToString();
		if (GameData.Instance.lastGameState.gold > 99999)
		{
			MainViewModel.Instance.BookGoldText = "99999+";
		}
		else
		{
			MainViewModel.Instance.BookGoldText = GameData.Instance.lastGameState.gold.ToString();
		}
		int num2 = GameData.Instance.lastGameState.population;
		if (GameData.Instance.lastGameState.housing_cap == 0)
		{
			num2 = 0;
		}
		MainViewModel.Instance.BookPopulationText = num2 + "/" + GameData.Instance.lastGameState.housing_cap;
		if (GameData.Instance.lastGameState.overcrowding_popularity == 0)
		{
			MainViewModel.Instance.BookPopulationColour = BookColour_Green;
		}
		else
		{
			MainViewModel.Instance.BookPopulationColour = BookColour_Red;
		}
		if (popularity >= 50)
		{
			MainViewModel.Instance.BookPopularityColour = BookColour_Green;
			MainViewModel.Instance.HUDRoot.setPulsing(0);
		}
		else
		{
			MainViewModel.Instance.BookPopularityColour = BookColour_Red;
			if (popularity <= 20)
			{
				MainViewModel.Instance.HUDRoot.setPulsing(1000);
			}
			else if (popularity <= 30 && lastPopularity > 30)
			{
				MainViewModel.Instance.HUDRoot.setPulsing(3);
			}
			else if (popularity <= 40 && lastPopularity > 40)
			{
				MainViewModel.Instance.HUDRoot.setPulsing(3);
			}
			else if (popularity <= 49 && lastPopularity > 49)
			{
				MainViewModel.Instance.HUDRoot.setPulsing(3);
			}
			else
			{
				MainViewModel.Instance.HUDRoot.setPulsing(-1);
			}
		}
		lastPopularity = popularity;
		if (GameData.Instance.lastGameState.upcoming_total_popularity > 0)
		{
			MainViewModel.Instance.PopularityIncreasingVis = true;
			MainViewModel.Instance.PopularityDecreasingVis = false;
		}
		else if (GameData.Instance.lastGameState.upcoming_total_popularity < 0)
		{
			MainViewModel.Instance.PopularityIncreasingVis = false;
			MainViewModel.Instance.PopularityDecreasingVis = true;
		}
		else
		{
			MainViewModel.Instance.PopularityIncreasingVis = false;
			MainViewModel.Instance.PopularityDecreasingVis = false;
		}
		if (GameData.Instance.lastGameState.gold > 9999)
		{
			MainViewModel.Instance.BookGoldLarge = false;
			MainViewModel.Instance.BookGoldSmall = true;
		}
		else
		{
			MainViewModel.Instance.BookGoldLarge = true;
			MainViewModel.Instance.BookGoldSmall = false;
		}
		bool flag = true;
		if (!MainViewModel.Instance.AllStoredGoodsVisible[2])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[3])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[4])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[6])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[7] && !MainViewModel.Instance.AllStoredGoodsVisible[8])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[9])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[10])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[11])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[12])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[13])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[14])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[16])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[17])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[18])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[19])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[20])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[21])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[22])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[23])
		{
			flag = false;
		}
		else if (!MainViewModel.Instance.AllStoredGoodsVisible[24])
		{
			flag = false;
		}
		if ((GameData.Instance.lastGameState.gotMarket & 1) > 0 && !flag)
		{
			if (MainViewModel.Instance.ShowAllHudVis)
			{
				MainViewModel.Instance.GotMarketVis = true;
				MainViewModel.Instance.GotMarketVis_Selected = false;
			}
			else
			{
				MainViewModel.Instance.GotMarketVis = false;
				MainViewModel.Instance.GotMarketVis_Selected = true;
			}
		}
		else
		{
			MainViewModel.Instance.GotMarketVis = false;
			MainViewModel.Instance.GotMarketVis_Selected = false;
		}
		MainViewModel.Instance.Show_HUD_Goods_Button_Disabled = (GameData.Instance.lastGameState.gotMarket & 2) == 0;
		if (MainViewModel.Instance.Show_HUD_Goods_Button_Disabled && MainViewModel.Instance.Show_HUD_Goods)
		{
			MainViewModel.Instance.SetGoodsPopupState(visible: false);
		}
		MainViewModel.Instance.HUDmain.SetEnginePanelText(GameData.Instance.lastGameState.panel_text_group, GameData.Instance.lastGameState.panel_text_text);
		bool flag2 = false;
		if (GameData.Instance.lastGameState.undoAvailable > 0)
		{
			flag2 = ((GameData.Instance.lastGameState.app_mode != 14 || GameData.Instance.lastGameState.app_sub_mode != 49) ? true : false);
		}
		if (((UIElement)MainViewModel.Instance.HUDmain.RefGameUndoButton).IsEnabled != flag2)
		{
			((UIElement)MainViewModel.Instance.HUDmain.RefGameUndoButton).IsEnabled = flag2;
		}
		if ((BaseComponent)(object)MainViewModel.Instance.HUDmain != (BaseComponent)null)
		{
			MainViewModel.Instance.HUDmain.UpdateRollover();
		}
		if (GameData.Instance.lastGameState.peasants_available_for_troops <= 0)
		{
			isEnabled = false;
		}
		if (GameData.Instance.lastGameState.peasants_available_for_troops <= 1)
		{
			isEnabled2 = false;
		}
		if (addChimpActions(GameData.Instance.lastGameState, ref line, ref line2, islamic: false))
		{
			MainViewModel.Instance.BuildingLine1Text = line;
			MainViewModel.Instance.BuildingLine2Text = line2;
		}
		if (GameData.Instance.lastGameState.building_maxhps_for_repair > 0)
		{
			MainViewModel.Instance.BuildingHPText = GameData.Instance.lastGameState.building_hps_for_repair + "/" + GameData.Instance.lastGameState.building_maxhps_for_repair;
			MainViewModel.Instance.BuildingRepairHPWidth = 60 * GameData.Instance.lastGameState.building_hps_for_repair / GameData.Instance.lastGameState.building_maxhps_for_repair;
			((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefButtonRepair).IsEnabled = GameData.Instance.lastGameState.building_hps_for_repair != GameData.Instance.lastGameState.building_maxhps_for_repair && GameData.Instance.lastGameState.can_do_repairs > 0;
		}
		if (ConfigSettings.Settings_Scribe == 0)
		{
			if (!MainViewModel.Instance.IsMapEditorMode)
			{
				MainViewModel.Instance.ScribeHeadImage = MainViewModel.Instance.GameSprites[296 + GameData.Instance.lastGameState.scribe_frame - 1];
			}
			else
			{
				MainViewModel.Instance.ScribeHeadImage = MainViewModel.Instance.GameSprites[296];
			}
		}
		else if (ConfigSettings.Settings_Scribe == 2)
		{
			if (!MainViewModel.Instance.IsMapEditorMode)
			{
				MainViewModel.Instance.ScribeHeadImage = MainViewModel.Instance.GameSprites[24 + GameData.Instance.lastGameState.scribe_frame - 1];
			}
			else
			{
				MainViewModel.Instance.ScribeHeadImage = MainViewModel.Instance.GameSprites[24];
			}
		}
		if (MainViewModel.Instance.Show_HUD_Objectives && !MainViewModel.Instance.Show_HUD_Briefing && GameData.scenario != null)
		{
			List<GameData.ScenarioEvent> events = GameData.scenario.getEvents();
			int startDate = 0;
			int nowDate = 0;
			int endDate = 0;
			int num3 = 0;
			string winTimer = GameData.scenario.getWinTimer(ref startDate, ref nowDate, ref endDate);
			if (winTimer == null)
			{
				((UIElement)MainViewModel.Instance.HUDObjectives.RefObjectiveTimer).Visibility = (Visibility)0;
			}
			else
			{
				MainViewModel.Instance.ObjectiveTimerText = winTimer;
				((UIElement)MainViewModel.Instance.HUDObjectives.RefObjectiveTimer).Visibility = (Visibility)2;
				long num4 = nowDate - startDate;
				long num5 = endDate - startDate;
				if (num4 > num5)
				{
					num4 = num5;
				}
				if (num5 > 0)
				{
					MainViewModel.Instance.ObjectiveTimerWidth = (int)(200 * num4 / num5);
				}
				num3 += 2;
			}
			num3 += UpdateObjectiveRows(events, MainViewModel.Instance.HUDObjectives.RefWGTObjectives);
			MainViewModel.Instance.HUDObjectives.SetSizeFromRows(num3);
		}
		if (MainViewModel.Instance.ScenarioEditorMode > Enums.ScenarioViews.Blank && GameData.Instance.scenarioOverview != null)
		{
			scenarioOverview = GameData.Instance.scenarioOverview;
			MainViewModel.Instance.ScenarioStartingMonthText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, scenarioOverview.startMonth);
			MainViewModel.Instance.ScenarioStartingPopText = scenarioOverview.scenario_start_popularity.ToString();
			int num6 = int.Parse(MainViewModel.Instance.ScenarioStartingYearText, Director.defaultCulture);
			if (num6 != scenarioOverview.startYear)
			{
				scenarioOverview.startYear = num6;
				EngineInterface.GameAction(Enums.GameActionCommand.Scenario_Set_Starting_Year, 0, scenarioOverview.startYear);
			}
			MainViewModel.Instance.SetStartingSpecial(scenarioOverview.special_start > 0);
			MainViewModel.Instance.ScenarioStartingSpecialGoldText = scenarioOverview.special_start_gold.ToString();
			string scenarioStartingSpecialRationsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 3);
			switch (scenarioOverview.special_start_rationing)
			{
			case 1:
				scenarioStartingSpecialRationsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 4);
				break;
			case 2:
				scenarioStartingSpecialRationsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 5);
				break;
			case 3:
				scenarioStartingSpecialRationsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 6);
				break;
			case 4:
				scenarioStartingSpecialRationsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 12);
				break;
			}
			MainViewModel.Instance.ScenarioStartingSpecialRationsText = scenarioStartingSpecialRationsText;
			MainViewModel.Instance.ScenarioStartingSpecialTaxText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_KEEP, scenarioOverview.special_start_tax_rate + 7);
			MainViewModel.Instance.ScenarioStartingGoldText = scenarioOverview.scenario_start_goods[15].ToString();
			MainViewModel.Instance.ScenarioStartingPitchText = scenarioOverview.scenario_start_goods[8].ToString();
			ScenarioEditorUpdateNewEventButtons();
			if (MainViewModel.Instance.ScenarioEditorMode != Enums.ScenarioViews.Main)
			{
				if (MainViewModel.Instance.ScenarioEditorMode == Enums.ScenarioViews.StartingGoods)
				{
					MainViewModel.Instance.ScenarioAdjustedStartingGoodsText = GameData.Instance.scenarioOverview.scenario_start_goods[MainViewModel.Instance.ScenarioStartingGoodsType] + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, MainViewModel.Instance.ScenarioStartingGoodsType);
					for (int i = 0; i <= 24; i++)
					{
						MainViewModel.Instance.StartingGoods[i] = GameData.Instance.scenarioOverview.scenario_start_goods[i];
					}
				}
				else if (MainViewModel.Instance.ScenarioEditorMode == Enums.ScenarioViews.TradedGoods)
				{
					string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_ON);
					string text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_OFF);
					for (int j = 0; j <= 24; j++)
					{
						if (GameData.Instance.scenarioOverview.scenario_trader_goods_available[j] > 0)
						{
							if (MainViewModel.Instance.TradingGoods[j] != text)
							{
								MainViewModel.Instance.TradingGoods[j] = text;
								MainViewModel.Instance.TradingGoodsBool[j] = true;
							}
						}
						else if (MainViewModel.Instance.TradingGoods[j] != text2)
						{
							MainViewModel.Instance.TradingGoods[j] = text2;
							MainViewModel.Instance.TradingGoodsBool[j] = false;
						}
					}
				}
				else if (MainViewModel.Instance.ScenarioEditorMode == Enums.ScenarioViews.Invasions)
				{
					MainViewModel.Instance.HUDScenario.PopulateInvasion(fromUpdate: true);
				}
				else if (MainViewModel.Instance.ScenarioEditorMode == Enums.ScenarioViews.Events)
				{
					MainViewModel.Instance.HUDScenario.PopulateEvent(fromUpdate: true);
				}
				else if (MainViewModel.Instance.ScenarioEditorMode == Enums.ScenarioViews.EventsActions)
				{
					MainViewModel.Instance.HUDScenario.PopulateEventActions(fromUpdate: true);
				}
				else if (MainViewModel.Instance.ScenarioEditorMode == Enums.ScenarioViews.EventsConditions)
				{
					MainViewModel.Instance.HUDScenario.PopulateEventConditions(fromUpdate: true);
				}
			}
		}
		if (GameData.Instance.lastGameState.app_mode == 14 && MainViewModel.Instance.Show_HUD_Briefing)
		{
			BriefingUIUpdate();
		}
		if (!MainViewModel.Instance.Show_HUD_Briefing)
		{
			if (SFXManager.instance.requestBinkPlayState < 0 && SFXManager.instance.binkIsPlaying)
			{
				int requestBinkPlayState = -SFXManager.instance.requestBinkPlayState;
				MainViewModel.Instance.HUDRoot.RadarME_Ended();
				MainViewModel.Instance.HUDRoot.RefRadarME.Volume = ConfigSettings.Settings_SFXVolume * MyAudioManager.GetMasterVolume() * SFXManager.instance.binkVolume;
				MainViewModel.Instance.HUDRoot.RefRadarME.Source = SFXManager.instance.requestBinkPlaybackURI;
				binkPlayWait = true;
				binkPlayDelay = DateTime.UtcNow;
				SFXManager.instance.requestBinkPlayState = requestBinkPlayState;
			}
			else if (SFXManager.instance.requestBinkPlayState > 0 && !SFXManager.instance.binkIsPlaying)
			{
				if (!binkPlayWait)
				{
					if (MainViewModel.Instance.HUDRoot.RefRadarME.Source == SFXManager.instance.requestBinkPlaybackURI)
					{
						MainViewModel.Instance.HUDRoot.RefRadarME.Volume = ConfigSettings.Settings_SFXVolume * MyAudioManager.GetMasterVolume() * SFXManager.instance.binkVolume;
						MainViewModel.Instance.HUDRoot.RefRadarME.Play();
						((UIElement)MainViewModel.Instance.HUDRoot.RefRadarME).Opacity = 1f;
						SFXManager.instance.binkIsPlaying = true;
						binkPaused = false;
					}
					else
					{
						MainViewModel.Instance.HUDRoot.RefRadarME.Volume = ConfigSettings.Settings_SFXVolume * MyAudioManager.GetMasterVolume() * SFXManager.instance.binkVolume;
						MainViewModel.Instance.HUDRoot.RefRadarME.Source = SFXManager.instance.requestBinkPlaybackURI;
						binkPlayWait = false;
						MainViewModel.Instance.HUDRoot.RefRadarME.Play();
						((UIElement)MainViewModel.Instance.HUDRoot.RefRadarME).Opacity = 1f;
						SFXManager.instance.binkIsPlaying = true;
						binkPaused = false;
					}
				}
				else if ((DateTime.UtcNow - binkPlayDelay).TotalMilliseconds > 20.0)
				{
					binkPlayWait = false;
					MainViewModel.Instance.HUDRoot.RefRadarME.Play();
					MainViewModel.Instance.HUDRoot.RefRadarME.Volume = ConfigSettings.Settings_SFXVolume * MyAudioManager.GetMasterVolume() * SFXManager.instance.binkVolume;
					((UIElement)MainViewModel.Instance.HUDRoot.RefRadarME).Opacity = 1f;
					SFXManager.instance.binkIsPlaying = true;
					binkPaused = false;
				}
			}
			else if (SFXManager.instance.requestBinkPlayState == 0 && SFXManager.instance.binkIsPlaying)
			{
				MainViewModel.Instance.HUDRoot.RadarME_Ended();
			}
			else if (SFXManager.instance.requestBinkPlayState == 3 && SFXManager.instance.binkIsPlaying && !MyAudioManager.Instance.isSpeechPlaying(1))
			{
				MainViewModel.Instance.HUDRoot.RadarME_Ended();
			}
		}
		if (SFXManager.instance.binkIsPlaying)
		{
			if (MainViewModel.Instance.Show_HUD_Briefing)
			{
				if (!binkPaused)
				{
					MainViewModel.Instance.HUDRoot.RefRadarME.Pause();
					binkPaused = true;
				}
			}
			else if (binkPaused)
			{
				MainViewModel.Instance.HUDRoot.RefRadarME.Play();
				binkPaused = false;
			}
		}
		bool show_ActionPoint = false;
		bool show_KeepEnclosed = false;
		byte b = 0;
		if (MainViewModel.Instance.UIVisible && !MainViewModel.Instance.IsMapEditorMode && Director.instance.SimRunning)
		{
			if (GameData.Instance.lastGameState.action_point_count > 0)
			{
				show_ActionPoint = true;
			}
			show_KeepEnclosed = GameData.Instance.lastGameState.keep_enclosed > 0;
			b = GameData.Instance.lastGameState.messageFrom;
		}
		MainViewModel.Instance.Show_ActionPoint = show_ActionPoint;
		MainViewModel.Instance.Show_KeepEnclosed = show_KeepEnclosed;
		MainViewModel.Instance.Show_MessageShield = b > 0;
		if (b != 0)
		{
			MainViewModel.Instance.MessageShieldImage = MainViewModel.Instance.getTeamShield(b);
		}
		if (GameData.Instance.lastGameState.app_mode == 14 && (BaseComponent)(object)MainViewModel.Instance.HUDmain != (BaseComponent)null)
		{
			Enums.ForcedAppModes force_app_mode = (Enums.ForcedAppModes)GameData.Instance.lastGameState.force_app_mode;
			GameData.Instance.lastGameState.force_app_mode = 0;
			if (EditorDirector.instance.ActivePlayerID <= 0)
			{
				MainViewModel.Instance.buildControlsFreeze(Mode: true);
				MainViewModel.Instance.HUDmain.NewBuildScreenBlank();
			}
			else if (GameData.Instance.game_type == 2 && GameData.Instance.mapType == Enums.GameModes.SIEGE && GameData.Instance.playerID == 2)
			{
				if ((GameData.Instance.app_sub_mode != 20 && GameData.Instance.app_sub_mode != 48 && GameData.Instance.app_sub_mode != 61) || force_app_mode == Enums.ForcedAppModes.refresh_current || !MainViewModel.Instance.FreezeMainControls)
				{
					MainViewModel.Instance.buildControlsFreeze(Mode: true);
					MainViewModel.Instance.HUDmain.NewBuildScreenBlank();
				}
			}
			else if ((GameData.Instance.game_type == 0 && GameData.Instance.app_sub_mode == 48) || force_app_mode == Enums.ForcedAppModes.blank)
			{
				MainViewModel.Instance.buildControlsFreeze(Mode: true);
				MainViewModel.Instance.HUDmain.NewBuildScreenBlank();
			}
			else
			{
				switch (force_app_mode)
				{
				case Enums.ForcedAppModes.keeps:
					MainViewModel.Instance.buildControlsFreeze(Mode: true);
					MainViewModel.Instance.HUDmain.NewBuildScreenKeeps();
					break;
				case Enums.ForcedAppModes.granary:
					MainViewModel.Instance.buildControlsFreeze(Mode: true);
					MainViewModel.Instance.HUDmain.NewBuildScreenFood(updateAppMode: false);
					break;
				case Enums.ForcedAppModes.refresh_current:
					MainViewModel.Instance.buildControlsFreeze(Mode: false);
					if (GameData.Instance.game_type == 4 || GameData.Instance.game_type == 1)
					{
						MainViewModel.Instance.HUDmain.NewBuildScreenCastle(force: true);
						((ToggleButton)MainViewModel.Instance.HUDmain.RefTabBuildCastle).IsChecked = true;
					}
					else if (GameData.Instance.game_type == 6)
					{
						MainViewModel.Instance.HUDmain.NewBuildScreenKeeps();
						((ToggleButton)MainViewModel.Instance.HUDmain.RefTabBuildCastle).IsChecked = true;
					}
					else if ((Director.instance.SkirmishModeGame || Director.instance.MultiplayerGame) && (GameData.Instance.lastGameState.gotMarket & 4) == 0)
					{
						MainViewModel.Instance.HUDmain.NewBuildScreenFood();
						((ToggleButton)MainViewModel.Instance.HUDmain.RefTabBuildFood).IsChecked = true;
					}
					else
					{
						MainViewModel.Instance.HUDmain.NewBuildScreenIndustry(force: true);
						((ToggleButton)MainViewModel.Instance.HUDmain.RefTabBuildIndustry).IsChecked = true;
					}
					break;
				case Enums.ForcedAppModes.castle:
					MainViewModel.Instance.buildControlsFreeze(Mode: false);
					if (GameData.Instance.lastGameState.app_sub_mode != 61 && GameData.Instance.lastGameState.app_sub_mode != 62)
					{
						if (GameData.Instance.game_type == 4 || GameData.Instance.game_type == 6)
						{
							MainViewModel.Instance.HUDmain.NewBuildScreenCastle();
							((ToggleButton)MainViewModel.Instance.HUDmain.RefTabBuildCastle).IsChecked = true;
						}
						else
						{
							MainViewModel.Instance.HUDmain.NewBuildScreenIndustry();
							((ToggleButton)MainViewModel.Instance.HUDmain.RefTabBuildIndustry).IsChecked = true;
						}
					}
					break;
				}
			}
		}
		if (GameData.Instance.lastGameState.app_mode != 16 && GameData.Instance.lastGameState.app_sub_mode != 57 && (int)((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefTradePost_Trade_Auto).Visibility == 2)
		{
			EngineInterface.GameAction(Enums.GameActionCommand.Autotrade_Apply, 0, 0);
			EngineInterface.GameAction(Enums.GameActionCommand.Autotrade_Pause, 0, 0);
			((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefTradePost_Trade_Normal).Visibility = (Visibility)2;
			((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefTradePost_Trade_Auto).Visibility = (Visibility)1;
		}
		if (GameData.Instance.lastGameState.app_mode == 14 && (GameData.Instance.lastGameState.app_sub_mode == 61 || GameData.Instance.lastGameState.app_sub_mode == 62))
		{
			switch ((int)GameData.Instance.lastGameState.troops_show_stance)
			{
			case 0:
				MainViewModel.Instance.GuardStanceActive = true;
				MainViewModel.Instance.DefensiveStanceActive = false;
				MainViewModel.Instance.AggressiveStanceActive = false;
				break;
			case 1:
				MainViewModel.Instance.DefensiveStanceActive = true;
				MainViewModel.Instance.GuardStanceActive = false;
				MainViewModel.Instance.AggressiveStanceActive = false;
				break;
			case 2:
				MainViewModel.Instance.AggressiveStanceActive = true;
				MainViewModel.Instance.GuardStanceActive = false;
				MainViewModel.Instance.DefensiveStanceActive = false;
				break;
			}
			MainViewModel.Instance.HUDTroopPanel.ShowAmmoOrders();
			if (MainViewModel.Instance.HUDTroopPanel.PatrolShouldBeVisible)
			{
				if (GameData.Instance.lastGameState.troops_patrol_mode != 0)
				{
					((UIElement)MainViewModel.Instance.HUDTroopPanel.RefUnitPatrol).Visibility = (Visibility)1;
					((UIElement)MainViewModel.Instance.HUDTroopPanel.RefUnitPatrolActive).Visibility = (Visibility)2;
				}
				else
				{
					((UIElement)MainViewModel.Instance.HUDTroopPanel.RefUnitPatrol).Visibility = (Visibility)2;
					((UIElement)MainViewModel.Instance.HUDTroopPanel.RefUnitPatrolActive).Visibility = (Visibility)1;
				}
			}
		}
		if (GameData.Instance.lastGameState.app_mode == 16)
		{
			if (GameData.Instance.lastGameState.app_sub_mode == 1 || GameData.Instance.lastGameState.app_sub_mode == 44 || GameData.Instance.lastGameState.app_sub_mode == 23 || GameData.Instance.lastGameState.app_sub_mode == 24 || GameData.Instance.lastGameState.app_sub_mode == 63 || GameData.Instance.lastGameState.app_sub_mode == 96)
			{
				int num7 = GameData.Instance.lastGameState.resources[15];
				int num8 = 1;
				if (MainViewModel.Instance.lastTroopBuildOver.Length > 0 && MainViewModel.Instance.lastTroopBuildChimp != Enums.eChimps.CHIMP_NUM_TYPES)
				{
					num8 = 1000;
					if (num8 > 1)
					{
						if (MainViewModel.Instance.lastTroopBuildChimp == Enums.eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL)
						{
							if (num8 > GameData.Instance.lastGameState.peasants_available_for_troops / 2)
							{
								num8 = GameData.Instance.lastGameState.peasants_available_for_troops / 2;
							}
						}
						else if (num8 > GameData.Instance.lastGameState.peasants_available_for_troops)
						{
							num8 = GameData.Instance.lastGameState.peasants_available_for_troops;
						}
						int chimpGoldCost = GameData.getChimpGoldCost((int)MainViewModel.Instance.lastTroopBuildChimp);
						if (chimpGoldCost > 0)
						{
							int num9 = num7 / chimpGoldCost;
							if (num9 < num8)
							{
								num8 = num9;
							}
						}
						if (num8 > 1)
						{
							int num10 = 10000;
							int num11 = 10000;
							int num12 = 10000;
							switch (MainViewModel.Instance.lastTroopBuildChimp)
							{
							case Enums.eChimps.CHIMP_TYPE_ARCHER:
								num10 = GameData.Instance.lastGameState.resources[17];
								break;
							case Enums.eChimps.CHIMP_TYPE_SPEARMAN:
								num10 = GameData.Instance.lastGameState.resources[19];
								break;
							case Enums.eChimps.CHIMP_TYPE_MACEMAN:
								num10 = GameData.Instance.lastGameState.resources[21];
								num11 = GameData.Instance.lastGameState.resources[23];
								break;
							case Enums.eChimps.CHIMP_TYPE_XBOWMAN:
								num10 = GameData.Instance.lastGameState.resources[18];
								num11 = GameData.Instance.lastGameState.resources[23];
								break;
							case Enums.eChimps.CHIMP_TYPE_PIKEMAN:
								num10 = GameData.Instance.lastGameState.resources[20];
								num11 = GameData.Instance.lastGameState.resources[24];
								break;
							case Enums.eChimps.CHIMP_TYPE_SWORDSMAN:
								num10 = GameData.Instance.lastGameState.resources[22];
								num11 = GameData.Instance.lastGameState.resources[24];
								break;
							case Enums.eChimps.CHIMP_TYPE_KNIGHT:
								num10 = GameData.Instance.lastGameState.resources[22];
								num11 = GameData.Instance.lastGameState.resources[24];
								num12 = GameData.Instance.lastGameState.total_horses_available;
								break;
							}
							if (num10 < num8)
							{
								num8 = num10;
							}
							if (num11 < num8)
							{
								num8 = num11;
							}
							if (num12 < num8)
							{
								num8 = num12;
							}
						}
					}
					MainViewModel.Instance.lastTroopsAmountToMakeMax = num8;
					MainViewModel.Instance.lastTroopsAmountToMakex5 = Math.Min(num8, 5);
					if (KeyManager.instance.isShiftDown())
					{
						num8 = Math.Min(num8, 5);
					}
					else if (!KeyManager.instance.isCtrlDown())
					{
						num8 = 1;
					}
					else if (Director.instance.MultiplayerGame)
					{
						num8 = Math.Min(num8, 100);
					}
					MainViewModel.Instance.lastTroopsAmountToMake = num8;
					MainViewModel.Instance.ButtonEnterCreateTroop(MainViewModel.Instance.lastTroopBuildOver);
					if (num8 <= 0)
					{
						num8 = 1;
					}
				}
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArcherButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitSpearmanButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitMacemanButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitXBowmanButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitPikemanButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitSwordsmanButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitKnightButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitEngineerButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitLaddermanButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitTunellerButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitMonkButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabBowButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabSlaveButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabSlingerButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabAssassinButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabHorseArcherButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabSwordsmanButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabGrenadierButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinCamelLancerButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinHealerButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinEunuchButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinAmbusherButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinSkirmisherButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinHeavyCamelButton).IsEnabled = isEnabled2;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinSapperButton).IsEnabled = isEnabled;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinDemolisherButton).IsEnabled = isEnabled;
				if (GameData.getChimpGoldCost(22) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArcherButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(24) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitSpearmanButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(26) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitMacemanButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(23) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitXBowmanButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(25) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitPikemanButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(27) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitSwordsmanButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(28) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitKnightButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(70) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabBowButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(71) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabSlaveButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(72) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabSlingerButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(73) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabAssassinButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(74) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabHorseArcherButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(75) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabSwordsmanButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(76) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArabGrenadierButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(78) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinCamelLancerButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(79) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinHealerButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(80) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinEunuchButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(81) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinAmbusherButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(82) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinSkirmisherButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(83) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinHeavyCamelButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(84) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinSapperButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(85) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitBedouinDemolisherButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(30) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitEngineerButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(29) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitLaddermanButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(30) * num8 <= num7 || GameData.getChimpGoldCost(29) * num8 <= num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitEngineerButton).Visibility = (Visibility)2;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitLaddermanButton).Visibility = (Visibility)2;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitEngineerButtonX).Visibility = (Visibility)2;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitLaddermanButtonX).Visibility = (Visibility)2;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefEngineersGuildNoGoldMessage).Visibility = (Visibility)1;
				}
				else
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitEngineerButton).Visibility = (Visibility)1;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitLaddermanButton).Visibility = (Visibility)1;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitEngineerButtonX).Visibility = (Visibility)1;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitLaddermanButtonX).Visibility = (Visibility)1;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefEngineersGuildNoGoldMessage).Visibility = (Visibility)2;
				}
				if (GameData.Instance.lastGameState.engineer_available == 0)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitEngineerButton).Visibility = (Visibility)1;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitEngineerButtonX).Visibility = (Visibility)1;
				}
				if (GameData.Instance.lastGameState.ladderman_available == 0)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitLaddermanButton).Visibility = (Visibility)1;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitLaddermanButtonX).Visibility = (Visibility)1;
				}
				if (GameData.getChimpGoldCost(5) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitTunellerButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(5) * num8 <= num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitTunellerButton).Visibility = (Visibility)2;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefTunnllersGuildNoGoldMessage).Visibility = (Visibility)1;
				}
				else
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitTunellerButton).Visibility = (Visibility)1;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefTunnllersGuildNoGoldMessage).Visibility = (Visibility)2;
				}
				if (GameData.getChimpGoldCost(37) * num8 > num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitMonkButton).IsEnabled = false;
				}
				if (GameData.getChimpGoldCost(37) * num8 <= num7)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitMonkButton).Visibility = (Visibility)2;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefCathedralNoGoldMessage).Visibility = (Visibility)1;
				}
				else
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitMonkButton).Visibility = (Visibility)1;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefCathedralNoGoldMessage).Visibility = (Visibility)2;
					if (GameData.Instance.lastGameState.lord_Type != 1 && GameData.Instance.lastGameState.lord_Type != 2 && GameData.Instance.lastGameState.lord_Type != 6 && GameData.Instance.lastGameState.lord_Type != 7)
					{
						MainViewModel.Instance.HUDBuildingPanel.RefCathedralNoGoldMessage.Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_CATHEDRAL, 1);
					}
					else
					{
						MainViewModel.Instance.HUDBuildingPanel.RefCathedralNoGoldMessage.Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 258);
					}
				}
				if (GameData.Instance.lastGameState.monk_available == 0)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitMonkButton).Visibility = (Visibility)1;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefCathedralNoGoldMessage).Visibility = (Visibility)1;
				}
				if (GameData.Instance.lastGameState.resources[17] < num8)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitArcherButton).IsEnabled = false;
				}
				if (GameData.Instance.lastGameState.resources[19] < num8)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitSpearmanButton).IsEnabled = false;
				}
				if (GameData.Instance.lastGameState.resources[21] < num8)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitMacemanButton).IsEnabled = false;
				}
				if (GameData.Instance.lastGameState.resources[18] < num8)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitXBowmanButton).IsEnabled = false;
				}
				if (GameData.Instance.lastGameState.resources[20] < num8)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitPikemanButton).IsEnabled = false;
				}
				if (GameData.Instance.lastGameState.resources[22] < num8)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitSwordsmanButton).IsEnabled = false;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitKnightButton).IsEnabled = false;
				}
				if (GameData.Instance.lastGameState.resources[24] < num8)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitSwordsmanButton).IsEnabled = false;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitKnightButton).IsEnabled = false;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitPikemanButton).IsEnabled = false;
				}
				if (GameData.Instance.lastGameState.resources[23] < num8)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitMacemanButton).IsEnabled = false;
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitXBowmanButton).IsEnabled = false;
				}
				if (GameData.Instance.lastGameState.total_horses_available < num8)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRecruitKnightButton).IsEnabled = false;
				}
				MainViewModel.Instance.Show_BarracksArcher = GameData.Instance.lastGameState.troop_types_available[0] > 0;
				MainViewModel.Instance.Show_BarracksXbowman = GameData.Instance.lastGameState.troop_types_available[1] > 0;
				MainViewModel.Instance.Show_BarracksSpearman = GameData.Instance.lastGameState.troop_types_available[2] > 0;
				MainViewModel.Instance.Show_BarracksPikeman = GameData.Instance.lastGameState.troop_types_available[3] > 0;
				MainViewModel.Instance.Show_BarracksMaceman = GameData.Instance.lastGameState.troop_types_available[4] > 0;
				MainViewModel.Instance.Show_BarracksSwordsman = GameData.Instance.lastGameState.troop_types_available[5] > 0;
				MainViewModel.Instance.Show_BarracksKnight = GameData.Instance.lastGameState.troop_types_available[6] > 0;
				MainViewModel.Instance.Show_BarracksBows1 = GameData.Instance.lastGameState.weapon_types_available[0] > 0;
				MainViewModel.Instance.Show_BarracksXBows1 = GameData.Instance.lastGameState.weapon_types_available[1] > 0;
				MainViewModel.Instance.Show_BarracksSpears1 = GameData.Instance.lastGameState.weapon_types_available[2] > 0;
				MainViewModel.Instance.Show_BarracksPikes1 = GameData.Instance.lastGameState.weapon_types_available[3] > 0;
				MainViewModel.Instance.Show_BarracksMaces1 = GameData.Instance.lastGameState.weapon_types_available[4] > 0;
				MainViewModel.Instance.Show_BarracksSwords1 = GameData.Instance.lastGameState.weapon_types_available[5] > 0;
				MainViewModel.Instance.Show_BarracksLeatherArmour1 = GameData.Instance.lastGameState.weapon_types_available[6] > 0;
				MainViewModel.Instance.Show_BarracksArmour1 = GameData.Instance.lastGameState.weapon_types_available[7] > 0;
				MainViewModel.Instance.Show_BarracksHorses1 = GameData.Instance.lastGameState.weapon_types_available[8] > 0;
				MainViewModel.Instance.Show_BarracksBows3 = GameData.Instance.lastGameState.weapon_types_available[0] > 0 && GameData.Instance.lastGameState.resources[17] > 0;
				MainViewModel.Instance.Show_BarracksXBows3 = GameData.Instance.lastGameState.weapon_types_available[1] > 0 && GameData.Instance.lastGameState.resources[18] > 0;
				MainViewModel.Instance.Show_BarracksSpears3 = GameData.Instance.lastGameState.weapon_types_available[2] > 0 && GameData.Instance.lastGameState.resources[19] > 0;
				MainViewModel.Instance.Show_BarracksPikes3 = GameData.Instance.lastGameState.weapon_types_available[3] > 0 && GameData.Instance.lastGameState.resources[20] > 0;
				MainViewModel.Instance.Show_BarracksMaces3 = GameData.Instance.lastGameState.weapon_types_available[4] > 0 && GameData.Instance.lastGameState.resources[21] > 0;
				MainViewModel.Instance.Show_BarracksSwords3 = GameData.Instance.lastGameState.weapon_types_available[5] > 0 && GameData.Instance.lastGameState.resources[22] > 0;
				MainViewModel.Instance.Show_BarracksLeatherArmour3 = GameData.Instance.lastGameState.weapon_types_available[6] > 0 && GameData.Instance.lastGameState.resources[23] > 0;
				MainViewModel.Instance.Show_BarracksArmour3 = GameData.Instance.lastGameState.weapon_types_available[7] > 0 && GameData.Instance.lastGameState.resources[24] > 0;
				MainViewModel.Instance.Show_BarracksHorses3 = GameData.Instance.lastGameState.weapon_types_available[8] > 0 && GameData.Instance.lastGameState.total_horses_available > 0;
				MainViewModel.Instance.Show_BarracksBowsOpaque = (MainViewModel.Instance.Show_BarracksBows3 ? 1f : 0.5f);
				MainViewModel.Instance.Show_BarracksXBowsOpaque = (MainViewModel.Instance.Show_BarracksXBows3 ? 1f : 0.5f);
				MainViewModel.Instance.Show_BarracksSpearsOpaque = (MainViewModel.Instance.Show_BarracksSpears3 ? 1f : 0.5f);
				MainViewModel.Instance.Show_BarracksPikesOpaque = (MainViewModel.Instance.Show_BarracksPikes3 ? 1f : 0.5f);
				MainViewModel.Instance.Show_BarracksMacesOpaque = (MainViewModel.Instance.Show_BarracksMaces3 ? 1f : 0.5f);
				MainViewModel.Instance.Show_BarracksSwordsOpaque = (MainViewModel.Instance.Show_BarracksSwords3 ? 1f : 0.5f);
				MainViewModel.Instance.Show_BarracksLeatherArmourOpaque = (MainViewModel.Instance.Show_BarracksLeatherArmour3 ? 1f : 0.5f);
				MainViewModel.Instance.Show_BarracksArmourOpaque = (MainViewModel.Instance.Show_BarracksArmour3 ? 1f : 0.5f);
				MainViewModel.Instance.Show_BarracksHorsesOpaque = (MainViewModel.Instance.Show_BarracksHorses3 ? 1f : 0.5f);
				MainViewModel.Instance.Show_MercPostArabBow = GameData.Instance.lastGameState.merc_troop_types_available[0] > 0;
				MainViewModel.Instance.Show_MercPostArabSlave = GameData.Instance.lastGameState.merc_troop_types_available[1] > 0;
				MainViewModel.Instance.Show_MercPostArabSlinger = GameData.Instance.lastGameState.merc_troop_types_available[2] > 0;
				MainViewModel.Instance.Show_MercPostArabAssassin = GameData.Instance.lastGameState.merc_troop_types_available[3] > 0;
				MainViewModel.Instance.Show_MercPostArabHorseArcher = GameData.Instance.lastGameState.merc_troop_types_available[4] > 0;
				MainViewModel.Instance.Show_MercPostArabSwordsman = GameData.Instance.lastGameState.merc_troop_types_available[5] > 0;
				MainViewModel.Instance.Show_MercPostArabGrenadier = GameData.Instance.lastGameState.merc_troop_types_available[6] > 0;
				MainViewModel.Instance.Show_BedouinCamelLancer = GameData.Instance.lastGameState.bed_troop_types_available[0] > 0;
				MainViewModel.Instance.Show_BedouinHealer = GameData.Instance.lastGameState.bed_troop_types_available[1] > 0;
				MainViewModel.Instance.Show_BedouinEunuch = GameData.Instance.lastGameState.bed_troop_types_available[2] > 0;
				MainViewModel.Instance.Show_BedouinAmbusher = GameData.Instance.lastGameState.bed_troop_types_available[3] > 0;
				MainViewModel.Instance.Show_BedouinSkirmisher = GameData.Instance.lastGameState.bed_troop_types_available[4] > 0;
				MainViewModel.Instance.Show_BedouinHeavyCamel = GameData.Instance.lastGameState.bed_troop_types_available[5] > 0;
				MainViewModel.Instance.Show_BedouinSapper = GameData.Instance.lastGameState.bed_troop_types_available[6] > 0;
				MainViewModel.Instance.Show_BedouinDemolisher = GameData.Instance.lastGameState.bed_troop_types_available[7] > 0;
			}
			if (GameData.Instance.lastGameState.app_sub_mode == 4)
			{
				MainViewModel.Instance.GranaryFoodBarWidth = 160 * GameData.Instance.lastGameState.food_clock / 15000;
				num = ((GameData.Instance.lastGameState.total_food != 1) ? 1 : 2);
				MainViewModel.Instance.InGranaryUnitsOfFoodText = GameData.Instance.lastGameState.total_food + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, num);
				if (GameData.Instance.lastGameState.months_of_food <= 0)
				{
					num = 11;
					MainViewModel.Instance.InGranaryMonthsOfFoodText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, num);
				}
				else
				{
					num = ((GameData.Instance.lastGameState.months_of_food != 1) ? 9 : 10);
					MainViewModel.Instance.InGranaryMonthsOfFoodText = GameData.Instance.lastGameState.months_of_food + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, num);
				}
				if (GameData.Instance.lastGameState.food_types_eaten > 1)
				{
					MainViewModel.Instance.InGranaryTypesPopFoodText = (GameData.Instance.lastGameState.foodsEaten_popularity / 25).ToString();
					num = ((GameData.Instance.lastGameState.food_types_eaten != 1) ? 7 : 8);
					MainViewModel.Instance.InGranaryTypesOfFoodText = GameData.Instance.lastGameState.food_types_eaten + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, num);
				}
				else
				{
					MainViewModel.Instance.InGranaryTypesPopFoodText = "";
					MainViewModel.Instance.InGranaryTypesOfFoodText = "";
				}
				MainViewModel.Instance.InGranaryRationsPopText = (GameData.Instance.lastGameState.rationing_popularity / 25).ToString();
				switch (GameData.Instance.lastGameState.rationing)
				{
				case 0:
					MainViewModel.Instance.InGranaryRationLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 3);
					break;
				case 1:
					MainViewModel.Instance.InGranaryRationLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 4);
					break;
				case 2:
					MainViewModel.Instance.InGranaryRationLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 5);
					break;
				case 3:
					MainViewModel.Instance.InGranaryRationLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 12);
					break;
				case 4:
					MainViewModel.Instance.InGranaryRationLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 6);
					break;
				default:
					MainViewModel.Instance.InGranaryRationLevelText = "";
					break;
				}
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 3)
			{
				MainViewModel.Instance.InInnBarrelsOfAleText = GameData.Instance.lastGameState.barrels_of_ale.ToString();
				MainViewModel.Instance.InInnFlagonsOfAleText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_INN, 3) + " " + GameData.Instance.lastGameState.pints_of_ale;
				MainViewModel.Instance.InInnWorkingInnsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_INN, 7) + " " + GameData.Instance.lastGameState.working_inns + " (" + GameData.Instance.lastGameState.total_inns + ")";
				MainViewModel.Instance.InInnPopularityText = (GameData.Instance.lastGameState.inn_coverage_popularity / 25).ToString();
				if (!turkish && !arabic)
				{
					MainViewModel.Instance.InInnCoverageText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_INN, 5) + " " + GameData.Instance.lastGameState.inn_coverage_percent + "%";
				}
				else
				{
					MainViewModel.Instance.InInnCoverageText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_INN, 5) + " %" + GameData.Instance.lastGameState.inn_coverage_percent;
				}
				if (GameData.Instance.lastGameState.inn_coverage_next > 0)
				{
					if (!turkish && !arabic)
					{
						MainViewModel.Instance.InInnNextLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_INN, 6) + " " + GameData.Instance.lastGameState.inn_coverage_next + "%";
					}
					else
					{
						MainViewModel.Instance.InInnNextLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_INN, 6) + " %" + GameData.Instance.lastGameState.inn_coverage_next;
					}
				}
				else
				{
					MainViewModel.Instance.InInnNextLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_INN, 8);
				}
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 2)
			{
				MainViewModel.Instance.InKeepPopulationText = GameData.Instance.lastGameState.population.ToString();
				MainViewModel.Instance.InKeepIncomeText = GameData.Instance.lastGameState.tax_amount.ToString();
				int num13 = GameData.Instance.lastGameState.tax_rate + 7;
				if (GameData.Instance.lastGameState.tax_rate >= 10)
				{
					num13++;
				}
				MainViewModel.Instance.InKeepTaxRateText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_KEEP, num13);
				MainViewModel.Instance.InKeepTaxPopText = (GameData.Instance.lastGameState.tax_popularity / 25).ToString();
				MainViewModel.Instance.InKeepSliderPos = GameData.Instance.lastGameState.tax_rate * 25;
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 13)
			{
				string workshopProducingText;
				if (GameData.Instance.lastGameState.production_no_resources > 0)
				{
					workshopProducingText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 11);
				}
				else
				{
					workshopProducingText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 3);
					workshopProducingText = ((GameData.Instance.lastGameState.weapon_being_made_now != 17) ? (workshopProducingText + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 8)) : (workshopProducingText + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 7)));
				}
				string text3 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 4);
				text3 = ((GameData.Instance.lastGameState.weapon_being_made_next != 17) ? (text3 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 8)) : (text3 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 7)));
				MainViewModel.Instance.WorkshopProducingText = workshopProducingText;
				MainViewModel.Instance.WorkshopProducingNextText = text3;
				if (MainViewModel.Instance.lastWeaponWorkshopRollover != "" && MainViewModel.Instance.HUDmain.OtherString != "")
				{
					MainViewModel.Instance.ButtonMakeWeaponMouseEnterFunction(MainViewModel.Instance.lastWeaponWorkshopRollover);
				}
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 15)
			{
				string workshopProducingText2;
				if (GameData.Instance.lastGameState.production_no_resources > 0)
				{
					workshopProducingText2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 11);
				}
				else
				{
					workshopProducingText2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 3);
					workshopProducingText2 = ((GameData.Instance.lastGameState.weapon_being_made_now != 19) ? (workshopProducingText2 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 10)) : (workshopProducingText2 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 9)));
				}
				string text4 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 4);
				text4 = ((GameData.Instance.lastGameState.weapon_being_made_next != 19) ? (text4 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 10)) : (text4 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 9)));
				MainViewModel.Instance.WorkshopProducingText = workshopProducingText2;
				MainViewModel.Instance.WorkshopProducingNextText = text4;
				if (MainViewModel.Instance.lastWeaponWorkshopRollover != "" && MainViewModel.Instance.HUDmain.OtherString != "")
				{
					MainViewModel.Instance.ButtonMakeWeaponMouseEnterFunction(MainViewModel.Instance.lastWeaponWorkshopRollover);
				}
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 14)
			{
				string workshopProducingText3;
				if (GameData.Instance.lastGameState.production_no_resources > 0)
				{
					workshopProducingText3 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 12);
				}
				else
				{
					workshopProducingText3 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 3);
					workshopProducingText3 = ((GameData.Instance.lastGameState.weapon_being_made_now != 21) ? (workshopProducingText3 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 5)) : (workshopProducingText3 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 6)));
				}
				string text5 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 4);
				text5 = ((GameData.Instance.lastGameState.weapon_being_made_next != 21) ? (text5 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 5)) : (text5 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_BLACKSMITHS_WORKSHOP, 6)));
				MainViewModel.Instance.WorkshopProducingText = workshopProducingText3;
				MainViewModel.Instance.WorkshopProducingNextText = text5;
				if (MainViewModel.Instance.lastWeaponWorkshopRollover != "" && MainViewModel.Instance.HUDmain.OtherString != "")
				{
					MainViewModel.Instance.ButtonMakeWeaponMouseEnterFunction(MainViewModel.Instance.lastWeaponWorkshopRollover);
				}
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 35)
			{
				MainViewModel.Instance.GroomNameText = "";
				MainViewModel.Instance.BrideNameText = "";
				MainViewModel.Instance.GroomImage = null;
				MainViewModel.Instance.BrideImage = null;
				if (GameData.Instance.lastGameState.lord_Type != 1 && GameData.Instance.lastGameState.lord_Type != 2 && GameData.Instance.lastGameState.lord_Type != 6 && GameData.Instance.lastGameState.lord_Type != 7)
				{
					if (GameData.Instance.lastGameState.marry_text == 34 && GameData.Instance.lastGameState.marry_status <= 0)
					{
						MainViewModel.Instance.ChurchPanelRingsVis = (Visibility)1;
						if (GameData.Instance.lastGameState.workers_have == 0)
						{
							MainViewModel.Instance.WeddingGossipText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 47);
						}
						else
						{
							MainViewModel.Instance.WeddingGossipText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MARRIAGE, GameData.Instance.lastGameState.marry_text);
						}
					}
					else
					{
						MainViewModel.Instance.ChurchPanelRingsVis = (Visibility)1;
						MainViewModel.Instance.WeddingGossipText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MARRIAGE, GameData.Instance.lastGameState.marry_text);
					}
					if (GameData.Instance.lastGameState.marry_status > 0)
					{
						MainViewModel.Instance.ChurchPanelRingsVis = (Visibility)2;
						MainViewModel.Instance.GroomImage = MainViewModel.Instance.GameSprites[MainViewModel.Instance.HUDBuildingPanel.GetPartnerImage(GameData.Instance.lastGameState.marry_male_type)];
						MainViewModel.Instance.BrideImage = MainViewModel.Instance.GameSprites[MainViewModel.Instance.HUDBuildingPanel.GetPartnerImage(GameData.Instance.lastGameState.marry_female_type)];
						if (GameData.Instance.lastGameState.marry_m_name1 > 0)
						{
							string text6 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_PEASANT_NAMES, GameData.Instance.lastGameState.marry_m_name1);
							if (GameData.Instance.lastGameState.marry_m_name2 > 0)
							{
								text6 = text6 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_PEASANT_SURNAMES, GameData.Instance.lastGameState.marry_m_name2);
							}
							MainViewModel.Instance.GroomNameText = text6;
						}
						if (GameData.Instance.lastGameState.marry_f_name1 > 0)
						{
							string text7 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_PEASANT_NAMES, GameData.Instance.lastGameState.marry_f_name1);
							if (GameData.Instance.lastGameState.marry_f_name2 > 0)
							{
								text7 = text7 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_PEASANT_SURNAMES, GameData.Instance.lastGameState.marry_f_name2);
							}
							MainViewModel.Instance.BrideNameText = text7;
						}
					}
				}
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 58 || GameData.Instance.lastGameState.app_sub_mode == 61 || GameData.Instance.lastGameState.app_sub_mode == 60 || GameData.Instance.lastGameState.app_sub_mode == 62 || GameData.Instance.lastGameState.app_sub_mode == 59 || GameData.Instance.lastGameState.app_sub_mode == 95)
			{
				int num14 = 0;
				if (GameData.Instance.lastGameState.app_sub_mode == 58)
				{
					num14 = 320;
				}
				if (GameData.Instance.lastGameState.app_sub_mode == 61)
				{
					num14 = 640;
				}
				if (GameData.Instance.lastGameState.app_sub_mode == 60)
				{
					num14 = 1280;
				}
				if (GameData.Instance.lastGameState.app_sub_mode == 62)
				{
					num14 = 120;
				}
				if (GameData.Instance.lastGameState.app_sub_mode == 59)
				{
					num14 = 640;
				}
				if (GameData.Instance.lastGameState.app_sub_mode == 95)
				{
					num14 = 320;
				}
				if (num14 > 0)
				{
					string text8 = (GameData.Instance.lastGameState.ai_clock * 100 / num14).ToString();
					MainViewModel.Instance.BuildingLine3Text = text8 + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_SIEGE_TENT, 6);
				}
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 34)
			{
				int ai_clock = GameData.Instance.lastGameState.ai_clock;
				int dog_cage_state = GameData.Instance.lastGameState.dog_cage_state;
				string text9 = ((ai_clock == 1) ? (ai_clock + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_STABLES, 3) + "\n") : (ai_clock + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_STABLES, 1) + "\n"));
				text9 = ((dog_cage_state == 1) ? (text9 + dog_cage_state + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_STABLES, 4)) : (text9 + dog_cage_state + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_STABLES, 2)));
				MainViewModel.Instance.BuildingLine3Text = text9;
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 45)
			{
				switch (locale)
				{
				case "itit":
				case "plpl":
					buildingTitleFontSize = 24.0;
					break;
				case "frfr":
					buildingTitleFontSize = 23.0;
					break;
				case "eses":
				case "dede":
				case "jajp":
					buildingTitleFontSize = 20.0;
					break;
				case "zhcn":
				case "zhhk":
					buildingTitleFontSize = 30.0;
					break;
				default:
					buildingTitleFontSize = 32.0;
					break;
				}
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 56 || GameData.Instance.lastGameState.app_sub_mode == 54)
			{
				buildingTitleFontSize = ((!(locale == "frfr")) ? 32.0 : 26.0);
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 55)
			{
				buildingTitleFontSize = ((!(locale == "frfr")) ? 32.0 : 24.0);
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 57)
			{
				int trading_current_goods = GameData.Instance.lastGameState.trading_current_goods;
				Enums.eUISprites eUISprites = MainViewModel.Instance.goodsSpriteEnumFromGoodsEnum((Enums.Goods)GameData.Instance.lastGameState.trading_current_goods);
				Enums.eUISprites eUISprites2 = MainViewModel.Instance.goodsSpriteEnumFromGoodsEnum((Enums.Goods)GameData.Instance.lastGameState.trading_prev_goods);
				Enums.eUISprites eUISprites3 = MainViewModel.Instance.goodsSpriteEnumFromGoodsEnum((Enums.Goods)GameData.Instance.lastGameState.trading_next_goods);
				int num15 = GameData.Instance.lastGameState.trade_buy_amounts[trading_current_goods];
				int num16 = GameData.Instance.lastGameState.trade_sell_amounts[trading_current_goods];
				int num17 = GameData.Instance.lastGameState.trade_buy_costs[trading_current_goods];
				int num18 = GameData.Instance.lastGameState.trade_sell_costs[trading_current_goods];
				MainViewModel.Instance.TradeGoodsImage = MainViewModel.Instance.GameSprites[(int)eUISprites];
				MainViewModel.Instance.SetSpriteWidth1((int)eUISprites, 100);
				MainViewModel.Instance.TradePrevGoodsImage = MainViewModel.Instance.GameSprites[(int)eUISprites2];
				MainViewModel.Instance.SetSpriteWidth3((int)eUISprites2, 50);
				MainViewModel.Instance.TradeNextGoodsImage = MainViewModel.Instance.GameSprites[(int)eUISprites3];
				MainViewModel.Instance.SetSpriteWidth4((int)eUISprites3, 50);
				MainViewModel.Instance.TradeGoodsAmountText = GameData.Instance.lastGameState.resources[trading_current_goods].ToString();
				if (!german && !russian && !french && !spanish)
				{
					MainViewModel.Instance.BuyText = Translate.Instance.lookUpText("TEXT_IN_TRADEPOST_006") + "  " + num15;
					MainViewModel.Instance.SellText = Translate.Instance.lookUpText("TEXT_IN_TRADEPOST_007") + "  " + num16;
				}
				else
				{
					MainViewModel.Instance.BuyText = Translate.Instance.lookUpText("TEXT_IN_TRADEPOST_006") + " " + num15;
					MainViewModel.Instance.SellText = Translate.Instance.lookUpText("TEXT_IN_TRADEPOST_007") + " " + num16;
				}
				MainViewModel.Instance.BuyPriceText = num17.ToString();
				MainViewModel.Instance.SellPriceText = num18.ToString();
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefTradeBuyButton).IsEnabled = true;
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefTradeSellButton).IsEnabled = true;
				if (num15 <= 0)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefTradeBuyButton).IsEnabled = false;
				}
				if (num16 <= 0)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefTradeSellButton).IsEnabled = false;
				}
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 71)
			{
				MainViewModel.Instance.PlayerNameText = ConfigSettings.Settings_UserName;
				string text10 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_PLAYER_DESC, GameData.Instance.lastGameState.playerdesc_message);
				text10 = text10 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_PLAYER_DESC, 40);
				text10 = text10 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_PLAYER_DESC, GameData.Instance.lastGameState.playerdesc_message2);
				MainViewModel.Instance.PlayerMottoText = text10;
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 72 || GameData.Instance.lastGameState.app_sub_mode == 69)
			{
				MainViewModel.Instance.PopReportFoodText = (GameData.Instance.lastGameState.food_popularity / 25).ToString();
				MainViewModel.Instance.PopReportTaxText = (GameData.Instance.lastGameState.tax_popularity / 25).ToString();
				MainViewModel.Instance.PopReportCrowdingText = (GameData.Instance.lastGameState.overcrowding_popularity / 25).ToString();
				MainViewModel.Instance.PopReportFearFactorText = (GameData.Instance.lastGameState.fearFactor_popularity / 25).ToString();
				MainViewModel.Instance.PopReportReligionText = ((GameData.Instance.lastGameState.religion_popularity + GameData.Instance.lastGameState.church_adjustment) / 25).ToString();
				MainViewModel.Instance.PopReportAleText = (GameData.Instance.lastGameState.inn_coverage_popularity / 25).ToString();
				MainViewModel.Instance.PopReportTotalText = (GameData.Instance.lastGameState.upcoming_total_popularity / 25).ToString();
				int num19 = GameData.Instance.lastGameState.fairs_popularity + GameData.Instance.lastGameState.marriage_popularity + GameData.Instance.lastGameState.jester_popularity + GameData.Instance.lastGameState.plague_popularity + GameData.Instance.lastGameState.wolves_popularity + GameData.Instance.lastGameState.bandits_popularity + GameData.Instance.lastGameState.fire_popularity;
				MainViewModel.Instance.PopReportEventsText = (num19 / 25).ToString();
				MainViewModel.Instance.PopReportFairsText = (GameData.Instance.lastGameState.fairs_popularity / 25).ToString();
				MainViewModel.Instance.PopReportMarriageText = (GameData.Instance.lastGameState.marriage_popularity / 25).ToString();
				MainViewModel.Instance.PopReportJesterText = (GameData.Instance.lastGameState.jester_popularity / 25).ToString();
				MainViewModel.Instance.PopReportPlagueText = (GameData.Instance.lastGameState.plague_popularity / 25).ToString();
				MainViewModel.Instance.PopReportWolvesText = (GameData.Instance.lastGameState.wolves_popularity / 25).ToString();
				MainViewModel.Instance.PopReportBanditsText = (GameData.Instance.lastGameState.bandits_popularity / 25).ToString();
				MainViewModel.Instance.PopReportFireText = (GameData.Instance.lastGameState.fire_popularity / 25).ToString();
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefShowEventsButton).Visibility = (Visibility)2;
				if (GameData.Instance.lastGameState.app_sub_mode == 72 && num19 == 0)
				{
					((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefShowEventsButton).Visibility = (Visibility)1;
				}
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 73)
			{
				int good_things = GameData.Instance.lastGameState.good_things;
				int bad_things = GameData.Instance.lastGameState.bad_things;
				MainViewModel.Instance.FFReportGoodBuildingsText = good_things.ToString();
				MainViewModel.Instance.FFReportBadBuildingsText = bad_things.ToString();
				MainViewModel.Instance.FFReportFearFactorText = (GameData.Instance.lastGameState.fearFactor_popularity / 25).ToString();
				MainViewModel.Instance.FFReportNextLevelText = "";
				MainViewModel.Instance.FFReportNextLevelAmountText = "";
				if (GameData.Instance.lastGameState.fear_factor >= 5)
				{
					MainViewModel.Instance.FFReportNextLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 25);
				}
				else if (GameData.Instance.lastGameState.fear_factor <= -5)
				{
					MainViewModel.Instance.FFReportNextLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 24);
				}
				else if (good_things > bad_things)
				{
					MainViewModel.Instance.FFReportNextLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 27);
					MainViewModel.Instance.FFReportNextLevelAmountText = GameData.Instance.lastGameState.fear_factor_next_level.ToString();
				}
				else if (bad_things > good_things)
				{
					MainViewModel.Instance.FFReportNextLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 26);
					MainViewModel.Instance.FFReportNextLevelAmountText = GameData.Instance.lastGameState.fear_factor_next_level.ToString();
				}
				int index = 0;
				if (GameData.Instance.lastGameState.fear_factor == 0)
				{
					index = 10;
				}
				else if (GameData.Instance.lastGameState.fear_factor > 0)
				{
					index = 11;
				}
				else if (GameData.Instance.lastGameState.fear_factor < 0)
				{
					index = 12;
				}
				MainViewModel.Instance.FFReportCommentaryText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, index);
				MainViewModel.Instance.FFReportEfficiencyAmountText = GameData.Instance.lastGameState.efficiency.ToString();
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 74)
			{
				int num20 = 0;
				int num21 = 1;
				int num22 = 1;
				int num23 = GameData.Instance.lastGameState.pop_months;
				int num24 = (GameData.Instance.lastGameState.pop_months - 300) / 12;
				if (num24 < 0)
				{
					num24 = 0;
				}
				string text11 = locale;
				buildingTitleFontSize = ((text11 == "plpl") ? 28.0 : ((!(text11 == "thth")) ? 32.0 : 26.0));
				if (num23 > 300)
				{
					num23 = 300;
				}
				for (int k = 0; k < num23; k++)
				{
					if (GameData.Instance.lastGameState.population_graph[k] > num20)
					{
						num20 = GameData.Instance.lastGameState.population_graph[k];
					}
				}
				if (num20 <= 8)
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "8";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "4";
					num21 = 12;
				}
				else if (num20 <= 16)
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "16";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "8";
					num21 = 6;
				}
				else if (num20 <= 24)
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "24";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "12";
					num21 = 4;
				}
				else if (num20 <= 32)
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "32";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "16";
					num21 = 3;
				}
				else if (num20 <= 50)
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "50";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "25";
					num21 = 2;
				}
				else if (num20 <= 100)
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "100";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "50";
					num21 = 1;
				}
				else if (num20 <= 200)
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "200";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "100";
					num22 = 2;
				}
				else if (num20 <= 300)
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "300";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "150";
					num22 = 3;
				}
				else if (num20 <= 400)
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "400";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "200";
					num22 = 4;
				}
				else if (num20 <= 500)
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "500";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "250";
					num22 = 5;
				}
				else if (num20 <= 600)
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "600";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "300";
					num22 = 6;
				}
				else if (num20 <= 800)
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "800";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "400";
					num22 = 8;
				}
				else if (num20 <= 1000)
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "1000";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "500";
					num22 = 10;
				}
				else
				{
					MainViewModel.Instance.GraphReportLeftScaleNo2Text = "2000";
					MainViewModel.Instance.GraphReportLeftScaleNo1Text = "1000";
					num22 = 20;
				}
				MainViewModel mainViewModel = MainViewModel.Instance;
				int num25 = num24;
				mainViewModel.GraphReportBottomScaleNo1Text = num25.ToString();
				MainViewModel.Instance.GraphReportBottomScaleNo2Text = (num24 + 5).ToString();
				MainViewModel.Instance.GraphReportBottomScaleNo3Text = (num24 + 10).ToString();
				MainViewModel.Instance.GraphReportBottomScaleNo4Text = (num24 + 15).ToString();
				MainViewModel.Instance.GraphReportBottomScaleNo5Text = (num24 + 20).ToString();
				MainViewModel.Instance.GraphReportBottomScaleNo6Text = (num24 + 25).ToString();
				string text12 = "";
				for (int l = 0; l < num23; l++)
				{
					if (l == 0)
					{
						text12 += "M";
					}
					int num26 = 100 - GameData.Instance.lastGameState.population_graph[l] * num21 / num22;
					string text13 = l + 1 + "," + num26;
					text12 += text13;
					if (l < num23 - 1)
					{
						text12 += ",";
					}
				}
				MainViewModel.Instance.GraphReportPathDataString = text12;
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 76 || GameData.Instance.lastGameState.app_sub_mode == 68 || GameData.Instance.lastGameState.app_sub_mode == 67 || GameData.Instance.lastGameState.app_sub_mode == 66)
			{
				int num27 = 0;
				for (int m = 0; m < 34; m++)
				{
					num27 += GameData.Instance.lastGameState.troop_counts[m];
				}
				MainViewModel.Instance.ArmyReportTotalTroopsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 16) + " " + num27;
				if (GameData.Instance.lastGameState.fear_factor > 0)
				{
					if (!turkish && !arabic)
					{
						MainViewModel.Instance.ArmyReportFFBoostText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 31) + " " + GameData.Instance.lastGameState.fear_factor * 5 + "%";
					}
					else
					{
						MainViewModel.Instance.ArmyReportFFBoostText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 31) + " %" + GameData.Instance.lastGameState.fear_factor * 5;
					}
				}
				else if (GameData.Instance.lastGameState.fear_factor < 0)
				{
					if (!turkish && !arabic)
					{
						MainViewModel.Instance.ArmyReportFFBoostText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 31) + " " + GameData.Instance.lastGameState.fear_factor * 5 + "%";
					}
					else
					{
						MainViewModel.Instance.ArmyReportFFBoostText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 31) + " %" + GameData.Instance.lastGameState.fear_factor * 5;
					}
				}
				else
				{
					MainViewModel.Instance.ArmyReportFFBoostText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 30);
				}
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 79)
			{
				if (GameData.Instance.lastGameState.lord_Type != 1 && GameData.Instance.lastGameState.lord_Type != 2 && GameData.Instance.lastGameState.lord_Type != 6 && GameData.Instance.lastGameState.lord_Type != 7)
				{
					MainViewModel.Instance.RelReportTotalPriestsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 22) + " : " + GameData.Instance.lastGameState.num_priests;
					MainViewModel.Instance.RelReportBlessedPeopleText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 17) + " " + GameData.Instance.lastGameState.blessed_percent;
				}
				else
				{
					MainViewModel.Instance.RelReportTotalPriestsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 8) + " : " + GameData.Instance.lastGameState.num_priests;
					MainViewModel.Instance.RelReportBlessedPeopleText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 10) + " % : " + GameData.Instance.lastGameState.blessed_percent;
				}
				MainViewModel.Instance.RelReportPopEffectText = (GameData.Instance.lastGameState.blessed_popularity / 25).ToString();
				MainViewModel mainViewModel2 = MainViewModel.Instance;
				Size renderSize = ((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefRelReportPopEffectLabel).RenderSize;
				mainViewModel2.RelReportPopEffectTextLabelWidth = (double)((Size)(ref renderSize)).Width + 4.0;
				if (GameData.Instance.lastGameState.blessed_next_level_at != 0)
				{
					if (!turkish && !arabic)
					{
						MainViewModel.Instance.RelReportNextLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 28) + " " + GameData.Instance.lastGameState.blessed_next_level_at + "%";
					}
					else
					{
						MainViewModel.Instance.RelReportNextLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 28) + " %" + GameData.Instance.lastGameState.blessed_next_level_at;
					}
				}
				else
				{
					MainViewModel.Instance.RelReportNextLevelText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 29);
				}
				((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefWGTRelReport2).Visibility = (Visibility)1;
				if (GameData.Instance.lastGameState.church_adjustment != 0)
				{
					if (GameData.Instance.lastGameState.church_adjustment == 25)
					{
						if (GameData.Instance.lastGameState.lord_Type != 1 && GameData.Instance.lastGameState.lord_Type != 2 && GameData.Instance.lastGameState.lord_Type != 6 && GameData.Instance.lastGameState.lord_Type != 7)
						{
							MainViewModel.Instance.RelReportTypeDemandedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 21);
						}
						else
						{
							MainViewModel.Instance.RelReportTypeDemandedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 9);
						}
						MainViewModel.Instance.RelReportDemandEffectText = (GameData.Instance.lastGameState.church_adjustment / 25).ToString();
						((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefWGTRelReport2).Visibility = (Visibility)2;
						MainViewModel mainViewModel3 = MainViewModel.Instance;
						renderSize = ((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefWGT_RelReportLabel).RenderSize;
						mainViewModel3.WGT_RelReportLabelWidth = ((Size)(ref renderSize)).Width;
					}
					else if (GameData.Instance.lastGameState.church_adjustment == 50)
					{
						if (GameData.Instance.lastGameState.lord_Type != 1 && GameData.Instance.lastGameState.lord_Type != 2 && GameData.Instance.lastGameState.lord_Type != 6 && GameData.Instance.lastGameState.lord_Type != 7)
						{
							MainViewModel.Instance.RelReportTypeDemandedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 20);
						}
						else
						{
							MainViewModel.Instance.RelReportTypeDemandedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 9);
						}
						MainViewModel.Instance.RelReportDemandEffectText = (GameData.Instance.lastGameState.church_adjustment / 25).ToString();
						((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefWGTRelReport2).Visibility = (Visibility)2;
						MainViewModel mainViewModel4 = MainViewModel.Instance;
						renderSize = ((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefWGT_RelReportLabel).RenderSize;
						mainViewModel4.WGT_RelReportLabelWidth = ((Size)(ref renderSize)).Width;
					}
					else if (GameData.Instance.lastGameState.church_adjustment == 75)
					{
						if (GameData.Instance.lastGameState.lord_Type != 1 && GameData.Instance.lastGameState.lord_Type != 2 && GameData.Instance.lastGameState.lord_Type != 6 && GameData.Instance.lastGameState.lord_Type != 7)
						{
							MainViewModel.Instance.RelReportTypeDemandedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_REPORT_BUTTONS, 32);
						}
						else
						{
							MainViewModel.Instance.RelReportTypeDemandedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 9);
						}
						MainViewModel.Instance.RelReportDemandEffectText = (GameData.Instance.lastGameState.church_adjustment / 25).ToString();
						((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefWGTRelReport2).Visibility = (Visibility)2;
						MainViewModel mainViewModel5 = MainViewModel.Instance;
						renderSize = ((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefWGT_RelReportLabel).RenderSize;
						mainViewModel5.WGT_RelReportLabelWidth = ((Size)(ref renderSize)).Width;
					}
					else
					{
						MainViewModel.Instance.RelReportTypeDemandedText = "";
					}
				}
				else
				{
					MainViewModel.Instance.RelReportTypeDemandedText = "";
				}
			}
			else if (GameData.Instance.lastGameState.app_sub_mode == 70)
			{
				int in_chimp_type = GameData.Instance.lastGameState.in_chimp_type;
				MainViewModel.Instance.ChimpTypeText = "";
				MainViewModel.Instance.ChimpCommentText = "";
				if (in_chimp_type == 54)
				{
					MainViewModel.Instance.ChimpNameText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, GameData.Instance.lastGameState.in_chimp_type);
				}
				else
				{
					if (in_chimp_type != 127)
					{
						MainViewModel.Instance.ChimpTypeText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, in_chimp_type);
						MainViewModel.Instance.ChimpNameText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_PEASANT_NAMES, GameData.Instance.lastGameState.inchimp_name1);
						if (GameData.Instance.lastGameState.inchimp_name2 > 0)
						{
							MainViewModel mainViewModel6 = MainViewModel.Instance;
							mainViewModel6.ChimpNameText = mainViewModel6.ChimpNameText + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_PEASANT_SURNAMES, GameData.Instance.lastGameState.inchimp_name2);
						}
					}
					else
					{
						MainViewModel.Instance.ChimpTypeText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 6);
						MainViewModel.Instance.ChimpNameText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 15 + GameData.Instance.lastGameState.inchimp_name1 % 10);
					}
					if (GameData.Instance.lastGameState.chimp_comments > 0)
					{
						MainViewModel.Instance.ChimpCommentText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_COMMENT, GameData.Instance.lastGameState.chimp_comments);
					}
				}
				MainViewModel.Instance.ChimpWorkText = getChimpActionText(GameData.Instance.lastGameState, in_chimp_type == 127);
			}
		}
		if ((Director.instance.MultiplayerGame || Director.instance.SkirmishModeGame) && GameData.Instance.lastGameState.numMPChatEntries > 0)
		{
			for (int n = 0; n < GameData.Instance.lastGameState.numMPChatEntries; n++)
			{
				switch (GameData.Instance.lastGameState.chat_store_data[n, 0])
				{
				case 1:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, 41) + " " + (GameData.Instance.lastGameState.chat_store_data[n, 3] + GameData.Instance.lastGameState.chat_store_data[n, 4] * 30000) + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, 42));
					break;
				case 2:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, 37));
					break;
				case 3:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, 38) + " " + Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 2]));
					break;
				case 4:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, Enums.eTextValues.TEXT_SCN_MESSAGE));
					break;
				case 5:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, Enums.eTextValues.TEXT_SCN_EVENT));
					break;
				case 6:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, Enums.eTextValues.TEXT_SCN_CHEESE) + " " + Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 2]));
					break;
				case 7:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_ANY_OF_THESE));
					break;
				case 8:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, 40) + " " + Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 2]));
					break;
				case 9:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, 39));
					break;
				case 10:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKIRMISH_MISC, 1) + " - " + GameData.Instance.lastGameState.chat_store_data[n, 3] + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, GameData.Instance.lastGameState.chat_store_data[n, 4]) + " : " + Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 2]));
					break;
				case 11:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES2, 4));
					break;
				case 12:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES2, 3) + " " + Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 2]));
					break;
				case 13:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES2, 6));
					break;
				case 14:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES2, 5) + " " + Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 2]));
					break;
				case 15:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKIRMISH_MISC, 2) + " - " + GameData.Instance.lastGameState.chat_store_data[n, 3] + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, GameData.Instance.lastGameState.chat_store_data[n, 4]) + " : " + Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 2]));
					break;
				case 16:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES2, 0));
					break;
				case 17:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, Enums.eTextValues.TEXT_SCN_SPEARMAN));
					break;
				case 18:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, Enums.eTextValues.TEXT_SCN_ARCHER));
					break;
				case 19:
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 1]), GameData.Instance.lastGameState.chat_store_data[n, 1], Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, Enums.eTextValues.TEXT_SCN_XBOWMAN) + " " + Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.chat_store_data[n, 2]));
					break;
				}
			}
		}
		if (GameData.Instance.lastGameState.skirmishInsultFrom > 0 && !ConfigSettings.Settings_MuteInsults)
		{
			MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.skirmishInsultFrom), GameData.Instance.lastGameState.skirmishInsultFrom, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_INSULTS, GameData.Instance.lastGameState.skirmishInsult));
			if (!ConfigSettings.Settings_MuteInsultSpeech)
			{
				SFXManager.instance.playInsult(GameData.Instance.lastGameState.skirmishInsult);
			}
		}
		if (MainViewModel.Instance.Show_HUD_Tutorial)
		{
			MainViewModel.Instance.HUDmain.monitorTutorialArrows();
		}
		if (MainViewModel.Instance.Show_HUD_MPInviteWarning)
		{
			MainViewModel.Instance.HUDMPInviteWarning.Update();
		}
		if (MainViewModel.Instance.Show_HUD_MPChatMessages)
		{
			MainViewModel.Instance.HUDMPChatMessages.Update();
		}
		if (GameData.Instance.game_type == 6)
		{
			OnScreenText.Instance.addOSTEntry(Enums.eOnScreenText.OST_STARTING_GOODS, 1, 0);
		}
		if (ConfigSettings.Settings_ShowGameTime)
		{
			MainViewModel.Instance.GameTime = GameData.GetTimeString((int)GameData.Instance.lastGameState.elapsedTime / 1000);
		}
		if (ConfigSettings.Settings_ShowLocalTime)
		{
			MainViewModel.Instance.LocalTime = DateTime.Now.ToShortTimeString();
		}
		MouseIsUpStroke = false;
		MouseIsDownStroke = false;
		MainViewModel.Instance.BuildingTitleFontSize = buildingTitleFontSize;
	}

	public void BriefingUIUpdate()
	{
		string text = "";
		int startDate = 0;
		int nowDate = 0;
		int endDate = 0;
		if (MainViewModel.Instance.BriefingMode == 1)
		{
			List<GameData.ScenarioEvent> events = GameData.scenario.getEvents();
			text = GameData.scenario.getWinTimer(ref startDate, ref nowDate, ref endDate);
			if (text == null)
			{
				((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefObjectiveTimer).Visibility = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.ObjectiveTimerText = text;
				((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefObjectiveTimer).Visibility = (Visibility)2;
				int num = nowDate - startDate;
				int num2 = endDate - startDate;
				if (num > num2)
				{
					num = num2;
				}
				if (num2 > 0)
				{
					MainViewModel.Instance.ObjectiveTimerWidth = 200 * num / num2;
				}
			}
			int num3 = events.Count;
			if (num3 > 9)
			{
				num3 = ((num3 <= 18) ? 9 : 10);
			}
			int num4 = 20 + num3 * 25;
			MainViewModel.Instance.BriefingTextMargin = "0," + num4 + ",0,0";
			MainViewModel.Instance.MissionBriefingText = GameData.Instance.GetMissionBriefing(null, fromBriefing: true);
			num4 -= 130;
			MainViewModel.Instance.BriefingTimerMargin = "100,0,0," + num4;
			UpdateObjectiveRows(events, MainViewModel.Instance.HUDBriefingPanel.RefWGTObjectives);
			if (((GameData.Instance.game_type == 0 && GameData.Instance.mission_level > 2 && GameData.Instance.mission_level != 31 && GameData.Instance.mission_level != 32 && GameData.Instance.mission_level != 33) || GameData.Instance.game_type == 2) && (GameData.Instance.game_type != 2 || GameData.Instance.mapType != Enums.GameModes.BUILD))
			{
				((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefBriefingDifficultyButton).IsEnabled = MainViewModel.Instance.BriefingFromStory && GameData.Instance.game_type == 0;
				((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefBriefingDifficultyButton).Visibility = (Visibility)2;
				if (GameData.Instance.difficulty_level == Enums.GameDifficulty.DIFFICULTY_EASY)
				{
					MainViewModel.Instance.BriefingDifficultyText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 19);
				}
				else if (GameData.Instance.difficulty_level == Enums.GameDifficulty.DIFFICULTY_NORMAL || GameData.Instance.difficulty_level == Enums.GameDifficulty.DIFFICULTY_NA)
				{
					MainViewModel.Instance.BriefingDifficultyText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 20);
				}
				else if (GameData.Instance.difficulty_level == Enums.GameDifficulty.DIFFICULTY_HARD)
				{
					MainViewModel.Instance.BriefingDifficultyText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 21);
				}
				else if (GameData.Instance.difficulty_level == Enums.GameDifficulty.DIFFICULTY_VERYHARD)
				{
					MainViewModel.Instance.BriefingDifficultyText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 22);
				}
			}
			else
			{
				((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefBriefingDifficultyButton).Visibility = (Visibility)1;
			}
		}
		else if (MainViewModel.Instance.BriefingMode == 2)
		{
			((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefBriefingDifficultyButton).Visibility = (Visibility)1;
			MainViewModel.Instance.MissionStrategyText = GameData.Instance.GetStrategyText();
			for (int i = 0; i < 5; i++)
			{
				((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefBriefingHintButtons[i]).Visibility = (Visibility)1;
				((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefBriefingHintTexts[i]).Visibility = (Visibility)1;
			}
			int num5 = GameData.Instance.GetNumHintsForCurrentMission();
			if (num5 == 0)
			{
				((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefHintsTitleStamp).Visibility = (Visibility)1;
				MainViewModel.Instance.BriefingHintsH = "";
				MainViewModel.Instance.BriefingHintsints = "";
				return;
			}
			((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefHintsTitleStamp).Visibility = (Visibility)2;
			string text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HINTS, 2);
			if (arabic || thai)
			{
				MainViewModel.Instance.BriefingHintsH = "";
				MainViewModel.Instance.BriefingHintsints = text2;
			}
			else
			{
				MainViewModel.Instance.BriefingHintsH = text2.Substring(0, 1);
				MainViewModel.Instance.BriefingHintsints = text2.Substring(1, text2.Length - 1);
			}
			if (num5 > 5)
			{
				num5 = 5;
			}
			for (int j = 0; j < num5; j++)
			{
				text = GameData.Instance.GetHintText(j);
				if (text == "")
				{
					((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefBriefingHintButtons[j]).Visibility = (Visibility)2;
					((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefBriefingHintTexts[j]).Visibility = (Visibility)2;
					MainViewModel.Instance.HUDBriefingPanel.RefBriefingHintTexts[j].Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HINTS, 4);
					break;
				}
				((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefBriefingHintButtons[j]).Visibility = (Visibility)2;
				((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefBriefingHintTexts[j]).Visibility = (Visibility)2;
				MainViewModel.Instance.HUDBriefingPanel.RefBriefingHintTexts[j].Text = text;
			}
		}
		else if (MainViewModel.Instance.BriefingMode == 3)
		{
			((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefBriefingDifficultyButton).Visibility = (Visibility)1;
			if (MainViewModel.Instance.HUDBriefingPanel.canGoBack())
			{
				((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefBriefingHelpBackButton).Visibility = (Visibility)2;
			}
			else
			{
				((UIElement)MainViewModel.Instance.HUDBriefingPanel.RefBriefingHelpBackButton).Visibility = (Visibility)1;
			}
		}
	}

	public int UpdateObjectiveRows(List<GameData.ScenarioEvent> eventsList, WGT_Objective[] RefWGTObjectives)
	{
		int num = RefWGTObjectives.Length;
		int num2 = 0;
		foreach (GameData.ScenarioEvent events in eventsList)
		{
			bool complete = false;
			string text = "";
			bool flag = true;
			if (events == null)
			{
				continue;
			}
			int num3 = events.eventID;
			if (num3 >= 20)
			{
				num3++;
			}
			if (num3 == 5)
			{
				num3 = 7;
			}
			if (num3 == 6)
			{
				num3 = 7;
			}
			if (num3 == 17)
			{
				num3 = 7;
			}
			if (num3 == 15 || num3 == 32)
			{
				num3 = 8;
			}
			string text2;
			switch (num3)
			{
			case 25:
			case 27:
			case 28:
			case 29:
			case 31:
			{
				int index = 132 + events.eventType;
				if (events.eventType == 31)
				{
					index = 166;
				}
				text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, index);
				flag = false;
				break;
			}
			case 26:
				text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 89);
				flag = false;
				break;
			case 33:
				text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 147);
				break;
			case 34:
				text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 148);
				break;
			case 30:
				text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 6);
				flag = false;
				break;
			case 3:
				text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_OBJECTIVES, num3);
				if (events.eventType == 5)
				{
					text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, Enums.eTextValues.TEXT_SCN_KILL_ALL_ENEMY_LORDS);
				}
				else if (events.eventType > 0)
				{
					text2 = text2 + " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, GameData.lord_killed_list[events.eventType]);
				}
				flag = false;
				break;
			default:
			{
				bool flag2 = false;
				if (GameData.Instance.game_type != 0)
				{
					switch (num3)
					{
					case 11:
						num3 = 117;
						flag2 = true;
						break;
					case 12:
						num3 = 118;
						flag2 = true;
						break;
					case 13:
						num3 = 119;
						flag2 = true;
						break;
					case 14:
						num3 = 120;
						flag2 = true;
						break;
					}
				}
				text2 = (flag2 ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SCENARIO, num3) : Translate.Instance.lookUpText(Enums.eTextSections.TEXT_OBJECTIVES, num3));
				break;
			}
			}
			switch (events.eventID)
			{
			case 5:
			case 6:
			case 7:
			case 17:
				if (events.eventType > 0)
				{
					text = text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, events.eventType) + " ";
					flag = true;
				}
				break;
			case 1:
			case 4:
			case 20:
			case 21:
			case 22:
			case 23:
			case 32:
			case 33:
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (events.complete > 0)
			{
				complete = true;
			}
			if (flag)
			{
				text = ((events.currentAmount != -9999 && !MainViewModel.Instance.BriefingFromStory) ? (text + events.targetAmount + " (" + events.currentAmount + ")") : (text + events.targetAmount));
			}
			RefWGTObjectives[num2].SetObjective(isActive: true, text2, text, complete);
			num2++;
			if (num2 >= num)
			{
				break;
			}
		}
		for (int i = num2; i < num; i++)
		{
			RefWGTObjectives[i].SetObjective(isActive: false, "", "", complete: false);
		}
		return num2;
	}

	public void RadarMouseClickDelayPostBriefing()
	{
		radarClickDelay = true;
		radarClickDelayTime = DateTime.UtcNow.AddMilliseconds(250.0);
	}

	public void RadarScrollMap()
	{
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		if (!MainViewModel.viewModelLoaded || MainViewModel.Instance.Show_HUD_Briefing || !MainControls.instance.IsUIVisible || (radarClickDelay && DateTime.UtcNow < radarClickDelayTime))
		{
			return;
		}
		if (!mouseIsDown)
		{
			radarScrollTrigged = false;
		}
		if (GameData.Instance.lastGameState == null || currentScene != Enums.SceneIDS.ActualMainGame || !MainViewModel.Instance.RadarLoaded || !MainViewModel.Instance.MainUILoaded || (MouseIsDownStroke && (((Point)(ref NGMousePoint)).X < 0f || ((Point)(ref NGMousePoint)).X >= (float)SHRadarRectSize || ((Point)(ref NGMousePoint)).Y < 0f || ((Point)(ref NGMousePoint)).Y >= (float)SHRadarRectSize)) || !mouseIsDown || MainControls.instance.CurrentAction == 8 || MainControls.instance.CurrentAction == 9 || MainControls.instance.CurrentAction == 5)
		{
			return;
		}
		if (MouseIsDownStroke)
		{
			if (((UIElement)MainViewModel.Instance.HUDRoot.RefRadarME).Opacity != 0f)
			{
				((UIElement)MainViewModel.Instance.HUDRoot.RefRadarME).Opacity = 0f;
				MouseIsDownStroke = false;
				radarScrollTrigged = false;
			}
			else
			{
				radarScrollTrigged = true;
				LastNGMousePoint = NGMousePoint;
				EngineInterface.GameAction(Enums.GameActionCommand.RadarClicked, (int)(((Point)(ref NGMousePoint)).X * SHRadarScalar), (int)(((Point)(ref NGMousePoint)).Y * SHRadarScalar));
			}
		}
		else
		{
			if (!radarScrollTrigged)
			{
				return;
			}
			float num = ((Point)(ref NGMousePoint)).X - ((Point)(ref LastNGMousePoint)).X;
			float num2 = ((Point)(ref LastNGMousePoint)).Y - ((Point)(ref NGMousePoint)).Y;
			if (num == 0f && num2 != 0f)
			{
				if (num2 > 0f)
				{
					KeyManager.instance.RadarHeldY = 1f;
				}
				else
				{
					KeyManager.instance.RadarHeldY = -1f;
				}
				return;
			}
			if (num2 == 0f && num != 0f)
			{
				if (num > 0f)
				{
					KeyManager.instance.RadarHeldX = 1f;
				}
				else
				{
					KeyManager.instance.RadarHeldX = -1f;
				}
				return;
			}
			float num3 = Math.Abs(num);
			float num4 = Math.Abs(num2);
			if (num3 > num4)
			{
				KeyManager.instance.RadarHeldX = num / num3;
				KeyManager.instance.RadarHeldY = num2 / num3;
			}
			else
			{
				KeyManager.instance.RadarHeldX = num / num4;
				KeyManager.instance.RadarHeldY = num2 / num4;
			}
		}
	}

	public void InitScenarioEditorValues()
	{
		EngineInterface.ScenarioOverview scenarioOverview = GameData.Instance.scenarioOverview;
		MainViewModel.Instance.ScenarioStartingMonthText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, scenarioOverview.startMonth);
		MainViewModel.Instance.ScenarioStartingPopText = scenarioOverview.scenario_start_popularity.ToString();
		MainViewModel.Instance.ScenarioStartingYearText = scenarioOverview.startYear.ToString();
		MainViewModel.Instance.SetStartingSpecial(scenarioOverview.special_start > 0);
		MainViewModel.Instance.ScenarioStartingSpecialGoldText = scenarioOverview.special_start_gold.ToString();
		string scenarioStartingSpecialRationsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 3);
		switch (scenarioOverview.special_start_rationing)
		{
		case 1:
			scenarioStartingSpecialRationsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 4);
			break;
		case 2:
			scenarioStartingSpecialRationsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 5);
			break;
		case 3:
			scenarioStartingSpecialRationsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 6);
			break;
		case 4:
			scenarioStartingSpecialRationsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GRANARY, 12);
			break;
		}
		MainViewModel.Instance.ScenarioStartingSpecialRationsText = scenarioStartingSpecialRationsText;
		MainViewModel.Instance.ScenarioStartingSpecialTaxText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_KEEP, scenarioOverview.special_start_tax_rate + 7);
		MainViewModel.Instance.ScenarioStartingGoldText = scenarioOverview.scenario_start_goods[15].ToString();
		MainViewModel.Instance.ScenarioStartingPitchText = scenarioOverview.scenario_start_goods[8].ToString();
		MainViewModel.Instance.SetMapTypeVisibility(GameData.Instance.multiplayerMap);
		ScenarioEditorUpdateNewEventButtons();
	}

	public void ScenarioEditorUpdateNewEventButtons()
	{
		bool flag = false;
		bool flag2 = false;
		if (GameData.Instance.mapType != Enums.GameModes.BUILD && MainViewModel.Instance.ScenarioEditorMode == Enums.ScenarioViews.Main)
		{
			flag = ((GameData.Instance.mapType != Enums.GameModes.SIEGE && GameData.Instance.mapType != Enums.GameModes.ECO) ? true : false);
			flag2 = true;
		}
		if (MainViewModel.Instance.ScenarioNewEventMessageVisibleBool != flag2)
		{
			MainViewModel.Instance.ScenarioNewEventMessageVisibleBool = flag2;
		}
		if (MainViewModel.Instance.ScenarioNewInvasionVisibleBool != flag)
		{
			MainViewModel.Instance.ScenarioNewInvasionVisibleBool = flag;
		}
		bool flag3 = false;
		bool flag4 = false;
		bool flag5 = false;
		bool flag6 = false;
		switch (MainViewModel.Instance.ScenarioEditorMode)
		{
		case Enums.ScenarioViews.Main:
			if (!GameData.Instance.multiplayerMap)
			{
				flag6 = true;
			}
			break;
		case Enums.ScenarioViews.Invasions:
		case Enums.ScenarioViews.Events:
			flag4 = true;
			flag5 = true;
			break;
		case Enums.ScenarioViews.EventsConditions:
		case Enums.ScenarioViews.EventsActions:
			flag4 = true;
			break;
		default:
			if (!GameData.Instance.multiplayerMap)
			{
				flag3 = true;
			}
			break;
		}
		if (MainViewModel.Instance.ScenarioCommonBackVisibleBool != flag3)
		{
			MainViewModel.Instance.ScenarioCommonBackVisibleBool = flag3;
		}
		if (MainViewModel.Instance.ScenarioCommonOKVisibleBool != flag4)
		{
			MainViewModel.Instance.ScenarioCommonOKVisibleBool = flag4;
		}
		if (MainViewModel.Instance.ScenarioCommonDeleteVisibleBool != flag5)
		{
			MainViewModel.Instance.ScenarioCommonDeleteVisibleBool = flag5;
		}
		if (MainViewModel.Instance.ScenarioCommonEditTeamsVisible != flag6)
		{
			MainViewModel.Instance.ScenarioCommonEditTeamsVisible = flag6;
		}
	}

	public bool addChimpActions(EngineInterface.PlayState state, ref string line1, ref string line2, bool islamic)
	{
		line1 = "";
		line2 = "";
		if (state == null)
		{
			return false;
		}
		if (state.building_type_sleeping != 0)
		{
			line2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 131);
			((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefBuildingZZZButtonOff).Visibility = (Visibility)2;
			((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefBuildingZZZButtonOn).Visibility = (Visibility)1;
			return true;
		}
		((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefBuildingZZZButtonOff).Visibility = (Visibility)1;
		((UIElement)MainViewModel.Instance.HUDBuildingPanel.RefBuildingZZZButtonOn).Visibility = (Visibility)2;
		if (state.in_structure_type == 100)
		{
			if (GameData.Instance.lastGameState.dog_cage_state != 0)
			{
				line1 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MISC2, 1);
				line2 = Platform_Multiplayer.Instance.getSkirmishName(GameData.Instance.lastGameState.dog_cage_state);
			}
			return true;
		}
		if (state.have_building_stats <= 0)
		{
			return false;
		}
		if (state.got_keep_access == 0)
		{
			line2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GENERAL_BUILDINGS, 9);
		}
		else if (state.turned_off > 0)
		{
			line2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GENERAL_BUILDINGS, 7);
		}
		else if (state.job_vacancies > 0)
		{
			if (state.workers_have == 0)
			{
				line2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GENERAL_BUILDINGS, 3);
			}
			else
			{
				line2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GENERAL_BUILDINGS, 4 + state.job_vacancies);
			}
		}
		else if (state.working > 0)
		{
			switch (state.in_structure_type)
			{
			case 34:
				if (state.mill_message < 100)
				{
					line1 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_MILL, state.mill_message + 3);
				}
				break;
			case 20:
				line1 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GENERAL_BUILDINGS, 2);
				break;
			case 12:
			case 13:
			case 14:
				line1 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GENERAL_BUILDINGS, 10);
				line2 = getChimpActionText(state, islamic);
				break;
			case 5:
				line1 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GENERAL_BUILDINGS, 10);
				line2 = getChimpActionText(state, islamic);
				break;
			default:
				line1 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GENERAL_BUILDINGS, 10);
				line2 = getChimpActionText(state, islamic);
				break;
			}
		}
		else
		{
			line1 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_GENERAL_BUILDINGS, 4);
		}
		return true;
	}

	public string getChimpActionText(EngineInterface.PlayState state, bool islamic)
	{
		int inchimp_n_text = state.inchimp_n_text;
		int in_chimp_goods = state.in_chimp_goods;
		if (inchimp_n_text > 0)
		{
			if (inchimp_n_text == 10 && in_chimp_goods > 0)
			{
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_UNIT_ACTIONS, 101) + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, in_chimp_goods) + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_UNIT_ACTIONS, 102);
			}
			if (inchimp_n_text == 6 && in_chimp_goods > 0)
			{
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_UNIT_ACTIONS, 100) + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, in_chimp_goods);
			}
			if (inchimp_n_text == 9 && in_chimp_goods > 0)
			{
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_UNIT_ACTIONS, 103) + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, in_chimp_goods);
			}
			if (islamic)
			{
				switch (inchimp_n_text)
				{
				case 24:
					return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 11);
				case 25:
					return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 12);
				case 26:
					return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 13);
				case 27:
					return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 14);
				}
			}
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_UNIT_ACTIONS, inchimp_n_text);
		}
		return "";
	}

	public bool overNoesisGUI()
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Expected O, but got Unknown
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Expected O, but got Unknown
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Expected O, but got Unknown
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0306: Expected O, but got Unknown
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_029a: Unknown result type (might be due to invalid IL or missing references)
		overUI = false;
		overBuildingMenu = false;
		if (!MainViewModel.Instance.MainUILoaded)
		{
			return false;
		}
		if ((Object)(object)m_MainCamera == (Object)null)
		{
			m_MainCamera = ((Component)((Component)Camera.main).transform).GetComponent<Camera>();
		}
		NGview = ((Component)m_MainCamera).GetComponent<NoesisView>();
		Visual val = (Visual)VisualTreeHelper.GetRoot((DependencyObject)(object)NGview.Content);
		if ((BaseComponent)(object)MainViewModel.Instance.HUDRoot.RefRadarMapImage != (BaseComponent)null && MainViewModel.Instance.RadarLoaded)
		{
			NGMousePoint = Mouse.GetPosition((UIElement)(object)MainViewModel.Instance.HUDRoot.RefRadarMapImage);
		}
		if (MainViewModel.Instance.Show_HUD_Briefing && MainViewModel.Instance.HUDBriefingPanel.webBrowserLoaded)
		{
			BriefingHelpMousePoint = Mouse.GetPosition((UIElement)(object)MainViewModel.Instance.HUDBriefingPanel.RefBriefingHelpTexture);
		}
		if (MainViewModel.Instance.Show_HUD_Help && MainViewModel.Instance.HUDHelp.webBrowserLoaded)
		{
			BriefingHelpMousePoint = Mouse.GetPosition((UIElement)(object)MainViewModel.Instance.HUDHelp.RefMainHelpTexture);
		}
		Vector3 mousePosition = Input.mousePosition;
		Point val2 = val.PointFromScreen(new Point(mousePosition.x, (float)Screen.height - mousePosition.y));
		HitTestResult val3 = VisualTreeHelper.HitTest(val, val2);
		Visual val4 = (Visual)((HitTestResult)(ref val3)).VisualHit;
		UIElement val5 = (UIElement)((HitTestResult)(ref val3)).VisualHit;
		if (val5 == null)
		{
			lastUIHit = "";
			return false;
		}
		if ((BaseComponent)(object)val4 != (BaseComponent)null)
		{
			lastUIHit = ((object)val4).GetType().ToString();
		}
		bool flag = MainViewModel.Instance.IsMapEditorMode && MainViewModel.Instance.MEMode == 0;
		UIElement val6 = val5;
		bool flag2 = true;
		while (val6 is FrameworkElement)
		{
			if ((((FrameworkElement)val6).Name == "AlignmentGrid" && !flag) || (((FrameworkElement)val6).Name == "MEMenuGrid" && flag) || ((FrameworkElement)val6).Name == "InBuildingLayoutRoot")
			{
				overBuildingMenu = true;
			}
			if (((FrameworkElement)val6).Tag != null && ((FrameworkElement)val6).Tag is string)
			{
				if ((string)((FrameworkElement)val6).Tag == "Ignore")
				{
					return false;
				}
				if ((string)((FrameworkElement)val6).Tag == "IgnoreNotME" && !flag)
				{
					return false;
				}
				if ((string)((FrameworkElement)val6).Tag == "IgnoreME" && flag)
				{
					return false;
				}
				if (flag2 && (string)((FrameworkElement)val6).Tag == "IgnoreSelf")
				{
					return false;
				}
			}
			flag2 = false;
			if ((BaseComponent)(object)((FrameworkElement)val6).Parent != (BaseComponent)null)
			{
				val6 = (UIElement)(object)((FrameworkElement)val6).Parent;
				continue;
			}
			DependencyObject parent = VisualTreeHelper.GetParent((DependencyObject)(object)val6);
			if ((BaseComponent)(object)parent == (BaseComponent)null || !(parent is FrameworkElement))
			{
				break;
			}
			val6 = (UIElement)parent;
		}
		overUI = true;
		return true;
	}

	public UIElement FindVisibleUIElement(Type type)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		if ((Object)(object)m_MainCamera == (Object)null)
		{
			m_MainCamera = ((Component)((Component)Camera.main).transform).GetComponent<Camera>();
		}
		NGview = ((Component)m_MainCamera).GetComponent<NoesisView>();
		Visual myVisual = (Visual)VisualTreeHelper.GetRoot((DependencyObject)(object)NGview.Content);
		return EnumVisual(myVisual, type);
	}

	public UIElement EnumVisual(Visual myVisual, Type type)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount((DependencyObject)(object)myVisual); i++)
		{
			Visual val = (Visual)VisualTreeHelper.GetChild((DependencyObject)(object)myVisual, i);
			if (((object)val).GetType() == type)
			{
				return (UIElement)val;
			}
			UIElement val2 = EnumVisual(val, type);
			if ((BaseComponent)(object)val2 != (BaseComponent)null)
			{
				return val2;
			}
		}
		return null;
	}

	public bool overNoesisGUITag(string tag)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Expected O, but got Unknown
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Expected O, but got Unknown
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Expected O, but got Unknown
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Expected O, but got Unknown
		overUI = false;
		overBuildingMenu = false;
		if (!MainViewModel.Instance.MainUILoaded)
		{
			return false;
		}
		if ((Object)(object)m_MainCamera == (Object)null)
		{
			m_MainCamera = ((Component)((Component)Camera.main).transform).GetComponent<Camera>();
		}
		NGview = ((Component)m_MainCamera).GetComponent<NoesisView>();
		Visual val = (Visual)VisualTreeHelper.GetRoot((DependencyObject)(object)NGview.Content);
		if ((BaseComponent)(object)MainViewModel.Instance.HUDRoot.RefRadarMapImage != (BaseComponent)null && MainViewModel.Instance.RadarLoaded)
		{
			NGMousePoint = Mouse.GetPosition((UIElement)(object)MainViewModel.Instance.HUDRoot.RefRadarMapImage);
		}
		if (MainViewModel.Instance.Show_HUD_Briefing && MainViewModel.Instance.HUDBriefingPanel.webBrowserLoaded)
		{
			BriefingHelpMousePoint = Mouse.GetPosition((UIElement)(object)MainViewModel.Instance.HUDBriefingPanel.RefBriefingHelpTexture);
		}
		if (MainViewModel.Instance.Show_HUD_Help && MainViewModel.Instance.HUDHelp.webBrowserLoaded)
		{
			BriefingHelpMousePoint = Mouse.GetPosition((UIElement)(object)MainViewModel.Instance.HUDHelp.RefMainHelpTexture);
		}
		Vector3 mousePosition = Input.mousePosition;
		Point val2 = val.PointFromScreen(new Point(mousePosition.x, (float)Screen.height - mousePosition.y));
		HitTestResult val3 = VisualTreeHelper.HitTest(val, val2);
		Visual val4 = (Visual)((HitTestResult)(ref val3)).VisualHit;
		UIElement val5 = (UIElement)((HitTestResult)(ref val3)).VisualHit;
		if (val5 == null)
		{
			lastUIHit = "";
			return false;
		}
		if ((BaseComponent)(object)val4 != (BaseComponent)null)
		{
			lastUIHit = ((object)val4).GetType().ToString();
		}
		if (MainViewModel.Instance.IsMapEditorMode)
		{
			_ = MainViewModel.Instance.MEMode == 0;
		}
		else
			_ = 0;
		UIElement val6 = val5;
		while (val6 is FrameworkElement)
		{
			if (((FrameworkElement)val6).Tag != null && ((FrameworkElement)val6).Tag is string && (string)((FrameworkElement)val6).Tag == tag)
			{
				return true;
			}
			if ((BaseComponent)(object)((FrameworkElement)val6).Parent != (BaseComponent)null)
			{
				val6 = (UIElement)(object)((FrameworkElement)val6).Parent;
				continue;
			}
			DependencyObject parent = VisualTreeHelper.GetParent((DependencyObject)(object)val6);
			if ((BaseComponent)(object)parent == (BaseComponent)null || !(parent is FrameworkElement))
			{
				break;
			}
			val6 = (UIElement)parent;
		}
		return false;
	}

	public void NewScene(Enums.SceneIDS sceneNo)
	{
		if (currentScene == Enums.SceneIDS.ActualMainGame && sceneNo != Enums.SceneIDS.ActualMainGame)
		{
			if (Director.instance.SimRunning)
			{
				EditorDirector.instance.stopGameSim(leavingScene: true);
			}
			TilemapManager.instance.ClearTilemap();
			GameMap.instance.clearSprites();
		}
		MainViewModel.Instance.Show_InGame = false;
		MainViewModel.Instance.Show_Frontend = false;
		if (sceneNo != Enums.SceneIDS.Intro)
		{
			MainViewModel.Instance.Show_IntroSequence = false;
		}
		MainViewModel.Instance.Show_ActionPoint = false;
		switch (sceneNo)
		{
		case Enums.SceneIDS.ActualMainGame:
			OnScreenText.Instance.initOST();
			MainViewModel.Instance.Show_InGame = true;
			MainViewModel.Instance.Show_MP_LoadingBlack = false;
			MainViewModel.Instance.Compass_Vis = ConfigSettings.Settings_Compass;
			if (ConfigSettings.Settings_ShowGameTime && !MainViewModel.Instance.IsMapEditorMode)
			{
				MainViewModel.Instance.ShowGameTime = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.ShowGameTime = (Visibility)0;
			}
			if (ConfigSettings.Settings_ShowLocalTime)
			{
				MainViewModel.Instance.ShowLocalTime = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.ShowLocalTime = (Visibility)0;
			}
			TilemapManager.instance.ClearTilemap();
			MainViewModel.Instance.ScaleIngameUI(ConfigSettings.Settings_UIScale);
			GameMap.instance.clearSprites();
			MainViewModel.Instance.MapLowerEdgeMaskImage = MainViewModel.Instance.GameSprites[87];
			MainViewModel.Instance.HUDmain.SetupModeDependantUI();
			((UIElement)MainViewModel.Instance.HUDmain.RefGameUndoButton).IsEnabled = false;
			MainViewModel.Instance.HUDmain.SetRolloverSelected(0, "");
			MainViewModel.Instance.HUDmain.SetEnginePanelText(0, 0, force: true);
			MainViewModel.Instance.HUDmain.UpdateRollover();
			if ((BaseComponent)(object)MainViewModel.Instance.HUDScenario != (BaseComponent)null)
			{
				((UIElement)MainViewModel.Instance.HUDScenario).IsEnabled = true;
				((UIElement)MainViewModel.Instance.HUDScenarioPopup).IsEnabled = true;
			}
			MainControls.instance.forceUIState(state: true);
			MainViewModel.Instance.RadarMargin = "0,0,103,8";
			MainViewModel.Instance.RadarPlusMargin = "0,0,94,135";
			MainViewModel.Instance.RadarMinusMargin = "0,0,94,125";
			MainViewModel.Instance.HUDmain.ResetTutorialArrows();
			if (MainViewModel.Instance.IsMapEditorMode)
			{
				MainViewModel.Instance.DefaultMapEditorUIGameAction();
				((UIElement)MainViewModel.Instance.HUDmain.RefGameInfoButton).IsEnabled = false;
				((ToggleButton)MainViewModel.Instance.HUDmain.RefButtonBuildModeBuildings).IsChecked = true;
				((ToggleButton)MainViewModel.Instance.HUDmain.RefButtonMEHeightControls).IsChecked = true;
				MainViewModel.Instance.HUDmain.SetupNewMEScreen(1, ignoreSetupCall: true);
				MainViewModel.Instance.HUDmain.SetEditorModeButtonVisibilityForSiegeThatMode(visible: true);
				MainViewModel.Instance.Compass_Margin = "0,50,10,0";
			}
			else
			{
				MainViewModel.Instance.DefaultGameUIGameAction();
				((UIElement)MainViewModel.Instance.HUDmain.RefGameInfoButton).IsEnabled = true;
				MainViewModel.Instance.Compass_Margin = "0,2,2,0";
			}
			MainControls.instance.setUIState(state: true);
			break;
		case Enums.SceneIDS.Intro:
			MainViewModel.Instance.Intro_Sequence.Init();
			setInfoDisplayVisible(visible: false);
			break;
		case Enums.SceneIDS.Story:
			MainViewModel.Instance.Show_Story = true;
			break;
		case Enums.SceneIDS.FrontEnd:
			setInfoDisplayVisible(visible: false);
			Platform_Multiplayer.Instance.gameMembers = null;
			FrontendMenus.OpenFrontEndMenus();
			if (!temp_intro_speech_played)
			{
				temp_intro_speech_played = true;
				SFXManager.instance.playIntroSpeech(ConfigSettings.Settings_UserName);
			}
			SFXManager.instance.playMusic(3, fadePrevious: false, 1f, restartOnSamePiece: false);
			break;
		}
		currentScene = sceneNo;
	}

	public void ResizeStoryWindow()
	{
		int width = Screen.width;
		int height = Screen.height;
		int num = 1365;
		int num2 = 768;
		int num3 = 1920;
		int num4 = 1080;
		float num5 = (float)width / (float)num;
		float num6 = (float)height / (float)num2;
		float num7 = Mathf.Min(num5, num6);
		MainViewModel.Instance.BriefingViewboxWidth = (int)((float)num3 * num7);
		MainViewModel.Instance.BriefingViewboxHeight = (int)((float)num4 * num7);
	}

	public void DelayedSwitchToScene2()
	{
		DelayedSwitchToScene2Time = DateTime.UtcNow.AddSeconds(0.5);
	}

	public void OnApplicationQuit()
	{
		MinimumWindowSize.Reset();
	}

	public void ExitApp()
	{
		exiting = true;
		Application.Quit();
	}

	public void setInfoDisplayVisible(bool visible)
	{
		if ((Object)(object)AFPSCounter.Instance != (Object)null)
		{
			((BaseCounterData)AFPSCounter.Instance.deviceInfoCounter).Enabled = visible;
		}
	}

	public void MonitorScreenResolutions()
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Invalid comparison between Unknown and I4
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Invalid comparison between Unknown and I4
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Invalid comparison between Unknown and I4
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Invalid comparison between Unknown and I4
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0297: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Invalid comparison between Unknown and I4
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0304: Invalid comparison between Unknown and I4
		//IL_0306: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0326: Unknown result type (might be due to invalid IL or missing references)
		//IL_032b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_0341: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		if (saveWindowSizeChange != DateTime.MinValue && saveWindowSizeChange < DateTime.UtcNow)
		{
			if (firstScreenChange)
			{
				firstScreenChange = false;
			}
			else
			{
				ConfigSettings.SaveSettings(onlyWhenAlreadyExists: true);
			}
			saveWindowSizeChange = DateTime.MinValue;
		}
		bool flag = false;
		Resolution currentResolution;
		if (lastFullscreenMode != Screen.fullScreenMode || !screenModeSet)
		{
			if (((int)lastFullscreenMode == 1 || (int)lastFullscreenMode == 0) && (int)Screen.fullScreenMode == 3 && ConfigSettings.Settings_LastWindowWidth > 0)
			{
				int num = ConfigSettings.Settings_LastWindowWidth;
				int num2 = ConfigSettings.Settings_LastWindowHeight;
				if (num < 1280)
				{
					num = 1280;
				}
				else if (num > Screen.mainWindowDisplayInfo.width)
				{
					num = Screen.mainWindowDisplayInfo.width;
				}
				if (num2 < 768)
				{
					num2 = 768;
				}
				else if (num2 > Screen.mainWindowDisplayInfo.height)
				{
					num2 = Screen.mainWindowDisplayInfo.height;
				}
				Screen.SetResolution(num, num2, false, 0);
				if (MainViewModel.viewModelLoaded)
				{
					MainViewModel.Instance.ScaleIngameUI(ConfigSettings.Settings_UIScale);
				}
			}
			if ((int)Screen.fullScreenMode == 0 || (int)Screen.fullScreenMode == 1)
			{
				bool flag2 = false;
				if (((int)Screen.fullScreenMode == 1 && ConfigSettings.Settings_LastFullscreenType == 0) || ((int)Screen.fullScreenMode == 0 && ConfigSettings.Settings_LastFullscreenType == 1))
				{
					flag2 = true;
				}
				if (ConfigSettings.Settings_LastFullscreenWidth > -1)
				{
					if (!(Screen.width != ConfigSettings.Settings_LastFullscreenWidth || Screen.height != ConfigSettings.Settings_LastFullscreenHeight || flag2))
					{
						int settings_LastFullscreenRefresh = ConfigSettings.Settings_LastFullscreenRefresh;
						currentResolution = Screen.currentResolution;
						if (settings_LastFullscreenRefresh == ((Resolution)(ref currentResolution)).refreshRate || ConfigSettings.Settings_LastFullscreenType != 0)
						{
							goto IL_01a3;
						}
					}
					FullScreenMode val = ((ConfigSettings.Settings_LastFullscreenType != 0) ? ((FullScreenMode)1) : ((FullScreenMode)0));
					Screen.SetResolution(ConfigSettings.Settings_LastFullscreenWidth, ConfigSettings.Settings_LastFullscreenHeight, val, ConfigSettings.Settings_LastFullscreenRefresh);
					flag = true;
				}
			}
			goto IL_01a3;
		}
		goto IL_01f6;
		IL_01f6:
		if (lastScreenWidth == Screen.width && lastScreenHeight == Screen.height)
		{
			return;
		}
		if ((Object)(object)PerfectPixelWithZoom.instance != (Object)null && !PerfectPixelWithZoom.instance.CanUserExtraZoom())
		{
			PerfectPixelWithZoom.instance.limitZoomOnResChange();
		}
		if (lastScreenWidth >= 0)
		{
			if (MainViewModel.Instance.Show_HUD_Briefing)
			{
				MainViewModel.Instance.ResizeBriefingScreen();
			}
			if (MainViewModel.viewModelLoaded)
			{
				MainViewModel.Instance.ScaleIngameUI(ConfigSettings.Settings_UIScale);
			}
			if (MainViewModel.Instance.Show_Frontend)
			{
				MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
			}
			else if (MainViewModel.viewModelLoaded)
			{
				FrontendMenus.UpdateVideoScale();
			}
		}
		else
		{
			lastFullscreenMode = Screen.fullScreenMode;
		}
		if (exiting)
		{
			return;
		}
		lastScreenWidth = Screen.width;
		lastScreenHeight = Screen.height;
		if ((int)Screen.fullScreenMode == 3)
		{
			ConfigSettings.Settings_LastWindowWidth = lastScreenWidth;
			ConfigSettings.Settings_LastWindowHeight = lastScreenHeight;
			saveWindowSizeChange = DateTime.UtcNow.AddSeconds(1.0);
		}
		else
		{
			if ((int)Screen.fullScreenMode != 1 && (int)Screen.fullScreenMode != 0)
			{
				return;
			}
			if (!flag)
			{
				ConfigSettings.Settings_LastFullscreenWidth = lastScreenWidth;
				ConfigSettings.Settings_LastFullscreenHeight = lastScreenHeight;
				currentResolution = Screen.currentResolution;
				ConfigSettings.Settings_LastFullscreenRefresh = ((Resolution)(ref currentResolution)).refreshRate;
				if (ConfigSettings.Settings_LastFullscreenType == -1)
				{
					if ((int)Screen.fullScreenMode == 0)
					{
						ConfigSettings.Settings_LastFullscreenType = 0;
					}
					else
					{
						ConfigSettings.Settings_LastFullscreenType = 1;
					}
				}
			}
			saveWindowSizeChange = DateTime.UtcNow.AddSeconds(1.0);
		}
		return;
		IL_01a3:
		if (!screenModeSet)
		{
			if (ConfigSettings.Settings_LockCursor)
			{
				Cursor.lockState = (CursorLockMode)2;
			}
			else
			{
				Cursor.lockState = (CursorLockMode)0;
			}
		}
		screenModeSet = true;
		lastFullscreenMode = Screen.fullScreenMode;
		if (ConfigSettings.Settings_LastFullscreenRefresh > 0)
		{
			HUD_Options.SetVSync(ConfigSettings.Settings_Vsync);
		}
		else
		{
			Application.targetFrameRate = 300;
			QualitySettings.vSyncCount = 0;
		}
		goto IL_01f6;
	}

	public void SetNoesisKeyboardState(bool state)
	{
		noesisHasKeyboard = state;
		if ((Object)(object)m_MainCamera == (Object)null)
		{
			m_MainCamera = ((Component)((Component)Camera.main).transform).GetComponent<Camera>();
		}
		NGview = ((Component)m_MainCamera).GetComponent<NoesisView>();
		NGview.EnableKeyboard = state;
	}
}
