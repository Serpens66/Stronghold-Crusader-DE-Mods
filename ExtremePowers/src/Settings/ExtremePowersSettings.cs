using SHCDESE.API.Components.Network;
using SHCDESE.API.Components.ModManager;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace ExtremePowers.Settings
{
    public sealed class ExtremePowersSettings : Shared.PresetLobbyModSettingsViewModel
    {
        private static readonly int[] SelectableUnitTypes = { 22, 23, 24, 25, 26, 27, 28, 30, 70, 75, 44 };
        private static readonly string[] SelectableUnitKeys = { "Archer", "Crossbowman", "Spearman", "Pikeman", "Maceman", "Swordsman", "Knight", "Engineer", "ArabArcher", "ArabSwordsman", "Deer" };
        private static readonly string[] SelectableSpriteKeys = { "ArrowVolley", "Heal", "Spearmen", "Engineers", "Macemen", "Gold", "Knights", "RockVolley" };
        private bool enableMod = true, enableGoldReplacement;
        private int regenerationPercent = 100;
        private int arrowDamage = 6000, arrowRadius = 6, arrowMode = 1, healAmount = 8000, healRadius = 6, rockDamage = 18000, rockRadius = 9, rockMode;
        private int spearmenType = 24, spearmenCount = 20, engineersType = 30, engineersCount = 14, macemenType = 26, macemenCount = 20, knightsType = 28, knightsCount = 10, goldMinimum = 1000, goldMaximum = 2499;
        private int demoUnitType = 24, demoOwner = -1, demoSpawnCount = 10;
        private string apiProtocolReport = "pending";
        private string demoName = "War Chest Reinforcements", demoTooltip = "Spawn configured reinforcements at a map point.", demoSprite = "extreme power 3";

        public ExtremePowersSettings()
        {
            ResetToDefaultCommand = new LocalCommand(Reset);
            UnitTypeOptions = CreateOptions("ExtremePowers.Unit.", SelectableUnitKeys);
            SpriteOptions = CreateOptions("ExtremePowers.Sprite.", SelectableSpriteKeys);
            DemoOwnerOptions = CreateOwnerOptions();
        }
        protected override string ResolveSettingsUiText(string key, string fallback) => SerpLocalization.Get(key);
        protected override void ConfigurePerPlayerLobbySettings(Shared.PerPlayerLobbySettingsBuilder settings)
        {
            settings.ResetSlotsWith(nameof(ApiProtocolReport), () => null).RequireReport(nameof(ApiProtocolReport), value => !string.IsNullOrWhiteSpace(value as string) && string.Equals(value as string, ApiProtocolReport, StringComparison.Ordinal));
        }
        public ICommand ResetToDefaultCommand { get; }
        public IReadOnlyList<string> UnitTypeOptions { get; }
        public IReadOnlyList<string> SpriteOptions { get; }
        public IReadOnlyList<string> DemoOwnerOptions { get; }
        public string TitleText => L("ExtremePowers.Title"); public string HelpText => L("ExtremePowers.Help"); public string EnableText => L("Common.EnableMod"); public string ResetText => L("Common.ResetToDefault"); public string EffectsText => L("ExtremePowers.Effects"); public string SpawnEffectsText => L("ExtremePowers.SpawnEffects"); public string GoldEffectText => L("ExtremePowers.GoldEffect"); public string DemoText => L("ExtremePowers.Demo"); public string GenericHelp => L("ExtremePowers.GenericHelp");
        public string RegenerationText => L("ExtremePowers.Regeneration");
        public string ArrowDamageText => L("ExtremePowers.ArrowDamage"); public string ArrowRadiusText => L("ExtremePowers.ArrowRadius"); public string ArrowModeText => L("ExtremePowers.ArrowSpread"); public string HealAmountText => L("ExtremePowers.HealAmount"); public string HealRadiusText => L("ExtremePowers.HealRadius"); public string RockDamageText => L("ExtremePowers.RockDamage"); public string RockRadiusText => L("ExtremePowers.RockRadius"); public string RockModeText => L("ExtremePowers.RockSpread");
        public string SpearmenSpawnText => L("ExtremePowers.SpearmenSpawn"); public string EngineersSpawnText => L("ExtremePowers.EngineersSpawn"); public string MacemenSpawnText => L("ExtremePowers.MacemenSpawn"); public string KnightsSpawnText => L("ExtremePowers.KnightsSpawn"); public string GoldRangeText => L("ExtremePowers.GoldRange"); public string EnableDemoText => L("ExtremePowers.EnableDemo"); public string DemoUnitTypeText => L("ExtremePowers.DemoUnitType"); public string DemoOwnerText => L("ExtremePowers.DemoOwner"); public string DemoUnitCountText => L("ExtremePowers.DemoUnitCount"); public string HudNameText => L("ExtremePowers.HudName"); public string HudTooltipText => L("ExtremePowers.HudTooltip"); public string HudSpriteText => L("ExtremePowers.HudSprite"); public string NoClientOptionsText => L("ExtremePowers.NoClientOptions");
        public string RegenerationHelpText => L("ExtremePowers.RegenerationHelp"); public string VolleyHelpText => L("ExtremePowers.VolleyHelp"); public string HealingHelpText => L("ExtremePowers.HealingHelp"); public string SpawnHelpText => L("ExtremePowers.SpawnHelp"); public string GoldHelpText => L("ExtremePowers.GoldHelp"); public string DemoEnableHelpText => L("ExtremePowers.DemoEnableHelp"); public string DemoUnitHelpText => L("ExtremePowers.DemoUnitHelp"); public string DemoOwnerHelpText => L("ExtremePowers.DemoOwnerHelp"); public string DemoCountHelpText => L("ExtremePowers.DemoCountHelp"); public string HudNameHelpText => L("ExtremePowers.HudNameHelp"); public string HudTooltipHelpText => L("ExtremePowers.HudTooltipHelp"); public string HudSpriteHelpText => L("ExtremePowers.HudSpriteHelp");

        [SyncHostOnly] public bool EnableMod { get => enableMod; set => Set(ref enableMod, value, nameof(EnableMod)); }
        [SyncHostOnly] public int RegenerationPercent { get => regenerationPercent; set => Set(ref regenerationPercent, Clamp(value, 0, 1000), nameof(RegenerationPercent)); }
        [SyncHostOnly] public int ArrowDamage { get => arrowDamage; set => Set(ref arrowDamage, NonNegative(value), nameof(ArrowDamage)); }
        [SyncHostOnly] public int ArrowRadius { get => arrowRadius; set => Set(ref arrowRadius, NonNegative(value), nameof(ArrowRadius)); }
        [SyncHostOnly] public int ArrowMode { get => arrowMode; set => Set(ref arrowMode, Clamp(value, 0, 1), nameof(ArrowMode)); }
        [SyncHostOnly] public int HealAmount { get => healAmount; set => Set(ref healAmount, NonNegative(value), nameof(HealAmount)); }
        [SyncHostOnly] public int HealRadius { get => healRadius; set => Set(ref healRadius, NonNegative(value), nameof(HealRadius)); }
        [SyncHostOnly] public int SpearmenType { get => spearmenType; set => SetUnitType(ref spearmenType, value, nameof(SpearmenType), nameof(SpearmenTypeIndex)); }
        [SyncHostOnly] public int SpearmenCount { get => spearmenCount; set => Set(ref spearmenCount, NonNegative(value), nameof(SpearmenCount)); }
        [SyncHostOnly] public int EngineersType { get => engineersType; set => SetUnitType(ref engineersType, value, nameof(EngineersType), nameof(EngineersTypeIndex)); }
        [SyncHostOnly] public int EngineersCount { get => engineersCount; set => Set(ref engineersCount, NonNegative(value), nameof(EngineersCount)); }
        [SyncHostOnly] public int MacemenType { get => macemenType; set => SetUnitType(ref macemenType, value, nameof(MacemenType), nameof(MacemenTypeIndex)); }
        [SyncHostOnly] public int MacemenCount { get => macemenCount; set => Set(ref macemenCount, NonNegative(value), nameof(MacemenCount)); }
        [SyncHostOnly] public int GoldMinimum { get => goldMinimum; set => Set(ref goldMinimum, NonNegative(value), nameof(GoldMinimum)); }
        [SyncHostOnly] public int GoldMaximum { get => goldMaximum; set => Set(ref goldMaximum, NonNegative(value), nameof(GoldMaximum)); }
        [SyncHostOnly] public int RockDamage { get => rockDamage; set => Set(ref rockDamage, NonNegative(value), nameof(RockDamage)); }
        [SyncHostOnly] public int RockRadius { get => rockRadius; set => Set(ref rockRadius, NonNegative(value), nameof(RockRadius)); }
        [SyncHostOnly] public int RockMode { get => rockMode; set => Set(ref rockMode, Clamp(value, 0, 1), nameof(RockMode)); }
        [SyncHostOnly] public int KnightsType { get => knightsType; set => SetUnitType(ref knightsType, value, nameof(KnightsType), nameof(KnightsTypeIndex)); }
        [SyncHostOnly] public int KnightsCount { get => knightsCount; set => Set(ref knightsCount, NonNegative(value), nameof(KnightsCount)); }
        [SyncHostOnly] public bool EnableGoldReplacement { get => enableGoldReplacement; set => Set(ref enableGoldReplacement, value, nameof(EnableGoldReplacement)); }
        [SyncHostOnly] public int DemoUnitType { get => demoUnitType; set { int previous = demoUnitType; SetUnitType(ref demoUnitType, value, nameof(DemoUnitType), nameof(DemoUnitTypeIndex)); if (demoUnitType == 44) SetDemoOwner(0); if (previous != demoUnitType) OnPropertyChanged(nameof(IsDemoOwnerSelectable)); } }
        [SyncHostOnly] public int DemoOwner { get => demoOwner; set => SetDemoOwner(value); }
        [SyncHostOnly] public int DemoSpawnCount { get => demoSpawnCount; set => Set(ref demoSpawnCount, Clamp(value, 0, 1000), nameof(DemoSpawnCount)); }
        [SyncHostOnly] public string DemoName { get => demoName; set => Set(ref demoName, value ?? string.Empty, nameof(DemoName)); }
        [SyncHostOnly] public string DemoTooltip { get => demoTooltip; set => Set(ref demoTooltip, value ?? string.Empty, nameof(DemoTooltip)); }
        [SyncHostOnly] public string DemoSprite { get => demoSprite; set { value = value ?? string.Empty; if (!CanMutateSetting(nameof(DemoSprite)) || demoSprite == value) return; demoSprite = value; OnPropertyChanged(nameof(DemoSprite)); OnPropertyChanged(nameof(DemoSpriteIndex)); } }
        [DoNotPersist] public int SpearmenTypeIndex { get => IndexOfUnit(SpearmenType); set => SetIndexedUnit(value, v => SpearmenType = v); }
        [DoNotPersist] public int EngineersTypeIndex { get => IndexOfUnit(EngineersType); set => SetIndexedUnit(value, v => EngineersType = v); }
        [DoNotPersist] public int MacemenTypeIndex { get => IndexOfUnit(MacemenType); set => SetIndexedUnit(value, v => MacemenType = v); }
        [DoNotPersist] public int KnightsTypeIndex { get => IndexOfUnit(KnightsType); set => SetIndexedUnit(value, v => KnightsType = v); }
        [DoNotPersist] public int DemoUnitTypeIndex { get => IndexOfUnit(DemoUnitType); set => SetIndexedUnit(value, v => DemoUnitType = v); }
        [DoNotPersist] public int DemoOwnerIndex { get => DemoOwner + 1; set { if ((uint)value < 10) DemoOwner = value - 1; } }
        [DoNotPersist] public bool IsDemoOwnerSelectable => DemoUnitType != 44;
        [DoNotPersist] public int DemoSpriteIndex { get => IndexOfSprite(DemoSprite); set { if ((uint)value < 8) DemoSprite = "extreme power " + (value + 1); } }
        [SyncPerPlayer, DoNotPersist] public string ApiProtocolReport { get => apiProtocolReport; set { value = value ?? string.Empty; if (apiProtocolReport == value) return; apiProtocolReport = value; OnPropertyChanged(nameof(ApiProtocolReport)); } }
        [DoNotPersist] public string[] ApiProtocolReportData { get; } = new string[9];

        private void Reset()
        {
            if (!CanEditHostSettings) return;
            EnableMod = true; RegenerationPercent = 100;
            ArrowDamage = 6000; ArrowRadius = 6; ArrowMode = 1; HealAmount = 8000; HealRadius = 6;
            SpearmenType = 24; SpearmenCount = 20; EngineersType = 30; EngineersCount = 14; MacemenType = 26; MacemenCount = 20; KnightsType = 28; KnightsCount = 10;
            GoldMinimum = 1000; GoldMaximum = 2499; RockDamage = 18000; RockRadius = 9; RockMode = 0; EnableGoldReplacement = false;
            DemoUnitType = 24; DemoOwner = -1; DemoSpawnCount = 10; DemoName = "War Chest Reinforcements";
            DemoTooltip = "Spawn configured reinforcements at a map point."; DemoSprite = "extreme power 3";
        }
        protected override void OnSettingsSnapshotApplied()
        {
            base.OnSettingsSnapshotApplied();
            // The initial unpublished demo stored type 2 (Burning Man). Keep the preset, but migrate unsafe UI choices.
            if (IndexOfUnit(SpearmenType) < 0) SpearmenType = 24;
            if (IndexOfUnit(EngineersType) < 0) EngineersType = 30;
            if (IndexOfUnit(MacemenType) < 0) MacemenType = 26;
            if (IndexOfUnit(KnightsType) < 0) KnightsType = 28;
            if (IndexOfUnit(DemoUnitType) < 0) DemoUnitType = 24;
            DemoOwner = DemoUnitType == 44 ? 0 : Clamp(DemoOwner, -1, 8);
            if (IndexOfSprite(DemoSprite) < 0) DemoSprite = "extreme power 3";
        }
        public int ResolveDemoOwner(int activatingPlayerId) => DemoUnitType == 44 ? 0 : DemoOwner < 0 ? activatingPlayerId : DemoOwner;
        private void Set<T>(ref T field, T value, string name) { if (!CanMutateSetting(name) || Equals(field, value)) return; field = value; OnPropertyChanged(name); }
        private void SetUnitType(ref int field, int value, string propertyName, string indexPropertyName)
        {
            value = NonNegative(value); if (!CanMutateSetting(propertyName) || field == value) return; field = value; OnPropertyChanged(propertyName); OnPropertyChanged(indexPropertyName);
        }
        private static void SetIndexedUnit(int index, Action<int> setter) { if ((uint)index < (uint)SelectableUnitTypes.Length) setter(SelectableUnitTypes[index]); }
        private void SetDemoOwner(int value)
        {
            value = DemoUnitType == 44 ? 0 : Clamp(value, -1, 8);
            if (!CanMutateSetting(nameof(DemoOwner)) || demoOwner == value) return;
            demoOwner = value; OnPropertyChanged(nameof(DemoOwner)); OnPropertyChanged(nameof(DemoOwnerIndex));
        }
        private static int IndexOfUnit(int value) => Array.IndexOf(SelectableUnitTypes, value);
        private static int IndexOfSprite(string value)
        {
            for (int index = 0; index < 8; index++) if (string.Equals(value, "extreme power " + (index + 1), StringComparison.OrdinalIgnoreCase)) return index;
            return -1;
        }
        private static IReadOnlyList<string> CreateOptions(string prefix, string[] keys)
        {
            string[] values = new string[keys.Length]; for (int index = 0; index < keys.Length; index++) values[index] = L(prefix + keys[index]); return values;
        }
        private static IReadOnlyList<string> CreateOwnerOptions()
        {
            string[] values = new string[10]; values[0] = L("ExtremePowers.Owner.TriggeringPlayer"); values[1] = L("ExtremePowers.Owner.Nature");
            for (int playerId = 1; playerId <= 8; playerId++) values[playerId + 1] = string.Format(L("ExtremePowers.Owner.PlayerFormat"), playerId);
            return values;
        }
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
