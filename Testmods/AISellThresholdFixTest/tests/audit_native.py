"""Hash-bound, read-only Vanilla control-flow and hook ownership audit."""
import hashlib
import json
import pathlib
import sqlite3
import sys
import capstone
import pefile

workspace = pathlib.Path(__file__).resolve().parents[3]
baseline = workspace / '_inspect/CrusaderDE-Native-Baseline'
current = json.loads((baseline / 'CURRENT.json').read_text())
manifest = json.loads((baseline / current['databaseManifest']).read_text())
binary = pathlib.Path(sys.argv[1]).read_bytes()
identity = hashlib.sha256(binary).hexdigest().upper()
assert identity == current['currentNativeHash'] == manifest['identities']['currentNativeHash']
assert identity == 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
db = sqlite3.connect((baseline / manifest['database']['path']).resolve().as_uri() + '?mode=ro', uri=True)
pe = pefile.PE(data=binary)
base = pe.OPTIONAL_HEADER.ImageBase
decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
decoder.detail = True
sites = [(0x3F1F3, 0x3EE10, '488d0516d06203448b8c2890f01200'),
         (0x29723, 0x29700, '8bd7488d2de42a6403418bf0488bcd')]
evidence = {'nativeSha256': identity, 'database': manifest['database']['path'], 'spans': []}
for site, function, expected in sites:
    assert pe.get_data(site, 15).hex() == expected
    length = db.execute('SELECT size FROM functions WHERE binary_hash=? AND rva=?', (identity, f'0x{function:X}')).fetchone()[0]
    instructions = list(decoder.disasm(pe.get_data(function, length), base + function))
    assert sum(i.size for i in instructions) == length
    for ins in instructions:
        if (ins.group(capstone.CS_GRP_JUMP) or ins.group(capstone.CS_GRP_CALL)) and ins.operands and ins.operands[0].type == capstone.CS_OP_IMM:
            target = ins.operands[0].imm - base
            assert not site < target < site + 15, (hex(ins.address), hex(target))
    interior = [f'0x{x:X}' for x in range(site + 1, site + 15)]
    refs = db.execute('SELECT from_rva,to_rva,type FROM xrefs WHERE binary_hash=? AND to_rva IN (' + ','.join('?' for _ in interior) + ')', (identity, *interior)).fetchall()
    assert not refs, refs
    displaced = list(decoder.disasm(pe.get_data(site, 15), base + site))
    assert sum(i.size for i in displaced) == 15
    evidence['spans'].append({'startRva': hex(site), 'endRva': hex(site + 15), 'incomingInterior': refs,
                              'instructions': [{'rva': hex(i.address-base), 'bytes': i.bytes.hex(), 'text': i.mnemonic+' '+i.op_str} for i in displaced]})
    print(f'PASS: RVA {site:X} span=15, complete instructions, whole-function branches and all baseline xrefs have no interior entry.')

# Known current owners from their source/entry contracts; preserve their separate spans.
owners = [(0x29700, 9, 'AIBuildDiagnoseTest sale entry'),
          (0x3F0C9, 19, 'Fixes wheat category'), (0xCEB90, 6, 'APIShared price entry')]
for start, length, owner in owners:
    for site, _, _ in sites:
        assert start + length <= site or site + 15 <= start, owner
evidence['knownAdjacentOwners'] = owners
decision = {i.address-base: i for i in decoder.disasm(pe.get_data(0x3EE10, 1105), base+0x3EE10)}
sale = {i.address-base: i for i in decoder.disasm(pe.get_data(0x29700, 160), base+0x29700)}
assert decision[0x3F202].mnemonic == 'cmp' and decision[0x3F205].mnemonic == 'jg'
assert decision[0x3F22F].operands[0].imm == base+0x29700
assert sale[0x29732].operands[0].imm == base+0xCEB90
assert sale[0x2976D].operands[0].imm == base+0xB7D80
assert sale[0x2978B].mnemonic == 'mov' and sale[0x2979F].mnemonic == 'ret'
assert bytes.fromhex(''.join(sale[a].bytes.hex() for a in [0x29723,0x29725,0x2972C,0x2972F])) == bytes.fromhex(sites[1][2])
print('PASS: native signed comparisons, price-before-removal ordering, epilogue and adjacent owner spans.')
if len(sys.argv)>2:
    pathlib.Path(sys.argv[2]).write_text(json.dumps(evidence, indent=2)+'\n', newline='\r\n')
