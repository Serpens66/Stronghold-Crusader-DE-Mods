import bisect
import hashlib
import json
import pathlib
import sys
import capstone
import pefile

native_path, workspace = map(pathlib.Path, sys.argv[1:])
identity = json.loads((workspace / '_inspect/CrusaderDE-Native-Baseline/CURRENT.json').read_text())
native_hash = hashlib.sha256(native_path.read_bytes()).hexdigest().upper()
assert native_hash == identity['currentNativeHash']
pe = pefile.PE(str(native_path))
start, stop = 0x5DD70, 0x60CAC
entry = bytes.fromhex('48 89 5c 24 20 55')
assert pe.get_data(start, 6) == entry
entries = {e.struct.BeginAddress: e.struct.EndAddress for e in pe.DIRECTORY_ENTRY_EXCEPTION}
assert entries[start] == stop
decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
baseline = workspace / '_inspect/CrusaderDE-Native-Baseline'
refs = baseline / native_hash / 'exports/xrefs.jsonl'
computed = {}
for line in refs.open(encoding='utf-8-sig'):
    row = json.loads(line)
    if not row.get('toRva'):
        continue
    target = int(row['toRva'], 16)
    assert not start < target < start+6, row
    if row.get('fromRva') and row['type'] == 'COMPUTED_JUMP':
        source = int(row['fromRva'], 16)
        if start <= source < stop:
            computed.setdefault(source, set()).add(target)
# Follow actual basic blocks. The PE function extent includes trailing switch data.
queue = [start]
instructions = {}
while queue:
    address = queue.pop()
    while address not in instructions:
        assert start <= address < stop, hex(address)
        ins = next(decoder.disasm(pe.get_data(address, 15), address, count=1), None)
        assert ins is not None
        instructions[address] = ins
        if ins.mnemonic.startswith('j'):
            if ins.op_str.startswith('0x'):
                target = int(ins.op_str, 16)
                assert not start < target < start+6
                if start <= target < stop:
                    queue.append(target)
            else:
                assert address in computed, ('Unresolved indirect jump', hex(address))
                queue.extend(computed[address])
            if ins.mnemonic == 'jmp':
                break
        if ins.mnemonic.startswith('ret') or ins.mnemonic == 'int3':
            break
        address += ins.size
assert [(instructions[a].address, instructions[a].size) for a in (start,start+5,start+6)] == [(start,5),(start+5,1),(start+6,1)]
assert len(computed) == 1 and all(source in instructions for source in computed)
signature = pe.get_data(start, 26)
code = next(s.get_data() for s in pe.sections if s.Name.rstrip(b'\0') == b'.text')
assert code.count(signature) == 1
# The table is generated in BSS, not stored as file bytes: audit its initializer.
semantic = baseline / identity['semanticDirectory'] / 'exports/semantic-decompiled-functions.c'
text = semantic.read_text(encoding='utf-8-sig')
initializer = text.split('/* FUNCTION FUN_1800619c0 ')[1].split('/* FUNCTION ')[0]
assert 'piVar1[-1] = iVar2;' in initializer and '*piVar1 = iVar3;' in initializer
assert 'iVar2 < param_2' in initializer and 'iVar3 < param_2' in initializer
print(f'PASS: hash {native_hash}; function [{start:#x},{stop:#x}), {len(instructions)} decoded instructions; unique signature; no interior xrefs; square footprint producer confirmed.')

# Hash-bound entire accumulator block and final farm checks, freshly disassembled.
acc = list(decoder.disasm(pe.get_data(0x787C7,0x78878-0x787C7),0x787C7))
assert next(i for i in acc if i.address==0x787CE).op_str == 'eax, 0x11'
assert next(i for i in acc if i.address==0x787E9).op_str == 'eax, 0x13'
assert next(i for i in acc if i.address==0x7880A).mnemonic == 'cmovns'
assert next(i for i in acc if i.address==0x78816).op_str == 'eax, 0x14'
assert next(i for i in acc if i.address==0x7881A).op_str == '0x78878'
# Green lives at stack+48: only the 16/128 increments; Foliage only increments coverage at +44.
assert next(i for i in acc if i.address==0x78838).op_str == 'ecx, [rdx + 1]'
assert next(i for i in acc if i.address==0x7884C).op_str == 'r9d, [rcx + 1]'
assert next(i for i in acc if i.address==0x78867).op_str == 'r8b, 1'
assert next(i for i in acc if i.address==0x7886D).op_str == 'ecx'
assert next(i for i in acc if i.address==0x7886F).op_str == 'dword ptr [rsp + 0x48], r9d'
assert next(i for i in acc if i.address==0x78874).op_str == 'dword ptr [rsp + 0x44], ecx'
final = list(decoder.disasm(pe.get_data(0x79898,0x798CF-0x79898),0x79898))
assert final[0].op_str == 'dword ptr [rsp + 0x44], ecx'
assert next(i for i in final if i.address==0x798B4).op_str == 'dword ptr [rsp + 0x48], 0x32'
print('PASS: native resource bits, River exclusion, separate coverage/green increments and final farm thresholds.')
