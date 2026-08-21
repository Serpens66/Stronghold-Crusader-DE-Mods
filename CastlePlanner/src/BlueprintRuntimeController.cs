using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CastlePlanner
{
    internal sealed class BlueprintRuntimeController
    {
        private const float ViewSettleDelaySeconds = 0.5f;
        private static readonly KeyCode[] SupportedKeys = CreateSupportedKeys();
        private readonly List<IDisposable> subscriptions =
            new List<IDisposable>();
        private readonly HashSet<KeyCode> hotkeyCaptureIgnoredKeys =
            new HashSet<KeyCode>();
        private ManualLogSource log;
        private CastlePlannerSettingsViewModel settings;
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
        private bool hotkeyCapturePending;
        private int hotkeyCaptureStartFrame;
        private float nextPrepareAttemptTime;
        private int lastRotation = int.MinValue;
        private bool lastFlattenedLandscape;
        private float pendingViewSettleTime = -1f;
        private bool suppressOverlayUntilViewSettled;
        private float nextRuntimeErrorLogTime;
        private int lastTickFrame = -1;
        private bool beforeRenderCallbackObserved;

        public BlueprintHudViewModel Hud { get; private set; }

        public static BlueprintRuntimeController Create(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings)
        {
            var controller = new BlueprintRuntimeController();
            controller.Initialize(log, settings);
            return controller;
        }

        private void Initialize(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings =
                settings ?? throw new ArgumentNullException(nameof(settings));
            sizeCalibration =
                new BlueprintBuildingSizeCalibration(log);
            buildingImageLibrary =
                new BlueprintBuildingImageLibrary(log);
            renderer = new BlueprintRenderer(
                log,
                sizeCalibration,
                buildingImageLibrary);
            Hud = new BlueprintHudViewModel(ToggleBlueprint, settings);

            settings.SettingsChanged += OnSettingsChanged;
            settings.BlueprintVisualSettingsChanged +=
                OnBlueprintVisualSettingsChanged;
            settings.HotkeyCaptureRequested += OnHotkeyCaptureRequested;
            subscriptions.Add(
                MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnStartMap));
            subscriptions.Add(
                MapLoaderR3EventHooks.OnLoadSave.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnLoadSave));
            subscriptions.Add(
                MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnUnloadMap));

            initialized = true;
            RefreshHud();
            // Blueprint input, camera, and overlay work must follow rendered frames even while
            // simulation ticks are paused. This verified static callback survives startup, and
            // TickOncePerFrame deduplicates multiple callbacks within the same rendered frame.
            Application.onBeforeRender += OnBeforeRender;
            Shared.DebugLogHelper.LogInfo(
                log,
                "Persistent local blueprint runtime initialized; " +
                "Application.onBeforeRender frame loop registered.");
        }

        private void OnBeforeRender()
        {
            if (!beforeRenderCallbackObserved)
            {
                beforeRenderCallbackObserved = true;
                Shared.DebugLogHelper.LogInfo(
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

            CrusaderDE.MainViewModel mainViewModel = null;
            if (CrusaderDE.MainViewModel.viewModelLoaded)
            {
                // The Instance getter constructs the vanilla view model when
                // called too early, while its own startup dependencies are null.
                mainViewModel = CrusaderDE.MainViewModel.Instance;
                Hud?.UpdateViewportSize(
                    CrusaderDE.MainViewModel.iUIScaleValueWidth,
                    CrusaderDE.MainViewModel.iUIScaleValueHeight);
            }
            Hud?.UpdateVanillaButtonSlot(
                mainViewModel != null &&
                (mainViewModel.Show_HUD_Extras_Button_Objectves ||
                 mainViewModel.Show_HUD_Extras_Button_Freebuild));
            UpdateHotkeyCapture();
            EnsureEditorMapState();
            if (!mapActive && IsSimulationActive())
            {
                // Fallback for unusual map flows that do not emit the normal hook.
                mapActive = true;
                if (settings.IsBlueprintMode && layout == null)
                    SchedulePrepare(false);
                RefreshHud();
            }

            if (!mapActive)
                return;

            // Decode at most one capture atlas per frame so the first projection
            // stays responsive while exact building layers appear progressively.
            if (buildingImageLibrary.ProcessOnePendingDepthLoad())
                RefreshHud();

            if (!settings.IsBlueprintMode)
                return;

            if (sizeCalibration.Tick() &&
                blueprintVisible &&
                layout != null)
            {
                RenderCurrentLayout("Vanilla building preview calibrated");
            }

            if (preparePending &&
                Time.unscaledTime >= nextPrepareAttemptTime)
            {
                TryPrepareBlueprint();
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

            KeyCode hotkey = settings.BlueprintHotkeyCode;
            if (!hotkeyCapturePending &&
                hotkey != KeyCode.None &&
                layout != null &&
                CanUseGameplayHotkeys() &&
                Input.GetKeyDown(hotkey))
            {
                ToggleBlueprint();
            }
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            ResetMapState();
            editorSessionActive = false;
            editorControlledPlayerId = -1;
            // SimRunning can still be false in OnStartMap(Post), while MainHUD is
            // already entering its map lifecycle and should expose the local toggle.
            mapActive = true;
            if (settings.IsBlueprintMode)
            {
                SchedulePrepare(false);
                TryPrepareBlueprint();
            }
            RefreshHud();
            Shared.DebugLogHelper.LogInfo(
                log,
                "Blueprint map-start lifecycle received; visibility reset to hidden.");
        }

        private void OnLoadSave(LoadSaveGameEventArgs args)
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
            Shared.DebugLogHelper.LogInfo(
                log,
                "Blueprint save-load lifecycle received; visibility reset to hidden.");
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            ResetMapState();
            mapActive = false;
            editorSessionActive = false;
            editorControlledPlayerId = -1;
            RefreshHud();
            Shared.DebugLogHelper.LogInfo(
                log,
                "Blueprint overlay cleared for map unload.");
        }

        private void OnSettingsChanged()
        {
            bool restoreVisibility =
                blueprintVisible && settings.IsBlueprintMode;
            renderer.Clear();
            layout = null;
            layoutKeepX = int.MinValue;
            layoutKeepY = int.MinValue;
            blueprintVisible = false;
            preparePending = false;
            showAfterPrepare = false;
            pendingViewSettleTime = -1f;
            suppressOverlayUntilViewSettled = false;

            if (settings.IsBlueprintMode && mapActive)
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

        private void OnHotkeyCaptureRequested()
        {
            hotkeyCapturePending = true;
            hotkeyCaptureStartFrame = Time.frameCount;
            hotkeyCaptureIgnoredKeys.Clear();

            // Remember keys already held by the settings click. State polling is
            // reliable while Noesis owns keyboard focus, unlike GetKeyDown().
            foreach (KeyCode key in SupportedKeys)
            {
                if (TryGetKey(key, out bool pressed) && pressed)
                    hotkeyCaptureIgnoredKeys.Add(key);
            }

            if (KeyManager.instance != null)
                KeyManager.instance.HotKeySelectorMode = true;

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint hotkey capture armed; selectorMode=" +
                $"{KeyManager.instance?.HotKeySelectorMode ?? false}, " +
                $"ignoredHeldKeys={hotkeyCaptureIgnoredKeys.Count}.");
        }

        private void UpdateHotkeyCapture()
        {
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

            if (KeyManager.instance != null)
            {
                int rawKey = KeyManager.instance.HotKeyCurrentKey;
                KeyCode vanillaKey = (KeyCode)(rawKey & 0xFFFF);
                if (rawKey != 0 &&
                    vanillaKey != KeyCode.None &&
                    Array.IndexOf(SupportedKeys, vanillaKey) >= 0 &&
                    !hotkeyCaptureIgnoredKeys.Contains(vanillaKey))
                {
                    CompleteHotkeyCapture(vanillaKey, "Vanilla KeyManager");
                    return;
                }
            }

            foreach (KeyCode key in SupportedKeys)
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

                CompleteHotkeyCapture(key, "Unity held-state scan");
                return;
            }
        }

        private void CompleteHotkeyCapture(KeyCode key, string source)
        {
            StopHotkeyCapture();
            settings.CompleteHotkeyCapture(key);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint hotkey capture completed: key={key}, " +
                $"value={(int)key}, source={source}.");
        }

        private void StopHotkeyCapture()
        {
            hotkeyCapturePending = false;
            hotkeyCaptureIgnoredKeys.Clear();
            if (KeyManager.instance != null)
                KeyManager.instance.HotKeySelectorMode = false;
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
            nextPrepareAttemptTime = 0f;
            RefreshHud();
        }

        private void TryPrepareBlueprint()
        {
            nextPrepareAttemptTime = Time.unscaledTime + 0.25f;
            if (!TryFindControlledKeep(out int keepX, out int keepY))
                return;

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

        private bool TryBuildBlueprintLayout(
            int keepX,
            int keepY,
            string reason)
        {
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
                    Shared.DebugLogHelper.LogInfo(
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
                layout = BlueprintLayoutBuilder.Build(
                    document,
                    keepX,
                    keepY);
                layoutKeepX = keepX;
                layoutKeepY = keepY;
                renderer.PreloadDepthCaptures(layout);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Blueprint prepared locally: reason={reason}, " +
                    $"file={fullPath}, keep=({keepX},{keepY}), " +
                    $"tiles={layout.Tiles.Count}, icons={layout.Icons.Count}, " +
                    $"unknownMappers={layout.UnknownMapperCount}, " +
                    $"miscItemsIgnored={document.miscItems?.Count ?? 0}.");
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

        private void ToggleBlueprint()
        {
            if (!settings.IsBlueprintMode || !mapActive)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Blueprint toggle ignored because Blueprint mode or the map is inactive.");
                RefreshHud();
                return;
            }

            if (blueprintVisible)
            {
                SetBlueprintVisible(false, "HUD or configured hotkey");
                return;
            }

            // The editor can move or replace the local Keep without a map
            // reload, so every activation must project from its live position.
            if (!TryFindControlledKeep(out int keepX, out int keepY))
            {
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

            preparePending = false;
            showAfterPrepare = false;
            if (layout == null || layoutKeepX != keepX || layoutKeepY != keepY)
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
                Shared.DebugLogHelper.LogInfo(
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
            Shared.DebugLogHelper.LogInfo(
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
                Shared.DebugLogHelper.LogInfo(
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
            Hud?.ResetForMapLifecycle();
            renderer?.Clear();
            layout = null;
            layoutKeepX = int.MinValue;
            layoutKeepY = int.MinValue;
            preparePending = false;
            showAfterPrepare = false;
            blueprintVisible = false;
            lastRotation = int.MinValue;
            lastFlattenedLandscape = false;
            pendingViewSettleTime = -1f;
            suppressOverlayUntilViewSettled = false;
        }

        private void RefreshHud()
        {
            Hud?.Update(
                settings?.IsBlueprintMode == true,
                mapActive,
                layout != null,
                blueprintVisible,
                renderer?.CompletedDepthCaptureCount ?? 0,
                renderer?.RequestedDepthCaptureCount ?? 0);
        }

        private void EnsureEditorMapState()
        {
            bool editor = IsMapEditor();
            if (!editor)
            {
                if (editorSessionActive)
                {
                    ResetMapState();
                    mapActive = false;
                    editorSessionActive = false;
                    editorControlledPlayerId = -1;
                    RefreshHud();
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        "Blueprint editor lifecycle ended after leaving the map editor.");
                }
                return;
            }

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
            Shared.DebugLogHelper.LogInfo(
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

        private static bool IsMapEditor() => Shared.GameModeHelper.IsMapEditor();

        private static bool IsSimulationActive()
        {
            return Director.instance != null &&
                   Director.instance.SimRunning &&
                   GameMap.instance != null &&
                   TilemapManager.instance != null;
        }

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

        private static KeyCode[] CreateSupportedKeys()
        {
            var values = new List<KeyCode>();
            var seen = new HashSet<int>();
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                int value = (int)key;
                if (key != KeyCode.None && seen.Add(value))
                    values.Add(key);
            }

            return values.ToArray();
        }
    }
}
