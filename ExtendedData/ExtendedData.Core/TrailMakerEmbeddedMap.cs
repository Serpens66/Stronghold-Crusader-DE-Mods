using APIShared.Maps;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace ExtendedData.Core
{
    internal static class TrailMakerEmbeddedMap
    {
        // Only the latest Maker Load is referenced by the lobby; a few older maps remain for quickly
        // reopening recent Trails. Least recently used entries beyond these limits are removed.
        internal const int MaximumCacheEntries = 16;
        internal const long MaximumCacheBytes = 256L * 1024 * 1024;

        // Called only for an explicit Maker Load. Content-addressed files stay alive while
        // the lobby/restart info refers to them; importing another Trail cannot replace them.
        internal static T Prepare<T>(string trailPath, string cacheRoot, Func<string, string, T> readMap)
            where T : class => Prepare(trailPath, cacheRoot, readMap, MaximumCacheEntries, MaximumCacheBytes);

        internal static T Prepare<T>(string trailPath, string cacheRoot, Func<string, string, T> readMap,
            int maximumEntries, long maximumBytes) where T : class
        {
            if (readMap == null) throw new ArgumentNullException(nameof(readMap));
            byte[] trail;
            using (var input = File.OpenRead(trailPath))
            {
                if (input.Length > TrailContainer.DefaultMaximumContainerBytes)
                    throw new InvalidDataException("The Trail container exceeds its byte limit.");
                trail = new byte[checked((int)input.Length)];
                int offset = 0, count;
                while (offset < trail.Length && (count = input.Read(trail, offset, trail.Length - offset)) > 0)
                    offset += count;
                if (offset != trail.Length) throw new EndOfStreamException("The Trail changed while it was being read.");
            }
            string label = TrailRestartMapReference.Read(TrailContainer.ReadRestartData(trail)).Name;
            byte[] map = TrailContainer.ExtractMap(trail);
            string key;
            using (var hash = SHA256.Create())
                key = BitConverter.ToString(hash.ComputeHash(map), 0, 8).Replace("-", string.Empty);
            Directory.CreateDirectory(cacheRoot);
            string target = Path.Combine(cacheRoot, key + ".map");
            if (File.Exists(target))
            {
                if (!File.ReadAllBytes(target).SequenceEqual(map))
                    throw new InvalidDataException("The embedded-map cache entry has different content.");
                // Reuse marks the entry as recently used for the size limit.
                File.SetLastWriteTimeUtc(target, DateTime.UtcNow);
            }
            else
            {
                // The Vanilla reader selects its map parser from the file extension.
                string staged = Path.Combine(cacheRoot, Path.GetRandomFileName() + ".map");
                bool created = false;
                try
                {
                    using (var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        created = true;
                        output.Write(map, 0, map.Length);
                        output.Flush(true);
                    }
                    if (readMap(staged, label) == null)
                        throw new InvalidDataException("The embedded Trail map could not be read.");
                    Shared.AtomicFileReplacement.Replace(staged, target);
                }
                finally
                {
                    if (created && File.Exists(staged)) File.Delete(staged);
                }
            }
            T result = readMap(target, label) ?? throw new InvalidDataException("The cached Trail map could not be read.");
            Prune(cacheRoot, target, maximumEntries, maximumBytes);
            return result;
        }

        // Keeps the entry just prepared and the most recently used others within both limits. Files the
        // game still holds open are skipped; a failed cleanup never affects the Maker Load.
        private static void Prune(string cacheRoot, string keep, int maximumEntries, long maximumBytes)
        {
            try
            {
                string current = Path.GetFullPath(keep);
                // The current entry always counts first, then the others from newest to oldest use.
                FileInfo[] entries = new DirectoryInfo(cacheRoot).GetFiles("*.map")
                    .OrderByDescending(file => string.Equals(file.FullName, current, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(file => file.LastWriteTimeUtc).ToArray();
                int count = 0;
                long bytes = 0;
                foreach (FileInfo entry in entries)
                {
                    bool isCurrent = string.Equals(entry.FullName, current, StringComparison.OrdinalIgnoreCase);
                    if (isCurrent || count < maximumEntries && bytes + entry.Length <= maximumBytes)
                    {
                        count++;
                        bytes += entry.Length;
                        continue;
                    }
                    try { entry.Delete(); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
