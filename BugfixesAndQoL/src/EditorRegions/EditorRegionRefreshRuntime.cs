using APIShared;
using APIShared.NativeSites;
using BepInEx.Logging;
using HarmonyLib;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace BugfixesAndQoL.EditorRegions
{
    // Static runtime, permanent Harmony patch and publisher subscriptions survive
    // SHCDE's startup destruction of the BaseUnityPlugin component.
    internal sealed class EditorRegionRefreshRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int RebuildDelegate(IntPtr context, int force);
        private static EditorRegionRefreshRuntime instance;
        private readonly RefreshState state = new RefreshState();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Harmony harmony = new Harmony("BugfixesAndQoL_Serp.EditorRegions");
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly object engineGate;
        private readonly GamePathingManagerAPI pathing;
        private readonly RebuildDelegate rebuild;
        private bool failed, executionReported;

        private EditorRegionRefreshRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings,
            object engineGate, GamePathingManagerAPI pathing, RebuildDelegate rebuild)
        {
            this.log = log; this.settings = settings; this.engineGate = engineGate;
            this.pathing = pathing; this.rebuild = rebuild;
        }

        internal static void Install(CrusaderLibraryLoadContext context, ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            if (instance != null) return;
            var before = EngineBoundary.ValidateManagedBoundary(out var gateField);
            var input = EngineBoundary.ValidateInputBoundary();
            object gate = gateField.GetValue(null) ?? throw new InvalidOperationException("Engine lock is null.");
            var resolved = BfqolNativeCache.Current.ResolveUnique(context.Memory, NativeContract.RebuildPattern,
                NativeContract.RebuildRva, "editor-region-rebuild", log);
            var claim = NativeSiteRegistry.Register(BugfixesAndQoLPlugin.PluginGuid, "EditorRegionRefresh",
                new[] { new NativeSiteSpan(resolved.Rva, 1, NativeSiteMode.CallOnly) });
            if (!claim.Succeeded) throw new InvalidOperationException(claim.Reason);
            var api = GamePathingManagerAPI.Instance;
            if (api.GetPathfindingContextView().Address == IntPtr.Zero)
                throw new InvalidOperationException("Pathfinding context unavailable.");
            var target = IntPtr.Add(context.ModuleHandle, resolved.Rva);
            var runtime = new EditorRegionRefreshRuntime(log, settings, gate, api,
                (RebuildDelegate)Marshal.GetDelegateForFunctionPointer(target, typeof(RebuildDelegate)));
            instance = runtime; // All later failures retain the published patch and its disabled runtime.
            try
            {
                runtime.harmony.Patch(before, postfix: new HarmonyMethod(typeof(EditorRegionRefreshRuntime), nameof(BeforeNativeCursor)));
                runtime.harmony.Patch(input, prefix: new HarmonyMethod(typeof(EditorRegionRefreshRuntime), nameof(CapturePlacementInput)));
                UnityEngine.Application.focusChanged += FocusChanged;
                runtime.Subscribe(MapEditorR3EventHooks.OnSetTileType);
                runtime.Subscribe(MapEditorR3EventHooks.OnSetTileTypeToNone);
                runtime.Subscribe(MapEditorR3EventHooks.OnSetTileHeightToPreset);
                runtime.Subscribe(MapEditorR3EventHooks.OnSetTileHeightToNumber);
                runtime.Subscribe(MapEditorR3EventHooks.OnRaiseTileHeight);
                runtime.Subscribe(MapEditorR3EventHooks.OnEvenTileHeight);
                runtime.Subscribe(MapEditorR3EventHooks.OnDelete);
                runtime.Subscribe(MapEditorR3EventHooks.OnVegetationPlace);
                runtime.SubscribeLifecycle();
                settings.SettingChanged += runtime.SettingChanged;
                Shared.DevDiag.Attach(log);
                Shared.DebugLogHelper.LogInfo(log, "EDITOR_REGIONS_READY: editor edits batched until mouse release; live native entry; locked pre-cursor refresh; no startup rebuild.");
            }
            catch (Exception error) { runtime.Fail(error); }
        }

        private void SubscribeLifecycle()
        {
            subscriptions.Add(Shared.MissionEvents.Started.Subscribe(Started));
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(Ended));
        }

        private void Subscribe<T>(R3EventHook<T> hook) where T : EventHookBase =>
            subscriptions.Add(hook.Observable.Subscribe(Edited));

        private void Started(MissionLifecycleNotification notification)
        {
            lock (engineGate)
            {
                state.Start(notification.Context.SessionId, notification.Context.IsEditor, settings.EnableMod && !failed);
                executionReported = false;
            }
        }

        private void Ended(MissionLifecycleNotification notification)
        { lock (engineGate) state.End(notification.Context.SessionId); }

        private void SettingChanged(string name)
        {
            if (name == nameof(BugfixesAndQoLViewModel.EnableMod))
                lock (engineGate) state.SetEnabled(settings.EnableMod && !failed);
        }

        private void Edited(EventHookBase args)
        {
            if (args.Phase != EventHookPhase.Post || failed) return;
            try
            {
                lock (engineGate)
                {
                    if (!state.Active) return;
                    state.Changed(pathing.GetComponentGeneration());
                    Shared.DevDiag.Count("editor.regions.edits");
                }
            }
            catch (Exception error) { Fail(error); }
        }

        private static void CapturePlacementInput()
        {
            var runtime = instance;
            int ticket = runtime?.state.PendingInputTicket ?? 0;
            if (ticket == 0) return;
            try
            {
                // SE key events use a separate, UI-filtered state machine. Read
                // physical input only on Vanilla's main-thread input path and
                // only while an actual edit is waiting, never on the engine thread.
                bool held = UnityEngine.Application.isFocused && UnityEngine.Input.GetMouseButton(0);
                runtime.state.ObservePlacementButton(ticket, held);
                Shared.DevDiag.Count("editor.regions.inputSamples");
            }
            catch (Exception error) { runtime.Fail(error); }
        }

        private static void FocusChanged(bool focused)
        {
            var runtime = instance;
            int ticket = runtime?.state.PendingInputTicket ?? 0;
            if (ticket == 0) return;
            if (focused) runtime.state.InvalidatePlacementInput();
            else runtime.state.ObservePlacementButton(ticket, false);
        }

        private static void BeforeNativeCursor()
        {
            var runtime = instance;
            if (runtime == null || !runtime.state.HasPending) return;
            if (!runtime.state.CanFlush) { Shared.DevDiag.Count("editor.regions.deferredPasses"); return; }
            try { runtime.Flush(); }
            catch (Exception error) { runtime.Fail(error); }
        }

        private void Flush()
        {
            // A preparation call from any other context cannot mutate global path data.
            if (!Monitor.IsEntered(engineGate)) throw new InvalidOperationException("Editor refresh lacks the Vanilla engine lock.");
            if (!state.CanFlush) return;
            var view = pathing.GetPathfindingContextView();
            var decision = state.BeginFlush(view.ComponentGeneration, view.ComponentGridDirty != 0);
            if (decision == RefreshDecision.AlreadyRebuilt) { Shared.DevDiag.Count("editor.regions.vanillaCompleted"); return; }
            if (decision != RefreshDecision.Rebuild) return;
            long started = 0;
            Shared.DevDiag.Start(ref started);
            try
            {
                pathing.RequestComponentRebuild();
                uint generation = view.ComponentGeneration;
                if (rebuild(view.Address, 1) != 1 || view.ComponentGeneration == generation)
                    throw new InvalidOperationException("Forced region rebuild did not publish a new generation.");
                if (!executionReported)
                {
                    executionReported = true;
                    Shared.DebugLogHelper.LogInfo(log, "EDITOR_REGIONS_EXECUTED: changed terrain regions rebuilt before native cursor processing.");
                }
            }
            finally
            {
                state.CompleteFlush();
                Shared.DevDiag.StopSpike(ref started, "editor.regions.rebuild", 10000);
            }
        }

        private void Fail(Exception error)
        {
            lock (engineGate) { if (failed) return; failed = true; state.SetEnabled(false); }
            Shared.DebugLogHelper.LogError(log, "EDITOR_REGIONS_DISABLED: editor region correction unavailable; Vanilla continues: " + error);
        }
    }
}
