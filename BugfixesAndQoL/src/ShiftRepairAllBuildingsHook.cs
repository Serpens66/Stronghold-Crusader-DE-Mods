// Feature: Shift-click the Vanilla Repair button to repair all eligible owned buildings.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class ShiftRepairAllBuildingsHook : IDisposable
    {
        private delegate void ButtonRepairDelegate(MainViewModel self, object parameter);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly List<int> buildingIds = new List<int>(256);
        private Hook hook;
        private ButtonRepairDelegate trampoline;
        private bool disposed;

        public ShiftRepairAllBuildingsHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Hook installedHook = null;
            try
            {
                installedHook = new Hook(FindButtonRepairMethod(), (ButtonRepairDelegate)ButtonRepairHook);
                ButtonRepairDelegate installedTrampoline = installedHook.GenerateTrampoline<ButtonRepairDelegate>();
                hook = installedHook;
                trampoline = installedTrampoline;
            }
            catch
            {
                installedHook?.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Hook current = hook;
            current?.Undo();
            current?.Dispose();
            hook = null;
            trampoline = null;
            buildingIds.Clear();
        }

        private static MethodInfo FindButtonRepairMethod()
        {
            MethodInfo method = typeof(MainViewModel).GetMethod(
                "ButtonRepairFunction",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(object) },
                null);

            if (method == null)
                throw new MissingMethodException(typeof(MainViewModel).FullName, "ButtonRepairFunction");

            return method;
        }

        private void ButtonRepairHook(MainViewModel self, object parameter)
        {
            // Preserve the clicked building's exact Vanilla behavior and ordering in every case.
            ButtonRepairDelegate original = trampoline;
            original(self, parameter);

            KeyManager keys = KeyManager.instance;
            if (disposed ||
                !settings.EnableMod ||
                !settings.EnableShiftRepairAllBuildings ||
                keys == null ||
                !keys.isShiftDown())
            {
                return;
            }

            try
            {
                QueueAdditionalRepairs(self);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL Shift-repair expansion failed after the selected building's Vanilla repair action: {ex}");
            }
        }

        private unsafe void QueueAdditionalRepairs(MainViewModel self)
        {
            if (self?.HUDBuildingPanel == null || GameData.Instance?.lastGameState == null)
                return;

            int controlledPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            if (controlledPlayerId <= 0)
                return;

            int selectedBuildingId = GameData.Instance.lastGameState.in_structure;
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            buildingIds.Clear();
            buildingApi.GetAllBuildings(
                buildingIds,
                AliveState.IsAlive,
                null,
                PlayerRelationship.Self,
                controlledPlayerId);

            foreach (int buildingId in buildingIds)
            {
                if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* building) || building == null)
                    continue;

                // Reuse Vanilla's general repair-button classifier instead of maintaining a tower/gatehouse list.
                bool vanillaShowsRepair = self.HUDBuildingPanel.GetBuildingShowRepair(
                    (int)building->r_BuildingType,
                    0);
                if (!ShiftRepairAllBuildingsPolicy.ShouldQueueAdditionalRepair(
                        buildingId,
                        selectedBuildingId,
                        building->r_PlayerIdOwner,
                        controlledPlayerId,
                        building->r_AliveState == AliveState.IsAlive,
                        building->r_CurrentHealth,
                        building->r_MaxHealth,
                        building->r_GlobalId,
                        vanillaShowsRepair))
                {
                    continue;
                }

                // Vanilla calculates costs, checks current resources, queues the chore, and deducts on execution.
                EngineInterface.GameAction(Enums.GameActionCommand.RepairBuilding, buildingId, 0);
            }
        }
    }
}
