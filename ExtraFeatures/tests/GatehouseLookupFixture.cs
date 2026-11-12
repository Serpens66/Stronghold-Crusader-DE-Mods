using System;
using System.Collections.Generic;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

// External managers and unrelated runtime state are fixtures. The production partial owns the
// run boundary, identity checks, path-record lookup and manual snapshot read/publication.
namespace SHCDESE.Interop.Enums
{
    public enum AliveState { Dead, IsAlive }
    public enum eStructs
    {
        STRUCT_GATE_MAIN, STRUCT_GATE_INNER, STRUCT_GATE_WOOD, STRUCT_GATE_POSTERN,
        STRUCT_GATEHOUSE, STRUCT_DRAWBRIDGE
    }
}

namespace SHCDESE.Interop
{
    public struct GameBuilding
    {
        public uint r_GlobalId;
        public AliveState r_AliveState;
        public eStructs r_BuildingType;
    }

    public struct PathConnectionRecord
    {
        public uint r_SubjectGlobalId;
        public int r_BuildingId;
        public int r_IsActive;
    }
}

namespace SHCDESE.API
{
    public sealed unsafe class GameBuildingManagerAPI
    {
        public static readonly GameBuildingManagerAPI Instance = new GameBuildingManagerAPI();
        public GameBuilding* Buildings;
        public int Lookups;

        public bool IsValidId(int id) => id >= 1 && id <= 2;

        public bool TryGetBuildingById(int id, out GameBuilding* building)
        {
            Lookups++;
            building = id >= 1 && id <= 2 ? Buildings + id - 1 : null;
            return building != null;
        }
    }

    public sealed unsafe class GamePathingManagerAPI
    {
        public static readonly GamePathingManagerAPI Instance = new GamePathingManagerAPI();
        public PathConnectionRecord* Record;
        public int Lookups;

        public bool TryGetPathConnectionRecordByBuildingId(int id, out PathConnectionRecord* record)
        {
            Lookups++;
            record = Record->r_IsActive != 0 && Record->r_BuildingId == id ? Record : null;
            return record != null;
        }
    }
}

namespace Shared
{
    internal static class GameplayModActivationGate
    {
        internal static bool IsEnabled(bool enabled) => enabled;
    }
}

namespace ExtraFeatures
{
    internal sealed unsafe partial class GatehouseAutomationRuntime
    {
        private sealed class Settings { internal bool EnableMod = true; }

        private readonly Settings settings = new Settings();
        private readonly HashSet<int> manualOnlyGateGlobalIds = new HashSet<int>();
        private bool automationReady = true;
        private bool mapActive = true;
        private bool editorSessionActive;
        private bool loadedMapStatePending;

        internal void AssertCandidateLookups(GameBuilding* gate, PathConnectionRecord* record)
        {
            var paths = GamePathingManagerAPI.Instance;
            paths.Lookups = 0;
            BeginGateQueryRun();
            try
            {
                GatehouseLookupFixture.Check(HasLivePathConnectionForQuery(1, gate) &&
                    HasLivePathConnectionForQuery(1, gate) && paths.Lookups == 1,
                    "Two candidates for one live gate should scan the record list only once.");

                gate->r_GlobalId = 2;
                GatehouseLookupFixture.Check(!HasLivePathConnectionForQuery(1, gate) && paths.Lookups == 2,
                    "A reused building slot must not use a previous Global-ID's record.");
                record->r_SubjectGlobalId = 2;
                GatehouseLookupFixture.Check(HasLivePathConnectionForQuery(1, gate) && paths.Lookups == 3,
                    "A previous failed lookup must be retried.");

                InvalidateGateQueryCache();
                GatehouseLookupFixture.Check(HasLivePathConnectionForQuery(1, gate) && paths.Lookups == 4,
                    "A map transition must invalidate the cached record inside a run.");

                BeginGateQueryRun();
                try
                {
                    GatehouseLookupFixture.Check(HasLivePathConnectionForQuery(1, gate) &&
                        HasLivePathConnectionForQuery(1, gate) && paths.Lookups == 6,
                        "Nested runs must query the current path record for each candidate.");
                }
                finally { EndGateQueryRun(); }
                GatehouseLookupFixture.Check(HasLivePathConnectionForQuery(1, gate) && paths.Lookups == 7,
                    "The outer run must look up the record again after a nested run.");

                editorSessionActive = true;
                GatehouseLookupFixture.Check(HasLivePathConnectionForQuery(1, gate) &&
                    HasLivePathConnectionForQuery(1, gate) && paths.Lookups == 9,
                    "Editor queries must stay fresh even inside a marked run.");
                editorSessionActive = false;
            }
            finally { EndGateQueryRun(); }

            GatehouseLookupFixture.Check(HasLivePathConnectionForQuery(1, gate) &&
                HasLivePathConnectionForQuery(1, gate) && paths.Lookups == 11,
                "Queries outside a marked run must always be fresh.");
            BeginGateQueryRun();
            try
            {
                GatehouseLookupFixture.Check(HasLivePathConnectionForQuery(1, gate) && paths.Lookups == 12,
                    "A new run must start with an empty cache.");
                record->r_IsActive = 0;
                GatehouseLookupFixture.Check(!HasLivePathConnectionForQuery(1, gate) && paths.Lookups == 13,
                    "A cached inactive path record must not satisfy a candidate query.");
            }
            finally { EndGateQueryRun(); }
            record->r_IsActive = 1;
        }

        internal void AssertManualLookups(GameBuilding* gate, PathConnectionRecord* record)
        {
            var paths = GamePathingManagerAPI.Instance;
            var buildings = GameBuildingManagerAPI.Instance;
            paths.Lookups = buildings.Lookups = 0;
            GatehouseLookupFixture.Check(!IsManualOnlyBuilding(1) && paths.Lookups == 0 && buildings.Lookups == 0,
                "An empty snapshot must short circuit before either manager lookup.");

            manualOnlyGateGlobalIds.Add(3);
            PublishManualGateIdsLocked();
            GatehouseLookupFixture.Check(!IsManualOnlyBuilding(1) && paths.Lookups == 0 && buildings.Lookups == 1,
                "An automatic gate must not scan path records even if another building is manual.");
            GatehouseLookupFixture.Check(IsManualOnlyBuilding(2) && paths.Lookups == 0,
                "A manual drawbridge must work without a path connection.");

            manualOnlyGateGlobalIds.Add((int)gate->r_GlobalId);
            PublishManualGateIdsLocked();
            GatehouseLookupFixture.Check(IsManualOnlyBuilding(1) && paths.Lookups == 1,
                "A manual gate with a current path connection must be accepted.");
            record->r_SubjectGlobalId = 99;
            GatehouseLookupFixture.Check(!IsManualOnlyBuilding(1) && paths.Lookups == 2,
                "A stale path connection must not qualify a manual gate.");
            record->r_SubjectGlobalId = gate->r_GlobalId;

            loadedMapStatePending = true;
            GatehouseLookupFixture.Check(!IsManualOnlyBuilding(1) && paths.Lookups == 2,
                "Saved manual identities cannot take effect before the map is ready.");
            loadedMapStatePending = false;

            manualOnlyGateGlobalIds.Clear();
            PublishManualGateIdsLocked();
            int before = buildings.Lookups;
            GatehouseLookupFixture.Check(!IsManualOnlyBuilding(1) && paths.Lookups == 2 && buildings.Lookups == before,
                "Publishing an empty snapshot must remove manual lookups after map teardown.");
        }
    }

    public static unsafe class GatehouseLookupFixture
    {
        internal static void Check(bool value, string reason)
        {
            if (!value) throw new InvalidOperationException(reason);
        }

        public static void Run()
        {
            GameBuilding* buildings = stackalloc GameBuilding[2];
            buildings[0] = new GameBuilding { r_GlobalId = 1, r_AliveState = AliveState.IsAlive };
            buildings[1] = new GameBuilding
            {
                r_GlobalId = 3, r_AliveState = AliveState.IsAlive, r_BuildingType = eStructs.STRUCT_DRAWBRIDGE
            };
            var record = new PathConnectionRecord { r_SubjectGlobalId = 1, r_BuildingId = 1, r_IsActive = 1 };
            GameBuildingManagerAPI.Instance.Buildings = buildings;
            GamePathingManagerAPI.Instance.Record = &record;
            try
            {
                var runtime = new GatehouseAutomationRuntime();
                runtime.AssertCandidateLookups(buildings, &record);
                runtime.AssertManualLookups(buildings, &record);
                Console.WriteLine("PASS: gatehouse candidate caching and manual lookup counts.");
            }
            finally
            {
                GameBuildingManagerAPI.Instance.Buildings = null;
                GamePathingManagerAPI.Instance.Record = null;
            }
        }
    }
}
