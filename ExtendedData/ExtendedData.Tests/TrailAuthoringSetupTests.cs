using System.Text;
using APIShared.Maps;
using ExtendedData.Core;

internal static class TrailAuthoringSetupTests
{
    private const string AuthoredSetup = "-12,1,5,2,3,4,5,123,25,1,Schloss,";
    private const string NeutralSetup = "-12,1,40,2,3,4,5,0,25,1,Schloss,";

    internal static void Run()
    {
        foreach (byte restartVersion in new byte[] { 51, 60 })
        {
            // A larger authored autosave interval shrinks both nested length fields.
            byte[] original = Container(restartVersion, AuthoredSetup);
            byte[] pristine = original.ToArray();
            byte[] result = TrailAuthoringSetup.Neutralize(original, 40);
            if (!result.SequenceEqual(Container(restartVersion, NeutralSetup)) || !original.SequenceEqual(pristine))
                throw new Exception("The neutralized Trail changed bytes outside the protected fields and their lengths.");
            if (!TrailContainer.ExtractMap(result).SequenceEqual(TrailContainer.ExtractMap(original)))
                throw new Exception("The embedded map changed while neutralizing the restart setup.");
            if (!TrailAuthoringSetup.Neutralize(result, 40).SequenceEqual(result))
                throw new Exception("Exporting an already neutral Trail changed it.");
        }

        try
        {
            TrailAuthoringSetup.Neutralize(Container(60, "-12,1,90"), 40);
            throw new Exception("Accepted a truncated restart setup.");
        }
        catch (InvalidDataException) { }

        // Vanilla also accepts the oldest unversioned setup string, which starts at fairness.
        byte[] unversioned = Container(51, "1,70,2,3,4,5,20,25,1,0");
        if (!TrailAuthoringSetup.Neutralize(unversioned, 40)
            .SequenceEqual(Container(51, "1,40,2,3,4,5,0,25,1,0")))
            throw new Exception("An unversioned Trail lost starting goods or peacetime instead of its protected fields.");

        TestBundledCoopAssets();
        TestExportPublicationRollback();
    }

    private static void TestBundledCoopAssets()
    {
        DirectoryInfo fixture = Directory.CreateTempSubdirectory("ExtendedDataCoopAssets-");
        string originalPath = Path.Combine(fixture.FullName, "Trail_Mission_1.trail");
        string bundledTrail = Path.Combine(fixture.FullName, "mission.trail");
        string bundledMap = Path.Combine(fixture.FullName, "map.map");
        try
        {
            byte[] original = Container(60, AuthoredSetup);
            byte[] neutral = Container(60, NeutralSetup);
            File.WriteAllBytes(originalPath, original);

            byte[] restart = TrailAuthoringSetup.StageCoopMissionAssets(originalPath, bundledTrail, bundledMap, 40);

            if (!File.ReadAllBytes(bundledTrail).SequenceEqual(neutral) ||
                !File.ReadAllBytes(bundledMap).SequenceEqual(TrailContainer.ExtractMap(original)) ||
                !restart.SequenceEqual(TrailContainer.ReadRestartData(neutral)) ||
                !File.ReadAllBytes(originalPath).SequenceEqual(original))
                throw new Exception("Coop mission assets did not preserve the map and neutralize the bundled Trail copy.");
        }
        finally
        {
            File.Delete(bundledMap);
            File.Delete(bundledTrail);
            File.Delete(originalPath);
            fixture.Delete();
        }
    }

    private static void TestExportPublicationRollback()
    {
        DirectoryInfo destination = Directory.CreateTempSubdirectory("ExtendedDataTrailRollback-");
        DirectoryInfo staging = destination.CreateSubdirectory("staged");
        var names = new[] { "Trail_Mission_1.trail", "Trail_Mission_2.trail" };
        string obsolete = Path.Combine(destination.FullName, "Trail_Mission_3.trail");
        try
        {
            foreach (string name in names)
            {
                File.WriteAllText(Path.Combine(destination.FullName, name), "previous " + name);
                File.WriteAllText(Path.Combine(staging.FullName, name), "new " + name);
            }
            File.WriteAllText(obsolete, "obsolete");

            string secondStaged = Directory.GetFiles(staging.FullName, "*.trail")[1];
            using (FileStream locked = File.Open(secondStaged, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try
                {
                    TrailAuthoringSetup.PublishExportCopies(staging.FullName, destination.FullName);
                    throw new Exception("Export publication replaced a locked staged file.");
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            }

            foreach (string name in names)
            {
                if (File.ReadAllText(Path.Combine(destination.FullName, name)) != "previous " + name ||
                    File.ReadAllText(Path.Combine(staging.FullName, name)) != "new " + name)
                    throw new Exception("Failed publication did not restore the previous export and staged files.");
            }
            if (File.ReadAllText(obsolete) != "obsolete")
                throw new Exception("Failed publication did not restore an obsolete file.");

            TrailAuthoringSetup.PublishExportCopies(staging.FullName, destination.FullName);
            if (File.Exists(obsolete) || names.Any(name =>
                File.ReadAllText(Path.Combine(destination.FullName, name)) != "new " + name))
                throw new Exception("Retry did not replace the entire previous export.");
        }
        finally
        {
            // All paths are inside the isolated fixture; preserve unexpected recovery data on a failed test.
            if (!Directory.Exists(Path.Combine(staging.FullName, "previous")))
            {
                foreach (string name in names)
                {
                    File.Delete(Path.Combine(staging.FullName, name));
                    File.Delete(Path.Combine(destination.FullName, name));
                }
                File.Delete(obsolete);
                staging.Delete();
                destination.Delete();
            }
        }
    }

    private static byte[] Container(byte restartVersion, string setup)
    {
        using var restart = new MemoryStream();
        using var container = new MemoryStream();
        using var restartWriter = new BinaryWriter(restart);
        using var writer = new BinaryWriter(container);
        byte[] mapName = Encoding.UTF8.GetBytes("Burgenstraße.map");
        byte[] setupBytes = Encoding.UTF8.GetBytes(setup);
        restartWriter.Write(restartVersion);
        restartWriter.Write(1); // One member, with a lord, team and colour.
        restartWriter.Write(4);
        restartWriter.Write(1);
        restartWriter.Write(2);
        restartWriter.Write((byte)2); // Workshop map.
        restartWriter.Write(mapName.Length);
        restartWriter.Write(mapName);
        restartWriter.Write(setupBytes.Length);
        restartWriter.Write(setupBytes);
        restartWriter.Write(new byte[] { 1, 0, 42, 0, 0, 0, 0xFE });

        writer.Write(0xFFFFFFFEu);
        for (int block = 0; block < 5; block++) writer.Write(0);
        writer.Write(4);
        writer.Write(17);
        writer.Write(checked((int)restart.Length));
        writer.Write(restart.ToArray());
        writer.Write(0);
        writer.Write(3036);
        writer.Write(new byte[] { 0xAB, 0x12, 0xFF, 0x41, 0x10 });
        return container.ToArray();
    }
}
