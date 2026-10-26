"""Current immutable machine evidence, using the compiled production site's TSV contract.

This checks instruction boundaries, all retained incoming xrefs and full function bytes.
It does not treat the mutable source inventory or a successful scan as runtime evidence.
"""
import hashlib
import json
import sqlite3
import struct
import sys
from pathlib import Path

root, contract = map(Path, sys.argv[1:3])
# Optional: record the passed result. Builds compare against this small hash-bound evidence
# instead of requiring the untracked multi-GB knowledge database inside every build copy.
evidence_out = Path(sys.argv[4]) if len(sys.argv) == 5 and sys.argv[3] == '--evidence-out' else None
baseline = root / '_inspect/CrusaderDE-Native-Baseline'
current = json.loads((baseline / 'CURRENT.json').read_text(encoding='utf-8-sig'))
manifest = json.loads((baseline / current['databaseManifest']).read_text(encoding='utf-8-sig'))
native = next(b for b in manifest['binaries'] if b['role'] == 'current-native')
image = Path(native['sourcePath']).read_bytes()
assert hashlib.sha256(image).hexdigest().upper() == current['currentNativeHash'] == native['hash']
con = sqlite3.connect((baseline / manifest['database']['path']).resolve().as_uri() + '?mode=ro', uri=True)
pe = struct.unpack_from('<I', image, 0x3C)[0]
count = struct.unpack_from('<H', image, pe + 6)[0]
optional = struct.unpack_from('<H', image, pe + 20)[0]
sections = []
for n in range(count):
    size, va, raw_size, raw = struct.unpack_from('<IIII', image, pe + 24 + optional + 40 * n + 8)
    sections.append((va, max(size, raw_size), raw))

def data(rva, length):
    raw = next(raw + rva - va for va, size, raw in sections if va <= rva < va + size)
    return image[raw:raw + length]

sites = []
for line in contract.read_text(encoding='utf-8-sig').splitlines():
    rva, length, function, function_length, digest, encoded = line.split('\t')
    rva, function = int(rva, 16), int(function, 16)
    length, function_length = int(length), int(function_length)
    assert data(rva, length).hex().upper() == encoded
    assert hashlib.sha256(data(function, function_length)).hexdigest().upper() == digest
    record = con.execute('SELECT payload FROM analysis_functions WHERE binary_hash=? AND rva=?',
                         (current['currentNativeHash'], f'0x{function:X}')).fetchone()
    assert record, f'Missing current machine record at {function:X}'
    function_data = json.loads(record[0])
    instructions = function_data['instructions']
    for i in instructions:
        assert data(int(i['rva'], 16), i['length']).hex().upper() == i['bytes'].upper()
    displaced = [i for i in instructions if rva <= int(i['rva'], 16) < rva + length]
    assert displaced and int(displaced[0]['rva'], 16) == rva
    assert sum(i['length'] for i in displaced) == length
    assert any(int(i['rva'], 16) == rva + length for i in instructions), 'Missing instruction continuation'
    interior = [f'0x{x:X}' for x in range(rva + 1, rva + length)]
    refs = con.execute('SELECT from_rva,to_rva,type FROM xrefs WHERE binary_hash=? AND to_rva IN (' +
                       ','.join('?' for _ in interior) + ')', (current['currentNativeHash'], *interior)).fetchall()
    assert not [r for r in refs if not rva <= int(r[0], 16) < rva + length], (hex(rva), refs)
    for previous, previous_length in sites:
        assert not (previous < rva + length and rva < previous + previous_length), 'Own hook overlap'
    sites.append((rva, length))
assert sites, 'No compiled sites supplied'
if evidence_out:
    lines = [f"# CrusaderDE {current['currentNativeHash']}"] + contract.read_text(encoding='utf-8-sig').splitlines()
    evidence_out.write_bytes(('\r\n'.join(lines) + '\r\n').encode('ascii'))
    print(f'Wrote audited contract evidence: {evidence_out}')
print(f'PASS: {len(sites)} compiled native sites; canonical hash/function bytes; exact instructions; no incoming interiors or own overlap.')
