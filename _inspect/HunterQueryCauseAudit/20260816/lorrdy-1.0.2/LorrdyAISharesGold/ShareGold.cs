using System;
using SHCDESE.API;
using SHCDESE.API.Components.Timer;
using SHCDESE.API.Logging;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Lua;

namespace LorrdyAISharesGold;

internal static class ShareGold
{
	private static readonly Lazy<ModLogHelper> _log = new Lazy<ModLogHelper>(() => ModLoggerFactory.CreateHelper("LorrdyAIShareGold"));

	private const string CALLBACK_NAME = "LorrdyAIShareGold_Timer";

	private static ModLogHelper Log => _log.Value;

	internal static void OnStartMap(MapStartEventArgs args)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		if ((int)((EventHookBase)args).Phase != 0)
		{
			Log.Information("Map started, starting timer", "OnStartMap", "C:\\Documents\\Code\\StrongholdCrusaderDEMods\\AISharesGold\\src\\SharesGold.cs");
			TimerEngine timerEngine = GameTimeManagerAPI.Instance.GetTimerEngine();
			string text = timerEngine.AddRepeatedAction(5000, (Action)OnTimerCallback, "LorrdyAIShareGold_Timer");
		}
	}

	internal static void OnTimerCallback()
	{
		GamePlayerManagerAPI instance = GamePlayerManagerAPI.Instance;
		int[] alivePlayerIds = instance.GetAlivePlayerIds();
		uint[] array = new uint[alivePlayerIds.Length];
		for (int i = 0; i < alivePlayerIds.Length; i++)
		{
			array[i] = instance.GetPlayerGold(alivePlayerIds[i]);
		}
		for (int j = 0; j < alivePlayerIds.Length; j++)
		{
			if (!instance.IsAIPlayer(alivePlayerIds[j]) || array[j] <= Plugin.LobbySettingsViewModel.MinGoldToShare)
			{
				continue;
			}
			for (int k = 0; k < alivePlayerIds.Length; k++)
			{
				if (j != k && instance.IsPlayerAlliedTo(alivePlayerIds[j], alivePlayerIds[k]) && instance.IsAIPlayer(alivePlayerIds[k]) && array[k] < Plugin.LobbySettingsViewModel.MaxGoldToGet)
				{
					HandleGiveGold(alivePlayerIds[j], alivePlayerIds[k]);
					if (Plugin.LobbySettingsViewModel.ShowMessage)
					{
						HandleGiveGoldMessage(alivePlayerIds[j], alivePlayerIds[k]);
					}
				}
			}
		}
	}

	private static void HandleGiveGold(int from, int to)
	{
		GamePlayerManagerAPI instance = GamePlayerManagerAPI.Instance;
		int goldAmountToShare = Plugin.LobbySettingsViewModel.GoldAmountToShare;
		instance.AddPlayerGold(from, -goldAmountToShare);
		instance.AddPlayerGold(to, goldAmountToShare);
		Log.Information($"Player {from} sends {goldAmountToShare} to {to}.", "HandleGiveGold", "C:\\Documents\\Code\\StrongholdCrusaderDEMods\\AISharesGold\\src\\SharesGold.cs");
	}

	private static void HandleGiveGoldMessage(int fromId, int toId)
	{
		string playerNameById = GetPlayerNameById(fromId);
		string playerNameById2 = GetPlayerNameById(toId);
		int goldAmountToShare = Plugin.LobbySettingsViewModel.GoldAmountToShare;
		LuaNetworkAPI.SendIngameChatLocal($"sends {goldAmountToShare} gold to {playerNameById2}.", playerNameById, fromId, 20);
	}

	private static string GetPlayerNameById(int playerId)
	{
		string text = GameNetworkAPI.GetPlayerById(playerId)?.playerName;
		if (text == null)
		{
			text = GameAIManagerAPI.Instance.GetCustomAILordNameByPlayerId(playerId);
		}
		return (!string.IsNullOrEmpty(text)) ? text : $"Player {playerId}";
	}
}
