#define CD_EXPORTS
#include "Protocol.h"
#include "ChunkRegistry.h"
#include <string>
#include <vector>
#include <stdio.h>
#include <Wincrypt.h>
#pragma comment(lib,"advapi32.lib")

namespace {
cd::Shared* shared = nullptr;
HANDLE backing = INVALID_HANDLE_VALUE, mapping = nullptr, collector = nullptr;
PVOID veh = nullptr;
SRWLOCK initLock = SRWLOCK_INIT, contextLock = SRWLOCK_INIT, mapLock = SRWLOCK_INIT;
__declspec(thread) bool handling = false;
__declspec(thread) bool naming = false;
struct KnownMethod { uint64_t method, address, length; };
KnownMethod knownMethods[16384]{};
cd::ChunkRegistry<> chunks;

std::wstring Extended(const wchar_t* p) {
    std::wstring s(p ? p : L"");
    if (s.compare(0, 4, L"\\\\?\\") == 0) return s;
    if (s.compare(0, 2, L"\\\\") == 0) return L"\\\\?\\UNC\\" + s.substr(2);
    return L"\\\\?\\" + s;
}
template<size_t N> bool CopyW(wchar_t (&out)[N], const wchar_t* in) {
    size_t n = in ? wcslen(in) : 0;
    if (n >= N) return false;
    if (n) memcpy(out, in, n * sizeof(wchar_t)); out[n] = 0; return true;
}
template<size_t N> bool CopyA(char (&out)[N], const char* in) {
    size_t n = 0; if (in) while (in[n] && n < N - 1) { out[n] = in[n]; ++n; }
    out[n] = 0; return in && in[n] != 0;
}
bool Executable(DWORD p) {
    return !(p & (PAGE_GUARD | PAGE_NOACCESS)) &&
        (p & (PAGE_EXECUTE | PAGE_EXECUTE_READ | PAGE_EXECUTE_READWRITE | PAGE_EXECUTE_WRITECOPY));
}
bool Readable(DWORD p) {
    return !(p & (PAGE_GUARD | PAGE_NOACCESS)) &&
        (p & (PAGE_READONLY | PAGE_READWRITE | PAGE_WRITECOPY | PAGE_EXECUTE_READ | PAGE_EXECUTE_READWRITE | PAGE_EXECUTE_WRITECOPY));
}

void AddMap(uint32_t kind, uint64_t address, uint64_t length, uint64_t method,
            const char* name, const char* image, const char* sha) {
    if (!shared) return;
    AcquireSRWLockExclusive(&mapLock);
    uint32_t metadataFlags = 0;
    if (kind == cd::ChunkCreated && !chunks.Created(address,length)) metadataFlags |= 2;
    if (kind == cd::ChunkDestroyed) {
        // Carry the span on the priority tombstone, even if its generic creation
        // record has already been omitted by the collector's separate budget.
        length = chunks.Destroyed(address);
        if (!length) metadataFlags |= 2; // Unknown span: consumers must invalidate conservatively.
        // A reused address must get a fresh mapping even if Mono reuses its method pointer.
        ZeroMemory(knownMethods,sizeof(knownMethods));
    }
    if (kind == cd::Jit || kind == cd::MethodFreed) {
        size_t key = size_t((method >> 4) % _countof(knownMethods));
        size_t entry = key;
        // Fixed hash, no allocation under the JIT callback; collisions replace a cache entry.
        for (size_t n = 0; n < 32; ++n) {
            size_t candidate = (key + n) % _countof(knownMethods);
            if (!knownMethods[candidate].method || knownMethods[candidate].method == method) { entry = candidate; break; }
        }
        auto& known = knownMethods[entry];
        if (kind == cd::Jit && known.method == method && known.address == address && known.length == length) {
            ReleaseSRWLockExclusive(&mapLock); return;
        }
        known = {method,address,length};
    }
    LONG64 seq = InterlockedIncrement64(&shared->header.mapSequence);
    if (seq > cd::MapCount) InterlockedIncrement64(&shared->header.mapDropped);
    cd::CodeMap& rec = shared->maps[(seq - 1) % cd::MapCount];
    InterlockedExchange64(&rec.commit,0);
    rec.sequence = seq; rec.qpc = cd::QpcNow(); rec.address = address; rec.length = length;
    rec.method = method; rec.kind = kind;
    rec.truncated = metadataFlags | uint32_t(CopyA(rec.name, name)) |
        uint32_t(CopyA(rec.image, image)) | uint32_t(CopyA(rec.sha256, sha));
    MemoryBarrier(); InterlockedExchange64(&rec.commit, seq);
    ReleaseSRWLockExclusive(&mapLock);
}

void AddRange(cd::CaptureSlot& slot, uint64_t address, uint32_t requested, uint32_t kind) {
    auto& h = slot.header;
    if (h.rangeCount == cd::RangeCount) { ++h.truncated; return; }
    // De-duplicate identical starting addresses, preserving the first and highest-priority range.
    for (uint32_t i = 0; i < h.rangeCount; ++i) if (h.ranges[i].address == address) return;
    auto& r = h.ranges[h.rangeCount++];
    r.address = address; r.requested = requested; r.kind = kind; r.copied = 0;
    r.payloadOffset = h.payloadBytes; r.error = 0; r.protect = r.state = r.type = 0;
    MEMORY_BASIC_INFORMATION mbi{};
    if (!VirtualQuery(reinterpret_cast<void*>(address), &mbi, sizeof(mbi))) { r.error = GetLastError(); return; }
    r.protect = mbi.Protect; r.state = mbi.State; r.type = mbi.Type;
    if (mbi.State != MEM_COMMIT || !Readable(mbi.Protect)) { r.error = ERROR_NOACCESS; return; }
    uint64_t regionEnd = uint64_t(mbi.BaseAddress) + mbi.RegionSize;
    if (address >= regionEnd) { r.error = ERROR_INVALID_ADDRESS; return; }
    uint64_t available = regionEnd - address;
    size_t capacity = sizeof(slot.payload) - h.payloadBytes;
    size_t bytes = requested;
    if (bytes > available) bytes = size_t(available);
    if (bytes > capacity) bytes = capacity;
    if (bytes < requested) ++h.truncated;
    if (!bytes) { r.error = ERROR_INSUFFICIENT_BUFFER; return; }
    SIZE_T copied = 0;
    if (!ReadProcessMemory(GetCurrentProcess(), reinterpret_cast<void*>(address), slot.payload + h.payloadBytes, bytes, &copied))
        r.error = GetLastError();
    r.copied = uint32_t(copied); h.payloadBytes += r.copied;
}

bool Reportable(DWORD code) {
    switch (code) {
    case EXCEPTION_ACCESS_VIOLATION: case EXCEPTION_IN_PAGE_ERROR: case EXCEPTION_ILLEGAL_INSTRUCTION:
    case EXCEPTION_INT_DIVIDE_BY_ZERO: case EXCEPTION_INT_OVERFLOW: case EXCEPTION_PRIV_INSTRUCTION:
    case EXCEPTION_STACK_OVERFLOW: case EXCEPTION_ARRAY_BOUNDS_EXCEEDED:
    case EXCEPTION_NONCONTINUABLE_EXCEPTION: case 0xC0000374: case 0xC0000409: return true;
    default: return false;
    }
}

LONG CALLBACK Observe(EXCEPTION_POINTERS* p) {
    if (!shared || !p || !p->ExceptionRecord || !p->ContextRecord || handling || naming || !Reportable(p->ExceptionRecord->ExceptionCode))
        return EXCEPTION_CONTINUE_SEARCH;
    handling = true;
    LONG64 seq = InterlockedIncrement64(&shared->header.captureSequence);
    auto& slot = shared->captures[(seq - 1) % cd::CaptureCount];
    if (InterlockedCompareExchange(&slot.header.busy, 1, 0) != 0) {
        InterlockedIncrement64(&shared->header.captureDropped); handling = false; return EXCEPTION_CONTINUE_SEARCH;
    }
    auto& h = slot.header;
    InterlockedExchange64(&h.commit, 0); h.sequence = seq; h.headerBytes = sizeof(h);
    h.qpc = cd::QpcNow(); h.utc = cd::UtcNow(); h.threadId = GetCurrentThreadId();
    h.rangeCount = h.payloadBytes = h.truncated = 0;
    h.trailSequence = shared->header.trailSequence; h.mapSequence = shared->header.mapSequence;
    h.contextSequence = 0; h.sessionContext[0] = 0;
    h.exception = *p->ExceptionRecord; h.context = *p->ContextRecord;
    // The shared context uses a seqlock; a racing writer never makes the VEH wait.
    LONG64 contextSeq = shared->header.contextSequence;
    if (!(contextSeq & 1)) {
        memcpy(h.sessionContext, shared->header.sessionContext, sizeof(h.sessionContext)); MemoryBarrier();
        if (contextSeq == shared->header.contextSequence) h.contextSequence = contextSeq;
        else h.sessionContext[0] = 0;
    }
    // Stack exhaustion requires a minimal record; do not consume more of the exhausted stack.
    if (h.exception.ExceptionCode != EXCEPTION_STACK_OVERFLOW) {
        AddRange(slot, h.context.Rip & ~uint64_t(4095), 16384, cd::Code);
        AddRange(slot, h.context.Rsp, 256 * 1024, cd::Stack);
        const uint64_t regs[] = {h.context.Rax,h.context.Rbx,h.context.Rcx,h.context.Rdx,h.context.Rsi,h.context.Rdi,
            h.context.Rbp,h.context.R8,h.context.R9,h.context.R10,h.context.R11,h.context.R12,h.context.R13,h.context.R14,h.context.R15};
        for (auto value : regs) if (value) AddRange(slot, value & ~uint64_t(4095), 4096, cd::Register);
        // Stack values are only candidates, never presented as an unwind result.
        for (uint32_t i = 0; i < h.rangeCount; ++i) {
            const auto& r = h.ranges[i]; if (r.kind != cd::Stack) continue;
            uint32_t limit = r.copied > 32768 ? 32768 : r.copied;
            for (uint32_t off = 0; off + 8 <= limit && h.rangeCount < cd::RangeCount; off += 8) {
                uint64_t candidate; memcpy(&candidate, slot.payload + r.payloadOffset + off, 8);
                MEMORY_BASIC_INFORMATION mbi{};
                if (candidate && VirtualQuery(reinterpret_cast<void*>(candidate), &mbi, sizeof(mbi)) && mbi.State == MEM_COMMIT && Executable(mbi.Protect))
                    AddRange(slot, candidate & ~uint64_t(4095), 4096, cd::StackCodeCandidate);
            }
            break;
        }
    }
    MemoryBarrier(); InterlockedExchange64(&h.commit, seq); InterlockedExchange(&h.busy, 0);
    handling = false;
    return EXCEPTION_CONTINUE_SEARCH;
}

// ABI declarations match Mono's modern profiler callbacks and embedding accessors.
using PtrFn = void* (__cdecl*)();
using PtrArgFn = void* (__cdecl*)(void*);
using IntArgFn = int (__cdecl*)(void*);
using FindFn = void* (__cdecl*)(void*,void*);
using NameFn = char* (__cdecl*)(void*,int);
using FreeFn = void (__cdecl*)(void*);
using JitCallback = void (__cdecl*)(void*,void*,void*);
using MethodCallback = void (__cdecl*)(void*,void*);
using ProfCreateFn = void* (__cdecl*)(void*);
using SetJitFn = void (__cdecl*)(void*,JitCallback);
using SetMethodFn = void (__cdecl*)(void*,MethodCallback);
using BufferCallback = void (__cdecl*)(void*,const void*,uint64_t,int,const void*);
using ChunkCallback = void (__cdecl*)(void*,const void*,uintptr_t);
using ChunkFreeCallback = void (__cdecl*)(void*,const void*);
using SetBufferFn = void (__cdecl*)(void*,BufferCallback);
using SetChunkFn = void (__cdecl*)(void*,ChunkCallback);
using SetChunkFreeFn = void (__cdecl*)(void*,ChunkFreeCallback);
PtrArgFn codeStart = nullptr, getMethod = nullptr, methodClass = nullptr, classImage = nullptr, imageName = nullptr;
IntArgFn codeSize = nullptr;
FindFn findJit = nullptr; NameFn methodName = nullptr; FreeFn monoFree = nullptr;
void* rootDomain = nullptr;
uintptr_t bootstrapAddress = 0;
bool monoAttempted = false;
bool TestedMono(HMODULE module) {
    // Exact file identity exercised by the independent Unity-Mono embedding regression.
    // A changed runtime disables only optional symbol profiling, never raw crash capture.
    static const char expected[]="6ef9f938fe54c1d4ed6958f892596a39721b488d4ebed1a54f1644ecb230216f";
    wchar_t path[32768]{};DWORD chars=GetModuleFileNameW(module,path,_countof(path));
    if(!chars || chars>=_countof(path))return false;
    auto extended=Extended(path);
    HANDLE file=CreateFileW(extended.c_str(),GENERIC_READ,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,nullptr,OPEN_EXISTING,FILE_FLAG_SEQUENTIAL_SCAN,nullptr);
    if(file==INVALID_HANDLE_VALUE)return false;
    HCRYPTPROV provider=0;HCRYPTHASH hash=0;bool ok=CryptAcquireContextW(&provider,nullptr,nullptr,PROV_RSA_AES,CRYPT_VERIFYCONTEXT|CRYPT_SILENT)!=FALSE;
    if(ok)ok=CryptCreateHash(provider,CALG_SHA_256,0,0,&hash)!=FALSE;
    BYTE buffer[65536];DWORD bytes=0;
    while(ok){if(!ReadFile(file,buffer,sizeof(buffer),&bytes,nullptr)){ok=false;break;}if(!bytes)break;ok=CryptHashData(hash,buffer,bytes,0)!=FALSE;}
    BYTE digest[32];DWORD length=sizeof(digest);if(ok)ok=CryptGetHashParam(hash,HP_HASHVAL,digest,&length,0)!=FALSE && length==sizeof(digest);
    if(hash)CryptDestroyHash(hash);if(provider)CryptReleaseContext(provider,0);CloseHandle(file);
    char text[65]{};static const char hex[]="0123456789abcdef";
    if(ok)for(size_t i=0;i<sizeof(digest);++i){text[2*i]=hex[digest[i]>>4];text[2*i+1]=hex[digest[i]&15];}
    AddMap(cd::Image,uint64_t(module),0,0,"mono-2.0-bdwgc.dll observed file identity","",ok?text:"hash-unavailable");
    return ok && strcmp(text,expected)==0;
}
void RecordJit(void* method, void* info) {
    if (!shared || naming || !method || !info) return;
    naming = true;
    __try {
        uint64_t start = uint64_t(codeStart(info)); int size = codeSize(info);
        if (start && size > 0) {
            char* name = methodName(method, 1);
            void* klass = methodClass(method); void* image = klass ? classImage(klass) : nullptr;
            const char* imageText = image ? reinterpret_cast<const char*>(imageName(image)) : "unknown";
            AddMap(cd::Jit, start, uint64_t(size), uint64_t(method), name ? name : "unknown", imageText, "");
            if (name) monoFree(name);
        }
    } __except(EXCEPTION_EXECUTE_HANDLER) { InterlockedIncrement(&shared->header.monoErrors); }
    naming = false;
}
void __cdecl JitDone(void*,void* method,void* info) { RecordJit(method,info); }
void __cdecl MethodFreed(void*,void* method) { AddMap(cd::MethodFreed,0,0,uint64_t(method),"","",""); }
void __cdecl BufferCreated(void*,const void* buffer,uint64_t length,int type,const void*) {
    char label[64]{};_snprintf_s(label,_countof(label),_TRUNCATE,"Mono generated buffer type=%d",type);
    AddMap(cd::CodeBuffer,uint64_t(buffer),length,0,label,"mono-2.0-bdwgc","");
}
void __cdecl ChunkCreated(void*,const void* chunk,uintptr_t length) { AddMap(cd::ChunkCreated,uint64_t(chunk),length,0,"Mono JIT chunk","mono-2.0-bdwgc",""); }
void __cdecl ChunkDestroyed(void*,const void* chunk) { AddMap(cd::ChunkDestroyed,uint64_t(chunk),0,0,"Mono JIT chunk destroyed","mono-2.0-bdwgc",""); }
void TryMono() {
    if (monoAttempted || !shared) return;
    HMODULE module = GetModuleHandleW(L"mono-2.0-bdwgc.dll");
    if (!module) { shared->header.monoState = -1; return; }
    monoAttempted = true;
    if(!TestedMono(module)){shared->header.monoState=-5;return;}
#define MONO_GET(var, type, name) var = reinterpret_cast<type>(GetProcAddress(module,name)); if (!var) { shared->header.monoState=-2; return; }
    PtrFn domainFn; ProfCreateFn create; SetJitFn setJit; SetMethodFn setFree;
    SetBufferFn setBuffer;SetChunkFn setChunk;SetChunkFreeFn setChunkFree;
    MONO_GET(domainFn,PtrFn,"mono_get_root_domain")
    MONO_GET(codeStart,PtrArgFn,"mono_jit_info_get_code_start")
    MONO_GET(codeSize,IntArgFn,"mono_jit_info_get_code_size")
    MONO_GET(getMethod,PtrArgFn,"mono_jit_info_get_method")
    MONO_GET(methodClass,PtrArgFn,"mono_method_get_class")
    MONO_GET(classImage,PtrArgFn,"mono_class_get_image")
    MONO_GET(imageName,PtrArgFn,"mono_image_get_name")
    MONO_GET(findJit,FindFn,"mono_jit_info_table_find")
    MONO_GET(methodName,NameFn,"mono_method_full_name")
    MONO_GET(monoFree,FreeFn,"mono_free")
    MONO_GET(create,ProfCreateFn,"mono_profiler_create")
    MONO_GET(setJit,SetJitFn,"mono_profiler_set_jit_done_callback")
    MONO_GET(setFree,SetMethodFn,"mono_profiler_set_method_free_callback")
    MONO_GET(setBuffer,SetBufferFn,"mono_profiler_set_jit_code_buffer_callback")
    MONO_GET(setChunk,SetChunkFn,"mono_profiler_set_jit_chunk_created_callback")
    MONO_GET(setChunkFree,SetChunkFreeFn,"mono_profiler_set_jit_chunk_destroyed_callback")
#undef MONO_GET
    rootDomain = domainFn(); if (!rootDomain) { shared->header.monoState=-3; return; }
    void* handle = create(nullptr); if (!handle) { shared->header.monoState=-4; return; }
    // Own modern handle; do not replace legacy/global profiler callbacks.
    setFree(handle,MethodFreed);setBuffer(handle,BufferCreated);setChunk(handle,ChunkCreated);setChunkFree(handle,ChunkDestroyed);setJit(handle,JitDone);
    shared->header.monoState = 1;
}
}

uint32_t WINAPI CD_GetAbiVersion() { return cd::Version; }
uint32_t WINAPI CD_Initialize(const wchar_t* directory,const wchar_t* collectorExe,const wchar_t* bep,const wchar_t* player,const wchar_t* crashes) {
    AcquireSRWLockExclusive(&initLock);
    if (shared) { ReleaseSRWLockExclusive(&initLock); return ERROR_ALREADY_INITIALIZED; }
    if (!directory || !collectorExe) { ReleaseSRWLockExclusive(&initLock); return ERROR_INVALID_PARAMETER; }
    auto path = Extended(directory) + L"\\emergency.bin";
    backing = CreateFileW(path.c_str(),GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,nullptr,CREATE_NEW,FILE_ATTRIBUTE_NORMAL,nullptr);
    DWORD error = ERROR_SUCCESS;
    if (backing == INVALID_HANDLE_VALUE) error = GetLastError();
    if (!error) {
        mapping = CreateFileMappingW(backing,nullptr,PAGE_READWRITE,0,sizeof(cd::Shared),nullptr);
        if (!mapping) error=GetLastError();
    }
    if (!error) {
        shared = reinterpret_cast<cd::Shared*>(MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,sizeof(cd::Shared)));
        if (!shared) error=GetLastError();
    }
    if (!error) {
        // Materialize the bounded backing file before publishing callbacks. A full disk
        // must fail during startup, not on the first write to a previously untouched slot.
        ZeroMemory(shared,sizeof(cd::Shared));
        if(!FlushViewOfFile(shared,sizeof(cd::Shared)) || !FlushFileBuffers(backing)) error=GetLastError();
    }
    if (!error) {
        auto& h=shared->header; h.magic=cd::Magic;h.version=cd::Version;h.headerBytes=sizeof(h);h.fileBytes=sizeof(cd::Shared);
        h.processId=GetCurrentProcessId();h.architecture=64;FILETIME created,exited,kernel,user;
        GetProcessTimes(GetCurrentProcess(),&created,&exited,&kernel,&user);h.processCreatedUtc=cd::FileTimeValue(created);
        h.startedUtc=cd::UtcNow();h.startedQpc=cd::QpcNow();LARGE_INTEGER frequency;QueryPerformanceFrequency(&frequency);h.qpcFrequency=frequency.QuadPart;
        if (!CopyW(h.bepLog,bep)||!CopyW(h.playerLog,player)||!CopyW(h.crashDirectory,crashes)) error=ERROR_FILENAME_EXCED_RANGE;
    }
    if (!error) {
        std::wstring command = L"\""+std::wstring(collectorExe)+L"\" --watch \""+std::wstring(directory)+L"\"";
        STARTUPINFOW startup{};startup.cb=sizeof(startup);PROCESS_INFORMATION process{};
        auto executablePath=Extended(collectorExe);
        if (!CreateProcessW(executablePath.c_str(),&command[0],nullptr,nullptr,FALSE,CREATE_NO_WINDOW,nullptr,nullptr,&startup,&process)) error=GetLastError();
        else {
            collector=process.hProcess;CloseHandle(process.hThread);
            ULONGLONG deadline=GetTickCount64()+5000;
            while (!shared->header.collectorReady && GetTickCount64()<deadline && WaitForSingleObject(collector,10)==WAIT_TIMEOUT) {}
            if (!shared->header.collectorReady) error=shared->header.collectorError?DWORD(shared->header.collectorError):ERROR_TIMEOUT;
        }
    }
    if (!error) { TryMono();veh=AddVectoredExceptionHandler(1,Observe);if(!veh)error=GetLastError()?GetLastError():ERROR_GEN_FAILURE; }
    if (error) {
        // No published handler exists on this path; release only this failed candidate.
        if (shared) { shared->header.collectorError=error;UnmapViewOfFile(shared);shared=nullptr; }
        if(mapping){CloseHandle(mapping);mapping=nullptr;}if(backing!=INVALID_HANDLE_VALUE){CloseHandle(backing);backing=INVALID_HANDLE_VALUE;}
        if(collector){CloseHandle(collector);collector=nullptr;}
    }
    ReleaseSRWLockExclusive(&initLock);return error;
}
uint32_t WINAPI CD_RegisterImage(uint64_t address,uint64_t length,const char* name,const char* sha) {
    if (!shared || !address || !length || address+length<address) return ERROR_INVALID_PARAMETER;
    AddMap(cd::Image,address,length,0,name,"",sha);return ERROR_SUCCESS;
}
uint32_t WINAPI CD_PublishContext(const char* json) {
    if (!shared || !json) return ERROR_INVALID_PARAMETER;
    size_t n=strlen(json);if(n>=cd::ContextBytes)return ERROR_INSUFFICIENT_BUFFER;
    AcquireSRWLockExclusive(&contextLock);InterlockedIncrement64(&shared->header.contextSequence);
    memcpy(shared->header.sessionContext,json,n+1);MemoryBarrier();InterlockedIncrement64(&shared->header.contextSequence);
    ReleaseSRWLockExclusive(&contextLock);return ERROR_SUCCESS;
}
void WINAPI CD_RecordEvent(uint32_t kind,int64_t a,int64_t b,int64_t c) {
    if(!shared)return;LONG64 seq=InterlockedIncrement64(&shared->header.trailSequence);
    auto& r=shared->trail[(seq-1)%cd::TrailCount];
    // CAS claim prevents an unusually delayed writer from racing a full ring wrap.
    LONG64 previous=r.sequence;
    if(r.commit!=previous || InterlockedCompareExchange64(&r.sequence,-seq,previous)!=previous){InterlockedIncrement64(&shared->header.trailDropped);return;}
    r.commit=0;r.qpc=cd::QpcNow();r.utc=cd::UtcNow();r.threadId=GetCurrentThreadId();r.kind=kind;r.value1=a;r.value2=b;r.value3=c;
    r.sequence=seq;MemoryBarrier();InterlockedExchange64(&r.commit,seq);
}
void WINAPI CD_MarkShutdown(){if(shared){shared->header.shutdownQpc=cd::QpcNow();InterlockedExchange(&shared->header.cleanShutdown,1);}}
uint32_t WINAPI CD_GetStatus(){
    if(!shared)return 0;
    bool ready=false;
    if(collector){
        DWORD state=WaitForSingleObject(collector,0);
        ready=state==WAIT_TIMEOUT && shared->header.collectorReady && !shared->header.collectorError;
        if(state!=WAIT_TIMEOUT && !shared->header.collectorError){
            InterlockedExchange(&shared->header.collectorError,state==WAIT_FAILED?LONG(GetLastError()):LONG(ERROR_BROKEN_PIPE));
            InterlockedExchange(&shared->header.collectorReady,0);
        }
    }
    return (veh?cd::Active:0)|(ready?cd::CollectorReady:0)|
        (shared->header.monoState==1?cd::MonoProfiler:0)|(shared->header.bootstrapDone?cd::BootstrapDone:0);
}
void WINAPI CD_Bootstrap(uint32_t budgetMicros){
    if(!shared || handling || shared->header.cleanShutdown || !budgetMicros)return;TryMono();if(shared->header.monoState!=1||shared->header.bootstrapDone)return;
    if(budgetMicros>1000)budgetMicros=1000;uint64_t stop=cd::QpcNow()+shared->header.qpcFrequency*budgetMicros/1000000;
    MEMORY_BASIC_INFORMATION mbi{};
    while(cd::QpcNow()<stop){
        if(!VirtualQuery(reinterpret_cast<void*>(bootstrapAddress),&mbi,sizeof(mbi))){shared->header.bootstrapDone=1;break;}
        uintptr_t end=uintptr_t(mbi.BaseAddress)+mbi.RegionSize;if(end<=bootstrapAddress){shared->header.bootstrapDone=1;break;}
        if(mbi.State!=MEM_COMMIT||mbi.Type!=MEM_PRIVATE||!Executable(mbi.Protect)){bootstrapAddress=end;continue;}
        void* info=findJit(rootDomain,reinterpret_cast<void*>(bootstrapAddress));InterlockedIncrement(&shared->header.bootstrapCalls);
        if(info){void* method=getMethod(info);RecordJit(method,info);int size=codeSize(info);uintptr_t start=uintptr_t(codeStart(info));
            uintptr_t next=size>0 && start<=UINTPTR_MAX-uintptr_t(size)?start+uintptr_t(size):0;
            bootstrapAddress=next>bootstrapAddress?next:bootstrapAddress+16;}
        else bootstrapAddress+=16;
    }
}
