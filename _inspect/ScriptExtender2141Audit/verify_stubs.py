"""Interpret the actual assembled prefix, fail closed on unsupported instructions.

This is a bounded offline machine-contract test, not a gameplay emulator.
"""
from pathlib import Path
import capstone
import itertools
import json

OUT = Path(__file__).resolve().parent
MASK = (1<<64)-1
FLAG_MASK = 1|4|16|64|128|2048
registers = ['rax','rcx','rdx','rbx','rsp','rbp','rsi','rdi'] + [f'r{i}' for i in range(8,16)]

def reginfo(name):
    aliases = {'ax':('rax',16),'cx':('rcx',16),'dx':('rdx',16),'bx':('rbx',16),
        'eax':('rax',32),'ecx':('rcx',32),'edx':('rdx',32),'ebx':('rbx',32),
        'esi':('rsi',32),'edi':('rdi',32),'esp':('rsp',32),'ebp':('rbp',32)}
    if name in aliases: return aliases[name]
    if name.startswith('r') and name[-1:] in ('w','d'): return name[:-1],16 if name[-1]=='w' else 32
    assert name in registers,name
    return name,64

def cmpflags(a,b,bits):
    mask=(1<<bits)-1; a&=mask; b&=mask; result=(a-b)&mask; sign=1<<(bits-1)
    return (int(a<b) | (int((result&255).bit_count()%2==0)<<2) |
        (int(bool((a^b^result)&16))<<4) | (int(result==0)<<6) |
        (int(bool(result&sign))<<7) | (int(bool((a^b)&(a^result)&sign))<<11))

def verify(stub, distance, threshold, override, active, cached, initial_flags):
    md=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_64);md.detail=True
    decoded=list(md.disasm(bytes.fromhex(stub['stub']),stub['stubAddress']))
    assert decoded and decoded[0].mnemonic=='cmp'
    instructions={i.address:i for i in decoded}
    r={name:0x123400000000+i*0x10000 for i,name in enumerate(registers)}
    r['rsp']=0x50000000
    operand,_=reginfo(stub['operand']); r[operand]=(r[operand]&~65535)|(threshold&65535)
    original=r.copy(); memory={}; flags=initial_flags; pc=stub['stubAddress']; writes=[]
    def address(op,ins):
        m=op.mem
        base=ins.address+ins.size if m.base==capstone.x86.X86_REG_RIP else r[ins.reg_name(m.base)] if m.base else 0
        index=r[ins.reg_name(m.index)] if m.index else 0
        return (base+index*m.scale+m.disp)&MASK
    def load(at,size): return sum(memory.get(at+i,0)<<(i*8) for i in range(size))
    def store(at,size,value):
        for i in range(size):memory[at+i]=(value>>(i*8))&255
    def get(op,ins):
        if op.type==capstone.x86.X86_OP_IMM:return op.imm
        if op.type==capstone.x86.X86_OP_MEM:return load(address(op,ins),op.size)
        name,bits=reginfo(ins.reg_name(op.reg));return r[name]&((1<<bits)-1)
    def put(op,ins,value):
        if op.type==capstone.x86.X86_OP_MEM:
            at=address(op,ins);store(at,op.size,value);writes.append((at,op.size));return
        name,bits=reginfo(ins.reg_name(op.reg));value&=(1<<bits)-1
        r[name]=value if bits>=32 else (r[name]&~((1<<bits)-1))|value
    distance_address=address(decoded[0].operands[0],decoded[0]);store(distance_address,2,distance)
    store(stub['context'],4,3);store(stub['manager']+3*0x490+0x6e6,2,22)
    store(stub['defaults']+44,2,123 if cached else 0)
    store(stub['custom']+44,2,override);store(stub['flags']+22,1,int(active))
    for step in range(100):
        ins=instructions[pc];ops=ins.operands;next_pc=pc+ins.size;m=ins.mnemonic
        if ins.group(capstone.CS_GRP_JUMP) and m!='jmp' and r['rsp']==original['rsp']:
            assert r==original,(hex(stub['rva']),m,'registers',r,original)
            expected=cmpflags(distance,override if active else threshold,16)
            assert flags&FLAG_MASK==expected,(hex(stub['rva']),'flags',flags,expected)
            assert load(stub['defaults']+44,2)==(123 if cached else threshold&65535)
            allowed={stub['defaults']+44}
            assert all(at in allowed or original['rsp']-64<=at<original['rsp'] for at,size in writes)
            return
        if m=='push' or m=='pushfq':
            value=flags if m=='pushfq' else get(ops[0],ins);r['rsp']-=8;store(r['rsp'],8,value);writes.append((r['rsp'],8))
        elif m=='pop' or m=='popfq':
            value=load(r['rsp'],8);r['rsp']+=8
            if m=='popfq':flags=value
            else:put(ops[0],ins,value)
        elif m in ('mov','movabs'):put(ops[0],ins,get(ops[1],ins))
        elif m=='movsx':
            value=get(ops[1],ins);bits=ops[1].size*8
            if value&(1<<(bits-1)):value-=1<<bits
            put(ops[0],ins,value)
        elif m=='lea':put(ops[0],ins,address(ops[1],ins))
        elif m=='imul':put(ops[0],ins,get(ops[1],ins)*get(ops[2],ins));flags&=~2049
        elif m=='add':put(ops[0],ins,get(ops[0],ins)+get(ops[1],ins));flags=2
        elif m=='cmp':flags=(flags&~FLAG_MASK)|cmpflags(get(ops[0],ins),get(ops[1],ins),ops[0].size*8)
        elif m in ('je','jne','jmp'):
            take=m=='jmp' or (bool(flags&64)==(m=='je'))
            if take:next_pc=get(ops[0],ins)
        else:raise AssertionError((m,ins.op_str))
        assert next_pc in instructions,(m,hex(next_pc))
        pc=next_pc
    raise AssertionError('Prefix did not reach the preserved Vanilla branch')

stubs=json.loads((OUT/'stubs.json').read_text())
cases=0
for stub in stubs:
    assert stub['redbird']=='1.5.0.0'
    emitted=bytes.fromhex(stub['stub'])
    assert emitted[-14:-8]==bytes.fromhex('ff2500000000')
    assert int.from_bytes(emitted[-8:],'little')==stub['target']+stub['length']
    decoder=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_64)
    assert sum(ins.size for ins in decoder.disasm(emitted[:-8],stub['stubAddress']))==len(emitted)-8
    for distance,threshold,override,active,cached,flags in itertools.product(
        [-32768,-1,0,399,400,401,32767],[400,432,680],[-32768,-1,0,400,681,32767],
        [False,True],[False,True],[2,0xAD7]):
        verify(stub,distance,threshold,override,active,cached,flags);cases+=1
result=dict(stubCount=len(stubs),scenarioCount=cases,registersPreserved=True,
    stackBalanced=True,compareFlagsCorrect=True,cacheUsesOriginalOperand=True,
    fullExecutableStubDecoded=True,trampolineContinuationVerified=True,
    scope='Actual Iced-emitted prefix until first restored Vanilla conditional branch; no gameplay execution')
(OUT/'stub-verification.json').write_bytes((json.dumps(result,indent=2)+'\n').replace('\n','\r\n').encode())
print(f'PASS: {cases} actual-stub prefix scenarios; all GPRs, stack, signed comparison flags and original-value cache preserved.')
