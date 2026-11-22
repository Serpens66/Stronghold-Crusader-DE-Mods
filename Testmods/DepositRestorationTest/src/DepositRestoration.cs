using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using APIShared;
using APIShared.Multiplayer;
using APIShared.NativeSites;
using APIShared.Savegames;
using BepInEx;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Shared;

namespace DepositRestoration
{
    internal sealed unsafe class DepositRestorationRuntime
    {
        private const string Feature = "wall-deposit-restoration";
        // Stable across the move to a main mod, including saves from the original prototype.
        private const string SaveIdentifier = "DepositRestorationTest_Serp";
        private const string Entry = "covered-deposits";
        private readonly string ownerGuid;
        private readonly ManualLogSource log;
        private HookTransaction transaction;
        private IntPtr state;
        private object engineGate;
        private ulong tileManager;
        private long pendingSession, activeSession;
        private byte[] pending;
        private bool invalidRecord, multiplayerWorld, repairing;
        private volatile bool ready, agreed;
        private Func<bool> intact;

        // Construct on the Unity thread before joining a lobby. Retain for the process.
        internal DepositRestorationRuntime(string ownerGuid, ManualLogSource log)
        {
            this.ownerGuid = ownerGuid;
            this.log = log;
            SimulationAgreement.Register(ownerGuid, Feature, required: true,
                isReady: () => ready && intact() && MultiplayerResyncEvents.CheckIntegrity(),
                apply: enabled => agreed = enabled);
        }

        internal void Install(CrusaderLibraryLoadContext context)
        {
            if (transaction != null) return;
            bool claimed = false;
            bool publishing = false;
            try
            {
                string path = Path.Combine(Paths.GameRootPath, "Stronghold Crusader Definitive Edition_Data",
                    "Plugins", "x86_64", "CrusaderDE.dll");
                if (context.ModuleHandle == IntPtr.Zero || DebugLogHelper.GetFileSha256(path) != DepositHooks.NativeHash ||
                    DepositHooks.Capacity != GameTileManagerView.NativePackedTileCapacity ||
                    DepositHooks.Deposits != 0xA0000 || (int)TilePropertyFlag.IsWall != 0x100)
                    throw new InvalidOperationException("The audited native terrain layout is unavailable.");

                if (!ApiShared.ForMod(ownerGuid).TryGetMissionLifecycle(out var lifecycle, out var diagnostic))
                    throw new InvalidOperationException(diagnostic?.Reason);
                var monitor = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
                if (monitor == null || !monitor.IsPrivate || monitor.FieldType != typeof(object))
                    throw new MissingFieldException("EngineInterface.threadLock");
                engineGate = monitor.GetValue(null) ?? throw new InvalidOperationException("Engine monitor unavailable.");

                ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
                tileManager = image + DepositHooks.TileManager;
                NativePatchGuard.Bind(context, log);
                var sites = DepositHooks.Sites;
                var spans = new NativeSiteSpan[sites.Length];
                var handles = new HookHandle<X64InlineHook>[sites.Length];
                for (int index = 0; index < sites.Length; index++)
                {
                    var site = sites[index];
                    if (!context.Memory.Slice(site.Rva, site.Bytes.Length).SequenceEqual(site.Bytes))
                        throw new InvalidOperationException("Unexpected original bytes at " + site.Name);
                    NativePatchGuard.RequireUnpatched(image + (uint)site.Rva, site.Bytes.Length, site.Name, Feature);
                    spans[index] = new NativeSiteSpan(site.Rva, site.Bytes.Length, NativeSiteMode.Exclusive);
                }
                var claim = NativeSiteRegistry.Register(ownerGuid, Feature, spans);
                if (!claim.Succeeded) throw new InvalidOperationException(claim.Reason);
                claimed = true;

                state = Marshal.AllocHGlobal(DepositHooks.HistoryOffset + DepositHooks.Capacity);
                Reset();
                var factory = SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory;
                transaction = new HookTransaction(context.Region, factory, new HookTransactionOptions
                {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    InstallOnCommit = false,
                    OwnsHooks = true
                });
                for (int index = 0; index < sites.Length; index++)
                {
                    var site = sites[index];
                    var handle = handles[index] = new HookHandle<X64InlineHook>();
                    transaction.AddInline(handle, HookTarget.FromAddress(image + (uint)site.Rva),
                        (assembler, original, _) => DepositHooks.Emit(assembler, site,
                            unchecked((ulong)state.ToInt64()), tileManager + DepositHooks.Logic, original),
                        hookSize: site.Bytes.Length, name: "Deposits." + site.Name);
                }
                var result = transaction.Commit();
                if (!result.IsCompleteSuccess) throw new InvalidOperationException(result.ToString());
                for (int index = 0; index < sites.Length; index++)
                {
                    var site = sites[index];
                    var handle = handles[index];
                    if (!handle.Success || handle.Hook.TargetAddress != image + (uint)site.Rva ||
                        handle.Hook.DisplacedByteCount != site.Bytes.Length)
                        throw new InvalidOperationException("Unexpected displaced span at " + site.Name);
                    NativePatchGuard.RequireUnpatched(handle.Hook, Feature);
                }

                publishing = true;
                foreach (var handle in handles)
                {
                    var hook = handle.Hook;
                    hook.Enable();
                    VerifyPublishedJump(hook);
                    NativePatchGuard.Watch(hook, Feature);
                }
                intact = NativePatchGuard.CaptureIntegrity(result.Installed, Feature);

                if (!MultiplayerResyncEvents.TryRegister(ownerGuid, Feature, BeginRepair, CompleteRepair, out string reason))
                    throw new InvalidOperationException(reason);
                if (!NativeWorldRestoreEvents.TryRegister(ownerGuid, Feature, RestoreWorld, out reason))
                    throw new InvalidOperationException(reason);
                var registration = new VersionedSaveDataRegistration(ownerGuid, SaveIdentifier, 1, 1,
                    new[] { Entry }, CaptureSave, _ => { })
                {
                    Scope = VersionedSaveScope.SaveGamesAndMaps,
                    SimulationRelevant = true,
                    RequireAtomicWrite = true,
                    Prepared = PrepareLoad
                };
                if (!VersionedSaveData.TryRegister(registration, out _, out reason))
                    throw new InvalidOperationException(reason);
                if (!lifecycle.TryRegisterObserver(Feature, Start, End, Initialize, out diagnostic))
                    throw new InvalidOperationException(diagnostic?.Reason);

                ready = true;
            }
            catch (Exception error)
            {
                if (!publishing)
                {
                    transaction?.Dispose();
                    transaction = null;
                    if (state != IntPtr.Zero) Marshal.FreeHGlobal(state);
                    state = IntPtr.Zero;
                    if (claimed) NativeSiteRegistry.Unregister(ownerGuid, Feature);
                }
                // A partly published transaction remains rooted and dormant.
                DebugLogHelper.LogError(log, $"Deposit restoration initialization failed. {error}");
            }
        }

        private static void VerifyPublishedJump(X64InlineHook hook)
        {
            byte* bytes = (byte*)hook.TargetAddress;
            if (!hook.IsInstalled || bytes[0] != 0xFF || bytes[1] != 0x25 || *(uint*)(bytes + 2) != 0 ||
                *(ulong*)(bytes + 6) != unchecked((ulong)hook.StubAddress.ToInt64()))
                throw new InvalidOperationException("Unexpected installed jump for " + hook.Name);
            for (int i = 14; i < hook.DisplacedByteCount; i++)
                if (bytes[i] != 0x90) throw new InvalidOperationException("Unexpected padding for " + hook.Name);
        }

        private Span<byte> History => new Span<byte>((byte*)state + DepositHooks.HistoryOffset, DepositHooks.Capacity);

        private void Reset()
        {
            Marshal.WriteByte(state, 0);
            History.Clear();
            pendingSession = activeSession = 0;
            pending = null;
            invalidRecord = multiplayerWorld = repairing = false;
        }

        private void Initialize(MissionLifecycleNotification notification)
        {
            if (notification.Phase != MissionInitializationPhase.BeforeLoad) return;
            lock (engineGate)
            {
                Reset();
                pendingSession = notification.Context.SessionId;
            }
        }

        private void End(MissionLifecycleNotification notification)
        {
            lock (engineGate)
                if (pendingSession == notification.Context.SessionId || activeSession == notification.Context.SessionId)
                    Reset();
        }

        private bool RestoreWorld(NativeWorldRestoreContext restore)
        {
            var context = restore.Mission;
            return context == null || pendingSession != context.SessionId || Activate(context);
        }

        private void Start(MissionLifecycleNotification notification)
        {
            lock (engineGate)
            {
                var context = notification.Context;
                // Editor creation can skip raw archive restoration.
                if (!Activate(context) && context.Mode.IsRealMultiplayer && agreed)
                    SimulationAgreement.ReportFault("Wall deposit restoration could not activate in the agreed world.");
            }
        }

        private bool Activate(MissionContext context)
        {
            if (!ready || activeSession == context.SessionId) return true;
            multiplayerWorld = context.Mode.IsRealMultiplayer;
            if (multiplayerWorld && !agreed) return true;
            NativePatchGuard.Recheck();
            if (!intact() || (multiplayerWorld && !MultiplayerResyncEvents.CheckIntegrity()))
                return RefuseWorld("A required wall deposit hook is unavailable.");

            var tiles = GameTileManagerAPI.Instance;
            var logic = tiles.GetLogicLayer();
            if (unchecked((ulong)tiles.GetTileManager().ToInt64()) != tileManager || logic.Length != DepositHooks.Capacity)
                return RefuseWorld("The current map uses a different tile layout.");
            if (pendingSession == context.SessionId)
            {
                if (invalidRecord) return RefuseWorld("The saved wall deposit state could not be read.");
                if (pending != null)
                {
                    if (BitConverter.ToInt32(pending, 0) != tiles.GetCurrentMapSize())
                        return RefuseWorld("Saved wall deposits use a different map size.");
                    var history = History;
                    for (int tile = 0; tile < logic.Length; tile++)
                        if ((logic[tile] & (int)TilePropertyFlag.IsWall) != 0)
                            history[tile] = pending[tile + sizeof(int)];
                }
            }
            pending = null;
            activeSession = context.SessionId;
            Marshal.WriteByte(state, 1);
            DebugLogHelper.LogInfo(log, $"Wall deposit restoration active for mission {activeSession} ({context.Mode.Kind}).");
            return true;
        }

        private bool RefuseWorld(string reason)
        {
            DebugLogHelper.LogError(log, reason);
            return false;
        }

        private void BeginRepair()
        {
            if (!multiplayerWorld || !agreed || activeSession == 0) return;
            repairing = true;
            Marshal.WriteByte(state, 0);
        }

        private void CompleteRepair()
        {
            if (!repairing) return;
            if (!agreed || !intact() || !MultiplayerResyncEvents.CheckIntegrity())
                throw new InvalidOperationException("Wall deposit restoration lost its agreed hooks during repair.");
            // Chore 65 repairs the Vanilla grid. Retire its private history on every peer.
            History.Clear();
            repairing = false;
            Marshal.WriteByte(state, 1);
        }

        private bool CaptureSave(VersionedSaveWriter writer)
        {
            lock (engineGate)
            {
                if (repairing || invalidRecord) return false;
                if (activeSession == 0) return pending == null;
                var logic = GameTileManagerAPI.Instance.GetLogicLayer();
                var history = History;
                byte[] record = null;
                for (int tile = 0; tile < history.Length; tile++)
                {
                    if (history[tile] == 0 || (logic[tile] & (int)TilePropertyFlag.IsWall) == 0) continue;
                    if (record == null)
                    {
                        record = new byte[DepositHooks.Capacity + sizeof(int)];
                        BitConverter.GetBytes(GameTileManagerAPI.Instance.GetCurrentMapSize()).CopyTo(record, 0);
                    }
                    record[tile + sizeof(int)] = history[tile];
                }
                if (record != null) writer.Write(Entry, record);
                return true;
            }
        }

        private void PrepareLoad(VersionedSaveLoad load)
        {
            lock (engineGate)
            {
                if (pendingSession == 0 || activeSession == pendingSession) return;
                pending = null;
                if (load.Status == VersionedSaveLoadStatus.NoRecord || load.Status == VersionedSaveLoadStatus.OutOfScope) return;
                if (load.Status != VersionedSaveLoadStatus.Restored)
                {
                    invalidRecord = true;
                    return;
                }
                if (!load.Entries.TryGetValue(Entry, out var record)) return;
                if (!ValidRecord(record))
                {
                    invalidRecord = true;
                    throw new InvalidDataException("Invalid wall deposit history.");
                }
                pending = record;
            }
        }

        private static bool ValidRecord(byte[] record)
        {
            if (record.Length != DepositHooks.Capacity + sizeof(int)) return false;
            for (int i = sizeof(int); i < record.Length; i++)
                if ((record[i] & ~DepositHooks.StoredMask) != 0) return false;
            return true;
        }
    }
}
