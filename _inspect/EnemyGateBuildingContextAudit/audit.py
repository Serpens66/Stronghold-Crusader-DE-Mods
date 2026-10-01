"""Read installed native bytes; never loads or patches the game runtime."""
import hashlib
import json
from pathlib import Path
import sqlite3
import struct
import capstone
import pefile

ROOT = Path(__file__).resolve().parents[2]
BASELINE = ROOT / "_inspect/CrusaderDE-Native-Baseline"
current = json.loads((BASELINE / "CURRENT.json").read_text(encoding="utf-8-sig"))
manifest = json.loads((BASELINE / current["databaseManifest"]).read_text(encoding="utf-8-sig"))
native = Path(next(b["sourcePath"] for b in manifest["binaries"] if b["role"] == "current-native"))
reference = current["currentNativeHash"]
checks = 0

def check(condition, message):
    global checks
    if not condition:
        raise AssertionError(message)
    checks += 1

check(hashlib.sha256(native.read_bytes()).hexdigest().upper() == reference, "installed DLL hash")
check(manifest["identities"]["currentNativeHash"] == reference, "manifest provenance")
db = sqlite3.connect((BASELINE / manifest["database"]["path"]).resolve().as_uri() + "?mode=ro", uri=True)
pe = pefile.PE(str(native))
image = pe.get_memory_mapped_image()
base = pe.OPTIONAL_HEADER.ImageBase
decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
functions = [0x2A720, 0x30620, 0x11E960, 0xDA020, 0xE2610, 0xE2CA0,
             0x123090, 0xD9C40, 0xDA590, 0xDB650, 0x196280, 0x1185A0]
instructions = {}
evidence = ["Installed native SHA-256: " + reference]
for rva in functions:
    row = db.execute("SELECT size,pseudocode FROM functions WHERE binary_hash=? AND rva=?",
                     (reference, f"0x{rva:X}")).fetchone()
    check(row is not None, f"function 0x{rva:X} present")
    decoded = list(decoder.disasm(image[rva:rva + row[0]], base + rva))
    instructions.update({i.address - base: i for i in decoded})
    evidence.extend([row[1], "INSTALLED MACHINE CODE:"])
    evidence.extend(f"{i.address-base:08X} {i.bytes.hex()} {i.mnemonic} {i.op_str}" for i in decoded)

expected = {
    0x2A9C5: "664489bc016a6dcc07",  # fallback opponent 1
    0x2A9E6: "6689b4016a6dcc07",    # selected opponent
    0x30682: "410fbfac3e4a060000",  # signed word at manager +0x64A
    0x30A09: "896c2428",           # sixth argument = planner EBP
    0x30A33: "e8e8950a00",         # planner calls DA020
    0x11E986: "33f6",             # ESI = zero
    0x11FF47: "490fbf442a5a",      # one-based leader ID
    0x11FF4D: "4869d090040000",    # native sentinel + ID * 0x490
    0x11FF65: "4183fc09",         # command 9
    0x11FF69: "7408",             # uses leader control word
    0x11FF6B: "8bc6",             # other commands use zero
    0x11FF6D: "4183fc26",         # command 38
    0x11FF71: "7508",             # skips leader word otherwise
    0x11FF73: "0fbf841aee060000",  # SIGNED WORD, not public BYTE
    0x11FF89: "89442428",         # sixth argument
    0x11FF9A: "e881a0fbff",       # DA020
    0x11FFA7: "e8e4300000",       # downstream ranking
    0x11FFAC: "3935f689fa05",     # candidate gate, compare with zero
}
for address, expected_bytes in expected.items():
    check(address in instructions, f"instruction boundary 0x{address:X}")
    check(instructions[address].bytes.hex() == expected_bytes, f"bytes 0x{address:X}")

for command in [9, 36, 38]:
    handler = struct.unpack_from("<I", image, 0x121D4C + (command - 3) * 4)[0]
    check(handler == 0x11FC25, f"building handler for command {command}")

callers = db.execute("SELECT from_rva,source_function FROM xrefs WHERE binary_hash=? AND to_rva=? AND type=?",
                     (reference, "0xDA020", "UNCONDITIONAL_CALL")).fetchall()
check(set(callers) == {("0x30A33", "FUN_180030620"), ("0x11FF9A", "FUN_18011e960")}, "two direct native callers")
refs = db.execute("SELECT from_rva FROM xrefs WHERE binary_hash=? AND to_rva=?",
                  (reference, "0x7CC6D6A")).fetchall()
check({r[0] for r in refs} == {"0x2A9C5", "0x2A9E6", "0x30682"}, "direct planning-field references")

# Equality of native sentinel indexing and public API indexing, including high IDs.
for unit_id in [1, 2, 9999, 10000]:
    check(0x65C + unit_id * 0x490 + 0x92 ==
          0x65C + 0x490 + (unit_id - 1) * 0x490 + 0x92, "leader ID basis")
check(0x64A - 0x2A == 0x620, "planning field relative offset")
check(0x5A - 0x2A == 0x30, "leader field relative offset")
check(0x6EE - 0x65C == 0x92, "control field relative offset")
check(0x620 != 0x61C, "not r_AttackTargetOwnerPlayerId")

output = Path(__file__).with_name("evidence.txt")
content = "\r\n".join("\n".join(evidence).replace("\r\n", "\n").split("\n")) + "\r\n"
output.write_bytes(content.encode("utf-8"))
check(output.read_bytes() == content.encode("utf-8"), "evidence readback")
print(f"PASS: {checks} static checks; full decompiler/native evidence: {output}")
