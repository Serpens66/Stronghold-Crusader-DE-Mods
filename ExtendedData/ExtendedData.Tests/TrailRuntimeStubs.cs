using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class CustomisationFileManager
{
    public readonly List<CustomLord> CustomLords = new();
    public readonly Dictionary<int, List<CustomAIV>> AivsByType = new();
    public List<CustomLord> GetCustomLords() => CustomLords;
    public List<CustomAIV> getLordAIVList(int type) =>
        AivsByType.TryGetValue(type, out var aivs) ? aivs : new List<CustomAIV>();
    public sealed class CustomLord
    {
        public string lordName;
        public List<CustomLordConfig> configs = new();
        public List<CustomAIV> aivs = new();
    }
    public sealed class CustomAIV { public ulong checksum; }
}

namespace CrusaderDE
{
    public sealed class FRONT_Multiplayer
    {
        public sealed class MPAIVInfo
        {
            public string lordName;
            public int lordType;
            public bool builtInLord;
            public CustomisationFileManager.CustomLordConfig lordConfig;
            public List<CustomisationFileManager.CustomAIV> aivs = new();
        }
    }
    public sealed class HUD_IngameMenu
    {
        public sealed class RestartSkirmishMapInfo
        {
            public List<int> lordTypes = new();
            public FRONT_Multiplayer.MPAIVInfo[] aivs;
        }
    }
}
namespace SHCDESE.API
{
    public sealed class GameAIManagerAPI
    {
        public static readonly GameAIManagerAPI Instance = new();
        public readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase);
        public bool IsSupportedCustomLord(string name) => Supported.Contains(name);
    }
}
namespace SHCDESE.API.Components.ModManager
{
    public sealed class GameAssetModManager
    {
        public static readonly GameAssetModManager Instance = new();
        public readonly Dictionary<string, string> Registered = new(StringComparer.OrdinalIgnoreCase);
        public bool TryGetRegisteredDirectory(string guid, out string directory) =>
            Registered.TryGetValue(guid, out directory);
    }
}
