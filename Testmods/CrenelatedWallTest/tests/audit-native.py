"""Read-only regression of the audited complete crenel placement/render feature."""
import hashlib
import json
from pathlib import Path
import sqlite3
import sys
import pefile
import capstone

workspace = Path(sys.argv[1]).resolve()
game = Path("E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition")
baseline = workspace / "_inspect/CrusaderDE-Native-Baseline"
current = json.loads((baseline / "CURRENT.json").read_text(encoding="utf-8-sig"))
native = game / "Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll"
digest = hashlib.sha256(native.read_bytes()).hexdigest().upper()
assert digest == current["currentNativeHash"] == "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2"
database = baseline / current["semanticDirectory"] / "CrusaderDE-semantic.sqlite"
connection = sqlite3.connect(database.as_uri() + "?mode=ro", uri=True)
contracts = {
    "0x85BD0": ["DLL_MapAction", "FUN_18008aaf0"],
    "0x8AAF0": ["FUN_180094120", "FUN_180090cd0"],
    "0x94120": ["FUN_18007a3b0", "FUN_180061210", "FUN_180023990", "0x19"],
    "0x11B50": ["FUN_18001f5f0", "FUN_18006f230", "DAT_1888e3d6c"],
    "0x7A3B0": ["FUN_18005c580", "0x10000400", "0x204e6e4"],
    "0x61210": ["0x1a", "0x60014", "0x60004"],
    "0x5C580": ["0x102", "0x100"],
    "0x6F230": ["param_8 <= local_60", "FUN_18007a3b0", "0x1a", "'b'", "'D'", "0x400000", "0xe1afe0", "local_res10 - 1U", "FUN_1800c8cc0", "c_game_update_pathfinding_for_tile_and_neighbors3x3", "FUN_1800e3b90"],
    "0x5C0E0": ["0x10", "return 1"],
    "0x6CB20": ["0x1a", "0x23", "0x400000", "0xe1afe0"],
    "0x65830": ["FUN_18005c2d0", "FUN_18005c1c0", "*puVar2 = *puVar2 | 0x400000", "0xc0074", "0xc007c", "0xc0070", "0xc0078", "0xc0084"],
    "0x5C2D0": ["param_4 == 0", "param_4 == 4", "0x200"],
    "0x5C1C0": ["param_4 == 1", "param_4 == 3", "param_4 == 5", "param_4 != 7", "0x200"],
    "0x6B530": ["uVar3 = 10", "uVar3 = 0x14", "return ~uVar3"],
    "0xC8CC0": ["param_3 * 2 + param_4", "uVar1 & 3"],
    "0xCF640": ["return 100", "DAT_183665f28 == 6", "0x130d10", "0xfa"],
    "0x1F5F0": ["+ param_3", "0xb0c09"],
    "0xE3B90": ["0x50", "= 2", "DAT_184a7faf0"],
    "0xCDE60": ["FUN_180065830", "FUN_18007c0a0", "FUN_18001d160"],
    "0x5BC70": ["FUN_180065830", "0x4e520"],
    "0x9C730": ["uVar2 + 0x14", "uVar2 + 10"],
    "0xD86F0": [">> 0x16", "0x4a5014b1"],
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
import base64, re
reader_source = (workspace / "Testmods/CrenelatedWallTest/src/NativeAvailability.cs").read_text(encoding="utf-8-sig")
reader_bytes = base64.b64decode(re.search(r'ReferenceCode = "([^"]+)"', reader_source)[1])
assert len(reader_bytes) == 179 and image.get_data(0xCF640, len(reader_bytes)) == reader_bytes
assert hashlib.sha256(reader_bytes).hexdigest().upper() == "F2C875634DF70ABE13AE3938E6F63B1C4BCF2E61061B753BCD3FB9EFFF23B21A"
assert sum(image.get_data(s.VirtualAddress, min(s.Misc_VirtualSize, s.SizeOfRawData)).count(reader_bytes) for s in image.sections if s.Characteristics & 0x20000000) == 1
source = (workspace / "shcde-script-extender/src/SHCDESE.BepInEx/Detours/BulkBuildingDetours.cs").read_text(encoding="utf-8-sig")
start = source.index("public static Int64 c_game_build_wall_hook_impl")
end = source.index("// void __fastcall c_game_bulldoze_wall", start)
wrapper = source[start:end]
assert "OnBuildWall.Raise" in wrapper and "eventArgs.BuildWallsMaximum" in wrapper
assert "SkipOriginalFunction" not in wrapper
assert "new(EventHookPhase.Post, playerId" in wrapper # Post carries original inputs, not effective mutated inputs.
rows = connection.execute("select rva,pseudocode from functions where binary_hash=? and pseudocode is not null", (digest,)).fetchall()
writers = [rva for rva, code in rows if "| 0x400000;" in code and "0x898400" in code]
assert sorted(writers) == ["0x65830", "0x6CB20", "0x6F230"], "New crenel high-bit writer: " + str(writers)
print("PASS: current native SHA-256,", len(contracts), "feature functions, Chore-25 dispatch, all three high-bit writers and installed-source event contract. No hooks installed or executable buffers changed.")
