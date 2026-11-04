"""Validate pinned native sites against the installed PE and baseline xrefs, without loading the game DLL."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import capstone
import pefile

def audit(workspace,game,source_root=None):
    native=game/'Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll'
    current=json.loads((workspace/'_inspect/CrusaderDE-Native-Baseline/CURRENT.json').read_text(encoding='utf-8-sig'))
    source=((source_root or workspace)/'Testmods/DesyncTest/src/NativeHooks.cs').read_text(encoding='utf-8-sig')
    expected=re.search(r'NativeHash="([A-F0-9]+)"',source)[1]
    actual=hashlib.sha256(native.read_bytes()).hexdigest().upper()
    if expected!=actual or current['currentNativeHash']!=actual:raise ValueError('native identity mismatch')
    backend=game/'BepInEx/plugins/000shcdese/RedBird.Backends.NativeX64.dll'
    if hashlib.sha256(backend.read_bytes()).hexdigest().upper()!=re.search(r'BackendHash="([A-F0-9]+)"',source)[1]:raise ValueError('backend changed')
    for name,constant in (('RedBird.X64.dll','InlineBackendHash'),('Iced.dll','AssemblerHash')):
        path=game/'BepInEx/plugins/000shcdese'/name
        if hashlib.sha256(path.read_bytes()).hexdigest().upper()!=re.search(constant+r'="([A-F0-9]+)"',source)[1]:raise ValueError(f'{name} changed')
    pe=pefile.PE(str(native));image=pe.get_memory_mapped_image();base=pe.OPTIONAL_HEADER.ImageBase
    decoder=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_64);decoder.detail=True
    sites=[]
    for name,rva,data in re.findall(r'new Site\("([^"]+)",0x([A-Fa-f0-9]+),"([a-f0-9]+)"\)',source):
        rva=int(rva,16);data=bytes.fromhex(data)
        if image[rva:rva+len(data)]!=data:raise ValueError(f'changed bytes: {name}')
        ins=list(decoder.disasm(data,base+rva))
        if sum(x.size for x in ins)!=len(data):raise ValueError(f'partial instruction: {name}')
        sites.append(dict(name=name,rva=rva,length=len(data),bytes=data.hex(),instructions=[dict(rva=x.address-base,text=x.mnemonic+' '+x.op_str) for x in ins]))
    root=workspace/'_inspect/CrusaderDE-Native-Baseline'
    xrefs=root/actual/'exports/xrefs.jsonl'
    failures=[]
    for line in xrefs.open(encoding='utf-8-sig'):
        ref=json.loads(line)
        if not ref.get('toRva') or not ref.get('fromRva'):continue
        target=int(ref['toRva'],16);origin=int(ref['fromRva'],16)
        for s in sites:
            if s['rva']<target<s['rva']+s['length'] and not s['rva']<=origin<s['rva']+s['length']:
                failures.append(dict(site=s['name'],reference=ref))
    if failures:raise ValueError(f'interior incoming references: {failures}')
    functions=root/actual/'exports/functions.jsonl'
    helper=next(json.loads(line) for line in functions.open(encoding='utf-8-sig') if json.loads(line).get('rva')=='0x78E0')
    raw=image[0x78e0:0x78e0+helper['size']]
    for i in decoder.disasm(raw,base+0x78e0):
        if i.mnemonic=='call' or any(o.type==capstone.x86.X86_OP_MEM and o.mem.base==capstone.x86.X86_REG_RIP for o in i.operands):
            raise ValueError('checksum helper is not position independent')
    # Independently decode all RIP anchors used to establish the four-byte prefix.
    def rip_target(rva):
        i=next(decoder.disasm(image[rva:rva+16],base+rva))
        return i.address+i.size+i.operands[1].mem.disp-base
    hash_start=rip_target(0x220c2);mask=rip_target(0x2209a);wire=rip_target(0x157ae)
    if (hash_start-(wire+0x490),mask-(wire+0x490))!=(0x8c,0x41c):raise ValueError('wire/hash layout changed')
    return dict(nativeHash=actual,backendHash=hashlib.sha256(backend.read_bytes()).hexdigest().upper(),inlineBackendHash=re.search(r'InlineBackendHash="([A-F0-9]+)"',source)[1],assemblerHash=re.search(r'AssemblerHash="([A-F0-9]+)"',source)[1],sites=sites,incomingInteriorReferences=failures,
                checksumHelper=raw.hex(),wireRecordBase=wire,hashStartRelativeWire=0x8c,maskRelativeWire=0x41c,
                limits='Static evidence. Full source audit and runtime host/client acceptance remain separate.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--workspace',type=Path,required=True);p.add_argument('--source-root',type=Path);p.add_argument('--game',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    a=p.parse_args();result=audit(a.workspace,a.game,a.source_root);a.output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print('PASS: native identity, backend, 10 complete spans, incoming edges and wire-prefix anchors')
