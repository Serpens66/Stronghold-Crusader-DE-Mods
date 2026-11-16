using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GameStateProbe
{
    /// <summary>
    /// Evaluates "Type::Member.Member[index].Method(args).*" against live managed state.
    /// - Type: full or simple name of any loaded type; members are searched static first, then instance.
    /// - [i] indexes arrays/IList, [key] indexes IDictionary (string or int key).
    /// - Methods: only read-style names (Get*, Is*, Has*, Can*, Count*, Find*, Contains*, ToString) with literal
    ///   arguments (numbers, true/false, "strings", enum names); methods with out/ref parameters are rejected.
    /// - ".*" dereferences a native pointer (T*) or NativePointer&lt;T&gt; to a blittable struct copy after a readability check.
    /// </summary>
    internal static class PathEvaluator
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] ReadPrefixes = { "Get", "Is", "Has", "Can", "Count", "Find", "Contains", "ToString" };

        public static object Evaluate(string path)
        {
            int sep = path.IndexOf("::", StringComparison.Ordinal);
            if (sep < 0) throw new ArgumentException("path must look like Type::Member.Member");
            var type = ResolveType(path.Substring(0, sep).Trim());
            object current = null;
            Type currentType = type;
            bool isStatic = true;
            foreach (var segment in Split(path.Substring(sep + 2)))
            {
                current = Step(current, currentType, isStatic, segment);
                isStatic = false;
                currentType = current?.GetType();
                if (current == null) break;
            }
            return current;
        }

        public static Type ResolveType(string name)
        {
            Type found = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                catch { continue; }
                foreach (var t in types)
                {
                    if (t.FullName == name) return t;
                    if (found == null && t.Name == name) found = t;
                }
            }
            return found ?? throw new ArgumentException("type '" + name + "' not found");
        }

        /// <summary>Splits on '.' outside of brackets, parentheses and quotes.</summary>
        private static IEnumerable<string> Split(string s)
        {
            int depth = 0, start = 0; bool quote = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') quote = !quote;
                else if (!quote && (c == '(' || c == '[')) depth++;
                else if (!quote && (c == ')' || c == ']')) depth--;
                else if (!quote && depth == 0 && c == '.')
                {
                    if (i > start) yield return s.Substring(start, i - start).Trim();
                    start = i + 1;
                }
            }
            if (start < s.Length) yield return s.Substring(start).Trim();
        }

        private static object Step(object target, Type type, bool isStatic, string segment)
        {
            if (segment == "*") return Deref(target);

            // Trailing [index] parts are applied after the member access.
            var indexes = new List<string>();
            while (segment.EndsWith("]", StringComparison.Ordinal))
            {
                int open = segment.LastIndexOf('[');
                indexes.Insert(0, segment.Substring(open + 1, segment.Length - open - 2));
                segment = segment.Substring(0, open);
            }

            object value = segment.Length == 0 ? target
                : segment.EndsWith(")", StringComparison.Ordinal) ? Call(target, type, isStatic, segment)
                : Member(target, type, isStatic, segment);

            foreach (var idx in indexes) value = Index(value, idx);
            return value;
        }

        private static object Member(object target, Type type, bool isStatic, string name)
        {
            var flags = Any | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            for (var t = type; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (f != null) return f.GetValue(isStatic ? null : target);
                var p = t.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                if (p != null && p.GetIndexParameters().Length == 0)
                {
                    GuardProperty(t, p);
                    return p.GetValue(isStatic ? null : target, null);
                }
            }
            throw new ArgumentException((isStatic ? "static " : "") + "member '" + name + "' not found on " + type.FullName);
        }

        /// <summary>Getters known to have side effects (see AGENTS/runtime-lifecycle.md).</summary>
        private static void GuardProperty(Type t, PropertyInfo p)
        {
            if (t.Name == "MainViewModel" && p.Name == "Instance")
            {
                var loaded = t.GetField("viewModelLoaded", Any | BindingFlags.Static);
                if (loaded == null || !(loaded.GetValue(null) is bool b) || !b)
                    throw new InvalidOperationException("MainViewModel.Instance would construct the view model; viewModelLoaded is false");
            }
        }

        private static object Call(object target, Type type, bool isStatic, string segment)
        {
            int open = segment.IndexOf('(');
            string name = segment.Substring(0, open);
            if (!ReadPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
                throw new InvalidOperationException("method '" + name + "' is not a read-style call (allowed prefixes: " + string.Join(", ", ReadPrefixes) + ")");
            var args = SplitArgs(segment.Substring(open + 1, segment.Length - open - 2));
            var flags = Any | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            var candidates = type.GetMethods(flags)
                .Where(m => m.Name == name && !m.IsGenericMethodDefinition && m.GetParameters().Length == args.Count
                            && m.GetParameters().All(pi => !pi.ParameterType.IsByRef && !pi.ParameterType.IsPointer))
                .ToList();
            foreach (var m in candidates)
            {
                var ps = m.GetParameters();
                var converted = new object[args.Count];
                bool ok = true;
                for (int i = 0; i < ps.Length && ok; i++) ok = TryConvert(args[i], ps[i].ParameterType, out converted[i]);
                if (!ok) continue;
                if (m.ReturnType.IsByRef || (m.ReturnType.Name.StartsWith("Span", StringComparison.Ordinal) || m.ReturnType.Name.StartsWith("ReadOnlySpan", StringComparison.Ordinal)))
                    throw new InvalidOperationException("method '" + name + "' returns a ref/Span type that reflection cannot box; use the native mem commands");
                return m.Invoke(isStatic ? null : target, converted);
            }
            throw new ArgumentException("no matching " + (isStatic ? "static " : "") + "method " + name + "/" + args.Count + " on " + type.FullName);
        }

        private static List<string> SplitArgs(string s)
        {
            var list = new List<string>();
            if (s.Trim().Length == 0) return list;
            int depth = 0, start = 0; bool quote = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') quote = !quote;
                else if (!quote && (c == '(' || c == '[')) depth++;
                else if (!quote && (c == ')' || c == ']')) depth--;
                else if (!quote && depth == 0 && c == ',') { list.Add(s.Substring(start, i - start).Trim()); start = i + 1; }
            }
            list.Add(s.Substring(start).Trim());
            return list;
        }

        private static bool TryConvert(string literal, Type target, out object value)
        {
            value = null;
            try
            {
                var underlying = Nullable.GetUnderlyingType(target) ?? target;
                if (literal.Length >= 2 && literal[0] == '"' && literal[literal.Length - 1] == '"')
                {
                    var text = literal.Substring(1, literal.Length - 2);
                    if (underlying == typeof(string)) { value = text; return true; }
                    if (underlying.IsEnum) { value = Enum.Parse(underlying, text, true); return true; }
                    return false;
                }
                if (underlying.IsEnum)
                {
                    value = long.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                        ? Enum.ToObject(underlying, n) : Enum.Parse(underlying, literal, true);
                    return true;
                }
                if (underlying == typeof(bool)) { value = bool.Parse(literal); return true; }
                if (underlying.IsPrimitive || underlying == typeof(decimal))
                {
                    value = Convert.ChangeType(literal, underlying, CultureInfo.InvariantCulture);
                    return true;
                }
                if (underlying == typeof(object)) { value = literal; return true; }
            }
            catch { }
            return false;
        }

        private static object Index(object value, string idx)
        {
            if (value == null) return null;
            if (value is IDictionary dict)
            {
                foreach (DictionaryEntry e in dict)
                    if (string.Equals(Convert.ToString(e.Key, CultureInfo.InvariantCulture), idx.Trim('"'), StringComparison.Ordinal)) return e.Value;
                throw new ArgumentException("key " + idx + " not found");
            }
            int i = int.Parse(idx, CultureInfo.InvariantCulture);
            if (value is IList list) return list[i];
            if (value is IEnumerable en) return en.Cast<object>().ElementAt(i);
            throw new ArgumentException("value of type " + value.GetType().Name + " is not indexable");
        }

        /// <summary>Copies the pointee of T* / NativePointer&lt;T&gt; into a boxed struct (blittable T only).</summary>
        private static unsafe object Deref(object value)
        {
            if (value == null) return null;
            Type pointee;
            ulong address;
            if (value is Pointer p)
            {
                // Pointer.Unbox loses the type; the boxed Pointer keeps it in a private field whose name
                // differs between runtimes (.NET Framework "_ptrType", Mono "type"), so take the Type-typed field.
                var typeField = typeof(Pointer).GetFields(Any | BindingFlags.Instance).FirstOrDefault(f => typeof(Type).IsAssignableFrom(f.FieldType));
                var ptrType = typeField?.GetValue(p) as Type ?? throw new InvalidOperationException("pointer type unavailable");
                pointee = ptrType.GetElementType();
                address = (ulong)Pointer.Unbox(p);
            }
            else if (value.GetType().IsGenericType && value.GetType().Name.StartsWith("NativePointer", StringComparison.Ordinal))
            {
                pointee = value.GetType().GetGenericArguments()[0];
                var inner = value.GetType().GetField("Pointer", Any | BindingFlags.Instance)?.GetValue(value) as Pointer
                            ?? throw new InvalidOperationException("NativePointer.Pointer unavailable");
                address = (ulong)Pointer.Unbox(inner);
            }
            else throw new ArgumentException("'*' needs a pointer, got " + value.GetType().Name);

            if (address == 0) return null;
            int size = Marshal.SizeOf(pointee);
            if (!ProbeCommands.IsReadable(address, (ulong)size)) throw new InvalidOperationException("pointee not readable");
            var boxed = Activator.CreateInstance(pointee);
            var handle = GCHandle.Alloc(boxed, GCHandleType.Pinned); // throws for non-blittable structs
            try
            {
                Buffer.MemoryCopy((void*)address, (void*)handle.AddrOfPinnedObject(), size, size);
            }
            finally { handle.Free(); }
            return boxed;
        }
    }
}
