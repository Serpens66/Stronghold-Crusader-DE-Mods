using System;
using System.Collections.Generic;
using System.Linq;
using AreaConstructionTest;

var result = new List<Tile>();
var anchor = new Tile(20, 20);

void Grid(Tile end, int width = 4, int height = 4, int size = 90) =>
    AreaGrid.Fill(result, anchor, end, width, height, size);

void Expect(params (int X, int Y)[] expected)
{
    if (!result.Select(tile => (tile.X, tile.Y)).SequenceEqual(expected))
        throw new InvalidOperationException("Unexpected ordered placement grid.");
}

Grid(new Tile(23, 23));
Expect((20, 20));
Grid(new Tile(24, 20));
Expect((20, 20), (24, 20), (20, 24), (24, 24));
Grid(new Tile(12, 15));
Expect((20, 20), (16, 20), (20, 16), (16, 16));
Grid(new Tile(12, 20));
Expect((20, 20), (16, 20), (20, 24), (16, 24));

Grid(new Tile(29, 29));
if (result.Count != 16 || result.Distinct().Count() != 16 ||
    result.Any(tile => tile.X == 28 || tile.Y == 28) ||
    !result.Contains(new Tile(29, 29)))
    throw new InvalidOperationException("The tiled blocks must leave one tile between footprints.");

Grid(new Tile(28, 28));
Expect((20, 20), (24, 20), (20, 24), (24, 24));
Grid(new Tile(26, 25), 3, 2);
Expect((20, 20), (23, 20), (20, 22), (23, 22),
    (20, 25), (23, 25), (20, 27), (23, 27));
Grid(new Tile(int.MaxValue, int.MaxValue), size: 26);
Expect((20, 20), (24, 20), (20, 24), (24, 24));
Console.WriteLine("PASS: fixed anchor, all drag directions, shrinking, spacing, rectangular footprints and map bounds.");

void Storage(Tile end, int width = 5, int height = 5, int size = 90) =>
    StorageGrid.Fill(result, anchor, end, width, height, size);

Storage(new Tile(24, 24));
Expect((20, 20));
Storage(new Tile(35, 20));
Expect((20, 20), (25, 20), (30, 20), (35, 20));
Storage(new Tile(20, 9));
Expect((20, 20), (20, 15), (20, 10));
Storage(new Tile(30, 25));
Expect((20, 20), (25, 20), (30, 20), (20, 25), (25, 25), (30, 25));
Storage(new Tile(15, 25));
Expect((20, 20), (15, 20), (20, 25), (15, 25));
Storage(new Tile(15, 15));
Expect((20, 20), (15, 20), (20, 15), (15, 15));
Storage(new Tile(25, 15));
Expect((20, 20), (25, 20), (20, 15), (25, 15));
Storage(new Tile(25, 20));
Expect((20, 20), (25, 20));
Storage(new Tile(10, 10), 3, 4);
Expect((20, 20), (17, 20), (14, 20), (11, 20),
    (20, 16), (17, 16), (14, 16), (11, 16),
    (20, 12), (17, 12), (14, 12), (11, 12));
Storage(new Tile(int.MaxValue, int.MinValue), size: 22);
Expect((20, 20), (20, 15), (20, 10), (20, 5), (20, 0));

var accepted = new List<Tile>();
var quoted = new List<(Tile Site, Tile? Neighbor)>();
var unvisited = new HashSet<Tile>();
void Connect(Func<Tile, bool> eligible)
{
    quoted.Clear();
    StorageGrid.CollectConnected(result, 5, 5, (site, neighbor) =>
    {
        quoted.Add((site, neighbor));
        return eligible(site);
    }, accepted, unvisited);
}

Storage(new Tile(35, 20));
Connect(site => site.X != 25);
if (!accepted.SequenceEqual(new[] { anchor }) ||
    !quoted.Select(quote => quote.Site.X).SequenceEqual(new[] { 20, 25 }))
    throw new InvalidOperationException("A blocked segment must prevent quoting disconnected sites.");

Storage(new Tile(30, 25));
Connect(site => !(site.X == 25 && site.Y == 20));
var detour = new[] { anchor, new Tile(20, 25), new Tile(25, 25), new Tile(30, 25), new Tile(30, 20) };
if (!accepted.SequenceEqual(detour) || quoted[0].Neighbor.HasValue ||
    !quoted.Single(quote => quote.Site.Equals(detour[4])).Neighbor.Equals(detour[3]) ||
    quoted.Select(quote => quote.Site).Distinct().Count() != quoted.Count)
    throw new InvalidOperationException("Connected sites must follow a deterministic edge-connected detour and be quoted once.");

Connect(site => !site.Equals(anchor));
if (accepted.Count != 0 || quoted.Count != 1)
    throw new InvalidOperationException("A failed Vanilla anchor must prevent expansion.");

int budget = 2;
Connect(_ => budget-- > 0);
if (!accepted.SequenceEqual(new[] { anchor, new Tile(25, 20) }))
    throw new InvalidOperationException("A limited quote must keep the closest connected sites in deterministic order.");
Console.WriteLine("PASS: storage rectangles, edge-connected detours, rejected anchors and budget-limited expansion.");
