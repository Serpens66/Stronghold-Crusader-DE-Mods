using System;

namespace EnemyGatePathfindingTest
{
    internal enum PathfindingGlobalTableKind
    {
        Profile,
        ConnectionPermission
    }

    internal readonly struct PathfindingGlobalMismatch
    {
        internal PathfindingGlobalMismatch(
            PathfindingGlobalTableKind kind,
            int connectionClass,
            int unitType,
            int expected,
            int actual)
        {
            Kind = kind;
            ConnectionClass = connectionClass;
            UnitType = unitType;
            Expected = expected;
            Actual = actual;
        }

        internal PathfindingGlobalTableKind Kind { get; }
        internal int ConnectionClass { get; }
        internal int UnitType { get; }
        internal int Expected { get; }
        internal int Actual { get; }
    }

    internal readonly struct PathfindingGlobalsComparison
    {
        internal PathfindingGlobalsComparison(
            int profileLength,
            int permissionLength,
            int profileMismatches,
            int permissionMismatches,
            int invalidValues,
            int largeProfiles,
            int defaultProfiles,
            int[] allowedByClass,
            ulong actualFingerprint,
            ulong expectedFingerprint,
            PathfindingGlobalMismatch[] samples)
        {
            ProfileLength = profileLength;
            PermissionLength = permissionLength;
            ProfileMismatches = profileMismatches;
            PermissionMismatches = permissionMismatches;
            InvalidValues = invalidValues;
            LargeProfiles = largeProfiles;
            DefaultProfiles = defaultProfiles;
            AllowedByClass = allowedByClass ?? Array.Empty<int>();
            ActualFingerprint = actualFingerprint;
            ExpectedFingerprint = expectedFingerprint;
            Samples = samples ?? Array.Empty<PathfindingGlobalMismatch>();
        }

        internal int ProfileLength { get; }
        internal int PermissionLength { get; }
        internal int ProfileMismatches { get; }
        internal int PermissionMismatches { get; }
        internal int InvalidValues { get; }
        internal int LargeProfiles { get; }
        internal int DefaultProfiles { get; }
        internal int[] AllowedByClass { get; }
        internal ulong ActualFingerprint { get; }
        internal ulong ExpectedFingerprint { get; }
        internal PathfindingGlobalMismatch[] Samples { get; }

        internal bool HasExpectedLengths =>
            ProfileLength == PathfindingGlobalsBaseline.UnitTypeCount &&
            PermissionLength == PathfindingGlobalsBaseline.PermissionCount;

        internal bool MatchesCanonical =>
            HasExpectedLengths && ProfileMismatches == 0 &&
            PermissionMismatches == 0 && InvalidValues == 0;
    }

    // Canonical process-start tables from CrusaderDE.dll FBCB9319. The values are
    // represented as compact 90-bit rows because both native tables contain Int32
    // Boolean/profile values zero or one. This class only compares; it never writes.
    internal static class PathfindingGlobalsBaseline
    {
        internal const int UnitTypeCount = 90;
        internal const int ConnectionClassCount = 6;
        internal const int PermissionCount = UnitTypeCount * ConnectionClassCount;
        internal const int ConnectionClassRowByteLength = 0x168;
        internal const int UnknownClassTableRva = 0x32BC48;
        internal const int FirstPublicClassTableRva = 0x32BDB0;
        internal const int LastPublicClassTableRva = 0x32C4B8;
        internal const ulong CanonicalFingerprint = 0xC63EAAE3BB01A032UL;

        private static readonly ulong[] ExpectedOneBits =
        {
            // Unit-type pathfinding profiles: 0=Large, 1=Default.
            0xE3FFFC7FCFFFFFFFUL, 0x0000000001F79BFFUL,
            // Connection classes 1..6, two UInt64 words per 90-entry row.
            0x0000000005400000UL, 0x0000000000040000UL,
            0x0000000005C00000UL, 0x0000000000378FC0UL,
            0xFFFFFFFFFFFFFFFEUL, 0x0000000003FFFFFFUL,
            0xFFFFFFFFFFFFFFFEUL, 0x0000000003FFFFFFUL,
            0xFFFFFFFFFFFFFFFEUL, 0x0000000003FFFFFFUL,
            0xFFFC0FFFFFFFFFFEUL, 0x0000000003FFFFFFUL
        };

        internal static int GetExpectedProfile(int unitType) =>
            GetExpectedRowValue(0, unitType);

        internal static int GetExpectedPermission(int connectionClass, int unitType)
        {
            if ((uint)(connectionClass - 1) >= ConnectionClassCount)
                throw new ArgumentOutOfRangeException(nameof(connectionClass));
            return GetExpectedRowValue(connectionClass, unitType);
        }

        internal static PathfindingGlobalsComparison Compare(
            ReadOnlySpan<int> profiles,
            ReadOnlySpan<int> permissions,
            int maximumSamples = 12)
        {
            if (maximumSamples < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumSamples));

            int profileMismatches = 0;
            int permissionMismatches = 0;
            int invalidValues = 0;
            int largeProfiles = 0;
            int defaultProfiles = 0;
            var allowedByClass = new int[ConnectionClassCount];
            var samples = new PathfindingGlobalMismatch[Math.Min(
                maximumSamples,
                Math.Max(0, profiles.Length) + Math.Max(0, permissions.Length))];
            int sampleCount = 0;
            ulong actualFingerprint = 1469598103934665603UL;
            ulong expectedFingerprint = 1469598103934665603UL;

            int profileCount = Math.Min(profiles.Length, UnitTypeCount);
            for (int unitType = 0; unitType < profileCount; unitType++)
            {
                int actual = profiles[unitType];
                int expected = GetExpectedProfile(unitType);
                Mix(ref actualFingerprint, actual);
                Mix(ref expectedFingerprint, expected);
                if (actual == 0) largeProfiles++;
                else if (actual == 1) defaultProfiles++;
                else invalidValues++;
                if (actual != expected)
                {
                    profileMismatches++;
                    AddSample(samples, ref sampleCount, new PathfindingGlobalMismatch(
                        PathfindingGlobalTableKind.Profile, 0, unitType, expected, actual));
                }
            }

            int permissionCount = Math.Min(permissions.Length, PermissionCount);
            for (int index = 0; index < permissionCount; index++)
            {
                int connectionClass = index / UnitTypeCount + 1;
                int unitType = index % UnitTypeCount;
                int actual = permissions[index];
                int expected = GetExpectedPermission(connectionClass, unitType);
                Mix(ref actualFingerprint, actual);
                Mix(ref expectedFingerprint, expected);
                if (actual != 0) allowedByClass[connectionClass - 1]++;
                if (actual != 0 && actual != 1) invalidValues++;
                if (actual != expected)
                {
                    permissionMismatches++;
                    AddSample(samples, ref sampleCount, new PathfindingGlobalMismatch(
                        PathfindingGlobalTableKind.ConnectionPermission,
                        connectionClass, unitType, expected, actual));
                }
            }

            if (sampleCount != samples.Length)
                Array.Resize(ref samples, sampleCount);
            return new PathfindingGlobalsComparison(
                profiles.Length, permissions.Length, profileMismatches,
                permissionMismatches, invalidValues, largeProfiles, defaultProfiles,
                allowedByClass, actualFingerprint, expectedFingerprint, samples);
        }

        private static int GetExpectedRowValue(int row, int unitType)
        {
            if ((uint)unitType >= UnitTypeCount)
                throw new ArgumentOutOfRangeException(nameof(unitType));
            int word = row * 2 + (unitType >> 6);
            return (int)((ExpectedOneBits[word] >> (unitType & 63)) & 1UL);
        }

        private static void AddSample(
            PathfindingGlobalMismatch[] samples,
            ref int sampleCount,
            PathfindingGlobalMismatch sample)
        {
            if (sampleCount < samples.Length)
                samples[sampleCount++] = sample;
        }

        private static void Mix(ref ulong fingerprint, int value)
        {
            unchecked
            {
                fingerprint = (fingerprint ^ (uint)value) * 1099511628211UL;
            }
        }
    }
}
