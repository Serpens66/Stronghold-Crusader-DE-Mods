// Feature: Show health totals for each visible selected troop type in the HUD.
using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.ViewModels;
using System;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class SelectedUnitHealthSlotViewModel : LobbyModSettingsBaseViewModel
    {
        private static readonly SolidColorBrush GreenBrush =
            new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, 102, 204, 102));
        private static readonly SolidColorBrush YellowBrush =
            new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, 255, 214, 102));
        private static readonly SolidColorBrush RedBrush =
            new SolidColorBrush(Noesis.Color.FromArgb(byte.MaxValue, 255, 102, 102));

        private string currentText = string.Empty;
        private string maximumText = string.Empty;
        private Brush currentForeground = GreenBrush;
        private long displayedCurrent = long.MinValue;
        private long displayedMaximum = long.MinValue;
        private int displayedType = -1;
        private SelectedUnitHealthBand displayedBand;
        private bool hasDisplayedSummary;

        internal static Brush GetBandBrush(SelectedUnitHealthBand band) =>
            band == SelectedUnitHealthBand.Green
                ? GreenBrush
                : band == SelectedUnitHealthBand.Yellow
                    ? YellowBrush
                    : RedBrush;

        public string CurrentText
        {
            get => currentText;
            private set
            {
                if (currentText == value)
                    return;
                currentText = value;
                OnPropertyChanged(nameof(CurrentText));
            }
        }

        public string MaximumText
        {
            get => maximumText;
            private set
            {
                if (maximumText == value)
                    return;
                maximumText = value;
                OnPropertyChanged(nameof(MaximumText));
            }
        }

        public Brush CurrentForeground
        {
            get => currentForeground;
            private set
            {
                if (ReferenceEquals(currentForeground, value))
                    return;
                currentForeground = value;
                OnPropertyChanged(nameof(CurrentForeground));
            }
        }

        public void Show(int type, SelectedUnitHealthSummary summary)
        {
            long current = SelectedUnitHealthSummary.ScaleForDisplay(summary.CurrentHealth);
            long maximum = SelectedUnitHealthSummary.ScaleForDisplay(summary.MaximumHealth);
            SelectedUnitHealthBand band = summary.Band;
            if (hasDisplayedSummary && displayedType == type && displayedCurrent == current &&
                displayedMaximum == maximum && displayedBand == band)
            {
                return;
            }

            displayedType = type;
            displayedCurrent = current;
            displayedMaximum = maximum;
            displayedBand = band;
            hasDisplayedSummary = true;
            CurrentText = current.ToString();
            MaximumText = maximum.ToString();
            CurrentForeground = GetBandBrush(band);
        }

        public void Clear()
        {
            if (!hasDisplayedSummary && currentText.Length == 0 && maximumText.Length == 0)
                return;

            hasDisplayedSummary = false;
            displayedType = -1;
            displayedCurrent = long.MinValue;
            displayedMaximum = long.MinValue;
            CurrentText = string.Empty;
            MaximumText = string.Empty;
        }
    }

    internal sealed class SelectedUnitHealthViewModel : LobbyModSettingsBaseViewModel
    {
        private Visibility healthVisibility = Visibility.Collapsed;
        public SelectedUnitHealthViewModel()
        {
            Slots = new SelectedUnitHealthSlotViewModel[SelectedUnitHealthPageLayout.SlotCount];
            for (int i = 0; i < Slots.Length; i++)
                Slots[i] = new SelectedUnitHealthSlotViewModel();
        }

        public SelectedUnitHealthSlotViewModel[] Slots { get; }
        public SelectedUnitHealthSlotViewModel Slot1 => Slots[0];
        public SelectedUnitHealthSlotViewModel Slot2 => Slots[1];
        public SelectedUnitHealthSlotViewModel Slot3 => Slots[2];
        public SelectedUnitHealthSlotViewModel Slot4 => Slots[3];
        public SelectedUnitHealthSlotViewModel Slot5 => Slots[4];
        public SelectedUnitHealthSlotViewModel Slot6 => Slots[5];
        public SelectedUnitHealthSlotViewModel Slot7 => Slots[6];
        public SelectedUnitHealthSlotViewModel Slot8 => Slots[7];

        public Visibility HealthVisibility
        {
            get => healthVisibility;
            private set
            {
                if (healthVisibility == value)
                    return;
                healthVisibility = value;
                OnPropertyChanged(nameof(HealthVisibility));
            }
        }

        public void Show(SelectedUnitHealthSummary[] summaries, int[] visibleTypes)
        {
            bool anyVisible = false;
            for (int slot = 0; slot < Slots.Length; slot++)
            {
                int type = visibleTypes != null && slot < visibleTypes.Length ? visibleTypes[slot] : -1;
                if (type >= 0 && type < summaries.Length && summaries[type].HasUnits)
                {
                    Slots[slot].Show(type, summaries[type]);
                    anyVisible = true;
                }
                else
                {
                    Slots[slot].Clear();
                }
            }

            HealthVisibility = anyVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ShowSlots(SelectedUnitHealthSummary[] summaries)
        {
            bool anyVisible = false;
            for (int slot = 0; slot < Slots.Length; slot++)
            {
                if (summaries != null && slot < summaries.Length && summaries[slot].HasUnits)
                {
                    Slots[slot].Show(int.MinValue + slot, summaries[slot]);
                    anyVisible = true;
                }
                else Slots[slot].Clear();
            }
            HealthVisibility = anyVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public void Hide()
        {
            HealthVisibility = Visibility.Collapsed;
            for (int i = 0; i < Slots.Length; i++)
                Slots[i].Clear();
        }
    }

    internal sealed unsafe class SelectedUnitHealthFeature : IDisposable
    {
        private static readonly FieldInfo SelectedChimpArrayField = typeof(HUD_Troops).GetField(
            "SelectedChimpArray",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo CurrentPageField = typeof(HUD_Troops).GetField(
            "currentPage",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo NoSelectedChimpTypesField = typeof(HUD_Troops).GetField(
            "NoSelectedChimpTypes",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Func<IUnitHudPresentationCapability> getPresentation;
        private readonly SelectedUnitHealthSummary[] summaries =
            new SelectedUnitHealthSummary[(int)eChimps.CHIMP_NUM_TYPES];
        private readonly int[] visibleTypes =
            new int[SelectedUnitHealthPageLayout.SlotCount];
        private int lastFrame = -1;
        private bool callbackErrorLogged;
        private bool disposed;
        private string lastEditorVisibilityDiagnostic;

        public SelectedUnitHealthFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            Func<IUnitHudPresentationCapability> getPresentation = null)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.getPresentation = getPresentation;
            ViewModel = new SelectedUnitHealthViewModel();

            // The BepInEx component is short-lived, but this static Unity event remains available in game.
            Application.onBeforeRender += OnBeforeRender;
        }

        public SelectedUnitHealthViewModel ViewModel { get; }

        public void RefreshSetting()
        {
            if (!settings.EnableClientFeatures || !settings.ShowSelectedUnitHealth)
                ViewModel.Hide();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Application.onBeforeRender -= OnBeforeRender;
            ViewModel.Hide();
        }

        private void OnBeforeRender()
        {
            if (disposed || lastFrame == Time.frameCount)
                return;
            lastFrame = Time.frameCount;

            try
            {
                Refresh();
            }
            catch (Exception ex)
            {
                ViewModel.Hide();
                if (!callbackErrorLogged)
                {
                    callbackErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL selected-unit health refresh failed; the display remains hidden: {ex}");
                }
            }
        }

        private void Refresh()
        {
            MainViewModel mainViewModel = MainViewModel.Instance;
            HUD_Troops troopPanel = mainViewModel?.HUDTroopPanel;
            bool mapEditor = Shared.GameModeHelper.IsMapEditor();
            if (!settings.EnableClientFeatures ||
                !settings.ShowSelectedUnitHealth ||
                mainViewModel == null ||
                !mainViewModel.Show_HUD_Troops ||
                troopPanel == null ||
                GameData.Instance == null)
            {
                ViewModel.Hide();
                LogEditorVisibilityState(mapEditor, "hidden: feature-or-troop-panel-unavailable");
                return;
            }

            EngineInterface.PlayState state = GameData.Instance.lastGameState;
            int selectedCount = state.numSelectedChimps;
            if (selectedCount <= 0 || state.selectedChimps == null)
            {
                ViewModel.Hide();
                LogEditorVisibilityState(mapEditor, "hidden: no-selected-units");
                return;
            }

            int controlledPlayerId = mapEditor
                ? (EditorDirector.instance?.ActivePlayerID ?? -1)
                : -1;
            if (mapEditor && (controlledPlayerId < 1 || controlledPlayerId > 8))
            {
                ViewModel.Hide();
                LogEditorVisibilityState(true, $"hidden: invalid-active-player-{controlledPlayerId}");
                return;
            }

            Array.Clear(summaries, 0, summaries.Length);
            int count = Math.Min(selectedCount, state.selectedChimps.Length);
            int eligibleCount = 0;
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            for (int i = 0; i < count; i++)
            {
                int unitId = state.selectedChimps[i];
                if (unitId <= 0 ||
                    !unitApi.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive ||
                    (mapEditor && unit->r_ControllableForPlayerId != controlledPlayerId))
                {
                    continue;
                }

                int type = (int)unit->r_UnitChimp;
                if (type < 0 || type >= summaries.Length)
                    continue;

                summaries[type].Add(unit->r_CurrentHealth, unit->r_MaxHealth);
                eligibleCount++;
            }

            if (SelectedChimpArrayField == null || CurrentPageField == null ||
                NoSelectedChimpTypesField == null)
                throw new MissingFieldException("HUD_Troops selected-type paging fields were not found.");

            var selectedTypeCounts = SelectedChimpArrayField.GetValue(troopPanel) as int[];
            int displayedTypeCount = (int)NoSelectedChimpTypesField.GetValue(troopPanel);
            int excludedType = -1;
            if (selectedTypeCounts != null &&
                selectedTypeCounts.Length > (int)eChimps.CHIMP_TYPE_LORD &&
                selectedTypeCounts[(int)eChimps.CHIMP_TYPE_LORD] > 0 &&
                displayedTypeCount != SelectedUnitHealthPageLayout.CountVisibleTypes(selectedTypeCounts))
            {
                // Vanilla excludes type 55. Only mirror it when the Lord-aware HUD hook
                // has explicitly included it in the same authoritative type count.
                excludedType = (int)eChimps.CHIMP_TYPE_LORD;
            }
            int currentPage = (int)CurrentPageField.GetValue(troopPanel);
            IUnitHudPresentationCapability presentation = getPresentation?.Invoke();
            if (presentation != null && TryShowApiSlots(presentation, state, count, mapEditor, controlledPlayerId))
            {
                LogEditorVisibilityState(mapEditor, $"visible via APIShared: selectedUnits={selectedCount}, playerId={controlledPlayerId}");
                return;
            }
            SelectedUnitHealthPageLayout.FillVisibleTypes(
                selectedTypeCounts,
                currentPage,
                visibleTypes,
                excludedType);
            ViewModel.Show(summaries, visibleTypes);
            LogEditorVisibilityState(mapEditor, $"visible: selectedUnits={selectedCount}, eligibleOwnedUnits={eligibleCount}, playerId={controlledPlayerId}, page={currentPage}");
        }

        private bool TryShowApiSlots(IUnitHudPresentationCapability presentation, EngineInterface.PlayState state, int count, bool mapEditor, int controlledPlayerId)
        {
            var slots = presentation.GetVisibleTroopSlots();
            if (slots == null || slots.Count == 0) return false;
            var customIds = new System.Collections.Generic.HashSet<int>();
            foreach (UnitHudCategorySnapshot category in presentation.GetSelectedCategories())
                foreach (UnitHudUnitSnapshot unit in category.Units) customIds.Add(unit.GameId);
            var slotSummaries = new SelectedUnitHealthSummary[SelectedUnitHealthPageLayout.SlotCount];
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            foreach (UnitHudSlotSnapshot slot in slots)
            {
                if (slot.Slot < 0 || slot.Slot >= slotSummaries.Length) continue;
                if (slot.Category != null)
                {
                    foreach (UnitHudUnitSnapshot item in slot.Category.Units) AddHealth(item.GameId, ref slotSummaries[slot.Slot], unitApi, mapEditor, controlledPlayerId);
                    continue;
                }
                for (int index = 0; index < count; index++)
                {
                    int unitId = state.selectedChimps[index];
                    if (customIds.Contains(unitId)) continue;
                    if (unitApi.TryGetUnitById(unitId, out GameUnit* unit) && unit != null && (int)unit->r_UnitChimp == slot.VanillaType)
                        AddHealth(unitId, ref slotSummaries[slot.Slot], unitApi, mapEditor, controlledPlayerId);
                }
            }
            ViewModel.ShowSlots(slotSummaries);
            return true;
        }

        private static void AddHealth(int unitId, ref SelectedUnitHealthSummary summary, GameUnitManagerAPI unitApi, bool mapEditor, int controlledPlayerId)
        {
            if (unitId <= 0 || unitApi == null || !unitApi.TryGetUnitById(unitId, out GameUnit* unit) || unit == null || unit->r_AliveState != AliveState.IsAlive || (mapEditor && unit->r_ControllableForPlayerId != controlledPlayerId)) return;
            summary.Add(unit->r_CurrentHealth, unit->r_MaxHealth);
        }

        private void LogEditorVisibilityState(bool mapEditor, string state)
        {
            if (!mapEditor || string.Equals(lastEditorVisibilityDiagnostic, state, StringComparison.Ordinal))
                return;
            lastEditorVisibilityDiagnostic = state;
            Shared.DebugLogHelper.LogDebug(log, $"Bugfixes and QoL selected-unit health editor state: {state}.");
        }

    }
}
