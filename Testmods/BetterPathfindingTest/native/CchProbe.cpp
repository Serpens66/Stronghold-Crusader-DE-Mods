// Owned-data, diagnostic-only CCH feasibility gate. Original implementation.
// Metric-independent symbolic Cholesky fill; directed basic customization.
// References and the measured acceptance decision are recorded in native/SOURCES.md.
#ifndef SERP_DEVDIAG
#error CCH prototype requires an explicit diagnostic build.
#endif
#include <algorithm>
#include <chrono>
#include <cstdint>
#include <memory>
#include <stdexcept>
#include <vector>

namespace cch {
using i64 = int64_t;
using u32 = uint32_t;
constexpr u32 infinity = 0x3fffffff;
constexpr int dx[8] = {0,1,1,1,0,-1,-1,-1}, dy[8] = {-1,-1,0,1,1,1,0,-1};
struct Exhausted {};
struct Memory {
    i64 maximum, bytes=65536, peak=65536, calls=0;
    explicit Memory(i64 limit): maximum(limit) { if (limit<bytes) throw Exhausted(); }
    void take(size_t n) { if(n>size_t(maximum-bytes))throw Exhausted(); bytes+=i64(n);peak=std::max(peak,bytes);++calls; }
    void give(size_t n) { bytes-=i64(n); }
};
template<class T> struct Alloc {
    using value_type=T; using propagate_on_container_move_assignment=std::true_type;
    Memory* m;
    explicit Alloc(Memory* p=nullptr):m(p){}
    template<class U> Alloc(const Alloc<U>& a):m(a.m){}
    T* allocate(size_t n) { if(n>(size_t(-1)-32)/sizeof(T))throw Exhausted();m->take(n*sizeof(T)+32);try{return static_cast<T*>(::operator new(n*sizeof(T)));}catch(...){m->give(n*sizeof(T)+32);throw;} }
    void deallocate(T* p,size_t n) {::operator delete(p);m->give(n*sizeof(T)+32);}
    template<class U> bool operator==(const Alloc<U>& a)const{return m==a.m;}
    template<class U> bool operator!=(const Alloc<U>& a)const{return m!=a.m;}
};
template<class T> using Vec=std::vector<T,Alloc<T>>;
struct Pair {int start,target,maximum,index;};
struct Result {int status,index,steps,offset,length,reserved;double milliseconds;};
static_assert(sizeof(Pair)==16 && sizeof(Result)==32,"CCH private ABI");
struct Arc {int id;bool forward;};
struct Label {int rank;u32 distance;int parentArc;};
struct Memo {Vec<Label> labels;explicit Memo(Memory* m):labels(Alloc<Label>(m)){};};
struct Context {
    Memory memory;int width,height,n,phase=0,cursor=0,sub=0,pairA=-1,pairB=1;
    i64 triangles=0,queryCount=0,expanded=0,directCount=0;
    Vec<uint8_t> valid;Vec<u32> edges;
    Vec<int> order,rank,parent,ancestor,marks,offset,head,middleUp,middleDown;
    Vec<Vec<int>> higher;
    Vec<u32> up,down,fd,bd;Vec<u32> fs,bs;Vec<int> fp,bp;
    Vec<Arc> chain;Vec<uint8_t> path,output;Vec<Pair> requests;Vec<Result> results;
    Vec<int> sourceCounts,goalCounts,sourceMemo;Vec<Memo> memos;
    Vec<u32> goalDistance;Vec<uint8_t> goalNext;
    u32 stamp=0,backStamp=0;int lastBackward=-1,lastField=-1;
    i64 fieldBuilds=0,fieldHits=0,sourceBuilds=0,sourceHits=0;
    int batchCursor=0;bool fullOutput=false;
    std::chrono::steady_clock::time_point batchStart;
    Context(int w,int h,const uint8_t* v,const u32* e,i64 limit):memory(limit),width(w),height(h),n(w*h),
        valid(v,v+n,Alloc<uint8_t>(&memory)),edges(e,e+n,Alloc<u32>(&memory)),
        order(Alloc<int>(&memory)),rank(n,-1,Alloc<int>(&memory)),parent(n,-1,Alloc<int>(&memory)),ancestor(n,-1,Alloc<int>(&memory)),marks(n,-1,Alloc<int>(&memory)),
        offset(Alloc<int>(&memory)),head(Alloc<int>(&memory)),middleUp(Alloc<int>(&memory)),middleDown(Alloc<int>(&memory)),higher(Alloc<Vec<int>>(&memory)),
        up(Alloc<u32>(&memory)),down(Alloc<u32>(&memory)),fd(n,infinity,Alloc<u32>(&memory)),bd(n,infinity,Alloc<u32>(&memory)),fs(n,0,Alloc<u32>(&memory)),bs(n,0,Alloc<u32>(&memory)),fp(n,-1,Alloc<int>(&memory)),bp(n,-1,Alloc<int>(&memory)),
        chain(Alloc<Arc>(&memory)),path(Alloc<uint8_t>(&memory)),output(Alloc<uint8_t>(&memory)),requests(Alloc<Pair>(&memory)),results(Alloc<Result>(&memory)),
        sourceCounts(n,0,Alloc<int>(&memory)),goalCounts(n,0,Alloc<int>(&memory)),sourceMemo(n,-1,Alloc<int>(&memory)),memos(Alloc<Memo>(&memory)),goalDistance(Alloc<u32>(&memory)),goalNext(Alloc<uint8_t>(&memory)) {
        order.reserve(n);higher.reserve(n);for(int i=0;i<n;++i)higher.emplace_back(Alloc<int>(&memory));
    }
    int neighbor(int node,int d)const {int x=node%width+dx[d],y=node/width+dy[d];return x>=0&&y>=0&&x<width&&y<height?y*width+x:-1;}
    int distance(int a,int b)const{return std::max(std::abs(a%width-b%width),std::abs(a/width-b/width));}
    void rectangleOrder(int x,int y,int w,int h) {
        if(!w||!h)return;
        if(w*h<=32){for(int yy=y;yy<y+h;++yy)for(int xx=x;xx<x+w;++xx)order.push_back(yy*width+xx);return;}
        if(w>=h){int left=w/2;rectangleOrder(x,y,left,h);rectangleOrder(x+left+1,y,w-left-1,h);rectangleOrder(x+left,y,1,h);}
        else {int top=h/2;rectangleOrder(x,y,w,top);rectangleOrder(x,y+top+1,w,h-top-1);rectangleOrder(x,y+top,w,1);}
    }
    template<class F> void originalLower(int r,F emit)const {
        int node=order[r];for(int d=0;d<8;++d){int to=neighbor(node,d);if(to>=0&&rank[to]<r)emit(rank[to]);}
    }
    int arc(int a,int b)const {
        auto first=head.begin()+offset[a],last=head.begin()+offset[a+1];auto p=std::lower_bound(first,last,b);
        if(p==last||*p!=b)throw std::logic_error("CCH missing symbolic triangle edge");return int(p-head.begin());
    }
    bool allowed(int a,int b)const {
        if(!valid[a]||!valid[b])return false;
        for(int d=0;d<8;++d)if(neighbor(a,d)==b)return (edges[a]&(1u<<d))&&!(edges[a]&(1u<<(8+d)));
        return false;
    }
    u32 base(int a,int b)const{return allowed(order[a],order[b])?1:infinity;}
    int step(int microseconds) {
        auto deadline=std::chrono::steady_clock::now()+std::chrono::microseconds(microseconds);
        int checked=0;
        do {
            if(phase==0){rectangleOrder(0,0,width,height);for(int r=0;r<n;++r)rank[order[r]]=r;phase=1;}
            if(phase==1){
                if(cursor==n){phase=2;cursor=0;continue;}
                int r=cursor++;originalLower(r,[&](int i){while(i>=0&&i<r){int next=ancestor[i];ancestor[i]=r;if(next<0)parent[i]=r;i=next;}});
            }else if(phase==2){
                if(cursor==n){offset.resize(n+1);offset[0]=0;for(int r=0;r<n;++r){if(higher[r].size()>size_t(INT32_MAX-offset[r]))throw Exhausted();offset[r+1]=offset[r]+int(higher[r].size());}head.resize(offset[n]);phase=3;cursor=0;continue;}
                int r=cursor++;originalLower(r,[&](int i){while(i>=0&&i<r&&marks[i]!=r){marks[i]=r;higher[i].push_back(r);i=parent[i];}});
            }else if(phase==3){
                if(cursor==n){Vec<Vec<int>>(Alloc<Vec<int>>(&memory)).swap(higher);Vec<int>(Alloc<int>(&memory)).swap(ancestor);Vec<int>(Alloc<int>(&memory)).swap(marks);up.resize(head.size());down.resize(head.size());middleUp.assign(head.size(),-1);middleDown.assign(head.size(),-1);phase=4;cursor=0;sub=0;continue;}
                int r=cursor++;std::copy(higher[r].begin(),higher[r].end(),head.begin()+offset[r]);Vec<int>(Alloc<int>(&memory)).swap(higher[r]);
            }else if(phase==4){
                if(cursor==n){phase=5;cursor=0;pairA=-1;pairB=0;continue;}
                int r=cursor;int id=offset[r]+sub;
                if(id==offset[r+1]){++cursor;sub=0;continue;}++sub;
                up[id]=base(r,head[id]);down[id]=base(head[id],r);middleUp[id]=middleDown[id]=-1;
            }else if(phase==5){
                if(cursor==n){phase=6;return 0;}
                int first=offset[cursor],end=offset[cursor+1];
                if(pairA<0){pairA=first;pairB=first+1;}
                if(pairA>=end-1){++cursor;pairA=-1;pairB=0;continue;}
                if(pairB>=end){++pairA;pairB=pairA+1;continue;}
                int a=pairA,b=pairB++;++triangles;
                bool f=down[a]<infinity&&up[b]<infinity,back=down[b]<infinity&&up[a]<infinity;
                if(!f&&!back)continue;
                int c=arc(head[a],head[b]);
                u32 candidate=down[a]+up[b];if(f&&candidate<up[c]){up[c]=candidate;middleUp[c]=cursor;}
                candidate=down[b]+up[a];if(back&&candidate<down[c]){down[c]=candidate;middleDown[c]=cursor;}
            }else return 0;
        }while((++checked&255)!=0||std::chrono::steady_clock::now()<deadline);
        return 1;
    }
    u32 getF(int r)const{return fs[r]==stamp?fd[r]:infinity;}
    u32 getB(int r)const{return bs[r]==backStamp?bd[r]:infinity;}
    void setF(int r,u32 d,int p){fs[r]=stamp;fd[r]=d;fp[r]=p;}
    void setB(int r,u32 d,int p){bs[r]=backStamp;bd[r]=d;bp[r]=p;}
    void backward(int target) {
        if(lastBackward==target)return;
        lastBackward=target;if(++backStamp==0){std::fill(bs.begin(),bs.end(),0);++backStamp;}
        int start=rank[target];setB(start,0,-1);
        for(int r=start;r>=0;r=parent[r]){u32 cost=getB(r);if(cost==infinity)continue;for(int id=offset[r];id<offset[r+1];++id){++expanded;u32 next=cost+down[id];if(down[id]<infinity&&next<getB(head[id]))setB(head[id],next,id);}}
    }
    void forwardAll(int source) {
        setF(source,0,-1);
        for(int r=source;r>=0;r=parent[r]){u32 cost=getF(r);if(cost==infinity)continue;for(int id=offset[r];id<offset[r+1];++id){++expanded;u32 next=cost+up[id];if(up[id]<infinity&&next<getF(head[id]))setF(head[id],next,id);}}
    }
    void field(int target) {
        if(lastField==target){++fieldHits;return;}
        lastField=-1;goalDistance.assign(n,infinity);goalNext.assign(n,255);Vec<int> queue{Alloc<int>(&memory)};queue.reserve(n);queue.push_back(target);goalDistance[target]=0;
        for(size_t i=0;i<queue.size();++i){int node=queue[i];for(int d=0;d<8;++d){int from=neighbor(node,(d+4)&7);if(from>=0&&goalDistance[from]==infinity&&allowed(from,node)){goalDistance[from]=goalDistance[node]+1;goalNext[from]=uint8_t(d);queue.push_back(from);}}}
        lastField=target;++fieldBuilds;
    }
    void appendBase(int a,int b) {int from=order[a],to=order[b];for(int d=0;d<8;++d)if(neighbor(from,d)==to){if(!allowed(from,to))throw std::logic_error("CCH illegal unpacked edge");path.push_back(uint8_t(d));return;}throw std::logic_error("CCH non-neighbor original arc");}
    void unpack(int a,int b,bool forward) {
        if(!fullOutput&&path.size()>=8)return;
        int id=arc(a,b),mid=forward?middleUp[id]:middleDown[id];
        if(mid<0){appendBase(forward?a:b,forward?b:a);return;}
        if(mid>=a)throw std::logic_error("CCH cyclic witness");
        if(forward){unpack(mid,a,false);unpack(mid,b,true);}else {unpack(mid,b,false);unpack(mid,a,true);}
    }
    bool straight(const Pair& q) {
        int node=q.start;path.clear();while(node!=q.target){int sx=(q.target%width>node%width)-(q.target%width<node%width),sy=(q.target/width>node/width)-(q.target/width<node/width),d=0;while(dx[d]!=sx||dy[d]!=sy)++d;int next=neighbor(node,d);if(next<0||!allowed(node,next)){path.clear();return false;}if(fullOutput||path.size()<8)path.push_back(uint8_t(d));node=next;}return true;
    }
    Result query(const Pair& q,int index) {
        Result result{1,index,0,int(output.size()),0,0,0};++queryCount;path.clear();
        if(q.start<0||q.target<0||q.start>=n||q.target>=n||q.maximum<0||!valid[q.start]||!valid[q.target])return result;
        if(straight(q)){++directCount;result.steps=distance(q.start,q.target);if(result.steps<=q.maximum)result.status=0;}
        else if(goalCounts[q.target]>=32||lastField==q.target) {
            field(q.target);u32 steps=goalDistance[q.start];if(steps==infinity||steps>u32(q.maximum))return result;
            result.status=0;result.steps=int(steps);int node=q.start,limit=fullOutput?int(steps):std::min(8,int(steps));
            for(int i=0;i<limit;++i){int d=goalNext[node];if(d>=8)throw std::logic_error("CCH goal field witness");path.push_back(uint8_t(d));node=neighbor(node,d);}
        } else {
            if(++stamp==0){std::fill(fs.begin(),fs.end(),0);++stamp;}
            int source=rank[q.start],target=rank[q.target],meeting=-1;u32 best=infinity;backward(q.target);
            int memo=sourceMemo[q.start];bool cached=memo>=0;
            if(cached){++sourceHits;for(const auto& v:memos[memo].labels)setF(v.rank,v.distance,v.parentArc);}
            else if(sourceCounts[q.start]>1&&memory.bytes<memory.maximum-memory.maximum/8){
                forwardAll(source);memo=int(memos.size());memos.emplace_back(&memory);sourceMemo[q.start]=memo;
                for(int r=source;r>=0;r=parent[r])memos[memo].labels.push_back({r,getF(r),fp[r]});++sourceBuilds;cached=true;
            } else setF(source,0,-1);
            for(int r=source;r>=0;r=parent[r]){
                u32 f=getF(r),back=getB(r);if(f<infinity&&back<infinity&&f+back<best){best=f+back;meeting=r;}
                if(cached||f==infinity||f+u32(distance(order[r],q.target))>=best)continue;
                for(int id=offset[r];id<offset[r+1];++id){++expanded;u32 next=f+up[id];if(up[id]<infinity&&next<getF(head[id]))setF(head[id],next,id);}
            }
            if(meeting<0||best>u32(q.maximum))return result;
            // Parent arcs point from the lower-rank predecessor to this rank.
            chain.clear();int current=meeting;
            while(current!=source){int id=fp[current];if(id<0)throw std::logic_error("CCH forward parent");int lo=int(std::upper_bound(offset.begin(),offset.end(),id)-offset.begin())-1;chain.push_back({id,true});current=lo;}
            std::reverse(chain.begin(),chain.end());for(const auto& item:chain){int lo=int(std::upper_bound(offset.begin(),offset.end(),item.id)-offset.begin())-1;unpack(lo,head[item.id],true);}
            current=meeting;while(current!=target){int id=bp[current];if(id<0)throw std::logic_error("CCH backward parent");int lo=int(std::upper_bound(offset.begin(),offset.end(),id)-offset.begin())-1;unpack(lo,head[id],false);current=lo;}
            if(path.size()!=(fullOutput?best:std::min(8u,best)))throw std::logic_error("CCH distance/witness mismatch");result.steps=int(best);result.status=0;
        }
        if(result.status==0){result.length=fullOutput?int(path.size()):std::min(8,int(path.size()));output.insert(output.end(),path.begin(),path.begin()+result.length);}
        return result;
    }
    void begin(const Pair* values,int count,bool full) {
        if(phase!=6)throw std::logic_error("CCH not ready");requests.assign(values,values+count);results.clear();output.clear();batchCursor=0;fullOutput=full;batchStart=std::chrono::steady_clock::now();
        std::fill(sourceCounts.begin(),sourceCounts.end(),0);std::fill(goalCounts.begin(),goalCounts.end(),0);
        for(const auto& q:requests){if(q.start<0||q.target<0||q.start>=n||q.target>=n)throw std::invalid_argument("CCH batch coordinates");++sourceCounts[q.start];++goalCounts[q.target];}
        std::sort(requests.begin(),requests.end(),[](const Pair&a,const Pair&b){return a.target!=b.target?a.target<b.target:a.start!=b.start?a.start<b.start:a.index<b.index;});
    }
    int batchStep(int micros) {
        auto deadline=std::chrono::steady_clock::now()+std::chrono::microseconds(micros);
        do {if(batchCursor==int(requests.size()))return 0;auto r=query(requests[batchCursor],requests[batchCursor].index);r.milliseconds=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-batchStart).count();results.push_back(r);++batchCursor;}while(std::chrono::steady_clock::now()<deadline);
        return batchCursor==int(requests.size())?0:1;
    }
    void replace(const uint8_t* v,const u32* e) {
        if(phase!=6)throw std::logic_error("CCH replacement while unfinished");
        std::copy(v,v+n,valid.begin());std::copy(e,e+n,edges.begin());phase=4;cursor=0;sub=0;pairA=-1;pairB=0;
        results.clear();output.clear();requests.clear();
        lastBackward=lastField=-1;memos.clear();std::fill(sourceMemo.begin(),sourceMemo.end(),-1);
    }
};
}
#define CCH_API extern "C" __declspec(dllexport)
CCH_API int cch_probe_contract() noexcept{return 0x02001020;}
CCH_API int cch_probe_create(int w,int h,const uint8_t* valid,const uint32_t* edges,int64_t limit,void** output) noexcept {
    if(!output)return 3;*output=nullptr;if(w<1||h<1||int64_t(w)*h>640000||!valid||!edges)return 3;
    try{*output=new cch::Context(w,h,valid,edges,limit);return 0;}catch(const cch::Exhausted&){return 2;}catch(const std::bad_alloc&){return 2;}catch(...){return 4;}
}
CCH_API void cch_probe_destroy(void* p) noexcept{delete static_cast<cch::Context*>(p);}
CCH_API int cch_probe_step(void* p,int micros) noexcept{if(!p||micros<1||micros>1000000)return 3;try{return static_cast<cch::Context*>(p)->step(micros);}catch(const cch::Exhausted&){return 2;}catch(const std::bad_alloc&){return 2;}catch(...){return 4;}}
CCH_API int cch_probe_stats(void* p,int64_t* values,int count) noexcept{if(!p||!values||count!=16)return 3;auto& c=*static_cast<cch::Context*>(p);int64_t data[16]={c.phase,c.cursor,int64_t(c.head.size()),c.memory.bytes,c.memory.peak,c.triangles,c.queryCount,c.expanded,c.directCount,c.memory.calls,int64_t(c.results.size()),int64_t(c.output.size()),c.fieldBuilds,c.fieldHits,c.sourceBuilds,c.sourceHits};std::copy(data,data+16,values);return 0;}
CCH_API int cch_probe_begin(void* p,const cch::Pair* q,int count,int full) noexcept{if(!p||!q||count<1||count>10000)return 3;try{static_cast<cch::Context*>(p)->begin(q,count,full!=0);return 0;}catch(const cch::Exhausted&){return 2;}catch(const std::bad_alloc&){return 2;}catch(...){return 4;}}
CCH_API int cch_probe_batch_step(void* p,int micros) noexcept{if(!p||micros<1)return 3;try{return static_cast<cch::Context*>(p)->batchStep(micros);}catch(const cch::Exhausted&){return 2;}catch(const std::bad_alloc&){return 2;}catch(...){return 4;}}
CCH_API int cch_probe_copy(void* p,cch::Result* results,int count,uint8_t* bytes,int capacity) noexcept{if(!p||!results)return 3;auto& c=*static_cast<cch::Context*>(p);if(count!=int(c.results.size())||capacity<int(c.output.size())||(!bytes&&!c.output.empty()))return 3;std::copy(c.results.begin(),c.results.end(),results);if(!c.output.empty())std::copy(c.output.begin(),c.output.end(),bytes);return 0;}
CCH_API int cch_probe_replace(void* p,const uint8_t* valid,const uint32_t* words) noexcept{if(!p||!valid||!words)return 3;try{static_cast<cch::Context*>(p)->replace(valid,words);return 0;}catch(...){return 4;}}
