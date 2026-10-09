using BepInEx.Configuration;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.EventAPI.Network;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class FormationRuntime
    {
        private bool Matches(
            ActiveFormationCommand command,
            IntPtr manager,
            int targetX,
            int targetY) =>
            command != null && manager == nativePathManager &&
            command.TargetX == targetX && command.TargetY == targetY;

        private bool TryTakeDestination(
            ActiveFormationCommand command,
            out NativeDestination destination)
        {
            lock (stateSync)
            {
                if (!ReferenceEquals(activeCommand, command) ||
                    command.Cursor < 0 || command.Cursor >= command.Destinations.Length)
                {
                    destination = default;
                    return false;
                }
                destination = command.Destinations[command.Cursor++];
                return true;
            }
        }

        private NativeDestination[] BuildManagedDestinations(
            int anchorX,
            int anchorY,
            FormationKind kind,
            int density,
            int direction,
            int rows,
            RangedPlacementMode placementMode,
            FormationUnit[] units,
            bool explicitDirection,
            out FormationDirectionIndicator directionIndicator)
        {
            directionIndicator = FormationDirectionIndicator.Hidden;
            bool assassinOnly = IsAssassinOnly(units);
            List<FormationPoint> slots;
            NativeDestination[] snapped;
            if (kind == FormationKind.Vanilla)
            {
                snapped = CaptureVanillaDestinations(
                    anchorX, anchorY, units.Length, density, assassinOnly);
                slots = BuildVanillaSlotMetadata(
                    anchorX, anchorY, snapped, direction);
            }
            else
            {
                slots = FormationModel.BuildRelativeSlots(
                    kind, units.Length, rows, density, direction);
                List<NativeDestination> candidates = CaptureReachableCandidates(
                    anchorX, anchorY, Math.Min(MaximumPreviewCandidates,
                        Math.Max(256, units.Length * 16)), assassinOnly, slots);
                if (candidates.Count == 0)
                    throw new InvalidOperationException(
                        "No reachable formation destination exists.");
                snapped = SnapSlots(anchorX, anchorY, slots, candidates);
            }
            directionIndicator = BuildDirectionIndicator(
                slots, snapped, direction, explicitDirection);
            int[] assignment = FormationModel.AssignSlotsByRole(
                units, slots, placementMode, kind);
            var result = new NativeDestination[units.Length];
            for (int unitIndex = 0; unitIndex < result.Length; unitIndex++)
            {
                int slotIndex = assignment[unitIndex];
                NativeDestination tile = snapped[slotIndex];
                result[unitIndex] = new NativeDestination(
                    tile.TileId,
                    tile.X,
                    tile.Y,
                    units[unitIndex].Role);
            }
            return result;
        }

        private static FormationDirectionIndicator BuildDirectionIndicator(
            IReadOnlyList<FormationPoint> slots,
            IReadOnlyList<NativeDestination> snapped,
            int directionSector,
            bool explicitDirection)
        {
            int count = Math.Min(slots?.Count ?? 0, snapped?.Count ?? 0);
            if (count == 0)
                return FormationDirectionIndicator.Hidden;
            int frontRank = int.MaxValue;
            for (int index = 0; index < count; index++)
                frontRank = Math.Min(frontRank, slots[index].Rank);
            long totalX = 0;
            long totalY = 0;
            int frontCount = 0;
            for (int index = 0; index < count; index++)
            {
                if (slots[index].Rank != frontRank)
                    continue;
                totalX += snapped[index].X;
                totalY += snapped[index].Y;
                frontCount++;
            }
            if (frontCount == 0)
                return FormationDirectionIndicator.Hidden;
            int centerX = (int)Math.Round((double)totalX / frontCount);
            int centerY = (int)Math.Round((double)totalY / frontCount);
            FormationModel.GetForwardVector(
                directionSector, out int forwardX, out int forwardY);
            return new FormationDirectionIndicator(
                true,
                centerX + forwardX,
                centerY + forwardY,
                centerX + forwardX * 4,
                centerY + forwardY * 4,
                explicitDirection);
        }

        private NativeDestination[] CaptureVanillaDestinations(
            int anchorX,
            int anchorY,
            int requiredCount,
            int density,
            bool assassinOnly)
        {
            GameTileManagerView tileManager = GameTileManagerAPI.Instance.TileManager ??
                throw new InvalidOperationException("Native tile manager is unavailable.");
            Span<byte> edges = tileManager.PathEdgeMaskGrid;
            Span<ushort> components = tileManager.PathConnectionGrid;
            Span<int> logic = tileManager.LogicGrid;
            if ((uint)anchorX >= MapWidth || (uint)anchorY >= MapWidth ||
                movementTargetAvailability == null ||
                movementTargetAvailability[anchorY * MapWidth + anchorX] == 0)
            {
                throw new InvalidOperationException(
                    "The Vanilla formation anchor is not pathable.");
            }
            int anchorTile = GameTileManagerAPI.Instance.GetTileId(anchorX, anchorY);
            if ((uint)anchorTile >= (uint)components.Length ||
                (uint)anchorTile >= (uint)edges.Length || components[anchorTile] == 0)
            {
                throw new InvalidOperationException(
                    "The Vanilla formation anchor is not pathable.");
            }

            int normalizedDensity = FormationModel.NormalizeDensity(density);
            int capacity = Math.Min(components.Length, MapWidth * MapWidth);
            var visited = new bool[capacity];
            var queue = new Queue<VanillaSearchNode>();
            var result = new List<NativeDestination>(requiredCount);
            ushort component = components[anchorTile];
            visited[anchorTile] = true;
            queue.Enqueue(new VanillaSearchNode(anchorTile, anchorX, anchorY, 1));
            int inspected = 0;
            while (queue.Count != 0 &&
                   inspected < MaximumVanillaSelectorCandidates &&
                   result.Count < requiredCount)
            {
                VanillaSearchNode current = queue.Dequeue();
                inspected++;
                int logicFlags = (uint)current.TileId < (uint)logic.Length
                    ? logic[current.TileId]
                    : 0x10000100;
                if (FormationModel.IsNativeVanillaSlotCandidate(
                        current.X - anchorX,
                        current.Y - anchorY,
                        current.PathDistance,
                        logicFlags,
                        normalizedDensity,
                        assassinOnly))
                {
                    result.Add(new NativeDestination(
                        current.TileId, current.X, current.Y, FormationRole.Neutral));
                }

                byte mask = edges[current.TileId];
                TryEnqueueVanillaCandidate(current.X - 1, current.Y, 0x40, mask,
                    current.PathDistance + 1, component, edges, components, visited, queue);
                TryEnqueueVanillaCandidate(current.X + 1, current.Y, 0x04, mask,
                    current.PathDistance + 1, component, edges, components, visited, queue);
                TryEnqueueVanillaCandidate(current.X, current.Y - 1, 0x01, mask,
                    current.PathDistance + 1, component, edges, components, visited, queue);
                TryEnqueueVanillaCandidate(current.X - 1, current.Y - 1, 0x80, mask,
                    current.PathDistance + 2, component, edges, components, visited, queue);
                TryEnqueueVanillaCandidate(current.X + 1, current.Y - 1, 0x02, mask,
                    current.PathDistance + 2, component, edges, components, visited, queue);
                TryEnqueueVanillaCandidate(current.X, current.Y + 1, 0x10, mask,
                    current.PathDistance + 1, component, edges, components, visited, queue);
                TryEnqueueVanillaCandidate(current.X - 1, current.Y + 1, 0x20, mask,
                    current.PathDistance + 2, component, edges, components, visited, queue);
                TryEnqueueVanillaCandidate(current.X + 1, current.Y + 1, 0x08, mask,
                    current.PathDistance + 2, component, edges, components, visited, queue);
            }
            if (result.Count != requiredCount)
            {
                throw new InvalidOperationException(
                    $"Vanilla supplied {result.Count} of {requiredCount} required slots.");
            }
            return result.ToArray();
        }

        private static List<FormationPoint> BuildVanillaSlotMetadata(
            int anchorX,
            int anchorY,
            IReadOnlyList<NativeDestination> destinations,
            int directionSector)
        {
            var relative = new List<FormationPoint>(destinations.Count);
            for (int index = 0; index < destinations.Count; index++)
            {
                int localX = destinations[index].X - anchorX;
                int localY = destinations[index].Y - anchorY;
                relative.Add(new FormationPoint(localX, localY, 0, 0));
            }
            return FormationModel.OrientNativeSlots(relative, directionSector);
        }

        private static void TryEnqueueVanillaCandidate(
            int x,
            int y,
            byte requiredMask,
            byte sourceMask,
            int pathDistance,
            ushort component,
            Span<byte> edges,
            Span<ushort> components,
            bool[] visited,
            Queue<VanillaSearchNode> queue)
        {
            if ((sourceMask & requiredMask) == 0 ||
                (uint)x >= MapWidth || (uint)y >= MapWidth)
                return;
            int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
            if ((uint)tileId >= (uint)visited.Length || visited[tileId] ||
                (uint)tileId >= (uint)edges.Length || components[tileId] != component)
                return;
            visited[tileId] = true;
            queue.Enqueue(new VanillaSearchNode(tileId, x, y, pathDistance));
        }

        private static NativeDestination[] SnapSlots(
            int anchorX,
            int anchorY,
            IReadOnlyList<FormationPoint> slots,
            IReadOnlyList<NativeDestination> candidates)
        {
            var points = new FormationPoint[candidates.Count];
            var tileIds = new int[candidates.Count];
            for (int index = 0; index < candidates.Count; index++)
            {
                points[index] = new FormationPoint(candidates[index].X, candidates[index].Y, 0, 0);
                tileIds[index] = candidates[index].TileId;
            }
            int[] selected = FormationPlacementModel.SelectCandidates(anchorX, anchorY, slots, points, tileIds);
            var result = new NativeDestination[slots.Count];
            for (int index = 0; index < slots.Count; index++) result[index] = candidates[selected[index]];
            return result;
        }

        private List<NativeDestination> CaptureReachableCandidates(
            int anchorX,
            int anchorY,
            int requestedCount,
            bool assassinOnly, IReadOnlyList<FormationPoint> desiredSlots)
        {
            GameTileManagerView tileManager = GameTileManagerAPI.Instance.TileManager ??
                throw new InvalidOperationException("Native tile manager is unavailable.");
            Span<byte> edges = tileManager.PathEdgeMaskGrid;
            Span<ushort> components = tileManager.PathConnectionGrid;
            Span<int> logic = tileManager.LogicGrid;
            int anchorTile = GameTileManagerAPI.Instance.GetTileId(anchorX, anchorY);
            if ((uint)anchorX >= MapWidth || (uint)anchorY >= MapWidth ||
                movementTargetAvailability == null ||
                movementTargetAvailability[anchorY * MapWidth + anchorX] == 0 ||
                (uint)anchorTile >= (uint)components.Length ||
                (uint)anchorTile >= (uint)edges.Length || components[anchorTile] == 0)
                return new List<NativeDestination>();

            int capacity = Math.Min(components.Length, MapWidth * MapWidth);
            var visited = new bool[capacity];
            var queue = new Queue<NativeDestination>();
            var result = new List<NativeDestination>(requestedCount);
            ushort component = components[anchorTile];
            var pending = new HashSet<int>();
            foreach (FormationPoint slot in FormationPlacementModel.EnumerateReachabilityProbes(desiredSlots))
            {
                int x = anchorX + slot.X, y = anchorY + slot.Y;
                if ((uint)x >= MapWidth || (uint)y >= MapWidth ||
                    movementTargetAvailability[y * MapWidth + x] == 0) continue;
                int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
                if ((uint)tile < (uint)capacity && (uint)tile < (uint)edges.Length &&
                    components[tile] == component && (!assassinOnly ||
                    ((uint)tile < (uint)logic.Length && (logic[tile] & 0x10000100) == 0)))
                    pending.Add(tile);
            }
            visited[anchorTile] = true;
            queue.Enqueue(new NativeDestination(
                anchorTile, anchorX, anchorY, FormationRole.Neutral));
            while (queue.Count != 0 && (result.Count < requestedCount || pending.Count != 0))
            {
                NativeDestination current = queue.Dequeue();
                bool available = movementTargetAvailability[
                    current.Y * MapWidth + current.X] != 0;
                bool assassinAllowed = !assassinOnly ||
                    ((uint)current.TileId < (uint)logic.Length &&
                     (logic[current.TileId] & 0x10000100) == 0);
                bool desired = pending.Remove(current.TileId);
                if (available && assassinAllowed && (result.Count < requestedCount || desired))
                    result.Add(current);
                byte mask = edges[current.TileId];
                TryEnqueueCandidate(current.X - 1, current.Y, 0x40, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X + 1, current.Y, 0x04, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X, current.Y - 1, 0x01, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X - 1, current.Y - 1, 0x80, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X + 1, current.Y - 1, 0x02, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X, current.Y + 1, 0x10, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X - 1, current.Y + 1, 0x20, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X + 1, current.Y + 1, 0x08, mask,
                    component, edges, components, visited, queue);
            }
            return result;
        }

        private static bool IsAssassinOnly(IReadOnlyList<FormationUnit> units)
        {
            if (units == null || units.Count == 0)
                return false;
            for (int index = 0; index < units.Count; index++)
            {
                if (units[index].UnitType != (int)eChimps.CHIMP_TYPE_ARAB_ASSASIN)
                    return false;
            }
            return true;
        }

        private static void TryEnqueueCandidate(
            int x,
            int y,
            byte requiredMask,
            byte sourceMask,
            ushort component,
            Span<byte> edges,
            Span<ushort> components,
            bool[] visited,
            Queue<NativeDestination> queue)
        {
            if ((sourceMask & requiredMask) == 0 ||
                (uint)x >= MapWidth || (uint)y >= MapWidth)
                return;
            int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
            if ((uint)tileId >= (uint)visited.Length || visited[tileId] ||
                (uint)tileId >= (uint)edges.Length ||
                components[tileId] != component)
                return;
            visited[tileId] = true;
            queue.Enqueue(new NativeDestination(
                tileId, x, y, FormationRole.Neutral));
        }

        private FormationUnit[] CaptureOrderedGroupUnits(int tribeId)
        {
            if (tribeId <= 0 || tribeId >= MaximumTribeCount)
                return Array.Empty<FormationUnit>();
            byte* tribe = (byte*)nativeTribeManager.ToPointer() + tribeId * TribeRecordSize;
            int count = *(ushort*)(tribe + TribeUnitCountOffset);
            if (count <= 0 || count > MaximumUnitCount)
                return Array.Empty<FormationUnit>();

            var result = new List<FormationUnit>(count);
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                int unitId = getGroupUnitId(nativeTribeManager, tribeId, ordinal);
                if (unitId <= 0 ||
                    !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_TribeId != tribeId)
                    continue;
                int unitType = (int)unit->r_UnitChimp;
                result.Add(new FormationUnit(
                    unitId, unit->r_GlobalId, unitType, Classify((eChimps)unitType)));
            }
            result.Sort((left, right) => left.UnitId.CompareTo(right.UnitId));
            return result.ToArray();
        }

        private static ulong ComputePlanHash(
            IReadOnlyList<FormationUnit> units,
            IReadOnlyList<NativeDestination> destinations,
            RangedPlacementMode placementMode, int rows)
        {
            int count = Math.Min(units?.Count ?? 0, destinations?.Count ?? 0);
            ulong hash = FormationPlanHash.Begin(count, placementMode, rows);
            for (int index = 0; index < count; index++)
            {
                FormationUnit unit = units[index];
                NativeDestination destination = destinations[index];
                FormationPlanHash.AddEntry(
                    ref hash,
                    unit.UnitId,
                    unit.GlobalId,
                    destination.X,
                    destination.Y,
                    destination.Role);
            }
            return hash;
        }

        private static FormationRole Classify(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_SPEARMAN:
                case eChimps.CHIMP_TYPE_PIKEMAN:
                case eChimps.CHIMP_TYPE_MACEMAN:
                case eChimps.CHIMP_TYPE_SWORDSMAN:
                case eChimps.CHIMP_TYPE_KNIGHT:
                case eChimps.CHIMP_TYPE_MONK:
                case eChimps.CHIMP_TYPE_LORD:
                case eChimps.CHIMP_TYPE_WAR_DOG:
                case eChimps.CHIMP_TYPE_ARAB_SLAVE:
                case eChimps.CHIMP_TYPE_ARAB_ASSASIN:
                case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                case eChimps.CHIMP_TYPE_ARAB_SWORDSMAN:
                case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                    return FormationRole.Front;

                case eChimps.CHIMP_TYPE_HEALER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEALER:
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD:
                    return FormationRole.Protected;

                case eChimps.CHIMP_TYPE_ARCHER:
                case eChimps.CHIMP_TYPE_ARCHER_debug:
                case eChimps.CHIMP_TYPE_XBOWMAN:
                case eChimps.CHIMP_TYPE_ARAB_BOW:
                case eChimps.CHIMP_TYPE_ARAB_SLINGER:
                case eChimps.CHIMP_TYPE_ARAB_GRENADIER:
                case eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER:
                case eChimps.CHIMP_TYPE_CATAPULT:
                case eChimps.CHIMP_TYPE_TREBUCHET:
                case eChimps.CHIMP_TYPE_MANGONEL:
                case eChimps.CHIMP_TYPE_BALLISTA:
                case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                case eChimps.CHIMP_TYPE_SIEGE_TOWER:
                case eChimps.CHIMP_TYPE_BATTERING_RAM:
                case eChimps.CHIMP_TYPE_ENGINEER:
                case eChimps.CHIMP_TYPE_LADDERMAN:
                case eChimps.CHIMP_TYPE_TUNNELER:
                case eChimps.CHIMP_TYPE_FIREMAN:
                case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER:
                case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER:
                    return FormationRole.Rear;

                default:
                    return FormationRole.Neutral;
            }
        }

        private readonly struct NativeDestination
        {
            internal NativeDestination(
                int tileId, int x, int y, FormationRole role)
            {
                TileId = tileId;
                X = x;
                Y = y;
                Role = role;
            }

            internal int TileId { get; }
            internal int X { get; }
            internal int Y { get; }
            internal FormationRole Role { get; }
        }

        private readonly struct VanillaSearchNode
        {
            internal VanillaSearchNode(
                int tileId, int x, int y, int pathDistance)
            {
                TileId = tileId;
                X = x;
                Y = y;
                PathDistance = pathDistance;
            }

            internal int TileId { get; }
            internal int X { get; }
            internal int Y { get; }
            internal int PathDistance { get; }
        }
    }
}
