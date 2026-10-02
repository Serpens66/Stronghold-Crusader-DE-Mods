from pathlib import Path
import hashlib, json, sqlite3, struct
import pefile

base = Path('_inspect/CrusaderDE-Native-Baseline')
current = json.loads((base / 'CURRENT.json').read_text())
manifest = json.loads((base / current['databaseManifest']).read_text())
native = Path(next(b['sourcePath'] for b in manifest['binaries'] if b['role'] == 'current-native'))
assert hashlib.sha256(native.read_bytes()).hexdigest().upper() == current['currentNativeHash']
pe = pefile.PE(str(native))
db = (base / manifest['database']['path']).resolve()
c = sqlite3.connect('file:' + str(db).replace('\\','/') + '?mode=ro', uri=True)
lines = ['Native hash: ' + current['currentNativeHash']]
for rotation in range(4):
    cells = struct.unpack('<25i', pe.get_data(0x2D1A30 + rotation*100,100))
    expected = tuple(int(5 <= i < 20 if rotation % 2 == 0 else 1 <= i % 5 <= 3) for i in range(25))
    assert cells == expected
    lines.append('Native bridge mapper %d: %s; nonzero=%d' % (rotation,cells,sum(cells)))
for rva in ['0x6D580','0x739C0','0xA5E20','0x645C0','0x64460','0x69850','0x69560',
            '0x59210','0x725A0','0x725E0','0xD90D0','0xD86F0','0xE3B90','0xE04B0',
            '0xE2610','0xE2F60','0xE2CA0','0xD9C40','0xDB650','0x117820',
            '0x117C70','0x11B520','0x11E960','0x196280','0xF4930']:
    row = c.execute('select name,pseudocode from functions where binary_hash=? and rva=?',
                    (current['currentNativeHash'],rva)).fetchone()
    assert row and row[1]
    lines.extend([rva+' '+row[0],row[1]])
    callers=c.execute('select from_rva,source_function from xrefs where binary_hash=? and to_rva=? and type like ?',
                       (current['currentNativeHash'],rva,'%CALL%')).fetchall()
    lines.append('Call references: '+repr(callers))
text='\r\n'.join(lines).replace('\r\r\n','\r\n')
path=Path('_inspect/EnemyGateBuildingContextAudit/bridge-native-evidence.txt')
text=text.replace('\r\n','\n').replace('\n','\r\n')
path.write_bytes(text.encode('utf-8')); assert path.read_bytes().decode('utf-8') == text
print('PASS: canonical native hash, four actual mapper fixtures, complete 25-function feature evidence retained')
