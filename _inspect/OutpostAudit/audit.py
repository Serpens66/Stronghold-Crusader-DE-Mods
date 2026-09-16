"""Read-only native inspection; optional evidence export, never patches the game."""
import argparse
import hashlib
import json
import re
import sqlite3
import struct
from pathlib import Path

import capstone
import pefile

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / '_inspect/CrusaderDE-Native-Baseline'
CURRENT = json.loads((BASE / 'CURRENT.json').read_text(encoding='utf-8-sig'))
SEM = BASE / CURRENT['semanticDirectory']
DLL = Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll')
HASH = hashlib.sha256(DLL.read_bytes()).hexdigest().upper()
assert HASH == CURRENT['currentNativeHash']
MANIFEST = json.loads((BASE / CURRENT['databaseManifest']).read_text(encoding='utf-8-sig'))
assert HASH == MANIFEST['identities']['currentNativeHash']
DB = sqlite3.connect((BASE / MANIFEST['database']['path']).as_uri() + '?mode=ro', uri=True)
DB.row_factory = sqlite3.Row
PE = pefile.PE(str(DLL))
TEXT = (SEM / 'exports/semantic-decompiled-functions.c').read_text(encoding='utf-8-sig')

def function(rva):
    key = f'0x{rva:X}'
    row = DB.execute('select * from functions where binary_hash=? and rva=?', (HASH, key)).fetchone()
    assert row is not None, key
    m = re.search(r'/\* FUNCTION [^\n]*RVA=' + key + r' \*/', TEXT)
    end = TEXT.find('/* FUNCTION', m.end()) if m else -1
    pseudo = TEXT[m.start():end if end >= 0 else None] if m else 'No baseline pseudocode.\n'
    dis = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    # Ghidra size sums body bytes; it is not necessarily a contiguous extent.
    # Outpost code ends at the audited RET at ACDE7, before its two jump tables.
    length = 0xACDE8-rva if rva == 0xABB90 else row['size']
    asm = '\n'.join(f'{i.address:08X}: {i.mnemonic} {i.op_str}' for i in dis.disasm(PE.get_data(rva, length), rva))
    return dict(binaryHash=HASH, rva=key, va=f'0x{PE.OPTIONAL_HEADER.ImageBase+rva:X}',
                baselineConfidence=row['confidence'], size=row['size'],
                disassemblyCoverage='full audited code extent, excluding jump tables' if rva == 0xABB90 else 'linear diagnostic window of baseline body size; not a function-boundary claim',
                callers=[dict(x) for x in DB.execute('select caller_rva from call_edges where binary_hash=? and callee_rva=?', (HASH,key))],
                callees=[dict(x) for x in DB.execute('select callee_rva,callee_name from call_edges where binary_hash=? and caller_rva=?', (HASH,key))],
                pseudocode=pseudo, disassembly=asm)

def write_text(path, text):
    text = text.replace('\r\n', '\n').replace('\r', '\n').replace('\n', '\r\n')
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(text.encode('utf-8'))
    assert path.read_bytes() == text.encode('utf-8')

parser = argparse.ArgumentParser()
parser.add_argument('rvas', nargs='*', type=lambda s: int(s, 16))
parser.add_argument('--export', action='store_true')
args = parser.parse_args()
for rva in args.rvas:
    result = function(rva)
    if args.export:
        target = ROOT / '_inspect/OutpostAudit' / HASH[:8] / f'{rva:X}.json'
        write_text(target, json.dumps(result, indent=2, ensure_ascii=False) + '\n')
        print(f'{result["rva"]}: {result["size"]} bytes, exported {target.name}')
    else:
        print(result['pseudocode'])
if args.export:
    table = [dict(index=i, values=list(struct.unpack('<13i', PE.get_data(0x2DD880+i*52,52)))) for i in range(24)]
    write_text(ROOT / '_inspect/OutpostAudit' / HASH[:8] / 'profiles.json', json.dumps(dict(binaryHash=HASH, tableRva='0x2DD880', stride=52, profiles=table), indent=2)+'\n')
