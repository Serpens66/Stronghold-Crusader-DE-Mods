using System;
using System.IO;

namespace UnitSpawnReturnProbe.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
                string runtime = File.ReadAllText(Path.Combine(root, "src", "UnitSpawnReturnProbeRuntime.cs"));
                string plugin = File.ReadAllText(Path.Combine(root, "src", "UnitSpawnReturnProbePlugin.cs"));
                string project = File.ReadAllText(Path.Combine(root, "UnitSpawnReturnProbe.csproj"));
                string combinedRuntimeSources = runtime + "\n" + plugin + "\n" + project;

                Require(runtime.Contains("ProbeDelaySeconds = 5"), "five-second delay missing");
                Require(runtime.Contains("probeCompleted") && runtime.Contains("oncePerMap=true"), "once-per-map guard missing");
                Require(runtime.Contains("CreateUnitLocal") && runtime.Contains("ScriptExtenderAbiSpawnDelegate") && runtime.Contains("NativeAbiSpawnDelegate"), "three probe variants missing");
                Require(runtime.Contains("spanIndex + 1"), "one-based unit ID conversion missing");
                Require(runtime.Contains("ExpectedSpawnRva = 0x17FEF0"), "native RVA guard missing");
                Require(runtime.Contains("GetField(") && runtime.Contains("OriginalEntryPointAddress"), "installed RedBird trampoline lookup missing");
                Require(!runtime.Contains("AddDetour") && !runtime.Contains("HookTransaction"), "probe must not install a competing detour");
                Require(runtime.Contains("MissionEvents.Started") && runtime.Contains("GameTimeManagerAPI.Instance.OnTick"), "event-driven lifecycle missing");
                Require(runtime.Contains("ActivePlayerHelper.GetActivePlayerIds()") &&
                        runtime.Contains("GetLocalPlayerId()") &&
                        runtime.Contains("activePlayerIds.Contains(PreferredPlayerId)"),
                    "active-player target selection missing");
                Require(runtime.Contains("GetPlayerKeepDoorPosition(playerId)") &&
                        runtime.Contains("GetNearestUnoccupiedTile(door.X, door.Y, 12)") &&
                        runtime.Contains("IsValidTileId(tileId)") &&
                        runtime.Contains("IsTileWalkableAndUnoccupied(tileId)") &&
                        runtime.Contains("GetTileHeight(tileId)"),
                    "StartConditions spawn-position parity missing");
                Require(runtime.Contains("ALL_SPAWN_PATHS_VALID"), "all-valid classification missing");
                Require(!plugin.Contains("OnDestroy") && !plugin.Contains("OnDisable") && !plugin.Contains("OnApplicationQuit"), "plugin teardown lifecycle method present");
                Require(!combinedRuntimeSources.Contains("System.Text.Json") &&
                        !combinedRuntimeSources.Contains("Newtonsoft.Json") &&
                        !combinedRuntimeSources.Contains("JavaScriptSerializer") &&
                        !combinedRuntimeSources.Contains("System.Web.Extensions") &&
                        !combinedRuntimeSources.Contains("DataContractJsonSerializer") &&
                        !combinedRuntimeSources.Contains("JsonUtility"),
                    "forbidden JSON dependency present");
                Require(!runtime.Contains(".Dispose(") && !plugin.Contains(".Dispose("), "runtime teardown call present");
                Require(project.Contains("$(ExtenderDir)\\SHCDESE.dll") && project.Contains("$(ApiSharedDir)\\APIShared.dll"), "canonical references missing");
                Require(project.Contains("Shared\\ActivePlayerHelper.cs"), "active-player helper source link missing");

                Console.WriteLine("UnitSpawnReturnProbe static policy tests passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
