using MapParser.Core;

namespace MapParser.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length < 2)
            {
                PrintUsage();
                return 1;
            }

            return args[0].ToLowerInvariant() switch
            {
                "info" => RunInfo(args[1]),
                "list" => RunList(args[1]),
                "dump" => RunDump(args),
                "validate" => RunValidate(args[1]),
                _ => UnknownCommand(args[0])
            };
        }
        catch (Exception ex) when (ex is MapParseException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 2;
        }
    }

    private static int RunInfo(string path)
    {
        MapDocument map = MapFileReader.Parse(path);
        Console.WriteLine($"File: {map.SourceName}");
        Console.WriteLine($"Format: {map.FormatKind}");
        Console.WriteLine($"Magic: 0x{map.Metadata.Magic:X8}");
        Console.WriteLine($"Map type: {map.Metadata.MapType}");
        Console.WriteLine($"Maximum players: {map.Metadata.MaxPlayers}");
        Console.WriteLine($"Mission type: {map.Metadata.ScenarioMissionType}");
        Console.WriteLine($"Mission lock: {map.Metadata.MissionLockType}");
        Console.WriteLine($"Standalone name: {map.Metadata.StandaloneFileName}");
        Console.WriteLine($"Skirmish: {map.Metadata.IsSkirmishMap}");
        Console.WriteLine($"Balanced: {map.Metadata.IsBalancedMap}");
        Console.WriteLine($"World size: {map.Metadata.WorldSize}");
        Console.WriteLine($"Keep locations: {string.Join(", ", map.Metadata.KeepLocations)}");
        if (map.Directory == null)
        {
            Console.WriteLine("Sections: unavailable (recognized special map)");
            Console.WriteLine($"Opaque tail: offset={map.OpaqueTailOffset}, size={map.OpaqueTailLength}");
        }
        else
        {
            Console.WriteLine(
                $"Directory: tag={map.Directory.DirectoryTag}, capacity={map.Directory.Capacity}, " +
                $"version={map.Directory.FormatVersion}, sections={map.Directory.SectionCount}, " +
                $"payload={map.Directory.PayloadSize}");
            Console.WriteLine($"Placement layers: {map.HasPlacementLayers}");
        }
        return 0;
    }

    private static int RunList(string path)
    {
        MapDocument map = MapFileReader.Parse(path);
        if (!map.SectionsAvailable)
        {
            Console.WriteLine("SectionsUnavailable");
            return 0;
        }

        Console.WriteLine("INDEX  ID    LOGICAL  STORAGE    RAW-BYTES  DECODED-BYTES  OFFSET    NAME");
        foreach (MapSectionInfo section in map.Sections)
        {
            Console.WriteLine(
                $"{section.Index,5}  {section.SectionId,4}  {section.LogicalSectionId,7}  " +
                $"{section.StorageKind,-9}  {section.StoredSize,9}  {section.UncompressedSize,13}  " +
                $"{section.AbsoluteOffset,8}  {MapSectionCatalog.GetName(section.LogicalSectionId)}");
        }
        return 0;
    }

    private static int RunDump(string[] args)
    {
        if (args.Length is < 3 or > 4)
            throw new ArgumentException("Usage: MapParser dump <file.map> <section-id> [output.bin]");
        if (!int.TryParse(args[2], out int sectionId))
            throw new ArgumentException($"Invalid section ID '{args[2]}'.");

        MapDocument map = MapFileReader.Parse(args[1]);
        MapSectionInfo section = map.TryGetSection(sectionId, out MapSectionInfo? exact)
            ? exact
            : map.GetLogicalSection(sectionId);
        byte[] content = section.ReadContent();
        string output = args.Length == 4 ? args[3] : $"section-{section.SectionId}.bin";
        File.WriteAllBytes(output, content);
        Console.WriteLine($"Wrote {content.Length} bytes from section {section.SectionId} to {Path.GetFullPath(output)}");
        return 0;
    }

    private static int RunValidate(string path)
    {
        string[] files = File.Exists(path)
            ? new[] { Path.GetFullPath(path) }
            : Directory.Exists(path)
                ? Directory.EnumerateFiles(path, "*.map", SearchOption.AllDirectories).OrderBy(value => value).ToArray()
                : throw new FileNotFoundException("Map file or directory was not found.", path);

        int regular = 0;
        int special = 0;
        int failed = 0;
        int unavailableSections = 0;
        foreach (string file in files)
        {
            try
            {
                MapDocument map = MapFileReader.Parse(file);
                if (!map.SectionsAvailable)
                {
                    special++;
                    Console.WriteLine($"SPECIAL SectionsUnavailable {file}");
                    continue;
                }

                // Force every lazy section so validate also checks DCL output lengths and CRC32.
                int unavailableInMap = 0;
                foreach (MapSectionInfo section in map.Sections)
                {
                    if (section.IsContentAvailable)
                        section.ReadContent();
                    else
                        unavailableInMap++;
                }
                unavailableSections += unavailableInMap;
                regular++;
                Console.WriteLine(
                    $"OK {map.Sections.Count,3} sections unavailable={unavailableInMap} {file}");
            }
            catch (Exception ex) when (ex is MapParseException or IOException or UnauthorizedAccessException)
            {
                failed++;
                Console.Error.WriteLine($"FAIL {file}: {ex.Message}");
            }
        }

        Console.WriteLine(
            $"Summary: files={files.Length}, regular={regular}, special={special}, " +
            $"unavailable-sections={unavailableSections}, failed={failed}");
        return failed == 0 ? 0 : 2;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Read-only Stronghold Crusader Definitive Edition .map parser");
        Console.WriteLine("  MapParser info <file.map>");
        Console.WriteLine("  MapParser list <file.map>");
        Console.WriteLine("  MapParser dump <file.map> <section-id> [output.bin]");
        Console.WriteLine("  MapParser validate <file.map-or-directory>");
    }
}
