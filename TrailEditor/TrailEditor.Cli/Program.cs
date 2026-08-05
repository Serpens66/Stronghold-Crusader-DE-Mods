using MapParser.Core;
using TrailEditor.Core;

return TrailEditorProgram.Run(args);

internal static class TrailEditorProgram
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length == 0)
                return Usage();
            return args[0].ToLowerInvariant() switch
            {
                "inspect" => Inspect(args),
                "export" => Export(args),
                "build" => Build(args),
                "validate" => Validate(args),
                "export-all" => ExportAll(args),
                "build-all" => BuildAll(args),
                "help" or "--help" or "-h" => Usage(0),
                _ => throw new ArgumentException($"Unknown command '{args[0]}'.")
            };
        }
        catch (Exception ex)
        {
            Log("ERROR", ex.Message, Console.Error);
            return 1;
        }
    }

    private static int Inspect(string[] args)
    {
        RequireCount(args, 2, "inspect <file.trail>");
        TrailContainerDocument container = TrailContainerCodec.ReadTrail(args[1]);
        TrailData data = RestartCodec.Decode(container.RestartData);
        Console.WriteLine($"File: {Path.GetFullPath(args[1])}");
        Console.WriteLine($"SHA-256: {TrailContainerCodec.Sha256(container.Bytes)}");
        Console.WriteLine($"Restart version/bytes: {data.FormatVersion}/{container.RestartData.Length}");
        Console.WriteLine($"Map: {data.Map.FileName} (source {data.Map.SourceKind}, {container.Map.Metadata.WorldSize}x{container.Map.Metadata.WorldSize})");
        Console.WriteLine($"Directory: tag {container.Map.Directory?.DirectoryTag}, capacity {container.Map.Directory?.Capacity}, sections {container.Map.Directory?.SectionCount}");
        Console.WriteLine($"Players/AI slots: {data.Players.Count}/{data.AiSlots.Count}");
        StartingGoldValues gold = SetupSemantics.GetStartingGold(data);
        Console.WriteLine($"Starting gold human/CPU: {gold.Human}/{gold.Computer} (level {data.Setup.StartingGoodsLevel}, fairness {data.Setup.Fairness}, multiplier x{gold.Multiplier})");
        Console.WriteLine($"Hidden flags: customisedExtremeTrail={data.CustomisedExtremeTrail}, customTestMission={data.CustomTestMission}");
        Console.WriteLine($"Trail: '{data.CustomTrailName}', level {data.CustomTrailLevel}, difficulty {data.CustomTrailDifficulty}");
        return 0;
    }

    private static int Export(string[] args)
    {
        if (args.Length is not (2 or 4) || (args.Length == 4 && args[2] != "-o"))
            throw new ArgumentException("Usage: export <file.trail> [-o <bundle-directory>]");
        string source = Path.GetFullPath(args[1]);
        string output = args.Length == 4
            ? Path.GetFullPath(args[3])
            : Path.Combine(Path.GetDirectoryName(source)!, Path.GetFileNameWithoutExtension(source) + ".unpacked");
        new BundleService().Export(source, output);
        Log("INFO", $"Exported '{source}' to '{output}'.");
        return 0;
    }

    private static int Build(string[] args)
    {
        if (args.Length is not (2 or 4) || (args.Length == 4 && args[2] != "-o"))
            throw new ArgumentException("Usage: build <trail.json> [-o <file.trail>]");
        string manifestPath = Path.GetFullPath(args[1]);
        var service = new BundleService();
        TrailManifest manifest = service.ReadManifest(manifestPath);
        string output = args.Length == 4
            ? Path.GetFullPath(args[3])
            : Path.Combine(Path.GetDirectoryName(manifestPath)!, Path.GetFileNameWithoutExtension(manifest.OriginalFileName) + ".edited.trail");
        WriteNew(output, service.Build(manifestPath));
        Log("INFO", $"Built '{output}'.");
        return 0;
    }

    private static int Validate(string[] args)
    {
        RequireCount(args, 2, "validate <file.trail|trail.json>");
        string path = Path.GetFullPath(args[1]);
        if (string.Equals(Path.GetExtension(path), ".trail", StringComparison.OrdinalIgnoreCase))
        {
            TrailContainerDocument container = TrailContainerCodec.ReadTrail(path);
            RestartCodec.Decode(container.RestartData);
        }
        else
        {
            new BundleService().Build(path);
        }
        Log("INFO", $"Validation passed: '{path}'.");
        return 0;
    }

    private static int ExportAll(string[] args)
    {
        RequireCount(args, 3, "export-all <sources-directory> <unpacked-directory>");
        string sources = RequireDirectory(args[1]);
        string unpacked = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(unpacked);
        string[] files = Directory.EnumerateFiles(sources, "*.trail", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        int failures = 0;
        foreach (string file in files)
        {
            try
            {
                string relative = Path.GetRelativePath(sources, file);
                string bundle = Path.Combine(unpacked, Path.GetDirectoryName(relative) ?? string.Empty, Path.GetFileNameWithoutExtension(relative));
                new BundleService().Export(file, bundle);
                Log("INFO", $"Exported '{relative}'.");
            }
            catch (Exception ex)
            {
                failures++;
                Log("ERROR", $"Export failed for '{file}': {ex.Message}", Console.Error);
            }
        }
        Log(failures == 0 ? "INFO" : "ERROR", $"Export summary: {files.Length - failures} succeeded, {failures} failed.", failures == 0 ? Console.Out : Console.Error);
        return failures == 0 ? 0 : 1;
    }

    private static int BuildAll(string[] args)
    {
        RequireCount(args, 3, "build-all <unpacked-directory> <repacked-directory>");
        string unpacked = RequireDirectory(args[1]);
        string repacked = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(repacked);
        string[] manifests = Directory.EnumerateFiles(unpacked, "trail.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        int failures = 0;
        foreach (string manifestPath in manifests)
        {
            try
            {
                var service = new BundleService();
                TrailManifest manifest = service.ReadManifest(manifestPath);
                string bundleDirectory = Path.GetDirectoryName(manifestPath)!;
                string relativeBundle = Path.GetRelativePath(unpacked, bundleDirectory);
                string relativeParent = Path.GetDirectoryName(relativeBundle) ?? string.Empty;
                string output = Path.Combine(repacked, relativeParent, manifest.OriginalFileName);
                WriteNew(output, service.Build(manifestPath));
                Log("INFO", $"Built '{Path.GetRelativePath(repacked, output)}'.");
            }
            catch (Exception ex)
            {
                failures++;
                Log("ERROR", $"Build failed for '{manifestPath}': {ex.Message}", Console.Error);
            }
        }
        Log(failures == 0 ? "INFO" : "ERROR", $"Build summary: {manifests.Length - failures} succeeded, {failures} failed.", failures == 0 ? Console.Out : Console.Error);
        return failures == 0 ? 0 : 1;
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
    }

    private static string RequireDirectory(string path)
    {
        string full = Path.GetFullPath(path);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"Directory not found: {full}");
        return full;
    }

    private static void RequireCount(string[] args, int count, string usage)
    {
        if (args.Length != count)
            throw new ArgumentException("Usage: " + usage);
    }

    private static int Usage(int exitCode = 2)
    {
        Console.WriteLine("Stronghold Crusader Definitive Edition .trail editor");
        Console.WriteLine("  TrailEditor inspect <file.trail>");
        Console.WriteLine("  TrailEditor export <file.trail> [-o <bundle-directory>]");
        Console.WriteLine("  TrailEditor build <trail.json> [-o <file.trail>]");
        Console.WriteLine("  TrailEditor validate <file.trail|trail.json>");
        Console.WriteLine("  TrailEditor export-all <sources-directory> <unpacked-directory>");
        Console.WriteLine("  TrailEditor build-all <unpacked-directory> <repacked-directory>");
        return exitCode;
    }

    private static void Log(string level, string message, TextWriter? writer = null)
    {
        (writer ?? Console.Out).WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}");
    }
}
