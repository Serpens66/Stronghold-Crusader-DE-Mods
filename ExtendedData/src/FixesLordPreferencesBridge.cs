using BepInEx.Bootstrap;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ExtendedData
{
    // Fixes owns these values. Reflection keeps Fixes optional and checks its actual runtime contract.
    internal sealed class FixesLordPreferencesBridge
    {
        private readonly Dictionary<string, object> originals = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly HashSet<string> originallyPresent = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> touched = new HashSet<string>(StringComparer.Ordinal);
        private IDictionary dictionary;
        private Type entryType;

        internal bool Installed => Chainloader.PluginInfos.ContainsKey("fixes");

        internal void Validate()
        {
            if (!Installed)
                return;
            if (dictionary != null)
                return;
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item =>
                item.GetType("Fixes.Boostrap.Plugin", false) != null);
            Type pluginType = assembly?.GetType("Fixes.Boostrap.Plugin", false);
            entryType = assembly?.GetType("Fixes.Config.CustomLordPreferencesEntry", false);
            object instance = pluginType?.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            dictionary = pluginType?.GetProperty("CustomLordPreferences", BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance) as IDictionary;
            if (entryType == null || dictionary == null)
                throw new InvalidOperationException("The installed Fixes plugin does not expose its Lord preferences dictionary.");
        }

        internal string Capture(string lordName)
        {
            Validate();
            if (!Installed || !dictionary.Contains(lordName))
                return null;
            object entry = dictionary[lordName];
            if (entry == null || entry.GetType() != entryType)
                throw new InvalidDataException("The Fixes Lord preference entry has an unexpected type.");
            return TypedPreferenceSnapshotCodec.Capture(entry);
        }

        internal void ValidateSnapshotValue(string json)
        {
            if (json == null) return;
            Validate();
            if (!Installed)
                throw new InvalidOperationException("A Trail requires Fixes Lord preferences, but Fixes is not installed.");
            TypedPreferenceSnapshotCodec.Restore(entryType, json);
        }

        internal void Apply(LordDataSnapshot snapshot)
        {
            if (Installed != snapshot.FixesInstalled)
                throw new InvalidOperationException("Fixes installation differs from the host.");
            if (!Installed)
                return;
            Validate();
            var desired = snapshot.Slots.GroupBy(item => item.LordName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(item => item.FixesJson).Distinct(StringComparer.Ordinal).Single(), StringComparer.Ordinal);
            var prepared = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> item in desired)
            {
                if (item.Value == null)
                {
                    prepared.Add(item.Key, null);
                    continue;
                }
                prepared.Add(item.Key, TypedPreferenceSnapshotCodec.Restore(entryType, item.Value));
            }
            foreach (string oldName in touched.Where(name => !desired.ContainsKey(name)).ToArray())
                RestoreOne(oldName);
            foreach (KeyValuePair<string, object> item in prepared)
            {
                if (!touched.Contains(item.Key))
                {
                    if (dictionary.Contains(item.Key))
                    {
                        originallyPresent.Add(item.Key);
                        originals[item.Key] = dictionary[item.Key];
                    }
                    touched.Add(item.Key);
                }
                if (item.Value == null)
                {
                    dictionary.Remove(item.Key);
                    continue;
                }
                dictionary[item.Key] = item.Value;
            }
        }

        internal void Restore()
        {
            foreach (string lordName in touched.ToArray())
                RestoreOne(lordName);
        }

        private void RestoreOne(string lordName)
        {
            if (originallyPresent.Remove(lordName))
                dictionary[lordName] = originals[lordName];
            else
                dictionary.Remove(lordName);
            originals.Remove(lordName);
            touched.Remove(lordName);
        }
    }
}
