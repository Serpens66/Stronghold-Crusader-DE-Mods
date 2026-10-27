// Private diagnostic backend. Original implementation of rectangle reduction and
// shared reverse fields; see SOURCES.md. No game imports, pointers or hooks.
#ifndef SERP_DEVDIAG
#error This private backend is diagnostic-only.
#endif
#include <algorithm>
#include <array>
#include <chrono>
#include <cstdint>
#include <limits>
#include <memory>
#include <map>
#include <stdexcept>
#include <tuple>
#include <vector>

namespace bpf {
using i64 = int64_t;
constexpr i64 inf = std::numeric_limits<i64>::max();
constexpr int dx[8] = {0,1,1,1,0,-1,-1,-1}, dy[8] = {-1,-1,0,1,1,1,0,-1};
struct Exhausted {};
struct SliceLimit {};
struct Budget {
    i64 limit, bytes = 65536, peak = 65536, allocatedTotal=0, allocationCalls=0;
    explicit Budget(i64 maximum) : limit(maximum) { if (maximum < bytes) throw Exhausted(); }
    void take(size_t n) { if (n > size_t(limit - bytes)) throw Exhausted(); bytes += i64(n); peak = std::max(peak, bytes); }
    void give(size_t n) { bytes -= i64(n); }
};
template<class T> struct Alloc {
    using value_type = T; using propagate_on_container_move_assignment = std::true_type;
    Budget* budget;
    explicit Alloc(Budget* b = nullptr) : budget(b) {}
    template<class U> Alloc(const Alloc<U>& a) : budget(a.budget) {}
    T* allocate(size_t n) { if (n > (size_t(-1)-32)/sizeof(T)) throw Exhausted(); budget->take(n*sizeof(T)+32); try { auto p=static_cast<T*>(::operator new(n*sizeof(T)));budget->allocatedTotal+=i64(n*sizeof(T));++budget->allocationCalls;return p; } catch (...) { budget->give(n*sizeof(T)+32); throw; } }
    void deallocate(T* p, size_t n) { ::operator delete(p); budget->give(n*sizeof(T)+32); }
    template<class U> bool operator==(const Alloc<U>& a) const { return budget == a.budget; }
    template<class U> bool operator!=(const Alloc<U>& a) const { return !(*this == a); }
};
template<class T> using Vec = std::vector<T, Alloc<T>>;
struct Request {
    int start, target, maximum, requireWet, exclude, boundOffset, boundCount, terminalOffset, terminalCount, reserved;
    i64 ground, moat;
};
struct Bound { i64 ground, moat, maximum; };
struct Terminal { int from, to, wet, structure; };
struct Result { int status = 2, index = 0, ground = 0, moat = 0, offset = 0, length = 0; i64 cost = 0; double completedMs = 0; };
struct Stats {
    i64 regions, vertices, pruned, fields, hits, macros, expanded, bytes, peak,
        refinements, revision, changed, evictions, completed, outputBytes, reserved;
};
static_assert(sizeof(Request) == 56 && sizeof(Bound) == 24 && sizeof(Terminal) == 16 && sizeof(Result) == 40 && sizeof(Stats) == 128, "C ABI layout");
struct Rect { int x, y, w, h, type; };
struct Context;

// Indexed decrease-key heap: at most one entry per state, no stale-entry growth.
struct Field {
    int target; i64 ground, moat; bool requireWet, exclude, complete = false;
    Vec<i64> distance; Vec<int> next, position, heap; Vec<uint8_t> kind;
    std::unique_ptr<Field> local; int localRegion=-1;
    Vec<uint32_t> stamps; Vec<i64> priorities; uint32_t generation=0; bool forward=false;
    Field(Budget* b, int count, const Request& q) : target(q.target), ground(q.ground), moat(q.moat), requireWet(q.requireWet != 0), exclude(q.exclude != 0),
        distance(Alloc<i64>(b)), next(Alloc<int>(b)), position(Alloc<int>(b)), heap(Alloc<int>(b)), kind(Alloc<uint8_t>(b)),stamps(Alloc<uint32_t>(b)),priorities(Alloc<i64>(b)) {
        int n = count * (requireWet ? 2 : 1); distance.assign(n, inf); next.assign(n, -1); position.assign(n, -1); kind.assign(n, 0); heap.reserve(n);
    }
    bool less(int a, int b) const { i64 ca=forward?priorities[a]:distance[a],cb=forward?priorities[b]:distance[b];return ca!=cb?ca<cb:a<b; }
    void put(int id) {
        int at = position[id]; if (at < 0) { at = int(heap.size()); heap.push_back(id); }
        while (at) { int p = (at-1)/2; if (!less(id,heap[p])) break; heap[at] = heap[p]; position[heap[at]] = at; at = p; }
        heap[at] = id; position[id] = at;
    }
    int pop() {
        int result = heap[0], last = heap.back(); heap.pop_back(); position[result] = -2;
        if (!heap.empty()) { int at = 0; while (at*2+1 < int(heap.size())) { int child = at*2+1; if (child+1 < int(heap.size()) && less(heap[child+1],heap[child])) ++child; if (!less(heap[child],last)) break; heap[at]=heap[child]; position[heap[at]]=at; at=child; } heap[at]=last; position[last]=at; }
        return result;
    }
    void seed(int state, i64 cost, int following, uint8_t k,i64 heuristic=0) { if(forward&&stamps[state]!=generation){stamps[state]=generation;distance[state]=inf;position[state]=-1;}if (cost < distance[state]) { distance[state]=cost;if(forward)priorities[state]=cost+heuristic; next[state]=following; kind[state]=k; put(state); } }
    void resetForward() { if(stamps.empty()){stamps.assign(distance.size(),0);priorities.resize(distance.size());}forward=true;heap.clear();if(++generation==0){std::fill(stamps.begin(),stamps.end(),0);generation=1;} }
    void releaseFrontier() { Vec<int>(heap.get_allocator()).swap(heap); Vec<int>(position.get_allocator()).swap(position); complete=true; }
};
struct Label { int node, ground, moat, parent, direction, next; i64 cost, priority; bool dead; };
struct Refine {
    Vec<int> heads, heap; Vec<Label> labels;
    explicit Refine(Budget* b, int n) : heads(n,-1,Alloc<int>(b)), heap(Alloc<int>(b)), labels(Alloc<Label>(b)) {}
    bool less(int a, int b) const { return labels[a].priority != labels[b].priority ? labels[a].priority < labels[b].priority : a < b; }
    void push(int id) { int at=int(heap.size()); heap.push_back(id); while(at) { int p=(at-1)/2; if(!less(id,heap[p]))break; heap[at]=heap[p];at=p; } heap[at]=id; }
    int pop() { int id=heap[0], last=heap.back(); heap.pop_back(); if(!heap.empty()){int at=0;while(at*2+1<int(heap.size())){int c=at*2+1;if(c+1<int(heap.size())&&less(heap[c+1],heap[c]))++c;if(!less(heap[c],last))break;heap[at]=heap[c];at=c;}heap[at]=last;}return id; }
};
struct Context {
    using Key=std::tuple<int,i64,i64,int,int,int>;
    Budget budget; int width, height, count;
    Vec<uint8_t> valid; Vec<uint32_t> edges; Vec<int> region, vertex, nodes; Vec<Rect> rectangles;
    std::array<std::unique_ptr<Field>,4> cache; Field* active = nullptr;
    Vec<Request> requests; Vec<Bound> bounds; Vec<Terminal> terminals; Vec<Result> results; Vec<uint8_t> output, path;
    std::unique_ptr<Refine> refinement;
    std::unique_ptr<Field> forwardSearch; bool forwardActive=false;
    std::map<Key,int,std::less<Key>,Alloc<std::pair<const Key,int>>> groups;
    int cursor=0; bool aborted=false; i64 fields=0,hits=0,macros=0,expanded=0,refinements=0,evictions=0,localFields=0,localHits=0,forwardCount=0,analyticCount=0;
    Key key(const Request& q) const { return {q.target,q.ground,q.moat,q.requireWet,q.exclude,region[q.start]}; }
    Key goalKey(const Request& q) const { return {q.target,q.ground,q.moat,q.requireWet,q.exclude,-1}; }
    void discardIncompleteSearch() { active=nullptr; refinement.reset();forwardSearch.reset();forwardActive=false; for(auto& entry:cache)entry.reset(); aborted=true; }
    std::chrono::steady_clock::time_point started;
    std::chrono::steady_clock::time_point sliceDeadline;
    Context(int w,int h,const uint8_t* v,const uint32_t* e,i64 maximum) : budget(maximum),width(w),height(h),count(w*h),
        valid(v,v+count,Alloc<uint8_t>(&budget)),edges(e,e+count,Alloc<uint32_t>(&budget)),region(count,-1,Alloc<int>(&budget)),vertex(count,-1,Alloc<int>(&budget)),nodes(Alloc<int>(&budget)),rectangles(Alloc<Rect>(&budget)),
        requests(Alloc<Request>(&budget)),bounds(Alloc<Bound>(&budget)),terminals(Alloc<Terminal>(&budget)),results(Alloc<Result>(&budget)),output(Alloc<uint8_t>(&budget)),path(Alloc<uint8_t>(&budget)),groups(std::less<Key>(),Alloc<std::pair<const Key,int>>(&budget)) { build(); }
    bool isValid(int n) const { return n>=0 && n<count && valid[n]!=0; }
    int neighbor(int n,int d) const { int x=n%width+dx[d],y=n/width+dy[d];return x>=0&&y>=0&&x<width&&y<height?y*width+x:-1; }
    int length(int a,int b) const { return std::max(std::abs(a%width-b%width),std::abs(a/width-b/width)); }
    // A cell is compressible only if every geometrically valid outgoing edge is
    // present and has the SAME wet/structure class. Directional exceptions stay explicit.
    int type(int n) const {
        if(!isValid(n))return -1; int tag=-1;
        for(int d=0;d<8;++d) {int to=neighbor(n,d);if(!isValid(to))continue;uint32_t word=edges[n];if(!(word&(1u<<d)))return -1;int t=((word>>(8+d))&1)|(((word>>(16+d))&1)<<1);if(tag<0)tag=t;else if(tag!=t)return -1;}
        return tag;
    }
    template<class F> void perimeter(const Rect& r,F f) const {
        for(int x=r.x;x<r.x+r.w;++x)f(r.y*width+x);
        if(r.h>1)for(int x=r.x;x<r.x+r.w;++x)f((r.y+r.h-1)*width+x);
        for(int y=r.y+1;y<r.y+r.h-1;++y){f(y*width+r.x);if(r.w>1)f(y*width+r.x+r.w-1);}
    }
    void build() {
        Vec<int8_t> types(count,int8_t(-2),Alloc<int8_t>(&budget));
        for(int n=0;n<count;++n)if(isValid(n))types[n]=int8_t(type(n));
        for(int n=0;n<count;++n) {
            if(!isValid(n)||region[n]>=0)continue;int x=n%width,y=n/width,t=types[n],w=1,h=1;
            if(t>=0) {while(x+w<width&&region[n+w]<0&&types[n+w]==t)++w;bool grow=true;while(grow&&y+h<height){for(int xx=x;xx<x+w;++xx){int i=(y+h)*width+xx;if(region[i]>=0||types[i]!=t){grow=false;break;}}if(grow)++h;}}
            int id=int(rectangles.size());rectangles.push_back({x,y,w,h,t});
            for(int yy=y;yy<y+h;++yy)for(int xx=x;xx<x+w;++xx)region[yy*width+xx]=id;
            perimeter(rectangles.back(),[&](int node){vertex[node]=int(nodes.size());nodes.push_back(node);});
        }
    }
    bool edge(int from,int d,const Request& q,int& to,bool& wet) const {
        to=neighbor(from,d);if(!isValid(from)||!isValid(to))return false;
        uint32_t word=edges[from];bool allowed=(word&(1u<<d))!=0,structure=(word&(1u<<(16+d)))!=0;wet=(word&(1u<<(8+d)))!=0;
        for(int i=0;i<q.terminalCount;++i){const auto& t=terminals[q.terminalOffset+i];if(t.from==from&&t.to==to){allowed=true;wet=t.wet!=0;structure=t.structure!=0;break;}}
        return allowed&&(!q.exclude||!structure);
    }
    bool allows(const Request& q,int g,int m) const {
        if(i64(g)+m>q.maximum)return false;
        for(int i=0;i<q.boundCount;++i){const auto& b=bounds[q.boundOffset+i];if(g>b.maximum/b.ground||m>(b.maximum-i64(g)*b.ground)/b.moat)return false;}
        return true;
    }
    void line(int from,int to) {
        while(from!=to){int sx=(to%width>from%width)-(to%width<from%width),sy=(to/width>from/width)-(to/width<from/width),d=0;while(dx[d]!=sx||dy[d]!=sy)++d;path.push_back(uint8_t(d));from=neighbor(from,d);}
    }
    bool validate(const Request& q,Result& r) const {
        int node=q.start,g=0,m=0;for(uint8_t d:path){int to;bool wet;if(d>=8||!edge(node,d,q,to,wet))throw std::logic_error("illegal output edge");if(wet)++m;else++g;node=to;}
        if(node!=q.target)throw std::logic_error("output endpoint");
        if(!allows(q,g,m)||(q.requireWet&&m==0))return false;
        r.status=0;r.ground=g;r.moat=m;r.cost=i64(g)*q.ground+i64(m)*q.moat;return true;
    }
    void relax(Field& f,int pred,int current,int steps,bool wet,uint8_t kind) {
        if(steps<=0)return;int state=f.requireWet?current/int(nodes.size()):0;
        i64 increment=i64(steps)*(wet?f.moat:f.ground);if(f.distance[current]>inf-increment)return;
        auto add=[&](int before){int p=pred+before*int(nodes.size());f.seed(p,f.distance[current]+increment,current,kind);};
        if(!f.requireWet)add(0);else if(wet){if(state){add(0);add(1);}}else add(state);
    }
    void initialize(Field& f) {
        int v=vertex[f.target],goalState=f.requireWet?1:0;
        if(v>=0){f.seed(v+goalState*int(nodes.size()),0,-1,0);return;}
        const auto& r=rectangles[region[f.target]];
        if(f.exclude&&(r.type&2))return;
        perimeter(r,[&](int node){int steps=length(node,f.target),p=vertex[node];bool wet=(r.type&1)!=0;auto add=[&](int state){f.seed(p+state*int(nodes.size()),i64(steps)*(wet?f.moat:f.ground),-2,1);};if(!f.requireWet)add(0);else if(wet&&steps){add(0);add(1);}else add(1);});
    }
    // RSR-style fan and diagonal macro edges, generated on demand. Their lengths
    // use SHCDE's Chebyshev step metric, NOT the paper's octile metric.
    template<class F> void macroEdges(int node,bool exclude,bool loops,F emit) {
        const auto& r=rectangles[region[node]];if(r.type<0||(exclude&&(r.type&2)))return;
        auto macro=[&](int n){if(n==node)return;++macros;emit(n,length(n,node),(r.type&1)!=0,uint8_t(1));};
        int x=node%width,y=node/width,right=r.x+r.w-1,bottom=r.y+r.h-1;
        if(r.w>2&&r.h>2){
            if(x==r.x||x==right){int xx=x==r.x?right:r.x,span=r.w-1;for(int yy=std::max(r.y,y-span);yy<=std::min(bottom,y+span);++yy)macro(yy*width+xx);}
            if(y==r.y||y==bottom){int yy=y==r.y?bottom:r.y,span=r.h-1;for(int xx=std::max(r.x,x-span);xx<=std::min(right,x+span);++xx)macro(yy*width+xx);}
            for(int d=1;d<8;d+=2){int steps=std::min(dx[d]>0?right-x:x-r.x,dy[d]>0?bottom-y:y-r.y);if(steps)macro((y+dy[d]*steps)*width+x+dx[d]*steps);}
        }
        // Preserve a positive wet excursion when a resource state requires a moat
        // visit but the optimal local walk returns to the same boundary vertex.
        if(loops&&(r.type&1))for(int d=0;d<8;++d){int n=neighbor(node,d);if(isValid(n)&&region[n]==region[node]){emit(node,2,true,uint8_t(2));break;}}
    }
    void expand(Field& f,int state) {
        int vi=state%int(nodes.size()),node=nodes[vi];Request q{};q.exclude=f.exclude;
        for(int d=0;d<8;++d){int from=neighbor(node,(d+4)&7),to;bool wet;if(!isValid(from)||vertex[from]<0||!edge(from,d,q,to,wet))continue;relax(f,vertex[from],state,1,wet,0);}
        macroEdges(node,f.exclude,f.requireWet,[&](int pred,int steps,bool wet,uint8_t kind){relax(f,vertex[pred],state,steps,wet,kind);});
    }
    int coordinate(int id,const Request& q) const { int i=id%(int(nodes.size())+2);return i<int(nodes.size())?nodes[i]:i==int(nodes.size())?q.start:q.target; }
    int sourceVertex(const Request& q) const { return vertex[q.start]>=0?vertex[q.start]:int(nodes.size()); }
    int targetVertex(const Request& q) const { return vertex[q.target]>=0?vertex[q.target]:int(nodes.size())+1; }
    void startForward(const Request& q) {
        if(!forwardSearch){Request doubled=q;doubled.requireWet=1;forwardSearch=std::make_unique<Field>(&budget,int(nodes.size())+2,doubled);}
        forwardSearch->resetForward();forwardActive=true;++forwardCount;
        forwardSearch->seed(sourceVertex(q),0,-1,0,i64(length(q.start,q.target))*std::min(q.ground,q.moat));
    }
    void forwardEdge(const Request& q,int from,int node,int steps,bool wet,uint8_t kind) {
        int base=int(nodes.size())+2;bool seen=q.requireWet&&(from>=base||wet&&steps>0);int to=node+(seen?base:0);
        i64 increment=i64(steps)*(wet?q.moat:q.ground);auto& f=*forwardSearch;if(f.distance[from]>inf-increment)return;
        f.seed(to,f.distance[from]+increment,from,kind,i64(length(coordinate(node,q),q.target))*std::min(q.ground,q.moat));
    }
    void expandForward(const Request& q,int state) {
        int id=state%(int(nodes.size())+2),node=coordinate(state,q);const auto& r=rectangles[region[node]];
        bool internal=r.type>=0&&(!q.exclude||!(r.type&2));
        if(id<int(nodes.size())){
            for(int d=0;d<8;++d){int to;bool wet;if(edge(node,d,q,to,wet)&&vertex[to]>=0)forwardEdge(q,state,vertex[to],1,wet,0);}
            macroEdges(node,q.exclude!=0,q.requireWet!=0,[&](int to,int steps,bool wet,uint8_t kind){forwardEdge(q,state,vertex[to],steps,wet,kind);});
        }else if(internal){perimeter(r,[&](int to){forwardEdge(q,state,vertex[to],length(node,to),(r.type&1)!=0,1);});}
        if(internal&&region[node]==region[q.target]){
            int steps=length(node,q.target);forwardEdge(q,state,targetVertex(q),steps,steps>0&&(r.type&1),1);
            if(q.requireWet&&node==q.target&&(r.type&1)&&r.w*r.h>1)forwardEdge(q,state,targetVertex(q),2,true,2);
        }
    }
    bool traceForward(const Request& q,int goal,Result& result) {
        auto& f=*forwardSearch;Vec<int> chain{Alloc<int>(&budget)};int guard=int(f.distance.size())+1;
        for(int i=goal;f.next[i]>=0;i=f.next[i]){if(--guard==0)throw std::logic_error("forward parent cycle");chain.push_back(i);}
        path.clear();int from=q.start;
        for(auto i=chain.rbegin();i!=chain.rend();++i){int to=coordinate(*i,q);if(f.kind[*i]==2)wetLoop(from);else line(from,to);from=to;if(path.size()>size_t(q.maximum))return false;}
        return validate(q,result);
    }
    Field& getField(const Request& q) {
        for(size_t i=0;i<cache.size();++i)if(cache[i]&&cache[i]->target==q.target&&cache[i]->ground==q.ground&&cache[i]->moat==q.moat&&cache[i]->requireWet==(q.requireWet!=0)&&cache[i]->exclude==(q.exclude!=0)){
            ++hits;auto found=std::move(cache[i]);for(size_t j=i;j>0;--j)cache[j]=std::move(cache[j-1]);cache[0]=std::move(found);return *cache[0];}
        if(cache.back()){cache.back().reset();++evictions;}for(size_t i=cache.size()-1;i>0;--i)cache[i]=std::move(cache[i-1]);
        cache[0]=std::make_unique<Field>(&budget,int(nodes.size()),q);++fields;initialize(*cache[0]);return *cache[0];
    }
    bool hasField(const Request& q) const {
        for(const auto& f:cache)if(f&&f->target==q.target&&f->ground==q.ground&&f->moat==q.moat&&f->requireWet==(q.requireWet!=0)&&f->exclude==(q.exclude!=0))return true;return false;
    }
    void wetLoop(int node) {
        for(int d=0;d<8;++d){int n=neighbor(node,d);if(isValid(n)&&region[n]==region[node]){path.push_back(uint8_t(d));path.push_back(uint8_t((d+4)&7));return;}}
        throw std::logic_error("wet loop witness");
    }
    bool prepareLocal(Field& f,const Request& q,int& allowance) {
        int rid=region[q.start];const auto& r=rectangles[rid];
        if(vertex[q.start]>=0||q.start==q.target||r.type<0||(q.exclude&&(r.type&2))||groups[key(q)]<32)return true;
        if(f.localRegion!=rid){f.local.reset();f.localRegion=rid;Request scalar=q;scalar.requireWet=0;f.local=std::make_unique<Field>(&budget,r.w*r.h,scalar);++localFields;
            int seen=f.requireWet&&(r.type&1)?1:0;
            perimeter(r,[&](int node){int slot=(node/width-r.y)*r.w+node%width-r.x;f.local->seed(slot,f.distance[vertex[node]+seen*int(nodes.size())],-1,0);});
            if(region[q.target]==rid&&(!q.requireWet||(r.type&1))){int slot=(q.target/width-r.y)*r.w+q.target%width-r.x;f.local->seed(slot,0,-1,0);}}
        auto& local=*f.local;
        if(local.complete){++localHits;return true;}
        while(!local.heap.empty()&&allowance>0){int at=local.pop(),x=at%r.w,y=at/r.w;--allowance;++expanded;
            for(int d=0;d<8;++d){int xx=x+dx[d],yy=y+dy[d];if(xx<0||yy<0||xx>=r.w||yy>=r.h)continue;i64 cost=(r.type&1)?q.moat:q.ground;if(local.distance[at]<=inf-cost)local.seed(yy*r.w+xx,local.distance[at]+cost,at,0);}}
        if(!local.heap.empty())return false;local.releaseFrontier();return true;
    }
    bool trace(Field& f,const Request& q,Result& result) {
        path.clear();int node=q.start,best=-1;uint8_t firstKind=0;i64 cost=inf;
        const auto& r=rectangles[region[node]];bool internal=r.type>=0&&(!q.exclude||!(r.type&2)),wet=internal&&(r.type&1);
        if(vertex[node]<0&&node!=q.target&&f.local&&f.localRegion==region[node]&&f.local->complete){
            auto& local=*f.local;int at=(node/width-r.y)*r.w+node%width-r.x;
            if(local.distance[at]==inf){result.status=1;return true;}
            int guard=r.w*r.h+1;
            while(local.next[at]>=0){if(--guard==0)throw std::logic_error("local field cycle");at=local.next[at];int to=(r.y+at/r.w)*width+r.x+at%r.w;line(node,to);node=to;}
            if(node==q.target)return validate(q,result);
            if(vertex[node]<0)throw std::logic_error("local continuation seed");
            best=vertex[node]+(f.requireWet&&wet?int(nodes.size()):0);cost=f.distance[best];
        } else {
        if(vertex[node]>=0){best=vertex[node];cost=f.distance[best];}
        if(internal){
            if(region[q.target]==region[node]){int steps=length(node,q.target);if(!q.requireWet||(wet&&steps)){cost=i64(steps)*(wet?q.moat:q.ground);best=-2;firstKind=1;}
                else if(wet&&steps==0&&r.w*r.h>1){cost=2*q.moat;best=-2;firstKind=2;}}
            perimeter(r,[&](int n){int steps=length(node,n),seen=f.requireWet&&wet&&steps>0?1:0,id=vertex[n]+seen*int(nodes.size());i64 c=f.distance[id];if(c!=inf)c+=i64(steps)*(wet?q.moat:q.ground);if(c<cost){cost=c;best=id;firstKind=1;}});
        }
        if(cost==inf){result.status=1;return true;}
        if(best==-2){if(firstKind==2)wetLoop(node);else line(node,q.target);return validate(q,result);}
        if(firstKind==1){int n=nodes[best%int(nodes.size())];line(node,n);node=n;}
        }
        int guard=int(nodes.size())*(f.requireWet?2:1)+1;
        while(best>=0){if(--guard<=0)throw std::logic_error("field witness cycle");int next=f.next[best];uint8_t k=f.kind[best];if(next==-1)break;if(next==-2){line(node,q.target);node=q.target;break;}int to=nodes[next%int(nodes.size())];if(k==2)wetLoop(node);else line(node,to);node=to;best=next;}
        return validate(q,result);
    }
    bool direct(const Request& q,Result& result) {
        if(q.start==q.target&&!q.requireWet){path.clear();return validate(q,result);}
        if(q.terminalCount||region[q.start]!=region[q.target])return false;
        const auto& r=rectangles[region[q.start]];if(r.type<0||(q.exclude&&(r.type&2)))return false;
        bool wet=(r.type&1)!=0;int steps=length(q.start,q.target);
        if((wet?q.moat:q.ground)>std::min(q.moat,q.ground)||q.requireWet&&(!wet||steps==0))return false;
        path.clear();line(q.start,q.target);return validate(q,result);
    }
    void addLabel(const Request& q,int node,int g,int m,int parent,int direction) {
        if(!allows(q,g,m))return;int remaining=length(node,q.target);
        for(int i=0;i<q.boundCount;++i){const auto& b=bounds[q.boundOffset+i];i64 used=i64(g)*b.ground+i64(m)*b.moat;if(remaining>(b.maximum-used)/std::min(b.ground,b.moat))return;}
        auto& s=*refinement;int head=s.heads[node];
        int checks=0;
        for(int i=head;i>=0;i=s.labels[i].next){if((++checks&255)==0&&std::chrono::steady_clock::now()>sliceDeadline)throw SliceLimit();const auto& old=s.labels[i];if(!old.dead&&((old.moat>0)==(m>0))&&old.ground<=g&&old.moat<=m)return;}
        for(int i=head;i>=0;i=s.labels[i].next){if((++checks&255)==0&&std::chrono::steady_clock::now()>sliceDeadline)throw SliceLimit();auto& old=s.labels[i];if(((old.moat>0)==(m>0))&&g<=old.ground&&m<=old.moat)old.dead=true;}
        i64 cost=i64(g)*q.ground+i64(m)*q.moat;int id=int(s.labels.size());s.labels.push_back({node,g,m,parent,direction,head,cost,cost+remaining*std::min(q.ground,q.moat),false});s.heads[node]=id;s.push(id);
    }
    void finish(Result& result) {
        result.index=requests[cursor].reserved;
        result.offset=int(output.size());if(result.status==0){result.length=int(path.size());output.insert(output.end(),path.begin(),path.end());}
        result.completedMs=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-started).count();results.push_back(result);++cursor;active=nullptr;forwardActive=false;refinement.reset();path.clear();
    }
    int step(int allowance) {
        sliceDeadline=std::chrono::steady_clock::now()+std::chrono::milliseconds(50);
        while(cursor<int(requests.size())&&allowance>0){const auto& q=requests[cursor];Result result;
            if(!active&&!refinement&&!forwardActive){if(direct(q,result)){++analyticCount;finish(result);--allowance;continue;}
                if(q.terminalCount){refinement=std::make_unique<Refine>(&budget,count);++refinements;addLabel(q,q.start,0,0,-1,-1);}else if(groups[goalKey(q)]>=32||hasField(q))active=&getField(q);else startForward(q);}
            if(forwardActive){auto& f=*forwardSearch;
                while(!f.heap.empty()&&allowance>0){int state=f.pop();--allowance;++expanded;
                    if(state%(int(nodes.size())+2)==targetVertex(q)&&(!q.requireWet||state>=int(nodes.size())+2)){
                        if(traceForward(q,state,result)){finish(result);break;}
                        forwardActive=false;refinement=std::make_unique<Refine>(&budget,count);++refinements;addLabel(q,q.start,0,0,-1,-1);break;}
                    expandForward(q,state);
                }
                if(forwardActive){if(!f.heap.empty())return 1;path.clear();result.status=1;finish(result);}if(!refinement)continue;
            }
            if(active){while(!active->heap.empty()&&allowance>0){int state=active->pop();expand(*active,state);++expanded;--allowance;}if(!active->heap.empty())return 1;if(!active->complete)active->releaseFrontier();
                if(!prepareLocal(*active,q,allowance))return 1;
                if(trace(*active,q,result)){finish(result);--allowance;continue;}
                refinement=std::make_unique<Refine>(&budget,count);++refinements;active=nullptr;addLabel(q,q.start,0,0,-1,-1);}
            auto& s=*refinement;
            while(!s.heap.empty()&&allowance>0){int id=s.pop();--allowance;Label current=s.labels[id];if(current.dead)continue;++expanded;
                if(current.node==q.target&&(!q.requireWet||current.moat>0)){path.clear();for(int i=id;s.labels[i].parent>=0;i=s.labels[i].parent)path.push_back(uint8_t(s.labels[i].direction));std::reverse(path.begin(),path.end());if(!validate(q,result))throw std::logic_error("refinement contract");finish(result);break;}
                for(int d=0;d<8;++d){int to;bool wet;if(edge(current.node,d,q,to,wet))addLabel(q,to,current.ground+!wet,current.moat+wet,id,d);}
            }
            if(refinement&&s.heap.empty()){path.clear();result.status=1;finish(result);}
        }
        return cursor==int(requests.size())?0:1;
    }
    void begin(const Request* qs,int n,const Bound* bs,int bn,const Terminal* ts,int tn) {
        if(aborted){Vec<uint8_t>(Alloc<uint8_t>(&budget)).swap(output);Vec<uint8_t>(Alloc<uint8_t>(&budget)).swap(path);Vec<Result>(Alloc<Result>(&budget)).swap(results);Vec<Request>(Alloc<Request>(&budget)).swap(requests);}
        requests.assign(qs,qs+n);if(bn)bounds.assign(bs,bs+bn);else bounds.clear();if(tn)terminals.assign(ts,ts+tn);else terminals.clear();results.clear();output.clear();path.clear();cursor=0;aborted=false;active=nullptr;forwardActive=false;refinement.reset();started=std::chrono::steady_clock::now();
        groups.clear();Vec<uint8_t> indices(n,0,Alloc<uint8_t>(&budget));
        for(const auto& q:requests){if(!isValid(q.start)||!isValid(q.target)||q.maximum<0||q.maximum>2000000||q.ground<=0||q.moat<=0||q.ground>inf/4000001||q.moat>inf/4000001||q.boundCount<0||q.boundCount>32||q.terminalCount<0||q.terminalCount>64||q.boundOffset<0||q.boundOffset>bn-q.boundCount||q.terminalOffset<0||q.terminalOffset>tn-q.terminalCount)throw std::invalid_argument("request");
            ++groups[key(q)];
            ++groups[goalKey(q)];
            if(q.reserved<0||q.reserved>=n||indices[q.reserved])throw std::invalid_argument("request identity");indices[q.reserved]=1;
            for(int i=0;i<q.boundCount;++i){const auto& b=bounds[q.boundOffset+i];if(b.ground<=0||b.moat<=0||b.maximum<0)throw std::invalid_argument("bound");}
            for(int i=0;i<q.terminalCount;++i){const auto& t=terminals[q.terminalOffset+i];if(t.to!=q.target||!isValid(t.from)||length(t.from,t.to)!=1)throw std::invalid_argument("terminal");}}
        // Cohort scheduling avoids cache thrashing for alternating formation goals.
        // Stable request identities, not scheduling position, bind returned paths.
        std::sort(requests.begin(),requests.end(),[&](const Request& a,const Request& b){auto ka=key(a),kb=key(b);return ka!=kb?ka<kb:a.reserved<b.reserved;});
    }
};
}

#define API extern "C" __declspec(dllexport)
using namespace bpf;
API int bpf_contract() { return 0x04003828; } // ABI 4 adds per-operation memory peaks for the resident owner.
API int bpf_reset_peak(void* handle) noexcept { if(!handle)return 3;auto& c=*static_cast<Context*>(handle);c.budget.peak=c.budget.bytes;return 0; }
API int bpf_create(int w,int h,const uint8_t* valid,const uint32_t* words,i64 bytes,void** output) noexcept {
    if(!output)return 3;*output=nullptr;if(!valid||!words||w<1||h<1||i64(w)*h>640000)return 3;
    try{*output=new Context(w,h,valid,words,bytes);return 0;}catch(const Exhausted&){return 2;}catch(const std::bad_alloc&){return 2;}catch(...){return 4;}
}
API void bpf_destroy(void* handle) noexcept { delete static_cast<Context*>(handle); }
API int bpf_set_limit(void* handle,i64 bytes) noexcept { if(!handle)return 3;auto& c=*static_cast<Context*>(handle);if(bytes<c.budget.bytes)return 2;c.budget.limit=bytes;return 0; }
API int bpf_allocations(void* handle,i64* bytes,i64* calls) noexcept { if(!handle||!bytes||!calls)return 3;auto& c=*static_cast<Context*>(handle);*bytes=c.budget.allocatedTotal;*calls=c.budget.allocationCalls;return 0; }
API int bpf_query_counts(void* handle,i64* forward,i64* analytic) noexcept { if(!handle||!forward||!analytic)return 3;auto& c=*static_cast<Context*>(handle);*forward=c.forwardCount;*analytic=c.analyticCount;return 0; }
API int bpf_begin(void* handle,const Request* requests,int count,const Bound* bounds,int bn,const Terminal* terminals,int tn) noexcept {
    if(!handle||!requests||count<1||count>10000||bn<0||tn<0||bn>320000||tn>640000||(bn&&!bounds)||(tn&&!terminals))return 3;
    try{static_cast<Context*>(handle)->begin(requests,count,bounds,bn,terminals,tn);return 0;}catch(const Exhausted&){return 2;}catch(const std::bad_alloc&){return 2;}catch(const std::invalid_argument&){return 3;}catch(...){return 4;}
}
API int bpf_step(void* handle,int operations) noexcept {
    if(!handle||operations<1)return 3;auto& c=*static_cast<Context*>(handle);
    try{return c.step(operations);}catch(const Exhausted&){c.discardIncompleteSearch();return 2;}catch(const SliceLimit&){c.discardIncompleteSearch();return 5;}catch(const std::bad_alloc&){c.discardIncompleteSearch();return 2;}catch(...){c.discardIncompleteSearch();return 4;}
}
API int bpf_copy(void* handle,Result* results,int count,uint8_t* bytes,int capacity,int* needed) noexcept {
    if(!handle||!needed)return 3;auto& c=*static_cast<Context*>(handle);*needed=int(c.output.size());if(count<int(c.results.size())||capacity<*needed||(!results&&!c.results.empty())||(!bytes&&*needed))return 3;
    std::copy(c.results.begin(),c.results.end(),results);if(*needed)std::copy(c.output.begin(),c.output.end(),bytes);return int(c.results.size());
}
API int bpf_stats(void* handle,Stats* s) noexcept {
    if(!handle||!s)return 3;auto& c=*static_cast<Context*>(handle);i64 cached=0;for(const auto& f:c.cache)if(f)++cached;*s={i64(c.rectangles.size()),i64(c.nodes.size()),0,c.fields,c.hits,c.macros,c.expanded,c.budget.bytes,c.budget.peak,c.refinements,c.localFields,c.localHits,c.evictions,i64(c.results.size()),i64(c.output.size()),cached};
    for(int n=0;n<c.count;++n)if(c.isValid(n)&&c.vertex[n]<0)++s->pruned;return 0;
}
