using SHCDESE.Interop;
using System;

namespace ExtraFeatures
{
    internal enum HealthCategory { None, Lord, Soldier, Civilian, Building }

    // Explicit gameplay groups: animals and passive decorative people retain their own HP.
    internal static class HealthMultiplierPolicy
    {
        public const int MinimumPercent = 10;
        public const int MaximumPercent = 500;
        public const int DefaultPercent = 100;
        public static int NormalizePercent(int percent) => Math.Max(MinimumPercent, Math.Min(MaximumPercent, percent));

        internal static HealthCategory ClassifyUnit(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_LORD: return HealthCategory.Lord;
                case eChimps.CHIMP_TYPE_TUNNELER:
                case eChimps.CHIMP_TYPE_ARCHER:
                case eChimps.CHIMP_TYPE_XBOWMAN:
                case eChimps.CHIMP_TYPE_SPEARMAN:
                case eChimps.CHIMP_TYPE_PIKEMAN:
                case eChimps.CHIMP_TYPE_MACEMAN:
                case eChimps.CHIMP_TYPE_SWORDSMAN:
                case eChimps.CHIMP_TYPE_KNIGHT:
                case eChimps.CHIMP_TYPE_LADDERMAN:
                case eChimps.CHIMP_TYPE_ENGINEER:
                case eChimps.CHIMP_TYPE_MONK:
                case eChimps.CHIMP_TYPE_CATAPULT:
                case eChimps.CHIMP_TYPE_TREBUCHET:
                case eChimps.CHIMP_TYPE_MANGONEL:
                case eChimps.CHIMP_TYPE_SIEGE_TOWER:
                case eChimps.CHIMP_TYPE_BATTERING_RAM:
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD:
                case eChimps.CHIMP_TYPE_BALLISTA:
                case eChimps.CHIMP_TYPE_ARAB_BOW:
                case eChimps.CHIMP_TYPE_ARAB_SLAVE:
                case eChimps.CHIMP_TYPE_ARAB_SLINGER:
                case eChimps.CHIMP_TYPE_ARAB_ASSASIN:
                case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                case eChimps.CHIMP_TYPE_ARAB_SWORDSMAN:
                case eChimps.CHIMP_TYPE_ARAB_GRENADIER:
                case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEALER:
                case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                case eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER:
                case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER: return HealthCategory.Soldier;
                case eChimps.CHIMP_TYPE_PEASANT:
                case eChimps.CHIMP_TYPE_WOODCUTTER:
                case eChimps.CHIMP_TYPE_FLETCHER:
                case eChimps.CHIMP_TYPE_HUNTER:
                case eChimps.CHIMP_TYPE_QUARRY_MASON:
                case eChimps.CHIMP_TYPE_QUARRY_GRUNT:
                case eChimps.CHIMP_TYPE_PITCHMAN:
                case eChimps.CHIMP_TYPE_FARMER_WHEAT:
                case eChimps.CHIMP_TYPE_FARMER_HOPS:
                case eChimps.CHIMP_TYPE_FARMER_APPLE:
                case eChimps.CHIMP_TYPE_FARMER_CATTLE:
                case eChimps.CHIMP_TYPE_MILLER:
                case eChimps.CHIMP_TYPE_BAKER:
                case eChimps.CHIMP_TYPE_BREWER:
                case eChimps.CHIMP_TYPE_POLETURNER:
                case eChimps.CHIMP_TYPE_BLACKSMITH:
                case eChimps.CHIMP_TYPE_ARMOURER:
                case eChimps.CHIMP_TYPE_TANNER:
                case eChimps.CHIMP_TYPE_MINER1:
                case eChimps.CHIMP_TYPE_MINER2:
                case eChimps.CHIMP_TYPE_PRIEST:
                case eChimps.CHIMP_TYPE_HEALER:
                case eChimps.CHIMP_TYPE_DRUNKARD:
                case eChimps.CHIMP_TYPE_INNKEEPER:
                case eChimps.CHIMP_TYPE_TRADER:
                case eChimps.CHIMP_TYPE_FIREMAN: return HealthCategory.Civilian;
                default: return HealthCategory.None;
            }
        }

        internal static HealthCategory ClassifyBuilding(eStructs type)
        {
            switch (type)
            {
                // Walls normally use tile state; also exclude wall-typed object contexts.
                case eStructs.STRUCT_NULL:
                case eStructs.STRUCT_WAS_WALL:
                case eStructs.STRUCT_WOOD_WALL:
                case eStructs.STRUCT_STONE_WALL:
                case eStructs.STRUCT_CRENAL_WALL:
                case eStructs.STRUCT_SIEGE_TOWER: return HealthCategory.None;
                default: return HealthCategory.Building;
            }
        }
    }

    internal sealed class HealthSettingsSnapshot
    {
        private readonly int humanLord, aiLord, humanSoldier, aiSoldier, humanCivilian, aiCivilian, humanBuilding, aiBuilding;
        internal HealthSettingsSnapshot(ExtraFeaturesViewModel settings)
        {
            humanLord = HealthMultiplierPolicy.NormalizePercent(settings.HumanLordHealthPercent);
            aiLord = HealthMultiplierPolicy.NormalizePercent(settings.AILordHealthPercent);
            humanSoldier = HealthMultiplierPolicy.NormalizePercent(settings.HumanSoldierHealthPercent);
            aiSoldier = HealthMultiplierPolicy.NormalizePercent(settings.AISoldierHealthPercent);
            humanCivilian = HealthMultiplierPolicy.NormalizePercent(settings.HumanCivilianHealthPercent);
            aiCivilian = HealthMultiplierPolicy.NormalizePercent(settings.AICivilianHealthPercent);
            humanBuilding = HealthMultiplierPolicy.NormalizePercent(settings.HumanBuildingHealthPercent);
            aiBuilding = HealthMultiplierPolicy.NormalizePercent(settings.AIBuildingHealthPercent);
        }

        internal int GetPercent(HealthCategory category, bool ai)
        {
            switch (category)
            {
                case HealthCategory.Lord: return ai ? aiLord : humanLord;
                case HealthCategory.Soldier: return ai ? aiSoldier : humanSoldier;
                case HealthCategory.Civilian: return ai ? aiCivilian : humanCivilian;
                case HealthCategory.Building: return ai ? aiBuilding : humanBuilding;
                default: return HealthMultiplierPolicy.DefaultPercent;
            }
        }
    }
}
