#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <algorithm>
#include <string>
#include <vector>
#include <set>
#include <sstream>
#include <type_traits>
#include <stdexcept>
#include "../native/Protocol.h"

namespace {
constexpr uint64_t MiB = 1024ull * 1024ull;
constexpr uint64_t SessionLimit = 128 * MiB;
constexpr uint64_t LogLimit = 12 * MiB;
constexpr uint64_t ExternalLimit = 24 * MiB;
constexpr uint64_t Reserve = 6 * MiB; // Managed metadata (4 MiB) and final reports.

struct Handle {
    HANDLE h = INVALID_HANDLE_VALUE;
    explicit Handle(HANDLE value = INVALID_HANDLE_VALUE) : h(value) {}
    ~Handle() { if (h != INVALID_HANDLE_VALUE && h != nullptr) CloseHandle(h); }
    Handle(const Handle&) = delete;
    Handle& operator=(const Handle&) = delete;
    operator HANDLE() const { return h; }
    bool valid() const { return h != INVALID_HANDLE_VALUE && h != nullptr; }
};

std::wstring Full(const std::wstring& path) {
    if (path.empty()) throw std::runtime_error("Empty path");
    DWORD n = GetFullPathNameW(path.c_str(), 0, nullptr, nullptr);
    if (!n || n > 32760) throw std::runtime_error("Invalid or overlong path");
    std::vector<wchar_t> out(n);
    if (!GetFullPathNameW(path.c_str(), n, out.data(), nullptr)) throw std::runtime_error("Path resolution failed");
    std::wstring result(out.data());
    while (result.size() > 3 && result.back() == L'\\') result.pop_back();
    return result;
}
std::wstring Long(const std::wstring& path) {
    if (path.rfind(L"\\\\?\\", 0) == 0) return path;
    std::wstring p = Full(path);
    return p.rfind(L"\\\\", 0) == 0 ? L"\\\\?\\UNC\\" + p.substr(2) : L"\\\\?\\" + p;
}
std::wstring Join(const std::wstring& a, const std::wstring& b) { return a + L"\\" + b; }
std::string Utf8(const std::wstring& text) {
    if (text.empty()) return {};
    int n = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(), static_cast<int>(text.size()), nullptr, 0, nullptr, nullptr);
    if (n <= 0) throw std::runtime_error("Invalid Unicode path");
    std::string result(n, '\0');
    WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(), static_cast<int>(text.size()), &result[0], n, nullptr, nullptr);
    return result;
}
uint64_t Filetime(FILETIME value) { return (uint64_t(value.dwHighDateTime) << 32) | value.dwLowDateTime; }
uint64_t FileSize(HANDLE h) {
    LARGE_INTEGER size{};
    if (!GetFileSizeEx(h, &size) || size.QuadPart < 0) throw std::runtime_error("Cannot read file size");
    return static_cast<uint64_t>(size.QuadPart);
}
bool Regular(HANDLE h) {
    FILE_ATTRIBUTE_TAG_INFO attr{};
    return GetFileInformationByHandleEx(h, FileAttributeTagInfo, &attr, sizeof(attr)) &&
        !(attr.FileAttributes & (FILE_ATTRIBUTE_REPARSE_POINT | FILE_ATTRIBUTE_DIRECTORY)) && GetFileType(h) == FILE_TYPE_DISK;
}
void RejectReparseAncestors(const std::wstring& input) {
    std::wstring path = Full(input);
    for (;;) {
        DWORD attr = GetFileAttributesW(Long(path).c_str());
        if (attr == INVALID_FILE_ATTRIBUTES || (attr & FILE_ATTRIBUTE_REPARSE_POINT)) throw std::runtime_error("Missing or linked directory");
        size_t slash = path.find_last_of(L"\\/");
        if (slash == std::wstring::npos || slash <= 2) break;
        path.resize(slash);
        if (path.rfind(L"\\\\", 0) == 0 && std::count(path.begin(), path.end(), L'\\') <= 3) break;
    }
}
void WriteAll(HANDLE h, const void* data, size_t size) {
    const char* p = static_cast<const char*>(data);
    while (size) {
        DWORD n = 0, request = static_cast<DWORD>(std::min<size_t>(size, 65536));
        if (!WriteFile(h, p, request, &n, nullptr) || n == 0) throw std::runtime_error("Write failed");
        p += n; size -= n;
    }
}
bool Seek(HANDLE h, uint64_t offset) {
    LARGE_INTEGER position{}; position.QuadPart = static_cast<LONGLONG>(offset);
    return SetFilePointerEx(h, position, nullptr, FILE_BEGIN) != FALSE;
}
std::string TimeNow() {
    SYSTEMTIME t{}; GetSystemTime(&t); char s[48]{};
    sprintf_s(s, "%04u-%02u-%02uT%02u:%02u:%02u.%03uZ", t.wYear, t.wMonth, t.wDay, t.wHour, t.wMinute, t.wSecond, t.wMilliseconds);
    return s;
}

struct FileEntry { std::wstring path; std::string relative; uint64_t size; };
bool Allowed(const std::string& relative) {
    if (relative.empty() || relative.find("..") != std::string::npos || relative.find(':') != std::string::npos || relative.find('\\') != std::string::npos) return false;
    const size_t slash = relative.find('/');
    if (slash != std::string::npos) {
        const std::string dir = relative.substr(0, slash);
        return (dir == "logs" || dir == "extender" || dir == "breadcrumbs") && relative.find('/', slash + 1) == std::string::npos;
    }
    return relative == "emergency.bin" || relative == "session.json" || relative == "report.txt" ||
        relative == "inventory.json" || relative == "images.json" || relative == "native-sites.json" || relative == "observer-status.json" ||
        relative == "collector.log" || relative == "collector-status.txt" || relative == "maps.bin" ||
        relative == "codes.bin" || relative == "code-map.bin" || relative == "code-maps.bin" || relative == "trail.bin" || relative == "trace.bin" || relative == "native-status.txt";
}
std::vector<FileEntry> Entries(const std::wstring& root, const std::wstring& sub = L"", bool exportOnly = true, unsigned depth = 0) {
    if (depth > 16) throw std::runtime_error("Session nesting limit exceeded");
    std::vector<FileEntry> result;
    WIN32_FIND_DATAW data{};
    const std::wstring current = sub.empty() ? root : Join(root, sub);
    HANDLE raw = FindFirstFileW(Long(Join(current, L"*")).c_str(), &data);
    if (raw == INVALID_HANDLE_VALUE) return result;
    do {
        std::wstring name(data.cFileName);
        if (name == L"." || name == L"..") continue;
        if (data.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) { FindClose(raw); throw std::runtime_error("Linked entry in session"); }
        if (data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
            if (!exportOnly || (sub.empty() && (name == L"logs" || name == L"extender" || name == L"breadcrumbs"))) {
                auto children = Entries(root, sub.empty() ? name : Join(sub, name), exportOnly, depth + 1); result.insert(result.end(), children.begin(), children.end());
            }
            continue;
        }
        std::string relative = sub.empty() ? Utf8(name) : Utf8(sub) + "/" + Utf8(name);
        if (exportOnly && !Allowed(relative)) continue;
        result.push_back({Join(current, name), relative, (uint64_t(data.nFileSizeHigh) << 32) | data.nFileSizeLow});
        if (result.size() > 512) { FindClose(raw); throw std::runtime_error("Too many session files"); }
    } while (FindNextFileW(raw, &data));
    FindClose(raw);
    return result;
}
uint64_t CountBytes(const std::wstring& root, uint64_t limit) {
    uint64_t total = 0;
    for (const auto& f : Entries(root, L"", false)) { if (f.size > limit || total > limit - f.size) return limit + 1; total += f.size; }
    return total;
}
uint64_t Used(const std::wstring& root) { return CountBytes(root, SessionLimit); }
void PutText(const std::wstring& path, const std::string& content) {
    Handle out(CreateFileW(Long(path).c_str(), GENERIC_WRITE, FILE_SHARE_READ, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
    if (!out.valid() || !Regular(out)) throw std::runtime_error("Cannot create report file");
    WriteAll(out, content.data(), content.size());
    if (!FlushFileBuffers(out)) throw std::runtime_error("Cannot flush report file");
}
void Note(const std::wstring& root, const std::string& message) {
    Handle h(CreateFileW(Long(Join(root, L"collector.log")).c_str(), FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
    if (!h.valid() || !Regular(h) || FileSize(h) >= MiB) return;
    const std::string line = TimeNow() + " " + message + "\r\n";
    WriteAll(h, line.data(), line.size());
}

struct Tail {
    std::wstring source, destination;
    uint64_t position = 0, written = 0, rotations = 0, missed = 0, chunkWritten = 0, outputRotations = 0;
    BY_HANDLE_FILE_INFORMATION identity{};
    bool identified = false, capped = false;
    void Roll(const std::wstring& root) {
        const std::wstring previous = destination + L".previous.log";
        const DWORD attr = GetFileAttributesW(Long(previous).c_str());
        if (attr != INVALID_FILE_ATTRIBUTES && (attr & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT))) throw std::runtime_error("Invalid prior log segment");
        if (!MoveFileExW(Long(destination).c_str(), Long(previous).c_str(), MOVEFILE_REPLACE_EXISTING)) throw std::runtime_error("Cannot rotate captured log");
        chunkWritten = 0; ++outputRotations;
        Note(root, "Rotated captured log; only the latest two 6 MiB segments are retained: " + Utf8(source));
    }
    void Poll(const std::wstring& root, uint64_t lastAllowedWrite = UINT64_MAX) {
        if (source.empty() || capped) return;
        Handle input(CreateFileW(Long(source).c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        if (!input.valid() || !Regular(input)) { ++missed; return; }
        BY_HANDLE_FILE_INFORMATION now{};
        if (!GetFileInformationByHandle(input, &now)) { ++missed; return; }
        uint64_t size = FileSize(input);
        bool changed = identified && (now.dwVolumeSerialNumber != identity.dwVolumeSerialNumber || now.nFileIndexHigh != identity.nFileIndexHigh || now.nFileIndexLow != identity.nFileIndexLow || size < position);
        if (lastAllowedWrite != UINT64_MAX && (changed || Filetime(now.ftLastWriteTime) > lastAllowedWrite)) {
            Note(root, "Final source log append omitted: generation changed or source was written after the observed process exit: " + Utf8(source));
            return;
        }
        if (changed) { ++rotations; position = 0; Note(root, "Source log rotation/truncation observed: " + Utf8(source)); }
        identity = now; identified = true;
        if (size > position + LogLimit) { position = size - LogLimit; Note(root, "Source log prefix omitted to retain the most recent 12 MiB: " + Utf8(source)); }
        if (size <= position) return;
        if (!Seek(input, position)) { ++missed; return; }
        char buffer[32768];
        while (position < size) {
            if (chunkWritten >= LogLimit / 2) Roll(root);
            const uint64_t used = Used(root);
            uint64_t room = used + Reserve >= SessionLimit ? 0ull : SessionLimit - used - Reserve;
            room = std::min(room, LogLimit / 2 - chunkWritten);
            if (!room) { capped = true; Note(root, "Session budget reached while capturing log: " + Utf8(source)); return; }
            Handle out(CreateFileW(Long(destination).c_str(), FILE_APPEND_DATA, FILE_SHARE_READ, nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            if (!out.valid() || !Regular(out)) { ++missed; return; }
            while (position < size && room) {
                DWORD read = 0; const DWORD request = static_cast<DWORD>(std::min<uint64_t>(sizeof(buffer), std::min(room, size - position)));
                if (!ReadFile(input, buffer, request, &read, nullptr) || !read) return;
                WriteAll(out, buffer, read); position += read; written += read; chunkWritten += read; room -= read;
            }
        }
    }
};

uint32_t CrcUpdate(uint32_t crc, const void* bytes, size_t n) {
    const uint8_t* p = static_cast<const uint8_t*>(bytes);
    while (n--) { crc ^= *p++; for (unsigned j = 0; j < 8; ++j) crc = (crc >> 1) ^ (0xedb88320u & (0u - (crc & 1u))); }
    return crc;
}
void U16(HANDLE h, uint16_t x) { WriteAll(h, &x, sizeof(x)); }
void U32(HANDLE h, uint32_t x) { WriteAll(h, &x, sizeof(x)); }
void CheckInactive(const std::wstring& root) {
    Handle file(CreateFileW(Long(Join(root, L"emergency.bin")).c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
    if (!file.valid()) { if (GetLastError() == ERROR_FILE_NOT_FOUND) return; throw std::runtime_error("Cannot check session activity"); }
    if (!Regular(file) || FileSize(file) != sizeof(cd::Shared)) throw std::runtime_error("Invalid emergency file");
    cd::Header header{}; DWORD read = 0;
    if (!ReadFile(file, &header, sizeof(header), &read, nullptr) || read != sizeof(header) || header.magic != cd::Magic || header.version != cd::Version || header.fileBytes != sizeof(cd::Shared)) throw std::runtime_error("Invalid emergency header");
    Handle process(OpenProcess(SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION, FALSE, header.processId));
    if (!process.valid()) { if (GetLastError() == ERROR_INVALID_PARAMETER) return; throw std::runtime_error("Cannot establish whether game is closed"); }
    FILETIME created{}, exited{}, kernel{}, user{};
    if (!GetProcessTimes(process, &created, &exited, &kernel, &user)) throw std::runtime_error("Cannot verify game process identity");
    if (Filetime(created) == header.processCreatedUtc && WaitForSingleObject(process, 0) != WAIT_OBJECT_0) throw std::runtime_error("Close the game before exporting its session");
}
struct Admission {
    Handle mutex;
    bool acquired = false;
    Admission() : mutex(CreateMutexW(nullptr, FALSE, L"Local\\SerpsCrashDiagnostics.Admission")) {
        if (!mutex.valid()) throw std::runtime_error("Cannot open diagnostic admission mutex");
        DWORD result = WaitForSingleObject(mutex, 2000);
        acquired = result == WAIT_OBJECT_0 || result == WAIT_ABANDONED;
        if (!acquired) throw std::runtime_error("Diagnostic admission is busy");
    }
    ~Admission() { if (acquired) ReleaseMutex(mutex); }
};
void CheckArchiveBudget(const std::wstring& session, uint64_t archiveBytes) {
    const size_t slash = session.find_last_of(L"\\/");
    if (slash == std::wstring::npos) throw std::runtime_error("Session has no diagnostic parent");
    const std::wstring root = session.substr(0, slash);
    uint64_t reservations = 0;
    WIN32_FIND_DATAW data{};
    HANDLE find = FindFirstFileW(Long(Join(root, L"*")).c_str(), &data);
    if (find == INVALID_HANDLE_VALUE) throw std::runtime_error("Cannot enumerate diagnostic budget root");
    do {
        std::wstring name(data.cFileName);
        if (name == L"." || name == L"..") continue;
        if (data.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) { FindClose(find); throw std::runtime_error("Linked entry in diagnostic root"); }
        uint64_t bytes = (uint64_t(data.nFileSizeHigh) << 32) | data.nFileSizeLow;
        if (data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
            const std::wstring child = Join(root, name);
            bytes = CountBytes(child, cd::RootBudget);
            if (GetFileAttributesW(Long(Join(child, L"session.json")).c_str()) != INVALID_FILE_ATTRIBUTES) bytes = std::max(bytes, SessionLimit);
        }
        if (bytes > cd::RootBudget || reservations > cd::RootBudget - bytes) { FindClose(find); throw std::runtime_error("Diagnostic root budget already exhausted"); }
        reservations += bytes;
    } while (FindNextFileW(find, &data));
    FindClose(find);
    if (archiveBytes > cd::RootBudget || reservations > cd::RootBudget - archiveBytes) throw std::runtime_error("512 MiB diagnostic root budget cannot also hold this ZIP; copy the session folder elsewhere before exporting");
}
void Export(const std::wstring& root) {
    RejectReparseAncestors(root);
    CheckInactive(root);
    auto files = Entries(root);
    uint64_t total = 0;
    for (const auto& file : files) { if (file.size > SessionLimit || total > SessionLimit - file.size) throw std::runtime_error("Session exceeds export limit"); total += file.size; }
    if (files.empty() || files.size() > 512) throw std::runtime_error("No recognized session files or too many files");
    std::sort(files.begin(), files.end(), [](const FileEntry& a, const FileEntry& b) { return a.relative < b.relative; });
    uint64_t zipBytes = total + 22;
    for (const auto& file : files) zipBytes += 76 + 2 * file.relative.size();
    // Shared with managed session admission, so a parallel game start cannot spend the same budget.
    Admission admission;
    CheckArchiveBudget(root, zipBytes);
    const std::wstring path = root + L".zip";
    Handle zip(CreateFileW(Long(path).c_str(), GENERIC_WRITE | DELETE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr));
    if (!zip.valid()) throw std::runtime_error("Export ZIP already exists or cannot be created");
    struct Central { FileEntry file; uint32_t crc, offset; };
    std::vector<Central> records;
    try {
        for (const auto& file : files) {
            Handle input(CreateFileW(Long(file.path).c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            if (!input.valid() || !Regular(input) || FileSize(input) != file.size) throw std::runtime_error("Session file changed during export");
            uint32_t crc = 0xffffffffu; char buffer[32768]; DWORD n = 0; uint64_t hashed = 0;
            while (hashed < file.size) {
                if (!ReadFile(input, buffer, static_cast<DWORD>(std::min<uint64_t>(sizeof(buffer), file.size - hashed)), &n, nullptr) || !n) throw std::runtime_error("Cannot checksum ZIP input");
                crc = CrcUpdate(crc, buffer, n); hashed += n;
            }
            crc ^= 0xffffffffu;
            if (!Seek(input, 0)) throw std::runtime_error("Cannot rewind export input");
            LARGE_INTEGER zero{}, at{}; if (!SetFilePointerEx(zip, zero, &at, FILE_CURRENT)) throw std::runtime_error("ZIP offset failure");
            if (at.QuadPart > UINT32_MAX || file.relative.size() > UINT16_MAX) throw std::runtime_error("ZIP limit exceeded");
            U32(zip, 0x04034b50); U16(zip, 20); U16(zip, 0x0800); U16(zip, 0); U16(zip, 0); U16(zip, 0x21);
            U32(zip, crc); U32(zip, static_cast<uint32_t>(file.size)); U32(zip, static_cast<uint32_t>(file.size)); U16(zip, static_cast<uint16_t>(file.relative.size())); U16(zip, 0);
            WriteAll(zip, file.relative.data(), file.relative.size());
            uint64_t copied = 0;
            while (copied < file.size) {
                if (!ReadFile(input, buffer, static_cast<DWORD>(std::min<uint64_t>(sizeof(buffer), file.size - copied)), &n, nullptr) || !n) throw std::runtime_error("Short ZIP input read");
                WriteAll(zip, buffer, n); copied += n;
            }
            records.push_back({file, crc, static_cast<uint32_t>(at.QuadPart)});
        }
        LARGE_INTEGER zero{}, centralStart{}, end{};
        SetFilePointerEx(zip, zero, &centralStart, FILE_CURRENT);
        for (const auto& item : records) {
            U32(zip, 0x02014b50); U16(zip, 20); U16(zip, 20); U16(zip, 0x0800); U16(zip, 0); U16(zip, 0); U16(zip, 0x21);
            U32(zip, item.crc); U32(zip, static_cast<uint32_t>(item.file.size)); U32(zip, static_cast<uint32_t>(item.file.size)); U16(zip, static_cast<uint16_t>(item.file.relative.size()));
            U16(zip, 0); U16(zip, 0); U16(zip, 0); U16(zip, 0); U32(zip, 0); U32(zip, item.offset); WriteAll(zip, item.file.relative.data(), item.file.relative.size());
        }
        SetFilePointerEx(zip, zero, &end, FILE_CURRENT);
        U32(zip, 0x06054b50); U16(zip, 0); U16(zip, 0); U16(zip, static_cast<uint16_t>(records.size())); U16(zip, static_cast<uint16_t>(records.size()));
        U32(zip, static_cast<uint32_t>(end.QuadPart - centralStart.QuadPart)); U32(zip, static_cast<uint32_t>(centralStart.QuadPart)); U16(zip, 0);
        if (!FlushFileBuffers(zip)) throw std::runtime_error("Cannot flush ZIP");
    } catch (...) {
        FILE_DISPOSITION_INFO disposition{}; disposition.DeleteFile = TRUE;
        // The output was created exclusively by this invocation; never remove a prior archive.
        SetFileInformationByHandle(zip, FileDispositionInfo, &disposition, sizeof(disposition));
        throw;
    }
}

std::wstring BoundedPath(const wchar_t* text) {
    size_t n = 0; while (n < cd::PathChars && text[n]) ++n;
    if (n == cd::PathChars) throw std::runtime_error("Unterminated protocol path");
    return std::wstring(text, n);
}
std::string BoundedText(const char* text, size_t size) {
    const void* end = memchr(text, 0, size);
    return std::string(text, end ? static_cast<const char*>(end) - text : size);
}
std::set<std::wstring> Names(const std::wstring& directory) {
    std::set<std::wstring> names;
    if (directory.empty()) return names;
    WIN32_FIND_DATAW data{};
    HANDLE find = FindFirstFileW(Long(Join(directory, L"*")).c_str(), &data);
    if (find == INVALID_HANDLE_VALUE) return names;
    do { if (!(data.dwFileAttributes & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT))) names.insert(data.cFileName); } while (FindNextFileW(find, &data));
    FindClose(find); return names;
}
bool CopyBounded(const std::wstring& root, const std::wstring& source, const std::wstring& destination, uint64_t& externalUsed) {
    Handle input(CreateFileW(Long(source).c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
    if (!input.valid() || !Regular(input)) return false;
    const uint64_t size = FileSize(input), used = Used(root);
    if (size > ExternalLimit - externalUsed || used + Reserve > SessionLimit || size > SessionLimit - used - Reserve) { Note(root, "External file omitted by budget: " + Utf8(source)); return false; }
    Handle output(CreateFileW(Long(destination).c_str(), GENERIC_WRITE | DELETE, FILE_SHARE_READ, nullptr, CREATE_NEW, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
    if (!output.valid() || !Regular(output)) return false;
    uint64_t copied = 0;
    try {
        char buffer[32768];
        while (copied < size) {
            DWORD n = 0;
            if (!ReadFile(input, buffer, static_cast<DWORD>(std::min<uint64_t>(sizeof(buffer), size - copied)), &n, nullptr) || !n) throw std::runtime_error("External file incomplete");
            WriteAll(output, buffer, n); copied += n;
        }
        if (!FlushFileBuffers(output)) throw std::runtime_error("External file flush failed");
        externalUsed += copied; return true;
    } catch (...) {
        FILE_DISPOSITION_INFO disposition{}; disposition.DeleteFile = TRUE;
        SetFileInformationByHandle(output, FileDispositionInfo, &disposition, sizeof(disposition));
        throw;
    }
}
void Subdir(const std::wstring& root, const std::wstring& name) {
    const std::wstring path = Join(root, name);
    if (!CreateDirectoryW(Long(path).c_str(), nullptr) && GetLastError() != ERROR_ALREADY_EXISTS) throw std::runtime_error("Cannot create capture subdirectory");
    const DWORD attr = GetFileAttributesW(Long(path).c_str());
    if (attr == INVALID_FILE_ATTRIBUTES || !(attr & FILE_ATTRIBUTE_DIRECTORY) || (attr & FILE_ATTRIBUTE_REPARSE_POINT)) throw std::runtime_error("Invalid capture subdirectory");
}
void CollectExternal(const std::wstring& root, const cd::Header& h, const std::set<std::wstring>& oldNames) {
    const std::wstring crashes = BoundedPath(h.crashDirectory);
    uint64_t externalUsed = 0;
    unsigned copies = 0;
    if (!crashes.empty()) {
        const std::wstring pidPart = L"-pid" + std::to_wstring(h.processId) + L"-tid";
        auto names = Names(crashes);
        for (const auto& name : names) {
            if (copies >= 64) { Note(root, "External report file count capped at 64"); break; }
            if (oldNames.count(name) || name.rfind(L"SHCDESE-crash-", 0) != 0 || name.find(pidPart) == std::wstring::npos) continue;
            const size_t dot = name.find_last_of(L'.');
            if (dot == std::wstring::npos || (name.substr(dot) != L".log" && name.substr(dot) != L".dmp")) continue;
            WIN32_FILE_ATTRIBUTE_DATA data{};
            if (!GetFileAttributesExW(Long(Join(crashes, name)).c_str(), GetFileExInfoStandard, &data) || Filetime(data.ftLastWriteTime) < h.startedUtc) continue;
            if (CopyBounded(root, Join(crashes, name), Join(Join(root, L"extender"), name), externalUsed)) ++copies;
        }
    }
    const std::wstring log = BoundedPath(h.bepLog);
    const size_t slash = log.find_last_of(L"\\/");
    if (slash != std::wstring::npos) {
        const std::wstring breadcrumbs = Join(log.substr(0, slash), L"SerpsModsDiagnostics");
        // .NET ticks and FILETIME both count 100 ns; epoch offset is contractual.
        const std::wstring identity = L"-pid" + std::to_wstring(h.processId) + L"-start" + std::to_wstring(h.processCreatedUtc + 504911232000000000ULL) + L"-";
        unsigned count = 0;
        for (const auto& name : Names(breadcrumbs)) {
            if (count >= 32) { Note(root, "Breadcrumb file count capped at 32"); break; }
            if (name.find(identity) == std::wstring::npos || name.size() < 4 || name.substr(name.size() - 4) != L".txt") continue;
            if (CopyBounded(root, Join(breadcrumbs, name), Join(Join(root, L"breadcrumbs"), name), externalUsed)) ++count;
        }
        Note(root, "Collected breadcrumb files with exact PID/process-start identity: " + std::to_string(count));
    }
    Note(root, "External report files collected: " + std::to_string(copies));
}

template<class T> bool StableRecord(const T& source, T& result) {
    const LONG64 seq = source.sequence;
    if (seq <= 0 || source.commit != seq) return false;
    MemoryBarrier(); memcpy(&result, &source, sizeof(T)); MemoryBarrier();
    return source.sequence == seq && source.commit == seq && result.sequence == seq && result.commit == seq;
}
struct Journal {
    Handle output;
    uint64_t cursor = 0, missing = 0, records = 0, bytes = 24, limit;
    uint64_t priorityBytes = 0, genericBytes = 0, skippedPriority = 0, skippedGeneric = 0;
    bool mapPriorities = false, priorityCapped = false, genericCapped = false;
    bool capped = false, failed = false;
    Journal(const std::wstring& path, const char (&magic)[9], uint32_t width, uint64_t cap, bool prioritizeMaps = false)
        : output(CreateFileW(Long(path).c_str(), GENERIC_WRITE, FILE_SHARE_READ, nullptr, CREATE_NEW, FILE_FLAG_OPEN_REPARSE_POINT, nullptr)), limit(cap), mapPriorities(prioritizeMaps) {
        if (!output.valid() || !Regular(output)) throw std::runtime_error("Cannot create record journal");
        if (mapPriorities && (width != sizeof(cd::CodeMap) || cap != 40 * MiB)) throw std::runtime_error("Invalid code-map journal priority budget");
        WriteAll(output, magic, 8); U32(output, mapPriorities ? 2u : cd::Version); U32(output, mapPriorities ? 0u : width); const uint64_t reserved = 0; WriteAll(output, &reserved, sizeof(reserved));
    }
    template<class T, size_t N> void Poll(const std::wstring& root, const T (&source)[N], LONG64 sequence) {
        if (failed || capped || sequence <= 0) return;
        uint64_t end = static_cast<uint64_t>(sequence);
        if (end > cursor + N) { missing += end - cursor - N; cursor = end - N; }
        try {
            const uint64_t used = Used(root);
            uint64_t room = used + Reserve >= SessionLimit ? 0 : SessionLimit - used - Reserve;
            while (cursor < end) {
                const uint64_t wanted = cursor + 1;
                T copy{}; const auto& slot = source[(wanted - 1) % N];
                if (!StableRecord(slot, copy) || static_cast<uint64_t>(copy.sequence) != wanted) {
                    if (slot.sequence > static_cast<LONG64>(wanted)) { ++missing; ++cursor; continue; }
                    break; // Writer still owns this slot; retry on the next poll.
                }
                bool priority = false;
                uint16_t lengths[4]{};
                uint64_t recordBytes = sizeof(copy);
                if constexpr (std::is_same_v<T, cd::CodeMap>) {
                    if (mapPriorities) {
                        lengths[0] = static_cast<uint16_t>(BoundedText(copy.name, sizeof(copy.name)).size());
                        lengths[1] = static_cast<uint16_t>(BoundedText(copy.image, sizeof(copy.image)).size());
                        lengths[2] = static_cast<uint16_t>(BoundedText(copy.sha256, sizeof(copy.sha256)).size());
                        recordBytes = 64ull + lengths[0] + lengths[1] + lengths[2];
                    }
                }
                if (mapPriorities) {
                    priority = copy.kind == cd::Image || copy.kind == cd::Jit || copy.kind == cd::MethodFreed || copy.kind == cd::ChunkDestroyed;
                    uint64_t& bucket = priority ? priorityBytes : genericBytes;
                    const uint64_t bucketLimit = priority ? 32 * MiB : 8 * MiB;
                    if (bucket + recordBytes > bucketLimit) {
                        bool& noted = priority ? priorityCapped : genericCapped;
                        if (!noted) Note(root, priority ? "High-priority code-map bucket reached 32 MiB; further names/lifetime records are omitted" :
                            "Generic code-map bucket reached 8 MiB; further generic buffers/chunks are omitted while names/lifetime records retain their 32 MiB reservation");
                        noted = true;
                        if (priority) ++skippedPriority; else ++skippedGeneric;
                        ++cursor;
                        continue;
                    }
                }
                if (bytes + recordBytes > limit || room < recordBytes) { capped = true; Note(root, "Record journal budget reached"); break; }
                if constexpr (std::is_same_v<T, cd::CodeMap>) {
                    if (mapPriorities) {
                        static_assert(offsetof(cd::CodeMap, name) == 56, "Compact map numeric prefix ABI");
                        WriteAll(output, &copy, 56); WriteAll(output, lengths, sizeof(lengths));
                        WriteAll(output, copy.name, lengths[0]); WriteAll(output, copy.image, lengths[1]); WriteAll(output, copy.sha256, lengths[2]);
                    } else WriteAll(output, &copy, sizeof(copy));
                } else WriteAll(output, &copy, sizeof(copy));
                bytes += recordBytes; ++records; ++cursor;
                room -= recordBytes;
                if (mapPriorities) { if (priority) priorityBytes += recordBytes; else genericBytes += recordBytes; }
            }
        } catch (const std::exception& error) { failed = true; Note(root, "Record journal write failed: " + std::string(error.what())); }
    }
    void Flush() { if (output.valid() && !FlushFileBuffers(output)) failed = true; }
};

std::string Summary(cd::Shared* shared, DWORD exitCode, bool observedExit, const Tail& bep, const Tail& player, const Journal& maps, const Journal& trail) {
    std::ostringstream out;
    out << "CrashDiagnostics session report\r\nGenerated UTC: " << TimeNow() << "\r\n"
        << "Protocol: " << cd::Version << "; process ID: " << shared->header.processId << "; creation FILETIME: " << shared->header.processCreatedUtc << "\r\n"
        << "Process exit observed: " << (observedExit ? "yes" : "no") << "; exit code: 0x" << std::hex << exitCode << std::dec << "\r\n"
        << "Clean shutdown marker: " << (shared->header.cleanShutdown ? "present" : "absent") << "\r\n"
        << "Exception sequence: " << shared->header.captureSequence << "; dropped: " << shared->header.captureDropped << "; retained ring slots: " << cd::CaptureCount << "\r\n"
        << "Code-map sequence: " << shared->header.mapSequence << "; map drops: " << shared->header.mapDropped << "; Mono state: " << shared->header.monoState << "; Mono errors: " << shared->header.monoErrors << "\r\n"
        << "Bootstrap complete: " << shared->header.bootstrapDone << "; lookup calls: " << shared->header.bootstrapCalls << "\r\n"
        << "Trail sequence: " << shared->header.trailSequence << "; trail drops: " << shared->header.trailDropped << "\r\n"
        << "Code-map journal records: " << maps.records << "; sequence gaps: " << maps.missing << "; capped: " << maps.capped << "; failed: " << maps.failed << "\r\n"
        << "Code-map priority bucket bytes: " << maps.priorityBytes << "; budget: " << 32 * MiB << "; policy omissions: " << maps.skippedPriority << "; capped: " << maps.priorityCapped << "\r\n"
        << "Code-map generic bucket bytes: " << maps.genericBytes << "; budget: " << 8 * MiB << "; policy omissions: " << maps.skippedGeneric << "; capped: " << maps.genericCapped << "\r\n"
        << "Trail journal records: " << trail.records << "; sequence gaps: " << trail.missing << "; capped: " << trail.capped << "; failed: " << trail.failed << "\r\n"
        << "BepInEx log bytes copied: " << bep.written << "; rotations: " << bep.rotations << "; read misses: " << bep.missed << "; capped: " << bep.capped << "\r\n"
        << "Player log bytes copied: " << player.written << "; rotations: " << player.rotations << "; read misses: " << player.missed << "; capped: " << player.capped << "\r\n\r\n";
    out << "Captured BepInEx segment rotations: " << bep.outputRotations << "; Player segment rotations: " << player.outputRotations << " (latest two 6 MiB segments per log retained)\r\n";
    std::vector<cd::CaptureHeader> captures;
    for (const auto& source : shared->captures) { cd::CaptureHeader copy{}; if (StableRecord(source.header, copy)) captures.push_back(copy); }
    std::sort(captures.begin(), captures.end(), [](const cd::CaptureHeader& a, const cd::CaptureHeader& b) { return a.sequence < b.sequence; });
    for (const auto& capture : captures) {
        bool progress = false;
        for (const auto& source : shared->trail) {
            cd::TrailRecord record{};
            if (StableRecord(source, record) && record.threadId == capture.threadId && record.qpc > capture.qpc) { progress = true; break; }
        }
        out << "Event " << capture.sequence << ": exception_observed; thread=" << capture.threadId << "; code=0x" << std::hex << capture.exception.ExceptionCode
            << "; address=0x" << reinterpret_cast<uint64_t>(capture.exception.ExceptionAddress) << std::dec
            << "; ranges=" << capture.rangeCount << "; payload bytes=" << capture.payloadBytes << "; truncated=" << capture.truncated << "\r\n";
        if (progress) out << "  progress_after_event: later recorded activity on the same OS thread (not a proof of the handling mechanism).\r\n";
        if (observedExit) out << "  process_exit_after_event: exit was observed after this record; temporal association is not a root-cause proof.\r\n";
    }
    out << "\r\nInterpretation limits:\r\n"
        << "- Native exception observations may be first-chance and handled by Mono/Unity/another handler.\r\n"
        << "- No clean-shutdown marker alone is not proof of a crash.\r\n"
        << "- Unknown process termination is reported as termination_unknown; no terminal fault is invented.\r\n"
        << "- Captured memory from other threads is not a coherent full-process snapshot.\r\n"
        << "- Ring overwrites and capture budgets may omit earlier events, logs, code mappings or data.\r\n"
        << "- The last logged operation and a hook owner do not establish who created an invalid state.\r\n"
        << "- Use the developer analyzer for instruction bytes, validated mappings and marked stack candidates.\r\n";
    if (!observedExit) out << "termination_unknown\r\n";
    return out.str();
}

int Watch(const std::wstring& root) {
    RejectReparseAncestors(root);
    Handle file(CreateFileW(Long(Join(root, L"emergency.bin")).c_str(), GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
    if (!file.valid() || !Regular(file) || FileSize(file) != sizeof(cd::Shared)) throw std::runtime_error("Invalid emergency mapping file");
    Handle mapping(CreateFileMappingW(file, nullptr, PAGE_READWRITE, 0, 0, nullptr));
    if (!mapping.valid()) throw std::runtime_error("Cannot open emergency mapping");
    auto shared = static_cast<cd::Shared*>(MapViewOfFile(mapping, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, sizeof(cd::Shared)));
    if (!shared) throw std::runtime_error("Cannot map emergency data");
    struct Unmap { void* p; ~Unmap() { UnmapViewOfFile(p); } } unmap{shared};
    if (shared->header.magic != cd::Magic || shared->header.version != cd::Version || shared->header.headerBytes != sizeof(cd::Header) || shared->header.fileBytes != sizeof(cd::Shared) || shared->header.architecture != 64) throw std::runtime_error("Unsupported emergency protocol");
    auto& header = shared->header;
    try {
        Handle process(OpenProcess(SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION, FALSE, header.processId));
        if (!process.valid()) throw std::runtime_error("Cannot open game process");
        FILETIME created{}, exited{}, kernel{}, user{};
        if (!GetProcessTimes(process, &created, &exited, &kernel, &user) || Filetime(created) != header.processCreatedUtc) throw std::runtime_error("Game process creation identity mismatch");
        if (InterlockedCompareExchange(&header.collectorPid, static_cast<LONG>(GetCurrentProcessId()), 0) != 0) throw std::runtime_error("Session already has a collector");
        Subdir(root, L"logs"); Subdir(root, L"extender"); Subdir(root, L"breadcrumbs");
        Tail bep{BoundedPath(header.bepLog), Join(Join(root, L"logs"), L"LogOutput.log")};
        Tail player{BoundedPath(header.playerLog), Join(Join(root, L"logs"), L"Player.log")};
        Journal maps(Join(root, L"code-maps.bin"), "CDMAPS2\0", sizeof(cd::CodeMap), 40 * MiB, true);
        Journal trail(Join(root, L"trail.bin"), "CDTRAIL1", sizeof(cd::TrailRecord), 8 * MiB);
        auto oldNames = Names(BoundedPath(header.crashDirectory));
        Note(root, "Collector verified process creation identity; native exception path uses no DbgHelp");
        InterlockedExchange(&header.collectorReady, 1);
        DWORD result = WAIT_TIMEOUT;
        bool exitedObserved = false;
        for (;;) {
            maps.Poll(root, shared->maps, header.mapSequence);
            trail.Poll(root, shared->trail, header.trailSequence);
            try { bep.Poll(root); player.Poll(root); }
            catch (const std::exception& e) { Note(root, "Log capture error: " + std::string(e.what())); }
            result = WaitForSingleObject(process, 250);
            if (result == WAIT_OBJECT_0) { exitedObserved = true; break; }
            if (result == WAIT_FAILED) break;
            if (header.collectorError) { Note(root, "Native initialization/runtime error=" + std::to_string(header.collectorError)); break; }
        }
        DWORD exitCode = 0;
        if (exitedObserved && !GetExitCodeProcess(process, &exitCode)) exitedObserved = false;
        // No sleep on an owned game thread: this process remains alive after the game closes.
        if (exitedObserved) {
            maps.Poll(root, shared->maps, header.mapSequence);
            trail.Poll(root, shared->trail, header.trailSequence);
            uint64_t exitUtc = 0;
            if (GetProcessTimes(process, &created, &exited, &kernel, &user)) exitUtc = Filetime(exited);
            try { bep.Poll(root, exitUtc); player.Poll(root, exitUtc); CollectExternal(root, header, oldNames); }
            catch (const std::exception& e) { Note(root, "Final collection incomplete: " + std::string(e.what())); }
        }
        maps.Flush(); trail.Flush();
        if (!FlushViewOfFile(shared, sizeof(cd::Shared))) Note(root, "Shared data flush failed");
        PutText(Join(root, L"report.txt"), Summary(shared, exitCode, exitedObserved, bep, player, maps, trail));
        PutText(Join(root, L"collector-status.txt"), "schemaVersion=1\r\nfinishedUtc=" + TimeNow() + "\r\nprocessExitObserved=" + (exitedObserved ? std::string("true") : std::string("false")) + "\r\n");
        return exitedObserved ? 0 : 3;
    } catch (...) {
        InterlockedExchange(&header.collectorError, static_cast<LONG>(GetLastError() ? GetLastError() : ERROR_GEN_FAILURE));
        throw;
    }
}
}

int wmain(int argc, wchar_t** argv) {
    try {
        if (argc == 3 && wcscmp(argv[1], L"--watch") == 0) return Watch(Full(argv[2]));
        if (argc == 3 && wcscmp(argv[1], L"--export") == 0) { Export(Full(argv[2])); return 0; }
        fwprintf(stderr, L"CrashDiagnostics collector\n  --watch SESSION (started by plugin)\n  --export SESSION (creates SESSION.zip; game must be closed)\n");
        return 2;
    } catch (const std::exception& error) {
        fprintf(stderr, "CrashDiagnostics collector: %s (Win32=%lu)\n", error.what(), GetLastError());
        return 1;
    }
}
