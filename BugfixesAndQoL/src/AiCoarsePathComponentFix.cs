using BepInEx.Logging;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    // Keeps the two PCL-derived AIV values in the same state as Vanilla's full 0x50720 build.
    // The installed hook and its event subscriptions are process-lifetime objects.
    internal sealed unsafe class AiCoarsePathComponentFix
    {
        private enum RefreshOutcome { Completed, Deferred, Failed }
        private const int RebuildRva = 0xE49D0;
        private const int DisplacedLength = 8;
        private const int PclCount = 320800;
        private const int ComponentCapacity = 10000;
        private const int CoarseWidth = 160;
        private const int CoarseCount = CoarseWidth * CoarseWidth;
        private const int ReferenceOffset = 0x5B504;
        private const int NativeAivStateRva = 0x34A9F70;
        private const int NativePathingContextRva = 0x60AD660;
        private static readonly byte[] Prologue = { 0x40, 0x53, 0x41, 0x57, 0x48, 0x83, 0xEC, 0x58 };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RebuildDelegate(ulong pathingContext, int force);

        private readonly ManualLogSource log;
        private readonly Func<bool> isEnabled;
        private readonly ulong moduleBase;
        private readonly DetourHandle<RebuildDelegate> rebuildHook = new DetourHandle<RebuildDelegate>();
        private readonly int[] componentCounts = new int[ComponentCapacity];
        private readonly byte[] foreignCounts = new byte[CoarseCount];
        private readonly HookTransaction transaction;
        private readonly IDisposable loadSubscription;
        private readonly IDisposable startSubscription;
        private readonly IDisposable endSubscription;
        private bool mapActive;
        private bool enabledForMap;
        private bool deferredRefresh;
        private bool refreshing;
        private bool unavailable;
        private long sessionId;
        private long noRebuildCalls;
        private int successfulRebuilds;
        private int appliedRebuilds;
        private int deferredRebuilds;
        private int skippedBeforeStart;
        private int skippedDisabled;
        private int skippedUnavailable;
        private int failedRefreshes;
        private int completedRefreshes;

        internal AiCoarsePathComponentFix(ManualLogSource log, Func<bool> isEnabled,
            CrusaderLibraryLoadContext context, bool hashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!hashMatches || !string.Equals(Shared.DebugLogHelper.CurrentNativeSha256,
                "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2",
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The AI coarse-grid fix requires the audited native DLL.");
            moduleBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            if (context.Memory.Length < RebuildRva + Prologue.Length ||
                !context.Memory.Slice(RebuildRva, Prologue.Length).SequenceEqual(Prologue))
                throw new InvalidOperationException("The PCL rebuild prologue differs from the audited eight bytes.");
            if (Marshal.SizeOf(typeof(AivCoarseCell)) != 0x30 ||
                Marshal.OffsetOf(typeof(AivCoarseCell), nameof(AivCoarseCell.ForeignPathComponentTileCount)).ToInt32() != 4)
                throw new InvalidOperationException("The installed coarse-cell layout differs.");

            ulong target = moduleBase + RebuildRva;
            var request = new DetourRequest<RebuildDelegate>
            {
                Name = "BugfixesAndQoL AI coarse PCL preflight",
                TargetAddress = target,
                Callback = Rebuild
            };
            var probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<RebuildDelegate>;
            if (probe == null) throw new InvalidOperationException("The installed RedBird backend is not NativeX64.");
            using (probe)
            {
                if (probe.Scheme.ToString() != "Indirect" || probe.DisplacedByteCount != DisplacedLength ||
                    probe.TargetAddress != target || probe.HookEntryPointAddress == IntPtr.Zero ||
                    probe.PointerSlot == IntPtr.Zero)
                    throw new InvalidOperationException("The PCL rebuild probe does not have the audited indirect eight-byte contract.");
            }

            HookTransaction pending = null;
            IDisposable loading = null;
            IDisposable starting = null;
            IDisposable ending = null;
            try
            {
                pending = new HookTransaction(context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                pending.AddDetour(rebuildHook, HookTarget.FromAddress(target), Rebuild);
                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess || !rebuildHook.Success)
                    throw new InvalidOperationException("The PCL rebuild detour did not install completely: " + result);
                ValidateInstalledHook(target);

                loading = Shared.MissionEvents.Loading.Subscribe(args =>
                {
                    if (args.Phase == APIShared.MissionInitializationPhase.BeforeLoad) ResetMap();
                });
                starting = Shared.MissionEvents.Started.Subscribe(OnSessionStarted);
                ending = Shared.MissionEvents.Ended.Subscribe(_ =>
                {
                    LogSessionSummary("ended");
                    ResetMap();
                });
                transaction = pending;
                loadSubscription = loading;
                startSubscription = starting;
                endSubscription = ending;
                pending = null;
            }
            catch
            {
                ending?.Dispose();
                starting?.Dispose();
                loading?.Dispose();
                pending?.Dispose(); // Only an unpublished installation candidate is rolled back.
                throw;
            }
            Shared.DebugLogHelper.LogInfo(log,
                "AI_COARSE_PCL_FIX_READY: publisher=native 0xE49D0; scheme=Indirect; displaced=8; " +
                "mapBootstrap=MissionEvents.Started.");
        }

        private void ValidateInstalledHook(ulong expectedTarget)
        {
            var detour = rebuildHook.Hook as NativeDetour<RebuildDelegate>;
            if (detour == null || !detour.IsInstalled || detour.TargetAddress != expectedTarget ||
                detour.Scheme.ToString() != "Indirect" || detour.DisplacedByteCount != DisplacedLength ||
                detour.ChainDepth != 1 || detour.TrampolineAddress == IntPtr.Zero ||
                detour.OriginalEntryPointAddress == IntPtr.Zero || detour.HookEntryPointAddress == IntPtr.Zero ||
                detour.PointerSlot == IntPtr.Zero)
                throw new InvalidOperationException("The installed PCL rebuild detour contract differs.");
            IntPtr entry = new IntPtr(unchecked((long)expectedTarget));
            if (Marshal.ReadByte(entry, 0) != 0xFF || Marshal.ReadByte(entry, 1) != 0x25 ||
                Marshal.ReadByte(entry, 6) != 0x90 || Marshal.ReadByte(entry, 7) != 0x90 ||
                IntPtr.Add(entry, 6 + Marshal.ReadInt32(entry, 2)) != detour.PointerSlot ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("The installed PCL rebuild pointer-slot patch differs.");
        }

        private int Rebuild(ulong pathingContext, int force)
        {
            int rebuilt = rebuildHook.Original(pathingContext, force);
            if (rebuilt != 1)
            {
                if (mapActive) noRebuildCalls++;
                return rebuilt;
            }
            try
            {
                if (pathingContext != moduleBase + NativePathingContextRva)
                    throw new InvalidOperationException("PCL rebuild received an unexpected native pathing context.");
                BugfixesAndQoLRuntime.NotifyAiPathComponentGridRebuilt();
                successfulRebuilds++;
                if (mapActive && enabledForMap && !unavailable)
                {
                    switch (RefreshOrDefer("native-rebuild"))
                    {
                        case RefreshOutcome.Completed: appliedRebuilds++; break;
                        case RefreshOutcome.Deferred: deferredRebuilds++; break;
                        default: failedRefreshes++; break;
                    }
                }
                else if (!mapActive) skippedBeforeStart++;
                else if (!enabledForMap) skippedDisabled++;
                else skippedUnavailable++;
            }
            catch (Exception ex)
            {
                unavailable = true;
                Shared.DebugLogHelper.LogError(log, "AI_COARSE_PCL_FIX_DISABLED: " + ex);
            }
            return rebuilt;
        }

        private void ResetMap()
        {
            mapActive = false;
            enabledForMap = false;
            deferredRefresh = false;
            sessionId = 0;
            noRebuildCalls = 0;
            successfulRebuilds = 0;
            appliedRebuilds = 0;
            deferredRebuilds = 0;
            skippedBeforeStart = 0;
            skippedDisabled = 0;
            skippedUnavailable = 0;
            failedRefreshes = 0;
            completedRefreshes = 0;
        }

        private void OnSessionStarted(APIShared.MissionLifecycleNotification args)
        {
            if (mapActive && sessionId == args.Context.SessionId) return;
            sessionId = args.Context.SessionId;
            enabledForMap = isEnabled();
            mapActive = true;
            Shared.DebugLogHelper.LogInfo(log,
                $"AI_COARSE_PCL_SESSION: session={sessionId}; start={args.Context.StartKind}; " +
                $"mode={args.Context.Mode.Kind}; enabled={enabledForMap}; available={!unavailable}; " +
                $"preStartRebuilds={skippedBeforeStart}; replay={args.IsReplay}.");
            if (enabledForMap && !unavailable) RefreshOrDefer("session-start");
            else Shared.DebugLogHelper.LogInfo(log,
                $"AI_COARSE_PCL_REFRESH: session={sessionId}; reason=session-start; " +
                $"status={(enabledForMap ? "skipped-unavailable" : "skipped-disabled")}.");
        }

        private RefreshOutcome RefreshOrDefer(string reason)
        {
            if (BugfixesAndQoLRuntime.HasActiveAiEconomyOverlay)
            {
                deferredRefresh = true;
                if (reason == "session-start")
                    Shared.DebugLogHelper.LogInfo(log,
                        $"AI_COARSE_PCL_REFRESH: session={sessionId}; reason={reason}; status=deferred-overlay.");
                return RefreshOutcome.Deferred;
            }
            if (refreshing)
            {
                unavailable = true;
                Shared.DebugLogHelper.LogError(log, "AI_COARSE_PCL_FIX_DISABLED: nested refresh.");
                return RefreshOutcome.Failed;
            }
            try
            {
                refreshing = true;
                Refresh(reason);
                return RefreshOutcome.Completed;
            }
            catch (Exception ex)
            {
                unavailable = true;
                Shared.DebugLogHelper.LogError(log, "AI_COARSE_PCL_FIX_DISABLED: " + ex);
                return RefreshOutcome.Failed;
            }
            finally
            {
                refreshing = false;
            }
        }

        internal void FlushDeferred()
        {
            if (!deferredRefresh || !mapActive || !enabledForMap || unavailable) return;
            deferredRefresh = false;
            RefreshOrDefer("overlay-restored");
        }

        private void LogSessionSummary(string reason)
        {
            if (!mapActive) return;
            Shared.DebugLogHelper.LogInfo(log,
                $"AI_COARSE_PCL_SUMMARY: session={sessionId}; reason={reason}; enabled={enabledForMap}; " +
                $"noRebuildCalls={noRebuildCalls}; successfulRebuilds={successfulRebuilds}; " +
                $"appliedRebuilds={appliedRebuilds}; deferredRebuilds={deferredRebuilds}; " +
                $"skippedBeforeStart={skippedBeforeStart}; skippedDisabled={skippedDisabled}; " +
                $"skippedUnavailable={skippedUnavailable}; failedRefreshes={failedRefreshes}; " +
                $"completedRefreshes={completedRefreshes}; " +
                $"pendingOverlayRefresh={deferredRefresh}; available={!unavailable}.");
        }

        private void Refresh(string reason)
        {
            Span<ushort> pcl = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            Span<AivCoarseCell> coarse = GameAIVManagerAPI.Instance.GetCoarseGrid();
            AivSystem* state = GameAIVManagerAPI.Instance.GetAIVSystemPointer();
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (state == null || (ulong)state != moduleBase + NativeAivStateRva ||
                pcl.Length != PclCount || coarse.Length != CoarseCount ||
                tiles.MapColumnLookupTable == null || tiles.MapRowLookupTable == null)
                throw new InvalidOperationException("The live native PCL/coarse views are incomplete.");

            Array.Clear(componentCounts, 0, componentCounts.Length);
            Array.Clear(foreignCounts, 0, foreignCounts.Length);
            ushort* column = tiles.MapColumnLookupTable;
            int* row = tiles.MapRowLookupTable;
            for (int tile = 0; tile < pcl.Length; tile++)
            {
                int component = pcl[tile];
                if (component >= ComponentCapacity)
                    throw new InvalidOperationException("A native path component exceeds Vanilla's 10000-slot reference table.");
                if (component > 0) componentCounts[component]++;
            }

            int reference = 0;
            int largest = 0;
            for (int component = 1; component < componentCounts.Length; component++)
            {
                if (componentCounts[component] <= largest) continue;
                largest = componentCounts[component];
                reference = component;
            }
            if (reference != 0)
            {
                for (int tile = 0; tile < pcl.Length; tile++)
                {
                    int y = column[tile];
                    if ((uint)y >= 800) throw new InvalidOperationException("Packed tile Y exceeds Vanilla map bounds.");
                    int x = tile - row[3 * y];
                    if ((uint)x >= 800) throw new InvalidOperationException("Packed tile X exceeds Vanilla map bounds.");
                    if (pcl[tile] == reference) continue;
                    int index = (x / 5) * CoarseWidth + y / 5;
                    if (foreignCounts[index] == 25)
                        throw new InvalidOperationException("A coarse cell contains more than 25 foreign tiles.");
                    foreignCounts[index]++;
                }
            }

            int* nativeReference = (int*)((byte*)state + ReferenceOffset);
            bool changedReference = *nativeReference != reference;
            int changedCells = 0;
            for (int index = 0; index < coarse.Length; index++)
                if (coarse[index].ForeignPathComponentTileCount != foreignCounts[index]) changedCells++;
            if (changedReference) *nativeReference = reference;
            if (changedCells != 0)
                for (int index = 0; index < coarse.Length; index++)
                    if (coarse[index].ForeignPathComponentTileCount != foreignCounts[index])
                        coarse[index].ForeignPathComponentTileCount = foreignCounts[index];
            completedRefreshes++;
            if (reason == "session-start" || completedRefreshes <= 3 ||
                ((changedReference || changedCells != 0) && completedRefreshes <= 8))
                Shared.DebugLogHelper.LogInfo(log,
                    $"AI_COARSE_PCL_REFRESH: session={sessionId}; reason={reason}; status=completed; " +
                    $"reference={reference}; referenceChanged={changedReference}; changedCells={changedCells}.");
        }
    }
}
