using System;
using System.Collections.Generic;

namespace ExtendedData.Core
{
    public enum TrailSettingMode
    {
        ModDefault = 0,
        Player = 1,
        Fixed = 2,
    }

    public sealed class CoopTrailPackageManifest
    {
        public int SchemaVersion { get; set; } = 1;
        public string PackageId { get; set; }
        public string DisplayName { get; set; }
        public int MissionCount { get; set; }
        public string ContentFingerprint { get; set; }
    }

    public sealed class CoopTrailPackage
    {
        public string RootPath { get; set; }
        public string MissionsPath { get; set; }
        public string ManifestPath { get; set; }
        public CoopTrailPackageManifest Manifest { get; set; }
        public IReadOnlyList<LoadedMission> Missions { get; set; }
    }

    public sealed class CoopMissionDefinition
    {
        public int SchemaVersion { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; } = string.Empty;
        public MapReference Map { get; set; }
        public CoopSettings Settings { get; set; } = new CoopSettings();
        public List<PlayerDefinition> Players { get; set; } = new List<PlayerDefinition>();
        public ModSettingsDefinition ModSettings { get; set; } = ModSettingsDefinition.CreateModDefaults();
        public string ModSettingsError { get; set; }
    }

    public sealed class CoopSettings
    {
        public int Fairness { get; set; } = 3;
        public int StartingGoodsLevel { get; set; } = 2;
        public bool AllowBarracksHost { get; set; } = true;
        public bool AllowMercenaryPostHost { get; set; } = true;
        public bool AllowStockadeHost { get; set; } = true;
        public bool AllowBarracksGuest { get; set; } = true;
        public bool AllowMercenaryPostGuest { get; set; } = true;
        public bool AllowStockadeGuest { get; set; } = true;
        public MultiplayerSetupSettings MultiplayerSetup { get; set; }
    }

    public sealed class MultiplayerSetupSettings
    {
        public int StartingGameSpeed { get; set; }
        public int WinCondition { get; set; }
        public int AllowAutoTrading { get; set; }
        public int NoKnockdownWalls { get; set; }
        public int AutoSave { get; set; }
        public int PeaceTime { get; set; }
        public int NoCows { get; set; }
        public int NoDogs { get; set; }
        public int ExtremeTroops { get; set; }
        public int ExtremePowers { get; set; }
        public int ExtremePowersAroundLord { get; set; }
        public int AllowOutposts { get; set; }
        public int AdvancedOptions { get; set; }
        public int AdvancedSkirmishOptions { get; set; }
        public int PreBuild { get; set; }
        public int ImprovedArabSwordsmen { get; set; }
        public int ImprovedLaddermen { get; set; }
        public int ImprovedSpearmen { get; set; }
        public int RebalancedHorseArchers { get; set; }
        public int ImprovedFletchers { get; set; }
        public int UncappedPeasants { get; set; }
        public int FasterPeasants { get; set; }
        public int EnemyHitPoints { get; set; }
        public int ImprovedSieging { get; set; }
        public int Healers { get; set; }
        public int Eunuchs { get; set; }
        public int NoGold { get; set; }
        public int ImprovedSieging2 { get; set; }
        public int[] BuildingsAvailable { get; set; }
        public int[] GoodsAvailable { get; set; }
        public int[] TroopsAvailable { get; set; }
    }

    public sealed class PlayerDefinition
    {
        public bool Active { get; set; } = true;
        public int Team { get; set; } = 1;
        public int Colour { get; set; }
        public int KeepPosition { get; set; }
        public LordReference Lord { get; set; }
        public List<AivReference> Aivs { get; set; } = new List<AivReference>();
        public int PreferredAiv { get; set; } = -1;
        public int? NativePreferredAiv { get; set; }
    }

    public class AssetReference
    {
        public string Source { get; set; }
        public int? Id { get; set; }
        public string Name { get; set; }
        public string File { get; set; }
    }

    public sealed class MapReference : AssetReference
    {
    }

    public sealed class LordReference : AssetReference
    {
        public string Configuration { get; set; }
        public int BaseLordId { get; set; }
    }

    public sealed class AivReference : AssetReference
    {
        public string LordName { get; set; }
        public int Rotation { get; set; }
    }

    public sealed class ModSettingsDefinition
    {
        public int SchemaVersion { get; set; } = 3;
        public Dictionary<string, ModSettingsEntry> Mods { get; set; } = new Dictionary<string, ModSettingsEntry>(StringComparer.Ordinal);

        public static ModSettingsDefinition CreateModDefaults() => new ModSettingsDefinition();
    }

    public sealed class ModSettingsEntry
    {
        public string[] PlayerSettings { get; set; } = Array.Empty<string>();
        public Dictionary<string, object> Overrides { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public sealed class LoadedMission
    {
        public int TrailNumber { get; set; }
        public int MissionNumber { get; set; }
        public string JsonPath { get; set; }
        public string MissionRoot { get; set; }
        public string ModSettingsPath { get; set; }
        public string LordRequirementsPath { get; set; }
        public TrailLordRequirements LordRequirements { get; set; }
        public CoopMissionDefinition Definition { get; set; }
        public IReadOnlyList<string> BundledFiles { get; set; }
    }
}
