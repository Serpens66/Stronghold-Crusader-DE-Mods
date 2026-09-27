namespace APIShared
{
    /// <summary>Immutable repair costs and resource amounts for the selected owned building.</summary>
    public sealed class BuildingRepairQuote
    {
        /// <summary>Creates a repair quote from a single simulation snapshot.</summary>
        public BuildingRepairQuote(int buildingId, int buildingGlobalId, int panel, bool canRepair,
            int currentHealth, int maxHealth, int wood, int stone, int iron, int pitch, int gold,
            int availableWood, int availableStone, int availableIron, int availablePitch, int availableGold)
        {
            BuildingId = buildingId;
            BuildingGlobalId = buildingGlobalId;
            Panel = panel;
            CanRepair = canRepair;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            Wood = wood;
            Stone = stone;
            Iron = iron;
            Pitch = pitch;
            Gold = gold;
            AvailableWood = availableWood;
            AvailableStone = availableStone;
            AvailableIron = availableIron;
            AvailablePitch = availablePitch;
            AvailableGold = availableGold;
        }

        /// <summary>One-based game building ID.</summary>
        public int BuildingId { get; }
        /// <summary>Stable identity of the selected building.</summary>
        public int BuildingGlobalId { get; }
        /// <summary>Vanilla in-building HUD panel.</summary>
        public int Panel { get; }
        /// <summary>Vanilla repair proximity and state permission.</summary>
        public bool CanRepair { get; }
        /// <summary>Current building health.</summary>
        public int CurrentHealth { get; }
        /// <summary>Maximum building health.</summary>
        public int MaxHealth { get; }
        /// <summary>Required wood.</summary>
        public int Wood { get; }
        /// <summary>Required stone.</summary>
        public int Stone { get; }
        /// <summary>Required iron.</summary>
        public int Iron { get; }
        /// <summary>Required pitch.</summary>
        public int Pitch { get; }
        /// <summary>Required gold.</summary>
        public int Gold { get; }
        /// <summary>Available wood.</summary>
        public int AvailableWood { get; }
        /// <summary>Available stone.</summary>
        public int AvailableStone { get; }
        /// <summary>Available iron.</summary>
        public int AvailableIron { get; }
        /// <summary>Available pitch.</summary>
        public int AvailablePitch { get; }
        /// <summary>Available gold.</summary>
        public int AvailableGold { get; }
        /// <summary>Whether every required resource is available in this snapshot.</summary>
        public bool HasResources => AvailableWood >= Wood && AvailableStone >= Stone &&
            AvailableIron >= Iron && AvailablePitch >= Pitch && AvailableGold >= Gold;
    }

    /// <summary>Shared repair execution and HUD presentation. All UI methods run on the UI thread.</summary>
    public interface IBuildingRepairCapability
    {
        /// <summary>Enables or disables this owner's use without removing process-wide hooks.</summary>
        void SetActive(bool active);
        /// <summary>Reads the selected building's verified simulation snapshot.</summary>
        bool TryGetSelectedQuote(out BuildingRepairQuote quote);
        /// <summary>Begins showing the complete repair tooltip for a stable button identity.</summary>
        void BeginHover(string buttonId);
        /// <summary>Stops showing the repair tooltip for that button.</summary>
        void EndHover(string buttonId);
    }
}
