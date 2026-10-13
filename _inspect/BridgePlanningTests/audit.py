"""Full installed native bodies for private-memory planning/raise reference tests."""
from pathlib import Path
import json,sqlite3,hashlib,pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
root=Path('_inspect/CrusaderDE-Native-Baseline');c=json.loads((root/'CURRENT.json').read_text());m=json.loads((root/c['databaseManifest']).read_text())
n=Path(next(x['sourcePath'] for x in m['binaries'] if x['role']=='current-native'));data=n.read_bytes();assert hashlib.sha256(data).hexdigest().upper()==c['currentNativeHash']
p=pefile.PE(data=data);d=sqlite3.connect((root/m['database']['path']).resolve().as_uri()+'?mode=ro',uri=True);d.row_factory=sqlite3.Row;cs=Cs(CS_ARCH_X86,CS_MODE_64)
references=[0x181E00,0x117C70]
known=[0x65040,0x6B910,0x6FC40,0x6FCC0,0xC2300,0xCF1A0,0x10DD90,0x1132A0,0x1132D0,0xCF020,0xDB650,0xE9610,0xE3590,0xE6AE0,0xE7530,0x193C80,0x193D90,0x1126B0,0x1127D0,0x112370,0x1123E0,0x112200,0x112450,0x112190,0x2C480,0x3C2E0,0x10DF60,0x115B10,0x2D250,0x2C5A0,0xCF360,0xD95E0,0xD9190,0xC4BF0,0xD8CE0,0x107160,0xE2610,0x1A64D0,0x7490,0x1A8D20,0xE49D0,0xC5040,0x645C0,0x69850,0x69560,0x725A0,0x725E0,0x6E620,0xD90D0,0xD86F0,0xC07C0,0x5B020,0xE3B90,0xE0530,0xE0770,0xE04B0,0xDE6A0,0xE06F0]
known.extend([0x1A6990,0x61ED0,0x61E70]) # full compiler stack probe and bounded work-target invalidation; failure routines stay guarded
known.extend([0x3C150,0x3B8D0,0x2B2C0]) # complete military follow-up wrappers; inner searches remain guarded
known.extend([0x2FD40,0x3CF20,0x3CAC0,0x3CE40,0x3FE50,0x3CDC0,0x2A6B0,0x3B450,0x30E90,0x3BD50,0x79C0,0x187E60,0x2FC80]) # complete military preamble/formation bodies; unknown side-effect branches stay guarded
known.extend([0x119F90,0x126020]) # complete membership enumeration and entry side-effect reference bodies
known.extend([0x196280,0x19B1B0,0x196840,0xF4930,0xF32B0,0xDAAC0,0xDA590,0xE62D0,0xE1640,0x199CD0]) # full own-command/line/fallback reference bodies
known.extend([0xDA020,0xEAB80,0xEDF30,0xEC880,0x18BE50,0xC0270,0xE2CA0,0x117820,0xD9C40,0xC2280]) # existing structure differential suite, complete bodies
known.extend([0xE4E90]) # complete private packed-direction reversal; E1640 failure repair remains guarded
known.extend([0x3C9C0,0x3B980,0x3BFA0,0x3BB80,0x3CCD0,0x3C0A0,0x3B820,0xEA4E0,0xCF400,0x122B40]) # full phase5 suffix dispatch/formation bodies; unknown unit continuations stay guarded
known.extend([0x7140,0x1A8EE0]) # complete standard memory-copy wrapper/body for own unit entry reference
known.extend([0xF3060]) # complete profile-enabled line search used by196280->F4930
known.extend([0x111330]) # complete reservation selector and retry loop for paired structure consumer
known.extend([0x1946A0]) # full own-state proximity continuation;197950 side effects stay guarded
known.extend([0x11B520,0x117BC0,0xE7C40,0x124740,0x119E30,0x119EE0,0x123460]) # full group formation body and bounded membership/calculated-work consumers;other branches guarded
known.extend([0xE1D30,0xE0970,0xE0AC0]) # full queue-based formation slot assignment bodies
lines=[c['currentNativeHash']];evidence=[str(n),c['currentNativeHash']];guards=set()
for a in known+references:
 row=d.execute('select * from functions where binary_hash=? and rva=?',(c['currentNativeHash'],f'0x{a:X}')).fetchone();assert row and row['pseudocode'],hex(a)
 code=p.get_data(a,row['size']);ins=list(cs.disasm(code,a));assert ins and ins[0].address==a; evidence.append(f'Decoded {sum(i.size for i in ins)}/{len(code)} bytes; remaining raw body={code[sum(i.size for i in ins):].hex()}')
 for i in ([] if a in references else ins):
  if i.mnemonic in ['call','jmp']:
   if i.op_str.startswith('0x'):
    dest=int(i.op_str,16)
    if not a<=dest<a+len(code) and dest not in known: guards.add(dest)
   elif i.mnemonic=='call': raise AssertionError(('unresolved indirect call',hex(a),str(i)))
 if a not in references: lines.append(f'{a:X}\t{row["size"]}\t{hashlib.sha256(code).hexdigest().upper()}')
 evidence.extend([f'{a:X} {row["name"]} {row["size"]}',row['pseudocode'],'\n'.join(f'{i.address:X} {i.bytes.hex()} {i.mnemonic} {i.op_str}' for i in ins)])
lines.extend(f'GUARD\t{g:X}' for g in sorted(guards));evidence.append('Unsupported branch target guards: '+repr([hex(g) for g in sorted(guards)]))
for name,text in [('native-contracts.tsv','\n'.join(lines)),('native-evidence.txt','\n\n'.join(evidence))]:
 out=Path('_inspect/BridgePlanningTests')/name;out.write_bytes(('\n'.join(line.rstrip() for line in text.replace('\r\n','\n').split('\n'))).replace('\n','\r\n').encode())
print('PASS full native audit:',len(known),'installed complete bodies; guarded unsupported branches:',[hex(g) for g in sorted(guards)])
