using System;
using System.IO;
using System.Security.Cryptography;

namespace ExtraFeatures
{
    internal static class Program
    {
        private const string DllPath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        private static int assertions;

        private static int Main()
        {
            try
            {
                byte[] file = File.ReadAllBytes(DllPath);
                Check(Hash(file) == ElevatedMoatNativeContract.ReferenceSha256, "canonical DLL hash");
                ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(MapPeImage(file));
                CheckHeight(0, 0, 0);
                CheckHeight(8, 0, 0);
                CheckHeight(12, 4, 4);
                CheckHeight(13, 5, 6);
                CheckHeight(16, 8, 9);
                CheckHeight(255, 247, 248);
                Console.WriteLine($"PASS: elevated-moat height tests ({assertions} assertions).");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: " + exception);
                return 1;
            }
        }

        private static void CheckHeight(byte defaultHeight, byte expectedMoat, byte expectedDrawbridge)
        {
            Check(ElevatedMoatNativeContract.CalculateCompletedHeight(defaultHeight) == expectedMoat,
                $"moat height for {defaultHeight}");
            Check(ElevatedMoatNativeContract.CalculateDrawbridgeHeight(defaultHeight) == expectedDrawbridge,
                $"drawbridge height for {defaultHeight}");
            Check(ElevatedMoatNativeContract.CalculateRestoredHeight(defaultHeight) == defaultHeight,
                $"restored height for {defaultHeight}");
        }

        private static byte[] MapPeImage(byte[] file)
        {
            int pe = BitConverter.ToInt32(file, 0x3C);
            int count = BitConverter.ToUInt16(file, pe + 6);
            int optionalSize = BitConverter.ToUInt16(file, pe + 20);
            int optional = pe + 24;
            var image = new byte[BitConverter.ToInt32(file, optional + 56)];
            Buffer.BlockCopy(file, 0, image, 0, BitConverter.ToInt32(file, optional + 60));
            int table = optional + optionalSize;
            for (int index = 0; index < count; index++)
            {
                int header = table + index * 40;
                int virtualAddress = BitConverter.ToInt32(file, header + 12);
                int rawSize = BitConverter.ToInt32(file, header + 16);
                int raw = BitConverter.ToInt32(file, header + 20);
                if (rawSize > 0)
                    Buffer.BlockCopy(file, raw, image, virtualAddress, Math.Min(rawSize, file.Length - raw));
            }
            return image;
        }

        private static string Hash(byte[] value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", string.Empty);
        }

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
