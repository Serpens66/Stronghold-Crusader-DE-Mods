"""Targeted migration; retain an inventory of every original life-state check."""
from pathlib import Path
import re
import json

ROOT = Path(__file__).resolve().parents[2]
EXCLUDED = {'bin', 'obj', 'tests', 'BepInEx'}
unit_names = {'unit', 'hunter', 'prey', 'otherHunter', 'attacker', 'healer',
              'chicken', 'lord', 'recruitedUnit', 'currentUnit', 'snapshotUnit',
              'preferred', 'selected', 'occupant', 'assignedUnit'}
# These checks describe initialization, corpse visuals, or unfiltered observations.
preserved = {
    ('Testmods/VirtualUnitsPrototype/src/VirtualEntityRuntime.cs', 255),
    ('Testmods/VirtualUnitsPrototype/src/VirtualEntityRuntime.cs', 641),
    ('Testmods/VirtualUnitsPrototype/src/VirtualEntityRuntime.cs', 845),
    ('Testmods/VirtualUnitsPrototype/src/VirtualEntityRuntime.cs', 879),
    ('BugfixesAndQoL/src/CorruptLordDataSpawnRuntime.cs', 340),
}
diagnostic_sources = {
    'HunterActiveTargetVisibilitySnapshot.cs', 'HunterNativeVisibilityProbe.cs',
    'HunterPclReachabilityDiagnostic.cs',
}
pattern = re.compile(r'\b(\w+)(->|\.)r_AliveState\s*(==|!=)\s*AliveState\.IsAlive')
records = []
changed = []
for mod in ROOT.iterdir():
    if not mod.is_dir() or mod.name.startswith(('.', '_')) or mod.name in ('shcde-script-extender', 'Helpers'):
        continue
    for path in mod.rglob('*.cs'):
        if 'src' not in path.parts or EXCLUDED.intersection(path.relative_to(ROOT).parts):
            continue
        relative = path.relative_to(ROOT).as_posix()
        text = path.read_text(encoding='utf-8-sig')
        lines = text.splitlines(keepends=True)
        for index, line in enumerate(lines):
            if not re.search(r'\bIsAlive\b|GetAllAliveUnits|GetAllUnits\(', line):
                continue
            decision = 'retain: non-unit, state/initialization, corpse or diagnostic contract'
            if path.name != 'UnitAccess.cs' and path.name not in diagnostic_sources and (relative, index+1) not in preserved:
                def replace(match):
                    name, access, op = match.groups()
                    allowed = name in unit_names
                    if name == 'target':
                        allowed = relative in ('APIShared/src/UnitCommands/UnitCommandPathRuntime.cs',
                                               'ImprovedHunters/src/ManualChickenAttackPatch.cs',
                                               'APIShared/src/UnitCommands/UnitCommandPathRuntime.cs')
                    if name == 'blocker':
                        allowed = relative == 'Testmods/StockpileAccessFixTest/src/StockpileAccessFixTestRuntime.cs'
                    if not allowed:
                        return match.group(0)
                    # Never replace a state component of a NeedsInit disjunction.
                    if 'NeedsInit' in line:
                        return match.group(0)
                    argument = name if access == '->' else 'in ' + name
                    return ('!' if op == '!=' else '') + 'APIShared.UnitAccess.IsReallyAlive(' + argument + ')'
                line = pattern.sub(replace, line)
                line = re.sub(r'(?:GameUnitManagerAPI\.Instance|unitApi)\.GetAllAliveUnits\(\)',
                              'APIShared.UnitAccess.GetAllReallyAliveUnits()', line)
            if line != lines[index]:
                decision = 'replace: action/selection requires a living unit'
            records.append({'file': relative, 'line': index+1, 'original': lines[index].strip(),
                            'result': line.strip(), 'decision': decision})
            lines[index] = line
        output = ''.join(lines)
        if output != text:
            # Explicit files only; normalize and ordinal-verify with the .NET preflight later.
            path.write_bytes(output.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8'))
            changed.append(relative)
(Path(__file__).parent / 'inventory.json').write_bytes((json.dumps(records, indent=2)+'\r\n').replace('\n','\r\n').replace('\r\r\n','\r\n').encode())
(Path(__file__).parent / 'changed.json').write_text(json.dumps(changed, indent=2), encoding='utf-8')
print(f'Inventoried {len(records)} checks; replaced {sum(r["decision"].startswith("replace") for r in records)} checks in {len(changed)} files.')
for p in changed:
    print(p)
