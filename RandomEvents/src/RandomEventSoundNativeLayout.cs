using Shared;
using System;

namespace RandomEvents
{
    internal readonly struct RandomEventSoundNativeResolution
    {
        public RandomEventSoundNativeResolution(
            int callsiteRva,
            int soundManagerRva,
            int soundHandlerRva,
            string method)
        {
            CallsiteRva = callsiteRva;
            SoundManagerRva = soundManagerRva;
            SoundHandlerRva = soundHandlerRva;
            Method = method;
        }

        public int CallsiteRva { get; }
        public int SoundManagerRva { get; }
        public int SoundHandlerRva { get; }
        public string Method { get; }
    }

    internal static class RandomEventSoundNativeLayout
    {
        internal const int ReferenceMarriageCallsiteRva = 0x104A20;
        internal const int ReferenceSoundManagerRva = 0x64C4490;
        internal const int ReferenceSoundHandlerRva = 0x2940;

        internal const string MarriageCallsitePattern =
            "4C 8B 0D ?? ?? ?? ?? 48 8D 05 ?? ?? ?? ?? 44 8B C2 48 89 44 24 20 " +
            "BA C9 00 00 00 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? BA CF 00 00 00 " +
            "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ??";

        internal const string SoundHandlerPattern =
            "41 B9 40 00 00 00 48 8D 0D ?? ?? ?? ?? 45 8D 41 24 E9 ?? ?? ?? ??";

        public static RandomEventSoundNativeResolution Resolve(
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            NativeResolution callsite = NativePatternResolver.ResolveUnique(
                memory,
                MarriageCallsitePattern,
                ReferenceMarriageCallsiteRva,
                referenceHashMatches,
                "marriage event sound callsite");
            int soundManagerRva = NativePatternResolver.ResolveRelativeTarget(
                memory,
                callsite.Rva + 47,
                callsite.Rva + 51);
            int soundHandlerRva = NativePatternResolver.ResolveRelativeTarget(
                memory,
                callsite.Rva + 52,
                callsite.Rva + 56);

            if (soundManagerRva <= 0 || soundManagerRva >= memory.Length)
                throw new InvalidOperationException($"marriage sound manager RVA 0x{soundManagerRva:X} is outside the module image.");
            if (!NativePatternResolver.MatchesPatternAt(memory, soundHandlerRva, SoundHandlerPattern))
                throw new InvalidOperationException($"marriage sound handler RVA 0x{soundHandlerRva:X} failed semantic validation.");
            if (referenceHashMatches &&
                (callsite.Rva != ReferenceMarriageCallsiteRva ||
                 soundManagerRva != ReferenceSoundManagerRva ||
                 soundHandlerRva != ReferenceSoundHandlerRva))
            {
                throw new InvalidOperationException(
                    $"reference marriage sound targets differ: callsite=0x{callsite.Rva:X}, " +
                    $"manager=0x{soundManagerRva:X}, handler=0x{soundHandlerRva:X}.");
            }

            return new RandomEventSoundNativeResolution(
                callsite.Rva,
                soundManagerRva,
                soundHandlerRva,
                callsite.Method);
        }
    }
}
