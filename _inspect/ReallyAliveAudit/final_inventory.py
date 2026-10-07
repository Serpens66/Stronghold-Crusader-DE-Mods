"""Reconcile the initial candidate inventory with reviewed source, without editing runtime files."""
from pathlib import Path
import difflib
import json
import subprocess

root = Path(__file__).resolve().parents[2]
path = Path(__file__).with_name('inventory.json')
records = json.loads(path.read_text(encoding='utf-8'))
cache = {}
for record in records:
    file = record['file']
    if file not in cache:
        old = subprocess.check_output(['git', 'show', 'HEAD:' + file], cwd=root).decode('utf-8-sig').splitlines()
        new = (root / file).read_text(encoding='utf-8-sig').splitlines()
        cache[file] = (new, difflib.SequenceMatcher(None, old, new, autojunk=False).get_opcodes())
    new, changes = cache[file]
    index = record['line'] - 1
    for tag, a, b, c, d in changes:
        if a <= index < b:
            if tag == 'equal' or b-a == d-c:
                result = new[c+index-a].strip()
            else:
                result = '\n'.join(new[c:d]).strip()
            record['result'] = result
            record['decision'] = ('replace: action/selection requires a living unit'
                if 'UnitAccess.IsReallyAlive(' in result or 'GetAllReallyAliveUnits(' in result
                else 'retain: non-unit, state/initialization, corpse or diagnostic contract')
            break
path.write_bytes((json.dumps(records, indent=2, ensure_ascii=False)+'\n').replace('\n','\r\n').encode('utf-8'))
print(f'Reconciled {len(records)} original candidate checks against final source.')
