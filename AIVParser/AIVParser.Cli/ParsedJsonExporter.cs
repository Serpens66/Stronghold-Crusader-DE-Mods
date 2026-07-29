using System.Text;
using System.Text.Json;
using AIVParser.Core;

namespace AIVParser.Cli;

public static class ParsedJsonExporter
{
    public static void Write(
        string path,
        AivParseResult result,
        AivRotation rotation)
    {
        AivBlueprint blueprint = result.Blueprint;
        AivGridPoint? keep = blueprint.KeepAnchor;

        using FileStream stream = File.Create(path);
        using var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Indented = true
            });

        writer.WriteStartObject();
        writer.WriteString("sourceName", blueprint.SourceName);
        writer.WriteNumber("pauseDelayAmount", blueprint.PauseDelayAmount);
        writer.WriteNumber("rotation", (int)rotation);

        writer.WritePropertyName("summary");
        writer.WriteStartObject();
        writer.WriteNumber("frameCount", blueprint.Frames.Count);
        writer.WriteNumber(
            "pausedFrameCount",
            blueprint.Frames.Count(frame => frame.ShouldPause));
        writer.WriteNumber("miscItemCount", blueprint.MiscItems.Count);
        writer.WriteNumber("warningCount", result.WarningCount);
        writer.WriteNumber("errorCount", result.ErrorCount);
        writer.WriteEndObject();

        writer.WritePropertyName("keepAnchor");
        if (keep.HasValue)
        {
            WritePoint(writer, keep.Value, keep, rotation);
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WritePropertyName("frames");
        writer.WriteStartArray();
        foreach (AivBuildFrame frame in blueprint.Frames)
        {
            writer.WriteStartObject();
            writer.WriteNumber("buildIndex", frame.BuildIndex);
            writer.WriteNumber("itemType", frame.RawItemType);
            writer.WriteString("mapperName", frame.Mapper.Name);
            writer.WriteString("category", frame.Mapper.Category.ToString());
            writer.WriteBoolean("knownMapper", frame.Mapper.IsKnown);
            writer.WriteBoolean("shouldPause", frame.ShouldPause);
            writer.WritePropertyName("positions");
            writer.WriteStartArray();
            foreach (AivGridPoint point in frame.Positions)
            {
                WritePoint(writer, point, keep, rotation);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("miscItems");
        writer.WriteStartArray();
        foreach (AivMiscPlacement item in blueprint.MiscItems)
        {
            writer.WriteStartObject();
            writer.WriteNumber("sourceIndex", item.SourceIndex);
            writer.WriteNumber("rawItemType", item.RawItemType);
            writer.WriteNumber("engineItemType", item.ItemType.EngineValue);
            writer.WriteString("itemTypeName", item.ItemType.Name);
            writer.WriteBoolean("knownItemType", item.ItemType.IsKnown);
            writer.WriteNumber("slotIndex", item.SlotIndex);
            writer.WritePropertyName("position");
            WritePoint(writer, item.Position, keep, rotation);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("diagnostics");
        writer.WriteStartArray();
        foreach (AivDiagnostic diagnostic in result.Diagnostics)
        {
            writer.WriteStartObject();
            writer.WriteString("severity", diagnostic.Severity.ToString());
            writer.WriteString("code", diagnostic.Code);
            writer.WriteString("location", diagnostic.Location);
            writer.WriteString("message", diagnostic.Message);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        writer.Dispose();
        stream.Dispose();

        // Utf8JsonWriter always emits LF. Normalize generated JSON to the
        // Windows workspace's CRLF convention after closing the stream.
        string json = File.ReadAllText(path)
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Replace("\n", "\r\n");
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    private static void WritePoint(
        Utf8JsonWriter writer,
        AivGridPoint point,
        AivGridPoint? keep,
        AivRotation rotation)
    {
        AivGridPoint rotated = AivGridTransform.Rotate(point, rotation);

        writer.WriteStartObject();
        writer.WriteNumber("encodedOffset", point.EncodedOffset);
        writer.WriteNumber("row", point.Row);
        writer.WriteNumber("column", point.Column);
        writer.WriteNumber("rotatedRow", rotated.Row);
        writer.WriteNumber("rotatedColumn", rotated.Column);

        writer.WritePropertyName("anchorDelta");
        if (keep.HasValue)
        {
            AivGridDelta delta =
                AivGridTransform.GetAnchorDelta(point, keep.Value, rotation);
            writer.WriteStartObject();
            writer.WriteNumber("row", delta.Row);
            writer.WriteNumber("column", delta.Column);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WriteEndObject();
    }
}
