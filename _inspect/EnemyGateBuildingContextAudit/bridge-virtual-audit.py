"""Retain full hash-selected bodies and installed-byte decoding for virtual topology."""
from pathlib import Path
import hashlib, json, sqlite3
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

root=Path('_inspect/CrusaderDE-Native-Baseline')
current=json.loads((root/'CURRENT.json').read_text())
manifest=json.loads((root/current['databaseManifest']).read_text())
native=Path(next(b['sourcePath'] for b in manifest['binaries'] if b['role']=='current-native'))
assert hashlib.sha256(native.read_bytes()).hexdigest().upper()==current['currentNativeHash']
db=sqlite3.connect((root/manifest['database']['path']).resolve().as_uri()+'?mode=ro',uri=True)
db.row_factory=sqlite3.Row
pe=pefile.PE(str(native));decoder=Cs(CS_ARCH_X86,CS_MODE_64)
# Entire feature chain, not selected call-site snippets. The companion audit
# additionally validates all 32 existing Absolute entries and their owners.
rvas=[0x64460,0x645C0,0x739C0,0x69850,0x69560,0x59210,0x725A0,0x725E0,
      0xD90D0,0xD86F0,0xD8CE0,0x107160,0xC07C0,0xE49D0,0xC4BF0,0xC5040,
      0xE3B90,0xE2610,0xB47E0,0x6CDD0,0xBACC0,0xBB3D0,0xBD320,
      0xCF360,0xCF400,0x3C2E0,0x3BD50,0x2D250,0xD95E0,0xD9190,
      0x2C480,0x2C5A0,0x3B450,0x3C150,0x3D260,0x11B520,0x117C70,0x117820,
      0xA5E20,0xB9330,0x6C3B0,0xC0270]
evidence=['Full native hash: '+current['currentNativeHash']];contracts=[]
for rva in rvas:
    row=db.execute('select * from functions where binary_hash=? and rva=?',
                   (current['currentNativeHash'],f'0x{rva:X}')).fetchone()
    assert row and row['pseudocode'],hex(rva)
    code=pe.get_data(rva,row['size']);instructions=list(decoder.disasm(code,rva))
    # Ghidra can include padding after the last instruction; retain undecoded
    # trailing bytes explicitly rather than claiming they are instructions.
    decoded=sum(x.size for x in instructions)
    assert instructions and instructions[0].address==rva
    refs=[dict(x) for x in db.execute('select from_rva,source_function,type from xrefs where binary_hash=? and to_rva=?',
                                   (current['currentNativeHash'],f'0x{rva:X}'))]
    contracts.append(dict(rva=rva,size=row['size'],sha256=hashlib.sha256(code).hexdigest(),decodedBytes=decoded,trailingBytes=code[decoded:].hex()))
    evidence.extend([f'RVA 0x{rva:X}, size={row["size"]}, confidence={row["confidence"]}',row['pseudocode'],
                     'References: '+repr(refs),'Installed instructions:\n'+'\n'.join(f'{x.address:X}: {x.bytes.hex()} {x.mnemonic} {x.op_str}' for x in instructions)])
out=Path('_inspect/EnemyGateBuildingContextAudit')
def write(path,text):
    data=text.replace('\r\n','\n').replace('\n','\r\n').encode();path.write_bytes(data);assert path.read_bytes()==data
write(out/'bridge-virtual-evidence.txt','\n\n'.join(evidence))
write(out/'bridge-virtual-contracts.json',json.dumps(dict(nativeHash=current['currentNativeHash'],functions=contracts),indent=2))
print('PASS virtual audit: full hash-selected bodies and installed decoding retained for',len(rvas),'functions')
