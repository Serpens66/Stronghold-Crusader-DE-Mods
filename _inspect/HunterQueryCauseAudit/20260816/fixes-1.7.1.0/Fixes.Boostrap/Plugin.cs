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
using SHCDESE.API;
using SHCDESE.API.Logging;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.GameGlobals;
using Serilog.Extensions.Logging;
using Zhuqiaomon.Assembly.Stateful;

namespace Fixes.Boostrap;

[BepInDependency(/*Could not decode attribute arguments.*/)]
[BepInPlugin("fixes", "Fixes", "1.7.1.0")]
public class Plugin : BaseUnityPlugin
{
	public enum UnstuckSiegeEngineBehaviour
	{
		Move,
		Delete,
		DeleteWithEngineers
	}

	public const string PLUGIN_GUID = "fixes";

	public const string PLUGIN_NAME = "Fixes";

	public const string PLUGIN_VERSION = "1.7.1.0";

	private const string SHCDESE_GUID = "000shcdese";

	public static Plugin Instance;

	private ModLogHelper Log;

	public ILoggerFactory LoggerFactory;

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

	public Dictionary<string, CustomLordPreferencesEntry> CustomLordPreferences { get; private set; }

	public Dictionary<AILords, bool>? HopsFarmWhitelist { get; private set; }

	public LobbySettingsViewModel LobbySettingsViewModel { get; private set; }

	private Plugin()
	{
		Instance = this;
		CustomLordPreferences = new Dictionary<string, CustomLordPreferencesEntry>();
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
		if (EnableCustomHovelBuildingLogic.Value)
		{
			ObservableSubscribeExtensions.Subscribe<AIQueryBuildHovelEventArgs>(AIR3EventHooks.OnAIQueryBuildHovelEventArgs.Observable, (Action<AIQueryBuildHovelEventArgs>)FixesAIEvents.OnAIShouldBuildHovel);
		}
		ObservableSubscribeExtensions.Subscribe<MapStartEventArgs>(MapLoaderR3EventHooks.OnStartMap.Observable, (Action<MapStartEventArgs>)FixesMapEvents.OnStartMap);
		LoadExternalConfigs();
		OverrideGlobals();
		Log.Information("Plugin Fixes is loaded!", "Awake", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Bootstrap\\Plugin.cs");
	}

	private unsafe void OverrideGlobals()
	{
		Log.Information("Overriding globals...", "OverrideGlobals", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Bootstrap\\Plugin.cs");
		((AssemblyGetSet<ushort>)(object)GameGlobalsManager.Instance.PathfindingMaxTilesConstraint)?.SetValue(AIPathfindingMaxDistance.Value);
		if (EnableWheatSaleCategoryFix.Value)
		{
			int* ptr = (int*)(GameGlobalsManager.Instance.AIResourceSellCategoryTableVA + 40);
			Log.Information($"Old Wheat Sale Category Value: {*ptr}", "OverrideGlobals", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Bootstrap\\Plugin.cs");
			*ptr = 6;
			Log.Information($"New Wheat Sale Category Value: {*ptr}", "OverrideGlobals", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Bootstrap\\Plugin.cs");
		}
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

	private void CrusaderLibrary_LibraryLoaded(IntPtr libraryHandle, System.ReadOnlySpan<byte> memory)
	{
		try
		{
			DetourManager.Instance.ApplyNative(libraryHandle, memory);
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
		EnableWheatSaleCategoryFix = ((BaseUnityPlugin)this).Config.Bind<bool>("EnableWheatSaleCategoryFix", "Enabled", true, "Enables the fix for a bug in the AIs market-sell logic where raw wheat was incorrectly categorized");
		ModularGoodsyardPlacement = ((BaseUnityPlugin)this).Config.Bind<bool>("ModularGoodsyardPlacement", "Enabled", true, "Enables the per-player toggle for goodsyard placement");
		AllowKeep3ForAIVs = ((BaseUnityPlugin)this).Config.Bind<bool>("AllowKeep3ForAIVs", "Enabled", true, "Enable KEEP3 usage for AIVs");
		EnableGatehouseFarmerFix = ((BaseUnityPlugin)this).Config.Bind<bool>("GatehouseFarmerFix", "Enabled", true, "Enables fix for wheat and cattle farmers that get stuck on gatehouses that automatically closed");
		EnableAISelectSiegeRallypointOverrides = ((BaseUnityPlugin)this).Config.Bind<bool>("AISelectSiegeRallypointOverrides", "Enabled", false, "Enables special overrides that will be active for all ais in the game in relation to sieges");
		AISelectSiegeRallypointMaxSearchRadius = ((BaseUnityPlugin)this).Config.Bind<int>("AISelectSiegeRallypointOverrides", "MaxSearchRadius", 110, "Limit for preferred standoff distance");
		AISelectSiegeRallypointPreferredStandoffDistance = ((BaseUnityPlugin)this).Config.Bind<int>("AISelectSiegeRallypointOverrides", "PreferredStandoffDistance", 90, "Preferred distance from besieged castle");
		AIPathfindingMaxDistance = ((BaseUnityPlugin)this).Config.Bind<ushort>("Pathfinding", "MaxTilesDistance", (ushort)2000, "The maximum amount of tiles the game will query before giving up. Warn: Do not mess with this too much, it can and will impact performance alot.");
		EnableAIDistancedSiegeTents = ((BaseUnityPlugin)this).Config.Bind<bool>("AIBuildDistancedSiegeTents", "Enabled", true, "Force the AI to add gaps between building siege tents.");
		EnableHarassingTentIdleEngineersFix = ((BaseUnityPlugin)this).Config.Bind<bool>("HarassingTentIdleEngineersFix", "Enabled", true, "Attempts to fix the bug that causes engineers to duplicate and become idle when mounting a harassment siege engine tent by delaying");
		HarassingTentIdleEngineersFixDelayAmount = ((BaseUnityPlugin)this).Config.Bind<int>("HarassingTentIdleEngineersFix", "DelayAmount", 1000, "The delay amount (ms) before the engineers get their assigning order to go to the tent.");
		EnableUnstuckUnitsOnAIBuiltWall = ((BaseUnityPlugin)this).Config.Bind<bool>("UnstuckUnitsOnAIBuiltWall", "Enabled", true, "Attempts to unstuck any units when the AI builds a wall over them. These units will be teleported back near their own keep.");
		UnstuckUnitsOnAIBuiltWallOnlySiegeUnits = ((BaseUnityPlugin)this).Config.Bind<bool>("UnstuckUnitsOnAIBuiltWall", "FilterOnlySiegeUnits", false, "Applies filter: Only siege units will be relocated");
		UnstuckUnitsOnAIBuiltWallSiegeUnitReassignmentDelay = ((BaseUnityPlugin)this).Config.Bind<int>("UnstuckUnitsOnAIBuiltWall", "SiegeUnitReassignmentDelay", 5000, "When the engineers will get the re-assignment order to re-man the moved siege engine");
		UnstuckUnitsOnAIBuiltWallSiegeBehaviour = ((BaseUnityPlugin)this).Config.Bind<UnstuckSiegeEngineBehaviour>("UnstuckUnitsOnAIBuiltWall", "BehaviourForSiegeUnits", UnstuckSiegeEngineBehaviour.Move, "What to do with siege engines.");
		EnableProperLowWallCostFix = ((BaseUnityPlugin)this).Config.Bind<bool>("ProperLowWallCostFix", "Enabled", false, "Fixes the stone cost calculation used to represent how many low walls a player can build.");
		EnableCustomHovelBuildingLogic = ((BaseUnityPlugin)this).Config.Bind<bool>("CustomHovelBuildingLogic", "Enabled", true, "Allows pre-AI configured behaviour regarding hovels");
		EnableGlobalOverrideCustomHovelBuildingLogic = ((BaseUnityPlugin)this).Config.Bind<bool>("CustomHovelBuildingLogic", "Force", true, "Applies the global override settings defined here for all AIs");
		CustomHovelBuildingLogicMinimumCivilianHousingSpace = ((BaseUnityPlugin)this).Config.Bind<int>("CustomHovelBuildingLogic", "MinimumCivilianHousingSpace", 12, "The minimum housing space to even consider building more hovels.");
		CustomHovelBuildingLogicRequiredCurrentIdlePeasantCount = ((BaseUnityPlugin)this).Config.Bind<int>("CustomHovelBuildingLogic", "RequiredCurrentIdlePeasantCount", 5, "If there are equal or above this amount of current idle peasants, the ai will not build more hovels.");
		CustomHovelBuildingLogicRequiredAverageIdlePeasantCount = ((BaseUnityPlugin)this).Config.Bind<int>("CustomHovelBuildingLogic", "RequiredAverageIdlePeasantCount", 5, "If there are equal or above this amount of average idle peasants, the ai will not build more hovels.");
		CustomHovelBuildingLogicRequiredMinimumPopularity = ((BaseUnityPlugin)this).Config.Bind<int>("CustomHovelBuildingLogic", "RequiredMinimumPopularity", 5000, "If the AI has less than this amout of popularity(0 - 10000), the ai will not build more hovels.");
	}
}
