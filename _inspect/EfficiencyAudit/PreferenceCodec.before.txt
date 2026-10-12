using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ExtendedData
{
    // Converts the entire public Fixes entry to values accepted by Shared.DependencyFreeJson.
    // A round trip is mandatory: a newly added property must never disappear silently.
    internal static class TypedPreferenceSnapshotCodec
    {
        internal static string Capture(object entry)
        {
            if (entry == null)
                throw new InvalidDataException("The Fixes preference entry is null.");
            Type type = entry.GetType();
            ValidateType(type, new HashSet<Type>(), 0, "Fixes preferences");
            string json = Serialize(entry, type);
            object restored = Restore(type, json);
            if (!string.Equals(json, Serialize(restored, type), StringComparison.Ordinal))
                throw new InvalidDataException("Fixes preferences cannot be represented without value loss.");
            return json;
        }

        internal static object Restore(Type type, string json)
        {
            ValidateType(type, new HashSet<Type>(), 0, "Fixes preferences");
            var values = Shared.DependencyFreeJson.Parse(json) as Dictionary<string, object>;
            if (values == null)
                throw new InvalidDataException("Fixes preferences must be a JSON object.");
            object restored = ConvertObject(values, type, "Fixes preferences", 0);
            if (!PreservesSuppliedValues(values, Shared.DependencyFreeJson.Parse(Serialize(restored, type))))
                throw new InvalidDataException("Fixes preferences cannot be reconstructed without value loss.");
            return restored;
        }

        private static string Serialize(object entry, Type type)
        {
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (PropertyInfo property in Properties(type))
                values.Add(property.Name, ProjectValue(property.GetValue(entry), property.PropertyType,
                    "Fixes preferences." + property.Name, 1));
            return Shared.DependencyFreeJson.Serialize(values);
        }

        private static object ProjectValue(object value, Type type, string path, int depth)
        {
            if (value == null)
                return null;
            if (depth > Shared.DependencyFreeJson.MaximumDepth)
                throw new InvalidDataException(path + " exceeds the JSON depth limit.");
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsEnum || type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
                type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid) ||
                type == typeof(TimeSpan))
                return value;
            if (type.IsArray || (type.IsGenericType && type.GetGenericArguments().Length == 1))
            {
                var values = new List<object>();
                Type elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                foreach (object item in (IEnumerable)value)
                    values.Add(ProjectValue(item, elementType, path + "[]", depth + 1));
                return values;
            }
            if (type.IsGenericType && type.GetGenericArguments().Length == 2)
            {
                var dictionary = value as IDictionary ?? throw new InvalidDataException(path + " is not a readable dictionary.");
                var values = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (string key in dictionary.Keys.Cast<object>().Select(key => key as string ??
                    throw new InvalidDataException(path + " has a non-string key.")).OrderBy(key => key, StringComparer.Ordinal))
                    values.Add(key, ProjectValue(dictionary[key], type.GetGenericArguments()[1], path + "." + key, depth + 1));
                return values;
            }
            var nested = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (PropertyInfo property in Properties(type))
                nested.Add(property.Name, ProjectValue(property.GetValue(value), property.PropertyType,
                    path + "." + property.Name, depth + 1));
            return nested;
        }

        private static PropertyInfo[] Properties(Type type)
        {
            PropertyInfo[] all = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (all.Length == 0 || all.Any(item => item.GetIndexParameters().Length != 0 ||
                item.GetGetMethod() == null || item.GetSetMethod() == null) ||
                all.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != all.Length)
                throw new InvalidDataException("Fixes preference type " + type.FullName +
                    " has unreadable, unwritable, indexed or ambiguous public properties.");
            return all.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        }

        private static void ValidateType(Type type, HashSet<Type> ancestors, int depth, string path)
        {
            if (depth > Shared.DependencyFreeJson.MaximumDepth)
                throw new InvalidDataException(path + " exceeds the JSON depth limit.");
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsEnum || type == typeof(string) || type == typeof(char) || type == typeof(bool) ||
                type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) ||
                type == typeof(ushort) || type == typeof(int) || type == typeof(uint) ||
                type == typeof(long) || type == typeof(ulong) || type == typeof(float) ||
                type == typeof(double) || type == typeof(decimal) || type == typeof(DateTime) ||
                type == typeof(DateTimeOffset) || type == typeof(Guid) || type == typeof(TimeSpan))
                return;
            if (type.IsArray && type.GetArrayRank() == 1)
            {
                ValidateType(type.GetElementType(), ancestors, depth + 1, path + "[]");
                return;
            }
            Type[] args = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
            if (args.Length == 1 && type.IsAssignableFrom(typeof(List<>).MakeGenericType(args)))
            {
                ValidateType(args[0], ancestors, depth + 1, path + "[]");
                return;
            }
            if (args.Length == 2 && args[0] == typeof(string) &&
                type.IsAssignableFrom(typeof(Dictionary<,>).MakeGenericType(args)))
            {
                ValidateType(args[1], ancestors, depth + 1, path + "[key]");
                return;
            }
            if (type.IsInterface || type.IsAbstract || type.ContainsGenericParameters ||
                (!type.IsValueType && type.GetConstructor(Type.EmptyTypes) == null) ||
                !ancestors.Add(type))
                throw new InvalidDataException(path + " uses an unsupported or recursive Fixes type: " + type.FullName);
            try
            {
                foreach (PropertyInfo property in Properties(type))
                    ValidateType(property.PropertyType, ancestors, depth + 1, path + "." + property.Name);
            }
            finally
            {
                ancestors.Remove(type);
            }
        }

        private static object ConvertObject(Dictionary<string, object> values, Type type, string path, int depth, object current = null)
        {
            PropertyInfo[] properties = Properties(type);
            string[] unknown = values.Keys.Where(name => !properties.Any(item => item.Name == name)).ToArray();
            if (unknown.Length != 0)
                throw new InvalidDataException(path + " differs from the installed Fixes property schema: " +
                    "unknown=[" + string.Join(",", unknown) + "].");
            object entry = Activator.CreateInstance(type);
            if (current != null)
                foreach (PropertyInfo property in properties)
                    property.SetValue(entry, CloneDefault(property.GetValue(current), property.PropertyType));
            foreach (PropertyInfo property in properties.Where(item => values.ContainsKey(item.Name)))
                property.SetValue(entry, ConvertValue(values[property.Name], property.PropertyType,
                    path + "." + property.Name, depth + 1, property.GetValue(entry)));
            return entry;
        }

        // Constructor defaults can contain shared mutable references. Clone them before merging
        // so validating a snapshot cannot mutate another Lord's entry or a static default object.
        private static object CloneDefault(object value, Type type)
        {
            if (value == null) return null;
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsEnum || type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
                type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid) || type == typeof(TimeSpan)) return value;
            if (type.IsArray)
            {
                var source = (Array)value; Type element = type.GetElementType(); Array copy = Array.CreateInstance(element, source.Length);
                for (int i = 0; i < source.Length; i++) copy.SetValue(CloneDefault(source.GetValue(i), element), i);
                return copy;
            }
            Type[] arguments = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
            if (arguments.Length == 1)
            {
                var copy = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(arguments));
                foreach (object item in (IEnumerable)value) copy.Add(CloneDefault(item, arguments[0]));
                return copy;
            }
            if (arguments.Length == 2)
            {
                var copy = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(arguments));
                foreach (DictionaryEntry item in (IDictionary)value) copy.Add(item.Key, CloneDefault(item.Value, arguments[1]));
                return copy;
            }
            object result = Activator.CreateInstance(type);
            foreach (PropertyInfo property in Properties(type)) property.SetValue(result, CloneDefault(property.GetValue(value), property.PropertyType));
            return result;
        }

        private static object ConvertValue(object value, Type type, string path, int depth, object current = null)
        {
            if (depth > Shared.DependencyFreeJson.MaximumDepth)
                throw new InvalidDataException(path + " exceeds the JSON depth limit.");
            Type nullable = Nullable.GetUnderlyingType(type);
            if (value == null)
            {
                if (type.IsValueType && nullable == null)
                    throw new InvalidDataException(path + " cannot be null.");
                return null;
            }
            type = nullable ?? type;
            try
            {
                if (type == typeof(string) || type == typeof(bool))
                {
                    if (value.GetType() != type) throw new InvalidCastException();
                    return value;
                }
                if (type == typeof(char))
                {
                    if (!(value is string character) || character.Length != 1) throw new InvalidCastException();
                    return character[0];
                }
                if (type == typeof(DateTime)) return DateTime.Parse(RequireString(value), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if (type == typeof(DateTimeOffset)) return DateTimeOffset.Parse(RequireString(value), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if (type == typeof(Guid)) return Guid.Parse(RequireString(value));
                if (type == typeof(TimeSpan)) return TimeSpan.ParseExact(RequireString(value), "c", CultureInfo.InvariantCulture);
                if (type.IsEnum) return Enum.ToObject(type, Convert.ChangeType(value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture));
                if (type.IsPrimitive || type == typeof(decimal))
                {
                    if (!(value is int || value is long || value is ulong || value is double)) throw new InvalidCastException();
                    return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                }
                if (type.IsArray)
                {
                    var items = value as List<object> ?? throw new InvalidCastException();
                    Type elementType = type.GetElementType();
                    Array array = Array.CreateInstance(elementType, items.Count);
                    for (int index = 0; index < items.Count; index++)
                        array.SetValue(ConvertValue(items[index], elementType, path + "[" + index + "]", depth + 1), index);
                    return array;
                }
                Type[] args = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
                if (args.Length == 1 && type.IsAssignableFrom(typeof(List<>).MakeGenericType(args)))
                {
                    var items = value as List<object> ?? throw new InvalidCastException();
                    var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(args));
                    for (int index = 0; index < items.Count; index++)
                        list.Add(ConvertValue(items[index], args[0], path + "[" + index + "]", depth + 1));
                    return list;
                }
                if (args.Length == 2 && args[0] == typeof(string) &&
                    type.IsAssignableFrom(typeof(Dictionary<,>).MakeGenericType(args)))
                {
                    var items = value as Dictionary<string, object> ?? throw new InvalidCastException();
                    var dictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args));
                    foreach (KeyValuePair<string, object> item in items)
                        dictionary.Add(item.Key, ConvertValue(item.Value, args[1], path + "." + item.Key, depth + 1));
                    return dictionary;
                }
                return ConvertObject(value as Dictionary<string, object> ?? throw new InvalidCastException(), type, path, depth, current);
            }
            catch (Exception exception) when (exception is InvalidCastException || exception is FormatException ||
                exception is OverflowException || exception is ArgumentException)
            {
                throw new InvalidDataException(path + " cannot be converted to the installed Fixes type " + type.FullName + ".", exception);
            }
        }

        // Compare supplied values only: additive properties retain current constructor defaults.
        // Projecting and parsing the restored value exposes rounding and overflow in conversion.
        private static bool PreservesSuppliedValues(object supplied, object restored)
        {
            if (supplied is Dictionary<string, object> fields)
                return restored is Dictionary<string, object> actual && fields.All(item =>
                    actual.TryGetValue(item.Key, out object value) && PreservesSuppliedValues(item.Value, value));
            if (supplied is List<object> items)
                return restored is List<object> actualItems && items.Count == actualItems.Count &&
                    items.Select((item, index) => PreservesSuppliedValues(item, actualItems[index])).All(equal => equal);
            if (Equals(supplied, restored)) return true;
            if (supplied is double && restored is double) return false;
            if (IsNumber(supplied) && IsNumber(restored))
            {
                return CanonicalNumber(supplied) == CanonicalNumber(restored);
            }
            return false;
        }

        private static bool IsNumber(object value) => value is int || value is long || value is ulong || value is double;
        private static string CanonicalNumber(object value)
        {
            string text = value is double number ? number.ToString("R", CultureInfo.InvariantCulture) :
                Convert.ToString(value, CultureInfo.InvariantCulture);
            bool negative = text.StartsWith("-", StringComparison.Ordinal);
            if (negative) text = text.Substring(1);
            int exponent = 0, e = text.IndexOfAny(new[] { 'e', 'E' });
            if (e >= 0) { exponent = int.Parse(text.Substring(e + 1), CultureInfo.InvariantCulture); text = text.Substring(0, e); }
            int point = text.IndexOf('.');
            if (point >= 0) { exponent -= text.Length - point - 1; text = text.Remove(point, 1); }
            text = text.TrimStart('0');
            if (text.Length == 0) return "0";
            int length = text.Length;
            text = text.TrimEnd('0'); exponent += length - text.Length;
            return (negative ? "-" : "") + text + "e" + exponent.ToString(CultureInfo.InvariantCulture);
        }

        private static string RequireString(object value) => value as string ?? throw new InvalidCastException();
    }
}
