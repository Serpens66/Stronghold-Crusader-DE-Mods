using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using R3;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;

namespace ImprovedHunters
{
    /// <summary>
    /// Keeps deer and goat herds in an area around their start place by shortening their routes right after
    /// Vanilla builds them (0x127E60), the only source of their destinations. Nothing runs between two routes.
    /// When no route stays for a herd in flight, Vanilla scatters it nearby (0x11DFE0); a herd that ended up
    /// outside goes home on its next route, in flight too. Applies in every game mode but the editor, where
    /// Vanilla's herds stand still.
    /// </summary>
    internal sealed unsafe class WildlifeRangeFeature
    {
        private const int BuildRouteRva = 0x127E60;
        private const string BuildRoutePattern =
            "48 89 5C 24 20 44 89 44 24 18 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 70 48 8B D9 48 63 C2 48 69 E8 88 06 00 00 48 89 44 24 68";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void BuildRouteDelegate(IntPtr sectorGrid, int herd, int reach, int mode);

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly WildlifeNativeAddresses addresses;
        private readonly WildlifeOriginTable origins;
        // Process-lived: the detour stays installed and only its logical state changes.
        private readonly DetourHandle<BuildRouteDelegate> buildRouteHook = new DetourHandle<BuildRouteDelegate>();
        private readonly HookTransaction transaction;
        private volatile bool active;
        private int size;
        private bool failureLogged;

        public WildlifeRangeFeature(
            ManualLogSource log,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches,
            ImprovedHuntersViewModel settings,
            WildlifeNativeAddresses addresses,
            WildlifeOriginTable origins)
        {
            this.log = log;
            this.settings = settings;
            this.addresses = addresses;
            this.origins = origins;
            int buildRoute = Shared.NativePatternResolver.ResolveUnique(
                memory, BuildRoutePattern, BuildRouteRva, referenceHashMatches, "herd route builder", log).Rva;

            // The library has just loaded and no simulation runs, so no thread needs to be stopped for this.
            transaction = new HookTransaction(
                region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
                {
                    Backend = new NativeDetourBackend(NativeDetourBackend.Instance.Options with { SuspendThreadsDuringPatch = false }),
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    OwnsHooks = true
                });
            transaction.AddDetour(buildRouteHook, HookTarget.FromAddress(imageBase + (ulong)buildRoute), OnBuildRoute);
            CommitResult result = transaction.Commit();
            if (!result.IsCompleteSuccess || !buildRouteHook.Success)
                throw new InvalidOperationException("The herd route hook was not installed.");

            // Not behind the mod's own mode gate, which leaves out modes this feature serves.
            // Already at the native start: herds can get their first route in the ticks before the mission counts as started.
            Shared.MissionEvents.NativeStart.Subscribe(start => Activate(start.Context.Mode));
            Shared.MissionEvents.Started.Subscribe(started => Activate(started.Context.Mode));
            Shared.MissionEvents.Ended.Subscribe(_ => active = false);
            Shared.DebugLogHelper.LogInfo(log, $"Improved Hunters herd range hook installed: route=0x{buildRoute:X}.");
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

        private void OnBuildRoute(IntPtr sectorGrid, int herd, int reach, int mode)
        {
            buildRouteHook.Original(sectorGrid, herd, reach, mode);
            if (!active)
                return;

            try
            {
                long measured = 0;
                Shared.DevDiag.Start(ref measured);
                LimitRoute(herd);
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

        private void LimitRoute(int herd)
        {
            if (herd <= 0 || herd >= WildlifeNativeAddresses.HerdCount)
                return;

            byte* record = addresses.GetHerdRecord(herd);
            if (!addresses.IsWildDeerOrGoatHerd(record, out bool isDeer) ||
                !APIShared.UnitAccess.TryGetById(*(short*)(record + WildlifeNativeAddresses.HerdLeaderOffset), out GameUnit* leader, out _) ||
                leader == null)
            {
                return;
            }

            int leaderX = leader->r_CurrentTilePositionX, leaderY = leader->r_CurrentTilePositionY;
            WildlifeOrigin origin = GetOrAddOrigin(herd, record, isDeer, leaderX, leaderY);
            short* route = (short*)(record + WildlifeNativeAddresses.HerdRouteOffset);
            short* routeCount = (short*)(record + WildlifeNativeAddresses.HerdRouteCountOffset);
            if (WildlifeRangePolicy.IsInside(leaderX, leaderY, origin.X, origin.Y, size))
            {
                int kept = WildlifeRangePolicy.CountBeforeLeaving(new ReadOnlySpan<short>(route, *routeCount * 2), *routeCount, origin.X, origin.Y, size);
                if (kept == *routeCount)
                    return;

                *routeCount = (short)kept;
                if (kept == 0)
                    SetFirstWaypoint(route, leaderX, leaderY);
            }
            else if (AreConnected(leaderX, leaderY, origin.X, origin.Y))
            {
                // Also in flight: herds set each other off, and a route away from here each time would never end.
                SetFirstWaypoint(route, origin.X, origin.Y);
                *routeCount = 1;
            }
        }

        // A herd placed after the map started has its first route from where it stands.
        private WildlifeOrigin GetOrAddOrigin(int herd, byte* record, bool isDeer, int x, int y)
        {
            int serial = *(int*)(record + WildlifeNativeAddresses.HerdSerialOffset);
            lock (origins.SyncRoot)
            {
                if (origins.TryFind(herd, serial, out WildlifeOrigin known))
                    return known;

                var added = new WildlifeOrigin
                {
                    IsDeer = isDeer,
                    Respawns = false,
                    X = (short)x,
                    Y = (short)y,
                    Herd = herd,
                    HerdSerial = serial,
                    ExtinctSinceTick = WildlifeOrigin.NotExtinct
                };
                origins.TryAdd(added);
                return added;
            }
        }

        // Vanilla reads the first waypoint in places without looking at the count, so it must not stay stale.
        private static void SetFirstWaypoint(short* route, int x, int y)
        {
            route[0] = (short)x;
            route[1] = (short)y;
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
