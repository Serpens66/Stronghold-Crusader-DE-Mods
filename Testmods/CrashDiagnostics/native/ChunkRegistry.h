#pragma once
#include <stdint.h>
#include <stddef.h>

namespace cd {
// Called only while the normal-context code-map writer lock is held. A bounded
// probe window avoids allocation and unbounded work in Mono profiler callbacks.
template<size_t Capacity = 4096, size_t ProbeLimit = 32>
class ChunkRegistry {
    struct Entry { uint64_t address, length; };
    Entry entries[Capacity]{};
    static_assert(Capacity > 0 && ProbeLimit > 0 && ProbeLimit <= Capacity,"chunk registry bounds");
    static size_t Index(uint64_t address) { return size_t(((address >> 12) ^ (address >> 24)) % Capacity); }
public:
    bool Created(uint64_t address, uint64_t length) {
        if (!address || !length || address > UINT64_MAX - length) return false;
        size_t firstFree = Capacity;
        size_t key = Index(address);
        for (size_t i = 0; i < ProbeLimit; ++i) {
            size_t index = (key + i) % Capacity;
            auto& entry = entries[index];
            if (entry.address == address) { entry.length = length; return true; }
            if (!entry.address && firstFree == Capacity) firstFree = index;
        }
        if (firstFree == Capacity) return false;
        entries[firstFree] = {address,length}; return true;
    }
    uint64_t Destroyed(uint64_t address) {
        if (!address) return 0;
        size_t key = Index(address);
        // Do not stop at holes: another colliding chunk can remain after removal.
        for (size_t i = 0; i < ProbeLimit; ++i) {
            auto& entry = entries[(key + i) % Capacity];
            if (entry.address == address) { uint64_t length = entry.length; entry = {}; return length; }
        }
        return 0;
    }
};
}
