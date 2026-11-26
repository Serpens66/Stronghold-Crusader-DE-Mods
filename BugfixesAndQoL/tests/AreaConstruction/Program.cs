using System;
using System.Collections.Generic;
using System.Linq;
using BugfixesAndQoL.AreaConstruction;
using SHCDESE.Interop;

foreach (var (tool, workers, unitType, minimumRecords) in new[]
{
    (eMappers.MAPPER_CATAPULT, 2, eChimps.CHIMP_TYPE_ENGINEER, 11),
    (eMappers.MAPPER_TREBUCHET, 3, eChimps.CHIMP_TYPE_ENGINEER, 11),
    (eMappers.MAPPER_SIEGE_TOWER, 4, eChimps.CHIMP_TYPE_ENGINEER, 11),
    (eMappers.MAPPER_BATTERING_RAM, 4, eChimps.CHIMP_TYPE_ENGINEER, 11),
    (eMappers.MAPPER_PORTABLE_SHIELD, 1, eChimps.CHIMP_TYPE_ENGINEER, 11),
    (eMappers.MAPPER_ARAB_BALLISTA, 2, eChimps.CHIMP_TYPE_ENGINEER, 20),
    (eMappers.MAPPER_TUNNEL_CONSTRUCTION, 1, eChimps.CHIMP_TYPE_TUNNELER, 3)
})
{
    if (CrewPlacement.Required(tool) != workers || CrewPlacement.UnitType(tool) != unitType ||
        CrewPlacement.MinimumFreeRecords(tool) != minimumRecords)
        throw new InvalidOperationException($"Unexpected Vanilla placement requirements for {tool}.");

    var crewBudget = new CrewBudget(workers, 3 * workers - 1);
    if (!crewBudget.CanReserve)
        throw new InvalidOperationException($"The first {tool} must fit.");
    crewBudget.HoldPending(workers);
    if (!crewBudget.HasPendingOrder || !crewBudget.CanReserve)
        throw new InvalidOperationException($"A pending {tool} must leave enough crew for exactly one more site.");
    crewBudget.Reserve();
    if (crewBudget.CanReserve)
        throw new InvalidOperationException($"The remaining crew cannot build another {tool}.");

    crewBudget = new CrewBudget(workers, 2 * workers - 1);
    if (crewBudget.HasPendingOrder || !crewBudget.CanReserve)
        throw new InvalidOperationException($"The live selection after a completed {tool} must not be charged twice.");
    crewBudget = new CrewBudget(workers, workers - 1);
    if (crewBudget.CanReserve)
        throw new InvalidOperationException($"A reduction in the live selection must prevent another {tool}.");
}
if (CrewPlacement.Required(eMappers.MAPPER_TUNNEL) != 0 ||
    CrewPlacement.Required(eMappers.MAPPER_HOVEL) != 0 || !default(CrewBudget).CanReserve)
    throw new InvalidOperationException("Ordinary placements must retain the existing budget rules.");
Console.WriteLine("PASS: all seven crew mappers, live selection limits, and pending crew reservations.");

var firstGesture = new PlacementFeedback();
using (PlacementFeedback.Enter(firstGesture)) { } // Vanilla rejects the first click before emitting a cue.
using (PlacementFeedback.Enter(firstGesture))
{
    if (!PlacementFeedback.AllowSound() || !PlacementFeedback.AllowVoice())
        throw new InvalidOperationException("The first accepted siege click must sound and speak.");
    using (PlacementFeedback.Enter(null))
        if (!PlacementFeedback.AllowSound() || !PlacementFeedback.AllowVoice())
            throw new InvalidOperationException("An independent click may play both cues.");
}
for (int frame = 0; frame < 18; frame++)
    using (PlacementFeedback.Enter(firstGesture))
        if (PlacementFeedback.AllowSound() || PlacementFeedback.AllowVoice())
            throw new InvalidOperationException("Later clicks in the same gesture must be quiet.");
using (PlacementFeedback.Enter(new PlacementFeedback()))
    if (!PlacementFeedback.AllowSound() || PlacementFeedback.AllowSound() || !PlacementFeedback.AllowVoice())
        throw new InvalidOperationException("A new gesture must own its own placement and speech allowance.");
if (!PlacementFeedback.AllowSound() || !PlacementFeedback.AllowSound() ||
    !PlacementFeedback.AllowVoice() || !PlacementFeedback.AllowVoice())
    throw new InvalidOperationException("Ordinary clicks outside a gesture must remain unchanged.");
Console.WriteLine("PASS: rejected and delayed placement feedback, independent gestures and single clicks.");

var cadence = new StorageBuildCadence();
if (!cadence.TryBegin(0))
    throw new InvalidOperationException("The first check must be allowed at native tick zero.");
for (int frame = 0; frame < 240; frame++)
    if (cadence.TryBegin(0))
        throw new InvalidOperationException("Repeated frames cannot repeat a storage check in the same tick.");
if (!cadence.TryBegin(1) || !cadence.TryBegin(80) || cadence.TryBegin(80) || cadence.TryBegin(79))
    throw new InvalidOperationException("Storage checks must follow new ticks without catching up or accepting stale ticks.");
if (!cadence.TryBegin(null) || !cadence.TryBegin(null) || cadence.TryBegin(80) || !cadence.TryBegin(81))
    throw new InvalidOperationException("Frame cadence must remain available without a clock and resume tick cadence on recovery.");
var wrapped = new StorageBuildCadence();
if (!wrapped.TryBegin(int.MaxValue) || !wrapped.TryBegin(int.MinValue) || wrapped.TryBegin(int.MaxValue))
    throw new InvalidOperationException("Signed map tick wrap must preserve forward progress.");
Console.WriteLine("PASS: storage cadence, slow ticks, skipped ticks, clock fallback, and counter wrap.");

foreach (bool stockpile in new[] { false, true })
{
    int vanillaLimit = stockpile ? 32 : 8;
    int recordsPerSection = stockpile ? 4 : 1;
    foreach (int value in new[] { -2, -1, 0, 1, 8, 20, 21, 100, 101 })
    {
        bool custom = value >= 1 && value <= 100;
        if (StorageEmptyLimitPolicy.IsValid(value) != (custom || value == -1))
            throw new InvalidOperationException($"Unexpected acceptance of storage limit {value}.");
        int expectedThreshold = custom ? value * recordsPerSection : vanillaLimit;
        int threshold = StorageEmptyLimitPolicy.RecordLimit(value, stockpile);
        if (threshold != expectedThreshold)
            throw new InvalidOperationException($"Unexpected storage threshold for {value}, stockpile={stockpile}.");

        foreach (int emptyRecords in new[] { 0, expectedThreshold - 1, expectedThreshold, expectedThreshold + 1 })
        {
            int compared = StorageEmptyLimitPolicy.CountForVanillaComparison(emptyRecords, threshold, vanillaLimit);
            if ((compared >= vanillaLimit) != (emptyRecords >= expectedThreshold) ||
                threshold == vanillaLimit && compared != emptyRecords)
                throw new InvalidOperationException($"Native threshold changed incorrectly at {emptyRecords} records.");
        }
    }
}
Console.WriteLine("PASS: Vanilla/custom empty storage thresholds, stockpile record conversion and native branch encoding.");

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

if (QuarryLayout.Fill(result, anchor, new Tile(25, 25)))
    throw new InvalidOperationException("A short quarry gesture must use the ordinary single placement.");
Expect((20, 20));
foreach (int directionX in new[] { -1, 1 })
foreach (int directionY in new[] { -1, 1 })
{
    if (!QuarryLayout.Fill(result, anchor, new Tile(20 + directionX * 70, 20 + directionY * 70)) ||
        !QuarryLayout.IsBlock(result))
        throw new InvalidOperationException("A long drag must produce one anchored quarry block.");
    Expect((20, 20), (20 + directionX * 6, 20), (20, 20 + directionY * 6),
        (20 + directionX * 6, 20 + directionY * 6));
}

QuarryLayout.Fill(result, anchor, new Tile(26, 26));
var existingPiles = new[] { new Tile(22, 14) };
if (QuarryLayout.AllowsPile(new Tile(25, 20), result, existingPiles) ||
    QuarryLayout.AllowsPile(new Tile(20, 15), result, existingPiles) ||
    QuarryLayout.AllowsPile(new Tile(21, 14), result, existingPiles) ||
    QuarryLayout.AllowsPile(new Tile(799, 20), result, existingPiles) ||
    !QuarryLayout.AllowsPile(new Tile(20, 14), result, existingPiles))
    throw new InvalidOperationException("A pile must avoid the whole block, its walkway, map edges and other piles.");
QuarryLayout.Fill(result, new Tile(796, 780), new Tile(799, 790));
Expect((796, 780), (796, 786));
if (!QuarryLayout.IsBlock(result) || QuarryLayout.IsBlock(new[] { anchor, new Tile(20, 26) }))
    throw new InvalidOperationException("Only canonical blocks may reach simulation, including a clipped map edge.");
Console.WriteLine("PASS: one quarry block, fixed order, protected walkway and disjoint pile footprints.");

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
Storage(new Tile(22, 18), width: 1, height: 1);
Expect((20, 20), (21, 20), (22, 20),
    (20, 19), (21, 19), (22, 19),
    (20, 18), (21, 18), (22, 18));
Console.WriteLine("PASS: dense one-tile murder-hole rectangle.");

foreach (int farmScale in new[] { 9, 10 })
{
    Storage(new Tile(20 + farmScale - 1, 20 + farmScale - 1), farmScale, farmScale);
    Expect((20, 20));
    foreach (int dx in new[] { -farmScale, farmScale })
    foreach (int dy in new[] { -farmScale, farmScale })
    {
        Storage(new Tile(20 + dx * 2, 20 + dy * 2), farmScale, farmScale);
        Expect((20, 20), (20 + dx, 20), (20 + 2 * dx, 20),
            (20, 20 + dy), (20 + dx, 20 + dy), (20 + 2 * dx, 20 + dy),
            (20, 20 + 2 * dy), (20 + dx, 20 + 2 * dy), (20 + 2 * dx, 20 + 2 * dy));
    }
    Storage(new Tile(200, 20), farmScale, farmScale, 38);
    Expect((20, 20), (20 + farmScale, 20));
}
Console.WriteLine("PASS: gapless 9×9 and 10×10 farm footprints in every direction and at the map edge.");

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
