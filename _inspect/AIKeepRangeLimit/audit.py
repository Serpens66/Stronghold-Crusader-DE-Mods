import sqlite3, pathlib, json, hashlib, struct
root = pathlib.Path(__file__).resolve().parents[2]
base = root / '_inspect/CrusaderDE-Native-Baseline'
index = json.loads((base / 'CURRENT.json').read_text(encoding='utf-8-sig'))
db = sqlite3.connect((base / index['semanticDirectory'] / 'CrusaderDE-semantic.sqlite').as_uri() + '?mode=ro', uri=True)
db.row_factory = sqlite3.Row
rows = list(db.execute("select * from xrefs where upper(to_rva) between '0XEEF90' and '0XEEF99'"))
assert rows and all(int(row['to_rva'], 16) == 0xeef90 for row in rows)
assert all(row['binary_hash'] == index['currentNativeHash'] for row in rows)
print('PASS: xrefs (including data/function references) enter only at EEF90:', [dict(row) for row in rows])
native = pathlib.Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll').read_bytes()
assert hashlib.sha256(native).hexdigest().upper() == index['currentNativeHash']
pe = struct.unpack_from('<I', native, 0x3c)[0]
count, optional = struct.unpack_from('<H', native, pe+6)[0], struct.unpack_from('<H', native, pe+20)[0]
for i in range(count):
    header = pe + 24 + optional + 40*i
    size, rva, rawsize, raw = struct.unpack_from('<IIII', native, header+8)
    flags = struct.unpack_from('<I', native, header+36)[0]
    if flags & 0x20000000:
        section = native[raw:raw+rawsize]
        for offset in range(len(section)-6):
            length = 0
            if section[offset] in (0xe8, 0xe9):
                length = 5
            elif section[offset] == 0x0f and 0x80 <= section[offset+1] <= 0x8f:
                length = 6
            if length:
                target = rva + offset + length + struct.unpack_from('<i', section, offset+length-4)[0]
                assert not 0xeef90 < target < 0xeef9a, ('Potential external rel32 edge into entry', hex(rva+offset))
    if rva <= 0xeef90 < rva + size:
        entry = native[raw+0xeef90-rva:raw+0xeef90-rva+317]
        print('function sha:', hashlib.sha256(entry).hexdigest().upper())
        (root / '_inspect/AIKeepRangeLimit/EEF90.bin').write_bytes(entry)
assert entry[:10].hex() == '48895c240848896c2410'
print('PASS: executable-section rel32 candidate scan: no incoming interior edge; complete 317-byte body captured for Iced tests.')
