"""Hash-bound, read-only native evidence; writes only retained audit artifacts."""
from pathlib import Path
import hashlib, json, sqlite3
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

ROOT = Path('_inspect/CrusaderDE-Native-Baseline')
index = json.loads((ROOT / 'CURRENT.json').read_text())
manifest = json.loads((ROOT / index['databaseManifest']).read_text())
native = Path(next(b['sourcePath'] for b in manifest['binaries'] if b['role'] == 'current-native'))
assert hashlib.sha256(native.read_bytes()).hexdigest().upper() == index['currentNativeHash']
db = sqlite3.connect((ROOT / manifest['database']['path']).resolve().as_uri() + '?mode=ro', uri=True)
db.row_factory = sqlite3.Row
pe = pefile.PE(str(native))
decoder = Cs(CS_ARCH_X86, CS_MODE_64)
# Function entries only. E2610, movement/attack builders and 113BC0 already have owners.
HOOKS = [0x64460, 0x645C0, 0xE49D0, 0x10D9F0, 0xD95E0, 0xD9190,
         0x10F150, 0x10DF60, 0x1127D0, 0x115B10, 0x1150E0, 0x1151E0,
         0x10AA20, 0x11A980, 0x122B40, 0xE7F60,
         0x110EC0, 0x111060, 0x111330, 0x111620, 0x111960, 0x111AF0,
         0x111C00, 0x111D90, 0x111F20,
         0x3C2E0,0x2D250,0x2C480,0x2C5A0,0x3BD50,0xCF360,0xCF400]
RELATED = [0x739C0,0xA5E20,0x69850,0x69560,0x59210,0x725A0,0x725E0,
           0xD90D0,0xD86F0,0xE3B90,0xC4BF0,0xC5040,0xE2610,
           0x10B870,0x10C7C0,0x10C340,0x10DD90,0x10F1F0,0x10F630,
           0x1126B0,0x112A00,0x112C80,0x112B90,0x112810,0x112D60,
           0x112EF0,0x113BC0,0x113E00,0x113F60,0x113AB0,0x1133B0,
           0xF17F0,0xEF0D0,0xEE980,0xEEC80,0xE6AE0,0xE7530,
           0x11E960,0x11B520,0x117C70,0x117820,0x196280,0xF4930,0xEDF30,
           0x2AE40,0x2A600,0x2B080,0x3B8D0,0x3B450,0x3FC40,0xEA4E0,0xEA620,
           0xF2CB0,0xEFD20,0x3C150,0x3B980,0x3BFA0,0x3CDC0,0x3BB80,0x3CCD0,
           0x3C0A0,0x3B820,0x3BC90,0x3D260,0x2B340,0x2B2C0,0x2D600,
           0x112370,0x1123E0,0x112200,0x112450,0x112190,0x1102D0,0x10A670,
           0x10C1C0,0x2A2C0,0x2A340,0x2C1B0,0x30E90,0x304B0,0x1140C0,
           0xEA3D0,0x3BFA0,0xCF360,0xCF400,0xCF020,
           0x3D020,0x18FC00,0x122800,0x3C9C0,0x2FD40,0x298C0,0x2A6B0]
evidence = ['Canonical SHA256: ' + index['currentNativeHash']]
contracts = []
for rva in dict.fromkeys(HOOKS + RELATED):
    key = f'0x{rva:X}'
    row = db.execute('select * from functions where binary_hash=? and rva=?',
                     (index['currentNativeHash'],key)).fetchone()
    assert row and row['pseudocode'], key
    evidence.extend([key + ' ' + row['name'] + ' confidence=' + row['confidence'], row['pseudocode']])
    refs = db.execute('select from_rva,source_function,type from xrefs where binary_hash=? and to_rva=?',
                      (index['currentNativeHash'],key)).fetchall()
    evidence.append('Call/branch references: ' + repr([dict(x) for x in refs]))
    if rva not in HOOKS:
        continue
    instructions = list(decoder.disasm(pe.get_data(rva,row['size']),rva))
    span = []
    length = 0
    for ins in instructions:
        span.append(ins)
        length += ins.size
        if length >= 14:
            break
    assert span and length >= 14
    assert all(x.mnemonic not in ['call','jmp','ret'] and not x.mnemonic.startswith('j') for x in span), key
    # Full-module direct xrefs are retained; every known interior entry rejects installation.
    interior = []
    for x in db.execute('select from_rva,to_rva,source_function,type from xrefs where binary_hash=?',
                         (index['currentNativeHash'],)):
        try:
            target = int(x['to_rva'],16)
        except (TypeError,ValueError):
            continue
        if rva < target < rva + length:
            interior.append(dict(x))
    assert not interior, (key,interior)
    # Decode every direct branch in the enclosing function, independently of DB xrefs.
    for ins in instructions:
        if ins.mnemonic.startswith("j") or ins.mnemonic == "call":
            try: target = int(ins.op_str, 0)
            except ValueError: continue
            assert not rva < target < rva + length, (key,ins.address,target)
    data = pe.get_data(rva,length)
    contract = dict(rva=rva,length=length,bytes=data.hex().upper(),end=rva+length,
                    size=row['size'],confidence=row['confidence'],incomingInterior=interior,
                    instructions=[f'{x.address:X}: {x.mnemonic} {x.op_str}' for x in span])
    contracts.append(contract)
    evidence.append('Absolute/14 backend entry contract: '+json.dumps(contract))
out = Path('_inspect/EnemyGateBuildingContextAudit')
def write(path,text):
    value=text.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8')
    path.write_bytes(value)
    assert path.read_bytes() == value
write(out/'bridge-decision-evidence.txt','\n\n'.join(evidence))
write(out/'bridge-decision-contracts.json',json.dumps(dict(nativeHash=index['currentNativeHash'],
        backendSha256=hashlib.sha256((native.parents[3]/'BepInEx/plugins/000shcdese/RedBird.Backends.NativeX64.dll').read_bytes()).hexdigest().upper(),
        scheme='Absolute',functions=contracts),indent=2))
print('PASS: canonical hash; '+str(len(dict.fromkeys(HOOKS+RELATED)))+' functions; '+str(len(contracts))+' full Absolute spans; no known interior entries')
