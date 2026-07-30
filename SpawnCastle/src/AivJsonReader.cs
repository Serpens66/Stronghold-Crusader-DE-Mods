using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SpawnCastle
{
    /// <summary>
    /// Dependency-free reader for the small, stable JSON schema used by AIV files.
    /// </summary>
    internal static class AivJsonReader
    {
        public static AivJsonDocument Parse(string json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));

            return new Reader(json).ReadDocument();
        }

        private sealed class Reader
        {
            private readonly string text;
            private int position;

            public Reader(string text)
            {
                this.text = text;
            }

            public AivJsonDocument ReadDocument()
            {
                var document = new AivJsonDocument();
                ReadObject(
                    propertyName =>
                    {
                        switch (propertyName)
                        {
                            case "pauseDelayAmount":
                                document.pauseDelayAmount = ReadInt32();
                                break;
                            case "frames":
                                document.frames = ReadFrameArray();
                                break;
                            case "miscItems":
                                document.miscItems = ReadMiscItemArray();
                                break;
                            default:
                                SkipValue();
                                break;
                        }
                    });

                SkipWhitespace();
                if (position != text.Length)
                    ThrowFormat("Unexpected content after the root object.");

                return document;
            }

            private List<AivJsonFrame> ReadFrameArray()
            {
                var frames = new List<AivJsonFrame>();
                ReadArray(() => frames.Add(ReadFrame()));
                return frames;
            }

            private AivJsonFrame ReadFrame()
            {
                var frame = new AivJsonFrame();
                ReadObject(
                    propertyName =>
                    {
                        switch (propertyName)
                        {
                            case "itemType":
                                frame.itemType = ReadInt32();
                                break;
                            case "tilePositionOfsets":
                                frame.tilePositionOfsets = ReadInt32Array();
                                break;
                            case "shouldPause":
                                frame.shouldPause = ReadBoolean();
                                break;
                            default:
                                SkipValue();
                                break;
                        }
                    });
                return frame;
            }

            private List<AivJsonMiscItem> ReadMiscItemArray()
            {
                var items = new List<AivJsonMiscItem>();
                ReadArray(() => items.Add(ReadMiscItem()));
                return items;
            }

            private AivJsonMiscItem ReadMiscItem()
            {
                var item = new AivJsonMiscItem();
                ReadObject(
                    propertyName =>
                    {
                        switch (propertyName)
                        {
                            case "positionOfset":
                                item.positionOfset = ReadInt32();
                                break;
                            case "itemType":
                                item.itemType = ReadInt32();
                                break;
                            case "number":
                                item.number = ReadInt32();
                                break;
                            default:
                                SkipValue();
                                break;
                        }
                    });
                return item;
            }

            private List<int> ReadInt32Array()
            {
                var values = new List<int>();
                ReadArray(() => values.Add(ReadInt32()));
                return values;
            }

            private void ReadObject(Action<string> readProperty)
            {
                Expect('{');
                if (TryConsume('}'))
                    return;

                while (true)
                {
                    string propertyName = ReadString();
                    Expect(':');
                    readProperty(propertyName);

                    if (TryConsume('}'))
                        return;

                    Expect(',');
                    // Accept trailing commas because the official toolchain does too.
                    if (TryConsume('}'))
                        return;
                }
            }

            private void ReadArray(Action readElement)
            {
                Expect('[');
                if (TryConsume(']'))
                    return;

                while (true)
                {
                    readElement();
                    if (TryConsume(']'))
                        return;

                    Expect(',');
                    if (TryConsume(']'))
                        return;
                }
            }

            private int ReadInt32()
            {
                SkipWhitespace();
                int start = position;
                if (Peek() == '-')
                    position++;

                int digitStart = position;
                while (char.IsDigit(Peek()))
                    position++;

                if (position == digitStart)
                    ThrowFormat("Expected an Int32 value.");

                string valueText = text.Substring(start, position - start);
                if (!int.TryParse(
                    valueText,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out int value))
                {
                    ThrowFormat($"Integer '{valueText}' is outside the Int32 range.");
                }

                return value;
            }

            private bool ReadBoolean()
            {
                SkipWhitespace();
                if (TryConsumeLiteral("true"))
                    return true;
                if (TryConsumeLiteral("false"))
                    return false;

                ThrowFormat("Expected a boolean value.");
                return false;
            }

            private string ReadString()
            {
                SkipWhitespace();
                if (Peek() != '"')
                    ThrowFormat("Expected a JSON string.");

                position++;
                var result = new StringBuilder();
                while (position < text.Length)
                {
                    char current = text[position++];
                    if (current == '"')
                        return result.ToString();

                    if (current < 0x20)
                        ThrowFormat("Unescaped control character in JSON string.");

                    if (current != '\\')
                    {
                        result.Append(current);
                        continue;
                    }

                    if (position >= text.Length)
                        ThrowFormat("Unterminated JSON escape sequence.");

                    char escaped = text[position++];
                    switch (escaped)
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u': result.Append(ReadUnicodeEscape()); break;
                        default:
                            ThrowFormat($"Unsupported JSON escape sequence '\\{escaped}'.");
                            break;
                    }
                }

                ThrowFormat("Unterminated JSON string.");
                return null;
            }

            private char ReadUnicodeEscape()
            {
                if (position + 4 > text.Length)
                    ThrowFormat("Incomplete JSON unicode escape.");

                string digits = text.Substring(position, 4);
                position += 4;
                if (!ushort.TryParse(
                    digits,
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out ushort value))
                {
                    ThrowFormat($"Invalid JSON unicode escape '\\u{digits}'.");
                }

                return (char)value;
            }

            private void SkipValue()
            {
                SkipWhitespace();
                switch (Peek())
                {
                    case '{':
                        ReadObject(_ => SkipValue());
                        return;
                    case '[':
                        ReadArray(SkipValue);
                        return;
                    case '"':
                        ReadString();
                        return;
                    case 't':
                        ExpectLiteral("true");
                        return;
                    case 'f':
                        ExpectLiteral("false");
                        return;
                    case 'n':
                        ExpectLiteral("null");
                        return;
                    default:
                        SkipNumber();
                        return;
                }
            }

            private void SkipNumber()
            {
                SkipWhitespace();
                int start = position;
                if (Peek() == '-')
                    position++;

                while (char.IsDigit(Peek()))
                    position++;

                if (Peek() == '.')
                {
                    position++;
                    while (char.IsDigit(Peek()))
                        position++;
                }

                if (Peek() == 'e' || Peek() == 'E')
                {
                    position++;
                    if (Peek() == '+' || Peek() == '-')
                        position++;
                    while (char.IsDigit(Peek()))
                        position++;
                }

                if (position == start)
                    ThrowFormat("Expected a JSON value.");
            }

            private void Expect(char expected)
            {
                SkipWhitespace();
                if (Peek() != expected)
                    ThrowFormat($"Expected '{expected}'.");

                position++;
            }

            private bool TryConsume(char expected)
            {
                SkipWhitespace();
                if (Peek() != expected)
                    return false;

                position++;
                return true;
            }

            private void ExpectLiteral(string literal)
            {
                if (!TryConsumeLiteral(literal))
                    ThrowFormat($"Expected '{literal}'.");
            }

            private bool TryConsumeLiteral(string literal)
            {
                SkipWhitespace();
                if (position + literal.Length > text.Length ||
                    string.CompareOrdinal(text, position, literal, 0, literal.Length) != 0)
                {
                    return false;
                }

                position += literal.Length;
                return true;
            }

            private void SkipWhitespace()
            {
                while (position < text.Length)
                {
                    char current = text[position];
                    if (current == '\uFEFF' ||
                        current == ' ' ||
                        current == '\t' ||
                        current == '\r' ||
                        current == '\n')
                    {
                        position++;
                        continue;
                    }

                    break;
                }
            }

            private char Peek()
            {
                return position < text.Length ? text[position] : '\0';
            }

            private void ThrowFormat(string message)
            {
                throw new FormatException(
                    $"Invalid AIVJSON at character {position}: {message}");
            }
        }
    }
}
