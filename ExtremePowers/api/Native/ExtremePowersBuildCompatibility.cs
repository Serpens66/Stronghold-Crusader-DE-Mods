namespace ExtremePowers.API
{
    public static class ExtremePowersBuildCompatibility
    {
        private static readonly byte[] DispatcherSignature = { 0x48,0x89,0x5C,0x24,0x10,0x48,0x89,0x6C,0x24,0x18,0x48,0x89,0x74,0x24,0x20,0x57,0x48,0x83,0xEC,0x40 };
        private static readonly byte[] SelectionSignature = { 0x40,0x53,0x48,0x83,0xEC,0x20,0x8B,0x05 };
        private static readonly byte[] HealSignature = { 0x48,0x89,0x5C,0x24,0x08,0x48,0x89,0x6C,0x24,0x10,0x48,0x89,0x74,0x24,0x18,0x48 };
        private static readonly byte[] VolleySignature = { 0x44,0x89,0x4C,0x24,0x20,0x44,0x89,0x44,0x24,0x18,0x48,0x89,0x4C,0x24,0x08,0x53 };
        private static readonly byte[] GoldAdvanceSignature = { 0x4C,0x63,0x81,0x48,0x9C,0x00,0x00,0x33,0xC0,0x42,0x0F,0xB7,0x54,0x41,0x08,0x41 };
        public const string SupportedSteamBuild = "24816905";
        public const string SupportedSha256 = NativeBuildGuard.SupportedSha256;
        public static bool IsSupportedImage(byte[] image) => NativeBuildGuard.IsSupportedImage(image);
        public static bool HasExpectedNativeSignatures(byte[] peImage) =>
            MatchesAtRva(peImage, 0xCD630, DispatcherSignature) && MatchesAtRva(peImage, 0x105510, SelectionSignature) &&
            MatchesAtRva(peImage, 0xE1E70, HealSignature) && MatchesAtRva(peImage, 0xDD6C0, VolleySignature) &&
            MatchesAtRva(peImage, 0x7530, GoldAdvanceSignature);

        private static bool MatchesAtRva(byte[] image, int rva, byte[] expected)
        {
            int offset = RvaToFileOffset(image, rva);
            if (offset < 0 || offset + expected.Length > (image?.Length ?? 0)) return false;
            for (int i = 0; i < expected.Length; i++) if (image[offset + i] != expected[i]) return false;
            return true;
        }

        private static int RvaToFileOffset(byte[] image, int rva)
        {
            if (image == null || image.Length < 0x40) return -1;
            int pe = System.BitConverter.ToInt32(image, 0x3C);
            if (pe < 0 || pe + 24 > image.Length || System.BitConverter.ToUInt32(image, pe) != 0x00004550) return -1;
            int sections = System.BitConverter.ToUInt16(image, pe + 6);
            int table = pe + 24 + System.BitConverter.ToUInt16(image, pe + 20);
            for (int index = 0; index < sections; index++)
            {
                int header = table + index * 40;
                if (header < 0 || header + 40 > image.Length) return -1;
                int virtualSize = System.BitConverter.ToInt32(image, header + 8);
                int virtualAddress = System.BitConverter.ToInt32(image, header + 12);
                int rawSize = System.BitConverter.ToInt32(image, header + 16);
                int rawAddress = System.BitConverter.ToInt32(image, header + 20);
                int size = System.Math.Max(virtualSize, rawSize);
                if (rva >= virtualAddress && rva < virtualAddress + size) return rawAddress + rva - virtualAddress;
            }
            return -1;
        }
    }
}
