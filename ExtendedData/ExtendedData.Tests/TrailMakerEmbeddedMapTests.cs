using APIShared.Maps;
using ExtendedData.Core;
using System.Text;

internal static class TrailMakerEmbeddedMapTests
{
    private sealed record Map(string Path, string Label, byte[] Bytes);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new Exception("Expected an invalid embedded map to fail closed.");
    }

    internal static void Run()
    {
        string root = TestWorkDirectory.New("maker-map");
        string cache = Path.Combine(root, "cache");
        try
        {
            Map Read(string path, string label)
            {
                // Required by the actual Vanilla parser, including temporary validation.
                Check(Path.GetExtension(path) == ".map", "Vanilla would route this file to its campaign parser.");
                return new(path, label, File.ReadAllBytes(path));
            }
            byte[] source = Container("Missing Workshop map", 42);
            string trail = Path.Combine(root, "mission.trail");
            File.WriteAllBytes(trail, source);
            var first = TrailMakerEmbeddedMap.Prepare(trail, cache, Read);
            Check(first.Label == "Missing Workshop map", "Original display identity lost.");
            Check(first.Bytes.SequenceEqual(TrailContainer.ExtractMap(source)) && !TrailContainer.ReadLayout(first.Bytes).HasRestartData,
                "Embedded map changed or still contains restart metadata.");
            Check(File.ReadAllBytes(trail).SequenceEqual(source), "Source Trail was changed.");
            var repeated = TrailMakerEmbeddedMap.Prepare(trail, cache, Read);
            Check(repeated.Path == first.Path && Directory.GetFiles(cache).Length == 1, "Repeated load replaced or leaked a cache entry.");
            byte[] secondSource = Container("Second map", 43);
            File.WriteAllBytes(trail, secondSource);
            var second = TrailMakerEmbeddedMap.Prepare(trail, cache, Read);
            Check(second.Path != first.Path && File.ReadAllBytes(first.Path).SequenceEqual(first.Bytes),
                "A later mission replaced a map still referenced by the lobby.");
            File.WriteAllBytes(trail, Container("Different mission, same map", 42));
            var renamed = TrailMakerEmbeddedMap.Prepare(trail, cache, Read);
            Check(renamed.Path == first.Path && renamed.Label == "Different mission, same map",
                "Reusing identical map bytes retained another mission's display label.");
            File.WriteAllBytes(first.Path, new byte[] { 1 });
            File.WriteAllBytes(trail, source);
            Reject(() => TrailMakerEmbeddedMap.Prepare(trail, cache, Read));
            string invalidCache = Path.Combine(root, "invalid");
            Reject(() => TrailMakerEmbeddedMap.Prepare<Map>(trail, invalidCache, (_, _) => null));
            Check(Directory.GetFiles(invalidCache).Length == 0, "Failed validation published a map or leaked its temporary file.");
            File.WriteAllBytes(trail, new byte[] { 1, 2 });
            Reject(() => TrailMakerEmbeddedMap.Prepare(trail, invalidCache, Read));

            // Bounded cache: the newest Maker Load and the most recently used others remain.
            string bounded = Path.Combine(root, "bounded");
            var paths = new List<string>();
            for (byte mapByte = 50; mapByte < 55; mapByte++)
            {
                File.WriteAllBytes(trail, Container("Map " + mapByte, mapByte));
                paths.Add(TrailMakerEmbeddedMap.Prepare(trail, bounded, Read, 2, long.MaxValue).Path);
                File.SetLastWriteTimeUtc(paths[^1], DateTime.UtcNow.AddMinutes(paths.Count));
            }
            Check(Directory.GetFiles(bounded).Length == 2 && File.Exists(paths[4]) && File.Exists(paths[3]),
                "The entry limit removed the current map or kept older entries.");
            File.WriteAllBytes(trail, Container("Map 60", 60));
            var single = TrailMakerEmbeddedMap.Prepare(trail, bounded, Read, 16, 1);
            Check(Directory.GetFiles(bounded).Length == 1 && File.Exists(single.Path),
                "The byte limit must keep only the current map when everything else exceeds it.");
        }
        finally { if (TestWorkDirectory.IsOwned(root)) Directory.Delete(root, true); }
    }

    private static byte[] Container(string label, byte mapByte)
    {
        using var restartStream = new MemoryStream();
        using (var writer = new BinaryWriter(restartStream, Encoding.UTF8, true))
        {
            writer.Write((byte)60); writer.Write(0); writer.Write((byte)2);
            byte[] name = Encoding.UTF8.GetBytes(label); writer.Write(name.Length); writer.Write(name);
        }
        byte[] restart = restartStream.ToArray();
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, Encoding.UTF8, true))
        {
            writer.Write(0xFFFFFFFEu);
            for (int block = 0; block < 6; block++) { writer.Write(1); writer.Write((byte)0); }
            writer.Write(restart.Length); writer.Write(restart); writer.Write(0);
            writer.Write(2036); writer.Write(mapByte);
        }
        return output.ToArray();
    }
}
