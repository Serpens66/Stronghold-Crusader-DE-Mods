using System.Text.RegularExpressions;

const int removeRva = 0x123EA0;
const int removeSize = 312;
const string expectedNativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

int assertions = 0;
string root = FindRepositoryRoot();
string nativePath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";

byte[] image = File.ReadAllBytes(nativePath);
Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image)) == expectedNativeHash,
    "canonical native hash");

byte[] signature = Convert.FromHexString(
    "48895C2408488974241048897C241841564883EC204C63CA4C8D3541C1EDFF4963F8418BC14969F19004000099488BD9");
byte[] tail = Convert.FromHexString(
    "FF8BD7488BCB488B5C2430488B742438488B7C24404883C420415EE948A8FFFF");
int rawOffset = RvaToRawOffset(image, removeRva);
Check(image.AsSpan(rawOffset, signature.Length).SequenceEqual(signature), "exact native signature");
Check(CountOccurrences(image, signature) == 1, "unique native signature");
Check(image.AsSpan(rawOffset + removeSize - tail.Length, tail.Length).SequenceEqual(tail),
    "exact native tail and RET");
Check(image[rawOffset + removeSize] == 0xCC, "exact native function boundary");

string adapter = Read("AIDefense", "src", "AIDefenseTribeUnassignAdapter.cs");
string runtime = Read("AIDefense", "src", "AIDefenseRuntime.cs");
string plugin = Read("AIDefense", "src", "AIDefensePlugin.cs");

Check(adapter.Contains("RemoveUnitFromTribeRva = 0x123EA0", StringComparison.Ordinal), "adapter RVA");
Check(adapter.Contains("RemoveUnitFromTribeSize = 312", StringComparison.Ordinal), "adapter function size");
Check(adapter.Contains("removeUnitFromTribe(manager, unitId, tribeId);", StringComparison.Ordinal),
    "native unitId/tribeId argument order");
Check(!adapter.Contains("unitId + 1", StringComparison.Ordinal) &&
      !adapter.Contains("unitId - 1", StringComparison.Ordinal) &&
      !adapter.Contains("tribeId + 1", StringComparison.Ordinal) &&
      !adapter.Contains("tribeId - 1", StringComparison.Ordinal),
    "one-based IDs cross the native boundary unchanged");
Check(adapter.Contains("unit->r_TribeId != tribeId", StringComparison.Ordinal), "membership precondition");
Check(adapter.Contains("unit->r_TribeId == tribeId", StringComparison.Ordinal), "membership postcondition");
Check(adapter.Contains("catch (Exception exception)", StringComparison.Ordinal), "native exception path");
Check(adapter.Contains("manager == IntPtr.Zero", StringComparison.Ordinal), "null-manager rejection");
Check(adapter.Contains("memory[RemoveUnitFromTribeRva + RemoveUnitFromTribeSize] != 0xCC", StringComparison.Ordinal),
    "runtime end-boundary validation");

Check(Regex.Matches(runtime, @"tribeUnassignAdapter\.TryUnassign\(").Count == 5,
    "all five AIDefense call sites use the adapter");
Check(!runtime.Contains("GameTribeManagerAPI.Instance.UnassignUnit", StringComparison.Ordinal) &&
      !runtime.Contains("unexpectedTribeApi.UnassignUnit", StringComparison.Ordinal),
    "broken public wrapper is absent");
Check(runtime.Contains("if (!assignmentIssued || unit->r_TribeId != privateTribeId)", StringComparison.Ordinal),
    "failed assignment detected");
Check(runtime.Contains("CleanupFailedPrivateTribeCreation(defender, unit);", StringComparison.Ordinal),
    "failed assignment enters rollback cleanup");
int cleanupStart = runtime.IndexOf("private void CleanupFailedPrivateTribeCreation", StringComparison.Ordinal);
int cleanupEnd = runtime.IndexOf("private bool RecordPrivateTribeFailure", cleanupStart, StringComparison.Ordinal);
Check(cleanupStart >= 0 && cleanupEnd > cleanupStart, "rollback cleanup body found");
string cleanup = runtime.Substring(cleanupStart, cleanupEnd - cleanupStart);
int cleanupUnassign = cleanup.IndexOf("tribeUnassignAdapter.TryUnassign", StringComparison.Ordinal);
int cleanupDelete = cleanup.IndexOf("DeleteTribeSafe", StringComparison.Ordinal);
int cleanupClear = cleanup.IndexOf("ClearPrivateTribeTracking", StringComparison.Ordinal);
Check(cleanupUnassign >= 0 && cleanupDelete > cleanupUnassign && cleanupClear > cleanupDelete,
    "rollback unassigns, deletes, then clears tracking");

Check(plugin.Contains("[BepInDependency(ScriptExtenderGuid, \"2.0.2\")]", StringComparison.Ordinal),
    "exact SHCDESE dependency");
Check(plugin.Contains("OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)", StringComparison.Ordinal),
    "LoadContext callback");
Check(plugin.IndexOf("InstallNative(context);", StringComparison.Ordinal) <
      plugin.IndexOf("persistentRuntime?.Apply();", StringComparison.Ordinal),
    "native validation precedes runtime activation");

Check(IsValidGameId(1, 4500), "first game ID accepted");
Check(IsValidGameId(4500, 4500), "last game ID accepted");
Check(!IsValidGameId(0, 4500), "zero game ID rejected");
Check(!IsValidGameId(4501, 4500), "past-last game ID rejected");

Console.WriteLine($"PASS: AIDefense 2.0.2 unassign contract ({assertions} assertions).");
return;

void Check(bool condition, string name)
{
    assertions++;
    if (!condition)
        throw new InvalidOperationException($"FAIL: {name}");
}

string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

static bool IsValidGameId(int gameId, int arrayLength) => gameId > 0 && gameId <= arrayLength;

string FindRepositoryRoot()
{
    DirectoryInfo? directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory != null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "AIDefense")) &&
            File.Exists(Path.Combine(directory.FullName, "UpdatePlan-SHCDESE-2.0.2.md")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Repository root was not found.");
}

static int CountOccurrences(byte[] imageBytes, byte[] pattern)
{
    int count = 0;
    for (int offset = 0; offset <= imageBytes.Length - pattern.Length; offset++)
    {
        if (imageBytes.AsSpan(offset, pattern.Length).SequenceEqual(pattern))
            count++;
    }
    return count;
}

static int RvaToRawOffset(byte[] imageBytes, int rva)
{
    int peOffset = BitConverter.ToInt32(imageBytes, 0x3C);
    int sectionCount = BitConverter.ToUInt16(imageBytes, peOffset + 6);
    int optionalHeaderSize = BitConverter.ToUInt16(imageBytes, peOffset + 20);
    int sectionTable = peOffset + 24 + optionalHeaderSize;
    for (int index = 0; index < sectionCount; index++)
    {
        int header = sectionTable + index * 40;
        int virtualSize = BitConverter.ToInt32(imageBytes, header + 8);
        int virtualAddress = BitConverter.ToInt32(imageBytes, header + 12);
        int rawSize = BitConverter.ToInt32(imageBytes, header + 16);
        int rawAddress = BitConverter.ToInt32(imageBytes, header + 20);
        int sectionSize = Math.Max(virtualSize, rawSize);
        if (rva >= virtualAddress && rva < virtualAddress + sectionSize)
            return checked(rawAddress + rva - virtualAddress);
    }
    throw new InvalidOperationException($"RVA 0x{rva:X} is outside PE sections.");
}
