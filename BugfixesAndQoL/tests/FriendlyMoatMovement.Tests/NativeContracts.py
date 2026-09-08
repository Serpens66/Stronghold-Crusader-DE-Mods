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

# Vanilla ordinary movement derives a group route mode before E2610. The attack
# builders hard-code zero at all three non-Assassin region checks.
def assert_call(call_rva, target_rva, expected_hex):
    code = pe.get_data(call_rva, 5)
    assert code == bytes.fromhex(expected_hex)
    assert code[0] == 0xE8
    assert call_rva + 5 + struct.unpack_from('<i', code, 1)[0] == target_rva

group_mode_entry = bytes.fromhex(
    '48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 '
    '89 7C 24 20 41 56 48 83 EC 20 48 63 F2 33 DB 4C '
    '69 C6 88 06 00 00 48 8B E9 41 0F BF 7C 08 5C 85 FF '
    '7E 4D 4C 8D 35 56 07 6D 06')
assert pe.get_data(0x117C70, len(group_mode_entry)) == group_mode_entry
assert binary.count(group_mode_entry) == 1
assert_call(0x11B736, 0x117C70, 'E8 35 C5 FF FF')
assert_call(0x11B755, 0xE2610, 'E8 B6 6E FC FF')
assert_call(0x11B768, 0xE9D90, 'E8 23 E6 FC FF')
assert_call(0x11B785, 0xE9FF0, 'E8 66 E8 FC FF')
for call_rva, expected_hex, zero_store in (
        (0xDBF0D, 'E8 FE 66 00 00', '33 C0 45 8B C5 89 44 24 20'),
        (0xDA1F9, 'E8 12 84 00 00', '33 C9 89 4C 24 20'),
        (0xDA47C, 'E8 8F 81 00 00', '33 C9 89 4C 24 20')):
    assert_call(call_rva, 0xE2610, expected_hex)
    zero_bytes = bytes.fromhex(zero_store)
    assert zero_bytes in pe.get_data(call_rva - 24, 24)

# DA020 publishes approach/footprint pairs but deliberately leaves the third
# score field untouched. 123090 later assigns the ground-flood score and removes
# every entry carrying Vanilla's unreachable sentinel.
decoder = Cs(CS_ARCH_X86, CS_MODE_64)
da020 = list(decoder.disasm(pe.get_data(0xDA020, 0x570), 0xDA020))
da020_operands = [instruction.op_str for instruction in da020]
assert 'rdi, 0x1b348' in da020_operands
assert 'dword ptr [rdi - 4], ebx' in da020_operands
assert any(operand.startswith('dword ptr [rdi],') for operand in da020_operands)
assert not any('[rdi + 4]' in operand or '0x1b34c' in operand for operand in da020_operands)
assert any('0x1b344' in operand for operand in da020_operands)
assert any('0x1b348' in operand for operand in da020_operands)

consumer = list(decoder.disasm(pe.get_data(0x123090, 0x250), 0x123090))
consumer_text = [(instruction.mnemonic, instruction.op_str) for instruction in consumer]
for call_rva, target_rva in (
        (0x1230AA, 0x117820),
        (0x123102, 0xDB650),
        (0x123125, 0xD9C40),
        (0x12312C, 0xDA590)):
    instruction = next(item for item in consumer if item.address == call_rva)
    assert instruction.mnemonic == 'call' and int(instruction.op_str, 16) == target_rva
assert ('mov', 'eax, 0x989680') in consumer_text
assert any(instruction.address == 0x123261 and instruction.mnemonic == 'cmp' and
           '0x1b34c' in instruction.op_str and '0x989680' in instruction.op_str
           for instruction in consumer)

# All three building-command continuations iterate the 12-byte records through
# r15, consuming offsets -4 and 0 only. The score at +4 is never read there.
for call_rva, span_length in ((0x11FFA7, 0x3A0), (0x1206DF, 0x500), (0x120CCD, 0x160)):
    continuation = list(decoder.disasm(pe.get_data(call_rva, span_length), call_rva))
    assert continuation[0].mnemonic == 'call' and int(continuation[0].op_str, 16) == 0x123090
    r15_memory = [instruction.op_str for instruction in continuation if '[r15' in instruction.op_str]
    assert any('[r15 - 4]' in operand for operand in r15_memory)
    assert any('dword ptr [r15]' in operand for operand in r15_memory)
    assert not any('[r15 + 4]' in operand for operand in r15_memory)
    assert any(instruction.mnemonic == 'add' and instruction.op_str == 'r15, 0xc'
               for instruction in continuation)

ladder_source = (root / 'BugfixesAndQoL/src/FriendlyMoatMovementRuntime.LadderAttackFix.cs').read_text(encoding='utf-8-sig')
assert 'getTribeMovementMode(nativeTribeManager, tribeId)' in ladder_source
assert re.search(r'originalRegionPairReachability\(\s*pathManager,\s*playerId,\s*sourceRegion,\s*targetRegion,\s*probe\.GroupMovementMode\)', ladder_source)
assert 'effectiveResult = groupModeResult' in ladder_source
assert 'effectiveResult = 1' not in ladder_source
assert 'AddDetour' not in ladder_source and 'HookTarget' not in ladder_source
assert 'TryBuild' not in ladder_source and 'WeightedMoat' not in ladder_source
assert 'originalCursorRegionPrecheck(' not in ladder_source
assert 'originalCursorReachability(' not in ladder_source
assert 'RetryingVanillaBuilder' not in ladder_source
assert ladder_source.count('originalAttackApproachFloodBuilder(') == 2
assert ladder_source.count('originalBuildingApproachBuilder(') == 2
assert ladder_source.count('requirePairedResult: true') == 1
assert ladder_source.count('requireReachableScore: false') == 1
assert 'int leadUnitId = *(ushort*)(tribe + TribeLeadUnitIdOffset);' in ladder_source
assert 'TryGetUnitById(leadUnitId, out GameUnit* unit)' in ladder_source
assert 'leadUnitId + 1' not in ladder_source and 'NativeUnitIndex' not in ladder_source
assert '!requireReachableScore || score != VanillaUnreachableCandidateScore' in source
assert 'restoredCandidate.Score = VanillaUnreachableCandidateScore' in ladder_source
assert 'PositiveLadderRegionTransitions' in ladder_source
assert 'GetTribeMovementModeRva = 0x117C70' in source
assert 'OrdinaryMovementGroupModeCallRva = 0x11B736' in source
sentinel_guard = source.index('scope.UnitId <= 0 ||', source.index('private bool IsBoundUnitAttackFlood'))
sentinel_lookup = source.index('TryGetUnitById(scope.UnitId', sentinel_guard)
assert sentinel_guard < sentinel_lookup
handler = source.index('if (TryHandleVanillaLadderRegionPair(')
moat_extension = source.index('if (TryAllowDigWorkRegionPair(', handler)
assert handler < moat_extension
print('PASS Vanilla group-mode/E2610 flow, DA020 score-free pairs, 123090 ground compaction, score-free attack consumers, exact result propagation, leader Game-ID contract, sentinel guard, and shared-detour integration.')

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
