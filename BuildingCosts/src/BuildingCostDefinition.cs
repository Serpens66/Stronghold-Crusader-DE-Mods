using SHCDESE.Interop;

namespace BuildingCosts
{
    internal sealed class BuildingCostDefinition
    {
        public BuildingCostDefinition(eMappers mapper, eStructs structure, string displayName)
        {
            Mapper = mapper;
            Structure = structure;
            DisplayName = displayName;
        }

        public eMappers Mapper { get; }
        public eStructs Structure { get; }
        public string DisplayName { get; }
    }
}
