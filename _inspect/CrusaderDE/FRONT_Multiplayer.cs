using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Noesis;
using Steamworks;
using UnityEngine;

namespace CrusaderDE;

public class FRONT_Multiplayer : UserControl
{
	public class MPAIVInfo
	{
		public int lordType;

		public string lordName = "";

		public bool builtIn = true;

		public bool community;

		public bool historical;

		public int rotation;

		public bool builtInLord = true;

		public CustomisationFileManager.CustomLordConfig lordConfig;

		public List<CustomisationFileManager.CustomAIV> aivs = new List<CustomisationFileManager.CustomAIV>();

		public byte[] imageData;

		public TextureSource image;

		public void Init(int _lordType, string _lordName)
		{
			builtInLord = true;
			lordType = _lordType;
			lordName = _lordName;
			builtIn = true;
			community = false;
			historical = false;
			rotation = 0;
			aivs.Clear();
			lordConfig = null;
			imageData = null;
			image = null;
			if (_lordName.Length > 0)
			{
				builtIn = false;
				builtInLord = false;
			}
		}

		public void Clear()
		{
			Init(0, "");
		}

		public string encode()
		{
			string text = ":X";
			if (!builtInLord && lordConfig != null)
			{
				byte[] inArray = lordConfig.encode();
				text = ":" + Convert.ToBase64String(inArray);
			}
			if (builtIn || aivs.Count == 0)
			{
				return "0:" + lordType + ":" + rotation + text + ":" + lordName;
			}
			if (community)
			{
				return "1:" + lordType + ":" + rotation + text + ":" + lordName;
			}
			if (historical)
			{
				return "2:" + lordType + ":" + rotation + text + ":" + lordName;
			}
			byte[] inArray2 = aivs[0].encode();
			if (imageData != null)
			{
				return Convert.ToBase64String(inArray2) + ":" + lordType + ":" + rotation + text + ":" + lordName + ":" + Convert.ToBase64String(imageData);
			}
			return Convert.ToBase64String(inArray2) + ":" + lordType + ":" + rotation + text + ":" + lordName;
		}

		public static string decodeLordName(string input)
		{
			try
			{
				string[] array = input.Split(':', StringSplitOptions.None);
				if (array.Length >= 5)
				{
					return array[4];
				}
			}
			catch (Exception)
			{
			}
			return "";
		}

		public void decode(string input)
		{
			lordType = 0;
			builtIn = true;
			builtInLord = true;
			community = false;
			historical = false;
			rotation = 0;
			lordName = "";
			imageData = null;
			image = null;
			aivs.Clear();
			try
			{
				string[] array = input.Split(':', StringSplitOptions.None);
				if (array.Length < 2 || array.Length > 6)
				{
					return;
				}
				if (array[0].Length == 0 || array[0] == "0")
				{
					builtIn = true;
				}
				else if (array[0] == "1")
				{
					builtIn = false;
					community = true;
				}
				else if (array[0] == "2")
				{
					builtIn = false;
					historical = true;
				}
				else
				{
					builtIn = false;
					CustomisationFileManager.CustomAIV item = CustomisationFileManager.CustomAIV.decode(Convert.FromBase64String(array[0]), 0);
					aivs.Add(item);
				}
				lordType = int.Parse(array[1]);
				if (array.Length >= 3)
				{
					rotation = int.Parse(array[2]);
				}
				if (array.Length >= 4 && array[3].Length > 1)
				{
					builtInLord = false;
					byte[] data = Convert.FromBase64String(array[3]);
					lordConfig = CustomisationFileManager.CustomLordConfig.decode(data);
				}
				if (array.Length >= 5)
				{
					lordName = array[4];
				}
				if (array.Length >= 6)
				{
					byte[] fileData = Convert.FromBase64String(array[5]);
					TextureSource val = MainViewModel.Instance.LoadImageFile(fileData);
					if ((BaseComponent)(object)val != (BaseComponent)null && ((ImageSource)val).Width == 144f && ((ImageSource)val).Height == 144f)
					{
						imageData = fileData;
						image = val;
					}
				}
			}
			catch (Exception)
			{
			}
		}
	}

	public class LobbyChatEntry
	{
		public string name;

		public string message;

		public int colourID;

		public DateTime received;
	}

	public class PlayerRow
	{
		public Grid RefRow;

		public Image RefReadyState;

		public Image RefColour;

		public TextBlock RefName;

		public Image RefHost;

		public TextBlock RefType;

		public TextBlock RefPing;

		public Button RefKick;

		public Button RefAISettings;

		public int playerID;

		public void Clear()
		{
			((UIElement)RefRow).Visibility = (Visibility)1;
			playerID = -1;
		}

		public void Update(FRONT_Multiplayer parent, Platform_Multiplayer.MPLobbyMember member, int row, int player)
		{
			playerID = player;
			if (member == null)
			{
				Clear();
				return;
			}
			SetVisibility((UIElement)(object)RefRow, (Visibility)2);
			if (playerID == 1 && !skirmishGame)
			{
				SetVisibility((UIElement)(object)RefHost, (Visibility)2);
			}
			else
			{
				SetVisibility((UIElement)(object)RefHost, (Visibility)1);
			}
			if (skirmishGame)
			{
				if (player == 1 && !spectatorMode)
				{
					SetButtonVisibility((UIElement)(object)RefKick, (Visibility)1);
				}
				else
				{
					SetButtonVisibility((UIElement)(object)RefKick, (Visibility)2);
				}
			}
			else if (parent.currentLobby.isHost && member.IsSelf())
			{
				SetButtonVisibility((UIElement)(object)RefKick, (Visibility)1);
			}
			else if (parent.currentLobby.isHost)
			{
				SetButtonVisibility((UIElement)(object)RefKick, (Visibility)2);
			}
			else
			{
				SetButtonVisibility((UIElement)(object)RefKick, (Visibility)1);
			}
			if (!skirmishGame && parent.currentLobby.isHost)
			{
				if (!member.IsSelf())
				{
					if (member.lastPingDuration > 0)
					{
						RefPing.Text = member.lastPingDuration / 2 + "ms";
					}
					else
					{
						RefPing.Text = "";
					}
				}
				else
				{
					RefPing.Text = "";
				}
			}
			else if ((BaseComponent)(object)RefPing != (BaseComponent)null)
			{
				RefPing.Text = "";
			}
			string name = member.Name;
			if (RefName.Text != name)
			{
				RefName.Text = name;
			}
			if (member.SkirmishHumanMember || !member.SkirmishMember)
			{
				RefType.Text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_WR_HUMAN);
				PropEx.SetButtonVisibility((UIElement)(object)RefAISettings, (Visibility)1);
			}
			else
			{
				RefType.Text = member.AITypeName;
				if ((skirmishGame || (parent.currentLobby != null && parent.currentLobby.isHost)) && member.SkirmishCustomLordExistsLocally)
				{
					PropEx.SetButtonVisibility((UIElement)(object)RefAISettings, (Visibility)2);
				}
				else
				{
					PropEx.SetButtonVisibility((UIElement)(object)RefAISettings, (Visibility)1);
				}
			}
			if (!skirmishGame)
			{
				if (!member.SkirmishMember)
				{
					if (member.ready)
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
				else if ((BaseComponent)(object)RefReadyState.Source != (BaseComponent)null)
				{
					RefReadyState.Source = null;
				}
			}
			else if ((BaseComponent)(object)RefReadyState.Source != (BaseComponent)null)
			{
				RefReadyState.Source = null;
			}
			ImageSource colourShield = GetColourShield(member.colourID);
			if ((BaseComponent)(object)RefColour.Source != (BaseComponent)(object)colourShield)
			{
				RefColour.Source = colourShield;
			}
			parent.currentLobby.getTeam(member).ToString();
		}
	}

	public class CoopMissionSetupData
	{
		public string mapName;

		public FileHeader header;

		public int[] keepOrder;

		public int fairness = 4;

		public int starting_level = 1;

		public int[] teams;

		public int[] AIs;

		public int[] AIVs;
	}

	public class AvatarCallback
	{
		public int row;

		public ulong steamID;
	}

	public const int MAX_HUMANS = 9;

	public bool panelLoaded;

	public ListView RefLobbyLists;

	public Slider RefLobbyMaxPlayersSlider;

	public Button RefJoinButton;

	public Button RefLobbySettingsButton;

	public Grid RefHeaderBar;

	public TextBox RefTextBoxGameName;

	public Button RefMultiplayerPlayButton;

	public Button RefReadyButton;

	public Button RefReadyButtonLock;

	public Button RefLoadButton;

	public TextBox RefMP_ChatInput;

	public TextBlock RefMP_ChatDisplay;

	public ScrollViewer RefMP_ChatScrollView;

	public Button RefColourSelectButton;

	public Button RefColShield1;

	public Button RefColShield2;

	public Button RefColShield3;

	public Button RefColShield4;

	public Button RefColShield5;

	public Button RefColShield6;

	public Button RefColShield7;

	public Button RefColShield8;

	public Button RefRandomAI1;

	public Button RefRandomAI2;

	public Button RefRandomAI3;

	public Button RefRandomAI4;

	public Button RefRandomAI5;

	public Button RefRandomAI6;

	public Button RefRandomAI7;

	public Button RefMultiplayerInvite;

	public Button RefMP_ChatSend;

	public Slider RefMapSizeMin_Slider;

	public Slider RefMapSizeMax_Slider;

	public Slider RefAIMin_Slider;

	public Slider RefAIMax_Slider;

	public CheckBox RefRandomTeams;

	public CheckBox RefRandomBalance;

	public CheckBox RefRandomOutposts;

	public CheckBox RefRandomExtreme;

	public CheckBox RefRandomAdvanced;

	public CheckBox RefRandomIncludeUser;

	public CheckBox RefRandomIncludeBuiltin;

	public CheckBox RefRandomIncludeWorkshop;

	public Button RefMultiplayerSetupInfo;

	public Image RefBasemap;

	public TextBox RefMP_SearchFilter;

	public TextBox RefMP_EnterShareCodeText;

	public Button RefShareJoinButton;

	public Storyboard pulseAnimation;

	public Storyboard settingsPulseAnimation;

	public RadioButton RefFairness1;

	public RadioButton RefFairness2;

	public RadioButton RefFairness3;

	public RadioButton RefFairness4;

	public RadioButton RefFairness5;

	public RadioButton RefGameType1;

	public RadioButton RefGameType2;

	public RadioButton RefGameType3;

	public Button RefRadarShield1;

	public Button RefRadarShield2;

	public Button RefRadarShield3;

	public Button RefRadarShield4;

	public Button RefRadarShield5;

	public Button RefRadarShield6;

	public Button RefRadarShield7;

	public Button RefRadarShield8;

	public Grid RefRadarShieldFace1;

	public Grid RefRadarShieldFace2;

	public Grid RefRadarShieldFace3;

	public Grid RefRadarShieldFace4;

	public Grid RefRadarShieldFace5;

	public Grid RefRadarShieldFace6;

	public Grid RefRadarShieldFace7;

	public Grid RefRadarShieldFace8;

	public Image RefRadarShieldTeam1;

	public Image RefRadarShieldTeam2;

	public Image RefRadarShieldTeam3;

	public Image RefRadarShieldTeam4;

	public Image RefRadarShieldTeam5;

	public Image RefRadarShieldTeam6;

	public Image RefRadarShieldTeam7;

	public Image RefRadarShieldTeam8;

	public Button RefTeamFace1;

	public Button RefTeamFace2;

	public Button RefTeamFace3;

	public Button RefTeamFace4;

	public Button RefTeamFace5;

	public Button RefTeamFace6;

	public Button RefTeamFace7;

	public Button RefTeamFace8;

	public Button RefTeamFaceCancel;

	public Image RefFloatingRadarShield;

	public Grid RefFloatingTeams;

	public Grid RefSkirmish_RadarMask;

	public CheckBox RefExtremeWarningCheck;

	public CheckBox RefEnableAdvancedSkirmishCheck;

	public CheckBox RefChatMuteDisable;

	public TextBlock RefMap_Balanced;

	public TextBlock RefMap_UnBalanced;

	public ListView RefCustomLordList;

	public Button RefTrailMakerTest;

	public static Color lightBarColCol = Color.FromArgb((byte)136, (byte)204, (byte)204, (byte)204);

	public static SolidColorBrush lightBarColour = new SolidColorBrush(lightBarColCol);

	public static Color darkBarColCol = Color.FromArgb((byte)136, (byte)170, (byte)170, (byte)170);

	public static SolidColorBrush darkBarColour = new SolidColorBrush(darkBarColCol);

	public static Color transparentCol = Color.FromArgb((byte)0, byte.MaxValue, byte.MaxValue, byte.MaxValue);

	public static SolidColorBrush transparentColour = new SolidColorBrush(transparentCol);

	public static Color teamYellowBarColCol = Color.FromArgb((byte)136, (byte)204, (byte)204, (byte)80);

	public static SolidColorBrush teamYellowBarColour = new SolidColorBrush(teamYellowBarColCol);

	public static Color teamRedBarColCol = Color.FromArgb((byte)136, (byte)204, (byte)80, (byte)80);

	public static SolidColorBrush teamRedBarColour = new SolidColorBrush(teamRedBarColCol);

	public static Color teamBlueBarColCol = Color.FromArgb((byte)136, (byte)80, (byte)140, (byte)204);

	public static SolidColorBrush teamBlueBarColour = new SolidColorBrush(teamBlueBarColCol);

	public static Color teamGreenBarColCol = Color.FromArgb((byte)136, (byte)80, (byte)204, (byte)80);

	public static SolidColorBrush teamGreenBarColour = new SolidColorBrush(teamGreenBarColCol);

	public List<Platform_Multiplayer.MPLobby> lobbies = new List<Platform_Multiplayer.MPLobby>();

	public string defaultMPSettings = "";

	public EngineInterface.MultiplayerSetupData MPDefaultsetupData;

	public EngineInterface.MultiplayerSetupData MPsetupData;

	public EngineInterface.MultiplayerSetupData MPTEMPsetupData;

	public static EngineInterface.MultiplayerSetupData MPLastSetupData = null;

	public Platform_Multiplayer.MPLobby selectedLobby;

	public Platform_Multiplayer.MPLobby currentLobby;

	public FileHeader selectedMPHeader;

	public int selectedCoopMissionID;

	public bool coopOrderSwapped;

	public bool singlePlayerCoop;

	public bool trailMakerMode;

	public ulong singlePlayerCoopAlly;

	public int matchmakingDefault = 1;

	public int numConnectedPlayers = 1;

	public int sortByColumn;

	public bool sortByAscending = true;

	public bool includeUser = true;

	public bool includeBuiltIn = true;

	public bool includeWorkshop = true;

	public bool MPLocalReady;

	public bool MPLocalReadyLocked;

	public bool readyAnimPlaying;

	public int MPTotalPlayers;

	public string MPLastMapName = "";

	public bool MPMapChecked;

	public bool MPMapValid;

	public bool MPGameLoading;

	public bool regetMapListNextTime;

	public bool pendingMPHost;

	public bool skipMapSelectRandomKeeps;

	public DateTime delayedSendDataToLobby = DateTime.MinValue;

	public DateTime nextHostSendPings = DateTime.MinValue;

	public string MPHostLobbyname = "";

	public DateTime multiplayerMapRequestTime = DateTime.MinValue;

	public DateTime lastAutoRefreshTime = DateTime.MinValue;

	public int MPLobbyMode;

	public int MPGameType;

	public int MPStartingSettings;

	public int ExtremeWarningSource;

	public ulong LatestSharedCode;

	public bool ShowSharingCode;

	public DateTime justEnteredSetupScreen = DateTime.MinValue;

	public DateTime lastSettingsRefresh = DateTime.MinValue;

	public int PlayerCap = 8;

	public int[] team_order = new int[9];

	public int SelectedRadarKeep = -1;

	public int SelectedFace = -1;

	public bool teampop_sultan_played;

	public bool teampop_rat_played;

	public bool showLobbyUnavailableMessage;

	public bool justEnteredSetup;

	public bool playKickSpeech = true;

	public int humanPlayerCount = -1;

	public DateTime nextTimeTeamSpeech = DateTime.UtcNow.AddSeconds(5.0);

	public DateTime hideToolTipTime = DateTime.MinValue;

	public bool closePanelDisplayed;

	public bool skirmishExtremeTroopsWarningShown;

	public bool lobbyChatRefreshPending;

	public DateTime lobbyChatRefreshTime = DateTime.MaxValue;

	public PlayerRow[] playerRows = new PlayerRow[8];

	public bool lastCanStart;

	public MPAIVInfo[] AIVs;

	public static readonly int[] MP_orig_remap_colour_order = new int[9] { 0, 1, 3, 4, 2, 6, 5, 7, 8 };

	public readonly string[] KickPlayerSpeech = new string[28]
	{
		"all_kick_player_01.wav", "rt_kick_player.wav", "sn_kick_player.wav", "pg_kick_player.wav", "wf_kick_player.wav", "sa_kick_player_01.wav", "ca_kick_player_01.wav", "su_kick_player_01.wav", "ri_kick_player_01.wav", "fr_kick_player_01.wav",
		"ph_kick_player_01.wav", "wa_kick_player_01.wav", "em_kick_player_01.wav", "ni_kick_player_01.wav", "sh_kick_player_01.wav", "ma_kick_player_01.wav", "ab_kick_player_01.wav", "je_kick_player_01.wav", "se_kick_player_01.wav", "no_kick_player_01.wav",
		"ka_kick_player_01.wav", "cn_kick_player_01.wav", "tr_kick_player_01.wav", "sg_kick_player_01.wav", "li_kick_player_01.wav", "cr_kick_player_01.wav", "ba_kick_player_01.wav", "bu_kick_player_01.wav"
	};

	public readonly string[] AddPlayerSpeech = new string[28]
	{
		"all_add_player_01.wav", "rt_add_player.wav", "sn_add_player.wav", "pg_add_player.wav", "wf_add_player.wav", "sa_add_player_01.wav", "ca_add_player_01.wav", "su_add_player_01.wav", "ri_add_player_01.wav", "fr_add_player_01.wav",
		"ph_add_player_01.wav", "wa_add_player_01.wav", "em_add_player_01.wav", "ni_add_player_01.wav", "sh_add_player_01.wav", "ma_add_player_01.wav", "ab_add_player_01.wav", "je_add_player_01.wav", "se_add_player_01.wav", "no_add_player_01.wav",
		"ka_add_player_01.wav", "cn_add_player_01.wav", "tr_add_player_01.wav", "sg_add_player_01.wav", "li_add_player_01.wav", "cr_add_player_01.wav", "ba_add_player_01.wav", "bu_add_player_01.wav"
	};

	public List<LobbyChatEntry> lobbyChat = new List<LobbyChatEntry>();

	public ListView RefFileLists;

	public CheckBox RefIncludeUser;

	public CheckBox RefIncludeBuiltin;

	public CheckBox RefIncludeWorkshop;

	public bool panelActive;

	public static bool skirmishGame = false;

	public static bool coopGame = false;

	public static bool coopGame_IsHost = false;

	public static int coopGame_ClientSelectedMission = -1;

	public bool pendingCoopWaitingPanel;

	public static bool customCoopGame = false;

	public Platform_Multiplayer.MPLobby coopPendingLobby;

	public static bool customizedTrail = false;

	public static int customizedTrailType = -1;

	public static int customizedTrailID = -1;

	public static bool spectatorMode = false;

	public bool extremeTrailCustomised;

	public ObservableCollection<FileRow> fileRows = new ObservableCollection<FileRow>();

	public ObservableCollection<FileRow> lobbyRows = new ObservableCollection<FileRow>();

	public ObservableCollection<FileRow> customLordRows = new ObservableCollection<FileRow>();

	public bool ignoreSelectRefresh;

	public List<FileHeader> headerlist;

	public bool insideValueChanged;

	public DateTime lastScrollTest = DateTime.MinValue;

	public DateTime startGameTime = DateTime.MinValue;

	public DateTime AILordTextClear = DateTime.MinValue;

	public int[,] start_few_troop_level = new int[5, 10]
	{
		{ 2, 0, 2, 0, 0, 0, 0, 0, 0, 0 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
		{ 2, 0, 2, 0, 0, 0, 0, 0, 0, 0 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
		{ 2, 0, 2, 0, 0, 0, 0, 0, 0, 0 }
	};

	public int[,] start_some_troop_level = new int[5, 10]
	{
		{ 3, 0, 3, 0, 0, 0, 0, 0, 0, 0 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
		{ 3, 0, 3, 0, 0, 0, 0, 0, 0, 0 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
		{ 8, 4, 0, 6, 0, 4, 4, 0, 3, 4 }
	};

	public int[,] start_many_troop_level = new int[5, 10]
	{
		{ 6, 0, 6, 0, 0, 0, 0, 0, 1, 0 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
		{ 8, 0, 8, 0, 4, 0, 0, 0, 3, 0 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
		{ 10, 6, 0, 8, 0, 6, 6, 0, 5, 6 }
	};

	public int[,] start_low_goods_level = new int[5, 20]
	{
		{
			100, 0, 50, 0, 0, 0, 0, 25, 0, 25,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0
		},
		{
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0
		},
		{
			100, 0, 100, 0, 0, 0, 0, 25, 0, 25,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0
		},
		{
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0
		},
		{
			100, 0, 100, 0, 0, 0, 0, 25, 0, 25,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0
		}
	};

	public int[,] start_med_goods_level = new int[5, 20]
	{
		{
			100, 0, 100, 0, 0, 0, 0, 50, 0, 50,
			0, 2, 0, 2, 0, 6, 0, 6, 0, 0
		},
		{
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0
		},
		{
			100, 0, 100, 0, 0, 0, 0, 50, 0, 50,
			0, 2, 0, 2, 0, 0, 0, 0, 0, 0
		},
		{
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0
		},
		{
			96, 0, 96, 24, 8, 16, 35, 15, 15, 15,
			8, 6, 4, 6, 4, 4, 4, 8, 8, 0
		}
	};

	public int[,] start_high_goods_level = new int[5, 20]
	{
		{
			120, 0, 22, 0, 0, 0, 0, 80, 0, 80,
			0, 4, 0, 4, 0, 0, 0, 0, 0, 0
		},
		{
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0
		},
		{
			120, 0, 96, 48, 16, 0, 0, 25, 0, 50,
			0, 8, 4, 8, 0, 4, 0, 8, 0, 0
		},
		{
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0
		},
		{
			120, 0, 192, 48, 16, 32, 40, 20, 0, 20,
			16, 8, 4, 4, 6, 6, 3, 10, 9, 0
		}
	};

	public Platform_Multiplayer.MPLobbyMember[] orderTeamMembers = new Platform_Multiplayer.MPLobbyMember[8];

	public Platform_Multiplayer.MPLobbyMember selectedTeamMember;

	public static CoopMissionSetupData[] CoopTrail1 = null;

	public static CoopMissionSetupData[] CoopTrail2 = null;

	public static CoopMissionSetupData[] CoopTrail3 = null;

	public Queue<AvatarCallback> avatarCallbacks = new Queue<AvatarCallback>();

	public int coopFriendsPage;

	public bool coopShowHiddenFriends;

	public ulong coopHiddenSelectedSteamID;

	public const int coopFriendsPageSize = 8;

	public ulong[] coopFriendsSteamIDs = new ulong[8];

	public bool[] coopFriendsRowHidden = new bool[8];

	public Avatars.AvatarDesign tempAD = new Avatars.AvatarDesign();

	public static void SetVisibility(UIElement element, Visibility state)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		if (element.Visibility != state)
		{
			element.Visibility = state;
		}
	}

	public static void SetButtonVisibility(UIElement element, Visibility state)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		if (PropEx.GetButtonVisibility(element) != state)
		{
			PropEx.SetButtonVisibility(element, state);
		}
	}

	public static ImageSource GetColourShield(int colourID, int state = 0, bool ingameRemap = false)
	{
		if (colourID < 0 || colourID >= MP_orig_remap_colour_order.Length)
		{
			return null;
		}
		colourID = ((!ingameRemap) ? MP_orig_remap_colour_order[colourID] : SpriteMapping.remapColours[SpriteMapping.RemapMPLoadedColour(colourID)]);
		switch (state)
		{
		case 0:
			switch (colourID)
			{
			case 1:
				return MainViewModel.Instance.GameSprites[110];
			case 2:
				return MainViewModel.Instance.GameSprites[107];
			case 3:
				return MainViewModel.Instance.GameSprites[108];
			case 4:
				return MainViewModel.Instance.GameSprites[109];
			case 5:
				return MainViewModel.Instance.GameSprites[112];
			case 6:
				return MainViewModel.Instance.GameSprites[111];
			case 7:
				return MainViewModel.Instance.GameSprites[113];
			case 8:
				return MainViewModel.Instance.GameSprites[114];
			}
			break;
		case 1:
			switch (colourID)
			{
			case 1:
				return MainViewModel.Instance.GameSprites[355];
			case 2:
				return MainViewModel.Instance.GameSprites[352];
			case 3:
				return MainViewModel.Instance.GameSprites[353];
			case 4:
				return MainViewModel.Instance.GameSprites[354];
			case 5:
				return MainViewModel.Instance.GameSprites[357];
			case 6:
				return MainViewModel.Instance.GameSprites[356];
			case 7:
				return MainViewModel.Instance.GameSprites[358];
			case 8:
				return MainViewModel.Instance.GameSprites[359];
			}
			break;
		case 2:
			switch (colourID)
			{
			case 1:
				return MainViewModel.Instance.GameSprites[363];
			case 2:
				return MainViewModel.Instance.GameSprites[360];
			case 3:
				return MainViewModel.Instance.GameSprites[361];
			case 4:
				return MainViewModel.Instance.GameSprites[362];
			case 5:
				return MainViewModel.Instance.GameSprites[365];
			case 6:
				return MainViewModel.Instance.GameSprites[364];
			case 7:
				return MainViewModel.Instance.GameSprites[366];
			case 8:
				return MainViewModel.Instance.GameSprites[367];
			}
			break;
		}
		return null;
	}

	public bool CoopPendingReadyHost()
	{
		return coopPendingLobby != null;
	}

	public FRONT_Multiplayer()
	{
		//IL_0446: Unknown result type (might be due to invalid IL or missing references)
		//IL_0450: Expected O, but got Unknown
		//IL_045c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0466: Expected O, but got Unknown
		//IL_0472: Unknown result type (might be due to invalid IL or missing references)
		//IL_047c: Expected O, but got Unknown
		//IL_0488: Unknown result type (might be due to invalid IL or missing references)
		//IL_0492: Expected O, but got Unknown
		//IL_049e: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a8: Expected O, but got Unknown
		//IL_04cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d5: Expected O, but got Unknown
		//IL_04e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04eb: Expected O, but got Unknown
		//IL_04f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0501: Expected O, but got Unknown
		//IL_050d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0517: Expected O, but got Unknown
		//IL_0523: Unknown result type (might be due to invalid IL or missing references)
		//IL_052d: Expected O, but got Unknown
		//IL_0539: Unknown result type (might be due to invalid IL or missing references)
		//IL_0543: Expected O, but got Unknown
		//IL_054f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0559: Expected O, but got Unknown
		//IL_0565: Unknown result type (might be due to invalid IL or missing references)
		//IL_056f: Expected O, but got Unknown
		//IL_057b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0585: Expected O, but got Unknown
		//IL_0591: Unknown result type (might be due to invalid IL or missing references)
		//IL_059b: Expected O, but got Unknown
		//IL_05a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b1: Expected O, but got Unknown
		//IL_05bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c7: Expected O, but got Unknown
		//IL_05d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_05dd: Expected O, but got Unknown
		//IL_05e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f3: Expected O, but got Unknown
		//IL_05ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0609: Expected O, but got Unknown
		//IL_0615: Unknown result type (might be due to invalid IL or missing references)
		//IL_061f: Expected O, but got Unknown
		//IL_062b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0635: Expected O, but got Unknown
		//IL_0641: Unknown result type (might be due to invalid IL or missing references)
		//IL_064b: Expected O, but got Unknown
		//IL_0657: Unknown result type (might be due to invalid IL or missing references)
		//IL_0661: Expected O, but got Unknown
		//IL_066d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0677: Expected O, but got Unknown
		//IL_0683: Unknown result type (might be due to invalid IL or missing references)
		//IL_068d: Expected O, but got Unknown
		//IL_0699: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a3: Expected O, but got Unknown
		//IL_06af: Unknown result type (might be due to invalid IL or missing references)
		//IL_06b9: Expected O, but got Unknown
		//IL_06c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_06cf: Expected O, but got Unknown
		//IL_06db: Unknown result type (might be due to invalid IL or missing references)
		//IL_06e5: Expected O, but got Unknown
		//IL_06f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_06fb: Expected O, but got Unknown
		//IL_0707: Unknown result type (might be due to invalid IL or missing references)
		//IL_0711: Expected O, but got Unknown
		//IL_071d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0727: Expected O, but got Unknown
		//IL_0733: Unknown result type (might be due to invalid IL or missing references)
		//IL_073d: Expected O, but got Unknown
		//IL_0749: Unknown result type (might be due to invalid IL or missing references)
		//IL_0753: Expected O, but got Unknown
		//IL_075f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0769: Expected O, but got Unknown
		//IL_0775: Unknown result type (might be due to invalid IL or missing references)
		//IL_077f: Expected O, but got Unknown
		//IL_078b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0795: Expected O, but got Unknown
		//IL_07a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ab: Expected O, but got Unknown
		//IL_07b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_07c1: Expected O, but got Unknown
		//IL_07cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_07d7: Expected O, but got Unknown
		//IL_07e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ee: Expected O, but got Unknown
		//IL_07fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0804: Expected O, but got Unknown
		//IL_0811: Unknown result type (might be due to invalid IL or missing references)
		//IL_081b: Expected O, but got Unknown
		//IL_0828: Unknown result type (might be due to invalid IL or missing references)
		//IL_0832: Expected O, but got Unknown
		//IL_083e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0848: Expected O, but got Unknown
		//IL_0854: Unknown result type (might be due to invalid IL or missing references)
		//IL_085e: Expected O, but got Unknown
		//IL_086a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0874: Expected O, but got Unknown
		//IL_0881: Unknown result type (might be due to invalid IL or missing references)
		//IL_088b: Expected O, but got Unknown
		//IL_0898: Unknown result type (might be due to invalid IL or missing references)
		//IL_08a2: Expected O, but got Unknown
		//IL_08af: Unknown result type (might be due to invalid IL or missing references)
		//IL_08b9: Expected O, but got Unknown
		//IL_08c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_08d0: Expected O, but got Unknown
		//IL_08dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_08e6: Expected O, but got Unknown
		//IL_08f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_08fd: Expected O, but got Unknown
		//IL_090a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0914: Expected O, but got Unknown
		//IL_0920: Unknown result type (might be due to invalid IL or missing references)
		//IL_092a: Expected O, but got Unknown
		//IL_0956: Unknown result type (might be due to invalid IL or missing references)
		//IL_0960: Expected O, but got Unknown
		//IL_0973: Unknown result type (might be due to invalid IL or missing references)
		//IL_097d: Expected O, but got Unknown
		//IL_0990: Unknown result type (might be due to invalid IL or missing references)
		//IL_099a: Expected O, but got Unknown
		//IL_09ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_09b7: Expected O, but got Unknown
		//IL_09ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_09d4: Expected O, but got Unknown
		//IL_09e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_09f1: Expected O, but got Unknown
		//IL_0a04: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a0e: Expected O, but got Unknown
		//IL_0a21: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a2b: Expected O, but got Unknown
		//IL_0a3e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a48: Expected O, but got Unknown
		//IL_0a5b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a65: Expected O, but got Unknown
		//IL_0a78: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a82: Expected O, but got Unknown
		//IL_0a95: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a9f: Expected O, but got Unknown
		//IL_0ab2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0abc: Expected O, but got Unknown
		//IL_0acf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ad9: Expected O, but got Unknown
		//IL_0aec: Unknown result type (might be due to invalid IL or missing references)
		//IL_0af6: Expected O, but got Unknown
		//IL_0b09: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b13: Expected O, but got Unknown
		//IL_0b26: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b30: Expected O, but got Unknown
		//IL_0b43: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b4d: Expected O, but got Unknown
		//IL_0b60: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b6a: Expected O, but got Unknown
		//IL_0b7d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b87: Expected O, but got Unknown
		//IL_0b9a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ba4: Expected O, but got Unknown
		//IL_0bb7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bc1: Expected O, but got Unknown
		//IL_0bd4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bde: Expected O, but got Unknown
		//IL_0bf1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bfb: Expected O, but got Unknown
		//IL_0c0e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c18: Expected O, but got Unknown
		//IL_0c2b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c35: Expected O, but got Unknown
		//IL_0c48: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c52: Expected O, but got Unknown
		//IL_0c65: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c6f: Expected O, but got Unknown
		//IL_0c82: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c8c: Expected O, but got Unknown
		//IL_0c9f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ca9: Expected O, but got Unknown
		//IL_0cbc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cc6: Expected O, but got Unknown
		//IL_0cd9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ce3: Expected O, but got Unknown
		//IL_0cf6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d00: Expected O, but got Unknown
		//IL_0d13: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d1d: Expected O, but got Unknown
		//IL_0d30: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d3a: Expected O, but got Unknown
		//IL_0d4d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d57: Expected O, but got Unknown
		//IL_0d6a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d74: Expected O, but got Unknown
		//IL_0d87: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d91: Expected O, but got Unknown
		//IL_0da4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dae: Expected O, but got Unknown
		//IL_0dc1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dcb: Expected O, but got Unknown
		//IL_0dde: Unknown result type (might be due to invalid IL or missing references)
		//IL_0de8: Expected O, but got Unknown
		//IL_0dfb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e05: Expected O, but got Unknown
		//IL_0e18: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e22: Expected O, but got Unknown
		//IL_0e35: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e3f: Expected O, but got Unknown
		//IL_0e52: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e5c: Expected O, but got Unknown
		//IL_0e6f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e79: Expected O, but got Unknown
		//IL_0e8c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e96: Expected O, but got Unknown
		//IL_0ea9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0eb3: Expected O, but got Unknown
		//IL_0ec6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ed0: Expected O, but got Unknown
		//IL_0ee3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0eed: Expected O, but got Unknown
		//IL_0f00: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f0a: Expected O, but got Unknown
		//IL_0f1d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f27: Expected O, but got Unknown
		//IL_0f3a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f44: Expected O, but got Unknown
		//IL_0f57: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f61: Expected O, but got Unknown
		//IL_0f74: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f7e: Expected O, but got Unknown
		//IL_0f91: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f9b: Expected O, but got Unknown
		//IL_0fae: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fb8: Expected O, but got Unknown
		//IL_0fcb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fd5: Expected O, but got Unknown
		//IL_0fe8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ff2: Expected O, but got Unknown
		//IL_1005: Unknown result type (might be due to invalid IL or missing references)
		//IL_100f: Expected O, but got Unknown
		//IL_1022: Unknown result type (might be due to invalid IL or missing references)
		//IL_102c: Expected O, but got Unknown
		//IL_103f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1049: Expected O, but got Unknown
		//IL_105c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1066: Expected O, but got Unknown
		//IL_1079: Unknown result type (might be due to invalid IL or missing references)
		//IL_1083: Expected O, but got Unknown
		//IL_1096: Unknown result type (might be due to invalid IL or missing references)
		//IL_10a0: Expected O, but got Unknown
		//IL_10b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_10bd: Expected O, but got Unknown
		//IL_10d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_10da: Expected O, but got Unknown
		//IL_10ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_10f7: Expected O, but got Unknown
		//IL_110a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1114: Expected O, but got Unknown
		//IL_1127: Unknown result type (might be due to invalid IL or missing references)
		//IL_1131: Expected O, but got Unknown
		//IL_1144: Unknown result type (might be due to invalid IL or missing references)
		//IL_114e: Expected O, but got Unknown
		//IL_1161: Unknown result type (might be due to invalid IL or missing references)
		//IL_116b: Expected O, but got Unknown
		//IL_1177: Unknown result type (might be due to invalid IL or missing references)
		//IL_1181: Expected O, but got Unknown
		//IL_118e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1198: Expected O, but got Unknown
		//IL_11a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_11af: Expected O, but got Unknown
		//IL_11bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_11c5: Expected O, but got Unknown
		//IL_11d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_11dc: Expected O, but got Unknown
		//IL_11e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_11f3: Expected O, but got Unknown
		//IL_11ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_1209: Expected O, but got Unknown
		//IL_1216: Unknown result type (might be due to invalid IL or missing references)
		//IL_1220: Expected O, but got Unknown
		//IL_122d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1237: Expected O, but got Unknown
		//IL_1243: Unknown result type (might be due to invalid IL or missing references)
		//IL_124d: Expected O, but got Unknown
		//IL_1259: Unknown result type (might be due to invalid IL or missing references)
		//IL_1263: Expected O, but got Unknown
		//IL_126f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1279: Expected O, but got Unknown
		//IL_1285: Unknown result type (might be due to invalid IL or missing references)
		//IL_128f: Expected O, but got Unknown
		//IL_129b: Unknown result type (might be due to invalid IL or missing references)
		//IL_12a5: Expected O, but got Unknown
		//IL_12b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_12bb: Expected O, but got Unknown
		//IL_12c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_12d1: Expected O, but got Unknown
		//IL_12dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_12e7: Expected O, but got Unknown
		//IL_12f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_12fd: Expected O, but got Unknown
		//IL_130a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1314: Expected O, but got Unknown
		//IL_1321: Unknown result type (might be due to invalid IL or missing references)
		//IL_132b: Expected O, but got Unknown
		//IL_1337: Unknown result type (might be due to invalid IL or missing references)
		//IL_1341: Expected O, but got Unknown
		//IL_134e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1358: Expected O, but got Unknown
		//IL_1365: Unknown result type (might be due to invalid IL or missing references)
		//IL_136f: Expected O, but got Unknown
		//IL_137b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1385: Expected O, but got Unknown
		//IL_1392: Unknown result type (might be due to invalid IL or missing references)
		//IL_139c: Expected O, but got Unknown
		//IL_13a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_13b3: Expected O, but got Unknown
		//IL_13bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_13c9: Expected O, but got Unknown
		//IL_13d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_13e0: Expected O, but got Unknown
		//IL_13ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_13f7: Expected O, but got Unknown
		//IL_1403: Unknown result type (might be due to invalid IL or missing references)
		//IL_140d: Expected O, but got Unknown
		//IL_141a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1424: Expected O, but got Unknown
		//IL_1431: Unknown result type (might be due to invalid IL or missing references)
		//IL_143b: Expected O, but got Unknown
		//IL_1447: Unknown result type (might be due to invalid IL or missing references)
		//IL_1451: Expected O, but got Unknown
		//IL_145e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1468: Expected O, but got Unknown
		//IL_1475: Unknown result type (might be due to invalid IL or missing references)
		//IL_147f: Expected O, but got Unknown
		//IL_148b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1495: Expected O, but got Unknown
		//IL_14a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_14ac: Expected O, but got Unknown
		//IL_14b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_14c3: Expected O, but got Unknown
		//IL_14cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_14d9: Expected O, but got Unknown
		//IL_14e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_14f0: Expected O, but got Unknown
		//IL_14fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_1507: Expected O, but got Unknown
		//IL_1513: Unknown result type (might be due to invalid IL or missing references)
		//IL_151d: Expected O, but got Unknown
		//IL_1529: Unknown result type (might be due to invalid IL or missing references)
		//IL_1533: Expected O, but got Unknown
		//IL_153f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1549: Expected O, but got Unknown
		//IL_1555: Unknown result type (might be due to invalid IL or missing references)
		//IL_155f: Expected O, but got Unknown
		//IL_156b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1575: Expected O, but got Unknown
		//IL_1581: Unknown result type (might be due to invalid IL or missing references)
		//IL_158b: Expected O, but got Unknown
		//IL_1597: Unknown result type (might be due to invalid IL or missing references)
		//IL_15a1: Expected O, but got Unknown
		//IL_15ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_15b7: Expected O, but got Unknown
		//IL_15c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_15cd: Expected O, but got Unknown
		//IL_15da: Unknown result type (might be due to invalid IL or missing references)
		//IL_15e4: Expected O, but got Unknown
		//IL_15f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_15fb: Expected O, but got Unknown
		//IL_1607: Unknown result type (might be due to invalid IL or missing references)
		//IL_1611: Expected O, but got Unknown
		//IL_161e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1628: Expected O, but got Unknown
		//IL_1635: Unknown result type (might be due to invalid IL or missing references)
		//IL_163f: Expected O, but got Unknown
		//IL_164b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1655: Expected O, but got Unknown
		//IL_1662: Unknown result type (might be due to invalid IL or missing references)
		//IL_166c: Expected O, but got Unknown
		//IL_1679: Unknown result type (might be due to invalid IL or missing references)
		//IL_1683: Expected O, but got Unknown
		//IL_168f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1699: Expected O, but got Unknown
		//IL_16a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_16b0: Expected O, but got Unknown
		//IL_16bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_16c7: Expected O, but got Unknown
		//IL_16d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_16dd: Expected O, but got Unknown
		//IL_16ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_16f4: Expected O, but got Unknown
		//IL_1701: Unknown result type (might be due to invalid IL or missing references)
		//IL_170b: Expected O, but got Unknown
		//IL_1717: Unknown result type (might be due to invalid IL or missing references)
		//IL_1721: Expected O, but got Unknown
		//IL_172e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1738: Expected O, but got Unknown
		//IL_1745: Unknown result type (might be due to invalid IL or missing references)
		//IL_174f: Expected O, but got Unknown
		//IL_175b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1765: Expected O, but got Unknown
		//IL_1772: Unknown result type (might be due to invalid IL or missing references)
		//IL_177c: Expected O, but got Unknown
		//IL_1789: Unknown result type (might be due to invalid IL or missing references)
		//IL_1793: Expected O, but got Unknown
		//IL_179f: Unknown result type (might be due to invalid IL or missing references)
		//IL_17a9: Expected O, but got Unknown
		//IL_17b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_17c0: Expected O, but got Unknown
		//IL_17cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_17d7: Expected O, but got Unknown
		//IL_17e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_17ed: Expected O, but got Unknown
		//IL_17fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_1804: Expected O, but got Unknown
		//IL_1811: Unknown result type (might be due to invalid IL or missing references)
		//IL_181b: Expected O, but got Unknown
		//IL_1827: Unknown result type (might be due to invalid IL or missing references)
		//IL_1831: Expected O, but got Unknown
		//IL_183d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1847: Expected O, but got Unknown
		//IL_1853: Unknown result type (might be due to invalid IL or missing references)
		//IL_185d: Expected O, but got Unknown
		//IL_186a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1874: Expected O, but got Unknown
		//IL_1880: Unknown result type (might be due to invalid IL or missing references)
		//IL_188a: Expected O, but got Unknown
		//IL_1896: Unknown result type (might be due to invalid IL or missing references)
		//IL_18a0: Expected O, but got Unknown
		//IL_18ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_18b6: Expected O, but got Unknown
		//IL_18c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_18cc: Expected O, but got Unknown
		//IL_18d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_18e3: Expected O, but got Unknown
		//IL_18f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_18fa: Expected O, but got Unknown
		//IL_1906: Unknown result type (might be due to invalid IL or missing references)
		//IL_1910: Expected O, but got Unknown
		//IL_191d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1927: Expected O, but got Unknown
		//IL_1934: Unknown result type (might be due to invalid IL or missing references)
		//IL_193e: Expected O, but got Unknown
		//IL_194a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1954: Expected O, but got Unknown
		//IL_1960: Unknown result type (might be due to invalid IL or missing references)
		//IL_196a: Expected O, but got Unknown
		//IL_19a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_19ae: Expected O, but got Unknown
		//IL_19ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_19c4: Expected O, but got Unknown
		//IL_19fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a08: Expected O, but got Unknown
		//IL_1a14: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a1e: Expected O, but got Unknown
		//IL_1a2a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a34: Expected O, but got Unknown
		//IL_1a40: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a4a: Expected O, but got Unknown
		//IL_1a56: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a60: Expected O, but got Unknown
		//IL_1a6c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a76: Expected O, but got Unknown
		//IL_1a82: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a8c: Expected O, but got Unknown
		//IL_1a98: Unknown result type (might be due to invalid IL or missing references)
		//IL_1aa2: Expected O, but got Unknown
		//IL_1aae: Unknown result type (might be due to invalid IL or missing references)
		//IL_1ab8: Expected O, but got Unknown
		//IL_1ac5: Unknown result type (might be due to invalid IL or missing references)
		//IL_1acf: Expected O, but got Unknown
		//IL_1adb: Unknown result type (might be due to invalid IL or missing references)
		//IL_1ae5: Expected O, but got Unknown
		//IL_1af0: Unknown result type (might be due to invalid IL or missing references)
		//IL_1af5: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b06: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b0b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b26: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b30: Expected O, but got Unknown
		//IL_1b30: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b41: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b46: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b61: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b6b: Expected O, but got Unknown
		//IL_1b6b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b7c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b81: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b93: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b9d: Expected O, but got Unknown
		//IL_1b9d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1bae: Unknown result type (might be due to invalid IL or missing references)
		//IL_1bb3: Unknown result type (might be due to invalid IL or missing references)
		//IL_1bc5: Unknown result type (might be due to invalid IL or missing references)
		//IL_1bcf: Expected O, but got Unknown
		//IL_1bcf: Unknown result type (might be due to invalid IL or missing references)
		//IL_1be0: Unknown result type (might be due to invalid IL or missing references)
		//IL_1be5: Unknown result type (might be due to invalid IL or missing references)
		//IL_1bf7: Unknown result type (might be due to invalid IL or missing references)
		//IL_1c01: Expected O, but got Unknown
		//IL_1c11: Unknown result type (might be due to invalid IL or missing references)
		//IL_1c16: Unknown result type (might be due to invalid IL or missing references)
		//IL_1c28: Unknown result type (might be due to invalid IL or missing references)
		//IL_1c32: Expected O, but got Unknown
		//IL_1c3f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1c49: Expected O, but got Unknown
		//IL_1c56: Unknown result type (might be due to invalid IL or missing references)
		//IL_1c60: Expected O, but got Unknown
		//IL_1c6b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1c70: Unknown result type (might be due to invalid IL or missing references)
		//IL_1c81: Unknown result type (might be due to invalid IL or missing references)
		//IL_1c86: Unknown result type (might be due to invalid IL or missing references)
		//IL_1ca4: Unknown result type (might be due to invalid IL or missing references)
		//IL_1cae: Expected O, but got Unknown
		//IL_1cae: Unknown result type (might be due to invalid IL or missing references)
		//IL_1cbf: Unknown result type (might be due to invalid IL or missing references)
		//IL_1cc4: Unknown result type (might be due to invalid IL or missing references)
		//IL_1ce2: Unknown result type (might be due to invalid IL or missing references)
		//IL_1cec: Expected O, but got Unknown
		//IL_1cfc: Unknown result type (might be due to invalid IL or missing references)
		//IL_1d01: Unknown result type (might be due to invalid IL or missing references)
		//IL_1d1f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1d29: Expected O, but got Unknown
		//IL_1d36: Unknown result type (might be due to invalid IL or missing references)
		//IL_1d40: Expected O, but got Unknown
		//IL_1d4d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1d57: Expected O, but got Unknown
		//IL_1df5: Unknown result type (might be due to invalid IL or missing references)
		//IL_1e19: Unknown result type (might be due to invalid IL or missing references)
		MainViewModel.Instance.FRONTMultiplayer = this;
		InitializeComponent();
		pulseAnimation = (Storyboard)((FrameworkElement)this).TryFindResource((object)"ReadyButtonAnim");
		settingsPulseAnimation = (Storyboard)((FrameworkElement)this).TryFindResource((object)"SettingsButtonAnim");
		RefFileLists = (ListView)((FrameworkElement)this).FindName("MapList");
		RefLobbyLists = (ListView)((FrameworkElement)this).FindName("LobbyList");
		RefLobbyMaxPlayersSlider = (Slider)((FrameworkElement)this).FindName("LobbyMaxPlayersSlider");
		((RangeBase)RefLobbyMaxPlayersSlider).ValueChanged += LobbyMaxPlayersSlider_ValueChanged;
		RefHeaderBar = (Grid)((FrameworkElement)this).FindName("HeaderBar");
		RefJoinButton = (Button)((FrameworkElement)this).FindName("JoinButton");
		RefLobbySettingsButton = (Button)((FrameworkElement)this).FindName("LobbySettingsButton");
		RefMultiplayerPlayButton = (Button)((FrameworkElement)this).FindName("MultiplayerPlayButton");
		RefReadyButton = (Button)((FrameworkElement)this).FindName("ReadyButton");
		RefReadyButtonLock = (Button)((FrameworkElement)this).FindName("ReadyButtonLock");
		RefLoadButton = (Button)((FrameworkElement)this).FindName("LoadButton");
		RefColourSelectButton = (Button)((FrameworkElement)this).FindName("ColourSelectButton");
		RefColShield1 = (Button)((FrameworkElement)this).FindName("ColShield1");
		RefColShield2 = (Button)((FrameworkElement)this).FindName("ColShield2");
		RefColShield3 = (Button)((FrameworkElement)this).FindName("ColShield3");
		RefColShield4 = (Button)((FrameworkElement)this).FindName("ColShield4");
		RefColShield5 = (Button)((FrameworkElement)this).FindName("ColShield5");
		RefColShield6 = (Button)((FrameworkElement)this).FindName("ColShield6");
		RefColShield7 = (Button)((FrameworkElement)this).FindName("ColShield7");
		RefColShield8 = (Button)((FrameworkElement)this).FindName("ColShield8");
		RefRandomAI1 = (Button)((FrameworkElement)this).FindName("RandomAI1");
		RefRandomAI2 = (Button)((FrameworkElement)this).FindName("RandomAI2");
		RefRandomAI3 = (Button)((FrameworkElement)this).FindName("RandomAI3");
		RefRandomAI4 = (Button)((FrameworkElement)this).FindName("RandomAI4");
		RefRandomAI5 = (Button)((FrameworkElement)this).FindName("RandomAI5");
		RefRandomAI6 = (Button)((FrameworkElement)this).FindName("RandomAI6");
		RefRandomAI7 = (Button)((FrameworkElement)this).FindName("RandomAI7");
		RefMultiplayerSetupInfo = (Button)((FrameworkElement)this).FindName("MultiplayerSetupInfo");
		RefBasemap = (Image)((FrameworkElement)this).FindName("Basemap");
		RefFairness1 = (RadioButton)((FrameworkElement)this).FindName("Fairness1");
		RefFairness2 = (RadioButton)((FrameworkElement)this).FindName("Fairness2");
		RefFairness3 = (RadioButton)((FrameworkElement)this).FindName("Fairness3");
		RefFairness4 = (RadioButton)((FrameworkElement)this).FindName("Fairness4");
		RefFairness5 = (RadioButton)((FrameworkElement)this).FindName("Fairness5");
		RefGameType1 = (RadioButton)((FrameworkElement)this).FindName("GameType1");
		RefGameType2 = (RadioButton)((FrameworkElement)this).FindName("GameType2");
		RefGameType3 = (RadioButton)((FrameworkElement)this).FindName("GameType3");
		RefMultiplayerInvite = (Button)((FrameworkElement)this).FindName("MultiplayerInvite");
		RefMP_ChatSend = (Button)((FrameworkElement)this).FindName("MP_ChatSend");
		RefTextBoxGameName = (TextBox)((FrameworkElement)this).FindName("TextBoxGameName");
		((UIElement)RefTextBoxGameName).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		RefMP_ChatInput = (TextBox)((FrameworkElement)this).FindName("MP_ChatInput");
		((UIElement)RefMP_ChatInput).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		((UIElement)RefMP_ChatInput).PreviewKeyUp += new KeyEventHandler(DetectChatEnter);
		RefMP_ChatDisplay = (TextBlock)((FrameworkElement)this).FindName("MP_ChatDisplay");
		RefMP_ChatScrollView = (ScrollViewer)((FrameworkElement)this).FindName("MP_ChatScrollView");
		RefMP_SearchFilter = (TextBox)((FrameworkElement)this).FindName("MP_SearchFilter");
		((UIElement)RefMP_SearchFilter).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(FilterTextInputFocus);
		((TextBoxBase)RefMP_SearchFilter).TextChanged += new RoutedEventHandler(FilterTextChangedHandler);
		((UIElement)RefMP_SearchFilter).PreviewKeyDown += new KeyEventHandler(TextBoxCheckForEscape);
		((UIElement)RefMP_SearchFilter).PreviewTextInput += new TextCompositionEventHandler(TextBoxEnterCheck);
		RefMP_EnterShareCodeText = (TextBox)((FrameworkElement)this).FindName("MP_EnterShareCodeText");
		((UIElement)RefMP_EnterShareCodeText).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		((TextBoxBase)RefMP_EnterShareCodeText).TextChanged += new RoutedEventHandler(EnterShareTextChangedHandler);
		RefShareJoinButton = (Button)((FrameworkElement)this).FindName("ShareJoinButton");
		for (int i = 0; i < 8; i++)
		{
			playerRows[i] = new PlayerRow();
		}
		playerRows[0].RefRow = (Grid)((FrameworkElement)this).FindName("Player1_Row");
		playerRows[0].RefReadyState = (Image)((FrameworkElement)this).FindName("Player1_ReadyState");
		playerRows[0].RefColour = (Image)((FrameworkElement)this).FindName("Player1_Colour");
		playerRows[0].RefName = (TextBlock)((FrameworkElement)this).FindName("Player1_Name");
		playerRows[0].RefHost = (Image)((FrameworkElement)this).FindName("Player1_Host");
		playerRows[0].RefType = (TextBlock)((FrameworkElement)this).FindName("Player1_Type");
		playerRows[0].RefPing = (TextBlock)((FrameworkElement)this).FindName("Player1_Ping");
		playerRows[0].RefKick = (Button)((FrameworkElement)this).FindName("Player1_Kick");
		playerRows[0].RefAISettings = (Button)((FrameworkElement)this).FindName("Player1_AISettings");
		playerRows[1].RefRow = (Grid)((FrameworkElement)this).FindName("Player2_Row");
		playerRows[1].RefReadyState = (Image)((FrameworkElement)this).FindName("Player2_ReadyState");
		playerRows[1].RefColour = (Image)((FrameworkElement)this).FindName("Player2_Colour");
		playerRows[1].RefName = (TextBlock)((FrameworkElement)this).FindName("Player2_Name");
		playerRows[1].RefHost = (Image)((FrameworkElement)this).FindName("Player2_Host");
		playerRows[1].RefType = (TextBlock)((FrameworkElement)this).FindName("Player2_Type");
		playerRows[1].RefPing = (TextBlock)((FrameworkElement)this).FindName("Player2_Ping");
		playerRows[1].RefKick = (Button)((FrameworkElement)this).FindName("Player2_Kick");
		playerRows[1].RefAISettings = (Button)((FrameworkElement)this).FindName("Player2_AISettings");
		playerRows[2].RefRow = (Grid)((FrameworkElement)this).FindName("Player3_Row");
		playerRows[2].RefReadyState = (Image)((FrameworkElement)this).FindName("Player3_ReadyState");
		playerRows[2].RefColour = (Image)((FrameworkElement)this).FindName("Player3_Colour");
		playerRows[2].RefName = (TextBlock)((FrameworkElement)this).FindName("Player3_Name");
		playerRows[2].RefHost = (Image)((FrameworkElement)this).FindName("Player3_Host");
		playerRows[2].RefType = (TextBlock)((FrameworkElement)this).FindName("Player3_Type");
		playerRows[2].RefPing = (TextBlock)((FrameworkElement)this).FindName("Player3_Ping");
		playerRows[2].RefKick = (Button)((FrameworkElement)this).FindName("Player3_Kick");
		playerRows[2].RefAISettings = (Button)((FrameworkElement)this).FindName("Player3_AISettings");
		playerRows[3].RefRow = (Grid)((FrameworkElement)this).FindName("Player4_Row");
		playerRows[3].RefReadyState = (Image)((FrameworkElement)this).FindName("Player4_ReadyState");
		playerRows[3].RefColour = (Image)((FrameworkElement)this).FindName("Player4_Colour");
		playerRows[3].RefName = (TextBlock)((FrameworkElement)this).FindName("Player4_Name");
		playerRows[3].RefHost = (Image)((FrameworkElement)this).FindName("Player4_Host");
		playerRows[3].RefType = (TextBlock)((FrameworkElement)this).FindName("Player4_Type");
		playerRows[3].RefPing = (TextBlock)((FrameworkElement)this).FindName("Player4_Ping");
		playerRows[3].RefKick = (Button)((FrameworkElement)this).FindName("Player4_Kick");
		playerRows[3].RefAISettings = (Button)((FrameworkElement)this).FindName("Player4_AISettings");
		playerRows[4].RefRow = (Grid)((FrameworkElement)this).FindName("Player5_Row");
		playerRows[4].RefReadyState = (Image)((FrameworkElement)this).FindName("Player5_ReadyState");
		playerRows[4].RefColour = (Image)((FrameworkElement)this).FindName("Player5_Colour");
		playerRows[4].RefName = (TextBlock)((FrameworkElement)this).FindName("Player5_Name");
		playerRows[4].RefHost = (Image)((FrameworkElement)this).FindName("Player5_Host");
		playerRows[4].RefType = (TextBlock)((FrameworkElement)this).FindName("Player5_Type");
		playerRows[4].RefPing = (TextBlock)((FrameworkElement)this).FindName("Player5_Ping");
		playerRows[4].RefKick = (Button)((FrameworkElement)this).FindName("Player5_Kick");
		playerRows[4].RefAISettings = (Button)((FrameworkElement)this).FindName("Player5_AISettings");
		playerRows[5].RefRow = (Grid)((FrameworkElement)this).FindName("Player6_Row");
		playerRows[5].RefReadyState = (Image)((FrameworkElement)this).FindName("Player6_ReadyState");
		playerRows[5].RefColour = (Image)((FrameworkElement)this).FindName("Player6_Colour");
		playerRows[5].RefName = (TextBlock)((FrameworkElement)this).FindName("Player6_Name");
		playerRows[5].RefHost = (Image)((FrameworkElement)this).FindName("Player6_Host");
		playerRows[5].RefType = (TextBlock)((FrameworkElement)this).FindName("Player6_Type");
		playerRows[5].RefPing = (TextBlock)((FrameworkElement)this).FindName("Player6_Ping");
		playerRows[5].RefKick = (Button)((FrameworkElement)this).FindName("Player6_Kick");
		playerRows[5].RefAISettings = (Button)((FrameworkElement)this).FindName("Player6_AISettings");
		playerRows[6].RefRow = (Grid)((FrameworkElement)this).FindName("Player7_Row");
		playerRows[6].RefReadyState = (Image)((FrameworkElement)this).FindName("Player7_ReadyState");
		playerRows[6].RefColour = (Image)((FrameworkElement)this).FindName("Player7_Colour");
		playerRows[6].RefName = (TextBlock)((FrameworkElement)this).FindName("Player7_Name");
		playerRows[6].RefHost = (Image)((FrameworkElement)this).FindName("Player7_Host");
		playerRows[6].RefType = (TextBlock)((FrameworkElement)this).FindName("Player7_Type");
		playerRows[6].RefPing = (TextBlock)((FrameworkElement)this).FindName("Player7_Ping");
		playerRows[6].RefKick = (Button)((FrameworkElement)this).FindName("Player7_Kick");
		playerRows[6].RefAISettings = (Button)((FrameworkElement)this).FindName("Player7_AISettings");
		playerRows[7].RefRow = (Grid)((FrameworkElement)this).FindName("Player8_Row");
		playerRows[7].RefReadyState = (Image)((FrameworkElement)this).FindName("Player8_ReadyState");
		playerRows[7].RefColour = (Image)((FrameworkElement)this).FindName("Player8_Colour");
		playerRows[7].RefName = (TextBlock)((FrameworkElement)this).FindName("Player8_Name");
		playerRows[7].RefHost = (Image)((FrameworkElement)this).FindName("Player8_Host");
		playerRows[7].RefType = (TextBlock)((FrameworkElement)this).FindName("Player8_Type");
		playerRows[7].RefPing = (TextBlock)((FrameworkElement)this).FindName("Player8_Ping");
		playerRows[7].RefKick = (Button)((FrameworkElement)this).FindName("Player8_Kick");
		playerRows[7].RefAISettings = (Button)((FrameworkElement)this).FindName("Player8_AISettings");
		RefIncludeUser = (CheckBox)((FrameworkElement)this).FindName("IncludeUser");
		((ToggleButton)RefIncludeUser).Checked += new RoutedEventHandler(Include_ValueChanged);
		((ToggleButton)RefIncludeUser).Unchecked += new RoutedEventHandler(Include_ValueChanged);
		RefIncludeBuiltin = (CheckBox)((FrameworkElement)this).FindName("IncludeBuiltin");
		((ToggleButton)RefIncludeBuiltin).Checked += new RoutedEventHandler(Include_ValueChanged);
		((ToggleButton)RefIncludeBuiltin).Unchecked += new RoutedEventHandler(Include_ValueChanged);
		RefIncludeWorkshop = (CheckBox)((FrameworkElement)this).FindName("IncludeWorkshop");
		((ToggleButton)RefIncludeWorkshop).Checked += new RoutedEventHandler(Include_ValueChanged);
		((ToggleButton)RefIncludeWorkshop).Unchecked += new RoutedEventHandler(Include_ValueChanged);
		RefRadarShieldTeam1 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam1");
		RefRadarShieldTeam2 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam2");
		RefRadarShieldTeam3 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam3");
		RefRadarShieldTeam4 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam4");
		RefRadarShieldTeam5 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam5");
		RefRadarShieldTeam6 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam6");
		RefRadarShieldTeam7 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam7");
		RefRadarShieldTeam8 = (Image)((FrameworkElement)this).FindName("RadarShieldTeam8");
		RefRadarShield1 = (Button)((FrameworkElement)this).FindName("RadarShield1");
		((UIElement)RefRadarShield1).PreviewMouseDown += new MouseButtonEventHandler(RadarShield1_Click);
		((UIElement)RefRadarShield1).PreviewMouseUp += new MouseButtonEventHandler(RadarShield_Up);
		RefRadarShield2 = (Button)((FrameworkElement)this).FindName("RadarShield2");
		((UIElement)RefRadarShield2).PreviewMouseDown += new MouseButtonEventHandler(RadarShield2_Click);
		((UIElement)RefRadarShield2).PreviewMouseUp += new MouseButtonEventHandler(RadarShield_Up);
		RefRadarShield3 = (Button)((FrameworkElement)this).FindName("RadarShield3");
		((UIElement)RefRadarShield3).PreviewMouseDown += new MouseButtonEventHandler(RadarShield3_Click);
		((UIElement)RefRadarShield3).PreviewMouseUp += new MouseButtonEventHandler(RadarShield_Up);
		RefRadarShield4 = (Button)((FrameworkElement)this).FindName("RadarShield4");
		((UIElement)RefRadarShield4).PreviewMouseDown += new MouseButtonEventHandler(RadarShield4_Click);
		((UIElement)RefRadarShield4).PreviewMouseUp += new MouseButtonEventHandler(RadarShield_Up);
		RefRadarShield5 = (Button)((FrameworkElement)this).FindName("RadarShield5");
		((UIElement)RefRadarShield5).PreviewMouseDown += new MouseButtonEventHandler(RadarShield5_Click);
		((UIElement)RefRadarShield5).PreviewMouseUp += new MouseButtonEventHandler(RadarShield_Up);
		RefRadarShield6 = (Button)((FrameworkElement)this).FindName("RadarShield6");
		((UIElement)RefRadarShield6).PreviewMouseDown += new MouseButtonEventHandler(RadarShield6_Click);
		((UIElement)RefRadarShield6).PreviewMouseUp += new MouseButtonEventHandler(RadarShield_Up);
		RefRadarShield7 = (Button)((FrameworkElement)this).FindName("RadarShield7");
		((UIElement)RefRadarShield7).PreviewMouseDown += new MouseButtonEventHandler(RadarShield7_Click);
		((UIElement)RefRadarShield7).PreviewMouseUp += new MouseButtonEventHandler(RadarShield_Up);
		RefRadarShield8 = (Button)((FrameworkElement)this).FindName("RadarShield8");
		((UIElement)RefRadarShield8).PreviewMouseDown += new MouseButtonEventHandler(RadarShield8_Click);
		((UIElement)RefRadarShield8).PreviewMouseUp += new MouseButtonEventHandler(RadarShield_Up);
		RefRadarShieldFace1 = (Grid)((FrameworkElement)this).FindName("RadarShieldFace1");
		RefRadarShieldFace2 = (Grid)((FrameworkElement)this).FindName("RadarShieldFace2");
		RefRadarShieldFace3 = (Grid)((FrameworkElement)this).FindName("RadarShieldFace3");
		RefRadarShieldFace4 = (Grid)((FrameworkElement)this).FindName("RadarShieldFace4");
		RefRadarShieldFace5 = (Grid)((FrameworkElement)this).FindName("RadarShieldFace5");
		RefRadarShieldFace6 = (Grid)((FrameworkElement)this).FindName("RadarShieldFace6");
		RefRadarShieldFace7 = (Grid)((FrameworkElement)this).FindName("RadarShieldFace7");
		RefRadarShieldFace8 = (Grid)((FrameworkElement)this).FindName("RadarShieldFace8");
		RefTeamFace1 = (Button)((FrameworkElement)this).FindName("TeamFace1");
		((UIElement)RefTeamFace1).PreviewMouseDown += new MouseButtonEventHandler(TeamFace1_Click);
		((UIElement)RefTeamFace1).PreviewMouseUp += new MouseButtonEventHandler(TeamFace_Up);
		RefTeamFace2 = (Button)((FrameworkElement)this).FindName("TeamFace2");
		((UIElement)RefTeamFace2).PreviewMouseDown += new MouseButtonEventHandler(TeamFace2_Click);
		((UIElement)RefTeamFace2).PreviewMouseUp += new MouseButtonEventHandler(TeamFace_Up);
		RefTeamFace3 = (Button)((FrameworkElement)this).FindName("TeamFace3");
		((UIElement)RefTeamFace3).PreviewMouseDown += new MouseButtonEventHandler(TeamFace3_Click);
		((UIElement)RefTeamFace3).PreviewMouseUp += new MouseButtonEventHandler(TeamFace_Up);
		RefTeamFace4 = (Button)((FrameworkElement)this).FindName("TeamFace4");
		((UIElement)RefTeamFace4).PreviewMouseDown += new MouseButtonEventHandler(TeamFace4_Click);
		((UIElement)RefTeamFace4).PreviewMouseUp += new MouseButtonEventHandler(TeamFace_Up);
		RefTeamFace5 = (Button)((FrameworkElement)this).FindName("TeamFace5");
		((UIElement)RefTeamFace5).PreviewMouseDown += new MouseButtonEventHandler(TeamFace5_Click);
		((UIElement)RefTeamFace5).PreviewMouseUp += new MouseButtonEventHandler(TeamFace_Up);
		RefTeamFace6 = (Button)((FrameworkElement)this).FindName("TeamFace6");
		((UIElement)RefTeamFace6).PreviewMouseDown += new MouseButtonEventHandler(TeamFace6_Click);
		((UIElement)RefTeamFace6).PreviewMouseUp += new MouseButtonEventHandler(TeamFace_Up);
		RefTeamFace7 = (Button)((FrameworkElement)this).FindName("TeamFace7");
		((UIElement)RefTeamFace7).PreviewMouseDown += new MouseButtonEventHandler(TeamFace7_Click);
		((UIElement)RefTeamFace7).PreviewMouseUp += new MouseButtonEventHandler(TeamFace_Up);
		RefTeamFace8 = (Button)((FrameworkElement)this).FindName("TeamFace8");
		((UIElement)RefTeamFace8).PreviewMouseDown += new MouseButtonEventHandler(TeamFace8_Click);
		((UIElement)RefTeamFace8).PreviewMouseUp += new MouseButtonEventHandler(TeamFace_Up);
		RefTeamFaceCancel = (Button)((FrameworkElement)this).FindName("TeamFaceCancel");
		((UIElement)RefTeamFaceCancel).PreviewMouseDown += new MouseButtonEventHandler(TeamFaceCancel_Click);
		((UIElement)RefTeamFaceCancel).PreviewMouseUp += new MouseButtonEventHandler(TeamFace_Up);
		RefFloatingRadarShield = (Image)((FrameworkElement)this).FindName("FloatingRadarShield");
		RefFloatingTeams = (Grid)((FrameworkElement)this).FindName("FloatingTeams");
		RefSkirmish_RadarMask = (Grid)((FrameworkElement)this).FindName("Skirmish_RadarMask");
		((UIElement)RefSkirmish_RadarMask).MouseDown += new MouseButtonEventHandler(SkirmishRadar_OffClick);
		RefMap_Balanced = (TextBlock)((FrameworkElement)this).FindName("Map_Balanced");
		RefMap_UnBalanced = (TextBlock)((FrameworkElement)this).FindName("Map_UnBalanced");
		RefExtremeWarningCheck = (CheckBox)((FrameworkElement)this).FindName("ExtremeWarningCheck");
		RefEnableAdvancedSkirmishCheck = (CheckBox)((FrameworkElement)this).FindName("EnableAdvancedSkirmishCheck");
		((ToggleButton)RefEnableAdvancedSkirmishCheck).Checked += new RoutedEventHandler(EnableAdvancedSkirmishCheck_ValueChanged);
		((ToggleButton)RefEnableAdvancedSkirmishCheck).Unchecked += new RoutedEventHandler(EnableAdvancedSkirmishCheck_ValueChanged);
		RefChatMuteDisable = (CheckBox)((FrameworkElement)this).FindName("ChatMuteDisable");
		((ToggleButton)RefChatMuteDisable).Checked += new RoutedEventHandler(MuteMPChat_ValueChanged);
		((ToggleButton)RefChatMuteDisable).Unchecked += new RoutedEventHandler(MuteMPChat_ValueChanged);
		RefMapSizeMin_Slider = (Slider)((FrameworkElement)this).FindName("MapSizeMin_Slider");
		RefMapSizeMax_Slider = (Slider)((FrameworkElement)this).FindName("MapSizeMax_Slider");
		((RangeBase)RefMapSizeMin_Slider).ValueChanged += MapSizeMin_Slider_ValueChanged;
		((RangeBase)RefMapSizeMax_Slider).ValueChanged += MapSizeMax_Slider_ValueChanged;
		RefAIMin_Slider = (Slider)((FrameworkElement)this).FindName("AIMin_Slider");
		RefAIMax_Slider = (Slider)((FrameworkElement)this).FindName("AIMax_Slider");
		((RangeBase)RefAIMin_Slider).ValueChanged += AIMin_Slider_ValueChanged;
		((RangeBase)RefAIMax_Slider).ValueChanged += AIMax_Slider_ValueChanged;
		RefRandomTeams = (CheckBox)((FrameworkElement)this).FindName("RandomTeams");
		RefRandomBalance = (CheckBox)((FrameworkElement)this).FindName("RandomBalance");
		RefRandomOutposts = (CheckBox)((FrameworkElement)this).FindName("RandomOutposts");
		RefRandomExtreme = (CheckBox)((FrameworkElement)this).FindName("RandomExtreme");
		RefRandomAdvanced = (CheckBox)((FrameworkElement)this).FindName("RandomAdvanced");
		RefRandomIncludeUser = (CheckBox)((FrameworkElement)this).FindName("RandomIncludeUser");
		RefRandomIncludeBuiltin = (CheckBox)((FrameworkElement)this).FindName("RandomIncludeBuiltin");
		RefRandomIncludeWorkshop = (CheckBox)((FrameworkElement)this).FindName("RandomIncludeWorkshop");
		RefCustomLordList = (ListView)((FrameworkElement)this).FindName("CustomLordList");
		((Control)RefCustomLordList).MouseDoubleClick += (MouseButtonEventHandler)delegate
		{
			ButtonClicked("AddCustomLord");
		};
		RefTrailMakerTest = (Button)((FrameworkElement)this).FindName("TrailMakerTest");
		GridView val = (GridView)RefFileLists.View;
		GridViewColumnHeader val2 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[4].Header;
		((ContentControl)val2).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 27);
		((ButtonBase)val2).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		GridViewColumnHeader val3 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[5].Header;
		((ContentControl)val3).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 28);
		((ButtonBase)val3).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		GridViewColumnHeader val4 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[0].Header;
		((ContentControl)val4).Content = "";
		((ButtonBase)val4).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		GridViewColumnHeader val5 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[1].Header;
		((ContentControl)val5).Content = "#";
		((ButtonBase)val5).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		GridViewColumnHeader val6 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[2].Header;
		((ContentControl)val6).Content = "";
		((ButtonBase)val6).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		GridViewColumnHeader val7 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val.Columns)[3].Header;
		((ContentControl)val7).Content = "";
		((ButtonBase)val7).Click += new RoutedEventHandler(FileListHeaderClickedHandler);
		((Selector)RefFileLists).SelectionChanged += (SelectionChangedEventHandler)delegate
		{
			if (((Selector)RefFileLists).SelectedItem != null)
			{
				if (sortByColumn < 10 || sortByColumn > 16)
				{
					((ListBox)RefFileLists).ScrollIntoView(((Selector)RefFileLists).SelectedItem);
				}
				if (skirmishGame || (currentLobby != null && currentLobby.isHost))
				{
					FileHeader fileHeader = ((FileRow)((Selector)RefFileLists).SelectedItem).fileHeader;
					if (fileHeader != null && (!ignoreSelectRefresh || selectedMPHeader != fileHeader))
					{
						selectedMPHeader = fileHeader;
						GameData.Instance.setKeepLocationsFromHeader(selectedMPHeader);
						if (!skipMapSelectRandomKeeps)
						{
							update_keep_locations_on_map_change();
						}
						UpdateRadarShieldPositions();
						UpdateHostInfo();
						updateRadarTexture(fileHeader);
						GameData.Instance.SetMissionTextFromHeader(fileHeader);
						PopulateMapDetailsPanel(fileHeader);
						if (!MPLocalReadyLocked)
						{
							MPLocalReady = false;
							if (!skirmishGame)
							{
								Platform_Multiplayer.Instance.SetMemberReadyState(state: false);
							}
						}
						MainViewModel.Instance.Show_MPPeacetime = !skirmishGame;
					}
				}
			}
		};
		((FrameworkElement)RefFileLists).Loaded += (RoutedEventHandler)delegate
		{
			if (((Selector)RefFileLists).SelectedItem != null)
			{
				((ListBox)RefFileLists).ScrollIntoView(((Selector)RefFileLists).SelectedItem);
			}
		};
		GridView val8 = (GridView)RefLobbyLists.View;
		GridViewColumnHeader val9 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val8.Columns)[0].Header;
		((ContentControl)val9).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 48);
		((ButtonBase)val9).Click += new RoutedEventHandler(LobbyListHeaderClickedHandler);
		GridViewColumnHeader val10 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val8.Columns)[1].Header;
		((ContentControl)val10).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 49);
		((ButtonBase)val10).Click += new RoutedEventHandler(LobbyListHeaderClickedHandler);
		GridViewColumnHeader val11 = (GridViewColumnHeader)((FreezableCollection<GridViewColumn>)(object)val8.Columns)[2].Header;
		((ContentControl)val11).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 50);
		((ButtonBase)val11).Click += new RoutedEventHandler(LobbyListHeaderClickedHandler);
		((Selector)RefLobbyLists).SelectionChanged += (SelectionChangedEventHandler)delegate
		{
			if (((Selector)RefLobbyLists).SelectedItem != null)
			{
				selectedLobby = ((FileRow)((Selector)RefLobbyLists).SelectedItem).lobby;
				Button refLobbySettingsButton = RefLobbySettingsButton;
				bool isEnabled = (((UIElement)RefJoinButton).IsEnabled = true);
				((UIElement)refLobbySettingsButton).IsEnabled = isEnabled;
				UpdateLobbySettingsButton();
			}
		};
		((Control)RefLobbyLists).MouseDoubleClick += (MouseButtonEventHandler)delegate
		{
			if (((Selector)RefLobbyLists).SelectedItem != null)
			{
				selectedLobby = ((FileRow)((Selector)RefLobbyLists).SelectedItem).lobby;
				Button refLobbySettingsButton = RefLobbySettingsButton;
				bool isEnabled = (((UIElement)RefJoinButton).IsEnabled = true);
				((UIElement)refLobbySettingsButton).IsEnabled = isEnabled;
				UpdateLobbySettingsButton();
				ButtonClicked("Join");
			}
		};
		if (FatControler.russian)
		{
			MainViewModel.Instance.MP_AI_Info_Margin = "10,0,0,0";
		}
		if (FatControler.german || FatControler.italian)
		{
			MainViewModel.Instance.MP_AI_Info_Margin = "10,2,0,0";
		}
		if (FatControler.japanese)
		{
			MainViewModel.Instance.MP_AI_Info_Margin = "10,1,0,0";
			RefMap_Balanced.FontSize = 14f;
			RefMap_Balanced.TextWrapping = (TextWrapping)1;
			RefMap_UnBalanced.FontSize = 14f;
			RefMap_UnBalanced.TextWrapping = (TextWrapping)1;
			((FrameworkElement)RefMap_Balanced).Margin = new Thickness(93f, 67f, 0f, 0f);
			((FrameworkElement)RefMap_UnBalanced).Margin = new Thickness(93f, 67f, 0f, 0f);
		}
		if (FatControler.korean)
		{
			MainViewModel.Instance.MP_AI_Info_Margin = "10,1,0,0";
		}
		if (FatControler.japanese)
		{
			PropEx.SetGlowButtonFontSize((UIElement)(object)RefMultiplayerSetupInfo, 14);
		}
		panelLoaded = true;
	}

	public static void Open(bool skirmishSetup = false, HUD_IngameMenu.RestartSkirmishMapInfo restartInfo = null, bool coopSetup = false, bool trailMaker = false, int customiseTrailType = -1, int customiseTrailID = -1)
	{
		MainViewModel.Instance.FRONTMultiplayer.doOpen(skirmishSetup, fromNew: true, restartInfo, coopSetup, trailMaker, customiseTrailType, customiseTrailID);
	}

	public void doOpen(bool skirmishSetup, bool fromNew = false, HUD_IngameMenu.RestartSkirmishMapInfo restartInfo = null, bool coopSetup = false, bool _trailMaker = false, int _customiseTrailType = -1, int _customiseTrailID = -1)
	{
		try
		{
			FileHeader selectedHeader = null;
			skirmishGame = skirmishSetup;
			coopGame = coopSetup;
			coopGame_IsHost = false;
			coopGame_ClientSelectedMission = -1;
			singlePlayerCoop = false;
			customizedTrail = false;
			extremeTrailCustomised = false;
			trailMakerMode = _trailMaker;
			MainViewModel.Instance.MultiplayerNonTrailMakerMode = !trailMakerMode;
			MainViewModel.Instance.CoopNewChatVis = false;
			MainViewModel.Instance.SkirmishSetupMode = skirmishGame;
			MainViewModel.Instance.MultiplayerSetupMode = !skirmishGame;
			panelActive = false;
			skipMapSelectRandomKeeps = false;
			if (fromNew)
			{
				closePanelDisplayed = false;
				spectatorMode = false;
				((FrameworkElement)RefHeaderBar).Width = 545f;
				MainViewModel.Instance.Show_ManageTrail = false;
				MainViewModel.Instance.HUDIngameMenu.restartMapInfo = null;
				MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo = null;
				MainViewModel.Instance.HUDIngameMenu.restartMPInfo = null;
				if (!coopGame || coopPendingLobby == null)
				{
					Platform_Multiplayer.Instance.exitMP();
				}
				EditorDirector.instance.pendingMPExit = false;
				InitCoopMissions();
				MainViewModel.Instance.Show_EnterSpectatorButton = !trailMakerMode;
				if (AIVs == null)
				{
					AIVs = new MPAIVInfo[8];
					for (int i = 0; i < 8; i++)
					{
						AIVs[i] = new MPAIVInfo();
					}
				}
				else
				{
					for (int j = 0; j < 8; j++)
					{
						AIVs[j].Clear();
					}
				}
				if (CustomisationFileManager.Instance.filesChanged)
				{
					CustomisationFileManager.Instance.BuildFileLists();
				}
				if (FrontendMenus.CurrentSelectedTrail == 21)
				{
					MainViewModel.Instance.Show_CoopTrail1 = coopGame;
				}
				else if (FrontendMenus.CurrentSelectedTrail == 22)
				{
					MainViewModel.Instance.Show_CoopTrail2 = coopGame;
				}
				else if (FrontendMenus.CurrentSelectedTrail == 23)
				{
					MainViewModel.Instance.Show_CoopTrail3 = coopGame;
				}
				if (currentLobby != null)
				{
					LeaveLobby(doLeaveOnSteam: true, refreshLobbyList: false);
				}
				MainViewModel.Instance.Show_MP_ExtremeWarning = false;
				MainViewModel.Instance.Show_CoopHostInvitePane = false;
				MainViewModel.Instance.Show_CoopHostJoinedPane = false;
				MainViewModel.Instance.Show_CoopClientPane = false;
				MainViewModel.Instance.Show_CoopConnectedChatVisible = false;
				MainViewModel.Instance.Show_CoopAIAllyPanel = false;
				MainViewModel.Instance.Show_CoopMapIcons = true;
				MainViewModel.Instance.Show_CoopWaiting = false;
				MainViewModel.Instance.Show_CoopOptions = false;
				customCoopGame = false;
				((UIElement)RefLobbyMaxPlayersSlider).IsEnabled = true;
				MainViewModel.Instance.Show_MPLobbyMaxPlayers = true;
				if (pendingCoopWaitingPanel)
				{
					if (coopGame || Platform_Multiplayer.Instance.PendingMPLobby)
					{
						MainViewModel.Instance.Show_CoopWaiting = true;
					}
					pendingCoopWaitingPanel = false;
				}
				Platform_Multiplayer.MPChatMuted = ConfigSettings.Settings_MuteMPChat;
				((ToggleButton)RefChatMuteDisable).IsChecked = ConfigSettings.Settings_MuteMPChat;
				((UIElement)RefMP_ChatSend).IsEnabled = !ConfigSettings.Settings_MuteMPChat;
				MainViewModel.Instance.Show_Radar160Border = false;
				MainViewModel.Instance.Show_Radar300Border = false;
				MainViewModel.Instance.Show_Radar500Border = false;
				MainViewModel.Instance.Show_Radar700Border = false;
				MainViewModel.Instance.Show_MPSteamIdentity = false;
				if (!skirmishGame)
				{
					MainViewModel.Instance.MP_SteamIdentity_Avatar = Platform_Multiplayer.Instance.GetLocalAvatar();
					MainViewModel.Instance.MP_SteamIdentity_Name = " " + Platform_Multiplayer.Instance.GetLocalSteamName() + " ";
				}
				Platform_Multiplayer.Instance.ClearSteamAvatarCache();
				pulseAnimation.Stop();
				settingsPulseAnimation.Stop();
				readyAnimPlaying = false;
				sortByColumn = 0;
				sortByAscending = true;
				includeUser = true;
				includeBuiltIn = true;
				includeWorkshop = true;
				((ToggleButton)RefIncludeBuiltin).IsChecked = true;
				((ToggleButton)RefIncludeUser).IsChecked = true;
				((ToggleButton)RefIncludeWorkshop).IsChecked = true;
				((UIElement)RefMultiplayerInvite).IsEnabled = true;
				ignoreSelectRefresh = false;
				((RangeBase)RefMapSizeMax_Slider).Value = 7f;
				((RangeBase)RefMapSizeMin_Slider).Value = 0f;
				((RangeBase)RefAIMax_Slider).Value = 7f;
				((RangeBase)RefAIMin_Slider).Value = 1f;
				((ToggleButton)RefRandomTeams).IsChecked = true;
				((ToggleButton)RefRandomBalance).IsChecked = true;
				((ToggleButton)RefRandomOutposts).IsChecked = false;
				((ToggleButton)RefRandomExtreme).IsChecked = false;
				((ToggleButton)RefRandomAdvanced).IsChecked = false;
				((ToggleButton)RefRandomIncludeUser).IsChecked = true;
				((ToggleButton)RefRandomIncludeBuiltin).IsChecked = true;
				((ToggleButton)RefRandomIncludeWorkshop).IsChecked = true;
				hideToolTipTime = DateTime.MinValue;
				MainViewModel.Instance.MPGame_Type_Description = "";
				MainViewModel.Instance.Show_MPGame_Type_Description = false;
				MainViewModel.Instance.Show_MP_SkirmishAdvanced = false;
				MainViewModel.Instance.Show_MPSettings = false;
				MainViewModel.Instance.Show_MPLobbySettings = false;
				MainViewModel.Instance.Show_MPAISettings = false;
				MainViewModel.Instance.MP_ShowFaces = false;
				Platform_Multiplayer.Instance.gameMembers = null;
				selectedMPHeader = null;
				selectedLobby = null;
				currentLobby = null;
				selectedCoopMissionID = 0;
				coopOrderSwapped = false;
				humanPlayerCount = -1;
				lobbies.Clear();
				for (int k = 0; k < 8; k++)
				{
					playerRows[k].Clear();
					FRONT_CoopTrail1.Instance.playerRows[k].Clear();
					FRONT_CoopTrail2.Instance.playerRows[k].Clear();
					FRONT_CoopTrail3.Instance.playerRows[k].Clear();
				}
				MPsetupData = EngineInterface.initMultiplayerGame(skirmishGame);
				if (defaultMPSettings == "")
				{
					defaultMPSettings = MPsetupData.ToString();
					MPDefaultsetupData = new EngineInterface.MultiplayerSetupData();
					MPDefaultsetupData.FromString(defaultMPSettings);
				}
				if (coopGame)
				{
					MPsetupData.FromString(defaultMPSettings);
					EngineInterface.setMultiplayerStartingData(MPsetupData);
				}
				if (skirmishSetup)
				{
					if (ConfigSettings.Settings_SkirmishPresets.Length > 0)
					{
						MPsetupData.FromStringCustomSkirmish(ConfigSettings.Settings_SkirmishPresets);
					}
					MPsetupData.starting_gamespeed = ConfigSettings.Settings_GameSpeed;
					if (MPsetupData.advanced_skirmish_options > 0)
					{
						MainViewModel.Instance.Show_SkirmishAdvancedEnabled = MPsetupData.advancedSkirmishOptionsEnabled();
					}
					else
					{
						MainViewModel.Instance.Show_SkirmishAdvancedEnabled = false;
					}
				}
				GameData.Instance.setKeepOrder(MPsetupData.start_keep_location_order);
				if (!skirmishSetup && !coopGame)
				{
					populateLobbyList();
					lastAutoRefreshTime = DateTime.UtcNow;
					Platform_Multiplayer.Instance.Initialise();
					Platform_Multiplayer.Instance.GetLobbies(matchmakingDefault, delegate
					{
						lobbies = Platform_Multiplayer.Instance.ReadLobbies();
						populateLobbyList();
					});
				}
				numConnectedPlayers = 2;
				PlayerCap = 8;
				headerlist = MapFileManager.Instance.GetMultiplayerMaps(sortByColumn, sortByAscending, numConnectedPlayers, includeBuiltIn, includeUser, includeWorkshop);
				MainViewModel.Instance.MultiplayerFilter = "";
				MainViewModel.Instance.MultiplayerFilterLabelVis = (Visibility)2;
				MainViewModel.Instance.MultiplayerFilterButtonVis = (Visibility)1;
				MainViewModel.Instance.MultiplayerEnterShareCode = "";
				((UIElement)RefShareJoinButton).IsEnabled = false;
				((UIElement)FRONT_CoopTrail1.Instance.RefShareJoinButton).IsEnabled = false;
				((UIElement)FRONT_CoopTrail2.Instance.RefShareJoinButton).IsEnabled = false;
				((UIElement)FRONT_CoopTrail3.Instance.RefShareJoinButton).IsEnabled = false;
				LatestSharedCode = 0uL;
				pendingMPHost = false;
				MPMapChecked = false;
				MPMapValid = false;
				MPGameLoading = false;
				regetMapListNextTime = false;
				MPLocalReady = false;
				MPLocalReadyLocked = false;
				((UIElement)RefReadyButtonLock).Visibility = (Visibility)1;
				((UIElement)FRONT_CoopTrail1.Instance.RefReadyButtonLock).Visibility = (Visibility)1;
				((UIElement)FRONT_CoopTrail2.Instance.RefReadyButtonLock).Visibility = (Visibility)1;
				((UIElement)FRONT_CoopTrail3.Instance.RefReadyButtonLock).Visibility = (Visibility)1;
				MPLobbyMode = 0;
				MPGameType = 0;
				MPStartingSettings = 0;
				MPLastMapName = "";
				multiplayerMapRequestTime = DateTime.MinValue;
				MainViewModel.Instance.MP_LobbyChatWindow = "";
				ShowSharingCode = false;
				showLobbyUnavailableMessage = false;
				MainViewModel.Instance.Show_SkirmishAllowOutposts = false;
				lobbyChatRefreshPending = false;
				UpdateMatchmakingButton();
				if (!skirmishSetup)
				{
					if (Platform_Multiplayer.Instance.PendingMPLobby)
					{
						Platform_Multiplayer.MPLobby joiningLobby = null;
						Platform_Multiplayer.Instance.AutoJoinPendingLobby(ref joiningLobby, delegate
						{
							if (Platform_Multiplayer.Instance.activeLobby == null)
							{
								showLobbyUnavailableMessage = true;
							}
							else
							{
								AutoJoinLobby(joiningLobby);
							}
						}, delegate(string name, string message, int colourID)
						{
							receivedLobbyChat(name, message, colourID);
						});
						Platform_Multiplayer.Instance.PendingMPLobby = false;
					}
					else if (coopGame)
					{
						MainViewModel.Instance.Show_CoopHostInvitePane = true;
						MainViewModel.Instance.Show_CoopOptions = false;
						coopGame_IsHost = true;
						ConfigSettings.CalcCoopProgress(0uL);
						Platform_Multiplayer.Instance.Initialise();
						int coopTrailID = 0;
						if (FrontendMenus.CurrentSelectedTrail == 22)
						{
							coopTrailID = 1;
						}
						else if (FrontendMenus.CurrentSelectedTrail == 23)
						{
							coopTrailID = 2;
						}
						if (coopPendingLobby != null)
						{
							MainViewModel.Instance.Show_CoopWaiting = true;
							currentLobby = coopPendingLobby;
							Platform_Multiplayer.Instance.SetActiveLobby(currentLobby);
							Platform_Multiplayer.Instance.initFastFollowOn();
							updateSteamIDMappings();
							UpdateRadarShieldPositions();
							UpdateHostInfo();
							MPHostLobbyname = RefTextBoxGameName.Text;
							ShowSetupScreen();
							coopFriendsPage = 0;
							coopShowHiddenFriends = false;
							if (ConfigSettings.getCoopTrailCount(countHidden: true) == ConfigSettings.getCoopTrailCount(countHidden: false))
							{
								((UIElement)FRONT_CoopTrail1.Instance.RefShowHidden).Visibility = (Visibility)1;
								((UIElement)FRONT_CoopTrail2.Instance.RefShowHidden).Visibility = (Visibility)1;
								((UIElement)FRONT_CoopTrail3.Instance.RefShowHidden).Visibility = (Visibility)1;
							}
							else
							{
								((UIElement)FRONT_CoopTrail1.Instance.RefShowHidden).Visibility = (Visibility)2;
								((UIElement)FRONT_CoopTrail2.Instance.RefShowHidden).Visibility = (Visibility)2;
								((UIElement)FRONT_CoopTrail3.Instance.RefShowHidden).Visibility = (Visibility)2;
							}
							((ToggleButton)FRONT_CoopTrail1.Instance.RefShowHidden).IsChecked = false;
							((ToggleButton)FRONT_CoopTrail2.Instance.RefShowHidden).IsChecked = false;
							((ToggleButton)FRONT_CoopTrail3.Instance.RefShowHidden).IsChecked = false;
							CoopPopulateFriendsList();
							MPLocalReady = true;
							Platform_Multiplayer.Instance.SetMemberReadyState(MPLocalReady);
							MainViewModel.Instance.FrontEndMenu.GenerateSwords();
							if (coopTrailID == 0)
							{
								MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(ConfigSettings.Settings_Progress_Trail_CoopNext1 + 1);
							}
							else
							{
								MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(ConfigSettings.Settings_Progress_Trail_CoopNext2 + 1);
							}
						}
						else
						{
							Platform_Multiplayer.Instance.CreateLobby("IGNORE!", "", "", 2, 0, 4, MPsetupData.ToString(), 0, delegate
							{
								currentLobby = Platform_Multiplayer.Instance.GetActiveLobby();
								updateSteamIDMappings();
								UpdateRadarShieldPositions();
								UpdateHostInfo();
								MPHostLobbyname = RefTextBoxGameName.Text;
								ShowSetupScreen();
								coopFriendsPage = 0;
								coopShowHiddenFriends = false;
								if (ConfigSettings.getCoopTrailCount(countHidden: true) == ConfigSettings.getCoopTrailCount(countHidden: false))
								{
									((UIElement)FRONT_CoopTrail1.Instance.RefShowHidden).Visibility = (Visibility)1;
									((UIElement)FRONT_CoopTrail2.Instance.RefShowHidden).Visibility = (Visibility)1;
									((UIElement)FRONT_CoopTrail3.Instance.RefShowHidden).Visibility = (Visibility)1;
								}
								else
								{
									((UIElement)FRONT_CoopTrail1.Instance.RefShowHidden).Visibility = (Visibility)2;
									((UIElement)FRONT_CoopTrail2.Instance.RefShowHidden).Visibility = (Visibility)2;
									((UIElement)FRONT_CoopTrail3.Instance.RefShowHidden).Visibility = (Visibility)2;
								}
								((ToggleButton)FRONT_CoopTrail1.Instance.RefShowHidden).IsChecked = false;
								((ToggleButton)FRONT_CoopTrail2.Instance.RefShowHidden).IsChecked = false;
								((ToggleButton)FRONT_CoopTrail3.Instance.RefShowHidden).IsChecked = false;
								CoopPopulateFriendsList();
								MPLocalReady = true;
								Platform_Multiplayer.Instance.SetMemberReadyState(MPLocalReady);
								MainViewModel.Instance.FrontEndMenu.GenerateSwords();
								if (coopTrailID == 0)
								{
									MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(ConfigSettings.Settings_Progress_Trail_CoopNext1 + 1);
								}
								else
								{
									MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(ConfigSettings.Settings_Progress_Trail_CoopNext2 + 1);
								}
							}, delegate(string name, string message, int colourID)
							{
								receivedLobbyChat(name, message, colourID);
							}, coopTrailID);
						}
					}
					ShowLobbyScreen();
				}
				else if (_customiseTrailType >= 0)
				{
					customizedTrail = true;
					customizedTrailType = _customiseTrailType;
					customizedTrailID = _customiseTrailID;
					if (customizedTrailType == 2)
					{
						extremeTrailCustomised = true;
					}
					string mapName = "";
					Platform_Multiplayer.TrailMissionInfo trailMissionInfo = new Platform_Multiplayer.TrailMissionInfo(EngineInterface.getTrailMissionInfo(_customiseTrailType, _customiseTrailID, ref mapName));
					if (customizedTrailType <= 2 && ConfigSettings.Settings_Allow_Classic_Bedouin_Stockade)
					{
						trailMissionInfo.stockade = 255;
					}
					currentLobby = new Platform_Multiplayer.MPLobby(trailMissionInfo);
					Platform_Multiplayer.Instance.activeLobby = currentLobby;
					MPsetupData = new EngineInterface.MultiplayerSetupData();
					MPsetupData.FromString(defaultMPSettings);
					MPsetupData.fairness = trailMissionInfo.fairness;
					MPsetupData.starting_goods_level = trailMissionInfo.starting_level;
					MPsetupData.MP_BuildingsAvailable[0] = trailMissionInfo.barracks;
					MPsetupData.MP_BuildingsAvailable[1] = trailMissionInfo.merc_post;
					MPsetupData.MP_BuildingsAvailable[2] = trailMissionInfo.stockade;
					MPsetupData.advanced_skirmish_options = trailMissionInfo.barracks | trailMissionInfo.merc_post | trailMissionInfo.stockade;
					if (_customiseTrailType == 2)
					{
						MPsetupData.extreme_powers = 1;
						MPsetupData.extreme_troops = 1;
					}
					for (int num = 0; num < 8; num++)
					{
						MPsetupData.start_keep_location_order[num] = -10;
					}
					for (int num2 = 0; num2 < 8; num2++)
					{
						AIVs[num2] = new MPAIVInfo();
						if (num2 > 0 && num2 < trailMissionInfo.num_players)
						{
							List<CustomisationFileManager.CustomAIV> lordAIVList = CustomisationFileManager.Instance.getLordAIVList(trailMissionInfo.lordTypes[num2] - 1);
							AIVs[num2].Init((trailMissionInfo.lordTypes[num2] - 1) / 8, "");
							AIVs[num2].aivs.Clear();
							AIVs[num2].aivs.Add(lordAIVList[trailMissionInfo.aiv_type[num2] % 100]);
							AIVs[num2].rotation = trailMissionInfo.aiv_type[num2] / 100;
							AIVs[num2].builtIn = false;
							MPsetupData.start_keep_location_order[trailMissionInfo.locations[num2] - 1] = num2;
						}
						else if (num2 == 0)
						{
							MPsetupData.start_keep_location_order[trailMissionInfo.locations[num2] - 1] = num2;
						}
					}
					selectedHeader = MapFileManager.Instance.GetHeaderFromFileNameMP(mapName);
					skipMapSelectRandomKeeps = true;
					GameData.Instance.setKeepOrder(MPsetupData.start_keep_location_order);
					updateSteamIDMappings();
					if (currentLobby.kickEmptySlots())
					{
						updateSteamIDMappings();
					}
					ReSortTeamInfo();
					CreateTeamShields();
					UpdateRadarShieldPositions();
				}
				else if (restartInfo != null)
				{
					currentLobby = new Platform_Multiplayer.MPLobby(restartInfo);
					PopulateAIVsFromRestartInfo(restartInfo);
					Platform_Multiplayer.Instance.activeLobby = currentLobby;
					MPsetupData = restartInfo.MPsetupData;
					selectedHeader = restartInfo.selectedHeader;
					skipMapSelectRandomKeeps = true;
					if (restartInfo.customisedExtremeTrail)
					{
						extremeTrailCustomised = true;
					}
					if (MPsetupData.advanced_skirmish_options > 0)
					{
						MainViewModel.Instance.Show_SkirmishAdvancedEnabled = MPsetupData.advancedSkirmishOptionsEnabled();
					}
					else
					{
						MainViewModel.Instance.Show_SkirmishAdvancedEnabled = false;
					}
					GameData.Instance.setKeepOrder(MPsetupData.start_keep_location_order);
					updateSteamIDMappings();
					if (currentLobby.kickEmptySlots())
					{
						updateSteamIDMappings();
					}
					ReSortTeamInfo();
					CreateTeamShields();
					UpdateRadarShieldPositions();
				}
				else
				{
					currentLobby = new Platform_Multiplayer.MPLobby();
					Platform_Multiplayer.Instance.activeLobby = currentLobby;
					currentLobby.isHost = true;
					Platform_Multiplayer.MPLobbyMember mPLobbyMember = new Platform_Multiplayer.MPLobbyMember();
					mPLobbyMember.colourID = ConfigSettings.Settings_PlayerColour + 1;
					mPLobbyMember.Name = ConfigSettings.Settings_UserName;
					mPLobbyMember.SkirmishHumanMember = (mPLobbyMember.SkirmishMember = true);
					mPLobbyMember.id.m_SteamID = Platform_Multiplayer.Instance.GetLocalSteamID();
					currentLobby.setTeam(mPLobbyMember, currentLobby.getFreeTeam());
					currentLobby.members.Add(mPLobbyMember);
					currentLobby.numLobbyMembers = currentLobby.members.Count;
					updateSteamIDMappings();
				}
				Button refLobbySettingsButton = RefLobbySettingsButton;
				bool isEnabled = (((UIElement)RefJoinButton).IsEnabled = false);
				((UIElement)refLobbySettingsButton).IsEnabled = isEnabled;
				((UIElement)RefLobbySettingsButton).Visibility = (Visibility)1;
			}
			MainViewModel.Instance.RadarStandaloneImage = null;
			MainViewModel.Instance.StandaloneMissionText = "";
			MainViewModel.Instance.StandaloneMissionTitle = "";
			MainViewModel.Instance.Show_StandaloneMissionHasOutposts = false;
			MainViewModel.Instance.StandaloneMissionSize = "";
			MainViewModel.Instance.StandaloneMissionPlayerCount = "";
			MainViewModel.Instance.Show_StandaloneMissionBalanced = true;
			MainViewModel.Instance.Show_StandaloneMissionUnBalanced = false;
			AILordTextClear = DateTime.MinValue;
			MainViewModel.Instance.SkirmishLordRolloverName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_WR_CHOOSE_OPP);
			MainViewModel.Instance.SkirmishLordRolloverName2 = "";
			MainViewModel.Instance.SkirmishLordRolloverDesc = "";
			MainViewModel.Instance.Show_AddAIPanel_Rollover = false;
			MainViewModel.Instance.Show_SkirmishAIADD = true;
			MainViewModel.Instance.Show_SkirmishRandomAI = skirmishGame;
			MainViewModel.Instance.Show_SkirmishTeams = true;
			MainViewModel.Instance.Show_AddAIPanel = false;
			MainViewModel.Instance.Show_AddAIPanel_Normal = true;
			MainViewModel.Instance.Show_SkirmishRandomAIPanel = false;
			MainViewModel.Instance.Show_AdvancedRandom = false;
			MainViewModel.Instance.Show_SkirmishTeamsPanel = false;
			if (skirmishGame)
			{
				((UIElement)RefMultiplayerPlayButton).Visibility = (Visibility)2;
			}
			RefFloatingRadarShield.Source = null;
			SelectedRadarKeep = -1;
			SelectedFace = -1;
			((UIElement)RefTeamFaceCancel).IsEnabled = false;
			MainViewModel.Instance.Show_SkirmishUIOnRadar = false;
			MainViewModel.Instance.AlliesFace = null;
			MainViewModel.Instance.AlliesFaceBackground = null;
			MainViewModel.Instance.AlliesHumanFaceVisible = false;
			UpdateButtons();
			GameData.Instance.game_type = 3;
			if (!skirmishSetup)
			{
				populateLobbyList();
			}
			else
			{
				pendingMPHost = true;
				ShowSetupScreen();
			}
			coopPendingLobby = null;
			Platform_Multiplayer.Instance.CoopContinuationLobbyID = 0uL;
			populateMapList(selectedHeader);
			SetupSkirmishModeSettings();
			skipMapSelectRandomKeeps = false;
			MainViewModel.Instance.Show_MultiplayerSetup = true;
			panelActive = true;
		}
		catch (Exception)
		{
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("BackMain");
		}
	}

	public ulong PreCreateCoopLobby(int coopTrailID, int coopMissionID)
	{
		ulong result = Platform_Multiplayer.Instance.CoopPartnerID();
		if (Platform_Multiplayer.Instance.IsGameMemberHost())
		{
			Platform_Multiplayer.Instance.CreateLobby("IGNORE!", "", "", 2, 0, 4, MPsetupData.ToString(), 0, delegate
			{
				coopPendingLobby = Platform_Multiplayer.Instance.GetActiveLobby();
				currentLobby = null;
				Platform_Multiplayer.Instance.SendCoopContinuationLobby(coopPendingLobby.identifier);
			}, delegate(string name, string message, int colourID)
			{
				receivedLobbyChat(name, message, colourID);
			}, coopTrailID, clearGameMembers: false);
		}
		return result;
	}

	public void FileListHeaderClickedHandler(object sender, RoutedEventArgs e)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		switch (((FrameworkElement)(GridViewColumnHeader)e.Source).Tag as string)
		{
		case "Name":
			if (sortByColumn == 0)
			{
				sortByAscending = !sortByAscending;
				break;
			}
			sortByColumn = 0;
			sortByAscending = true;
			break;
		case "Date":
			if (sortByColumn == 1)
			{
				sortByAscending = !sortByAscending;
				break;
			}
			sortByColumn = 1;
			sortByAscending = false;
			break;
		case "Players":
			if (!KeyManager.instance.isShiftDown())
			{
				if (sortByColumn == 2)
				{
					sortByAscending = !sortByAscending;
					break;
				}
				sortByColumn = 2;
				sortByAscending = false;
			}
			else if (sortByColumn >= 10)
			{
				sortByColumn++;
				if (sortByColumn > 16)
				{
					sortByColumn = 10;
				}
			}
			else
			{
				sortByColumn = 10;
				sortByAscending = false;
			}
			break;
		case "Size":
			if (sortByColumn == 3)
			{
				sortByAscending = !sortByAscending;
				break;
			}
			sortByColumn = 3;
			sortByAscending = false;
			break;
		case "Balanced":
			if (sortByColumn == 5)
			{
				sortByAscending = !sortByAscending;
				break;
			}
			sortByColumn = 5;
			sortByAscending = false;
			break;
		}
		populateMapList(selectedMPHeader, ignoreRefresh: true);
	}

	public void populateMapList(FileHeader selectedHeader = null, bool ignoreRefresh = false)
	{
		includeBuiltIn = ((ToggleButton)RefIncludeBuiltin).IsChecked.Value;
		includeUser = ((ToggleButton)RefIncludeUser).IsChecked.Value;
		includeWorkshop = ((ToggleButton)RefIncludeWorkshop).IsChecked.Value;
		_ = sortByColumn;
		headerlist = MapFileManager.Instance.GetMultiplayerMaps(sortByColumn, sortByAscending, numConnectedPlayers, includeBuiltIn, includeUser, includeWorkshop);
		FileRow fileRow = null;
		fileRows.Clear();
		if (headerlist != null)
		{
			string text = RefMP_SearchFilter.Text;
			string value = text.ToLowerInvariant();
			foreach (FileHeader item in headerlist)
			{
				if (text.Length <= 0 || item.display_filename.Contains(text) || item.display_filename.ToLowerInvariant().Contains(value))
				{
					FileRow fileRow2 = new FileRow();
					fileRow2.Text2 = item.getDateString();
					fileRow2.Text3 = item.maxPlayers.ToString();
					if (item.world_size > 0)
					{
						fileRow2.Text4 = item.world_size.ToString();
					}
					else
					{
						fileRow2.Text4 = "";
					}
					if (item.builtinMap)
					{
						fileRow2.TypeImage = MainViewModel.Instance.GameSprites[88];
					}
					else if (item.workshopMap)
					{
						fileRow2.TypeImage = MainViewModel.Instance.GameSprites[89];
					}
					else if (item.userMap)
					{
						fileRow2.TypeImage = MainViewModel.Instance.GameSprites[90];
					}
					string text2 = "";
					if (!item.balanced)
					{
						fileRow2.BalancedImage = MainViewModel.Instance.GameSprites[687];
					}
					else
					{
						fileRow2.BalancedImage = null;
					}
					fileRow2.Text1 = text2 + item.display_filename;
					fileRow2.fileHeader = item;
					fileRows.Add(fileRow2);
					if (item == selectedHeader)
					{
						fileRow = fileRow2;
						selectedMPHeader = selectedHeader;
					}
				}
			}
		}
		((ItemsControl)RefFileLists).ItemsSource = fileRows;
		if (fileRow != null)
		{
			if (ignoreRefresh)
			{
				ignoreSelectRefresh = true;
			}
			((Selector)RefFileLists).SelectedItem = fileRow;
			ignoreSelectRefresh = false;
		}
	}

	public void populateLobbyList()
	{
		ulong num = 0uL;
		FileRow fileRow = null;
		if (selectedLobby != null)
		{
			num = selectedLobby.identifier;
		}
		lobbyRows.Clear();
		for (int i = 0; i < lobbies.Count; i++)
		{
			Platform_Multiplayer.MPLobby mPLobby = lobbies[i];
			if (mPLobby.settings == "" || mPLobby.maxPlayers == "")
			{
				continue;
			}
			FileRow fileRow2 = new FileRow();
			fileRow2.Text1 = mPLobby.gameName;
			fileRow2.Text3 = mPLobby.numLobbyMembers + "/" + mPLobby.AIPlayers + "/" + mPLobby.maxPlayers;
			fileRow2.Text4 = mPLobby.country;
			fileRow2.lobby = mPLobby;
			string text = "";
			if (EngineInterface.MultiplayerSetupData.compareSettingsStrings(defaultMPSettings, mPLobby.settings))
			{
				EngineInterface.MultiplayerSetupData multiplayerSetupData = new EngineInterface.MultiplayerSetupData();
				multiplayerSetupData.FromString(mPLobby.settings);
				if (mPLobby.gameTypeCoop == "1")
				{
					if (multiplayerSetupData.extreme_troops == 0)
					{
						if (multiplayerSetupData.advanced_options == 0)
						{
							fileRow2.TypeImage = MainViewModel.Instance.GameSprites[725];
						}
						else
						{
							fileRow2.TypeImage = MainViewModel.Instance.GameSprites[726];
						}
					}
					else if (multiplayerSetupData.advanced_options == 0)
					{
						fileRow2.TypeImage = MainViewModel.Instance.GameSprites[727];
					}
					else
					{
						fileRow2.TypeImage = MainViewModel.Instance.GameSprites[728];
					}
				}
				else if (multiplayerSetupData.extreme_troops == 0)
				{
					if (multiplayerSetupData.advanced_options == 0)
					{
						fileRow2.TypeImage = MainViewModel.Instance.GameSprites[665];
					}
					else
					{
						fileRow2.TypeImage = MainViewModel.Instance.GameSprites[666];
					}
				}
				else if (multiplayerSetupData.advanced_options == 0)
				{
					fileRow2.TypeImage = MainViewModel.Instance.GameSprites[669];
				}
				else
				{
					fileRow2.TypeImage = MainViewModel.Instance.GameSprites[670];
				}
			}
			else if (mPLobby.gameTypeCoop == "1")
			{
				fileRow2.TypeImage = MainViewModel.Instance.GameSprites[729];
			}
			else
			{
				fileRow2.TypeImage = null;
			}
			string description = "";
			string text2 = mPLobby.mapFileName.Replace(".map", "");
			string text3 = Translate.Instance.translateMapNames(text2, ref description);
			if (description == "" && text2 == text3)
			{
				fileRow2.Text2 = text + mPLobby.mapName;
			}
			else
			{
				fileRow2.Text2 = text + text3;
			}
			lobbyRows.Add(fileRow2);
			if (mPLobby.identifier == num)
			{
				fileRow = fileRow2;
			}
		}
		((ItemsControl)RefLobbyLists).ItemsSource = lobbyRows;
		if (fileRow != null)
		{
			((Selector)RefLobbyLists).SelectedItem = fileRow;
			return;
		}
		Button refLobbySettingsButton = RefLobbySettingsButton;
		bool isEnabled = (((UIElement)RefJoinButton).IsEnabled = false);
		((UIElement)refLobbySettingsButton).IsEnabled = isEnabled;
		((UIElement)RefLobbySettingsButton).Visibility = (Visibility)1;
	}

	public void LobbyListHeaderClickedHandler(object sender, RoutedEventArgs e)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		switch (((FrameworkElement)(GridViewColumnHeader)e.Source).Tag as string)
		{
		case "Name":
			if (sortByColumn == 0)
			{
				sortByAscending = !sortByAscending;
				break;
			}
			sortByColumn = 0;
			sortByAscending = true;
			break;
		case "Date":
			if (sortByColumn == 1)
			{
				sortByAscending = !sortByAscending;
				break;
			}
			sortByColumn = 1;
			sortByAscending = false;
			break;
		}
		populateLobbyList();
	}

	public void updateRadarTexture(FileHeader header)
	{
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Expected O, but got Unknown
		MainViewModel.Instance.Show_Radar160Border = false;
		MainViewModel.Instance.Show_Radar300Border = false;
		MainViewModel.Instance.Show_Radar500Border = false;
		MainViewModel.Instance.Show_Radar700Border = false;
		if (header != null)
		{
			MainViewModel.Instance.Show_MPRadar = true;
			switch (header.world_size)
			{
			case 160:
				MainViewModel.Instance.Show_Radar160Border = true;
				break;
			case 300:
				MainViewModel.Instance.Show_Radar300Border = true;
				break;
			case 500:
				MainViewModel.Instance.Show_Radar500Border = true;
				break;
			case 700:
				MainViewModel.Instance.Show_Radar700Border = true;
				break;
			}
			byte[] radarFromFile = MapFileManager.Instance.GetRadarFromFile(header.filePath);
			if (radarFromFile != null)
			{
				TextureSource radarStandaloneImage = new TextureSource(MapFileManager.Instance.GetRadarPreview(radarFromFile));
				MainViewModel.Instance.RadarStandaloneImage = (ImageSource)(object)radarStandaloneImage;
			}
		}
		else
		{
			MainViewModel.Instance.Show_MPRadar = false;
		}
	}

	public void Include_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			populateMapList(selectedMPHeader, ignoreRefresh: true);
		}
	}

	public void CreateRandomSkirmish()
	{
		int minSize = 160;
		int maxSize = 160;
		float value = ((RangeBase)RefMapSizeMin_Slider).Value;
		if (value <= 3f)
		{
			if (value <= 1f)
			{
				if (value != 0f)
				{
					if (value == 1f)
					{
						minSize = 200;
					}
				}
				else
				{
					minSize = 160;
				}
			}
			else if (value != 2f)
			{
				if (value == 3f)
				{
					minSize = 400;
				}
			}
			else
			{
				minSize = 300;
			}
		}
		else if (value <= 5f)
		{
			if (value != 4f)
			{
				if (value == 5f)
				{
					minSize = 600;
				}
			}
			else
			{
				minSize = 500;
			}
		}
		else if (value != 6f)
		{
			if (value == 7f)
			{
				minSize = 800;
			}
		}
		else
		{
			minSize = 700;
		}
		value = ((RangeBase)RefMapSizeMax_Slider).Value;
		if (value <= 3f)
		{
			if (value <= 1f)
			{
				if (value != 0f)
				{
					if (value == 1f)
					{
						maxSize = 200;
					}
				}
				else
				{
					maxSize = 160;
				}
			}
			else if (value != 2f)
			{
				if (value == 3f)
				{
					maxSize = 400;
				}
			}
			else
			{
				maxSize = 300;
			}
		}
		else if (value <= 5f)
		{
			if (value != 4f)
			{
				if (value == 5f)
				{
					maxSize = 600;
				}
			}
			else
			{
				maxSize = 500;
			}
		}
		else if (value != 6f)
		{
			if (value == 7f)
			{
				maxSize = 800;
			}
		}
		else
		{
			maxSize = 700;
		}
		int num = (int)((RangeBase)RefAIMin_Slider).Value;
		if (((ToggleButton)RefRandomIncludeBuiltin).IsChecked.Value && !((ToggleButton)RefIncludeBuiltin).IsChecked.Value)
		{
			((ToggleButton)RefIncludeBuiltin).IsChecked = true;
		}
		if (((ToggleButton)RefRandomIncludeUser).IsChecked.Value && !((ToggleButton)RefIncludeUser).IsChecked.Value)
		{
			((ToggleButton)RefIncludeUser).IsChecked = true;
		}
		if (((ToggleButton)RefRandomIncludeWorkshop).IsChecked.Value && !((ToggleButton)RefIncludeWorkshop).IsChecked.Value)
		{
			((ToggleButton)RefIncludeWorkshop).IsChecked = true;
		}
		FileHeader randomMultiplayerMap = MapFileManager.Instance.GetRandomMultiplayerMap(num + 1, minSize, maxSize, ((ToggleButton)RefRandomIncludeBuiltin).IsChecked.Value, ((ToggleButton)RefRandomIncludeUser).IsChecked.Value, ((ToggleButton)RefRandomIncludeWorkshop).IsChecked.Value);
		if (randomMultiplayerMap == null)
		{
			return;
		}
		currentLobby.maxPlayers = randomMultiplayerMap.maxPlayers.ToString();
		populateMapList(randomMultiplayerMap);
		Random random = new Random();
		int num2 = (int)((RangeBase)RefAIMax_Slider).Value;
		int num3 = random.Next(num, num2 + 1);
		if (spectatorMode)
		{
			num3++;
		}
		SkirmishAIAddClick((-num3).ToString());
		if (((ToggleButton)RefRandomBalance).IsChecked.Value)
		{
			switch (random.Next(5))
			{
			case 0:
				MPsetupData.fairness = 1;
				updateSkirmishStartingGoldLevels();
				MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 3);
				break;
			case 1:
				MPsetupData.fairness = 2;
				updateSkirmishStartingGoldLevels();
				MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 3);
				break;
			case 2:
				MPsetupData.fairness = 3;
				updateSkirmishStartingGoldLevels();
				MainViewModel.Instance.MPGame_Advantage = "";
				break;
			case 3:
				MPsetupData.fairness = 4;
				updateSkirmishStartingGoldLevels();
				MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 4);
				break;
			case 4:
				MPsetupData.fairness = 5;
				updateSkirmishStartingGoldLevels();
				MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 4);
				break;
			}
			switch (random.Next(3))
			{
			case 0:
				MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 0);
				MPsetupData.starting_goods_level = 1;
				updateSkirmishStartingGoldLevels();
				break;
			case 1:
				MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 1);
				MPsetupData.starting_goods_level = 2;
				updateSkirmishStartingGoldLevels();
				break;
			case 2:
				MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 2);
				MPsetupData.starting_goods_level = 3;
				updateSkirmishStartingGoldLevels();
				break;
			}
			SetupSkirmishModeSettings();
		}
		if (((ToggleButton)RefRandomExtreme).IsChecked.Value)
		{
			MPsetupData.extreme_troops = random.Next(2);
			if (MPsetupData.extreme_troops > 0)
			{
				MainViewModel.Instance.MP_Settings_ExTroops = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_ExTroops = MainViewModel.Instance.GameSprites[641];
			}
			MPsetupData.extreme_powers = random.Next(2);
			if (MPsetupData.extreme_powers == 0)
			{
				MPsetupData.extreme_powers_around_lord = 0;
				MainViewModel.Instance.MPSettings_ExPowersLord_Opacity = 0.5f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_ExPowersLord_Opacity = 1f;
				MPsetupData.extreme_powers_around_lord = random.Next(2);
				if (MPsetupData.extreme_powers_around_lord > 0)
				{
					MainViewModel.Instance.MP_Settings_ExPowersLord = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_ExPowersLord = MainViewModel.Instance.GameSprites[641];
				}
			}
			if (MPsetupData.extreme_powers > 0)
			{
				MainViewModel.Instance.MP_Settings_ExPowers = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_ExPowers = MainViewModel.Instance.GameSprites[641];
				MainViewModel.Instance.MP_Settings_ExPowersLord = MainViewModel.Instance.GameSprites[641];
			}
		}
		if (((ToggleButton)RefRandomOutposts).IsChecked.Value)
		{
			MPsetupData.allow_outposts = random.Next(2);
			if (MPsetupData.allow_outposts > 0)
			{
				MainViewModel.Instance.MP_Settings_AllowOutposts = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_AllowOutposts = MainViewModel.Instance.GameSprites[641];
			}
		}
		if (((ToggleButton)RefRandomAdvanced).IsChecked.Value)
		{
			MPsetupData.advanced_skirmish_options = 1;
			MPsetupData.global_improved_sieging = random.Next(2);
			if (MPsetupData.global_improved_sieging > 0)
			{
				MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[641];
			}
			MPsetupData.advopt_pre_build = random.Next(2);
			if (MPsetupData.advopt_pre_build > 0)
			{
				MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[641];
			}
			MPsetupData.advopt_improved_arabswordsmen = random.Next(2);
			if (MPsetupData.advopt_improved_arabswordsmen > 0)
			{
				MainViewModel.Instance.MP_Settings_Adv_ASword = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Adv_ASword = MainViewModel.Instance.GameSprites[641];
			}
			MPsetupData.advopt_rebalanced_horsearchers = random.Next(2);
			if (MPsetupData.advopt_rebalanced_horsearchers > 0)
			{
				MainViewModel.Instance.MP_Settings_Adv_HorseArchers = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Adv_HorseArchers = MainViewModel.Instance.GameSprites[641];
			}
			MPsetupData.advopt_improved_laddermen = random.Next(2);
			if (MPsetupData.advopt_improved_laddermen > 0)
			{
				MainViewModel.Instance.MP_Settings_Adv_Laddermen = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Adv_Laddermen = MainViewModel.Instance.GameSprites[641];
			}
			MPsetupData.advopt_improved_spearmen = random.Next(2);
			if (MPsetupData.advopt_improved_spearmen > 0)
			{
				MainViewModel.Instance.MP_Settings_Adv_Spearmen = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Adv_Spearmen = MainViewModel.Instance.GameSprites[641];
			}
			MPsetupData.advopt_improved_fletchers = random.Next(2);
			if (MPsetupData.advopt_improved_fletchers > 0)
			{
				MainViewModel.Instance.MP_Settings_Adv_Fletchers = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Adv_Fletchers = MainViewModel.Instance.GameSprites[641];
			}
			MPsetupData.advopt_uncapped_peasants = random.Next(2);
			if (MPsetupData.advopt_uncapped_peasants > 0)
			{
				MainViewModel.Instance.MP_Settings_Uncapped_Peasants = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Uncapped_Peasants = MainViewModel.Instance.GameSprites[641];
			}
			MPsetupData.advopt_faster_peasants = random.Next(2);
			if (MPsetupData.advopt_faster_peasants > 0)
			{
				MainViewModel.Instance.MP_Settings_Faster_Peasants = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Faster_Peasants = MainViewModel.Instance.GameSprites[641];
			}
			MPsetupData.advopt_healers = random.Next(2);
			if (MPsetupData.advopt_healers > 0)
			{
				MainViewModel.Instance.MP_Settings_Healers = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Healers = MainViewModel.Instance.GameSprites[641];
			}
			MPsetupData.advopt_eunuchs = random.Next(2);
			if (MPsetupData.advopt_eunuchs > 0)
			{
				MainViewModel.Instance.MP_Settings_Eunuchs = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Eunuchs = MainViewModel.Instance.GameSprites[641];
			}
			MPsetupData.advopt_nogold = random.Next(2);
			if (MPsetupData.advopt_nogold > 0)
			{
				MainViewModel.Instance.MP_Settings_NoGold = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_NoGold = MainViewModel.Instance.GameSprites[641];
			}
			MPsetupData.advopt_enemy_hps = random.Next(4);
			string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 413) + " ";
			switch (MPsetupData.advopt_enemy_hps)
			{
			case 0:
				text += "66%";
				break;
			case 1:
				text += "100%";
				break;
			case 2:
				text += "125%";
				break;
			case 3:
				text += "150%";
				break;
			}
			MainViewModel.Instance.MP_Settings_enemyhps = text;
		}
		if (((ToggleButton)RefRandomTeams).IsChecked.Value)
		{
			List<Platform_Multiplayer.MPLobbyMember> members = currentLobby.members;
			if (members.Count > 2)
			{
				for (int i = 0; i < members.Count; i++)
				{
					int playerID = team_order[i + 1];
					Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID = currentLobby.GetLobbyMemberFromThis_PlayerID(playerID);
					lobbyMemberFromThis_PlayerID.teamShield = -1;
					orderTeamMembers[i] = lobbyMemberFromThis_PlayerID;
				}
				int num4 = random.Next(members.Count - 1) + 2;
				int[] array = new int[members.Count];
				for (int j = 0; j < num4; j++)
				{
					array[j] = j + 1;
				}
				for (int k = num4; k < members.Count; k++)
				{
					array[k] = random.Next(num4) + 1;
				}
				for (int l = 0; l < members.Count; l++)
				{
					int num5 = random.Next(members.Count);
					int num6 = array[l];
					array[l] = array[num5];
					array[num5] = num6;
				}
				for (int m = 0; m < members.Count; m++)
				{
					currentLobby.setTeam(members[m], array[m]);
				}
			}
		}
		PopulateTeamsPanel();
		CreateTeamShields();
		UpdateRadarShieldPositions();
		UpdateHostInfo();
	}

	public void updateRandomSkirmishPanel()
	{
		float value = ((RangeBase)RefMapSizeMin_Slider).Value;
		if (value <= 3f)
		{
			if (value <= 1f)
			{
				if (value != 0f)
				{
					if (value == 1f)
					{
						MainViewModel.Instance.MP_RandomMapSizeMin = "200";
					}
				}
				else
				{
					MainViewModel.Instance.MP_RandomMapSizeMin = "160";
				}
			}
			else if (value != 2f)
			{
				if (value == 3f)
				{
					MainViewModel.Instance.MP_RandomMapSizeMin = "400";
				}
			}
			else
			{
				MainViewModel.Instance.MP_RandomMapSizeMin = "300";
			}
		}
		else if (value <= 5f)
		{
			if (value != 4f)
			{
				if (value == 5f)
				{
					MainViewModel.Instance.MP_RandomMapSizeMin = "600";
				}
			}
			else
			{
				MainViewModel.Instance.MP_RandomMapSizeMin = "500";
			}
		}
		else if (value != 6f)
		{
			if (value == 7f)
			{
				MainViewModel.Instance.MP_RandomMapSizeMin = "800";
			}
		}
		else
		{
			MainViewModel.Instance.MP_RandomMapSizeMin = "700";
		}
		value = ((RangeBase)RefMapSizeMax_Slider).Value;
		if (value <= 3f)
		{
			if (value <= 1f)
			{
				if (value != 0f)
				{
					if (value == 1f)
					{
						MainViewModel.Instance.MP_RandomMapSizeMax = "200";
					}
				}
				else
				{
					MainViewModel.Instance.MP_RandomMapSizeMax = "160";
				}
			}
			else if (value != 2f)
			{
				if (value == 3f)
				{
					MainViewModel.Instance.MP_RandomMapSizeMax = "400";
				}
			}
			else
			{
				MainViewModel.Instance.MP_RandomMapSizeMax = "300";
			}
		}
		else if (value <= 5f)
		{
			if (value != 4f)
			{
				if (value == 5f)
				{
					MainViewModel.Instance.MP_RandomMapSizeMax = "600";
				}
			}
			else
			{
				MainViewModel.Instance.MP_RandomMapSizeMax = "500";
			}
		}
		else if (value != 6f)
		{
			if (value == 7f)
			{
				MainViewModel.Instance.MP_RandomMapSizeMax = "800";
			}
		}
		else
		{
			MainViewModel.Instance.MP_RandomMapSizeMax = "700";
		}
		int num = 0;
		if (spectatorMode)
		{
			num = 1;
		}
		MainViewModel.Instance.MP_RandomAIMin = ((int)((RangeBase)RefAIMin_Slider).Value + num).ToString();
		MainViewModel.Instance.MP_RandomAIMax = ((int)((RangeBase)RefAIMax_Slider).Value + num).ToString();
	}

	public void MapSizeMax_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)((RangeBase)RefMapSizeMax_Slider).Value;
		if (!insideValueChanged)
		{
			insideValueChanged = true;
			if (num < (int)((RangeBase)RefMapSizeMin_Slider).Value)
			{
				((RangeBase)RefMapSizeMin_Slider).Value = num;
			}
			insideValueChanged = false;
		}
		updateRandomSkirmishPanel();
	}

	public void MapSizeMin_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)((RangeBase)RefMapSizeMin_Slider).Value;
		if (!insideValueChanged)
		{
			insideValueChanged = true;
			if (num > (int)((RangeBase)RefMapSizeMax_Slider).Value)
			{
				((RangeBase)RefMapSizeMax_Slider).Value = num;
			}
			insideValueChanged = false;
		}
		updateRandomSkirmishPanel();
	}

	public void AIMax_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)((RangeBase)RefAIMax_Slider).Value;
		if (!insideValueChanged)
		{
			insideValueChanged = true;
			if (num < (int)((RangeBase)RefAIMin_Slider).Value)
			{
				((RangeBase)RefAIMin_Slider).Value = num;
			}
			insideValueChanged = false;
		}
		updateRandomSkirmishPanel();
	}

	public void AIMin_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)((RangeBase)RefAIMin_Slider).Value;
		if (!insideValueChanged)
		{
			insideValueChanged = true;
			if (num > (int)((RangeBase)RefAIMax_Slider).Value)
			{
				((RangeBase)RefAIMax_Slider).Value = num;
			}
			insideValueChanged = false;
		}
		updateRandomSkirmishPanel();
	}

	public void ButtonClicked(string param)
	{
		if (closePanelDisplayed)
		{
			return;
		}
		switch (param)
		{
		case "Back":
			if (SelectedRadarKeep != -1)
			{
				SelectedRadarKeep = -1;
				MainViewModel.Instance.Show_SkirmishUIOnRadar = false;
				UpdateRadarShieldPositions();
			}
			else if (SelectedFace != -1)
			{
				SelectedFace = -1;
				((UIElement)RefTeamFaceCancel).IsEnabled = false;
				PopulateTeamsPanel();
			}
			else if (MainViewModel.Instance.Show_CoopHidePanel)
			{
				MainViewModel.Instance.Show_CoopHidePanel = false;
				coopFriendsPage = 0;
				CoopPopulateFriendsList();
			}
			else if (MainViewModel.Instance.Show_CoopOptions && coopGame)
			{
				ButtonClicked("CloseCoopOptions");
			}
			else if (MainViewModel.Instance.Show_SkirmishTeamsPanel)
			{
				CreateTeamShields();
				UpdateRadarShieldPositions();
				MainViewModel.Instance.AlliesFace = null;
				MainViewModel.Instance.AlliesFaceBackground = null;
				MainViewModel.Instance.AlliesHumanFaceVisible = false;
				MainViewModel.Instance.Show_SkirmishTeamsPanel = false;
				if (currentLobby.numLobbyMembers > 1)
				{
					SFXManager.instance.playGenieSpeech(3, "Genie_02.wav", 1f);
				}
			}
			else if (MainViewModel.Instance.Show_CoopAIAllyPanel)
			{
				MainViewModel.Instance.Show_CoopAIAllyPanel = false;
				MainViewModel.Instance.Show_CoopMapIcons = true;
			}
			else if (MainViewModel.Instance.Show_MP_SkirmishAdvanced)
			{
				ButtonClicked("CloseSkirmishAdvanced");
			}
			else if (MainViewModel.Instance.Show_AddAIPanel)
			{
				MainViewModel.Instance.Show_AddAIPanel = false;
			}
			else if (MainViewModel.Instance.Show_SkirmishRandomAIPanel || MainViewModel.Instance.Show_AdvancedRandom)
			{
				MainViewModel.Instance.Show_SkirmishRandomAIPanel = false;
				MainViewModel.Instance.Show_AdvancedRandom = false;
			}
			else if (MainViewModel.Instance.Show_MPSettings)
			{
				MainViewModel.Instance.Show_MPSettings = false;
			}
			else if (MainViewModel.Instance.Show_MPAISettings)
			{
				MainViewModel.Instance.Show_MPAISettings = false;
				if (!skirmishGame)
				{
					UpdateHostInfo();
				}
			}
			else if (MainViewModel.Instance.Show_ManageTrail)
			{
				if (MainViewModel.Instance.Show_TM_Import)
				{
					MainViewModel.Instance.Show_TM_Import = false;
				}
				else if (MainViewModel.Instance.Show_TM_Export)
				{
					MainViewModel.Instance.Show_TM_Export = false;
				}
				else
				{
					MainViewModel.Instance.Show_ManageTrail = false;
				}
			}
			else if (skirmishGame)
			{
				closePanelDisplayed = true;
				HUD_ConfirmationPopup.ShowConfirmation(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 212), delegate
				{
					closePanelDisplayed = false;
					LeaveLobby(doLeaveOnSteam: true, refreshLobbyList: false);
					if (customizedTrail)
					{
						switch (customizedTrailType)
						{
						case 0:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Trail");
							break;
						case 1:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Trail2");
							break;
						case 2:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Trail3");
							break;
						case 11:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands1");
							break;
						case 12:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands2");
							break;
						case 13:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands3");
							break;
						case 14:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands4");
							break;
						case 15:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands5");
							break;
						case 16:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands6");
							break;
						case 17:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands7");
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
					else if (!trailMakerMode)
					{
						MainViewModel.Instance.FrontEndMenu.ButtonClicked("Skirmish");
					}
					else
					{
						MainViewModel.Instance.FrontEndMenu.ButtonClicked("MapEditor");
					}
				}, delegate
				{
					closePanelDisplayed = false;
				}, MPConf: true);
			}
			else if (MainViewModel.Instance.Show_MPSharing)
			{
				MainViewModel.Instance.Show_MPSharing = false;
				MainViewModel.Instance.Show_CoopMapIcons = true;
			}
			else if (MainViewModel.Instance.Show_MPColours)
			{
				MainViewModel.Instance.Show_MPColours = false;
			}
			else if (MainViewModel.Instance.Show_MPLobbySettings)
			{
				MainViewModel.Instance.Show_MPLobbySettings = false;
			}
			else if (coopGame)
			{
				closePanelDisplayed = true;
				HUD_ConfirmationPopup.ShowConfirmation(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 212), delegate
				{
					closePanelDisplayed = false;
					LeaveLobby(doLeaveOnSteam: true, refreshLobbyList: false);
					MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coops");
				}, delegate
				{
					closePanelDisplayed = false;
				}, MPConf: true);
			}
			else if (MainViewModel.Instance.Show_MPJoiningLobby)
			{
				LeaveLobby();
				MainViewModel.Instance.FrontEndMenu.ButtonClicked("BackMain");
			}
			else
			{
				closePanelDisplayed = true;
				HUD_ConfirmationPopup.ShowConfirmation(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 212), delegate
				{
					closePanelDisplayed = false;
					LeaveLobby();
					ShowLobbyScreen();
				}, delegate
				{
					closePanelDisplayed = false;
				}, MPConf: true);
			}
			break;
		case "Host":
			MainViewModel.Instance.Show_CreatingMPHost = true;
			((RangeBase)RefLobbyMaxPlayersSlider).Value = 8f;
			FRONT_Multiplayer_Setup.ResetMaxPlayers();
			MainViewModel.Instance.MPCreateMaxPlayers = "8";
			PlayerCap = 8;
			MPHostLobbyname = "";
			updateHostLobbyButton();
			if (SteamManager.Initialized)
			{
				string personaName = SteamFriends.GetPersonaName();
				MPHostLobbyname = personaName + " Game";
				RefTextBoxGameName.Text = MPHostLobbyname;
			}
			break;
		case "Join":
		{
			bool flag2 = false;
			if (selectedLobby != null)
			{
				string settings = selectedLobby.settings;
				MPTEMPsetupData = new EngineInterface.MultiplayerSetupData();
				MPTEMPsetupData.FromString(settings);
				if (MPTEMPsetupData.extreme_troops > 0)
				{
					flag2 = true;
				}
			}
			if (!skirmishExtremeTroopsWarningShown && ConfigSettings.Settings_Show_Extreme_Warning && flag2)
			{
				MainViewModel.Instance.Show_MP_ExtremeWarning = true;
				ExtremeWarningSource = 1;
				skirmishExtremeTroopsWarningShown = true;
				break;
			}
			Platform_Multiplayer.Instance.JoinLobby(selectedLobby, delegate
			{
				if (Platform_Multiplayer.Instance.activeLobby == null)
				{
					showLobbyUnavailableMessage = true;
				}
				else
				{
					updateSteamIDMappings();
					currentLobby = selectedLobby;
					ShowSetupScreen();
					headerlist = MapFileManager.Instance.GetMultiplayerMaps(sortByColumn, sortByAscending, numConnectedPlayers, includeBuiltIn, includeUser, includeWorkshop);
				}
			}, delegate(string name, string message, int colourID)
			{
				receivedLobbyChat(name, message, colourID);
			});
			break;
		}
		case "ShareJoin":
			if (LatestSharedCode != 0)
			{
				Platform_Multiplayer.Instance.SetInviteLobbyID(LatestSharedCode);
				Platform_Multiplayer.MPLobby joiningLobby = null;
				Platform_Multiplayer.Instance.AutoJoinPendingLobby(ref joiningLobby, delegate
				{
					AutoJoinLobby(joiningLobby);
				}, delegate(string name, string message, int colourID)
				{
					receivedLobbyChat(name, message, colourID);
				});
				Platform_Multiplayer.Instance.PendingMPLobby = false;
				LatestSharedCode = 0uL;
				RefMP_EnterShareCodeText.Text = "";
			}
			break;
		case "Refresh":
			lastAutoRefreshTime = DateTime.MinValue;
			break;
		case "RegionDefault":
			if (matchmakingDefault != 1)
			{
				matchmakingDefault = 1;
				lastAutoRefreshTime = DateTime.MinValue;
				UpdateMatchmakingButton();
			}
			break;
		case "RegionLocal":
			if (matchmakingDefault != 0)
			{
				matchmakingDefault = 0;
				lastAutoRefreshTime = DateTime.MinValue;
				UpdateMatchmakingButton();
			}
			break;
		case "RegionGlobal":
			if (matchmakingDefault != 2)
			{
				matchmakingDefault = 2;
				lastAutoRefreshTime = DateTime.MinValue;
				UpdateMatchmakingButton();
			}
			break;
		case "TogglePublic":
			if (MPLobbyMode == 0)
			{
				MPLobbyMode = 4;
			}
			else
			{
				MPLobbyMode = 0;
			}
			updateHostLobbyButton();
			break;
		case "ToggleGameType":
			MPGameType++;
			if (MPGameType > 3)
			{
				MPGameType = 0;
			}
			((UIElement)RefLobbyMaxPlayersSlider).IsEnabled = MPGameType < 2;
			MainViewModel.Instance.Show_MPLobbyMaxPlayers = MPGameType < 2;
			updateHostLobbyButton();
			break;
		case "ToggleSettings":
			MPStartingSettings++;
			if (MPStartingSettings == 1 && MPLastSetupData == null)
			{
				MPStartingSettings++;
			}
			if (MPStartingSettings == 2 && ConfigSettings.Settings_MPPresets1.Length == 0)
			{
				MPStartingSettings++;
			}
			if (MPStartingSettings == 3 && ConfigSettings.Settings_MPPresets2.Length == 0)
			{
				MPStartingSettings++;
			}
			if (MPStartingSettings > 3)
			{
				MPStartingSettings = 0;
			}
			updateHostLobbyButton();
			break;
		case "ChangeLobbyType":
			if (MPLobbyMode == 0)
			{
				MPLobbyMode = 4;
				Platform_Multiplayer.Instance.ChangeLobbyType(4);
			}
			else
			{
				MPLobbyMode = 0;
				Platform_Multiplayer.Instance.ChangeLobbyType(0);
			}
			UpdateLobbyChangeButtons();
			break;
		case "DoHost":
			if ((MPGameType == 1 || MPGameType == 3) && !skirmishExtremeTroopsWarningShown && ConfigSettings.Settings_Show_Extreme_Warning)
			{
				MainViewModel.Instance.Show_MP_ExtremeWarning = true;
				ExtremeWarningSource = 0;
				skirmishExtremeTroopsWarningShown = true;
				break;
			}
			switch (MPStartingSettings)
			{
			case 0:
				ButtonClicked("DoHostDefault");
				break;
			case 1:
				ButtonClicked("DoHostPrevious");
				break;
			case 2:
				ButtonClicked("DoHostPresets1");
				break;
			case 3:
				ButtonClicked("DoHostPresets2");
				break;
			}
			break;
		case "DoHostPrevious":
			if (RefTextBoxGameName.Text.Length > 0)
			{
				string str4 = MPLastSetupData.ToString();
				MPsetupData.FromString(str4, ignoreKeepOrder: true);
				if (MPGameType == 1 || MPGameType == 3)
				{
					MPsetupData.extreme_troops = 1;
				}
				else
				{
					MPsetupData.extreme_troops = 0;
				}
				customCoopGame = MPGameType == 2 || MPGameType == 3;
				MainViewModel.Instance.Show_SkirmishTeams = !customCoopGame;
				EngineInterface.setMultiplayerStartingData(MPsetupData);
				pendingMPHost = true;
				MainViewModel.Instance.Show_MPJoiningLobby = false;
				MainViewModel.Instance.Show_CreatingMPHost = false;
			}
			break;
		case "DoHostDefault":
			if (RefTextBoxGameName.Text.Length > 0)
			{
				string str3 = MPDefaultsetupData.ToString();
				MPsetupData.FromString(str3, ignoreKeepOrder: true);
				if (MPGameType == 1 || MPGameType == 3)
				{
					MPsetupData.extreme_troops = 1;
				}
				else
				{
					MPsetupData.extreme_troops = 0;
				}
				customCoopGame = MPGameType == 2 || MPGameType == 3;
				MainViewModel.Instance.Show_SkirmishTeams = !customCoopGame;
				EngineInterface.setMultiplayerStartingData(MPsetupData);
				pendingMPHost = true;
				MainViewModel.Instance.Show_MPJoiningLobby = false;
				MainViewModel.Instance.Show_CreatingMPHost = false;
			}
			break;
		case "DoHostPresets1":
			if (RefTextBoxGameName.Text.Length > 0)
			{
				pendingMPHost = true;
				MPsetupData.FromString(ConfigSettings.Settings_MPPresets1, ignoreKeepOrder: true);
				if (MPGameType == 1 || MPGameType == 3)
				{
					MPsetupData.extreme_troops = 1;
				}
				else
				{
					MPsetupData.extreme_troops = 0;
				}
				customCoopGame = MPGameType == 2 || MPGameType == 3;
				MainViewModel.Instance.Show_SkirmishTeams = !customCoopGame;
				EngineInterface.setMultiplayerStartingData(MPsetupData);
				MainViewModel.Instance.Show_MPJoiningLobby = false;
				MainViewModel.Instance.Show_CreatingMPHost = false;
			}
			break;
		case "DoHostPresets2":
			if (RefTextBoxGameName.Text.Length > 0)
			{
				pendingMPHost = true;
				MPsetupData.FromString(ConfigSettings.Settings_MPPresets2, ignoreKeepOrder: true);
				if (MPGameType == 1 || MPGameType == 3)
				{
					MPsetupData.extreme_troops = 1;
				}
				else
				{
					MPsetupData.extreme_troops = 0;
				}
				customCoopGame = MPGameType == 2 || MPGameType == 3;
				MainViewModel.Instance.Show_SkirmishTeams = !customCoopGame;
				EngineInterface.setMultiplayerStartingData(MPsetupData);
				MainViewModel.Instance.Show_MPJoiningLobby = false;
				MainViewModel.Instance.Show_CreatingMPHost = false;
			}
			break;
		case "CancelHost":
			MainViewModel.Instance.Show_CreatingMPHost = false;
			break;
		case "Invite":
			Platform_Multiplayer.Instance.InviteOverlay();
			break;
		case "Ready":
			if (MPMapValid || currentLobby.isHost)
			{
				MPLocalReady = !MPLocalReady;
			}
			else
			{
				MPLocalReady = false;
			}
			Platform_Multiplayer.Instance.SetMemberReadyState(MPLocalReady);
			break;
		case "ReadyLock":
			if (MPLocalReady)
			{
				MPLocalReadyLocked = !MPLocalReadyLocked;
			}
			break;
		case "CoopOptions":
			SetupSkirmishModeSettings();
			MainViewModel.Instance.Show_CoopOptions = true;
			break;
		case "CloseCoopOptions":
			MainViewModel.Instance.Show_CoopOptions = false;
			UpdateHostInfo();
			break;
		case "SwapCoop":
			coopOrderSwapped = !coopOrderSwapped;
			currentLobby.coopOrderSwapped = coopOrderSwapped;
			if (currentLobby != null)
			{
				CoopMissionChanged(currentLobby.coopTrailID, currentLobby.coopSelectedMission);
			}
			break;
		case "Play":
			if (skirmishGame)
			{
				StartSkirmishGame();
				break;
			}
			if (!skirmishGame)
			{
				string str2 = MPsetupData.ToString();
				if (MPLastSetupData == null)
				{
					MPLastSetupData = new EngineInterface.MultiplayerSetupData();
				}
				MPLastSetupData.FromString(str2);
			}
			UpdateHostInfo();
			Platform_Multiplayer.Instance.HostStartGame();
			startGameTime = DateTime.UtcNow.AddMilliseconds(500.0);
			break;
		case "Load":
			MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
			if (!skirmishGame)
			{
				if (coopGame && (singlePlayerCoop || MainViewModel.Instance.Show_CoopHostInvitePane))
				{
					HUD_LoadSaveRequester.OpenLoadSaveRequester(Enums.RequesterTypes.LoadSinglePlayerCoopGame, delegate(string filename, FileHeader header)
					{
						MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MainGame);
						Director.instance.SetPausedState(state: false);
						EditorDirector.instance.stopGameSim();
						EditorDirector.instance.loadSaveGame(header.filePath, header.standAlone_filename, header);
						MainViewModel.Instance.InitObjectiveGoodsPanelDelayed();
					}, delegate
					{
						updateRadarTexture(selectedMPHeader);
					}, -1, skirmishScreen: true);
					break;
				}
				Platform_Multiplayer.Instance.SendSaveCRCs(coopGame);
				Enums.RequesterTypes reqType = Enums.RequesterTypes.LoadMultiplayerGame;
				if (coopGame)
				{
					reqType = Enums.RequesterTypes.LoadMultiplayerCoopGame;
				}
				HUD_LoadSaveRequester.OpenLoadSaveRequester(reqType, delegate(string filename, FileHeader header)
				{
					Platform_Multiplayer.Instance.HostLoadGame(header.fileName);
					startGameTime = DateTime.UtcNow.AddMilliseconds(500.0);
				}, delegate
				{
					updateRadarTexture(selectedMPHeader);
				}, currentLobby.CountHumanPlayers() - 1);
			}
			else
			{
				HUD_LoadSaveRequester.OpenLoadSaveRequester(Enums.RequesterTypes.LoadSinglePlayerGame, delegate(string filename, FileHeader header)
				{
					MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MainGame);
					Director.instance.SetPausedState(state: false);
					EditorDirector.instance.stopGameSim();
					EditorDirector.instance.loadSaveGame(header.filePath, header.standAlone_filename, header);
					MainViewModel.Instance.InitObjectiveGoodsPanelDelayed();
				}, delegate
				{
				}, -1, skirmishScreen: true);
			}
			break;
		case "Options":
			MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
			HUD_Options.OpenOptions(fromIngameMenu: false, fromMP: true);
			break;
		case "Kick_1":
		case "Kick_2":
		case "Kick_3":
		case "Kick_4":
		case "Kick_5":
		case "Kick_6":
		case "Kick_7":
		case "Kick_8":
		{
			int num29 = param[param.Length - 1] - 49;
			num29 = playerRows[num29].playerID;
			Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID4 = currentLobby.GetLobbyMemberFromThis_PlayerID(num29);
			if (lobbyMemberFromThis_PlayerID4 == null)
			{
				break;
			}
			bool flag5 = false;
			if (!skirmishGame)
			{
				if (currentLobby.isHost && !lobbyMemberFromThis_PlayerID4.IsSelf())
				{
					if (!lobbyMemberFromThis_PlayerID4.SkirmishMember)
					{
						Platform_Multiplayer.Instance.KickMemberFromLobby(lobbyMemberFromThis_PlayerID4);
					}
					else
					{
						flag5 = true;
					}
				}
			}
			else
			{
				flag5 = true;
			}
			if (!flag5)
			{
				break;
			}
			if (lobbyMemberFromThis_PlayerID4.SkirmishMember && !lobbyMemberFromThis_PlayerID4.SkirmishHumanMember && playKickSpeech && !MyAudioManager.Instance.isSpeechPlaying(3))
			{
				int num30 = lobbyMemberFromThis_PlayerID4.GetLordType() + 1;
				if (num30 < KickPlayerSpeech.Length)
				{
					SFXManager.instance.playGenieSpeech(3, KickPlayerSpeech[num30], 1f);
				}
				else if (CustomisationFileManager.CustomMediaExists && !MyAudioManager.Instance.isSpeechPlaying(3))
				{
					string path2 = MapFileManager.SplitCustomTrailName(lobbyMemberFromThis_PlayerID4.customLordName);
					string text6 = Path.Combine(ConfigSettings.GetUserCustomMediaPath(), path2, "KICK_PLAYER.wav");
					if (File.Exists(text6))
					{
						MyAudioManager.Instance.PlaySpeech(3, "*", text6, force: true);
					}
				}
			}
			Platform_Multiplayer.Instance.kickSkirmishPlayer(lobbyMemberFromThis_PlayerID4.id.m_SteamID);
			currentLobby.validateTeams();
			updateSteamIDMappings();
			ReSortTeamInfo();
			UpdateHostInfo();
			CreateTeamShields();
			UpdateRadarShieldPositions();
			UpdateRandomAIButtons();
			break;
		}
		case "ShowAI":
			SFXManager.instance.playGenieSpeech(2, "Genie_03.wav", 1f);
			MainViewModel.Instance.Show_AddAIPanel = true;
			MainViewModel.Instance.Show_AddAIPanel_Normal = true;
			break;
		case "ShowRandomAI":
			SFXManager.instance.playUISound(252);
			UpdateRandomAIButtons();
			MainViewModel.Instance.Show_SkirmishRandomAIPanel = true;
			break;
		case "AdvancedRandom":
			MainViewModel.Instance.Show_AdvancedRandom = true;
			updateRandomSkirmishPanel();
			break;
		case "CreateRandomSkirmish":
			CreateRandomSkirmish();
			break;
		case "ShowTeams":
			SFXManager.instance.playGenieSpeech(3, "Genie_01.wav", 1f);
			MainViewModel.Instance.Show_SkirmishTeamsPanel = true;
			teampop_sultan_played = false;
			teampop_rat_played = false;
			PopulateTeamsPanel();
			break;
		case "RadarUp1":
		case "RadarUp2":
		case "RadarUp3":
		case "RadarUp4":
		case "RadarUp5":
		case "RadarUp6":
		case "RadarUp7":
		case "RadarUp8":
			if (skirmishGame || currentLobby.isHost)
			{
				int num26 = param[param.Length - 1] - 49;
				if (SelectedRadarKeep >= 0 && SelectedRadarKeep != num26)
				{
					ButtonClicked(param.Replace("Up", ""));
				}
			}
			break;
		case "Radar1":
		case "Radar2":
		case "Radar3":
		case "Radar4":
		case "Radar5":
		case "Radar6":
		case "Radar7":
		case "Radar8":
		{
			if ((!skirmishGame && !currentLobby.isHost) || MainViewModel.Instance.Show_SkirmishTeamsPanel)
			{
				break;
			}
			int num18 = param[param.Length - 1] - 49;
			if (SelectedRadarKeep < 0)
			{
				SelectedRadarKeep = num18;
				MainViewModel.Instance.Show_SkirmishUIOnRadar = true;
			}
			else
			{
				if (SelectedRadarKeep != num18)
				{
					int num19 = MPsetupData.start_keep_location_order[SelectedRadarKeep];
					MPsetupData.start_keep_location_order[SelectedRadarKeep] = MPsetupData.start_keep_location_order[num18];
					MPsetupData.start_keep_location_order[num18] = num19;
				}
				UpdateHostInfo();
				SelectedRadarKeep = -1;
				MainViewModel.Instance.Show_SkirmishUIOnRadar = false;
			}
			UpdateRadarShieldPositions();
			break;
		}
		case "RandShields":
			update_keep_locations_on_map_change();
			UpdateHostInfo();
			UpdateRadarShieldPositions();
			break;
		case "TeamFaceUp1":
		case "TeamFaceUp2":
		case "TeamFaceUp3":
		case "TeamFaceUp4":
		case "TeamFaceUp5":
		case "TeamFaceUp6":
		case "TeamFaceUp7":
		case "TeamFaceUp8":
			if (skirmishGame || (currentLobby != null && currentLobby.isHost))
			{
				int num17 = param[param.Length - 1] - 49;
				if (SelectedFace >= 0 && SelectedFace != num17)
				{
					ButtonClicked(param.Replace("Up", ""));
				}
			}
			break;
		case "TeamFace1":
		case "TeamFace2":
		case "TeamFace3":
		case "TeamFace4":
		case "TeamFace5":
		case "TeamFace6":
		case "TeamFace7":
		case "TeamFace8":
		{
			if (!skirmishGame && (currentLobby == null || !currentLobby.isHost))
			{
				break;
			}
			int num23 = param[param.Length - 1] - 49;
			if (SelectedFace < 0)
			{
				if (orderTeamMembers[num23] != null)
				{
					selectedTeamMember = orderTeamMembers[num23];
					((UIElement)RefTeamFaceCancel).IsEnabled = true;
					SelectedFace = num23;
					switch (num23)
					{
					case 0:
						MainViewModel.Instance.AlliesFace = MainViewModel.Instance.AlliesFace0;
						MainViewModel.Instance.AlliesFaceBackground = MainViewModel.Instance.AlliesFaceBackground0;
						MainViewModel.Instance.AlliesHumanFaceVisible = MainViewModel.Instance.AlliesHumanFaceVis[0];
						break;
					case 1:
						MainViewModel.Instance.AlliesFace = MainViewModel.Instance.AlliesFace1;
						MainViewModel.Instance.AlliesFaceBackground = MainViewModel.Instance.AlliesFaceBackground1;
						MainViewModel.Instance.AlliesHumanFaceVisible = MainViewModel.Instance.AlliesHumanFaceVis[1];
						break;
					case 2:
						MainViewModel.Instance.AlliesFace = MainViewModel.Instance.AlliesFace2;
						MainViewModel.Instance.AlliesFaceBackground = MainViewModel.Instance.AlliesFaceBackground2;
						MainViewModel.Instance.AlliesHumanFaceVisible = MainViewModel.Instance.AlliesHumanFaceVis[2];
						break;
					case 3:
						MainViewModel.Instance.AlliesFace = MainViewModel.Instance.AlliesFace3;
						MainViewModel.Instance.AlliesFaceBackground = MainViewModel.Instance.AlliesFaceBackground3;
						MainViewModel.Instance.AlliesHumanFaceVisible = MainViewModel.Instance.AlliesHumanFaceVis[3];
						break;
					case 4:
						MainViewModel.Instance.AlliesFace = MainViewModel.Instance.AlliesFace4;
						MainViewModel.Instance.AlliesFaceBackground = MainViewModel.Instance.AlliesFaceBackground4;
						MainViewModel.Instance.AlliesHumanFaceVisible = MainViewModel.Instance.AlliesHumanFaceVis[4];
						break;
					case 5:
						MainViewModel.Instance.AlliesFace = MainViewModel.Instance.AlliesFace5;
						MainViewModel.Instance.AlliesFaceBackground = MainViewModel.Instance.AlliesFaceBackground5;
						MainViewModel.Instance.AlliesHumanFaceVisible = MainViewModel.Instance.AlliesHumanFaceVis[5];
						break;
					case 6:
						MainViewModel.Instance.AlliesFace = MainViewModel.Instance.AlliesFace6;
						MainViewModel.Instance.AlliesFaceBackground = MainViewModel.Instance.AlliesFaceBackground6;
						MainViewModel.Instance.AlliesHumanFaceVisible = MainViewModel.Instance.AlliesHumanFaceVis[6];
						break;
					case 7:
						MainViewModel.Instance.AlliesFace = MainViewModel.Instance.AlliesFace7;
						MainViewModel.Instance.AlliesFaceBackground = MainViewModel.Instance.AlliesFaceBackground7;
						MainViewModel.Instance.AlliesHumanFaceVisible = MainViewModel.Instance.AlliesHumanFaceVis[7];
						break;
					}
				}
				break;
			}
			SFXManager.instance.playUISoundVariant(7, 1);
			if (SelectedFace != num23 && orderTeamMembers[num23] != null)
			{
				int team = currentLobby.getTeam(orderTeamMembers[num23]);
				currentLobby.setTeam(selectedTeamMember, team);
				UpdateHostInfo();
				bool flag3 = false;
				if (skirmishGame)
				{
					if (selectedTeamMember.GetLordType() == 0 && !teampop_rat_played)
					{
						for (int num24 = 1; num24 < 9; num24++)
						{
							Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID2 = currentLobby.GetLobbyMemberFromThis_PlayerID(num24);
							if (lobbyMemberFromThis_PlayerID2 != null && lobbyMemberFromThis_PlayerID2.IsSelf() && currentLobby.getTeam(lobbyMemberFromThis_PlayerID2) == team)
							{
								SFXManager.instance.playGenieSpeech(3, "Genie_13.wav", 1f);
								teampop_rat_played = true;
								flag3 = true;
							}
						}
					}
					if (selectedTeamMember.GetLordType() == 6 && !teampop_sultan_played)
					{
						for (int num25 = 1; num25 < 9; num25++)
						{
							Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID3 = currentLobby.GetLobbyMemberFromThis_PlayerID(num25);
							if (lobbyMemberFromThis_PlayerID3 != null && lobbyMemberFromThis_PlayerID3.IsSelf() && currentLobby.getTeam(lobbyMemberFromThis_PlayerID3) == team)
							{
								SFXManager.instance.playGenieSpeech(3, "Genie_14.wav", 1f);
								teampop_sultan_played = true;
								flag3 = true;
							}
						}
					}
				}
				if (!flag3 && !MyAudioManager.Instance.isSpeechPlaying(3) && DateTime.UtcNow > nextTimeTeamSpeech)
				{
					nextTimeTeamSpeech = DateTime.UtcNow.AddSeconds(5.0);
					switch (new Random().Next(7))
					{
					case 0:
						SFXManager.instance.playGenieSpeech(3, "genie_05.wav", 1f);
						break;
					case 1:
						SFXManager.instance.playGenieSpeech(3, "genie_06.wav", 1f);
						break;
					case 2:
						SFXManager.instance.playGenieSpeech(3, "genie_07.wav", 1f);
						break;
					case 3:
						SFXManager.instance.playGenieSpeech(3, "genie_08.wav", 1f);
						break;
					case 4:
						SFXManager.instance.playGenieSpeech(3, "genie_09.wav", 1f);
						break;
					case 5:
						SFXManager.instance.playGenieSpeech(3, "genie_10.wav", 1f);
						break;
					case 6:
						SFXManager.instance.playGenieSpeech(3, "genie_04.wav", 1f);
						break;
					}
				}
			}
			SelectedFace = -1;
			((UIElement)RefTeamFaceCancel).IsEnabled = false;
			PopulateTeamsPanel();
			break;
		}
		case "TeamFaceCancel":
			if ((skirmishGame || (currentLobby != null && currentLobby.isHost)) && SelectedFace >= 0 && currentLobby.CountTeamMembers(currentLobby.getTeam(selectedTeamMember)) > 1)
			{
				SFXManager.instance.playUISoundVariant(7, 0);
				currentLobby.setTeam(selectedTeamMember, currentLobby.getFreeTeam());
				UpdateHostInfo();
				SelectedFace = -1;
				((UIElement)RefTeamFaceCancel).IsEnabled = false;
				PopulateTeamsPanel();
			}
			break;
		case "SendChat":
			if (!Platform_Multiplayer.MPChatMuted)
			{
				if (RefMP_ChatInput.Text.Length > 0)
				{
					Platform_Multiplayer.Instance.SendLobbyChatMessage(RefMP_ChatInput.Text);
					RefMP_ChatInput.Text = "";
				}
				if (FRONT_CoopTrail1.Instance.RefMP_ChatInput.Text.Length > 0)
				{
					Platform_Multiplayer.Instance.SendLobbyChatMessage(FRONT_CoopTrail1.Instance.RefMP_ChatInput.Text);
					FRONT_CoopTrail1.Instance.RefMP_ChatInput.Text = "";
				}
				if (FRONT_CoopTrail2.Instance.RefMP_ChatInput.Text.Length > 0)
				{
					Platform_Multiplayer.Instance.SendLobbyChatMessage(FRONT_CoopTrail2.Instance.RefMP_ChatInput.Text);
					FRONT_CoopTrail2.Instance.RefMP_ChatInput.Text = "";
				}
				if (FRONT_CoopTrail3.Instance.RefMP_ChatInput.Text.Length > 0)
				{
					Platform_Multiplayer.Instance.SendLobbyChatMessage(FRONT_CoopTrail3.Instance.RefMP_ChatInput.Text);
					FRONT_CoopTrail3.Instance.RefMP_ChatInput.Text = "";
				}
			}
			break;
		case "ChatToggle":
			MainViewModel.Instance.Show_CoopConnectedChatVisible = !MainViewModel.Instance._show_CoopConnectedChatVisible;
			MainViewModel.Instance.CoopNewChatVis = false;
			break;
		case "SkirmishMasters":
			FRONT_SkirmishMasters.Open();
			break;
		case "LobbySettings":
		case "Setup":
		{
			bool flag4 = false;
			if (currentLobby != null && currentLobby.isHost)
			{
				flag4 = true;
			}
			string settings2;
			if (param == "Setup")
			{
				MainViewModel.Instance.Show_MPSettings = true;
				MainViewModel.Instance.Show_MPSettings_AdvancedBuildings = true;
				MainViewModel.Instance.Show_MPSettings_AdvancedTroops = false;
				MainViewModel.Instance.Show_MPSettings_AdvancedTrading = false;
				MainViewModel.Instance.Show_MPSettings_AdvancedSettings = false;
				MainViewModel.Instance.MP_Settings_Button = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 58);
				settings2 = MPsetupData.ToString();
				settingsPulseAnimation.Stop();
				((UIElement)FRONT_Multiplayer_Setup.Instance.RefMP_UsePrevious).IsEnabled = flag4 && MPLastSetupData != null;
				((UIElement)FRONT_Multiplayer_Setup.Instance.RefMP_UsePresets1).IsEnabled = flag4 && ConfigSettings.Settings_MPPresets1.Length > 0;
				((UIElement)FRONT_Multiplayer_Setup.Instance.RefMP_UsePresets2).IsEnabled = flag4 && ConfigSettings.Settings_MPPresets2.Length > 0;
			}
			else
			{
				if (selectedLobby == null)
				{
					((UIElement)RefLobbySettingsButton).Visibility = (Visibility)1;
					break;
				}
				flag4 = false;
				MainViewModel.Instance.Show_MPIsHost = false;
				settings2 = selectedLobby.settings;
				MainViewModel.Instance.Show_MPLobbySettings = true;
				MainViewModel.Instance.Show_MPSettings_AdvancedBuildings = true;
				MainViewModel.Instance.Show_MPSettings_AdvancedTroops = false;
				MainViewModel.Instance.Show_MPSettings_AdvancedTrading = false;
				MainViewModel.Instance.Show_MPSettings_AdvancedSettings = false;
				MainViewModel.Instance.Show_MPPeacetime = true;
				lastSettingsRefresh = DateTime.UtcNow;
			}
			MPTEMPsetupData = new EngineInterface.MultiplayerSetupData();
			ImportSettings(settings2, flag4);
			if (!skirmishGame && flag4)
			{
				MainViewModel.Instance.MPSettingHeight = "640";
			}
			else if (skirmishGame || flag4 || MPTEMPsetupData.advanced_options > 0)
			{
				MainViewModel.Instance.MPSettingHeight = "560";
			}
			else
			{
				MainViewModel.Instance.MPSettingHeight = "530";
			}
			MainViewModel.Instance.Show_MPOnlySettings = !skirmishGame;
			MainViewModel.Instance.Show_MPSettings_MaxPlayers = !skirmishGame && flag4 && !customCoopGame;
			break;
		}
		case "UsePrevious":
		{
			string text4 = MPLastSetupData.ToString();
			MPsetupData.FromString(text4, ignoreKeepOrder: true);
			ImportSettings(text4, isHost: true);
			break;
		}
		case "UseDefault":
		{
			string text3 = MPDefaultsetupData.ToString();
			MPsetupData.FromString(text3, ignoreKeepOrder: true);
			ImportSettings(text3, isHost: true);
			break;
		}
		case "UsePresets1":
			MPsetupData.FromString(ConfigSettings.Settings_MPPresets1, ignoreKeepOrder: true);
			ImportSettings(ConfigSettings.Settings_MPPresets1, isHost: true);
			break;
		case "UsePresets2":
			MPsetupData.FromString(ConfigSettings.Settings_MPPresets2, ignoreKeepOrder: true);
			ImportSettings(ConfigSettings.Settings_MPPresets2, isHost: true);
			break;
		case "SavePresets1":
			ConfigSettings.Settings_MPPresets1 = MPTEMPsetupData.ToString();
			ConfigSettings.SaveSettings();
			((UIElement)FRONT_Multiplayer_Setup.Instance.RefMP_UsePresets1).IsEnabled = true;
			break;
		case "SavePresets2":
			ConfigSettings.Settings_MPPresets2 = MPTEMPsetupData.ToString();
			ConfigSettings.SaveSettings();
			((UIElement)FRONT_Multiplayer_Setup.Instance.RefMP_UsePresets2).IsEnabled = true;
			break;
		case "Fairness1":
			if (!skirmishGame)
			{
				MPTEMPsetupData.fairness = 1;
				updateStartingGoldLevels();
			}
			else
			{
				MPsetupData.fairness = 1;
				updateSkirmishStartingGoldLevels();
			}
			SFXManager.instance.playUISound(256);
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 3);
			break;
		case "Fairness2":
			if (!skirmishGame)
			{
				MPTEMPsetupData.fairness = 2;
				updateStartingGoldLevels();
			}
			else
			{
				MPsetupData.fairness = 2;
				updateSkirmishStartingGoldLevels();
			}
			SFXManager.instance.playUISound(255);
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 3);
			break;
		case "Fairness3":
			if (!skirmishGame)
			{
				MPTEMPsetupData.fairness = 3;
				updateStartingGoldLevels();
			}
			else
			{
				MPsetupData.fairness = 3;
				updateSkirmishStartingGoldLevels();
			}
			SFXManager.instance.playUISound(254);
			MainViewModel.Instance.MPGame_Advantage = "";
			break;
		case "Fairness4":
			if (!skirmishGame)
			{
				MPTEMPsetupData.fairness = 4;
				updateStartingGoldLevels();
			}
			else
			{
				MPsetupData.fairness = 4;
				updateSkirmishStartingGoldLevels();
			}
			SFXManager.instance.playUISound(255);
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 4);
			break;
		case "Fairness5":
			if (!skirmishGame)
			{
				MPTEMPsetupData.fairness = 5;
				updateStartingGoldLevels();
			}
			else
			{
				MPsetupData.fairness = 5;
				updateSkirmishStartingGoldLevels();
			}
			SFXManager.instance.playUISound(256);
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 4);
			break;
		case "GameType1":
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 0);
			if (!skirmishGame)
			{
				MPTEMPsetupData.starting_goods_level = 1;
				updateStartingGoldLevels();
			}
			else
			{
				MPsetupData.starting_goods_level = 1;
				updateSkirmishStartingGoldLevels();
			}
			break;
		case "GameType2":
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 1);
			if (!skirmishGame)
			{
				MPTEMPsetupData.starting_goods_level = 2;
				updateStartingGoldLevels();
			}
			else
			{
				MPsetupData.starting_goods_level = 2;
				updateSkirmishStartingGoldLevels();
			}
			break;
		case "GameType3":
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 2);
			if (!skirmishGame)
			{
				MPTEMPsetupData.starting_goods_level = 3;
				updateStartingGoldLevels();
			}
			else
			{
				MPsetupData.starting_goods_level = 3;
				updateSkirmishStartingGoldLevels();
			}
			break;
		case "GameType1_over":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES2, 7);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "GameType2_over":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES2, 8);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "GameType3_over":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ALLIES2, 9);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Fairness1_over":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 305);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Fairness2_over":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 306);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Fairness3_over":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 307);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Fairness4_over":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 308);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Fairness5_over":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 309);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_GameSpeed_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 314);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_PeaceTime_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 315);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_MaxPlayers_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 316);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Walls_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 317);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Cows_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 318);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Dogs_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 319);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Autotrading_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 320);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Autosave_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 321);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_ExtremePowers_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 322);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_ExtremeTroops_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 380);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_ExtremePowerLord_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 323);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_AllowOutposts_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 324);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Adv_EnemyHPS_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 414);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_PreBuild_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 398);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_ImprovedSieging_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 427);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Adv_ASword_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 400);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Adv_HorseArchers_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 402);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Adv_Laddermen_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 404);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Adv_Spearmen_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 406);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Adv_Fletchers_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 408);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Adv_Uncapped_Peasants_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 410);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Adv_Faster_Peasants_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 412);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Adv_Healers_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 435);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Adv_Eunuchs_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 450);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Adv_NoGold_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 452);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "EnableSpectatorMode_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 528);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Apply_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 361);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Cancel_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 362);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_UsePrevious_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 350);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_UsePresets1_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 354);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_UsePresets2_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 355);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_SavePresets1_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 352);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_SavePresets2_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 353);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_UseDefault_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 351);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_ToggleAdvanced_Enter":
			if (MainViewModel.Instance.Show_MPSettings_AdvancedOptions)
			{
				MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 349);
			}
			else
			{
				MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 348);
			}
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_All_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 359);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_None_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 360);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Buildings_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 356);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 357);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trading_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 358);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Settings_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 441);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Building_Barracks_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_RAT);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Building_MercPost_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_SNAKE);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Building_Bedouin_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.BHELP_TEXT_BEDOUIN_STOCKADE);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Building_Moat_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_NARRATIVE);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Building_DairyFarm_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_GOLD);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Building_AppleFarm_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_FRUIT);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Building_WheatFarm_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_MEAT);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Building_HopsFarm_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_ALE);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Building_Market_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_LOAD_SCN);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Building_PitchRig_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_EDIT);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Building_Churches_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_CROSSBOWS);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Archer_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_ENGINEER);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_XBow_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_POPULARITY);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Spear_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_KNIGHT);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Pike_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_LADDERMAN);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Mace_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_BUY);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Sword_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_SELL);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Knight_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.TEXT_SCN_WEEKSOFFMAP);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Monk_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 37) + " / " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 255);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_ArabBow_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 70);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Slave_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 71);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Slinger_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 72);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Assassin_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 73);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_HorseArcher_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 74);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Arab_Sword_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 75);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Arab_Gren_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 76);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Arab_Ballista_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 77);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Bed_Lancer_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 78);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Bed_Healer_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 79);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Bed_Eunuch_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 80);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Bed_Ambush_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 81);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Bed_Skirmish_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 82);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Bed_Heavy_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 83);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Bed_Sapper_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 84);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Bed_Demo_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 85);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Catapult_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 39);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Treb_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 40);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Tower_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 58);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Ram_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 59);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Shield_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 60);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Ballista_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 61);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Mangonel_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 41);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Engi_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 30);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Ladder_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 29);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Troops_Tunnel_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 442) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CHIMP_NAMES, 5);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Wood_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 2);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Hops_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 3);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Stone_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 4);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Iron_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 6);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Pitch_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 7);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Wheat_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 9);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Ale_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 14);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Flour_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 16);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Meat_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 12);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Cheese_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 11);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Bread_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 10);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Apples_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 13);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Bows_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 17);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Spears_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 19);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Mace_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 21);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_XBows_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 18);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Pikes_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 20);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Swords_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 22);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Leather_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 23);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "Settings_Tab_Trade_Armour_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 443) + " : " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, 24);
			MainViewModel.Instance.Show_MPGame_Type_Description = true;
			break;
		case "GameType_leave":
		case "Fairness_leave":
		case "Settings_leave":
			hideToolTipTime = DateTime.UtcNow.AddSeconds(0.5);
			break;
		case "Settings_ExtremePowers":
			if (!skirmishGame && !coopGame)
			{
				if (MPTEMPsetupData.extreme_powers == 0)
				{
					MPTEMPsetupData.extreme_powers = 1;
					MainViewModel.Instance.MPSettings_ExPowersLord_Opacity = 1f;
				}
				else
				{
					MPTEMPsetupData.extreme_powers = 0;
					MPTEMPsetupData.extreme_powers_around_lord = 0;
					MainViewModel.Instance.MPSettings_ExPowersLord_Opacity = 0.5f;
				}
				if (MPTEMPsetupData.extreme_powers > 0)
				{
					MainViewModel.Instance.MP_Settings_ExPowers = MainViewModel.Instance.GameSprites[640];
					break;
				}
				MainViewModel.Instance.MP_Settings_ExPowers = MainViewModel.Instance.GameSprites[641];
				MainViewModel.Instance.MP_Settings_ExPowersLord = MainViewModel.Instance.GameSprites[641];
			}
			else
			{
				if (MPsetupData.extreme_powers == 0)
				{
					MPsetupData.extreme_powers = 1;
					MainViewModel.Instance.MPSettings_ExPowersLord_Opacity = 1f;
				}
				else
				{
					MPsetupData.extreme_powers = 0;
					MPsetupData.extreme_powers_around_lord = 0;
					MainViewModel.Instance.MPSettings_ExPowersLord_Opacity = 0.5f;
				}
				if (MPsetupData.extreme_powers > 0)
				{
					MainViewModel.Instance.MP_Settings_ExPowers = MainViewModel.Instance.GameSprites[640];
					break;
				}
				MainViewModel.Instance.MP_Settings_ExPowers = MainViewModel.Instance.GameSprites[641];
				MainViewModel.Instance.MP_Settings_ExPowersLord = MainViewModel.Instance.GameSprites[641];
			}
			break;
		case "Settings_ExtremePowerLord":
			if (!skirmishGame && !coopGame)
			{
				if (MPTEMPsetupData.extreme_powers_around_lord == 0 && MPTEMPsetupData.extreme_powers != 0)
				{
					MPTEMPsetupData.extreme_powers_around_lord = 1;
				}
				else
				{
					MPTEMPsetupData.extreme_powers_around_lord = 0;
				}
				if (MPTEMPsetupData.extreme_powers_around_lord > 0)
				{
					MainViewModel.Instance.MP_Settings_ExPowersLord = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_ExPowersLord = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPsetupData.extreme_powers_around_lord == 0 && MPsetupData.extreme_powers != 0)
				{
					MPsetupData.extreme_powers_around_lord = 1;
				}
				else
				{
					MPsetupData.extreme_powers_around_lord = 0;
				}
				if (MPsetupData.extreme_powers_around_lord > 0)
				{
					MainViewModel.Instance.MP_Settings_ExPowersLord = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_ExPowersLord = MainViewModel.Instance.GameSprites[641];
				}
			}
			break;
		case "Settings_ExtremeTroops":
			if (!skirmishGame)
			{
				break;
			}
			if (MPsetupData.extreme_troops == 0)
			{
				if (!skirmishExtremeTroopsWarningShown && ConfigSettings.Settings_Show_Extreme_Warning)
				{
					MainViewModel.Instance.Show_MP_ExtremeWarning = true;
					ExtremeWarningSource = 2;
					skirmishExtremeTroopsWarningShown = true;
				}
				MPsetupData.extreme_troops = 1;
			}
			else
			{
				MPsetupData.extreme_troops = 0;
			}
			if (MPsetupData.extreme_troops > 0)
			{
				MainViewModel.Instance.MP_Settings_ExTroops = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_ExTroops = MainViewModel.Instance.GameSprites[641];
			}
			break;
		case "Settings_AllowOutposts":
			if (!skirmishGame)
			{
				if (MPTEMPsetupData.allow_outposts == 0)
				{
					MPTEMPsetupData.allow_outposts = 1;
				}
				else
				{
					MPTEMPsetupData.allow_outposts = 0;
				}
				if (MPTEMPsetupData.allow_outposts > 0)
				{
					MainViewModel.Instance.MP_Settings_AllowOutposts = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_AllowOutposts = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPsetupData.allow_outposts == 0)
				{
					MPsetupData.allow_outposts = 1;
				}
				else
				{
					MPsetupData.allow_outposts = 0;
				}
				if (MPsetupData.allow_outposts > 0)
				{
					MainViewModel.Instance.MP_Settings_AllowOutposts = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_AllowOutposts = MainViewModel.Instance.GameSprites[641];
				}
			}
			break;
		case "Settings_Walls":
			if (MPTEMPsetupData.no_knockdown_walls == 0)
			{
				MPTEMPsetupData.no_knockdown_walls = 1;
			}
			else
			{
				MPTEMPsetupData.no_knockdown_walls = 0;
			}
			if (MPTEMPsetupData.no_knockdown_walls > 0)
			{
				MainViewModel.Instance.MP_Settings_Wall = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Wall = MainViewModel.Instance.GameSprites[641];
			}
			break;
		case "Settings_Dogs":
			if (MPTEMPsetupData.no_dogs == 0)
			{
				MPTEMPsetupData.no_dogs = 1;
			}
			else
			{
				MPTEMPsetupData.no_dogs = 0;
			}
			if (MPTEMPsetupData.no_dogs > 0)
			{
				MainViewModel.Instance.MP_Settings_Dogs = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Dogs = MainViewModel.Instance.GameSprites[641];
			}
			break;
		case "Settings_Cows":
			if (MPTEMPsetupData.no_cows == 0)
			{
				MPTEMPsetupData.no_cows = 1;
			}
			else
			{
				MPTEMPsetupData.no_cows = 0;
			}
			if (MPTEMPsetupData.no_cows > 0)
			{
				MainViewModel.Instance.MP_Settings_Cows = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Cows = MainViewModel.Instance.GameSprites[641];
			}
			break;
		case "Settings_Autotrading":
			if (MPTEMPsetupData.allow_autotrading == 0)
			{
				MPTEMPsetupData.allow_autotrading = 1;
			}
			else
			{
				MPTEMPsetupData.allow_autotrading = 0;
			}
			if (MPTEMPsetupData.allow_autotrading > 0)
			{
				MainViewModel.Instance.MP_Settings_Autotrading = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Autotrading = MainViewModel.Instance.GameSprites[641];
			}
			break;
		case "Settings_Autosave":
			if (!coopGame)
			{
				if (MPTEMPsetupData.autosave == 0)
				{
					MPTEMPsetupData.autosave = 5;
				}
				else if (MPTEMPsetupData.autosave == 5)
				{
					MPTEMPsetupData.autosave = 10;
				}
				else if (MPTEMPsetupData.autosave == 10)
				{
					MPTEMPsetupData.autosave = 20;
				}
				else if (MPTEMPsetupData.autosave == 20)
				{
					MPTEMPsetupData.autosave = 0;
				}
				switch (MPTEMPsetupData.autosave)
				{
				case 0:
					MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_MACEMEN);
					break;
				case 5:
					MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_SWORDSMEN);
					break;
				case 10:
					MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_KNIGHTS);
					break;
				case 20:
					MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_LADDERMEN);
					break;
				}
			}
			else if (!singlePlayerCoop)
			{
				if (MPsetupData.autosave == 0)
				{
					MPsetupData.autosave = 5;
				}
				else if (MPsetupData.autosave == 5)
				{
					MPsetupData.autosave = 10;
				}
				else if (MPsetupData.autosave == 10)
				{
					MPsetupData.autosave = 20;
				}
				else if (MPsetupData.autosave == 20)
				{
					MPsetupData.autosave = 0;
				}
				switch (MPsetupData.autosave)
				{
				case 0:
					MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_MACEMEN);
					break;
				case 5:
					MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_SWORDSMEN);
					break;
				case 10:
					MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_KNIGHTS);
					break;
				case 20:
					MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_LADDERMEN);
					break;
				}
			}
			break;
		case "Settings_ToggleAdvanced":
			if (MPTEMPsetupData.advanced_options != 0)
			{
				MPTEMPsetupData.advanced_options = 0;
				MainViewModel.Instance.MPSettings_AdvancedButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKIRMISH_MISC, 19);
			}
			else
			{
				MPTEMPsetupData.advanced_options = 1;
				MainViewModel.Instance.MPSettings_AdvancedButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKIRMISH_MISC, 20);
			}
			MainViewModel.Instance.Show_MPSettings_AdvancedOptions = MPTEMPsetupData.advanced_options != 0;
			break;
		case "Settings_Tab_Buildings":
			MainViewModel.Instance.Show_MPSettings_AdvancedBuildings = true;
			MainViewModel.Instance.Show_MPSettings_AdvancedTroops = false;
			MainViewModel.Instance.Show_MPSettings_AdvancedTrading = false;
			MainViewModel.Instance.Show_MPSettings_AdvancedSettings = false;
			break;
		case "Settings_Tab_Troops":
			MainViewModel.Instance.Show_MPSettings_AdvancedBuildings = false;
			MainViewModel.Instance.Show_MPSettings_AdvancedTroops = true;
			MainViewModel.Instance.Show_MPSettings_AdvancedTrading = false;
			MainViewModel.Instance.Show_MPSettings_AdvancedSettings = false;
			break;
		case "Settings_Tab_Trading":
			MainViewModel.Instance.Show_MPSettings_AdvancedBuildings = false;
			MainViewModel.Instance.Show_MPSettings_AdvancedTroops = false;
			MainViewModel.Instance.Show_MPSettings_AdvancedTrading = true;
			MainViewModel.Instance.Show_MPSettings_AdvancedSettings = false;
			break;
		case "Settings_Tab_Settings":
			MainViewModel.Instance.MPSettings_AdvSkirmish_Opacity = 1f;
			MainViewModel.Instance.Show_MPSettings_AdvancedBuildings = false;
			MainViewModel.Instance.Show_MPSettings_AdvancedTroops = false;
			MainViewModel.Instance.Show_MPSettings_AdvancedTrading = false;
			MainViewModel.Instance.Show_MPSettings_AdvancedSettings = true;
			break;
		case "Settings_Tab_All":
			if (MainViewModel.Instance.Show_MPSettings_AdvancedBuildings)
			{
				for (int num12 = 0; num12 < 8; num12++)
				{
					MPTEMPsetupData.MP_BuildingsAvailable[num12] = 1;
					MainViewModel.Instance.MPSetupBuildingsBool[num12] = MPTEMPsetupData.MP_BuildingsAvailable[num12] != 0;
				}
				MPTEMPsetupData.MP_BuildingsAvailable[10] = 1;
				MainViewModel.Instance.MPSetupBuildingsBool[10] = MPTEMPsetupData.MP_BuildingsAvailable[10] != 0;
				MPTEMPsetupData.MP_BuildingsAvailable[11] = 1;
				MainViewModel.Instance.MPSetupBuildingsBool[11] = MPTEMPsetupData.MP_BuildingsAvailable[11] != 0;
				MPTEMPsetupData.MP_BuildingsAvailable[12] = 1;
				MainViewModel.Instance.MPSetupBuildingsBool[12] = MPTEMPsetupData.MP_BuildingsAvailable[12] != 0;
			}
			if (MainViewModel.Instance.Show_MPSettings_AdvancedTroops)
			{
				for (int num13 = 0; num13 < 32; num13++)
				{
					MPTEMPsetupData.MP_TroopsAvailable[num13] = 1;
					MainViewModel.Instance.MPSetupTroopsBool[num13] = MPTEMPsetupData.MP_TroopsAvailable[num13] != 0;
				}
				for (int num14 = 8; num14 < 10; num14++)
				{
					MPTEMPsetupData.MP_BuildingsAvailable[num14] = 1;
					MainViewModel.Instance.MPSetupBuildingsBool[num14] = MPTEMPsetupData.MP_BuildingsAvailable[num14] != 0;
				}
			}
			if (MainViewModel.Instance.Show_MPSettings_AdvancedTrading)
			{
				for (int num15 = 0; num15 < 25; num15++)
				{
					MPTEMPsetupData.MP_GoodsAvailable[num15] = 1;
					MainViewModel.Instance.TradingGoodsBool[num15] = MPTEMPsetupData.MP_GoodsAvailable[num15] != 0;
				}
			}
			if (MainViewModel.Instance.Show_MPSettings_AdvancedOptions)
			{
				MPTEMPsetupData.advopt_pre_build = 1;
				MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[640];
				MPTEMPsetupData.global_improved_sieging = 1;
				MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[640];
				MPTEMPsetupData.advopt_improved_arabswordsmen = 1;
				MainViewModel.Instance.MP_Settings_Adv_ASword = MainViewModel.Instance.GameSprites[640];
				MPTEMPsetupData.advopt_rebalanced_horsearchers = 1;
				MainViewModel.Instance.MP_Settings_Adv_HorseArchers = MainViewModel.Instance.GameSprites[640];
				MPTEMPsetupData.advopt_improved_laddermen = 1;
				MainViewModel.Instance.MP_Settings_Adv_Laddermen = MainViewModel.Instance.GameSprites[640];
				MPTEMPsetupData.advopt_improved_spearmen = 1;
				MainViewModel.Instance.MP_Settings_Adv_Spearmen = MainViewModel.Instance.GameSprites[640];
				MPTEMPsetupData.advopt_improved_fletchers = 1;
				MainViewModel.Instance.MP_Settings_Adv_Fletchers = MainViewModel.Instance.GameSprites[640];
				MPTEMPsetupData.advopt_uncapped_peasants = 1;
				MainViewModel.Instance.MP_Settings_Uncapped_Peasants = MainViewModel.Instance.GameSprites[640];
				MPTEMPsetupData.advopt_faster_peasants = 1;
				MainViewModel.Instance.MP_Settings_Faster_Peasants = MainViewModel.Instance.GameSprites[640];
				MPTEMPsetupData.advopt_healers = 1;
				MainViewModel.Instance.MP_Settings_Healers = MainViewModel.Instance.GameSprites[640];
				MPTEMPsetupData.advopt_eunuchs = 1;
				MainViewModel.Instance.MP_Settings_Eunuchs = MainViewModel.Instance.GameSprites[640];
				MPTEMPsetupData.advopt_nogold = 1;
				MainViewModel.Instance.MP_Settings_NoGold = MainViewModel.Instance.GameSprites[640];
			}
			break;
		case "Settings_Tab_None":
			if (MainViewModel.Instance.Show_MPSettings_AdvancedBuildings)
			{
				for (int num8 = 0; num8 < 8; num8++)
				{
					MPTEMPsetupData.MP_BuildingsAvailable[num8] = 0;
					MainViewModel.Instance.MPSetupBuildingsBool[num8] = MPTEMPsetupData.MP_BuildingsAvailable[num8] != 0;
				}
				MPTEMPsetupData.MP_BuildingsAvailable[10] = 0;
				MainViewModel.Instance.MPSetupBuildingsBool[10] = MPTEMPsetupData.MP_BuildingsAvailable[10] != 0;
				MPTEMPsetupData.MP_BuildingsAvailable[11] = 0;
				MainViewModel.Instance.MPSetupBuildingsBool[11] = MPTEMPsetupData.MP_BuildingsAvailable[11] != 0;
				MPTEMPsetupData.MP_BuildingsAvailable[12] = 0;
				MainViewModel.Instance.MPSetupBuildingsBool[12] = MPTEMPsetupData.MP_BuildingsAvailable[12] != 0;
			}
			if (MainViewModel.Instance.Show_MPSettings_AdvancedTroops)
			{
				for (int num9 = 0; num9 < 32; num9++)
				{
					MPTEMPsetupData.MP_TroopsAvailable[num9] = 0;
					MainViewModel.Instance.MPSetupTroopsBool[num9] = MPTEMPsetupData.MP_TroopsAvailable[num9] != 0;
				}
				for (int num10 = 8; num10 < 10; num10++)
				{
					MPTEMPsetupData.MP_BuildingsAvailable[num10] = 0;
					MainViewModel.Instance.MPSetupBuildingsBool[num10] = MPTEMPsetupData.MP_BuildingsAvailable[num10] != 0;
				}
			}
			if (MainViewModel.Instance.Show_MPSettings_AdvancedTrading)
			{
				for (int num11 = 0; num11 < 25; num11++)
				{
					MPTEMPsetupData.MP_GoodsAvailable[num11] = 0;
					MainViewModel.Instance.TradingGoodsBool[num11] = MPTEMPsetupData.MP_GoodsAvailable[num11] != 0;
				}
			}
			if (MainViewModel.Instance.Show_MPSettings_AdvancedOptions)
			{
				MPTEMPsetupData.advopt_pre_build = 0;
				MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[641];
				MPTEMPsetupData.global_improved_sieging = 0;
				MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[641];
				MPTEMPsetupData.advopt_improved_arabswordsmen = 0;
				MainViewModel.Instance.MP_Settings_Adv_ASword = MainViewModel.Instance.GameSprites[641];
				MPTEMPsetupData.advopt_rebalanced_horsearchers = 0;
				MainViewModel.Instance.MP_Settings_Adv_HorseArchers = MainViewModel.Instance.GameSprites[641];
				MPTEMPsetupData.advopt_improved_laddermen = 0;
				MainViewModel.Instance.MP_Settings_Adv_Laddermen = MainViewModel.Instance.GameSprites[641];
				MPTEMPsetupData.advopt_improved_spearmen = 0;
				MainViewModel.Instance.MP_Settings_Adv_Spearmen = MainViewModel.Instance.GameSprites[641];
				MPTEMPsetupData.advopt_improved_fletchers = 0;
				MainViewModel.Instance.MP_Settings_Adv_Fletchers = MainViewModel.Instance.GameSprites[641];
				MPTEMPsetupData.advopt_uncapped_peasants = 0;
				MainViewModel.Instance.MP_Settings_Uncapped_Peasants = MainViewModel.Instance.GameSprites[641];
				MPTEMPsetupData.advopt_faster_peasants = 0;
				MainViewModel.Instance.MP_Settings_Faster_Peasants = MainViewModel.Instance.GameSprites[641];
				MPTEMPsetupData.advopt_healers = 0;
				MainViewModel.Instance.MP_Settings_Healers = MainViewModel.Instance.GameSprites[641];
				MPTEMPsetupData.advopt_eunuchs = 0;
				MainViewModel.Instance.MP_Settings_Eunuchs = MainViewModel.Instance.GameSprites[641];
				MPTEMPsetupData.advopt_nogold = 0;
				MainViewModel.Instance.MP_Settings_NoGold = MainViewModel.Instance.GameSprites[641];
			}
			break;
		case "GOODS_0":
		case "GOODS_1":
		case "GOODS_2":
		case "GOODS_3":
		case "GOODS_4":
		case "GOODS_5":
		case "GOODS_6":
		case "GOODS_7":
		case "GOODS_8":
		case "GOODS_9":
		case "GOODS_10":
		case "GOODS_11":
		case "GOODS_12":
		case "GOODS_13":
		case "GOODS_14":
		case "GOODS_15":
		case "GOODS_16":
		case "GOODS_17":
		case "GOODS_18":
		case "GOODS_19":
		case "GOODS_20":
		case "GOODS_21":
		case "GOODS_22":
		case "GOODS_23":
		case "GOODS_24":
		{
			int num6 = int.Parse(param.Substring(6));
			if (MPTEMPsetupData.MP_GoodsAvailable[num6] != 0)
			{
				MPTEMPsetupData.MP_GoodsAvailable[num6] = 0;
			}
			else
			{
				MPTEMPsetupData.MP_GoodsAvailable[num6] = 1;
			}
			for (int num7 = 0; num7 < 25; num7++)
			{
				MainViewModel.Instance.TradingGoodsBool[num7] = MPTEMPsetupData.MP_GoodsAvailable[num7] != 0;
			}
			break;
		}
		case "STRUCT_BARRACKS_STONE":
		case "STRUCT_BARRACKS_WOOD":
		case "STRUCT_BEDOUIN_STOCKADE":
		case "STRUCT_CATTLEFARM":
		case "STRUCT_APPLEFARM":
		case "STRUCT_WHEATFARM":
		case "STRUCT_HOPSFARM":
		case "STRUCT_TRADEPOST":
		case "STRUCT_BALLISTA":
		case "STRUCT_MANGONEL":
		case "STRUCT_PITCH_DIGGER":
		case "STRUCT_CHURCH":
		case "STRUCT_MOAT":
		{
			int num4 = 0;
			switch (param)
			{
			case "STRUCT_BARRACKS_STONE":
				num4 = 0;
				break;
			case "STRUCT_BARRACKS_WOOD":
				num4 = 1;
				break;
			case "STRUCT_BEDOUIN_STOCKADE":
				num4 = 2;
				break;
			case "STRUCT_CATTLEFARM":
				num4 = 3;
				break;
			case "STRUCT_APPLEFARM":
				num4 = 4;
				break;
			case "STRUCT_WHEATFARM":
				num4 = 5;
				break;
			case "STRUCT_HOPSFARM":
				num4 = 6;
				break;
			case "STRUCT_TRADEPOST":
				num4 = 7;
				break;
			case "STRUCT_BALLISTA":
				num4 = 8;
				break;
			case "STRUCT_MANGONEL":
				num4 = 9;
				break;
			case "STRUCT_PITCH_DIGGER":
				num4 = 10;
				break;
			case "STRUCT_CHURCH":
				num4 = 11;
				break;
			case "STRUCT_MOAT":
				num4 = 12;
				break;
			}
			if (MPTEMPsetupData.MP_BuildingsAvailable[num4] != 0)
			{
				MPTEMPsetupData.MP_BuildingsAvailable[num4] = 0;
			}
			else
			{
				MPTEMPsetupData.MP_BuildingsAvailable[num4] = 1;
			}
			for (int num5 = 0; num5 < 13; num5++)
			{
				MainViewModel.Instance.MPSetupBuildingsBool[num5] = MPTEMPsetupData.MP_BuildingsAvailable[num5] != 0;
			}
			break;
		}
		case "TROOPS_0":
		case "TROOPS_1":
		case "TROOPS_2":
		case "TROOPS_3":
		case "TROOPS_4":
		case "TROOPS_5":
		case "TROOPS_6":
		case "TROOPS_7":
		case "TROOPS_8":
		case "TROOPS_9":
		case "TROOPS_10":
		case "TROOPS_11":
		case "TROOPS_12":
		case "TROOPS_13":
		case "TROOPS_14":
		case "TROOPS_15":
		case "TROOPS_16":
		case "TROOPS_17":
		case "TROOPS_18":
		case "TROOPS_19":
		case "TROOPS_20":
		case "TROOPS_21":
		case "TROOPS_22":
		case "TROOPS_23":
		case "TROOPS_24":
		case "TROOPS_25":
		case "TROOPS_26":
		case "TROOPS_27":
		case "TROOPS_28":
		case "TROOPS_29":
		case "TROOPS_30":
		case "TROOPS_31":
		{
			int num2 = int.Parse(param.Substring(7));
			if (MPTEMPsetupData.MP_TroopsAvailable[num2] != 0)
			{
				MPTEMPsetupData.MP_TroopsAvailable[num2] = 0;
			}
			else
			{
				MPTEMPsetupData.MP_TroopsAvailable[num2] = 1;
			}
			for (int num3 = 0; num3 < 32; num3++)
			{
				MainViewModel.Instance.MPSetupTroopsBool[num3] = MPTEMPsetupData.MP_TroopsAvailable[num3] != 0;
			}
			break;
		}
		case "ApplySettings":
		{
			MPTEMPsetupData.peacetime = (int)((RangeBase)FRONT_Multiplayer_Setup.Instance.RefMP_Settings_Peacetime_Slider).Value;
			if (!customCoopGame)
			{
				PlayerCap = (int)((RangeBase)FRONT_Multiplayer_Setup.Instance.RefSetupMaxPlayersSlider).Value;
			}
			MainViewModel.Instance.Show_MPSettings = false;
			if (!MainViewModel.Instance.Show_MPPeacetime)
			{
				MPTEMPsetupData.peacetime = 0;
			}
			if (MPTEMPsetupData.advanced_options > 0)
			{
				bool flag = false;
				for (int i = 0; i < MPTEMPsetupData.MP_BuildingsAvailable.Length; i++)
				{
					if (MPTEMPsetupData.MP_BuildingsAvailable[i] == 0)
					{
						flag = true;
					}
				}
				for (int j = 0; j < MPTEMPsetupData.MP_GoodsAvailable.Length; j++)
				{
					if (MPTEMPsetupData.MP_GoodsAvailable[j] == 0)
					{
						flag = true;
					}
				}
				for (int k = 0; k < MPTEMPsetupData.MP_TroopsAvailable.Length; k++)
				{
					if (MPTEMPsetupData.MP_TroopsAvailable[k] == 0)
					{
						flag = true;
					}
				}
				if (MPTEMPsetupData.advopt_enemy_hps != 1 || MPTEMPsetupData.advopt_faster_peasants > 0 || MPTEMPsetupData.advopt_healers > 0 || MPTEMPsetupData.advopt_eunuchs > 0 || MPTEMPsetupData.advopt_nogold > 0 || MPTEMPsetupData.advopt_improved_arabswordsmen > 0 || MPTEMPsetupData.advopt_improved_fletchers > 0 || MPTEMPsetupData.advopt_improved_laddermen > 0 || MPTEMPsetupData.advopt_improved_spearmen > 0 || MPTEMPsetupData.advopt_pre_build > 0 || MPTEMPsetupData.advopt_rebalanced_horsearchers > 0 || MPTEMPsetupData.advopt_uncapped_peasants > 0)
				{
					flag = true;
				}
				if (!flag)
				{
					MPTEMPsetupData.advanced_options = 0;
				}
			}
			string str = MPTEMPsetupData.ToString();
			MPsetupData.FromString(str, ignoreKeepOrder: true);
			if (!skirmishGame)
			{
				if (MPLastSetupData == null)
				{
					MPLastSetupData = new EngineInterface.MultiplayerSetupData();
				}
				MPLastSetupData.FromString(str);
			}
			UpdateHostInfo(delayed: true);
			break;
		}
		case "CancelSettings":
			MainViewModel.Instance.Show_MPSettings = false;
			MainViewModel.Instance.Show_MPLobbySettings = false;
			break;
		case "RetrieveMap":
			multiplayerMapRequestTime = DateTime.UtcNow.AddSeconds(60.0);
			MainViewModel.Instance.MapRetrieveProgress = "1";
			Platform_Multiplayer.Instance.RequestMap(currentLobby.mapFileName, delegate
			{
				multiplayerMapRequestTime = DateTime.UtcNow.AddSeconds(1.0);
			}, delegate
			{
				if (Platform_Multiplayer.Instance.MapReceiveProgress > 0)
				{
					MainViewModel.Instance.MapRetrieveProgress = (Platform_Multiplayer.Instance.MapReceiveProgress * 4).ToString();
				}
				else
				{
					MainViewModel.Instance.MapRetrieveProgress = "0";
					multiplayerMapRequestTime = DateTime.UtcNow.AddSeconds(1.0);
				}
			});
			break;
		case "AISettings_1":
		case "AISettings_2":
		case "AISettings_3":
		case "AISettings_4":
		case "AISettings_5":
		case "AISettings_6":
		case "AISettings_7":
		case "AISettings_8":
		{
			int num = param[param.Length - 1] - 49;
			num = playerRows[num].playerID;
			MainViewModel.Instance.Show_AddAIPanel = false;
			MainViewModel.Instance.Show_SkirmishRandomAIPanel = false;
			MainViewModel.Instance.Show_AdvancedRandom = false;
			FRONT_Multiplayer_AISettings.Show(num, AIVs[num - 1], !skirmishGame);
			break;
		}
		case "CancelAISettings":
			MainViewModel.Instance.Show_MPAISettings = false;
			if (!skirmishGame)
			{
				UpdateHostInfo();
			}
			break;
		case "CancelConnecting":
			LeaveLobby();
			MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
			break;
		case "ClearFilter":
			RefMP_SearchFilter.Text = "";
			MainViewModel.Instance.MultiplayerFilterLabelVis = (Visibility)2;
			MainViewModel.Instance.MultiplayerFilterButtonVis = (Visibility)1;
			break;
		case "ShowSharing":
			MainViewModel.Instance.Show_MPSharing = true;
			MainViewModel.Instance.Show_CoopMapIcons = false;
			break;
		case "CopySharing":
			GUIUtility.systemCopyBuffer = Platform_Multiplayer.Instance.ShareCodeString;
			break;
		case "DisplaySharing":
			ShowSharingCode = true;
			break;
		case "ColourPicker":
			ShowColourPicker();
			break;
		case "Col1":
			SetShieldColour(1);
			break;
		case "Col2":
			SetShieldColour(2);
			break;
		case "Col3":
			SetShieldColour(3);
			break;
		case "Col4":
			SetShieldColour(4);
			break;
		case "Col5":
			SetShieldColour(5);
			break;
		case "Col6":
			SetShieldColour(6);
			break;
		case "Col7":
			SetShieldColour(7);
			break;
		case "Col8":
			SetShieldColour(8);
			break;
		case "AddCustomLord":
		{
			if (currentLobby == null || !currentLobby.isHost)
			{
				break;
			}
			int count = currentLobby.members.Count;
			int num27 = PlayerCap;
			if (customCoopGame)
			{
				num27 = selectedMPHeader.maxPlayers;
			}
			if (count >= num27 || (count >= currentLobby.iMaxPlayers && !customCoopGame))
			{
				break;
			}
			if (!skirmishGame)
			{
				if (customCoopGame)
				{
					int maxPlayers = selectedMPHeader.maxPlayers;
					if (count >= maxPlayers || (count == maxPlayers - 1 && currentLobby.CountHumanPlayers() == 1))
					{
						break;
					}
				}
				else if ((count == PlayerCap - 1 || count == currentLobby.iMaxPlayers - 1) && currentLobby.CountAIPlayers() == count - 1)
				{
					break;
				}
			}
			CustomisationFileManager.CustomLord customLord = null;
			if (((Selector)RefCustomLordList).SelectedItem == null)
			{
				break;
			}
			customLord = ((FileRow)((Selector)RefCustomLordList).SelectedItem).lord;
			int forcedTeam = -1;
			if (customCoopGame)
			{
				forcedTeam = currentLobby.findCustomCoopEnemyTeam();
			}
			Platform_Multiplayer.MPLobbyMember mPLobbyMember = Platform_Multiplayer.Instance.AddCustomSkirmishPlayerLocal(customLord, forcedTeam);
			if (CustomisationFileManager.CustomMediaExists && !MyAudioManager.Instance.isSpeechPlaying(3))
			{
				string path = MapFileManager.SplitCustomTrailName(mPLobbyMember.customLordName);
				string text5 = Path.Combine(ConfigSettings.GetUserCustomMediaPath(), path, "ADD_PLAYER.wav");
				if (File.Exists(text5))
				{
					MyAudioManager.Instance.PlaySpeech(3, "*", text5, force: true);
				}
			}
			updateSteamIDMappings();
			for (int num28 = 0; num28 < 8; num28++)
			{
				if (currentLobby.this_player_to_SteamID_mapping[num28] == mPLobbyMember.GetSteamID())
				{
					ulong steamID = mPLobbyMember.GetSteamID();
					int lordSubType = mPLobbyMember.GetLordSubType();
					mPLobbyMember.SetValidCustomLordType(num28, lordSubType);
					currentLobby.this_player_to_SteamID_mapping[num28] = mPLobbyMember.GetSteamID();
					currentLobby.switchTeamID(steamID, mPLobbyMember.GetSteamID());
					break;
				}
			}
			ReSortTeamInfo();
			if (mPLobbyMember != null)
			{
				int thisPlayerFromSteamID = currentLobby.getThisPlayerFromSteamID(mPLobbyMember.id.m_SteamID);
				AIVs[thisPlayerFromSteamID - 1].Init(mPLobbyMember.GetLordType(), customLord.lordName);
				AIVs[thisPlayerFromSteamID - 1].lordConfig = customLord.configs[0];
				AIVs[thisPlayerFromSteamID - 1].aivs.Add(customLord.aivs[0]);
				AIVs[thisPlayerFromSteamID - 1].imageData = customLord.imageData;
				AIVs[thisPlayerFromSteamID - 1].image = customLord.image;
			}
			UpdateHostInfo();
			CreateTeamShields();
			UpdateRadarShieldPositions();
			break;
		}
		case "CloseCustomLord":
			MainViewModel.Instance.Show_AddAIPanel_Normal = true;
			break;
		case "COOP_START":
			if (singlePlayerCoop)
			{
				ConfigSettings.InitCoopGame(userName: (singlePlayerCoopAlly >= 25) ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 453 + 17 * ((int)singlePlayerCoopAlly - 25)) : ((singlePlayerCoopAlly < 16) ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 239 + 9 * (int)singlePlayerCoopAlly) : Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 88 + 9 * ((int)singlePlayerCoopAlly - 16))), steamID: singlePlayerCoopAlly + 1000);
				StartSkirmishGame();
			}
			else if (currentLobby != null && currentLobby.CountHumanPlayers() == 2)
			{
				ulong coopPartnerID = Platform_Multiplayer.Instance.GetCoopPartnerID();
				if (coopPartnerID != 0L)
				{
					ConfigSettings.InitCoopGame(coopPartnerID, Platform_Multiplayer.Instance.getSteamUserName(coopPartnerID), Platform_Multiplayer.Instance.LastCoAString);
				}
				Platform_Multiplayer.Instance.HostStartGame();
				startGameTime = DateTime.UtcNow.AddMilliseconds(500.0);
			}
			break;
		case "CoopKick":
			playKickSpeech = false;
			ButtonClicked("Kick_2");
			playKickSpeech = true;
			singlePlayerCoop = false;
			break;
		case "CoopLeave":
			ButtonClicked("Back");
			break;
		case "Coop_Friend1":
		case "Coop_Friend2":
		case "Coop_Friend3":
		case "Coop_Friend4":
		case "Coop_Friend5":
		case "Coop_Friend6":
		case "Coop_Friend7":
		case "Coop_Friend8":
		{
			int num22 = int.Parse(param.Substring(11)) - 1;
			if (coopFriendsSteamIDs[num22] != 0L)
			{
				ConfigSettings.CalcCoopProgress(coopFriendsSteamIDs[num22], capProgress: true);
				MainViewModel.Instance.FrontEndMenu.GenerateSwords();
				if (FrontendMenus.CurrentSelectedTrail == 21)
				{
					MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(ConfigSettings.Settings_Progress_Trail_CoopNext1 + 1);
				}
				else if (FrontendMenus.CurrentSelectedTrail == 22)
				{
					MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(ConfigSettings.Settings_Progress_Trail_CoopNext2 + 1);
				}
				else if (FrontendMenus.CurrentSelectedTrail == 23)
				{
					MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(ConfigSettings.Settings_Progress_Trail_CoopNext3 + 1);
				}
			}
			break;
		}
		case "Coop_Friend1-":
		case "Coop_Friend2-":
		case "Coop_Friend3-":
		case "Coop_Friend4-":
		case "Coop_Friend5-":
		case "Coop_Friend6-":
		case "Coop_Friend7-":
		case "Coop_Friend8-":
		{
			int num21 = int.Parse(param.Substring(11, 1)) - 1;
			if (coopFriendsSteamIDs[num21] != 0L)
			{
				coopHiddenSelectedSteamID = coopFriendsSteamIDs[num21];
				string userName = "";
				bool coopRowHiddenInfo = ConfigSettings.getCoopRowHiddenInfo(coopHiddenSelectedSteamID, out userName);
				MainViewModel.Instance.Show_CoopHideButton = !coopRowHiddenInfo;
				MainViewModel.Instance.Show_CoopShowButton = coopRowHiddenInfo;
				MainViewModel.Instance.CoopHideName = userName;
				MainViewModel.Instance.Show_CoopHidePanel = true;
			}
			break;
		}
		case "CloseCoopHide":
			MainViewModel.Instance.Show_CoopHidePanel = false;
			coopFriendsPage = 0;
			CoopPopulateFriendsList();
			break;
		case "Coop_Hide":
			ConfigSettings.setCoopHidden(coopHiddenSelectedSteamID, state: true);
			MainViewModel.Instance.Show_CoopHidePanel = false;
			coopFriendsPage = 0;
			((UIElement)FRONT_CoopTrail1.Instance.RefShowHidden).Visibility = (Visibility)2;
			((UIElement)FRONT_CoopTrail2.Instance.RefShowHidden).Visibility = (Visibility)2;
			((UIElement)FRONT_CoopTrail3.Instance.RefShowHidden).Visibility = (Visibility)2;
			CoopPopulateFriendsList();
			break;
		case "Coop_Show":
			ConfigSettings.setCoopHidden(coopHiddenSelectedSteamID, state: false);
			MainViewModel.Instance.Show_CoopHidePanel = false;
			coopFriendsPage = 0;
			((UIElement)FRONT_CoopTrail1.Instance.RefShowHidden).Visibility = (Visibility)2;
			((UIElement)FRONT_CoopTrail2.Instance.RefShowHidden).Visibility = (Visibility)2;
			((UIElement)FRONT_CoopTrail3.Instance.RefShowHidden).Visibility = (Visibility)2;
			CoopPopulateFriendsList();
			if (ConfigSettings.getCoopTrailCount(countHidden: true) == ConfigSettings.getCoopTrailCount(countHidden: false))
			{
				((UIElement)FRONT_CoopTrail1.Instance.RefShowHidden).Visibility = (Visibility)1;
				((UIElement)FRONT_CoopTrail2.Instance.RefShowHidden).Visibility = (Visibility)1;
				((UIElement)FRONT_CoopTrail3.Instance.RefShowHidden).Visibility = (Visibility)1;
			}
			else
			{
				((UIElement)FRONT_CoopTrail1.Instance.RefShowHidden).Visibility = (Visibility)2;
				((UIElement)FRONT_CoopTrail2.Instance.RefShowHidden).Visibility = (Visibility)2;
				((UIElement)FRONT_CoopTrail3.Instance.RefShowHidden).Visibility = (Visibility)2;
			}
			((ToggleButton)FRONT_CoopTrail1.Instance.RefShowHidden).IsChecked = false;
			((ToggleButton)FRONT_CoopTrail2.Instance.RefShowHidden).IsChecked = false;
			((ToggleButton)FRONT_CoopTrail3.Instance.RefShowHidden).IsChecked = false;
			coopShowHiddenFriends = false;
			break;
		case "CoopContinue1":
		case "CoopContinue2":
		case "CoopContinue3":
		case "CoopContinue4":
		case "CoopContinue5":
		case "CoopContinue6":
		case "CoopContinue7":
		case "CoopContinue8":
		{
			int num20 = int.Parse(param.Substring(12)) - 1;
			if (coopFriendsSteamIDs[num20] != 0L)
			{
				SkirmishAIAddClick(((int)(coopFriendsSteamIDs[num20] - 1000)).ToString());
			}
			break;
		}
		case "CoopUp":
			if (coopFriendsPage > 0)
			{
				coopFriendsPage--;
				CoopPopulateFriendsList();
			}
			break;
		case "CoopDown":
		{
			int coopTrailCount = ConfigSettings.getCoopTrailCount(coopShowHiddenFriends);
			if (coopFriendsPage < (coopTrailCount - 1) / 8)
			{
				coopFriendsPage++;
				CoopPopulateFriendsList();
			}
			break;
		}
		case "CoopSinglePlayer":
			MainViewModel.Instance.Show_CoopAIAllyPanel = true;
			MainViewModel.Instance.Show_CoopMapIcons = false;
			break;
		case "CoopSinglePlayerBack":
			MainViewModel.Instance.Show_CoopAIAllyPanel = false;
			MainViewModel.Instance.Show_CoopMapIcons = true;
			break;
		case "CreateCustomCoop":
			Open();
			MainViewModel.Instance.FRONTMultiplayer.ButtonClicked("Host");
			MPGameType = 1;
			ButtonClicked("ToggleGameType");
			break;
		case "ClearWait":
			MainViewModel.Instance.Show_CoopWaiting = false;
			LeaveLobby(doLeaveOnSteam: true, refreshLobbyList: false);
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("Skirmish");
			break;
		case "CloseExtremeWarning":
			MainViewModel.Instance.Show_MP_ExtremeWarning = false;
			if (((ToggleButton)RefExtremeWarningCheck).IsChecked == true)
			{
				ConfigSettings.Settings_Show_Extreme_Warning = false;
				ConfigSettings.SaveSettings();
			}
			switch (ExtremeWarningSource)
			{
			case 0:
				ButtonClicked("DoHost");
				break;
			case 1:
				ButtonClicked("Join");
				break;
			case 2:
				break;
			}
			break;
		case "CloseExtremeWarningNo":
			MainViewModel.Instance.Show_MP_ExtremeWarning = false;
			skirmishExtremeTroopsWarningShown = false;
			if (((ToggleButton)RefExtremeWarningCheck).IsChecked == true)
			{
				ConfigSettings.Settings_Show_Extreme_Warning = false;
				ConfigSettings.SaveSettings();
			}
			if (skirmishGame)
			{
				MPsetupData.extreme_troops = 0;
				MainViewModel.Instance.MP_Settings_ExTroops = MainViewModel.Instance.GameSprites[641];
			}
			break;
		case "ShowFaces":
			updateRadarFaces();
			MainViewModel.Instance.MP_ShowFaces = true;
			break;
		case "HideFaces":
			MainViewModel.Instance.MP_ShowFaces = false;
			break;
		case "Settings_SkirmishAdvanced":
			((ToggleButton)RefEnableAdvancedSkirmishCheck).IsChecked = MPsetupData.advanced_skirmish_options > 0;
			if (MPsetupData.advanced_skirmish_options > 0)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Opacity = 0.5f;
			}
			MainViewModel.Instance.Show_MP_SkirmishAdvanced = true;
			break;
		case "CloseSkirmishAdvanced":
			MainViewModel.Instance.Show_MP_SkirmishAdvanced = false;
			if (MPsetupData.advanced_skirmish_options > 0)
			{
				MainViewModel.Instance.Show_SkirmishAdvancedEnabled = MPsetupData.advancedSkirmishOptionsEnabled();
			}
			else
			{
				MainViewModel.Instance.Show_SkirmishAdvancedEnabled = false;
			}
			break;
		case "EnableSpectatorMode":
		{
			for (int num16 = 0; num16 < 8; num16++)
			{
				if (num16 >= playerRows.Length)
				{
					continue;
				}
				int playerID = playerRows[num16].playerID;
				if (playerID >= 1 && playerID <= 8)
				{
					Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID = currentLobby.GetLobbyMemberFromThis_PlayerID(playerID);
					if (lobbyMemberFromThis_PlayerID != null && lobbyMemberFromThis_PlayerID.SkirmishHumanMember)
					{
						ButtonClicked("Kick_" + (num16 + 1));
						break;
					}
				}
			}
			spectatorMode = true;
			MainViewModel.Instance.StandaloneTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 526);
			ButtonClicked("CloseSkirmishAdvanced");
			break;
		}
		case "RememberSkirmishAdvanced":
			ConfigSettings.Settings_SkirmishPresets = MPsetupData.ToStringCustomSkirmish();
			ConfigSettings.SaveSettings();
			break;
		case "Settings_ImprovedSieging":
			if (skirmishGame)
			{
				if (MPsetupData.global_improved_sieging == 0)
				{
					MPsetupData.global_improved_sieging = 1;
				}
				else
				{
					MPsetupData.global_improved_sieging = 0;
				}
				if (MPsetupData.global_improved_sieging > 0)
				{
					MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPTEMPsetupData.global_improved_sieging == 0)
				{
					MPTEMPsetupData.global_improved_sieging = 1;
				}
				else
				{
					MPTEMPsetupData.global_improved_sieging = 0;
				}
				if (MPTEMPsetupData.global_improved_sieging > 0)
				{
					MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[641];
				}
			}
			break;
		case "Settings_PreBuild":
			if (skirmishGame)
			{
				if (MPsetupData.advopt_pre_build == 0)
				{
					MPsetupData.advopt_pre_build = 1;
				}
				else
				{
					MPsetupData.advopt_pre_build = 0;
				}
				if (MPsetupData.advopt_pre_build > 0)
				{
					MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPTEMPsetupData.advopt_pre_build == 0)
				{
					MPTEMPsetupData.advopt_pre_build = 1;
				}
				else
				{
					MPTEMPsetupData.advopt_pre_build = 0;
				}
				if (MPTEMPsetupData.advopt_pre_build > 0)
				{
					MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[641];
				}
			}
			break;
		case "Settings_Adv_ASword":
			if (skirmishGame)
			{
				if (MPsetupData.advopt_improved_arabswordsmen == 0)
				{
					MPsetupData.advopt_improved_arabswordsmen = 1;
				}
				else
				{
					MPsetupData.advopt_improved_arabswordsmen = 0;
				}
				if (MPsetupData.advopt_improved_arabswordsmen > 0)
				{
					MainViewModel.Instance.MP_Settings_Adv_ASword = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Adv_ASword = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPTEMPsetupData.advopt_improved_arabswordsmen == 0)
				{
					MPTEMPsetupData.advopt_improved_arabswordsmen = 1;
				}
				else
				{
					MPTEMPsetupData.advopt_improved_arabswordsmen = 0;
				}
				if (MPTEMPsetupData.advopt_improved_arabswordsmen > 0)
				{
					MainViewModel.Instance.MP_Settings_Adv_ASword = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Adv_ASword = MainViewModel.Instance.GameSprites[641];
				}
			}
			break;
		case "Settings_Adv_HorseArchers":
			if (skirmishGame)
			{
				if (MPsetupData.advopt_rebalanced_horsearchers == 0)
				{
					MPsetupData.advopt_rebalanced_horsearchers = 1;
				}
				else
				{
					MPsetupData.advopt_rebalanced_horsearchers = 0;
				}
				if (MPsetupData.advopt_rebalanced_horsearchers > 0)
				{
					MainViewModel.Instance.MP_Settings_Adv_HorseArchers = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Adv_HorseArchers = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPTEMPsetupData.advopt_rebalanced_horsearchers == 0)
				{
					MPTEMPsetupData.advopt_rebalanced_horsearchers = 1;
				}
				else
				{
					MPTEMPsetupData.advopt_rebalanced_horsearchers = 0;
				}
				if (MPTEMPsetupData.advopt_rebalanced_horsearchers > 0)
				{
					MainViewModel.Instance.MP_Settings_Adv_HorseArchers = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Adv_HorseArchers = MainViewModel.Instance.GameSprites[641];
				}
			}
			break;
		case "Settings_Adv_Laddermen":
			if (skirmishGame)
			{
				if (MPsetupData.advopt_improved_laddermen == 0)
				{
					MPsetupData.advopt_improved_laddermen = 1;
				}
				else
				{
					MPsetupData.advopt_improved_laddermen = 0;
				}
				if (MPsetupData.advopt_improved_laddermen > 0)
				{
					MainViewModel.Instance.MP_Settings_Adv_Laddermen = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Adv_Laddermen = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPTEMPsetupData.advopt_improved_laddermen == 0)
				{
					MPTEMPsetupData.advopt_improved_laddermen = 1;
				}
				else
				{
					MPTEMPsetupData.advopt_improved_laddermen = 0;
				}
				if (MPTEMPsetupData.advopt_improved_laddermen > 0)
				{
					MainViewModel.Instance.MP_Settings_Adv_Laddermen = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Adv_Laddermen = MainViewModel.Instance.GameSprites[641];
				}
			}
			break;
		case "Settings_Adv_Spearmen":
			if (skirmishGame)
			{
				if (MPsetupData.advopt_improved_spearmen == 0)
				{
					MPsetupData.advopt_improved_spearmen = 1;
				}
				else
				{
					MPsetupData.advopt_improved_spearmen = 0;
				}
				if (MPsetupData.advopt_improved_spearmen > 0)
				{
					MainViewModel.Instance.MP_Settings_Adv_Spearmen = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Adv_Spearmen = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPTEMPsetupData.advopt_improved_spearmen == 0)
				{
					MPTEMPsetupData.advopt_improved_spearmen = 1;
				}
				else
				{
					MPTEMPsetupData.advopt_improved_spearmen = 0;
				}
				if (MPTEMPsetupData.advopt_improved_spearmen > 0)
				{
					MainViewModel.Instance.MP_Settings_Adv_Spearmen = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Adv_Spearmen = MainViewModel.Instance.GameSprites[641];
				}
			}
			break;
		case "Settings_Adv_Fletchers":
			if (skirmishGame)
			{
				if (MPsetupData.advopt_improved_fletchers == 0)
				{
					MPsetupData.advopt_improved_fletchers = 1;
				}
				else
				{
					MPsetupData.advopt_improved_fletchers = 0;
				}
				if (MPsetupData.advopt_improved_fletchers > 0)
				{
					MainViewModel.Instance.MP_Settings_Adv_Fletchers = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Adv_Fletchers = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPTEMPsetupData.advopt_improved_fletchers == 0)
				{
					MPTEMPsetupData.advopt_improved_fletchers = 1;
				}
				else
				{
					MPTEMPsetupData.advopt_improved_fletchers = 0;
				}
				if (MPTEMPsetupData.advopt_improved_fletchers > 0)
				{
					MainViewModel.Instance.MP_Settings_Adv_Fletchers = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Adv_Fletchers = MainViewModel.Instance.GameSprites[641];
				}
			}
			break;
		case "Settings_Adv_Uncapped_Peasants":
			if (skirmishGame)
			{
				if (MPsetupData.advopt_uncapped_peasants == 0)
				{
					MPsetupData.advopt_uncapped_peasants = 1;
				}
				else
				{
					MPsetupData.advopt_uncapped_peasants = 0;
				}
				if (MPsetupData.advopt_uncapped_peasants > 0)
				{
					MainViewModel.Instance.MP_Settings_Uncapped_Peasants = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Uncapped_Peasants = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPTEMPsetupData.advopt_uncapped_peasants == 0)
				{
					MPTEMPsetupData.advopt_uncapped_peasants = 1;
				}
				else
				{
					MPTEMPsetupData.advopt_uncapped_peasants = 0;
				}
				if (MPTEMPsetupData.advopt_uncapped_peasants > 0)
				{
					MainViewModel.Instance.MP_Settings_Uncapped_Peasants = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Uncapped_Peasants = MainViewModel.Instance.GameSprites[641];
				}
			}
			break;
		case "Settings_Adv_Faster_Peasants":
			if (skirmishGame)
			{
				if (MPsetupData.advopt_faster_peasants == 0)
				{
					MPsetupData.advopt_faster_peasants = 1;
				}
				else
				{
					MPsetupData.advopt_faster_peasants = 0;
				}
				if (MPsetupData.advopt_faster_peasants > 0)
				{
					MainViewModel.Instance.MP_Settings_Faster_Peasants = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Faster_Peasants = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPTEMPsetupData.advopt_faster_peasants == 0)
				{
					MPTEMPsetupData.advopt_faster_peasants = 1;
				}
				else
				{
					MPTEMPsetupData.advopt_faster_peasants = 0;
				}
				if (MPTEMPsetupData.advopt_faster_peasants > 0)
				{
					MainViewModel.Instance.MP_Settings_Faster_Peasants = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Faster_Peasants = MainViewModel.Instance.GameSprites[641];
				}
			}
			break;
		case "Settings_Adv_Healers":
			if (skirmishGame)
			{
				if (MPsetupData.advopt_healers == 0)
				{
					MPsetupData.advopt_healers = 1;
				}
				else
				{
					MPsetupData.advopt_healers = 0;
				}
				if (MPsetupData.advopt_healers > 0)
				{
					MainViewModel.Instance.MP_Settings_Healers = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Healers = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPTEMPsetupData.advopt_healers == 0)
				{
					MPTEMPsetupData.advopt_healers = 1;
				}
				else
				{
					MPTEMPsetupData.advopt_healers = 0;
				}
				if (MPTEMPsetupData.advopt_healers > 0)
				{
					MainViewModel.Instance.MP_Settings_Healers = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Healers = MainViewModel.Instance.GameSprites[641];
				}
			}
			break;
		case "Settings_Adv_Eunuchs":
			if (skirmishGame)
			{
				if (MPsetupData.advopt_eunuchs == 0)
				{
					MPsetupData.advopt_eunuchs = 1;
				}
				else
				{
					MPsetupData.advopt_eunuchs = 0;
				}
				if (MPsetupData.advopt_eunuchs > 0)
				{
					MainViewModel.Instance.MP_Settings_Eunuchs = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Eunuchs = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPTEMPsetupData.advopt_eunuchs == 0)
				{
					MPTEMPsetupData.advopt_eunuchs = 1;
				}
				else
				{
					MPTEMPsetupData.advopt_eunuchs = 0;
				}
				if (MPTEMPsetupData.advopt_eunuchs > 0)
				{
					MainViewModel.Instance.MP_Settings_Eunuchs = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_Eunuchs = MainViewModel.Instance.GameSprites[641];
				}
			}
			break;
		case "Settings_Adv_NoGold":
			if (skirmishGame)
			{
				if (MPsetupData.advopt_nogold == 0)
				{
					MPsetupData.advopt_nogold = 1;
				}
				else
				{
					MPsetupData.advopt_nogold = 0;
				}
				if (MPsetupData.advopt_nogold > 0)
				{
					MainViewModel.Instance.MP_Settings_NoGold = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_NoGold = MainViewModel.Instance.GameSprites[641];
				}
				updateSkirmishStartingGoldLevels();
			}
			else
			{
				if (MPTEMPsetupData.advopt_nogold == 0)
				{
					MPTEMPsetupData.advopt_nogold = 1;
				}
				else
				{
					MPTEMPsetupData.advopt_nogold = 0;
				}
				if (MPTEMPsetupData.advopt_nogold > 0)
				{
					MainViewModel.Instance.MP_Settings_NoGold = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_NoGold = MainViewModel.Instance.GameSprites[641];
				}
				updateStartingGoldLevels();
			}
			break;
		case "Settings_Adv_EnemyHPS":
			if (skirmishGame)
			{
				MPsetupData.advopt_enemy_hps++;
				if (MPsetupData.advopt_enemy_hps > 3)
				{
					MPsetupData.advopt_enemy_hps = 0;
				}
				string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 413) + " ";
				switch (MPsetupData.advopt_enemy_hps)
				{
				case 0:
					text += "66%";
					break;
				case 1:
					text += "100%";
					break;
				case 2:
					text += "125%";
					break;
				case 3:
					text += "150%";
					break;
				}
				MainViewModel.Instance.MP_Settings_enemyhps = text;
			}
			else
			{
				MPTEMPsetupData.advopt_enemy_hps++;
				if (MPTEMPsetupData.advopt_enemy_hps > 3)
				{
					MPTEMPsetupData.advopt_enemy_hps = 0;
				}
				string text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 413) + " ";
				switch (MPTEMPsetupData.advopt_enemy_hps)
				{
				case 0:
					text2 += "66%";
					break;
				case 1:
					text2 += "100%";
					break;
				case 2:
					text2 += "125%";
					break;
				case 3:
					text2 += "150%";
					break;
				}
				MainViewModel.Instance.MP_Settings_enemyhps = text2;
			}
			break;
		case "TMTest":
			StartSkirmishGame();
			break;
		case "TMManage":
		{
			HUD_IngameMenu.RestartSkirmishMapInfo restartSkirmishMapInfo = new HUD_IngameMenu.RestartSkirmishMapInfo();
			restartSkirmishMapInfo.MPsetupData = MPsetupData;
			restartSkirmishMapInfo.selectedHeader = selectedMPHeader;
			restartSkirmishMapInfo.importMembers(currentLobby);
			restartSkirmishMapInfo.importAIVs(AIVs);
			MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo = restartSkirmishMapInfo;
			MainViewModel.Instance.HUDIngameMenu.restartMapInfo = null;
			MainViewModel.Instance.HUDIngameMenu.restartMPInfo = null;
			FRONT_ManageTrail.Show(lastCanStart);
			break;
		}
		case "CloseManageTrail":
			MainViewModel.Instance.Show_ManageTrail = false;
			break;
		}
	}

	public void EnableAdvancedSkirmishCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (((ToggleButton)RefEnableAdvancedSkirmishCheck).IsChecked.Value)
		{
			MPsetupData.advanced_skirmish_options = 1;
			MainViewModel.Instance.MPSettings_AdvSkirmish_Opacity = 1f;
		}
		else
		{
			MPsetupData.advanced_skirmish_options = 0;
			MainViewModel.Instance.MPSettings_AdvSkirmish_Opacity = 0.5f;
		}
	}

	public void MuteMPChat_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			Platform_Multiplayer.MPChatMuted = ((ToggleButton)RefChatMuteDisable).IsChecked.Value;
			((UIElement)RefMP_ChatSend).IsEnabled = !Platform_Multiplayer.MPChatMuted;
		}
	}

	public void AutoJoinLobby(Platform_Multiplayer.MPLobby joiningLobby)
	{
		currentLobby = joiningLobby;
		if (currentLobby.coopTrailGame)
		{
			ConfigSettings.CalcCoopProgress(1uL);
			if (currentLobby.coopTrailID == 0)
			{
				FrontendMenus.CurrentSelectedTrailCoop1Mission = 1;
				FrontendMenus.CurrentSelectedTrail = 21;
				MainViewModel.Instance.FrontEndMenu.GenerateSwords();
				MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(1);
				MainViewModel.Instance.Show_CoopTrail1 = true;
			}
			else if (currentLobby.coopTrailID == 1)
			{
				FrontendMenus.CurrentSelectedTrailCoop2Mission = 1;
				FrontendMenus.CurrentSelectedTrail = 22;
				MainViewModel.Instance.FrontEndMenu.GenerateSwords();
				MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(1);
				MainViewModel.Instance.Show_CoopTrail2 = true;
			}
			else if (currentLobby.coopTrailID == 2)
			{
				FrontendMenus.CurrentSelectedTrailCoop3Mission = 1;
				FrontendMenus.CurrentSelectedTrail = 23;
				MainViewModel.Instance.FrontEndMenu.GenerateSwords();
				MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(1);
				MainViewModel.Instance.Show_CoopTrail3 = true;
			}
			coopGame = true;
			coopGame_IsHost = false;
			MainViewModel.Instance.Show_CoopClientPane = true;
			MainViewModel.Instance.Show_CoopHostInvitePane = false;
			MainViewModel.Instance.Show_CoopHostJoinedPane = false;
			ulong coopPartnerID = Platform_Multiplayer.Instance.GetCoopPartnerID();
			if (coopPartnerID != 0L)
			{
				ConfigSettings.InitCoopGame(coopPartnerID, Platform_Multiplayer.Instance.getSteamUserName(coopPartnerID), Platform_Multiplayer.Instance.LastCoAString);
			}
		}
		else
		{
			coopGame = false;
			MainViewModel.Instance.Show_CoopTrail1 = false;
			MainViewModel.Instance.Show_CoopTrail2 = false;
		}
		ShowSetupScreen();
	}

	public void UpdateButtons()
	{
	}

	public void SetupSkirmishModeSettings()
	{
		MainViewModel.Instance.MPGame_Type_Description = "";
		MainViewModel.Instance.Show_MPGame_Type_Description = false;
		switch (MPsetupData.fairness)
		{
		case 1:
			((ToggleButton)RefFairness1).IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 3);
			break;
		case 2:
			((ToggleButton)RefFairness2).IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 3);
			break;
		case 3:
			((ToggleButton)RefFairness3).IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = "";
			break;
		case 4:
			((ToggleButton)RefFairness4).IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 4);
			break;
		case 5:
			((ToggleButton)RefFairness5).IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 4);
			break;
		}
		switch (MPsetupData.starting_goods_level)
		{
		case 1:
			((ToggleButton)RefGameType1).IsChecked = true;
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 0);
			break;
		case 2:
			((ToggleButton)RefGameType2).IsChecked = true;
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 1);
			break;
		case 3:
			((ToggleButton)RefGameType3).IsChecked = true;
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 2);
			break;
		}
		if (MPsetupData.extreme_powers > 0)
		{
			MainViewModel.Instance.MPSettings_ExPowersLord_Opacity = 1f;
			MainViewModel.Instance.MP_Settings_ExPowers = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MPSettings_ExPowersLord_Opacity = 0.5f;
			MainViewModel.Instance.MP_Settings_ExPowers = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.extreme_powers_around_lord > 0 && MPsetupData.extreme_powers > 0)
		{
			MainViewModel.Instance.MP_Settings_ExPowersLord = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_ExPowersLord = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.extreme_troops > 0)
		{
			MainViewModel.Instance.MP_Settings_ExTroops = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_ExTroops = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.allow_outposts > 0)
		{
			MainViewModel.Instance.MP_Settings_AllowOutposts = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_AllowOutposts = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.global_improved_sieging > 0)
		{
			MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.advopt_pre_build > 0)
		{
			MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.advopt_improved_arabswordsmen > 0)
		{
			MainViewModel.Instance.MP_Settings_Adv_ASword = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Adv_ASword = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.advopt_rebalanced_horsearchers > 0)
		{
			MainViewModel.Instance.MP_Settings_Adv_HorseArchers = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Adv_HorseArchers = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.advopt_improved_laddermen > 0)
		{
			MainViewModel.Instance.MP_Settings_Adv_Laddermen = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Adv_Laddermen = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.advopt_improved_spearmen > 0)
		{
			MainViewModel.Instance.MP_Settings_Adv_Spearmen = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Adv_Spearmen = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.advopt_improved_fletchers > 0)
		{
			MainViewModel.Instance.MP_Settings_Adv_Fletchers = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Adv_Fletchers = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.advopt_uncapped_peasants > 0)
		{
			MainViewModel.Instance.MP_Settings_Uncapped_Peasants = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Uncapped_Peasants = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.advopt_faster_peasants > 0)
		{
			MainViewModel.Instance.MP_Settings_Faster_Peasants = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Faster_Peasants = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.advopt_healers > 0)
		{
			MainViewModel.Instance.MP_Settings_Healers = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Healers = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.advopt_eunuchs > 0)
		{
			MainViewModel.Instance.MP_Settings_Eunuchs = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Eunuchs = MainViewModel.Instance.GameSprites[641];
		}
		if (MPsetupData.advopt_nogold > 0)
		{
			MainViewModel.Instance.MP_Settings_NoGold = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_NoGold = MainViewModel.Instance.GameSprites[641];
		}
		string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 413) + " ";
		switch (MPsetupData.advopt_enemy_hps)
		{
		case 0:
			text += "66%";
			break;
		case 1:
			text += "100%";
			break;
		case 2:
			text += "125%";
			break;
		case 3:
			text += "150%";
			break;
		}
		MainViewModel.Instance.MP_Settings_enemyhps = text;
		((RangeBase)FRONT_CoopTrail1.Instance.RefMP_Settings_GameSpeed_Slider).Value = MPsetupData.starting_gamespeed / 5;
		((RangeBase)FRONT_CoopTrail2.Instance.RefMP_Settings_GameSpeed_Slider).Value = MPsetupData.starting_gamespeed / 5;
		((RangeBase)FRONT_CoopTrail3.Instance.RefMP_Settings_GameSpeed_Slider).Value = MPsetupData.starting_gamespeed / 5;
		MainViewModel.Instance.MP_Settings_GameSpeed = MPsetupData.starting_gamespeed.ToString();
		switch (MPsetupData.autosave)
		{
		case 0:
			MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_MACEMEN);
			break;
		case 5:
			MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_SWORDSMEN);
			break;
		case 10:
			MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_KNIGHTS);
			break;
		case 20:
			MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_LADDERMEN);
			break;
		}
		if (coopGame)
		{
			if (singlePlayerCoop)
			{
				MainViewModel.Instance.MPSettings_Autosave_Opacity = 0.3f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_Autosave_Opacity = 1f;
			}
		}
		updateSkirmishStartingGoldLevels();
	}

	public void updateSkirmishStartingGoldLevels()
	{
		int num = 1;
		if (extremeTrailCustomised)
		{
			num = 3;
		}
		int num2 = MPsetupData.starting_goods_level - 1;
		int num3 = MPsetupData.fairness - 1;
		if (MPsetupData.advanced_skirmish_options > 0 && MPsetupData.advopt_nogold > 0)
		{
			MainViewModel.Instance.MPGame_GoldHuman = "0";
		}
		else
		{
			MainViewModel.Instance.MPGame_GoldHuman = (GameData.starting_gold_table[num2, num3, 0] * num).ToString();
		}
		MainViewModel.Instance.MPGame_GoldComputer = (GameData.starting_gold_table[num2, num3, 1] * num).ToString();
	}

	public void ImportSettings(string settings, bool isHost = false)
	{
		if (MPTEMPsetupData == null)
		{
			return;
		}
		MPTEMPsetupData.FromString(settings);
		if (isHost)
		{
			MainViewModel.Instance.MPSettings_Fairness_Opacity = 1f;
			MainViewModel.Instance.MPSettings_GameType_Opacity = 1f;
			MainViewModel.Instance.MPSettings_StrongWall_Opacity = 1f;
			MainViewModel.Instance.MPSettings_Cows_Opacity = 1f;
			MainViewModel.Instance.MPSettings_Dogs_Opacity = 1f;
			MainViewModel.Instance.MPSettings_Autotrading_Opacity = 1f;
			MainViewModel.Instance.MPSettings_Autosave_Opacity = 1f;
			MainViewModel.Instance.MPSettings_GameSpeed_Opacity = 1f;
			MainViewModel.Instance.MPSettings_PeaceTime_Opacity = 1f;
			MainViewModel.Instance.MPSettings_ExTroops_Opacity = 0.5f;
			MainViewModel.Instance.MPSettings_ExPowers_Opacity = 1f;
			MainViewModel.Instance.MPSettings_ExPowersLord_Opacity = 1f;
			MainViewModel.Instance.MPSettings_AllowOutposts_Opacity = 1f;
		}
		else
		{
			if (MPDefaultsetupData.fairness != MPTEMPsetupData.fairness)
			{
				MainViewModel.Instance.MPSettings_Fairness_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_Fairness_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.starting_goods_level != MPTEMPsetupData.starting_goods_level)
			{
				MainViewModel.Instance.MPSettings_GameType_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_GameType_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.no_knockdown_walls != MPTEMPsetupData.no_knockdown_walls)
			{
				MainViewModel.Instance.MPSettings_StrongWall_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_StrongWall_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.no_cows != MPTEMPsetupData.no_cows)
			{
				MainViewModel.Instance.MPSettings_Cows_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_Cows_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.no_dogs != MPTEMPsetupData.no_dogs)
			{
				MainViewModel.Instance.MPSettings_Dogs_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_Dogs_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.allow_autotrading != MPTEMPsetupData.allow_autotrading)
			{
				MainViewModel.Instance.MPSettings_Autotrading_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_Autotrading_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.autosave != MPTEMPsetupData.autosave)
			{
				MainViewModel.Instance.MPSettings_Autosave_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_Autosave_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.starting_gamespeed != MPTEMPsetupData.starting_gamespeed)
			{
				MainViewModel.Instance.MPSettings_GameSpeed_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_GameSpeed_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.peacetime != MPTEMPsetupData.peacetime)
			{
				MainViewModel.Instance.MPSettings_PeaceTime_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_PeaceTime_Opacity = 0.5f;
			}
			MainViewModel.Instance.MPSettings_ExTroops_Opacity = 0.5f;
			if (MPDefaultsetupData.extreme_powers != MPTEMPsetupData.extreme_powers)
			{
				MainViewModel.Instance.MPSettings_ExPowers_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_ExPowers_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.extreme_powers_around_lord != MPTEMPsetupData.extreme_powers_around_lord)
			{
				MainViewModel.Instance.MPSettings_ExPowersLord_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_ExPowersLord_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.allow_outposts != MPTEMPsetupData.allow_outposts)
			{
				MainViewModel.Instance.MPSettings_AllowOutposts_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AllowOutposts_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.advopt_enemy_hps != MPTEMPsetupData.advopt_enemy_hps)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_EnemyHPS_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_EnemyHPS_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.global_improved_sieging != MPTEMPsetupData.global_improved_sieging)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Improved_Sieging_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Improved_Sieging_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.advopt_pre_build != MPTEMPsetupData.advopt_pre_build)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_PreBuild_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_PreBuild_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.advopt_improved_arabswordsmen != MPTEMPsetupData.advopt_improved_arabswordsmen)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Improved_ASword_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Improved_ASword_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.advopt_rebalanced_horsearchers != MPTEMPsetupData.advopt_rebalanced_horsearchers)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_HorseA_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_HorseA_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.advopt_improved_laddermen != MPTEMPsetupData.advopt_improved_laddermen)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Ladder_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Ladder_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.advopt_improved_spearmen != MPTEMPsetupData.advopt_improved_spearmen)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Spear_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Spear_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.advopt_improved_fletchers != MPTEMPsetupData.advopt_improved_fletchers)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Fletch_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Fletch_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.advopt_uncapped_peasants != MPTEMPsetupData.advopt_uncapped_peasants)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Uncapped_Peasants_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Uncapped_Peasants_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.advopt_uncapped_peasants != MPTEMPsetupData.advopt_uncapped_peasants)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Uncapped_Peasants_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Uncapped_Peasants_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.advopt_faster_peasants != MPTEMPsetupData.advopt_faster_peasants)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Faster_peasants_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Faster_peasants_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.advopt_healers != MPTEMPsetupData.advopt_healers)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Healers_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Healers_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.advopt_eunuchs != MPTEMPsetupData.advopt_eunuchs)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Eunuchs_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Eunuchs_Opacity = 0.5f;
			}
			if (MPDefaultsetupData.advopt_healers != MPTEMPsetupData.advopt_healers)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_NoGold_Opacity = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_NoGold_Opacity = 0.5f;
			}
		}
		switch (MPTEMPsetupData.fairness)
		{
		case 1:
			((ToggleButton)FRONT_Multiplayer_Setup.Instance.RefFairness1).IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 3);
			break;
		case 2:
			((ToggleButton)FRONT_Multiplayer_Setup.Instance.RefFairness2).IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 3);
			break;
		case 3:
			((ToggleButton)FRONT_Multiplayer_Setup.Instance.RefFairness3).IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = "";
			break;
		case 4:
			((ToggleButton)FRONT_Multiplayer_Setup.Instance.RefFairness4).IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 4);
			break;
		case 5:
			((ToggleButton)FRONT_Multiplayer_Setup.Instance.RefFairness5).IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 4);
			break;
		}
		MainViewModel.Instance.MPGame_Type_Description = "";
		MainViewModel.Instance.Show_MPGame_Type_Description = false;
		switch (MPTEMPsetupData.starting_goods_level)
		{
		case 1:
			((ToggleButton)FRONT_Multiplayer_Setup.Instance.RefGameType1).IsChecked = true;
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 0);
			break;
		case 2:
			((ToggleButton)FRONT_Multiplayer_Setup.Instance.RefGameType2).IsChecked = true;
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 1);
			break;
		case 3:
			((ToggleButton)FRONT_Multiplayer_Setup.Instance.RefGameType3).IsChecked = true;
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 2);
			break;
		}
		updateStartingGoldLevels();
		((RangeBase)FRONT_Multiplayer_Setup.Instance.RefMP_Settings_GameSpeed_Slider).Value = MPTEMPsetupData.starting_gamespeed / 5;
		MainViewModel.Instance.MP_Settings_GameSpeed = MPTEMPsetupData.starting_gamespeed.ToString();
		((RangeBase)FRONT_Multiplayer_Setup.Instance.RefMP_Settings_Peacetime_Slider).Value = MPTEMPsetupData.peacetime;
		if (FatControler.ukrainian)
		{
			MainViewModel.Instance.MP_Settings_Peacetime = MPTEMPsetupData.peacetime + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 168);
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Peacetime = MPTEMPsetupData.peacetime + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 168);
		}
		if (MPTEMPsetupData.no_knockdown_walls > 0)
		{
			MainViewModel.Instance.MP_Settings_Wall = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Wall = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.no_dogs > 0)
		{
			MainViewModel.Instance.MP_Settings_Dogs = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Dogs = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.no_cows > 0)
		{
			MainViewModel.Instance.MP_Settings_Cows = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Cows = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.allow_autotrading > 0)
		{
			MainViewModel.Instance.MP_Settings_Autotrading = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Autotrading = MainViewModel.Instance.GameSprites[641];
		}
		switch (MPTEMPsetupData.autosave)
		{
		case 0:
			MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_MACEMEN);
			break;
		case 5:
			MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_SWORDSMEN);
			break;
		case 10:
			MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_KNIGHTS);
			break;
		case 20:
			MainViewModel.Instance.MP_Settings_Autosave = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_SCN_LADDERMEN);
			break;
		}
		if (MPTEMPsetupData.extreme_powers > 0)
		{
			if (isHost)
			{
				MainViewModel.Instance.MPSettings_ExPowersLord_Opacity = 1f;
			}
			MainViewModel.Instance.MP_Settings_ExPowers = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			if (isHost)
			{
				MainViewModel.Instance.MPSettings_ExPowersLord_Opacity = 0.5f;
			}
			MainViewModel.Instance.MP_Settings_ExPowers = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.extreme_powers_around_lord > 0 && MPTEMPsetupData.extreme_powers > 0)
		{
			MainViewModel.Instance.MP_Settings_ExPowersLord = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_ExPowersLord = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.extreme_troops > 0)
		{
			MainViewModel.Instance.MP_Settings_ExTroops = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_ExTroops = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.allow_outposts > 0)
		{
			MainViewModel.Instance.MP_Settings_AllowOutposts = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_AllowOutposts = MainViewModel.Instance.GameSprites[641];
		}
		((RangeBase)FRONT_Multiplayer_Setup.Instance.RefSetupMaxPlayersSlider).Value = PlayerCap;
		MainViewModel.Instance.Show_MPSettings_AdvancedOptions = MPTEMPsetupData.advanced_options != 0;
		if (MainViewModel.Instance.Show_MPSettings_AdvancedOptions)
		{
			MainViewModel.Instance.MPSettings_AdvancedButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKIRMISH_MISC, 20);
		}
		else
		{
			MainViewModel.Instance.MPSettings_AdvancedButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKIRMISH_MISC, 19);
		}
		for (int i = 0; i < 25; i++)
		{
			MainViewModel.Instance.TradingGoodsBool[i] = MPTEMPsetupData.MP_GoodsAvailable[i] != 0;
		}
		for (int j = 0; j < 13; j++)
		{
			MainViewModel.Instance.MPSetupBuildingsBool[j] = MPTEMPsetupData.MP_BuildingsAvailable[j] != 0;
		}
		for (int k = 0; k < 32; k++)
		{
			MainViewModel.Instance.MPSetupTroopsBool[k] = MPTEMPsetupData.MP_TroopsAvailable[k] != 0;
		}
		if (MPTEMPsetupData.global_improved_sieging > 0)
		{
			MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.advopt_pre_build > 0)
		{
			MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.advopt_improved_arabswordsmen > 0)
		{
			MainViewModel.Instance.MP_Settings_Adv_ASword = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Adv_ASword = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.advopt_rebalanced_horsearchers > 0)
		{
			MainViewModel.Instance.MP_Settings_Adv_HorseArchers = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Adv_HorseArchers = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.advopt_improved_laddermen > 0)
		{
			MainViewModel.Instance.MP_Settings_Adv_Laddermen = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Adv_Laddermen = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.advopt_improved_spearmen > 0)
		{
			MainViewModel.Instance.MP_Settings_Adv_Spearmen = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Adv_Spearmen = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.advopt_improved_fletchers > 0)
		{
			MainViewModel.Instance.MP_Settings_Adv_Fletchers = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Adv_Fletchers = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.advopt_uncapped_peasants > 0)
		{
			MainViewModel.Instance.MP_Settings_Uncapped_Peasants = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Uncapped_Peasants = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.advopt_faster_peasants > 0)
		{
			MainViewModel.Instance.MP_Settings_Faster_Peasants = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Faster_Peasants = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.advopt_healers > 0)
		{
			MainViewModel.Instance.MP_Settings_Healers = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Healers = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.advopt_eunuchs > 0)
		{
			MainViewModel.Instance.MP_Settings_Eunuchs = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_Eunuchs = MainViewModel.Instance.GameSprites[641];
		}
		if (MPTEMPsetupData.advopt_nogold > 0)
		{
			MainViewModel.Instance.MP_Settings_NoGold = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_NoGold = MainViewModel.Instance.GameSprites[641];
		}
		string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 413) + " ";
		switch (MPTEMPsetupData.advopt_enemy_hps)
		{
		case 0:
			text += "66%";
			break;
		case 1:
			text += "100%";
			break;
		case 2:
			text += "125%";
			break;
		case 3:
			text += "150%";
			break;
		}
		MainViewModel.Instance.MP_Settings_enemyhps = text;
	}

	public void UpdateLobbySettingsButton()
	{
		if (selectedLobby != null && EngineInterface.MultiplayerSetupData.compareSettingsStrings(selectedLobby.settings, defaultMPSettings))
		{
			((UIElement)RefLobbySettingsButton).Visibility = (Visibility)2;
		}
		else
		{
			((UIElement)RefLobbySettingsButton).Visibility = (Visibility)1;
		}
	}

	public void UpdateLobbyChangeButtons()
	{
		if (MPLobbyMode == 0)
		{
			MainViewModel.Instance.LobbyTypeHeading = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 197);
			MainViewModel.Instance.LobbyTypeButton = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 196);
		}
		else
		{
			MainViewModel.Instance.LobbyTypeHeading = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 198);
			MainViewModel.Instance.LobbyTypeButton = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 195);
		}
	}

	public void Update()
	{
		//IL_188a: Unknown result type (might be due to invalid IL or missing references)
		//IL_188f: Unknown result type (might be due to invalid IL or missing references)
		//IL_18b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_18ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_18f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_1918: Unknown result type (might be due to invalid IL or missing references)
		//IL_0865: Unknown result type (might be due to invalid IL or missing references)
		//IL_09bb: Unknown result type (might be due to invalid IL or missing references)
		if (hideToolTipTime != DateTime.MinValue && DateTime.UtcNow > hideToolTipTime)
		{
			hideToolTipTime = DateTime.MinValue;
			MainViewModel.Instance.MPGame_Type_Description = "";
			MainViewModel.Instance.Show_MPGame_Type_Description = false;
			MainViewModel.Instance.AI_Settings_Help = "";
			MainViewModel.Instance.Show_AI_Settings_Help = false;
		}
		MonitorAILordText();
		if (MPGameLoading)
		{
			return;
		}
		if (coopGame)
		{
			Update_Coop();
		}
		if (MPLocalReady)
		{
			Platform_Multiplayer.Instance.ReceiveGameMessages();
		}
		if (!MainViewModel.Instance.Show_CreatingMPHost)
		{
			bool flag = false;
			if (!skirmishGame)
			{
				bool refreshTeams = false;
				bool settingsChanged = false;
				bool flag2 = MPsetupData.advanced_options > 0;
				bool num = Platform_Multiplayer.Instance.RefreshLobbyList(ref MPsetupData, ref refreshTeams, ref settingsChanged, coopGame);
				if (updateSteamIDMappings())
				{
					refreshTeams = true;
				}
				if (num)
				{
					LeaveLobby();
					ShowLobbyScreen();
				}
				else if (refreshTeams)
				{
					UpdateCustomLordNamesFromMP();
					UpdateHostInfo();
				}
				if (currentLobby != null && (GameData.Instance.setKeepOrder(MPsetupData.start_keep_location_order) || refreshTeams))
				{
					ReSortTeamInfo();
					UpdateRadarShieldPositions();
					flag = true;
					CreateTeamShields();
				}
				if (settingsChanged && DateTime.UtcNow > justEnteredSetupScreen)
				{
					if (currentLobby != null && !currentLobby.isHost)
					{
						MainViewModel.Instance.MP_Settings_Button = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 59);
						settingsPulseAnimation.Begin();
						if (!coopGame)
						{
							if (MPsetupData.advanced_options > 0 && !flag2)
							{
								receivedLobbyChat("", Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKIRMISH_MISC, 27), -1, systemMessage: true);
							}
							else
							{
								receivedLobbyChat("", Translate.Instance.lookUpText(Enums.eTextSections.TEXT_SKIRMISH_MISC, 28), -1, systemMessage: true);
							}
						}
						if (MainViewModel.Instance.Show_MPSettings)
						{
							ImportSettings(MPsetupData.ToString());
						}
					}
					if (MPLocalReady && !MPLocalReadyLocked)
					{
						MPLocalReady = false;
						Platform_Multiplayer.Instance.SetMemberReadyState(MPLocalReady);
					}
				}
				if (MainViewModel.Instance.Show_MPLobbySettings)
				{
					if (selectedLobby != null)
					{
						if ((DateTime.UtcNow - lastSettingsRefresh).TotalSeconds > 1.0)
						{
							lastSettingsRefresh = DateTime.UtcNow;
							ImportSettings(selectedLobby.settings);
						}
					}
					else
					{
						MainViewModel.Instance.Show_MPLobbySettings = false;
					}
				}
				if (refreshTeams && MainViewModel.Instance.Show_SkirmishTeamsPanel)
				{
					PopulateTeamsPanel();
				}
				if (justEnteredSetup)
				{
					justEnteredSetup = false;
					if (((Selector)RefFileLists).SelectedItem != null)
					{
						((ListBox)RefFileLists).ScrollIntoView(((Selector)RefFileLists).SelectedItem);
					}
				}
			}
			if (currentLobby != null && !currentLobby.isHost && !MainViewModel.Instance.Show_SkirmishTeamsPanel)
			{
				EngineInterface.setMultiplayerStartingData(MPsetupData);
				GameData.Instance.setKeepOrder(MPsetupData.start_keep_location_order);
				if (!flag)
				{
					UpdateRadarShieldPositions();
				}
			}
			if (currentLobby != null && currentLobby.startGame != null && currentLobby.startGame.Length > 0)
			{
				if (!currentLobby.isHost || startGameTime < DateTime.UtcNow)
				{
					if (currentLobby.startGame.StartsWith("GO!"))
					{
						string[] array = currentLobby.startGame.Split("!", StringSplitOptions.None);
						if (array.Length != 2 || array[1] == currentLobby.AIVDataChecksum())
						{
							int coopTrailID = 0;
							if (coopGame)
							{
								if (FrontendMenus.CurrentSelectedTrail == 21)
								{
									coopTrailID = 1;
								}
								else if (FrontendMenus.CurrentSelectedTrail == 22)
								{
									coopTrailID = 2;
								}
								else if (FrontendMenus.CurrentSelectedTrail == 23)
								{
									coopTrailID = 3;
								}
							}
							Platform_Multiplayer.Instance.StartGame(MPsetupData, selectedMPHeader, coopTrailID, selectedCoopMissionID);
							MPGameLoading = true;
							MainViewModel.Instance.Show_MPJoiningLobby = false;
							MainViewModel.Instance.Show_MPGameCreation = false;
						}
					}
					else
					{
						FileHeader headerFromMpSaveFileName = MapFileManager.Instance.GetHeaderFromMpSaveFileName(currentLobby.startGame);
						if (headerFromMpSaveFileName != null)
						{
							Platform_Multiplayer.Instance.StartSave(MPsetupData, headerFromMpSaveFileName);
							MPGameLoading = true;
							MainViewModel.Instance.Show_MPJoiningLobby = false;
							MainViewModel.Instance.Show_MPGameCreation = false;
						}
						else
						{
							Debug.LogError((object)("Missing Save file : " + currentLobby.startGame));
						}
					}
				}
				Director.instance.StartMultiplayerGame();
			}
			if (!skirmishGame && currentLobby != null && currentLobby.isHost && DateTime.UtcNow > nextHostSendPings)
			{
				nextHostSendPings = DateTime.UtcNow.AddSeconds(2.0);
				Platform_Multiplayer.Instance.HostSendLobbyPings();
			}
		}
		if (delayedSendDataToLobby != DateTime.MinValue && DateTime.UtcNow > delayedSendDataToLobby)
		{
			UpdateHostInfo();
		}
		if (MainViewModel.Instance.Show_MPJoiningLobby && (DateTime.UtcNow - lastAutoRefreshTime).TotalSeconds > 30.0)
		{
			lastAutoRefreshTime = DateTime.UtcNow;
			Platform_Multiplayer.Instance.GetLobbies(matchmakingDefault, delegate
			{
				lobbies = Platform_Multiplayer.Instance.ReadLobbies();
				populateLobbyList();
			});
		}
		if (pendingMPHost)
		{
			pendingMPHost = false;
			if (!skirmishGame)
			{
				for (int num2 = 0; num2 < 8; num2++)
				{
					MPsetupData.start_keep_location_order[num2] = -10;
				}
			}
			if (headerlist != null && headerlist.Count > 0)
			{
				if (selectedMPHeader == null)
				{
					headerlist = MapFileManager.Instance.GetMultiplayerMaps(sortByColumn, sortByAscending, numConnectedPlayers, includeBuiltIn, includeUser, includeWorkshop);
					if (selectedMPHeader == null && includeBuiltIn)
					{
						foreach (FileHeader item in headerlist)
						{
							if (item.builtinMap && ((!skirmishGame && item.fileName.ToLowerInvariant() == "close encounters") || (skirmishGame && item.fileName.ToLowerInvariant() == "crater lake")))
							{
								selectedMPHeader = item;
								break;
							}
						}
					}
					if (selectedMPHeader == null)
					{
						selectedMPHeader = headerlist[0];
					}
					GameData.Instance.setKeepLocationsFromHeader(selectedMPHeader);
					update_keep_locations_on_map_change();
					if (skirmishGame)
					{
						UpdateRadarShieldPositions();
					}
					populateMapList(selectedMPHeader);
					updateRadarTexture(selectedMPHeader);
					GameData.Instance.SetMissionTextFromHeader(selectedMPHeader);
					PopulateMapDetailsPanel(selectedMPHeader);
					MainViewModel.Instance.Show_MPPeacetime = !skirmishGame;
				}
				if (!skirmishGame)
				{
					Platform_Multiplayer.Instance.CreateLobby(RefTextBoxGameName.Text, selectedMPHeader.display_filename, selectedMPHeader.fileName, selectedMPHeader.maxPlayers, customCoopGame ? 1 : 0, MPLobbyMode, MPsetupData.ToString(), (int)selectedMPHeader.crc, delegate
					{
						currentLobby = Platform_Multiplayer.Instance.GetActiveLobby();
						updateSteamIDMappings();
						updateSteamIDMappings();
						UpdateRadarShieldPositions();
						UpdateHostInfo();
						MPHostLobbyname = RefTextBoxGameName.Text;
						ShowSetupScreen();
					}, delegate(string name, string message, int colourID)
					{
						receivedLobbyChat(name, message, colourID);
					});
				}
			}
		}
		if (currentLobby != null)
		{
			List<Platform_Multiplayer.MPLobbyMember> members = currentLobby.members;
			MPTotalPlayers = members.Count;
			ReSortTeamInfo();
			int num3 = currentLobby.CountHumanPlayers();
			if (num3 != humanPlayerCount)
			{
				if (humanPlayerCount != -1)
				{
					if (num3 > humanPlayerCount)
					{
						SFXManager.instance.playSound(318);
					}
					else
					{
						SFXManager.instance.playSound(319);
					}
				}
				humanPlayerCount = num3;
			}
			int num4 = -1;
			bool flag3 = false;
			for (int num5 = members.Count; num5 < 8; num5++)
			{
				((Panel)(Grid)((FrameworkElement)playerRows[num5].RefRow).Parent).Background = (Brush)(object)lightBarColour;
			}
			for (int num6 = 0; num6 < members.Count; num6++)
			{
				int row = num6;
				int playerID = team_order[num6 + 1];
				Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID = currentLobby.GetLobbyMemberFromThis_PlayerID(playerID);
				if (lobbyMemberFromThis_PlayerID == null)
				{
					continue;
				}
				int thisPlayerFromSteamID = currentLobby.getThisPlayerFromSteamID(lobbyMemberFromThis_PlayerID.id.m_SteamID);
				int team = currentLobby.getTeam(lobbyMemberFromThis_PlayerID);
				if (team != num4)
				{
					flag3 = !flag3;
				}
				switch (lobbyMemberFromThis_PlayerID.teamShield)
				{
				case 1:
					((Panel)playerRows[num6].RefRow).Background = (Brush)(object)teamRedBarColour;
					break;
				case 2:
					((Panel)playerRows[num6].RefRow).Background = (Brush)(object)teamYellowBarColour;
					break;
				case 3:
					((Panel)playerRows[num6].RefRow).Background = (Brush)(object)teamBlueBarColour;
					break;
				case 4:
					((Panel)playerRows[num6].RefRow).Background = (Brush)(object)teamGreenBarColour;
					break;
				default:
					if (flag3)
					{
						((Panel)playerRows[num6].RefRow).Background = (Brush)(object)lightBarColour;
					}
					else
					{
						((Panel)playerRows[num6].RefRow).Background = (Brush)(object)darkBarColour;
					}
					break;
				}
				((Panel)(Grid)((FrameworkElement)playerRows[num6].RefRow).Parent).Background = (Brush)(object)transparentColour;
				num4 = team;
				playerRows[num6].Update(this, lobbyMemberFromThis_PlayerID, row, thisPlayerFromSteamID);
				if (coopGame && MainViewModel.Instance.Show_CoopTrail1)
				{
					FRONT_CoopTrail1.Instance.playerRows[num6].Update(this, lobbyMemberFromThis_PlayerID, row, thisPlayerFromSteamID);
				}
				if (coopGame && MainViewModel.Instance.Show_CoopTrail2)
				{
					FRONT_CoopTrail2.Instance.playerRows[num6].Update(this, lobbyMemberFromThis_PlayerID, row, thisPlayerFromSteamID);
				}
				if (coopGame && MainViewModel.Instance.Show_CoopTrail3)
				{
					FRONT_CoopTrail3.Instance.playerRows[num6].Update(this, lobbyMemberFromThis_PlayerID, row, thisPlayerFromSteamID);
				}
				if ((skirmishGame && num6 == 0) || (!skirmishGame && lobbyMemberFromThis_PlayerID.IsSelf()))
				{
					UpdateColourShields(GetPlayerColour());
				}
				if (currentLobby.isHost && lobbyMemberFromThis_PlayerID != null && lobbyMemberFromThis_PlayerID.mapRequested != null && lobbyMemberFromThis_PlayerID.mapRequested.Length > 0 && lobbyMemberFromThis_PlayerID.mapRequested.ToLower() == currentLobby.mapFileName.ToLower() && Platform_Multiplayer.Instance.SendMap(lobbyMemberFromThis_PlayerID, currentLobby.mapFileName, selectedMPHeader.filePath, delegate
				{
					receivedLobbyChat("", Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 295), -1, systemMessage: true);
				}))
				{
					addSystemLobbyChat(lobbyMemberFromThis_PlayerID.Name, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 139) + " ->");
				}
			}
			Platform_Multiplayer.Instance.ProcessMapSendQueue();
			for (int num7 = members.Count; num7 < 8; num7++)
			{
				playerRows[num7].Update(this, null, num7, -1);
				if (coopGame && MainViewModel.Instance.Show_CoopTrail1)
				{
					FRONT_CoopTrail1.Instance.playerRows[num7].Update(this, null, num7, -1);
				}
				if (coopGame && MainViewModel.Instance.Show_CoopTrail2)
				{
					FRONT_CoopTrail2.Instance.playerRows[num7].Update(this, null, num7, -1);
				}
				if (coopGame && MainViewModel.Instance.Show_CoopTrail3)
				{
					FRONT_CoopTrail3.Instance.playerRows[num7].Update(this, null, num7, -1);
				}
			}
			if (MPLocalReady)
			{
				if (readyAnimPlaying)
				{
					readyAnimPlaying = false;
					pulseAnimation.Stop();
				}
				SetReadyStateImage(RefReadyButton, MainViewModel.Instance.GameSprites[105], MainViewModel.Instance.GameSprites[106]);
				SetReadyStateImage(FRONT_CoopTrail1.Instance.RefReadyButton, MainViewModel.Instance.GameSprites[105], MainViewModel.Instance.GameSprites[106]);
				SetReadyStateImage(FRONT_CoopTrail2.Instance.RefReadyButton, MainViewModel.Instance.GameSprites[105], MainViewModel.Instance.GameSprites[106]);
				SetReadyStateImage(FRONT_CoopTrail3.Instance.RefReadyButton, MainViewModel.Instance.GameSprites[105], MainViewModel.Instance.GameSprites[106]);
				((UIElement)RefReadyButtonLock).Visibility = (Visibility)2;
				((UIElement)FRONT_CoopTrail1.Instance.RefReadyButtonLock).Visibility = (Visibility)2;
				((UIElement)FRONT_CoopTrail2.Instance.RefReadyButtonLock).Visibility = (Visibility)2;
				((UIElement)FRONT_CoopTrail3.Instance.RefReadyButtonLock).Visibility = (Visibility)2;
				if (MPLocalReadyLocked)
				{
					SetReadyStateImage(RefReadyButtonLock, MainViewModel.Instance.GameSprites[688], MainViewModel.Instance.GameSprites[689]);
					SetReadyStateImage(FRONT_CoopTrail1.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[688], MainViewModel.Instance.GameSprites[689]);
					SetReadyStateImage(FRONT_CoopTrail2.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[688], MainViewModel.Instance.GameSprites[689]);
					SetReadyStateImage(FRONT_CoopTrail3.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[688], MainViewModel.Instance.GameSprites[689]);
				}
				else
				{
					SetReadyStateImage(RefReadyButtonLock, MainViewModel.Instance.GameSprites[690], MainViewModel.Instance.GameSprites[691]);
					SetReadyStateImage(FRONT_CoopTrail1.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[690], MainViewModel.Instance.GameSprites[691]);
					SetReadyStateImage(FRONT_CoopTrail2.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[690], MainViewModel.Instance.GameSprites[691]);
					SetReadyStateImage(FRONT_CoopTrail3.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[690], MainViewModel.Instance.GameSprites[691]);
				}
			}
			else
			{
				if (!readyAnimPlaying)
				{
					readyAnimPlaying = true;
					pulseAnimation.Begin();
				}
				SetReadyStateImage(RefReadyButton, MainViewModel.Instance.GameSprites[103], MainViewModel.Instance.GameSprites[104]);
				SetReadyStateImage(FRONT_CoopTrail1.Instance.RefReadyButton, MainViewModel.Instance.GameSprites[103], MainViewModel.Instance.GameSprites[104]);
				SetReadyStateImage(FRONT_CoopTrail2.Instance.RefReadyButton, MainViewModel.Instance.GameSprites[103], MainViewModel.Instance.GameSprites[104]);
				SetReadyStateImage(FRONT_CoopTrail3.Instance.RefReadyButton, MainViewModel.Instance.GameSprites[103], MainViewModel.Instance.GameSprites[104]);
				((UIElement)RefReadyButtonLock).Visibility = (Visibility)1;
				((UIElement)FRONT_CoopTrail1.Instance.RefReadyButtonLock).Visibility = (Visibility)1;
				((UIElement)FRONT_CoopTrail2.Instance.RefReadyButtonLock).Visibility = (Visibility)1;
				((UIElement)FRONT_CoopTrail3.Instance.RefReadyButtonLock).Visibility = (Visibility)1;
			}
			if (currentLobby != null && !currentLobby.isHost)
			{
				DependencyObject listBox = MainViewModel.GetListBox((DependencyObject)(object)RefFileLists);
				((UIElement)((listBox is ListBox) ? listBox : null)).IsHitTestVisible = false;
				if (multiplayerMapRequestTime != DateTime.MinValue && multiplayerMapRequestTime < DateTime.UtcNow)
				{
					multiplayerMapRequestTime = DateTime.MinValue;
					MPMapChecked = false;
				}
				if (!MPMapChecked || MPLastMapName != currentLobby.mapFileName)
				{
					if (!MPLocalReadyLocked)
					{
						MPLocalReady = false;
						Platform_Multiplayer.Instance.SetMemberReadyState(state: false);
					}
					if (!Platform_Multiplayer.Instance.MapRetrievalInProgress())
					{
						MPMapChecked = true;
						MPLastMapName = currentLobby.mapFileName;
						int intFromString = EditorDirector.getIntFromString(currentLobby.crc);
						FileHeader headerFromFileNameMP = MapFileManager.Instance.GetHeaderFromFileNameMP(MPLastMapName, intFromString);
						if (headerFromFileNameMP == null)
						{
							Platform_Multiplayer.Instance.SetMapStatus(1);
							MPMapValid = false;
							regetMapListNextTime = true;
							selectedMPHeader = null;
							MainViewModel.Instance.MP_RetrieveMapName = MPLastMapName;
							MainViewModel.Instance.Show_MPFileList = false;
							MainViewModel.Instance.Show_MPRadar = false;
							MainViewModel.Instance.Show_MPRetrieveMapPanel = true;
							MainViewModel.Instance.MapRetrieveProgress = "0";
							MPLocalReady = false;
							Platform_Multiplayer.Instance.SetMemberReadyState(state: false);
						}
						else if (headerFromFileNameMP.crc != intFromString)
						{
							Platform_Multiplayer.Instance.SetMapStatus(2);
							MPMapValid = false;
							regetMapListNextTime = true;
							selectedMPHeader = null;
							MainViewModel.Instance.MP_RetrieveMapName = MPLastMapName;
							MainViewModel.Instance.Show_MPFileList = false;
							MainViewModel.Instance.Show_MPRadar = false;
							MainViewModel.Instance.Show_MPRetrieveMapPanel = true;
							MainViewModel.Instance.MapRetrieveProgress = "0";
							MPLocalReady = false;
							Platform_Multiplayer.Instance.SetMemberReadyState(state: false);
						}
						else
						{
							Platform_Multiplayer.Instance.SetMapStatus(0);
							MPMapValid = true;
							MainViewModel.Instance.Show_MPFileList = true;
							MainViewModel.Instance.Show_MPRetrieveMapPanel = false;
							if (regetMapListNextTime)
							{
								headerlist = MapFileManager.Instance.GetMultiplayerMaps(sortByColumn, sortByAscending, numConnectedPlayers, includeBuiltIn, includeUser, includeWorkshop);
								populateMapList(selectedMPHeader);
							}
							regetMapListNextTime = false;
							selectedMPHeader = headerFromFileNameMP;
							foreach (FileRow item2 in ((ItemsControl)RefFileLists).ItemsSource)
							{
								if (item2.fileHeader == selectedMPHeader)
								{
									((Selector)RefFileLists).SelectedItem = item2;
									((ListBox)RefFileLists).ScrollIntoView(((Selector)RefFileLists).SelectedItem);
									break;
								}
							}
							GameData.Instance.setKeepLocationsFromHeader(selectedMPHeader);
							update_keep_locations_on_map_change();
							UpdateRadarShieldPositions();
							updateRadarTexture(selectedMPHeader);
							GameData.Instance.SetMissionTextFromHeader(selectedMPHeader);
							PopulateMapDetailsPanel(selectedMPHeader);
							MainViewModel.Instance.Show_MPPeacetime = !skirmishGame;
						}
					}
				}
			}
			else
			{
				DependencyObject listBox2 = MainViewModel.GetListBox((DependencyObject)(object)RefFileLists);
				((UIElement)((listBox2 is ListBox) ? listBox2 : null)).IsHitTestVisible = true;
				MPMapValid = true;
			}
			if (ShowSharingCode)
			{
				MainViewModel.Instance.MultiplayerShareCode = Platform_Multiplayer.Instance.ShareCodeString;
			}
			else
			{
				MainViewModel.Instance.MultiplayerShareCode = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 179);
			}
			if (MainViewModel.Instance.Show_MPRetrieveMapPanel)
			{
				if (multiplayerMapRequestTime == DateTime.MinValue)
				{
					MainViewModel.Instance.Show_MPRetrieveMapButton = true;
					MainViewModel.Instance.Show_MPRetrievingMapMessage = false;
				}
				else
				{
					MainViewModel.Instance.Show_MPRetrieveMapButton = false;
					MainViewModel.Instance.Show_MPRetrievingMapMessage = true;
				}
			}
			bool flag4 = false;
			bool flag5 = false;
			bool flag6 = false;
			if (coopGame && singlePlayerCoop)
			{
				flag4 = true;
			}
			else if (!skirmishGame)
			{
				int num8 = currentLobby.CountAIPlayers();
				flag4 = currentLobby.numLobbyMembers > 1 && (currentLobby.numLobbyMembers <= currentLobby.iMaxPlayers || (customCoopGame && currentLobby.numLobbyMembers <= selectedMPHeader.maxPlayers)) && MPLocalReady;
				if (flag4)
				{
					if (num8 > 0)
					{
						flag6 = true;
					}
					if (currentLobby.numLobbyMembers - 1 == num8)
					{
						flag4 = false;
					}
				}
				((UIElement)RefMultiplayerInvite).IsEnabled = currentLobby.numLobbyMembers < currentLobby.iMaxPlayers;
			}
			else
			{
				flag4 = currentLobby.numLobbyMembers > 1 && currentLobby.numLobbyMembers <= currentLobby.iMaxPlayers;
			}
			if (flag4)
			{
				if (!skirmishGame)
				{
					foreach (Platform_Multiplayer.MPLobbyMember member in currentLobby.members)
					{
						if (!member.ready && !member.SkirmishMember)
						{
							flag4 = false;
							break;
						}
					}
				}
				if (flag4)
				{
					flag5 = true;
					if (!currentLobby.getEnoughTeams())
					{
						flag4 = false;
					}
				}
			}
			lastCanStart = flag4;
			if (flag4)
			{
				int num9 = 1;
				foreach (Platform_Multiplayer.MPLobbyMember member2 in currentLobby.members)
				{
					num9 = currentLobby.getThisPlayerFromSteamID(member2.id.m_SteamID);
					if (!member2.SkirmishHumanMember && (!coopGame || num9 != 1) && !coopGame && !AIVs[num9 - 1].builtIn && AIVs[num9 - 1].aivs.Count == 0)
					{
						flag4 = false;
					}
				}
			}
			((UIElement)RefMultiplayerPlayButton).IsEnabled = flag4;
			((UIElement)RefTrailMakerTest).IsEnabled = flag4;
			((UIElement)FRONT_CoopTrail1.Instance.RefMultiplayerPlayButton).IsEnabled = flag4;
			((UIElement)FRONT_CoopTrail2.Instance.RefMultiplayerPlayButton).IsEnabled = flag4;
			((UIElement)FRONT_CoopTrail3.Instance.RefMultiplayerPlayButton).IsEnabled = flag4;
			((UIElement)RefLoadButton).IsEnabled = (flag5 && !flag6) | skirmishGame;
			((UIElement)FRONT_CoopTrail1.Instance.RefLoadButton).IsEnabled = flag4 | singlePlayerCoop | MainViewModel.Instance.Show_CoopHostInvitePane;
			((UIElement)FRONT_CoopTrail2.Instance.RefLoadButton).IsEnabled = flag4 | singlePlayerCoop | MainViewModel.Instance.Show_CoopHostInvitePane;
			((UIElement)FRONT_CoopTrail3.Instance.RefLoadButton).IsEnabled = flag4 | singlePlayerCoop | MainViewModel.Instance.Show_CoopHostInvitePane;
		}
		if ((DateTime.UtcNow - lastScrollTest).TotalMilliseconds > 150.0 && (MainViewModel.Instance.Show_MPJoiningLobby || (currentLobby != null && currentLobby.isHost)))
		{
			if (KeyManager.instance.CursorUpHeld)
			{
				lastScrollTest = DateTime.UtcNow;
				ListView val = ((!MainViewModel.Instance.Show_MPJoiningLobby) ? RefFileLists : RefLobbyLists);
				DependencyObject scrollViewer = MainViewModel.GetScrollViewer((DependencyObject)(object)val);
				ScrollViewer val2 = (ScrollViewer)(object)((scrollViewer is ScrollViewer) ? scrollViewer : null);
				if ((BaseComponent)(object)val2 != (BaseComponent)null)
				{
					if (((Selector)val).SelectedItem == null)
					{
						val2.ScrollToVerticalOffset(val2.VerticalOffset - 30f);
					}
					else
					{
						if (((Selector)val).SelectedIndex > 0)
						{
							int selectedIndex = ((Selector)val).SelectedIndex;
							((Selector)val).SelectedIndex = selectedIndex - 1;
						}
						((ListBox)val).ScrollIntoView(((Selector)val).SelectedItem);
					}
				}
			}
			else if (KeyManager.instance.CursorDownHeld)
			{
				lastScrollTest = DateTime.UtcNow;
				ListView val3 = ((!MainViewModel.Instance.Show_MPJoiningLobby) ? RefFileLists : RefLobbyLists);
				DependencyObject scrollViewer2 = MainViewModel.GetScrollViewer((DependencyObject)(object)val3);
				ScrollViewer val4 = (ScrollViewer)(object)((scrollViewer2 is ScrollViewer) ? scrollViewer2 : null);
				if ((BaseComponent)(object)val4 != (BaseComponent)null)
				{
					if (((Selector)val3).SelectedItem == null)
					{
						val4.ScrollToVerticalOffset(val4.VerticalOffset + 30f);
					}
					else
					{
						if (((Selector)val3).SelectedIndex < ((ItemsControl)RefFileLists).Items.Count - 1)
						{
							int selectedIndex = ((Selector)val3).SelectedIndex;
							((Selector)val3).SelectedIndex = selectedIndex + 1;
						}
						((ListBox)val3).ScrollIntoView(((Selector)val3).SelectedItem);
					}
				}
			}
		}
		if (SelectedRadarKeep >= 0)
		{
			Point position = Mouse.GetPosition((UIElement)(object)RefBasemap);
			Thickness margin = default(Thickness);
			((Thickness)(ref margin))._002Ector(((Point)(ref position)).X, ((Point)(ref position)).Y, -100f, -100f);
			((FrameworkElement)RefFloatingRadarShield).Margin = margin;
			if (Input.GetMouseButtonDown(1))
			{
				SelectedRadarKeep = -1;
				MainViewModel.Instance.Show_SkirmishUIOnRadar = false;
				UpdateRadarShieldPositions();
			}
		}
		if (SelectedFace >= 0)
		{
			Point position2 = Mouse.GetPosition((UIElement)(object)RefBasemap);
			Thickness margin2 = default(Thickness);
			((Thickness)(ref margin2))._002Ector(((Point)(ref position2)).X, ((Point)(ref position2)).Y, -100f, -100f);
			((FrameworkElement)RefFloatingTeams).Margin = margin2;
			if (Input.GetMouseButtonDown(1))
			{
				SelectedFace = -1;
				((UIElement)RefTeamFaceCancel).IsEnabled = false;
				PopulateTeamsPanel();
			}
		}
		if (showLobbyUnavailableMessage)
		{
			showLobbyUnavailableMessage = false;
			HUD_ConfirmationPopup.ShowOK(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 194), delegate
			{
			}, MPConf: true);
		}
		refreshLobbyChat(fromReceive: false);
	}

	public void SetReadyStateImage(Button readyButton, ImageSource image, ImageSource overImage)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		if ((BaseComponent)(ImageSource)PropEx.GetSprite1((UIElement)(object)readyButton) != (BaseComponent)(object)image)
		{
			PropEx.SetSprite1((UIElement)(object)readyButton, image);
			PropEx.SetSprite2((UIElement)(object)readyButton, overImage);
			PropEx.SetSprite3((UIElement)(object)readyButton, overImage);
		}
	}

	public void Update_Coop()
	{
		if (currentLobby != null)
		{
			if (MainViewModel.Instance.Show_CoopWaiting)
			{
				if (currentLobby.CountHumanPlayers() != 2 || (!MainViewModel.Instance.Show_CoopHostInvitePane && currentLobby.coopSelectedMission <= 0))
				{
					return;
				}
				MainViewModel.Instance.Show_CoopWaiting = false;
			}
			if (MainViewModel.Instance.Show_CoopHostInvitePane)
			{
				if (currentLobby.CountHumanPlayers() == 2 || singlePlayerCoop)
				{
					MainViewModel.Instance.Show_CoopHostInvitePane = false;
					MainViewModel.Instance.Show_CoopHostJoinedPane = true;
					MainViewModel.Instance.Show_MPSharing = false;
					MainViewModel.Instance.Show_CoopMapIcons = true;
					if (!singlePlayerCoop)
					{
						ulong coopPartnerID = Platform_Multiplayer.Instance.GetCoopPartnerID();
						if (coopPartnerID != 0L)
						{
							ConfigSettings.InitCoopGame(coopPartnerID, Platform_Multiplayer.Instance.getSteamUserName(coopPartnerID), Platform_Multiplayer.Instance.LastCoAString);
							ConfigSettings.CalcCoopProgress(0uL);
							ConfigSettings.CalcCoopProgress(coopPartnerID);
						}
					}
					else
					{
						ConfigSettings.InitCoopGame(userName: (singlePlayerCoopAlly >= 25) ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 453 + 17 * ((int)singlePlayerCoopAlly - 25)) : ((singlePlayerCoopAlly < 16) ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 239 + 9 * (int)singlePlayerCoopAlly) : Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 88 + 9 * ((int)singlePlayerCoopAlly - 16))), steamID: singlePlayerCoopAlly + 1000);
						ConfigSettings.CalcCoopProgress(0uL);
						ConfigSettings.CalcCoopProgress(singlePlayerCoopAlly + 1000);
					}
					coopOrderSwapped = false;
					if (FrontendMenus.CurrentSelectedTrail == 21)
					{
						Platform_Multiplayer.Instance.SetCoopTrailProgress(0, ConfigSettings.Settings_Progress_Trail_Coop1_Status, FrontendMenus.CurrentSelectedTrailCoop1Mission, ConfigSettings.Settings_Progress_Trail_Coop1, coopOrderSwapped);
						MainViewModel.Instance.FrontEndMenu.GenerateSwords();
						MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(ConfigSettings.Settings_Progress_Trail_CoopNext1 + 1);
						CoopMissionChanged(0, FrontendMenus.CurrentSelectedTrailCoop1Mission);
					}
					else if (FrontendMenus.CurrentSelectedTrail == 22)
					{
						Platform_Multiplayer.Instance.SetCoopTrailProgress(1, ConfigSettings.Settings_Progress_Trail_Coop2_Status, FrontendMenus.CurrentSelectedTrailCoop2Mission, ConfigSettings.Settings_Progress_Trail_Coop2, coopOrderSwapped);
						MainViewModel.Instance.FrontEndMenu.GenerateSwords();
						MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(ConfigSettings.Settings_Progress_Trail_CoopNext2 + 1);
						CoopMissionChanged(1, FrontendMenus.CurrentSelectedTrailCoop2Mission);
					}
					else if (FrontendMenus.CurrentSelectedTrail == 23)
					{
						Platform_Multiplayer.Instance.SetCoopTrailProgress(2, ConfigSettings.Settings_Progress_Trail_Coop3_Status, FrontendMenus.CurrentSelectedTrailCoop3Mission, ConfigSettings.Settings_Progress_Trail_Coop3, coopOrderSwapped);
						MainViewModel.Instance.FrontEndMenu.GenerateSwords();
						MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(ConfigSettings.Settings_Progress_Trail_CoopNext3 + 1);
						CoopMissionChanged(2, FrontendMenus.CurrentSelectedTrailCoop3Mission);
					}
				}
				if (avatarCallbacks.Count > 0)
				{
					AvatarCallback avatarCallback = avatarCallbacks.Peek();
					ImageSource userAvatar = Platform_Multiplayer.Instance.GetUserAvatar(avatarCallback.steamID);
					if ((BaseComponent)(object)userAvatar != (BaseComponent)null)
					{
						avatarCallbacks.Dequeue();
						SetCoopRowAvatar(avatarCallback.row, userAvatar);
					}
				}
			}
			if (MainViewModel.Instance.Show_CoopHostJoinedPane)
			{
				if (!singlePlayerCoop && currentLobby.CountHumanPlayers() < 2)
				{
					MainViewModel.Instance.Show_CoopHostInvitePane = true;
					MainViewModel.Instance.Show_CoopHostJoinedPane = false;
					MainViewModel.Instance.Show_CoopOptions = false;
					ClearCoopAIs();
				}
				else
				{
					updateSteamIDMappings();
					if (currentLobby.coopTrailID == 0)
					{
						FRONT_CoopTrail1.Instance.UpdateRadarShieldPositions();
					}
					else if (currentLobby.coopTrailID == 1)
					{
						FRONT_CoopTrail2.Instance.UpdateRadarShieldPositions();
					}
					else if (currentLobby.coopTrailID == 2)
					{
						FRONT_CoopTrail3.Instance.UpdateRadarShieldPositions();
					}
				}
			}
			if (!MainViewModel.Instance.Show_CoopClientPane)
			{
				return;
			}
			ulong coopPartnerID2 = Platform_Multiplayer.Instance.GetCoopPartnerID();
			if (coopPartnerID2 != 0L && ConfigSettings.getCoopInfo(coopPartnerID2, currentLobby.coopTrailID) == null)
			{
				ConfigSettings.InitCoopGame(coopPartnerID2, Platform_Multiplayer.Instance.getSteamUserName(coopPartnerID2), Platform_Multiplayer.Instance.LastCoAString);
			}
			if (currentLobby.coopTrailProgress != null && (coopGame_ClientSelectedMission != currentLobby.coopSelectedMission || coopOrderSwapped != currentLobby.coopOrderSwapped))
			{
				if (currentLobby.coopTrailID == 0)
				{
					for (int i = 0; i < 10; i++)
					{
						ConfigSettings.Settings_Progress_Trail_Coop1_Status[i] = currentLobby.coopTrailProgress[i];
					}
					ConfigSettings.Settings_Progress_Trail_Coop1 = currentLobby.coopTrailFullProgress;
					FrontendMenus.CurrentSelectedTrailCoop1Mission = currentLobby.coopSelectedMission;
				}
				else if (currentLobby.coopTrailID == 1)
				{
					for (int j = 0; j < 10; j++)
					{
						ConfigSettings.Settings_Progress_Trail_Coop2_Status[j] = currentLobby.coopTrailProgress[j];
					}
					ConfigSettings.Settings_Progress_Trail_Coop2 = currentLobby.coopTrailFullProgress;
					FrontendMenus.CurrentSelectedTrailCoop2Mission = currentLobby.coopSelectedMission;
				}
				else if (currentLobby.coopTrailID == 2)
				{
					for (int k = 0; k < 10; k++)
					{
						ConfigSettings.Settings_Progress_Trail_Coop3_Status[k] = currentLobby.coopTrailProgress[k];
					}
					ConfigSettings.Settings_Progress_Trail_Coop3 = currentLobby.coopTrailFullProgress;
					FrontendMenus.CurrentSelectedTrailCoop3Mission = currentLobby.coopSelectedMission;
				}
				if (currentLobby.clientFound)
				{
					MainViewModel.Instance.FrontEndMenu.GenerateSwords();
					MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(currentLobby.coopSelectedMission);
					coopGame_ClientSelectedMission = currentLobby.coopSelectedMission;
					coopOrderSwapped = currentLobby.coopOrderSwapped;
					CoopMissionChanged(currentLobby.coopTrailID, currentLobby.coopSelectedMission);
				}
			}
			updateSteamIDMappings();
			if (currentLobby.coopTrailID == 0)
			{
				FRONT_CoopTrail1.Instance.UpdateRadarShieldPositions();
			}
			else if (currentLobby.coopTrailID == 1)
			{
				FRONT_CoopTrail2.Instance.UpdateRadarShieldPositions();
			}
			else if (currentLobby.coopTrailID == 2)
			{
				FRONT_CoopTrail3.Instance.UpdateRadarShieldPositions();
			}
			if (currentLobby.CountHumanPlayers() < 2 || currentLobby.members.Count == 0)
			{
				LeaveLobby(doLeaveOnSteam: true, refreshLobbyList: false);
				MainViewModel.Instance.FrontEndMenu.ButtonClicked("Skirmish");
			}
		}
		else if (MainViewModel.Instance.Show_CoopClientPane)
		{
			LeaveLobby(doLeaveOnSteam: true, refreshLobbyList: false);
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("Skirmish");
		}
	}

	public void ResumeCoop()
	{
		pendingCoopWaitingPanel = true;
		Platform_Multiplayer.Instance.ResumeCoop();
	}

	public void ShowLobbyScreen()
	{
		MainViewModel.Instance.Show_MPJoiningLobby = true;
		MainViewModel.Instance.Show_MPGameCreation = false;
		MainViewModel.Instance.Show_MPSettings = false;
		MainViewModel.Instance.Show_MPSteamIdentity = false;
		MainViewModel.Instance.Show_SkirmishTeams = true;
		customCoopGame = false;
		((UIElement)RefMultiplayerPlayButton).Visibility = (Visibility)1;
		((UIElement)RefLoadButton).Visibility = (Visibility)1;
		selectedMPHeader = null;
		MainViewModel.Instance.Show_MPIsHost = false;
		MainViewModel.Instance.StandaloneTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 51);
		lobbyChat.Clear();
		((BaseUICollection)RefMP_ChatDisplay.Inlines).Clear();
		((BaseUICollection)FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines).Clear();
		((BaseUICollection)FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines).Clear();
		((BaseUICollection)FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines).Clear();
	}

	public void ShowSetupScreen()
	{
		if (currentLobby == null && !skirmishGame)
		{
			return;
		}
		if (!skirmishGame)
		{
			MainViewModel.Instance.Show_MPSteamIdentity = true;
			MainViewModel.Instance.Show_MPIsHost = currentLobby.isHost;
		}
		else
		{
			MainViewModel.Instance.Show_MPIsHost = true;
		}
		MainViewModel.Instance.Show_MPFileList = true;
		MainViewModel.Instance.Show_MPJoiningLobby = false;
		MainViewModel.Instance.Show_MPGameCreation = true;
		MainViewModel.Instance.Show_MPSettings = false;
		MainViewModel.Instance.Show_MPRetrieveMapPanel = false;
		RefMP_ChatInput.Text = "";
		FRONT_CoopTrail1.Instance.RefMP_ChatInput.Text = "";
		FRONT_CoopTrail2.Instance.RefMP_ChatInput.Text = "";
		FRONT_CoopTrail3.Instance.RefMP_ChatInput.Text = "";
		MainViewModel.Instance.MP_LobbyChatWindow = "";
		MainViewModel.Instance.MP_Settings_Button = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 58);
		MainViewModel.Instance.Show_AddAIPanel = false;
		MainViewModel.Instance.Show_SkirmishRandomAIPanel = false;
		MainViewModel.Instance.Show_SkirmishTeamsPanel = false;
		MainViewModel.Instance.Show_AdvancedRandom = false;
		UpdateLobbyChangeButtons();
		includeUser = true;
		includeBuiltIn = true;
		includeWorkshop = true;
		((ToggleButton)RefIncludeBuiltin).IsChecked = true;
		((ToggleButton)RefIncludeUser).IsChecked = true;
		((ToggleButton)RefIncludeWorkshop).IsChecked = true;
		justEnteredSetupScreen = DateTime.UtcNow.AddSeconds(5.0);
		justEnteredSetup = true;
		if (skirmishGame || currentLobby.isHost)
		{
			((UIElement)RefMultiplayerPlayButton).Visibility = (Visibility)2;
			((UIElement)RefMultiplayerPlayButton).IsEnabled = false;
			if (!skirmishGame)
			{
				((UIElement)RefLoadButton).Visibility = (Visibility)2;
				((UIElement)RefLoadButton).IsEnabled = false;
			}
		}
		else
		{
			((UIElement)RefMultiplayerPlayButton).Visibility = (Visibility)1;
			((UIElement)RefLoadButton).Visibility = (Visibility)1;
		}
		if (!skirmishGame && currentLobby.gameTypeCoop == "1")
		{
			MainViewModel.Instance.StandaloneTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 432);
			customCoopGame = true;
			MainViewModel.Instance.Show_SkirmishTeams = false;
		}
		else if (trailMakerMode)
		{
			((FrameworkElement)RefHeaderBar).Width = 845f;
			MainViewModel.Instance.StandaloneTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 31);
		}
		else if (!skirmishGame || currentLobby.numLobbyMembers != currentLobby.CountAIPlayers())
		{
			MainViewModel.Instance.StandaloneTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 56);
		}
		else
		{
			MainViewModel.Instance.StandaloneTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 526);
			spectatorMode = true;
		}
	}

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/FRONT_Multiplayer.xaml");
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

	public void SkirmishAIAddClick(string param)
	{
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		int num = int.Parse(param);
		switch (num)
		{
		case 98:
		{
			customLordRows.Clear();
			List<CustomisationFileManager.CustomLord> customLords = CustomisationFileManager.Instance.GetCustomLords();
			foreach (CustomisationFileManager.CustomLord item in customLords)
			{
				FileRow fileRow = new FileRow();
				fileRow.Text1 = item.lordDisplayName;
				fileRow.lord = item;
				if (item.workshop)
				{
					fileRow.TypeImage = MainViewModel.Instance.GameSprites[89];
				}
				customLordRows.Add(fileRow);
			}
			((ItemsControl)RefCustomLordList).ItemsSource = customLordRows;
			if (customLords.Count > 0)
			{
				((Selector)RefCustomLordList).SelectedIndex = 0;
			}
			MainViewModel.Instance.Show_AddAIPanel_Normal = false;
			break;
		}
		case 0:
		case 1:
		case 2:
		case 3:
		case 4:
		case 5:
		case 6:
		case 7:
		case 8:
		case 9:
		case 10:
		case 11:
		case 12:
		case 13:
		case 14:
		case 15:
		case 16:
		case 17:
		case 18:
		case 19:
		case 20:
		case 21:
		case 22:
		case 23:
		case 24:
		case 25:
		case 26:
		case 99:
			if ((num == 20 || num == 21) && !FrontendMenus.DLC1Owned)
			{
				SteamFriends.ActivateGameOverlayToStore(new AppId_t(3030340u), (EOverlayToStoreFlag)0);
			}
			else if ((num == 22 || num == 23) && !FrontendMenus.DLC2Owned)
			{
				SteamFriends.ActivateGameOverlayToStore(new AppId_t(3030350u), (EOverlayToStoreFlag)0);
			}
			else if ((num == 25 || num == 26) && !FrontendMenus.DLC3Owned)
			{
				SteamFriends.ActivateGameOverlayToStore(new AppId_t(4483540u), (EOverlayToStoreFlag)0);
			}
			else if (coopGame)
			{
				singlePlayerCoop = true;
				singlePlayerCoopAlly = (ulong)num;
				MainViewModel.Instance.Show_CoopAIAllyPanel = false;
				MainViewModel.Instance.Show_CoopMapIcons = true;
				MainViewModel.Instance.Show_CoopHostJoinedPane = true;
				Platform_Multiplayer.Instance.AddSkirmishPlayerLocal(num);
			}
			else
			{
				if (currentLobby == null || !currentLobby.isHost)
				{
					break;
				}
				int count = currentLobby.members.Count;
				int num4 = PlayerCap;
				if (customCoopGame)
				{
					num4 = selectedMPHeader.maxPlayers;
				}
				if (count >= num4 || (count >= currentLobby.iMaxPlayers && !customCoopGame))
				{
					break;
				}
				if (!skirmishGame)
				{
					if (customCoopGame)
					{
						int maxPlayers = selectedMPHeader.maxPlayers;
						if (count >= maxPlayers || (count == maxPlayers - 1 && currentLobby.CountHumanPlayers() == 1))
						{
							break;
						}
					}
					else if ((count == PlayerCap - 1 || count == currentLobby.iMaxPlayers - 1) && currentLobby.CountAIPlayers() == count - 1)
					{
						break;
					}
				}
				if (num == 99)
				{
					Random random2 = new Random();
					int num5 = 0;
					bool flag2 = false;
					while (!flag2)
					{
						num5 = random2.Next(29);
						switch (num5)
						{
						case 20:
						case 21:
							if (!FrontendMenus.DLC1Owned)
							{
								continue;
							}
							break;
						case 22:
						case 23:
							if (!FrontendMenus.DLC2Owned)
							{
								continue;
							}
							break;
						case 25:
						case 26:
							if (!FrontendMenus.DLC3Owned)
							{
								continue;
							}
							break;
						case 27:
						case 28:
							if (!FrontendMenus.DLC4Owned)
							{
								continue;
							}
							break;
						}
						break;
					}
					num = num5;
				}
				if (!MyAudioManager.Instance.isSpeechPlaying(3))
				{
					SFXManager.instance.playGenieSpeech(3, AddPlayerSpeech[num + 1], 1f);
				}
				int forcedTeam = -1;
				if (customCoopGame)
				{
					forcedTeam = currentLobby.findCustomCoopEnemyTeam();
				}
				Platform_Multiplayer.MPLobbyMember mPLobbyMember2 = Platform_Multiplayer.Instance.AddSkirmishPlayerLocal(num, forcedTeam);
				updateSteamIDMappings();
				ReSortTeamInfo();
				UpdateHostInfo();
				CreateTeamShields();
				UpdateRadarShieldPositions();
				if (mPLobbyMember2 != null)
				{
					int thisPlayerFromSteamID2 = currentLobby.getThisPlayerFromSteamID(mPLobbyMember2.id.m_SteamID);
					AIVs[thisPlayerFromSteamID2 - 1].Init(num, "");
				}
			}
			break;
		case -8:
		case -7:
		case -6:
		case -5:
		case -4:
		case -3:
		case -2:
		case -1:
		{
			playKickSpeech = false;
			for (int i = 2; i <= 8; i++)
			{
				Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID = currentLobby.GetLobbyMemberFromThis_PlayerID(i);
				if (lobbyMemberFromThis_PlayerID != null)
				{
					Platform_Multiplayer.Instance.kickSkirmishPlayer(lobbyMemberFromThis_PlayerID.id.m_SteamID);
					currentLobby.validateTeams();
					updateSteamIDMappings();
					ReSortTeamInfo();
					UpdateHostInfo();
					UpdateRadarShieldPositions();
					UpdateRandomAIButtons();
				}
			}
			playKickSpeech = true;
			currentLobby.validateTeams();
			Random random = new Random();
			int num2 = -num;
			for (int j = 0; j < num2; j++)
			{
				int num3 = 0;
				bool flag = false;
				while (!flag)
				{
					num3 = random.Next(29);
					switch (num3)
					{
					case 20:
					case 21:
						if (!FrontendMenus.DLC1Owned)
						{
							continue;
						}
						break;
					case 22:
					case 23:
						if (!FrontendMenus.DLC2Owned)
						{
							continue;
						}
						break;
					case 25:
					case 26:
						if (!FrontendMenus.DLC3Owned)
						{
							continue;
						}
						break;
					case 27:
					case 28:
						if (!FrontendMenus.DLC4Owned)
						{
							continue;
						}
						break;
					}
					break;
				}
				if (currentLobby.members.Count >= PlayerCap || currentLobby.members.Count >= currentLobby.iMaxPlayers)
				{
					break;
				}
				Platform_Multiplayer.MPLobbyMember mPLobbyMember = Platform_Multiplayer.Instance.AddSkirmishPlayerLocal(num3);
				updateSteamIDMappings();
				if (mPLobbyMember != null)
				{
					int thisPlayerFromSteamID = currentLobby.getThisPlayerFromSteamID(mPLobbyMember.id.m_SteamID);
					AIVs[thisPlayerFromSteamID - 1].Init(num3, "");
				}
			}
			updateSteamIDMappings();
			ReSortTeamInfo();
			UpdateHostInfo();
			CreateTeamShields();
			UpdateRadarShieldPositions();
			UpdateRandomAIButtons();
			break;
		}
		}
	}

	public void UpdateRandomAIButtons()
	{
		int num = currentLobby.iMaxPlayers - 1;
		if (num < 0)
		{
			num = 0;
		}
		((UIElement)RefRandomAI1).IsEnabled = num >= 1;
		((UIElement)RefRandomAI2).IsEnabled = num >= 2;
		((UIElement)RefRandomAI3).IsEnabled = num >= 3;
		((UIElement)RefRandomAI4).IsEnabled = num >= 4;
		((UIElement)RefRandomAI5).IsEnabled = num >= 5;
		((UIElement)RefRandomAI6).IsEnabled = num >= 6;
		((UIElement)RefRandomAI7).IsEnabled = num >= 7;
	}

	public void MonitorAILordText()
	{
		if (AILordTextClear != DateTime.MinValue && AILordTextClear < DateTime.UtcNow)
		{
			AILordTextClear = DateTime.MinValue;
			MainViewModel.Instance.SkirmishLordRolloverName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_WR_CHOOSE_OPP);
			MainViewModel.Instance.SkirmishLordRolloverName2 = "";
			MainViewModel.Instance.SkirmishLordRolloverDesc = "";
			MainViewModel.Instance.Show_AddAIPanel_Rollover = false;
		}
	}

	public void AILordEnter(string param)
	{
		AILordTextClear = DateTime.MinValue;
		int num = int.Parse(param);
		if (num == 99)
		{
			MainViewModel.Instance.SkirmishLordRolloverName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 385);
			MainViewModel.Instance.SkirmishLordRolloverDesc = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 386);
			MainViewModel.Instance.SkirmishLordRolloverRating = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 387);
			MainViewModel.Instance.SkirmishLordRolloverTroops = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 388);
			MainViewModel.Instance.SkirmishLordRolloverCastle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 389);
			MainViewModel.Instance.SkirmishLordRolloverStyle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 390);
			MainViewModel.Instance.SkirmishLordRolloverSaying = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 391);
			MainViewModel.Instance.SkirmishLordRolloverFace = MainViewModel.Instance.GameSprites[724];
			MainViewModel.Instance.SkirmishLordRolloverSayingOpacity = 0f;
			MainViewModel.Instance.Show_AddAIPanel_Rollover = true;
			MainViewModel.Instance.SkirmishLordRolloverName2 = "";
			return;
		}
		if (num == 98)
		{
			MainViewModel.Instance.SkirmishLordRolloverName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 21);
			MainViewModel.Instance.SkirmishLordRolloverDesc = "";
			MainViewModel.Instance.SkirmishLordRolloverRating = "";
			MainViewModel.Instance.SkirmishLordRolloverTroops = "";
			MainViewModel.Instance.SkirmishLordRolloverCastle = "";
			MainViewModel.Instance.SkirmishLordRolloverStyle = "";
			MainViewModel.Instance.SkirmishLordRolloverSaying = "";
			MainViewModel.Instance.SkirmishLordRolloverFace = MainViewModel.Instance.GameSprites[724];
			MainViewModel.Instance.SkirmishLordRolloverSayingOpacity = 0f;
			MainViewModel.Instance.Show_AddAIPanel_Rollover = true;
			MainViewModel.Instance.SkirmishLordRolloverName2 = "";
			return;
		}
		if (num >= 25)
		{
			MainViewModel.Instance.SkirmishLordRolloverName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 453 + 17 * (num - 25));
			if ((num == 25 || num == 26) && !FrontendMenus.DLC3Owned)
			{
				MainViewModel.Instance.SkirmishLordRolloverName2 = " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 377);
			}
			else if ((num == 27 || num == 28) && !FrontendMenus.DLC4Owned)
			{
				MainViewModel.Instance.SkirmishLordRolloverName2 = " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 377);
			}
			else
			{
				MainViewModel.Instance.SkirmishLordRolloverName2 = "";
			}
			MainViewModel.Instance.SkirmishLordRolloverDesc = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 521 + (num - 25));
		}
		else if (num >= 16)
		{
			MainViewModel.Instance.SkirmishLordRolloverName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 88 + 9 * (num - 16));
			if ((num == 22 || num == 23) && !FrontendMenus.DLC2Owned)
			{
				MainViewModel.Instance.SkirmishLordRolloverName2 = " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 377);
			}
			else if ((num == 20 || num == 21) && !FrontendMenus.DLC1Owned)
			{
				MainViewModel.Instance.SkirmishLordRolloverName2 = " - " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 377);
			}
			else
			{
				MainViewModel.Instance.SkirmishLordRolloverName2 = "";
			}
			MainViewModel.Instance.SkirmishLordRolloverDesc = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 169 + (num - 16));
		}
		else
		{
			MainViewModel.Instance.SkirmishLordRolloverName2 = "";
			MainViewModel.Instance.SkirmishLordRolloverName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 239 + 9 * num);
			MainViewModel.Instance.SkirmishLordRolloverDesc = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, 385 + num);
		}
		MainViewModel.Instance.SkirmishLordRolloverRating = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_AI_LORD_HELP, (num + 1) * 5);
		MainViewModel.Instance.SkirmishLordRolloverTroops = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_AI_LORD_HELP, (num + 1) * 5 + 1);
		MainViewModel.Instance.SkirmishLordRolloverCastle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_AI_LORD_HELP, (num + 1) * 5 + 2);
		MainViewModel.Instance.SkirmishLordRolloverStyle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_AI_LORD_HELP, (num + 1) * 5 + 3);
		MainViewModel.Instance.SkirmishLordRolloverSaying = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_AI_LORD_HELP, (num + 1) * 5 + 4);
		MainViewModel.Instance.SkirmishLordRolloverSayingOpacity = 1f;
		MainViewModel.Instance.SkirmishLordRolloverFace = MainViewModel.Instance.getAIFace(num + 1);
		MainViewModel.Instance.Show_AddAIPanel_Rollover = true;
	}

	public void AILordLeave(string param)
	{
		AILordTextClear = DateTime.UtcNow.AddMilliseconds(250.0);
	}

	public void UpdateMatchmakingButton()
	{
		if (matchmakingDefault == 1)
		{
			MainViewModel.Instance.MP_LobbyLocalRegionVis = (Visibility)1;
			MainViewModel.Instance.MP_LobbyDefaultRegionVis = (Visibility)2;
			MainViewModel.Instance.MP_LobbyGlobalRegionVis = (Visibility)1;
		}
		else if (matchmakingDefault == 0)
		{
			MainViewModel.Instance.MP_LobbyLocalRegionVis = (Visibility)2;
			MainViewModel.Instance.MP_LobbyDefaultRegionVis = (Visibility)1;
			MainViewModel.Instance.MP_LobbyGlobalRegionVis = (Visibility)1;
		}
		else if (matchmakingDefault == 2)
		{
			MainViewModel.Instance.MP_LobbyLocalRegionVis = (Visibility)1;
			MainViewModel.Instance.MP_LobbyDefaultRegionVis = (Visibility)1;
			MainViewModel.Instance.MP_LobbyGlobalRegionVis = (Visibility)2;
		}
	}

	public void updateHostLobbyButton()
	{
		if (MPLobbyMode == 0)
		{
			MainViewModel.Instance.MP_PublicPrivateText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 54);
		}
		else if (MPLobbyMode == 4)
		{
			MainViewModel.Instance.MP_PublicPrivateText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 177);
		}
		else
		{
			MainViewModel.Instance.MP_PublicPrivateText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 55);
		}
		switch (MPGameType)
		{
		case 0:
			MainViewModel.Instance.MP_GameTypeText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 214);
			MainViewModel.Instance.MP_GameTypeText_Desc = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 543);
			break;
		case 1:
			MainViewModel.Instance.MP_GameTypeText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 215);
			MainViewModel.Instance.MP_GameTypeText_Desc = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 545);
			break;
		case 2:
			MainViewModel.Instance.MP_GameTypeText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 216);
			MainViewModel.Instance.MP_GameTypeText_Desc = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 544);
			break;
		case 3:
			MainViewModel.Instance.MP_GameTypeText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 215) + " + " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 216);
			MainViewModel.Instance.MP_GameTypeText_Desc = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 546);
			break;
		}
		switch (MPStartingSettings)
		{
		case 0:
			MainViewModel.Instance.MP_SettingsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 207);
			break;
		case 1:
			MainViewModel.Instance.MP_SettingsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 208);
			break;
		case 2:
			MainViewModel.Instance.MP_SettingsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 209);
			break;
		case 3:
			MainViewModel.Instance.MP_SettingsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 210);
			break;
		}
	}

	public void receivedLobbyChat(string _name, string _message, int _colourID, bool systemMessage = false)
	{
		if (systemMessage || !Platform_Multiplayer.MPChatMuted)
		{
			if (_message.Length > 300)
			{
				_message = _message.Substring(0, 300);
			}
			_message = _message.Replace("\n", "");
			LobbyChatEntry item = new LobbyChatEntry
			{
				name = _name,
				message = _message,
				colourID = _colourID,
				received = DateTime.UtcNow
			};
			lobbyChat.Add(item);
			if (lobbyChat.Count > 30)
			{
				lobbyChat.RemoveAt(0);
			}
			if (coopGame && !MainViewModel.Instance.Show_CoopConnectedChatVisible)
			{
				MainViewModel.Instance.CoopNewChatVis = true;
			}
			refreshLobbyChat();
		}
	}

	public void refreshLobbyChat(bool fromReceive = true)
	{
		//IL_03ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0415: Unknown result type (might be due to invalid IL or missing references)
		//IL_0420: Unknown result type (might be due to invalid IL or missing references)
		//IL_042b: Unknown result type (might be due to invalid IL or missing references)
		//IL_043b: Expected O, but got Unknown
		//IL_043d: Expected O, but got Unknown
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Expected O, but got Unknown
		//IL_00f6: Expected O, but got Unknown
		//IL_046e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0478: Expected O, but got Unknown
		//IL_04ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b8: Expected O, but got Unknown
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Expected O, but got Unknown
		//IL_01be: Expected O, but got Unknown
		//IL_0514: Unknown result type (might be due to invalid IL or missing references)
		//IL_051e: Expected O, but got Unknown
		//IL_04eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f5: Expected O, but got Unknown
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Expected O, but got Unknown
		//IL_022f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Expected O, but got Unknown
		//IL_029f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ec: Expected O, but got Unknown
		//IL_02ee: Expected O, but got Unknown
		//IL_0295: Unknown result type (might be due to invalid IL or missing references)
		//IL_029f: Expected O, but got Unknown
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Expected O, but got Unknown
		//IL_0324: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Expected O, but got Unknown
		//IL_0369: Unknown result type (might be due to invalid IL or missing references)
		//IL_0373: Expected O, but got Unknown
		//IL_03df: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e9: Expected O, but got Unknown
		//IL_03ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b8: Expected O, but got Unknown
		if (fromReceive)
		{
			if (lobbyChatRefreshPending)
			{
				return;
			}
			if (DateTime.UtcNow > lobbyChatRefreshTime)
			{
				lobbyChatRefreshPending = true;
				return;
			}
		}
		else
		{
			if (!lobbyChatRefreshPending || DateTime.UtcNow < lobbyChatRefreshTime)
			{
				return;
			}
			lobbyChatRefreshPending = false;
		}
		((BaseUICollection)RefMP_ChatDisplay.Inlines).Clear();
		((BaseUICollection)FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines).Clear();
		((BaseUICollection)FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines).Clear();
		((BaseUICollection)FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines).Clear();
		foreach (LobbyChatEntry item in lobbyChat)
		{
			if (item.colourID >= 0)
			{
				ImageSource colourShield = GetColourShield(item.colourID);
				InlineUIContainer val = new InlineUIContainer
				{
					Child = (UIElement)new Image
					{
						Source = colourShield,
						Width = 14f,
						Height = 14f
					}
				};
				if (MainViewModel.Instance.Show_CoopTrail1)
				{
					((UICollection<Inline>)(object)FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)(object)val);
				}
				else if (MainViewModel.Instance.Show_CoopTrail2)
				{
					((UICollection<Inline>)(object)FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)(object)val);
				}
				else if (MainViewModel.Instance.Show_CoopTrail3)
				{
					((UICollection<Inline>)(object)FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)(object)val);
				}
				else
				{
					((UICollection<Inline>)(object)RefMP_ChatDisplay.Inlines).Add((Inline)(object)val);
				}
				InlineUIContainer val2 = new InlineUIContainer
				{
					Child = (UIElement)new TextBlock
					{
						Text = " " + item.name + " :",
						Width = 600f,
						FontSize = 14f,
						Height = 14f
					}
				};
				if (MainViewModel.Instance.Show_CoopTrail1)
				{
					((UICollection<Inline>)(object)FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)(object)val2);
					((UICollection<Inline>)(object)FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)new LineBreak());
				}
				else if (MainViewModel.Instance.Show_CoopTrail2)
				{
					((UICollection<Inline>)(object)FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)(object)val2);
					((UICollection<Inline>)(object)FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)new LineBreak());
				}
				else if (MainViewModel.Instance.Show_CoopTrail3)
				{
					((UICollection<Inline>)(object)FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)(object)val2);
					((UICollection<Inline>)(object)FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)new LineBreak());
				}
				else
				{
					((UICollection<Inline>)(object)RefMP_ChatDisplay.Inlines).Add((Inline)(object)val2);
					((UICollection<Inline>)(object)RefMP_ChatDisplay.Inlines).Add((Inline)new LineBreak());
				}
				InlineUIContainer val3 = new InlineUIContainer
				{
					Child = (UIElement)new TextBlock
					{
						Text = item.message,
						TextWrapping = (TextWrapping)2,
						Margin = new Thickness(40f, 0f, 5f, 0f),
						FontSize = 12f
					}
				};
				if (MainViewModel.Instance.Show_CoopTrail1)
				{
					((UICollection<Inline>)(object)FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)(object)val3);
					((UICollection<Inline>)(object)FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)new Run(Environment.NewLine));
				}
				else if (MainViewModel.Instance.Show_CoopTrail2)
				{
					((UICollection<Inline>)(object)FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)(object)val3);
					((UICollection<Inline>)(object)FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)new Run(Environment.NewLine));
				}
				else if (MainViewModel.Instance.Show_CoopTrail3)
				{
					((UICollection<Inline>)(object)FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)(object)val3);
					((UICollection<Inline>)(object)FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)new Run(Environment.NewLine));
				}
				else
				{
					((UICollection<Inline>)(object)RefMP_ChatDisplay.Inlines).Add((Inline)(object)val3);
					((UICollection<Inline>)(object)RefMP_ChatDisplay.Inlines).Add((Inline)new Run(Environment.NewLine));
				}
			}
			else
			{
				InlineUIContainer val4 = new InlineUIContainer
				{
					Child = (UIElement)new TextBlock
					{
						Text = item.message + " " + item.name,
						Width = 600f,
						FontSize = 14f,
						Height = 16f
					}
				};
				if (MainViewModel.Instance.Show_CoopTrail1)
				{
					((UICollection<Inline>)(object)FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)(object)val4);
					((UICollection<Inline>)(object)FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)new LineBreak());
				}
				else if (MainViewModel.Instance.Show_CoopTrail2)
				{
					((UICollection<Inline>)(object)FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)(object)val4);
					((UICollection<Inline>)(object)FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)new LineBreak());
				}
				else if (MainViewModel.Instance.Show_CoopTrail3)
				{
					((UICollection<Inline>)(object)FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)(object)val4);
					((UICollection<Inline>)(object)FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines).Add((Inline)new LineBreak());
				}
				else
				{
					((UICollection<Inline>)(object)RefMP_ChatDisplay.Inlines).Add((Inline)(object)val4);
					((UICollection<Inline>)(object)RefMP_ChatDisplay.Inlines).Add((Inline)new LineBreak());
				}
			}
		}
		RefMP_ChatScrollView.ScrollToBottom();
		FRONT_CoopTrail1.Instance.RefMP_ChatScrollView.ScrollToBottom();
		FRONT_CoopTrail2.Instance.RefMP_ChatScrollView.ScrollToBottom();
		FRONT_CoopTrail3.Instance.RefMP_ChatScrollView.ScrollToBottom();
		lobbyChatRefreshTime = DateTime.UtcNow.AddMilliseconds(500.0);
	}

	public void addSystemLobbyChat(string _name, string _message)
	{
		LobbyChatEntry item = new LobbyChatEntry
		{
			name = _name,
			message = _message,
			colourID = -1,
			received = DateTime.UtcNow
		};
		lobbyChat.Add(item);
		if (lobbyChat.Count > 100)
		{
			lobbyChat.RemoveAt(0);
		}
		if (coopGame && !MainViewModel.Instance.Show_CoopConnectedChatVisible)
		{
			MainViewModel.Instance.CoopNewChatVis = true;
		}
		refreshLobbyChat();
	}

	public void updateStartingGoldLevels()
	{
		int num = MPTEMPsetupData.starting_goods_level - 1;
		int num2 = MPTEMPsetupData.fairness - 1;
		if (MPTEMPsetupData.advanced_options > 0 && MPTEMPsetupData.advopt_nogold > 0)
		{
			MainViewModel.Instance.MPGame_GoldHuman = "0";
		}
		else
		{
			MainViewModel.Instance.MPGame_GoldHuman = GameData.starting_gold_table[num, num2, 0].ToString();
		}
		MainViewModel.Instance.MPGame_GoldComputer = GameData.starting_gold_table[num, num2, 1].ToString();
	}

	public void MP_Settings_Peacetime_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelLoaded && panelActive && MPTEMPsetupData != null)
		{
			int peacetime = (int)((RangeBase)FRONT_Multiplayer_Setup.Instance.RefMP_Settings_Peacetime_Slider).Value;
			if (FatControler.ukrainian)
			{
				MainViewModel.Instance.MP_Settings_Peacetime = peacetime + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 168);
			}
			else
			{
				MainViewModel.Instance.MP_Settings_Peacetime = peacetime + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 168);
			}
			MPTEMPsetupData.peacetime = peacetime;
		}
	}

	public void MP_Settings_GameSpeed_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (!panelLoaded || !panelActive)
		{
			return;
		}
		if (MPTEMPsetupData != null)
		{
			int starting_gamespeed = (int)((RangeBase)FRONT_Multiplayer_Setup.Instance.RefMP_Settings_GameSpeed_Slider).Value * 5;
			MainViewModel.Instance.MP_Settings_GameSpeed = starting_gamespeed.ToString();
			MPTEMPsetupData.starting_gamespeed = starting_gamespeed;
		}
		else if (coopGame && MPsetupData != null)
		{
			int starting_gamespeed2 = 0;
			if (MainViewModel.Instance.Show_CoopTrail1)
			{
				starting_gamespeed2 = (int)((RangeBase)FRONT_CoopTrail1.Instance.RefMP_Settings_GameSpeed_Slider).Value * 5;
			}
			else if (MainViewModel.Instance.Show_CoopTrail2)
			{
				starting_gamespeed2 = (int)((RangeBase)FRONT_CoopTrail2.Instance.RefMP_Settings_GameSpeed_Slider).Value * 5;
			}
			else if (MainViewModel.Instance.Show_CoopTrail3)
			{
				starting_gamespeed2 = (int)((RangeBase)FRONT_CoopTrail3.Instance.RefMP_Settings_GameSpeed_Slider).Value * 5;
			}
			MainViewModel.Instance.MP_Settings_GameSpeed = starting_gamespeed2.ToString();
			MPsetupData.starting_gamespeed = starting_gamespeed2;
		}
	}

	public void UpdateHostInfo(bool delayed = false)
	{
		EngineInterface.setMultiplayerStartingData(MPsetupData);
		if (GameData.Instance.setKeepOrder(MPsetupData.start_keep_location_order))
		{
			UpdateRadarShieldPositions();
		}
		if (!skirmishGame)
		{
			if (delayed)
			{
				delayedSendDataToLobby = DateTime.UtcNow.AddSeconds(1.5);
			}
			else if (selectedMPHeader != null)
			{
				delayedSendDataToLobby = DateTime.MinValue;
				int num = selectedMPHeader.maxPlayers;
				if (num > PlayerCap)
				{
					num = PlayerCap;
				}
				if (customCoopGame)
				{
					num = 2;
				}
				Platform_Multiplayer.Instance.UpdateHostLobbyInfo(MPHostLobbyname, selectedMPHeader.display_filename, selectedMPHeader.fileName, num, customCoopGame ? 1 : 0, MPsetupData.ToString(), (int)selectedMPHeader.crc, AIVs);
			}
		}
		else if (selectedMPHeader != null)
		{
			int num2 = selectedMPHeader.maxPlayers;
			if (num2 > PlayerCap)
			{
				num2 = PlayerCap;
			}
			currentLobby.maxPlayers = num2.ToString();
		}
	}

	public void ShowColourPicker()
	{
		if (currentLobby != null)
		{
			int playerColour = GetPlayerColour();
			if (playerColour > 0)
			{
				MainViewModel.Instance.Show_MPColours = true;
				UpdateColourShields(playerColour);
			}
		}
	}

	public int GetPlayerColour()
	{
		int result = -1;
		foreach (Platform_Multiplayer.MPLobbyMember member in currentLobby.members)
		{
			if (member.IsSelf())
			{
				result = member.colourID;
				break;
			}
		}
		return result;
	}

	public void SetShieldColour(int colourID)
	{
		Platform_Multiplayer.Instance.SetPlayerColour(colourID);
		UpdateColourShields(colourID);
		UpdateRadarShieldPositions();
	}

	public void UpdateColourShields(int colourID)
	{
		PropEx.SetSprite1((UIElement)(object)RefColourSelectButton, GetColourShield(colourID));
		PropEx.SetSprite2((UIElement)(object)RefColourSelectButton, GetColourShield(colourID, 1));
		PropEx.SetSprite3((UIElement)(object)RefColourSelectButton, GetColourShield(colourID, 1));
		PropEx.SetSprite4((UIElement)(object)RefColourSelectButton, GetColourShield(colourID));
		List<int> usedColours = Platform_Multiplayer.Instance.GetUsedColours(colourID);
		bool flag = !usedColours.Contains(1);
		((UIElement)RefColShield1).IsEnabled = flag;
		((UIElement)RefColShield1).Opacity = (flag ? 1f : 0.5f);
		bool flag2 = !usedColours.Contains(2);
		((UIElement)RefColShield2).IsEnabled = flag2;
		((UIElement)RefColShield2).Opacity = (flag2 ? 1f : 0.5f);
		bool flag3 = !usedColours.Contains(3);
		((UIElement)RefColShield3).IsEnabled = flag3;
		((UIElement)RefColShield3).Opacity = (flag3 ? 1f : 0.5f);
		bool flag4 = !usedColours.Contains(4);
		((UIElement)RefColShield4).IsEnabled = flag4;
		((UIElement)RefColShield4).Opacity = (flag4 ? 1f : 0.5f);
		bool flag5 = !usedColours.Contains(5);
		((UIElement)RefColShield5).IsEnabled = flag5;
		((UIElement)RefColShield5).Opacity = (flag5 ? 1f : 0.5f);
		bool flag6 = !usedColours.Contains(6);
		((UIElement)RefColShield6).IsEnabled = flag6;
		((UIElement)RefColShield6).Opacity = (flag6 ? 1f : 0.5f);
		bool flag7 = !usedColours.Contains(7);
		((UIElement)RefColShield7).IsEnabled = flag7;
		((UIElement)RefColShield7).Opacity = (flag7 ? 1f : 0.5f);
		bool flag8 = !usedColours.Contains(8);
		((UIElement)RefColShield8).IsEnabled = flag8;
		((UIElement)RefColShield8).Opacity = (flag8 ? 1f : 0.5f);
		if (colourID == 1)
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield1, GetColourShield(1, 2));
			PropEx.SetSprite2((UIElement)(object)RefColShield1, GetColourShield(1, 2));
			PropEx.SetSprite3((UIElement)(object)RefColShield1, GetColourShield(1, 2));
			PropEx.SetSprite4((UIElement)(object)RefColShield1, GetColourShield(1, 2));
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield1, GetColourShield(1));
			PropEx.SetSprite2((UIElement)(object)RefColShield1, GetColourShield(1, 1));
			PropEx.SetSprite3((UIElement)(object)RefColShield1, GetColourShield(1, 1));
			PropEx.SetSprite4((UIElement)(object)RefColShield1, GetColourShield(1));
		}
		if (colourID == 2)
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield2, GetColourShield(2, 2));
			PropEx.SetSprite2((UIElement)(object)RefColShield2, GetColourShield(2, 2));
			PropEx.SetSprite3((UIElement)(object)RefColShield2, GetColourShield(2, 2));
			PropEx.SetSprite4((UIElement)(object)RefColShield2, GetColourShield(2, 2));
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield2, GetColourShield(2));
			PropEx.SetSprite2((UIElement)(object)RefColShield2, GetColourShield(2, 1));
			PropEx.SetSprite3((UIElement)(object)RefColShield2, GetColourShield(2, 1));
			PropEx.SetSprite4((UIElement)(object)RefColShield2, GetColourShield(2));
		}
		if (colourID == 3)
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield3, GetColourShield(3, 2));
			PropEx.SetSprite2((UIElement)(object)RefColShield3, GetColourShield(3, 2));
			PropEx.SetSprite3((UIElement)(object)RefColShield3, GetColourShield(3, 2));
			PropEx.SetSprite4((UIElement)(object)RefColShield3, GetColourShield(3, 2));
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield3, GetColourShield(3));
			PropEx.SetSprite2((UIElement)(object)RefColShield3, GetColourShield(3, 1));
			PropEx.SetSprite3((UIElement)(object)RefColShield3, GetColourShield(3, 1));
			PropEx.SetSprite4((UIElement)(object)RefColShield3, GetColourShield(3));
		}
		if (colourID == 4)
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield4, GetColourShield(4, 2));
			PropEx.SetSprite2((UIElement)(object)RefColShield4, GetColourShield(4, 2));
			PropEx.SetSprite3((UIElement)(object)RefColShield4, GetColourShield(4, 2));
			PropEx.SetSprite4((UIElement)(object)RefColShield4, GetColourShield(4, 2));
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield4, GetColourShield(4));
			PropEx.SetSprite2((UIElement)(object)RefColShield4, GetColourShield(4, 1));
			PropEx.SetSprite3((UIElement)(object)RefColShield4, GetColourShield(4, 1));
			PropEx.SetSprite4((UIElement)(object)RefColShield4, GetColourShield(4));
		}
		if (colourID == 5)
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield5, GetColourShield(5, 2));
			PropEx.SetSprite2((UIElement)(object)RefColShield5, GetColourShield(5, 2));
			PropEx.SetSprite3((UIElement)(object)RefColShield5, GetColourShield(5, 2));
			PropEx.SetSprite4((UIElement)(object)RefColShield5, GetColourShield(5, 2));
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield5, GetColourShield(5));
			PropEx.SetSprite2((UIElement)(object)RefColShield5, GetColourShield(5, 1));
			PropEx.SetSprite3((UIElement)(object)RefColShield5, GetColourShield(5, 1));
			PropEx.SetSprite4((UIElement)(object)RefColShield5, GetColourShield(5));
		}
		if (colourID == 6)
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield6, GetColourShield(6, 2));
			PropEx.SetSprite2((UIElement)(object)RefColShield6, GetColourShield(6, 2));
			PropEx.SetSprite3((UIElement)(object)RefColShield6, GetColourShield(6, 2));
			PropEx.SetSprite4((UIElement)(object)RefColShield6, GetColourShield(6, 2));
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield6, GetColourShield(6));
			PropEx.SetSprite2((UIElement)(object)RefColShield6, GetColourShield(6, 1));
			PropEx.SetSprite3((UIElement)(object)RefColShield6, GetColourShield(6, 1));
			PropEx.SetSprite4((UIElement)(object)RefColShield6, GetColourShield(6));
		}
		if (colourID == 7)
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield7, GetColourShield(7, 2));
			PropEx.SetSprite2((UIElement)(object)RefColShield7, GetColourShield(7, 2));
			PropEx.SetSprite3((UIElement)(object)RefColShield7, GetColourShield(7, 2));
			PropEx.SetSprite4((UIElement)(object)RefColShield7, GetColourShield(7, 2));
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield7, GetColourShield(7));
			PropEx.SetSprite2((UIElement)(object)RefColShield7, GetColourShield(7, 1));
			PropEx.SetSprite3((UIElement)(object)RefColShield7, GetColourShield(7, 1));
			PropEx.SetSprite4((UIElement)(object)RefColShield7, GetColourShield(7));
		}
		if (colourID == 8)
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield8, GetColourShield(8, 2));
			PropEx.SetSprite2((UIElement)(object)RefColShield8, GetColourShield(8, 2));
			PropEx.SetSprite3((UIElement)(object)RefColShield8, GetColourShield(8, 2));
			PropEx.SetSprite4((UIElement)(object)RefColShield8, GetColourShield(8, 2));
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefColShield8, GetColourShield(8));
			PropEx.SetSprite2((UIElement)(object)RefColShield8, GetColourShield(8, 1));
			PropEx.SetSprite3((UIElement)(object)RefColShield8, GetColourShield(8, 1));
			PropEx.SetSprite4((UIElement)(object)RefColShield8, GetColourShield(8));
		}
	}

	public void LeaveLobby(bool doLeaveOnSteam = true, bool refreshLobbyList = true)
	{
		if (!skirmishGame)
		{
			Platform_Multiplayer.Instance.SetMemberReadyState(state: false);
		}
		MainViewModel.Instance.StandaloneTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 51);
		MainViewModel.Instance.Show_MPJoiningLobby = true;
		pendingMPHost = false;
		MainViewModel.Instance.Show_CreatingMPHost = false;
		currentLobby = null;
		MPHostLobbyname = "";
		MPMapChecked = false;
		MPMapValid = false;
		MPLocalReady = false;
		MPLocalReadyLocked = false;
		selectedLobby = null;
		ShowSharingCode = false;
		selectedMPHeader = null;
		delayedSendDataToLobby = DateTime.MinValue;
		MainViewModel.Instance.Show_CoopOptions = false;
		MainViewModel.Instance.CoopNewChatVis = false;
		humanPlayerCount = -1;
		lobbyChat.Clear();
		((BaseUICollection)RefMP_ChatDisplay.Inlines).Clear();
		((BaseUICollection)FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines).Clear();
		((BaseUICollection)FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines).Clear();
		((BaseUICollection)FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines).Clear();
		multiplayerMapRequestTime = DateTime.MinValue;
		if (skirmishGame)
		{
			return;
		}
		if (doLeaveOnSteam)
		{
			Platform_Multiplayer.Instance.LeaveLobby();
		}
		if (refreshLobbyList)
		{
			lastAutoRefreshTime = DateTime.UtcNow;
			Platform_Multiplayer.Instance.GetLobbies(matchmakingDefault, delegate
			{
				lobbies = Platform_Multiplayer.Instance.ReadLobbies();
				populateLobbyList();
				lastAutoRefreshTime = DateTime.UtcNow.AddSeconds(-28.0);
			});
		}
	}

	public void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	public void FilterTextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
		if ((bool)e.NewValue)
		{
			MainViewModel.Instance.MultiplayerFilterLabelVis = (Visibility)1;
		}
		else if (RefMP_SearchFilter.Text.Length == 0)
		{
			MainViewModel.Instance.MultiplayerFilterLabelVis = (Visibility)2;
		}
	}

	public void FilterTextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			populateMapList(selectedMPHeader, ignoreRefresh: true);
			if (RefMP_SearchFilter.Text.Length == 0)
			{
				MainViewModel.Instance.MultiplayerFilterButtonVis = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.MultiplayerFilterButtonVis = (Visibility)2;
			}
		}
	}

	public void TextBoxCheckForEscape(object sender, KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		if ((int)e.Key == 13)
		{
			((UIElement)this).Keyboard.ClearFocus();
			KeyManager.instance.ignoreEscape();
		}
	}

	public void TextBoxEnterCheck(object sender, TextCompositionEventArgs e)
	{
		if (e.Text == "\n")
		{
			((RoutedEventArgs)e).Handled = true;
			((UIElement)this).Keyboard.ClearFocus();
		}
	}

	public void DetectChatEnter(object sender, KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		if ((int)e.Key == 6)
		{
			ButtonClicked("SendChat");
		}
	}

	public void EnterShareTextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (!panelActive)
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
			LatestSharedCode = num;
			((UIElement)RefShareJoinButton).IsEnabled = true;
		}
		else
		{
			((UIElement)RefShareJoinButton).IsEnabled = false;
		}
	}

	public void LobbyMaxPlayersSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)((RangeBase)RefLobbyMaxPlayersSlider).Value;
			MainViewModel.Instance.MPCreateMaxPlayers = num.ToString();
			PlayerCap = num;
			((RangeBase)FRONT_Multiplayer_Setup.Instance.RefSetupMaxPlayersSlider).Value = num;
		}
	}

	public void SetupMaxPlayersSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)((RangeBase)FRONT_Multiplayer_Setup.Instance.RefSetupMaxPlayersSlider).Value;
			MainViewModel.Instance.MPCreateMaxPlayers = num.ToString();
		}
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
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_020d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0291: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_030e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0324: Unknown result type (might be due to invalid IL or missing references)
		//IL_0329: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0342: Unknown result type (might be due to invalid IL or missing references)
		//IL_035a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Unknown result type (might be due to invalid IL or missing references)
		//IL_0375: Unknown result type (might be due to invalid IL or missing references)
		//IL_0389: Unknown result type (might be due to invalid IL or missing references)
		//IL_038e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03da: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0408: Unknown result type (might be due to invalid IL or missing references)
		//IL_040d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0421: Unknown result type (might be due to invalid IL or missing references)
		//IL_0426: Unknown result type (might be due to invalid IL or missing references)
		//IL_043e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0454: Unknown result type (might be due to invalid IL or missing references)
		//IL_0459: Unknown result type (might be due to invalid IL or missing references)
		//IL_046d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0472: Unknown result type (might be due to invalid IL or missing references)
		//IL_048a: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_04be: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0505: Unknown result type (might be due to invalid IL or missing references)
		//IL_050a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0522: Unknown result type (might be due to invalid IL or missing references)
		//IL_0538: Unknown result type (might be due to invalid IL or missing references)
		//IL_053d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0551: Unknown result type (might be due to invalid IL or missing references)
		//IL_0556: Unknown result type (might be due to invalid IL or missing references)
		//IL_056e: Unknown result type (might be due to invalid IL or missing references)
		((FrameworkElement)RefRadarShield1).Margin = GameData.Instance.getKeepPosition(0, scaled: true);
		((FrameworkElement)RefRadarShield2).Margin = GameData.Instance.getKeepPosition(1, scaled: true);
		((FrameworkElement)RefRadarShield3).Margin = GameData.Instance.getKeepPosition(2, scaled: true);
		((FrameworkElement)RefRadarShield4).Margin = GameData.Instance.getKeepPosition(3, scaled: true);
		((FrameworkElement)RefRadarShield5).Margin = GameData.Instance.getKeepPosition(4, scaled: true);
		((FrameworkElement)RefRadarShield6).Margin = GameData.Instance.getKeepPosition(5, scaled: true);
		((FrameworkElement)RefRadarShield7).Margin = GameData.Instance.getKeepPosition(6, scaled: true);
		((FrameworkElement)RefRadarShield8).Margin = GameData.Instance.getKeepPosition(7, scaled: true);
		Grid refRadarShieldFace = RefRadarShieldFace1;
		Thickness margin = ((FrameworkElement)RefRadarShield1).Margin;
		float num = ((Thickness)(ref margin)).Left - 4f;
		margin = ((FrameworkElement)RefRadarShield1).Margin;
		((FrameworkElement)refRadarShieldFace).Margin = new Thickness(num, ((Thickness)(ref margin)).Top - 4f, -1000f, -1000f);
		Grid refRadarShieldFace2 = RefRadarShieldFace2;
		margin = ((FrameworkElement)RefRadarShield2).Margin;
		float num2 = ((Thickness)(ref margin)).Left - 4f;
		margin = ((FrameworkElement)RefRadarShield2).Margin;
		((FrameworkElement)refRadarShieldFace2).Margin = new Thickness(num2, ((Thickness)(ref margin)).Top - 4f, -1000f, -1000f);
		Grid refRadarShieldFace3 = RefRadarShieldFace3;
		margin = ((FrameworkElement)RefRadarShield3).Margin;
		float num3 = ((Thickness)(ref margin)).Left - 4f;
		margin = ((FrameworkElement)RefRadarShield3).Margin;
		((FrameworkElement)refRadarShieldFace3).Margin = new Thickness(num3, ((Thickness)(ref margin)).Top - 4f, -1000f, -1000f);
		Grid refRadarShieldFace4 = RefRadarShieldFace4;
		margin = ((FrameworkElement)RefRadarShield4).Margin;
		float num4 = ((Thickness)(ref margin)).Left - 4f;
		margin = ((FrameworkElement)RefRadarShield4).Margin;
		((FrameworkElement)refRadarShieldFace4).Margin = new Thickness(num4, ((Thickness)(ref margin)).Top - 4f, -1000f, -1000f);
		Grid refRadarShieldFace5 = RefRadarShieldFace5;
		margin = ((FrameworkElement)RefRadarShield5).Margin;
		float num5 = ((Thickness)(ref margin)).Left - 4f;
		margin = ((FrameworkElement)RefRadarShield5).Margin;
		((FrameworkElement)refRadarShieldFace5).Margin = new Thickness(num5, ((Thickness)(ref margin)).Top - 4f, -1000f, -1000f);
		Grid refRadarShieldFace6 = RefRadarShieldFace6;
		margin = ((FrameworkElement)RefRadarShield6).Margin;
		float num6 = ((Thickness)(ref margin)).Left - 4f;
		margin = ((FrameworkElement)RefRadarShield6).Margin;
		((FrameworkElement)refRadarShieldFace6).Margin = new Thickness(num6, ((Thickness)(ref margin)).Top - 4f, -1000f, -1000f);
		Grid refRadarShieldFace7 = RefRadarShieldFace7;
		margin = ((FrameworkElement)RefRadarShield7).Margin;
		float num7 = ((Thickness)(ref margin)).Left - 4f;
		margin = ((FrameworkElement)RefRadarShield7).Margin;
		((FrameworkElement)refRadarShieldFace7).Margin = new Thickness(num7, ((Thickness)(ref margin)).Top - 4f, -1000f, -1000f);
		Grid refRadarShieldFace8 = RefRadarShieldFace8;
		margin = ((FrameworkElement)RefRadarShield8).Margin;
		float num8 = ((Thickness)(ref margin)).Left - 4f;
		margin = ((FrameworkElement)RefRadarShield8).Margin;
		((FrameworkElement)refRadarShieldFace8).Margin = new Thickness(num8, ((Thickness)(ref margin)).Top - 4f, -1000f, -1000f);
		Image refRadarShieldTeam = RefRadarShieldTeam1;
		margin = ((FrameworkElement)RefRadarShield1).Margin;
		float num9 = ((Thickness)(ref margin)).Left + 14f;
		margin = ((FrameworkElement)RefRadarShield1).Margin;
		((FrameworkElement)refRadarShieldTeam).Margin = new Thickness(num9, ((Thickness)(ref margin)).Top + 8f, -1000f, -1000f);
		Image refRadarShieldTeam2 = RefRadarShieldTeam2;
		margin = ((FrameworkElement)RefRadarShield2).Margin;
		float num10 = ((Thickness)(ref margin)).Left + 14f;
		margin = ((FrameworkElement)RefRadarShield2).Margin;
		((FrameworkElement)refRadarShieldTeam2).Margin = new Thickness(num10, ((Thickness)(ref margin)).Top + 8f, -1000f, -1000f);
		Image refRadarShieldTeam3 = RefRadarShieldTeam3;
		margin = ((FrameworkElement)RefRadarShield3).Margin;
		float num11 = ((Thickness)(ref margin)).Left + 14f;
		margin = ((FrameworkElement)RefRadarShield3).Margin;
		((FrameworkElement)refRadarShieldTeam3).Margin = new Thickness(num11, ((Thickness)(ref margin)).Top + 8f, -1000f, -1000f);
		Image refRadarShieldTeam4 = RefRadarShieldTeam4;
		margin = ((FrameworkElement)RefRadarShield4).Margin;
		float num12 = ((Thickness)(ref margin)).Left + 14f;
		margin = ((FrameworkElement)RefRadarShield4).Margin;
		((FrameworkElement)refRadarShieldTeam4).Margin = new Thickness(num12, ((Thickness)(ref margin)).Top + 8f, -1000f, -1000f);
		Image refRadarShieldTeam5 = RefRadarShieldTeam5;
		margin = ((FrameworkElement)RefRadarShield5).Margin;
		float num13 = ((Thickness)(ref margin)).Left + 14f;
		margin = ((FrameworkElement)RefRadarShield5).Margin;
		((FrameworkElement)refRadarShieldTeam5).Margin = new Thickness(num13, ((Thickness)(ref margin)).Top + 8f, -1000f, -1000f);
		Image refRadarShieldTeam6 = RefRadarShieldTeam6;
		margin = ((FrameworkElement)RefRadarShield6).Margin;
		float num14 = ((Thickness)(ref margin)).Left + 14f;
		margin = ((FrameworkElement)RefRadarShield6).Margin;
		((FrameworkElement)refRadarShieldTeam6).Margin = new Thickness(num14, ((Thickness)(ref margin)).Top + 8f, -1000f, -1000f);
		Image refRadarShieldTeam7 = RefRadarShieldTeam7;
		margin = ((FrameworkElement)RefRadarShield7).Margin;
		float num15 = ((Thickness)(ref margin)).Left + 14f;
		margin = ((FrameworkElement)RefRadarShield7).Margin;
		((FrameworkElement)refRadarShieldTeam7).Margin = new Thickness(num15, ((Thickness)(ref margin)).Top + 8f, -1000f, -1000f);
		Image refRadarShieldTeam8 = RefRadarShieldTeam8;
		margin = ((FrameworkElement)RefRadarShield8).Margin;
		float num16 = ((Thickness)(ref margin)).Left + 14f;
		margin = ((FrameworkElement)RefRadarShield8).Margin;
		((FrameworkElement)refRadarShieldTeam8).Margin = new Thickness(num16, ((Thickness)(ref margin)).Top + 8f, -1000f, -1000f);
		if (SelectedRadarKeep < 0)
		{
			RefFloatingRadarShield.Source = null;
		}
		if (SelectedRadarKeep != 0)
		{
			PropEx.SetSprite1((UIElement)(object)RefRadarShield1, getKeepShield(0));
			PropEx.SetSprite2((UIElement)(object)RefRadarShield1, getKeepShield(0, hightlighted: true));
			PropEx.SetSprite3((UIElement)(object)RefRadarShield1, getKeepShield(0, hightlighted: true));
			PropEx.SetSprite4((UIElement)(object)RefRadarShield1, getKeepShield(0));
			RefRadarShieldTeam1.Source = getKeepTeamShield(0);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(0);
			PropEx.SetSprite1((UIElement)(object)RefRadarShield1, null);
			PropEx.SetSprite2((UIElement)(object)RefRadarShield1, null);
			PropEx.SetSprite3((UIElement)(object)RefRadarShield1, null);
			PropEx.SetSprite4((UIElement)(object)RefRadarShield1, null);
			RefRadarShieldTeam1.Source = null;
		}
		if (SelectedRadarKeep != 1)
		{
			PropEx.SetSprite1((UIElement)(object)RefRadarShield2, getKeepShield(1));
			PropEx.SetSprite2((UIElement)(object)RefRadarShield2, getKeepShield(1, hightlighted: true));
			PropEx.SetSprite3((UIElement)(object)RefRadarShield2, getKeepShield(1, hightlighted: true));
			PropEx.SetSprite4((UIElement)(object)RefRadarShield2, getKeepShield(1));
			RefRadarShieldTeam2.Source = getKeepTeamShield(1);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(1);
			PropEx.SetSprite1((UIElement)(object)RefRadarShield2, null);
			PropEx.SetSprite2((UIElement)(object)RefRadarShield2, null);
			PropEx.SetSprite3((UIElement)(object)RefRadarShield2, null);
			PropEx.SetSprite4((UIElement)(object)RefRadarShield2, null);
			RefRadarShieldTeam2.Source = null;
		}
		if (SelectedRadarKeep != 2)
		{
			PropEx.SetSprite1((UIElement)(object)RefRadarShield3, getKeepShield(2));
			PropEx.SetSprite2((UIElement)(object)RefRadarShield3, getKeepShield(2, hightlighted: true));
			PropEx.SetSprite3((UIElement)(object)RefRadarShield3, getKeepShield(2, hightlighted: true));
			PropEx.SetSprite4((UIElement)(object)RefRadarShield3, getKeepShield(2));
			RefRadarShieldTeam3.Source = getKeepTeamShield(2);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(2);
			PropEx.SetSprite1((UIElement)(object)RefRadarShield3, null);
			PropEx.SetSprite2((UIElement)(object)RefRadarShield3, null);
			PropEx.SetSprite3((UIElement)(object)RefRadarShield3, null);
			PropEx.SetSprite4((UIElement)(object)RefRadarShield3, null);
			RefRadarShieldTeam3.Source = null;
		}
		if (SelectedRadarKeep != 3)
		{
			PropEx.SetSprite1((UIElement)(object)RefRadarShield4, getKeepShield(3));
			PropEx.SetSprite2((UIElement)(object)RefRadarShield4, getKeepShield(3, hightlighted: true));
			PropEx.SetSprite3((UIElement)(object)RefRadarShield4, getKeepShield(3, hightlighted: true));
			PropEx.SetSprite4((UIElement)(object)RefRadarShield4, getKeepShield(3));
			RefRadarShieldTeam4.Source = getKeepTeamShield(3);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(3);
			PropEx.SetSprite1((UIElement)(object)RefRadarShield4, null);
			PropEx.SetSprite2((UIElement)(object)RefRadarShield4, null);
			PropEx.SetSprite3((UIElement)(object)RefRadarShield4, null);
			PropEx.SetSprite4((UIElement)(object)RefRadarShield4, null);
			RefRadarShieldTeam4.Source = null;
		}
		if (SelectedRadarKeep != 4)
		{
			PropEx.SetSprite1((UIElement)(object)RefRadarShield5, getKeepShield(4));
			PropEx.SetSprite2((UIElement)(object)RefRadarShield5, getKeepShield(4, hightlighted: true));
			PropEx.SetSprite3((UIElement)(object)RefRadarShield5, getKeepShield(4, hightlighted: true));
			PropEx.SetSprite4((UIElement)(object)RefRadarShield5, getKeepShield(4));
			RefRadarShieldTeam5.Source = getKeepTeamShield(4);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(4);
			PropEx.SetSprite1((UIElement)(object)RefRadarShield5, null);
			PropEx.SetSprite2((UIElement)(object)RefRadarShield5, null);
			PropEx.SetSprite3((UIElement)(object)RefRadarShield5, null);
			PropEx.SetSprite4((UIElement)(object)RefRadarShield5, null);
			RefRadarShieldTeam5.Source = null;
		}
		if (SelectedRadarKeep != 5)
		{
			PropEx.SetSprite1((UIElement)(object)RefRadarShield6, getKeepShield(5));
			PropEx.SetSprite2((UIElement)(object)RefRadarShield6, getKeepShield(5, hightlighted: true));
			PropEx.SetSprite3((UIElement)(object)RefRadarShield6, getKeepShield(5, hightlighted: true));
			PropEx.SetSprite4((UIElement)(object)RefRadarShield6, getKeepShield(5));
			RefRadarShieldTeam6.Source = getKeepTeamShield(5);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(5);
			PropEx.SetSprite1((UIElement)(object)RefRadarShield6, null);
			PropEx.SetSprite2((UIElement)(object)RefRadarShield6, null);
			PropEx.SetSprite3((UIElement)(object)RefRadarShield6, null);
			PropEx.SetSprite4((UIElement)(object)RefRadarShield6, null);
			RefRadarShieldTeam6.Source = null;
		}
		if (SelectedRadarKeep != 6)
		{
			PropEx.SetSprite1((UIElement)(object)RefRadarShield7, getKeepShield(6));
			PropEx.SetSprite2((UIElement)(object)RefRadarShield7, getKeepShield(6, hightlighted: true));
			PropEx.SetSprite3((UIElement)(object)RefRadarShield7, getKeepShield(6, hightlighted: true));
			PropEx.SetSprite4((UIElement)(object)RefRadarShield7, getKeepShield(6));
			RefRadarShieldTeam7.Source = getKeepTeamShield(6);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(6);
			PropEx.SetSprite1((UIElement)(object)RefRadarShield7, null);
			PropEx.SetSprite2((UIElement)(object)RefRadarShield7, null);
			PropEx.SetSprite3((UIElement)(object)RefRadarShield7, null);
			PropEx.SetSprite4((UIElement)(object)RefRadarShield7, null);
			RefRadarShieldTeam7.Source = null;
		}
		if (SelectedRadarKeep != 7)
		{
			PropEx.SetSprite1((UIElement)(object)RefRadarShield8, getKeepShield(7));
			PropEx.SetSprite2((UIElement)(object)RefRadarShield8, getKeepShield(7, hightlighted: true));
			PropEx.SetSprite3((UIElement)(object)RefRadarShield8, getKeepShield(7, hightlighted: true));
			PropEx.SetSprite4((UIElement)(object)RefRadarShield8, getKeepShield(7));
			RefRadarShieldTeam8.Source = getKeepTeamShield(7);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(7);
			PropEx.SetSprite1((UIElement)(object)RefRadarShield8, null);
			PropEx.SetSprite2((UIElement)(object)RefRadarShield8, null);
			PropEx.SetSprite3((UIElement)(object)RefRadarShield8, null);
			PropEx.SetSprite4((UIElement)(object)RefRadarShield8, null);
			RefRadarShieldTeam8.Source = null;
		}
		updateRadarFaces();
	}

	public void updateRadarFaces()
	{
		for (int i = 0; i < 8; i++)
		{
			createRadarFace(i);
		}
		MainViewModel.Instance.AlliesFaceX = Platform_Multiplayer.Instance.GetLocalAvatar();
	}

	public ImageSource getKeepShield(int keepID, bool hightlighted = false, bool hideBlank = false)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		int num = MPsetupData.start_keep_location_order[keepID];
		if (num < 0 || num >= 8)
		{
			if (!hideBlank)
			{
				Thickness keepPosition = GameData.Instance.getKeepPosition(keepID);
				if (((Thickness)(ref keepPosition)).Left > 0f)
				{
					return MainViewModel.Instance.GameSprites[576];
				}
			}
			return null;
		}
		if (currentLobby == null)
		{
			return null;
		}
		ulong num2 = currentLobby.this_player_to_SteamID_mapping[num];
		foreach (Platform_Multiplayer.MPLobbyMember member in currentLobby.members)
		{
			if (member.id.m_SteamID == num2)
			{
				return GameData.Instance.GetColourShield(member.colourID, mpSetupMapping: true, hightlighted);
			}
		}
		return null;
	}

	public ImageSource getKeepTeamShield(int keepID)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		int num = MPsetupData.start_keep_location_order[keepID];
		if (num < 0 || num >= 8)
		{
			Thickness keepPosition = GameData.Instance.getKeepPosition(keepID);
			_ = ((Thickness)(ref keepPosition)).Left;
			_ = 0f;
			return null;
		}
		if (currentLobby == null)
		{
			return null;
		}
		ulong num2 = currentLobby.this_player_to_SteamID_mapping[num];
		foreach (Platform_Multiplayer.MPLobbyMember member in currentLobby.members)
		{
			if (member.id.m_SteamID == num2)
			{
				int teamShield = member.teamShield;
				return MainViewModel.Instance.getTeamAlliesShield(teamShield);
			}
		}
		return null;
	}

	public void createRadarFace(int keepID)
	{
		int num = MPsetupData.start_keep_location_order[keepID];
		if (num < 0 || num >= 8)
		{
			switch (keepID)
			{
			case 0:
				MainViewModel.Instance.AlliesFaceBackground0 = null;
				MainViewModel.Instance.AlliesFace0 = null;
				break;
			case 1:
				MainViewModel.Instance.AlliesFaceBackground1 = null;
				MainViewModel.Instance.AlliesFace1 = null;
				break;
			case 2:
				MainViewModel.Instance.AlliesFaceBackground2 = null;
				MainViewModel.Instance.AlliesFace2 = null;
				break;
			case 3:
				MainViewModel.Instance.AlliesFaceBackground3 = null;
				MainViewModel.Instance.AlliesFace3 = null;
				break;
			case 4:
				MainViewModel.Instance.AlliesFaceBackground4 = null;
				MainViewModel.Instance.AlliesFace4 = null;
				break;
			case 5:
				MainViewModel.Instance.AlliesFaceBackground5 = null;
				MainViewModel.Instance.AlliesFace5 = null;
				break;
			case 6:
				MainViewModel.Instance.AlliesFaceBackground6 = null;
				MainViewModel.Instance.AlliesFace6 = null;
				break;
			case 7:
				MainViewModel.Instance.AlliesFaceBackground7 = null;
				MainViewModel.Instance.AlliesFace7 = null;
				break;
			}
			MainViewModel.Instance.AlliesHumanFaceVis[keepID] = false;
		}
		else
		{
			if (currentLobby == null)
			{
				return;
			}
			ulong num2 = currentLobby.this_player_to_SteamID_mapping[num];
			foreach (Platform_Multiplayer.MPLobbyMember member in currentLobby.members)
			{
				if (member.id.m_SteamID != num2)
				{
					continue;
				}
				switch (keepID)
				{
				case 0:
					MainViewModel.Instance.AlliesFaceBackground0 = MainViewModel.Instance.getAIFaceBackground(member.colourID, setupRemap: false, noMap: true);
					if (!member.SkirmishMember || member.SkirmishHumanMember)
					{
						MainViewModel.Instance.AlliesFace0 = Platform_Multiplayer.Instance.GetUserAvatar(num2);
					}
					else
					{
						MainViewModel.Instance.AlliesFace0 = MainViewModel.Instance.getAIFace(member.GetLordType() + 1);
					}
					break;
				case 1:
					MainViewModel.Instance.AlliesFaceBackground1 = MainViewModel.Instance.getAIFaceBackground(member.colourID, setupRemap: false, noMap: true);
					if (!member.SkirmishMember || member.SkirmishHumanMember)
					{
						MainViewModel.Instance.AlliesFace1 = Platform_Multiplayer.Instance.GetUserAvatar(num2);
					}
					else
					{
						MainViewModel.Instance.AlliesFace1 = MainViewModel.Instance.getAIFace(member.GetLordType() + 1);
					}
					break;
				case 2:
					MainViewModel.Instance.AlliesFaceBackground2 = MainViewModel.Instance.getAIFaceBackground(member.colourID, setupRemap: false, noMap: true);
					if (!member.SkirmishMember || member.SkirmishHumanMember)
					{
						MainViewModel.Instance.AlliesFace2 = Platform_Multiplayer.Instance.GetUserAvatar(num2);
					}
					else
					{
						MainViewModel.Instance.AlliesFace2 = MainViewModel.Instance.getAIFace(member.GetLordType() + 1);
					}
					break;
				case 3:
					MainViewModel.Instance.AlliesFaceBackground3 = MainViewModel.Instance.getAIFaceBackground(member.colourID, setupRemap: false, noMap: true);
					if (!member.SkirmishMember || member.SkirmishHumanMember)
					{
						MainViewModel.Instance.AlliesFace3 = Platform_Multiplayer.Instance.GetUserAvatar(num2);
					}
					else
					{
						MainViewModel.Instance.AlliesFace3 = MainViewModel.Instance.getAIFace(member.GetLordType() + 1);
					}
					break;
				case 4:
					MainViewModel.Instance.AlliesFaceBackground4 = MainViewModel.Instance.getAIFaceBackground(member.colourID, setupRemap: false, noMap: true);
					if (!member.SkirmishMember || member.SkirmishHumanMember)
					{
						MainViewModel.Instance.AlliesFace4 = Platform_Multiplayer.Instance.GetUserAvatar(num2);
					}
					else
					{
						MainViewModel.Instance.AlliesFace4 = MainViewModel.Instance.getAIFace(member.GetLordType() + 1);
					}
					break;
				case 5:
					MainViewModel.Instance.AlliesFaceBackground5 = MainViewModel.Instance.getAIFaceBackground(member.colourID, setupRemap: false, noMap: true);
					if (!member.SkirmishMember || member.SkirmishHumanMember)
					{
						MainViewModel.Instance.AlliesFace5 = Platform_Multiplayer.Instance.GetUserAvatar(num2);
					}
					else
					{
						MainViewModel.Instance.AlliesFace5 = MainViewModel.Instance.getAIFace(member.GetLordType() + 1);
					}
					break;
				case 6:
					MainViewModel.Instance.AlliesFaceBackground6 = MainViewModel.Instance.getAIFaceBackground(member.colourID, setupRemap: false, noMap: true);
					if (!member.SkirmishMember || member.SkirmishHumanMember)
					{
						MainViewModel.Instance.AlliesFace6 = Platform_Multiplayer.Instance.GetUserAvatar(num2);
					}
					else
					{
						MainViewModel.Instance.AlliesFace6 = MainViewModel.Instance.getAIFace(member.GetLordType() + 1);
					}
					break;
				case 7:
					MainViewModel.Instance.AlliesFaceBackground7 = MainViewModel.Instance.getAIFaceBackground(member.colourID, setupRemap: false, noMap: true);
					if (!member.SkirmishMember || member.SkirmishHumanMember)
					{
						MainViewModel.Instance.AlliesFace7 = Platform_Multiplayer.Instance.GetUserAvatar(num2);
					}
					else
					{
						MainViewModel.Instance.AlliesFace7 = MainViewModel.Instance.getAIFace(member.GetLordType() + 1);
					}
					break;
				}
				MainViewModel.Instance.AlliesHumanFaceVis[keepID] = !member.SkirmishMember || member.SkirmishHumanMember;
				return;
			}
			switch (keepID)
			{
			case 0:
				MainViewModel.Instance.AlliesFaceBackground0 = null;
				MainViewModel.Instance.AlliesFace0 = null;
				break;
			case 1:
				MainViewModel.Instance.AlliesFaceBackground1 = null;
				MainViewModel.Instance.AlliesFace1 = null;
				break;
			case 2:
				MainViewModel.Instance.AlliesFaceBackground2 = null;
				MainViewModel.Instance.AlliesFace2 = null;
				break;
			case 3:
				MainViewModel.Instance.AlliesFaceBackground3 = null;
				MainViewModel.Instance.AlliesFace3 = null;
				break;
			case 4:
				MainViewModel.Instance.AlliesFaceBackground4 = null;
				MainViewModel.Instance.AlliesFace4 = null;
				break;
			case 5:
				MainViewModel.Instance.AlliesFaceBackground5 = null;
				MainViewModel.Instance.AlliesFace5 = null;
				break;
			case 6:
				MainViewModel.Instance.AlliesFaceBackground6 = null;
				MainViewModel.Instance.AlliesFace6 = null;
				break;
			case 7:
				MainViewModel.Instance.AlliesFaceBackground7 = null;
				MainViewModel.Instance.AlliesFace7 = null;
				break;
			}
			MainViewModel.Instance.AlliesHumanFaceVis[keepID] = false;
		}
	}

	public bool updateSteamIDMappings()
	{
		if (!skirmishGame && (currentLobby == null || !currentLobby.isHost))
		{
			return false;
		}
		ulong[] array = new ulong[8];
		for (int i = 0; i < 8; i++)
		{
			array[i] = currentLobby.this_player_to_SteamID_mapping[i];
		}
		List<ulong> list = new List<ulong>();
		foreach (Platform_Multiplayer.MPLobbyMember member in currentLobby.members)
		{
			list.Add(member.id.m_SteamID);
		}
		for (int j = 0; j < 8; j++)
		{
			if (currentLobby.this_player_to_SteamID_mapping[j] != 0L)
			{
				if (!list.Contains(currentLobby.this_player_to_SteamID_mapping[j]))
				{
					remove_player_from_keep_locations(currentLobby.this_player_to_SteamID_mapping[j]);
					currentLobby.this_player_to_SteamID_mapping[j] = 0uL;
				}
				else
				{
					list.Remove(currentLobby.this_player_to_SteamID_mapping[j]);
					add_player_to_keep_locations(currentLobby.this_player_to_SteamID_mapping[j]);
				}
			}
		}
		foreach (ulong item in list)
		{
			for (int k = 0; k < 8; k++)
			{
				if (currentLobby.this_player_to_SteamID_mapping[k] == 0L)
				{
					currentLobby.this_player_to_SteamID_mapping[k] = item;
					add_player_to_keep_locations(item);
					break;
				}
			}
		}
		for (int l = 0; l < 8; l++)
		{
			if (array[l] != currentLobby.this_player_to_SteamID_mapping[l])
			{
				return true;
			}
		}
		return false;
	}

	public void add_player_to_keep_locations(ulong steamID)
	{
		if (!skirmishGame && (currentLobby == null || !currentLobby.isHost))
		{
			return;
		}
		int thisPlayerFromSteamID = currentLobby.getThisPlayerFromSteamID(steamID);
		if (thisPlayerFromSteamID < 0)
		{
			return;
		}
		for (int i = 0; i < 8; i++)
		{
			if (MPsetupData.start_keep_location_order[i] == thisPlayerFromSteamID - 1)
			{
				return;
			}
		}
		int num = 0;
		int num2 = 0;
		for (int j = 0; j < 8; j++)
		{
			if (GameData.Instance.Keep_Locations[j, 0] >= 0)
			{
				num++;
				if (MPsetupData.start_keep_location_order[j] >= 0)
				{
					num2++;
				}
			}
		}
		if (num <= num2)
		{
			return;
		}
		int num3 = new Random().Next(num - num2);
		for (int k = 0; k < 8; k++)
		{
			if (GameData.Instance.Keep_Locations[k, 0] >= 0 && MPsetupData.start_keep_location_order[k] < 0)
			{
				num3--;
				if (num3 < 0)
				{
					MPsetupData.start_keep_location_order[k] = thisPlayerFromSteamID - 1;
					UpdateHostInfo(delayed: true);
					break;
				}
			}
		}
	}

	public void remove_player_from_keep_locations(ulong steamID)
	{
		if (!skirmishGame && (currentLobby == null || !currentLobby.isHost))
		{
			return;
		}
		int thisPlayerFromSteamID = currentLobby.getThisPlayerFromSteamID(steamID);
		if (thisPlayerFromSteamID < 0)
		{
			return;
		}
		for (int i = 0; i < 8; i++)
		{
			if (MPsetupData.start_keep_location_order[i] + 1 == thisPlayerFromSteamID)
			{
				MPsetupData.start_keep_location_order[i] = -10;
				UpdateHostInfo(delayed: true);
				break;
			}
		}
	}

	public void update_keep_locations_on_map_change()
	{
		if (!skirmishGame && (currentLobby == null || !currentLobby.isHost))
		{
			return;
		}
		for (int i = 0; i < 8; i++)
		{
			MPsetupData.start_keep_location_order[i] = -10;
		}
		foreach (Platform_Multiplayer.MPLobbyMember member in currentLobby.members)
		{
			add_player_to_keep_locations(member.id.m_SteamID);
		}
	}

	public void UpdateCustomLordNamesFromMP()
	{
		if (currentLobby == null || currentLobby.members == null)
		{
			return;
		}
		foreach (Platform_Multiplayer.MPLobbyMember member in currentLobby.members)
		{
			if (member.SkirmishMember && !member.SkirmishHumanMember)
			{
				string input = "";
				switch (currentLobby.getThisPlayerFromSteamID(member.id.m_SteamID))
				{
				case 2:
					input = currentLobby.AIVDataPlayer2;
					break;
				case 3:
					input = currentLobby.AIVDataPlayer3;
					break;
				case 4:
					input = currentLobby.AIVDataPlayer4;
					break;
				case 5:
					input = currentLobby.AIVDataPlayer5;
					break;
				case 6:
					input = currentLobby.AIVDataPlayer6;
					break;
				case 7:
					input = currentLobby.AIVDataPlayer7;
					break;
				case 8:
					input = currentLobby.AIVDataPlayer8;
					break;
				}
				string customLordName = MPAIVInfo.decodeLordName(input);
				member.customLordName = customLordName;
			}
		}
	}

	public void RestartSkirmishGame(HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
	{
		currentLobby = new Platform_Multiplayer.MPLobby(restartInfo);
		PopulateAIVsFromRestartInfo(restartInfo);
		Platform_Multiplayer.Instance.activeLobby = currentLobby;
		MPsetupData = restartInfo.MPsetupData;
		selectedMPHeader = restartInfo.selectedHeader;
		trailMakerMode = restartInfo.customTestMission;
		updateSteamIDMappings();
		if (currentLobby.kickEmptySlots())
		{
			updateSteamIDMappings();
		}
		StartSkirmishGame();
	}

	public void StartCustomTrailMission(HUD_IngameMenu.RestartSkirmishMapInfo restartInfo, FileHeader headerToUse = null)
	{
		currentLobby = new Platform_Multiplayer.MPLobby(restartInfo);
		PopulateAIVsFromRestartInfo(restartInfo);
		Platform_Multiplayer.Instance.activeLobby = currentLobby;
		MPsetupData = restartInfo.MPsetupData;
		selectedMPHeader = headerToUse;
		trailMakerMode = restartInfo.customTestMission;
		updateSteamIDMappings();
		if (currentLobby.kickEmptySlots())
		{
			updateSteamIDMappings();
		}
		bool flag = false;
		int num = ConfigSettings.Settings_PlayerColour + 1;
		Platform_Multiplayer.MPLobbyMember mPLobbyMember = null;
		foreach (Platform_Multiplayer.MPLobbyMember member in currentLobby.members)
		{
			if (member.SkirmishHumanMember && member.colourID != num)
			{
				flag = true;
				mPLobbyMember = member;
				break;
			}
		}
		if (flag)
		{
			bool flag2 = false;
			foreach (Platform_Multiplayer.MPLobbyMember member2 in currentLobby.members)
			{
				if (!member2.SkirmishHumanMember && member2.colourID == num)
				{
					flag2 = true;
					int colourID = mPLobbyMember.colourID;
					mPLobbyMember.colourID = num;
					member2.colourID = colourID;
					break;
				}
			}
			if (!flag2 && mPLobbyMember != null)
			{
				mPLobbyMember.colourID = num;
			}
		}
		StartSkirmishGame(restartInfo);
	}

	public void PopulateAIVsFromRestartInfo(HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
	{
		MPAIVInfo[] aivs = restartInfo.aivs;
		AIVs = new MPAIVInfo[8];
		if (aivs != null && aivs.Length == 8)
		{
			for (int i = 0; i < 8; i++)
			{
				AIVs[i] = new MPAIVInfo();
				AIVs[i].lordType = aivs[i].lordType;
				AIVs[i].lordName = aivs[i].lordName;
				AIVs[i].builtIn = aivs[i].builtIn;
				AIVs[i].community = aivs[i].community;
				AIVs[i].historical = aivs[i].historical;
				AIVs[i].rotation = aivs[i].rotation;
				AIVs[i].aivs = new List<CustomisationFileManager.CustomAIV>();
				foreach (CustomisationFileManager.CustomAIV aiv in aivs[i].aivs)
				{
					AIVs[i].aivs.Add(aiv);
				}
				AIVs[i].builtInLord = aivs[i].builtInLord;
				AIVs[i].lordConfig = aivs[i].lordConfig;
				AIVs[i].imageData = aivs[i].imageData;
				AIVs[i].image = aivs[i].image;
			}
			return;
		}
		for (int j = 0; j < 8; j++)
		{
			AIVs[j] = new MPAIVInfo();
			if (j < restartInfo.lordTypes.Count && restartInfo.lordTypes[j] >= 0)
			{
				AIVs[j].Init((restartInfo.lordTypes[j] - 1) / 8, "");
			}
		}
	}

	public void StartSkirmishGame(HUD_IngameMenu.RestartSkirmishMapInfo customTrailRestartInfo = null)
	{
		MainViewModel.Instance.Show_BlackOut = true;
		MainViewModel.Instance.Show_MultiplayerSetup = false;
		HUD_LoadSaveRequester.ClearSavedName(selectedMPHeader.display_filename);
		MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MainGame);
		if (!coopGame)
		{
			for (int i = 0; i < 8; i++)
			{
				MPsetupData.preferredAIVs[i] = -1 - AIVs[i].rotation;
			}
		}
		HUD_IngameMenu.RestartSkirmishMapInfo restartSkirmishMapInfo = customTrailRestartInfo;
		if (restartSkirmishMapInfo == null)
		{
			restartSkirmishMapInfo = new HUD_IngameMenu.RestartSkirmishMapInfo();
			restartSkirmishMapInfo.customisedExtremeTrail = extremeTrailCustomised;
			restartSkirmishMapInfo.MPsetupData = MPsetupData;
			restartSkirmishMapInfo.selectedHeader = selectedMPHeader;
			restartSkirmishMapInfo.importMembers(currentLobby);
			restartSkirmishMapInfo.importAIVs(AIVs);
		}
		MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo = restartSkirmishMapInfo;
		MainViewModel.Instance.HUDIngameMenu.restartMapInfo = null;
		MainViewModel.Instance.HUDIngameMenu.restartMPInfo = null;
		if (trailMakerMode)
		{
			restartSkirmishMapInfo.customTestMission = true;
		}
		Director.instance.SetEngineFrameRate(MPsetupData.starting_gamespeed);
		EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
		int num = 0;
		if (!coopGame)
		{
			EngineInterface.initMultiplayerGame(skirmishGame: true, restartSkirmishMapInfo.encode(), 0, 0, trailMakerMode, restartSkirmishMapInfo.customTrail, extremeTrailCustomised);
		}
		else
		{
			if (FrontendMenus.CurrentSelectedTrail == 21)
			{
				num = 1;
			}
			else if (FrontendMenus.CurrentSelectedTrail == 22)
			{
				num = 2;
			}
			else if (FrontendMenus.CurrentSelectedTrail == 23)
			{
				num = 3;
			}
			EngineInterface.initMultiplayerGame(skirmishGame: true, restartSkirmishMapInfo.encode(), num, selectedCoopMissionID);
		}
		EngineInterface.setMultiplayerStartingData(MPsetupData);
		EngineInterface.InitAIVLoading();
		Platform_Multiplayer.Instance.gameMembers = new List<Platform_Multiplayer.MPGameMember>();
		int num2 = 1;
		int localPlayer = -1;
		foreach (Platform_Multiplayer.MPLobbyMember member in currentLobby.members)
		{
			Platform_Multiplayer.MPGameMember mPGameMember = new Platform_Multiplayer.MPGameMember();
			num2 = (mPGameMember.playerID = currentLobby.getThisPlayerFromSteamID(member.id.m_SteamID));
			mPGameMember.colourID = member.colourID;
			int team = currentLobby.getTeam(member);
			if (currentLobby.CountTeamMembers(team) <= 1)
			{
				team = 0;
			}
			if (member.SkirmishHumanMember || (coopGame && num2 == 1))
			{
				mPGameMember.playerName = ConfigSettings.Settings_UserName;
				localPlayer = num2;
				EngineInterface.RegisterMPPlayer(num2, member.Name, team, localPlayer: true, -1);
			}
			else
			{
				EngineInterface.RegisterSkirmishUser(num2, member.GetLordType(), member.GetLordSubType(), team);
				if (coopGame && num > 0)
				{
					AIVLoader.UploadDefaultAIV(member.GetLordType(), num2);
				}
				else
				{
					if (AIVs[num2 - 1].builtIn || AIVs[num2 - 1].aivs.Count == 0)
					{
						AIVLoader.UploadDefaultAIV(member.GetLordType(), num2);
					}
					else if (AIVs[num2 - 1].community)
					{
						AIVLoader.UploadDefaultAIV(member.GetLordType(), num2, evreySkirmishSet: true);
					}
					else if (AIVs[num2 - 1].historical)
					{
						AIVLoader.UploadDefaultAIV(member.GetLordType(), num2, evreySkirmishSet: false, evreyHistoricalSet: true);
					}
					else
					{
						int num3 = 0;
						foreach (CustomisationFileManager.CustomAIV aiv in AIVs[num2 - 1].aivs)
						{
							EngineInterface.ImportAIV(num2 - 1, num3, aiv.data, 1);
							num3++;
						}
					}
					if (!AIVs[num2 - 1].builtInLord && AIVs[num2 - 1].lordConfig != null)
					{
						EngineInterface.setCustomLordConfig(ref AIVs[num2 - 1].lordConfig.lordData, num2);
					}
				}
			}
			Platform_Multiplayer.Instance.gameMembers.Add(mPGameMember);
			num2++;
		}
		if (spectatorMode)
		{
			EngineInterface.GameAction(Enums.GameActionCommand.SpectatorMode, 0, 0);
		}
		EngineInterface.LoadMapReturnData retData = EngineInterface.loadMultiplayerMap(selectedMPHeader.filePath);
		EngineInterface.SetUTF8MapName(selectedMPHeader.display_filename);
		GameData.Instance.getCachedMissionName(selectedMPHeader);
		EngineInterface.SetMPRandSeed(EngineInterface.StartMultiplayerGame(fromSave: false));
		AchievementsCommon.Instance.ResetOnMissionStart();
		EditorDirector.instance.postLoading(retData, startGameThread: false);
		EditorDirector.instance.SetLocalPlayer(localPlayer);
		SpriteMapping.BuildMultiPlayerColourMapping();
		MainViewModel.Instance.InitObjectiveGoodsPanel();
		Director.instance.startSimThread();
		Director.instance.DelayCentreKeep();
		if (!coopGame)
		{
			Director.instance.SetPostUpdateCallback(delegate
			{
				FatControler.instance.BriefingUIUpdate();
				MainViewModel.Instance.ButtonGotoBriefing("FromStory");
				MainViewModel.Instance.InitObjectiveGoodsPanel();
				MainViewModel.Instance.Show_BlackOut = false;
				Director.instance.DelayCentreKeep();
			});
		}
		else
		{
			MainViewModel.Instance.Show_BlackOut = false;
		}
		if (coopGame)
		{
			LeaveLobby(doLeaveOnSteam: true, refreshLobbyList: false);
		}
	}

	public void SkirmishRadar_OffClick(object sender, MouseEventArgs e)
	{
		SelectedRadarKeep = -1;
		MainViewModel.Instance.Show_SkirmishUIOnRadar = false;
		UpdateRadarShieldPositions();
	}

	public void RadarShield1_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("Radar1");
	}

	public void RadarShield2_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("Radar2");
	}

	public void RadarShield3_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("Radar3");
	}

	public void RadarShield4_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("Radar4");
	}

	public void RadarShield5_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("Radar5");
	}

	public void RadarShield6_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("Radar6");
	}

	public void RadarShield7_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("Radar7");
	}

	public void RadarShield8_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("Radar8");
	}

	public void RadarShield_Up(object sender, MouseEventArgs e)
	{
		if (GetOverButton(RefRadarShield1))
		{
			ButtonClicked("RadarUp1");
		}
		else if (GetOverButton(RefRadarShield2))
		{
			ButtonClicked("RadarUp2");
		}
		else if (GetOverButton(RefRadarShield3))
		{
			ButtonClicked("RadarUp3");
		}
		else if (GetOverButton(RefRadarShield4))
		{
			ButtonClicked("RadarUp4");
		}
		else if (GetOverButton(RefRadarShield5))
		{
			ButtonClicked("RadarUp5");
		}
		else if (GetOverButton(RefRadarShield6))
		{
			ButtonClicked("RadarUp6");
		}
		else if (GetOverButton(RefRadarShield7))
		{
			ButtonClicked("RadarUp7");
		}
		else if (GetOverButton(RefRadarShield8))
		{
			ButtonClicked("RadarUp8");
		}
	}

	public void TeamFace1_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("TeamFace1");
	}

	public void TeamFace2_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("TeamFace2");
	}

	public void TeamFace3_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("TeamFace3");
	}

	public void TeamFace4_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("TeamFace4");
	}

	public void TeamFace5_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("TeamFace5");
	}

	public void TeamFace6_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("TeamFace6");
	}

	public void TeamFace7_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("TeamFace7");
	}

	public void TeamFace8_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("TeamFace8");
	}

	public void TeamFace_Up(object sender, MouseEventArgs e)
	{
		if (GetOverButton(RefTeamFace1))
		{
			ButtonClicked("TeamFaceUp1");
		}
		else if (GetOverButton(RefTeamFace2))
		{
			ButtonClicked("TeamFaceUp2");
		}
		else if (GetOverButton(RefTeamFace3))
		{
			ButtonClicked("TeamFaceUp3");
		}
		else if (GetOverButton(RefTeamFace4))
		{
			ButtonClicked("TeamFaceUp4");
		}
		else if (GetOverButton(RefTeamFace5))
		{
			ButtonClicked("TeamFaceUp5");
		}
		else if (GetOverButton(RefTeamFace6))
		{
			ButtonClicked("TeamFaceUp6");
		}
		else if (GetOverButton(RefTeamFace7))
		{
			ButtonClicked("TeamFaceUp7");
		}
		else if (GetOverButton(RefTeamFace8))
		{
			ButtonClicked("TeamFaceUp8");
		}
		else if (GetOverButton(RefTeamFaceCancel))
		{
			ButtonClicked("TeamFaceCancel");
		}
	}

	public void TeamFaceCancel_Click(object sender, MouseEventArgs e)
	{
		ButtonClicked("TeamFaceCancel");
	}

	public void ReSortTeamInfo()
	{
		int num = 0;
		bool[] array = new bool[9];
		int[] array2 = new int[9];
		int[] array3 = new int[9];
		int[] array4 = new int[9];
		int[] array5 = new int[9];
		for (int i = 0; i < 9; i++)
		{
			array3[i] = team_order[i];
			array5[i] = 0;
		}
		for (int i = 1; i < 9; i++)
		{
			Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID = currentLobby.GetLobbyMemberFromThis_PlayerID(i);
			if (lobbyMemberFromThis_PlayerID == null)
			{
				array4[i] = (array2[i] = -1);
				continue;
			}
			array4[i] = (array2[i] = currentLobby.getTeam(lobbyMemberFromThis_PlayerID));
			if (array2[i] >= 0 && array2[i] < 9)
			{
				array5[array2[i]]++;
			}
		}
		for (int i = 0; i < 9; i++)
		{
			if (array5[i] != 1)
			{
				continue;
			}
			for (int j = 0; j < 9; j++)
			{
				if (array2[j] == i)
				{
					array2[j] = 0;
					array5[i] = 0;
					break;
				}
			}
		}
		for (int i = 1; i < 9; i++)
		{
			team_order[i] = 0;
			array[i] = false;
		}
		for (int i = 0; i < 9; i++)
		{
			array5[i] = 0;
		}
		int num2;
		for (int i = 1; i < 9; i++)
		{
			if (array2[i] >= 0)
			{
				num2 = array2[i];
				array5[num2]++;
				if (num2 != 0)
				{
					array5[0]++;
				}
				num++;
			}
		}
		for (int j = 1; j <= 4; j++)
		{
			if (array5[j] < 1)
			{
				continue;
			}
			if (array5[j] == array5[0] && !customCoopGame)
			{
				for (int i = 1; i < 9; i++)
				{
					if (array2[i] >= 0)
					{
						array2[i] = 0;
					}
				}
				array5[j] = 0;
				break;
			}
			if (array5[j] != 1)
			{
				continue;
			}
			for (int i = 1; i < 9; i++)
			{
				if (array2[i] == j)
				{
					array2[i] = 0;
				}
			}
			array5[j] = 0;
		}
		for (int j = 1; j <= 7; j++)
		{
			bool flag = false;
			if (array5[j] != 0)
			{
				continue;
			}
			for (int i = 1; i < 9; i++)
			{
				if (array2[i] > j)
				{
					array2[i]--;
					flag = true;
				}
			}
			for (int i = j + 1; i <= 8; i++)
			{
				array5[i - 1] = array5[i];
			}
			array5[8] = 0;
			if (flag)
			{
				j--;
			}
		}
		num2 = 1;
		for (int i = 1; i <= num; i++)
		{
			int num3 = -1;
			int num4 = -1;
			for (int j = 1; j < 9; j++)
			{
				if (array2[j] >= 0 && !array[j])
				{
					int num5 = array2[j];
					if (num5 == 0)
					{
						num5 = 10;
					}
					if (num5 < num4 || num3 == -1)
					{
						num3 = j;
						num4 = num5;
					}
				}
			}
			if (num3 < 0)
			{
				break;
			}
			array[num3] = true;
			team_order[num2++] = num3;
		}
		int num6 = 1;
		for (int i = 1; i < 9; i++)
		{
			if (array5[i] == 0)
			{
				num6 = i;
				break;
			}
		}
		for (int i = 1; i < 9; i++)
		{
			if (array2[i] == 0)
			{
				array2[i] = num6++;
			}
		}
		for (int i = 0; i < 9; i++)
		{
			if (array3[i] == team_order[i] && array4[i] == array2[i])
			{
				continue;
			}
			for (i = 1; i < 9; i++)
			{
				Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID2 = currentLobby.GetLobbyMemberFromThis_PlayerID(i);
				if (lobbyMemberFromThis_PlayerID2 != null)
				{
					currentLobby.setTeam(lobbyMemberFromThis_PlayerID2, array2[i]);
				}
			}
			UpdateHostInfo();
			break;
		}
	}

	public void ClearTeamsPanel()
	{
		for (int i = 0; i < 8; i++)
		{
			orderTeamMembers[i] = null;
		}
		selectedTeamMember = null;
	}

	public void CreateTeamShields()
	{
		if (coopGame || currentLobby == null)
		{
			return;
		}
		List<Platform_Multiplayer.MPLobbyMember> members = currentLobby.members;
		int num = -2;
		int num2 = -1;
		if (members.Count >= 2 && currentLobby.getTeam(currentLobby.GetLobbyMemberFromThis_PlayerID(team_order[1])) == currentLobby.getTeam(currentLobby.GetLobbyMemberFromThis_PlayerID(team_order[2])))
		{
			num = -1;
		}
		for (int i = 0; i < members.Count; i++)
		{
			int playerID = team_order[i + 1];
			Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID = currentLobby.GetLobbyMemberFromThis_PlayerID(playerID);
			if (lobbyMemberFromThis_PlayerID == null)
			{
				continue;
			}
			lobbyMemberFromThis_PlayerID.teamShield = -1;
			int team = currentLobby.getTeam(lobbyMemberFromThis_PlayerID);
			if (num != -2)
			{
				bool flag = false;
				if (team != num)
				{
					num2++;
					flag = true;
				}
				if (!flag || (i != members.Count - 1 && team == currentLobby.getTeam(currentLobby.GetLobbyMemberFromThis_PlayerID(team_order[i + 2]))))
				{
					lobbyMemberFromThis_PlayerID.teamShield = team;
				}
				num = team;
			}
		}
	}

	public void PopulateTeamsPanel()
	{
		//IL_0311: Unknown result type (might be due to invalid IL or missing references)
		//IL_037d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0382: Unknown result type (might be due to invalid IL or missing references)
		//IL_0395: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ce: Expected O, but got Unknown
		ReSortTeamInfo();
		ClearTeamsPanel();
		MainViewModel.Instance.AlliesFace = null;
		MainViewModel.Instance.AlliesFaceBackground = null;
		MainViewModel.Instance.AlliesHumanFaceVisible = false;
		List<Platform_Multiplayer.MPLobbyMember> members = currentLobby.members;
		MPTotalPlayers = members.Count;
		for (int i = 0; i < 8; i++)
		{
			MainViewModel.Instance.setAlliesFace(i, null, null);
			MainViewModel.Instance.AlliesHumanFaceVis[i] = false;
			MainViewModel.Instance.SkirmishTeamSlicesRed[i] = false;
			MainViewModel.Instance.SkirmishTeamSlicesYellow[i] = false;
			MainViewModel.Instance.SkirmishTeamSlicesBlue[i] = false;
			MainViewModel.Instance.SkirmishTeamSlicesGreen[i] = false;
		}
		int num = -2;
		int num2 = -1;
		if (members.Count >= 2 && currentLobby.getTeam(currentLobby.GetLobbyMemberFromThis_PlayerID(team_order[1])) == currentLobby.getTeam(currentLobby.GetLobbyMemberFromThis_PlayerID(team_order[2])))
		{
			num = -1;
		}
		for (int j = members.Count; j < 8; j++)
		{
			MainViewModel.Instance.SkirmishTeamNames[j] = "";
		}
		for (int k = 0; k < members.Count; k++)
		{
			ImageSource requestedGoods = null;
			int playerID = team_order[k + 1];
			Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID = currentLobby.GetLobbyMemberFromThis_PlayerID(playerID);
			lobbyMemberFromThis_PlayerID.teamShield = -1;
			orderTeamMembers[k] = lobbyMemberFromThis_PlayerID;
			int team = currentLobby.getTeam(lobbyMemberFromThis_PlayerID);
			if (num != -2)
			{
				bool flag = false;
				if (team != num)
				{
					num2++;
					flag = true;
				}
				if (!flag || (k != members.Count - 1 && team == currentLobby.getTeam(currentLobby.GetLobbyMemberFromThis_PlayerID(team_order[k + 2]))))
				{
					switch (num2)
					{
					case 0:
						MainViewModel.Instance.SkirmishTeamSlicesRed[k] = true;
						break;
					case 1:
						MainViewModel.Instance.SkirmishTeamSlicesYellow[k] = true;
						break;
					case 2:
						MainViewModel.Instance.SkirmishTeamSlicesBlue[k] = true;
						break;
					case 3:
						MainViewModel.Instance.SkirmishTeamSlicesGreen[k] = true;
						break;
					}
					requestedGoods = MainViewModel.Instance.getTeamAlliesShield(team);
					lobbyMemberFromThis_PlayerID.teamShield = team;
				}
			}
			currentLobby.getThisPlayerFromSteamID(lobbyMemberFromThis_PlayerID.id.m_SteamID);
			if (lobbyMemberFromThis_PlayerID.SkirmishMember && !lobbyMemberFromThis_PlayerID.SkirmishHumanMember)
			{
				int num3 = lobbyMemberFromThis_PlayerID.GetLordType() + 1;
				MainViewModel.Instance.setAlliesFace(k, MainViewModel.Instance.getAIFace(num3), MainViewModel.Instance.getAIFaceBackground(lobbyMemberFromThis_PlayerID.colourID, setupRemap: true), requestedGoods);
				if (num3 < 30)
				{
					MainViewModel.Instance.SkirmishTeamNames[k] = OnScreenText.getComputerName(num3, lobbyMemberFromThis_PlayerID.GetLordSubType());
				}
				else
				{
					MainViewModel.Instance.SkirmishTeamNames[k] = MapFileManager.SplitCustomTrailName(lobbyMemberFromThis_PlayerID.customLordName);
				}
			}
			else
			{
				MainViewModel.Instance.setAlliesFace(k, Platform_Multiplayer.Instance.GetUserAvatar(lobbyMemberFromThis_PlayerID.id), MainViewModel.Instance.getAIFaceBackground(lobbyMemberFromThis_PlayerID.colourID, setupRemap: true), requestedGoods);
				MainViewModel.Instance.SkirmishTeamNames[k] = lobbyMemberFromThis_PlayerID.Name;
				MainViewModel.Instance.AlliesHumanFaceVis[k] = true;
			}
			Color val = OnScreenText.Instance.MPTeamColours[MP_orig_remap_colour_order[lobbyMemberFromThis_PlayerID.colourID]];
			MainViewModel.Instance.SkirmishTeamNameColours[k] = new SolidColorBrush(Color.FromArgb(byte.MaxValue, (byte)(val.r * 255f), (byte)(val.g * 255f), (byte)(val.b * 255f)));
			if (num != -2)
			{
				num = team;
			}
		}
	}

	public bool GetOverButton(Button targetGrid)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		Point val = ((Visual)targetGrid).PointToScreen(new Point(0f, 0f));
		Point val2 = ((Visual)targetGrid).PointToScreen(new Point(((FrameworkElement)targetGrid).ActualWidth, ((FrameworkElement)targetGrid).ActualHeight - 1f));
		Vector3 mousePosition = Input.mousePosition;
		if (FatControler.arabic && !ConfigSettings.Settings_ArabicL2R)
		{
			mousePosition.x = (float)Screen.width - mousePosition.x;
		}
		mousePosition.y = (float)Screen.height - mousePosition.y;
		if (mousePosition.x < ((Point)(ref val)).X || mousePosition.x > ((Point)(ref val2)).X)
		{
			return false;
		}
		if (mousePosition.y < ((Point)(ref val)).Y || mousePosition.y > ((Point)(ref val2)).Y)
		{
			return false;
		}
		return true;
	}

	public void InitCoopMissions()
	{
		if (CoopTrail1 == null)
		{
			CoopTrail1 = new CoopMissionSetupData[10];
			int num = 0;
			CoopMissionSetupData coopMissionSetupData = new CoopMissionSetupData();
			coopMissionSetupData.mapName = "TippedScales";
			coopMissionSetupData.keepOrder = new int[8] { 1, 2, 3, 4, -1, -1, -1, -1 };
			coopMissionSetupData.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData.AIs = new int[2] { 20, 20 };
			coopMissionSetupData.AIVs = new int[2] { 100, 104 };
			CoopTrail1[num++] = coopMissionSetupData;
			CoopMissionSetupData coopMissionSetupData2 = new CoopMissionSetupData();
			coopMissionSetupData2.mapName = "Verdant Border";
			coopMissionSetupData2.keepOrder = new int[8] { 1, 2, 4, 3, -1, -1, -1, -1 };
			coopMissionSetupData2.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData2.AIs = new int[2] { 20, 19 };
			coopMissionSetupData2.AIVs = new int[2] { 103, 203 };
			CoopTrail1[num++] = coopMissionSetupData2;
			CoopMissionSetupData coopMissionSetupData3 = new CoopMissionSetupData();
			coopMissionSetupData3.mapName = "Province of Bodrum OP";
			coopMissionSetupData3.keepOrder = new int[8] { 2, 3, 6, 1, 4, 7, 5, -1 };
			coopMissionSetupData3.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData3.AIs = new int[5] { 3, 18, 17, 3, 1 };
			coopMissionSetupData3.AIVs = new int[5] { 200, 204, 201, 200, 201 };
			CoopTrail1[num++] = coopMissionSetupData3;
			CoopMissionSetupData coopMissionSetupData4 = new CoopMissionSetupData();
			coopMissionSetupData4.mapName = "TheDivide";
			coopMissionSetupData4.keepOrder = new int[8] { 1, 2, 3, 6, 7, 5, 8, 4 };
			coopMissionSetupData4.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData4.AIs = new int[6] { 4, 6, 3, 2, 6, 2 };
			coopMissionSetupData4.AIVs = new int[6] { 304, 106, 202, 203, 205, 107 };
			CoopTrail1[num++] = coopMissionSetupData4;
			CoopMissionSetupData coopMissionSetupData5 = new CoopMissionSetupData();
			coopMissionSetupData5.mapName = "Oasis Struggle";
			coopMissionSetupData5.keepOrder = new int[8] { 1, 2, 3, 5, 6, 4, -1, -1 };
			coopMissionSetupData5.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData5.AIs = new int[4] { 18, 1, 20, 16 };
			coopMissionSetupData5.AIVs = new int[4] { 105, 202, 105, 402 };
			CoopTrail1[num++] = coopMissionSetupData5;
			CoopMissionSetupData coopMissionSetupData6 = new CoopMissionSetupData();
			coopMissionSetupData6.mapName = "Protected";
			coopMissionSetupData6.keepOrder = new int[8] { 1, 2, 8, 7, 3, 4, 5, 6 };
			coopMissionSetupData6.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData6.AIs = new int[6] { 19, 19, 20, 20, 20, 5 };
			coopMissionSetupData6.AIVs = new int[6] { 302, 100, 203, 202, 306, 103 };
			CoopTrail1[num++] = coopMissionSetupData6;
			CoopMissionSetupData coopMissionSetupData7 = new CoopMissionSetupData();
			coopMissionSetupData7.mapName = "HighRoad";
			coopMissionSetupData7.fairness = 3;
			coopMissionSetupData7.keepOrder = new int[8] { 1, 2, 5, 3, 4, 6, -1, -1 };
			coopMissionSetupData7.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData7.AIs = new int[4] { 15, 17, 9, 8 };
			coopMissionSetupData7.AIVs = new int[4] { 407, 305, 204, 201 };
			CoopTrail1[num++] = coopMissionSetupData7;
			CoopMissionSetupData coopMissionSetupData8 = new CoopMissionSetupData();
			coopMissionSetupData8.mapName = "CrossedStreams";
			coopMissionSetupData8.keepOrder = new int[8] { 1, 2, 4, 3, 5, 6, -1, -1 };
			coopMissionSetupData8.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData8.AIs = new int[4] { 8, 18, 18, 17 };
			coopMissionSetupData8.AIVs = new int[4] { 207, 103, 304, 204 };
			CoopTrail1[num++] = coopMissionSetupData8;
			CoopMissionSetupData coopMissionSetupData9 = new CoopMissionSetupData();
			coopMissionSetupData9.mapName = "Shattered Peninsula";
			coopMissionSetupData9.keepOrder = new int[8] { 2, 1, 3, 4, 6, 5, 7, 8 };
			coopMissionSetupData9.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData9.AIs = new int[6] { 17, 20, 3, 4, 8, 5 };
			coopMissionSetupData9.AIVs = new int[6] { 304, 205, 307, 204, 305, 203 };
			CoopTrail1[num++] = coopMissionSetupData9;
			CoopMissionSetupData coopMissionSetupData10 = new CoopMissionSetupData();
			coopMissionSetupData10.mapName = "Serpent's Gorge";
			coopMissionSetupData10.keepOrder = new int[8] { 1, 2, 3, 5, 7, 4, 6, -1 };
			coopMissionSetupData10.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData10.AIs = new int[5] { 19, 7, 12, 20, 3 };
			coopMissionSetupData10.AIVs = new int[5] { 101, 103, 306, 101, 105 };
			CoopTrail1[num++] = coopMissionSetupData10;
		}
		if (CoopTrail2 == null)
		{
			CoopTrail2 = new CoopMissionSetupData[10];
			int num2 = 0;
			CoopMissionSetupData coopMissionSetupData11 = new CoopMissionSetupData();
			coopMissionSetupData11.mapName = "Encircled";
			coopMissionSetupData11.keepOrder = new int[8] { 2, 1, 3, 4, 6, 5, -1, -1 };
			coopMissionSetupData11.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData11.AIs = new int[4] { 5, 6, 13, 8 };
			coopMissionSetupData11.AIVs = new int[4] { 206, 206, 403, 104 };
			coopMissionSetupData11.starting_level = 1;
			CoopTrail2[num2++] = coopMissionSetupData11;
			CoopMissionSetupData coopMissionSetupData12 = new CoopMissionSetupData();
			coopMissionSetupData12.mapName = "The Spine";
			coopMissionSetupData12.keepOrder = new int[8] { 1, 3, 5, 6, 2, 4, -1, -1 };
			coopMissionSetupData12.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData12.AIs = new int[4] { 19, 21, 6, 7 };
			coopMissionSetupData12.AIVs = new int[4] { 307, 206, 101, 404 };
			coopMissionSetupData12.fairness = 4;
			coopMissionSetupData12.starting_level = 1;
			CoopTrail2[num2++] = coopMissionSetupData12;
			CoopMissionSetupData coopMissionSetupData13 = new CoopMissionSetupData();
			coopMissionSetupData13.mapName = "Iron Rush";
			coopMissionSetupData13.keepOrder = new int[8] { 1, 2, 5, 4, 3, -1, -1, -1 };
			coopMissionSetupData13.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData13.AIs = new int[3] { 11, 11, 10 };
			coopMissionSetupData13.AIVs = new int[3] { 306, 305, 305 };
			coopMissionSetupData13.fairness = 5;
			coopMissionSetupData13.starting_level = 3;
			CoopTrail2[num2++] = coopMissionSetupData13;
			CoopMissionSetupData coopMissionSetupData14 = new CoopMissionSetupData();
			coopMissionSetupData14.mapName = "Crete Peninsula";
			coopMissionSetupData14.keepOrder = new int[8] { 5, 7, 1, 8, 3, 2, 6, 4 };
			coopMissionSetupData14.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData14.AIs = new int[6] { 25, 24, 5, 8, 15, 13 };
			coopMissionSetupData14.AIVs = new int[6] { 107, 307, 406, 201, 101, 205 };
			coopMissionSetupData14.fairness = 5;
			coopMissionSetupData14.starting_level = 2;
			CoopTrail2[num2++] = coopMissionSetupData14;
			CoopMissionSetupData coopMissionSetupData15 = new CoopMissionSetupData();
			coopMissionSetupData15.mapName = "Splintered Cradle";
			coopMissionSetupData15.keepOrder = new int[8] { 5, 6, 3, 2, 1, 4, -1, -1 };
			coopMissionSetupData15.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData15.AIs = new int[4] { 23, 9, 12, 3 };
			coopMissionSetupData15.AIVs = new int[4] { 405, 206, 104, 102 };
			coopMissionSetupData15.fairness = 4;
			coopMissionSetupData15.starting_level = 1;
			CoopTrail2[num2++] = coopMissionSetupData15;
			CoopMissionSetupData coopMissionSetupData16 = new CoopMissionSetupData();
			coopMissionSetupData16.mapName = "RidgeRoad";
			coopMissionSetupData16.keepOrder = new int[8] { 1, 2, 3, 4, -1, -1, -1, -1 };
			coopMissionSetupData16.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData16.AIs = new int[2] { 18, 8 };
			coopMissionSetupData16.AIVs = new int[2] { 306, 101 };
			coopMissionSetupData16.fairness = 5;
			coopMissionSetupData16.starting_level = 3;
			CoopTrail2[num2++] = coopMissionSetupData16;
			CoopMissionSetupData coopMissionSetupData17 = new CoopMissionSetupData();
			coopMissionSetupData17.mapName = "BigBend";
			coopMissionSetupData17.keepOrder = new int[8] { 1, 2, 6, 3, 5, 4, 7, -1 };
			coopMissionSetupData17.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData17.AIs = new int[5] { 22, 14, 4, 17, 3 };
			coopMissionSetupData17.AIVs = new int[5] { 305, 104, 102, 203, 404 };
			coopMissionSetupData17.fairness = 5;
			coopMissionSetupData17.starting_level = 1;
			CoopTrail2[num2++] = coopMissionSetupData17;
			CoopMissionSetupData coopMissionSetupData18 = new CoopMissionSetupData();
			coopMissionSetupData18.mapName = "DividedConquerors";
			coopMissionSetupData18.keepOrder = new int[8] { 1, 2, 5, 6, 3, 4, -1, -1 };
			coopMissionSetupData18.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData18.AIs = new int[4] { 24, 16, 20, 12 };
			coopMissionSetupData18.AIVs = new int[4] { 206, 403, 206, 401 };
			coopMissionSetupData18.fairness = 5;
			coopMissionSetupData18.starting_level = 1;
			CoopTrail2[num2++] = coopMissionSetupData18;
			CoopMissionSetupData coopMissionSetupData19 = new CoopMissionSetupData();
			coopMissionSetupData19.mapName = "TheGreatProvider";
			coopMissionSetupData19.keepOrder = new int[8] { 7, 3, 1, 2, 6, 5, 8, 4 };
			coopMissionSetupData19.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData19.AIs = new int[6] { 23, 12, 5, 25, 19, 22 };
			coopMissionSetupData19.AIVs = new int[6] { 202, 201, 303, 104, 201, 201 };
			coopMissionSetupData19.fairness = 5;
			coopMissionSetupData19.starting_level = 3;
			CoopTrail2[num2++] = coopMissionSetupData19;
			CoopMissionSetupData coopMissionSetupData20 = new CoopMissionSetupData();
			coopMissionSetupData20.mapName = "EmeraldCliff";
			coopMissionSetupData20.keepOrder = new int[8] { 5, 6, 2, 1, 8, 4, 3, 7 };
			coopMissionSetupData20.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData20.AIs = new int[6] { 18, 17, 19, 25, 15, 16 };
			coopMissionSetupData20.AIVs = new int[6] { 204, 106, 305, 103, 106, 1 };
			coopMissionSetupData20.fairness = 3;
			coopMissionSetupData20.starting_level = 3;
			CoopTrail2[num2++] = coopMissionSetupData20;
		}
		if (CoopTrail3 == null)
		{
			CoopTrail3 = new CoopMissionSetupData[10];
			int num3 = 0;
			CoopMissionSetupData coopMissionSetupData21 = new CoopMissionSetupData();
			coopMissionSetupData21.mapName = "Fortification";
			coopMissionSetupData21.keepOrder = new int[8] { 1, 2, 3, 4, -1, -1, -1, -1 };
			coopMissionSetupData21.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData21.AIs = new int[2] { 16, 5 };
			coopMissionSetupData21.AIVs = new int[2] { 1404, 1302 };
			coopMissionSetupData21.starting_level = 1;
			coopMissionSetupData21.fairness = 5;
			CoopTrail3[num3++] = coopMissionSetupData21;
			CoopMissionSetupData coopMissionSetupData22 = new CoopMissionSetupData();
			coopMissionSetupData22.mapName = "TheRunways";
			coopMissionSetupData22.keepOrder = new int[8] { 1, 4, 3, 6, 2, 5, -1, -1 };
			coopMissionSetupData22.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData22.AIs = new int[4] { 22, 11, 9, 8 };
			coopMissionSetupData22.AIVs = new int[4] { 100, 204, 1201, 1107 };
			coopMissionSetupData22.fairness = 4;
			coopMissionSetupData22.starting_level = 3;
			CoopTrail3[num3++] = coopMissionSetupData22;
			CoopMissionSetupData coopMissionSetupData23 = new CoopMissionSetupData();
			coopMissionSetupData23.mapName = "Craggy Cliffs";
			coopMissionSetupData23.keepOrder = new int[8] { 4, 5, 2, 7, 6, 1, 3, 8 };
			coopMissionSetupData23.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData23.AIs = new int[6] { 11, 8, 17, 1, 20, 21 };
			coopMissionSetupData23.AIVs = new int[6] { 300, 1201, 305, 403, 103, 206 };
			coopMissionSetupData23.fairness = 4;
			coopMissionSetupData23.starting_level = 1;
			CoopTrail3[num3++] = coopMissionSetupData23;
			CoopMissionSetupData coopMissionSetupData24 = new CoopMissionSetupData();
			coopMissionSetupData24.mapName = "CenterOfPower";
			coopMissionSetupData24.keepOrder = new int[8] { 1, 2, 7, 3, 4, 5, 8, 6 };
			coopMissionSetupData24.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData24.AIs = new int[6] { 23, 26, 5, 18, 13, 14 };
			coopMissionSetupData24.AIVs = new int[6] { 1105, 205, 306, 106, 1102, 104 };
			coopMissionSetupData24.fairness = 5;
			coopMissionSetupData24.starting_level = 1;
			CoopTrail3[num3++] = coopMissionSetupData24;
			CoopMissionSetupData coopMissionSetupData25 = new CoopMissionSetupData();
			coopMissionSetupData25.mapName = "SeparationAnxiety";
			coopMissionSetupData25.keepOrder = new int[8] { 1, 2, 8, 5, 3, 4, 7, 6 };
			coopMissionSetupData25.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData25.AIs = new int[6] { 2, 7, 6, 24, 17, 12 };
			coopMissionSetupData25.AIVs = new int[6] { 1105, 1102, 204, 201, 103, 100 };
			coopMissionSetupData25.fairness = 3;
			coopMissionSetupData25.starting_level = 3;
			CoopTrail3[num3++] = coopMissionSetupData25;
			CoopMissionSetupData coopMissionSetupData26 = new CoopMissionSetupData();
			coopMissionSetupData26.mapName = "Lakes of Konya";
			coopMissionSetupData26.keepOrder = new int[8] { 6, 3, 7, 5, 4, 2, 1, -1 };
			coopMissionSetupData26.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData26.AIs = new int[5] { 26, 22, 18, 2, 25 };
			coopMissionSetupData26.AIVs = new int[5] { 1405, 207, 104, 102, 206 };
			coopMissionSetupData26.fairness = 4;
			coopMissionSetupData26.starting_level = 3;
			CoopTrail3[num3++] = coopMissionSetupData26;
			CoopMissionSetupData coopMissionSetupData27 = new CoopMissionSetupData();
			coopMissionSetupData27.mapName = "Shattered Peninsula";
			coopMissionSetupData27.keepOrder = new int[8] { 7, 5, 1, 2, 8, 3, 4, 6 };
			coopMissionSetupData27.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData27.AIs = new int[6] { 10, 16, 25, 4, 23, 19 };
			coopMissionSetupData27.AIVs = new int[6] { 102, 305, 104, 1104, 1200, 106 };
			coopMissionSetupData27.fairness = 4;
			coopMissionSetupData27.starting_level = 2;
			CoopTrail3[num3++] = coopMissionSetupData27;
			CoopMissionSetupData coopMissionSetupData28 = new CoopMissionSetupData();
			coopMissionSetupData28.mapName = "Mind Games";
			coopMissionSetupData28.keepOrder = new int[8] { 1, 2, 4, 5, 6, 3, -1, -1 };
			coopMissionSetupData28.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData28.AIs = new int[4] { 27, 18, 13, 12 };
			coopMissionSetupData28.AIVs = new int[4] { 1105, 1305, 1302, 1102 };
			coopMissionSetupData28.fairness = 4;
			coopMissionSetupData28.starting_level = 1;
			CoopTrail3[num3++] = coopMissionSetupData28;
			CoopMissionSetupData coopMissionSetupData29 = new CoopMissionSetupData();
			coopMissionSetupData29.mapName = "PathToVictory";
			coopMissionSetupData29.keepOrder = new int[8] { 1, 2, 3, -1, -1, -1, -1, -1 };
			coopMissionSetupData29.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData29.AIs = new int[1] { 26 };
			coopMissionSetupData29.AIVs = new int[1] { 1304 };
			coopMissionSetupData29.fairness = 4;
			coopMissionSetupData29.starting_level = 3;
			CoopTrail3[num3++] = coopMissionSetupData29;
			CoopMissionSetupData coopMissionSetupData30 = new CoopMissionSetupData();
			coopMissionSetupData30.mapName = "OpenOcean";
			coopMissionSetupData30.keepOrder = new int[8] { 3, 4, 7, 6, 2, 1, 5, 8 };
			coopMissionSetupData30.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData30.AIs = new int[6] { 27, 27, 4, 4, 3, 20 };
			coopMissionSetupData30.AIVs = new int[6] { 101, 202, 1107, 1207, 207, 101 };
			coopMissionSetupData30.fairness = 2;
			coopMissionSetupData30.starting_level = 2;
			CoopTrail3[num3++] = coopMissionSetupData30;
		}
		CoopMissionSetupData[] coopTrail = CoopTrail1;
		foreach (CoopMissionSetupData coopMissionSetupData31 in coopTrail)
		{
			if (coopMissionSetupData31 != null)
			{
				coopMissionSetupData31.header = MapFileManager.Instance.GetHeaderFromFileNameMP(coopMissionSetupData31.mapName);
			}
		}
		coopTrail = CoopTrail2;
		foreach (CoopMissionSetupData coopMissionSetupData32 in coopTrail)
		{
			if (coopMissionSetupData32 != null)
			{
				coopMissionSetupData32.header = MapFileManager.Instance.GetHeaderFromFileNameMP(coopMissionSetupData32.mapName);
			}
		}
		coopTrail = CoopTrail3;
		foreach (CoopMissionSetupData coopMissionSetupData33 in coopTrail)
		{
			if (coopMissionSetupData33 != null)
			{
				coopMissionSetupData33.header = MapFileManager.Instance.GetHeaderFromFileNameMP(coopMissionSetupData33.mapName);
			}
		}
	}

	public void ClearCoopAIs()
	{
		playKickSpeech = false;
		Platform_Multiplayer.Instance.ClearAIsFromLobby();
		updateSteamIDMappings();
		ReSortTeamInfo();
		UpdateHostInfo();
		UpdateRadarShieldPositions();
		UpdateRandomAIButtons();
		playKickSpeech = true;
	}

	public void CoopMissionChanged(int trailID, int missionID, bool resetOrderSwapped = false)
	{
		if (resetOrderSwapped)
		{
			coopOrderSwapped = false;
		}
		CoopMissionSetupData coopMissionSetupData = null;
		switch (trailID)
		{
		case 0:
			Platform_Multiplayer.Instance.SetCoopTrailProgress(0, ConfigSettings.Settings_Progress_Trail_Coop1_Status, missionID, ConfigSettings.Settings_Progress_Trail_Coop1, coopOrderSwapped);
			coopMissionSetupData = CoopTrail1[missionID - 1];
			MainViewModel.Instance.CoopMissionTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_COOP, missionID);
			break;
		case 1:
			Platform_Multiplayer.Instance.SetCoopTrailProgress(1, ConfigSettings.Settings_Progress_Trail_Coop2_Status, missionID, ConfigSettings.Settings_Progress_Trail_Coop2, coopOrderSwapped);
			coopMissionSetupData = CoopTrail2[missionID - 1];
			MainViewModel.Instance.CoopMissionTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_COOP, missionID + 10);
			break;
		case 2:
			Platform_Multiplayer.Instance.SetCoopTrailProgress(2, ConfigSettings.Settings_Progress_Trail_Coop3_Status, missionID, ConfigSettings.Settings_Progress_Trail_Coop3, coopOrderSwapped);
			coopMissionSetupData = CoopTrail3[missionID - 1];
			MainViewModel.Instance.CoopMissionTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_COOP, missionID + 25);
			break;
		}
		if (coopMissionSetupData == null)
		{
			return;
		}
		if (currentLobby.isHost)
		{
			ClearCoopAIs();
			if (MainViewModel.Instance.Show_CoopHostInvitePane)
			{
				return;
			}
		}
		if (singlePlayerCoop)
		{
			Platform_Multiplayer.Instance.AddSkirmishPlayerLocal((int)singlePlayerCoopAlly, 1, notRandom: true);
		}
		currentLobby.validateTeams();
		currentLobby.forceCoopTeams();
		selectedMPHeader = coopMissionSetupData.header;
		selectedCoopMissionID = missionID - 1;
		GameData.Instance.setKeepLocationsFromHeader(selectedMPHeader);
		for (int i = 0; i < 8; i++)
		{
			MPsetupData.preferredAIVs[i] = -1;
			MainViewModel.Instance.SkirmishBriefingPreBuiltVis[i] = false;
		}
		int num = 2;
		int num2 = 0;
		int[] aIs = coopMissionSetupData.AIs;
		foreach (int num3 in aIs)
		{
			MPsetupData.preferredAIVs[num] = coopMissionSetupData.AIVs[num2++];
			if (MPsetupData.preferredAIVs[num] >= 1000)
			{
				MainViewModel.Instance.SkirmishBriefingPreBuiltVis[num] = true;
			}
			if (currentLobby.isHost)
			{
				Platform_Multiplayer.Instance.AddSkirmishPlayerLocal(num3 - 1, coopMissionSetupData.teams[num], notRandom: true);
			}
			num++;
		}
		MPsetupData.advopt_healers = 1;
		MPsetupData.advopt_nogold = 0;
		MPsetupData.advopt_eunuchs = 0;
		MPsetupData.advanced_skirmish_options = 1;
		MPsetupData.advopt_pre_build = 0;
		MPsetupData.advopt_improved_arabswordsmen = 0;
		MPsetupData.advopt_improved_laddermen = 0;
		MPsetupData.advopt_improved_spearmen = 0;
		MPsetupData.advopt_rebalanced_horsearchers = 0;
		MPsetupData.advopt_improved_fletchers = 0;
		MPsetupData.advopt_uncapped_peasants = 0;
		MPsetupData.advopt_faster_peasants = 0;
		MPsetupData.advopt_enemy_hps = 0;
		MPsetupData.global_improved_sieging = 0;
		for (int k = 0; k < 8; k++)
		{
			MPsetupData.start_keep_location_order[k] = -1;
		}
		for (int l = 0; l < 8; l++)
		{
			if (coopOrderSwapped && l < 2)
			{
				if (coopMissionSetupData.keepOrder[l ^ 1] > 0)
				{
					MPsetupData.start_keep_location_order[coopMissionSetupData.keepOrder[l ^ 1] - 1] = l;
				}
			}
			else if (coopMissionSetupData.keepOrder[l] > 0)
			{
				MPsetupData.start_keep_location_order[coopMissionSetupData.keepOrder[l] - 1] = l;
			}
		}
		MPsetupData.fairness = coopMissionSetupData.fairness;
		MPsetupData.starting_goods_level = coopMissionSetupData.starting_level;
		if (currentLobby.isHost)
		{
			updateSteamIDMappings();
			switch (trailID)
			{
			case 0:
				FRONT_CoopTrail1.Instance.UpdateRadarShieldPositions();
				break;
			case 1:
				FRONT_CoopTrail2.Instance.UpdateRadarShieldPositions();
				break;
			case 2:
				FRONT_CoopTrail3.Instance.UpdateRadarShieldPositions();
				break;
			}
		}
		UpdateHostInfo();
		updateRadarTexture(coopMissionSetupData.header);
	}

	public void PopulateMapDetailsPanel(FileHeader header)
	{
		MainViewModel.Instance.StandaloneMissionText = GameData.Instance.GetMissionBriefing(header);
		MainViewModel.Instance.StandaloneMissionTitle = header.display_filename;
		MainViewModel.Instance.Show_SkirmishAllowOutposts = header.hasOutposts;
		MainViewModel.Instance.Show_StandaloneMissionHasOutposts = header.hasOutposts;
		if (header.world_size >= 160 && header.world_size <= 800)
		{
			MainViewModel.Instance.StandaloneMissionSize = header.world_size.ToString();
		}
		else
		{
			MainViewModel.Instance.StandaloneMissionSize = "?";
		}
		MainViewModel.Instance.StandaloneMissionPlayerCount = header.maxPlayers.ToString();
		MainViewModel.Instance.Show_StandaloneMissionBalanced = header.balanced;
		MainViewModel.Instance.Show_StandaloneMissionUnBalanced = !header.balanced;
	}

	public ImageSource requestAvatar(int _row, ulong _steamID)
	{
		ImageSource userAvatar = Platform_Multiplayer.Instance.GetUserAvatar(_steamID);
		if ((BaseComponent)(object)userAvatar != (BaseComponent)null)
		{
			return userAvatar;
		}
		Platform_Multiplayer.Instance.RequestUserAvatar(_steamID);
		userAvatar = Platform_Multiplayer.Instance.GetUserAvatar(_steamID);
		if ((BaseComponent)(object)userAvatar != (BaseComponent)null)
		{
			return userAvatar;
		}
		avatarCallbacks.Enqueue(new AvatarCallback
		{
			row = _row,
			steamID = _steamID
		});
		return null;
	}

	public void ShowHidden_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			if (FrontendMenus.CurrentSelectedTrail == 21)
			{
				coopShowHiddenFriends = ((ToggleButton)FRONT_CoopTrail1.Instance.RefShowHidden).IsChecked.Value;
			}
			else if (FrontendMenus.CurrentSelectedTrail == 22)
			{
				coopShowHiddenFriends = ((ToggleButton)FRONT_CoopTrail2.Instance.RefShowHidden).IsChecked.Value;
			}
			else if (FrontendMenus.CurrentSelectedTrail == 23)
			{
				coopShowHiddenFriends = ((ToggleButton)FRONT_CoopTrail3.Instance.RefShowHidden).IsChecked.Value;
			}
			coopFriendsPage = 0;
			CoopPopulateFriendsList();
		}
	}

	public void CoopPopulateFriendsList()
	{
		MainViewModel.Instance.Show_CoopShowUp = coopFriendsPage > 0;
		int coopTrailCount = ConfigSettings.getCoopTrailCount(coopShowHiddenFriends);
		MainViewModel.Instance.Show_CoopShowDown = coopFriendsPage < (coopTrailCount - 1) / 8;
		int trailID = 0;
		if (FrontendMenus.CurrentSelectedTrail == 22)
		{
			trailID = 1;
		}
		else if (FrontendMenus.CurrentSelectedTrail == 23)
		{
			trailID = 2;
		}
		for (int i = 0; i < 8; i++)
		{
			if (ConfigSettings.getCoopRowInfo(i + coopFriendsPage * 8, trailID, out var steamID, out var userName, out var hidden, coopShowHiddenFriends, out var CoAString) != null)
			{
				if (steamID < 2000)
				{
					ImageSource aIFace = MainViewModel.Instance.getAIFace((int)steamID - 1000 + 1);
					SetCoopRow(i, userName, steamID, aIFace, hidden);
					continue;
				}
				ImageSource avatar;
				if (CoAString != null && CoAString.Length > 0)
				{
					tempAD.FromString(CoAString);
					Platform_Multiplayer.Instance.CreateCoAAvatar(steamID, tempAD);
					avatar = Platform_Multiplayer.Instance.GetUserAvatar(steamID);
				}
				else
				{
					avatar = requestAvatar(i, steamID);
				}
				string text = Platform_Multiplayer.Instance.getSteamUserName(steamID);
				if (text == "[unknown]")
				{
					text = userName;
				}
				SetCoopRow(i, text, steamID, avatar, hidden);
			}
			else
			{
				SetCoopRow(i, "", 0uL, null, hidden: false);
			}
		}
	}

	public void SetCoopRow(int row, string name, ulong steamID, ImageSource avatar, bool hidden)
	{
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Invalid comparison between Unknown and I4
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Invalid comparison between Unknown and I4
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Invalid comparison between Unknown and I4
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e6: Invalid comparison between Unknown and I4
		//IL_03a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a7: Invalid comparison between Unknown and I4
		//IL_0462: Unknown result type (might be due to invalid IL or missing references)
		//IL_0468: Invalid comparison between Unknown and I4
		//IL_0523: Unknown result type (might be due to invalid IL or missing references)
		//IL_0529: Invalid comparison between Unknown and I4
		//IL_05e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ea: Invalid comparison between Unknown and I4
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Invalid comparison between Unknown and I4
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Invalid comparison between Unknown and I4
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Invalid comparison between Unknown and I4
		//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f3: Invalid comparison between Unknown and I4
		//IL_03ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b4: Invalid comparison between Unknown and I4
		//IL_046f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0475: Invalid comparison between Unknown and I4
		//IL_0530: Unknown result type (might be due to invalid IL or missing references)
		//IL_0536: Invalid comparison between Unknown and I4
		//IL_05f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f7: Invalid comparison between Unknown and I4
		coopFriendsSteamIDs[row] = steamID;
		coopFriendsRowHidden[row] = hidden;
		switch (row)
		{
		case 0:
			MainViewModel.Instance.Coop_Name_1 = name;
			MainViewModel.Instance.Coop_Image_1 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_1 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_1 = (Visibility)1;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_1 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_1 = (Visibility)1;
			}
			if ((int)MainViewModel.Instance.Coop_Continue_Line_1 == 1 && (int)MainViewModel.Instance.Coop_Invite_Line_1 == 1)
			{
				MainViewModel.Instance.Coop_Show_Line_1 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_1 = (Visibility)1;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_1 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_1 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_1 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_1 = (Visibility)2;
			}
			break;
		case 1:
			MainViewModel.Instance.Coop_Name_2 = name;
			MainViewModel.Instance.Coop_Image_2 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_2 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_2 = (Visibility)1;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_2 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_2 = (Visibility)1;
			}
			if ((int)MainViewModel.Instance.Coop_Continue_Line_2 == 1 && (int)MainViewModel.Instance.Coop_Invite_Line_2 == 1)
			{
				MainViewModel.Instance.Coop_Show_Line_2 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_2 = (Visibility)1;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_2 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_2 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_2 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_2 = (Visibility)2;
			}
			break;
		case 2:
			MainViewModel.Instance.Coop_Name_3 = name;
			MainViewModel.Instance.Coop_Image_3 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_3 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_3 = (Visibility)1;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_3 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_3 = (Visibility)1;
			}
			if ((int)MainViewModel.Instance.Coop_Continue_Line_3 == 1 && (int)MainViewModel.Instance.Coop_Invite_Line_3 == 1)
			{
				MainViewModel.Instance.Coop_Show_Line_3 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_3 = (Visibility)1;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_3 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_3 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_3 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_3 = (Visibility)2;
			}
			break;
		case 3:
			MainViewModel.Instance.Coop_Name_4 = name;
			MainViewModel.Instance.Coop_Image_4 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_4 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_4 = (Visibility)1;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_4 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_4 = (Visibility)1;
			}
			if ((int)MainViewModel.Instance.Coop_Continue_Line_4 == 1 && (int)MainViewModel.Instance.Coop_Invite_Line_4 == 1)
			{
				MainViewModel.Instance.Coop_Show_Line_4 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_4 = (Visibility)1;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_4 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_4 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_4 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_4 = (Visibility)2;
			}
			break;
		case 4:
			MainViewModel.Instance.Coop_Name_5 = name;
			MainViewModel.Instance.Coop_Image_5 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_5 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_5 = (Visibility)1;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_5 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_5 = (Visibility)1;
			}
			if ((int)MainViewModel.Instance.Coop_Continue_Line_5 == 1 && (int)MainViewModel.Instance.Coop_Invite_Line_5 == 1)
			{
				MainViewModel.Instance.Coop_Show_Line_5 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_5 = (Visibility)1;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_5 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_5 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_5 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_5 = (Visibility)2;
			}
			break;
		case 5:
			MainViewModel.Instance.Coop_Name_6 = name;
			MainViewModel.Instance.Coop_Image_6 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_6 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_6 = (Visibility)1;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_6 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_6 = (Visibility)1;
			}
			if ((int)MainViewModel.Instance.Coop_Continue_Line_6 == 1 && (int)MainViewModel.Instance.Coop_Invite_Line_6 == 1)
			{
				MainViewModel.Instance.Coop_Show_Line_6 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_6 = (Visibility)1;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_6 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_6 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_6 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_6 = (Visibility)2;
			}
			break;
		case 6:
			MainViewModel.Instance.Coop_Name_7 = name;
			MainViewModel.Instance.Coop_Image_7 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_7 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_7 = (Visibility)1;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_7 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_7 = (Visibility)1;
			}
			if ((int)MainViewModel.Instance.Coop_Continue_Line_7 == 1 && (int)MainViewModel.Instance.Coop_Invite_Line_7 == 1)
			{
				MainViewModel.Instance.Coop_Show_Line_7 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_7 = (Visibility)1;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_7 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_7 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_7 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_7 = (Visibility)2;
			}
			break;
		case 7:
			MainViewModel.Instance.Coop_Name_8 = name;
			MainViewModel.Instance.Coop_Image_8 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_8 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_8 = (Visibility)1;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_8 = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_8 = (Visibility)1;
			}
			if ((int)MainViewModel.Instance.Coop_Continue_Line_8 == 1 && (int)MainViewModel.Instance.Coop_Invite_Line_8 == 1)
			{
				MainViewModel.Instance.Coop_Show_Line_8 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_8 = (Visibility)1;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_8 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_8 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_8 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_8 = (Visibility)2;
			}
			break;
		}
	}

	public void SetCoopRowAvatar(int row, ImageSource avatar)
	{
		switch (row)
		{
		case 0:
			MainViewModel.Instance.Coop_Image_1 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_1 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_1 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_1 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_1 = (Visibility)2;
			}
			break;
		case 1:
			MainViewModel.Instance.Coop_Image_2 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_2 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_2 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_2 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_2 = (Visibility)2;
			}
			break;
		case 2:
			MainViewModel.Instance.Coop_Image_3 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_3 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_3 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_3 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_3 = (Visibility)2;
			}
			break;
		case 3:
			MainViewModel.Instance.Coop_Image_4 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_4 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_4 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_4 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_4 = (Visibility)2;
			}
			break;
		case 4:
			MainViewModel.Instance.Coop_Image_5 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_5 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_5 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_5 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_5 = (Visibility)2;
			}
			break;
		case 5:
			MainViewModel.Instance.Coop_Image_6 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_6 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_6 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_6 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_6 = (Visibility)2;
			}
			break;
		case 6:
			MainViewModel.Instance.Coop_Image_7 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_7 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_7 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_7 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_7 = (Visibility)2;
			}
			break;
		case 7:
			MainViewModel.Instance.Coop_Image_8 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_8 = (Visibility)2;
				MainViewModel.Instance.Coop_Hide_Line_8 = (Visibility)1;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_8 = (Visibility)1;
				MainViewModel.Instance.Coop_Hide_Line_8 = (Visibility)2;
			}
			break;
		}
	}
}
