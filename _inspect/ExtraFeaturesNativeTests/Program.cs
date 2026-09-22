using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ExtraFeatures
{
    internal static class Program
    {
        private const string DllPath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        private const string SupportedBuildHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private static int assertions;

        private static int Main()
        {
            try
            {
                byte[] file = File.ReadAllBytes(DllPath);
                Check(Hash(file) == SupportedBuildHash, "canonical DLL hash");
                byte[] image = MapPeImage(file);
                TestPermanentRuntimeContracts(FindWorkspace());
                TestNativeTargetMap(image);
                Console.WriteLine($"PASS: ExtraFeatures native tests ({assertions} assertions).");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex);
                return 1;
            }
        }

        private static void TestPermanentRuntimeContracts(string workspace)
        {
            string sourceDirectory = Path.Combine(workspace, "ExtraFeatures", "src");
            string[] sourcePaths = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.TopDirectoryOnly);
            string production = string.Join("\n", sourcePaths.Select(File.ReadAllText));
            string project = File.ReadAllText(Path.Combine(workspace, "ExtraFeatures", "ExtraFeatures.csproj"));
            string plugin = File.ReadAllText(Path.Combine(sourceDirectory, "ExtraFeaturesPlugin.cs"));
            string runtime = File.ReadAllText(Path.Combine(sourceDirectory, "ExtraFeaturesRuntime.cs"));
            string plague = File.ReadAllText(Path.Combine(sourceDirectory, "PlagueDurationPatch.cs"));
            string monk = File.ReadAllText(Path.Combine(sourceDirectory, "MonkAlwaysRunPatch.cs"));
            string manifest = File.ReadAllText(Path.Combine(workspace, "ExtraFeatures", "info.json"));
            Match minimumMatch = Regex.Match(manifest,
                "\\\"MinimumScriptExtenderVersion\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"");
            string minimumExtenderVersion = minimumMatch.Success ? minimumMatch.Groups[1].Value : string.Empty;

            Check(!production.Contains("CodePatch.Write(") &&
                  !production.Contains(".Hook.Enable(") &&
                  !production.Contains(".Hook.Disable("),
                "runtime never rewrites published executable code");
            Check(!production.Contains("Zhuqiaomon") && !project.Contains("Zhuqiaomon") &&
                  !production.Contains("HookRef<") && !production.Contains(".Unload()") &&
                  !production.Contains("Value.Hook.Trampoline"),
                "obsolete hook backends and teardown calls stay removed");
            Check(project.Contains("<Reference Include=\"RedBird.Abstractions\"") &&
                  project.Contains("<Reference Include=\"RedBird.Core\"") &&
                  project.Contains("<Reference Include=\"RedBird.X64\"") &&
                  !project.Contains("PolyHook2.NET"),
                "project uses the Script Extender RedBird assemblies");
            Check((string.IsNullOrEmpty(minimumExtenderVersion) ||
                   plugin.Contains($"[BepInDependency(ScriptExtenderGuid, \"{minimumExtenderVersion}\")]") ) &&
                  plugin.Contains("OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)"),
                "dependency matches the manifest and consumes the load context");
            Check(runtime.Contains("context.ModuleHandle") && runtime.Contains("context.Memory") &&
                  runtime.Contains("context.Region") && !production.Contains("nativeRegion.Dispose()") &&
                  !production.Contains("context.Region.Dispose()"),
                "native load-context values are borrowed without disposing the ScanRegion");
            Check(plague.Contains("LifetimeComparisonDisplacedBytes = 17") &&
                  plague.Contains("Volatile.Write(ref expectedLifetime") &&
                  plague.Contains("lifetimeComparisonHook.Hook.DisplacedByteCount") &&
                  plague.Contains("RollbackUnpublishedLifetimeComparisonHook"),
                "plague lifetime uses one permanent comparison hook and an atomic logical value");
            Check(monk.Contains("using RedBird.X64.Extensions;") &&
                  monk.Contains("assembler.AddUnrestrictedJmp(") &&
                  monk.Contains("hookSize: HookSize"),
                "Monk generator retains audited unrestricted jumps and hook boundary");
            Check(Regex.Matches(production, @"RollbackUnpublished\w*\(").Count > 0 &&
                  !Regex.IsMatch(production, @"(?:OnDestroy|OnDisable|OnApplicationQuit)\s*\([^)]*\)[\s\S]{0,500}?\.Dispose\s*\("),
                "rollback is limited to unpublished initialization candidates");
        }

        private static string FindWorkspace()
        {
            DirectoryInfo directory = new DirectoryInfo(Environment.CurrentDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "ExtraFeatures")))
                    return directory.FullName;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Workspace root was not found.");
        }

        private static void TestNativeTargetMap(byte[] image)
        {
            CheckFunction(image, 0x504F0, 304, "2D6CB2745E0E6619C9D40DDD4F07C70CFF494FE8DECCC6175658C395EBD00393", "AI flag disease handler");
            CheckFunction(image, 0x51790, 2774, "69731F77776995C9FC452A7A9A41408385B757B461F0E7FAB76E291BE64C3ECF", "AIV build-step handler");
            CheckFunction(image, 0x5CD90, 1077, "099D5E8B4AB0B93EB2BE39501D06AE0FC38F481035AF50650654F6F233B23A17", "AIV placement handler");
            CheckFunction(image, 0x9A080, 410, "902372F40007B9FBE5F14FB7C48366F4090A261E2DE21463698B15FFDC7F704B", "Disease update handler");
            CheckFunction(image, 0x9F700, 525, "D4C059E5AED1B7FFCFA334E0A361EDA4DC7B49EF1FBAE9F8972E231FC4A0BC6A", "apothecary Disease search handler");
            CheckFunction(image, 0xCEB10, 31, "5B45784D8B227D4BEB1AA822E6B12523BD9A0825EFA17764909A037E613C6C6A", "AI buy-price helper");
            CheckFunction(image, 0xCEB90, 31, "D428FAE5C2A3BED0B48195B2661F56550E5B53F6E8EE9A603FADA56DAEE8F670", "AI sell-price helper");
            CheckFunction(image, 0x151090, 3969, "785E5FB37D378726A55C84609FFD307CDC81865B964BB631EB98A3EBE5B1CB58", "Monk handler");
            CheckPattern(image, 0x9A164, "41 0F BF 44 18 18 03 D0 B8 ?? ?? ?? ?? 41 89 54 18 14 66 41 39 84 18 D0 00 00 00 7C 06", "plague lifetime and comparison span");
            CheckPattern(image, 0x9F86B, "83 3D ?? ?? ?? ?? 1E 7F ?? 0F BF 4B 1C 48 8D 15 ?? ?? ?? ?? 44 0F BF 4B 1A", "apothecary distance hook span");
            CheckPattern(image, 0x151436, "66 46 39 B4 2B 14 09 00 00 75 22 66 46 39 B4 2B 9E 09 00 00 74 17", "Monk movement hook and following branch");
            Check(ReadInt32(image, 0x9A16D) == 800, "plague lifetime immediate");
            Check(image[0x9F871] == 30, "apothecary Vanilla distance immediate");
        }

        private static void CheckFunction(byte[] image, int rva, int size, string expectedHash, string label)
        {
            byte[] bytes = new byte[size];
            Buffer.BlockCopy(image, rva, bytes, 0, size);
            Check(Hash(bytes) == expectedHash, label + " function hash");
        }

        private static void CheckPattern(byte[] image, int rva, string pattern, string label)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Check(rva >= 0 && rva <= image.Length - tokens.Length, label + " bounds");
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] == "??")
                    continue;
                Check(image[rva + i] == Convert.ToByte(tokens[i], 16), label + $" byte +0x{i:X}");
            }
        }

        private static byte[] MapPeImage(byte[] file)
        {
            int pe = ReadInt32(file, 0x3C), count = ReadUInt16(file, pe + 6), optionalSize = ReadUInt16(file, pe + 20), optional = pe + 24;
            int imageSize = ReadInt32(file, optional + 56), headers = ReadInt32(file, optional + 60);
            var image = new byte[imageSize];
            Buffer.BlockCopy(file, 0, image, 0, Math.Min(headers, file.Length));
            int table = optional + optionalSize;
            for (int i = 0; i < count; i++)
            {
                int h = table + i * 40, virtualAddress = ReadInt32(file, h + 12), rawSize = ReadInt32(file, h + 16), raw = ReadInt32(file, h + 20);
                if (rawSize > 0) Buffer.BlockCopy(file, raw, image, virtualAddress, Math.Min(rawSize, file.Length - raw));
            }
            return image;
        }

        private static int ReadInt32(byte[] value, int offset) => value[offset] | value[offset + 1] << 8 | value[offset + 2] << 16 | value[offset + 3] << 24;
        private static int ReadUInt16(byte[] value, int offset) => value[offset] | value[offset + 1] << 8;
        private static string Hash(byte[] value) { using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", ""); }
        private static void Check(bool condition, string message) { assertions++; if (!condition) throw new InvalidOperationException(message); }
    }
}
