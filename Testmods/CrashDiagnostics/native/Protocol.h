#pragma once
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <Windows.h>
#include <stdint.h>
#include <stddef.h>

// On-disk ABI v1: little endian, Windows x64 CONTEXT, default /Zp8 packing.
// Writers finish every record by publishing commit == sequence with a release barrier.
namespace cd {
constexpr uint32_t Version = 1;
constexpr uint64_t Magic = 0x3147414944434853ULL; // SHCDIAG1
constexpr uint32_t CaptureCount = 16;
constexpr uint32_t CaptureBytes = 1024 * 1024;
constexpr uint32_t RangeCount = 96;
constexpr uint32_t TrailCount = 4096;
constexpr uint32_t MapCount = 8192;
constexpr uint32_t ContextBytes = 4096;
constexpr uint32_t PathChars = 4096;
constexpr uint64_t SessionBudget = 128ULL * 1024 * 1024;
constexpr uint64_t RootBudget = 512ULL * 1024 * 1024;

enum : uint32_t { Image = 1, Jit = 2, MethodFreed = 3, CodeBuffer = 4, ChunkCreated = 5, ChunkDestroyed = 6 };
enum : uint32_t { Code = 1, Stack = 2, Register = 3, StackCodeCandidate = 4 };
enum : uint32_t { Active = 1, CollectorReady = 2, MonoProfiler = 4, BootstrapDone = 8 };

struct MemoryRange {
    uint64_t address;
    uint32_t requested, copied, payloadOffset, error;
    uint32_t kind, protect, state, type;
};
static_assert(sizeof(MemoryRange) == 40, "range ABI");

struct CaptureHeader {
    volatile LONG64 sequence, commit;
    volatile LONG busy;
    uint32_t headerBytes;
    uint64_t qpc, utc;
    uint32_t threadId, rangeCount, payloadBytes, truncated;
    uint64_t trailSequence, mapSequence, contextSequence;
    EXCEPTION_RECORD exception;
    CONTEXT context;
    char sessionContext[ContextBytes];
    MemoryRange ranges[RangeCount];
};
struct CaptureSlot {
    CaptureHeader header;
    uint8_t payload[CaptureBytes - sizeof(CaptureHeader)];
};
static_assert(sizeof(CaptureSlot) == CaptureBytes, "capture ABI: exactly 1 MiB");

struct TrailRecord {
    volatile LONG64 sequence, commit;
    uint64_t qpc, utc;
    uint32_t threadId, kind;
    int64_t value1, value2, value3;
};
static_assert(sizeof(TrailRecord) == 64, "trail ABI");

struct CodeMap {
    volatile LONG64 sequence, commit;
    uint64_t qpc, address, length, method;
    uint32_t kind, truncated; // Flags: 1=text truncated, 2=chunk registry span unavailable.
    char name[512], image[128], sha256[72];
};
static_assert(sizeof(CodeMap) == 768, "code-map ABI");

struct Header {
    uint64_t magic;
    uint32_t version, headerBytes;
    uint64_t fileBytes;
    uint32_t processId, architecture;
    uint64_t processCreatedUtc, startedUtc, startedQpc, qpcFrequency;
    volatile LONG64 captureSequence, captureDropped, trailSequence, trailDropped;
    volatile LONG64 mapSequence, mapDropped, contextSequence;
    volatile LONG cleanShutdown, collectorReady, collectorPid, collectorError;
    volatile LONG monoState, bootstrapDone, bootstrapCalls, monoErrors;
    uint64_t shutdownQpc;
    char sessionContext[ContextBytes];
    wchar_t bepLog[PathChars], playerLog[PathChars], crashDirectory[PathChars];
};
struct Shared {
    Header header;
    TrailRecord trail[TrailCount];
    CodeMap maps[MapCount];
    CaptureSlot captures[CaptureCount];
};
static_assert(sizeof(CONTEXT) == 1232, "Windows x64 CONTEXT is part of ABI v1");
static_assert(sizeof(Shared) < 32 * 1024 * 1024, "shared data budget");

inline uint64_t FileTimeValue(FILETIME t) { return (uint64_t(t.dwHighDateTime) << 32) | t.dwLowDateTime; }
inline uint64_t UtcNow() { FILETIME t; GetSystemTimeAsFileTime(&t); return FileTimeValue(t); }
inline uint64_t QpcNow() { LARGE_INTEGER t; QueryPerformanceCounter(&t); return uint64_t(t.QuadPart); }
}

#ifdef CD_EXPORTS
#define CD_API extern "C" __declspec(dllexport)
#else
#define CD_API extern "C" __declspec(dllimport)
#endif
CD_API uint32_t WINAPI CD_GetAbiVersion();
CD_API uint32_t WINAPI CD_Initialize(const wchar_t*, const wchar_t*, const wchar_t*, const wchar_t*, const wchar_t*);
CD_API uint32_t WINAPI CD_RegisterImage(uint64_t, uint64_t, const char*, const char*);
CD_API uint32_t WINAPI CD_PublishContext(const char*);
CD_API void WINAPI CD_RecordEvent(uint32_t, int64_t, int64_t, int64_t);
CD_API void WINAPI CD_MarkShutdown();
CD_API uint32_t WINAPI CD_GetStatus();
CD_API void WINAPI CD_Bootstrap(uint32_t);
