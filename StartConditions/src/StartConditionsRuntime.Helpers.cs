using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace StartConditions
{
    public sealed partial class StartConditionsRuntime
    {
        private static bool IsLocalPlayer(int playerId)
        {
            int rawLocalPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return playerId == rawLocalPlayerId;
        }

        private void ForEachAlivePlayer(Action<int> callback)
        {
            int[] aliveIds = GamePlayerManagerAPI.Instance.GetAlivePlayerIds();
            for (int i = 0; i < aliveIds.Length; i++)
            {
                int playerId = aliveIds[i] + 1;
                if (GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) && HasKeep(playerId))
                    callback(playerId);
            }
        }

        private bool HasKeep(int playerId)
        {
            int keepId = GamePlayerManagerAPI.Instance.GetPlayerKeepId(playerId);
            return keepId != -1 && keepId != 0;
        }

        private static bool TryParseNullableInt(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            return int.TryParse(text.Trim(), out value);
        }

        private Dictionary<TEnum, int> ParseEnumAmounts<TEnum>(string text) where TEnum : struct
        {
            Dictionary<TEnum, int> result = new Dictionary<TEnum, int>();
            if (string.IsNullOrWhiteSpace(text))
                return result;

            string[] lines = text.Split(new[] { '\r', '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                string[] parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                {
                    LogDebug("Invalid setting line:", line);
                    continue;
                }

                string enumName = parts[0].Trim();
                string amountText = parts[1].Trim();
                if (!Enum.TryParse(enumName, true, out TEnum enumValue))
                {
                    LogDebug("Unknown enum value:", enumName);
                    continue;
                }

                if (!int.TryParse(amountText, out int amount))
                {
                    LogDebug("Invalid amount for", enumName, ":", amountText);
                    continue;
                }

                result[enumValue] = amount;
            }

            return result;
        }

        private static bool IsDefinedEnumValue<TEnum>(object value) where TEnum : struct
        {
            Type enumType = typeof(TEnum);
            Type underlyingType = Enum.GetUnderlyingType(enumType);
            object typedValue = Convert.ChangeType(value, underlyingType);
            return Enum.IsDefined(enumType, typedValue);
        }

        private void LogConfiguredTroops(string label, Dictionary<eChimps, int> troops)
        {
            LogDebug(label, FormatTroopCounts(troops));
        }

        private static string FormatTroopCounts(Dictionary<eChimps, int> troops)
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<eChimps, int> entry in troops)
            {
                if (entry.Value > 0)
                    parts.Add(entry.Key + "=" + entry.Value);
            }

            return parts.Count == 0 ? "<none>" : string.Join(", ", parts);
        }
    }
}
