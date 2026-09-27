using SHCDESE.Interop;

namespace BuildingLimit
{
    public sealed class BuildingLimitDefinition
    {
        public eMappers Mapper { get; }
        public eStructs[] Structures { get; }
        internal eChimps? CountedUnitType { get; }
        public string DisplayName { get; }

        public BuildingLimitDefinition(eMappers mapper, string displayName, params eStructs[] structures)
            : this(mapper, displayName, structures, null)
        {
        }

        internal BuildingLimitDefinition(eMappers mapper, string displayName, eStructs[] structures, eChimps? countedUnitType)
        {
            Mapper = mapper;
            Structures = structures;
            CountedUnitType = countedUnitType;
            DisplayName = displayName;
        }
    }
}

