using APIShared.GameModes;
using Shared;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Input;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace CastlePlanner
{
    internal sealed class BlueprintRuntimeController
    {
        private delegate void CameraUpdateDelegate(CameraControls2D self);
        private delegate void EditorPlayerActionDelegate(EditorDirector self, int playerId);

        private const float ViewSettleDelaySeconds = 0.5f;
        private static readonly KeyCode[] CaptureMouseButtons =
        {
            KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2,
            KeyCode.Mouse3, KeyCode.Mouse4, KeyCode.Mouse5, KeyCode.Mouse6
        };
        private readonly List<IDisposable> subscriptions =
            new List<IDisposable>();
        private readonly HashSet<KeyCode> hotkeyCaptureIgnoredKeys =
            new HashSet<KeyCode>();
        private ManualLogSource log;
        private CastlePlannerSettingsViewModel settings;
        private FreeCastlePreviewRuntime preview;
        private BlueprintRenderer renderer;
        private BlueprintBuildingSizeCalibration sizeCalibration;
        private BlueprintBuildingImageLibrary buildingImageLibrary;
        private BlueprintLayout layout;
        private int layoutKeepX = int.MinValue;
        private int layoutKeepY = int.MinValue;
        private bool initialized;
        private bool mapActive;
        private bool editorSessionActive;
        private int editorControlledPlayerId = -1;
        private bool preparePending;
        private bool showAfterPrepare;
        private bool blueprintVisible;
        private bool controlledKeepAvailable;
        private bool hotkeyCapturePending;
        private int hotkeyCaptureStartFrame;
        private int lastRotation = int.MinValue;
        private bool lastFlattenedLandscape;
        private float pendingViewSettleTime = -1f;
        private bool suppressOverlayUntilViewSettled;
        private float nextRuntimeErrorLogTime;
        private int lastTickFrame = -1;
        private bool beforeRenderCallbackObserved;
        private bool depthLoadReady;
        private bool hudObserverPending;
        private MainViewModel observedHudViewModel;
        private Hook cameraUpdateHook;
        private CameraUpdateDelegate cameraUpdateTrampoline;
        private Hook editorPlayerHook;
        private EditorPlayerActionDelegate editorPlayerTrampoline;

        public BlueprintHudViewModel Hud { get; private set; }

        public static BlueprintRuntimeController Create(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings,
            FreeCastlePreviewRuntime preview)
        {
            var controller = new BlueprintRuntimeController();
            controller.Initialize(log, settings, preview);
            return controller;
        }

        private void Initialize(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings,
            FreeCastlePreviewRuntime preview)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings =
                settings ?? throw new ArgumentNullException(nameof(settings));
            this.preview = preview ?? throw new ArgumentNullException(nameof(preview));
            sizeCalibration =
                new BlueprintBuildingSizeCalibration(log);
            buildingImageLibrary =
                new BlueprintBuildingImageLibrary(log);
            buildingImageLibrary.DepthReadCompleted += OnDepthReadCompleted;
            renderer = new BlueprintRenderer(
                log,
                sizeCalibration,
                buildingImageLibrary);
            Hud = new BlueprintHudViewModel(ToggleBlueprint, settings, preview);
            InstallEditorPlayerHook();
            InstallCameraWheelGuard();

            settings.SettingsChanged += OnSettingsChanged;
            settings.BlueprintVisualSettingsChanged +=
                OnBlueprintVisualSettingsChanged;
            settings.BlueprintContentSettingsChanged +=
                OnBlueprintContentSettingsChanged;
            settings.HotkeyCaptureRequested += OnHotkeyCaptureRequested;
            subscriptions.Add(InputR3EventHooks.OnKeyDown.Observable.Subscribe(OnKeyDown));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawned));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(OnBuildingDeleted));
            preview.SelectionVisualChanged += OnPreviewSelectionChanged;
            Shared.GameplayModActivationGate.StateChanged += OnModeStateChanged;
            subscriptions.Add(Shared.GameplaySessionLifecycle.SubscribeStarted(
                log,
                context =>
                {
                    if (context.IsEditor)
                        OnEditorMapReady();
                    else if (context.IsLoadedSave)
                        OnLoadSave(context.Notification);
                    else
                        OnStartMap(context.Notification);
                }));
            subscriptions.Add(
                Shared.MissionEvents.Ended
                    .Subscribe(OnUnloadMap));

            initialized = true;
            RefreshHud();
            // Blueprint input, camera, and overlay work must follow rendered frames even while
            // simulation ticks are paused. This verified static callback survives startup, and
            // TickOncePerFrame deduplicates multiple callbacks within the same rendered frame.
            Application.onBeforeRender += OnBeforeRender;
            Application.focusChanged += OnApplicationFocusChanged;
            Shared.DebugLogHelper.LogDebug(
                log,
                "Persistent local blueprint runtime initialized; " +
                "Application.onBeforeRender frame loop and focus-loss guard " +
                "registered.");
        }

        private void OnApplicationFocusChanged(bool focused)
        {
            if (focused)
                return;

            try
            {
                if (Hud?.CloseSearchPopupForApplicationFocusLoss() == true)
                {
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        "Blueprint AIVJSON search popup closed because the " +
                        "application lost focus.");
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Failed to close the Blueprint AIVJSON search popup " +
                    $"during application focus loss: {ex}");
            }
        }

        private void OnBeforeRender()
        {
            // A hidden projection has no frame-dependent work after its one-time preparation.
            if (!blueprintVisible && !hotkeyCapturePending && !depthLoadReady &&
                !(mapActive && buildingImageLibrary.HasCompletedDepthRead) &&
                !(Hud?.RequiresFrameInput ?? false) &&
                !hudObserverPending &&
                !(editorSessionActive && editorControlledPlayerId < 1))
                return;

            if (!beforeRenderCallbackObserved)
            {
                beforeRenderCallbackObserved = true;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "Persistent Application.onBeforeRender blueprint callback is active.");
            }

            try
            {
                HideStaleNormalProjectionBeforeRender();
                TickOncePerFrame();
            }
            catch (Exception ex)
            {
                // Keep the process-lifetime callback alive after transient scene changes.
                if (Time.unscaledTime >= nextRuntimeErrorLogTime)
                {
                    nextRuntimeErrorLogTime = Time.unscaledTime + 5f;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Blueprint frame loop recovered from an error: {ex}");
                }
            }
        }

        private void TickOncePerFrame()
        {
            int frame = Time.frameCount;
            if (lastTickFrame == frame)
                return;

            lastTickFrame = frame;
            Tick();
        }

        private void Tick()
        {
            if (!initialized)
                return;

            if (hudObserverPending)
                ObserveHudState();
            Hud?.EnsureInteractiveElementsAttached();
            Hud?.CompleteCastleSearchOpeningClick();
            Hud?.ProcessOpenDropDownWheel();
            UpdateHotkeyCapture();
            if (editorSessionActive && editorControlledPlayerId < 1)
                RefreshEditorPlayer();

            if (!mapActive)
                return;

            // Decode at most one capture atlas per frame so the first projection
            // stays responsive while exact building layers appear progressively.
            if (depthLoadReady || buildingImageLibrary.HasCompletedDepthRead)
            {
                if (buildingImageLibrary.ProcessOnePendingDepthLoad())
                    RefreshHud();
                depthLoadReady = buildingImageLibrary.HasCompletedDepthRead;
            }

            if (!EffectiveBlueprintMode)
                return;

            if (blueprintVisible && layout != null && sizeCalibration.Tick())
            {
                RenderCurrentLayout("Vanilla building preview calibrated");
            }

            if (blueprintVisible && layout != null)
            {
                int rotation = (int)GameMap.instance.CurrentRotation();
                bool flattened = EngineInterface.FlattenedLandscape;
                if (rotation != lastRotation ||
                    flattened != lastFlattenedLandscape)
                {
                    bool returningToNormal =
                        !flattened && lastFlattenedLandscape;
                    if (returningToNormal ||
                        (suppressOverlayUntilViewSettled && !flattened))
                    {
                        SuppressOverlayUntilViewSettled(rotation);
                    }
                    else if (RenderCurrentLayout("map view changed"))
                    {
                        suppressOverlayUntilViewSettled = false;
                        ScheduleViewSettleRebuild(flattened, rotation);
                    }
                }
                else if (pendingViewSettleTime >= 0f &&
                    Time.unscaledTime >= pendingViewSettleTime)
                {
                    pendingViewSettleTime = -1f;
                    suppressOverlayUntilViewSettled = false;
                    RenderCurrentLayout("map view settled");
                }
            }

        }

        private void InstallEditorPlayerHook()
        {
            MethodInfo method = typeof(EditorDirector).GetMethod(
                nameof(EditorDirector.SetEditorPlayerID), BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(int) }, null) ??
                throw new MissingMethodException("EditorDirector.SetEditorPlayerID");
            Hook candidate = null;
            try
            {
                candidate = new Hook(method, (EditorPlayerActionDelegate)OnEditorPlayerAction);
                EditorPlayerActionDelegate trampoline =
                    candidate.GenerateTrampoline<EditorPlayerActionDelegate>();
                editorPlayerTrampoline = trampoline;
                editorPlayerHook = candidate;
            }
            catch
            {
                try { candidate?.Undo(); } catch { }
                try { candidate?.Dispose(); } catch { }
                throw;
            }
        }

        private void OnEditorPlayerAction(EditorDirector editor, int playerId)
        {
            editorPlayerTrampoline(editor, playerId);
            if (editorSessionActive)
                RefreshEditorPlayer();
        }

        private void InstallCameraWheelGuard()
        {
            MethodInfo update = typeof(CameraControls2D).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new MissingMethodException("CameraControls2D.Update");
            cameraUpdateHook = new Hook(
                update,
                (CameraUpdateDelegate)CameraUpdateHook);
            cameraUpdateTrampoline =
                cameraUpdateHook.GenerateTrampoline<CameraUpdateDelegate>();
        }

        private void CameraUpdateHook(CameraControls2D camera)
        {
            if (Hud?.SettingsPanelVisible == true &&
                Shared.GameplayFeatureModePolicy.IsAllowed(
                    CastlePlannerPlugin.PluginGuid,
                    Shared.GameplayFeatureId.CastleBlueprints,
                    Shared.GameplayModActivationGate.Snapshot) &&
                Hud?.ShouldSuppressMapZoom() == true)
                camera.AllowZoom = false;
            cameraUpdateTrampoline(camera);
        }

        private void OnStartMap(APIShared.MissionLifecycleNotification args)
        {
            ResetMapState();
            editorSessionActive = false;
            editorControlledPlayerId = -1;
            // SimRunning can still be false in OnStartMap(Post), while MainHUD is
            // already entering its map lifecycle and should expose the local toggle.
            mapActive = true;
            if (EffectiveBlueprintMode)
            {
                SchedulePrepare(preview.IsPreviewActive);
                TryPrepareBlueprint();
            }
            RefreshHud();
            Shared.DebugLogHelper.LogDebug(
                log,
                "Blueprint map-start lifecycle received; visibility reset to hidden.");
        }

        private void OnLoadSave(APIShared.MissionLifecycleNotification args)
        {
            ResetMapState();
            editorSessionActive = false;
            editorControlledPlayerId = -1;
            mapActive = true;
            if (settings.IsBlueprintMode)
            {
                SchedulePrepare(false);
                TryPrepareBlueprint();
            }
            RefreshHud();
            Shared.DebugLogHelper.LogDebug(
                log,
                "Blueprint save-load lifecycle received; visibility reset to hidden.");
        }

        private void OnUnloadMap(APIShared.MissionLifecycleNotification args)
        {
            ResetMapState();
            hudObserverPending = false;
            mapActive = false;
            editorSessionActive = false;
            editorControlledPlayerId = -1;
            RefreshHud();
            Shared.DebugLogHelper.LogDebug(
                log,
                "Blueprint overlay cleared for map unload.");
        }

        private void OnSettingsChanged()
        {
            bool restoreVisibility =
                (preview?.IsPreviewActive == true && preview.HasSelectedCastle) ||
                (blueprintVisible && EffectiveBlueprintMode);
            renderer.Clear();
            layout = null;
            layoutKeepX = int.MinValue;
            layoutKeepY = int.MinValue;
            blueprintVisible = false;
            preparePending = false;
            showAfterPrepare = false;
            pendingViewSettleTime = -1f;
            suppressOverlayUntilViewSettled = false;

            if (EffectiveBlueprintMode && mapActive)
            {
                SchedulePrepare(restoreVisibility);
                TryPrepareBlueprint();
            }

            RefreshHud();
        }

        private bool EffectiveBlueprintMode =>
            Shared.GameplayFeatureModePolicy.IsAllowed(
                CastlePlannerPlugin.PluginGuid,
                Shared.GameplayFeatureId.CastleBlueprints,
                Shared.GameplayModActivationGate.Snapshot) &&
            (settings?.IsBlueprintMode == true || preview?.IsPreviewActive == true);

        private void OnModeStateChanged(bool allowed)
        {
            if (!allowed)
            {
                ResetMapState();
                hudObserverPending = false;
            }
            else if (mapActive && settings.IsBlueprintMode)
            {
                SchedulePrepare(false);
                TryPrepareBlueprint();
            }
            RefreshHud();
        }

        private void OnPreviewSelectionChanged()
        {
            // Preview changes stay visible while choosing a castle. Leaving a
            // no-castle preview always restores the normal layout hidden.
            bool restoreVisibility = preview.IsPreviewActive;
            renderer.Clear();
            layout = null;
            layoutKeepX = int.MinValue;
            layoutKeepY = int.MinValue;
            blueprintVisible = false;
            if (mapActive && EffectiveBlueprintMode)
            {
                SchedulePrepare(restoreVisibility);
                TryPrepareBlueprint();
            }
            RefreshHud();
        }

        private void OnBlueprintVisualSettingsChanged()
        {
            // Existing transforms and material properties can be updated without
            // destroying thousands of projection objects while a slider moves.
            renderer.UpdateVisualSettings(
                settings.BlueprintIconScaleValue,
                settings.BlueprintIconAlphaValue);
        }

        private void OnBlueprintContentSettingsChanged()
        {
            // Spawn-selection previews continue to follow the spawn options. The
            // new local filters only rebuild normal in-game Blueprint layouts.
            if (preview.IsPreviewActive)
                return;

            OnSettingsChanged();
        }

        private void OnHotkeyCaptureRequested()
        {
            hotkeyCapturePending = true;
            hotkeyCaptureStartFrame = Time.frameCount;
            hotkeyCaptureIgnoredKeys.Clear();

            // Ignore mouse buttons already held by the click that opened capture.
            foreach (KeyCode key in CaptureMouseButtons)
            {
                if (TryGetKey(key, out bool pressed) && pressed)
                    hotkeyCaptureIgnoredKeys.Add(key);
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Blueprint hotkey capture armed; ignoredHeldMouseButtons=" +
                $"{hotkeyCaptureIgnoredKeys.Count}.");
        }

        private void OnKeyDown(UnityInputEventArgs args)
        {
            if (args == null || args.Phase != EventHookPhase.Pre)
                return;
            if (!hotkeyCapturePending)
            {
                if (args.Key == settings.BlueprintHotkeyCode &&
                    args.Key != KeyCode.None && layout != null &&
                    EffectiveBlueprintMode && mapActive && CanUseGameplayHotkeys() &&
                    (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) == settings.BlueprintHotkeyAlt &&
                    (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) == settings.BlueprintHotkeyControl &&
                    (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) == settings.BlueprintHotkeyShift)
                    ToggleBlueprint();
                return;
            }
            if (Time.frameCount <= hotkeyCaptureStartFrame)
                return;
            try
            {
                if (IsCaptureWindowActive())
                {
                    CompleteHotkeyCapture(args.Key, "Script Extender OnKeyDown");
                    return;
                }
                settings.CancelHotkeyCapture();
                StopHotkeyCapture();
            }
            catch (Exception ex)
            {
                settings.CancelHotkeyCapture();
                StopHotkeyCapture();
                Shared.DebugLogHelper.LogError(log, $"Blueprint hotkey capture failed: {ex}");
            }
        }

        private bool IsCaptureWindowActive()
        {
            var hub = SHCDESE.BepInEx.Bootstrap.Plugin.ModSettingsHubViewModel;
            return hub != null &&
                   hub.WindowVisibility == Noesis.Visibility.Visible &&
                   ReferenceEquals(hub.SelectedTab?.ViewModel, settings);
        }

        private void UpdateHotkeyCapture()
        {
            if (hotkeyCapturePending && !IsCaptureWindowActive())
                settings.CancelHotkeyCapture();
            if (hotkeyCapturePending && !settings.IsCapturingHotkey)
            {
                StopHotkeyCapture();
                return;
            }

            if (!hotkeyCapturePending ||
                Time.frameCount <= hotkeyCaptureStartFrame)
            {
                return;
            }

            foreach (KeyCode key in CaptureMouseButtons)
            {
                if (!TryGetKey(key, out bool pressed))
                    continue;

                if (hotkeyCaptureIgnoredKeys.Contains(key))
                {
                    if (!pressed)
                        hotkeyCaptureIgnoredKeys.Remove(key);
                    continue;
                }

                if (!pressed)
                    continue;

                if (CompleteHotkeyCapture(key, "Unity mouse held-state scan"))
                    return;
                hotkeyCaptureIgnoredKeys.Add(key);
            }
        }

        private bool CompleteHotkeyCapture(KeyCode key, string source)
        {
            if (key != KeyCode.Escape &&
                !CastlePlannerSettingsViewModel.IsMainHotkey(key))
                return false;
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!settings.CompleteHotkeyCapture(key, alt, control, shift))
                return false;
            StopHotkeyCapture();
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Blueprint hotkey capture finished: key={key}, " +
                $"value={(int)key}, source={source}.");
            return true;
        }

        private void StopHotkeyCapture()
        {
            hotkeyCapturePending = false;
            hotkeyCaptureIgnoredKeys.Clear();
        }

        private static bool TryGetKey(KeyCode key, out bool pressed)
        {
            try
            {
                pressed = Input.GetKey(key);
                return true;
            }
            catch
            {
                pressed = false;
                return false;
            }
        }

        private void SchedulePrepare(bool restoreVisibility)
        {
            preparePending = true;
            showAfterPrepare = restoreVisibility;
            RefreshHud();
        }

        private void TryPrepareBlueprint()
        {
            if (!TryFindControlledKeep(out int keepX, out int keepY))
            {
                if (controlledKeepAvailable)
                {
                    controlledKeepAvailable = false;
                    RefreshHud();
                }
                return;
            }

            controlledKeepAvailable = true;

            if (!TryBuildBlueprintLayout(
                    keepX,
                    keepY,
                    "scheduled preparation"))
            {
                preparePending = false;
                showAfterPrepare = false;
                RefreshHud();
                return;
            }

            bool restoreVisibility = showAfterPrepare;
            preparePending = false;
            showAfterPrepare = false;
            if (restoreVisibility)
                SetBlueprintVisible(true, "settings reload");
            else
                RefreshHud();
        }

        private void OnDepthReadCompleted()
        {
            depthLoadReady = mapActive;
        }

        private void OnBuildingSpawned(BuildingSpawnEventArgs args)
        {
            if (args == null || args.Phase != EventHookPhase.Post ||
                !IsKeep(args.Building))
                return;

            int playerId = args.PlayerId;
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                if (mapActive && preparePending && playerId == GetControlledPlayerId())
                    TryPrepareBlueprint();
            });
        }

        private void OnBuildingDeleted(BuildingDeleteEventArgs args)
        {
            if (args == null || args.Phase != EventHookPhase.Post)
                return;

            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                if (editorSessionActive && layout != null &&
                    (!TryFindControlledKeep(out int keepX, out int keepY) ||
                     keepX != layoutKeepX || keepY != layoutKeepY))
                    OnSettingsChanged();
            });
        }

        private bool TryBuildBlueprintLayout(
            int keepX,
            int keepY,
            string reason)
        {
            bool previewHasNoCastle =
                preview.IsPreviewActive && !preview.HasSelectedCastle;
            bool committedPlayerHasNoCastle = false;
            if (preview.IsSpawnMapPass)
            {
                committedPlayerHasNoCastle =
                    !preview.TryGetCommittedSelection(
                        GetControlledPlayerId(),
                        out FreeCastleSelection committed) ||
                    !committed.HasCastle;
            }
            if (previewHasNoCastle || committedPlayerHasNoCastle)
            {
                layout = null;
                layoutKeepX = int.MinValue;
                layoutKeepY = int.MinValue;
                renderer.Clear();
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "Blueprint hidden because the start selection contains no castle.");
                return false;
            }

            if (!settings.TryResolveSelectedFile(out string fullPath))
            {
                layout = null;
                layoutKeepX = int.MinValue;
                layoutKeepY = int.MinValue;
                renderer.Clear();
                if (!settings.IsSpawnMode)
                {
                    // The host AIV dropdown is irrelevant while Spawn Castle is disabled.
                    // A stale local Blueprint choice therefore disables only the optional overlay.
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Blueprint preparation skipped because the local AIVJSON is unavailable and Spawn Castle is disabled: '{settings.SelectedCastle}'.");
                    return false;
                }

                Shared.DebugLogHelper.LogError(
                    log,
                    $"Blueprint preparation failed; overlay remains unavailable: " +
                    $"Selected AIVJSON is unavailable while Spawn Castle is enabled: '{settings.SelectedCastle}'.");
                return false;
            }

            try
            {
                string json = File.ReadAllText(fullPath);
                AivJsonDocument document = AivJsonReader.Parse(json);
                AivSpawnOptions displayOptions = preview.IsPreviewActive
                    ? settings.GetLocalPreviewSpawnOptions()
                    : settings.GetBlueprintDisplayOptions();
                AivJsonDocument filteredDocument = AivSpawnPlan.Filter(
                    document,
                    displayOptions);
                AIVParser.Core.AivRotation castleRotation = GetPreviewRotation();
                layout = BlueprintLayoutBuilder.Build(
                    filteredDocument,
                    keepX,
                    keepY,
                    castleRotation,
                    preview.IsPreviewActive
                        ? BlueprintProjectionMode.NativeFixedGrid
                        : BlueprintProjectionMode.NativeFixedGridAlignedToKeep);
                layoutKeepX = keepX;
                layoutKeepY = keepY;
                renderer.PreloadDepthCaptures(layout);
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Blueprint prepared locally: reason={reason}, " +
                    $"file={fullPath}, keep=({keepX},{keepY}), " +
                    $"projectedKeep=({layout.ProjectedKeep.X},{layout.ProjectedKeep.Y}), " +
                    $"castleRotation={(int)layout.CastleRotation}, " +
                    $"tiles={layout.Tiles.Count}, icons={layout.Icons.Count}, " +
                    $"unknownMappers={layout.UnknownMapperCount}, " +
                    $"miscItemsIgnored={filteredDocument.miscItems?.Count ?? 0}.");
                return true;
            }
            catch (Exception ex)
            {
                layout = null;
                layoutKeepX = int.MinValue;
                layoutKeepY = int.MinValue;
                renderer.Clear();
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Blueprint preparation failed; overlay remains unavailable: {ex}");
                return false;
            }
        }

        private bool TryFindControlledKeep(out int keepX, out int keepY)
        {
            keepX = 0;
            keepY = 0;
            if (GameBuildingManagerAPI.Instance == null ||
                GamePlayerManagerAPI.Instance == null)
            {
                return false;
            }

            int controlledPlayerId = GetControlledPlayerId();
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(controlledPlayerId))
                return false;

            Span<GameBuilding> buildings =
                GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            foreach (GameBuilding building in buildings)
            {
                if (building.r_PlayerIdOwner != controlledPlayerId ||
                    !IsKeep(building.r_BuildingType) ||
                    (building.r_AliveState != AliveState.NeedsInit &&
                     building.r_AliveState != AliveState.IsAlive))
                {
                    continue;
                }

                keepX = building.r_TilePositionXBegin;
                keepY = building.r_TilePositionYBegin;
                return true;
            }

            return false;
        }

        private AIVParser.Core.AivRotation GetPreviewRotation()
        {
            int nativeRotation = 0;
            if (preview != null)
            {
                if (preview.IsPreviewActive)
                {
                    nativeRotation = preview.SelectedNativeRotation;
                }
                else if (preview.IsSpawnMapPass)
                {
                    preview.TryGetCommittedRotation(
                        GetControlledPlayerId(),
                        out nativeRotation);
                }
            }

            switch (nativeRotation)
            {
                case 2: return AIVParser.Core.AivRotation.Degrees90;
                case 4: return AIVParser.Core.AivRotation.Degrees180;
                case 6: return AIVParser.Core.AivRotation.Degrees270;
                default: return AIVParser.Core.AivRotation.Degrees0;
            }
        }

        private void ToggleBlueprint()
        {
            if (!EffectiveBlueprintMode || !mapActive)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Blueprint toggle ignored because Blueprint mode or the map is inactive.");
                RefreshHud();
                return;
            }

            // The editor can move or replace the local Keep without a map
            // reload, so every toggle must validate its live position.
            if (!TryFindControlledKeep(out int keepX, out int keepY))
            {
                controlledKeepAvailable = false;
                renderer.Clear();
                layout = null;
                layoutKeepX = int.MinValue;
                layoutKeepY = int.MinValue;
                blueprintVisible = false;
                SchedulePrepare(false);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Blueprint remains hidden because no live local Keep was found during activation.");
                RefreshHud();
                return;
            }

            controlledKeepAvailable = true;
            if (blueprintVisible)
            {
                SetBlueprintVisible(false, "HUD or configured hotkey");
                return;
            }

            preparePending = false;
            showAfterPrepare = false;
            AIVParser.Core.AivRotation castleRotation = GetPreviewRotation();
            if (layout == null ||
                layoutKeepX != keepX ||
                layoutKeepY != keepY ||
                layout.CastleRotation != castleRotation)
            {
                if (!TryBuildBlueprintLayout(
                        keepX,
                        keepY,
                        "activation Keep refresh"))
                {
                    RefreshHud();
                    return;
                }
            }

            SetBlueprintVisible(true, "HUD or configured hotkey");
        }

        private void SetBlueprintVisible(bool visible, string reason)
        {
            if (blueprintVisible == visible)
                return;

            if (visible)
            {
                blueprintVisible = true;
                if (!renderer.TryShowExisting(layout) && !RenderCurrentLayout(reason))
                {
                    blueprintVisible = false;
                    renderer.Clear();
                }
            }
            else
            {
                renderer.Hide();
                blueprintVisible = false;
                pendingViewSettleTime = -1f;
                suppressOverlayUntilViewSettled = false;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Blueprint hidden locally: reason={reason}.");
            }

            RefreshHud();
        }

        private void HideStaleNormalProjectionBeforeRender()
        {
            if (!initialized ||
                !mapActive ||
                !blueprintVisible ||
                layout == null ||
                GameMap.instance == null ||
                EngineInterface.FlattenedLandscape)
            {
                return;
            }

            int rotation = (int)GameMap.instance.CurrentRotation();
            if (lastFlattenedLandscape ||
                (suppressOverlayUntilViewSettled &&
                    rotation != lastRotation))
            {
                // This callback can observe Vanilla's view change after our
                // regular tick, but still before Unity submits the frame.
                SuppressOverlayUntilViewSettled(rotation);
            }
        }

        private void SuppressOverlayUntilViewSettled(int rotation)
        {
            // Normal terrain heights remain stale briefly after leaving flat
            // view. Hide instead of displaying that incorrect projection.
            renderer.Clear();
            lastRotation = rotation;
            lastFlattenedLandscape = false;
            suppressOverlayUntilViewSettled = true;
            pendingViewSettleTime =
                Time.unscaledTime + ViewSettleDelaySeconds;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Blueprint temporarily hidden while the normal map " +
                $"projection settles: delay={ViewSettleDelaySeconds:F2}s, " +
                $"flattened=false, rotation={rotation}.");
        }

        private void ScheduleViewSettleRebuild(
            bool flattened,
            int rotation)
        {
            // The native flag changes before terrain heights and Tilemaps have
            // fully settled, so retain the final confirmation render.
            pendingViewSettleTime =
                Time.unscaledTime + ViewSettleDelaySeconds;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Blueprint view settle rebuild scheduled: " +
                $"delay={ViewSettleDelaySeconds:F2}s, " +
                $"flattened={flattened}, rotation={rotation}.");
        }

        private bool RenderCurrentLayout(string reason)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                BlueprintRenderResult result = renderer.Render(
                    layout,
                    settings.BlueprintIconScaleValue,
                    settings.BlueprintIconAlphaValue);
                lastRotation = (int)GameMap.instance.CurrentRotation();
                lastFlattenedLandscape =
                    EngineInterface.FlattenedLandscape;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Blueprint rendered locally: reason={reason}, " +
                    $"tiles={result.RenderedTiles}, icons={result.RenderedIcons}, " +
                    $"clipped={result.ClippedTiles}, " +
                    $"depthReady={renderer.CompletedDepthCaptureCount}/" +
                    $"{renderer.RequestedDepthCaptureCount}, " +
                    $"elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F1}, " +
                    $"rotation={lastRotation}, " +
                    $"flattened={lastFlattenedLandscape}.");
                return true;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Blueprint rendering failed: {ex}");
                return false;
            }
        }

        private void ResetMapState()
        {
            if (observedHudViewModel != null)
                observedHudViewModel.PropertyChanged -= OnHudPropertyChanged;
            observedHudViewModel = null;
            hudObserverPending = true;
            Hud?.ResetForMapLifecycle();
            renderer?.Clear();
            layout = null;
            layoutKeepX = int.MinValue;
            layoutKeepY = int.MinValue;
            preparePending = false;
            showAfterPrepare = false;
            blueprintVisible = false;
            depthLoadReady = false;
            controlledKeepAvailable = false;
            lastRotation = int.MinValue;
            lastFlattenedLandscape = false;
            pendingViewSettleTime = -1f;
            suppressOverlayUntilViewSettled = false;
        }

        private void ObserveHudState()
        {
            if (!MainViewModel.viewModelLoaded || MainViewModel.Instance?.HUDmain == null)
                return;

            MainViewModel viewModel = MainViewModel.Instance;
            if (!ReferenceEquals(observedHudViewModel, viewModel))
            {
                if (observedHudViewModel != null)
                    observedHudViewModel.PropertyChanged -= OnHudPropertyChanged;
                observedHudViewModel = viewModel;
                viewModel.PropertyChanged += OnHudPropertyChanged;
            }
            hudObserverPending = false;
            Hud?.UpdateViewportSize(
                MainViewModel.iUIScaleValueWidth,
                MainViewModel.iUIScaleValueHeight);
            UpdateVanillaButtonSlot(viewModel);
            if (preparePending)
                TryPrepareBlueprint();
        }

        private void OnHudPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args?.PropertyName == nameof(MainViewModel.Show_HUD_Extras_Button_Objectves) ||
                args?.PropertyName == nameof(MainViewModel.Show_HUD_Extras_Button_Freebuild))
                UpdateVanillaButtonSlot(sender as MainViewModel);
        }

        private void UpdateVanillaButtonSlot(MainViewModel viewModel)
        {
            Hud?.UpdateVanillaButtonSlot(viewModel != null &&
                (viewModel.Show_HUD_Extras_Button_Objectves ||
                 viewModel.Show_HUD_Extras_Button_Freebuild));
        }

        private void RefreshHud()
        {
            Hud?.Update(
                EffectiveBlueprintMode,
                mapActive,
                controlledKeepAvailable,
                layout != null,
                blueprintVisible,
                renderer?.CompletedDepthCaptureCount ?? 0,
                renderer?.RequestedDepthCaptureCount ?? 0);
        }

        private void OnEditorMapReady()
        {
            ResetMapState();
            mapActive = true;
            editorSessionActive = true;
            editorControlledPlayerId = -1;
            RefreshEditorPlayer();
            RefreshHud();
        }

        private void RefreshEditorPlayer()
        {
            if (!editorSessionActive) return;

            int activePlayerId = EditorDirector.instance?.ActivePlayerID ?? -1;
            if (activePlayerId < 1 ||
                activePlayerId > GamePlayerManagerAPI.MAX_PLAYERS ||
                GameData.Instance?.lastGameState == null ||
                GameMap.instance == null ||
                TilemapManager.instance == null)
            {
                return;
            }

            if (editorSessionActive && editorControlledPlayerId == activePlayerId)
                return;

            bool restoreVisibility = editorSessionActive && blueprintVisible;
            int previousPlayerId = editorControlledPlayerId;
            ResetMapState();
            mapActive = true;
            editorSessionActive = true;
            editorControlledPlayerId = activePlayerId;
            if (settings.IsBlueprintMode)
            {
                SchedulePrepare(restoreVisibility);
                TryPrepareBlueprint();
            }
            RefreshHud();
            Shared.DebugLogHelper.LogDebug(
                log,
                previousPlayerId > 0
                    ? $"Blueprint editor player changed: previousActivePlayerId={previousPlayerId}, activePlayerId={activePlayerId}, restoreVisibility={restoreVisibility}."
                    : $"Blueprint editor lifecycle started: activePlayerId={activePlayerId}.");
        }

        private static int GetControlledPlayerId()
        {
            if (IsMapEditor())
                return EditorDirector.instance?.ActivePlayerID ?? -1;

            return GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1;
        }

        private static bool IsMapEditor() => APIShared.GameModes.GameModeHelper.IsMapEditor();

        private static bool CanUseGameplayHotkeys()
        {
            return FatControler.instance != null &&
                   !FatControler.instance.NoesisHasKeyboard &&
                   ((Director.instance != null && Director.instance.SimRunning) ||
                    IsMapEditor());
        }

        private static bool IsKeep(eStructs structure)
        {
            return structure == eStructs.STRUCT_KEEP_ONE ||
                   structure == eStructs.STRUCT_KEEP_TWO ||
                   structure == eStructs.STRUCT_KEEP_THREE ||
                   structure == eStructs.STRUCT_KEEP_FOUR ||
                   structure == eStructs.STRUCT_KEEP_FIVE;
        }

    }
}
