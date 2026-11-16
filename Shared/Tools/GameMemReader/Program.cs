using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameMemReader;

/// <summary>
/// Read-only inspector for a running Stronghold Crusader DE process (see Usage()).
/// Works without any mod installed: native globals are resolved from the Extender's AOB patterns,
/// struct layouts come from the installed Extender assembly.
/// </summary>
internal static class Program
{
    private const string DefaultGame = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
    private const string ModuleName = "CrusaderDE.dll";

    private static readonly string[] UnitSummary =
    {
        "r_GlobalId", "r_UnitChimp", "r_AliveState", "r_ControllableForPlayerId", "r_SpawnedForPlayerIndex",
        "r_CurrentTilePositionX", "r_CurrentTilePositionY", "r_CurrentHealthPercentage", "r_AIState", "r_TribeId"
    };

    private static readonly string[] BuildingSummary =
    {
        "r_GlobalId", "r_BuildingType", "r_AliveState", "r_PlayerIdOwner", "r_TilePositionXBegin", "r_TilePositionYBegin"
    };

    private static int Main(string[] args)
    {
        try
        {
            return Run(new Args(args));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{Stamp()} error: {ex.Message}");
            return 2;
        }
    }

    private static string Stamp() => DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static int Run(Args a)
    {
        if (a.Positional.Count == 0 || a.Has("help")) { Usage(); return 0; }
        if (a.Positional[0] is "eval" or "probe" or "ptypes" or "members")
            return ProbeOnly(a);
        var game = a.Get("game") ?? DefaultGame;
        var ctx = Context.Create(game, a.Get("extender-src"));
        foreach (var w in ctx.Layouts.Warnings) Console.Error.WriteLine($"{Stamp()} warning: {w}");

        var cmd = a.Positional[0].ToLowerInvariant();
        int repeat = int.Parse(a.Get("repeat") ?? "1", CultureInfo.InvariantCulture);
        int interval = int.Parse(a.Get("interval") ?? "1000", CultureInfo.InvariantCulture);
        var json = new JsonSerializerOptions { WriteIndented = !a.Has("compact") };

        for (int i = 0; i < repeat; i++)
        {
            if (i > 0) Thread.Sleep(interval);
            JsonNode result = cmd switch
            {
                "globals" => Globals(ctx, a.Positional.ElementAtOrDefault(1)),
                "types" => Types(ctx, a.Positional.ElementAtOrDefault(1)),
                "layout" => LayoutOf(ctx, a.Required(1, "type")),
                _ => WithProcess(ctx, a, p => cmd switch
                {
                    "info" => Info(ctx, p),
                    "status" => Status(ctx, p),
                    "units" => Array(ctx, p, "GameUnit", "GameUnitManagerVA", "GameUnitArray", 10_000, a, UnitSummary),
                    "unit" => One(ctx, p, "GameUnit", "GameUnitManagerVA", "GameUnitArray", 10_000, a),
                    "buildings" => Array(ctx, p, "GameBuilding", "GameBuildingManagerVA", "BuildingsArray", 4_000, a, BuildingSummary),
                    "building" => One(ctx, p, "GameBuilding", "GameBuildingManagerVA", "BuildingsArray", 4_000, a),
                    "players" => Players(ctx, p, a),
                    "struct" => Struct(ctx, p, a),
                    "read" => ReadPrimitive(ctx, p, a),
                    "raw" => Raw(ctx, p, a),
                    "grid" => Grid(ctx, p, a),
                    _ => throw new ArgumentException($"unknown command '{cmd}' (see --help)")
                })
            };
            if (repeat > 1 && result is JsonObject o) o["_sample"] = i;
            if (repeat > 1 && result is JsonObject o2) o2["_time"] = Stamp();
            Console.WriteLine(result.ToJsonString(json));
        }
        return 0;
    }

    private static JsonNode WithProcess(Context ctx, Args a, Func<ProcessMemory, JsonNode> body)
    {
        using var p = ProcessMemory.TryAttach(ModuleName, out var reason)
            ?? throw new InvalidOperationException($"game not attachable: {reason}");
        if (!string.Equals(Path.GetFullPath(p.ModulePath), Path.GetFullPath(ctx.DllPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"running module {p.ModulePath} differs from analysed {ctx.DllPath}; pass --game");
        // --source probe: copy memory inside the game (needs Testmods/GameStateProbe); default: ReadProcessMemory.
        if (string.Equals(a.Get("source"), "probe", StringComparison.OrdinalIgnoreCase))
            p.Probe = new ProbeClient(ProbePort(a), a.Get("context") ?? "auto");
        var result = body(p);
        if (p.Probe != null && result is JsonObject o) o["_source"] = "probe/" + p.Probe.Context;
        return result;
    }

    private static int ProbePort(Args a) => int.Parse(a.Get("port") ?? "47811", CultureInfo.InvariantCulture);

    /// <summary>Commands answered entirely by the in-game probe (managed state); no layouts or process handle needed.</summary>
    private static int ProbeOnly(Args a)
    {
        var json = new JsonSerializerOptions { WriteIndented = !a.Has("compact") };
        using var probe = new ProbeClient(ProbePort(a), a.Get("context") ?? "auto");
        JsonObject request;
        switch (a.Positional[0])
        {
            case "eval":
                request = new JsonObject
                {
                    ["cmd"] = "eval", ["path"] = a.Required(1, "path"),
                    ["depth"] = int.Parse(a.Get("depth") ?? "2", CultureInfo.InvariantCulture),
                    ["limit"] = int.Parse(a.Get("limit") ?? "50", CultureInfo.InvariantCulture),
                    ["props"] = a.Has("props")
                };
                break;
            case "ptypes":
                request = new JsonObject { ["cmd"] = "types", ["filter"] = a.Required(1, "filter") };
                break;
            case "members":
                request = new JsonObject { ["cmd"] = "members", ["type"] = a.Required(1, "type"), ["static"] = !a.Has("instance") };
                break;
            default:
                request = JsonNode.Parse(a.Required(1, "json"))!.AsObject();
                break;
        }

        int samples = int.Parse(a.Get("samples") ?? "0", CultureInfo.InvariantCulture);
        if (samples > 0)
        {
            // Wrap any request into a tick-paced watch on the server side.
            var watch = new JsonObject
            {
                ["cmd"] = "watch", ["request"] = request, ["samples"] = samples,
                ["everyTicks"] = int.Parse(a.Get("every") ?? "1", CultureInfo.InvariantCulture)
            };
            foreach (var line in probe.SendMany(watch, samples))
                Console.WriteLine(line.ToJsonString(json));
            return 0;
        }
        Console.WriteLine(probe.Send(request).ToJsonString(json));
        return 0;
    }

    // ---------------------------------------------------------------- commands without process

    private static JsonNode Globals(Context ctx, string? filter)
    {
        var arr = new JsonArray();
        foreach (var g in ctx.Globals.Where(g => filter == null || g.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            arr.Add(new JsonObject
            {
                ["name"] = g.Name, ["kind"] = g.Kind, ["rva"] = "0x" + g.Rva.ToString("X"),
                ["matches"] = g.MatchCount, ["sourceLine"] = g.SourceLine
            });
        return new JsonObject { ["nativeHash"] = ctx.NativeHash, ["count"] = arr.Count, ["globals"] = arr };
    }

    private static JsonNode Types(Context ctx, string? filter)
    {
        var arr = new JsonArray();
        foreach (var t in ctx.Layouts.StructTypes(filter))
        {
            try { arr.Add(new JsonObject { ["name"] = t.Name, ["size"] = ctx.Layouts.Layout(t).Size }); }
            catch (Exception ex) { arr.Add(new JsonObject { ["name"] = t.Name, ["error"] = ex.Message }); }
        }
        return new JsonObject { ["types"] = arr };
    }

    private static JsonNode LayoutOf(Context ctx, string typeName)
    {
        var t = ctx.Layouts.FindType(typeName) ?? throw new ArgumentException($"type {typeName} not found");
        var l = ctx.Layouts.Layout(t);
        var arr = new JsonArray();
        foreach (var f in l.Fields)
            arr.Add(new JsonObject
            {
                ["offset"] = "0x" + f.Offset.ToString("X4"), ["name"] = f.Name,
                ["type"] = (f.ElementType ?? f.Type).Name + (f.ArrayLength > 0 ? $"[{f.ArrayLength}]" : ""),
                ["size"] = f.Size, ["lua"] = f.LuaExposed
            });
        return new JsonObject { ["type"] = t.FullName, ["size"] = l.Size, ["fields"] = arr };
    }

    // ---------------------------------------------------------------- process commands

    private static JsonNode Info(Context ctx, ProcessMemory p) => new JsonObject
    {
        ["pid"] = p.ProcessId, ["process"] = p.ProcessName, ["moduleBase"] = "0x" + p.ModuleBase.ToString("X16"),
        ["moduleSize"] = p.ModuleSize, ["nativeHash"] = ctx.NativeHash, ["extender"] = ctx.Layouts.Extender.GetName().Version?.ToString(),
        ["globalsResolved"] = ctx.Globals.Count(g => g.Kind is "rip" or "disp"), ["globalsTotal"] = ctx.Globals.Count,
        ["ambiguousGlobals"] = new JsonArray(ctx.Globals.Where(g => g.MatchCount > 1).Select(g => (JsonNode)g.Name).ToArray())
    };

    private static JsonNode Status(Context ctx, ProcessMemory p)
    {
        var o = new JsonObject();
        void Int(string key, string global)
        {
            if (ctx.TryAddress(p, global, out var va)) o[key] = p.Read<int>(va);
        }
        Int("tick", "CurrentGameTickVA");
        Int("paused", "GamePausedVA");
        Int("localPlayerId", "LocalPlayerIdVA");
        Int("isInMapEditor", "IsInMapEditorVA");
        Int("gameTypeMode", "CurrentGameTypeModeVA");
        Int("mapSize", "CurrentMapSizeVA");
        if (ctx.TryAddress(p, "CurrentMapNameVA", out var nameVa) && ctx.TryAddress(p, "CurrentMapNameLengthVA", out var lenVa))
        {
            var len = p.Read<int>(lenVa);
            if (len is > 0 and < 512)
                o["mapName"] = System.Text.Encoding.Latin1.GetString(p.Read(nameVa, len));
        }
        if (ctx.TryAddress(p, "GameUnitManagerVA", out var um))
        {
            o["unitsTotal"] = p.Read<uint>(um + 4);
            o["nextUnitId"] = p.Read<uint>(um);
        }
        if (ctx.TryAddress(p, "GameBuildingManagerVA", out var bm))
            o["activeBuildings"] = p.Read<uint>(bm);
        o["time"] = Stamp();
        return o;
    }

    /// <summary>Generic array of native records; id N lives at arrayField + N * recordSize (id 0 is reserved).</summary>
    private static JsonNode Array(Context ctx, ProcessMemory p, string typeName, string global, string arrayField, int capacity, Args a, string[] summary)
    {
        var (layout, baseVa) = ArrayBase(ctx, p, typeName, global, arrayField);
        var sw = Stopwatch.StartNew();
        var bytes = p.Read(baseVa, layout.Size * capacity);
        var readMs = sw.Elapsed.TotalMilliseconds;
        var selector = FieldSelector(layout, a.Get("fields"), summary);
        var where = ParseWhere(a.GetAll("where"));
        bool all = a.Has("all");
        int limit = int.Parse(a.Get("limit") ?? "100000", CultureInfo.InvariantCulture);
        var stateField = layout.Field("r_AliveState");
        var arr = new JsonArray();
        int matched = 0;
        for (int id = 1; id < capacity; id++)
        {
            var rec = bytes.AsSpan(id * layout.Size, layout.Size);
            if (!all && stateField != null && BitConverter.ToInt16(rec.Slice(stateField.Offset, 2)) == 0) continue;
            if (where.Count > 0)
            {
                var full = ctx.Layouts.Decode(layout, rec, f => where.ContainsKey(f.Name));
                if (!where.All(w => Matches(full[w.Key], w.Value))) continue;
            }
            matched++;
            if (arr.Count >= limit) continue;
            var o = new JsonObject { ["id"] = id };
            foreach (var kv in ctx.Layouts.Decode(layout, rec, selector).ToList())
                o[kv.Key] = kv.Value?.DeepClone();
            arr.Add(o);
        }
        return new JsonObject
        {
            ["type"] = typeName, ["matched"] = matched, ["returned"] = arr.Count,
            ["readMs"] = Math.Round(readMs, 2), ["time"] = Stamp(), ["items"] = arr
        };
    }

    private static JsonNode One(Context ctx, ProcessMemory p, string typeName, string global, string arrayField, int capacity, Args a)
    {
        int id = int.Parse(a.Required(1, "id"), CultureInfo.InvariantCulture);
        if (id <= 0 || id >= capacity) throw new ArgumentException($"id must be 1..{capacity - 1}");
        var (layout, baseVa) = ArrayBase(ctx, p, typeName, global, arrayField);
        var va = baseVa + (ulong)(id * layout.Size);
        var rec = p.Read(va, layout.Size);
        var o = new JsonObject { ["id"] = id, ["address"] = "0x" + va.ToString("X16") };
        foreach (var kv in ctx.Layouts.Decode(layout, rec, FieldSelector(layout, a.Get("fields"), null, luaDefault: !a.Has("all-fields"))).ToList())
            o[kv.Key] = kv.Value?.DeepClone();
        return o;
    }

    private static JsonNode Players(Context ctx, ProcessMemory p, Args a)
    {
        var t = ctx.Layouts.FindType("GamePlayerResources") ?? throw new InvalidOperationException("GamePlayerResources missing");
        var layout = ctx.Layouts.Layout(t);
        // Offset and count are the Extender's own constants (GamePlayerManagerAPI); players are 1-based.
        var offset = Convert.ToUInt64(ctx.Layouts.Constant("GamePlayerManagerAPI", "PLAYER_RESOURCES_OFFSET") ?? throw new InvalidOperationException("PLAYER_RESOURCES_OFFSET missing"));
        var max = Convert.ToInt32(ctx.Layouts.Constant("GamePlayerManagerAPI", "MAX_PLAYERS") ?? 8);
        var baseVa = ctx.Address(p, "GamePlayerManagerVA") + offset;
        var only = a.Positional.Count > 1 ? int.Parse(a.Positional[1], CultureInfo.InvariantCulture) : 0;
        var selector = FieldSelector(layout, a.Get("fields"), null, luaDefault: !a.Has("all-fields"));
        var arr = new JsonArray();
        for (int id = 1; id <= max; id++)
        {
            if (only != 0 && id != only) continue;
            var rec = p.Read(baseVa + (ulong)((id - 1) * layout.Size), layout.Size);
            var o = new JsonObject { ["playerId"] = id };
            foreach (var kv in ctx.Layouts.Decode(layout, rec, selector).ToList())
                o[kv.Key] = kv.Value?.DeepClone();
            arr.Add(o);
        }
        return new JsonObject { ["time"] = Stamp(), ["players"] = arr };
    }

    /// <summary>struct &lt;Type&gt; &lt;address&gt; [--count n] : decode any Extender struct at any address.</summary>
    private static JsonNode Struct(Context ctx, ProcessMemory p, Args a)
    {
        var t = ctx.Layouts.FindType(a.Required(1, "type")) ?? throw new ArgumentException("type not found");
        var layout = ctx.Layouts.Layout(t);
        var va = ctx.Address(p, a.Required(2, "address"));
        int count = int.Parse(a.Get("count") ?? "1", CultureInfo.InvariantCulture);
        var selector = FieldSelector(layout, a.Get("fields"), null, luaDefault: false);
        var arr = new JsonArray();
        for (int i = 0; i < count; i++)
        {
            var rec = p.Read(va + (ulong)(i * layout.Size), layout.Size);
            arr.Add(ctx.Layouts.Decode(layout, rec, selector));
        }
        return new JsonObject { ["type"] = t.Name, ["address"] = "0x" + va.ToString("X16"), ["size"] = layout.Size, ["items"] = arr };
    }

    /// <summary>read &lt;int32|uint16|...|EnumName&gt; &lt;address&gt; [--count n]</summary>
    private static JsonNode ReadPrimitive(Context ctx, ProcessMemory p, Args a)
    {
        var typeName = a.Required(1, "type");
        var t = PrimitiveType(typeName) ?? ctx.Layouts.FindType(typeName) ?? throw new ArgumentException("type not found");
        int size = ctx.Layouts.SizeOf(t);
        var va = ctx.Address(p, a.Required(2, "address"));
        int count = int.Parse(a.Get("count") ?? "1", CultureInfo.InvariantCulture);
        var bytes = p.Read(va, size * count);
        var arr = new JsonArray();
        for (int i = 0; i < count; i++) arr.Add(ctx.Layouts.DecodeValue(t, bytes.AsSpan(i * size, size)));
        return new JsonObject { ["address"] = "0x" + va.ToString("X16"), ["type"] = t.Name, ["values"] = arr };
    }

    private static JsonNode Raw(Context ctx, ProcessMemory p, Args a)
    {
        var va = ctx.Address(p, a.Required(1, "address"));
        int len = int.Parse(a.Required(2, "length"), CultureInfo.InvariantCulture);
        var bytes = p.Read(va, len);
        var lines = new JsonArray();
        for (int i = 0; i < bytes.Length; i += 16)
            lines.Add($"{va + (ulong)i:X16}  {Convert.ToHexString(bytes, i, Math.Min(16, bytes.Length - i))}");
        return new JsonObject { ["address"] = "0x" + va.ToString("X16"), ["length"] = len, ["hex"] = lines };
    }

    /// <summary>
    /// grid &lt;Name&gt; &lt;x&gt; &lt;y&gt; [w h] : reads a packed tile grid of GameTileManagerView.
    /// Grid offset and element type are taken from the view (const NameOffset + property Span&lt;T&gt; Name);
    /// tile index = x + MapRowLookupTable[3*y] exactly like GameTileManagerAPI.
    /// </summary>
    private static JsonNode Grid(Context ctx, ProcessMemory p, Args a)
    {
        var name = a.Required(1, "grid");
        int x = int.Parse(a.Required(2, "x"), CultureInfo.InvariantCulture);
        int y = int.Parse(a.Required(3, "y"), CultureInfo.InvariantCulture);
        int w = a.Positional.Count > 4 ? int.Parse(a.Positional[4], CultureInfo.InvariantCulture) : 1;
        int h = a.Positional.Count > 5 ? int.Parse(a.Positional[5], CultureInfo.InvariantCulture) : 1;
        var view = ctx.Layouts.FindType("GameTileManagerView") ?? throw new InvalidOperationException("GameTileManagerView missing");
        var prop = view.GetProperties().FirstOrDefault(pr => string.Equals(pr.Name, name, StringComparison.OrdinalIgnoreCase)
                                                          && pr.PropertyType.IsGenericType && pr.PropertyType.Name.StartsWith("Span"))
                   ?? throw new ArgumentException($"no Span grid property '{name}' on GameTileManagerView");
        var elem = prop.PropertyType.GetGenericArguments()[0];
        var offset = Convert.ToUInt64(ctx.Layouts.Constant("GameTileManagerView", prop.Name + "Offset")
                                      ?? throw new ArgumentException($"no const {prop.Name}Offset"));
        int es = ctx.Layouts.SizeOf(elem);
        var gridVa = ctx.Address(p, "GameTileManagerVA") + offset;
        var rowTable = ctx.Address(p, "MapRowLookupTableRVA");
        var rows = new JsonArray();
        for (int yy = y; yy < y + h; yy++)
        {
            int rowStart = p.Read<int>(rowTable + (ulong)(3 * yy * 4));
            var row = new JsonArray();
            var bytes = p.Read(gridVa + (ulong)((x + rowStart) * es), w * es);
            for (int i = 0; i < w; i++) row.Add(ctx.Layouts.DecodeValue(elem, bytes.AsSpan(i * es, es)));
            rows.Add(new JsonObject { ["y"] = yy, ["rowStartIndex"] = rowStart, ["values"] = row });
        }
        return new JsonObject { ["grid"] = prop.Name, ["elementType"] = elem.Name, ["x"] = x, ["rows"] = rows };
    }

    // ---------------------------------------------------------------- helpers

    private static (TypeLayout Layout, ulong ArrayVa) ArrayBase(Context ctx, ProcessMemory p, string typeName, string global, string arrayField)
    {
        var t = ctx.Layouts.FindType(typeName) ?? throw new InvalidOperationException($"{typeName} missing");
        var layout = ctx.Layouts.Layout(t);
        var managerType = ctx.Layouts.FindType(global.Replace("VA", "")) ?? throw new InvalidOperationException($"manager type for {global} missing");
        var field = ctx.Layouts.Layout(managerType).Field(arrayField) ?? throw new InvalidOperationException($"{arrayField} missing");
        return (layout, ctx.Address(p, global) + (ulong)field.Offset);
    }

    private static Func<FieldLayout, bool>? FieldSelector(TypeLayout layout, string? fields, string[]? summary, bool luaDefault = false)
    {
        if (fields == "*") return null;
        if (fields != null)
        {
            var set = fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return f => set.Contains(f.Name);
        }
        if (summary != null)
        {
            var set = summary.ToHashSet();
            return f => set.Contains(f.Name);
        }
        return luaDefault ? f => f.LuaExposed : null;
    }

    private static Dictionary<string, string> ParseWhere(IEnumerable<string> clauses) =>
        clauses.Select(c => c.Split('=', 2)).Where(p => p.Length == 2)
               .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Matches a decoded value by number or by enum name ("IsAlive" matches "IsAlive (2)").</summary>
    private static bool Matches(JsonNode? node, string expected)
    {
        if (node == null) return false;
        var text = node is JsonValue v && v.TryGetValue<string>(out var s) ? s : node.ToJsonString();
        if (string.Equals(text, expected, StringComparison.OrdinalIgnoreCase)) return true;
        var paren = text.LastIndexOf('(');
        if (paren > 0)
        {
            var namePart = text[..paren].Trim();
            var numPart = text[(paren + 1)..].TrimEnd(')');
            return string.Equals(namePart, expected, StringComparison.OrdinalIgnoreCase) || numPart == expected;
        }
        return false;
    }

    private static Type? PrimitiveType(string name) => name.ToLowerInvariant() switch
    {
        "byte" or "uint8" => typeof(byte), "sbyte" or "int8" => typeof(sbyte),
        "int16" or "short" => typeof(short), "uint16" or "ushort" => typeof(ushort),
        "int32" or "int" => typeof(int), "uint32" or "uint" => typeof(uint),
        "int64" or "long" => typeof(long), "uint64" or "ulong" => typeof(ulong),
        "float" => typeof(float), "double" => typeof(double), "ptr" or "pointer" => typeof(IntPtr),
        _ => null
    };

    private static void Usage()
    {
        Console.WriteLine("""
            GameMemReader - read-only inspector for a running SHCDE process (JSON output)

              info                          process, module base, native hash, resolved globals
              status                        tick, pause, local player, editor, map, unit/building counts
              units  [--all] [--where f=v]... [--fields a,b|*] [--limit n]
              unit <id> [--all-fields | --fields a,b]
              buildings / building <id>     same options as units/unit
              players [id] [--all-fields | --fields a,b]
              struct <Type> <addr> [--count n] [--fields a,b]
              read <int32|uint16|...|Enum> <addr> [--count n]
              raw <addr> <length>
              grid <GridName> <x> <y> [w h] e.g. grid TileUnitIdGrid 100 100 5 5
              globals [filter] / types [filter] / layout <Type>     (no running game needed)

              <addr>: 0x7FF...(absolute) | GlobalName | GlobalName+0x10
              common: --repeat n --interval ms --compact --game <dir> --extender-src <GameGlobalsManager.cs>
                      --source probe [--context auto|tick|main] [--port 47811]
                          read memory through Testmods/GameStateProbe (copied inside the game; tick = between sim ticks)

            In-game probe only (needs Testmods/GameStateProbe installed):
              eval "<Type>::<Member>.<Member>[i].GetX(1)" [--depth n] [--limit n] [--props]
              ptypes <filter>               loaded managed types
              members <Type> [--instance]   fields/properties/methods
              probe '<json request>'        raw request, e.g. '{"cmd":"ping"}' or '{"cmd":"quit"}'
              --samples n [--every k]       repeat eval/probe every k simulation ticks (server-side watch)
            """);
    }
}

/// <summary>Everything that does not depend on the live process (cached across repeats).</summary>
internal sealed class Context
{
    public required string DllPath { get; init; }
    public required string NativeHash { get; init; }
    public required List<ResolvedGlobal> Globals { get; init; }
    public required LayoutModel Layouts { get; init; }

    public static Context Create(string game, string? extenderSrc)
    {
        var dll = Path.Combine(game, "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
        var plugin = Path.Combine(game, "BepInEx", "plugins", "000shcdese");
        extenderSrc ??= FindExtenderSource() ?? throw new FileNotFoundException("GameGlobalsManager.cs not found; pass --extender-src");
        var cache = Path.Combine(AppContext.BaseDirectory, "cache");
        var globals = GlobalResolver.Resolve(dll, extenderSrc, cache, out var hash);
        var layouts = new LayoutModel(Path.Combine(plugin, "SHCDESE.dll"), new[]
        {
            plugin, Path.Combine(game, "BepInEx", "core"),
            Path.Combine(game, "Stronghold Crusader Definitive Edition_Data", "Managed")
        });
        return new Context { DllPath = dll, NativeHash = hash, Globals = globals, Layouts = layouts };
    }

    private static string? FindExtenderSource()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "shcde-script-extender", "src", "SHCDESE.BepInEx", "GameGlobals", "GameGlobalsManager.cs");
            if (File.Exists(p)) return p;
        }
        return null;
    }

    public bool TryAddress(ProcessMemory p, string expr, out ulong va)
    {
        try { va = Address(p, expr); return true; }
        catch { va = 0; return false; }
    }

    /// <summary>0xABS | Global | Global+0xOFF. Globals of kind "disp" are taken as module-relative (Extender convention for *RVA tables).</summary>
    public ulong Address(ProcessMemory p, string expr)
    {
        var parts = expr.Split('+', 2);
        ulong baseVa;
        if (parts[0].StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            baseVa = ulong.Parse(parts[0][2..], NumberStyles.HexNumber);
        else
        {
            var g = Globals.FirstOrDefault(x => string.Equals(x.Name, parts[0], StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException($"unknown global '{parts[0]}'");
            if (g.Kind is not ("rip" or "disp")) throw new InvalidOperationException($"global {g.Name} unresolved ({g.Kind})");
            baseVa = p.ModuleBase + g.Rva;
        }
        if (parts.Length == 2)
        {
            var off = parts[1].Trim();
            baseVa += off.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? ulong.Parse(off[2..], NumberStyles.HexNumber)
                : ulong.Parse(off, CultureInfo.InvariantCulture);
        }
        return baseVa;
    }
}

internal sealed class Args
{
    private readonly Dictionary<string, List<string>> _opts = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Positional { get; } = new();

    public Args(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                var key = args[i][2..];
                var value = i + 1 < args.Length && !args[i + 1].StartsWith("--") && IsValued(key) ? args[++i] : "";
                if (!_opts.TryGetValue(key, out var list)) _opts[key] = list = new List<string>();
                list.Add(value);
            }
            else Positional.Add(args[i]);
        }
    }

    private static bool IsValued(string key) => key is not ("all" or "all-fields" or "compact" or "help" or "props" or "instance");
    public bool Has(string key) => _opts.ContainsKey(key);
    public string? Get(string key) => _opts.TryGetValue(key, out var l) ? l[^1] : null;
    public IEnumerable<string> GetAll(string key) => _opts.TryGetValue(key, out var l) ? l : Enumerable.Empty<string>();
    public string Required(int index, string what) => Positional.ElementAtOrDefault(index) ?? throw new ArgumentException($"missing <{what}>");
}
