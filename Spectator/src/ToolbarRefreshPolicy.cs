using System;

namespace Spectator
{
    [Flags]
    internal enum ToolbarRefresh { None = 0, Status = 1, Layout = 2, State = 4 }

    // Bound values update themselves. Only changes affecting our imperative presentation
    // request extra work; typing an amount must not rebuild player cards or rich tooltips.
    internal static class ToolbarRefreshPolicy
    {
        internal static ToolbarRefresh ForProperty(string property)
        {
            switch (property)
            {
                case null: case "":
                    return ToolbarRefresh.Status | ToolbarRefresh.Layout | ToolbarRefresh.State;
                case "Status":
                    return ToolbarRefresh.Status;
                case "StatusVisibility": case "GoodsStatusVisibility":
                    return ToolbarRefresh.Status | ToolbarRefresh.Layout;
                case "ExpandedVisibility": case "GoodsVisibility": case "OrdersVisibility":
                case "NavigationVisibility": case "PlacementActive": case "PlacementVisibility":
                case "ExpandButtonVisibility": case "ActiveViewKey":
                    return ToolbarRefresh.Status | ToolbarRefresh.Layout | ToolbarRefresh.State;
                case "ExpandLabel": case "ExpandFontSize":
                    return ToolbarRefresh.Layout;
                case "Powers": case "PowersEnabled":
                    return ToolbarRefresh.State;
                default:
                    return ToolbarRefresh.None;
            }
        }
    }
}
