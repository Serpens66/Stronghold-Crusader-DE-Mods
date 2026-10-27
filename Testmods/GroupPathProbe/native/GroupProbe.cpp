// Offline group-planning probe. Original implementation, no game imports.
// Variant A ("lanes"): exact per-slot fields, up to 64 slots per bit-parallel
// backward Dial search. Variant B ("anchor"): one exact field to the group anchor,
// then exact box-restricted endings to the individual slots.
#ifndef SERP_DEVDIAG
#error Group probe requires an explicit diagnostic build.
#endif
#include <algorithm>
#include <chrono>
#include <cstdint>
#include <intrin.h>
#include <new>
#include <vector>

namespace gp {
using i64 = int64_t;
using u64 = uint64_t;
using u32 = uint32_t;
constexpr int dx[8] = {0,1,1,1,0,-1,-1,-1}, dy[8] = {-1,-1,0,1,1,1,0,-1};
constexpr int MaxSteps = 2000;
struct Exhausted {};
struct Memory {
    i64 maximum, bytes = 0, peak = 0, calls = 0;
    void take(size_t n) { if (n > size_t(maximum - bytes)) throw Exhausted(); bytes += i64(n); peak = std::max(peak, bytes); ++calls; }
    void give(size_t n) { bytes -= i64(n); }
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
enum StatIndex { SettleEvents, Entries, Blocks, Splits, Fallbacks, FieldNs, ExtractNs, MaxGroupNs, PeakBytes, Allocations, LocalNs, AnchorNs, StatCount };

struct Box { int x0 = 0, y0 = 0, x1 = -1, y1 = -1; bool none() const { return x1 < x0; } };
struct Entry { int node; u64 mask; };

struct Context {
    Memory memory; int width, height, n;
    Vec<uint8_t> valid; Vec<u32> edges;
    // Per-search state; generation stamps avoid clearing between blocks.
    Vec<u32> gen, needGen, base; Vec<u64> settled, need; Vec<uint8_t> offset;
    Vec<u32> ringBucket; Vec<int> ringEntry; Vec<Vec<Entry>> ring;
    u32 generation = 0, bucketBase = 1; int ringSize = 0;
    i64 stats[StatCount] = {};
    Vec<uint8_t> output; Vec<Result> results;
    Context(int w, int h, const uint8_t* v, const u32* e, i64 limit) : width(w), height(h), n(w * h),
        valid(Alloc<uint8_t>(&memory)), edges(Alloc<u32>(&memory)), gen(Alloc<u32>(&memory)), needGen(Alloc<u32>(&memory)), base(Alloc<u32>(&memory)),
        settled(Alloc<u64>(&memory)), need(Alloc<u64>(&memory)), offset(Alloc<uint8_t>(&memory)), ringBucket(Alloc<u32>(&memory)), ringEntry(Alloc<int>(&memory)),
        ring(Alloc<Vec<Entry>>(&memory)), output(Alloc<uint8_t>(&memory)), results(Alloc<Result>(&memory)) {
        memory.maximum = limit;
        valid.assign(v, v + n); edges.assign(e, e + n);
        gen.assign(n, 0); needGen.assign(n, 0); base.assign(n, 0); settled.assign(n, 0); need.assign(n, 0); offset.assign(size_t(n) * 64, 0);
    }
    bool inside(const Box& b, int node) const { if (b.none()) return true; int x = node % width, y = node / width; return x >= b.x0 && x <= b.x1 && y >= b.y0 && y <= b.y1; }
    int neighbor(int node, int d) const { int x = node % width + dx[d], y = node / width + dy[d]; return (unsigned)x < unsigned(width) && (unsigned)y < unsigned(height) ? y * width + x : -1; }
    void prepareRing(int maximumCost) {
        int size = maximumCost + 1;
        if (size == ringSize) return;
        ringSize = size; ringBucket.assign(size_t(n) * size, 0); ringEntry.assign(size_t(n) * size, -1);
        ring.clear(); for (int i = 0; i < size; ++i) ring.emplace_back(Alloc<Entry>(&memory));
        single_ring.clear(); for (int i = 0; i < size; ++i) single_ring.emplace_back(Alloc<Entry>(&memory));
        bucketBase = 1;
    }
    bool single = false; // last solve used the one-lane kernel (no offsets written)
    u64 lanes(int node) const { return gen[node] == generation ? settled[node] : 0; }
    // Exact distance of a settled lane; callers only read lanes reported by lanes().
    u32 distance(int node, int lane) const { return single ? base[node] : base[node] + offset[size_t(node) * 64 + lane]; }

    // One-lane Dial search with tentative distances; avoids the lane merge table and
    // per-lane offsets. Same exactness argument: buckets pop in nondecreasing order.
    void solveSingle(int slot, const std::vector<std::pair<int,int>>& needs, const Box& box, int ground, int moat) {
        if (++generation == 0) { std::fill(gen.begin(), gen.end(), 0u); std::fill(needGen.begin(), needGen.end(), 0u); generation = 1; }
        single = true; i64 remaining = 0, pending = 0;
        for (auto& need0 : needs) { int s = need0.first; if (needGen[s] != generation) { needGen[s] = generation; need[s] = 1; ++remaining; } }
        // gen marks "tentative distance present"; settled bit 1 marks final.
        auto relax = [&](int node, u32 value) {
            if (gen[node] == generation) { if (settled[node] || base[node] <= value) return; }
            else { gen[node] = generation; settled[node] = 0; }
            base[node] = value; single_ring[value % ringSize].push_back(Entry{node, value}); ++pending; ++stats[Entries];
        };
        if (valid[slot] && inside(box, slot)) relax(slot, 0);
        for (u32 bucket = 0; pending > 0 && remaining > 0; ++bucket) {
            Vec<Entry>& list = single_ring[bucket % ringSize];
            for (size_t i = 0; i < list.size(); ++i) {
                Entry item = list[i]; --pending; int v = item.node;
                if (settled[v] || base[v] != u32(item.mask)) continue;
                settled[v] = 1; ++stats[SettleEvents];
                if (needGen[v] == generation && need[v]) { need[v] = 0; --remaining; }
                for (int d = 0; d < 8; ++d) {
                    int u = neighbor(v, (d + 4) & 7);
                    if (u < 0 || !valid[u] || !inside(box, u)) continue;
                    u32 word = edges[u];
                    if (word & (1u << d)) relax(u, bucket + u32((word & (1u << (8 + d))) ? moat : ground));
                }
            }
            list.clear();
        }
        for (auto& l : single_ring) l.clear();
    }
    Vec<Vec<Entry>> single_ring{Alloc<Vec<Entry>>(&memory)};

    // Bit-parallel backward Dial search. Lane j is seeded at slots[j]; a lane settles at
    // node v exactly once, in increasing distance order, so the first settle is optimal.
    // Returns false only when a lane's distance exceeds the 8-bit per-node offset.
    bool solve(const int* slots, int count, const std::vector<std::pair<int,int>>& needs, const Box& box, int ground, int moat) {
        if (++generation == 0) { std::fill(gen.begin(), gen.end(), 0u); std::fill(needGen.begin(), needGen.end(), 0u); generation = 1; }
        if (bucketBase > 0xF0000000u) { std::fill(ringBucket.begin(), ringBucket.end(), 0u); bucketBase = 1; }
        single = false;
        const u32 start = bucketBase; i64 pending = 0, remaining = 0;
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
                for (int d = 0; d < 8; ++d) {
                    int u = neighbor(v, (d + 4) & 7);
                    if (u < 0 || !valid[u] || !inside(box, u)) continue;
                    u32 word = edges[u];
                    if (!(word & (1u << d))) continue;
                    u64 open = fresh & ~lanes(u);
                    if (open) push(u, open, bucket + u32((word & (1u << (8 + d))) ? moat : ground));
                }
            }
            list.clear(); ++bucket;
        }
        for (auto& l : ring) l.clear();
        bucketBase = bucket + ringSize + 1; // keys of discarded entries can never match again
        return true;
    }
    // Deterministic forward descent along lane j until stop(node) holds.
    template<class Stop> int descend(int node, int lane, int ground, int moat, const Box& box, Vec<uint8_t>& path, int& g, int& w, Stop stop) {
        while (!stop(node)) {
            u32 here = distance(node, lane); int next = -1;
            for (int d = 0; d < 8 && next < 0; ++d) {
                if (!(edges[node] & (1u << d))) continue;
                int u = neighbor(node, d);
                if (u < 0 || !valid[u] || !inside(box, u) || !(lanes(u) & (u64(1) << lane))) continue;
                bool wet = (edges[node] & (1u << (8 + d))) != 0; u32 cost = u32(wet ? moat : ground);
                if (distance(u, lane) + cost != here) continue;
                next = u; path.push_back(uint8_t(d)); if (wet) ++w; else ++g;
            }
            if (next < 0) return -1; // unreachable by construction; reported as contract failure
            node = next;
            if (int(path.size()) > MaxSteps * 4) return -1;
        }
        return node;
    }
};

using Clock = std::chrono::steady_clock;
static i64 Ns(Clock::time_point a) { return std::chrono::duration_cast<std::chrono::nanoseconds>(Clock::now() - a).count(); }

struct Batch {
    Context& c; const Unit* units; int count; const Group* groups; int groupCount; int margin;
    std::vector<int> order;
    void emit(const Unit& u, int status, const Vec<uint8_t>& path, int g, int w, const Group& p) {
        Result r{}; r.index = u.index; r.status = status; r.offset = int(c.output.size());
        if (status == Found && int(path.size()) > MaxSteps) r.status = OverLimit;
        if (r.status == Found) { r.length = int(path.size()); r.ground = g; r.moat = w; r.cost = i64(g) * p.ground + i64(w) * p.moat; c.output.insert(c.output.end(), path.begin(), path.end()); }
        c.results.push_back(r);
    }
    // Exact solve for the given units of one group, chunking distinct slots into lanes.
    void exact(const std::vector<int>& members, const Group& p, const Box& box, std::vector<int>* failed,
               const std::vector<int>* entries = nullptr, const std::vector<Vec<uint8_t>>* prefixes = nullptr, const std::vector<std::pair<int,int>>* prefixCounts = nullptr) {
        std::vector<int> slots;
        for (int i : members) if (std::find(slots.begin(), slots.end(), units[i].slot) == slots.end()) slots.push_back(units[i].slot);
        // Chunk by 8x8 tiles: nearby slots keep the per-node lane spread small.
        std::stable_sort(slots.begin(), slots.end(), [&](int a, int b) {
            int ta = (a / c.width / 8) * 4096 + (a % c.width / 8), tb = (b / c.width / 8) * 4096 + (b % c.width / 8);
            return ta < tb;
        });
        std::vector<std::pair<int,int>> spans{{0, int(slots.size())}};
        while (!spans.empty()) {
            auto span = spans.back(); spans.pop_back();
            if (span.second - span.first > 64) { spans.push_back({span.first + 64, span.second}); span.second = span.first + 64; }
            int lanesCount = span.second - span.first;
            std::vector<std::pair<int,int>> needs; std::vector<std::pair<int,int>> owners; // member, lane
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
            c.stats[FieldNs] += Ns(t0);
            if (!ok) { ++c.stats[Splits]; int mid = span.first + lanesCount / 2; spans.push_back({mid, span.second}); spans.push_back({span.first, mid}); continue; }
            auto t1 = Clock::now();
            for (auto& owner : owners) {
                int k = owner.first, lane = owner.second; const Unit& u = units[members[k]];
                int from = entries ? (*entries)[k] : u.start;
                Vec<uint8_t> path(Alloc<uint8_t>(&c.memory)); int g = 0, w = 0;
                if (prefixes) { path = (*prefixes)[k]; g = (*prefixCounts)[k].first; w = (*prefixCounts)[k].second; }
                if (!(c.lanes(from) & (u64(1) << lane))) { if (failed) failed->push_back(members[k]); else emit(u, Unreachable, path, 0, 0, p); continue; }
                int end = c.descend(from, lane, p.ground, p.moat, box, path, g, w, [&](int node) { return node == u.slot; });
                if (end < 0) { if (failed) failed->push_back(members[k]); else emit(u, OverLimit, path, 0, 0, p); continue; }
                emit(u, Found, path, g, w, p);
            }
            c.stats[ExtractNs] += Ns(t1);
        }
    }
    void anchor(const std::vector<int>& members, const Group& p) {
        // 1) exact field to the anchor for all starts of this group.
        Box none; Box box; box.x0 = c.width; box.y0 = c.height; box.x1 = -1; box.y1 = -1;
        for (int i : members) { int x = units[i].slot % c.width, y = units[i].slot / c.width; box.x0 = std::min(box.x0, x); box.y0 = std::min(box.y0, y); box.x1 = std::max(box.x1, x); box.y1 = std::max(box.y1, y); }
        { int x = p.anchor % c.width, y = p.anchor / c.width; box.x0 = std::min(box.x0, x); box.y0 = std::min(box.y0, y); box.x1 = std::max(box.x1, x); box.y1 = std::max(box.y1, y); }
        Box wide = box;
        auto grow = [&](Box& b, int m) { b.x0 = std::max(0, b.x0 - m); b.y0 = std::max(0, b.y0 - m); b.x1 = std::min(c.width - 1, b.x1 + m); b.y1 = std::min(c.height - 1, b.y1 + m); };
        grow(box, margin); grow(wide, margin + 16);
        std::vector<std::pair<int,int>> needs; for (int i : members) needs.push_back({units[i].start, 0});
        auto t0 = Clock::now(); ++c.stats[Blocks];
        c.solveSingle(p.anchor, needs, none, p.ground, p.moat);
        // Prefixes for both ending boxes are taken while the anchor field is still live.
        struct Prefix { std::vector<int> units, entries; std::vector<Vec<uint8_t>> paths; std::vector<std::pair<int,int>> counts; };
        Prefix narrow, broad; std::vector<int> fallback;
        auto take = [&](Prefix& into, const Box& b, int i) {
            Vec<uint8_t> path(Alloc<uint8_t>(&c.memory)); int g = 0, w = 0;
            int entry = c.descend(units[i].start, 0, p.ground, p.moat, none, path, g, w, [&](int node) { return c.inside(b, node); });
            if (entry < 0) return false;
            into.units.push_back(i); into.entries.push_back(entry); into.paths.push_back(std::move(path)); into.counts.push_back({g, w}); return true;
        };
        for (int i : members) {
            if (!(c.lanes(units[i].start) & 1) || !take(narrow, box, i)) { fallback.push_back(i); continue; }
            take(broad, wide, i);
        }
        c.stats[AnchorNs] += Ns(t0);
        // 2) exact box-restricted endings from each entry point to its own slot; a
        //    failed narrow ending retries once in a box widened by 16 tiles.
        auto t1 = Clock::now();
        std::vector<int> retry;
        exact(narrow.units, p, box, &retry, &narrow.entries, &narrow.paths, &narrow.counts);
        if (!retry.empty()) {
            Prefix again;
            for (int i : retry) {
                auto it = std::find(broad.units.begin(), broad.units.end(), i);
                if (it == broad.units.end()) { fallback.push_back(i); continue; }
                size_t k = size_t(it - broad.units.begin());
                again.units.push_back(i); again.entries.push_back(broad.entries[k]); again.paths.push_back(broad.paths[k]); again.counts.push_back(broad.counts[k]);
            }
            exact(again.units, p, wide, &fallback, &again.entries, &again.paths, &again.counts);
        }
        c.stats[LocalNs] += Ns(t1);
        // 3) units without an anchor or box route use the exact unrestricted solver.
        c.stats[Fallbacks] += i64(fallback.size());
        if (!fallback.empty()) exact(fallback, p, none, nullptr);
    }
    void run(int mode) {
        int maximumCost = 1;
        for (int g = 0; g < groupCount; ++g) maximumCost = std::max(maximumCost, std::max(groups[g].ground, groups[g].moat));
        c.prepareRing(maximumCost);
        std::vector<std::vector<int>> members(groupCount);
        for (int i = 0; i < count; ++i) members[units[i].group].push_back(i);
        for (int g = 0; g < groupCount; ++g) {
            if (members[g].empty()) continue;
            auto t = Clock::now();
            if (mode == 0) exact(members[g], groups[g], Box(), nullptr); else anchor(members[g], groups[g]);
            c.stats[MaxGroupNs] = std::max(c.stats[MaxGroupNs], Ns(t));
        }
    }
};
}

using namespace gp;
extern "C" __declspec(dllexport) int gp_contract() { return 0x03001016; }
extern "C" __declspec(dllexport) int gp_create(int width, int height, const uint8_t* valid, const u32* edges, i64 limit, Context** out) {
    *out = nullptr;
    if (width < 1 || height < 1 || i64(width) * height > 640000 || !valid || !edges) return 3;
    try { *out = new Context(width, height, valid, edges, limit); return 0; }
    catch (Exhausted&) { return 2; } catch (std::bad_alloc&) { return 2; } catch (...) { return 4; }
}
extern "C" __declspec(dllexport) void gp_destroy(Context* c) { delete c; }
// mode 0 = exact lanes (A), mode 1 = anchor + exact box endings (B).
extern "C" __declspec(dllexport) int gp_run(Context* c, int mode, const Unit* units, int count, const Group* groups, int groupCount, int margin, int* outputBytes) {
    if (!c || !units || !groups || count < 0 || groupCount < 1 || margin < 0 || (mode != 0 && mode != 1)) return 3;
    for (int i = 0; i < count; ++i) {
        const Unit& u = units[i];
        if (u.group < 0 || u.group >= groupCount || u.start < 0 || u.start >= c->n || u.slot < 0 || u.slot >= c->n || !c->valid[u.start] || !c->valid[u.slot]) return 3;
    }
    for (int g = 0; g < groupCount; ++g) if (groups[g].ground < 1 || groups[g].moat < 1 || groups[g].ground > 63 || groups[g].moat > 63 || groups[g].anchor < 0 || groups[g].anchor >= c->n || !c->valid[groups[g].anchor]) return 3;
    try {
        for (auto& s : c->stats) s = 0;
        c->output.clear(); c->results.clear(); c->results.reserve(size_t(count));
        Batch{*c, units, count, groups, groupCount, margin}.run(mode);
        c->stats[PeakBytes] = c->memory.peak; c->stats[Allocations] = c->memory.calls;
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
    std::copy(c->stats, c->stats + StatCount, data); data[PeakBytes] = c->memory.peak; return 0;
}
