using System.Collections.Generic;
using System.ComponentModel;

namespace CrusaderCastleEditorUnity;

public class PnlEditLordViewModel : INotifyPropertyChanged
{
	private bool inSetupPhase;

	private bool _areChangesPresent;

	private string _editorTitle;

	private string _saveButtonContents;

	private PairValueIntString _selectedFlag;

	private PairValueIntString _selectedLordFaction;

	private PairValueIntString _selectedLordPowerDisplayLevel;

	private int _hpPercentage;

	private int _criticalPopThreshold;

	private int _lowPopThreshold;

	private int _highPopThreshold;

	private int _doubleRationThreshold;

	private PairValueIntString _minTax;

	private PairValueIntString _maxTax;

	private PairValueIntString _selectedAleAllowance;

	private PairValueIntString _selectedReligionAllowance;

	private int _foodPurchaseAmount;

	private int _weaponPurchaseAmount;

	private int _pesterGoodsDelay;

	private int _goodsSendMargin;

	private int _sellFoodMargin;

	private int _appleBuyMargin;

	private int _cheeseBuyMargin;

	private int _breadBuyMargin;

	private int _wheatBuyMargin;

	private int _hopBuyMargin;

	private int _woodSellMargin;

	private int _stoneSellMargin;

	private int _miscSellMargin;

	private int _weaponSellMargin;

	private int _aleSellMargin;

	private int _pitchSellMargin;

	private int _flourSellMargin;

	private int _saleVariance;

	private int _baseGoldReserve;

	private int _buildDelay;

	private int _buildingRepairDelay;

	private int _populationPerFarm;

	private int _populationPerWoodCutter;

	private int _populationPerQuarry;

	private int _populationPerIronMine;

	private int _populationPerPitchDugout;

	private int _maximumFarmAmount;

	private int _maximumWoodCutterAmount;

	private int _maximumQuarryAmount;

	private int _maximumIronMineAmount;

	private int _maximumPitchDugoutAmount;

	private PairValueIntString _selectedFarm1;

	private PairValueIntString _selectedFarm2;

	private PairValueIntString _selectedFarm3;

	private PairValueIntString _selectedFarm4;

	private PairValueIntString _selectedFarm5;

	private PairValueIntString _selectedFarm6;

	private PairValueIntString _selectedFarm7;

	private PairValueIntString _selectedFarm8;

	private PairValueIntString _selectedBlackSmithMode;

	private PairValueIntString _selectedFletcherMode;

	private PairValueIntString _selectedPoleturnerMode;

	private PairValueIntString _bannedResource1;

	private PairValueIntString _bannedResource2;

	private PairValueIntString _bannedResource3;

	private PairValueIntString _bannedResource4;

	private PairValueIntString _bannedResource5;

	private PairValueIntString _bannedResource6;

	private PairValueIntString _bannedResource7;

	private PairValueIntString _bannedResource8;

	private PairValueIntString _bannedResource9;

	private PairValueIntString _bannedResource10;

	private PairValueIntString _bannedResource11;

	private PairValueIntString _bannedResource12;

	private PairValueIntString _bannedResource13;

	private PairValueIntString _bannedResource14;

	private PairValueIntString _bannedResource15;

	private int _weakDefenseModeProb;

	private int _normalDefenseModeProb;

	private int _strongDefenseModeProb;

	private int _weakHarassModeProb;

	private int _normalHarassModeProb;

	private int _strongHarassModeProb;

	private int _weakSiegeModeProb;

	private int _normalSiegeModeProb;

	private int _strongSiegeModeProb;

	private PairValueIntString _weakRecruitIntervalProb;

	private PairValueIntString _normalRecruitIntervalProb;

	private PairValueIntString _strongRecruitIntervalProb;

	private int _totalDefensiveTroopCount;

	private PairValueIntString _defensiveUnitType1;

	private PairValueIntString _defensiveUnitType2;

	private PairValueIntString _defensiveUnitType3;

	private PairValueIntString _defensiveUnitType4;

	private PairValueIntString _defensiveUnitType5;

	private PairValueIntString _defensiveUnitType6;

	private PairValueIntString _defensiveUnitType7;

	private PairValueIntString _defensiveUnitType8;

	private int _castleWallTroopCount;

	private PairValueIntString _selectedMobilePatrolMode;

	private int _waypointPatrolInterval;

	private int _waypointPatrolGroupCount;

	private int _siegeEngineGoldThreshold;

	private int _siegeEnginePurchaseInterval;

	private int _protectionPatrolInterval;

	private int _protectionPatrolGroups;

	private PairValueIntString _selectedEconomyDefenseTroop;

	private int _economyDefenseTroopCount;

	private PairValueIntString _selectedBodyguardTroop;

	private int _bodyguardTroopCount;

	private PairValueIntString _selectedDiggerTroop;

	private int _diggerTroopCount;

	private int _dogCageTriggerLevel;

	private int _dogCageReleaseDelayLevel;

	private int _harassTroopBaseCount;

	private int _harassTroopCountVariance;

	private PairValueIntString _harassUnitType1;

	private PairValueIntString _harassUnitType2;

	private PairValueIntString _harassUnitType3;

	private PairValueIntString _harassUnitType4;

	private PairValueIntString _harassUnitType5;

	private PairValueIntString _harassUnitType6;

	private PairValueIntString _harassUnitType7;

	private PairValueIntString harassUnitType8;

	private int _harassSiegeEngineCount;

	private PairValueIntString _harassSiegeEngineType1;

	private PairValueIntString _harassSiegeEngineType2;

	private PairValueIntString _harassSiegeEngineType3;

	private PairValueIntString _harassSiegeEngineType4;

	private PairValueIntString _harassSiegeEngineType5;

	private PairValueIntString _harassSiegeEngineType6;

	private PairValueIntString _harassSiegeEngineType7;

	private PairValueIntString _harassSiegeEngineType8;

	private int _harassRetargetDelay;

	private int _siegeUnitBaseCount;

	private int _siegeUnitCountVariance;

	private PairValueIntString _selectedVanguardTroop;

	private int _selectedVanguardTroopLimit;

	private PairValueIntString _selectedBackupTroop;

	private int _backupTroopLimit;

	private int _backupTroopGroups;

	private PairValueIntString _selectedPatrolTroopType;

	private int _patrolTroopTypeLimit;

	private int _patrolTroopTypeGroups;

	private PairValueIntString _selectedEngageTroopType;

	private int _engageTroopLimit;

	private PairValueIntString _selectedSiegeProtectionType;

	private int _siegeProtectionLimit;

	private int _siegeProtectionGroupCount;

	private PairValueIntString _siegeArmyUnitType1;

	private PairValueIntString _siegeArmyUnitType2;

	private PairValueIntString _siegeArmyUnitType3;

	private PairValueIntString _siegeArmyUnitType4;

	private PairValueIntString _siegeArmyUnitType5;

	private PairValueIntString _siegeArmyUnitType6;

	private PairValueIntString _siegeArmyUnitType7;

	private PairValueIntString _siegeArmyUnitType8;

	private PairValueIntString _siegeArmyUnitType9;

	private PairValueIntString _siegeArmyUnitType10;

	private PairValueIntString _siegeArmyUnitType11;

	private PairValueIntString _siegeArmyUnitType12;

	private PairValueIntString _siegeArmyUnitType13;

	private PairValueIntString _siegeArmyUnitType14;

	private PairValueIntString _siegeArmyUnitType15;

	private PairValueIntString _siegeArmyUnitType16;

	private PairValueIntString _siegeArmyUnitType17;

	private PairValueIntString _siegeArmyUnitType18;

	private PairValueIntString _siegeArmyUnitType19;

	private PairValueIntString _siegeArmyUnitType20;

	private PairValueIntString _siegeArmyUnitType21;

	private PairValueIntString _siegeArmyUnitType22;

	private PairValueIntString _siegeArmyUnitType23;

	private PairValueIntString _siegeArmyUnitType24;

	private int _siegeArmyLimit;

	private int _siegeArmyGroupCount;

	private int _siege_max_troops;

	private int _siege_normal_wave_multiplier;

	private int _siege_high_gold_wave_multiplier;

	private PairValueIntString _siegeArmySiegeEngineType1;

	private PairValueIntString _siegeArmySiegeEngineType2;

	private PairValueIntString _siegeArmySiegeEngineType3;

	private PairValueIntString _siegeArmySiegeEngineType4;

	private PairValueIntString _siegeArmySiegeEngineType5;

	private PairValueIntString _siegeArmySiegeEngineType6;

	private PairValueIntString _siegeArmySiegeEngineType7;

	private PairValueIntString _siegeArmySiegeEngineType8;

	private int _engineerLimit;

	private PairValueIntString _selectedDiggerType;

	private int _diggerLimit;

	private int _assassinLimit;

	private int _laddermanLimit;

	private int _tunnelerLimit;

	private int _siegeAdvanceArmyThreshold;

	private int _siegeRetreatThreshold;

	private int _castleBombardmentDuration;

	private int _successRetreatDelay;

	private int _cowThrowInterval;

	private bool _UseAdvancedAI;

	private bool _UseAdvancedAI2;

	private PairValueIntString _targetingPreference;

	private int _cooperationProb;

	private int _jointSiegeTroopThreshold;

	private int _normalArcherStartingAmount;

	private int _normalSpearmanStartingAmount;

	private int _normalMacemanStartingAmount;

	private int _normalCrossbowmanStartingAmount;

	private int _normalPikemanStartingAmount;

	private int _normalSwordsmanStartingAmount;

	private int _normalKnightStartingAmount;

	private int _normalArabianArcherStartingAmount;

	private int _normalSlaveStartingAmount;

	private int _normalSlingerStartingAmount;

	private int _normalAssassinStartingAmount;

	private int _normalHorseArcherStartingAmount;

	private int _normalArabianSwordsmanStartingAmount;

	private int _normalFireThrowerStartingAmount;

	private int _normalFireBallistaStartingAmount;

	private int _normalSkirmisherStartingAmount;

	private int _normalSapperStartingAmount;

	private int _normalCamelLancerStartingAmount;

	private int _normalHeavyCamelStartingAmount;

	private int _normalDemolisherStartingAmount;

	private int _normalEunuchStartingAmount;

	private int _normalHealerStartingAmount;

	private int _normalAmbusherStartingAmount;

	private int _normalEngineerStartingAmount;

	private int _normalMonkStartingAmount;

	private int _deathmatchArcherStartingAmount;

	private int _deathmatchSpearmanStartingAmount;

	private int _deathmatchMacemanStartingAmount;

	private int _deathmatchCrossbowmanStartingAmount;

	private int _deathmatchPikemanStartingAmount;

	private int _deathmatchSwordsmanStartingAmount;

	private int _deathmatchKnightStartingAmount;

	private int _deathmatchArabianArcherStartingAmount;

	private int _deathmatchSlaveStartingAmount;

	private int _deathmatchSlingerStartingAmount;

	private int _deathmatchAssassinStartingAmount;

	private int _deathmatchHorseArcherStartingAmount;

	private int _deathmatchArabianSwordsmanStartingAmount;

	private int _deathmatchFireThrowerStartingAmount;

	private int _deathmatchFireBallistaStartingAmount;

	private int _deathmatchSkirmisherStartingAmount;

	private int _deathmatchSapperStartingAmount;

	private int _deathmatchCamelLancerStartingAmount;

	private int _deathmatchHeavyCamelStartingAmount;

	private int _deathmatchDemolisherStartingAmount;

	private int _deathmatchEunuchStartingAmount;

	private int _deathmatchHealerStartingAmount;

	private int _deathmatchAmbusherStartingAmount;

	private int _deathmatchEngineerStartingAmount;

	private int _deathmatchMonkStartingAmount;

	private int _crusaderArcherStartingAmount;

	private int _crusaderSpearmanStartingAmount;

	private int _crusaderMacemanStartingAmount;

	private int _crusaderCrossbowmanStartingAmount;

	private int _crusaderPikemanStartingAmount;

	private int _crusaderSwordsmanStartingAmount;

	private int _crusaderKnightStartingAmount;

	private int _crusaderArabianArcherStartingAmount;

	private int _crusaderSlaveStartingAmount;

	private int _crusaderSlingerStartingAmount;

	private int _crusaderAssassinStartingAmount;

	private int _crusaderHorseArcherStartingAmount;

	private int _crusaderArabianSwordsmanStartingAmount;

	private int _crusaderFireThrowerStartingAmount;

	private int _crusaderFireBallistaStartingAmount;

	private int _crusaderSkirmisherStartingAmount;

	private int _crusaderSapperStartingAmount;

	private int _crusaderCamelLancerStartingAmount;

	private int _crusaderHeavyCamelStartingAmount;

	private int _crusaderDemolisherStartingAmount;

	private int _crusaderEunuchStartingAmount;

	private int _crusaderHealerStartingAmount;

	private int _crusaderAmbusherStartingAmount;

	private int _crusaderEngineerStartingAmount;

	private int _crusaderMonkStartingAmount;

	public static PnlEditLordViewModel instance;

	public bool AreChangesPresent
	{
		get
		{
			return _areChangesPresent;
		}
		set
		{
			_areChangesPresent = value;
			SaveButtonContents = (_areChangesPresent ? "Save*" : "Save");
		}
	}

	public string EditorTitle
	{
		get
		{
			return _editorTitle;
		}
		set
		{
			_editorTitle = value;
			OnPropertyChanged("EditorTitle");
		}
	}

	public string SaveButtonContents
	{
		get
		{
			return _saveButtonContents;
		}
		set
		{
			_saveButtonContents = value;
			OnPropertyChanged("SaveButtonContents");
		}
	}

	public List<PairValueIntString> LordFlagTypes { get; private set; }

	public PairValueIntString SelectedFlag
	{
		get
		{
			return _selectedFlag;
		}
		set
		{
			_selectedFlag = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.flag_type = _selectedFlag.Value;
			OnPropertyChanged("SelectedFlag");
		}
	}

	public List<PairValueIntString> LordFactions { get; private set; }

	public PairValueIntString SelectedLordFaction
	{
		get
		{
			return _selectedLordFaction;
		}
		set
		{
			_selectedLordFaction = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.lord_gfx_type = _selectedLordFaction.Value;
			OnPropertyChanged("SelectedLordFaction");
		}
	}

	public List<PairValueIntString> LordPowerDisplayLevels { get; private set; }

	public PairValueIntString SelectedLordPowerDisplayLevel
	{
		get
		{
			return _selectedLordPowerDisplayLevel;
		}
		set
		{
			_selectedLordPowerDisplayLevel = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.lord_power_display_level = _selectedLordPowerDisplayLevel.Value;
			OnPropertyChanged("SelectedLordPowerDisplayLevel");
		}
	}

	public int HPPercentage
	{
		get
		{
			return _hpPercentage;
		}
		set
		{
			if (_hpPercentage != value)
			{
				_hpPercentage = value;
				AreChangesPresent = true;
				Helpers.SelectedLordConfig.lord.lord_hps_percent = _hpPercentage;
				OnPropertyChanged("HPPercentage");
			}
		}
	}

	public int CriticalPopThreshold
	{
		get
		{
			return _criticalPopThreshold;
		}
		set
		{
			_criticalPopThreshold = value;
			if (!inSetupPhase && value > LowPopThreshold)
			{
				LowPopThreshold = _criticalPopThreshold + 1;
			}
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.vlow_popularity = _criticalPopThreshold * 100;
			OnPropertyChanged("CriticalPopThreshold");
		}
	}

	public int LowPopThreshold
	{
		get
		{
			return _lowPopThreshold;
		}
		set
		{
			if (!inSetupPhase)
			{
				if (value < CriticalPopThreshold + 1)
				{
					_lowPopThreshold = CriticalPopThreshold + 1;
				}
				else
				{
					_lowPopThreshold = value;
				}
				if (value > HighPopThreshold)
				{
					HighPopThreshold = _lowPopThreshold + 1;
				}
			}
			else
			{
				_lowPopThreshold = value;
			}
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.low_popularity = _lowPopThreshold * 100;
			OnPropertyChanged("LowPopThreshold");
		}
	}

	public int HighPopThreshold
	{
		get
		{
			return _highPopThreshold;
		}
		set
		{
			if (!inSetupPhase)
			{
				if (value < LowPopThreshold + 1)
				{
					_highPopThreshold = LowPopThreshold + 1;
				}
				else
				{
					_highPopThreshold = value;
				}
			}
			else
			{
				_highPopThreshold = value;
			}
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.high_popularity = _highPopThreshold * 100;
			OnPropertyChanged("HighPopThreshold");
		}
	}

	public int DoubleRationThreshold
	{
		get
		{
			return _doubleRationThreshold;
		}
		set
		{
			_doubleRationThreshold = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.ration_boost = _doubleRationThreshold;
			OnPropertyChanged("DoubleRationThreshold");
		}
	}

	public List<PairValueIntString> TaxLevels { get; private set; }

	public PairValueIntString MinTax
	{
		get
		{
			return _minTax;
		}
		set
		{
			_minTax = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.min_tax = _minTax.Value;
			OnPropertyChanged("MinTax");
		}
	}

	public PairValueIntString MaxTax
	{
		get
		{
			return _maxTax;
		}
		set
		{
			_maxTax = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.max_tax = _maxTax.Value;
			OnPropertyChanged("MaxTax");
		}
	}

	public List<PairValueIntString> AleAllowances { get; private set; }

	public PairValueIntString SelectedAleAllowance
	{
		get
		{
			return _selectedAleAllowance;
		}
		set
		{
			_selectedAleAllowance = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.use_of_ale = _selectedAleAllowance.Value;
			OnPropertyChanged("SelectedAleAllowance");
		}
	}

	public List<PairValueIntString> ReligionAllowances { get; private set; }

	public PairValueIntString SelectedReligionAllowance
	{
		get
		{
			return _selectedReligionAllowance;
		}
		set
		{
			_selectedReligionAllowance = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.use_of_religion = _selectedReligionAllowance.Value;
			OnPropertyChanged("SelectedReligionAllowance");
		}
	}

	public int FoodPurchaseAmount
	{
		get
		{
			return _foodPurchaseAmount;
		}
		set
		{
			_foodPurchaseAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.buy_food_amount = _foodPurchaseAmount;
			OnPropertyChanged("FoodPurchaseAmount");
		}
	}

	public int WeaponPurchaseAmount
	{
		get
		{
			return _weaponPurchaseAmount;
		}
		set
		{
			_weaponPurchaseAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.buy_weapons = _weaponPurchaseAmount;
			OnPropertyChanged("WeaponPurchaseAmount");
		}
	}

	public int PesterGoodsDelay
	{
		get
		{
			return _pesterGoodsDelay;
		}
		set
		{
			_pesterGoodsDelay = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.pester_for_goods_delay = _pesterGoodsDelay;
			OnPropertyChanged("PesterGoodsDelay");
		}
	}

	public int GoodsSendMargin
	{
		get
		{
			return _goodsSendMargin;
		}
		set
		{
			_goodsSendMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.send_goods_margin = _goodsSendMargin;
			OnPropertyChanged("GoodsSendMargin");
		}
	}

	public int SellFoodMargin
	{
		get
		{
			return _sellFoodMargin;
		}
		set
		{
			_sellFoodMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_food_at = _sellFoodMargin;
			OnPropertyChanged("SellFoodMargin");
		}
	}

	public int AppleBuyMargin
	{
		get
		{
			return _appleBuyMargin;
		}
		set
		{
			_appleBuyMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.buy_apples_at = _appleBuyMargin;
			OnPropertyChanged("AppleBuyMargin");
		}
	}

	public int CheeseBuyMargin
	{
		get
		{
			return _cheeseBuyMargin;
		}
		set
		{
			_cheeseBuyMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.buy_cheese_at = _cheeseBuyMargin;
			OnPropertyChanged("CheeseBuyMargin");
		}
	}

	public int BreadBuyMargin
	{
		get
		{
			return _breadBuyMargin;
		}
		set
		{
			_breadBuyMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.buy_bread_at = _breadBuyMargin;
			OnPropertyChanged("BreadBuyMargin");
		}
	}

	public int WheatBuyMargin
	{
		get
		{
			return _wheatBuyMargin;
		}
		set
		{
			_wheatBuyMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.buy_wheat_at = _wheatBuyMargin;
			OnPropertyChanged("WheatBuyMargin");
		}
	}

	public int HopBuyMargin
	{
		get
		{
			return _hopBuyMargin;
		}
		set
		{
			_hopBuyMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.buy_hops_at = _hopBuyMargin;
			OnPropertyChanged("HopBuyMargin");
		}
	}

	public int WoodSellMargin
	{
		get
		{
			return _woodSellMargin;
		}
		set
		{
			_woodSellMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.trade_wood_at = _woodSellMargin;
			OnPropertyChanged("WoodSellMargin");
		}
	}

	public int StoneSellMargin
	{
		get
		{
			return _stoneSellMargin;
		}
		set
		{
			_stoneSellMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.trade_stone_at = _stoneSellMargin;
			OnPropertyChanged("StoneSellMargin");
		}
	}

	public int MiscSellMargin
	{
		get
		{
			return _miscSellMargin;
		}
		set
		{
			_miscSellMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.trade_resources_at = _miscSellMargin;
			OnPropertyChanged("MiscSellMargin");
		}
	}

	public int WeaponSellMargin
	{
		get
		{
			return _weaponSellMargin;
		}
		set
		{
			_weaponSellMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.trade_weapons_at = _weaponSellMargin;
			OnPropertyChanged("WeaponSellMargin");
		}
	}

	public int AleSellMargin
	{
		get
		{
			return _aleSellMargin;
		}
		set
		{
			_aleSellMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.trade_ale_at = _aleSellMargin;
			OnPropertyChanged("AleSellMargin");
		}
	}

	public int PitchSellMargin
	{
		get
		{
			return _pitchSellMargin;
		}
		set
		{
			_pitchSellMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.trade_pitch_at = _pitchSellMargin;
			OnPropertyChanged("PitchSellMargin");
		}
	}

	public int FlourSellMargin
	{
		get
		{
			return _flourSellMargin;
		}
		set
		{
			_flourSellMargin = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.trade_flour_at = _flourSellMargin;
			OnPropertyChanged("FlourSellMargin");
		}
	}

	public int SaleVariance
	{
		get
		{
			return _saleVariance;
		}
		set
		{
			_saleVariance = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.trade_minimum = _saleVariance;
			OnPropertyChanged("SaleVariance");
		}
	}

	public int BaseGoldReserve
	{
		get
		{
			return _baseGoldReserve;
		}
		set
		{
			_baseGoldReserve = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.base_gold_reserves = _baseGoldReserve;
			OnPropertyChanged("BaseGoldReserve");
		}
	}

	public int BuildDelay
	{
		get
		{
			return _buildDelay;
		}
		set
		{
			_buildDelay = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.build_rate = _buildDelay;
			OnPropertyChanged("BuildDelay");
		}
	}

	public int BuildingRepairDelay
	{
		get
		{
			return _buildingRepairDelay;
		}
		set
		{
			_buildingRepairDelay = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.crushed_building_delay = _buildingRepairDelay;
			OnPropertyChanged("BuildingRepairDelay");
		}
	}

	public int PopulationPerFarm
	{
		get
		{
			return _populationPerFarm;
		}
		set
		{
			_populationPerFarm = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.people_to_farm_ratio = _populationPerFarm;
			OnPropertyChanged("PopulationPerFarm");
		}
	}

	public int PopulationPerWoodCutter
	{
		get
		{
			return _populationPerWoodCutter;
		}
		set
		{
			_populationPerWoodCutter = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.extract_wood_ratio = _populationPerWoodCutter;
			OnPropertyChanged("PopulationPerWoodCutter");
		}
	}

	public int PopulationPerQuarry
	{
		get
		{
			return _populationPerQuarry;
		}
		set
		{
			_populationPerQuarry = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.extract_stone_ratio = _populationPerQuarry;
			OnPropertyChanged("PopulationPerQuarry");
		}
	}

	public int PopulationPerIronMine
	{
		get
		{
			return _populationPerIronMine;
		}
		set
		{
			_populationPerIronMine = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.extract_iron_ratio = _populationPerIronMine;
			OnPropertyChanged("PopulationPerIronMine");
		}
	}

	public int PopulationPerPitchDugout
	{
		get
		{
			return _populationPerPitchDugout;
		}
		set
		{
			_populationPerPitchDugout = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.extract_pitch_ratio = _populationPerPitchDugout;
			OnPropertyChanged("PopulationPerPitchDugout");
		}
	}

	public int MaximumFarmAmount
	{
		get
		{
			return _maximumFarmAmount;
		}
		set
		{
			_maximumFarmAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.max_farms = _maximumFarmAmount;
			OnPropertyChanged("MaximumFarmAmount");
		}
	}

	public int MaximumWoodCutterAmount
	{
		get
		{
			return _maximumWoodCutterAmount;
		}
		set
		{
			_maximumWoodCutterAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.max_woodcutters = _maximumWoodCutterAmount;
			OnPropertyChanged("MaximumWoodCutterAmount");
		}
	}

	public int MaximumQuarryAmount
	{
		get
		{
			return _maximumQuarryAmount;
		}
		set
		{
			_maximumQuarryAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.max_quarries = _maximumQuarryAmount;
			OnPropertyChanged("MaximumQuarryAmount");
		}
	}

	public int MaximumIronMineAmount
	{
		get
		{
			return _maximumIronMineAmount;
		}
		set
		{
			_maximumIronMineAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.max_mines = _maximumIronMineAmount;
			OnPropertyChanged("MaximumIronMineAmount");
		}
	}

	public int MaximumPitchDugoutAmount
	{
		get
		{
			return _maximumPitchDugoutAmount;
		}
		set
		{
			_maximumPitchDugoutAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.max_pitch_dugouts = _maximumPitchDugoutAmount;
			OnPropertyChanged("MaximumPitchDugoutAmount");
		}
	}

	public List<PairValueIntString> FarmChoices { get; private set; }

	public PairValueIntString SelectedFarm1
	{
		get
		{
			return _selectedFarm1;
		}
		set
		{
			_selectedFarm1 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.farm_types[0] = _selectedFarm1.Value;
			OnPropertyChanged("SelectedFarm1");
		}
	}

	public PairValueIntString SelectedFarm2
	{
		get
		{
			return _selectedFarm2;
		}
		set
		{
			_selectedFarm2 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.farm_types[1] = _selectedFarm2.Value;
			OnPropertyChanged("SelectedFarm2");
		}
	}

	public PairValueIntString SelectedFarm3
	{
		get
		{
			return _selectedFarm3;
		}
		set
		{
			_selectedFarm3 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.farm_types[2] = _selectedFarm3.Value;
			OnPropertyChanged("SelectedFarm3");
		}
	}

	public PairValueIntString SelectedFarm4
	{
		get
		{
			return _selectedFarm4;
		}
		set
		{
			_selectedFarm4 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.farm_types[3] = _selectedFarm4.Value;
			OnPropertyChanged("SelectedFarm4");
		}
	}

	public PairValueIntString SelectedFarm5
	{
		get
		{
			return _selectedFarm5;
		}
		set
		{
			_selectedFarm5 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.farm_types[4] = _selectedFarm5.Value;
			OnPropertyChanged("SelectedFarm5");
		}
	}

	public PairValueIntString SelectedFarm6
	{
		get
		{
			return _selectedFarm6;
		}
		set
		{
			_selectedFarm6 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.farm_types[5] = _selectedFarm6.Value;
			OnPropertyChanged("SelectedFarm6");
		}
	}

	public PairValueIntString SelectedFarm7
	{
		get
		{
			return _selectedFarm7;
		}
		set
		{
			_selectedFarm7 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.farm_types[6] = _selectedFarm7.Value;
			OnPropertyChanged("SelectedFarm7");
		}
	}

	public PairValueIntString SelectedFarm8
	{
		get
		{
			return _selectedFarm8;
		}
		set
		{
			_selectedFarm8 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.farm_types[7] = _selectedFarm8.Value;
			OnPropertyChanged("SelectedFarm8");
		}
	}

	public List<PairValueIntString> BlacksmithOperationModes { get; private set; }

	public PairValueIntString SelectedBlackSmithMode
	{
		get
		{
			return _selectedBlackSmithMode;
		}
		set
		{
			_selectedBlackSmithMode = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.blacksmiths_make = _selectedBlackSmithMode.Value;
			OnPropertyChanged("SelectedBlackSmithMode");
		}
	}

	public List<PairValueIntString> FletcherOperationModes { get; private set; }

	public PairValueIntString SelectedFletcherMode
	{
		get
		{
			return _selectedFletcherMode;
		}
		set
		{
			_selectedFletcherMode = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.fletchers_make = _selectedFletcherMode.Value;
			OnPropertyChanged("SelectedFletcherMode");
		}
	}

	public List<PairValueIntString> PoleturnerOperationModes { get; private set; }

	public PairValueIntString SelectedPoleturnerMode
	{
		get
		{
			return _selectedPoleturnerMode;
		}
		set
		{
			_selectedPoleturnerMode = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.poleturners_make = _selectedPoleturnerMode.Value;
			OnPropertyChanged("SelectedPoleturnerMode");
		}
	}

	public List<PairValueIntString> GameResources { get; private set; }

	public PairValueIntString BannedResource1
	{
		get
		{
			return _bannedResource1;
		}
		set
		{
			_bannedResource1 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[0] = _bannedResource1.Value;
			OnPropertyChanged("BannedResource1");
		}
	}

	public PairValueIntString BannedResource2
	{
		get
		{
			return _bannedResource2;
		}
		set
		{
			_bannedResource2 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[1] = _bannedResource2.Value;
			OnPropertyChanged("BannedResource2");
		}
	}

	public PairValueIntString BannedResource3
	{
		get
		{
			return _bannedResource3;
		}
		set
		{
			_bannedResource3 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[2] = _bannedResource3.Value;
			OnPropertyChanged("BannedResource3");
		}
	}

	public PairValueIntString BannedResource4
	{
		get
		{
			return _bannedResource4;
		}
		set
		{
			_bannedResource4 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[3] = _bannedResource4.Value;
			OnPropertyChanged("BannedResource4");
		}
	}

	public PairValueIntString BannedResource5
	{
		get
		{
			return _bannedResource5;
		}
		set
		{
			_bannedResource5 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[4] = _bannedResource5.Value;
			OnPropertyChanged("BannedResource5");
		}
	}

	public PairValueIntString BannedResource6
	{
		get
		{
			return _bannedResource6;
		}
		set
		{
			_bannedResource6 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[5] = _bannedResource6.Value;
			OnPropertyChanged("BannedResource6");
		}
	}

	public PairValueIntString BannedResource7
	{
		get
		{
			return _bannedResource7;
		}
		set
		{
			_bannedResource7 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[6] = _bannedResource7.Value;
			OnPropertyChanged("BannedResource7");
		}
	}

	public PairValueIntString BannedResource8
	{
		get
		{
			return _bannedResource8;
		}
		set
		{
			_bannedResource8 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[7] = _bannedResource8.Value;
			OnPropertyChanged("BannedResource8");
		}
	}

	public PairValueIntString BannedResource9
	{
		get
		{
			return _bannedResource9;
		}
		set
		{
			_bannedResource9 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[8] = _bannedResource9.Value;
			OnPropertyChanged("BannedResource9");
		}
	}

	public PairValueIntString BannedResource10
	{
		get
		{
			return _bannedResource10;
		}
		set
		{
			_bannedResource10 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[9] = _bannedResource10.Value;
			OnPropertyChanged("BannedResource10");
		}
	}

	public PairValueIntString BannedResource11
	{
		get
		{
			return _bannedResource11;
		}
		set
		{
			_bannedResource11 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[10] = _bannedResource11.Value;
			OnPropertyChanged("BannedResource11");
		}
	}

	public PairValueIntString BannedResource12
	{
		get
		{
			return _bannedResource12;
		}
		set
		{
			_bannedResource12 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[11] = _bannedResource12.Value;
			OnPropertyChanged("BannedResource12");
		}
	}

	public PairValueIntString BannedResource13
	{
		get
		{
			return _bannedResource13;
		}
		set
		{
			_bannedResource13 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[12] = _bannedResource13.Value;
			OnPropertyChanged("BannedResource13");
		}
	}

	public PairValueIntString BannedResource14
	{
		get
		{
			return _bannedResource14;
		}
		set
		{
			_bannedResource14 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[13] = _bannedResource14.Value;
			OnPropertyChanged("BannedResource14");
		}
	}

	public PairValueIntString BannedResource15
	{
		get
		{
			return _bannedResource15;
		}
		set
		{
			_bannedResource15 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.sell_all[14] = _bannedResource15.Value;
			OnPropertyChanged("BannedResource15");
		}
	}

	public List<PairValueIntString> RecruitmentIntervals { get; private set; }

	public int WeakDefenseModeProb
	{
		get
		{
			return _weakDefenseModeProb;
		}
		set
		{
			_weakDefenseModeProb = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.chance_of_defensive[1] = _weakDefenseModeProb;
			OnPropertyChanged("WeakDefenseModeProb");
		}
	}

	public int NormalDefenseModeProb
	{
		get
		{
			return _normalDefenseModeProb;
		}
		set
		{
			_normalDefenseModeProb = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.chance_of_defensive[0] = _normalDefenseModeProb;
			OnPropertyChanged("NormalDefenseModeProb");
		}
	}

	public int StrongDefenseModeProb
	{
		get
		{
			return _strongDefenseModeProb;
		}
		set
		{
			_strongDefenseModeProb = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.chance_of_defensive[2] = _strongDefenseModeProb;
			OnPropertyChanged("StrongDefenseModeProb");
		}
	}

	public int WeakHarassModeProb
	{
		get
		{
			return _weakHarassModeProb;
		}
		set
		{
			_weakHarassModeProb = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.chance_of_harrasment[1] = _weakHarassModeProb;
			OnPropertyChanged("WeakHarassModeProb");
		}
	}

	public int NormalHarassModeProb
	{
		get
		{
			return _normalHarassModeProb;
		}
		set
		{
			_normalHarassModeProb = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.chance_of_harrasment[0] = _normalHarassModeProb;
			OnPropertyChanged("NormalHarassModeProb");
		}
	}

	public int StrongHarassModeProb
	{
		get
		{
			return _strongHarassModeProb;
		}
		set
		{
			_strongHarassModeProb = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.chance_of_harrasment[2] = _strongHarassModeProb;
			OnPropertyChanged("StrongHarassModeProb");
		}
	}

	public int WeakSiegeModeProb
	{
		get
		{
			return _weakSiegeModeProb;
		}
		set
		{
			_weakSiegeModeProb = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.chance_of_seiging[1] = _weakSiegeModeProb;
			OnPropertyChanged("WeakSiegeModeProb");
		}
	}

	public int NormalSiegeModeProb
	{
		get
		{
			return _normalSiegeModeProb;
		}
		set
		{
			_normalSiegeModeProb = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.chance_of_seiging[0] = _normalSiegeModeProb;
			OnPropertyChanged("NormalSiegeModeProb");
		}
	}

	public int StrongSiegeModeProb
	{
		get
		{
			return _strongSiegeModeProb;
		}
		set
		{
			_strongSiegeModeProb = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.chance_of_seiging[2] = _strongSiegeModeProb;
			OnPropertyChanged("StrongSiegeModeProb");
		}
	}

	public PairValueIntString WeakRecruitIntervalProb
	{
		get
		{
			return _weakRecruitIntervalProb;
		}
		set
		{
			_weakRecruitIntervalProb = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.troop_production_rate[1] = _weakRecruitIntervalProb.Value;
			OnPropertyChanged("WeakRecruitIntervalProb");
		}
	}

	public PairValueIntString NormalRecruitIntervalProb
	{
		get
		{
			return _normalRecruitIntervalProb;
		}
		set
		{
			_normalRecruitIntervalProb = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.troop_production_rate[0] = _normalRecruitIntervalProb.Value;
			OnPropertyChanged("NormalRecruitIntervalProb");
		}
	}

	public PairValueIntString StrongRecruitIntervalProb
	{
		get
		{
			return _strongRecruitIntervalProb;
		}
		set
		{
			_strongRecruitIntervalProb = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.troop_production_rate[2] = _strongRecruitIntervalProb.Value;
			OnPropertyChanged("StrongRecruitIntervalProb");
		}
	}

	public List<PairValueIntString> AllTroops { get; private set; }

	public List<PairValueIntString> AllTroopsReactive { get; private set; }

	public List<PairValueIntString> DiggerTroops { get; private set; }

	public List<PairValueIntString> SiegeEngines { get; private set; }

	public List<PairValueIntString> SiegeEngineChimps { get; private set; }

	public int TotalDefensiveTroopCount
	{
		get
		{
			return _totalDefensiveTroopCount;
		}
		set
		{
			_totalDefensiveTroopCount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.defensive_trigger_level = _totalDefensiveTroopCount;
			OnPropertyChanged("TotalDefensiveTroopCount");
		}
	}

	public PairValueIntString DefensiveUnitType1
	{
		get
		{
			return _defensiveUnitType1;
		}
		set
		{
			_defensiveUnitType1 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.defensive_troops[0] = _defensiveUnitType1.Value;
			OnPropertyChanged("DefensiveUnitType1");
		}
	}

	public PairValueIntString DefensiveUnitType2
	{
		get
		{
			return _defensiveUnitType2;
		}
		set
		{
			_defensiveUnitType2 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.defensive_troops[1] = _defensiveUnitType2.Value;
			OnPropertyChanged("DefensiveUnitType2");
		}
	}

	public PairValueIntString DefensiveUnitType3
	{
		get
		{
			return _defensiveUnitType3;
		}
		set
		{
			_defensiveUnitType3 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.defensive_troops[2] = _defensiveUnitType3.Value;
			OnPropertyChanged("DefensiveUnitType3");
		}
	}

	public PairValueIntString DefensiveUnitType4
	{
		get
		{
			return _defensiveUnitType4;
		}
		set
		{
			_defensiveUnitType4 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.defensive_troops[3] = _defensiveUnitType4.Value;
			OnPropertyChanged("DefensiveUnitType4");
		}
	}

	public PairValueIntString DefensiveUnitType5
	{
		get
		{
			return _defensiveUnitType5;
		}
		set
		{
			_defensiveUnitType5 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.defensive_troops[4] = _defensiveUnitType5.Value;
			OnPropertyChanged("DefensiveUnitType5");
		}
	}

	public PairValueIntString DefensiveUnitType6
	{
		get
		{
			return _defensiveUnitType6;
		}
		set
		{
			_defensiveUnitType6 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.defensive_troops[5] = _defensiveUnitType6.Value;
			OnPropertyChanged("DefensiveUnitType6");
		}
	}

	public PairValueIntString DefensiveUnitType7
	{
		get
		{
			return _defensiveUnitType7;
		}
		set
		{
			_defensiveUnitType7 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.defensive_troops[6] = _defensiveUnitType7.Value;
			OnPropertyChanged("DefensiveUnitType7");
		}
	}

	public PairValueIntString DefensiveUnitType8
	{
		get
		{
			return _defensiveUnitType8;
		}
		set
		{
			_defensiveUnitType8 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.defensive_troops[7] = _defensiveUnitType8.Value;
			OnPropertyChanged("DefensiveUnitType8");
		}
	}

	public int CastleWallTroopCount
	{
		get
		{
			return _castleWallTroopCount;
		}
		set
		{
			_castleWallTroopCount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.defense_patrol_trigger_level = _castleWallTroopCount;
			OnPropertyChanged("CastleWallTroopCount");
		}
	}

	public PairValueIntString SelectedMobilePatrolMode
	{
		get
		{
			return _selectedMobilePatrolMode;
		}
		set
		{
			_selectedMobilePatrolMode = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.defense_patrol_style = _selectedMobilePatrolMode.Value;
			OnPropertyChanged("SelectedMobilePatrolMode");
		}
	}

	public List<PairValueIntString> MobilePatrolStyles { get; private set; }

	public int WaypointPatrolInterval
	{
		get
		{
			return _waypointPatrolInterval;
		}
		set
		{
			_waypointPatrolInterval = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.move_mobile_defenders = _waypointPatrolInterval;
			OnPropertyChanged("WaypointPatrolInterval");
		}
	}

	public int WaypointPatrolGroupCount
	{
		get
		{
			return _waypointPatrolGroupCount;
		}
		set
		{
			_waypointPatrolGroupCount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.max_mobile_groups = _waypointPatrolGroupCount;
			OnPropertyChanged("WaypointPatrolGroupCount");
		}
	}

	public int SiegeEngineGoldThreshold
	{
		get
		{
			return _siegeEngineGoldThreshold;
		}
		set
		{
			_siegeEngineGoldThreshold = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.buy_defense_machines_at = _siegeEngineGoldThreshold;
			OnPropertyChanged("SiegeEngineGoldThreshold");
		}
	}

	public int SiegeEnginePurchaseInterval
	{
		get
		{
			return _siegeEnginePurchaseInterval;
		}
		set
		{
			_siegeEnginePurchaseInterval = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.buy_defense_machines_delay = _siegeEnginePurchaseInterval;
			OnPropertyChanged("SiegeEnginePurchaseInterval");
		}
	}

	public int ProtectionPatrolInterval
	{
		get
		{
			return _protectionPatrolInterval;
		}
		set
		{
			_protectionPatrolInterval = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.defense_patrol_delay = _protectionPatrolInterval;
			OnPropertyChanged("ProtectionPatrolInterval");
		}
	}

	public int ProtectionPatrolGroups
	{
		get
		{
			return _protectionPatrolGroups;
		}
		set
		{
			_protectionPatrolGroups = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.defense_patrols = _protectionPatrolGroups;
			OnPropertyChanged("ProtectionPatrolGroups");
		}
	}

	public PairValueIntString SelectedEconomyDefenseTroop
	{
		get
		{
			return _selectedEconomyDefenseTroop;
		}
		set
		{
			_selectedEconomyDefenseTroop = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.economy_protection_type = _selectedEconomyDefenseTroop.Value;
			OnPropertyChanged("SelectedEconomyDefenseTroop");
		}
	}

	public int EconomyDefenseTroopCount
	{
		get
		{
			return _economyDefenseTroopCount;
		}
		set
		{
			_economyDefenseTroopCount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.economy_protection_number = _economyDefenseTroopCount;
			OnPropertyChanged("EconomyDefenseTroopCount");
		}
	}

	public PairValueIntString SelectedBodyguardTroop
	{
		get
		{
			return _selectedBodyguardTroop;
		}
		set
		{
			_selectedBodyguardTroop = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.bodyguard_type = _selectedBodyguardTroop.Value;
			OnPropertyChanged("SelectedBodyguardTroop");
		}
	}

	public int BodyguardTroopCount
	{
		get
		{
			return _bodyguardTroopCount;
		}
		set
		{
			_bodyguardTroopCount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.bodyguard_number = _bodyguardTroopCount;
			OnPropertyChanged("BodyguardTroopCount");
		}
	}

	public PairValueIntString SelectedDiggerTroop
	{
		get
		{
			return _selectedDiggerTroop;
		}
		set
		{
			_selectedDiggerTroop = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.moat_digger_type = _selectedDiggerTroop.Value;
			OnPropertyChanged("SelectedDiggerTroop");
		}
	}

	public int DiggerTroopCount
	{
		get
		{
			return _diggerTroopCount;
		}
		set
		{
			_diggerTroopCount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.moat_diggers = _diggerTroopCount;
			OnPropertyChanged("DiggerTroopCount");
		}
	}

	public int DogCageTriggerLevel
	{
		get
		{
			return _dogCageTriggerLevel;
		}
		set
		{
			_dogCageTriggerLevel = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.dog_points_count = _dogCageTriggerLevel;
			OnPropertyChanged("DogCageTriggerLevel");
		}
	}

	public int DogCageReleaseDelayLevel
	{
		get
		{
			return _dogCageReleaseDelayLevel;
		}
		set
		{
			_dogCageReleaseDelayLevel = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.dog_release_timing = _dogCageReleaseDelayLevel;
			OnPropertyChanged("DogCageReleaseDelayLevel");
		}
	}

	public int HarassTroopBaseCount
	{
		get
		{
			return _harassTroopBaseCount;
		}
		set
		{
			_harassTroopBaseCount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_trigger_level = _harassTroopBaseCount;
			OnPropertyChanged("HarassTroopBaseCount");
		}
	}

	public int HarassTroopCountVariance
	{
		get
		{
			return _harassTroopCountVariance;
		}
		set
		{
			_harassTroopCountVariance = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_trigger_variance = _harassTroopCountVariance;
			OnPropertyChanged("HarassTroopCountVariance");
		}
	}

	public PairValueIntString HarassUnitType1
	{
		get
		{
			return _harassUnitType1;
		}
		set
		{
			_harassUnitType1 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_troops[0] = _harassUnitType1.Value;
			OnPropertyChanged("HarassUnitType1");
		}
	}

	public PairValueIntString HarassUnitType2
	{
		get
		{
			return _harassUnitType2;
		}
		set
		{
			_harassUnitType2 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_troops[1] = _harassUnitType2.Value;
			OnPropertyChanged("HarassUnitType2");
		}
	}

	public PairValueIntString HarassUnitType3
	{
		get
		{
			return _harassUnitType3;
		}
		set
		{
			_harassUnitType3 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_troops[2] = _harassUnitType3.Value;
			OnPropertyChanged("HarassUnitType3");
		}
	}

	public PairValueIntString HarassUnitType4
	{
		get
		{
			return _harassUnitType4;
		}
		set
		{
			_harassUnitType4 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_troops[3] = _harassUnitType4.Value;
			OnPropertyChanged("HarassUnitType4");
		}
	}

	public PairValueIntString HarassUnitType5
	{
		get
		{
			return _harassUnitType5;
		}
		set
		{
			_harassUnitType5 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_troops[4] = _harassUnitType5.Value;
			OnPropertyChanged("HarassUnitType5");
		}
	}

	public PairValueIntString HarassUnitType6
	{
		get
		{
			return _harassUnitType6;
		}
		set
		{
			_harassUnitType6 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_troops[5] = _harassUnitType6.Value;
			OnPropertyChanged("HarassUnitType6");
		}
	}

	public PairValueIntString HarassUnitType7
	{
		get
		{
			return _harassUnitType7;
		}
		set
		{
			_harassUnitType7 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_troops[6] = _harassUnitType7.Value;
			OnPropertyChanged("HarassUnitType7");
		}
	}

	public PairValueIntString HarassUnitType8
	{
		get
		{
			return harassUnitType8;
		}
		set
		{
			harassUnitType8 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_troops[7] = harassUnitType8.Value;
			OnPropertyChanged("HarassUnitType8");
		}
	}

	public int HarassSiegeEngineCount
	{
		get
		{
			return _harassSiegeEngineCount;
		}
		set
		{
			_harassSiegeEngineCount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.max_harrasment_machines = _harassSiegeEngineCount;
			OnPropertyChanged("HarassSiegeEngineCount");
		}
	}

	public PairValueIntString HarassSiegeEngineType1
	{
		get
		{
			return _harassSiegeEngineType1;
		}
		set
		{
			_harassSiegeEngineType1 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_machines[0] = _harassSiegeEngineType1.Value;
			OnPropertyChanged("HarassSiegeEngineType1");
		}
	}

	public PairValueIntString HarassSiegeEngineType2
	{
		get
		{
			return _harassSiegeEngineType2;
		}
		set
		{
			_harassSiegeEngineType2 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_machines[1] = _harassSiegeEngineType2.Value;
			OnPropertyChanged("HarassSiegeEngineType2");
		}
	}

	public PairValueIntString HarassSiegeEngineType3
	{
		get
		{
			return _harassSiegeEngineType3;
		}
		set
		{
			_harassSiegeEngineType3 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_machines[2] = _harassSiegeEngineType3.Value;
			OnPropertyChanged("HarassSiegeEngineType3");
		}
	}

	public PairValueIntString HarassSiegeEngineType4
	{
		get
		{
			return _harassSiegeEngineType4;
		}
		set
		{
			_harassSiegeEngineType4 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_machines[3] = _harassSiegeEngineType4.Value;
			OnPropertyChanged("HarassSiegeEngineType4");
		}
	}

	public PairValueIntString HarassSiegeEngineType5
	{
		get
		{
			return _harassSiegeEngineType5;
		}
		set
		{
			_harassSiegeEngineType5 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_machines[4] = _harassSiegeEngineType5.Value;
			OnPropertyChanged("HarassSiegeEngineType5");
		}
	}

	public PairValueIntString HarassSiegeEngineType6
	{
		get
		{
			return _harassSiegeEngineType6;
		}
		set
		{
			_harassSiegeEngineType6 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_machines[5] = _harassSiegeEngineType6.Value;
			OnPropertyChanged("HarassSiegeEngineType6");
		}
	}

	public PairValueIntString HarassSiegeEngineType7
	{
		get
		{
			return _harassSiegeEngineType7;
		}
		set
		{
			_harassSiegeEngineType7 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_machines[6] = _harassSiegeEngineType7.Value;
			OnPropertyChanged("HarassSiegeEngineType7");
		}
	}

	public PairValueIntString HarassSiegeEngineType8
	{
		get
		{
			return _harassSiegeEngineType8;
		}
		set
		{
			_harassSiegeEngineType8 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrasment_machines[7] = _harassSiegeEngineType8.Value;
			OnPropertyChanged("HarassSiegeEngineType8");
		}
	}

	public int HarassRetargetDelay
	{
		get
		{
			return _harassRetargetDelay;
		}
		set
		{
			_harassRetargetDelay = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.harrass_delay = _harassRetargetDelay;
			OnPropertyChanged("HarassRetargetDelay");
		}
	}

	public int SiegeUnitBaseCount
	{
		get
		{
			return _siegeUnitBaseCount;
		}
		set
		{
			_siegeUnitBaseCount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_trigger_level = _siegeUnitBaseCount;
			OnPropertyChanged("SiegeUnitBaseCount");
		}
	}

	public int SiegeUnitCountVariance
	{
		get
		{
			return _siegeUnitCountVariance;
		}
		set
		{
			_siegeUnitCountVariance = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_trigger_variance = _siegeUnitCountVariance;
			OnPropertyChanged("SiegeUnitCountVariance");
		}
	}

	public PairValueIntString SelectedVanguardTroop
	{
		get
		{
			return _selectedVanguardTroop;
		}
		set
		{
			_selectedVanguardTroop = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_herring_troop = _selectedVanguardTroop.Value;
			OnPropertyChanged("SelectedVanguardTroop");
		}
	}

	public int SelectedVanguardTroopLimit
	{
		get
		{
			return _selectedVanguardTroopLimit;
		}
		set
		{
			_selectedVanguardTroopLimit = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_herring_amount = _selectedVanguardTroopLimit;
			OnPropertyChanged("SelectedVanguardTroopLimit");
		}
	}

	public PairValueIntString SelectedBackupTroop
	{
		get
		{
			return _selectedBackupTroop;
		}
		set
		{
			_selectedBackupTroop = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_cover_troop = _selectedBackupTroop.Value;
			OnPropertyChanged("SelectedBackupTroop");
		}
	}

	public int BackupTroopLimit
	{
		get
		{
			return _backupTroopLimit;
		}
		set
		{
			_backupTroopLimit = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_cover_amount = _backupTroopLimit;
			OnPropertyChanged("BackupTroopLimit");
		}
	}

	public int BackupTroopGroups
	{
		get
		{
			return _backupTroopGroups;
		}
		set
		{
			_backupTroopGroups = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_cover_tribes = _backupTroopGroups;
			OnPropertyChanged("BackupTroopGroups");
		}
	}

	public PairValueIntString SelectedPatrolTroopType
	{
		get
		{
			return _selectedPatrolTroopType;
		}
		set
		{
			_selectedPatrolTroopType = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_storm_troop = _selectedPatrolTroopType.Value;
			OnPropertyChanged("SelectedPatrolTroopType");
		}
	}

	public int PatrolTroopTypeLimit
	{
		get
		{
			return _patrolTroopTypeLimit;
		}
		set
		{
			_patrolTroopTypeLimit = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_storm_amount = _patrolTroopTypeLimit;
			OnPropertyChanged("PatrolTroopTypeLimit");
		}
	}

	public int PatrolTroopTypeGroups
	{
		get
		{
			return _patrolTroopTypeGroups;
		}
		set
		{
			_patrolTroopTypeGroups = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_storm_tribes = _patrolTroopTypeGroups;
			OnPropertyChanged("PatrolTroopTypeGroups");
		}
	}

	public PairValueIntString SelectedEngageTroopType
	{
		get
		{
			return _selectedEngageTroopType;
		}
		set
		{
			_selectedEngageTroopType = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_shock_troop = _selectedEngageTroopType.Value;
			OnPropertyChanged("SelectedEngageTroopType");
		}
	}

	public int EngageTroopLimit
	{
		get
		{
			return _engageTroopLimit;
		}
		set
		{
			_engageTroopLimit = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_shock_amount = _engageTroopLimit;
			OnPropertyChanged("EngageTroopLimit");
		}
	}

	public PairValueIntString SelectedSiegeProtectionType
	{
		get
		{
			return _selectedSiegeProtectionType;
		}
		set
		{
			_selectedSiegeProtectionType = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_reserve_troop = _selectedSiegeProtectionType.Value;
			OnPropertyChanged("SelectedSiegeProtectionType");
		}
	}

	public int SiegeProtectionLimit
	{
		get
		{
			return _siegeProtectionLimit;
		}
		set
		{
			_siegeProtectionLimit = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_reserve_amount = _siegeProtectionLimit;
			OnPropertyChanged("SiegeProtectionLimit");
		}
	}

	public int SiegeProtectionGroupCount
	{
		get
		{
			return _siegeProtectionGroupCount;
		}
		set
		{
			_siegeProtectionGroupCount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_reserve_tribes = _siegeProtectionGroupCount;
			OnPropertyChanged("SiegeProtectionGroupCount");
		}
	}

	public PairValueIntString SiegeArmyUnitType1
	{
		get
		{
			return _siegeArmyUnitType1;
		}
		set
		{
			_siegeArmyUnitType1 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[0] = _siegeArmyUnitType1.Value;
			OnPropertyChanged("SiegeArmyUnitType1");
		}
	}

	public PairValueIntString SiegeArmyUnitType2
	{
		get
		{
			return _siegeArmyUnitType2;
		}
		set
		{
			_siegeArmyUnitType2 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[1] = _siegeArmyUnitType2.Value;
			OnPropertyChanged("SiegeArmyUnitType2");
		}
	}

	public PairValueIntString SiegeArmyUnitType3
	{
		get
		{
			return _siegeArmyUnitType3;
		}
		set
		{
			_siegeArmyUnitType3 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[2] = _siegeArmyUnitType3.Value;
			OnPropertyChanged("SiegeArmyUnitType3");
		}
	}

	public PairValueIntString SiegeArmyUnitType4
	{
		get
		{
			return _siegeArmyUnitType4;
		}
		set
		{
			_siegeArmyUnitType4 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[3] = _siegeArmyUnitType4.Value;
			OnPropertyChanged("SiegeArmyUnitType4");
		}
	}

	public PairValueIntString SiegeArmyUnitType5
	{
		get
		{
			return _siegeArmyUnitType5;
		}
		set
		{
			_siegeArmyUnitType5 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[4] = _siegeArmyUnitType5.Value;
			OnPropertyChanged("SiegeArmyUnitType5");
		}
	}

	public PairValueIntString SiegeArmyUnitType6
	{
		get
		{
			return _siegeArmyUnitType6;
		}
		set
		{
			_siegeArmyUnitType6 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[5] = _siegeArmyUnitType6.Value;
			OnPropertyChanged("SiegeArmyUnitType6");
		}
	}

	public PairValueIntString SiegeArmyUnitType7
	{
		get
		{
			return _siegeArmyUnitType7;
		}
		set
		{
			_siegeArmyUnitType7 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[6] = _siegeArmyUnitType7.Value;
			OnPropertyChanged("SiegeArmyUnitType7");
		}
	}

	public PairValueIntString SiegeArmyUnitType8
	{
		get
		{
			return _siegeArmyUnitType8;
		}
		set
		{
			_siegeArmyUnitType8 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[7] = _siegeArmyUnitType8.Value;
			OnPropertyChanged("SiegeArmyUnitType8");
		}
	}

	public PairValueIntString SiegeArmyUnitType9
	{
		get
		{
			return _siegeArmyUnitType9;
		}
		set
		{
			_siegeArmyUnitType9 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[8] = _siegeArmyUnitType9.Value;
			OnPropertyChanged("SiegeArmyUnitType9");
		}
	}

	public PairValueIntString SiegeArmyUnitType10
	{
		get
		{
			return _siegeArmyUnitType10;
		}
		set
		{
			_siegeArmyUnitType10 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[9] = _siegeArmyUnitType10.Value;
			OnPropertyChanged("SiegeArmyUnitType10");
		}
	}

	public PairValueIntString SiegeArmyUnitType11
	{
		get
		{
			return _siegeArmyUnitType11;
		}
		set
		{
			_siegeArmyUnitType11 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[10] = _siegeArmyUnitType11.Value;
			OnPropertyChanged("SiegeArmyUnitType11");
		}
	}

	public PairValueIntString SiegeArmyUnitType12
	{
		get
		{
			return _siegeArmyUnitType12;
		}
		set
		{
			_siegeArmyUnitType12 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[11] = _siegeArmyUnitType12.Value;
			OnPropertyChanged("SiegeArmyUnitType12");
		}
	}

	public PairValueIntString SiegeArmyUnitType13
	{
		get
		{
			return _siegeArmyUnitType13;
		}
		set
		{
			_siegeArmyUnitType13 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[12] = _siegeArmyUnitType13.Value;
			OnPropertyChanged("SiegeArmyUnitType13");
		}
	}

	public PairValueIntString SiegeArmyUnitType14
	{
		get
		{
			return _siegeArmyUnitType14;
		}
		set
		{
			_siegeArmyUnitType14 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[13] = _siegeArmyUnitType14.Value;
			OnPropertyChanged("SiegeArmyUnitType14");
		}
	}

	public PairValueIntString SiegeArmyUnitType15
	{
		get
		{
			return _siegeArmyUnitType15;
		}
		set
		{
			_siegeArmyUnitType15 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[14] = _siegeArmyUnitType15.Value;
			OnPropertyChanged("SiegeArmyUnitType15");
		}
	}

	public PairValueIntString SiegeArmyUnitType16
	{
		get
		{
			return _siegeArmyUnitType16;
		}
		set
		{
			_siegeArmyUnitType16 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[15] = _siegeArmyUnitType16.Value;
			OnPropertyChanged("SiegeArmyUnitType16");
		}
	}

	public PairValueIntString SiegeArmyUnitType17
	{
		get
		{
			return _siegeArmyUnitType17;
		}
		set
		{
			_siegeArmyUnitType17 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[16] = _siegeArmyUnitType17.Value;
			OnPropertyChanged("SiegeArmyUnitType17");
		}
	}

	public PairValueIntString SiegeArmyUnitType18
	{
		get
		{
			return _siegeArmyUnitType18;
		}
		set
		{
			_siegeArmyUnitType18 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[17] = _siegeArmyUnitType18.Value;
			OnPropertyChanged("SiegeArmyUnitType18");
		}
	}

	public PairValueIntString SiegeArmyUnitType19
	{
		get
		{
			return _siegeArmyUnitType19;
		}
		set
		{
			_siegeArmyUnitType19 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[18] = _siegeArmyUnitType19.Value;
			OnPropertyChanged("SiegeArmyUnitType19");
		}
	}

	public PairValueIntString SiegeArmyUnitType20
	{
		get
		{
			return _siegeArmyUnitType20;
		}
		set
		{
			_siegeArmyUnitType20 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[19] = _siegeArmyUnitType20.Value;
			OnPropertyChanged("SiegeArmyUnitType20");
		}
	}

	public PairValueIntString SiegeArmyUnitType21
	{
		get
		{
			return _siegeArmyUnitType21;
		}
		set
		{
			_siegeArmyUnitType21 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[20] = _siegeArmyUnitType21.Value;
			OnPropertyChanged("SiegeArmyUnitType21");
		}
	}

	public PairValueIntString SiegeArmyUnitType22
	{
		get
		{
			return _siegeArmyUnitType22;
		}
		set
		{
			_siegeArmyUnitType22 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[21] = _siegeArmyUnitType22.Value;
			OnPropertyChanged("SiegeArmyUnitType22");
		}
	}

	public PairValueIntString SiegeArmyUnitType23
	{
		get
		{
			return _siegeArmyUnitType23;
		}
		set
		{
			_siegeArmyUnitType23 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[22] = _siegeArmyUnitType23.Value;
			OnPropertyChanged("SiegeArmyUnitType23");
		}
	}

	public PairValueIntString SiegeArmyUnitType24
	{
		get
		{
			return _siegeArmyUnitType24;
		}
		set
		{
			_siegeArmyUnitType24 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_troops[23] = _siegeArmyUnitType24.Value;
			OnPropertyChanged("SiegeArmyUnitType24");
		}
	}

	public int SiegeArmyLimit
	{
		get
		{
			return _siegeArmyLimit;
		}
		set
		{
			_siegeArmyLimit = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_amount = _siegeArmyLimit;
			OnPropertyChanged("SiegeArmyLimit");
		}
	}

	public int SiegeArmyGroupCount
	{
		get
		{
			return _siegeArmyGroupCount;
		}
		set
		{
			_siegeArmyGroupCount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_wall_tribes = _siegeArmyGroupCount;
			OnPropertyChanged("SiegeArmyGroupCount");
		}
	}

	public int Siege_max_troops
	{
		get
		{
			return _siege_max_troops;
		}
		set
		{
			_siege_max_troops = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_max_troops = _siege_max_troops;
			OnPropertyChanged("Siege_max_troops");
		}
	}

	public int Siege_normal_wave_multiplier
	{
		get
		{
			return _siege_normal_wave_multiplier;
		}
		set
		{
			_siege_normal_wave_multiplier = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_normal_wave_multiplier = _siege_normal_wave_multiplier;
			OnPropertyChanged("Siege_normal_wave_multiplier");
		}
	}

	public int Siege_high_gold_wave_multiplier
	{
		get
		{
			return _siege_high_gold_wave_multiplier;
		}
		set
		{
			_siege_high_gold_wave_multiplier = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_high_gold_wave_multiplier = _siege_high_gold_wave_multiplier;
			OnPropertyChanged("Siege_high_gold_wave_multiplier");
		}
	}

	public PairValueIntString SiegeArmySiegeEngineType1
	{
		get
		{
			return _siegeArmySiegeEngineType1;
		}
		set
		{
			_siegeArmySiegeEngineType1 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_machines[0] = _siegeArmySiegeEngineType1.Value;
			OnPropertyChanged("SiegeArmySiegeEngineType1");
		}
	}

	public PairValueIntString SiegeArmySiegeEngineType2
	{
		get
		{
			return _siegeArmySiegeEngineType2;
		}
		set
		{
			_siegeArmySiegeEngineType2 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_machines[1] = _siegeArmySiegeEngineType2.Value;
			OnPropertyChanged("SiegeArmySiegeEngineType2");
		}
	}

	public PairValueIntString SiegeArmySiegeEngineType3
	{
		get
		{
			return _siegeArmySiegeEngineType3;
		}
		set
		{
			_siegeArmySiegeEngineType3 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_machines[2] = _siegeArmySiegeEngineType3.Value;
			OnPropertyChanged("SiegeArmySiegeEngineType3");
		}
	}

	public PairValueIntString SiegeArmySiegeEngineType4
	{
		get
		{
			return _siegeArmySiegeEngineType4;
		}
		set
		{
			_siegeArmySiegeEngineType4 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_machines[3] = _siegeArmySiegeEngineType4.Value;
			OnPropertyChanged("SiegeArmySiegeEngineType4");
		}
	}

	public PairValueIntString SiegeArmySiegeEngineType5
	{
		get
		{
			return _siegeArmySiegeEngineType5;
		}
		set
		{
			_siegeArmySiegeEngineType5 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_machines[4] = _siegeArmySiegeEngineType5.Value;
			OnPropertyChanged("SiegeArmySiegeEngineType5");
		}
	}

	public PairValueIntString SiegeArmySiegeEngineType6
	{
		get
		{
			return _siegeArmySiegeEngineType6;
		}
		set
		{
			_siegeArmySiegeEngineType6 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_machines[5] = _siegeArmySiegeEngineType6.Value;
			OnPropertyChanged("SiegeArmySiegeEngineType6");
		}
	}

	public PairValueIntString SiegeArmySiegeEngineType7
	{
		get
		{
			return _siegeArmySiegeEngineType7;
		}
		set
		{
			_siegeArmySiegeEngineType7 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_machines[6] = _siegeArmySiegeEngineType7.Value;
			OnPropertyChanged("SiegeArmySiegeEngineType7");
		}
	}

	public PairValueIntString SiegeArmySiegeEngineType8
	{
		get
		{
			return _siegeArmySiegeEngineType8;
		}
		set
		{
			_siegeArmySiegeEngineType8 = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_machines[7] = _siegeArmySiegeEngineType8.Value;
			OnPropertyChanged("SiegeArmySiegeEngineType8");
		}
	}

	public int EngineerLimit
	{
		get
		{
			return _engineerLimit;
		}
		set
		{
			_engineerLimit = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_eng_amount = _engineerLimit;
			OnPropertyChanged("EngineerLimit");
		}
	}

	public PairValueIntString SelectedDiggerType
	{
		get
		{
			return _selectedDiggerType;
		}
		set
		{
			_selectedDiggerType = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_moat_troop = _selectedDiggerType.Value;
			OnPropertyChanged("SelectedDiggerType");
		}
	}

	public int DiggerLimit
	{
		get
		{
			return _diggerLimit;
		}
		set
		{
			_diggerLimit = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_moat_amount = _diggerLimit;
			OnPropertyChanged("DiggerLimit");
		}
	}

	public int AssassinLimit
	{
		get
		{
			return _assassinLimit;
		}
		set
		{
			_assassinLimit = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_assasin_amount = _assassinLimit;
			OnPropertyChanged("AssassinLimit");
		}
	}

	public int LaddermanLimit
	{
		get
		{
			return _laddermanLimit;
		}
		set
		{
			_laddermanLimit = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_ladder_amount = _laddermanLimit;
			OnPropertyChanged("LaddermanLimit");
		}
	}

	public int TunnelerLimit
	{
		get
		{
			return _tunnelerLimit;
		}
		set
		{
			_tunnelerLimit = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_tunnel_amount = _tunnelerLimit;
			OnPropertyChanged("TunnelerLimit");
		}
	}

	public int SiegeAdvanceArmyThreshold
	{
		get
		{
			return _siegeAdvanceArmyThreshold;
		}
		set
		{
			_siegeAdvanceArmyThreshold = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_troops_on_site_percent = _siegeAdvanceArmyThreshold;
			OnPropertyChanged("SiegeAdvanceArmyThreshold");
		}
	}

	public int SiegeRetreatThreshold
	{
		get
		{
			return _siegeRetreatThreshold;
		}
		set
		{
			_siegeRetreatThreshold = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_troops_at_home_percent = _siegeRetreatThreshold;
			OnPropertyChanged("SiegeRetreatThreshold");
		}
	}

	public int CastleBombardmentDuration
	{
		get
		{
			return _castleBombardmentDuration;
		}
		set
		{
			_castleBombardmentDuration = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_soften_up_delay = _castleBombardmentDuration;
			OnPropertyChanged("CastleBombardmentDuration");
		}
	}

	public int SuccessRetreatDelay
	{
		get
		{
			return _successRetreatDelay;
		}
		set
		{
			_successRetreatDelay = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_victory_delay = _successRetreatDelay;
			OnPropertyChanged("SuccessRetreatDelay");
		}
	}

	public int CowThrowInterval
	{
		get
		{
			return _cowThrowInterval;
		}
		set
		{
			_cowThrowInterval = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_cow_timer = _cowThrowInterval;
			OnPropertyChanged("CowThrowInterval");
		}
	}

	public bool UseAdvancedAI
	{
		get
		{
			return _UseAdvancedAI;
		}
		set
		{
			_UseAdvancedAI = value;
			AreChangesPresent = true;
			if (_UseAdvancedAI)
			{
				Helpers.SelectedLordConfig.lord.use_improved_sieging |= 1;
			}
			else
			{
				Helpers.SelectedLordConfig.lord.use_improved_sieging &= -2;
			}
			OnPropertyChanged("UseAdvancedAI");
		}
	}

	public bool UseAdvancedAI2
	{
		get
		{
			return _UseAdvancedAI2;
		}
		set
		{
			_UseAdvancedAI2 = value;
			AreChangesPresent = true;
			if (_UseAdvancedAI2)
			{
				Helpers.SelectedLordConfig.lord.use_improved_sieging |= 2;
			}
			else
			{
				Helpers.SelectedLordConfig.lord.use_improved_sieging &= -3;
			}
			OnPropertyChanged("UseAdvancedAI2");
		}
	}

	public List<PairValueIntString> TargetPreferences { get; private set; }

	public PairValueIntString TargetingPreference
	{
		get
		{
			return _targetingPreference;
		}
		set
		{
			_targetingPreference = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.who_to_pick_on = _targetingPreference.Value;
			OnPropertyChanged("TargetingPreference");
		}
	}

	public int CooperationProb
	{
		get
		{
			return _cooperationProb;
		}
		set
		{
			_cooperationProb = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.percent_chance_waiting_for_joint_attack = _cooperationProb;
			OnPropertyChanged("CooperationProb");
		}
	}

	public int JointSiegeTroopThreshold
	{
		get
		{
			return _jointSiegeTroopThreshold;
		}
		set
		{
			_jointSiegeTroopThreshold = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.siege_troops_before_will_come_to_rescue = _jointSiegeTroopThreshold;
			OnPropertyChanged("JointSiegeTroopThreshold");
		}
	}

	public int NormalArcherStartingAmount
	{
		get
		{
			return _normalArcherStartingAmount;
		}
		set
		{
			_normalArcherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[0] = _normalArcherStartingAmount;
			OnPropertyChanged("NormalArcherStartingAmount");
		}
	}

	public int NormalSpearmanStartingAmount
	{
		get
		{
			return _normalSpearmanStartingAmount;
		}
		set
		{
			_normalSpearmanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[2] = _normalSpearmanStartingAmount;
			OnPropertyChanged("NormalSpearmanStartingAmount");
		}
	}

	public int NormalMacemanStartingAmount
	{
		get
		{
			return _normalMacemanStartingAmount;
		}
		set
		{
			_normalMacemanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[4] = _normalMacemanStartingAmount;
			OnPropertyChanged("NormalMacemanStartingAmount");
		}
	}

	public int NormalCrossbowmanStartingAmount
	{
		get
		{
			return _normalCrossbowmanStartingAmount;
		}
		set
		{
			_normalCrossbowmanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[1] = _normalCrossbowmanStartingAmount;
			OnPropertyChanged("NormalCrossbowmanStartingAmount");
		}
	}

	public int NormalPikemanStartingAmount
	{
		get
		{
			return _normalPikemanStartingAmount;
		}
		set
		{
			_normalPikemanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[3] = _normalPikemanStartingAmount;
			OnPropertyChanged("NormalPikemanStartingAmount");
		}
	}

	public int NormalSwordsmanStartingAmount
	{
		get
		{
			return _normalSwordsmanStartingAmount;
		}
		set
		{
			_normalSwordsmanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[5] = _normalSwordsmanStartingAmount;
			OnPropertyChanged("NormalSwordsmanStartingAmount");
		}
	}

	public int NormalKnightStartingAmount
	{
		get
		{
			return _normalKnightStartingAmount;
		}
		set
		{
			_normalKnightStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[6] = _normalKnightStartingAmount;
			OnPropertyChanged("NormalKnightStartingAmount");
		}
	}

	public int NormalArabianArcherStartingAmount
	{
		get
		{
			return _normalArabianArcherStartingAmount;
		}
		set
		{
			_normalArabianArcherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[10] = _normalArabianArcherStartingAmount;
			OnPropertyChanged("NormalArabianArcherStartingAmount");
		}
	}

	public int NormalSlaveStartingAmount
	{
		get
		{
			return _normalSlaveStartingAmount;
		}
		set
		{
			_normalSlaveStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[11] = _normalSlaveStartingAmount;
			OnPropertyChanged("NormalSlaveStartingAmount");
		}
	}

	public int NormalSlingerStartingAmount
	{
		get
		{
			return _normalSlingerStartingAmount;
		}
		set
		{
			_normalSlingerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[12] = _normalSlingerStartingAmount;
			OnPropertyChanged("NormalSlingerStartingAmount");
		}
	}

	public int NormalAssassinStartingAmount
	{
		get
		{
			return _normalAssassinStartingAmount;
		}
		set
		{
			_normalAssassinStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[13] = _normalAssassinStartingAmount;
			OnPropertyChanged("NormalAssassinStartingAmount");
		}
	}

	public int NormalHorseArcherStartingAmount
	{
		get
		{
			return _normalHorseArcherStartingAmount;
		}
		set
		{
			_normalHorseArcherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[14] = _normalHorseArcherStartingAmount;
			OnPropertyChanged("NormalHorseArcherStartingAmount");
		}
	}

	public int NormalArabianSwordsmanStartingAmount
	{
		get
		{
			return _normalArabianSwordsmanStartingAmount;
		}
		set
		{
			_normalArabianSwordsmanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[15] = _normalArabianSwordsmanStartingAmount;
			OnPropertyChanged("NormalArabianSwordsmanStartingAmount");
		}
	}

	public int NormalFireThrowerStartingAmount
	{
		get
		{
			return _normalFireThrowerStartingAmount;
		}
		set
		{
			_normalFireThrowerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[16] = _normalFireThrowerStartingAmount;
			OnPropertyChanged("NormalFireThrowerStartingAmount");
		}
	}

	public int NormalFireBallistaStartingAmount
	{
		get
		{
			return _normalFireBallistaStartingAmount;
		}
		set
		{
			_normalFireBallistaStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[17] = _normalFireBallistaStartingAmount;
			OnPropertyChanged("NormalFireBallistaStartingAmount");
		}
	}

	public int NormalSkirmisherStartingAmount
	{
		get
		{
			return _normalSkirmisherStartingAmount;
		}
		set
		{
			_normalSkirmisherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[24] = _normalSkirmisherStartingAmount;
			OnPropertyChanged("NormalSkirmisherStartingAmount");
		}
	}

	public int NormalSapperStartingAmount
	{
		get
		{
			return _normalSapperStartingAmount;
		}
		set
		{
			_normalSapperStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[26] = _normalSapperStartingAmount;
			OnPropertyChanged("NormalSapperStartingAmount");
		}
	}

	public int NormalCamelLancerStartingAmount
	{
		get
		{
			return _normalCamelLancerStartingAmount;
		}
		set
		{
			_normalCamelLancerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[20] = _normalCamelLancerStartingAmount;
			OnPropertyChanged("NormalCamelLancerStartingAmount");
		}
	}

	public int NormalHeavyCamelStartingAmount
	{
		get
		{
			return _normalHeavyCamelStartingAmount;
		}
		set
		{
			_normalHeavyCamelStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[25] = _normalHeavyCamelStartingAmount;
			OnPropertyChanged("NormalHeavyCamelStartingAmount");
		}
	}

	public int NormalDemolisherStartingAmount
	{
		get
		{
			return _normalDemolisherStartingAmount;
		}
		set
		{
			_normalDemolisherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[27] = _normalDemolisherStartingAmount;
			OnPropertyChanged("NormalDemolisherStartingAmount");
		}
	}

	public int NormalEunuchStartingAmount
	{
		get
		{
			return _normalEunuchStartingAmount;
		}
		set
		{
			_normalEunuchStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[22] = _normalEunuchStartingAmount;
			OnPropertyChanged("NormalEunuchStartingAmount");
		}
	}

	public int NormalHealerStartingAmount
	{
		get
		{
			return _normalHealerStartingAmount;
		}
		set
		{
			_normalHealerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[21] = _normalHealerStartingAmount;
			OnPropertyChanged("NormalHealerStartingAmount");
		}
	}

	public int NormalAmbusherStartingAmount
	{
		get
		{
			return _normalAmbusherStartingAmount;
		}
		set
		{
			_normalAmbusherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[23] = _normalAmbusherStartingAmount;
			OnPropertyChanged("NormalAmbusherStartingAmount");
		}
	}

	public int NormalEngineerStartingAmount
	{
		get
		{
			return _normalEngineerStartingAmount;
		}
		set
		{
			_normalEngineerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[8] = _normalEngineerStartingAmount;
			OnPropertyChanged("NormalEngineerStartingAmount");
		}
	}

	public int NormalMonkStartingAmount
	{
		get
		{
			return _normalMonkStartingAmount;
		}
		set
		{
			_normalMonkStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_normal[9] = _normalMonkStartingAmount;
			OnPropertyChanged("NormalMonkStartingAmount");
		}
	}

	public int DeathmatchArcherStartingAmount
	{
		get
		{
			return _deathmatchArcherStartingAmount;
		}
		set
		{
			_deathmatchArcherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[0] = _deathmatchArcherStartingAmount;
			OnPropertyChanged("DeathmatchArcherStartingAmount");
		}
	}

	public int DeathmatchSpearmanStartingAmount
	{
		get
		{
			return _deathmatchSpearmanStartingAmount;
		}
		set
		{
			_deathmatchSpearmanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[2] = _deathmatchSpearmanStartingAmount;
			OnPropertyChanged("DeathmatchSpearmanStartingAmount");
		}
	}

	public int DeathmatchMacemanStartingAmount
	{
		get
		{
			return _deathmatchMacemanStartingAmount;
		}
		set
		{
			_deathmatchMacemanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[4] = _deathmatchMacemanStartingAmount;
			OnPropertyChanged("DeathmatchMacemanStartingAmount");
		}
	}

	public int DeathmatchCrossbowmanStartingAmount
	{
		get
		{
			return _deathmatchCrossbowmanStartingAmount;
		}
		set
		{
			_deathmatchCrossbowmanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[1] = _deathmatchCrossbowmanStartingAmount;
			OnPropertyChanged("DeathmatchCrossbowmanStartingAmount");
		}
	}

	public int DeathmatchPikemanStartingAmount
	{
		get
		{
			return _deathmatchPikemanStartingAmount;
		}
		set
		{
			_deathmatchPikemanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[3] = _deathmatchPikemanStartingAmount;
			OnPropertyChanged("DeathmatchPikemanStartingAmount");
		}
	}

	public int DeathmatchSwordsmanStartingAmount
	{
		get
		{
			return _deathmatchSwordsmanStartingAmount;
		}
		set
		{
			_deathmatchSwordsmanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[5] = _deathmatchSwordsmanStartingAmount;
			OnPropertyChanged("DeathmatchSwordsmanStartingAmount");
		}
	}

	public int DeathmatchKnightStartingAmount
	{
		get
		{
			return _deathmatchKnightStartingAmount;
		}
		set
		{
			_deathmatchKnightStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[6] = _deathmatchKnightStartingAmount;
			OnPropertyChanged("DeathmatchKnightStartingAmount");
		}
	}

	public int DeathmatchArabianArcherStartingAmount
	{
		get
		{
			return _deathmatchArabianArcherStartingAmount;
		}
		set
		{
			_deathmatchArabianArcherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[10] = _deathmatchArabianArcherStartingAmount;
			OnPropertyChanged("DeathmatchArabianArcherStartingAmount");
		}
	}

	public int DeathmatchSlaveStartingAmount
	{
		get
		{
			return _deathmatchSlaveStartingAmount;
		}
		set
		{
			_deathmatchSlaveStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[11] = _deathmatchSlaveStartingAmount;
			OnPropertyChanged("DeathmatchSlaveStartingAmount");
		}
	}

	public int DeathmatchSlingerStartingAmount
	{
		get
		{
			return _deathmatchSlingerStartingAmount;
		}
		set
		{
			_deathmatchSlingerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[12] = _deathmatchSlingerStartingAmount;
			OnPropertyChanged("DeathmatchSlingerStartingAmount");
		}
	}

	public int DeathmatchAssassinStartingAmount
	{
		get
		{
			return _deathmatchAssassinStartingAmount;
		}
		set
		{
			_deathmatchAssassinStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[13] = _deathmatchAssassinStartingAmount;
			OnPropertyChanged("DeathmatchAssassinStartingAmount");
		}
	}

	public int DeathmatchHorseArcherStartingAmount
	{
		get
		{
			return _deathmatchHorseArcherStartingAmount;
		}
		set
		{
			_deathmatchHorseArcherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[14] = _deathmatchHorseArcherStartingAmount;
			OnPropertyChanged("DeathmatchHorseArcherStartingAmount");
		}
	}

	public int DeathmatchArabianSwordsmanStartingAmount
	{
		get
		{
			return _deathmatchArabianSwordsmanStartingAmount;
		}
		set
		{
			_deathmatchArabianSwordsmanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[15] = _deathmatchArabianSwordsmanStartingAmount;
			OnPropertyChanged("DeathmatchArabianSwordsmanStartingAmount");
		}
	}

	public int DeathmatchFireThrowerStartingAmount
	{
		get
		{
			return _deathmatchFireThrowerStartingAmount;
		}
		set
		{
			_deathmatchFireThrowerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[16] = _deathmatchFireThrowerStartingAmount;
			OnPropertyChanged("DeathmatchFireThrowerStartingAmount");
		}
	}

	public int DeathmatchFireBallistaStartingAmount
	{
		get
		{
			return _deathmatchFireBallistaStartingAmount;
		}
		set
		{
			_deathmatchFireBallistaStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[17] = _deathmatchFireBallistaStartingAmount;
			OnPropertyChanged("DeathmatchFireBallistaStartingAmount");
		}
	}

	public int DeathmatchSkirmisherStartingAmount
	{
		get
		{
			return _deathmatchSkirmisherStartingAmount;
		}
		set
		{
			_deathmatchSkirmisherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[24] = _deathmatchSkirmisherStartingAmount;
			OnPropertyChanged("DeathmatchSkirmisherStartingAmount");
		}
	}

	public int DeathmatchSapperStartingAmount
	{
		get
		{
			return _deathmatchSapperStartingAmount;
		}
		set
		{
			_deathmatchSapperStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[26] = _deathmatchSapperStartingAmount;
			OnPropertyChanged("DeathmatchSapperStartingAmount");
		}
	}

	public int DeathmatchCamelLancerStartingAmount
	{
		get
		{
			return _deathmatchCamelLancerStartingAmount;
		}
		set
		{
			_deathmatchCamelLancerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[20] = _deathmatchCamelLancerStartingAmount;
			OnPropertyChanged("DeathmatchCamelLancerStartingAmount");
		}
	}

	public int DeathmatchHeavyCamelStartingAmount
	{
		get
		{
			return _deathmatchHeavyCamelStartingAmount;
		}
		set
		{
			_deathmatchHeavyCamelStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[25] = _deathmatchHeavyCamelStartingAmount;
			OnPropertyChanged("DeathmatchHeavyCamelStartingAmount");
		}
	}

	public int DeathmatchDemolisherStartingAmount
	{
		get
		{
			return _deathmatchDemolisherStartingAmount;
		}
		set
		{
			_deathmatchDemolisherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[27] = _deathmatchDemolisherStartingAmount;
			OnPropertyChanged("DeathmatchDemolisherStartingAmount");
		}
	}

	public int DeathmatchEunuchStartingAmount
	{
		get
		{
			return _deathmatchEunuchStartingAmount;
		}
		set
		{
			_deathmatchEunuchStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[22] = _deathmatchEunuchStartingAmount;
			OnPropertyChanged("DeathmatchEunuchStartingAmount");
		}
	}

	public int DeathmatchHealerStartingAmount
	{
		get
		{
			return _deathmatchHealerStartingAmount;
		}
		set
		{
			_deathmatchHealerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[21] = _deathmatchHealerStartingAmount;
			OnPropertyChanged("DeathmatchHealerStartingAmount");
		}
	}

	public int DeathmatchAmbusherStartingAmount
	{
		get
		{
			return _deathmatchAmbusherStartingAmount;
		}
		set
		{
			_deathmatchAmbusherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[23] = _deathmatchAmbusherStartingAmount;
			OnPropertyChanged("DeathmatchAmbusherStartingAmount");
		}
	}

	public int DeathmatchEngineerStartingAmount
	{
		get
		{
			return _deathmatchEngineerStartingAmount;
		}
		set
		{
			_deathmatchEngineerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[8] = _deathmatchEngineerStartingAmount;
			OnPropertyChanged("DeathmatchEngineerStartingAmount");
		}
	}

	public int DeathmatchMonkStartingAmount
	{
		get
		{
			return _deathmatchMonkStartingAmount;
		}
		set
		{
			_deathmatchMonkStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_deathmatch[9] = _deathmatchMonkStartingAmount;
			OnPropertyChanged("DeathmatchMonkStartingAmount");
		}
	}

	public int CrusaderArcherStartingAmount
	{
		get
		{
			return _crusaderArcherStartingAmount;
		}
		set
		{
			_crusaderArcherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[0] = _crusaderArcherStartingAmount;
			OnPropertyChanged("CrusaderArcherStartingAmount");
		}
	}

	public int CrusaderSpearmanStartingAmount
	{
		get
		{
			return _crusaderSpearmanStartingAmount;
		}
		set
		{
			_crusaderSpearmanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[2] = _crusaderSpearmanStartingAmount;
			OnPropertyChanged("CrusaderSpearmanStartingAmount");
		}
	}

	public int CrusaderMacemanStartingAmount
	{
		get
		{
			return _crusaderMacemanStartingAmount;
		}
		set
		{
			_crusaderMacemanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[4] = _crusaderMacemanStartingAmount;
			OnPropertyChanged("CrusaderMacemanStartingAmount");
		}
	}

	public int CrusaderCrossbowmanStartingAmount
	{
		get
		{
			return _crusaderCrossbowmanStartingAmount;
		}
		set
		{
			_crusaderCrossbowmanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[1] = _crusaderCrossbowmanStartingAmount;
			OnPropertyChanged("CrusaderCrossbowmanStartingAmount");
		}
	}

	public int CrusaderPikemanStartingAmount
	{
		get
		{
			return _crusaderPikemanStartingAmount;
		}
		set
		{
			_crusaderPikemanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[3] = _crusaderPikemanStartingAmount;
			OnPropertyChanged("CrusaderPikemanStartingAmount");
		}
	}

	public int CrusaderSwordsmanStartingAmount
	{
		get
		{
			return _crusaderSwordsmanStartingAmount;
		}
		set
		{
			_crusaderSwordsmanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[5] = _crusaderSwordsmanStartingAmount;
			OnPropertyChanged("CrusaderSwordsmanStartingAmount");
		}
	}

	public int CrusaderKnightStartingAmount
	{
		get
		{
			return _crusaderKnightStartingAmount;
		}
		set
		{
			_crusaderKnightStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[6] = _crusaderKnightStartingAmount;
			OnPropertyChanged("CrusaderKnightStartingAmount");
		}
	}

	public int CrusaderArabianArcherStartingAmount
	{
		get
		{
			return _crusaderArabianArcherStartingAmount;
		}
		set
		{
			_crusaderArabianArcherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[10] = _crusaderArabianArcherStartingAmount;
			OnPropertyChanged("CrusaderArabianArcherStartingAmount");
		}
	}

	public int CrusaderSlaveStartingAmount
	{
		get
		{
			return _crusaderSlaveStartingAmount;
		}
		set
		{
			_crusaderSlaveStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[11] = _crusaderSlaveStartingAmount;
			OnPropertyChanged("CrusaderSlaveStartingAmount");
		}
	}

	public int CrusaderSlingerStartingAmount
	{
		get
		{
			return _crusaderSlingerStartingAmount;
		}
		set
		{
			_crusaderSlingerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[12] = _crusaderSlingerStartingAmount;
			OnPropertyChanged("CrusaderSlingerStartingAmount");
		}
	}

	public int CrusaderAssassinStartingAmount
	{
		get
		{
			return _crusaderAssassinStartingAmount;
		}
		set
		{
			_crusaderAssassinStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[13] = _crusaderAssassinStartingAmount;
			OnPropertyChanged("CrusaderAssassinStartingAmount");
		}
	}

	public int CrusaderHorseArcherStartingAmount
	{
		get
		{
			return _crusaderHorseArcherStartingAmount;
		}
		set
		{
			_crusaderHorseArcherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[14] = _crusaderHorseArcherStartingAmount;
			OnPropertyChanged("CrusaderHorseArcherStartingAmount");
		}
	}

	public int CrusaderArabianSwordsmanStartingAmount
	{
		get
		{
			return _crusaderArabianSwordsmanStartingAmount;
		}
		set
		{
			_crusaderArabianSwordsmanStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[15] = _crusaderArabianSwordsmanStartingAmount;
			OnPropertyChanged("CrusaderArabianSwordsmanStartingAmount");
		}
	}

	public int CrusaderFireThrowerStartingAmount
	{
		get
		{
			return _crusaderFireThrowerStartingAmount;
		}
		set
		{
			_crusaderFireThrowerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[16] = _crusaderFireThrowerStartingAmount;
			OnPropertyChanged("CrusaderFireThrowerStartingAmount");
		}
	}

	public int CrusaderFireBallistaStartingAmount
	{
		get
		{
			return _crusaderFireBallistaStartingAmount;
		}
		set
		{
			_crusaderFireBallistaStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[17] = _crusaderFireBallistaStartingAmount;
			OnPropertyChanged("CrusaderFireBallistaStartingAmount");
		}
	}

	public int CrusaderSkirmisherStartingAmount
	{
		get
		{
			return _crusaderSkirmisherStartingAmount;
		}
		set
		{
			_crusaderSkirmisherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[24] = _crusaderSkirmisherStartingAmount;
			OnPropertyChanged("CrusaderSkirmisherStartingAmount");
		}
	}

	public int CrusaderSapperStartingAmount
	{
		get
		{
			return _crusaderSapperStartingAmount;
		}
		set
		{
			_crusaderSapperStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[26] = _crusaderSapperStartingAmount;
			OnPropertyChanged("CrusaderSapperStartingAmount");
		}
	}

	public int CrusaderCamelLancerStartingAmount
	{
		get
		{
			return _crusaderCamelLancerStartingAmount;
		}
		set
		{
			_crusaderCamelLancerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[20] = _crusaderCamelLancerStartingAmount;
			OnPropertyChanged("CrusaderCamelLancerStartingAmount");
		}
	}

	public int CrusaderHeavyCamelStartingAmount
	{
		get
		{
			return _crusaderHeavyCamelStartingAmount;
		}
		set
		{
			_crusaderHeavyCamelStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[25] = _crusaderHeavyCamelStartingAmount;
			OnPropertyChanged("CrusaderHeavyCamelStartingAmount");
		}
	}

	public int CrusaderDemolisherStartingAmount
	{
		get
		{
			return _crusaderDemolisherStartingAmount;
		}
		set
		{
			_crusaderDemolisherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[27] = _crusaderDemolisherStartingAmount;
			OnPropertyChanged("CrusaderDemolisherStartingAmount");
		}
	}

	public int CrusaderEunuchStartingAmount
	{
		get
		{
			return _crusaderEunuchStartingAmount;
		}
		set
		{
			_crusaderEunuchStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[22] = _crusaderEunuchStartingAmount;
			OnPropertyChanged("CrusaderEunuchStartingAmount");
		}
	}

	public int CrusaderHealerStartingAmount
	{
		get
		{
			return _crusaderHealerStartingAmount;
		}
		set
		{
			_crusaderHealerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[21] = _crusaderHealerStartingAmount;
			OnPropertyChanged("CrusaderHealerStartingAmount");
		}
	}

	public int CrusaderAmbusherStartingAmount
	{
		get
		{
			return _crusaderAmbusherStartingAmount;
		}
		set
		{
			_crusaderAmbusherStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[23] = _crusaderAmbusherStartingAmount;
			OnPropertyChanged("CrusaderAmbusherStartingAmount");
		}
	}

	public int CrusaderEngineerStartingAmount
	{
		get
		{
			return _crusaderEngineerStartingAmount;
		}
		set
		{
			_crusaderEngineerStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[8] = _crusaderEngineerStartingAmount;
			OnPropertyChanged("CrusaderEngineerStartingAmount");
		}
	}

	public int CrusaderMonkStartingAmount
	{
		get
		{
			return _crusaderMonkStartingAmount;
		}
		set
		{
			_crusaderMonkStartingAmount = value;
			AreChangesPresent = true;
			Helpers.SelectedLordConfig.lord.starting_troops_crusader[9] = _crusaderMonkStartingAmount;
			OnPropertyChanged("CrusaderMonkStartingAmount");
		}
	}

	public DelegateCommand NoCommand { get; private set; }

	public DelegateCommand EditLordCancelCommand { get; private set; }

	public DelegateCommand EditLordSaveCommand { get; private set; }

	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged(string propertyName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public PnlEditLordViewModel()
	{
		instance = this;
		SaveButtonContents = "Save";
		EditLordCancelCommand = new DelegateCommand(OnEditLordCancelPress);
		EditLordSaveCommand = new DelegateCommand(EditLordSavePress);
		InitCollections();
	}

	internal void SetStartingValues()
	{
		inSetupPhase = true;
		JsonReadWrite.NewAIC Lord = Helpers.SelectedLordConfig.lord;
		EditorTitle = "Stronghold Crusader DE CPU Lord Editor - " + MainViewModel.instance.CurrentLordName;
		HPPercentage = Lord.lord_hps_percent;
		SelectedFlag = LordFlagTypes.Find((PairValueIntString flag) => flag.Value == Lord.flag_type);
		SelectedLordFaction = LordFactions.Find((PairValueIntString faction) => faction.Value == Lord.lord_gfx_type);
		SelectedLordPowerDisplayLevel = LordPowerDisplayLevels.Find((PairValueIntString powerLevel) => powerLevel.Value == Lord.lord_power_display_level);
		CriticalPopThreshold = Lord.vlow_popularity / 100;
		LowPopThreshold = Lord.low_popularity / 100;
		HighPopThreshold = Lord.high_popularity / 100;
		MinTax = TaxLevels.Find((PairValueIntString lowTax) => lowTax.Value == Lord.min_tax);
		MaxTax = TaxLevels.Find((PairValueIntString highTax) => highTax.Value == Lord.max_tax);
		DoubleRationThreshold = Lord.ration_boost;
		SelectedAleAllowance = AleAllowances.Find((PairValueIntString allowance) => allowance.Value == Lord.use_of_ale);
		SelectedReligionAllowance = ReligionAllowances.Find((PairValueIntString religionAllowance) => religionAllowance.Value == Lord.use_of_religion);
		BuildDelay = Lord.build_rate;
		BuildingRepairDelay = Lord.crushed_building_delay;
		PopulationPerFarm = Lord.people_to_farm_ratio;
		PopulationPerWoodCutter = Lord.extract_wood_ratio;
		PopulationPerQuarry = Lord.extract_stone_ratio;
		PopulationPerIronMine = Lord.extract_iron_ratio;
		PopulationPerPitchDugout = Lord.extract_pitch_ratio;
		MaximumFarmAmount = Lord.max_farms;
		MaximumWoodCutterAmount = Lord.max_woodcutters;
		MaximumQuarryAmount = Lord.max_quarries;
		MaximumIronMineAmount = Lord.max_mines;
		MaximumPitchDugoutAmount = Lord.max_pitch_dugouts;
		FoodPurchaseAmount = Lord.buy_food_amount;
		WeaponPurchaseAmount = Lord.buy_weapons;
		PesterGoodsDelay = Lord.pester_for_goods_delay;
		GoodsSendMargin = Lord.send_goods_margin;
		SellFoodMargin = Lord.sell_food_at;
		AppleBuyMargin = Lord.buy_apples_at;
		CheeseBuyMargin = Lord.buy_cheese_at;
		BreadBuyMargin = Lord.buy_bread_at;
		WheatBuyMargin = Lord.buy_wheat_at;
		HopBuyMargin = Lord.buy_hops_at;
		WoodSellMargin = Lord.trade_wood_at;
		StoneSellMargin = Lord.trade_stone_at;
		MiscSellMargin = Lord.trade_resources_at;
		WeaponSellMargin = Lord.trade_weapons_at;
		AleSellMargin = Lord.trade_ale_at;
		PitchSellMargin = Lord.trade_pitch_at;
		FlourSellMargin = Lord.trade_flour_at;
		BaseGoldReserve = Lord.base_gold_reserves;
		SaleVariance = Lord.trade_minimum;
		SelectedFarm1 = FarmChoices.Find((PairValueIntString farm1) => farm1.Value == Lord.farm_types[0]);
		SelectedFarm2 = FarmChoices.Find((PairValueIntString farm2) => farm2.Value == Lord.farm_types[1]);
		SelectedFarm3 = FarmChoices.Find((PairValueIntString farm3) => farm3.Value == Lord.farm_types[2]);
		SelectedFarm4 = FarmChoices.Find((PairValueIntString farm4) => farm4.Value == Lord.farm_types[3]);
		SelectedFarm5 = FarmChoices.Find((PairValueIntString farm5) => farm5.Value == Lord.farm_types[4]);
		SelectedFarm6 = FarmChoices.Find((PairValueIntString farm6) => farm6.Value == Lord.farm_types[5]);
		SelectedFarm7 = FarmChoices.Find((PairValueIntString farm7) => farm7.Value == Lord.farm_types[6]);
		SelectedFarm8 = FarmChoices.Find((PairValueIntString farm8) => farm8.Value == Lord.farm_types[7]);
		SelectedBlackSmithMode = BlacksmithOperationModes.Find((PairValueIntString smithMode) => smithMode.Value == Lord.blacksmiths_make);
		SelectedFletcherMode = FletcherOperationModes.Find((PairValueIntString fletcherMode) => fletcherMode.Value == Lord.fletchers_make);
		SelectedPoleturnerMode = PoleturnerOperationModes.Find((PairValueIntString poleturnerMode) => poleturnerMode.Value == Lord.poleturners_make);
		BannedResource1 = GameResources.Find((PairValueIntString resource1) => resource1.Value == Lord.sell_all[0]);
		BannedResource2 = GameResources.Find((PairValueIntString resource2) => resource2.Value == Lord.sell_all[1]);
		BannedResource3 = GameResources.Find((PairValueIntString resource3) => resource3.Value == Lord.sell_all[2]);
		BannedResource4 = GameResources.Find((PairValueIntString resource4) => resource4.Value == Lord.sell_all[3]);
		BannedResource5 = GameResources.Find((PairValueIntString resource5) => resource5.Value == Lord.sell_all[4]);
		BannedResource6 = GameResources.Find((PairValueIntString resource6) => resource6.Value == Lord.sell_all[5]);
		BannedResource7 = GameResources.Find((PairValueIntString resource7) => resource7.Value == Lord.sell_all[6]);
		BannedResource8 = GameResources.Find((PairValueIntString resource8) => resource8.Value == Lord.sell_all[7]);
		BannedResource9 = GameResources.Find((PairValueIntString resource9) => resource9.Value == Lord.sell_all[8]);
		BannedResource10 = GameResources.Find((PairValueIntString resource10) => resource10.Value == Lord.sell_all[9]);
		BannedResource11 = GameResources.Find((PairValueIntString resource11) => resource11.Value == Lord.sell_all[10]);
		BannedResource12 = GameResources.Find((PairValueIntString resource12) => resource12.Value == Lord.sell_all[11]);
		BannedResource13 = GameResources.Find((PairValueIntString resource13) => resource13.Value == Lord.sell_all[12]);
		BannedResource14 = GameResources.Find((PairValueIntString resource14) => resource14.Value == Lord.sell_all[13]);
		BannedResource15 = GameResources.Find((PairValueIntString resource15) => resource15.Value == Lord.sell_all[14]);
		WeakDefenseModeProb = Lord.chance_of_defensive[1];
		WeakHarassModeProb = Lord.chance_of_harrasment[1];
		WeakSiegeModeProb = Lord.chance_of_seiging[1];
		WeakRecruitIntervalProb = RecruitmentIntervals.Find((PairValueIntString weakRecruit) => weakRecruit.Value == Lord.troop_production_rate[1]);
		NormalDefenseModeProb = Lord.chance_of_defensive[0];
		NormalHarassModeProb = Lord.chance_of_harrasment[0];
		NormalSiegeModeProb = Lord.chance_of_seiging[0];
		NormalRecruitIntervalProb = RecruitmentIntervals.Find((PairValueIntString NormalRecruit) => NormalRecruit.Value == Lord.troop_production_rate[0]);
		StrongDefenseModeProb = Lord.chance_of_defensive[2];
		StrongHarassModeProb = Lord.chance_of_harrasment[2];
		StrongSiegeModeProb = Lord.chance_of_seiging[2];
		StrongRecruitIntervalProb = RecruitmentIntervals.Find((PairValueIntString StrongRecruit) => StrongRecruit.Value == Lord.troop_production_rate[2]);
		DefensiveUnitType1 = AllTroops.Find((PairValueIntString defenseTroop1) => defenseTroop1.Value == Lord.defensive_troops[0]);
		DefensiveUnitType2 = AllTroops.Find((PairValueIntString defenseTroop2) => defenseTroop2.Value == Lord.defensive_troops[1]);
		DefensiveUnitType3 = AllTroops.Find((PairValueIntString defenseTroop3) => defenseTroop3.Value == Lord.defensive_troops[2]);
		DefensiveUnitType4 = AllTroops.Find((PairValueIntString defenseTroop4) => defenseTroop4.Value == Lord.defensive_troops[3]);
		DefensiveUnitType5 = AllTroops.Find((PairValueIntString defenseTroop5) => defenseTroop5.Value == Lord.defensive_troops[4]);
		DefensiveUnitType6 = AllTroops.Find((PairValueIntString defenseTroop6) => defenseTroop6.Value == Lord.defensive_troops[5]);
		DefensiveUnitType7 = AllTroops.Find((PairValueIntString defenseTroop7) => defenseTroop7.Value == Lord.defensive_troops[6]);
		DefensiveUnitType8 = AllTroops.Find((PairValueIntString defenseTroop8) => defenseTroop8.Value == Lord.defensive_troops[7]);
		TotalDefensiveTroopCount = Lord.defensive_trigger_level;
		CastleWallTroopCount = Lord.defense_patrol_trigger_level;
		WaypointPatrolInterval = Lord.move_mobile_defenders;
		WaypointPatrolGroupCount = Lord.max_mobile_groups;
		SiegeEngineGoldThreshold = Lord.buy_defense_machines_at;
		SiegeEnginePurchaseInterval = Lord.buy_defense_machines_delay;
		ProtectionPatrolInterval = Lord.defense_patrol_delay;
		SelectedMobilePatrolMode = MobilePatrolStyles.Find((PairValueIntString patrolStyle) => patrolStyle.Value == Lord.defense_patrol_style);
		ProtectionPatrolGroups = Lord.defense_patrols;
		SelectedEconomyDefenseTroop = AllTroopsReactive.Find((PairValueIntString defenseTroop) => defenseTroop.Value == Lord.economy_protection_type);
		EconomyDefenseTroopCount = Lord.economy_protection_number;
		SelectedBodyguardTroop = AllTroopsReactive.Find((PairValueIntString bodyguardTroop) => bodyguardTroop.Value == Lord.bodyguard_type);
		BodyguardTroopCount = Lord.bodyguard_number;
		SelectedDiggerTroop = DiggerTroops.Find((PairValueIntString diggerTroop) => diggerTroop.Value == Lord.moat_digger_type);
		DiggerTroopCount = Lord.moat_diggers;
		DogCageTriggerLevel = Lord.dog_points_count;
		DogCageReleaseDelayLevel = Lord.dog_release_timing;
		HarassTroopBaseCount = Lord.harrasment_trigger_level;
		HarassTroopCountVariance = Lord.harrasment_trigger_variance;
		HarassUnitType1 = AllTroops.Find((PairValueIntString harassTroop1) => harassTroop1.Value == Lord.harrasment_troops[0]);
		HarassUnitType2 = AllTroops.Find((PairValueIntString harassTroop2) => harassTroop2.Value == Lord.harrasment_troops[1]);
		HarassUnitType3 = AllTroops.Find((PairValueIntString harassTroop3) => harassTroop3.Value == Lord.harrasment_troops[2]);
		HarassUnitType4 = AllTroops.Find((PairValueIntString harassTroop4) => harassTroop4.Value == Lord.harrasment_troops[3]);
		HarassUnitType5 = AllTroops.Find((PairValueIntString harassTroop5) => harassTroop5.Value == Lord.harrasment_troops[4]);
		HarassUnitType6 = AllTroops.Find((PairValueIntString harassTroop6) => harassTroop6.Value == Lord.harrasment_troops[5]);
		HarassUnitType7 = AllTroops.Find((PairValueIntString harassTroop7) => harassTroop7.Value == Lord.harrasment_troops[6]);
		HarassUnitType8 = AllTroops.Find((PairValueIntString harassTroop8) => harassTroop8.Value == Lord.harrasment_troops[7]);
		HarassSiegeEngineCount = Lord.max_harrasment_machines;
		HarassSiegeEngineType1 = SiegeEngines.Find((PairValueIntString harassEngine1) => harassEngine1.Value == Lord.harrasment_machines[0]);
		HarassSiegeEngineType2 = SiegeEngines.Find((PairValueIntString harassEngine2) => harassEngine2.Value == Lord.harrasment_machines[1]);
		HarassSiegeEngineType3 = SiegeEngines.Find((PairValueIntString harassEngine3) => harassEngine3.Value == Lord.harrasment_machines[2]);
		HarassSiegeEngineType4 = SiegeEngines.Find((PairValueIntString harassEngine4) => harassEngine4.Value == Lord.harrasment_machines[3]);
		HarassSiegeEngineType5 = SiegeEngines.Find((PairValueIntString harassEngine5) => harassEngine5.Value == Lord.harrasment_machines[4]);
		HarassSiegeEngineType6 = SiegeEngines.Find((PairValueIntString harassEngine6) => harassEngine6.Value == Lord.harrasment_machines[5]);
		HarassSiegeEngineType7 = SiegeEngines.Find((PairValueIntString harassEngine7) => harassEngine7.Value == Lord.harrasment_machines[6]);
		HarassSiegeEngineType8 = SiegeEngines.Find((PairValueIntString harassEngine8) => harassEngine8.Value == Lord.harrasment_machines[7]);
		HarassRetargetDelay = Lord.harrass_delay;
		SiegeUnitBaseCount = Lord.siege_trigger_level;
		SiegeUnitCountVariance = Lord.siege_trigger_variance;
		SelectedVanguardTroop = AllTroops.Find((PairValueIntString vanguard) => vanguard.Value == Lord.siege_herring_troop);
		SelectedVanguardTroopLimit = Lord.siege_herring_amount;
		SelectedBackupTroop = AllTroops.Find((PairValueIntString backup) => backup.Value == Lord.siege_cover_troop);
		BackupTroopLimit = Lord.siege_cover_amount;
		BackupTroopGroups = Lord.siege_cover_tribes;
		SelectedPatrolTroopType = AllTroops.Find((PairValueIntString patrol) => patrol.Value == Lord.siege_storm_troop);
		PatrolTroopTypeGroups = Lord.siege_storm_tribes;
		PatrolTroopTypeLimit = Lord.siege_storm_amount;
		SelectedEngageTroopType = AllTroops.Find((PairValueIntString engage) => engage.Value == Lord.siege_shock_troop);
		EngageTroopLimit = Lord.siege_shock_amount;
		SelectedSiegeProtectionType = AllTroops.Find((PairValueIntString siegeProtect) => siegeProtect.Value == Lord.siege_reserve_troop);
		SiegeProtectionLimit = Lord.siege_reserve_amount;
		SiegeProtectionGroupCount = Lord.siege_reserve_tribes;
		SiegeArmyUnitType1 = AllTroops.Find((PairValueIntString siegeUnit1) => siegeUnit1.Value == Lord.siege_wall_troops[0]);
		SiegeArmyUnitType2 = AllTroops.Find((PairValueIntString siegeUnit2) => siegeUnit2.Value == Lord.siege_wall_troops[1]);
		SiegeArmyUnitType3 = AllTroops.Find((PairValueIntString siegeUnit3) => siegeUnit3.Value == Lord.siege_wall_troops[2]);
		SiegeArmyUnitType4 = AllTroops.Find((PairValueIntString siegeUnit4) => siegeUnit4.Value == Lord.siege_wall_troops[3]);
		SiegeArmyUnitType5 = AllTroops.Find((PairValueIntString siegeUnit5) => siegeUnit5.Value == Lord.siege_wall_troops[4]);
		SiegeArmyUnitType6 = AllTroops.Find((PairValueIntString siegeUnit6) => siegeUnit6.Value == Lord.siege_wall_troops[5]);
		SiegeArmyUnitType7 = AllTroops.Find((PairValueIntString siegeUnit7) => siegeUnit7.Value == Lord.siege_wall_troops[6]);
		SiegeArmyUnitType8 = AllTroops.Find((PairValueIntString siegeUnit8) => siegeUnit8.Value == Lord.siege_wall_troops[7]);
		SiegeArmyUnitType9 = AllTroops.Find((PairValueIntString siegeUnit9) => siegeUnit9.Value == Lord.siege_wall_troops[8]);
		SiegeArmyUnitType10 = AllTroops.Find((PairValueIntString siegeUnit10) => siegeUnit10.Value == Lord.siege_wall_troops[9]);
		SiegeArmyUnitType11 = AllTroops.Find((PairValueIntString siegeUnit11) => siegeUnit11.Value == Lord.siege_wall_troops[10]);
		SiegeArmyUnitType12 = AllTroops.Find((PairValueIntString siegeUnit12) => siegeUnit12.Value == Lord.siege_wall_troops[11]);
		SiegeArmyUnitType13 = AllTroops.Find((PairValueIntString siegeUnit13) => siegeUnit13.Value == Lord.siege_wall_troops[12]);
		SiegeArmyUnitType14 = AllTroops.Find((PairValueIntString siegeUnit14) => siegeUnit14.Value == Lord.siege_wall_troops[13]);
		SiegeArmyUnitType15 = AllTroops.Find((PairValueIntString siegeUnit15) => siegeUnit15.Value == Lord.siege_wall_troops[14]);
		SiegeArmyUnitType16 = AllTroops.Find((PairValueIntString siegeUnit16) => siegeUnit16.Value == Lord.siege_wall_troops[15]);
		SiegeArmyUnitType17 = AllTroops.Find((PairValueIntString siegeUnit17) => siegeUnit17.Value == Lord.siege_wall_troops[16]);
		SiegeArmyUnitType18 = AllTroops.Find((PairValueIntString siegeUnit18) => siegeUnit18.Value == Lord.siege_wall_troops[17]);
		SiegeArmyUnitType19 = AllTroops.Find((PairValueIntString siegeUnit19) => siegeUnit19.Value == Lord.siege_wall_troops[18]);
		SiegeArmyUnitType20 = AllTroops.Find((PairValueIntString siegeUnit20) => siegeUnit20.Value == Lord.siege_wall_troops[19]);
		SiegeArmyUnitType21 = AllTroops.Find((PairValueIntString siegeUnit21) => siegeUnit21.Value == Lord.siege_wall_troops[20]);
		SiegeArmyUnitType22 = AllTroops.Find((PairValueIntString siegeUnit22) => siegeUnit22.Value == Lord.siege_wall_troops[21]);
		SiegeArmyUnitType23 = AllTroops.Find((PairValueIntString siegeUnit23) => siegeUnit23.Value == Lord.siege_wall_troops[22]);
		SiegeArmyUnitType24 = AllTroops.Find((PairValueIntString siegeUnit24) => siegeUnit24.Value == Lord.siege_wall_troops[23]);
		SiegeArmyLimit = Lord.siege_wall_amount;
		SiegeArmyGroupCount = Lord.siege_wall_tribes;
		Siege_max_troops = Lord.siege_max_troops;
		Siege_normal_wave_multiplier = Lord.siege_normal_wave_multiplier;
		Siege_high_gold_wave_multiplier = Lord.siege_high_gold_wave_multiplier;
		SiegeAdvanceArmyThreshold = Lord.siege_troops_on_site_percent;
		SiegeRetreatThreshold = Lord.siege_troops_at_home_percent;
		CastleBombardmentDuration = Lord.siege_soften_up_delay;
		SuccessRetreatDelay = Lord.siege_victory_delay;
		CowThrowInterval = Lord.siege_cow_timer;
		UseAdvancedAI = (Lord.use_improved_sieging & 1) != 0;
		UseAdvancedAI2 = (Lord.use_improved_sieging & 2) != 0;
		SiegeArmySiegeEngineType1 = SiegeEngineChimps.Find((PairValueIntString armyEngine1) => armyEngine1.Value == Lord.siege_machines[0]);
		SiegeArmySiegeEngineType2 = SiegeEngineChimps.Find((PairValueIntString armyEngine2) => armyEngine2.Value == Lord.siege_machines[1]);
		SiegeArmySiegeEngineType3 = SiegeEngineChimps.Find((PairValueIntString armyEngine3) => armyEngine3.Value == Lord.siege_machines[2]);
		SiegeArmySiegeEngineType4 = SiegeEngineChimps.Find((PairValueIntString armyEngine4) => armyEngine4.Value == Lord.siege_machines[3]);
		SiegeArmySiegeEngineType5 = SiegeEngineChimps.Find((PairValueIntString armyEngine5) => armyEngine5.Value == Lord.siege_machines[4]);
		SiegeArmySiegeEngineType6 = SiegeEngineChimps.Find((PairValueIntString armyEngine6) => armyEngine6.Value == Lord.siege_machines[5]);
		SiegeArmySiegeEngineType7 = SiegeEngineChimps.Find((PairValueIntString armyEngine7) => armyEngine7.Value == Lord.siege_machines[6]);
		SiegeArmySiegeEngineType8 = SiegeEngineChimps.Find((PairValueIntString armyEngine8) => armyEngine8.Value == Lord.siege_machines[7]);
		EngineerLimit = Lord.siege_eng_amount;
		SelectedDiggerType = DiggerTroops.Find((PairValueIntString siegeDigger) => siegeDigger.Value == Lord.siege_moat_troop);
		DiggerLimit = Lord.siege_moat_amount;
		AssassinLimit = Lord.siege_assasin_amount;
		LaddermanLimit = Lord.siege_ladder_amount;
		TunnelerLimit = Lord.siege_tunnel_amount;
		TargetingPreference = TargetPreferences.Find((PairValueIntString targetPreference) => targetPreference.Value == Lord.who_to_pick_on);
		CooperationProb = Lord.percent_chance_waiting_for_joint_attack;
		JointSiegeTroopThreshold = Lord.siege_troops_before_will_come_to_rescue;
		NormalArcherStartingAmount = Lord.starting_troops_normal[0];
		NormalSpearmanStartingAmount = Lord.starting_troops_normal[2];
		NormalMacemanStartingAmount = Lord.starting_troops_normal[4];
		NormalCrossbowmanStartingAmount = Lord.starting_troops_normal[1];
		NormalPikemanStartingAmount = Lord.starting_troops_normal[3];
		NormalSwordsmanStartingAmount = Lord.starting_troops_normal[5];
		NormalKnightStartingAmount = Lord.starting_troops_normal[6];
		NormalArabianArcherStartingAmount = Lord.starting_troops_normal[10];
		NormalSlaveStartingAmount = Lord.starting_troops_normal[11];
		NormalSlingerStartingAmount = Lord.starting_troops_normal[12];
		NormalAssassinStartingAmount = Lord.starting_troops_normal[13];
		NormalHorseArcherStartingAmount = Lord.starting_troops_normal[14];
		NormalArabianSwordsmanStartingAmount = Lord.starting_troops_normal[15];
		NormalFireThrowerStartingAmount = Lord.starting_troops_normal[16];
		NormalSkirmisherStartingAmount = Lord.starting_troops_normal[24];
		NormalSapperStartingAmount = Lord.starting_troops_normal[26];
		NormalCamelLancerStartingAmount = Lord.starting_troops_normal[20];
		NormalHeavyCamelStartingAmount = Lord.starting_troops_normal[25];
		NormalDemolisherStartingAmount = Lord.starting_troops_normal[27];
		NormalEunuchStartingAmount = Lord.starting_troops_normal[22];
		NormalHealerStartingAmount = Lord.starting_troops_normal[21];
		NormalAmbusherStartingAmount = Lord.starting_troops_normal[23];
		NormalEngineerStartingAmount = Lord.starting_troops_normal[8];
		NormalMonkStartingAmount = Lord.starting_troops_normal[9];
		NormalFireBallistaStartingAmount = (Lord.starting_troops_normal[17] = 0);
		DeathmatchArcherStartingAmount = Lord.starting_troops_deathmatch[0];
		DeathmatchSpearmanStartingAmount = Lord.starting_troops_deathmatch[2];
		DeathmatchMacemanStartingAmount = Lord.starting_troops_deathmatch[4];
		DeathmatchCrossbowmanStartingAmount = Lord.starting_troops_deathmatch[1];
		DeathmatchPikemanStartingAmount = Lord.starting_troops_deathmatch[3];
		DeathmatchSwordsmanStartingAmount = Lord.starting_troops_deathmatch[5];
		DeathmatchKnightStartingAmount = Lord.starting_troops_deathmatch[6];
		DeathmatchArabianArcherStartingAmount = Lord.starting_troops_deathmatch[10];
		DeathmatchSlaveStartingAmount = Lord.starting_troops_deathmatch[11];
		DeathmatchSlingerStartingAmount = Lord.starting_troops_deathmatch[12];
		DeathmatchAssassinStartingAmount = Lord.starting_troops_deathmatch[13];
		DeathmatchHorseArcherStartingAmount = Lord.starting_troops_deathmatch[14];
		DeathmatchArabianSwordsmanStartingAmount = Lord.starting_troops_deathmatch[15];
		DeathmatchFireThrowerStartingAmount = Lord.starting_troops_deathmatch[16];
		DeathmatchSkirmisherStartingAmount = Lord.starting_troops_deathmatch[24];
		DeathmatchSapperStartingAmount = Lord.starting_troops_deathmatch[26];
		DeathmatchCamelLancerStartingAmount = Lord.starting_troops_deathmatch[20];
		DeathmatchHeavyCamelStartingAmount = Lord.starting_troops_deathmatch[25];
		DeathmatchDemolisherStartingAmount = Lord.starting_troops_deathmatch[27];
		DeathmatchEunuchStartingAmount = Lord.starting_troops_deathmatch[22];
		DeathmatchHealerStartingAmount = Lord.starting_troops_deathmatch[21];
		DeathmatchAmbusherStartingAmount = Lord.starting_troops_deathmatch[23];
		DeathmatchEngineerStartingAmount = Lord.starting_troops_deathmatch[8];
		DeathmatchMonkStartingAmount = Lord.starting_troops_deathmatch[9];
		DeathmatchFireBallistaStartingAmount = (Lord.starting_troops_deathmatch[17] = 0);
		CrusaderArcherStartingAmount = Lord.starting_troops_crusader[0];
		CrusaderSpearmanStartingAmount = Lord.starting_troops_crusader[2];
		CrusaderMacemanStartingAmount = Lord.starting_troops_crusader[4];
		CrusaderCrossbowmanStartingAmount = Lord.starting_troops_crusader[1];
		CrusaderPikemanStartingAmount = Lord.starting_troops_crusader[3];
		CrusaderSwordsmanStartingAmount = Lord.starting_troops_crusader[5];
		CrusaderKnightStartingAmount = Lord.starting_troops_crusader[6];
		CrusaderArabianArcherStartingAmount = Lord.starting_troops_crusader[10];
		CrusaderSlaveStartingAmount = Lord.starting_troops_crusader[11];
		CrusaderSlingerStartingAmount = Lord.starting_troops_crusader[12];
		CrusaderAssassinStartingAmount = Lord.starting_troops_crusader[13];
		CrusaderHorseArcherStartingAmount = Lord.starting_troops_crusader[14];
		CrusaderArabianSwordsmanStartingAmount = Lord.starting_troops_crusader[15];
		CrusaderFireThrowerStartingAmount = Lord.starting_troops_crusader[16];
		CrusaderSkirmisherStartingAmount = Lord.starting_troops_crusader[24];
		CrusaderSapperStartingAmount = Lord.starting_troops_crusader[26];
		CrusaderCamelLancerStartingAmount = Lord.starting_troops_crusader[20];
		CrusaderHeavyCamelStartingAmount = Lord.starting_troops_crusader[25];
		CrusaderDemolisherStartingAmount = Lord.starting_troops_crusader[27];
		CrusaderEunuchStartingAmount = Lord.starting_troops_crusader[22];
		CrusaderHealerStartingAmount = Lord.starting_troops_crusader[21];
		CrusaderAmbusherStartingAmount = Lord.starting_troops_crusader[23];
		CrusaderEngineerStartingAmount = Lord.starting_troops_crusader[8];
		CrusaderMonkStartingAmount = Lord.starting_troops_crusader[9];
		CrusaderFireBallistaStartingAmount = (Lord.starting_troops_crusader[17] = 0);
		inSetupPhase = false;
		AreChangesPresent = false;
	}

	private void InitCollections()
	{
		LordFlagTypes = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "None",
				Value = -1
			},
			new PairValueIntString
			{
				Key = "Small flag",
				Value = 10
			},
			new PairValueIntString
			{
				Key = "Medium flag",
				Value = 11
			},
			new PairValueIntString
			{
				Key = "Large flag",
				Value = 12
			},
			new PairValueIntString
			{
				Key = "Giant flag",
				Value = 13
			},
			new PairValueIntString
			{
				Key = "Skull",
				Value = 15
			}
		};
		LordPowerDisplayLevels = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "Extremely weak",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Very weak",
				Value = 1
			},
			new PairValueIntString
			{
				Key = "Moderately weak",
				Value = 2
			},
			new PairValueIntString
			{
				Key = "Mildly weak",
				Value = 3
			},
			new PairValueIntString
			{
				Key = "Slightly weak",
				Value = 4
			},
			new PairValueIntString
			{
				Key = "Average",
				Value = 5
			},
			new PairValueIntString
			{
				Key = "Slightly strong",
				Value = 6
			},
			new PairValueIntString
			{
				Key = "Mildly strong",
				Value = 7
			},
			new PairValueIntString
			{
				Key = "Moderately strong",
				Value = 8
			},
			new PairValueIntString
			{
				Key = "Very strong",
				Value = 9
			},
			new PairValueIntString
			{
				Key = "Extremely strong",
				Value = 10
			}
		};
		LordFactions = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "Crusader",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Female Crusader",
				Value = 4
			},
			new PairValueIntString
			{
				Key = "Arabic",
				Value = 1
			},
			new PairValueIntString
			{
				Key = "Female Arabic",
				Value = 6
			},
			new PairValueIntString
			{
				Key = "Bedouin",
				Value = 2
			},
			new PairValueIntString
			{
				Key = "Female Bedouin",
				Value = 7
			}
		};
		AleAllowances = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "No Ale",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Low Ale",
				Value = 1
			},
			new PairValueIntString
			{
				Key = "High Ale",
				Value = 2
			}
		};
		ReligionAllowances = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "No Religion",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Low Religion",
				Value = 1
			},
			new PairValueIntString
			{
				Key = "High Religion",
				Value = 2
			}
		};
		TaxLevels = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "Generous bribe",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Large bribe",
				Value = 1
			},
			new PairValueIntString
			{
				Key = "Small bribe",
				Value = 2
			},
			new PairValueIntString
			{
				Key = "None",
				Value = 3
			},
			new PairValueIntString
			{
				Key = "Low",
				Value = 4
			},
			new PairValueIntString
			{
				Key = "Average",
				Value = 5
			},
			new PairValueIntString
			{
				Key = "High",
				Value = 6
			},
			new PairValueIntString
			{
				Key = "Mean",
				Value = 7
			},
			new PairValueIntString
			{
				Key = "Extortionate",
				Value = 8
			},
			new PairValueIntString
			{
				Key = "Downright cruel",
				Value = 9
			},
			new PairValueIntString
			{
				Key = "Even crueler",
				Value = 10
			},
			new PairValueIntString
			{
				Key = "Crueler than cruel",
				Value = 11
			},
			new PairValueIntString
			{
				Key = "Cheating",
				Value = 12
			}
		};
		FarmChoices = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "None",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Wheat farm",
				Value = 30
			},
			new PairValueIntString
			{
				Key = "Hops farm",
				Value = 31
			},
			new PairValueIntString
			{
				Key = "Apple orchard",
				Value = 32
			},
			new PairValueIntString
			{
				Key = "Dairy farm",
				Value = 33
			}
		};
		BlacksmithOperationModes = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "Maces",
				Value = 21
			},
			new PairValueIntString
			{
				Key = "Swords",
				Value = 22
			},
			new PairValueIntString
			{
				Key = "Both",
				Value = -999
			}
		};
		FletcherOperationModes = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "Bows",
				Value = 17
			},
			new PairValueIntString
			{
				Key = "Crossbows",
				Value = 18
			},
			new PairValueIntString
			{
				Key = "Both",
				Value = -999
			}
		};
		PoleturnerOperationModes = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "Spears",
				Value = 19
			},
			new PairValueIntString
			{
				Key = "Pikes",
				Value = 20
			},
			new PairValueIntString
			{
				Key = "Both",
				Value = -999
			}
		};
		GameResources = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "None",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Wood",
				Value = 2
			},
			new PairValueIntString
			{
				Key = "Hops",
				Value = 3
			},
			new PairValueIntString
			{
				Key = "Stone",
				Value = 4
			},
			new PairValueIntString
			{
				Key = "Leather",
				Value = 5
			},
			new PairValueIntString
			{
				Key = "Iron",
				Value = 6
			},
			new PairValueIntString
			{
				Key = "Pitch",
				Value = 7
			},
			new PairValueIntString
			{
				Key = "Wheat",
				Value = 9
			},
			new PairValueIntString
			{
				Key = "Bread",
				Value = 10
			},
			new PairValueIntString
			{
				Key = "Cheese",
				Value = 11
			},
			new PairValueIntString
			{
				Key = "Meat",
				Value = 12
			},
			new PairValueIntString
			{
				Key = "Apples",
				Value = 13
			},
			new PairValueIntString
			{
				Key = "Ale",
				Value = 14
			},
			new PairValueIntString
			{
				Key = "Flour",
				Value = 16
			},
			new PairValueIntString
			{
				Key = "Bows",
				Value = 17
			},
			new PairValueIntString
			{
				Key = "Crossbows",
				Value = 18
			},
			new PairValueIntString
			{
				Key = "Spears",
				Value = 19
			},
			new PairValueIntString
			{
				Key = "Pikes",
				Value = 20
			},
			new PairValueIntString
			{
				Key = "Maces",
				Value = 21
			},
			new PairValueIntString
			{
				Key = "Swords",
				Value = 22
			},
			new PairValueIntString
			{
				Key = "Leather Armor",
				Value = 23
			},
			new PairValueIntString
			{
				Key = "Metal Armor",
				Value = 24
			}
		};
		AllTroops = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "None",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Tunneler",
				Value = 5
			},
			new PairValueIntString
			{
				Key = "Archer",
				Value = 22
			},
			new PairValueIntString
			{
				Key = "Crossbowman",
				Value = 23
			},
			new PairValueIntString
			{
				Key = "Spearman",
				Value = 24
			},
			new PairValueIntString
			{
				Key = "Pikeman",
				Value = 25
			},
			new PairValueIntString
			{
				Key = "Maceman",
				Value = 26
			},
			new PairValueIntString
			{
				Key = "Swordsman",
				Value = 27
			},
			new PairValueIntString
			{
				Key = "Knight",
				Value = 28
			},
			new PairValueIntString
			{
				Key = "Ladderman",
				Value = 29
			},
			new PairValueIntString
			{
				Key = "Engineer",
				Value = 30
			},
			new PairValueIntString
			{
				Key = "Monk",
				Value = 37
			},
			new PairValueIntString
			{
				Key = "Catapult",
				Value = 39
			},
			new PairValueIntString
			{
				Key = "Trebuchet",
				Value = 40
			},
			new PairValueIntString
			{
				Key = "Mangonel",
				Value = 41
			},
			new PairValueIntString
			{
				Key = "Siege tower",
				Value = 58
			},
			new PairValueIntString
			{
				Key = "Battering ram",
				Value = 59
			},
			new PairValueIntString
			{
				Key = "Portable shield",
				Value = 60
			},
			new PairValueIntString
			{
				Key = "Ballista",
				Value = 61
			},
			new PairValueIntString
			{
				Key = "Arabian archer",
				Value = 70
			},
			new PairValueIntString
			{
				Key = "Slave",
				Value = 71
			},
			new PairValueIntString
			{
				Key = "Slinger",
				Value = 72
			},
			new PairValueIntString
			{
				Key = "Assassin",
				Value = 73
			},
			new PairValueIntString
			{
				Key = "Horse archer",
				Value = 74
			},
			new PairValueIntString
			{
				Key = "Arabian swordsman",
				Value = 75
			},
			new PairValueIntString
			{
				Key = "Fire thrower",
				Value = 76
			},
			new PairValueIntString
			{
				Key = "Fire ballista",
				Value = 77
			},
			new PairValueIntString
			{
				Key = "Camel lancer",
				Value = 78
			},
			new PairValueIntString
			{
				Key = "Healer",
				Value = 79
			},
			new PairValueIntString
			{
				Key = "Eunuch",
				Value = 80
			},
			new PairValueIntString
			{
				Key = "Ambusher",
				Value = 81
			},
			new PairValueIntString
			{
				Key = "Skirmisher",
				Value = 82
			},
			new PairValueIntString
			{
				Key = "Heavy camel",
				Value = 83
			},
			new PairValueIntString
			{
				Key = "Sapper",
				Value = 84
			},
			new PairValueIntString
			{
				Key = "Demolisher",
				Value = 85
			}
		};
		AllTroopsReactive = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "Tunneler",
				Value = 5
			},
			new PairValueIntString
			{
				Key = "Archer",
				Value = 22
			},
			new PairValueIntString
			{
				Key = "Crossbowman",
				Value = 23
			},
			new PairValueIntString
			{
				Key = "Spearman",
				Value = 24
			},
			new PairValueIntString
			{
				Key = "Pikeman",
				Value = 25
			},
			new PairValueIntString
			{
				Key = "Maceman",
				Value = 26
			},
			new PairValueIntString
			{
				Key = "Swordsman",
				Value = 27
			},
			new PairValueIntString
			{
				Key = "Knight",
				Value = 28
			},
			new PairValueIntString
			{
				Key = "Engineer",
				Value = 30
			},
			new PairValueIntString
			{
				Key = "Monk",
				Value = 37
			},
			new PairValueIntString
			{
				Key = "Arabian archer",
				Value = 70
			},
			new PairValueIntString
			{
				Key = "Slave",
				Value = 71
			},
			new PairValueIntString
			{
				Key = "Slinger",
				Value = 72
			},
			new PairValueIntString
			{
				Key = "Assassin",
				Value = 73
			},
			new PairValueIntString
			{
				Key = "Horse archer",
				Value = 74
			},
			new PairValueIntString
			{
				Key = "Arabian swordsman",
				Value = 75
			},
			new PairValueIntString
			{
				Key = "Fire thrower",
				Value = 76
			},
			new PairValueIntString
			{
				Key = "Camel lancer",
				Value = 78
			},
			new PairValueIntString
			{
				Key = "Healer",
				Value = 79
			},
			new PairValueIntString
			{
				Key = "Eunuch",
				Value = 80
			},
			new PairValueIntString
			{
				Key = "Ambusher",
				Value = 81
			},
			new PairValueIntString
			{
				Key = "Skirmisher",
				Value = 82
			},
			new PairValueIntString
			{
				Key = "Heavy camel",
				Value = 83
			},
			new PairValueIntString
			{
				Key = "Sapper",
				Value = 84
			},
			new PairValueIntString
			{
				Key = "Demolisher",
				Value = 85
			}
		};
		DiggerTroops = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "None",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Archer",
				Value = 22
			},
			new PairValueIntString
			{
				Key = "Spearman",
				Value = 24
			},
			new PairValueIntString
			{
				Key = "Pikeman",
				Value = 25
			},
			new PairValueIntString
			{
				Key = "Maceman",
				Value = 26
			},
			new PairValueIntString
			{
				Key = "Engineer",
				Value = 30
			},
			new PairValueIntString
			{
				Key = "Slave",
				Value = 71
			},
			new PairValueIntString
			{
				Key = "Eunuch",
				Value = 80
			},
			new PairValueIntString
			{
				Key = "Ambusher",
				Value = 81
			},
			new PairValueIntString
			{
				Key = "Skirmisher",
				Value = 82
			},
			new PairValueIntString
			{
				Key = "Sapper",
				Value = 84
			},
			new PairValueIntString
			{
				Key = "Demolisher",
				Value = 85
			}
		};
		SiegeEngines = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "None",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Catapult",
				Value = 190
			},
			new PairValueIntString
			{
				Key = "Trebuchet",
				Value = 191
			},
			new PairValueIntString
			{
				Key = "Siege tower",
				Value = 192
			},
			new PairValueIntString
			{
				Key = "Battering ram",
				Value = 193
			},
			new PairValueIntString
			{
				Key = "Portable shield",
				Value = 194
			},
			new PairValueIntString
			{
				Key = "Fire ballista",
				Value = 358
			}
		};
		SiegeEngineChimps = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "None",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Catapult",
				Value = 39
			},
			new PairValueIntString
			{
				Key = "Trebuchet",
				Value = 40
			},
			new PairValueIntString
			{
				Key = "Mangonel",
				Value = 41
			},
			new PairValueIntString
			{
				Key = "Siege tower",
				Value = 58
			},
			new PairValueIntString
			{
				Key = "Battering ram",
				Value = 59
			},
			new PairValueIntString
			{
				Key = "Portable shield",
				Value = 60
			},
			new PairValueIntString
			{
				Key = "Ballista",
				Value = 61
			},
			new PairValueIntString
			{
				Key = "Fire ballista",
				Value = 77
			}
		};
		RecruitmentIntervals = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "Twice per day",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Once per day",
				Value = 1
			},
			new PairValueIntString
			{
				Key = "Once per week",
				Value = 4
			},
			new PairValueIntString
			{
				Key = "Once every two weeks",
				Value = 8
			}
		};
		TargetPreferences = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "Richest player",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Weakest player",
				Value = 1
			},
			new PairValueIntString
			{
				Key = "Closest player",
				Value = 2
			},
			new PairValueIntString
			{
				Key = "Any player",
				Value = 3
			},
			new PairValueIntString
			{
				Key = "Human players",
				Value = 4
			}
		};
		MobilePatrolStyles = new List<PairValueIntString>
		{
			new PairValueIntString
			{
				Key = "Static",
				Value = 0
			},
			new PairValueIntString
			{
				Key = "Mobile",
				Value = 1
			}
		};
	}

	private void OnEditLordCancelPress(object parameter)
	{
		if (AreChangesPresent)
		{
			MainViewModel.instance.ShouldShowQuitWithoutSavingPanel(should: true);
		}
		else
		{
			MainViewModel.instance.ShowMainMenu();
		}
	}

	private void EditLordSavePress(object parameter)
	{
		MainViewModel.instance.DisplaySavePanel(aivs: false, lords: true);
	}
}
