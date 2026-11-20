using ImprovedHunters;
using Iced.Intel;
using RedBird.X64.Hooks;
using SHCDESE.Interop;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

static class Program
{
    private const string CanonicalNativePath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
    private static int assertions;

    public static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            Environment.ExitCode = 1;
        }
    }

    private static void Run(string[] args)
    {
        TestInactivePreyCatalog();
        Assert(HunterInactivePreyPolicy.IsOwnAdmission("hunters", "rabbit", "hunters", "rabbit"), "frozen own admission stays protected after gate closure");
        Assert(!HunterInactivePreyPolicy.IsOwnAdmission("hunters", "rabbit", "foreign", "rabbit"), "same-type foreign admission stays outside the closed gate");
        Assert(!HunterInactivePreyPolicy.IsOwnAdmission("hunters", "rabbit", "hunters", "other"), "another registration is not the frozen own admission");
        Assert(!HunterInactivePreyPolicy.IsOwnAdmission("hunters", "rabbit", null, null), "no admission grants no veto");
        Assert(!HunterInactivePreyPolicy.IsOwnAdmission("hunters", "rabbit", "Hunters", "rabbit"), "owner identities remain ordinal");
        TestPopulationClamp();
        TestMigrationContracts();
        TestNativePatterns();
        NativeStubExecutionTests.Run(MapPeImage(File.ReadAllBytes(CanonicalNativePath), out _));
        Console.WriteLine($"ImprovedHunters chicken policy tests passed: assertions={assertions}.");
    }

    private static void TestInactivePreyCatalog()
    {
        int preyType = (int)eChimps.CHIMP_TYPE_RABBIT;
        // Exercise the real APIShared catalog arbitration, not a replica of its winner selection.
        var registrationType = typeof(APIShared.Wildlife.HuntablePreyRegistration);
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var builder = registrationType.Assembly.GetType("APIShared.Wildlife.HuntablePreyCatalogBuilder").GetMethod("Compile", flags);
        APIShared.Wildlife.HuntablePreyRegistration Register(string owner, string id, int priority) =>
            (APIShared.Wildlife.HuntablePreyRegistration)Activator.CreateInstance(registrationType,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                new object[] { owner, id, new APIShared.Wildlife.HuntablePreyDefinition(preyType), priority, "" }, null);
        APIShared.Wildlife.HuntablePreyCatalog Compile(params APIShared.Wildlife.HuntablePreyRegistration[] entries) =>
            (APIShared.Wildlife.HuntablePreyCatalog)builder.Invoke(null, new object[] { entries });
        var own = Register("hunters", "rabbit", 0);
        var foreign = Register("foreign", "rabbit", 1);
        var frozen = Compile(own);
        Assert(HunterInactivePreyPolicy.ShouldRefuse(preyType, own, frozen), "closed gate refuses its own frozen admission");
        var setEnabled = registrationType.GetMethod("SetEnabledFlag", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        setEnabled.Invoke(own, new object[] { false });
        Assert(HunterInactivePreyPolicy.ShouldRefuse(preyType, own, frozen), "logical disable does not change an existing frozen admission");
        var catalog = Compile(own, foreign);
        Assert(catalog.IsHuntable(preyType), "foreign registration still admits the prey type");
        Assert(!HunterInactivePreyPolicy.ShouldRefuse(preyType, own, catalog), "inactive owner cannot veto the foreign catalog winner");
        Assert(!HunterInactivePreyPolicy.ShouldRefuse(preyType, own, Compile(own)), "disabled own registration does not admit prey in the next mission");
        setEnabled.Invoke(own, new object[] { true });
        Assert(!HunterInactivePreyPolicy.ShouldRefuse(preyType, own, Compile(own, foreign)), "mode-closed owner also leaves a higher-priority foreign admission alone");
        Assert(!HunterInactivePreyPolicy.ShouldRefuse(preyType, null, frozen), "unknown own type grants no veto");
    }

    private static void TestMigrationContracts()
    {
        string root = FindWorkspaceRoot();
        string sourceRoot = Path.Combine(root, "ImprovedHunters", "src");
        string allSource = string.Join("\n", Directory.GetFiles(sourceRoot, "*.cs")
            .Select(File.ReadAllText));
        string runtime = File.ReadAllText(Path.Combine(sourceRoot, "ImprovedHuntersRuntime.cs"));
        string infrastructure = File.ReadAllText(Path.Combine(sourceRoot, "HunterHookInfrastructure.cs"));
        string project = File.ReadAllText(Path.Combine(root, "ImprovedHunters", "ImprovedHunters.csproj"));

        Assert(!allSource.Contains("Zhuqiaomon", StringComparison.Ordinal) &&
            !allSource.Contains("HookRef<", StringComparison.Ordinal) &&
            !allSource.Contains(".Unload()", StringComparison.Ordinal),
            "A legacy Zhuqiaomon hook or teardown contract remains.");
        // 1.2.0 removed the two hooks that steered a jump through Rflags (attack gate, State-1 query result)
        // and added the State-0 MoveHere destination hook for firing positions. Since 1.2.0 APIShared owns
        // the Hunter query identity (the issue-123 capture hook and its transaction are gone), own-player
        // chickens and the reservation release (their PermanentNativeGate stubs and eight patterns are gone).
        // APIShared.Sight now owns the Hunter's hut dispatch reader (PermanentDispatchIndexOverride is gone).
        // APIShared.Wildlife.ChickenPopulationModifiers now owns the chicken target compare (GranaryChickenLimitPatch is gone).
        Assert(Count(allSource, "new HookHandle<X64InlineHook>()") == 12,
            "ImprovedHunters must own exactly twelve typed inline-hook handle declarations.");
        // Every transaction commits through HunterHookInfrastructure.Commit, which claims its native-site group first.
        Assert(Count(allSource, "CommitResult commitResult = HunterHookInfrastructure.Commit(transaction, ") == 5 &&
            Count(allSource, "commitResult.IsCompleteSuccess") == 5,
            "Every one of the five atomic hook transactions must check aggregate success.");
        Assert(infrastructure.Contains("HookTarget.FromAddress(address)", StringComparison.Ordinal) &&
            infrastructure.Contains("new ContextHookOptions", StringComparison.Ordinal) &&
            infrastructure.Contains("OwnsHooks = true", StringComparison.Ordinal),
            "The common RedBird target/options/feature ownership contract is incomplete.");
        // Corpse lifetimes of rabbits, camels and chickens belong to their APIShared HuntablePrey registrations;
        // this mod writes no instruction immediate itself.
        Assert(!allSource.Contains("ManagedAssemblyImmediate", StringComparison.Ordinal) &&
            Count(allSource, "WithMinimumCorpseLifetime(") == 3,
            "Rabbit, camel and chicken corpse lifetimes must come only from their HuntablePrey registrations.");
        Assert(project.Contains("RedBird.Abstractions.dll", StringComparison.Ordinal) &&
            project.Contains("RedBird.Core.dll", StringComparison.Ordinal) &&
            project.Contains("RedBird.X64.dll", StringComparison.Ordinal) &&
            !project.Contains("Zhuqiaomon.dll", StringComparison.Ordinal),
            "ImprovedHunters project references do not match the RedBird migration.");
    }

    private static void TestNativePatterns()
    {
        const string nativePath = CanonicalNativePath;
        const string expectedHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        byte[] file = File.ReadAllBytes(nativePath);
        Assert(Convert.ToHexString(SHA256.HashData(file)) == expectedHash,
            "The canonical native DLL changed; ImprovedHunters patterns require a fresh audit.");
        byte[] image = MapPeImage(file, out int sizeOfHeaders);

        string sourceRoot = Path.Combine(FindWorkspaceRoot(), "ImprovedHunters", "src");
        var definitions = new List<(string Name, byte?[] Bytes, int ExpectedRva)>();
        var uniquelyResolvedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in Directory.GetFiles(sourceRoot, "*.cs"))
        {
            string source = File.ReadAllText(path);
            foreach (Match call in Regex.Matches(
                source,
                @"ResolveUnique\(\s*memory,\s*(?<name>[A-Za-z0-9_]*Pattern)",
                RegexOptions.Singleline | RegexOptions.CultureInvariant))
            {
                uniquelyResolvedNames.Add(call.Groups["name"].Value);
            }
            foreach (Match definition in Regex.Matches(
                source,
                @"const\s+string\s+(?<name>[A-Za-z0-9_]*Pattern)\s*=\s*(?<value>.*?);",
                RegexOptions.Singleline | RegexOptions.CultureInvariant))
            {
                string value = string.Concat(Regex.Matches(definition.Groups["value"].Value, "\"([^\"]*)\"")
                    .Select(match => match.Groups[1].Value));
                byte?[] bytes = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Select(token => token is "?" or "??" ? (byte?)null : Convert.ToByte(token, 16))
                    .ToArray();
                string patternName = definition.Groups["name"].Value;
                string rvaName = patternName[..^"Pattern".Length] + "Rva";
                Match rva = Regex.Match(
                    source,
                    $@"const\s+int\s+{Regex.Escape(rvaName)}\s*=\s*0x(?<rva>[0-9A-Fa-f]+)",
                    RegexOptions.CultureInvariant);
                definitions.Add(($"{Path.GetFileName(path)}:{patternName}", bytes,
                    rva.Success ? Convert.ToInt32(rva.Groups["rva"].Value, 16) : -1));
            }
        }

        // Since 1.2.0 APIShared's Herds service owns the herd route builder, the deer and goat threat reads and the
        // herd records, so their four patterns (route, two flight sites, herd manager tick) left this mod.
        // APIShared.Sight now owns the sight wrapper/core calls (six patterns) and the Hunter's hut height dispatch
        // (two patterns, one of them uniquely resolved), so those eight left this mod as well.
        // APIShared now owns the camel and chicken corpse constants, the chicken target compare and the build-menu
        // availability query (four uniquely resolved patterns).
        Assert(definitions.Count == 25, $"Expected 25 native pattern definitions, found {definitions.Count}.");
        Assert(uniquelyResolvedNames.Count == 24,
            $"Expected 24 uniquely resolved pattern contracts, found {uniquelyResolvedNames.Count}.");
        int uniqueFallbackPatterns = 0;
        int ambiguousReferencePatterns = 0;
        foreach ((string name, byte?[] bytes, int expectedRva) in definitions)
        {
            List<int> matches = FindPattern(image, bytes);
            string patternName = name[(name.IndexOf(':') + 1)..];
            Assert(matches.Count > 0,
                $"Native pattern {name} has no match in the canonical mapped PE image.");
            if (expectedRva >= 0)
            {
                Assert(matches.Contains(expectedRva),
                    $"Native pattern {name} does not match its audited RVA 0x{expectedRva:X}; matches={string.Join(",", matches.Select(value => $"0x{value:X}"))}.");
            }
            Assert(matches.All(match => match >= sizeOfHeaders && match <= image.Length - bytes.Length),
                $"Native pattern {name} resolved outside the mapped executable sections.");
            if (uniquelyResolvedNames.Contains(patternName))
            {
                if (matches.Count == 1)
                    uniqueFallbackPatterns++;
                else
                    ambiguousReferencePatterns++;
            }
        }
        int resolvedDefinitionCount = definitions.Count(definition =>
            uniquelyResolvedNames.Contains(definition.Name[(definition.Name.IndexOf(':') + 1)..]));
        Assert(uniqueFallbackPatterns + ambiguousReferencePatterns == resolvedDefinitionCount,
            "Not every ResolveUnique contract was classified.");
        string resolver = File.ReadAllText(Path.Combine(FindWorkspaceRoot(), "Shared", "Runtime", "Native", "NativePatternResolver.cs"));
        Assert(resolver.Contains("FindUniquePattern(memory, bytes, name, searchScope)", StringComparison.Ordinal),
            "Ambiguous non-reference pattern fallbacks are not guarded fail-closed.");
        Console.WriteLine($"ImprovedHunters native patterns verified: definitions={definitions.Count}, uniqueFallback={uniqueFallbackPatterns}, ambiguousReferenceOnly={ambiguousReferencePatterns}.");
    }

    private static byte[] MapPeImage(byte[] file, out int sizeOfHeaders)
    {
        Assert(file.Length >= 0x100 && file[0] == (byte)'M' && file[1] == (byte)'Z',
            "Canonical native input is not a PE file.");
        int peOffset = BitConverter.ToInt32(file, 0x3C);
        Assert(BitConverter.ToUInt32(file, peOffset) == 0x00004550, "PE signature is invalid.");
        ushort sectionCount = BitConverter.ToUInt16(file, peOffset + 6);
        ushort optionalHeaderSize = BitConverter.ToUInt16(file, peOffset + 20);
        int optionalHeader = peOffset + 24;
        int sizeOfImage = checked((int)BitConverter.ToUInt32(file, optionalHeader + 56));
        sizeOfHeaders = checked((int)BitConverter.ToUInt32(file, optionalHeader + 60));
        byte[] image = new byte[sizeOfImage];
        Buffer.BlockCopy(file, 0, image, 0, Math.Min(sizeOfHeaders, file.Length));
        int sectionTable = optionalHeader + optionalHeaderSize;
        for (int index = 0; index < sectionCount; index++)
        {
            int section = sectionTable + index * 40;
            int virtualAddress = checked((int)BitConverter.ToUInt32(file, section + 12));
            int rawSize = checked((int)BitConverter.ToUInt32(file, section + 16));
            int rawOffset = checked((int)BitConverter.ToUInt32(file, section + 20));
            int copyLength = Math.Min(rawSize, Math.Min(file.Length - rawOffset, image.Length - virtualAddress));
            if (copyLength > 0)
                Buffer.BlockCopy(file, rawOffset, image, virtualAddress, copyLength);
        }
        return image;
    }

    private static List<int> FindPattern(byte[] image, byte?[] pattern)
    {
        var matches = new List<int>();
        for (int offset = 0; offset <= image.Length - pattern.Length; offset++)
        {
            int index = 0;
            while (index < pattern.Length &&
                (!pattern[index].HasValue || pattern[index].GetValueOrDefault() == image[offset + index]))
                index++;
            if (index == pattern.Length)
                matches.Add(offset);
        }
        return matches;
    }

    private static string FindWorkspaceRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "ImprovedHunters")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Workspace root was not found.");
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }


    private static void TestPopulationClamp()
    {
        Assert(GranaryChickenSpawnPolicy.ClampTarget(-1) == 0, "Population minimum clamp failed.");
        Assert(GranaryChickenSpawnPolicy.ClampTarget(101) == 100, "Population maximum clamp failed.");
        Assert(GranaryChickenSpawnPolicy.ClampTarget(10) == 10, "Population value changed unexpectedly.");
        Assert(GranaryChickenSpawnPolicy.DefaultTargetPerPlayer == 10, "The population default changed.");
        Assert(TannerMeatPolicy.DefaultMeatPerTrip == 2, "The Tanner default must be two meat per trip.");
        TestChickenRespawnInterval();
        TestWildlifeRange();
        TestWildlifeOrigins();
        TestHerdSettings();
        Assert(HunterSearchRangePolicy.Clamp(0) == 0 && HunterSearchRangePolicy.Clamp(-3) == 0 && HunterSearchRangePolicy.Clamp(5) == 21 &&
            HunterSearchRangePolicy.Clamp(40) == 40 && HunterSearchRangePolicy.Clamp(999) == 200, "The search range clamp changed.");
        Assert(HunterSearchRangePolicy.IsWithin(500, 0) && HunterSearchRangePolicy.IsWithin(30, 30) && !HunterSearchRangePolicy.IsWithin(31, 30),
            "The search range rule changed.");
        Assert(TannerMeatPolicy.ClampMeatPerTrip(0) == 1 && TannerMeatPolicy.ClampMeatPerTrip(51) == 50 && TannerMeatPolicy.ClampMeatPerTrip(50) == 50, "The meat per trip clamp failed.");
        Assert(TannerMeatPolicy.MeatRunsFromSliderValue(3) == 0 && TannerMeatPolicy.MeatRunsFromSliderValue(0) == 3 &&
            TannerMeatPolicy.SliderValueFromMeatRuns(1) == 2, "The slider must run from only meat on the left to no meat on the right.");
    }

    private static void TestChickenRespawnInterval()
    {
        var policy = new GranaryChickenSpawnPolicy(8);
        const int interval = 30 * 40;
        Assert(GranaryChickenSpawnPolicy.DefaultRespawnSeconds == 30, "The default chicken interval must be thirty seconds.");
        Assert(policy.TargetForDecision(1, 10, 2, 180, interval) == 10, "First spawn attempt must be available.");
        Assert(policy.TargetForDecision(1, 10, 3, 380, interval) == 3, "Cooldown must suppress spawn without removing chickens.");
        Assert(policy.TargetForDecision(2, 10, 0, 380, interval) == 10, "Players must have independent cooldowns.");
        Assert(policy.TargetForDecision(1, 10, 1, 1379, interval) == 1, "Losing chickens must not reset the cooldown.");
        Assert(policy.TargetForDecision(1, 10, 1, 1380, interval) == 10, "A spawn is allowed at the deadline.");
        Assert(policy.TargetForDecision(1, 0, 4, 1381, interval) == 0, "A zero target must still remove surplus.");
        Assert(policy.TargetForDecision(1, 10, 1, 1381, 0) == 10, "Zero seconds must use Vanilla's cadence.");
        policy.Reset(fromSave: true);
        Assert(policy.TargetForDecision(1, 10, 10, 2000, interval) == 10, "Loading must preserve a full population.");
        Assert(policy.TargetForDecision(1, 10, 9, 3199, interval) == 9, "Reloading cannot bypass the interval.");
        Assert(policy.TargetForDecision(1, 10, 9, 3200, interval) == 10, "Spawn must resume after the load delay.");
        policy.Reset(fromSave: false);
        Assert(policy.TargetForDecision(1, 10, 0, 1, interval) == 10, "A fresh match must clear the prior cooldown.");
    }

    private static void TestWildlifeRange()
    {
        Assert(WildlifeRangePolicy.IsInside(450, 350, 400, 400, 100) && !WildlifeRangePolicy.IsInside(451, 400, 400, 400, 100) && !WildlifeRangePolicy.IsInside(400, 349, 400, 400, 100),
            "The area must reach exactly half its size from the start place.");
        short[] route = { 410, 400, 430, 400, 451, 400, 420, 400 };
        Assert(WildlifeRangePolicy.CountBeforeLeaving(route, 2, 400, 400, 100) == 2, "A route inside the area must stay whole.");
        Assert(WildlifeRangePolicy.CountBeforeLeaving(route, 4, 400, 400, 100) == 2, "A route must end before its first waypoint outside, also when a later one is inside again.");
        Assert(WildlifeRangePolicy.CountBeforeLeaving(new short[] { 460, 400 }, 1, 400, 400, 100) == 0, "A route leaving at once must keep nothing.");
        Assert(WildlifeRangePolicy.CountBeforeLeaving(route, 0, 400, 400, 100) == 0, "An empty route must stay empty.");
    }

    private static void TestHerdSettings()
    {
        Assert(HerdSettingsPolicy.ClampReturnSeconds(0) == 0 && HerdSettingsPolicy.ClampReturnSeconds(-5) == 0 && HerdSettingsPolicy.ClampReturnSeconds(1) == 30 &&
            HerdSettingsPolicy.ClampReturnSeconds(600) == 600 && HerdSettingsPolicy.ClampReturnSeconds(9999) == 1800, "The return time must keep 0 as off and stay within its range.");
        Assert(HerdSettingsPolicy.ClampRangeSize(0) == 0 && HerdSettingsPolicy.ClampRangeSize(10) == 40 && HerdSettingsPolicy.ClampRangeSize(100) == 100 && HerdSettingsPolicy.ClampRangeSize(999) == 400,
            "The area size must keep 0 as no limit and stay within its range.");
        Assert(HerdSettingsPolicy.ClampStartHerdMode(-1) == 0 && HerdSettingsPolicy.ClampStartHerdMode(2) == 2 && HerdSettingsPolicy.ClampStartHerdMode(9) == 2, "The start herd mode must be one of its three values.");
        Assert(!HerdSettingsPolicy.WantsStartHerds(0, false) && !HerdSettingsPolicy.WantsStartHerds(0, true) && HerdSettingsPolicy.WantsStartHerds(1, false) && !HerdSettingsPolicy.WantsStartHerds(1, true) &&
            HerdSettingsPolicy.WantsStartHerds(2, false) && HerdSettingsPolicy.WantsStartHerds(2, true), "Start herds must follow the mode and whether the map has herds.");

        var places = WildlifeStartPlaces.Around(100, 400, 400, 400).ToList();
        Assert(places[0] == (155, 400), "The first place must lie towards the middle of the map.");
        Assert(places.Distinct().Count() == places.Count, "Every place may be tried once.");
        Assert(places.All(p => Math.Max(Math.Abs(p.X - 100), Math.Abs(p.Y - 400)) >= WildlifeStartPlaces.NearestDistance),
            "A start herd must stay outside the 100 x 100 tiles of a castle layout.");
        Assert(places.All(p => Math.Max(Math.Abs(p.X - 100), Math.Abs(p.Y - 400)) <= 70), "A start herd must stay near its keep.");
        Assert(places.SequenceEqual(WildlifeStartPlaces.Around(100, 400, 400, 400)), "All peers must try the same places.");
    }

    private static void TestWildlifeOrigins()
    {
        var table = new WildlifeOriginTable();
        Assert(!table.IsCaptured && !table.TryFind(5, 50, out _), "A new table knows no herd.");
        var deer = new WildlifeOrigin { IsDeer = true, Respawns = true, X = 400, Y = 300, Herd = 5, HerdSerial = 50, ExtinctSinceTick = WildlifeOrigin.NotExtinct };
        Assert(table.TryAdd(deer) && table.TryFind(5, 50, out WildlifeOrigin found) && found.X == 400 && found.Y == 300, "An added herd must be found.");
        Assert(!table.TryFind(5, 51, out _), "A record reused for a new herd must not count as the old one.");
        Assert(table.TryAdd(new WildlifeOrigin { X = 100, Y = 200, Herd = 5, HerdSerial = 51, ExtinctSinceTick = 1234 }) && table.TryFind(5, 51, out _) && table.Count == 2 && table[0].HerdSerial == 50,
            "A new herd on a reused record must leave the old start place in the table.");
        Assert(!table.TryAdd(new WildlifeOrigin { X = 1, Y = 1, Herd = 0 }) && !table.TryAdd(new WildlifeOrigin { X = 800, Y = 1, Herd = 6 }) && !table.TryAdd(new WildlifeOrigin { X = 1, Y = 1, Herd = WildlifeOriginTable.HerdCapacity }),
            "Entries outside the game's herds or grid must be refused.");
        Assert(table.TryMarkRespawning(5, 51) && table.TryFind(5, 51, out WildlifeOrigin marked) && marked.Respawns && marked.X == 100 && !table.TryMarkRespawning(5, 49),
            "A herd known from its first route must count as a herd of the map, with the same place.");
        WildlifeOrigin unmarked = table[1];
        unmarked.Respawns = false;
        table[1] = unmarked;
        table.MarkCaptured();

        byte[] data = table.Encode();
        var restored = new WildlifeOriginTable();
        Assert(restored.TryRestore(data) && restored.IsCaptured && restored.Count == 2, "The save record must restore the table.");
        Assert(restored[0].IsDeer && restored[0].Respawns && restored[0].ExtinctSinceTick == WildlifeOrigin.NotExtinct && !restored[1].IsDeer && !restored[1].Respawns && restored[1].ExtinctSinceTick == 1234 && restored[1].X == 100,
            "The save record must keep every field.");
        Assert(restored.TryFind(5, 51, out _) && restored.TryFind(5, 50, out _) && !restored.TryFind(5, 49, out _), "After a load every entry must be found by its serial, also on a shared record.");
        WildlifeOrigin stale = restored[0];
        stale.ExtinctSinceTick = 99;
        restored[0] = stale;
        Assert(restored.TryFind(5, 51, out WildlifeOrigin current) && current.X == 100 && restored[0].ExtinctSinceTick == 99, "An extinct entry must not take a shared record from the current herd.");
        stale.ExtinctSinceTick = WildlifeOrigin.NotExtinct;
        restored[0] = stale;
        Assert(data.AsSpan().SequenceEqual(restored.Encode()), "The same table must give the same bytes on every peer.");
        Assert(!restored.TryRestore(data[..^1]) && !restored.IsCaptured && restored.Count == 0, "A damaged record must leave the table empty, so the herds are read again.");
        Assert(!restored.TryRestore(null!) && !restored.TryRestore(new byte[] { 9, 0, 0, 0, 0 }), "An unknown record must be refused.");
        table.Clear();
        Assert(!table.IsCaptured && table.Count == 0 && !table.TryFind(5, 51, out _), "A cleared table must know nothing.");
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
