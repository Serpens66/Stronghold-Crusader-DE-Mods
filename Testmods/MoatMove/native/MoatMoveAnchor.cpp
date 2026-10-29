// MoatMove "anchor" mode: native route core. Original implementation; it never calls
// into the game, reads no game memory and owns all of its buffers. Derived from the
// offline GroupPathProbe variant "B per unit" (BetterPathfinding.md section 18.3).
//
// Contract summary (mirrored by the managed adapter):
// - Per player one traversal mask, written by the managed side from the same predicate
//   as MoatMove Fast. Words use the FastTraversalCache layout: bits 0..7 = friendly
//   allowed directions, bits 16..23 = wet (moat) directions. Masks are written in
//   chunks; a mask is usable once every node was written since mask_begin.
// - A task is one command: anchor, cost profile and the start set. A moat cost of 0 is
//   a ground-only profile: wet edges are never used (units that cannot enter moats).
//   mma_step computes
//   pending tasks with a fork-join over worker threads. Each task writes only itself,
//   so results never depend on thread count or scheduling.
// - Queries answer one unit (start, slot) in caller order. Sub-anchor fields for far
//   slots are cached per task, so the caller must query in a deterministic order.
// - A task becomes Stale when its player's mask changed after the task was computed.
// - Routes longer than 2,000 steps are never returned as routes.
#include <algorithm>
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstdint>
#include <functional>
#include <intrin.h>
#include <map>
#include <memory>
#include <mutex>
#include <new>
#include <thread>
#include <vector>
#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

namespace mma {
using i64 = int64_t;
using u64 = uint64_t;
using u32 = uint32_t;
constexpr int dx[8] = {0,1,1,1,0,-1,-1,-1}, dy[8] = {-1,-1,0,1,1,1,0,-1};
// 48 sub-anchors (about 2.5 MiB each on 800x800) serve wide formations around blocked anchors.
constexpr int MaxSteps = 2000, MaxPlayers = 8, MaxSubAnchors = 48, MaxCost = 255;
constexpr u32 Unset = 0xFFFFFFFFu;
struct Exhausted {};

// Thread-safe counted budget; worker fields allocate concurrently.
struct Memory {
    std::mutex lock; i64 maximum = 0, bytes = 0, peak = 0, calls = 0;
    void take(size_t n) { std::lock_guard<std::mutex> g(lock); if (n > size_t(maximum - bytes)) throw Exhausted(); bytes += i64(n); peak = std::max(peak, bytes); ++calls; }
    void give(size_t n) { std::lock_guard<std::mutex> g(lock); bytes -= i64(n); }
};
template<class T> struct Alloc {
    using value_type = T; using propagate_on_container_move_assignment = std::true_type;
    Memory* m;
    explicit Alloc(Memory* p = nullptr) : m(p) {}
    template<class U> Alloc(const Alloc<U>& a) : m(a.m) {}
    T* allocate(size_t n) { if (n > (size_t(-1) - 32) / sizeof(T)) throw Exhausted(); m->take(n * sizeof(T) + 32); try { return static_cast<T*>(::operator new(n * sizeof(T))); } catch (...) { m->give(n * sizeof(T) + 32); throw; } }
    void deallocate(T* p, size_t n) { ::operator delete(p); m->give(n * sizeof(T) + 32); }
    template<class U> bool operator==(const Alloc<U>& a) const { return m == a.m; }
    template<class U> bool operator!=(const Alloc<U>& a) const { return m != a.m; }
};
template<class T> using Vec = std::vector<T, Alloc<T>>;

// Private ABI, mirrored in the managed adapter.
struct QueryResult { int status, ground, moat, length, flags, reserved; i64 cost; };
static_assert(sizeof(QueryResult) == 32, "anchor core ABI");
enum Status { Found = 0, Unreachable = 1, OverLimit = 2, Pending = 3, Stale = 4, Invalid = 5, Failed = 6 };
constexpr int Ready = Found; // task status: field computed and current
enum Flags { ViaAnchor = 1, SubAnchor = 2, NewSubAnchor = 4, Uncached = 8 };
enum Code { Ok = 0, NoMemory = 2, BadArgument = 3, Internal = 4 };
// AnchorNoStart..AnchorTooFar: why an anchor (main or sub) could not answer a query.
enum StatIndex { TasksComputed, TasksFailed, SettleEvents, Entries, Queries, ViaChosen, SubAnchors, SubCapped, StepNs, QueryNs, SubNs, MaskWrites, MaskChanges, PeakBytes, Allocations, Workers,
    AnchorNoStart, AnchorNoEntry, AnchorUnreached, AnchorTooFar, StatCount };

struct Box { int x0 = 0, y0 = 0, x1 = -1, y1 = -1; };
static int Ctz(u32 m) { unsigned long i; _BitScanForward(&i, m); return int(i); }
using Clock = std::chrono::steady_clock;
static i64 Ns(Clock::time_point a) { return std::chrono::duration_cast<std::chrono::nanoseconds>(Clock::now() - a).count(); }

struct Map {
    int width = 0, height = 0, n = 0, off[8] = {};
    bool inside(const Box& b, int node) const { int x = node % width, y = node / width; return x >= b.x0 && x <= b.x1 && y >= b.y0 && y <= b.y1; }
    int chebyshev(int a, int b) const { return std::max(std::abs(a % width - b % width), std::abs(a / width - b / width)); }
};

// Directed adjacency of one player: outgoing and incoming direction bits, wet bits.
struct Mask {
    Vec<uint8_t> out, outWet, in, inWet, written; i64 writtenCount = 0; u64 revision = 1;
    explicit Mask(Memory* m, int n) : out(size_t(n), 0, Alloc<uint8_t>(m)), outWet(size_t(n), 0, Alloc<uint8_t>(m)), in(size_t(n), 0, Alloc<uint8_t>(m)),
        inWet(size_t(n), 0, Alloc<uint8_t>(m)), written(size_t(n), 0, Alloc<uint8_t>(m)) {}
    bool ready(int n) const { return writtenCount == n; }
    // Returns true when the stored edges of the node changed.
    bool set(const Map& map, int node, u32 word) {
        int x = node % map.width, y = node / map.width; uint8_t o = 0, w = 0;
        for (int d = 0; d < 8; ++d) {
            int nx = x + dx[d], ny = y + dy[d];
            if ((unsigned)nx >= unsigned(map.width) || (unsigned)ny >= unsigned(map.height) || !(word & (1u << d))) continue;
            o |= uint8_t(1u << d); if (word & (1u << (16 + d))) w |= uint8_t(1u << d);
        }
        if (!written[node]) { written[node] = 1; ++writtenCount; }
        if (o == out[node] && w == outWet[node]) return false;
        for (int d = 0; d < 8; ++d) {
            int nx = x + dx[d], ny = y + dy[d];
            if ((unsigned)nx >= unsigned(map.width) || (unsigned)ny >= unsigned(map.height)) continue;
            int t = ny * map.width + nx; uint8_t bit = uint8_t(1u << d);
            in[t] = uint8_t((in[t] & ~bit) | (o & bit)); inWet[t] = uint8_t((inWet[t] & ~bit) | (w & bit));
        }
        out[node] = o; outWet[node] = w; return true;
    }
};

// One-lane Dial search workspace. Buckets pop in nondecreasing order, so a node's
// first pop at its current tentative distance is final.
struct Field {
    Vec<u32> gen, dist, needGen; Vec<uint8_t> done; Vec<Vec<int>> ring;
    u32 generation = 0; i64 settles = 0, entries = 0; bool exhausted = false;
    Field(Memory* m, int n, int buckets = MaxCost + 1) : gen(size_t(n), 0u, Alloc<u32>(m)), dist(size_t(n), 0u, Alloc<u32>(m)), needGen(size_t(n), 0u, Alloc<u32>(m)),
        done(size_t(n), 0, Alloc<uint8_t>(m)), ring(Alloc<Vec<int>>(m)) { for (int i = 0; i < buckets; ++i) ring.emplace_back(Alloc<int>(m)); }
    bool reached(int v) const { return gen[v] == generation && done[v]; }
    // Touched in the last search: dist is an upper bound, exact for settled nodes.
    bool touched(int v) const { return gen[v] == generation; }
    // Backward A* from source towards two targets inside a box, stopping once both are settled.
    // h = cheapest step cost x Chebyshev distance to the nearer target is consistent, so settled
    // distances are exact. Keys g + h of queued nodes stay within cost + cheapest step of the
    // current key, which needs more than 2 x MaxCost buckets (local field only). A descent over
    // touched nodes is exact too: a neighbour whose bound plus the step cost equals the exact
    // distance of the current node has that bound as its exact distance.
    void solveTowards(const Map& map, const Mask& mask, int source, int a, int b, int ground, int moat, const Box& box) {
        if (++generation == 0) { std::fill(gen.begin(), gen.end(), 0u); std::fill(needGen.begin(), needGen.end(), 0u); generation = 1; }
        const int size = int(ring.size()), w = map.width; const u32 step = u32(moat ? std::min(ground, moat) : ground);
        const int ax = a % w, ay = a / w, bx = b % w, by = b / w;
        auto h = [&](int x, int y) { return step * u32(std::min(std::max(std::abs(x - ax), std::abs(y - ay)), std::max(std::abs(x - bx), std::abs(y - by)))); };
        i64 pending = 0, remaining = 0;
        for (int t : {a, b}) if (needGen[t] != generation) { needGen[t] = generation; ++remaining; }
        auto relax = [&](int u, int x, int y, u32 value) {
            if (x < box.x0 || x > box.x1 || y < box.y0 || y > box.y1) return;
            if (gen[u] != generation) { gen[u] = generation; done[u] = 0; }
            else if (done[u] || dist[u] <= value) return;
            dist[u] = value; ring[(value + h(x, y)) % size].push_back(u); ++pending; ++entries;
        };
        const int sx = source % w, sy = source / w;
        relax(source, sx, sy, 0);
        for (u32 key = h(sx, sy); pending > 0 && remaining > 0; ++key) {
            Vec<int>& list = ring[key % size];
            for (size_t i = 0; i < list.size() && remaining > 0; ++i) {
                int v = list[i]; --pending;
                if (done[v]) continue;
                const int vx = v % w, vy = v / w; const u32 g = dist[v];
                if (g + h(vx, vy) != key) continue;
                done[v] = 1; ++settles;
                if (needGen[v] == generation) { needGen[v] = 0; --remaining; }
                // Incoming edge u -> v in direction d: u lies at v - (dx, dy).
                for (u32 m = moat ? u32(mask.in[v]) : u32(mask.in[v] & ~mask.inWet[v] & 0xFF); m; m &= m - 1) {
                    int d = Ctz(m); relax(v - map.off[d], vx - dx[d], vy - dy[d], g + u32((mask.inWet[v] >> d) & 1 ? moat : ground));
                }
            }
            list.clear();
        }
        exhausted = remaining > 0;
        for (auto& l : ring) l.clear();
    }
    void solve(const Map& map, const Mask& mask, int source, bool forward, const std::vector<int>& targets, int ground, int moat, const Box* box = nullptr) {
        if (++generation == 0) { std::fill(gen.begin(), gen.end(), 0u); std::fill(needGen.begin(), needGen.end(), 0u); generation = 1; }
        const int size = int(ring.size()); i64 pending = 0, remaining = 0;
        for (int t : targets) if (needGen[t] != generation) { needGen[t] = generation; ++remaining; }
        auto relax = [&](int u, u32 value) {
            if (box && !map.inside(*box, u)) return;
            if (gen[u] != generation) { gen[u] = generation; done[u] = 0; }
            else if (done[u] || dist[u] <= value) return;
            dist[u] = value; ring[value % size].push_back(u); ++pending; ++entries;
        };
        relax(source, 0);
        for (u32 bucket = 0; pending > 0 && remaining > 0; ++bucket) {
            Vec<int>& list = ring[bucket % size];
            for (size_t i = 0; i < list.size() && remaining > 0; ++i) {
                int v = list[i]; --pending;
                if (done[v] || dist[v] != bucket) continue;
                done[v] = 1; ++settles;
                if (needGen[v] == generation) { needGen[v] = 0; --remaining; }
                // moat == 0: ground-only profile, wet (moat) edges are not traversable.
                if (forward) { for (u32 m = moat ? u32(mask.out[v]) : u32(mask.out[v] & ~mask.outWet[v] & 0xFF); m; m &= m - 1) { int d = Ctz(m); relax(v + map.off[d], bucket + u32((mask.outWet[v] >> d) & 1 ? moat : ground)); } }
                else { for (u32 m = moat ? u32(mask.in[v]) : u32(mask.in[v] & ~mask.inWet[v] & 0xFF); m; m &= m - 1) { int d = Ctz(m); relax(v - map.off[d], bucket + u32((mask.inWet[v] >> d) & 1 ? moat : ground)); } }
            }
            list.clear();
        }
        // Exhausted: every node that reaches the source is settled (some target was not).
        exhausted = remaining > 0;
        for (auto& l : ring) l.clear();
    }
};

// Compact backward field to an anchor: exact cost per settled node, Unset otherwise.
struct Stored {
    int anchor = -1; Vec<u32> dist;
    explicit Stored(Memory* m) : dist(Alloc<u32>(m)) {}
    void take(const Map& map, const Field& f, int a) { anchor = a; dist.assign(size_t(map.n), Unset); for (int v = 0; v < map.n; ++v) if (f.reached(v)) dist[v] = f.dist[v]; }
};
// Deterministic descent (lowest direction first) along a backward cost function. The stop
// test also gets the node's coordinates, tracked per step instead of divided per test.
template<class Dist, class Stop> static int Descend(const Map& map, const Mask& mask, Dist distance, int node, int ground, int moat, std::vector<uint8_t>& path, int& g, int& w, Stop stop) {
    int x = node % map.width, y = node / map.width;
    while (!stop(node, x, y)) {
        u32 here = distance(node); int next = -1;
        for (u32 m = moat ? u32(mask.out[node]) : u32(mask.out[node] & ~mask.outWet[node] & 0xFF); m && next < 0; m &= m - 1) {
            int d = Ctz(m), u = node + map.off[d]; u32 there = distance(u); if (there == Unset) continue;
            bool wet = ((mask.outWet[node] >> d) & 1) != 0;
            if (there + u32(wet ? moat : ground) != here) continue;
            next = u; x += dx[d]; y += dy[d]; path.push_back(uint8_t(d)); if (wet) ++w; else ++g;
        }
        if (next < 0 || int(path.size()) > MaxSteps * 4) return -1;
        node = next;
    }
    return node;
}

struct Task {
    int id = 0, player = 0, anchor = 0, ground = 1, moat = 1, status = Pending; u64 revision = 0; bool exhausted = false;
    std::vector<int> starts; std::unique_ptr<Stored> main; std::vector<std::unique_ptr<Stored>> subs;
};

// Persistent fork-join pool: workers sleep between steps; the caller is worker 0.
struct Pool {
    std::vector<std::thread> threads; std::mutex lock; std::condition_variable wake, finished;
    std::function<void(int)> job; u64 round = 0; int busy = 0; bool stopping = false;
    void resize(int workers) {
        if (int(threads.size()) == workers - 1) return;
        stop();
        stopping = false;
        u64 current; { std::lock_guard<std::mutex> g(lock); current = round; }
        for (int w = 1; w < workers; ++w) threads.emplace_back([this, w, current] { loop(w, current); });
    }
    void loop(int worker, u64 seen) {
        for (;;) {
            std::function<void(int)> work;
            { std::unique_lock<std::mutex> g(lock); wake.wait(g, [&] { return stopping || round != seen; }); if (stopping) return; seen = round; work = job; }
            work(worker);
            { std::lock_guard<std::mutex> g(lock); if (--busy == 0) finished.notify_all(); }
        }
    }
    void run(const std::function<void(int)>& work) {
        { std::lock_guard<std::mutex> g(lock); job = work; busy = int(threads.size()); ++round; }
        wake.notify_all();
        work(0);
        std::unique_lock<std::mutex> g(lock); finished.wait(g, [&] { return busy == 0; });
    }
    void stop() {
        { std::lock_guard<std::mutex> g(lock); stopping = true; }
        wake.notify_all();
        for (auto& t : threads) t.join();
        threads.clear();
    }
    ~Pool() { stop(); }
};

struct Context {
    Memory memory; Map map;
    std::unique_ptr<Mask> masks[MaxPlayers + 1];
    std::map<int, std::unique_ptr<Task>> tasks; int nextId = 1;
    std::vector<std::unique_ptr<Field>> fields; std::unique_ptr<Field> local;
    // Query scratch (queries run on the simulation thread only).
    std::vector<uint8_t> path, rest; std::vector<int> targets;
    Pool pool; i64 stats[StatCount] = {};
    Context(int w, int h, i64 limit) {
        memory.maximum = limit; map.width = w; map.height = h; map.n = w * h;
        for (int d = 0; d < 8; ++d) map.off[d] = dy[d] * w + dx[d];
        local = std::make_unique<Field>(&memory, map.n, 2 * MaxCost + 2);
    }
    ~Context() { pool.stop(); }
    Mask* mask(int player) { return player >= 1 && player <= MaxPlayers ? masks[player].get() : nullptr; }
    bool fresh(const Task& t) { Mask* m = mask(t.player); return m && m->ready(map.n) && t.revision == m->revision; }
};

static bool ValidNode(const Context& c, int node) { return node >= 0 && node < c.map.n; }

// Answers one unit. Anchors are tried in creation order (main anchor first). An anchor is
// usable when the slot is graph-near to it inside the anchor-slot box; otherwise a field
// at the slot is created once (sub-anchor) and reused by later queries of this task.
static int Query(Context& c, Task& t, int start, int slot, int margin, uint8_t* bytes, int capacity, QueryResult& r) {
    const Map& map = c.map; const Mask& mask = *c.mask(t.player); Field& local = *c.local;
    std::vector<uint8_t>& path = c.path; std::vector<uint8_t>& rest = c.rest;
    auto finish = [&](int g, int w, int flags) {
        r.flags = flags; r.ground = g; r.moat = w; r.cost = i64(g) * t.ground + i64(w) * t.moat; r.length = int(path.size());
        if (int(path.size()) > MaxSteps || int(path.size()) > capacity) { r.status = OverLimit; r.length = 0; return; }
        std::copy(path.begin(), path.end(), bytes); r.status = Found;
    };
    // The main field ran out before reaching every start: a start it never settled cannot reach
    // the anchor, so it cannot reach any slot that does (directed graph). Answer that exactly
    // instead of building a full-map sub-anchor per query (AI units behind walls, 29.10.2026).
    if (t.exhausted && t.main->dist[start] == Unset && t.main->dist[slot] != Unset) { r.status = Unreachable; r.flags = 0; return Ok; }
    for (size_t a = 0; a <= t.subs.size(); ++a) {
        const Stored& s = a == 0 ? *t.main : *t.subs[a - 1];
        if (s.dist[start] == Unset) { ++c.stats[AnchorNoStart]; continue; }
        Box box;
        box.x0 = std::max(0, std::min(s.anchor % map.width, slot % map.width) - margin); box.x1 = std::min(map.width - 1, std::max(s.anchor % map.width, slot % map.width) + margin);
        box.y0 = std::max(0, std::min(s.anchor / map.width, slot / map.width) - margin); box.y1 = std::min(map.height - 1, std::max(s.anchor / map.width, slot / map.width) + margin);
        auto stored = [&](int v) { return s.dist[v]; };
        path.clear(); int gH = 0, wH = 0;
        int entry = Descend(map, mask, stored, start, t.ground, t.moat, path, gH, wH, [&](int, int x, int y) { return x >= box.x0 && x <= box.x1 && y >= box.y0 && y <= box.y1; });
        if (entry < 0) { ++c.stats[AnchorNoEntry]; continue; }
        local.solveTowards(map, mask, slot, s.anchor, entry, t.ground, t.moat, box);
        if (!local.reached(s.anchor)) { ++c.stats[AnchorUnreached]; continue; }
        if (local.dist[s.anchor] > u32(t.ground) * u32(2 * map.chebyshev(s.anchor, slot) + 8)) { ++c.stats[AnchorTooFar]; continue; }
        auto localDistance = [&](int v) { return local.touched(v) ? local.dist[v] : Unset; };
        i64 boxCost = local.reached(entry) ? i64(gH) * t.ground + i64(wH) * t.moat + local.dist[entry] : -1;
        i64 viaCost = i64(s.dist[start]) + local.dist[s.anchor];
        int g = gH, w = wH, flags = a > 0 ? SubAnchor : 0;
        if (boxCost >= 0 && boxCost <= viaCost) {
            rest.clear(); if (Descend(map, mask, localDistance, entry, t.ground, t.moat, rest, g, w, [&](int v, int, int) { return v == slot; }) < 0) continue;
        } else {
            ++c.stats[ViaChosen]; flags |= ViaAnchor;
            if (Descend(map, mask, stored, entry, t.ground, t.moat, path, g, w, [&](int v, int, int) { return v == s.anchor; }) < 0) continue;
            rest.clear(); if (Descend(map, mask, localDistance, s.anchor, t.ground, t.moat, rest, g, w, [&](int v, int, int) { return v == slot; }) < 0) continue;
        }
        path.insert(path.end(), rest.begin(), rest.end());
        finish(g, w, flags); return Ok;
    }
    // No usable anchor: an exact field at the slot, cached as a sub-anchor while allowed. With
    // every sub-anchor slot used the query is not answered (the caller keeps Vanilla) rather
    // than building an uncached full-map field per query.
    if (int(t.subs.size()) >= MaxSubAnchors) { r.status = OverLimit; r.flags = Uncached; ++c.stats[SubCapped]; return Ok; }
    auto t0 = Clock::now();
    // An exhausted main field splits the starts: those it settled reach the anchor, the others
    // cannot. A sub-anchor targets only starts of the querying start's class; the other class
    // usually lies in another component and would force a search over this whole component.
    // Answers stay exact: a later query uses a sub-anchor only where it settled that start.
    std::vector<int>& targets = c.targets; targets.clear();
    const bool startInMain = t.main->dist[start] != Unset;
    for (int s : t.starts) if (!t.exhausted || (t.main->dist[s] != Unset) == startInMain) targets.push_back(s);
    targets.push_back(start);
    local.solve(map, mask, slot, false, targets, t.ground, t.moat);
    int flags = NewSubAnchor | SubAnchor;
    t.subs.push_back(std::make_unique<Stored>(&c.memory)); t.subs.back()->take(map, local, slot);
    const Stored* s = t.subs.back().get(); ++c.stats[SubAnchors];
    c.stats[SubNs] += Ns(t0);
    path.clear(); int g = 0, w = 0;
    if (s->dist[start] == Unset) { r.status = Unreachable; r.flags = flags; return Ok; }
    auto stored = [&](int v) { return s->dist[v]; };
    if (Descend(map, mask, stored, start, t.ground, t.moat, path, g, w, [&](int v, int, int) { return v == slot; }) < 0) { r.status = OverLimit; r.flags = flags; return Ok; }
    finish(g, w, flags); return Ok;
}

template<class F> static int Guard(F body) {
    try { return body(); }
    catch (Exhausted&) { return NoMemory; } catch (std::bad_alloc&) { return NoMemory; } catch (...) { return Internal; }
}
}

using namespace mma;
extern "C" {
__declspec(dllexport) int mma_contract() { return 0x04000005; }
// Physical cores minus two (Unity main thread plus simulation/render), within 1..8.
__declspec(dllexport) int mma_recommended_threads() {
    DWORD length = 0; GetLogicalProcessorInformationEx(RelationProcessorCore, nullptr, &length);
    int cores = 0;
    if (length != 0) {
        std::vector<uint8_t> buffer(length);
        if (GetLogicalProcessorInformationEx(RelationProcessorCore, reinterpret_cast<PSYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX>(buffer.data()), &length))
            for (DWORD at = 0; at < length;) { auto* item = reinterpret_cast<PSYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX>(buffer.data() + at); if (item->Relationship == RelationProcessorCore) ++cores; at += item->Size; }
    }
    if (cores <= 0) cores = int(std::max(1u, std::thread::hardware_concurrency()));
    return std::max(1, std::min(8, cores - 2));
}
__declspec(dllexport) int mma_create(int width, int height, i64 limit, Context** out) {
    if (!out) return BadArgument; *out = nullptr;
    if (width < 1 || height < 1 || i64(width) * height > 640000 || limit < (64LL << 20)) return BadArgument;
    return Guard([&] { *out = new Context(width, height, limit); return Ok; });
}
__declspec(dllexport) void mma_destroy(Context* c) { delete c; }
// Starts a complete rewrite of one player's mask; tasks of that player become stale.
__declspec(dllexport) int mma_mask_begin(Context* c, int player) {
    if (!c || player < 1 || player > MaxPlayers) return BadArgument;
    return Guard([&] {
        u64 revision = c->masks[player] ? c->masks[player]->revision + 1 : 1;
        c->masks[player].reset(); c->masks[player] = std::make_unique<Mask>(&c->memory, c->map.n); c->masks[player]->revision = revision;
        return Ok;
    });
}
// Writes consecutive nodes [first, first + count) in FastTraversalCache word layout.
__declspec(dllexport) int mma_mask_write(Context* c, int player, int first, int count, const u32* words, int* changed) {
    if (!c || !words || !c->mask(player) || first < 0 || count < 0 || first > c->map.n - count) return BadArgument;
    Mask& m = *c->mask(player); bool wasReady = m.ready(c->map.n); int n = 0;
    for (int i = 0; i < count; ++i) if (m.set(c->map, first + i, words[i])) ++n;
    c->stats[MaskWrites] += count; c->stats[MaskChanges] += n;
    if (wasReady && n) ++m.revision;
    if (changed) *changed = n; return Ok;
}
// Rewrites arbitrary nodes (incremental update after terrain or access changes).
__declspec(dllexport) int mma_mask_update(Context* c, int player, const int* nodes, const u32* words, int count, int* changed) {
    if (!c || !nodes || !words || !c->mask(player) || count < 0) return BadArgument;
    for (int i = 0; i < count; ++i) if (!ValidNode(*c, nodes[i])) return BadArgument;
    Mask& m = *c->mask(player); bool wasReady = m.ready(c->map.n); int n = 0;
    for (int i = 0; i < count; ++i) if (m.set(c->map, nodes[i], words[i])) ++n;
    c->stats[MaskWrites] += count; c->stats[MaskChanges] += n;
    if (wasReady && n) ++m.revision;
    if (changed) *changed = n; return Ok;
}
__declspec(dllexport) int mma_mask_state(Context* c, int player, i64* written, u64* revision) {
    if (!c || player < 1 || player > MaxPlayers) return BadArgument;
    Mask* m = c->mask(player);
    if (written) *written = m ? m->writtenCount : 0; if (revision) *revision = m ? m->revision : 0;
    return m && m->ready(c->map.n) ? Ok : Pending;
}
__declspec(dllexport) int mma_task_create(Context* c, int player, int anchor, int ground, int moat, const int* starts, int count, int* id) {
    if (!c || !id || player < 1 || player > MaxPlayers || !ValidNode(*c, anchor) || ground < 1 || moat < 0 || ground > MaxCost || moat > MaxCost || count < 0 || (count && !starts)) return BadArgument;
    for (int i = 0; i < count; ++i) if (!ValidNode(*c, starts[i])) return BadArgument;
    return Guard([&] {
        auto t = std::make_unique<Task>(); t->id = c->nextId++; t->player = player; t->anchor = anchor; t->ground = ground; t->moat = moat;
        t->starts.assign(starts, starts + count); *id = t->id; c->tasks[t->id] = std::move(t); return Ok;
    });
}
// Returns the task to Pending (after a stale report); its cached fields are dropped.
__declspec(dllexport) int mma_task_restart(Context* c, int id) {
    if (!c || !c->tasks.count(id)) return BadArgument;
    Task& t = *c->tasks[id]; t.status = Pending; t.main.reset(); t.subs.clear(); return Ok;
}
__declspec(dllexport) int mma_task_release(Context* c, int id) { if (!c || !c->tasks.erase(id)) return BadArgument; return Ok; }
__declspec(dllexport) int mma_task_status(Context* c, int id) {
    if (!c || !c->tasks.count(id)) return Invalid;
    Task& t = *c->tasks[id];
    if (t.status == Ready && !c->fresh(t)) return Stale;
    return t.status;
}
// Computes up to maxTasks pending tasks (lowest IDs first) whose mask is ready.
__declspec(dllexport) int mma_step(Context* c, int maxTasks, int threads, int* computed) {
    if (!c || maxTasks < 0 || threads < 1 || threads > 64) return BadArgument;
    return Guard([&] {
        auto t0 = Clock::now();
        std::vector<Task*> work;
        for (auto& entry : c->tasks) {
            Task& t = *entry.second; Mask* m = c->mask(t.player);
            if (int(work.size()) >= maxTasks) break;
            if (t.status == Pending && m && m->ready(c->map.n)) work.push_back(&t);
        }
        // A fixed worker count keeps one persistent pool; idle workers find no task.
        int workers = threads;
        while (int(c->fields.size()) < workers) c->fields.push_back(std::make_unique<Field>(&c->memory, c->map.n));
        c->pool.resize(workers); c->stats[Workers] = workers;
        std::atomic<int> next{0};
        // Allocation failures stay with their task; the task is reported Failed.
        auto job = [&](int worker) {
            Field& f = *c->fields[worker];
            for (;;) {
                int i = next.fetch_add(1); if (i >= int(work.size())) break;
                Task& t = *work[i];
                try {
                    f.solve(c->map, *c->mask(t.player), t.anchor, false, t.starts, t.ground, t.moat);
                    auto s = std::make_unique<Stored>(&c->memory); s->take(c->map, f, t.anchor);
                    t.main = std::move(s); t.subs.clear(); t.exhausted = f.exhausted; t.revision = c->mask(t.player)->revision; t.status = Ready;
                }
                catch (...) { t.main.reset(); t.status = Failed; }
            }
        };
        if (!work.empty()) c->pool.run(job);
        for (Task* t : work) { if (t->status == Ready) ++c->stats[TasksComputed]; else ++c->stats[TasksFailed]; }
        for (auto& f : c->fields) { c->stats[SettleEvents] += f->settles; c->stats[Entries] += f->entries; f->settles = f->entries = 0; }
        if (computed) *computed = int(work.size());
        c->stats[StepNs] += Ns(t0);
        return Ok;
    });
}
__declspec(dllexport) int mma_query(Context* c, int id, int start, int slot, int margin, uint8_t* bytes, int capacity, QueryResult* result) {
    if (!c || !result || !bytes || capacity < 0 || margin < 0) return BadArgument;
    *result = QueryResult{}; result->status = Invalid;
    if (!c->tasks.count(id) || !ValidNode(*c, start) || !ValidNode(*c, slot)) return BadArgument;
    Task& t = *c->tasks[id];
    if (t.status != Ready) { result->status = t.status; return Ok; }
    if (!c->fresh(t)) { result->status = Stale; return Ok; }
    return Guard([&] {
        auto t0 = Clock::now(); ++c->stats[Queries];
        int code = Query(*c, t, start, slot, margin, bytes, capacity, *result);
        c->stats[SettleEvents] += c->local->settles; c->stats[Entries] += c->local->entries; c->local->settles = c->local->entries = 0;
        c->stats[QueryNs] += Ns(t0); return code;
    });
}
__declspec(dllexport) int mma_stats(Context* c, i64* data, int count, int reset) {
    if (!c || !data || count < StatCount) return BadArgument;
    c->stats[PeakBytes] = c->memory.peak; c->stats[Allocations] = c->memory.calls;
    std::copy(c->stats, c->stats + StatCount, data);
    if (reset) { for (auto& s : c->stats) s = 0; std::lock_guard<std::mutex> g(c->memory.lock); c->memory.peak = c->memory.bytes; }
    return Ok;
}
}
