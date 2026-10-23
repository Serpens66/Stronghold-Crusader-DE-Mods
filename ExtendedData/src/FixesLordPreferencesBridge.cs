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
        private sealed class StoredValue
        {
            internal IDictionary Store;
            internal object Key, Value;
            internal bool Present;
            internal void Write() { if (Present) Store[Key] = Value; else Store.Remove(Key); }
        }
        private readonly Dictionary<string, StoredValue> touched = new Dictionary<string, StoredValue>(StringComparer.OrdinalIgnoreCase);
        private IDictionary custom;
        private IDictionary extended;
        private Type entryType;
        private FieldInfo mapSlots;
        private bool inheritedPreferences;
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
            MethodInfo fallback = preferences?.GetMethod("TryGetBaseLordPreferences", BindingFlags.NonPublic | BindingFlags.Static);
            if (fallback != null && (fallback.ReturnType != typeof(bool) ||
                !fallback.GetParameters().Select(parameter => parameter.ParameterType)
                    .SequenceEqual(new[] { typeof(int), entry.MakeByRefType() })))
                throw new InvalidOperationException("The installed Fixes inheritance contract changed.");
            inheritedPreferences = fallback != null;
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
            => Capture(lordName, lordType, false);

        private string Capture(string lordName, int? lordType, bool original)
        {
            Validate();
            if (!Installed) return null;
            int type = lordType ?? ResolveLordType(lordName);
            IDictionary store = Store(type); object key = Key(lordName, type);
            var source = new FixesPreferenceSource { CustomName = type < 0 ? lordName : null,
                LordType = type < 0 ? null : (int?)type };
            object entry;
            if (original && touched.TryGetValue(FixesPreferenceSources.Identity(source), out StoredValue saved))
            {
                if (!saved.Present) return null;
                entry = saved.Value;
            }
            else
            {
                if (store == null || !store.Contains(key)) return null;
                entry = store[key];
            }
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

        internal IReadOnlyList<FixesPreferenceSource> CaptureSources(string name, int type, int baseLord)
            => CaptureSources(name, type, baseLord, null);

        private IReadOnlyList<FixesPreferenceSource> CaptureSources(string name, int type, int baseLord,
            IDictionary<string, FixesPreferenceSource> recorded)
        {
            Validate();
            if (!Installed) return null;
            if (!inheritedPreferences) return Array.Empty<FixesPreferenceSource>();
            Func<FixesPreferenceSource, FixesPreferenceSource> read = source =>
            {
                string id = FixesPreferenceSources.Identity(source);
                source.Json = recorded != null && recorded.TryGetValue(id, out FixesPreferenceSource saved)
                    ? saved.Json : Capture(source.CustomName, source.LordType ?? -1, recorded != null);
                return source;
            };
            if (type < 0 && read(new FixesPreferenceSource { CustomName = name }).Json != null)
                return Array.Empty<FixesPreferenceSource>();
            var result = new List<FixesPreferenceSource>();
            // Fixes GetBaseLord accepts only the native base enums. Do not clamp modder AIC values.
            if (baseLord > (int)SHCDESE.Interop.AILords.SK_NULL && baseLord < (int)SHCDESE.Interop.AILords.SK_X1 &&
                Enum.IsDefined(typeof(SHCDESE.Interop.AILords), (uint)baseLord))
            {
                string baseName = ((SHCDESE.Interop.AILords)(uint)baseLord).ToString().Substring(3);
                FixesPreferenceSource customSource = read(new FixesPreferenceSource { CustomName = baseName });
                result.Add(customSource);
                if (customSource.Json == null)
                    result.Add(read(new FixesPreferenceSource { LordType = baseLord - 1 }));
            }
            return result;
        }

        internal static CustomisationFileManager.CustomLordConfig ResolveConfiguration(LordDataSlot slot)
        {
            int type = ResolveLordType(slot);
            var matches = (CustomisationFileManager.Instance.getLordLordList(type, slot.LordName) ??
                new List<CustomisationFileManager.CustomLordConfig>()).Where(config => config != null &&
                    config.lordType == type && string.Equals(config.name, slot.ConfigName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(config.checksum.ToString(), slot.ConfigChecksum, StringComparison.Ordinal))
                .GroupBy(config => config.path, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Missing or ambiguous saved AIC configuration: " + slot.LordName);
            return matches[0];
        }

        // The caller must be the authenticated host/local author. Parsing already verified the old digest.
        internal LordDataSnapshot CompleteSources(LordDataSnapshot snapshot, Func<LordDataSlot, int> resolveBase = null)
        {
            if (!snapshot.FixesInstalled || snapshot.Slots.All(slot => slot.FixesSources != null)) return snapshot;
            Validate();
            if (!Installed) throw new InvalidDataException("Fixes is required to migrate inherited preferences.");
            var recorded = Collect(snapshot);
            LordDataSlot[] slots = snapshot.Slots.Select(slot => new LordDataSlot
            {
                PlayerId = slot.PlayerId, LordType = ResolveLordType(slot), LordName = slot.LordName,
                ConfigName = slot.ConfigName, ConfigChecksum = slot.ConfigChecksum, ModLordJson = slot.ModLordJson,
                FixesJson = slot.FixesJson,
                FixesSources = slot.FixesSources ?? CaptureSources(slot.LordName, ResolveLordType(slot),
                    resolveBase != null ? resolveBase(slot) : ResolveConfiguration(slot).lordData.opponent_type, recorded),
            }).ToArray();
            var result = LordDataSnapshot.Create(snapshot.SessionId, snapshot.FixesInstalled, slots);
            Collect(result); // Detect shared-source conflicts before publishing any mutation.
            return result;
        }

        internal IReadOnlyList<FixesPreferenceSource> CaptureRecordedSources(IReadOnlyList<FixesPreferenceSource> sources)
        {
            if (sources == null) return null;
            return sources.Select(source => new FixesPreferenceSource { CustomName = source.CustomName,
                LordType = source.LordType, Json = Capture(source.CustomName, source.LordType ?? -1) }).ToArray();
        }

        internal IReadOnlyList<FixesPreferenceSource> NormalizeSources(IReadOnlyList<FixesPreferenceSource> sources)
            => sources?.Select(source => new FixesPreferenceSource { CustomName = source.CustomName,
                LordType = source.LordType, Json = Normalize(source.Json) }).ToArray();

        private Dictionary<string, FixesPreferenceSource> Collect(LordDataSnapshot snapshot)
        {
            var result = new Dictionary<string, FixesPreferenceSource>(StringComparer.OrdinalIgnoreCase);
            foreach (LordDataSlot slot in snapshot.Slots)
            {
                int type = ResolveLordType(slot);
                AddSource(result, new FixesPreferenceSource { CustomName = type < 0 ? slot.LordName : null,
                    LordType = type < 0 ? null : (int?)type, Json = slot.FixesJson });
                foreach (FixesPreferenceSource source in slot.FixesSources ?? Array.Empty<FixesPreferenceSource>())
                    AddSource(result, source);
            }
            return result;
        }

        private void AddSource(IDictionary<string, FixesPreferenceSource> values, FixesPreferenceSource source)
        {
            string id = FixesPreferenceSources.Identity(source);
            string json = Normalize(source.Json);
            if (values.TryGetValue(id, out FixesPreferenceSource existing) && existing.Json != json)
                throw new InvalidDataException("Conflicting shared Fixes preference source: " + id);
            values[id] = new FixesPreferenceSource { CustomName = source.CustomName, LordType = source.LordType, Json = json };
        }
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
            var prepared = new Dictionary<string, StoredValue>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in Collect(snapshot))
            {
                FixesPreferenceSource source = pair.Value;
                IDictionary store = Store(source.LordType ?? -1);
                if (store == null && source.Json != null) throw new InvalidOperationException("Installed Fixes does not support Extended Lord preferences.");
                if (store != null) prepared.Add(pair.Key, new StoredValue { Store = store,
                    Key = Key(source.CustomName, source.LordType ?? -1), Present = source.Json != null,
                    Value = source.Json == null ? null : TypedPreferenceSnapshotCodec.Restore(entryType, source.Json) });
            }
            var previous = new Dictionary<string, StoredValue>(touched, StringComparer.OrdinalIgnoreCase);
            var rollback = touched.Concat(prepared).GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => Read(group.First().Value)).ToArray();
            try
            {
                foreach (string old in touched.Keys.Where(key => !prepared.ContainsKey(key)).ToArray()) RestoreOne(old);
                foreach (var pair in prepared)
                {
                    if (!touched.ContainsKey(pair.Key)) touched.Add(pair.Key, Read(pair.Value));
                    pair.Value.Write();
                }
            }
            catch
            {
                foreach (StoredValue value in rollback) value.Write();
                touched.Clear(); foreach (var pair in previous) touched.Add(pair.Key, pair.Value);
                throw;
            }
        }
        private static StoredValue Read(StoredValue target) => new StoredValue { Store = target.Store, Key = target.Key,
            Present = target.Store.Contains(target.Key), Value = target.Store.Contains(target.Key) ? target.Store[target.Key] : null };
        internal void Restore() { foreach (string identity in touched.Keys.ToArray()) RestoreOne(identity); }
        private void RestoreOne(string identity)
        {
            touched[identity].Write(); touched.Remove(identity);
        }
    }
}
