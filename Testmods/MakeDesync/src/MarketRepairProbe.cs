using System;
using System.Reflection;
using System.Threading;
using APIShared.Multiplayer;
using BepInEx.Bootstrap;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace MakeDesync
{
    // Observation only. Never invoke the market owner's repair or purchase methods.
    internal sealed unsafe class MarketRepairProbe
    {
        private const string OwnerGuid = "AIMarketOverbuyDiagnostic_Serp";
        private const int Players = GamePlayerManagerAPI.MAX_PLAYERS, Goods = (int)eGoods.Count;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly Type owner;
        private readonly object engineGate;
        private readonly Func<bool> inMultiplayer;
        private readonly Action<string> report;
        private Snapshot before;

        private sealed class Snapshot
        {
            internal readonly int[] Pending = new int[(Players + 1) * Goods];
            internal int SeenPlayers, Active, Partial, Writer, Receipt;
            internal bool Repairing, Correction;
        }

        private MarketRepairProbe(Type owner, object engineGate, Func<bool> inMultiplayer, Action<string> report)
        { this.owner = owner; this.engineGate = engineGate; this.inMultiplayer = inMultiplayer; this.report = report; }

        internal static void Register(object engineGate, Func<bool> inMultiplayer, Action<string> report)
        {
            if (!Chainloader.PluginInfos.TryGetValue(OwnerGuid, out var plugin) ||
                !new Version(0, 1, 0).Equals(plugin.Metadata.Version) || ReferenceEquals(plugin.Instance, null)) return;
            var owner = plugin.Instance.GetType().Assembly.GetType("AIMarketOverbuyDiagnostic.MarketPendingDemandFix");
            if (owner == null) { report("MAKE_DESYNC_MARKET_UNAVAILABLE reason=missing-devdiag-owner"); return; }
            var probe = new MarketRepairProbe(owner, engineGate, inMultiplayer, report);
            if (!MultiplayerResyncEvents.TryRegister(Plugin.Guid, "market-before", () => probe.Observe(true, true),
                    () => probe.Observe(false, true), out string reason, order: -101) ||
                !MultiplayerResyncEvents.TryRegister(Plugin.Guid, "market-after", () => probe.Observe(true, false),
                    () => probe.Observe(false, false), out reason, order: -99))
                report("MAKE_DESYNC_MARKET_UNAVAILABLE " + reason);
            else report("MAKE_DESYNC_MARKET_READY");
        }

        private void Observe(bool starting, bool first)
        {
            // These are critical APIShared callbacks. A failed test observation must not fault the world.
            try
            {
                if (!Monitor.IsEntered(engineGate) || !inMultiplayer()) return;
                if (first) before = null;
                var snapshot = Capture();
                if (first) { before = snapshot; return; }
                var prior = before;
                before = null;
                string stage = starting ? "start" : "complete";
                if (prior == null) { report($"MAKE_DESYNC_MARKET_UNAVAILABLE stage={stage} reason=no-before-snapshot"); return; }
                bool pendingSame = prior.SeenPlayers == snapshot.SeenPlayers &&
                    prior.Pending.AsSpan().SequenceEqual(snapshot.Pending);
                bool gates = starting
                    ? snapshot.Writer == 0 && snapshot.Receipt == 0
                    : prior.Writer == 0 && prior.Receipt == 0 && snapshot.Correction;
                bool pass = snapshot.SeenPlayers != 0 && pendingSame && snapshot.Active == 0 &&
                    snapshot.Repairing == starting && gates;
                report($"MAKE_DESYNC_MARKET_BOUNDARY stage={stage} pendingUnchanged={pendingSame} " +
                    $"activeBefore={prior.Active} activeAfter={snapshot.Active} partialBefore={prior.Partial} " +
                    $"writer={snapshot.Writer} receipt={snapshot.Receipt} pass={pass}");
            }
            catch (Exception error)
            {
                before = null;
                try { report("MAKE_DESYNC_MARKET_UNAVAILABLE " + error.Message); }
                catch { /* Diagnostic output cannot affect simulation. */ }
            }
        }

        private Snapshot Capture()
        {
            var world = owner.GetField("worldState", Static).GetValue(null);
            var snapshot = new Snapshot
            {
                Repairing = (bool)Field(world, "InRepair"),
                Correction = (bool)owner.GetProperty("CorrectionActive", Static).GetValue(null),
                Writer = ReadGate("writerGate"),
                Receipt = ReadGate("receiptGate")
            };
            var rows = (Array)owner.GetField("requests", Static).GetValue(null);
            if (rows.Length != snapshot.Pending.Length || snapshot.Writer < 0 || snapshot.Receipt < 0)
                throw new InvalidOperationException("Unexpected live market layout.");
            var resourcesApi = GamePlayerManagerAPI.Instance;
            for (int player = 1; player <= Players; player++)
            {
                if (!resourcesApi.TryGetPlayerResourcesById(player, out GamePlayerResources* resources) || resources == null) continue;
                snapshot.SeenPlayers |= 1 << player;
                int* pending = (int*)&resources->r_AIPendingMarketPurchaseAmountNull;
                for (int good = 0; good < Goods; good++)
                {
                    int index = player * Goods + good;
                    snapshot.Pending[index] = pending[good];
                    object target = Field(rows.GetValue(index), "Target");
                    if (!(bool)Field(target, "Active")) continue;
                    snapshot.Active++;
                    if (pending[good] == 4 && (int)Field(target, "ExpectedPending") == 4 &&
                        (int)Field(target, "OriginalPackage") == 5 && (int)Field(target, "Credited") == 1 &&
                        (int)Field(target, "LastObservedStock") == 0 && resourcesApi.GetGoodAmount(player, (eGoods)good) == 0)
                        snapshot.Partial++;
                }
            }
            return snapshot;
        }

        private int ReadGate(string name)
        {
            var address = (IntPtr)owner.GetField(name, Static).GetValue(null);
            return address == IntPtr.Zero ? -1 : *(int*)address;
        }

        private static object Field(object instance, string name) =>
            instance.GetType().GetField(name, Instance).GetValue(instance);
    }
}
