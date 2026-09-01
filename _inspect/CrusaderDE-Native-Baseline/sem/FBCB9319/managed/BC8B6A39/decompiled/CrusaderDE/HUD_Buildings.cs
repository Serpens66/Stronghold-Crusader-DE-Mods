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

	private bool currentAutoTradeOn;

	private bool sliderSetup;

	private bool insideValueChanged;

	private TranslateTransform[] SelTroopPositions = new TranslateTransform[8]
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

	private int[] sketchList = new int[110]
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

	private string[] BuildingTitles = new string[110]
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

	private string[] BuildingNames = new string[110]
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

	private int[] IsWorkerBuilding = new int[110]
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

	private int[] ShowWorkersBuilding = new int[110]
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
		InitializeComponent();
		MainViewModel.Instance.HUDBuildingPanel = this;
		RefRecruitArcherButton = (ToggleButton)FindName("BarracksArcher");
		RefRecruitSpearmanButton = (ToggleButton)FindName("BarracksSpearman");
		RefRecruitMacemanButton = (ToggleButton)FindName("BarracksMaceman");
		RefRecruitXBowmanButton = (ToggleButton)FindName("BarracksXBowman");
		RefRecruitPikemanButton = (ToggleButton)FindName("BarracksPikeman");
		RefRecruitSwordsmanButton = (ToggleButton)FindName("BarracksSwordsman");
		RefRecruitKnightButton = (ToggleButton)FindName("BarracksKnight");
		RefRecruitArabBowButton = (ToggleButton)FindName("MercArabBow");
		RefRecruitArabSlaveButton = (ToggleButton)FindName("MercArabSlave");
		RefRecruitArabSlingerButton = (ToggleButton)FindName("MercArabSlinger");
		RefRecruitArabAssassinButton = (ToggleButton)FindName("MercArabAssassin");
		RefRecruitArabHorseArcherButton = (ToggleButton)FindName("MercArabHorseArcher");
		RefRecruitArabSwordsmanButton = (ToggleButton)FindName("MercArabSwordsman");
		RefRecruitArabGrenadierButton = (ToggleButton)FindName("MercArabGrenadier");
		RefRecruitBedouinCamelLancerButton = (ToggleButton)FindName("BedouinCamelLancer");
		RefRecruitBedouinHealerButton = (ToggleButton)FindName("BedouinHealer");
		RefRecruitBedouinEunuchButton = (ToggleButton)FindName("BedouinEunuch");
		RefRecruitBedouinAmbusherButton = (ToggleButton)FindName("BedouinAmbusher");
		RefRecruitBedouinSkirmisherButton = (ToggleButton)FindName("BedouinSkirmisher");
		RefRecruitBedouinHeavyCamelButton = (ToggleButton)FindName("BedouinHeavyCamel");
		RefRecruitBedouinSapperButton = (ToggleButton)FindName("BedouinSapper");
		RefRecruitBedouinDemolisherButton = (ToggleButton)FindName("BedouinDemolisher");
		RefRecruitEngineerButton = (Button)FindName("BarracksEngineer");
		RefRecruitEngineerButtonX = (Button)FindName("BarracksEngineerX");
		RefRecruitLaddermanButton = (Button)FindName("BarracksLadderman");
		RefRecruitLaddermanButtonX = (Button)FindName("BarracksLaddermanX");
		RefRecruitTunellerButton = (Button)FindName("BarracksTuneller");
		RefRecruitMonkButton = (Button)FindName("BarracksMonk");
		RefButtonReleaseDogs = (Button)FindName("ButtonReleaseDogs");
		RefHUDBuildingFullClickMask = (Grid)FindName("HUDBuildingFullClickMask");
		RefBuildingPanel = (Grid)FindName("BuildingPanel");
		RefBarracksPanel = (Grid)FindName("BarracksPanel");
		RefMercPostPanel = (Grid)FindName("MercPostPanel");
		RefBedouinStockadePanel = (Grid)FindName("BedouinStockadePanel");
		RefWorkerPanel = (Grid)FindName("WorkerPanel");
		RefKeepPanel = (Grid)FindName("KeepPanel");
		RefOutpostPanel = (Grid)FindName("OutpostPanel");
		RefOutpostArabPanel = (Grid)FindName("OutpostArabPanel");
		RefOutpostBedouinPanel = (Grid)FindName("OutpostBedouinPanel");
		RefGranaryPanel = (Grid)FindName("GranaryPanel");
		RefArmouryPanel = (Grid)FindName("ArmouryPanel");
		RefStockpilePanel = (Grid)FindName("StockpilePanel");
		RefInnPanel = (Grid)FindName("InnPanel");
		RefFletchersPanel = (Grid)FindName("FletchersPanel");
		RefPoleturnersPanel = (Grid)FindName("PoleturnersPanel");
		RefBlacksmithsPanel = (Grid)FindName("BlacksmithsPanel");
		RefChurchPanel = (Grid)FindName("ChurchPanel");
		RefTradepostPanel = (Grid)FindName("TradepostPanel");
		RefTradingFoodPanel = (Grid)FindName("TradingFoodPanel");
		RefTradingResourcesPanel = (Grid)FindName("TradingResourcesPanel");
		RefTradingWeaponsPanel = (Grid)FindName("TradingWeaponsPanel");
		RefTradingPricesPanel = (Grid)FindName("TradingPricesPanel");
		RefTradingTradePanel = (Grid)FindName("TradingTradePanel");
		RefTradeBuyButton = (Button)FindName("TradeBuy");
		RefTradeSellButton = (Button)FindName("TradeSell");
		RefTradeErrorAnination = (Storyboard)TryFindResource("TradeErrorFadeOut");
		RefTradePost_Trade_Normal = (Grid)FindName("TradePost_Trade_Normal");
		RefTradePost_Trade_Auto = (Grid)FindName("TradePost_Trade_Auto");
		RefTradePost_Trade_Auto.Visibility = Visibility.Hidden;
		RefTrade_AutoToggle = (Button)FindName("Trade_AutoToggle");
		RefTrade_GoTo_Auto = (Button)FindName("Trade_GoTo_Auto");
		RefAutotrade_Sell_Slider = (Slider)FindName("Autotrade_Sell_Slider");
		RefAutotrade_Buy_Slider = (Slider)FindName("Autotrade_Buy_Slider");
		RefAutotrade_Sell_Slider.ValueChanged += Autotrade_Sell_Slider_ValueChanged;
		RefAutotrade_Buy_Slider.ValueChanged += Autotrade_Buy_Slider_ValueChanged;
		RefOutpostSizeSlider = (Slider)FindName("OutpostSize");
		RefOutpostSizeSlider.ValueChanged += OutpostSizeSlider_ValueChanged;
		RefOutpostDelaySlider = (Slider)FindName("OutpostDelay");
		RefOutpostDelaySlider.ValueChanged += OutpostDelaySlider_ValueChanged;
		RefOutpostArabSizeSlider = (Slider)FindName("OutpostArabSize");
		RefOutpostArabSizeSlider.ValueChanged += OutpostArabSizeSlider_ValueChanged;
		RefOutpostArabDelaySlider = (Slider)FindName("OutpostArabDelay");
		RefOutpostArabDelaySlider.ValueChanged += OutpostArabDelaySlider_ValueChanged;
		RefOutpostBedouinSizeSlider = (Slider)FindName("OutpostBedouinSize");
		RefOutpostBedouinSizeSlider.ValueChanged += OutpostBedouinSizeSlider_ValueChanged;
		RefOutpostBedouinDelaySlider = (Slider)FindName("OutpostBedouinDelay");
		RefOutpostBedouinDelaySlider.ValueChanged += OutpostBedouinDelaySlider_ValueChanged;
		RefReportsPanel = (Grid)FindName("ReportsPanel");
		RefReportsPopularityPanel = (Grid)FindName("ReportsPopularityPanel");
		RefReportsPopEventsPanel = (Grid)FindName("ReportsPopEventsPanel");
		RefShowEventsButton = (Grid)FindName("ShowEventsPanelControl");
		RefReportsFearFactorPanel = (Grid)FindName("ReportsFearFactorPanel");
		RefReportsPopulationPanel = (Grid)FindName("ReportsPopulationPanel");
		RefReportsArmy1Panel = (Grid)FindName("ReportsArmy1Panel");
		RefReportsArmy2Panel = (Grid)FindName("ReportsArmy2Panel");
		RefReportsArmy3Panel = (Grid)FindName("ReportsArmy3Panel");
		RefReportsArmy4Panel = (Grid)FindName("ReportsArmy4Panel");
		RefReportsStoresPanel = (Grid)FindName("ReportsStoresPanel");
		RefReportsWeaponsPanel = (Grid)FindName("ReportsWeaponsPanel");
		RefReportsReligionPanel = (Grid)FindName("ReportsReligionPanel");
		RefWGT_PopReportAleText = (TextBlock)FindName("WGT_PopReportAleText");
		RefKeepTaxRate = (TextBlock)FindName("KeepTaxRate");
		RefChimpPanel = (Grid)FindName("ChimpPanel");
		RefShowGatePanel = (Grid)FindName("ShowGate");
		RefShowDrawbridgePanel = (Grid)FindName("ShowDrawbridge");
		RefShowEngineersGuildPanel = (Grid)FindName("EngineersGuildPanel");
		RefShowTunellersGuildPanel = (Grid)FindName("TunellersGuildPanel");
		RefShowCathedralPanel = (Grid)FindName("CathedralPanel");
		RefShowDogsPanel = (Grid)FindName("ShowDogs");
		RefReportsFoodPanel = (Grid)FindName("ReportsFoodPanel");
		RefShowWorkersPanel = (Grid)FindName("ShowWorkersPanel");
		RefShowInfoPanel = (Grid)FindName("ShowInfoPanel");
		RefShowRepairPanel = (Grid)FindName("ShowRepairPanel");
		RefHelpButton = (Button)FindName("BuildingHelpButton");
		RefButtonRepair = (Button)FindName("ButtonRepair");
		RefTroopCostsText = (TextBlock)FindName("TroopCostsText");
		RefTroopNameText = (TextBlock)FindName("TroopNameText");
		RefTroopHelpText = (TextBlock)FindName("TroopHelpText");
		RefArabTroopCostsText = (TextBlock)FindName("ArabTroopCostsText");
		RefArabTroopNameText = (TextBlock)FindName("ArabTroopNameText");
		RefArabTroopHelpText = (TextBlock)FindName("ArabTroopHelpText");
		RefBedouinTroopCostsText = (TextBlock)FindName("BedouinTroopCostsText");
		RefBedouinTroopNameText = (TextBlock)FindName("BedouinTroopNameText");
		RefBedouinTroopHelpText = (TextBlock)FindName("BedouinTroopHelpText");
		RefEngineersCostsText = (TextBlock)FindName("EngineersCostsText");
		RefEngineersHelpText = (TextBlock)FindName("EngineersHelpText");
		RefTunellersCostsText = (TextBlock)FindName("TunellersCostsText");
		RefTunellersHelpText = (TextBlock)FindName("TunellersHelpText");
		RefDogsReleasedText = (TextBlock)FindName("DogsReleasedText");
		RefMonkCostsText = (TextBlock)FindName("MonkCostsText");
		RefMonkHelpText = (TextBlock)FindName("MonkHelpText");
		RefEngineersGuildNoGoldMessage = (TextBlock)FindName("EngineersGuildNoGoldMessage");
		RefTunnllersGuildNoGoldMessage = (TextBlock)FindName("TunnllersGuildNoGoldMessage");
		RefCathedralNoGoldMessage = (TextBlock)FindName("CathedralNoGoldMessage");
		RefWGTFoodTypePop = (WGT_Popularity)FindName("WGT_FoodTypePop");
		RefWGTRationsPop = (WGT_Popularity)FindName("WGT_RationsPop");
		RefWGTInnPop = (WGT_Popularity)FindName("WGT_InnPop");
		RefWGTTaxPop = (WGT_Popularity)FindName("WGT_TaxPop");
		RefWGTPopReportFoodPop = (WGT_Popularity)FindName("WGT_PopReportFood");
		RefWGTPopReportTaxPop = (WGT_Popularity)FindName("WGT_PopReportTax");
		RefWGTPopReportCrowdingPop = (WGT_Popularity)FindName("WGT_PopReportCrowding");
		RefWGTPopReportFearFactorPop = (WGT_Popularity)FindName("WGT_PopReportFearFactor");
		RefWGTPopReportReligionPop = (WGT_Popularity)FindName("WGT_PopReportReligion");
		RefWGTPopReportAlePop = (WGT_Popularity)FindName("WGT_PopReportAle");
		RefWGTPopReportEventsPop = (WGT_Popularity)FindName("WGT_PopReportEvents");
		RefWGTPopReportTotalPop = (WGT_Popularity)FindName("WGT_PopReportTotal");
		RefWGTPopReportTotal2Pop = (WGT_Popularity)FindName("WGT_PopReportTotal2");
		RefWGTPopReportFairsPop = (WGT_Popularity)FindName("WGT_PopReportFairs");
		RefWGTPopReportMarriagePop = (WGT_Popularity)FindName("WGT_PopReportMarriage");
		RefWGTPopReportJesterPop = (WGT_Popularity)FindName("WGT_PopReportJester");
		RefWGTPopReportPlaguePop = (WGT_Popularity)FindName("WGT_PopReportPlague");
		RefWGTPopReportWolvesPop = (WGT_Popularity)FindName("WGT_PopReportWolves");
		RefWGTPopReportBanditsPop = (WGT_Popularity)FindName("WGT_PopReportBandits");
		RefWGTPopReportFirePop = (WGT_Popularity)FindName("WGT_PopReportFire");
		RefWGTFFReportFearFactorPop = (WGT_Popularity)FindName("WGT_FFReportFearFactor");
		RefWGTRelReport = (WGT_Popularity)FindName("WGT_RelReport");
		RefWGTRelReport2 = (WGT_Popularity)FindName("WGT_RelReport2");
		RefRationHandNone = (Image)FindName("RationHandNone");
		RefRationHandHalf = (Image)FindName("RationHandHalf");
		RefRationHandFull = (Image)FindName("RationHandFull");
		RefRationHandExtra = (Image)FindName("RationHandExtra");
		RefRationHandDouble = (Image)FindName("RationHandDouble");
		RefStopMeatConsumption = (Image)FindName("StopMeatConsumption");
		RefStopCheeseConsumption = (Image)FindName("StopCheeseConsumption");
		RefStopBreadConsumption = (Image)FindName("StopBreadConsumption");
		RefStopApplesConsumption = (Image)FindName("StopApplesConsumption");
		RefButtonOpenGate = (Button)FindName("ButtonOpenGate");
		RefButtonCloseGate = (Button)FindName("ButtonCloseGate");
		RefButtonOpenBridge = (Button)FindName("ButtonOpenBridge");
		RefButtonCloseBridge = (Button)FindName("ButtonCloseBridge");
		RefProducingBows = (RadioButton)FindName("ProducingBows");
		RefProducingXBows = (RadioButton)FindName("ProducingXBows");
		RefProducingSpears = (RadioButton)FindName("ProducingSpears");
		RefProducingPikes = (RadioButton)FindName("ProducingPikes");
		RefProducingSwords = (RadioButton)FindName("ProducingSwords");
		RefProducingMaces = (RadioButton)FindName("ProducingMaces");
		RefRelReportPopEffectLabel = (TextBlock)FindName("RelReportPopEffectLabel");
		RefWGT_RelReportLabel = (TextBlock)FindName("WGT_RelReportLabel");
		RefBuildingZZZButtonOn = (Button)FindName("BuildingZZZButtonOn");
		RefBuildingZZZButtonOff = (Button)FindName("BuildingZZZButtonOff");
		RefButtonArmyReportBack = (Button)FindName("ButtonArmyReportBack");
		RefButtonArmyReportBack2 = (Button)FindName("ButtonArmyReportBack2");
		RefButtonArmyReportBack3 = (Button)FindName("ButtonArmyReportBack3");
		MainViewModel.Instance.HUDmain.RefTutorialArrow3 = (Image)FindName("TutorialArrow3");
		MainViewModel.Instance.HUDmain.RefTutorialArrow4 = (Image)FindName("TutorialArrow4");
		MainViewModel.Instance.HUDmain.RefTutorialArrow6 = (Image)FindName("TutorialArrow6");
		MainViewModel.Instance.HUDmain.RefTutorialArrow19 = (Image)FindName("TutorialArrow19");
		MainViewModel.Instance.HUDmain.RefTutorialArrow21 = (Image)FindName("TutorialArrow21");
		RefReportsReligionPanelSubArea = (Grid)FindName("ReportsReligionPanelSubArea");
		RefAutotrade_Buy_Text = (TextBlock)FindName("Autotrade_Buy_Text");
		RefAutotrade_Sell_Text = (TextBlock)FindName("Autotrade_Sell_Text");
		RefTradeErrorMessage = (TextBlock)FindName("TradeErrorMessage");
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
			RefTradeBuyButton.Width = 226f;
			RefTradeSellButton.Width = 226f;
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
			RefKeepTaxRate.Margin = new Thickness(-60f, 0f, 0f, 4f);
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
				RefReportsReligionPanelSubArea.Margin = new Thickness(180f, 40f, 340f, 0f);
			}
			RefTradeErrorMessage.Margin = new Thickness(0f, 0f, 208f, 0f);
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
				RefAutotrade_Buy_Slider.Value = i;
				break;
			}
		}
		for (int j = 0; j <= 100; j++)
		{
			if (num3 <= sliderCurve[j])
			{
				RefAutotrade_Sell_Slider.Value = j;
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

	private void UpdateAutoTradeButton(bool tradingOn)
	{
		if (tradingOn)
		{
			PropEx.SetSprite1(RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[269]);
			PropEx.SetSprite2(RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[270]);
			PropEx.SetSprite3(RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[270]);
			PropEx.SetSprite4(RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[270]);
		}
		else
		{
			PropEx.SetSprite1(RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[271]);
			PropEx.SetSprite2(RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[272]);
			PropEx.SetSprite3(RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[272]);
			PropEx.SetSprite4(RefTrade_AutoToggle, MainViewModel.Instance.GameSprites[272]);
		}
	}

	public void UpdateAutoTradeSelectButton(bool lastState = false)
	{
		if ((!lastState) ? (GameData.Instance.lastGameState.autotrade_onoff[GameData.Instance.lastGameState.trading_current_goods] > 0) : currentAutoTradeOn)
		{
			PropEx.SetSprite1(RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[273]);
			PropEx.SetSprite2(RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[274]);
			PropEx.SetSprite3(RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[274]);
			PropEx.SetSprite4(RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[274]);
		}
		else
		{
			PropEx.SetSprite1(RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[275]);
			PropEx.SetSprite2(RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[276]);
			PropEx.SetSprite3(RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[276]);
			PropEx.SetSprite4(RefTrade_GoTo_Auto, MainViewModel.Instance.GameSprites[276]);
		}
	}

	private void Autotrade_Sell_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (FatControler.currentScene == (Enums.SceneIDS)0)
		{
			return;
		}
		int num = (int)RefAutotrade_Sell_Slider.Value;
		if (!insideValueChanged)
		{
			insideValueChanged = true;
			if (num <= (int)RefAutotrade_Buy_Slider.Value)
			{
				RefAutotrade_Buy_Slider.Value = num - 1;
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

	private void Autotrade_Buy_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (FatControler.currentScene == (Enums.SceneIDS)0)
		{
			return;
		}
		int num = (int)RefAutotrade_Buy_Slider.Value;
		if (!insideValueChanged)
		{
			insideValueChanged = true;
			if (num >= (int)RefAutotrade_Sell_Slider.Value)
			{
				RefAutotrade_Sell_Slider.Value = num + 1;
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

	private void OutpostSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (!inSetup)
		{
			int state = (int)RefOutpostSizeSlider.Value;
			EngineInterface.GameAction(Enums.GameActionCommand.SetOutpostSize, GameData.Instance.lastGameState.in_structure, state);
		}
	}

	private void OutpostDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (!inSetup)
		{
			int state = (int)RefOutpostDelaySlider.Value;
			EngineInterface.GameAction(Enums.GameActionCommand.SetOutpostDelay, GameData.Instance.lastGameState.in_structure, state);
		}
	}

	private void OutpostArabSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (!inSetup)
		{
			int state = (int)RefOutpostArabSizeSlider.Value;
			EngineInterface.GameAction(Enums.GameActionCommand.SetOutpostSize, GameData.Instance.lastGameState.in_structure, state);
		}
	}

	private void OutpostArabDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (!inSetup)
		{
			int state = (int)RefOutpostArabDelaySlider.Value;
			EngineInterface.GameAction(Enums.GameActionCommand.SetOutpostDelay, GameData.Instance.lastGameState.in_structure, state);
		}
	}

	private void OutpostBedouinSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (!inSetup)
		{
			int state = (int)RefOutpostBedouinSizeSlider.Value;
			EngineInterface.GameAction(Enums.GameActionCommand.SetOutpostSize, GameData.Instance.lastGameState.in_structure, state);
		}
	}

	private void OutpostBedouinDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<float> e)
	{
		if (!inSetup)
		{
			int state = (int)RefOutpostBedouinDelaySlider.Value;
			EngineInterface.GameAction(Enums.GameActionCommand.SetOutpostDelay, GameData.Instance.lastGameState.in_structure, state);
		}
	}

	private void InitializeComponent()
	{
		GUI.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_Buildings.xaml");
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
		if (eventName == "MouseEnter" && handlerName == "MouseEnterBuildingIconHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MouseEnterBuildingIconHandler;
			}
			else if (source is RadioButton)
			{
				((RadioButton)source).MouseEnter += MouseEnterBuildingIconHandler;
			}
			return true;
		}
		if (eventName == "MouseLeave" && handlerName == "MouseLeaveBuildingIconHandler")
		{
			if (source is Button)
			{
				((Button)source).MouseLeave += MouseLeaveBuildingIconHandler;
			}
			else if (source is RadioButton)
			{
				((RadioButton)source).MouseLeave += MouseLeaveBuildingIconHandler;
			}
			return true;
		}
		return false;
	}

	private void MouseEnterBuildingIconHandler(object sender, MouseEventArgs e)
	{
		if (e.Source is Button)
		{
			if (((Button)e.Source).Tag != null && ((Button)e.Source).Tag is string)
			{
				MainViewModel.Instance.HUDmain.HoverString = (string)((Button)e.Source).Tag;
			}
			else
			{
				MainViewModel.Instance.HUDmain.HoverString = "";
			}
		}
	}

	private void MouseLeaveBuildingIconHandler(object sender, MouseEventArgs e)
	{
		MainViewModel.Instance.HUDmain.HoverString = "";
	}

	public void OpenFletchers()
	{
		if (GameData.Instance.lastGameState != null)
		{
			if (GameData.Instance.lastGameState.weapon_being_made_next == 17)
			{
				RefProducingBows.IsChecked = true;
			}
			else
			{
				RefProducingXBows.IsChecked = true;
			}
			if (GameData.Instance.lastGameState.can_make_xbows > 0)
			{
				RefProducingXBows.Visibility = Visibility.Visible;
			}
			else
			{
				RefProducingXBows.Visibility = Visibility.Hidden;
			}
			if (GameData.Instance.lastGameState.can_make_bows > 0)
			{
				RefProducingBows.Visibility = Visibility.Visible;
			}
			else
			{
				RefProducingBows.Visibility = Visibility.Hidden;
			}
		}
	}

	public void OpenPoleturners()
	{
		if (GameData.Instance.lastGameState != null)
		{
			if (GameData.Instance.lastGameState.weapon_being_made_next == 19)
			{
				RefProducingSpears.IsChecked = true;
			}
			else
			{
				RefProducingPikes.IsChecked = true;
			}
			if (GameData.Instance.lastGameState.can_make_pike > 0)
			{
				RefProducingPikes.Visibility = Visibility.Visible;
			}
			else
			{
				RefProducingPikes.Visibility = Visibility.Hidden;
			}
			if (GameData.Instance.lastGameState.can_make_spear > 0)
			{
				RefProducingSpears.Visibility = Visibility.Visible;
			}
			else
			{
				RefProducingSpears.Visibility = Visibility.Hidden;
			}
		}
	}

	public void OpenBlacksmiths()
	{
		if (GameData.Instance.lastGameState != null)
		{
			if (GameData.Instance.lastGameState.weapon_being_made_next == 22)
			{
				RefProducingSwords.IsChecked = true;
			}
			else
			{
				RefProducingMaces.IsChecked = true;
			}
			if (GameData.Instance.lastGameState.can_make_sword > 0)
			{
				RefProducingSwords.Visibility = Visibility.Visible;
			}
			else
			{
				RefProducingSwords.Visibility = Visibility.Hidden;
			}
			if (GameData.Instance.lastGameState.can_make_mace > 0)
			{
				RefProducingMaces.Visibility = Visibility.Visible;
			}
			else
			{
				RefProducingMaces.Visibility = Visibility.Hidden;
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
					PropEx.SetSprite1(RefButtonCloseGate, MainViewModel.Instance.GameSprites[85]);
					PropEx.SetSprite2(RefButtonCloseGate, MainViewModel.Instance.GameSprites[85]);
					PropEx.SetSprite1(RefButtonOpenGate, MainViewModel.Instance.GameSprites[84]);
					PropEx.SetSprite2(RefButtonOpenGate, MainViewModel.Instance.GameSprites[84]);
				}
				else
				{
					PropEx.SetSprite1(RefButtonCloseGate, MainViewModel.Instance.GameSprites[86]);
					PropEx.SetSprite2(RefButtonCloseGate, MainViewModel.Instance.GameSprites[86]);
					PropEx.SetSprite1(RefButtonOpenGate, MainViewModel.Instance.GameSprites[83]);
					PropEx.SetSprite2(RefButtonOpenGate, MainViewModel.Instance.GameSprites[83]);
				}
				break;
			case 37:
				if (GameData.Instance.lastGameState.gatehouse_state == 10)
				{
					PropEx.SetSprite1(RefButtonCloseBridge, MainViewModel.Instance.GameSprites[85]);
					PropEx.SetSprite2(RefButtonCloseBridge, MainViewModel.Instance.GameSprites[85]);
					PropEx.SetSprite1(RefButtonOpenBridge, MainViewModel.Instance.GameSprites[84]);
					PropEx.SetSprite2(RefButtonOpenBridge, MainViewModel.Instance.GameSprites[84]);
				}
				else
				{
					PropEx.SetSprite1(RefButtonCloseBridge, MainViewModel.Instance.GameSprites[86]);
					PropEx.SetSprite2(RefButtonCloseBridge, MainViewModel.Instance.GameSprites[86]);
					PropEx.SetSprite1(RefButtonOpenBridge, MainViewModel.Instance.GameSprites[83]);
					PropEx.SetSprite2(RefButtonOpenBridge, MainViewModel.Instance.GameSprites[83]);
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
				MainViewModel.Instance.OutpostVisible[i] = Visibility.Visible;
			}
			else
			{
				MainViewModel.Instance.OutpostVisible[i] = Visibility.Hidden;
			}
		}
	}
}
