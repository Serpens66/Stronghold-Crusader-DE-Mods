"""Hash-bound, read-only audit. Runtime publication additionally checks pristine/live bytes."""
import hashlib
import json
import sqlite3
import struct
from pathlib import Path
from capstone import Cs, CS_ARCH_X86, CS_MODE_64, CS_GRP_JUMP, CS_GRP_CALL
from capstone.x86 import X86_OP_IMM

root = Path(__file__).resolve().parents[3]
baseline = root / '_inspect/CrusaderDE-Native-Baseline'
current = json.loads((baseline / 'CURRENT.json').read_text(encoding='utf-8-sig'))
manifest = json.loads((baseline / current['databaseManifest']).read_text(encoding='utf-8-sig'))
binary = next(b for b in manifest['binaries'] if b['role'] == 'current-native')
image = Path(binary['sourcePath']).read_bytes()
identity = hashlib.sha256(image).hexdigest().upper()
assert identity == current['currentNativeHash'] == 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
pe = struct.unpack_from('<I', image, 0x3c)[0]
sections = [struct.unpack_from('<IIII', image, pe + 24 + struct.unpack_from('<H', image, pe + 20)[0] + i * 40 + 8)
            for i in range(struct.unpack_from('<H', image, pe + 6)[0])]
def read(rva, count):
    offset = next(rva - va + raw for size, va, raw_size, raw in sections if va <= rva < va + raw_size)
    return image[offset:offset + count]

site, length, fallback, end = 0x58BAE, 14, 0x58B2E, 0x58BBC
assert read(site, length) == bytes.fromhex('4489993C981B0044899140981B00')
decoder = Cs(CS_ARCH_X86, CS_MODE_64); decoder.detail = True
instructions = list(decoder.disasm(read(0x58950, 0x290), 0x58950))
assert sum(i.size for i in instructions) == 0x290
assert [i.size for i in instructions if site <= i.address < end] == [7, 7]
assert any(i.address == fallback and i.mnemonic == 'movzx' and i.op_str.startswith('eax,') for i in instructions)
assert any(i.address == end and i.mnemonic == 'mov' and i.op_str == 'r12, qword ptr [rsp + 8]' for i in instructions)
assert all(i.mnemonic in ('mov', 'add', 'pop', 'ret') for i in instructions if i.address >= end)
assert not any(i.group(CS_GRP_CALL) or any(s in i.op_str for s in ('xmm', 'ymm', 'zmm')) for i in instructions)
edges = [(i.address, i.operands[0].imm) for i in instructions if i.group(CS_GRP_JUMP) and i.operands[0].type == X86_OP_IMM]
assert not [(a, b) for a, b in edges if site < b < end]
assert (0x58B28, site) in edges and (0x58BAC, end) in edges
with sqlite3.connect((baseline / manifest['database']['path']).resolve().as_uri() + '?mode=ro', uri=True) as q:
    assert q.execute('SELECT size FROM functions WHERE binary_hash=? AND rva=?', (identity, '0x58950')).fetchone()[0] == 0x290
    incoming = q.execute('SELECT from_rva,to_rva,type FROM xrefs WHERE binary_hash=? AND to_rva IS NOT NULL', (identity,)).fetchall()
    assert not [(f, t, k) for f, t, k in incoming if site < int(t, 16) < end]
# Audited selected-owner spans: nearby entry (Bugfix), farmland inline (Fixes),
# general wood search entry and worker increment. Live publication still rejects
# any new owner occupying our bytes, independent of these dated source findings.
for owner, start, size in [('Bugfix nearby entry envelope', 0x58950, 32), ('Fixes farmland', 0x58B12, 0x1C),
                           ('general wood entry envelope', 0x58020, 32), ('general worker counter', 0x12D1A2, 21)]:
    assert not (site < start + size and start < end), owner
    print(f'PASS disjoint selected-owner span: {owner} {start:#x}+{size}')
print(f'PASS {identity}: full BFS decoded; no calls/SIMD, two displaced MOVs, fallback/epilogue boundaries, no interior incoming edges')
