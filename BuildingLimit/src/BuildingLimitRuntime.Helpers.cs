using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;

namespace BuildingLimit
{
    public sealed partial class BuildingLimitRuntime
    {
        private static bool IsLocalPlayer(int playerId)
        {
            int rawLocalPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return playerId == rawLocalPlayerId;
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
                    LogInfo("Invalid setting line:", line);
                    continue;
                }

                string enumName = parts[0].Trim();
                string amountText = parts[1].Trim();
                if (!Enum.TryParse(enumName, true, out TEnum enumValue))
                {
                    LogInfo("Unknown enum value:", enumName);
                    continue;
                }

                if (!int.TryParse(amountText, out int amount))
                {
                    LogInfo("Invalid amount for", enumName, ":", amountText);
                    continue;
                }

                result[enumValue] = amount;
            }

            return result;
        }
    }
}
