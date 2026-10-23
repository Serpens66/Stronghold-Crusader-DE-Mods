using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ContentCodec = APIShared.ModSettings.MissionModSettingsJson;
using ContentDocument = APIShared.ModSettings.MissionModSettingsDocument;
using ContentEntry = APIShared.ModSettings.MissionModSettingsEntry;

namespace ExtendedData.Core
{
    // Published DTOs and filesystem APIs retain their assembly identities. Schema logic
    // lives only in Content; the bridge preserves legacy in-place and alias semantics.
    public static class ModSettingsJson
    {
        public const int MaximumBytes = ContentCodec.MaximumBytes;
        public const int MaximumSettingsPerMod = ContentCodec.MaximumSettingsPerMod;
        public static ModSettingsDefinition Read(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > MaximumBytes) throw new InvalidDataException("Mod settings exceed 8 MiB.");
                using (var reader = new StreamReader(stream, Encoding.UTF8)) return ParseObject(reader.ReadToEnd());
            }
        }

        public static ModSettingsDefinition ParseObject(string json) => FromContent(ContentCodec.ParseObject(json));

        internal static ModSettingsDefinition ParseObject(Dictionary<string, object> root) =>
            FromContent(ContentCodec.ParseDocument(root));

        public static ModSettingsDefinition NormalizeAndValidate(ModSettingsDefinition settings, string path)
        {
            var bridge = new DocumentBridge(settings ?? ModSettingsDefinition.CreateModDefaults());
            try { ContentCodec.NormalizeAndValidate(bridge.Document, path); }
            finally { bridge.CopyBack(); }
            return bridge.Legacy;
        }

        public static string[] RemoveUnknownSettings(ModSettingsDefinition document, string modId,
            IEnumerable<string> currentSettingNames)
        {
            if (document == null) return Array.Empty<string>();
            var bridge = new DocumentBridge(document);
            try { return ContentCodec.RemoveUnknownSettings(bridge.Document, modId, currentSettingNames); }
            finally { bridge.CopyBack(); }
        }

        public static string Serialize(ModSettingsDefinition document)
        {
            var bridge = new DocumentBridge(document ?? ModSettingsDefinition.CreateModDefaults());
            try { return ContentCodec.Serialize(bridge.Document); }
            finally { bridge.CopyBack(); }
        }

        public static bool IsSupportedValue(object value) => ContentCodec.IsSupportedValue(value);

        private static ModSettingsDefinition FromContent(ContentDocument document)
        {
            var legacy = new ModSettingsDefinition { SchemaVersion = document.SchemaVersion };
            var entries = new Dictionary<ContentEntry, ModSettingsEntry>();
            legacy.Mods = CopyMods(document.Mods, entries);
            return legacy;
        }

        private static Dictionary<string, ModSettingsEntry> CopyMods(
            Dictionary<string, ContentEntry> source, Dictionary<ContentEntry, ModSettingsEntry> entries)
        {
            if (source == null) return null;
            var result = new Dictionary<string, ModSettingsEntry>(source.Comparer);
            foreach (var pair in source)
            {
                if (pair.Value == null) { result.Add(pair.Key, null); continue; }
                if (!entries.TryGetValue(pair.Value, out var entry))
                {
                    entry = new ModSettingsEntry { PlayerSettings = pair.Value.PlayerSettings, Overrides = pair.Value.Overrides };
                    entries.Add(pair.Value, entry);
                }
                result.Add(pair.Key, entry);
            }
            return result;
        }

        private sealed class DocumentBridge
        {
            internal readonly ModSettingsDefinition Legacy;
            internal readonly ContentDocument Document;
            private readonly Dictionary<string, ContentEntry> originalMods;
            private readonly Dictionary<ContentEntry, ModSettingsEntry> entries = new Dictionary<ContentEntry, ModSettingsEntry>();

            internal DocumentBridge(ModSettingsDefinition legacy)
            {
                Legacy = legacy;
                var aliases = new Dictionary<ModSettingsEntry, ContentEntry>();
                if (legacy.Mods != null)
                {
                    originalMods = new Dictionary<string, ContentEntry>(legacy.Mods.Comparer);
                    foreach (var pair in legacy.Mods)
                    {
                        if (pair.Value == null) { originalMods.Add(pair.Key, null); continue; }
                        if (!aliases.TryGetValue(pair.Value, out var entry))
                        {
                            entry = new ContentEntry { PlayerSettings = pair.Value.PlayerSettings, Overrides = pair.Value.Overrides };
                            aliases.Add(pair.Value, entry);
                            entries.Add(entry, pair.Value);
                        }
                        originalMods.Add(pair.Key, entry);
                    }
                }
                Document = new ContentDocument { SchemaVersion = legacy.SchemaVersion, Mods = originalMods };
            }

            internal void CopyBack()
            {
                // Copy even on failure: the old codec exposed normalization of already visited
                // entries before rejecting a later invalid entry. Preserve shared entry aliases.
                foreach (var pair in entries)
                {
                    pair.Value.PlayerSettings = pair.Key.PlayerSettings;
                    pair.Value.Overrides = pair.Key.Overrides;
                }
                if (!ReferenceEquals(Document.Mods, originalMods))
                    Legacy.Mods = CopyMods(Document.Mods, entries);
                else if (originalMods != null)
                {
                    // RemoveUnknownSettings edits the existing dictionary, while normalization
                    // replaces it. Keep that distinction observable to existing consumers.
                    foreach (string key in Legacy.Mods.Keys.Where(key => !originalMods.ContainsKey(key)).ToArray())
                        Legacy.Mods.Remove(key);
                }
            }
        }
        public static void WriteAtomic(string path, ModSettingsDefinition document)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, Serialize(document), new UTF8Encoding(false));
                if (File.Exists(fullPath)) Shared.AtomicFileReplacement.Replace(temporary, fullPath);
                else File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

    }
}
