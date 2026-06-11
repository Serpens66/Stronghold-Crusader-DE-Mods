using SHCDESE.Interop;

namespace BuildingLimit
{
    public sealed class BuildingLimitDefinition
    {
        public eMappers Mapper { get; }
        public eStructs[] Structures { get; }
        public string DisplayName { get; }

        public BuildingLimitDefinition(eMappers mapper, string displayName, params eStructs[] structures)
        {
            Mapper = mapper;
            Structures = structures;
            DisplayName = displayName;
        }
    }
}

