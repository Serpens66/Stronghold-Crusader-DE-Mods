using SHCDESE.Interop;

namespace BugfixesAndQoL.AreaConstruction
{
    internal static class CrewPlacement
    {
        internal static int Required(eMappers mapper) => mapper switch
        {
            eMappers.MAPPER_TUNNEL_CONSTRUCTION or eMappers.MAPPER_PORTABLE_SHIELD => 1,
            eMappers.MAPPER_CATAPULT or eMappers.MAPPER_ARAB_BALLISTA => 2,
            eMappers.MAPPER_TREBUCHET => 3,
            eMappers.MAPPER_SIEGE_TOWER or eMappers.MAPPER_BATTERING_RAM => 4,
            _ => 0
        };

        internal static eChimps UnitType(eMappers mapper) => mapper == eMappers.MAPPER_TUNNEL_CONSTRUCTION
            ? eChimps.CHIMP_TYPE_TUNNELER : eChimps.CHIMP_TYPE_ENGINEER;

        internal static int MinimumFreeRecords(eMappers mapper) => mapper switch
        {
            eMappers.MAPPER_TUNNEL_CONSTRUCTION => 3,
            eMappers.MAPPER_CATAPULT or eMappers.MAPPER_TREBUCHET or eMappers.MAPPER_SIEGE_TOWER or
                eMappers.MAPPER_BATTERING_RAM or eMappers.MAPPER_PORTABLE_SHIELD => 11,
            _ => 20
        };
    }

    internal struct CrewBudget
    {
        private readonly int required;
        private int available;

        internal CrewBudget(int required, int selected)
        {
            this.required = required;
            available = selected;
            HasPendingOrder = false;
        }

        internal bool HasPendingOrder { get; private set; }
        internal bool CanReserve => available >= required;

        internal void HoldPending(int workers)
        {
            available -= workers;
            HasPendingOrder = true;
        }

        internal void Reserve() => available -= required;
    }
}
