using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CoopTrailReplacer.Core
{
    [DataContract]
    public sealed class CoopMissionDefinition
    {
        [DataMember(Name = "schemaVersion", Order = 1, IsRequired = true)] public int SchemaVersion { get; set; }
        [DataMember(Name = "displayName", Order = 2, IsRequired = true)] public string DisplayName { get; set; }
        [DataMember(Name = "description", Order = 3)] public string Description { get; set; } = string.Empty;
        [DataMember(Name = "map", Order = 4, IsRequired = true)] public MapReference Map { get; set; }
        [DataMember(Name = "settings", Order = 5)] public CoopSettings Settings { get; set; } = new CoopSettings();
        [DataMember(Name = "players", Order = 6, IsRequired = true)] public List<PlayerDefinition> Players { get; set; } = new List<PlayerDefinition>();
        [DataMember(Name = "startConditions", Order = 7)] public StartConditionsDefinition StartConditions { get; set; } = new StartConditionsDefinition();
    }

    [DataContract]
    public sealed class CoopSettings
    {
        [DataMember(Name = "fairness", Order = 1)] public int Fairness { get; set; } = 3;
        [DataMember(Name = "startingGoodsLevel", Order = 2)] public int StartingGoodsLevel { get; set; } = 2;
        [DataMember(Name = "allowBarracksHost", Order = 3)] public bool AllowBarracksHost { get; set; } = true;
        [DataMember(Name = "allowMercenaryPostHost", Order = 4)] public bool AllowMercenaryPostHost { get; set; } = true;
        [DataMember(Name = "allowStockadeHost", Order = 5)] public bool AllowStockadeHost { get; set; } = true;
        [DataMember(Name = "allowBarracksGuest", Order = 6)] public bool AllowBarracksGuest { get; set; } = true;
        [DataMember(Name = "allowMercenaryPostGuest", Order = 7)] public bool AllowMercenaryPostGuest { get; set; } = true;
        [DataMember(Name = "allowStockadeGuest", Order = 8)] public bool AllowStockadeGuest { get; set; } = true;
    }

    [DataContract]
    public sealed class PlayerDefinition
    {
        [DataMember(Name = "active", Order = 1)] public bool Active { get; set; } = true;
        [DataMember(Name = "team", Order = 2)] public int Team { get; set; } = 1;
        [DataMember(Name = "colour", Order = 3)] public int Colour { get; set; }
        [DataMember(Name = "keepPosition", Order = 4, IsRequired = true)] public int KeepPosition { get; set; }
        [DataMember(Name = "lord", Order = 5)] public LordReference Lord { get; set; }
        [DataMember(Name = "aivs", Order = 6)] public List<AivReference> Aivs { get; set; } = new List<AivReference>();
        [DataMember(Name = "preferredAiv", Order = 7)] public int PreferredAiv { get; set; } = -1;
    }

    [DataContract]
    public class AssetReference
    {
        [DataMember(Name = "source", Order = 1, IsRequired = true)] public string Source { get; set; }
        [DataMember(Name = "id", Order = 2)] public int? Id { get; set; }
        [DataMember(Name = "name", Order = 3)] public string Name { get; set; }
        [DataMember(Name = "file", Order = 4)] public string File { get; set; }
    }

    [DataContract]
    public sealed class MapReference : AssetReference
    {
    }

    [DataContract]
    public sealed class LordReference : AssetReference
    {
        [DataMember(Name = "configuration", Order = 5)] public string Configuration { get; set; }
        [DataMember(Name = "baseLordId", Order = 6)] public int BaseLordId { get; set; }
    }

    [DataContract]
    public sealed class AivReference : AssetReference
    {
        [DataMember(Name = "lordName", Order = 5)] public string LordName { get; set; }
        [DataMember(Name = "rotation", Order = 6)] public int Rotation { get; set; }
    }

    [DataContract]
    public sealed class StartConditionsDefinition
    {
        [DataMember(Name = "setStartGoldAI", Order = 1)] public int SetStartGoldAI { get; set; } = -1;
        [DataMember(Name = "setStartGoldHuman", Order = 2)] public int SetStartGoldHuman { get; set; } = -1;
        [DataMember(Name = "addStartGoldAI", Order = 3)] public int AddStartGoldAI { get; set; }
        [DataMember(Name = "addStartGoldHuman", Order = 4)] public int AddStartGoldHuman { get; set; }
        [DataMember(Name = "multiplyStartTroopsAI", Order = 5)] public int MultiplyStartTroopsAI { get; set; } = 1;
        [DataMember(Name = "multiplyStartTroopsHuman", Order = 6)] public int MultiplyStartTroopsHuman { get; set; } = 1;
        [DataMember(Name = "startGoodsAI", Order = 7)] public Dictionary<string, int> StartGoodsAI { get; set; } = new Dictionary<string, int>();
        [DataMember(Name = "startGoodsHuman", Order = 8)] public Dictionary<string, int> StartGoodsHuman { get; set; } = new Dictionary<string, int>();
        [DataMember(Name = "addStartTroopsAI", Order = 9)] public Dictionary<string, int> AddStartTroopsAI { get; set; } = new Dictionary<string, int>();
        [DataMember(Name = "addStartTroopsHuman", Order = 10)] public Dictionary<string, int> AddStartTroopsHuman { get; set; } = new Dictionary<string, int>();
    }

    public sealed class LoadedMission
    {
        public int TrailNumber { get; set; }
        public int MissionNumber { get; set; }
        public string JsonPath { get; set; }
        public string MissionRoot { get; set; }
        public CoopMissionDefinition Definition { get; set; }
        public byte[] CanonicalJson { get; set; }
        public IReadOnlyList<string> BundledFiles { get; set; }
        public string Hash { get; set; }
    }
}
