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
	public class HotKeyEntry
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

	public int menuSection;

	public bool panelActive;

	public Grid RefVideoSettings;

	public Grid RefSoundSettings;

	public Grid RefKeySettings;

	public Grid RefControlSettings;

	public Grid RefNameSettings;

	public Grid RefCoASettings;

	public Grid RefCheatSettings;

	public Button RefCOA_Button;

	public ComboBox RefResolutionCombo;

	public ComboBox RefScreenModeCombo;

	public Slider RefMasterVolumeSlider;

	public Slider RefMusicVolumeSlider;

	public Slider RefSpeechVolumeSlider;

	public Slider RefUnitSpeechVolumeSlider;

	public Slider RefSFXVolumeSlider;

	public Slider RefScrollSpeedSlider;

	public Slider RefGameSpeedSlider;

	public Slider RefUIScaleSlider;

	public TextBox RefTextBoxChangeName;

	public CheckBox RefVSyncCheck;

	public CheckBox RefLockCursorCheck;

	public CheckBox RefBuildingTooltipsCheck;

	public TextBlock RefBuildingTooltipsCheckText;

	public CheckBox RefSteamHelpCheck;

	public TextBlock RefSteamHelpCheckText;

	public CheckBox RefCompassCheck;

	public CheckBox RefLocalTimeCheck;

	public CheckBox RefGameTimeCheck;

	public CheckBox RefSandsTimerCheck;

	public CheckBox RefRadarZoomCheck;

	public TextBlock RefRadarZoomCheckText;

	public TextBlock RefSteamIdentity;

	public CheckBox RefCustomIntros;

	public TextBlock RefCustomIntrosName;

	public CheckBox RefUISoundsCheck;

	public CheckBox RefGenieSpeechCheck;

	public CheckBox RefEnglishSpeechCheck;

	public CheckBox RefMuteInsultSpeechCheck;

	public TextBlock RefMuteInsultSpeechCheckText;

	public CheckBox RefMuteInsultsCheck;

	public TextBlock RefMuteInsultsCheckText;

	public CheckBox RefMuteBackgroundCheck;

	public TextBlock RefMuteBackgroundCheckText;

	public CheckBox RefReduceSoundsCheck;

	public TextBlock RefReduceSoundsCheckText;

	public CheckBox RefCheatKeysCheck;

	public TextBlock RefPingsCheckText;

	public CheckBox RefPingsCheck;

	public TextBlock RefExtraZoomCheckText;

	public CheckBox RefExtraZoomCheck;

	public Button RefSFXDefaultsButton;

	public CheckBox RefShowMoatCheck;

	public TextBlock RefShowMoatText;

	public CheckBox RefConfirmDisbandCheck;

	public TextBlock RefConfirmDisbandText;

	public CheckBox RefLeaderboard_OptOut;

	public CheckBox RefLeaderboard_Names;

	public CheckBox RefLeaderboard_Images;

	public CheckBox RefNewsletterCheck;

	public CheckBox RefSandsTimeDisable;

	public CheckBox RefChatMuteDisable;

	public CheckBox RefArabicL2RCheck;

	public TextBlock RefLeaderboard_OptOutText;

	public TextBlock RefLeaderboard_NamesText;

	public TextBlock RefLeaderboard_ImagesText;

	public Button RefNewsletterSignupButton;

	public TextBox RefTextBoxNewsletter;

	public Image RefScribeLock;

	public Button RefOptionsChickenButton;

	public TextBlock RefPlayerSettingsHeading;

	public RadioButton RefPlayerColourShield1;

	public RadioButton RefPlayerColourShield2;

	public RadioButton RefPlayerColourShield3;

	public RadioButton RefPlayerColourShield4;

	public RadioButton RefPlayerColourShield5;

	public RadioButton RefPlayerColourShield6;

	public RadioButton RefPlayerColourShield7;

	public RadioButton RefPlayerColourShield8;

	public Grid RefOptionsHotKeyPanel;

	public Grid RefUIScaleGrid;

	public ListView RefHotKeyList;

	public Button RefOptionsHotKeyNewKeyApply;

	public Button RefCursorSystemButton;

	public Button RefCursorSwordButton;

	public Button RefCursorSwordXButton;

	public Button RefCursorSwordX2Button;

	public Button RefScribeClassicButton;

	public Button RefScribeModernButton;

	public Button RefCrusaderLordButton;

	public Button RefArabicLordButton;

	public Button RefBedouinLordButton;

	public Button RefScribeLordButton;

	public Button RefFemaleLordButton;

	public Button RefBessyLordButton;

	public Button RefArabicLordFemaleButton;

	public Button RefBedouinLordFemaleButton;

	public Button RefOptionsKeys1;

	public Button RefOptionsKeys2;

	public static HUD_Options instance1 = null;

	public static HUD_Options instance2 = null;

	public static HUD_Options instance3 = null;

	public ObservableCollection<HotKeyRow> hotKeyRows = new ObservableCollection<HotKeyRow>();

	public Enums.KeyFunctions selectedFunction = Enums.KeyFunctions.NumActions;

	public int selectedColumn = -1;

	public bool resChanged;

	public bool screenModeChanged;

	public DateTime lastDynamicChanged = DateTime.MaxValue;

	public static Regex _regex = CreateRegEx();

	public HotKeyEntry[] hotKeyList = new HotKeyEntry[179]
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

	public Dictionary<Enums.KeyFunctions, int> hotKeyTextDict;

	public HUD_Options()
	{
		//IL_0b1c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b26: Expected O, but got Unknown
		//IL_0b32: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b3c: Expected O, but got Unknown
		//IL_0b48: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b52: Expected O, but got Unknown
		//IL_0b5e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b68: Expected O, but got Unknown
		//IL_0b74: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b7e: Expected O, but got Unknown
		//IL_0b8a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b94: Expected O, but got Unknown
		//IL_0ba0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0baa: Expected O, but got Unknown
		//IL_0bb6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bc0: Expected O, but got Unknown
		//IL_0bcc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bd6: Expected O, but got Unknown
		//IL_0be2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bec: Expected O, but got Unknown
		//IL_0bf8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c02: Expected O, but got Unknown
		//IL_0c0e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c18: Expected O, but got Unknown
		//IL_0c24: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c2e: Expected O, but got Unknown
		//IL_0c51: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c5b: Expected O, but got Unknown
		//IL_0c7e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c88: Expected O, but got Unknown
		//IL_0cab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cb5: Expected O, but got Unknown
		//IL_0cd8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ce2: Expected O, but got Unknown
		//IL_0d05: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d0f: Expected O, but got Unknown
		//IL_0d32: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d3c: Expected O, but got Unknown
		//IL_0d5f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d69: Expected O, but got Unknown
		//IL_0d8c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d96: Expected O, but got Unknown
		//IL_0da3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dad: Expected O, but got Unknown
		//IL_0db9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dc3: Expected O, but got Unknown
		//IL_0dd0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dda: Expected O, but got Unknown
		//IL_0de7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0df1: Expected O, but got Unknown
		//IL_0dfd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e07: Expected O, but got Unknown
		//IL_0e14: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e1e: Expected O, but got Unknown
		//IL_0e2b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e35: Expected O, but got Unknown
		//IL_0e41: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e4b: Expected O, but got Unknown
		//IL_0e58: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e62: Expected O, but got Unknown
		//IL_0e6f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e79: Expected O, but got Unknown
		//IL_0e85: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e8f: Expected O, but got Unknown
		//IL_0e9c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ea6: Expected O, but got Unknown
		//IL_0eb3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ebd: Expected O, but got Unknown
		//IL_0ec9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ed3: Expected O, but got Unknown
		//IL_0edf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ee9: Expected O, but got Unknown
		//IL_0ef6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f00: Expected O, but got Unknown
		//IL_0f0d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f17: Expected O, but got Unknown
		//IL_0f23: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f2d: Expected O, but got Unknown
		//IL_0f39: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f43: Expected O, but got Unknown
		//IL_0f50: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f5a: Expected O, but got Unknown
		//IL_0f67: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f71: Expected O, but got Unknown
		//IL_0f7d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f87: Expected O, but got Unknown
		//IL_0f94: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f9e: Expected O, but got Unknown
		//IL_0fab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fb5: Expected O, but got Unknown
		//IL_0fc1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fcb: Expected O, but got Unknown
		//IL_0fd8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fe2: Expected O, but got Unknown
		//IL_0fef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ff9: Expected O, but got Unknown
		//IL_1005: Unknown result type (might be due to invalid IL or missing references)
		//IL_100f: Expected O, but got Unknown
		//IL_101c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1026: Expected O, but got Unknown
		//IL_1033: Unknown result type (might be due to invalid IL or missing references)
		//IL_103d: Expected O, but got Unknown
		//IL_1049: Unknown result type (might be due to invalid IL or missing references)
		//IL_1053: Expected O, but got Unknown
		//IL_1060: Unknown result type (might be due to invalid IL or missing references)
		//IL_106a: Expected O, but got Unknown
		//IL_1077: Unknown result type (might be due to invalid IL or missing references)
		//IL_1081: Expected O, but got Unknown
		//IL_108d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1097: Expected O, but got Unknown
		//IL_10a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_10ad: Expected O, but got Unknown
		//IL_10ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_10c4: Expected O, but got Unknown
		//IL_10d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_10db: Expected O, but got Unknown
		//IL_10e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_10f1: Expected O, but got Unknown
		//IL_10fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_1107: Expected O, but got Unknown
		//IL_1113: Unknown result type (might be due to invalid IL or missing references)
		//IL_111d: Expected O, but got Unknown
		//IL_112a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1134: Expected O, but got Unknown
		//IL_1141: Unknown result type (might be due to invalid IL or missing references)
		//IL_114b: Expected O, but got Unknown
		//IL_1157: Unknown result type (might be due to invalid IL or missing references)
		//IL_1161: Expected O, but got Unknown
		//IL_116e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1178: Expected O, but got Unknown
		//IL_1185: Unknown result type (might be due to invalid IL or missing references)
		//IL_118f: Expected O, but got Unknown
		//IL_119b: Unknown result type (might be due to invalid IL or missing references)
		//IL_11a5: Expected O, but got Unknown
		//IL_11b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_11bc: Expected O, but got Unknown
		//IL_11c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_11d3: Expected O, but got Unknown
		//IL_11df: Unknown result type (might be due to invalid IL or missing references)
		//IL_11e9: Expected O, but got Unknown
		//IL_11f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_1200: Expected O, but got Unknown
		//IL_120d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1217: Expected O, but got Unknown
		//IL_1223: Unknown result type (might be due to invalid IL or missing references)
		//IL_122d: Expected O, but got Unknown
		//IL_1239: Unknown result type (might be due to invalid IL or missing references)
		//IL_1243: Expected O, but got Unknown
		//IL_1250: Unknown result type (might be due to invalid IL or missing references)
		//IL_125a: Expected O, but got Unknown
		//IL_1267: Unknown result type (might be due to invalid IL or missing references)
		//IL_1271: Expected O, but got Unknown
		//IL_127d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1287: Expected O, but got Unknown
		//IL_1293: Unknown result type (might be due to invalid IL or missing references)
		//IL_129d: Expected O, but got Unknown
		//IL_12aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_12b4: Expected O, but got Unknown
		//IL_12c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_12cb: Expected O, but got Unknown
		//IL_12d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_12e1: Expected O, but got Unknown
		//IL_12ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_12f7: Expected O, but got Unknown
		//IL_1304: Unknown result type (might be due to invalid IL or missing references)
		//IL_130e: Expected O, but got Unknown
		//IL_131b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1325: Expected O, but got Unknown
		//IL_1331: Unknown result type (might be due to invalid IL or missing references)
		//IL_133b: Expected O, but got Unknown
		//IL_1347: Unknown result type (might be due to invalid IL or missing references)
		//IL_1351: Expected O, but got Unknown
		//IL_135d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1367: Expected O, but got Unknown
		//IL_1374: Unknown result type (might be due to invalid IL or missing references)
		//IL_137e: Expected O, but got Unknown
		//IL_138b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1395: Expected O, but got Unknown
		//IL_13a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_13ab: Expected O, but got Unknown
		//IL_13b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_13c2: Expected O, but got Unknown
		//IL_13cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_13d9: Expected O, but got Unknown
		//IL_13e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_13ef: Expected O, but got Unknown
		//IL_13fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_1405: Expected O, but got Unknown
		//IL_1412: Unknown result type (might be due to invalid IL or missing references)
		//IL_141c: Expected O, but got Unknown
		//IL_1429: Unknown result type (might be due to invalid IL or missing references)
		//IL_1433: Expected O, but got Unknown
		//IL_143f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1449: Expected O, but got Unknown
		//IL_1455: Unknown result type (might be due to invalid IL or missing references)
		//IL_145f: Expected O, but got Unknown
		//IL_146c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1476: Expected O, but got Unknown
		//IL_1483: Unknown result type (might be due to invalid IL or missing references)
		//IL_148d: Expected O, but got Unknown
		//IL_1499: Unknown result type (might be due to invalid IL or missing references)
		//IL_14a3: Expected O, but got Unknown
		//IL_14af: Unknown result type (might be due to invalid IL or missing references)
		//IL_14b9: Expected O, but got Unknown
		//IL_14c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_14d0: Expected O, but got Unknown
		//IL_14dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_14e7: Expected O, but got Unknown
		//IL_14f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_14fd: Expected O, but got Unknown
		//IL_1509: Unknown result type (might be due to invalid IL or missing references)
		//IL_1513: Expected O, but got Unknown
		//IL_151f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1529: Expected O, but got Unknown
		//IL_1535: Unknown result type (might be due to invalid IL or missing references)
		//IL_153f: Expected O, but got Unknown
		//IL_154b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1555: Expected O, but got Unknown
		//IL_1561: Unknown result type (might be due to invalid IL or missing references)
		//IL_156b: Expected O, but got Unknown
		//IL_1577: Unknown result type (might be due to invalid IL or missing references)
		//IL_1581: Expected O, but got Unknown
		//IL_158d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1597: Expected O, but got Unknown
		//IL_15a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_15ad: Expected O, but got Unknown
		//IL_15b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_15c3: Expected O, but got Unknown
		//IL_15d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_15da: Expected O, but got Unknown
		//IL_15e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_15f1: Expected O, but got Unknown
		//IL_15fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_1607: Expected O, but got Unknown
		//IL_1613: Unknown result type (might be due to invalid IL or missing references)
		//IL_161d: Expected O, but got Unknown
		//IL_162a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1634: Expected O, but got Unknown
		//IL_1641: Unknown result type (might be due to invalid IL or missing references)
		//IL_164b: Expected O, but got Unknown
		//IL_1657: Unknown result type (might be due to invalid IL or missing references)
		//IL_1661: Expected O, but got Unknown
		//IL_166d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1677: Expected O, but got Unknown
		//IL_1684: Unknown result type (might be due to invalid IL or missing references)
		//IL_168e: Expected O, but got Unknown
		//IL_169b: Unknown result type (might be due to invalid IL or missing references)
		//IL_16a5: Expected O, but got Unknown
		//IL_16b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_16bb: Expected O, but got Unknown
		//IL_16c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_16d1: Expected O, but got Unknown
		//IL_16de: Unknown result type (might be due to invalid IL or missing references)
		//IL_16e8: Expected O, but got Unknown
		//IL_16f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_16ff: Expected O, but got Unknown
		//IL_170b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1715: Expected O, but got Unknown
		//IL_1722: Unknown result type (might be due to invalid IL or missing references)
		//IL_172c: Expected O, but got Unknown
		//IL_1739: Unknown result type (might be due to invalid IL or missing references)
		//IL_1743: Expected O, but got Unknown
		//IL_174f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1759: Expected O, but got Unknown
		//IL_1766: Unknown result type (might be due to invalid IL or missing references)
		//IL_1770: Expected O, but got Unknown
		//IL_177d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1787: Expected O, but got Unknown
		//IL_1793: Unknown result type (might be due to invalid IL or missing references)
		//IL_179d: Expected O, but got Unknown
		//IL_17a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_17b3: Expected O, but got Unknown
		//IL_17bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_17c9: Expected O, but got Unknown
		//IL_17d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_17df: Expected O, but got Unknown
		//IL_17ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_17f6: Expected O, but got Unknown
		//IL_1803: Unknown result type (might be due to invalid IL or missing references)
		//IL_180d: Expected O, but got Unknown
		//IL_1819: Unknown result type (might be due to invalid IL or missing references)
		//IL_1823: Expected O, but got Unknown
		//IL_1834: Unknown result type (might be due to invalid IL or missing references)
		//IL_1839: Unknown result type (might be due to invalid IL or missing references)
		//IL_19ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_19b6: Expected O, but got Unknown
		//IL_19b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_19bc: Expected O, but got Unknown
		//IL_19ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a22: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a28: Expected O, but got Unknown
		//IL_1a6b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a8e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1a94: Expected O, but got Unknown
		//IL_1ad7: Unknown result type (might be due to invalid IL or missing references)
		//IL_1afa: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b00: Invalid comparison between Unknown and I4
		//IL_1b10: Unknown result type (might be due to invalid IL or missing references)
		//IL_185d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1864: Expected O, but got Unknown
		//IL_1b3e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1b48: Expected O, but got Unknown
		//IL_1935: Unknown result type (might be due to invalid IL or missing references)
		//IL_1963: Unknown result type (might be due to invalid IL or missing references)
		//IL_1c43: Unknown result type (might be due to invalid IL or missing references)
		//IL_1c9c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1f44: Unknown result type (might be due to invalid IL or missing references)
		//IL_1f4e: Expected O, but got Unknown
		//IL_1f5b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1f65: Expected O, but got Unknown
		//IL_1f71: Unknown result type (might be due to invalid IL or missing references)
		//IL_1f7b: Expected O, but got Unknown
		//IL_1f87: Unknown result type (might be due to invalid IL or missing references)
		//IL_1f91: Expected O, but got Unknown
		//IL_1f9d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1fa7: Expected O, but got Unknown
		//IL_1fb3: Unknown result type (might be due to invalid IL or missing references)
		//IL_1fbd: Expected O, but got Unknown
		//IL_1fc9: Unknown result type (might be due to invalid IL or missing references)
		//IL_1fd3: Expected O, but got Unknown
		//IL_1fdf: Unknown result type (might be due to invalid IL or missing references)
		//IL_1fe9: Expected O, but got Unknown
		//IL_1ff5: Unknown result type (might be due to invalid IL or missing references)
		//IL_1fff: Expected O, but got Unknown
		//IL_200b: Unknown result type (might be due to invalid IL or missing references)
		//IL_2015: Expected O, but got Unknown
		//IL_2021: Unknown result type (might be due to invalid IL or missing references)
		//IL_202b: Expected O, but got Unknown
		//IL_2037: Unknown result type (might be due to invalid IL or missing references)
		//IL_2041: Expected O, but got Unknown
		//IL_204d: Unknown result type (might be due to invalid IL or missing references)
		//IL_2057: Expected O, but got Unknown
		//IL_2063: Unknown result type (might be due to invalid IL or missing references)
		//IL_206d: Expected O, but got Unknown
		//IL_2079: Unknown result type (might be due to invalid IL or missing references)
		//IL_2083: Expected O, but got Unknown
		//IL_208f: Unknown result type (might be due to invalid IL or missing references)
		//IL_2099: Expected O, but got Unknown
		//IL_20a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_20af: Expected O, but got Unknown
		//IL_20bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_20c5: Expected O, but got Unknown
		//IL_20d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_20db: Expected O, but got Unknown
		//IL_210c: Unknown result type (might be due to invalid IL or missing references)
		//IL_2140: Unknown result type (might be due to invalid IL or missing references)
		InitializeComponent();
		if ((BaseComponent)(object)instance1 == (BaseComponent)null)
		{
			instance1 = this;
		}
		else if ((BaseComponent)(object)instance2 == (BaseComponent)null)
		{
			instance2 = this;
		}
		else if ((BaseComponent)(object)instance3 == (BaseComponent)null)
		{
			instance3 = this;
		}
		RefVideoSettings = (Grid)((FrameworkElement)this).FindName("VideoSettings");
		RefSoundSettings = (Grid)((FrameworkElement)this).FindName("SoundSettings");
		RefKeySettings = (Grid)((FrameworkElement)this).FindName("KeySettings");
		RefControlSettings = (Grid)((FrameworkElement)this).FindName("ControlSettings");
		RefNameSettings = (Grid)((FrameworkElement)this).FindName("NameSettings");
		RefCoASettings = (Grid)((FrameworkElement)this).FindName("CoASettings");
		RefCheatSettings = (Grid)((FrameworkElement)this).FindName("CheatSettings");
		RefResolutionCombo = (ComboBox)((FrameworkElement)this).FindName("ResolutionCombo");
		RefScreenModeCombo = (ComboBox)((FrameworkElement)this).FindName("ScreenModeCombo");
		RefOptionsHotKeyPanel = (Grid)((FrameworkElement)this).FindName("OptionsHotKeyPanel");
		RefUIScaleGrid = (Grid)((FrameworkElement)this).FindName("UIScaleGrid");
		RefCOA_Button = (Button)((FrameworkElement)this).FindName("COA_Button");
		RefMasterVolumeSlider = (Slider)((FrameworkElement)this).FindName("MasterVolumeSlider");
		((RangeBase)RefMasterVolumeSlider).ValueChanged += MasterVolumeSlider_ValueChanged;
		RefMusicVolumeSlider = (Slider)((FrameworkElement)this).FindName("MusicVolumeSlider");
		((RangeBase)RefMusicVolumeSlider).ValueChanged += MusicVolumeSlider_ValueChanged;
		RefSpeechVolumeSlider = (Slider)((FrameworkElement)this).FindName("SpeechVolumeSlider");
		((RangeBase)RefSpeechVolumeSlider).ValueChanged += SpeechVolumeSlider_ValueChanged;
		RefUnitSpeechVolumeSlider = (Slider)((FrameworkElement)this).FindName("UnitSpeechVolumeSlider");
		((RangeBase)RefUnitSpeechVolumeSlider).ValueChanged += UnitSpeechVolumeSlider_ValueChanged;
		RefSFXVolumeSlider = (Slider)((FrameworkElement)this).FindName("SFXVolumeSlider");
		((RangeBase)RefSFXVolumeSlider).ValueChanged += SFXVolumeSlider_ValueChanged;
		RefScrollSpeedSlider = (Slider)((FrameworkElement)this).FindName("ScrollSpeedSlider");
		((RangeBase)RefScrollSpeedSlider).ValueChanged += ScrollSpeedSlider_ValueChanged;
		RefGameSpeedSlider = (Slider)((FrameworkElement)this).FindName("GameSpeedSlider");
		((RangeBase)RefGameSpeedSlider).ValueChanged += GameSpeedSlider_ValueChanged;
		RefUIScaleSlider = (Slider)((FrameworkElement)this).FindName("UIScaleSlider");
		((RangeBase)RefUIScaleSlider).ValueChanged += UIScaleSlider_ValueChanged;
		RefTextBoxChangeName = (TextBox)((FrameworkElement)this).FindName("TextBoxChangeName");
		((UIElement)RefTextBoxChangeName).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		RefTextBoxNewsletter = (TextBox)((FrameworkElement)this).FindName("TextBoxNewsletter");
		((UIElement)RefTextBoxNewsletter).IsKeyboardFocusedChanged += new DependencyPropertyChangedEventHandler(TextInputFocus);
		((TextBoxBase)RefTextBoxNewsletter).TextChanged += new RoutedEventHandler(NewsletterValueChanged);
		RefVSyncCheck = (CheckBox)((FrameworkElement)this).FindName("VSyncCheck");
		((ToggleButton)RefVSyncCheck).Checked += new RoutedEventHandler(VSyncCheck_ValueChanged);
		((ToggleButton)RefVSyncCheck).Unchecked += new RoutedEventHandler(VSyncCheck_ValueChanged);
		RefLockCursorCheck = (CheckBox)((FrameworkElement)this).FindName("LockCursorCheck");
		((ToggleButton)RefLockCursorCheck).Checked += new RoutedEventHandler(LockCursor_ValueChanged);
		((ToggleButton)RefLockCursorCheck).Unchecked += new RoutedEventHandler(LockCursor_ValueChanged);
		RefBuildingTooltipsCheck = (CheckBox)((FrameworkElement)this).FindName("BuildingTooltipsCheck");
		((ToggleButton)RefBuildingTooltipsCheck).Checked += new RoutedEventHandler(BuildingTooltipsCheck_ValueChanged);
		((ToggleButton)RefBuildingTooltipsCheck).Unchecked += new RoutedEventHandler(BuildingTooltipsCheck_ValueChanged);
		RefBuildingTooltipsCheckText = (TextBlock)((FrameworkElement)this).FindName("BuildingTooltipsCheckText");
		RefSteamHelpCheck = (CheckBox)((FrameworkElement)this).FindName("SteamHelpCheck");
		((ToggleButton)RefSteamHelpCheck).Checked += new RoutedEventHandler(SteamHelp_ValueChanged);
		((ToggleButton)RefSteamHelpCheck).Unchecked += new RoutedEventHandler(SteamHelp_ValueChanged);
		RefSteamHelpCheckText = (TextBlock)((FrameworkElement)this).FindName("SteamHelpCheckText");
		RefCompassCheck = (CheckBox)((FrameworkElement)this).FindName("CompassCheck");
		((ToggleButton)RefCompassCheck).Checked += new RoutedEventHandler(CompassCheck_ValueChanged);
		((ToggleButton)RefCompassCheck).Unchecked += new RoutedEventHandler(CompassCheck_ValueChanged);
		RefLocalTimeCheck = (CheckBox)((FrameworkElement)this).FindName("LocalTimeCheck");
		((ToggleButton)RefLocalTimeCheck).Checked += new RoutedEventHandler(LocalTimeCheck_ValueChanged);
		((ToggleButton)RefLocalTimeCheck).Unchecked += new RoutedEventHandler(LocalTimeCheck_ValueChanged);
		RefGameTimeCheck = (CheckBox)((FrameworkElement)this).FindName("GameTimeCheck");
		((ToggleButton)RefGameTimeCheck).Checked += new RoutedEventHandler(GameTimeCheck_ValueChanged);
		((ToggleButton)RefGameTimeCheck).Unchecked += new RoutedEventHandler(GameTimeCheck_ValueChanged);
		RefSandsTimerCheck = (CheckBox)((FrameworkElement)this).FindName("SandsTimerCheck");
		((ToggleButton)RefSandsTimerCheck).Checked += new RoutedEventHandler(SandsTimerCheck_ValueChanged);
		((ToggleButton)RefSandsTimerCheck).Unchecked += new RoutedEventHandler(SandsTimerCheck_ValueChanged);
		RefRadarZoomCheck = (CheckBox)((FrameworkElement)this).FindName("RadarZoomCheck");
		((ToggleButton)RefRadarZoomCheck).Checked += new RoutedEventHandler(RadarZoomCheck_ValueChanged);
		((ToggleButton)RefRadarZoomCheck).Unchecked += new RoutedEventHandler(RadarZoomCheck_ValueChanged);
		RefRadarZoomCheckText = (TextBlock)((FrameworkElement)this).FindName("RadarZoomCheckText");
		RefCustomIntros = (CheckBox)((FrameworkElement)this).FindName("CustomIntros");
		((ToggleButton)RefCustomIntros).Checked += new RoutedEventHandler(CustomIntros_ValueChanged);
		((ToggleButton)RefCustomIntros).Unchecked += new RoutedEventHandler(CustomIntros_ValueChanged);
		RefCustomIntrosName = (TextBlock)((FrameworkElement)this).FindName("CustomIntrosName");
		RefSteamIdentity = (TextBlock)((FrameworkElement)this).FindName("SteamIdentity");
		RefUISoundsCheck = (CheckBox)((FrameworkElement)this).FindName("UISoundsCheck");
		((ToggleButton)RefUISoundsCheck).Checked += new RoutedEventHandler(UISounds_ValueChanged);
		((ToggleButton)RefUISoundsCheck).Unchecked += new RoutedEventHandler(UISounds_ValueChanged);
		RefGenieSpeechCheck = (CheckBox)((FrameworkElement)this).FindName("GenieSpeechCheck");
		((ToggleButton)RefGenieSpeechCheck).Checked += new RoutedEventHandler(GenieSpeech_ValueChanged);
		((ToggleButton)RefGenieSpeechCheck).Unchecked += new RoutedEventHandler(GenieSpeech_ValueChanged);
		RefEnglishSpeechCheck = (CheckBox)((FrameworkElement)this).FindName("EnglishSpeechCheck");
		((ToggleButton)RefEnglishSpeechCheck).Checked += new RoutedEventHandler(EnglishSpeech_ValueChanged);
		((ToggleButton)RefEnglishSpeechCheck).Unchecked += new RoutedEventHandler(EnglishSpeech_ValueChanged);
		RefMuteInsultSpeechCheck = (CheckBox)((FrameworkElement)this).FindName("MuteInsultSpeechCheck");
		((ToggleButton)RefMuteInsultSpeechCheck).Checked += new RoutedEventHandler(MuteInsult_ValueChanged);
		((ToggleButton)RefMuteInsultSpeechCheck).Unchecked += new RoutedEventHandler(MuteInsult_ValueChanged);
		RefMuteInsultSpeechCheckText = (TextBlock)((FrameworkElement)this).FindName("MuteInsultSpeechCheckText");
		RefMuteInsultsCheck = (CheckBox)((FrameworkElement)this).FindName("MuteInsultsCheck");
		((ToggleButton)RefMuteInsultsCheck).Checked += new RoutedEventHandler(MuteInsult_ValueChanged);
		((ToggleButton)RefMuteInsultsCheck).Unchecked += new RoutedEventHandler(MuteInsult_ValueChanged);
		RefMuteInsultsCheckText = (TextBlock)((FrameworkElement)this).FindName("MuteInsultsCheckText");
		RefMuteBackgroundCheck = (CheckBox)((FrameworkElement)this).FindName("MuteBackground");
		((ToggleButton)RefMuteBackgroundCheck).Checked += new RoutedEventHandler(MuteBackground_ValueChanged);
		((ToggleButton)RefMuteBackgroundCheck).Unchecked += new RoutedEventHandler(MuteBackground_ValueChanged);
		RefMuteBackgroundCheckText = (TextBlock)((FrameworkElement)this).FindName("MuteBackgroundText");
		RefReduceSoundsCheck = (CheckBox)((FrameworkElement)this).FindName("ReduceSoundsCheck");
		((ToggleButton)RefReduceSoundsCheck).Checked += new RoutedEventHandler(ReduceSounds_ValueChanged);
		((ToggleButton)RefReduceSoundsCheck).Unchecked += new RoutedEventHandler(ReduceSounds_ValueChanged);
		RefReduceSoundsCheckText = (TextBlock)((FrameworkElement)this).FindName("ReduceSoundsCheckText");
		RefSFXDefaultsButton = (Button)((FrameworkElement)this).FindName("SFXDefaultsButton");
		RefCheatKeysCheck = (CheckBox)((FrameworkElement)this).FindName("CheatKeysCheck");
		((ToggleButton)RefCheatKeysCheck).Checked += new RoutedEventHandler(CheatKeys_ValueChanged);
		((ToggleButton)RefCheatKeysCheck).Unchecked += new RoutedEventHandler(CheatKeys_ValueChanged);
		RefPingsCheck = (CheckBox)((FrameworkElement)this).FindName("PingsCheck");
		((ToggleButton)RefPingsCheck).Checked += new RoutedEventHandler(Pings_ValueChanged);
		((ToggleButton)RefPingsCheck).Unchecked += new RoutedEventHandler(Pings_ValueChanged);
		RefPingsCheckText = (TextBlock)((FrameworkElement)this).FindName("PingsCheckText");
		RefExtraZoomCheck = (CheckBox)((FrameworkElement)this).FindName("ExtraZoomCheck");
		((ToggleButton)RefExtraZoomCheck).Checked += new RoutedEventHandler(ExtraZoom_ValueChanged);
		((ToggleButton)RefExtraZoomCheck).Unchecked += new RoutedEventHandler(ExtraZoom_ValueChanged);
		RefExtraZoomCheckText = (TextBlock)((FrameworkElement)this).FindName("ExtraZoomCheckText");
		RefShowMoatCheck = (CheckBox)((FrameworkElement)this).FindName("ShowMoatCheck");
		((ToggleButton)RefShowMoatCheck).Checked += new RoutedEventHandler(ShowMoat_ValueChanged);
		((ToggleButton)RefShowMoatCheck).Unchecked += new RoutedEventHandler(ShowMoat_ValueChanged);
		RefShowMoatText = (TextBlock)((FrameworkElement)this).FindName("ShowMoatText");
		RefConfirmDisbandCheck = (CheckBox)((FrameworkElement)this).FindName("ConfirmDisbandCheck");
		((ToggleButton)RefConfirmDisbandCheck).Checked += new RoutedEventHandler(ConfirmDisband_ValueChanged);
		((ToggleButton)RefConfirmDisbandCheck).Unchecked += new RoutedEventHandler(ConfirmDisband_ValueChanged);
		RefConfirmDisbandText = (TextBlock)((FrameworkElement)this).FindName("ConfirmDisbandText");
		RefPlayerColourShield1 = (RadioButton)((FrameworkElement)this).FindName("PlayerColourShield1");
		RefPlayerColourShield2 = (RadioButton)((FrameworkElement)this).FindName("PlayerColourShield2");
		RefPlayerColourShield3 = (RadioButton)((FrameworkElement)this).FindName("PlayerColourShield3");
		RefPlayerColourShield4 = (RadioButton)((FrameworkElement)this).FindName("PlayerColourShield4");
		RefPlayerColourShield5 = (RadioButton)((FrameworkElement)this).FindName("PlayerColourShield5");
		RefPlayerColourShield6 = (RadioButton)((FrameworkElement)this).FindName("PlayerColourShield6");
		RefPlayerColourShield7 = (RadioButton)((FrameworkElement)this).FindName("PlayerColourShield7");
		RefPlayerColourShield8 = (RadioButton)((FrameworkElement)this).FindName("PlayerColourShield8");
		RefLeaderboard_OptOut = (CheckBox)((FrameworkElement)this).FindName("Leaderboard_OptOut");
		((ToggleButton)RefLeaderboard_OptOut).Checked += new RoutedEventHandler(Leaderboard_OptOut_ValueChanged);
		((ToggleButton)RefLeaderboard_OptOut).Unchecked += new RoutedEventHandler(Leaderboard_OptOut_ValueChanged);
		RefLeaderboard_OptOutText = (TextBlock)((FrameworkElement)this).FindName("Leaderboard_OptOutText");
		RefLeaderboard_Names = (CheckBox)((FrameworkElement)this).FindName("Leaderboard_Names");
		((ToggleButton)RefLeaderboard_Names).Checked += new RoutedEventHandler(Leaderboard_Names_ValueChanged);
		((ToggleButton)RefLeaderboard_Names).Unchecked += new RoutedEventHandler(Leaderboard_Names_ValueChanged);
		RefLeaderboard_NamesText = (TextBlock)((FrameworkElement)this).FindName("Leaderboard_NamesText");
		RefLeaderboard_Images = (CheckBox)((FrameworkElement)this).FindName("Leaderboard_Images");
		((ToggleButton)RefLeaderboard_Images).Checked += new RoutedEventHandler(Leaderboard_Images_ValueChanged);
		((ToggleButton)RefLeaderboard_Images).Unchecked += new RoutedEventHandler(Leaderboard_Images_ValueChanged);
		RefLeaderboard_ImagesText = (TextBlock)((FrameworkElement)this).FindName("Leaderboard_ImagesText");
		RefSandsTimeDisable = (CheckBox)((FrameworkElement)this).FindName("SandsTimeDisable");
		((ToggleButton)RefSandsTimeDisable).Checked += new RoutedEventHandler(SandsTimeDisable_ValueChanged);
		((ToggleButton)RefSandsTimeDisable).Unchecked += new RoutedEventHandler(SandsTimeDisable_ValueChanged);
		RefChatMuteDisable = (CheckBox)((FrameworkElement)this).FindName("ChatMuteDisable");
		((ToggleButton)RefChatMuteDisable).Checked += new RoutedEventHandler(MuteMPChat_ValueChanged);
		((ToggleButton)RefChatMuteDisable).Unchecked += new RoutedEventHandler(MuteMPChat_ValueChanged);
		RefArabicL2RCheck = (CheckBox)((FrameworkElement)this).FindName("ArabicL2RCheck");
		((ToggleButton)RefArabicL2RCheck).Checked += new RoutedEventHandler(ArabicL2R_ValueChanged);
		((ToggleButton)RefArabicL2RCheck).Unchecked += new RoutedEventHandler(ArabicL2R_ValueChanged);
		RefPlayerSettingsHeading = (TextBlock)((FrameworkElement)this).FindName("PlayerSettingsHeading");
		RefOptionsChickenButton = (Button)((FrameworkElement)this).FindName("OptionsChickenButton");
		RefNewsletterSignupButton = (Button)((FrameworkElement)this).FindName("NewsletterSignupButton");
		RefNewsletterCheck = (CheckBox)((FrameworkElement)this).FindName("NewsletterCheck");
		((ToggleButton)RefNewsletterCheck).Checked += new RoutedEventHandler(NewsletterCheck_ValueChanged);
		((ToggleButton)RefNewsletterCheck).Unchecked += new RoutedEventHandler(NewsletterCheck_ValueChanged);
		RefScribeLock = (Image)((FrameworkElement)this).FindName("ScribeLock");
		Resolution[] resolutions = Screen.resolutions;
		for (int i = 0; i < resolutions.Length; i++)
		{
			Resolution val = resolutions[i];
			if (((Resolution)(ref val)).width >= 1280 && ((Resolution)(ref val)).height >= 768)
			{
				ComboBoxItem val2 = new ComboBoxItem();
				if (!FatControler.arabic)
				{
					((ContentControl)val2).Content = ((Resolution)(ref val)).width + "x" + ((Resolution)(ref val)).height + " (" + ((Resolution)(ref val)).refreshRate + "hz)";
				}
				else
				{
					((ContentControl)val2).Content = ((Resolution)(ref val)).width + "x" + ((Resolution)(ref val)).height + " " + ((Resolution)(ref val)).refreshRate + "hz";
				}
				((FrameworkElement)val2).Tag = val;
				((FrameworkElement)val2).Height = 25f;
				((Control)val2).Padding = new Thickness(12f, 0f, 12f, 0f);
				((FrameworkElement)val2).VerticalAlignment = (VerticalAlignment)1;
				((ItemsControl)RefResolutionCombo).Items.Add((object)val2);
			}
		}
		UpdateResListbox();
		((Selector)RefResolutionCombo).SelectionChanged += new SelectionChangedEventHandler(RefResolutionCombo_SelectionChanged);
		ComboBoxItem val3 = new ComboBoxItem();
		((ContentControl)val3).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 88);
		((FrameworkElement)val3).Tag = 0;
		((FrameworkElement)val3).Height = 25f;
		((Control)val3).Padding = new Thickness(12f, 0f, 12f, 0f);
		((FrameworkElement)val3).VerticalAlignment = (VerticalAlignment)1;
		((ItemsControl)RefScreenModeCombo).Items.Add((object)val3);
		ComboBoxItem val4 = new ComboBoxItem();
		((ContentControl)val4).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 100);
		((FrameworkElement)val4).Tag = 1;
		((FrameworkElement)val4).Height = 25f;
		((Control)val4).Padding = new Thickness(12f, 0f, 12f, 0f);
		((FrameworkElement)val4).VerticalAlignment = (VerticalAlignment)1;
		((ItemsControl)RefScreenModeCombo).Items.Add((object)val4);
		ComboBoxItem val5 = new ComboBoxItem();
		((ContentControl)val5).Content = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 101);
		((FrameworkElement)val5).Tag = 2;
		((FrameworkElement)val5).Height = 25f;
		((Control)val5).Padding = new Thickness(12f, 0f, 12f, 0f);
		((FrameworkElement)val5).VerticalAlignment = (VerticalAlignment)1;
		((ItemsControl)RefScreenModeCombo).Items.Add((object)val5);
		if ((int)Screen.fullScreenMode == 1)
		{
			((Selector)RefScreenModeCombo).SelectedIndex = 1;
		}
		else if ((int)Screen.fullScreenMode == 0)
		{
			((Selector)RefScreenModeCombo).SelectedIndex = 0;
		}
		else
		{
			((Selector)RefScreenModeCombo).SelectedIndex = 2;
		}
		((Selector)RefScreenModeCombo).SelectionChanged += new SelectionChangedEventHandler(RefScreenModeCombo_SelectionChanged);
		if (FatControler.polish || FatControler.ukrainian || FatControler.french || FatControler.spanish || FatControler.russian || FatControler.thai)
		{
			((FrameworkElement)RefBuildingTooltipsCheck).Height = 43f;
			RefBuildingTooltipsCheckText.LineHeight = 20f;
			RefBuildingTooltipsCheckText.LineStackingStrategy = (LineStackingStrategy)0;
		}
		if (FatControler.japanese || FatControler.ukrainian)
		{
			((FrameworkElement)RefRadarZoomCheck).Height = 43f;
			RefRadarZoomCheckText.LineHeight = 20f;
			RefRadarZoomCheckText.LineStackingStrategy = (LineStackingStrategy)0;
		}
		if (FatControler.arabic || FatControler.ukrainian)
		{
			((FrameworkElement)RefSteamHelpCheck).Height = 43f;
			RefSteamHelpCheckText.LineHeight = 20f;
			RefSteamHelpCheckText.LineStackingStrategy = (LineStackingStrategy)0;
		}
		if (FatControler.greek)
		{
			((FrameworkElement)RefCustomIntros).Height = 43f;
			((FrameworkElement)RefCustomIntros).Margin = new Thickness(0f, 100f, 0f, 0f);
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
			((FrameworkElement)RefPlayerSettingsHeading).Margin = new Thickness(51f, 10f, 50f, 0f);
		}
		if (FatControler.polish)
		{
			((FrameworkElement)RefReduceSoundsCheck).Height = 43f;
			RefReduceSoundsCheckText.LineHeight = 20f;
			RefReduceSoundsCheckText.LineStackingStrategy = (LineStackingStrategy)0;
		}
		if (FatControler.german || FatControler.japanese || FatControler.turkish)
		{
			((FrameworkElement)RefMuteInsultsCheck).Height = 43f;
			RefMuteInsultsCheckText.LineHeight = 20f;
			RefMuteInsultsCheckText.LineStackingStrategy = (LineStackingStrategy)0;
		}
		if (FatControler.german)
		{
			((FrameworkElement)RefMuteInsultSpeechCheck).Height = 43f;
			RefMuteInsultSpeechCheckText.LineHeight = 20f;
			RefMuteInsultSpeechCheckText.LineStackingStrategy = (LineStackingStrategy)0;
		}
		if (FatControler.polish)
		{
			((FrameworkElement)RefLeaderboard_OptOut).Height = 43f;
			RefLeaderboard_OptOutText.LineHeight = 20f;
			RefLeaderboard_OptOutText.LineStackingStrategy = (LineStackingStrategy)0;
			((FrameworkElement)RefLeaderboard_Names).Height = 43f;
			RefLeaderboard_NamesText.LineHeight = 20f;
			RefLeaderboard_NamesText.LineStackingStrategy = (LineStackingStrategy)0;
		}
		if (FatControler.arabic || FatControler.french || FatControler.spanish || FatControler.italian || FatControler.polish || FatControler.japanese || FatControler.dutch || FatControler.ukrainian)
		{
			((FrameworkElement)RefLeaderboard_Images).Height = 43f;
			RefLeaderboard_ImagesText.LineHeight = 20f;
			RefLeaderboard_ImagesText.LineStackingStrategy = (LineStackingStrategy)0;
		}
		if (FatControler.russian || FatControler.arabic)
		{
			((FrameworkElement)RefMuteBackgroundCheck).Height = 43f;
			RefMuteBackgroundCheckText.LineHeight = 20f;
			RefMuteBackgroundCheckText.LineStackingStrategy = (LineStackingStrategy)0;
		}
		if (FatControler.arabic)
		{
			((UIElement)RefArabicL2RCheck).Visibility = (Visibility)2;
		}
		if (FatControler.german || FatControler.portuguese || FatControler.russian || FatControler.ukrainian || FatControler.czech || FatControler.french || FatControler.hungarian || FatControler.italian || FatControler.japanese || FatControler.polish || FatControler.spanish || FatControler.thai || FatControler.turkish || FatControler.greek || FatControler.arabic)
		{
			((FrameworkElement)RefPingsCheck).Height = 43f;
			RefPingsCheckText.LineHeight = 20f;
			RefPingsCheckText.LineStackingStrategy = (LineStackingStrategy)0;
		}
		if (FatControler.polish)
		{
			((FrameworkElement)RefExtraZoomCheck).Height = 43f;
			RefExtraZoomCheckText.LineHeight = 20f;
			RefExtraZoomCheckText.LineStackingStrategy = (LineStackingStrategy)0;
		}
		if (FatControler.UsesEnglishSpeechFolder())
		{
			((UIElement)RefEnglishSpeechCheck).Visibility = (Visibility)0;
		}
		RefHotKeyList = (ListView)((FrameworkElement)this).FindName("HotKeyList");
		((Selector)RefHotKeyList).SelectionChanged += (SelectionChangedEventHandler)delegate
		{
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			if (((Selector)RefHotKeyList).SelectedItem != null)
			{
				MainViewModel.Instance.OptionsHotKeyTitle = ((HotKeyRow)((Selector)RefHotKeyList).SelectedItem).Text1;
				selectedFunction = (Enums.KeyFunctions)((HotKeyRow)((Selector)RefHotKeyList).SelectedItem).iDataValue;
				string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 93);
				KeyCode keyCode = KeyManager.instance.GetKeyCode(selectedFunction, 0);
				KeyCode keyCode2 = KeyManager.instance.GetKeyCode(selectedFunction, 1);
				if ((int)keyCode == 0)
				{
					MainViewModel.Instance.OptionsHotKey1 = text;
				}
				else
				{
					MainViewModel.Instance.OptionsHotKey1 = GetKeyCodeString(keyCode);
				}
				if ((int)keyCode2 == 0)
				{
					MainViewModel.Instance.OptionsHotKey2 = text;
				}
				else
				{
					MainViewModel.Instance.OptionsHotKey2 = GetKeyCodeString(keyCode2);
				}
				((UIElement)RefOptionsHotKeyPanel).Visibility = (Visibility)2;
				MainViewModel.Instance.OptionsHotKeySelectVis = (Visibility)2;
				MainViewModel.Instance.OptionsHotKeyChangeVis = (Visibility)1;
			}
		};
		RefOptionsHotKeyNewKeyApply = (Button)((FrameworkElement)this).FindName("OptionsHotKeyNewKeyApply");
		RefCursorSystemButton = (Button)((FrameworkElement)this).FindName("CursorSystemButton");
		RefCursorSwordButton = (Button)((FrameworkElement)this).FindName("CursorSwordButton");
		RefCursorSwordXButton = (Button)((FrameworkElement)this).FindName("CursorSwordXButton");
		RefCursorSwordX2Button = (Button)((FrameworkElement)this).FindName("CursorSwordX2Button");
		RefScribeClassicButton = (Button)((FrameworkElement)this).FindName("ScribeClassicButton");
		RefScribeModernButton = (Button)((FrameworkElement)this).FindName("ScribeModernButton");
		RefCrusaderLordButton = (Button)((FrameworkElement)this).FindName("CrusaderLordButton");
		RefArabicLordButton = (Button)((FrameworkElement)this).FindName("ArabicLordButton");
		RefBedouinLordButton = (Button)((FrameworkElement)this).FindName("BedouinLordButton");
		RefScribeLordButton = (Button)((FrameworkElement)this).FindName("ScribeLordButton");
		RefFemaleLordButton = (Button)((FrameworkElement)this).FindName("FemaleLordButton");
		RefBessyLordButton = (Button)((FrameworkElement)this).FindName("BessyLordButton");
		RefArabicLordFemaleButton = (Button)((FrameworkElement)this).FindName("ArabicLordFemaleButton");
		RefBedouinLordFemaleButton = (Button)((FrameworkElement)this).FindName("BedouinLordFemaleButton");
		RefOptionsKeys1 = (Button)((FrameworkElement)this).FindName("OptionsKeys1");
		RefOptionsKeys2 = (Button)((FrameworkElement)this).FindName("OptionsKeys2");
		if (FatControler.hungarian)
		{
			((FrameworkElement)RefOptionsKeys1).Width = 380f;
			((FrameworkElement)RefOptionsKeys1).Margin = new Thickness(10f, 0f, 0f, 70f);
			((FrameworkElement)RefOptionsKeys2).Width = 380f;
			((FrameworkElement)RefOptionsKeys2).Margin = new Thickness(10f, 0f, 0f, 30f);
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
		if (((UIElement)instance1).IsVisible)
		{
			MainViewModel.Instance.HUDOptions = instance1;
		}
		else if (((UIElement)instance2).IsVisible)
		{
			MainViewModel.Instance.HUDOptions = instance2;
		}
		else if (((UIElement)instance3).IsVisible)
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

	public void Init()
	{
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_023e: Invalid comparison between Unknown and I4
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
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
		((RangeBase)RefMasterVolumeSlider).Value = (int)(ConfigSettings.Settings_MasterVolume * 100f);
		((RangeBase)RefMusicVolumeSlider).Value = (int)(ConfigSettings.Settings_MusicVolume * 100f);
		((RangeBase)RefSpeechVolumeSlider).Value = (int)(ConfigSettings.Settings_SpeechVolume * 100f);
		((RangeBase)RefUnitSpeechVolumeSlider).Value = (int)(ConfigSettings.Settings_UnitSpeechVolume * 100f);
		((RangeBase)RefSFXVolumeSlider).Value = (int)(ConfigSettings.Settings_SFXVolume * 100f);
		MainViewModel.Instance.MasterVolumeValue = ((RangeBase)RefMasterVolumeSlider).Value.ToString();
		MainViewModel.Instance.MusicVolumeValue = ((RangeBase)RefMusicVolumeSlider).Value.ToString();
		MainViewModel.Instance.SpeechVolumeValue = ((RangeBase)RefSpeechVolumeSlider).Value.ToString();
		MainViewModel.Instance.UnitSpeechVolumeValue = ((RangeBase)RefUnitSpeechVolumeSlider).Value.ToString();
		MainViewModel.Instance.SfxVolumeValue = ((RangeBase)RefSFXVolumeSlider).Value.ToString();
		((RangeBase)RefScrollSpeedSlider).Value = ConfigSettings.Settings_ScrollSpeed;
		((RangeBase)RefGameSpeedSlider).Value = ConfigSettings.Settings_GameSpeed / 5;
		RefTextBoxChangeName.Text = ConfigSettings.Settings_UserName;
		((RangeBase)RefUIScaleSlider).Value = (int)(ConfigSettings.Settings_UIScale * 100f);
		if (MainViewModel.Instance.Show_HUD_OptionsMP)
		{
			((UIElement)RefOptionsChickenButton).Visibility = (Visibility)1;
			MainViewModel.Instance.OptionsMPLordVis = (Visibility)0;
		}
		else
		{
			((UIElement)RefOptionsChickenButton).Visibility = (Visibility)2;
			if (Director.instance.SimRunning)
			{
				MainViewModel.Instance.OptionsMPLordVis = (Visibility)0;
			}
			else
			{
				MainViewModel.Instance.OptionsMPLordVis = (Visibility)2;
			}
		}
		if (ConfigSettings.Settings_Vsync)
		{
			((ToggleButton)RefVSyncCheck).IsChecked = true;
		}
		else
		{
			((ToggleButton)RefVSyncCheck).IsChecked = false;
		}
		if ((int)Screen.fullScreenMode == 1)
		{
			((Selector)RefScreenModeCombo).SelectedIndex = 1;
		}
		else if ((int)Screen.fullScreenMode == 0)
		{
			((Selector)RefScreenModeCombo).SelectedIndex = 0;
		}
		else
		{
			((Selector)RefScreenModeCombo).SelectedIndex = 2;
		}
		((ToggleButton)RefLockCursorCheck).IsChecked = ConfigSettings.Settings_LockCursor;
		((ToggleButton)RefBuildingTooltipsCheck).IsChecked = ConfigSettings.Settings_ShowBuildingTooltips;
		((ToggleButton)RefSteamHelpCheck).IsChecked = ConfigSettings.Settings_UseSteamOverlayForHelp;
		((ToggleButton)RefCompassCheck).IsChecked = ConfigSettings.Settings_Compass;
		((ToggleButton)RefLocalTimeCheck).IsChecked = ConfigSettings.Settings_ShowLocalTime;
		((ToggleButton)RefGameTimeCheck).IsChecked = ConfigSettings.Settings_ShowGameTime;
		((ToggleButton)RefSandsTimerCheck).IsChecked = ConfigSettings.Settings_ShowSandsTimer;
		((ToggleButton)RefRadarZoomCheck).IsChecked = ConfigSettings.Settings_RadarDefaultZoomedOut;
		((ToggleButton)RefCustomIntros).IsChecked = ConfigSettings.Settings_CustomIntros;
		((ToggleButton)RefUISoundsCheck).IsChecked = ConfigSettings.Settings_PlayUISFX;
		((ToggleButton)RefGenieSpeechCheck).IsChecked = ConfigSettings.Settings_GenieSpeech;
		((ToggleButton)RefReduceSoundsCheck).IsChecked = ConfigSettings.Settings_ReduceMusicVolumeForSpeech;
		((ToggleButton)RefCheatKeysCheck).IsChecked = ConfigSettings.Settings_CheatKeysEnabled;
		((ToggleButton)RefPingsCheck).IsChecked = ConfigSettings.Settings_ShowPings;
		((ToggleButton)RefExtraZoomCheck).IsChecked = ConfigSettings.Settings_ExtraZoom;
		((ToggleButton)RefShowMoatCheck).IsChecked = ConfigSettings.Settings_ShowPlannedMoat;
		((ToggleButton)RefConfirmDisbandCheck).IsChecked = ConfigSettings.Settings_Confirm_Disband_Troops;
		((ToggleButton)RefEnglishSpeechCheck).IsChecked = ConfigSettings.Settings_EnglishSpeech;
		((ToggleButton)RefMuteBackgroundCheck).IsChecked = !ConfigSettings.Settings_BackgroundAudio;
		((ToggleButton)RefLeaderboard_OptOut).IsChecked = ConfigSettings.Settings_Leaderboard_OptOut;
		((ToggleButton)RefLeaderboard_Names).IsChecked = ConfigSettings.Settings_Leaderboard_Names;
		((ToggleButton)RefLeaderboard_Images).IsChecked = ConfigSettings.Settings_Leaderboard_Images;
		((ToggleButton)RefSandsTimeDisable).IsChecked = ConfigSettings.Settings_HideSoTTiming;
		((ToggleButton)RefChatMuteDisable).IsChecked = ConfigSettings.Settings_MuteMPChat;
		((ToggleButton)RefArabicL2RCheck).IsChecked = ConfigSettings.Settings_ArabicL2R;
		panelActive = false;
		((ToggleButton)RefMuteInsultsCheck).IsChecked = ConfigSettings.Settings_MuteInsults;
		((ToggleButton)RefMuteInsultSpeechCheck).IsChecked = ConfigSettings.Settings_MuteInsultSpeech;
		panelActive = true;
		if (Director.instance.MultiplayerGame || MainViewModel.Instance.Show_HUD_OptionsMP)
		{
			((UIElement)RefCOA_Button).IsEnabled = false;
		}
		else
		{
			((UIElement)RefCOA_Button).IsEnabled = from != 0;
		}
		MainViewModel.Instance.OptionsScaleApplyVisible = (Visibility)1;
		if (Director.instance.MultiplayerGame || MainViewModel.Instance.Show_HUD_OptionsMP)
		{
			MainViewModel.Instance.OptionsGameSpeedVis = (Visibility)0;
		}
		else
		{
			MainViewModel.Instance.OptionsGameSpeedVis = (Visibility)2;
		}
		if (ConfigSettings.AchievementsDisabled)
		{
			MainViewModel.Instance.OptionsAchievementsDisabledVis = (Visibility)2;
		}
		else
		{
			MainViewModel.Instance.OptionsAchievementsDisabledVis = (Visibility)1;
		}
		if ((Director.instance.SimRunning || MainViewModel.Instance.Show_HUD_OptionsMP) && GameData.Instance.game_type != 3)
		{
			MainViewModel.Instance.OptionsInGameCheatsVis = (Visibility)2;
		}
		else
		{
			MainViewModel.Instance.OptionsInGameCheatsVis = (Visibility)1;
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
			MainViewModel.Instance.OptionsPlayerColourVis = (Visibility)1;
		}
		else
		{
			MainViewModel.Instance.OptionsPlayerColourVis = (Visibility)2;
		}
		switch (ConfigSettings.Settings_PlayerColour)
		{
		case 0:
			((ToggleButton)RefPlayerColourShield1).IsChecked = true;
			break;
		case 1:
			((ToggleButton)RefPlayerColourShield2).IsChecked = true;
			break;
		case 2:
			((ToggleButton)RefPlayerColourShield3).IsChecked = true;
			break;
		case 3:
			((ToggleButton)RefPlayerColourShield4).IsChecked = true;
			break;
		case 4:
			((ToggleButton)RefPlayerColourShield5).IsChecked = true;
			break;
		case 5:
			((ToggleButton)RefPlayerColourShield6).IsChecked = true;
			break;
		case 6:
			((ToggleButton)RefPlayerColourShield7).IsChecked = true;
			break;
		case 7:
			((ToggleButton)RefPlayerColourShield8).IsChecked = true;
			break;
		}
		UpdateUIScaleSliderVis();
		MainViewModel.Instance.OptionsApplyVisible = (Visibility)1;
		resChanged = false;
		screenModeChanged = false;
		MainViewModel.Instance.MP_SteamIdentity_Name = Platform_Multiplayer.Instance.GetLocalSteamName();
		CreateHotkeyList();
		MainViewModel.Instance.OptionsNewsletterVis = (Visibility)0;
		if (FrontendMenus.newsletterSignUp)
		{
			((UIElement)RefScribeLock).Visibility = (Visibility)1;
		}
		else
		{
			((UIElement)RefScribeLock).Visibility = (Visibility)2;
		}
		((UIElement)RefNewsletterSignupButton).IsEnabled = false;
		RefTextBoxNewsletter.Text = "";
	}

	public void RefreshGameSpeed()
	{
		((RangeBase)RefGameSpeedSlider).Value = ConfigSettings.Settings_GameSpeed / 5;
	}

	public void Update()
	{
		UpdateUIScaleSliderVis();
		if (FrontendMenus.newsletterSignUp)
		{
			((UIElement)RefScribeLock).Visibility = (Visibility)1;
		}
		else
		{
			((UIElement)RefScribeLock).Visibility = (Visibility)2;
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
				((UIElement)RefOptionsHotKeyNewKeyApply).IsEnabled = true;
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
		//IL_043a: Unknown result type (might be due to invalid IL or missing references)
		//IL_043f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0454: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cc8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ccd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cdb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ce0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ce2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cf9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c05: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c0a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c18: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c1d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c1f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0291: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d05: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c36: Unknown result type (might be due to invalid IL or missing references)
		//IL_030e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0319: Unknown result type (might be due to invalid IL or missing references)
		//IL_0324: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d1c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c42: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c59: Unknown result type (might be due to invalid IL or missing references)
		//IL_0347: Unknown result type (might be due to invalid IL or missing references)
		//IL_0351: Unknown result type (might be due to invalid IL or missing references)
		//IL_0356: Unknown result type (might be due to invalid IL or missing references)
		//IL_038a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0333: Unknown result type (might be due to invalid IL or missing references)
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
			if (((ToggleButton)RefVSyncCheck).IsChecked.Value)
			{
				if (((Selector)RefResolutionCombo).SelectedItem != null)
				{
					Resolution val = (Resolution)((FrameworkElement)(ComboBoxItem)((Selector)RefResolutionCombo).SelectedItem).Tag;
					targetFrameRate = ((Resolution)(ref val)).refreshRate;
				}
				else
				{
					Resolution currentResolution = Screen.currentResolution;
					targetFrameRate = ((Resolution)(ref currentResolution)).refreshRate;
				}
			}
			Application.targetFrameRate = targetFrameRate;
			if (screenModeChanged && !resChanged && ((Selector)RefScreenModeCombo).SelectedIndex == 2)
			{
				Screen.fullScreenMode = (FullScreenMode)3;
			}
			else
			{
				FullScreenMode val2 = (FullScreenMode)3;
				switch (((Selector)RefScreenModeCombo).SelectedIndex)
				{
				case 0:
					val2 = (FullScreenMode)0;
					ConfigSettings.Settings_LastFullscreenType = 0;
					break;
				case 1:
					val2 = (FullScreenMode)1;
					ConfigSettings.Settings_LastFullscreenType = 1;
					break;
				case 2:
					val2 = (FullScreenMode)3;
					break;
				}
				if (((Selector)RefResolutionCombo).SelectedItem == null)
				{
					Screen.fullScreenMode = val2;
				}
				else
				{
					Resolution val3 = (Resolution)((FrameworkElement)(ComboBoxItem)((Selector)RefResolutionCombo).SelectedItem).Tag;
					ConfigSettings.Settings_LastFullscreenWidth = ((Resolution)(ref val3)).width;
					ConfigSettings.Settings_LastFullscreenHeight = ((Resolution)(ref val3)).height;
					ConfigSettings.Settings_LastFullscreenRefresh = ((Resolution)(ref val3)).refreshRate;
					Screen.SetResolution(((Resolution)(ref val3)).width, ((Resolution)(ref val3)).height, val2, ((Resolution)(ref val3)).refreshRate);
				}
			}
			SetVSync(((ToggleButton)RefVSyncCheck).IsChecked.Value);
			UpdateResListbox(fromSettings: true);
			Save();
			MainViewModel.Instance.OptionsApplyVisible = (Visibility)1;
			screenModeChanged = false;
			resChanged = false;
			UpdateUIScaleSliderVis();
			break;
		}
		case -3:
		{
			float scaleFactor = (ConfigSettings.Settings_UIScale = (float)(int)((RangeBase)RefUIScaleSlider).Value / 100f);
			if (MainViewModel.Instance.Show_InGame)
			{
				MainViewModel.Instance.ScaleIngameUI(scaleFactor);
			}
			else
			{
				MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
			}
			MainViewModel.Instance.OptionsScaleApplyVisible = (Visibility)1;
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
				SteamFriends.ActivateGameOverlayToWebPage("https://store.steampowered.com/news/app/3024040", (EActivateGameOverlayToWebPageMode)0);
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
				MainViewModel.Instance.OptionsNewsletterVis = (Visibility)2;
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
			((RangeBase)RefMasterVolumeSlider).Value = (int)(ConfigSettings.Settings_MasterVolume * 100f);
			((RangeBase)RefMusicVolumeSlider).Value = (int)(ConfigSettings.Settings_MusicVolume * 100f);
			((RangeBase)RefSpeechVolumeSlider).Value = (int)(ConfigSettings.Settings_SpeechVolume * 100f);
			((RangeBase)RefUnitSpeechVolumeSlider).Value = (int)(ConfigSettings.Settings_UnitSpeechVolume * 100f);
			((RangeBase)RefSFXVolumeSlider).Value = (int)(ConfigSettings.Settings_SFXVolume * 100f);
			Save();
			break;
		case -40:
			((RangeBase)RefScrollSpeedSlider).Value = (ConfigSettings.Settings_ScrollSpeed = 5);
			Save();
			break;
		case -41:
			((RangeBase)RefGameSpeedSlider).Value = 8f;
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
			MainViewModel.Instance.OptionsAchievementsDisabledVis = (Visibility)2;
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
				ConfigSettings.Settings_CoopCheatsEnabled = false;
				ConfigSettings.SaveSettings();
				ConfigSettings.TempMissionUnlock = false;
				ConfigSettings.AchievementsDisabled = false;
				ConfigSettings.WipeCampaignScores();
				MainViewModel.Instance.OptionsAchievementsDisabledVis = (Visibility)1;
			}, delegate
			{
			}, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 42));
			break;
		case 104:
			EngineInterface.GameAction(Enums.GameActionCommand.SH1Cheats, 3, 0);
			ConfigSettings.AchievementsDisabled = true;
			MainViewModel.Instance.OptionsAchievementsDisabledVis = (Visibility)2;
			break;
		case 105:
			EngineInterface.GameAction(Enums.GameActionCommand.SH1Cheats, 2, 0);
			ConfigSettings.AchievementsDisabled = true;
			MainViewModel.Instance.OptionsAchievementsDisabledVis = (Visibility)2;
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
					MainViewModel.Instance.OptionsAchievementsDisabledVis = (Visibility)2;
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
			((UIElement)RefOptionsHotKeyPanel).Visibility = (Visibility)1;
			CreateHotkeyList();
			((Selector)RefHotKeyList).SelectedItem = null;
			break;
		case -102:
			MainViewModel.Instance.OptionsHotKeyCurrentKey = MainViewModel.Instance.OptionsHotKey1;
			MainViewModel.Instance.OptionsHotKeyNewKey = "";
			((UIElement)RefOptionsHotKeyNewKeyApply).IsEnabled = false;
			KeyManager.instance.HotKeySelectorMode = true;
			selectedColumn = 0;
			MainViewModel.Instance.OptionsHotKeySelectVis = (Visibility)1;
			MainViewModel.Instance.OptionsHotKeyChangeVis = (Visibility)2;
			break;
		case -103:
			MainViewModel.Instance.OptionsHotKeyCurrentKey = MainViewModel.Instance.OptionsHotKey2;
			MainViewModel.Instance.OptionsHotKeyNewKey = "";
			((UIElement)RefOptionsHotKeyNewKeyApply).IsEnabled = false;
			KeyManager.instance.HotKeySelectorMode = true;
			selectedColumn = 1;
			MainViewModel.Instance.OptionsHotKeySelectVis = (Visibility)1;
			MainViewModel.Instance.OptionsHotKeyChangeVis = (Visibility)2;
			break;
		case -104:
			KeyManager.instance.HotKeySelectorMode = false;
			MainViewModel.Instance.OptionsHotKeySelectVis = (Visibility)2;
			MainViewModel.Instance.OptionsHotKeyChangeVis = (Visibility)1;
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
			if ((int)keyCode3 == 0)
			{
				MainViewModel.Instance.OptionsHotKey1 = text2;
			}
			else
			{
				MainViewModel.Instance.OptionsHotKey1 = GetKeyCodeString(keyCode3);
			}
			if ((int)keyCode4 == 0)
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
			MainViewModel.Instance.OptionsHotKeySelectVis = (Visibility)2;
			MainViewModel.Instance.OptionsHotKeyChangeVis = (Visibility)1;
			break;
		}
		case -106:
		{
			KeyManager.instance.SetNewKey(selectedFunction, -1, selectedColumn);
			string text = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 93);
			KeyCode keyCode = KeyManager.instance.GetKeyCode(selectedFunction, 0);
			KeyCode keyCode2 = KeyManager.instance.GetKeyCode(selectedFunction, 1);
			if ((int)keyCode == 0)
			{
				MainViewModel.Instance.OptionsHotKey1 = text;
			}
			else
			{
				MainViewModel.Instance.OptionsHotKey1 = GetKeyCodeString(keyCode);
			}
			if ((int)keyCode2 == 0)
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
			MainViewModel.Instance.OptionsHotKeySelectVis = (Visibility)2;
			MainViewModel.Instance.OptionsHotKeyChangeVis = (Visibility)1;
			break;
		}
		case -20000:
			Director.instance.SignupNewsletter(RefTextBoxNewsletter.Text, delegate
			{
				FrontendMenus.newsletterSignUp = true;
				((UIElement)RefScribeLock).Visibility = (Visibility)1;
				ButtonClicked(-198);
			});
			MainViewModel.Instance.OptionsNewsletterVis = (Visibility)1;
			break;
		case -20001:
			MainViewModel.Instance.OptionsNewsletterVis = (Visibility)1;
			break;
		}
	}

	public void Save()
	{
		ConfigSettings.Settings_UserName = RefTextBoxChangeName.Text;
		ConfigSettings.SaveSettings();
		lastDynamicChanged = DateTime.MaxValue;
	}

	public void UpdateMenus()
	{
		for (int i = 0; i < 7; i++)
		{
			if (menuSection == i)
			{
				MainViewModel.Instance.OptionsSectionsBorders[i] = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.OptionsSectionsBorders[i] = (Visibility)1;
			}
		}
		((UIElement)RefVideoSettings).Visibility = (Visibility)1;
		((UIElement)RefSoundSettings).Visibility = (Visibility)1;
		((UIElement)RefKeySettings).Visibility = (Visibility)1;
		((UIElement)RefControlSettings).Visibility = (Visibility)1;
		((UIElement)RefCheatSettings).Visibility = (Visibility)1;
		((UIElement)RefNameSettings).Visibility = (Visibility)1;
		((UIElement)RefCoASettings).Visibility = (Visibility)1;
		switch (menuSection)
		{
		case 0:
			((UIElement)RefVideoSettings).Visibility = (Visibility)2;
			UpdateResListbox();
			break;
		case 1:
			((UIElement)RefSoundSettings).Visibility = (Visibility)2;
			break;
		case 2:
			((UIElement)RefKeySettings).Visibility = (Visibility)2;
			break;
		case 3:
			((UIElement)RefControlSettings).Visibility = (Visibility)2;
			break;
		case 4:
			((UIElement)RefNameSettings).Visibility = (Visibility)2;
			break;
		case 6:
			HUD_CoatOfArms.InitBackground();
			((UIElement)RefCoASettings).Visibility = (Visibility)2;
			break;
		case 7:
			((UIElement)RefCheatSettings).Visibility = (Visibility)2;
			break;
		case 5:
			break;
		}
	}

	public void UpdateControls()
	{
		if (ConfigSettings.Settings_PushMapScrolling)
		{
			MainViewModel.Instance.OptionsPushEnabled = (Visibility)2;
			MainViewModel.Instance.OptionsPushDisabled = (Visibility)1;
		}
		else
		{
			MainViewModel.Instance.OptionsPushEnabled = (Visibility)1;
			MainViewModel.Instance.OptionsPushDisabled = (Visibility)2;
		}
		if (ConfigSettings.Settings_SH1RTSControls)
		{
			MainViewModel.Instance.OptionsSH1RTS = (Visibility)2;
			MainViewModel.Instance.OptionsDERTS = (Visibility)1;
		}
		else
		{
			MainViewModel.Instance.OptionsSH1RTS = (Visibility)1;
			MainViewModel.Instance.OptionsDERTS = (Visibility)2;
		}
		if (ConfigSettings.Settings_SH1MouseWheel)
		{
			MainViewModel.Instance.OptionsWheelSH1 = (Visibility)2;
			MainViewModel.Instance.OptionsWheelZoom = (Visibility)1;
		}
		else
		{
			MainViewModel.Instance.OptionsWheelSH1 = (Visibility)1;
			MainViewModel.Instance.OptionsWheelZoom = (Visibility)2;
		}
		if (ConfigSettings.Settings_SH1CentreControls)
		{
			MainViewModel.Instance.OptionsCenteringSH1 = (Visibility)2;
			MainViewModel.Instance.OptionsCenteringModern = (Visibility)1;
		}
		else
		{
			MainViewModel.Instance.OptionsCenteringSH1 = (Visibility)1;
			MainViewModel.Instance.OptionsCenteringModern = (Visibility)2;
		}
	}

	public void UpdateCursors()
	{
		if (ConfigSettings.Settings_CursorStyle == 0)
		{
			PropEx.SetSprite1((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[265]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[263]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[263]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[265]);
			PropEx.SetSprite1((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite1((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite1((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[268]);
		}
		else if (ConfigSettings.Settings_CursorStyle == 1)
		{
			PropEx.SetSprite1((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[264]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[264]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[264]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[264]);
			PropEx.SetSprite1((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite1((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite1((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[268]);
		}
		else if (ConfigSettings.Settings_CursorStyle == 2)
		{
			PropEx.SetSprite1((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[265]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[263]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[263]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[265]);
			PropEx.SetSprite1((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite1((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite1((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[268]);
		}
		else if (ConfigSettings.Settings_CursorStyle == 3)
		{
			PropEx.SetSprite1((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[265]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[263]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[263]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSystemButton, MainViewModel.Instance.GameSprites[265]);
			PropEx.SetSprite1((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSwordX2Button, MainViewModel.Instance.GameSprites[267]);
			PropEx.SetSprite1((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSwordButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite1((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[268]);
			PropEx.SetSprite2((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite3((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[266]);
			PropEx.SetSprite4((UIElement)(object)RefCursorSwordXButton, MainViewModel.Instance.GameSprites[268]);
		}
		if (ConfigSettings.Settings_Scribe == 0)
		{
			PropEx.SetSprite1((UIElement)(object)RefScribeClassicButton, MainViewModel.Instance.GameSprites[340]);
			PropEx.SetSprite2((UIElement)(object)RefScribeClassicButton, MainViewModel.Instance.GameSprites[340]);
			PropEx.SetSprite3((UIElement)(object)RefScribeClassicButton, MainViewModel.Instance.GameSprites[340]);
			PropEx.SetSprite4((UIElement)(object)RefScribeClassicButton, MainViewModel.Instance.GameSprites[340]);
			PropEx.SetSprite1((UIElement)(object)RefScribeModernButton, MainViewModel.Instance.GameSprites[344]);
			PropEx.SetSprite2((UIElement)(object)RefScribeModernButton, MainViewModel.Instance.GameSprites[343]);
			PropEx.SetSprite3((UIElement)(object)RefScribeModernButton, MainViewModel.Instance.GameSprites[343]);
			PropEx.SetSprite4((UIElement)(object)RefScribeModernButton, MainViewModel.Instance.GameSprites[344]);
		}
		else if (ConfigSettings.Settings_Scribe == 2)
		{
			PropEx.SetSprite1((UIElement)(object)RefScribeClassicButton, MainViewModel.Instance.GameSprites[341]);
			PropEx.SetSprite2((UIElement)(object)RefScribeClassicButton, MainViewModel.Instance.GameSprites[340]);
			PropEx.SetSprite3((UIElement)(object)RefScribeClassicButton, MainViewModel.Instance.GameSprites[340]);
			PropEx.SetSprite4((UIElement)(object)RefScribeClassicButton, MainViewModel.Instance.GameSprites[341]);
			PropEx.SetSprite1((UIElement)(object)RefScribeModernButton, MainViewModel.Instance.GameSprites[343]);
			PropEx.SetSprite2((UIElement)(object)RefScribeModernButton, MainViewModel.Instance.GameSprites[343]);
			PropEx.SetSprite3((UIElement)(object)RefScribeModernButton, MainViewModel.Instance.GameSprites[343]);
			PropEx.SetSprite4((UIElement)(object)RefScribeModernButton, MainViewModel.Instance.GameSprites[343]);
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

	public void UpdateLords()
	{
		MainViewModel.Instance.Options_CurrentLord = GetLordName(ConfigSettings.Settings_LordType);
		if (ConfigSettings.Settings_LordType == 0)
		{
			PropEx.SetSprite1((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite2((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite3((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite4((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[396]);
			PropEx.SetSprite2((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite3((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[397]);
			PropEx.SetSprite4((UIElement)(object)RefCrusaderLordButton, MainViewModel.Instance.GameSprites[396]);
		}
		if (ConfigSettings.Settings_LordType == 1)
		{
			PropEx.SetSprite1((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite2((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite3((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite4((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[398]);
			PropEx.SetSprite2((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite3((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[399]);
			PropEx.SetSprite4((UIElement)(object)RefArabicLordButton, MainViewModel.Instance.GameSprites[398]);
		}
		if (ConfigSettings.Settings_LordType == 2)
		{
			PropEx.SetSprite1((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite2((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite3((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite4((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[659]);
			PropEx.SetSprite2((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite3((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[660]);
			PropEx.SetSprite4((UIElement)(object)RefBedouinLordButton, MainViewModel.Instance.GameSprites[659]);
		}
		if (ConfigSettings.Settings_LordType == 3)
		{
			PropEx.SetSprite1((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite2((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite3((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite4((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[661]);
			PropEx.SetSprite2((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite3((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[662]);
			PropEx.SetSprite4((UIElement)(object)RefScribeLordButton, MainViewModel.Instance.GameSprites[661]);
		}
		if (ConfigSettings.Settings_LordType == 4)
		{
			PropEx.SetSprite1((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite2((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite3((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite4((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[663]);
			PropEx.SetSprite2((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite3((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[664]);
			PropEx.SetSprite4((UIElement)(object)RefFemaleLordButton, MainViewModel.Instance.GameSprites[663]);
		}
		if (ConfigSettings.Settings_LordType == 5)
		{
			PropEx.SetSprite1((UIElement)(object)RefBessyLordButton, MainViewModel.Instance.GameSprites[668]);
			PropEx.SetSprite2((UIElement)(object)RefBessyLordButton, MainViewModel.Instance.GameSprites[668]);
			PropEx.SetSprite3((UIElement)(object)RefBessyLordButton, MainViewModel.Instance.GameSprites[668]);
			PropEx.SetSprite4((UIElement)(object)RefBessyLordButton, MainViewModel.Instance.GameSprites[668]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefBessyLordButton, MainViewModel.Instance.GameSprites[667]);
			PropEx.SetSprite2((UIElement)(object)RefBessyLordButton, MainViewModel.Instance.GameSprites[668]);
			PropEx.SetSprite3((UIElement)(object)RefBessyLordButton, MainViewModel.Instance.GameSprites[668]);
			PropEx.SetSprite4((UIElement)(object)RefBessyLordButton, MainViewModel.Instance.GameSprites[667]);
		}
		if (AchievementsCommon.Instance.IsAchievementComplete(Enums.Achievements.Place_Dairy_Farms))
		{
			((UIElement)RefBessyLordButton).Visibility = (Visibility)2;
		}
		else
		{
			((UIElement)RefBessyLordButton).Visibility = (Visibility)1;
		}
		if (ConfigSettings.Settings_LordType == 6)
		{
			PropEx.SetSprite1((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite2((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite3((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite4((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[709]);
			PropEx.SetSprite2((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite3((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[710]);
			PropEx.SetSprite4((UIElement)(object)RefArabicLordFemaleButton, MainViewModel.Instance.GameSprites[709]);
		}
		if (ConfigSettings.Settings_LordType == 7)
		{
			PropEx.SetSprite1((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite2((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite3((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite4((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[711]);
			PropEx.SetSprite2((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite3((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[712]);
			PropEx.SetSprite4((UIElement)(object)RefBedouinLordFemaleButton, MainViewModel.Instance.GameSprites[711]);
		}
	}

	public void RefResolutionCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		if (((Selector)RefResolutionCombo).SelectedItem != null)
		{
			resChanged = true;
			MainViewModel.Instance.OptionsApplyVisible = (Visibility)2;
		}
	}

	public void RefScreenModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
	{
		if (((Selector)RefScreenModeCombo).SelectedItem != null)
		{
			screenModeChanged = true;
			MainViewModel.Instance.OptionsApplyVisible = (Visibility)2;
		}
	}

	public void UpdateResListbox(bool fromSettings = false)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Expected O, but got Unknown
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		int num;
		int num2;
		int num3;
		if (!fromSettings)
		{
			num = Screen.width;
			num2 = Screen.height;
			Resolution currentResolution = Screen.currentResolution;
			num3 = ((Resolution)(ref currentResolution)).refreshRate;
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
		((Selector)RefResolutionCombo).SelectedItem = null;
		Enumerator enumerator = ((ItemsControl)RefResolutionCombo).Items.GetEnumerator();
		while (((Enumerator)(ref enumerator)).MoveNext())
		{
			ComboBoxItem val = (ComboBoxItem)((Enumerator)(ref enumerator)).Current;
			Resolution val2 = (Resolution)((FrameworkElement)val).Tag;
			if (num == ((Resolution)(ref val2)).width && num2 == ((Resolution)(ref val2)).height && Math.Abs(((Resolution)(ref val2)).refreshRate - num3) < 2)
			{
				((Selector)RefResolutionCombo).SelectedItem = val;
				break;
			}
		}
	}

	public void MasterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)((RangeBase)RefMasterVolumeSlider).Value;
			MainViewModel.Instance.MasterVolumeValue = num.ToString();
			ConfigSettings.Settings_MasterVolume = (float)num / 100f;
			MyAudioManager.Instance.updateSFXVolumeFromSettings();
			MyAudioManager.Instance.updateSpeechVolumeFromSettings();
			MyAudioManager.Instance.updateMusicVolumeFromSettings();
			lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
		}
	}

	public void MusicVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)((RangeBase)RefMusicVolumeSlider).Value;
			MainViewModel.Instance.MusicVolumeValue = num.ToString();
			ConfigSettings.Settings_MusicVolume = (float)num / 100f;
			MyAudioManager.Instance.updateMusicVolumeFromSettings();
			lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
		}
	}

	public void SpeechVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)((RangeBase)RefSpeechVolumeSlider).Value;
			MainViewModel.Instance.SpeechVolumeValue = num.ToString();
			ConfigSettings.Settings_SpeechVolume = (float)num / 100f;
			MyAudioManager.Instance.updateSpeechVolumeFromSettings();
			lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
		}
	}

	public void UnitSpeechVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)((RangeBase)RefUnitSpeechVolumeSlider).Value;
			MainViewModel.Instance.UnitSpeechVolumeValue = num.ToString();
			ConfigSettings.Settings_UnitSpeechVolume = (float)num / 100f;
			MyAudioManager.Instance.updateSpeechVolumeFromSettings();
			lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
		}
	}

	public void SFXVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)((RangeBase)RefSFXVolumeSlider).Value;
			MainViewModel.Instance.SfxVolumeValue = num.ToString();
			ConfigSettings.Settings_SFXVolume = (float)num / 100f;
			MyAudioManager.Instance.updateSFXVolumeFromSettings();
			lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
		}
	}

	public void ScrollSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int settings_ScrollSpeed = (int)((RangeBase)RefScrollSpeedSlider).Value;
			MainViewModel.Instance.ScrollSpeedValue = settings_ScrollSpeed.ToString();
			ConfigSettings.Settings_ScrollSpeed = settings_ScrollSpeed;
			lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
		}
	}

	public void GameSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			int num = (int)((RangeBase)RefGameSpeedSlider).Value * 5;
			if (!Director.instance.MultiplayerGame)
			{
				MainViewModel.Instance.GameSpeedValue = num.ToString();
				Director.instance.SetEngineFrameRate(num);
				ConfigSettings.Settings_GameSpeed = num;
				lastDynamicChanged = DateTime.UtcNow.AddSeconds(2.0);
			}
		}
	}

	public void UIScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (panelActive)
		{
			MainViewModel.Instance.OptionsScaleApplyVisible = (Visibility)2;
		}
	}

	public void LockCursor_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_LockCursor = ((ToggleButton)RefLockCursorCheck).IsChecked.Value;
			Save();
		}
	}

	public void BuildingTooltipsCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ShowBuildingTooltips = ((ToggleButton)RefBuildingTooltipsCheck).IsChecked.Value;
			Save();
		}
	}

	public void SteamHelp_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_UseSteamOverlayForHelp = ((ToggleButton)RefSteamHelpCheck).IsChecked.Value;
			Save();
		}
	}

	public void CompassCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_Compass = ((ToggleButton)RefCompassCheck).IsChecked.Value;
			Save();
			if (Director.instance.SimRunning || MainViewModel.Instance.Show_HUD_OptionsMP)
			{
				MainViewModel.Instance.IngameUI.setRotationImage(GameMap.instance.CurrentRotation());
				MainViewModel.Instance.Compass_Vis = ConfigSettings.Settings_Compass;
			}
		}
	}

	public void LocalTimeCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (!panelActive)
		{
			return;
		}
		ConfigSettings.Settings_ShowLocalTime = ((ToggleButton)RefLocalTimeCheck).IsChecked.Value;
		Save();
		if (Director.instance.SimRunning || MainViewModel.Instance.Show_HUD_OptionsMP)
		{
			if (ConfigSettings.Settings_ShowLocalTime)
			{
				MainViewModel.Instance.ShowLocalTime = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.ShowLocalTime = (Visibility)0;
			}
		}
	}

	public void GameTimeCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (!panelActive)
		{
			return;
		}
		ConfigSettings.Settings_ShowGameTime = ((ToggleButton)RefGameTimeCheck).IsChecked.Value;
		Save();
		if (Director.instance.SimRunning || MainViewModel.Instance.Show_HUD_OptionsMP)
		{
			if (ConfigSettings.Settings_ShowGameTime && !MainViewModel.Instance.IsMapEditorMode)
			{
				MainViewModel.Instance.ShowGameTime = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.ShowGameTime = (Visibility)0;
			}
		}
	}

	public void SandsTimerCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ShowSandsTimer = ((ToggleButton)RefSandsTimerCheck).IsChecked.Value;
			Save();
			if (Director.instance.SimRunning && GameData.Instance.IsSandsOfTime())
			{
				MainViewModel.Instance.Show_OST_SandsOfTimeVis = ConfigSettings.Settings_ShowSandsTimer && !ConfigSettings.Settings_HideSoTTiming;
			}
		}
	}

	public void RadarZoomCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_RadarDefaultZoomedOut = ((ToggleButton)RefRadarZoomCheck).IsChecked.Value;
			Save();
		}
	}

	public void CustomIntros_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_CustomIntros = ((ToggleButton)RefCustomIntros).IsChecked.Value;
			Save();
		}
	}

	public void UISounds_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_PlayUISFX = ((ToggleButton)RefUISoundsCheck).IsChecked.Value;
			Save();
		}
	}

	public void GenieSpeech_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_GenieSpeech = ((ToggleButton)RefGenieSpeechCheck).IsChecked.Value;
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

	public void EnglishSpeech_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_EnglishSpeech = ((ToggleButton)RefEnglishSpeechCheck).IsChecked.Value;
			Save();
		}
	}

	public void MuteInsult_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_MuteInsults = ((ToggleButton)RefMuteInsultsCheck).IsChecked.Value;
			ConfigSettings.Settings_MuteInsultSpeech = ((ToggleButton)RefMuteInsultSpeechCheck).IsChecked.Value;
			Save();
		}
	}

	public void MuteBackground_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_BackgroundAudio = !((ToggleButton)RefMuteBackgroundCheck).IsChecked.Value;
			Save();
		}
	}

	public void ReduceSounds_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ReduceMusicVolumeForSpeech = ((ToggleButton)RefReduceSoundsCheck).IsChecked.Value;
			Save();
		}
	}

	public void CheatKeys_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_CheatKeysEnabled = ((ToggleButton)RefCheatKeysCheck).IsChecked.Value;
			Save();
		}
	}

	public void Pings_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ShowPings = ((ToggleButton)RefPingsCheck).IsChecked.Value;
			Save();
		}
	}

	public void ExtraZoom_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ExtraZoom = ((ToggleButton)RefExtraZoomCheck).IsChecked.Value;
			Save();
		}
	}

	public void ShowMoat_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ShowPlannedMoat = ((ToggleButton)RefShowMoatCheck).IsChecked.Value;
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

	public void ConfirmDisband_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_Confirm_Disband_Troops = ((ToggleButton)RefConfirmDisbandCheck).IsChecked.Value;
			Save();
		}
	}

	public void Leaderboard_OptOut_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_Leaderboard_OptOut = ((ToggleButton)RefLeaderboard_OptOut).IsChecked.Value;
			Save();
		}
	}

	public void Leaderboard_Names_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_Leaderboard_Names = ((ToggleButton)RefLeaderboard_Names).IsChecked.Value;
			Save();
		}
	}

	public void Leaderboard_Images_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_Leaderboard_Images = ((ToggleButton)RefLeaderboard_Images).IsChecked.Value;
			Save();
		}
	}

	public void SandsTimeDisable_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_HideSoTTiming = ((ToggleButton)RefSandsTimeDisable).IsChecked.Value;
			MainViewModel.Instance.Show_LeaderboardOptIn = !ConfigSettings.Settings_HideSoTTiming;
			Save();
		}
	}

	public void MuteMPChat_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_MuteMPChat = ((ToggleButton)RefChatMuteDisable).IsChecked.Value;
			Save();
		}
	}

	public void ArabicL2R_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			ConfigSettings.Settings_ArabicL2R = ((ToggleButton)RefArabicL2RCheck).IsChecked.Value;
			Save();
		}
	}

	public void VSyncCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			MainViewModel.Instance.OptionsApplyVisible = (Visibility)2;
		}
	}

	public static void SetVSync(bool state)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Invalid comparison between Unknown and I4
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (state)
		{
			if (((int)Screen.fullScreenMode == 1 || (int)Screen.fullScreenMode == 0) && ConfigSettings.Settings_LastFullscreenRefresh > 0)
			{
				Application.targetFrameRate = ConfigSettings.Settings_LastFullscreenRefresh;
			}
			else
			{
				Resolution currentResolution = Screen.currentResolution;
				Application.targetFrameRate = ((Resolution)(ref currentResolution)).refreshRate;
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
			((UIElement)RefUIScaleGrid).Visibility = (Visibility)1;
		}
		else
		{
			((UIElement)RefUIScaleGrid).Visibility = (Visibility)2;
		}
	}

	public void NewsletterValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			((UIElement)RefNewsletterSignupButton).IsEnabled = IsValidEmail(RefTextBoxNewsletter.Text) && ((ToggleButton)RefNewsletterCheck).IsChecked.Value;
		}
	}

	public void NewsletterCheck_ValueChanged(object sender, RoutedEventArgs e)
	{
		if (panelActive)
		{
			((UIElement)RefNewsletterSignupButton).IsEnabled = IsValidEmail(RefTextBoxNewsletter.Text) && ((ToggleButton)RefNewsletterCheck).IsChecked.Value;
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

	public void InitializeComponent()
	{
		NoesisUnity.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_Options.xaml");
	}

	public override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected O, but got Unknown
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Expected O, but got Unknown
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
		if (eventName == "MouseEnter" && handlerName == "ChickenButtonEnter")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseEnter += new MouseEventHandler(ChickenButtonEnter);
			}
			return true;
		}
		return false;
	}

	public void ChickenButtonEnter(object sender, MouseEventArgs e)
	{
		SFXManager.instance.playUISound(137);
	}

	public void TextInputFocus(object sender, DependencyPropertyChangedEventArgs e)
	{
		MainViewModel.Instance.SetNoesisKeyboardState((bool)e.NewValue);
	}

	public void CreateHotkeyList()
	{
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
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
			if ((int)keyCode == 0 && (int)keyCode2 == 0)
			{
				hotKeyRow.Text2 = text;
			}
			else if ((int)keyCode2 == 0)
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
		((ItemsControl)RefHotKeyList).ItemsSource = hotKeyRows;
	}

	public static string GetPlaceBuildingHotkeyText(int value)
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
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected I4, but got Unknown
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Invalid comparison between Unknown and I4
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Invalid comparison between Unknown and I4
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Invalid comparison between Unknown and I4
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0767: Unknown result type (might be due to invalid IL or missing references)
		int num = code & 0xFFFF;
		bool num2 = (code & 0x10000) > 0;
		bool flag = (code & 0x20000) > 0;
		bool flag2 = (code & 0x40000) > 0;
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
		return num switch
		{
			48 => text + "0", 
			49 => text + "1", 
			50 => text + "2", 
			51 => text + "3", 
			52 => text + "4", 
			53 => text + "5", 
			54 => text + "6", 
			55 => text + "7", 
			56 => text + "8", 
			57 => text + "9", 
			96 => text + "`", 
			92 => text + "\\", 
			45 => text + "-", 
			61 => text + "=", 
			91 => text + "[", 
			93 => text + "]", 
			59 => text + ";", 
			39 => text + "'", 
			44 => text + ",", 
			46 => text + ".", 
			47 => text + "/", 
			32 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 109), 
			256 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 0", 
			257 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 1", 
			258 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 2", 
			259 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 3", 
			260 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 4", 
			261 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 5", 
			262 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 6", 
			263 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 7", 
			264 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 8", 
			265 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " 9", 
			325 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 112) + " 2", 
			326 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 112) + " 3", 
			327 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 112) + " 4", 
			328 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 112) + " 5", 
			329 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 112) + " 6", 
			266 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " .", 
			270 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " +", 
			269 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " -", 
			267 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " /", 
			268 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " *", 
			271 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " " + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 82), 
			272 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 81) + " =", 
			9 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 83), 
			301 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 84), 
			13 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 85), 
			8 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 86), 
			273 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 87), 
			274 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 88), 
			275 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 89), 
			276 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 90), 
			277 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 91), 
			278 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 92), 
			279 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 93), 
			280 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 94), 
			281 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 95), 
			127 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 96), 
			300 => text + Translate.Instance.lookUpText(Enums.eTextSections.TEXT_HOT_KEYS, 97), 
			_ => text + ((object)(KeyCode)num/*cast due to constrained. prefix*/).ToString(), 
		};
	}
}
