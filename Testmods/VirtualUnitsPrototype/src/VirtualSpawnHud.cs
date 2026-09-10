using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.NoesisUtil;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using UnityEngine;
using VirtualUnitsPrototype.API;

namespace VirtualUnitsPrototype
{
    internal sealed class VirtualSpawnController
    {
        private readonly VirtualEntityRuntime runtime;
        private readonly ManualLogSource log;
        private SelectionKind selection;
        private string selectedTypeId;
        private bool waitForRelease;
        private int lastFrame = -1;
        private int lastHudMouseDownFrame = -1;
        private Noesis.FrameworkElement hudHost;
        private Noesis.FrameworkElement hudToggle;
        private Noesis.FrameworkElement hudPanel;
        private WorldClickCandidate pendingWorldClick;
        private string lastRejectedReason;
        private bool unityThreadLogged;

        public VirtualSpawnController(VirtualEntityRuntime runtime, ManualLogSource log)
        {
            this.runtime = runtime; this.log = log;
            Hud = new VirtualSpawnHudViewModel(SelectUnit, SelectBuilding, Cancel);
        }
        public VirtualSpawnHudViewModel Hud { get; }
        public void Initialize()
        {
            Application.onBeforeRender += OnBeforeRender;
            Application.focusChanged += OnFocusChanged;
            ApplyAvailability(false);
        }
        public void ResetForMapLifecycle() { pendingWorldClick = null; DetachHud(); Cancel(); }
        public void ApplyAvailability(bool available) { if (!available) Cancel(); Hud.SetAvailability(available); }
        public void ReportRuntimeResult(VirtualApiResult result) { Hud.SetResult(result.ToString()); }

        private void SelectUnit()
        {
            VirtualUnitDefinition definition = runtime.VisibleUnits().FirstOrDefault();
            if (definition == null) { Hud.SetResult("Keine Unit-Definition verfügbar."); return; }
            selection = SelectionKind.Unit; selectedTypeId = definition.TypeId; waitForRelease = true; Hud.SetSelection(definition.DisplayName);
        }
        private void SelectBuilding()
        {
            VirtualBuildingDefinition definition = runtime.VisibleBuildings().FirstOrDefault();
            if (definition == null) { Hud.SetResult("Keine Gebäude-Definition verfügbar."); return; }
            selection = SelectionKind.Building; selectedTypeId = definition.TypeId; waitForRelease = true; Hud.SetSelection(definition.DisplayName);
        }
        private void Cancel() { selection = SelectionKind.None; selectedTypeId = null; waitForRelease = false; pendingWorldClick = null; Hud?.SetSelection("Keine"); }

        private void OnBeforeRender()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try { Tick(); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, $"HUD frame loop recovered from an error: {ex}"); Cancel(); }
        }
        private void Tick()
        {
            if (!unityThreadLogged)
            {
                unityThreadLogged = true;
                Shared.DebugLogHelper.LogInfo(log, $"Unity input and completion context established: thread={Thread.CurrentThread.ManagedThreadId}.");
            }
            runtime.DrainMainThreadWork();
            EnsureHudAttached();
            EvaluatePendingWorldClick();
            if (!runtime.CanMutate || selection == SelectionKind.None) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { Cancel(); Hud.SetResult("Platzierung abgebrochen."); return; }
            if (waitForRelease)
            {
                if (!Input.GetMouseButton(0)) waitForRelease = false;
                return;
            }
            if (!Input.GetMouseButtonDown(0) || pendingWorldClick != null) return;
            pendingWorldClick = new WorldClickCandidate(Time.frameCount, selection, selectedTypeId, Input.mousePosition);
        }

        private void EvaluatePendingWorldClick()
        {
            WorldClickCandidate candidate = pendingWorldClick;
            if (candidate == null || Time.frameCount <= candidate.Frame) return;
            pendingWorldClick = null;
            if (!runtime.CanMutate) { LogRejected(candidate, "runtime mutation is unavailable"); return; }
            if (candidate.Selection == SelectionKind.None) { LogRejected(candidate, "no placement type is selected"); return; }
            if (candidate.Frame <= lastHudMouseDownFrame) { LogRejected(candidate, "mouse-down originated inside an interactive VUP surface"); return; }
            MainViewModel viewModel = MainViewModel.Instance;
            if (!IsGameplayHudClear(viewModel)) { LogRejected(candidate, DescribeHudBlock(viewModel)); return; }
            if (MainControls.instance == null) { LogRejected(candidate, "MainControls is unavailable"); return; }
            if (MainControls.instance.overGUI) { LogRejected(candidate, "Vanilla reports overGUI"); return; }
            if (MainControls.instance.isOffWorld()) { LogRejected(candidate, "Vanilla reports an off-world pointer"); return; }
            float mouseX = 0f, mouseY = 0f;
            MainControls.instance.getMouseMapTilePosition(ref mouseX, ref mouseY);
            int internalX = (int)mouseX, internalY = (int)mouseY;
            GameMapTile mapTile = GameMap.instance?.getMapTile(internalX, internalY);
            if (mapTile == null || GameTileManagerAPI.Instance == null ||
                !GameTileManagerAPI.Instance.IsTileInsideMapBounds(mapTile.gameMapX, mapTile.gameMapY))
            {
                Hud.SetResult("Ungültige Kartenposition.");
                Shared.DebugLogHelper.LogWarning(log, $"Diagnostic placement rejected: screen={candidate.ScreenPosition}, internalTile={internalX},{internalY}, localTile=<unresolved>.");
                return;
            }
            int tileX = mapTile.gameMapX, tileY = mapTile.gameMapY;
            int tileId = GameTileManagerAPI.Instance.GetTileId(tileX, tileY);
            VirtualApiResult result = candidate.Selection == SelectionKind.Unit
                ? VirtualEntityApi.QueueVirtualUnitSpawn(candidate.TypeId, tileX, tileY, out VirtualOperationTicket unitTicket)
                : VirtualEntityApi.QueueVirtualBuildingSpawn(candidate.TypeId, tileX, tileY, out VirtualOperationTicket buildingTicket);
            Hud.SetTarget(tileX, tileY);
            Hud.SetResult(result.Code == VirtualApiResultCode.InitializationPending ? $"Initialisierung läuft: {result.Message}" : result.ToString());
            lastRejectedReason = null;
            Shared.DebugLogHelper.LogInfo(log, $"Diagnostic placement: selection={candidate.Selection}, type={candidate.TypeId}, screen={candidate.ScreenPosition}, internalTile={internalX},{internalY}, localTile={tileX},{tileY}, tileId={tileId}, sourceFrame={candidate.Frame}, evaluationFrame={Time.frameCount}, result={result}.");
        }

        private static bool IsGameplayHudClear(MainViewModel viewModel)
        {
            return viewModel != null && viewModel.Show_HUD_Main && !viewModel.Show_BlackOut &&
                !viewModel.Show_HUD_Briefing && !viewModel.Show_HUD_IngameMenu &&
                !viewModel.Show_HUD_FrontEndBlackout && !viewModel.Show_HUD_MissionOver;
        }

        private static string DescribeHudBlock(MainViewModel viewModel) => viewModel == null
            ? "MainViewModel is unavailable"
            : $"gameplay HUD blocked: main={viewModel.Show_HUD_Main}, blackOut={viewModel.Show_BlackOut}, briefing={viewModel.Show_HUD_Briefing}, ingameMenu={viewModel.Show_HUD_IngameMenu}, frontEndBlackout={viewModel.Show_HUD_FrontEndBlackout}, missionOver={viewModel.Show_HUD_MissionOver}";

        private void LogRejected(WorldClickCandidate candidate, string reason)
        {
            if (string.Equals(lastRejectedReason, reason, StringComparison.Ordinal)) return;
            lastRejectedReason = reason;
            Shared.DebugLogHelper.LogInfo(log, $"World-click candidate rejected: sourceFrame={candidate.Frame}, evaluationFrame={Time.frameCount}, screen={candidate.ScreenPosition}, reason={reason}.");
        }

        private void EnsureHudAttached()
        {
            Noesis.FrameworkElement candidate = GameXAMLManagerAPI.Instance?.FindGlobalElement("VirtualUnitsPrototypeHud");
            if (ReferenceEquals(candidate, hudHost) && hudToggle != null && hudPanel != null) return;
            DetachHud();
            hudHost = candidate;
            if (hudHost == null) return;
            hudToggle = hudHost.FindName("VirtualUnitsPrototypeHudToggle") as Noesis.FrameworkElement ??
                GameXAMLManagerAPI.Instance.FindGlobalElement("VirtualUnitsPrototypeHudToggle");
            hudPanel = hudHost.FindName("VirtualUnitsPrototypeHudPanel") as Noesis.FrameworkElement ??
                GameXAMLManagerAPI.Instance.FindGlobalElement("VirtualUnitsPrototypeHudPanel");
            AttachInteractiveSurface(hudToggle);
            AttachInteractiveSurface(hudPanel);
            Shared.DebugLogHelper.LogInfo(log, $"VUP interactive HUD surfaces attached: host={hudHost.ActualWidth:0.0}x{hudHost.ActualHeight:0.0}, toggle={DescribeSize(hudToggle)}, panel={DescribeSize(hudPanel)}.");
        }

        private void DetachHud()
        {
            DetachInteractiveSurface(hudToggle);
            DetachInteractiveSurface(hudPanel);
            hudToggle = null;
            hudPanel = null;
            hudHost = null;
        }

        private void AttachInteractiveSurface(Noesis.FrameworkElement surface)
        {
            if (surface == null) return;
            surface.PreviewMouseDown += OnHudMouseDown;
            surface.PreviewMouseUp += OnHudMouseUp;
        }

        private void DetachInteractiveSurface(Noesis.FrameworkElement surface)
        {
            if (surface == null) return;
            surface.PreviewMouseDown -= OnHudMouseDown;
            surface.PreviewMouseUp -= OnHudMouseUp;
        }

        private static string DescribeSize(Noesis.FrameworkElement element) => element == null ? "missing" : $"{element.ActualWidth:0.0}x{element.ActualHeight:0.0}";

        private void OnHudMouseDown(object sender, Noesis.MouseButtonEventArgs args)
        {
            lastHudMouseDownFrame = Time.frameCount;
            pendingWorldClick = null;
        }

        private void OnHudMouseUp(object sender, Noesis.MouseButtonEventArgs args) { }
        private void OnFocusChanged(bool focused) { if (!focused) { pendingWorldClick = null; waitForRelease = true; } }

        private sealed class WorldClickCandidate
        {
            public WorldClickCandidate(int frame, SelectionKind selection, string typeId, Vector3 screenPosition)
            { Frame = frame; Selection = selection; TypeId = typeId; ScreenPosition = screenPosition; }
            public int Frame { get; }
            public SelectionKind Selection { get; }
            public string TypeId { get; }
            public Vector3 ScreenPosition { get; }
        }
        private enum SelectionKind { None, Unit, Building }
    }

    internal sealed class VirtualSpawnHudViewModel : INotifyPropertyChanged
    {
        private bool available;
        private bool panelVisible;
        private string selection = "Keine";
        private string target = "-";
        private string result = "Bereit";

        public VirtualSpawnHudViewModel(Action selectUnit, Action selectBuilding, Action cancel)
        {
            TogglePanelCommand = new RelayCommand(() => PanelVisible = !PanelVisible);
            SelectUnitCommand = new RelayCommand(selectUnit);
            SelectBuildingCommand = new RelayCommand(selectBuilding);
            CancelCommand = new RelayCommand(cancel);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public ICommand TogglePanelCommand { get; }
        public ICommand SelectUnitCommand { get; }
        public ICommand SelectBuildingCommand { get; }
        public ICommand CancelCommand { get; }
        public bool Available { get => available; private set { if (available == value) return; available = value; OnChanged(nameof(Available)); OnChanged(nameof(HudVisible)); if (!value) PanelVisible = false; } }
        public bool HudVisible => Available;
        public bool PanelVisible { get => panelVisible; set { if (panelVisible == value) return; panelVisible = value; OnChanged(nameof(PanelVisible)); } }
        public string SelectionText => "Auswahl: " + selection;
        public string TargetText => "Ziel: " + target;
        public string ResultText => "Ergebnis: " + result;
        public void SetAvailability(bool value) => Available = value;
        public void SetSelection(string value) { selection = value ?? "Keine"; OnChanged(nameof(SelectionText)); }
        public void SetTarget(int x, int y) { target = $"{x}, {y}"; OnChanged(nameof(TargetText)); }
        public void SetResult(string value) { result = value ?? string.Empty; OnChanged(nameof(ResultText)); }
        private void OnChanged(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
