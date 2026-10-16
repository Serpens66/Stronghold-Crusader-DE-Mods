using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using Fixes.Config;
using Fixes.Detours;
using Fixes.Events;
using Fixes.UI;
using Fixes.Util;
using Microsoft.Extensions.Logging;
using R3;
using RedBird.Core.Memory.Scanners;
using RedBird.X64.Assembly.Stateful;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Logging;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.GameGlobals;
using Serilog.Extensions.Logging;

namespace Fixes.Boostrap;

[BepInDependency(/*Could not decode attribute arguments.*/)]
[BepInPlugin("fixes", "Fixes", "1.26.4.0")]
public class Plugin : BaseUnityPlugin, IRelocatedNativeBuffers
{
	public enum UnstuckSiegeEngineBehaviour
	{
		Move,
		Delete,
		DeleteWithEngineers
	}

	public const string PLUGIN_GUID = "fixes";

	public const string PLUGIN_NAME = "Fixes";

	public const string PLUGIN_VERSION = "1.26.4.0";

	private const string SHCDESE_GUID = "000shcdese";

	public static Plugin Instance;

	private ModLogHelper Log;

	public ILoggerFactory LoggerFactory;

	public ManagedAssemblyImmediate<ushort>? MinimumKeepPCLTilesToBeEnclosed;

	public ConfigEntry<bool> EnableHopsFarmFix;

	public ConfigEntry<bool> ForceHopsFarmFix;

	public ConfigEntry<bool> EnableWheatSaleCategoryFix;

	public ConfigEntry<bool> ModularGoodsyardPlacement;

	public ConfigEntry<bool> AllowKeep3ForAIVs;

	public ConfigEntry<bool> EnableGatehouseFarmerFix;

	public ConfigEntry<bool> EnableAISelectSiegeRallypointOverrides;

	public ConfigEntry<int> AISelectSiegeRallypointMaxSearchRadius;

	public ConfigEntry<int> AISelectSiegeRallypointPreferredStandoffDistance;

	public ConfigEntry<ushort> AIPathfindingMaxDistance;

	public ConfigEntry<bool> EnableAIDistancedSiegeTents;

	public ConfigEntry<bool> EnableHarassingTentIdleEngineersFix;

	public ConfigEntry<int> HarassingTentIdleEngineersFixDelayAmount;

	public ConfigEntry<bool> EnableUnstuckUnitsOnAIBuiltWall;

	public ConfigEntry<bool> UnstuckUnitsOnAIBuiltWallOnlySiegeUnits;

	public ConfigEntry<int> UnstuckUnitsOnAIBuiltWallSiegeUnitReassignmentDelay;

	public ConfigEntry<UnstuckSiegeEngineBehaviour> UnstuckUnitsOnAIBuiltWallSiegeBehaviour;

	public ConfigEntry<bool> EnableProperLowWallCostFix;

	public ConfigEntry<bool> EnableCustomHovelBuildingLogic;

	public ConfigEntry<bool> EnableGlobalOverrideCustomHovelBuildingLogic;

	public ConfigEntry<int> CustomHovelBuildingLogicMinimumCivilianHousingSpace;

	public ConfigEntry<int> CustomHovelBuildingLogicRequiredCurrentIdlePeasantCount;

	public ConfigEntry<int> CustomHovelBuildingLogicRequiredAverageIdlePeasantCount;

	public ConfigEntry<int> CustomHovelBuildingLogicRequiredMinimumPopularity;

	public ConfigEntry<bool> EnableLowWallRefundFix;

	public ConfigEntry<bool> EnableMissingHorsePurchasingFix;

	public ConfigEntry<bool> EnableExtendedPCLArray;

	public ConfigEntry<bool> EnableWorkersKeepPCLDependencyOriginFix;

	public ConfigEntry<bool> EnableMountedUnitsRetreatFix;

	public ConfigEntry<bool> EnableUnitsSpeedReductionOnSelectionFix;

	public ConfigEntry<bool> EnableProductionBuildingsDontLoseStorageOnSleep;

	public ConfigEntry<bool> EnablePerLordCustomRequiredDefensiveRecruitmentTickTime;

	public ConfigEntry<bool> AllowAIToBuiltTowersAndSimiliarOverAlliedUnits;

	public ConfigEntry<bool> AllowLethalAILordTargeting;

	public ConfigEntry<bool> ImplementStrongestAttackPrioritySupport;

	public ConfigEntry<bool> AllowCustomPlayerKeepRotations;

	public ConfigEntry<bool> AssignNewTribeOnUnitTypeTrim;

	public ConfigEntry<bool> RelocateAIVOrderedMapTileIds;

	public ConfigEntry<bool> AllowCustomMinGoldForHarassmentSiegeEngines;

	public ConfigEntry<bool> EnableDisableAllWildlifeOption;

	public ConfigEntry<bool> EnableTunnelersDontDamageAlliedStuff;

	public ConfigEntry<bool> EnableStuckOxGruntsFix;

	public ConfigEntry<bool> AllowCustomStoneToOxenRatio;

	public ConfigEntry<bool> AllowCustomMaxOxenAmount;

	public ConfigEntry<bool> EnableStair6SlowdownFix;

	public ConfigEntry<bool> EnableArabBallistaOffsetFix;

	public ConfigEntry<bool> AllowLordMovementFreedomPatch;

	public ConfigEntry<bool> EnableWorkersStuckInMoatFix;

	public ConfigEntry<bool> AllowDeletionOfMoatsWithUnitsInsidePatch;

	public ConfigEntry<bool> AllowCustomAIPurchasingRoutines;

	public ConfigEntry<bool> AllowCustomGoodBadRequirements;

	public ConfigEntry<bool> AllowCustomSiegeLifetime;

	public ConfigEntry<bool> AllowCustomAIControl;

	public ConfigEntry<bool> SmarterSiegeAssassins;

	public ConfigEntry<bool> SmarterSiegeLaddermen;

	public ConfigEntry<bool> AllowCustomTunnelerDigRange;

	public ConfigEntry<bool> AllowSiegeAssassinsIgnoreNonSiegeTargets;

	public ConfigEntry<bool> FixPlacementSelectionNotAccountingForFarmland;

	public Dictionary<string, CustomLordPreferencesEntry> CustomLordPreferences { get; private set; }

	public Dictionary<AILords, bool>? HopsFarmWhitelist { get; private set; }

	public LobbySettingsViewModel LobbySettingsViewModel { get; private set; }

	public IntPtr ComponentTileCountsAddress { get; internal set; }

	public IntPtr ComponentVisitGenerationsAddress { get; internal set; }

	public int ComponentCapacity
	{
		get
		{
			if (!(ComponentTileCountsAddress != IntPtr.Zero) || !(ComponentVisitGenerationsAddress != IntPtr.Zero))
			{
				return 0;
			}
			return 4000;
		}
	}

	public IntPtr OrderedMapTileIdsAddress { get; internal set; }

	public int OrderedMapTileCapacityPerVillage
	{
		get
		{
			if (!(OrderedMapTileIdsAddress != IntPtr.Zero))
			{
				return 0;
			}
			return 40000;
		}
	}

	private Plugin()
	{
		Instance = this;
		CustomLordPreferences = new Dictionary<string, CustomLordPreferencesEntry>(StringComparer.OrdinalIgnoreCase);
		HopsFarmWhitelist = new Dictionary<AILords, bool>();
	}

	private void Awake()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Expected O, but got Unknown
		Log = ModLoggerFactory.CreateHelper("Fixes");
		LoggerFactory = (ILoggerFactory)new SerilogLoggerFactory(Log.GetLogger(), false, (LoggerProviderCollection)null);
		PluginConfigAwake();
		Log.Information("Plugin Fixes is loading!", "Awake", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Bootstrap\\Plugin.cs");
		CrusaderLibrary.Instance.LibraryLoaded += new OnLibraryLoadedDelegate(CrusaderLibrary_LibraryLoaded);
		ObservableSubscribeExtensions.Subscribe<AIProcessCustomLordEventArgs>(AIR3EventHooks.OnAIProcessCustomLord.Observable, (Action<AIProcessCustomLordEventArgs>)FixesAIEvents.OnProcessCustomLord);
		ObservableSubscribeExtensions.Subscribe<AISelectSiegeRallypointEventArgs>(AIR3EventHooks.OnAISelectSiegeRallypoint.Observable, (Action<AISelectSiegeRallypointEventArgs>)FixesAIEvents.OnAISelectSiegeRallypoint);
		if (EnableUnstuckUnitsOnAIBuiltWall.Value)
		{
			ObservableSubscribeExtensions.Subscribe<AIBuildWallEventArgs>(AIR3EventHooks.OnAIBuildWall.Observable, (Action<AIBuildWallEventArgs>)FixesAIEvents.OnAIBuildWall);
		}
		ObservableSubscribeExtensions.Subscribe<AIQueryBuildHovelEventArgs>(AIR3EventHooks.OnAIQueryBuildHovelEventArgs.Observable, (Action<AIQueryBuildHovelEventArgs>)FixesAIEvents.OnAIShouldBuildHovel);
		if (SmarterSiegeAssassins.Value)
		{
			ObservableSubscribeExtensions.Subscribe<BuildingCaptureEventArgs>(BuildingR3EventHooks.OnBuildingCapture.Observable, (Action<BuildingCaptureEventArgs>)FixesBuildingEvents.OnBuildingCapture);
		}
		ObservableSubscribeExtensions.Subscribe<MapPostLoadEventArgs>(MapLoaderR3EventHooks.OnPostLoad.Observable, (Action<MapPostLoadEventArgs>)FixesMapEvents.OnMapPostLoad);
		ObservableSubscribeExtensions.Subscribe<MapUnloadEventArgs>(MapLoaderR3EventHooks.OnUnloadMap.Observable, (Action<MapUnloadEventArgs>)FixesMapEvents.OnUnloadMap);
		LoadExternalConfigs();
		OverrideGlobals();
		Log.Information("Plugin Fixes is loaded!", "Awake", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Bootstrap\\Plugin.cs");
	}

	private void OverrideGlobals()
	{
		Log.Information("Overriding globals...", "OverrideGlobals", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Bootstrap\\Plugin.cs");
		((AssemblyValueSite<ushort>)(object)GameGlobalsManager.Instance.PathfindingMaxTilesConstraint)?.SetValue(AIPathfindingMaxDistance.Value);
	}

	private void LoadExternalConfigs()
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		Log.Information("Loading external configuration...", "LoadExternalConfigs", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Bootstrap\\Plugin.cs");
		JsonSerializerOptions val = new JsonSerializerOptions
		{
			WriteIndented = true
		};
		string modDataDirectory = IOHelper.ModDataDirectory;
		Directory.CreateDirectory(modDataDirectory);
		string path = Path.Combine(modDataDirectory, "hopsFarmWhitelist.json");
		if (File.Exists(path))
		{
			HopsFarmWhitelist = JsonSerializer.Deserialize<Dictionary<AILords, bool>>(File.ReadAllText(path), (JsonSerializerOptions)null);
			return;
		}
		HopsFarmWhitelist = new Dictionary<AILords, bool>();
		foreach (AILords value in Enum.GetValues(typeof(AILords)))
		{
			HopsFarmWhitelist.Add(value, value: false);
		}
		File.WriteAllText(path, JsonSerializer.Serialize<Dictionary<AILords, bool>>(HopsFarmWhitelist, val));
	}

	private void CrusaderLibrary_LibraryLoaded(CrusaderLibraryLoadContext context)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		try
		{
			AobCacheOptions val = new AobCacheOptions(Path.Combine(IOHelper.ModDataDirectory, "aobcache.json"));
			DataScanner scanner = context.CreateScanner(val, Instance.LoggerFactory.CreateLogger("Scanner"));
			DetourManager.Instance.ApplyNative(context, scanner, val);
			LobbySettingsViewModel = new LobbySettingsViewModel();
			GameXAMLManagerAPI.Instance.RegisterLobbyModSettings((BaseUnityPlugin)(object)this, "Fixes", (object)LobbySettingsViewModel, "ScriptExtenderUI/FixesSettings.xaml");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error during LibraryLoad event", "CrusaderLibrary_LibraryLoaded", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Bootstrap\\Plugin.cs");
		}
	}

	private void PluginConfigAwake()
	{
		EnableHopsFarmFix = ((BaseUnityPlugin)this).Config.Bind<bool>("HopsfarmFix", "Enabled", true, "Enable Hops Farm Fix");
		ForceHopsFarmFix = ((BaseUnityPlugin)this).Config.Bind<bool>("HopsfarmFix", "Force", false, "Force the Hops Farm Fix to affect all AIs");
		AllowCustomPlayerKeepRotations = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowCustomPlayerKeepRotations", "Enabled", true, "Allows players to decide which way their keep is rotated towards.");
		EnableWheatSaleCategoryFix = ((BaseUnityPlugin)this).Config.Bind<bool>("EnableWheatSaleCategoryFix", "Enabled", false, "Enables the fix for a bug in the AIs market-sell logic where raw wheat was incorrectly categorized");
		ModularGoodsyardPlacement = ((BaseUnityPlugin)this).Config.Bind<bool>("ModularGoodsyardPlacement", "Enabled", true, "Enables the per-player toggle for goodsyard placement");
		AllowKeep3ForAIVs = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowKeep3ForAIVs", "Enabled", true, "Enable KEEP3 usage for AIVs");
		EnableGatehouseFarmerFix = ((BaseUnityPlugin)this).Config.Bind<bool>("GatehouseFarmerFix", "Enabled", true, "Enables fix for wheat and cattle farmers that get stuck on gatehouses that automatically closed");
		EnableAISelectSiegeRallypointOverrides = ((BaseUnityPlugin)this).Config.Bind<bool>("AISelectSiegeRallypointOverrides", "Enabled", false, "Enables special overrides that will be active for all ais in the game in relation to sieges");
		AISelectSiegeRallypointMaxSearchRadius = ((BaseUnityPlugin)this).Config.Bind<int>("AISelectSiegeRallypointOverrides", "MaxSearchRadius", 110, "Limit for preferred standoff distance");
		AISelectSiegeRallypointPreferredStandoffDistance = ((BaseUnityPlugin)this).Config.Bind<int>("AISelectSiegeRallypointOverrides", "PreferredStandoffDistance", 90, "Preferred distance from besieged castle");
		AllowAIToBuiltTowersAndSimiliarOverAlliedUnits = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowAIToBuiltTowersAndSimiliarOverAlliedUnits", "Enabled", true, "Allows AIs to build certain buildings such as Towers or Gatehouses over allied units.");
		AllowCustomStoneToOxenRatio = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowCustomStoneToOxenRatio", "Enabled", true, "Allows AI lords to supply their own stone to oxen ratio.");
		AllowCustomMaxOxenAmount = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowCustomMaxOxenAmount", "Enabled", true, "Allows AI lords to supply their own max oxen amount.");
		AllowLethalAILordTargeting = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowLethalAILordTargeting", "Enabled", true, "The game is very lenient by default and does not like targetting any lords themselves. This setting overrides this behaviour, and any siege-assigned tribes are now LETHAL.");
		AIPathfindingMaxDistance = ((BaseUnityPlugin)this).Config.Bind<ushort>("Pathfinding", "MaxTilesDistance", (ushort)2000, "The maximum amount of tiles the game will query before giving up. Warn: Do not mess with this too much, it can and will impact performance alot.");
		EnableExtendedPCLArray = ((BaseUnityPlugin)this).Config.Bind<bool>("Pathfinding", "EnableExtendedPCLArray", true, "Increases the maximum amount of Path Connection Layers (PCL)s available for the game from 1000 to 4000.");
		EnableWorkersKeepPCLDependencyOriginFix = ((BaseUnityPlugin)this).Config.Bind<bool>("Pathfinding", "EnableWorkersKeepPCLDependencyOriginFix", true, "Fixes the behaviour that if the keep is enclosed or otherwise unreachable (A->B style) to a given worker or their workshop destination, they fail to pathfind, even though the worker can just still access their workshop and their storage destination, etc.");
		EnableMountedUnitsRetreatFix = ((BaseUnityPlugin)this).Config.Bind<bool>("Pathfinding", "EnableMountedUnitsRetreatFix", true, "Fixes the bug that causes mounted units in tribes that can retreat (ex: sorties) to become stuck since they can't position themselves onto the keep door.");
		EnableStuckOxGruntsFix = ((BaseUnityPlugin)this).Config.Bind<bool>("Pathfinding", "EnableStuckOxGruntsFix", true, "Introduces a simple reachability check for grunts (and perhaps other workers) for their work targets at no cost.");
		EnableProductionBuildingsDontLoseStorageOnSleep = ((BaseUnityPlugin)this).Config.Bind<bool>("ProductionBuildingsDontLoseStorageOnSleepFix", "Enabled", true, "Fixes some buildings that lose their local storage when put to sleep, such as the Ox tether and the Iron mine.");
		EnableAIDistancedSiegeTents = ((BaseUnityPlugin)this).Config.Bind<bool>("AIBuildDistancedSiegeTents", "Enabled", true, "Force the AI to add gaps between building siege tents.");
		EnableHarassingTentIdleEngineersFix = ((BaseUnityPlugin)this).Config.Bind<bool>("HarassingTentIdleEngineersFix", "Enabled", true, "Attempts to fix the bug that causes engineers to duplicate and become idle when mounting a harassment siege engine tent by delaying");
		HarassingTentIdleEngineersFixDelayAmount = ((BaseUnityPlugin)this).Config.Bind<int>("HarassingTentIdleEngineersFix", "DelayAmount", 1000, "The delay amount (ms) before the engineers get their assigning order to go to the tent.");
		EnableUnitsSpeedReductionOnSelectionFix = ((BaseUnityPlugin)this).Config.Bind<bool>("UnitsSuppressSpeedReductionOnSelectionFix", "Enabled", true, "Fixes the behaviour that units reduce their max speed when selected by suppressing this. Only affects units with special running states, such as maceman, etc.");
		EnableUnstuckUnitsOnAIBuiltWall = ((BaseUnityPlugin)this).Config.Bind<bool>("UnstuckUnitsOnAIBuiltWall", "Enabled", true, "Attempts to unstuck any units when the AI builds a wall over them. These units will be teleported back near their own keep.");
		UnstuckUnitsOnAIBuiltWallOnlySiegeUnits = ((BaseUnityPlugin)this).Config.Bind<bool>("UnstuckUnitsOnAIBuiltWall", "FilterOnlySiegeUnits", false, "Applies filter: Only siege units will be relocated");
		UnstuckUnitsOnAIBuiltWallSiegeUnitReassignmentDelay = ((BaseUnityPlugin)this).Config.Bind<int>("UnstuckUnitsOnAIBuiltWall", "SiegeUnitReassignmentDelay", 5000, "When the engineers will get the re-assignment order to re-man the moved siege engine");
		UnstuckUnitsOnAIBuiltWallSiegeBehaviour = ((BaseUnityPlugin)this).Config.Bind<UnstuckSiegeEngineBehaviour>("UnstuckUnitsOnAIBuiltWall", "BehaviourForSiegeUnits", UnstuckSiegeEngineBehaviour.Move, "What to do with siege engines.");
		EnableProperLowWallCostFix = ((BaseUnityPlugin)this).Config.Bind<bool>("ProperLowWallCostFix", "Enabled", true, "Fixes the stone cost calculation used to represent how many low walls a player can build.");
		EnableLowWallRefundFix = ((BaseUnityPlugin)this).Config.Bind<bool>("EnableLowWallRefundFix", "Enabled", true, "Fixes the stone refund accumulation bug that lets the game struggle to properly refund the stone cost for refunded low-height walls.");
		EnableMissingHorsePurchasingFix = ((BaseUnityPlugin)this).Config.Bind<bool>("EnableMissingHorsePurchasingFix", "Enabled", true, "Fixes a bug where an AI attempting to recruit a knight without an available horse could begin purchasing unrelated weapons or armour.");
		EnableCustomHovelBuildingLogic = ((BaseUnityPlugin)this).Config.Bind<bool>("CustomHovelBuildingLogic", "Enabled", true, "Allows per-AI configured behaviour regarding hovels");
		EnableGlobalOverrideCustomHovelBuildingLogic = ((BaseUnityPlugin)this).Config.Bind<bool>("CustomHovelBuildingLogic", "Force", true, "Applies the global override settings defined here for all AIs");
		CustomHovelBuildingLogicMinimumCivilianHousingSpace = ((BaseUnityPlugin)this).Config.Bind<int>("CustomHovelBuildingLogic", "MinimumCivilianHousingSpace", 12, "The minimum housing space to even consider building more hovels.");
		CustomHovelBuildingLogicRequiredCurrentIdlePeasantCount = ((BaseUnityPlugin)this).Config.Bind<int>("CustomHovelBuildingLogic", "RequiredCurrentIdlePeasantCount", 5, "If there are equal or above this amount of current idle peasants, the ai will not build more hovels.");
		CustomHovelBuildingLogicRequiredAverageIdlePeasantCount = ((BaseUnityPlugin)this).Config.Bind<int>("CustomHovelBuildingLogic", "RequiredAverageIdlePeasantCount", 5, "If there are equal or above this amount of average idle peasants, the ai will not build more hovels.");
		CustomHovelBuildingLogicRequiredMinimumPopularity = ((BaseUnityPlugin)this).Config.Bind<int>("CustomHovelBuildingLogic", "RequiredMinimumPopularity", 5000, "If the AI has less than this amout of popularity(0 - 10000), the ai will not build more hovels.");
		EnablePerLordCustomRequiredDefensiveRecruitmentTickTime = ((BaseUnityPlugin)this).Config.Bind<bool>("EnablePerLordCustomRequiredDefensiveRecruitmentTickTime", "Enabled", true, "Allows per-AI configured defensive recruitment tick time");
		ImplementStrongestAttackPrioritySupport = ((BaseUnityPlugin)this).Config.Bind<bool>("ImplementStrongestAttackPrioritySupport", "Enabled", true, "Implements id 5 to the enum that controls what targets an AI wants to besiege: The Strongest");
		AssignNewTribeOnUnitTypeTrim = ((BaseUnityPlugin)this).Config.Bind<bool>("AssignNewTribeOnUnitTypeTrim", "Enabled", true, "Auto-assigns removed unit types from the active selection a new tribe.");
		RelocateAIVOrderedMapTileIds = ((BaseUnityPlugin)this).Config.Bind<bool>("RelocateAIVOrderedMapTileIds", "Enabled", true, "Relocates and expands the 4000 item buffer that contains tileId specific build step data (Walls, Pitch, Moats, etc).");
		AllowCustomMinGoldForHarassmentSiegeEngines = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowCustomMinGoldForHarassmentSiegeEngines", "Enabled", true, "Allows AI lords to supply their own min gold for harassment siege engines.");
		EnableDisableAllWildlifeOption = ((BaseUnityPlugin)this).Config.Bind<bool>("EnableDisableAllWildlifeOption", "Enabled", true, "Enables this option to work in the first place to kill all wildlife on map start.");
		EnableTunnelersDontDamageAlliedStuff = ((BaseUnityPlugin)this).Config.Bind<bool>("EnableTunnelersDontDamageAlliedStuff", "Enabled", true, "This fix prevents tunnelers from damaging own or allied objects.");
		EnableStair6SlowdownFix = ((BaseUnityPlugin)this).Config.Bind<bool>("EnableStair6SlowdownFix", "Enabled", true, "This fix prevents Stair6's placed by the AIV builder to slow it down per stair placed.");
		EnableArabBallistaOffsetFix = ((BaseUnityPlugin)this).Config.Bind<bool>("EnableArabBallistaOffsetFix", "Enabled", true, "This fixes the arab ballista sprite offset (x+1, y+1)");
		AllowLordMovementFreedomPatch = ((BaseUnityPlugin)this).Config.Bind<bool>("Pathfinding", "AllowLordMovementFreedomPatch", true, "Allows the gameplay option that unrestricts the lord movement in the first place");
		EnableWorkersStuckInMoatFix = ((BaseUnityPlugin)this).Config.Bind<bool>("EnableWorkersStuckInMoatFix", "Enabled", true, "This fixes workers that get otherwise stuck in moats, when exiting their working place for example, by simply despawning them.");
		AllowDeletionOfMoatsWithUnitsInsidePatch = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowDeletionOfMoatsWithUnitsInsidePatch", "Enabled", true, "Makes it possible to delete moats that contain units inside of it (stuck or otherwise)");
		AllowCustomAIPurchasingRoutines = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowCustomAIPurchasingRoutines", "Enabled", true, "Makes it possible for AIs to have custom purchasing behaviours. Allowing things such as BuyIronAt, etc");
		AllowCustomGoodBadRequirements = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowCustomGoodBadRequirements", "Enabled", true, "Makes it possible for AIs to have custom requirements for good/bad things.");
		AllowCustomSiegeLifetime = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowCustomSiegeLifetime", "Enabled", true, "Makes it possible for AIs to have custom siege lifetime.");
		AllowCustomAIControl = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowCustomAIControl", "Enabled", true, "Makes it possible for AIs to have custom AI controls in terms of disabled AI or disabled siege AI.");
		SmarterSiegeAssassins = ((BaseUnityPlugin)this).Config.Bind<bool>("SmarterSiegeAssassins", "Enabled", true, "Lets assassins during sieges capture more than one gatehouse.");
		SmarterSiegeLaddermen = ((BaseUnityPlugin)this).Config.Bind<bool>("SmarterSiegeLaddermen", "Enabled", true, "Lets laddermen during sieges deploy their ladders preferrably on walls that grant better access to the keep.");
		AllowCustomTunnelerDigRange = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowCustomTunnelerDigRange", "Enabled", true, "Makes it possible for AIs to have custom dig ranges for tunnelers.");
		AllowSiegeAssassinsIgnoreNonSiegeTargets = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowSiegeAssassinsIgnoreNonSiegeTargets", "Enabled", true, "Makes it possible for AIs to let their siege assassins ignore non active siege targets.");
		FixPlacementSelectionNotAccountingForFarmland = ((BaseUnityPlugin)this).Config.Bind<bool>("FixPlacementSelectionNotAccountingForFarmland", "Enabled", true, "Fixes the bug where building placement selectors (for AI mostly) failed to recognize that their destined target is occupied by farmland (such as orchards, etc) and be stuck in a loop attempting to build something there.");
	}
}
