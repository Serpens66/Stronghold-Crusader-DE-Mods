using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace AIDefensePatrolTest
{
    internal static class Program
    {
        private const string ExpectedHash =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

        private static int Main(string[] args)
        {
            try
            {
                string gameDir = args.Length == 1
                    ? args[0]
                    : @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
                string nativePath = Path.Combine(
                    gameDir,
                    "Stronghold Crusader Definitive Edition_Data",
                    "Plugins",
                    "x86_64",
                    "CrusaderDE.dll");

                AssertPolicy();
                AssertScriptExtenderVersion();
                AIDefensePatrolNativeDefinition.ValidateManagedLayout();

                byte[] file = File.ReadAllBytes(nativePath);
                AssertEqual(ExpectedHash, ComputeSha256(file), "canonical native SHA-256");
                AssertEqual(Shared.DebugLogHelper.CurrentNativeSha256, ExpectedHash, "shared native SHA-256");
                byte[] virtualImage = MapPeImage(file);
                AIDefensePatrolNativeDefinition.Validate(virtualImage);

                Console.WriteLine(
                    "PASS: policy, Script Extender 1.42.0 API, GameUnit layout, native hash, " +
                    "unique signature, 15-byte instruction span, branch targets, call targets, and incoming branches.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: " + exception);
                return 1;
            }
        }

        private static void AssertPolicy()
        {
            AssertTrue(DefenseAssignmentPolicy.NeedsCastleDefender(19, 20), "wall deficit");
            AssertFalse(DefenseAssignmentPolicy.NeedsCastleDefender(20, 20), "wall quota met");
            AssertFalse(DefenseAssignmentPolicy.NeedsCastleDefender(30, 20), "wall quota exceeded");
            AssertFalse(DefenseAssignmentPolicy.NeedsCastleDefender(0, 0), "zero values");
            AssertTrue(DefenseAssignmentPolicy.NeedsCastleDefender(0, int.MaxValue), "maximum DefWalls");
            AssertFalse(DefenseAssignmentPolicy.NeedsCastleDefender(int.MaxValue, int.MaxValue), "maximum quota met");
            AssertEqual(unchecked((uint)int.MaxValue), DefenseAssignmentPolicy.SelectComparisonValue(true), "castle comparison sentinel");
            AssertEqual(unchecked((uint)int.MinValue), DefenseAssignmentPolicy.SelectComparisonValue(false), "patrol comparison sentinel");
        }

        private static void AssertScriptExtenderVersion()
        {
            Version actual = typeof(SHCDESE.API.LowLevel.CrusaderLibrary).Assembly.GetName().Version;
            AssertEqual(new Version(1, 42, 0, 0), actual, "Script Extender assembly version");
        }

        private static byte[] MapPeImage(byte[] file)
        {
            int peOffset = ReadInt32(file, 0x3C);
            if (ReadUInt32(file, peOffset) != 0x00004550)
                throw new InvalidDataException("Invalid PE signature.");

            int sectionCount = ReadUInt16(file, peOffset + 6);
            int optionalHeaderSize = ReadUInt16(file, peOffset + 20);
            int optionalHeader = peOffset + 24;
            int sizeOfImage = ReadInt32(file, optionalHeader + 56);
            int sizeOfHeaders = ReadInt32(file, optionalHeader + 60);
            byte[] image = new byte[sizeOfImage];
            Buffer.BlockCopy(file, 0, image, 0, Math.Min(sizeOfHeaders, file.Length));

            int sectionTable = optionalHeader + optionalHeaderSize;
            for (int index = 0; index < sectionCount; index++)
            {
                int section = sectionTable + index * 40;
                int virtualAddress = ReadInt32(file, section + 12);
                int rawSize = ReadInt32(file, section + 16);
                int rawAddress = ReadInt32(file, section + 20);
                if (rawSize <= 0)
                    continue;
                if (rawAddress < 0 || rawAddress > file.Length - rawSize ||
                    virtualAddress < 0 || virtualAddress > image.Length - rawSize)
                {
                    throw new InvalidDataException("PE section lies outside the file or virtual image.");
                }
                Buffer.BlockCopy(file, rawAddress, image, virtualAddress, rawSize);
            }

            return image;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        private static int ReadUInt16(byte[] bytes, int offset) =>
            bytes[offset] | bytes[offset + 1] << 8;

        private static int ReadInt32(byte[] bytes, int offset) =>
            bytes[offset] |
            bytes[offset + 1] << 8 |
            bytes[offset + 2] << 16 |
            bytes[offset + 3] << 24;

        private static uint ReadUInt32(byte[] bytes, int offset) => unchecked((uint)ReadInt32(bytes, offset));

        private static void AssertTrue(bool value, string name)
        {
            if (!value)
                throw new InvalidOperationException(name + " was expected to be true.");
        }

        private static void AssertFalse(bool value, string name) => AssertTrue(!value, name);

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    $"{name} differs: expected={expected}, actual={actual}.");
            }
        }
    }
}
