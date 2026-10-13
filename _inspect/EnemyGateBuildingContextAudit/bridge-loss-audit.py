"""Read complete feature bodies and installed bytes; retain reproducible evidence."""
from pathlib import Path
import hashlib, json, re

scope = {}
exec(Path('_inspect/EnemyGateBuildingContextAudit/bridge-decision-audit.py').read_text(encoding='utf-8-sig').split('def write(')[0], scope)
db, pe, native_hash = scope['db'], scope['pe'], scope['index']['currentNativeHash']
roots = [0x7EB00,0xC4290,0xC43A0,0xC60F0,0xB8310,0xB8460,0x61FC0,
         0x622D0,0x61E70,0x69560,0x69850,0x61EF0,0x69610,0x725E0,
         0xB82D0,0xB5C40,0xC7280,0xC7260,0xBCEB0,0xCCD20,0xCFE90,
         0xD95A0,0x739C0,0xE49D0,0xC4BF0,0xC5040,0xE3B90,
         0x146A70,0x196280,0xDA020,0x123090,0x11A980,0xEDF30,
         0x3C2E0,0x2D250,0x2C480,0x122B40,
         0x3CF20,0x126020,0x119F90,0x2FC80,0x2FD40,0x187E60,
         0x19B1B0,0x196840,0xF4930,0xF32B0,0xE1640,0x18BE50,0x199CD0,0xE62D0,0x7140,0x7490]
# Every damage caller is inspected, not only the presumed maceman path.
callers = db.execute('select distinct source_function from xrefs where binary_hash=? and to_rva=?', (native_hash,'0x7EB00')).fetchall()
for caller in callers:
    if caller[0] and re.fullmatch(r'FUN_18[0-9a-fA-F]{7}', caller[0]):
        roots.append(int(caller[0][4:],16)-0x180000000)
bodies=[]; contracts=[]
for rva in dict.fromkeys(roots):
    row=db.execute('select * from functions where binary_hash=? and rva=?',(native_hash,f'0x{rva:X}')).fetchone()
    assert row and row['pseudocode'], hex(rva)
    data=pe.get_data(rva,row['size']); assert len(data)==row['size']
    instructions=list(scope['decoder'].disasm(data,rva))
    contracts.append(dict(rva=f'0x{rva:X}',size=row['size'],sha256=hashlib.sha256(data).hexdigest().upper(),decodedBytes=sum(i.size for i in instructions)))
    bodies.append(row['pseudocode']+'\nINSTALLED INSTRUCTIONS\n'+'\n'.join(f'{i.address:X}: {i.mnemonic} {i.op_str}' for i in instructions))
out=Path('_inspect/EnemyGateBuildingContextAudit')
for name,text in [('bridge-loss-full-evidence.txt','Native SHA256 '+native_hash+'\n\n'+'\n\n'.join(bodies)),('bridge-loss-contracts.json',json.dumps(contracts,indent=2))]:
    (out/name).write_bytes(text.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
print('PASS complete planning audit122 plus destruction/attack bodies:',len(contracts),'installed hash',native_hash)
