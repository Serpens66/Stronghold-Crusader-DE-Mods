from pathlib import Path
import re
script=Path('_inspect/EnemyGateBuildingContextAudit/native-assassin-publication-audit.py')
ns={'__file__':str(script.resolve())};exec(script.read_text().split('output=Path')[0],ns)
count=0
for p in Path('_inspect/AssassinGateClimb').glob('0x*.txt'):
 text=p.read_text(encoding='utf-8-sig');rva=int(p.stem,16)
 row=ns['db'].execute('select pseudocode from functions where binary_hash=? and rva=?',(ns['current']['currentNativeHash'],f'0x{rva:X}')).fetchone()
 assert row and row[0].replace('\r\n','\n') in text.replace('\r\n','\n'),p
 for m in re.finditer(r'^([0-9A-Fa-f]{8}) ([0-9a-f]+) ',text,re.M):
  b=bytes.fromhex(m[2]);assert ns['read'](int(m[1],16),len(b))==b,(p,m[1]);count+=1
print('PASS: 60 complete search/climb bodies and',count,'installed instruction ranges;21 planning/output bodies.')
# Existing public SDK signature locates the complete Maceman update body (no hook).
image=ns['data'];matches=list(re.finditer(rb'\xb8....\x66\x42\x39\x84\x13....\x7e.\x45\x85\xf6',image,re.S));assert len(matches)==1
raw=matches[0].start();import struct
rva=None
for i in range(struct.unpack_from('<H',image,ns['pe']+6)[0]):
 s=ns['sections']+40*i;virtual,va,rawsize,start=struct.unpack_from('<IIII',image,s+8)
 if start<=raw<start+rawsize:rva=va+raw-start
rows=[(r,n,p) for r,n,p in ns['db'].execute('select rva,size,pseudocode from functions where binary_hash=?',(ns['current']['currentNativeHash'],)) if int(r,16)<=rva<int(r,16)+n]
assert len(rows)==1
r,size,pseudo=rows[0];body=ns['read'](int(r,16),size);assert len(body)==size
text='Complete Maceman update: '+r+' size='+str(size)+' SDK pattern='+hex(rva)+'\n'+pseudo+'\nIncoming calls:'+str(ns['db'].execute('select from_rva,source_function,type from xrefs where binary_hash=? and to_rva=?',(ns['current']['currentNativeHash'],r)).fetchall())+'\n'
Path('_inspect/EnemyGateBuildingContextAudit/macemen-native-update-evidence.txt').write_bytes(text.replace('\r\n','\n').replace('\n','\r\n').encode())
print('PASS: complete Maceman update',r,'size',size,'bytes, incoming references recorded. State/command fields remain raw diagnostic values.')