using System;
using System.Collections.Generic;
using Pathfinding.UnitCommands;
using SHCDESE.API;
using SHCDESE.Interop;
using static Pathfinding.UnitCommands.UnitCommandPathRuntime;

namespace Pathfinding.Engine
{
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        // Surface permissions are player-neutral; player/gate/moat edges remain in the existing masks.
        private readonly byte[] surfaceWords = new byte[MapWidth * MapWidth];
        private readonly byte[] surfaceWriteBuffer = new byte[AnchorMaskNodesPerTick];
        private readonly bool[] surfaceDirtyFlags = new bool[MapWidth * MapWidth];
        private readonly List<int> surfaceDirty = new List<int>();
        private readonly int[] surfaceNodes = new int[AnchorMaskNodesPerTick];
        private int surfaceNext, surfaceSweep, surfacePoolInvalidations;
        private readonly long[] surfaceRevisions = new long[3];
        internal override long GetMovementSurfaceRevision(RouteSurfaceClass surface) =>
            (uint)surface < (uint)surfaceRevisions.Length ? surfaceRevisions[(int)surface] : 0;
        private ulong surfaceBuildingFingerprint;
        private bool surfaceFingerprintKnown;

        private sealed class AnchorSurfaceSlot
        {
            internal AnchorTaskSpec Spec;
            internal int Task = AnchorPendingTask;
            internal AnchorCostProfile Costs;
        }

        private RouteSurfaceClass SurfaceForPlan(PlanScope plan) => plan != null &&
            APIShared.UnitAccess.TryGetById(plan.UnitId, out GameUnit* unit, out _) && unit != null
                ? runtime.ResolveMovementSurface(unit) : RouteSurfaceClass.Any;

        private static int SurfaceSlot(bool ground, RouteSurfaceClass surface) => (int)surface * 2 + (ground ? 1 : 0);
        private AnchorSurfaceSlot CurrentSurfaceSlot(bool ground, RouteSurfaceClass surface)
        {
            if (surface == RouteSurfaceClass.Any || anchorFrames.Count == 0) return null;
            return anchorFrames[anchorFrames.Count - 1].SurfaceSlots?[SurfaceSlot(ground, surface)];
        }
        private int CurrentAnchorTask(bool ground, RouteSurfaceClass surface = RouteSurfaceClass.Any)
        {
            if (surface != RouteSurfaceClass.Any) return CurrentSurfaceSlot(ground, surface)?.Task ?? 0;
            if (anchorFrames.Count == 0) return ground ? anchorGroundTask : anchorDispatchTask;
            AnchorDispatchFrame frame = anchorFrames[anchorFrames.Count - 1];
            return ground ? frame.GroundTask : frame.Task;
        }

        private void ResetMovementSurfaces()
        {
            surfaceNext = surfaceSweep = surfacePoolInvalidations = 0; surfaceFingerprintKnown = false;
            Array.Clear(surfaceWords, 0, surfaceWords.Length);
            surfaceRevisions[1]++; surfaceRevisions[2]++;
            Array.Clear(surfaceDirtyFlags, 0, surfaceDirtyFlags.Length); surfaceDirty.Clear();
            // A new map cannot inherit the preceding map's permissions, even before its first tick.
            if (anchorAvailable)
                for (int first = 0; first < surfaceWords.Length; first += surfaceWriteBuffer.Length)
                {
                    int count = Math.Min(surfaceWriteBuffer.Length, surfaceWords.Length - first);
                    Array.Clear(surfaceWriteBuffer, 0, count);
                    CheckSurfaceWrite(AnchorNative.mma_surface_write(anchorContext, first, count, surfaceWriteBuffer, out _));
                }
        }

        private void MarkMovementSurface(int node)
        {
            if ((uint)node >= surfaceDirtyFlags.Length || surfaceDirtyFlags[node]) return;
            surfaceDirtyFlags[node] = true; surfaceDirty.Add(node);
        }
        private byte ReadSurfaceNode(int node)
        {
            int tile = GameTileManagerAPI.Instance.GetTileId(node % MapWidth, node / MapWidth);
            return IsValidTileId(tile) ? runtime.ReadMovementSurfaceTile(tile) : (byte)0;
        }
        private void CheckSurfaceWrite(int result)
        {
            if (result != 0) throw new InvalidOperationException("Native movement-surface metadata update failed.");
        }
        private byte RefreshSurfaceNode(int node)
        {
            byte before = surfaceWords[node], after = ReadSurfaceNode(node);
            for (int surface = 1; surface <= 2; surface++)
                if (MovementSurfacePolicy.Allows((RouteSurfaceClass)surface, before) != MovementSurfacePolicy.Allows((RouteSurfaceClass)surface, after))
                {
                    surfaceRevisions[surface]++;
                    surfacePoolInvalidations |= 1 << surface;
                }
            return surfaceWords[node] = after;
        }
        private void WriteSurfaceRange(int first, int count)
        {
            for (int i = 0; i < count; i++) surfaceWriteBuffer[i] = RefreshSurfaceNode(first + i);
            CheckSurfaceWrite(AnchorNative.mma_surface_write(anchorContext, first, count, surfaceWriteBuffer, out _));
        }
        private void RefreshMovementSurfaces(bool complete)
        {
            ulong fingerprint = runtime.GetMovementSurfaceBuildingPolicyFingerprint();
            if (surfaceFingerprintKnown && fingerprint != surfaceBuildingFingerprint)
            {
                surfaceNext = 0;
                // Live audits protect routes while the bounded sweep updates older cached permissions.
            }
            surfaceFingerprintKnown = true; surfaceBuildingFingerprint = fingerprint;
            do
            {
                int count = Math.Min(AnchorMaskNodesPerTick, surfaceWords.Length - surfaceNext);
                if (count > 0) { WriteSurfaceRange(surfaceNext, count); surfaceNext += count; }
            } while (complete && surfaceNext < surfaceWords.Length);

            int dirty = Math.Min(surfaceDirty.Count, surfaceNodes.Length);
            for (int i = 0; i < dirty; i++)
            {
                int node = surfaceDirty[i]; surfaceNodes[i] = node;
                surfaceWriteBuffer[i] = RefreshSurfaceNode(node); surfaceDirtyFlags[node] = false;
            }
            if (dirty != 0)
            {
                CheckSurfaceWrite(AnchorNative.mma_surface_update(anchorContext, surfaceNodes, surfaceWriteBuffer, dirty, out _));
                surfaceDirty.RemoveRange(0, dirty);
            }
            if (surfaceNext == surfaceWords.Length)
            {
                int count = Math.Min(AnchorResweepNodesPerTick, surfaceWords.Length - surfaceSweep);
                WriteSurfaceRange(surfaceSweep, count); surfaceSweep = (surfaceSweep + count) % surfaceWords.Length;
            }
            for (int surface = 1; surface <= 2; surface++)
                if ((surfacePoolInvalidations & (1 << surface)) != 0)
                {
                    fastSimulationRouting?.Pool.InvalidateSurface((RouteSurfaceClass)surface);
                    fastCursorRouting?.Pool.InvalidateSurface((RouteSurfaceClass)surface);
                }
            surfacePoolInvalidations = 0;
        }

        private bool AuditRouteSurface(byte[] packed, int count, int start, RouteSurfaceClass surface)
        {
            if (surface == RouteSurfaceClass.Any) return true;
            int x = start % MapWidth, y = start / MapWidth;
            for (int i = 0; i < count; i++)
            {
                int d = (packed[i >> 1] >> ((i & 1) * 4)) & 15;
                if (d > 7) return false;
                x += FastRouteField.Dx[d]; y += FastRouteField.Dy[d];
                if ((uint)x >= MapWidth || (uint)y >= MapWidth) return false;
                int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (!runtime.IsMovementSurfaceAllowed(surface, tile))
                { MarkMovementSurface(y * MapWidth + x); return false; }
            }
            return true;
        }
    }
}
