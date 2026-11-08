using APIShared.Wildlife;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using System;

namespace ImprovedHunters
{
    /// <summary>
    /// Keeps deer and goat herds in an area around their start place by shortening their routes right after
    /// Vanilla builds them, the only source of their destinations, through APIShared's HerdRouteEvents. Nothing runs
    /// between two routes. When no route stays for a herd in flight, Vanilla scatters it nearby; a herd that ended
    /// up outside goes home on its next route, in flight too. Applies in every game mode but the editor, where
    /// Vanilla's herds stand still.
    /// </summary>
    internal sealed class WildlifeRangeFeature
    {
        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly WildlifeOriginTable origins;
        private readonly short[] waypoints = new short[HerdRoute.MaximumWaypoints * 2];
        private volatile bool active;
        private int size;
        private bool failureLogged;

        public WildlifeRangeFeature(ManualLogSource log, ImprovedHuntersViewModel settings, WildlifeOriginTable origins)
        {
            this.log = log;
            this.settings = settings;
            this.origins = origins;
            // Process-lived: the handler stays registered and only its logical state changes.
            if (!HerdRouteEvents.TryRegister(ImprovedHuntersPlugin.PluginGuid, "herd-range", OnRoute, out string reason))
                throw new InvalidOperationException(reason);

            // Not behind the mod's own mode gate, which leaves out modes this feature serves.
            // Already at the native start: herds can get their first route in the ticks before the mission counts as started.
            Shared.MissionEvents.NativeStart.Subscribe(start => Activate(start.Context.Mode));
            Shared.MissionEvents.Started.Subscribe(started => Activate(started.Context.Mode));
            Shared.MissionEvents.Ended.Subscribe(_ => active = false);
            Shared.DebugLogHelper.LogInfo(log, "Improved Hunters herd range registered with APIShared's herd route events.");
        }

        private void Activate(APIShared.GameModes.GameModeSnapshot mode)
        {
            size = settings.HerdRangeSize;
            active = settings.EnableMod && size != HerdSettingsPolicy.Off && Shared.GameplayFeatureModePolicy.IsAllowed(
                ImprovedHuntersPlugin.PluginGuid,
                Shared.GameplayFeatureId.ImprovedHunterWildlifeRange,
                mode);
            Shared.DebugLogHelper.LogInfo(log, $"Improved Hunters herd range: active={active}, size={size}, {mode.ToDiagnosticString()}.");
        }

        private void OnRoute(HerdRoute route)
        {
            if (!active)
                return;

            try
            {
                long measured = 0;
                Shared.DevDiag.Start(ref measured);
                LimitRoute(route);
                Shared.DevDiag.StopSpike(ref measured, "hunters.herdRoute", 5000);
            }
            catch (Exception exception)
            {
                if (failureLogged)
                    return;

                failureLogged = true;
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters herd range failed: {exception}");
            }
        }

        private void LimitRoute(HerdRoute route)
        {
            HerdInfo herd = route.Herd;
            int leaderX = route.LeaderTileX, leaderY = route.LeaderTileY;
            if (!WildlifeNativeAddresses.IsWildDeerOrGoatHerd(herd, out bool isDeer) || leaderX < 0 || leaderY < 0)
                return;

            WildlifeOrigin origin = GetOrAddOrigin(herd, isDeer, leaderX, leaderY);
            if (WildlifeRangePolicy.IsInside(leaderX, leaderY, origin.X, origin.Y, size))
            {
                int count = route.WaypointCount;
                for (int index = 0; index < count; index++)
                {
                    HerdWaypoint waypoint = route.GetWaypoint(index);
                    waypoints[index * 2] = (short)waypoint.TileX;
                    waypoints[index * 2 + 1] = (short)waypoint.TileY;
                }

                // APIShared gives a route cut to nothing the leader's own tile as its first waypoint.
                route.Truncate(WildlifeRangePolicy.CountBeforeLeaving(new ReadOnlySpan<short>(waypoints, 0, count * 2), count, origin.X, origin.Y, size));
            }
            else if (AreConnected(leaderX, leaderY, origin.X, origin.Y))
            {
                // Also in flight: herds set each other off, and a route away from here each time would never end.
                route.ReplaceWithSingleWaypoint(origin.X, origin.Y);
            }
        }

        // A herd placed after the map started has its first route from where it stands.
        private WildlifeOrigin GetOrAddOrigin(HerdInfo herd, bool isDeer, int x, int y)
        {
            int serial = unchecked((int)herd.GlobalId);
            lock (origins.SyncRoot)
            {
                if (origins.TryFind(herd.HerdId, serial, out WildlifeOrigin known))
                    return known;

                var added = new WildlifeOrigin
                {
                    IsDeer = isDeer,
                    Respawns = false,
                    X = (short)x,
                    Y = (short)y,
                    Herd = herd.HerdId,
                    HerdSerial = serial,
                    ExtinctSinceTick = WildlifeOrigin.NotExtinct
                };
                origins.TryAdd(added);
                return added;
            }
        }

        private static bool AreConnected(int fromX, int fromY, int toX, int toY)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int from = tiles.GetTileId(fromX, fromY), to = tiles.GetTileId(toX, toY);
            return tiles.IsValidTileId(from) && tiles.IsValidTileId(to) &&
                (uint)from < (uint)components.Length && (uint)to < (uint)components.Length &&
                components[from] == components[to];
        }
    }
}
