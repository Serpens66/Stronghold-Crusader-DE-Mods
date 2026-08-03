using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AIVParser.Core;
using AIVPlacement.Core;
using MapParser.Core;

namespace AIVPlacement.OracleComparison;

internal static class Program
{
    private static readonly Regex OracleAttemptPattern = new(
        @"AIV placement oracle attempt #(?<sequence>\d+)\.(?<attempt>\d+): " +
        @"mapName=(?<mapName>.*?), mapFile=(?<mapPath>.*?), " +
        @"mapFileSha256=(?<mapSha256>[0-9a-fA-F]{64}), playerId=(?<playerId>\d+), " +
        @"method=(?<method>.*?), candidateId=(?<candidateId>-?\d+), " +
        @"aivName=(?<aivName>.*?), aivJson=(?<aivPath>.*?), " +
        @"aivJsonSha256=(?<aivSha256>[0-9a-fA-F]{64}), " +
        @"orientation=(?<orientation>\d+) \([^)]*\), " +
        @"result=(?<result>Complete|Partial|Rejected), " +
        @"rawFitScore=(?<rawFitScore>-?\d+), fitPercent=(?<fitPercentage>\d+), " +
        @"evaluatedCells=(?<evaluatedCells>\d+), blockedCells=(?<blockedCells>\d+), " +
        @"origin=\(-?\d+,-?\d+\), keepReference=\((?<keepX>-?\d+),(?<keepY>-?\d+)\)\.",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions InputOptions = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions OutputOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && string.Equals(args[0], "import-log", StringComparison.Ordinal))
                return ImportOracleLog(args);

            if (!TryParseArguments(args, out Options options))
                return 2;

            OracleCorpus corpus = ReadJson<OracleCorpus>(options.ManifestPath);
            ValidateCorpus(corpus);
            IReadOnlyList<OracleCase> selectedCases = SelectCases(corpus, options);
            ComparisonReport report = RunCorpus(corpus, selectedCases);

            if (options.OutputPath is not null)
            {
                string outputPath = Path.GetFullPath(options.OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                WriteTextCrlf(outputPath, JsonSerializer.Serialize(report, OutputOptions));
                Log("INFO", $"Wrote comparison report: {outputPath}");
            }

            PrintSummary(report);
            return report.FailedCaseCount == 0 && report.MismatchCount == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Log("ERROR", ex.ToString());
            return 2;
        }
    }

    private static ComparisonReport RunCorpus(
        OracleCorpus corpus,
        IReadOnlyList<OracleCase> cases)
    {
        var total = Stopwatch.StartNew();
        string mapPath = Path.GetFullPath(corpus.Map.Path);
        VerifyFile(mapPath, corpus.Map.Sha256, "map");

        var parse = Stopwatch.StartNew();
        MapDocument document = MapFileReader.Parse(mapPath);
        MapKeepAnchors anchors = MapKeepAnchors.Create(document);
        AivPreplacementMapState? preplacementMap = anchors.Slots.Any(item =>
            item.Status == MapKeepAnchorStatus.Exact)
                ? AivPreplacementMapState.Create(document)
                : null;
        parse.Stop();
        Log("INFO", $"Prepared map in {parse.Elapsed.TotalMilliseconds:F1} ms; " +
            $"worldSize={document.Metadata.WorldSize}, cases={cases.Count}.");

        var results = new List<CaseComparison>(cases.Count);
        for (int index = 0; index < cases.Count; index++)
        {
            OracleCase oracleCase = cases[index];
            Stopwatch caseTimer = Stopwatch.StartNew();
            CaseComparison result = CompareCase(preplacementMap, anchors, corpus, oracleCase);
            caseTimer.Stop();
            result.ElapsedMilliseconds = caseTimer.Elapsed.TotalMilliseconds;
            results.Add(result);

            TimeSpan elapsed = total.Elapsed;
            double casesPerSecond = (index + 1) / Math.Max(elapsed.TotalSeconds, 0.001);
            TimeSpan eta = TimeSpan.FromSeconds(
                (cases.Count - index - 1) / Math.Max(casesPerSecond, 0.001));
            Log("INFO", $"Progress {index + 1}/{cases.Count}; case={oracleCase.Id}; " +
                $"classification={result.Classification}; elapsed={elapsed:c}; eta={eta:c}.");
        }

        total.Stop();
        return new ComparisonReport
        {
            CorpusName = corpus.Name,
            MapName = Path.GetFileName(mapPath),
            MapSha256 = NormalizeHash(corpus.Map.Sha256),
            WorldSize = document.Metadata.WorldSize,
            StartedAtLocal = DateTime.Now - total.Elapsed,
            ElapsedMilliseconds = total.Elapsed.TotalMilliseconds,
            Cases = results,
            ExactMatchCount = results.Count(result => result.Classification == ComparisonClassification.ExactMatch),
            NotEvaluableCount = results.Count(result => result.Classification == ComparisonClassification.NotEvaluable),
            MismatchCount = results.Count(result => result.Classification == ComparisonClassification.Mismatch),
            FailedCaseCount = results.Count(result => result.Classification == ComparisonClassification.Error)
        };
    }

    private static CaseComparison CompareCase(
        AivPreplacementMapState? map,
        MapKeepAnchors anchors,
        OracleCorpus corpus,
        OracleCase oracleCase)
    {
        try
        {
            MapCoordinate keep = new MapCoordinate(oracleCase.KeepX, oracleCase.KeepY);
            string aivPath = Path.GetFullPath(oracleCase.AivPath);
            VerifyFile(aivPath, oracleCase.AivSha256, "AIV");

            if (!TryResolveKeepAnchor(
                anchors,
                oracleCase,
                keep,
                out MapKeepAnchorResult? anchor,
                out string anchorFailure))
            {
                return new CaseComparison
                {
                    Id = oracleCase.Id,
                    MapSha256 = NormalizeHash(corpus.Map.Sha256),
                    AivName = Path.GetFileName(aivPath),
                    AivSha256 = NormalizeHash(oracleCase.AivSha256),
                    PlayerId = oracleCase.PlayerId,
                    MapKeepSlot = oracleCase.MapKeepSlot,
                    KeepX = keep.X,
                    KeepY = keep.Y,
                    Rotation = oracleCase.Rotation,
                    Native = oracleCase.Native,
                    Classification = ComparisonClassification.NotEvaluable,
                    FirstDifference = anchorFailure
                };
            }

            if (map is null)
                throw new InvalidOperationException("The exact Keep has no pre-placement map state.");

            AivBlueprint blueprint = LoadBlueprint(aivPath);
            AivPlacementResult offline = new AivPlacementEvaluator().Evaluate(
                map,
                blueprint,
                keep,
                ParseRotation(oracleCase.Rotation));

            ComparisonClassification classification = Classify(offline, oracleCase.Native);
            return new CaseComparison
            {
                Id = oracleCase.Id,
                MapSha256 = NormalizeHash(corpus.Map.Sha256),
                AivName = Path.GetFileName(aivPath),
                AivSha256 = NormalizeHash(oracleCase.AivSha256),
                PlayerId = oracleCase.PlayerId,
                MapKeepSlot = anchor!.SlotIndex,
                KeepX = keep.X,
                KeepY = keep.Y,
                Rotation = oracleCase.Rotation,
                Native = oracleCase.Native,
                Offline = ToOfflineResult(offline),
                Classification = classification,
                FirstDifference = DescribeFirstDifference(offline, oracleCase.Native),
                FirstIssue = ToIssue(SelectFirstIssue(offline))
            };
        }
        catch (Exception ex)
        {
            return new CaseComparison
            {
                Id = oracleCase.Id,
                Classification = ComparisonClassification.Error,
                FirstDifference = ex.Message
            };
        }
    }

    private static AivBlueprint LoadBlueprint(string path)
    {
        // JSON loading stays in the diagnostic tool so the offline core remains package-free.
        byte[] bytes = File.ReadAllBytes(path);
        AivJsonDocument? document = JsonSerializer.Deserialize<AivJsonDocument>(bytes, InputOptions);
        AivParseResult parsed = new AivBlueprintParser().Parse(document, path);
        if (!parsed.IsValid)
        {
            string diagnostics = string.Join(
                "; ",
                parsed.Diagnostics.Select(item => $"{item.Code} {item.Location}: {item.Message}"));
            throw new InvalidDataException($"AIV parsing failed: {diagnostics}");
        }

        return parsed.Blueprint;
    }

    private static bool TryResolveKeepAnchor(
        MapKeepAnchors anchors,
        OracleCase oracleCase,
        MapCoordinate expectedCoordinate,
        out MapKeepAnchorResult? match,
        out string failure)
    {
        // Chat 10 resolves the serialized Keep itself; lobby player-to-slot mapping belongs to Chat 11.
        MapKeepAnchorResult[] matches = anchors.Slots
            .Where(item => item.Status == MapKeepAnchorStatus.Exact &&
                item.Coordinate.HasValue &&
                item.Coordinate.Value.Equals(expectedCoordinate))
            .ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"Keep coordinate {expectedCoordinate} matched {matches.Length} exact map slots.");
        }

        if (matches.Length == 1)
        {
            match = matches[0];
            if (oracleCase.MapKeepSlot.HasValue && oracleCase.MapKeepSlot.Value != match.SlotIndex)
            {
                throw new InvalidOperationException(
                    $"Map Keep slot mismatch: corpus={oracleCase.MapKeepSlot.Value}, map={match.SlotIndex}.");
            }

            failure = string.Empty;
            return true;
        }

        MapKeepAnchorResult[] exactSlots = anchors.Slots
            .Where(item => item.Status == MapKeepAnchorStatus.Exact)
            .ToArray();
        if (exactSlots.Length > 0)
        {
            throw new InvalidOperationException(
                $"Keep coordinate {expectedCoordinate} matched no exact map slot although " +
                $"{exactSlots.Length} exact slots are available.");
        }

        string reasons = string.Join(
            ", ",
            anchors.Slots
                .Where(item => item.IsSelectable)
                .Select(item => item.FailureKind)
                .Distinct()
                .OrderBy(item => item.ToString()));
        match = null;
        failure = $"Offline Keep anchor is unavailable: {reasons}.";
        return false;
    }

    private static ComparisonClassification Classify(
        AivPlacementResult offline,
        NativeOracleResult native)
    {
        if (offline.Status == AivPlacementStatus.NotEvaluable)
            return ComparisonClassification.NotEvaluable;

        bool equal = offline.Status == NativeStatus(native.PlacementState) &&
            offline.Score.SequentialBuildScore == native.RawFitScore &&
            offline.Score.FitPercentage == native.FitPercentage &&
            offline.Score.EvaluatedTileCount == native.EvaluatedCells &&
            offline.Score.BlockedTileCount == native.BlockedCells;
        return equal
            ? ComparisonClassification.ExactMatch
            : ComparisonClassification.Mismatch;
    }

    private static string DescribeFirstDifference(
        AivPlacementResult offline,
        NativeOracleResult native)
    {
        if (offline.Status == AivPlacementStatus.NotEvaluable)
        {
            AivPlacementIssue? unresolved = SelectFirstIssue(offline);
            return unresolved is null
                ? "Offline evaluation requires unresolved native data."
                : $"Offline unresolved rule {unresolved.Kind} at " +
                    $"{unresolved.MapCoordinate}, build step {unresolved.BuildIndex}.";
        }

        AivPlacementStatus expectedStatus = NativeStatus(native.PlacementState);
        if (offline.Status != expectedStatus)
            return $"Status: native={expectedStatus}, offline={offline.Status}.";
        if (offline.Score.SequentialBuildScore != native.RawFitScore)
        {
            return $"Sequential score: native={native.RawFitScore}, " +
                $"offline={offline.Score.SequentialBuildScore}.";
        }
        if (offline.Score.FitPercentage != native.FitPercentage)
        {
            return $"Fit percentage: native={native.FitPercentage}, " +
                $"offline={offline.Score.FitPercentage}.";
        }
        if (offline.Score.EvaluatedTileCount != native.EvaluatedCells)
        {
            return $"Evaluated cells: native={native.EvaluatedCells}, " +
                $"offline={offline.Score.EvaluatedTileCount}.";
        }
        if (offline.Score.BlockedTileCount != native.BlockedCells)
        {
            return $"Blocked cells: native={native.BlockedCells}, " +
                $"offline={offline.Score.BlockedTileCount}.";
        }

        return string.Empty;
    }

    private static AivPlacementIssue? SelectFirstIssue(AivPlacementResult result)
    {
        if (result.Status == AivPlacementStatus.NotEvaluable)
        {
            AivPlacementIssue? unresolved = result.Issues.FirstOrDefault(issue =>
                issue.Kind.HasFlag(AivPlacementIssueKind.UnresolvedNativeRule));
            if (unresolved is not null)
                return unresolved;
        }

        return result.Issues.FirstOrDefault();
    }

    private static OfflineResult ToOfflineResult(AivPlacementResult result) => new()
    {
        Status = result.Status,
        SequentialBuildScore = result.Score.SequentialBuildScore,
        FitPercentage = result.Score.FitPercentage,
        EvaluatedCells = result.Score.EvaluatedTileCount,
        BlockedCells = result.Score.BlockedTileCount,
        TotalElements = result.TotalElementCount,
        BlockedElements = result.BlockedElementCount,
        NotEvaluableElements = result.NotEvaluableElementCount,
        IssueCount = result.Issues.Count
    };

    private static IssueEvidence? ToIssue(AivPlacementIssue? issue)
    {
        if (issue is null)
            return null;

        AivPlacementTileEvidence? evidence = issue.TileEvidence;
        return new IssueEvidence
        {
            Kind = issue.Kind,
            ElementIndex = issue.ElementIndex,
            BuildIndex = issue.BuildIndex,
            MapperValue = issue.MapperValue,
            TileKind = issue.TileKind,
            X = issue.MapCoordinate.X,
            Y = issue.MapCoordinate.Y,
            TileId = issue.TileId,
            TerrainFlags = evidence?.TerrainFlags,
            SecondaryLogic = evidence?.SecondaryLogic,
            Height = evidence?.Height,
            DefaultHeight = evidence?.DefaultHeight,
            OrganismId = evidence?.OrganismId,
            BuildingId = evidence?.BuildingId,
            EntityId = evidence?.EntityId,
            OwnerId = evidence?.OwnerId
        };
    }

    private static void VerifyFile(string path, string expectedHash, string kind)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"The {kind} file does not exist.", path);

        using FileStream stream = File.OpenRead(path);
        string actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!string.Equals(actual, NormalizeHash(expectedHash), StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The {kind} SHA-256 differs: expected={NormalizeHash(expectedHash)}, actual={actual}.");
        }
    }

    private static void WriteTextCrlf(string path, string content)
    {
        // Reports follow the repository's CRLF text contract on every platform.
        string normalized = content.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        File.WriteAllText(path, normalized);
        if (!string.Equals(File.ReadAllText(path), normalized, StringComparison.Ordinal))
            throw new IOException($"The report could not be verified after writing: {path}");
    }

    private static IReadOnlyList<OracleCase> SelectCases(OracleCorpus corpus, Options options)
    {
        IEnumerable<OracleCase> selected = corpus.Cases;
        if (options.CaseId is not null)
            selected = selected.Where(item => string.Equals(item.Id, options.CaseId, StringComparison.Ordinal));
        if (options.Limit.HasValue)
            selected = selected.Take(options.Limit.Value);

        OracleCase[] values = selected.ToArray();
        if (values.Length == 0)
            throw new ArgumentException("The selection contains no Oracle cases.");
        return values;
    }

    private static void ValidateCorpus(OracleCorpus corpus)
    {
        if (string.IsNullOrWhiteSpace(corpus.Name) ||
            string.IsNullOrWhiteSpace(corpus.Map.Path) ||
            string.IsNullOrWhiteSpace(corpus.Map.Sha256) ||
            corpus.Cases.Count == 0)
        {
            throw new InvalidDataException("The Oracle corpus is incomplete.");
        }

        if (corpus.Cases.Any(item => string.IsNullOrWhiteSpace(item.Id)))
            throw new InvalidDataException("Every Oracle case needs an ID.");
        if (corpus.Cases.GroupBy(item => item.Id, StringComparer.Ordinal).Any(group => group.Count() != 1))
            throw new InvalidDataException("Oracle case IDs must be unique.");
    }

    private static T ReadJson<T>(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The Oracle corpus manifest does not exist.", fullPath);
        return JsonSerializer.Deserialize<T>(File.ReadAllBytes(fullPath), InputOptions)
            ?? throw new InvalidDataException("JSON deserialization returned no document.");
    }

    private static AivRotation ParseRotation(int degrees) => degrees switch
    {
        0 => AivRotation.Degrees0,
        90 => AivRotation.Degrees90,
        180 => AivRotation.Degrees180,
        270 => AivRotation.Degrees270,
        _ => throw new InvalidDataException($"Unsupported rotation {degrees}.")
    };

    private static AivPlacementStatus NativeStatus(int placementState) => placementState switch
    {
        2 => AivPlacementStatus.Complete,
        1 => AivPlacementStatus.Partial,
        0 => AivPlacementStatus.Impossible,
        _ => throw new InvalidDataException($"Unknown native placement state {placementState}.")
    };

    private static int ImportOracleLog(string[] args)
    {
        if (args.Length != 3)
        {
            PrintUsage();
            return 2;
        }

        string logPath = Path.GetFullPath(args[1]);
        string outputDirectory = Path.GetFullPath(args[2]);
        if (!File.Exists(logPath))
            throw new FileNotFoundException("The BepInEx log does not exist.", logPath);

        byte[] logBytes = ReadStableSharedFile(logPath);
        var attempts = new List<ImportedOracleAttempt>();
        using var logReader = new StringReader(Encoding.UTF8.GetString(logBytes));
        string? line;
        while ((line = logReader.ReadLine()) is not null)
        {
            Match match = OracleAttemptPattern.Match(line);
            if (!match.Success)
                continue;

            string result = match.Groups["result"].Value;
            attempts.Add(new ImportedOracleAttempt
            {
                Sequence = int.Parse(match.Groups["sequence"].Value),
                Attempt = int.Parse(match.Groups["attempt"].Value),
                MapName = match.Groups["mapName"].Value,
                MapPath = match.Groups["mapPath"].Value,
                MapSha256 = NormalizeHash(match.Groups["mapSha256"].Value),
                PlayerId = int.Parse(match.Groups["playerId"].Value),
                AivName = match.Groups["aivName"].Value,
                AivPath = match.Groups["aivPath"].Value,
                AivSha256 = NormalizeHash(match.Groups["aivSha256"].Value),
                Rotation = NativeOrientationToDegrees(
                    int.Parse(match.Groups["orientation"].Value)),
                KeepX = int.Parse(match.Groups["keepX"].Value),
                KeepY = int.Parse(match.Groups["keepY"].Value),
                Native = new NativeOracleResult
                {
                    PlacementState = result switch
                    {
                        "Complete" => 2,
                        "Partial" => 1,
                        "Rejected" => 0,
                        _ => throw new InvalidDataException($"Unknown Oracle result '{result}'.")
                    },
                    RawFitScore = int.Parse(match.Groups["rawFitScore"].Value),
                    FitPercentage = int.Parse(match.Groups["fitPercentage"].Value),
                    EvaluatedCells = int.Parse(match.Groups["evaluatedCells"].Value),
                    BlockedCells = int.Parse(match.Groups["blockedCells"].Value)
                }
            });
        }

        if (attempts.Count == 0)
            throw new InvalidDataException("The log contains no complete Oracle attempt rows.");

        string sourceLogSha256 = Convert.ToHexString(SHA256.HashData(logBytes));
        Directory.CreateDirectory(outputDirectory);
        int corpusCount = 0;
        foreach (IGrouping<string, ImportedOracleAttempt> group in attempts
            .GroupBy(item => $"{item.MapPath}\n{item.MapSha256}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.First().MapName, StringComparer.OrdinalIgnoreCase))
        {
            ImportedOracleAttempt first = group.First();
            var corpus = new OracleCorpus
            {
                Name = $"Passive Oracle capture {Path.GetFileName(logPath)} - {first.MapName}",
                SourceLogSha256 = sourceLogSha256,
                Map = new OracleMap
                {
                    Path = first.MapPath,
                    Sha256 = first.MapSha256
                },
                Cases = group
                    .OrderBy(item => item.Sequence)
                    .ThenBy(item => item.Attempt)
                    .Select(item => new OracleCase
                    {
                        Id = $"oracle-{item.Sequence:D3}-{item.Attempt:D2}-" +
                            $"{MakeFileStem(item.AivName)}-r{item.Rotation}",
                        AivPath = item.AivPath,
                        AivSha256 = item.AivSha256,
                        PlayerId = item.PlayerId,
                        KeepX = item.KeepX,
                        KeepY = item.KeepY,
                        Rotation = item.Rotation,
                        Native = item.Native
                    })
                    .ToList()
            };

            string outputPath = Path.Combine(
                outputDirectory,
                $"{MakeFileStem(Path.GetFileNameWithoutExtension(first.MapName))}.json");
            WriteTextCrlf(outputPath, JsonSerializer.Serialize(corpus, OutputOptions));
            corpusCount++;
            Log("INFO", $"Imported {corpus.Cases.Count} cases: {outputPath}");
        }

        Log("INFO", $"Import summary: corpora={corpusCount}, attempts={attempts.Count}, " +
            $"sourceLogSha256={sourceLogSha256}.");
        return 0;
    }

    private static int NativeOrientationToDegrees(int orientation) => orientation switch
    {
        0 => 0,
        2 => 90,
        4 => 180,
        6 => 270,
        _ => throw new InvalidDataException($"Unsupported native orientation {orientation}.")
    };

    private static byte[] ReadStableSharedFile(string path)
    {
        var before = new FileInfo(path);
        long expectedLength = before.Length;
        DateTime expectedWriteTimeUtc = before.LastWriteTimeUtc;

        // BepInEx keeps the log open; sharing is safe only if the snapshot stays unchanged.
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        var after = new FileInfo(path);
        if (after.Length != expectedLength || after.LastWriteTimeUtc != expectedWriteTimeUtc)
        {
            throw new IOException(
                "The BepInEx log changed while it was being read; retry after logging is idle.");
        }

        return buffer.ToArray();
    }

    private static string MakeFileStem(string value)
    {
        string stem = new string(value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray());
        return string.Join(
            "-",
            stem.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeHash(string hash) => hash.Replace("-", string.Empty).ToUpperInvariant();

    private static bool TryParseArguments(string[] args, out Options options)
    {
        options = new Options();
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return false;
        }

        options.ManifestPath = args[0];
        for (int index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--case" when index + 1 < args.Length:
                    options.CaseId = args[++index];
                    break;
                case "--limit" when index + 1 < args.Length &&
                    int.TryParse(args[++index], out int limit) && limit > 0:
                    options.Limit = limit;
                    break;
                case "--output" when index + 1 < args.Length:
                    options.OutputPath = args[++index];
                    break;
                default:
                    Log("ERROR", $"Invalid option '{args[index]}'.");
                    PrintUsage();
                    return false;
            }
        }

        return true;
    }

    private static void PrintUsage() => Console.WriteLine(
        "Usage:\n" +
        "  AIVPlacement.OracleComparison <corpus.json> " +
        "[--case <id>] [--limit <count>] [--output <report.json>]\n" +
        "  AIVPlacement.OracleComparison import-log <LogOutput.log> <output-directory>");

    private static void PrintSummary(ComparisonReport report) => Log(
        "INFO",
        $"Summary: cases={report.Cases.Count}, exact={report.ExactMatchCount}, " +
        $"notEvaluable={report.NotEvaluableCount}, mismatches={report.MismatchCount}, " +
        $"errors={report.FailedCaseCount}, elapsed={report.ElapsedMilliseconds:F1} ms.");

    private static void Log(string level, string message) =>
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}");

    private sealed class Options
    {
        public string ManifestPath { get; set; } = string.Empty;
        public string? CaseId { get; set; }
        public int? Limit { get; set; }
        public string? OutputPath { get; set; }
    }
}

internal enum ComparisonClassification
{
    ExactMatch,
    NotEvaluable,
    Mismatch,
    Error
}

internal sealed class OracleCorpus
{
    public string Name { get; set; } = string.Empty;
    public string SourceLogSha256 { get; set; } = string.Empty;
    public OracleMap Map { get; set; } = new();
    public List<OracleCase> Cases { get; set; } = new();
}

internal sealed class OracleMap
{
    public string Path { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
}

internal sealed class OracleCase
{
    public string Id { get; set; } = string.Empty;
    public string AivPath { get; set; } = string.Empty;
    public string AivSha256 { get; set; } = string.Empty;
    public int PlayerId { get; set; }
    public int? MapKeepSlot { get; set; }
    public int KeepX { get; set; }
    public int KeepY { get; set; }
    public int Rotation { get; set; }
    public NativeOracleResult Native { get; set; } = new();
}

internal sealed class NativeOracleResult
{
    public int PlacementState { get; set; }
    public int RawFitScore { get; set; }
    public int FitPercentage { get; set; }
    public int EvaluatedCells { get; set; }
    public int BlockedCells { get; set; }
}

internal sealed class ComparisonReport
{
    public string CorpusName { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public string MapSha256 { get; set; } = string.Empty;
    public int WorldSize { get; set; }
    public DateTime StartedAtLocal { get; set; }
    public double ElapsedMilliseconds { get; set; }
    public int ExactMatchCount { get; set; }
    public int NotEvaluableCount { get; set; }
    public int MismatchCount { get; set; }
    public int FailedCaseCount { get; set; }
    public List<CaseComparison> Cases { get; set; } = new();
}

internal sealed class CaseComparison
{
    public string Id { get; set; } = string.Empty;
    public string MapSha256 { get; set; } = string.Empty;
    public string AivName { get; set; } = string.Empty;
    public string AivSha256 { get; set; } = string.Empty;
    public int PlayerId { get; set; }
    public int? MapKeepSlot { get; set; }
    public int KeepX { get; set; }
    public int KeepY { get; set; }
    public int Rotation { get; set; }
    public NativeOracleResult? Native { get; set; }
    public OfflineResult? Offline { get; set; }
    public ComparisonClassification Classification { get; set; }
    public string FirstDifference { get; set; } = string.Empty;
    public IssueEvidence? FirstIssue { get; set; }
    public double ElapsedMilliseconds { get; set; }
}

internal sealed class OfflineResult
{
    public AivPlacementStatus Status { get; set; }
    public int SequentialBuildScore { get; set; }
    public int FitPercentage { get; set; }
    public int EvaluatedCells { get; set; }
    public int BlockedCells { get; set; }
    public int TotalElements { get; set; }
    public int BlockedElements { get; set; }
    public int NotEvaluableElements { get; set; }
    public int IssueCount { get; set; }
}

internal sealed class IssueEvidence
{
    public AivPlacementIssueKind Kind { get; set; }
    public int ElementIndex { get; set; }
    public int BuildIndex { get; set; }
    public int MapperValue { get; set; }
    public AivProjectedTileKind TileKind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int? TileId { get; set; }
    public int? TerrainFlags { get; set; }
    public byte? SecondaryLogic { get; set; }
    public byte? Height { get; set; }
    public byte? DefaultHeight { get; set; }
    public ushort? OrganismId { get; set; }
    public ushort? BuildingId { get; set; }
    public ushort? EntityId { get; set; }
    public byte? OwnerId { get; set; }
}

internal sealed class ImportedOracleAttempt
{
    public int Sequence { get; set; }
    public int Attempt { get; set; }
    public string MapName { get; set; } = string.Empty;
    public string MapPath { get; set; } = string.Empty;
    public string MapSha256 { get; set; } = string.Empty;
    public int PlayerId { get; set; }
    public string AivName { get; set; } = string.Empty;
    public string AivPath { get; set; } = string.Empty;
    public string AivSha256 { get; set; } = string.Empty;
    public int Rotation { get; set; }
    public int KeepX { get; set; }
    public int KeepY { get; set; }
    public NativeOracleResult Native { get; set; } = new();
}
