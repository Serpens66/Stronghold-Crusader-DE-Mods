using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace GameStateProbe
{
    /// <summary>
    /// Converts arbitrary objects into the DOM accepted by Shared.DependencyFreeJson
    /// (Dictionary/List/primitives). Fields are always included; properties only on request,
    /// because getters may have side effects. Depth, collection size and cycles are bounded.
    /// </summary>
    internal sealed class ObjectDumper
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly int maxDepth;
        private readonly int limit;
        private readonly bool includeProperties;
        private readonly HashSet<object> visiting = new HashSet<object>(ReferenceEqualityComparer.Instance);

        public ObjectDumper(int maxDepth, int limit, bool includeProperties)
        {
            this.maxDepth = Math.Max(0, Math.Min(maxDepth, 8));
            this.limit = Math.Max(1, Math.Min(limit, 100000));
            this.includeProperties = includeProperties;
        }

        public object Dump(object value) => Dump(value, 0);

        private object Dump(object value, int depth)
        {
            if (value == null) return null;
            var type = value.GetType();

            // Destroyed Unity objects compare equal to null through the overloaded operator.
            if (value is UnityEngine.Object uo && uo == null) return "<destroyed " + type.Name + ">";
            if (value is string s) return s;
            if (value is bool b) return b;
            if (type.IsEnum) return value.ToString() + " (" + Convert.ToInt64(value, CultureInfo.InvariantCulture) + ")";
            if (value is float f) return (double)f;
            if (value is double d) return d;
            if (value is decimal m) return (double)m;
            if (value is IntPtr ip) return "0x" + ip.ToInt64().ToString("X16");
            if (value is UIntPtr up) return "0x" + up.ToUInt64().ToString("X16");
            if (value is Pointer ptr) unsafe { return "0x" + ((ulong)Pointer.Unbox(ptr)).ToString("X16"); }
            if (type.IsPrimitive) return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            if (value is Type t) return "type " + t.FullName;
            if (value is Delegate del) return "delegate " + del.Method.DeclaringType?.Name + "." + del.Method.Name;

            if (depth >= maxDepth) return "<" + type.Name + ">";
            if (!type.IsValueType && !visiting.Add(value)) return "<cycle " + type.Name + ">";
            try
            {
                if (value is IDictionary dict)
                {
                    var o = new Dictionary<string, object>();
                    int n = 0;
                    foreach (DictionaryEntry e in dict)
                    {
                        if (n++ >= limit) { o["<truncated>"] = dict.Count; break; }
                        o[Convert.ToString(e.Key, CultureInfo.InvariantCulture) ?? "null"] = Dump(e.Value, depth + 1);
                    }
                    return o;
                }
                if (value is IEnumerable en)
                {
                    var list = new List<object>();
                    int n = 0;
                    foreach (var item in en)
                    {
                        if (n++ >= limit) { list.Add("<truncated after " + limit + ">"); break; }
                        list.Add(Dump(item, depth + 1));
                    }
                    return list;
                }

                var obj = new Dictionary<string, object> { ["$type"] = type.FullName };
                for (var ct = type; ct != null && ct != typeof(object) && ct != typeof(ValueType); ct = ct.BaseType)
                {
                    foreach (var field in ct.GetFields(Instance | BindingFlags.DeclaredOnly))
                    {
                        if (obj.ContainsKey(field.Name)) continue;
                        obj[field.Name] = Safe(() => Dump(FieldValue(field, value), depth + 1));
                    }
                    if (!includeProperties) continue;
                    foreach (var prop in ct.GetProperties(Instance | BindingFlags.DeclaredOnly))
                    {
                        if (prop.GetIndexParameters().Length != 0 || obj.ContainsKey(prop.Name) || prop.GetGetMethod(true) == null) continue;
                        obj[prop.Name] = Safe(() => Dump(prop.GetValue(value, null), depth + 1));
                    }
                }
                return obj;
            }
            finally
            {
                if (!type.IsValueType) visiting.Remove(value);
            }
        }

        /// <summary>Fixed buffers appear as a nested struct; expose their elements instead.</summary>
        private static object FieldValue(FieldInfo field, object owner)
        {
            var fixedBuffer = field.GetCustomAttribute<System.Runtime.CompilerServices.FixedBufferAttribute>();
            var raw = field.GetValue(owner);
            if (fixedBuffer == null || raw == null) return raw;
            int size = System.Runtime.InteropServices.Marshal.SizeOf(fixedBuffer.ElementType);
            var bytes = new byte[size * fixedBuffer.Length];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(raw, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { System.Runtime.InteropServices.Marshal.Copy(handle.AddrOfPinnedObject(), bytes, 0, bytes.Length); }
            finally { handle.Free(); }
            var list = new List<object>(fixedBuffer.Length);
            for (int i = 0; i < fixedBuffer.Length; i++)
            {
                long v = 0;
                for (int k = size - 1; k >= 0; k--) v = (v << 8) | bytes[i * size + k];
                list.Add(v);
            }
            return list;
        }

        private static object Safe(Func<object> read)
        {
            try { return read(); }
            catch (TargetInvocationException ex) { return "<error " + ex.InnerException?.GetType().Name + ": " + ex.InnerException?.Message + ">"; }
            catch (Exception ex) { return "<error " + ex.GetType().Name + ": " + ex.Message + ">"; }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
