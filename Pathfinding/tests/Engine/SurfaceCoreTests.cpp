// Exercises the production native core directly, including its C ABI. No game code is loaded.
#include "../../native/PathfindingAnchor.cpp"
#include <iostream>
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

int main(int argc, char** argv) {
    try { if (argc == 2 && std::string(argv[1]) == "--memory-probe") { MemoryWorkerProbe(); ProductionMemoryWorkerProbe(); return 0; }
        Check(mma_contract() == 0x04000011, "private ABI"); CorridorTests(); SubanchorAndBatchTests(); RandomDirectedTests();
        MemoryWorkerProbe(); ProductionMemoryWorkerProbe(); AllocationFailureReentryTests();
        std::cout << "PASS: " << assertions << " production native surface/core assertions.\n"; return 0; }
    catch (const std::exception& error) { std::cerr << "FAIL: " << error.what() << " after " << assertions << " assertions.\n"; return 1; }
}
