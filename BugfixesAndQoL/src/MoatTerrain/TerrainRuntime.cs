using APIShared;
using APIShared.NativeSites;
using APIShared.Savegames;
using BepInEx;
using BepInEx.Logging;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL.MoatTerrain
{
    internal sealed unsafe class TerrainRuntime
    {
        internal const string SaveIdentifier = "BugfixesAndQoL.MoatTerrain";
        private const string SiteName = "MoatTerrainRestoration";
        private const string Entry = "terrain";
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly List<HookHandle<X64InlineHook>> hooks = new List<HookHandle<X64InlineHook>>();
        private HookTransaction transaction;
        private IntPtr buffer;
        private TerrainHistory history;
        private object engineGate;
        private long nativeTileManager;
        private int reportedMarkers;
        private readonly TerrainPublication publication = new TerrainPublication();

        internal TerrainRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings)
        { this.log = log; this.settings = settings; }
        private bool Enabled => publication.IsReady && settings.EnableMod && settings.EnableMoatTerrainRestoration;

        internal void Install(CrusaderLibraryLoadContext context, bool referenceHashMatches)
        {
            if (context == null || context.ModuleHandle == IntPtr.Zero || !referenceHashMatches)
                throw new InvalidOperationException("Moat terrain requires its audited native image/layout.");
            string path = Path.Combine(Paths.GameRootPath, "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            if (Shared.DebugLogHelper.GetFileSha256(path) != NativeDefinition.Hash)
                throw new InvalidOperationException("Moat terrain native hash differs.");
            NativeDefinition.ValidateLayouts();
            NativeDefinition.ValidateCode(context.Memory);
            // Existing Vanilla lock serializes run, editor mutations and saving. Resolve it once;
            // never depend on another optional feature having initialized its own bridge.
            FieldInfo gate = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
            if (gate == null || gate.FieldType != typeof(object) || (engineGate = gate.GetValue(null)) == null)
                throw new MissingFieldException("EngineInterface.threadLock");
            ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
            nativeTileManager = unchecked((long)(image + NativeDefinition.TileManager));
            foreach (NativeDefinition.Site site in NativeDefinition.Sites)
            {
                string pattern = BitConverter.ToString(site.Bytes).Replace('-', ' ');
                var resolved = Shared.NativePatternResolver.ResolveUnique(context.Memory, pattern, site.Rva, true,
                    "moat terrain " + site.Kind, log);
                if (resolved.Rva != site.Rva) throw new InvalidOperationException("Moat terrain target moved outside its audited layout.");
                byte[] live = new byte[site.Length];
                Marshal.Copy(new IntPtr(unchecked((long)(image + (uint)site.Rva))), live, 0, live.Length);
                if (!live.AsSpan().SequenceEqual(site.Bytes)) throw new InvalidOperationException("Occupied moat terrain site: " + site.Kind);
                using (var probe = new X64InlineHook(image + (uint)site.Rva, site.Length))
                    if (probe.DisplacedByteCount != site.Length) throw new InvalidOperationException("Unexpected moat terrain displacement: " + site.Kind);
            }
            NativeSiteSpan[] spans = new NativeSiteSpan[NativeDefinition.Sites.Length];
            for (int i = 0; i < spans.Length; i++)
                spans[i] = new NativeSiteSpan(NativeDefinition.Sites[i].Rva, NativeDefinition.Sites[i].Length, NativeSiteMode.Exclusive);
            var claim = NativeSiteRegistry.Register(BugfixesAndQoLPlugin.PluginGuid, SiteName, spans);
            if (!claim.Succeeded) throw new InvalidOperationException(claim.Reason);
            try
            {
                buffer = Marshal.AllocHGlobal(TerrainState.BufferBytes);
                history = new TerrainHistory(buffer);
                ulong state = unchecked((ulong)buffer.ToInt64());
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region, installOnCommit: false);
                foreach (NativeDefinition.Site site in NativeDefinition.Sites)
                {
                    var handle = new HookHandle<X64InlineHook>();
                    hooks.Add(handle);
                    transaction.AddInline(handle, HookTarget.FromAddress(image + (uint)site.Rva),
                        (a, original, ret) => NativeCode.Emit(a, site.Kind, state, image + NativeDefinition.TileManager, original),
                        hookSize: site.Length, name: "MoatTerrain_" + site.Kind);
                }
                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess) throw new InvalidOperationException("Moat terrain transaction failed: " + result);
                for (int i = 0; i < hooks.Count; i++)
                {
                    var site = NativeDefinition.Sites[i]; var handle = hooks[i];
                    if (!handle.Success || handle.Hook.IsInstalled || handle.Hook.TargetAddress != image + (uint)site.Rva ||
                        handle.Hook.DisplacedByteCount != site.Length)
                        throw new InvalidOperationException("Prepared moat terrain span differs: " + site.Kind);
                }
                var registration = CreateSaveRegistration();
                if (!VersionedSaveData.TryRegister(registration, out _, out string reason)) throw new InvalidOperationException(reason);
                publication.RetainRegisteredResources();
                Shared.MissionEvents.SetOwner(BugfixesAndQoLPlugin.PluginGuid);
                subscriptions.Add(Shared.MissionEvents.Initialization.Subscribe(OnInitialization));
                subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(OnEnd));
                subscriptions.Add(Shared.MissionEvents.Started.Subscribe(OnStart));
                publication.Enable(() =>
                {
                    foreach (var hook in result.Installed) hook.Enable();
                }, () => ValidateInstalledHooks(image));
                Info("MOAT_TERRAIN_READY: 14 permanent native sites; native-only tile work; save schema 1; awaiting mission.");
            }
            catch
            {
                publication.Fail(() => history?.SetEnabled(false), () =>
                {
                    transaction?.Dispose(); // Only an unpublished initialization candidate.
                    if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                    buffer = IntPtr.Zero;
                    NativeSiteRegistry.Unregister(BugfixesAndQoLPlugin.PluginGuid, SiteName);
                });
                throw;
            }
        }

        internal void Refresh()
        {
            if (!publication.IsReady) return;
            lock (engineGate) history.SetEnabled(Enabled);
        }

        private void ValidateInstalledHooks(ulong image)
        {
            for (int i = 0; i < hooks.Count; i++)
            {
                var site = NativeDefinition.Sites[i]; var handle = hooks[i];
                if (!handle.Success || !handle.Hook.IsInstalled || handle.Hook.TargetAddress != image + (uint)site.Rva ||
                    handle.Hook.DisplacedByteCount != site.Length)
                    throw new InvalidOperationException("Installed moat terrain span differs: " + site.Kind);
                byte* live = (byte*)(image + (uint)site.Rva);
                if (live[0] != 0xFF || live[1] != 0x25 || *(uint*)(live + 2) != 0 ||
                    *(ulong*)(live + 6) != unchecked((ulong)handle.Hook.StubAddress.ToInt64()))
                    throw new InvalidOperationException("Installed moat terrain jump differs: " + site.Kind);
                for (int n = 14; n < site.Length; n++) if (live[n] != 0x90)
                    throw new InvalidOperationException("Installed moat terrain padding differs: " + site.Kind);
                Info($"MOAT_TERRAIN_HOOK kind={site.Kind}; rva=0x{site.Rva:X}; end=0x{site.Rva + site.Length:X}; span={site.Length}.");
            }
        }

        // Keep the archive boundary declarative and independently inspectable. No registration
        // or native work happens here; APIShared owns the actual format and multiplayer agreement.
        internal VersionedSaveDataRegistration CreateSaveRegistration() =>
            new VersionedSaveDataRegistration(BugfixesAndQoLPlugin.PluginGuid, SaveIdentifier,
                1, 1, new[] { Entry }, Capture, Loaded)
            { Scope = VersionedSaveScope.SaveGamesAndMaps, SimulationRelevant = true };

        private void OnInitialization(MissionLifecycleNotification notification)
        {
            if (notification.Phase != MissionInitializationPhase.BeforeLoad) return;
            lock (engineGate) { history.Reset(); reportedMarkers = 0; }
        }
        private void OnEnd(MissionLifecycleNotification notification)
        {
            lock (engineGate)
            {
                ReportExecution(); history.Reset(); reportedMarkers = 0;
            }
        }
        private void OnStart(MissionLifecycleNotification notification)
        {
            try
            {
                lock (engineGate)
                {
                    ValidateCurrentView();
                    history.Begin(notification.Context.SessionId, GameTileManagerAPI.Instance.GetCurrentMapSize(), Enabled, Filter);
                    Info($"MOAT_TERRAIN_SESSION id={notification.Context.SessionId}; active={history.Active}; editor={notification.Context.IsEditor}; save={notification.Context.IsSave}; replay={notification.IsReplay}.");
                }
            }
            catch (Exception error) { lock (engineGate) history.SetEnabled(false); Error("Mission activation rejected", error); }
        }
        private void Loaded(VersionedSaveLoad load)
        {
            lock (engineGate)
            {
                if (!Enabled) { history.SetEnabled(false); return; }
                if (load.Status == VersionedSaveLoadStatus.NoRecord || load.Status == VersionedSaveLoadStatus.OutOfScope ||
                    load.Status == VersionedSaveLoadStatus.Corrupt) return;
                if (load.Status != VersionedSaveLoadStatus.Restored)
                { history.RejectLoad(); Info("MOAT_TERRAIN_LOAD inactive: " + load.Status); return; }
                if (!load.Entries.TryGetValue(Entry, out byte[] bytes)) return;
                try { history.Stage(bytes); }
                catch (Exception error)
                {
                    history.RejectLoad(); Error("Saved terrain data rejected", error);
                    throw; // APIShared isolates this and preserves the unreadable source record.
                }
            }
        }
        private bool Capture(VersionedSaveWriter writer)
        {
            lock (engineGate)
            {
                try
                {
                    ReportExecution();
                    byte[] bytes = history.Capture(Enabled, Filter);
                    if (bytes != null) writer.Write(Entry, bytes);
                    return true; // Empty replaces old known entries, including when disabled.
                }
                catch (Exception error)
                {
                    history.SetEnabled(false); Error("Terrain capture rejected; history discarded", error);
                    return true; // Never leave a stale known entry in an otherwise successful save.
                }
            }
        }
        private void ValidateCurrentView()
        {
            var api = GameTileManagerAPI.Instance;
            if (api.GetTileManager().ToInt64() != nativeTileManager || api.GetLogicLayer().Length != TerrainState.Capacity ||
                api.GetLogic2Layer().Length != TerrainState.Capacity || api.GetStructureLayer().Length != TerrainState.Capacity)
                throw new InvalidOperationException("Current tile spans differ from the native contract.");
        }
        private static void Filter(Span<ushort> tiles)
        {
            var api = GameTileManagerAPI.Instance;
            var logic = api.GetLogicLayer(); var structures = api.GetStructureLayer();
            if (logic.Length != tiles.Length || structures.Length != tiles.Length) throw new InvalidOperationException("Saved tile capacity differs.");
            for (int tile = 0; tile < tiles.Length; tile++)
            {
                ushort value = tiles[tile];
                if (value == 0) continue;
                if (!TerrainState.IsPersistent(value)) { tiles[tile] = 0; continue; }
                var flags = (TilePropertyFlag)logic[tile];
                if ((flags & TilePropertyFlag.IsMoat) != 0 && (value & TerrainState.Dug) != 0) continue;
                if ((flags & TilePropertyFlag.PlannedMoat) != 0 && (value & TerrainState.Planned) != 0) continue;
                int buildingId = structures[tile];
                if ((value & TerrainState.Bridge) != 0 && buildingId > 0 &&
                    GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) &&
                    building->r_BuildingType == eStructs.STRUCT_DRAWBRIDGE) continue;
                tiles[tile] = 0;
            }
        }
        private void ReportExecution()
        {
            int markers = (history.Captured != 0 ? 1 : 0) | (history.Restored != 0 ? 2 : 0) | (history.InvalidContext ? 4 : 0);
            if ((markers & 3 & ~reportedMarkers) != 0)
                Info($"MOAT_TERRAIN_EXECUTED: captured={history.Captured != 0}; restored={history.Restored != 0}; afterStartupCleanup=true.");
            if ((markers & 4 & ~reportedMarkers) != 0) Error("Native tile bounds rejected a context", null);
            reportedMarkers |= markers;
        }
        private void Info(string text) { try { log.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {text}"); } catch { } }
        private void Error(string text, Exception error) { try { log.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MOAT_TERRAIN_ERROR {text}: {error}"); } catch { } }
    }
}
