using System.Collections.Generic;

namespace CastlePlanner
{
    internal enum BlueprintHudDisplayState
    {
        Unavailable,
        Off,
        Loading,
        On
    }

    internal static class BlueprintHudStatePolicy
    {
        public static BlueprintHudDisplayState Resolve(
            bool controlledKeepAvailable,
            bool blueprintVisible,
            int completedDepthCaptures,
            int requestedDepthCaptures)
        {
            if (!controlledKeepAvailable)
                return BlueprintHudDisplayState.Unavailable;
            if (!blueprintVisible)
                return BlueprintHudDisplayState.Off;
            return completedDepthCaptures < requestedDepthCaptures
                ? BlueprintHudDisplayState.Loading
                : BlueprintHudDisplayState.On;
        }
    }

    internal static class BlueprintSelectionPolicy
    {
        public static bool IsValidBindingSelection(
            string candidate,
            ICollection<string> availableChoices)
        {
            return !string.IsNullOrEmpty(candidate) &&
                availableChoices != null &&
                availableChoices.Contains(candidate);
        }
    }

    internal static class BlueprintSearchPolicy
    {
        public static bool Matches(string option, string searchText)
        {
            return string.IsNullOrWhiteSpace(searchText) ||
                (!string.IsNullOrEmpty(option) &&
                 option.IndexOf(searchText.Trim(),
                     System.StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
