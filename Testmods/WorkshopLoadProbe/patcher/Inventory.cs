using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx;

namespace WorkshopLoadProbe
{
    // P6/T5: every Payload file must match the manifest exactly; extra files fail the package.
    internal sealed class Inventory
    {
        internal bool Ok;
        private int files, hashed, cacheHits, missing, wrongSize, wrongHash, extra, extraDlls;
        private long bytes;
        private long ms;
        private readonly List<string> problems = new List<string>();

        internal static Inventory Verify(string item, Package package, HashCache cache)
        {
            var result = new Inventory();
            var watch = Stopwatch.StartNew();
            string payload = Path.Combine(item, "Payload");
            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in package.Files)
            {
                string full = Path.Combine(payload, file.Path.Replace('/', Path.DirectorySeparatorChar));
                expected.Add(Path.GetFullPath(full));
                result.files++;
                var info = new FileInfo(full);
                if (!info.Exists) { result.missing++; result.Note("missing " + file.Path); continue; }
                if (info.Length != file.Size) { result.wrongSize++; result.Note("size " + file.Path); continue; }
                string sha = cache?.Lookup(info);
                if (sha != null) result.cacheHits++;
                else
                {
                    sha = HashCache.Sha256(info.FullName);
                    result.hashed++;
                    result.bytes += info.Length;
                }
                cache?.Remember(info, sha);
                if (sha != file.Sha) { result.wrongHash++; result.Note("hash " + file.Path); }
            }
            foreach (string root in package.Roots)
                if (!Directory.Exists(Path.Combine(payload, root.Replace('/', Path.DirectorySeparatorChar)))) result.Note("root missing " + root);
            if (Directory.Exists(payload))
                foreach (string file in Directory.GetFiles(payload, "*", SearchOption.AllDirectories))
                    if (!expected.Contains(Path.GetFullPath(file)))
                    {
                        result.extra++;
                        if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) result.extraDlls++;
                        result.Note("extra " + file.Substring(payload.Length + 1));
                    }
            result.ms = watch.ElapsedMilliseconds;
            result.Ok = result.missing == 0 && result.wrongSize == 0 && result.wrongHash == 0 && result.extra == 0 && result.problems.All(p => !p.StartsWith("root", StringComparison.Ordinal));
            return result;
        }

        private void Note(string problem) { if (problems.Count < 10) problems.Add(problem); }

        public override string ToString() =>
            "ok=" + Ok + " files=" + files + " hashed=" + hashed + " hashedBytes=" + bytes + " cacheHits=" + cacheHits + " missing=" + missing +
            " wrongSize=" + wrongSize + " wrongHash=" + wrongHash + " extra=" + extra + " extraDlls=" + extraDlls + " ms=" + ms +
            (problems.Count == 0 ? "" : " first=[" + string.Join("; ", problems.ToArray()) + "]");
    }

    // Path, size and last-write time key a stored SHA-256, as planned in P6. Only BepInEx/cache is written.
    internal sealed class HashCache
    {
        private readonly string path = Path.Combine(Paths.CachePath, "WorkshopLoadProbe.hashes.tsv");
        private readonly Dictionary<string, string[]> stored = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> used = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal static HashCache Load()
        {
            var cache = new HashCache();
            try
            {
                if (File.Exists(cache.path))
                    foreach (string line in File.ReadAllLines(cache.path))
                    {
                        string[] parts = line.Split('\t');
                        if (parts.Length == 4) cache.stored[parts[0]] = parts;
                    }
                ProbeLog.Info("HASH_CACHE", "entries=" + cache.stored.Count + " " + cache.path);
            }
            catch (Exception ex) { ProbeLog.Warning("HASH_CACHE", "unreadable, starting empty: " + ex.Message); cache.stored.Clear(); }
            return cache;
        }

        internal string Lookup(FileInfo info) =>
            stored.TryGetValue(info.FullName, out string[] parts) && parts[1] == info.Length.ToString() &&
            parts[2] == info.LastWriteTimeUtc.Ticks.ToString() ? parts[3] : null;

        internal void Remember(FileInfo info, string sha) =>
            used[info.FullName] = info.FullName + "\t" + info.Length + "\t" + info.LastWriteTimeUtc.Ticks + "\t" + sha;

        internal void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                File.WriteAllText(temp, string.Join("\r\n", used.Values.ToArray()) + "\r\n", new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception ex) { ProbeLog.Warning("HASH_CACHE", "not saved: " + ex.Message); }
        }

        internal static string Sha256(string file)
        {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
