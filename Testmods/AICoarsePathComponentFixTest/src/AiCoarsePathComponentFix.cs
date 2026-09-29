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
using System.IO;
using System.Runtime.InteropServices;

namespace AICoarsePathComponentFixTest
{
    // Observes the hypothetical full 0x50720 values without changing Vanilla's AIV grid.
    // The installed hook and its event subscriptions are process-lifetime objects.
    internal sealed unsafe class AiCoarsePathComponentFix
    {
        private enum RefreshOutcome { Completed, Deferred, Failed }
        private const int RebuildRva = 0xE49D0;
        private const string IsolationSaveName = "test_canari_nowoodcutters_probe.sav";
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
        private readonly int isolationMode;
        private readonly ulong moduleBase;
        private readonly CrusaderLibraryLoadContext loadContext;
        private readonly DetourHandle<RebuildDelegate> rebuildHook = new DetourHandle<RebuildDelegate>();
        private readonly int[] componentCounts = new int[ComponentCapacity];
        private readonly byte[] foreignCounts = new byte[CoarseCount];
        private HookTransaction transaction;
        private readonly IDisposable loadSubscription;
        private readonly IDisposable startSubscription;
        private readonly IDisposable endSubscription;
        private int lastPathGeneration;
        private bool generationKnown;
        private bool mapActive;
        private bool isolationArmedForMap;
        private bool enabledForMap;
        private bool deferredRefresh;
        private bool initialRefreshPending;
        private bool refreshing;
        private bool unavailable;
        private long sessionId;
        private long noRebuildCalls;
        private int successfulRebuilds;
        private int auditedRebuilds;
        private int deferredRebuilds;
        private int skippedBeforeStart;
        private int skippedDisabled;
        private int skippedUnavailable;
        private int failedRefreshes;
        private int completedRefreshes;

        internal AiCoarsePathComponentFix(ManualLogSource log, Func<bool> isEnabled,
            CrusaderLibraryLoadContext context, bool hashMatches, int isolationMode)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
            if (isolationMode < 0 || isolationMode > 5)
                throw new ArgumentOutOfRangeException(nameof(isolationMode));
            this.isolationMode = isolationMode;
            if (context == null) throw new ArgumentNullException(nameof(context));
            loadContext = context;
            if (!hashMatches || !string.Equals(Shared.DebugLogHelper.CurrentNativeSha256,
                "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2",
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The AI coarse-grid audit requires the audited native DLL.");
            moduleBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            if (context.Memory.Length < RebuildRva + Prologue.Length ||
                !context.Memory.Slice(RebuildRva, Prologue.Length).SequenceEqual(Prologue))
                throw new InvalidOperationException("The PCL rebuild prologue differs from the audited eight bytes.");
            if (Marshal.SizeOf(typeof(AivCoarseCell)) != 0x30 ||
                Marshal.OffsetOf(typeof(AivCoarseCell), nameof(AivCoarseCell.ForeignPathComponentTileCount)).ToInt32() != 4)
                throw new InvalidOperationException("The installed coarse-cell layout differs.");

            ulong target = moduleBase + RebuildRva;
            if (isolationMode >= 1 && isolationMode <= 4)
            {
                var request = new DetourRequest<RebuildDelegate>
                {
                    Name = "AICoarsePathComponentFixTest isolation preflight",
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
            }

            IDisposable loading = null;
            IDisposable starting = null;
            IDisposable ending = null;
            try
            {
                loading = Shared.MissionEvents.Loading.Subscribe(args =>
                {
                    if (args.Phase != APIShared.MissionInitializationPhase.BeforeLoad) return;
                    ResetMap();
                    ArmIsolationForSave(args.Context);
                });
                starting = Shared.MissionEvents.Started.Subscribe(OnSessionStarted);
                ending = Shared.MissionEvents.Ended.Subscribe(_ =>
                {
                    LogSessionSummary("ended");
                    ResetMap();
                });
                loadSubscription = loading;
                startSubscription = starting;
                endSubscription = ending;
            }
            catch
            {
                ending?.Dispose();
                starting?.Dispose();
                loading?.Dispose();
                throw;
            }
            Shared.DebugLogHelper.LogInfo(log,
                $"AI_COARSE_PCL_AUDIT_READY: publisher={(isolationMode == 0 || isolationMode == 5 ? "GameTimeManagerAPI.OnTick" : "native 0xE49D0")}; " +
                $"isolationMode={isolationMode}; hookInstalled={transaction != null}; " +
                "mapBootstrap=MissionEvents.Started; observationOnly=True; writes=0.");
        }

        private void ArmIsolationForSave(APIShared.MissionContext context)
        {
            if (isolationMode < 1 || isolationMode > 4 || context == null || !context.IsSave ||
                !string.Equals(Path.GetFileName(context.FilePath ?? ""), IsolationSaveName,
                    StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                InstallIsolationHook();
                isolationArmedForMap = true;
                Shared.DebugLogHelper.LogInfo(log,
                    $"AI_COARSE_PCL_ISOLATION_ARMED: save={IsolationSaveName}; mode={isolationMode}; hookInstalled=True.");
            }
            catch (Exception ex)
            {
                unavailable = true;
                Shared.DebugLogHelper.LogError(log, "AI_COARSE_PCL_ISOLATION_UNAVAILABLE: " + ex);
            }
        }

        private void InstallIsolationHook()
        {
            if (transaction != null) return;
            HookTransaction pending = null;
            try
            {
                pending = new HookTransaction(loadContext.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                ulong target = moduleBase + RebuildRva;
                pending.AddDetour(rebuildHook, HookTarget.FromAddress(target), Rebuild);
                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess || !rebuildHook.Success)
                    throw new InvalidOperationException("The PCL rebuild detour did not install completely: " + result);
                ValidateInstalledHook(target);
                transaction = pending;
                pending = null;
            }
            finally { pending?.Dispose(); } // Only a failed, unpublished installation candidate is rolled back.
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
            if (!isolationArmedForMap) return rebuilt;
            if (isolationMode == 1) return rebuilt;
            if (rebuilt != 1)
            {
                if (mapActive) noRebuildCalls++;
                return rebuilt;
            }
            try
            {
                if (pathingContext != moduleBase + NativePathingContextRva)
                    throw new InvalidOperationException("PCL rebuild received an unexpected native pathing context.");
                if (isolationMode >= 2)
                    BugfixesAndQoL.BugfixesAndQoLRuntime.NotifyAiPathComponentGridRebuilt();
                successfulRebuilds++;
                if (isolationMode >= 3 && mapActive && enabledForMap && !unavailable)
                {
                    switch (RefreshOrDefer("native-rebuild", isolationMode >= 4))
                    {
                        case RefreshOutcome.Completed: auditedRebuilds++; break;
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
                Shared.DebugLogHelper.LogError(log, "AI_COARSE_PCL_AUDIT_UNAVAILABLE: " + ex);
            }
            return rebuilt;
        }

        private void ResetMap()
        {
            mapActive = false;
            isolationArmedForMap = false;
            enabledForMap = false;
            deferredRefresh = false;
            initialRefreshPending = false;
            sessionId = 0;
            noRebuildCalls = 0;
            successfulRebuilds = 0;
            auditedRebuilds = 0;
            deferredRebuilds = 0;
            skippedBeforeStart = 0;
            skippedDisabled = 0;
            skippedUnavailable = 0;
            failedRefreshes = 0;
            completedRefreshes = 0;
            generationKnown = false;
        }

        private void OnSessionStarted(APIShared.MissionLifecycleNotification args)
        {
            if (mapActive && sessionId == args.Context.SessionId) return;
            sessionId = args.Context.SessionId;
            enabledForMap = isEnabled();
            mapActive = true;
            lastPathGeneration = ReadPathGeneration();
            generationKnown = true;
            initialRefreshPending = isolationMode == 5;
            Shared.DebugLogHelper.LogInfo(log,
                $"AI_COARSE_PCL_SESSION: session={sessionId}; start={args.Context.StartKind}; " +
                $"mode={args.Context.Mode.Kind}; auditEnabled={enabledForMap}; available={!unavailable}; " +
                $"isolationMode={isolationMode}; isolationArmed={isolationArmedForMap}; " +
                $"hookInstalled={transaction != null}; pathGeneration={lastPathGeneration}; observationOnly=True; writes=0; " +
                $"preStartRebuilds={skippedBeforeStart}; replay={args.IsReplay}.");
            // AIBuildDiagnoseTest: observer-only session status, before the first AI build step.
            APIShared.AiBuildDiagnostic.Publish("coarse-audit-session", 0,
                0, unavailable ? 1 : 0, sessionId);
            if (enabledForMap && !unavailable && isolationArmedForMap &&
                isolationMode >= 3 && isolationMode <= 4)
                RefreshOrDefer("session-start", isolationMode >= 4);
            else Shared.DebugLogHelper.LogInfo(log,
                $"AI_COARSE_PCL_REFRESH: session={sessionId}; reason=session-start; " +
                $"status={(!enabledForMap ? "skipped-disabled" : unavailable ? "skipped-unavailable" : isolationMode == 5 ? "deferred-first-tick" : "isolation-no-scan")}.");
        }

        internal void OnTick(int tick)
        {
            if (!mapActive || !enabledForMap || unavailable) return;
            int generation = ReadPathGeneration();
            if (!generationKnown)
            {
                lastPathGeneration = generation;
                generationKnown = true;
                return;
            }
            bool changed = generation != lastPathGeneration;
            if (changed)
            {
                int previous = lastPathGeneration;
                lastPathGeneration = generation;
                Shared.DebugLogHelper.LogInfo(log,
                    $"AI_COARSE_PCL_GENERATION: session={sessionId}; tick={tick}; previous={previous}; current={generation}; mode={isolationMode}.");
                APIShared.AiBuildDiagnostic.Publish("coarse-generation", 0, previous, generation, tick, sessionId);
            }
            if (isolationMode == 5 && (changed || initialRefreshPending || deferredRefresh))
            {
                string reason = initialRefreshPending ? "first-tick" :
                    deferredRefresh ? "deferred-tick" : "tick-rebuild";
                initialRefreshPending = false;
                deferredRefresh = false;
                RefreshOrDefer(reason, true);
            }
        }

        private int ReadPathGeneration() =>
            *(int*)(moduleBase + NativePathingContextRva + 0x74);

        private RefreshOutcome RefreshOrDefer(string reason, bool publish)
        {
            if (BugfixesAndQoL.BugfixesAndQoLRuntime.HasActiveAiEconomyOverlay)
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
                Shared.DebugLogHelper.LogError(log, "AI_COARSE_PCL_AUDIT_UNAVAILABLE: nested refresh.");
                return RefreshOutcome.Failed;
            }
            try
            {
                refreshing = true;
                Refresh(reason, publish);
                return RefreshOutcome.Completed;
            }
            catch (Exception ex)
            {
                unavailable = true;
                Shared.DebugLogHelper.LogError(log, "AI_COARSE_PCL_AUDIT_UNAVAILABLE: " + ex);
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
            if (isolationMode == 5) return; // Tick-audit mode only scans from the next persistent game tick.
            deferredRefresh = false;
            RefreshOrDefer("overlay-restored", isolationMode == 0 || isolationMode >= 4);
        }

        private void LogSessionSummary(string reason)
        {
            if (!mapActive) return;
            Shared.DebugLogHelper.LogInfo(log,
                $"AI_COARSE_PCL_SUMMARY: session={sessionId}; reason={reason}; auditEnabled={enabledForMap}; " +
                $"noRebuildCalls={noRebuildCalls}; successfulRebuilds={successfulRebuilds}; " +
                $"auditedRebuilds={auditedRebuilds}; deferredRebuilds={deferredRebuilds}; " +
                $"skippedBeforeStart={skippedBeforeStart}; skippedDisabled={skippedDisabled}; " +
                $"skippedUnavailable={skippedUnavailable}; failedRefreshes={failedRefreshes}; " +
                $"completedRefreshes={completedRefreshes}; " +
                $"pendingOverlayRefresh={deferredRefresh}; available={!unavailable}; " +
                "observationOnly=True; writes=0.");
        }

        private void Refresh(string reason, bool publish)
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
            int storedReference = *nativeReference;
            bool hypotheticalReferenceChange = storedReference != reference;
            int hypotheticalCellChanges = 0;
            for (int index = 0; index < coarse.Length; index++)
                if (coarse[index].ForeignPathComponentTileCount != foreignCounts[index]) hypotheticalCellChanges++;
            if (publish && APIShared.AiBuildDiagnostic.HasObserver)
            {
                APIShared.AiBuildDiagnostic.Publish("coarse-audit-begin", 0, sessionId);
                for (int index = 0; index < coarse.Length; index++)
                    if (coarse[index].ForeignPathComponentTileCount != foreignCounts[index])
                        APIShared.AiBuildDiagnostic.Publish("coarse-audit-cell", 0,
                            index, coarse[index].ForeignPathComponentTileCount,
                            foreignCounts[index], sessionId);
                APIShared.AiBuildDiagnostic.Publish("coarse-audit-summary", 0,
                    reference, storedReference, hypotheticalCellChanges, sessionId);
            }
            completedRefreshes++;
            if (reason == "session-start" || completedRefreshes <= 3 ||
                ((hypotheticalReferenceChange || hypotheticalCellChanges != 0) && completedRefreshes <= 8))
                Shared.DebugLogHelper.LogInfo(log,
                    $"AI_COARSE_PCL_AUDIT: session={sessionId}; reason={reason}; status=completed; " +
                    $"storedReference={storedReference}; hypotheticalReference={reference}; " +
                    $"hypotheticalReferenceChange={hypotheticalReferenceChange}; " +
                    $"hypotheticalCellChanges={hypotheticalCellChanges}; observationOnly=True; writes=0.");
        }
    }
}
