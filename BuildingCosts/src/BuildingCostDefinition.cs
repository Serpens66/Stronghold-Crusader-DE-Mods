using SHCDESE.Interop;

namespace BuildingCosts
{
    public sealed class BuildingCostDefinition
    {
        public eMappers Mapper { get; }
        public eStructs[] Structures { get; }
        public string DisplayName { get; }

        public BuildingCostDefinition(eMappers mapper, string displayName, params eStructs[] structures)
        {
            Mapper = mapper;
            Structures = structures;
            DisplayName = displayName;
        }
    }
}
