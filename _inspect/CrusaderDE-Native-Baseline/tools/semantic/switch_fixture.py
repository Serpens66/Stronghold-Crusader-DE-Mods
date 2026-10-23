"""A source-owned x64 PE switch fixture for static Ghidra verification; never executed."""
import hashlib
import json
from pathlib import Path
import struct


def bytes_and_cases():
    # int selector in ECX; unsigned bound on selector+2; five signed cases.
    code=bytes.fromhex('8BC183C00283F8047728488D152F000000486304824801D0FFE0')
    code+=b''.join(b'\xB8'+struct.pack('<I',value)+b'\xC3' for value in [7,11,13,17,99])
    code+=b'\xCC'*(64-len(code))
    code+=b''.join(struct.pack('<i',target-64) for target in [26,26,32,38,44])
    pe=bytearray(1024);pe[:2]=b'MZ';struct.pack_into('<I',pe,0x3c,0x80);pe[0x80:0x84]=b'PE\0\0'
    struct.pack_into('<HHIIIHH',pe,0x84,0x8664,1,0,0,0,240,0x22)
    optional=0x98;struct.pack_into('<H',pe,optional,0x20b)
    struct.pack_into('<I',pe,optional+4,512);struct.pack_into('<I',pe,optional+16,0x1000)
    struct.pack_into('<I',pe,optional+20,0x1000);struct.pack_into('<Q',pe,optional+24,0x180000000)
    struct.pack_into('<II',pe,optional+32,0x1000,512);struct.pack_into('<HH',pe,optional+40,6,0)
    struct.pack_into('<II',pe,optional+56,0x2000,512);struct.pack_into('<HH',pe,optional+68,3,0x8160)
    struct.pack_into('<QQQQ',pe,optional+72,0x100000,0x1000,0x100000,0x1000);struct.pack_into('<I',pe,optional+108,16)
    section=optional+240;pe[section:section+8]=b'.text\0\0\0'
    struct.pack_into('<IIII',pe,section+8,len(code),0x1000,512,512);struct.pack_into('<I',pe,section+36,0x60000020)
    pe[512:512+len(code)]=code
    expected={'selectorRegister':'ECX','functionRva':'0x1000','switchRva':'0x1018','cases':[
        {'value':v,'target':hex(0x1000+t),'result':result} for v,t,result in [(-2,26,7),(-1,26,7),(0,32,11),(1,38,13),(2,44,17)]],
        'defaultTarget':'0x1032','defaultResult':99,'tableRva':'0x1040','entrySize':4,'entryCount':5}
    return bytes(pe),expected


def create(root):
    pe,expected=bytes_and_cases();digest=hashlib.sha256(pe).hexdigest().upper()
    root=Path(root)/digest[:8];root.mkdir(parents=True,exist_ok=True)
    evidence=root/'expected.json'
    if evidence.exists() and json.loads(evidence.read_text())['binaryHash']!=digest:raise ValueError('Fixture directory hash-prefix collision')
    path=root/'switch.exe';path.write_bytes(pe)
    (root/'expected.json').write_text(json.dumps({**expected,'binaryHash':digest},indent=2).replace('\n','\r\n')+'\r\n',encoding='utf-8',newline='')
    (root/'selection.txt').write_text('0x1000\r\n',encoding='utf-8',newline='')
    return {'directory':str(root.resolve()),'binaryHash':digest,'path':str(path.resolve())}


if __name__=='__main__':
    import sys
    print(json.dumps(create(sys.argv[1])))
