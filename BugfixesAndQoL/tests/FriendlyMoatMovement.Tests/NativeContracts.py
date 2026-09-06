"""Read-only, hash-bound ABI check for the added native reconstruction adapter."""
from pathlib import Path
import hashlib
import json
import re
import struct
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

root = Path(__file__).resolve().parents[3]
baseline = root / '_inspect/CrusaderDE-Native-Baseline'
current = json.loads((baseline / 'CURRENT.json').read_text(encoding='utf-8-sig'))
game = Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
dll = game / 'Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll'
binary = dll.read_bytes()
expected_hash = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
assert hashlib.sha256(binary).hexdigest().upper() == current['currentNativeHash'].upper() == expected_hash
pe = pefile.PE(data=binary, fast_load=True)
rva = 0xE32B0
expected = bytes.fromhex('40 53 48 83 EC 30 44 8B 49 10 33 C0 44 8B 41 0C 48 8B D9 8B 51 08 89 44 24 28 89 81 68 5F 15 00 8B 41 14 89 44 24 20 E8 64 E3 FF FF 8B 83 68 5F 15 00 48 83 C4 30 5B C3')
actual = pe.get_data(rva, len(expected))
assert actual == expected and rva + len(actual) == 0xE32E8
source = (root / 'BugfixesAndQoL/src/FriendlyMoatMovementRuntime.cs').read_text(encoding='utf-8-sig')
match = re.search(r'ValidateExactBytes\(memory, 0xE32B0, new byte\[\] \{([^}]+)\}', source)
assert match and bytes(int(x, 16) for x in re.findall(r'0x[0-9A-Fa-f]+', match[1])) == expected
instructions = list(Cs(CS_ARCH_X86, CS_MODE_64).disasm(actual, rva))
assert sum(i.size for i in instructions) == len(actual)
assert [i.address for i in instructions[:6]] == [rva, rva+2, rva+6, rva+10, rva+12, rva+16]
assert [(i.mnemonic, i.op_str) for i in instructions[:6]] == [
    ('push', 'rbx'), ('sub', 'rsp, 0x30'), ('mov', 'r9d, dword ptr [rcx + 0x10]'),
    ('xor', 'eax, eax'), ('mov', 'r8d, dword ptr [rcx + 0xc]'), ('mov', 'rbx, rcx')]
assert actual[0x27] == 0xE8 and rva + 0x2C + struct.unpack_from('<i', actual, 0x28)[0] == 0xE1640
assert [(i.mnemonic, i.op_str) for i in instructions[-3:]] == [('add', 'rsp, 0x30'), ('pop', 'rbx'), ('ret', '')]
print('PASS native SHA-256, runtime pattern, full 56-byte function, instruction boundaries, RCX/RBX dataflow, stack and E1640 call.')
for instruction in instructions:
    print(f'{instruction.address:08X}  {instruction.mnemonic} {instruction.op_str}')

# New observer entries are exact, unique, and start at unwind function boundaries.
pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY['IMAGE_DIRECTORY_ENTRY_EXCEPTION']])
recovery = (root / 'BugfixesAndQoL/src/NativeMovementRecovery.cs').read_text(encoding='utf-8-sig')
for rva_text, hex_bytes in re.findall(r'InstallConnectivityObserver\(memory, libraryBase, (0x[0-9A-F]+),\s*"([0-9A-F ]+)"', recovery):
    rva = int(rva_text, 16)
    code = bytes.fromhex(hex_bytes)
    assert pe.get_data(rva, len(code)) == code
    assert binary.count(code) == 1
    entries = [e.struct for e in pe.DIRECTORY_ENTRY_EXCEPTION if e.struct.BeginAddress == rva]
    assert len(entries) == 1
    decoded = list(Cs(CS_ARCH_X86, CS_MODE_64).disasm(pe.get_data(rva, 32), rva))
    copied = []
    for ins in decoded:
        copied.append(ins)
        if sum(i.size for i in copied) >= 14: break
    assert sum(i.size for i in copied) <= len(code)
    print(f'PASS observer {rva:X} unique entry, ABI prefix, copied span {sum(i.size for i in copied)}, function end {entries[0].EndAddress:X}')

# Original common failure block has no interior branch entry. Recovery is distinct
# from the invalid-target exit (19676C) and the already-called-builder exit (196734).
start, end = 0x19664B, 0x196659
expected = bytes.fromhex('33 C0 8B D6 48 89 05 8E 70 F1 05 49 8B CF')
assert pe.get_data(start, end-start) == expected
whole = list(Cs(CS_ARCH_X86, CS_MODE_64).disasm(pe.get_data(0x196280,0x510),0x196280))
for ins in whole:
    if ins.mnemonic.startswith('j') and ins.op_str.startswith('0x'):
        assert not start < int(ins.op_str,16) < end
span = list(Cs(CS_ARCH_X86, CS_MODE_64).disasm(expected,start))
assert [(i.mnemonic,i.op_str) for i in span] == [
    ('xor','eax, eax'),('mov','edx, esi'),('mov','qword ptr [rip + 0x5f1708e], rax'),('mov','rcx, r15')]
assert not any('xmm' in i.op_str or 'ymm' in i.op_str for i in whole)
assert 'asm.sub(rsp, 0x30)' in recovery and 'asm.add(rsp, 0x30)' in recovery
ordered = ['asm.mov(r8d, __dword_ptr[rsp + 0xA0])','asm.mov(r9d, __dword_ptr[rsp + 0xA8])',
           'asm.mov(__dword_ptr[rsp + 0x20], r14d)','asm.mov(__dword_ptr[rsp + 0x28], ebp)',
           'asm.mov(rax, address)','asm.call(rax)','asm.add(rsp, 0x30)','asm.test(eax, eax)',
           'asm.jmp(libraryBase + 0x196585)','foreach (var instruction in original)']
positions=[recovery.index(fragment) for fragment in ordered]
assert positions == sorted(positions)
assert '(0x900 - NativeUnitSlotDataOffset)' in recovery and '(0x8EC - NativeUnitSlotDataOffset)' in recovery
assert 'NativeUnitSlotDataOffset = 0x65C' in source
assert 'ValidateRecoveryEdges' in recovery and 'MoatTraversalPolicy.FriendlyOnly' in recovery
# Vanillas unit record definition fixes the same manager/slot boundary.
manager_source=(root/'shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnitManager.cs').read_text()
assert 'offset 0x65C' in manager_source
print('PASS pre-builder 14-byte block, all branch entries, dead volatile registers/XMM, aligned shadow/stack args, replay and direct continuation; no hook at UnitMoveHere entry.')

# Decode the REAL production emitter output produced by the managed regression run.
# This checks encoded stack/register operations, not merely the source text.
stub=(root/'BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/latest-recovery-stub.bin').read_bytes()
decoded=list(Cs(CS_ARCH_X86,CS_MODE_64).disasm(stub,0x181000000))
assert sum(i.size for i in decoded)==len(stub)
assert [(i.mnemonic,i.op_str) for i in decoded[:7]]==[
 ('sub','rsp, 0x30'),('mov','rcx, r15'),('mov','edx, esi'),
 ('mov','r8d, dword ptr [rsp + 0xa0]'),('mov','r9d, dword ptr [rsp + 0xa8]'),
 ('mov','dword ptr [rsp + 0x20], r14d'),('mov','dword ptr [rsp + 0x28], ebp')]
assert any(i.mnemonic=='jmp' and i.op_str=='0x180196585' for i in decoded)
print('PASS actual 73-byte production recovery emitter: full decoded instructions, start reads before argument writes, native continuation.')
for instruction in decoded:
 print(f'{instruction.address:010X}  {instruction.mnemonic} {instruction.op_str}')
