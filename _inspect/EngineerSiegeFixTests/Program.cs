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
        ["SiegeTentTickPattern"] = 0x158690,
        ["AiCrewBookkeepingPattern"] = 0x123EA0,
        ["ClearSelectedUnitPattern"] = 0x186C20,
        ["RemoveUnitFromGroupsPattern"] = 0x19A5D0
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
        CheckBytes(image, 0x158690, "40 53 48 83 EC 30", "whole-function detour prologue");
        CheckBytes(image, 0x1586E1,
            "B8 04 00 00 00 66 89 84 19 E4 06 00 00",
            "tent marks pending conversion");
        CheckBytes(image, 0x1586FE,
            "66 83 C0 27 66 89 84 19 22 09 00 00",
            "tent stores catapult/trebuchet pending type");
        CheckBytes(image, 0x15870A,
            "B8 06 00 00 00 66 89 84 19 24 09 00 00",
            "tent stores pending state six");
        CheckBytes(image, 0x195D36,
            "0F B7 8B 22 09 00 00 66 89 83 E4 06 00 00 0F B7 83 24 09 00 00 66 89 83 18 09 00 00",
            "conversion reads type and state before overwriting destination fields");
        CheckBytes(image, 0x186C20,
            "48 63 C2 48 69 D0 90 04 00 00",
            "selection cleanup ABI uses manager RCX and unit ID EDX");
        CheckBytes(image, 0x19A5DF,
            "4D 63 D0 48 8B D9 48 63 FA",
            "group cleanup ABI uses unit ID R8 and player ID EDX");
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
        int capture = runtime.IndexOf("var units = new List<EngineerSnapshot>()", StringComparison.Ordinal);
        int selection = runtime.IndexOf("EngineerCrewHandoffPolicy.TrySelect", capture, StringComparison.Ordinal);
        int firstWrite = runtime.IndexOf("WriteUInt16(device, AssignedEngineerIdsOffset", selection, StringComparison.Ordinal);
        int engineerWrite = runtime.IndexOf("WriteUInt32(engineer, AnimationTimerOffset", firstWrite, StringComparison.Ordinal);
        int helperCall = runtime.IndexOf("clearSelectedUnit(currentManager", engineerWrite, StringComparison.Ordinal);
        Check(capture >= 0 && selection > capture && firstWrite > selection,
            "all candidate reads and validation precede writes");
        Check(engineerWrite > firstWrite && helperCall > engineerWrite,
            "device commit precedes engineer transition and bookkeeping");
        Check(runtime.Contains("tentTickHook.Value.Hook.Trampoline();"), "Vanilla tent routine always called");
        Check(plugin.Contains("requireCurrentVersion: true"), "unknown native hashes fail closed");
        Check(runtime.Contains("WriteUInt16(device, PendingAiStateOffset, 0)"), "converted device becomes ready");
        Check(plugin.Contains("runtime?.PollRuntimeDiagnostics();"), "Unity update polls runtime diagnostics");
        Check(runtime.Contains("RUNTIME_VALIDATION_IMMEDIATE_PASS"), "immediate validation success marker");
        Check(runtime.Contains("RUNTIME_VALIDATION_PASS"), "eventual validation success marker");
        Check(runtime.Contains("RUNTIME_VALIDATION_FAILED"), "validation failure marker");
        Check(runtime.Contains("RUNTIME_VALIDATION_INCONCLUSIVE"), "inconclusive validation marker");
        Check(runtime.Contains("currentTick == lastDiagnosticTick"), "diagnostics poll at most once per game tick");
        Check(runtime.Contains("unchecked(currentTick - diagnostic.CommitTick)"), "tick timeout is wrap safe");
        Check(runtime.Contains("ReadUInt32(unit, GlobalIdOffset)"), "diagnostics validate global identities");
        Check(runtime.Contains("AllOriginalEngineersGone"), "diagnostics verify original engineers disappear");
        Check(runtime.Contains("CrewSlotsMatch"), "diagnostics verify device crew identities");
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
