#!/usr/bin/env python3
"""Strict, read-only Stronghold Crusader Definitive Edition .map reference parser."""

from __future__ import annotations

import argparse
import binascii
import hashlib
import json
import struct
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple

SCDE_MAGIC = 0xFFFFFFFE
STANDARD_TAGS = {2036, 3036, 4036}
SPECIAL_TAGS = {1076, 2100, 2108}
MAX_BLOCK = 128 * 1024 * 1024
MAX_FILE = 512 * 1024 * 1024
MAX_TOTAL_UNCOMPRESSED = 1024 * 1024 * 1024
PLACEMENT_IDS = (1003, 1037, 1005, 1045, 1004, 1012, 1026, 1043)
TILE_IDS = {
    1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010,
    1012, 1020, 1021, 1026, 1028, 1029, 1030, 1033, 1036, 1037,
    1043, 1045, 1049, 1103, 1104, 1105, 1118,
}


class MapParseError(Exception):
    pass


class MapCorruptError(MapParseError):
    pass


class MapUnsupportedError(MapParseError):
    pass


class MapCrcError(MapCorruptError):
    pass


def _u32(data: bytes, offset: int, subject: str = "u32") -> int:
    if offset < 0 or offset + 4 > len(data):
        raise MapCorruptError(f"map ended while reading {subject} at offset {offset}")
    return struct.unpack_from("<I", data, offset)[0]


def _i32(data: bytes, offset: int, subject: str = "i32") -> int:
    if offset < 0 or offset + 4 > len(data):
        raise MapCorruptError(f"map ended while reading {subject} at offset {offset}")
    return struct.unpack_from("<i", data, offset)[0]


def _logical_id(section_id: int) -> int:
    candidate = section_id - 2000
    return candidate if candidate in TILE_IDS else section_id


# PKWARE Data Compression Library "explode", based on Mark Adler's blast.c tables.
_MAXBITS = 13
_LITLEN = bytes([
    11, 124, 8, 7, 28, 7, 188, 13, 76, 4, 10, 8, 12, 10, 12, 10, 8, 23,
    8, 9, 7, 6, 7, 8, 7, 6, 55, 8, 23, 24, 12, 11, 7, 9, 11, 12, 6, 7,
    22, 5, 7, 24, 6, 11, 9, 6, 7, 22, 7, 11, 38, 7, 9, 8, 25, 11, 8, 11,
    9, 12, 8, 12, 5, 38, 5, 38, 5, 11, 7, 5, 6, 21, 6, 10, 53, 8, 7, 24,
    10, 27, 44, 253, 253, 253, 252, 252, 252, 13, 12, 45, 12, 45, 12, 61,
    12, 45, 44, 173,
])
_LENLEN = bytes([2, 35, 36, 53, 38, 23])
_DISTLEN = bytes([2, 20, 53, 230, 247, 151, 248])
_LEN_BASE = (3, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 24, 40, 72, 136, 264)
_LEN_EXTRA = (0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8)


class _Huffman:
    def __init__(self, repeat_lengths: bytes) -> None:
        lengths: List[int] = []
        for value in repeat_lengths:
            lengths.extend([value & 15] * ((value >> 4) + 1))
        self.count = [0] * (_MAXBITS + 1)
        for length in lengths:
            self.count[length] += 1
        offsets = [0] * (_MAXBITS + 2)
        for length in range(1, _MAXBITS + 1):
            offsets[length + 1] = offsets[length] + self.count[length]
        self.symbol = [0] * len(lengths)
        for symbol, length in enumerate(lengths):
            if length:
                self.symbol[offsets[length]] = symbol
                offsets[length] += 1


_LITCODE = _Huffman(_LITLEN)
_LENCODE = _Huffman(_LENLEN)
_DISTCODE = _Huffman(_DISTLEN)


class _Bits:
    def __init__(self, data: bytes) -> None:
        self.data = data
        self.pos = 0
        self.buffer = 0
        self.count = 0

    def read(self, needed: int) -> int:
        while self.count < needed:
            if self.pos >= len(self.data):
                raise MapCorruptError("PKWARE bitstream ended unexpectedly")
            self.buffer |= self.data[self.pos] << self.count
            self.pos += 1
            self.count += 8
        result = self.buffer & ((1 << needed) - 1)
        self.buffer >>= needed
        self.count -= needed
        return result

    def decode(self, table: _Huffman) -> int:
        code = first = index = 0
        for length in range(1, _MAXBITS + 1):
            code |= self.read(1) ^ 1
            count = table.count[length]
            if code < first + count:
                return table.symbol[index + code - first]
            index += count
            first = (first + count) << 1
            code <<= 1
        raise MapCorruptError("invalid PKWARE Huffman code")


def explode(source: bytes, expected_size: int) -> bytes:
    bits = _Bits(source)
    literal_mode = bits.read(8)
    if literal_mode not in (0, 1):
        raise MapCorruptError(f"invalid PKWARE literal mode {literal_mode}")
    dictionary_bits = bits.read(8)
    if dictionary_bits not in (4, 5, 6):
        raise MapCorruptError(f"invalid PKWARE dictionary size code {dictionary_bits}")
    output = bytearray()
    while True:
        if bits.read(1) == 0:
            value = bits.read(8) if literal_mode == 0 else bits.decode(_LITCODE)
            if len(output) >= expected_size:
                raise MapCorruptError("PKWARE output exceeds declared size")
            output.append(value)
            continue
        symbol = bits.decode(_LENCODE)
        length = _LEN_BASE[symbol] + bits.read(_LEN_EXTRA[symbol])
        if length == 519:
            break
        distance_bits = 2 if length == 2 else dictionary_bits
        distance = (bits.decode(_DISTCODE) << distance_bits) + bits.read(distance_bits) + 1
        if distance > len(output) or distance <= 0:
            raise MapCorruptError("PKWARE stream contains an invalid distance")
        if len(output) + length > expected_size:
            raise MapCorruptError("PKWARE output exceeds declared size")
        # Overlapping LZ copies repeat the distance-sized source pattern.
        pattern = bytes(output[-distance:])
        repeats = (length + distance - 1) // distance
        output.extend((pattern * repeats)[:length])
    if len(output) != expected_size:
        raise MapCorruptError(
            f"PKWARE output length mismatch: expected={expected_size}, actual={len(output)}")
    return bytes(output)


@dataclass
class Section:
    owner: "MapFile"
    index: int
    section_id: int
    logical_id: int
    storage: str
    uncompressed_size: int
    stored_size: int
    relative_offset: int
    absolute_offset: int
    _content: Optional[bytes] = field(default=None, init=False, repr=False)

    @property
    def content_available(self) -> bool:
        return self.storage != "unavailable-zero-filled-dcl"

    def read(self) -> bytes:
        if self._content is not None:
            return self._content
        if self.storage == "raw":
            self._content = self.owner.raw[
                self.absolute_offset:self.absolute_offset + self.uncompressed_size]
            return self._content
        if not self.content_available:
            raise MapUnsupportedError(
                f"section {self.section_id} is a recognized zero-filled DCL placeholder")
        blob = self.owner.raw[self.absolute_offset:self.absolute_offset + self.stored_size]
        if len(blob) < 12:
            raise MapCorruptError(f"compressed section {self.section_id} has no complete header")
        declared_size, compressed_size, expected_crc = struct.unpack_from("<III", blob)
        if declared_size != self.uncompressed_size:
            raise MapCorruptError(
                f"section {self.section_id} size mismatch: directory={self.uncompressed_size}, "
                f"header={declared_size}")
        if compressed_size > len(blob) - 12:
            raise MapCorruptError(f"section {self.section_id} compressed payload exceeds bounds")
        content = explode(blob[12:12 + compressed_size], declared_size)
        actual_crc = binascii.crc32(content) & 0xFFFFFFFF
        if actual_crc != expected_crc:
            raise MapCrcError(
                f"section {self.section_id} CRC32 mismatch: expected=0x{expected_crc:08X}, "
                f"actual=0x{actual_crc:08X}")
        self._content = content
        return content


@dataclass
class MapFile:
    raw: bytes
    source: str
    format_kind: str
    preamble: Dict[str, int]
    metadata: Dict[str, object]
    directory: Optional[Dict[str, int]]
    sections: List[Section]
    opaque_tail_offset: int

    @property
    def has_placement_layers(self) -> bool:
        by_id = {section.logical_id: section for section in self.sections}
        if not all(section_id in by_id for section_id in PLACEMENT_IDS):
            return False
        logic_size = by_id[1003].uncompressed_size
        if logic_size <= 0 or logic_size % 4:
            return False
        count = logic_size // 4
        expected = {1037: count, 1005: count, 1045: count, 1004: count * 2,
                    1012: count * 2, 1026: count * 2, 1043: count}
        return all(by_id[key].uncompressed_size == size for key, size in expected.items())

    @property
    def opaque_tail(self) -> bytes:
        return self.raw[self.opaque_tail_offset:]

    @classmethod
    def parse(cls, data: bytes, source: str = "") -> "MapFile":
        data = bytes(data)
        if len(data) > MAX_FILE:
            raise MapUnsupportedError("map exceeds the supported 512 MiB limit")
        if _u32(data, 0, "SCDE magic") != SCDE_MAGIC:
            raise MapUnsupportedError("only SCDE magic 0xFFFFFFFE is supported")
        offset = 4
        blocks: Dict[str, Tuple[int, int]] = {}
        for name in ("radar", "description", "u1", "u2", "u3", "u4"):
            size = _u32(data, offset, f"{name} size")
            offset += 4
            if size > MAX_BLOCK:
                raise MapUnsupportedError(f"{name} exceeds the supported block limit")
            if offset + size > len(data):
                raise MapCorruptError(f"{name} block exceeds file bounds")
            blocks[name] = (offset, size)
            offset += size
        if blocks["u4"][1]:
            restart_size = _u32(data, offset, "restart size")
            offset += 4
            if restart_size > MAX_BLOCK or offset + restart_size > len(data):
                raise MapCorruptError("restart block exceeds file bounds")
            offset += restart_size
            if restart_size:
                _u32(data, offset, "restart terminator")
                offset += 4
        tag_offset = offset
        tag = _u32(data, offset, "directory tag")
        offset += 4
        preamble = {f"{name}_size": size for name, (_, size) in blocks.items()}
        preamble["directory_tag_offset"] = tag_offset
        metadata = _metadata(data, blocks)
        if tag in SPECIAL_TAGS:
            return cls(data, source, "scde-special", preamble, metadata, None, [], tag_offset)
        if tag not in STANDARD_TAGS:
            raise MapUnsupportedError(f"unsupported SCDE directory tag {tag}")
        capacity = (tag - 36) // 20
        body_offset = offset
        body_size = tag - 4
        if body_offset + body_size > len(data):
            raise MapCorruptError("section directory exceeds file bounds")
        payload_size = _u32(data, body_offset, "payload size")
        section_count = _u32(data, body_offset + 4, "section count")
        version = _u32(data, body_offset + 8, "format version")
        if section_count > capacity:
            raise MapCorruptError(
                f"section count {section_count} exceeds capacity {capacity}")
        array_offset = body_offset + 28
        payload_offset = body_offset + body_size
        payload_end = payload_offset + payload_size
        if payload_end > len(data):
            raise MapCorruptError("section payload exceeds file bounds")
        result = cls(data, source, "scde", preamble, metadata,
                     {"tag": tag, "capacity": capacity, "version": version,
                      "section_count": section_count, "payload_size": payload_size,
                      "payload_offset": payload_offset}, [], payload_end)
        raw_ids, logical_ids = set(), set()
        total = 0
        expected_relative = 0
        for index in range(section_count):
            values = [_u32(data, array_offset + (array_index * capacity + index) * 4)
                      for array_index in range(5)]
            uncompressed, stored, section_id, flag, relative = values
            if uncompressed > MAX_BLOCK or stored > MAX_BLOCK:
                raise MapCorruptError(f"section {section_id} exceeds the supported size")
            if flag not in (0, 1):
                raise MapCorruptError(f"section {section_id} has unknown compression flag {flag}")
            if flag == 0 and stored != uncompressed:
                raise MapCorruptError(f"raw section {section_id} has inconsistent sizes")
            if flag == 1 and stored < 12:
                raise MapCorruptError(f"compressed section {section_id} is shorter than its header")
            if relative + stored > payload_size:
                raise MapCorruptError(f"section {section_id} exceeds payload bounds")
            if relative != expected_relative:
                raise MapCorruptError(
                    f"section {section_id} starts at {relative}, expected {expected_relative}")
            expected_relative = relative + stored
            if flag == 1 and (_u32(data, payload_offset + relative) != uncompressed or
                              _u32(data, payload_offset + relative + 4) != stored - 12):
                raise MapCorruptError(
                    f"section {section_id} compressed header sizes disagree with directory")
            logical = _logical_id(section_id)
            if section_id in raw_ids:
                raise MapCorruptError(f"duplicate section ID {section_id}")
            if logical in logical_ids:
                raise MapCorruptError(f"duplicate logical section ID {logical}")
            raw_ids.add(section_id)
            logical_ids.add(logical)
            total += uncompressed
            if total > MAX_TOTAL_UNCOMPRESSED:
                raise MapUnsupportedError("total uncompressed size exceeds 1 GiB")
            absolute = payload_offset + relative
            storage = "raw" if flag == 0 else "pkware-dcl"
            if flag == 1 and _is_unavailable_1190(data, absolute, stored, section_id, uncompressed):
                storage = "unavailable-zero-filled-dcl"
            result.sections.append(Section(result, index, section_id, logical, storage,
                                           uncompressed, stored, relative, absolute))
        if expected_relative != payload_size:
            raise MapCorruptError("section ranges do not cover the declared payload exactly")
        return result


def _metadata(data: bytes, blocks: Dict[str, Tuple[int, int]]) -> Dict[str, object]:
    def optional(block: str, relative: int, fallback: int = 0) -> int:
        start, size = blocks[block]
        return _i32(data, start + relative) if relative + 4 <= size else fallback

    u3_start, u3_size = blocks["u3"]
    name = ""
    if u3_size >= 16:
        name_size = _u32(data, u3_start + 12)
        if 0 < name_size <= u3_size - 16:
            try:
                name = data[u3_start + 16:u3_start + 16 + name_size].decode("utf-8").split("\0", 1)[0]
            except UnicodeDecodeError as error:
                raise MapCorruptError("U3 standalone filename is not valid UTF-8") from error
    keeps = []
    u4_start, u4_size = blocks["u4"]
    if u4_size >= 80:
        keeps = [(_i32(data, u4_start + 16 + index * 8),
                  _i32(data, u4_start + 20 + index * 8)) for index in range(8)]
    return {
        "magic": SCDE_MAGIC,
        "map_type": optional("u2", 0),
        "max_players": optional("u2", 24),
        "mission_type": optional("u3", 0),
        "mission_lock_type": optional("u3", 8),
        "standalone_filename": name,
        "is_skirmish": optional("u4", 4) == 99,
        "is_balanced": optional("u4", 12, 1) == 0,
        "keep_locations": keeps,
        "world_size": optional("u4", 80),
    }


def _is_unavailable_1190(data: bytes, offset: int, stored: int,
                         section_id: int, uncompressed: int) -> bool:
    return (section_id == 1190 and stored > 12 and
            _u32(data, offset) == uncompressed and
            _u32(data, offset + 4) == stored - 12 and
            not any(data[offset + 12:offset + stored]))


def parse_file(path: Path) -> MapFile:
    if path.suffix.lower() != ".map":
        raise MapUnsupportedError("only .map files are supported")
    return MapFile.parse(path.read_bytes(), str(path.resolve()))


def manifest(map_file: MapFile) -> Dict[str, object]:
    sections = []
    for section in map_file.sections:
        digest = hashlib.sha256(section.read()).hexdigest() if section.content_available else None
        sections.append({"id": section.section_id, "logical_id": section.logical_id,
                         "storage": section.storage, "stored_size": section.stored_size,
                         "uncompressed_size": section.uncompressed_size, "sha256": digest})
    return {"format": map_file.format_kind, "metadata": map_file.metadata,
            "directory": map_file.directory, "has_placement_layers": map_file.has_placement_layers,
            "opaque_tail_size": len(map_file.opaque_tail), "sections": sections}


def _files(path: Path) -> Iterable[Path]:
    if path.is_file():
        yield path
    elif path.is_dir():
        yield from sorted(path.rglob("*.map"))
    else:
        raise FileNotFoundError(path)


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    for command in ("info", "list", "manifest"):
        item = sub.add_parser(command)
        item.add_argument("file", type=Path)
    dump = sub.add_parser("dump")
    dump.add_argument("file", type=Path)
    dump.add_argument("section_id", type=int)
    dump.add_argument("output", nargs="?", type=Path)
    validate = sub.add_parser("validate")
    validate.add_argument("path", type=Path)
    validate.add_argument("--progress-every", type=int, default=10,
                          help="report elapsed time and ETA every N maps (default: 10)")
    args = parser.parse_args(argv)
    try:
        if args.command == "validate":
            regular = special = failed = unavailable_sections = 0
            paths = list(_files(args.path))
            started = time.perf_counter()
            progress_every = max(1, args.progress_every)
            for index, path in enumerate(paths, 1):
                state = "OK"
                unavailable_in_map = 0
                try:
                    current = parse_file(path)
                    if current.directory is None:
                        special += 1
                        state = "SPECIAL"
                    else:
                        for section in current.sections:
                            if section.content_available:
                                section.read()
                            else:
                                unavailable_in_map += 1
                        unavailable_sections += unavailable_in_map
                        regular += 1
                except (OSError, MapParseError) as error:
                    failed += 1
                    state = "FAIL"
                    print(f"FAIL {path}: {error}", file=sys.stderr)
                if index == 1 or index == len(paths) or index % progress_every == 0 or state == "FAIL":
                    elapsed = time.perf_counter() - started
                    eta = (elapsed / index) * (len(paths) - index) if index else 0.0
                    print(f"[{index}/{len(paths)}] {state} unavailable={unavailable_in_map} "
                          f"elapsed={elapsed:.1f}s eta={eta:.1f}s {path}",
                          flush=True)
            elapsed = time.perf_counter() - started
            print(f"Summary: files={len(paths)}, regular={regular}, special={special}, "
                  f"unavailable-sections={unavailable_sections}, failed={failed}, "
                  f"elapsed={elapsed:.1f}s")
            return 0 if failed == 0 else 2
        current = parse_file(args.file)
        if args.command == "info":
            print(json.dumps({"format": current.format_kind, "metadata": current.metadata,
                              "directory": current.directory,
                              "has_placement_layers": current.has_placement_layers,
                              "opaque_tail_size": len(current.opaque_tail)}, indent=2))
        elif args.command == "list":
            for section in current.sections:
                print(section.index, section.section_id, section.logical_id, section.storage,
                      section.stored_size, section.uncompressed_size, section.absolute_offset)
        elif args.command == "manifest":
            print(json.dumps(manifest(current), indent=2, sort_keys=True))
        elif args.command == "dump":
            section = next((value for value in current.sections
                            if value.section_id == args.section_id), None)
            if section is None:
                section = next((value for value in current.sections
                                if value.logical_id == args.section_id), None)
            if section is None:
                raise MapParseError(f"section {args.section_id} was not found")
            output = args.output or Path(f"section-{section.section_id}.bin")
            output.write_bytes(section.read())
            print(f"Wrote {section.uncompressed_size} bytes to {output.resolve()}")
        return 0
    except (OSError, MapParseError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
