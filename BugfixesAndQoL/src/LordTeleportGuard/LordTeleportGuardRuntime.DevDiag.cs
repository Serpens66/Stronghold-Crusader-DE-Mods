using System;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL.LordTeleportGuard
{
    internal sealed unsafe partial class LordTeleportGuardRuntime
    {
        private int loggedRecords;
        private long loggedTotal;
        private bool missionLookupAttempted;
        private APIShared.IMissionLifecycleCapability diagnosticMission;
        partial void FlushDiagnostics(int tick)
        {
            if (trace == IntPtr.Zero) return;
            int count = Marshal.ReadInt32(trace);
            int dropped = Marshal.ReadInt32(trace, 4);
            long total = Marshal.ReadInt64(trace, 8);
            if (count < 0 || count > LordTeleportNative.TraceCapacity || total != (long)count + (uint)dropped)
                throw new InvalidOperationException("Lord teleport diagnostic invariant failed.");
            if (count > loggedRecords)
            {
                if (!missionLookupAttempted)
                {
                    missionLookupAttempted = true;
                    APIShared.ApiShared.Current.TryGetMissionLifecycle(BugfixesAndQoLPlugin.PluginGuid, out diagnosticMission, out _);
                }
                // Observation context only; early native loading may precede a ready mission.
                string session = diagnosticMission?.Current?.SessionId.ToString() ?? "unknown";
                var mode = APIShared.GameModes.GameModeHelper.Capture();
                Shared.DevDiag.Log(() => "LORD_TELEPORT_CONTEXT: tickObserved=" + tick + "; sessionObserved=" + session +
                    "; " + mode.ToDiagnosticString());
            }
            while (loggedRecords < count)
            {
                IntPtr p = IntPtr.Add(trace, LordTeleportNative.TraceHeader + loggedRecords++ * LordTeleportNative.TraceStride);
                int unitId = Marshal.ReadInt32(p), globalId = Marshal.ReadInt32(p, 4), player = Marshal.ReadInt32(p, 8);
                int source = Marshal.ReadInt32(p, 12), x = Marshal.ReadInt32(p, 16), y = Marshal.ReadInt32(p, 20);
                ulong caller = unchecked((ulong)Marshal.ReadInt64(p, 24));
                int state = Marshal.ReadInt32(p, 32), height = Marshal.ReadInt32(p, 36);
                string reason = caller == image + 0x15ABD6 ? "return-path-failed" :
                    caller == image + 0x183014 ? "invalid-standing-component" : "other-home-relocation";
                Shared.DevDiag.Log(() => "LORD_TELEPORT_BLOCKED: tickObserved=" + tick + "; reason=" + reason +
                    "; unit=" + unitId + "; global=" + globalId + "; player=" + player +
                    "; source=(" + (short)source + "," + (short)(source >> 16) + "); height=" + height +
                    "; home=(" + x + "," + y + "); state=" + state + "; returnAddress=0x" + caller.ToString("X"));
            }
            if (total != loggedTotal && (tick % 50 == 0 || loggedTotal == 0))
            {
                Shared.DevDiag.Log(() => "LORD_TELEPORT_COUNTS: blocked=" + total + "; records=" + count + "; dropped=" + (uint)dropped + "; invariant=True.");
                loggedTotal = total;
            }
        }
    }
}
