using System;

namespace ExtremePowers.API
{
    public enum ExtremePowerProjectileKind { Rock = 0, Arrow = 1 }
    public sealed class VolleyConfiguration { public int Damage { get; set; } public int Radius { get; set; } public ExtremePowerProjectileKind ProjectileKind { get; set; } public VolleyConfiguration Clone() => (VolleyConfiguration)MemberwiseClone(); }
    public sealed class HealingConfiguration { public int Amount { get; set; } public int Radius { get; set; } public HealingConfiguration Clone() => (HealingConfiguration)MemberwiseClone(); }
    public sealed class SpawnConfiguration { public int UnitType { get; set; } public int Count { get; set; } public SpawnConfiguration Clone() => (SpawnConfiguration)MemberwiseClone(); }
    public sealed class GoldConfiguration { public int Minimum { get; set; } public int Maximum { get; set; } public GoldConfiguration Clone() => (GoldConfiguration)MemberwiseClone(); }

    public class ExtremePowersTuning
    {
        public ExtremePowersTuning()
        {
            Costs = new int[8]; ArrowVolley = new VolleyConfiguration(); Heal = new HealingConfiguration();
            Spearmen = new SpawnConfiguration(); Engineers = new SpawnConfiguration(); Macemen = new SpawnConfiguration(); Knights = new SpawnConfiguration();
            Gold = new GoldConfiguration(); RockVolley = new VolleyConfiguration(); RegenerationPercent = 100;
        }
        /// <summary>Gets or sets the execution cost for power IDs 0 through 7.</summary>
        /// <remarks>The supported game build keeps the visible HUD labels tied to each slot's Vanilla cost. Custom values are functional but not recommended unless the consumer also provides matching HUD labels.</remarks>
        public int[] Costs { get; set; }
        public int RegenerationPercent { get; set; }
        public VolleyConfiguration ArrowVolley { get; set; }
        public HealingConfiguration Heal { get; set; }
        public SpawnConfiguration Spearmen { get; set; }
        public SpawnConfiguration Engineers { get; set; }
        public SpawnConfiguration Macemen { get; set; }
        public GoldConfiguration Gold { get; set; }
        public VolleyConfiguration RockVolley { get; set; }
        public SpawnConfiguration Knights { get; set; }
        public ExtremePowersTuning Clone() => new ExtremePowersTuning { Costs = (int[])Costs.Clone(), RegenerationPercent = RegenerationPercent, ArrowVolley = ArrowVolley.Clone(), Heal = Heal.Clone(), Spearmen = Spearmen.Clone(), Engineers = Engineers.Clone(), Macemen = Macemen.Clone(), Gold = Gold.Clone(), RockVolley = RockVolley.Clone(), Knights = Knights.Clone() };
        internal void Validate()
        {
            if (Costs == null || Costs.Length != 8) throw new ArgumentException("Exactly eight power costs are required.", nameof(Costs));
            for (int i = 0; i < Costs.Length; i++) if (Costs[i] < 0 || Costs[i] > 1000000) throw new ArgumentOutOfRangeException(nameof(Costs));
            if (RegenerationPercent < 0 || RegenerationPercent > 1000) throw new ArgumentOutOfRangeException(nameof(RegenerationPercent));
            ValidateVolley(ArrowVolley); ValidateVolley(RockVolley);
            if (Heal == null || Heal.Amount < 0 || Heal.Radius < 0) throw new ArgumentOutOfRangeException(nameof(Heal));
            ValidateSpawn(Spearmen); ValidateSpawn(Engineers); ValidateSpawn(Macemen); ValidateSpawn(Knights);
            if (Gold == null || Gold.Minimum < 0 || Gold.Maximum < Gold.Minimum) throw new ArgumentOutOfRangeException(nameof(Gold));
        }
        private static void ValidateVolley(VolleyConfiguration value) { if (value == null || value.Damage < 0 || value.Radius < 0 || !Enum.IsDefined(typeof(ExtremePowerProjectileKind), value.ProjectileKind)) throw new ArgumentOutOfRangeException(nameof(value)); }
        // eChimps is sequential in the supported Script Extender: 0 is NULL and 90 is the end sentinel.
        private static void ValidateSpawn(SpawnConfiguration value) { if (value == null || value.UnitType <= 0 || value.UnitType >= 90 || value.Count < 0 || value.Count > 10000) throw new ArgumentOutOfRangeException(nameof(value)); }
    }

    public sealed class VanillaExtremePowersConfiguration : ExtremePowersTuning { }
}
