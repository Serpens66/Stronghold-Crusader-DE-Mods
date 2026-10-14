"""Read-only native span/data-flow regression against CURRENT and canonical DLL."""
import hashlib
import json
import pathlib
import re
import sqlite3
import sys
import capstone
import pefile

root = pathlib.Path(__file__).resolve().parents[3]
baseline = root / '_inspect/CrusaderDE-Native-Baseline'
current = json.loads((baseline / 'CURRENT.json').read_text())
native_path = pathlib.Path(sys.argv[1])
binary = native_path.read_bytes()
native_hash = hashlib.sha256(binary).hexdigest().upper()
assert native_hash == current['currentNativeHash'] == 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
pe = pefile.PE(data=binary)
base = pe.OPTIONAL_HEADER.ImageBase
cs = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
cs.detail = True
cs.skipdata = True
db = sqlite3.connect((baseline / current['semanticDirectory'] / 'CrusaderDE-semantic.sqlite').resolve().as_uri() + '?mode=ro', uri=True)
sites = [(0x9FDB8, 32, 0x9F960), (0x192809, 26, 0x192750), (0x9CB6F, 26, 0x9C730), (0x9CC3D, 26, 0x9C730)]
source = (pathlib.Path(__file__).parents[1] / 'src/PlagueHeightAssembly.cs').read_text()
hexes = re.findall(r'"([A-F0-9]{40,})"', source)
for (site, size, function), expected in zip(sites, hexes):
    assert pe.get_data(site, size).hex().upper() == expected
    instructions = list(cs.disasm(pe.get_data(site, size), base + site))
    assert sum(i.size for i in instructions) == size and all(i.id for i in instructions)
    row = db.execute('select size from functions where binary_hash=? and rva=?', (native_hash, f'0x{function:X}')).fetchone()
    assert row
    for ins in cs.disasm(pe.get_data(function, row[0]), base + function):
        if ins.id and (ins.group(capstone.CS_GRP_JUMP) or ins.group(capstone.CS_GRP_CALL)) and ins.operands and ins.operands[0].type == capstone.CS_OP_IMM:
            target = ins.operands[0].imm - base
            assert not (site < target < site + size and not site <= ins.address - base < site + size), (hex(site), hex(ins.address), hex(target))
    # Includes cross-function jumps, jump-table references and non-code evidence.
    interior = [f'0x{x:X}' for x in range(site + 1, site + size)]
    xrefs = db.execute('select from_rva,to_rva,type from xrefs where binary_hash=? and to_rva in (' + ','.join('?' for _ in interior) + ')', (native_hash, *interior)).fetchall()
    assert not xrefs, xrefs
    print(f'PASS: native RVA {site:X}, span {size}, no interior entry; ' + ' / '.join(i.mnemonic + ' ' + i.op_str for i in instructions))

# The called helper uses only these registers and no SIMD, stack or mutable memory.
helper = list(cs.disasm(pe.get_data(0xC07C0, 0x70), base + 0xC07C0))
for i in helper:
    assert i.id and i.mnemonic not in ('call', 'push', 'pop')
    reads, writes = i.regs_access()
    assert all(i.reg_name(r) in ('eax', 'rax', 'ecx', 'rcx', 'edx', 'rdx', 'rflags', 'eflags') or (i.mnemonic == 'ret' and i.reg_name(r) == 'rsp') for r in writes)
    assert not any(o.type == capstone.CS_OP_MEM and o.access & capstone.CS_AC_WRITE for o in i.operands)

assert 'a.movsxd(rdx, __dword_ptr[rsp + 64])' in source
assert source.index('a.movsxd(rdx, __dword_ptr[rsp + 64])') < source.index('a.mov(__word_ptr[rbx + rcx + 0x50], ax)')
assert 'for (int i = 0; i < 6; i++) Replay(a, original[i]);' in source
assert 'Replay(a, original[8]); Replay(a, original[9]);' in source
print('PASS: native helper register/memory contract, saved-index read-before-write and neighbor alive gates.')
