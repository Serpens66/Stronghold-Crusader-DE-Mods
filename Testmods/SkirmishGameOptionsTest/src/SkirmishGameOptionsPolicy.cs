using System;

namespace SkirmishGameOptionsTest
{
    internal static class SkirmishGameOptionsPolicy
    {
        internal static bool IsWorkingCopyCommand(string command)
        {
            if (string.IsNullOrEmpty(command) ||
                string.Equals(command, "Setup", StringComparison.Ordinal) ||
                string.Equals(command, "ApplySettings", StringComparison.Ordinal) ||
                string.Equals(command, "CancelSettings", StringComparison.Ordinal))
            {
                return false;
            }

            return command.StartsWith("Settings_", StringComparison.Ordinal) ||
                   command.StartsWith("Fairness", StringComparison.Ordinal) ||
                   command.StartsWith("GameType", StringComparison.Ordinal) ||
                   command.StartsWith("GOODS_", StringComparison.Ordinal) ||
                   command.StartsWith("STRUCT_", StringComparison.Ordinal) ||
                   command.StartsWith("TROOPS_", StringComparison.Ordinal);
        }

        internal static bool ShouldBlockCow(bool localSkirmish, bool noCows) =>
            localSkirmish && noCows;

        internal static bool ShouldBlockAutoTrading(bool localSkirmish, bool allowAutoTrading) =>
            localSkirmish && !allowAutoTrading;

        internal static int ToSkirmishAdvancedFlag(int normalizedAdvancedOptions) =>
            normalizedAdvancedOptions == 0 ? 0 : 1;
    }
}
