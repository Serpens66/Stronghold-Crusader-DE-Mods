#include "../../native/Protocol.h"
#include "../../native/ChunkRegistry.h"
#include <stdio.h>
#include <string>
#include <vector>

namespace {
using Init = uint32_t (WINAPI*)(const wchar_t*,const wchar_t*,const wchar_t*,const wchar_t*,const wchar_t*);
using Record = void (WINAPI*)(uint32_t,int64_t,int64_t,int64_t);
using Shutdown = void (WINAPI*)();
using Context = uint32_t (WINAPI*)(const char*);
using Image = uint32_t (WINAPI*)(uint64_t,uint64_t,const char*,const char*);
using Bootstrap = void (WINAPI*)(uint32_t);
using Status = uint32_t (WINAPI*)();
Record record = nullptr;
volatile LONG handled = 0;
LONG CALLBACK OtherHandler(EXCEPTION_POINTERS* p) {
    if (p && p->ExceptionRecord->ExceptionCode == EXCEPTION_ACCESS_VIOLATION) InterlockedIncrement(&handled);
    return EXCEPTION_CONTINUE_SEARCH;
}
__declspec(noinline) int ReadAt(volatile int* p) { return *p; }
int CaughtRead(volatile int* p) {
    __try { return ReadAt(p); }
    __except(EXCEPTION_EXECUTE_HANDLER) { return -1; }
}
DWORD WINAPI Worker(void* index) {
    for (int i=0;i<16;++i) { CaughtRead(nullptr);record(2,int64_t(index),i,0); }
    return 0;
}
std::string Utf8(const wchar_t* value) {
    int n=WideCharToMultiByte(CP_UTF8,0,value,-1,nullptr,0,nullptr,nullptr);
    std::string result(n,0);WideCharToMultiByte(CP_UTF8,0,value,-1,&result[0],n,nullptr,nullptr);return result;
}
int MonoHost(HMODULE dll,const wchar_t* monoPath,const wchar_t* managedPath,const wchar_t* assemblyPath) {
    HMODULE mono=LoadLibraryW(monoPath);if(!mono)return 80;
    auto dirs=reinterpret_cast<void(__cdecl*)(const char*,const char*)>(GetProcAddress(mono,"mono_set_dirs"));
    auto assembliesPath=reinterpret_cast<void(__cdecl*)(const char*)>(GetProcAddress(mono,"mono_set_assemblies_path"));
    auto init=reinterpret_cast<void*(__cdecl*)(const char*,const char*)>(GetProcAddress(mono,"mono_jit_init_version"));
    auto open=reinterpret_cast<void*(__cdecl*)(void*,const char*)>(GetProcAddress(mono,"mono_domain_assembly_open"));
    auto image=reinterpret_cast<void*(__cdecl*)(void*)>(GetProcAddress(mono,"mono_assembly_get_image"));
    auto klass=reinterpret_cast<void*(__cdecl*)(void*,const char*,const char*)>(GetProcAddress(mono,"mono_class_from_name"));
    auto method=reinterpret_cast<void*(__cdecl*)(void*,const char*,int)>(GetProcAddress(mono,"mono_class_get_method_from_name"));
    auto invoke=reinterpret_cast<void*(__cdecl*)(void*,void*,void**,void**)>(GetProcAddress(mono,"mono_runtime_invoke"));
    auto unbox=reinterpret_cast<void*(__cdecl*)(void*)>(GetProcAddress(mono,"mono_object_unbox"));
    auto build=reinterpret_cast<const char*(__cdecl*)()>(GetProcAddress(mono,"mono_get_runtime_build_info"));
    auto bootstrap=reinterpret_cast<Bootstrap>(GetProcAddress(dll,"CD_Bootstrap"));
    auto status=reinterpret_cast<Status>(GetProcAddress(dll,"CD_GetStatus"));
    if(!dirs||!assembliesPath||!init||!open||!image||!klass||!method||!invoke||!unbox||!bootstrap||!status)return 81;
    std::string managed=Utf8(managedPath),assembly=Utf8(assemblyPath);dirs(managed.c_str(),managed.c_str());assembliesPath(managed.c_str());
    void* domain=init("CrashDiagnostics.Tests","v4.0.30319");if(!domain)return 82;
    printf("MONO runtime=%s\n",build?build():"unknown");
    bootstrap(1000);if(!(status() & cd::MonoProfiler))return 83;
    void* loaded=open(domain,assembly.c_str());if(!loaded)return 84;
    void* c=klass(image(loaded),"CrashDiagnostics.Tests","MonoProbe");if(!c)return 85;
    void* m=method(c,"Compute",1);if(!m)return 86;
    int input=21;void* args[]={&input};void* exception=nullptr;void* result=invoke(m,nullptr,args,&exception);
    if(exception||!result||*static_cast<int*>(unbox(result))!=42)return 87;
    bootstrap(1000);record(2,7,42,0);return 0;
}
int CollectorLoss(HMODULE dll,const wchar_t* collectorPath,const wchar_t* session) {
    std::wstring file=std::wstring(session)+L"\\emergency.bin";
    HANDLE f=CreateFileW(file.c_str(),GENERIC_READ,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,nullptr,OPEN_EXISTING,0,nullptr);
    if(f==INVALID_HANDLE_VALUE)return 90;
    HANDLE mapping=CreateFileMappingW(f,nullptr,PAGE_READONLY,0,0,nullptr);if(!mapping){CloseHandle(f);return 91;}
    auto shared=reinterpret_cast<const cd::Shared*>(MapViewOfFile(mapping,FILE_MAP_READ,0,0,0));
    if(!shared){CloseHandle(mapping);CloseHandle(f);return 92;}
    DWORD pid=shared->header.collectorPid;uint64_t started=shared->header.startedUtc;
    HANDLE process=OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION|PROCESS_TERMINATE|SYNCHRONIZE,FALSE,pid);
    wchar_t actual[32768];DWORD chars=_countof(actual);FILETIME created,exited,kernel,user;
    bool matches=process && QueryFullProcessImageNameW(process,0,actual,&chars) &&
        GetProcessTimes(process,&created,&exited,&kernel,&user) && cd::FileTimeValue(created)>=started;
    std::wstring image=matches?actual:L"";if(image.compare(0,4,L"\\\\?\\")==0)image=image.substr(4);
    wchar_t expected[32768];GetFullPathNameW(collectorPath,_countof(expected),expected,nullptr);
    matches=matches && _wcsicmp(image.c_str(),expected)==0;
    bool terminated=matches && TerminateProcess(process,91);
    if(terminated)WaitForSingleObject(process,5000);
    if(process)CloseHandle(process);UnmapViewOfFile(shared);CloseHandle(mapping);CloseHandle(f);
    if(!terminated)return 93;
    auto status=reinterpret_cast<Status>(GetProcAddress(dll,"CD_GetStatus"));
    if(!status || !(status()&cd::Active) || (status()&cd::CollectorReady))return 94;
    if(CaughtRead(nullptr)!=-1)return 95;
    record(2,7,91,0);return 0;
}
}

int wmain(int argc,wchar_t** argv) {
    if(argc==2 && wcscmp(argv[1],L"--chunk-registry")==0) {
        cd::ChunkRegistry<8,2> registry;
        if(registry.Created(0,4096)||registry.Created(UINT64_MAX-7,16))return 101;
        // Three equal hashes exercise bounded-full behavior without huge fixtures.
        if(!registry.Created(0x1000,0x2000)||!registry.Created(0x9000,0x3000)||registry.Created(0x11000,0x4000))return 102;
        if(registry.Destroyed(0x11000)!=0||registry.Destroyed(0x1000)!=0x2000)return 103;
        if(registry.Destroyed(0x9000)!=0x3000||registry.Destroyed(0x9000)!=0)return 104;
        if(!registry.Created(0x11000,0x4000)||!registry.Created(0x11000,0x5000)||registry.Destroyed(0x11000)!=0x5000)return 105;
        printf("PASS: production chunk registry known-span, missing, collision, full and address-reuse cases\n");return 0;
    }
    if(argc==2 && wcscmp(argv[1],L"--layout")==0) {
        printf("{\"shared\":%zu,\"header\":%zu,\"captureHeader\":%zu,\"capture\":%zu,\"map\":%zu,\"trail\":%zu,"
            "\"trailOffset\":%zu,\"mapsOffset\":%zu,\"capturesOffset\":%zu,\"exceptionOffset\":%zu,\"contextOffset\":%zu,\"rangesOffset\":%zu,\"contextTextOffset\":%zu}\n",
            sizeof(cd::Shared),sizeof(cd::Header),sizeof(cd::CaptureHeader),sizeof(cd::CaptureSlot),sizeof(cd::CodeMap),sizeof(cd::TrailRecord),
            offsetof(cd::Shared,trail),offsetof(cd::Shared,maps),offsetof(cd::Shared,captures),offsetof(cd::CaptureHeader,exception),offsetof(cd::CaptureHeader,context),offsetof(cd::CaptureHeader,ranges),offsetof(cd::CaptureHeader,sessionContext));
        return 0;
    }
    if(argc != 5 && argc != 8) { fwprintf(stderr,L"Usage: TestHost DLL COLLECTOR SESSION MODE [MONO_DLL MANAGED_DIR PROBE_ASSEMBLY]\n");return 64; }
    SetErrorMode(SEM_NOGPFAULTERRORBOX|SEM_FAILCRITICALERRORS);
    if(!CreateDirectoryW(argv[3],nullptr) && GetLastError()!=ERROR_ALREADY_EXISTS)return 65;
    HMODULE dll=LoadLibraryW(argv[1]);if(!dll){fwprintf(stderr,L"LoadLibrary %lu\n",GetLastError());return 66;}
    auto init=reinterpret_cast<Init>(GetProcAddress(dll,"CD_Initialize"));
    record=reinterpret_cast<Record>(GetProcAddress(dll,"CD_RecordEvent"));
    auto shutdown=reinterpret_cast<Shutdown>(GetProcAddress(dll,"CD_MarkShutdown"));
    auto context=reinterpret_cast<Context>(GetProcAddress(dll,"CD_PublishContext"));
    auto image=reinterpret_cast<Image>(GetProcAddress(dll,"CD_RegisterImage"));
    if(!init||!record||!shutdown||!context||!image)return 67;
    // This independent handler proves the production VEH never consumes the exception.
    AddVectoredExceptionHandler(0,OtherHandler);
    DWORD initStatus=init(argv[3],argv[2],L"",L"",L"");
    if(initStatus){fwprintf(stderr,L"CD_Initialize %lu\n",initStatus);return 68;}
    if(context("{\"testHost\":true,\"missionId\":7}")!=0)return 69;
    HMODULE own=GetModuleHandleW(nullptr);auto dos=reinterpret_cast<IMAGE_DOS_HEADER*>(own);
    auto nt=reinterpret_cast<IMAGE_NT_HEADERS64*>(reinterpret_cast<BYTE*>(own)+dos->e_lfanew);
    image(uint64_t(own),nt->OptionalHeader.SizeOfImage,"CrashDiagnostics.TestHost.exe","");
    record(11,7,0,0);
    std::wstring mode(argv[4]);
    if(mode==L"mono-unsupported") {
        if(argc!=8 || !LoadLibraryW(argv[5]))return 96;
        auto bootstrap=reinterpret_cast<Bootstrap>(GetProcAddress(dll,"CD_Bootstrap"));
        auto status=reinterpret_cast<Status>(GetProcAddress(dll,"CD_GetStatus"));
        if(!bootstrap||!status)return 97;bootstrap(1000);
        if(!(status()&cd::Active)||(status()&cd::MonoProfiler))return 98;
        if(CaughtRead(nullptr)!=-1)return 99;record(2,7,-5,0);shutdown();return 0;
    }
    if(mode==L"mono") {
        if(argc!=8)return 78;int result=MonoHost(dll,argv[5],argv[6],argv[7]);if(result)return result;
        shutdown();Sleep(50);return 0;
    }
    if(mode==L"collectorloss") {
        int result=CollectorLoss(dll,argv[2],argv[3]);if(result)return result;
        shutdown();return 0;
    }
    if(mode==L"crash") { Sleep(50);ReadAt(nullptr);return 70; }
    if(mode==L"caught") {
        if(CaughtRead(nullptr)!=-1||handled!=1)return 71;
        record(2,7,1,0);
    } else if(mode==L"guard") {
        void* region=VirtualAlloc(nullptr,8192,MEM_RESERVE|MEM_COMMIT,PAGE_READWRITE);if(!region)return 72;
        DWORD old;VirtualProtect(static_cast<BYTE*>(region)+4096,4096,PAGE_NOACCESS,&old);
        if(CaughtRead(reinterpret_cast<int*>(static_cast<BYTE*>(region)+4096))!=-1)return 73;
        record(2,7,2,0);VirtualFree(region,0,MEM_RELEASE);
    } else if(mode==L"concurrent") {
        HANDLE threads[8];for(uintptr_t i=0;i<8;++i)threads[i]=CreateThread(nullptr,0,Worker,reinterpret_cast<void*>(i),0,nullptr);
        for(auto thread:threads)if(!thread)return 74;
        WaitForMultipleObjects(8,threads,TRUE,INFINITE);for(auto thread:threads)CloseHandle(thread);
        if(handled!=128)return 75;
    } else if(mode==L"rotate") {
        for(int i=0;i<40;++i){if(CaughtRead(nullptr)!=-1)return 76;record(2,7,i,0);}
    } else if(mode==L"trail") {
        for(int i=0;i<10000;++i)record(2,7,i,0);
    } else if(mode!=L"clean") return 77;
    shutdown();Sleep(50);
    printf("PASS host mode=%ls handled=%ld\n",argv[4],handled);
    return 0;
}
