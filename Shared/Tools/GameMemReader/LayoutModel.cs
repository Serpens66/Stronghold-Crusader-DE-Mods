using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json.Nodes;

namespace GameMemReader;

internal sealed record FieldLayout(string Name, Type Type, int Offset, int Size, int ArrayLength, Type? ElementType, bool LuaExposed);

internal sealed record TypeLayout(Type Type, int Size, IReadOnlyList<FieldLayout> Fields)
{
    public FieldLayout? Field(string name) => Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Native struct layouts taken from the installed Script Extender assembly (metadata only, nothing executed).
/// Offsets are computed from the declared layout and every struct size is cross-checked against the runtime size.
/// </summary>
internal sealed class LayoutModel
{
    private readonly Dictionary<Type, TypeLayout> _cache = new();
    public Assembly Extender { get; }
    public List<string> Warnings { get; } = new();

    public LayoutModel(string extenderDll, IEnumerable<string> probeDirs)
    {
        var dirs = probeDirs.Where(Directory.Exists).ToList();
        var ctx = new AssemblyLoadContext("shcdese-metadata", isCollectible: false);
        ctx.Resolving += (c, name) =>
        {
            foreach (var d in dirs)
            {
                var p = Path.Combine(d, name.Name + ".dll");
                if (File.Exists(p)) return c.LoadFromAssemblyPath(p);
            }
            return null;
        };
        Extender = ctx.LoadFromAssemblyPath(extenderDll);
    }

    public Type? FindType(string name)
    {
        Type[] types;
        try { types = Extender.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
        return types.FirstOrDefault(t => t.FullName == name)
            ?? types.FirstOrDefault(t => t.Name == name && t.IsValueType)
            ?? types.FirstOrDefault(t => t.Name == name);
    }

    public IEnumerable<Type> StructTypes(string? filter)
    {
        Type[] types;
        try { types = Extender.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
        return types.Where(t => t.IsValueType && !t.IsEnum && t.Namespace?.StartsWith("SHCDESE.Interop") == true
                                && !t.Name.Contains('<') && (filter == null || t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(t => t.Name);
    }

    /// <summary>Reads a compile-time constant (also private) from any Extender type.</summary>
    public object? Constant(string typeName, string constName)
    {
        var t = FindType(typeName);
        var f = t?.GetField(constName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        return f?.IsLiteral == true ? f.GetRawConstantValue() : null;
    }

    public TypeLayout Layout(Type t)
    {
        if (_cache.TryGetValue(t, out var cached)) return cached;

        var sl = t.StructLayoutAttribute;
        int pack = sl?.Pack is > 0 ? sl.Pack : 8;
        bool explicitLayout = sl?.Value == LayoutKind.Explicit;
        var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                      .OrderBy(f => f.MetadataToken).ToList();
        var result = new List<FieldLayout>();
        int offset = 0, end = 0, maxAlign = 1;
        foreach (var f in fields)
        {
            int size, align, arrayLength = 0;
            Type? elementType = null;
            var fixedBuf = f.GetCustomAttribute<FixedBufferAttribute>();
            if (fixedBuf != null)
            {
                elementType = fixedBuf.ElementType;
                arrayLength = fixedBuf.Length;
                var es = SizeOf(elementType);
                size = es * arrayLength;
                align = Math.Min(pack, Align(elementType));
            }
            else
            {
                size = SizeOf(f.FieldType);
                align = Math.Min(pack, Align(f.FieldType));
            }
            if (explicitLayout)
                offset = f.GetCustomAttribute<FieldOffsetAttribute>()?.Value ?? 0;
            else
                offset = (offset + align - 1) / align * align;
            maxAlign = Math.Max(maxAlign, align);
            bool lua = f.CustomAttributes.Any(a => a.AttributeType.Name == "LuaExposedAttribute");
            result.Add(new FieldLayout(f.Name, f.FieldType, offset, size, arrayLength, elementType, lua));
            end = Math.Max(end, offset + size);
            if (!explicitLayout) offset += size;
        }
        int computed = (end + maxAlign - 1) / maxAlign * maxAlign;
        int declared = sl?.Size ?? 0;
        int total = Math.Max(computed, declared);

        // Cross-check against the CLR's own size for the same struct (fails loudly on layout mistakes).
        try
        {
            var runtime = (int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!.MakeGenericMethod(t).Invoke(null, null)!;
            if (runtime != total)
                Warnings.Add($"layout {t.Name}: computed size {total} != runtime size {runtime}; offsets may be wrong");
        }
        catch (Exception ex)
        {
            Warnings.Add($"layout {t.Name}: runtime size check unavailable ({ex.GetType().Name})");
        }

        var layout = new TypeLayout(t, total, result);
        _cache[t] = layout;
        return layout;
    }

    public int SizeOf(Type t)
    {
        if (t.IsPointer || t == typeof(IntPtr) || t == typeof(UIntPtr)) return 8;
        if (t.IsEnum) return SizeOf(Enum.GetUnderlyingType(t));
        if (t.IsPrimitive) return t == typeof(bool) ? 1 : t == typeof(char) ? 2 : Marshal.SizeOf(t);
        if (t.IsValueType) return Layout(t).Size;
        return 8; // references never appear in native structs; treat as pointer-sized
    }

    private int Align(Type t)
    {
        if (t.IsPointer || t == typeof(IntPtr) || t == typeof(UIntPtr)) return 8;
        if (t.IsEnum) return Align(Enum.GetUnderlyingType(t));
        if (t.IsPrimitive) return SizeOf(t);
        if (t.IsValueType)
        {
            var l = Layout(t);
            var pack = t.StructLayoutAttribute?.Pack is > 0 ? t.StructLayoutAttribute.Pack : 8;
            return Math.Min(pack, l.Fields.Count == 0 ? 1 : l.Fields.Max(f => Align(f.ElementType ?? f.Type)));
        }
        return 8;
    }

    /// <summary>Decodes a struct from raw bytes into JSON. selector: null = all fields.</summary>
    public JsonObject Decode(TypeLayout layout, ReadOnlySpan<byte> data, Func<FieldLayout, bool>? selector = null)
    {
        var obj = new JsonObject();
        foreach (var f in layout.Fields)
        {
            if (selector != null && !selector(f)) continue;
            if (f.Offset + f.Size > data.Length) break;
            var slice = data.Slice(f.Offset, f.Size);
            if (f.ElementType != null)
            {
                var arr = new JsonArray();
                int es = SizeOf(f.ElementType);
                for (int i = 0; i < f.ArrayLength; i++)
                    arr.Add(DecodeValue(f.ElementType, slice.Slice(i * es, es)));
                obj[f.Name] = arr;
            }
            else
            {
                obj[f.Name] = DecodeValue(f.Type, slice);
            }
        }
        return obj;
    }

    public JsonNode? DecodeValue(Type t, ReadOnlySpan<byte> b)
    {
        if (t.IsPointer || t == typeof(IntPtr) || t == typeof(UIntPtr))
            return "0x" + BitConverter.ToUInt64(b).ToString("X16");
        if (t.IsEnum)
        {
            var raw = ReadInteger(Enum.GetUnderlyingType(t), b);
            var value = Enum.ToObject(t, raw);
            var text = value.ToString()!;
            // Unknown values print as plain numbers; keep the raw number next to the name.
            return long.TryParse(text, out _) ? JsonValue.Create(raw) : JsonValue.Create($"{text} ({raw})");
        }
        if (t == typeof(bool)) return b[0] != 0;
        if (t == typeof(float)) return BitConverter.ToSingle(b);
        if (t == typeof(double)) return BitConverter.ToDouble(b);
        if (t == typeof(char)) return ((char)BitConverter.ToUInt16(b)).ToString();
        if (t.IsPrimitive) return JsonValue.Create(ReadInteger(t, b));
        if (t.IsValueType) return Decode(Layout(t), b);
        return null;
    }

    private static long ReadInteger(Type t, ReadOnlySpan<byte> b) => Type.GetTypeCode(t) switch
    {
        TypeCode.Byte => b[0],
        TypeCode.SByte => (sbyte)b[0],
        TypeCode.Int16 => BitConverter.ToInt16(b),
        TypeCode.UInt16 => BitConverter.ToUInt16(b),
        TypeCode.Int32 => BitConverter.ToInt32(b),
        TypeCode.UInt32 => BitConverter.ToUInt32(b),
        TypeCode.Int64 => BitConverter.ToInt64(b),
        TypeCode.UInt64 => (long)BitConverter.ToUInt64(b),
        _ => 0
    };
}
