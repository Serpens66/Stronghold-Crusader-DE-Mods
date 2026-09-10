using SHCDESE.AICDecoder;

namespace TrailEditor.Core;

public sealed class TrailData
{
    public int FormatVersion { get; set; } = 60;
    public List<TrailPlayerSlot> Players { get; set; } = new();
    public TrailMapReference Map { get; set; } = new();
    public MultiplayerSetupData Setup { get; set; } = new();
    public bool ExtremeTroops { get; set; }
    public bool ExtremePowers { get; set; }
    public bool ExtremePowersAroundLord { get; set; }
    public bool AllowOutposts { get; set; }
    public bool CustomisedExtremeTrail { get; set; }
    public List<TrailAiSlot> AiSlots { get; set; } = new();
    public bool CustomTestMission { get; set; }
    public bool CustomTrail { get; set; }
    public int CustomTrailLevel { get; set; }
    public string CustomTrailName { get; set; } = string.Empty;
    public int CustomTrailDifficulty { get; set; }
}

public sealed class TrailPlayerSlot
{
    public int LordType { get; set; }
    public int Team { get; set; }
    public int Colour { get; set; }
}

public sealed class TrailMapReference
{
    public int SourceKind { get; set; }
    public string FileName { get; set; } = string.Empty;
}

public sealed class TrailAiSlot
{
    public int LordType { get; set; }
    public bool BuiltIn { get; set; }
    public bool Community { get; set; }
    public bool Historical { get; set; }
    public int Rotation { get; set; }
    public List<CustomAivData> Aivs { get; set; } = new();
    public bool BuiltInLord { get; set; }
    public CustomLordData? LordConfig { get; set; }
    public string LordName { get; set; } = string.Empty;
    public byte[]? ImageData { get; set; }
}

public sealed class CustomAivData
{
    public int FormatVersion { get; set; } = 1;
    public int LordType { get; set; }
    public bool BuiltIn { get; set; }
    public ulong Checksum { get; set; }
    public string Name { get; set; } = string.Empty;
    public short[] Data { get; set; } = Array.Empty<short>();
}

public sealed class CustomLordData
{
    public int FormatVersion { get; set; } = 1;
    public int LordType { get; set; }
    public ulong Checksum { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ConfigVersion { get; set; } = 2;
    public InternalAIC Config { get; set; }
}

public sealed class MultiplayerSetupData
{
    public int Fairness { get; set; }
    public int StartingGameSpeed { get; set; }
    public int StartingGoodsLevel { get; set; }
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
    public int GlobalImprovedSieging { get; set; }
    public int Healers { get; set; }
    public int Eunuchs { get; set; }
    public int NoGold { get; set; }
    public int GlobalImprovedSieging2 { get; set; }
    public int[] BuildingsAvailable { get; set; } = new int[13];
    public int[] GoodsAvailable { get; set; } = new int[25];
    public int[] TroopsAvailable { get; set; } = new int[32];
    public int[] PreferredAivs { get; set; } = new int[8];
    public int[] KeepLocationOrder { get; set; } = new int[8];
}

public sealed class TrailManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string OriginalFileName { get; set; } = string.Empty;
    public string OriginalSha256 { get; set; } = string.Empty;
    public string MapFile { get; set; } = "map.map";
    public TrailManifestData Trail { get; set; } = new();
}

public sealed class TrailManifestData
{
    public int FormatVersion { get; set; }
    public List<TrailPlayerSlot> Players { get; set; } = new();
    public TrailMapReference Map { get; set; } = new();
    public MultiplayerSetupData Setup { get; set; } = new();
    public bool ExtremeTroops { get; set; }
    public bool ExtremePowers { get; set; }
    public bool ExtremePowersAroundLord { get; set; }
    public bool AllowOutposts { get; set; }
    public bool CustomisedExtremeTrail { get; set; }
    public List<TrailAiSlotManifest> AiSlots { get; set; } = new();
    public bool CustomTestMission { get; set; }
    public bool CustomTrail { get; set; }
    public int CustomTrailLevel { get; set; }
    public string CustomTrailName { get; set; } = string.Empty;
    public int CustomTrailDifficulty { get; set; }
}

public sealed class TrailAiSlotManifest
{
    public int LordType { get; set; }
    public bool BuiltIn { get; set; }
    public bool Community { get; set; }
    public bool Historical { get; set; }
    public int Rotation { get; set; }
    public List<CustomAivManifest> Aivs { get; set; } = new();
    public bool BuiltInLord { get; set; }
    public CustomLordManifest? LordConfig { get; set; }
    public string LordName { get; set; } = string.Empty;
    public string? ImageFile { get; set; }
    public string? OriginalImageSha256 { get; set; }
}

public sealed class CustomAivManifest
{
    public int FormatVersion { get; set; }
    public int LordType { get; set; }
    public bool BuiltIn { get; set; }
    public ulong Checksum { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DataFile { get; set; } = string.Empty;
}

public sealed class CustomLordManifest
{
    public int FormatVersion { get; set; }
    public int LordType { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ConfigVersion { get; set; }
    public string LordJsonFile { get; set; } = string.Empty;
    public string InternalsFile { get; set; } = string.Empty;
}

public sealed class AicInternals
{
    public Dictionary<string, int> Fields { get; set; } = new(StringComparer.Ordinal);
}
