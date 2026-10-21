using System;
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

        internal void Clear()
        {
            document = null;
            ContextId = string.Empty;
        }

        private static ModSettingsDefinition Copy(ModSettingsDefinition source) =>
            source == null ? null : ModSettingsJson.ParseObject(ModSettingsJson.Serialize(source));
    }
}
