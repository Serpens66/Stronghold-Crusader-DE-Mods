using SHCDESE.API.Components.Network;
using SHCDESE.ViewModels;
using System;

namespace SettingsMod
{
    public sealed class SettingsModLobbyViewModel : LobbyModSettingsBaseViewModel
    {
        public event Action<string> SettingChanged;

        private int multiplyGoodsGainAI = 0;
        private int multiplyGoodsGainHuman = 0;
        private int multiplyGoodsGainInMoneyAI = 2;
        private int multiplyGoodsGainInMoneyHuman = 0;
        private string setStartGoldAI = "";
        private string setStartGoldHuman = "0";
        private int addStartGoldAI = 10000;
        private int addStartGoldHuman = 0;
        private bool overwriteStartGoodsAI = true;
        private bool overwriteStartGoodsHuman = false;
        private int multiplyStartTroopsAI = 10;
        private int multiplyStartTroopsHuman = 0;

        private bool advancedOptionsEnabled = true;
        private bool advancedSkirmishOptionsEnabled = true;
        private bool betterHealers = true;
        private bool fasterPeasants = false;
        private bool globalImprovedSiegeBehaviour = true;
        private bool globalMoreAggressiveSiegeBehaviour = true;
        private bool improvedArabSwordsman = true;
        private bool improvedFletchers = true;
        private bool improvedLadderman = true;
        private bool improvedSpearman = true;
        private bool nerfEunuchs = true;
        private bool noKnockdownWalls = true;
        private bool rebalancedHorseArchers = true;
        private bool uncappedPeasants = false;
        private string enemyHealthModifier = "VeryStrong";

        private string startGoodsAI = @"STORED_WOOD_PLANKS=192
STORED_RAW_HOPS=0
STORED_STONE_BLOCKS=240
STORED_IRON_INGOTS=48
STORED_PITCH_RAW=48
STORED_RAW_WHEAT=0
STORED_FOOD_BREAD=100
STORED_FOOD_CHEESE=100
STORED_FOOD_MEAT=100
STORED_FOOD_FRUIT=100
STORED_FOOD_ALE=16
STORED_FLOUR=0
STORED_BOWS=0
STORED_CROSSBOWS=0
STORED_SPEARS=0
STORED_PIKES=0
STORED_MACES=0
STORED_SWORDS=0
STORED_LEATHER_ARMOUR=0
STORED_METAL_ARMOUR=0";

        private string startGoodsHuman = @"STORED_WOOD_PLANKS=96
STORED_RAW_HOPS=0
STORED_STONE_BLOCKS=48
STORED_IRON_INGOTS=0
STORED_PITCH_RAW=0
STORED_RAW_WHEAT=0
STORED_FOOD_BREAD=50
STORED_FOOD_CHEESE=0
STORED_FOOD_MEAT=0
STORED_FOOD_FRUIT=0
STORED_FOOD_ALE=0
STORED_FLOUR=0
STORED_BOWS=0
STORED_CROSSBOWS=0
STORED_SPEARS=0
STORED_PIKES=0
STORED_MACES=0
STORED_SWORDS=0
STORED_LEATHER_ARMOUR=0
STORED_METAL_ARMOUR=0";

        private string addStartTroopsAI = DefaultTroops;
        private string addStartTroopsHuman = DefaultTroops;

        public const string DefaultTroops = @"CHIMP_TYPE_ARCHER=0
CHIMP_TYPE_SPEARMAN=0
CHIMP_TYPE_MACEMAN=0
CHIMP_TYPE_XBOWMAN=0
CHIMP_TYPE_PIKEMAN=0
CHIMP_TYPE_SWORDSMAN=0
CHIMP_TYPE_KNIGHT=0
CHIMP_TYPE_ENGINEER=0
CHIMP_TYPE_MONK=0
CHIMP_TYPE_LADDERMAN=0
CHIMP_TYPE_TUNNELER=0
CHIMP_TYPE_ARAB_BOW=0
CHIMP_TYPE_ARAB_SLAVE=0
CHIMP_TYPE_ARAB_SLINGER=0
CHIMP_TYPE_ARAB_ASSASIN=0
CHIMP_TYPE_ARAB_HORSEMAN=0
CHIMP_TYPE_ARAB_SWORDSMAN=0
CHIMP_TYPE_ARAB_GRENADIER=0
CHIMP_TYPE_BEDOUIN_CAMEL_LANCER=0
CHIMP_TYPE_BEDOUIN_HEALER=0
CHIMP_TYPE_BEDOUIN_EUNUCH=0
CHIMP_TYPE_BEDOUIN_AMBUSHER=0
CHIMP_TYPE_BEDOUIN_SKIRMISHER=0
CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL=0
CHIMP_TYPE_BEDOUIN_SAPPER=0
CHIMP_TYPE_BEDOUIN_DEMOLISHER=0";

        [SyncHostOnly] public int MultiplyGoodsGainAI { get => multiplyGoodsGainAI; set => Set(ref multiplyGoodsGainAI, value, nameof(MultiplyGoodsGainAI)); }
        [SyncHostOnly] public int MultiplyGoodsGainHuman { get => multiplyGoodsGainHuman; set => Set(ref multiplyGoodsGainHuman, value, nameof(MultiplyGoodsGainHuman)); }
        [SyncHostOnly] public int MultiplyGoodsGainInMoneyAI { get => multiplyGoodsGainInMoneyAI; set => Set(ref multiplyGoodsGainInMoneyAI, value, nameof(MultiplyGoodsGainInMoneyAI)); }
        [SyncHostOnly] public int MultiplyGoodsGainInMoneyHuman { get => multiplyGoodsGainInMoneyHuman; set => Set(ref multiplyGoodsGainInMoneyHuman, value, nameof(MultiplyGoodsGainInMoneyHuman)); }
        [SyncHostOnly] public string SetStartGoldAI { get => setStartGoldAI; set => Set(ref setStartGoldAI, value, nameof(SetStartGoldAI)); }
        [SyncHostOnly] public string SetStartGoldHuman { get => setStartGoldHuman; set => Set(ref setStartGoldHuman, value, nameof(SetStartGoldHuman)); }
        [SyncHostOnly] public int AddStartGoldAI { get => addStartGoldAI; set => Set(ref addStartGoldAI, value, nameof(AddStartGoldAI)); }
        [SyncHostOnly] public int AddStartGoldHuman { get => addStartGoldHuman; set => Set(ref addStartGoldHuman, value, nameof(AddStartGoldHuman)); }
        [SyncHostOnly] public bool OverwriteStartGoodsAI { get => overwriteStartGoodsAI; set => Set(ref overwriteStartGoodsAI, value, nameof(OverwriteStartGoodsAI)); }
        [SyncHostOnly] public bool OverwriteStartGoodsHuman { get => overwriteStartGoodsHuman; set => Set(ref overwriteStartGoodsHuman, value, nameof(OverwriteStartGoodsHuman)); }
        [SyncHostOnly] public string StartGoodsAI { get => startGoodsAI; set => Set(ref startGoodsAI, value, nameof(StartGoodsAI)); }
        [SyncHostOnly] public string StartGoodsHuman { get => startGoodsHuman; set => Set(ref startGoodsHuman, value, nameof(StartGoodsHuman)); }
        [SyncHostOnly] public int MultiplyStartTroopsAI { get => multiplyStartTroopsAI; set => Set(ref multiplyStartTroopsAI, value, nameof(MultiplyStartTroopsAI)); }
        [SyncHostOnly] public int MultiplyStartTroopsHuman { get => multiplyStartTroopsHuman; set => Set(ref multiplyStartTroopsHuman, value, nameof(MultiplyStartTroopsHuman)); }
        [SyncHostOnly] public string AddStartTroopsAI { get => addStartTroopsAI; set => Set(ref addStartTroopsAI, value, nameof(AddStartTroopsAI)); }
        [SyncHostOnly] public string AddStartTroopsHuman { get => addStartTroopsHuman; set => Set(ref addStartTroopsHuman, value, nameof(AddStartTroopsHuman)); }

        [SyncHostOnly] public bool AdvancedOptionsEnabled { get => advancedOptionsEnabled; set => Set(ref advancedOptionsEnabled, value, nameof(AdvancedOptionsEnabled)); }
        [SyncHostOnly] public bool AdvancedSkirmishOptionsEnabled { get => advancedSkirmishOptionsEnabled; set => Set(ref advancedSkirmishOptionsEnabled, value, nameof(AdvancedSkirmishOptionsEnabled)); }
        [SyncHostOnly] public bool BetterHealers { get => betterHealers; set => Set(ref betterHealers, value, nameof(BetterHealers)); }
        [SyncHostOnly] public bool FasterPeasants { get => fasterPeasants; set => Set(ref fasterPeasants, value, nameof(FasterPeasants)); }
        [SyncHostOnly] public bool GlobalImprovedSiegeBehaviour { get => globalImprovedSiegeBehaviour; set => Set(ref globalImprovedSiegeBehaviour, value, nameof(GlobalImprovedSiegeBehaviour)); }
        [SyncHostOnly] public bool GlobalMoreAggressiveSiegeBehaviour { get => globalMoreAggressiveSiegeBehaviour; set => Set(ref globalMoreAggressiveSiegeBehaviour, value, nameof(GlobalMoreAggressiveSiegeBehaviour)); }
        [SyncHostOnly] public bool ImprovedArabSwordsman { get => improvedArabSwordsman; set => Set(ref improvedArabSwordsman, value, nameof(ImprovedArabSwordsman)); }
        [SyncHostOnly] public bool ImprovedFletchers { get => improvedFletchers; set => Set(ref improvedFletchers, value, nameof(ImprovedFletchers)); }
        [SyncHostOnly] public bool ImprovedLadderman { get => improvedLadderman; set => Set(ref improvedLadderman, value, nameof(ImprovedLadderman)); }
        [SyncHostOnly] public bool ImprovedSpearman { get => improvedSpearman; set => Set(ref improvedSpearman, value, nameof(ImprovedSpearman)); }
        [SyncHostOnly] public bool NerfEunuchs { get => nerfEunuchs; set => Set(ref nerfEunuchs, value, nameof(NerfEunuchs)); }
        [SyncHostOnly] public bool NoKnockdownWalls { get => noKnockdownWalls; set => Set(ref noKnockdownWalls, value, nameof(NoKnockdownWalls)); }
        [SyncHostOnly] public bool RebalancedHorseArchers { get => rebalancedHorseArchers; set => Set(ref rebalancedHorseArchers, value, nameof(RebalancedHorseArchers)); }
        [SyncHostOnly] public bool UncappedPeasants { get => uncappedPeasants; set => Set(ref uncappedPeasants, value, nameof(UncappedPeasants)); }
        [SyncHostOnly] public string EnemyHealthModifier { get => enemyHealthModifier; set => Set(ref enemyHealthModifier, value, nameof(EnemyHealthModifier)); }

        private void Set<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value))
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }
    }
}
