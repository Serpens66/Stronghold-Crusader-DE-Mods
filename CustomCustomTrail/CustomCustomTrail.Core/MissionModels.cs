using System;
using System.Collections.Generic;

namespace CustomCustomTrail.Core
{
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
        public ModSettingsDefinition ModSettings { get; set; } = ModSettingsDefinition.CreateDisabled();
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
        public int SchemaVersion { get; set; } = 1;
        public Dictionary<string, ModSettingsEntry> Mods { get; set; } = new Dictionary<string, ModSettingsEntry>(StringComparer.Ordinal);

        public static ModSettingsDefinition CreateDisabled() => new ModSettingsDefinition();
    }

    public sealed class ModSettingsEntry
    {
        public bool Enabled { get; set; }
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public sealed class LoadedMission
    {
        public int TrailNumber { get; set; }
        public int MissionNumber { get; set; }
        public string JsonPath { get; set; }
        public string MissionRoot { get; set; }
        public CoopMissionDefinition Definition { get; set; }
        public IReadOnlyList<string> BundledFiles { get; set; }
    }
}
