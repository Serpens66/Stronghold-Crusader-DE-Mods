// Feature: Show the summed health of the current troop selection in the HUD.
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.ViewModels;
using System;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class SelectedUnitHealthViewModel : LobbyModSettingsBaseViewModel
    {
        private string healthText = string.Empty;
        private Visibility healthVisibility = Visibility.Collapsed;

        public string HealthText
        {
            get => healthText;
            private set
            {
                if (healthText == value)
                    return;
                healthText = value;
                OnPropertyChanged(nameof(HealthText));
            }
        }

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

        public void Show(string text)
        {
            HealthText = text;
            HealthVisibility = Visibility.Visible;
        }

        public void Hide()
        {
            HealthVisibility = Visibility.Collapsed;
            HealthText = string.Empty;
        }
    }

    internal sealed unsafe class SelectedUnitHealthFeature : IDisposable
    {
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
            if (!settings.EnableClientFeatures ||
                !settings.ShowSelectedUnitHealth ||
                mainViewModel == null ||
                !mainViewModel.Show_HUD_Troops ||
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

            int count = Math.Min(selectedCount, state.selectedChimps.Length);
            var summary = new SelectedUnitHealthSummary();
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

                summary.Add(unit->r_CurrentHealth, unit->r_MaxHealth);
            }

            if (summary.HasUnits)
                ViewModel.Show(summary.Format());
            else
                ViewModel.Hide();
        }
    }
}
