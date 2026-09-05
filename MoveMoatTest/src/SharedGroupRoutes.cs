using System;
using System.Collections.Generic;
using System.Diagnostics;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace MoveMoatTest
{
    internal sealed class GroupRouteSession
    {
        internal readonly bool Enabled;
        internal readonly bool Shared;
        internal int Epoch = -1, Tick = -1;
        internal long Revision = -1;
        internal bool Captured, Failed;
        internal readonly Dictionary<int, uint> Identities = new Dictionary<int, uint>();
        internal readonly Dictionary<int, Group> Units = new Dictionary<int, Group>();
        internal long MainSearches, ConnectorNodes, Reused, Fallbacks, MainTicks, ConnectorTicks;
        internal double MainMilliseconds => MainTicks*1000.0/Stopwatch.Frequency;
        internal double ConnectorMilliseconds => ConnectorTicks*1000.0/Stopwatch.Frequency;
        internal GroupRouteSession(bool enabled, bool shared) { Enabled = enabled; Shared = shared; }
        internal sealed class Member
        {
            internal int Id, Player, Start;
            internal uint GlobalId;
            internal WeightedMovementCostProfile Profile;
        }
        internal sealed class Group
        {
            internal Member Reference;
            internal int Count;
            internal readonly List<Route> Routes = new List<Route>();
        }
        internal sealed class Route
        {
            internal int Target;
            internal bool Reserved;
            internal SharedRouteField Field;
        }
        internal void Capture(List<Member> remaining, int width)
        {
            // Input ID order also fixes ties for compatible profile partitions.
            remaining.Sort((a,b) => a.Id.CompareTo(b.Id));
            while (remaining.Count > 0)
            {
                Member first = remaining[0];
                var compatible = remaining.FindAll(m => m.Player == first.Player && m.Profile.Equals(first.Profile));
                while (compatible.Count > 0)
                {
                    long sumX = 0, sumY = 0;
                    foreach (Member m in compatible) { sumX += m.Start % width; sumY += m.Start / width; }
                    Member reference = null; long best = long.MaxValue;
                    foreach (Member m in compatible)
                    {
                        long dx = (long)(m.Start % width) * compatible.Count - sumX;
                        long dy = (long)(m.Start / width) * compatible.Count - sumY;
                        long distance = dx * dx + dy * dy;
                        if (distance < best) { best = distance; reference = m; }
                    }
                    var group = new Group { Reference = reference };
                    for (int i = compatible.Count - 1; i >= 0; i--)
                    {
                        Member m = compatible[i];
                        if (Math.Max(Math.Abs(m.Start % width-reference.Start % width), Math.Abs(m.Start / width-reference.Start / width)) > SharedRouteField.Radius) continue;
                        Units[m.Id] = group; Identities[m.Id] = m.GlobalId; group.Count++; remaining.Remove(m); compatible.RemoveAt(i);
                    }
                }
            }
            Captured = true;
        }
    }

    internal sealed unsafe partial class MoveMoatPathTest
    {
        private bool ExtensionsEnabled => activeMoveCommand?.Routes.Enabled ?? activeAttackCommand?.Routes.Enabled ??
            unitMoveFrame?.ExtensionsEnabledAtStart ?? MoveMoatTestPlugin.Settings.EnableMod;
        private bool TryBuildSharedGroupRoute(PlanScope plan, GameUnit* unit, int sx, int sy, int tx, int ty,
            WeightedMovementCostProfile profile, bool reserved, MoatSearchLimit[] limits,
            out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute encoded)
        {
            try { return TryBuildSharedGroupRouteCore(plan,unit,sx,sy,tx,ty,profile,reserved,limits,out summary,out encoded); }
            catch (Exception ex)
            {
                GroupRouteSession session=activeMoveCommand?.Routes ?? activeAttackCommand?.Routes;
                if(session != null) { session.Failed=true;session.Fallbacks++; }
                TryLogDiagnosticFailure("shared-route-individual-fallback",ex);
                summary=default;encoded=default;return false;
            }
        }
        private bool TryBuildSharedGroupRouteCore(PlanScope plan, GameUnit* unit, int sx, int sy, int tx, int ty,
            WeightedMovementCostProfile profile, bool reserved, MoatSearchLimit[] limits,
            out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute encoded)
        {
            summary = default; encoded = default;
            GroupRouteSession session = activeMoveCommand?.Routes ?? activeAttackCommand?.Routes;
            if (session == null || !session.Enabled || !session.Shared || unit == null || plan == null ||
                plan.MoatWorkMovement || activeMoatWorkSelection != null ||
                GetCurrentUnitMoveFrame()?.Parent != null) return false;
            int tick = CaptureCurrentGameTick();
            if (session.Epoch != mapEpoch || session.Tick != tick || session.Revision != placementRevision)
            {
                session.Units.Clear(); session.Identities.Clear(); session.Captured = false; session.Failed = false;
                session.Epoch = mapEpoch; session.Tick = tick; session.Revision = placementRevision;
            }
            if (session.Failed) { session.Fallbacks++; return false; }
            if (!session.Captured)
            {
                var members = new List<GroupRouteSession.Member>();
                IEnumerable<int> ids = activeMoveCommand != null ? (IEnumerable<int>)activeMoveCommand.ActiveUnitIdsAtDispatch : activeAttackCommand.CandidateUnitIds;
                foreach (int id in ids)
                {
                    if (!GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* u) || u == null ||
                        u->r_AliveState != AliveState.IsAlive || !CanDigMoat(u) ||
                        !TryCaptureWeightedMovementCostProfile(u, out WeightedMovementCostProfile p, out _)) continue;
                    GetNativeMovementStart(u, out int x, out int y);
                    if ((uint)x >= MapWidth || (uint)y >= MapWidth) continue;
                    members.Add(new GroupRouteSession.Member { Id = id, GlobalId = u->r_GlobalId,
                        Player = u->r_ControllableForPlayerId, Start = x+y*MapWidth, Profile = p });
                }
                session.Capture(members, MapWidth);
            }
            if (!session.Units.TryGetValue(plan.UnitId, out GroupRouteSession.Group group) || group.Count < 2 ||
                (!session.Identities.TryGetValue(plan.UnitId, out uint identity) || identity != unit->r_GlobalId) ||
                unit->r_ControllableForPlayerId != plan.PlayerId || unit->r_AliveState != AliveState.IsAlive ||
                group.Reference.Player != plan.PlayerId || !group.Reference.Profile.Equals(profile) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(group.Reference.Id, out GameUnit* reference) || reference == null ||
                reference->r_AliveState != AliveState.IsAlive || reference->r_GlobalId != group.Reference.GlobalId || reference->r_ControllableForPlayerId != plan.PlayerId)
                { session.Fallbacks++; return false; }
            int start = sx+sy*MapWidth, target = tx+ty*MapWidth;
            // Geometry stays anchored at the command snapshot even after Vanilla starts the reference unit.
            int rx=group.Reference.Start%MapWidth, ry=group.Reference.Start/MapWidth;
            if (Math.Max(Math.Abs(sx-rx), Math.Abs(sy-ry)) > SharedRouteField.Radius) { session.Fallbacks++; return false; }
            GroupRouteSession.Route route = null;
            foreach (var r in group.Routes)
                if (r.Reserved == reserved && Math.Max(Math.Abs(tx-r.Target%MapWidth), Math.Abs(ty-r.Target/MapWidth)) <= SharedRouteField.Radius) { route=r; break; }
            if (route == null)
            {
                session.MainSearches++;
                route = new GroupRouteSession.Route { Target = target, Reserved = reserved };
                // Failed main routes are remembered within this exact synchronous state.
                group.Routes.Add(route);
                long mainStarted=Stopwatch.GetTimestamp();
                bool found=weightedMoatRoutePlanner.TryBuildEncoded(plan.PlayerId, rx, ry, tx, ty, profile, reserved,
                    out WeightedMoatRouteSummary main, out WeightedMoatEncodedRoute bytes);
                session.MainTicks+=Stopwatch.GetTimestamp()-mainStarted;
                if (found && main.StructuralEdges == 0 && bytes.IsValid)
                {
                    int[] nodes = new int[bytes.DirectionCount+1]; nodes[0] = group.Reference.Start;
                    for (int i=0;i<bytes.DirectionCount;i++)
                    { int d=(bytes.Bytes[i>>1] >> ((i&1)*4)) & 15; nodes[i+1]=nodes[i]+WeightedMoatRoutePlanner.DirectionX[d]+WeightedMoatRoutePlanner.DirectionY[d]*MapWidth; }
                    long connectorStarted=Stopwatch.GetTimestamp();
                    route.Field = new SharedRouteField(MapWidth, nodes, (a,b) => SharedEdgeCost(plan.PlayerId, profile, a,b, reserved ? target : -1));
                    session.ConnectorTicks+=Stopwatch.GetTimestamp()-connectorStarted;
                    session.ConnectorNodes += route.Field.Expanded;
                }
            }
            if (route.Field == null || !route.Field.TryConnect(start,target,out int[] path)) { session.Fallbacks++; return false; }
            byte[] output = new byte[path.Length/2];
            for (int i=1;i<path.Length;i++)
            {
                int dx=path[i]%MapWidth-path[i-1]%MapWidth, dy=path[i]/MapWidth-path[i-1]/MapWidth, direction=-1;
                for (int d=0;d<8;d++) if (WeightedMoatRoutePlanner.DirectionX[d]==dx && WeightedMoatRoutePlanner.DirectionY[d]==dy) { direction=d; break; }
                if (direction < 0) { session.Fallbacks++; return false; }
                output[(i-1)>>1] |= (byte)(direction << (((i-1)&1)*4));
            }
            fixed (byte* buffer=output)
                if (!weightedMoatRoutePlanner.TryDescribeEncodedPath(plan.PlayerId,sx,sy,tx,ty,profile,buffer,path.Length-1,reserved,out summary) ||
                    summary.StructuralEdges != 0 || summary.MoatEdges == 0) { session.Fallbacks++; return false; }
            if (limits != null) foreach (MoatSearchLimit limit in limits)
                if (!limit.Allows(summary.GroundEdges,summary.MoatEdges,0)) { session.Fallbacks++; return false; }
            encoded = new WeightedMoatEncodedRoute(output,path.Length-1); session.Reused++; return true;
        }
        private long SharedEdgeCost(int player, WeightedMovementCostProfile profile, int from, int to, int terminal)
        {
            if (from == terminal) return -1;
            int x=from%MapWidth,y=from/MapWidth,nx=to%MapWidth,ny=to/MapWidth,direction=-1;
            for(int d=0;d<8;d++) if(WeightedMoatRoutePlanner.DirectionX[d]==nx-x && WeightedMoatRoutePlanner.DirectionY[d]==ny-y) {direction=d;break;}
            if(direction<0 || !weightedMoatRoutePlanner.TryGetTraversalEdge(player,x,y,GameTileManagerAPI.Instance.GetTileId(x,y),
                nx,ny,GameTileManagerAPI.Instance.GetTileId(nx,ny),direction,to==terminal,to==terminal,MoatTraversalPolicy.FriendlyOnly,
                out MoatTraversalEdgeKind kind,out bool structure) || structure) return -1;
            return profile.GetEdgeFixedCost(kind == MoatTraversalEdgeKind.FriendlyMoat);
        }
    }
}
