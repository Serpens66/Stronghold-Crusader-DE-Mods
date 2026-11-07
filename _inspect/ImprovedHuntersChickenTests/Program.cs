using ImprovedHunters;
using Iced.Intel;
using RedBird.X64.Assembly.Stateful;
using RedBird.X64.Hooks;
using SHCDESE.Interop;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

static class Program
{
    private const string CanonicalNativePath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
    private static int assertions;

    private sealed class StubCodeWriter : CodeWriter
    {
        internal readonly List<byte> Bytes = new();
        public override void WriteByte(byte value) => Bytes.Add(value);
    }

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
        if (args.Length == 1 && args[0] == "dispatch")
        {
            TestDispatchReaderHooks();
            Console.WriteLine($"ImprovedHunters dispatch hooks passed: assertions={assertions}.");
            return;
        }
        TestPopulationClamp();
        TestHunterQueryActorPolicy();
        TestStatefulImmediateLifecycle();
        TestMigrationContracts();
        TestNativePatterns();
        TestDispatchReaderHooks();
        NativeStubExecutionTests.Run(MapPeImage(File.ReadAllBytes(CanonicalNativePath), out _));
        Console.WriteLine($"ImprovedHunters chicken policy tests passed: assertions={assertions}.");
    }

    private static void TestStatefulImmediateLifecycle()
    {
        IntPtr instruction = Marshal.AllocHGlobal(16);
        try
        {
            Marshal.Copy(new byte[] { 0x66, 0xB8, 0x34, 0x12 }, 0, instruction, 4);
            var site = new ManagedAssemblyImmediate<short>(instruction, operand: 1);
            Assert(site.OriginalValue == 0x1234 && site.GetValue() == 0x1234,
                "RedBird did not decode the original imm16 value.");
            site.SetValue(1800);
            Assert(site.GetValue() == 1800 && Marshal.ReadByte(instruction, 2) == 0x08 &&
                Marshal.ReadByte(instruction, 3) == 0x07,
                "RedBird did not encode the requested imm16 value.");
            site.Dispose();
            Assert(Marshal.ReadByte(instruction, 2) == 0x34 && Marshal.ReadByte(instruction, 3) == 0x12,
                "Disposing a mod-owned RedBird immediate did not restore its original bytes.");

            Marshal.Copy(new byte[] { 0x66, 0x83, 0xF8, 0x01 }, 0, instruction, 4);
            using var narrowSite = new ManagedAssemblyImmediate<short>(instruction, operand: 1);
            narrowSite.SetValue(127);
            Assert(narrowSite.GetValue() == 127, "Signed imm8 upper boundary was rejected.");
            AssertThrows<ArgumentOutOfRangeException>(() => narrowSite.SetValue(128),
                "Signed imm8 upper overflow was not rejected.");
            narrowSite.SetValue(-128);
            Assert(narrowSite.GetValue() == -128, "Signed imm8 lower boundary was rejected.");
            AssertThrows<ArgumentOutOfRangeException>(() => narrowSite.SetValue(-129),
                "Signed imm8 lower overflow was not rejected.");
        }
        finally
        {
            Marshal.FreeHGlobal(instruction);
        }
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
        Assert(Count(allSource, "new HookHandle<X64InlineHook>()") == 16,
            "ImprovedHunters must own exactly sixteen typed inline-hook handle declarations.");
        Assert(Count(allSource, "CommitResult commitResult = transaction.Commit()") == 7 &&
            Count(allSource, "commitResult.IsCompleteSuccess") == 7,
            "Every one of the seven atomic hook transactions must check aggregate success.");
        Assert(infrastructure.Contains("HookTarget.FromAddress(address)", StringComparison.Ordinal) &&
            infrastructure.Contains("new ContextHookOptions", StringComparison.Ordinal) &&
            infrastructure.Contains("OwnsHooks = true", StringComparison.Ordinal),
            "The common RedBird target/options/feature ownership contract is incomplete.");
        Assert(!runtime.Contains("rabbitDespawnTickTime?.Dispose()", StringComparison.Ordinal) &&
            runtime.Contains("rabbitDespawnOverride?.Dispose()", StringComparison.Ordinal) &&
            runtime.Contains("camelDespawnTickTime?.Dispose()", StringComparison.Ordinal) &&
            runtime.Contains("chickenDespawnTickTime?.Dispose()", StringComparison.Ordinal),
            "Rabbit/ camel/ chicken immediate ownership is not separated correctly.");
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
        var uniquelyResolvedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "CamelDespawnTickTimePattern",
            "ChickenDespawnTickTimePattern"
        };
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

        Assert(definitions.Count == 49, $"Expected 49 native pattern definitions, found {definitions.Count}.");
        Assert(uniquelyResolvedNames.Count == 41,
            $"Expected 41 uniquely resolved pattern contracts, found {uniquelyResolvedNames.Count}.");
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

    private static void TestDispatchReaderHooks()
    {
        const string nativePath = CanonicalNativePath;
        byte[] image = MapPeImage(File.ReadAllBytes(nativePath), out _);
        foreach ((int rva, int length, int sourceType, byte replacement, bool inR9) in new[]
        {
            (0x6BA0C, 17, 7, (byte)3, false)
        })
        {
            IntPtr copy = Marshal.AllocHGlobal(64);
            IntPtr flag = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                Marshal.Copy(image, rva, copy, 64);
                Marshal.WriteInt32(flag, 0);
                using var hook = new X64InlineHook(unchecked((ulong)copy.ToInt64()), length);
                Assert(hook.DisplacedByteCount == length,
                    $"Dispatch reader 0x{rva:X} displaced {hook.DisplacedByteCount}, expected {length}.");
                int generated = 0;
                Assembler? emitted = null;
                hook.Generate((assembler, instructions, returnAddress) =>
                {
                    Assert(instructions.Length == 2 &&
                           instructions[0].Length + instructions[1].Length == length,
                        $"Dispatch reader 0x{rva:X} has unexpected original instructions.");
                    DispatchIndexStub.Generate(assembler, instructions,
                        unchecked((ulong)flag.ToInt64()), sourceType, replacement, inR9);
                    emitted = assembler;
                    generated++;
                });
                Assert(generated == 1 && hook.StubAddress != IntPtr.Zero,
                    $"Dispatch reader 0x{rva:X} did not assemble one complete RedBird stub.");
                var writer = new StubCodeWriter();
                string? error = null;
                Assert(emitted != null && emitted.TryAssemble(writer,
                    unchecked((ulong)hook.StubAddress.ToInt64()), out error, out _),
                    $"Dispatch reader 0x{rva:X} did not reassemble: {error}.");
                byte[] bytes = writer.Bytes.ToArray();
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes),
                    unchecked((ulong)hook.StubAddress.ToInt64()));
                var decoded = new List<Instruction>();
                while (decoder.IP - unchecked((ulong)hook.StubAddress.ToInt64()) < (ulong)(bytes.Length - 8))
                {
                    Instruction instruction = decoder.Decode();
                    Assert(!instruction.IsInvalid, $"Dispatch reader 0x{rva:X} has invalid stub code.");
                    decoded.Add(instruction);
                }
                Assert(decoder.IP - unchecked((ulong)hook.StubAddress.ToInt64()) == (ulong)(bytes.Length - 8) &&
                    bytes.AsSpan(bytes.Length - 14, 6).SequenceEqual(new byte[] { 0xFF, 0x25, 0, 0, 0, 0 }) &&
                    BitConverter.ToUInt64(bytes, bytes.Length - 8) == unchecked((ulong)copy.ToInt64()) + (ulong)length,
                    $"Dispatch reader 0x{rva:X} did not decode completely.");
                Assert(decoded.Count(instruction => instruction.Mnemonic == Mnemonic.Movzx &&
                    instruction.Op0Register == Register.EAX) == 1 &&
                    decoded.Count(instruction => instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.ECX) == 1,
                    $"Dispatch reader 0x{rva:X} duplicated an original table load.");
            }
            finally
            {
                Marshal.FreeHGlobal(flag);
                Marshal.FreeHGlobal(copy);
            }
        }
        string stub = File.ReadAllText(Path.Combine(FindWorkspaceRoot(),
            "ImprovedHunters", "src", "DispatchIndexStub.cs"));
        Assert(Count(stub, "assembler.AddInstruction(instructions[0])") == 1 &&
               Count(stub, "assembler.AddInstruction(instructions[1])") == 1 &&
               stub.Contains("assembler.je(vanilla);") &&
               stub.Contains("assembler.jne(readOriginalIndex);") &&
               stub.Contains("assembler.jmp(loadDispatchTarget);"),
            "Dispatch reader must preserve one vanilla load, one replacement skip and one common target load.");
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

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            assertions++;
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void TestPopulationClamp()
    {
        Assert(GranaryChickenSpawnPolicy.ClampTarget(-1) == 0, "Population minimum clamp failed.");
        Assert(GranaryChickenSpawnPolicy.ClampTarget(101) == 100, "Population maximum clamp failed.");
        Assert(GranaryChickenSpawnPolicy.ClampTarget(10) == 10, "Population value changed unexpectedly.");
        Assert(GranaryChickenSpawnPolicy.DefaultTargetPerPlayer == 10, "The population default changed.");
        Assert(TannerMeatPolicy.DefaultMeatPerTrip == 5, "The Tanner default changed.");
        TestTannerRunTable();
        TestWildlifeRange();
        TestWildlifeOrigins();
        TestHerdSettings();
        Assert(HunterSearchRangePolicy.Clamp(0) == 0 && HunterSearchRangePolicy.Clamp(-3) == 0 && HunterSearchRangePolicy.Clamp(5) == 21 &&
            HunterSearchRangePolicy.Clamp(40) == 40 && HunterSearchRangePolicy.Clamp(999) == 200, "The search range clamp changed.");
        Assert(HunterSearchRangePolicy.IsWithin(500, 0) && HunterSearchRangePolicy.IsWithin(30, 30) && !HunterSearchRangePolicy.IsWithin(31, 30),
            "The search range rule changed.");
        Assert(TannerMeatPolicy.ClampMeatPerTrip(0) == 1 && TannerMeatPolicy.ClampMeatPerTrip(101) == 100, "The meat per trip clamp failed.");
        Assert(TannerMeatPolicy.MeatRunsFromSliderValue(3) == 0 && TannerMeatPolicy.MeatRunsFromSliderValue(0) == 3 &&
            TannerMeatPolicy.SliderValueFromMeatRuns(1) == 2, "The slider must run from only meat on the left to no meat on the right.");
    }

    private static void TestHunterQueryActorPolicy()
    {
        const ulong manager = 0x10000000;
        Assert(HunterQueryActorPolicy.TryReconstructHunterUnitId(
                manager + 96 * HunterQueryActorPolicy.NativeUnitSlotSize,
                manager,
                out int hunterUnitId) && hunterUnitId == 96,
            "The native Hunter ID reconstruction failed.");
        Assert(!HunterQueryActorPolicy.TryReconstructHunterUnitId(manager, manager, out _),
            "Hunter ID zero must be rejected.");
        Assert(!HunterQueryActorPolicy.TryReconstructHunterUnitId(manager - 1, manager, out _),
            "A Hunter base below the manager must be rejected.");
        Assert(!HunterQueryActorPolicy.TryReconstructHunterUnitId(manager + 1, manager, out _),
            "A non-slot-aligned Hunter base must be rejected.");
        Assert(HunterQueryActorPolicy.IsMatchingCapture(170, 170, 94),
            "An identity-matching query capture must be accepted.");
        Assert(!HunterQueryActorPolicy.IsMatchingCapture(170, 171, 94),
            "A capture from another candidate must be rejected.");
        Assert(!HunterQueryActorPolicy.IsMatchingCapture(170, 170, 0),
            "A capture without a reconstructed Hunter must be rejected.");
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

    private static unsafe void TestTannerRunTable()
    {
        var table = new TannerRunTable();
        Assert(table.GetMeatRuns(7, 100) == 0, "A new Tannery starts without meat.");
        table.SetMeatRuns(7, 100, 2);
        table.SetMeatRuns(3, 50, 1);
        Assert(table.GetMeatRuns(7, 100) == 2, "The table must return the chosen runs.");
        Assert(table.GetMeatRuns(7, 101) == 0, "Another building on the same slot must not inherit them.");
        byte* entry = (byte*)table.Address + 7 * TannerRunTable.EntrySize;
        entry[TannerRunTable.MeatTripOffset] = 1;
        table.SetMeatRuns(7, 100, 0);
        Assert(entry[TannerRunTable.MeatTripOffset] == 1, "A Tanner on his way keeps the good he carries.");
        table.SetMeatRuns(7, 101, 3);
        Assert(entry[TannerRunTable.MeatTripOffset] == 0, "A new building must not inherit a trip.");
        table.SetMeatRuns(7, 101, 4);
        Assert(table.GetMeatRuns(7, 101) == 3, "An invalid value must be ignored.");
        entry[TannerRunTable.MeatTripOffset] = 1;

        byte[] saved = table.Encode();
        var restored = new TannerRunTable();
        Assert(restored.TryRestore(saved) && restored.GetMeatRuns(7, 101) == 3 && restored.GetMeatRuns(3, 50) == 1, "The save record must restore the table.");
        Assert(((byte*)restored.Address + 7 * TannerRunTable.EntrySize)[TannerRunTable.MeatTripOffset] == 1, "The save record must restore a trip on its way.");
        Assert(restored.Encode().AsSpan().SequenceEqual(saved), "The same state must give the same bytes on every peer.");

        byte[] damaged = (byte[])saved.Clone();
        damaged[sizeof(int) + 6] = 9;
        Assert(!restored.TryRestore(damaged) && restored.GetMeatRuns(7, 101) == 0, "A damaged record must leave the table empty.");
        Assert(!restored.TryRestore(new byte[3]) && !restored.TryRestore(null!), "A truncated record must be refused.");
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
