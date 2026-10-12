"""Complete current-hash structure approach audit; read-only installed native input."""
from pathlib import Path
import runpy,hashlib,json
ctx=runpy.run_path('_inspect/EnemyGateBuildingContextAudit/bridge-decision-audit.py')
rvas=[0x2C620,0x2C710,0x199C00,0x1140C0,0x113AB0,0x11A980,0x113BC0,0x122B40,0x117820,0x196280,0x3B450,0x30E90,0x3BD50,0xEAB80,0xEC880,0x11D8D0,0xEDF30,0x199CD0,0x19B1B0,0x11C650,0x1946A0,0x181E30]
evidence=['Native SHA256: '+ctx['index']['currentNativeHash'],'No new observer, search or behavior patch.']
for rva in rvas:
 row=ctx['db'].execute('select * from functions where binary_hash=? and rva=?',(ctx['index']['currentNativeHash'],f'0x{rva:X}')).fetchone()
 assert row and row['pseudocode']
 body=ctx['pe'].get_data(rva,row['size']);instructions=list(ctx['decoder'].disasm(body,rva));assert len(body)==row['size'] and sum(i.size for i in instructions)==len(body)
 evidence.extend([f'RVA 0x{rva:X}, bytes {len(body)}, SHA256 {hashlib.sha256(body).hexdigest().upper()}',row['pseudocode'],'\n'.join(f'{i.address:X}: {i.mnemonic} {i.op_str}' for i in instructions)])
ctx['write'](Path('_inspect/EnemyGateBuildingContextAudit/bridge-melee-evidence.txt'),'\n\n'.join(evidence))
print('PASS: 22 complete current native structure/approach bodies; existing observers only')
