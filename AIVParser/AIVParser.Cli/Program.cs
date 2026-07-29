using AIVParser.Core;

namespace AIVParser.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 ||
                IsHelp(args[0]))
            {
                PrintUsage();
                return args.Length == 0 ? 2 : 0;
            }

            return args[0].ToLowerInvariant() switch
            {
                "validate" => RunValidate(args),
                "inspect" => RunInspect(args),
                _ => UsageError($"Unknown command '{args[0]}'.")
            };
        }
        catch (Exception ex)
        {
            CliLog.Error($"Unexpected failure: {ex}");
            return 2;
        }
    }

    private static int RunValidate(string[] args)
    {
        if (args.Length != 2)
        {
            return UsageError("validate expects exactly one file or directory path.");
        }

        string inputPath = Path.GetFullPath(args[1]);
        IReadOnlyList<string> files;
        if (File.Exists(inputPath))
        {
            files = new[] { inputPath };
        }
        else if (Directory.Exists(inputPath))
        {
            files = Directory
                .EnumerateFiles(inputPath, "*.aivjson", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        else
        {
            CliLog.Error($"Input path does not exist: {inputPath}");
            return 2;
        }

        if (files.Count == 0)
        {
            CliLog.Error($"No *.aivjson files found below: {inputPath}");
            return 2;
        }

        int validFiles = 0;
        int invalidFiles = 0;
        int frameCount = 0;
        int miscCount = 0;
        int warningCount = 0;
        int errorCount = 0;

        var parser = new AivBlueprintParser();
        foreach (string file in files)
        {
            AivJsonLoadResult loaded = AivJsonFileLoader.Load(file);
            AivParseResult parsed = parser.Parse(
                loaded.Document,
                file,
                loaded.Diagnostics);

            frameCount += parsed.Blueprint.Frames.Count;
            miscCount += parsed.Blueprint.MiscItems.Count;
            warningCount += parsed.WarningCount;
            errorCount += parsed.ErrorCount;

            if (parsed.IsValid)
            {
                validFiles++;
                CliLog.Info(
                    $"OK {file} | frames={parsed.Blueprint.Frames.Count}, " +
                    $"misc={parsed.Blueprint.MiscItems.Count}, warnings={parsed.WarningCount}");
            }
            else
            {
                invalidFiles++;
                CliLog.Error(
                    $"INVALID {file} | errors={parsed.ErrorCount}, warnings={parsed.WarningCount}");
            }

            PrintDiagnostics(parsed.Diagnostics);
        }

        CliLog.Info(
            $"Summary: files={files.Count}, valid={validFiles}, invalid={invalidFiles}, " +
            $"frames={frameCount}, misc={miscCount}, warnings={warningCount}, errors={errorCount}");
        return invalidFiles == 0 ? 0 : 1;
    }

    private static int RunInspect(string[] args)
    {
        if (args.Length < 2)
        {
            return UsageError("inspect expects one AIV JSON file.");
        }

        string inputPath = Path.GetFullPath(args[1]);
        string outputDirectory = Path.Combine(
            Environment.CurrentDirectory,
            "AIVParser-output");
        AivRotation rotation = AivRotation.Degrees0;

        for (int index = 2; index < args.Length; index++)
        {
            string option = args[index];
            if (option is "-o" or "--output")
            {
                if (++index >= args.Length)
                {
                    return UsageError($"{option} requires a directory path.");
                }

                outputDirectory = Path.GetFullPath(args[index]);
            }
            else if (option == "--rotation")
            {
                if (++index >= args.Length ||
                    !TryParseRotation(args[index], out rotation))
                {
                    return UsageError("--rotation must be one of 0, 90, 180, or 270.");
                }
            }
            else
            {
                return UsageError($"Unknown inspect option '{option}'.");
            }
        }

        if (!File.Exists(inputPath))
        {
            CliLog.Error($"Input file does not exist: {inputPath}");
            return 2;
        }

        if (!string.Equals(
                Path.GetExtension(inputPath),
                ".aivjson",
                StringComparison.OrdinalIgnoreCase))
        {
            CliLog.Error("V1 only supports files with the .aivjson extension.");
            return 2;
        }

        AivJsonLoadResult loaded = AivJsonFileLoader.Load(inputPath);
        AivParseResult parsed = new AivBlueprintParser().Parse(
            loaded.Document,
            inputPath,
            loaded.Diagnostics);
        PrintDiagnostics(parsed.Diagnostics);
        if (!parsed.IsValid)
        {
            CliLog.Error(
                $"Inspection aborted: errors={parsed.ErrorCount}, warnings={parsed.WarningCount}.");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);
        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string jsonPath = Path.Combine(outputDirectory, baseName + ".parsed.json");
        string svgPath = Path.Combine(outputDirectory, baseName + ".svg");

        ParsedJsonExporter.Write(jsonPath, parsed, rotation);
        SvgExporter.Write(svgPath, parsed, rotation);

        CliLog.Info(
            $"Parsed {inputPath} | frames={parsed.Blueprint.Frames.Count}, " +
            $"misc={parsed.Blueprint.MiscItems.Count}, warnings={parsed.WarningCount}");
        CliLog.Info($"Wrote semantic JSON: {jsonPath}");
        CliLog.Info($"Wrote SVG preview: {svgPath}");
        return 0;
    }

    private static void PrintDiagnostics(IEnumerable<AivDiagnostic> diagnostics)
    {
        foreach (AivDiagnostic diagnostic in diagnostics)
        {
            string line =
                $"{diagnostic.Code} {diagnostic.Location}: {diagnostic.Message}";
            if (diagnostic.Severity == AivDiagnosticSeverity.Error)
            {
                CliLog.Error(line);
            }
            else
            {
                CliLog.Warning(line);
            }
        }
    }

    private static bool TryParseRotation(string value, out AivRotation rotation)
    {
        switch (value)
        {
            case "0":
                rotation = AivRotation.Degrees0;
                return true;
            case "90":
                rotation = AivRotation.Degrees90;
                return true;
            case "180":
                rotation = AivRotation.Degrees180;
                return true;
            case "270":
                rotation = AivRotation.Degrees270;
                return true;
            default:
                rotation = AivRotation.Degrees0;
                return false;
        }
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "help";
    }

    private static int UsageError(string message)
    {
        CliLog.Error(message);
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Stronghold Crusader DE AIV Parser

            Usage:
              AIVParser validate <file-or-directory>
              AIVParser inspect <file.aivjson> [-o <directory>] [--rotation 0|90|180|270]

            validate recursively checks every *.aivjson below a directory and writes no files.
            inspect writes <name>.parsed.json and <name>.svg. Its default output directory is
            ./AIVParser-output.
            """);
    }
}

internal static class CliLog
{
    public static void Info(string message)
    {
        Write("INFO", message, Console.Out);
    }

    public static void Warning(string message)
    {
        Write("WARN", message, Console.Out);
    }

    public static void Error(string message)
    {
        Write("ERROR", message, Console.Error);
    }

    private static void Write(string level, string message, TextWriter writer)
    {
        writer.WriteLine(
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}");
    }
}
