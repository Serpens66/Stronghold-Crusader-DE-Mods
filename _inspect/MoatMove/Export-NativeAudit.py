"""Export hash-bound read-only evidence for the MoatMove optimization audit.

This is an evidence collector, not proof that any exported function was audited.
It never loads the game DLL or writes the baseline/database/game installation.
Run from any directory with the workspace's portable Python (pefile/capstone).
"""
from pathlib import Path
import hashlib
import json
import sqlite3
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

ROOT = Path(__file__).resolve().parents[2]
BASELINE = ROOT / '_inspect/CrusaderDE-Native-Baseline'
CURRENT = json.loads((BASELINE / 'CURRENT.json').read_text(encoding='utf-8-sig'))
MANIFEST = json.loads((BASELINE / CURRENT['databaseManifest']).read_text(encoding='utf-8-sig'))
NATIVE = Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll')
RAW = NATIVE.read_bytes()
HASH = hashlib.sha256(RAW).hexdigest().upper()
assert HASH == CURRENT['currentNativeHash'] == MANIFEST['identities']['currentNativeHash']
DATABASE = (BASELINE / MANIFEST['database']['path']).resolve()
assert DATABASE.is_relative_to(BASELINE.resolve())
DB = sqlite3.connect(DATABASE.as_uri() + '?mode=ro', uri=True)
DB.row_factory = sqlite3.Row
PE = pefile.PE(data=RAW)
DECODER = Cs(CS_ARCH_X86, CS_MODE_64)

# Explicit feature inventory, including producer/consumer and fallback boundaries.
# A row here means "collect evidence", never "behavior verified".
SEEDS = '''
59210 61E70 69560 69D60 6AF60 6C490 6B910
8C5F0 B70C0 C07C0 C0270 C3E50 C4BF0 C5040
D86F0 D90D0 D9C40 DA020 DA590 DAA50 DAAC0 DAFD0 DB650 DBC60
DCD60 DCE60 DE6A0 DF720 E04B0 E0970 E0AC0 E1110 E1640 E1D30
E2610 E2CA0 E2F60 E32B0 E49D0 E4E90 E62D0 E7C40 E7F60 E9D90 E9FF0
F0030 F03C0 F3060 F32B0 F4630 F4930
107160 117820 117BC0 117C70 118310 118E00 119E30 119EE0 119F90
11B520 11E960 123090 1232E0 123460 124740
181890 182B00 1853F0 1855A0 186AD0 18BC00 18BE50 18D460 18E1E0
191C00 195E30 196280 196810 196840 196870 1976C0 197950 1988F0
199CD0 19B1B0 19B260
3E700 110740 13F540 143400 145030 146A70 14AED0 169B70 174F90 178350 17B540 17CF50
639C0 69470 188340 122800 198620 E3B90 DC3C0
B72C0 18DC40 1811A0 198C40 196CF0 187200 1946A0 D8CE0 E0530 E06F0 E0770
11A980 11D7C0 11D8D0 11DD10 196100 1178D0 1243D0 725E0 180230
'''.split()
OUTPUT = ROOT / '_inspect/MoatMove/NativeAudit' / HASH[:8]
OUTPUT.mkdir(parents=True, exist_ok=True)
rows = []
missing = []
for number in sorted({int(seed, 16) for seed in SEEDS}):
    rva = f'0x{number:X}'
    row = DB.execute('SELECT * FROM functions WHERE binary_hash=? AND rva=?', (HASH, rva)).fetchone()
    if row is None:
        missing.append(rva)
        continue
    record = dict(row)
    key = (HASH, rva)
    record['callees'] = [dict(x) for x in DB.execute(
        'SELECT callee_rva,callee_name FROM call_edges WHERE binary_hash=? AND caller_rva=?', key)]
    record['callers'] = [dict(x) for x in DB.execute(
        'SELECT caller_rva FROM call_edges WHERE binary_hash=? AND callee_rva=?', key)]
    code = PE.get_data(number, record['size'])
    record['installedBytesSha256'] = hashlib.sha256(code).hexdigest().upper()
    record['linearDisassembly'] = [
        {'rva': f'0x{inst.address - PE.OPTIONAL_HEADER.ImageBase:X}',
         'bytes': inst.bytes.hex(' ').upper(), 'instruction': f'{inst.mnemonic} {inst.op_str}'.rstrip()}
        for inst in DECODER.disasm(code, PE.OPTIONAL_HEADER.ImageBase + number)]
    record['reviewStatus'] = 'collected-not-reviewed'
    rows.append(record)
assert not missing, f'Not function entries in current baseline: {missing}'
with (OUTPUT / 'functions.jsonl').open('w', encoding='utf-8', newline='\r\n') as stream:
    for row in rows:
        stream.write(json.dumps(row, ensure_ascii=True, separators=(',', ':')) + '\n')
sources = ROOT / 'Testmods/MoatMove/src'
identity = {
    'nativeSha256': HASH,
    'scriptExtenderCommit': MANIFEST['identities']['scriptExtenderCommit'],
    'functionCount': len(rows),
    'evidenceFileSha256': hashlib.sha256((OUTPUT / 'functions.jsonl').read_bytes()).hexdigest().upper(),
    'sourceHashes': {p.name: hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in sorted(sources.glob('*.cs'))},
    'status': 'incomplete-audit; evidence collection does not authorize implementation',
}
with (OUTPUT / 'identity.json').open('w', encoding='utf-8', newline='\r\n') as stream:
    stream.write(json.dumps(identity, indent=2) + '\n')
DB.close()
print(json.dumps({k: identity[k] for k in ('nativeSha256', 'functionCount', 'evidenceFileSha256', 'status')}, indent=2))
