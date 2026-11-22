using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using APIShared;
using APIShared.GameModes;
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

namespace DepositRestorationTest
{
    [BepInPlugin(Guid, Feature, "0.1.0")]
    [BepInDependency("000shcdese", "2.14.4")]
    [BepInDependency("APIShared_Serp", "0.62.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed unsafe class Plugin : BaseUnityPlugin
    {
        private const string Guid = "DepositRestorationTest_Serp";
        private const string Feature = "Deposit Restoration Test";
        private const string Entry = "covered-deposits";
        private static ManualLogSource log;
        private static HookTransaction transaction;
        private static IntPtr state;
        private static object engineGate;
        private static ulong tileManager;
        private static long session;
        private static byte[] pending;
        private static Func<bool> intact;

        private void Awake()
        {
            log = Logger;
            CrusaderLibrary.Instance.LibraryLoaded += Install;
        }

        private static void Install(CrusaderLibraryLoadContext context)
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

                if (!ApiShared.ForMod(Guid).TryGetMissionLifecycle(out var lifecycle, out var diagnostic))
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
                var claim = NativeSiteRegistry.Register(Guid, Feature, spans);
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

                var registration = new VersionedSaveDataRegistration(Guid, Guid, 1, 1,
                    new[] { Entry }, CaptureSave, StageLoad)
                {
                    Scope = VersionedSaveScope.SaveGamesAndMaps,
                    SimulationRelevant = true
                };
                if (!VersionedSaveData.TryRegister(registration, out _, out string reason))
                    throw new InvalidOperationException(reason);
                if (!lifecycle.TryRegisterObserver("deposit-history", Start, End, Initialize, out diagnostic))
                    throw new InvalidOperationException(diagnostic?.Reason);

                DebugLogHelper.LogInfo(log, $"DEPOSIT_RESTORATION_READY hooks={sites.Length}");
            }
            catch (Exception error)
            {
                if (!publishing)
                {
                    transaction?.Dispose();
                    transaction = null;
                    if (state != IntPtr.Zero) Marshal.FreeHGlobal(state);
                    state = IntPtr.Zero;
                    if (claimed) NativeSiteRegistry.Unregister(Guid, Feature);
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

        private static Span<byte> History => new Span<byte>((byte*)state + DepositHooks.HistoryOffset, DepositHooks.Capacity);

        private static void Reset()
        {
            Marshal.WriteByte(state, 0);
            History.Clear();
            session = 0;
            pending = null;
        }

        private static void Initialize(MissionLifecycleNotification notification)
        {
            if (notification.Phase != MissionInitializationPhase.BeforeLoad) return;
            lock (engineGate) Reset();
        }

        private static void End(MissionLifecycleNotification notification)
        {
            lock (engineGate) Reset();
        }

        private static void Start(MissionLifecycleNotification notification)
        {
            lock (engineGate)
            {
                var context = notification.Context;
                if (session == context.SessionId) return;
                session = context.SessionId;
                NativePatchGuard.Recheck();
                var tiles = GameTileManagerAPI.Instance;
                var logic = tiles.GetLogicLayer();
                bool enabled = intact() && !context.Mode.IsRealMultiplayer &&
                    (context.IsEditor || context.Mode.Kind != GameModeKind.Unknown) &&
                    unchecked((ulong)tiles.GetTileManager().ToInt64()) == tileManager &&
                    logic.Length == DepositHooks.Capacity;
                if (enabled && pending != null)
                {
                    int mapSize = tiles.GetCurrentMapSize();
                    if (BitConverter.ToInt32(pending, 0) == mapSize)
                    {
                        var history = History;
                        for (int tile = 0; tile < logic.Length; tile++)
                            if ((logic[tile] & (int)TilePropertyFlag.IsWall) != 0)
                                history[tile] = pending[tile + sizeof(int)];
                    }
                    else DebugLogHelper.LogError(log, "Saved deposit history uses a different map size.");
                }
                pending = null;
                Marshal.WriteByte(state, enabled ? (byte)1 : (byte)0);
                DebugLogHelper.LogInfo(log, $"DEPOSIT_RESTORATION_SESSION id={session} active={enabled} editor={context.IsEditor}");
            }
        }

        private static bool CaptureSave(VersionedSaveWriter writer)
        {
            lock (engineGate)
            {
                if (Marshal.ReadByte(state) == 0) return false;
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

        private static void StageLoad(VersionedSaveLoad load)
        {
            lock (engineGate)
            {
                pending = null;
                if (load.Status != VersionedSaveLoadStatus.Restored || !load.Entries.TryGetValue(Entry, out var record)) return;
                if (record.Length != DepositHooks.Capacity + sizeof(int))
                    throw new InvalidDataException("Unexpected deposit history length.");
                for (int i = sizeof(int); i < record.Length; i++)
                    if ((record[i] & ~DepositHooks.StoredMask) != 0)
                        throw new InvalidDataException("Invalid resource flags in deposit history.");
                pending = record;
            }
        }
    }
}
