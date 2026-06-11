namespace BuildingLimit
{
    public sealed class BuildingLimitRule
    {
        public BuildingLimitDefinition Definition { get; }
        public int Limit { get; }

        public BuildingLimitRule(BuildingLimitDefinition definition, int limit)
        {
            Definition = definition;
            Limit = limit;
        }
    }
}

