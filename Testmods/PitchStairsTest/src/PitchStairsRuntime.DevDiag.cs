using APIShared;
using SHCDESE.API;
using System;

namespace PitchStairsTest
{
    // DEVDIAG only: shows whether every start path (new map, save, editor) reached the start/load
    // normalization in 0x804C0. Remaining lowered pitch tiles mean a path bypassed 0x71670.
    internal sealed partial class PitchStairsRuntime
    {
        partial void DiagMissionStarted(MissionLifecycleNotification notification)
        {
            try
            {
                var tiles = GameTileManagerAPI.Instance;
                Span<byte> height = tiles.GetHeightLayer(), defaults = tiles.GetDefaultHeightLayer();
                Span<int> logic = tiles.GetLogicLayer();
                Span<ushort> structures = tiles.TileManager.StructureGrid, misc = tiles.TileManager.MiscDisplayGrid;
                int pitch = 0, lowered = 0, firstLowered = -1;
                for (int i = 0; i < logic.Length; i++)
                {
                    if ((logic[i] & NativeCode.PitchTrapFlag) == 0) continue;
                    pitch++;
                    if (NativeCode.IsVanillaLoweredPitch(height[i], defaults[i], logic[i], misc[i], structures[i]))
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
