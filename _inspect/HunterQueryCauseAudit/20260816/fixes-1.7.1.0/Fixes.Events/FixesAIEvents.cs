using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Fixes.Boostrap;
using Fixes.Config;
using SHCDESE.API;
using SHCDESE.API.Logging;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Interop.Query;
using SHCDESE.Logging;

namespace Fixes.Events;

internal class FixesAIEvents
{
	private static readonly Lazy<ModLogHelper> _log = new Lazy<ModLogHelper>(() => ModLoggerFactory.CreateHelper("Fixes"));

	private static ModLogHelper Log => _log.Value;

	internal unsafe static void OnAIShouldBuildHovel(AIQueryBuildHovelEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		if ((int)e.Mappers != 54)
		{
			((EventHookBase)e).SkipOriginalFunction = false;
			return;
		}
		((EventHookBase)e).SkipOriginalFunction = true;
		GamePlayerResources* ptr = default(GamePlayerResources*);
		if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(e.PlayerId, ref ptr))
		{
			LogHelper.Warning($"Could not find playerresource by id: {e.PlayerId}", "OnAIShouldBuildHovel", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Events\\FixesAIEvents.cs");
			return;
		}
		Plugin instance = Plugin.Instance;
		GameAIManagerAPI instance2 = GameAIManagerAPI.Instance;
		GamePlayerManagerAPI instance3 = GamePlayerManagerAPI.Instance;
		int value = instance.CustomHovelBuildingLogicMinimumCivilianHousingSpace.Value;
		int value2 = instance.CustomHovelBuildingLogicRequiredCurrentIdlePeasantCount.Value;
		int value3 = instance.CustomHovelBuildingLogicRequiredAverageIdlePeasantCount.Value;
		int value4 = instance.CustomHovelBuildingLogicRequiredMinimumPopularity.Value;
		AILords aILord = instance3.GetAILord(e.PlayerId);
		if (instance2.IsCustomLord(aILord))
		{
			string customAILordNameByPlayerId = instance2.GetCustomAILordNameByPlayerId(e.PlayerId);
			if (instance.CustomLordPreferences.TryGetValue(customAILordNameByPlayerId, out CustomLordPreferencesEntry value5))
			{
				if (value5.HovelBuildingLogicDontBuildWhenAboveHousingSpace.HasValue)
				{
					value = value5.HovelBuildingLogicDontBuildWhenAboveHousingSpace.Value;
				}
				if (value5.HovelBuildingLogicDontBuildWhenAboveOrEqualToCurrentIdlePeasants.HasValue)
				{
					value2 = value5.HovelBuildingLogicDontBuildWhenAboveOrEqualToCurrentIdlePeasants.Value;
				}
				if (value5.HovelBuildingLogicDontBuildWhenAboveOrEqualToAverageIdlePeasants.HasValue)
				{
					value3 = value5.HovelBuildingLogicDontBuildWhenAboveOrEqualToAverageIdlePeasants.Value;
				}
				if (value5.HovelBuildingLogicDontBuildWhenBelowPopularity.HasValue)
				{
					value4 = value5.HovelBuildingLogicDontBuildWhenBelowPopularity.Value;
				}
			}
		}
		if (instance.EnableGlobalOverrideCustomHovelBuildingLogic.Value)
		{
			int r_CivilianHousingSpace = (int)((GamePlayerResources)ptr).r_CivilianHousingSpace;
			if (r_CivilianHousingSpace > value && (((GamePlayerResources)ptr).r_TotalPopulation < r_CivilianHousingSpace || ((GamePlayerResources)ptr).r_IdlePeasantCurrent >= value2 || ((GamePlayerResources)ptr).r_IdlePeasantAverage >= value3 || ((GamePlayerResources)ptr).r_CurrentPopularity < value4))
			{
				e.ReturnValue = true;
			}
		}
	}

	internal static void OnAISelectSiegeRallypoint(AISelectSiegeRallypointEventArgs e)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		Plugin instance = Plugin.Instance;
		GameAIManagerAPI instance2 = GameAIManagerAPI.Instance;
		AILords aILord = GamePlayerManagerAPI.Instance.GetAILord(e.PlayerId);
		if (instance2.IsCustomLord(aILord))
		{
			string customAILordNameByPlayerId = instance2.GetCustomAILordNameByPlayerId(e.PlayerId);
			if (instance.CustomLordPreferences.TryGetValue(customAILordNameByPlayerId, out CustomLordPreferencesEntry value))
			{
				if (value.SiegeSelectionMaxSearchRange.HasValue)
				{
					e.MaxSearchRange = value.SiegeSelectionMaxSearchRange.Value;
				}
				if (value.SiegeSelectionPreferredStandoffDistance.HasValue)
				{
					e.PreferredStandoffDistance = value.SiegeSelectionPreferredStandoffDistance.Value;
				}
			}
		}
		if (instance.EnableAISelectSiegeRallypointOverrides.Value)
		{
			e.MaxSearchRange = instance.AISelectSiegeRallypointMaxSearchRadius.Value;
			e.PreferredStandoffDistance = instance.AISelectSiegeRallypointPreferredStandoffDistance.Value;
		}
	}

	internal static void OnAIBuildWall(AIBuildWallEventArgs e)
	{
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		GameTileManagerAPI instance = GameTileManagerAPI.Instance;
		GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
		GamePlayerManagerAPI instance2 = GamePlayerManagerAPI.Instance;
		int tileId = instance.GetTileId(e.TileX, e.TileY);
		if (instance.GetTileUnitId(tileId) == 0 || instance.HasTilePropertyFlag(tileId, (TilePropertyFlag)256))
		{
			return;
		}
		Dictionary<int, UnmanagedVector2<ushort>> safePositions = BuildSafePositionMap(instance2, instance);
		bool filterSiege = Plugin.Instance.UnstuckUnitsOnAIBuiltWallOnlySiegeUnits.Value;
		GameStructQuery<GameUnit> val = unitApi.QueryUnits().Where(UnitPredicates.IsAlive).Where(UnitPredicates.IsWithinRect(e.TileX, e.TileY, 1, 1));
		if (filterSiege)
		{
			eChimps[] array = new eChimps[5];
			RuntimeHelpers.InitializeArray(array, (RuntimeFieldHandle)/*OpCode not supported: LdMemberToken*/);
			val = val.Where(UnitPredicates.IsOfAnyType((eChimps[])(object)array));
		}
		val.ForEach((RefAction<GameUnit>)delegate(in GameUnit unit, int unitIndex)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			int num = unitIndex + 1;
			UnmanagedVector2<ushort> val2 = safePositions[unit.r_ControllableForPlayerId];
			bool flag = unitApi.IsSiegeEngineManned(num);
			if (!filterSiege && !flag)
			{
				unitApi.SetCurrentLocalTilePosition(num, val2);
			}
			else
			{
				HandleStuckSiegeUnit(unitApi, in unit, num, val2);
			}
		});
	}

	private unsafe static Dictionary<int, UnmanagedVector2<ushort>> BuildSafePositionMap(GamePlayerManagerAPI playerApi, GameTileManagerAPI tileApi)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<int, UnmanagedVector2<ushort>> dictionary = new Dictionary<int, UnmanagedVector2<ushort>>();
		GamePlayerResources* ptr = default(GamePlayerResources*);
		for (int i = 1; i <= 8; i++)
		{
			if (playerApi.TryGetPlayerResourcesById(i, ref ptr))
			{
				int r_KeepDoorTilePositionX = (int)((GamePlayerResources)ptr).r_KeepDoorTilePositionX;
				int r_KeepDoorTilePositionY = (int)((GamePlayerResources)ptr).r_KeepDoorTilePositionY;
				if (r_KeepDoorTilePositionX != 0 || r_KeepDoorTilePositionY != 0)
				{
					dictionary.Add(i, tileApi.GetNearestUnoccupiedTile(r_KeepDoorTilePositionX, r_KeepDoorTilePositionY, 15));
				}
			}
		}
		dictionary.Add(0, tileApi.GetNearestUnoccupiedTile(400, 400, 15));
		return dictionary;
	}

	private static void HandleStuckSiegeUnit(GameUnitManagerAPI unitApi, in GameUnit unit, int unitId, UnmanagedVector2<ushort> safePos)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		switch (Plugin.Instance.UnstuckUnitsOnAIBuiltWallSiegeBehaviour.Value)
		{
		case Plugin.UnstuckSiegeEngineBehaviour.Move:
			MoveSiegeUnit(unitApi, in unit, unitId, safePos);
			break;
		case Plugin.UnstuckSiegeEngineBehaviour.Delete:
			unitApi.DeleteUnitSafe(unitId);
			break;
		case Plugin.UnstuckSiegeEngineBehaviour.DeleteWithEngineers:
			unitApi.DeleteUnitSafe(unitId);
			DeleteEngineers(unitApi, in unit);
			break;
		}
	}

	private unsafe static void MoveSiegeUnit(GameUnitManagerAPI unitApi, in GameUnit unit, int unitId, UnmanagedVector2<ushort> safePos)
	{
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		int r_ControllableForPlayerId = unit.r_ControllableForPlayerId;
		(ushort, uint)[] obj = new(ushort, uint)[4]
		{
			(unit.r_AssignedEngineer1, unit.r_AssignedEngineer1GlobalId),
			(unit.r_AssignedEngineer2, unit.r_AssignedEngineer2GlobalId),
			(unit.r_AssignedEngineer3, unit.r_AssignedEngineer3GlobalId),
			(unit.r_AssignedEngineer4, unit.r_AssignedEngineer4GlobalId)
		};
		unitApi.DeleteUnitSafe(unitId);
		int newSiegeUnitId = (int)unitApi.CreateUnitLocal(r_ControllableForPlayerId, r_ControllableForPlayerId, (int)safePos.X, (int)safePos.Y, 8, unit.r_UnitChimp);
		GameTribeManagerAPI instance = GameTribeManagerAPI.Instance;
		int newTribeId = (int)instance.Create(r_ControllableForPlayerId, false);
		(ushort, uint)[] array = obj;
		GameUnit* ptr = default(GameUnit*);
		for (int i = 0; i < array.Length; i++)
		{
			ushort item = array[i].Item1;
			if (item != 0 && unitApi.TryGetUnitById((int)item, ref ptr))
			{
				instance.AssignUnit(newTribeId, (int)item);
				unitApi.SetCurrentLocalTilePosition((int)item, safePos);
			}
		}
		int value = Plugin.Instance.UnstuckUnitsOnAIBuiltWallSiegeUnitReassignmentDelay.Value;
		GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(value, (Action)delegate
		{
			GameTribeManagerAPI.Instance.ManSiegeEquipment(newTribeId, newSiegeUnitId);
		}, string.Empty);
	}

	private unsafe static void DeleteEngineers(GameUnitManagerAPI unitApi, in GameUnit unit)
	{
		ushort[] array = new ushort[4] { unit.r_AssignedEngineer1, unit.r_AssignedEngineer2, unit.r_AssignedEngineer3, unit.r_AssignedEngineer4 };
		GameUnit* ptr = default(GameUnit*);
		foreach (ushort num in array)
		{
			if (num != 0 && unitApi.TryGetUnitById((int)num, ref ptr))
			{
				unitApi.DeleteUnitSafe((int)num);
			}
		}
	}

	internal static void OnProcessCustomLord(AIProcessCustomLordEventArgs e)
	{
		string lordName = e.CustomLord.lordName;
		Log.Information("Processing custom AI lord: " + lordName, "OnProcessCustomLord", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Events\\FixesAIEvents.cs");
		string text = default(string);
		if (!GameAssetManagerAPI.Instance.GetModifiedFilePath("fixes/preferences.json", ref text))
		{
			Log.Warning("Preference file not found for [" + lordName + "]", "OnProcessCustomLord", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Events\\FixesAIEvents.cs");
			return;
		}
		Log.Information("Found preference file: [" + text + "]", "OnProcessCustomLord", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Events\\FixesAIEvents.cs");
		CustomLordPreferencesEntry customLordPreferencesEntry = JsonSerializer.Deserialize<CustomLordPreferencesEntry>(File.ReadAllText(text), (JsonSerializerOptions)null);
		if (customLordPreferencesEntry == null)
		{
			Log.Error("Failed to parse configuration for [" + lordName + "]", "OnProcessCustomLord", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Events\\FixesAIEvents.cs");
		}
		else if (!Plugin.Instance.CustomLordPreferences.ContainsKey(lordName))
		{
			Plugin.Instance.CustomLordPreferences.Add(lordName, customLordPreferencesEntry);
		}
		else
		{
			Log.Warning("Lord [" + lordName + "] was already added to preferences dictionary", "OnProcessCustomLord", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Events\\FixesAIEvents.cs");
		}
	}
}
