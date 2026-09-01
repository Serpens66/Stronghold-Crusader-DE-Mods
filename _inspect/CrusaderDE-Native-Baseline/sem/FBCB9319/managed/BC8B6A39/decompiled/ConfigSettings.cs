using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CrusaderDE;
using UnityEngine;

public class ConfigSettings
{
	private class Scores
	{
		public string mapName;

		public int difficulty = 1000;
	}

	[Serializable]
	private class SkirmishMasters
	{
		public List<EngineInterface.MPScoreData> skirmishMastersData = new List<EngineInterface.MPScoreData>();
	}

	private class Coop
	{
		public ulong steamID;

		public string userName;

		public bool hidden;

		public string CoAString = "";

		public int[] trail1 = new int[10];

		public int[] trail2 = new int[10];

		public int[] trail3 = new int[10];

		public int[] trail4 = new int[10];
	}

	public const int SCREEN_SIZE_MINIMUM_WIDTH = 1280;

	public const int SCREEN_SIZE_MINIMUM_HEIGHT = 768;

	public static bool AchievementsDisabled = false;

	public static bool TempMissionUnlock = false;

	private static bool settingsDirty = false;

	private static bool _settingsFileExisted = false;

	private static string settings_UserName = "Lord Crusader";

	private static string settings_NewsletterEmail = "";

	private static int[] settings_Trail1Times = new int[50];

	private static int[] settings_Trail2Times = new int[30];

	private static int[] settings_Trail3Times = new int[20];

	private static int[] settings_Trail_Sands1_Times = new int[5];

	private static int[] settings_Trail_Sands2_Times = new int[7];

	private static int[] settings_Trail_Sands3_Times = new int[9];

	private static int[] settings_Trail_Sands4_Times = new int[11];

	private static int[] settings_Trail_Sands5_Times = new int[9];

	private static int[] settings_Trail_Sands6_Times = new int[9];

	private static int[] settings_Trail_Sands7_Times = new int[9];

	private static int[] settings_Trail_Sands8_Times = new int[9];

	private static bool settings_ShowSandsIntro = true;

	private static bool settings_PushMapScrolling = true;

	private static bool settings_SH1MouseWheel = false;

	private static bool settings_SH1RTSControls = true;

	private static bool settings_SH1CentreControls = true;

	private static bool settings_TroopMoveMode = false;

	private static bool settings_ShowBuildingTooltips = true;

	public const float Default_MusicVolume = 1f;

	private static float settings_MusicVolume = 1f;

	public const float Default_SpeechVolume = 1f;

	private static float settings_SpeechVolume = 1f;

	public const float Default_UnitSpeechVolume = 1f;

	private static float settings_UnitSpeechVolume = 1f;

	public const float Default_SFXVolume = 1f;

	private static float settings_SFXVolume = 1f;

	public const float Default_MasterVolume = 0.8f;

	private static float settings_MasterVolume = 0.8f;

	private static bool settings_PlayUISFX = true;

	private static bool settings_GenieSpeech = true;

	private static bool settings_MuteInsults = false;

	private static bool settings_MuteInsultSpeech = false;

	private static bool settings_BackgroundAudio = true;

	private static bool settings_ReduceMusicVolumeForSpeech = true;

	private static bool settings_UseSteamOverlayForHelp = false;

	private static bool settings_CheatKeysEnabled = false;

	private static bool settings_CoopCheatsEnabled = false;

	private static bool settings_ShowPings = false;

	private static bool settings_ExtraZoom = true;

	private static bool settings_Leaderboard_OptOut = false;

	private static bool settings_Leaderboard_Names = false;

	private static bool settings_Leaderboard_Images = false;

	private static bool settings_Show_Extreme_Warning = true;

	public const int Default_ScrollSpeed = 5;

	private static int settings_ScrollSpeed = 5;

	private static bool settings_Confirm_Disband_Troops = true;

	private static bool settings_ArabicL2R = false;

	private static bool settings_Compass = false;

	private static bool settings_ShowGameTime = false;

	private static bool settings_ShowLocalTime = false;

	private static bool settings_ShowSandsTimer = true;

	private static bool settings_RadarDefaultZoomedOut = false;

	private static bool settings_CustomIntros = true;

	public const int Default_GameSpeed = 40;

	private static int settings_GameSpeed = 40;

	private static bool settings_ShowPlannedMoat = false;

	private static bool settings_LockCursor = false;

	private static bool settings_Vsync = false;

	private static bool settings_EnglishSpeech = false;

	private static bool settings_SkipIntro = false;

	private static int settings_Scribe = 2;

	private static bool settings_HideSoTTiming = false;

	private static bool settings_Allow_Classic_Bedouin_Stockade = false;

	private static bool settings_ShowCustomisationWarning = true;

	private static int settings_LordType = 0;

	private static int settings_AvatarOrdinary = 1002;

	private static int settings_AvatarOrdinaryColour1 = 22;

	private static int settings_AvatarOrdinaryColour2 = 11;

	private static int settings_AvatarCharge = 2019;

	private static int settings_AvatarChargeColour1 = 10;

	private static int settings_AvatarChargeColour2 = 110;

	private static bool settings_UseSteamAvatar = false;

	private static bool settings_MuteMPChat = false;

	private static int settings_CursorStyle = 0;

	private static int settings_PlayerColour = 0;

	private static int settings_LastWindowWidth = -1;

	private static int settings_LastWindowHeight = -1;

	private static int settings_LastFullscreenWidth = -1;

	private static int settings_LastFullscreenHeight = -1;

	private static int settings_LastFullscreenRefresh = -1;

	private static int settings_LastFullscreenType = -1;

	private static float settings_UIScale = 1f;

	private static string settings_MPPresets1 = "";

	private static string settings_MPPresets2 = "";

	private static string settings_SkirmishPresets = "";

	private static int settings_Progress_Historical1Campaign = 1;

	private static int settings_Progress_Historical2Campaign = 1;

	private static int settings_Progress_Historical3Campaign = 1;

	private static int settings_Progress_Historical4Campaign = 1;

	private static int settings_Progress_Historical5Campaign = 1;

	private static int settings_Progress_Historical6Campaign = 1;

	private static int settings_Progress_Historical7Campaign = 1;

	private static int settings_Progress_Trail = 1;

	private static int settings_Progress_Trail2 = 1;

	private static int settings_Progress_Trail3 = 1;

	private static int settings_Progress_Trail_Sands1 = 1;

	private static int settings_Progress_Trail_Sands2 = 1;

	private static int settings_Progress_Trail_Sands3 = 1;

	private static int settings_Progress_Trail_Sands4 = 1;

	private static int settings_Progress_Trail_Sands5 = 1;

	private static int settings_Progress_Trail_Sands6 = 1;

	private static int settings_Progress_Trail_Sands7 = 1;

	private static int settings_Progress_Trail_Sands8 = 1;

	private static bool settings_ShowExtremeHelp = true;

	private static bool settings_DLC4_Pip1 = true;

	private static bool settings_DLC4_Pip2 = true;

	private static bool settings_DLC4_Pip3 = true;

	private static bool settings_DLC4_Pip4 = true;

	private static bool settings_DLC4_Pip5 = true;

	private static bool settings_DLC4_Pip6 = true;

	private static int settings_Trail1Difficulty = 1;

	private static int settings_Trail2Difficulty = 1;

	private static int settings_Trail3Difficulty = 1;

	private static int settings_SandsTrail1Difficulty = 1;

	private static int settings_SandsTrail2Difficulty = 1;

	private static int settings_SandsTrail3Difficulty = 1;

	private static int settings_SandsTrail4Difficulty = 1;

	private static int settings_SandsTrail5Difficulty = 1;

	private static int settings_SandsTrail6Difficulty = 1;

	private static int settings_SandsTrail7Difficulty = 1;

	private static int settings_SandsTrail8Difficulty = 1;

	private static bool muteSounds = false;

	public static string[] extendedLordPaths = new string[29]
	{
		"Rat", "Snake", "Pig", "Wolf", "Saladin", "Caliph", "Sultan", "Richard", "Frederick", "Philip",
		"Wazir", "Emir", "Nizar", "Sheriff", "Marshal", "Abbot", "Jewel", "Sentinel", "Nomad", "Kahinah",
		"Canary", "Trader", "Sergeant", "Lioness", "Crocodile", "Baldwin", "Bullseye", "Surgeon", "Baibars"
	};

	private static Dictionary<string, Scores> scores = new Dictionary<string, Scores>();

	private static SkirmishMasters skirmishMasters = null;

	public static int Settings_Progress_Trail_Coop1 = 1;

	public static int Settings_Progress_Trail_Coop2 = 1;

	public static int Settings_Progress_Trail_CoopNext1 = 1;

	public static int Settings_Progress_Trail_CoopNext2 = 1;

	public static int[] Settings_Progress_Trail_Coop1_Status = new int[10];

	public static int[] Settings_Progress_Trail_Coop2_Status = new int[10];

	public static int Settings_Progress_Trail_Coop3 = 1;

	public static int Settings_Progress_Trail_CoopNext3 = 1;

	public static int[] Settings_Progress_Trail_Coop3_Status = new int[10];

	public static int Settings_Progress_Trail_Coop4 = 1;

	public static int Settings_Progress_Trail_CoopNext4 = 1;

	public static int[] Settings_Progress_Trail_Coop4_Status = new int[10];

	private static Dictionary<ulong, Coop> coopInfoDict = new Dictionary<ulong, Coop>();

	private static List<Coop> coopInfoList = new List<Coop>();

	private static Dictionary<string, int[]> customTrailCompleted = new Dictionary<string, int[]>();

	public static bool SettingsFileExisted => _settingsFileExisted;

	public static string Settings_UserName
	{
		get
		{
			return settings_UserName;
		}
		set
		{
			if (settings_UserName != value)
			{
				settingsDirty = true;
				settings_UserName = value;
			}
		}
	}

	public static string Settings_NewsletterEmail
	{
		get
		{
			return settings_NewsletterEmail;
		}
		set
		{
			if (settings_NewsletterEmail != value)
			{
				settingsDirty = true;
				settings_NewsletterEmail = value;
			}
		}
	}

	public static int[] Settings_Trail1Times => settings_Trail1Times;

	public static int[] Settings_Trail2Times => settings_Trail2Times;

	public static int[] Settings_Trail3Times => settings_Trail3Times;

	public static int[] Settings_Trail_Sands1_Times => settings_Trail_Sands1_Times;

	public static int[] Settings_Trail_Sands2_Times => settings_Trail_Sands2_Times;

	public static int[] Settings_Trail_Sands3_Times => settings_Trail_Sands3_Times;

	public static int[] Settings_Trail_Sands4_Times => settings_Trail_Sands4_Times;

	public static int[] Settings_Trail_Sands5_Times => settings_Trail_Sands5_Times;

	public static int[] Settings_Trail_Sands6_Times => settings_Trail_Sands6_Times;

	public static int[] Settings_Trail_Sands7_Times => settings_Trail_Sands7_Times;

	public static int[] Settings_Trail_Sands8_Times => settings_Trail_Sands8_Times;

	public static bool Settings_ShowSandsIntro
	{
		get
		{
			return settings_ShowSandsIntro;
		}
		set
		{
			if (settings_ShowSandsIntro != value)
			{
				settingsDirty = true;
				settings_ShowSandsIntro = value;
			}
		}
	}

	public static bool Settings_PushMapScrolling
	{
		get
		{
			return settings_PushMapScrolling;
		}
		set
		{
			if (settings_PushMapScrolling != value)
			{
				settingsDirty = true;
				settings_PushMapScrolling = value;
			}
		}
	}

	public static bool Settings_SH1MouseWheel
	{
		get
		{
			return settings_SH1MouseWheel;
		}
		set
		{
			if (settings_SH1MouseWheel != value)
			{
				settingsDirty = true;
				settings_SH1MouseWheel = value;
			}
		}
	}

	public static bool Settings_SH1RTSControls
	{
		get
		{
			return settings_SH1RTSControls;
		}
		set
		{
			if (settings_SH1RTSControls != value)
			{
				settingsDirty = true;
				settings_SH1RTSControls = value;
			}
		}
	}

	public static bool Settings_SH1CentreControls
	{
		get
		{
			return settings_SH1CentreControls;
		}
		set
		{
			if (settings_SH1CentreControls != value)
			{
				settingsDirty = true;
				settings_SH1CentreControls = value;
			}
		}
	}

	public static bool Settings_TroopMoveMode
	{
		get
		{
			return settings_TroopMoveMode;
		}
		set
		{
			if (settings_TroopMoveMode != value)
			{
				settingsDirty = true;
				settings_TroopMoveMode = value;
			}
		}
	}

	public static bool Settings_ShowBuildingTooltips
	{
		get
		{
			return settings_ShowBuildingTooltips;
		}
		set
		{
			if (settings_ShowBuildingTooltips != value)
			{
				settingsDirty = true;
				settings_ShowBuildingTooltips = value;
			}
		}
	}

	public static float Settings_MusicVolume
	{
		get
		{
			return settings_MusicVolume;
		}
		set
		{
			if (settings_MusicVolume != value)
			{
				settingsDirty = true;
				settings_MusicVolume = value;
			}
		}
	}

	public static float Settings_SpeechVolume
	{
		get
		{
			return settings_SpeechVolume;
		}
		set
		{
			if (settings_SpeechVolume != value)
			{
				settingsDirty = true;
				settings_SpeechVolume = value;
			}
		}
	}

	public static float Settings_UnitSpeechVolume
	{
		get
		{
			return settings_UnitSpeechVolume;
		}
		set
		{
			if (settings_UnitSpeechVolume != value)
			{
				settingsDirty = true;
				settings_UnitSpeechVolume = value;
			}
		}
	}

	public static float Settings_SFXVolume
	{
		get
		{
			return settings_SFXVolume;
		}
		set
		{
			if (settings_SFXVolume != value)
			{
				settingsDirty = true;
				settings_SFXVolume = value;
			}
		}
	}

	public static float Settings_MasterVolume
	{
		get
		{
			if (!muteSounds)
			{
				return settings_MasterVolume;
			}
			return 0f;
		}
		set
		{
			if (settings_MasterVolume != value)
			{
				settingsDirty = true;
				settings_MasterVolume = value;
			}
		}
	}

	public static bool Settings_PlayUISFX
	{
		get
		{
			return settings_PlayUISFX;
		}
		set
		{
			if (settings_PlayUISFX != value)
			{
				settingsDirty = true;
				settings_PlayUISFX = value;
			}
		}
	}

	public static bool Settings_GenieSpeech
	{
		get
		{
			return settings_GenieSpeech;
		}
		set
		{
			if (settings_GenieSpeech != value)
			{
				settingsDirty = true;
				settings_GenieSpeech = value;
			}
		}
	}

	public static bool Settings_MuteInsults
	{
		get
		{
			return settings_MuteInsults;
		}
		set
		{
			if (settings_MuteInsults != value)
			{
				settingsDirty = true;
				settings_MuteInsults = value;
			}
		}
	}

	public static bool Settings_MuteInsultSpeech
	{
		get
		{
			return settings_MuteInsultSpeech;
		}
		set
		{
			if (settings_MuteInsultSpeech != value)
			{
				settingsDirty = true;
				settings_MuteInsultSpeech = value;
			}
		}
	}

	public static bool Settings_BackgroundAudio
	{
		get
		{
			return settings_BackgroundAudio;
		}
		set
		{
			if (settings_BackgroundAudio != value)
			{
				settingsDirty = true;
				settings_BackgroundAudio = value;
			}
		}
	}

	public static bool Settings_ReduceMusicVolumeForSpeech
	{
		get
		{
			return settings_ReduceMusicVolumeForSpeech;
		}
		set
		{
			if (settings_ReduceMusicVolumeForSpeech != value)
			{
				settingsDirty = true;
				settings_ReduceMusicVolumeForSpeech = value;
			}
		}
	}

	public static bool Settings_UseSteamOverlayForHelp
	{
		get
		{
			return settings_UseSteamOverlayForHelp;
		}
		set
		{
			if (settings_UseSteamOverlayForHelp != value)
			{
				settingsDirty = true;
				settings_UseSteamOverlayForHelp = value;
			}
		}
	}

	public static bool Settings_CheatKeysEnabled
	{
		get
		{
			return settings_CheatKeysEnabled;
		}
		set
		{
			if (settings_CheatKeysEnabled != value)
			{
				settingsDirty = true;
				settings_CheatKeysEnabled = value;
			}
		}
	}

	public static bool Settings_CoopCheatsEnabled
	{
		get
		{
			return settings_CoopCheatsEnabled;
		}
		set
		{
			if (settings_CoopCheatsEnabled != value)
			{
				settingsDirty = true;
				settings_CoopCheatsEnabled = value;
			}
		}
	}

	public static bool Settings_ShowPings
	{
		get
		{
			return settings_ShowPings;
		}
		set
		{
			if (settings_ShowPings != value)
			{
				settingsDirty = true;
				settings_ShowPings = value;
			}
		}
	}

	public static bool Settings_ExtraZoom
	{
		get
		{
			return settings_ExtraZoom;
		}
		set
		{
			if (settings_ExtraZoom != value)
			{
				settingsDirty = true;
				settings_ExtraZoom = value;
			}
		}
	}

	public static bool Settings_Leaderboard_OptOut
	{
		get
		{
			return settings_Leaderboard_OptOut;
		}
		set
		{
			if (settings_Leaderboard_OptOut != value)
			{
				settingsDirty = true;
				settings_Leaderboard_OptOut = value;
			}
		}
	}

	public static bool Settings_Leaderboard_Names
	{
		get
		{
			return settings_Leaderboard_Names;
		}
		set
		{
			if (settings_Leaderboard_Names != value)
			{
				settingsDirty = true;
				settings_Leaderboard_Names = value;
			}
		}
	}

	public static bool Settings_Leaderboard_Images
	{
		get
		{
			return settings_Leaderboard_Images;
		}
		set
		{
			if (settings_Leaderboard_Images != value)
			{
				settingsDirty = true;
				settings_Leaderboard_Images = value;
			}
		}
	}

	public static bool Settings_Show_Extreme_Warning
	{
		get
		{
			return settings_Show_Extreme_Warning;
		}
		set
		{
			if (settings_Show_Extreme_Warning != value)
			{
				settingsDirty = true;
				settings_Show_Extreme_Warning = value;
			}
		}
	}

	public static int Settings_ScrollSpeed
	{
		get
		{
			return settings_ScrollSpeed;
		}
		set
		{
			if (settings_ScrollSpeed != value)
			{
				settingsDirty = true;
				settings_ScrollSpeed = value;
			}
		}
	}

	public static bool Settings_Confirm_Disband_Troops
	{
		get
		{
			return settings_Confirm_Disband_Troops;
		}
		set
		{
			if (settings_Confirm_Disband_Troops != value)
			{
				settingsDirty = true;
				settings_Confirm_Disband_Troops = value;
			}
		}
	}

	public static bool Settings_ArabicL2R
	{
		get
		{
			return settings_ArabicL2R;
		}
		set
		{
			if (settings_ArabicL2R != value)
			{
				settingsDirty = true;
				settings_ArabicL2R = value;
			}
		}
	}

	public static bool Settings_Compass
	{
		get
		{
			return settings_Compass;
		}
		set
		{
			if (settings_Compass != value)
			{
				settingsDirty = true;
				settings_Compass = value;
			}
		}
	}

	public static bool Settings_ShowGameTime
	{
		get
		{
			return settings_ShowGameTime;
		}
		set
		{
			if (settings_ShowGameTime != value)
			{
				settingsDirty = true;
				settings_ShowGameTime = value;
			}
		}
	}

	public static bool Settings_ShowLocalTime
	{
		get
		{
			return settings_ShowLocalTime;
		}
		set
		{
			if (settings_ShowLocalTime != value)
			{
				settingsDirty = true;
				settings_ShowLocalTime = value;
			}
		}
	}

	public static bool Settings_ShowSandsTimer
	{
		get
		{
			return settings_ShowSandsTimer;
		}
		set
		{
			if (settings_ShowSandsTimer != value)
			{
				settingsDirty = true;
				settings_ShowSandsTimer = value;
			}
		}
	}

	public static bool Settings_RadarDefaultZoomedOut
	{
		get
		{
			return settings_RadarDefaultZoomedOut;
		}
		set
		{
			if (settings_RadarDefaultZoomedOut != value)
			{
				settingsDirty = true;
				settings_RadarDefaultZoomedOut = value;
			}
		}
	}

	public static bool Settings_CustomIntros
	{
		get
		{
			return settings_CustomIntros;
		}
		set
		{
			if (settings_CustomIntros != value)
			{
				settingsDirty = true;
				settings_CustomIntros = value;
			}
		}
	}

	public static int Settings_GameSpeed
	{
		get
		{
			return settings_GameSpeed;
		}
		set
		{
			if (settings_GameSpeed != value)
			{
				settingsDirty = true;
				settings_GameSpeed = value;
			}
		}
	}

	public static bool Settings_ShowPlannedMoat
	{
		get
		{
			return settings_ShowPlannedMoat;
		}
		set
		{
			if (settings_ShowPlannedMoat != value)
			{
				settingsDirty = true;
				settings_ShowPlannedMoat = value;
			}
		}
	}

	public static bool Settings_LockCursor
	{
		get
		{
			return settings_LockCursor;
		}
		set
		{
			if (settings_LockCursor != value)
			{
				settingsDirty = true;
				settings_LockCursor = value;
				if (value)
				{
					Cursor.lockState = CursorLockMode.Confined;
				}
				else
				{
					Cursor.lockState = CursorLockMode.None;
				}
			}
		}
	}

	public static bool Settings_Vsync
	{
		get
		{
			return settings_Vsync;
		}
		set
		{
			if (settings_Vsync != value)
			{
				settingsDirty = true;
				settings_Vsync = value;
			}
		}
	}

	public static bool Settings_EnglishSpeech
	{
		get
		{
			return settings_EnglishSpeech;
		}
		set
		{
			if (settings_EnglishSpeech != value)
			{
				settingsDirty = true;
				settings_EnglishSpeech = value;
			}
		}
	}

	public static bool Settings_SkipIntro => settings_SkipIntro;

	public static int Settings_Scribe
	{
		get
		{
			return settings_Scribe;
		}
		set
		{
			if (settings_Scribe != value)
			{
				settingsDirty = true;
				settings_Scribe = value;
			}
		}
	}

	public static bool Settings_HideSoTTiming
	{
		get
		{
			return settings_HideSoTTiming;
		}
		set
		{
			if (settings_HideSoTTiming != value)
			{
				settingsDirty = true;
				settings_HideSoTTiming = value;
			}
		}
	}

	public static bool Settings_Allow_Classic_Bedouin_Stockade
	{
		get
		{
			return settings_Allow_Classic_Bedouin_Stockade;
		}
		set
		{
			if (settings_Allow_Classic_Bedouin_Stockade != value)
			{
				settingsDirty = true;
				settings_Allow_Classic_Bedouin_Stockade = value;
			}
		}
	}

	public static bool Settings_ShowCustomisationWarning
	{
		get
		{
			return settings_ShowCustomisationWarning;
		}
		set
		{
			if (settings_ShowCustomisationWarning != value)
			{
				settingsDirty = true;
				settings_ShowCustomisationWarning = value;
			}
		}
	}

	public static int Settings_LordType
	{
		get
		{
			return settings_LordType;
		}
		set
		{
			if (settings_LordType != value)
			{
				settingsDirty = true;
				settings_LordType = value;
			}
		}
	}

	public static int Settings_AvatarOrdinary
	{
		get
		{
			return settings_AvatarOrdinary;
		}
		set
		{
			if (settings_AvatarOrdinary != value)
			{
				settingsDirty = true;
				settings_AvatarOrdinary = value;
			}
		}
	}

	public static int Settings_AvatarOrdinaryColour1
	{
		get
		{
			return settings_AvatarOrdinaryColour1;
		}
		set
		{
			if (settings_AvatarOrdinaryColour1 != value)
			{
				settingsDirty = true;
				settings_AvatarOrdinaryColour1 = value;
			}
		}
	}

	public static int Settings_AvatarOrdinaryColour2
	{
		get
		{
			return settings_AvatarOrdinaryColour2;
		}
		set
		{
			if (settings_AvatarOrdinaryColour2 != value)
			{
				settingsDirty = true;
				settings_AvatarOrdinaryColour2 = value;
			}
		}
	}

	public static int Settings_AvatarCharge
	{
		get
		{
			return settings_AvatarCharge;
		}
		set
		{
			if (settings_AvatarCharge != value)
			{
				settingsDirty = true;
				settings_AvatarCharge = value;
			}
		}
	}

	public static int Settings_AvatarChargeColour1
	{
		get
		{
			return settings_AvatarChargeColour1;
		}
		set
		{
			if (settings_AvatarChargeColour1 != value)
			{
				settingsDirty = true;
				settings_AvatarChargeColour1 = value;
			}
		}
	}

	public static int Settings_AvatarChargeColour2
	{
		get
		{
			return settings_AvatarChargeColour2;
		}
		set
		{
			if (settings_AvatarChargeColour2 != value)
			{
				settingsDirty = true;
				settings_AvatarChargeColour2 = value;
			}
		}
	}

	public static bool Settings_UseSteamAvatar
	{
		get
		{
			return settings_UseSteamAvatar;
		}
		set
		{
			if (settings_UseSteamAvatar != value)
			{
				settingsDirty = true;
				settings_UseSteamAvatar = value;
			}
		}
	}

	public static bool Settings_MuteMPChat
	{
		get
		{
			return settings_MuteMPChat;
		}
		set
		{
			if (settings_MuteMPChat != value)
			{
				settingsDirty = true;
				settings_MuteMPChat = value;
			}
		}
	}

	public static int Settings_CursorStyle
	{
		get
		{
			return settings_CursorStyle;
		}
		set
		{
			if (settings_CursorStyle != value)
			{
				settingsDirty = true;
				settings_CursorStyle = value;
			}
		}
	}

	public static int Settings_PlayerColour
	{
		get
		{
			return settings_PlayerColour;
		}
		set
		{
			if (settings_PlayerColour != value)
			{
				settingsDirty = true;
				settings_PlayerColour = value;
			}
		}
	}

	public static int Settings_LastWindowWidth
	{
		get
		{
			return settings_LastWindowWidth;
		}
		set
		{
			if (settings_LastWindowWidth != value)
			{
				settingsDirty = true;
				settings_LastWindowWidth = value;
			}
		}
	}

	public static int Settings_LastWindowHeight
	{
		get
		{
			return settings_LastWindowHeight;
		}
		set
		{
			if (settings_LastWindowHeight != value)
			{
				settingsDirty = true;
				settings_LastWindowHeight = value;
			}
		}
	}

	public static int Settings_LastFullscreenWidth
	{
		get
		{
			return settings_LastFullscreenWidth;
		}
		set
		{
			if (settings_LastFullscreenWidth != value)
			{
				settingsDirty = true;
				settings_LastFullscreenWidth = value;
			}
		}
	}

	public static int Settings_LastFullscreenHeight
	{
		get
		{
			return settings_LastFullscreenHeight;
		}
		set
		{
			if (settings_LastFullscreenHeight != value)
			{
				settingsDirty = true;
				settings_LastFullscreenHeight = value;
			}
		}
	}

	public static int Settings_LastFullscreenRefresh
	{
		get
		{
			return settings_LastFullscreenRefresh;
		}
		set
		{
			if (settings_LastFullscreenRefresh != value)
			{
				settingsDirty = true;
				settings_LastFullscreenRefresh = value;
			}
		}
	}

	public static int Settings_LastFullscreenType
	{
		get
		{
			return settings_LastFullscreenType;
		}
		set
		{
			if (settings_LastFullscreenType != value)
			{
				settingsDirty = true;
				settings_LastFullscreenType = value;
			}
		}
	}

	public static float Settings_UIScale
	{
		get
		{
			return settings_UIScale;
		}
		set
		{
			if (settings_UIScale != value)
			{
				settingsDirty = true;
				settings_UIScale = value;
			}
		}
	}

	public static string Settings_MPPresets1
	{
		get
		{
			return settings_MPPresets1;
		}
		set
		{
			if (settings_MPPresets1 != value)
			{
				settingsDirty = true;
				settings_MPPresets1 = value;
			}
		}
	}

	public static string Settings_MPPresets2
	{
		get
		{
			return settings_MPPresets2;
		}
		set
		{
			if (settings_MPPresets2 != value)
			{
				settingsDirty = true;
				settings_MPPresets2 = value;
			}
		}
	}

	public static string Settings_SkirmishPresets
	{
		get
		{
			return settings_SkirmishPresets;
		}
		set
		{
			if (settings_SkirmishPresets != value)
			{
				settingsDirty = true;
				settings_SkirmishPresets = value;
			}
		}
	}

	public static int Settings_Progress_Historical1Campaign
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 5;
			}
			return settings_Progress_Historical1Campaign;
		}
		set
		{
			if (settings_Progress_Historical1Campaign != value)
			{
				settingsDirty = true;
				settings_Progress_Historical1Campaign = value;
			}
		}
	}

	public static int Settings_Progress_Historical2Campaign
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 5;
			}
			return settings_Progress_Historical2Campaign;
		}
		set
		{
			if (settings_Progress_Historical2Campaign != value)
			{
				settingsDirty = true;
				settings_Progress_Historical2Campaign = value;
			}
		}
	}

	public static int Settings_Progress_Historical3Campaign
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 5;
			}
			return settings_Progress_Historical3Campaign;
		}
		set
		{
			if (settings_Progress_Historical3Campaign != value)
			{
				settingsDirty = true;
				settings_Progress_Historical3Campaign = value;
			}
		}
	}

	public static int Settings_Progress_Historical4Campaign
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 5;
			}
			return settings_Progress_Historical4Campaign;
		}
		set
		{
			if (settings_Progress_Historical4Campaign != value)
			{
				settingsDirty = true;
				settings_Progress_Historical4Campaign = value;
			}
		}
	}

	public static int Settings_Progress_Historical5Campaign
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 5;
			}
			return settings_Progress_Historical5Campaign;
		}
		set
		{
			if (settings_Progress_Historical5Campaign != value)
			{
				settingsDirty = true;
				settings_Progress_Historical5Campaign = value;
			}
		}
	}

	public static int Settings_Progress_Historical6Campaign
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 5;
			}
			return settings_Progress_Historical6Campaign;
		}
		set
		{
			if (settings_Progress_Historical6Campaign != value)
			{
				settingsDirty = true;
				settings_Progress_Historical6Campaign = value;
			}
		}
	}

	public static int Settings_Progress_Historical7Campaign
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 5;
			}
			return settings_Progress_Historical7Campaign;
		}
		set
		{
			if (settings_Progress_Historical7Campaign != value)
			{
				settingsDirty = true;
				settings_Progress_Historical7Campaign = value;
			}
		}
	}

	public static int Settings_Progress_Trail
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 51;
			}
			return settings_Progress_Trail;
		}
		set
		{
			if (settings_Progress_Trail != value)
			{
				settingsDirty = true;
				settings_Progress_Trail = value;
			}
		}
	}

	public static int Settings_Progress_Trail2
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 31;
			}
			return settings_Progress_Trail2;
		}
		set
		{
			if (settings_Progress_Trail2 != value)
			{
				settingsDirty = true;
				settings_Progress_Trail2 = value;
			}
		}
	}

	public static int Settings_Progress_Trail3
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 21;
			}
			return settings_Progress_Trail3;
		}
		set
		{
			if (settings_Progress_Trail3 != value)
			{
				settingsDirty = true;
				settings_Progress_Trail3 = value;
			}
		}
	}

	public static int Settings_Progress_Trail_Sands1
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 6;
			}
			return settings_Progress_Trail_Sands1;
		}
		set
		{
			if (settings_Progress_Trail_Sands1 != value)
			{
				settingsDirty = true;
				settings_Progress_Trail_Sands1 = value;
			}
		}
	}

	public static int Settings_Progress_Trail_Sands2
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 8;
			}
			return settings_Progress_Trail_Sands2;
		}
		set
		{
			if (settings_Progress_Trail_Sands2 != value)
			{
				settingsDirty = true;
				settings_Progress_Trail_Sands2 = value;
			}
		}
	}

	public static int Settings_Progress_Trail_Sands3
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 10;
			}
			return settings_Progress_Trail_Sands3;
		}
		set
		{
			if (settings_Progress_Trail_Sands3 != value)
			{
				settingsDirty = true;
				settings_Progress_Trail_Sands3 = value;
			}
		}
	}

	public static int Settings_Progress_Trail_Sands4
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 12;
			}
			return settings_Progress_Trail_Sands4;
		}
		set
		{
			if (settings_Progress_Trail_Sands4 != value)
			{
				settingsDirty = true;
				settings_Progress_Trail_Sands4 = value;
			}
		}
	}

	public static int Settings_Progress_Trail_Sands5
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 10;
			}
			return settings_Progress_Trail_Sands5;
		}
		set
		{
			if (settings_Progress_Trail_Sands5 != value)
			{
				settingsDirty = true;
				settings_Progress_Trail_Sands5 = value;
			}
		}
	}

	public static int Settings_Progress_Trail_Sands6
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 10;
			}
			return settings_Progress_Trail_Sands6;
		}
		set
		{
			if (settings_Progress_Trail_Sands6 != value)
			{
				settingsDirty = true;
				settings_Progress_Trail_Sands6 = value;
			}
		}
	}

	public static int Settings_Progress_Trail_Sands7
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 10;
			}
			return settings_Progress_Trail_Sands7;
		}
		set
		{
			if (settings_Progress_Trail_Sands7 != value)
			{
				settingsDirty = true;
				settings_Progress_Trail_Sands7 = value;
			}
		}
	}

	public static int Settings_Progress_Trail_Sands8
	{
		get
		{
			if (TempMissionUnlock)
			{
				return 10;
			}
			return settings_Progress_Trail_Sands8;
		}
		set
		{
			if (settings_Progress_Trail_Sands8 != value)
			{
				settingsDirty = true;
				settings_Progress_Trail_Sands8 = value;
			}
		}
	}

	public static bool Settings_ShowExtremeHelp
	{
		get
		{
			return settings_ShowExtremeHelp;
		}
		set
		{
			if (settings_ShowExtremeHelp != value)
			{
				settingsDirty = true;
				settings_ShowExtremeHelp = value;
			}
		}
	}

	public static bool Settings_DLC4_Pip1
	{
		get
		{
			return settings_DLC4_Pip1;
		}
		set
		{
			if (settings_DLC4_Pip1 != value)
			{
				settingsDirty = true;
				settings_DLC4_Pip1 = value;
			}
		}
	}

	public static bool Settings_DLC4_Pip2
	{
		get
		{
			return settings_DLC4_Pip2;
		}
		set
		{
			if (settings_DLC4_Pip2 != value)
			{
				settingsDirty = true;
				settings_DLC4_Pip2 = value;
			}
		}
	}

	public static bool Settings_DLC4_Pip3
	{
		get
		{
			return settings_DLC4_Pip3;
		}
		set
		{
			if (settings_DLC4_Pip3 != value)
			{
				settingsDirty = true;
				settings_DLC4_Pip3 = value;
			}
		}
	}

	public static bool Settings_DLC4_Pip4
	{
		get
		{
			return settings_DLC4_Pip4;
		}
		set
		{
			if (settings_DLC4_Pip4 != value)
			{
				settingsDirty = true;
				settings_DLC4_Pip4 = value;
			}
		}
	}

	public static bool Settings_DLC4_Pip5
	{
		get
		{
			return settings_DLC4_Pip5;
		}
		set
		{
			if (settings_DLC4_Pip5 != value)
			{
				settingsDirty = true;
				settings_DLC4_Pip5 = value;
			}
		}
	}

	public static bool Settings_DLC4_Pip6
	{
		get
		{
			return settings_DLC4_Pip6;
		}
		set
		{
			if (settings_DLC4_Pip6 != value)
			{
				settingsDirty = true;
				settings_DLC4_Pip6 = value;
			}
		}
	}

	public static int Settings_Trail1Difficulty
	{
		get
		{
			return settings_Trail1Difficulty;
		}
		set
		{
			if (settings_Trail1Difficulty != value)
			{
				settingsDirty = true;
				settings_Trail1Difficulty = value;
			}
		}
	}

	public static int Settings_Trail2Difficulty
	{
		get
		{
			return settings_Trail2Difficulty;
		}
		set
		{
			if (settings_Trail2Difficulty != value)
			{
				settingsDirty = true;
				settings_Trail2Difficulty = value;
			}
		}
	}

	public static int Settings_Trail3Difficulty
	{
		get
		{
			return settings_Trail3Difficulty;
		}
		set
		{
			if (settings_Trail3Difficulty != value)
			{
				settingsDirty = true;
				settings_Trail3Difficulty = value;
			}
		}
	}

	public static int Settings_SandsTrail1Difficulty
	{
		get
		{
			return settings_SandsTrail1Difficulty;
		}
		set
		{
			if (settings_SandsTrail1Difficulty != value)
			{
				settingsDirty = true;
				settings_SandsTrail1Difficulty = value;
			}
		}
	}

	public static int Settings_SandsTrail2Difficulty
	{
		get
		{
			return settings_SandsTrail2Difficulty;
		}
		set
		{
			if (settings_SandsTrail2Difficulty != value)
			{
				settingsDirty = true;
				settings_SandsTrail2Difficulty = value;
			}
		}
	}

	public static int Settings_SandsTrail3Difficulty
	{
		get
		{
			return settings_SandsTrail3Difficulty;
		}
		set
		{
			if (settings_SandsTrail3Difficulty != value)
			{
				settingsDirty = true;
				settings_SandsTrail3Difficulty = value;
			}
		}
	}

	public static int Settings_SandsTrail4Difficulty
	{
		get
		{
			return settings_SandsTrail4Difficulty;
		}
		set
		{
			if (settings_SandsTrail4Difficulty != value)
			{
				settingsDirty = true;
				settings_SandsTrail4Difficulty = value;
			}
		}
	}

	public static int Settings_SandsTrail5Difficulty
	{
		get
		{
			return settings_SandsTrail5Difficulty;
		}
		set
		{
			if (settings_SandsTrail5Difficulty != value)
			{
				settingsDirty = true;
				settings_SandsTrail5Difficulty = value;
			}
		}
	}

	public static int Settings_SandsTrail6Difficulty
	{
		get
		{
			return settings_SandsTrail6Difficulty;
		}
		set
		{
			if (settings_SandsTrail6Difficulty != value)
			{
				settingsDirty = true;
				settings_SandsTrail6Difficulty = value;
			}
		}
	}

	public static int Settings_SandsTrail7Difficulty
	{
		get
		{
			return settings_SandsTrail7Difficulty;
		}
		set
		{
			if (settings_SandsTrail7Difficulty != value)
			{
				settingsDirty = true;
				settings_SandsTrail7Difficulty = value;
			}
		}
	}

	public static int Settings_SandsTrail8Difficulty
	{
		get
		{
			return settings_SandsTrail8Difficulty;
		}
		set
		{
			if (settings_SandsTrail8Difficulty != value)
			{
				settingsDirty = true;
				settings_SandsTrail8Difficulty = value;
			}
		}
	}

	public static void SetDirty()
	{
		settingsDirty = true;
	}

	public static float GetScrollSpeed()
	{
		return (float)Settings_ScrollSpeed * 0.1f + 0.5f;
	}

	public static Avatars.AvatarDesign getAvatar()
	{
		return new Avatars.AvatarDesign
		{
			background = (Enums.AvatarItems)Settings_AvatarOrdinary,
			background_colour1 = (Enums.AvatarItems)Settings_AvatarOrdinaryColour1,
			background_colour2 = (Enums.AvatarItems)Settings_AvatarOrdinaryColour2,
			item = (Enums.AvatarItems)Settings_AvatarCharge,
			item_colour1 = (Enums.AvatarItems)Settings_AvatarChargeColour1,
			item_colour2 = (Enums.AvatarItems)Settings_AvatarChargeColour2
		};
	}

	public static int countTrailMissionsCompleted(int trailType)
	{
		int num = 0;
		switch (trailType)
		{
		case 0:
		{
			for (int i = 0; i < 50; i++)
			{
				if (settings_Trail1Times[i] >= 0)
				{
					num++;
				}
			}
			break;
		}
		case 1:
		{
			for (int i = 0; i < 30; i++)
			{
				if (settings_Trail2Times[i] >= 0)
				{
					num++;
				}
			}
			break;
		}
		case 2:
		{
			for (int i = 0; i < 20; i++)
			{
				if (settings_Trail3Times[i] >= 0)
				{
					num++;
				}
			}
			break;
		}
		case 11:
		{
			for (int i = 0; i < 5; i++)
			{
				if (settings_Trail_Sands1_Times[i] >= 0)
				{
					num++;
				}
			}
			break;
		}
		case 12:
		{
			for (int i = 0; i < 7; i++)
			{
				if (settings_Trail_Sands2_Times[i] >= 0)
				{
					num++;
				}
			}
			break;
		}
		case 13:
		{
			for (int i = 0; i < 9; i++)
			{
				if (settings_Trail_Sands3_Times[i] >= 0)
				{
					num++;
				}
			}
			break;
		}
		case 14:
		{
			for (int i = 0; i < 11; i++)
			{
				if (settings_Trail_Sands4_Times[i] >= 0)
				{
					num++;
				}
			}
			break;
		}
		case 15:
		{
			for (int i = 0; i < 9; i++)
			{
				if (settings_Trail_Sands5_Times[i] >= 0)
				{
					num++;
				}
			}
			break;
		}
		case 16:
		{
			for (int i = 0; i < 9; i++)
			{
				if (settings_Trail_Sands6_Times[i] >= 0)
				{
					num++;
				}
			}
			break;
		}
		case 17:
		{
			for (int i = 0; i < 9; i++)
			{
				if (settings_Trail_Sands7_Times[i] >= 0)
				{
					num++;
				}
			}
			break;
		}
		case 18:
		{
			for (int i = 0; i < 9; i++)
			{
				if (settings_Trail_Sands8_Times[i] >= 0)
				{
					num++;
				}
			}
			break;
		}
		}
		return num;
	}

	public static int getTrailStartDate(int trailType, int trailID, int sandsNoTimeValue = -1200)
	{
		int num = 13200;
		int[] array = new int[51];
		switch (trailType)
		{
		case 0:
		{
			int i;
			for (i = 0; i < 50; i++)
			{
				if (settings_Trail1Times[i] == -1200)
				{
					array[i] = num;
					num += 1200;
				}
				else if (settings_Trail1Times[i] < 0)
				{
					if (!TempMissionUnlock)
					{
						break;
					}
					array[i] = num;
				}
				else
				{
					array[i] = num;
					num += settings_Trail1Times[i];
				}
			}
			array[i] = num;
			while (trailID >= 0)
			{
				if (array[trailID] > 0)
				{
					return array[trailID];
				}
				trailID--;
			}
			return num;
		}
		case 1:
		{
			num += 360;
			int i;
			for (i = 0; i < 30; i++)
			{
				if (settings_Trail2Times[i] == -1200)
				{
					array[i] = num;
					num += 1200;
				}
				else if (settings_Trail2Times[i] < 0)
				{
					if (!TempMissionUnlock)
					{
						break;
					}
					array[i] = num;
				}
				else
				{
					array[i] = num;
					num += settings_Trail2Times[i];
				}
			}
			array[i] = num;
			while (trailID >= 0)
			{
				if (array[trailID] > 0)
				{
					return array[trailID];
				}
				trailID--;
			}
			return num;
		}
		case 2:
		{
			num += 360;
			int i;
			for (i = 0; i < 20; i++)
			{
				if (settings_Trail3Times[i] == -1200)
				{
					array[i] = num;
					num += 1200;
				}
				else if (settings_Trail3Times[i] < 0)
				{
					if (!TempMissionUnlock)
					{
						break;
					}
					array[i] = num;
				}
				else
				{
					array[i] = num;
					num += settings_Trail3Times[i];
				}
			}
			array[i] = num;
			while (trailID >= 0)
			{
				if (array[trailID] > 0)
				{
					return array[trailID];
				}
				trailID--;
			}
			return num;
		}
		case 11:
		{
			num = 0;
			if (trailID >= 0)
			{
				if (settings_Trail_Sands1_Times[trailID] < int.MaxValue)
				{
					return settings_Trail_Sands1_Times[trailID];
				}
				return sandsNoTimeValue;
			}
			for (int i = 0; i < 5; i++)
			{
				if (settings_Trail_Sands1_Times[i] >= 0 && settings_Trail_Sands1_Times[i] < int.MaxValue)
				{
					num += settings_Trail_Sands1_Times[i];
				}
			}
			return num;
		}
		case 12:
		{
			num = 0;
			if (trailID >= 0)
			{
				if (settings_Trail_Sands2_Times[trailID] < int.MaxValue)
				{
					return settings_Trail_Sands2_Times[trailID];
				}
				return sandsNoTimeValue;
			}
			for (int i = 0; i < 7; i++)
			{
				if (settings_Trail_Sands2_Times[i] >= 0 && settings_Trail_Sands2_Times[i] < int.MaxValue)
				{
					num += settings_Trail_Sands2_Times[i];
				}
			}
			return num;
		}
		case 13:
		{
			num = 0;
			if (trailID >= 0)
			{
				if (settings_Trail_Sands3_Times[trailID] < int.MaxValue)
				{
					return settings_Trail_Sands3_Times[trailID];
				}
				return sandsNoTimeValue;
			}
			for (int i = 0; i < 9; i++)
			{
				if (settings_Trail_Sands3_Times[i] >= 0 && settings_Trail_Sands3_Times[i] < int.MaxValue)
				{
					num += settings_Trail_Sands3_Times[i];
				}
			}
			return num;
		}
		case 14:
		{
			num = 0;
			if (trailID >= 0)
			{
				if (settings_Trail_Sands4_Times[trailID] < int.MaxValue)
				{
					return settings_Trail_Sands4_Times[trailID];
				}
				return sandsNoTimeValue;
			}
			for (int i = 0; i < 11; i++)
			{
				if (settings_Trail_Sands4_Times[i] >= 0 && settings_Trail_Sands4_Times[i] < int.MaxValue)
				{
					num += settings_Trail_Sands4_Times[i];
				}
			}
			return num;
		}
		case 15:
		{
			num = 0;
			if (trailID >= 0)
			{
				if (settings_Trail_Sands5_Times[trailID] < int.MaxValue)
				{
					return settings_Trail_Sands5_Times[trailID];
				}
				return sandsNoTimeValue;
			}
			for (int i = 0; i < 9; i++)
			{
				if (settings_Trail_Sands5_Times[i] >= 0 && settings_Trail_Sands5_Times[i] < int.MaxValue)
				{
					num += settings_Trail_Sands5_Times[i];
				}
			}
			return num;
		}
		case 16:
		{
			num = 0;
			if (trailID >= 0)
			{
				if (settings_Trail_Sands6_Times[trailID] < int.MaxValue)
				{
					return settings_Trail_Sands6_Times[trailID];
				}
				return sandsNoTimeValue;
			}
			for (int i = 0; i < 9; i++)
			{
				if (settings_Trail_Sands6_Times[i] >= 0 && settings_Trail_Sands6_Times[i] < int.MaxValue)
				{
					num += settings_Trail_Sands6_Times[i];
				}
			}
			return num;
		}
		case 17:
		{
			num = 0;
			if (trailID >= 0)
			{
				if (settings_Trail_Sands7_Times[trailID] < int.MaxValue)
				{
					return settings_Trail_Sands7_Times[trailID];
				}
				return sandsNoTimeValue;
			}
			for (int i = 0; i < 9; i++)
			{
				if (settings_Trail_Sands7_Times[i] >= 0 && settings_Trail_Sands7_Times[i] < int.MaxValue)
				{
					num += settings_Trail_Sands7_Times[i];
				}
			}
			return num;
		}
		case 18:
		{
			num = 0;
			if (trailID >= 0)
			{
				if (settings_Trail_Sands8_Times[trailID] < int.MaxValue)
				{
					return settings_Trail_Sands8_Times[trailID];
				}
				return sandsNoTimeValue;
			}
			for (int i = 0; i < 9; i++)
			{
				if (settings_Trail_Sands8_Times[i] >= 0 && settings_Trail_Sands8_Times[i] < int.MaxValue)
				{
					num += settings_Trail_Sands8_Times[i];
				}
			}
			return num;
		}
		default:
			return num;
		}
	}

	public static void toggleMuteSounds()
	{
		muteSounds = !muteSounds;
		MyAudioManager.Instance.updateSFXVolumeFromSettings();
		MyAudioManager.Instance.updateSpeechVolumeFromSettings();
		MyAudioManager.Instance.updateMusicVolumeFromSettings();
	}

	public static string GetSettingsFileName(bool createPaths = false)
	{
		string persistentDataPath = Application.persistentDataPath;
		if (createPaths)
		{
			if (!Directory.Exists(persistentDataPath))
			{
				Directory.CreateDirectory(persistentDataPath);
			}
			if (!Directory.Exists(persistentDataPath + "\\Saves"))
			{
				Directory.CreateDirectory(persistentDataPath + "\\Saves");
			}
			if (!Directory.Exists(persistentDataPath + "\\Maps"))
			{
				Directory.CreateDirectory(persistentDataPath + "\\Maps");
			}
			if (!Directory.Exists(persistentDataPath + "\\CustomLords"))
			{
				Directory.CreateDirectory(persistentDataPath + "\\CustomLords");
			}
			if (!Directory.Exists(persistentDataPath + "\\ExtendedLords"))
			{
				Directory.CreateDirectory(persistentDataPath + "\\ExtendedLords");
			}
			string[] array = extendedLordPaths;
			foreach (string path in array)
			{
				string path2 = Path.Combine(persistentDataPath, "ExtendedLords", path);
				if (!Directory.Exists(path2))
				{
					Directory.CreateDirectory(path2);
				}
			}
			if (!Directory.Exists(persistentDataPath + "\\CustomTrails"))
			{
				Directory.CreateDirectory(persistentDataPath + "\\CustomTrails");
			}
			if (!Directory.Exists(persistentDataPath + "\\TrailMaker"))
			{
				Directory.CreateDirectory(persistentDataPath + "\\TrailMaker");
			}
			if (!Directory.Exists(persistentDataPath + "\\CustomMedia"))
			{
				Directory.CreateDirectory(persistentDataPath + "\\CustomMedia");
			}
			try
			{
				string destFileName = Path.Combine(persistentDataPath, "CustomMedia", "ReadMe.txt");
				File.Copy(Path.Combine(Application.streamingAssetsPath, "CustomMediaExample", "ReadMe.txt"), destFileName, overwrite: true);
			}
			catch (Exception)
			{
			}
		}
		return persistentDataPath + "/settings.cfg";
	}

	public static string GetSavesPath()
	{
		return Application.persistentDataPath + "\\Saves";
	}

	public static string GetUserMapsPath()
	{
		return Application.persistentDataPath + "\\Maps";
	}

	public static string GetMpAutoSavePath()
	{
		return Application.persistentDataPath + "\\Saves\\";
	}

	public static string GetUserCustomLordsPath()
	{
		return Application.persistentDataPath + "\\CustomLords";
	}

	public static string GetUserExtendedLordsPath()
	{
		return Application.persistentDataPath + "\\ExtendedLords";
	}

	public static string GetUserCustomTrailsPath()
	{
		return Application.persistentDataPath + "\\CustomTrails";
	}

	public static string GetUserTrailMakerPath()
	{
		return Application.persistentDataPath + "\\TrailMaker";
	}

	public static string GetUserCustomMediaPath()
	{
		return Application.persistentDataPath + "\\CustomMedia";
	}

	public static string GetUserTrailMakerBackupPath()
	{
		return Application.persistentDataPath + "\\TrailMaker\\Backups";
	}

	public static void LoadSettings()
	{
		for (int i = 0; i < 50; i++)
		{
			Settings_Trail1Times[i] = -1;
		}
		for (int j = 0; j < 30; j++)
		{
			Settings_Trail2Times[j] = -1;
		}
		for (int k = 0; k < 20; k++)
		{
			Settings_Trail3Times[k] = -1;
		}
		for (int l = 0; l < 5; l++)
		{
			Settings_Trail_Sands1_Times[l] = -1;
		}
		for (int m = 0; m < 7; m++)
		{
			Settings_Trail_Sands2_Times[m] = -1;
		}
		for (int n = 0; n < 9; n++)
		{
			Settings_Trail_Sands3_Times[n] = -1;
		}
		for (int num = 0; num < 11; num++)
		{
			Settings_Trail_Sands4_Times[num] = -1;
		}
		for (int num2 = 0; num2 < 9; num2++)
		{
			Settings_Trail_Sands5_Times[num2] = -1;
		}
		for (int num3 = 0; num3 < 9; num3++)
		{
			Settings_Trail_Sands6_Times[num3] = -1;
		}
		for (int num4 = 0; num4 < 9; num4++)
		{
			Settings_Trail_Sands7_Times[num4] = -1;
		}
		for (int num5 = 0; num5 < 9; num5++)
		{
			Settings_Trail_Sands8_Times[num5] = -1;
		}
		string settingsFileName = GetSettingsFileName(createPaths: true);
		try
		{
			string settingsString = File.ReadAllText(settingsFileName);
			loadSettingsFromString(settingsString);
			KeyManager.instance.LoadFromString(settingsString);
			_settingsFileExisted = true;
		}
		catch (Exception)
		{
		}
		try
		{
			LoadScores();
		}
		catch (Exception)
		{
		}
		try
		{
			LoadSkirmishMasters();
		}
		catch (Exception)
		{
		}
		try
		{
			LoadCoop();
		}
		catch (Exception)
		{
		}
		try
		{
			LoadCustomTrailInfo();
		}
		catch (Exception)
		{
		}
	}

	public static void SaveSettings(bool onlyWhenAlreadyExists = false)
	{
		string settingsFileName = GetSettingsFileName();
		bool flag = File.Exists(settingsFileName);
		if (!settingsDirty && flag)
		{
			return;
		}
		settingsDirty = false;
		if (onlyWhenAlreadyExists && !flag)
		{
			return;
		}
		string text = "";
		text += createSettingString();
		text += KeyManager.instance.SaveToString();
		try
		{
			File.WriteAllText(settingsFileName, text);
		}
		catch (Exception)
		{
		}
	}

	private static void loadSettingsFromString(string settingsString)
	{
		Settings_ArabicL2R = false;
		Settings_UseSteamAvatar = true;
		string[] array = settingsString.Split("||SETTINGS||\n");
		if (array.Length == 3)
		{
			settings_ExtraZoom = false;
			string[] array2 = array[1].Split("\n");
			foreach (string text in array2)
			{
				try
				{
					string[] array3 = text.Split(":");
					if (array3.Length <= 1)
					{
						continue;
					}
					switch (array3[0].ToLowerInvariant())
					{
					case "name":
						settings_UserName = array3[1];
						if (settings_UserName.Length > 39)
						{
							settings_UserName = settings_UserName.Substring(0, 39);
						}
						break;
					case "pushmapscrolling":
						settings_PushMapScrolling = bool.Parse(array3[1]);
						break;
					case "scrollspeed":
						settings_ScrollSpeed = Math.Clamp(int.Parse(array3[1], Director.defaultCulture), 0, 15);
						break;
					case "trail1times":
					{
						for (int num5 = 0; num5 < 50; num5++)
						{
							settings_Trail1Times[num5] = int.Parse(array3[num5 + 1], Director.defaultCulture);
						}
						break;
					}
					case "trail2times":
					{
						for (int num2 = 0; num2 < 30; num2++)
						{
							settings_Trail2Times[num2] = int.Parse(array3[num2 + 1], Director.defaultCulture);
						}
						break;
					}
					case "trail3times":
					{
						for (int m = 0; m < 20; m++)
						{
							settings_Trail3Times[m] = int.Parse(array3[m + 1], Director.defaultCulture);
						}
						break;
					}
					case "trails1times":
					{
						for (int j = 0; j < 5; j++)
						{
							settings_Trail_Sands1_Times[j] = int.Parse(array3[j + 1], Director.defaultCulture);
						}
						break;
					}
					case "trails2times":
					{
						for (int num6 = 0; num6 < 7; num6++)
						{
							settings_Trail_Sands2_Times[num6] = int.Parse(array3[num6 + 1], Director.defaultCulture);
						}
						break;
					}
					case "trails3times":
					{
						for (int num4 = 0; num4 < 9; num4++)
						{
							settings_Trail_Sands3_Times[num4] = int.Parse(array3[num4 + 1], Director.defaultCulture);
						}
						break;
					}
					case "trails4times":
					{
						for (int num3 = 0; num3 < 11; num3++)
						{
							settings_Trail_Sands4_Times[num3] = int.Parse(array3[num3 + 1], Director.defaultCulture);
						}
						break;
					}
					case "trails5times":
					{
						for (int num = 0; num < 9; num++)
						{
							settings_Trail_Sands5_Times[num] = int.Parse(array3[num + 1], Director.defaultCulture);
						}
						break;
					}
					case "trails6times":
					{
						for (int n = 0; n < 9; n++)
						{
							settings_Trail_Sands6_Times[n] = int.Parse(array3[n + 1], Director.defaultCulture);
						}
						break;
					}
					case "trails7times":
					{
						for (int l = 0; l < 9; l++)
						{
							settings_Trail_Sands7_Times[l] = int.Parse(array3[l + 1], Director.defaultCulture);
						}
						break;
					}
					case "trails8times":
					{
						for (int k = 0; k < 9; k++)
						{
							settings_Trail_Sands8_Times[k] = int.Parse(array3[k + 1], Director.defaultCulture);
						}
						break;
					}
					case "gamespeed":
						settings_GameSpeed = Math.Clamp(int.Parse(array3[1], Director.defaultCulture) / 5 * 5, 10, 90);
						break;
					case "sh1mousewheel":
						settings_SH1MouseWheel = bool.Parse(array3[1]);
						break;
					case "sh1rtscontrols":
						settings_SH1RTSControls = bool.Parse(array3[1]);
						break;
					case "sh1centrecontrols":
						settings_SH1CentreControls = bool.Parse(array3[1]);
						break;
					case "troopmovemode":
						settings_TroopMoveMode = bool.Parse(array3[1]);
						break;
					case "showbuildingtooltips":
						settings_ShowBuildingTooltips = bool.Parse(array3[1]);
						break;
					case "playuisfx":
						settings_PlayUISFX = bool.Parse(array3[1]);
						break;
					case "geniespeech":
						settings_GenieSpeech = bool.Parse(array3[1]);
						break;
					case "muteinsults":
						settings_MuteInsults = bool.Parse(array3[1]);
						break;
					case "muteinsultspeech":
						settings_MuteInsultSpeech = bool.Parse(array3[1]);
						break;
					case "backgroundaudio":
						settings_BackgroundAudio = bool.Parse(array3[1]);
						break;
					case "reducemusicvolumeforspeech":
						settings_ReduceMusicVolumeForSpeech = bool.Parse(array3[1]);
						break;
					case "usesteamoverlayforhelp":
						settings_UseSteamOverlayForHelp = bool.Parse(array3[1]);
						break;
					case "cheatkeysenabled":
						settings_CheatKeysEnabled = bool.Parse(array3[1]);
						break;
					case "coopcheatsenabled":
						settings_CoopCheatsEnabled = bool.Parse(array3[1]);
						break;
					case "showpings":
						settings_ShowPings = bool.Parse(array3[1]);
						break;
					case "extrazoom":
						settings_ExtraZoom = bool.Parse(array3[1]);
						break;
					case "leaderboard_optout":
						settings_Leaderboard_OptOut = bool.Parse(array3[1]);
						break;
					case "leaderboard_names":
						settings_Leaderboard_Names = bool.Parse(array3[1]);
						break;
					case "leaderboard_images":
						settings_Leaderboard_Images = bool.Parse(array3[1]);
						break;
					case "show_extreme_warning":
						settings_Show_Extreme_Warning = bool.Parse(array3[1]);
						break;
					case "confirm_disband_troops":
						settings_Confirm_Disband_Troops = bool.Parse(array3[1]);
						break;
					case "arabicl2r":
						settings_ArabicL2R = bool.Parse(array3[1]);
						break;
					case "compass":
						settings_Compass = bool.Parse(array3[1]);
						break;
					case "localtime":
						settings_ShowLocalTime = bool.Parse(array3[1]);
						break;
					case "gametime":
						settings_ShowGameTime = bool.Parse(array3[1]);
						break;
					case "sandstimer":
						settings_ShowSandsTimer = bool.Parse(array3[1]);
						break;
					case "sandsintro":
						settings_ShowSandsIntro = bool.Parse(array3[1]);
						break;
					case "radardefaultzoomedout":
						settings_RadarDefaultZoomedOut = bool.Parse(array3[1]);
						break;
					case "customintros":
						settings_CustomIntros = bool.Parse(array3[1]);
						break;
					case "moat":
						settings_ShowPlannedMoat = bool.Parse(array3[1]);
						break;
					case "mastervolume":
						settings_MasterVolume = Mathf.Clamp(float.Parse(array3[1], Director.defaultCulture), 0f, 1f);
						break;
					case "sfxvolume":
						settings_SFXVolume = Mathf.Clamp(float.Parse(array3[1], Director.defaultCulture), 0f, 1f);
						break;
					case "speechvolume":
						settings_SpeechVolume = Mathf.Clamp(float.Parse(array3[1], Director.defaultCulture), 0f, 1f);
						break;
					case "unitspeechvolume":
						settings_UnitSpeechVolume = Mathf.Clamp(float.Parse(array3[1], Director.defaultCulture), 0f, 1f);
						break;
					case "musicvolume":
						settings_MusicVolume = Mathf.Clamp(float.Parse(array3[1], Director.defaultCulture), 0f, 1f);
						break;
					case "englishspeech":
						settings_EnglishSpeech = bool.Parse(array3[1]);
						break;
					case "lockcursor":
						settings_LockCursor = bool.Parse(array3[1]);
						break;
					case "cursorstyle":
						settings_CursorStyle = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "playercolour":
						settings_PlayerColour = int.Parse(array3[1], Director.defaultCulture);
						if (settings_PlayerColour < 0 || settings_PlayerColour >= 8)
						{
							settings_PlayerColour = 0;
						}
						break;
					case "avatarordinary":
						settings_AvatarOrdinary = int.Parse(array3[1], Director.defaultCulture);
						if (settings_AvatarOrdinary < 1000 || settings_AvatarOrdinary > 1091)
						{
							settings_AvatarOrdinary = 1002;
						}
						break;
					case "avatarcharge":
						settings_AvatarCharge = int.Parse(array3[1], Director.defaultCulture);
						if (settings_AvatarCharge < 2000 || settings_AvatarCharge > 2093)
						{
							settings_AvatarCharge = 2019;
						}
						break;
					case "avatarordinarycolour1":
						settings_AvatarOrdinaryColour1 = int.Parse(array3[1], Director.defaultCulture);
						if (settings_AvatarOrdinaryColour1 < 1 || settings_AvatarOrdinaryColour1 > 25)
						{
							settings_AvatarOrdinaryColour1 = 22;
						}
						break;
					case "avatarordinarycolour2":
						settings_AvatarOrdinaryColour2 = int.Parse(array3[1], Director.defaultCulture);
						if (settings_AvatarOrdinaryColour2 < 1 || settings_AvatarOrdinaryColour2 > 25)
						{
							settings_AvatarOrdinaryColour2 = 11;
						}
						break;
					case "avatarchargecolour1":
						settings_AvatarChargeColour1 = int.Parse(array3[1], Director.defaultCulture);
						if (settings_AvatarChargeColour1 < 1 || settings_AvatarChargeColour1 > 25)
						{
							settings_AvatarChargeColour1 = 10;
						}
						break;
					case "avatarchargecolour2":
						settings_AvatarChargeColour2 = int.Parse(array3[1], Director.defaultCulture);
						if (settings_AvatarChargeColour2 < 101 || settings_AvatarChargeColour2 > 125)
						{
							settings_AvatarChargeColour2 = 110;
						}
						break;
					case "usesteamavatar":
						settings_UseSteamAvatar = bool.Parse(array3[1]);
						break;
					case "mutempchat":
						settings_MuteMPChat = bool.Parse(array3[1]);
						break;
					case "winwidth":
						settings_LastWindowWidth = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "winheight":
						settings_LastWindowHeight = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "fullwidth":
						settings_LastFullscreenWidth = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "fullheight":
						settings_LastFullscreenHeight = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "fullrefreshrate":
						settings_LastFullscreenRefresh = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "fullscreentype":
						settings_LastFullscreenType = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "vsync":
						settings_Vsync = bool.Parse(array3[1]);
						break;
					case "scribe":
						settings_Scribe = int.Parse(array3[1], Director.defaultCulture);
						if (settings_Scribe == 1)
						{
							settings_Scribe = 2;
						}
						break;
					case "skipintro":
						settings_SkipIntro = bool.Parse(array3[1]);
						break;
					case "lordtype":
						Settings_LordType = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "hidesottiming":
						settings_HideSoTTiming = bool.Parse(array3[1]);
						break;
					case "allow_classic_bedouin_stockade":
						settings_Allow_Classic_Bedouin_Stockade = bool.Parse(array3[1]);
						break;
					case "showcustomisationwarning":
						settings_ShowCustomisationWarning = bool.Parse(array3[1]);
						break;
					case "uiscale":
						settings_UIScale = Mathf.Clamp(float.Parse(array3[1], Director.defaultCulture), 0f, 1f);
						break;
					case "campaignextra1":
					case "campaign1":
						Settings_Progress_Historical1Campaign = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "campaignextra2":
					case "campaign2":
						Settings_Progress_Historical2Campaign = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "campaignextra3":
					case "campaign3":
						Settings_Progress_Historical3Campaign = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "campaignextra4":
					case "campaign4":
						Settings_Progress_Historical4Campaign = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "campaign5":
						Settings_Progress_Historical5Campaign = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "campaign6":
						Settings_Progress_Historical6Campaign = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "campaign7":
						Settings_Progress_Historical7Campaign = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "campaigntrail2":
						settings_Progress_Trail2 = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "campaigntrail3":
						settings_Progress_Trail3 = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "campaigntrail":
						settings_Progress_Trail = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sands1trail":
						settings_Progress_Trail_Sands1 = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sands2trail":
						settings_Progress_Trail_Sands2 = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sands3trail":
						settings_Progress_Trail_Sands3 = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sands4trail":
						settings_Progress_Trail_Sands4 = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "trail1difficulty":
						settings_Trail1Difficulty = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "trail2difficulty":
						settings_Trail2Difficulty = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "trail3difficulty":
						settings_Trail3Difficulty = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sandstrail2difficulty":
						settings_SandsTrail2Difficulty = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sandstrail1difficulty":
						settings_SandsTrail1Difficulty = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sandstrail3difficulty":
						settings_SandsTrail3Difficulty = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sandstrail4difficulty":
						settings_SandsTrail4Difficulty = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sands5trail":
						settings_Progress_Trail_Sands5 = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sandstrail5difficulty":
						settings_SandsTrail5Difficulty = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sands6trail":
						settings_Progress_Trail_Sands6 = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sandstrail6difficulty":
						settings_SandsTrail6Difficulty = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sands7trail":
						settings_Progress_Trail_Sands7 = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sandstrail7difficulty":
						settings_SandsTrail7Difficulty = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sands8trail":
						settings_Progress_Trail_Sands8 = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "sandstrail8difficulty":
						settings_SandsTrail8Difficulty = int.Parse(array3[1], Director.defaultCulture);
						break;
					case "showextremehelp":
						settings_ShowExtremeHelp = bool.Parse(array3[1]);
						break;
					case "dlc4pips":
					{
						string[] array7 = array3[1].Split(",");
						if (array7.Length == 6)
						{
							settings_DLC4_Pip1 = bool.Parse(array7[0]);
							settings_DLC4_Pip2 = bool.Parse(array7[1]);
							settings_DLC4_Pip3 = bool.Parse(array7[2]);
							settings_DLC4_Pip4 = bool.Parse(array7[3]);
							settings_DLC4_Pip5 = bool.Parse(array7[4]);
							settings_DLC4_Pip6 = bool.Parse(array7[5]);
						}
						break;
					}
					case "presets1":
					{
						settings_MPPresets1 = "";
						string text4 = array3[1];
						if (text4.Length > 10)
						{
							byte[] array6 = Convert.FromBase64String(text4);
							if (array6.Length != 0)
							{
								settings_MPPresets1 = Encoding.UTF8.GetString(array6);
							}
						}
						break;
					}
					case "presets2":
					{
						settings_MPPresets2 = "";
						string text3 = array3[1];
						if (text3.Length > 10)
						{
							byte[] array5 = Convert.FromBase64String(text3);
							if (array5.Length != 0)
							{
								settings_MPPresets2 = Encoding.UTF8.GetString(array5);
							}
						}
						break;
					}
					case "skirmishpresets":
					{
						settings_SkirmishPresets = "";
						string text2 = array3[1];
						if (text2.Length > 10)
						{
							byte[] array4 = Convert.FromBase64String(text2);
							if (array4.Length != 0)
							{
								settings_SkirmishPresets = Encoding.UTF8.GetString(array4);
							}
						}
						break;
					}
					case "newsletteremail":
						settings_NewsletterEmail = array3[1];
						if (settings_NewsletterEmail.Length > 100)
						{
							settings_NewsletterEmail = settings_UserName.Substring(0, 100);
						}
						break;
					}
				}
				catch (Exception)
				{
				}
			}
		}
		if (settings_Progress_Trail > 51)
		{
			settings_Progress_Trail = 51;
		}
		if (Settings_Progress_Historical1Campaign > 5)
		{
			Settings_Progress_Historical1Campaign = 5;
		}
		if (Settings_Progress_Historical2Campaign > 5)
		{
			Settings_Progress_Historical2Campaign = 5;
		}
		if (Settings_Progress_Historical3Campaign > 5)
		{
			Settings_Progress_Historical3Campaign = 5;
		}
		if (Settings_Progress_Historical4Campaign > 5)
		{
			Settings_Progress_Historical4Campaign = 5;
		}
		if (Settings_Progress_Historical5Campaign > 5)
		{
			Settings_Progress_Historical5Campaign = 5;
		}
		if (Settings_Progress_Historical6Campaign > 5)
		{
			Settings_Progress_Historical6Campaign = 5;
		}
		if (Settings_Progress_Historical7Campaign > 5)
		{
			Settings_Progress_Historical7Campaign = 5;
		}
		if (settings_Progress_Trail2 > 31)
		{
			settings_Progress_Trail2 = 31;
		}
		if (settings_Progress_Trail3 > 21)
		{
			settings_Progress_Trail3 = 21;
		}
		if (settings_Progress_Trail_Sands1 > 6)
		{
			settings_Progress_Trail_Sands1 = 6;
		}
		if (settings_Progress_Trail_Sands2 > 8)
		{
			settings_Progress_Trail_Sands2 = 8;
		}
		if (settings_Progress_Trail_Sands3 > 10)
		{
			settings_Progress_Trail_Sands3 = 10;
		}
		if (settings_Progress_Trail_Sands4 > 12)
		{
			settings_Progress_Trail_Sands4 = 12;
		}
		if (settings_Progress_Trail_Sands5 > 10)
		{
			settings_Progress_Trail_Sands5 = 10;
		}
		if (settings_Progress_Trail_Sands6 > 10)
		{
			settings_Progress_Trail_Sands6 = 10;
		}
		if (settings_Progress_Trail_Sands7 > 10)
		{
			settings_Progress_Trail_Sands7 = 10;
		}
		if (settings_Progress_Trail_Sands8 > 10)
		{
			settings_Progress_Trail_Sands8 = 10;
		}
	}

	private static string createSettingString()
	{
		string text = "||SETTINGS||\n";
		text = text + "Name:" + settings_UserName + "\n";
		text = text + "PushMapScrolling:" + settings_PushMapScrolling + "\n";
		text = text + "ScrollSpeed:" + settings_ScrollSpeed + "\n";
		text = text + "GameSpeed:" + settings_GameSpeed + "\n";
		text = text + "SH1MouseWheel:" + settings_SH1MouseWheel + "\n";
		text = text + "SH1RTSControls:" + settings_SH1RTSControls + "\n";
		text = text + "SH1CentreControls:" + settings_SH1CentreControls + "\n";
		text = text + "TroopMoveMode:" + settings_TroopMoveMode + "\n";
		text = text + "ShowBuildingTooltips:" + settings_ShowBuildingTooltips + "\n";
		text = text + "Compass:" + settings_Compass + "\n";
		text = text + "LocalTime:" + settings_ShowLocalTime + "\n";
		text = text + "GameTime:" + settings_ShowGameTime + "\n";
		text = text + "SandsTimer:" + settings_ShowSandsTimer + "\n";
		text = text + "SandsIntro:" + settings_ShowSandsIntro + "\n";
		text = text + "RadarDefaultZoomedOut:" + settings_RadarDefaultZoomedOut + "\n";
		text = text + "CustomIntros:" + settings_CustomIntros + "\n";
		text = text + "MasterVolume:" + settings_MasterVolume.ToString(Director.defaultCulture) + "\n";
		text = text + "SFXVolume:" + settings_SFXVolume.ToString(Director.defaultCulture) + "\n";
		text = text + "SpeechVolume:" + settings_SpeechVolume.ToString(Director.defaultCulture) + "\n";
		text = text + "UnitSpeechVolume:" + settings_UnitSpeechVolume.ToString(Director.defaultCulture) + "\n";
		text = text + "MusicVolume:" + settings_MusicVolume.ToString(Director.defaultCulture) + "\n";
		text = text + "PlayUISFX:" + settings_PlayUISFX + "\n";
		text = text + "GenieSpeech:" + settings_GenieSpeech + "\n";
		text = text + "MuteInsults:" + settings_MuteInsults + "\n";
		text = text + "MuteInsultSpeech:" + settings_MuteInsultSpeech + "\n";
		text = text + "BackgroundAudio:" + settings_BackgroundAudio + "\n";
		text = text + "EnglishSpeech:" + settings_EnglishSpeech + "\n";
		text = text + "ReduceMusicVolumeForSpeech:" + settings_ReduceMusicVolumeForSpeech + "\n";
		text = text + "UseSteamOverlayForHelp:" + settings_UseSteamOverlayForHelp + "\n";
		text = text + "CheatKeysEnabled:" + settings_CheatKeysEnabled + "\n";
		text = text + "CoopCheatsEnabled:" + settings_CoopCheatsEnabled + "\n";
		text = text + "ShowPings:" + settings_ShowPings + "\n";
		text = text + "ExtraZoom:" + settings_ExtraZoom + "\n";
		text = text + "Leaderboard_OptOut:" + settings_Leaderboard_OptOut + "\n";
		text = text + "Leaderboard_Names:" + settings_Leaderboard_Names + "\n";
		text = text + "Leaderboard_Images:" + settings_Leaderboard_Images + "\n";
		text = text + "Show_Extreme_Warning:" + settings_Show_Extreme_Warning + "\n";
		text = text + "Confirm_Disband_Troops:" + settings_Confirm_Disband_Troops + "\n";
		if (settings_ArabicL2R)
		{
			text = text + "ArabicL2R:" + settings_ArabicL2R + "\n";
		}
		text = text + "LockCursor:" + settings_LockCursor + "\n";
		text = text + "CursorStyle:" + settings_CursorStyle + "\n";
		text = text + "PlayerColour:" + settings_PlayerColour + "\n";
		text = text + "AvatarOrdinary:" + settings_AvatarOrdinary + "\n";
		text = text + "AvatarOrdinaryColour1:" + settings_AvatarOrdinaryColour1 + "\n";
		text = text + "AvatarOrdinaryColour2:" + settings_AvatarOrdinaryColour2 + "\n";
		text = text + "AvatarCharge:" + Settings_AvatarCharge + "\n";
		text = text + "AvatarChargeColour1:" + settings_AvatarChargeColour1 + "\n";
		text = text + "AvatarChargeColour2:" + settings_AvatarChargeColour2 + "\n";
		text = text + "UseSteamAvatar:" + settings_UseSteamAvatar + "\n";
		text = text + "MuteMPChat:" + settings_MuteMPChat + "\n";
		text = text + "LordType:" + settings_LordType + "\n";
		text = text + "Moat:" + settings_ShowPlannedMoat + "\n";
		text = text + "WinWidth:" + settings_LastWindowWidth + "\n";
		text = text + "WinHeight:" + settings_LastWindowHeight + "\n";
		text = text + "FullWidth:" + settings_LastFullscreenWidth + "\n";
		text = text + "FullHeight:" + settings_LastFullscreenHeight + "\n";
		text = text + "FullRefreshRate:" + settings_LastFullscreenRefresh + "\n";
		text = text + "FullscreenType:" + settings_LastFullscreenType + "\n";
		text = text + "VSync:" + settings_Vsync + "\n";
		text = text + "Scribe:" + settings_Scribe + "\n";
		text = text + "SkipIntro:" + settings_SkipIntro + "\n";
		text = text + "HideSoTTiming:" + settings_HideSoTTiming + "\n";
		text = text + "Allow_Classic_Bedouin_Stockade:" + settings_Allow_Classic_Bedouin_Stockade + "\n";
		text = text + "ShowCustomisationWarning:" + settings_ShowCustomisationWarning + "\n";
		text = text + "UIScale:" + settings_UIScale.ToString(Director.defaultCulture) + "\n";
		text = text + "Campaign1:" + Settings_Progress_Historical1Campaign + "\n";
		text = text + "Campaign2:" + Settings_Progress_Historical2Campaign + "\n";
		text = text + "Campaign3:" + Settings_Progress_Historical3Campaign + "\n";
		text = text + "Campaign4:" + Settings_Progress_Historical4Campaign + "\n";
		text = text + "Campaign5:" + Settings_Progress_Historical5Campaign + "\n";
		text = text + "Campaign6:" + Settings_Progress_Historical6Campaign + "\n";
		text = text + "Campaign7:" + Settings_Progress_Historical7Campaign + "\n";
		text = text + "CampaignTrail:" + settings_Progress_Trail + "\n";
		text = text + "CampaignTrail2:" + settings_Progress_Trail2 + "\n";
		text = text + "CampaignTrail3:" + settings_Progress_Trail3 + "\n";
		text = text + "Sands1Trail:" + settings_Progress_Trail_Sands1 + "\n";
		text = text + "Sands2Trail:" + settings_Progress_Trail_Sands2 + "\n";
		text = text + "Sands3Trail:" + settings_Progress_Trail_Sands3 + "\n";
		text = text + "Sands4Trail:" + settings_Progress_Trail_Sands4 + "\n";
		text = text + "Trail1Difficulty:" + settings_Trail1Difficulty + "\n";
		text = text + "Trail2Difficulty:" + settings_Trail2Difficulty + "\n";
		text = text + "Trail3Difficulty:" + settings_Trail3Difficulty + "\n";
		text = text + "SandsTrail1Difficulty:" + settings_SandsTrail1Difficulty + "\n";
		text = text + "SandsTrail2Difficulty:" + settings_SandsTrail2Difficulty + "\n";
		text = text + "SandsTrail3Difficulty:" + settings_SandsTrail3Difficulty + "\n";
		text = text + "SandsTrail4Difficulty:" + settings_SandsTrail4Difficulty + "\n";
		text = text + "Sands5Trail:" + settings_Progress_Trail_Sands5 + "\n";
		text = text + "SandsTrail5Difficulty:" + settings_SandsTrail5Difficulty + "\n";
		text = text + "Sands6Trail:" + settings_Progress_Trail_Sands6 + "\n";
		text = text + "SandsTrail6Difficulty:" + settings_SandsTrail6Difficulty + "\n";
		text = text + "Sands7Trail:" + settings_Progress_Trail_Sands7 + "\n";
		text = text + "SandsTrail7Difficulty:" + settings_SandsTrail7Difficulty + "\n";
		text = text + "Sands8Trail:" + settings_Progress_Trail_Sands8 + "\n";
		text = text + "SandsTrail8Difficulty:" + settings_SandsTrail8Difficulty + "\n";
		text += "Trail1Times:";
		for (int i = 0; i < 50; i++)
		{
			text = text + settings_Trail1Times[i] + ":";
		}
		text += "\n";
		text += "Trail2Times:";
		for (int j = 0; j < 30; j++)
		{
			text = text + settings_Trail2Times[j] + ":";
		}
		text += "\n";
		text += "Trail3Times:";
		for (int k = 0; k < 20; k++)
		{
			text = text + settings_Trail3Times[k] + ":";
		}
		text += "\n";
		text += "TrailS1Times:";
		for (int l = 0; l < 5; l++)
		{
			text = text + settings_Trail_Sands1_Times[l] + ":";
		}
		text += "\n";
		text += "TrailS2Times:";
		for (int m = 0; m < 7; m++)
		{
			text = text + settings_Trail_Sands2_Times[m] + ":";
		}
		text += "\n";
		text += "TrailS3Times:";
		for (int n = 0; n < 9; n++)
		{
			text = text + settings_Trail_Sands3_Times[n] + ":";
		}
		text += "\n";
		text += "TrailS4Times:";
		for (int num = 0; num < 11; num++)
		{
			text = text + settings_Trail_Sands4_Times[num] + ":";
		}
		text += "\n";
		text += "TrailS5Times:";
		for (int num2 = 0; num2 < 9; num2++)
		{
			text = text + settings_Trail_Sands5_Times[num2] + ":";
		}
		text += "\n";
		text += "TrailS6Times:";
		for (int num3 = 0; num3 < 9; num3++)
		{
			text = text + settings_Trail_Sands6_Times[num3] + ":";
		}
		text += "\n";
		text += "TrailS7Times:";
		for (int num4 = 0; num4 < 9; num4++)
		{
			text = text + settings_Trail_Sands7_Times[num4] + ":";
		}
		text += "\n";
		text += "TrailS8Times:";
		for (int num5 = 0; num5 < 9; num5++)
		{
			text = text + settings_Trail_Sands8_Times[num5] + ":";
		}
		text += "\n";
		text = text + "ShowExtremeHelp:" + settings_ShowExtremeHelp + "\n";
		text = text + "DLC4Pips:" + settings_DLC4_Pip1 + "," + settings_DLC4_Pip2 + "," + settings_DLC4_Pip3 + "," + settings_DLC4_Pip4 + "," + settings_DLC4_Pip5 + "," + settings_DLC4_Pip6 + "\n";
		string text2 = Convert.ToBase64String(Encoding.UTF8.GetBytes(settings_MPPresets1));
		text = text + "presets1:" + text2 + "\n";
		text2 = Convert.ToBase64String(Encoding.UTF8.GetBytes(settings_MPPresets2));
		text = text + "presets2:" + text2 + "\n";
		text2 = Convert.ToBase64String(Encoding.UTF8.GetBytes(settings_SkirmishPresets));
		text = text + "SkirmishPresets:" + text2 + "\n";
		text = text + "NewsletterEmail:" + settings_NewsletterEmail + "\n";
		return text + "||SETTINGS||\n";
	}

	public static void validateLordType()
	{
		if (Settings_LordType == 3)
		{
			Settings_LordType = 0;
		}
		if (Settings_LordType == 5 && !AchievementsCommon.Instance.IsAchievementComplete(Enums.Achievements.Place_Dairy_Farms))
		{
			Settings_LordType = 0;
		}
	}

	private static string GetScoresFileName()
	{
		return Application.persistentDataPath + "/missions.cfg";
	}

	public static void LoadScores()
	{
		string scoresFileName = GetScoresFileName();
		try
		{
			string[] array = File.ReadAllText(scoresFileName).Split("\n");
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(':');
				if (array2.Length != 2)
				{
					continue;
				}
				try
				{
					Scores scores = new Scores
					{
						mapName = array2[0]
					};
					int difficulty = 1000;
					if (array2.Length == 2)
					{
						try
						{
							difficulty = int.Parse(array2[1], Director.defaultCulture);
						}
						catch (Exception)
						{
						}
					}
					scores.difficulty = difficulty;
					ConfigSettings.scores[array2[0].ToLower()] = scores;
				}
				catch (Exception)
				{
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public static void SaveScores()
	{
		string scoresFileName = GetScoresFileName();
		string text = "";
		foreach (KeyValuePair<string, Scores> score in scores)
		{
			text = text + score.Value.mapName + ":" + score.Value.difficulty + "\n";
		}
		try
		{
			File.WriteAllText(scoresFileName, text);
		}
		catch (Exception)
		{
		}
	}

	public static void WipeCampaignScores()
	{
		scores.Clear();
		SaveScores();
	}

	public static bool MapCompleted(string mapname)
	{
		if (scores.TryGetValue(mapname.ToLower(), out var _))
		{
			return true;
		}
		return false;
	}

	public static bool MapCompleted(string mapname, ref int difficulty)
	{
		if (scores.TryGetValue(mapname.ToLower(), out var value))
		{
			difficulty = value.difficulty;
			return true;
		}
		return false;
	}

	public static int ManageScores(string mapname, int newDifficulty = 1000)
	{
		if (scores.TryGetValue(mapname.ToLower(), out var value))
		{
			bool flag = false;
			if (newDifficulty != 1000 && (value.difficulty == 1000 || newDifficulty > value.difficulty))
			{
				value.difficulty = newDifficulty;
				flag = true;
			}
			if (flag)
			{
				SaveScores();
			}
			return 0;
		}
		Scores value2 = new Scores
		{
			mapName = mapname,
			difficulty = newDifficulty
		};
		scores[mapname.ToLower()] = value2;
		SaveScores();
		return 0;
	}

	private static string GetSkirmishMastersFileName()
	{
		return Application.persistentDataPath + "/skirmishmasters.cfg";
	}

	public static List<EngineInterface.MPScoreData> GetSkirmishMastersData()
	{
		return skirmishMasters.skirmishMastersData;
	}

	public static void AddSkirmishMastersGame(EngineInterface.MPScoreData data)
	{
		skirmishMasters.skirmishMastersData.Add(data);
		ReIndexSkirmishMasters();
		SaveSkirmishMasters();
	}

	public static void DeleteSkirmishMastersGame(EngineInterface.MPScoreData data)
	{
		foreach (EngineInterface.MPScoreData skirmishMastersDatum in skirmishMasters.skirmishMastersData)
		{
			if (skirmishMastersDatum.index == data.index)
			{
				skirmishMasters.skirmishMastersData.Remove(skirmishMastersDatum);
				break;
			}
		}
		ReIndexSkirmishMasters();
		SaveSkirmishMasters();
	}

	private static void SaveSkirmishMasters()
	{
		string skirmishMastersFileName = GetSkirmishMastersFileName();
		try
		{
			string contents = JsonUtility.ToJson(skirmishMasters);
			File.WriteAllText(skirmishMastersFileName, contents);
		}
		catch (Exception)
		{
		}
	}

	private static void LoadSkirmishMasters()
	{
		string skirmishMastersFileName = GetSkirmishMastersFileName();
		try
		{
			skirmishMasters = JsonUtility.FromJson<SkirmishMasters>(File.ReadAllText(skirmishMastersFileName));
			List<EngineInterface.MPScoreData> list = new List<EngineInterface.MPScoreData>();
			foreach (EngineInterface.MPScoreData skirmishMastersDatum in skirmishMasters.skirmishMastersData)
			{
				if (skirmishMastersDatum.version != 2)
				{
					list.Add(skirmishMastersDatum);
				}
			}
			foreach (EngineInterface.MPScoreData item in list)
			{
				skirmishMasters.skirmishMastersData.Remove(item);
			}
			ReIndexSkirmishMasters();
		}
		catch (Exception)
		{
		}
		if (skirmishMasters == null)
		{
			skirmishMasters = new SkirmishMasters();
		}
	}

	private static void ReIndexSkirmishMasters()
	{
		int num = 0;
		foreach (EngineInterface.MPScoreData skirmishMastersDatum in skirmishMasters.skirmishMastersData)
		{
			skirmishMastersDatum.index = num++;
		}
	}

	private static string GetCoopFileName()
	{
		return Application.persistentDataPath + "/coop.cfg";
	}

	public static void LoadCoop()
	{
		string coopFileName = GetCoopFileName();
		try
		{
			string[] array = File.ReadAllText(coopFileName).Split("\n");
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Replace("\r", "").Split("|");
				string[] array3 = array2[0].Split(':');
				if (array3.Length != 4 && array3.Length != 5)
				{
					continue;
				}
				try
				{
					ulong num = ulong.Parse(array3[0], Director.defaultCulture);
					Coop coop = new Coop
					{
						steamID = num
					};
					try
					{
						string[] array4 = array3[1].Split(',');
						for (int j = 0; j < 10 && j < array4.Length; j++)
						{
							coop.trail1[j] = int.Parse(array4[j], Director.defaultCulture);
						}
						string[] array5 = array3[2].Split(',');
						for (int k = 0; k < 10 && k < array5.Length; k++)
						{
							coop.trail2[k] = int.Parse(array5[k], Director.defaultCulture);
						}
						if (array5.Length >= 20)
						{
							for (int l = 0; l < 10 && l < array5.Length; l++)
							{
								coop.trail3[l] = int.Parse(array5[l + 10], Director.defaultCulture);
							}
						}
						if (array5.Length >= 30)
						{
							for (int m = 0; m < 10 && m < array5.Length; m++)
							{
								coop.trail4[m] = int.Parse(array5[m + 20], Director.defaultCulture);
							}
						}
						coop.userName = array3[3];
						coop.hidden = array3.Length == 5;
						if (array2.Length > 1)
						{
							coop.CoAString = array2[1];
						}
						else
						{
							coop.CoAString = "";
						}
					}
					catch (Exception)
					{
					}
					coopInfoDict[num] = coop;
					coopInfoList.Add(coop);
				}
				catch (Exception)
				{
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public static void SaveCoop()
	{
		if (coopInfoDict.Count <= 0)
		{
			return;
		}
		string coopFileName = GetCoopFileName();
		string text = "";
		foreach (KeyValuePair<ulong, Coop> item in coopInfoDict)
		{
			text = text + item.Value.steamID + ":";
			for (int i = 0; i < 10; i++)
			{
				text += item.Value.trail1[i];
				text = ((i >= 9) ? (text + ":") : (text + ","));
			}
			for (int j = 0; j < 10; j++)
			{
				text += item.Value.trail2[j];
				if (j < 9)
				{
					text += ",";
				}
			}
			text += ",";
			for (int k = 0; k < 10; k++)
			{
				text += item.Value.trail3[k];
				if (k < 9)
				{
					text += ",";
				}
			}
			text += ",";
			for (int l = 0; l < 10; l++)
			{
				text += item.Value.trail4[l];
				if (l < 9)
				{
					text += ",";
				}
			}
			text = text + ":" + item.Value.userName.Replace(":", "").Replace("|", "");
			if (item.Value.hidden)
			{
				text += ":1";
			}
			text = text + "|" + item.Value.CoAString;
			text += Environment.NewLine;
		}
		try
		{
			File.WriteAllText(coopFileName, text);
		}
		catch (Exception)
		{
		}
	}

	public static int[] getCoopInfo(ulong steamID, int trailID)
	{
		if (coopInfoDict.TryGetValue(steamID, out var value))
		{
			switch (trailID)
			{
			case 0:
				return value.trail1;
			case 1:
				return value.trail2;
			case 2:
				return value.trail3;
			case 3:
				return value.trail4;
			}
		}
		return null;
	}

	public static void setCoopHidden(ulong steamID, bool state)
	{
		if (coopInfoDict.TryGetValue(steamID, out var value) && value.hidden != state)
		{
			value.hidden = state;
			SaveCoop();
		}
	}

	public static bool getCoopRowHiddenInfo(ulong steamID, out string userName)
	{
		if (coopInfoDict.TryGetValue(steamID, out var value))
		{
			if (value.steamID < 10000)
			{
				int num = (int)(value.steamID - 1000);
				if (num >= 25)
				{
					userName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 453 + 17 * (num - 25));
				}
				else if (num >= 16)
				{
					userName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 88 + 9 * (num - 16));
				}
				else
				{
					userName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 239 + 9 * num);
				}
			}
			else
			{
				userName = value.userName;
			}
			return value.hidden;
		}
		userName = "";
		return false;
	}

	public static int[] getCoopRowInfo(int row, int trailID, out ulong steamID, out string userName, out bool hidden, bool countHidden, out string CoAString)
	{
		if (!countHidden)
		{
			int num = 0;
			int num2 = 0;
			bool flag = false;
			foreach (KeyValuePair<ulong, Coop> item in coopInfoDict)
			{
				if (!item.Value.hidden)
				{
					if (num == row)
					{
						row = num2;
						flag = true;
						break;
					}
					num++;
				}
				num2++;
			}
			if (!flag)
			{
				steamID = 0uL;
				userName = "";
				hidden = false;
				CoAString = "";
				return null;
			}
		}
		if (row < coopInfoList.Count)
		{
			Coop coop = coopInfoList[row];
			steamID = coop.steamID;
			hidden = coop.hidden;
			if (steamID < 10000)
			{
				int num3 = (int)(steamID - 1000);
				if (num3 >= 25)
				{
					userName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 453 + 17 * (num3 - 25));
				}
				else if (num3 >= 16)
				{
					userName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 88 + 9 * (num3 - 16));
				}
				else
				{
					userName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 239 + 9 * num3);
				}
			}
			else
			{
				userName = coop.userName;
			}
			CoAString = coop.CoAString;
			switch (trailID)
			{
			case 0:
				return coop.trail1;
			case 1:
				return coop.trail2;
			case 2:
				return coop.trail3;
			case 3:
				return coop.trail4;
			}
		}
		steamID = 0uL;
		userName = "";
		hidden = false;
		CoAString = "";
		return null;
	}

	public static int getCoopTrailCount(bool countHidden)
	{
		if (!countHidden)
		{
			int num = 0;
			{
				foreach (KeyValuePair<ulong, Coop> item in coopInfoDict)
				{
					if (!item.Value.hidden)
					{
						num++;
					}
				}
				return num;
			}
		}
		return coopInfoList.Count;
	}

	public static void InitCoopGame(ulong steamID, string userName, string _CoAString = "")
	{
		if (!coopInfoDict.ContainsKey(steamID))
		{
			Coop coop = new Coop();
			coop.steamID = steamID;
			coop.userName = userName;
			coop.CoAString = _CoAString;
			coopInfoDict[steamID] = coop;
			coopInfoList.Add(coop);
			SaveCoop();
		}
		else if (userName != "[unknown]")
		{
			Coop coop2 = coopInfoDict[steamID];
			if (coop2.userName != userName || coop2.CoAString != _CoAString)
			{
				coop2.userName = userName;
				coop2.CoAString = _CoAString;
				SaveCoop();
			}
		}
	}

	public static void CoopCompleted(ulong steamID, int trailID, int missionID, string _CoAString = "")
	{
		if (!coopInfoDict.TryGetValue(steamID, out var value))
		{
			return;
		}
		switch (trailID)
		{
		case 0:
			if (missionID >= 0 && missionID < 10)
			{
				value.trail1[missionID] = 1;
				value.CoAString = _CoAString;
				SaveCoop();
			}
			break;
		case 1:
			if (missionID >= 0 && missionID < 10)
			{
				value.trail2[missionID] = 1;
				value.CoAString = _CoAString;
				SaveCoop();
			}
			break;
		case 2:
			if (missionID >= 0 && missionID < 10)
			{
				value.trail3[missionID] = 1;
				value.CoAString = _CoAString;
				SaveCoop();
			}
			break;
		case 3:
			if (missionID >= 0 && missionID < 10)
			{
				value.trail4[missionID] = 1;
				value.CoAString = _CoAString;
				SaveCoop();
			}
			break;
		}
	}

	public static void CalcCoopProgress(ulong steamID, bool capProgress = false)
	{
		switch (steamID)
		{
		case 1uL:
		{
			for (int k = 0; k < 10; k++)
			{
				Settings_Progress_Trail_Coop1_Status[k] = 0;
				Settings_Progress_Trail_Coop2_Status[k] = 0;
				Settings_Progress_Trail_Coop3_Status[k] = 0;
				Settings_Progress_Trail_Coop4_Status[k] = 0;
			}
			Settings_Progress_Trail_Coop1 = 1;
			Settings_Progress_Trail_Coop2 = 1;
			Settings_Progress_Trail_Coop3 = 1;
			Settings_Progress_Trail_Coop4 = 1;
			break;
		}
		case 0uL:
		{
			int num5 = 0;
			if (TempMissionUnlock || Settings_CoopCheatsEnabled)
			{
				num5 = 1;
			}
			for (int l = 0; l < 10; l++)
			{
				Settings_Progress_Trail_Coop1_Status[l] = num5;
				Settings_Progress_Trail_Coop2_Status[l] = num5;
				Settings_Progress_Trail_Coop3_Status[l] = num5;
				Settings_Progress_Trail_Coop4_Status[l] = num5;
			}
			foreach (KeyValuePair<ulong, Coop> item in coopInfoDict)
			{
				for (int m = 0; m < 10; m++)
				{
					if (item.Value.trail1[m] > 0)
					{
						Settings_Progress_Trail_Coop1_Status[m] = 1;
					}
					if (item.Value.trail2[m] > 0)
					{
						Settings_Progress_Trail_Coop2_Status[m] = 1;
					}
					if (item.Value.trail3[m] > 0)
					{
						Settings_Progress_Trail_Coop3_Status[m] = 1;
					}
					if (item.Value.trail4[m] > 0)
					{
						Settings_Progress_Trail_Coop4_Status[m] = 1;
					}
				}
			}
			for (int num6 = 9; num6 >= 0; num6--)
			{
				if (Settings_Progress_Trail_Coop1_Status[num6] > 0)
				{
					Settings_Progress_Trail_Coop1 = num6 + 2;
					break;
				}
			}
			for (int num7 = 9; num7 >= 0; num7--)
			{
				if (Settings_Progress_Trail_Coop2_Status[num7] > 0)
				{
					Settings_Progress_Trail_Coop2 = num7 + 2;
					break;
				}
			}
			for (int num8 = 9; num8 >= 0; num8--)
			{
				if (Settings_Progress_Trail_Coop3_Status[num8] > 0)
				{
					Settings_Progress_Trail_Coop3 = num8 + 2;
					break;
				}
			}
			for (int num9 = 9; num9 >= 0; num9--)
			{
				if (Settings_Progress_Trail_Coop4_Status[num9] > 0)
				{
					Settings_Progress_Trail_Coop4 = num9 + 2;
					break;
				}
			}
			break;
		}
		default:
		{
			int[] coopInfo = getCoopInfo(steamID, 0);
			int[] coopInfo2 = getCoopInfo(steamID, 1);
			int[] coopInfo3 = getCoopInfo(steamID, 2);
			int[] coopInfo4 = getCoopInfo(steamID, 3);
			if (coopInfo != null && coopInfo2 != null)
			{
				for (int i = 0; i < 10; i++)
				{
					Settings_Progress_Trail_Coop1_Status[i] = coopInfo[i];
					Settings_Progress_Trail_Coop2_Status[i] = coopInfo2[i];
					if (coopInfo3 != null)
					{
						Settings_Progress_Trail_Coop3_Status[i] = coopInfo3[i];
					}
					else
					{
						Settings_Progress_Trail_Coop3_Status[i] = 0;
					}
					if (coopInfo4 != null)
					{
						Settings_Progress_Trail_Coop4_Status[i] = coopInfo4[i];
					}
					else
					{
						Settings_Progress_Trail_Coop4_Status[i] = 0;
					}
				}
			}
			else
			{
				for (int j = 0; j < 10; j++)
				{
					Settings_Progress_Trail_Coop1_Status[j] = 0;
					Settings_Progress_Trail_Coop2_Status[j] = 0;
					Settings_Progress_Trail_Coop3_Status[j] = 0;
					Settings_Progress_Trail_Coop4_Status[j] = 0;
				}
			}
			if (!capProgress)
			{
				break;
			}
			for (int num = 9; num >= 0; num--)
			{
				if (Settings_Progress_Trail_Coop1_Status[num] > 0)
				{
					Settings_Progress_Trail_Coop1 = num + 2;
					break;
				}
			}
			for (int num2 = 9; num2 >= 0; num2--)
			{
				if (Settings_Progress_Trail_Coop2_Status[num2] > 0)
				{
					Settings_Progress_Trail_Coop2 = num2 + 2;
					break;
				}
			}
			for (int num3 = 9; num3 >= 0; num3--)
			{
				if (Settings_Progress_Trail_Coop3_Status[num3] > 0)
				{
					Settings_Progress_Trail_Coop3 = num3 + 2;
					break;
				}
			}
			for (int num4 = 9; num4 >= 0; num4--)
			{
				if (Settings_Progress_Trail_Coop4_Status[num4] > 0)
				{
					Settings_Progress_Trail_Coop4 = num4 + 2;
					break;
				}
			}
			break;
		}
		}
		Settings_Progress_Trail_CoopNext1 = 9;
		Settings_Progress_Trail_CoopNext2 = 9;
		for (int n = 0; n < 10; n++)
		{
			if (Settings_Progress_Trail_Coop1_Status[n] == 0)
			{
				Settings_Progress_Trail_CoopNext1 = n;
				break;
			}
		}
		for (int num10 = 0; num10 < 10; num10++)
		{
			if (Settings_Progress_Trail_Coop2_Status[num10] == 0)
			{
				Settings_Progress_Trail_CoopNext2 = num10;
				break;
			}
		}
		Settings_Progress_Trail_CoopNext3 = 9;
		for (int num11 = 0; num11 < 10; num11++)
		{
			if (Settings_Progress_Trail_Coop3_Status[num11] == 0)
			{
				Settings_Progress_Trail_CoopNext3 = num11;
				break;
			}
		}
		Settings_Progress_Trail_CoopNext4 = 9;
		for (int num12 = 0; num12 < 10; num12++)
		{
			if (Settings_Progress_Trail_Coop4_Status[num12] == 0)
			{
				Settings_Progress_Trail_CoopNext4 = num12;
				break;
			}
		}
	}

	private static string GetCustomTrailsCompletedFileName()
	{
		return Application.persistentDataPath + "/custom_trails.cfg";
	}

	public static void CustomTrailCheat(string trailName, int maxLength)
	{
		int[] customTrailStatus = GetCustomTrailStatus(trailName);
		for (int i = 0; i < 50 && i < maxLength; i++)
		{
			if (customTrailStatus[i] == 0)
			{
				AddCustomTrailScore(trailName, i, completed: false, cheated: true);
				break;
			}
		}
	}

	public static void AddCustomTrailScore(string trailName, int missionID, bool completed, bool cheated)
	{
		if (!customTrailCompleted.TryGetValue(trailName, out var value))
		{
			value = new int[50];
		}
		if (completed)
		{
			value[missionID] = 1;
		}
		else if (cheated)
		{
			value[missionID] = -1;
		}
		customTrailCompleted[trailName] = value;
		SaveCustomTrailInfo();
	}

	public static int[] GetCustomTrailStatus(string trailName)
	{
		if (customTrailCompleted.TryGetValue(trailName, out var value))
		{
			return value;
		}
		return new int[50];
	}

	public static int GetCustomTrailProgress(string trailName)
	{
		int[] customTrailStatus = GetCustomTrailStatus(trailName);
		for (int i = 0; i < 50; i++)
		{
			if (customTrailStatus[i] == 0)
			{
				return i;
			}
		}
		return 50;
	}

	public static void LoadCustomTrailInfo()
	{
		string customTrailsCompletedFileName = GetCustomTrailsCompletedFileName();
		try
		{
			string[] array = File.ReadAllText(customTrailsCompletedFileName).Split("\n");
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split('/');
				if (array2.Length != 2)
				{
					continue;
				}
				try
				{
					int[] array3 = new int[50];
					string[] array4 = array2[1].Split(':');
					if (array4.Length >= 50)
					{
						for (int j = 0; j < 50; j++)
						{
							array3[j] = int.Parse(array4[j]);
						}
						customTrailCompleted[array2[0]] = array3;
					}
				}
				catch (Exception)
				{
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public static void SaveCustomTrailInfo()
	{
		if (customTrailCompleted.Count <= 0)
		{
			return;
		}
		string customTrailsCompletedFileName = GetCustomTrailsCompletedFileName();
		string text = "";
		foreach (KeyValuePair<string, int[]> item in customTrailCompleted)
		{
			text = text + item.Key + "/";
			for (int i = 0; i < 50; i++)
			{
				text = text + item.Value[i] + ":";
			}
			text += "\n";
		}
		try
		{
			File.WriteAllText(customTrailsCompletedFileName, text);
		}
		catch (Exception)
		{
		}
	}

	public static string GetUserWorkshopPath()
	{
		string text = Application.persistentDataPath + "\\UserWorkshopMaps";
		if (!Directory.Exists(text))
		{
			Directory.CreateDirectory(text);
		}
		return text;
	}

	public static string GetWorkshopUploadRootPath()
	{
		string text = Application.persistentDataPath + "\\WorkshopTemp";
		if (!Directory.Exists(text))
		{
			Directory.CreateDirectory(text);
		}
		return text;
	}

	public static string GetWorkshopUploadContentPath()
	{
		string text = GetWorkshopUploadRootPath() + "\\Content";
		if (Directory.Exists(text))
		{
			Directory.Delete(text, recursive: true);
		}
		Directory.CreateDirectory(text);
		return text;
	}
}
