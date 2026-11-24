using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using APIShared;
using APIShared.Multiplayer;
using APIShared.NativeSites;
using APIShared.Savegames;
using APIShared.Simulation;
using BepInEx.Bootstrap;
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

namespace BugfixesAndQoL.WallDeposits
{
    internal sealed unsafe class DepositRestorationRuntime
    {
        private const string Feature = "wall-deposit-restoration";
        // Stable across the move to a main mod, including saves from the original prototype.
        private const string SaveIdentifier = "DepositRestorationTest_Serp";
        private const string Entry = "covered-deposits";
        private const int RecordSize = sizeof(int) + DepositHooks.Capacity;
        private const string Owner = BugfixesAndQoLPlugin.PluginGuid;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private HookTransaction transaction;
        private bool attempted;
        private IntPtr state;
        private object engineGate;
        private ulong tileManager;
        private long pendingSession, activeSession;
        private byte[] pending;
        private bool invalidRecord, multiplayerWorld, repairing, repairHistoryApplied;
        private bool requiresCompatibleReader;
        private bool repairWritersSuspended;
        private int engineRunDepth;
        private bool resyncReady;
        private volatile bool ready, agreed;
        private Func<bool> intact;

        // Construct on the Unity thread before joining a lobby. Retain for the process.
        internal DepositRestorationRuntime(BugfixesAndQoLViewModel settings, ManualLogSource log)
        {
            this.settings = settings;
            this.log = log;
            // If a peer cannot install the fix, keep it inactive on all peers.
            SimulationAgreement.Register(Owner, Feature, required: false,
                isReady: () => ready && intact() && HasResyncIntegrity(),
                apply: enabled => agreed = enabled);
        }

        internal void Install(CrusaderLibraryLoadContext context)
        {
            if (attempted) return;
            attempted = true;
            // The provider list stays the same even when the option is off or a peer
            // cannot install the native hooks. Inactive participants capture an empty record.
            resyncReady = MultiplayerResyncData.TryRegister<byte[]>(Owner, Entry, 1, RecordSize,
                CaptureResync, PrepareResync, CommitResync, out string reason);
            if (!resyncReady)
                DebugLogHelper.LogError(log, "Wall deposit multiplayer resync registration failed. " + reason);

            if (Chainloader.PluginInfos.ContainsKey(SaveIdentifier))
            {
                DebugLogHelper.LogWarning(log, "The standalone Deposit Restoration plugin is installed. Remove it to use the BugfixesAndQoL option.");
                return;
            }

            if (!ApiShared.ForMod(Owner).TryGetMissionLifecycle(out var lifecycle, out var diagnostic))
                throw new InvalidOperationException(diagnostic?.Reason);
            if (!EngineRunEvents.TryRegister(Owner, Feature + "-repair-writers", () => engineRunDepth++, EndEngineRun, out reason) ||
                !MultiplayerResyncEvents.TryRegister(Owner, Feature, BeginRepair, CompleteRepair, out reason) ||
                !NativeWorldRestoreEvents.TryRegister(Owner, Feature, RestoreWorld, out reason))
                throw new InvalidOperationException(reason);
            if (!lifecycle.TryRegisterObserver(Feature, Start, End, Initialize, out diagnostic))
                throw new InvalidOperationException(diagnostic?.Reason);

            InstallHooksAndSaveData(context);
            ready = true;
            DebugLogHelper.LogInfo(log, $"Wall deposit hooks installed by reference RVA ({DepositHooks.Sites.Length} spans).");
        }

        private void InstallHooksAndSaveData(CrusaderLibraryLoadContext context)
        {
            // The library-load callback runs before any simulation code can enter these spans.
            // Only this unpublished candidate may be disposed when installation fails.
            if (!HookInstallPhase.InitialLibraryLoad || DebugLogHelper.InstalledNativeSha256() != DepositHooks.NativeHash ||
                context.ModuleHandle == IntPtr.Zero ||
                DepositHooks.Capacity != GameTileManagerView.NativePackedTileCapacity ||
                DepositHooks.Deposits != 0xA0000 || (int)TilePropertyFlag.IsWall != 0x100)
                throw new InvalidOperationException("The audited wall deposit layout or startup installation window is unavailable.");

            bool claimed = false;
            var sites = DepositHooks.Sites;
            var handles = new HookHandle<X64InlineHook>[sites.Length];
            int activationAttempts = 0;
            try
            {
                engineGate = EngineThreadLock.Gate;
                ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
                tileManager = image + DepositHooks.TileManager;
                var spans = new NativeSiteSpan[sites.Length];
                for (int index = 0; index < sites.Length; index++)
                {
                    var site = sites[index];
                    if (!context.Memory.Slice(site.Rva, site.Bytes.Length).SequenceEqual(site.Bytes))
                        throw new InvalidOperationException("Unexpected original bytes at " + site.Name);
                    NativePatchGuard.RequireUnpatched(image + (uint)site.Rva, site.Bytes.Length, site.Name, Feature);
                    spans[index] = new NativeSiteSpan(site.Rva, site.Bytes.Length, NativeSiteMode.Exclusive);
                }
                var claim = NativeSiteRegistry.Register(Owner, Feature, spans);
                if (!claim.Succeeded) throw new InvalidOperationException(claim.Reason);
                claimed = true;

                state = Marshal.AllocHGlobal(DepositHooks.HistoryOffset + DepositHooks.Capacity);
                Reset();
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region);
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

                foreach (var handle in handles)
                {
                    var hook = handle.Hook;
                    activationAttempts++;
                    hook.Enable();
                    VerifyPublishedJump(hook);
                }
                foreach (var handle in handles)
                    NativePatchGuard.Watch(handle.Hook, Feature);
                intact = NativePatchGuard.CaptureIntegrity(result.Installed, Feature);

                var registration = new VersionedSaveDataRegistration(Owner, SaveIdentifier, 1, 1,
                    new[] { Entry }, CaptureSave, _ => { })
                {
                    Scope = VersionedSaveScope.SaveGamesAndMaps,
                    SimulationRelevant = true,
                    RequireAtomicWrite = true,
                    Prepared = PrepareLoad
                };
                if (!VersionedSaveData.TryRegister(registration, out _, out string reason))
                    throw new InvalidOperationException(reason);
            }
            catch (Exception installError)
            {
                // RedBird's transaction disposal logs hook failures and continues.
                // Confirm the native spans are restored before releasing the buffer.
                try
                {
                    for (int index = activationAttempts - 1; index >= 0; index--)
                    {
                        var hook = handles[index].Hook;
                        hook.Disable();
                        var expected = sites[index].Bytes;
                        if (!new ReadOnlySpan<byte>((void*)hook.TargetAddress, expected.Length).SequenceEqual(expected))
                            throw new InvalidOperationException("Could not restore the original bytes at " + sites[index].Name);
                    }
                }
                catch (Exception rollbackError)
                {
                    // The dormant candidate and its state stay rooted if a span could not be restored.
                    throw new AggregateException("Wall deposit hook rollback failed.", installError, rollbackError);
                }
                transaction?.Dispose();
                transaction = null;
                if (state != IntPtr.Zero) Marshal.FreeHGlobal(state);
                state = IntPtr.Zero;
                if (claimed) NativeSiteRegistry.Unregister(Owner, Feature);
                throw;
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
            requiresCompatibleReader = false;
            invalidRecord = multiplayerWorld = repairing = repairHistoryApplied = repairWritersSuspended = false;
        }

        private void Initialize(MissionLifecycleNotification notification)
        {
            if (!ready || notification.Phase != MissionInitializationPhase.BeforeLoad) return;
            lock (engineGate)
            {
                Reset();
                pendingSession = notification.Context.SessionId;
            }
        }

        private void End(MissionLifecycleNotification notification)
        {
            if (!ready) return;
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
            if (!ready) return;
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
            // APIShared protects these bytes from replacement, so the atomic writer
            // cannot save this world. Refuse restoration before gameplay begins.
            if (pendingSession == context.SessionId && requiresCompatibleReader)
            {
                DebugLogHelper.LogError(log, "This file needs a compatible version of wall deposit restoration before it can be loaded and saved.");
                return false;
            }
            if (!settings.EnableMod || !settings.EnableWallDepositRestoration) return true;
            multiplayerWorld = context.Mode.IsRealMultiplayer;
            if (multiplayerWorld && !agreed)
            {
                DebugLogHelper.LogWarningLimited(log, "wall-deposits.agreement",
                    "Wall deposit restoration was not available on every player. It is disabled for this multiplayer session.");
                return true;
            }
            if (!intact() || (multiplayerWorld && !HasResyncIntegrity()))
                return SkipWorld("A required wall deposit hook is unavailable.");

            var tiles = GameTileManagerAPI.Instance;
            var logic = tiles.GetLogicLayer();
            if (unchecked((ulong)tiles.GetTileManager().ToInt64()) != tileManager || logic.Length != DepositHooks.Capacity)
                return SkipWorld("The current map uses a different wall deposit tile layout.");
            if (pendingSession == context.SessionId)
            {
                bool wrongMapSize = pending != null && BinaryPrimitives.ReadInt32LittleEndian(pending) != tiles.GetCurrentMapSize();
                if (invalidRecord || wrongMapSize)
                {
                    const string message = "The saved wall deposit history could not be restored.";
                    if (multiplayerWorld) return SkipWorld(message);
                    DebugLogHelper.LogWarning(log, message);
                }
                else if (pending != null)
                {
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

        private bool SkipWorld(string reason)
        {
            DebugLogHelper.LogError(log, reason);
            return !multiplayerWorld;
        }

        private void BeginRepair()
        {
            if (!multiplayerWorld || !agreed || activeSession == 0) return;
            repairing = true;
            repairHistoryApplied = false;
            repairWritersSuspended = false;
            if (engineRunDepth == 0) SuspendRepairWriters();
        }

        private void EndEngineRun()
        {
            // The materialized due list can contain ordinary wall Chores after Chore 54.
            if (--engineRunDepth == 0 && repairing && !repairWritersSuspended) SuspendRepairWriters();
        }

        private void SuspendRepairWriters()
        {
            Marshal.WriteByte(state, 0);
            repairWritersSuspended = true;
        }

        private void CompleteRepair()
        {
            if (!repairing) return;
            if (!agreed || !intact() || !HasResyncIntegrity())
                throw new InvalidOperationException("Wall deposit restoration lost its agreed hooks during repair.");
            if (!repairWritersSuspended || !repairHistoryApplied)
                throw new InvalidOperationException("Wall deposit repair completed without committing the host history.");
            repairing = false;
            repairWritersSuspended = false;
            Marshal.WriteByte(state, 1);
        }

        private bool HasResyncIntegrity() => resyncReady && MultiplayerResyncEvents.CheckIntegrity() &&
            MultiplayerResyncData.CheckIntegrity();

        private byte[] CaptureResync()
        {
            if (activeSession == 0 || !multiplayerWorld || !agreed) return Array.Empty<byte>();
            if (!repairing)
                throw new InvalidOperationException("Wall deposit history is unavailable for the agreed repair.");
            var record = NewRecord(GameTileManagerAPI.Instance.GetCurrentMapSize());
            // Client comparison uses the pre-repair bytes. Host authority is captured later,
            // after any remaining due-list entries have changed the history.
            History.CopyTo(record.AsSpan(sizeof(int)));
            return record;
        }

        private byte[] PrepareResync(ReadOnlyMemory<byte> snapshot)
        {
            if (snapshot.IsEmpty && activeSession == 0) return null;
            if (!repairing || !repairWritersSuspended || !agreed || !intact())
                throw new InvalidOperationException("Wall deposit history cannot be prepared outside the agreed repair.");
            var tiles = GameTileManagerAPI.Instance;
            var record = snapshot.Span;
            if (!ValidRecord(record) || BinaryPrimitives.ReadInt32LittleEndian(record) != tiles.GetCurrentMapSize())
                throw new InvalidDataException("Invalid wall deposit history for the repaired map.");
            var logic = tiles.GetLogicLayer();
            if (unchecked((ulong)tiles.GetTileManager().ToInt64()) != tileManager || logic.Length != DepositHooks.Capacity)
                throw new InvalidOperationException("The repaired map uses a different tile layout.");

            var history = new byte[DepositHooks.Capacity];
            for (int tile = 0; tile < history.Length; tile++)
                if ((logic[tile] & (int)TilePropertyFlag.IsWall) != 0)
                    history[tile] = record[tile + sizeof(int)];
            return history;
        }

        private void CommitResync(byte[] history)
        {
            if (history == null) return;
            history.AsSpan().CopyTo(History);
            repairHistoryApplied = true;
        }

        private bool CaptureSave(VersionedSaveWriter writer)
        {
            lock (engineGate)
            {
                if (repairing) return false;
                if (activeSession == 0) return true;
                var logic = GameTileManagerAPI.Instance.GetLogicLayer();
                var history = History;
                byte[] record = null;
                for (int tile = 0; tile < history.Length; tile++)
                {
                    if (history[tile] == 0 || (logic[tile] & (int)TilePropertyFlag.IsWall) == 0) continue;
                    if (record == null) record = NewRecord(GameTileManagerAPI.Instance.GetCurrentMapSize());
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
                    requiresCompatibleReader = load.Status == VersionedSaveLoadStatus.TooNew ||
                        load.Status == VersionedSaveLoadStatus.Unrecognized ||
                        load.Status == VersionedSaveLoadStatus.MigrationFailed;
                    return;
                }
                if (!load.Entries.TryGetValue(Entry, out var record)) return;
                if (!ValidRecord(record))
                {
                    invalidRecord = true;
                    return;
                }
                pending = record;
            }
        }

        private static byte[] NewRecord(int mapSize)
        {
            var record = new byte[RecordSize];
            BinaryPrimitives.WriteInt32LittleEndian(record, mapSize);
            return record;
        }

        private static bool ValidRecord(ReadOnlySpan<byte> record)
        {
            if (record.Length != RecordSize) return false;
            for (int i = sizeof(int); i < record.Length; i++)
                if ((record[i] & ~DepositHooks.StoredMask) != 0) return false;
            return true;
        }
    }
}
