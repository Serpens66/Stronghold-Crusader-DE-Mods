using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using APIShared;
using APIShared.Automation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace PathfindingLoadTest
{
    // Route shape scenarios for Pathfinding TODO G10 (shape-gap, shape-end): 100 spearmen get one
    // ordinary player move to a diagonal target behind a wall. Reads every member's published native
    // route once right after the command and samples the real movement until all members settled.
    // Measures straight pieces, steps and arrival progress; it changes no unit, route or setting.
    internal sealed partial class Fixture
    {
        // Same hash-bound unit-manager contract as Pathfinding's publication: per-unit direction
        // buffer at +B4FE78 + one-based unitId * 1000 bytes, two 4-bit directions per byte (low first).
        private const int PathBufferOffset = 0xB4FE78, PathBufferStride = 1000;
        private const int ShapeSampleTicks = 50, ShapeMaximumSamples = 60;

        private struct RouteShape { internal int Routes, Steps, Pieces, Turns, Invalid, EndMismatch; internal double Euclid; }

        private void ShapeTarget(int x, int y, out int tx, out int ty)
        {
            // Diagonal target below-right of the wall: the gap variant passes the middle gap,
            // the end variant has to round the wall's upper end.
            tx = x + width + 24; ty = y + height + 6;
            // "far": same wall as "end", but a distant diagonal target; long routes show L shapes most.
            if (shape == "far") { tx = x + width + 90; ty = y + height + 45; }
        }

        private void PrepareShapeWall(int wx, int middle)
        {
            var api = GameBuildingManagerAPI.Instance;
            if (shape == "gap")
            {
                api.CreateWall(owner, wx, originY - 12, wx, middle - 3, eMappers.MAPPER_WALL);
                LogWallOutcome("upper", wx, originY - 12, middle - 3);
                api.CreateWall(owner, wx, middle + 3, wx, originY + height + 12, eMappers.MAPPER_WALL);
                LogWallOutcome("lower", wx, middle + 3, originY + height + 12);
            }
            else
            {
                api.CreateWall(owner, wx, originY - 6, wx, originY + height + 12, eMappers.MAPPER_WALL);
                LogWallOutcome("single", wx, originY - 6, originY + height + 12);
            }
        }

        private bool ShapeWallExpected(int x, int y, int wx, int middle) =>
            x == wx && (shape == "gap" ? (y <= middle - 3 || y >= middle + 3) : y >= originY - 6);

        // Maximal digital straight pieces: at most two adjacent directions, the minor one only in
        // single steps, interior major runs differing by at most one (as SurfaceCoreTests).
        private static int StraightPieces(List<int> path)
        {
            int pieces = 0, i = 0;
            while (i < path.Count)
            {
                pieces++; int a = path[i], b = -1, j = i + 1;
                var runs = new List<KeyValuePair<int, int>> { new KeyValuePair<int, int>(a, 1) };
                for (; j < path.Count; j++)
                {
                    int d = path[j];
                    if (d != a && b < 0) { if (((d - a) & 7) != 1 && ((a - d) & 7) != 1) break; b = d; }
                    if (d != a && d != b) break;
                    var next = new List<KeyValuePair<int, int>>(runs);
                    if (next[next.Count - 1].Key == d) next[next.Count - 1] = new KeyValuePair<int, int>(d, next[next.Count - 1].Value + 1);
                    else next.Add(new KeyValuePair<int, int>(d, 1));
                    int longA = 0, longB = 0, countA = 0, countB = 0;
                    foreach (var r in next) { if (r.Key == a) { countA += r.Value; longA = Math.Max(longA, r.Value); } else { countB += r.Value; longB = Math.Max(longB, r.Value); } }
                    bool ok = !(longA > 1 && longB > 1);
                    int major = longB > 1 || (longA <= 1 && countB > countA) ? b : a;
                    int lo = int.MaxValue, hi = 0;
                    for (int k = 1; k + 1 < next.Count; k++) if (next[k].Key == major) { lo = Math.Min(lo, next[k].Value); hi = Math.Max(hi, next[k].Value); }
                    if (lo != int.MaxValue && (hi - lo > 1 || (next[0].Key == major && next[0].Value > hi + 1) ||
                        (next[next.Count - 1].Key == major && next[next.Count - 1].Value > hi + 1))) ok = false;
                    if (!ok) break;
                    runs = next;
                }
                i = j;
            }
            return pieces;
        }

        private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 }, Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private static string RunText(List<int> path)
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < path.Count;)
            {
                int j = i; while (j < path.Count && path[j] == path[i]) j++;
                if (text.Length > 0) text.Append(' ');
                text.Append(path[i]).Append('x').Append(j - i); i = j;
            }
            return text.ToString();
        }

        private unsafe RouteShape ReadRoutes()
        {
            var shape = new RouteShape();
            var path = new List<int>(2000);
            foreach (var identity in units)
            {
                Require(UnitAccess.TryGetById(identity.Id, out GameUnit* u, out _) && u->r_GlobalId == identity.Global, "Shape route owner changed");
                int length = u->r_PathPlanLength;
                if (length <= 0) continue;
                if (length > 2 * PathBufferStride) { shape.Invalid++; continue; }
                byte* buffer = (byte*)unitManager + PathBufferOffset + (long)identity.Id * PathBufferStride;
                path.Clear(); bool valid = true; int x = u->r_CurrentTilePositionX, y = u->r_CurrentTilePositionY;
                for (int k = 0; k < length; k++)
                {
                    int d = (buffer[k >> 1] >> ((k & 1) * 4)) & 0xF;
                    if (d > 7) { valid = false; break; }
                    path.Add(d); x += Dx[d]; y += Dy[d];
                }
                if (!valid) { shape.Invalid++; continue; }
                // The route was read before the first step; its end must be the member's own target.
                if (u->r_CurrentPathPlanIndex != 0 || x != u->r_TargetTilePositionX || y != u->r_TargetTilePositionY) shape.EndMismatch++;
                shape.Routes++; shape.Steps += length; shape.Pieces += StraightPieces(path);
                // Every 25th route as run-length direction text (e.g. "2x12 3x5"), so the shape is readable in the log.
                if (shape.Routes % 25 == 1) log("shape-route-sample id=" + identity.Id + " start=" + u->r_CurrentTilePositionX + "," +
                    u->r_CurrentTilePositionY + " target=" + x + "," + y + " runs=" + RunText(path));
                for (int k = 0; k < path.Count; k++)
                {
                    shape.Euclid += (path[k] & 1) != 0 ? Math.Sqrt(2) : 1;
                    if (k > 0 && path[k] != path[k - 1]) shape.Turns++;
                }
            }
            return shape;
        }

        private IEnumerable<AutomationWait> MoveShape(AutomationRun run)
        {
            BindMove();
            RouteShape routes = default;
            double elapsedMs = 0;
            run.InSimulation(run.CurrentMission, () => {
                RequireTargetFree();
                Require(SelectionMatches(), "Selection/group changed before command");
                for (int i = 0; i < units.Count; i++) units[i] = Capture(units[i].Id);
                var clock = Stopwatch.StartNew();
                move(unitManager, tribe, targetX, targetY, 0, 0);
                clock.Stop(); elapsedMs = clock.Elapsed.TotalMilliseconds;
                routes = ReadRoutes();
                return 0;
            });
            string F(double v) => v.ToString("F3", CultureInfo.InvariantCulture);
            int r = Math.Max(1, routes.Routes);
            log("shape-routes scenario=" + shape + " count=" + count + " commandMs=" + F(elapsedMs) + " routes=" + routes.Routes +
                " steps=" + routes.Steps + " pieces=" + routes.Pieces + " turns=" + routes.Turns + " euclid=" + F(routes.Euclid) +
                " piecesPerRoute=" + F((double)routes.Pieces / r) + " stepsPerRoute=" + F((double)routes.Steps / r) +
                " invalid=" + routes.Invalid + " endMismatch=" + routes.EndMismatch);
            Require(routes.Routes >= count * 9 / 10 && routes.Invalid == 0, "Too few readable published routes");
            // Real movement: a member is settled once it moved and kept its tile for one sample.
            var last = new Dictionary<int, long>();
            int settledAll = -1;
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            for (int sample = 1; sample <= ShapeMaximumSamples; sample++)
            {
                yield return run.WaitForTicks(run.CurrentMission, ShapeSampleTicks);
                int moved = 0, settled = 0, near = 0, live = 0;
                run.InSimulation(run.CurrentMission, () => { CountSettled(last, ref live, ref moved, ref settled, ref near); return 0; });
                log("shape-sample scenario=" + shape + " tick=" + (sample * ShapeSampleTicks) + " live=" + live + " moved=" + moved +
                    " settled=" + settled + " nearTarget=" + near);
                Require(live == count, "Shape fixture units died or identities changed");
                if (settled == count) { settledAll = sample * ShapeSampleTicks; break; }
            }
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            log("shape-complete scenario=" + shape + " allSettledTick=" + settledAll + " sampleTicks=" + ShapeSampleTicks);
            Require(settledAll > 0, "Not every member settled within " + (ShapeSampleTicks * ShapeMaximumSamples) + " ticks");
        }

        private unsafe void CountSettled(Dictionary<int, long> last, ref int live, ref int moved, ref int settled, ref int near)
        {
            foreach (var identity in units)
            {
                if (!UnitAccess.TryGetById(identity.Id, out GameUnit* u, out _) || u->r_GlobalId != identity.Global || !UnitAccess.IsReallyAlive(u)) continue;
                live++;
                int x = u->r_CurrentTilePositionX, y = u->r_CurrentTilePositionY;
                long key = ((long)x << 32) | (uint)y;
                bool hasMoved = x != identity.X || y != identity.Y;
                if (hasMoved) moved++;
                if (hasMoved && last.TryGetValue(identity.Id, out long previous) && previous == key && u->r_PathPlanStateBitFlags == 0) settled++;
                if (Math.Max(Math.Abs(x - targetX), Math.Abs(y - targetY)) <= 8) near++;
                last[identity.Id] = key;
            }
        }
    }
}
