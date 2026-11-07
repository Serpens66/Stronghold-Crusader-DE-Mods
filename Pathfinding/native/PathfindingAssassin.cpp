// Native kernel of Pathfinding's weighted Assassin A* (AssassinPathfindingRuntime.TryBuildWeightedRoute,
// bucket queue variant). It reproduces the managed search exactly: the same edge predicates in the same
// order, the same Dial buckets (last-in first-out within a key, stale entries skipped), the same node
// limit and the same route preparation, so both return the same route, cost and expansion count. Rare
// managed decisions (policy-masked gate edges, direct gatehouse climb endpoints) are callbacks; the
// native special tile predicate (0x107160) is called directly. Single-threaded (simulation thread).
#include <cstdint>
#include <cstring>
#include <vector>
#include <new>

namespace {
constexpr int MapWidth = 800, CoordinateCount = MapWidth * MapWidth, TileCount = 320800;
constexpr int MaximumCommittedPathLength = 2000, BucketRing = 4096;
constexpr int32_t IntMax = 0x7FFFFFFF;
constexpr uint32_t AssassinFallbackBlockingMask = 0x4A5014B1u, NativeSpecialTileFlag = 1u << 12,
    IsWallFlag = 1u << 8, IsStairsFlag = 1u << 11, IsLowWallFlag = 1u << 16;
constexpr uint8_t GroundEdgeKind = 1, ClimbEdgeKind = 2;
constexpr int DirectionX[8] = { 0, 1, 1, 1, 0, -1, -1, -1 };
constexpr int DirectionY[8] = { -1, -1, 0, 1, 1, 1, 0, -1 };
}

extern "C" {
// Native game tables (or test fixtures) and the managed callbacks; all pointers stay valid while bound.
struct BfqAssassinTables {
    const uint8_t* validCoordinates;   // CoordinateCount bytes, y * 800 + x
    const int32_t* rowLookup;          // tile id of (x, y) = rowLookup[y * 3] + x
    const uint32_t* tileFlags;         // TileCount
    const uint8_t* occupancy;          // TileCount, ordinary connection directions
    const uint16_t* building;          // TileCount
    const uint8_t* height;             // TileCount
    const uint8_t* directionMasks;     // 8
    uint8_t (*specialTile)(void* context, int tile);
    void* specialContext;
    // AssassinPathAPI.IsDirectGatehouseClimbEndpoint (managed); called only where the building rule fails.
    int (*directGatehouseEndpoint)(int tile);
    // AllowsAssassinTransition for a direction the gate policy forbids (managed, identity checks).
    int (*maskedTransition)(int from, int to, int direction, int allowClimbing, int allowReserved);
    // IsDirectionAllowed of a policy without a direction mask (test fixtures); unused with policyMask.
    int (*directionAllowed)(int tile, int direction);
};
struct BfqAssassinRequest {
    int startX, startY, targetX, targetY, maximumNodes, cardinalTicks, diagonalTicks, allowClimbing, allowReserved;
    const uint8_t* policyMask;         // allowed directions per tile; null: see usePolicyCallback
    int policyMaskLength;
    int usePolicyCallback;             // 1: directionAllowed decides (policy without a mask)
};
// status: 0 no route (exhausted or node limit), 1 route, 2 bucket overflow (caller runs the heap search),
// 3 route longer than MaximumCommittedPathLength or not ending at the start (caller reports no route).
struct BfqAssassinResult { int status, expanded, totalCost, routeLength, groundEdges, climbEdges; };
}

namespace {
struct Scratch {
    std::vector<int32_t> costs, estimated, parents;
    std::vector<uint32_t> gen;            // node touched in the current search (else cost IntMax)
    std::vector<uint32_t> done;           // node expanded in the current search
    std::vector<uint8_t> kinds;
    std::vector<std::vector<int>> buckets;
    // directGatehouseEndpoint answers per tile and search: the building state does not change during a
    // search, so each tile costs at most one managed transition (its walls are building tiles).
    std::vector<uint32_t> gateGen;
    std::vector<uint8_t> gateValue;
    uint32_t generation = 0;
    Scratch() : costs(CoordinateCount), estimated(CoordinateCount), parents(CoordinateCount), gen(CoordinateCount, 0),
        done(CoordinateCount, 0), kinds(CoordinateCount), buckets(BucketRing), gateGen(TileCount, 0), gateValue(TileCount, 0) {}
};
Scratch* scratch;

inline int32_t SaturatingAdd(int32_t left, int32_t right) {
    if (left == IntMax || right == IntMax || left > IntMax - right) return IntMax;
    return left + right;
}
inline int32_t EstimateOctile(int x, int y, int tx, int ty, int32_t cardinal, int32_t diagonal) {
    int dx = tx - x < 0 ? x - tx : tx - x, dy = ty - y < 0 ? y - ty : ty - y;
    int diagonalSteps = dx < dy ? dx : dy, straightSteps = (dx > dy ? dx : dy) - diagonalSteps;
    int32_t twoCardinal = SaturatingAdd(cardinal, cardinal);
    int32_t effective = diagonal < twoCardinal ? diagonal : twoCardinal;
    int64_t estimate = int64_t(diagonalSteps) * effective + int64_t(straightSteps) * cardinal;
    return estimate >= IntMax ? IntMax : int32_t(estimate);
}

struct Search {
    const BfqAssassinTables& t; const BfqAssassinRequest& r; Scratch& s;
    uint32_t g; int bucketCount = 0; int32_t bucketCurrent = 0; bool overflow = false;
    int32_t Cost(int node) const { return s.gen[node] == g ? s.costs[node] : IntMax; }
    bool ValidCoordinate(int x, int y) const { return unsigned(x) < unsigned(MapWidth) && unsigned(y) < unsigned(MapWidth) && t.validCoordinates[y * MapWidth + x] != 0; }
    int TileId(int x, int y) const { return t.rowLookup[y * 3] + x; }
    static bool NativeTile(int tile) { return unsigned(tile) < unsigned(TileCount); }
    void Push(int node) {
        int32_t key = s.estimated[node];
        if (key < bucketCurrent || key - bucketCurrent >= BucketRing) { overflow = true; return; }
        s.buckets[key & (BucketRing - 1)].push_back(node); ++bucketCount;
    }
    int Pop() {
        while (bucketCount > 0) {
            std::vector<int>& list = s.buckets[bucketCurrent & (BucketRing - 1)];
            while (!list.empty()) {
                int node = list.back(); list.pop_back(); --bucketCount;
                if (s.done[node] == g || s.estimated[node] != bucketCurrent) continue;
                s.done[node] = g;
                return node;
            }
            ++bucketCurrent;
        }
        return -1;
    }
    void Clear() { if (bucketCount != 0) for (auto& list : s.buckets) list.clear(); bucketCount = 0; }
    bool GatehouseEndpoint(int tile) const {
        if (!t.directGatehouseEndpoint) return false;
        if (s.gateGen[tile] != g) { s.gateGen[tile] = g; s.gateValue[tile] = t.directGatehouseEndpoint(tile) != 0 ? 1 : 0; }
        return s.gateValue[tile] != 0;
    }
    // AssassinPathfindingRuntime.IsVanillaAssassinFallback (all four parts evaluated, as there).
    bool Fallback(int current, int target, uint32_t currentFlags) const {
        uint32_t targetFlags = t.tileFlags[target];
        bool targetAccepted = (targetFlags & AssassinFallbackBlockingMask) == 0;
        // Without a native predicate (test fixtures never flag special tiles) the tile is not accepted.
        if (!targetAccepted && (targetFlags & NativeSpecialTileFlag) != 0)
            targetAccepted = t.specialTile != nullptr && t.specialTile(t.specialContext, target) != 0;
        bool startAccepted = t.building[current] == 0 || (r.allowReserved && t.occupancy[current] != 0) || GatehouseEndpoint(current);
        bool targetBuildingAccepted = t.building[target] == 0 || (r.allowReserved && t.occupancy[target] != 0) || GatehouseEndpoint(target);
        bool hasWall = ((currentFlags | targetFlags) & IsWallFlag) != 0;
        return targetAccepted && startAccepted && targetBuildingAccepted && hasWall;
    }
    // AssassinClimbCostPolicy.GetAdditionalTicks for a climb edge.
    int32_t ClimbTicks(int current, int target) const {
        int difference = int(t.height[target]) - int(t.height[current]);
        uint32_t flags = t.tileFlags[target];
        if (difference == 0) return 0;
        if (difference < 0) return 80;
        if ((flags & IsLowWallFlag) != 0) return 240;
        if ((flags & IsWallFlag) != 0 && (flags & IsStairsFlag) == 0) return 400;
        int scaled = (difference * 400 + 45) / 90;
        return scaled < 80 ? 80 : scaled;
    }
    bool Allowed(int from, int to, int direction) const {
        if (r.usePolicyCallback) { if (t.directionAllowed(from, direction) != 0) return true; }
        else if (r.policyMask == nullptr || from >= r.policyMaskLength || (r.policyMask[from] & (1 << direction)) != 0) return true;
        return t.maskedTransition != nullptr && t.maskedTransition(from, to, direction, r.allowClimbing, r.allowReserved) != 0;
    }
    void Touch(int node, int32_t cost, int parent, uint8_t kind, int32_t estimated) {
        s.gen[node] = g; s.costs[node] = cost; s.parents[node] = parent; s.kinds[node] = kind; s.estimated[node] = estimated;
    }
    // Edge u -> v in direction d with exactly Run's predicates in Run's order (ordinary connection, cardinal
    // climb fallback, climbing switch, gate policy) and Run's edge cost; v is valid and native (caller).
    bool Edge(int uTile, uint32_t uFlags, int d, int vTile, int32_t& cost, bool& climb) const {
        const bool cardinalStep = (d & 1) == 0;
        const int32_t movement = cardinalStep ? r.cardinalTicks : r.diagonalTicks;
        const bool ordinary = (t.occupancy[uTile] & t.directionMasks[d]) != 0 ||
            (t.occupancy[vTile] & t.directionMasks[d ^ 4]) != 0;
        climb = false;
        if (!ordinary) {
            if (!cardinalStep) return false;
            if (!Fallback(uTile, vTile, uFlags)) return false;
            climb = true;
            if (!r.allowClimbing) return false;
        }
        if (!Allowed(uTile, vTile, d)) return false;
        const int32_t climbTicks = climb ? ClimbTicks(uTile, vTile) : 0;
        cost = movement > IntMax - climbTicks ? IntMax : movement + climbTicks;
        return true;
    }

    int Run(int* routeNodes, int32_t* routeCosts, int routeCapacity, BfqAssassinResult& result) {
        const int sx = r.startX, sy = r.startY, tx = r.targetX, ty = r.targetY;
        const int32_t cardinal = r.cardinalTicks, diagonal = r.diagonalTicks;
        const int startNode = sy * MapWidth + sx, targetNode = ty * MapWidth + tx;
        int nodeLimit = r.maximumNodes < TileCount ? r.maximumNodes : TileCount;
        if (nodeLimit < 1) nodeLimit = 1;
        Touch(startNode, 0, -1, 0, EstimateOctile(sx, sy, tx, ty, cardinal, diagonal));
        bucketCurrent = s.estimated[startNode]; Push(startNode);
        int expanded = 0;
        while (bucketCount > 0 && expanded < nodeLimit) {
            int current = Pop();
            if (current < 0) break;
            ++expanded;
            if (current == targetNode) {
                Clear();
                result.expanded = expanded; result.totalCost = s.costs[targetNode];
                int length = 0, ground = 0, climb = 0, node = targetNode;
                while (node >= 0 && length < routeCapacity) {
                    routeNodes[length] = node; routeCosts[length] = s.costs[node]; ++length;
                    if (s.kinds[node] == GroundEdgeKind) ++ground; else if (s.kinds[node] == ClimbEdgeKind) ++climb;
                    if (node == startNode) break;
                    node = s.parents[node];
                }
                result.routeLength = length; result.groundEdges = ground; result.climbEdges = climb;
                return (length == 0 || length > MaximumCommittedPathLength || routeNodes[length - 1] != startNode) ? 3 : 1;
            }
            const int cx = current % MapWidth, cy = current / MapWidth, currentTile = TileId(cx, cy);
            if (!NativeTile(currentTile)) continue;
            const uint32_t currentFlags = t.tileFlags[currentTile];
            const int32_t currentCost = s.costs[current];
            for (int direction = 0; direction < 8; ++direction) {
                const int nx = cx + DirectionX[direction], ny = cy + DirectionY[direction];
                if (!ValidCoordinate(nx, ny)) continue;
                const int nextTile = TileId(nx, ny);
                if (!NativeTile(nextTile)) continue;
                const int nextNode = ny * MapWidth + nx;
                const bool cardinalStep = (direction & 1) == 0;
                const int32_t movement = cardinalStep ? cardinal : diagonal;
                const int32_t nextCost = Cost(nextNode);
                if (int64_t(currentCost) + movement >= nextCost) continue;
                const bool ordinary = (t.occupancy[currentTile] & t.directionMasks[direction]) != 0 ||
                    (t.occupancy[nextTile] & t.directionMasks[direction ^ 4]) != 0;
                bool climbEdge = false;
                if (!ordinary) {
                    if (!cardinalStep) continue;
                    if (!Fallback(currentTile, nextTile, currentFlags)) continue;
                    climbEdge = true;
                    if (!r.allowClimbing) continue;
                }
                if (!Allowed(currentTile, nextTile, direction)) continue;
                const int32_t climbTicks = climbEdge ? ClimbTicks(currentTile, nextTile) : 0;
                const int32_t edgeCost = movement > IntMax - climbTicks ? IntMax : movement + climbTicks;
                const int32_t newCost = currentCost > IntMax - edgeCost ? IntMax : currentCost + edgeCost;
                if (newCost >= nextCost) continue;
                const int32_t estimate = SaturatingAdd(newCost, EstimateOctile(nx, ny, tx, ty, cardinal, diagonal));
                Touch(nextNode, newCost, current, climbEdge ? ClimbEdgeKind : GroundEdgeKind, estimate);
                Push(nextNode);
            }
            if (overflow) break;
        }
        Clear();
        result.expanded = expanded;
        return overflow ? 2 : 0;
    }
};

// Next search generation of the scratch (stamps restart at 1 after a wrap).
uint32_t NextGeneration(Scratch& s) {
    if (++s.generation == 0) {
        std::fill(s.gen.begin(), s.gen.end(), 0u); std::fill(s.done.begin(), s.done.end(), 0u);
        std::fill(s.gateGen.begin(), s.gateGen.end(), 0u); s.generation = 1;
    }
    return s.generation;
}

// Group field (shared work for the members of one Assassin group move): one backward Dijkstra from the move
// target (anchor) over exactly Search's edges and costs, resumed only as far as members need. dist[v] is v's exact
// cost to the anchor; DescentRoute follows it instead of a per-unit search, which expands ~41,000 tiles per member
// (test_stresstest5, 03.11.2026).
constexpr int MaxGroupFields = 4;
struct GroupField {
    int handle = 0, anchor = -1, current = 0, pending = 0; uint32_t generation = 0; uint64_t used = 0;
    bool failed = false;
    std::vector<uint32_t> gen; std::vector<int32_t> dist; std::vector<uint8_t> done; std::vector<std::vector<int>> ring;
    GroupField() : gen(CoordinateCount, 0u), dist(CoordinateCount, 0), done(CoordinateCount, 0), ring(BucketRing) {}
    bool Touched(int v) const { return gen[v] == generation; }
    bool Settled(int v) const { return gen[v] == generation && done[v] != 0; }
};
GroupField* groupFields[MaxGroupFields];
int nextGroupHandle = 1; uint64_t groupClock = 0;

// Settles nodes in nondecreasing distance until `node` is settled, nothing is queued, or an edge cost does not fit
// the ring. Every edge costs at least the cardinal movement (> 0), so a settled distance is exact, every node with a
// smaller distance is settled before it.
void ResumeField(const Search& se, GroupField& f, int node) {
    while (f.pending > 0 && !f.Settled(node)) {
        std::vector<int>& list = f.ring[f.current & (BucketRing - 1)];
        if (list.empty()) { ++f.current; continue; }
        const int v = list.back(); list.pop_back(); --f.pending;
        if (f.done[v] != 0 || f.dist[v] != f.current) continue;
        f.done[v] = 1;
        const int vx = v % MapWidth, vy = v / MapWidth, vTile = se.TileId(vx, vy);
        if (!Search::NativeTile(vTile)) continue;
        for (int d = 0; d < 8; ++d) {
            const int ux = vx - DirectionX[d], uy = vy - DirectionY[d];
            if (!se.ValidCoordinate(ux, uy)) continue;
            const int u = uy * MapWidth + ux;
            if (f.Settled(u)) continue;
            const int uTile = se.TileId(ux, uy);
            if (!Search::NativeTile(uTile)) continue;
            int32_t cost; bool climb;
            if (!se.Edge(uTile, se.t.tileFlags[uTile], d, vTile, cost, climb)) continue;
            const int64_t next = int64_t(f.dist[v]) + cost;
            if (f.Touched(u) && next >= f.dist[u]) continue;
            if (next - f.current >= BucketRing) { f.failed = true; return; }
            if (!f.Touched(u)) { f.gen[u] = f.generation; f.done[u] = 0; }
            f.dist[u] = int32_t(next); f.ring[int(next) & (BucketRing - 1)].push_back(u); ++f.pending;
        }
    }
}

std::vector<int> descentNodes; std::vector<int32_t> descentCosts;

// As Pathfinding's anchor routes: the exact descent along the field towards the anchor until the route enters the box
// around anchor and slot (margin), then Run's A* from that entry to the slot. Near-optimal, not cost-optimal (user
// decision 03.11.2026). Status as Run (1 route, 0 none, 2 bucket overflow, 3 rejected route), or 4 when the field
// failed (edge cost beyond the ring) or the start does not reach the anchor (the caller keeps the per-unit search).
int DescentRoute(Search& se, GroupField& f, int start, int slot, int margin, int* routeNodes, int32_t* routeCosts, int capacity, BfqAssassinResult& result) {
    ResumeField(se, f, start);
    if (f.failed || !f.Settled(start)) return 4;
    const int ax = f.anchor % MapWidth, ay = f.anchor / MapWidth, tx = slot % MapWidth, ty = slot / MapWidth;
    const int x0 = (ax < tx ? ax : tx) - margin, x1 = (ax > tx ? ax : tx) + margin;
    const int y0 = (ay < ty ? ay : ty) - margin, y1 = (ay > ty ? ay : ty) + margin;
    descentNodes.clear(); descentCosts.clear();
    int node = start, ground = 0, climbs = 0; int32_t g = 0;
    descentNodes.push_back(node); descentCosts.push_back(0);
    for (;;) {
        const int x = node % MapWidth, y = node / MapWidth;
        if (node == f.anchor || (x >= x0 && x <= x1 && y >= y0 && y <= y1)) break;
        const int uTile = se.TileId(x, y);
        const uint32_t uFlags = se.t.tileFlags[uTile];
        int next = -1; int32_t step = 0; bool stepClimb = false;
        // Lowest direction first: deterministic, every peer descends the same way.
        for (int d = 0; d < 8 && next < 0; ++d) {
            const int nx = x + DirectionX[d], ny = y + DirectionY[d];
            if (!se.ValidCoordinate(nx, ny)) continue;
            const int v = ny * MapWidth + nx;
            if (!f.Settled(v)) continue;
            const int vTile = se.TileId(nx, ny);
            if (!Search::NativeTile(vTile)) continue;
            int32_t cost; bool climb;
            if (!se.Edge(uTile, uFlags, d, vTile, cost, climb)) continue;
            if (int64_t(f.dist[v]) + cost != f.dist[node]) continue;
            next = v; step = cost; stepClimb = climb;
        }
        if (next < 0 || int(descentNodes.size()) >= MaximumCommittedPathLength) return 3;
        g = SaturatingAdd(g, step); node = next;
        descentNodes.push_back(node); descentCosts.push_back(g);
        if (stepClimb) ++climbs; else ++ground;
    }
    BfqAssassinRequest local = se.r;
    local.startX = node % MapWidth; local.startY = node / MapWidth;
    // se's generation: the descent touched no node, only the per-search gatehouse answers, which stay valid.
    Search finish{ se.t, local, se.s, se.g };
    const int status = finish.Run(routeNodes, routeCosts, capacity, result);
    if (status != 1) return status;
    int length = result.routeLength;
    for (int i = 0; i < length; ++i) routeCosts[i] = SaturatingAdd(routeCosts[i], g);
    // The entry is the A* route's last node; the descent before it follows towards the start.
    for (int i = int(descentNodes.size()) - 2; i >= 0; --i) {
        if (length >= capacity || length >= MaximumCommittedPathLength) return 3;
        routeNodes[length] = descentNodes[size_t(i)]; routeCosts[length] = descentCosts[size_t(i)]; ++length;
    }
    result.routeLength = length; result.totalCost = routeCosts[0];
    result.groundEdges += ground; result.climbEdges += climbs;
    return 1;
}

GroupField* FindGroupField(int handle) {
    for (GroupField* f : groupFields) if (f && f->handle == handle) return f;
    return nullptr;
}

bool ValidRequest(const BfqAssassinTables* tables, const BfqAssassinRequest* request) {
    if (!tables || !request || !tables->validCoordinates || !tables->rowLookup || !tables->tileFlags || !tables->occupancy ||
        !tables->building || !tables->height || !tables->directionMasks) return false;
    const BfqAssassinRequest& r = *request;
    if (r.usePolicyCallback && (!tables->directionAllowed || !tables->maskedTransition)) return false;
    if (r.policyMask && !tables->maskedTransition) return false;
    return unsigned(r.startX) < unsigned(MapWidth) && unsigned(r.startY) < unsigned(MapWidth) &&
        unsigned(r.targetX) < unsigned(MapWidth) && unsigned(r.targetY) < unsigned(MapWidth);
}
}

extern "C" {
__declspec(dllexport) int bfq_assassin_contract() { return 0x01000002; }

// Starts a group field towards the request's target (start fields are ignored) in a free or the least recently
// used slot and returns its handle (> 0). Negative on invalid arguments, allocation failure, or movement costs
// that cannot fit the ring with the largest climb cost (the caller keeps per-unit searches).
__declspec(dllexport) int bfq_assassin_field_create(const BfqAssassinTables* tables, const BfqAssassinRequest* request, int* handle) {
    if (!handle || !ValidRequest(tables, request)) return -1;
    // ClimbTicks is at most (255 * 400 + 45) / 90 = 1133.
    if (request->cardinalTicks <= 0 || request->diagonalTicks <= 0 || request->diagonalTicks > BucketRing - 1200) return -1;
    try {
        GroupField* f = nullptr;
        for (GroupField*& slot : groupFields) if (!slot) { slot = new GroupField(); f = slot; break; }
        if (!f) { f = groupFields[0]; for (GroupField* candidate : groupFields) if (candidate->used < f->used) f = candidate; }
        for (auto& list : f->ring) list.clear();
        if (++f->generation == 0) { std::fill(f->gen.begin(), f->gen.end(), 0u); f->generation = 1; }
        f->handle = nextGroupHandle++; if (nextGroupHandle <= 0) nextGroupHandle = 1;
        f->used = ++groupClock; f->failed = false; f->current = 0; f->pending = 0;
        f->anchor = request->targetY * MapWidth + request->targetX;
        f->gen[f->anchor] = f->generation; f->done[f->anchor] = 0; f->dist[f->anchor] = 0;
        f->ring[0].push_back(f->anchor); f->pending = 1;
        *handle = f->handle;
        return 0;
    }
    catch (const std::bad_alloc&) { return -2; }
}

// Route of one member from the request's start to its slot (the request's target) along the field `handle`.
// The request must carry the field's movement costs, switches and gate policy.
// margin (>= 0): DescentRoute's box margin. Returns 0 with result filled (DescentRoute's status; result.expanded
// counts Run's expansions), -4 when the handle was recycled.
__declspec(dllexport) int bfq_assassin_field_route(int handle, const BfqAssassinTables* tables, const BfqAssassinRequest* request,
    int margin, int* routeNodes, int32_t* routeCosts, int routeCapacity, BfqAssassinResult* result) {
    if (margin < 0 || !routeNodes || !routeCosts || routeCapacity < 1 || !result || !ValidRequest(tables, request)) return -1;
    GroupField* f = FindGroupField(handle);
    if (!f) return -4;
    std::memset(result, 0, sizeof(*result));
    try {
        if (!scratch) scratch = new Scratch();
        Scratch& s = *scratch;
        f->used = ++groupClock;
        Search se{ *tables, *request, s, NextGeneration(s) };
        const int start = request->startY * MapWidth + request->startX, slot = request->targetY * MapWidth + request->targetX;
        result->status = DescentRoute(se, *f, start, slot, margin, routeNodes, routeCosts, routeCapacity, *result);
        return 0;
    }
    catch (const std::bad_alloc&) { return -2; }
}

// Returns 0 with result filled; negative on invalid arguments or allocation failure (caller keeps
// the managed search). Coordinates must be valid and start/target native tiles (the managed caller
// checks them first, as before its own search).
__declspec(dllexport) int bfq_assassin_search(const BfqAssassinTables* tables, const BfqAssassinRequest* request,
    int* routeNodes, int32_t* routeCosts, int routeCapacity, BfqAssassinResult* result) {
    if (!tables || !request || !routeNodes || !routeCosts || routeCapacity < 1 || !result ||
        !tables->validCoordinates || !tables->rowLookup || !tables->tileFlags || !tables->occupancy || !tables->building ||
        !tables->height || !tables->directionMasks) return -1;
    const BfqAssassinRequest& r = *request;
    if (r.usePolicyCallback && (!tables->directionAllowed || !tables->maskedTransition)) return -1;
    if (r.policyMask && !tables->maskedTransition) return -1;
    if (unsigned(r.startX) >= unsigned(MapWidth) || unsigned(r.startY) >= unsigned(MapWidth) ||
        unsigned(r.targetX) >= unsigned(MapWidth) || unsigned(r.targetY) >= unsigned(MapWidth)) return -1;
    std::memset(result, 0, sizeof(*result));
    try {
        if (!scratch) scratch = new Scratch();
        Scratch& s = *scratch;
        if (++s.generation == 0) {
            std::fill(s.gen.begin(), s.gen.end(), 0u); std::fill(s.done.begin(), s.done.end(), 0u);
            std::fill(s.gateGen.begin(), s.gateGen.end(), 0u); s.generation = 1;
        }
        Search search{ *tables, r, s, s.generation };
        result->status = search.Run(routeNodes, routeCosts, routeCapacity, *result);
        return 0;
    }
    catch (const std::bad_alloc&) { return -2; }
}
}
