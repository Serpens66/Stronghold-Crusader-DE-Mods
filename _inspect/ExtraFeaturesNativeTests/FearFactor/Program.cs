using System;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using RedBird.X64.Hooks;
using Iced.Intel;

namespace ExtraFeatures
{
    internal static class Program
    {
        private const string DllPath =
            @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        private static int assertions;

        private static int Main()
        {
            try
            {
                TestDamagePolicy();
                string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
                string vm = File.ReadAllText(Path.Combine(root, "ExtraFeatures", "src", "ExtraFeaturesViewModel.cs"));
                Check(vm.Contains("[SyncHostOnly] public bool EnableFearFactorNeutralization"), "host-only setting contract");
                Check(vm.Contains("private bool enableFearFactorNeutralization;") && vm.Contains("EnableFearFactorNeutralization = false;"), "default and reset disabled");
                string host = File.ReadAllText(Path.Combine(root, "ExtraFeatures", "src", "ExtraFeaturesRuntime.cs"));
                Check(host.Contains("FearFactorNeutralizationPolicy.IsEnabled(settings.EnableMod,") && host.Contains("settings.EnableFearFactorNeutralization, Shared.GameplayModActivationGate.IsAllowed"), "shared mode and mod activation gate");
                Check(host.Contains("nameof(ExtraFeaturesViewModel.EnableFearFactorNeutralization)"), "setting changes reconcile native activation");
                string runtime = File.ReadAllText(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "ExtraFeatures", "src", "FearFactorNeutralizationRuntime.cs"));
                Check(runtime.Contains("Registers = X64SmartCPUContextRegs.All"),
                    "UI callback preserves live volatile registers including render-cache R11");
                Check(runtime.Contains("Placement = OverwrittenInstructionPlacement.BeforeCallback"),
                    "fear load executes before neutralizing its result");
                Check(runtime.Contains("int originalDamage = damageHook.Original(unitManager, baseDamage, playerIndex);") &&
                    runtime.Contains("return originalDamage;"), "disabled damage delegates to Vanilla");
                Check(runtime.Contains("if (!enabled) return;"), "disabled overlay preserves loaded value");
                Check(runtime.Contains("pending.Dispose()") && runtime.Contains("DisplacedByteCount != FearFactorNativeDefinition.UiHookLength"), "partial installation rolls back");
                Check(runtime.Contains("FearFactorNeutralizationTest_Serp"), "legacy hook conflict guard");
                byte[] file = File.ReadAllBytes(DllPath);
                Check(Hash(file) == FearFactorNativeDefinition.ReferenceSha256, "canonical DLL hash");
                byte[] image = MapPeImage(file);
                TestActualRedBirdSpan(image);
                FearFactorNativeDefinition.Validate(image, FearFactorNativeDefinition.PreferredImageBase);
                Check(Shared.NativePatternResolver.FindUniquePattern(
                        image,
                        FearFactorNativeDefinition.DamageFunctionPattern,
                        "fear damage") == FearFactorNativeDefinition.DamageFunctionRva,
                    "damage function pattern is unique at the audited RVA");
                Check(Shared.NativePatternResolver.FindUniquePattern(
                        image,
                        FearFactorNativeDefinition.UiFearLoadPattern,
                        "fear overlay") == FearFactorNativeDefinition.UiPatternRva,
                    "unit-overlay pattern is unique at the audited RVA");

                byte[] changed = (byte[])image.Clone();
                changed[FearFactorNativeDefinition.UiFearLoadRva] ^= 1;
                Expect<InvalidOperationException>(
                    () => FearFactorNativeDefinition.Validate(
                        changed,
                        FearFactorNativeDefinition.PreferredImageBase),
                    "changed UI hook bytes fail closed");

                foreach (int offset in new[] {
                    FearFactorNativeDefinition.DamageFunctionRva + 20,
                    FearFactorNativeDefinition.DamageFunctionRva + FearFactorNativeDefinition.DamageFunctionLength,
                    FearFactorNativeDefinition.UiHookRva + FearFactorNativeDefinition.UiHookLength })
                {
                    byte[] invalid = (byte[])image.Clone();
                    invalid[offset] ^= 1;
                    Expect<InvalidOperationException>(() => FearFactorNativeDefinition.Validate(invalid,
                        FearFactorNativeDefinition.PreferredImageBase), "changed function/return/TEST contract fails closed");
                }
                byte[] incoming = (byte[])image.Clone();
                int source = FearFactorNativeDefinition.UnitOverlayFunctionRva;
                incoming[source] = 0xE9;
                Buffer.BlockCopy(BitConverter.GetBytes(FearFactorNativeDefinition.UiHookRva + 7 - source - 5),
                    0, incoming, source + 1, 4);
                Expect<InvalidOperationException>(() => FearFactorNativeDefinition.Validate(incoming,
                    FearFactorNativeDefinition.PreferredImageBase), "incoming jump into UI span is rejected");

                Console.WriteLine($"PASS: Fear Factor Neutralization tests ({assertions} assertions).");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: " + exception);
                return 1;
            }
        }

        private static void TestDamagePolicy()
        {
            foreach (bool mod in new[] { false, true })
                foreach (bool setting in new[] { false, true })
                    foreach (bool allowed in new[] { false, true })
                        Check(FearFactorNeutralizationPolicy.IsEnabled(mod, setting, allowed) == (mod && setting && allowed),
                            "activation truth table");
            const int damage = 100;
            Check(FearFactorNeutralizationPolicy.CalculateVanillaDamage(damage, -5) == 75,
                "Vanilla fear -5 damage");
            Check(FearFactorNeutralizationPolicy.CalculateVanillaDamage(damage, 0) == 100,
                "Vanilla fear 0 damage");
            Check(FearFactorNeutralizationPolicy.CalculateVanillaDamage(damage, 5) == 125,
                "Vanilla fear +5 damage");
            Check(FearFactorNeutralizationPolicy.CalculateNeutralDamage(damage) == 100,
                "neutral damage ignores fear");
        }

        private static void TestActualRedBirdSpan(byte[] image)
        {
            // Only decode a copied byte buffer: never generate or enable a hook in this test.
            IntPtr buffer = Marshal.AllocHGlobal(64);
            try
            {
                Marshal.Copy(image, FearFactorNativeDefinition.UiHookRva, buffer, 64);
                using (var hook = new X64InlineHook(unchecked((ulong)buffer.ToInt64()),
                    FearFactorNativeDefinition.UiHookLength))
                {
                    Check(hook.DisplacedByteCount == 14, "installed RedBird displaces exactly IMUL+MOV");
                    Check(FearFactorNativeDefinition.UiHookRva + hook.DisplacedByteCount == 0x1A1A00,
                        "callback returns before TEST EAX,EAX");
                }
                Marshal.Copy(image, FearFactorNativeDefinition.UiFearLoadRva, buffer, 64);
                using (var oldHook = new X64InlineHook(unchecked((ulong)buffer.ToInt64()), 7))
                    Check(oldHook.DisplacedByteCount == 14,
                        "regression: requesting seven bytes also displaces TEST/JE/IMUL");

                var decoder = Decoder.Create(64, new ByteArrayCodeReader(
                    new byte[] { 0x85, 0xC0, 0x74, 0x2A }));
                decoder.IP = FearFactorNativeDefinition.PreferredImageBase + 0x1A1A00;
                decoder.Decode(out Instruction test);
                decoder.Decode(out Instruction branch);
                Check(test.Mnemonic == Mnemonic.Test && branch.Mnemonic == Mnemonic.Je &&
                    branch.NearBranchTarget == FearFactorNativeDefinition.PreferredImageBase + 0x1A1A2E,
                    "zeroed fear reaches the neutral healthbar branch for either original sign");
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        private static byte[] MapPeImage(byte[] file)
        {
            int peOffset = ReadInt32(file, 0x3C);
            int sectionCount = ReadUInt16(file, peOffset + 6);
            int optionalHeaderSize = ReadUInt16(file, peOffset + 20);
            int optionalHeader = peOffset + 24;
            int imageSize = ReadInt32(file, optionalHeader + 56);
            int headersSize = ReadInt32(file, optionalHeader + 60);
            byte[] image = new byte[imageSize];
            Buffer.BlockCopy(file, 0, image, 0, Math.Min(headersSize, file.Length));

            int sectionTable = optionalHeader + optionalHeaderSize;
            for (int index = 0; index < sectionCount; index++)
            {
                int section = sectionTable + index * 40;
                int virtualAddress = ReadInt32(file, section + 12);
                int rawSize = ReadInt32(file, section + 16);
                int rawOffset = ReadInt32(file, section + 20);
                if (rawSize > 0)
                    Buffer.BlockCopy(file, rawOffset, image, virtualAddress, rawSize);
            }
            return image;
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        private static int ReadUInt16(byte[] bytes, int offset) =>
            bytes[offset] | bytes[offset + 1] << 8;

        private static int ReadInt32(byte[] bytes, int offset) =>
            bytes[offset] |
            bytes[offset + 1] << 8 |
            bytes[offset + 2] << 16 |
            bytes[offset + 3] << 24;

        private static void Check(bool condition, string name)
        {
            assertions++;
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + name);
        }

        private static void Expect<T>(Action action, string name) where T : Exception
        {
            assertions++;
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            throw new InvalidOperationException("Expected " + typeof(T).Name + ": " + name);
        }
    }
}
