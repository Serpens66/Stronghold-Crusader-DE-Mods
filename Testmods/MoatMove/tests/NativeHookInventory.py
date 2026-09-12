"""Read-only complete copied-hook inventory; no game process or DLL writes."""
from pathlib import Path
import hashlib
import json
import re
import sqlite3
import struct
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

root = Path(__file__).resolve().parents[3]
mod = root / 'Testmods/MoatMove'
sources = {p.name: p.read_text(encoding='utf-8-sig') for p in (mod / 'src').glob('*.cs')}
source = '\n'.join(sources.values())
baseline = root / '_inspect/CrusaderDE-Native-Baseline'
current = json.loads((baseline / 'CURRENT.json').read_text(encoding='utf-8-sig'))
manifest = json.loads((baseline / current['databaseManifest']).read_text(encoding='utf-8-sig'))
native = Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll')
raw = native.read_bytes()
assert hashlib.sha256(raw).hexdigest().upper() == current['currentNativeHash'] == manifest['identities']['currentNativeHash']
pe = pefile.PE(data=raw)
db = sqlite3.connect((baseline / manifest['database']['path']).resolve().as_uri() + '?mode=ro', uri=True)
decoder = Cs(CS_ARCH_X86, CS_MODE_64)
constants = {n: int(v, 16) for n, v in re.findall(r'const\s+(?:int|uint|ulong)\s+(\w+)\s*=\s*(0x[0-9A-Fa-f]+)', source)}
arrays = {name: [int(v, 0) for v in re.findall(r'0x[0-9A-Fa-f]+|\b\d+\b', body)]
          for name, body in re.findall(r'(?:int|byte)\[\]\s+(\w+)\s*=\s*(?:new byte\[\]\s*)?\{([^}]+)\}', source)}
patterns = {name: ''.join(re.findall(r'"([^"]*)"', body)) for name, body in re.findall(r'const string (\w+)\s*=\s*(.*?);', source, re.S)}
sections = [s for s in pe.sections if s.Characteristics & 0x20000000]

def executable(rva):
    return any(s.VirtualAddress <= rva < s.VirtualAddress + s.Misc_VirtualSize for s in sections)

ambiguous_fallbacks = []

def unique_pattern(rva, pattern, name):
    tokens = pattern.split()
    regex = b''.join(b'.' if token == '??' else re.escape(bytes([int(token, 16)])) for token in tokens)
    hits = []
    for section in sections:
        hits.extend(section.VirtualAddress + m.start() for m in re.finditer(b'(?=(' + regex + b'))', section.get_data(), re.S))
    assert rva in hits, (name, hex(rva), hits)
    if hits != [rva]:
        ambiguous_fallbacks.append((name, rva, hits))
        print(f'NOTE {name}: reference RVA 0x{rva:X} matches; existing fallback has {len(hits)} matches and must fail closed.', flush=True)

resolved = {}
for variable, pattern, rva in re.findall(r'(\w+)\s*=\s*Resolve\(\s*memory,\s*(\w+),\s*(\w+)', source):
    assert rva in constants and pattern in patterns, (variable, pattern, rva)
    unique_pattern(constants[rva], patterns[pattern], variable)
    resolved[variable] = constants[rva]
assert len(resolved) >= 25, 'Resolve scanner stopped covering the runtime'

def address_value(expression):
    expression = expression.strip()
    indexed = re.fullmatch(r'(\w+)\[(\d+)\]', expression)
    if indexed:
        return arrays[indexed[1]][int(indexed[2])]
    arithmetic = re.fullmatch(r'(\w+)\s*([-+])\s*(0x[0-9A-Fa-f]+)', expression)
    if arithmetic:
        return address_value(arithmetic[1]) + (-1 if arithmetic[2] == '-' else 1) * int(arithmetic[3], 16)
    return int(expression, 16) if expression.startswith('0x') else constants[expression]

for pattern, expression in re.findall(r'(?<![\w])Resolve\(\s*memory,\s*(\w+),\s*([^,]+),', source):
    if address_value(expression) not in resolved.values():
        unique_pattern(address_value(expression), patterns[pattern], pattern)
observer_sites = re.findall(r'InstallConnectivityObserver\(\s*(?:pendingTransaction|transaction),\s*memory,\s*libraryBase,\s*(0x[0-9A-F]+),\s*"([0-9A-F ]+)"', source)
assert len(observer_sites) == 9, ('Observer coverage changed', len(observer_sites))
for rva, pattern in observer_sites:
    before = len(ambiguous_fallbacks)
    unique_pattern(int(rva, 16), pattern, 'observer')
    assert len(ambiguous_fallbacks) == before, 'Observers require unique patterns even at the reference hash'

# All AddDetour targets are either a named Resolve result or the explicitly
# validated reconstruction entry. Helper method's address parameter is excluded.
targets = set()
for call in re.findall(r'= AddDetour\((.*?);', source, re.S):
    resolution = re.search(r'\b(\w+)\.Rva', call)
    literal = re.search(r'libraryBase\s*\+\s*(0x[0-9A-F]+)', call)
    if resolution:
        targets.add(resolved[resolution[1]])
    elif literal:
        targets.add(int(literal[1], 16))
    else:
        assert 'libraryBase + unchecked((uint)rva), callback' in call, call
targets.update(int(rva, 16) for rva, _ in observer_sites)
assert len(targets) >= 30, ('Detour coverage changed', len(targets))
for rva in sorted(targets):
    assert executable(rva)
    record = db.execute('select binary_hash,size from functions where rva=?', (f'0x{rva:X}',)).fetchone()
    assert record and record[0] == current['currentNativeHash'], hex(rva)
    whole = list(decoder.disasm(pe.get_data(rva, record[1]), rva))
    assert whole and whole[0].mnemonic not in ('call', 'jmp'), ('Call-site used as function target', hex(rva))
    copied = []
    for instruction in whole:
        copied.append(instruction)
        if sum(i.size for i in copied) >= 14:
            break
    end = copied[-1].address + copied[-1].size
    for instruction in whole:
        if (instruction.mnemonic.startswith('j') or instruction.mnemonic == 'call') and instruction.op_str.startswith('0x'):
            target = int(instruction.op_str, 16)
            assert not rva < target < end, ('Interior entry', hex(rva), instruction.address, target)
    print(f'PASS detour function 0x{rva:X}, whole-instruction 14-byte prefix ends 0x{end:X}')

# Static exact-byte and relative-call guards from the copied runtime.
exact_count = 0
for rva, body in re.findall(r'ValidateExactBytes\(\s*memory,\s*(\w+),\s*new byte\[\]\s*\{([^}]+)\}', source):
    address = int(rva, 16) if rva.startswith('0x') else constants[rva]
    expected = bytes(int(v.strip(), 0) for v in body.split(',') if v.strip())
    assert expected and pe.get_data(address, len(expected)) == expected, (rva, expected.hex())
    exact_count += 1
assert exact_count >= 10, exact_count
for rva, array in re.findall(r'ValidateExactBytes\(\s*memory,\s*(\w+),\s*(\w+),', source):
    if rva == 'callRva':
        continue  # Helper's dynamic input; actual call sites checked below.
    if rva == 'rva' and array == 'expected':
        continue  # Observer helper; all nine literal entry patterns checked above.
    expected = bytes(arrays[array])
    assert pe.get_data(address_value(rva), len(expected)) == expected, (rva, array)
    exact_count += 1
call_count = 0
for call, target, body in re.findall(r'ValidateCallTarget\(\s*memory,\s*([^,]+),\s*([^,]+),\s*new byte\[\]\s*\{([^}]+)\}', source):
    address = address_value(call)
    expected = bytes(int(v.strip(), 0) for v in body.split(',') if v.strip())
    assert len(expected) == 5 and expected[0] == 0xE8 and pe.get_data(address, 5) == expected
    assert address + 5 + struct.unpack_from('<i', expected, 1)[0] == address_value(target), (call, target)
    call_count += 1
assert call_count >= 30, call_count

# Incoming cross-function edges into the sole actual inline span, including
# baseline non-code/data references, are checked in addition to local branches.
start, end = 0x19664B, 0x196659
columns = [row[1] for row in db.execute('pragma table_info(xrefs)')]
assert 'to_rva' in columns and 'from_rva' in columns, columns
for record in db.execute('select * from xrefs where binary_hash=?', (current['currentNativeHash'],)):
    item = dict(zip(columns, record))
    if item['to_rva'] is None:
        continue  # External/non-module reference cannot enter this native span.
    target = int(item['to_rva'], 16)
    if start < target < end:
        origin = int(item['from_rva'], 16)
        assert start <= origin < end, item
print(f'PASS copied-hook inventory: {len(resolved)} named reference resolutions ({len(ambiguous_fallbacks)} existing ambiguous fallbacks), {len(observer_sites)} observers, {len(targets)} function targets, {exact_count} exact-byte guards, {call_count} relative-call guards and inline incoming-edge check.')
