"""Offline, bounded CrashDiagnostics/SHCDESE reader. Never attaches to a process.

Python 3 is needed only on the developer's machine. Capstone is optional; an
unavailable disassembler does not turn unknown code into an inferred method.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import struct
import sys
import datetime
from dataclasses import dataclass

MAX_FILE = 128 * 1024 * 1024
MAX_STRING = 65536
MAX_RECORDS = 100000

# ABI v1 is Windows x64 /Zp8. TestHost --layout verifies every fixed offset.
LAYOUT = {"shared": 23359648, "header": 28832, "captureHeader": 9408,
          "capture": 1048576, "map": 768, "trail": 64,
          "trailOffset": 28832, "mapsOffset": 290976, "capturesOffset": 6582432,
          "exceptionOffset": 80, "contextOffset": 240,
          "rangesOffset": 5568, "contextTextOffset": 1472}


class FormatError(ValueError):
    pass


@dataclass
class Blob:
    data: bytes

    def take(self, start: int, size: int) -> bytes:
        if start < 0 or size < 0 or start > len(self.data) or size > len(self.data) - start:
            raise FormatError(f"Range outside file: offset={start}, size={size}")
        return self.data[start:start + size]

    def unpack(self, fmt: str, start: int):
        return struct.unpack("<" + fmt, self.take(start, struct.calcsize("<" + fmt)))

    def array(self, start: int, count: int, width: int):
        if count > MAX_RECORDS:
            raise FormatError("Too many binary records")
        self.take(start, count * width)
        return range(count)

    def md_string(self, offset: int) -> str:
        length, = self.unpack("I", offset)
        if length > MAX_STRING or length % 2:
            raise FormatError("Invalid minidump string size")
        try:
            return self.take(offset + 4, length).decode("utf-16-le")
        except UnicodeError as error:
            raise FormatError("Invalid minidump Unicode") from error


def load(path: pathlib.Path) -> Blob:
    if path.is_symlink() or not path.is_file():
        raise FormatError("Input must be a regular file, not a symlink")
    if path.stat().st_size > MAX_FILE:
        raise FormatError("Input exceeds 128 MiB limit")
    with path.open("rb") as stream:
        data = stream.read(MAX_FILE + 1)
    if len(data) > MAX_FILE:
        raise FormatError("Input grew beyond limit while reading")
    return Blob(data)


def disassemble(data: bytes, address: int) -> dict:
    try:
        import capstone
    except ImportError:
        return {"available": False, "reason": "Capstone not installed", "bytes": data[:64].hex()}
    decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    instructions = []
    for item in decoder.disasm(data[:96], address, count=12):
        instructions.append({"address": hex(item.address), "bytes": item.bytes.hex(),
                             "mnemonic": item.mnemonic, "operands": item.op_str})
    return {"available": True, "instructions": instructions}


def minidump(blob: Blob) -> dict:
    magic, version, count, directory, _, timestamp, flags = blob.unpack("4sIIIIIQ", 0)
    if magic != b"MDMP" or (version & 0xffff) != 0xa793:
        raise FormatError("Not a supported Windows minidump")
    if count > 4096:
        raise FormatError("Too many minidump streams")
    streams = {}
    for i in blob.array(directory, count, 12):
        kind, size, offset = blob.unpack("III", directory + 12 * i)
        blob.take(offset, size)
        if kind in streams:
            raise FormatError("Duplicate minidump stream kind")
        streams[kind] = (offset, size)
    modules = []
    for kind in (4, 0xfff0):  # Wine's ELF module list has the MINIDUMP_MODULE layout.
        if kind not in streams:
            continue
        offset, size = streams[kind]
        number, = blob.unpack("I", offset)
        if number > 4096 or size < 4 + 108 * number:
            raise FormatError("Invalid module list size")
        for i in blob.array(offset + 4, number, 108):
            base, length, checksum, stamp, name = blob.unpack("QIIII", offset + 4 + i * 108)
            modules.append({"base": base, "size": length, "name": blob.md_string(name),
                            "timestamp": stamp, "checksum": checksum,
                            "source": "PE" if kind == 4 else "Wine ELF"})
    memory = []
    if 5 in streams:
        offset, size = streams[5]
        number, = blob.unpack("I", offset)
        if size < 4 + 16 * number:
            raise FormatError("Invalid memory list size")
        for i in blob.array(offset + 4, number, 16):
            base, length, data_offset = blob.unpack("QII", offset + 4 + i * 16)
            blob.take(data_offset, length)
            memory.append((base, length, data_offset))
    report = {"kind": "minidump", "timestampUnix": timestamp, "flags": hex(flags),
              "modules": modules, "memoryRegions": len(memory), "limitations": [
                  "A minidump exception stream alone does not prove a fatal/unhandled exception.",
                  "Absent memory or module records do not prove the memory/module was absent at runtime."]}
    if 6 not in streams:
        report["limitations"].append("No exception stream")
        return report
    offset, size = streams[6]
    if size < 168:
        raise FormatError("Short exception stream")
    thread, = blob.unpack("I", offset)
    code, ex_flags = blob.unpack("II", offset + 8)
    address, = blob.unpack("Q", offset + 24)
    parameter_count, = blob.unpack("I", offset + 32)
    if parameter_count > 15:
        raise FormatError("Invalid exception parameter count")
    parameters = list(blob.unpack("Q" * parameter_count, offset + 40))
    context_size, context_offset = blob.unpack("II", offset + 160)
    context = blob.take(context_offset, context_size)
    exception = {"threadId": thread, "code": hex(code), "flags": hex(ex_flags), "address": hex(address),
                 "parameters": [hex(x) for x in parameters]}
    if code in (0xc0000005, 0xc0000006) and len(parameters) >= 2:
        exception["operation"] = {0: "read", 1: "write", 8: "execute"}.get(parameters[0], "unknown")
        exception["accessAddress"] = hex(parameters[1])
    report["exception"] = exception
    if len(context) >= 256:
        names = ("RAX", "RCX", "RDX", "RBX", "RSP", "RBP", "RSI", "RDI", "R8", "R9", "R10", "R11", "R12", "R13", "R14", "R15", "RIP")
        # Only AMD64 contexts may use these offsets. Other architectures remain raw.
        context_flags, = struct.unpack_from("<I", context, 48)
        if context_flags & 0x100000:
            exception["registers"] = {name: hex(struct.unpack_from("<Q", context, 120 + i * 8)[0])
                                      for i, name in enumerate(names)}
    report["addressMappings"] = [dict(module=m["name"], rva=hex(address - m["base"]), source=m["source"])
                                  for m in modules if m["base"] <= address < m["base"] + m["size"]]
    code_bytes = None
    for base, length, data_offset in memory:
        if base <= address < base + length:
            code_bytes = blob.take(data_offset + address - base, min(96, base + length - address))
            break
    report["faultCodeCaptured"] = code_bytes is not None
    if code_bytes is not None:
        report["faultCode"] = disassemble(code_bytes, address)
    else:
        report["limitations"].append("Instruction bytes at the exception address were not captured")
    return report


def crash_log(blob: Blob) -> dict:
    text = blob.data.decode("utf-8-sig", errors="replace")
    reports = []
    for part in text.split("SHCDE-SE native crash report")[1:]:
        fault = re.search(r"Exception:\s*(\S+)\s*\((0x[0-9A-Fa-f]+)\)\s+at\s+(0x[0-9A-Fa-f]+)", part)
        if not fault:
            continue
        process = re.search(r"Process ID:\s*(\d+)\s+Thread ID:\s*(\d+)", part)
        access = re.search(r"Access:\s*(\w+)\s+at virtual address\s+(0x[0-9A-Fa-f]+)", part)
        frames = re.findall(r"^\s*#(\d+)\s+(0x[0-9A-Fa-f]+)([^\r\n]*)", part, re.MULTILINE)
        record = {"name": fault[1], "code": hex(int(fault[2], 16)), "address": hex(int(fault[3], 16)),
                  "frames": [{"index": int(n), "address": hex(int(a, 16)), "description": d.strip()}
                             for n, a, d in frames[:256]]}
        if process:
            record.update(processId=int(process[1]), threadId=int(process[2]))
        if access:
            record.update(operation=access[1], accessAddress=hex(int(access[2], 16)))
        reports.append(record)
        if len(reports) > 1024:
            raise FormatError("Too many crash reports")
    return {"kind": "crash-log", "events": reports, "limitations": [
        "First-chance native reports are not proof of a fatal exception.",
        "Absolute addresses from different processes must not be treated as stable function identities.",
        "An ordinary BepInEx log without a crash block supplies context only."]}


def analyze_file(path: pathlib.Path) -> dict:
    blob = load(path)
    if blob.data.startswith(b"MDMP"):
        result = minidump(blob)
    elif path.suffix.lower() in (".log", ".txt"):
        result = crash_log(blob)
    elif path.name == "emergency.bin":
        maps_file, trail_file = path.parent / "code-maps.bin", path.parent / "trail.bin"
        optional_warnings = []
        def optional_journal(journal_path, kind):
            try:
                return journal(load(journal_path), kind) if journal_path.exists() else []
            except (FormatError, OSError, struct.error) as error:
                optional_warnings.append(f"Optional {journal_path.name} rejected ({error}); using committed emergency-ring records only.")
                return []
        maps, trail = optional_journal(maps_file, "maps"), optional_journal(trail_file, "trail")
        try:
            result = emergency(blob, maps, trail)
        except FormatError as error:
            if not maps and not trail:
                raise
            # A syntactically valid optional archive can still contradict its
            # raw ring. Validate the emergency independently before falling back.
            result = emergency(blob)
            optional_warnings.append(f"Optional journal/ring inconsistency rejected ({error}); using committed emergency-ring records only.")
        result["limitations"].extend(optional_warnings)
        enrich_session(path.parent, result)
    else:
        raise FormatError("Unrecognized diagnostic input")
    result["file"] = str(path)
    result["fileSha256"] = hashlib.sha256(blob.data).hexdigest()
    return result


def small_json(path: pathlib.Path) -> dict | None:
    if not path.exists():
        return None
    blob = load(path)
    if len(blob.data) > 512 * 1024:
        raise FormatError("Metadata file exceeds 512 KiB")
    try:
        value = json.loads(blob.data.decode("utf-8-sig"))
    except (ValueError, UnicodeError, RecursionError) as error:
        raise FormatError("Malformed session metadata") from error
    if not isinstance(value, dict) or value.get("schemaVersion") != 1:
        raise FormatError("Unsupported session metadata schema")
    return value


def filetime_from_iso(value) -> int | None:
    if not isinstance(value, str):
        return None
    try:
        date = datetime.datetime.fromisoformat(value.replace("Z", "+00:00"))
        if date.tzinfo is None:
            return None
        epoch = datetime.datetime(1601, 1, 1, tzinfo=datetime.timezone.utc)
        delta = date.astimezone(datetime.timezone.utc) - epoch
        return (delta.days * 86400 + delta.seconds) * 10000000 + delta.microseconds * 10
    except (ValueError, OverflowError):
        return None


def unsigned_address(value) -> int:
    if isinstance(value, int) and not isinstance(value, bool):
        result = value
    elif isinstance(value, str) and re.fullmatch(r"0x[0-9a-fA-F]{1,16}", value):
        result = int(value, 16)
    else:
        raise FormatError("Invalid metadata address")
    if not 0 <= result < (1 << 64):
        raise FormatError("Metadata address outside x64 range")
    return result


def enrich_session(directory: pathlib.Path, result: dict):
    session = small_json(directory / "session.json")
    inventory = small_json(directory / "inventory.json")
    image = small_json(directory / "images.json")
    sites = small_json(directory / "native-sites.json")
    observers = small_json(directory / "observer-status.json")
    if session is not None and session.get("processId") != result["processId"]:
        raise FormatError("Session manifest and emergency process identities differ")
    result["sessionMetadata"] = {"session": session, "inventory": inventory, "images": image,
                                 "nativeSites": sites, "observers": observers}
    modules = []
    if inventory:
        rows = inventory.get("modules", [])
        if not isinstance(rows, list) or len(rows) > 512:
            raise FormatError("Invalid module inventory")
        for row in rows:
            if not isinstance(row, dict) or "baseAddress" not in row or "imageSize" not in row:
                continue
            start, size = unsigned_address(row["baseAddress"]), unsigned_address(row["imageSize"])
            if start + size > (1 << 64):
                raise FormatError("Module inventory address overflow")
            modules.append({"base": start, "size": size, "name": str(row.get("name", "unknown"))[:512],
                            "identity": row.get("file"), "capturedUtc": inventory.get("capturedUtc"), "source": "module-inventory"})
    native_base = None
    if image and "baseAddress" in image and "imageSize" in image:
        native_base = unsigned_address(image["baseAddress"])
        modules.append({"base": native_base, "size": unsigned_address(image["imageSize"]),
                        "name": str(image.get("name", "CrusaderDE.dll"))[:512], "identity": image.get("identity"),
                        "capturedUtc": image.get("capturedUtc"), "source": "explicit-image-inventory"})
    stubs = []
    log_files = [directory / "logs" / "LogOutput.log.previous.log", directory / "logs" / "LogOutput.log"]
    for log in log_files:
        if not log.exists():
            continue
        text = load(log).data.decode("utf-8-sig", errors="replace")
        for match in re.finditer(r'\["([^"\r\n]{1,256})"\]\s+compiled stub at\s+(0x[0-9A-Fa-f]+),\s+size:\s*(\d+)\s+bytes(?:\s*\(target:\s*(0x[0-9A-Fa-f]+)\))?', text):
            start, size = int(match[2], 16), int(match[3])
            if not (0 < size <= 1024 * 1024 and 0 < start < (1 << 64) - size):
                continue
            stubs.append({"base": start, "size": size, "name": match[1], "target": match[4],
                          "source": log.name, "timeEvidence": "log-reported allocation; no QPC/generation proof"})
            if len(stubs) >= 8192:
                break
    registrations = sites.get("registrations", []) if sites else []
    if not isinstance(registrations, list) or len(registrations) > 1024:
        raise FormatError("Invalid native-site inventory")
    for event in result["events"]:
        address = int(event["exception"]["address"], 16)
        historical = []
        for module in modules:
            if module["base"] <= address < module["base"] + module["size"]:
                captured = filetime_from_iso(module["capturedUtc"])
                historical.append({"name": module["name"], "offset": hex(address - module["base"]),
                                   "source": module["source"], "identity": module["identity"], "capturedUtc": module["capturedUtc"],
                                   "confidence": "inventory membership, not function/causal proof",
                                   "capturedBeforeEvent": None if captured is None else captured <= event["utcFiletime"]})
        for stub in stubs:
            if stub["base"] <= address < stub["base"] + stub["size"]:
                historical.append({"name": stub["name"], "offset": hex(address - stub["base"]),
                                   "source": "log-reported-stub", "reportedTarget": stub["target"],
                                   "confidence": "historical allocation range only; address reuse not excluded"})
        event["historicalAddressMappings"] = historical
        event["relatedNativeSites"] = []
        if native_base is not None and address >= native_base:
            rva = address - native_base
            for site in registrations:
                if not isinstance(site, dict):
                    raise FormatError("Invalid native-site entry")
                start, size = site.get("rva"), site.get("length")
                if isinstance(start, int) and isinstance(size, int) and start <= rva < start + size:
                    event["relatedNativeSites"].append({"ownerGuid": site.get("ownerGuid"), "name": site.get("name"),
                                                        "rva": hex(start), "length": size, "mode": site.get("mode"),
                                                        "capturedUtc": sites.get("capturedUtc"),
                                                        "confidence": "cooperative registration; does not prove installed hook or fault ownership"})
    result["limitations"].append("Module inventories and log-reported stubs are historical evidence; they do not prove lifetime, current bytes, or causation.")


def c_text(data: bytes) -> str:
    return data.split(b"\0", 1)[0].decode("utf-8", errors="replace")


def code_map(blob: Blob, offset: int) -> dict | None:
    sequence, commit, qpc, address, length, method, kind, truncated = blob.unpack("QQQQQQII", offset)
    if not sequence or sequence != commit:
        return None
    if length > (1 << 48) or address + length > (1 << 64):
        raise FormatError("Invalid code-map address span")
    return {"sequence": sequence, "qpc": qpc, "address": address, "length": length,
            "method": method, "kind": kind, "truncated": truncated,
            "name": c_text(blob.take(offset + 56, 512)),
            "image": c_text(blob.take(offset + 568, 128)),
            "sha256": c_text(blob.take(offset + 696, 72))}


def trail_record(blob: Blob, offset: int) -> dict | None:
    seq, commit, qpc, utc, thread, kind, a, b, c = blob.unpack("QQQQIIqqq", offset)
    if not seq or seq != commit:
        return None
    return {"sequence": seq, "qpc": qpc, "utcFiletime": utc, "threadId": thread,
            "kind": kind, "value1": a, "value2": b, "value3": c}


def journal(blob: Blob, kind: str) -> list[dict]:
    magic, version, width, reserved = blob.unpack("8sIIQ", 0)
    if kind == "maps" and magic == b"CDMAPS2\0":
        if version != 2 or width != 0 or reserved:
            raise FormatError("Unsupported compact map journal header")
        result, offset, previous, previous_qpc = [], 24, 0, 0
        while offset < len(blob.data):
            if len(result) >= (40 * 1024 * 1024) // 64:
                raise FormatError("Too many compact map records")
            sequence, commit, qpc, address, length, method, record_kind, truncated, name_len, image_len, sha_len, unused = blob.unpack("QQQQQQIIHHHH", offset)
            if unused or name_len > 512 or image_len > 128 or sha_len > 72:
                raise FormatError("Invalid compact map string lengths")
            if not sequence or sequence != commit or sequence <= previous or not qpc or qpc < previous_qpc:
                raise FormatError("Uncommitted or unordered compact map sequence/QPC")
            if length > (1 << 48) or address + length > (1 << 64):
                raise FormatError("Invalid compact map address span")
            cursor = offset + 64
            strings = []
            try:
                for size in (name_len, image_len, sha_len):
                    text = blob.take(cursor, size)
                    if b"\0" in text:
                        raise FormatError("NUL inside compact map string")
                    strings.append(text.decode("utf-8", errors="replace" if truncated & 1 else "strict"))
                    cursor += size
            except UnicodeError as error:
                raise FormatError("Invalid UTF-8 in compact map string") from error
            result.append({"sequence": sequence, "qpc": qpc, "address": address, "length": length,
                           "method": method, "kind": record_kind, "truncated": truncated,
                           "name": strings[0], "image": strings[1], "sha256": strings[2]})
            previous, previous_qpc, offset = sequence, qpc, cursor
        return result
    expected = (b"CDMAPS1\0", 768) if kind == "maps" else (b"CDTRAIL1", 64)
    if (magic, width) != expected or version != 1 or reserved:
        raise FormatError("Unsupported journal header")
    if (len(blob.data) - 24) % width:
        raise FormatError("Journal ends with a partial record")
    count = (len(blob.data) - 24) // width
    if count > MAX_RECORDS * 2:
        raise FormatError("Too many journal records")
    parser = code_map if kind == "maps" else trail_record
    result, previous = [], 0
    for i in range(count):
        record = parser(blob, 24 + i * width)
        if record is None or record["sequence"] <= previous:
            raise FormatError("Uncommitted or unordered journal record")
        previous = record["sequence"]
        result.append(record)
    return result


def merged_records(archived: list[dict], current: list[dict]) -> list[dict]:
    result = {r["sequence"]: r for r in archived}
    for record in current:
        seq = record["sequence"]
        if seq in result and result[seq] != record:
            raise FormatError("Contradicting ring/journal record identity")
        result[seq] = record
    return sorted(result.values(), key=lambda r: r["sequence"])


def valid_maps(sequence: int, maps: list[dict], qpc: int = (1 << 64) - 1) -> list[dict]:
    relevant = [m for m in maps if m["sequence"] <= sequence and m["qpc"] <= qpc]
    freed = {}
    destroyed = []
    for record in relevant:
        kind = record["kind"]
        if kind == 3:
            freed[record["method"]] = record["sequence"]
        elif kind == 6:
            if record["length"]:
                # Native chunk tracking supplies the span even when the generic
                # ChunkCreated record was omitted by the archive priority policy.
                destroyed.append((record["sequence"], record["address"], record["length"]))
            else:
                # Missing native span is not a zero-byte free. Conservatively
                # discard all older generated-code identities; keep native images.
                destroyed.append((record["sequence"], None, None))
    valid = []
    for record in relevant:
        if record["kind"] not in (1, 2, 4, 5) or not record["length"]:
            continue
        if record["method"] and freed.get(record["method"], 0) > record["sequence"]:
            continue
        if record["kind"] != 1 and any(seq > record["sequence"] and
                                       (base is None or (base < record["address"] + record["length"] and record["address"] < base + length))
                                       for seq, base, length in destroyed):
            continue
        valid.append(record)
    return valid


class MapResolver:
    """Static interval tree: stack scans do not repeatedly walk every JIT record."""
    def __init__(self, sequence: int, maps: list[dict], qpc: int = (1 << 64) - 1):
        records = sorted(valid_maps(sequence, maps, qpc), key=lambda m: m["address"])

        def build(lo, hi):
            if lo >= hi:
                return None
            mid = (lo + hi) // 2
            left, right = build(lo, mid), build(mid + 1, hi)
            record = records[mid]
            maximum = max(record["address"] + record["length"], left[1] if left else 0, right[1] if right else 0)
            return record, maximum, left, right

        self.root = build(0, len(records))

    def resolve(self, address: int) -> list[dict]:
        matches = []

        def visit(node):
            if node is None or node[1] <= address:
                return
            record, maximum, left, right = node
            visit(left)
            if record["address"] <= address < record["address"] + record["length"]:
                matches.append({"kind": {1: "registered-image", 2: "named-jit-method", 4: "generic-code-buffer", 5: "jit-allocation-chunk"}[record["kind"]],
                                "name": record["name"], "image": record["image"], "offset": hex(address - record["address"]),
                                "mapSequence": record["sequence"], "sha256": record["sha256"], "nameTruncated": bool(record["truncated"] & 1)})
            if record["address"] <= address:
                visit(right)

        visit(self.root)
        rank = {"named-jit-method": 0, "registered-image": 1, "generic-code-buffer": 2, "jit-allocation-chunk": 3}
        return sorted(matches, key=lambda m: (rank[m["kind"]], -m["mapSequence"]))


def resolve(address: int, sequence: int, maps: list[dict], qpc: int = (1 << 64) - 1) -> list[dict]:
    return MapResolver(sequence, maps, qpc).resolve(address)


def emergency(blob: Blob, archived_maps=None, archived_trail=None) -> dict:
    if len(blob.data) != LAYOUT["shared"]:
        raise FormatError("Wrong emergency file size")
    magic, version, header_bytes, file_bytes, process, architecture = blob.unpack("QIIQII", 0)
    if magic != 0x3147414944434853 or version != 1 or header_bytes != LAYOUT["header"] or file_bytes != len(blob.data) or architecture != 64:
        raise FormatError("Unsupported emergency protocol/layout")
    created, started, started_qpc, frequency = blob.unpack("QQQQ", 32)
    capture_seq, capture_drop, trail_seq, trail_drop, map_seq, map_drop, context_seq = blob.unpack("QQQQQQQ", 64)
    clean, ready, collector_pid, collector_error, mono_state, bootstrap, calls, mono_errors = blob.unpack("IIIIIIII", 120)
    if not frequency:
        raise FormatError("Zero timestamp frequency")
    ring_maps = []
    for i in range(8192):
        record = code_map(blob, LAYOUT["mapsOffset"] + i * 768)
        if record:
            if (record["sequence"] - 1) % 8192 != i or record["sequence"] > map_seq:
                raise FormatError("Code-map sequence does not belong to its ring slot")
            ring_maps.append(record)
    maps = merged_records(archived_maps or [], ring_maps)
    ring_trail = []
    for i in range(4096):
        record = trail_record(blob, LAYOUT["trailOffset"] + i * 64)
        if record:
            if (record["sequence"] - 1) % 4096 != i or record["sequence"] > trail_seq:
                raise FormatError("Trail sequence does not belong to its ring slot")
            ring_trail.append(record)
    trail = merged_records(archived_trail or [], ring_trail)
    events = []
    partial = 0
    for index in range(16):
        slot = LAYOUT["capturesOffset"] + index * LAYOUT["capture"]
        seq, commit, busy, size = blob.unpack("QQII", slot)
        if not seq:
            continue
        if seq != commit or busy:
            partial += 1
            continue
        if size != LAYOUT["captureHeader"] or (seq - 1) % 16 != index or seq > capture_seq:
            raise FormatError("Invalid capture slot identity/header")
        qpc, utc, thread, count, payload_size, truncated = blob.unpack("QQIIII", slot + 24)
        trail_at, maps_at, context_at = blob.unpack("QQQ", slot + 56)
        if count > 96 or payload_size > LAYOUT["capture"] - size or maps_at > map_seq or trail_at > trail_seq:
            raise FormatError("Invalid capture count/payload/sequence")
        ex = slot + LAYOUT["exceptionOffset"]
        code, flags = blob.unpack("II", ex)
        address, = blob.unpack("Q", ex + 16)
        parameters_count, = blob.unpack("I", ex + 24)
        if parameters_count > 15:
            raise FormatError("Invalid exception parameter count")
        params = blob.unpack("Q" * parameters_count, ex + 32)
        exception = {"code": hex(code), "flags": hex(flags), "address": hex(address), "parameters": [hex(x) for x in params]}
        resolver = MapResolver(maps_at, maps, qpc)
        if code in (0xc0000005, 0xc0000006) and len(params) >= 2:
            exception.update(operation={0: "read", 1: "write", 8: "execute"}.get(params[0], "unknown"), accessAddress=hex(params[1]))
        names = ("RAX", "RCX", "RDX", "RBX", "RSP", "RBP", "RSI", "RDI", "R8", "R9", "R10", "R11", "R12", "R13", "R14", "R15", "RIP")
        registers = dict(zip(names, blob.unpack("Q" * 17, slot + LAYOUT["contextOffset"] + 120)))
        event = {"sequence": seq, "qpc": qpc, "utcFiletime": utc, "threadId": thread,
                 "status": ["exception_observed"], "exception": exception,
                 "registers": {name: hex(value) for name, value in registers.items()},
                 "ranges": [], "truncated": truncated,
                 "sessionContext": c_text(blob.take(slot + LAYOUT["contextTextOffset"], 4096)),
                 "addressMappings": resolver.resolve(address),
                 "faultCodeCaptured": False, "stackCandidates": []}
        if any(r["threadId"] == thread and r["qpc"] > qpc for r in trail):
            event["status"].append("progress_after_event")
        raw_ranges = []
        for ri in range(count):
            start, requested, copied, payload_offset, error, kind, protect, state, memory_type = blob.unpack("QIIIIIIII", slot + LAYOUT["rangesOffset"] + ri * 40)
            if copied > requested or payload_offset > payload_size or copied > payload_size - payload_offset or start + copied > (1 << 64):
                raise FormatError("Captured range points outside its committed payload")
            data = blob.take(slot + size + payload_offset, copied)
            event["ranges"].append({"address": hex(start), "requested": requested, "copied": copied, "error": error,
                                    "kind": kind, "protection": hex(protect), "state": hex(state), "memoryType": hex(memory_type)})
            raw_ranges.append((start, data, kind))
            if start <= address < start + copied:
                event["faultCodeCaptured"] = True
                event["faultCode"] = disassemble(data[address - start:address - start + 96], address)
        executable = [(start, len(data)) for start, data, kind in raw_ranges if kind in (1, 4)]
        seen_candidates = set()
        for start, data, kind in raw_ranges:
            if kind != 2:
                continue
            for offset in range(0, min(len(data), 32768) - 7, 8):
                value, = struct.unpack_from("<Q", data, offset)
                mappings = resolver.resolve(value)
                if value in seen_candidates or not (mappings or any(base <= value < base + length for base, length in executable)):
                    continue
                seen_candidates.add(value)
                event["stackCandidates"].append({"stackAddress": hex(start + offset), "value": hex(value), "mappings": mappings,
                                                  "confidence": "candidate-only; not a validated unwind frame"})
                if len(event["stackCandidates"]) >= 128:
                    break
        events.append(event)
    return {"kind": "emergency", "protocolVersion": version, "processId": process,
            "processCreatedFiletime": created, "captureStartedFiletime": started,
            "startedQpc": started_qpc, "qpcFrequency": frequency,
            "cleanShutdownMarker": bool(clean), "collectorReady": bool(ready), "collectorPid": collector_pid,
            "collectorError": collector_error, "monoState": mono_state, "bootstrapDone": bool(bootstrap),
            "bootstrapCalls": calls, "monoErrors": mono_errors,
            "counters": {"captureSequence": capture_seq, "captureDropped": capture_drop,
                         "trailSequence": trail_seq, "trailDropped": trail_drop, "mapSequence": map_seq,
                         "mapRingOverwrites": map_drop, "partialCaptureSlots": partial,
                         "unavailableEarlierCaptureRecords": max(0, capture_seq - len(events)),
                         "unknownSpanChunkDestructions": sum(1 for m in maps if m["kind"] == 6 and not m["length"])},
            "events": sorted(events, key=lambda e: e["sequence"]), "codeMapRecords": len(maps), "trailRecords": len(trail),
            "limitations": ["Native records observe exceptions before final handling; fatality is not inferred.",
                            "Code-buffer/chunk mappings are generic regions, not proof of a method or culprit.",
                            "Bootstrap may miss already compiled short/unmapped methods; unresolved addresses remain unknown.",
                            "Stack candidates are raw values, not a reconstructed call stack.",
                            "Other-thread memory and earlier code maps are not a coherent exception-time snapshot.",
                            "An unknown-span chunk destruction invalidates older generated-code names conservatively.",
                            "Absence of a clean shutdown marker is not proof of a crash."]}


def analyze_path(path: pathlib.Path) -> list[dict]:
    if not path.is_dir():
        return [analyze_file(path)]
    if path.is_symlink():
        raise FormatError("Linked input directories are not accepted")
    files = []
    for child in sorted(path.iterdir()):
        if child.name == "emergency.bin" or (child.name.startswith("SHCDESE-crash-") and child.suffix in (".log", ".dmp")):
            files.append(child)
        if child.name == "extender" and child.is_dir() and not child.is_symlink():
            files.extend(p for p in sorted(child.iterdir()) if p.suffix in (".log", ".dmp"))
    if len(files) > 512:
        raise FormatError("Too many diagnostic inputs")
    return [analyze_file(p) for p in files]


def text_report(results: list[dict]) -> str:
    lines = ["CrashDiagnostics offline analysis", ""]
    for result in results:
        lines.extend([result["file"], "  kind=" + result["kind"]])
        if "exception" in result:
            lines.append("  exception=" + json.dumps(result["exception"], ensure_ascii=False))
        if "events" in result:
            lines.append(f"  observed events={len(result['events'])}")
            for event in result["events"][:32]:
                lines.append("  " + json.dumps(event, ensure_ascii=False))
        if "faultCodeCaptured" in result:
            lines.append("  fault instruction bytes captured=" + str(result["faultCodeCaptured"]))
            lines.append("  mappings=" + json.dumps(result.get("addressMappings", []), ensure_ascii=False))
        if "faultCode" in result:
            lines.append("  fault code=" + json.dumps(result["faultCode"], ensure_ascii=False))
        lines.extend("  limitation: " + item for item in result.get("limitations", []))
        lines.append("")
    lines.append("No root cause is inferred solely from the last log entry, a hook owner, or a raw stack candidate.")
    return "\n".join(lines) + "\n"


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inputs", type=pathlib.Path, nargs="+")
    parser.add_argument("--output", type=pathlib.Path)
    parser.add_argument("--json-output", type=pathlib.Path)
    args = parser.parse_args(argv)
    try:
        results = [item for path in args.inputs for item in analyze_path(path)]
        report = text_report(results)
        if args.output:
            args.output.write_text(report, encoding="utf-8")
        else:
            print(report, end="")
        if args.json_output:
            args.json_output.write_text(json.dumps({"schemaVersion": 1, "results": results}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        return 0
    except (FormatError, OSError, struct.error) as error:
        print("Diagnostic input rejected: " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
