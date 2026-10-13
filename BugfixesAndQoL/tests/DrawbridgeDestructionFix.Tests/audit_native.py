"""Read-only static audit of the current native image and retained baseline edges."""
import hashlib
import json
import struct
import sys
from pathlib import Path
import capstone
import pefile

root = Path(sys.argv[1])
index = json.loads((root / '_inspect/CrusaderDE-Native-Baseline/CURRENT.json').read_text(encoding='utf-8-sig'))
manifest = json.loads((root / '_inspect/CrusaderDE-Native-Baseline' / index['databaseManifest']).read_text(encoding='utf-8-sig'))
native = next(b for b in manifest['binaries'] if b['role'] == 'current-native')
path = Path(native['sourcePath'])
assert hashlib.sha256(path.read_bytes()).hexdigest().upper() == index['currentNativeHash'] == native['hash']
assert index['currentNativeHash'] == 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
pe = pefile.PE(str(path))
base = pe.OPTIONAL_HEADER.ImageBase
decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
decoder.detail = True
instructions = list(decoder.disasm(pe.get_data(0x7EB00, 0xD8A), base + 0x7EB00))
span = [i for i in instructions if base + 0x7EE6B <= i.address < base + 0x7EE7A]
assert [(i.size, i.mnemonic) for i in span] == [(4, 'cmp'), (6, 'jne'), (5, 'sub')]
assert sum(i.size for i in span) == 15
assert span[1].operands[0].imm == base + 0x7EF87
assert next(i for i in instructions if i.address == base + 0x7EE7A).mnemonic == 'cmp'
for i in instructions:
    if i.group(capstone.CS_GRP_JUMP) or i.group(capstone.CS_GRP_CALL):
        for operand in i.operands:
            assert not (operand.type == capstone.CS_OP_IMM and base + 0x7EE6B < operand.imm < base + 0x7EE7A)
edges = root / '_inspect/CrusaderDE-Native-Baseline' / index['currentNativeHash'] / 'exports/xrefs.jsonl'
for line in edges.read_text(encoding='utf-8-sig').splitlines():
    target = json.loads(line).get('toRva')
    assert not (target and 0x7EE6B < int(target, 16) < 0x7EE7A)
assert struct.unpack('<I', pe.get_data(0x2E7510 + 49 * 4, 4))[0] == 0  # STRUCT_DRAWBRIDGE: default health
assert struct.unpack('<I', pe.get_data(0x2E6710 + 49 * 4, 4))[0] == 0  # STRUCT_DRAWBRIDGE: damage eligibility
assert pe.get_data(0x7EE62, 9).hex() == '664189942f68010000'  # stores signed-16 post-hit HP
assert pe.get_data(0x7F018, 9).hex() == '410fbf942f68010000'  # later reload before lethal decision
assert pe.get_data(0x7F024, 6).hex() == '0f8fd5070000'  # survivors bypass ordinary destruction
print('PASS: current native hash; HP/damage tables; 15-byte instruction span; no incoming interior edges; post-damage writer and later lethal branch.')
