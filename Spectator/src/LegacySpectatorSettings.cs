using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MessagePack;

namespace Spectator
{
    // Only reads the documented settings envelopes. APIShared remains the sole settings writer.
    // The separate journal survives both default materialization and failed persistence/restarts.
    internal sealed class LegacySpectatorSettings
    {
        internal static readonly string[] Keys = {
            nameof(SpectatorViewModel.EnableSpectatorPerspective),
            nameof(SpectatorViewModel.EnableEliminatedPlayersBecomeSpectators)
        };
        private readonly string target, source, targetLegacy, sourceLegacy, journal;
        private readonly Dictionary<string, bool> pending = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> baseline = new Dictionary<string, bool>(StringComparer.Ordinal);
        private bool prepared, awaitingSource;
        internal bool RegistrationSafe { get; private set; }

        internal LegacySpectatorSettings(string documents, string targetLegacy, string sourceLegacy)
        {
            target = Path.Combine(documents, "SHCDE", "APIShared", "Spectator_Serp", "settings.msgpack");
            source = Path.Combine(documents, "SHCDE", "APIShared", "BugfixesAndQoL_Serp", "settings.msgpack");
            journal = Path.Combine(documents, "SHCDE", "Spectator", "legacy-settings-migration.json");
            this.targetLegacy = targetLegacy; this.sourceLegacy = sourceLegacy;
        }

        internal void Prepare(Dictionary<string, bool> defaults)
        {
            if (prepared) return;
            pending.Clear(); baseline.Clear();
            byte[] receipt = ReadOptional(journal);
            if (receipt != null)
            {
                var root = Shared.DependencyFreeJson.Parse(Encoding.UTF8.GetString(receipt)) as Dictionary<string, object>;
                if (root == null || !root.TryGetValue("Version", out object version) || !(version is int) || (int)version != 1 ||
                    !root.TryGetValue("AwaitingSource", out object waiting) || !(waiting is bool) ||
                    !root.TryGetValue("Eligible", out object eligible) || !(eligible is Dictionary<string, object> eligibleEntries) ||
                    !root.TryGetValue("Pending", out object values) || !(values is Dictionary<string, object> entries))
                    throw new InvalidDataException("Invalid Spectator migration journal.");
                awaitingSource = (bool)waiting;
                foreach (var entry in eligibleEntries)
                {
                    if (!awaitingSource || !Keys.Contains(entry.Key) || !(entry.Value is bool))
                        throw new InvalidDataException("Invalid Spectator migration eligibility.");
                    baseline.Add(entry.Key, (bool)entry.Value);
                }
                foreach (var entry in entries)
                {
                    if (awaitingSource || !Keys.Contains(entry.Key) || !(entry.Value is Dictionary<string, object> item) ||
                        !item.TryGetValue("Value", out object value) || !(value is bool) ||
                        !item.TryGetValue("Baseline", out object before) || !(before is bool))
                        throw new InvalidDataException("Invalid Spectator migration entry.");
                    pending.Add(entry.Key, (bool)value); baseline.Add(entry.Key, (bool)before);
                }
                RegistrationSafe = true;
            }
            else
            {
                Dictionary<string, byte[]> existing = ReadSettings(target, targetLegacy);
                foreach (string key in Keys)
                {
                    // Presence is authoritative, including a saved false. Validate it before recording success.
                    if (existing.TryGetValue(key, out byte[] saved)) { ReadBool(saved); continue; }
                    baseline.Add(key, defaults[key]);
                }
                // Persist initial target absence before touching a possibly unreadable BFQ file.
                awaitingSource = baseline.Count > 0;
                SaveJournal();
                RegistrationSafe = true;
            }
            if (awaitingSource)
            {
                Dictionary<string, byte[]> legacy = ReadSettings(source, sourceLegacy);
                foreach (string key in baseline.Keys.ToArray())
                    if (legacy.TryGetValue(key, out byte[] old)) pending.Add(key, ReadBool(old));
                    else baseline.Remove(key);
                awaitingSource = false;
                SaveJournal();
            }
            prepared = true;
        }

        internal void Apply(bool canEdit, Func<Dictionary<string, byte[]>> current, Action<Dictionary<string, byte[]>> apply)
        {
            Apply(_ => canEdit, current, apply);
        }

        internal void Apply(Func<string, bool> canEdit, Func<Dictionary<string, byte[]>> current, Action<Dictionary<string, byte[]>> apply)
        {
            if (!prepared || pending.Count == 0) return;
            var snapshot = current();
            var changes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var entry in pending.ToArray())
            {
                if (!canEdit(entry.Key)) continue;
                if (!snapshot.TryGetValue(entry.Key, out byte[] bytes)) return; // registration not active yet
                bool value = ReadBool(bytes);
                if (value != baseline[entry.Key] && value != entry.Value)
                { pending.Remove(entry.Key); baseline.Remove(entry.Key); continue; } // a newer local choice wins
                changes.Add(entry.Key, MessagePackSerializer.Serialize(entry.Value));
            }
            if (changes.Count > 0)
            {
                apply(changes);
                // The public controller reports no storage result. Verify its persisted snapshot before completing.
                var stored = ReadSettings(target, null);
                foreach (var entry in changes)
                    if (!stored.TryGetValue(entry.Key, out byte[] bytes) || ReadBool(bytes) != pending[entry.Key])
                        throw new IOException("Spectator migration persistence was not confirmed: " + entry.Key);
                foreach (string key in changes.Keys) { pending.Remove(key); baseline.Remove(key); }
            }
            SaveJournal();
        }

        // A later explicit edit must also win when a role restriction deferred the import.
        internal void PreserveEdit(string key)
        {
            if (!prepared || !pending.Remove(key)) return;
            baseline.Remove(key); SaveJournal();
        }

        private void SaveJournal()
        {
            var entries = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var entry in pending)
                entries.Add(entry.Key, new Dictionary<string, object> { { "Value", entry.Value }, { "Baseline", baseline[entry.Key] } });
            var eligible = new Dictionary<string, object>(StringComparer.Ordinal);
            if (awaitingSource) foreach (var entry in baseline) eligible.Add(entry.Key, entry.Value);
            string text = Shared.DependencyFreeJson.Serialize(new Dictionary<string, object> {
                { "Version", 1 }, { "AwaitingSource", awaitingSource }, { "Eligible", eligible }, { "Pending", entries }
            });
            Directory.CreateDirectory(Path.GetDirectoryName(journal));
            string temp = journal + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, text, new UTF8Encoding(false));
                // Existence/read failures are never interpreted as absence.
                if (ReadOptional(journal) == null) File.Move(temp, journal);
                else File.Replace(temp, journal, null);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        internal static string ResolveDocuments()
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (string folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                string.IsNullOrWhiteSpace(profile) ? null : Path.Combine(profile, "Documents"),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) })
                if (!string.IsNullOrWhiteSpace(folder) && Path.IsPathRooted(folder)) return Path.GetFullPath(folder);
            throw new IOException("No personal settings directory is available.");
        }

        internal static Dictionary<string, byte[]> ReadSettings(string canonical, string legacy)
        {
            byte[] data = ReadOptional(canonical);
            if (data == null && legacy != null) data = ReadOptional(legacy);
            if (data == null) return new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var envelope = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(data);
            if (envelope == null) throw new InvalidDataException("Empty settings envelope.");
            if (!envelope.TryGetValue("__SerpPresetSchemaVersion", out byte[] schema)) return envelope;
            int version = MessagePackSerializer.Deserialize<int>(schema);
            string key;
            if (version == 3) key = "__SerpCurrentSettings";
            else if (version == 1 || version == 2)
                key = envelope.TryGetValue("__SerpActivePreset", out byte[] selected) && MessagePackSerializer.Deserialize<int>(selected) == 1
                    ? "__SerpPreset2" : "__SerpPreset1";
            else throw new InvalidDataException("Unsupported settings schema: " + version);
            if (!envelope.TryGetValue(key, out byte[] nested) || nested == null)
                throw new InvalidDataException("Missing settings snapshot: " + key);
            return MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(nested) ??
                throw new InvalidDataException("Empty settings snapshot.");
        }

        private static bool ReadBool(byte[] bytes)
        {
            // Require the actual primitive encoding; no coercion from numeric or string values.
            if (bytes == null || bytes.Length != 1 || bytes[0] != 0xc2 && bytes[0] != 0xc3)
                throw new InvalidDataException("Invalid spectator setting boolean.");
            return bytes[0] == 0xc3;
        }

        private static byte[] ReadOptional(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > 4 * 1024 * 1024) throw new InvalidDataException("Settings file exceeds migration limit.");
                    byte[] data = new byte[checked((int)stream.Length)];
                    int offset = 0;
                    while (offset < data.Length)
                    {
                        int read = stream.Read(data, offset, data.Length - offset);
                        if (read == 0) throw new EndOfStreamException();
                        offset += read;
                    }
                    return data;
                }
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }
    }
}
