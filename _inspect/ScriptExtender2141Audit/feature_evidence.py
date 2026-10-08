"""Retain full disassembly and recorded edges of audited feature-path functions."""
from pathlib import Path
import capstone,hashlib,json,pefile,sqlite3
ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).resolve().parent
HASH='FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
native=Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll').read_bytes()
assert hashlib.sha256(native).hexdigest().upper()==HASH
pe=pefile.PE(data=native);base=pe.OPTIONAL_HEADER.ImageBase
db=sqlite3.connect(f'file:{ROOT/"_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/CrusaderDE-semantic.sqlite"}?mode=ro',uri=True)
md=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_64)
rows=[];text=[]
for rva,role in [(0x182B00,'dispatcher'),(0x18C040,'wild distance writer'),(0x18C160,'enemy distance writer'),(0x19A1F0,'distance refresh'),(0x18E9A0,'downstream target/attack checks')]:
    result=db.execute('select size,confidence from functions where binary_hash=? and rva=?',(HASH,f'0x{rva:X}')).fetchone()
    assert result,(hex(rva),'missing full function extent')
    size,confidence=result;body=pe.get_data(rva,size)
    decoded=list(md.disasm(body,base+rva))
    assert sum(i.size for i in decoded)==size
    edges=list(db.execute('select from_rva,to_rva,type from xrefs where binary_hash=? and (to_rva=? or from_rva=?)',(HASH,f'0x{rva:X}',f'0x{rva:X}')))
    rows.append(dict(rva=rva,role=role,size=size,confidence=confidence,bodySha256=hashlib.sha256(body).hexdigest().upper(),recordedEntryEdges=edges))
    text.append(f'\n{role}: RVA 0x{rva:X}, full extent {size}, role confidence {confidence}\n')
    text.extend(f'{i.address-base:08X}  {i.bytes.hex():<32} {i.mnemonic} {i.op_str}\n' for i in decoded)
for name,value in [('feature-path.json',json.dumps(dict(nativeHash=HASH,functions=rows),indent=2)+'\n'),('feature-path.asm',''.join(text))]:
    (OUT/name).write_bytes(value.replace('\n','\r\n').encode())
print('PASS: full dispatcher, three distance writers and downstream target/attack disassembly retained.')
