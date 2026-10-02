using System;
using System.Collections.Generic;
using System.IO;

namespace EnemyBridgePathTest
{
    internal static class DrawbridgeClosureTests
    {
        private const int Width = 13;
        private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
        private static int count;
        private static void Check(bool value, string message)
        { count++; if (!value) throw new Exception(message); }
        private static int Tile(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Width
            ? y * Width + x : -1;

        internal static int Run()
        {
            count = 0;
            // Independent fixtures extracted from Native 2D1A30, not the production predicate.
            int[][] native = {
                new[] {0,0,0,0,0, 1,1,1,1,1, 1,1,1,1,1, 1,1,1,1,1, 0,0,0,0,0},
                new[] {0,1,1,1,0, 0,1,1,1,0, 0,1,1,1,0, 0,1,1,1,0, 0,1,1,1,0}
            };
            for (int rotation = 0; rotation < 4; rotation++)
            {
                var closed = new HashSet<int>();
                byte[] masks = NewMasks();
                var owners = new GateEdgeOwnership();
                int changed = 0;
                for (int cell = 0; cell < 25; cell++)
                {
                    bool expected = native[rotation & 1][cell] != 0;
                    Check(DrawbridgeClosurePolicy.IsClosureCell(rotation * 2, cell) == expected,
                        "native mapper rotation/cell");
                    if (!expected) continue;
                    int x = 4 + cell % 5, y = 4 + cell / 5;
                    int tile = Tile(x, y);
                    closed.Add(tile);
                    changed += DrawbridgeClosurePolicy.BlockCell(masks, tile, x, y, Tile, owners, 578, 539);
                }
                Check(closed.Count == 15, "native closure has fifteen cells");
                int expectedChanges = 0;
                for (int y = 1; y < Width - 1; y++)
                for (int x = 1; x < Width - 1; x++)
                for (int direction = 0; direction < 8; direction++)
                {
                    int from = Tile(x, y), to = Tile(x + Dx[direction], y + Dy[direction]);
                    bool blocked = closed.Contains(from) || closed.Contains(to);
                    Check(((masks[from] >> direction) & 1) == (blocked ? 0 : 1),
                        "exact native tile isolation including diagonal entry/exit and nearby land");
                    if (blocked)
                    {
                        expectedChanges++;
                        Check(owners.Resolve(from, direction) == 578 && owners.ResolveBridge(from, direction) == 539,
                            "exact separate gate and bridge attribution");
                    }
                }
                Check(changed == expectedChanges, "edge count includes every newly blocked direction exactly once");
                Check(DrawbridgeClosurePolicy.BlockCell(masks, Tile(6,6), 6,6, Tile, owners,578,539) == 0,
                    "repeated masking has no extra changes");
                // A true opening beside the deck and ordinary wall climbing outside it remain possible.
                Check((masks[Tile(3,6)] & 1) != 0, "real side path preserved");
                foreach (int destination in new[] { Tile(6,6), Tile(10,6), Tile(2,10) })
                    Check(Cost(Tile(2,6), destination, (a,b,d) => !closed.Contains(a) && !closed.Contains(b)) ==
                        Cost(Tile(2,6), destination, (a,b,d) => (masks[a] & (1 << d)) != 0),
                        "hypothetical mask matches independent physical closure");
                // Several bridges of one gate can overlap: the gate stays exact, bridge ambiguity is explicit.
                DrawbridgeClosurePolicy.BlockCell(masks, Tile(6,6),6,6,Tile,owners,578,540);
                Check(owners.Resolve(Tile(6,6),2) == 578 && owners.ResolveBridge(Tile(6,6),2) == -1,
                    "overlapping bridges do not erase common gate attribution");
                DrawbridgeClosurePolicy.BlockCell(masks, Tile(1,1),1,1,Tile,owners,578,540);
                Check(owners.ResolveBridge(Tile(1,1),2) == 540, "second bridge's separate identity");
            }
            Check(!DrawbridgeClosurePolicy.IsClosureCell(8,0) && !DrawbridgeClosurePolicy.IsClosureCell(-1,10),
                "unknown rotation fails open");
            var aggregate = new EnemyGatePathfindingTest.AiGateDecisionAggregate(); aggregate.Reset();
            for (int i = 0; i < 80; i++)
                aggregate.RecordGateState(5,578,"bridge-reachability","same-pcl",3,i,1,1,"bridge=" + i);
            var rows = aggregate.Drain(out var definitions);
            Check(definitions.Length == 80 && rows.Length == 80, "more than thirty-two bridge states are retained");
            return count;
        }

        private static byte[] NewMasks()
        { var masks = new byte[Width*Width]; for (int i=0;i<masks.Length;i++) masks[i]=255; return masks; }
        private static int Cost(int start, int target, Func<int,int,int,bool> allow)
        {
            var costs = new int[Width*Width]; var done = new bool[costs.Length];
            for (int i=0;i<costs.Length;i++) costs[i]=int.MaxValue; costs[start]=0;
            while (true)
            {
                int current=-1;
                for (int i=0;i<costs.Length;i++) if (!done[i] && costs[i]!=int.MaxValue &&
                    (current<0 || costs[i]<costs[current])) current=i;
                if (current<0) return -1;
                if (current==target) return costs[current];
                done[current]=true;
                for (int d=0;d<8;d++)
                {
                    int to=Tile(current%Width+Dx[d],current/Width+Dy[d]);
                    if (to<0 || !allow(current,to,d)) continue;
                    int next=costs[current]+((d&1)==0 ? 10 : 14);
                    if (next<costs[to]) costs[to]=next;
                }
            }
        }
    }
}
