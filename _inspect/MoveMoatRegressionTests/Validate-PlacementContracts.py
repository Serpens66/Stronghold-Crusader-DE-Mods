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

# Minimum whole-instruction prefixes covering a possible 14-byte entry detour.
# MonoMod relocates the original instructions; there is no custom register clobber.
prefixes = {
    0x118E00: (15, ['mov', 'mov', 'mov']),
    0x181890: (15, ['mov', 'mov', 'push', 'sub']),
    0xF03C0: (17, ['mov', 'push', 'push', 'push', 'sub', 'inc']),
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
click = pseudo(0x195E30)
assert click.index('FUN_180023990') < click.index('0x40000000')
assert 'FUN_180196100' in pseudo(0x10AE0)
assert 'FUN_18011b520' in pseudo(0x196100)
table_pointer = int.from_bytes(pe.get_data(0x2C7A30 + 0x11 * 8, 8), 'little')
assert table_pointer - image_base == 0x10AE0
print('PASS canonical hash, native chore dispatch, Unit target fields, collision and free-place output contracts.')
