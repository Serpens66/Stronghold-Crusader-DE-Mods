using APIShared.Maps;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace ExtendedData.Core
{
    internal static class TrailMakerEmbeddedMap
    {
        // Called only for an explicit Maker Load. Content-addressed files stay alive while
        // the lobby/restart info refers to them; importing another Trail cannot replace them.
        internal static T Prepare<T>(string trailPath, string cacheRoot, Func<string, string, T> readMap)
            where T : class
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
            return readMap(target, label) ?? throw new InvalidDataException("The cached Trail map could not be read.");
        }
    }
}
