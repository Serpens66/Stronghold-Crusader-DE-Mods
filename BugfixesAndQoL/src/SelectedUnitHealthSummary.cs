// Feature: Aggregate selected-unit health without UI or game-state dependencies.
namespace BugfixesAndQoL
{
    internal struct SelectedUnitHealthSummary
    {
        public long CurrentHealth { get; private set; }
        public long MaximumHealth { get; private set; }
        public int UnitCount { get; private set; }

        public void Add(long currentHealth, long maximumHealth)
        {
            if (maximumHealth <= 0 || currentHealth < 0)
                return;

            CurrentHealth += currentHealth;
            MaximumHealth += maximumHealth;
            UnitCount++;
        }

        public bool HasUnits => UnitCount > 0;

        public string Format() => "HP: " + CurrentHealth + " / " + MaximumHealth;
    }
}
