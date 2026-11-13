using APIShared.ModSettings;
using ExtendedData.Core;
using System;
using System.Linq;

namespace ExtendedData
{
    // Migrate runtime-owned copies before provider/property matching. The published
    // legacy JSON codec keeps its existing parsing, aliasing and mutation contract.
    internal static class MissionSettingsMigration
    {
        internal static ModSettingsDefinition Migrate(ModSettingsDefinition source)
        {
            if (source == null) return null;
            var shared = new MissionModSettingsDocument
            {
                SchemaVersion = source.SchemaVersion,
                Mods = source.Mods?.ToDictionary(item => item.Key, item => item.Value == null ? null :
                    new MissionModSettingsEntry { PlayerSettings = item.Value.PlayerSettings, Overrides = item.Value.Overrides },
                    StringComparer.Ordinal)
            };
            var migrated = ModSettingsMigration.MigrateMissionDocument(shared);
            return new ModSettingsDefinition
            {
                SchemaVersion = migrated.SchemaVersion,
                Mods = migrated.Mods.ToDictionary(item => item.Key, item => item.Value == null ? null :
                    new ModSettingsEntry { PlayerSettings = item.Value.PlayerSettings, Overrides = item.Value.Overrides },
                    StringComparer.Ordinal)
            };
        }
    }
}
