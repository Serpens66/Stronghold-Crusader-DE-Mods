using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fixes.Boostrap;
using SHCDESE.API;
using SHCDESE.API.Logging;
using SHCDESE.Interop;

namespace Fixes.Config;

internal static class Preferences
{
	private const string Path = "override/fixes/preferences.json";

	private static readonly ModLogHelper Log = ModLoggerFactory.CreateHelper("Preferences");

	private static CustomLordPreferencesEntry?[] _slots = new CustomLordPreferencesEntry[9];

	private static readonly Dictionary<int, CustomLordPreferencesEntry> ExtendedLords = new Dictionary<int, CustomLordPreferencesEntry>();

	private static readonly int AICCount = Enum.GetValues(typeof(AILords)).Length;

	internal static void Clear()
	{
		_slots = new CustomLordPreferencesEntry[9];
	}

	internal static void SetExtendedLord(int lordType, CustomLordPreferencesEntry preferences)
	{
		ExtendedLords[lordType + 1] = preferences;
	}

	internal static bool TryGet(int playerId, out CustomLordPreferencesEntry preferences)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (playerId >= 1 && playerId <= 8)
		{
			CustomLordPreferencesEntry customLordPreferencesEntry = _slots[playerId];
			if (customLordPreferencesEntry != null)
			{
				preferences = customLordPreferencesEntry;
				return true;
			}
		}
		GameAIManagerAPI instance = GameAIManagerAPI.Instance;
		AILords aILord = GamePlayerManagerAPI.Instance.GetAILord(playerId);
		if (instance.IsCustomLord(aILord) && Plugin.Instance.CustomLordPreferences.TryGetValue(instance.GetCustomAILordNameByPlayerId(playerId), out CustomLordPreferencesEntry value))
		{
			preferences = value;
			return true;
		}
		if (TryGetBaseLordPreferences(playerId, out CustomLordPreferencesEntry preferences2))
		{
			preferences = preferences2;
			return true;
		}
		preferences = null;
		return false;
	}

	private unsafe static AILords GetBaseLord(int playerId)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		GamePlayerResources* ptr = default(GamePlayerResources*);
		if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId, ref ptr) && ((GamePlayerResources)ptr).r_AILordMinusOne != 0)
		{
			int num = (int)(((GamePlayerResources)ptr).r_AILordMinusOne - 1);
			if ((uint)num < AICCount)
			{
				InternalAIC* valuePointer = GameAIManagerAPI.Instance.GetAICArray().GetValuePointer(num);
				if (valuePointer != null && ((InternalAIC)valuePointer).opponent_type > 0 && ((InternalAIC)valuePointer).opponent_type < 30)
				{
					return (AILords)((InternalAIC)valuePointer).opponent_type;
				}
				return (AILords)0;
			}
			return (AILords)0;
		}
		return (AILords)0;
	}

	private unsafe static bool TryGetBaseLordPreferences(int playerId, out CustomLordPreferencesEntry preferences)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected I4, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected I4, but got Unknown
		AILords baseLord = GetBaseLord(playerId);
		if ((int)baseLord != 0)
		{
			string key = ((object)(*(AILords*)(&baseLord))/*cast due to constrained. prefix*/).ToString().Substring(3);
			if (Plugin.Instance.CustomLordPreferences.TryGetValue(key, out CustomLordPreferencesEntry value))
			{
				preferences = value;
				return true;
			}
			if (ExtendedLords.TryGetValue((int)baseLord, out CustomLordPreferencesEntry value2))
			{
				preferences = value2;
				return true;
			}
		}
		if (ExtendedLords.TryGetValue((int)GamePlayerManagerAPI.Instance.GetAILord(playerId), out CustomLordPreferencesEntry value3))
		{
			preferences = value3;
			return true;
		}
		preferences = null;
		return false;
	}

	internal unsafe static void Load()
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		Clear();
		try
		{
			string text = GameMapArchiveManagerAPI.Instance.TryReadTextFile("override/fixes/preferences.json", true);
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}
			JsonNode obj = JsonNode.Parse(text, (JsonNodeOptions?)null, default(JsonDocumentOptions));
			JsonObject val = (JsonObject)(object)((obj is JsonObject) ? obj : null);
			bool flag = val == null;
			if (!flag)
			{
				JsonNode val2 = ((JsonNode)val)["byName"];
				bool flag2 = ((val2 == null || val2 is JsonObject) ? true : false);
				flag = !flag2;
			}
			bool flag3 = flag;
			if (!flag3)
			{
				JsonNode val2 = ((JsonNode)val)["bySlot"];
				bool flag2 = ((val2 == null || val2 is JsonObject) ? true : false);
				flag3 = !flag2;
			}
			if (flag3)
			{
				Log.Error("Invalid override/fixes/preferences.json: byName and bySlot must be objects", "Load", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Config\\Preferences.cs");
				return;
			}
			JsonNode obj2 = ((JsonNode)val)["byName"];
			JsonObject val3 = (JsonObject)(object)((obj2 is JsonObject) ? obj2 : null);
			JsonNode obj3 = ((JsonNode)val)["bySlot"];
			JsonObject val4 = (JsonObject)(object)((obj3 is JsonObject) ? obj3 : null);
			CustomLordPreferencesEntry[] array = new CustomLordPreferencesEntry[9];
			GameAIManagerAPI instance = GameAIManagerAPI.Instance;
			GamePlayerManagerAPI instance2 = GamePlayerManagerAPI.Instance;
			for (int i = 1; i <= 8; i++)
			{
				AILords aILord = instance2.GetAILord(i);
				if ((int)aILord == 0)
				{
					continue;
				}
				string customAILordNameByPlayerId = instance.GetCustomAILordNameByPlayerId(i);
				AILords baseLord = GetBaseLord(i);
				string text2 = (((int)baseLord != 0) ? ((object)(*(AILords*)(&baseLord))/*cast due to constrained. prefix*/).ToString().Substring(3) : string.Empty);
				string text3 = ((object)(*(AILords*)(&aILord))/*cast due to constrained. prefix*/).ToString();
				JsonNode val5 = null;
				if (val3 != null)
				{
					foreach (KeyValuePair<string, JsonNode> item in val3)
					{
						if (string.Equals(item.Key, customAILordNameByPlayerId, StringComparison.OrdinalIgnoreCase) || string.Equals(item.Key, text3, StringComparison.OrdinalIgnoreCase) || (text3.StartsWith("SK_", StringComparison.Ordinal) && string.Equals(item.Key, text3.Substring(3), StringComparison.OrdinalIgnoreCase)))
						{
							val5 = item.Value;
							break;
						}
					}
				}
				if (val5 == null && val3 != null && !string.IsNullOrEmpty(text2))
				{
					foreach (KeyValuePair<string, JsonNode> item2 in val3)
					{
						if (string.Equals(item2.Key, text2, StringComparison.OrdinalIgnoreCase) || string.Equals(item2.Key, "SK_" + text2, StringComparison.OrdinalIgnoreCase))
						{
							val5 = item2.Value;
							break;
						}
					}
				}
				JsonNode val6 = ((val4 != null) ? ((JsonNode)val4)[i.ToString()] : null);
				if (val5 != null || val6 != null)
				{
					if ((val5 != null && !(val5 is JsonObject)) || (val6 != null && !(val6 is JsonObject)))
					{
						Log.Error(string.Format("Invalid {0}: player {1} preferences must be objects", "override/fixes/preferences.json", i), "Load", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Config\\Preferences.cs");
						return;
					}
					CustomLordPreferencesEntry value;
					CustomLordPreferencesEntry preferences;
					JsonObject val7 = JsonSerializer.SerializeToNode<CustomLordPreferencesEntry>(Plugin.Instance.CustomLordPreferences.TryGetValue(customAILordNameByPlayerId, out value) ? value : (TryGetBaseLordPreferences(i, out preferences) ? preferences : new CustomLordPreferencesEntry()), (JsonSerializerOptions)null).AsObject();
					JsonObject val8 = (JsonObject)(object)((val5 is JsonObject) ? val5 : null);
					if (val8 != null)
					{
						Merge(val7, val8);
					}
					JsonObject val9 = (JsonObject)(object)((val6 is JsonObject) ? val6 : null);
					if (val9 != null)
					{
						Merge(val7, val9);
					}
					array[i] = JsonSerializer.Deserialize<CustomLordPreferencesEntry>((JsonNode)(object)val7, (JsonSerializerOptions)null);
				}
			}
			_slots = array;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Invalid override/fixes/preferences.json; map preferences were ignored", "Load", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-fixes\\src\\shcde-fixes\\Config\\Preferences.cs");
		}
	}

	private static void Merge(JsonObject target, JsonObject source)
	{
		foreach (KeyValuePair<string, JsonNode> item in source)
		{
			string key = item.Key;
			JsonNode value = item.Value;
			((JsonNode)target)[key] = ((value != null) ? value.DeepClone() : null);
		}
	}
}
