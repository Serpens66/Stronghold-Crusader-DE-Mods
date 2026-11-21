using APIShared.Maps;
using System;
using System.IO;

namespace BugfixesAndQoL
{
    internal static class LongMapFileNamesTests
    {
        internal static void Run(Action<bool, string> check)
        {
            // The real native basename buffer has room for 63 UTF-16 units plus its terminator.
            const string directory = @"C:\Maps";
            string exactName = new string('a', 59) + ".map";
            string oneTooLong = new string('a', 60) + ".map";
            var exact = LongMapFileNames.Decide(Path.Combine(directory, exactName), canAlias: false);
            check(exact.NativeFileName == null && exact.Rejection == null, "63 UTF-16 units fit the original native filename");

            var supported = LongMapFileNames.Decide(Path.Combine(directory, oneTooLong), canAlias: true);
            string alias = supported.NativeFileName;
            check(supported.Rejection == null && alias != null && alias != oneTooLong && alias.Length <= 63 &&
                alias.EndsWith(".map", StringComparison.Ordinal), "64 UTF-16 units get a short native-only map name");
            var otherRoot = LongMapFileNames.Decide(Path.Combine(@"D:\Steam\workshop", oneTooLong), canAlias: true);
            var otherMap = LongMapFileNames.Decide(Path.Combine(directory, "b" + oneTooLong), canAlias: true);
            check(otherRoot.NativeFileName == alias && otherMap.NativeFileName != alias,
                "host and client derive the same alias from the filename despite distinct local roots");

            var unsupported = LongMapFileNames.Decide(Path.Combine(directory, oneTooLong), canAlias: false);
            check(unsupported.NativeFileName == null && unsupported.Rejection?.Contains("64") == true,
                "an unsafe name is reported when there is no native alias hook");

            string unicodeName = new string('a', 58) + "\U0001F3F0.map";
            check(LongMapFileNames.Decide(Path.Combine(directory, unicodeName), canAlias: true).NativeFileName != null,
                "a supplementary-plane character consumes two native UTF-16 units");

            // A safe basename must not excuse overflow of the separate absolute-path buffer.
            string fullLimit = Path.Combine(@"C:\", new string('d', 192), exactName);
            check(fullLimit.Length == 259, "path fixture reaches the native full-path boundary");
            check(LongMapFileNames.Decide(fullLimit, canAlias: true).Rejection == null,
                "259 UTF-16 units fit the full path buffer");
            string overlong = Path.Combine(@"C:\", new string('d', 193), exactName);
            var pathFailure = LongMapFileNames.Decide(overlong, canAlias: true);
            check(pathFailure.NativeFileName == null && pathFailure.Rejection?.Contains(overlong) == true,
                "a 260-unit full path is rejected even when the basename fits and aliases are available");
        }
    }
}

// Supply game boundaries only to compile the actual managed API contracts and read their decisions.
namespace APIShared.Maps
{
    public static partial class MapLoadPolicy
    {
        internal static bool CanAlias => false;

        private static bool TryInstallNativeAlias(out string diagnostic)
        {
            diagnostic = "Native hooks are outside this test.";
            return false;
        }
    }
}

namespace APIShared.Internal
{
    internal static class UnityMainThreadDispatch
    {
        internal static bool IsMainThread => true;
    }
}
