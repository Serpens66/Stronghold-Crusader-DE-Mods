"""Read-only native boundary regression against the current semantic database."""
import hashlib
import re
import sqlite3
import sys
from pathlib import Path
import capstone
import pefile

workspace, game = map(Path, sys.argv[1:])
native = game / 'Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll'
expected = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
assert hashlib.sha256(native.read_bytes()).hexdigest().upper() == expected
pe = pefile.PE(str(native))
decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
decoder.detail = True
database = workspace / '_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/CrusaderDE-semantic.sqlite'
with sqlite3.connect(database.as_uri() + '?mode=ro', uri=True) as db:
    edges = db.execute('select from_rva,to_rva,type from xrefs where binary_hash=? and to_rva is not null', (expected,)).fetchall()
    for rva, size, lengths in [(0x16F590, 3879, [5,5,5]), (0x1A41E0, 137, [4,3,4,3])]:
        row = db.execute('select size,pseudocode from functions where binary_hash=? and rva=?', (expected, f'0x{rva:X}')).fetchone()
        assert row and row[0] == size
        instructions = list(decoder.disasm(pe.get_data(rva,size),rva))
        total = 0
        span = []
        for instruction in instructions:
            span.append(instruction)
            total += instruction.size
            if total >= 14: break
        assert [i.size for i in span] == lengths
        end = rva + total
        assert not [e for e in edges if rva < int(str(e[1]),16) < end]
        for instruction in instructions:
            if instruction.group(capstone.CS_GRP_JUMP) or instruction.group(capstone.CS_GRP_CALL):
                for operand in instruction.operands:
                    if operand.type == capstone.x86.X86_OP_IMM:
                        assert not rva < operand.imm < end
        assert all(not i.group(capstone.CS_GRP_JUMP) and not i.group(capstone.CS_GRP_CALL) for i in span)
        assert instructions[len(span)].address == end
        print(f'PASS: RVA 0x{rva:X}, Absolute span={total}, lengths={lengths}, continuation=0x{end:X}, no interior edges.')
    # Verify the actual unit dispatch table and native current-unit RIP operand.
    assert int.from_bytes(pe.get_data(0x321CB0+75*8,8),'little') == pe.OPTIONAL_HEADER.ImageBase+0x16F590
    displacement = int.from_bytes(pe.get_data(0x16F5AF,4),'little',signed=True)
    assert 0x16F5B3+displacement == 0x9302C4
    call = pe.get_data(0x1700CD,5)
    assert call[0] == 0xE8 and 0x1700D2+int.from_bytes(call[1:],'little',signed=True)==0x2960
    print('PASS: type75 dispatcher, current-unit derivation and exact idle sound call target.')
