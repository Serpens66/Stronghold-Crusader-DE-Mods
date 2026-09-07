using System;
using System.IO;
using System.Security.Cryptography;

namespace FearFactorNeutralizationTest
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
                byte[] file = File.ReadAllBytes(DllPath);
                Check(Hash(file) == FearFactorNativeDefinition.ReferenceSha256, "canonical DLL hash");
                byte[] image = MapPeImage(file);
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
