using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;

unsafe
{
    foreach (var state in Enum.GetValues<SHCDESE.Interop.Enums.AliveState>())
    foreach (uint marker in new uint[] { 0, 1, 0xFFFF, 0x10000, 0xFFFF0000, 0xFFFFFFFF })
    {
        GameUnit life = new GameUnit { r_AliveState = state, r_IsKilledByProjectile = (ushort)marker, r_InterestingTodo4MaybeRandomPathing = (ushort)(marker >> 16), r_CurrentHealth = 0 };
        bool expected = state == SHCDESE.Interop.Enums.AliveState.IsAlive && (marker & 0xFFFF) == 0;
        Check(UnitAccess.IsReallyAlive(in life) == expected, "reference life predicate");
        Check(UnitAccess.IsReallyAlive(&life) == expected, "pointer life predicate");
        life.r_CurrentHealth = uint.MaxValue;
        Check(UnitAccess.IsReallyAlive(in life) == expected, "health is not an additional rule");
    }
    Check(!UnitAccess.IsReallyAlive((GameUnit*)null), "null life view");
    var queryManager = new GameUnitManagerAPI();
    GameUnitManagerAPI.Current = queryManager;
    Check(UnitAccess.GetAllReallyAliveUnits().Length == 0, "empty query");
    queryManager.QueryRecords = new[] {
        new GameUnit { r_AliveState = SHCDESE.Interop.Enums.AliveState.IsAlive },
        new GameUnit { r_AliveState = SHCDESE.Interop.Enums.AliveState.IsAlive, r_IsKilledByProjectile = 1 },
        new GameUnit { r_AliveState = SHCDESE.Interop.Enums.AliveState.NeedsInit },
        new GameUnit { r_AliveState = SHCDESE.Interop.Enums.AliveState.IsAlive, r_IsKilledByProjectile = 0, r_InterestingTodo4MaybeRandomPathing = 1 },
        new GameUnit { r_AliveState = SHCDESE.Interop.Enums.AliveState.MarkedForDeletion },
    };
    int[] livingIds = UnitAccess.GetAllReallyAliveUnits();
    Check(livingIds.SequenceEqual(new[] { 1, 4 }), "query filters death low word and returns ordered one-based IDs");
    queryManager.QueryRecords[0].r_IsKilledByProjectile = 1;
    Check(livingIds.SequenceEqual(new[] { 1, 4 }) && UnitAccess.GetAllReallyAliveUnits().SequenceEqual(new[] { 4 }),
        "query is a momentary copy, later life changes require revalidation");
    GameUnit record = new GameUnit { Alive = 4, GlobalId = 0 }; // NeedsInit is still resolvable.
    var manager = new GameUnitManagerAPI { Pointer = &record };
    GameUnitManagerAPI.Current = manager;
    foreach (int id in new[] { int.MinValue, -1, 0 })
    {
        int before = GameUnitManagerAPI.InstanceReads;
        Check(!UnitAccess.TryGetById(id, out GameUnit* unit, out var reason) && unit == null && reason == UnitLookupFailure.InvalidId, "invalid ID");
        Check(GameUnitManagerAPI.InstanceReads == before && manager.Reads == 0 && manager.Validations == 0, "invalid ID never touches manager");
    }
    Check(!UnitAccess.TryGetById(null, 1, out GameUnit* absent, out var missing) && absent == null && missing == UnitLookupFailure.ManagerUnavailable, "missing manager");
    foreach (int id in new[] { 10000, int.MaxValue })
    {
        Check(!UnitAccess.TryGetById(manager, id, out GameUnit* unit, out var reason) && unit == null && reason == UnitLookupFailure.OutOfRange, "upper bound");
        Check(manager.Reads == 0, "out-of-range never enters SDK lookup");
    }
    foreach (int id in new[] { 1, 9999 })
    {
        Check(UnitAccess.TryGetById(manager, id, out GameUnit* unit, out var reason) && unit == &record && reason == UnitLookupFailure.None, "inclusive bounds");
        Check(manager.LastId == id && unit->Alive == 4 && unit->GlobalId == 0, "no ID conversion or life/identity filtering");
    }
    manager.Pointer = null;
    Check(!UnitAccess.TryGetById(manager, 1, out GameUnit* unloaded, out var unavailable) && unloaded == null && unavailable == UnitLookupFailure.Unavailable, "unavailable pointer after unload");
    manager.Pointer = &record; manager.ReturnFalse = true;
    Check(!UnitAccess.TryGetById(manager, 1, out GameUnit* failed, out var failure) && failed == null && failure == UnitLookupFailure.Unavailable, "false SDK result clears even non-null pointer");
    manager.ReturnFalse = false;
    Check(UnitAccess.TryGetById(manager, 1, out GameUnit* reloaded, out _) && reloaded == &record, "reload uses current manager storage");

    var log = new BepInEx.Logging.ManualLogSource();
    UnitAccess.InitializeDiagnostics(log);
    for (int i=0; i<100; i++) UnitAccess.TryGetById(manager, 0, out _, out _, "Mod/src/Worker.cs", "OnEvent", 12);
    Check(log.Lines.Count == 1 && log.Lines[0].Contains("mod=Mod") && log.Lines[0].Contains("reason=InvalidId"), "bounded attributed debug diagnostic");
    for (int i=0; i<300; i++) UnitAccess.TryGetById(manager, 0, out _, out _, "Mod/src/Worker.cs", "OnEvent", 100+i);
    Check(log.Lines.Count == 128, "global diagnostic bound");
}
Console.WriteLine("PASS: production UnitAccess boundaries, no invalid SDK calls, NeedsInit/identity neutrality, unload/reload, failure pointer clearing and bounded diagnostics.");

static void Check(bool value, string label) { if (!value) throw new Exception(label); }
namespace SHCDESE.Interop.Enums { public enum AliveState : short { None, NeedsInit, IsAlive, MarkedForDeletion, Unknown, Unknown5, Paused } }
namespace SHCDESE.Interop { public struct GameUnit { public int Alive, GlobalId; public SHCDESE.Interop.Enums.AliveState r_AliveState; public ushort r_IsKilledByProjectile, r_InterestingTodo4MaybeRandomPathing; public uint r_CurrentHealth; } }
namespace SHCDESE.API
{
    public unsafe class GameUnitManagerAPI
    {
        public static GameUnitManagerAPI Current;
        public static int InstanceReads;
        public static GameUnitManagerAPI Instance { get { InstanceReads++; return Current!; } }
        public GameUnit* Pointer;
        public int Reads, Validations, LastId;
        public bool ReturnFalse;
        public GameUnit[] QueryRecords = Array.Empty<GameUnit>();
        public UnitQuery QueryUnits() => new UnitQuery(QueryRecords);
        public delegate bool UnitPredicate(in GameUnit unit);
        public sealed class UnitQuery
        {
            private readonly GameUnit[] records;
            private UnitPredicate predicate;
            public UnitQuery(GameUnit[] records) { this.records = records; }
            public UnitQuery Where(UnitPredicate filter) { predicate = filter; return this; }
            public void ToIdList(List<int> ids)
            {
                for (int spanIndex = 0; spanIndex < records.Length; spanIndex++)
                    if (predicate == null || predicate(in records[spanIndex])) ids.Add(spanIndex + 1);
            }
        }
        public bool IsValidId(int id) { Validations++; return id>0 && id<=9999; }
        public bool TryGetUnitById(int id, out GameUnit* unit) { Reads++; LastId=id; unit=Pointer; return !ReturnFalse; }
    }
}
namespace BepInEx.Logging
{
    public class ManualLogSource { public readonly List<string> Lines=new(); public void LogDebug(object line) => Lines.Add(line.ToString()!); }
}
