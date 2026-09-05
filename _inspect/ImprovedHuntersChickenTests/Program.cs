using ImprovedHunters;
using RedBird.X64.Assembly.Stateful;
using SHCDESE.Interop;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

static class Program
{
    private static int assertions;

    private sealed class PendingSpawn
    {
        public int PlayerId { get; }
        public int UnitType { get; }
        public bool Matched { get; set; }

        public PendingSpawn(int playerId, int unitType)
        {
            PlayerId = playerId;
            UnitType = unitType;
        }
    }

    public static void Main()
    {
        TestLimits();
        TestSpawnMatching();
        TestSlotReuse();
        TestGranarySelection();
        TestHunterQueryActorPolicy();
        TestManualChickenAttackPolicy();
        TestStatefulImmediateLifecycle();
        TestMigrationContracts();
        TestNativePatterns();
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
            "ImprovedHunters must own exactly sixteen typed context-hook handles.");
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
        const string nativePath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
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

        Assert(definitions.Count == 29, $"Expected 29 native pattern definitions, found {definitions.Count}.");
        Assert(uniquelyResolvedNames.Count == 19,
            $"Expected 19 uniquely resolved pattern contracts, found {uniquelyResolvedNames.Count}.");
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
        string resolver = File.ReadAllText(Path.Combine(FindWorkspaceRoot(), "Shared", "NativePatternResolver.cs"));
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

    private static void TestLimits()
    {
        Assert(!GranaryChickenSpawnPolicy.TryGetNormalizedVanillaTarget(false, 0, 10, out _),
            "Disabled management must leave Vanilla's target untouched.");
        AssertTarget(0, 0, 0);
        AssertTarget(0, 1, int.MaxValue);
        AssertTarget(9, 10, int.MaxValue);
        AssertTarget(10, 10, 0);
        AssertTarget(11, 10, 0);
        AssertTarget(10, 5, 0);
        AssertTarget(-5, 10, int.MaxValue);
        Assert(GranaryChickenSpawnPolicy.ClampMaximum(-1) == 0, "Limit minimum clamp failed.");
        Assert(GranaryChickenSpawnPolicy.ClampMaximum(101) == 100, "Limit maximum clamp failed.");
    }

    private static void TestSpawnMatching()
    {
        const int Chicken = 62;
        const int Deer = 44;
        Stack<PendingSpawn> pending = new();
        pending.Push(new PendingSpawn(1, Chicken));

        PendingSpawn first = pending.Peek();
        Assert(!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, first.Matched, first.PlayerId, first.UnitType, 10, 20, 3, 2, Chicken, 160, 80, 3),
            "Another player's chicken must not match.");
        Assert(!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, first.Matched, first.PlayerId, first.UnitType, 10, 20, 3, 1, Deer, 160, 80, 3),
            "Another unit type must not match.");
        Assert(!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, first.Matched, first.PlayerId, first.UnitType, 10, 20, 3, 1, Chicken, 161, 80, 3),
            "A chicken at another position must not match.");
        Assert(!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, first.Matched, first.PlayerId, first.UnitType, 10, 20, 3, 1, Chicken, 160, 80, 4),
            "A chicken at another elevation must not match.");
        Assert(GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, first.Matched, first.PlayerId, first.UnitType, 10, 20, 3, 1, Chicken, 160, 80, 3),
            "The immediate matching granary chicken was not recognized.");
        first.Matched = true;
        Assert(!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, first.Matched, first.PlayerId, first.UnitType, 10, 20, 3, 1, Chicken, 160, 80, 3),
            "One granary event must not match twice.");

        pending.Push(new PendingSpawn(2, Chicken));
        PendingSpawn nested = pending.Peek();
        Assert(GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(true, nested.Matched, nested.PlayerId, nested.UnitType, 12, 21, 4, 2, Chicken, 168, 96, 4),
            "Nested top-of-stack spawn was not matched.");
        nested.Matched = true;
        pending.Pop();
        Assert(ReferenceEquals(pending.Peek(), first), "Nested spawn completion did not restore its parent context.");

        long failedReturnValue = 0;
        Assert(failedReturnValue <= 0, "Failed spawn test fixture is invalid.");
        Assert(GranaryChickenSpawnPolicy.CanAssignCompletedSpawn(true, 42, true, true, 9001, true, true, true),
            "A successful neutral chicken spawn must be assignable.");
        Assert(!GranaryChickenSpawnPolicy.CanAssignCompletedSpawn(true, failedReturnValue, false, false, 0, false, false, false),
            "A failed spawn must not create an assignment.");
        Assert(!GranaryChickenSpawnPolicy.CanAssignCompletedSpawn(true, 42, true, true, 9001, false, true, true),
            "A non-neutral owner must fail post-spawn validation.");
        Assert(!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(false, false, 1, Chicken, 10, 20, 3, 1, Chicken, 160, 80, 3),
            "Inactive safety guards must prevent neutralization.");
    }

    private static void TestSlotReuse()
    {
        Assert(GranaryChickenSpawnPolicy.IsTrackedIdentityValid(100, 100, true, true),
            "Stable slot/global identity should remain assigned.");
        Assert(!GranaryChickenSpawnPolicy.IsTrackedIdentityValid(100, 101, true, true),
            "A reused slot with a new global ID must be removed.");
        Assert(!GranaryChickenSpawnPolicy.IsTrackedIdentityValid(100, 100, false, true),
            "A reused slot with another type must be removed.");
        Assert(!GranaryChickenSpawnPolicy.IsTrackedIdentityValid(100, 100, true, false),
            "A dead chicken must be removed.");
        Assert(!GranaryChickenSpawnPolicy.IsTrackedIdentityValid(0, 0, true, true),
            "A zero global ID must never become a stable assignment.");
    }

    private static void TestGranarySelection()
    {
        Assert(GranaryChickenSpawnPolicy.ChebyshevDistance(10, 10, 13, 14) == 4,
            "Chebyshev distance is incorrect.");
        Assert(GranaryChickenSpawnPolicy.IsBetterGranaryCandidate(3, 9, 2, 4, 1, 1),
            "A nearer granary must win.");
        Assert(GranaryChickenSpawnPolicy.IsBetterGranaryCandidate(3, 4, 2, 3, 5, 1),
            "Building ID must break equal-distance ties.");
        Assert(GranaryChickenSpawnPolicy.IsBetterGranaryCandidate(3, 4, 1, 3, 4, 2),
            "Player ID must be the final deterministic tie-break.");
        Assert(!GranaryChickenSpawnPolicy.IsBetterGranaryCandidate(3, 4, 2, 3, 4, 1),
            "A worse player-ID tie must not replace the current candidate.");
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

    private static void TestManualChickenAttackPolicy()
    {
        eChimps[] supportedRangedTypes =
        {
            eChimps.CHIMP_TYPE_ARCHER,
            eChimps.CHIMP_TYPE_XBOWMAN,
            eChimps.CHIMP_TYPE_ARCHER_debug,
            eChimps.CHIMP_TYPE_CATAPULT,
            eChimps.CHIMP_TYPE_TREBUCHET,
            eChimps.CHIMP_TYPE_MANGONEL,
            eChimps.CHIMP_TYPE_BALLISTA,
            eChimps.CHIMP_TYPE_ARAB_BOW,
            eChimps.CHIMP_TYPE_ARAB_SLINGER,
            eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
            eChimps.CHIMP_TYPE_ARAB_GRENADIER,
            eChimps.CHIMP_TYPE_ARAB_BALLISTA,
            eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER,
            eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,
            eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
        };
        foreach (eChimps type in supportedRangedTypes)
        {
            Assert(ManualChickenAttackPolicy.CanOverrideCompatibilityRejection(type),
                $"Ranged attacker {type} must be eligible for an explicit chicken order.");
        }

        eChimps[] vanillaRejectedTypes =
        {
            eChimps.CHIMP_TYPE_HUNTER,
            eChimps.CHIMP_TYPE_PEASANT,
            eChimps.CHIMP_TYPE_SPEARMAN,
            eChimps.CHIMP_TYPE_PIKEMAN,
            eChimps.CHIMP_TYPE_MACEMAN,
            eChimps.CHIMP_TYPE_SWORDSMAN,
            eChimps.CHIMP_TYPE_KNIGHT,
            eChimps.CHIMP_TYPE_ENGINEER,
            eChimps.CHIMP_TYPE_MONK,
            eChimps.CHIMP_TYPE_BEDOUIN_HEALER,
            eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH,
            eChimps.CHIMP_TYPE_BEDOUIN_SAPPER,
            eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER,
        };
        foreach (eChimps type in vanillaRejectedTypes)
        {
            Assert(!ManualChickenAttackPolicy.CanOverrideCompatibilityRejection(type),
                $"Non-projectile attacker {type} must retain Vanilla's chicken rejection.");
        }
    }

    private static void AssertTarget(int liveCount, int limit, int expectedTarget)
    {
        Assert(GranaryChickenSpawnPolicy.TryGetNormalizedVanillaTarget(true, liveCount, limit, out int actualTarget),
            "Enabled management did not provide an override.");
        Assert(actualTarget == expectedTarget,
            $"Unexpected normalized target for live={liveCount}, limit={limit}: {actualTarget}.");
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
