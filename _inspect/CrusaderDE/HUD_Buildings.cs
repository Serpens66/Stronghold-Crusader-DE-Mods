using Noesis;

namespace CrusaderDE;

public class HUD_Buildings : UserControl
{
	public ToggleButton RefRecruitArcherButton;

	public ToggleButton RefRecruitSpearmanButton;

	public ToggleButton RefRecruitMacemanButton;

	public ToggleButton RefRecruitXBowmanButton;

	public ToggleButton RefRecruitPikemanButton;

	public ToggleButton RefRecruitSwordsmanButton;

	public ToggleButton RefRecruitKnightButton;

	public Button RefRecruitEngineerButton;

	public Button RefRecruitEngineerButtonX;

	public Button RefRecruitLaddermanButton;

	public Button RefRecruitLaddermanButtonX;

	public Button RefRecruitTunellerButton;

	public Button RefRecruitMonkButton;

	public Button RefButtonReleaseDogs;

	public ToggleButton RefRecruitArabBowButton;

	public ToggleButton RefRecruitArabSlaveButton;

	public ToggleButton RefRecruitArabSlingerButton;

	public ToggleButton RefRecruitArabAssassinButton;

	public ToggleButton RefRecruitArabHorseArcherButton;

	public ToggleButton RefRecruitArabSwordsmanButton;

	public ToggleButton RefRecruitArabGrenadierButton;

	public ToggleButton RefRecruitBedouinCamelLancerButton;

	public ToggleButton RefRecruitBedouinHealerButton;

	public ToggleButton RefRecruitBedouinEunuchButton;

	public ToggleButton RefRecruitBedouinAmbusherButton;

	public ToggleButton RefRecruitBedouinSkirmisherButton;

	public ToggleButton RefRecruitBedouinHeavyCamelButton;

	public ToggleButton RefRecruitBedouinSapperButton;

	public ToggleButton RefRecruitBedouinDemolisherButton;

	public Grid RefHUDBuildingFullClickMask;

	public Grid RefBuildingPanel;

	public Grid RefBarracksPanel;

	public Grid RefMercPostPanel;

	public Grid RefBedouinStockadePanel;

	public Grid RefWorkerPanel;

	public Grid RefKeepPanel;

	public Grid RefOutpostPanel;

	public Grid RefOutpostArabPanel;

	public Grid RefOutpostBedouinPanel;

	public Grid RefGranaryPanel;

	public Grid RefArmouryPanel;

	public Grid RefStockpilePanel;

	public Grid RefInnPanel;

	public Grid RefFletchersPanel;

	public Grid RefPoleturnersPanel;

	public Grid RefBlacksmithsPanel;

	public Grid RefChurchPanel;

	public Grid RefTradepostPanel;

	public Grid RefTradingFoodPanel;

	public Grid RefTradingResourcesPanel;

	public Grid RefTradingWeaponsPanel;

	public Grid RefTradingPricesPanel;

	public Grid RefTradingTradePanel;

	public Button RefTradeBuyButton;

	public Button RefTradeSellButton;

	public Storyboard RefTradeErrorAnination;

	public Grid RefTradePost_Trade_Normal;

	public Grid RefTradePost_Trade_Auto;

	public Slider RefAutotrade_Sell_Slider;

	public Slider RefAutotrade_Buy_Slider;

	public Button RefTrade_AutoToggle;

	public Button RefTrade_GoTo_Auto;

	public Grid RefReportsPanel;

	public Grid RefReportsPopularityPanel;

	public Grid RefReportsPopEventsPanel;

	public Grid RefShowEventsButton;

	public Grid RefReportsFearFactorPanel;

	public Grid RefReportsPopulationPanel;

	public Grid RefReportsArmy1Panel;

	public Grid RefReportsArmy2Panel;

	public Grid RefReportsArmy3Panel;

	public Grid RefReportsArmy4Panel;

	public Grid RefReportsStoresPanel;

	public Grid RefReportsWeaponsPanel;

	public Grid RefReportsReligionPanel;

	public TextBlock RefWGT_PopReportAleText;

	public Grid RefChimpPanel;

	public Grid RefReportsFoodPanel;

	public Button RefHelpButton;

	public Grid RefShowWorkersPanel;

	public Grid RefShowInfoPanel;

	public Grid RefShowRepairPanel;

	public Grid RefShowGatePanel;

	public Grid RefShowDogsPanel;

	public Grid RefShowDrawbridgePanel;

	public Grid RefShowEngineersGuildPanel;

	public Grid RefShowTunellersGuildPanel;

	public Grid RefShowCathedralPanel;

	public Button RefButtonRepair;

	public TextBlock RefTroopCostsText;

	public TextBlock RefTroopNameText;

	public TextBlock RefTroopHelpText;

	public TextBlock RefArabTroopCostsText;

	public TextBlock RefArabTroopNameText;

	public TextBlock RefArabTroopHelpText;

	public TextBlock RefBedouinTroopCostsText;

	public TextBlock RefBedouinTroopNameText;

	public TextBlock RefBedouinTroopHelpText;

	public TextBlock RefEngineersCostsText;

	public TextBlock RefEngineersHelpText;

	public TextBlock RefTunellersCostsText;

	public TextBlock RefTunellersHelpText;

	public TextBlock RefDogsReleasedText;

	public TextBlock RefMonkCostsText;

	public TextBlock RefMonkHelpText;

	public TextBlock RefEngineersGuildNoGoldMessage;

	public TextBlock RefTunnllersGuildNoGoldMessage;

	public TextBlock RefCathedralNoGoldMessage;

	public WGT_Popularity RefWGTFoodTypePop;

	public WGT_Popularity RefWGTRationsPop;

	public WGT_Popularity RefWGTInnPop;

	public WGT_Popularity RefWGTTaxPop;

	public WGT_Popularity RefWGTPopReportFoodPop;

	public WGT_Popularity RefWGTPopReportTaxPop;

	public WGT_Popularity RefWGTPopReportCrowdingPop;

	public WGT_Popularity RefWGTPopReportFearFactorPop;

	public WGT_Popularity RefWGTPopReportReligionPop;

	public WGT_Popularity RefWGTPopReportAlePop;

	public WGT_Popularity RefWGTPopReportEventsPop;

	public WGT_Popularity RefWGTPopReportTotalPop;

	public WGT_Popularity RefWGTPopReportTotal2Pop;

	public WGT_Popularity RefWGTPopReportFairsPop;

	public WGT_Popularity RefWGTPopReportMarriagePop;

	public WGT_Popularity RefWGTPopReportJesterPop;

	public WGT_Popularity RefWGTPopReportPlaguePop;

	public WGT_Popularity RefWGTPopReportWolvesPop;

	public WGT_Popularity RefWGTPopReportBanditsPop;

	public WGT_Popularity RefWGTPopReportFirePop;

	public WGT_Popularity RefWGTFFReportFearFactorPop;

	public WGT_Popularity RefWGTRelReport;

	public WGT_Popularity RefWGTRelReport2;

	public Image RefRationHandNone;

	public Image RefRationHandHalf;

	public Image RefRationHandFull;

	public Image RefRationHandExtra;

	public Image RefRationHandDouble;

	public Image RefStopMeatConsumption;

	public Image RefStopCheeseConsumption;

	public Image RefStopBreadConsumption;

	public Image RefStopApplesConsumption;

	public Button RefButtonOpenGate;

	public Button RefButtonCloseGate;

	public Button RefButtonOpenBridge;

	public Button RefButtonCloseBridge;

	public RadioButton RefProducingBows;

	public RadioButton RefProducingXBows;

	public RadioButton RefProducingSpears;

	public RadioButton RefProducingPikes;

	public RadioButton RefProducingSwords;

	public RadioButton RefProducingMaces;

	public TextBlock RefRelReportPopEffectLabel;

	public TextBlock RefWGT_RelReportLabel;

	public Button RefBuildingZZZButtonOn;

	public Button RefBuildingZZZButtonOff;

	public Button RefButtonArmyReportBack;

	public Button RefButtonArmyReportBack2;

	public Button RefButtonArmyReportBack3;

	public TextBlock RefAutotrade_Buy_Text;

	public TextBlock RefAutotrade_Sell_Text;

	public TextBlock RefTradeErrorMessage;

	public bool inSetup;

	public Slider RefOutpostSizeSlider;

	public Slider RefOutpostDelaySlider;

	public Slider RefOutpostArabSizeSlider;

	public Slider RefOutpostArabDelaySlider;

	public Slider RefOutpostBedouinSizeSlider;

	public Slider RefOutpostBedouinDelaySlider;

	public TextBlock RefKeepTaxRate;

	public Grid RefReportsReligionPanelSubArea;

	public bool currentAutoTradeOn;

	public bool sliderSetup;

	public bool insideValueChanged;

	public TranslateTransform[] SelTroopPositions = (TranslateTransform[])(object)new TranslateTransform[8]
	{
		new TranslateTransform(-181f, 0f),
		new TranslateTransform(-130f, 0f),
		new TranslateTransform(-78f, 0f),
		new TranslateTransform(-27f, 0f),
		new TranslateTransform(25f, 0f),
		new TranslateTransform(76f, 0f),
		new TranslateTransform(128f, 0f),
		new TranslateTransform(180f, 0f)
	};

	public int[] sketchList = new int[110]
	{
		0, 12, 0, 13, 14, 15, 16, 17, 0, 0,
		0, 0, 18, 19, 20, 21, 22, 23, 24, 0,
		25, 25, 26, 27, 100, 97, 0, 28, 29, 0,
		30, 31, 32, 33, 34, 35, 36, 36, 36, 0,
		37, 37, 37, 37, 37, 39, 39, 39, 39, 39,
		102, 0, 103, 0, 104, 38, 0, 0, 0, 0,
		0, 39, 40, 41, 0, 42, 43, 44, 0, 0,
		96, 37, 37, 37, 39, 39, 39, 39, 39, 39,
		104, 104, 104, 104, 104, 0, 39, 39, 39, 39,
		0, 45, 46, 47, 48, 49, 0, 50, 51, 52,
		53, 81, 0, 54, 55, 0, 0, 0, 0, 0
	};

	public string[] BuildingTitles = new string[110]
	{
		"", "TEXT_IN_HOUSE_001", "TEXT_IN_OUTPOST_025", "TEXT_IN_WOODCUTTERS_HUT_001", "TEXT_IN_OXEN_BASE_001", "TEXT_IN_IRON_MINE_001", "TEXT_IN_PITCH_DIGGER_001", "TEXT_IN_HUNTERS_HUT_001", "TEXT_IN_BARRACKS_001", "TEXT_IN_BARRACKS_001",
		"TEXT_IN_GOODS_YARD_001", "TEXT_IN_ARMOURY_001", "TEXT_IN_FLETCHERS_WORKSHOP_001", "TEXT_IN_BLACKSMITHS_WORKSHOP_001", "TEXT_IN_POLETURNERS_WORKSHOP_001", "TEXT_IN_ARMOURERS_WORKSHOP_001", "TEXT_IN_TANNERS_WORKSHOP_001", "TEXT_IN_BAKERS_WORKSHOP_001", "TEXT_IN_BREWERS_WORKSHOP_001", "TEXT_IN_GRANARY_001",
		"TEXT_IN_QUARRY_001", "TEXT_IN_QUARRYPILE_001", "TEXT_IN_INN_001", "TEXT_IN_HEALERS_001", "TEXT_IN_ENGINEERS_GUILD_001", "TEXT_IN_TUNNELLERS_GUILD_001", "TEXT_IN_TRADEPOST_001", "TEXT_IN_WELL_001", "TEXT_IN_OIL_SMELTER_001", "TEXT_IN_SIEGE_TENT_001",
		"TEXT_IN_WHEATFARM_001", "TEXT_IN_HOPSFARM_001", "TEXT_IN_APPLEFARM_001", "TEXT_IN_CATTLEFARM_001", "TEXT_IN_MILL_001", "TEXT_IN_STABLES_001", "TEXT_IN_CHURCH_001", "TEXT_IN_CHURCH_004", "TEXT_IN_CHURCH_005", "TEXT_BUBBLE_HELP_TEXT_232",
		"TEXT_IN_KEEP_001", "TEXT_IN_KEEP_003", "TEXT_IN_KEEP_005", "TEXT_IN_KEEP_004", "TEXT_IN_KEEP_005", "TEXT_IN_GATEHOUSE_001", "TEXT_IN_GATEHOUSE_001", "TEXT_IN_GATEHOUSE_001", "TEXT_IN_POSTERN_GATE_001", "TEXT_IN_DRAWBRIDGE_001",
		"TEXT_IN_TUNNEL_ENTERANCE_001", "TEXT_IN_TRAINING_GROUND_001", "TEXT_IN_SIGNPOST_001", "TEXT_IN_TRAINING_GROUND_001", "TEXT_IN_SIEGE_TENT_008", "TEXT_IN_CAMP_FIRE_001", "TEXT_IN_TRAINING_GROUND_001", "TEXT_IN_TRAINING_GROUND_001", "TEXT_IN_TRAINING_GROUND_001", "TEXT_IN_TRAINING_GROUND_001",
		"TEXT_IN_GATEHOUSE_001", "TEXT_IN_TOWER_001", "TEXT_IN_GALLOWS_001", "TEXT_IN_STOCKS_001", "TEXT_IN_WITCH_HOIST_001", "TEXT_IN_MAYPOLE_001", "TEXT_IN_GARDEN_001", "TEXT_IN_KILLING_PIT_001", "", "",
		"TEXT_IN_WATERPOT_001", "TEXT_IN_KEEP_001", "TEXT_IN_KEEP_001", "TEXT_IN_KEEP_001", "TEXT_IN_TOWER_001", "TEXT_IN_TOWER_001", "TEXT_IN_TOWER_001", "TEXT_IN_TOWER_001", "TEXT_IN_TOWER_001", "TEXT_IN_TOWER_001",
		"TEXT_IN_CATAPULT_001", "TEXT_IN_TREBUCHET_001", "TEXT_IN_SIEGE_TENT_004", "TEXT_IN_SIEGE_TENT_005", "TEXT_IN_SIEGE_TENT_006", "TEXT_IN_TUNNEL_ENTERANCE_001", "TEXT_IN_TOWER_001", "TEXT_IN_TOWER_001", "TEXT_IN_TOWER_001", "TEXT_IN_TOWER_001",
		"", "TEXT_IN_CESS_PIT_001", "TEXT_IN_BURNING_STAKE_001", "TEXT_IN_GIBBET_001", "TEXT_IN_DUNGEON_001", "TEXT_IN_STRETCHING_RACK_001", "TEXT_IN_FLOGGING_RACK_001", "TEXT_IN_CHOPPING_BLOCK_001", "TEXT_IN_DUNKING_STOOL_001", "TEXT_IN_DOG_CAGE_001",
		"TEXT_IN_STATUE_001", "TEXT_IN_SHRINE_001", "TEXT_IN_BEEHIVE_001", "TEXT_IN_DANCING_BEAR_001", "TEXT_IN_POND_001", "TEXT_IN_BEAR_CAVE_001", "TEXT_IN_OUTPOST_001", "TEXT_IN_OUTPOST_010", "", ""
	};

	public string[] BuildingNames = new string[110]
	{
		"", "TEXT_IN_HOUSE_001", "TEXT_IN_OUTPOST_025", "TEXT_IN_WOODCUTTERS_HUT_001", "TEXT_IN_OXEN_BASE_001", "TEXT_IN_IRON_MINE_001", "TEXT_IN_PITCH_DIGGER_001", "TEXT_IN_HUNTERS_HUT_001", "TEXT_BUBBLE_HELP_TEXT_011", "TEXT_BUBBLE_HELP_TEXT_010",
		"TEXT_IN_GOODS_YARD_001", "TEXT_IN_ARMOURY_001", "TEXT_IN_FLETCHERS_WORKSHOP_001", "TEXT_IN_BLACKSMITHS_WORKSHOP_001", "TEXT_IN_POLETURNERS_WORKSHOP_001", "TEXT_IN_ARMOURERS_WORKSHOP_001", "TEXT_IN_TANNERS_WORKSHOP_001", "TEXT_IN_BAKERS_WORKSHOP_001", "TEXT_IN_BREWERS_WORKSHOP_001", "TEXT_IN_GRANARY_001",
		"TEXT_IN_QUARRY_001", "TEXT_IN_QUARRYPILE_001", "TEXT_IN_INN_001", "TEXT_IN_HEALERS_001", "TEXT_IN_ENGINEERS_GUILD_001", "TEXT_IN_TUNNELLERS_GUILD_001", "TEXT_IN_TRADEPOST_001", "TEXT_IN_WELL_001", "TEXT_IN_OIL_SMELTER_001", "TEXT_IN_SIEGE_TENT_001",
		"TEXT_IN_WHEATFARM_001", "TEXT_IN_HOPSFARM_001", "TEXT_IN_APPLEFARM_001", "TEXT_IN_CATTLEFARM_001", "TEXT_IN_MILL_001", "TEXT_IN_STABLES_001", "TEXT_IN_CHURCH_001", "TEXT_IN_CHURCH_004", "TEXT_IN_CHURCH_005", "TEXT_BUBBLE_HELP_TEXT_232",
		"TEXT_IN_KEEP_001", "TEXT_IN_KEEP_003", "TEXT_IN_KEEP_005", "TEXT_IN_KEEP_004", "TEXT_IN_KEEP_005", "TEXT_IN_GATEHOUSE_001", "TEXT_IN_GATEHOUSE_001", "TEXT_IN_GATEHOUSE_001", "TEXT_IN_POSTERN_GATE_001", "TEXT_IN_DRAWBRIDGE_001",
		"TEXT_IN_TUNNEL_ENTERANCE_001", "TEXT_IN_TRAINING_GROUND_001", "TEXT_IN_SIGNPOST_001", "TEXT_IN_TRAINING_GROUND_001", "TEXT_IN_SIEGE_TENT_008", "TEXT_IN_CAMP_FIRE_001", "TEXT_IN_TRAINING_GROUND_001", "TEXT_IN_TRAINING_GROUND_001", "TEXT_IN_TRAINING_GROUND_001", "TEXT_IN_TRAINING_GROUND_001",
		"TEXT_IN_GATEHOUSE_001", "TEXT_IN_TOWER_001", "TEXT_IN_GALLOWS_001", "TEXT_IN_STOCKS_001", "TEXT_IN_WITCH_HOIST_001", "TEXT_IN_MAYPOLE_001", "TEXT_IN_GARDEN_001", "TEXT_IN_KILLING_PIT_001", "", "",
		"TEXT_IN_WATERPOT_001", "TEXT_IN_KEEP_001", "TEXT_IN_KEEP_001", "TEXT_IN_KEEP_001", "TEXT_BUBBLE_HELP_TEXT_022", "TEXT_BUBBLE_HELP_TEXT_023", "TEXT_BUBBLE_HELP_TEXT_024", "TEXT_BUBBLE_HELP_TEXT_025", "TEXT_BUBBLE_HELP_TEXT_026", "TEXT_BUBBLE_HELP_TEXT_026",
		"TEXT_IN_CATAPULT_001", "TEXT_IN_TREBUCHET_001", "TEXT_IN_SIEGE_TENT_004", "TEXT_IN_SIEGE_TENT_005", "TEXT_IN_SIEGE_TENT_006", "TEXT_IN_TUNNEL_ENTERANCE_001", "TEXT_BUBBLE_HELP_TEXT_022", "TEXT_BUBBLE_HELP_TEXT_023", "TEXT_BUBBLE_HELP_TEXT_024", "TEXT_BUBBLE_HELP_TEXT_025",
		"", "TEXT_IN_CESS_PIT_001", "TEXT_IN_BURNING_STAKE_001", "TEXT_IN_GIBBET_001", "TEXT_IN_DUNGEON_001", "TEXT_IN_STRETCHING_RACK_001", "TEXT_IN_FLOGGING_RACK_001", "TEXT_IN_CHOPPING_BLOCK_001", "TEXT_IN_DUNKING_STOOL_001", "TEXT_IN_DOG_CAGE_001",
		"TEXT_IN_STATUE_001", "TEXT_IN_SHRINE_001", "TEXT_IN_BEEHIVE_001", "TEXT_IN_DANCING_BEAR_001", "TEXT_IN_POND_001", "TEXT_IN_BEAR_CAVE_001", "TEXT_IN_OUTPOST_001", "TEXT_IN_OUTPOST_010", "TEXT_BUBBLE_HELP_TEXT_349", ""
	};

	public int[] IsWorkerBuilding = new int[110]
	{
		0, 0, 0, 1, 1, 1, 1, 1, 0, 0,
		0, 0, 1, 1, 1, 1, 1, 1, 1, 0,
		1, 0, 1, 1, 0, 0, 0, 1, 0, 0,
		1, 1, 1, 1, 1, 0, 1, 1, 1, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		1, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0
	};

	public int[] ShowWorkersBuilding = new int[110]
	{
		0, 0, 0, 1, 1, 1, 1, 1, 0, 0,
		0, 0, 1, 1, 1, 1, 1, 1, 1, 0,
		1, 0, 1, 1, 0, 0, 0, 1, 0, 0,
		1, 1, 1, 1, 1, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		1, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		1, 0, 0, 0, 0, 0, 0, 0, 0, 0
	};

	public HUD_Buildings()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected O, but got Unknown
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Expected O, but got Unknown
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Expected O, but got Unknown
		//IL_08cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_08d5: Expected O, but got Unknown
		//IL_08e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_08eb: Expected O, but got Unknown
		//IL_08f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0901: Expected O, but got Unknown
		//IL_090d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0917: Expected O, but got Unknown
		//IL_0923: Unknown result type (might be due to invalid IL or missing references)
		//IL_092d: Expected O, but got Unknown
		//IL_0939: Unknown result type (might be due to invalid IL or missing references)
		//IL_0943: Expected O, but got Unknown
		//IL_094f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0959: Expected O, but got Unknown
		//IL_0965: Unknown result type (might be due to invalid IL or missing references)
		//IL_096f: Expected O, but got Unknown
		//IL_097b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0985: Expected O, but got Unknown
		//IL_0991: Unknown result type (might be due to invalid IL or missing references)
		//IL_099b: Expected O, but got Unknown
		//IL_09a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_09b1: Expected O, but got Unknown
		//IL_09bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_09c7: Expected O, but got Unknown
		//IL_09d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_09dd: Expected O, but got Unknown
		//IL_09e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_09f3: Expected O, but got Unknown
		//IL_09ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a09: Expected O, but got Unknown
		//IL_0a15: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a1f: Expected O, but got Unknown
		//IL_0a2b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a35: Expected O, but got Unknown
		//IL_0a41: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a4b: Expected O, but got Unknown
		//IL_0a57: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a61: Expected O, but got Unknown
		//IL_0a6d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a77: Expected O, but got Unknown
		//IL_0a83: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a8d: Expected O, but got Unknown
		//IL_0a99: Unknown result type (might be due to invalid IL or missing references)
		//IL_0aa3: Expected O, but got Unknown
		//IL_0aaf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ab9: Expected O, but got Unknown
		//IL_0ac5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0acf: Expected O, but got Unknown
		//IL_0adb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ae5: Expected O, but got Unknown
		//IL_0af1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0afb: Expected O, but got Unknown
		//IL_0b07: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b11: Expected O, but got Unknown
		//IL_0b1d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b27: Expected O, but got Unknown
		//IL_0b33: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b3d: Expected O, but got Unknown
		//IL_0b49: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b53: Expected O, but got Unknown
		//IL_0b5f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b69: Expected O, but got Unknown
		//IL_0b75: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b7f: Expected O, but got Unknown
		//IL_0b8b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b95: Expected O, but got Unknown
		//IL_0ba1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bab: Expected O, but got Unknown
		//IL_0bb7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bc1: Expected O, but got Unknown
		//IL_0bcd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bd7: Expected O, but got Unknown
		//IL_0be3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bed: Expected O, but got Unknown
		//IL_0bf9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c03: Expected O, but got Unknown
		//IL_0c0f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c19: Expected O, but got Unknown
		//IL_0c25: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c2f: Expected O, but got Unknown
		//IL_0c3b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c45: Expected O, but got Unknown
		//IL_0c51: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c5b: Expected O, but got Unknown
		//IL_0c67: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c71: Expected O, but got Unknown
		//IL_0c7d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c87: Expected O, but got Unknown
		//IL_0c93: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c9d: Expected O, but got Unknown
		//IL_0ca9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cb3: Expected O, but got Unknown
		//IL_0cbf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cc9: Expected O, but got Unknown
		//IL_0cd5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cdf: Expected O, but got Unknown
		//IL_0ceb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cf5: Expected O, but got Unknown
		//IL_0d01: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d0b: Expected O, but got Unknown
		//IL_0d17: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d21: Expected O, but got Unknown
		//IL_0d2d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d37: Expected O, but got Unknown
		//IL_0d43: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d4d: Expected O, but got Unknown
		//IL_0d59: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d63: Expected O, but got Unknown
		//IL_0d6f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d79: Expected O, but got Unknown
		//IL_0d85: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d8f: Expected O, but got Unknown
		//IL_0d9b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0da5: Expected O, but got Unknown
		//IL_0db1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dbb: Expected O, but got Unknown
		//IL_0dd3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ddd: Expected O, but got Unknown
		//IL_0de9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0df3: Expected O, but got Unknown
		//IL_0dff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e09: Expected O, but got Unknown
		//IL_0e15: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e1f: Expected O, but got Unknown
		//IL_0e59: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e63: Expected O, but got Unknown
		//IL_0e86: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e90: Expected O, but got Unknown
		//IL_0eb3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ebd: Expected O, but got Unknown
		//IL_0ee0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0eea: Expected O, but got Unknown
		//IL_0f0d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f17: Expected O, but got Unknown
		//IL_0f3a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f44: Expected O, but got Unknown
		//IL_0f67: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f71: Expected O, but got Unknown
		//IL_0f7d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f87: Expected O, but got Unknown
		//IL_0f93: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f9d: Expected O, but got Unknown
		//IL_0fa9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fb3: Expected O, but got Unknown
		//IL_0fbf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fc9: Expected O, but got Unknown
		//IL_0fd5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fdf: Expected O, but got Unknown
		//IL_0feb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ff5: Expected O, but got Unknown
		//IL_1001: Unknown result type (might be due to invalid IL or missing references)
		//IL_100b: Expected O, but got Unknown
		//IL_1017: Unknown result type (might be due to invalid IL or missing references)
		//IL_1021: Expected O, but got Unknown
		//IL_102d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1037: Expected O, but got Unknown
		//IL_1043: Unknown result type (might be due to invalid IL or missing references)
		//IL_104d: Expected O, but got Unknown
		//IL_1059: Unknown result type (might be due to invalid IL or missing references)
		//IL_1063: Expected O, but got Unknown
		//IL_106f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1079: Expected O, but got Unknown
		//IL_1085: Unknown result type (might be due to invalid IL or missing references)
		//IL_108f: Expected O, but got Unknown
		//IL_109b: Unknown result type (might be due to invalid IL or missing references)
		//IL_10a5: Expected O, but got Unknown
		//IL_10b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_10bb: Expected O, but got Unknown
		//IL_10c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_10d1: Expected O, but got Unknown
		//IL_10dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_10e7: Expected O, but got Unknown
		//IL_10f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_10fd: Expected O, but got Unknown
		//IL_1109: Unknown result type (might be due to invalid IL or missing references)
		//IL_1113: Expected O, but got Unknown
		//IL_111f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1129: Expected O, but got Unknown
		//IL_1135: Unknown result type (might be due to invalid IL or missing references)
		//IL_113f: Expected O, but got Unknown
		//IL_114b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1155: Expected O, but got Unknown
		//IL_1161: Unknown result type (might be due to invalid IL or missing references)
		//IL_116b: Expected O, but got Unknown
		//IL_1177: Unknown result type (might be due to invalid IL or missing references)
		//IL_1181: Expected O, but got Unknown
		//IL_118d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1197: Expected O, but got Unknown
		//IL_11a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_11ad: Expected O, but got Unknown
		//IL_11b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_11c3: Expected O, but got Unknown
		//IL_11cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_11d9: Expected O, but got Unknown
		//IL_11e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_11ef: Expected O, but got Unknown
		//IL_11fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_1205: Expected O, but got Unknown
		//IL_1211: Unknown result type (might be due to invalid IL or missing references)
		//IL_121b: Expected O, but got Unknown
		//IL_1227: Unknown result type (might be due to invalid IL or missing references)
		//IL_1231: Expected O, but got Unknown
		//IL_123d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1247: Expected O, but got Unknown
		//IL_1253: Unknown result type (might be due to invalid IL or missing references)
		//IL_125d: Expected O, but got Unknown
		//IL_1269: Unknown result type (might be due to invalid IL or missing references)
		//IL_1273: Expected O, but got Unknown
		//IL_127f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1289: Expected O, but got Unknown
		//IL_1295: Unknown result type (might be due to invalid IL or missing references)
		//IL_129f: Expected O, but got Unknown
		//IL_12ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_12b5: Expected O, but got Unknown
		//IL_12c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_12cb: Expected O, but got Unknown
		//IL_12d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_12e1: Expected O, but got Unknown
		//IL_12ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_12f7: Expected O, but got Unknown
		//IL_1303: Unknown result type (might be due to invalid IL or missing references)
		//IL_130d: Expected O, but got Unknown
		//IL_1319: Unknown result type (might be due to invalid IL or missing references)
		//IL_1323: Expected O, but got Unknown
		//IL_132f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1339: Expected O, but got Unknown
		//IL_1345: Unknown result type (might be due to invalid IL or missing references)
		//IL_134f: Expected O, but got Unknown
		//IL_135b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1365: Expected O, but got Unknown
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
		//IL_15d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_15e3: Expected O, but got Unknown
		//IL_15ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_15f9: Expected O, but got Unknown
		//IL_1605: Unknown result type (might be due to invalid IL or missing references)
		//IL_160f: Expected O, but got Unknown
		//IL_161b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1625: Expected O, but got Unknown
		//IL_1631: Unknown result type (might be due to invalid IL or missing references)
		//IL_163b: Expected O, but got Unknown
		//IL_1647: Unknown result type (might be due to invalid IL or missing references)
		//IL_1651: Expected O, but got Unknown
		//IL_165d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1667: Expected O, but got Unknown
		//IL_1673: Unknown result type (might be due to invalid IL or missing references)
		//IL_167d: Expected O, but got Unknown
		//IL_1689: Unknown result type (might be due to invalid IL or missing references)
		//IL_1693: Expected O, but got Unknown
		//IL_169f: Unknown result type (might be due to invalid IL or missing references)
		//IL_16a9: Expected O, but got Unknown
		//IL_16b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_16bf: Expected O, but got Unknown
		//IL_16cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_16d5: Expected O, but got Unknown
		//IL_16e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_16eb: Expected O, but got Unknown
		//IL_16f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_1701: Expected O, but got Unknown
		//IL_170d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1717: Expected O, but got Unknown
		//IL_1723: Unknown result type (might be due to invalid IL or missing references)
		//IL_172d: Expected O, but got Unknown
		//IL_1739: Unknown result type (might be due to invalid IL or missing references)
		//IL_1743: Expected O, but got Unknown
		//IL_174f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1759: Expected O, but got Unknown
		//IL_1765: Unknown result type (might be due to invalid IL or missing references)
		//IL_176f: Expected O, but got Unknown
		//IL_177b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1785: Expected O, but got Unknown
		//IL_1791: Unknown result type (might be due to invalid IL or missing references)
		//IL_179b: Expected O, but got Unknown
		//IL_17b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_17ba: Expected O, but got Unknown
		//IL_17cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_17d9: Expected O, but got Unknown
		//IL_17ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_17f8: Expected O, but got Unknown
		//IL_180d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1817: Expected O, but got Unknown
		//IL_182c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1836: Expected O, but got Unknown
		//IL_1842: Unknown result type (might be due to invalid IL or missing references)
		//IL_184c: Expected O, but got Unknown
		//IL_1858: Unknown result type (might be due to invalid IL or missing references)
		//IL_1862: Expected O, but got Unknown
		//IL_186e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1878: Expected O, but got Unknown
		//IL_1884: Unknown result type (might be due to invalid IL or missing references)
		//IL_188e: Expected O, but got Unknown
		//IL_1a75: Unknown result type (might be due to invalid IL or missing references)
		//IL_1af2: Unknown result type (might be due to invalid IL or missing references)
		//IL_1ace: Unknown result type (might be due to invalid IL or missing references)
		InitializeComponent();
		MainViewModel.Instance.HUDBuildingPanel = this;
		RefRecruitArcherButton = (ToggleButton)((FrameworkElement)this).FindName("BarracksArcher");
		RefRecruitSpearmanButton = (ToggleButton)((FrameworkElement)this).FindName("BarracksSpearman");
		RefRecruitMacemanButton = (ToggleButton)((FrameworkElement)this).FindName("BarracksMaceman");
		RefRecruitXBowmanButton = (ToggleButton)((FrameworkElement)this).FindName("BarracksXBowman");
		RefRecruitPikemanButton = (ToggleButton)((FrameworkElement)this).FindName("BarracksPikeman");
		RefRecruitSwordsmanButton = (ToggleButton)((FrameworkElement)this).FindName("BarracksSwordsman");
		RefRecruitKnightButton = (ToggleButton)((FrameworkElement)this).FindName("BarracksKnight");
		RefRecruitArabBowButton = (ToggleButton)((FrameworkElement)this).FindName("MercArabBow");
		RefRecruitArabSlaveButton = (ToggleButton)((FrameworkElement)this).FindName("MercArabSlave");
		RefRecruitArabSlingerButton = (ToggleButton)((FrameworkElement)this).FindName("MercArabSlinger");
		RefRecruitArabAssassinButton = (ToggleButton)((FrameworkElement)this).FindName("MercArabAssassin");
		RefRecruitArabHorseArcherButton = (ToggleButton)((FrameworkElement)this).FindName("MercArabHorseArcher");
		RefRecruitArabSwordsmanButton = (ToggleButton)((FrameworkElement)this).FindName("MercArabSwordsman");
		RefRecruitArabGrenadierButton = (ToggleButton)((FrameworkElement)this).FindName("MercArabGrenadier");
		RefRecruitBedouinCamelLancerButton = (ToggleButton)((FrameworkElement)this).FindName("BedouinCamelLancer");
		RefRecruitBedouinHealerButton = (ToggleButton)((FrameworkElement)this).FindName("BedouinHealer");
		RefRecruitBedouinEunuchButton = (ToggleButton)((FrameworkElement)this).FindName("BedouinEunuch");
		RefRecruitBedouinAmbusherButton = (ToggleButton)((FrameworkElement)this).FindName("BedouinAmbusher");
		RefRecruitBedouinSkirmisherButton = (ToggleButton)((FrameworkElement)this).FindName("BedouinSkirmisher");
		RefRecruitBedouinHeavyCamelButton = (ToggleButton)((FrameworkElement)this).FindName("BedouinHeavyCamel");
		RefRecruitBedouinSapperButton = (ToggleButton)((FrameworkElement)this).FindName("BedouinSapper");
		RefRecruitBedouinDemolisherButton = (ToggleButton)((FrameworkElement)this).FindName("BedouinDemolisher");
		RefRecruitEngineerButton = (Button)((FrameworkElement)this).FindName("BarracksEngineer");
		RefRecruitEngineerButtonX = (Button)((FrameworkElement)this).FindName("BarracksEngineerX");
		RefRecruitLaddermanButton = (Button)((FrameworkElement)this).FindName("BarracksLadderman");
		RefRecruitLaddermanButtonX = (Button)((FrameworkElement)this).FindName("BarracksLaddermanX");
		RefRecruitTunellerButton = (Button)((FrameworkElement)this).FindName("BarracksTuneller");
		RefRecruitMonkButton = (Button)((FrameworkElement)this).FindName("BarracksMonk");
		RefButtonReleaseDogs = (Button)((FrameworkElement)this).FindName("ButtonReleaseDogs");
		RefHUDBuildingFullClickMask = (Grid)((FrameworkElement)this).FindName("HUDBuildingFullClickMask");
		RefBuildingPanel = (Grid)((FrameworkElement)this).FindName("BuildingPanel");
		RefBarracksPanel = (Grid)((FrameworkElement)this).FindName("BarracksPanel");
		RefMercPostPanel = (Grid)((FrameworkElement)this).FindName("MercPostPanel");
		RefBedouinStockadePanel = (Grid)((FrameworkElement)this).FindName("BedouinStockadePanel");
		RefWorkerPanel = (Grid)((FrameworkElement)this).FindName("WorkerPanel");
		RefKeepPanel = (Grid)((FrameworkElement)this).FindName("KeepPanel");
		RefOutpostPanel = (Grid)((FrameworkElement)this).FindName("OutpostPanel");
		RefOutpostArabPanel = (Grid)((FrameworkElement)this).FindName("OutpostArabPanel");
		RefOutpostBedouinPanel = (Grid)((FrameworkElement)this).FindName("OutpostBedouinPanel");
		RefGranaryPanel = (Grid)((FrameworkElement)this).FindName("GranaryPanel");
		RefArmouryPanel = (Grid)((FrameworkElement)this).FindName("ArmouryPanel");
		RefStockpilePanel = (Grid)((FrameworkElement)this).FindName("StockpilePanel");
		RefInnPanel = (Grid)((FrameworkElement)this).FindName("InnPanel");
		RefFletchersPanel = (Grid)((FrameworkElement)this).FindName("FletchersPanel");
		RefPoleturnersPanel = (Grid)((FrameworkElement)this).FindName("PoleturnersPanel");
		RefBlacksmithsPanel = (Grid)((FrameworkElement)this).FindName("BlacksmithsPanel");
		RefChurchPanel = (Grid)((FrameworkElement)this).FindName("ChurchPanel");
		RefTradepostPanel = (Grid)((FrameworkElement)this).FindName("TradepostPanel");
		RefTradingFoodPanel = (Grid)((FrameworkElement)this).FindName("TradingFoodPanel");
		RefTradingResourcesPanel = (Grid)((FrameworkElement)this).FindName("TradingResourcesPanel");
		RefTradingWeaponsPanel = (Grid)((FrameworkElement)this).FindName("TradingWeaponsPanel");
		RefTradingPricesPanel = (Grid)((FrameworkElement)this).FindName("TradingPricesPanel");
		RefTradingTradePanel = (Grid)((FrameworkElement)this).FindName("TradingTradePanel");
		RefTradeBuyButton = (Button)((FrameworkElement)this).FindName("TradeBuy");
		RefTradeSellButton = (Button)((FrameworkElement)this).FindName("TradeSell");
		RefTradeErrorAnination = (Storyboard)((FrameworkElement)this).TryFindResource((object)"TradeErrorFadeOut");
		RefTradePost_Trade_Normal = (Grid)((FrameworkElement)this).FindName("TradePost_Trade_Normal");
		RefTradePost_Trade_Auto = (Grid)((FrameworkElement)this).FindName("TradePost_Trade_Auto");
		((UIElement)RefTradePost_Trade_Auto).Visibility = (Visibility)1;
		RefTrade_AutoToggle = (Button)((FrameworkElement)this).FindName("Trade_AutoToggle");
		RefTrade_GoTo_Auto = (Button)((FrameworkElement)this).FindName("Trade_GoTo_Auto");
		RefAutotrade_Sell_Slider = (Slider)((FrameworkElement)this).FindName("Autotrade_Sell_Slider");
		RefAutotrade_Buy_Slider = (Slider)((FrameworkElement)this).FindName("Autotrade_Buy_Slider");
		((RangeBase)RefAutotrade_Sell_Slider).ValueChanged += Autotrade_Sell_Slider_ValueChanged;
		((RangeBase)RefAutotrade_Buy_Slider).ValueChanged += Autotrade_Buy_Slider_ValueChanged;
		RefOutpostSizeSlider = (Slider)((FrameworkElement)this).FindName("OutpostSize");
		((RangeBase)RefOutpostSizeSlider).ValueChanged += OutpostSizeSlider_ValueChanged;
		RefOutpostDelaySlider = (Slider)((FrameworkElement)this).FindName("OutpostDelay");
		((RangeBase)RefOutpostDelaySlider).ValueChanged += OutpostDelaySlider_ValueChanged;
		RefOutpostArabSizeSlider = (Slider)((FrameworkElement)this).FindName("OutpostArabSize");
		((RangeBase)RefOutpostArabSizeSlider).ValueChanged += OutpostArabSizeSlider_ValueChanged;
		RefOutpostArabDelaySlider = (Slider)((FrameworkElement)this).FindName("OutpostArabDelay");
		((RangeBase)RefOutpostArabDelaySlider).ValueChanged += OutpostArabDelaySlider_ValueChanged;
		RefOutpostBedouinSizeSlider = (Slider)((FrameworkElement)this).FindName("OutpostBedouinSize");
		((RangeBase)RefOutpostBedouinSizeSlider).ValueChanged += OutpostBedouinSizeSlider_ValueChanged;
		RefOutpostBedouinDelaySlider = (Slider)((FrameworkElement)this).FindName("OutpostBedouinDelay");
		((RangeBase)RefOutpostBedouinDelaySlider).ValueChanged += OutpostBedouinDelaySlider_ValueChanged;
		RefReportsPanel = (Grid)((FrameworkElement)this).FindName("ReportsPanel");
		RefReportsPopularityPanel = (Grid)((FrameworkElement)this).FindName("ReportsPopularityPanel");
		RefReportsPopEventsPanel = (Grid)((FrameworkElement)this).FindName("ReportsPopEventsPanel");
		RefShowEventsButton = (Grid)((FrameworkElement)this).FindName("ShowEventsPanelControl");
		RefReportsFearFactorPanel = (Grid)((FrameworkElement)this).FindName("ReportsFearFactorPanel");
		RefReportsPopulationPanel = (Grid)((FrameworkElement)this).FindName("ReportsPopulationPanel");
		RefReportsArmy1Panel = (Grid)((FrameworkElement)this).FindName("ReportsArmy1Panel");
		RefReportsArmy2Panel = (Grid)((FrameworkElement)this).FindName("ReportsArmy2Panel");
		RefReportsArmy3Panel = (Grid)((FrameworkElement)this).FindName("ReportsArmy3Panel");
		RefReportsArmy4Panel = (Grid)((FrameworkElement)this).FindName("ReportsArmy4Panel");
		RefReportsStoresPanel = (Grid)((FrameworkElement)this).FindName("ReportsStoresPanel");
		RefReportsWeaponsPanel = (Grid)((FrameworkElement)this).FindName("ReportsWeaponsPanel");
		RefReportsReligionPanel = (Grid)((FrameworkElement)this).FindName("ReportsReligionPanel");
		RefWGT_PopReportAleText = (TextBlock)((FrameworkElement)this).FindName("WGT_PopReportAleText");
		RefKeepTaxRate = (TextBlock)((FrameworkElement)this).FindName("KeepTaxRate");
		RefChimpPanel = (Grid)((FrameworkElement)this).FindName("ChimpPanel");
		RefShowGatePanel = (Grid)((FrameworkElement)this).FindName("ShowGate");
		RefShowDrawbridgePanel = (Grid)((FrameworkElement)this).FindName("ShowDrawbridge");
		RefShowEngineersGuildPanel = (Grid)((FrameworkElement)this).FindName("EngineersGuildPanel");
		RefShowTunellersGuildPanel = (Grid)((FrameworkElement)this).FindName("TunellersGuildPanel");
		RefShowCathedralPanel = (Grid)((FrameworkElement)this).FindName("CathedralPanel");
		RefShowDogsPanel = (Grid)((FrameworkElement)this).FindName("ShowDogs");
		RefReportsFoodPanel = (Grid)((FrameworkElement)this).FindName("ReportsFoodPanel");
		RefShowWorkersPanel = (Grid)((FrameworkElement)this).FindName("ShowWorkersPanel");
		RefShowInfoPanel = (Grid)((FrameworkElement)this).FindName("ShowInfoPanel");
		RefShowRepairPanel = (Grid)((FrameworkElement)this).FindName("ShowRepairPanel");
		RefHelpButton = (Button)((FrameworkElement)this).FindName("BuildingHelpButton");
		RefButtonRepair = (Button)((FrameworkElement)this).FindName("ButtonRepair");
		RefTroopCostsText = (TextBlock)((FrameworkElement)this).FindName("TroopCostsText");
		RefTroopNameText = (TextBlock)((FrameworkElement)this).FindName("TroopNameText");
		RefTroopHelpText = (TextBlock)((FrameworkElement)this).FindName("TroopHelpText");
		RefArabTroopCostsText = (TextBlock)((FrameworkElement)this).FindName("ArabTroopCostsText");
		RefArabTroopNameText = (TextBlock)((FrameworkElement)this).FindName("ArabTroopNameText");
		RefArabTroopHelpText = (TextBlock)((FrameworkElement)this).FindName("ArabTroopHelpText");
		RefBedouinTroopCostsText = (TextBlock)((FrameworkElement)this).FindName("BedouinTroopCostsText");
		RefBedouinTroopNameText = (TextBlock)((FrameworkElement)this).FindName("BedouinTroopNameText");
		RefBedouinTroopHelpText = (TextBlock)((FrameworkElement)this).FindName("BedouinTroopHelpText");
		RefEngineersCostsText = (TextBlock)((FrameworkElement)this).FindName("EngineersCostsText");
		RefEngineersHelpText = (TextBlock)((FrameworkElement)this).FindName("EngineersHelpText");
		RefTunellersCostsText = (TextBlock)((FrameworkElement)this).FindName("TunellersCostsText");
		RefTunellersHelpText = (TextBlock)((FrameworkElement)this).FindName("TunellersHelpText");
		RefDogsReleasedText = (TextBlock)((FrameworkElement)this).FindName("DogsReleasedText");
		RefMonkCostsText = (TextBlock)((FrameworkElement)this).FindName("MonkCostsText");
		RefMonkHelpText = (TextBlock)((FrameworkElement)this).FindName("MonkHelpText");
		RefEngineersGuildNoGoldMessage = (TextBlock)((FrameworkElement)this).FindName("EngineersGuildNoGoldMessage");
		RefTunnllersGuildNoGoldMessage = (TextBlock)((FrameworkElement)this).FindName("TunnllersGuildNoGoldMessage");
		RefCathedralNoGoldMessage = (TextBlock)((FrameworkElement)this).FindName("CathedralNoGoldMessage");
		RefWGTFoodTypePop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_FoodTypePop");
		RefWGTRationsPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_RationsPop");
		RefWGTInnPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_InnPop");
		RefWGTTaxPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_TaxPop");
		RefWGTPopReportFoodPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportFood");
		RefWGTPopReportTaxPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportTax");
		RefWGTPopReportCrowdingPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportCrowding");
		RefWGTPopReportFearFactorPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportFearFactor");
		RefWGTPopReportReligionPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportReligion");
		RefWGTPopReportAlePop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportAle");
		RefWGTPopReportEventsPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportEvents");
		RefWGTPopReportTotalPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportTotal");
		RefWGTPopReportTotal2Pop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportTotal2");
		RefWGTPopReportFairsPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportFairs");
		RefWGTPopReportMarriagePop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportMarriage");
		RefWGTPopReportJesterPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportJester");
		RefWGTPopReportPlaguePop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportPlague");
		RefWGTPopReportWolvesPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportWolves");
		RefWGTPopReportBanditsPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportBandits");
		RefWGTPopReportFirePop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_PopReportFire");
		RefWGTFFReportFearFactorPop = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_FFReportFearFactor");
		RefWGTRelReport = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_RelReport");
		RefWGTRelReport2 = (WGT_Popularity)((FrameworkElement)this).FindName("WGT_RelReport2");
		RefRationHandNone = (Image)((FrameworkElement)this).FindName("RationHandNone");
		RefRationHandHalf = (Image)((FrameworkElement)this).FindName("RationHandHalf");
		RefRationHandFull = (Image)((FrameworkElement)this).FindName("RationHandFull");
		RefRationHandExtra = (Image)((FrameworkElement)this).FindName("RationHandExtra");
		RefRationHandDouble = (Image)((FrameworkElement)this).FindName("RationHandDouble");
		RefStopMeatConsumption = (Image)((FrameworkElement)this).FindName("StopMeatConsumption");
		RefStopCheeseConsumption = (Image)((FrameworkElement)this).FindName("StopCheeseConsumption");
		RefStopBreadConsumption = (Image)((FrameworkElement)this).FindName("StopBreadConsumption");
		RefStopApplesConsumption = (Image)((FrameworkElement)this).FindName("StopApplesConsumption");
		RefButtonOpenGate = (Button)((FrameworkElement)this).FindName("ButtonOpenGate");
		RefButtonCloseGate = (Button)((FrameworkElement)this).FindName("ButtonCloseGate");
		RefButtonOpenBridge = (Button)((FrameworkElement)this).FindName("ButtonOpenBridge");
		RefButtonCloseBridge = (Button)((FrameworkElement)this).FindName("ButtonCloseBridge");
		RefProducingBows = (RadioButton)((FrameworkElement)this).FindName("ProducingBows");
		RefProducingXBows = (RadioButton)((FrameworkElement)this).FindName("ProducingXBows");
		RefProducingSpears = (RadioButton)((FrameworkElement)this).FindName("ProducingSpears");
		RefProducingPikes = (RadioButton)((FrameworkElement)this).FindName("ProducingPikes");
		RefProducingSwords = (RadioButton)((FrameworkElement)this).FindName("ProducingSwords");
		RefProducingMaces = (RadioButton)((FrameworkElement)this).FindName("ProducingMaces");
		RefRelReportPopEffectLabel = (TextBlock)((FrameworkElement)this).FindName("RelReportPopEffectLabel");
		RefWGT_RelReportLabel = (TextBlock)((FrameworkElement)this).FindName("WGT_RelReportLabel");
		RefBuildingZZZButtonOn = (Button)((FrameworkElement)this).FindName("BuildingZZZButtonOn");
		RefBuildingZZZButtonOff = (Button)((FrameworkElement)this).FindName("BuildingZZZButtonOff");
		RefButtonArmyReportBack = (Button)((FrameworkElement)this).FindName("ButtonArmyReportBack");
		RefButtonArmyReportBack2 = (Button)((FrameworkElement)this).FindName("ButtonArmyReportBack2");
		RefButtonArmyReportBack3 = (Button)((FrameworkElement)this).FindName("ButtonArmyReportBack3");
		MainViewModel.Instance.HUDmain.RefTutorialArrow3 = (Image)((FrameworkElement)this).FindName("TutorialArrow3");
		MainViewModel.Instance.HUDmain.RefTutorialArrow4 = (Image)((FrameworkElement)this).FindName("TutorialArrow4");
		MainViewModel.Instance.HUDmain.RefTutorialArrow6 = (Image)((FrameworkElement)this).FindName("TutorialArrow6");
		MainViewModel.Instance.HUDmain.RefTutorialArrow19 = (Image)((FrameworkElement)this).FindName("TutorialArrow19");
		MainViewModel.Instance.HUDmain.RefTutorialArrow21 = (Image)((FrameworkElement)this).FindName("TutorialArrow21");
		RefReportsReligionPanelSubArea = (Grid)((FrameworkElement)this).FindName("ReportsReligionPanelSubArea");
		RefAutotrade_Buy_Text = (TextBlock)((FrameworkElement)this).FindName("Autotrade_Buy_Text");
		RefAutotrade_Sell_Text = (TextBlock)((FrameworkElement)this).FindName("Autotrade_Sell_Text");
		RefTradeErrorMessage = (TextBlock)((FrameworkElement)this).FindName("TradeErrorMessage");
		if (FatControler.thai)
		{
			RefWGT_PopReportAleText.FontSize = 14f;
		}
		if (FatControler.french || FatControler.czech)
		{
			RefAutotrade_Buy_Text.FontSize = 18f;
			RefAutotrade_Sell_Text.FontSize = 18f;
		}
		if (FatControler.dutch)
		{
			RefAutotrade_Buy_Text.FontSize = 17f;
			RefAutotrade_Sell_Text.FontSize = 17f;
		}
		if (FatControler.polish)
		{
			RefAutotrade_Buy_Text.FontSize = 16f;
			RefAutotrade_Sell_Text.FontSize = 16f;
		}
		if (FatControler.italian)
		{
			RefTradeErrorMessage.FontSize = 16f;
			MainViewModel.Instance.BuySellFontSize = "20";
		}
		if (FatControler.spanish)
		{
			MainViewModel.Instance.BuySellFontSize = "19";
		}
		if (FatControler.portuguese)
		{
			MainViewModel.Instance.BuySellFontSize = "19";
			RefAutotrade_Buy_Text.FontSize = 16f;
			RefAutotrade_Sell_Text.FontSize = 16f;
		}
		if (FatControler.german)
		{
			MainViewModel.Instance.BuySellFontSize = "19";
			((FrameworkElement)RefTradeBuyButton).Width = 226f;
			((FrameworkElement)RefTradeSellButton).Width = 226f;
		}
		if (FatControler.russian)
		{
			MainViewModel.Instance.BuySellFontSize = "20";
			RefKeepTaxRate.FontSize = 19f;
		}
		if (FatControler.turkish)
		{
			RefTradeErrorMessage.FontSize = 14f;
			MainViewModel.Instance.BuySellFontSize = "20";
		}
		if (FatControler.hungarian)
		{
			RefAutotrade_Buy_Text.FontSize = 18f;
			RefAutotrade_Sell_Text.FontSize = 18f;
			MainViewModel.Instance.BuySellFontSize = "20";
			RefKeepTaxRate.FontSize = 18f;
			((FrameworkElement)RefKeepTaxRate).Margin = new Thickness(-60f, 0f, 0f, 4f);
		}
		if (FatControler.ukrainian)
		{
			RefTradeErrorMessage.FontSize = 16f;
		}
		if (FatControler.arabic)
		{
			RefTradeErrorMessage.FontSize = 16f;
			if (ConfigSettings.Settings_ArabicL2R)
			{
				((FrameworkElement)RefReportsReligionPanelSubArea).Margin = new Thickness(180f, 40f, 340f, 0f);
			}
			((FrameworkElement)RefTradeErrorMessage).Margin = new Thickness(0f, 0f, 208f, 0f);
		}
	}

	public void initAutoTrade(int goods = -1)
	{
		sliderSetup = true;
		int num = GameData.Instance.lastGameState.trading_current_goods;
		if (goods != -1)
		{
			num = goods;
		}
		int num2 = GameData.Instance.lastGameState.autotrade_buy_amount[num];
		int num3 = GameData.Instance.lastGameState.autotrade_sell_amount[num];
		currentAutoTradeOn = GameData.Instance.lastGameState.autotrade_onoff[num] > 0;
		EngineInterface.GameAction(Enums.GameActionCommand.Autotrade_OnOff, num, GameData.Instance.lastGameState.autotrade_onoff[num]);
		EngineInterface.GameAction(Enums.GameActionCommand.Autotrade_SetSell, -1, num3);
		EngineInterface.GameAction(Enums.GameActionCommand.Autotrade_SetBuy, -1, num2);
		MainViewModel.Instance.TradeAutoBuy = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 143) + " < " + num2;
		MainViewModel.Instance.TradeAutoSell = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 144) + " > " + num3;
		UpdateAutoTradeButton(currentAutoTradeOn);
		int[] sliderCurve = HUD_Scenario.SliderCurve1000;
		for (int i = 0; i <= 100; i++)
		{
			if (num2 <= sliderCurve[i])
			{
				((RangeBase)RefAutotrade_Buy_Slider).Value = i;
				break;
			}
		}
		for (int j = 0; j <= 100; j++)
		{
			if (num3 <= sliderCurve[j])
			{
				((RangeBase)RefAutotrade_Sell_Slider).Value = j;
				break;
			}
		}
		sliderSetup = false;
	}

	public void toggleAutoTrade()
	{
		currentAutoTradeOn = !currentAutoTradeOn;
		if (currentAutoTradeOn)
		{
			EngineInterface.GameAction(Enums.GameActionCommand.Autotrade_OnOff, -1, 1);
		}
		else
		{
			EngineInterface.GameAction(Enums.GameActionCommand.Autotrade_OnOff, -1, 0);
		}
		UpdateAutoTradeButton(currentAutoTradeOn);
	}

	public void UpdateAutoTradeButton(bool tradingOn)
	{
		if (tradingOn)
		{
			PropEx.SetSprite1((UIElement)(object)RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[269]);
			PropEx.SetSprite2((UIElement)(object)RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[270]);
			PropEx.SetSprite3((UIElement)(object)RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[270]);
			PropEx.SetSprite4((UIElement)(object)RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[270]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[271]);
			PropEx.SetSprite2((UIElement)(object)RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[272]);
			PropEx.SetSprite3((UIElement)(object)RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[272]);
			PropEx.SetSprite4((UIElement)(object)RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[272]);
		}
	}

	public void UpdateAutoTradeSelectButton(bool lastState = false)
	{
		if ((!lastState) ? (GameData.Instance.lastGameState.autotrade_onoff[GameData.Instance.lastGameState.trading_current_goods] > 0) : currentAutoTradeOn)
		{
			PropEx.SetSprite1((UIElement)(object)RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[273]);
			PropEx.SetSprite2((UIElement)(object)RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[274]);
			PropEx.SetSprite3((UIElement)(object)RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[274]);
			PropEx.SetSprite4((UIElement)(object)RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[274]);
		}
		else
		{
			PropEx.SetSprite1((UIElement)(object)RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[275]);
			PropEx.SetSprite2((UIElement)(object)RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[276]);
			PropEx.SetSprite3((UIElement)(object)RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[276]);
			PropEx.SetSprite4((UIElement)(object)RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[276]);
		}
	}

	public void Autotrade_Sell_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (FatControler.currentScene == (Enums.SceneIDS)0)
		{
			return;
		}
		int num = (int)((RangeBase)RefAutotrade_Sell_Slider).Value;
		if (!insideValueChanged)
		{
			insideValueChanged = true;
			if (num <= (int)((RangeBase)RefAutotrade_Buy_Slider).Value)
			{
				((RangeBase)RefAutotrade_Buy_Slider).Value = num - 1;
			}
			insideValueChanged = false;
		}
		MainViewModel.Instance.TradeAutoSell = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 144) + " > " + HUD_Scenario.SliderCurve1000[num];
		EngineInterface.GameAction(Enums.GameActionCommand.Autotrade_SetSell, 0, HUD_Scenario.SliderCurve1000[num]);
		if (!sliderSetup && !currentAutoTradeOn)
		{
			toggleAutoTrade();
		}
	}

	public void Autotrade_Buy_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (FatControler.currentScene == (Enums.SceneIDS)0)
		{
			return;
		}
		int num = (int)((RangeBase)RefAutotrade_Buy_Slider).Value;
		if (!insideValueChanged)
		{
			insideValueChanged = true;
			if (num >= (int)((RangeBase)RefAutotrade_Sell_Slider).Value)
			{
				((RangeBase)RefAutotrade_Sell_Slider).Value = num + 1;
			}
			insideValueChanged = false;
		}
		MainViewModel.Instance.TradeAutoBuy = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 143) + " < " + HUD_Scenario.SliderCurve1000[num];
		EngineInterface.GameAction(Enums.GameActionCommand.Autotrade_SetBuy, 0, HUD_Scenario.SliderCurve1000[num]);
		if (!sliderSetup && !currentAutoTradeOn)
		{
			toggleAutoTrade();
		}
	}

	public void OutpostSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (!inSetup)
		{
			int state = (int)((RangeBase)RefOutpostSizeSlider).Value;
			EngineInterface.GameAction(Enums.GameActionCommand.SetOutpostSize, GameData.Instance.lastGameState.in_structure, state);
		}
	}

	public void OutpostDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (!inSetup)
		{
			int state = (int)((RangeBase)RefOutpostDelaySlider).Value;
			EngineInterface.GameAction(Enums.GameActionCommand.SetOutpostDelay, GameData.Instance.lastGameState.in_structure, state);
		}
	}

	public void OutpostArabSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (!inSetup)
		{
			int state = (int)((RangeBase)RefOutpostArabSizeSlider).Value;
			EngineInterface.GameAction(Enums.GameActionCommand.SetOutpostSize, GameData.Instance.lastGameState.in_structure, state);
		}
	}

	public void OutpostArabDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (!inSetup)
		{
			int state = (int)((RangeBase)RefOutpostArabDelaySlider).Value;
			EngineInterface.GameAction(Enums.GameActionCommand.SetOutpostDelay, GameData.Instance.lastGameState.in_structure, state);
		}
	}

	public void OutpostBedouinSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (!inSetup)
		{
			int state = (int)((RangeBase)RefOutpostBedouinSizeSlider).Value;
			EngineInterface.GameAction(Enums.GameActionCommand.SetOutpostSize, GameData.Instance.lastGameState.in_structure, state);
		}
	}

	public void OutpostBedouinDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (!inSetup)
		{
			int state = (int)((RangeBase)RefOutpostBedouinDelaySlider).Value;
			EngineInterface.GameAction(Enums.GameActionCommand.SetOutpostDelay, GameData.Instance.lastGameState.in_structure, state);
		}
	}

	public void InitializeComponent()
	{
		GUI.LoadComponent((object)this, "Assets/GUI/XAMLResources/HUD_Buildings.xaml");
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
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Expected O, but got Unknown
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Expected O, but got Unknown
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Expected O, but got Unknown
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
		if (eventName == "MouseEnter" && handlerName == "MouseEnterBuildingIconHandler")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseEnter += new MouseEventHandler(MouseEnterBuildingIconHandler);
			}
			else if (source is RadioButton)
			{
				((UIElement)(RadioButton)source).MouseEnter += new MouseEventHandler(MouseEnterBuildingIconHandler);
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "MouseLeaveBuildingIconHandler")
		{
			if (source is Button)
			{
				((UIElement)(Button)source).MouseLeave += new MouseEventHandler(MouseLeaveBuildingIconHandler);
			}
			else if (source is RadioButton)
			{
				((UIElement)(RadioButton)source).MouseLeave += new MouseEventHandler(MouseLeaveBuildingIconHandler);
			}
			return true;
		}
		return false;
	}

	public void MouseEnterBuildingIconHandler(object sender, MouseEventArgs e)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		if (((RoutedEventArgs)e).Source is Button)
		{
			if (((FrameworkElement)(Button)((RoutedEventArgs)e).Source).Tag != null && ((FrameworkElement)(Button)((RoutedEventArgs)e).Source).Tag is string)
			{
				MainViewModel.Instance.HUDmain.HoverString = (string)((FrameworkElement)(Button)((RoutedEventArgs)e).Source).Tag;
			}
			else
			{
				MainViewModel.Instance.HUDmain.HoverString = "";
			}
		}
	}

	public void MouseLeaveBuildingIconHandler(object sender, MouseEventArgs e)
	{
		MainViewModel.Instance.HUDmain.HoverString = "";
	}

	public void OpenFletchers()
	{
		if (GameData.Instance.lastGameState != null)
		{
			if (GameData.Instance.lastGameState.weapon_being_made_next == 17)
			{
				((ToggleButton)RefProducingBows).IsChecked = true;
			}
			else
			{
				((ToggleButton)RefProducingXBows).IsChecked = true;
			}
			if (GameData.Instance.lastGameState.can_make_xbows > 0)
			{
				((UIElement)RefProducingXBows).Visibility = (Visibility)2;
			}
			else
			{
				((UIElement)RefProducingXBows).Visibility = (Visibility)1;
			}
			if (GameData.Instance.lastGameState.can_make_bows > 0)
			{
				((UIElement)RefProducingBows).Visibility = (Visibility)2;
			}
			else
			{
				((UIElement)RefProducingBows).Visibility = (Visibility)1;
			}
		}
	}

	public void OpenPoleturners()
	{
		if (GameData.Instance.lastGameState != null)
		{
			if (GameData.Instance.lastGameState.weapon_being_made_next == 19)
			{
				((ToggleButton)RefProducingSpears).IsChecked = true;
			}
			else
			{
				((ToggleButton)RefProducingPikes).IsChecked = true;
			}
			if (GameData.Instance.lastGameState.can_make_pike > 0)
			{
				((UIElement)RefProducingPikes).Visibility = (Visibility)2;
			}
			else
			{
				((UIElement)RefProducingPikes).Visibility = (Visibility)1;
			}
			if (GameData.Instance.lastGameState.can_make_spear > 0)
			{
				((UIElement)RefProducingSpears).Visibility = (Visibility)2;
			}
			else
			{
				((UIElement)RefProducingSpears).Visibility = (Visibility)1;
			}
		}
	}

	public void OpenBlacksmiths()
	{
		if (GameData.Instance.lastGameState != null)
		{
			if (GameData.Instance.lastGameState.weapon_being_made_next == 22)
			{
				((ToggleButton)RefProducingSwords).IsChecked = true;
			}
			else
			{
				((ToggleButton)RefProducingMaces).IsChecked = true;
			}
			if (GameData.Instance.lastGameState.can_make_sword > 0)
			{
				((UIElement)RefProducingSwords).Visibility = (Visibility)2;
			}
			else
			{
				((UIElement)RefProducingSwords).Visibility = (Visibility)1;
			}
			if (GameData.Instance.lastGameState.can_make_mace > 0)
			{
				((UIElement)RefProducingMaces).Visibility = (Visibility)2;
			}
			else
			{
				((UIElement)RefProducingMaces).Visibility = (Visibility)1;
			}
		}
	}

	public string GetBuildingTitle(int thisType, int thisPanel)
	{
		switch (thisPanel)
		{
		case 70:
			return "";
		case 54:
			return "TEXT_IN_TRADEPOST_003";
		case 55:
			return "TEXT_IN_TRADEPOST_004";
		case 56:
			return "TEXT_IN_TRADEPOST_005";
		case 53:
			return "";
		case 57:
			return "";
		case 71:
			return "";
		case 72:
			return "TEXT_REPORT_BUTTONS_002";
		case 69:
			return "TEXT_REPORT_BUTTONS_002";
		case 73:
			return "TEXT_REPORT_BUTTONS_003";
		case 74:
			return "TEXT_REPORT_BUTTONS_004";
		case 75:
			return "TEXT_REPORT_BUTTONS_005";
		case 76:
			return "TEXT_REPORT_BUTTONS_006";
		case 68:
			return "TEXT_REPORT_BUTTONS_006";
		case 67:
			return "TEXT_REPORT_BUTTONS_006";
		case 66:
			return "TEXT_REPORT_BUTTONS_006";
		case 77:
			return "TEXT_REPORT_BUTTONS_007";
		case 78:
			return "TEXT_REPORT_BUTTONS_008";
		case 79:
			return "TEXT_REPORT_BUTTONS_009";
		case 93:
			return "TEXT_IN_POND_001";
		case 4:
			return "TEXT_IN_GRANARY_001";
		case 64:
			return "";
		default:
			if (thisType >= 118 && thisType <= 120)
			{
				return "TEXT_IN_GARDEN_001";
			}
			if (thisType <= 0)
			{
				return "";
			}
			if (thisType >= 110)
			{
				return "";
			}
			if (thisType == 36 && GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
			{
				return "TEXT_ISLAMIC_001";
			}
			if (thisType == 37 && GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
			{
				return "TEXT_ISLAMIC_002";
			}
			if (thisType == 38 && GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
			{
				return "TEXT_ISLAMIC_003";
			}
			return BuildingTitles[thisType];
		}
	}

	public int GetBuildingSketchImage(int thisType, int thisPanel)
	{
		switch (thisPanel)
		{
		case 71:
			return 0;
		case 72:
			return 78;
		case 69:
			return 78;
		case 73:
			return 64;
		case 74:
			return 79;
		case 75:
			return 66;
		case 76:
			return 61;
		case 68:
			return 61;
		case 67:
			return 61;
		case 66:
			return 61;
		case 77:
			return 83;
		case 78:
			return 88;
		case 79:
			if (GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
			{
				return 98;
			}
			return 81;
		case 93:
			return 55;
		default:
		{
			if (thisType >= 118 && thisType <= 120)
			{
				return 43;
			}
			if (thisPanel == 70)
			{
				return thisType switch
				{
					3 => 93, 
					62 => 59, 
					4 => 95, 
					6 => 70, 
					8 => 85, 
					7 => 85, 
					9 => 14, 
					10 => 76, 
					13 => 63, 
					14 => 63, 
					12 => 63, 
					11 => 63, 
					15 => 60, 
					16 => 57, 
					18 => 77, 
					19 => 91, 
					20 => 21, 
					31 => 72, 
					32 => 72, 
					33 => 80, 
					127 => 99, 
					34 => 69, 
					35 => 62, 
					36 => 71, 
					42 => 86, 
					43 => 35, 
					57 => 73, 
					17 => 94, 
					21 => 92, 
					63 => 74, 
					64 => 60, 
					1 => 45, 
					47 => 54, 
					45 => 52, 
					67 => 52, 
					53 => 65, 
					54 => 67, 
					_ => thisType switch
					{
						9 => 14, 
						51 => 14, 
						56 => 90, 
						52 => 101, 
						66 => 105, 
						65 => 106, 
						_ => 0, 
					}, 
				};
			}
			if (thisType <= 0)
			{
				return 0;
			}
			if (thisType >= 110)
			{
				return 0;
			}
			int num = sketchList[thisType];
			if (num == 36 && GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
			{
				return 98;
			}
			if (num == 53 && GameData.Instance.lastGameState != null && (GameData.Instance.lastGameState.lord_Type == 1 || GameData.Instance.lastGameState.lord_Type == 2 || GameData.Instance.lastGameState.lord_Type == 6 || GameData.Instance.lastGameState.lord_Type == 7))
			{
				return 107;
			}
			return num;
		}
		}
	}

	public string GetBuildingName(int thisType)
	{
		if (thisType >= 118 && thisType <= 120)
		{
			return "TEXT_IN_GARDEN_001";
		}
		if (thisType == 999)
		{
			return "TEXT_BUBBLE_HELP_TEXT_013";
		}
		if (thisType <= 0)
		{
			return "";
		}
		if (thisType == 200)
		{
			return "TEXT_ISLAMIC_001";
		}
		if (thisType == 201)
		{
			return "TEXT_ISLAMIC_002";
		}
		if (thisType == 202)
		{
			return "TEXT_ISLAMIC_003";
		}
		if (thisType >= 110)
		{
			return "";
		}
		return BuildingNames[thisType];
	}

	public int GetPartnerImage(int thisType)
	{
		return thisType switch
		{
			3 => 151, 
			4 => 152, 
			5 => 153, 
			6 => 157, 
			8 => 158, 
			7 => 158, 
			10 => 159, 
			13 => 160, 
			14 => 160, 
			12 => 174, 
			11 => 173, 
			15 => 161, 
			16 => 162, 
			18 => 163, 
			19 => 164, 
			20 => 165, 
			31 => 166, 
			32 => 166, 
			33 => 167, 
			34 => 168, 
			35 => 169, 
			36 => 170, 
			42 => 171, 
			57 => 172, 
			17 => 154, 
			21 => 155, 
			63 => 156, 
			_ => 174, 
		};
	}

	public bool GetBuildingSleepable(int thisType, int thisPanel)
	{
		switch (thisPanel)
		{
		case 70:
			return false;
		case 71:
			return false;
		case 72:
			return false;
		case 69:
			return false;
		case 73:
			return false;
		case 74:
			return false;
		case 75:
			return false;
		case 76:
			return false;
		case 68:
			return false;
		case 67:
			return false;
		case 66:
			return false;
		case 77:
			return false;
		case 78:
			return false;
		case 79:
			return false;
		default:
			if (thisType <= 0)
			{
				return false;
			}
			if (thisType >= 110)
			{
				return false;
			}
			if (IsWorkerBuilding[thisType] == 1)
			{
				return true;
			}
			return false;
		}
	}

	public bool GetBuildingShowWorkers(int thisType, int thisPanel)
	{
		switch (thisPanel)
		{
		case 70:
			return false;
		case 71:
			return false;
		case 72:
			return false;
		case 69:
			return false;
		case 73:
			return false;
		case 74:
			return false;
		case 75:
			return false;
		case 76:
			return false;
		case 68:
			return false;
		case 67:
			return false;
		case 66:
			return false;
		case 77:
			return false;
		case 78:
			return false;
		case 79:
			return false;
		default:
			if (thisType <= 0)
			{
				return false;
			}
			if (thisType >= 110)
			{
				return false;
			}
			if (ShowWorkersBuilding[thisType] == 1)
			{
				return true;
			}
			return false;
		}
	}

	public bool GetBuildingShowInfo(int thisType, int thisPanel)
	{
		switch (thisPanel)
		{
		case 70:
			return false;
		case 64:
			return true;
		default:
			switch (thisType)
			{
			case 1:
			case 4:
			case 21:
			case 28:
			case 35:
			case 54:
			case 62:
			case 63:
			case 65:
			case 66:
			case 74:
			case 75:
			case 76:
			case 77:
			case 78:
			case 80:
			case 81:
			case 82:
			case 83:
			case 84:
			case 91:
			case 92:
			case 93:
			case 94:
			case 95:
			case 97:
			case 98:
			case 101:
			case 103:
			case 118:
			case 119:
			case 120:
				return true;
			default:
				return false;
			}
		}
	}

	public string GetBuildingInfo(int thisType, int thisPanel)
	{
		switch (thisPanel)
		{
		case 70:
			return "";
		case 64:
			return "TEXT_NEW_CTEXT_385";
		case 71:
		case 72:
		case 73:
		case 74:
		case 75:
		case 76:
		case 77:
		case 78:
		case 79:
			return "";
		default:
			return thisType switch
			{
				1 => "TEXT_IN_HOUSE_002", 
				21 => "TEXT_IN_QUARRYPILE_002", 
				4 => "TEXT_IN_OXEN_BASE_004", 
				28 => "TEXT_IN_OIL_SMELTER_002", 
				74 => "TEXT_IN_TOWER_002", 
				75 => "TEXT_IN_TOWER_003", 
				76 => "TEXT_IN_TOWER_004", 
				77 => "TEXT_IN_TOWER_005", 
				78 => "TEXT_IN_TOWER_006", 
				62 => "TEXT_IN_GALLOWS_002", 
				91 => "TEXT_IN_GALLOWS_002", 
				63 => "TEXT_IN_GALLOWS_002", 
				92 => "TEXT_IN_GALLOWS_002", 
				94 => "TEXT_IN_GALLOWS_002", 
				95 => "TEXT_IN_GALLOWS_002", 
				93 => "TEXT_IN_GALLOWS_002", 
				97 => "TEXT_IN_GALLOWS_002", 
				98 => "TEXT_IN_GALLOWS_002", 
				65 => "TEXT_IN_MAYPOLE_002", 
				103 => "TEXT_IN_DANCING_BEAR_002", 
				66 => "TEXT_IN_GARDEN_002", 
				118 => "TEXT_IN_GARDEN_002", 
				119 => "TEXT_IN_GARDEN_002", 
				120 => "TEXT_IN_GARDEN_002", 
				101 => "TEXT_IN_MAYPOLE_002", 
				_ => "", 
			};
		}
	}

	public bool GetBuildingShowRepair(int thisType, int thisPanel)
	{
		return thisPanel switch
		{
			70 => false, 
			53 => false, 
			71 => false, 
			72 => false, 
			69 => false, 
			73 => false, 
			74 => false, 
			75 => false, 
			76 => false, 
			68 => false, 
			67 => false, 
			66 => false, 
			77 => false, 
			78 => false, 
			79 => false, 
			_ => thisType switch
			{
				74 => true, 
				75 => true, 
				76 => true, 
				77 => true, 
				78 => true, 
				47 => true, 
				46 => true, 
				45 => true, 
				48 => true, 
				_ => false, 
			}, 
		};
	}

	public bool GetBuildingShowHelp(int thisType, int thisPanel)
	{
		switch (thisPanel)
		{
		case 70:
			return false;
		case 53:
			return false;
		case 71:
			return false;
		case 72:
			return false;
		case 69:
			return false;
		case 73:
			return false;
		case 74:
			return false;
		case 75:
			return false;
		case 76:
			return false;
		case 68:
			return false;
		case 67:
			return false;
		case 66:
			return false;
		case 77:
			return false;
		case 78:
			return false;
		case 79:
			return false;
		case 89:
			if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.dog_cage_state != 0)
			{
				return false;
			}
			break;
		}
		return HUD_Help.doesBuildingHelpExist(thisType);
	}

	public void UpdateButtonState()
	{
		if (GameData.Instance.lastGameState == null)
		{
			return;
		}
		int app_sub_mode = GameData.Instance.app_sub_mode;
		if (app_sub_mode <= 37)
		{
			switch (app_sub_mode)
			{
			case 36:
				if (GameData.Instance.lastGameState.gatehouse_state == 10)
				{
					PropEx.SetSprite1((UIElement)(object)RefButtonCloseGate, MainViewModel.Instance.GameSprites[85]);
					PropEx.SetSprite2((UIElement)(object)RefButtonCloseGate, MainViewModel.Instance.GameSprites[85]);
					PropEx.SetSprite1((UIElement)(object)RefButtonOpenGate, MainViewModel.Instance.GameSprites[84]);
					PropEx.SetSprite2((UIElement)(object)RefButtonOpenGate, MainViewModel.Instance.GameSprites[84]);
				}
				else
				{
					PropEx.SetSprite1((UIElement)(object)RefButtonCloseGate, MainViewModel.Instance.GameSprites[86]);
					PropEx.SetSprite2((UIElement)(object)RefButtonCloseGate, MainViewModel.Instance.GameSprites[86]);
					PropEx.SetSprite1((UIElement)(object)RefButtonOpenGate, MainViewModel.Instance.GameSprites[83]);
					PropEx.SetSprite2((UIElement)(object)RefButtonOpenGate, MainViewModel.Instance.GameSprites[83]);
				}
				break;
			case 37:
				if (GameData.Instance.lastGameState.gatehouse_state == 10)
				{
					PropEx.SetSprite1((UIElement)(object)RefButtonCloseBridge, MainViewModel.Instance.GameSprites[85]);
					PropEx.SetSprite2((UIElement)(object)RefButtonCloseBridge, MainViewModel.Instance.GameSprites[85]);
					PropEx.SetSprite1((UIElement)(object)RefButtonOpenBridge, MainViewModel.Instance.GameSprites[84]);
					PropEx.SetSprite2((UIElement)(object)RefButtonOpenBridge, MainViewModel.Instance.GameSprites[84]);
				}
				else
				{
					PropEx.SetSprite1((UIElement)(object)RefButtonCloseBridge, MainViewModel.Instance.GameSprites[86]);
					PropEx.SetSprite2((UIElement)(object)RefButtonCloseBridge, MainViewModel.Instance.GameSprites[86]);
					PropEx.SetSprite1((UIElement)(object)RefButtonOpenBridge, MainViewModel.Instance.GameSprites[83]);
					PropEx.SetSprite2((UIElement)(object)RefButtonOpenBridge, MainViewModel.Instance.GameSprites[83]);
				}
				break;
			}
			return;
		}
		if (app_sub_mode != 45)
		{
			_ = 88;
			return;
		}
		for (int i = 0; i < 8; i++)
		{
			if ((GameData.Instance.lastGameState.marry_m_name1 & (1 << i)) > 0)
			{
				MainViewModel.Instance.OutpostVisible[i] = (Visibility)2;
			}
			else
			{
				MainViewModel.Instance.OutpostVisible[i] = (Visibility)1;
			}
		}
	}
}
