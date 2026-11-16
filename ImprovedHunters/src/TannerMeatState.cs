using APIShared.Buildings;
using APIShared.Networking;
using SHCDESE.Interop;
using System;

namespace ImprovedHunters
{
    /// <summary>
    /// The per-Tannery state in APIShared's building state table: the slider's meat runs, which only the
    /// owner changes through APIShared's synchronized request, and whether the Tanner's current trip carries
    /// meat, which only the native stubs write. APIShared keeps it identical on all peers and in saves
    /// (record <c>ImprovedHunters.TanneryMeatRuns</c>) and clears it for new buildings and fresh missions.
    /// </summary>
    internal sealed class TannerMeatState
    {
        private const int MeatRunsIndex = 0;
        private const int MeatTripIndex = 1;

        private readonly BuildingStateTable table;

        private TannerMeatState(BuildingStateTable table)
        {
            this.table = table;
            Layout = new TannerStateLayout(
                table.NativeAddress,
                table.Capacity,
                table.EntrySize,
                table.GlobalIdOffset,
                table.ValuesOffset + MeatRunsIndex,
                table.ValuesOffset + MeatTripIndex);
        }

        /// <summary>Registers on every peer, whatever the settings say: APIShared registers its request Chore with the
        /// first table, and the dynamic packet IDs must match on all peers.</summary>
        internal static TannerMeatState Register(Func<bool> isEnabled)
        {
            var registration = new BuildingStateRegistration(
                ImprovedHuntersPlugin.PluginGuid,
                "tanner-meat",
                "ImprovedHunters.TanneryMeatRuns",
                new[] { eStructs.STRUCT_TANNERS_WORKSHOP },
                new[]
                {
                    new BuildingStateValue("meat-runs", TannerMeatPolicy.HidesPerCow, BuildingStateValueAccess.OwnerRequest),
                    new BuildingStateValue("meat-trip", 1, BuildingStateValueAccess.Simulation)
                })
            {
                AcceptsOwnerRequests = isEnabled
            };
            if (!BuildingStates.TryRegister(registration, out BuildingStateTable table, out string reason))
                throw new InvalidOperationException(reason);

            return new TannerMeatState(table);
        }

        internal TannerStateLayout Layout { get; }

        internal int GetMeatRuns(int buildingId, uint globalId) => table.GetValue(buildingId, globalId, MeatRunsIndex);

        /// <summary>Whether this peer's last slider change has not reached the table yet.</summary>
        internal bool HasPendingChange => table.HasPendingRequest;

        /// <summary>For a closed panel: a change that never arrives must not hold the next opening back.</summary>
        internal void ForgetPendingChange() => table.ForgetPendingRequest();

        /// <summary>A Tanner on his way keeps the good he carries; only later runs follow the new value.</summary>
        internal NetworkSendResult RequestMeatRuns(int buildingId, uint globalId, int meatRuns) =>
            TannerMeatPolicy.IsValidMeatRuns(meatRuns)
                ? table.RequestChange(buildingId, globalId, MeatRunsIndex, (byte)meatRuns)
                : null;
    }
}
