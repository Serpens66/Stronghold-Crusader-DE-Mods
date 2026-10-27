namespace CastlePlanner
{
    internal static class BlueprintLayoutPolicy
    {
        public static bool CanPrepare(bool previewActive, bool previewHasSelectedCastle)
        {
            // Only the active spawn preview needs a castle selection; normal
            // local Blueprints are independent of whether a castle was spawned.
            return !previewActive || previewHasSelectedCastle;
        }
    }

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
