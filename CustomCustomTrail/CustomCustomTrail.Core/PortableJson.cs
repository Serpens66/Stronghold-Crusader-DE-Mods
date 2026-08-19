using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CustomCustomTrail.Core
{
    // Small JSON codec kept inside Core so Unity does not need an additional serializer assembly.
    internal static class PortableJson
    {
        public static object Parse(string json) => new Parser(json ?? string.Empty).Parse();

        public static string Serialize(object value)
        {
            var output = new StringBuilder(4096);
            WriteValue(output, value, 0);
            output.Append("\r\n");
            return output.ToString();
        }

        private static void WriteValue(StringBuilder output, object value, int depth)
        {
            if (value == null)
            {
                output.Append("null");
                return;
            }
            if (value is string text)
            {
                WriteString(output, text);
                return;
            }
            if (value is bool boolean)
            {
                output.Append(boolean ? "true" : "false");
                return;
            }
            if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint ||
                value is long || value is ulong || value is decimal)
            {
                output.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }
            if (value is float single)
            {
                if (float.IsNaN(single) || float.IsInfinity(single))
                    throw new InvalidDataException("JSON cannot contain a non-finite number.");
                output.Append(single.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (value is double number)
            {
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new InvalidDataException("JSON cannot contain a non-finite number.");
                output.Append(number.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (value is IDictionary<string, object> dictionary)
            {
                WriteObject(output, dictionary, depth);
                return;
            }
            if (value is IEnumerable sequence)
            {
                WriteArray(output, sequence.Cast<object>(), depth);
                return;
            }
            throw new InvalidDataException("Unsupported JSON value [" + value.GetType().FullName + "].");
        }

        private static void WriteObject(StringBuilder output, IDictionary<string, object> values, int depth)
        {
            output.Append('{');
            if (values.Count == 0)
            {
                output.Append('}');
                return;
            }
            output.Append("\r\n");
            int index = 0;
            foreach (KeyValuePair<string, object> value in values)
            {
                Indent(output, depth + 1);
                WriteString(output, value.Key);
                output.Append(": ");
                WriteValue(output, value.Value, depth + 1);
                if (++index < values.Count) output.Append(',');
                output.Append("\r\n");
            }
            Indent(output, depth);
            output.Append('}');
        }

        private static void WriteArray(StringBuilder output, IEnumerable<object> values, int depth)
        {
            object[] items = values.ToArray();
            output.Append('[');
            if (items.Length == 0)
            {
                output.Append(']');
                return;
            }
            output.Append("\r\n");
            for (int index = 0; index < items.Length; index++)
            {
                Indent(output, depth + 1);
                WriteValue(output, items[index], depth + 1);
                if (index + 1 < items.Length) output.Append(',');
                output.Append("\r\n");
            }
            Indent(output, depth);
            output.Append(']');
        }

        private static void Indent(StringBuilder output, int depth) => output.Append(' ', depth * 2);

        private static void WriteString(StringBuilder output, string value)
        {
            output.Append('"');
            foreach (char character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (character < 0x20) output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else output.Append(character);
                        break;
                }
            }
            output.Append('"');
        }

        private sealed class Parser
        {
            private readonly string json;
            private int position;

            public Parser(string json) => this.json = json;

            public object Parse()
            {
                object value = ParseValue();
                SkipWhitespace();
                if (position != json.Length) Fail("Unexpected trailing content");
                return value;
            }

            private object ParseValue()
            {
                SkipWhitespace();
                if (position >= json.Length) Fail("Unexpected end of JSON");
                switch (json[position])
                {
                    case '{': return ParseDictionary();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': ReadLiteral("true"); return true;
                    case 'f': ReadLiteral("false"); return false;
                    case 'n': ReadLiteral("null"); return null;
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseDictionary()
            {
                position++;
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace();
                if (Consume('}')) return result;
                while (true)
                {
                    SkipWhitespace();
                    if (position >= json.Length || json[position] != '"') Fail("Expected an object property name");
                    string key = ParseString();
                    SkipWhitespace();
                    if (!Consume(':')) Fail("Expected ':' after an object property name");
                    if (result.ContainsKey(key)) Fail("Duplicate object property [" + key + "]");
                    result[key] = ParseValue();
                    SkipWhitespace();
                    if (Consume('}')) return result;
                    if (!Consume(',')) Fail("Expected ',' or '}' in object");
                }
            }

            private List<object> ParseArray()
            {
                position++;
                var result = new List<object>();
                SkipWhitespace();
                if (Consume(']')) return result;
                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    if (Consume(']')) return result;
                    if (!Consume(',')) Fail("Expected ',' or ']' in array");
                }
            }

            private string ParseString()
            {
                position++;
                var result = new StringBuilder();
                while (position < json.Length)
                {
                    char character = json[position++];
                    if (character == '"') return result.ToString();
                    if (character < 0x20) Fail("Unescaped control character in string");
                    if (character != '\\')
                    {
                        result.Append(character);
                        continue;
                    }
                    if (position >= json.Length) Fail("Incomplete string escape");
                    switch (json[position++])
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
                        default: Fail("Invalid string escape"); break;
                    }
                }
                Fail("Unterminated string");
                return null;
            }

            private char ParseUnicodeEscape()
            {
                if (position + 4 > json.Length) Fail("Incomplete Unicode escape");
                string digits = json.Substring(position, 4);
                if (!ushort.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort value))
                    Fail("Invalid Unicode escape");
                position += 4;
                return (char)value;
            }

            private object ParseNumber()
            {
                int start = position;
                if (Consume('-') && position >= json.Length) Fail("Incomplete number");
                if (Consume('0'))
                {
                    if (position < json.Length && char.IsDigit(json[position])) Fail("Leading zero in number");
                }
                else
                {
                    if (position >= json.Length || json[position] < '1' || json[position] > '9') Fail("Invalid JSON value");
                    while (position < json.Length && char.IsDigit(json[position])) position++;
                }
                bool fractional = false;
                if (Consume('.'))
                {
                    fractional = true;
                    if (position >= json.Length || !char.IsDigit(json[position])) Fail("Invalid number fraction");
                    while (position < json.Length && char.IsDigit(json[position])) position++;
                }
                if (position < json.Length && (json[position] == 'e' || json[position] == 'E'))
                {
                    fractional = true;
                    position++;
                    if (position < json.Length && (json[position] == '+' || json[position] == '-')) position++;
                    if (position >= json.Length || !char.IsDigit(json[position])) Fail("Invalid number exponent");
                    while (position < json.Length && char.IsDigit(json[position])) position++;
                }
                string token = json.Substring(start, position - start);
                if (fractional)
                {
                    if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double real) || double.IsNaN(real) || double.IsInfinity(real))
                        Fail("Invalid floating-point number");
                    return real;
                }
                if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer))
                    Fail("Integer is outside the supported range");
                return integer >= int.MinValue && integer <= int.MaxValue ? (object)(int)integer : integer;
            }

            private void ReadLiteral(string literal)
            {
                if (position + literal.Length > json.Length || string.CompareOrdinal(json, position, literal, 0, literal.Length) != 0)
                    Fail("Invalid JSON literal [" + literal + "]");
                position += literal.Length;
            }

            private bool Consume(char character)
            {
                if (position >= json.Length || json[position] != character) return false;
                position++;
                return true;
            }

            private void SkipWhitespace()
            {
                while (position < json.Length && (json[position] == ' ' || json[position] == '\t' || json[position] == '\r' || json[position] == '\n')) position++;
            }

            private void Fail(string message) => throw new InvalidDataException(message + " at JSON position " + position + ".");
        }
    }
}
