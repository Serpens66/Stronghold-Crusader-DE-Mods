"""Validate every proposed shared hook span against the hash-checked native image."""
import json, hashlib, sqlite3
from pathlib import Path
import pefile, capstone
root = Path(__file__).resolve().parents[2]
baseline = root / '_inspect/CrusaderDE-Native-Baseline'
current = json.loads((baseline/'CURRENT.json').read_text(encoding='utf-8-sig'))
manifest = json.loads((baseline/current['databaseManifest']).read_text(encoding='utf-8-sig'))
native = Path(next(b['sourcePath'] for b in manifest['binaries'] if b['role']=='current-native'))
assert hashlib.sha256(native.read_bytes()).hexdigest().upper() == current['currentNativeHash']
pe = pefile.PE(str(native)); data = pe.get_memory_mapped_image(); base = pe.OPTIONAL_HEADER.ImageBase
decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64); decoder.detail=True
db = sqlite3.connect((baseline/manifest['database']['path']).resolve().as_uri()+'?mode=ro',uri=True)
sites = [(0xD9C40, 0xD9C40, 6, 10), (0xD9C40,0xD9F0C,14,16),
         (0xD9C40,0xD9F1C,14,15), (0xE1640,0xE19D8,14,18), (0xE1640,0xE19F9,14,23)]
result=[]
for function,start,minimum,expected in sites:
    size = db.execute('select size from functions where binary_hash=? and rva=?',
                      (current['currentNativeHash'],f'0x{function:X}')).fetchone()[0]
    instructions=list(decoder.disasm(data[function:function+size],base+function))
    displaced=[]; length=0
    for i in instructions:
        if start <= i.address-base < start+minimum or (displaced and length < minimum):
            displaced.append(i); length+=i.size
        if displaced and length>=minimum: break
    assert displaced[0].address-base == start and length == expected, (hex(start),length)
    # Full containing function, both branch sides; no external predecessor may enter the interior.
    incoming=[]
    for i in instructions:
        if i.group(capstone.CS_GRP_JUMP) or i.group(capstone.CS_GRP_CALL):
            if i.operands[0].type == capstone.x86.X86_OP_IMM:
                target=i.operands[0].imm-base
                if start < target < start+length and not start <= i.address-base < start+length:
                    incoming.append((hex(i.address-base),hex(target)))
    assert not incoming, (hex(start),incoming)
    row={'function':hex(function),'start':hex(start),'end':hex(start+length),
         'minimum':minimum,'displaced':length,'bytes':data[start:start+length].hex(),
         'instructions':[f'{i.address-base:X}: {i.mnemonic} {i.op_str}' for i in displaced],
         'external_interior_predecessors':incoming}
    result.append(row)
# Check direct cross-function branches/calls throughout executable PE sections.
for section in pe.sections:
    if not section.Characteristics & 0x20000000: continue
    start=section.VirtualAddress; end=start+section.Misc_VirtualSize
    decoder.skipdata=True
    for i in decoder.disasm(data[start:end],base+start):
        if i.id == 0 or not (i.group(capstone.CS_GRP_JUMP) or i.group(capstone.CS_GRP_CALL)): continue
        if i.operands[0].type != capstone.x86.X86_OP_IMM: continue
        target=i.operands[0].imm-base; origin=i.address-base
        for row in result:
            a,b=int(row['start'],16),int(row['end'],16)
            assert not (a < target < b and not a <= origin < b), (hex(origin),hex(target))
path=Path(__file__).with_name('hook-spans.json')
path.write_bytes((json.dumps({'hash':current['currentNativeHash'],'sites':result},indent=2)+'\n').replace('\n','\r\n').encode())
print('PASS: five spans; full functions and executable-section direct incoming edges validated.')
print(json.dumps(result,indent=2))
