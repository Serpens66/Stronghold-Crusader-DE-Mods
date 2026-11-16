// The tests compile the production implementation, not a second tailer or ZIP writer.
#define wmain CollectorProgramMain
#include "Collector.cpp"
#undef wmain

namespace {
std::string ReadTail(const std::wstring& path, size_t bytes = 32) {
    Handle file(CreateFileW(Long(path).c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
    if (!file.valid() || !Regular(file)) throw std::runtime_error("Test output missing");
    uint64_t length = FileSize(file);
    bytes = static_cast<size_t>(std::min<uint64_t>(bytes, length));
    if (!Seek(file, length - bytes)) throw std::runtime_error("Test seek failed");
    std::string result(bytes, '\0'); DWORD read = 0;
    if (!ReadFile(file, result.data(), static_cast<DWORD>(bytes), &read, nullptr) || read != bytes) throw std::runtime_error("Test short read");
    return result;
}
void Require(bool condition, const char* message) { if (!condition) throw std::runtime_error(message); }
}

int wmain(int argc, wchar_t** argv) {
    if (argc != 2) { fprintf(stderr, "Collector.Tests EMPTY_NEW_DIRECTORY\n"); return 2; }
    try {
        const std::wstring root = Full(argv[1]);
        Require(CreateDirectoryW(Long(root).c_str(), nullptr) != FALSE, "Test directory must be new");
        Subdir(root, L"logs");
        const std::wstring source = Join(root, L"source.log"), output = Join(Join(root, L"logs"), L"LogOutput.log");
        PutText(source, "original");
        Tail tail{source, output}; tail.Poll(root);
        Require(ReadTail(output) == "original", "Initial log capture failed");

        PutText(source, "original-FROM-A-LATER-PROCESS");
        uint64_t now = cd::UtcNow(), future = now + 100000000;
        FILETIME ft{}; ft.dwLowDateTime = static_cast<DWORD>(future); ft.dwHighDateTime = static_cast<DWORD>(future >> 32);
        { Handle file(CreateFileW(Long(source).c_str(), FILE_WRITE_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
          Require(file.valid() && SetFileTime(file, nullptr, nullptr, &ft), "Cannot set isolated fixture timestamp"); }
        tail.Poll(root, now);
        Require(ReadTail(output) == "original", "Final append included a later process generation");

        PutText(source, "x");
        tail.Poll(root, cd::UtcNow() + 100000000);
        Require(ReadTail(output) == "original", "Final append included a truncated source generation");
        tail.Poll(root);
        Require(ReadTail(output) == "originalx" && tail.rotations == 1, "Running log truncation was not tracked");

        const std::wstring large = Join(root, L"large-source.log"), largeOut = Join(Join(root, L"logs"), L"Player.log");
        { Handle file(CreateFileW(Long(large).c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr));
          Require(file.valid(), "Cannot create fixture log"); std::vector<char> block(65536, 'A');
          for (unsigned i = 0; i < 320; ++i) WriteAll(file, block.data(), block.size()); }
        Tail rolling{large, largeOut}; rolling.Poll(root);
        { Handle file(CreateFileW(Long(large).c_str(), FILE_APPEND_DATA, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
          const char marker[] = "LATEST-CRASH-MARKER"; WriteAll(file, marker, sizeof(marker) - 1); }
        rolling.Poll(root);
        Require(ReadTail(largeOut).find("LATEST-CRASH-MARKER") != std::string::npos, "Log stopped capturing after 12 MiB");
        Require(rolling.outputRotations >= 2 && !rolling.capped, "Captured log did not rotate within budget");

        const std::wstring journalPath = Join(root, L"code-maps.bin");
        Journal maps(journalPath, "CDMAPS2\0", sizeof(cd::CodeMap), 40 * MiB, true);
        cd::CodeMap slots[16]{};
        uint64_t sequence = 0;
        auto publish = [&](uint32_t kind) {
            ++sequence; auto& row = slots[(sequence - 1) % 16];
            row.commit = 0; row.sequence = static_cast<LONG64>(sequence);
            row.qpc = sequence; row.address = 0x100000 + sequence * 256; row.length = 256; row.kind = kind;
            row.name[0] = row.image[0] = row.sha256[0] = 0;
            row.commit = static_cast<LONG64>(sequence);
        };
        for (unsigned i = 0; i < 140000; ++i) {
            publish(i % 2 ? cd::CodeBuffer : cd::ChunkCreated);
            if (sequence % 16 == 0) maps.Poll(root, slots, static_cast<LONG64>(sequence));
        }
        maps.Poll(root, slots, static_cast<LONG64>(sequence));
        Require(maps.genericCapped && maps.skippedGeneric > 0 && !maps.capped && maps.missing == 0,
            "Generic map flood did not remain isolated from high-priority reservation");
        const uint32_t priorityKinds[] = {cd::Image, cd::Jit, cd::MethodFreed, cd::ChunkDestroyed};
        for (uint32_t kind : priorityKinds) {
            publish(kind); auto& row = slots[(sequence - 1) % 16];
            strcpy_s(row.name, "named"); strcpy_s(row.image, "demo"); strcpy_s(row.sha256, "abc");
        }
        maps.Poll(root, slots, static_cast<LONG64>(sequence)); maps.Flush();
        Require(maps.priorityBytes == 4 * 76 && !maps.priorityCapped && maps.skippedPriority == 0 && !maps.failed,
            "High-priority names/lifetime records were not preserved after generic cap");
        Handle archived(CreateFileW(Long(journalPath).c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        Require(archived.valid() && Regular(archived), "Cannot inspect production map journal");
        uint8_t journalHeader[24]{}; DWORD headerRead = 0;
        Require(ReadFile(archived, journalHeader, sizeof(journalHeader), &headerRead, nullptr) && headerRead == sizeof(journalHeader) &&
            memcmp(journalHeader, "CDMAPS2\0", 8) == 0 && journalHeader[8] == 2 && journalHeader[12] == 0, "Invalid compact journal header");
        Require(Seek(archived, FileSize(archived) - 4 * 76), "Cannot seek final high-priority archive records");
        for (uint32_t kind : priorityKinds) {
            cd::CodeMap row{}; DWORD read = 0; uint16_t lengths[4]{}; char text[12]{};
            Require(ReadFile(archived, &row, 56, &read, nullptr) && read == 56 && row.sequence == row.commit && row.kind == kind &&
                ReadFile(archived, lengths, sizeof(lengths), &read, nullptr) && read == sizeof(lengths) && lengths[0] == 5 && lengths[1] == 4 && lengths[2] == 3 && !lengths[3] &&
                ReadFile(archived, text, sizeof(text), &read, nullptr) && read == sizeof(text) && memcmp(text, "nameddemoabc", 12) == 0,
                "Archived map journal omitted or changed a high-priority record after generic flood");
        }
        for (unsigned i = 0; i < 100000; ++i) {
            publish(cd::Jit);
            if (sequence % 16 == 0) maps.Poll(root, slots, static_cast<LONG64>(sequence));
        }
        maps.Poll(root, slots, static_cast<LONG64>(sequence)); maps.Flush();
        Require(maps.priorityBytes == 4 * 76 + 100000 * 64 && !maps.priorityCapped && maps.skippedPriority == 0 && !maps.capped && !maps.failed,
            "100000 short compact method records exhausted the priority budget");
        Require(FileSize(archived) == 24 + maps.genericBytes + maps.priorityBytes, "Compact journal size does not match serialized-byte accounting");
        Require(maps.genericBytes <= 8 * MiB && maps.bytes <= 40 * MiB, "Map priority buckets exceeded byte budgets");
        printf("PASS collector: final-generation guards, bounded latest-tail retention, compact map bytes, generic flood and 100000 short priority records\n");
        return 0;
    } catch (const std::exception& error) {
        fprintf(stderr, "FAIL collector: %s (Win32=%lu)\n", error.what(), GetLastError());
        return 1;
    }
}
