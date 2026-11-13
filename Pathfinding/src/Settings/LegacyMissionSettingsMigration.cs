using APIShared.ModSettings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pathfinding
{
    // Personal-file import and mission ownership are separate: old saves and author documents
    // must move their host settings/rules before their now-retired BFQoL keys are filtered.
    internal static class LegacyMissionSettingsMigration
    {
        internal static void Register()
        {
            ModSettingsMigration.RegisterModMove(PathfindingPlugin.PluginGuid, "bfqol-pathfinding-split",
                new ModSettingsModMove("BugfixesAndQoL_Serp", PathfindingPlugin.PluginGuid,
                    LegacyBfqolSettings.Keys.Select(key => new ModSettingMove(key, key, typeof(bool))),
                    new[] { new ModSettingMove(nameof(PathfindingViewModel.EnableMod), nameof(PathfindingViewModel.EnableMod), typeof(bool)) },
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        // These options did not belong to the old Modpack. Their fixed author rules
                        // also prevent strict Trails from reactivating the new core's code default.
                        { nameof(PathfindingViewModel.EnablePathfindingCore), false },
                        { nameof(PathfindingViewModel.EnableFriendlyMoatTraversal), false },
                        { nameof(PathfindingViewModel.AvoidFire), false },
                        { nameof(PathfindingViewModel.AvoidPlague), false }
                    }));
        }
    }
}
