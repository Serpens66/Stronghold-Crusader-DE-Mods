using SHCDESE.API.Components.Network;
using SHCDESE.API.Components.ModManager;
using System;
using System.Windows.Input;

namespace ExtremePowers.Settings
{
    public sealed class ExtremePowersSettings : Shared.PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true, enableGoldReplacement;
        private int regenerationPercent = 100;
        private int arrowCost = 636, healCost = 1272, spearmenCost = 1908, engineersCost = 2544, macemenCost = 3180, goldCost = 3816, knightsCost = 4452, rockCost = 5088;
        private int arrowDamage = 6000, arrowRadius = 6, arrowMode = 1, healAmount = 8000, healRadius = 6, rockDamage = 18000, rockRadius = 9, rockMode;
        private int spearmenType = 24, spearmenCount = 20, engineersType = 30, engineersCount = 14, macemenType = 26, macemenCount = 20, knightsType = 28, knightsCount = 10, goldMinimum = 1000, goldMaximum = 2499;
        private int demoUnitType = 2, demoSpawnCount = 10;
        private string apiProtocolReport = "pending";
        private string demoName = "War Chest Reinforcements", demoTooltip = "Spawn configured reinforcements at a map point.", demoSprite = "extreme power 3";

        public ExtremePowersSettings() { ResetToDefaultCommand = new LocalCommand(Reset); }
        protected override string ResolveSettingsUiText(string key, string fallback) => SerpLocalization.Get(key);
        protected override void ConfigurePerPlayerLobbySettings(Shared.PerPlayerLobbySettingsBuilder settings)
        {
            settings.ResetSlotsWith(nameof(ApiProtocolReport), () => null).RequireReport(nameof(ApiProtocolReport), value => !string.IsNullOrWhiteSpace(value as string) && string.Equals(value as string, ApiProtocolReport, StringComparison.Ordinal));
        }
        public ICommand ResetToDefaultCommand { get; }
        public string TitleText => L("ExtremePowers.Title"); public string HelpText => L("ExtremePowers.Help"); public string EnableText => L("Common.EnableMod"); public string ResetText => L("Common.ResetToDefault"); public string CostsText => L("ExtremePowers.Costs"); public string EffectsText => L("ExtremePowers.Effects"); public string DemoText => L("ExtremePowers.Demo"); public string GenericHelp => L("ExtremePowers.GenericHelp");
        public string RegenerationText => L("ExtremePowers.Regeneration"); public string ArrowText => L("ExtremePowers.Arrow"); public string HealText => L("ExtremePowers.Heal"); public string SpearmenText => L("ExtremePowers.Spearmen"); public string EngineersText => L("ExtremePowers.Engineers"); public string MacemenText => L("ExtremePowers.Macemen"); public string GoldText => L("ExtremePowers.Gold"); public string KnightsText => L("ExtremePowers.Knights"); public string RockText => L("ExtremePowers.Rock");
        public string ArrowDamageText => L("ExtremePowers.ArrowDamage"); public string ArrowRadiusText => L("ExtremePowers.ArrowRadius"); public string ArrowModeText => L("ExtremePowers.ArrowSpread"); public string HealAmountText => L("ExtremePowers.HealAmount"); public string HealRadiusText => L("ExtremePowers.HealRadius"); public string RockDamageText => L("ExtremePowers.RockDamage"); public string RockRadiusText => L("ExtremePowers.RockRadius"); public string RockModeText => L("ExtremePowers.RockSpread");
        public string SpearmenSpawnText => L("ExtremePowers.SpearmenSpawn"); public string EngineersSpawnText => L("ExtremePowers.EngineersSpawn"); public string MacemenSpawnText => L("ExtremePowers.MacemenSpawn"); public string KnightsSpawnText => L("ExtremePowers.KnightsSpawn"); public string GoldRangeText => L("ExtremePowers.GoldRange"); public string EnableDemoText => L("ExtremePowers.EnableDemo"); public string DemoUnitTypeText => L("ExtremePowers.DemoUnitType"); public string DemoUnitCountText => L("ExtremePowers.DemoUnitCount"); public string HudNameText => L("ExtremePowers.HudName"); public string HudTooltipText => L("ExtremePowers.HudTooltip"); public string HudSpriteText => L("ExtremePowers.HudSprite"); public string NoClientOptionsText => L("ExtremePowers.NoClientOptions");

        [SyncHostOnly] public bool EnableMod { get => enableMod; set => Set(ref enableMod, value, nameof(EnableMod)); }
        [SyncHostOnly] public int RegenerationPercent { get => regenerationPercent; set => Set(ref regenerationPercent, Clamp(value, 0, 1000), nameof(RegenerationPercent)); }
        [SyncHostOnly] public int ArrowCost { get => arrowCost; set => Set(ref arrowCost, NonNegative(value), nameof(ArrowCost)); }
        [SyncHostOnly] public int HealCost { get => healCost; set => Set(ref healCost, NonNegative(value), nameof(HealCost)); }
        [SyncHostOnly] public int SpearmenCost { get => spearmenCost; set => Set(ref spearmenCost, NonNegative(value), nameof(SpearmenCost)); }
        [SyncHostOnly] public int EngineersCost { get => engineersCost; set => Set(ref engineersCost, NonNegative(value), nameof(EngineersCost)); }
        [SyncHostOnly] public int MacemenCost { get => macemenCost; set => Set(ref macemenCost, NonNegative(value), nameof(MacemenCost)); }
        [SyncHostOnly] public int GoldCost { get => goldCost; set => Set(ref goldCost, NonNegative(value), nameof(GoldCost)); }
        [SyncHostOnly] public int RockCost { get => rockCost; set => Set(ref rockCost, NonNegative(value), nameof(RockCost)); }
        [SyncHostOnly] public int KnightsCost { get => knightsCost; set => Set(ref knightsCost, NonNegative(value), nameof(KnightsCost)); }
        [SyncHostOnly] public int ArrowDamage { get => arrowDamage; set => Set(ref arrowDamage, NonNegative(value), nameof(ArrowDamage)); }
        [SyncHostOnly] public int ArrowRadius { get => arrowRadius; set => Set(ref arrowRadius, NonNegative(value), nameof(ArrowRadius)); }
        [SyncHostOnly] public int ArrowMode { get => arrowMode; set => Set(ref arrowMode, Clamp(value, 0, 1), nameof(ArrowMode)); }
        [SyncHostOnly] public int HealAmount { get => healAmount; set => Set(ref healAmount, NonNegative(value), nameof(HealAmount)); }
        [SyncHostOnly] public int HealRadius { get => healRadius; set => Set(ref healRadius, NonNegative(value), nameof(HealRadius)); }
        [SyncHostOnly] public int SpearmenType { get => spearmenType; set => Set(ref spearmenType, NonNegative(value), nameof(SpearmenType)); }
        [SyncHostOnly] public int SpearmenCount { get => spearmenCount; set => Set(ref spearmenCount, NonNegative(value), nameof(SpearmenCount)); }
        [SyncHostOnly] public int EngineersType { get => engineersType; set => Set(ref engineersType, NonNegative(value), nameof(EngineersType)); }
        [SyncHostOnly] public int EngineersCount { get => engineersCount; set => Set(ref engineersCount, NonNegative(value), nameof(EngineersCount)); }
        [SyncHostOnly] public int MacemenType { get => macemenType; set => Set(ref macemenType, NonNegative(value), nameof(MacemenType)); }
        [SyncHostOnly] public int MacemenCount { get => macemenCount; set => Set(ref macemenCount, NonNegative(value), nameof(MacemenCount)); }
        [SyncHostOnly] public int GoldMinimum { get => goldMinimum; set => Set(ref goldMinimum, NonNegative(value), nameof(GoldMinimum)); }
        [SyncHostOnly] public int GoldMaximum { get => goldMaximum; set => Set(ref goldMaximum, NonNegative(value), nameof(GoldMaximum)); }
        [SyncHostOnly] public int RockDamage { get => rockDamage; set => Set(ref rockDamage, NonNegative(value), nameof(RockDamage)); }
        [SyncHostOnly] public int RockRadius { get => rockRadius; set => Set(ref rockRadius, NonNegative(value), nameof(RockRadius)); }
        [SyncHostOnly] public int RockMode { get => rockMode; set => Set(ref rockMode, Clamp(value, 0, 1), nameof(RockMode)); }
        [SyncHostOnly] public int KnightsType { get => knightsType; set => Set(ref knightsType, NonNegative(value), nameof(KnightsType)); }
        [SyncHostOnly] public int KnightsCount { get => knightsCount; set => Set(ref knightsCount, NonNegative(value), nameof(KnightsCount)); }
        [SyncHostOnly] public bool EnableGoldReplacement { get => enableGoldReplacement; set => Set(ref enableGoldReplacement, value, nameof(EnableGoldReplacement)); }
        [SyncHostOnly] public int DemoUnitType { get => demoUnitType; set => Set(ref demoUnitType, NonNegative(value), nameof(DemoUnitType)); }
        [SyncHostOnly] public int DemoSpawnCount { get => demoSpawnCount; set => Set(ref demoSpawnCount, Clamp(value, 0, 1000), nameof(DemoSpawnCount)); }
        [SyncHostOnly] public string DemoName { get => demoName; set => Set(ref demoName, value ?? string.Empty, nameof(DemoName)); }
        [SyncHostOnly] public string DemoTooltip { get => demoTooltip; set => Set(ref demoTooltip, value ?? string.Empty, nameof(DemoTooltip)); }
        [SyncHostOnly] public string DemoSprite { get => demoSprite; set => Set(ref demoSprite, value ?? string.Empty, nameof(DemoSprite)); }
        [SyncPerPlayer, DoNotPersist] public string ApiProtocolReport { get => apiProtocolReport; set { value = value ?? string.Empty; if (apiProtocolReport == value) return; apiProtocolReport = value; OnPropertyChanged(nameof(ApiProtocolReport)); } }
        [DoNotPersist] public string[] ApiProtocolReportData { get; } = new string[9];

        private void Reset()
        {
            if (!CanEditHostSettings) return;
            EnableMod = true; RegenerationPercent = 100;
            ArrowCost = 636; HealCost = 1272; SpearmenCost = 1908; EngineersCost = 2544; MacemenCost = 3180; GoldCost = 3816; KnightsCost = 4452; RockCost = 5088;
            ArrowDamage = 6000; ArrowRadius = 6; ArrowMode = 1; HealAmount = 8000; HealRadius = 6;
            SpearmenType = 24; SpearmenCount = 20; EngineersType = 30; EngineersCount = 14; MacemenType = 26; MacemenCount = 20; KnightsType = 28; KnightsCount = 10;
            GoldMinimum = 1000; GoldMaximum = 2499; RockDamage = 18000; RockRadius = 9; RockMode = 0; EnableGoldReplacement = false;
            DemoUnitType = 2; DemoSpawnCount = 10; DemoName = "War Chest Reinforcements";
            DemoTooltip = "Spawn configured reinforcements at a map point."; DemoSprite = "extreme power 3";
        }
        private void Set<T>(ref T field, T value, string name) { if (!CanMutateSetting(name) || Equals(field, value)) return; field = value; OnPropertyChanged(name); }
        private static int NonNegative(int value) => value < 0 ? 0 : value;
        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
        private static string L(string key) => SerpLocalization.Get(key);
        private sealed class LocalCommand : ICommand
        {
            private readonly Action execute;
            internal LocalCommand(Action execute) { this.execute = execute; }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => execute();
            public event EventHandler CanExecuteChanged { add { } remove { } }
        }
    }
}
