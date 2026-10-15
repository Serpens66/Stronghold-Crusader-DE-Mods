"""Snapshot/check dependency source, metadata and shipped files without changing APIShared."""
from pathlib import Path
import hashlib
import json
import sys

workspace = Path(sys.argv[2]).resolve()
root = workspace / "APIShared"
snapshot = workspace / "_inspect/CrenelatedWallTest/APIShared-before-20261015.json"
folders = [root / name for name in ("src", "Properties", "Override", "Patches", "Locales", "BepInEx/plugins/APIShared_Serp")]
files = {p for p in root.iterdir() if p.is_file() and not p.name.startswith(".")}
for folder in folders:
    if folder.exists():
        files.update(p for p in folder.rglob("*") if p.is_file())
state = {str(p.relative_to(root)).replace("\\", "/"): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(files)}
if sys.argv[1] == "capture":
    if snapshot.exists():
        raise SystemExit("Refusing to replace the original dependency snapshot.")
    snapshot.parent.mkdir(parents=True, exist_ok=True)
    with snapshot.open("w", encoding="utf-8", newline="\r\n") as f:
        f.write(json.dumps(state, indent=2) + "\n")
else:
    prior = json.loads(snapshot.read_text(encoding="utf-8"))
    changed = sorted(key for key in set(state) | set(prior) if state.get(key) != prior.get(key))
    if changed:
        raise SystemExit("APIShared changed since the task snapshot: " + ", ".join(changed))
print("PASS: APIShared source/metadata/package", sys.argv[1], "-", len(state), "SHA-256 file identities.")
