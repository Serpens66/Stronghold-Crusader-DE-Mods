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

        private void ForEachActivePlayer(Action<int> callback)
        {
            for (int i = 0; i < activePlayerIds.Length; i++)
            {
                int playerId = activePlayerIds[i];
                try
                {
                    callback(playerId);
                }
                catch (Exception ex)
                {
                    LogError("Start Conditions player operation failed; remaining players continue:", playerId, ex);
                }
            }
        }

        private void TryRunFeature(string featureName, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LogError("Start Conditions feature failed; independent features continue:", featureName, ex);
            }
        }

        private Dictionary<TEnum, int> ParseEnumAmounts<TEnum>(string text, int minimum, int maximum) where TEnum : struct
        {
            Dictionary<TEnum, int> result = new Dictionary<TEnum, int>();
            if (string.IsNullOrWhiteSpace(text))
                return result;

            string[] lines = text.Split(new[] { '\r', '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            int invalidEntries = 0;
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                string[] parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                {
                    invalidEntries++;
                    continue;
                }

                string enumName = parts[0].Trim();
                string amountText = parts[1].Trim();
                if (!Enum.TryParse(enumName, true, out TEnum enumValue))
                {
                    invalidEntries++;
                    continue;
                }

                if (!int.TryParse(amountText, out int amount))
                {
                    invalidEntries++;
                    continue;
                }

                result[enumValue] = Math.Max(minimum, Math.Min(maximum, amount));
            }

            if (invalidEntries > 0)
            {
                LogWarning(
                    "Start Conditions ignored",
                    invalidEntries,
                    "invalid",
                    typeof(TEnum).Name,
                    "configuration entries.");
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

    }
}
