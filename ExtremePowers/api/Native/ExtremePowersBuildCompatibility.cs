namespace ExtremePowers.API
{
    public static class ExtremePowersBuildCompatibility
    {
        public const string SupportedSteamBuild = "24816905";
        public const string SupportedSha256 = NativeBuildGuard.SupportedSha256;
        public static bool IsSupportedImage(byte[] image) => NativeBuildGuard.IsSupportedImage(image);
        public static bool HasExpectedNativeSignatures(byte[] peImage) => NativeExtremePowersSignatures.MatchesPeImage(peImage);
    }
}
