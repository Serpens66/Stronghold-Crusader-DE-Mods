"""Execute canonical Vanilla PCL rebuild/query in a private PE image, never the game.

The graph fixtures supply already updated tile flags/edges, exactly the boundary
after editor mutation. Native terrain-tool execution and GUI acceptance are separate.
Only the native memory-fill helper is replaced by CRT memset in this private image;
the real flood, dirty/countdown gate and reachability predicate execute unchanged.
"""
import ctypes as c
import hashlib
import json
import pathlib
import re
import struct
import sys
import time
import pefile

game = pathlib.Path(sys.argv[1])
contract = json.loads(pathlib.Path(sys.argv[2]).read_text())
binary = game / 'Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll'
data = binary.read_bytes()
assert hashlib.sha256(data).hexdigest().upper() == contract['hash'], 'fixture requires its audited native image/layout'
pe = pefile.PE(data=data, fast_load=True)
pattern = b''.join(b'.' if '?' in t else re.escape(bytes([int(t, 16)])) for t in contract['pattern'].split())
text = next(s for s in pe.sections if s.Name.startswith(b'.text'))
hits = [text.VirtualAddress + m.start() for m in re.finditer(pattern, text.get_data(), re.S)]
assert hits == [contract['rva']], 'compiled production pattern must uniquely identify the real rebuild'
k = c.WinDLL('kernel32', use_last_error=True)
k.VirtualAlloc.argtypes = [c.c_void_p, c.c_size_t, c.c_uint32, c.c_uint32]
k.VirtualAlloc.restype = c.c_void_p
k.VirtualFree.argtypes = [c.c_void_p, c.c_size_t, c.c_uint32]
base = k.VirtualAlloc(None, pe.OPTIONAL_HEADER.SizeOfImage, 0x3000, 0x40)
assert base, c.get_last_error()

def u32(rva): return c.c_uint32.from_address(base + rva)
def i32(rva): return c.c_int32.from_address(base + rva)
def byte(rva): return c.c_ubyte.from_address(base + rva)
def pcl(tile): return c.c_ushort.from_address(base + 0x50EC690 + tile * 2).value

try:
    for section in pe.sections:
        raw = section.get_data()
        c.memmove(base + section.VirtualAddress, raw, len(raw))
    # E49D0's participating code/globals are RIP-relative. No imported game startup,
    # DllMain, live buffers, hooks, archive or game process is loaded.
    memset = c.cast(c.CDLL('msvcrt').memset, c.c_void_p).value
    clear = bytes.fromhex('48 89 D0 4C 89 C9 44 89 C2 49 89 C0 48 B8') + struct.pack('<Q', memset) + bytes.fromhex('FF E0')
    c.memmove(base + 0x7490, clear, len(clear))
    manager = base + 0x60AD660
    rebuild = c.WINFUNCTYPE(c.c_int, c.c_void_p, c.c_int)(base + contract['rva'])
    reachable = c.WINFUNCTYPE(c.c_int, c.c_void_p, c.c_int, c.c_int, c.c_int, c.c_int)(base + 0xE2610)
    capacity = 320800
    flags = (c.c_uint32 * capacity).from_address(base + 0x48F71B0)
    edges = (c.c_ubyte * capacity).from_address(base + 0x51890D0)
    # One synthetic packed row. Everything outside the five-cell corridor is
    # impassable, so only east/west edges participate in this controlled fixture.
    for i in range(capacity): flags[i] = 0x10
    i32(0x402FF34).value = capacity
    u32(0x64CCBB0 + 0x50).value = 0  # no gate buildings in this graph fixture

    def corridor():
        for tile in range(1, 6):
            flags[tile] = 0
            edges[tile] = (0x40 if tile > 1 else 0) | (4 if tile < 5 else 0)

    timings = []
    for kind, obstruction in [('water', 1), ('moat', 0x40000000), ('rock', 0x80)]:
        corridor()
        assert rebuild(manager, 1) == 1
        assert pcl(1) == pcl(5) != 0
        flags[3] = obstruction
        edges[2] &= ~4; edges[3] = 0; edges[4] &= ~0x40
        generation = u32(0x60AD660 + 0x74).value
        assert i32(0x60AD660 + 0x6C).value == 0
        for _ in range(201): assert rebuild(manager, 0) == 0
        assert u32(0x60AD660 + 0x74).value == generation
        assert reachable(manager, 1, pcl(1), pcl(5), 0) != 0, 'stale regions reproduce false permission'
        i32(0x60AD660 + 0x6C).value = 1  # RequestComponentRebuild's exact public contract
        began = time.perf_counter()
        assert rebuild(manager, 1) == 1
        timings.append((time.perf_counter() - began) * 1000)
        assert pcl(1) != pcl(5) and pcl(3) == 0
        assert reachable(manager, 1, pcl(1), pcl(5), 0) == 0
        assert u32(0x60AD660 + 0x74).value == (generation + 1) & 0xFFFFFFFF
        assert i32(0x60AD660 + 0x6C).value == 0
        corridor()
        assert rebuild(manager, 1) == 1 and pcl(1) == pcl(5) != 0
        print('PASS: actual native stale/forced/removal reachability:', kind)
    print('PASS: compiled semantic fallback; real full-capacity PCL rebuild ms:', ', '.join(f'{v:.3f}' for v in timings), '(sparse private fixture; not gameplay performance)')
finally:
    k.VirtualFree(base, 0, 0x8000)
