using System.Text.Json;
using AIVParser.Core;

namespace AIVParser.Cli;

public sealed class AivJsonLoadResult
{
    public AivJsonLoadResult(
        AivJsonDocument? document,
        IReadOnlyList<AivDiagnostic> diagnostics)
    {
        Document = document;
        Diagnostics = diagnostics;
    }

    public AivJsonDocument? Document { get; }
    public IReadOnlyList<AivDiagnostic> Diagnostics { get; }
}

public static class AivJsonFileLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly HashSet<string> RootProperties =
        new(StringComparer.Ordinal)
        {
            "pauseDelayAmount",
            "frames",
            "miscItems"
        };

    private static readonly HashSet<string> FrameProperties =
        new(StringComparer.Ordinal)
        {
            "itemType",
            "tilePositionOfsets",
            "shouldPause"
        };

    private static readonly HashSet<string> MiscProperties =
        new(StringComparer.Ordinal)
        {
            "positionOfset",
            "itemType",
            "number"
        };

    public static AivJsonLoadResult Load(string path)
    {
        var diagnostics = new List<AivDiagnostic>();
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            using JsonDocument json = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

            InspectSchema(json.RootElement, diagnostics);
            AivJsonDocument? document =
                JsonSerializer.Deserialize<AivJsonDocument>(bytes, SerializerOptions);
            if (document == null)
            {
                diagnostics.Add(Error(
                    "JSON001",
                    "JSON deserialization returned no AIV document.",
                    "$"));
            }

            return new AivJsonLoadResult(document, diagnostics);
        }
        catch (JsonException ex)
        {
            diagnostics.Add(Error(
                "JSON002",
                $"Invalid JSON: {ex.Message}",
                "$"));
        }
        catch (IOException ex)
        {
            diagnostics.Add(Error(
                "JSON003",
                $"Could not read the file: {ex.Message}",
                path));
        }
        catch (UnauthorizedAccessException ex)
        {
            diagnostics.Add(Error(
                "JSON004",
                $"Access denied while reading the file: {ex.Message}",
                path));
        }

        return new AivJsonLoadResult(null, diagnostics);
    }

    private static void InspectSchema(
        JsonElement root,
        ICollection<AivDiagnostic> diagnostics)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Error(
                "JSON005",
                "The AIV JSON root must be an object.",
                "$"));
            return;
        }

        WarnUnknownProperties(root, RootProperties, "$", diagnostics);

        if (!root.TryGetProperty("pauseDelayAmount", out JsonElement pauseDelay))
        {
            diagnostics.Add(Error(
                "JSON006",
                "Required property 'pauseDelayAmount' is missing.",
                "$.pauseDelayAmount"));
        }
        else if (pauseDelay.ValueKind != JsonValueKind.Number ||
                 !pauseDelay.TryGetInt32(out _))
        {
            diagnostics.Add(Error(
                "JSON007",
                "Property 'pauseDelayAmount' must be an Int32.",
                "$.pauseDelayAmount"));
        }

        if (root.TryGetProperty("frames", out JsonElement frames) &&
            frames.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement frame in frames.EnumerateArray())
            {
                string location = $"$.frames[{index}]";
                if (frame.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(Error(
                        "JSON008",
                        "Frame entry must be an object.",
                        location));
                }
                else
                {
                    WarnUnknownProperties(frame, FrameProperties, location, diagnostics);
                    RequireProperty(frame, "itemType", location, diagnostics);
                    RequireProperty(frame, "tilePositionOfsets", location, diagnostics);
                    RequireProperty(frame, "shouldPause", location, diagnostics);
                }

                index++;
            }
        }

        if (root.TryGetProperty("miscItems", out JsonElement miscItems) &&
            miscItems.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in miscItems.EnumerateArray())
            {
                string location = $"$.miscItems[{index}]";
                if (item.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(Error(
                        "JSON009",
                        "Misc entry must be an object.",
                        location));
                }
                else
                {
                    WarnUnknownProperties(item, MiscProperties, location, diagnostics);
                    RequireProperty(item, "positionOfset", location, diagnostics);
                    RequireProperty(item, "itemType", location, diagnostics);
                    RequireProperty(item, "number", location, diagnostics);
                }

                index++;
            }
        }
    }

    private static void WarnUnknownProperties(
        JsonElement value,
        ISet<string> knownProperties,
        string location,
        ICollection<AivDiagnostic> diagnostics)
    {
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!knownProperties.Contains(property.Name))
            {
                diagnostics.Add(new AivDiagnostic(
                    AivDiagnosticSeverity.Warning,
                    "JSON010",
                    $"Unknown JSON property '{property.Name}' was ignored.",
                    location + "." + property.Name));
            }
        }
    }

    private static void RequireProperty(
        JsonElement value,
        string name,
        string location,
        ICollection<AivDiagnostic> diagnostics)
    {
        if (!value.TryGetProperty(name, out _))
        {
            diagnostics.Add(Error(
                "JSON011",
                $"Required property '{name}' is missing.",
                location + "." + name));
        }
    }

    private static AivDiagnostic Error(string code, string message, string location)
    {
        return new AivDiagnostic(AivDiagnosticSeverity.Error, code, message, location);
    }
}
