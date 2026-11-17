// Exercises the production native core directly, including its C ABI. No game code is loaded.
#include "../../native/PathfindingAnchor.cpp"
#include <iostream>
#include <array>
#include <filesystem>
#include <fstream>
#include <queue>
#include <random>
#include <stdexcept>

static int assertions = 0;
static void Check(bool ok, const char* reason) { ++assertions; if (!ok) throw std::runtime_error(reason); }

struct Fixture {
    Context* context = nullptr; int width, height; std::vector<u32> words; std::vector<uint8_t> surfaces;
    Fixture(int w, int h, i64 limit = 128LL << 20) : width(w), height(h), words(w * h), surfaces(w * h, 0x83) {
        Check(mma_create(w, h, limit, &context) == Ok, "create");
    }
    ~Fixture() { mma_destroy(context); }
    int node(int x, int y) const { return y * width + x; }
    void edge(int x, int y, int d, bool wet = false) {
        words[node(x, y)] |= (1u << d) | (wet ? (1u << (16 + d)) : (1u << (8 + d)));
    }
    void link(int x, int y, int d) { edge(x, y, d); edge(x + dx[d], y + dy[d], (d + 4) & 7); }
    void publish(bool withSurfaces = true) {
        Check(mma_mask_begin(context, 1) == Ok, "mask begin");
        int changed = 0;
        Check(mma_mask_write(context, 1, 0, int(words.size()), words.data(), &changed) == Ok, "mask write");
        if (withSurfaces) Check(mma_surface_write(context, 0, int(surfaces.size()), surfaces.data(), &changed) == Ok, "surface write");
    }
    void surface(int node, uint8_t value) {
        int changed = -1; Check(mma_surface_update(context, &node, &value, 1, &changed) == Ok, "surface update"); surfaces[node] = value;
    }
    QueryResult single(int start, int target, int kind, std::vector<uint8_t>& path, int moat = 0) {
        path.assign(MaxSteps, 0xFF); QueryResult result{};
        Check(mma_single_route_limited(context, 1, target, 1, moat, kind, start, 0, path.data(), int(path.size()), &result) == Ok, "single ABI");
        path.resize(result.length); return result;
    }
    int task(int anchor, int kind, const std::vector<int>& starts, int moat = 0) {
        int id = 0; Check(mma_task_create(context, 1, anchor, 1, moat, kind, starts.data(), int(starts.size()), &id) == Ok && id > 0, "task ABI"); return id;
    }
    void step(int workers) { int done = 0; Check(mma_step(context, 64, workers, &done) == Ok, "step"); }
    QueryResult query(int id, int start, int target, std::vector<uint8_t>& path) {
        path.assign(MaxSteps, 0xFF); QueryResult result{};
        Check(mma_query(context, id, start, target, 8, path.data(), int(path.size()), &result) == Ok, "query"); path.resize(result.length); return result;
    }
    bool allowed(int node, int kind) const {
        return kind == 0 || ((surfaces[node] & 0x80) && (surfaces[node] & (1 << (kind - 1))));
    }
    int distance(int start, int target, int kind, int moat = 0) const {
        std::vector<int> distances(words.size(), -1); std::queue<int> queue; distances[start] = 0; queue.push(start);
        while (!queue.empty()) {
            int v = queue.front(); queue.pop(); if (v == target) return distances[v];
            for (int d = 0; d < 8; ++d) {
                int x = v % width + dx[d], y = v / width + dy[d];
                if (x < 0 || x >= width || y < 0 || y >= height) continue;
                bool permitted = moat ? (words[v] & (1u << d)) != 0 : (words[v] & (1u << (8 + d))) && !(words[v] & (1u << (16 + d)));
                int u = node(x, y); if (!permitted || !allowed(u, kind) || distances[u] >= 0) continue;
                distances[u] = distances[v] + 1; queue.push(u);
            }
        }
        return -1;
    }
    void verify(int start, int target, int kind, const QueryResult& result, const std::vector<uint8_t>& path, bool exact, int moat = 0) const {
        const int expected = distance(start, target, kind, moat);
        Check((result.status == Found) == (expected >= 0), "reachability disagrees with independent BFS");
        if (result.status != Found) { Check(result.status == Unreachable, "unexpected status"); return; }
        if (exact) Check(result.length == expected && result.cost == expected, "single route is not shortest");
        int v = start;
        for (uint8_t d : path) {
            Check(d < 8, "invalid direction");
            Check((words[v] & (1u << ((moat ? 0 : 8) + d))) != 0, "route ignores directed edge");
            v += dy[d] * width + dx[d]; Check(v >= 0 && v < int(words.size()) && allowed(v, kind), "route enters forbidden surface");
        }
        Check(v == target, "route endpoint");
    }
};

static void CorridorTests() {
    Fixture f(12, 10);
    for (int x = 2; x < 6; ++x) { f.link(x, 4, 2); f.link(x, 6, 2); }
    for (int y = 4; y < 6; ++y) { f.link(2, y, 4); f.link(6, y, 4); }
    const int start = f.node(2, 4), target = f.node(6, 4), wall = f.node(4, 4);
    f.surfaces[wall] = 0x80; f.publish(false);
    std::vector<uint8_t> path;
    auto r = f.single(start, target, 2, path); Check(r.status == Unreachable, "unknown surfaces must fail closed");
    f.publish();
    for (int kind = 0; kind <= 2; ++kind) {
        r = f.single(start, target, kind, path); f.verify(start, target, kind, r, path, true);
        Check(r.length == (kind == 0 ? 4 : 8), "wall shortcut versus detour");
    }
    int ids[3];
    for (int kind = 0; kind <= 2; ++kind) ids[kind] = f.task(target, kind, { start });
    f.step(4);
    for (int kind = 0; kind <= 2; ++kind) { r = f.query(ids[kind], start, target, path); f.verify(start, target, kind, r, path, false); Check(r.length == (kind == 0 ? 4 : 8), "group surface class"); }
    f.surface(wall, 0x81);
    Check(mma_task_status(f.context, ids[0]) == Ready, "Any unaffected by surfaces");
    Check(mma_task_status(f.context, ids[1]) == Stale, "Large invalidates when entry changes");
    Check(mma_task_status(f.context, ids[2]) == Ready, "Restricted unaffected by Large-only change");
    Check(mma_task_restart(f.context, ids[1]) == Ok, "restart"); f.step(1);
    r = f.query(ids[1], start, target, path); Check(r.length == 4, "Large-only permission");
    r = f.query(ids[2], start, target, path); Check(r.length == 8, "Large permission must not leak to Restricted");
    f.surface(start, 0x80);
    for (int kind = 1; kind <= 2; ++kind) { r = f.single(start, target, kind, path); f.verify(start, target, kind, r, path, true); }
    r = f.single(target, start, 2, path); Check(r.status == Unreachable, "forbidden endpoint");
    // The same destination filter applies to the friendly/moat lane, not just ground.
    f.surface(wall, 0x80);
    for (int kind = 0; kind <= 2; ++kind) {
        r = f.single(start, target, kind, path, 1); f.verify(start, target, kind, r, path, true, 1);
        Check(r.length == (kind == 0 ? 4 : 8), "friendly-lane surface class");
    }
    f.surface(wall, 0x83);
    Check(mma_task_status(f.context, ids[2]) == Stale, "Restricted invalidates when entry opens");
    r = f.single(start, target, 2, path); Check(r.length == 4, "new permission is live");
    int nodes[2] = { wall, int(f.words.size()) }; uint8_t values[2] = {0x80, 0x80}; int changed = -1;
    Check(mma_surface_update(f.context, nodes, values, 2, &changed) == BadArgument, "invalid update");
    r = f.single(start, target, 2, path); Check(r.length == 4, "invalid update is atomic");
    values[0] = 0x84; Check(mma_surface_write(f.context, wall, 1, values, &changed) == BadArgument, "reserved surface bits");
    QueryResult invalid{}; uint8_t buffer[8];
    Check(mma_single_route(f.context, 1, target, 1, 0, 3, start, buffer, 8, &invalid) == BadArgument, "invalid single class");
    int id = 0; Check(mma_task_create(f.context, 1, target, 1, 0, -1, &start, 1, &id) == BadArgument, "invalid task class");
    // Same-class fields may reuse; another class must never satisfy this request.
    int repeat = f.task(target, 0, { start }); f.step(1);
    r = f.query(repeat, start, target, path); Check(r.length == 4, "cached unrestricted route");
    i64 stats[StatCount] = {}; Check(mma_stats(f.context, stats, StatCount, 0) == Ok && stats[FieldReuses] >= 1, "same-class field reuse");
}

static void PopulateSubanchorFixture(Fixture& f) {
    for (int x = 45; x < 58; ++x) { f.link(x, 45, 2); f.link(x, 47, 2); }
    for (int y = 45; y < 47; ++y) { f.link(45, y, 4); f.link(58, y, 4); }
    f.surfaces[f.node(50, 45)] = 0x80; f.publish();
    Check(mma_set_sub_single_budget(f.context, 0) == Ok, "disable sub-single to exercise real subanchors");
}

static void AppendResult(std::vector<i64>& snapshot, const QueryResult& r) {
    snapshot.insert(snapshot.end(), {r.status, r.ground, r.moat, r.length, r.flags, r.reserved, r.cost});
}

static void OpenGround(Fixture& f) {
    std::fill(f.words.begin(), f.words.end(), 0xFFFFu);
}

static void BlockGroundTile(Fixture& f, int x, int y) {
    f.words[f.node(x,y)] = 0;
    for (int d = 0; d < 8; ++d) {
        const int px = x - dx[d], py = y - dy[d];
        if (px < 0 || px >= f.width || py < 0 || py >= f.height) continue;
        f.words[f.node(px,py)] &= ~((1u << d) | (1u << (8+d)) | (1u << (16+d)));
    }
}

static int DirectionChanges(const std::vector<uint8_t>& path) {
    int changes = 0;
    for (size_t i = 1; i < path.size(); ++i) if (path[i] != path[i-1]) ++changes;
    return changes;
}

struct FrozenNaturalMap {
    int originX = 0, originY = 0, width = 0, height = 0;
    std::vector<uint8_t> edges;
    static FrozenNaturalMap read() {
        const auto file = std::filesystem::path(__FILE__).parent_path()/"NaturalRouteFixture.bin";
        std::ifstream input(file,std::ios::binary);
        Check(input.good(), "frozen natural-route fixture is missing");
        std::array<uint8_t,32> header{};
        input.read(reinterpret_cast<char*>(header.data()),std::streamsize(header.size()));
        Check(input.gcount() == std::streamsize(header.size()), "frozen natural-route header is truncated");
        const std::array<uint8_t,8> magic{'P','F','N','A','T','1',0,0};
        Check(std::equal(magic.begin(),magic.end(),header.begin()), "frozen natural-route magic differs");
        auto word = [&](int offset) {
            return u32(header[offset]) | (u32(header[offset+1])<<8) |
                (u32(header[offset+2])<<16) | (u32(header[offset+3])<<24);
        };
        Check(word(8) == 1, "unsupported frozen natural-route format");
        const u32 width = word(20), height = word(24), count = word(28);
        Check(width > 0 && height > 0 && u64(width)*height <= 640000 && count == u64(width)*height,
            "frozen natural-route dimensions do not match its payload");
        FrozenNaturalMap result;
        result.originX = int32_t(word(12)); result.originY = int32_t(word(16));
        result.width = int(width); result.height = int(height); result.edges.resize(count);
        input.read(reinterpret_cast<char*>(result.edges.data()),std::streamsize(count));
        Check(input.gcount() == std::streamsize(count), "frozen natural-route edge data is truncated");
        char trailing = 0;
        Check(!input.read(&trailing,1) && input.eof(), "frozen natural-route fixture has trailing data or an I/O error");
        return result;
    }
    int node(int worldX, int worldY) const {
        Check(worldX >= originX && worldX < originX+width && worldY >= originY && worldY < originY+height,
            "captured natural-route endpoint lies outside the frozen map");
        return (worldY-originY)*width+(worldX-originX);
    }
    void publish(Fixture& f) const {
        Check(f.width == width && f.height == height, "natural fixture destination has different dimensions");
        for (size_t i = 0; i < edges.size(); ++i) f.words[i] = u32(edges[i]) | (u32(edges[i])<<8);
        f.publish();
    }
};

struct NaturalQuality {
    int steps = 0, diagonal = 0, turns = 0, end = -1;
    i64 geometric = 0, maxCross = 0, sumCross = 0;
};

// A separate mathematical oracle: every vertex, including both endpoints, is
// measured against the fixed reference chord. Dividing by its common length is
// unnecessary when comparing the original and candidate for that same chord.
static NaturalQuality MeasureNaturalQuality(const Fixture& f, int start,
    const std::vector<uint8_t>& path, int referenceStart, int referenceEnd) {
    const int sx = referenceStart%f.width, sy = referenceStart/f.width;
    const int vx = referenceEnd%f.width-sx, vy = referenceEnd/f.width-sy;
    NaturalQuality result; result.steps = int(path.size()); result.turns = DirectionChanges(path);
    int node = start;
    auto point = [&] {
        const i64 cross = std::abs(i64(node%f.width-sx)*vy-i64(node/f.width-sy)*vx);
        result.maxCross = std::max(result.maxCross,cross); result.sumCross += cross;
    };
    point();
    for (uint8_t d : path) {
        Check(d < 8, "natural route contains an invalid direction");
        const int x = node%f.width+dx[d], y = node/f.width+dy[d];
        Check(x >= 0 && x < f.width && y >= 0 && y < f.height &&
            (f.words[node] & (1u<<(8+d))) != 0 && (f.words[node] & (1u<<(16+d))) == 0,
            "natural route invents an outgoing dry edge or leaves its frozen map");
        node = f.node(x,y); Check(f.allowed(node,2), "natural route enters a forbidden surface");
        result.diagonal += d & 1; result.geometric += (d & 1) ? 1414 : 1000; point();
    }
    result.end = node;
    return result;
}

static void CheckNaturalWholeBounds(const NaturalQuality& before, const NaturalQuality& after, int target, int capacity) {
    Check(after.end == target && after.steps <= capacity && after.steps <= MaxSteps,
        "natural route loses its endpoint or exceeds its publication buffer");
    Check(i64(after.steps)*100 <= i64(before.steps)*105,
        "natural replacements compound beyond the original whole-route five-percent step allowance");
    Check(after.maxCross <= before.maxCross, "natural replacement increases the global maximum line deviation");
}

static void CheckNaturalWindowQuality(const NaturalQuality& before, const NaturalQuality& after, bool anyWholeIncrease) {
    Check(after.geometric*100 <= before.geometric*(anyWholeIncrease ? 90 : 95),
        "natural replacement lacks the required five/ten-percent geometric improvement");
    Check(after.maxCross*4 <= before.maxCross*3,
        "natural replacement does not reduce the local maximum deviation by a quarter");
    Check(after.sumCross*i64(before.steps+1)*4 <= before.sumCross*i64(after.steps+1)*3,
        "natural replacement does not reduce its vertex-mean local deviation by a quarter");
}

// Immutable 0.1.11 outputs from NaturalRouteFixture.json, compressed only to keep
// the captured evidence readable. These are inputs, not a second path solver.
static std::vector<uint8_t> CapturedNaturalBaseline(int which) {
    std::vector<uint8_t> result;
    auto append = [&](std::initializer_list<std::pair<int,int>> runs) {
        for (const auto& run : runs) result.insert(result.end(),size_t(run.second),uint8_t(run.first));
    };
    if (which == 0) append({{2,3},{1,2},{2,6},{1,2},{2,6},{1,2},{2,5},{1,2},{2,6},{1,2},{2,5},{1,1},{3,47}});
    else {
        Check(which == 1, "unknown captured natural-route case");
        append({{2,10},{3,1},{2,7},{1,4},{2,2},{1,7},{2,2},{1,6},{2,2},{1,7},{2,2},{1,7},{2,2},
            {1,7},{2,2},{1,6},{2,2},{1,7},{2,2},{1,3},{3,58},{2,1},{3,3},{2,1},{3,1},{2,4},
            {3,1},{2,17},{3,1},{2,17},{1,5},{2,2},{1,2}});
    }
    Check(result.size() == size_t(which == 0 ? 89 : 201), "captured natural-route trace length differs");
    return result;
}

// Whole farmer routes captured through both public entry points from V4 source
// e68953a7bc3301223e97b35dfa7b4ca2618c77546a7b06e679707460f1448eb8.
// The same frozen edge fixture contains their early minor bend and later major L.
static std::vector<uint8_t> CapturedFullNaturalBaseline(int which) {
    std::vector<uint8_t> result;
    auto append = [&](std::initializer_list<std::pair<int,int>> runs) {
        for (const auto& run : runs) result.insert(result.end(),size_t(run.second),uint8_t(run.first));
    };
    if (which == 0) append({{6,1},{7,1},{6,6}});
    else { Check(which == 1, "unknown whole farmer fixture"); append({{5,3},{7,3},{6,6}}); }
    append({{4,1},{5,1},{4,1},{5,1},{4,1},{5,1},{4,1},{5,1},{4,14},{3,2},{4,3},{3,2},{4,3},
        {3,1},{4,2},{3,2},{4,1},{3,1},{2,10},{3,1},{2,7},{1,4},{2,2},{1,7},{2,2},{1,6},{2,2},
        {1,7},{2,2},{1,7},{2,2},{1,7},{2,2},{1,6},{2,2},{1,7},{2,2},{1,3},{3,58},{2,1},{3,3},
        {2,1},{3,1},{2,4},{3,1},{2,17},{3,1},{2,17},{1,2},{2,1},{1,3},{2,1},{1,2},{2,1},{1,6},
        {3,1},{2,12},{3,1}});
    Check(result.size() == size_t(which ? 273 : 269), "whole farmer fixture direction count differs");
    return result;
}

static void NaturalFrozenRegressionTests() {
    const auto data = FrozenNaturalMap::read();
    const int coordinates[][4] = {{355,332,444,368},{299,363,500,367}};
    // Same frozen graph queried through 0.1.11's SingleRoute/Query (DLL hash in
    // NaturalRouteFixture.json). Native direction ties are not rotation-invariant;
    // compare each public entry to its own old output, not another orientation's path.
    // Fields: steps, actual primary cost, geometric cost, global maximum cross.
    static constexpr i64 publishedBaselines[8][2][2][4] = {
        {{{89,89,113012,2491},{89,89,113012,2491}},{{201,201,253164,11005},{201,201,253164,11005}}},
        {{{89,89,121292,2805},{89,89,121292,2805}},{{201,201,244056,9026},{201,201,244056,9026}}},
        {{{89,89,121292,2805},{89,89,121292,2805}},{{201,201,244056,9026},{201,201,244056,9026}}},
        {{{89,89,113012,2491},{89,89,113012,2491}},{{201,201,253164,11005},{201,201,253164,11005}}},
        {{{89,89,121292,2805},{89,89,121292,2805}},{{201,201,244056,9026},{201,201,244056,9026}}},
        {{{89,89,120464,2716},{89,89,103904,1749}},{{201,201,237432,8857},{201,201,208452,1507}}},
        {{{89,89,113012,2491},{89,89,113012,2491}},{{201,201,253164,11005},{201,201,253164,11005}}},
        {{{89,89,113012,2491},{89,89,103904,1749}},{{201,201,253164,11001},{201,201,208452,1507}}}
    };
    for (int orientation = 0; orientation < 8; ++orientation) for (int which = 0; which < 2; ++which) {
        const int width = (orientation&1) ? data.height : data.width, height = (orientation&1) ? data.width : data.height;
        Fixture f(width,height);
        auto transform = [&](int node) {
            int x = node%data.width, y = node/data.width;
            if (orientation&1) std::swap(x,y);
            if (orientation&2) x = width-1-x; if (orientation&4) y = height-1-y;
            return f.node(x,y);
        };
        uint8_t directions[8];
        for (int d = 0; d < 8; ++d) {
            int x = dx[d], y = dy[d]; if (orientation&1) std::swap(x,y);
            if (orientation&2) x = -x; if (orientation&4) y = -y;
            int mapped = 0; while (mapped < 8 && (dx[mapped] != x || dy[mapped] != y)) ++mapped;
            Check(mapped < 8, "frozen edge rotation is not a native direction"); directions[d] = uint8_t(mapped);
        }
        for (size_t i = 0; i < data.edges.size(); ++i) for (int d = 0; d < 8; ++d)
            if (data.edges[i] & (1u<<d)) f.words[transform(int(i))] |= (1u<<directions[d]) | (1u<<(8+directions[d]));
        f.publish();
        const int start = transform(data.node(coordinates[which][0],coordinates[which][1]));
        const int target = transform(data.node(coordinates[which][2],coordinates[which][3]));
        auto baseline = CapturedNaturalBaseline(which); for (auto& d : baseline) d = directions[d];
        const int n = int(baseline.size());
        const auto before = MeasureNaturalQuality(f,start,baseline,start,target);
        Check(before.end == target && before.geometric == (which ? 253164 : 113012) &&
            before.maxCross == (which ? 11005 : 2491), "captured baseline or frozen edge rotation differs from its evidence");
        std::vector<int> nodes(size_t(n)+1,start);
        for (int i = 0; i < n; ++i) nodes[i+1] = nodes[i]+f.context->map.off[baseline[i]];
        auto& scratch = *f.context->scratch;
        TraversalView view(f.context->map,*f.context->mask(1),*f.context->surfaces,2);
        const CorridorWindow window = FindCorridorWindow(f.context->map,baseline,nodes,nullptr);
        Check(window.a == (which ? 24 : 0) && window.b == (which ? 152 : 89),
            "frozen severe bend no longer selects the captured source window");
        auto check = [&](const std::vector<uint8_t>& path, bool strict = true, int api = 0) {
            const auto after = MeasureNaturalQuality(f,start,path,start,target);
            NaturalQuality reference = before;
            if (!strict) {
                const auto& old = publishedBaselines[orientation][which][api];
                reference.steps = int(old[0]); reference.geometric = old[2]; reference.maxCross = old[3];
            }
            CheckNaturalWholeBounds(reference,after,target,MaxSteps);
            if (strict) Check(after.steps == n && after.geometric <= (which ? 220000 : 103904) &&
                    after.maxCross <= i64(which ? 12 : 4)*n,
                    "captured natural route is still a long artificial approach around the buildings");
            else Check(after.geometric <= reference.geometric, "natural public route is geometrically worse than its own old baseline");
            return after;
        };
        auto improved = baseline; i64 rays = 0, segments = 0; bool capped = false;
        const i64 memoryBefore = f.context->memory.bytes, peakBefore = f.context->memory.peak;
        ImproveCorridors(f.context->map,view,scratch,start,improved,1,MaxSteps,rays,segments,capped);
        Check(improved != baseline && !capped, "bounded work still discards the real farmer/castle-route improvement");
        const auto after = check(improved);
        Check(f.context->memory.bytes == memoryBefore && f.context->memory.peak <= std::max(peakBefore,memoryBefore+64*1024),
            "natural route retains temporary native memory or exceeds its 64-KiB workspace");
        const std::vector<uint8_t> oldWindow(baseline.begin()+window.a,baseline.begin()+window.b);
        const std::vector<uint8_t> newWindow(improved.begin()+window.a,improved.begin()+window.b);
        CheckNaturalWindowQuality(MeasureNaturalQuality(f,nodes[window.a],oldWindow,nodes[window.a],nodes[window.b]),
            MeasureNaturalQuality(f,nodes[window.a],newWindow,nodes[window.a],nodes[window.b]),after.steps > before.steps);
        std::vector<uint8_t> published; auto result = f.single(start,target,2,published);
        Check(result.status == Found && result.cost == result.length && result.moat == 0,
            "natural single result does not report its actual uniform primary cost"); check(published,orientation == 0,0);
        const int task = f.task(target,2,{start}); f.step(2);
        result = f.query(task,start,target,published);
        Check(result.status == Found && result.cost == result.length && result.moat == 0,
            "natural group result does not report its actual primary cost"); check(published,orientation == 0,1);
        for (int workers : {1,2,4,8}) {
            std::vector<int> starts(19,start), targets(19,target);
            std::vector<uint8_t> bytes(starts.size()*MaxSteps,0xA5); std::vector<QueryResult> results(starts.size());
            Check(mma_query_batch(f.context,task,starts.data(),targets.data(),int(starts.size()),8,workers,
                bytes.data(),MaxSteps,results.data()) == Ok, "natural batch ABI");
            for (size_t i = 0; i < starts.size(); ++i) {
                const auto& r = results[i];
                std::vector<uint8_t> path(bytes.begin()+i*MaxSteps,bytes.begin()+i*MaxSteps+r.length);
                Check(r.status == Found && r.cost == r.length && path == published && bytes[i*MaxSteps+r.length] == 0xA5,
                    "natural publication changes across logical workers or overwrites its output tail");
            }
        }
        if (orientation == 0 && which == 0) {
            auto rejected = baseline; const i64 oldLimit = scratch.memory->maximum, oomBytes = f.context->memory.bytes;
            scratch.memory->maximum = scratch.memory->bytes;
            ImproveCorridors(f.context->map,view,scratch,start,rejected,1,MaxSteps,rays,segments,capped);
            scratch.memory->maximum = oldLimit;
            Check(rejected == baseline && f.context->memory.bytes == oomBytes,
                "optional corridor workspace failure destroys a valid baseline");
            uint8_t byte = 0xA5; QueryResult shortResult{};
            Check(mma_single_route(f.context,1,target,1,0,2,start,&byte,0,&shortResult) == Ok &&
                shortResult.status == OverLimit && shortResult.length == 0 && byte == 0xA5,
                "natural single ignores a zero-size publication buffer");
            int g = n, w = 0; auto excluded = baseline;
            StraightenRoute(f.context->map,view,scratch,start,excluded,1,7,g,w);
            Check(excluded == baseline, "natural corridor changes an excluded moat-profile baseline");
            int outside = 0, changed = 0; uint8_t penalty = 1;
            Check(mma_penalty_update(f.context,1,&outside,&penalty,1,&changed) == Ok, "excluded penalty fixture ABI");
            excluded = baseline; g = n; w = 0;
            StraightenRoute(f.context->map,view,scratch,start,excluded,1,0,g,w);
            Check(excluded == baseline, "natural corridor changes a baseline when a penalty profile is active");
        }
    }
}

static void NaturalRankedWholeRouteTests() {
    const auto data = FrozenNaturalMap::read();
    const int starts[][2] = {{303,325},{307,324}};
    for (int which = 0; which < 2; ++which) {
        Fixture f(data.width,data.height); data.publish(f);
        const int start = data.node(starts[which][0],starts[which][1]), target = data.node(521,363);
        const auto baseline = CapturedFullNaturalBaseline(which);
        const auto before = MeasureNaturalQuality(f,start,baseline,start,target);
        Check(before.end == target && before.geometric == (which ? 335928 : 329858) &&
            before.maxCross == (which ? 8658 : 8436), "whole farmer capture differs from its V4 evidence");
        std::vector<int> nodes(baseline.size()+1,start);
        for (size_t i = 0; i < baseline.size(); ++i) nodes[i+1] = nodes[i]+f.context->map.off[baseline[i]];
        const auto first = FindCorridorWindow(f.context->map,baseline,nodes,nullptr);
        const auto second = FindCorridorWindow(f.context->map,baseline,nodes,&first);
        Check(first.b-first.a == 128 && second.b-second.a == 39 && first.a > second.a,
            "whole farmer fixture no longer ranks the late major bend above the early small bend");
        const int legStart = data.node(298,362);
        const auto leg = std::find(nodes.begin(),nodes.end(),legStart);
        Check(leg != nodes.end() && nodes.end()-leg-1 == 223, "whole farmer capture lost its long local leg");
        const size_t legIndex = size_t(leg-nodes.begin());
        auto check = [&](const std::vector<uint8_t>& path) {
            const auto quality = MeasureNaturalQuality(f,start,path,start,target);
            CheckNaturalWholeBounds(before,quality,target,MaxSteps);
            Check(path.size() == baseline.size() && quality.geometric < before.geometric,
                "whole farmer corridor loses its original primary cost or does not improve geometry");
            Check(std::equal(baseline.begin(),baseline.begin()+first.a,path.begin()) &&
                std::equal(baseline.begin()+first.b,baseline.end(),path.begin()+first.b),
                "a lower-ranked window modifies or replaces the already qualified whole-route result");
            const std::vector<uint8_t> oldWindow(baseline.begin()+first.a,baseline.begin()+first.b);
            const std::vector<uint8_t> newWindow(path.begin()+first.a,path.begin()+first.b);
            CheckNaturalWindowQuality(MeasureNaturalQuality(f,nodes[first.a],oldWindow,nodes[first.a],nodes[first.b]),
                MeasureNaturalQuality(f,nodes[first.a],newWindow,nodes[first.a],nodes[first.b]),false);
            const std::vector<uint8_t> local(path.begin()+legIndex,path.end());
            const auto localQuality = MeasureNaturalQuality(f,legStart,local,legStart,target);
            Check(localQuality.maxCross <= 12*223,
                "a complete farmer route still approaches the destination along the wide artificial L");
        };
        auto improved = baseline; i64 rays = 0, segments = 0; bool capped = false;
        TraversalView view(f.context->map,*f.context->mask(1),*f.context->surfaces,2);
        ImproveCorridors(f.context->map,view,*f.context->scratch,start,improved,1,MaxSteps,rays,segments,capped);
        Check(!capped && improved != baseline,
            "an ineffective early window consumes the major-bend budget or discards an already qualified correction");
        check(improved);
        std::vector<uint8_t> published; auto result = f.single(start,target,2,published);
        Check(result.status == Found && result.cost == result.length && result.ground == result.length && result.moat == 0,
            "whole farmer single publication misreports its actual primary cost"); check(published);
        const int task = f.task(target,2,{start}); f.step(2);
        result = f.query(task,start,target,published);
        Check(result.status == Found && result.cost == result.length && result.ground == result.length && result.moat == 0,
            "whole farmer group publication misreports its actual primary cost"); check(published);
        for (int workers : {1,8}) {
            std::vector<int> from(19,start), to(19,target);
            std::vector<uint8_t> bytes(from.size()*MaxSteps,0xA5); std::vector<QueryResult> results(from.size());
            Check(mma_query_batch(f.context,task,from.data(),to.data(),int(from.size()),8,workers,
                bytes.data(),MaxSteps,results.data()) == Ok, "whole farmer batch ABI");
            for (size_t i = 0; i < from.size(); ++i) {
                const auto& r = results[i];
                Check(r.status == Found && r.cost == r.length && r.length == int(published.size()) &&
                    std::equal(published.begin(),published.end(),bytes.begin()+i*MaxSteps) &&
                    bytes[i*MaxSteps+r.length] == 0xA5,
                    "whole farmer correction depends on worker count or writes beyond its publication");
            }
        }
    }
}

static int AddDirectedRoute(Fixture& f, int start, const std::vector<uint8_t>& path) {
    int node = start;
    for (uint8_t d : path) { f.edge(node%f.width,node/f.width,d); node += dy[d]*f.width+dx[d]; }
    return node;
}

static void NaturalCostBudgetTests() {
    for (int n : {19,20}) {
        Fixture f(60,100); const int start = f.node(10,80);
        std::vector<uint8_t> baseline(size_t(n/2),1);
        if (n&1) baseline.push_back(2);
        baseline.insert(baseline.end(),size_t(n/2),3);
        const int target = AddDirectedRoute(f,start,baseline);
        std::vector<uint8_t> alternate{4}; alternate.insert(alternate.end(),size_t(n-1),2); alternate.push_back(1);
        Check(AddDirectedRoute(f,start,alternate) == target, "five-percent boundary fixture endpoint");
        f.publish(); TraversalView view(f.context->map,*f.context->mask(1),*f.context->surfaces,2);
        const auto before = MeasureNaturalQuality(f,start,baseline,start,target);
        auto path = baseline; i64 rays = 0, segments = 0; bool capped = false;
        ImproveCorridors(f.context->map,view,*f.context->scratch,start,path,1,MaxSteps,rays,segments,capped);
        const auto after = MeasureNaturalQuality(f,start,path,start,target);
        CheckNaturalWholeBounds(before,after,target,MaxSteps);
        if (n == 19) Check(path == baseline, "five-percent step cap rounds 19 steps up instead of down");
        else {
            Check(path.size() == 21, "exactly five-percent extra primary cost is not admitted for the direct route");
            CheckNaturalWindowQuality(before,after,true);
            path = baseline; capped = false;
            ImproveCorridors(f.context->map,view,*f.context->scratch,start,path,1,20,rays,segments,capped);
            Check(path == baseline, "natural replacement exceeds a buffer that fits only the original route");
            // Establish the native search's unchanged optimum before testing the
            // public publication boundary: smoothing deliberately spends one step,
            // so SingleSearch must preserve its signed negative "saved" value.
            Check(f.distance(start,target,2) == 20, "public extra-cost fixture lost its independent 20-step optimum");
            auto& field = *f.context->scratch->local;
            const Box all{0,0,f.width-1,f.height-1};
            field.solveTowards(f.context->map,view,target,start,start,1,0,all);
            Check(field.reached(start) && field.dist[start] == 20,
                "public extra-cost fixture does not reach the native A* baseline of 20");
            const int task = f.task(target,2,{start}); f.step(1);
            for (int api = 0; api < 2; ++api) for (int capacity : {21,20}) {
                std::array<uint8_t,22> bytes; bytes.fill(0xA5); QueryResult published{};
                const int code = api == 0 ?
                    mma_single_route(f.context,1,target,1,0,2,start,bytes.data(),capacity,&published) :
                    mma_query(f.context,task,start,target,8,bytes.data(),capacity,&published);
                Check(code == Ok && published.status == Found && published.cost == capacity &&
                    published.length == capacity && published.ground == capacity && published.moat == 0,
                    "public extra-cost route loses its signed saving, actual cost, ground count or length");
                const std::vector<uint8_t> emitted(bytes.begin(),bytes.begin()+published.length);
                f.verify(start,target,2,published,emitted,false);
                const auto quality = MeasureNaturalQuality(f,start,emitted,start,target);
                CheckNaturalWholeBounds(before,quality,target,capacity);
                if (capacity == 21) CheckNaturalWindowQuality(before,quality,true);
                else Check(emitted == baseline, "short public buffer does not preserve the valid original 20-step route");
                Check(bytes[size_t(published.length)] == 0xA5,
                    "public extra-cost route writes beyond its actual published length");
            }
        }
    }
    // Two different dominant axes separated by a straight connector produce two
    // disjoint 32-step source windows. Each can improve via a 36-step corridor;
    // the original whole route (113 -> floor(118.65)=118) permits only one.
    Fixture f(100,100); const int start = f.node(10,80);
    std::vector<uint8_t> baseline(16,1); baseline.insert(baseline.end(),16,3);
    baseline.insert(baseline.end(),49,0); baseline.insert(baseline.end(),16,5); baseline.insert(baseline.end(),16,3);
    const int target = AddDirectedRoute(f,start,baseline);
    std::vector<uint8_t> first{4,4}; first.insert(first.end(),32,2); first.push_back(0); first.push_back(0);
    Check(AddDirectedRoute(f,start,first) == f.node(42,80), "first disjoint corridor endpoint");
    std::vector<uint8_t> second{2,2}; second.insert(second.end(),32,4); second.push_back(6); second.push_back(6);
    Check(AddDirectedRoute(f,f.node(42,31),second) == target, "second disjoint corridor endpoint");
    f.publish(); TraversalView view(f.context->map,*f.context->mask(1),*f.context->surfaces,2);
    std::vector<int> nodes(baseline.size()+1,start);
    for (size_t i = 0; i < baseline.size(); ++i) nodes[i+1] = nodes[i]+f.context->map.off[baseline[i]];
    const auto a = FindCorridorWindow(f.context->map,baseline,nodes,nullptr);
    const auto b = FindCorridorWindow(f.context->map,baseline,nodes,&a);
    Check(a.a == 0 && a.b == 32 && b.a == 81 && b.b == 113,
        "non-compounding fixture does not select two disjoint original windows");
    const auto before = MeasureNaturalQuality(f,start,baseline,start,target);
    auto path = baseline; i64 rays = 0, segments = 0; bool capped = false;
    ImproveCorridors(f.context->map,view,*f.context->scratch,start,path,1,MaxSteps,rays,segments,capped);
    const auto after = MeasureNaturalQuality(f,start,path,start,target);
    CheckNaturalWholeBounds(before,after,target,MaxSteps);
    Check(path.size() == 117 && !capped,
        "two windows compound their primary allowance or discard an otherwise affordable first correction");
}

static void NaturalCutProofTests() {
    for (int orientation = 0; orientation < 8; ++orientation) for (int edgeCase = 0; edgeCase < 2; ++edgeCase) {
        Fixture f(64,64);
        auto point = [&](int x, int y) {
            if (orientation&1) std::swap(x,y);
            if (orientation&2) x = 63-x; if (orientation&4) y = 63-y;
            return f.node(x,y);
        };
        int direction[8];
        for (int d = 0; d < 8; ++d) {
            int x = dx[d], y = dy[d]; if (orientation&1) std::swap(x,y);
            if (orientation&2) x = -x; if (orientation&4) y = -y;
            int mapped = 0; while (mapped < 8 && (dx[mapped] != x || dy[mapped] != y)) ++mapped;
            Check(mapped < 8, "cut fixture direction transform"); direction[d] = mapped;
        }
        OpenGround(f);
        for (int y = 0; y < 64; ++y) for (int d : {1,2,3})
            f.words[point(32,y)] &= ~((1u<<direction[d]) | (1u<<(8+direction[d])));
        const int ay = edgeCase ? 1 : 25, by = edgeCase ? 1 : 35;
        const int start = point(10,ay), target = point(50,by), gapY = edgeCase ? 0 : 19;
        auto prove = [&](bool expected) {
            f.publish(); TraversalView view(f.context->map,*f.context->mask(1),*f.context->surfaces,2);
            CorridorBudget budget(80);
            Check(CorridorCut(f.context->map,view,budget,start,target) == expected,
                "directed cut proof rejects a real forward gap or accepts reverse-only connectivity");
            Check(budget.work <= 40+75, "cut precheck exceeds its reference-plus-75 bound");
        };
        prove(true); // Reverse links remain open: they cannot cross the forward cut.
        f.words[point(32,gapY)] |= (1u<<direction[2]) | (1u<<(8+direction[2]));
        prove(false); // One directed forward edge at the outer band/border is enough.
        f.surfaces[point(33,gapY)] = 0x80; prove(true);
        f.surfaces[point(33,gapY)] = 0x83; f.surfaces[point(32,gapY)] = 0x80; prove(false);
    }
}

static void NaturalGeometryThresholdTests() {
    for (int extraCost = 0; extraCost < 2; ++extraCost) {
        Fixture f(80,100); const int start = f.node(10,80);
        std::vector<uint8_t> window(10,1); window.insert(window.end(),10,3);
        const int join = AddDirectedRoute(f,start,window);
        auto baseline = window; baseline.insert(baseline.end(),49,0);
        const int target = AddDirectedRoute(f,start,baseline);
        std::vector<uint8_t> candidate;
        if (extraCost) {
            candidate.push_back(4); candidate.insert(candidate.end(),7,1);
            candidate.insert(candidate.end(),6,3); candidate.insert(candidate.end(),7,2);
        } else {
            for (int i = 0; i < 2; ++i) {
                candidate.insert(candidate.end(),4,1); candidate.insert(candidate.end(),4,3);
                candidate.insert(candidate.end(),2,2);
            }
        }
        Check(AddDirectedRoute(f,start,candidate) == join, "geometric-threshold fixture endpoints");
        f.publish(); TraversalView view(f.context->map,*f.context->mask(1),*f.context->surfaces,2);
        const auto oldQuality = MeasureNaturalQuality(f,start,window,start,join);
        const auto offered = MeasureNaturalQuality(f,start,candidate,start,join);
        Check(offered.geometric*100 <= oldQuality.geometric*95 && offered.geometric*100 > oldQuality.geometric*90,
            "geometric-threshold fixture must lie strictly between five and ten percent improvement");
        auto path = baseline; i64 rays = 0, segments = 0; bool capped = false;
        ImproveCorridors(f.context->map,view,*f.context->scratch,start,path,1,MaxSteps,rays,segments,capped);
        CheckNaturalWholeBounds(MeasureNaturalQuality(f,start,baseline,start,target),
            MeasureNaturalQuality(f,start,path,start,target),target,MaxSteps);
        if (extraCost) Check(path == baseline,
            "a route with extra primary cost is accepted with only five-to-ten-percent geometric improvement");
        else Check(path != baseline && path.size() == baseline.size(),
            "a no-extra-cost route incorrectly needs ten-percent geometric improvement");
    }
}

static void NaturalWorkFallbackTests() {
    Fixture f(160,150); OpenGround(f);
    for (int y = 0; y < f.height; ++y) for (int x = 60; x < f.width; ++x) f.words[f.node(x,y)] = 0;
    const int start = f.node(10,100);
    std::vector<uint8_t> baseline(64,1); baseline.insert(baseline.end(),64,3);
    const int target = AddDirectedRoute(f,start,baseline);
    f.edge(60,99,3); // The first blocked plane has a real gap, but its component is a dead end.
    f.publish(); TraversalView view(f.context->map,*f.context->mask(1),*f.context->surfaces,2);
    auto path = baseline; i64 rays = 0, segments = 0; bool capped = false;
    const i64 bytes = f.context->memory.bytes;
    ImproveCorridors(f.context->map,view,*f.context->scratch,start,path,1,MaxSteps,rays,segments,capped);
    Check(capped && path == baseline && f.context->memory.bytes == bytes,
        "bounded unsuccessful corridor work publishes a partial route or retains its temporary memory");
    Check(MeasureNaturalQuality(f,start,path,start,target).end == target,
        "budget fallback loses the original valid endpoint");
}

// Previous public direct-route behaviour, retained only as a regression reference:
// it is the fallback that must remain available when the quieter corridor is blocked.
static std::vector<uint8_t> PreviousRaster(int sx, int sy, int tx, int ty) {
    const int ax = std::abs(tx-sx), ay = std::abs(ty-sy), length = std::max(ax,ay);
    const int signX = (tx > sx)-(tx < sx), signY = (ty > sy)-(ty < sy);
    const bool majorX = ax >= ay; const int minor = majorX ? ay : ax;
    int x = sx, y = sy, error = length/2;
    std::vector<uint8_t> path;
    for (int i = 0; i < length; ++i) {
        int nx = x, ny = y;
        if (majorX) nx += signX; else ny += signY;
        error += minor;
        if (error >= length) { error -= length; if (majorX) ny += signY; else nx += signX; }
        int direction = 0;
        while (direction < 8 && (dx[direction] != nx-x || dy[direction] != ny-y)) ++direction;
        Check(direction < 8, "previous raster reference is not an eight-neighbour step");
        path.push_back(uint8_t(direction)); x = nx; y = ny;
    }
    return path;
}

// A minimum step count alone accepts the reported large L. The cross product also
// bounds the actual emitted route to one raster cell from the reference segment.
static void VerifyStraightRay(const Fixture& f, int start, int target,
    const QueryResult& result, const std::vector<uint8_t>& path, bool bfsOracle = true) {
    if (bfsOracle) f.verify(start,target,2,result,path,true);
    else Check(result.status == Found, "free minimum-cost ray not found");
    const int sx = start % f.width, sy = start / f.width;
    const int tx = target % f.width, ty = target / f.width;
    const int vx = tx-sx, vy = ty-sy, length = std::max(std::abs(vx),std::abs(vy));
    Check(result.length == length && result.ground == length && result.moat == 0,
        "direct ray changes minimum length or ground classification");
    Check(result.cost == length && path.size() == size_t(length), "free ray cost or byte count differs from its length");
    Check(result.flags == 0 && result.reserved == 0, "direct ray reports anchor flags or A* settles");
    int x = sx, y = sy;
    for (size_t i = 0; i < path.size(); ++i) {
        Check(path[i] < 8 && (f.words[f.node(x,y)] & (1u << (8+path[i]))) != 0,
            "direct ray invented an outgoing ground edge");
        x += dx[path[i]]; y += dy[path[i]];
        Check(x >= 0 && x < f.width && y >= 0 && y < f.height && f.allowed(f.node(x,y),2),
            "direct ray leaves the map or enters a forbidden surface");
        Check(std::abs((x-sx)*vy - (y-sy)*vx) <= length,
            "minimum-cost route still makes a large L instead of following the segment");
        Check(std::max(std::abs(tx-x),std::abs(ty-y)) == length-int(i)-1,
            "direct ray does not advance along its major axis");
    }
}

static void DirectGroundRayTests() {
    Fixture f(129,129); OpenGround(f); f.publish();
    const int start = f.node(64,64);
    const int offsets[][2] = {{0,20},{0,-20},{20,0},{-20,0},{20,20},{20,-20},{-20,20},{-20,-20},
        {3,20},{-3,20},{3,-20},{-3,-20},{20,3},{20,-3},{-20,3},{-20,-3},
        {11,20},{-11,20},{11,-20},{-11,-20},{20,11},{20,-11},{-20,11},{-20,-11},
        {45,22},{45,-22},{-45,22},{-45,-22},{22,45},{22,-45},{-22,45},{-22,-45}};
    std::vector<int> starts, targets;
    std::vector<std::vector<uint8_t>> expected;
    uint8_t bytes[MaxSteps+1];
    for (const auto& offset : offsets) {
        const int target = f.node(64+offset[0],64+offset[1]);
        std::fill(std::begin(bytes),std::end(bytes),uint8_t(0xA5)); QueryResult result{};
        // One settle cannot discover this route. Success must therefore avoid A*,
        // rather than merely change the old search's direction order.
        Check(mma_single_route_limited(f.context,1,target,1,0,2,start,1,bytes,MaxSteps,&result) == Ok,
            "direct limited single ABI");
        std::vector<uint8_t> path(bytes,bytes+result.length);
        VerifyStraightRay(f,start,target,result,path);
        const int turns = DirectionChanges(path);
        if (std::abs(offset[0]) == std::abs(offset[1]) || offset[0] == 0 || offset[1] == 0)
            Check(turns == 0, "a supported eight-direction ray needlessly changes heading");
        if (result.length == 45)
            Check(turns <= 15, "reported 45/22 route retains per-tile heading changes");
        Check(bytes[result.length] == 0xA5, "direct single writes past active route");
        starts.push_back(start); targets.push_back(target); expected.push_back(path);
    }
    const int task = f.task(start,2,starts);
    std::vector<uint8_t> pending;
    auto result = f.query(task,start,targets[0],pending);
    Check(result.status == Pending && result.length == 0, "direct ray bypasses task readiness");
    f.step(4);
    for (size_t i = 0; i < targets.size(); ++i) {
        std::vector<uint8_t> path; result = f.query(task,start,targets[i],path);
        VerifyStraightRay(f,start,targets[i],result,path);
        Check(path == expected[i], "single and group direct rays disagree");
    }
    for (int workers : {1,2,4,8}) {
        std::vector<uint8_t> batch(starts.size()*MaxSteps,0xA5);
        std::vector<QueryResult> results(starts.size());
        Check(mma_query_batch(f.context,task,starts.data(),targets.data(),int(starts.size()),8,workers,
            batch.data(),MaxSteps,results.data()) == Ok, "direct batch ABI");
        for (size_t i = 0; i < starts.size(); ++i) {
            const auto& r = results[i];
            std::vector<uint8_t> path(batch.begin()+i*MaxSteps,batch.begin()+i*MaxSteps+r.length);
            VerifyStraightRay(f,start,targets[i],r,path);
            Check(path == expected[i] && batch[i*MaxSteps+r.length] == 0xA5,
                "direct batch changes bytes across workers or writes past active route");
        }
    }
    f.surface(targets[0],0x80);
    result = f.query(task,start,targets[1],pending);
    Check(result.status == Stale && result.length == 0, "direct ray bypasses stale surface generation");
    std::vector<QueryResult> stale(starts.size()); std::vector<uint8_t> staleBytes(starts.size()*MaxSteps,0xA5);
    Check(mma_query_batch(f.context,task,starts.data(),targets.data(),int(starts.size()),8,8,
        staleBytes.data(),MaxSteps,stale.data()) == Ok, "stale direct batch ABI");
    for (const auto& r : stale) Check(r.status == Stale && r.length == 0, "direct batch bypasses stale task");
}

static void QuietGroundSlopeTests() {
    Fixture f(129,129); OpenGround(f); f.publish();
    const int sx = 64, sy = 64, start = f.node(sx,sy);
    uint8_t bytes[MaxSteps+1];
    // All rational slopes through the reported 45-step range, reflected and rotated.
    // The open map makes Chebyshev an independent exact-cost oracle without another BFS.
    for (int length = 1; length <= 45; ++length) for (int minor = 0; minor <= length; ++minor)
        for (int orientation = 0; orientation < 8; ++orientation) {
            int vx = (orientation & 1) ? minor : length, vy = (orientation & 1) ? length : minor;
            if (orientation & 2) vx = -vx; if (orientation & 4) vy = -vy;
            const int tx = sx+vx, ty = sy+vy, target = f.node(tx,ty);
            std::fill(std::begin(bytes),std::end(bytes),uint8_t(0xA5)); QueryResult result{};
            Check(mma_single_route_limited(f.context,1,target,1,0,2,start,1,bytes,MaxSteps,&result) == Ok,
                "quiet slope single ABI");
            std::vector<uint8_t> path(bytes,bytes+result.length);
            VerifyStraightRay(f,start,target,result,path,false);
            Check(DirectionChanges(path) <= DirectionChanges(PreviousRaster(sx,sy,tx,ty)),
                "quiet free ray adds heading changes compared with the previous raster");
            if (minor == 0 || minor == length) Check(DirectionChanges(path) == 0,
                "pure native direction acquired turns in a rotated slope case");
            Check(bytes[result.length] == 0xA5, "quiet slope overwrites the output sentinel");
        }
    // The actual coordinates are translated by (-300,-320); only the map origin
    // changes, preserving the exact reported (316,364)->(361,342) delta and parity.
    const int from = f.node(16,44), to = f.node(61,22);
    QueryResult actual{};
    Check(mma_single_route_limited(f.context,1,to,1,0,2,from,1,bytes,MaxSteps,&actual) == Ok,
        "reported route single ABI");
    std::vector<uint8_t> path(bytes,bytes+actual.length);
    VerifyStraightRay(f,from,to,actual,path);
    Check(DirectionChanges(PreviousRaster(16,44,61,22)) == 44 && DirectionChanges(path) <= 15,
        "reported 44-turn raster has not become a quieter 45-step route");
}

static void QuietRunQuotientTests() {
    // This rectangle covers every actual (numerator, divisor) pair for D<=800
    // without repeating the same pair for every slope: numerator<=2D, divisor<=D.
    // The ordinary division is the independent oracle for the optimized helper.
    for (int denominator = 1; denominator <= 800; ++denominator) {
        const u64 reciprocal = (u64(1)<<32)/u32(denominator);
        for (int numerator = 0; numerator <= 1600; ++numerator)
            Check(RasterCursor<RasterStyle::Quiet>::runQuotient(numerator,denominator,reciprocal) == numerator/denominator,
                "quiet run quotient differs from exact division in the map-sized range");
    }
    std::mt19937 random(0x673C5A19);
    for (int i = 0; i < 10000; ++i) {
        const int length = 1+int(random()%640000), denominator = 1+int(random()%u32(length));
        const u64 reciprocal = (u64(1)<<32)/u32(denominator);
        const int multiple = (2*length/denominator)*denominator;
        const int cases[] = {0,2*length,int(random()%u32(2*length+1)),multiple,
            std::max(0,multiple-1),std::min(2*length,multiple+1)};
        for (int numerator : cases)
            Check(RasterCursor<RasterStyle::Quiet>::runQuotient(numerator,denominator,reciprocal) == numerator/denominator,
                "quiet run quotient loses an exact-multiple boundary in the full supported node range");
    }
}

static void SelectedEdgePermissionTests() {
    std::mt19937 random(0x14BE6731);
    Fixture f(13,11);
    for (int phase = 0; phase < 12; ++phase) {
        for (size_t i = 0; i < f.words.size(); ++i) {
            // The first phase includes every border bit before Mask clamps it;
            // later phases vary ordinary, ground-only and wet direction bits.
            f.words[i] = phase == 0 ? 0xFFFFFFu : random() & 0xFFFFFFu;
            f.surfaces[i] = uint8_t(random()%4 | (random()%5 ? 0x80 : 0));
        }
        f.publish();
        for (int kind = 0; kind <= 2; ++kind) {
            TraversalView view(f.context->map,*f.context->mask(1),*f.context->surfaces,kind);
            for (int moat : {0,7}) for (int node = 0; node < int(f.words.size()); ++node) {
                const uint8_t expected = view.outgoing(node,moat);
                for (int d = 0; d < 8; ++d)
                    Check(view.allowsEdge(node,d,moat) == ((expected & (1u<<d)) != 0),
                        "selected-edge permission differs from the full directed/surface mask");
            }
        }
    }
}

static void QuietGroundFallbackAndBridgeTests() {
    {
        Fixture f(80,80);
        const int sx = 10, sy = 40, tx = 55, ty = 18;
        const auto previous = PreviousRaster(sx,sy,tx,ty);
        int x = sx, y = sy;
        for (uint8_t d : previous) { f.edge(x,y,d); x += dx[d]; y += dy[d]; }
        f.publish(); const int start = f.node(sx,sy), target = f.node(tx,ty);
        uint8_t bytes[MaxSteps+1]; std::fill(std::begin(bytes),std::end(bytes),uint8_t(0xA5)); QueryResult result{};
        Check(mma_single_route_limited(f.context,1,target,1,0,2,start,1,bytes,MaxSteps,&result) == Ok,
            "blocked quiet corridor fallback ABI");
        std::vector<uint8_t> path(bytes,bytes+result.length);
        VerifyStraightRay(f,start,target,result,path);
        Check(path == previous && bytes[result.length] == 0xA5,
            "blocked quiet corridor loses the legal previous raster or adds search work");
    }
    {
        Fixture f(50,50); OpenGround(f);
        for (int y = 11; y < 30; y += 2) BlockGroundTile(f,10,y);
        f.publish(); const int start = f.node(10,10), target = f.node(10,30);
        std::vector<uint8_t> repeated;
        for (int i = 0; i < 10; ++i) { repeated.push_back(3); repeated.push_back(5); }
        Check(DirectionChanges(repeated) == 19, "repeated-bridge regression no longer has the reported winding input");
        auto& scratch = *f.context->scratch;
        TraversalView view(f.context->map,*f.context->mask(1),*f.context->surfaces,2);
        int ground = 20, moat = 0;
        const i64 saved = StraightenRoute(f.context->map,view,scratch,start,repeated,1,0,ground,moat);
        QueryResult normalized{}; normalized.status = Found; normalized.length = int(repeated.size());
        normalized.ground = ground; normalized.moat = moat; normalized.cost = 20-saved;
        VerifyStraightRay(f,start,target,normalized,repeated);
        Check(saved == 0 && DirectionChanges(repeated) <= 2,
            "repeated valid bridges were not consolidated into a straight side strip");
        uint8_t bytes[MaxSteps]; QueryResult result{};
        Check(mma_single_route_limited(f.context,1,target,1,0,2,start,1,bytes,MaxSteps,&result) == Ok,
            "repeated-bridge public route ABI");
        std::vector<uint8_t> path(bytes,bytes+result.length);
        VerifyStraightRay(f,start,target,result,path);
        Check(DirectionChanges(path) <= 2, "public direct route still emits repeated opposing bridge diagonals");
        // The production pair normalizer consumes an explicit part of the shared
        // cap. Exercise its real remaining-budget boundary without a new test knob.
        const i64 cap = 16LL*20+64;
        i64 work = cap-3; uint8_t first = 3, second = 5;
        const int side = f.node(11,11);
        Check(!NormalizePair(f.context->map,view,side,first,second,1,0,false,&work,cap) &&
            work == cap-3 && first == 3 && second == 5,
            "pair normalization exceeds its remaining work allowance or changes a rejected pair");
        work = cap-4;
        Check(NormalizePair(f.context->map,view,side,first,second,1,0,false,&work,cap) &&
            work == cap && first == 4 && second == 4,
            "pair normalization fails at an exactly sufficient work allowance");
    }
}

// Independently sum emitted edges, including the native per-edge saturation. A
// geometric step count or g*ground+w*moat is not an oracle when surcharges exist.
static i64 EmittedWeightedCost(const Fixture& f, int start, int target,
    const std::vector<uint8_t>& path, int ground, int moat, const std::vector<uint8_t>& penalties) {
    int node = start; i64 cost = 0;
    for (uint8_t d : path) {
        Check(d < 8, "weighted ray emits an invalid direction");
        const bool wet = (f.words[node] & (1u << (16+d))) != 0;
        const bool allowed = moat ? (f.words[node] & (1u << d)) != 0 :
            (f.words[node] & (1u << (8+d))) != 0 && !wet;
        Check(allowed, "weighted ray invents a forbidden directed edge");
        const int next = node + dy[d]*f.width + dx[d];
        Check(next >= 0 && next < int(f.words.size()) && f.allowed(next,2),
            "weighted ray leaves its permitted surface");
        const int fullCost = (moat && wet ? moat : ground) + int(penalties[next])*ground;
        cost += std::min(255,fullCost); node = next;
    }
    Check(node == target, "weighted ray lost its exact endpoint");
    return cost;
}

static void QuietWeightedRayTests() {
    Fixture f(80,80); OpenGround(f); f.publish();
    std::vector<int> nodes(f.words.size()); std::vector<uint8_t> penalties(f.words.size());
    for (size_t i = 0; i < nodes.size(); ++i) {
        nodes[i] = int(i);
        penalties[i] = i % 5 == 0 ? uint8_t(254) : i % 3 == 0 ? uint8_t(2) : uint8_t(0);
    }
    int changed = 0;
    Check(mma_penalty_update(f.context,1,nodes.data(),penalties.data(),int(nodes.size()),&changed) == Ok,
        "quiet weighted penalty fixture ABI");
    const int start = f.node(10,45), target = f.node(55,23);
    for (int phase = 0; phase < 2; ++phase) {
        if (phase) {
            for (int y = 0; y < f.height; ++y) for (int x = 0; x < f.width; ++x)
                if ((x+y)%7 == 0) f.words[f.node(x,y)] |= 0xFF0000u;
            f.publish();
            Check(mma_penalty_update(f.context,1,nodes.data(),penalties.data(),int(nodes.size()),&changed) == Ok,
                "quiet wet penalty fixture ABI");
        }
        TraversalView view(f.context->map,*f.context->mask(1),*f.context->surfaces,2);
        for (int ground : {1,17,255}) {
            const int moat = phase ? 7 : 0;
            std::vector<uint8_t> path; i64 work = 0;
            const i64 cost = RayCost(f.context->map,view,start,target,ground,moat,1000000,work,&path);
            Check(cost >= 0 && path.size() == 45 && work >= 45,
                "quiet weighted ray failed on an open permitted graph");
            Check(cost == EmittedWeightedCost(f,start,target,path,ground,moat,penalties),
                "quiet ray cost differs from emitted surcharge/cap/wet edges");
            work = 0;
            std::vector<uint8_t> rejected{0xEE};
            // The cap deliberately reserves a whole ray per attempted style, as in
            // the previous scheduler, even when its first entered edge rejects it.
            Check(RayCost(f.context->map,view,start,target,ground,moat,0,work,&rejected) < 0 && work == 2*45 &&
                rejected == std::vector<uint8_t>{0xEE},
                "failed quiet and previous weighted emissions are not both charged or roll back incorrectly");
        }
    }
}

static void QuietLongRouteAccountingTests() {
    for (int shape = 0; shape < 2; ++shape) {
        Fixture f(shape ? 2002 : 702,shape ? 205 : 354);
        // A wide prefix and thin tail make Quiet fail late while the previous
        // raster stays usable. These 700/2000-step cases do not necessarily exhaust
        // the cap after restoring the old segment selection; no forced-hit claim.
        const int prefixWidth = shape ? 1500 : 530;
        for (int y = 0; y < f.height; ++y) for (int x = 0; x < prefixWidth; ++x)
            f.words[f.node(x,y)] = 0xFFFFu;
        const int start = f.node(1,1); int x = 1, y = 1;
        std::vector<uint8_t> path;
        if (shape) path = PreviousRaster(1,1,1999,201);
        else for (int i = 0; i < 349; ++i) { path.push_back(3); path.push_back(2); }
        path.push_back(3); path.push_back(4);
        Check(path.size() == size_t(shape ? MaxSteps : 700), "long accounting fixture has the wrong publication boundary");
        for (uint8_t d : path) { f.edge(x,y,d); x += dx[d]; y += dy[d]; }
        const int target = f.node(x,y); f.publish();
        auto& scratch = *f.context->scratch;
        TraversalView view(f.context->map,*f.context->mask(1),*f.context->surfaces,2);
        int ground = int(path.size()), moat = 0;
        const i64 oldCost = ground;
        const i64 saved = StraightenRoute(f.context->map,view,scratch,start,path,1,0,ground,moat);
        const std::vector<uint8_t> penalties(f.words.size(),0);
        const i64 cost = EmittedWeightedCost(f,start,target,path,1,0,penalties);
        Check(cost == oldCost-saved && cost <= oldCost && ground == int(path.size()) && moat == 0,
            "long straightening loses route cost, endpoint or ground accounting");
    }
}

static void DirectGroundObstaclesAndGuards() {
    {
        Fixture f(50,50); OpenGround(f); BlockGroundTile(f,10,20); f.publish();
        const int start = f.node(10,10), target = f.node(10,30);
        uint8_t bytes[MaxSteps]; QueryResult result{};
        Check(mma_single_route_limited(f.context,1,target,1,0,2,start,1,bytes,MaxSteps,&result) == Ok,
            "bridged direct single ABI");
        std::vector<uint8_t> path(bytes,bytes+result.length);
        VerifyStraightRay(f,start,target,result,path);
        const int task = f.task(target,2,{start}); f.step(1);
        auto group = f.query(task,start,target,path); VerifyStraightRay(f,start,target,group,path);
        // Entering surface restrictions are identical to edge restrictions; leaving a
        // forbidden start remains legal and must not disable the direct certificate.
        f.surface(start,0x80);
        result = f.single(start,target,2,path); VerifyStraightRay(f,start,target,result,path);
        f.surface(target,0x80);
        result = f.single(start,target,2,path);
        Check(result.status == Unreachable && result.length == 0, "direct ray enters a forbidden endpoint");
    }
    {
        Fixture f(45,45); OpenGround(f);
        for (int y = 0; y < 44; ++y) BlockGroundTile(f,22,y);
        f.publish(); const int start = f.node(5,22), target = f.node(39,22);
        std::vector<uint8_t> path; auto result = f.single(start,target,2,path);
        f.verify(start,target,2,result,path,true);
        Check(result.reserved > 0 && result.length > 34, "wide wall fails to retain the exact-search fallback");
        BlockGroundTile(f,22,44); f.publish();
        uint8_t bytes[MaxSteps]; std::fill(std::begin(bytes),std::end(bytes),uint8_t(0xA5));
        Check(mma_single_route(f.context,1,target,1,0,2,start,bytes,MaxSteps,&result) == Ok,
            "unreachable direct fallback ABI");
        Check(result.status == Unreachable && result.length == 0 && bytes[0] == 0xA5,
            "failed partial ray publishes bytes for an unreachable query");
    }
    {
        Fixture f(35,35);
        for (int y = 5; y < 29; ++y) f.edge(10,y,4);
        f.publish(); const int start = f.node(10,5), target = f.node(10,29);
        std::vector<uint8_t> path; auto result = f.single(start,target,2,path);
        VerifyStraightRay(f,start,target,result,path);
        result = f.single(target,start,2,path);
        Check(result.status == Unreachable, "direct ray reverses a one-way edge");
    }
    {
        Fixture f(35,35);
        for (int y = 5; y < 29; ++y) if (y != 16 && y != 17) f.edge(10,y,4);
        f.edge(10,16,3); f.edge(10,18,1); // Only the reverse of the bridge's second edge exists.
        f.publish(); const int start = f.node(10,5), target = f.node(10,29);
        uint8_t bytes[MaxSteps]; std::fill(std::begin(bytes),std::end(bytes),uint8_t(0xA5)); QueryResult result{};
        Check(mma_single_route(f.context,1,target,1,0,2,start,bytes,MaxSteps,&result) == Ok &&
            result.status == Unreachable && result.length == 0 && bytes[0] == 0xA5,
            "bridge invents its second forward edge or leaks a failed partial ray");
        const int task = f.task(target,2,{start}); f.step(1);
        std::vector<uint8_t> path; result = f.query(task,start,target,path);
        Check(result.status == Unreachable, "group bridge reverses its second one-way edge");
        f.edge(11,17,5); f.publish();
        result = f.single(start,target,2,path); VerifyStraightRay(f,start,target,result,path);
    }
    {
        Fixture f(35,35); OpenGround(f); f.publish();
        const int start = f.node(10,5), target = f.node(10,29);
        uint8_t bytes[MaxSteps]; std::fill(std::begin(bytes),std::end(bytes),uint8_t(0xA5)); QueryResult result{};
        Check(mma_single_route(f.context,1,target,1,0,2,start,bytes,0,&result) == Ok &&
            result.status == OverLimit && result.length == 0 && bytes[0] == 0xA5,
            "zero-capacity direct query writes or returns an overlong result");
        Check(mma_single_route(f.context,1,target,1,0,2,start,bytes,23,&result) == Ok &&
            result.status == OverLimit && result.length == 0 && bytes[23] == 0xA5,
            "direct query ignores a short capacity");
        Check(mma_single_route(f.context,1,start,1,0,2,start,bytes,0,&result) == Ok &&
            result.status == Found && result.length == 0 && result.cost == 0,
            "zero-length route changed its existing result");
        Check(mma_single_route_limited(f.context,1,target,1,1,2,start,1,bytes,MaxSteps,&result) == Ok &&
            result.status == OverLimit && result.reserved == 1,
            "ground direct shortcut changes a moat-profile search budget");
        std::vector<int> nodes(f.words.size()); std::vector<uint8_t> penalty(f.words.size(),1);
        for (size_t i = 0; i < nodes.size(); ++i) nodes[i] = int(i);
        int changed = 0;
        Check(mma_penalty_update(f.context,1,nodes.data(),penalty.data(),int(nodes.size()),&changed) == Ok,
            "direct penalty fixture ABI");
        std::vector<uint8_t> path; result = f.single(start,target,2,path);
        f.verify(start,target,2,result,path,false);
        Check(result.cost == 48 && result.reserved > 0, "direct certificate ignores entering penalties");
        // MaxCost saturation makes cost(to)==ground true despite a positive penalty.
        // The certificate still promises surcharge-free edges, not accidental equality.
        Check(mma_single_route_limited(f.context,1,target,255,0,2,start,1,bytes,MaxSteps,&result) == Ok &&
            result.status == OverLimit && result.reserved == 1,
            "saturated entering penalty incorrectly qualifies as a surcharge-free direct ray");
    }
}

static void SubanchorAndBatchTests() {
    for (int shape = 0; shape < 2; ++shape) {
    std::vector<i64> baseline;
    // Every worker count starts with cold fields and cold worker scratch. A reused fixture would
    // let field/sub-anchor cache hits hide the actual parallel computations being compared.
    for (int workers : {1,2,4,8}) {
        Fixture f(64, 64);
        if (shape == 0) PopulateSubanchorFixture(f);
        else {
            std::fill(f.words.begin(),f.words.end(),0xFFFFu);
            for (int y = 8; y <= 55; ++y) f.surfaces[f.node(32,y)] = 0x80;
            f.publish();
        }
        std::vector<int> starts, queryTargets;
        if (shape == 0) {
            std::vector<int> corridor;
            for (int x = 45; x <= 58; ++x) if (x != 50) corridor.push_back(f.node(x,45));
            for (int x = 45; x <= 58; ++x) corridor.push_back(f.node(x,47));
            corridor.push_back(f.node(45,46)); corridor.push_back(f.node(58,46));
            for (int i = 0; i < 19; ++i) {
                starts.push_back(corridor[i]); queryTargets.push_back(corridor[(i+11)%corridor.size()]);
            }
        } else {
            for (int i = 0; i < 19; ++i) {
                starts.push_back(f.node(8+i%10,28+i/10)); queryTargets.push_back(f.node(51+i%7,27+i/7));
            }
        }
        // Nineteen distinct starts and destinations exercise three strides per logical bank,
        // including the non-full tail. Repeated identical rows could hide skipped later strides.
        auto distinct = [](std::vector<int> nodes) { std::sort(nodes.begin(),nodes.end()); return std::unique(nodes.begin(),nodes.end()) == nodes.end(); };
        Check(starts.size() > 16 && distinct(starts) && distinct(queryTargets), "strided batch inputs must be distinct");
        std::vector<i64> snapshot;
        for (int warm = 0; warm < 2; ++warm) {
            std::vector<int> tasks;
            // Nine independent tasks force mma_step to distribute real work even with eight workers.
            for (int kind = 0; kind <= 2; ++kind)
                for (int offset = 0; offset < 3; ++offset)
                    tasks.push_back(f.task(f.node((shape == 0 ? 4 : 52) + offset,shape == 0 ? 4 : 30),kind,starts));
            f.step(workers);
            for (size_t taskIndex = 0; taskIndex < tasks.size(); ++taskIndex) {
                const int kind = int(taskIndex / 3), task = tasks[taskIndex];
                Check(mma_task_status(f.context, task) == Ready, "parallel class field not ready");
                // First batch creates sub-anchors in deterministic caller order; the second uses them
                // in parallel. Both passes are compared including active bytes and every ABI field.
                for (int pass = 0; pass < 2; ++pass) {
                    const std::vector<int>& slots = queryTargets;
                    std::vector<uint8_t> bytes(starts.size() * MaxSteps, 0xFF);
                    std::vector<QueryResult> results(starts.size());
                    Check(mma_query_batch(f.context, task, starts.data(), slots.data(), int(starts.size()), 8, workers,
                        bytes.data(), MaxSteps, results.data()) == Ok, "batch ABI");
                    for (size_t i = 0; i < starts.size(); ++i) {
                        const auto& r = results[i];
                        Check(r.length >= 0 && r.length <= MaxSteps, "batch output length");
                        std::vector<uint8_t> path(bytes.begin()+i*MaxSteps,bytes.begin()+i*MaxSteps+r.length);
                        f.verify(starts[i],slots[i],kind,r,path,false);
                        AppendResult(snapshot, r); snapshot.insert(snapshot.end(), path.begin(), path.end());
                        if (r.length < MaxSteps) Check(bytes[i*MaxSteps+r.length] == 0xFF, "batch writes past active route");
                    }
                }
            }
            for (int id : tasks) Check(mma_task_release(f.context, id) == Ok, "release deterministic task");
        }
        if (workers == 1) baseline = snapshot;
        else Check(snapshot == baseline, "cold/warm class task or subanchor batch depends on worker count");
    }
    }
}

// The context reserves CPU-independent logical workspace quotas. A configuration too small
// even for those banks must fail at create, before any unit can choose a different fallback.
static void MemoryWorkerProbe() {
    Context* context = nullptr;
    Check(mma_create(800,800,64LL << 20,&context) == NoMemory && context == nullptr,
        "insufficient CPU-independent work banks must fail create consistently");
}

// The production limit is also tested through valid ABI inputs. This deliberately large
// pending-task set is a native resource test, not a claim about a normal managed group frame.
// Its dense stored fields consume the same counted allocator as retained sub-anchor fields.
static void ProductionMemoryWorkerProbe() {
    std::vector<i64> baseline;
    for (int workers : {1,2,4,8}) {
        Fixture f(800,800,768LL << 20);
        std::fill(f.words.begin(),f.words.end(),0xFFFFu); f.publish();
        const int start = f.node(50,400), target = f.node(750,400);
        // Warm the same logical work with different physical worker histories. Only physical
        // scheduling differs; every context receives the same masks, tasks and query order.
        const int history[] = {8,1,4,2};
        for (int stage = 0; stage < 4; ++stage) {
            std::vector<int> warm;
            const int warmTarget = f.node(300+stage,400);
            for (int kind = 0; kind <= 2; ++kind) warm.push_back(f.task(warmTarget,kind,{start}));
            int computed = -1;
            Check(mma_step(f.context,3,history[(stage+workers)%4],&computed) == Ok, "worker history step");
            for (int id : warm) Check(mma_task_release(f.context,id) == Ok, "worker history release");
        }
        std::vector<int> ids;
        for (int i = 0; i < 288; ++i) ids.push_back(f.task(target,i%3,{start}));
        int computed = -1;
        const int code = mma_step(f.context,int(ids.size()),workers,&computed);
        int ready = 0, failed = 0;
        Check(code == Ok && computed == int(ids.size()), "resource step ABI");
        std::vector<i64> snapshot = {code,computed};
        for (size_t i = 0; i < ids.size(); ++i) {
            const int status = mma_task_status(f.context,ids[i]); snapshot.push_back(status);
            if (status == Ready) ++ready; if (status == Failed) ++failed;
            if (status == Failed || i%17 == 0) {
                std::vector<uint8_t> path; auto result = f.query(ids[i],start,target,path);
                AppendResult(snapshot,result); snapshot.insert(snapshot.end(),path.begin(),path.end());
                if (status == Failed) Check(result.status == Failed && result.length == 0 && result.cost == 0,
                    "resource-failed task publishes a result or stale payload");
                else Check(result.status == Found && result.length > 0, "resource-ready task loses its route");
            }
        }
        Check(ready > 0 && failed > 0, "resource fixture does not cross the retained-store quota");
        Check(f.context->memory.bytes <= f.context->memory.maximum && f.context->memory.peak <= f.context->memory.maximum,
            "hard native total memory cap was exceeded");
        if (workers == 1) baseline = snapshot;
        else Check(snapshot == baseline, "resource failure/publication depends on worker count or worker history");
        // Reentry after quota failures must not mutate the retained successes or failed states.
        Check(mma_step(f.context,int(ids.size()),workers,&computed) == Ok && computed == 0,
            "resource-failed tasks unexpectedly recompute on reentry");
        std::cout << "RESOURCE workers=" << workers << " limitMiB=768 code=" << code << " tasks=" << ids.size()
            << " ready=" << ready << " failed=" << failed << " bytes=" << f.context->memory.bytes
            << " peak=" << f.context->memory.peak << "\n";
    }
}

static void AllocationFailureReentryTests() {
    Fixture f(64,64); std::fill(f.words.begin(),f.words.end(),0xFFFFu); f.publish();
    const int start = f.node(2,30), target = f.node(61,30);
    std::vector<uint8_t> expected; auto normal = f.single(start,target,2,expected);
    Check(normal.status == Found, "reentry fixture normal single");
    // Force a real allocation failure after some bucket entries have been queued. The same
    // production Field objects must be safe to reuse after their quota becomes available again.
    for (int mode = 0; mode < 3; ++mode) {
        Memory memory; memory.maximum = 64LL << 20;
        Field field(&memory,64*64,2*MaxCost+2);
        TraversalView view(f.context->map,*f.context->mask(1),*f.context->surfaces,2);
        const i64 normalLimit = memory.maximum; memory.maximum = memory.bytes + 128;
        bool failed = false;
        Box box{0,0,63,63}; std::vector<int> starts{start};
        auto solve = [&] {
            if (mode == 0) field.solve(f.context->map,view,target,false,starts,1,0);
            else if (mode == 1) field.solveTowards(f.context->map,view,target,start,start,1,0,box);
            else field.solveToBox(f.context->map,view,target,starts,1,0,Box{2,30,2,30});
        };
        try { solve(); } catch (const Exhausted&) { failed = true; }
        Check(failed, "workspace fault did not reach an allocation boundary");
        for (const auto& bucket : field.ring) Check(bucket.empty(), "failed field retains pending bucket entries");
        memory.maximum = normalLimit; solve();
        Check(field.reached(start) && field.dist[start] == u32(normal.cost), "field reentry differs after workspace allocation failure");
    }
    // A sub-anchor is only attached to the task after the complete stored field exists.
    Fixture sub(64,64); PopulateSubanchorFixture(sub);
    const int from = sub.node(45,45), to = sub.node(58,45);
    const int task = sub.task(sub.node(4,4),2,{from}); sub.step(4);
    auto& store = sub.context->storeMemory; const i64 limit = store.maximum; store.maximum = store.bytes;
    uint8_t bytes[MaxSteps]; std::fill(std::begin(bytes),std::end(bytes),uint8_t(0xFF)); QueryResult result{};
    Check(mma_query(sub.context,task,from,to,8,bytes,MaxSteps,&result) == NoMemory,
        "sub-anchor fault did not reach the store quota");
    Check(sub.context->tasks[task]->subs.empty(), "failed sub-anchor leaves a partial stored field attached");
    store.maximum = limit;
    std::vector<uint8_t> recovered; result = sub.query(task,from,to,recovered);
    sub.verify(from,to,2,result,recovered,false);
}

static void RandomDirectedTests() {
    std::mt19937 random(0x62CFA193);
    for (int seed = 0; seed < 40; ++seed) {
        Fixture f(13, 11);
        for (int y=0;y<f.height;++y) for(int x=0;x<f.width;++x) {
            for(int d=0;d<8;++d) if(x+dx[d]>=0&&x+dx[d]<f.width&&y+dy[d]>=0&&y+dy[d]<f.height && random()%5<3) f.edge(x,y,d);
            const int permission = int(random()%3); f.surfaces[f.node(x,y)] = uint8_t(0x80 | (permission == 2 ? 3 : permission));
        }
        f.publish();
        for(int kind=0;kind<=2;++kind) for(int i=0;i<12;++i) {
            int start=int(random()%f.words.size()),target=int(random()%f.words.size()); std::vector<uint8_t> path;
            auto result=f.single(start,target,kind,path); f.verify(start,target,kind,result,path,true);
        }
    }
}

// Minimum straight pieces under the player's geometric contract: adjacent native
// headings and every node within one raster cell of that piece's endpoint segment.
// Unlike the former run-length test this accepts quieter runs of both headings,
// while still rejecting the large L of a lowest-direction-first descent.
static int StraightPieces(const std::vector<uint8_t>& path) {
    const int n = int(path.size());
    std::vector<int> x(size_t(n)+1,0), y(size_t(n)+1,0), pieces(size_t(n)+1,n+1);
    for (int i = 0; i < n; ++i) { x[i+1] = x[i]+dx[path[i]]; y[i+1] = y[i]+dy[path[i]]; }
    pieces[n] = 0;
    for (int i = n-1; i >= 0; --i) {
        const int first = path[i]; int second = -1;
        for (int j = i; j < n; ++j) {
            const int d = path[j];
            if (d != first && second < 0) {
                if (((d-first)&7) != 1 && ((first-d)&7) != 1) break;
                second = d;
            }
            if (d != first && d != second) break;
            const int vx = x[j+1]-x[i], vy = y[j+1]-y[i], length = std::max(std::abs(vx),std::abs(vy));
            bool straight = length == j+1-i;
            for (int k = i+1; straight && k <= j; ++k)
                if (std::abs((x[k]-x[i])*vy-(y[k]-y[i])*vx) > length) straight = false;
            if (straight) pieces[i] = std::min(pieces[i],1+pieces[j+1]);
        }
    }
    return pieces[0];
}

// Routes around larger obstacles stay optimal but consist of few straight pieces (TODO G10):
// before the obstacle, around its corner and after it, instead of long L-shaped sections.
static void StraightenedObstacleTests() {
    for (int shape = 0; shape < 2; ++shape) {
        Fixture f(80,80); OpenGround(f);
        if (shape == 0) for (int y = 0; y < 50; ++y) BlockGroundTile(f,40,y);          // long wall, open south end
        else for (int y = 25; y < 55; ++y) for (int x = 30; x < 50; ++x) BlockGroundTile(f,x,y); // large block
        f.publish();
        // Every pair's raster ray crosses the obstacle, so the direct ray cannot answer it.
        const int pairs[2][4][4] = {{{8,10,72,12},{8,30,70,35},{10,45,75,20},{5,20,70,44}},
                                    {{8,30,72,48},{10,40,70,30},{5,50,75,35},{12,28,68,52}}};
        std::vector<int> s, t;
        for (int i = 0; i < 4; ++i) { const int* p = pairs[shape][i]; s.push_back(f.node(p[0],p[1])); t.push_back(f.node(p[2],p[3])); }
        for (int i = 0; i < 4; ++i) {
            std::vector<uint8_t> path; auto r = f.single(s[i],t[i],0,path);
            f.verify(s[i],t[i],0,r,path,true);
            Check(r.reserved > 0, "obstacle fixture must need the exact search, not the direct ray");
            Check(StraightPieces(path) <= 3, "single route around a large obstacle keeps L-shaped sections");
        }
        for (int i = 0; i < 4; ++i) {
            const int task = f.task(t[i],0,{s[i]}); f.step(2);
            std::vector<uint8_t> path; auto r = f.query(task,s[i],t[i],path);
            f.verify(s[i],t[i],0,r,path,false);
            Check(r.cost == f.distance(s[i],t[i],0), "group route around an obstacle is not optimal");
            Check(StraightPieces(path) <= 3, "group route around a large obstacle keeps L-shaped sections");
        }
        i64 stats[StatCount] = {}; Check(mma_stats(f.context,stats,StatCount,0) == Ok, "stats");
        // The published counter now includes the optional corridor budget as well
        // as string pulling. A safe corridor fallback may cap; the exact route cost,
        // legal edges and three-piece shape remain checked above without relaxation.
        Check(stats[StraightenRoutes] > 0 && stats[StraightenSegments] > 0 &&
            stats[StraightenCapped] >= 0 && stats[StraightenCapped] <= stats[StraightenRoutes], "straightening counters");
    }
}

// Independent weighted reference: entering cost (wet ? moat : ground) + penalty * ground, capped
// at MaxCost, over the same directed edges as the core (ground-only: dry ground edges).
static i64 WeightedDistance(const Fixture& f, const std::vector<uint8_t>& penalty, int start, int target, int ground, int moat) {
    std::vector<i64> best(f.words.size(), -1);
    std::priority_queue<std::pair<i64,int>, std::vector<std::pair<i64,int>>, std::greater<>> queue;
    best[start] = 0; queue.push({0,start});
    while (!queue.empty()) {
        auto [d, v] = queue.top(); queue.pop();
        if (d != best[v]) continue; if (v == target) return d;
        for (int k = 0; k < 8; ++k) {
            const int x = v % f.width + dx[k], y = v / f.width + dy[k];
            if (x < 0 || y < 0 || x >= f.width || y >= f.height) continue;
            const bool wet = (f.words[v] & (1u << (16 + k))) != 0;
            const bool ok = moat ? (f.words[v] & (1u << k)) != 0 : ((f.words[v] & (1u << (8 + k))) != 0 && !wet);
            if (!ok) continue;
            const int u = f.node(x,y);
            const i64 step = std::min<i64>(MaxCost, (moat && wet ? moat : ground) + i64(penalty[u]) * ground);
            if (best[u] < 0 || d + step < best[u]) { best[u] = d + step; queue.push({best[u],u}); }
        }
    }
    return -1;
}

// Straightening compares the exact cost of each replaced section, so moat costs, entering penalties
// and directed edges never make a route more expensive than the weighted optimum.
static void RandomWeightedTests() {
    std::mt19937 random(0x51A1647);
    for (int seed = 0; seed < 30; ++seed) {
        Fixture f(24, 20);
        std::vector<uint8_t> penalty(f.words.size(), 0);
        for (int y = 0; y < f.height; ++y) for (int x = 0; x < f.width; ++x) {
            const bool wetTile = random() % 6 == 0;
            for (int d = 0; d < 8; ++d) {
                const int nx = x + dx[d], ny = y + dy[d];
                if (nx < 0 || ny < 0 || nx >= f.width || ny >= f.height || random() % 10 < 2) continue;
                f.edge(x,y,d,wetTile && random() % 2 == 0);
            }
            if (random() % 5 == 0) penalty[f.node(x,y)] = uint8_t(1 + random() % 3);
        }
        f.publish();
        std::vector<int> nodes(f.words.size()); for (size_t i = 0; i < nodes.size(); ++i) nodes[i] = int(i);
        // Phase 0: random penalties; 1: half of them cleared again; 2: none left. The flat-cost
        // shortcut of the straightening applies only when no penalized node remains (phase 2).
        for (int phase = 0; phase < 3; ++phase) {
        if (phase == 1) for (auto& p : penalty) if (random() % 2 == 0) p = 0;
        if (phase == 2) std::fill(penalty.begin(), penalty.end(), uint8_t(0));
        int changed = 0; Check(mma_penalty_update(f.context,1,nodes.data(),penalty.data(),int(nodes.size()),&changed) == Ok, "weighted penalty ABI");
        Check((f.context->mask(1)->penalized == 0) == (phase == 2 || std::all_of(penalty.begin(), penalty.end(), [](uint8_t p) { return p == 0; })),
            "penalized node count disagrees with the written penalties");
        for (int i = 0; i < 16; ++i) {
            const int start = int(random() % f.words.size()), target = int(random() % f.words.size());
            const int ground = 1 + int(random() % 3), moat = (i & 1) ? ground + 1 + int(random() % 5) : 0;
            uint8_t bytes[MaxSteps]; QueryResult r{};
            Check(mma_single_route(f.context,1,target,ground,moat,0,start,bytes,MaxSteps,&r) == Ok, "weighted single ABI");
            const i64 expected = WeightedDistance(f,penalty,start,target,ground,moat);
            Check((r.status == Found) == (expected >= 0), "weighted reachability");
            if (r.status != Found) continue;
            i64 cost = 0; int v = start;
            for (int k = 0; k < r.length; ++k) {
                const int d = bytes[k]; const bool wet = (f.words[v] & (1u << (16 + d))) != 0;
                Check(moat ? (f.words[v] & (1u << d)) != 0 : ((f.words[v] & (1u << (8 + d))) != 0 && !wet), "weighted route edge");
                v += dy[d] * f.width + dx[d];
                cost += std::min<i64>(MaxCost, (moat && wet ? moat : ground) + i64(penalty[v]) * ground);
            }
            Check(v == target, "weighted route endpoint");
            Check(cost == expected && r.cost == expected, "straightened weighted route is not optimal");
        }
        }
    }
}

int main(int argc, char** argv) {
    try { if (argc == 2 && std::string(argv[1]) == "--memory-probe") { MemoryWorkerProbe(); ProductionMemoryWorkerProbe(); return 0; }
        Check(mma_contract() == 0x04000012, "private ABI"); CorridorTests(); DirectGroundRayTests(); QuietGroundSlopeTests(); QuietRunQuotientTests(); SelectedEdgePermissionTests();
        QuietGroundFallbackAndBridgeTests(); QuietWeightedRayTests(); QuietLongRouteAccountingTests(); DirectGroundObstaclesAndGuards();
        NaturalFrozenRegressionTests(); NaturalRankedWholeRouteTests(); NaturalCostBudgetTests(); NaturalGeometryThresholdTests(); NaturalCutProofTests(); NaturalWorkFallbackTests();
        SubanchorAndBatchTests(); RandomDirectedTests(); StraightenedObstacleTests(); RandomWeightedTests();
        MemoryWorkerProbe(); ProductionMemoryWorkerProbe(); AllocationFailureReentryTests();
        std::cout << "PASS: " << assertions << " production native surface/core assertions.\n"; return 0; }
    catch (const std::exception& error) { std::cerr << "FAIL: " << error.what() << " after " << assertions << " assertions.\n"; return 1; }
}
