"""Read-only installed-hash and full-baseline incoming-edge audit for the four inline spans."""
from pathlib import Path
import hashlib, json, os, sqlite3, pefile, capstone
workspace = Path(__file__).resolve().parents[2]
source = workspace / '_inspect/CrusaderDE-Native-Baseline'
# Branch build copies lack the untracked database; their driver passes the local baseline read-only.
base = Path(os.environ.get('SHCDE_NATIVE_BASELINE_ROOT') or source)
current = json.loads((base / 'CURRENT.json').read_text())
if base.resolve() != source.resolve():
    tracked = json.loads((source / 'CURRENT.json').read_text())
    assert (tracked['currentNativeHash'], tracked['databaseManifest']) == (current['currentNativeHash'], current['databaseManifest']), \
        'shared baseline does not identify the dataset of the source'
manifest = json.loads((base / current['databaseManifest']).read_text())
game = Path(os.environ.get('SHCDE_GAME_DIR') or r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
dll = game / 'Stronghold Crusader Definitive Edition_Data' / 'Plugins' / 'x86_64' / 'CrusaderDE.dll'
assert hashlib.sha256(dll.read_bytes()).hexdigest().upper() == current['currentNativeHash'] == manifest['identities']['currentNativeHash']
db = sqlite3.connect((base / manifest['database']['path']).as_uri() + '?mode=ro', uri=True)
pe = pefile.PE(str(dll)); decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64); decoder.detail = True; decoder.skipdata = True
spans = [(0x105593,14,0x105510),(0x8CAF8,14,0x8C5F0),(0x8CE20,18,0x8C5F0),(0x105627,14,0x105510)]
refs = list(db.execute('select from_rva,to_rva,type from xrefs where binary_hash=?',(current['currentNativeHash'],)))
for start,length,parent in spans:
    row = db.execute('select size from functions where binary_hash=? and rva=?',(current['currentNativeHash'],hex(parent).upper().replace('0X','0x'))).fetchone()
    assert row, hex(parent)
    instructions = list(decoder.disasm(pe.get_data(parent,row[0]),0x180000000+parent))
    assert sum(i.size for i in instructions) == row[0], 'complete parent decode'
    boundaries = {i.address-0x180000000 for i in instructions}
    assert start in boundaries and start+length in boundaries
    for source,target,kind in refs:
        if target is None: continue
        if start < int(target,16) < start+length:
            assert kind in ('FALL_THROUGH',), (hex(start),source,target,kind)
    print('PASS span',hex(start),length,'complete parent',hex(parent),row[0],'baseline xrefs checked')
# Actor reads are isolated to the actual power branch; no native global-player writes are introduced.
for start,size in [(0x105510,326),(0x897F0,119)]:
    body=pe.get_data(start,size)
    print('CONTRACT',hex(start),size,hashlib.sha256(body).hexdigest().upper())
