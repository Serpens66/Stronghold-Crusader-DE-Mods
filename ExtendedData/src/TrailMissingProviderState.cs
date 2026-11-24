using System;
using System.Collections.Generic;
using System.Linq;
using ExtendedData.Core;

namespace ExtendedData
{
    internal enum MissionProviderScope { Trail, Map }
    // An unresolved authored document is not an active mission-settings context.
    // Retain a detached copy so capture/export cannot silently drop its missing owners.
    internal sealed class TrailMissingProviderState
    {
        private ModSettingsDefinition document;
        private string[] missing = Array.Empty<string>();
        internal string Source { get; private set; } = string.Empty;
        internal MissionProviderScope Scope { get; private set; }
        internal bool IsBlocked => missing.Length != 0;
        internal string[] Missing => (string[])missing.Clone();
        internal ModSettingsDefinition Capture() => document == null ? null : MissionSettingsMigration.Migrate(document);

        internal bool Check(ModSettingsDefinition proposed, IEnumerable<string> compatibleProviders, string source,
            MissionProviderScope scope = MissionProviderScope.Trail)
        {
            if (proposed?.Mods == null) throw new ArgumentNullException(nameof(proposed));
            if (compatibleProviders == null) throw new ArgumentNullException(nameof(compatibleProviders));
            var available = new HashSet<string>(compatibleProviders, StringComparer.Ordinal);
            string[] unresolved = proposed.Mods.Keys.Where(id => !available.Contains(id))
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (unresolved.Length == 0) { Clear(); return false; }
            document = MissionSettingsMigration.Migrate(proposed);
            missing = unresolved;
            Source = source ?? string.Empty;
            Scope = scope;
            return true;
        }

        internal void Clear()
        { document = null; missing = Array.Empty<string>(); Source = string.Empty; Scope = MissionProviderScope.Trail; }
    }

    internal sealed class MissingMissionSettingsProviderException : InvalidOperationException
    {
        internal MissingMissionSettingsProviderException(string reason) : base(reason) { }
    }
}
