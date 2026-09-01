using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Noesis;
using Steamworks;
using UnityEngine;

namespace CrusaderDE;

public class HUD_Options : UserControl
{
	private class HotKeyEntry
	{
		public Enums.KeyFunctions function;

		public int textID;

		public HotKeyEntry(Enums.KeyFunctions f, int t)
		{
			function = f;
			textID = t;
		}
	}

	public static int from = 0;

	private int menuSection;

	private bool panelActive;

	private Noesis.Grid RefVideoSettings;

	private Noesis.Grid RefSoundSettings;

	private Noesis.Grid RefKeySettings;

	private Noesis.Grid RefControlSettings;

	private Noesis.Grid RefNameSettings;

	private Noesis.Grid RefCoASettings;

	private Noesis.Grid RefCheatSettings;

	private Button RefCOA_Button;

	private ComboBox RefResolutionCombo;

	private ComboBox RefScreenModeCombo;

	private Slider RefMasterVolumeSlider;

	private Slider RefMusicVolumeSlider;

	private Slider RefSpeechVolumeSlider;

	private Slider RefUnitSpeechVolumeSlider;

	private Slider RefSFXVolumeSlider;

	private Slider RefScrollSpeedSlider;

	private Slider RefGameSpeedSlider;

	private Slider RefUIScaleSlider;

	private TextBox RefTextBoxChangeName;

	private CheckBox RefVSyncCheck;

	private CheckBox RefLockCursorCheck;

	private CheckBox RefBuildingTooltipsCheck;

	private TextBlock RefBuildingTooltipsCheckText;

	private CheckBox RefSteamHelpCheck;

	private TextBlock RefSteamHelpCheckText;

	private CheckBox RefCompassCheck;

	private CheckBox RefLocalTimeCheck;

	private CheckBox RefGameTimeCheck;

	private CheckBox RefSandsTimerCheck;

	private CheckBox RefRadarZoomCheck;

	private TextBlock RefRadarZoomCheckText;

	private TextBlock RefSteamIdentity;

	private CheckBox RefCustomIntros;

	private TextBlock RefCustomIntrosName;

	private CheckBox RefUISoundsCheck;

	private CheckBox RefGenieSpeechCheck;

	private CheckBox RefEnglishSpeechCheck;

	private CheckBox RefMuteInsultSpeechCheck;

	private TextBlock RefMuteInsultSpeechCheckText;

	private CheckBox RefMuteInsultsCheck;

	private TextBlock RefMuteInsultsCheckText;

	private CheckBox RefMuteBackgroundCheck;

	private TextBlock RefMuteBackgroundCheckText;

	private CheckBox RefReduceSoundsCheck;

	private TextBlock RefReduceSoundsCheckText;

	private CheckBox RefCheatKeysCheck;

	private TextBlock RefPingsCheckText;

	private CheckBox RefPingsCheck;

	private TextBlock RefExtraZoomCheckText;

	private CheckBox RefExtraZoomCheck;

	private Button RefSFXDefaultsButton;

	private CheckBox RefShowMoatCheck;

	private TextBlock RefShowMoatText;

	private CheckBox RefConfirmDisbandCheck;

	private TextBlock RefConfirmDisbandText;

	private CheckBox RefTroopMoveCheck;

	private CheckBox RefLeaderboard_OptOut;

	private CheckBox RefLeaderboard_Names;

	private CheckBox RefLeaderboard_Images;

	private CheckBox RefNewsletterCheck;

	private CheckBox RefSandsTimeDisable;

	private CheckBox RefChatMuteDisable;

	private CheckBox RefArabicL2RCheck;

	private TextBlock RefLeaderboard_OptOutText;

	private TextBlock RefLeaderboard_NamesText;

	private TextBlock RefLeaderboard_ImagesText;

	private Button RefNewsletterSignupButton;

	private TextBox RefTextBoxNewsletter;

	private Image RefScribeLock;

	private Button RefOptionsChickenButton;

	private TextBlock RefPlayerSettingsHeading;

	private RadioButton RefPlayerColourShield1;

	private RadioButton RefPlayerColourShield2;

	private RadioButton RefPlayerColourShield3;

	private RadioButton RefPlayerColourShield4;

	private RadioButton RefPlayerColourShield5;

	private RadioButton RefPlayerColourShield6;

	private RadioButton RefPlayerColourShield7;

	private RadioButton RefPlayerColourShield8;

	public Noesis.Grid RefOptionsHotKeyPanel;

	private Noesis.Grid RefUIScaleGrid;

	private ListView RefHotKeyList;

	private Button RefOptionsHotKeyNewKeyApply;

	private Button RefCursorSystemButton;

	private Button RefCursorSwordButton;

	private Button RefCursorSwordXButton;

	private Button RefCursorSwordX2Button;

	private Button RefScribeClassicButton;

	private Button RefScribeModernButton;

	private Button RefCrusaderLordButton;

	private Button RefArabicLordButton;

	private Button RefBedouinLordButton;

	private Button RefScribeLordButton;

	private Button RefFemaleLordButton;

	private Button RefBessyLordButton;

	private Button RefArabicLordFemaleButton;

	private Button RefBedouinLordFemaleButton;

	private Button RefOptionsKeys1;

	private Button RefOptionsKeys2;

	private static HUD_Options instance1 = null;

	private static HUD_Options instance2 = null;

	private static HUD_Options instance3 = null;

	private ObservableCollection<HotKeyRow> hotKeyRows = new ObservableCollection<HotKeyRow>();

	private Enums.KeyFunctions selectedFunction = Enums.KeyFunctions.NumActions;

	private int selectedColumn = -1;

	private bool resChanged;

	private bool screenModeChanged;

	private DateTime lastDynamicChanged = DateTime.MaxValue;

	private static Regex _regex = CreateRegEx();

	private HotKeyEntry[] hotKeyList = new HotKeyEntry[179]
	{
		new HotKeyEntry(Enums.KeyFunctions.Left, 1),
		new HotKeyEntry(Enums.KeyFunctions.Right, 2),
		new HotKeyEntry(Enums.KeyFunctions.Up, 3),
		new HotKeyEntry(Enums.KeyFunctions.Down, 4),
		new HotKeyEntry(Enums.KeyFunctions.Pause, 5),
		new HotKeyEntry(Enums.KeyFunctions.HomeKeep, 6),
		new HotKeyEntry(Enums.KeyFunctions.Market, 7),
		new HotKeyEntry(Enums.KeyFunctions.Signpost, 8),
		new HotKeyEntry(Enums.KeyFunctions.Barracks, 9),
		new HotKeyEntry(Enums.KeyFunctions.MercPost, 113),
		new HotKeyEntry(Enums.KeyFunctions.BedouinStockade, 115),
		new HotKeyEntry(Enums.KeyFunctions.Granary, 10),
		new HotKeyEntry(Enums.KeyFunctions.Armoury, 125),
		new HotKeyEntry(Enums.KeyFunctions.EngineersGuild, 126),
		new HotKeyEntry(Enums.KeyFunctions.TunnelersGuild, 127),
		new HotKeyEntry(Enums.KeyFunctions.Cathedral, 132),
		new HotKeyEntry(Enums.KeyFunctions.Lord, 106),
		new HotKeyEntry(Enums.KeyFunctions.CycleLord, 107),
		new HotKeyEntry(Enums.KeyFunctions.MapRotateLeft, 11),
		new HotKeyEntry(Enums.KeyFunctions.MapRotateRight, 12),
		new HotKeyEntry(Enums.KeyFunctions.FlattenLandscape, 13),
		new HotKeyEntry(Enums.KeyFunctions.ZoomOut, 14),
		new HotKeyEntry(Enums.KeyFunctions.ZoomIn, 15),
		new HotKeyEntry(Enums.KeyFunctions.Patrol, 16),
		new HotKeyEntry(Enums.KeyFunctions.StanceStand, 134),
		new HotKeyEntry(Enums.KeyFunctions.StanceDefensive, 135),
		new HotKeyEntry(Enums.KeyFunctions.StanceAggressive, 136),
		new HotKeyEntry(Enums.KeyFunctions.Stop, 128),
		new HotKeyEntry(Enums.KeyFunctions.AttackHere, 130),
		new HotKeyEntry(Enums.KeyFunctions.RotateBuilding, 108),
		new HotKeyEntry(Enums.KeyFunctions.Load, 17),
		new HotKeyEntry(Enums.KeyFunctions.Save, 18),
		new HotKeyEntry(Enums.KeyFunctions.IncreaseEngineSpeed, 19),
		new HotKeyEntry(Enums.KeyFunctions.DecreaseEngineSpeed, 20),
		new HotKeyEntry(Enums.KeyFunctions.ToggleUI, 21),
		new HotKeyEntry(Enums.KeyFunctions.ToggleFrameRate, 22),
		new HotKeyEntry(Enums.KeyFunctions.RadarZoomIn, 101),
		new HotKeyEntry(Enums.KeyFunctions.RadarZoomOut, 102),
		new HotKeyEntry(Enums.KeyFunctions.FreeBuildEvents, 23),
		new HotKeyEntry(Enums.KeyFunctions.ToggleObjectives, 104),
		new HotKeyEntry(Enums.KeyFunctions.ToggleGoods, 103),
		new HotKeyEntry(Enums.KeyFunctions.AllyToggle, 114),
		new HotKeyEntry(Enums.KeyFunctions.ToggleHealthBars, 124),
		new HotKeyEntry(Enums.KeyFunctions.QuickSave, 105),
		new HotKeyEntry(Enums.KeyFunctions.OpenChat, 24),
		new HotKeyEntry(Enums.KeyFunctions.ShowPings, 25),
		new HotKeyEntry(Enums.KeyFunctions.MPPing, 133),
		new HotKeyEntry(Enums.KeyFunctions.GroupTroops0, 26),
		new HotKeyEntry(Enums.KeyFunctions.GroupTroops1, 27),
		new HotKeyEntry(Enums.KeyFunctions.GroupTroops2, 28),
		new HotKeyEntry(Enums.KeyFunctions.GroupTroops3, 29),
		new HotKeyEntry(Enums.KeyFunctions.GroupTroops4, 30),
		new HotKeyEntry(Enums.KeyFunctions.GroupTroops5, 31),
		new HotKeyEntry(Enums.KeyFunctions.GroupTroops6, 32),
		new HotKeyEntry(Enums.KeyFunctions.GroupTroops7, 33),
		new HotKeyEntry(Enums.KeyFunctions.GroupTroops8, 34),
		new HotKeyEntry(Enums.KeyFunctions.GroupTroops9, 35),
		new HotKeyEntry(Enums.KeyFunctions.SelectClan0, 36),
		new HotKeyEntry(Enums.KeyFunctions.SelectClan1, 37),
		new HotKeyEntry(Enums.KeyFunctions.SelectClan2, 38),
		new HotKeyEntry(Enums.KeyFunctions.SelectClan3, 39),
		new HotKeyEntry(Enums.KeyFunctions.SelectClan4, 40),
		new HotKeyEntry(Enums.KeyFunctions.SelectClan5, 41),
		new HotKeyEntry(Enums.KeyFunctions.SelectClan6, 42),
		new HotKeyEntry(Enums.KeyFunctions.SelectClan7, 43),
		new HotKeyEntry(Enums.KeyFunctions.SelectClan8, 44),
		new HotKeyEntry(Enums.KeyFunctions.SelectClan9, 45),
		new HotKeyEntry(Enums.KeyFunctions.SetBookmark0, 46),
		new HotKeyEntry(Enums.KeyFunctions.SetBookmark1, 47),
		new HotKeyEntry(Enums.KeyFunctions.SetBookmark2, 48),
		new HotKeyEntry(Enums.KeyFunctions.SetBookmark3, 49),
		new HotKeyEntry(Enums.KeyFunctions.SetBookmark4, 50),
		new HotKeyEntry(Enums.KeyFunctions.SetBookmark5, 51),
		new HotKeyEntry(Enums.KeyFunctions.SetBookmark6, 52),
		new HotKeyEntry(Enums.KeyFunctions.SetBookmark7, 53),
		new HotKeyEntry(Enums.KeyFunctions.SetBookmark8, 54),
		new HotKeyEntry(Enums.KeyFunctions.SetBookmark9, 55),
		new HotKeyEntry(Enums.KeyFunctions.GotoBookmark0, 56),
		new HotKeyEntry(Enums.KeyFunctions.GotoBookmark1, 57),
		new HotKeyEntry(Enums.KeyFunctions.GotoBookmark2, 58),
		new HotKeyEntry(Enums.KeyFunctions.GotoBookmark3, 59),
		new HotKeyEntry(Enums.KeyFunctions.GotoBookmark4, 60),
		new HotKeyEntry(Enums.KeyFunctions.GotoBookmark5, 61),
		new HotKeyEntry(Enums.KeyFunctions.GotoBookmark6, 62),
		new HotKeyEntry(Enums.KeyFunctions.GotoBookmark7, 63),
		new HotKeyEntry(Enums.KeyFunctions.GotoBookmark8, 64),
		new HotKeyEntry(Enums.KeyFunctions.GotoBookmark9, 65),
		new HotKeyEntry(Enums.KeyFunctions.Cheat_gold, 110),
		new HotKeyEntry(Enums.KeyFunctions.Cheat_freestuff, 111),
		new HotKeyEntry(Enums.KeyFunctions.ExtremePower1, 116),
		new HotKeyEntry(Enums.KeyFunctions.ExtremePower2, 117),
		new HotKeyEntry(Enums.KeyFunctions.ExtremePower3, 118),
		new HotKeyEntry(Enums.KeyFunctions.ExtremePower4, 119),
		new HotKeyEntry(Enums.KeyFunctions.ExtremePower5, 120),
		new HotKeyEntry(Enums.KeyFunctions.ExtremePower6, 121),
		new HotKeyEntry(Enums.KeyFunctions.ExtremePower7, 122),
		new HotKeyEntry(Enums.KeyFunctions.ExtremePower8, 123),
		new HotKeyEntry(Enums.KeyFunctions.EditorHoldTime, 98),
		new HotKeyEntry(Enums.KeyFunctions.EditorRespawnLord, 99),
		new HotKeyEntry(Enums.KeyFunctions.EditorWipeAnimals, 100),
		new HotKeyEntry(Enums.KeyFunctions.EditorShowConnections, 131),
		new HotKeyEntry(Enums.KeyFunctions.PlaceStairs, -1),
		new HotKeyEntry(Enums.KeyFunctions.PlaceLowWalls, -2),
		new HotKeyEntry(Enums.KeyFunctions.PlaceWalls, -3),
		new HotKeyEntry(Enums.KeyFunctions.PlaceCrenal, -4),
		new HotKeyEntry(Enums.KeyFunctions.PlaceBarracks, -5),
		new HotKeyEntry(Enums.KeyFunctions.PlaceMercPost, -6),
		new HotKeyEntry(Enums.KeyFunctions.PlaceBedouinStockade, -7),
		new HotKeyEntry(Enums.KeyFunctions.PlaceArmoury, -8),
		new HotKeyEntry(Enums.KeyFunctions.PlaceTower1, -9),
		new HotKeyEntry(Enums.KeyFunctions.PlaceTower2, -10),
		new HotKeyEntry(Enums.KeyFunctions.PlaceTower3, -11),
		new HotKeyEntry(Enums.KeyFunctions.PlaceTower4, -12),
		new HotKeyEntry(Enums.KeyFunctions.PlaceTower5, -13),
		new HotKeyEntry(Enums.KeyFunctions.PlaceEngineersGuild, -14),
		new HotKeyEntry(Enums.KeyFunctions.PlaceTunnelGuild, -15),
		new HotKeyEntry(Enums.KeyFunctions.PlaceBallista, -17),
		new HotKeyEntry(Enums.KeyFunctions.PlaceMangonel, -16),
		new HotKeyEntry(Enums.KeyFunctions.PlaceStables, -18),
		new HotKeyEntry(Enums.KeyFunctions.PlaceSmelter, -19),
		new HotKeyEntry(Enums.KeyFunctions.PlaceSmallGatehouse, -20),
		new HotKeyEntry(Enums.KeyFunctions.PlaceLargeGatehouse, -21),
		new HotKeyEntry(Enums.KeyFunctions.PlaceDrawbridge, -22),
		new HotKeyEntry(Enums.KeyFunctions.PlaceDogCage, -23),
		new HotKeyEntry(Enums.KeyFunctions.PlacePitchDitch, -24),
		new HotKeyEntry(Enums.KeyFunctions.PlaceKillingPit, -25),
		new HotKeyEntry(Enums.KeyFunctions.PlaceBrazier, -26),
		new HotKeyEntry(Enums.KeyFunctions.PlaceDigMoat, -27),
		new HotKeyEntry(Enums.KeyFunctions.PlaceClearMoat, -28),
		new HotKeyEntry(Enums.KeyFunctions.PlaceStockpile, -29),
		new HotKeyEntry(Enums.KeyFunctions.PlaceWoodcutter, -30),
		new HotKeyEntry(Enums.KeyFunctions.PlaceQuarry, -31),
		new HotKeyEntry(Enums.KeyFunctions.PlaceOxen, -32),
		new HotKeyEntry(Enums.KeyFunctions.PlaceIronMine, -33),
		new HotKeyEntry(Enums.KeyFunctions.PlacePitchRig, -34),
		new HotKeyEntry(Enums.KeyFunctions.PlaceMarket, -35),
		new HotKeyEntry(Enums.KeyFunctions.PlaceHunter, -36),
		new HotKeyEntry(Enums.KeyFunctions.PlaceDairyFarm, -37),
		new HotKeyEntry(Enums.KeyFunctions.PlaceAppleFarm, -38),
		new HotKeyEntry(Enums.KeyFunctions.PlaceWheatFarm, -39),
		new HotKeyEntry(Enums.KeyFunctions.PlaceHopsFarm, -40),
		new HotKeyEntry(Enums.KeyFunctions.PlaceHouse, -41),
		new HotKeyEntry(Enums.KeyFunctions.PlaceChurchMosque1, -42),
		new HotKeyEntry(Enums.KeyFunctions.PlaceChurchMosque2, -43),
		new HotKeyEntry(Enums.KeyFunctions.PlaceChurchMosque3, -44),
		new HotKeyEntry(Enums.KeyFunctions.PlaceApothecary, -45),
		new HotKeyEntry(Enums.KeyFunctions.PlaceWell, -46),
		new HotKeyEntry(Enums.KeyFunctions.PlaceWaterpot, -47),
		new HotKeyEntry(Enums.KeyFunctions.PlaceFletcher, -48),
		new HotKeyEntry(Enums.KeyFunctions.PlacePoleturner, -49),
		new HotKeyEntry(Enums.KeyFunctions.PlaceBlacksmith, -50),
		new HotKeyEntry(Enums.KeyFunctions.PlaceTanner, -51),
		new HotKeyEntry(Enums.KeyFunctions.PlaceArmourer, -52),
		new HotKeyEntry(Enums.KeyFunctions.PlaceGranary, -53),
		new HotKeyEntry(Enums.KeyFunctions.PlaceBaker, -54),
		new HotKeyEntry(Enums.KeyFunctions.PlaceMill, -55),
		new HotKeyEntry(Enums.KeyFunctions.PlaceBrewer, -56),
		new HotKeyEntry(Enums.KeyFunctions.PlaceInn, -57),
		new HotKeyEntry(Enums.KeyFunctions.PlaceMaypole, -58),
		new HotKeyEntry(Enums.KeyFunctions.PlaceDancingBear, -59),
		new HotKeyEntry(Enums.KeyFunctions.PlaceGardens1, -60),
		new HotKeyEntry(Enums.KeyFunctions.PlaceGardens2, -61),
		new HotKeyEntry(Enums.KeyFunctions.PlaceGardens3, -62),
		new HotKeyEntry(Enums.KeyFunctions.PlaceStatue, -63),
		new HotKeyEntry(Enums.KeyFunctions.PlaceShrine, -64),
		new HotKeyEntry(Enums.KeyFunctions.PlaceFlag1, -65),
		new HotKeyEntry(Enums.KeyFunctions.PlaceFlag2, -66),
		new HotKeyEntry(Enums.KeyFunctions.PlaceFlag3, -67),
		new HotKeyEntry(Enums.KeyFunctions.PlaceFlag4, -68),
		new HotKeyEntry(Enums.KeyFunctions.PlaceGallows, -69),
		new HotKeyEntry(Enums.KeyFunctions.PlaceCesspit, -70),
		new HotKeyEntry(Enums.KeyFunctions.PlaceStocks, -71),
		new HotKeyEntry(Enums.KeyFunctions.PlaceHeads, -72),
		new HotKeyEntry(Enums.KeyFunctions.PlaceBurningStake, -73),
		new HotKeyEntry(Enums.KeyFunctions.PlaceDungeon, -74),
		new HotKeyEntry(Enums.KeyFunctions.PlaceRack, -75),
		new HotKeyEntry(Enums.KeyFunctions.PlaceGibbett, -76),
		new HotKeyEntry(Enums.KeyFunctions.PlaceChoppingBlock, -77),
		new HotKeyEntry(Enums.KeyFunctions.PlaceDunkingStool, -78)
	};

	private Dictionary<Enums.KeyFunctions, int> hotKeyTextDict;

	public HUD_Options()
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
		else if (instance3 == null)
		{
			instance3 = this;
		}
		RefVideoSettings = (Noesis.Grid)FindName("VideoSettings");
		RefSoundSettings = (Noesis.Grid)FindName("SoundSettings");
		RefKeySettings = (Noesis.Grid)FindName("KeySettings");
		RefControlSettings = (Noesis.Grid)FindName("ControlSettings");
		RefNameSettings = (Noesis.Grid)FindName("NameSettings");
		RefCoASettings = (Noesis.Grid)FindName("CoASettings");
		RefCheatSettings = (Noesis.Grid)FindName("CheatSettings");
		RefResolutionCombo = (ComboBox)FindName("ResolutionCombo");
		RefScreenModeCombo = (ComboBox)FindName("ScreenModeCombo");
		RefOptionsHotKeyPanel = (Noesis.Grid)FindName("OptionsHotKeyPanel");
		RefUIScaleGrid = (Noesis.Grid)FindName("UIScaleGrid");
		RefCOA_Button = (Button)FindName("COA_Button");
		RefMasterVolumeSlider = (Slider)FindName("MasterVolumeSlider");
		RefMasterVolumeSlider.ValueChanged += MasterVolumeSlider_ValueChanged;
		RefMusicVolumeSlider = (Slider)FindName("MusicVolumeSlider");
		RefMusicVolumeSlider.ValueChanged += MusicVolumeSlider_ValueChanged;
		RefSpeechVolumeSlider = (Slider)FindName("SpeechVolumeSlider");
		RefSpeechVolumeSlider.ValueChanged += SpeechVolumeSlider_ValueChanged;
		RefUnitSpeechVolumeSlider = (Slider)FindName("UnitSpeechVolumeSlider");
		RefUnitSpeechVolumeSlider.ValueChanged += UnitSpeechVolumeSlider_ValueChanged;
		RefSFXVolumeSlider = (Slider)FindName("SFXVolumeSlider");
		RefSFXVolumeSlider.ValueChanged += SFXVolumeSlider_ValueChanged;
		RefScrollSpeedSlider = (Slider)FindName("ScrollSpeedSlider");
		RefScrollSpeedSlider.ValueChanged += ScrollSpeedSlider_ValueChanged;
		RefGameSpeedSlider = (Slider)FindName("GameSpeedSlider");
		RefGameSpeedSlider.ValueChanged += GameSpeedSlider_ValueChanged;
		RefUIScaleSlider = (Slider)FindName("UIScaleSlider");
		RefUIScaleSlider.ValueChanged += UIScaleSlider_ValueChanged;
		RefTextBoxChangeName = (TextBox)FindName("TextBoxChangeName");
		RefTextBoxChangeName.IsKeyboardFocusedChanged += TextInputFocus;
		RefTextBoxNewsletter = (TextBox)FindName("TextBoxNewsletter");
		RefTextBoxNewsletter.IsKeyboardFocusedChanged += TextInputFocus;
		RefTextBoxNewsletter.TextChanged += NewsletterValueChanged;
		RefVSyncCheck = (CheckBox)FindName("VSyncCheck");
		RefVSyncCheck.Checked += VSyncCheck_ValueChanged;
		RefVSyncCheck.Unchecked += VSyncCheck_ValueChanged;
		RefLockCursorCheck = (CheckBox)FindName("LockCursorCheck");
		RefLockCursorCheck.Checked += LockCursor_ValueChanged;
		RefLockCursorCheck.Unchecked += LockCursor_ValueChanged;
		RefBuildingTooltipsCheck = (CheckBox)FindName("BuildingTooltipsCheck");
		RefBuildingTooltipsCheck.Checked += BuildingTooltipsCheck_ValueChanged;
		RefBuildingTooltipsCheck.Unchecked += BuildingTooltipsCheck_ValueChanged;
		RefBuildingTooltipsCheckText = (TextBlock)FindName("BuildingTooltipsCheckText");
		RefSteamHelpCheck = (CheckBox)FindName("SteamHelpCheck");
		RefSteamHelpCheck.Checked += SteamHelp_ValueChanged;
		RefSteamHelpCheck.Unchecked += SteamHelp_ValueChanged;
		RefSteamHelpCheckText = (TextBlock)FindName("SteamHelpCheckText");
		RefCompassCheck = (CheckBox)FindName("CompassCheck");
		RefCompassCheck.Checked += CompassCheck_ValueChanged;
		RefCompassCheck.Unchecked += CompassCheck_ValueChanged;
		RefLocalTimeCheck = (CheckBox)FindName("LocalTimeCheck");
		RefLocalTimeCheck.Checked += LocalTimeCheck_ValueChanged;
		RefLocalTimeCheck.Unchecked += LocalTimeCheck_ValueChanged;
		RefGameTimeCheck = (CheckBox)FindName("GameTimeCheck");
		RefGameTimeCheck.Checked += GameTimeCheck_ValueChanged;
		RefGameTimeCheck.Unchecked += GameTimeCheck_ValueChanged;
		RefSandsTimerCheck = (CheckBox)FindName("SandsTimerCheck");
		RefSandsTimerCheck.Checked += SandsTimerCheck_ValueChanged;
		RefSandsTimerCheck.Unchecked += SandsTimerCheck_ValueChanged;
		RefRadarZoomCheck = (CheckBox)FindName("RadarZoomCheck");
		RefRadarZoomCheck.Checked += RadarZoomCheck_ValueChanged;
		RefRadarZoomCheck.Unchecked += RadarZoomCheck_ValueChanged;
		RefRadarZoomCheckText = (TextBlock)FindName("RadarZoomCheckText");
		RefCustomIntros = (CheckBox)FindName("CustomIntros");
		RefCustomIntros.Checked += CustomIntros_ValueChanged;
		RefCustomIntros.Unchecked += CustomIntros_ValueChanged;
		RefCustomIntrosName = (TextBlock)FindName("CustomIntrosName");
		RefSteamIdentity = (TextBlock)FindName("SteamIdentity");
		RefUISoundsCheck = (CheckBox)FindName("UISoundsCheck");
		RefUISoundsCheck.Checked += UISounds_ValueChanged;
		RefUISoundsCheck.Unchecked += UISounds_ValueChanged;
		RefGenieSpeechCheck = (CheckBox)FindName("GenieSpeechCheck");
		RefGenieSpeechCheck.Checked += GenieSpeech_ValueChanged;
		RefGenieSpeechCheck.Unchecked += GenieSpeech_ValueChanged;
		RefEnglishSpeechCheck = (CheckBox)FindName("EnglishSpeechCheck");
		RefEnglishSpeechCheck.Checked += EnglishSpeech_ValueChanged;
		RefEnglishSpeechCheck.Unchecked += EnglishSpeech_ValueChanged;
		RefMuteInsultSpeechCheck = (CheckBox)FindName("MuteInsultSpeechCheck");
		RefMuteInsultSpeechCheck.Checked += MuteInsult_ValueChanged;
		RefMuteInsultSpeechCheck.Unchecked += MuteInsult_ValueChanged;
		RefMuteInsultSpeechCheckText = (TextBlock)FindName("MuteInsultSpeechCheckText");
		RefMuteInsultsCheck = (CheckBox)FindName("MuteInsultsCheck");
		RefMuteInsultsCheck.Checked += MuteInsult_ValueChanged;
		RefMuteInsultsCheck.Unchecked += MuteInsult_ValueChanged;
		RefMuteInsultsCheckText = (TextBlock)FindName("MuteInsultsCheckText");
		RefMuteBackgroundCheck = (CheckBox)FindName("MuteBackground");
		RefMuteBackgroundCheck.Checked += MuteBackground_ValueChanged;
		RefMuteBackgroundCheck.Unchecked += MuteBackground_ValueChanged;
		RefMuteBackgroundCheckText = (TextBlock)FindName("MuteBackgroundText");
		RefReduceSoundsCheck = (CheckBox)FindName("ReduceSoundsCheck");
		RefReduceSoundsCheck.Checked += ReduceSounds_ValueChanged;
		RefReduceSoundsCheck.Unchecked += ReduceSounds_ValueChanged;
		RefReduceSoundsCheckText = (TextBlock)FindName("ReduceSoundsCheckText");
		RefSFXDefaultsButton = (Button)FindName("SFXDefaultsButton");
		RefCheatKeysCheck = (CheckBox)FindName("CheatKeysCheck");
		RefCheatKeysCheck.Checked += CheatKeys_ValueChanged;
		RefCheatKeysCheck.Unchecked += CheatKeys_ValueChanged;
		RefPingsCheck = (CheckBox)FindName("PingsCheck");
		RefPingsCheck.Checked += Pings_ValueChanged;
		RefPingsCheck.Unchecked += Pings_ValueChanged;
		RefPingsCheckText = (TextBlock)FindName("PingsCheckText");
		RefExtraZoomCheck = (CheckBox)FindName("ExtraZoomCheck");
		RefExtraZoomCheck.Checked += ExtraZoom_ValueChanged;
		RefExtraZoomCheck.Unchecked += ExtraZoom_ValueChanged;
		RefExtraZoomCheckText = (TextBlock)FindName("ExtraZoomCheckText");
		RefShowMoatCheck = (CheckBox)FindName("ShowMoatCheck");
		RefShowMoatCheck.Checked += ShowMoat_ValueChanged;
		RefShowMoatCheck.Unchecked += ShowMoat_ValueChanged;
		RefShowMoatText = (TextBlock)FindName("ShowMoatText");
		RefConfirmDisbandCheck = (CheckBox)FindName("ConfirmDisbandCheck");
		RefConfirmDisbandCheck.Checked += ConfirmDisband_ValueChanged;
		RefConfirmDisbandCheck.Unchecked += ConfirmDisband_ValueChanged;
		RefConfirmDisbandText = (TextBlock)FindName("ConfirmDisbandText");
		RefTroopMoveCheck = (CheckBox)FindName("TroopMoveCheck");
		RefTroopMoveCheck.Checked += TroopMove_ValueChanged;
		RefTroopMoveCheck.Unchecked += TroopMove_ValueChanged;
		RefPlayerColourShield1 = (RadioButton)FindName("PlayerColourShield1");
		RefPlayerColourShield2 = (RadioButton)FindName("PlayerColourShield2");
		RefPlayerColourShield3 = (RadioButton)FindName("PlayerColourShield3");
		RefPlayerColourShield4 = (RadioButton)FindName("PlayerColourShield4");
		RefPlayerColourShield5 = (RadioButton)FindName("PlayerColourShield5");
		RefPlayerColourShield6 = (RadioButton)FindName("PlayerColourShield6");
		RefPlayerColourShield7 = (RadioButton)FindName("PlayerColourShield7");
		RefPlayerColourShield8 = (RadioButton)FindName("PlayerColourShield8");
		RefLeaderboard_OptOut = (CheckBox)FindName("Leaderboard_OptOut");
		RefLeaderboard_OptOut.Checked += Leaderboard_OptOut_ValueChanged;
		RefLeaderboard_OptOut.Unchecked += Leaderboard_OptOut_ValueChanged;
		RefLeaderboard_OptOutText = (TextBlock)FindName("Leaderboard_OptOutText");
		RefLeaderboard_Names = (CheckBox)FindName("Leaderboard_Names");
		RefLeaderboard_Names.Checked += Leaderboard_Names_ValueChanged;
		RefLeaderboard_Names.Unchecked += Leaderboard_Names_ValueChanged;
		RefLeaderboard_NamesText = (TextBlock)FindName("Leaderboard_NamesText");
		RefLeaderboard_Images = (CheckBox)FindName("Leaderboard_Images");
		RefLeaderboard_Images.Checked += Leaderboard_Images_ValueChanged;
		RefLeaderboard_Images.Unchecked += Leaderboard_Images_ValueChanged;
		RefLeaderboard_ImagesText = (TextBlock)FindName("Leaderboard_ImagesText");
		RefSandsTimeDisable = (CheckBox)FindName("SandsTimeDisable");
		RefSandsTimeDisable.Checked += SandsTimeDisable_ValueChanged;
		RefSandsTimeDisable.Unchecked += SandsTimeDisable_ValueChanged;
		RefChatMuteDisable = (CheckBox)FindName("ChatMuteDisable");
		RefChatMuteDisable.Checked += MuteMPChat_ValueChanged;
		RefChatMuteDisable.Unchecked += MuteMPChat_ValueChanged;
		RefArabicL2RCheck = (CheckBox)FindName("ArabicL2RCheck");
		RefArabicL2RCheck.Checked += ArabicL2R_ValueChanged;
		RefArabicL2RCheck.Unchecked += ArabicL2R_ValueChanged;
		RefPlayerSettingsHeading = (TextBlock)FindName("PlayerSettingsHeading");
		RefOptionsChickenButton = (Button)FindName("OptionsChickenButton");
		RefNewsletterSignupButton = (Button)FindName("NewsletterSignupButton");
		RefNewsletterCheck = (CheckBox)FindName("NewsletterCheck");
		RefNewsletterCheck.Checked += NewsletterCheck_ValueChanged;
		RefNewsletterCheck.Unchecked += NewsletterCheck_ValueChanged;
		RefScribeLock = (Image)FindName("ScribeLock");
		Resolution[] resolutions = Screen.resolutions;
		for (int i = 0; i < resolutions.Length; i++)
		{
			Resolution resolution = resolutions[i];
			if (resolution.width >= 1280 && resolution.height >= 768)
			{
				ComboBoxItem comboBoxItem = new ComboBoxItem();
				if (!FatControler.arabic)
				{
					comboBoxItem.Content = resolution.width + "x" + resolution.height + " (" + resolution.refreshRate + "hz)";
				}
				else
				{
					comboBoxItem.Content = resolution.width + "x" + resolution.height + " " + resolution.refreshRate + "hz";
				}
				comboBoxItem.Tag = resolution;
				comboBoxItem.Height = 25f;
				comboBoxItem.Padding = new Thickness(12f, 0f, 12f, 0f);
				comboBoxItem.VerticalAlignment = VerticalAlignment.Center;
				RefResolutionCombo.Items.Add(comboBoxItem);
			}
		}
		UpdateResListbox();
		RefResolutionCombo.SelectionChanged += RefResolutionCombo_SelectionChanged;
		ComboBoxItem item = new ComboBoxItem
		{
			Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 88),
			Tag = 0,
			Height = 25f,
			Padding = new Thickness(12f, 0f, 12f, 0f),
			VerticalAlignment = VerticalAlignment.Center
		};
		RefScreenModeCombo.Items.Add(item);
		ComboBoxItem item2 = new ComboBoxItem
		{
			Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 100),
			Tag = 1,
			Height = 25f,
			Padding = new Thickness(12f, 0f, 12f, 0f),
			VerticalAlignment = VerticalAlignment.Center
		};
		RefScreenModeCombo.Items.Add(item2);
		ComboBoxItem item3 = new ComboBoxItem
		{
			Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 101),
			Tag = 2,
			Height = 25f,
			Padding = new Thickness(12f, 0f, 12f, 0f),
			VerticalAlignment = VerticalAlignment.Center
		};
		RefScreenModeCombo.Items.Add(item3);
		if (Screen.fullScreenMode == FullScreenMode.FullScreenWindow)
		{
			RefScreenModeCombo.SelectedIndex = 1;
		}
		else if (Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen)
		{
			RefScreenModeCombo.SelectedIndex = 0;
		}
		else
		{
			RefScreenModeCombo.SelectedIndex = 2;
		}
		RefScreenModeCombo.SelectionChanged += RefScreenModeCombo_SelectionChanged;
		if (FatControler.polish || FatControler.ukrainian || FatControler.french || FatControler.spanish || FatControler.russian || FatControler.thai)
		{
			RefBuildingTooltipsCheck.Height = 43f;
			RefBuildingTooltipsCheckText.LineHeight = 20f;
			RefBuildingTooltipsCheckText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		}
		if (FatControler.japanese || FatControler.ukrainian)
		{
			RefRadarZoomCheck.Height = 43f;
			RefRadarZoomCheckText.LineHeight = 20f;
			RefRadarZoomCheckText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		}
		if (FatControler.arabic || FatControler.ukrainian)
		{
			RefSteamHelpCheck.Height = 43f;
			RefSteamHelpCheckText.LineHeight = 20f;
			RefSteamHelpCheckText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		}
		if (FatControler.greek)
		{
			RefCustomIntros.Height = 43f;
			RefCustomIntros.Margin = new Thickness(0f, 100f, 0f, 0f);
		}
		if (FatControler.spanish)
		{
			RefCustomIntrosName.FontSize = 18f;
		}
		if (FatControler.japanese)
		{
			RefSteamIdentity.FontSize = 11f;
		}
		if (FatControler.thai)
		{
			RefPlayerSettingsHeading.Margin = new Thickness(51f, 10f, 50f, 0f);
		}
		if (FatControler.polish)
		{
			RefReduceSoundsCheck.Height = 43f;
			RefReduceSoundsCheckText.LineHeight = 20f;
			RefReduceSoundsCheckText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		}
		if (FatControler.german || FatControler.japanese || FatControler.turkish)
		{
			RefMuteInsultsCheck.Height = 43f;
			RefMuteInsultsCheckText.LineHeight = 20f;
			RefMuteInsultsCheckText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		}
		if (FatControler.german)
		{
			RefMuteInsultSpeechCheck.Height = 43f;
			RefMuteInsultSpeechCheckText.LineHeight = 20f;
			RefMuteInsultSpeechCheckText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		}
		if (FatControler.polish)
		{
			RefLeaderboard_OptOut.Height = 43f;
			RefLeaderboard_OptOutText.LineHeight = 20f;
			RefLeaderboard_OptOutText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
			RefLeaderboard_Names.Height = 43f;
			RefLeaderboard_NamesText.LineHeight = 20f;
			RefLeaderboard_NamesText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		}
		if (FatControler.arabic || FatControler.french || FatControler.spanish || FatControler.italian || FatControler.polish || FatControler.japanese || FatControler.dutch || FatControler.ukrainian)
		{
			RefLeaderboard_Images.Height = 43f;
			RefLeaderboard_ImagesText.LineHeight = 20f;
			RefLeaderboard_ImagesText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		}
		if (FatControler.russian || FatControler.arabic)
		{
			RefMuteBackgroundCheck.Height = 43f;
			RefMuteBackgroundCheckText.LineHeight = 20f;
			RefMuteBackgroundCheckText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		}
		if (FatControler.arabic)
		{
			RefArabicL2RCheck.Visibility = Visibility.Visible;
		}
		if (FatControler.german || FatControler.portuguese || FatControler.russian || FatControler.ukrainian || FatControler.czech || FatControler.french || FatControler.hungarian || FatControler.italian || FatControler.japanese || FatControler.polish || FatControler.spanish || FatControler.thai || FatControler.turkish || FatControler.greek || FatControler.arabic)
		{
			RefPingsCheck.Height = 43f;
			RefPingsCheckText.LineHeight = 20f;
			RefPingsCheckText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		}
		if (FatControler.polish)
		{
			RefExtraZoomCheck.Height = 43f;
			RefExtraZoomCheckText.LineHeight = 20f;
			RefExtraZoomCheckText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		}
		if (FatControler.UsesEnglishSpeechFolder())
		{
			RefEnglishSpeechCheck.Visibility = Visibility.Collapsed;
		}
		RefHotKeyList = (ListView)FindName("HotKeyList");
		RefHotKeyList.SelectionChanged += delegate
		{
			if (RefHotKeyList.SelectedItem != null)
			{
				MainViewModel.Instance.OptionsHotKeyTitle = ((HotKeyRow)RefHotKeyList.SelectedItem).Text1;
				selectedFunction = (Enums.KeyFunctions)((HotKeyRow)RefHotKeyList.SelectedItem).iDataValue;
				string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 93);
				KeyCode keyCode = KeyManager.instance.GetKeyCode(selectedFunction, 0);
				KeyCode keyCode2 = KeyManager.instance.GetKeyCode(selectedFunction, 1);
				if (keyCode == KeyCode.None)
				{
					MainViewModel.Instance.OptionsHotKey1 = text;
				}
				else
				{
					MainViewModel.Instance.OptionsHotKey1 = GetKeyCodeString(keyCode);
				}
				if (keyCode2 == KeyCode.None)
				{
					MainViewModel.Instance.OptionsHotKey2 = text;
				}
				else
				{
					MainViewModel.Instance.OptionsHotKey2 = GetKeyCodeString(keyCode2);
				}
				RefOptionsHotKeyPanel.Visibility = Visibility.Visible;
				MainViewModel.Instance.OptionsHotKeySelectVis = Visibility.Visible;
				MainViewModel.Instance.OptionsHotKeyChangeVis = Visibility.Hidden;
			}
		};
		RefOptionsHotKeyNewKeyApply = (Button)FindName("OptionsHotKeyNewKeyApply");
		RefCursorSystemButton = (Button)FindName("CursorSystemButton");
		RefCursorSwordButton = (Button)FindName("CursorSwordButton");
		RefCursorSwordXButton = (Button)FindName("CursorSwordXButton");
		RefCursorSwordX2Button = (Button)FindName("CursorSwordX2Button");
		RefScribeClassicButton = (Button)FindName("ScribeClassicButton");
		RefScribeModernButton = (Button)FindName("ScribeModernButton");
		RefCrusaderLordButton = (Button)FindName("CrusaderLordButton");
		RefArabicLordButton = (Button)FindName("ArabicLordButton");
		RefBedouinLordButton = (Button)FindName("BedouinLordButton");
		RefScribeLordButton = (Button)FindName("ScribeLordButton");
		RefFemaleLordButton = (Button)FindName("FemaleLordButton");
		RefBessyLordButton = (Button)FindName("BessyLordButton");
		RefArabicLordFemaleButton = (Button)FindName("ArabicLordFemaleButton");
		RefBedouinLordFemaleButton = (Button)FindName("BedouinLordFemaleButton");
		RefOptionsKeys1 = (Button)FindName("OptionsKeys1");
		RefOptionsKeys2 = (Button)FindName("OptionsKeys2");
		if (FatControler.hungarian)
		{
			RefOptionsKeys1.Width = 380f;
			RefOptionsKeys1.Margin = new Thickness(10f, 0f, 0f, 70f);
			RefOptionsKeys2.Width = 380f;
			RefOptionsKeys2.Margin = new Thickness(10f, 0f, 0f, 30f);
		}
		CreateHotkeyList();
	}

	public static void OpenOptions(bool fromIngameMenu, bool fromMP = false)
	{
		if (!fromMP)
		{
			MainViewModel.Instance.Show_HUD_Options = true;
		}
		else
		{
			MainViewModel.Instance.Show_HUD_OptionsMP = true;
		}
		if (instance1.IsVisible)
		{
			MainViewModel.Instance.HUDOptions = instance1;
		}
		else if (instance2.IsVisible)
		{
			MainViewModel.Instance.HUDOptions = instance2;
		}
		else if (instance3.IsVisible)
		{
			MainViewModel.Instance.HUDOptions = instance3;
		}
		if (fromIngameMenu)
		{
			from = 0;
		}
		else
		{
			from = 1;
		}
		MainViewModel.Instance.HUDOptions.Init();
	}

	private void Init()
	{
		if (Director.instance.SimRunning)
		{
			if (MainViewModel.Instance.ShowingScenario)
			{
				MainViewModel.Instance.HUDScenario.StartExitAnim();
			}
			MainViewModel.Instance.MPChatVisible = false;
		}
		HUD_CoatOfArms.Init(ConfigSettings.getAvatar());
		panelActive = true;
		menuSection = 0;
		UpdateMenus();
		UpdateControls();
		UpdateCursors();
		UpdateLords();
		RefMasterVolumeSlider.Value = (int)(ConfigSettings.Settings_MasterVolume * 100f);
		RefMusicVolumeSlider.Value = (int)(ConfigSettings.Settings_MusicVolume * 100f);
		RefSpeechVolumeSlider.Value = (int)(ConfigSettings.Settings_SpeechVolume * 100f);
		RefUnitSpeechVolumeSlider.Value = (int)(ConfigSettings.Settings_UnitSpeechVolume * 100f);
		RefSFXVolumeSlider.Value = (int)(ConfigSettings.Settings_SFXVolume * 100f);
		MainViewModel.Instance.MasterVolumeValue = RefMasterVolumeSlider.Value.ToString();
		MainViewModel.Instance.MusicVolumeValue = RefMusicVolumeSlider.Value.ToString();
		MainViewModel.Instance.SpeechVolumeValue = RefSpeechVolumeSlider.Value.ToString();
		MainViewModel.Instance.UnitSpeechVolumeValue = RefUnitSpeechVolumeSlider.Value.ToString();
		MainViewModel.Instance.SfxVolumeValue = RefSFXVolumeSlider.Value.ToString();
		RefScrollSpeedSlider.Value = ConfigSettings.Settings_ScrollSpeed;
		RefGameSpeedSlider.Value = ConfigSettings.Settings_GameSpeed / 5;
		RefTextBoxChangeName.Text = ConfigSettings.Settings_UserName;
		RefUIScaleSlider.Value = (int)(ConfigSettings.Settings_UIScale * 100f);
		if (MainViewModel.Instance.Show_HUD_OptionsMP)
		{
			RefOptionsChickenButton.Visibility = Visibility.Hidden;
			MainViewModel.Instance.OptionsMPLordVis = Visibility.Collapsed;
		}
		else
		{
			RefOptionsChickenButton.Visibility = Visibility.Visible;
			if (Director.instance.SimRunning)
			{
				MainViewModel.Instance.OptionsMPLordVis = Visibility.Collapsed;
			}
			else
			{
				MainViewModel.Instance.OptionsMPLordVis = Visibility.Visible;
			}
		}
		if (ConfigSettings.Settings_Vsync)
		{
			RefVSyncCheck.IsChecked = true;
		}
		else
		{
			RefVSyncCheck.IsChecked = false;
		}
		if (Screen.fullScreenMode == FullScreenMode.FullScreenWindow)
		{
			RefScreenModeCombo.SelectedIndex = 1;
		}
		else if (Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen)
		{
			RefScreenModeCombo.SelectedIndex = 0;
		}
		else
		{
			RefScreenModeCombo.SelectedIndex = 2;
		}
		RefLockCursorCheck.IsChecked = ConfigSettings.Settings_LockCursor;
		RefBuildingTooltipsCheck.IsChecked = ConfigSettings.Settings_ShowBuildingTooltips;
		RefSteamHelpCheck.IsChecked = ConfigSettings.Settings_UseSteamOverlayForHelp;
		RefCompassCheck.IsChecked = ConfigSettings.Settings_Compass;
		RefLocalTimeCheck.IsChecked = ConfigSettings.Settings_ShowLocalTime;
		RefGameTimeCheck.IsChecked = ConfigSettings.Settings_ShowGameTime;
		RefSandsTimerCheck.IsChecked = ConfigSettings.Settings_ShowSandsTimer;
		RefRadarZoomCheck.IsChecked = ConfigSettings.Settings_RadarDefaultZoomedOut;
		RefCustomIntros.IsChecked = ConfigSettings.Settings_CustomIntros;
		RefUISoundsCheck.IsChecked = ConfigSettings.Settings_PlayUISFX;
		RefGenieSpeechCheck.IsChecked = ConfigSettings.Settings_GenieSpeech;
		RefReduceSoundsCheck.IsChecked = ConfigSettings.Settings_ReduceMusicVolumeForSpeech;
		RefCheatKeysCheck.IsChecked = ConfigSettings.Settings_CheatKeysEnabled;
		RefPingsCheck.IsChecked = ConfigSettings.Settings_ShowPings;
		RefExtraZoomCheck.IsChecked = ConfigSettings.Settings_ExtraZoom;
		RefShowMoatCheck.IsChecked = ConfigSettings.Settings_ShowPlannedMoat;
		RefConfirmDisbandCheck.IsChecked = ConfigSettings.Settings_Confirm_Disband_Troops;
		RefTroopMoveCheck.IsChecked = ConfigSettings.Settings_TroopMoveMode;
		RefEnglishSpeechCheck.IsChecked = ConfigSettings.Settings_EnglishSpeech;
		RefMuteBackgroundCheck.IsChecked = !ConfigSettings.Settings_BackgroundAudio;
		RefLeaderboard_OptOut.IsChecked = ConfigSettings.Settings_Leaderboard_OptOut;
		RefLeaderboard_Names.IsChecked = ConfigSettings.Settings_Leaderboard_Names;
		RefLeaderboard_Images.IsChecked = ConfigSettings.Settings_Leaderboard_Images;
		RefSandsTimeDisable.IsChecked = ConfigSettings.Settings_HideSoTTiming;
		RefChatMuteDisable.IsChecked = ConfigSettings.Settings_MuteMPChat;
		RefArabicL2RCheck.IsChecked = ConfigSettings.Settings_ArabicL2R;
		panelActive = false;
		RefMuteInsultsCheck.IsChecked = ConfigSettings.Settings_MuteInsults;
		RefMuteInsultSpeechCheck.IsChecked = ConfigSettings.Settings_MuteInsultSpeech;
		panelActive = true;
		if (Director.instance.MultiplayerGame || MainViewModel.Instance.Show_HUD_OptionsMP)
		{
			RefCOA_Button.IsEnabled = false;
		}
		else
		{
			RefCOA_Button.IsEnabled = from != 0;
		}
		MainViewModel.Instance.OptionsScaleApplyVisible = Visibility.Hidden;
		if (Director.instance.MultiplayerGame || MainViewModel.Instance.Show_HUD_OptionsMP)
		{
			MainViewModel.Instance.OptionsGameSpeedVis = Visibility.Collapsed;
		}
		else
		{
			MainViewModel.Instance.OptionsGameSpeedVis = Visibility.Visible;
		}
		if (ConfigSettings.AchievementsDisabled)
		{
			MainViewModel.Instance.OptionsAchievementsDisabledVis = Visibility.Visible;
		}
		else
		{
			MainViewModel.Instance.OptionsAchievementsDisabledVis = Visibility.Hidden;
		}
		if ((Director.instance.SimRunning || MainViewModel.Instance.Show_HUD_OptionsMP) && GameData.Instance.game_type != 3)
		{
			MainViewModel.Instance.OptionsInGameCheatsVis = Visibility.Visible;
		}
		else
		{
			MainViewModel.Instance.OptionsInGameCheatsVis = Visibility.Hidden;
		}
		if (GameData.Instance.lastGameState == null || GameData.Instance.lastGameState.free_buildingCheat == 0)
		{
			MainViewModel.Instance.OptionsFreeBuildingsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 39);
		}
		else
		{
			MainViewModel.Instance.OptionsFreeBuildingsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 40);
		}
		if (Director.instance.SimRunning || MainViewModel.Instance.Show_HUD_OptionsMP)
		{
			MainViewModel.Instance.OptionsPlayerColourVis = Visibility.Hidden;
		}
		else
		{
			MainViewModel.Instance.OptionsPlayerColourVis = Visibility.Visible;
		}
		switch (ConfigSettings.Settings_PlayerColour)
		{
		case 0:
			RefPlayerColourShield1.IsChecked = true;
			break;
		case 1:
			RefPlayerColourShield2.IsChecked = true;
			break;
		case 2:
			RefPlayerColourShield3.IsChecked = true;
			break;
		case 3:
			RefPlayerColourShield4.IsChecked = true;
			break;
		case 4:
			RefPlayerColourShield5.IsChecked = true;
			break;
		case 5:
			RefPlayerColourShield6.IsChecked = true;
			break;
		case 6:
			RefPlayerColourShield7.IsChecked = true;
			break;
		case 7:
			RefPlayerColourShield8.IsChecked = true;
			break;
		}
		UpdateUIScaleSliderVis();
		MainViewModel.Instance.OptionsApplyVisible = Visibility.Hidden;
		resChanged = false;
		screenModeChanged = false;
		MainViewModel.Instance.MP_SteamIdentity_Name = Platform_Multiplayer.Instance.GetLocalSteamName();
		CreateHotkeyList();
		MainViewModel.Instance.OptionsNewsletterVis = Visibility.Collapsed;
		if (FrontendMenus.newsletterSignUp)
		{
			RefScribeLock.Visibility = Visibility.Hidden;
		}
		else
		{
			RefScribeLock.Visibility = Visibility.Visible;
		}
		RefNewsletterSignupButton.IsEnabled = false;
		RefTextBoxNewsletter.Text = "";
	}

	public void RefreshGameSpeed()
	{
		RefGameSpeedSlider.Value = ConfigSettings.Settings_GameSpeed / 5;
	}

	public void Update()
	{
		UpdateUIScaleSliderVis();
		if (FrontendMenus.newsletterSignUp)
		{
			RefScribeLock.Visibility = Visibility.Hidden;
		}
		else
		{
			RefScribeLock.Visibility = Visibility.Visible;
		}
		if (KeyManager.instance.HotKeySelectorMode)
		{
			if (KeyManager.instance.HotKeyCurrentKey == 0)
			{
				MainViewModel.Instance.OptionsHotKeyNewKey = "";
				MainViewModel.Instance.OptionsHotKeyWarning = "";
			}
			else
			{
				MainViewModel.Instance.OptionsHotKeyNewKey = GetKeyCodeString((KeyCode)KeyManager.instance.HotKeyCurrentKey);
				RefOptionsHotKeyNewKeyApply.IsEnabled = true;
				Enums.KeyFunctions hotKeyFunction = KeyManager.instance.GetHotKeyFunction();
				if (hotKeyFunction == Enums.KeyFunctions.NumActions)
				{
					MainViewModel.Instance.OptionsHotKeyWarning = "";
				}
				else
				{
					string text = "";
					if (hotKeyTextDict.TryGetValue(hotKeyFunction, out var value))
					{
						text = ((value < 0) ? GetPlaceBuildingHotkeyText(value) : Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, value));
						MainViewModel.Instance.OptionsHotKeyWarning = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 97) + " " + text;
					}
				}
			}
		}
		if (lastDynamicChanged < DateTime.UtcNow)
		{
			Save();
		}
	}

	public void ButtonClicked(int param)
	{
		switch (param)
		{
		case -1:
			if (from != 0)
			{
				HUD_CoatOfArms.SaveIfChanged();
			}
			switch (from)
			{
			case 0:
			{
				MainViewModel instance2 = MainViewModel.Instance;
				bool show_HUD_Options = (MainViewModel.Instance.Show_HUD_OptionsMP = false);
				instance2.Show_HUD_Options = show_HUD_Options;
				if (!MainViewModel.Instance.HUDIngameMenu.wasPaused)
				{
					Director.instance.SetPausedState(state: false);
				}
				MainViewModel.Instance.HUDmain.InGameOptions(null, null);
				break;
			}
			case 1:
			{
				MainViewModel instance = MainViewModel.Instance;
				bool show_HUD_Options = (MainViewModel.Instance.Show_HUD_OptionsMP = false);
				instance.Show_HUD_Options = show_HUD_Options;
				break;
			}
			}
			break;
		case -2:
		{
			int targetFrameRate = 0;
			if (RefVSyncCheck.IsChecked.Value)
			{
				targetFrameRate = ((RefResolutionCombo.SelectedItem == null) ? Screen.currentResolution.refreshRate : ((Resolution)((ComboBoxItem)RefResolutionCombo.SelectedItem).Tag).refreshRate);
			}
			Application.targetFrameRate = targetFrameRate;
			if (screenModeChanged && !resChanged && RefScreenModeCombo.SelectedIndex == 2)
			{
				Screen.fullScreenMode = FullScreenMode.Windowed;
			}
			else
			{
				FullScreenMode fullScreenMode = FullScreenMode.Windowed;
				switch (RefScreenModeCombo.SelectedIndex)
				{
				case 0:
					fullScreenMode = FullScreenMode.ExclusiveFullScreen;
					ConfigSettings.Settings_LastFullscreenType = 0;
					break;
				case 1:
					fullScreenMode = FullScreenMode.FullScreenWindow;
					ConfigSettings.Settings_LastFullscreenType = 1;
					break;
				case 2:
					fullScreenMode = FullScreenMode.Windowed;
					break;
				}
				if (RefResolutionCombo.SelectedItem == null)
				{
					Screen.fullScreenMode = fullScreenMode;
				}
				else
				{
					Resolution resolution = (Resolution)((ComboBoxItem)RefResolutionCombo.SelectedItem).Tag;
					ConfigSettings.Settings_LastFullscreenWidth = resolution.width;
					ConfigSettings.Settings_LastFullscreenHeight = resolution.height;
					ConfigSettings.Settings_LastFullscreenRefresh = resolution.refreshRate;
					Screen.SetResolution(resolution.width, resolution.height, fullScreenMode, resolution.refreshRate);
				}
			}
			SetVSync(RefVSyncCheck.IsChecked.Value);
			UpdateResListbox(fromSettings: true);
			Save();
			MainViewModel.Instance.OptionsApplyVisible = Visibility.Hidden;
			screenModeChanged = false;
			resChanged = false;
			UpdateUIScaleSliderVis();
			break;
		}
		case -3:
		{
			float scaleFactor = (ConfigSettings.Settings_UIScale = (float)(int)RefUIScaleSlider.Value / 100f);
			if (MainViewModel.Instance.Show_InGame)
			{
				MainViewModel.Instance.ScaleIngameUI(scaleFactor);
			}
			else
			{
				MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
			}
			MainViewModel.Instance.OptionsScaleApplyVisible = Visibility.Hidden;
			Save();
			break;
		}
		case 1:
		case 2:
		case 3:
		case 4:
		case 5:
		case 7:
		case 8:
		{
			Visibility optionsApplyVisible = MainViewModel.Instance.OptionsApplyVisible;
			menuSection = param - 1;
			UpdateMenus();
			MainViewModel.Instance.OptionsApplyVisible = optionsApplyVisible;
			_ = 7;
			break;
		}
		case 6:
			try
			{
				string persistentDataPath = Application.persistentDataPath;
				Application.OpenURL("file://" + persistentDataPath);
				break;
			}
			catch (Exception)
			{
				break;
			}
		case 9:
			try
			{
				SteamFriends.ActivateGameOverlayToWebPage("https://store.steampowered.com/news/app/3024040");
				break;
			}
			catch (Exception)
			{
				break;
			}
		case -10:
			ConfigSettings.Settings_CursorStyle = 1;
			Director.instance.resetCursor();
			UpdateCursors();
			Save();
			break;
		case -11:
			ConfigSettings.Settings_CursorStyle = 0;
			Director.instance.resetCursor();
			UpdateCursors();
			Save();
			break;
		case -12:
			ConfigSettings.Settings_CursorStyle = 2;
			Director.instance.resetCursor();
			UpdateCursors();
			Save();
			break;
		case -13:
			ConfigSettings.Settings_CursorStyle = 3;
			Director.instance.resetCursor();
			UpdateCursors();
			Save();
			break;
		case -15:
			ConfigSettings.Settings_Scribe = 0;
			UpdateCursors();
			Save();
			break;
		case -16:
			ConfigSettings.Settings_Scribe = 1;
			UpdateCursors();
			Save();
			break;
		case -17:
			ConfigSettings.Settings_Scribe = 2;
			UpdateCursors();
			Save();
			break;
		case -1000:
			MainViewModel.Instance.Options_CurrentLord = GetLordName(ConfigSettings.Settings_LordType);
			break;
		case -1800:
			MainViewModel.Instance.Options_CurrentLord = GetLordName(0);
			break;
		case -18:
			ConfigSettings.Settings_LordType = 0;
			UpdateLords();
			Save();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case -1900:
			MainViewModel.Instance.Options_CurrentLord = GetLordName(1);
			break;
		case -19:
			ConfigSettings.Settings_LordType = 1;
			UpdateLords();
			Save();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case -1990:
			MainViewModel.Instance.Options_CurrentLord = GetLordName(2);
			break;
		case -199:
			ConfigSettings.Settings_LordType = 2;
			UpdateLords();
			Save();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case -1980:
			MainViewModel.Instance.Options_CurrentLord = GetLordName(3);
			break;
		case -198:
			if (FrontendMenus.newsletterSignUp)
			{
				ConfigSettings.Settings_LordType = 3;
				UpdateLords();
				Save();
				EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			}
			else
			{
				MainViewModel.Instance.OptionsNewsletterVis = Visibility.Visible;
			}
			break;
		case -1970:
			MainViewModel.Instance.Options_CurrentLord = GetLordName(4);
			break;
		case -197:
			ConfigSettings.Settings_LordType = 4;
			UpdateLords();
			Save();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case -1960:
			MainViewModel.Instance.Options_CurrentLord = GetLordName(5);
			break;
		case -196:
			ConfigSettings.Settings_LordType = 5;
			UpdateLords();
			Save();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case -1950:
			MainViewModel.Instance.Options_CurrentLord = GetLordName(6);
			break;
		case -195:
			ConfigSettings.Settings_LordType = 6;
			UpdateLords();
			Save();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case -1940:
			MainViewModel.Instance.Options_CurrentLord = GetLordName(7);
			break;
		case -194:
			ConfigSettings.Settings_LordType = 7;
			UpdateLords();
			Save();
			EngineInterface.GameAction(Enums.GameActionCommand.LordType, ConfigSettings.Settings_LordType, ConfigSettings.Settings_LordType);
			break;
		case -20:
			ConfigSettings.Settings_MasterVolume = 0.8f;
			ConfigSettings.Settings_MusicVolume = 1f;
			ConfigSettings.Settings_SpeechVolume = 1f;
			ConfigSettings.Settings_UnitSpeechVolume = 1f;
			ConfigSettings.Settings_SFXVolume = 1f;
			RefMasterVolumeSlider.Value = (int)(ConfigSettings.Settings_MasterVolume * 100f);
			RefMusicVolumeSlider.Value = (int)(ConfigSettings.Settings_MusicVolume * 100f);
			RefSpeechVolumeSlider.Value = (int)(ConfigSettings.Settings_SpeechVolume * 100f);
			RefUnitSpeechVolumeSlider.Value = (int)(ConfigSettings.Settings_UnitSpeechVolume * 100f);
			RefSFXVolumeSlider.Value = (int)(ConfigSettings.Settings_SFXVolume * 100f);
			Save();
			break;
		case -40:
			RefScrollSpeedSlider.Value = (ConfigSettings.Settings_ScrollSpeed = 5);
			Save();
			break;
		case -41:
			RefGameSpeedSlider.Value = 8f;
			break;
		case 41:
			KeyManager.instance.SetDefaultFunctionsSH1();
			CreateHotkeyList();
			ConfigSettings.SetDirty();
			Save();
			break;
		case 42:
			KeyManager.instance.SetDefaultFunctionsNew();
			CreateHotkeyList();
			ConfigSettings.SetDirty();
			Save();
			break;
		case 43:
			ConfigSettings.Settings_PushMapScrolling = true;
			UpdateControls();
			Save();
			break;
		case 44:
			ConfigSettings.Settings_PushMapScrolling = false;
			UpdateControls();
			Save();
			break;
		case 45:
			ConfigSettings.Settings_SH1RTSControls = true;
			UpdateControls();
			Save();
			break;
		case 46:
			ConfigSettings.Settings_SH1RTSControls = false;
			UpdateControls();
			Save();
			break;
		case 47:
			ConfigSettings.Settings_SH1MouseWheel = true;
			UpdateControls();
			Save();
			break;
		case 48:
			ConfigSettings.Settings_SH1MouseWheel = false;
			UpdateControls();
			Save();
			break;
		case 49:
			ConfigSettings.Settings_SH1CentreControls = true;
			UpdateControls();
			Save();
			break;
		case 50:
			ConfigSettings.Settings_SH1CentreControls = false;
			UpdateControls();
			Save();
			break;
		case 101:
			ConfigSettings.TempMissionUnlock = true;
			ConfigSettings.AchievementsDisabled = true;
			MainViewModel.Instance.OptionsAchievementsDisabledVis = Visibility.Visible;
			break;
		case 102:
			HUD_ConfirmationPopup.ShowConfirmationMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 33), delegate
			{
				ConfigSettings.Settings_Progress_Historical1Campaign = 5;
				ConfigSettings.Settings_Progress_Historical2Campaign = 5;
				ConfigSettings.Settings_Progress_Historical3Campaign = 5;
				ConfigSettings.Settings_Progress_Historical4Campaign = 5;
				ConfigSettings.Settings_Progress_Historical5Campaign = 5;
				ConfigSettings.Settings_Progress_Historical6Campaign = 5;
				ConfigSettings.Settings_Progress_Historical7Campaign = 5;
				ConfigSettings.Settings_Progress_Trail = 51;
				ConfigSettings.Settings_Progress_Trail2 = 31;
				ConfigSettings.Settings_Progress_Trail3 = 21;
				ConfigSettings.Settings_Progress_Trail_Sands1 = 6;
				ConfigSettings.Settings_Progress_Trail_Sands2 = 8;
				ConfigSettings.Settings_Progress_Trail_Sands3 = 10;
				ConfigSettings.Settings_Progress_Trail_Sands4 = 12;
				ConfigSettings.Settings_Progress_Trail_Sands5 = 10;
				ConfigSettings.Settings_Progress_Trail_Sands6 = 10;
				ConfigSettings.Settings_Progress_Trail_Sands7 = 10;
				ConfigSettings.Settings_Progress_Trail_Sands8 = 10;
				for (int i = 0; i < 50; i++)
				{
					if (ConfigSettings.Settings_Trail1Times[i] == -1)
					{
						ConfigSettings.Settings_Trail1Times[i] = -1200;
					}
				}
				for (int j = 0; j < 30; j++)
				{
					if (ConfigSettings.Settings_Trail2Times[j] == -1)
					{
						ConfigSettings.Settings_Trail2Times[j] = -1200;
					}
				}
				for (int k = 0; k < 20; k++)
				{
					if (ConfigSettings.Settings_Trail3Times[k] == -1)
					{
						ConfigSettings.Settings_Trail3Times[k] = -1200;
					}
				}
				for (int l = 0; l < 5; l++)
				{
					if (ConfigSettings.Settings_Trail_Sands1_Times[l] == -1)
					{
						ConfigSettings.Settings_Trail_Sands1_Times[l] = -1200;
					}
				}
				for (int m = 0; m < 7; m++)
				{
					if (ConfigSettings.Settings_Trail_Sands2_Times[m] == -1)
					{
						ConfigSettings.Settings_Trail_Sands2_Times[m] = -1200;
					}
				}
				for (int n = 0; n < 9; n++)
				{
					if (ConfigSettings.Settings_Trail_Sands3_Times[n] == -1)
					{
						ConfigSettings.Settings_Trail_Sands3_Times[n] = -1200;
					}
				}
				for (int num3 = 0; num3 < 11; num3++)
				{
					if (ConfigSettings.Settings_Trail_Sands4_Times[num3] == -1)
					{
						ConfigSettings.Settings_Trail_Sands4_Times[num3] = -1200;
					}
				}
				for (int num4 = 0; num4 < 9; num4++)
				{
					if (ConfigSettings.Settings_Trail_Sands5_Times[num4] == -1)
					{
						ConfigSettings.Settings_Trail_Sands5_Times[num4] = -1200;
					}
				}
				for (int num5 = 0; num5 < 9; num5++)
				{
					if (ConfigSettings.Settings_Trail_Sands6_Times[num5] == -1)
					{
						ConfigSettings.Settings_Trail_Sands6_Times[num5] = -1200;
					}
				}
				for (int num6 = 0; num6 < 9; num6++)
				{
					if (ConfigSettings.Settings_Trail_Sands7_Times[num6] == -1)
					{
						ConfigSettings.Settings_Trail_Sands7_Times[num6] = -1200;
					}
				}
				for (int num7 = 0; num7 < 9; num7++)
				{
					if (ConfigSettings.Settings_Trail_Sands8_Times[num7] == -1)
					{
						ConfigSettings.Settings_Trail_Sands8_Times[num7] = -1200;
					}
				}
				ConfigSettings.Settings_CoopCheatsEnabled = true;
				ConfigSettings.SaveSettings();
			}, delegate
			{
			}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 41));
			break;
		case 103:
			HUD_ConfirmationPopup.ShowConfirmationMessage(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 36), delegate
			{
				ConfigSettings.Settings_Progress_Historical1Campaign = 1;
				ConfigSettings.Settings_Progress_Historical2Campaign = 1;
				ConfigSettings.Settings_Progress_Historical3Campaign = 1;
				ConfigSettings.Settings_Progress_Historical4Campaign = 1;
				ConfigSettings.Settings_Progress_Historical5Campaign = 1;
				ConfigSettings.Settings_Progress_Historical6Campaign = 1;
				ConfigSettings.Settings_Progress_Historical7Campaign = 1;
				ConfigSettings.Settings_Progress_Trail = 1;
				ConfigSettings.Settings_Progress_Trail2 = 1;
				ConfigSettings.Settings_Progress_Trail3 = 1;
				ConfigSettings.Settings_Progress_Trail_Sands1 = 1;
				ConfigSettings.Settings_Progress_Trail_Sands2 = 1;
				ConfigSettings.Settings_Progress_Trail_Sands3 = 1;
				ConfigSettings.Settings_Progress_Trail_Sands4 = 1;
				ConfigSettings.Settings_Progress_Trail_Sands5 = 1;
				ConfigSettings.Settings_Progress_Trail_Sands6 = 1;
				ConfigSettings.Settings_Progress_Trail_Sands7 = 1;
				ConfigSettings.Settings_Progress_Trail_Sands8 = 1;
				FrontendMenus.CurrentSelectedHistorical1Mission = 11;
				FrontendMenus.CurrentSelectedHistorical2Mission = 21;
				FrontendMenus.CurrentSelectedHistorical3Mission = 31;
				FrontendMenus.CurrentSelectedHistorical4Mission = 41;
				FrontendMenus.CurrentSelectedHistorical5Mission = 51;
				FrontendMenus.CurrentSelectedHistorical6Mission = 61;
				FrontendMenus.CurrentSelectedHistorical7Mission = 71;
				for (int i = 0; i < 50; i++)
				{
					ConfigSettings.Settings_Trail1Times[i] = -1;
				}
				for (int j = 0; j < 30; j++)
				{
					ConfigSettings.Settings_Trail2Times[j] = -1;
				}
				for (int k = 0; k < 20; k++)
				{
					ConfigSettings.Settings_Trail3Times[k] = -1;
				}
				for (int l = 0; l < 5; l++)
				{
					ConfigSettings.Settings_Trail_Sands1_Times[l] = -1;
				}
				for (int m = 0; m < 7; m++)
				{
					ConfigSettings.Settings_Trail_Sands2_Times[m] = -1;
				}
				for (int n = 0; n < 9; n++)
				{
					ConfigSettings.Settings_Trail_Sands3_Times[n] = -1;
				}
				for (int num3 = 0; num3 < 11; num3++)
				{
					ConfigSettings.Settings_Trail_Sands4_Times[num3] = -1;
				}
				for (int num4 = 0; num4 < 9; num4++)
				{
					ConfigSettings.Settings_Trail_Sands5_Times[num4] = -1;
				}
				for (int num5 = 0; num5 < 9; num5++)
				{
					ConfigSettings.Settings_Trail_Sands6_Times[num5] = -1;
				}
				for (int num6 = 0; num6 < 9; num6++)
				{
					ConfigSettings.Settings_Trail_Sands7_Times[num6] = -1;
				}
				for (int num7 = 0; num7 < 9; num7++)
				{
					ConfigSettings.Settings_Trail_Sands8_Times[num7] = -1;
				}
				ConfigSettings.Settings_CoopCheatsEnabled = false;
				ConfigSettings.SaveSettings();
				ConfigSettings.TempMissionUnlock = false;
				ConfigSettings.AchievementsDisabled = false;
				ConfigSettings.WipeCampaignScores();
				MainViewModel.Instance.OptionsAchievementsDisabledVis = Visibility.Hidden;
			}, delegate
			{
			}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 42));
			break;
		case 104:
			EngineInterface.GameAction(Enums.GameActionCommand.SH1Cheats, 3, 0);
			ConfigSettings.AchievementsDisabled = true;
			MainViewModel.Instance.OptionsAchievementsDisabledVis = Visibility.Visible;
			break;
		case 105:
			EngineInterface.GameAction(Enums.GameActionCommand.SH1Cheats, 2, 0);
			ConfigSettings.AchievementsDisabled = true;
			MainViewModel.Instance.OptionsAchievementsDisabledVis = Visibility.Visible;
			break;
		case 106:
			if (GameData.Instance.lastGameState != null)
			{
				if (GameData.Instance.lastGameState.free_buildingCheat == 0)
				{
					GameData.Instance.lastGameState.free_buildingCheat = 1;
					MainViewModel.Instance.OptionsFreeBuildingsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 40);
					EngineInterface.GameAction(Enums.GameActionCommand.SH1Cheats, 1, 0);
					ConfigSettings.AchievementsDisabled = true;
					MainViewModel.Instance.OptionsAchievementsDisabledVis = Visibility.Visible;
				}
				else
				{
					GameData.Instance.lastGameState.free_buildingCheat = 0;
					MainViewModel.Instance.OptionsFreeBuildingsText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 39);
					EngineInterface.GameAction(Enums.GameActionCommand.SH1Cheats, 0, 0);
				}
			}
			break;
		case 70:
		case 71:
		case 72:
		case 73:
		case 74:
		case 75:
		case 76:
		case 77:
			ConfigSettings.Settings_PlayerColour = param - 70;
			Save();
			break;
		case -101:
			RefOptionsHotKeyPanel.Visibility = Visibility.Hidden;
			CreateHotkeyList();
			RefHotKeyList.SelectedItem = null;
			break;
		case -102:
			MainViewModel.Instance.OptionsHotKeyCurrentKey = MainViewModel.Instance.OptionsHotKey1;
			MainViewModel.Instance.OptionsHotKeyNewKey = "";
			RefOptionsHotKeyNewKeyApply.IsEnabled = false;
			KeyManager.instance.HotKeySelectorMode = true;
			selectedColumn = 0;
			MainViewModel.Instance.OptionsHotKeySelectVis = Visibility.Hidden;
			MainViewModel.Instance.OptionsHotKeyChangeVis = Visibility.Visible;
			break;
		case -103:
			MainViewModel.Instance.OptionsHotKeyCurrentKey = MainViewModel.Instance.OptionsHotKey2;
			MainViewModel.Instance.OptionsHotKeyNewKey = "";
			RefOptionsHotKeyNewKeyApply.IsEnabled = false;
			KeyManager.instance.HotKeySelectorMode = true;
			selectedColumn = 1;
			MainViewModel.Instance.OptionsHotKeySelectVis = Visibility.Hidden;
			MainViewModel.Instance.OptionsHotKeyChangeVis = Visibility.Visible;
			break;
		case -104:
			KeyManager.instance.HotKeySelectorMode = false;
			MainViewModel.Instance.OptionsHotKeySelectVis = Visibility.Visible;
			MainViewModel.Instance.OptionsHotKeyChangeVis = Visibility.Hidden;
			break;
		case -105:
		{
			if (KeyManager.instance.HotKeyCurrentKey > 0)
			{
				KeyManager.instance.SetNewKey(selectedFunction, KeyManager.instance.HotKeyCurrentKey, selectedColumn);
			}
			string text2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 93);
			KeyCode keyCode3 = KeyManager.instance.GetKeyCode(selectedFunction, 0);
			KeyCode keyCode4 = KeyManager.instance.GetKeyCode(selectedFunction, 1);
			if (keyCode3 == KeyCode.None)
			{
				MainViewModel.Instance.OptionsHotKey1 = text2;
			}
			else
			{
				MainViewModel.Instance.OptionsHotKey1 = GetKeyCodeString(keyCode3);
			}
			if (keyCode4 == KeyCode.None)
			{
				MainViewModel.Instance.OptionsHotKey2 = text2;
			}
			else
			{
				MainViewModel.Instance.OptionsHotKey2 = GetKeyCodeString(keyCode4);
			}
			ConfigSettings.SetDirty();
			Save();
			KeyManager.instance.HotKeySelectorMode = false;
			MainViewModel.Instance.OptionsHotKeySelectVis = Visibility.Visible;
			MainViewModel.Instance.OptionsHotKeyChangeVis = Visibility.Hidden;
			break;
		}
		case -106:
		{
			KeyManager.instance.SetNewKey(selectedFunction, -1, selectedColumn);
			string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 93);
			KeyCode keyCode = KeyManager.instance.GetKeyCode(selectedFunction, 0);
			KeyCode keyCode2 = KeyManager.instance.GetKeyCode(selectedFunction, 1);
			if (keyCode == KeyCode.None)
			{
				MainViewModel.Instance.OptionsHotKey1 = text;
			}
			else
			{
				MainViewModel.Instance.OptionsHotKey1 = GetKeyCodeString(keyCode);
			}
			if (keyCode2 == KeyCode.None)
			{
				MainViewModel.Instance.OptionsHotKey2 = text;
			}
			else
			{
				MainViewModel.Instance.OptionsHotKey2 = GetKeyCodeString(keyCode2);
			}
			ConfigSettings.SetDirty();
			Save();
			KeyManager.instance.HotKeySelectorMode = false;
			MainViewModel.Instance.OptionsHotKeySelectVis = Visibility.Visible;
			MainViewModel.Instance.OptionsHotKeyChangeVis = Visibility.Hidden;
			break;
		}
		case -20000:
			Director.instance.SignupNewsletter(RefTextBoxNewsletter.Text, delegate
			{
				FrontendMenus.newsletterSignUp = true;
				RefScribeLock.Visibility = Visibility.Hidden;
				ButtonClicked(-198);
			});
			MainViewModel.Instance.OptionsNewsletterVis = Visibility.Hidden;
			break;
		case -20001:
			MainViewModel.Instance.OptionsNewsletterVis = Visibility.Hidden;
			break;
		}
	}

	public void Save()
	{
		ConfigSettings.Settings_UserName = RefTextBoxChangeName.Text;
		ConfigSettings.SaveSettings();
		lastDynamicChanged = DateTime.MaxValue;
	}

	private void UpdateMenus()
	{
		for (int i = 0; i < 7; i++)
		{
			if (menuSection == i)
			{
				MainViewModel.Instance.OptionsSectionsBorders[i] = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.OptionsSectionsBorders[i] = Visibility.Hidden;
			}
		}
		RefVideoSettings.Visibility = Visibility.Hidden;
		RefSoundSettings.Visibility = Visibility.Hidden;
		RefKeySettings.Visibility = Visibility.Hidden;
		RefControlSettings.Visibility = Visibility.Hidden;
		RefCheatSettings.Visibility = Visibility.Hidden;
		RefNameSettings.Visibility = Visibility.Hidden;
		RefCoASettings.Visibility = Visibility.Hidden;
		switch (menuSection)
		{
		case 0:
			RefVideoSettings.Visibility = Visibility.Visible;
			UpdateResListbox();
			break;
		case 1:
			RefSoundSettings.Visibility = Visibility.Visible;
			break;
		case 2:
			RefKeySettings.Visibility = Visibility.Visible;
			break;
		case 3:
			RefControlSettings.Visibility = Visibility.Visible;
			break;
		case 4:
			RefNameSettings.Visibility = Visibility.Visible;
			break;
		case 6:
			HUD_CoatOfArms.InitBackground();
			RefCoASettings.Visibility = Visibility.Visible;
			break;
		case 7:
			RefCheatSettings.Visibility = Visibility.Visible;
			break;
		case 5:
			break;
		}
	}

	private void UpdateControls()
	{
		if (ConfigSettings.Settings_PushMapScrolling)
		{
			MainViewModel.Instance.OptionsPushEnabled = Visibility.Visible;
			MainViewModel.Instance.OptionsPushDisabled = Visibility.Hidden;
		}
		else
		{
			MainViewModel.Instance.OptionsPushEnabled = Visibility.Hidden;
			MainViewModel.Instance.OptionsPushDisabled = Visibility.Visible;
		}
		if (ConfigSettings.Settings_SH1RTSControls)
		{
			MainViewModel.Instance.OptionsSH1RTS = Visibility.Visible;
			MainViewModel.Instance.OptionsDERTS = Visibility.Hidden;
		}
		else
		{
			MainViewModel.Instance.OptionsSH1RTS = Visibility.Hidden;
			MainViewModel.Instance.OptionsDERTS = Visibility.Visible;
		}
		if (ConfigSettings.Settings_SH1MouseWheel)
		{
			MainViewModel.Instance.OptionsWheelSH1 = Visibility.Visible;
			MainViewModel.Instance.OptionsWheelZoom = Visibility.Hidden;
		}
		else
		{
			MainViewModel.Instance.OptionsWheelSH1 = Visibility.Hidden;
			MainViewModel.Instance.OptionsWheelZoom = Visibility.Visible;
		}
		if (ConfigSettings.Settings_SH1CentreControls)
		{
			MainViewModel.Instance.OptionsCenteringSH1 = Visibility.Visible;
			MainViewModel.Instance.OptionsCenteringModern = Visibility.Hidden;
		}
		else
		{
			MainViewModel.Instance.OptionsCenteringSH1 = Visibility.Hidden;
			MainViewModel.Instance.OptionsCenteringModern = Visibility.Visible;
		}
	}

	private void UpdateCursors()
	{
		if (ConfigSettings.Settings_CursorStyle == 0)
		{
			PropEx.SetSprite1(RefCursorSystemButton, MainViewModel.Instance.GameSprites[265]);
			PropEx.SetSprite2(RefCursorSystemButton, MainViewModel.Instance.GameSprites[263]);
			PropEx.SetSprite3(RefCursorSystemButton, MainViewModel.Instance.GameSprites[263]);
			PropEx.SetSprite4(RefCursorSystemButton, MainViewModel.Instance.GameSprites[265]);
			PropEx.SetSprite1(RefCursorSwordButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite2(RefCursorSwordButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite3(RefCursorSwordButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite4(RefCursorSwordButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite1(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite1(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[268]);
		}
		else if (ConfigSettings.Settings_CursorStyle == 1)
		{
			PropEx.SetSprite1(RefCursorSystemButton, MainViewModel.Instance.GameSprites[264]);
			PropEx.SetSprite2(RefCursorSystemButton, MainViewModel.Instance.GameSprites[264]);
			PropEx.SetSprite3(RefCursorSystemButton, MainViewModel.Instance.GameSprites[264]);
			PropEx.SetSprite4(RefCursorSystemButton, MainViewModel.Instance.GameSprites[264]);
			PropEx.SetSprite1(RefCursorSwordButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2(RefCursorSwordButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3(RefCursorSwordButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4(RefCursorSwordButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite1(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite1(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[268]);
		}
		else if (ConfigSettings.Settings_CursorStyle == 2)
		{
			PropEx.SetSprite1(RefCursorSystemButton, MainViewModel.Instance.GameSprites[265]);
			PropEx.SetSprite2(RefCursorSystemButton, MainViewModel.Instance.GameSprites[263]);
			PropEx.SetSprite3(RefCursorSystemButton, MainViewModel.Instance.GameSprites[263]);
			PropEx.SetSprite4(RefCursorSystemButton, MainViewModel.Instance.GameSprites[265]);
			PropEx.SetSprite1(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite2(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite3(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite4(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite1(RefCursorSwordButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2(RefCursorSwordButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3(RefCursorSwordButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4(RefCursorSwordButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite1(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[268]);
		}
		else if (ConfigSettings.Settings_CursorStyle == 3)
		{
			PropEx.SetSprite1(RefCursorSystemButton, MainViewModel.Instance.GameSprites[265]);
			PropEx.SetSprite2(RefCursorSystemButton, MainViewModel.Instance.GameSprites[263]);
			PropEx.SetSprite3(RefCursorSystemButton, MainViewModel.Instance.GameSprites[263]);
			PropEx.SetSprite4(RefCursorSystemButton, MainViewModel.Instance.GameSprites[265]);
			PropEx.SetSprite1(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite2(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite3(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite4(RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite1(RefCursorSwordButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2(RefCursorSwordButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3(RefCursorSwordButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4(RefCursorSwordButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite1(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4(RefCursorSwordXButton, MainViewModel.Instance.GameSprites[268]);
		}
		if (ConfigSettings.Settings_Scribe == 0)
		{
			PropEx.SetSprite1(RefScribeClassicButton, MainViewModel.Instance.GameSprites[340]);
			PropEx.SetSprite2(RefScribeClassicButton, MainViewModel.Instance.GameSprites[340]);
			PropEx.SetSprite3(RefScribeClassicButton, MainViewModel.Instance.GameSprites[340]);
			PropEx.SetSprite4(RefScribeClassicButton, MainViewModel.Instance.GameSprites[340]);
			PropEx.SetSprite1(RefScribeModernButton, MainViewModel.Instance.GameSprites[344]);
			PropEx.SetSprite2(RefScribeModernButton, MainViewModel.Instance.GameSprites[343]);
			PropEx.SetSprite3(RefScribeModernButton, MainViewModel.Instance.GameSprites[343]);
			PropEx.SetSprite4(RefScribeModernButton, MainViewModel.Instance.GameSprites[344]);
		}
		else if (ConfigSettings.Settings_Scribe == 2)
		{
			PropEx.SetSprite1(RefScribeClassicButton, MainViewModel.Instance.GameSprites[341]);
			PropEx.SetSprite2(RefScribeClassicButton, MainViewModel.Instance.GameSprites[340]);
			PropEx.SetSprite3(RefScribeClassicButton, MainViewModel.Instance.GameSprites[340]);
			PropEx.SetSprite4(RefScribeClassicButton, MainViewModel.Instance.GameSprites[341]);
			PropEx.SetSprite1(RefScribeModernButton, MainViewModel.Instance.GameSprites[343]);
			PropEx.SetSprite2(RefScribeModernButton, MainViewModel.Instance.GameSprites[343]);
			PropEx.SetSprite3(RefScribeModernButton, MainViewModel.Instance.GameSprites[343]);
			PropEx.SetSprite4(RefScribeModernButton, MainViewModel.Instance.GameSprites[343]);
		}
	}

	public static string GetLordName(int lordType)
	{
		switch (lordType)
		{
		case 6:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 333);
		case 7:
			return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 334);
		case 3:
			lordType = 4;
			break;
		case 4:
			lordType = 3;
			break;
		}
		return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 283 + lordType);
	}

	private void UpdateLords()
	{
		MainViewModel.Instance.Options_CurrentLord = GetLordName(ConfigSettings.Settings_LordType);
		if (ConfigSettings.Settings_LordType == 0)
		{
			PropEx.SetSprite1(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite2(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite3(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite4(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
		}
		else
		{
			PropEx.SetSprite1(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[396]);
			PropEx.SetSprite2(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite3(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite4(RefCrusaderLordButton, MainViewModel.Instance.GameSprites[396]);
		}
		if (ConfigSettings.Settings_LordType == 1)
		{
			PropEx.SetSprite1(RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite2(RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite3(RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite4(RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
		}
		else
		{
			PropEx.SetSprite1(RefArabicLordButton, MainViewModel.Instance.GameSprites[398]);
			PropEx.SetSprite2(RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite3(RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite4(RefArabicLordButton, MainViewModel.Instance.GameSprites[398]);
		}
		if (ConfigSettings.Settings_LordType == 2)
		{
			PropEx.SetSprite1(RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite2(RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite3(RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite4(RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
		}
		else
		{
			PropEx.SetSprite1(RefBedouinLordButton, MainViewModel.Instance.GameSprites[659]);
			PropEx.SetSprite2(RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite3(RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite4(RefBedouinLordButton, MainViewModel.Instance.GameSprites[659]);
		}
		if (ConfigSettings.Settings_LordType == 3)
		{
			PropEx.SetSprite1(RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite2(RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite3(RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite4(RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
		}
		else
		{
			PropEx.SetSprite1(RefScribeLordButton, MainViewModel.Instance.GameSprites[661]);
			PropEx.SetSprite2(RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite3(RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite4(RefScribeLordButton, MainViewModel.Instance.GameSprites[661]);
		}
		if (ConfigSettings.Settings_LordType == 4)
		{
			PropEx.SetSprite1(RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite2(RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite3(RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite4(RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
		}
		else
		{
			PropEx.SetSprite1(RefFemaleLordButton, MainViewModel.Instance.GameSprites[663]);
			PropEx.SetSprite2(RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite3(RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite4(RefFemaleLordButton, MainViewModel.Instance.GameSprites[663]);
		}
		if (ConfigSettings.Settings_LordType == 5)
		{
			PropEx.SetSprite1(RefBessyLordButton, MainViewModel.Instance.GameSprites[668]);
			PropEx.SetSprite2(RefBessyLordButton, MainViewModel.Instance.GameSprites[668]);
			PropEx.SetSprite3(RefBessyLordButton, MainViewModel.Instance.GameSprites[668]);
			PropEx.SetSprite4(RefBessyLordButton, MainViewModel.Instance.GameSprites[668]);
		}
		else
		{
			PropEx.SetSprite1(RefBessyLordButton, MainViewModel.Instance.GameSprites[667]);
			PropEx.SetSprite2(RefBessyLordButton, MainViewModel.Instance.GameSprites[668]);
			PropEx.SetSprite3(RefBessyLordButton, MainViewModel.Instance.GameSprites[668]);
			PropEx.SetSprite4(RefBessyLordButton, MainViewModel.Instance.GameSprites[667]);
		}
		if (AchievementsCommon.Instance.IsAchievementComplete(Enums.Achievements.Place_Dairy_Farms))
		{
			RefBessyLordButton.Visibility = Visibility.Visible;
		}
		else
		{
			RefBessyLordButton.Visibility = Visibility.Hidden;
		}
		if (ConfigSettings.Settings_LordType == 6)
		{
			PropEx.SetSprite1(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite2(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite3(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite4(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
		}
		else
		{
			PropEx.SetSprite1(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[709]);
			PropEx.SetSprite2(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite3(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite4(RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[709]);
		}
		if (ConfigSettings.Settings_LordType == 7)
		{
			PropEx.SetSprite1(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite2(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite3(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite4(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
		}
		else
		{
			PropEx.SetSprite1(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[711]);
			PropEx.SetSprite2(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite3(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite4(RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[711]);
		}
	}

	private void RefResolutionCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		if (RefResolutionCombo.SelectedItem != null)
		{
			resChanged = true;
			MainViewModel.Instance.OptionsApplyVisible = Visibility.Visible;
		}
	}

	private void RefScreenModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		if (RefScreenModeCombo.SelectedItem != null)
		{
			screenModeChanged = true;
			MainViewModel.Instance.OptionsApplyVisible = Visibility.Visible;
		}
	}

	private void UpdateResListbox(bool fromSettings = false)
	{
		int num;
		int num2;
		int num3;
		if (!fromSettings)
		{
			num = Screen.width;
			num2 = Screen.height;
			num3 = Screen.currentResolution.refreshRate;
			if (ConfigSettings.Settings_Vsync)
			{
				num3 = Application.targetFrameRate;
			}
		}
		else
		{
			num = ConfigSettings.Settings_LastFullscreenWidth;
			num2 = ConfigSettings.Settings_LastFullscreenHeight;
			num3 = ConfigSettings.Settings_LastFullscreenRefresh;
		}
		RefResolutionCombo.SelectedItem = null;
		ItemCollection.Enumerator enumerator = RefResolutionCombo.Items.GetEnumerator();
		while (enumerator.MoveNext())
		{
			ComboBoxItem comboBoxItem = (ComboBoxItem)enumerator.Current;
			Resolution resolution = (Resolution)comboBoxItem.Tag;
			if (num == resolution.width && num2 == resolution.height && Math.Abs(resolution.refreshRate - num3) < 2)
			{
				RefResolutionCombo.SelectedItem = comboBoxItem;
				break;
			}
		}
	}

	private void MasterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)RefMasterVolumeSlider.Value;
			MainViewModel.Instance.MasterVolumeValue = num.ToString();
			ConfigSettings.Settings_MasterVolume = (float)num / 100f;
			MyAudioManager.Instance.updateSFXVolumeFromSettings();
			MyAudioManager.Instance.updateSpeechVolumeFromSettings();
			MyAudioManager.Instance.updateMusicVolumeFromSettings();
			lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
		}
	}

	private void MusicVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)RefMusicVolumeSlider.Value;
			MainViewModel.Instance.MusicVolumeValue = num.ToString();
			ConfigSettings.Settings_MusicVolume = (float)num / 100f;
			MyAudioManager.Instance.updateMusicVolumeFromSettings();
			lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
		}
	}

	private void SpeechVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)RefSpeechVolumeSlider.Value;
			MainViewModel.Instance.SpeechVolumeValue = num.ToString();
			ConfigSettings.Settings_SpeechVolume = (float)num / 100f;
			MyAudioManager.Instance.updateSpeechVolumeFromSettings();
			lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
		}
	}

	private void UnitSpeechVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)RefUnitSpeechVolumeSlider.Value;
			MainViewModel.Instance.UnitSpeechVolumeValue = num.ToString();
			ConfigSettings.Settings_UnitSpeechVolume = (float)num / 100f;
			MyAudioManager.Instance.updateSpeechVolumeFromSettings();
			lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
		}
	}

	private void SFXVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)RefSFXVolumeSlider.Value;
			MainViewModel.Instance.SfxVolumeValue = num.ToString();
			ConfigSettings.Settings_SFXVolume = (float)num / 100f;
			MyAudioManager.Instance.updateSFXVolumeFromSettings();
			lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
		}
	}

	private void ScrollSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int settings_ScrollSpeed = (int)RefScrollSpeedSlider.Value;
			MainViewModel.Instance.ScrollSpeedValue = settings_ScrollSpeed.ToString();
			ConfigSettings.Settings_ScrollSpeed = settings_ScrollSpeed;
			lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
		}
	}

	private void GameSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)RefGameSpeedSlider.Value * 5;
			if (!Director.instance.MultiplayerGame)
			{
				MainViewModel.Instance.GameSpeedValue = num.ToString();
				Director.instance.SetEngineFrameRate(num);
				ConfigSettings.Settings_GameSpeed = num;
				lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
			}
		}
	}

	private void UIScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			MainViewModel.Instance.OptionsScaleApplyVisible = Visibility.Visible;
		}
	}

	private void LockCursor_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_LockCursor = RefLockCursorCheck.IsChecked.Value;
			Save();
		}
	}

	private void BuildingTooltipsCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ShowBuildingTooltips = RefBuildingTooltipsCheck.IsChecked.Value;
			Save();
		}
	}

	private void SteamHelp_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_UseSteamOverlayForHelp = RefSteamHelpCheck.IsChecked.Value;
			Save();
		}
	}

	private void CompassCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_Compass = RefCompassCheck.IsChecked.Value;
			Save();
			if (Director.instance.SimRunning || MainViewModel.Instance.Show_HUD_OptionsMP)
			{
				MainViewModel.Instance.IngameUI.setRotationImage(GameMap.instance.CurrentRotation());
				MainViewModel.Instance.Compass_Vis = ConfigSettings.Settings_Compass;
			}
		}
	}

	private void LocalTimeCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (!panelActive)
		{
			return;
		}
		ConfigSettings.Settings_ShowLocalTime = RefLocalTimeCheck.IsChecked.Value;
		Save();
		if (Director.instance.SimRunning || MainViewModel.Instance.Show_HUD_OptionsMP)
		{
			if (ConfigSettings.Settings_ShowLocalTime)
			{
				MainViewModel.Instance.ShowLocalTime = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.ShowLocalTime = Visibility.Collapsed;
			}
		}
	}

	private void GameTimeCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (!panelActive)
		{
			return;
		}
		ConfigSettings.Settings_ShowGameTime = RefGameTimeCheck.IsChecked.Value;
		Save();
		if (Director.instance.SimRunning || MainViewModel.Instance.Show_HUD_OptionsMP)
		{
			if (ConfigSettings.Settings_ShowGameTime && !MainViewModel.Instance.IsMapEditorMode)
			{
				MainViewModel.Instance.ShowGameTime = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.ShowGameTime = Visibility.Collapsed;
			}
		}
	}

	private void SandsTimerCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ShowSandsTimer = RefSandsTimerCheck.IsChecked.Value;
			Save();
			if (Director.instance.SimRunning && GameData.Instance.IsSandsOfTime())
			{
				MainViewModel.Instance.Show_OST_SandsOfTimeVis = ConfigSettings.Settings_ShowSandsTimer && !ConfigSettings.Settings_HideSoTTiming;
			}
		}
	}

	private void RadarZoomCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_RadarDefaultZoomedOut = RefRadarZoomCheck.IsChecked.Value;
			Save();
		}
	}

	private void CustomIntros_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_CustomIntros = RefCustomIntros.IsChecked.Value;
			Save();
		}
	}

	private void UISounds_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_PlayUISFX = RefUISoundsCheck.IsChecked.Value;
			Save();
		}
	}

	private void GenieSpeech_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_GenieSpeech = RefGenieSpeechCheck.IsChecked.Value;
			if (ConfigSettings.Settings_GenieSpeech)
			{
				EngineInterface.GameAction(Enums.GameActionCommand.GenieSpeech, 1, 1);
			}
			else
			{
				EngineInterface.GameAction(Enums.GameActionCommand.GenieSpeech, 0, 0);
			}
			Save();
		}
	}

	private void EnglishSpeech_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_EnglishSpeech = RefEnglishSpeechCheck.IsChecked.Value;
			Save();
		}
	}

	private void MuteInsult_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_MuteInsults = RefMuteInsultsCheck.IsChecked.Value;
			ConfigSettings.Settings_MuteInsultSpeech = RefMuteInsultSpeechCheck.IsChecked.Value;
			Save();
		}
	}

	private void MuteBackground_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_BackgroundAudio = !RefMuteBackgroundCheck.IsChecked.Value;
			Save();
		}
	}

	private void ReduceSounds_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ReduceMusicVolumeForSpeech = RefReduceSoundsCheck.IsChecked.Value;
			Save();
		}
	}

	private void CheatKeys_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_CheatKeysEnabled = RefCheatKeysCheck.IsChecked.Value;
			Save();
		}
	}

	private void Pings_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ShowPings = RefPingsCheck.IsChecked.Value;
			Save();
		}
	}

	private void ExtraZoom_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ExtraZoom = RefExtraZoomCheck.IsChecked.Value;
			Save();
		}
	}

	private void ShowMoat_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ShowPlannedMoat = RefShowMoatCheck.IsChecked.Value;
			if (ConfigSettings.Settings_ShowPlannedMoat)
			{
				EngineInterface.GameAction(Enums.GameActionCommand.ShowPlannedMoat, 1, 1);
			}
			else
			{
				EngineInterface.GameAction(Enums.GameActionCommand.ShowPlannedMoat, 0, 0);
			}
			Save();
		}
	}

	private void ConfirmDisband_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_Confirm_Disband_Troops = RefConfirmDisbandCheck.IsChecked.Value;
			Save();
		}
	}

	private void TroopMove_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_TroopMoveMode = RefTroopMoveCheck.IsChecked.Value;
			Save();
		}
	}

	private void Leaderboard_OptOut_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_Leaderboard_OptOut = RefLeaderboard_OptOut.IsChecked.Value;
			Save();
		}
	}

	private void Leaderboard_Names_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_Leaderboard_Names = RefLeaderboard_Names.IsChecked.Value;
			Save();
		}
	}

	private void Leaderboard_Images_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_Leaderboard_Images = RefLeaderboard_Images.IsChecked.Value;
			Save();
		}
	}

	private void SandsTimeDisable_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_HideSoTTiming = RefSandsTimeDisable.IsChecked.Value;
			MainViewModel.Instance.Show_LeaderboardOptIn = !ConfigSettings.Settings_HideSoTTiming;
			Save();
		}
	}

	private void MuteMPChat_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_MuteMPChat = RefChatMuteDisable.IsChecked.Value;
			Save();
		}
	}

	private void ArabicL2R_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ArabicL2R = RefArabicL2RCheck.IsChecked.Value;
			Save();
		}
	}

	private void VSyncCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			MainViewModel.Instance.OptionsApplyVisible = Visibility.Visible;
		}
	}

	public static void SetVSync(bool state)
	{
		if (state)
		{
			if ((Screen.fullScreenMode == FullScreenMode.FullScreenWindow || Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen) && ConfigSettings.Settings_LastFullscreenRefresh > 0)
			{
				Application.targetFrameRate = ConfigSettings.Settings_LastFullscreenRefresh;
			}
			else
			{
				Application.targetFrameRate = Screen.currentResolution.refreshRate;
			}
			QualitySettings.vSyncCount = 1;
			ConfigSettings.Settings_Vsync = true;
		}
		else
		{
			Application.targetFrameRate = 300;
			QualitySettings.vSyncCount = 0;
			ConfigSettings.Settings_Vsync = false;
		}
	}

	public void UpdateUIScaleSliderVis()
	{
		if (Screen.width <= 1366 || Screen.height <= 768)
		{
			RefUIScaleGrid.Visibility = Visibility.Hidden;
		}
		else
		{
			RefUIScaleGrid.Visibility = Visibility.Visible;
		}
	}

	private void NewsletterValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			RefNewsletterSignupButton.IsEnabled = IsValidEmail(RefTextBoxNewsletter.Text) && RefNewsletterCheck.IsChecked.Value;
		}
	}

	private void NewsletterCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			RefNewsletterSignupButton.IsEnabled = IsValidEmail(RefTextBoxNewsletter.Text) && RefNewsletterCheck.IsChecked.Value;
		}
	}

	public static bool IsValidEmail(string valueAsString)
	{
		if (valueAsString == null)
		{
			return false;
		}
		if (_regex != null)
		{
			if (valueAsString != null)
			{
				return _regex.Match(valueAsString).Length > 0;
			}
			return false;
		}
		int num = 0;
		for (int i = 0; i < valueAsString.Length; i++)
		{
			if (valueAsString[i] == '@')
			{
				num++;
			}
		}
		if (valueAsString != null && num == 1 && valueAsString[0] != '@')
		{
			return valueAsString[valueAsString.Length - 1] != '@';
		}
		return false;
	}

	public static Regex CreateRegEx()
	{
		TimeSpan matchTimeout = TimeSpan.FromSeconds(2.0);
		try
		{
			if (AppDomain.CurrentDomain.GetData("REGEX_DEFAULT_MATCH_TIMEOUT") == null)
			{
				return new Regex("^((([a-z]|\\d|[!#\\$%&'\\*\\+\\-\\/=\\?\\^_`{\\|}~]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])+(\\.([a-z]|\\d|[!#\\$%&'\\*\\+\\-\\/=\\?\\^_`{\\|}~]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])+)*)|((\\x22)((((\\x20|\\x09)*(\\x0d\\x0a))?(\\x20|\\x09)+)?(([\\x01-\\x08\\x0b\\x0c\\x0e-\\x1f\\x7f]|\\x21|[\\x23-\\x5b]|[\\x5d-\\x7e]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])|(\\\\([\\x01-\\x09\\x0b\\x0c\\x0d-\\x7f]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF]))))*(((\\x20|\\x09)*(\\x0d\\x0a))?(\\x20|\\x09)+)?(\\x22)))@((([a-z]|\\d|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])|(([a-z]|\\d|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])([a-z]|\\d|-|\\.|_|~|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])*([a-z]|\\d|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])))\\.)+(([a-z]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])|(([a-z]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])([a-z]|\\d|-|\\.|_|~|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])*([a-z]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])))\\.?$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled, matchTimeout);
			}
		}
		catch
		{
		}
		return new Regex("^((([a-z]|\\d|[!#\\$%&'\\*\\+\\-\\/=\\?\\^_`{\\|}~]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])+(\\.([a-z]|\\d|[!#\\$%&'\\*\\+\\-\\/=\\?\\^_`{\\|}~]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])+)*)|((\\x22)((((\\x20|\\x09)*(\\x0d\\x0a))?(\\x20|\\x09)+)?(([\\x01-\\x08\\x0b\\x0c\\x0e-\\x1f\\x7f]|\\x21|[\\x23-\\x5b]|[\\x5d-\\x7e]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])|(\\\\([\\x01-\\x09\\x0b\\x0c\\x0d-\\x7f]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF]))))*(((\\x20|\\x09)*(\\x0d\\x0a))?(\\x20|\\x09)+)?(\\x22)))@((([a-z]|\\d|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])|(([a-z]|\\d|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])([a-z]|\\d|-|\\.|_|~|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])*([a-z]|\\d|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])))\\.)+(([a-z]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])|(([a-z]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])([a-z]|\\d|-|\\.|_|~|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])*([a-z]|[\\u00A0-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFEF])))\\.?$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled);
	}

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_Options.xaml");
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
		if (eventName == "MouseEnter" && handlerName == "ChickenButtonEnter")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += ChickenButtonEnter;
			}
			return true;
		}
		return false;
	}

	public void ChickenButtonEnter(object sender, MouseEventArgs e)
	{
		SFXManager.instance.playUISound(137);
	}

	private void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	private void CreateHotkeyList()
	{
		hotKeyRows.Clear();
		string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 93);
		HotKeyEntry[] array;
		if (hotKeyTextDict == null)
		{
			hotKeyTextDict = new Dictionary<Enums.KeyFunctions, int>();
			array = hotKeyList;
			foreach (HotKeyEntry hotKeyEntry in array)
			{
				hotKeyTextDict[hotKeyEntry.function] = hotKeyEntry.textID;
			}
		}
		int width = 210;
		array = hotKeyList;
		foreach (HotKeyEntry hotKeyEntry2 in array)
		{
			HotKeyRow hotKeyRow = new HotKeyRow(this, width);
			if (hotKeyEntry2.textID >= 0)
			{
				hotKeyRow.Text1 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, hotKeyEntry2.textID);
			}
			else
			{
				hotKeyRow.Text1 = GetPlaceBuildingHotkeyText(hotKeyEntry2.textID);
			}
			KeyCode keyCode = KeyManager.instance.GetKeyCode(hotKeyEntry2.function, 0);
			KeyCode keyCode2 = KeyManager.instance.GetKeyCode(hotKeyEntry2.function, 1);
			if (keyCode == KeyCode.None && keyCode2 == KeyCode.None)
			{
				hotKeyRow.Text2 = text;
			}
			else if (keyCode2 == KeyCode.None)
			{
				hotKeyRow.Text2 = GetKeyCodeString(keyCode);
			}
			else
			{
				hotKeyRow.Text2 = GetKeyCodeString(keyCode) + " / " + GetKeyCodeString(keyCode2);
			}
			int function = (int)hotKeyEntry2.function;
			hotKeyRow.DataValue = function.ToString();
			hotKeyRow.iDataValue = (int)hotKeyEntry2.function;
			hotKeyRows.Add(hotKeyRow);
		}
		RefHotKeyList.ItemsSource = hotKeyRows;
	}

	private static string GetPlaceBuildingHotkeyText(int value)
	{
		string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 371) + " ";
		switch (value)
		{
		case -1:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 14);
			break;
		case -2:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 15);
			break;
		case -3:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 12);
			break;
		case -4:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 13);
			break;
		case -5:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 9);
			break;
		case -6:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 10);
			break;
		case -7:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 348);
			break;
		case -8:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 8);
			break;
		case -9:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 21);
			break;
		case -10:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 22);
			break;
		case -11:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 23);
			break;
		case -12:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 24);
			break;
		case -13:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 25);
			break;
		case -14:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 66);
			break;
		case -15:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 67);
			break;
		case -16:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 133);
			break;
		case -17:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 197);
			break;
		case -18:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 11);
			break;
		case -19:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 139);
			break;
		case -20:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 29);
			break;
		case -21:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 91);
			break;
		case -22:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 30);
			break;
		case -23:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 279);
			break;
		case -24:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 204);
			break;
		case -25:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 17);
			break;
		case -26:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 16);
			break;
		case -27:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 31);
			break;
		case -28:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 92);
			break;
		case -29:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 45);
			break;
		case -30:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 42);
			break;
		case -31:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 41);
			break;
		case -32:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 50);
			break;
		case -33:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 43);
			break;
		case -34:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 44);
			break;
		case -35:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 49);
			break;
		case -36:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 62);
			break;
		case -37:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 61);
			break;
		case -38:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 59);
			break;
		case -39:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 58);
			break;
		case -40:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 60);
			break;
		case -41:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 39);
			break;
		case -42:
			text = ((ConfigSettings.Settings_LordType == 1 || ConfigSettings.Settings_LordType == 2 || ConfigSettings.Settings_LordType == 6 || ConfigSettings.Settings_LordType == 7) ? (text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 0)) : (text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 101)));
			break;
		case -43:
			text = ((ConfigSettings.Settings_LordType == 1 || ConfigSettings.Settings_LordType == 2 || ConfigSettings.Settings_LordType == 6 || ConfigSettings.Settings_LordType == 7) ? (text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 1)) : (text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 102)));
			break;
		case -44:
			text = ((ConfigSettings.Settings_LordType == 1 || ConfigSettings.Settings_LordType == 2 || ConfigSettings.Settings_LordType == 6 || ConfigSettings.Settings_LordType == 7) ? (text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_ISLAMIC, 2)) : (text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 103)));
			break;
		case -45:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 65);
			break;
		case -46:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 47);
			break;
		case -47:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_IN_WATERPOT, 0);
			break;
		case -48:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 54);
			break;
		case -49:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 55);
			break;
		case -50:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 51);
			break;
		case -51:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 53);
			break;
		case -52:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 52);
			break;
		case -53:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 46);
			break;
		case -54:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 56);
			break;
		case -55:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 48);
			break;
		case -56:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 57);
			break;
		case -57:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 64);
			break;
		case -58:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 72);
			break;
		case -59:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 283);
			break;
		case -60:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 104);
			break;
		case -61:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 107);
			break;
		case -62:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 106);
			break;
		case -63:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 280);
			break;
		case -64:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 281);
			break;
		case -65:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 95);
			break;
		case -66:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 215);
			break;
		case -67:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 96);
			break;
		case -68:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 97);
			break;
		case -69:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 71);
			break;
		case -70:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 271);
			break;
		case -71:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 75);
			break;
		case -72:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 309);
			break;
		case -73:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 272);
			break;
		case -74:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 274);
			break;
		case -75:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 275);
			break;
		case -76:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 273);
			break;
		case -77:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 277);
			break;
		case -78:
			text += Translate.Instance.lookUpText(Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, 278);
			break;
		}
		return text;
	}

	public static string GetKeyCodeString(KeyCode code)
	{
		int num = (int)(code & (KeyCode)65535);
		bool num2 = (code & (KeyCode)65536) > KeyCode.None;
		bool flag = (code & (KeyCode)131072) > KeyCode.None;
		bool flag2 = (code & (KeyCode)262144) > KeyCode.None;
		string text = "";
		if (num2)
		{
			text = text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 78) + " ";
		}
		if (flag)
		{
			text = text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 79) + " ";
		}
		if (flag2)
		{
			text = text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 80) + " ";
		}
		switch (num)
		{
		case 48:
			return text + "0";
		case 49:
			return text + "1";
		case 50:
			return text + "2";
		case 51:
			return text + "3";
		case 52:
			return text + "4";
		case 53:
			return text + "5";
		case 54:
			return text + "6";
		case 55:
			return text + "7";
		case 56:
			return text + "8";
		case 57:
			return text + "9";
		case 96:
			return text + "`";
		case 92:
			return text + "\\";
		case 45:
			return text + "-";
		case 61:
			return text + "=";
		case 91:
			return text + "[";
		case 93:
			return text + "]";
		case 59:
			return text + ";";
		case 39:
			return text + "'";
		case 44:
			return text + ",";
		case 46:
			return text + ".";
		case 47:
			return text + "/";
		case 32:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 109);
		case 256:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 0";
		case 257:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 1";
		case 258:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 2";
		case 259:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 3";
		case 260:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 4";
		case 261:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 5";
		case 262:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 6";
		case 263:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 7";
		case 264:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 8";
		case 265:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 9";
		case 325:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 112) + " 2";
		case 326:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 112) + " 3";
		case 327:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 112) + " 4";
		case 328:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 112) + " 5";
		case 329:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 112) + " 6";
		case 266:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " .";
		case 270:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " +";
		case 269:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " -";
		case 267:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " /";
		case 268:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " *";
		case 271:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 82);
		case 272:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " =";
		case 9:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 83);
		case 301:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 84);
		case 13:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 85);
		case 8:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 86);
		case 273:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 87);
		case 274:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 88);
		case 275:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 89);
		case 276:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 90);
		case 277:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 91);
		case 278:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 92);
		case 279:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 93);
		case 280:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 94);
		case 281:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 95);
		case 127:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 96);
		case 300:
			return text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 97);
		default:
		{
			string text2 = text;
			KeyCode keyCode = (KeyCode)num;
			return text2 + keyCode;
		}
		}
	}
}
