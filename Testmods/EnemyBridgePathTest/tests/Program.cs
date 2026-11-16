using System;
using System.Collections.Generic;
using System.Linq;

namespace EnemyBridgePathTest.Tests
{
    // Behaviour tests for the production deck closure core on a synthetic rectangular grid.
    // Regression classes: a window that does not restore the native direction grid exactly,
    // a deck that still connects its surroundings, a gate link in the wrong direction and a
    // wrong landing region for the portal endpoint.
    internal static class Program
    {
        private static int failures, checks;

        private static int Main()
        {
            CutSeparatesDeckAndRestoresExactly();
            LinkJoinsDeckWithGateCorridorOnly();
            JournalKeepsFirstOriginalAcrossRepeatedWrites();
            LandingUsesCentreThenSides();
            AbutmentRowJoinsDeckAndGate();
            Console.WriteLine(failures == 0 ? "EnemyBridgePathTest tests passed: " + checks + " checks." : "EnemyBridgePathTest tests FAILED: " + failures + " of " + checks);
            return failures == 0 ? 0 : 1;
        }

        private static void Check(bool condition, string name)
        {
            checks++;
            if (!condition) { failures++; Console.WriteLine("FAIL: " + name); }
        }

        // 20x20 grid. Row 10 is a moat except the deck columns 9..11 (rows 8..12 form the
        // 5x3 deck). Gate corridor tile at (10,13), a closed gate with walls beside it.
        // Interior (rows 14+) is additionally reachable laterally from deck row 12 via (8,12)
        // and (12,12), reproducing the sideways crossing.
        private sealed class Grid : IDeckGrid
        {
            internal const int W = 20, H = 20;
            internal readonly byte[] Directions = new byte[W * H];
            internal readonly bool[] Walkable = new bool[W * H];
            public byte GetDirections(int tile) => Directions[tile];
            public void SetDirections(int tile, byte value) => Directions[tile] = value;
            public bool TryGetCoordinates(int tile, out int x, out int y) { x = tile % W; y = tile / W; return tile >= 0 && tile < W * H; }
            public bool TryGetTile(int x, int y, out int tile) { tile = y * W + x; return x >= 0 && y >= 0 && x < W && y < H; }
            internal int T(int x, int y) => y * W + x;

            internal void Build(Func<int, int, bool> walkable)
            {
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) Walkable[T(x, y)] = walkable(x, y);
                for (int t = 0; t < W * H; t++)
                {
                    byte bits = 0;
                    if (Walkable[t])
                        for (int d = 0; d < 8; d++)
                        {
                            TryGetCoordinates(t, out int x, out int y);
                            int dx = DirectionBits.Dx[d], dy = DirectionBits.Dy[d];
                            if (!TryGetTile(x + dx, y + dy, out int n) || !Walkable[n]) continue;
                            // Diagonals need both orthogonal corners walkable.
                            if (dx != 0 && dy != 0 && (!TryGetTile(x + dx, y, out int c1) || !Walkable[c1] || !TryGetTile(x, y + dy, out int c2) || !Walkable[c2])) continue;
                            bits |= DirectionBits.Bit[d];
                        }
                    Directions[t] = bits;
                }
            }

            // Directed flood over outgoing bits, seeded in scan order like 0xE49D0.
            internal int[] Components()
            {
                var label = new int[W * H];
                int next = 1;
                for (int seed = 0; seed < W * H; seed++)
                {
                    if (!Walkable[seed] || label[seed] != 0) continue;
                    var queue = new Queue<int>(); queue.Enqueue(seed); label[seed] = next;
                    while (queue.Count > 0)
                    {
                        int t = queue.Dequeue(); TryGetCoordinates(t, out int x, out int y);
                        for (int d = 0; d < 8; d++)
                            if ((Directions[t] & DirectionBits.Bit[d]) != 0 && TryGetTile(x + DirectionBits.Dx[d], y + DirectionBits.Dy[d], out int n) && label[n] == 0)
                            { label[n] = next; queue.Enqueue(n); }
                    }
                    next++;
                }
                return label;
            }
        }

        // Rows 8..12: moat, bridged by the deck columns 9..11; lateral land (8,12)/(12,12).
        // Row 13: wall line with the gate corridor tile (10,13) and lateral land columns 8/12.
        private static Grid Layout(out List<int> deck, out int front, out int gate)
        {
            var g = new Grid();
            g.Build((x, y) =>
            {
                if (y >= 8 && y <= 12) return (x >= 9 && x <= 11) || ((x == 8 || x == 12) && y == 12);
                if (y == 13) return x == 8 || x == 10 || x == 12;
                return true;
            });
            front = g.T(10, 12); gate = g.T(10, 13);
            // Closed gate (as inside a 0xC4BF0(1) window): both corridor apertures are cut.
            Cut(g, front, gate);
            for (int x = 9; x <= 11; x++) Cut(g, gate, g.T(x, 14));
            deck = new List<int>();
            for (int y = 8; y <= 12; y++) for (int x = 9; x <= 11; x++) deck.Add(g.T(x, y));
            return g;
        }

        private static void Cut(Grid g, int a, int b)
        {
            g.TryGetCoordinates(a, out int ax, out int ay); g.TryGetCoordinates(b, out int bx, out int by);
            int d = DirectionBits.FromDelta(bx - ax, by - ay);
            g.Directions[a] = (byte)(g.Directions[a] & ~DirectionBits.Bit[d]);
            g.Directions[b] = (byte)(g.Directions[b] & ~DirectionBits.Bit[DirectionBits.Opposite(d)]);
        }

        private static void CutSeparatesDeckAndRestoresExactly()
        {
            var g = Layout(out var deck, out int front, out int gate);
            var deckSet = new HashSet<int>(deck);
            byte[] original = (byte[])g.Directions.Clone();
            var before = g.Components();
            int outside = g.T(10, 2), inside = g.T(10, 18), lateral = g.T(8, 13);
            Check(before[outside] == before[inside], "lateral deck crossing connects outside and inside before the cut");

            var journal = new DirectionJournal(Grid.W * Grid.H);
            int removed = DeckClosureCore.CutDeck(g, deck, deckSet.Contains, journal);
            Check(removed > 0, "cut removes edges");
            for (int i = 0; i < g.Directions.Length; i++)
            {
                if (deckSet.Contains(i)) continue;
                g.TryGetCoordinates(i, out int x, out int y);
                for (int d = 0; d < 8; d++)
                    if (g.TryGetTile(x + DirectionBits.Dx[d], y + DirectionBits.Dy[d], out int n) && deckSet.Contains(n))
                        Check((g.Directions[i] & DirectionBits.Bit[d]) == 0, "no edge into the deck from " + x + "/" + y);
            }
            foreach (int cell in deck)
            {
                g.TryGetCoordinates(cell, out int x, out int y);
                for (int d = 0; d < 8; d++)
                    if (g.TryGetTile(x + DirectionBits.Dx[d], y + DirectionBits.Dy[d], out int n))
                        Check(deckSet.Contains(n) ? (g.Directions[cell] & DirectionBits.Bit[d]) == (original[cell] & DirectionBits.Bit[d])
                                                  : (g.Directions[cell] & DirectionBits.Bit[d]) == 0, "deck edge rule at " + x + "/" + y + " dir " + d);
            }
            var after = g.Components();
            Check(after[outside] != after[inside], "cut separates outside from inside");
            Check(after[deck[0]] != after[outside] && after[deck[0]] != after[inside] && after[deck[0]] != 0, "deck forms its own island");
            Check(after[lateral] == after[inside], "lateral land stays with the interior");

            journal.Restore(g);
            Check(g.Directions.SequenceEqual(original), "restore returns every direction byte exactly");
            Check(journal.Count == 0, "journal empty after restore");
        }

        private static void LinkJoinsDeckWithGateCorridorOnly()
        {
            var g = Layout(out var deck, out int front, out int gate);
            var deckSet = new HashSet<int>(deck);
            byte[] original = (byte[])g.Directions.Clone();
            var journal = new DirectionJournal(Grid.W * Grid.H);
            DeckClosureCore.CutDeck(g, deck, deckSet.Contains, journal);
            int added = DeckClosureCore.Link(g, front, gate, true, true, journal);
            Check(added == 2, "both aperture directions linked");
            Check((g.Directions[front] & DirectionBits.Bit[4]) != 0 && (g.Directions[gate] & DirectionBits.Bit[0]) != 0, "link uses the front->gate (south) and gate->front (north) bits");
            var c = g.Components();
            Check(c[front] == c[gate], "deck joins the gate corridor component");
            Check(c[front] != c[g.T(10, 2)], "deck stays separated from the outside");
            Check(DeckClosureCore.Link(g, front, gate, false, false, journal) == 0, "closed aperture adds nothing");
            journal.Restore(g);
            Check(g.Directions.SequenceEqual(original), "link is restored exactly");
        }

        private static void JournalKeepsFirstOriginalAcrossRepeatedWrites()
        {
            var g = new Grid();
            g.Directions[5] = 0xAA;
            var journal = new DirectionJournal(Grid.W * Grid.H);
            journal.Write(g, 5, 0x0F); journal.Write(g, 5, 0x00); journal.Write(g, 5, 0xFF);
            Check(journal.Count == 1, "one tile recorded once");
            journal.Restore(g);
            Check(g.Directions[5] == 0xAA, "first original value restored");
            journal.Write(g, 5, 0x11);
            journal.Restore(g);
            Check(g.Directions[5] == 0xAA, "second window records afresh");
        }

        // Game layout seen in test_gates_bridge.sav: the 5x5 bridge footprint has an abutment row
        // (land) between gate and the 3-row moat deck. Footprint rows 8..12 at columns 8..12,
        // deck rows 9..11, abutment row 12 (in front of the gate) and row 8 (far end).
        private static void AbutmentRowJoinsDeckAndGate()
        {
            var g = new Grid();
            g.Build((x, y) =>
            {
                if (y >= 9 && y <= 11) return x >= 8 && x <= 12;     // deck over the moat
                if (y == 12) return x >= 6 && x <= 14;                // abutment row plus a short berm
                if (y == 13) return x == 10;                          // gate corridor in the wall line
                return y < 9 || y > 13;
            });
            int front = g.T(10, 12), gate = g.T(10, 13);
            Cut(g, front, gate);
            for (int x = 9; x <= 11; x++) Cut(g, gate, g.T(x, 14));
            var deck = new List<int>();
            for (int y = 9; y <= 11; y++) for (int x = 8; x <= 12; x++) deck.Add(g.T(x, y));
            var deckSet = new HashSet<int>(deck);
            Func<int, bool> onBridge = t => { g.TryGetCoordinates(t, out int x, out int y); return x >= 8 && x <= 12 && y >= 8 && y <= 12; };
            byte[] original = (byte[])g.Directions.Clone();

            Check(DeckClosureCore.FindNearDeck(g, front, gate, deckSet.Contains, onBridge, out int abutment, out int near), "near deck found across the abutment row");
            Check(abutment == front && near == g.T(10, 11), "abutment is the front tile, near deck the next axis tile");
            Check(!DeckClosureCore.FindNearDeck(g, g.T(10, 2), gate, deckSet.Contains, onBridge, out _, out _), "no deck from an unrelated tile");
            g.TryGetCoordinates(near, out int nx, out int ny);
            int d = DirectionBits.FromDelta(0, 1);
            bool nearToAb = (g.Directions[near] & DirectionBits.Bit[d]) != 0, abToNear = (g.Directions[abutment] & DirectionBits.Bit[DirectionBits.Opposite(d)]) != 0;

            var journal = new DirectionJournal(Grid.W * Grid.H);
            DeckClosureCore.CutDeck(g, deck, deckSet.Contains, journal);
            DeckClosureCore.Link(g, front, gate, true, true, journal);
            DeckClosureCore.Link(g, near, abutment, nearToAb, abToNear, journal);
            var c = g.Components();
            Check(c[near] == c[front] && c[front] == c[gate], "deck, abutment and gate corridor form one region");
            Check(c[g.T(10, 7)] != c[near], "far landing is separated from the deck");
            Check(c[g.T(6, 12)] == c[front], "berm beside the abutment stays with the abutment");
            Check(DeckClosureCore.FindLanding(g, front, gate, onBridge, t => c[t]) == c[g.T(10, 7)], "landing lies beyond the whole footprint");
            journal.Restore(g);
            Check(g.Directions.SequenceEqual(original), "abutment layout restored exactly");
        }

        private static void LandingUsesCentreThenSides()
        {
            var g = Layout(out var deck, out int front, out int gate);
            var deckSet = new HashSet<int>(deck);
            var comps = new int[Grid.W * Grid.H];
            comps[g.T(10, 7)] = 42;
            Check(DeckClosureCore.FindLanding(g, front, gate, deckSet.Contains, t => comps[t]) == 42, "landing is the centre tile beyond the far deck end");
            comps[g.T(10, 7)] = 0; comps[g.T(11, 7)] = 43;
            Check(DeckClosureCore.FindLanding(g, front, gate, deckSet.Contains, t => comps[t]) == 43, "side landing used when the centre is not walkable");
            comps[g.T(11, 7)] = 0;
            Check(DeckClosureCore.FindLanding(g, front, gate, deckSet.Contains, t => comps[t]) == 0, "no walkable landing keeps the endpoint");
            Check(DeckClosureCore.FindLanding(g, front, g.T(11, 13), deckSet.Contains, t => comps[t]) == 0, "diagonal gate offset is rejected");
        }
    }
}
