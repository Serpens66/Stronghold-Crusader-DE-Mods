using System;
using Noesis;
using NoesisApp;
using Steamworks;
using UnityEngine;

namespace CrusaderDE;

public class FrontendMenus : UserControl
{
	public static int CurrentSelectedTrailMission = -1;

	public static int CurrentSelectedTrail2Mission = -1;

	public static int CurrentSelectedTrail3Mission = -1;

	public static int CurrentSelectedTrailSands1Mission = -1;

	public static int CurrentSelectedTrailSands2Mission = -1;

	public static int CurrentSelectedTrailSands3Mission = -1;

	public static int CurrentSelectedTrailSands4Mission = -1;

	public static int CurrentSelectedTrailSands5Mission = -1;

	public static int CurrentSelectedTrailSands6Mission = -1;

	public static int CurrentSelectedTrailSands7Mission = -1;

	public static int CurrentSelectedTrailCoop3Mission = -1;

	public static int CurrentSelectedTrailSands8Mission = -1;

	public static int CurrentSelectedTrailCoop4Mission = -1;

	public static int CurrentSelectedCustomTrailMission = -1;

	public static int CurrentSelectedTrailCoop1Mission = -1;

	public static int CurrentSelectedTrailCoop2Mission = -1;

	private int CurrentSelectedHistoricalMission = -1;

	public static int CurrentSelectedHistorical1Mission = -1;

	public static int CurrentSelectedHistorical2Mission = -1;

	public static int CurrentSelectedHistorical3Mission = -1;

	public static int CurrentSelectedHistorical4Mission = -1;

	public static int CurrentSelectedHistorical5Mission = -1;

	public static int CurrentSelectedHistorical6Mission = -1;

	public static int CurrentSelectedHistorical7Mission = -1;

	public static int CurrentSelectedTrail = 0;

	private static bool ControlSchemeOptionsShown = false;

	private bool ControlSchemeModernSelected = true;

	private static bool ForceShowTutorialHelp = false;

	private static bool loadingStill = false;

	public Storyboard refModernControlsEnlarge;

	public Storyboard refModernControlsShrink;

	public Storyboard refClassicControlsEnlarge;

	public Storyboard refClassicControlsShrink;

	private static Border refControlSchemeModernBorder;

	private static Border refControlSchemeClassicBorder;

	public static Storyboard refHandButtonAnim;

	public static TextBlock RefDeselectText;

	public Storyboard refShowModernKeybinds;

	public Storyboard refShowClassicKeybinds;

	public Storyboard refMainMenu_ShowMainMenu;

	public Storyboard refMainMenu_ShowCombat;

	public Storyboard refMainMenu_ShowEco;

	public Storyboard refMainMenu_ShowDLC;

	public Storyboard refMainMenu_ShowTrails;

	public Storyboard refMainMenu_ShowSkirmish;

	public MediaElement refFrontendBackVideo;

	public MediaElement refMissionOverVideo;

	public static bool isVideoPlaying = false;

	public Image refLogoMain;

	public Image refLogoLoading;

	public Image refADImage;

	public Image refTrailChicken;

	public CheckBox refSandsTimeDisable;

	public Button refRoadmapDLC1Buy;

	public Button refRoadmapDLC2Buy;

	private bool loopBackgroundVideo;

	private int currentDifficultySetting = 1;

	private static int sandsSpeechVariant = 0;

	private static bool sh4Shown = false;

	private static DateTime sh4StartTime = DateTime.MinValue;

	private static bool dlcsChecked = false;

	public static bool DLC1Owned = false;

	public static bool DLC2Owned = false;

	public static bool DLC3Owned = false;

	public static bool DLC4Owned = false;

	public static bool newsletterSignUp = false;

	private static DateTime newsLetterCheck = DateTime.MinValue;

	public string CustomTrailName = "";

	public int CustomTrailLength;

	public int CustomTrailProgress;

	private DateTime richardAfraidTime = DateTime.MinValue;

	private DateTime lastRolloverSoundTime = DateTime.MinValue;

	private int[] trailLocations = new int[100]
	{
		550, 546, 772, 543, 986, 555, 1279, 535, 1397, 434,
		1523, 350, 1781, 277, 2026, 237, 2265, 264, 2286, 360,
		2104, 460, 1790, 693, 1551, 732, 1309, 668, 1079, 710,
		834, 719, 613, 793, 516, 936, 593, 1056, 789, 1150,
		1084, 1054, 1300, 899, 1579, 879, 1904, 900, 2108, 801,
		2191, 698, 2295, 591, 2419, 497, 2584, 520, 2498, 645,
		2388, 731, 2560, 804, 2811, 796, 2834, 938, 3040, 1171,
		2905, 1331, 2560, 1376, 2400, 1250, 2305, 1057, 2055, 994,
		1974, 1204, 2019, 1469, 1814, 1600, 1514, 1644, 1327, 1533,
		1349, 1374, 1082, 1360, 813, 1481, 737, 1631, 835, 1834
	};

	private int[] sword_ang = new int[50]
	{
		3, 0, 2, 0, 3, 2, 0, 2, 3, 1,
		3, 0, 1, 2, 0, 1, 2, 3, 2, 1,
		0, 1, 2, 0, 3, 2, 0, 1, 2, 1,
		3, 0, 1, 2, 0, 1, 2, 3, 2, 1,
		0, 1, 2, 0, 3, 2, 0, 2, 1, 3
	};

	private int[] trail2Locations = new int[60]
	{
		329, 529, 529, 580, 657, 520, 868, 468, 1125, 416,
		1363, 383, 1613, 333, 1901, 351, 2116, 245, 2386, 298,
		2316, 378, 2394, 436, 2540, 550, 2688, 733, 2910, 1084,
		2539, 1179, 2197, 877, 2112, 544, 1915, 480, 1625, 500,
		1382, 597, 1589, 752, 1775, 997, 2015, 1162, 1492, 1582,
		1412, 1354, 902, 1334, 1322, 1054, 1022, 897, 570, 757
	};

	private int[] trail3Locations = new int[40]
	{
		529, 540, 444, 679, 448, 818, 507, 983, 611, 1139,
		770, 1296, 1020, 1424, 1322, 1520, 1680, 1557, 2078, 1485,
		2289, 1291, 2576, 1168, 2793, 1059, 2665, 909, 2358, 850,
		1839, 648, 1644, 748, 1318, 739, 1381, 492, 1539, 366
	};

	private int[] sands1Locations = new int[10] { 1900, 524, 2152, 668, 2587, 668, 2450, 844, 2072, 1131 };

	private int[] sands2Locations = new int[14]
	{
		2265, 420, 2363, 555, 2217, 661, 2385, 770, 2330, 920,
		2469, 1083, 2819, 1128
	};

	private int[] sands3Locations = new int[18]
	{
		2374, 333, 2135, 411, 2054, 546, 2013, 733, 2393, 737,
		2726, 755, 2817, 939, 2476, 1085, 2282, 1261
	};

	private int[] sands4Locations = new int[22]
	{
		1698, 529, 1937, 392, 2280, 359, 2567, 431, 2674, 581,
		2398, 679, 2137, 770, 2548, 835, 2637, 1031, 2839, 1172,
		3097, 1096
	};

	private int[] sands5Locations = new int[18]
	{
		1824, 316, 2174, 357, 2467, 522, 2793, 642, 2850, 876,
		2445, 998, 1902, 918, 2100, 1211, 1739, 1391
	};

	private int[] sands6Locations = new int[18]
	{
		1948, 1163, 2139, 892, 1922, 705, 1835, 542, 2122, 405,
		2491, 463, 2730, 609, 2847, 807, 2915, 1094
	};

	private int[] sands7Locations = new int[18]
	{
		2065, 1139, 2126, 857, 1720, 794, 1843, 581, 2200, 450,
		2587, 505, 2900, 648, 2978, 879, 2624, 1120
	};

	private int[] sands8Locations = new int[18]
	{
		2102, 1241, 2648, 1150, 2874, 915, 2804, 663, 2545, 463,
		2100, 392, 1752, 492, 1920, 794, 2376, 689
	};

	private int[] coop1Locations = new int[20]
	{
		2280, 235, 1904, 319, 1863, 444, 2180, 518, 2813, 653,
		2602, 844, 3208, 1024, 2878, 1287, 2354, 1133, 2211, 942
	};

	private int[] coop2Locations = new int[20]
	{
		2364, 802, 2148, 983, 2971, 926, 3180, 826, 2845, 720,
		2652, 653, 2391, 644, 2113, 653, 1904, 487, 2450, 179
	};

	private int[] coop3Locations = new int[20]
	{
		2661, 1100, 2630, 898, 2504, 779, 2378, 672, 2172, 592,
		1946, 555, 1850, 481, 2048, 422, 1948, 309, 1729, 309
	};

	private int[] coop4Locations = new int[20]
	{
		2376, 1159, 2674, 1118, 2865, 1024, 2956, 887, 2921, 735,
		2734, 616, 2652, 479, 2445, 413, 2150, 383, 1891, 390
	};

	private int[] trailChickens = new int[50]
	{
		483, 510, 485, 507, 485, 509, 511, 487, 486, 514,
		488, 516, 484, 506, 496, 510, 504, 497, 513, 498,
		499, 507, 500, 501, 505, 515, 502, 489, 490, 523,
		508, 491, 522, 493, 504, 517, 492, 524, 500, 518,
		493, 519, 494, 495, 521, 487, 517, 525, 503, 520
	};

	private int sktrail_knights_anim;

	private DateTime knight_anim_time = DateTime.UtcNow;

	private DateTime chickenAnimTime = DateTime.UtcNow;

	private bool chickenRollover;

	private bool trailCompleted;

	private bool trailCompletedWithCheats;

	public FrontendMenus()
	{
		base.DataContext = MainViewModel.Instance;
		MainViewModel.Instance.FrontEndMenu = this;
		InitializeComponent();
		refMissionOverVideo = (MediaElement)FindName("MissionOverVideo");
		refFrontendBackVideo = (MediaElement)FindName("FrontendBackVideo");
		refFrontendBackVideo.MediaEnded += FrontendBackVideo_Ended;
		refFrontendBackVideo.MediaOpened += FrontendBackVideo_Opened;
		Uri source = ((Screen.width > 1920 || Screen.height > 1080) ? new Uri("Assets/GUI/Video/front_end_background.webm", UriKind.Relative) : new Uri("Assets/GUI/Video/front_end_background_low.webm", UriKind.Relative));
		refFrontendBackVideo.Source = source;
		refModernControlsEnlarge = (Storyboard)TryFindResource("EnlargeModern");
		refModernControlsShrink = (Storyboard)TryFindResource("ShrinkModern");
		refClassicControlsEnlarge = (Storyboard)TryFindResource("EnlargeClassic");
		refClassicControlsShrink = (Storyboard)TryFindResource("ShrinkClassic");
		refHandButtonAnim = (Storyboard)TryFindResource("HandButtonAnim");
		refMainMenu_ShowMainMenu = (Storyboard)TryFindResource("MainMenu_ShowMainMenu");
		refMainMenu_ShowCombat = (Storyboard)TryFindResource("MainMenu_ShowCombat");
		refMainMenu_ShowEco = (Storyboard)TryFindResource("MainMenu_ShowEco");
		refMainMenu_ShowDLC = (Storyboard)TryFindResource("MainMenu_ShowDLC");
		refMainMenu_ShowTrails = (Storyboard)TryFindResource("MainMenu_ShowTrails");
		refMainMenu_ShowSkirmish = (Storyboard)TryFindResource("MainMenu_ShowSkirmish");
		refShowModernKeybinds = (Storyboard)TryFindResource("ShowModernKeybinds");
		refShowClassicKeybinds = (Storyboard)TryFindResource("ShowClassicKeybinds");
		refControlSchemeModernBorder = (Border)FindName("ControlSchemeModernBorder");
		refControlSchemeClassicBorder = (Border)FindName("ControlSchemeClassicBorder");
		RefDeselectText = (TextBlock)FindName("DeselectText");
		refLogoMain = (Image)FindName("LogoMain");
		refLogoLoading = (Image)FindName("LogoLoading");
		refADImage = (Image)FindName("ADImage");
		refLogoMain.Source = MainViewModel.Instance.GetImage(Enums.eImages.IMAGE_FRONTEND_LOGO);
		refLogoLoading.Source = MainViewModel.Instance.GetImage(Enums.eImages.IMAGE_FRONTEND_LOGO);
		refADImage.Source = MainViewModel.Instance.GetImage(Enums.eImages.IMAGE_AD);
		refSandsTimeDisable = (CheckBox)FindName("SandsTimeDisable");
		refRoadmapDLC1Buy = (Button)FindName("RoadmapDLC1Buy");
		refRoadmapDLC2Buy = (Button)FindName("RoadmapDLC2Buy");
	}

	public static void ClearUIPanels(bool frontEndState = true, bool logo = true, bool clearAvatarCache = true)
	{
		if (isVideoPlaying)
		{
			MainViewModel.Instance.FrontEndMenu.refFrontendBackVideo.Pause();
			isVideoPlaying = false;
		}
		MainViewModel.Instance.Show_Frontend = frontEndState;
		MainViewModel.Instance.Show_Frontend_MainMenu = false;
		MainViewModel.Instance.Show_Frontend_OtherModes = false;
		MainViewModel.Instance.Show_FrontEndCombat_Skirmish_Help = false;
		MainViewModel.Instance.Show_FrontEndCombat_Historical_Help = false;
		MainViewModel.Instance.Show_FrontEndCombat_Sandbox_Help = false;
		MainViewModel.Instance.Show_FrontEndCombat_MP_Help = false;
		MainViewModel.Instance.Show_FrontEndCombat_ME_Help = false;
		MainViewModel.Instance.Show_FrontEndCombat_Roadmap_Help = false;
		MainViewModel.Instance.Show_FrontEndSkirmish_Trail_Help = false;
		MainViewModel.Instance.Show_FrontEndSkirmish_Sands_Help = false;
		MainViewModel.Instance.Show_FrontEndSkirmish_Coop_Help = false;
		MainViewModel.Instance.Show_FrontEndSkirmish_Custom_Help = false;
		MainViewModel.Instance.Show_FrontEndSkirmish_CustomTrail_Help = false;
		MainViewModel.Instance.Show_FrontEndSkirmish_Invasion_Help = false;
		MainViewModel.Instance.Show_FrontEndSkirmish_Freebuild_Help = false;
		MainViewModel.Instance.Show_FrontEndHistorical_1_Help = false;
		MainViewModel.Instance.Show_FrontEndHistorical_2_Help = false;
		MainViewModel.Instance.Show_FrontEndHistorical_3_Help = false;
		MainViewModel.Instance.Show_FrontEndHistorical_4_Help = false;
		MainViewModel.Instance.Show_FrontEndHistorical_5_Help = false;
		MainViewModel.Instance.Show_FrontEndHistorical_6_Help = false;
		MainViewModel.Instance.Show_FrontEndHistorical_7_Help = false;
		MainViewModel.Instance.Show_Frontend_Roadmap = false;
		MainViewModel.Instance.Show_HUD_CustomTrails = false;
		MainViewModel.Instance.Show_SH4AdIntro = false;
		MainViewModel.Instance.Show_Frontend_Eco = false;
		MainViewModel.Instance.Show_Frontend_Historical = false;
		MainViewModel.Instance.Show_Frontend_Skirmish = false;
		MainViewModel.Instance.Show_Frontend_Skirmish_Trails = false;
		MainViewModel.Instance.Show_CoopTrail1 = false;
		MainViewModel.Instance.Show_CoopTrail2 = false;
		MainViewModel.Instance.Show_CoopTrail3 = false;
		MainViewModel.Instance.Show_CoopTrail4 = false;
		MainViewModel.Instance.Show_Frontend_Coop = false;
		MainViewModel.Instance.Show_Frontend_Controls_Selection = false;
		MainViewModel.Instance.Show_FrontMenus = true;
		MainViewModel.Instance.Show_FrontMenus_Background_Historical = false;
		MainViewModel.Instance.Show_FrontMenus_Background_Main = true;
		MainViewModel.Instance.Show_FrontMenusTrailsBackground = false;
		MainViewModel.Instance.Show_FrontMenusTrailsSandsBackground = false;
		MainViewModel.Instance.Show_Historical1CampaignMenu = false;
		MainViewModel.Instance.Show_Historical2CampaignMenu = false;
		MainViewModel.Instance.Show_Historical3CampaignMenu = false;
		MainViewModel.Instance.Show_Historical4CampaignMenu = false;
		MainViewModel.Instance.Show_Historical5CampaignMenu = false;
		MainViewModel.Instance.Show_Historical6CampaignMenu = false;
		MainViewModel.Instance.Show_Historical7CampaignMenu = false;
		MainViewModel.Instance.Show_TrailCampaignMenu = false;
		MainViewModel.Instance.Show_Trail2CampaignMenu = false;
		MainViewModel.Instance.Show_Trail3CampaignMenu = false;
		MainViewModel.Instance.Show_SandsTrail1Menu = false;
		MainViewModel.Instance.Show_SandsTrail2Menu = false;
		MainViewModel.Instance.Show_SandsTrail3Menu = false;
		MainViewModel.Instance.Show_SandsTrail4Menu = false;
		MainViewModel.Instance.Show_SandsTrail5Menu = false;
		MainViewModel.Instance.Show_SandsTrail6Menu = false;
		MainViewModel.Instance.Show_SandsTrail7Menu = false;
		MainViewModel.Instance.Show_SandsTrail8Menu = false;
		MainViewModel.Instance.Show_TrailCustomisationButtons = true;
		MainViewModel.Instance.Show_Frontend_Skirmish_Sands = false;
		MainViewModel.Instance.Show_StandaloneSetup = false;
		MainViewModel.Instance.Show_MultiplayerSetup = false;
		MainViewModel.Instance.Show_Credits = false;
		MainViewModel.Instance.Show_MapEditor = false;
		HUD_Leaderboard.CloseLeaderboard();
		MainViewModel.Instance.IngameUI.clearVideos();
		MainViewModel.Instance.Show_Frontend_Logo = logo;
		MainViewModel.Instance.ChickenCheatRolloverVis = Visibility.Hidden;
		MainViewModel.Instance.Show_Sands_Intro_Text = ConfigSettings.Settings_ShowSandsIntro;
		MainViewModel.Instance.Show_ClassicTrailsOptions = false;
		refHandButtonAnim.Stop();
		if (clearAvatarCache)
		{
			Platform_Multiplayer.Instance.ClearSteamAvatarCache();
		}
	}

	public static void OpenFrontEndMenus()
	{
		MainViewModel.Instance.FrontEndMenu.Init();
		ClearUIPanels();
		MainViewModel.Instance.Show_FrontMenus = true;
		MainViewModel.Instance.DLC4PIP1 = ConfigSettings.Settings_DLC4_Pip1;
		MainViewModel.Instance.DLC4PIP2 = ConfigSettings.Settings_DLC4_Pip2;
		MainViewModel.Instance.DLC4PIP3 = ConfigSettings.Settings_DLC4_Pip3;
		MainViewModel.Instance.DLC4PIP4 = ConfigSettings.Settings_DLC4_Pip4;
		MainViewModel.Instance.DLC4PIP5 = ConfigSettings.Settings_DLC4_Pip5;
		MainViewModel.Instance.DLC4PIP6 = ConfigSettings.Settings_DLC4_Pip6;
		switch (FatControler.locale)
		{
		case "kokr":
		case "jajp":
		case "zhcn":
		case "zhhk":
			MainViewModel.Instance.FrontEndButtonMargin = "132,0,0,12";
			break;
		case "thth":
			MainViewModel.Instance.FrontEndButtonMargin = "132,0,0,16";
			MainViewModel.Instance.DemoFrontEndButtonFontSize = 38.0;
			break;
		}
		if (!ConfigSettings.SettingsFileExisted && !ControlSchemeOptionsShown)
		{
			MainViewModel.Instance.Show_FrontMenus_Background_Main = true;
			ControlSchemeOptionsShown = true;
			ForceShowTutorialHelp = true;
			MainViewModel.Instance.Show_Frontend_Controls_Selection = true;
			MainViewModel.Instance.FrontEndMenu.refModernControlsEnlarge.Begin();
			MainViewModel.Instance.Show_Frontend_Logo = false;
			refControlSchemeModernBorder.Visibility = Visibility.Visible;
			refControlSchemeClassicBorder.Visibility = Visibility.Hidden;
			refHandButtonAnim.Begin();
			MainViewModel.Instance.FrontEndMenu.UpdateTutorialHelpVisibility(state: true);
		}
		else if (spriteLoader.instance.spritesLoaded)
		{
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_Frontend_MainMenu = true;
			MainViewModel.Instance.FrontEndMenu.refMainMenu_ShowMainMenu.Begin();
			loadingStill = false;
			if (!sh4Shown)
			{
				sh4Shown = true;
				sh4StartTime = DateTime.UtcNow.AddSeconds(10.0);
				MainViewModel.Instance.Show_SH4AdIntro = true;
			}
		}
		else
		{
			loadingStill = true;
		}
	}

	public static void triggerNewsLetterMonitor()
	{
		newsLetterCheck = DateTime.UtcNow.AddMinutes(2.0);
	}

	public static void MonitorNewsLetter()
	{
		if (!dlcsChecked || newsletterSignUp || !SteamManager.Initialized || !(newsLetterCheck != DateTime.MinValue) || !(DateTime.UtcNow > newsLetterCheck))
		{
			return;
		}
		newsLetterCheck = DateTime.UtcNow.AddMinutes(2.0);
		Director.instance.SignupNewsletter(ConfigSettings.Settings_NewsletterEmail, delegate
		{
			if (ConfigSettings.Settings_NewsletterEmail.Length == 0)
			{
				newsletterSignUp = true;
			}
		}, showRequester: false, checkCall: true);
	}

	private void Init()
	{
		if (!dlcsChecked)
		{
			dlcsChecked = true;
			if (SteamManager.Initialized)
			{
				if (SteamApps.BIsDlcInstalled(new AppId_t(3030340u)))
				{
					DLC1Owned = true;
				}
				if (SteamApps.BIsDlcInstalled(new AppId_t(3030350u)))
				{
					DLC2Owned = true;
				}
				if (SteamApps.BIsDlcInstalled(new AppId_t(4483540u)))
				{
					DLC3Owned = true;
				}
				if (SteamApps.BIsDlcInstalled(new AppId_t(4483530u)))
				{
					DLC4Owned = true;
				}
			}
			refRoadmapDLC1Buy.IsHitTestVisible = !DLC3Owned;
			if (DLC3Owned)
			{
				PropEx.SetTextCentre(refRoadmapDLC1Buy, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 378));
				refRoadmapDLC1Buy.IsHitTestVisible = false;
			}
			else
			{
				PropEx.SetTextCentre(refRoadmapDLC1Buy, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 379));
				refRoadmapDLC1Buy.IsHitTestVisible = true;
			}
			refRoadmapDLC2Buy.IsHitTestVisible = !DLC4Owned;
			if (DLC4Owned)
			{
				PropEx.SetTextCentre(refRoadmapDLC2Buy, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 378));
				refRoadmapDLC2Buy.IsHitTestVisible = false;
			}
			else
			{
				PropEx.SetTextCentre(refRoadmapDLC2Buy, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 379));
				refRoadmapDLC2Buy.IsHitTestVisible = true;
			}
			MainViewModel.Instance.InitDLCAILordVisibility();
		}
		if (CurrentSelectedHistoricalMission < 0)
		{
			CurrentSelectedTrailMission = ConfigSettings.Settings_Progress_Trail;
			if (CurrentSelectedTrailMission > 50)
			{
				CurrentSelectedTrailMission = 50;
			}
			CurrentSelectedTrail2Mission = ConfigSettings.Settings_Progress_Trail2;
			if (CurrentSelectedTrail2Mission > 30)
			{
				CurrentSelectedTrail2Mission = 30;
			}
			CurrentSelectedTrail3Mission = ConfigSettings.Settings_Progress_Trail3;
			if (CurrentSelectedTrail3Mission > 20)
			{
				CurrentSelectedTrail3Mission = 20;
			}
			CurrentSelectedTrailSands1Mission = ConfigSettings.Settings_Progress_Trail_Sands1;
			if (CurrentSelectedTrailSands1Mission > 5)
			{
				CurrentSelectedTrailSands1Mission = 5;
			}
			CurrentSelectedTrailSands2Mission = ConfigSettings.Settings_Progress_Trail_Sands2;
			if (CurrentSelectedTrailSands2Mission > 7)
			{
				CurrentSelectedTrailSands2Mission = 7;
			}
			CurrentSelectedTrailSands3Mission = ConfigSettings.Settings_Progress_Trail_Sands3;
			if (CurrentSelectedTrailSands3Mission > 9)
			{
				CurrentSelectedTrailSands3Mission = 9;
			}
			CurrentSelectedTrailSands4Mission = ConfigSettings.Settings_Progress_Trail_Sands4;
			if (CurrentSelectedTrailSands4Mission > 11)
			{
				CurrentSelectedTrailSands4Mission = 11;
			}
			CurrentSelectedTrailSands5Mission = ConfigSettings.Settings_Progress_Trail_Sands5;
			if (CurrentSelectedTrailSands5Mission > 9)
			{
				CurrentSelectedTrailSands5Mission = 9;
			}
			CurrentSelectedTrailSands6Mission = ConfigSettings.Settings_Progress_Trail_Sands6;
			if (CurrentSelectedTrailSands6Mission > 9)
			{
				CurrentSelectedTrailSands6Mission = 9;
			}
			CurrentSelectedTrailSands7Mission = ConfigSettings.Settings_Progress_Trail_Sands7;
			if (CurrentSelectedTrailSands7Mission > 9)
			{
				CurrentSelectedTrailSands7Mission = 9;
			}
			CurrentSelectedTrailCoop3Mission = 1;
			CurrentSelectedTrailSands8Mission = ConfigSettings.Settings_Progress_Trail_Sands8;
			if (CurrentSelectedTrailSands8Mission > 9)
			{
				CurrentSelectedTrailSands8Mission = 9;
			}
			CurrentSelectedTrailCoop4Mission = 1;
			CurrentSelectedCustomTrailMission = 0;
			CurrentSelectedTrailCoop1Mission = 1;
			CurrentSelectedTrailCoop2Mission = 1;
			CurrentSelectedHistoricalMission = (CurrentSelectedHistorical1Mission = ConfigSettings.Settings_Progress_Historical1Campaign + 10);
			CurrentSelectedHistorical2Mission = ConfigSettings.Settings_Progress_Historical2Campaign + 20;
			CurrentSelectedHistorical3Mission = ConfigSettings.Settings_Progress_Historical3Campaign + 30;
			CurrentSelectedHistorical4Mission = ConfigSettings.Settings_Progress_Historical4Campaign + 40;
			CurrentSelectedHistorical5Mission = ConfigSettings.Settings_Progress_Historical5Campaign + 50;
			CurrentSelectedHistorical6Mission = ConfigSettings.Settings_Progress_Historical6Campaign + 60;
			CurrentSelectedHistorical7Mission = ConfigSettings.Settings_Progress_Historical7Campaign + 70;
			if (CurrentSelectedHistorical1Mission > 15)
			{
				CurrentSelectedHistorical1Mission = (CurrentSelectedHistoricalMission = 15);
			}
			if (CurrentSelectedHistorical2Mission > 25)
			{
				CurrentSelectedHistorical2Mission = 25;
			}
			if (CurrentSelectedHistorical3Mission > 35)
			{
				CurrentSelectedHistorical3Mission = 35;
			}
			if (CurrentSelectedHistorical4Mission > 45)
			{
				CurrentSelectedHistorical4Mission = 45;
			}
			if (CurrentSelectedHistorical5Mission > 55)
			{
				CurrentSelectedHistorical5Mission = 55;
			}
			if (CurrentSelectedHistorical6Mission > 65)
			{
				CurrentSelectedHistorical6Mission = 65;
			}
			if (CurrentSelectedHistorical7Mission > 75)
			{
				CurrentSelectedHistorical7Mission = 75;
			}
		}
		else
		{
			CurrentSelectedHistoricalMission = CurrentSelectedHistorical1Mission;
		}
		ButtonHistoricalCampaignClicked(CurrentSelectedHistoricalMission);
		UpdateTutorialHelpVisibility(state: false);
		refSandsTimeDisable.IsChecked = ConfigSettings.Settings_HideSoTTiming;
		Director.instance.setCursor(0, force: true);
	}

	public void ButtonClicked(string param)
	{
		switch (param)
		{
		case "EcoCampaign":
			break;
		case "MainCampaign":
			break;
		case "DLCECO":
			break;
		case "Siege":
			break;
		case "EcoMission":
			break;
		case "JustBuild":
			break;
		case "DEBUGSTORY":
			break;
		case "Combat":
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_Frontend_OtherModes = true;
			MainViewModel.Instance.FrontEndMenu.refMainMenu_ShowCombat.Begin();
			break;
		case "Freebuild":
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			FRONT_StandaloneMission.Open(Enums.StartUpUIPanels.FreeBuild);
			break;
		case "Historical":
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Historical_Campaign1_Complete = AchievementsCommon.Instance.IsAchievementComplete(Enums.Achievements.Complete_Campaign_1);
			MainViewModel.Instance.Historical_Campaign1_Not_Complete = !MainViewModel.Instance.Historical_Campaign1_Complete;
			MainViewModel.Instance.Historical_Campaign2_Complete = AchievementsCommon.Instance.IsAchievementComplete(Enums.Achievements.Complete_Campaign_2);
			MainViewModel.Instance.Historical_Campaign2_Not_Complete = !MainViewModel.Instance.Historical_Campaign2_Complete;
			MainViewModel.Instance.Historical_Campaign3_Complete = AchievementsCommon.Instance.IsAchievementComplete(Enums.Achievements.Complete_Campaign_3);
			MainViewModel.Instance.Historical_Campaign3_Not_Complete = !MainViewModel.Instance.Historical_Campaign3_Complete;
			MainViewModel.Instance.Historical_Campaign4_Complete = AchievementsCommon.Instance.IsAchievementComplete(Enums.Achievements.Complete_Campaign_4);
			MainViewModel.Instance.Historical_Campaign4_Not_Complete = !MainViewModel.Instance.Historical_Campaign4_Complete;
			MainViewModel.Instance.Historical_Campaign5_Complete = AchievementsCommon.Instance.IsAchievementComplete(Enums.Achievements.Complete_Campaign_5);
			MainViewModel.Instance.Historical_Campaign5_Not_Complete = !MainViewModel.Instance.Historical_Campaign5_Complete;
			MainViewModel.Instance.Historical_Campaign6_Complete = AchievementsCommon.Instance.IsAchievementComplete(Enums.Achievements.Complete_Campaign_6);
			MainViewModel.Instance.Historical_Campaign6_Not_Complete = !MainViewModel.Instance.Historical_Campaign6_Complete;
			MainViewModel.Instance.Historical_Campaign7_Complete = AchievementsCommon.Instance.IsAchievementComplete(Enums.Achievements.Complete_Campaign_7);
			MainViewModel.Instance.Historical_Campaign7_Not_Complete = !MainViewModel.Instance.Historical_Campaign7_Complete;
			MainViewModel.Instance.Show_FrontMenus = true;
			MainViewModel.Instance.Show_FrontMenus_Background_Historical = true;
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_Frontend_Historical = true;
			MainViewModel.Instance.FrontEndMenu.refMainMenu_ShowDLC.Begin();
			break;
		case "Custom":
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			FRONT_Multiplayer.Open(skirmishSetup: true);
			break;
		case "CustomTrails":
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_FrontMenusTrailsBackground = true;
			HUD_CustomTrailsSelect.OpenCustomTrails();
			break;
		case "Multiplayer":
			if (SteamUser.BLoggedOn())
			{
				ClearUIPanels(frontEndState: true, logo: false);
				MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
				FRONT_Multiplayer.Open();
			}
			else
			{
				HUD_ConfirmationPopup.ShowConfirmationOKMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 368), delegate
				{
				}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 366));
			}
			break;
		case "MapEditor":
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			FRONT_EditorSetup.Open();
			break;
		case "Tutorial":
			ForceShowTutorialHelp = false;
			MainViewModel.Instance.InitNewScene(Enums.SceneIDS.Tutorial);
			break;
		case "Options":
			UpdateFrontMenuPopupScale();
			MainViewModel.Instance.InitNewScene(Enums.SceneIDS.Options);
			break;
		case "LoadTrail":
		case "Load":
		{
			bool trailsScreen = false;
			if (param == "LoadTrail")
			{
				trailsScreen = true;
			}
			UpdateFrontMenuPopupScale();
			HUD_LoadSaveRequester.OpenLoadSaveRequester(Enums.RequesterTypes.LoadSinglePlayerGame, delegate(string filename, FileHeader header)
			{
				Platform_Multiplayer.Instance.gameMembers = null;
				MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MainGame);
				Director.instance.SetPausedState(state: false);
				EditorDirector.instance.stopGameSim();
				EditorDirector.instance.loadSaveGame(header.filePath, header.standAlone_filename, header);
				MainViewModel.Instance.InitObjectiveGoodsPanelDelayed();
			}, delegate
			{
			}, -1, skirmishScreen: false, trailsScreen);
			break;
		}
		case "Exit":
			UpdateFrontMenuPopupScale();
			SFXManager.instance.playSpeech(1, "general_quitgame.wav", 1f);
			HUD_ConfirmationPopup.ShowConfirmation(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 9), delegate
			{
				FatControler.instance.ExitApp();
			}, delegate
			{
			});
			break;
		case "BackMain":
			ClearUIPanels();
			refFrontendBackVideo.Play();
			isVideoPlaying = true;
			MainViewModel.Instance.Show_Frontend_MainMenu = true;
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.FrontEndMenu.refMainMenu_ShowMainMenu.Begin();
			break;
		case "LeaveCredits":
			SFXManager.instance.playMusic(3);
			ClearUIPanels();
			refFrontendBackVideo.Play();
			isVideoPlaying = true;
			MainViewModel.Instance.Show_Frontend_MainMenu = true;
			MainViewModel.Instance.FrontEndMenu.refMainMenu_ShowMainMenu.Begin();
			break;
		case "LeaveControls":
			ClearUIPanels();
			if (ControlSchemeModernSelected)
			{
				ConfigSettings.Settings_PushMapScrolling = false;
				KeyManager.instance.SetDefaultFunctionsNew();
				ConfigSettings.Settings_SH1RTSControls = false;
				ConfigSettings.Settings_SH1MouseWheel = false;
				ConfigSettings.Settings_SH1CentreControls = false;
			}
			else
			{
				ConfigSettings.Settings_PushMapScrolling = true;
				KeyManager.instance.SetDefaultFunctionsSH1();
				ConfigSettings.Settings_SH1RTSControls = true;
				ConfigSettings.Settings_SH1MouseWheel = true;
				ConfigSettings.Settings_SH1CentreControls = true;
			}
			ConfigSettings.SaveSettings();
			refFrontendBackVideo.Play();
			isVideoPlaying = true;
			MainViewModel.Instance.Show_Frontend_MainMenu = true;
			MainViewModel.Instance.FrontEndMenu.refMainMenu_ShowMainMenu.Begin();
			HUD_ConfirmationPopup.ShowConfirmationMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 303), delegate
			{
				ButtonClicked("Tutorial");
			}, delegate
			{
			}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 445));
			if (!sh4Shown)
			{
				sh4Shown = true;
				sh4StartTime = DateTime.UtcNow.AddSeconds(10.0);
				MainViewModel.Instance.Show_SH4AdIntro = true;
			}
			break;
		case "Historical1":
			CurrentSelectedHistoricalMission = CurrentSelectedHistorical1Mission;
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_Historical1CampaignMenu = true;
			ButtonHistoricalCampaignClicked(CurrentSelectedHistoricalMission);
			UpdateCampaignListButtonVisibility(ConfigSettings.Settings_Progress_Historical1Campaign);
			break;
		case "Historical2":
			CurrentSelectedHistoricalMission = CurrentSelectedHistorical2Mission;
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_Historical2CampaignMenu = true;
			ButtonHistoricalCampaignClicked(CurrentSelectedHistoricalMission);
			UpdateCampaignListButtonVisibility(ConfigSettings.Settings_Progress_Historical2Campaign);
			break;
		case "Historical3":
			CurrentSelectedHistoricalMission = CurrentSelectedHistorical3Mission;
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_Historical3CampaignMenu = true;
			ButtonHistoricalCampaignClicked(CurrentSelectedHistoricalMission);
			UpdateCampaignListButtonVisibility(ConfigSettings.Settings_Progress_Historical3Campaign);
			break;
		case "Historical4":
			CurrentSelectedHistoricalMission = CurrentSelectedHistorical4Mission;
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_Historical4CampaignMenu = true;
			ButtonHistoricalCampaignClicked(CurrentSelectedHistoricalMission);
			UpdateCampaignListButtonVisibility(ConfigSettings.Settings_Progress_Historical4Campaign);
			break;
		case "Historical5":
			CurrentSelectedHistoricalMission = CurrentSelectedHistorical5Mission;
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_Historical5CampaignMenu = true;
			ButtonHistoricalCampaignClicked(CurrentSelectedHistoricalMission);
			UpdateCampaignListButtonVisibility(ConfigSettings.Settings_Progress_Historical5Campaign);
			break;
		case "Historical6":
			CurrentSelectedHistoricalMission = CurrentSelectedHistorical6Mission;
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_Historical6CampaignMenu = true;
			ButtonHistoricalCampaignClicked(CurrentSelectedHistoricalMission);
			UpdateCampaignListButtonVisibility(ConfigSettings.Settings_Progress_Historical6Campaign);
			break;
		case "Historical7":
			CurrentSelectedHistoricalMission = CurrentSelectedHistorical7Mission;
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_Historical7CampaignMenu = true;
			ButtonHistoricalCampaignClicked(CurrentSelectedHistoricalMission);
			UpdateCampaignListButtonVisibility(ConfigSettings.Settings_Progress_Historical7Campaign);
			break;
		case "Invasion":
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			FRONT_StandaloneMission.Open(Enums.StartUpUIPanels.Invasion);
			break;
		case "Skirmish":
			if (ConfigSettings.Settings_DLC4_Pip1)
			{
				MainViewModel.Instance.DLC4PIP1 = (ConfigSettings.Settings_DLC4_Pip1 = false);
				ConfigSettings.SaveSettings();
			}
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus = true;
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_FrontMenusTrailsBackground = true;
			MainViewModel.Instance.Show_Frontend_Skirmish = true;
			MainViewModel.Instance.FrontEndMenu.refMainMenu_ShowSkirmish.Begin();
			break;
		case "Trails":
		{
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_ClassicTrailsOptions = false;
			updateAdvancedTrailsIcons();
			MainViewModel.Instance.Show_FrontMenus = true;
			MainViewModel.Instance.Show_Frontend_Skirmish_Trails = true;
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_FrontMenusTrailsBackground = true;
			int num = ConfigSettings.countTrailMissionsCompleted(0);
			int num2 = ConfigSettings.countTrailMissionsCompleted(1);
			int num3 = ConfigSettings.countTrailMissionsCompleted(2);
			if (!FatControler.arabic || !ConfigSettings.Settings_ArabicL2R)
			{
				MainViewModel.Instance.Trail1_CompletedText = num + " / 50 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
				MainViewModel.Instance.Trail2_CompletedText = num2 + " / 30 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
				MainViewModel.Instance.Trail3_CompletedText = num3 + " / 20 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
			}
			else
			{
				MainViewModel.Instance.Trail1_CompletedText = "50 / " + num + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
				MainViewModel.Instance.Trail2_CompletedText = "30 / " + num2 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
				MainViewModel.Instance.Trail3_CompletedText = "20 / " + num3 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
			}
			MainViewModel.Instance.FrontEndMenu.refMainMenu_ShowTrails.Begin();
			MainViewModel.Instance.Classic_Trail1_Complete = num == 50;
			MainViewModel.Instance.Classic_Trail2_Complete = num2 == 30;
			MainViewModel.Instance.Classic_Trail3_Complete = num3 == 20;
			MainViewModel.Instance.Classic_Trail1_Not_Complete = !MainViewModel.Instance.Classic_Trail1_Complete;
			MainViewModel.Instance.Classic_Trail2_Not_Complete = !MainViewModel.Instance.Classic_Trail2_Complete;
			MainViewModel.Instance.Classic_Trail3_Not_Complete = !MainViewModel.Instance.Classic_Trail3_Complete;
			break;
		}
		case "Trail":
			ClearUIPanels(frontEndState: true, logo: false);
			currentDifficultySetting = ConfigSettings.Settings_Trail1Difficulty;
			UpdateDiffcultyButton();
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_TrailCampaignMenu = true;
			CurrentSelectedTrail = 0;
			if (CurrentSelectedTrailMission >= ConfigSettings.Settings_Progress_Trail)
			{
				CurrentSelectedTrailMission = ConfigSettings.Settings_Progress_Trail;
			}
			GenerateSwords();
			refTrailChicken = FRONT_Trail.refChicken;
			ButtonTrailCampaignClicked(CurrentSelectedTrailMission);
			break;
		case "Trail2":
			ClearUIPanels(frontEndState: true, logo: false);
			currentDifficultySetting = ConfigSettings.Settings_Trail2Difficulty;
			UpdateDiffcultyButton();
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_Trail2CampaignMenu = true;
			CurrentSelectedTrail = 1;
			if (CurrentSelectedTrail2Mission >= ConfigSettings.Settings_Progress_Trail2)
			{
				CurrentSelectedTrail2Mission = ConfigSettings.Settings_Progress_Trail2;
			}
			GenerateSwords();
			refTrailChicken = FRONT_Trail2.refChicken;
			ButtonTrailCampaignClicked(CurrentSelectedTrail2Mission);
			break;
		case "Trail3":
			ClearUIPanels(frontEndState: true, logo: false);
			currentDifficultySetting = ConfigSettings.Settings_Trail3Difficulty;
			UpdateDiffcultyButton();
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_Trail3CampaignMenu = true;
			CurrentSelectedTrail = 2;
			if (CurrentSelectedTrail3Mission >= ConfigSettings.Settings_Progress_Trail3)
			{
				CurrentSelectedTrail3Mission = ConfigSettings.Settings_Progress_Trail3;
			}
			GenerateSwords();
			refTrailChicken = FRONT_Trail3.refChicken;
			ButtonTrailCampaignClicked(CurrentSelectedTrail3Mission);
			break;
		case "Customize":
		{
			Action act = delegate
			{
				ClearUIPanels(frontEndState: true, logo: false);
				MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
				int num15 = 0;
				switch (CurrentSelectedTrail)
				{
				case 0:
					num15 = CurrentSelectedTrailMission;
					break;
				case 1:
					num15 = CurrentSelectedTrail2Mission;
					break;
				case 2:
					num15 = CurrentSelectedTrail3Mission;
					break;
				case 11:
					num15 = CurrentSelectedTrailSands1Mission;
					break;
				case 12:
					num15 = CurrentSelectedTrailSands2Mission;
					break;
				case 13:
					num15 = CurrentSelectedTrailSands3Mission;
					break;
				case 14:
					num15 = CurrentSelectedTrailSands4Mission;
					break;
				case 15:
					num15 = CurrentSelectedTrailSands5Mission;
					break;
				case 16:
					num15 = CurrentSelectedTrailSands6Mission;
					break;
				case 17:
					num15 = CurrentSelectedTrailSands7Mission;
					break;
				case 18:
					num15 = CurrentSelectedTrailSands8Mission;
					break;
				}
				FRONT_Multiplayer.Open(skirmishSetup: true, null, coopSetup: false, trailMaker: false, CurrentSelectedTrail, num15 - 1);
			};
			if (!ConfigSettings.Settings_ShowCustomisationWarning)
			{
				act();
				break;
			}
			HUD_ConfirmationPopup.ShowConfirmationMessageCheck(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 70), delegate
			{
				act();
				if (!ConfigSettings.Settings_ShowCustomisationWarning)
				{
					ConfigSettings.SaveSettings();
				}
			}, delegate
			{
			}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 71), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 311), initialCheckState: false, delegate(bool state)
			{
				ConfigSettings.Settings_ShowCustomisationWarning = !state;
			}, MPConf: false, CurrentSelectedTrail);
			break;
		}
		case "TrailsDifficulty":
			currentDifficultySetting++;
			if (currentDifficultySetting > 4)
			{
				currentDifficultySetting = 0;
			}
			switch (CurrentSelectedTrail)
			{
			case 0:
				ConfigSettings.Settings_Trail1Difficulty = currentDifficultySetting;
				break;
			case 1:
				ConfigSettings.Settings_Trail2Difficulty = currentDifficultySetting;
				break;
			case 2:
				ConfigSettings.Settings_Trail3Difficulty = currentDifficultySetting;
				break;
			case 11:
				ConfigSettings.Settings_SandsTrail1Difficulty = currentDifficultySetting;
				break;
			case 12:
				ConfigSettings.Settings_SandsTrail2Difficulty = currentDifficultySetting;
				break;
			case 13:
				ConfigSettings.Settings_SandsTrail3Difficulty = currentDifficultySetting;
				break;
			case 14:
				ConfigSettings.Settings_SandsTrail4Difficulty = currentDifficultySetting;
				break;
			case 15:
				ConfigSettings.Settings_SandsTrail5Difficulty = currentDifficultySetting;
				break;
			case 16:
				ConfigSettings.Settings_SandsTrail6Difficulty = currentDifficultySetting;
				break;
			case 17:
				ConfigSettings.Settings_SandsTrail7Difficulty = currentDifficultySetting;
				break;
			case 18:
				ConfigSettings.Settings_SandsTrail8Difficulty = currentDifficultySetting;
				break;
			}
			ConfigSettings.SaveSettings();
			UpdateDiffcultyButton();
			break;
		case "TrailsAdvancedSettings":
			MainViewModel.Instance.Show_ClassicTrailsOptions = true;
			break;
		case "TrailsAdvanced_Close":
			MainViewModel.Instance.Show_ClassicTrailsOptions = false;
			break;
		case "TrailsAdvanced_Bedouin":
			ConfigSettings.Settings_Allow_Classic_Bedouin_Stockade = !ConfigSettings.Settings_Allow_Classic_Bedouin_Stockade;
			ConfigSettings.SaveSettings();
			updateAdvancedTrailsIcons();
			break;
		case "SandsFromParent":
			sandsSpeechVariant++;
			if ((sandsSpeechVariant & 1) != 0)
			{
				SFXManager.instance.playGenieSpeech(3, "GenieDE001.wav", 1f);
			}
			else
			{
				SFXManager.instance.playGenieSpeech(3, "GenieDE002.wav", 1f);
			}
			ButtonClicked("Sands");
			break;
		case "Sands":
		{
			if (ConfigSettings.Settings_DLC4_Pip2)
			{
				MainViewModel.Instance.DLC4PIP2 = (ConfigSettings.Settings_DLC4_Pip2 = false);
				ConfigSettings.SaveSettings();
			}
			ClearUIPanels(frontEndState: true, logo: false);
			UpdateDiffcultyButton();
			MainViewModel.Instance.Show_FrontMenus = true;
			MainViewModel.Instance.Show_Frontend_Skirmish_Sands = true;
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_FrontMenusTrailsSandsBackground = true;
			int num4 = ConfigSettings.countTrailMissionsCompleted(11);
			int num5 = ConfigSettings.countTrailMissionsCompleted(12);
			int num6 = ConfigSettings.countTrailMissionsCompleted(13);
			int num7 = ConfigSettings.countTrailMissionsCompleted(14);
			int num8 = ConfigSettings.countTrailMissionsCompleted(15);
			int num9 = ConfigSettings.countTrailMissionsCompleted(16);
			int num10 = ConfigSettings.countTrailMissionsCompleted(17);
			int num11 = ConfigSettings.countTrailMissionsCompleted(18);
			if (num4 < 5)
			{
				if (!FatControler.arabic || !ConfigSettings.Settings_ArabicL2R)
				{
					MainViewModel.Instance.Sands1_CompletedText = num4 + " / 5 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
				}
				else
				{
					MainViewModel.Instance.Sands1_CompletedText = "5 / " + num4 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
				}
				MainViewModel.Instance.Sands_Trail1_Complete = false;
			}
			else
			{
				MainViewModel.Instance.Sands1_CompletedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 60);
				MainViewModel.Instance.Sands_Trail1_Complete = true;
			}
			if (num5 < 7)
			{
				if (!FatControler.arabic || !ConfigSettings.Settings_ArabicL2R)
				{
					MainViewModel.Instance.Sands2_CompletedText = num5 + " / 7 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
				}
				else
				{
					MainViewModel.Instance.Sands2_CompletedText = "7 / " + num5 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
				}
				MainViewModel.Instance.Sands_Trail2_Complete = false;
			}
			else
			{
				MainViewModel.Instance.Sands2_CompletedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 60);
				MainViewModel.Instance.Sands_Trail2_Complete = true;
			}
			if (num6 < 9)
			{
				if (!FatControler.arabic || !ConfigSettings.Settings_ArabicL2R)
				{
					MainViewModel.Instance.Sands3_CompletedText = num6 + " / 9 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
				}
				else
				{
					MainViewModel.Instance.Sands3_CompletedText = "9 / " + num6 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
				}
				MainViewModel.Instance.Sands_Trail3_Complete = false;
			}
			else
			{
				MainViewModel.Instance.Sands3_CompletedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 60);
				MainViewModel.Instance.Sands_Trail3_Complete = true;
			}
			if (num7 < 11)
			{
				if (!FatControler.arabic || !ConfigSettings.Settings_ArabicL2R)
				{
					MainViewModel.Instance.Sands4_CompletedText = num7 + " / 11 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
				}
				else
				{
					MainViewModel.Instance.Sands4_CompletedText = "11 / " + num7 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
				}
				MainViewModel.Instance.Sands_Trail4_Complete = false;
			}
			else
			{
				MainViewModel.Instance.Sands4_CompletedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 60);
				MainViewModel.Instance.Sands_Trail4_Complete = true;
			}
			MainViewModel.Instance.Sands_Trail1_Not_Complete = !MainViewModel.Instance.Sands_Trail1_Complete;
			MainViewModel.Instance.Sands_Trail2_Not_Complete = !MainViewModel.Instance.Sands_Trail2_Complete;
			MainViewModel.Instance.Sands_Trail3_Not_Complete = !MainViewModel.Instance.Sands_Trail3_Complete;
			MainViewModel.Instance.Sands_Trail4_Not_Complete = !MainViewModel.Instance.Sands_Trail4_Complete;
			if (DLC1Owned)
			{
				if (num8 < 9)
				{
					if (!FatControler.arabic || !ConfigSettings.Settings_ArabicL2R)
					{
						MainViewModel.Instance.Sands5_CompletedText = num8 + " / 9 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
					}
					else
					{
						MainViewModel.Instance.Sands5_CompletedText = "9 / " + num8 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
					}
					MainViewModel.Instance.Sands_Trail5_Complete = false;
				}
				else
				{
					MainViewModel.Instance.Sands5_CompletedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 60);
					MainViewModel.Instance.Sands_Trail5_Complete = true;
				}
				MainViewModel.Instance.Sands_Trail5_Not_Complete = !MainViewModel.Instance.Sands_Trail5_Complete;
				MainViewModel.Instance.Sands_Trail5_Not_Available = false;
			}
			else
			{
				MainViewModel instance = MainViewModel.Instance;
				bool sands_Trail5_Not_Complete = (MainViewModel.Instance.Sands_Trail5_Complete = false);
				instance.Sands_Trail5_Not_Complete = sands_Trail5_Not_Complete;
				MainViewModel.Instance.Sands_Trail5_Not_Available = true;
				MainViewModel.Instance.Sands5_CompletedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 190);
			}
			if (DLC2Owned)
			{
				if (num9 < 9)
				{
					if (!FatControler.arabic || !ConfigSettings.Settings_ArabicL2R)
					{
						MainViewModel.Instance.Sands6_CompletedText = num9 + " / 9 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
					}
					else
					{
						MainViewModel.Instance.Sands6_CompletedText = "9 / " + num9 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
					}
					MainViewModel.Instance.Sands_Trail6_Complete = false;
				}
				else
				{
					MainViewModel.Instance.Sands6_CompletedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 60);
					MainViewModel.Instance.Sands_Trail6_Complete = true;
				}
				MainViewModel.Instance.Sands_Trail6_Not_Complete = !MainViewModel.Instance.Sands_Trail6_Complete;
				MainViewModel.Instance.Sands_Trail6_Not_Available = false;
			}
			else
			{
				MainViewModel instance2 = MainViewModel.Instance;
				bool sands_Trail5_Not_Complete = (MainViewModel.Instance.Sands_Trail6_Complete = false);
				instance2.Sands_Trail6_Not_Complete = sands_Trail5_Not_Complete;
				MainViewModel.Instance.Sands_Trail6_Not_Available = true;
				MainViewModel.Instance.Sands6_CompletedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 190);
			}
			if (DLC3Owned)
			{
				if (num10 < 9)
				{
					if (!FatControler.arabic || !ConfigSettings.Settings_ArabicL2R)
					{
						MainViewModel.Instance.Sands7_CompletedText = num10 + " / 9 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
					}
					else
					{
						MainViewModel.Instance.Sands7_CompletedText = "9 / " + num10 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
					}
					MainViewModel.Instance.Sands_Trail7_Complete = false;
				}
				else
				{
					MainViewModel.Instance.Sands7_CompletedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 60);
					MainViewModel.Instance.Sands_Trail7_Complete = true;
				}
				MainViewModel.Instance.Sands_Trail7_Not_Complete = !MainViewModel.Instance.Sands_Trail7_Complete;
				MainViewModel.Instance.Sands_Trail7_Not_Available = false;
			}
			else
			{
				MainViewModel instance3 = MainViewModel.Instance;
				bool sands_Trail5_Not_Complete = (MainViewModel.Instance.Sands_Trail7_Complete = false);
				instance3.Sands_Trail7_Not_Complete = sands_Trail5_Not_Complete;
				MainViewModel.Instance.Sands_Trail7_Not_Available = true;
				MainViewModel.Instance.Sands7_CompletedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 190);
			}
			if (DLC4Owned)
			{
				if (num11 < 9)
				{
					if (!FatControler.arabic || !ConfigSettings.Settings_ArabicL2R)
					{
						MainViewModel.Instance.Sands8_CompletedText = num11 + " / 9 " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
					}
					else
					{
						MainViewModel.Instance.Sands8_CompletedText = "9 / " + num11 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 189);
					}
					MainViewModel.Instance.Sands_Trail8_Complete = false;
				}
				else
				{
					MainViewModel.Instance.Sands8_CompletedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 60);
					MainViewModel.Instance.Sands_Trail8_Complete = true;
				}
				MainViewModel.Instance.Sands_Trail8_Not_Complete = !MainViewModel.Instance.Sands_Trail8_Complete;
				MainViewModel.Instance.Sands_Trail8_Not_Available = false;
			}
			else
			{
				MainViewModel instance4 = MainViewModel.Instance;
				bool sands_Trail5_Not_Complete = (MainViewModel.Instance.Sands_Trail8_Complete = false);
				instance4.Sands_Trail8_Not_Complete = sands_Trail5_Not_Complete;
				MainViewModel.Instance.Sands_Trail8_Not_Available = true;
				MainViewModel.Instance.Sands8_CompletedText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 190);
			}
			MainViewModel.Instance.SetSandsTrailRankImage(0, null);
			MainViewModel.Instance.SetSandsTrailRankImage(1, null);
			MainViewModel.Instance.SetSandsTrailRankImage(2, null);
			MainViewModel.Instance.SetSandsTrailRankImage(3, null);
			MainViewModel.Instance.SetSandsTrailRankImage(4, null);
			MainViewModel.Instance.SetSandsTrailRankImage(5, null);
			for (int num12 = 11; num12 < 17; num12++)
			{
				int num13 = ConfigSettings.Settings_Progress_Trail_Sands1;
				int num14 = 5;
				switch (num12)
				{
				case 11:
					num13 = ConfigSettings.Settings_Progress_Trail_Sands1;
					num14 = 5;
					break;
				case 12:
					num13 = ConfigSettings.Settings_Progress_Trail_Sands2;
					num14 = 7;
					break;
				case 13:
					num13 = ConfigSettings.Settings_Progress_Trail_Sands3;
					num14 = 9;
					break;
				case 14:
					num13 = ConfigSettings.Settings_Progress_Trail_Sands4;
					num14 = 11;
					break;
				case 15:
					num13 = ConfigSettings.Settings_Progress_Trail_Sands5;
					num14 = 9;
					break;
				case 16:
					num13 = ConfigSettings.Settings_Progress_Trail_Sands6;
					num14 = 9;
					break;
				case 17:
					num13 = ConfigSettings.Settings_Progress_Trail_Sands7;
					num14 = 9;
					break;
				case 18:
					num13 = ConfigSettings.Settings_Progress_Trail_Sands8;
					num14 = 9;
					break;
				}
				if (num13 < num14)
				{
					continue;
				}
				if (!GameData.Instance.SandsUsedChicken(num12))
				{
					int trailStartDate = ConfigSettings.getTrailStartDate(num12, -1);
					int seconds = 0;
					MainViewModel.Instance.TrailDate = GameData.GetTimeString(trailStartDate / 40);
					MainViewModel.Instance.TrailTarget = GameData.Instance.GetSandsOfTimeTargetTime(num12, -num13, ref seconds);
					if (!ConfigSettings.Settings_HideSoTTiming)
					{
						MainViewModel.Instance.SetSandsTrailRankImage(num12 - 11, GameData.Instance.GetSandsOfTimeRankImage(trailStartDate, seconds));
					}
				}
				else if (!ConfigSettings.Settings_HideSoTTiming)
				{
					MainViewModel.Instance.SetSandsTrailRankImage(num12 - 11, MainViewModel.Instance.GameSprites[642]);
				}
			}
			break;
		}
		case "Sands1FromParent":
			SFXManager.instance.playGenieSpeech(3, "GenieDE005.wav", 1f);
			ButtonClicked("Sands1");
			break;
		case "Sands1":
			ClearUIPanels(frontEndState: true, logo: false);
			currentDifficultySetting = ConfigSettings.Settings_SandsTrail1Difficulty;
			UpdateDiffcultyButton();
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_SandsTrail1Menu = true;
			CurrentSelectedTrail = 11;
			if (CurrentSelectedTrailSands1Mission >= ConfigSettings.Settings_Progress_Trail_Sands1)
			{
				CurrentSelectedTrailSands1Mission = ConfigSettings.Settings_Progress_Trail_Sands1;
			}
			GenerateSwords();
			refTrailChicken = FRONT_SandsTrail1.refChicken;
			ButtonTrailCampaignClicked(CurrentSelectedTrailSands1Mission);
			break;
		case "Sands2FromParent":
			SFXManager.instance.playGenieSpeech(3, "GenieDE006.wav", 1f);
			ButtonClicked("Sands2");
			break;
		case "Sands2":
			ClearUIPanels(frontEndState: true, logo: false);
			currentDifficultySetting = ConfigSettings.Settings_SandsTrail2Difficulty;
			UpdateDiffcultyButton();
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_SandsTrail2Menu = true;
			CurrentSelectedTrail = 12;
			if (CurrentSelectedTrailSands2Mission >= ConfigSettings.Settings_Progress_Trail_Sands2)
			{
				CurrentSelectedTrailSands2Mission = ConfigSettings.Settings_Progress_Trail_Sands2;
			}
			GenerateSwords();
			refTrailChicken = FRONT_SandsTrail2.refChicken;
			ButtonTrailCampaignClicked(CurrentSelectedTrailSands2Mission);
			break;
		case "Sands3FromParent":
			SFXManager.instance.playGenieSpeech(3, "GenieDE007.wav", 1f);
			ButtonClicked("Sands3");
			break;
		case "Sands3":
			ClearUIPanels(frontEndState: true, logo: false);
			currentDifficultySetting = ConfigSettings.Settings_SandsTrail3Difficulty;
			UpdateDiffcultyButton();
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_SandsTrail3Menu = true;
			CurrentSelectedTrail = 13;
			if (CurrentSelectedTrailSands3Mission >= ConfigSettings.Settings_Progress_Trail_Sands3)
			{
				CurrentSelectedTrailSands3Mission = ConfigSettings.Settings_Progress_Trail_Sands3;
			}
			GenerateSwords();
			refTrailChicken = FRONT_SandsTrail3.refChicken;
			ButtonTrailCampaignClicked(CurrentSelectedTrailSands3Mission);
			break;
		case "Sands4FromParent":
			SFXManager.instance.playGenieSpeech(3, "GenieDE008.wav", 1f);
			ButtonClicked("Sands4");
			break;
		case "Sands4":
			ClearUIPanels(frontEndState: true, logo: false);
			currentDifficultySetting = ConfigSettings.Settings_SandsTrail4Difficulty;
			UpdateDiffcultyButton();
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_SandsTrail4Menu = true;
			CurrentSelectedTrail = 14;
			if (CurrentSelectedTrailSands4Mission >= ConfigSettings.Settings_Progress_Trail_Sands4)
			{
				CurrentSelectedTrailSands4Mission = ConfigSettings.Settings_Progress_Trail_Sands4;
			}
			GenerateSwords();
			refTrailChicken = FRONT_SandsTrail4.refChicken;
			ButtonTrailCampaignClicked(CurrentSelectedTrailSands4Mission);
			break;
		case "Sands5FromParent":
			SFXManager.instance.playGenieSpeech(3, "GenieDE012.wav", 1f);
			ButtonClicked("Sands5");
			break;
		case "Sands5":
			if (!DLC1Owned)
			{
				SteamFriends.ActivateGameOverlayToStore(new AppId_t(3030340u), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
				break;
			}
			ClearUIPanels(frontEndState: true, logo: false);
			currentDifficultySetting = ConfigSettings.Settings_SandsTrail5Difficulty;
			UpdateDiffcultyButton();
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_SandsTrail5Menu = true;
			CurrentSelectedTrail = 15;
			if (CurrentSelectedTrailSands5Mission >= ConfigSettings.Settings_Progress_Trail_Sands5)
			{
				CurrentSelectedTrailSands5Mission = ConfigSettings.Settings_Progress_Trail_Sands5;
			}
			GenerateSwords();
			refTrailChicken = FRONT_SandsTrail5.refChicken;
			ButtonTrailCampaignClicked(CurrentSelectedTrailSands5Mission);
			break;
		case "Sands6FromParent":
			SFXManager.instance.playGenieSpeech(3, "GenieDE011.wav", 1f);
			ButtonClicked("Sands6");
			break;
		case "Sands6":
			if (!DLC2Owned)
			{
				SteamFriends.ActivateGameOverlayToStore(new AppId_t(3030350u), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
				break;
			}
			ClearUIPanels(frontEndState: true, logo: false);
			currentDifficultySetting = ConfigSettings.Settings_SandsTrail6Difficulty;
			UpdateDiffcultyButton();
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_SandsTrail6Menu = true;
			CurrentSelectedTrail = 16;
			if (CurrentSelectedTrailSands6Mission >= ConfigSettings.Settings_Progress_Trail_Sands6)
			{
				CurrentSelectedTrailSands6Mission = ConfigSettings.Settings_Progress_Trail_Sands6;
			}
			GenerateSwords();
			refTrailChicken = FRONT_SandsTrail6.refChicken;
			ButtonTrailCampaignClicked(CurrentSelectedTrailSands6Mission);
			break;
		case "Sands7FromParent":
			SFXManager.instance.playGenieSpeech(3, "GenieDE014.wav", 1f);
			ButtonClicked("Sands7");
			break;
		case "Sands7":
			if (!DLC3Owned)
			{
				SteamFriends.ActivateGameOverlayToStore(new AppId_t(4483540u), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
				break;
			}
			ClearUIPanels(frontEndState: true, logo: false);
			currentDifficultySetting = ConfigSettings.Settings_SandsTrail7Difficulty;
			UpdateDiffcultyButton();
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_SandsTrail7Menu = true;
			CurrentSelectedTrail = 17;
			if (CurrentSelectedTrailSands7Mission >= ConfigSettings.Settings_Progress_Trail_Sands7)
			{
				CurrentSelectedTrailSands7Mission = ConfigSettings.Settings_Progress_Trail_Sands7;
			}
			GenerateSwords();
			refTrailChicken = FRONT_SandsTrail7.refChicken;
			ButtonTrailCampaignClicked(CurrentSelectedTrailSands7Mission);
			break;
		case "Sands8FromParent":
			SFXManager.instance.playGenieSpeech(3, "GenieDE013.wav", 1f);
			ButtonClicked("Sands8");
			break;
		case "Sands8":
			if (ConfigSettings.Settings_DLC4_Pip3)
			{
				MainViewModel.Instance.DLC4PIP3 = (ConfigSettings.Settings_DLC4_Pip3 = false);
				ConfigSettings.SaveSettings();
			}
			if (!DLC4Owned)
			{
				SteamFriends.ActivateGameOverlayToStore(new AppId_t(4483530u), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
				break;
			}
			ClearUIPanels(frontEndState: true, logo: false);
			currentDifficultySetting = ConfigSettings.Settings_SandsTrail8Difficulty;
			UpdateDiffcultyButton();
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_SandsTrail8Menu = true;
			CurrentSelectedTrail = 18;
			if (CurrentSelectedTrailSands8Mission >= ConfigSettings.Settings_Progress_Trail_Sands8)
			{
				CurrentSelectedTrailSands8Mission = ConfigSettings.Settings_Progress_Trail_Sands8;
			}
			GenerateSwords();
			refTrailChicken = FRONT_SandsTrail8.refChicken;
			ButtonTrailCampaignClicked(CurrentSelectedTrailSands8Mission);
			break;
		case "SandsInfo":
			MainViewModel.Instance.Show_Sands_Intro_Text = true;
			refSandsTimeDisable.IsChecked = ConfigSettings.Settings_HideSoTTiming;
			break;
		case "Coops":
			if (ConfigSettings.Settings_DLC4_Pip4)
			{
				MainViewModel.Instance.DLC4PIP4 = (ConfigSettings.Settings_DLC4_Pip4 = false);
				ConfigSettings.SaveSettings();
			}
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
			MainViewModel.Instance.Show_FrontMenusTrailsBackground = true;
			MainViewModel.Instance.Show_Frontend_Coop = true;
			break;
		case "Coop":
			if (SteamUser.BLoggedOn())
			{
				ClearUIPanels(frontEndState: true, logo: false);
				CurrentSelectedTrail = 21;
				MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
				FRONT_Multiplayer.Open(skirmishSetup: false, null, coopSetup: true);
				refTrailChicken = null;
			}
			else
			{
				HUD_ConfirmationPopup.ShowConfirmationOKMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 367), delegate
				{
				}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 365));
			}
			break;
		case "Coop2":
			if (SteamUser.BLoggedOn())
			{
				ClearUIPanels(frontEndState: true, logo: false);
				CurrentSelectedTrail = 22;
				MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
				FRONT_Multiplayer.Open(skirmishSetup: false, null, coopSetup: true);
				refTrailChicken = null;
			}
			else
			{
				HUD_ConfirmationPopup.ShowConfirmationOKMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 367), delegate
				{
				}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 365));
			}
			break;
		case "Coop3":
			if (SteamUser.BLoggedOn())
			{
				ClearUIPanels(frontEndState: true, logo: false);
				CurrentSelectedTrail = 23;
				MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
				FRONT_Multiplayer.Open(skirmishSetup: false, null, coopSetup: true);
				refTrailChicken = null;
			}
			else
			{
				HUD_ConfirmationPopup.ShowConfirmationOKMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 367), delegate
				{
				}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 365));
			}
			break;
		case "Coop4":
			if (ConfigSettings.Settings_DLC4_Pip5)
			{
				MainViewModel.Instance.DLC4PIP5 = (ConfigSettings.Settings_DLC4_Pip5 = false);
				ConfigSettings.SaveSettings();
			}
			if (SteamUser.BLoggedOn())
			{
				ClearUIPanels(frontEndState: true, logo: false);
				CurrentSelectedTrail = 24;
				MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
				FRONT_Multiplayer.Open(skirmishSetup: false, null, coopSetup: true);
				refTrailChicken = null;
			}
			else
			{
				HUD_ConfirmationPopup.ShowConfirmationOKMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 367), delegate
				{
				}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 365));
			}
			break;
		case "Credits":
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_Credits = true;
			break;
		case "SkirmishMasters":
			FRONT_SkirmishMasters.Open();
			break;
		case "Roadmap":
			if (ConfigSettings.Settings_DLC4_Pip6)
			{
				MainViewModel.Instance.DLC4PIP6 = (ConfigSettings.Settings_DLC4_Pip6 = false);
				ConfigSettings.SaveSettings();
			}
			ClearUIPanels(frontEndState: true, logo: false);
			MainViewModel.Instance.Show_Frontend_Roadmap = true;
			break;
		case "BuyDLC1":
			SteamFriends.ActivateGameOverlayToStore(new AppId_t(4483540u), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
			break;
		case "BuyDLC2":
			SteamFriends.ActivateGameOverlayToStore(new AppId_t(4483530u), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
			break;
		case "ExitControl":
			UpdateFrontMenuPopupScale();
			HUD_ConfirmationPopup.ShowConfirmation(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 9), delegate
			{
				FatControler.instance.ExitApp();
			}, delegate
			{
			});
			break;
		case "ControlsModern":
			if (!ControlSchemeModernSelected)
			{
				ControlSchemeModernSelected = true;
				refClassicControlsShrink.Begin();
				refControlSchemeModernBorder.Visibility = Visibility.Visible;
				refControlSchemeClassicBorder.Visibility = Visibility.Hidden;
			}
			break;
		case "ControlsClassic":
			if (ControlSchemeModernSelected)
			{
				ControlSchemeModernSelected = false;
				refModernControlsShrink.Begin();
				refControlSchemeModernBorder.Visibility = Visibility.Hidden;
				refControlSchemeClassicBorder.Visibility = Visibility.Visible;
			}
			break;
		}
	}

	public void OpenCustomTrail(string trailname, int level = -1)
	{
		CustomTrailName = trailname;
		CustomTrailLength = MapFileManager.Instance.GetCustomTrailMissionsCount(trailname);
		if (CustomTrailLength > 0)
		{
			CustomTrailProgress = (CurrentSelectedCustomTrailMission = ConfigSettings.GetCustomTrailProgress(trailname) + 1);
			if (level >= 0)
			{
				CurrentSelectedCustomTrailMission = level;
			}
			if (CurrentSelectedCustomTrailMission >= CustomTrailLength)
			{
				CurrentSelectedCustomTrailMission = CustomTrailLength;
			}
			if (CustomTrailLength > 30)
			{
				ClearUIPanels(frontEndState: true, logo: false);
				MainViewModel.Instance.Show_TrailCustomisationButtons = false;
				currentDifficultySetting = 1;
				UpdateDiffcultyButton();
				MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
				MainViewModel.Instance.Show_TrailCampaignMenu = true;
				CurrentSelectedTrail = 90;
				CustomTrailFillWithCrosses();
				GenerateSwords();
				refTrailChicken = FRONT_Trail.refChicken;
				ButtonTrailCampaignClicked(CurrentSelectedCustomTrailMission);
			}
			else if (CustomTrailLength > 20)
			{
				ClearUIPanels(frontEndState: true, logo: false);
				MainViewModel.Instance.Show_TrailCustomisationButtons = false;
				currentDifficultySetting = 1;
				UpdateDiffcultyButton();
				MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
				MainViewModel.Instance.Show_Trail2CampaignMenu = true;
				CurrentSelectedTrail = 91;
				CustomTrailFillWithCrosses();
				GenerateSwords();
				refTrailChicken = FRONT_Trail2.refChicken;
				ButtonTrailCampaignClicked(CurrentSelectedCustomTrailMission);
			}
			else
			{
				ClearUIPanels(frontEndState: true, logo: false);
				MainViewModel.Instance.Show_TrailCustomisationButtons = false;
				currentDifficultySetting = 1;
				UpdateDiffcultyButton();
				MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
				MainViewModel.Instance.Show_Trail3CampaignMenu = true;
				CurrentSelectedTrail = 92;
				CustomTrailFillWithCrosses();
				GenerateSwords();
				refTrailChicken = FRONT_Trail3.refChicken;
				ButtonTrailCampaignClicked(CurrentSelectedCustomTrailMission);
			}
		}
	}

	public void Update()
	{
		if (loadingStill && spriteLoader.instance.spritesLoaded)
		{
			refFrontendBackVideo.Play();
			isVideoPlaying = true;
			MainViewModel.Instance.Show_Frontend_MainMenu = true;
			MainViewModel.Instance.FrontEndMenu.refMainMenu_ShowMainMenu.Begin();
			loadingStill = false;
		}
		if (MainViewModel.Instance.Show_TrailCampaignMenu || MainViewModel.Instance.Show_Trail2CampaignMenu || MainViewModel.Instance.Show_Trail3CampaignMenu || MainViewModel.Instance.Show_SandsTrail1Menu || MainViewModel.Instance.Show_SandsTrail2Menu || MainViewModel.Instance.Show_SandsTrail3Menu || MainViewModel.Instance.Show_SandsTrail4Menu || MainViewModel.Instance.Show_SandsTrail5Menu || MainViewModel.Instance.Show_SandsTrail6Menu || MainViewModel.Instance.Show_SandsTrail7Menu || MainViewModel.Instance.Show_CoopTrail3 || MainViewModel.Instance.Show_SandsTrail8Menu || MainViewModel.Instance.Show_CoopTrail4 || MainViewModel.Instance.Show_CoopTrail1 || MainViewModel.Instance.Show_CoopTrail2)
		{
			AnimateKnightAndChickenButton();
			if (richardAfraidTime != DateTime.MinValue && richardAfraidTime < DateTime.UtcNow)
			{
				richardAfraidTime = DateTime.MinValue;
				SFXManager.instance.playSpeech(1, "ri_kick_player_01.wav", 1f);
			}
		}
		if (MainViewModel.Instance.Show_SH4AdIntro && DateTime.UtcNow > sh4StartTime)
		{
			ButtonClicked("BackMain");
		}
	}

	private void MouseEnterMainButtonHandler(object sender, MouseEventArgs e)
	{
		if (!(e.Source is Button))
		{
			return;
		}
		switch ((string)((Button)e.Source).CommandParameter)
		{
		case "BackMain":
			break;
		case "EcoMission":
			break;
		case "JustBuild":
			break;
		case "Combat":
			MainViewModel.Instance.Show_FrontEndCombat_Sandbox_Help = true;
			PlayShieldRollover();
			break;
		case "Eco":
			PlayShieldRollover();
			break;
		case "Tutorial":
			UpdateTutorialHelpVisibility(state: true);
			break;
		case "Options":
		case "Load":
			PlayShieldRollover(0.5f);
			break;
		case "Exit":
			PlayExitRollover();
			break;
		case "Skirmish":
			MainViewModel.Instance.Show_FrontEndCombat_Skirmish_Help = true;
			PlayShieldRollover();
			break;
		case "Historical":
			MainViewModel.Instance.Show_FrontEndCombat_Historical_Help = true;
			PlayShieldRollover();
			break;
		case "Multiplayer":
			MainViewModel.Instance.Show_FrontEndCombat_MP_Help = true;
			PlayShieldRollover();
			break;
		case "MapEditor":
			MainViewModel.Instance.Show_FrontEndCombat_ME_Help = true;
			PlayShieldRollover();
			break;
		case "Roadmap":
			MainViewModel.Instance.Show_FrontEndCombat_Roadmap_Help = true;
			PlayShieldRollover();
			break;
		case "EcoCampaign":
		case "Sands1FromParent":
		case "Sands2FromParent":
		case "Sands3FromParent":
		case "Sands4FromParent":
		case "Sands5FromParent":
		case "Sands6FromParent":
		case "Sands7FromParent":
		case "Sands8FromParent":
		case "Trail":
		case "Trail1":
		case "Trail2":
		case "Trail3":
			PlayShieldRollover();
			break;
		case "Trails":
			MainViewModel.Instance.Show_FrontEndSkirmish_Trail_Help = true;
			PlayShieldRollover();
			break;
		case "SandsFromParent":
			MainViewModel.Instance.Show_FrontEndSkirmish_Sands_Help = true;
			PlayShieldRollover();
			break;
		case "Coops":
			MainViewModel.Instance.Show_FrontEndSkirmish_Coop_Help = true;
			PlayShieldRollover();
			break;
		case "Custom":
			MainViewModel.Instance.Show_FrontEndSkirmish_Custom_Help = true;
			PlayShieldRollover();
			break;
		case "CustomTrails":
			MainViewModel.Instance.Show_FrontEndSkirmish_CustomTrail_Help = true;
			PlayShieldRollover();
			break;
		case "Invasion":
			MainViewModel.Instance.Show_FrontEndSkirmish_Invasion_Help = true;
			PlayShieldRollover();
			break;
		case "Freebuild":
			MainViewModel.Instance.Show_FrontEndSkirmish_Freebuild_Help = true;
			PlayShieldRollover();
			break;
		case "Historical1":
			MainViewModel.Instance.Show_FrontEndHistorical_1_Help = true;
			PlayShieldRollover();
			break;
		case "Historical2":
			MainViewModel.Instance.Show_FrontEndHistorical_2_Help = true;
			PlayShieldRollover();
			break;
		case "Historical3":
			MainViewModel.Instance.Show_FrontEndHistorical_3_Help = true;
			PlayShieldRollover();
			break;
		case "Historical4":
			MainViewModel.Instance.Show_FrontEndHistorical_4_Help = true;
			PlayShieldRollover();
			break;
		case "Historical5":
			MainViewModel.Instance.Show_FrontEndHistorical_5_Help = true;
			PlayShieldRollover();
			break;
		case "Historical6":
			MainViewModel.Instance.Show_FrontEndHistorical_6_Help = true;
			PlayShieldRollover();
			break;
		case "Historical7":
			MainViewModel.Instance.Show_FrontEndHistorical_7_Help = true;
			PlayShieldRollover();
			break;
		case "ControlsModern":
			if (!ControlSchemeModernSelected)
			{
				refModernControlsEnlarge.Begin();
				refModernControlsShrink.Stop();
				refShowModernKeybinds.Begin();
				refShowClassicKeybinds.Stop();
			}
			break;
		case "ControlsClassic":
			if (ControlSchemeModernSelected)
			{
				refClassicControlsEnlarge.Begin();
				refClassicControlsShrink.Stop();
				refShowModernKeybinds.Stop();
				refShowClassicKeybinds.Begin();
			}
			break;
		}
	}

	private void MouseLeaveMainButtonHandler(object sender, MouseEventArgs e)
	{
		if (!(e.Source is Button))
		{
			return;
		}
		switch ((string)((Button)e.Source).CommandParameter)
		{
		case "EcoCampaign":
			break;
		case "ControlsModern":
			if (!ControlSchemeModernSelected)
			{
				refModernControlsEnlarge.Stop();
				refModernControlsShrink.Begin();
				refShowModernKeybinds.Stop();
				refShowClassicKeybinds.Begin();
			}
			break;
		case "ControlsClassic":
			if (ControlSchemeModernSelected)
			{
				refClassicControlsEnlarge.Stop();
				refClassicControlsShrink.Begin();
				refShowModernKeybinds.Begin();
				refShowClassicKeybinds.Stop();
			}
			break;
		case "Tutorial":
			UpdateTutorialHelpVisibility(state: false);
			break;
		case "Skirmish":
			MainViewModel.Instance.Show_FrontEndCombat_Skirmish_Help = false;
			break;
		case "Historical":
			MainViewModel.Instance.Show_FrontEndCombat_Historical_Help = false;
			break;
		case "Combat":
			MainViewModel.Instance.Show_FrontEndCombat_Sandbox_Help = false;
			break;
		case "Multiplayer":
			MainViewModel.Instance.Show_FrontEndCombat_MP_Help = false;
			break;
		case "MapEditor":
			MainViewModel.Instance.Show_FrontEndCombat_ME_Help = false;
			break;
		case "Roadmap":
			MainViewModel.Instance.Show_FrontEndCombat_Roadmap_Help = false;
			break;
		case "Trails":
			MainViewModel.Instance.Show_FrontEndSkirmish_Trail_Help = false;
			break;
		case "SandsFromParent":
			MainViewModel.Instance.Show_FrontEndSkirmish_Sands_Help = false;
			break;
		case "Coops":
			MainViewModel.Instance.Show_FrontEndSkirmish_Coop_Help = false;
			break;
		case "Custom":
			MainViewModel.Instance.Show_FrontEndSkirmish_Custom_Help = false;
			break;
		case "CustomTrails":
			MainViewModel.Instance.Show_FrontEndSkirmish_CustomTrail_Help = false;
			break;
		case "Invasion":
			MainViewModel.Instance.Show_FrontEndSkirmish_Invasion_Help = false;
			break;
		case "Freebuild":
			MainViewModel.Instance.Show_FrontEndSkirmish_Freebuild_Help = false;
			break;
		case "Historical1":
			MainViewModel.Instance.Show_FrontEndHistorical_1_Help = false;
			break;
		case "Historical2":
			MainViewModel.Instance.Show_FrontEndHistorical_2_Help = false;
			break;
		case "Historical3":
			MainViewModel.Instance.Show_FrontEndHistorical_3_Help = false;
			break;
		case "Historical4":
			MainViewModel.Instance.Show_FrontEndHistorical_4_Help = false;
			break;
		case "Historical5":
			MainViewModel.Instance.Show_FrontEndHistorical_5_Help = false;
			break;
		case "Historical6":
			MainViewModel.Instance.Show_FrontEndHistorical_6_Help = false;
			break;
		case "Historical7":
			MainViewModel.Instance.Show_FrontEndHistorical_7_Help = false;
			break;
		case "DLCECO":
			MainViewModel.Instance.Show_FrontEndEco_DLC_Help = false;
			break;
		case "DLC3NOT":
			MainViewModel.Instance.Show_FrontEndCombat_DLC_3_Help = false;
			break;
		}
	}

	private void SandsClickHandler(object sender, MouseEventArgs e)
	{
		MainViewModel.Instance.Show_Sands_Intro_Text = false;
		if (ConfigSettings.Settings_ShowSandsIntro)
		{
			ConfigSettings.Settings_ShowSandsIntro = false;
			ConfigSettings.SaveSettings();
		}
		if (ConfigSettings.Settings_HideSoTTiming != refSandsTimeDisable.IsChecked)
		{
			ConfigSettings.Settings_HideSoTTiming = refSandsTimeDisable.IsChecked.Value;
			MainViewModel.Instance.Show_LeaderboardOptIn = !ConfigSettings.Settings_HideSoTTiming;
			ConfigSettings.SaveSettings();
			ButtonClicked("Sands");
		}
	}

	public void PlayShieldRollover(float volume = 1f)
	{
		if (lastRolloverSoundTime < DateTime.UtcNow)
		{
			SFXManager.instance.playUISound(146, volume);
			lastRolloverSoundTime = DateTime.UtcNow.AddMilliseconds(200.0);
		}
	}

	public void PlayExitRollover(float volume = 1f)
	{
		if (lastRolloverSoundTime < DateTime.UtcNow)
		{
			SFXManager.instance.playUISound(251);
			lastRolloverSoundTime = DateTime.UtcNow.AddMilliseconds(200.0);
		}
	}

	private void updateAdvancedTrailsIcons()
	{
		if (ConfigSettings.Settings_Allow_Classic_Bedouin_Stockade)
		{
			MainViewModel.Instance.TrailsAdvanced_State = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.TrailsAdvanced_State = MainViewModel.Instance.GameSprites[641];
		}
	}

	public void ButtonHistoricalCampaignClicked(int value)
	{
		if (value < 0)
		{
			if (value == -1)
			{
				ButtonClicked("Historical");
			}
			else
			{
				FRONT_Story.OpenStory((CurrentSelectedHistoricalMission / 10 - 1) * 5 + CurrentSelectedHistoricalMission % 10);
			}
			return;
		}
		CurrentSelectedHistoricalMission = value;
		if (value >= 10 && value < 20)
		{
			CurrentSelectedHistorical1Mission = value;
		}
		if (value >= 20 && value < 30)
		{
			CurrentSelectedHistorical2Mission = value;
		}
		if (value >= 30 && value < 40)
		{
			CurrentSelectedHistorical3Mission = value;
		}
		if (value >= 40 && value < 50)
		{
			CurrentSelectedHistorical4Mission = value;
		}
		if (value >= 50 && value < 60)
		{
			CurrentSelectedHistorical5Mission = value;
		}
		if (value >= 60 && value < 70)
		{
			CurrentSelectedHistorical6Mission = value;
		}
		if (value >= 70 && value < 80)
		{
			CurrentSelectedHistorical7Mission = value;
		}
		UpdateHistoricalCampaignSelectedButton(value);
	}

	private void UpdateHistoricalCampaignSelectedButton(int buttonID)
	{
		buttonID %= 10;
		for (int i = 0; i < MainViewModel.Instance.HistoricalCampaignMenuButtonBorders.Count; i++)
		{
			MainViewModel.Instance.HistoricalCampaignMenuButtonBorders[i] = Visibility.Hidden;
		}
		if (buttonID >= MainViewModel.Instance.HistoricalCampaignMenuButtonBorders.Count)
		{
			buttonID = MainViewModel.Instance.HistoricalCampaignMenuButtonBorders.Count - 1;
		}
		MainViewModel.Instance.HistoricalCampaignMenuButtonBorders[buttonID] = Visibility.Visible;
	}

	public void ButtonTrailCampaignClicked(int value, bool fromRealClick = false)
	{
		if (value < 0)
		{
			switch (value)
			{
			case -1:
				if (CurrentSelectedTrail >= 90)
				{
					ButtonClicked("CustomTrails");
				}
				else if (CurrentSelectedTrail < 10)
				{
					ButtonClicked("Trails");
				}
				else
				{
					ButtonClicked("Sands");
				}
				return;
			case -11:
				ButtonClicked("SkirmishMasters");
				return;
			case -13:
				switch (CurrentSelectedTrail)
				{
				case 11:
					HUD_Leaderboard.OpenLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands1Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 7 + CurrentSelectedTrailSands1Mission - 1));
					break;
				case 12:
					HUD_Leaderboard.OpenLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands2Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 12 + CurrentSelectedTrailSands2Mission - 1));
					break;
				case 13:
					HUD_Leaderboard.OpenLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands3Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 19 + CurrentSelectedTrailSands3Mission - 1));
					break;
				case 14:
					HUD_Leaderboard.OpenLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands4Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 28 + CurrentSelectedTrailSands4Mission - 1));
					break;
				case 15:
					HUD_Leaderboard.OpenLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands5Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 39 + CurrentSelectedTrailSands5Mission - 1));
					break;
				case 16:
					HUD_Leaderboard.OpenLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands6Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 49 + CurrentSelectedTrailSands6Mission - 1));
					break;
				case 17:
					HUD_Leaderboard.OpenLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands7Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 82 + CurrentSelectedTrailSands7Mission - 1));
					break;
				case 18:
					HUD_Leaderboard.OpenLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands8Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 92 + CurrentSelectedTrailSands8Mission - 1));
					break;
				}
				return;
			case -12:
				switch (CurrentSelectedTrail)
				{
				case 0:
					if (ConfigSettings.Settings_Progress_Trail < 51)
					{
						ConfigSettings.Settings_Trail1Times[ConfigSettings.Settings_Progress_Trail] = -1200;
						ConfigSettings.Settings_Progress_Trail++;
						ConfigSettings.SaveSettings();
						CurrentSelectedTrailMission = ConfigSettings.Settings_Progress_Trail;
						SFXManager.instance.playUISound(258);
						richardAfraidTime = DateTime.UtcNow.AddSeconds(1.0);
						ButtonClicked("Trail");
					}
					break;
				case 1:
					if (ConfigSettings.Settings_Progress_Trail2 < 31)
					{
						ConfigSettings.Settings_Trail2Times[ConfigSettings.Settings_Progress_Trail2] = -1200;
						ConfigSettings.Settings_Progress_Trail2++;
						ConfigSettings.SaveSettings();
						CurrentSelectedTrail2Mission = ConfigSettings.Settings_Progress_Trail2;
						SFXManager.instance.playUISound(258);
						richardAfraidTime = DateTime.UtcNow.AddSeconds(1.0);
						ButtonClicked("Trail2");
					}
					break;
				case 2:
					if (ConfigSettings.Settings_Progress_Trail3 < 21)
					{
						ConfigSettings.Settings_Trail3Times[ConfigSettings.Settings_Progress_Trail3] = -1200;
						ConfigSettings.Settings_Progress_Trail3++;
						ConfigSettings.SaveSettings();
						CurrentSelectedTrail3Mission = ConfigSettings.Settings_Progress_Trail3;
						SFXManager.instance.playUISound(258);
						richardAfraidTime = DateTime.UtcNow.AddSeconds(1.0);
						ButtonClicked("Trail3");
					}
					break;
				case 11:
					if (ConfigSettings.Settings_Progress_Trail_Sands1 < 5)
					{
						ConfigSettings.Settings_Trail_Sands1_Times[ConfigSettings.Settings_Progress_Trail_Sands1] = -1200;
						ConfigSettings.Settings_Progress_Trail_Sands1++;
						ConfigSettings.SaveSettings();
						CurrentSelectedTrailSands1Mission = ConfigSettings.Settings_Progress_Trail_Sands1;
						SFXManager.instance.playUISound(258);
						richardAfraidTime = DateTime.UtcNow.AddSeconds(1.0);
						ButtonClicked("Sands1");
					}
					break;
				case 12:
					if (ConfigSettings.Settings_Progress_Trail_Sands2 < 7)
					{
						ConfigSettings.Settings_Trail_Sands2_Times[ConfigSettings.Settings_Progress_Trail_Sands2] = -1200;
						ConfigSettings.Settings_Progress_Trail_Sands2++;
						ConfigSettings.SaveSettings();
						CurrentSelectedTrailSands2Mission = ConfigSettings.Settings_Progress_Trail_Sands2;
						SFXManager.instance.playUISound(258);
						richardAfraidTime = DateTime.UtcNow.AddSeconds(1.0);
						ButtonClicked("Sands2");
					}
					break;
				case 13:
					if (ConfigSettings.Settings_Progress_Trail_Sands3 < 9)
					{
						ConfigSettings.Settings_Trail_Sands3_Times[ConfigSettings.Settings_Progress_Trail_Sands3] = -1200;
						ConfigSettings.Settings_Progress_Trail_Sands3++;
						ConfigSettings.SaveSettings();
						CurrentSelectedTrailSands3Mission = ConfigSettings.Settings_Progress_Trail_Sands3;
						SFXManager.instance.playUISound(258);
						richardAfraidTime = DateTime.UtcNow.AddSeconds(1.0);
						ButtonClicked("Sands3");
					}
					break;
				case 14:
					if (ConfigSettings.Settings_Progress_Trail_Sands4 < 11)
					{
						ConfigSettings.Settings_Trail_Sands4_Times[ConfigSettings.Settings_Progress_Trail_Sands4] = -1200;
						ConfigSettings.Settings_Progress_Trail_Sands4++;
						ConfigSettings.SaveSettings();
						CurrentSelectedTrailSands4Mission = ConfigSettings.Settings_Progress_Trail_Sands4;
						SFXManager.instance.playUISound(258);
						richardAfraidTime = DateTime.UtcNow.AddSeconds(1.0);
						ButtonClicked("Sands4");
					}
					break;
				case 15:
					if (ConfigSettings.Settings_Progress_Trail_Sands5 < 10)
					{
						ConfigSettings.Settings_Trail_Sands5_Times[ConfigSettings.Settings_Progress_Trail_Sands5] = -1200;
						ConfigSettings.Settings_Progress_Trail_Sands5++;
						ConfigSettings.SaveSettings();
						CurrentSelectedTrailSands5Mission = ConfigSettings.Settings_Progress_Trail_Sands5;
						SFXManager.instance.playUISound(258);
						richardAfraidTime = DateTime.UtcNow.AddSeconds(1.0);
						ButtonClicked("Sands5");
					}
					break;
				case 16:
					if (ConfigSettings.Settings_Progress_Trail_Sands6 < 10)
					{
						ConfigSettings.Settings_Trail_Sands6_Times[ConfigSettings.Settings_Progress_Trail_Sands6] = -1200;
						ConfigSettings.Settings_Progress_Trail_Sands6++;
						ConfigSettings.SaveSettings();
						CurrentSelectedTrailSands6Mission = ConfigSettings.Settings_Progress_Trail_Sands6;
						SFXManager.instance.playUISound(258);
						richardAfraidTime = DateTime.UtcNow.AddSeconds(1.0);
						ButtonClicked("Sands6");
					}
					break;
				case 17:
					if (ConfigSettings.Settings_Progress_Trail_Sands7 < 10)
					{
						ConfigSettings.Settings_Trail_Sands7_Times[ConfigSettings.Settings_Progress_Trail_Sands7] = -1200;
						ConfigSettings.Settings_Progress_Trail_Sands7++;
						ConfigSettings.SaveSettings();
						CurrentSelectedTrailSands7Mission = ConfigSettings.Settings_Progress_Trail_Sands7;
						SFXManager.instance.playUISound(258);
						richardAfraidTime = DateTime.UtcNow.AddSeconds(1.0);
						ButtonClicked("Sands7");
					}
					break;
				case 18:
					if (ConfigSettings.Settings_Progress_Trail_Sands8 < 10)
					{
						ConfigSettings.Settings_Trail_Sands8_Times[ConfigSettings.Settings_Progress_Trail_Sands8] = -1200;
						ConfigSettings.Settings_Progress_Trail_Sands8++;
						ConfigSettings.SaveSettings();
						CurrentSelectedTrailSands8Mission = ConfigSettings.Settings_Progress_Trail_Sands8;
						SFXManager.instance.playUISound(258);
						richardAfraidTime = DateTime.UtcNow.AddSeconds(1.0);
						ButtonClicked("Sands8");
					}
					break;
				case 90:
				case 91:
				case 92:
					ConfigSettings.CustomTrailCheat(CustomTrailName, CustomTrailLength);
					OpenCustomTrail(CustomTrailName);
					break;
				}
				return;
			case -20:
				return;
			}
			HUD_Leaderboard.CloseLeaderboard();
			switch (CurrentSelectedTrail)
			{
			case 0:
				StartTrailMission(CurrentSelectedTrailMission - 1, CurrentSelectedTrail);
				break;
			case 1:
				StartTrailMission(CurrentSelectedTrail2Mission - 1, CurrentSelectedTrail);
				break;
			case 2:
				StartTrailMission(CurrentSelectedTrail3Mission - 1, CurrentSelectedTrail);
				break;
			case 11:
				if (ConfigSettings.Settings_Progress_Trail_Sands1 == 1)
				{
					SFXManager.instance.playGenieSpeech(3, "GenieDE003.wav", 1f);
				}
				StartTrailMission(CurrentSelectedTrailSands1Mission - 1, CurrentSelectedTrail);
				break;
			case 12:
				if (ConfigSettings.Settings_Progress_Trail_Sands2 == 1)
				{
					SFXManager.instance.playGenieSpeech(3, "GenieDE004.wav", 1f);
				}
				StartTrailMission(CurrentSelectedTrailSands2Mission - 1, CurrentSelectedTrail);
				break;
			case 13:
				if (ConfigSettings.Settings_Progress_Trail_Sands3 == 1)
				{
					SFXManager.instance.playGenieSpeech(3, "GenieDE003.wav", 1f);
				}
				StartTrailMission(CurrentSelectedTrailSands3Mission - 1, CurrentSelectedTrail);
				break;
			case 14:
				if (ConfigSettings.Settings_Progress_Trail_Sands4 == 1)
				{
					SFXManager.instance.playGenieSpeech(3, "GenieDE004.wav", 1f);
				}
				StartTrailMission(CurrentSelectedTrailSands4Mission - 1, CurrentSelectedTrail);
				break;
			case 15:
				if (ConfigSettings.Settings_Progress_Trail_Sands5 == 1)
				{
					SFXManager.instance.playGenieSpeech(3, "GenieDE003.wav", 1f);
				}
				StartTrailMission(CurrentSelectedTrailSands5Mission - 1, CurrentSelectedTrail);
				break;
			case 16:
				if (ConfigSettings.Settings_Progress_Trail_Sands6 == 1)
				{
					SFXManager.instance.playGenieSpeech(3, "GenieDE004.wav", 1f);
				}
				StartTrailMission(CurrentSelectedTrailSands6Mission - 1, CurrentSelectedTrail);
				break;
			case 17:
				if (ConfigSettings.Settings_Progress_Trail_Sands7 == 1)
				{
					SFXManager.instance.playGenieSpeech(3, "GenieDE003.wav", 1f);
				}
				StartTrailMission(CurrentSelectedTrailSands7Mission - 1, CurrentSelectedTrail);
				break;
			case 18:
				if (ConfigSettings.Settings_Progress_Trail_Sands8 == 1)
				{
					SFXManager.instance.playGenieSpeech(3, "GenieDE004.wav", 1f);
				}
				StartTrailMission(CurrentSelectedTrailSands8Mission - 1, CurrentSelectedTrail);
				break;
			case 21:
				StartTrailMission(CurrentSelectedTrailCoop1Mission - 1, CurrentSelectedTrail);
				break;
			case 22:
				StartTrailMission(CurrentSelectedTrailCoop2Mission - 1, CurrentSelectedTrail);
				break;
			case 23:
				StartTrailMission(CurrentSelectedTrailCoop3Mission - 1, CurrentSelectedTrail);
				break;
			case 24:
				StartTrailMission(CurrentSelectedTrailCoop4Mission - 1, CurrentSelectedTrail);
				break;
			case 90:
			case 91:
			case 92:
				StartCustomTrailMission(CustomTrailName, CurrentSelectedCustomTrailMission);
				break;
			}
			return;
		}
		switch (CurrentSelectedTrail)
		{
		case 0:
			CurrentSelectedTrailMission = value;
			break;
		case 1:
			CurrentSelectedTrail2Mission = value;
			break;
		case 2:
			CurrentSelectedTrail3Mission = value;
			break;
		case 11:
			CurrentSelectedTrailSands1Mission = value;
			if (MainViewModel.Instance.Show_Leaderboard)
			{
				HUD_Leaderboard.ChangeLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands1Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 7 + CurrentSelectedTrailSands1Mission - 1));
			}
			break;
		case 12:
			CurrentSelectedTrailSands2Mission = value;
			if (MainViewModel.Instance.Show_Leaderboard)
			{
				HUD_Leaderboard.ChangeLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands2Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 12 + CurrentSelectedTrailSands2Mission - 1));
			}
			break;
		case 13:
			CurrentSelectedTrailSands3Mission = value;
			if (MainViewModel.Instance.Show_Leaderboard)
			{
				HUD_Leaderboard.ChangeLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands3Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 19 + CurrentSelectedTrailSands3Mission - 1));
			}
			break;
		case 14:
			CurrentSelectedTrailSands4Mission = value;
			if (MainViewModel.Instance.Show_Leaderboard)
			{
				HUD_Leaderboard.ChangeLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands4Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 28 + CurrentSelectedTrailSands4Mission - 1));
			}
			break;
		case 15:
			CurrentSelectedTrailSands5Mission = value;
			if (MainViewModel.Instance.Show_Leaderboard)
			{
				HUD_Leaderboard.ChangeLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands5Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 39 + CurrentSelectedTrailSands5Mission - 1));
			}
			break;
		case 16:
			CurrentSelectedTrailSands6Mission = value;
			if (MainViewModel.Instance.Show_Leaderboard)
			{
				HUD_Leaderboard.ChangeLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands6Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 49 + CurrentSelectedTrailSands6Mission - 1));
			}
			break;
		case 17:
			CurrentSelectedTrailSands7Mission = value;
			if (MainViewModel.Instance.Show_Leaderboard)
			{
				HUD_Leaderboard.ChangeLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands7Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 82 + CurrentSelectedTrailSands7Mission - 1));
			}
			break;
		case 18:
			CurrentSelectedTrailSands8Mission = value;
			if (MainViewModel.Instance.Show_Leaderboard)
			{
				HUD_Leaderboard.ChangeLeaderboard(Platform_Leaderboards.GetSandsLeaderboardName(CurrentSelectedTrail - 10, CurrentSelectedTrailSands8Mission - 1), Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 92 + CurrentSelectedTrailSands8Mission - 1));
			}
			break;
		case 21:
			if (fromRealClick && MainViewModel.Instance.Show_CoopClientPane)
			{
				return;
			}
			CurrentSelectedTrailCoop1Mission = value;
			if (fromRealClick)
			{
				MainViewModel.Instance.FRONTMultiplayer.CoopMissionChanged(0, value, resetOrderSwapped: true);
			}
			break;
		case 22:
			if (fromRealClick && MainViewModel.Instance.Show_CoopClientPane)
			{
				return;
			}
			CurrentSelectedTrailCoop2Mission = value;
			if (fromRealClick)
			{
				MainViewModel.Instance.FRONTMultiplayer.CoopMissionChanged(1, value, resetOrderSwapped: true);
			}
			break;
		case 23:
			if (fromRealClick && MainViewModel.Instance.Show_CoopClientPane)
			{
				return;
			}
			CurrentSelectedTrailCoop3Mission = value;
			if (fromRealClick)
			{
				MainViewModel.Instance.FRONTMultiplayer.CoopMissionChanged(2, value, resetOrderSwapped: true);
			}
			break;
		case 24:
			if (fromRealClick && MainViewModel.Instance.Show_CoopClientPane)
			{
				return;
			}
			CurrentSelectedTrailCoop4Mission = value;
			if (fromRealClick)
			{
				MainViewModel.Instance.FRONTMultiplayer.CoopMissionChanged(3, value, resetOrderSwapped: true);
			}
			break;
		case 90:
		case 91:
		case 92:
			CurrentSelectedCustomTrailMission = value;
			break;
		}
		UpdateTrailCampaignSelectedButton(value);
	}

	private void GetRawTrailCoord(int trail, int buttonID, out int x, out int y)
	{
		x = (y = -1000);
		switch (trail)
		{
		case 0:
		case 90:
			if (buttonID >= 1 && buttonID <= 50)
			{
				x = trailLocations[(buttonID - 1) * 2];
				y = trailLocations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 1:
		case 91:
			if (buttonID >= 1 && buttonID <= 30)
			{
				x = trail2Locations[(buttonID - 1) * 2];
				y = trail2Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 2:
		case 92:
			if (buttonID >= 1 && buttonID <= 20)
			{
				x = trail3Locations[(buttonID - 1) * 2];
				y = trail3Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 11:
			if (buttonID >= 1 && buttonID <= 5)
			{
				x = sands1Locations[(buttonID - 1) * 2];
				y = sands1Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 12:
			if (buttonID >= 1 && buttonID <= 7)
			{
				x = sands2Locations[(buttonID - 1) * 2];
				y = sands2Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 13:
			if (buttonID >= 1 && buttonID <= 9)
			{
				x = sands3Locations[(buttonID - 1) * 2];
				y = sands3Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 14:
			if (buttonID >= 1 && buttonID <= 11)
			{
				x = sands4Locations[(buttonID - 1) * 2];
				y = sands4Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 15:
			if (buttonID >= 1 && buttonID <= 9)
			{
				x = sands5Locations[(buttonID - 1) * 2];
				y = sands5Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 16:
			if (buttonID >= 1 && buttonID <= 9)
			{
				x = sands6Locations[(buttonID - 1) * 2];
				y = sands6Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 17:
			if (buttonID >= 1 && buttonID <= 9)
			{
				x = sands7Locations[(buttonID - 1) * 2];
				y = sands7Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 18:
			if (buttonID >= 1 && buttonID <= 9)
			{
				x = sands8Locations[(buttonID - 1) * 2];
				y = sands8Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 21:
			if (buttonID >= 1 && buttonID <= 10)
			{
				x = coop1Locations[(buttonID - 1) * 2];
				y = coop1Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 22:
			if (buttonID >= 1 && buttonID <= 10)
			{
				x = coop2Locations[(buttonID - 1) * 2];
				y = coop2Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 23:
			if (buttonID >= 1 && buttonID <= 10)
			{
				x = coop3Locations[(buttonID - 1) * 2];
				y = coop3Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		case 24:
			if (buttonID >= 1 && buttonID <= 10)
			{
				x = coop4Locations[(buttonID - 1) * 2];
				y = coop4Locations[(buttonID - 1) * 2 + 1];
			}
			break;
		}
	}

	private void GetTrailCoord(int trail, int buttonID, out int x, out int y)
	{
		GetRawTrailCoord(trail, buttonID, out x, out y);
		x = x / 2 - 239;
		y /= 2;
	}

	private void UpdateTrailCampaignSelectedButton(int buttonID)
	{
		TriggerAnimatedKnight();
		int num = 0;
		switch (CurrentSelectedTrail)
		{
		case 0:
		{
			num = ConfigSettings.Settings_Progress_Trail;
			if (num > 50)
			{
				num = 50;
			}
			MainViewModel.Instance.TrailMissionTitle = buttonID + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_TRAIL_NAMES_CRU, buttonID);
			int trailStartDate2 = ConfigSettings.getTrailStartDate(CurrentSelectedTrail, buttonID - 1);
			MainViewModel.Instance.TrailMissionDate = trailStartDate2 / 12 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 364);
			trailStartDate2 = ConfigSettings.getTrailStartDate(CurrentSelectedTrail, ConfigSettings.Settings_Progress_Trail - 1);
			MainViewModel.Instance.TrailDate = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, trailStartDate2 % 12) + " " + trailStartDate2 / 12 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 364);
			break;
		}
		case 1:
		{
			num = ConfigSettings.Settings_Progress_Trail2;
			if (num > 30)
			{
				num = 30;
			}
			MainViewModel.Instance.TrailMissionTitle = buttonID + 50 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_TRAIL_NAMES_CRU, buttonID + 50);
			int trailStartDate5 = ConfigSettings.getTrailStartDate(CurrentSelectedTrail, buttonID - 1);
			MainViewModel.Instance.TrailMissionDate = trailStartDate5 / 12 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 364);
			trailStartDate5 = ConfigSettings.getTrailStartDate(CurrentSelectedTrail, ConfigSettings.Settings_Progress_Trail2 - 1);
			MainViewModel.Instance.TrailDate = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, trailStartDate5 % 12) + " " + trailStartDate5 / 12 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 364);
			break;
		}
		case 2:
		{
			num = ConfigSettings.Settings_Progress_Trail3;
			if (num > 20)
			{
				num = 20;
			}
			MainViewModel instance = MainViewModel.Instance;
			int num2 = buttonID;
			instance.TrailMissionTitle = num2 + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_TRAIL_NAMES_CRU, buttonID + 80);
			int trailStartDate = ConfigSettings.getTrailStartDate(CurrentSelectedTrail, buttonID - 1);
			MainViewModel.Instance.TrailMissionDate = trailStartDate / 12 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 364);
			trailStartDate = ConfigSettings.getTrailStartDate(CurrentSelectedTrail, ConfigSettings.Settings_Progress_Trail3 - 1);
			MainViewModel.Instance.TrailDate = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MONTHS, trailStartDate % 12) + " " + trailStartDate / 12 + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 364);
			break;
		}
		case 11:
		case 12:
		case 13:
		case 14:
		case 15:
		case 16:
		case 17:
		case 18:
		{
			int num3 = 5;
			int num4 = ConfigSettings.Settings_Progress_Trail_Sands1;
			int num5 = 7;
			switch (CurrentSelectedTrail)
			{
			case 11:
				num3 = 5;
				num4 = ConfigSettings.Settings_Progress_Trail_Sands1;
				num5 = 7;
				break;
			case 12:
				num3 = 7;
				num4 = ConfigSettings.Settings_Progress_Trail_Sands2;
				num5 = 12;
				break;
			case 13:
				num3 = 9;
				num4 = ConfigSettings.Settings_Progress_Trail_Sands3;
				num5 = 19;
				break;
			case 14:
				num3 = 11;
				num4 = ConfigSettings.Settings_Progress_Trail_Sands4;
				num5 = 28;
				break;
			case 15:
				num3 = 9;
				num4 = ConfigSettings.Settings_Progress_Trail_Sands5;
				num5 = 39;
				break;
			case 16:
				num3 = 9;
				num4 = ConfigSettings.Settings_Progress_Trail_Sands6;
				num5 = 49;
				break;
			case 17:
				num3 = 9;
				num4 = ConfigSettings.Settings_Progress_Trail_Sands7;
				num5 = 82;
				break;
			case 18:
				num3 = 9;
				num4 = ConfigSettings.Settings_Progress_Trail_Sands8;
				num5 = 92;
				break;
			}
			num = num4;
			if (num > num3)
			{
				num = num3;
			}
			int trailStartDate3 = ConfigSettings.getTrailStartDate(CurrentSelectedTrail, buttonID - 1);
			int seconds = 0;
			if (CurrentSelectedTrail <= 16)
			{
				MainViewModel.Instance.TrailTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, CurrentSelectedTrail - 10);
			}
			if (CurrentSelectedTrail == 17)
			{
				MainViewModel.Instance.TrailTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 102);
			}
			if (CurrentSelectedTrail == 18)
			{
				MainViewModel.Instance.TrailTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 103);
			}
			MainViewModel.Instance.TrailMissionTitle = buttonID + ". " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, num5 + buttonID - 1);
			if (trailStartDate3 < 0)
			{
				MainViewModel.Instance.TrailMissionDate = "";
			}
			else
			{
				MainViewModel.Instance.TrailMissionDate = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 62) + " : " + GameData.GetTimeString(trailStartDate3 / 40);
			}
			MainViewModel.Instance.TrailMissionTarget = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 59) + " : " + GameData.Instance.GetSandsOfTimeTargetTime(CurrentSelectedTrail, buttonID - 1, ref seconds);
			MainViewModel.Instance.SandsInProgressVis = Visibility.Visible;
			int trailStartDate4 = ConfigSettings.getTrailStartDate(CurrentSelectedTrail, -1);
			int seconds2 = 0;
			MainViewModel.Instance.TrailDate = GameData.GetTimeString(trailStartDate4 / 40);
			MainViewModel.Instance.TrailTarget = GameData.Instance.GetSandsOfTimeTargetTime(CurrentSelectedTrail, -1000, ref seconds2);
			if (num4 <= num3 || GameData.Instance.SandsUsedChicken(CurrentSelectedTrail))
			{
				MainViewModel.Instance.SandsCompletedRankImage = null;
				MainViewModel.Instance.SandsCompletedRankText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 69);
			}
			else
			{
				MainViewModel.Instance.SandsCompletedRankImage = GameData.Instance.GetSandsOfTimeRankImage(trailStartDate4, seconds2);
				MainViewModel.Instance.SandsCompletedRankText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SANDS_OF_TIME, 60);
			}
			for (int i = 0; i < num3; i++)
			{
				MainViewModel.Instance.TrailCampaignMenuButtonBorders[i] = Visibility.Hidden;
				MainViewModel.Instance.TrailCampaignMenuButtonEnabled[i] = false;
				MainViewModel.Instance.TrailCampaignMenuButtonTimeText[i] = "";
				MainViewModel.Instance.SetSandsTrailRankImage(i, null);
			}
			for (int j = 0; j < num4 - 1; j++)
			{
				MainViewModel.Instance.TrailCampaignMenuButtonEnabled[j] = true;
				if (!ConfigSettings.Settings_HideSoTTiming)
				{
					int num6 = ConfigSettings.getTrailStartDate(CurrentSelectedTrail, j, -99999) / 40;
					if (num6 > 0)
					{
						MainViewModel.Instance.TrailCampaignMenuButtonTimeText[j] = GameData.GetTimeString(num6) + "      ";
						GameData.Instance.GetSandsOfTimeTargetTime(CurrentSelectedTrail, j, ref seconds);
						MainViewModel.Instance.SetSandsTrailRankImage(j, GameData.Instance.GetSandsOfTimeRankImage(ConfigSettings.getTrailStartDate(CurrentSelectedTrail, j), seconds));
					}
					else if (num6 == -2499)
					{
						MainViewModel.Instance.SetSandsTrailRankImage(j, MainViewModel.Instance.GameSprites[592]);
					}
					else
					{
						MainViewModel.Instance.SetSandsTrailRankImage(j, MainViewModel.Instance.GameSprites[642]);
					}
				}
			}
			if (num4 - 1 <= num)
			{
				MainViewModel.Instance.TrailCampaignMenuButtonEnabled[num4 - 1] = true;
			}
			MainViewModel.Instance.TrailCampaignMenuButtonBorders[buttonID - 1] = Visibility.Visible;
			break;
		}
		case 21:
			num = ConfigSettings.Settings_Progress_Trail_Coop1;
			break;
		case 22:
			num = ConfigSettings.Settings_Progress_Trail_Coop2;
			break;
		case 23:
			num = ConfigSettings.Settings_Progress_Trail_Coop3;
			break;
		case 24:
			num = ConfigSettings.Settings_Progress_Trail_Coop4;
			break;
		case 90:
		case 91:
		case 92:
		{
			num = CustomTrailProgress;
			if (num > CustomTrailLength)
			{
				num = CustomTrailLength;
			}
			string text = MapFileManager.SplitCustomTrailName(CustomTrailName);
			MainViewModel.Instance.TrailMissionTitle = buttonID + ". " + text;
			MainViewModel.Instance.TrailMissionDate = "";
			MainViewModel.Instance.TrailDate = "";
			break;
		}
		}
		GetTrailCoord(CurrentSelectedTrail, num, out var x, out var y);
		MainViewModel.Instance.FlagXPos = x + 233 - 70 + 35;
		MainViewModel.Instance.FlagYPos = y + 20 + 38;
		GetTrailCoord(CurrentSelectedTrail, buttonID, out x, out y);
		if (FatControler.arabic)
		{
			if (buttonID == num)
			{
				MainViewModel.Instance.KnightXPos = x + 233 - 120 - 40;
			}
			else
			{
				MainViewModel.Instance.KnightXPos = x + 233 - 120 - 10;
			}
		}
		else if (buttonID == num)
		{
			MainViewModel.Instance.KnightXPos = x + 233 - 120 - 40;
		}
		else
		{
			MainViewModel.Instance.KnightXPos = x + 233 - 120 - 10;
		}
		MainViewModel.Instance.KnightYPos = y - 10;
		MainViewModel.Instance.KnightZPos = y + 40;
	}

	public void UpdateDiffcultyButton()
	{
		if (currentDifficultySetting == 4)
		{
			MainViewModel.Instance.TrailsDifficultySetting = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 215);
		}
		else
		{
			MainViewModel.Instance.TrailsDifficultySetting = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAINOPTIONS, 19 + currentDifficultySetting);
		}
	}

	public void CustomTrailFillWithCrosses()
	{
		int num = 1;
		switch (CurrentSelectedTrail)
		{
		case 90:
			num = 50;
			break;
		case 91:
			num = 30;
			break;
		case 92:
			num = 20;
			break;
		}
		for (int i = 0; i < num; i++)
		{
			GetTrailCoord(CurrentSelectedTrail, i + 1, out var x, out var y);
			ImageSource image = MainViewModel.Instance.GameSprites[735];
			MainViewModel.Instance.TrailSwordXPos[i] = x + 233 - 5 + 65;
			MainViewModel.Instance.TrailSwordYPos[i] = y + 50 + 50;
			MainViewModel.Instance.SetTrailSwordImage(i, image);
		}
	}

	public void GenerateSwords()
	{
		int num = 50;
		int num2 = 1;
		int num3 = 50;
		int[] array = null;
		switch (CurrentSelectedTrail)
		{
		case 0:
			num = ConfigSettings.Settings_Progress_Trail - 1;
			num2 = 50;
			break;
		case 1:
			num = ConfigSettings.Settings_Progress_Trail2 - 1;
			num2 = 30;
			break;
		case 2:
			num = ConfigSettings.Settings_Progress_Trail3 - 1;
			num2 = 20;
			break;
		case 11:
			num = ConfigSettings.Settings_Progress_Trail_Sands1 - 1;
			num2 = 5;
			break;
		case 12:
			num = ConfigSettings.Settings_Progress_Trail_Sands2 - 1;
			num2 = 7;
			break;
		case 13:
			num = ConfigSettings.Settings_Progress_Trail_Sands3 - 1;
			num2 = 9;
			break;
		case 14:
			num = ConfigSettings.Settings_Progress_Trail_Sands4 - 1;
			num2 = 11;
			break;
		case 15:
			num = ConfigSettings.Settings_Progress_Trail_Sands5 - 1;
			num2 = 9;
			break;
		case 16:
			num = ConfigSettings.Settings_Progress_Trail_Sands6 - 1;
			num2 = 9;
			break;
		case 17:
			num = ConfigSettings.Settings_Progress_Trail_Sands7 - 1;
			num2 = 9;
			break;
		case 18:
			num = ConfigSettings.Settings_Progress_Trail_Sands8 - 1;
			num2 = 9;
			break;
		case 21:
			num = ConfigSettings.Settings_Progress_Trail_Coop1 - 1;
			num2 = 10;
			break;
		case 22:
			num = ConfigSettings.Settings_Progress_Trail_Coop2 - 1;
			num2 = 10;
			break;
		case 23:
			num = ConfigSettings.Settings_Progress_Trail_Coop3 - 1;
			num2 = 10;
			break;
		case 24:
			num = ConfigSettings.Settings_Progress_Trail_Coop4 - 1;
			num2 = 10;
			break;
		case 90:
			num = CustomTrailProgress - 1;
			num3 = (num2 = CustomTrailLength);
			array = ConfigSettings.GetCustomTrailStatus(CustomTrailName);
			break;
		case 91:
			num = CustomTrailProgress - 1;
			num3 = (num2 = CustomTrailLength);
			array = ConfigSettings.GetCustomTrailStatus(CustomTrailName);
			break;
		case 92:
			num = CustomTrailProgress - 1;
			num3 = (num2 = CustomTrailLength);
			array = ConfigSettings.GetCustomTrailStatus(CustomTrailName);
			break;
		}
		bool flag = false;
		int num4 = 0;
		for (int i = 0; i < num; i++)
		{
			GetTrailCoord(CurrentSelectedTrail, i + 1, out var x, out var y);
			if (CurrentSelectedTrail == 0 && (i == 18 || i == 34))
			{
				num4 += 4;
			}
			bool flag2 = false;
			ImageSource image = MainViewModel.Instance.GameSprites[471 + sword_ang[i] + num4];
			switch (CurrentSelectedTrail)
			{
			case 0:
				if (ConfigSettings.Settings_Trail1Times[i] < 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 1:
				if (ConfigSettings.Settings_Trail2Times[i] < 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 2:
				if (ConfigSettings.Settings_Trail3Times[i] < 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 11:
				if (ConfigSettings.Settings_Trail_Sands1_Times[i] < 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 12:
				if (ConfigSettings.Settings_Trail_Sands2_Times[i] < 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 13:
				if (ConfigSettings.Settings_Trail_Sands3_Times[i] < 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 14:
				if (ConfigSettings.Settings_Trail_Sands4_Times[i] < 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 15:
				if (ConfigSettings.Settings_Trail_Sands5_Times[i] < 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 16:
				if (ConfigSettings.Settings_Trail_Sands6_Times[i] < 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 17:
				if (ConfigSettings.Settings_Trail_Sands7_Times[i] < 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 18:
				if (ConfigSettings.Settings_Trail_Sands8_Times[i] < 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 21:
				if (ConfigSettings.Settings_Progress_Trail_Coop1_Status[i] == 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 22:
				if (ConfigSettings.Settings_Progress_Trail_Coop2_Status[i] == 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 23:
				if (ConfigSettings.Settings_Progress_Trail_Coop3_Status[i] == 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 24:
				if (ConfigSettings.Settings_Progress_Trail_Coop4_Status[i] == 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			case 90:
			case 91:
			case 92:
				if (array[i] < 0)
				{
					image = MainViewModel.Instance.GameSprites[trailChickens[i]];
					flag2 = true;
				}
				break;
			}
			if (!flag2)
			{
				if (FatControler.arabic && !ConfigSettings.Settings_ArabicL2R)
				{
					MainViewModel.Instance.TrailSwordXPos[i] = x + 233 - 5 + 50;
					MainViewModel.Instance.TrailSwordYPos[i] = y + 50;
				}
				else
				{
					MainViewModel.Instance.TrailSwordXPos[i] = x + 233 - 5;
					MainViewModel.Instance.TrailSwordYPos[i] = y + 50;
				}
			}
			else
			{
				flag = true;
				if (FatControler.arabic && !ConfigSettings.Settings_ArabicL2R)
				{
					MainViewModel.Instance.TrailSwordXPos[i] = x + 233 - 5 + 50 + 10;
					MainViewModel.Instance.TrailSwordYPos[i] = y + 50 + 15;
				}
				else
				{
					MainViewModel.Instance.TrailSwordXPos[i] = x + 233 - 5 + 50;
					MainViewModel.Instance.TrailSwordYPos[i] = y + 50 + 15;
				}
			}
			MainViewModel.Instance.SetTrailSwordImage(i, image);
		}
		for (int j = num; j < num3; j++)
		{
			MainViewModel.Instance.SetTrailSwordImage(j, null);
		}
		if (num == num2)
		{
			trailCompleted = true;
			trailCompletedWithCheats = flag;
		}
		else
		{
			trailCompleted = false;
			trailCompletedWithCheats = false;
		}
	}

	public void TrailMapClicked(int mapX, int mapY)
	{
		mapX *= 2;
		mapY *= 2;
		int num = 50;
		switch (CurrentSelectedTrail)
		{
		case 0:
			num = ConfigSettings.Settings_Progress_Trail;
			break;
		case 1:
			num = ConfigSettings.Settings_Progress_Trail2;
			break;
		case 2:
			num = ConfigSettings.Settings_Progress_Trail3;
			break;
		case 11:
			num = ConfigSettings.Settings_Progress_Trail_Sands1;
			break;
		case 12:
			num = ConfigSettings.Settings_Progress_Trail_Sands2;
			break;
		case 13:
			num = ConfigSettings.Settings_Progress_Trail_Sands3;
			break;
		case 14:
			num = ConfigSettings.Settings_Progress_Trail_Sands4;
			break;
		case 15:
			num = ConfigSettings.Settings_Progress_Trail_Sands5;
			break;
		case 16:
			num = ConfigSettings.Settings_Progress_Trail_Sands6;
			break;
		case 17:
			num = ConfigSettings.Settings_Progress_Trail_Sands7;
			break;
		case 18:
			num = ConfigSettings.Settings_Progress_Trail_Sands8;
			break;
		case 21:
			num = ConfigSettings.Settings_Progress_Trail_Coop1;
			break;
		case 22:
			num = ConfigSettings.Settings_Progress_Trail_Coop2;
			break;
		case 23:
			num = ConfigSettings.Settings_Progress_Trail_Coop3;
			break;
		case 24:
			num = ConfigSettings.Settings_Progress_Trail_Coop4;
			break;
		case 90:
			num = Math.Min(CustomTrailProgress, CustomTrailLength);
			break;
		case 91:
			num = Math.Min(CustomTrailProgress, CustomTrailLength);
			break;
		case 92:
			num = Math.Min(CustomTrailProgress, CustomTrailLength);
			break;
		}
		for (int i = 0; i < num; i++)
		{
			GetRawTrailCoord(CurrentSelectedTrail, i + 1, out var x, out var y);
			if (mapX >= x - 60 && mapX <= x + 60 && mapY >= y - 32 && mapY <= y + 32)
			{
				ButtonTrailCampaignClicked(i + 1, fromRealClick: true);
			}
		}
	}

	private void TriggerAnimatedKnight()
	{
		sktrail_knights_anim = 1;
		knight_anim_time = DateTime.UtcNow;
	}

	private void AnimateKnightAndChickenButton()
	{
		int num = 0;
		DateTime utcNow = DateTime.UtcNow;
		double totalMilliseconds = (utcNow - knight_anim_time).TotalMilliseconds;
		if (sktrail_knights_anim == 0)
		{
			num = (int)(totalMilliseconds / 50.0);
			if (num > 18)
			{
				num = 0;
				knight_anim_time = DateTime.UtcNow;
			}
		}
		if (sktrail_knights_anim > 0)
		{
			num = (int)(totalMilliseconds / 50.0);
			if (num > 30)
			{
				num = 30;
				sktrail_knights_anim = 0;
				knight_anim_time = DateTime.UtcNow;
			}
			num += 19;
		}
		int currentSelectedTrail = CurrentSelectedTrail;
		if ((uint)(currentSelectedTrail - 21) <= 3u)
		{
			MainViewModel.Instance.TrailKnight = MainViewModel.Instance.GameSprites[718];
		}
		else
		{
			MainViewModel.Instance.TrailKnight = MainViewModel.Instance.GameSprites[526 + num];
		}
		if (!(refTrailChicken != null))
		{
			return;
		}
		if (trailCompleted)
		{
			if (trailCompletedWithCheats)
			{
				refTrailChicken.Source = MainViewModel.Instance.GameSprites[708];
			}
			else
			{
				refTrailChicken.Source = MainViewModel.Instance.GameSprites[707];
			}
			return;
		}
		int num2 = (int)((utcNow - chickenAnimTime).TotalMilliseconds / 66.0);
		if (!chickenRollover)
		{
			if (num2 > 31)
			{
				num2 = 0;
				chickenAnimTime = DateTime.UtcNow;
			}
			refTrailChicken.Source = MainViewModel.Instance.GameSprites[593 + num2];
		}
		else
		{
			if (num2 > 14)
			{
				num2 = 0;
				chickenAnimTime = DateTime.UtcNow;
			}
			refTrailChicken.Source = MainViewModel.Instance.GameSprites[593 + num2 + 32];
		}
	}

	public void ChickenCommandEnter(object sender, MouseEventArgs e)
	{
		if (!trailCompleted)
		{
			chickenRollover = true;
			chickenAnimTime = DateTime.UtcNow;
			MainViewModel.Instance.ChickenCheatRolloverVis = Visibility.Visible;
		}
	}

	public void ChickenCommandLeave(object sender, MouseEventArgs e)
	{
		chickenRollover = false;
		chickenAnimTime = DateTime.UtcNow;
		MainViewModel.Instance.ChickenCheatRolloverVis = Visibility.Hidden;
	}

	public void StartTrailMission(int missionID, int trailID)
	{
		int difficulty = 1;
		switch (trailID)
		{
		case 0:
			difficulty = ConfigSettings.Settings_Trail1Difficulty;
			break;
		case 1:
			difficulty = ConfigSettings.Settings_Trail2Difficulty;
			break;
		case 2:
			difficulty = ConfigSettings.Settings_Trail3Difficulty;
			break;
		case 11:
			if (ConfigSettings.Settings_HideSoTTiming)
			{
				difficulty = ConfigSettings.Settings_SandsTrail1Difficulty;
			}
			break;
		case 12:
			if (ConfigSettings.Settings_HideSoTTiming)
			{
				difficulty = ConfigSettings.Settings_SandsTrail2Difficulty;
			}
			break;
		case 13:
			if (ConfigSettings.Settings_HideSoTTiming)
			{
				difficulty = ConfigSettings.Settings_SandsTrail3Difficulty;
			}
			break;
		case 14:
			if (ConfigSettings.Settings_HideSoTTiming)
			{
				difficulty = ConfigSettings.Settings_SandsTrail4Difficulty;
			}
			break;
		case 15:
			if (ConfigSettings.Settings_HideSoTTiming)
			{
				difficulty = ConfigSettings.Settings_SandsTrail5Difficulty;
			}
			break;
		case 16:
			if (ConfigSettings.Settings_HideSoTTiming)
			{
				difficulty = ConfigSettings.Settings_SandsTrail6Difficulty;
			}
			break;
		case 17:
			if (ConfigSettings.Settings_HideSoTTiming)
			{
				difficulty = ConfigSettings.Settings_SandsTrail7Difficulty;
			}
			break;
		case 18:
			if (ConfigSettings.Settings_HideSoTTiming)
			{
				difficulty = ConfigSettings.Settings_SandsTrail8Difficulty;
			}
			break;
		}
		MainViewModel.Instance.StartSkirmishTrailMission(trailID, missionID, difficulty);
	}

	private void StartCustomTrailMission(string CustomTrailName, int missionID)
	{
		MainViewModel.Instance.StartCustomTrailMission(CustomTrailName, missionID, currentDifficultySetting);
	}

	private void UpdateCampaignListButtonVisibility(int unlockedLevel)
	{
		for (int i = 1; i <= 21; i++)
		{
			if (i <= unlockedLevel)
			{
				MainViewModel.Instance.CampaignMenuButtonsVisible[i] = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.CampaignMenuButtonsVisible[i] = Visibility.Hidden;
			}
		}
	}

	private void UpdateTutorialHelpVisibility(bool state)
	{
		if (ForceShowTutorialHelp)
		{
			MainViewModel.Instance.ShowTutorialHelpText = Visibility.Visible;
		}
		else if (state)
		{
			MainViewModel.Instance.ShowTutorialHelpText = Visibility.Visible;
		}
		else
		{
			MainViewModel.Instance.ShowTutorialHelpText = Visibility.Hidden;
		}
	}

	public void UpdateFrontMenuPopupScale()
	{
		float num = Screen.width;
		float num2 = Screen.height;
		float num3 = 1f;
		if (num < 1920f || num2 < 1080f)
		{
			int num4 = 1920;
			int num5 = 1080;
			float a = num / (float)num4;
			float b = num2 / (float)num5;
			num3 = 1f / Mathf.Min(a, b);
			if (num3 < 1f)
			{
				num3 = 1f;
			}
		}
		if (Screen.width > 1366 && Screen.height > 768)
		{
			num3 = (1.6f - num3) * ConfigSettings.Settings_UIScale + num3;
		}
		MainViewModel.Instance.FrontEndRequesterWidth = (int)(1036f * num3);
		MainViewModel.Instance.FrontEndRequesterHeight = (int)(636f * num3);
		MainViewModel.Instance.FrontEndOptionsWidth = (int)(836f * num3);
		MainViewModel.Instance.FrontEndOptionsHeight = (int)(636f * num3);
		MainViewModel.Instance.FrontEndHelpWidth = (int)(856f * num3);
		MainViewModel.Instance.FrontEndHelpHeight = (int)(536f * num3);
		MainViewModel.Instance.FrontEndConfirmationWidth = (int)((double)HUD_ConfirmationPopup.ConfirmationWidth * 1.2 * (double)num3);
		MainViewModel.Instance.FrontEndConfirmationHeight = (int)((double)HUD_ConfirmationPopup.ConfirmationHeight * 1.2 * (double)num3);
		UpdateVideoScale();
	}

	public void ShowMPConnectionPopup()
	{
		HUD_ConfirmationPopup.ShowConfirmationOKMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 446), delegate
		{
		}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 447));
	}

	public void PlayBackgroundVideo(bool state)
	{
		if (state)
		{
			UpdateVideoScale();
			refFrontendBackVideo.Play();
			isVideoPlaying = true;
		}
		else
		{
			refFrontendBackVideo.Stop();
			isVideoPlaying = false;
		}
	}

	private void FrontendBackVideo_Opened(object sender, RoutedEventArgs args)
	{
		MainViewModel.Instance.PreLoadBlankVis = false;
		refFrontendBackVideo.Play();
		isVideoPlaying = true;
		MainViewModel.Instance.FrontEndMenu.refMainMenu_ShowMainMenu.Begin();
	}

	private void FrontendBackVideo_Ended(object sender, RoutedEventArgs args)
	{
		if (loopBackgroundVideo)
		{
			refFrontendBackVideo.Stop();
			refFrontendBackVideo.Play();
			isVideoPlaying = true;
		}
	}

	public static void UpdateVideoScale()
	{
		float num = Screen.width;
		float num2 = Screen.height;
		float num3 = 1.7777778f;
		float num4 = num / num2;
		if (num4 == num3)
		{
			MainViewModel.Instance.FrontEndVideoMargin = "0,0";
		}
		else if (num4 > num3)
		{
			MainViewModel.Instance.FrontEndVideoMargin = "0,-1079";
		}
		else
		{
			MainViewModel.Instance.FrontEndVideoMargin = "-1919,0";
		}
	}

	private void InitializeComponent()
	{
		Noesis.GUI.LoadComponent(this, "Assets/GUI/XAML/FrontendMenus.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "MouseEnter" && handlerName == "MouseEnterMainButtonHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MouseEnterMainButtonHandler;
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "MouseLeaveMainButtonHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseLeave += MouseLeaveMainButtonHandler;
			}
			return true;
		}
		if (eventName == "MouseDown" && handlerName == "SandsClickHandler")
		{
			if (source is Noesis.Grid)
			{
				((Noesis.Grid)source).MouseDown += SandsClickHandler;
			}
			return true;
		}
		return false;
	}
}
