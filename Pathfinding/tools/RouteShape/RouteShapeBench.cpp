// Route shape and cost benchmark for the native anchor core (TODO G10). Offline only: it includes
// the production core source directly, loads no game code and never runs inside Unity.
// Run-RouteShape.ps1 compiles it twice (a baseline core from Git and the working tree core) so that
// identical queries can be compared. Usage:
//   RouteShapeBench <loads.bin|-> <summary.csv> <routes.bin>   measure synthetic (+ exported) loads
//   RouteShapeBench --compare <baseline routes.bin> <candidate routes.bin>
// Quality metrics are in tile space: the camera can rotate, so no screen projection is assumed.
#ifndef ROUTE_SHAPE_KERNEL
#include "../../native/PathfindingAnchor.cpp"
#endif
#include <cmath>
#include <cstdio>
#include <fstream>
#include <iostream>
#include <random>
#include <string>

namespace bench {
using namespace mma;

struct Load { std::string name; int width = 0, height = 0; const std::vector<u32>* words = nullptr; std::vector<int> anchors, starts, targets, groups, ground, moat; };
struct Shape { int pieces = 0, turns = 0; double euclid = 0; };

// Splits a route into maximal digital straight pieces: at most two adjacent directions, the minor
// one only in single steps, and the interior runs of the major one differing by at most one step.
// An L made of a long straight and a long diagonal run is two pieces; a Bresenham line is one.
static Shape Measure(const uint8_t* path, int length) {
    Shape s; if (length <= 0) return s;
    for (int i = 0; i < length; ++i) s.euclid += (path[i] & 1) ? 1.4142135623730951 : 1.0;
    for (int i = 1; i < length; ++i) if (path[i] != path[i - 1]) ++s.turns;
    int i = 0;
    while (i < length) {
        ++s.pieces;
        const int a = path[i]; int b = -1, j = i + 1;
        // Runs inside this piece: (direction, length).
        std::vector<std::pair<int, int>> runs{{a, 1}};
        for (; j < length; ++j) {
            const int d = path[j];
            if (d != a && b < 0) { if (((d - a) & 7) != 1 && ((a - d) & 7) != 1) break; b = d; }
            if (d != a && d != b) break;
            std::vector<std::pair<int, int>> next = runs;
            if (next.back().first == d) ++next.back().second; else next.push_back({d, 1});
            // Major direction: the one with a run longer than one, or the more frequent one.
            int countA = 0, countB = 0, longA = 0, longB = 0;
            for (auto& r : next) { if (r.first == a) { countA += r.second; longA = std::max(longA, r.second); } else { countB += r.second; longB = std::max(longB, r.second); } }
            bool ok = true;
            if (b >= 0) {
                if (longA > 1 && longB > 1) ok = false;
                const int major = longB > 1 || (longA <= 1 && countB > countA) ? b : a;
                int lo = INT32_MAX, hi = 0;
                for (size_t k = 1; k + 1 < next.size(); ++k) if (next[k].first == major) { lo = std::min(lo, next[k].second); hi = std::max(hi, next[k].second); }
                if (lo != INT32_MAX && hi - lo > 1) ok = false;
                // Leading/trailing major runs may be shorter, never more than one step longer.
                if (lo != INT32_MAX && next.front().first == major && next.front().second > hi + 1) ok = false;
                if (lo != INT32_MAX && next.back().first == major && next.back().second > hi + 1) ok = false;
            }
            if (!ok) break;
            runs.swap(next);
        }
        i = j;
    }
    return s;
}

static double Percentile(std::vector<double> values, double p) {
    if (values.empty()) return 0; std::sort(values.begin(), values.end());
    size_t k = size_t(std::ceil(p * double(values.size()))); if (k > 0) --k; return values[std::min(k, values.size() - 1)];
}

struct Record { int status, length; i64 cost; };

struct Accumulator {
    i64 found = 0, unreachable = 0, other = 0, cost = 0, steps = 0, pieces = 0, turns = 0, chebyshev = 0; double euclid = 0, chord = 0;
    std::vector<double> latency;
    void add(const QueryResult& r, const uint8_t* path, int start, int target, int width) {
        if (r.status == Found) {
            ++found; cost += r.cost; steps += r.length;
            Shape s = Measure(path, r.length); pieces += s.pieces; turns += s.turns; euclid += s.euclid;
            const int vx = target % width - start % width, vy = target / width - start / width;
            chord += std::sqrt(double(vx) * vx + double(vy) * vy); chebyshev += std::max(std::abs(vx), std::abs(vy));
        } else if (r.status == Unreachable) ++unreachable; else ++other;
    }
};

static void Fail(const std::string& reason) { throw std::runtime_error(reason); }

// Independent edge check of a returned route against the published words.
static void Validate(const Load& load, int start, int target, int moat, const QueryResult& r, const uint8_t* path) {
    if (r.status != Found) return;
    int v = start; const int w = load.width;
    for (int i = 0; i < r.length; ++i) {
        const int d = path[i]; if (d > 7) Fail(load.name + ": invalid direction");
        const u32 word = (*load.words)[v];
        const bool ok = moat ? (word & (1u << d)) != 0 : ((word & (1u << (8 + d))) != 0 && !(word & (1u << (16 + d))));
        if (!ok) Fail(load.name + ": route uses a missing edge");
        const int x = v % w + dx[d], y = v / w + dy[d];
        if (x < 0 || y < 0 || x >= w || y >= load.height) Fail(load.name + ": route leaves the map");
        v = y * w + x;
    }
    if (v != target) Fail(load.name + ": route endpoint");
}

struct Run {
    std::ofstream& csv; std::ofstream& routes;
    void row(const std::string& load, const std::string& mode, const Accumulator& a, double totalMs, double extraMs, const i64* stats) {
        csv << load << ',' << mode << ',' << a.found << ',' << a.unreachable << ',' << a.other << ',' << a.cost << ',' << a.steps << ','
            << a.pieces << ',' << a.turns << ',' << a.euclid << ',' << a.chord << ',' << a.chebyshev << ','
            << Percentile(a.latency, 0.5) << ',' << Percentile(a.latency, 0.95) << ',' << Percentile(a.latency, 0.99) << ',' << Percentile(a.latency, 1.0) << ','
            << totalMs << ',' << extraMs << ',' << stats[SingleSettles] << ',' << stats[SettleEvents] << ',' << stats[ViaChosen] << ',' << stats[SubAnchors] << ',' << stats[AnchorTooFar] << ',' << stats[PeakBytes];
        // Counters added after the 26 of contract 0x04000011 (e.g. StraightenRoute) as extra columns.
        std::string extra;
        for (int k = 26; k < int(StatCount); ++k) { csv << ',' << stats[k]; extra += " s" + std::to_string(k) + "=" + std::to_string(stats[k]); }
        csv << "\r\n";
        std::cout << load << " " << mode << ": found=" << a.found << " cost=" << a.cost << " pieces=" << a.pieces << " euclid=" << a.euclid
            << " p95=" << Percentile(a.latency, 0.95) << "us max=" << Percentile(a.latency, 1.0) << "us total=" << totalMs << "ms" << extra << "\n";
    }
    void record(const QueryResult& r) { Record x{r.status, r.length, r.cost}; routes.write(reinterpret_cast<const char*>(&x), sizeof x); }
};

static double Us(Clock::time_point a) { return std::chrono::duration<double, std::micro>(Clock::now() - a).count(); }

static void Publish(Context* c, const Load& load) {
    if (mma_mask_begin(c, 1) != Ok) Fail("mask begin");
    int changed = 0;
    if (mma_mask_write(c, 1, 0, int(load.words->size()), load.words->data(), &changed) != Ok) Fail("mask write");
}

static void MeasureLoad(const Load& load, Run& run, int threads, int singleLimit, const std::vector<uint8_t>* penalty = nullptr) {
    for (int profile = 0; profile < 2; ++profile) {
        // profile 0: ground-only (moat cost 0, the common game case); 1: the load's moat profile.
        Context* c = nullptr; if (mma_create(load.width, load.height, 768LL << 20, &c) != Ok) Fail("create");
        Publish(c, load);
        if (penalty) {
            std::vector<int> nodes; std::vector<uint8_t> values;
            for (int i = 0; i < int(penalty->size()); ++i) if ((*penalty)[i]) { nodes.push_back(i); values.push_back((*penalty)[i]); }
            int changed = 0; if (mma_penalty_update(c, 1, nodes.data(), values.data(), int(nodes.size()), &changed) != Ok) Fail("penalty");
        }
        const std::string tag = profile == 0 ? "ground" : "moat";
        std::vector<uint8_t> bytes(MaxSteps);
        i64 stats[256] = {};
        // Group mode: one task per group, then one batch per group (as AnchorRouting does).
        {
            std::map<int, std::vector<int>> members;
            for (int i = 0; i < int(load.starts.size()); ++i) members[load.groups[i]].push_back(i);
            mma_stats(c, stats, StatCount, 1);
            Accumulator a; double stepMs = 0, batchMs = 0;
            std::vector<int> tasks;
            for (auto& m : members) {
                const int first = m.second.front(), moat = profile ? load.moat[first] : 0;
                std::vector<int> starts; for (int i : m.second) starts.push_back(load.starts[i]);
                int id = 0; if (mma_task_create(c, 1, load.anchors[m.first], load.ground[first], moat, 0, starts.data(), int(starts.size()), &id) != Ok) Fail("task");
                tasks.push_back(id);
            }
            auto t0 = Clock::now(); int computed = 0;
            if (mma_step(c, int(tasks.size()), threads, &computed) != Ok) Fail("step");
            stepMs = Us(t0) / 1000.0;
            size_t taskIndex = 0;
            for (auto& m : members) {
                const int id = tasks[taskIndex++], count = int(m.second.size());
                const int moat = profile ? load.moat[m.second.front()] : 0;
                std::vector<int> starts, slots; for (int i : m.second) { starts.push_back(load.starts[i]); slots.push_back(load.targets[i]); }
                std::vector<uint8_t> batch(size_t(count) * MaxSteps); std::vector<QueryResult> results(count);
                auto b0 = Clock::now();
                if (mma_query_batch(c, id, starts.data(), slots.data(), count, 8, threads, batch.data(), MaxSteps, results.data()) != Ok) Fail("batch");
                const double us = Us(b0); batchMs += us / 1000.0;
                for (int k = 0; k < count; ++k) {
                    const uint8_t* path = batch.data() + size_t(k) * MaxSteps;
                    Validate(load, starts[k], slots[k], moat, results[k], path);
                    a.add(results[k], path, starts[k], slots[k], load.width); a.latency.push_back(us / count);
                    run.record(results[k]);
                }
            }
            mma_stats(c, stats, StatCount, 1);
            run.row(load.name, "group-" + tag, a, batchMs, stepMs, stats);
            // Like AnchorRouting after a move: finished tasks release their fields and sub-anchors.
            for (int id : tasks) if (mma_task_release(c, id) != Ok) Fail("release");
        }
        // Single mode: the exact single route (AnchorSingleMoves), unlimited budget, sequential.
        if (singleLimit > 0) {
            Accumulator a; double total = 0;
            const int count = std::min(singleLimit, int(load.starts.size()));
            for (int i = 0; i < count; ++i) {
                const int moat = profile ? load.moat[i] : 0; QueryResult r{};
                auto q0 = Clock::now();
                const int code = mma_single_route_limited(c, 1, load.targets[i], load.ground[i], moat, 0, load.starts[i], 0, bytes.data(), MaxSteps, &r);
                // NoMemory: the counted workspace quota (the game then keeps Vanilla); recorded as Failed.
                if (code == NoMemory) { r = QueryResult{}; r.status = Failed; }
                else if (code != Ok) Fail("single code=" + std::to_string(code));
                const double us = Us(q0); total += us / 1000.0;
                Validate(load, load.starts[i], load.targets[i], moat, r, bytes.data());
                a.add(r, bytes.data(), load.starts[i], load.targets[i], load.width); a.latency.push_back(us); run.record(r);
            }
            mma_stats(c, stats, StatCount, 1);
            run.row(load.name, "single-" + tag, a, total, 0, stats);
        }
        mma_destroy(c);
    }
}

// ---- synthetic scenarios -------------------------------------------------------------------
struct Grid {
    int w, h; std::vector<u32> words; std::vector<uint8_t> blocked, penalty;
    Grid(int width, int height) : w(width), h(height), words(size_t(width) * height, 0xFFFFu), blocked(size_t(width) * height, 0), penalty(size_t(width) * height, 0) {}
    int node(int x, int y) const { return y * w + x; }
    void block(int x, int y) {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        blocked[node(x, y)] = 1; words[node(x, y)] = 0;
        for (int d = 0; d < 8; ++d) { const int px = x - dx[d], py = y - dy[d]; if (px >= 0 && py >= 0 && px < w && py < h) words[node(px, py)] &= ~((1u << d) | (1u << (8 + d)) | (1u << (16 + d))); }
    }
    void rect(int x0, int y0, int x1, int y1) { for (int y = y0; y <= y1; ++y) for (int x = x0; x <= x1; ++x) block(x, y); }
    // Moat tile: entering it is a wet friendly edge and no ground edge.
    void wet(int x, int y) {
        for (int d = 0; d < 8; ++d) { const int px = x - dx[d], py = y - dy[d]; if (px >= 0 && py >= 0 && px < w && py < h && !blocked[node(px, py)]) { words[node(px, py)] &= ~(1u << (8 + d)); words[node(px, py)] |= (1u << d) | (1u << (16 + d)); } }
    }
    // One-way band: inside it, edges with a northward component are removed (units only go south/sideways).
    void oneWay(int x0, int y0, int x1, int y1) {
        for (int y = y0; y <= y1; ++y) for (int x = x0; x <= x1; ++x) for (int d : {7, 0, 1}) words[node(x, y)] &= ~((1u << d) | (1u << (8 + d)));
    }
};

static int Free(const Grid& g, std::mt19937& random, int x0, int y0, int x1, int y1) {
    for (int guard = 0; guard < 10000; ++guard) {
        const int x = x0 + int(random() % unsigned(x1 - x0 + 1)), y = y0 + int(random() % unsigned(y1 - y0 + 1));
        if (!g.blocked[g.node(x, y)]) return g.node(x, y);
    }
    Fail("no free tile"); return -1;
}

// Groups of 64: an 8x8 start cluster and an 8x8 formation around the anchor, plus single pairs.
static Load Groups(const std::string& name, const Grid& g, std::mt19937& random, int groups, int fromX0, int fromX1, int toX0, int toX1, int ground = 1, int moat = 7) {
    Load load; load.name = name; load.width = g.w; load.height = g.h; load.words = &g.words;
    for (int k = 0; k < groups; ++k) {
        const int center = Free(g, random, fromX0, 12, fromX1, g.h - 13), anchor = Free(g, random, toX0, 12, toX1, g.h - 13);
        load.anchors.push_back(anchor);
        const int cx = center % g.w, cy = center / g.w, ax = anchor % g.w, ay = anchor / g.w;
        for (int i = 0; i < 64; ++i) {
            const int sx = cx - 4 + i % 8, sy = cy - 4 + i / 8, tx = ax - 4 + i % 8, ty = ay - 4 + i / 8;
            if (g.blocked[g.node(sx, sy)] || g.blocked[g.node(tx, ty)]) continue;
            load.starts.push_back(g.node(sx, sy)); load.targets.push_back(g.node(tx, ty)); load.groups.push_back(k);
            load.ground.push_back(ground); load.moat.push_back(moat);
        }
    }
    return load;
}

static void Synthetic(Run& run, int threads) {
    const int N = 256;
    struct Scenario { std::string name; Grid grid; int fx0, fx1, tx0, tx1; bool penalty; };
    std::vector<Scenario> list;
    { Grid g(N, N); list.push_back({"syn-open", g, 12, 243, 12, 243, false}); }
    { Grid g(N, N); g.rect(127, 30, 128, 225); list.push_back({"syn-wall", g, 12, 100, 155, 243, false}); }
    { Grid g(N, N); g.rect(98, 98, 157, 157); list.push_back({"syn-block", g, 12, 80, 175, 243, false}); }
    { Grid g(N, N);
      // Castle ring (thickness 2) with a 4-tile gate on the south side; starts outside west, targets inside.
      g.rect(80, 80, 175, 81); g.rect(80, 174, 175, 175); g.rect(80, 80, 81, 175); g.rect(174, 80, 175, 175);
      for (int x = 126; x <= 129; ++x) for (int y = 174; y <= 175; ++y) { g.blocked[g.node(x, y)] = 0; g.words[g.node(x, y)] = 0xFFFFu; }
      // Reopen edges into the gate from its free neighbours.
      for (int x = 125; x <= 130; ++x) for (int y = 173; y <= 176; ++y) if (!g.blocked[g.node(x, y)]) for (int d = 0; d < 8; ++d) {
          const int nx = x + dx[d], ny = y + dy[d]; if (!g.blocked[g.node(nx, ny)]) g.words[g.node(x, y)] |= (1u << d) | (1u << (8 + d)); }
      list.push_back({"syn-ring", g, 12, 60, 100, 155, false}); }
    { Grid g(N, N);
      // Chokepoints: three walls with one 3-tile gap each, gaps at different heights.
      const int gaps[3] = {40, 200, 120};
      for (int k = 0; k < 3; ++k) { const int x = 70 + k * 60; g.rect(x, 0, x + 1, gaps[k] - 1); g.rect(x, gaps[k] + 3, x + 1, N - 1); }
      list.push_back({"syn-choke", g, 12, 60, 200, 243, false}); }
    { Grid g(N, N); std::mt19937 r(0x5EED);
      for (int i = 0; i < N * N * 12 / 100; ++i) g.block(int(r() % N), int(r() % N));
      list.push_back({"syn-forest", g, 12, 80, 175, 243, false}); }
    { Grid g(N, N); for (int y = 90; y <= 165; ++y) for (int x = 90; x <= 165; ++x) g.penalty[g.node(x, y)] = 3;
      list.push_back({"syn-penalty", g, 12, 80, 175, 243, true}); }
    { Grid g(N, N); for (int y = 0; y < N; ++y) if (y < 228 || y > 233) for (int x = 124; x <= 131; ++x) g.wet(x, y);
      // A dry crossing far south keeps ground routes possible.
      list.push_back({"syn-moat", g, 12, 100, 155, 243, false}); }
    { Grid g(N, N); g.oneWay(0, 100, N - 1, 155); list.push_back({"syn-oneway", g, 12, 243, 12, 243, false}); }
    for (auto& s : list) {
        std::mt19937 random(0xC0FFEE ^ unsigned(s.name.size() * 7919));
        Load load = Groups(s.name, s.grid, random, 40, s.fx0, s.fx1, s.tx0, s.tx1);
        load.words = &s.grid.words;
        MeasureLoad(load, run, threads, 2000, s.penalty ? &s.grid.penalty : nullptr);
    }
}

// ---- exported loads (Testmods/GroupPathProbe --export-anchor-loads) ----------------------------
template<class T> static T Read(std::ifstream& in) { T v{}; in.read(reinterpret_cast<char*>(&v), sizeof v); if (!in) Fail("loads: truncated"); return v; }

static void Exported(const std::string& path, Run& run, int threads) {
    std::ifstream in(path, std::ios::binary); if (!in) Fail("loads: cannot open");
    if (Read<int>(in) != 0x31425352) Fail("loads: magic");
    const int mapCount = Read<int>(in);
    std::vector<std::vector<u32>> maps(mapCount); std::vector<std::pair<int, int>> sizes(mapCount);
    for (int m = 0; m < mapCount; ++m) {
        sizes[m] = {Read<int>(in), Read<int>(in)}; maps[m].resize(size_t(sizes[m].first) * sizes[m].second);
        in.read(reinterpret_cast<char*>(maps[m].data()), std::streamsize(maps[m].size() * sizeof(u32))); if (!in) Fail("loads: words");
    }
    const int loads = Read<int>(in);
    for (int l = 0; l < loads; ++l) {
        Load load; const int nameLength = Read<int>(in); load.name.resize(nameLength); in.read(load.name.data(), nameLength);
        const int map = Read<int>(in); load.width = sizes[map].first; load.height = sizes[map].second; load.words = &maps[map];
        const int groups = Read<int>(in); for (int g = 0; g < groups; ++g) load.anchors.push_back(Read<int>(in));
        const int queries = Read<int>(in);
        for (int q = 0; q < queries; ++q) {
            load.starts.push_back(Read<int>(in)); load.targets.push_back(Read<int>(in)); load.groups.push_back(Read<int>(in));
            load.ground.push_back(Read<int>(in)); load.moat.push_back(Read<int>(in));
        }
        load.name = "cap-" + load.name;
        MeasureLoad(load, run, threads, 2000);
    }
}

// Status must agree; a candidate route may never cost more than the baseline route.
static int Compare(const std::string& a, const std::string& b) {
    std::ifstream fa(a, std::ios::binary), fb(b, std::ios::binary);
    i64 n = 0, cheaper = 0, saved = 0, shorter = 0; Record x{}, y{};
    while (fa.read(reinterpret_cast<char*>(&x), sizeof x)) {
        if (!fb.read(reinterpret_cast<char*>(&y), sizeof y)) Fail("compare: candidate has fewer routes");
        ++n;
        if (x.status != y.status) Fail("compare: status differs at route " + std::to_string(n - 1));
        if (x.status == Found) {
            if (y.cost > x.cost) Fail("compare: candidate is more expensive at route " + std::to_string(n - 1));
            if (y.cost < x.cost) { ++cheaper; saved += x.cost - y.cost; }
            if (y.length < x.length) ++shorter;
        }
    }
    if (fb.read(reinterpret_cast<char*>(&y), sizeof y)) Fail("compare: candidate has more routes");
    std::cout << "COMPARE routes=" << n << " cheaper=" << cheaper << " costSaved=" << saved << " fewerSteps=" << shorter << " moreExpensive=0 statusChanges=0\n";
    return 0;
}
}

int main(int argc, char** argv) {
    try {
        if (argc == 4 && std::string(argv[1]) == "--compare") return bench::Compare(argv[2], argv[3]);
        if (argc != 4) { std::cerr << "usage: RouteShapeBench <loads.bin|-> <summary.csv> <routes.bin> | --compare <a> <b>\n"; return 64; }
        std::ofstream csv(argv[2], std::ios::binary), routes(argv[3], std::ios::binary);
        csv << "load,mode,found,unreachable,other,cost,steps,pieces,turns,euclid,chord,chebyshev,p50us,p95us,p99us,maxus,totalMs,stepMs,singleSettles,settles,viaChosen,subAnchors,anchorTooFar,peakBytes\r\n";
        bench::Run run{csv, routes};
        // Fixed thread count: results never depend on it, timings stay comparable between cores.
        const int threads = 4;
        bench::Synthetic(run, threads);
        if (std::string(argv[1]) != "-") bench::Exported(argv[1], run, threads);
        std::cout << "DONE\n"; return 0;
    } catch (const std::exception& e) { std::cerr << "FAIL: " << e.what() << "\n"; return 1; }
}
