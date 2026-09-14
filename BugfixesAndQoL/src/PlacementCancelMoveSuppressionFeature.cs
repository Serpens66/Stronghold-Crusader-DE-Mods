// Feature: Right-click cancels building placement without issuing a move order.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class PlacementCancelMoveSuppressionFeature
    {
        private delegate void StopAllPlacementDelegate(MainControls self);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly PlacementCancelMoveSuppressionState state =
            new PlacementCancelMoveSuppressionState();
        private Hook stopAllPlacementHook;
        private StopAllPlacementDelegate stopAllPlacementOriginal;
        private IDisposable moveOrderSubscription;
        private IDisposable mapLoadSubscription;
        private IDisposable mapUnloadSubscription;
        private bool captureFailureLogged;
        private bool moveFailureLogged;

        public PlacementCancelMoveSuppressionFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Install()
        {
            if (stopAllPlacementHook != null)
                return;

            Hook installedHook = null;
            IDisposable installedMoveOrderSubscription = null;
            IDisposable installedMapLoadSubscription = null;
            IDisposable installedMapUnloadSubscription = null;
            bool installedSettingsSubscription = false;
            try
            {
                installedHook = new Hook(
                    FindStopAllPlacementMethod(),
                    (StopAllPlacementDelegate)StopAllPlacementHook);
                stopAllPlacementOriginal =
                    installedHook.GenerateTrampoline<StopAllPlacementDelegate>();
                installedMoveOrderSubscription =
                    TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                        .Subscribe(OnMoveOrder);
                installedMapLoadSubscription =
                    MapLoaderR3EventHooks.OnLoadMap.Observable.Subscribe(_ => state.Clear());
                installedMapUnloadSubscription =
                    MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(_ => state.Clear());
                settings.SettingChanged += OnSettingChanged;
                installedSettingsSubscription = true;

                stopAllPlacementHook = installedHook;
                moveOrderSubscription = installedMoveOrderSubscription;
                mapLoadSubscription = installedMapLoadSubscription;
                mapUnloadSubscription = installedMapUnloadSubscription;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "Bugfixes and QoL placement-cancel move suppression installed with event-driven callbacks only.");
            }
            catch
            {
                if (installedSettingsSubscription)
                    settings.SettingChanged -= OnSettingChanged;
                installedMapUnloadSubscription?.Dispose();
                installedMapLoadSubscription?.Dispose();
                installedMoveOrderSubscription?.Dispose();
                installedHook?.Undo();
                installedHook?.Dispose();
                stopAllPlacementOriginal = null;
                throw;
            }
        }

        private static MethodInfo FindStopAllPlacementMethod()
        {
            MethodInfo method = typeof(MainControls).GetMethod(
                nameof(MainControls.StopAllPlacement),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (method == null)
                throw new MissingMethodException(typeof(MainControls).FullName, nameof(MainControls.StopAllPlacement));
            return method;
        }

        private void StopAllPlacementHook(MainControls self)
        {
            try
            {
                ObservePlacementCancellation(self);
            }
            catch (Exception ex)
            {
                state.Clear();
                if (!captureFailureLogged)
                {
                    captureFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL could not classify a placement-cancel click; Vanilla handling continues: {ex}");
                }
            }

            stopAllPlacementOriginal(self);
        }

        private unsafe void ObservePlacementCancellation(MainControls controls)
        {
            if (!Input.GetMouseButtonDown(1))
                return;

            // StopAllPlacement is called for every gameplay right-click. Replacing the
            // marker here guarantees that a later ordinary click cannot inherit it.
            state.Clear();
            if (!IsEnabled || controls == null || controls.CurrentAction != 5 ||
                ConfigSettings.Settings_SH1RTSControls)
            {
                return;
            }

            EditorDirector director = EditorDirector.instance;
            if (director == null || director.overUI())
                return;

            int localPlayerId = director.ActivePlayerID;
            if (localPlayerId <= 0)
                return;

            SelectedUnitInfo[] selected =
                GamePlayerManagerAPI.Instance.GetSelectedChimps() ?? Array.Empty<SelectedUnitInfo>();
            var identities = new List<PlacementCancelUnitIdentity>(selected.Length);
            foreach (SelectedUnitInfo selectedUnit in selected)
            {
                int unitId = selectedUnit.UnitId;
                if (unitId <= 0 ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_ControllableForPlayerId != localPlayerId)
                {
                    continue;
                }

                identities.Add(new PlacementCancelUnitIdentity(unitId, unit->r_GlobalId));
            }

            state.Replace(localPlayerId, identities);
        }

        private unsafe void OnMoveOrder(TribeIssueOrderMoveHereEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre || !args.IsNewOrder || args.SkipOriginalFunction)
                return;

            try
            {
                if (!IsEnabled)
                {
                    state.Clear();
                    return;
                }

                EditorDirector director = EditorDirector.instance;
                int localPlayerId = director?.ActivePlayerID ?? -1;
                if (localPlayerId <= 0 ||
                    !GameTribeManagerAPI.Instance.TryGetTribeById(args.TribeId, out GameTribe* tribe) ||
                    tribe == null || tribe->r_AliveState != AliveState.IsAlive ||
                    tribe->r_PlayerIdOwner != localPlayerId)
                {
                    return;
                }

                var tribeUnitIds = new List<int>();
                if (!GameTribeManagerAPI.Instance.GetUnits(args.TribeId, tribeUnitIds))
                    return;

                var identities = new List<PlacementCancelUnitIdentity>(tribeUnitIds.Count);
                foreach (int unitId in tribeUnitIds)
                {
                    if (unitId <= 0 ||
                        !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                        unit == null || unit->r_AliveState != AliveState.IsAlive ||
                        unit->r_ControllableForPlayerId != localPlayerId)
                    {
                        continue;
                    }

                    identities.Add(new PlacementCancelUnitIdentity(unitId, unit->r_GlobalId));
                }

                if (!state.TryConsumeMatchingGroup(
                        localPlayerId,
                        identities,
                        out int matchedCount,
                        out int remainingCount))
                {
                    return;
                }

                args.SkipOriginalFunction = true;
                args.ReturnValue = 0;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    () => $"Bugfixes and QoL suppressed placement-cancel move order: tribe={args.TribeId}, matchedUnits={matchedCount}, remainingUnits={remainingCount}.");
            }
            catch (Exception ex)
            {
                state.Clear();
                if (!moveFailureLogged)
                {
                    moveFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL placement-cancel move suppression failed; Vanilla move handling continues: {ex}");
                }
            }
        }

        private bool IsEnabled =>
            settings.EnableMod &&
            settings.EnableClientFeatures &&
            settings.PreventMoveOrderOnPlacementCancel;

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(BugfixesAndQoLViewModel.EnableMod) ||
                propertyName == nameof(BugfixesAndQoLViewModel.EnableClientFeatures) ||
                propertyName == nameof(BugfixesAndQoLViewModel.PreventMoveOrderOnPlacementCancel))
            {
                state.Clear();
            }
        }
    }
}
