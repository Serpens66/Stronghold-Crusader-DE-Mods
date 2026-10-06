using BepInEx.Bootstrap;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ExtendedData
{
    // Fixes owns the dictionaries and map override precedence. Never write its slot overrides.
    internal sealed class FixesLordPreferencesBridge
    {
        private readonly Dictionary<string, object> originals = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> originallyPresent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> touched = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private IDictionary custom;
        private IDictionary extended;
        private Type entryType;
        private FieldInfo mapSlots;
        internal bool Installed => Chainloader.PluginInfos.ContainsKey("fixes");

        internal void Validate()
        {
            if (!Installed || custom != null) return;
            Assembly assembly = Chainloader.PluginInfos["fixes"].Instance.GetType().Assembly;
            Type plugin = assembly.GetType("Fixes.Boostrap.Plugin", false);
            Type entry = assembly.GetType("Fixes.Config.CustomLordPreferencesEntry", false);
            object instance = plugin?.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            object dictionary = plugin?.GetProperty("CustomLordPreferences", BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);
            if (entry == null || dictionary?.GetType() != typeof(Dictionary<,>).MakeGenericType(typeof(string), entry))
                throw new InvalidOperationException("The installed Fixes custom preference contract changed.");
            Type preferences = assembly.GetType("Fixes.Config.Preferences", false);
            FieldInfo field = preferences?.GetField("ExtendedLords", BindingFlags.NonPublic | BindingFlags.Static);
            IDictionary extendedCandidate = null;
            if (field != null)
            {
                if (field.FieldType != typeof(Dictionary<,>).MakeGenericType(typeof(int), entry))
                    throw new InvalidOperationException("The installed Fixes extended preference contract changed.");
                extendedCandidate = field.GetValue(null) as IDictionary ?? throw new InvalidOperationException("Fixes extended dictionary is null.");
            }
            FieldInfo slots = preferences?.GetField("_slots", BindingFlags.NonPublic | BindingFlags.Static);
            if (slots != null && slots.FieldType != entry.MakeArrayType())
                throw new InvalidOperationException("The installed Fixes map preference contract changed.");
            entryType = entry; extended = extendedCandidate; mapSlots = slots; custom = (IDictionary)dictionary;
        }

        internal static int ResolveLordType(string name, string configName = null, string checksum = null)
        {
            var candidates = Enumerable.Range(-1, ConfigSettings.extendedLordPaths.Length + 1)
                .SelectMany(type => CustomisationFileManager.Instance.getLordLordList(type, name) ??
                    new List<CustomisationFileManager.CustomLordConfig>())
                .Where(config => config != null && (configName == null || string.Equals(config.name, configName, StringComparison.OrdinalIgnoreCase)) &&
                    (checksum == null || string.Equals(config.checksum.ToString(), checksum, StringComparison.Ordinal)))
                .Select(config => config.lordType).Distinct().ToArray();
            if (candidates.Length != 1) throw new InvalidDataException("Lord type is missing or ambiguous: " + name);
            return candidates[0];
        }
        internal static int ResolveLordType(LordDataSlot slot) => slot.LordType ?? ResolveLordType(slot.LordName, slot.ConfigName, slot.ConfigChecksum);
        internal static string Identity(LordDataSlot slot)
        {
            int type = ResolveLordType(slot);
            return type >= 0 ? "extended:" + type : "custom:" + slot.LordName;
        }
        private IDictionary Store(int type) => type < 0 ? custom : extended;
        private static object Key(string name, int type) => type < 0 ? (object)name : checked(type + 1);

        internal string Capture(string lordName, int? lordType = null)
        {
            Validate();
            if (!Installed) return null;
            int type = lordType ?? ResolveLordType(lordName);
            IDictionary store = Store(type); object key = Key(lordName, type);
            if (store == null || !store.Contains(key)) return null;
            object entry = store[key];
            if (entry == null || entry.GetType() != entryType) throw new InvalidDataException("Unexpected Fixes entry type.");
            return TypedPreferenceSnapshotCodec.Capture(entry);
        }
        internal string Normalize(string json)
        {
            if (json == null) return null;
            Validate();
            if (!Installed) throw new InvalidOperationException("This snapshot requires Fixes.");
            return TypedPreferenceSnapshotCodec.Capture(TypedPreferenceSnapshotCodec.Restore(entryType, json));
        }
        internal void ValidateSnapshotValue(string json) { Normalize(json); }
        internal bool HasMapOverride(int playerId)
        {
            Validate();
            Array slots = mapSlots?.GetValue(null) as Array;
            return slots != null && playerId >= 1 && playerId < slots.Length && slots.GetValue(playerId) != null;
        }
        internal void Apply(LordDataSnapshot snapshot)
        {
            if (Installed != snapshot.FixesInstalled) throw new InvalidOperationException("Fixes installation differs from the host.");
            if (!Installed) return;
            Validate();
            var desired = snapshot.Slots.GroupBy(Identity, StringComparer.OrdinalIgnoreCase).ToArray();
            var prepared = new Dictionary<string, Tuple<LordDataSlot, int, object>>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in desired)
            {
                LordDataSlot slot = group.First(); int type = ResolveLordType(slot);
                string json = group.Select(item => Normalize(item.FixesJson)).Distinct(StringComparer.Ordinal).Single();
                if (Store(type) == null && json != null) throw new InvalidOperationException("Installed Fixes does not support Extended Lord preferences.");
                prepared.Add(group.Key, Tuple.Create(slot, type, json == null ? null : TypedPreferenceSnapshotCodec.Restore(entryType, json)));
            }
            foreach (string old in touched.Keys.Where(key => !prepared.ContainsKey(key)).ToArray()) RestoreOne(old);
            foreach (var item in prepared)
            {
                var value = item.Value; IDictionary store = Store(value.Item2);
                if (store == null) continue; // Older Fixes has no extended store; absent values require no write.
                object key = Key(value.Item1.LordName, value.Item2);
                if (!touched.ContainsKey(item.Key))
                {
                    if (store.Contains(key)) { originallyPresent.Add(item.Key); originals[item.Key] = store[key]; }
                    touched.Add(item.Key, value.Item2);
                }
                if (value.Item3 == null) store.Remove(key); else store[key] = value.Item3;
            }
        }
        internal void Restore() { foreach (string identity in touched.Keys.ToArray()) RestoreOne(identity); }
        private void RestoreOne(string identity)
        {
            int type = touched[identity]; IDictionary store = Store(type);
            object key = type < 0 ? (object)identity.Substring("custom:".Length) : checked(type + 1);
            if (originallyPresent.Remove(identity)) store[key] = originals[identity]; else store.Remove(key);
            originals.Remove(identity); touched.Remove(identity);
        }
    }
}
