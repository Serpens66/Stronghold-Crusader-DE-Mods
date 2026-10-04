#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MessagePack;

namespace Shared
{
    /// <summary>Optional configuration endpoints, independent of their mod's UI registration.</summary>
    public static class ModSettingsApplication
    {
        private static readonly Dictionary<string, PresetLobbyModSettingsViewModel> endpoints = new Dictionary<string, PresetLobbyModSettingsViewModel>(StringComparer.Ordinal);
        private static string journalPath, contextId = "";
        private static Dictionary<string, Dictionary<string, byte[]>> resume;
        private static string resumeContext;
        private static bool consumed;
        private static Dictionary<string, string> personalFingerprints;
        private static Dictionary<string, Dictionary<string, string>> preparedSources;
        public static string ContextId => contextId;
        public static IReadOnlyDictionary<string, PresetLobbyModSettingsViewModel> Endpoints => new Dictionary<string, PresetLobbyModSettingsViewModel>(endpoints);

        internal static void Register(string id, PresetLobbyModSettingsViewModel endpoint, string assemblyPath)
        {
            if (endpoints.TryGetValue(id, out var previous) && !ReferenceEquals(previous, endpoint))
                throw new InvalidOperationException("Duplicate configuration endpoint: " + id);
            endpoints[id] = endpoint;
            if (journalPath == null)
#if API_SHARED_PRESET_TESTS
                journalPath = Path.Combine(Path.GetDirectoryName(assemblyPath), "RestartPreparation.json");
#else
                journalPath = Path.Combine(BepInEx.Paths.ConfigPath, "APIShared", "RestartPreparation.json");
#endif
        }

        public static PropertyInfo[] GetHostProperties(object endpoint)
        {
            if (endpoint is PresetLobbyModSettingsViewModel model && model.System_HasDynamicSettings)
                return model.System_GetPresetSettingDescriptors().Where(x => x.Scope == PresetSettingScope.Host)
                    .Select(x => (PropertyInfo)new DescribedProperty(model, x)).ToArray();
            return endpoint.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanRead && x.CanWrite && x.GetCustomAttributes(true).Any(a => a.GetType().Name == "SyncHostOnlyAttribute") &&
                    !x.GetCustomAttributes(true).Any(a => a.GetType().Name == "DoNotPersistAttribute")).ToArray();
        }

        public static bool HasRestartPreparation => resume != null || (journalPath != null && File.Exists(journalPath));
        public static bool HasApplicationEndpoints => endpoints.Values.Any(x => x.System_HasApplicationBackend);

        // A context is selected before any mission values are materialized. Its identity includes
        // the source content hash; stale preparations are never silently applied to new content.
        public static void EnterContext(string identity)
        {
            if (string.IsNullOrWhiteSpace(identity)) throw new ArgumentException("Missing settings context identity.");
            ReadJournal();
            if (resume != null && !consumed && resumeContext != identity &&
                resumeContext.Substring(0, resumeContext.LastIndexOf('|') + 1) == identity.Substring(0, identity.LastIndexOf('|') + 1))
                throw new InvalidDataException("Mission settings changed after restart preparation. Discard the preparation before retrying.");
            if (resume != null && !consumed && resumeContext == identity)
                foreach (var entry in personalFingerprints)
                {
                    if (!endpoints.TryGetValue(entry.Key, out var endpoint))
                        throw new InvalidDataException("Required settings provider is missing: " + entry.Key);
                    if (endpoint.System_OwnConfigurationFingerprint() != entry.Value)
                        throw new InvalidDataException("Personal settings changed after restart preparation: " + entry.Key);
                }
            contextId = identity;
        }

        public static Dictionary<string, byte[]> ResumeSnapshot(string id)
        {
            if (resume == null || consumed || resumeContext != contextId) return null;
            if (!resume.TryGetValue(id, out var snapshot)) return null;
            return Clone(snapshot);
        }

        public static void RestorePreparedSources(string id)
        {
            if (resume == null || consumed || resumeContext != contextId || preparedSources == null) return;
            if (endpoints.TryGetValue(id, out var endpoint) && preparedSources.TryGetValue(id, out var sources))
                endpoint.RestoreRestartSources(sources);
        }

        public static bool Commit(string id)
        {
            if (!endpoints.TryGetValue(id, out var endpoint)) return false;
            byte[] previous = journalPath != null && File.Exists(journalPath) ? File.ReadAllBytes(journalPath) : null;
            var previousSources = preparedSources; var previousResume = resume; var previousFingerprints = personalFingerprints; string previousContext = resumeContext;
            bool written = contextId.Length != 0 && endpoint.System_ConfigurationNeedsRestart();
            if (written) SavePreparation(); // Durable intent precedes package publication.
            try { return endpoint.System_ApplyConfiguration(contextId, true); }
            catch
            {
                if (written)
                {
                    if (previous == null) File.Delete(journalPath);
                    else PublishJournal(previous);
                    preparedSources = previousSources; resume = previousResume; personalFingerprints = previousFingerprints; resumeContext = previousContext;
                }
                throw;
            }
        }

        /// <summary>Returns false until all required startup configurations are actually loaded.</summary>
        public static bool PrepareLaunch()
        {
            ReadJournal();
            if (resume != null && !consumed && resumeContext == contextId)
                foreach (string id in resume.Keys)
                    if (!endpoints.ContainsKey(id)) throw new InvalidDataException("Required settings provider is missing: " + id);
            byte[] previous = journalPath != null && File.Exists(journalPath) ? File.ReadAllBytes(journalPath) : null;
            var previousSources = preparedSources; var previousResume = resume;
            var previousPersonal = personalFingerprints;
            string previousContext = resumeContext;
            bool written = contextId.Length != 0 && endpoints.Values.Any(x => x.System_ConfigurationNeedsRestart());
            if (written) SavePreparation();
            try
            {
                bool ready = true;
                foreach (var item in endpoints)
                {
                    if (!item.Value.System_HasApplicationBackend) continue;
                    bool restart = item.Value.System_ApplyConfiguration(contextId);
                    item.Value.ReportConfigurationResult(restart);
                    if (restart) ready = false;
                }
                return ready;
            }
            catch
            {
                if (written)
                {
                    if (previous == null) File.Delete(journalPath); else PublishJournal(previous);
                    preparedSources = previousSources; resume = previousResume; personalFingerprints = previousPersonal; resumeContext = previousContext;
                }
                throw;
            }
        }

        public static void ConfirmStarted()
        {
            // Keep active settings and the identity available for an in-session mission restart.
            consumed = true;
            if (journalPath != null && File.Exists(journalPath)) File.Delete(journalPath);
            resume = null;
        }

        public static void ExitContext()
        {
            bool preparingRestart = !consumed && resume != null && resumeContext == contextId;
            contextId = "";
            if (!preparingRestart)
                foreach (var endpoint in endpoints.Values)
                    endpoint.System_ReturnToOwnConfiguration();
            // An unconsumed preparation survives a process exit or menu re-entry.
            if (consumed) { resume = null; resumeContext = null; }
            consumed = false;
        }

        public static void DiscardPreparation()
        {
            foreach (var endpoint in endpoints.Values) endpoint.DiscardApplicationPackage();
            if (journalPath != null && File.Exists(journalPath)) File.Delete(journalPath);
            resume = null; resumeContext = null; consumed = false;
            foreach (var endpoint in endpoints.Values) endpoint.RefreshConfigurationBindings();
        }

#if API_SHARED_PRESET_TESTS
        internal static void ResetForTests(string path)
        {
            endpoints.Clear(); journalPath = path; resume = null; resumeContext = null;
            contextId = ""; consumed = false;
        }
#endif

        private static Dictionary<string, byte[]> Clone(Dictionary<string, byte[]> source) =>
            source.ToDictionary(x => x.Key, x => (byte[])x.Value.Clone(), StringComparer.Ordinal);

        private static void SavePreparation()
        {
            if (journalPath == null) throw new InvalidOperationException("Restart storage is not initialized.");
            var fingerprints = endpoints.Where(x => x.Value.System_HasApplicationBackend)
                .ToDictionary(x => x.Key, x => x.Value.System_OwnConfigurationFingerprint(), StringComparer.Ordinal);
            var sources = endpoints.ToDictionary(x => x.Key, x => x.Value.CaptureRestartSources(), StringComparer.Ordinal);
            var mods = new Dictionary<string, object>(StringComparer.Ordinal);
            var snapshots = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);
            foreach (var item in endpoints)
            {
                var snapshot = item.Value.CaptureApplicationSnapshot();
                if (snapshot.Count > 16384) throw new InvalidDataException("Too many settings.");
                snapshots[item.Key] = snapshot;
                mods[item.Key] = snapshot.ToDictionary(x => x.Key, x => (object)Convert.ToBase64String(x.Value), StringComparer.Ordinal);
            }
            string payload = DependencyFreeJson.Serialize(new Dictionary<string, object> { ["schema"] = 1, ["context"] = contextId, ["mods"] = mods, ["sources"] = sources.ToDictionary(x => x.Key, x => (object)x.Value), ["personal"] = fingerprints.ToDictionary(x => x.Key, x => (object)x.Value) });
            string json = DependencyFreeJson.Serialize(new Dictionary<string, object> { ["payload"] = payload, ["sha256"] = Digest(payload) });
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length > 8 * 1024 * 1024) throw new InvalidDataException("Restart preparation exceeds 8 MiB.");
            PublishJournal(bytes);
            preparedSources = sources; resume = snapshots; personalFingerprints = fingerprints; resumeContext = contextId; consumed = false;
        }

        private static string Digest(string text)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }

        private static void PublishJournal(byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(journalPath));
            string temporary = journalPath + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(journalPath)) File.Replace(temporary, journalPath, null); else File.Move(temporary, journalPath);
        }

        private static void ReadJournal()
        {
            if (resume != null || journalPath == null || !File.Exists(journalPath)) return;
            using (var stream = new FileStream(journalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > 8 * 1024 * 1024) throw new InvalidDataException("Restart preparation exceeds 8 MiB.");
                using (var reader = new StreamReader(stream))
                {
                    var envelope = DependencyFreeJson.Parse(reader.ReadToEnd()) as Dictionary<string, object>;
                    if (envelope == null || !envelope.TryGetValue("payload", out var payload) || !(payload is string content) ||
                        !envelope.TryGetValue("sha256", out var hash) || !(hash is string checksum) || Digest(content) != checksum)
                        throw new InvalidDataException("Restart preparation failed its integrity check.");
                    var root = DependencyFreeJson.Parse(content) as Dictionary<string, object>;
                    if (root == null || !root.TryGetValue("schema", out var schema) || Convert.ToInt32(schema) != 1 ||
                        !root.TryGetValue("context", out var context) || !(context is string identity) || string.IsNullOrWhiteSpace(identity) ||
                        !root.TryGetValue("mods", out var rawMods) || !(rawMods is Dictionary<string, object> mods))
                        throw new InvalidDataException("Invalid restart preparation.");
                    var parsed = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);
                    foreach (var mod in mods)
                    {
                        if (!(mod.Value is Dictionary<string, object> values) || values.Count > 16384)
                            throw new InvalidDataException("Invalid restart snapshot.");
                        parsed[mod.Key] = values.ToDictionary(x => x.Key, x => Convert.FromBase64String((string)x.Value), StringComparer.Ordinal);
                    }
                    if (!root.TryGetValue("personal", out var personal) || !(personal is Dictionary<string, object> personalMap))
                        throw new InvalidDataException("Restart preparation has no personal configuration provenance.");
                    personalFingerprints = personalMap.ToDictionary(x => x.Key, x => (string)x.Value, StringComparer.Ordinal);
                    if (!root.TryGetValue("sources", out var rawSources) || !(rawSources is Dictionary<string, object> sourceMap))
                        throw new InvalidDataException("Restart preparation has no source selection.");
                    preparedSources = sourceMap.ToDictionary(x => x.Key,
                        x => ((Dictionary<string, object>)x.Value).ToDictionary(v => v.Key, v => (string)v.Value), StringComparer.Ordinal);
                    resume = parsed; resumeContext = identity;
                }
            }
        }

        // Adapter for existing schema consumers. Reads are sourced from the provider's snapshot;
        // mutation is intentionally only possible through the complete endpoint snapshot API.
        private sealed class DescribedProperty : PropertyInfo
        {
            private readonly PresetLobbyModSettingsViewModel owner;
            private readonly PresetSettingDescriptor descriptor;
            internal DescribedProperty(PresetLobbyModSettingsViewModel owner, PresetSettingDescriptor descriptor) { this.owner = owner; this.descriptor = descriptor; }
            public override string Name => descriptor.PropertyName;
            public override Type PropertyType => descriptor.PropertyType;
            public override Type DeclaringType => owner.GetType();
            public override Type ReflectedType => owner.GetType();
            public override PropertyAttributes Attributes => PropertyAttributes.None;
            public override bool CanRead => true;
            public override bool CanWrite => true;
            public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture) =>
                owner.System_ReadDescribedValue(Name);
            public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture) => throw new InvalidOperationException("Apply a complete configuration snapshot.");
            public override MethodInfo[] GetAccessors(bool nonPublic) => Array.Empty<MethodInfo>();
            public override MethodInfo GetGetMethod(bool nonPublic) => null;
            public override MethodInfo GetSetMethod(bool nonPublic) => null;
            public override ParameterInfo[] GetIndexParameters() => Array.Empty<ParameterInfo>();
            public override object[] GetCustomAttributes(bool inherit) => Array.Empty<object>();
            public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<object>();
            public override bool IsDefined(Type attributeType, bool inherit) => attributeType == typeof(RequiresRestartAttribute) && descriptor.RequiresRestart;
        }
    }
}
