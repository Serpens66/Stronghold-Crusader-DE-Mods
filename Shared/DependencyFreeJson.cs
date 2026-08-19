using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Shared
{
    // Shared JSON codec for Unity runtime projects that cannot safely load serializer assemblies.
    public static class DependencyFreeJson
    {
        public const int MaximumDepth = 64;

        private static readonly object PropertyCacheLock = new object();
        private static readonly Dictionary<Type, PropertyInfo[]> PropertyCache =
            new Dictionary<Type, PropertyInfo[]>();

        public static object Parse(string json, bool allowTrailingCommas = false) =>
            new Parser(json ?? string.Empty, allowTrailingCommas).Parse();

        public static string Serialize(object value)
        {
            var output = new StringBuilder(4096);
            WriteValue(output, value, 0, new HashSet<object>(ReferenceComparer.Instance));
            output.Append("\r\n");
            return output.ToString();
        }

        private static void WriteValue(
            StringBuilder output,
            object value,
            int depth,
            HashSet<object> ancestors)
        {
            if (depth > MaximumDepth)
                throw new InvalidDataException("JSON exceeds the maximum depth of " + MaximumDepth + ".");
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
            if (value is char characterValue)
            {
                WriteString(output, characterValue.ToString());
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
            if (value is DateTime dateTime)
            {
                WriteString(output, dateTime.ToString("O", CultureInfo.InvariantCulture));
                return;
            }
            if (value is DateTimeOffset dateTimeOffset)
            {
                WriteString(output, dateTimeOffset.ToString("O", CultureInfo.InvariantCulture));
                return;
            }
            if (value is Guid guid)
            {
                WriteString(output, guid.ToString("D"));
                return;
            }
            if (value is TimeSpan timeSpan)
            {
                WriteString(output, timeSpan.ToString("c", CultureInfo.InvariantCulture));
                return;
            }

            Type valueType = value.GetType();
            if (valueType.IsEnum)
            {
                TypeCode underlyingCode = Type.GetTypeCode(Enum.GetUnderlyingType(valueType));
                if (underlyingCode == TypeCode.Byte || underlyingCode == TypeCode.UInt16 ||
                    underlyingCode == TypeCode.UInt32 || underlyingCode == TypeCode.UInt64)
                {
                    output.Append(Convert.ToUInt64(value, CultureInfo.InvariantCulture)
                        .ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    output.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture)
                        .ToString(CultureInfo.InvariantCulture));
                }
                return;
            }

            bool trackReference = !valueType.IsValueType;
            if (trackReference && !ancestors.Add(value))
                throw new InvalidDataException("JSON cannot serialize a cyclic object graph.");

            try
            {
                if (value is IDictionary dictionary)
                {
                    WriteObject(output, dictionary, depth, ancestors);
                    return;
                }
                if (value is IEnumerable sequence)
                {
                    WriteArray(output, sequence, depth, ancestors);
                    return;
                }

                PropertyInfo[] properties = GetSerializableProperties(valueType);
                if (properties.Length == 0)
                    throw new InvalidDataException("Unsupported JSON value [" + valueType.FullName + "].");
                WriteObject(output, value, properties, depth, ancestors);
            }
            finally
            {
                if (trackReference)
                    ancestors.Remove(value);
            }
        }

        private static void WriteObject(
            StringBuilder output,
            IDictionary values,
            int depth,
            HashSet<object> ancestors)
        {
            output.Append('{');
            if (values.Count == 0)
            {
                output.Append('}');
                return;
            }
            output.Append("\r\n");
            int index = 0;
            foreach (DictionaryEntry value in values)
            {
                if (!(value.Key is string key))
                    throw new InvalidDataException("JSON object keys must be strings.");
                Indent(output, depth + 1);
                WriteString(output, key);
                output.Append(": ");
                WriteValue(output, value.Value, depth + 1, ancestors);
                if (++index < values.Count) output.Append(',');
                output.Append("\r\n");
            }
            Indent(output, depth);
            output.Append('}');
        }

        private static void WriteObject(
            StringBuilder output,
            object value,
            PropertyInfo[] properties,
            int depth,
            HashSet<object> ancestors)
        {
            output.Append('{');
            if (properties.Length == 0)
            {
                output.Append('}');
                return;
            }
            output.Append("\r\n");
            for (int index = 0; index < properties.Length; index++)
            {
                Indent(output, depth + 1);
                WriteString(output, properties[index].Name);
                output.Append(": ");
                WriteValue(output, properties[index].GetValue(value, null), depth + 1, ancestors);
                if (index + 1 < properties.Length) output.Append(',');
                output.Append("\r\n");
            }
            Indent(output, depth);
            output.Append('}');
        }

        private static void WriteArray(
            StringBuilder output,
            IEnumerable values,
            int depth,
            HashSet<object> ancestors)
        {
            output.Append('[');
            IEnumerator enumerator = values.GetEnumerator();
            try
            {
                if (!enumerator.MoveNext())
                {
                    output.Append(']');
                    return;
                }

                output.Append("\r\n");
                bool first = true;
                do
                {
                    if (!first)
                        output.Append(",\r\n");
                    first = false;
                    Indent(output, depth + 1);
                    WriteValue(output, enumerator.Current, depth + 1, ancestors);
                }
                while (enumerator.MoveNext());
                output.Append("\r\n");
                Indent(output, depth);
                output.Append(']');
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }

        private static PropertyInfo[] GetSerializableProperties(Type type)
        {
            lock (PropertyCacheLock)
            {
                if (PropertyCache.TryGetValue(type, out PropertyInfo[] cached))
                    return cached;

                var properties = new List<PropertyInfo>();
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (property.CanRead && property.GetIndexParameters().Length == 0)
                        properties.Add(property);
                }
                properties.Sort((left, right) =>
                {
                    int metadataOrder = left.MetadataToken.CompareTo(right.MetadataToken);
                    return metadataOrder != 0
                        ? metadataOrder
                        : string.CompareOrdinal(left.Name, right.Name);
                });
                cached = properties.ToArray();
                PropertyCache.Add(type, cached);
                return cached;
            }
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

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object left, object right) => ReferenceEquals(left, right);

            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }

        private sealed class Parser
        {
            private readonly string json;
            private readonly bool allowTrailingCommas;
            private int position;

            public Parser(string json, bool allowTrailingCommas)
            {
                this.json = json;
                this.allowTrailingCommas = allowTrailingCommas;
            }

            public object Parse()
            {
                object value = ParseValue(0);
                SkipWhitespace();
                if (position != json.Length) Fail("Unexpected trailing content");
                return value;
            }

            private object ParseValue(int depth)
            {
                if (depth > MaximumDepth)
                    Fail("JSON exceeds the maximum depth of " + MaximumDepth);
                SkipWhitespace();
                if (position >= json.Length) Fail("Unexpected end of JSON");
                switch (json[position])
                {
                    case '{': return ParseDictionary(depth);
                    case '[': return ParseArray(depth);
                    case '"': return ParseString();
                    case 't': ReadLiteral("true"); return true;
                    case 'f': ReadLiteral("false"); return false;
                    case 'n': ReadLiteral("null"); return null;
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseDictionary(int depth)
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
                    result[key] = ParseValue(depth + 1);
                    SkipWhitespace();
                    if (Consume('}')) return result;
                    if (!Consume(',')) Fail("Expected ',' or '}' in object");
                    SkipWhitespace();
                    if (allowTrailingCommas && Consume('}')) return result;
                }
            }

            private List<object> ParseArray(int depth)
            {
                position++;
                var result = new List<object>();
                SkipWhitespace();
                if (Consume(']')) return result;
                while (true)
                {
                    result.Add(ParseValue(depth + 1));
                    SkipWhitespace();
                    if (Consume(']')) return result;
                    if (!Consume(',')) Fail("Expected ',' or ']' in array");
                    SkipWhitespace();
                    if (allowTrailingCommas && Consume(']')) return result;
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
                {
                    if (token[0] == '-')
                        Fail("Integer is outside the supported range");
                    if (!ulong.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out ulong unsignedInteger))
                        Fail("Integer is outside the supported range");
                    return unsignedInteger;
                }
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
                while (position < json.Length &&
                       (json[position] == ' ' || json[position] == '\t' || json[position] == '\r' ||
                        json[position] == '\n' || (position == 0 && json[position] == '\uFEFF')))
                    position++;
            }

            private void Fail(string message) => throw new InvalidDataException(message + " at JSON position " + position + ".");
        }
    }
}
