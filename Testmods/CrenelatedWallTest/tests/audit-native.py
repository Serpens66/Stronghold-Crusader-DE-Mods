"""Read-only regression of the audited complete crenel placement/render feature."""
import hashlib
import json
from pathlib import Path
import sqlite3
import sys
import os
import pefile
import capstone

workspace = Path(sys.argv[1]).resolve()
game = Path("E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition")
baseline = Path(os.environ.get("SHCDE_NATIVE_BASELINE_ROOT", workspace / "_inspect/CrusaderDE-Native-Baseline")).resolve()
current = json.loads((baseline / "CURRENT.json").read_text(encoding="utf-8-sig"))
native = game / "Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll"
digest = hashlib.sha256(native.read_bytes()).hexdigest().upper()
assert digest == current["currentNativeHash"] == "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2"
# Resolve the database through the baseline manifest, exactly like tools/semantic/query.ps1.
manifest = json.loads((baseline / current["databaseManifest"]).read_text(encoding="utf-8-sig"))
database = (baseline / manifest["database"]["path"]).resolve()
assert str(database).lower().startswith(str(baseline).lower()) and database.is_file(), "Semantic database missing: " + str(database)
connection = sqlite3.connect(database.as_uri() + "?mode=ro", uri=True)
contracts = {
    "0x85BD0": ["DLL_MapAction", "FUN_18008aaf0"],
    "0x8AAF0": ["FUN_180094120", "FUN_180090cd0"],
    "0x94120": ["FUN_18007a3b0", "FUN_180061210", "FUN_180023990", "0x19"],
    "0x11B50": ["FUN_18001f5f0", "FUN_18006f230", "DAT_1888e3d6c"],
    "0x5C580": ["0x102", "0x100"],
    "0x6F230": ["param_8 <= local_60", "FUN_18007a3b0", "0x1a", "'b'", "'D'", "0x400000", "0xe1afe0", "local_res10 - 1U", "FUN_1800c8cc0", "c_game_update_pathfinding_for_tile_and_neighbors3x3", "FUN_1800e3b90"],
    "0x5C0E0": ["0x10", "return 1"],
    "0x6CB20": ["0x1a", "0x23", "0x400000", "0xe1afe0"],
    "0x65830": ["FUN_18005c2d0", "FUN_18005c1c0", "*puVar2 = *puVar2 | 0x400000", "0xc0074", "0xc007c", "0xc0070", "0xc0078", "0xc0084"],
    "0x5C2D0": ["param_4 == 0", "param_4 == 4", "0x200"],
    "0x5C1C0": ["param_4 == 1", "param_4 == 3", "param_4 == 5", "param_4 != 7", "0x200"],
    "0x6B530": ["uVar3 = 10", "uVar3 = 0x14", "return ~uVar3"],
    "0xC8CC0": ["param_3 * 2 + param_4", "uVar1 & 3"],
    "0x6A560": ["0x400102", "return 0xf"],  # Ladder target: needs a low crenel/wall tile.
    "0x1F5F0": ["+ param_3", "0xb0c09"],
    "0xE3B90": ["0x50", "= 2", "DAT_184a7faf0"],
    "0xCDE60": ["FUN_180065830", "FUN_18007c0a0", "FUN_18001d160"],
    "0x5BC70": ["FUN_180065830", "0x4e520"],
    "0x9C730": ["uVar2 + 0x14", "uVar2 + 10"],
    "0xD86F0": [">> 0x16", "0x4a5014b1"],  # High crenels get no path edges at all.
    "0xDC900": [">> 0x16", "0x10000300"],
    "0xE9D90": [">> 0x16", "0x10000100"],
    "0xE9FF0": [">> 0x16", "0x10000100"],
    "0xF0710": [">> 0x16", "0x10000100"],
    "0x10DF60": ["0x400000", "c_game_get_next_reachable_pcl_to_destination_for_player"],
}
for rva, tokens in contracts.items():
    row = connection.execute("select pseudocode from functions where binary_hash=? and rva=?", (digest, rva)).fetchone()
    assert row and row[0], "Missing audited function " + rva
    for token in tokens:
        assert token in row[0], rva + ": missing contract " + token
image = pefile.PE(str(native))
import struct
handler = struct.unpack("<Q", image.get_data(0x2C7A30 + 25 * 8, 8))[0]
assert handler - image.OPTIONAL_HEADER.ImageBase == 0x11B50
decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
handler_size = connection.execute("select size from functions where binary_hash=? and rva=?", (digest, "0x11B50")).fetchone()[0]
instructions = list(decoder.disasm(image.get_data(0x11B50, handler_size), handler))
assert any(i.mnemonic == "call" and "0x18006f230" in i.op_str for i in instructions)
import re
source_path = workspace / "shcde-script-extender/src/SHCDESE.BepInEx/Detours/BulkBuildingDetours.cs"
if not source_path.is_file() and os.environ.get("SHCDE_NATIVE_BASELINE_ROOT"):
    # Isolated branch sources reuse the canonical read-only baseline/fork.
    source_path = baseline.parent.parent / "shcde-script-extender/src/SHCDESE.BepInEx/Detours/BulkBuildingDetours.cs"
source = source_path.read_text(encoding="utf-8-sig")
start = source.index("public static Int64 c_game_build_wall_hook_impl")
end = source.index("// void __fastcall c_game_bulldoze_wall", start)
wrapper = source[start:end]
assert "OnBuildWall.Raise" in wrapper and "eventArgs.BuildWallsMaximum" in wrapper
assert "SkipOriginalFunction" not in wrapper
assert "new(EventHookPhase.Post, playerId" in wrapper # Post carries original inputs, not effective mutated inputs.
rows = connection.execute("select rva,pseudocode from functions where binary_hash=? and pseudocode is not null", (digest,)).fetchall()
writers = [rva for rva, code in rows if "| 0x400000;" in code and "0x898400" in code]
assert sorted(writers) == ["0x65830", "0x6CB20", "0x6F230"], "New crenel high-bit writer: " + str(writers)
hook_source = (workspace / "Testmods/CrenelatedWallTest/src/StraightGraphicsHook.cs").read_text(encoding="utf-8-sig")
predicate = bytes.fromhex(re.search(r'BodyHex = "([0-9A-F]+)"', hook_source)[1])
assert len(predicate) == 237 and image.get_data(0x5C2D0, 237) == predicate
prologue = list(decoder.disasm(predicate[:15], 0x18005C2D0))
assert len(prologue) == 3 and [i.size for i in prologue] == [5, 5, 5] and all(i.mnemonic == "mov" for i in prologue)
callers = [rva for rva, code in rows if "FUN_18005c2d0(" in code and rva != "0x5C2D0"]
assert callers == ["0x65830"], "Straight predicate gained a non-graphics consumer: " + str(callers)
decoder.detail = True
decoder.skipdata = True
entry_calls = []
for section in image.sections:
    if not section.Characteristics & 0x20000000:
        continue
    for ins in decoder.disasm(section.get_data(), image.OPTIONAL_HEADER.ImageBase + section.VirtualAddress):
        if not ins.id or not (ins.group(capstone.CS_GRP_JUMP) or ins.group(capstone.CS_GRP_CALL)):
            continue
        for op in ins.operands:
            if op.type != capstone.x86.X86_OP_IMM:
                continue
            assert not 0x18005C2D0 < op.imm < 0x18005C2DF, "Incoming edge into displaced prologue"
            if op.imm == 0x18005C2D0:
                entry_calls.append(ins.address)
assert entry_calls == [0x180065DD6, 0x180065E4F]
backend = game / "BepInEx/plugins/000shcdese/RedBird.Backends.NativeX64.dll"
assert hashlib.sha256(backend.read_bytes()).hexdigest().upper() == re.search(r'BackendHash = "([0-9A-F]+)"', hook_source)[1]
print("PASS: current native SHA-256,", len(contracts), "feature functions, Chore-25, all high-bit writers, graphics-only callers, full 237-byte predicate, Absolute/15 entry/continuation, incoming edges and installed NativeX64 identity. Audit only; no hooks installed.")
