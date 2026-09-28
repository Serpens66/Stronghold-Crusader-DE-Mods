using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MapParser.Core;
using Serilog;
using SHCDESE.AICDecoder;
using SHCDESE.AIVDecoder.Models;
using SHCDESE.AIVDecoder.Services;
using TrailEditor.Core;

return SaveExtractorProgram.Run(args);

internal static class SaveExtractorProgram
{
    private const int VillageSectionId = 1107;
    private const int VillageCount = 9;
    private const int VillageSize = 0x6D98;
    private const int OwnerOffset = 0;
    private const int RotationOffset = 8;
    private const int VariantOffset = 12;
    private const int SelectionStateOffset = 16;

    private sealed record SelectedCpu(
        int PlayerId,
        int VillageSlot,
        int VariantIndex,
        int Rotation,
        int SelectionState,
        TrailAiSlot AiSlot,
        IReadOnlyList<SaveData> AivData,
        string? LordFile);

    public static int Run(string[] args)
    {
        if (args.Length != 3 || !string.Equals(args[0], "extract", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Usage: SaveExtractor extract <file.sav> <new-output-directory>");
            return 1;
        }

        try
        {
            Extract(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 2;
        }
    }

    private static void Extract(string sourcePath, string outputDirectory)
    {
        if (!string.Equals(Path.GetExtension(sourcePath), ".sav", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The input must be a .sav file.");
        if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
            throw new IOException($"Output already exists: {outputDirectory}");

        byte[] saveBytes = File.ReadAllBytes(sourcePath);
        MapDocument save = MapFileReader.Parse(saveBytes);
        MapPreambleInfo preamble = save.Preamble;
        if (preamble.RestartSizeFieldOffset < 0 || preamble.RestartInfoSize == 0 ||
            preamble.RestartTerminatorOffset < 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(saveBytes.AsSpan(preamble.RestartTerminatorOffset, 4)) != 0)
            throw new InvalidDataException("Save has no valid skirmish restart block.");

        byte[] restartBytes = saveBytes.AsSpan(preamble.RestartPayloadOffset, checked((int)preamble.RestartInfoSize)).ToArray();
        TrailData restart = RestartCodec.Decode(restartBytes);
        if (restart.Players.Count != 8 || restart.AiSlots.Count != 8)
            throw new InvalidDataException("Expected eight player and AI slots in the restart block.");

        byte[] villageBytes = save.GetLogicalSection(VillageSectionId).ReadContent();
        if (villageBytes.Length != VillageCount * VillageSize)
            throw new InvalidDataException($"Unexpected native AIV village section size: {villageBytes.Length}.");

        ILogger logger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();
        var decoder = new AIVDecoder(logger);
        var encoder = new AIVEncoder(logger);
        var selected = new List<SelectedCpu>();
        var matchedOwners = new HashSet<int>();
        for (int villageSlot = 1; villageSlot < VillageCount; villageSlot++)
        {
            int offset = villageSlot * VillageSize;
            int owner = BinaryPrimitives.ReadInt32LittleEndian(villageBytes.AsSpan(offset + OwnerOffset, 4));
            if (owner == 0)
                continue;
            if (owner is < 1 or > 8 || !matchedOwners.Add(owner))
                throw new InvalidDataException($"Invalid or duplicate AIV village owner {owner}.");
            if (restart.Players[owner - 1].LordType is -9999 or -1)
                throw new InvalidDataException($"Village owner {owner} is not an active CPU in the restart block.");

            int rotation = BinaryPrimitives.ReadInt32LittleEndian(villageBytes.AsSpan(offset + RotationOffset, 4));
            int variant = BinaryPrimitives.ReadInt32LittleEndian(villageBytes.AsSpan(offset + VariantOffset, 4));
            int state = BinaryPrimitives.ReadInt32LittleEndian(villageBytes.AsSpan(offset + SelectionStateOffset, 4));
            TrailAiSlot ai = restart.AiSlots[owner - 1];
            if (state is not (1 or 2) || rotation is not (0 or 2 or 4 or 6) ||
                variant < 0 || variant >= ai.Aivs.Count)
                throw new InvalidDataException($"CPU {owner} has no valid finalized AIV selection.");

            var decodedAivs = new List<SaveData>(ai.Aivs.Count);
            foreach (CustomAivData candidate in ai.Aivs)
            {
                SaveData decoded = decoder.Decode(candidate.Data);
                if (!encoder.Encode(decoded).SequenceEqual(candidate.Data))
                    throw new InvalidDataException($"AIV '{candidate.Name}' for CPU {owner} does not round-trip to its saved data.");
                decodedAivs.Add(decoded);
            }

            selected.Add(new SelectedCpu(owner, villageSlot, variant, rotation, state, ai, decodedAivs,
                ai.LordConfig == null ? null : $"slot-{owner}.lordjson"));
        }

        int[] cpuIds = Enumerable.Range(1, 8)
            .Where(id => restart.Players[id - 1].LordType is not (-9999 or -1))
            .ToArray();
        if (selected.Count == 0 || !cpuIds.Order().SequenceEqual(matchedOwners.Order()))
            throw new InvalidDataException("Active CPUs and saved AIV village owners do not match.");

        byte[] mapBytes = ExtractMap(saveBytes, save);
        string saveHash = Convert.ToHexString(SHA256.HashData(saveBytes));
        string mapHash = Convert.ToHexString(SHA256.HashData(mapBytes));

        Directory.CreateDirectory(outputDirectory);
        WriteNewBytes(Path.Combine(outputDirectory, "map.map"), mapBytes);
        foreach (SelectedCpu cpu in selected)
        {
            for (int index = 0; index < cpu.AivData.Count; index++)
            {
                string aivPath = Path.Combine(outputDirectory, AivFileName(cpu.PlayerId, index));
                BundleService.WriteJson(aivPath, cpu.AivData[index]);
                SaveData parsedAiv = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(aivPath, Encoding.UTF8))
                    ?? throw new InvalidDataException($"Could not read generated AIV JSON: {aivPath}");
                if (!encoder.Encode(parsedAiv).SequenceEqual(cpu.AiSlot.Aivs[index].Data))
                    throw new InvalidDataException($"Generated AIV JSON differs from saved CPU {cpu.PlayerId} candidate {index}.");
            }

            if (cpu.LordFile != null)
            {
                PublicAIC publicAic = PublicAIC.FromInternal(cpu.AiSlot.LordConfig!.Config);
                string lordPath = Path.Combine(outputDirectory, cpu.LordFile);
                BundleService.WriteJson(lordPath, new { lord = publicAic });
                using JsonDocument parsedLord = JsonDocument.Parse(File.ReadAllText(lordPath, Encoding.UTF8));
                if (!parsedLord.RootElement.TryGetProperty("lord", out _))
                    throw new InvalidDataException($"Generated Lord JSON has no lord root: {lordPath}");
            }
        }

        string mapping = BuildMapping(sourcePath, saveHash, mapHash, restart, selected);
        WriteNewText(Path.Combine(outputDirectory, "mapping.md"), mapping);
        int totalAivs = selected.Sum(cpu => cpu.AivData.Count);
        Console.WriteLine($"Extracted map and {totalAivs} AIVs for {selected.Count} CPUs to {outputDirectory}");
        foreach (SelectedCpu cpu in selected)
            Console.WriteLine($"CPU {cpu.PlayerId}: {cpu.AiSlot.LordConfig?.Name ?? "embedded vanilla Lord"}; " +
                              $"AIV {cpu.VariantIndex + 1} ({cpu.AiSlot.Aivs[cpu.VariantIndex].Name})");
    }

    private static byte[] ExtractMap(byte[] saveBytes, MapDocument save)
    {
        MapPreambleInfo p = save.Preamble;
        using var stream = new MemoryStream(checked(saveBytes.Length - (int)p.RestartInfoSize - 4));
        stream.Write(saveBytes, 0, p.RestartSizeFieldOffset);
        stream.Write(new byte[4]);
        stream.Write(saveBytes, p.DirectoryTagOffset, saveBytes.Length - p.DirectoryTagOffset);
        byte[] mapBytes = stream.ToArray();
        MapDocument parsed = MapFileReader.Parse(mapBytes);
        if (parsed.Preamble.RestartInfoSize != 0 || parsed.Sections.Count != save.Sections.Count ||
            !saveBytes.AsSpan(p.DirectoryTagOffset).SequenceEqual(mapBytes.AsSpan(parsed.Preamble.DirectoryTagOffset)))
            throw new InvalidDataException("Extracted map did not preserve the saved map sections.");
        return mapBytes;
    }

    private static string BuildMapping(string sourcePath, string saveHash, string mapHash,
        TrailData restart, List<SelectedCpu> selected)
    {
        var lines = new List<string>
        {
            "# Save extraction",
            "",
            $"Source save: `{sourcePath}`",
            $"Source SHA-256: `{saveHash}`",
            $"Map in save: `{restart.Map.FileName}`",
            $"Extracted map: `map.map` (SHA-256 `{mapHash}`)",
            "",
            "The map contains the state at the time of the save. AIV indices are zero-based in the native save state.",
            "",
            "| CPU player ID | Native village slot | Lord configuration | Lord file | Active AIV | Saved AIV index | Selection state | Rotation | AIV file |",
            "| --- | --- | --- | --- | --- | --- | --- | --- | --- |"
        };
        foreach (SelectedCpu cpu in selected.OrderBy(value => value.PlayerId))
        {
            string lordName = cpu.AiSlot.LordConfig?.Name ?? "embedded vanilla Lord";
            string aivName = cpu.AiSlot.Aivs[cpu.VariantIndex].Name;
            lines.Add($"| {cpu.PlayerId} | {cpu.VillageSlot} | {lordName} | {cpu.LordFile ?? "—"} | " +
                      $"{aivName} | {cpu.VariantIndex} | {cpu.SelectionState} | {cpu.Rotation} | {AivFileName(cpu.PlayerId, cpu.VariantIndex)} |");
        }
        lines.Add("");
        lines.Add("## All saved AIV candidates");
        lines.Add("");
        lines.Add("| CPU player ID | Candidate number | Saved index | AIV name | Used in this save | AIV file |");
        lines.Add("| --- | --- | --- | --- | --- | --- |");
        foreach (SelectedCpu cpu in selected.OrderBy(value => value.PlayerId))
        {
            for (int index = 0; index < cpu.AiSlot.Aivs.Count; index++)
            {
                string used = index == cpu.VariantIndex ? "**YES**" : "No";
                lines.Add($"| {cpu.PlayerId} | {index + 1} | {index} | {cpu.AiSlot.Aivs[index].Name} | " +
                          $"{used} | {AivFileName(cpu.PlayerId, index)} |");
            }
        }
        return string.Join("\r\n", lines) + "\r\n";
    }

    private static string AivFileName(int playerId, int index) => $"slot-{playerId}-aiv-{index + 1:D2}.aivjson";

    private static void WriteNewBytes(string path, byte[] bytes)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(bytes);
    }

    private static void WriteNewText(string path, string content)
    {
        if (content.Replace("\r\n", "", StringComparison.Ordinal).Contains('\n'))
            throw new InvalidDataException("Mapping text contains bare LF line endings.");
        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
            writer.Write(content);
        if (!string.Equals(content, File.ReadAllText(path, Encoding.UTF8), StringComparison.Ordinal))
            throw new IOException($"Text verification failed: {path}");
    }
}
