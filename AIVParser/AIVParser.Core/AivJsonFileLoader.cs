using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AIVParser.Core
{
    public sealed class AivJsonLoadResult
    {
        public AivJsonLoadResult(
            AivJsonDocument document,
            IReadOnlyList<AivDiagnostic> diagnostics)
        {
            Document = document;
            Diagnostics = diagnostics ?? Array.Empty<AivDiagnostic>();
        }

        public AivJsonDocument Document { get; }
        public IReadOnlyList<AivDiagnostic> Diagnostics { get; }
    }

    /// <summary>
    /// Loads the small AIVJSON schema without runtime JSON dependencies. This keeps
    /// the parser usable from the net481 BepInEx plugin as well as the net10 CLI.
    /// </summary>
    public static class AivJsonFileLoader
    {
        private static readonly HashSet<string> RootProperties =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "pauseDelayAmount",
                "frames",
                "miscItems"
            };

        private static readonly HashSet<string> FrameProperties =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "itemType",
                "tilePositionOfsets",
                "shouldPause"
            };

        private static readonly HashSet<string> MiscProperties =
            new HashSet<string>(StringComparer.Ordinal)
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
                string text = File.ReadAllText(
                    path,
                    new UTF8Encoding(false, true));
                return ParseText(text, diagnostics);
            }
            catch (JsonInputException ex)
            {
                diagnostics.Add(Error(
                    "JSON002",
                    $"Invalid JSON: {ex.Message}",
                    "$"));
            }
            catch (DecoderFallbackException ex)
            {
                diagnostics.Add(Error(
                    "JSON002",
                    $"Invalid JSON encoding: {ex.Message}",
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

        public static AivJsonLoadResult LoadText(string text, string sourceName = null)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var diagnostics = new List<AivDiagnostic>();
            try
            {
                return ParseText(text, diagnostics);
            }
            catch (JsonInputException ex)
            {
                diagnostics.Add(Error(
                    "JSON002",
                    $"Invalid JSON: {ex.Message}",
                    string.IsNullOrEmpty(sourceName) ? "$" : sourceName));
            }

            return new AivJsonLoadResult(null, diagnostics);
        }

        private static AivJsonLoadResult ParseText(
            string text,
            ICollection<AivDiagnostic> diagnostics)
        {
            JsonValue root = new PackageFreeJsonParser(text).Parse();
            AivJsonDocument document = ReadDocument(root, diagnostics);
            return new AivJsonLoadResult(
                document,
                diagnostics as IReadOnlyList<AivDiagnostic> ??
                    new List<AivDiagnostic>(diagnostics));
        }

        private static AivJsonDocument ReadDocument(
            JsonValue root,
            ICollection<AivDiagnostic> diagnostics)
        {
            if (root.Kind != JsonKind.Object)
            {
                diagnostics.Add(Error(
                    "JSON005",
                    "The AIV JSON root must be an object.",
                    "$"));
                return null;
            }

            WarnUnknownProperties(root, RootProperties, "$", diagnostics);

            int pauseDelayAmount = 0;
            JsonValue pauseDelay;
            if (!root.ObjectValue.TryGetValue("pauseDelayAmount", out pauseDelay))
            {
                diagnostics.Add(Error(
                    "JSON006",
                    "Required property 'pauseDelayAmount' is missing.",
                    "$.pauseDelayAmount"));
            }
            else if (!TryReadInt32(pauseDelay, out pauseDelayAmount))
            {
                diagnostics.Add(Error(
                    "JSON007",
                    "Property 'pauseDelayAmount' must be an Int32.",
                    "$.pauseDelayAmount"));
                throw new JsonInputException(
                    "Property '$.pauseDelayAmount' cannot be converted to Int32.");
            }

            return new AivJsonDocument
            {
                pauseDelayAmount = pauseDelayAmount,
                frames = ReadFrames(root, diagnostics),
                miscItems = ReadMiscItems(root, diagnostics)
            };
        }

        private static List<AivJsonFrame> ReadFrames(
            JsonValue root,
            ICollection<AivDiagnostic> diagnostics)
        {
            JsonValue frames;
            if (!root.ObjectValue.TryGetValue("frames", out frames) ||
                frames.Kind == JsonKind.Null)
            {
                return null;
            }

            if (frames.Kind != JsonKind.Array)
                throw new JsonInputException("Property '$.frames' must be an array.");

            var result = new List<AivJsonFrame>();
            for (int index = 0; index < frames.ArrayValue.Count; index++)
            {
                JsonValue frame = frames.ArrayValue[index];
                string location = $"$.frames[{index}]";
                if (frame.Kind == JsonKind.Null)
                {
                    diagnostics.Add(Error("JSON008", "Frame entry must be an object.", location));
                    result.Add(null);
                    continue;
                }

                if (frame.Kind != JsonKind.Object)
                {
                    diagnostics.Add(Error("JSON008", "Frame entry must be an object.", location));
                    throw new JsonInputException($"Property '{location}' must be an object.");
                }

                if (frame.ObjectValue.Count == 0)
                {
                    result.Add(new AivJsonFrame
                    {
                        itemType = 0,
                        tilePositionOfsets = new List<int>(),
                        shouldPause = false
                    });
                    continue;
                }

                WarnUnknownProperties(frame, FrameProperties, location, diagnostics);
                RequireProperty(frame, "itemType", location, diagnostics);
                RequireProperty(frame, "tilePositionOfsets", location, diagnostics);
                RequireProperty(frame, "shouldPause", location, diagnostics);

                result.Add(new AivJsonFrame
                {
                    itemType = ReadOptionalInt32(frame, "itemType", location),
                    tilePositionOfsets = ReadOptionalInt32Array(
                        frame,
                        "tilePositionOfsets",
                        location),
                    shouldPause = ReadOptionalBoolean(frame, "shouldPause", location)
                });
            }

            return result;
        }

        private static List<AivJsonMiscItem> ReadMiscItems(
            JsonValue root,
            ICollection<AivDiagnostic> diagnostics)
        {
            JsonValue miscItems;
            if (!root.ObjectValue.TryGetValue("miscItems", out miscItems) ||
                miscItems.Kind == JsonKind.Null)
            {
                return null;
            }

            if (miscItems.Kind != JsonKind.Array)
                throw new JsonInputException("Property '$.miscItems' must be an array.");

            var result = new List<AivJsonMiscItem>();
            for (int index = 0; index < miscItems.ArrayValue.Count; index++)
            {
                JsonValue item = miscItems.ArrayValue[index];
                string location = $"$.miscItems[{index}]";
                if (item.Kind == JsonKind.Null)
                {
                    diagnostics.Add(Error("JSON009", "Misc entry must be an object.", location));
                    result.Add(null);
                    continue;
                }

                if (item.Kind != JsonKind.Object)
                {
                    diagnostics.Add(Error("JSON009", "Misc entry must be an object.", location));
                    throw new JsonInputException($"Property '{location}' must be an object.");
                }

                WarnUnknownProperties(item, MiscProperties, location, diagnostics);
                RequireProperty(item, "positionOfset", location, diagnostics);
                RequireProperty(item, "itemType", location, diagnostics);
                RequireProperty(item, "number", location, diagnostics);

                result.Add(new AivJsonMiscItem
                {
                    positionOfset = ReadOptionalInt32(item, "positionOfset", location),
                    itemType = ReadOptionalInt32(item, "itemType", location),
                    number = ReadOptionalInt32(item, "number", location)
                });
            }

            return result;
        }

        private static int ReadOptionalInt32(
            JsonValue value,
            string propertyName,
            string location)
        {
            JsonValue property;
            if (!value.ObjectValue.TryGetValue(propertyName, out property))
                return 0;

            int result;
            if (!TryReadInt32(property, out result))
            {
                throw new JsonInputException(
                    $"Property '{location}.{propertyName}' must be an Int32.");
            }

            return result;
        }

        private static List<int> ReadOptionalInt32Array(
            JsonValue value,
            string propertyName,
            string location)
        {
            JsonValue property;
            if (!value.ObjectValue.TryGetValue(propertyName, out property) ||
                property.Kind == JsonKind.Null)
            {
                return null;
            }

            if (property.Kind != JsonKind.Array)
            {
                throw new JsonInputException(
                    $"Property '{location}.{propertyName}' must be an array.");
            }

            var result = new List<int>();
            for (int index = 0; index < property.ArrayValue.Count; index++)
            {
                int number;
                if (!TryReadInt32(property.ArrayValue[index], out number))
                {
                    throw new JsonInputException(
                        $"Property '{location}.{propertyName}[{index}]' must be an Int32.");
                }

                result.Add(number);
            }

            return result;
        }

        private static bool ReadOptionalBoolean(
            JsonValue value,
            string propertyName,
            string location)
        {
            JsonValue property;
            if (!value.ObjectValue.TryGetValue(propertyName, out property))
                return false;

            if (property.Kind != JsonKind.Boolean)
            {
                throw new JsonInputException(
                    $"Property '{location}.{propertyName}' must be a Boolean.");
            }

            return property.BooleanValue;
        }

        private static bool TryReadInt32(JsonValue value, out int result)
        {
            result = 0;
            return value.Kind == JsonKind.Number &&
                   int.TryParse(
                       value.NumberValue,
                       NumberStyles.AllowLeadingSign,
                       CultureInfo.InvariantCulture,
                       out result);
        }

        private static void WarnUnknownProperties(
            JsonValue value,
            ISet<string> knownProperties,
            string location,
            ICollection<AivDiagnostic> diagnostics)
        {
            foreach (string name in value.ObjectValue.Keys)
            {
                if (!knownProperties.Contains(name))
                {
                    diagnostics.Add(new AivDiagnostic(
                        AivDiagnosticSeverity.Warning,
                        "JSON010",
                        $"Unknown JSON property '{name}' was ignored.",
                        location + "." + name));
                }
            }
        }

        private static void RequireProperty(
            JsonValue value,
            string name,
            string location,
            ICollection<AivDiagnostic> diagnostics)
        {
            if (!value.ObjectValue.ContainsKey(name))
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

        private enum JsonKind
        {
            Object,
            Array,
            String,
            Number,
            Boolean,
            Null
        }

        private sealed class JsonValue
        {
            private JsonValue(JsonKind kind)
            {
                Kind = kind;
            }

            public JsonKind Kind { get; }
            public Dictionary<string, JsonValue> ObjectValue { get; private set; }
            public List<JsonValue> ArrayValue { get; private set; }
            public string StringValue { get; private set; }
            public string NumberValue { get; private set; }
            public bool BooleanValue { get; private set; }

            public static JsonValue Object(Dictionary<string, JsonValue> value)
            {
                return new JsonValue(JsonKind.Object) { ObjectValue = value };
            }

            public static JsonValue Array(List<JsonValue> value)
            {
                return new JsonValue(JsonKind.Array) { ArrayValue = value };
            }

            public static JsonValue String(string value)
            {
                return new JsonValue(JsonKind.String) { StringValue = value };
            }

            public static JsonValue Number(string value)
            {
                return new JsonValue(JsonKind.Number) { NumberValue = value };
            }

            public static JsonValue Boolean(bool value)
            {
                return new JsonValue(JsonKind.Boolean) { BooleanValue = value };
            }

            public static JsonValue Null()
            {
                return new JsonValue(JsonKind.Null);
            }
        }

        private sealed class PackageFreeJsonParser
        {
            private readonly string text;
            private int index;

            public PackageFreeJsonParser(string text)
            {
                this.text = text ?? string.Empty;
            }

            public JsonValue Parse()
            {
                SkipTrivia();
                JsonValue result = ParseValue();
                SkipTrivia();
                if (index != text.Length)
                    Fail("Unexpected content after the JSON value.");

                return result;
            }

            private JsonValue ParseValue()
            {
                SkipTrivia();
                if (index >= text.Length)
                    Fail("Unexpected end of input.");

                switch (text[index])
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return JsonValue.String(ParseString());
                    case 't':
                        ReadLiteral("true");
                        return JsonValue.Boolean(true);
                    case 'f':
                        ReadLiteral("false");
                        return JsonValue.Boolean(false);
                    case 'n':
                        ReadLiteral("null");
                        return JsonValue.Null();
                    default:
                        if (text[index] == '-' || IsDigit(text[index]))
                            return JsonValue.Number(ParseNumber());
                        Fail($"Unexpected character '{text[index]}'.");
                        return null;
                }
            }

            private JsonValue ParseObject()
            {
                index++;
                var result = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
                SkipTrivia();
                if (Consume('}'))
                    return JsonValue.Object(result);

                while (true)
                {
                    SkipTrivia();
                    if (index >= text.Length || text[index] != '"')
                        Fail("Object property name must be a string.");

                    string name = ParseString();
                    SkipTrivia();
                    Expect(':');
                    result[name] = ParseValue();
                    SkipTrivia();
                    if (Consume('}'))
                        break;

                    Expect(',');
                    SkipTrivia();
                    // The official editor and the old CLI loader both allow trailing commas.
                    if (Consume('}'))
                        break;
                }

                return JsonValue.Object(result);
            }

            private JsonValue ParseArray()
            {
                index++;
                var result = new List<JsonValue>();
                SkipTrivia();
                if (Consume(']'))
                    return JsonValue.Array(result);

                while (true)
                {
                    result.Add(ParseValue());
                    SkipTrivia();
                    if (Consume(']'))
                        break;

                    Expect(',');
                    SkipTrivia();
                    if (Consume(']'))
                        break;
                }

                return JsonValue.Array(result);
            }

            private string ParseString()
            {
                Expect('"');
                var result = new StringBuilder();
                while (index < text.Length)
                {
                    char value = text[index++];
                    if (value == '"')
                        return result.ToString();
                    if (value < 0x20)
                        Fail("Unescaped control character inside a string.");
                    if (value != '\\')
                    {
                        result.Append(value);
                        continue;
                    }

                    if (index >= text.Length)
                        Fail("Unterminated escape sequence.");

                    char escape = text[index++];
                    switch (escape)
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u': result.Append(ParseUnicodeEscape()); break;
                        default: Fail($"Unknown string escape '\\{escape}'."); break;
                    }
                }

                Fail("Unterminated string.");
                return null;
            }

            private char ParseUnicodeEscape()
            {
                if (index + 4 > text.Length)
                    Fail("Incomplete Unicode escape.");

                int value = 0;
                for (int offset = 0; offset < 4; offset++)
                {
                    int digit = HexValue(text[index++]);
                    if (digit < 0)
                        Fail("Invalid Unicode escape.");
                    value = (value << 4) | digit;
                }

                return (char)value;
            }

            private string ParseNumber()
            {
                int start = index;
                Consume('-');
                if (Consume('0'))
                {
                    if (index < text.Length && IsDigit(text[index]))
                        Fail("A JSON number cannot contain a leading zero.");
                }
                else
                {
                    ReadDigits("Expected a digit in the JSON number.");
                }

                if (Consume('.'))
                    ReadDigits("Expected a digit after the decimal point.");

                if (Consume('e') || Consume('E'))
                {
                    if (!Consume('+'))
                        Consume('-');
                    ReadDigits("Expected an exponent digit.");
                }

                return text.Substring(start, index - start);
            }

            private void ReadDigits(string error)
            {
                int start = index;
                while (index < text.Length && IsDigit(text[index]))
                    index++;
                if (index == start)
                    Fail(error);
            }

            private void ReadLiteral(string literal)
            {
                if (index + literal.Length > text.Length ||
                    !string.Equals(
                        text.Substring(index, literal.Length),
                        literal,
                        StringComparison.Ordinal))
                {
                    Fail($"Expected '{literal}'.");
                }

                index += literal.Length;
            }

            private void SkipTrivia()
            {
                while (index < text.Length)
                {
                    if (char.IsWhiteSpace(text[index]))
                    {
                        index++;
                        continue;
                    }

                    if (index + 1 >= text.Length || text[index] != '/')
                        return;

                    if (text[index + 1] == '/')
                    {
                        index += 2;
                        while (index < text.Length &&
                               text[index] != '\r' &&
                               text[index] != '\n')
                        {
                            index++;
                        }
                        continue;
                    }

                    if (text[index + 1] == '*')
                    {
                        index += 2;
                        int end = text.IndexOf("*/", index, StringComparison.Ordinal);
                        if (end < 0)
                            Fail("Unterminated block comment.");
                        index = end + 2;
                        continue;
                    }

                    return;
                }
            }

            private void Expect(char value)
            {
                if (!Consume(value))
                    Fail($"Expected '{value}'.");
            }

            private bool Consume(char value)
            {
                if (index >= text.Length || text[index] != value)
                    return false;
                index++;
                return true;
            }

            private void Fail(string message)
            {
                throw new JsonInputException($"{message} Position {index}.");
            }

            private static bool IsDigit(char value)
            {
                return value >= '0' && value <= '9';
            }

            private static int HexValue(char value)
            {
                if (value >= '0' && value <= '9')
                    return value - '0';
                if (value >= 'a' && value <= 'f')
                    return value - 'a' + 10;
                if (value >= 'A' && value <= 'F')
                    return value - 'A' + 10;
                return -1;
            }
        }

        private sealed class JsonInputException : Exception
        {
            public JsonInputException(string message)
                : base(message)
            {
            }
        }
    }
}
