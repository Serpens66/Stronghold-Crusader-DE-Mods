using EngineerSiegeFix;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

internal static class Program
{
    private const string ExpectedHash =
        "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
    private const string DllPath =
        @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";

    private static readonly Dictionary<string, int> Patterns = new Dictionary<string, int>
    {
        ["CatapultHandlerPattern"] = 0x1520D0,
        ["TrebuchetHandlerPattern"] = 0x1535F0,
        ["CatapultStateSixPattern"] = 0x1524FA,
        ["TrebuchetStateSixPattern"] = 0x153A78,
        ["SiegeTentTickPattern"] = 0x158690,
        ["SiegeTentCompletionTailPattern"] = 0x158762,
        ["UnitConversionPattern"] = 0x195D10
    };

    private static int assertions;

    private static int Main()
    {
        try
        {
            string workspace = FindWorkspace();
            byte[] file = File.ReadAllBytes(DllPath);
            Check(Hash(file) == ExpectedHash, "canonical native hash");
            PeImage pe = PeImage.Load(file);
            CheckPatterns(workspace, pe);
            CheckNativeContracts(pe.Image);
            CheckSourceContracts(workspace);
            CheckPolicy();
            Console.WriteLine($"PASS: EngineerSiegeFix tests ({assertions} assertions).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
    }

    private static void CheckPatterns(string workspace, PeImage pe)
    {
        string source = File.ReadAllText(Path.Combine(
            workspace,
            "EngineerSiegeFix",
            "src",
            "EngineerSiegeFixNativeDefinition.cs"));
        Dictionary<string, string> values = ReadConstStrings(source);
        foreach (KeyValuePair<string, int> contract in Patterns)
        {
            Check(values.TryGetValue(contract.Key, out string value), contract.Key + " exists");
            Token[] pattern = Parse(value);
            List<int> matches = FindMatches(pe, pattern);
            Check(matches.Count == 1 && matches[0] == contract.Value,
                contract.Key + " unique at expected RVA");
        }
    }

    private static void CheckNativeContracts(byte[] image)
    {
        CheckBytes(image, 0x1520D0,
            "48 89 5C 24 08",
            "catapult hook spans exactly one complete prologue instruction");
        CheckBytes(image, 0x1520D5,
            "48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 81 EC D0 00 00 00",
            "catapult resumes with the remaining saved registers and stack allocation");
        CheckBytes(image, 0x1535F0,
            "48 89 5C 24 08",
            "trebuchet hook spans exactly one complete prologue instruction");
        CheckBytes(image, 0x1535F5,
            "48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 81 EC F0 00 00 00",
            "trebuchet resumes with the remaining saved registers and stack allocation");
        CheckBytes(image, 0x184103,
            "48 0F BF 84 19 E6 06 00 00",
            "central dispatcher loads the signed unit type into RAX");
        CheckBytes(image, 0x18410C,
            "41 FF 94 C6 B0 1C 32 00",
            "central dispatcher calls the handler table indexed by RAX");
        Check(BitConverter.ToUInt64(image, 0x321CB0 + 0x27 * 8) == 0x1801520D0UL,
            "handler table entry 0x27 points to the catapult handler");
        Check(BitConverter.ToUInt64(image, 0x321CB0 + 0x28 * 8) == 0x1801535F0UL,
            "handler table entry 0x28 points to the trebuchet handler");
        CheckBytes(image, 0x1524FA,
            "8B 84 2B DC 09 00 00",
            "catapult state-six hook spans exactly the phase-seed read");
        CheckBytes(image, 0x152501,
            "33 05 C9 AF 69 03",
            "catapult state-six hook resumes before the phase xor");
        CheckBytes(image, 0x153A78,
            "42 8B 84 1B DC 09 00 00",
            "trebuchet state-six hook spans exactly the phase-seed read");
        CheckBytes(image, 0x153A80,
            "33 05 4A 9A 69 03",
            "trebuchet state-six hook resumes before the phase xor");
        CheckBytes(image, 0x158690,
            "40 53 48 83 EC 30",
            "siege-tent entry hook spans push RBX and stack allocation");
        CheckBytes(image, 0x158696,
            "48 63 05 27 7C 7D 00",
            "siege-tent entry resumes with current-context ID read");
        CheckBytes(image, 0x158762,
            "C7 84 19 14 0A 00 00 00 00 00 00",
            "siege-tent completion hook spans exactly the pending-field clear");
        CheckBytes(image, 0x15876D,
            "48 83 C4 30 5B C3",
            "siege-tent completion resumes at the complete epilogue");
        CheckBytes(image, 0x195D10,
            "48 89 5C 24 08",
            "unit converter hook spans exactly the RBX save");
        CheckBytes(image, 0x195D15,
            "48 89 74 24 10 57 48 83 EC 20",
            "unit converter resumes with remaining prologue");
        CheckBytes(image, 0x1527B3,
            "42 C7 84 03 18 09 00 00 6D 00 05 00",
            "Vanilla catapult handoff writes engineer consume state");
        CheckBytes(image, 0x153D18,
            "42 C7 84 03 18 09 00 00 6D 00 05 00",
            "Vanilla trebuchet handoff writes engineer consume state");
    }

    private static void CheckSourceContracts(string workspace)
    {
        string runtime = File.ReadAllText(Path.Combine(
            workspace,
            "EngineerSiegeFix",
            "src",
            "EngineerSiegeFixRuntime.cs"));
        string plugin = File.ReadAllText(Path.Combine(
            workspace,
            "EngineerSiegeFix",
            "src",
            "EngineerSiegeFixPlugin.cs"));
        Check(runtime.Contains("ref catapultHandlerHook"), "catapult handler-entry hook registered");
        Check(runtime.Contains("ref trebuchetHandlerHook"), "trebuchet handler-entry hook registered");
        Check(runtime.Contains("ref catapultStateSixHook"), "catapult state-six hook registered");
        Check(runtime.Contains("ref trebuchetStateSixHook"), "trebuchet state-six hook registered");
        Check(runtime.Contains("ref siegeTentTickHook"), "siege-tent entry hook registered");
        Check(runtime.Contains("ref siegeTentCompletionHook"), "siege-tent completion hook registered");
        Check(runtime.Contains("ref unitConversionHook"), "unit converter hook registered");
        Check(Regex.Matches(runtime, "hookSize: 5").Count == 3,
            "handler entries and converter use exact five-byte hook spans");
        Check(runtime.Contains("hookSize: 6"), "siege-tent entry uses exact six-byte hook span");
        Check(runtime.Contains("hookSize: 7"), "catapult state-six uses exact seven-byte hook span");
        Check(runtime.Contains("hookSize: 8"), "trebuchet state-six uses exact eight-byte hook span");
        Check(runtime.Contains("hookSize: 11"), "siege-tent completion uses exact eleven-byte hook span");
        Check(runtime.Contains("placement: OverwrittenInstructionPlacement.BeforeCallback"),
            "displaced RBX save executes before each observation callback");
        Check(runtime.Contains("regs: X64SmartCPUContextRegs.All"),
            "callbacks preserve every live handler-entry register");
        Check(runtime.Contains("GetCurrentContextUnitId"), "one-based device ID uses the state dispatcher context");
        Check(runtime.Contains("ValidateHandlerTableTarget(CatapultType"),
            "runtime validates the relocated catapult table entry");
        Check(runtime.Contains("ValidateHandlerTableTarget(TrebuchetType"),
            "runtime validates the relocated trebuchet table entry");
        Check(!runtime.Contains("WriteUInt16"), "diagnostic runtime contains no 16-bit native write helper");
        Check(!runtime.Contains("WriteUInt32"), "diagnostic runtime contains no 32-bit native write helper");
        Check(!runtime.Contains("Marshal.GetDelegateForFunctionPointer"),
            "diagnostic runtime calls no mutating native cleanup helper");
        Check(!runtime.Contains("Hook.Trampoline"), "handler hooks do not replace either function");
        Check(!runtime.Contains("AddDetour"), "handler hooks avoid function detours");
        Check(runtime.Contains("SIEGE_CANDIDATE_TENT_ENTRY"), "siege-tent entry has a distinct marker");
        Check(runtime.Contains("SIEGE_CANDIDATE_TENT_COMPLETION"),
            "siege-tent completion has a distinct marker");
        Check(runtime.Contains("SIEGE_CANDIDATE_CONVERTER_ENTRY"),
            "unit converter has a distinct marker");
        Check(runtime.Contains("SIEGE_CANDIDATE_CATAPULT_STATE6"),
            "catapult state-six has a distinct marker");
        Check(runtime.Contains("SIEGE_CANDIDATE_TREBUCHET_STATE6"),
            "trebuchet state-six has a distinct marker");
        Check(runtime.Contains("identitySource=fastcall-rcx-rdx"),
            "converter identity follows its native RCX/RDX ABI");
        Check(runtime.Contains("context.Pointer->RAX"),
            "state-six observers record the displaced phase-seed result");
        Check(runtime.Contains("activeObservationHooks=7"),
            "installation marker confirms every simultaneous observation hook");
        Check(plugin.Contains("requireCurrentVersion: true"), "unknown native hashes fail closed");
        Check(plugin.Contains("private static EngineerSiegeFixRuntime runtime"),
            "runtime remains rooted after SHCDE destroys the startup plugin component");
        Check(plugin.Contains("private static void OnCrusaderLibraryLoaded"),
            "library initialization does not depend on the destroyed component instance");
        Check(!plugin.Contains("void Update("), "plugin does not rely on its short-lived Unity Update callback");
        Check(!plugin.Contains("void OnDestroy("), "plugin has no startup OnDestroy teardown");
        Check(!plugin.Contains("runtime?.Dispose"), "plugin never disposes process-wide hooks during startup cleanup");
        Check(!plugin.Contains("LibraryLoaded -= OnCrusaderLibraryLoaded"),
            "plugin never removes its process-wide library subscription during startup cleanup");
        Check(runtime.Contains("GameTimeManagerAPI.Instance.OnTick += OnGameTick"),
            "simulation diagnostics use the long-lived Script Extender tick publisher");
        Check(runtime.Contains("SIEGE_TICK_HEARTBEAT"),
            "the first verified tick callbacks emit bounded lifecycle evidence");
        Check(runtime.IndexOf("SIEGE_TICK_HEARTBEAT", StringComparison.Ordinal) <
              runtime.IndexOf("GetUnitManager().Pointer", StringComparison.Ordinal),
            "tick heartbeat precedes the first unit-manager guard");
        Check(!runtime.Contains("GameTickRva"), "runtime no longer trusts the unvalidated raw tick RVA");
        Check(!runtime.Contains("lastPollTick"), "runtime no longer suppresses polling through the raw tick guard");
        Check(runtime.Contains("SIEGE_ROUTE_DIAGNOSTIC_INSTALLED"), "dispatcher evidence is logged at installation");
        Check(runtime.Contains("SIEGE_HANDLER_ENTRY"), "handler-entry transitions are logged");
        Check(runtime.Contains("SIEGE_SLOT_TRANSITION"), "siege slot transitions are logged");
        Check(runtime.Contains("SIEGE_ENGINEER_TRANSITION"), "associated engineer transitions are logged");
        Check(runtime.Contains("correctionActive=false"), "log explicitly identifies observation-only mode");
        Check(runtime.Contains("private void OnGameTick(int tick)"), "each poll is driven exactly by a game-tick callback");
        Check(runtime.Contains("TickHeartbeatLimit = 3"), "lifecycle heartbeat logging is bounded");
        Check(runtime.Contains("HandlerTransitionLimit = 160"), "handler transition logging is bounded");
        Check(runtime.Contains("SlotTransitionLimit = 320"), "siege slot logging is bounded");
        Check(runtime.Contains("EngineerTransitionLimit = 480"), "engineer logging is bounded");
        Check(runtime.Contains("ReadUInt32(unit, GlobalIdOffset)"), "diagnostics capture global identities");
        Check(runtime.Contains("AssignedEngineerGlobalsOffset + 8"), "diagnostics capture all three crew identities");
        Check(runtime.Contains("diagnosticsDisabled = true"), "diagnostic faults cannot escape every Unity frame");
    }

    private static void CheckPolicy()
    {
        DeviceSnapshot cat = Device(100, EngineerCrewHandoffPolicy.CatapultType, false);
        CheckSelected(cat, new[] { Engineer(2, 12, 100), Engineer(3, 13, 100) }, 2, "catapult two crew");
        CheckSelected(Device(2, EngineerCrewHandoffPolicy.CatapultType, false),
            new[] { Engineer(100, 12, 2), Engineer(101, 13, 2) }, 2, "device ID below engineer IDs");
        CheckSelected(Device(200, EngineerCrewHandoffPolicy.TrebuchetType, false),
            new[] { Engineer(2, 12, 200), Engineer(3, 13, 200), Engineer(4, 14, 200) }, 3,
            "trebuchet three crew");

        for (int phase = 0; phase < 16; phase++)
            CheckSelected(cat, new[] { Engineer(2, 12, 100), Engineer(3, 13, 100) }, 2,
                "tick phase " + phase);

        for (uint phaseSeed = 0; phaseSeed < 16; phaseSeed++)
        {
            int scheduled = 0;
            for (uint tick = 0; tick < 16; tick++)
            {
                bool actual = EngineerCrewHandoffPolicy.IsScheduledCrewSearch(phaseSeed, tick);
                bool expected = ((phaseSeed ^ tick ^ 0xFFFFFFF8U) & 0xFU) == 0;
                Check(actual == expected, $"Vanilla cadence seed {phaseSeed} tick {tick}");
                if (actual)
                    scheduled++;
            }
            Check(scheduled == 1, $"exactly one search in 16 ticks for seed {phaseSeed}");
        }

        CheckRejected(cat, new[] { Engineer(2, 12, 100) }, "incomplete crew");
        CheckRejected(cat, new[] { Engineer(2, 12, 100), Engineer(3, 13, 100), Engineer(4, 14, 100) },
            "ambiguous extra crew");
        CheckRejected(cat, new[] { Engineer(2, 12, 100), Engineer(2, 13, 100) }, "duplicate unit ID");
        CheckRejected(cat, new[] { Engineer(2, 12, 100), Engineer(3, 12, 100) }, "duplicate global ID");
        CheckRejected(cat, new[] { Engineer(2, 12, 100, alive: false), Engineer(3, 13, 100) }, "dead engineer");
        CheckRejected(cat, new[] { Engineer(2, 12, 100, owner: 2), Engineer(3, 13, 100) }, "foreign engineer");
        CheckRejected(cat, new[] { Engineer(2, 12, 100, type: 29), Engineer(3, 13, 100) }, "wrong type");
        CheckRejected(cat, new[] { Engineer(2, 12, 100, assignment: 1), Engineer(3, 13, 100) }, "assigned slot");
        CheckRejected(cat, new[] { Engineer(2, 0, 100), Engineer(3, 13, 100) }, "stale global ID");
        CheckRejected(cat, new[] { Engineer(2, 12, 99), Engineer(3, 13, 100) }, "wrong target");
        CheckRejected(cat, new[] { Engineer(2, 12, 100, worldX: 130), Engineer(3, 13, 100) }, "too far");
        CheckRejected(cat, new[] { Engineer(2, 12, 100, height: 17), Engineer(3, 13, 100) }, "height mismatch");

        DeviceSnapshot aiCat = Device(100, EngineerCrewHandoffPolicy.CatapultType, true);
        CheckSelected(aiCat,
            new[] { Engineer(2, 12, 0, command: 0, role: 0x16), Engineer(3, 13, 0, command: 0, role: 0x16) },
            2,
            "AI role assignment");
        CheckRejected(cat,
            new[] { Engineer(2, 12, 0, command: 0, role: 0x16), Engineer(3, 13, 0, command: 0, role: 0x16) },
            "human player cannot use AI role fallback");
        CheckRejected(Device(100, EngineerCrewHandoffPolicy.TrebuchetType, true),
            new[]
            {
                Engineer(2, 12, 0, command: 0, role: 0x16),
                Engineer(3, 13, 0, command: 0, role: 0x16),
                Engineer(4, 14, 0, command: 0, role: 0x16)
            },
            "Vanilla trebuchet path has no AI-role-only fallback");
        CheckRejected(Device(100, 0x29, false),
            new[] { Engineer(2, 12, 100), Engineer(3, 13, 100) }, "unsupported device");

        CheckDiagnostic(HandoffDiagnosticOutcome.Pending, true, true, false, true, false, 0,
            "pending before conversion");
        CheckDiagnostic(HandoffDiagnosticOutcome.Pending, true, true, true, true, false, 255,
            "live engineers allowed before timeout");
        CheckDiagnostic(HandoffDiagnosticOutcome.Passed, true, true, true, true, true, 1,
            "ready device and consumed engineers pass");
        CheckDiagnostic(HandoffDiagnosticOutcome.Failed, true, true, true, false, true, 1,
            "ready device crew mismatch fails immediately");
        CheckDiagnostic(HandoffDiagnosticOutcome.Failed, true, true, false, true, false, 256,
            "conversion timeout fails");
        CheckDiagnostic(HandoffDiagnosticOutcome.Failed, true, true, true, true, false, 256,
            "surviving engineer timeout fails");
        CheckDiagnostic(HandoffDiagnosticOutcome.Inconclusive, false, true, false, false, false, uint.MaxValue,
            "session change is inconclusive");
        CheckDiagnostic(HandoffDiagnosticOutcome.Inconclusive, true, false, false, false, false, 10,
            "destroyed device is inconclusive");
    }

    private static void CheckDiagnostic(
        HandoffDiagnosticOutcome expected,
        bool sessionContinues,
        bool deviceIdentityPresent,
        bool deviceReady,
        bool crewMatches,
        bool engineersGone,
        uint elapsedTicks,
        string name)
    {
        HandoffDiagnosticOutcome actual = EngineerHandoffDiagnosticPolicy.Evaluate(
            sessionContinues,
            deviceIdentityPresent,
            deviceReady,
            crewMatches,
            engineersGone,
            elapsedTicks);
        Check(actual == expected, name);
    }

    private static DeviceSnapshot Device(int id, ushort type, bool ai) =>
        new DeviceSnapshot(id, 500, 1, type, ai, 100, 100, 0, 0);

    private static EngineerSnapshot Engineer(
        int id,
        uint global,
        ushort target,
        byte owner = 1,
        bool alive = true,
        ushort type = EngineerCrewHandoffPolicy.EngineerType,
        int assignment = 0,
        ushort command = EngineerCrewHandoffPolicy.BuildSiegeEngineCommand,
        ushort role = 0,
        int worldX = 100,
        int worldY = 100,
        int height = 0) =>
        new EngineerSnapshot(
            id, global, owner, type, alive, assignment, command, target, role, 0,
            worldX, worldY, height, 0);

    private static void CheckSelected(
        DeviceSnapshot device,
        IReadOnlyList<EngineerSnapshot> units,
        int expected,
        string name)
    {
        bool result = EngineerCrewHandoffPolicy.TrySelect(device, units, out EngineerSnapshot[] crew);
        Check(result && crew != null && crew.Length == expected, name);
    }

    private static void CheckRejected(
        DeviceSnapshot device,
        IReadOnlyList<EngineerSnapshot> units,
        string name)
    {
        bool result = EngineerCrewHandoffPolicy.TrySelect(device, units, out EngineerSnapshot[] crew);
        Check(!result && crew == null, name);
    }

    private static Dictionary<string, string> ReadConstStrings(string source)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var declaration = new Regex(
            "const\\s+string\\s+(?<name>[A-Za-z0-9_]+)\\s*=\\s*(?<value>(?:\\s*\"(?:[^\"\\\\]|\\\\.)*\"\\s*\\+?)+)\\s*;",
            RegexOptions.Singleline);
        var literal = new Regex("\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");
        foreach (Match match in declaration.Matches(source))
        {
            string value = string.Concat(literal.Matches(match.Groups["value"].Value)
                .Cast<Match>().Select(item => Regex.Unescape(item.Groups["text"].Value)));
            result[match.Groups["name"].Value] = value;
        }
        return result;
    }

    private static Token[] Parse(string pattern) =>
        pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value == "?" || value == "??"
                ? new Token(0, true)
                : new Token(Convert.ToByte(value, 16), false))
            .ToArray();

    private static List<int> FindMatches(PeImage pe, Token[] pattern)
    {
        var result = new List<int>();
        foreach (Section section in pe.Sections.Where(item => item.Executable))
        {
            int end = Math.Min(pe.Image.Length, section.Rva + section.Size) - pattern.Length;
            for (int rva = section.Rva; rva <= end; rva++)
            {
                bool matches = true;
                for (int index = 0; index < pattern.Length; index++)
                {
                    if (!pattern[index].Wildcard && pe.Image[rva + index] != pattern[index].Value)
                    {
                        matches = false;
                        break;
                    }
                }
                if (matches)
                    result.Add(rva);
            }
        }
        return result;
    }

    private static void CheckBytes(byte[] image, int rva, string expected, string name)
    {
        byte[] bytes = expected.Split(' ').Select(value => Convert.ToByte(value, 16)).ToArray();
        Check(rva >= 0 && rva <= image.Length - bytes.Length, name + " bounds");
        for (int index = 0; index < bytes.Length; index++)
            Check(image[rva + index] == bytes[index], name + " byte " + index);
    }

    private static string Hash(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
    }

    private static string FindWorkspace()
    {
        DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "EngineerSiegeFix")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Workspace root not found.");
    }

    private static void Check(bool condition, string name)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("Assertion failed: " + name);
    }

    private readonly struct Token
    {
        public Token(byte value, bool wildcard)
        {
            Value = value;
            Wildcard = wildcard;
        }
        public byte Value { get; }
        public bool Wildcard { get; }
    }

    private readonly struct Section
    {
        public Section(int rva, int size, bool executable)
        {
            Rva = rva;
            Size = size;
            Executable = executable;
        }
        public int Rva { get; }
        public int Size { get; }
        public bool Executable { get; }
    }

    private sealed class PeImage
    {
        private PeImage(byte[] image, List<Section> sections)
        {
            Image = image;
            Sections = sections;
        }
        public byte[] Image { get; }
        public List<Section> Sections { get; }

        public static PeImage Load(byte[] file)
        {
            int pe = BitConverter.ToInt32(file, 0x3C);
            int sectionCount = BitConverter.ToUInt16(file, pe + 6);
            int optionalSize = BitConverter.ToUInt16(file, pe + 20);
            int optional = pe + 24;
            int imageSize = BitConverter.ToInt32(file, optional + 56);
            byte[] image = new byte[imageSize];
            int headers = BitConverter.ToInt32(file, optional + 60);
            Buffer.BlockCopy(file, 0, image, 0, Math.Min(headers, file.Length));
            var sections = new List<Section>();
            int table = optional + optionalSize;
            for (int index = 0; index < sectionCount; index++)
            {
                int header = table + index * 40;
                int virtualSize = BitConverter.ToInt32(file, header + 8);
                int rva = BitConverter.ToInt32(file, header + 12);
                int rawSize = BitConverter.ToInt32(file, header + 16);
                int raw = BitConverter.ToInt32(file, header + 20);
                if (rawSize > 0)
                    Buffer.BlockCopy(file, raw, image, rva, Math.Min(rawSize, image.Length - rva));
                uint characteristics = BitConverter.ToUInt32(file, header + 36);
                sections.Add(new Section(rva, Math.Max(virtualSize, rawSize),
                    (characteristics & 0x20000000U) != 0));
            }
            return new PeImage(image, sections);
        }
    }
}
