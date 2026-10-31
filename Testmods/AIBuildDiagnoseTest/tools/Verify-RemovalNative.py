"""Read-only validation of removal entries against the selected Vanilla baseline."""
import hashlib
import json
import re
import sqlite3
import struct
from pathlib import Path

mod = Path(__file__).resolve().parents[1]
root = mod.parents[1]
baseline = root / '_inspect/CrusaderDE-Native-Baseline'
current = json.loads((baseline / 'CURRENT.json').read_text(encoding='utf-8-sig'))
manifest = json.loads((baseline / current['databaseManifest']).read_text(encoding='utf-8-sig'))
native = next(item for item in manifest['binaries'] if item['role'] == 'current-native')
image = Path(native['sourcePath']).read_bytes()
binary_hash = hashlib.sha256(image).hexdigest().upper()
assert binary_hash == current['currentNativeHash'] == manifest['identities']['currentNativeHash']
contracts = (mod / 'src/Diagnostics/EconomyHookContract.cs').read_text(encoding='utf-8-sig')
entries = [(int(r, 16), int(n), bytes.fromhex(b)) for r, n, b in re.findall(
    r'new EconomyHookContract\(0x([a-fA-F0-9]+), (\d+), "([A-F0-9]+)"\)', contracts)]
worker_contracts = (mod / 'src/Diagnostics/WorkerCauseNativeHooks.cs').read_text(encoding='utf-8-sig')
worker_sites = [(int(r, 16), int(n), bytes.fromhex(b)) for r, n, b in re.findall(
    r'new Site\(0x([a-fA-F0-9]+), (\d+), (?:true|false), "([A-F0-9]+)"\)', worker_contracts)]
assert len(worker_sites) == 2
pe = struct.unpack_from('<I', image, 0x3c)[0]
sections = [struct.unpack_from('<IIII', image, pe + 24 + struct.unpack_from('<H', image, pe + 20)[0] + i * 40 + 8)
            for i in range(struct.unpack_from('<H', image, pe + 6)[0])]
database = baseline / manifest['database']['path']
with sqlite3.connect(database.resolve().as_uri() + '?mode=ro', uri=True) as query:
    incoming = query.execute('SELECT from_rva,to_rva,type FROM xrefs WHERE binary_hash=? AND to_rva IS NOT NULL',
                             (binary_hash,)).fetchall()
    for rva, length, prefix in entries + worker_sites:
        offset = next(rva - va + raw for size, va, raw_size, raw in sections if va <= rva < va + raw_size)
        assert image[offset:offset + len(prefix)] == prefix, hex(rva)
        assert not [(f, t, k) for f, t, k in incoming if rva < int(t, 16) < rva + length], hex(rva)
        function_rva = '0x12BC00' if (rva, length, prefix) in worker_sites else hex(rva).upper().replace('0X', '0x')
        assert query.execute('SELECT 1 FROM functions WHERE binary_hash=? AND rva=?',
                             (binary_hash, function_rva)).fetchone(), hex(rva)
        print(f'PASS entry={rva:#x} span={length} fingerprint=current-image interior-xrefs=none')
print('PASS all economy/removal entries; instruction boundaries/backend are executed by Diagnostics.Tests')
