namespace BuildingLimit
{
    public sealed class BuildingLimitRule
    {
        public BuildingLimitDefinition Definition { get; }
        public int Limit { get; }
        public int DisplayLimit { get; }

        public BuildingLimitRule(BuildingLimitDefinition definition, int limit, int displayLimit)
        {
            Definition = definition;
            Limit = limit;
            DisplayLimit = displayLimit;
        }
    }
}

