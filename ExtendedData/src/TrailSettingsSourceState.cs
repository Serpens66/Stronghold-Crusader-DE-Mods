using System;
using System.Collections.Generic;
using System.Linq;
using ExtendedData.Core;

namespace ExtendedData
{
    // Owns the authored rules, independently of pruning and edits to the live mission draft.
    // Publication follows successful activation; a failed switch cannot expose the previous mission.
    internal sealed class TrailSettingsSourceState
    {
        private ModSettingsDefinition document;
        internal bool IsAvailable => document != null;
        internal string ContextId { get; private set; } = string.Empty;

        internal T Activate<T>(ModSettingsDefinition source, string contextId, Func<T> activate)
        {
            Clear();
            ModSettingsDefinition snapshot = Copy(source);
            T result = activate();
            document = snapshot;
            ContextId = contextId ?? string.Empty;
            return result;
        }

        internal void Set(ModSettingsDefinition source, string contextId) =>
            Activate(source, contextId, () => true);

        internal ModSettingsDefinition Capture() => Copy(document);

        // Reset starts from the playable disabled baseline, retaining only explicit
        // Player/Fixed host values resolved by APIShared. Neither input is editable state.
        internal static Dictionary<string, byte[]> ComposeResetSnapshot(
            Dictionary<string, byte[]> disabled, Dictionary<string, byte[]> resolved, ModSettingsEntry entry)
        {
            var result = disabled.ToDictionary(item => item.Key,
                item => item.Value == null ? null : (byte[])item.Value.Clone(), StringComparer.Ordinal);
            foreach (string name in (entry?.PlayerSettings ?? Array.Empty<string>())
                .Concat(entry?.Overrides?.Keys ?? Enumerable.Empty<string>()))
                if (resolved.TryGetValue(name, out byte[] bytes)) result[name] = bytes == null ? null : (byte[])bytes.Clone();
            return result;
        }

        internal void Clear()
        {
            document = null;
            ContextId = string.Empty;
        }

        private static ModSettingsDefinition Copy(ModSettingsDefinition source) =>
            source == null ? null : ModSettingsJson.ParseObject(ModSettingsJson.Serialize(source));
    }
}
