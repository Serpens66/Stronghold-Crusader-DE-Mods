using System;
using System.Collections.Generic;

namespace ExtendedData.Core
{
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

}
