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
				string[] array = input.Split(':');
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
				string[] array = input.Split(':');
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
					TextureSource textureSource = MainViewModel.Instance.LoadImageFile(fileData);
					if (textureSource != null && textureSource.Width == 144f && textureSource.Height == 144f)
					{
						imageData = fileData;
						image = textureSource;
					}
				}
			}
			catch (Exception)
			{
			}
		}
	}

	private class LobbyChatEntry
	{
		public string name;

		public string message;

		public int colourID;

		public DateTime received;
	}

	public class PlayerRow
	{
		public Noesis.Grid RefRow;

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
			RefRow.Visibility = Visibility.Hidden;
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
			SetVisibility(RefRow, Visibility.Visible);
			if (playerID == 1 && !skirmishGame)
			{
				SetVisibility(RefHost, Visibility.Visible);
			}
			else
			{
				SetVisibility(RefHost, Visibility.Hidden);
			}
			if (skirmishGame)
			{
				if (player == 1 && !spectatorMode)
				{
					SetButtonVisibility(RefKick, Visibility.Hidden);
				}
				else
				{
					SetButtonVisibility(RefKick, Visibility.Visible);
				}
			}
			else if (parent.currentLobby.isHost && member.IsSelf())
			{
				SetButtonVisibility(RefKick, Visibility.Hidden);
			}
			else if (parent.currentLobby.isHost)
			{
				SetButtonVisibility(RefKick, Visibility.Visible);
			}
			else
			{
				SetButtonVisibility(RefKick, Visibility.Hidden);
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
			else if (RefPing != null)
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
				PropEx.SetButtonVisibility(RefAISettings, Visibility.Hidden);
			}
			else
			{
				RefType.Text = member.AITypeName;
				if ((skirmishGame || (parent.currentLobby != null && parent.currentLobby.isHost)) && member.SkirmishCustomLordExistsLocally)
				{
					PropEx.SetButtonVisibility(RefAISettings, Visibility.Visible);
				}
				else
				{
					PropEx.SetButtonVisibility(RefAISettings, Visibility.Hidden);
				}
			}
			if (!skirmishGame)
			{
				if (!member.SkirmishMember)
				{
					if (member.ready)
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
				else if (RefReadyState.Source != null)
				{
					RefReadyState.Source = null;
				}
			}
			else if (RefReadyState.Source != null)
			{
				RefReadyState.Source = null;
			}
			ImageSource colourShield = GetColourShield(member.colourID);
			if (RefColour.Source != colourShield)
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

		public int allowBarracksPlayer1 = 1;

		public int allowMercPostPlayer1 = 1;

		public int allowStockadePlayer1 = 1;

		public int allowBarracksPlayer2 = 1;

		public int allowMercPostPlayer2 = 1;

		public int allowStockadePlayer2 = 1;
	}

	private class AvatarCallback
	{
		public int row;

		public ulong steamID;
	}

	private const int MAX_HUMANS = 9;

	private bool panelLoaded;

	private ListView RefLobbyLists;

	private Slider RefLobbyMaxPlayersSlider;

	private Button RefJoinButton;

	private Button RefLobbySettingsButton;

	private Noesis.Grid RefHeaderBar;

	private TextBox RefTextBoxGameName;

	public Button RefMultiplayerPlayButton;

	private Button RefReadyButton;

	private Button RefReadyButtonLock;

	private Button RefLoadButton;

	private TextBox RefMP_ChatInput;

	private TextBlock RefMP_ChatDisplay;

	private ScrollViewer RefMP_ChatScrollView;

	private Button RefColourSelectButton;

	private Button RefColShield1;

	private Button RefColShield2;

	private Button RefColShield3;

	private Button RefColShield4;

	private Button RefColShield5;

	private Button RefColShield6;

	private Button RefColShield7;

	private Button RefColShield8;

	private Button RefRandomAI1;

	private Button RefRandomAI2;

	private Button RefRandomAI3;

	private Button RefRandomAI4;

	private Button RefRandomAI5;

	private Button RefRandomAI6;

	private Button RefRandomAI7;

	private Button RefMultiplayerInvite;

	private Button RefMP_ChatSend;

	public Slider RefMapSizeMin_Slider;

	public Slider RefMapSizeMax_Slider;

	public Slider RefAIMin_Slider;

	public Slider RefAIMax_Slider;

	private CheckBox RefRandomTeams;

	private CheckBox RefRandomBalance;

	private CheckBox RefRandomOutposts;

	private CheckBox RefRandomExtreme;

	private CheckBox RefRandomAdvanced;

	private CheckBox RefRandomIncludeUser;

	private CheckBox RefRandomIncludeBuiltin;

	private CheckBox RefRandomIncludeWorkshop;

	private Button RefMultiplayerSetupInfo;

	private Image RefBasemap;

	private TextBox RefMP_SearchFilter;

	private TextBox RefMP_EnterShareCodeText;

	private Button RefShareJoinButton;

	private Storyboard pulseAnimation;

	private Storyboard settingsPulseAnimation;

	private RadioButton RefFairness1;

	private RadioButton RefFairness2;

	private RadioButton RefFairness3;

	private RadioButton RefFairness4;

	private RadioButton RefFairness5;

	private RadioButton RefGameType1;

	private RadioButton RefGameType2;

	private RadioButton RefGameType3;

	public Button RefRadarShield1;

	public Button RefRadarShield2;

	public Button RefRadarShield3;

	public Button RefRadarShield4;

	public Button RefRadarShield5;

	public Button RefRadarShield6;

	public Button RefRadarShield7;

	public Button RefRadarShield8;

	public Noesis.Grid RefRadarShieldFace1;

	public Noesis.Grid RefRadarShieldFace2;

	public Noesis.Grid RefRadarShieldFace3;

	public Noesis.Grid RefRadarShieldFace4;

	public Noesis.Grid RefRadarShieldFace5;

	public Noesis.Grid RefRadarShieldFace6;

	public Noesis.Grid RefRadarShieldFace7;

	public Noesis.Grid RefRadarShieldFace8;

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

	public Noesis.Grid RefFloatingTeams;

	public Noesis.Grid RefSkirmish_RadarMask;

	public CheckBox RefExtremeWarningCheck;

	public CheckBox RefEnableAdvancedSkirmishCheck;

	private CheckBox RefChatMuteDisable;

	private TextBlock RefMap_Balanced;

	private TextBlock RefMap_UnBalanced;

	private ListView RefCustomLordList;

	private Button RefTrailMakerTest;

	private static Noesis.Color lightBarColCol = Noesis.Color.FromArgb(136, 204, 204, 204);

	private static SolidColorBrush lightBarColour = new SolidColorBrush(lightBarColCol);

	private static Noesis.Color darkBarColCol = Noesis.Color.FromArgb(136, 170, 170, 170);

	private static SolidColorBrush darkBarColour = new SolidColorBrush(darkBarColCol);

	private static Noesis.Color transparentCol = Noesis.Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue);

	private static SolidColorBrush transparentColour = new SolidColorBrush(transparentCol);

	private static Noesis.Color teamYellowBarColCol = Noesis.Color.FromArgb(136, 204, 204, 80);

	private static SolidColorBrush teamYellowBarColour = new SolidColorBrush(teamYellowBarColCol);

	private static Noesis.Color teamRedBarColCol = Noesis.Color.FromArgb(136, 204, 80, 80);

	private static SolidColorBrush teamRedBarColour = new SolidColorBrush(teamRedBarColCol);

	private static Noesis.Color teamBlueBarColCol = Noesis.Color.FromArgb(136, 80, 140, 204);

	private static SolidColorBrush teamBlueBarColour = new SolidColorBrush(teamBlueBarColCol);

	private static Noesis.Color teamGreenBarColCol = Noesis.Color.FromArgb(136, 80, 204, 80);

	private static SolidColorBrush teamGreenBarColour = new SolidColorBrush(teamGreenBarColCol);

	private List<Platform_Multiplayer.MPLobby> lobbies = new List<Platform_Multiplayer.MPLobby>();

	private string defaultMPSettings = "";

	private EngineInterface.MultiplayerSetupData MPDefaultsetupData;

	private EngineInterface.MultiplayerSetupData MPsetupData;

	private EngineInterface.MultiplayerSetupData MPTEMPsetupData;

	public static EngineInterface.MultiplayerSetupData MPLastSetupData = null;

	private Platform_Multiplayer.MPLobby selectedLobby;

	public Platform_Multiplayer.MPLobby currentLobby;

	private FileHeader selectedMPHeader;

	private int selectedCoopMissionID;

	private bool coopOrderSwapped;

	public bool singlePlayerCoop;

	public bool trailMakerMode;

	private ulong singlePlayerCoopAlly;

	private int matchmakingDefault = 1;

	private int numConnectedPlayers = 1;

	private int sortByColumn;

	private bool sortByAscending = true;

	private bool includeUser = true;

	private bool includeBuiltIn = true;

	private bool includeWorkshop = true;

	private bool MPLocalReady;

	private bool MPLocalReadyLocked;

	private bool readyAnimPlaying;

	private int MPTotalPlayers;

	private string MPLastMapName = "";

	private bool MPMapChecked;

	private bool MPMapValid;

	private bool MPGameLoading;

	private bool regetMapListNextTime;

	private bool pendingMPHost;

	private bool skipMapSelectRandomKeeps;

	private DateTime delayedSendDataToLobby = DateTime.MinValue;

	private DateTime nextHostSendPings = DateTime.MinValue;

	private string MPHostLobbyname = "";

	private DateTime multiplayerMapRequestTime = DateTime.MinValue;

	private DateTime lastAutoRefreshTime = DateTime.MinValue;

	private int MPLobbyMode;

	private int MPGameType;

	private int MPStartingSettings;

	private int ExtremeWarningSource;

	public ulong LatestSharedCode;

	private bool ShowSharingCode;

	private DateTime justEnteredSetupScreen = DateTime.MinValue;

	private DateTime lastSettingsRefresh = DateTime.MinValue;

	private int PlayerCap = 8;

	private int[] team_order = new int[9];

	private int SelectedRadarKeep = -1;

	private int SelectedFace = -1;

	private bool teampop_sultan_played;

	private bool teampop_rat_played;

	private bool showLobbyUnavailableMessage;

	private bool justEnteredSetup;

	private bool playKickSpeech = true;

	private int humanPlayerCount = -1;

	private DateTime nextTimeTeamSpeech = DateTime.UtcNow.AddSeconds(5.0);

	public DateTime hideToolTipTime = DateTime.MinValue;

	public bool closePanelDisplayed;

	private bool skirmishExtremeTroopsWarningShown;

	private bool lobbyChatRefreshPending;

	private DateTime lobbyChatRefreshTime = DateTime.MaxValue;

	private PlayerRow[] playerRows = new PlayerRow[8];

	private bool lastCanStart;

	public MPAIVInfo[] AIVs;

	public static readonly int[] MP_orig_remap_colour_order = new int[9] { 0, 1, 3, 4, 2, 6, 5, 7, 8 };

	private readonly string[] KickPlayerSpeech = new string[30]
	{
		"all_kick_player_01.wav", "rt_kick_player.wav", "sn_kick_player.wav", "pg_kick_player.wav", "wf_kick_player.wav", "sa_kick_player_01.wav", "ca_kick_player_01.wav", "su_kick_player_01.wav", "ri_kick_player_01.wav", "fr_kick_player_01.wav",
		"ph_kick_player_01.wav", "wa_kick_player_01.wav", "em_kick_player_01.wav", "ni_kick_player_01.wav", "sh_kick_player_01.wav", "ma_kick_player_01.wav", "ab_kick_player_01.wav", "je_kick_player_01.wav", "se_kick_player_01.wav", "no_kick_player_01.wav",
		"ka_kick_player_01.wav", "cn_kick_player_01.wav", "tr_kick_player_01.wav", "sg_kick_player_01.wav", "li_kick_player_01.wav", "cr_kick_player_01.wav", "ba_kick_player_01.wav", "bu_kick_player_01.wav", "sr_kick_player_01.wav", "sb_kick_player_01.wav"
	};

	private readonly string[] AddPlayerSpeech = new string[30]
	{
		"all_add_player_01.wav", "rt_add_player.wav", "sn_add_player.wav", "pg_add_player.wav", "wf_add_player.wav", "sa_add_player_01.wav", "ca_add_player_01.wav", "su_add_player_01.wav", "ri_add_player_01.wav", "fr_add_player_01.wav",
		"ph_add_player_01.wav", "wa_add_player_01.wav", "em_add_player_01.wav", "ni_add_player_01.wav", "sh_add_player_01.wav", "ma_add_player_01.wav", "ab_add_player_01.wav", "je_add_player_01.wav", "se_add_player_01.wav", "no_add_player_01.wav",
		"ka_add_player_01.wav", "cn_add_player_01.wav", "tr_add_player_01.wav", "sg_add_player_01.wav", "li_add_player_01.wav", "cr_add_player_01.wav", "ba_add_player_01.wav", "bu_add_player_01.wav", "sr_add_player_01.wav", "sb_add_player_01.wav"
	};

	private List<LobbyChatEntry> lobbyChat = new List<LobbyChatEntry>();

	private ListView RefFileLists;

	private CheckBox RefIncludeUser;

	private CheckBox RefIncludeBuiltin;

	private CheckBox RefIncludeWorkshop;

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

	private ObservableCollection<FileRow> fileRows = new ObservableCollection<FileRow>();

	private ObservableCollection<FileRow> lobbyRows = new ObservableCollection<FileRow>();

	private ObservableCollection<FileRow> customLordRows = new ObservableCollection<FileRow>();

	private bool ignoreSelectRefresh;

	private List<FileHeader> headerlist;

	private bool insideValueChanged;

	private DateTime lastScrollTest = DateTime.MinValue;

	private DateTime startGameTime = DateTime.MinValue;

	private DateTime AILordTextClear = DateTime.MinValue;

	private int[,] start_few_troop_level = new int[5, 10]
	{
		{ 2, 0, 2, 0, 0, 0, 0, 0, 0, 0 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
		{ 2, 0, 2, 0, 0, 0, 0, 0, 0, 0 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
		{ 2, 0, 2, 0, 0, 0, 0, 0, 0, 0 }
	};

	private int[,] start_some_troop_level = new int[5, 10]
	{
		{ 3, 0, 3, 0, 0, 0, 0, 0, 0, 0 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
		{ 3, 0, 3, 0, 0, 0, 0, 0, 0, 0 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
		{ 8, 4, 0, 6, 0, 4, 4, 0, 3, 4 }
	};

	private int[,] start_many_troop_level = new int[5, 10]
	{
		{ 6, 0, 6, 0, 0, 0, 0, 0, 1, 0 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
		{ 8, 0, 8, 0, 4, 0, 0, 0, 3, 0 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
		{ 10, 6, 0, 8, 0, 6, 6, 0, 5, 6 }
	};

	private int[,] start_low_goods_level = new int[5, 20]
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

	private int[,] start_med_goods_level = new int[5, 20]
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

	private int[,] start_high_goods_level = new int[5, 20]
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

	private Platform_Multiplayer.MPLobbyMember[] orderTeamMembers = new Platform_Multiplayer.MPLobbyMember[8];

	private Platform_Multiplayer.MPLobbyMember selectedTeamMember;

	private static CoopMissionSetupData[] CoopTrail1 = null;

	private static CoopMissionSetupData[] CoopTrail2 = null;

	private static CoopMissionSetupData[] CoopTrail3 = null;

	private static CoopMissionSetupData[] CoopTrail4 = null;

	private Queue<AvatarCallback> avatarCallbacks = new Queue<AvatarCallback>();

	private int coopFriendsPage;

	private bool coopShowHiddenFriends;

	private ulong coopHiddenSelectedSteamID;

	private const int coopFriendsPageSize = 8;

	private ulong[] coopFriendsSteamIDs = new ulong[8];

	private bool[] coopFriendsRowHidden = new bool[8];

	private Avatars.AvatarDesign tempAD = new Avatars.AvatarDesign();

	public static void SetVisibility(UIElement element, Visibility state)
	{
		if (element.Visibility != state)
		{
			element.Visibility = state;
		}
	}

	public static void SetButtonVisibility(UIElement element, Visibility state)
	{
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
		MainViewModel.Instance.FRONTMultiplayer = this;
		InitializeComponent();
		pulseAnimation = (Storyboard)TryFindResource("ReadyButtonAnim");
		settingsPulseAnimation = (Storyboard)TryFindResource("SettingsButtonAnim");
		RefFileLists = (ListView)FindName("MapList");
		RefLobbyLists = (ListView)FindName("LobbyList");
		RefLobbyMaxPlayersSlider = (Slider)FindName("LobbyMaxPlayersSlider");
		RefLobbyMaxPlayersSlider.ValueChanged += LobbyMaxPlayersSlider_ValueChanged;
		RefHeaderBar = (Noesis.Grid)FindName("HeaderBar");
		RefJoinButton = (Button)FindName("JoinButton");
		RefLobbySettingsButton = (Button)FindName("LobbySettingsButton");
		RefMultiplayerPlayButton = (Button)FindName("MultiplayerPlayButton");
		RefReadyButton = (Button)FindName("ReadyButton");
		RefReadyButtonLock = (Button)FindName("ReadyButtonLock");
		RefLoadButton = (Button)FindName("LoadButton");
		RefColourSelectButton = (Button)FindName("ColourSelectButton");
		RefColShield1 = (Button)FindName("ColShield1");
		RefColShield2 = (Button)FindName("ColShield2");
		RefColShield3 = (Button)FindName("ColShield3");
		RefColShield4 = (Button)FindName("ColShield4");
		RefColShield5 = (Button)FindName("ColShield5");
		RefColShield6 = (Button)FindName("ColShield6");
		RefColShield7 = (Button)FindName("ColShield7");
		RefColShield8 = (Button)FindName("ColShield8");
		RefRandomAI1 = (Button)FindName("RandomAI1");
		RefRandomAI2 = (Button)FindName("RandomAI2");
		RefRandomAI3 = (Button)FindName("RandomAI3");
		RefRandomAI4 = (Button)FindName("RandomAI4");
		RefRandomAI5 = (Button)FindName("RandomAI5");
		RefRandomAI6 = (Button)FindName("RandomAI6");
		RefRandomAI7 = (Button)FindName("RandomAI7");
		RefMultiplayerSetupInfo = (Button)FindName("MultiplayerSetupInfo");
		RefBasemap = (Image)FindName("Basemap");
		RefFairness1 = (RadioButton)FindName("Fairness1");
		RefFairness2 = (RadioButton)FindName("Fairness2");
		RefFairness3 = (RadioButton)FindName("Fairness3");
		RefFairness4 = (RadioButton)FindName("Fairness4");
		RefFairness5 = (RadioButton)FindName("Fairness5");
		RefGameType1 = (RadioButton)FindName("GameType1");
		RefGameType2 = (RadioButton)FindName("GameType2");
		RefGameType3 = (RadioButton)FindName("GameType3");
		RefMultiplayerInvite = (Button)FindName("MultiplayerInvite");
		RefMP_ChatSend = (Button)FindName("MP_ChatSend");
		RefTextBoxGameName = (TextBox)FindName("TextBoxGameName");
		RefTextBoxGameName.IsKeyboardFocusedChanged += TextInputFocus;
		RefMP_ChatInput = (TextBox)FindName("MP_ChatInput");
		RefMP_ChatInput.IsKeyboardFocusedChanged += TextInputFocus;
		RefMP_ChatInput.PreviewKeyUp += DetectChatEnter;
		RefMP_ChatDisplay = (TextBlock)FindName("MP_ChatDisplay");
		RefMP_ChatScrollView = (ScrollViewer)FindName("MP_ChatScrollView");
		RefMP_SearchFilter = (TextBox)FindName("MP_SearchFilter");
		RefMP_SearchFilter.IsKeyboardFocusedChanged += FilterTextInputFocus;
		RefMP_SearchFilter.TextChanged += FilterTextChangedHandler;
		RefMP_SearchFilter.PreviewKeyDown += TextBoxCheckForEscape;
		RefMP_SearchFilter.PreviewTextInput += TextBoxEnterCheck;
		RefMP_EnterShareCodeText = (TextBox)FindName("MP_EnterShareCodeText");
		RefMP_EnterShareCodeText.IsKeyboardFocusedChanged += TextInputFocus;
		RefMP_EnterShareCodeText.TextChanged += EnterShareTextChangedHandler;
		RefShareJoinButton = (Button)FindName("ShareJoinButton");
		for (int i = 0; i < 8; i++)
		{
			playerRows[i] = new PlayerRow();
		}
		playerRows[0].RefRow = (Noesis.Grid)FindName("Player1_Row");
		playerRows[0].RefReadyState = (Image)FindName("Player1_ReadyState");
		playerRows[0].RefColour = (Image)FindName("Player1_Colour");
		playerRows[0].RefName = (TextBlock)FindName("Player1_Name");
		playerRows[0].RefHost = (Image)FindName("Player1_Host");
		playerRows[0].RefType = (TextBlock)FindName("Player1_Type");
		playerRows[0].RefPing = (TextBlock)FindName("Player1_Ping");
		playerRows[0].RefKick = (Button)FindName("Player1_Kick");
		playerRows[0].RefAISettings = (Button)FindName("Player1_AISettings");
		playerRows[1].RefRow = (Noesis.Grid)FindName("Player2_Row");
		playerRows[1].RefReadyState = (Image)FindName("Player2_ReadyState");
		playerRows[1].RefColour = (Image)FindName("Player2_Colour");
		playerRows[1].RefName = (TextBlock)FindName("Player2_Name");
		playerRows[1].RefHost = (Image)FindName("Player2_Host");
		playerRows[1].RefType = (TextBlock)FindName("Player2_Type");
		playerRows[1].RefPing = (TextBlock)FindName("Player2_Ping");
		playerRows[1].RefKick = (Button)FindName("Player2_Kick");
		playerRows[1].RefAISettings = (Button)FindName("Player2_AISettings");
		playerRows[2].RefRow = (Noesis.Grid)FindName("Player3_Row");
		playerRows[2].RefReadyState = (Image)FindName("Player3_ReadyState");
		playerRows[2].RefColour = (Image)FindName("Player3_Colour");
		playerRows[2].RefName = (TextBlock)FindName("Player3_Name");
		playerRows[2].RefHost = (Image)FindName("Player3_Host");
		playerRows[2].RefType = (TextBlock)FindName("Player3_Type");
		playerRows[2].RefPing = (TextBlock)FindName("Player3_Ping");
		playerRows[2].RefKick = (Button)FindName("Player3_Kick");
		playerRows[2].RefAISettings = (Button)FindName("Player3_AISettings");
		playerRows[3].RefRow = (Noesis.Grid)FindName("Player4_Row");
		playerRows[3].RefReadyState = (Image)FindName("Player4_ReadyState");
		playerRows[3].RefColour = (Image)FindName("Player4_Colour");
		playerRows[3].RefName = (TextBlock)FindName("Player4_Name");
		playerRows[3].RefHost = (Image)FindName("Player4_Host");
		playerRows[3].RefType = (TextBlock)FindName("Player4_Type");
		playerRows[3].RefPing = (TextBlock)FindName("Player4_Ping");
		playerRows[3].RefKick = (Button)FindName("Player4_Kick");
		playerRows[3].RefAISettings = (Button)FindName("Player4_AISettings");
		playerRows[4].RefRow = (Noesis.Grid)FindName("Player5_Row");
		playerRows[4].RefReadyState = (Image)FindName("Player5_ReadyState");
		playerRows[4].RefColour = (Image)FindName("Player5_Colour");
		playerRows[4].RefName = (TextBlock)FindName("Player5_Name");
		playerRows[4].RefHost = (Image)FindName("Player5_Host");
		playerRows[4].RefType = (TextBlock)FindName("Player5_Type");
		playerRows[4].RefPing = (TextBlock)FindName("Player5_Ping");
		playerRows[4].RefKick = (Button)FindName("Player5_Kick");
		playerRows[4].RefAISettings = (Button)FindName("Player5_AISettings");
		playerRows[5].RefRow = (Noesis.Grid)FindName("Player6_Row");
		playerRows[5].RefReadyState = (Image)FindName("Player6_ReadyState");
		playerRows[5].RefColour = (Image)FindName("Player6_Colour");
		playerRows[5].RefName = (TextBlock)FindName("Player6_Name");
		playerRows[5].RefHost = (Image)FindName("Player6_Host");
		playerRows[5].RefType = (TextBlock)FindName("Player6_Type");
		playerRows[5].RefPing = (TextBlock)FindName("Player6_Ping");
		playerRows[5].RefKick = (Button)FindName("Player6_Kick");
		playerRows[5].RefAISettings = (Button)FindName("Player6_AISettings");
		playerRows[6].RefRow = (Noesis.Grid)FindName("Player7_Row");
		playerRows[6].RefReadyState = (Image)FindName("Player7_ReadyState");
		playerRows[6].RefColour = (Image)FindName("Player7_Colour");
		playerRows[6].RefName = (TextBlock)FindName("Player7_Name");
		playerRows[6].RefHost = (Image)FindName("Player7_Host");
		playerRows[6].RefType = (TextBlock)FindName("Player7_Type");
		playerRows[6].RefPing = (TextBlock)FindName("Player7_Ping");
		playerRows[6].RefKick = (Button)FindName("Player7_Kick");
		playerRows[6].RefAISettings = (Button)FindName("Player7_AISettings");
		playerRows[7].RefRow = (Noesis.Grid)FindName("Player8_Row");
		playerRows[7].RefReadyState = (Image)FindName("Player8_ReadyState");
		playerRows[7].RefColour = (Image)FindName("Player8_Colour");
		playerRows[7].RefName = (TextBlock)FindName("Player8_Name");
		playerRows[7].RefHost = (Image)FindName("Player8_Host");
		playerRows[7].RefType = (TextBlock)FindName("Player8_Type");
		playerRows[7].RefPing = (TextBlock)FindName("Player8_Ping");
		playerRows[7].RefKick = (Button)FindName("Player8_Kick");
		playerRows[7].RefAISettings = (Button)FindName("Player8_AISettings");
		RefIncludeUser = (CheckBox)FindName("IncludeUser");
		RefIncludeUser.Checked += Include_ValueChanged;
		RefIncludeUser.Unchecked += Include_ValueChanged;
		RefIncludeBuiltin = (CheckBox)FindName("IncludeBuiltin");
		RefIncludeBuiltin.Checked += Include_ValueChanged;
		RefIncludeBuiltin.Unchecked += Include_ValueChanged;
		RefIncludeWorkshop = (CheckBox)FindName("IncludeWorkshop");
		RefIncludeWorkshop.Checked += Include_ValueChanged;
		RefIncludeWorkshop.Unchecked += Include_ValueChanged;
		RefRadarShieldTeam1 = (Image)FindName("RadarShieldTeam1");
		RefRadarShieldTeam2 = (Image)FindName("RadarShieldTeam2");
		RefRadarShieldTeam3 = (Image)FindName("RadarShieldTeam3");
		RefRadarShieldTeam4 = (Image)FindName("RadarShieldTeam4");
		RefRadarShieldTeam5 = (Image)FindName("RadarShieldTeam5");
		RefRadarShieldTeam6 = (Image)FindName("RadarShieldTeam6");
		RefRadarShieldTeam7 = (Image)FindName("RadarShieldTeam7");
		RefRadarShieldTeam8 = (Image)FindName("RadarShieldTeam8");
		RefRadarShield1 = (Button)FindName("RadarShield1");
		RefRadarShield1.PreviewMouseDown += RadarShield1_Click;
		RefRadarShield1.PreviewMouseUp += RadarShield_Up;
		RefRadarShield2 = (Button)FindName("RadarShield2");
		RefRadarShield2.PreviewMouseDown += RadarShield2_Click;
		RefRadarShield2.PreviewMouseUp += RadarShield_Up;
		RefRadarShield3 = (Button)FindName("RadarShield3");
		RefRadarShield3.PreviewMouseDown += RadarShield3_Click;
		RefRadarShield3.PreviewMouseUp += RadarShield_Up;
		RefRadarShield4 = (Button)FindName("RadarShield4");
		RefRadarShield4.PreviewMouseDown += RadarShield4_Click;
		RefRadarShield4.PreviewMouseUp += RadarShield_Up;
		RefRadarShield5 = (Button)FindName("RadarShield5");
		RefRadarShield5.PreviewMouseDown += RadarShield5_Click;
		RefRadarShield5.PreviewMouseUp += RadarShield_Up;
		RefRadarShield6 = (Button)FindName("RadarShield6");
		RefRadarShield6.PreviewMouseDown += RadarShield6_Click;
		RefRadarShield6.PreviewMouseUp += RadarShield_Up;
		RefRadarShield7 = (Button)FindName("RadarShield7");
		RefRadarShield7.PreviewMouseDown += RadarShield7_Click;
		RefRadarShield7.PreviewMouseUp += RadarShield_Up;
		RefRadarShield8 = (Button)FindName("RadarShield8");
		RefRadarShield8.PreviewMouseDown += RadarShield8_Click;
		RefRadarShield8.PreviewMouseUp += RadarShield_Up;
		RefRadarShieldFace1 = (Noesis.Grid)FindName("RadarShieldFace1");
		RefRadarShieldFace2 = (Noesis.Grid)FindName("RadarShieldFace2");
		RefRadarShieldFace3 = (Noesis.Grid)FindName("RadarShieldFace3");
		RefRadarShieldFace4 = (Noesis.Grid)FindName("RadarShieldFace4");
		RefRadarShieldFace5 = (Noesis.Grid)FindName("RadarShieldFace5");
		RefRadarShieldFace6 = (Noesis.Grid)FindName("RadarShieldFace6");
		RefRadarShieldFace7 = (Noesis.Grid)FindName("RadarShieldFace7");
		RefRadarShieldFace8 = (Noesis.Grid)FindName("RadarShieldFace8");
		RefTeamFace1 = (Button)FindName("TeamFace1");
		RefTeamFace1.PreviewMouseDown += TeamFace1_Click;
		RefTeamFace1.PreviewMouseUp += TeamFace_Up;
		RefTeamFace2 = (Button)FindName("TeamFace2");
		RefTeamFace2.PreviewMouseDown += TeamFace2_Click;
		RefTeamFace2.PreviewMouseUp += TeamFace_Up;
		RefTeamFace3 = (Button)FindName("TeamFace3");
		RefTeamFace3.PreviewMouseDown += TeamFace3_Click;
		RefTeamFace3.PreviewMouseUp += TeamFace_Up;
		RefTeamFace4 = (Button)FindName("TeamFace4");
		RefTeamFace4.PreviewMouseDown += TeamFace4_Click;
		RefTeamFace4.PreviewMouseUp += TeamFace_Up;
		RefTeamFace5 = (Button)FindName("TeamFace5");
		RefTeamFace5.PreviewMouseDown += TeamFace5_Click;
		RefTeamFace5.PreviewMouseUp += TeamFace_Up;
		RefTeamFace6 = (Button)FindName("TeamFace6");
		RefTeamFace6.PreviewMouseDown += TeamFace6_Click;
		RefTeamFace6.PreviewMouseUp += TeamFace_Up;
		RefTeamFace7 = (Button)FindName("TeamFace7");
		RefTeamFace7.PreviewMouseDown += TeamFace7_Click;
		RefTeamFace7.PreviewMouseUp += TeamFace_Up;
		RefTeamFace8 = (Button)FindName("TeamFace8");
		RefTeamFace8.PreviewMouseDown += TeamFace8_Click;
		RefTeamFace8.PreviewMouseUp += TeamFace_Up;
		RefTeamFaceCancel = (Button)FindName("TeamFaceCancel");
		RefTeamFaceCancel.PreviewMouseDown += TeamFaceCancel_Click;
		RefTeamFaceCancel.PreviewMouseUp += TeamFace_Up;
		RefFloatingRadarShield = (Image)FindName("FloatingRadarShield");
		RefFloatingTeams = (Noesis.Grid)FindName("FloatingTeams");
		RefSkirmish_RadarMask = (Noesis.Grid)FindName("Skirmish_RadarMask");
		RefSkirmish_RadarMask.MouseDown += SkirmishRadar_OffClick;
		RefMap_Balanced = (TextBlock)FindName("Map_Balanced");
		RefMap_UnBalanced = (TextBlock)FindName("Map_UnBalanced");
		RefExtremeWarningCheck = (CheckBox)FindName("ExtremeWarningCheck");
		RefEnableAdvancedSkirmishCheck = (CheckBox)FindName("EnableAdvancedSkirmishCheck");
		RefEnableAdvancedSkirmishCheck.Checked += EnableAdvancedSkirmishCheck_ValueChanged;
		RefEnableAdvancedSkirmishCheck.Unchecked += EnableAdvancedSkirmishCheck_ValueChanged;
		RefChatMuteDisable = (CheckBox)FindName("ChatMuteDisable");
		RefChatMuteDisable.Checked += MuteMPChat_ValueChanged;
		RefChatMuteDisable.Unchecked += MuteMPChat_ValueChanged;
		RefMapSizeMin_Slider = (Slider)FindName("MapSizeMin_Slider");
		RefMapSizeMax_Slider = (Slider)FindName("MapSizeMax_Slider");
		RefMapSizeMin_Slider.ValueChanged += MapSizeMin_Slider_ValueChanged;
		RefMapSizeMax_Slider.ValueChanged += MapSizeMax_Slider_ValueChanged;
		RefAIMin_Slider = (Slider)FindName("AIMin_Slider");
		RefAIMax_Slider = (Slider)FindName("AIMax_Slider");
		RefAIMin_Slider.ValueChanged += AIMin_Slider_ValueChanged;
		RefAIMax_Slider.ValueChanged += AIMax_Slider_ValueChanged;
		RefRandomTeams = (CheckBox)FindName("RandomTeams");
		RefRandomBalance = (CheckBox)FindName("RandomBalance");
		RefRandomOutposts = (CheckBox)FindName("RandomOutposts");
		RefRandomExtreme = (CheckBox)FindName("RandomExtreme");
		RefRandomAdvanced = (CheckBox)FindName("RandomAdvanced");
		RefRandomIncludeUser = (CheckBox)FindName("RandomIncludeUser");
		RefRandomIncludeBuiltin = (CheckBox)FindName("RandomIncludeBuiltin");
		RefRandomIncludeWorkshop = (CheckBox)FindName("RandomIncludeWorkshop");
		RefCustomLordList = (ListView)FindName("CustomLordList");
		RefCustomLordList.MouseDoubleClick += delegate
		{
			ButtonClicked("AddCustomLord");
		};
		RefTrailMakerTest = (Button)FindName("TrailMakerTest");
		GridView obj = (GridView)RefFileLists.View;
		GridViewColumnHeader obj2 = (GridViewColumnHeader)obj.Columns[4].Header;
		obj2.Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 27);
		obj2.Click += FileListHeaderClickedHandler;
		GridViewColumnHeader obj3 = (GridViewColumnHeader)obj.Columns[5].Header;
		obj3.Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 28);
		obj3.Click += FileListHeaderClickedHandler;
		GridViewColumnHeader obj4 = (GridViewColumnHeader)obj.Columns[0].Header;
		obj4.Content = "";
		obj4.Click += FileListHeaderClickedHandler;
		GridViewColumnHeader obj5 = (GridViewColumnHeader)obj.Columns[1].Header;
		obj5.Content = "#";
		obj5.Click += FileListHeaderClickedHandler;
		GridViewColumnHeader obj6 = (GridViewColumnHeader)obj.Columns[2].Header;
		obj6.Content = "";
		obj6.Click += FileListHeaderClickedHandler;
		GridViewColumnHeader obj7 = (GridViewColumnHeader)obj.Columns[3].Header;
		obj7.Content = "";
		obj7.Click += FileListHeaderClickedHandler;
		RefFileLists.SelectionChanged += delegate
		{
			if (RefFileLists.SelectedItem != null)
			{
				if (sortByColumn < 10 || sortByColumn > 16)
				{
					RefFileLists.ScrollIntoView(RefFileLists.SelectedItem);
				}
				if (skirmishGame || (currentLobby != null && currentLobby.isHost))
				{
					FileHeader fileHeader = ((FileRow)RefFileLists.SelectedItem).fileHeader;
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
		RefFileLists.Loaded += delegate
		{
			if (RefFileLists.SelectedItem != null)
			{
				RefFileLists.ScrollIntoView(RefFileLists.SelectedItem);
			}
		};
		GridView obj8 = (GridView)RefLobbyLists.View;
		GridViewColumnHeader obj9 = (GridViewColumnHeader)obj8.Columns[0].Header;
		obj9.Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 48);
		obj9.Click += LobbyListHeaderClickedHandler;
		GridViewColumnHeader obj10 = (GridViewColumnHeader)obj8.Columns[1].Header;
		obj10.Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 49);
		obj10.Click += LobbyListHeaderClickedHandler;
		GridViewColumnHeader obj11 = (GridViewColumnHeader)obj8.Columns[2].Header;
		obj11.Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 50);
		obj11.Click += LobbyListHeaderClickedHandler;
		RefLobbyLists.SelectionChanged += delegate
		{
			if (RefLobbyLists.SelectedItem != null)
			{
				selectedLobby = ((FileRow)RefLobbyLists.SelectedItem).lobby;
				Button refLobbySettingsButton = RefLobbySettingsButton;
				bool isEnabled = (RefJoinButton.IsEnabled = true);
				refLobbySettingsButton.IsEnabled = isEnabled;
				UpdateLobbySettingsButton();
			}
		};
		RefLobbyLists.MouseDoubleClick += delegate
		{
			if (RefLobbyLists.SelectedItem != null)
			{
				selectedLobby = ((FileRow)RefLobbyLists.SelectedItem).lobby;
				Button refLobbySettingsButton = RefLobbySettingsButton;
				bool isEnabled = (RefJoinButton.IsEnabled = true);
				refLobbySettingsButton.IsEnabled = isEnabled;
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
			RefMap_Balanced.TextWrapping = TextWrapping.Wrap;
			RefMap_UnBalanced.FontSize = 14f;
			RefMap_UnBalanced.TextWrapping = TextWrapping.Wrap;
			RefMap_Balanced.Margin = new Thickness(93f, 67f, 0f, 0f);
			RefMap_UnBalanced.Margin = new Thickness(93f, 67f, 0f, 0f);
		}
		if (FatControler.korean)
		{
			MainViewModel.Instance.MP_AI_Info_Margin = "10,1,0,0";
		}
		if (FatControler.japanese)
		{
			PropEx.SetGlowButtonFontSize(RefMultiplayerSetupInfo, 14);
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
				RefHeaderBar.Width = 545f;
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
				else if (FrontendMenus.CurrentSelectedTrail == 24)
				{
					MainViewModel.Instance.Show_CoopTrail4 = coopGame;
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
				RefLobbyMaxPlayersSlider.IsEnabled = true;
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
				RefChatMuteDisable.IsChecked = ConfigSettings.Settings_MuteMPChat;
				RefMP_ChatSend.IsEnabled = !ConfigSettings.Settings_MuteMPChat;
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
				RefIncludeBuiltin.IsChecked = true;
				RefIncludeUser.IsChecked = true;
				RefIncludeWorkshop.IsChecked = true;
				RefMultiplayerInvite.IsEnabled = true;
				ignoreSelectRefresh = false;
				RefMapSizeMax_Slider.Value = 7f;
				RefMapSizeMin_Slider.Value = 0f;
				RefAIMax_Slider.Value = 7f;
				RefAIMin_Slider.Value = 1f;
				RefRandomTeams.IsChecked = true;
				RefRandomBalance.IsChecked = true;
				RefRandomOutposts.IsChecked = false;
				RefRandomExtreme.IsChecked = false;
				RefRandomAdvanced.IsChecked = false;
				RefRandomIncludeUser.IsChecked = true;
				RefRandomIncludeBuiltin.IsChecked = true;
				RefRandomIncludeWorkshop.IsChecked = true;
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
					FRONT_CoopTrail4.Instance.playerRows[k].Clear();
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
				MainViewModel.Instance.MultiplayerFilterLabelVis = Visibility.Visible;
				MainViewModel.Instance.MultiplayerFilterButtonVis = Visibility.Hidden;
				MainViewModel.Instance.MultiplayerEnterShareCode = "";
				RefShareJoinButton.IsEnabled = false;
				FRONT_CoopTrail1.Instance.RefShareJoinButton.IsEnabled = false;
				FRONT_CoopTrail2.Instance.RefShareJoinButton.IsEnabled = false;
				FRONT_CoopTrail3.Instance.RefShareJoinButton.IsEnabled = false;
				FRONT_CoopTrail4.Instance.RefShareJoinButton.IsEnabled = false;
				LatestSharedCode = 0uL;
				pendingMPHost = false;
				MPMapChecked = false;
				MPMapValid = false;
				MPGameLoading = false;
				regetMapListNextTime = false;
				MPLocalReady = false;
				MPLocalReadyLocked = false;
				RefReadyButtonLock.Visibility = Visibility.Hidden;
				FRONT_CoopTrail1.Instance.RefReadyButtonLock.Visibility = Visibility.Hidden;
				FRONT_CoopTrail2.Instance.RefReadyButtonLock.Visibility = Visibility.Hidden;
				FRONT_CoopTrail3.Instance.RefReadyButtonLock.Visibility = Visibility.Hidden;
				FRONT_CoopTrail4.Instance.RefReadyButtonLock.Visibility = Visibility.Hidden;
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
						else if (FrontendMenus.CurrentSelectedTrail == 24)
						{
							coopTrailID = 3;
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
								FRONT_CoopTrail1.Instance.RefShowHidden.Visibility = Visibility.Hidden;
								FRONT_CoopTrail2.Instance.RefShowHidden.Visibility = Visibility.Hidden;
								FRONT_CoopTrail3.Instance.RefShowHidden.Visibility = Visibility.Hidden;
								FRONT_CoopTrail4.Instance.RefShowHidden.Visibility = Visibility.Hidden;
							}
							else
							{
								FRONT_CoopTrail1.Instance.RefShowHidden.Visibility = Visibility.Visible;
								FRONT_CoopTrail2.Instance.RefShowHidden.Visibility = Visibility.Visible;
								FRONT_CoopTrail3.Instance.RefShowHidden.Visibility = Visibility.Visible;
								FRONT_CoopTrail4.Instance.RefShowHidden.Visibility = Visibility.Visible;
							}
							FRONT_CoopTrail1.Instance.RefShowHidden.IsChecked = false;
							FRONT_CoopTrail2.Instance.RefShowHidden.IsChecked = false;
							FRONT_CoopTrail3.Instance.RefShowHidden.IsChecked = false;
							FRONT_CoopTrail4.Instance.RefShowHidden.IsChecked = false;
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
									FRONT_CoopTrail1.Instance.RefShowHidden.Visibility = Visibility.Hidden;
									FRONT_CoopTrail2.Instance.RefShowHidden.Visibility = Visibility.Hidden;
									FRONT_CoopTrail3.Instance.RefShowHidden.Visibility = Visibility.Hidden;
									FRONT_CoopTrail4.Instance.RefShowHidden.Visibility = Visibility.Hidden;
								}
								else
								{
									FRONT_CoopTrail1.Instance.RefShowHidden.Visibility = Visibility.Visible;
									FRONT_CoopTrail2.Instance.RefShowHidden.Visibility = Visibility.Visible;
									FRONT_CoopTrail3.Instance.RefShowHidden.Visibility = Visibility.Visible;
									FRONT_CoopTrail4.Instance.RefShowHidden.Visibility = Visibility.Visible;
								}
								FRONT_CoopTrail1.Instance.RefShowHidden.IsChecked = false;
								FRONT_CoopTrail2.Instance.RefShowHidden.IsChecked = false;
								FRONT_CoopTrail3.Instance.RefShowHidden.IsChecked = false;
								FRONT_CoopTrail4.Instance.RefShowHidden.IsChecked = false;
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
				bool isEnabled = (RefJoinButton.IsEnabled = false);
				refLobbySettingsButton.IsEnabled = isEnabled;
				RefLobbySettingsButton.Visibility = Visibility.Hidden;
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
				RefMultiplayerPlayButton.Visibility = Visibility.Visible;
			}
			RefFloatingRadarShield.Source = null;
			SelectedRadarKeep = -1;
			SelectedFace = -1;
			RefTeamFaceCancel.IsEnabled = false;
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

	private void FileListHeaderClickedHandler(object sender, RoutedEventArgs e)
	{
		switch (((GridViewColumnHeader)e.Source).Tag as string)
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

	private void populateMapList(FileHeader selectedHeader = null, bool ignoreRefresh = false)
	{
		includeBuiltIn = RefIncludeBuiltin.IsChecked.Value;
		includeUser = RefIncludeUser.IsChecked.Value;
		includeWorkshop = RefIncludeWorkshop.IsChecked.Value;
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
		RefFileLists.ItemsSource = fileRows;
		if (fileRow != null)
		{
			if (ignoreRefresh)
			{
				ignoreSelectRefresh = true;
			}
			RefFileLists.SelectedItem = fileRow;
			ignoreSelectRefresh = false;
		}
	}

	private void populateLobbyList()
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
		RefLobbyLists.ItemsSource = lobbyRows;
		if (fileRow != null)
		{
			RefLobbyLists.SelectedItem = fileRow;
			return;
		}
		Button refLobbySettingsButton = RefLobbySettingsButton;
		bool isEnabled = (RefJoinButton.IsEnabled = false);
		refLobbySettingsButton.IsEnabled = isEnabled;
		RefLobbySettingsButton.Visibility = Visibility.Hidden;
	}

	private void LobbyListHeaderClickedHandler(object sender, RoutedEventArgs e)
	{
		switch (((GridViewColumnHeader)e.Source).Tag as string)
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

	private void updateRadarTexture(FileHeader header)
	{
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
				MainViewModel.Instance.RadarStandaloneImage = radarStandaloneImage;
			}
		}
		else
		{
			MainViewModel.Instance.Show_MPRadar = false;
		}
	}

	private void Include_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			populateMapList(selectedMPHeader, ignoreRefresh: true);
		}
	}

	private void CreateRandomSkirmish()
	{
		int minSize = 160;
		int maxSize = 160;
		float value = RefMapSizeMin_Slider.Value;
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
		value = RefMapSizeMax_Slider.Value;
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
		int num = (int)RefAIMin_Slider.Value;
		if (RefRandomIncludeBuiltin.IsChecked.Value && !RefIncludeBuiltin.IsChecked.Value)
		{
			RefIncludeBuiltin.IsChecked = true;
		}
		if (RefRandomIncludeUser.IsChecked.Value && !RefIncludeUser.IsChecked.Value)
		{
			RefIncludeUser.IsChecked = true;
		}
		if (RefRandomIncludeWorkshop.IsChecked.Value && !RefIncludeWorkshop.IsChecked.Value)
		{
			RefIncludeWorkshop.IsChecked = true;
		}
		FileHeader randomMultiplayerMap = MapFileManager.Instance.GetRandomMultiplayerMap(num + 1, minSize, maxSize, RefRandomIncludeBuiltin.IsChecked.Value, RefRandomIncludeUser.IsChecked.Value, RefRandomIncludeWorkshop.IsChecked.Value);
		if (randomMultiplayerMap == null)
		{
			return;
		}
		currentLobby.maxPlayers = randomMultiplayerMap.maxPlayers.ToString();
		populateMapList(randomMultiplayerMap);
		System.Random random = new System.Random();
		int num2 = (int)RefAIMax_Slider.Value;
		int num3 = random.Next(num, num2 + 1);
		if (spectatorMode)
		{
			num3++;
		}
		SkirmishAIAddClick((-num3).ToString());
		if (RefRandomBalance.IsChecked.Value)
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
		if (RefRandomExtreme.IsChecked.Value)
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
		if (RefRandomOutposts.IsChecked.Value)
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
		if (RefRandomAdvanced.IsChecked.Value)
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
			MPsetupData.global_improved_sieging2 = random.Next(2);
			if (MPsetupData.global_improved_sieging2 > 0)
			{
				MainViewModel.Instance.MP_Settings_ImprovedSiegingX = MainViewModel.Instance.GameSprites[640];
			}
			else
			{
				MainViewModel.Instance.MP_Settings_ImprovedSiegingX = MainViewModel.Instance.GameSprites[641];
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
		if (RefRandomTeams.IsChecked.Value)
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

	private void updateRandomSkirmishPanel()
	{
		float value = RefMapSizeMin_Slider.Value;
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
		value = RefMapSizeMax_Slider.Value;
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
		MainViewModel.Instance.MP_RandomAIMin = ((int)RefAIMin_Slider.Value + num).ToString();
		MainViewModel.Instance.MP_RandomAIMax = ((int)RefAIMax_Slider.Value + num).ToString();
	}

	private void MapSizeMax_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)RefMapSizeMax_Slider.Value;
		if (!insideValueChanged)
		{
			insideValueChanged = true;
			if (num < (int)RefMapSizeMin_Slider.Value)
			{
				RefMapSizeMin_Slider.Value = num;
			}
			insideValueChanged = false;
		}
		updateRandomSkirmishPanel();
	}

	private void MapSizeMin_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)RefMapSizeMin_Slider.Value;
		if (!insideValueChanged)
		{
			insideValueChanged = true;
			if (num > (int)RefMapSizeMax_Slider.Value)
			{
				RefMapSizeMax_Slider.Value = num;
			}
			insideValueChanged = false;
		}
		updateRandomSkirmishPanel();
	}

	private void AIMax_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)RefAIMax_Slider.Value;
		if (!insideValueChanged)
		{
			insideValueChanged = true;
			if (num < (int)RefAIMin_Slider.Value)
			{
				RefAIMin_Slider.Value = num;
			}
			insideValueChanged = false;
		}
		updateRandomSkirmishPanel();
	}

	private void AIMin_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		int num = (int)RefAIMin_Slider.Value;
		if (!insideValueChanged)
		{
			insideValueChanged = true;
			if (num > (int)RefAIMax_Slider.Value)
			{
				RefAIMax_Slider.Value = num;
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
				RefTeamFaceCancel.IsEnabled = false;
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
						case 18:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands8");
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
			RefLobbyMaxPlayersSlider.Value = 8f;
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
			bool flag4 = false;
			if (selectedLobby != null)
			{
				string settings = selectedLobby.settings;
				MPTEMPsetupData = new EngineInterface.MultiplayerSetupData();
				MPTEMPsetupData.FromString(settings);
				if (MPTEMPsetupData.extreme_troops > 0)
				{
					flag4 = true;
				}
			}
			if (!skirmishExtremeTroopsWarningShown && ConfigSettings.Settings_Show_Extreme_Warning && flag4)
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
			RefLobbyMaxPlayersSlider.IsEnabled = MPGameType < 2;
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
			int num2 = param[param.Length - 1] - 49;
			num2 = playerRows[num2].playerID;
			Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID3 = currentLobby.GetLobbyMemberFromThis_PlayerID(num2);
			if (lobbyMemberFromThis_PlayerID3 == null)
			{
				break;
			}
			bool flag2 = false;
			if (!skirmishGame)
			{
				if (currentLobby.isHost && !lobbyMemberFromThis_PlayerID3.IsSelf())
				{
					if (!lobbyMemberFromThis_PlayerID3.SkirmishMember)
					{
						Platform_Multiplayer.Instance.KickMemberFromLobby(lobbyMemberFromThis_PlayerID3);
					}
					else
					{
						flag2 = true;
					}
				}
			}
			else
			{
				flag2 = true;
			}
			if (!flag2)
			{
				break;
			}
			if (lobbyMemberFromThis_PlayerID3.SkirmishMember && !lobbyMemberFromThis_PlayerID3.SkirmishHumanMember && playKickSpeech && !MyAudioManager.Instance.isSpeechPlaying(3))
			{
				int num3 = lobbyMemberFromThis_PlayerID3.GetLordType() + 1;
				if (num3 < KickPlayerSpeech.Length)
				{
					SFXManager.instance.playGenieSpeech(3, KickPlayerSpeech[num3], 1f);
				}
				else if (CustomisationFileManager.CustomMediaExists && !MyAudioManager.Instance.isSpeechPlaying(3))
				{
					string path = MapFileManager.SplitCustomTrailName(lobbyMemberFromThis_PlayerID3.customLordName);
					string text3 = System.IO.Path.Combine(ConfigSettings.GetUserCustomMediaPath(), path, "KICK_PLAYER.wav");
					if (File.Exists(text3))
					{
						MyAudioManager.Instance.PlaySpeech(3, "*", text3, force: true);
					}
				}
			}
			Platform_Multiplayer.Instance.kickSkirmishPlayer(lobbyMemberFromThis_PlayerID3.id.m_SteamID);
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
				int num28 = param[param.Length - 1] - 49;
				if (SelectedRadarKeep >= 0 && SelectedRadarKeep != num28)
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
			int num24 = param[param.Length - 1] - 49;
			if (SelectedRadarKeep < 0)
			{
				SelectedRadarKeep = num24;
				MainViewModel.Instance.Show_SkirmishUIOnRadar = true;
			}
			else
			{
				if (SelectedRadarKeep != num24)
				{
					int num25 = MPsetupData.start_keep_location_order[SelectedRadarKeep];
					MPsetupData.start_keep_location_order[SelectedRadarKeep] = MPsetupData.start_keep_location_order[num24];
					MPsetupData.start_keep_location_order[num24] = num25;
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
				int num20 = param[param.Length - 1] - 49;
				if (SelectedFace >= 0 && SelectedFace != num20)
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
			int num = param[param.Length - 1] - 49;
			if (SelectedFace < 0)
			{
				if (orderTeamMembers[num] != null)
				{
					selectedTeamMember = orderTeamMembers[num];
					RefTeamFaceCancel.IsEnabled = true;
					SelectedFace = num;
					switch (num)
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
			if (SelectedFace != num && orderTeamMembers[num] != null)
			{
				int team = currentLobby.getTeam(orderTeamMembers[num]);
				currentLobby.setTeam(selectedTeamMember, team);
				UpdateHostInfo();
				bool flag = false;
				if (skirmishGame)
				{
					if (selectedTeamMember.GetLordType() == 0 && !teampop_rat_played)
					{
						for (int i = 1; i < 9; i++)
						{
							Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID = currentLobby.GetLobbyMemberFromThis_PlayerID(i);
							if (lobbyMemberFromThis_PlayerID != null && lobbyMemberFromThis_PlayerID.IsSelf() && currentLobby.getTeam(lobbyMemberFromThis_PlayerID) == team)
							{
								SFXManager.instance.playGenieSpeech(3, "Genie_13.wav", 1f);
								teampop_rat_played = true;
								flag = true;
							}
						}
					}
					if (selectedTeamMember.GetLordType() == 6 && !teampop_sultan_played)
					{
						for (int j = 1; j < 9; j++)
						{
							Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID2 = currentLobby.GetLobbyMemberFromThis_PlayerID(j);
							if (lobbyMemberFromThis_PlayerID2 != null && lobbyMemberFromThis_PlayerID2.IsSelf() && currentLobby.getTeam(lobbyMemberFromThis_PlayerID2) == team)
							{
								SFXManager.instance.playGenieSpeech(3, "Genie_14.wav", 1f);
								teampop_sultan_played = true;
								flag = true;
							}
						}
					}
				}
				if (!flag && !MyAudioManager.Instance.isSpeechPlaying(3) && DateTime.UtcNow > nextTimeTeamSpeech)
				{
					nextTimeTeamSpeech = DateTime.UtcNow.AddSeconds(5.0);
					switch (new System.Random().Next(7))
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
			RefTeamFaceCancel.IsEnabled = false;
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
				RefTeamFaceCancel.IsEnabled = false;
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
				if (FRONT_CoopTrail4.Instance.RefMP_ChatInput.Text.Length > 0)
				{
					Platform_Multiplayer.Instance.SendLobbyChatMessage(FRONT_CoopTrail4.Instance.RefMP_ChatInput.Text);
					FRONT_CoopTrail4.Instance.RefMP_ChatInput.Text = "";
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
			bool flag5 = false;
			if (currentLobby != null && currentLobby.isHost)
			{
				flag5 = true;
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
				FRONT_Multiplayer_Setup.Instance.RefMP_UsePrevious.IsEnabled = flag5 && MPLastSetupData != null;
				FRONT_Multiplayer_Setup.Instance.RefMP_UsePresets1.IsEnabled = flag5 && ConfigSettings.Settings_MPPresets1.Length > 0;
				FRONT_Multiplayer_Setup.Instance.RefMP_UsePresets2.IsEnabled = flag5 && ConfigSettings.Settings_MPPresets2.Length > 0;
			}
			else
			{
				if (selectedLobby == null)
				{
					RefLobbySettingsButton.Visibility = Visibility.Hidden;
					break;
				}
				flag5 = false;
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
			ImportSettings(settings2, flag5);
			if (!skirmishGame && flag5)
			{
				MainViewModel.Instance.MPSettingHeight = "640";
			}
			else if (skirmishGame || flag5 || MPTEMPsetupData.advanced_options > 0)
			{
				MainViewModel.Instance.MPSettingHeight = "560";
			}
			else
			{
				MainViewModel.Instance.MPSettingHeight = "530";
			}
			MainViewModel.Instance.Show_MPOnlySettings = !skirmishGame;
			MainViewModel.Instance.Show_MPSettings_MaxPlayers = !skirmishGame && flag5 && !customCoopGame;
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
			string text5 = MPDefaultsetupData.ToString();
			MPsetupData.FromString(text5, ignoreKeepOrder: true);
			ImportSettings(text5, isHost: true);
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
			FRONT_Multiplayer_Setup.Instance.RefMP_UsePresets1.IsEnabled = true;
			break;
		case "SavePresets2":
			ConfigSettings.Settings_MPPresets2 = MPTEMPsetupData.ToString();
			ConfigSettings.SaveSettings();
			FRONT_Multiplayer_Setup.Instance.RefMP_UsePresets2.IsEnabled = true;
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
		case "Settings_ImprovedSiegingX_Enter":
			MainViewModel.Instance.MPGame_Type_Description = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 548);
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
				for (int num15 = 0; num15 < 8; num15++)
				{
					MPTEMPsetupData.MP_BuildingsAvailable[num15] = 1;
					MainViewModel.Instance.MPSetupBuildingsBool[num15] = MPTEMPsetupData.MP_BuildingsAvailable[num15] != 0;
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
				for (int num16 = 0; num16 < 32; num16++)
				{
					MPTEMPsetupData.MP_TroopsAvailable[num16] = 1;
					MainViewModel.Instance.MPSetupTroopsBool[num16] = MPTEMPsetupData.MP_TroopsAvailable[num16] != 0;
				}
				for (int num17 = 8; num17 < 10; num17++)
				{
					MPTEMPsetupData.MP_BuildingsAvailable[num17] = 1;
					MainViewModel.Instance.MPSetupBuildingsBool[num17] = MPTEMPsetupData.MP_BuildingsAvailable[num17] != 0;
				}
			}
			if (MainViewModel.Instance.Show_MPSettings_AdvancedTrading)
			{
				for (int num18 = 0; num18 < 25; num18++)
				{
					MPTEMPsetupData.MP_GoodsAvailable[num18] = 1;
					MainViewModel.Instance.TradingGoodsBool[num18] = MPTEMPsetupData.MP_GoodsAvailable[num18] != 0;
				}
			}
			if (MainViewModel.Instance.Show_MPSettings_AdvancedOptions)
			{
				MPTEMPsetupData.advopt_pre_build = 1;
				MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[640];
				MPTEMPsetupData.global_improved_sieging = 1;
				MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[640];
				MPTEMPsetupData.global_improved_sieging2 = 1;
				MainViewModel.Instance.MP_Settings_ImprovedSiegingX = MainViewModel.Instance.GameSprites[640];
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
				for (int num11 = 0; num11 < 8; num11++)
				{
					MPTEMPsetupData.MP_BuildingsAvailable[num11] = 0;
					MainViewModel.Instance.MPSetupBuildingsBool[num11] = MPTEMPsetupData.MP_BuildingsAvailable[num11] != 0;
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
				for (int num12 = 0; num12 < 32; num12++)
				{
					MPTEMPsetupData.MP_TroopsAvailable[num12] = 0;
					MainViewModel.Instance.MPSetupTroopsBool[num12] = MPTEMPsetupData.MP_TroopsAvailable[num12] != 0;
				}
				for (int num13 = 8; num13 < 10; num13++)
				{
					MPTEMPsetupData.MP_BuildingsAvailable[num13] = 0;
					MainViewModel.Instance.MPSetupBuildingsBool[num13] = MPTEMPsetupData.MP_BuildingsAvailable[num13] != 0;
				}
			}
			if (MainViewModel.Instance.Show_MPSettings_AdvancedTrading)
			{
				for (int num14 = 0; num14 < 25; num14++)
				{
					MPTEMPsetupData.MP_GoodsAvailable[num14] = 0;
					MainViewModel.Instance.TradingGoodsBool[num14] = MPTEMPsetupData.MP_GoodsAvailable[num14] != 0;
				}
			}
			if (MainViewModel.Instance.Show_MPSettings_AdvancedOptions)
			{
				MPTEMPsetupData.advopt_pre_build = 0;
				MainViewModel.Instance.MP_Settings_PreBuild = MainViewModel.Instance.GameSprites[641];
				MPTEMPsetupData.global_improved_sieging = 0;
				MainViewModel.Instance.MP_Settings_ImprovedSieging = MainViewModel.Instance.GameSprites[641];
				MPTEMPsetupData.global_improved_sieging2 = 0;
				MainViewModel.Instance.MP_Settings_ImprovedSiegingX = MainViewModel.Instance.GameSprites[641];
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
			int num9 = int.Parse(param.Substring(6));
			if (MPTEMPsetupData.MP_GoodsAvailable[num9] != 0)
			{
				MPTEMPsetupData.MP_GoodsAvailable[num9] = 0;
			}
			else
			{
				MPTEMPsetupData.MP_GoodsAvailable[num9] = 1;
			}
			for (int num10 = 0; num10 < 25; num10++)
			{
				MainViewModel.Instance.TradingGoodsBool[num10] = MPTEMPsetupData.MP_GoodsAvailable[num10] != 0;
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
			int num7 = 0;
			switch (param)
			{
			case "STRUCT_BARRACKS_STONE":
				num7 = 0;
				break;
			case "STRUCT_BARRACKS_WOOD":
				num7 = 1;
				break;
			case "STRUCT_BEDOUIN_STOCKADE":
				num7 = 2;
				break;
			case "STRUCT_CATTLEFARM":
				num7 = 3;
				break;
			case "STRUCT_APPLEFARM":
				num7 = 4;
				break;
			case "STRUCT_WHEATFARM":
				num7 = 5;
				break;
			case "STRUCT_HOPSFARM":
				num7 = 6;
				break;
			case "STRUCT_TRADEPOST":
				num7 = 7;
				break;
			case "STRUCT_BALLISTA":
				num7 = 8;
				break;
			case "STRUCT_MANGONEL":
				num7 = 9;
				break;
			case "STRUCT_PITCH_DIGGER":
				num7 = 10;
				break;
			case "STRUCT_CHURCH":
				num7 = 11;
				break;
			case "STRUCT_MOAT":
				num7 = 12;
				break;
			}
			if (MPTEMPsetupData.MP_BuildingsAvailable[num7] != 0)
			{
				MPTEMPsetupData.MP_BuildingsAvailable[num7] = 0;
			}
			else
			{
				MPTEMPsetupData.MP_BuildingsAvailable[num7] = 1;
			}
			for (int num8 = 0; num8 < 13; num8++)
			{
				MainViewModel.Instance.MPSetupBuildingsBool[num8] = MPTEMPsetupData.MP_BuildingsAvailable[num8] != 0;
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
			int num5 = int.Parse(param.Substring(7));
			if (MPTEMPsetupData.MP_TroopsAvailable[num5] != 0)
			{
				MPTEMPsetupData.MP_TroopsAvailable[num5] = 0;
			}
			else
			{
				MPTEMPsetupData.MP_TroopsAvailable[num5] = 1;
			}
			for (int num6 = 0; num6 < 32; num6++)
			{
				MainViewModel.Instance.MPSetupTroopsBool[num6] = MPTEMPsetupData.MP_TroopsAvailable[num6] != 0;
			}
			break;
		}
		case "ApplySettings":
		{
			MPTEMPsetupData.peacetime = (int)FRONT_Multiplayer_Setup.Instance.RefMP_Settings_Peacetime_Slider.Value;
			if (!customCoopGame)
			{
				PlayerCap = (int)FRONT_Multiplayer_Setup.Instance.RefSetupMaxPlayersSlider.Value;
			}
			MainViewModel.Instance.Show_MPSettings = false;
			if (!MainViewModel.Instance.Show_MPPeacetime)
			{
				MPTEMPsetupData.peacetime = 0;
			}
			if (MPTEMPsetupData.advanced_options > 0)
			{
				bool flag3 = false;
				for (int k = 0; k < MPTEMPsetupData.MP_BuildingsAvailable.Length; k++)
				{
					if (MPTEMPsetupData.MP_BuildingsAvailable[k] == 0)
					{
						flag3 = true;
					}
				}
				for (int l = 0; l < MPTEMPsetupData.MP_GoodsAvailable.Length; l++)
				{
					if (MPTEMPsetupData.MP_GoodsAvailable[l] == 0)
					{
						flag3 = true;
					}
				}
				for (int m = 0; m < MPTEMPsetupData.MP_TroopsAvailable.Length; m++)
				{
					if (MPTEMPsetupData.MP_TroopsAvailable[m] == 0)
					{
						flag3 = true;
					}
				}
				if (MPTEMPsetupData.advopt_enemy_hps != 1 || MPTEMPsetupData.advopt_faster_peasants > 0 || MPTEMPsetupData.advopt_healers > 0 || MPTEMPsetupData.advopt_eunuchs > 0 || MPTEMPsetupData.advopt_nogold > 0 || MPTEMPsetupData.advopt_improved_arabswordsmen > 0 || MPTEMPsetupData.advopt_improved_fletchers > 0 || MPTEMPsetupData.advopt_improved_laddermen > 0 || MPTEMPsetupData.advopt_improved_spearmen > 0 || MPTEMPsetupData.advopt_pre_build > 0 || MPTEMPsetupData.advopt_rebalanced_horsearchers > 0 || MPTEMPsetupData.advopt_uncapped_peasants > 0)
				{
					flag3 = true;
				}
				if (!flag3)
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
			int num4 = param[param.Length - 1] - 49;
			num4 = playerRows[num4].playerID;
			MainViewModel.Instance.Show_AddAIPanel = false;
			MainViewModel.Instance.Show_SkirmishRandomAIPanel = false;
			MainViewModel.Instance.Show_AdvancedRandom = false;
			FRONT_Multiplayer_AISettings.Show(num4, AIVs[num4 - 1], !skirmishGame);
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
			MainViewModel.Instance.MultiplayerFilterLabelVis = Visibility.Visible;
			MainViewModel.Instance.MultiplayerFilterButtonVis = Visibility.Hidden;
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
			int num26 = PlayerCap;
			if (customCoopGame)
			{
				num26 = selectedMPHeader.maxPlayers;
			}
			if (count >= num26 || (count >= currentLobby.iMaxPlayers && !customCoopGame))
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
			if (RefCustomLordList.SelectedItem == null)
			{
				break;
			}
			customLord = ((FileRow)RefCustomLordList.SelectedItem).lord;
			int forcedTeam = -1;
			if (customCoopGame)
			{
				forcedTeam = currentLobby.findCustomCoopEnemyTeam();
			}
			Platform_Multiplayer.MPLobbyMember mPLobbyMember = Platform_Multiplayer.Instance.AddCustomSkirmishPlayerLocal(customLord, forcedTeam);
			if (CustomisationFileManager.CustomMediaExists && !MyAudioManager.Instance.isSpeechPlaying(3))
			{
				string path2 = MapFileManager.SplitCustomTrailName(mPLobbyMember.customLordName);
				string text6 = System.IO.Path.Combine(ConfigSettings.GetUserCustomMediaPath(), path2, "ADD_PLAYER.wav");
				if (File.Exists(text6))
				{
					MyAudioManager.Instance.PlaySpeech(3, "*", text6, force: true);
				}
			}
			updateSteamIDMappings();
			for (int num27 = 0; num27 < 8; num27++)
			{
				if (currentLobby.this_player_to_SteamID_mapping[num27] == mPLobbyMember.GetSteamID())
				{
					ulong steamID = mPLobbyMember.GetSteamID();
					int lordSubType = mPLobbyMember.GetLordSubType();
					mPLobbyMember.SetValidCustomLordType(num27, lordSubType);
					currentLobby.this_player_to_SteamID_mapping[num27] = mPLobbyMember.GetSteamID();
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
			int num23 = int.Parse(param.Substring(11)) - 1;
			if (coopFriendsSteamIDs[num23] != 0L)
			{
				ConfigSettings.CalcCoopProgress(coopFriendsSteamIDs[num23], capProgress: true);
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
				else if (FrontendMenus.CurrentSelectedTrail == 24)
				{
					MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(ConfigSettings.Settings_Progress_Trail_CoopNext4 + 1);
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
			int num22 = int.Parse(param.Substring(11, 1)) - 1;
			if (coopFriendsSteamIDs[num22] != 0L)
			{
				coopHiddenSelectedSteamID = coopFriendsSteamIDs[num22];
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
			FRONT_CoopTrail1.Instance.RefShowHidden.Visibility = Visibility.Visible;
			FRONT_CoopTrail2.Instance.RefShowHidden.Visibility = Visibility.Visible;
			FRONT_CoopTrail3.Instance.RefShowHidden.Visibility = Visibility.Visible;
			FRONT_CoopTrail4.Instance.RefShowHidden.Visibility = Visibility.Visible;
			CoopPopulateFriendsList();
			break;
		case "Coop_Show":
			ConfigSettings.setCoopHidden(coopHiddenSelectedSteamID, state: false);
			MainViewModel.Instance.Show_CoopHidePanel = false;
			coopFriendsPage = 0;
			FRONT_CoopTrail1.Instance.RefShowHidden.Visibility = Visibility.Visible;
			FRONT_CoopTrail2.Instance.RefShowHidden.Visibility = Visibility.Visible;
			FRONT_CoopTrail3.Instance.RefShowHidden.Visibility = Visibility.Visible;
			FRONT_CoopTrail4.Instance.RefShowHidden.Visibility = Visibility.Visible;
			CoopPopulateFriendsList();
			if (ConfigSettings.getCoopTrailCount(countHidden: true) == ConfigSettings.getCoopTrailCount(countHidden: false))
			{
				FRONT_CoopTrail1.Instance.RefShowHidden.Visibility = Visibility.Hidden;
				FRONT_CoopTrail2.Instance.RefShowHidden.Visibility = Visibility.Hidden;
				FRONT_CoopTrail3.Instance.RefShowHidden.Visibility = Visibility.Hidden;
				FRONT_CoopTrail4.Instance.RefShowHidden.Visibility = Visibility.Hidden;
			}
			else
			{
				FRONT_CoopTrail1.Instance.RefShowHidden.Visibility = Visibility.Visible;
				FRONT_CoopTrail2.Instance.RefShowHidden.Visibility = Visibility.Visible;
				FRONT_CoopTrail3.Instance.RefShowHidden.Visibility = Visibility.Visible;
				FRONT_CoopTrail4.Instance.RefShowHidden.Visibility = Visibility.Visible;
			}
			FRONT_CoopTrail1.Instance.RefShowHidden.IsChecked = false;
			FRONT_CoopTrail2.Instance.RefShowHidden.IsChecked = false;
			FRONT_CoopTrail3.Instance.RefShowHidden.IsChecked = false;
			FRONT_CoopTrail4.Instance.RefShowHidden.IsChecked = false;
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
			int num21 = int.Parse(param.Substring(12)) - 1;
			if (coopFriendsSteamIDs[num21] != 0L)
			{
				SkirmishAIAddClick(((int)(coopFriendsSteamIDs[num21] - 1000)).ToString());
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
			if (RefExtremeWarningCheck.IsChecked == true)
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
			if (RefExtremeWarningCheck.IsChecked == true)
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
			RefEnableAdvancedSkirmishCheck.IsChecked = MPsetupData.advanced_skirmish_options > 0;
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
			for (int num19 = 0; num19 < 8; num19++)
			{
				if (num19 >= playerRows.Length)
				{
					continue;
				}
				int playerID = playerRows[num19].playerID;
				if (playerID >= 1 && playerID <= 8)
				{
					Platform_Multiplayer.MPLobbyMember lobbyMemberFromThis_PlayerID4 = currentLobby.GetLobbyMemberFromThis_PlayerID(playerID);
					if (lobbyMemberFromThis_PlayerID4 != null && lobbyMemberFromThis_PlayerID4.SkirmishHumanMember)
					{
						ButtonClicked("Kick_" + (num19 + 1));
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
		case "Settings_ImprovedSiegingX":
			if (skirmishGame)
			{
				if (MPsetupData.global_improved_sieging2 == 0)
				{
					MPsetupData.global_improved_sieging2 = 1;
				}
				else
				{
					MPsetupData.global_improved_sieging2 = 0;
				}
				if (MPsetupData.global_improved_sieging2 > 0)
				{
					MainViewModel.Instance.MP_Settings_ImprovedSiegingX = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_ImprovedSiegingX = MainViewModel.Instance.GameSprites[641];
				}
			}
			else
			{
				if (MPTEMPsetupData.global_improved_sieging2 == 0)
				{
					MPTEMPsetupData.global_improved_sieging2 = 1;
				}
				else
				{
					MPTEMPsetupData.global_improved_sieging2 = 0;
				}
				if (MPTEMPsetupData.global_improved_sieging2 > 0)
				{
					MainViewModel.Instance.MP_Settings_ImprovedSiegingX = MainViewModel.Instance.GameSprites[640];
				}
				else
				{
					MainViewModel.Instance.MP_Settings_ImprovedSiegingX = MainViewModel.Instance.GameSprites[641];
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

	private void EnableAdvancedSkirmishCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (RefEnableAdvancedSkirmishCheck.IsChecked.Value)
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

	private void MuteMPChat_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			Platform_Multiplayer.MPChatMuted = RefChatMuteDisable.IsChecked.Value;
			RefMP_ChatSend.IsEnabled = !Platform_Multiplayer.MPChatMuted;
		}
	}

	private void AutoJoinLobby(Platform_Multiplayer.MPLobby joiningLobby)
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
			else if (currentLobby.coopTrailID == 3)
			{
				FrontendMenus.CurrentSelectedTrailCoop4Mission = 1;
				FrontendMenus.CurrentSelectedTrail = 24;
				MainViewModel.Instance.FrontEndMenu.GenerateSwords();
				MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(1);
				MainViewModel.Instance.Show_CoopTrail4 = true;
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

	private void UpdateButtons()
	{
	}

	private void SetupSkirmishModeSettings()
	{
		MainViewModel.Instance.MPGame_Type_Description = "";
		MainViewModel.Instance.Show_MPGame_Type_Description = false;
		switch (MPsetupData.fairness)
		{
		case 1:
			RefFairness1.IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 3);
			break;
		case 2:
			RefFairness2.IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 3);
			break;
		case 3:
			RefFairness3.IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = "";
			break;
		case 4:
			RefFairness4.IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 4);
			break;
		case 5:
			RefFairness5.IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 4);
			break;
		}
		switch (MPsetupData.starting_goods_level)
		{
		case 1:
			RefGameType1.IsChecked = true;
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 0);
			break;
		case 2:
			RefGameType2.IsChecked = true;
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 1);
			break;
		case 3:
			RefGameType3.IsChecked = true;
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
		if (MPsetupData.global_improved_sieging2 > 0)
		{
			MainViewModel.Instance.MP_Settings_ImprovedSiegingX = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_ImprovedSiegingX = MainViewModel.Instance.GameSprites[641];
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
		FRONT_CoopTrail1.Instance.RefMP_Settings_GameSpeed_Slider.Value = MPsetupData.starting_gamespeed / 5;
		FRONT_CoopTrail2.Instance.RefMP_Settings_GameSpeed_Slider.Value = MPsetupData.starting_gamespeed / 5;
		FRONT_CoopTrail3.Instance.RefMP_Settings_GameSpeed_Slider.Value = MPsetupData.starting_gamespeed / 5;
		FRONT_CoopTrail4.Instance.RefMP_Settings_GameSpeed_Slider.Value = MPsetupData.starting_gamespeed / 5;
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

	private void updateSkirmishStartingGoldLevels()
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

	private void ImportSettings(string settings, bool isHost = false)
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
			if (MPDefaultsetupData.global_improved_sieging2 != MPTEMPsetupData.global_improved_sieging2)
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Improved_Sieging_OpacityX = 1f;
			}
			else
			{
				MainViewModel.Instance.MPSettings_AdvSkirmish_Improved_Sieging_OpacityX = 0.5f;
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
			FRONT_Multiplayer_Setup.Instance.RefFairness1.IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 3);
			break;
		case 2:
			FRONT_Multiplayer_Setup.Instance.RefFairness2.IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 3);
			break;
		case 3:
			FRONT_Multiplayer_Setup.Instance.RefFairness3.IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = "";
			break;
		case 4:
			FRONT_Multiplayer_Setup.Instance.RefFairness4.IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 4);
			break;
		case 5:
			FRONT_Multiplayer_Setup.Instance.RefFairness5.IsChecked = true;
			MainViewModel.Instance.MPGame_Advantage = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 4);
			break;
		}
		MainViewModel.Instance.MPGame_Type_Description = "";
		MainViewModel.Instance.Show_MPGame_Type_Description = false;
		switch (MPTEMPsetupData.starting_goods_level)
		{
		case 1:
			FRONT_Multiplayer_Setup.Instance.RefGameType1.IsChecked = true;
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 0);
			break;
		case 2:
			FRONT_Multiplayer_Setup.Instance.RefGameType2.IsChecked = true;
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 1);
			break;
		case 3:
			FRONT_Multiplayer_Setup.Instance.RefGameType3.IsChecked = true;
			MainViewModel.Instance.MPGame_Type = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_TYPE, 2);
			break;
		}
		updateStartingGoldLevels();
		FRONT_Multiplayer_Setup.Instance.RefMP_Settings_GameSpeed_Slider.Value = MPTEMPsetupData.starting_gamespeed / 5;
		MainViewModel.Instance.MP_Settings_GameSpeed = MPTEMPsetupData.starting_gamespeed.ToString();
		FRONT_Multiplayer_Setup.Instance.RefMP_Settings_Peacetime_Slider.Value = MPTEMPsetupData.peacetime;
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
		FRONT_Multiplayer_Setup.Instance.RefSetupMaxPlayersSlider.Value = PlayerCap;
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
		if (MPTEMPsetupData.global_improved_sieging2 > 0)
		{
			MainViewModel.Instance.MP_Settings_ImprovedSiegingX = MainViewModel.Instance.GameSprites[640];
		}
		else
		{
			MainViewModel.Instance.MP_Settings_ImprovedSiegingX = MainViewModel.Instance.GameSprites[641];
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

	private void UpdateLobbySettingsButton()
	{
		if (selectedLobby != null && EngineInterface.MultiplayerSetupData.compareSettingsStrings(selectedLobby.settings, defaultMPSettings))
		{
			RefLobbySettingsButton.Visibility = Visibility.Visible;
		}
		else
		{
			RefLobbySettingsButton.Visibility = Visibility.Hidden;
		}
	}

	private void UpdateLobbyChangeButtons()
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
					if (RefFileLists.SelectedItem != null)
					{
						RefFileLists.ScrollIntoView(RefFileLists.SelectedItem);
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
						string[] array = currentLobby.startGame.Split("!");
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
								else if (FrontendMenus.CurrentSelectedTrail == 24)
								{
									coopTrailID = 4;
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
							Debug.LogError("Missing Save file : " + currentLobby.startGame);
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
				((Noesis.Grid)playerRows[num5].RefRow.Parent).Background = lightBarColour;
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
					playerRows[num6].RefRow.Background = teamRedBarColour;
					break;
				case 2:
					playerRows[num6].RefRow.Background = teamYellowBarColour;
					break;
				case 3:
					playerRows[num6].RefRow.Background = teamBlueBarColour;
					break;
				case 4:
					playerRows[num6].RefRow.Background = teamGreenBarColour;
					break;
				default:
					if (flag3)
					{
						playerRows[num6].RefRow.Background = lightBarColour;
					}
					else
					{
						playerRows[num6].RefRow.Background = darkBarColour;
					}
					break;
				}
				((Noesis.Grid)playerRows[num6].RefRow.Parent).Background = transparentColour;
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
				if (coopGame && MainViewModel.Instance.Show_CoopTrail4)
				{
					FRONT_CoopTrail4.Instance.playerRows[num6].Update(this, lobbyMemberFromThis_PlayerID, row, thisPlayerFromSteamID);
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
				if (coopGame && MainViewModel.Instance.Show_CoopTrail4)
				{
					FRONT_CoopTrail4.Instance.playerRows[num7].Update(this, null, num7, -1);
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
				SetReadyStateImage(FRONT_CoopTrail4.Instance.RefReadyButton, MainViewModel.Instance.GameSprites[105], MainViewModel.Instance.GameSprites[106]);
				RefReadyButtonLock.Visibility = Visibility.Visible;
				FRONT_CoopTrail1.Instance.RefReadyButtonLock.Visibility = Visibility.Visible;
				FRONT_CoopTrail2.Instance.RefReadyButtonLock.Visibility = Visibility.Visible;
				FRONT_CoopTrail3.Instance.RefReadyButtonLock.Visibility = Visibility.Visible;
				FRONT_CoopTrail4.Instance.RefReadyButtonLock.Visibility = Visibility.Visible;
				if (MPLocalReadyLocked)
				{
					SetReadyStateImage(RefReadyButtonLock, MainViewModel.Instance.GameSprites[688], MainViewModel.Instance.GameSprites[689]);
					SetReadyStateImage(FRONT_CoopTrail1.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[688], MainViewModel.Instance.GameSprites[689]);
					SetReadyStateImage(FRONT_CoopTrail2.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[688], MainViewModel.Instance.GameSprites[689]);
					SetReadyStateImage(FRONT_CoopTrail3.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[688], MainViewModel.Instance.GameSprites[689]);
					SetReadyStateImage(FRONT_CoopTrail4.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[688], MainViewModel.Instance.GameSprites[689]);
				}
				else
				{
					SetReadyStateImage(RefReadyButtonLock, MainViewModel.Instance.GameSprites[690], MainViewModel.Instance.GameSprites[691]);
					SetReadyStateImage(FRONT_CoopTrail1.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[690], MainViewModel.Instance.GameSprites[691]);
					SetReadyStateImage(FRONT_CoopTrail2.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[690], MainViewModel.Instance.GameSprites[691]);
					SetReadyStateImage(FRONT_CoopTrail3.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[690], MainViewModel.Instance.GameSprites[691]);
					SetReadyStateImage(FRONT_CoopTrail4.Instance.RefReadyButtonLock, MainViewModel.Instance.GameSprites[690], MainViewModel.Instance.GameSprites[691]);
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
				SetReadyStateImage(FRONT_CoopTrail4.Instance.RefReadyButton, MainViewModel.Instance.GameSprites[103], MainViewModel.Instance.GameSprites[104]);
				RefReadyButtonLock.Visibility = Visibility.Hidden;
				FRONT_CoopTrail1.Instance.RefReadyButtonLock.Visibility = Visibility.Hidden;
				FRONT_CoopTrail2.Instance.RefReadyButtonLock.Visibility = Visibility.Hidden;
				FRONT_CoopTrail3.Instance.RefReadyButtonLock.Visibility = Visibility.Hidden;
				FRONT_CoopTrail4.Instance.RefReadyButtonLock.Visibility = Visibility.Hidden;
			}
			if (currentLobby != null && !currentLobby.isHost)
			{
				(MainViewModel.GetListBox(RefFileLists) as ListBox).IsHitTestVisible = false;
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
							foreach (FileRow item2 in RefFileLists.ItemsSource)
							{
								if (item2.fileHeader == selectedMPHeader)
								{
									RefFileLists.SelectedItem = item2;
									RefFileLists.ScrollIntoView(RefFileLists.SelectedItem);
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
				(MainViewModel.GetListBox(RefFileLists) as ListBox).IsHitTestVisible = true;
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
				RefMultiplayerInvite.IsEnabled = currentLobby.numLobbyMembers < currentLobby.iMaxPlayers;
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
			RefMultiplayerPlayButton.IsEnabled = flag4;
			RefTrailMakerTest.IsEnabled = flag4;
			FRONT_CoopTrail1.Instance.RefMultiplayerPlayButton.IsEnabled = flag4;
			FRONT_CoopTrail2.Instance.RefMultiplayerPlayButton.IsEnabled = flag4;
			FRONT_CoopTrail3.Instance.RefMultiplayerPlayButton.IsEnabled = flag4;
			FRONT_CoopTrail4.Instance.RefMultiplayerPlayButton.IsEnabled = flag4;
			RefLoadButton.IsEnabled = (flag5 && !flag6) | skirmishGame;
			FRONT_CoopTrail1.Instance.RefLoadButton.IsEnabled = flag4 | singlePlayerCoop | MainViewModel.Instance.Show_CoopHostInvitePane;
			FRONT_CoopTrail2.Instance.RefLoadButton.IsEnabled = flag4 | singlePlayerCoop | MainViewModel.Instance.Show_CoopHostInvitePane;
			FRONT_CoopTrail3.Instance.RefLoadButton.IsEnabled = flag4 | singlePlayerCoop | MainViewModel.Instance.Show_CoopHostInvitePane;
			FRONT_CoopTrail4.Instance.RefLoadButton.IsEnabled = flag4 | singlePlayerCoop | MainViewModel.Instance.Show_CoopHostInvitePane;
		}
		if ((DateTime.UtcNow - lastScrollTest).TotalMilliseconds > 150.0 && (MainViewModel.Instance.Show_MPJoiningLobby || (currentLobby != null && currentLobby.isHost)))
		{
			if (KeyManager.instance.CursorUpHeld)
			{
				lastScrollTest = DateTime.UtcNow;
				ListView listView = ((!MainViewModel.Instance.Show_MPJoiningLobby) ? RefFileLists : RefLobbyLists);
				ScrollViewer scrollViewer = MainViewModel.GetScrollViewer(listView) as ScrollViewer;
				if (scrollViewer != null)
				{
					if (listView.SelectedItem == null)
					{
						scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 30f);
					}
					else
					{
						if (listView.SelectedIndex > 0)
						{
							listView.SelectedIndex--;
						}
						listView.ScrollIntoView(listView.SelectedItem);
					}
				}
			}
			else if (KeyManager.instance.CursorDownHeld)
			{
				lastScrollTest = DateTime.UtcNow;
				ListView listView2 = ((!MainViewModel.Instance.Show_MPJoiningLobby) ? RefFileLists : RefLobbyLists);
				ScrollViewer scrollViewer2 = MainViewModel.GetScrollViewer(listView2) as ScrollViewer;
				if (scrollViewer2 != null)
				{
					if (listView2.SelectedItem == null)
					{
						scrollViewer2.ScrollToVerticalOffset(scrollViewer2.VerticalOffset + 30f);
					}
					else
					{
						if (listView2.SelectedIndex < RefFileLists.Items.Count - 1)
						{
							listView2.SelectedIndex++;
						}
						listView2.ScrollIntoView(listView2.SelectedItem);
					}
				}
			}
		}
		if (SelectedRadarKeep >= 0)
		{
			Point position = Mouse.GetPosition(RefBasemap);
			Thickness margin = new Thickness(position.X, position.Y, -100f, -100f);
			RefFloatingRadarShield.Margin = margin;
			if (Input.GetMouseButtonDown(1))
			{
				SelectedRadarKeep = -1;
				MainViewModel.Instance.Show_SkirmishUIOnRadar = false;
				UpdateRadarShieldPositions();
			}
		}
		if (SelectedFace >= 0)
		{
			Point position2 = Mouse.GetPosition(RefBasemap);
			Thickness margin2 = new Thickness(position2.X, position2.Y, -100f, -100f);
			RefFloatingTeams.Margin = margin2;
			if (Input.GetMouseButtonDown(1))
			{
				SelectedFace = -1;
				RefTeamFaceCancel.IsEnabled = false;
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

	private void SetReadyStateImage(Button readyButton, ImageSource image, ImageSource overImage)
	{
		if ((ImageSource)PropEx.GetSprite1(readyButton) != image)
		{
			PropEx.SetSprite1(readyButton, image);
			PropEx.SetSprite2(readyButton, overImage);
			PropEx.SetSprite3(readyButton, overImage);
		}
	}

	private void Update_Coop()
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
					else if (FrontendMenus.CurrentSelectedTrail == 24)
					{
						Platform_Multiplayer.Instance.SetCoopTrailProgress(3, ConfigSettings.Settings_Progress_Trail_Coop4_Status, FrontendMenus.CurrentSelectedTrailCoop4Mission, ConfigSettings.Settings_Progress_Trail_Coop4, coopOrderSwapped);
						MainViewModel.Instance.FrontEndMenu.GenerateSwords();
						MainViewModel.Instance.FrontEndMenu.ButtonTrailCampaignClicked(ConfigSettings.Settings_Progress_Trail_CoopNext4 + 1);
						CoopMissionChanged(3, FrontendMenus.CurrentSelectedTrailCoop4Mission);
					}
				}
				if (avatarCallbacks.Count > 0)
				{
					AvatarCallback avatarCallback = avatarCallbacks.Peek();
					ImageSource userAvatar = Platform_Multiplayer.Instance.GetUserAvatar(avatarCallback.steamID);
					if (userAvatar != null)
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
					else if (currentLobby.coopTrailID == 3)
					{
						FRONT_CoopTrail4.Instance.UpdateRadarShieldPositions();
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
				else if (currentLobby.coopTrailID == 3)
				{
					for (int l = 0; l < 10; l++)
					{
						ConfigSettings.Settings_Progress_Trail_Coop4_Status[l] = currentLobby.coopTrailProgress[l];
					}
					ConfigSettings.Settings_Progress_Trail_Coop4 = currentLobby.coopTrailFullProgress;
					FrontendMenus.CurrentSelectedTrailCoop4Mission = currentLobby.coopSelectedMission;
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
			else if (currentLobby.coopTrailID == 3)
			{
				FRONT_CoopTrail4.Instance.UpdateRadarShieldPositions();
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

	private void ShowLobbyScreen()
	{
		MainViewModel.Instance.Show_MPJoiningLobby = true;
		MainViewModel.Instance.Show_MPGameCreation = false;
		MainViewModel.Instance.Show_MPSettings = false;
		MainViewModel.Instance.Show_MPSteamIdentity = false;
		MainViewModel.Instance.Show_SkirmishTeams = true;
		customCoopGame = false;
		RefMultiplayerPlayButton.Visibility = Visibility.Hidden;
		RefLoadButton.Visibility = Visibility.Hidden;
		selectedMPHeader = null;
		MainViewModel.Instance.Show_MPIsHost = false;
		MainViewModel.Instance.StandaloneTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 51);
		lobbyChat.Clear();
		RefMP_ChatDisplay.Inlines.Clear();
		FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines.Clear();
		FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines.Clear();
		FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines.Clear();
		FRONT_CoopTrail4.Instance.RefMP_ChatDisplay.Inlines.Clear();
	}

	private void ShowSetupScreen()
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
		FRONT_CoopTrail4.Instance.RefMP_ChatInput.Text = "";
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
		RefIncludeBuiltin.IsChecked = true;
		RefIncludeUser.IsChecked = true;
		RefIncludeWorkshop.IsChecked = true;
		justEnteredSetupScreen = DateTime.UtcNow.AddSeconds(5.0);
		justEnteredSetup = true;
		if (skirmishGame || currentLobby.isHost)
		{
			RefMultiplayerPlayButton.Visibility = Visibility.Visible;
			RefMultiplayerPlayButton.IsEnabled = false;
			if (!skirmishGame)
			{
				RefLoadButton.Visibility = Visibility.Visible;
				RefLoadButton.IsEnabled = false;
			}
		}
		else
		{
			RefMultiplayerPlayButton.Visibility = Visibility.Hidden;
			RefLoadButton.Visibility = Visibility.Hidden;
		}
		if (!skirmishGame && currentLobby.gameTypeCoop == "1")
		{
			MainViewModel.Instance.StandaloneTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 432);
			customCoopGame = true;
			MainViewModel.Instance.Show_SkirmishTeams = false;
		}
		else if (trailMakerMode)
		{
			RefHeaderBar.Width = 845f;
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

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/FRONT_Multiplayer.xaml");
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

	public void SkirmishAIAddClick(string param)
	{
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
			RefCustomLordList.ItemsSource = customLordRows;
			if (customLords.Count > 0)
			{
				RefCustomLordList.SelectedIndex = 0;
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
		case 27:
		case 28:
		case 99:
			if ((num == 20 || num == 21) && !FrontendMenus.DLC1Owned)
			{
				SteamFriends.ActivateGameOverlayToStore(new AppId_t(3030340u), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
			}
			else if ((num == 22 || num == 23) && !FrontendMenus.DLC2Owned)
			{
				SteamFriends.ActivateGameOverlayToStore(new AppId_t(3030350u), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
			}
			else if ((num == 25 || num == 26) && !FrontendMenus.DLC3Owned)
			{
				SteamFriends.ActivateGameOverlayToStore(new AppId_t(4483540u), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
			}
			else if ((num == 27 || num == 28) && !FrontendMenus.DLC4Owned)
			{
				SteamFriends.ActivateGameOverlayToStore(new AppId_t(4483530u), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
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
					System.Random random2 = new System.Random();
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
			System.Random random = new System.Random();
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

	private void UpdateRandomAIButtons()
	{
		int num = currentLobby.iMaxPlayers - 1;
		if (num < 0)
		{
			num = 0;
		}
		RefRandomAI1.IsEnabled = num >= 1;
		RefRandomAI2.IsEnabled = num >= 2;
		RefRandomAI3.IsEnabled = num >= 3;
		RefRandomAI4.IsEnabled = num >= 4;
		RefRandomAI5.IsEnabled = num >= 5;
		RefRandomAI6.IsEnabled = num >= 6;
		RefRandomAI7.IsEnabled = num >= 7;
	}

	private void MonitorAILordText()
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

	private void UpdateMatchmakingButton()
	{
		if (matchmakingDefault == 1)
		{
			MainViewModel.Instance.MP_LobbyLocalRegionVis = Visibility.Hidden;
			MainViewModel.Instance.MP_LobbyDefaultRegionVis = Visibility.Visible;
			MainViewModel.Instance.MP_LobbyGlobalRegionVis = Visibility.Hidden;
		}
		else if (matchmakingDefault == 0)
		{
			MainViewModel.Instance.MP_LobbyLocalRegionVis = Visibility.Visible;
			MainViewModel.Instance.MP_LobbyDefaultRegionVis = Visibility.Hidden;
			MainViewModel.Instance.MP_LobbyGlobalRegionVis = Visibility.Hidden;
		}
		else if (matchmakingDefault == 2)
		{
			MainViewModel.Instance.MP_LobbyLocalRegionVis = Visibility.Hidden;
			MainViewModel.Instance.MP_LobbyDefaultRegionVis = Visibility.Hidden;
			MainViewModel.Instance.MP_LobbyGlobalRegionVis = Visibility.Visible;
		}
	}

	private void updateHostLobbyButton()
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

	private void receivedLobbyChat(string _name, string _message, int _colourID, bool systemMessage = false)
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

	private void refreshLobbyChat(bool fromReceive = true)
	{
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
		RefMP_ChatDisplay.Inlines.Clear();
		FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines.Clear();
		FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines.Clear();
		FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines.Clear();
		FRONT_CoopTrail4.Instance.RefMP_ChatDisplay.Inlines.Clear();
		foreach (LobbyChatEntry item5 in lobbyChat)
		{
			if (item5.colourID >= 0)
			{
				ImageSource colourShield = GetColourShield(item5.colourID);
				InlineUIContainer item = new InlineUIContainer
				{
					Child = new Image
					{
						Source = colourShield,
						Width = 14f,
						Height = 14f
					}
				};
				if (MainViewModel.Instance.Show_CoopTrail1)
				{
					FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines.Add(item);
				}
				else if (MainViewModel.Instance.Show_CoopTrail2)
				{
					FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines.Add(item);
				}
				else if (MainViewModel.Instance.Show_CoopTrail3)
				{
					FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines.Add(item);
				}
				else if (MainViewModel.Instance.Show_CoopTrail4)
				{
					FRONT_CoopTrail4.Instance.RefMP_ChatDisplay.Inlines.Add(item);
				}
				else
				{
					RefMP_ChatDisplay.Inlines.Add(item);
				}
				InlineUIContainer item2 = new InlineUIContainer
				{
					Child = new TextBlock
					{
						Text = " " + item5.name + " :",
						Width = 600f,
						FontSize = 14f,
						Height = 14f
					}
				};
				if (MainViewModel.Instance.Show_CoopTrail1)
				{
					FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines.Add(item2);
					FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines.Add(new LineBreak());
				}
				else if (MainViewModel.Instance.Show_CoopTrail2)
				{
					FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines.Add(item2);
					FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines.Add(new LineBreak());
				}
				else if (MainViewModel.Instance.Show_CoopTrail3)
				{
					FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines.Add(item2);
					FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines.Add(new LineBreak());
				}
				else if (MainViewModel.Instance.Show_CoopTrail4)
				{
					FRONT_CoopTrail4.Instance.RefMP_ChatDisplay.Inlines.Add(item2);
					FRONT_CoopTrail4.Instance.RefMP_ChatDisplay.Inlines.Add(new LineBreak());
				}
				else
				{
					RefMP_ChatDisplay.Inlines.Add(item2);
					RefMP_ChatDisplay.Inlines.Add(new LineBreak());
				}
				InlineUIContainer item3 = new InlineUIContainer
				{
					Child = new TextBlock
					{
						Text = item5.message,
						TextWrapping = TextWrapping.WrapWithOverflow,
						Margin = new Thickness(40f, 0f, 5f, 0f),
						FontSize = 12f
					}
				};
				if (MainViewModel.Instance.Show_CoopTrail1)
				{
					FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines.Add(item3);
					FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines.Add(new Run(Environment.NewLine));
				}
				else if (MainViewModel.Instance.Show_CoopTrail2)
				{
					FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines.Add(item3);
					FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines.Add(new Run(Environment.NewLine));
				}
				else if (MainViewModel.Instance.Show_CoopTrail3)
				{
					FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines.Add(item3);
					FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines.Add(new Run(Environment.NewLine));
				}
				else if (MainViewModel.Instance.Show_CoopTrail4)
				{
					FRONT_CoopTrail4.Instance.RefMP_ChatDisplay.Inlines.Add(item3);
					FRONT_CoopTrail4.Instance.RefMP_ChatDisplay.Inlines.Add(new Run(Environment.NewLine));
				}
				else
				{
					RefMP_ChatDisplay.Inlines.Add(item3);
					RefMP_ChatDisplay.Inlines.Add(new Run(Environment.NewLine));
				}
			}
			else
			{
				InlineUIContainer item4 = new InlineUIContainer
				{
					Child = new TextBlock
					{
						Text = item5.message + " " + item5.name,
						Width = 600f,
						FontSize = 14f,
						Height = 16f
					}
				};
				if (MainViewModel.Instance.Show_CoopTrail1)
				{
					FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines.Add(item4);
					FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines.Add(new LineBreak());
				}
				else if (MainViewModel.Instance.Show_CoopTrail2)
				{
					FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines.Add(item4);
					FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines.Add(new LineBreak());
				}
				else if (MainViewModel.Instance.Show_CoopTrail3)
				{
					FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines.Add(item4);
					FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines.Add(new LineBreak());
				}
				else if (MainViewModel.Instance.Show_CoopTrail4)
				{
					FRONT_CoopTrail4.Instance.RefMP_ChatDisplay.Inlines.Add(item4);
					FRONT_CoopTrail4.Instance.RefMP_ChatDisplay.Inlines.Add(new LineBreak());
				}
				else
				{
					RefMP_ChatDisplay.Inlines.Add(item4);
					RefMP_ChatDisplay.Inlines.Add(new LineBreak());
				}
			}
		}
		RefMP_ChatScrollView.ScrollToBottom();
		FRONT_CoopTrail1.Instance.RefMP_ChatScrollView.ScrollToBottom();
		FRONT_CoopTrail2.Instance.RefMP_ChatScrollView.ScrollToBottom();
		FRONT_CoopTrail3.Instance.RefMP_ChatScrollView.ScrollToBottom();
		FRONT_CoopTrail4.Instance.RefMP_ChatScrollView.ScrollToBottom();
		lobbyChatRefreshTime = DateTime.UtcNow.AddMilliseconds(500.0);
	}

	private void addSystemLobbyChat(string _name, string _message)
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

	private void updateStartingGoldLevels()
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
			int peacetime = (int)FRONT_Multiplayer_Setup.Instance.RefMP_Settings_Peacetime_Slider.Value;
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
			int starting_gamespeed = (int)FRONT_Multiplayer_Setup.Instance.RefMP_Settings_GameSpeed_Slider.Value * 5;
			MainViewModel.Instance.MP_Settings_GameSpeed = starting_gamespeed.ToString();
			MPTEMPsetupData.starting_gamespeed = starting_gamespeed;
		}
		else if (coopGame && MPsetupData != null)
		{
			int starting_gamespeed2 = 0;
			if (MainViewModel.Instance.Show_CoopTrail1)
			{
				starting_gamespeed2 = (int)FRONT_CoopTrail1.Instance.RefMP_Settings_GameSpeed_Slider.Value * 5;
			}
			else if (MainViewModel.Instance.Show_CoopTrail2)
			{
				starting_gamespeed2 = (int)FRONT_CoopTrail2.Instance.RefMP_Settings_GameSpeed_Slider.Value * 5;
			}
			else if (MainViewModel.Instance.Show_CoopTrail3)
			{
				starting_gamespeed2 = (int)FRONT_CoopTrail3.Instance.RefMP_Settings_GameSpeed_Slider.Value * 5;
			}
			else if (MainViewModel.Instance.Show_CoopTrail4)
			{
				starting_gamespeed2 = (int)FRONT_CoopTrail4.Instance.RefMP_Settings_GameSpeed_Slider.Value * 5;
			}
			MainViewModel.Instance.MP_Settings_GameSpeed = starting_gamespeed2.ToString();
			MPsetupData.starting_gamespeed = starting_gamespeed2;
		}
	}

	private void UpdateHostInfo(bool delayed = false)
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

	private void ShowColourPicker()
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

	private int GetPlayerColour()
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

	private void SetShieldColour(int colourID)
	{
		Platform_Multiplayer.Instance.SetPlayerColour(colourID);
		UpdateColourShields(colourID);
		UpdateRadarShieldPositions();
	}

	private void UpdateColourShields(int colourID)
	{
		PropEx.SetSprite1(RefColourSelectButton, GetColourShield(colourID));
		PropEx.SetSprite2(RefColourSelectButton, GetColourShield(colourID, 1));
		PropEx.SetSprite3(RefColourSelectButton, GetColourShield(colourID, 1));
		PropEx.SetSprite4(RefColourSelectButton, GetColourShield(colourID));
		List<int> usedColours = Platform_Multiplayer.Instance.GetUsedColours(colourID);
		bool flag = !usedColours.Contains(1);
		RefColShield1.IsEnabled = flag;
		RefColShield1.Opacity = (flag ? 1f : 0.5f);
		bool flag2 = !usedColours.Contains(2);
		RefColShield2.IsEnabled = flag2;
		RefColShield2.Opacity = (flag2 ? 1f : 0.5f);
		bool flag3 = !usedColours.Contains(3);
		RefColShield3.IsEnabled = flag3;
		RefColShield3.Opacity = (flag3 ? 1f : 0.5f);
		bool flag4 = !usedColours.Contains(4);
		RefColShield4.IsEnabled = flag4;
		RefColShield4.Opacity = (flag4 ? 1f : 0.5f);
		bool flag5 = !usedColours.Contains(5);
		RefColShield5.IsEnabled = flag5;
		RefColShield5.Opacity = (flag5 ? 1f : 0.5f);
		bool flag6 = !usedColours.Contains(6);
		RefColShield6.IsEnabled = flag6;
		RefColShield6.Opacity = (flag6 ? 1f : 0.5f);
		bool flag7 = !usedColours.Contains(7);
		RefColShield7.IsEnabled = flag7;
		RefColShield7.Opacity = (flag7 ? 1f : 0.5f);
		bool flag8 = !usedColours.Contains(8);
		RefColShield8.IsEnabled = flag8;
		RefColShield8.Opacity = (flag8 ? 1f : 0.5f);
		if (colourID == 1)
		{
			PropEx.SetSprite1(RefColShield1, GetColourShield(1, 2));
			PropEx.SetSprite2(RefColShield1, GetColourShield(1, 2));
			PropEx.SetSprite3(RefColShield1, GetColourShield(1, 2));
			PropEx.SetSprite4(RefColShield1, GetColourShield(1, 2));
		}
		else
		{
			PropEx.SetSprite1(RefColShield1, GetColourShield(1));
			PropEx.SetSprite2(RefColShield1, GetColourShield(1, 1));
			PropEx.SetSprite3(RefColShield1, GetColourShield(1, 1));
			PropEx.SetSprite4(RefColShield1, GetColourShield(1));
		}
		if (colourID == 2)
		{
			PropEx.SetSprite1(RefColShield2, GetColourShield(2, 2));
			PropEx.SetSprite2(RefColShield2, GetColourShield(2, 2));
			PropEx.SetSprite3(RefColShield2, GetColourShield(2, 2));
			PropEx.SetSprite4(RefColShield2, GetColourShield(2, 2));
		}
		else
		{
			PropEx.SetSprite1(RefColShield2, GetColourShield(2));
			PropEx.SetSprite2(RefColShield2, GetColourShield(2, 1));
			PropEx.SetSprite3(RefColShield2, GetColourShield(2, 1));
			PropEx.SetSprite4(RefColShield2, GetColourShield(2));
		}
		if (colourID == 3)
		{
			PropEx.SetSprite1(RefColShield3, GetColourShield(3, 2));
			PropEx.SetSprite2(RefColShield3, GetColourShield(3, 2));
			PropEx.SetSprite3(RefColShield3, GetColourShield(3, 2));
			PropEx.SetSprite4(RefColShield3, GetColourShield(3, 2));
		}
		else
		{
			PropEx.SetSprite1(RefColShield3, GetColourShield(3));
			PropEx.SetSprite2(RefColShield3, GetColourShield(3, 1));
			PropEx.SetSprite3(RefColShield3, GetColourShield(3, 1));
			PropEx.SetSprite4(RefColShield3, GetColourShield(3));
		}
		if (colourID == 4)
		{
			PropEx.SetSprite1(RefColShield4, GetColourShield(4, 2));
			PropEx.SetSprite2(RefColShield4, GetColourShield(4, 2));
			PropEx.SetSprite3(RefColShield4, GetColourShield(4, 2));
			PropEx.SetSprite4(RefColShield4, GetColourShield(4, 2));
		}
		else
		{
			PropEx.SetSprite1(RefColShield4, GetColourShield(4));
			PropEx.SetSprite2(RefColShield4, GetColourShield(4, 1));
			PropEx.SetSprite3(RefColShield4, GetColourShield(4, 1));
			PropEx.SetSprite4(RefColShield4, GetColourShield(4));
		}
		if (colourID == 5)
		{
			PropEx.SetSprite1(RefColShield5, GetColourShield(5, 2));
			PropEx.SetSprite2(RefColShield5, GetColourShield(5, 2));
			PropEx.SetSprite3(RefColShield5, GetColourShield(5, 2));
			PropEx.SetSprite4(RefColShield5, GetColourShield(5, 2));
		}
		else
		{
			PropEx.SetSprite1(RefColShield5, GetColourShield(5));
			PropEx.SetSprite2(RefColShield5, GetColourShield(5, 1));
			PropEx.SetSprite3(RefColShield5, GetColourShield(5, 1));
			PropEx.SetSprite4(RefColShield5, GetColourShield(5));
		}
		if (colourID == 6)
		{
			PropEx.SetSprite1(RefColShield6, GetColourShield(6, 2));
			PropEx.SetSprite2(RefColShield6, GetColourShield(6, 2));
			PropEx.SetSprite3(RefColShield6, GetColourShield(6, 2));
			PropEx.SetSprite4(RefColShield6, GetColourShield(6, 2));
		}
		else
		{
			PropEx.SetSprite1(RefColShield6, GetColourShield(6));
			PropEx.SetSprite2(RefColShield6, GetColourShield(6, 1));
			PropEx.SetSprite3(RefColShield6, GetColourShield(6, 1));
			PropEx.SetSprite4(RefColShield6, GetColourShield(6));
		}
		if (colourID == 7)
		{
			PropEx.SetSprite1(RefColShield7, GetColourShield(7, 2));
			PropEx.SetSprite2(RefColShield7, GetColourShield(7, 2));
			PropEx.SetSprite3(RefColShield7, GetColourShield(7, 2));
			PropEx.SetSprite4(RefColShield7, GetColourShield(7, 2));
		}
		else
		{
			PropEx.SetSprite1(RefColShield7, GetColourShield(7));
			PropEx.SetSprite2(RefColShield7, GetColourShield(7, 1));
			PropEx.SetSprite3(RefColShield7, GetColourShield(7, 1));
			PropEx.SetSprite4(RefColShield7, GetColourShield(7));
		}
		if (colourID == 8)
		{
			PropEx.SetSprite1(RefColShield8, GetColourShield(8, 2));
			PropEx.SetSprite2(RefColShield8, GetColourShield(8, 2));
			PropEx.SetSprite3(RefColShield8, GetColourShield(8, 2));
			PropEx.SetSprite4(RefColShield8, GetColourShield(8, 2));
		}
		else
		{
			PropEx.SetSprite1(RefColShield8, GetColourShield(8));
			PropEx.SetSprite2(RefColShield8, GetColourShield(8, 1));
			PropEx.SetSprite3(RefColShield8, GetColourShield(8, 1));
			PropEx.SetSprite4(RefColShield8, GetColourShield(8));
		}
	}

	private void LeaveLobby(bool doLeaveOnSteam = true, bool refreshLobbyList = true)
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
		RefMP_ChatDisplay.Inlines.Clear();
		FRONT_CoopTrail1.Instance.RefMP_ChatDisplay.Inlines.Clear();
		FRONT_CoopTrail2.Instance.RefMP_ChatDisplay.Inlines.Clear();
		FRONT_CoopTrail3.Instance.RefMP_ChatDisplay.Inlines.Clear();
		FRONT_CoopTrail4.Instance.RefMP_ChatDisplay.Inlines.Clear();
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

	private void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	private void FilterTextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
		if ((bool)e.NewValue)
		{
			MainViewModel.Instance.MultiplayerFilterLabelVis = Visibility.Hidden;
		}
		else if (RefMP_SearchFilter.Text.Length == 0)
		{
			MainViewModel.Instance.MultiplayerFilterLabelVis = Visibility.Visible;
		}
	}

	private void FilterTextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			populateMapList(selectedMPHeader, ignoreRefresh: true);
			if (RefMP_SearchFilter.Text.Length == 0)
			{
				MainViewModel.Instance.MultiplayerFilterButtonVis = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.MultiplayerFilterButtonVis = Visibility.Visible;
			}
		}
	}

	private void TextBoxCheckForEscape(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			base.Keyboard.ClearFocus();
			KeyManager.instance.ignoreEscape();
		}
	}

	private void TextBoxEnterCheck(object sender, TextCompositionEventArgs e)
	{
		if (e.Text == "\n")
		{
			e.Handled = true;
			base.Keyboard.ClearFocus();
		}
	}

	private void DetectChatEnter(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			ButtonClicked("SendChat");
		}
	}

	private void EnterShareTextChangedHandler(object sender, RoutedEventArgs e)
	{
		if (!panelActive)
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
			LatestSharedCode = num;
			RefShareJoinButton.IsEnabled = true;
		}
		else
		{
			RefShareJoinButton.IsEnabled = false;
		}
	}

	public void LobbyMaxPlayersSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)RefLobbyMaxPlayersSlider.Value;
			MainViewModel.Instance.MPCreateMaxPlayers = num.ToString();
			PlayerCap = num;
			FRONT_Multiplayer_Setup.Instance.RefSetupMaxPlayersSlider.Value = num;
		}
	}

	public void SetupMaxPlayersSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)FRONT_Multiplayer_Setup.Instance.RefSetupMaxPlayersSlider.Value;
			MainViewModel.Instance.MPCreateMaxPlayers = num.ToString();
		}
	}

	private void UpdateRadarShieldPositions()
	{
		RefRadarShield1.Margin = GameData.Instance.getKeepPosition(0, scaled: true);
		RefRadarShield2.Margin = GameData.Instance.getKeepPosition(1, scaled: true);
		RefRadarShield3.Margin = GameData.Instance.getKeepPosition(2, scaled: true);
		RefRadarShield4.Margin = GameData.Instance.getKeepPosition(3, scaled: true);
		RefRadarShield5.Margin = GameData.Instance.getKeepPosition(4, scaled: true);
		RefRadarShield6.Margin = GameData.Instance.getKeepPosition(5, scaled: true);
		RefRadarShield7.Margin = GameData.Instance.getKeepPosition(6, scaled: true);
		RefRadarShield8.Margin = GameData.Instance.getKeepPosition(7, scaled: true);
		RefRadarShieldFace1.Margin = new Thickness(RefRadarShield1.Margin.Left - 4f, RefRadarShield1.Margin.Top - 4f, -1000f, -1000f);
		RefRadarShieldFace2.Margin = new Thickness(RefRadarShield2.Margin.Left - 4f, RefRadarShield2.Margin.Top - 4f, -1000f, -1000f);
		RefRadarShieldFace3.Margin = new Thickness(RefRadarShield3.Margin.Left - 4f, RefRadarShield3.Margin.Top - 4f, -1000f, -1000f);
		RefRadarShieldFace4.Margin = new Thickness(RefRadarShield4.Margin.Left - 4f, RefRadarShield4.Margin.Top - 4f, -1000f, -1000f);
		RefRadarShieldFace5.Margin = new Thickness(RefRadarShield5.Margin.Left - 4f, RefRadarShield5.Margin.Top - 4f, -1000f, -1000f);
		RefRadarShieldFace6.Margin = new Thickness(RefRadarShield6.Margin.Left - 4f, RefRadarShield6.Margin.Top - 4f, -1000f, -1000f);
		RefRadarShieldFace7.Margin = new Thickness(RefRadarShield7.Margin.Left - 4f, RefRadarShield7.Margin.Top - 4f, -1000f, -1000f);
		RefRadarShieldFace8.Margin = new Thickness(RefRadarShield8.Margin.Left - 4f, RefRadarShield8.Margin.Top - 4f, -1000f, -1000f);
		RefRadarShieldTeam1.Margin = new Thickness(RefRadarShield1.Margin.Left + 14f, RefRadarShield1.Margin.Top + 8f, -1000f, -1000f);
		RefRadarShieldTeam2.Margin = new Thickness(RefRadarShield2.Margin.Left + 14f, RefRadarShield2.Margin.Top + 8f, -1000f, -1000f);
		RefRadarShieldTeam3.Margin = new Thickness(RefRadarShield3.Margin.Left + 14f, RefRadarShield3.Margin.Top + 8f, -1000f, -1000f);
		RefRadarShieldTeam4.Margin = new Thickness(RefRadarShield4.Margin.Left + 14f, RefRadarShield4.Margin.Top + 8f, -1000f, -1000f);
		RefRadarShieldTeam5.Margin = new Thickness(RefRadarShield5.Margin.Left + 14f, RefRadarShield5.Margin.Top + 8f, -1000f, -1000f);
		RefRadarShieldTeam6.Margin = new Thickness(RefRadarShield6.Margin.Left + 14f, RefRadarShield6.Margin.Top + 8f, -1000f, -1000f);
		RefRadarShieldTeam7.Margin = new Thickness(RefRadarShield7.Margin.Left + 14f, RefRadarShield7.Margin.Top + 8f, -1000f, -1000f);
		RefRadarShieldTeam8.Margin = new Thickness(RefRadarShield8.Margin.Left + 14f, RefRadarShield8.Margin.Top + 8f, -1000f, -1000f);
		if (SelectedRadarKeep < 0)
		{
			RefFloatingRadarShield.Source = null;
		}
		if (SelectedRadarKeep != 0)
		{
			PropEx.SetSprite1(RefRadarShield1, getKeepShield(0));
			PropEx.SetSprite2(RefRadarShield1, getKeepShield(0, hightlighted: true));
			PropEx.SetSprite3(RefRadarShield1, getKeepShield(0, hightlighted: true));
			PropEx.SetSprite4(RefRadarShield1, getKeepShield(0));
			RefRadarShieldTeam1.Source = getKeepTeamShield(0);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(0);
			PropEx.SetSprite1(RefRadarShield1, null);
			PropEx.SetSprite2(RefRadarShield1, null);
			PropEx.SetSprite3(RefRadarShield1, null);
			PropEx.SetSprite4(RefRadarShield1, null);
			RefRadarShieldTeam1.Source = null;
		}
		if (SelectedRadarKeep != 1)
		{
			PropEx.SetSprite1(RefRadarShield2, getKeepShield(1));
			PropEx.SetSprite2(RefRadarShield2, getKeepShield(1, hightlighted: true));
			PropEx.SetSprite3(RefRadarShield2, getKeepShield(1, hightlighted: true));
			PropEx.SetSprite4(RefRadarShield2, getKeepShield(1));
			RefRadarShieldTeam2.Source = getKeepTeamShield(1);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(1);
			PropEx.SetSprite1(RefRadarShield2, null);
			PropEx.SetSprite2(RefRadarShield2, null);
			PropEx.SetSprite3(RefRadarShield2, null);
			PropEx.SetSprite4(RefRadarShield2, null);
			RefRadarShieldTeam2.Source = null;
		}
		if (SelectedRadarKeep != 2)
		{
			PropEx.SetSprite1(RefRadarShield3, getKeepShield(2));
			PropEx.SetSprite2(RefRadarShield3, getKeepShield(2, hightlighted: true));
			PropEx.SetSprite3(RefRadarShield3, getKeepShield(2, hightlighted: true));
			PropEx.SetSprite4(RefRadarShield3, getKeepShield(2));
			RefRadarShieldTeam3.Source = getKeepTeamShield(2);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(2);
			PropEx.SetSprite1(RefRadarShield3, null);
			PropEx.SetSprite2(RefRadarShield3, null);
			PropEx.SetSprite3(RefRadarShield3, null);
			PropEx.SetSprite4(RefRadarShield3, null);
			RefRadarShieldTeam3.Source = null;
		}
		if (SelectedRadarKeep != 3)
		{
			PropEx.SetSprite1(RefRadarShield4, getKeepShield(3));
			PropEx.SetSprite2(RefRadarShield4, getKeepShield(3, hightlighted: true));
			PropEx.SetSprite3(RefRadarShield4, getKeepShield(3, hightlighted: true));
			PropEx.SetSprite4(RefRadarShield4, getKeepShield(3));
			RefRadarShieldTeam4.Source = getKeepTeamShield(3);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(3);
			PropEx.SetSprite1(RefRadarShield4, null);
			PropEx.SetSprite2(RefRadarShield4, null);
			PropEx.SetSprite3(RefRadarShield4, null);
			PropEx.SetSprite4(RefRadarShield4, null);
			RefRadarShieldTeam4.Source = null;
		}
		if (SelectedRadarKeep != 4)
		{
			PropEx.SetSprite1(RefRadarShield5, getKeepShield(4));
			PropEx.SetSprite2(RefRadarShield5, getKeepShield(4, hightlighted: true));
			PropEx.SetSprite3(RefRadarShield5, getKeepShield(4, hightlighted: true));
			PropEx.SetSprite4(RefRadarShield5, getKeepShield(4));
			RefRadarShieldTeam5.Source = getKeepTeamShield(4);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(4);
			PropEx.SetSprite1(RefRadarShield5, null);
			PropEx.SetSprite2(RefRadarShield5, null);
			PropEx.SetSprite3(RefRadarShield5, null);
			PropEx.SetSprite4(RefRadarShield5, null);
			RefRadarShieldTeam5.Source = null;
		}
		if (SelectedRadarKeep != 5)
		{
			PropEx.SetSprite1(RefRadarShield6, getKeepShield(5));
			PropEx.SetSprite2(RefRadarShield6, getKeepShield(5, hightlighted: true));
			PropEx.SetSprite3(RefRadarShield6, getKeepShield(5, hightlighted: true));
			PropEx.SetSprite4(RefRadarShield6, getKeepShield(5));
			RefRadarShieldTeam6.Source = getKeepTeamShield(5);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(5);
			PropEx.SetSprite1(RefRadarShield6, null);
			PropEx.SetSprite2(RefRadarShield6, null);
			PropEx.SetSprite3(RefRadarShield6, null);
			PropEx.SetSprite4(RefRadarShield6, null);
			RefRadarShieldTeam6.Source = null;
		}
		if (SelectedRadarKeep != 6)
		{
			PropEx.SetSprite1(RefRadarShield7, getKeepShield(6));
			PropEx.SetSprite2(RefRadarShield7, getKeepShield(6, hightlighted: true));
			PropEx.SetSprite3(RefRadarShield7, getKeepShield(6, hightlighted: true));
			PropEx.SetSprite4(RefRadarShield7, getKeepShield(6));
			RefRadarShieldTeam7.Source = getKeepTeamShield(6);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(6);
			PropEx.SetSprite1(RefRadarShield7, null);
			PropEx.SetSprite2(RefRadarShield7, null);
			PropEx.SetSprite3(RefRadarShield7, null);
			PropEx.SetSprite4(RefRadarShield7, null);
			RefRadarShieldTeam7.Source = null;
		}
		if (SelectedRadarKeep != 7)
		{
			PropEx.SetSprite1(RefRadarShield8, getKeepShield(7));
			PropEx.SetSprite2(RefRadarShield8, getKeepShield(7, hightlighted: true));
			PropEx.SetSprite3(RefRadarShield8, getKeepShield(7, hightlighted: true));
			PropEx.SetSprite4(RefRadarShield8, getKeepShield(7));
			RefRadarShieldTeam8.Source = getKeepTeamShield(7);
		}
		else
		{
			RefFloatingRadarShield.Source = getKeepShield(7);
			PropEx.SetSprite1(RefRadarShield8, null);
			PropEx.SetSprite2(RefRadarShield8, null);
			PropEx.SetSprite3(RefRadarShield8, null);
			PropEx.SetSprite4(RefRadarShield8, null);
			RefRadarShieldTeam8.Source = null;
		}
		updateRadarFaces();
	}

	private void updateRadarFaces()
	{
		for (int i = 0; i < 8; i++)
		{
			createRadarFace(i);
		}
		MainViewModel.Instance.AlliesFaceX = Platform_Multiplayer.Instance.GetLocalAvatar();
	}

	public ImageSource getKeepShield(int keepID, bool hightlighted = false, bool hideBlank = false)
	{
		int num = MPsetupData.start_keep_location_order[keepID];
		if (num < 0 || num >= 8)
		{
			if (!hideBlank && GameData.Instance.getKeepPosition(keepID).Left > 0f)
			{
				return MainViewModel.Instance.GameSprites[576];
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
		int num = MPsetupData.start_keep_location_order[keepID];
		if (num < 0 || num >= 8)
		{
			_ = GameData.Instance.getKeepPosition(keepID).Left;
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

	private bool updateSteamIDMappings()
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

	private void add_player_to_keep_locations(ulong steamID)
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
		int num3 = new System.Random().Next(num - num2);
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

	private void remove_player_from_keep_locations(ulong steamID)
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

	private void update_keep_locations_on_map_change()
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

	private void StartSkirmishGame(HUD_IngameMenu.RestartSkirmishMapInfo customTrailRestartInfo = null)
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
			else if (FrontendMenus.CurrentSelectedTrail == 24)
			{
				num = 4;
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

	private void ReSortTeamInfo()
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

	private void CreateTeamShields()
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

	private void PopulateTeamsPanel()
	{
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
			UnityEngine.Color color = OnScreenText.Instance.MPTeamColours[MP_orig_remap_colour_order[lobbyMemberFromThis_PlayerID.colourID]];
			MainViewModel.Instance.SkirmishTeamNameColours[k] = new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, (byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f)));
			if (num != -2)
			{
				num = team;
			}
		}
	}

	private bool GetOverButton(Button targetGrid)
	{
		Point point = targetGrid.PointToScreen(new Point(0f, 0f));
		Point point2 = targetGrid.PointToScreen(new Point(targetGrid.ActualWidth, targetGrid.ActualHeight - 1f));
		Vector3 mousePosition = Input.mousePosition;
		if (FatControler.arabic && !ConfigSettings.Settings_ArabicL2R)
		{
			mousePosition.x = (float)Screen.width - mousePosition.x;
		}
		mousePosition.y = (float)Screen.height - mousePosition.y;
		if (mousePosition.x < point.X || mousePosition.x > point2.X)
		{
			return false;
		}
		if (mousePosition.y < point.Y || mousePosition.y > point2.Y)
		{
			return false;
		}
		return true;
	}

	private void InitCoopMissions()
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
		if (CoopTrail4 == null)
		{
			CoopTrail4 = new CoopMissionSetupData[10];
			int num4 = 0;
			CoopMissionSetupData coopMissionSetupData31 = new CoopMissionSetupData();
			coopMissionSetupData31.mapName = "RiverPass";
			coopMissionSetupData31.keepOrder = new int[8] { 1, 2, 4, 5, 6, 3, -1, -1 };
			coopMissionSetupData31.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData31.AIs = new int[4] { 18, 27, 8, 11 };
			coopMissionSetupData31.AIVs = new int[4] { 202, 1105, 404, 1104 };
			coopMissionSetupData31.starting_level = 1;
			coopMissionSetupData31.fairness = 5;
			coopMissionSetupData31.allowMercPostPlayer1 = 0;
			coopMissionSetupData31.allowStockadePlayer1 = 0;
			coopMissionSetupData31.allowBarracksPlayer2 = 0;
			CoopTrail4[num4++] = coopMissionSetupData31;
			CoopMissionSetupData coopMissionSetupData32 = new CoopMissionSetupData();
			coopMissionSetupData32.mapName = "Crocodiles nest";
			coopMissionSetupData32.keepOrder = new int[8] { 2, 1, 3, 4, -1, -1, -1, -1 };
			coopMissionSetupData32.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData32.AIs = new int[2] { 26, 28 };
			coopMissionSetupData32.AIVs = new int[2] { 1305, 1406 };
			coopMissionSetupData32.fairness = 5;
			coopMissionSetupData32.starting_level = 3;
			coopMissionSetupData32.allowMercPostPlayer1 = 0;
			coopMissionSetupData32.allowStockadePlayer2 = 0;
			CoopTrail4[num4++] = coopMissionSetupData32;
			CoopMissionSetupData coopMissionSetupData33 = new CoopMissionSetupData();
			coopMissionSetupData33.mapName = "IronDefence";
			coopMissionSetupData33.keepOrder = new int[8] { 1, 2, 3, 7, 6, 5, 4, -1 };
			coopMissionSetupData33.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData33.AIs = new int[5] { 25, 6, 29, 12, 13 };
			coopMissionSetupData33.AIVs = new int[5] { 202, 305, 303, 1100, 1301 };
			coopMissionSetupData33.fairness = 4;
			coopMissionSetupData33.starting_level = 3;
			coopMissionSetupData33.allowMercPostPlayer1 = 0;
			coopMissionSetupData33.allowStockadePlayer1 = 0;
			coopMissionSetupData33.allowMercPostPlayer2 = 0;
			coopMissionSetupData33.allowStockadePlayer2 = 0;
			CoopTrail4[num4++] = coopMissionSetupData33;
			CoopMissionSetupData coopMissionSetupData34 = new CoopMissionSetupData();
			coopMissionSetupData34.mapName = "TheTreeOfLife";
			coopMissionSetupData34.keepOrder = new int[8] { 1, 2, 3, 4, 5, -1, -1, -1 };
			coopMissionSetupData34.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData34.AIs = new int[3] { 27, 18, 5 };
			coopMissionSetupData34.AIVs = new int[3] { 402, 1403, 304 };
			coopMissionSetupData34.fairness = 4;
			coopMissionSetupData34.starting_level = 3;
			coopMissionSetupData34.allowMercPostPlayer2 = 0;
			coopMissionSetupData34.allowStockadePlayer1 = 0;
			CoopTrail4[num4++] = coopMissionSetupData34;
			CoopMissionSetupData coopMissionSetupData35 = new CoopMissionSetupData();
			coopMissionSetupData35.mapName = "CanyonRidge";
			coopMissionSetupData35.keepOrder = new int[8] { 6, 7, 5, 2, 3, 1, 4, 8 };
			coopMissionSetupData35.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData35.AIs = new int[6] { 6, 22, 9, 24, 4, 19 };
			coopMissionSetupData35.AIVs = new int[6] { 1204, 1100, 405, 204, 1200, 201 };
			coopMissionSetupData35.fairness = 5;
			coopMissionSetupData35.starting_level = 3;
			CoopTrail4[num4++] = coopMissionSetupData35;
			CoopMissionSetupData coopMissionSetupData36 = new CoopMissionSetupData();
			coopMissionSetupData36.mapName = "TheShallows";
			coopMissionSetupData36.keepOrder = new int[8] { 1, 2, 4, 3, -1, -1, -1, -1 };
			coopMissionSetupData36.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData36.AIs = new int[2] { 29, 4 };
			coopMissionSetupData36.AIVs = new int[2] { 1107, 1204 };
			coopMissionSetupData36.fairness = 5;
			coopMissionSetupData36.starting_level = 1;
			CoopTrail4[num4++] = coopMissionSetupData36;
			CoopMissionSetupData coopMissionSetupData37 = new CoopMissionSetupData();
			coopMissionSetupData37.mapName = "OldWounds";
			coopMissionSetupData37.keepOrder = new int[8] { 1, 2, 4, 3, 5, -1, -1, -1 };
			coopMissionSetupData37.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData37.AIs = new int[3] { 4, 16, 14 };
			coopMissionSetupData37.AIVs = new int[3] { 1201, 104, 304 };
			coopMissionSetupData37.fairness = 5;
			coopMissionSetupData37.starting_level = 1;
			coopMissionSetupData37.allowMercPostPlayer1 = 0;
			coopMissionSetupData37.allowStockadePlayer1 = 0;
			coopMissionSetupData37.allowMercPostPlayer2 = 0;
			coopMissionSetupData37.allowStockadePlayer2 = 0;
			CoopTrail4[num4++] = coopMissionSetupData37;
			CoopMissionSetupData coopMissionSetupData38 = new CoopMissionSetupData();
			coopMissionSetupData38.mapName = "OverlookPlateau";
			coopMissionSetupData38.keepOrder = new int[8] { 1, 2, 3, 5, 6, 7, 4, -1 };
			coopMissionSetupData38.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData38.AIs = new int[5] { 25, 26, 2, 12, 3 };
			coopMissionSetupData38.AIVs = new int[5] { 207, 1204, 105, 105, 402 };
			coopMissionSetupData38.fairness = 5;
			coopMissionSetupData38.starting_level = 3;
			coopMissionSetupData38.allowBarracksPlayer1 = 0;
			coopMissionSetupData38.allowMercPostPlayer2 = 0;
			coopMissionSetupData38.allowStockadePlayer2 = 0;
			CoopTrail4[num4++] = coopMissionSetupData38;
			CoopMissionSetupData coopMissionSetupData39 = new CoopMissionSetupData();
			coopMissionSetupData39.mapName = "ABridgeApart";
			coopMissionSetupData39.keepOrder = new int[8] { 1, 2, 4, 3, 5, -1, -1, -1 };
			coopMissionSetupData39.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData39.AIs = new int[3] { 28, 24, 9 };
			coopMissionSetupData39.AIVs = new int[3] { 103, 1205, 1205 };
			coopMissionSetupData39.fairness = 4;
			coopMissionSetupData39.starting_level = 3;
			coopMissionSetupData39.allowBarracksPlayer1 = 0;
			coopMissionSetupData39.allowBarracksPlayer2 = 0;
			CoopTrail4[num4++] = coopMissionSetupData39;
			CoopMissionSetupData coopMissionSetupData40 = new CoopMissionSetupData();
			coopMissionSetupData40.mapName = "CrusadesCrossing";
			coopMissionSetupData40.keepOrder = new int[8] { 1, 2, 5, 8, 3, 4, 6, 7 };
			coopMissionSetupData40.teams = new int[8] { 1, 1, 2, 2, 2, 2, 2, 2 };
			coopMissionSetupData40.AIs = new int[6] { 5, 23, 17, 21, 8, 4 };
			coopMissionSetupData40.AIVs = new int[6] { 202, 104, 205, 204, 202, 1206 };
			coopMissionSetupData40.fairness = 4;
			coopMissionSetupData40.starting_level = 3;
			CoopTrail4[num4++] = coopMissionSetupData40;
		}
		CoopMissionSetupData[] coopTrail = CoopTrail1;
		foreach (CoopMissionSetupData coopMissionSetupData41 in coopTrail)
		{
			if (coopMissionSetupData41 != null)
			{
				coopMissionSetupData41.header = MapFileManager.Instance.GetHeaderFromFileNameMP(coopMissionSetupData41.mapName);
			}
		}
		coopTrail = CoopTrail2;
		foreach (CoopMissionSetupData coopMissionSetupData42 in coopTrail)
		{
			if (coopMissionSetupData42 != null)
			{
				coopMissionSetupData42.header = MapFileManager.Instance.GetHeaderFromFileNameMP(coopMissionSetupData42.mapName);
			}
		}
		coopTrail = CoopTrail3;
		foreach (CoopMissionSetupData coopMissionSetupData43 in coopTrail)
		{
			if (coopMissionSetupData43 != null)
			{
				coopMissionSetupData43.header = MapFileManager.Instance.GetHeaderFromFileNameMP(coopMissionSetupData43.mapName);
			}
		}
		coopTrail = CoopTrail4;
		foreach (CoopMissionSetupData coopMissionSetupData44 in coopTrail)
		{
			if (coopMissionSetupData44 != null)
			{
				coopMissionSetupData44.header = MapFileManager.Instance.GetHeaderFromFileNameMP(coopMissionSetupData44.mapName);
			}
		}
	}

	private void ClearCoopAIs()
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
		case 3:
			Platform_Multiplayer.Instance.SetCoopTrailProgress(3, ConfigSettings.Settings_Progress_Trail_Coop4_Status, missionID, ConfigSettings.Settings_Progress_Trail_Coop4, coopOrderSwapped);
			coopMissionSetupData = CoopTrail4[missionID - 1];
			MainViewModel.Instance.CoopMissionTitle = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_COOP, missionID + 35);
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
		MPsetupData.global_improved_sieging2 = 0;
		for (int k = 0; k < MPsetupData.MP_BuildingsAvailable.Length; k++)
		{
			MPsetupData.MP_BuildingsAvailable[k] = 1;
		}
		if (!singlePlayerCoop)
		{
			if (currentLobby.isHost)
			{
				if (!coopOrderSwapped)
				{
					MPsetupData.MP_BuildingsAvailable[0] = coopMissionSetupData.allowBarracksPlayer1;
					MPsetupData.MP_BuildingsAvailable[1] = coopMissionSetupData.allowMercPostPlayer1;
					MPsetupData.MP_BuildingsAvailable[2] = coopMissionSetupData.allowStockadePlayer1;
				}
				else
				{
					MPsetupData.MP_BuildingsAvailable[0] = coopMissionSetupData.allowBarracksPlayer2;
					MPsetupData.MP_BuildingsAvailable[1] = coopMissionSetupData.allowMercPostPlayer2;
					MPsetupData.MP_BuildingsAvailable[2] = coopMissionSetupData.allowStockadePlayer2;
				}
			}
			else if (!coopOrderSwapped)
			{
				MPsetupData.MP_BuildingsAvailable[0] = coopMissionSetupData.allowBarracksPlayer2;
				MPsetupData.MP_BuildingsAvailable[1] = coopMissionSetupData.allowMercPostPlayer2;
				MPsetupData.MP_BuildingsAvailable[2] = coopMissionSetupData.allowStockadePlayer2;
			}
			else
			{
				MPsetupData.MP_BuildingsAvailable[0] = coopMissionSetupData.allowBarracksPlayer1;
				MPsetupData.MP_BuildingsAvailable[1] = coopMissionSetupData.allowMercPostPlayer1;
				MPsetupData.MP_BuildingsAvailable[2] = coopMissionSetupData.allowStockadePlayer1;
			}
		}
		for (int l = 0; l < 8; l++)
		{
			MPsetupData.start_keep_location_order[l] = -1;
		}
		for (int m = 0; m < 8; m++)
		{
			if (coopOrderSwapped && m < 2)
			{
				if (coopMissionSetupData.keepOrder[m ^ 1] > 0)
				{
					MPsetupData.start_keep_location_order[coopMissionSetupData.keepOrder[m ^ 1] - 1] = m;
				}
			}
			else if (coopMissionSetupData.keepOrder[m] > 0)
			{
				MPsetupData.start_keep_location_order[coopMissionSetupData.keepOrder[m] - 1] = m;
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
			case 3:
				FRONT_CoopTrail4.Instance.UpdateRadarShieldPositions();
				break;
			}
		}
		UpdateHostInfo();
		updateRadarTexture(coopMissionSetupData.header);
	}

	private void PopulateMapDetailsPanel(FileHeader header)
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

	private ImageSource requestAvatar(int _row, ulong _steamID)
	{
		ImageSource userAvatar = Platform_Multiplayer.Instance.GetUserAvatar(_steamID);
		if (userAvatar != null)
		{
			return userAvatar;
		}
		Platform_Multiplayer.Instance.RequestUserAvatar(_steamID);
		userAvatar = Platform_Multiplayer.Instance.GetUserAvatar(_steamID);
		if (userAvatar != null)
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
				coopShowHiddenFriends = FRONT_CoopTrail1.Instance.RefShowHidden.IsChecked.Value;
			}
			else if (FrontendMenus.CurrentSelectedTrail == 22)
			{
				coopShowHiddenFriends = FRONT_CoopTrail2.Instance.RefShowHidden.IsChecked.Value;
			}
			else if (FrontendMenus.CurrentSelectedTrail == 23)
			{
				coopShowHiddenFriends = FRONT_CoopTrail3.Instance.RefShowHidden.IsChecked.Value;
			}
			else if (FrontendMenus.CurrentSelectedTrail == 24)
			{
				coopShowHiddenFriends = FRONT_CoopTrail4.Instance.RefShowHidden.IsChecked.Value;
			}
			coopFriendsPage = 0;
			CoopPopulateFriendsList();
		}
	}

	private void CoopPopulateFriendsList()
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
		else if (FrontendMenus.CurrentSelectedTrail == 24)
		{
			trailID = 3;
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

	private void SetCoopRow(int row, string name, ulong steamID, ImageSource avatar, bool hidden)
	{
		coopFriendsSteamIDs[row] = steamID;
		coopFriendsRowHidden[row] = hidden;
		switch (row)
		{
		case 0:
			MainViewModel.Instance.Coop_Name_1 = name;
			MainViewModel.Instance.Coop_Image_1 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_1 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_1 = Visibility.Hidden;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_1 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_1 = Visibility.Hidden;
			}
			if (MainViewModel.Instance.Coop_Continue_Line_1 == Visibility.Hidden && MainViewModel.Instance.Coop_Invite_Line_1 == Visibility.Hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_1 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_1 = Visibility.Hidden;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_1 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_1 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_1 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_1 = Visibility.Visible;
			}
			break;
		case 1:
			MainViewModel.Instance.Coop_Name_2 = name;
			MainViewModel.Instance.Coop_Image_2 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_2 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_2 = Visibility.Hidden;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_2 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_2 = Visibility.Hidden;
			}
			if (MainViewModel.Instance.Coop_Continue_Line_2 == Visibility.Hidden && MainViewModel.Instance.Coop_Invite_Line_2 == Visibility.Hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_2 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_2 = Visibility.Hidden;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_2 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_2 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_2 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_2 = Visibility.Visible;
			}
			break;
		case 2:
			MainViewModel.Instance.Coop_Name_3 = name;
			MainViewModel.Instance.Coop_Image_3 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_3 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_3 = Visibility.Hidden;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_3 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_3 = Visibility.Hidden;
			}
			if (MainViewModel.Instance.Coop_Continue_Line_3 == Visibility.Hidden && MainViewModel.Instance.Coop_Invite_Line_3 == Visibility.Hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_3 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_3 = Visibility.Hidden;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_3 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_3 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_3 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_3 = Visibility.Visible;
			}
			break;
		case 3:
			MainViewModel.Instance.Coop_Name_4 = name;
			MainViewModel.Instance.Coop_Image_4 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_4 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_4 = Visibility.Hidden;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_4 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_4 = Visibility.Hidden;
			}
			if (MainViewModel.Instance.Coop_Continue_Line_4 == Visibility.Hidden && MainViewModel.Instance.Coop_Invite_Line_4 == Visibility.Hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_4 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_4 = Visibility.Hidden;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_4 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_4 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_4 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_4 = Visibility.Visible;
			}
			break;
		case 4:
			MainViewModel.Instance.Coop_Name_5 = name;
			MainViewModel.Instance.Coop_Image_5 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_5 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_5 = Visibility.Hidden;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_5 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_5 = Visibility.Hidden;
			}
			if (MainViewModel.Instance.Coop_Continue_Line_5 == Visibility.Hidden && MainViewModel.Instance.Coop_Invite_Line_5 == Visibility.Hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_5 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_5 = Visibility.Hidden;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_5 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_5 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_5 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_5 = Visibility.Visible;
			}
			break;
		case 5:
			MainViewModel.Instance.Coop_Name_6 = name;
			MainViewModel.Instance.Coop_Image_6 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_6 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_6 = Visibility.Hidden;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_6 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_6 = Visibility.Hidden;
			}
			if (MainViewModel.Instance.Coop_Continue_Line_6 == Visibility.Hidden && MainViewModel.Instance.Coop_Invite_Line_6 == Visibility.Hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_6 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_6 = Visibility.Hidden;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_6 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_6 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_6 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_6 = Visibility.Visible;
			}
			break;
		case 6:
			MainViewModel.Instance.Coop_Name_7 = name;
			MainViewModel.Instance.Coop_Image_7 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_7 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_7 = Visibility.Hidden;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_7 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_7 = Visibility.Hidden;
			}
			if (MainViewModel.Instance.Coop_Continue_Line_7 == Visibility.Hidden && MainViewModel.Instance.Coop_Invite_Line_7 == Visibility.Hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_7 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_7 = Visibility.Hidden;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_7 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_7 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_7 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_7 = Visibility.Visible;
			}
			break;
		case 7:
			MainViewModel.Instance.Coop_Name_8 = name;
			MainViewModel.Instance.Coop_Image_8 = avatar;
			if (steamID < 2000 && steamID != 0)
			{
				MainViewModel.Instance.Coop_Continue_Line_8 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Continue_Line_8 = Visibility.Hidden;
			}
			if (steamID >= 2000)
			{
				MainViewModel.Instance.Coop_Invite_Line_8 = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.Coop_Invite_Line_8 = Visibility.Hidden;
			}
			if (MainViewModel.Instance.Coop_Continue_Line_8 == Visibility.Hidden && MainViewModel.Instance.Coop_Invite_Line_8 == Visibility.Hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_8 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_8 = Visibility.Hidden;
			}
			else if (hidden)
			{
				MainViewModel.Instance.Coop_Show_Line_8 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_8 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_8 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_8 = Visibility.Visible;
			}
			break;
		}
	}

	private void SetCoopRowAvatar(int row, ImageSource avatar)
	{
		switch (row)
		{
		case 0:
			MainViewModel.Instance.Coop_Image_1 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_1 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_1 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_1 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_1 = Visibility.Visible;
			}
			break;
		case 1:
			MainViewModel.Instance.Coop_Image_2 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_2 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_2 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_2 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_2 = Visibility.Visible;
			}
			break;
		case 2:
			MainViewModel.Instance.Coop_Image_3 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_3 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_3 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_3 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_3 = Visibility.Visible;
			}
			break;
		case 3:
			MainViewModel.Instance.Coop_Image_4 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_4 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_4 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_4 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_4 = Visibility.Visible;
			}
			break;
		case 4:
			MainViewModel.Instance.Coop_Image_5 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_5 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_5 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_5 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_5 = Visibility.Visible;
			}
			break;
		case 5:
			MainViewModel.Instance.Coop_Image_6 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_6 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_6 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_6 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_6 = Visibility.Visible;
			}
			break;
		case 6:
			MainViewModel.Instance.Coop_Image_7 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_7 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_7 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_7 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_7 = Visibility.Visible;
			}
			break;
		case 7:
			MainViewModel.Instance.Coop_Image_8 = avatar;
			if (coopFriendsRowHidden[row])
			{
				MainViewModel.Instance.Coop_Show_Line_8 = Visibility.Visible;
				MainViewModel.Instance.Coop_Hide_Line_8 = Visibility.Hidden;
			}
			else
			{
				MainViewModel.Instance.Coop_Show_Line_8 = Visibility.Hidden;
				MainViewModel.Instance.Coop_Hide_Line_8 = Visibility.Visible;
			}
			break;
		}
	}
}
