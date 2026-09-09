using BepInEx.Logging;
using SHCDESE.NoesisUtil;
using System;
using System.ComponentModel;
using System.Linq;
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

        public VirtualSpawnController(VirtualEntityRuntime runtime, ManualLogSource log)
        {
            this.runtime = runtime; this.log = log;
            Hud = new VirtualSpawnHudViewModel(SelectUnit, SelectBuilding, Cancel);
        }
        public VirtualSpawnHudViewModel Hud { get; }
        public void Initialize() { Application.onBeforeRender += OnBeforeRender; SetAvailability(false); }
        public void SetAvailability(bool available) { if (!available) Cancel(); Hud.SetAvailability(available); }
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
        private void Cancel() { selection = SelectionKind.None; selectedTypeId = null; waitForRelease = false; Hud?.SetSelection("Keine"); }

        private void OnBeforeRender()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try { Tick(); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, $"HUD frame loop recovered from an error: {ex}"); Cancel(); }
        }
        private void Tick()
        {
            runtime.Tick();
            if (!runtime.CanMutate || selection == SelectionKind.None) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { Cancel(); Hud.SetResult("Platzierung abgebrochen."); return; }
            if (waitForRelease)
            {
                if (!Input.GetMouseButton(0)) waitForRelease = false;
                return;
            }
            if (!Input.GetMouseButtonDown(0)) return;
            if (MainControls.instance == null || MainControls.instance.overGUI || MainControls.instance.isOffWorld()) return;
            float mouseX = 0f, mouseY = 0f;
            MainControls.instance.getMouseMapTilePosition(ref mouseX, ref mouseY);
            int tileX = (int)mouseX, tileY = (int)mouseY;
            VirtualApiResult result = selection == SelectionKind.Unit
                ? VirtualEntityApi.SpawnVirtualUnit(selectedTypeId, tileX, tileY, out VirtualEntityInstance unit)
                : VirtualEntityApi.SpawnVirtualBuilding(selectedTypeId, tileX, tileY, out VirtualEntityInstance building);
            Hud.SetTarget(tileX, tileY);
            Hud.SetResult(result.Code == VirtualApiResultCode.InitializationPending ? $"Initialisierung läuft: {result.Message}" : result.ToString());
            Shared.DebugLogHelper.LogInfo(log, $"Diagnostic placement: selection={selection}, type={selectedTypeId}, tile={tileX},{tileY}, result={result}.");
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
