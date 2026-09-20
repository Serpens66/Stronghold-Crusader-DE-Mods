using System;

namespace SkirmishGameOptionsTest
{
    internal static class SkirmishGameOptionsPolicy
    {
        internal sealed class AdvancedState
        {
            internal int[] Buildings = Array.Empty<int>();
            internal int[] Goods = Array.Empty<int>();
            internal int[] Troops = Array.Empty<int>();
            internal int PreBuild;
            internal int ImprovedArabSwordsmen;
            internal int ImprovedLaddermen;
            internal int ImprovedSpearmen;
            internal int RebalancedHorseArchers;
            internal int ImprovedFletchers;
            internal int UncappedPeasants;
            internal int FasterPeasants;
            internal int EnemyHitPoints = 1;
            internal int ImprovedSieging;
            internal int ImprovedSieging2;
            internal int Healers;
            internal int Eunuchs;
            internal int NoGold;
        }

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
                   command.StartsWith("TROOPS_", StringComparison.Ordinal) ||
                   IsPresetLoadCommand(command);
        }

        internal static bool IsPresetLoadCommand(string command) =>
            string.Equals(command, "UsePrevious", StringComparison.Ordinal) ||
            string.Equals(command, "UseDefault", StringComparison.Ordinal) ||
            string.Equals(command, "UsePresets1", StringComparison.Ordinal) ||
            string.Equals(command, "UsePresets2", StringComparison.Ordinal);

        internal static bool IsPresetSaveCommand(string command) =>
            string.Equals(command, "SavePresets1", StringComparison.Ordinal) ||
            string.Equals(command, "SavePresets2", StringComparison.Ordinal);

        internal static bool ShouldBlockCow(bool localSkirmish, bool noCows) =>
            localSkirmish && noCows;

        internal static bool ShouldBlockAutoTrading(bool localSkirmish, bool allowAutoTrading) =>
            localSkirmish && !allowAutoTrading;

        internal static bool ShouldAllowNoDogsToggle(bool nativePatchAvailable) =>
            nativePatchAvailable;

        internal static bool HasConfiguredAdvancedOptions(AdvancedState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return HasDisabledEntry(state.Buildings) ||
                   HasDisabledEntry(state.Goods) ||
                   HasDisabledEntry(state.Troops) ||
                   state.PreBuild > 0 ||
                   state.ImprovedArabSwordsmen > 0 ||
                   state.ImprovedLaddermen > 0 ||
                   state.ImprovedSpearmen > 0 ||
                   state.RebalancedHorseArchers > 0 ||
                   state.ImprovedFletchers > 0 ||
                   state.UncappedPeasants > 0 ||
                   state.FasterPeasants > 0 ||
                   state.EnemyHitPoints != 1 ||
                   state.ImprovedSieging > 0 ||
                   state.ImprovedSieging2 > 0 ||
                   state.Healers > 0 ||
                   state.Eunuchs > 0 ||
                   state.NoGold > 0;
        }

        internal static int ToSkirmishAdvancedFlag(
            bool advancedRequested,
            AdvancedState state) =>
            advancedRequested && HasConfiguredAdvancedOptions(state) ? 1 : 0;

        internal static bool ShouldShowAdvancedIndicator(
            int advancedSkirmishFlag,
            AdvancedState state) =>
            advancedSkirmishFlag != 0 && HasConfiguredAdvancedOptions(state);

        internal static float GetExtremeTroopsOpacity(bool localSkirmish) =>
            localSkirmish ? 1f : 0.5f;

        internal static float GetOutpostOpacity(bool mapAllowsOutposts) =>
            mapAllowsOutposts ? 1f : 0.3f;

        internal static bool ShouldAllowOutpostToggle(
            bool localSkirmish,
            bool mapAllowsOutposts) =>
            !localSkirmish || mapAllowsOutposts;

        internal static int NormalizeOutpostsForApply(
            bool localSkirmish,
            bool mapAllowsOutposts,
            int requestedValue) =>
            localSkirmish && !mapAllowsOutposts ? 0 : requestedValue;

        internal static int ToSharedAdvancedFlag(
            int multiplayerFlag,
            int skirmishFlag,
            AdvancedState state) =>
            ToSkirmishAdvancedFlag(
                multiplayerFlag != 0 || skirmishFlag != 0,
                state);

        private static bool HasDisabledEntry(int[] values)
        {
            if (values == null)
                return false;

            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == 0)
                    return true;
            }
            return false;
        }
    }
}
