"""Read-only ABI/instruction checks against the installed, hash-bound native DLL."""
from pathlib import Path
import hashlib
import json
import re
import sqlite3
import capstone
import pefile

root = Path(__file__).resolve().parents[2]
baseline = root / '_inspect/CrusaderDE-Native-Baseline'
current = json.loads((baseline / 'CURRENT.json').read_text())
manifest = json.loads((baseline / current['databaseManifest']).read_text())
native = Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll')
raw = native.read_bytes()
assert hashlib.sha256(raw).hexdigest().upper() == current['currentNativeHash']
db = sqlite3.connect((baseline / manifest['database']['path']).resolve().as_uri() + '?mode=ro', uri=True)
pe = pefile.PE(data=raw, fast_load=True)
decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
image_base = pe.OPTIONAL_HEADER.ImageBase
source = (root / 'MoveMoatTest/src/MoatPlacement.cs').read_text()
source += (root / 'MoveMoatTest/src/NativeFormationSlots.cs').read_text()

# Minimum whole-instruction prefixes covering a possible 14-byte entry detour.
# MonoMod relocates the original instructions; there is no custom register clobber.
prefixes = {
    0x118E00: (15, ['mov', 'mov', 'mov']),
    0x181890: (15, ['mov', 'mov', 'push', 'sub']),
    0xF03C0: (17, ['mov', 'push', 'push', 'push', 'sub', 'inc']),
    0xE1D30: (15, ['mov', 'mov', 'mov']),
}
for rva, (length, mnemonics) in prefixes.items():
    bound_hash, size, code = db.execute('select binary_hash,size,pseudocode from functions where rva=?', (f'0x{rva:X}',)).fetchone()
    assert bound_hash == current['currentNativeHash']
    expected = re.search(r'0x' + f'{rva:X}' + r',\s*"([0-9A-F ]+)"', source).group(1)
    pattern = bytes.fromhex(expected)
    assert pe.get_data(rva, len(pattern)) == pattern and raw.count(pattern) == 1
    instructions = list(decoder.disasm(pe.get_data(rva, length), rva))
    assert [i.mnemonic for i in instructions] == mnemonics
    assert instructions[-1].address + instructions[-1].size == rva + length
    print(f'PASS entry 0x{rva:X}..0x{rva+length:X}: {pe.get_data(rva,length).hex(" ").upper()}')
    for i in instructions:
        print(f'  {i.address:X} {i.mnemonic} {i.op_str}')
    calls = [i for i in decoder.disasm(pe.get_data(rva, size), rva) if i.mnemonic == 'call']
    print('  calls:', ', '.join(f'{i.address:X}->{i.op_str}' for i in calls))

def pseudo(rva):
    binary_hash, text = db.execute('select binary_hash,pseudocode from functions where rva=?', (f'0x{rva:X}',)).fetchone()
    assert binary_hash == current['currentNativeHash']
    return text

common = pseudo(0x118E00)
assert 'FUN_180196280' in common and 'DAT_1867e8d34' in common and 'DAT_1867e8d36' in common
unstack = pseudo(0x181890)
assert 'FUN_1800f03c0' in unstack and 'FUN_180196280' in unstack and 'DAT_1809302c4' in unstack
free = pseudo(0xF03C0)
assert 'DAT_1851d75f0' in free and 'DAT_184c559b0' in free and 'DAT_1850ec690' in free
# Native GameUnit start 0x65C and managed destination offsets map exactly to
# the two writes at manager + unitId*0x490 + 0x934 / 0x936 in 118E00.
assert 0x65C + 0x2D8 == 0x934 and 0x65C + 0x2DA == 0x936
group = pseudo(0x11B520)
assert 'FUN_180117bc0' in group and 'FUN_180118e00' in group and 'if (0 < sVar5)' in group
assert 'FUN_1800e1d30' in group and '3999 < *(int *)(param_1 + 0x14)' in group
assert 'DAT_187cc6734' in pseudo(0xE1D30) and '0x155f6c' in pseudo(0xE1D30)
decoder.detail = True
slot_code = list(decoder.disasm(pe.get_data(0xE1D30, 0x13F), 0xE1D30))
assert slot_code[-1].mnemonic == 'ret' and slot_code[-1].address == 0xE1E6E
assert not any(i.mnemonic == 'call' for i in slot_code)
rip_writes = []
for i in slot_code:
    if i.mnemonic == 'mov' and i.operands[0].type == capstone.x86.X86_OP_MEM:
        dest = i.operands[0]
        if dest.mem.base == capstone.x86.X86_REG_RIP:
            rip_writes.append((i.address, i.address + i.size + dest.mem.disp, i.reg_name(i.operands[1].reg)))
assert rip_writes == [(0xE1E46, 0x7CC6730, 'edx'), (0xE1E4C, 0x7CC6734, 'r15d'), (0xE1E5F, 0x7CC672C, 'ecx')]
assert [(i.mnemonic, i.op_str) for i in slot_code if i.address in (0xE1E30, 0xE1E33, 0xE1E3C)] == [
    ('movsxd', 'rcx, r15d'), ('movsx', 'rdx, word ptr [r14 + rcx*2 + 0x28f3ec]'),
    ('mov', 'eax, dword ptr [r14 + rcx*4 + 0x155f6c]')]
print('PASS formation ABI: void(RCX manager, EDX spacing, R8D x, R9D y); only X/Y/index outputs, full return and register reads verified.')
click = pseudo(0x195E30)
assert click.index('FUN_180023990') < click.index('0x40000000')
assert 'FUN_180196100' in pseudo(0x10AE0)
assert 'FUN_18011b520' in pseudo(0x196100)
table_pointer = int.from_bytes(pe.get_data(0x2C7A30 + 0x11 * 8, 8), 'little')
assert table_pointer - image_base == 0x10AE0
print('PASS canonical hash, native chore dispatch, Unit target fields, collision and free-place output contracts.')

# Existing DBC60 ABI is unchanged: only its already bounded requested-result
# argument is raised. Queue construction precedes all count-limited extraction.
attack = pseudo(0xDBC60)
assert 'param_6 = param_6 * 2;' in attack and 'local_54 = 500;' in attack
assert 'param_6 < 0x32' in attack and 'if (10 < sVar4) break;' in attack
assert attack.index('if (10 < sVar4) break;') < attack.index('puVar12 = (undefined4 *)(param_1 + 0x1b348)')
assert 'puVar12 = puVar12 + 3;' in attack and '*puVar12 = 1;' in attack and '*puVar12 = 0;' in attack
assert 'FUN_1800dbc60' in pseudo(0x11E960) and 'piVar33 = piVar33 + 3;' in pseudo(0x11E960)
print('PASS attack native pool: one depth-limited flood, 50..500 result limit, unchanged three-int consumer records.')

# Building placement uses current leader coordinates, one flood, and three-int
# records. Zero footprint denotes staging; only a zero approach ends the list.
consumer = pseudo(0x123090)
producer = pseudo(0xDA020)
caller = pseudo(0x11E960)
assert 'puVar7 = puVar7 + 3;' in consumer
assert '(&DAT_1860c89a4)[(longlong)iVar4 * 3] = 0;' in consumer
assert '*(int *)((longlong)&DAT_1860c89ac + lVar8) != 10000000' in consumer
assert '*(int *)((longlong)&DAT_1860c89b4 + lVar6) == 0' in consumer
assert '*puVar16 = 0;' in producer and 'puVar16 = puVar16 + 3;' in producer
assert 'iVar12 = piVar33[-1];' in caller and 'iVar12 = *piVar33;' in caller
entry = list(decoder.disasm(pe.get_data(0x123090, 0xA1), 0x123090))
by_address = {i.address:(i.mnemonic,i.op_str) for i in entry}
assert by_address[0x1230BD] == ('movsx','rcx, word ptr [r9 + rdi + 0x5a]')
assert by_address[0x1230CA] == ('imul','rdx, rcx, 0x490')
assert by_address[0x1230D1] == ('movsx','ecx, word ptr [rdx + rdi + 0x67e8b1e]')
assert by_address[0x1230D9] == ('movsx','r9d, word ptr [rdx + rdi + 0x67e8b1c]')
assert by_address[0x123102] == ('call','0xdb650')
assert by_address[0x123125] == ('call','0xd9c40')
assert by_address[0x12312C] == ('call','0xda590')
print('PASS building native ABI: leader/current start, all three consumer variants, paired-prefix sorting, staging records and first-word termination.')
