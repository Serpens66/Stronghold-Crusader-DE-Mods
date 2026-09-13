using System;
using System.Collections.Generic;
using SHCDESE.API;

namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        private sealed class FastGroupDistribution
        {
            internal MoveCommandScope Command;
            internal int Player, Anchor, Next;
            internal long Revision;
            internal readonly List<int> Slots = new List<int>();
            internal readonly Dictionary<int, byte[]> Suffixes = new Dictionary<int, byte[]>();
        }
        private FastGroupDistribution fastDistribution;
        private readonly Dictionary<FastUnitIdentity, FastGroupDistribution> fastUnitDistributions =
            new Dictionary<FastUnitIdentity, FastGroupDistribution>();

        private void RetainFastDistribution(MoveCommandScope command)
        {
            if (!RequiredOnlyMode || command == null) return;
            foreach (int id in command.ActiveUnitIdsAtDispatch)
                if (GameUnitManagerAPI.Instance.TryGetUnitById(id, out var unit) && unit != null)
                {
                    var identity = new FastUnitIdentity(id, unit->r_GlobalId);
                    fastUnitDistributions.Remove(identity);
                    if (fastDistribution?.Command == command) fastUnitDistributions[identity] = fastDistribution;
                }
        }

        private bool TryChooseFastFormation(IntPtr manager, int x, int y, out int tile)
        {
            tile = 0;
            if (!RequiredOnlyMode || !IsScopedPureMoveFormationCall(manager, x, y, activeMoveCommand)) return false;
            MoveCommandScope command = activeMoveCommand;
            EnsureMoveCommandGroupSummary(command);
            if (!command.MoatRelevant || command.ActiveUnitsAtDispatch == 0 ||
                command.DiggersAtDispatch != command.ActiveUnitsAtDispatch) return false;
            if (nativeTribeManager == IntPtr.Zero || command.TribeId <= 0 || command.TribeId >= MaximumTribeCount) return false;
            int player = *(ushort*)((byte*)nativeTribeManager + command.TribeId * TribeRecordSize + 0x2C);
            FastRoutingState state = GetFastRouting(false); RefreshFastRouting(state);
            MakeFastEdge(state, new FastFieldKey(player, y * MapWidth + x, false));
            FastTraversalCache map = state.Maps[player];
            if (fastDistribution == null || fastDistribution.Command != command || fastDistribution.Revision != map.Revision)
                fastDistribution = BuildFastDistribution(command, player, map);
            if (fastDistribution.Slots.Count == 0) return false;
            int cell = fastDistribution.Slots[fastDistribution.Next++ % fastDistribution.Slots.Count];
            tile = GameTileManagerAPI.Instance.GetTileId(cell % MapWidth, cell / MapWidth);
            int* output = (int*)((byte*)nativeTribeManager + 0x0C);
            output[0] = cell % MapWidth; output[1] = cell / MapWidth;
            // The native field cursor is not consumed by this selector. Keep its
            // original value for any subsequent native-only selector in this command.
            return true;
        }

        private FastGroupDistribution BuildFastDistribution(MoveCommandScope command, int player, FastTraversalCache map)
        {
            const int radius = 8, side = 17;
            int anchor = command.TargetY * MapWidth + command.TargetX;
            var result = new FastGroupDistribution { Command = command, Player = player, Anchor = anchor, Revision = map.Revision };
            int anchorTile = GameTileManagerAPI.Instance.GetTileId(command.TargetX, command.TargetY);
            if (!IsValidTileId(anchorTile)) return result;
            bool ground = !IsCompletedMoatTile(anchorTile);
            int Global(int local)
            {
                int x = command.TargetX + local % side - radius, y = command.TargetY + local / side - radius;
                return (uint)x < MapWidth && (uint)y < MapWidth ? y * MapWidth + x : -1;
            }
            bool Edge(int from, int to, int direction, bool reverse, out bool wet, out bool structure)
            {
                int a = Global(from), b = Global(to); wet = structure = false;
                if (a < 0 || b < 0) return false;
                return reverse ? map.Edge(b, a, (direction + 4) & 7, ground, out wet, out structure)
                    : map.Edge(a, b, direction, ground, out wet, out structure);
            }
            var toward = new FastRouteField(side, side,
                (int a, int b, int d, out bool wet, out bool structure) => Edge(a, b, d, false, out wet, out structure));
            var away = new FastRouteField(side, side,
                (int a, int b, int d, out bool wet, out bool structure) => Edge(a, b, d, true, out wet, out structure));
            int center = radius * side + radius; toward.Reset(center); away.Reset(center);
            var candidates = new List<int>();
            for (int node = 0; node < side * side; node++) candidates.Add(node);
            candidates.Sort((a, b) => {
                int da = Math.Max(Math.Abs(a % side - radius), Math.Abs(a / side - radius));
                int db = Math.Max(Math.Abs(b % side - radius), Math.Abs(b / side - radius));
                int order = da.CompareTo(db); return order != 0 ? order : a.CompareTo(b);
            });
            foreach (int local in candidates)
            {
                int global = Global(local);
                if (global < 0 || movementTargetAvailability[global] == 0) continue;
                int tile = GameTileManagerAPI.Instance.GetTileId(global % MapWidth, global / MapWidth);
                // Keep ground and moat anchors on their respective surface. Thus a
                // distributed ground target never acquires an extra moat crossing.
                if (!IsValidTileId(tile) || ground == IsCompletedMoatTile(tile)) continue;
                if (toward.Advance(local, side * side) != FastRouteStatus.Found ||
                    away.Advance(local, side * side) != FastRouteStatus.Found) continue;
                away.GetPath(local, out int[] reverse);
                var suffix = new byte[reverse.Length - 1];
                for (int i = reverse.Length - 1, at = 0; i > 0; i--, at++)
                {
                    int dx = reverse[i - 1] % side - reverse[i] % side;
                    int dy = reverse[i - 1] / side - reverse[i] / side;
                    for (int d = 0; d < 8; d++)
                        if (WeightedMoatRoutePlanner.DirectionX[d] == dx && WeightedMoatRoutePlanner.DirectionY[d] == dy)
                        { suffix[at] = (byte)d; break; }
                }
                result.Slots.Add(global); result.Suffixes.Add(global, suffix);
            }
            return result;
        }

        private bool TryFastGroupSuffix(int player, int target, out int anchor, out byte[] suffix, int unitId = 0)
        {
            anchor = target; suffix = null;
            FastGroupDistribution distribution = fastDistribution;
            if (activeMoveCommand == null && unitId > 0 &&
                GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out var unit) && unit != null)
                fastUnitDistributions.TryGetValue(new FastUnitIdentity(unitId, unit->r_GlobalId), out distribution);
            else if (distribution?.Command != activeMoveCommand) distribution = null;
            if (distribution == null || activeAttackCommand != null ||
                activeMoatWorkSelection != null || distribution.Player != player) return false;
            if (fastSimulationRouting != null && fastSimulationRouting.Maps.TryGetValue(player, out var map) &&
                distribution.Revision != map.Revision)
            {
                var refreshed = BuildFastDistribution(distribution.Command, player, map);
                distribution.Slots.Clear(); distribution.Slots.AddRange(refreshed.Slots);
                distribution.Suffixes.Clear();
                foreach (var entry in refreshed.Suffixes) distribution.Suffixes.Add(entry.Key, entry.Value);
                distribution.Revision = refreshed.Revision;
            }
            if (!distribution.Suffixes.TryGetValue(target, out suffix)) return false;
            anchor = distribution.Anchor; return true;
        }
    }
}
