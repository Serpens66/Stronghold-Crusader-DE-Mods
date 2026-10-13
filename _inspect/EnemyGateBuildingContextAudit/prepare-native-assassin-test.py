# Development-only local schema-3 settings editor. No runtime or save changes.
from pathlib import Path
import ctypes
import hashlib
import io
import os
import sys

P = Path('E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/plugins/BugfixesAndQoL_Serp/LobbyModSettings/BugfixesAndQoL_Serp.msgpack')
WEIGHTED = 'EnableImprovedAssassinPathfinding'
CLIMB = 'EnableAssassinGatehouseClimbFix'
CURRENT = '__SerpCurrentSettings'
SCHEMA = '__SerpPresetSchemaVersion'

def require(condition, reason):
    if not condition:
        raise ValueError(reason)

def fields(data):
    position = 0
    def number(length):
        nonlocal position
        require(position + length <= len(data), 'Truncated MessagePack')
        value = int.from_bytes(data[position:position + length], 'big')
        position += length
        return value
    def blob(kind):
        nonlocal position
        tag = number(1)
        if kind == 'str' and 0xa0 <= tag <= 0xbf:
            length = tag & 31
        elif kind == 'str' and tag in (0xd9, 0xda, 0xdb):
            length = number({0xd9: 1, 0xda: 2, 0xdb: 4}[tag])
        elif kind == 'bin' and tag in (0xc4, 0xc5, 0xc6):
            length = number({0xc4: 1, 0xc5: 2, 0xc6: 4}[tag])
        else:
            raise ValueError('Unsupported field representation')
        start = position
        position += length
        require(position <= len(data), 'Truncated field')
        return data[start:position], start
    tag = number(1)
    count = number(2) if tag == 0xde else number(4) if tag == 0xdf else tag & 15 if 0x80 <= tag <= 0x8f else None
    require(count is not None, 'Expected map')
    result = {}
    for _ in range(count):
        key = blob('str')[0].decode('utf-8')
        require(key not in result, 'Duplicate key')
        result[key] = blob('bin')
    require(position == len(data), 'Trailing bytes')
    return result

def prepare(before):
    outer = fields(before)
    require(SCHEMA in outer and outer[SCHEMA][0] == b'\x03', 'Only audited schema 3 is supported')
    require(CURRENT in outer, 'Missing authoritative current settings')
    inner = fields(outer[CURRENT][0])
    for values in (outer, inner):
        require(WEIGHTED in values and values[WEIGHTED][0] in (b'\xc2', b'\xc3'), 'Missing/non-boolean weighted setting')
        require(CLIMB in values and values[CLIMB][0] == b'\xc3', 'Climbing must remain enabled')
    offsets = {outer[WEIGHTED][1], outer[CURRENT][1] + inner[WEIGHTED][1]}
    after = bytearray(before)
    for offset in offsets:
        after[offset] = 0xc2
    after = bytes(after)
    require(len(after) == len(before), 'Unexpected length change')
    require(all(a == b or index in offsets for index, (a, b) in enumerate(zip(before, after))), 'Unrelated bytes changed')
    changed_outer = fields(after)
    changed_inner = fields(changed_outer[CURRENT][0])
    for key, value in outer.items():
        if key not in (WEIGHTED, CURRENT):
            require(changed_outer[key] == value, 'Unrelated outer setting changed')
    for key, value in inner.items():
        if key != WEIGHTED:
            require(changed_inner[key] == value, 'Unrelated current setting changed')
    require(changed_outer[WEIGHTED][0] == changed_inner[WEIGHTED][0] == b'\xc2', 'Effective value did not change')
    return after

def apply_stream(stream, before, after):
    stream.seek(0)
    require(stream.read() == before, 'Concurrent configuration edit; nothing written')
    stream.seek(0)
    require(stream.write(after) == len(after), 'Incomplete write')
    stream.flush()
    stream.seek(0)
    require(stream.read() == after, 'Readback mismatch')

def apply_exclusive(path, before, after):
    # Windows share mode 0 prevents another reader/writer changing the file while verified/written.
    import msvcrt
    from ctypes import wintypes
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    create = kernel.CreateFileW
    create.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    create.restype = wintypes.HANDLE
    handle = create(str(path), 0xc0000000, 0, None, 3, 0x80, None)
    if handle == ctypes.c_void_p(-1).value:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        descriptor = msvcrt.open_osfhandle(handle, os.O_RDWR | os.O_BINARY)
    except Exception:
        kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        kernel.CloseHandle(handle)
        raise
    with os.fdopen(descriptor, 'r+b') as stream:
        apply_stream(stream, before, after)
        os.fsync(stream.fileno())

def tests():
    def pack(values):
        result = bytearray(b'\xde' + len(values).to_bytes(2, 'big'))
        for key, value in values.items():
            name = key.encode()
            result += b'\xd9' + bytes([len(name)]) + name
            result += b'\xc5' + len(value).to_bytes(2, 'big') + value
        return bytes(result)
    def fixture(root, nested, schema=b'\x03'):
        inside = {WEIGHTED: nested, CLIMB: b'\xc3', 'untouched': b'\x00\xff\x01'}
        return pack({SCHEMA: schema, WEIGHTED: root, CLIMB: b'\xc3', CURRENT: pack(inside), 'untouched': b'\x01\x02\x03'})
    for root in (b'\xc2', b'\xc3'):
        for nested in (b'\xc2', b'\xc3'):
            before = fixture(root, nested)
            after = prepare(before)
            require(prepare(after) == after, 'Idempotence')
            stream = io.BytesIO(before)
            apply_stream(stream, before, after)
            require(stream.getvalue() == after, 'Write path')
    invalid = [fixture(b'\xc3', b'\xc3', b'\x04'), fixture(b'\xc3', b'\x01'), pack({WEIGHTED: b'\xc3'}), bytes.fromhex('82a141c401c3a141c401c2')]
    for data in invalid:
        try:
            prepare(data)
        except ValueError:
            continue
        raise RuntimeError('Invalid format accepted')
    before = fixture(b'\xc3', b'\xc3')
    changed = fixture(b'\xc2', b'\xc3')
    stream = io.BytesIO(changed)
    try:
        apply_stream(stream, before, prepare(before))
    except ValueError:
        require(stream.getvalue() == changed, 'Concurrent edit was overwritten')
    else:
        raise RuntimeError('Concurrent edit accepted')
    print('PASS: outer/current precedence, unrelated bytes, idempotence, invalid formats, duplicate keys, concurrent edit and write/readback')

if __name__ == '__main__':
    tests()
    before = P.read_bytes()
    after = prepare(before)
    outer = fields(before)
    inner = fields(outer[CURRENT][0])
    print('Stored before: outerWeighted=', outer[WEIGHTED][0] == b'\xc3', 'effectiveCurrentWeighted=', inner[WEIGHTED][0] == b'\xc3', 'climb=True; changedBytes=', sum(a != b for a,b in zip(before,after)))
    if '--apply' in sys.argv:
        apply_exclusive(P, before, after)
        actual = P.read_bytes()
        require(actual == after and prepare(actual) == actual, 'Final persisted verification')
        print('APPLIED and VERIFIED: outerWeighted=False, effectiveCurrentWeighted=False, climb=True; sha256=' + hashlib.sha256(actual).hexdigest())
