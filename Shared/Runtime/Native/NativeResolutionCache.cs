using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Shared
{
    // Native address and layout resolution with one-time fallback cost.
    // - Known reference hash: confirm the audited reference value only (byte comparison, no scan).
    // - Unknown hash: confirm a value cached for exactly this native SHA-256 and mod version; only
    //   without a confirmed cache entry run the expensive fallback once and remember its result.
    // The cache never authorizes a value by itself: every use re-runs the cheap confirmation.
    // Read and write failures only cause a new fallback, never a feature failure.
    internal sealed class NativeResolutionCache
    {
        private const string Header = "SERP-NATIVE-CACHE-1";
        private readonly object sync = new object();
        private readonly Dictionary<string, int> values = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly string path, nativeHash, modVersion;
        private bool dirty;

        private NativeResolutionCache(string path, string nativeHash, string modVersion, bool referenceHashMatches)
        {
            this.path = path;
            this.nativeHash = nativeHash ?? string.Empty;
            this.modVersion = modVersion ?? string.Empty;
            ReferenceHashMatches = referenceHashMatches;
        }

        /// <summary>True when the loaded image is the audited reference build.</summary>
        public bool ReferenceHashMatches { get; }

        /// <summary>Opens the owner's cache. A known reference hash needs no file; nothing is read or written then.</summary>
        public static NativeResolutionCache Open(string ownerGuid, string modVersion, string nativeHash,
            bool referenceHashMatches, string directory = null)
        {
            if (string.IsNullOrWhiteSpace(ownerGuid)) throw new ArgumentException("Owner GUID required.", nameof(ownerGuid));
            string file = null;
            if (!referenceHashMatches && !string.IsNullOrEmpty(nativeHash))
            {
                try
                {
                    string root = directory ?? BepInEx.Paths.CachePath;
                    if (!string.IsNullOrEmpty(root)) file = Path.Combine(root, ownerGuid + ".native-cache.tsv");
                }
                catch (Exception) { file = null; }
            }
            var cache = new NativeResolutionCache(file, nativeHash, modVersion, referenceHashMatches);
            cache.Load();
            return cache;
        }

        public bool TryGet(string id, out int value)
        {
            lock (sync) return values.TryGetValue(id, out value);
        }

        public void Store(string id, int value)
        {
            if (path == null) return;
            lock (sync)
            {
                if (values.TryGetValue(id, out int existing) && existing == value) return;
                values[id] = value;
                dirty = true;
            }
        }

        /// <summary>Resolves a code target: reference RVA, then confirmed cache entry, then unique pattern scan.</summary>
        public NativeResolution ResolveUnique(ReadOnlySpan<byte> memory, string pattern, int referenceRva, string name,
            ManualLogSource log = null, NativePatternSearchScope searchScope = NativePatternSearchScope.ExecutableSections)
        {
            int rva;
            string method;
            if (ReferenceHashMatches && NativePatternResolver.MatchesPatternAt(memory, referenceRva, pattern))
            {
                rva = referenceRva;
                method = "reference-rva";
            }
            else if (TryGet(name, out int cached) && NativePatternResolver.MatchesPatternAt(memory, cached, pattern))
            {
                rva = cached;
                method = "cache";
            }
            else
            {
                rva = NativePatternResolver.FindUniquePattern(memory, pattern, name, searchScope);
                method = ReferenceHashMatches ? "signature-fallback-after-rva-validation-failure" : "signature-fallback";
                Store(name, rva);
            }
            DebugLogHelper.LogInfo(log, $"Native address resolved: name={name}, method={method}, rva=0x{rva:X}.");
            return new NativeResolution(rva, method);
        }

        /// <summary>
        /// Resolves a derived value (data address, field offset, stride). <paramref name="confirm"/> must be cheap
        /// and prove the value again, e.g. by re-decoding its anchor instruction. <paramref name="derive"/> runs
        /// only without a confirmed reference or cache value; it must validate its own second evidence and throw
        /// when the value cannot be established.
        /// </summary>
        public NativeResolution ResolveValue(string name, int referenceValue, Func<int, bool> confirm, Func<int> derive,
            ManualLogSource log = null)
        {
            if (confirm == null) throw new ArgumentNullException(nameof(confirm));
            if (derive == null) throw new ArgumentNullException(nameof(derive));
            int value;
            string method;
            if (ReferenceHashMatches && confirm(referenceValue))
            {
                value = referenceValue;
                method = "reference-value";
            }
            else if (TryGet(name, out int cached) && confirm(cached))
            {
                value = cached;
                method = "cache";
            }
            else
            {
                value = derive();
                if (!confirm(value)) throw new InvalidOperationException(name + " derived value 0x" + value.ToString("X") + " failed its confirmation.");
                method = ReferenceHashMatches ? "derived-after-reference-validation-failure" : "derived";
                Store(name, value);
            }
            DebugLogHelper.LogInfo(log, $"Native value resolved: name={name}, method={method}, value=0x{value:X}.");
            return new NativeResolution(value, method);
        }

        /// <summary>Writes new results atomically. Returns false when nothing was written; never throws.</summary>
        public bool Save()
        {
            string text;
            lock (sync)
            {
                if (!dirty || path == null) return false;
                var builder = new StringBuilder();
                builder.Append(Header).Append('\t').Append(nativeHash).Append('\t').Append(modVersion).Append("\r\n");
                foreach (KeyValuePair<string, int> entry in values)
                    builder.Append(entry.Key).Append('\t').Append(entry.Value.ToString("X", CultureInfo.InvariantCulture)).Append("\r\n");
                text = builder.ToString();
                dirty = false;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, text, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception)
            {
                // The cache only saves time; the next start resolves again.
                return false;
            }
        }

        private void Load()
        {
            if (path == null) return;
            try
            {
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path);
                if (lines.Length == 0) return;
                string[] header = lines[0].Split('\t');
                // Another native build or mod version invalidates every entry.
                if (header.Length != 3 || header[0] != Header || header[1] != nativeHash || header[2] != modVersion) return;
                for (int index = 1; index < lines.Length; index++)
                {
                    string[] cells = lines[index].Split('\t');
                    if (cells.Length == 2 && cells[0].Length > 0 &&
                        int.TryParse(cells[1], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int value))
                        values[cells[0]] = value;
                }
            }
            catch (Exception) { values.Clear(); }
        }
    }
}
