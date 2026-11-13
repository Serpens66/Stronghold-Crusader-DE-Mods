using APIShared;
using SHCDESE.API;
using System;

namespace BugfixesAndQoL.PitchStairs
{
    // DEVDIAG only: shows whether every start path (new map, save, editor) reached the start/load
    // normalization in 0x804C0. Remaining lowered pitch tiles mean a path bypassed 0x71670.
    // Also reports the fire-path counters of the 0x99420 stubs at each mission start and end.
    internal sealed partial class PitchStairsRuntime
    {
        // Fire-path breakdown of the 0x99420 stubs: explains a zero fireSourceAdjusted/fireNeighborAdjusted.
        // Pitch and visible pitch are counted only while the fix is enabled.
        private static readonly (string Name, int Offset)[] FireCounters =
        {
            ("fireSourceCalls", NativeCode.FireSourceCallsOffset),
            ("fireSourcePitch", NativeCode.FireSourcePitchOffset),
            ("fireSourcePitchVisible", NativeCode.FireSourcePitchVisibleOffset),
            ("fireNeighborCalls", NativeCode.FireNeighborCallsOffset),
        };
        private readonly long[] fireReported = new long[FireCounters.Length];

        partial void DiagFireCounters(ref bool enabled) => enabled = true;

        partial void DiagMissionEnded(MissionLifecycleNotification notification) =>
            LogFireCounters("end", notification.Context?.SessionId);

        private void LogFireCounters(string point, long? session)
        {
            var parts = new string[FireCounters.Length];
            for (int i = 0; i < FireCounters.Length; i++)
            {
                long value = Read(FireCounters[i].Offset);
                parts[i] = $"{FireCounters[i].Name}={value - fireReported[i]}/{value}";
                fireReported[i] = value;
            }
            Shared.DebugLogHelper.LogInfo(log, $"DEVDIAG PITCH_STAIRS_FIRE point={point}; session={session}; delta/total: {string.Join("; ", parts)}.");
        }

        partial void DiagMissionStarted(MissionLifecycleNotification notification)
        {
            LogFireCounters("start", notification.Context?.SessionId);
            try
            {
                var tiles = GameTileManagerAPI.Instance;
                Span<byte> height = tiles.GetHeightLayer(), defaults = tiles.GetDefaultHeightLayer();
                Span<int> logic = tiles.GetLogicLayer();
                Span<ushort> structures = tiles.TileManager.StructureGrid;
                int pitch = 0, lowered = 0, firstLowered = -1;
                for (int i = 0; i < logic.Length; i++)
                {
                    if ((logic[i] & NativeCode.PitchTrapFlag) == 0) continue;
                    pitch++;
                    if (NativeCode.IsVanillaLoweredPitch(height[i], defaults[i], logic[i], structures[i]))
                    {
                        lowered++;
                        if (firstLowered < 0) firstLowered = i;
                    }
                }
                Shared.DevDiag.Count("pitchStairs.pitchTiles", pitch);
                Shared.DebugLogHelper.LogInfo(log, $"DEVDIAG PITCH_STAIRS_SCAN start={notification.Context?.StartKind}; " +
                    $"editor={notification.Context?.IsEditor}; pitchTiles={pitch}; stillLowered={lowered}; " +
                    $"firstLoweredTile={firstLowered}; spans={height.Length}/{defaults.Length}/{logic.Length}/{structures.Length}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "DEVDIAG PITCH_STAIRS_SCAN_FAILED: " + ex);
            }
        }
    }
}
