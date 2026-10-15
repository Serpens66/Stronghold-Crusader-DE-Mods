"""Read-only installed APIShared identity for the explicitly approved testmod exception."""
from pathlib import Path
import hashlib
import json
import sys

workspace = Path(sys.argv[2]).resolve()
root = Path("E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/plugins/APIShared_Serp")
snapshot = workspace / "_inspect/CrenelatedWallTest/APIShared-installed-before-20261015.json"
state = {str(p.relative_to(root)).replace("\\", "/"): hashlib.sha256(p.read_bytes()).hexdigest()
         for p in sorted(root.rglob("*")) if p.is_file()}
assert state.get("APIShared.dll") == "2797b06042b0634ccc97ee5d2175c96944f95edfa1924d764776d9a6bde14758", "Approved installed SDK DLL changed"
if sys.argv[1] == "capture":
    if snapshot.exists():
        raise SystemExit("Refusing to replace the installed dependency snapshot.")
    with snapshot.open("w", encoding="utf-8", newline="\r\n") as f:
        f.write(json.dumps(state, indent=2) + "\n")
else:
    prior = json.loads(snapshot.read_text(encoding="utf-8"))
    changed = sorted(key for key in set(state) | set(prior) if state.get(key) != prior.get(key))
    if changed:
        raise SystemExit("Installed APIShared changed: " + ", ".join(changed))
print("PASS: installed APIShared 0.9.2", sys.argv[1], "-", len(state), "unchanged SHA-256 file identities; no build/copy.")
