// Feature: Show health totals for each visible selected troop type in the HUD.
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

        public void Show(SelectedUnitHealthSummary summary)
        {
            CurrentText = summary.FormatCurrent();
            MaximumText = summary.FormatMaximum();
            CurrentForeground = summary.Band == SelectedUnitHealthBand.Green
                ? GreenBrush
                : summary.Band == SelectedUnitHealthBand.Yellow
                    ? YellowBrush
                    : RedBrush;
        }

        public void Clear()
        {
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
                    Slots[slot].Show(summaries[type]);
                    anyVisible = true;
                }
                else
                {
                    Slots[slot].Clear();
                }
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

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private int lastFrame = -1;
        private bool callbackErrorLogged;
        private bool disposed;

        public SelectedUnitHealthFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
            if (!settings.EnableClientFeatures ||
                !settings.ShowSelectedUnitHealth ||
                mainViewModel == null ||
                !mainViewModel.Show_HUD_Troops ||
                troopPanel == null ||
                GameData.Instance == null)
            {
                ViewModel.Hide();
                return;
            }

            EngineInterface.PlayState state = GameData.Instance.lastGameState;
            int selectedCount = state.numSelectedChimps;
            if (selectedCount <= 0 || state.selectedChimps == null)
            {
                ViewModel.Hide();
                return;
            }

            var summaries = new SelectedUnitHealthSummary[(int)eChimps.CHIMP_NUM_TYPES];
            int count = Math.Min(selectedCount, state.selectedChimps.Length);
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            for (int i = 0; i < count; i++)
            {
                int unitId = state.selectedChimps[i];
                if (unitId <= 0 ||
                    !unitApi.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    continue;
                }

                int type = (int)unit->r_UnitChimp;
                if (type < 0 || type >= summaries.Length)
                    continue;

                summaries[type].Add(unit->r_CurrentHealth, unit->r_MaxHealth);
            }

            if (SelectedChimpArrayField == null || CurrentPageField == null)
                throw new MissingFieldException("HUD_Troops selected-type paging fields were not found.");

            var selectedTypeCounts = SelectedChimpArrayField.GetValue(troopPanel) as int[];
            int currentPage = (int)CurrentPageField.GetValue(troopPanel);
            int[] visibleTypes = SelectedUnitHealthPageLayout.GetVisibleTypes(
                selectedTypeCounts,
                currentPage);
            ViewModel.Show(summaries, visibleTypes);
        }
    }
}
