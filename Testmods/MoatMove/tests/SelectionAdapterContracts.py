"""Hash-bound native spans and independent execution model of actual emitted bytes."""
import hashlib
import json
import re
import sqlite3
from pathlib import Path
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import X86_OP_REG, X86_OP_IMM, X86_OP_MEM

root = Path(__file__).resolve().parents[3]
mod = root / 'Testmods/MoatMove'
baseline = root / '_inspect/CrusaderDE-Native-Baseline'
current = json.loads((baseline / 'CURRENT.json').read_text())
manifest = json.loads((baseline / current['databaseManifest']).read_text())
binary = Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll').read_bytes()
assert hashlib.sha256(binary).hexdigest().upper() == current['currentNativeHash'] == manifest['identities']['currentNativeHash']
assert manifest['identities']['scriptExtenderCommit'] == '2cee24e33b5a5d81d1c275efabc714ac59917b7b'
pe = pefile.PE(data=binary)
dis = Cs(CS_ARCH_X86, CS_MODE_64)
dis.detail = True
source = (mod / 'src/AssassinSelectionAdapters.cs').read_text()
rvas = [int(x, 16) for x in re.search(r'SelectionCallRvas = \{([^}]+)', source)[1].split(',')]
spans = [bytes.fromhex(x) for x in re.findall(r'"([0-9A-F]{28,})"', source)]
assert list(map(len, spans)) == [17, 14, 17, 18, 15, 14]
db = sqlite3.connect((baseline / manifest['database']['path']).resolve().as_uri() + '?mode=ro', uri=True)
for origin, target in db.execute('select from_rva,to_rva from xrefs where binary_hash=?', (current['currentNativeHash'],)):
    if not origin or not target: continue
    a, b = int(origin, 16), int(target, 16)
    for start, span in zip(rvas, spans):
        assert not (start < b < start+len(span) and not start <= a < start+len(span)), ('external entry into displaced span', hex(a), hex(b))
db.close()
runtime = (mod / 'src/FriendlyMoatMovementRuntime.cs').read_text()
assert 'pendingCursorMode' not in runtime and 'CursorTilePairFallbackSelectionDelegate' not in runtime
assert 'originalCursorTilePairFallbackSelection' not in runtime
assert 'ValidateSelectionCallAdapters(libraryBase)' in runtime
assert 'DisplacedByteCount' in source and 'returnAddress !=' in source
lib, stub, mask = 0x180000000, 0x181000000, (1 << 64) - 1
volatile = ['rax', 'rcx', 'rdx', 'r8', 'r9', 'r10', 'r11']
registers = volatile + ['rbx', 'rbp', 'rsi', 'rdi', 'rsp', 'r12', 'r13', 'r14', 'r15'] + ['xmm'+str(i) for i in range(16)]

def full(name):
    if name in ['eax', 'ecx', 'edx', 'ebx', 'ebp', 'esi', 'edi', 'esp']:
        return 'r' + name[1:]
    return name[:-1] if name.startswith('r') and name.endswith('d') else name

class Machine:
    def __init__(self, seed, result, decision):
        self.reg = {r: (seed + n + 1) * 0x10204080 for n, r in enumerate(registers)}
        self.reg['rsp'] = 0x100000
        self.initial_selection = self.reg['rcx']
        self.memory, self.flags, self.calls = {}, 0x246, []
        self.result, self.decision = result, decision
    def address(self, op, ins):
        m = op.mem
        base = ins.address + ins.size if ins.reg_name(m.base) == 'rip' else self.reg.get(ins.reg_name(m.base), 0)
        return base + self.reg.get(ins.reg_name(m.index), 0) * m.scale + m.disp
    def read(self, op, ins):
        if op.type == X86_OP_IMM: return op.imm & mask
        if op.type == X86_OP_REG: return self.reg[full(ins.reg_name(op.reg))] & ((1 << (op.size*8))-1)
        a = self.address(op, ins)
        return sum(self.memory.get(a+j, ((a+j)*17+19)&255) << (8*j) for j in range(op.size))
    def write(self, op, ins, value):
        value &= (1 << (op.size*8))-1
        if op.type == X86_OP_REG: self.reg[full(ins.reg_name(op.reg))] = value
        else:
            a = self.address(op, ins)
            for j in range(op.size): self.memory[a+j] = (value >> (8*j)) & 255
    def push(self, value):
        self.reg['rsp'] -= 8
        for j in range(8): self.memory[self.reg['rsp']+j] = (value >> (8*j)) & 255
    def pop(self):
        value = sum(self.memory[self.reg['rsp']+j] << (8*j) for j in range(8))
        self.reg['rsp'] += 8
        return value
    def clobber(self, salt):
        for n, r in enumerate(volatile + ['xmm'+str(i) for i in range(6)]): self.reg[r] = salt+n
        self.flags = 0x202 | (salt & 0x40)
    def run(self, instructions):
        for ins in instructions:
            op, m = ins.operands, ins.mnemonic
            if m == 'call':
                target = self.read(op[0], ins)
                assert self.reg['rsp'] % 16 == 0, 'Win64 call stack alignment'
                if target == lib+0x196870:
                    assert self.reg['rcx'] == self.initial_selection
                    self.clobber(0x11223000); self.reg['rax'] = self.result & mask
                else:
                    assert target == 0x123456789ABCDEF0
                    assert self.reg['rcx'] == self.initial_selection and self.reg['rdx'] == self.result & mask
                    self.clobber(0x55667000); self.reg['rax'] = self.decision & mask
                # Both callees are allowed to use their full home area.
                for j in range(32): self.memory[self.reg['rsp']+j] = j ^ 0xA5
                self.calls.append(target)
            elif m in ('mov', 'movabs', 'movdqu'): self.write(op[0], ins, self.read(op[1], ins))
            elif m == 'lea': self.write(op[0], ins, self.address(op[1], ins))
            elif m == 'sub': self.write(op[0], ins, self.read(op[0], ins)-self.read(op[1], ins)); self.flags = 0x202
            elif m == 'pushfq': self.push(self.flags)
            elif m == 'popfq': self.flags = self.pop()
            elif m == 'push': self.push(self.read(op[0], ins))
            elif m == 'pop': self.write(op[0], ins, self.pop())
            elif m in ('test', 'xor'):
                value = self.read(op[0], ins) & self.read(op[1], ins) if m == 'test' else self.read(op[0], ins) ^ self.read(op[1], ins)
                if m == 'xor': self.write(op[0], ins, value)
                self.flags = 0x202 | (0x40 if value == 0 else 0)
            elif m in ('je', 'jne'):
                if bool(self.flags & 0x40) == (m == 'je'): return op[0].imm
            else: raise AssertionError((hex(ins.address), m, ins.op_str))
        return None

checks = 0
for rva, span in zip(rvas, spans):
    assert pe.get_data(rva, len(span)) == span
    original = list(dis.disasm(span, lib+rva))
    assert sum(i.size for i in original) == len(span) and original[0].mnemonic == 'call'
    assert original[0].operands[0].imm == lib+0x196870
    emitted = list(dis.disasm((root / f'_inspect/MoatMove/se26-selection-{rva:X}.bin').read_bytes(), stub))
    for seed in range(20):
        for result, decision in [(0, 0), (0, 1), (1, 1), (7, 7), (0x100000001, 0x100000001), (-1, -1)]:
            actual, expected = Machine(seed, result, decision), Machine(seed, result, decision)
            expected.run(original[:1]); expected.reg['rax'] = decision & mask
            expected_exit = expected.run(original[1:]); actual_exit = actual.run(emitted)
            assert actual_exit == expected_exit and actual.reg == expected.reg and actual.flags == expected.flags, (hex(rva), result, actual.reg, expected.reg)
            assert actual.calls == [lib+0x196870, 0x123456789ABCDEF0]
            checks += 1
print(f'PASS: {checks} actual adapter machine-state comparisons; stack, shadow space, GPRs, XMMs, flags, calls and both branch outcomes.')
