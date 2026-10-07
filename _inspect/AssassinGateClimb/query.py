"""Hash-checked read-only native audit; preserve complete per-function evidence."""
import sys, json, sqlite3, hashlib
from pathlib import Path
import pefile, capstone
root = Path(__file__).resolve().parents[2]
baseline = root / '_inspect/CrusaderDE-Native-Baseline'
current = json.loads((baseline / 'CURRENT.json').read_text(encoding='utf-8-sig'))
manifest = json.loads((baseline / current['databaseManifest']).read_text(encoding='utf-8-sig'))
native = Path(next(x['sourcePath'] for x in manifest['binaries'] if x['role']=='current-native'))
assert hashlib.sha256(native.read_bytes()).hexdigest().upper() == current['currentNativeHash'] == manifest['identities']['currentNativeHash']
db = sqlite3.connect((baseline / manifest['database']['path']).resolve().as_uri()+'?mode=ro', uri=True)
pe = pefile.PE(str(native)); data = pe.get_memory_mapped_image(); base = pe.OPTIONAL_HEADER.ImageBase
decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
for arg in sys.argv[1:]:
    rva = f'0x{int(arg,16):X}'
    row = db.execute('select name,size,confidence,pseudocode from functions where binary_hash=? and rva=?', (current['currentNativeHash'],rva)).fetchone()
    if not row: print('Missing',rva); continue
    calls = db.execute('select callee_rva from call_edges where binary_hash=? and caller_rva=?',(current['currentNativeHash'],rva)).fetchall()
    callers = db.execute('select caller_rva from call_edges where binary_hash=? and callee_rva=?',(current['currentNativeHash'],rva)).fetchall()
    asm = '\n'.join(f'{i.address-base:08X} {i.bytes.hex()} {i.mnemonic} {i.op_str}' for i in decoder.disasm(data[int(arg,16):int(arg,16)+row[1]],base+int(arg,16)))
    text = f"Hash {current['currentNativeHash']}\n{row[0]} {rva} size={row[1]} confidence={row[2]}\nCallers {callers}\nCallees {calls}\n{row[3]}\nASM:\n{asm}\n"
    path=Path(__file__).parent/(rva+'.txt'); path.write_bytes(text.replace('\r\n','\n').replace('\n','\r\n').encode())
    print(text.split('ASM:')[0])
