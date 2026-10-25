"""Second-pass review: execute pristine leaf/height code on private buffers only.

Numeric selectors are literal machine-contract cases, not gameplay constants or
new public enums. Never attaches to the game or patches a published function.
"""
import ctypes
import hashlib
import itertools
import json
from pathlib import Path
import sqlite3
import struct
import sys
import capstone
import pefile


def main():
    baseline = Path(__file__).resolve().parents[2]
    current = json.loads((baseline / 'CURRENT.json').read_text(encoding='utf-8'))
    manifest = json.loads((baseline / current['databaseManifest']).read_text(encoding='utf-8'))
    native = Path(sys.argv[1]).read_bytes()
    binary_hash = hashlib.sha256(native).hexdigest().upper()
    assert binary_hash == current['currentNativeHash']
    pe = pefile.PE(data=native)
    con = sqlite3.connect((baseline / manifest['database']['path']).resolve().as_uri() + '?mode=ro', uri=True)
    cs = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    cs.detail = True
    checked = []
    for rva in (0xC0700, 0xC07C0, 0x184FD0, 0xCEB10, 0xCEB90, 0x3B1D0):
        size, expected = con.execute('SELECT size,raw_hash FROM functions WHERE binary_hash=? AND rva=?',
                                     (binary_hash, f'0x{rva:X}')).fetchone()
        data = pe.get_data(rva, size)
        assert hashlib.sha256(data).hexdigest().upper() == expected
        ins = list(cs.disasm(data, pe.OPTIONAL_HEADER.ImageBase + rva))
        assert sum(i.size for i in ins) == size
        checked.append(dict(rva=f'0x{rva:X}', size=size, rawHash=expected, instructions=len(ins)))
    # The complete C07C0 leaf has no calls or memory writes. Jump-table data is
    # copied with the function, at its original module-relative address.
    for ins in cs.disasm(pe.get_data(0xC07C0, 112), pe.OPTIONAL_HEADER.ImageBase + 0xC07C0):
        assert ins.mnemonic != 'call'
        assert not any(o.type == capstone.CS_OP_MEM and o.access & capstone.CS_AC_WRITE for o in ins.operands)
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.VirtualAlloc.restype = ctypes.c_void_p
    kernel.VirtualAlloc.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_ulong, ctypes.c_ulong]
    kernel.VirtualFree.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_ulong]
    module = kernel.VirtualAlloc(None, 0x6800000, 0x3000, 0x40)
    if not module:
        raise ctypes.WinError(ctypes.get_last_error())
    cases = 0
    try:
        ctypes.memmove(module + 0xC0700, pe.get_data(0xC0700, 0x17F), 0x17F)
        ctypes.memmove(module + 0x184FD0, pe.get_data(0x184FD0, 437), 437)
        for rva in (0xCEB10, 0xCEB90):
            ctypes.memmove(module + rva, pe.get_data(rva, 31), 31)
        height = ctypes.WINFUNCTYPE(ctypes.c_int, ctypes.c_void_p, ctypes.c_int)(module + 0xC07C0)
        update = ctypes.WINFUNCTYPE(None, ctypes.c_void_p, ctypes.c_int)(module + 0x184FD0)
        prices = [ctypes.WINFUNCTYPE(ctypes.c_int, ctypes.c_void_p, ctypes.c_int, ctypes.c_int, ctypes.c_int)(module + rva)
                  for rva in (0xCEB10, 0xCEB90)]
        manager = ctypes.create_string_buffer(0x3000)
        mp = ctypes.addressof(manager)
        roof = {40:64, 41:92, 42:190, 45:128, 46:128, 69:118, 74:296, 75:148, 76:180, 77:192, 78:192}
        extra = {41:-12, 42:-25, 74:-6, 75:-10, 76:-18, 77:-21, 78:-13}
        def word(address, value):
            ctypes.c_uint16.from_address(address).value = value & 0xFFFF
        def integer(address, value):
            ctypes.c_int.from_address(address).value = value
        def byte(address, value):
            ctypes.c_uint8.from_address(address).value = value
        def s16(value):
            return ctypes.c_int16(value).value
        # Exhaust every possible raw type word, including both sides of all switch bounds.
        for raw_type in range(65536):
            word(mp + 0x32C + 0x12E, raw_type)
            assert height(mp, 1) == roof.get(raw_type, 0), raw_type
            cases += 1
        # Exact signed division toward zero, divide-before-multiply and wrapping
        # multiplication are exercised in the existing named price helpers.
        for value, amount in itertools.product((-2147483648, -17, -1, 0, 1, 4, 5, 17, 2147483647), (-8, 0, 1, 7, 2147483647)):
            for function, offset in zip(prices, (0x1817B8, 0x1817BC)):
                integer(module + offset + 3 * 8, value)
                q = abs(value) // 5 * (-1 if value < 0 else 1)
                assert function(module, 2, 3, amount) == ctypes.c_int32(q * amount).value
                cases += 1
        tile = 1
        integer(module + 0x9302C4, 2)  # Additional-height source differs from the updated unit ID.
        bits = (0x100, 0x400, 0x800, 0x10000, 0x10000000)
        flags = [sum(bit for bit, enabled in zip(bits, state) if enabled) for state in itertools.product((0,1), repeat=5)]
        # Includes default/no-building, each height case, bridge/type special cases,
        # both state flags, signed boundaries, and the independent final addition.
        combinations = itertools.product(flags, (0, 40, 41, 42, 45, 49, 58, 69, 74, 75, 76, 77, 78),
                                          (0,1), (0,1), (-1, 0, 97, 98, 32767), (-13,0,7), (1,2))
        for mask, kind, suppress, transition, base_height, addition, unit_id in combinations:
            slot = mp + unit_id * 0x490
            ctypes.memset(mp, 0xA5, 0x3000)
            integer(slot + 0x72C, tile)
            word(slot + 0x712, base_height)
            word(slot + 0x6E6, 58 if kind == 58 else 1)
            byte(slot + 0xA5A, suppress)
            byte(slot + 0xA5B, transition)
            integer(module + 0x48F71B0 + tile * 4, mask)
            building_id = 0 if kind == 0 else 1
            word(module + 0x4B6AA50 + tile * 2, building_id)
            byte(module + 0x4DDD350 + tile, 23)
            building = module + 0x64CCBB0 + building_id * 0x32C
            word(building + 0x12E, kind)
            word(building + 0x148, 31)
            integer(building + 0x10C, transition)
            word(module + 0x67E8E70 + 2 * 0x490, addition)
            expected = bytearray(ctypes.string_at(mp, 0x3000))
            def ew(offset, value):
                struct.pack_into('<H', expected, unit_id * 0x490 + offset, value & 0xFFFF)
            correction, auxiliary, new_base, new_transition = 0, 0, base_height, transition
            if mask & 0x10010D00 == 0:
                if building_id:
                    if kind == 49:
                        correction = 8 - base_height
                elif transition:
                    new_base, new_transition = 23, 0
            elif mask & 0x800:
                correction = 12
            elif mask & 0x100:
                if building_id:
                    if not suppress:
                        correction = 40
                    else:
                        correction = -90 if base_height >= 98 else 0
                        new_transition = 1
            elif mask & 0x400:
                correction = 2
            elif mask & 0x10000000:
                if not suppress and kind != 58:
                    correction = roof.get(kind, 0)
                auxiliary = extra.get(kind, 0) if transition else 0
                new_base = 31
            ew(0x712, new_base)
            ew(0x714, correction + addition)
            ew(0x6DC, auxiliary)
            expected[unit_id * 0x490 + 0xA5B] = new_transition
            update(mp, unit_id)
            assert ctypes.string_at(mp, 0x3000) == expected, (mask,kind,suppress,transition,base_height,addition,unit_id)
            cases += 1
        result = dict(status='ok', binaryHash=binary_hash, checks=cases, functions=checked,
                      method='Pristine copied machine code in private synthetic memory, no game hooks or game acceptance.',
                      reviewedBranches='Every roof-type word; standing-height flag precedence, restoration, signed thresholds, both helpers and independent final signed addition; full manager buffer compared.')
        Path(sys.argv[2]).write_text(json.dumps(result, indent=2).replace('\n','\r\n')+'\r\n', encoding='utf-8', newline='')
        print(json.dumps(result))
    finally:
        kernel.VirtualFree(module, 0, 0x8000)


if __name__ == '__main__':
    main()
