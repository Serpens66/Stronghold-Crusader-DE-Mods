using System;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.API.Logging;
using SHCDESE.GameGlobals;
using SHCDESE.ViewModels;
using Zhuqiaomon.Assembly.Stateful;

namespace Fixes.UI;

public class LobbySettingsViewModel : LobbyModSettingsBaseViewModel
{
	private static readonly Lazy<ModLogHelper> _log = new Lazy<ModLogHelper>(() => ModLoggerFactory.CreateHelper("Fixes"));

	private bool disableAllAnimals;

	private int peaceTime;

	private static ModLogHelper Log => _log.Value;

	[SyncPerPlayer]
	public bool PlaceGoodsyard
	{
		get
		{
			int num = Math.Max(1, GameNetworkAPI.GetLocalPlayerId());
			return PlaceGoodsyardData[num];
		}
		set
		{
			int num = Math.Max(1, GameNetworkAPI.GetLocalPlayerId());
			PlaceGoodsyardData[num] = value;
			((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("PlaceGoodsyard");
		}
	}

	public bool[] PlaceGoodsyardData { get; } = new bool[9] { true, true, true, true, true, true, true, true, true };

	[SyncHostOnly]
	public bool DisableAllAnimals
	{
		get
		{
			return disableAllAnimals;
		}
		set
		{
			disableAllAnimals = value;
			((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("DisableAllAnimals");
		}
	}

	[SyncHostOnly]
	public string AIPeaceTime
	{
		get
		{
			return peaceTime.ToString();
		}
		set
		{
			if (int.TryParse(value, out var result))
			{
				peaceTime = result;
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("AIPeaceTime");
			}
		}
	}

	public bool AIMaxOxTethersUnavailable => GameGlobalsManager.Instance.AIMaxOxTethers == null;

	[SyncHostOnly]
	public string AIMaxOxTethers
	{
		get
		{
			return (((AssemblyGetSet<ushort>)(object)GameGlobalsManager.Instance.AIMaxOxTethers)?.GetValue())?.ToString() ?? "0";
		}
		set
		{
			if (ushort.TryParse(value, out var result))
			{
				((AssemblyGetSet<ushort>)(object)GameGlobalsManager.Instance.AIMaxOxTethers)?.SetValue(result);
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("AIMaxOxTethers");
			}
		}
	}

	public bool AIStoneToOxenRatioUnavailable => GameGlobalsManager.Instance.AIStoneToOxenRatio == null;

	[SyncHostOnly]
	public string AIStoneToOxenRatio
	{
		get
		{
			return (((AssemblyGetSet<ushort>)(object)GameGlobalsManager.Instance.AIStoneToOxenRatio)?.GetValue())?.ToString() ?? "0";
		}
		set
		{
			if (ushort.TryParse(value, out var result))
			{
				((AssemblyGetSet<ushort>)(object)GameGlobalsManager.Instance.AIStoneToOxenRatio)?.SetValue(result);
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("AIStoneToOxenRatio");
			}
		}
	}

	public bool AIGoldThresholdForHarassmentSiegeEnginesUnavailable => GameGlobalsManager.Instance.AIGoldThresholdForHarassmentSiegeEngines == null;

	[SyncHostOnly]
	public string AIGoldThresholdForHarassmentSiegeEngines
	{
		get
		{
			return (((AssemblyGetSet<ushort>)(object)GameGlobalsManager.Instance.AIGoldThresholdForHarassmentSiegeEngines)?.GetValue())?.ToString() ?? "0";
		}
		set
		{
			if (ushort.TryParse(value, out var result))
			{
				((AssemblyGetSet<ushort>)(object)GameGlobalsManager.Instance.AIGoldThresholdForHarassmentSiegeEngines)?.SetValue(result);
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("AIGoldThresholdForHarassmentSiegeEngines");
			}
		}
	}
}
