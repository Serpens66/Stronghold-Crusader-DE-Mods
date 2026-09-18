// SHCDESE_COARSE_GRID_BUFFER_WORKAROUND
// Temporary compatibility code for SHCDE-SE 2.7.1's Unity Mono TypeLoadException.
// Remove this entire file once GameAIVManagerAPI.Instance and its official live-village and
// build-step spans initialize successfully under the game's Unity Mono runtime.
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using System;

namespace BugfixesAndQoL
{
    internal unsafe interface IAiStoneReserveAivDataSource
    {
        Span<AivVillageState> GetLiveVillageSlots();
        Span<AivBuildStep> GetBuildSteps(int villageSlot);
    }

    internal sealed unsafe class OfficialAiStoneReserveAivDataSource : IAiStoneReserveAivDataSource
    {
        private readonly GameAIVManagerAPI api;

        public OfficialAiStoneReserveAivDataSource(GameAIVManagerAPI api)
        {
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public Span<AivVillageState> GetLiveVillageSlots() => api.GetLiveVillageSlots();

        public Span<AivBuildStep> GetBuildSteps(int villageSlot) => api.GetBuildSteps(villageSlot);
    }

    // SHCDESE_COARSE_GRID_BUFFER_WORKAROUND: raw backend isolated for complete later removal.
    internal static unsafe class ShcdeSeCoarseGridBufferWorkaround
    {
        internal const string Marker = "SHCDESE_COARSE_GRID_BUFFER_WORKAROUND";
        internal const int AivSystemSize = 0x1BBF58;
        internal const int LiveVillageOffset = 0x6D9C;
        internal const int LiveVillageCount = 8;
        internal const int VillageStride = 0x6D98;
        internal const int VillageBuildStepsOffset = 0x34;
        internal const int BuildStepCapacity = 1_000;

        internal static T SelectBackend<T>(
            Func<T> officialFactory,
            Func<T> workaroundFactory,
            out bool workaroundActive,
            out Exception knownScriptExtenderFailure)
        {
            if (officialFactory == null)
                throw new ArgumentNullException(nameof(officialFactory));
            if (workaroundFactory == null)
                throw new ArgumentNullException(nameof(workaroundFactory));

            try
            {
                T official = officialFactory();
                workaroundActive = false;
                knownScriptExtenderFailure = null;
                return official;
            }
            catch (Exception exception) when (IsKnownFailure(exception))
            {
                T workaround = workaroundFactory();
                workaroundActive = true;
                knownScriptExtenderFailure = exception;
                return workaround;
            }
        }

        internal static bool IsKnownFailure(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (current is TypeLoadException &&
                    Contains(current.Message, "CoarseGridBuffer") &&
                    Contains(current.Message, "FixedBuffer") &&
                    (Contains(current.Message, "1228816") ||
                     Contains(current.Message, "bigger than 1Mb")))
                {
                    return true;
                }
            }

            return false;
        }

        internal static IAiStoneReserveAivDataSource CreateRawDataSource(
            ulong moduleAddress,
            int moduleLength)
        {
            ulong aivSystemAddress = GameGlobalsManager.Instance.AIVSystemVA;
            if (!IsAivSystemRangeInsideModule(aivSystemAddress, moduleAddress, moduleLength))
            {
                throw new InvalidOperationException(
                    $"The Script Extender AIVSystem address failed module-range validation: " +
                    $"aivSystem=0x{aivSystemAddress:X}, module=0x{moduleAddress:X}, moduleLength={moduleLength}.");
            }

            return new RawAiStoneReserveAivDataSource(aivSystemAddress);
        }

        internal static bool IsAivSystemRangeInsideModule(
            ulong aivSystemAddress,
            ulong moduleAddress,
            int moduleLength)
        {
            if (aivSystemAddress == 0 || moduleAddress == 0 || moduleLength <= 0 ||
                (aivSystemAddress & 3UL) != 0)
            {
                return false;
            }

            ulong moduleEnd = checked(moduleAddress + (ulong)moduleLength);
            return aivSystemAddress >= moduleAddress &&
                   aivSystemAddress <= moduleEnd &&
                   (ulong)AivSystemSize <= moduleEnd - aivSystemAddress;
        }

        private static bool Contains(string value, string expected)
        {
            return value?.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed unsafe class RawAiStoneReserveAivDataSource : IAiStoneReserveAivDataSource
        {
            private readonly byte* aivSystem;

            public RawAiStoneReserveAivDataSource(ulong aivSystemAddress)
            {
                aivSystem = (byte*)aivSystemAddress;
            }

            public Span<AivVillageState> GetLiveVillageSlots()
            {
                return new Span<AivVillageState>(
                    aivSystem + LiveVillageOffset,
                    LiveVillageCount);
            }

            public Span<AivBuildStep> GetBuildSteps(int villageSlot)
            {
                if (villageSlot < 1 || villageSlot > LiveVillageCount)
                    return Span<AivBuildStep>.Empty;

                byte* village = aivSystem + LiveVillageOffset +
                    (villageSlot - 1) * VillageStride;
                return new Span<AivBuildStep>(
                    village + VillageBuildStepsOffset,
                    BuildStepCapacity);
            }
        }
    }
}
