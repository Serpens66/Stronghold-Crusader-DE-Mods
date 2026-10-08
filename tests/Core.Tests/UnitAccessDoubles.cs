using SHCDESE.Interop;
// Test doubles for the external SDK boundary; native layout is validated separately.
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
