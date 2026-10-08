from pathlib import Path
import subprocess, difflib

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
pairs = {
    'runtime': ('Testmods/FormationTest/src/FormationTestRuntime.cs', 'APIShared/src/UnitCommands/FormationRuntime.cs'),
    'model': ('Testmods/FormationTest/src/FormationModel.cs', 'APIShared/src/UnitCommands/FormationModel.cs'),
    'release': ('Testmods/FormationTest/src/FormationReleaseStateModel.cs', 'APIShared/src/UnitCommands/FormationReleaseStateModel.cs'),
    'packet': ('Testmods/FormationTest/src/FormationOrderPacket.cs', 'APIShared/src/UnitCommands/FormationOrderPacket.cs'),
    'menu': ('Testmods/FormationTest/src/FormationMenuViewModel.cs', 'BugfixesAndQoL/src/FormationMenuViewModel.cs'),
    'overlay': ('Testmods/FormationTest/src/FormationPreviewOverlay.cs', 'BugfixesAndQoL/src/FormationPreviewOverlay.cs'),
    'preview-model': ('Testmods/FormationTest/src/FormationPreviewMarkerModel.cs', 'APIShared/src/UnitCommands/FormationPreviewMarkerModel.cs'),
    'markers': ('BugfixesAndQoL/src/LargeMoveTargetMarkerRenderer.cs', 'APIShared/src/UnitCommands/LargeMoveTargetMarkerRenderer.cs'),
}
for name, (old_path, new_path) in pairs.items():
    old = subprocess.check_output(['git', 'show', 'HEAD:' + old_path], cwd=root).decode('utf-8-sig')
    new = (root / new_path).read_text(encoding='utf-8-sig')
    diff = ''.join(difflib.unified_diff(old.splitlines(True), new.splitlines(True), fromfile='HEAD:' + old_path, tofile=new_path))
    (out / ('review-' + name + '.diff')).write_bytes(diff.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8'))
    print(name, len(diff.splitlines()), 'comparison lines')
