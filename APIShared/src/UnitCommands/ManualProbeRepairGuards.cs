using System;
using System.Runtime.InteropServices;
using RedBird.X64.Hooks.Transaction;
using RedBird.Core.Memory;

namespace APIShared.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int ProbePclRebuildDelegate(IntPtr manager, int force);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void ProbeConnectionsRepairDelegate(IntPtr manager, int tile, int y);
        private RedBirdDetour<ProbePclRebuildDelegate> probePclRebuild;
        private RedBirdDetour<MaskRebuildDelegate> probeBuildingsRepair;
        private RedBirdDetour<ProbeConnectionsRepairDelegate> probeConnectionsRepair;
        private ProbePclRebuildDelegate originalProbePclRebuild;
        private MaskRebuildDelegate originalProbeBuildingsRepair;
        private ProbeConnectionsRepairDelegate originalProbeConnectionsRepair;

        // E1640 invokes these only after reconstruction has already failed and
        // set length to zero. A read-only probe must not repair global topology.
        // All simulation calls retain the complete original repair sequence.
        private void InstallManualProbeRepairGuards(HookTransaction transaction, ulong libraryBase)
        {
            ProbePclRebuildDelegate pcl = (manager, force) => nativeManualProbe ? 0 : originalProbePclRebuild(manager, force);
            MaskRebuildDelegate buildings = manager => { if (!nativeManualProbe) originalProbeBuildingsRepair(manager); };
            ProbeConnectionsRepairDelegate connections = (manager, tile, y) => { if (!nativeManualProbe) originalProbeConnectionsRepair(manager, tile, y); };
            connectivityDelegates.Add(pcl); connectivityDelegates.Add(buildings); connectivityDelegates.Add(connections);
            probePclRebuild = AddDetour(transaction, libraryBase + 0xE49D0, pcl);
            probeBuildingsRepair = AddDetour(transaction, libraryBase + 0xC3E50, buildings);
            probeConnectionsRepair = AddDetour(transaction, libraryBase + 0xDE6A0, connections);
        }
        private void CompleteManualProbeRepairGuards()
        {
            originalProbePclRebuild = probePclRebuild.Original;
            originalProbeBuildingsRepair = probeBuildingsRepair.Original;
            originalProbeConnectionsRepair = probeConnectionsRepair.Original;
        }
    }
}
