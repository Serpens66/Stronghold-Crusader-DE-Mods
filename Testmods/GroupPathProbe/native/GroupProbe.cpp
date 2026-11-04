// Offline group-planning probe. Original implementation, no game imports.
// Variant A ("lanes"): exact per-slot fields, up to 64 slots per bit-parallel
// backward Dial search. Variant B ("anchor"): one exact field per distinct anchor
// and profile (shared by groups, computed on worker threads), then exact
// box-restricted endings to the individual slots.
#ifndef SERP_DEVDIAG
#error Group probe requires an explicit diagnostic build.
#endif
#include <algorithm>
#include <atomic>
#include <chrono>
#include <deque>
#include <cstdint>
#include <intrin.h>
#include <map>
#include <memory>
#include <mutex>
#include <new>
#include <thread>
#include <tuple>
#include <vector>

namespace gp {
using i64 = int64_t;
using u64 = uint64_t;
using u32 = uint32_t;
constexpr int dx[8] = {0,1,1,1,0,-1,-1,-1}, dy[8] = {-1,-1,0,1,1,1,0,-1};
constexpr int MaxSteps = 2000;
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

// Private ABI, mirrored in tests/GroupProbeInterop.cs.
struct Unit { int start, slot, group, index; };
struct Group { int anchor, ground, moat, reserved; };
struct Result { int index, status, ground, moat, offset, length; i64 cost; };
static_assert(sizeof(Unit) == 16 && sizeof(Group) == 16 && sizeof(Result) == 32, "group probe ABI");
enum Status { Found = 0, Unreachable = 1, OverLimit = 2 };
enum StatIndex { SettleEvents, Entries, Blocks, Splits, Fallbacks, ExactFallbacks, FieldTasks, LaneNs, ExtractNs, MaxGroupNs, MaxTaskNs, AnchorNs, LocalNs, FallbackNs, PeakBytes, Allocations, Threads, ViaChosen, FarUnits, SubPlans, StatCount };

struct Box { int x0 = 0, y0 = 0, x1 = -1, y1 = -1; bool none() const { return x1 < x0; } };
struct Entry { int node; u64 mask; };
static int Ctz(u32 m) { unsigned long i; _BitScanForward(&i, m); return int(i); }

struct Context;
// One-lane Dial search workspace (one per worker thread). Buckets pop in
// nondecreasing order; a node's first pop at its current tentative distance is final.
struct Field {
    Vec<u32> gen, dist, needGen; Vec<uint8_t> done; Vec<Vec<int>> ring;
    u32 generation = 0; i64 settles = 0, entries = 0;
    Field(Memory* m, int n, int ringSize) : gen(size_t(n), 0u, Alloc<u32>(m)), dist(size_t(n), 0u, Alloc<u32>(m)), needGen(size_t(n), 0u, Alloc<u32>(m)),
        done(size_t(n), 0, Alloc<uint8_t>(m)), ring(Alloc<Vec<int>>(m)) { for (int i = 0; i < ringSize; ++i) ring.emplace_back(Alloc<int>(m)); }
    bool reached(int v) const { return gen[v] == generation && done[v]; }
    void solve(const Context& c, int source, bool forward, const std::vector<int>& targets, int ground, int moat, const Box* box = nullptr);
};

struct Context {
    Memory memory; int width, height, n, off[8];
    Vec<uint8_t> valid, out, outWet, in, inWet;
    // Lane workspace (serial use only); generation stamps avoid clearing between blocks.
    Vec<u32> gen, needGen, base; Vec<u64> settled, need; Vec<uint8_t> offset;
    Vec<u32> ringBucket; Vec<int> ringEntry; Vec<Vec<Entry>> ring;
    u32 generation = 0, bucketBase = 1; int ringSize = 0;
    std::vector<std::unique_ptr<Field>> fields;
    i64 stats[StatCount] = {};
    Vec<uint8_t> output; Vec<Result> results;
    Context(int w, int h, const uint8_t* v, const u32* e, i64 limit) : width(w), height(h), n(w * h),
        valid(Alloc<uint8_t>(&memory)), out(Alloc<uint8_t>(&memory)), outWet(Alloc<uint8_t>(&memory)), in(Alloc<uint8_t>(&memory)), inWet(Alloc<uint8_t>(&memory)),
        gen(Alloc<u32>(&memory)), needGen(Alloc<u32>(&memory)), base(Alloc<u32>(&memory)), settled(Alloc<u64>(&memory)), need(Alloc<u64>(&memory)), offset(Alloc<uint8_t>(&memory)),
        ringBucket(Alloc<u32>(&memory)), ringEntry(Alloc<int>(&memory)), ring(Alloc<Vec<Entry>>(&memory)), output(Alloc<uint8_t>(&memory)), results(Alloc<Result>(&memory)) {
        memory.maximum = limit;
        for (int d = 0; d < 8; ++d) off[d] = dy[d] * w + dx[d];
        valid.assign(v, v + n); out.assign(n, 0); outWet.assign(n, 0); in.assign(n, 0); inWet.assign(n, 0);
        // Precomputed directed adjacency: no division or bounds test in the search loops.
        for (int node = 0; node < n; ++node) {
            if (!valid[node]) continue; int x = node % w, y = node / w;
            for (int d = 0; d < 8; ++d) {
                int nx = x + dx[d], ny = y + dy[d];
                if ((unsigned)nx >= unsigned(w) || (unsigned)ny >= unsigned(h) || !valid[ny * w + nx] || !(e[node] & (1u << d))) continue;
                int to = ny * w + nx; bool wet = (e[node] & (1u << (8 + d))) != 0;
                out[node] |= uint8_t(1u << d); if (wet) outWet[node] |= uint8_t(1u << d);
                in[to] |= uint8_t(1u << d); if (wet) inWet[to] |= uint8_t(1u << d);
            }
        }
        gen.assign(n, 0); needGen.assign(n, 0); base.assign(n, 0); settled.assign(n, 0); need.assign(n, 0); offset.assign(size_t(n) * 64, 0);
    }
    bool inside(const Box& b, int node) const { if (b.none()) return true; int x = node % width, y = node / width; return x >= b.x0 && x <= b.x1 && y >= b.y0 && y <= b.y1; }
    void prepare(int maximumCost, int threads) {
        int size = maximumCost + 1;
        if (size != ringSize) {
            ringSize = size; ringBucket.assign(size_t(n) * size, 0); ringEntry.assign(size_t(n) * size, -1);
            ring.clear(); for (int i = 0; i < size; ++i) ring.emplace_back(Alloc<Entry>(&memory));
            bucketBase = 1; fields.clear();
        }
        while (int(fields.size()) < threads) fields.push_back(std::make_unique<Field>(&memory, n, ringSize));
    }
    u64 lanes(int node) const { return gen[node] == generation ? settled[node] : 0; }
    // Exact distance of a settled lane; callers only read lanes reported by lanes().
    u32 distance(int node, int lane) const { return base[node] + offset[size_t(node) * 64 + lane]; }

    // Bit-parallel backward Dial search. Lane j is seeded at slots[j]; a lane settles at
    // node v exactly once, in increasing distance order, so the first settle is optimal.
    // Returns false only when a lane's distance exceeds the 8-bit per-node offset.
    bool solve(const int* slots, int count, const std::vector<std::pair<int,int>>& needs, const Box& box, int ground, int moat) {
        if (++generation == 0) { std::fill(gen.begin(), gen.end(), 0u); std::fill(needGen.begin(), needGen.end(), 0u); generation = 1; }
        if (bucketBase > 0xF0000000u) { std::fill(ringBucket.begin(), ringBucket.end(), 0u); bucketBase = 1; }
        const bool boxed = !box.none(); const u32 start = bucketBase; i64 pending = 0, remaining = 0;
        for (auto& need0 : needs) {
            int s = need0.first; u64 bit = u64(1) << need0.second;
            if (needGen[s] != generation) { needGen[s] = generation; need[s] = 0; }
            if (!(need[s] & bit)) { need[s] |= bit; ++remaining; }
        }
        auto push = [&](int node, u64 mask, u32 bucket) {
            size_t key = size_t(node) * ringSize + bucket % ringSize;
            Vec<Entry>& list = ring[bucket % ringSize];
            if (ringBucket[key] == bucket) { list[ringEntry[key]].mask |= mask; return; }
            ringBucket[key] = bucket; ringEntry[key] = int(list.size()); list.push_back(Entry{node, mask}); ++pending; ++stats[Entries];
        };
        for (int j = 0; j < count; ++j) if (valid[slots[j]] && inside(box, slots[j])) push(slots[j], u64(1) << j, start);
        u32 bucket = start;
        while (pending > 0 && remaining > 0) {
            Vec<Entry>& list = ring[bucket % ringSize];
            for (size_t i = 0; i < list.size(); ++i) {
                Entry item = list[i]; --pending;
                int v = item.node;
                if (gen[v] != generation) { gen[v] = generation; settled[v] = 0; base[v] = bucket - start; }
                u64 fresh = item.mask & ~settled[v];
                if (!fresh) continue;
                u32 local = bucket - start - base[v];
                if (local > 255) { for (auto& l : ring) l.clear(); bucketBase = bucket + ringSize + 1; return false; }
                settled[v] |= fresh; ++stats[SettleEvents];
                uint8_t* row = &offset[size_t(v) * 64];
                for (u64 m = fresh; m; m &= m - 1) { unsigned long lane; _BitScanForward64(&lane, m); row[lane] = uint8_t(local); }
                if (needGen[v] == generation) { u64 hit = need[v] & fresh; if (hit) { remaining -= i64(__popcnt64(hit)); need[v] &= ~hit; } }
                for (u32 m = in[v]; m; m &= m - 1) {
                    int d = Ctz(m), u = v - off[d];
                    if (boxed && !inside(box, u)) continue;
                    u64 open = fresh & ~lanes(u);
                    if (open) push(u, open, bucket + u32((inWet[v] >> d) & 1 ? moat : ground));
                }
            }
            list.clear(); ++bucket;
        }
        for (auto& l : ring) l.clear();
        bucketBase = bucket + ringSize + 1; // keys of discarded entries can never match again
        return true;
    }
    // Deterministic forward descent along lane j (lowest direction first) until stop(node).
    template<class Stop> int descend(int node, int lane, int ground, int moat, const Box& box, Vec<uint8_t>& path, int& g, int& w, Stop stop) {
        const bool boxed = !box.none();
        while (!stop(node)) {
            u32 here = distance(node, lane); int next = -1;
            for (u32 m = out[node]; m && next < 0; m &= m - 1) {
                int d = Ctz(m), u = node + off[d];
                if ((boxed && !inside(box, u)) || !(lanes(u) & (u64(1) << lane))) continue;
                bool wet = ((outWet[node] >> d) & 1) != 0;
                if (distance(u, lane) + u32(wet ? moat : ground) != here) continue;
                next = u; path.push_back(uint8_t(d)); if (wet) ++w; else ++g;
            }
            if (next < 0 || int(path.size()) > MaxSteps * 4) return -1;
            node = next;
        }
        return node;
    }
};

void Field::solve(const Context& c, int source, bool forward, const std::vector<int>& targets, int ground, int moat, const Box* box) {
    if (++generation == 0) { std::fill(gen.begin(), gen.end(), 0u); std::fill(needGen.begin(), needGen.end(), 0u); generation = 1; }
    const int size = int(ring.size()); i64 pending = 0, remaining = 0;
    for (int t : targets) if (needGen[t] != generation) { needGen[t] = generation; ++remaining; }
    auto relax = [&](int u, u32 value) {
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
            if (forward) { for (u32 m = c.out[v]; m; m &= m - 1) { int d = Ctz(m); if (box && !c.inside(*box, v + c.off[d])) continue; relax(v + c.off[d], bucket + u32((c.outWet[v] >> d) & 1 ? moat : ground)); } }
            else { for (u32 m = c.in[v]; m; m &= m - 1) { int d = Ctz(m); if (box && !c.inside(*box, v - c.off[d])) continue; relax(v - c.off[d], bucket + u32((c.inWet[v] >> d) & 1 ? moat : ground)); } }
        }
        list.clear();
    }
    for (auto& l : ring) l.clear();
}
// Backward field: walk from node toward the source (lowest direction first).
template<class Stop> static int FieldDescend(const Context& c, const Field& f, int node, int ground, int moat, Vec<uint8_t>& path, int& g, int& w, Stop stop) {
    while (!stop(node)) {
        u32 here = f.dist[node]; int next = -1;
        for (u32 m = c.out[node]; m && next < 0; m &= m - 1) {
            int d = Ctz(m), u = node + c.off[d]; if (!f.reached(u)) continue;
            bool wet = ((c.outWet[node] >> d) & 1) != 0;
            if (f.dist[u] + u32(wet ? moat : ground) != here) continue;
            next = u; path.push_back(uint8_t(d)); if (wet) ++w; else ++g;
        }
        if (next < 0 || int(path.size()) > MaxSteps * 4) return -1;
        node = next;
    }
    return node;
}
// Forward field: reconstruct source -> node by walking predecessors backward.
static bool FieldAscend(const Context& c, const Field& f, int node, int source, int ground, int moat, std::vector<uint8_t>& path, int& g, int& w) {
    path.clear();
    while (node != source) {
        u32 here = f.dist[node]; int next = -1;
        for (u32 m = c.in[node]; m && next < 0; m &= m - 1) {
            int d = Ctz(m), u = node - c.off[d]; if (!f.reached(u)) continue;
            bool wet = ((c.inWet[node] >> d) & 1) != 0;
            if (f.dist[u] + u32(wet ? moat : ground) != here) continue;
            next = u; path.push_back(uint8_t(d)); if (wet) ++w; else ++g;
        }
        if (next < 0 || int(path.size()) > MaxSteps * 4) return false;
        node = next;
    }
    std::reverse(path.begin(), path.end());
    return true;
}

using Clock = std::chrono::steady_clock;
static i64 Ns(Clock::time_point a) { return std::chrono::duration_cast<std::chrono::nanoseconds>(Clock::now() - a).count(); }

// Per member of variant B: the full exact route start -> anchor (backward field) with the
// positions where it first enters the narrow and the wide ending box, plus the exact
// route anchor -> slot (forward field). The via-anchor route bounds every ending.
struct Prefix {
    bool ok = false, via = false, moved = false; int narrowLength = 0, wideLength = 0, narrowEntry = -1, wideEntry = -1;
    int gN = 0, wN = 0, gW = 0, wW = 0, gHead = 0, wHead = 0, gTail = 0, wTail = 0; i64 viaCost = 0, tailCost = 0;
    std::vector<uint8_t> head, tail; const Prefix* alt = nullptr; // same member in the parent plan
};
struct Plan { Group p{}; Box narrow, wide; std::vector<int> members; std::vector<Prefix> prefixes; };

struct Batch {
    Context& c; const Unit* units; int count; const Group* groups; int groupCount; int margin, threads;
    void emit(const Unit& u, int status, const uint8_t* path, size_t length, int g, int w, const Group& p) {
        Result r{}; r.index = u.index; r.status = status; r.offset = int(c.output.size());
        if (status == Found && int(length) > MaxSteps) r.status = OverLimit;
        if (r.status == Found) { r.length = int(length); r.ground = g; r.moat = w; r.cost = i64(g) * p.ground + i64(w) * p.moat; c.output.insert(c.output.end(), path, path + length); }
        c.results.push_back(r);
    }
    void emitVia(const Unit& u, const Prefix& x, const Group& p) {
        std::vector<uint8_t> path(x.head); path.insert(path.end(), x.tail.begin(), x.tail.end());
        emit(u, Found, path.data(), path.size(), x.gHead + x.gTail, x.wHead + x.wTail, p);
    }
    // Exact lane solve for units of one group; optional per-member entry points and
    // prefixes (variant B endings). A cheaper via-anchor alternative replaces the box
    // route (ties keep the box route). Units without a lane route go to failed (if given).
    void exact(const std::vector<int>& members, const Group& p, const Box& box, std::vector<int>* failed,
               const std::vector<int>* entries = nullptr, const std::vector<const Prefix*>* prefixes = nullptr, bool wide = false) {
        std::vector<int> slots;
        for (int i : members) if (std::find(slots.begin(), slots.end(), units[i].slot) == slots.end()) slots.push_back(units[i].slot);
        // Chunk by 8x8 tiles: nearby slots keep the per-node lane spread small.
        std::stable_sort(slots.begin(), slots.end(), [&](int a, int b) {
            return (a / c.width / 8) * 4096 + (a % c.width / 8) < (b / c.width / 8) * 4096 + (b % c.width / 8);
        });
        std::vector<std::pair<int,int>> spans{{0, int(slots.size())}};
        while (!spans.empty()) {
            auto span = spans.back(); spans.pop_back();
            if (span.second - span.first > 64) { spans.push_back({span.first + 64, span.second}); span.second = span.first + 64; }
            int lanesCount = span.second - span.first;
            std::vector<std::pair<int,int>> needs, owners; // (from, lane), (member, lane)
            for (size_t k = 0; k < members.size(); ++k) {
                int i = members[k];
                auto it = std::find(slots.begin() + span.first, slots.begin() + span.second, units[i].slot);
                if (it == slots.begin() + span.second) continue;
                int lane = int(it - (slots.begin() + span.first)); int from = entries ? (*entries)[k] : units[i].start;
                needs.push_back({from, lane}); owners.push_back({int(k), lane});
            }
            ++c.stats[Blocks];
            auto t0 = Clock::now();
            bool ok = c.solve(slots.data() + span.first, lanesCount, needs, box, p.ground, p.moat);
            c.stats[LaneNs] += Ns(t0);
            if (!ok) { ++c.stats[Splits]; int mid = span.first + lanesCount / 2; spans.push_back({mid, span.second}); spans.push_back({span.first, mid}); continue; }
            auto t1 = Clock::now();
            for (auto& owner : owners) {
                int k = owner.first, lane = owner.second; const Unit& u = units[members[k]];
                int from = entries ? (*entries)[k] : u.start;
                const Prefix* x = prefixes ? (*prefixes)[k] : nullptr;
                Vec<uint8_t> path(Alloc<uint8_t>(&c.memory)); int g = 0, w = 0;
                if (x) { int length = wide ? x->wideLength : x->narrowLength; path.assign(x->head.begin(), x->head.begin() + length); g = wide ? x->gW : x->gN; w = wide ? x->wW : x->wN; }
                if (!(c.lanes(from) & (u64(1) << lane))) { if (failed) failed->push_back(members[k]); else emit(u, Unreachable, nullptr, 0, 0, 0, p); continue; }
                int end = c.descend(from, lane, p.ground, p.moat, box, path, g, w, [&](int node) { return node == u.slot; });
                if (end < 0) { if (failed) failed->push_back(members[k]); else emit(u, OverLimit, nullptr, 0, 0, 0, p); continue; }
                if (const Prefix* v = x ? bestVia(x) : nullptr) if (v->viaCost < i64(g) * p.ground + i64(w) * p.moat) { ++c.stats[ViaChosen]; emitVia(u, *v, p); continue; }
                emit(u, Found, path.data(), path.size(), g, w, p);
            }
            c.stats[ExtractNs] += Ns(t1);
        }
    }
    void runExact(const std::vector<std::vector<int>>& members) {
        for (int g = 0; g < groupCount; ++g) {
            if (members[g].empty()) continue;
            auto t = Clock::now(); exact(members[g], groups[g], Box(), nullptr);
            c.stats[MaxGroupNs] = std::max(c.stats[MaxGroupNs], Ns(t));
        }
    }
    Plan makePlan(const Group& p, std::vector<int> members) {
        Plan plan; plan.p = p; plan.members = std::move(members); plan.prefixes.resize(plan.members.size());
        auto grow = [&](Box b, int m) { b.x0 = std::max(0, b.x0 - m); b.y0 = std::max(0, b.y0 - m); b.x1 = std::min(c.width - 1, b.x1 + m); b.y1 = std::min(c.height - 1, b.y1 + m); return b; };
        Box box; box.x0 = p.anchor % c.width; box.x1 = box.x0; box.y0 = p.anchor / c.width; box.y1 = box.y0;
        for (int i : plan.members) { int x = units[i].slot % c.width, y = units[i].slot / c.width; box.x0 = std::min(box.x0, x); box.y0 = std::min(box.y0, y); box.x1 = std::max(box.x1, x); box.y1 = std::max(box.y1, y); }
        plan.narrow = grow(box, margin); plan.wide = grow(box, margin + 16);
        return plan;
    }
    int chebyshev(int a, int b) const { return std::max(std::abs(a % c.width - b % c.width), std::abs(a / c.width - b / c.width)); }
    // Phase 1 for the given plans, one task per distinct (anchor, profile) on worker
    // threads: a backward field (all starts) and an early-stopping forward field (all
    // slots). Every task writes only its own plans: results do not depend on threads.
    void fields(std::vector<Plan>& plans) {
        std::map<std::tuple<int,int,int>, std::vector<int>> keyed;
        for (size_t g = 0; g < plans.size(); ++g) if (!plans[g].members.empty()) keyed[{plans[g].p.anchor, plans[g].p.ground, plans[g].p.moat}].push_back(int(g));
        std::vector<std::vector<int>> tasks; for (auto& entry : keyed) tasks.push_back(entry.second);
        c.stats[FieldTasks] += i64(tasks.size());
        std::vector<i64> taskNs(tasks.size(), 0);
        std::atomic<int> next{0}; std::atomic<int> failure{0};
        auto work = [&](int worker) {
            try {
                Field& f = *c.fields[worker];
                for (;;) {
                    int t = next.fetch_add(1); if (t >= int(tasks.size())) break;
                    auto t0 = Clock::now(); const Group& p = plans[tasks[t][0]].p;
                    std::vector<int> targets; for (int g : tasks[t]) for (int i : plans[g].members) targets.push_back(units[i].start);
                    f.solve(c, p.anchor, false, targets, p.ground, p.moat);
                    Vec<uint8_t> path(Alloc<uint8_t>(&c.memory));
                    for (int g : tasks[t]) {
                        Plan& plan = plans[g];
                        for (size_t k = 0; k < plan.members.size(); ++k) {
                            int s = units[plan.members[k]].start; Prefix& x = plan.prefixes[k];
                            if (!f.reached(s)) continue;
                            path.clear(); x.gHead = x.wHead = 0;
                            if (FieldDescend(c, f, s, p.ground, p.moat, path, x.gHead, x.wHead, [&](int v) { return v == p.anchor; }) < 0) continue;
                            x.head.assign(path.begin(), path.end()); x.viaCost = i64(x.gHead) * p.ground + i64(x.wHead) * p.moat;
                            // Box entries are prefixes of the same deterministic descent.
                            int node = s, g0 = 0, w0 = 0;
                            for (size_t step = 0;; ++step) {
                                if (x.wideEntry < 0 && c.inside(plan.wide, node)) { x.wideEntry = node; x.wideLength = int(step); x.gW = g0; x.wW = w0; }
                                if (x.narrowEntry < 0 && c.inside(plan.narrow, node)) { x.narrowEntry = node; x.narrowLength = int(step); x.gN = g0; x.wN = w0; }
                                if (x.narrowEntry >= 0 || step == x.head.size()) break;
                                int d = x.head[step]; if ((c.outWet[node] >> d) & 1) ++w0; else ++g0; node += c.off[d];
                            }
                            x.ok = x.narrowEntry >= 0 && x.wideEntry >= 0;
                        }
                    }
                    std::vector<int> slots; for (int g : tasks[t]) for (size_t k = 0; k < plans[g].members.size(); ++k) if (plans[g].prefixes[k].ok) slots.push_back(units[plans[g].members[k]].slot);
                    if (!slots.empty()) {
                        f.solve(c, p.anchor, true, slots, p.ground, p.moat);
                        std::vector<uint8_t> tail;
                        for (int g : tasks[t]) for (size_t k = 0; k < plans[g].members.size(); ++k) {
                            Prefix& x = plans[g].prefixes[k]; int slot = units[plans[g].members[k]].slot;
                            if (!x.ok || !f.reached(slot)) continue;
                            x.gTail = x.wTail = 0;
                            if (!FieldAscend(c, f, slot, p.anchor, p.ground, p.moat, tail, x.gTail, x.wTail)) continue;
                            x.tail = tail; x.via = true; x.tailCost = i64(x.gTail) * p.ground + i64(x.wTail) * p.moat; x.viaCost += x.tailCost;
                        }
                    }
                    taskNs[t] = Ns(t0);
                }
            }
            catch (Exhausted&) { failure = 2; } catch (std::bad_alloc&) { failure = 2; } catch (...) { failure = 4; }
        };
        { std::vector<std::thread> pool; for (int w = 1; w < threads; ++w) pool.emplace_back(work, w); work(0); for (auto& t : pool) t.join(); }
        if (failure == 2) throw Exhausted(); if (failure) throw 0;
        for (i64 ns : taskNs) c.stats[MaxTaskNs] = std::max(c.stats[MaxTaskNs], ns);
        for (auto& f : c.fields) { c.stats[SettleEvents] += f->settles; c.stats[Entries] += f->entries; f->settles = f->entries = 0; }
    }
    // Phase 2 for one plan: exact box-restricted endings, narrow box first, then the
    // widened box. Members handed to a sub-plan are skipped.
    void endings(Plan& plan, std::vector<int>& fallback) {
        std::vector<int> list, entries, retry; std::vector<const Prefix*> pre;
        for (size_t k = 0; k < plan.members.size(); ++k) {
            const Prefix& x = plan.prefixes[k];
            if (x.moved) continue;
            if (!x.ok) { fallback.push_back(plan.members[k]); continue; }
            list.push_back(plan.members[k]); entries.push_back(x.narrowEntry); pre.push_back(&x);
        }
        exact(list, plan.p, plan.narrow, &retry, &entries, &pre, false);
        if (retry.empty()) return;
        list.clear(); entries.clear(); pre.clear();
        for (int i : retry) { const Prefix& x = plan.prefixes[member(plan, i)]; list.push_back(i); entries.push_back(x.wideEntry); pre.push_back(&x); }
        exact(list, plan.p, plan.wide, &fallback, &entries, &pre, true);
    }
    static size_t member(const Plan& plan, int i) { return size_t(std::find(plan.members.begin(), plan.members.end(), i) - plan.members.begin()); }
    // Cheapest via-anchor route along a member's plan chain (own plan first on ties).
    static const Prefix* bestVia(const Prefix* x) {
        const Prefix* best = nullptr;
        for (const Prefix* y = x; y; y = y->alt) if (y->via && (!best || y->viaCost < best->viaCost)) best = y;
        return best;
    }
    void runAnchor(const std::vector<std::vector<int>>& members) {
        // Level 0 holds the requested groups; later levels hold far-slot sub-plans. A
        // finished level is never modified again, so alt pointers into it stay valid.
        std::deque<std::vector<Plan>> levels(1);
        for (int g = 0; g < groupCount; ++g) levels[0].push_back(makePlan(groups[g], members[g]));
        auto t1 = Clock::now();
        fields(levels[0]);
        // Far slots: spatially inside the formation but far from its anchor in the graph
        // (e.g. behind a wall). They form a sub-plan with their own anchor (one more field);
        // repeated up to three levels so slots behind different obstacles separate.
        for (int depth = 0; depth < 3; ++depth) {
            std::vector<Plan> next;
            for (Plan& plan : levels.back()) {
                std::vector<int> far; std::vector<Prefix*> origin;
                for (size_t k = 0; k < plan.members.size(); ++k) {
                    Prefix& x = plan.prefixes[k]; int slot = units[plan.members[k]].slot;
                    if (x.via && x.tailCost > i64(plan.p.ground) * (2 * chebyshev(plan.p.anchor, slot) + 8)) { x.moved = true; far.push_back(plan.members[k]); origin.push_back(&x); }
                }
                if (far.empty()) continue;
                // Sub-anchor: the far slot with the smallest Chebyshev sum to the other far
                // slots; its own tail is zero, so every level removes at least one unit.
                int best = units[far[0]].slot; i64 bestSum = -1;
                for (int a : far) { i64 sum = 0; for (int b : far) sum += chebyshev(units[a].slot, units[b].slot); if (bestSum < 0 || sum < bestSum) { bestSum = sum; best = units[a].slot; } }
                Group q = plan.p; q.anchor = best;
                Plan sub = makePlan(q, far);
                for (size_t k = 0; k < far.size(); ++k) sub.prefixes[k].alt = origin[k];
                next.push_back(std::move(sub)); c.stats[FarUnits] += i64(far.size());
            }
            if (next.empty()) break;
            c.stats[SubPlans] += i64(next.size());
            levels.push_back(std::move(next));
            fields(levels.back());
        }
        c.stats[AnchorNs] = Ns(t1);
        auto t2 = Clock::now();
        std::vector<std::vector<std::vector<int>>> fallback(levels.size());
        for (size_t l = 0; l < levels.size(); ++l) { fallback[l].resize(levels[l].size()); for (size_t g = 0; g < levels[l].size(); ++g) endings(levels[l][g], fallback[l][g]); }
        c.stats[LocalNs] = Ns(t2);
        // 3) Units without a box ending take their cheapest via-anchor route along the
        //    plan chain; only units without any use the full lane solver, which also
        //    proves unreachability.
        auto t3 = Clock::now();
        for (size_t l = 0; l < levels.size(); ++l) for (size_t g = 0; g < levels[l].size(); ++g) {
            const std::vector<int>& list = fallback[l][g]; const Plan& plan = levels[l][g];
            if (list.empty()) continue;
            c.stats[Fallbacks] += i64(list.size());
            std::vector<int> exactList;
            for (int i : list) { const Prefix* v = bestVia(&plan.prefixes[member(plan, i)]); if (v) emitVia(units[i], *v, plan.p); else exactList.push_back(i); }
            c.stats[ExactFallbacks] += i64(exactList.size());
            if (!exactList.empty()) exact(exactList, plan.p, Box(), nullptr);
        }
        c.stats[FallbackNs] = Ns(t3);
    }
    // Variant "B per unit" (integration shape for MoatMove): anchor fields are prepared
    // first (parallel, one per distinct anchor and profile); afterwards every unit is
    // answered alone, in fixed order, exactly as TryFastEncoded would ask for it later
    // with its slot only known at that moment.
    struct Stored { int anchor = -1; std::vector<u32> dist; };
    static constexpr u32 Unset = 0xFFFFFFFFu;
    void store(const Field& f, Stored& s, int anchor) {
        s.anchor = anchor; s.dist.assign(size_t(c.n), Unset);
        for (int v = 0; v < c.n; ++v) if (f.reached(v)) s.dist[v] = f.dist[v];
    }
    template<class Stop> int storedDescend(const Stored& s, int node, int ground, int moat, std::vector<uint8_t>& path, int& g, int& w, Stop stop) {
        while (!stop(node)) {
            u32 here = s.dist[node]; int next = -1;
            for (u32 m = c.out[node]; m && next < 0; m &= m - 1) {
                int d = Ctz(m), u = node + c.off[d]; if (s.dist[u] == Unset) continue;
                bool wet = ((c.outWet[node] >> d) & 1) != 0;
                if (s.dist[u] + u32(wet ? moat : ground) != here) continue;
                next = u; path.push_back(uint8_t(d)); if (wet) ++w; else ++g;
            }
            if (next < 0 || int(path.size()) > MaxSteps * 4) return -1;
            node = next;
        }
        return node;
    }
    void runPerUnit(const std::vector<std::vector<int>>& members) {
        // 1) Main anchor fields, keyed by (anchor, profile), computed on worker threads.
        std::map<std::tuple<int,int,int>, std::vector<int>> keyed;
        for (int g = 0; g < groupCount; ++g) if (!members[g].empty()) keyed[{groups[g].anchor, groups[g].ground, groups[g].moat}].push_back(g);
        std::vector<std::vector<int>> tasks; std::vector<int> taskOf(groupCount, -1);
        for (auto& entry : keyed) { for (int g : entry.second) taskOf[g] = int(tasks.size()); tasks.push_back(entry.second); }
        c.stats[FieldTasks] = i64(tasks.size());
        std::vector<std::vector<Stored>> anchors(tasks.size());
        std::atomic<int> next{0}; std::atomic<int> failure{0};
        auto work = [&](int worker) {
            try {
                Field& f = *c.fields[worker];
                for (;;) {
                    int t = next.fetch_add(1); if (t >= int(tasks.size())) break;
                    const Group& p = groups[tasks[t][0]];
                    std::vector<int> targets; for (int g : tasks[t]) for (int i : members[g]) targets.push_back(units[i].start);
                    f.solve(c, p.anchor, false, targets, p.ground, p.moat);
                    anchors[t].emplace_back(); store(f, anchors[t].back(), p.anchor);
                }
            }
            catch (Exhausted&) { failure = 2; } catch (std::bad_alloc&) { failure = 2; } catch (...) { failure = 4; }
        };
        auto t1 = Clock::now();
        { std::vector<std::thread> pool; for (int w = 1; w < threads; ++w) pool.emplace_back(work, w); work(0); for (auto& t : pool) t.join(); }
        c.stats[AnchorNs] = Ns(t1);
        if (failure == 2) throw Exhausted(); if (failure) throw 0;
        for (auto& f : c.fields) { c.stats[SettleEvents] += f->settles; c.stats[Entries] += f->entries; f->settles = f->entries = 0; }
        // 2) Per-unit answers, serial. Anchors are tried in creation order (main anchor
        //    first). An anchor is usable when the slot is graph-near to it inside the
        //    anchor-slot box; otherwise a sub-anchor field at the slot is created once and
        //    reused by later units of the same task.
        auto t2 = Clock::now(); i64 subNs = 0;
        Field& local = *c.fields[0];
        std::vector<uint8_t> head, tail;
        for (int g = 0; g < groupCount; ++g) {
            if (members[g].empty()) continue;
            const Group& p = groups[g]; std::vector<Stored>& list = anchors[taskOf[g]];
            for (int i : members[g]) {
                const Unit& u = units[i]; bool done = false;
                for (size_t a = 0; a < list.size() && !done; ++a) {
                    const Stored& s = list[a];
                    if (s.dist[u.start] == Unset) continue;
                    Box box; box.x0 = std::min(s.anchor % c.width, u.slot % c.width); box.x1 = std::max(s.anchor % c.width, u.slot % c.width);
                    box.y0 = std::min(s.anchor / c.width, u.slot / c.width); box.y1 = std::max(s.anchor / c.width, u.slot / c.width);
                    box.x0 = std::max(0, box.x0 - margin); box.y0 = std::max(0, box.y0 - margin); box.x1 = std::min(c.width - 1, box.x1 + margin); box.y1 = std::min(c.height - 1, box.y1 + margin);
                    head.clear(); int gH = 0, wH = 0;
                    int entry = storedDescend(s, u.start, p.ground, p.moat, head, gH, wH, [&](int v) { return c.inside(box, v); });
                    if (entry < 0) continue;
                    local.solve(c, u.slot, false, std::vector<int>{s.anchor, entry}, p.ground, p.moat, &box);
                    if (!local.reached(s.anchor) || local.dist[s.anchor] > u32(p.ground) * u32(2 * chebyshev(s.anchor, u.slot) + 8)) continue;
                    i64 boxCost = local.reached(entry) ? i64(gH) * p.ground + i64(wH) * p.moat + local.dist[entry] : -1;
                    i64 viaCost = i64(s.dist[u.start]) + local.dist[s.anchor];
                    int g2 = gH, w2 = wH; tail.clear();
                    if (boxCost >= 0 && boxCost <= viaCost) {
                        Vec<uint8_t> rest(Alloc<uint8_t>(&c.memory));
                        if (FieldDescend(c, local, entry, p.ground, p.moat, rest, g2, w2, [&](int v) { return v == u.slot; }) < 0) continue;
                        head.insert(head.end(), rest.begin(), rest.end());
                    } else {
                        ++c.stats[ViaChosen];
                        if (storedDescend(s, entry, p.ground, p.moat, head, g2, w2, [&](int v) { return v == s.anchor; }) < 0) continue;
                        Vec<uint8_t> rest(Alloc<uint8_t>(&c.memory));
                        if (FieldDescend(c, local, s.anchor, p.ground, p.moat, rest, g2, w2, [&](int v) { return v == u.slot; }) < 0) continue;
                        head.insert(head.end(), rest.begin(), rest.end());
                    }
                    if (a > 0) ++c.stats[FarUnits];
                    emit(u, Found, head.data(), head.size(), g2, w2, p); done = true;
                }
                if (done) continue;
                // No usable anchor: an exact field at this slot becomes a new sub-anchor.
                auto t3 = Clock::now();
                std::vector<int> targets; for (int j : members[g]) targets.push_back(units[j].start);
                local.solve(c, u.slot, false, targets, p.ground, p.moat);
                list.emplace_back(); store(local, list.back(), u.slot); ++c.stats[SubPlans];
                subNs += Ns(t3);
                const Stored& s = list.back(); head.clear(); int g2 = 0, w2 = 0;
                if (s.dist[u.start] == Unset) { emit(u, Unreachable, nullptr, 0, 0, 0, p); continue; }
                if (storedDescend(s, u.start, p.ground, p.moat, head, g2, w2, [&](int v) { return v == u.slot; }) < 0) { emit(u, OverLimit, nullptr, 0, 0, 0, p); continue; }
                emit(u, Found, head.data(), head.size(), g2, w2, p);
            }
        }
        c.stats[FallbackNs] = subNs; c.stats[LocalNs] = Ns(t2) - subNs;
        c.stats[SettleEvents] += local.settles; c.stats[Entries] += local.entries; local.settles = local.entries = 0;
    }
    void run(int mode) {
        int maximumCost = 1;
        for (int g = 0; g < groupCount; ++g) maximumCost = std::max(maximumCost, std::max(groups[g].ground, groups[g].moat));
        c.prepare(maximumCost, threads);
        std::vector<std::vector<int>> members(groupCount);
        for (int i = 0; i < count; ++i) members[units[i].group].push_back(i);
        if (mode == 0) runExact(members); else if (mode == 1) runAnchor(members); else runPerUnit(members);
    }
};
}

using namespace gp;
extern "C" __declspec(dllexport) int gp_contract() { return 0x0300101B; }
extern "C" __declspec(dllexport) int gp_create(int width, int height, const uint8_t* valid, const u32* edges, i64 limit, Context** out) {
    *out = nullptr;
    if (width < 1 || height < 1 || i64(width) * height > 640000 || !valid || !edges) return 3;
    try { *out = new Context(width, height, valid, edges, limit); return 0; }
    catch (Exhausted&) { return 2; } catch (std::bad_alloc&) { return 2; } catch (...) { return 4; }
}
extern "C" __declspec(dllexport) void gp_destroy(Context* c) { delete c; }
// mode 0 = exact lanes (A), mode 1 = anchor + exact box endings (B), mode 2 = B per unit.
extern "C" __declspec(dllexport) int gp_run(Context* c, int mode, const Unit* units, int count, const Group* groups, int groupCount, int margin, int threads, int* outputBytes) {
    if (!c || !units || !groups || count < 0 || groupCount < 1 || margin < 0 || threads < 1 || threads > 64 || mode < 0 || mode > 2) return 3;
    for (int i = 0; i < count; ++i) {
        const Unit& u = units[i];
        if (u.group < 0 || u.group >= groupCount || u.start < 0 || u.start >= c->n || u.slot < 0 || u.slot >= c->n || !c->valid[u.start] || !c->valid[u.slot]) return 3;
    }
    for (int g = 0; g < groupCount; ++g) if (groups[g].ground < 1 || groups[g].moat < 1 || groups[g].ground > 63 || groups[g].moat > 63 || groups[g].anchor < 0 || groups[g].anchor >= c->n || !c->valid[groups[g].anchor]) return 3;
    try {
        for (auto& s : c->stats) s = 0;
        c->output.clear(); c->results.clear(); c->results.reserve(size_t(count));
        Batch{*c, units, count, groups, groupCount, margin, threads}.run(mode);
        c->stats[PeakBytes] = c->memory.peak; c->stats[Allocations] = c->memory.calls; c->stats[Threads] = threads;
        *outputBytes = int(c->output.size());
        return int(c->results.size()) == count ? 0 : 5;
    }
    catch (Exhausted&) { return 2; } catch (std::bad_alloc&) { return 2; } catch (...) { return 4; }
}
extern "C" __declspec(dllexport) int gp_copy(Context* c, Result* results, int count, uint8_t* bytes, int capacity) {
    if (!c || count != int(c->results.size()) || capacity < int(c->output.size())) return 3;
    std::copy(c->results.begin(), c->results.end(), results);
    std::copy(c->output.begin(), c->output.end(), bytes);
    return 0;
}
extern "C" __declspec(dllexport) int gp_stats(Context* c, i64* data, int count) {
    if (!c || count < StatCount) return 3;
    std::copy(c->stats, c->stats + StatCount, data); return 0;
}
