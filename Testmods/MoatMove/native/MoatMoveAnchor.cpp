// MoatMove "anchor" mode: native route core. Original implementation; it never calls
// into the game, reads no game memory and owns all of its buffers. Derived from the
// offline GroupPathProbe variant "B per unit" (BetterPathfinding.md section 18.3).
//
// Contract summary (mirrored by the managed adapter):
// - Per player one traversal mask, written by the managed side from the same predicate
//   as MoatMove Fast. Words use the FastTraversalCache layout: bits 0..7 = friendly
//   allowed directions (moat mode), bits 8..15 = ground directions, bits 16..23 = wet
//   (moat) directions. Masks are written in chunks; a mask is usable once every node
//   was written since mask_begin.
// - A task is one command: anchor, cost profile and the start set. A moat cost of 0 is
//   a ground-only profile on the ground directions (units that cannot enter moats and
//   ground decisions); other profiles use the friendly directions.
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
constexpr int MaxSteps = 2000, MaxPlayers = 8, MaxSubAnchors = 48, MaxCost = 255, SubSingleBudget = 4096, StartFilterBudget = 16;
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
// Internal only: a batch query that needs a new sub-anchor is answered in the ordered pass.
constexpr int Deferred = 7;
constexpr int Ready = Found; // task status: field computed and current
enum Flags { ViaAnchor = 1, SubAnchor = 2, NewSubAnchor = 4, Uncached = 8 };
enum Code { Ok = 0, NoMemory = 2, BadArgument = 3, Internal = 4 };
// AnchorNoStart..AnchorTooFar: why an anchor (main or sub) could not answer a query.
enum StatIndex { TasksComputed, TasksFailed, SettleEvents, Entries, Queries, ViaChosen, SubAnchors, SubCapped, StepNs, QueryNs, SubNs, MaskWrites, MaskChanges, PeakBytes, Allocations, Workers,
    AnchorNoStart, AnchorNoEntry, AnchorUnreached, AnchorTooFar, FieldReuses, SingleQueries, SingleSettles, SingleNs, SubSingles, StartsEnclosed, StatCount };

struct Box { int x0 = 0, y0 = 0, x1 = -1, y1 = -1; };
static int Ctz(u32 m) { unsigned long i; _BitScanForward(&i, m); return int(i); }
using Clock = std::chrono::steady_clock;
static i64 Ns(Clock::time_point a) { return std::chrono::duration_cast<std::chrono::nanoseconds>(Clock::now() - a).count(); }

struct Map {
    int width = 0, height = 0, n = 0, off[8] = {};
    bool inside(const Box& b, int node) const { int x = node % width, y = node / width; return x >= b.x0 && x <= b.x1 && y >= b.y0 && y <= b.y1; }
    int chebyshev(int a, int b) const { return std::max(std::abs(a % width - b % width), std::abs(a / width - b / width)); }
};

// Directed adjacency of one player: outgoing and incoming direction bits, wet bits, and the
// dry edges of ground-only profiles. A word carries the moat-mode edges in bits 0..7 (DAFD0
// rules, wet in 16..23) and the ground edges in bits 8..15 (DA590 rules: Vanilla's ground
// search follows the native direction byte without DAFD0's height correction; keeps, towers
// and walls differ, pf-iter-7).
struct Mask {
    // penalty: extra cost (in ground steps of the task) for entering a node (APIShared route tile rules).
    Vec<uint8_t> out, outWet, outDry, in, inWet, inDry, written, penalty; i64 writtenCount = 0; u64 revision = 1;
    explicit Mask(Memory* m, int n) : out(size_t(n), 0, Alloc<uint8_t>(m)), outWet(size_t(n), 0, Alloc<uint8_t>(m)), outDry(size_t(n), 0, Alloc<uint8_t>(m)),
        in(size_t(n), 0, Alloc<uint8_t>(m)), inWet(size_t(n), 0, Alloc<uint8_t>(m)), inDry(size_t(n), 0, Alloc<uint8_t>(m)), written(size_t(n), 0, Alloc<uint8_t>(m)),
        penalty(size_t(n), 0, Alloc<uint8_t>(m)) {}
    // Cost of an edge that enters node v: the profile's step cost plus v's penalty, saturating at MaxCost.
    u32 cost(int v, bool wet, int ground, int moat) const {
        u32 c = u32(moat && wet ? moat : ground) + u32(penalty[v]) * u32(ground);
        return c > u32(MaxCost) ? u32(MaxCost) : c;
    }
    uint8_t outgoing(int v, int moat) const { return moat ? out[v] : outDry[v]; }
    uint8_t incoming(int v, int moat) const { return moat ? in[v] : inDry[v]; }
    bool ready(int n) const { return writtenCount == n; }
    // Returns true when the stored edges of the node changed.
    bool set(const Map& map, int node, u32 word) {
        int x = node % map.width, y = node / map.width; uint8_t o = 0, w = 0, g = 0;
        for (int d = 0; d < 8; ++d) {
            int nx = x + dx[d], ny = y + dy[d];
            if ((unsigned)nx >= unsigned(map.width) || (unsigned)ny >= unsigned(map.height)) continue;
            if (word & (1u << d)) { o |= uint8_t(1u << d); if (word & (1u << (16 + d))) w |= uint8_t(1u << d); }
            // A ground edge is never wet (moat tiles are not ground).
            if ((word & (1u << (8 + d))) && !(word & (1u << (16 + d)))) g |= uint8_t(1u << d);
        }
        if (!written[node]) { written[node] = 1; ++writtenCount; }
        if (o == out[node] && w == outWet[node] && g == outDry[node]) return false;
        for (int d = 0; d < 8; ++d) {
            int nx = x + dx[d], ny = y + dy[d];
            if ((unsigned)nx >= unsigned(map.width) || (unsigned)ny >= unsigned(map.height)) continue;
            int t = ny * map.width + nx; uint8_t bit = uint8_t(1u << d);
            in[t] = uint8_t((in[t] & ~bit) | (o & bit)); inWet[t] = uint8_t((inWet[t] & ~bit) | (w & bit));
            inDry[t] = uint8_t((inDry[t] & ~bit) | (g & bit));
        }
        out[node] = o; outWet[node] = w; outDry[node] = g; return true;
    }
};

// One-lane Dial search workspace. Buckets pop in nondecreasing order, so a node's
// first pop at its current tentative distance is final.
struct Field {
    Vec<u32> gen, dist, needGen; Vec<uint8_t> done; Vec<Vec<int>> ring;
    // Nodes settled by the last solve(), in settle order (Stored::take copies only these).
    Vec<int> settled;
    u32 generation = 0; i64 settles = 0, entries = 0; bool exhausted = false, overBudget = false;
    Field(Memory* m, int n, int buckets = MaxCost + 1) : gen(size_t(n), 0u, Alloc<u32>(m)), dist(size_t(n), 0u, Alloc<u32>(m)), needGen(size_t(n), 0u, Alloc<u32>(m)),
        done(size_t(n), 0, Alloc<uint8_t>(m)), ring(Alloc<Vec<int>>(m)), settled(Alloc<int>(m)) { for (int i = 0; i < buckets; ++i) ring.emplace_back(Alloc<int>(m)); }
    bool reached(int v) const { return gen[v] == generation && done[v]; }
    // Touched in the last search: dist is an upper bound, exact for settled nodes.
    bool touched(int v) const { return gen[v] == generation; }
    // Backward A* from source towards two targets inside a box, stopping once both are settled.
    // h = cheapest step cost x Chebyshev distance to the nearer target is consistent, so settled
    // distances are exact. Keys g + h of queued nodes stay within cost + cheapest step of the
    // current key, which needs more than 2 x MaxCost buckets (local field only). A descent over
    // touched nodes is exact too: a neighbour whose bound plus the step cost equals the exact
    // distance of the current node has that bound as its exact distance.
    // budget > 0: stops after that many settles (overBudget; nothing is proven unreachable then).
    void solveTowards(const Map& map, const Mask& mask, int source, int a, int b, int ground, int moat, const Box& box, i64 budget = 0) {
        if (++generation == 0) { std::fill(gen.begin(), gen.end(), 0u); std::fill(needGen.begin(), needGen.end(), 0u); generation = 1; }
        const int size = int(ring.size()), w = map.width; const u32 step = u32(moat ? std::min(ground, moat) : ground);
        const int ax = a % w, ay = a / w, bx = b % w, by = b / w;
        auto h = [&](int x, int y) { return step * u32(std::min(std::max(std::abs(x - ax), std::abs(y - ay)), std::max(std::abs(x - bx), std::abs(y - by)))); };
        i64 pending = 0, remaining = 0; const i64 stop = budget > 0 ? settles + budget : -1; overBudget = false;
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
            // LIFO within a key: a node pushed at the current key lies deeper on a tie, so ties dive
            // towards the targets. FIFO filled the whole diamond of equally short open-ground routes
            // (1,377 settles per single route in pf-iter-28). Any order of equal keys stays exact.
            while (!list.empty() && remaining > 0) {
                int v = list.back(); list.pop_back(); --pending;
                if (done[v]) continue;
                const int vx = v % w, vy = v / w; const u32 g = dist[v];
                if (g + h(vx, vy) != key) continue;
                done[v] = 1; ++settles;
                if (needGen[v] == generation) { needGen[v] = 0; --remaining; }
                if (settles == stop && remaining > 0) { overBudget = true; pending = 0; break; }
                // Incoming edge u -> v in direction d: u lies at v - (dx, dy).
                for (u32 m = mask.incoming(v, moat); m; m &= m - 1) {
                    int d = Ctz(m); relax(v - map.off[d], vx - dx[d], vy - dy[d], g + mask.cost(v, ((mask.inWet[v] >> d) & 1) != 0, ground, moat));
                }
            }
        }
        exhausted = remaining > 0;
        for (auto& l : ring) l.clear();
    }
    void solve(const Map& map, const Mask& mask, int source, bool forward, const std::vector<int>& targets, int ground, int moat, const Box* box = nullptr) {
        if (++generation == 0) { std::fill(gen.begin(), gen.end(), 0u); std::fill(needGen.begin(), needGen.end(), 0u); generation = 1; }
        const int size = int(ring.size()); i64 pending = 0, remaining = 0; settled.clear();
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
                done[v] = 1; ++settles; settled.push_back(v);
                if (needGen[v] == generation) { needGen[v] = 0; --remaining; }
                // moat == 0: ground-only profile, wet (moat) edges are not traversable.
                if (forward) { for (u32 m = mask.outgoing(v, moat); m; m &= m - 1) { int d = Ctz(m); relax(v + map.off[d], bucket + mask.cost(v + map.off[d], ((mask.outWet[v] >> d) & 1) != 0, ground, moat)); } }
                else { for (u32 m = mask.incoming(v, moat); m; m &= m - 1) { int d = Ctz(m); relax(v - map.off[d], bucket + mask.cost(v, ((mask.inWet[v] >> d) & 1) != 0, ground, moat)); } }
            }
            list.clear();
        }
        // Exhausted: every node that reaches the source is settled (some target was not).
        exhausted = remaining > 0;
        for (auto& l : ring) l.clear();
    }
    // Backward A* from source towards the box around all targets (a group's starts). h = cheapest
    // step x Chebyshev distance to the box is consistent and 0 inside it, so a target's key is its
    // exact cost. The search continues through the key of the last target: every node on an
    // optimal route of any target then has key <= that cost and is settled with its exact
    // distance, which is all descents and queries read. A group far from its anchor settles an
    // ellipse instead of a full disc (main fields were 70-90 ms per busy tick, pf-iter-24).
    // Needs more than 2 x MaxCost buckets, like solveTowards.
    void solveToBox(const Map& map, const Mask& mask, int source, const std::vector<int>& targets, int ground, int moat, const Box& box) {
        if (++generation == 0) { std::fill(gen.begin(), gen.end(), 0u); std::fill(needGen.begin(), needGen.end(), 0u); generation = 1; }
        const int size = int(ring.size()), w = map.width; const u32 step = u32(moat ? std::min(ground, moat) : ground);
        auto h = [&](int x, int y) {
            int ox = std::max(0, std::max(box.x0 - x, x - box.x1)), oy = std::max(0, std::max(box.y0 - y, y - box.y1));
            return step * u32(std::max(ox, oy));
        };
        i64 pending = 0, remaining = 0; settled.clear();
        for (int t : targets) if (needGen[t] != generation) { needGen[t] = generation; ++remaining; }
        auto relax = [&](int u, int x, int y, u32 value) {
            if (gen[u] != generation) { gen[u] = generation; done[u] = 0; }
            else if (done[u] || dist[u] <= value) return;
            dist[u] = value; ring[(value + h(x, y)) % size].push_back(u); ++pending; ++entries;
        };
        relax(source, source % w, source / w, 0);
        u32 limit = ~0u;
        for (u32 key = h(source % w, source / w); pending > 0 && key <= limit; ++key) {
            Vec<int>& list = ring[key % size];
            for (size_t i = 0; i < list.size(); ++i) {
                int v = list[i]; --pending;
                if (done[v]) continue;
                const int vx = v % w, vy = v / w; const u32 g = dist[v];
                if (g + h(vx, vy) != key) continue;
                done[v] = 1; ++settles; settled.push_back(v);
                if (needGen[v] == generation) { needGen[v] = 0; if (--remaining == 0) limit = key; }
                for (u32 m = mask.incoming(v, moat); m; m &= m - 1) {
                    int d = Ctz(m); relax(v - map.off[d], vx - dx[d], vy - dy[d], g + mask.cost(v, ((mask.inWet[v] >> d) & 1) != 0, ground, moat));
                }
            }
            list.clear();
        }
        exhausted = remaining > 0;
        for (auto& l : ring) l.clear();
    }
};

// Compact backward field to an anchor: exact cost per settled node, Unset otherwise.
struct Stored {
    // box: the target box of a solveToBox field (every node on an optimal route from a cell inside
    // it to the anchor is settled when that cell is); boxed == false for full fields.
    int anchor = -1; bool exhausted = false, sparse = false, boxed = false; Box box; u32 mask = 0; Vec<u32> dist, values; Vec<int> keys;
    explicit Stored(Memory* m) : dist(Alloc<u32>(m)), values(Alloc<u32>(m)), keys(Alloc<int>(m)) {}
    static u32 Hash(int v) { u32 h = u32(v) * 0x9E3779B1u; return h ^ (h >> 16); }
    // Takes the settled distances of the field's last solve(). A small field (a sub-anchor that
    // stopped near its slot) goes into an open-addressing table: a full-map array per sub-anchor
    // cost ~0.75 ms each and dominated moves of unreachable groups (pf-iter-4, ~100 per move).
    void take(const Map& map, const Field& f, int a) {
        anchor = a; exhausted = f.exhausted; sparse = f.settled.size() * 8 < size_t(map.n);
        if (sparse) {
            size_t capacity = 16; while (capacity < f.settled.size() * 2) capacity <<= 1;
            dist.clear(); keys.assign(capacity, -1); values.assign(capacity, Unset); mask = u32(capacity - 1);
            for (int v : f.settled) { u32 h = Hash(v) & mask; while (keys[h] >= 0) h = (h + 1) & mask; keys[h] = v; values[h] = f.dist[v]; }
        }
        else { keys.clear(); values.clear(); dist.assign(size_t(map.n), Unset); for (int v : f.settled) dist[v] = f.dist[v]; }
    }
    u32 at(int v) const {
        if (!sparse) return dist[v];
        for (u32 h = Hash(v) & mask;; h = (h + 1) & mask) { int k = keys[h]; if (k == v) return values[h]; if (k < 0) return Unset; }
    }
};
// Deterministic descent (lowest direction first) along a backward cost function. The stop
// test also gets the node's coordinates, tracked per step instead of divided per test.
template<class Dist, class Stop> static int Descend(const Map& map, const Mask& mask, Dist distance, int node, int ground, int moat, std::vector<uint8_t>& path, int& g, int& w, Stop stop) {
    int x = node % map.width, y = node / map.width;
    while (!stop(node, x, y)) {
        u32 here = distance(node); int next = -1;
        for (u32 m = mask.outgoing(node, moat); m && next < 0; m &= m - 1) {
            int d = Ctz(m), u = node + map.off[d]; u32 there = distance(u); if (there == Unset) continue;
            bool wet = ((mask.outWet[node] >> d) & 1) != 0;
            if (there + mask.cost(u, wet, ground, moat) != here) continue;
            next = u; x += dx[d]; y += dy[d]; path.push_back(uint8_t(d)); if (wet) ++w; else ++g;
        }
        if (next < 0 || int(path.size()) > MaxSteps * 4) return -1;
        node = next;
    }
    return node;
}

struct Task {
    int id = 0, player = 0, anchor = 0, ground = 1, moat = 1, status = Pending; u64 revision = 0; bool exhausted = false;
    std::vector<int> starts; std::shared_ptr<Stored> main; std::vector<std::unique_ptr<Stored>> subs;
    // Starts not proven enclosed at the last compute (mma_step); the field, its box, the field cache and
    // sub-anchors target only these.
    std::vector<int> open;
};

// Recently computed main fields. A field depends only on player mask (revision), anchor and
// cost profile; its settled nodes are a prefix of one deterministic settle order, so a field
// that settled every start of a new task (or ran out, settling everything that reaches the
// anchor) holds exactly the distances a new solve would compute. Several AI groups sent to one
// rally point in a tick then share one field (pf-iter-12: 624 repeated fields per run).
struct CachedField { int player = 0, anchor = 0, ground = 0, moat = 0; u64 revision = 0, used = 0; std::shared_ptr<Stored> field; };
constexpr int FieldCacheSize = 8;

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

// Per-thread query workspace: the local A* field (doubled ring) and path buffers.
struct Scratch {
    std::unique_ptr<Field> local; std::vector<uint8_t> path, rest; std::vector<int> targets; i64 stats[StatCount] = {};
    // Forward flood of mma_single_route's enclosure proof (stamped per call, sized on first use).
    std::vector<u32> seen; std::vector<int> flood; u32 seenGen = 0;
    Scratch(Memory* m, int n) : local(std::make_unique<Field>(m, n, 2 * MaxCost + 2)) {}
};

struct Context {
    Memory memory; Map map;
    std::unique_ptr<Mask> masks[MaxPlayers + 1];
    std::map<int, std::unique_ptr<Task>> tasks; int nextId = 1;
    std::vector<CachedField> fieldCache; u64 fieldCacheClock = 0;
    std::vector<std::unique_ptr<Field>> fields;
    // Simulation-thread scratch (single queries, ordered batch pass) and one per batch worker.
    std::unique_ptr<Scratch> scratch; std::vector<std::unique_ptr<Scratch>> workers;
    Pool pool; i64 stats[StatCount] = {};
    // Settle budget of the single route tried before a new sub-anchor (0: none; mma_set_sub_single_budget).
    i64 subSingleBudget = SubSingleBudget;
    Context(int w, int h, i64 limit) {
        memory.maximum = limit; map.width = w; map.height = h; map.n = w * h;
        for (int d = 0; d < 8; ++d) map.off[d] = dy[d] * w + dx[d];
        scratch = std::make_unique<Scratch>(&memory, map.n);
    }
    ~Context() { pool.stop(); }
    Mask* mask(int player) { return player >= 1 && player <= MaxPlayers ? masks[player].get() : nullptr; }
    bool fresh(const Task& t) { Mask* m = mask(t.player); return m && m->ready(map.n) && t.revision == m->revision; }
};

static bool ValidNode(const Context& c, int node) { return node >= 0 && node < c.map.n; }

// Exact enclosure proof before a single route: the start's forward component over the same edges
// (outgoing, as the backward A* reads them incoming) is exhausted within SingleFloodBudget nodes
// without the target, so no route exists. The backward A* would instead settle the target's whole
// component (pf-iter-34: 256 unreachable single routes, 56 M settles and 2 s in one window, mostly
// starts on building tiles without ground edges). Larger start components keep the A*.
constexpr int SingleFloodBudget = 2048;
static bool StartEnclosed(const Map& map, const Mask& mask, Scratch& s, int start, int target, int moat, int budget = SingleFloodBudget) {
    if (s.seen.size() != size_t(map.n)) { s.seen.assign(size_t(map.n), 0u); s.seenGen = 0; }
    if (++s.seenGen == 0) { std::fill(s.seen.begin(), s.seen.end(), 0u); s.seenGen = 1; }
    if (start == target) return false;
    s.flood.clear(); s.flood.push_back(start); s.seen[start] = s.seenGen;
    for (size_t i = 0; i < s.flood.size(); ++i) {
        const int v = s.flood[i];
        for (u32 m = mask.outgoing(v, moat); m; m &= m - 1) {
            const int u = v + map.off[Ctz(m)];
            if (u == target) return false;
            if (s.seen[u] == s.seenGen) continue;
            if (int(s.flood.size()) >= budget) return false;
            s.seen[u] = s.seenGen; s.flood.push_back(u);
        }
    }
    return true;
}

// One exact route from start to target with the scratch's local field (backward A* from the target
// over the whole map, after the enclosure proof): Found with the directions in s.path, Unreachable,
// or OverLimit (over budget settles when budget > 0, or longer than MaxSteps/capacity).
static int SingleSearch(const Map& map, const Mask& mask, Scratch& s, int start, int target, int ground, int moat, i64 budget, int capacity,
    int& g, int& w, i64& cost) {
    i64* stats = s.stats; auto t0 = Clock::now(); ++stats[SingleQueries];
    auto done = [&](int status) { stats[SingleNs] += Ns(t0); return status; };
    if (StartEnclosed(map, mask, s, start, target, moat)) return done(Unreachable);
    Field& local = *s.local; const i64 before = local.settles;
    Box all; all.x0 = 0; all.y0 = 0; all.x1 = map.width - 1; all.y1 = map.height - 1;
    local.solveTowards(map, mask, target, start, start, ground, moat, all, budget);
    stats[SingleSettles] += local.settles - before; local.settles = before;
    if (local.overBudget) return done(OverLimit);
    if (!local.reached(start)) return done(Unreachable);
    s.path.clear(); g = 0; w = 0;
    auto touched = [&](int v) { return local.touched(v) ? local.dist[v] : Unset; };
    if (Descend(map, mask, touched, start, ground, moat, s.path, g, w, [&](int v, int, int) { return v == target; }) < 0 ||
        int(s.path.size()) > MaxSteps || int(s.path.size()) > capacity) return done(OverLimit);
    cost = i64(local.dist[start]);
    return done(Found);
}

// Answers one unit. Anchors are tried in creation order (main anchor first). An anchor is
// usable when the slot is graph-near to it inside the anchor-slot box; otherwise a field
// at the slot is created once (sub-anchor) and reused by later queries of this task.
// mayCreate == false (parallel batch pass): the task is only read; a query that would create
// a sub-anchor returns Deferred and is answered later in caller order, so results equal a
// sequential run exactly.
static int Query(Context& c, Task& t, Scratch& sc, int start, int slot, int margin, uint8_t* bytes, int capacity, QueryResult& r, bool mayCreate = true) {
    const Map& map = c.map; const Mask& mask = *c.mask(t.player); Field& local = *sc.local; i64* stats = sc.stats;
    std::vector<uint8_t>& path = sc.path; std::vector<uint8_t>& rest = sc.rest;
    auto finish = [&](int g, int w, int flags) {
        r.flags = flags; r.ground = g; r.moat = w; r.cost = i64(g) * t.ground + i64(w) * t.moat; r.length = int(path.size());
        if (int(path.size()) > MaxSteps || int(path.size()) > capacity) { r.status = OverLimit; r.length = 0; return; }
        std::copy(path.begin(), path.end(), bytes); r.status = Found;
    };
    // A field that ran out before reaching every target settled every node that reaches its
    // anchor: a start it never settled cannot reach the anchor, so it cannot reach any slot that
    // does (directed graph). Answer that exactly instead of building a full-map sub-anchor per
    // query (AI units behind walls, 29.10.2026). Sub-anchors count too: the slots of a group whose
    // starts lie in another component then cost one exhausted sub-anchor, not one each (pf-iter-3).
    for (size_t a = 0; a <= t.subs.size(); ++a) {
        const Stored& s = a == 0 ? *t.main : *t.subs[a - 1];
        if (s.exhausted && s.at(start) == Unset && s.at(slot) != Unset) { r.status = Unreachable; r.flags = 0; return Ok; }
    }
    for (size_t a = 0; a <= t.subs.size(); ++a) {
        const Stored& s = a == 0 ? *t.main : *t.subs[a - 1];
        const u32 startDistance = s.at(start);
        if (startDistance == Unset) { ++stats[AnchorNoStart]; continue; }
        Box box;
        box.x0 = std::max(0, std::min(s.anchor % map.width, slot % map.width) - margin); box.x1 = std::min(map.width - 1, std::max(s.anchor % map.width, slot % map.width) + margin);
        box.y0 = std::max(0, std::min(s.anchor / map.width, slot / map.width) - margin); box.y1 = std::min(map.height - 1, std::max(s.anchor / map.width, slot / map.width) + margin);
        auto stored = [&](int v) { return s.at(v); };
        path.clear(); int gH = 0, wH = 0;
        int entry = Descend(map, mask, stored, start, t.ground, t.moat, path, gH, wH, [&](int, int x, int y) { return x >= box.x0 && x <= box.x1 && y >= box.y0 && y <= box.y1; });
        if (entry < 0) { ++stats[AnchorNoEntry]; continue; }
        local.solveTowards(map, mask, slot, s.anchor, entry, t.ground, t.moat, box);
        if (!local.reached(s.anchor)) { ++stats[AnchorUnreached]; continue; }
        if (local.dist[s.anchor] > u32(t.ground) * u32(2 * map.chebyshev(s.anchor, slot) + 8)) { ++stats[AnchorTooFar]; continue; }
        auto localDistance = [&](int v) { return local.touched(v) ? local.dist[v] : Unset; };
        // The descent to the entry follows the stored field exactly: its cost is the distance drop
        // (penalties included; equal to gH * ground + wH * moat without them).
        i64 boxCost = local.reached(entry) ? i64(startDistance) - i64(s.at(entry)) + local.dist[entry] : -1;
        i64 viaCost = i64(startDistance) + local.dist[s.anchor];
        int g = gH, w = wH, flags = a > 0 ? SubAnchor : 0;
        if (boxCost >= 0 && boxCost <= viaCost) {
            rest.clear(); if (Descend(map, mask, localDistance, entry, t.ground, t.moat, rest, g, w, [&](int v, int, int) { return v == slot; }) < 0) continue;
        } else {
            ++stats[ViaChosen]; flags |= ViaAnchor;
            if (Descend(map, mask, stored, entry, t.ground, t.moat, path, g, w, [&](int v, int, int) { return v == s.anchor; }) < 0) continue;
            rest.clear(); if (Descend(map, mask, localDistance, s.anchor, t.ground, t.moat, rest, g, w, [&](int v, int, int) { return v == slot; }) < 0) continue;
        }
        path.insert(path.end(), rest.begin(), rest.end());
        finish(g, w, flags); return Ok;
    }
    // No usable anchor: first the exact single route under SubSingleBudget settles (pf-iter-39: 108
    // sub-anchors and 118 ms in one window, mostly members whose start the main field never settled);
    // it is not cached. A longer search falls through to the sub-anchor. Sequential pass only
    // (mayCreate), so results stay independent of the thread count.
    if (mayCreate && c.subSingleBudget > 0) {
        int g = 0, w = 0; i64 cost = 0;
        const int status = SingleSearch(map, mask, sc, start, slot, t.ground, t.moat, c.subSingleBudget, capacity, g, w, cost);
        if (status == Unreachable) { ++stats[SubSingles]; r.status = Unreachable; r.flags = 0; return Ok; }
        if (status == Found) {
            ++stats[SubSingles]; std::copy(sc.path.begin(), sc.path.end(), bytes);
            r.status = Found; r.flags = 0; r.ground = g; r.moat = w; r.length = int(sc.path.size()); r.cost = i64(g) * t.ground + i64(w) * t.moat;
            return Ok;
        }
    }
    // Then an exact field at the slot, cached as a sub-anchor while allowed. With
    // every sub-anchor slot used the query is not answered (the caller keeps Vanilla) rather
    // than building an uncached full-map field per query.
    if (int(t.subs.size()) >= MaxSubAnchors) { r.status = OverLimit; r.flags = Uncached; ++stats[SubCapped]; return Ok; }
    if (!mayCreate) { r.status = Deferred; r.flags = 0; return Ok; }
    auto t0 = Clock::now();
    // An exhausted main field splits the starts: those it settled reach the anchor, the others
    // cannot. A sub-anchor targets only starts of the querying start's class; the other class
    // usually lies in another component and would force a search over this whole component.
    // Answers stay exact: a later query uses a sub-anchor only where it settled that start.
    std::vector<int>& targets = sc.targets; targets.clear();
    const bool startInMain = t.main->at(start) != Unset;
    for (int s : t.open) if (!t.exhausted || (t.main->at(s) != Unset) == startInMain) targets.push_back(s);
    targets.push_back(start);
    local.solve(map, mask, slot, false, targets, t.ground, t.moat);
    int flags = NewSubAnchor | SubAnchor;
    t.subs.push_back(std::make_unique<Stored>(&c.memory)); t.subs.back()->take(map, local, slot);
    const Stored* s = t.subs.back().get(); ++stats[SubAnchors];
    stats[SubNs] += Ns(t0);
    path.clear(); int g = 0, w = 0;
    if (s->at(start) == Unset) { r.status = Unreachable; r.flags = flags; return Ok; }
    auto stored = [&](int v) { return s->at(v); };
    if (Descend(map, mask, stored, start, t.ground, t.moat, path, g, w, [&](int v, int, int) { return v == slot; }) < 0) { r.status = OverLimit; r.flags = flags; return Ok; }
    finish(g, w, flags); return Ok;
}

// Moves a scratch's counters and its local field's settle/entry counts into the context.
static void MergeScratch(Context& c, Scratch& sc) {
    for (int i = 0; i < StatCount; ++i) { c.stats[i] += sc.stats[i]; sc.stats[i] = 0; }
    c.stats[SettleEvents] += sc.local->settles; c.stats[Entries] += sc.local->entries; sc.local->settles = sc.local->entries = 0;
}

template<class F> static int Guard(F body) {
    try { return body(); }
    catch (Exhausted&) { return NoMemory; } catch (std::bad_alloc&) { return NoMemory; } catch (...) { return Internal; }
}
}

using namespace mma;
extern "C" {
__declspec(dllexport) int mma_contract() { return 0x0400000F; }
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
// Per-node entry penalties (0..255 ground steps) of one player's mask; a change makes its tasks stale.
__declspec(dllexport) int mma_penalty_update(Context* c, int player, const int* nodes, const uint8_t* values, int count, int* changed) {
    if (!c || !nodes || !values || !c->mask(player) || count < 0) return BadArgument;
    Mask& m = *c->mask(player); bool ready = m.ready(c->map.n); int n = 0;
    for (int i = 0; i < count; ++i) { if (!ValidNode(*c, nodes[i])) return BadArgument; }
    for (int i = 0; i < count; ++i) if (m.penalty[nodes[i]] != values[i]) { m.penalty[nodes[i]] = values[i]; ++n; }
    if (ready && n) ++m.revision;
    if (changed) *changed = n;
    return Ok;
}
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
            if (t.status != Pending || !m || !m->ready(c->map.n)) continue;
            // A start whose forward component closes within StartFilterBudget nodes without the anchor
            // (units on building or tower tiles without edges) can never be settled: the field would
            // exhaust the anchor's whole component to prove it (~234,000 nodes, ~9 ms; nearly all field
            // time of pf-diag-field). Such starts stay out of the field; their queries find no stored
            // distance and get the single route's own exact enclosure proof.
            t.open.clear();
            for (int s : t.starts) if (!StartEnclosed(c->map, *m, *c->scratch, s, t.anchor, t.moat, StartFilterBudget)) t.open.push_back(s);
            if (t.open.empty()) t.open.push_back(t.anchor);
            c->stats[StartsEnclosed] += i64(t.starts.size()) - i64(t.open.size());
            bool reused = false;
            for (CachedField& e : c->fieldCache) {
                if (e.player != t.player || e.anchor != t.anchor || e.ground != t.ground || e.moat != t.moat || e.revision != m->revision) continue;
                bool covers = e.field->exhausted;
                if (!covers)
                {
                    covers = true;
                    for (int s : t.open)
                        if (e.field->at(s) == Unset || (e.field->boxed && !c->map.inside(e.field->box, s))) { covers = false; break; }
                }
                if (!covers) break;
                t.main = e.field; t.subs.clear(); t.exhausted = e.field->exhausted; t.revision = m->revision; t.status = Ready;
                e.used = ++c->fieldCacheClock; ++c->stats[FieldReuses]; reused = true; break;
            }
            if (!reused) work.push_back(&t);
        }
        // A fixed worker count keeps one persistent pool; idle workers find no task.
        int workers = threads;
        while (int(c->fields.size()) < workers) c->fields.push_back(std::make_unique<Field>(&c->memory, c->map.n, 2 * MaxCost + 2));
        c->pool.resize(workers); c->stats[Workers] = workers;
        std::atomic<int> next{0};
        // Allocation failures stay with their task; the task is reported Failed.
        auto job = [&](int worker) {
            Field& f = *c->fields[worker];
            for (;;) {
                int i = next.fetch_add(1); if (i >= int(work.size())) break;
                Task& t = *work[i];
                try {
                    Box box; box.x0 = box.y0 = INT32_MAX; box.x1 = box.y1 = -1;
                    for (int s : t.open) { int x = s % c->map.width, y = s / c->map.width; box.x0 = std::min(box.x0, x); box.x1 = std::max(box.x1, x); box.y0 = std::min(box.y0, y); box.y1 = std::max(box.y1, y); }
                    // Scattered starts (a box over most of the map) gain nothing from the heuristic: full field.
                    bool boxed = std::max(box.x1 - box.x0, box.y1 - box.y0) <= c->map.width / 4;
                    if (boxed) f.solveToBox(c->map, *c->mask(t.player), t.anchor, t.open, t.ground, t.moat, box);
                    else f.solve(c->map, *c->mask(t.player), t.anchor, false, t.open, t.ground, t.moat);
                    auto s = std::make_shared<Stored>(&c->memory); s->take(c->map, f, t.anchor); s->boxed = boxed; s->box = box;
                    t.main = std::move(s); t.subs.clear(); t.exhausted = f.exhausted; t.revision = c->mask(t.player)->revision; t.status = Ready;
                }
                catch (...) { t.main.reset(); t.status = Failed; }
            }
        };
        if (!work.empty()) c->pool.run(job);
        for (Task* t : work) {
            if (t->status != Ready) { ++c->stats[TasksFailed]; continue; }
            ++c->stats[TasksComputed];
            // Same key replaces its entry; otherwise the least recently used one is evicted.
            CachedField* slot = nullptr;
            for (CachedField& e : c->fieldCache) if (e.player == t->player && e.anchor == t->anchor && e.ground == t->ground && e.moat == t->moat) { slot = &e; break; }
            if (!slot && int(c->fieldCache.size()) < FieldCacheSize) { c->fieldCache.emplace_back(); slot = &c->fieldCache.back(); }
            if (!slot) { slot = &c->fieldCache[0]; for (CachedField& e : c->fieldCache) if (e.used < slot->used) slot = &e; }
            slot->player = t->player; slot->anchor = t->anchor; slot->ground = t->ground; slot->moat = t->moat;
            slot->revision = t->revision; slot->field = t->main; slot->used = ++c->fieldCacheClock;
        }
        for (auto& f : c->fields) { c->stats[SettleEvents] += f->settles; c->stats[Entries] += f->entries; f->settles = f->entries = 0; }
        if (computed) *computed = int(work.size());
        c->stats[StepNs] += Ns(t0);
        return Ok;
    });
}

// Route of one unit from start to target (ground/moat profile like a task): backward A* from the
// target towards the start over the whole map (solveTowards; Chebyshev heuristic), so a trip
// explores a corridor instead of the disc of a full field. Found: packed directions as for
// mma_query; Unreachable when the target's backward component was settled without the start.
// Exact optimal cost; deterministic for equal inputs.
// budget > 0 (mma_single_route_limited): at most that many A* settles; a longer search answers
// OverLimit (never Unreachable), so the caller can fall back to another exact source.
// result->reserved: settles of this call (any status), for callers that share a settle budget.
static int SingleRoute(Context* c, int player, int target, int ground, int moat, int start, i64 budget, uint8_t* bytes, int capacity, QueryResult* result) {
    if (!c || !result || !bytes || capacity < 0 || ground < 1 || ground > MaxCost || moat < 0 || moat > MaxCost || budget < 0) return BadArgument;
    *result = QueryResult{}; result->status = Invalid;
    if (!ValidNode(*c, start) || !ValidNode(*c, target)) return BadArgument;
    Mask* mask = c->mask(player);
    if (!mask || !mask->ready(c->map.n)) { result->status = Pending; return Ok; }
    return Guard([&] {
        Scratch& sc = *c->scratch; int g = 0, w = 0; i64 cost = 0; const i64 before = sc.stats[SingleSettles];
        const int status = SingleSearch(c->map, *mask, sc, start, target, ground, moat, budget, capacity, g, w, cost);
        result->reserved = int(std::min<i64>(sc.stats[SingleSettles] - before, 0x7FFFFFFF));
        MergeScratch(*c, sc);
        result->status = status;
        if (status == Found) { std::copy(sc.path.begin(), sc.path.end(), bytes); result->ground = g; result->moat = w; result->length = int(sc.path.size()); result->cost = cost; }
        return Ok;
    });
}

__declspec(dllexport) int mma_set_sub_single_budget(Context* c, int budget) {
    if (!c || budget < 0) return BadArgument;
    c->subSingleBudget = budget; return Ok;
}
__declspec(dllexport) int mma_single_route(Context* c, int player, int target, int ground, int moat, int start, uint8_t* bytes, int capacity, QueryResult* result) {
    return SingleRoute(c, player, target, ground, moat, start, 0, bytes, capacity, result);
}
__declspec(dllexport) int mma_single_route_limited(Context* c, int player, int target, int ground, int moat, int start, int budget, uint8_t* bytes, int capacity, QueryResult* result) {
    return SingleRoute(c, player, target, ground, moat, start, budget, bytes, capacity, result);
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
        Scratch& sc = *c->scratch;
        int code = Query(*c, t, sc, start, slot, margin, bytes, capacity, *result);
        MergeScratch(*c, sc);
        c->stats[QueryNs] += Ns(t0); return code;
    });
}
// Answers count units of one task: a parallel pass over existing anchors (each worker with
// its own scratch, the task read-only), then the deferred queries in caller order with the
// ordinary sub-anchor rules. Unit i writes its steps to bytes + i * stride. Results equal
// count single queries in the same order, independent of the thread count.
__declspec(dllexport) int mma_query_batch(Context* c, int id, const int* starts, const int* slots, int count, int margin, int threads,
    uint8_t* bytes, int stride, QueryResult* results) {
    if (!c || !starts || !slots || !bytes || !results || count < 0 || margin < 0 || stride < 0 || threads < 1 || threads > 64) return BadArgument;
    if (!c->tasks.count(id)) return BadArgument;
    for (int i = 0; i < count; ++i) if (!ValidNode(*c, starts[i]) || !ValidNode(*c, slots[i])) return BadArgument;
    Task& t = *c->tasks[id];
    for (int i = 0; i < count; ++i) { results[i] = QueryResult{}; results[i].status = Invalid; }
    if (t.status != Ready || !c->fresh(t)) { for (int i = 0; i < count; ++i) results[i].status = t.status != Ready ? t.status : int(Stale); return Ok; }
    return Guard([&] {
        auto t0 = Clock::now();
        while (int(c->workers.size()) < threads) c->workers.push_back(std::make_unique<Scratch>(&c->memory, c->map.n));
        c->pool.resize(threads);
        std::atomic<int> next{0}; std::atomic<int> failure{Ok};
        auto job = [&](int worker) {
            Scratch& sc = *c->workers[worker];
            for (;;) {
                int i = next.fetch_add(1); if (i >= count) break;
                try { Query(*c, t, sc, starts[i], slots[i], margin, bytes + size_t(i) * size_t(stride), stride, results[i], false); }
                catch (Exhausted&) { failure = NoMemory; results[i].status = Failed; }
                catch (std::bad_alloc&) { failure = NoMemory; results[i].status = Failed; }
                catch (...) { failure = Internal; results[i].status = Failed; }
            }
        };
        if (count > 0) c->pool.run(job);
        for (int w = 0; w < threads; ++w) MergeScratch(*c, *c->workers[w]);
        if (failure != Ok) return int(failure);
        Scratch& sc = *c->scratch;
        for (int i = 0; i < count; ++i)
            if (results[i].status == Deferred) Query(*c, t, sc, starts[i], slots[i], margin, bytes + size_t(i) * size_t(stride), stride, results[i], true);
        MergeScratch(*c, sc);
        c->stats[Queries] += count; c->stats[QueryNs] += Ns(t0);
        return int(Ok);
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
