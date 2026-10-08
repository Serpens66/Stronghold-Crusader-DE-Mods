from pathlib import Path
exec(Path('_inspect/FormationRows/implement.py').read_text().split("p='APIShared/src/UnitCommands/FormationModel.cs'")[0])
p='APIShared/src/UnitCommands/FormationRuntime.cs';s=read(p)
baseline=(root/'_inspect/FormationRows/before'/p).read_text(encoding='utf-8-sig')
start=s.index('        private NativeDestination[] CaptureVanillaDestinations(');end=s.index('        private static NativeDestination[] SnapSlots(',start)
bs=baseline.index('        private NativeDestination[] CaptureVanillaDestinations(');be=baseline.index('        private static NativeDestination[] SnapSlots(',bs)
s=s[:start]+baseline[bs:be]+s[end:];write(p,s)
p='BugfixesAndQoL/tests/Formations.Tests/Program.cs';s=read(p).replace('var line = FormationModel.BuildRelativeSlots(FormationKind.Line, 31, 31, 4, 1);','var line = FormationModel.BuildRelativeSlots(FormationKind.Line, 31, 1, 4, 1);');write(p,s)
for f in (root/'BugfixesAndQoL/Locales').glob('*.txt'):
    p=str(f.relative_to(root));s=read(p)
    if f.stem == 'de-DE':
        help='Waehle Formation, Dichte und Rollenplatzierung im Truppen-HUD. Halte die konfigurierte Move-Maustaste ueber freiem Boden gedrueckt und ziehe fuer die Blickrichtung. Mausrad hoch entfernt eine Reihe (breiter), runter fuegt eine Reihe hinzu (laenger). Kreis und Vanilla behalten ihre Geometrie. Dichte wird nur im Fenster eingestellt. Loslassen erteilt genau einen synchronisierten Move. Erfordert mindestens zwei ausgewaehlte Einheiten. Shift-Wegpunkte behalten ihr bisheriges Verhalten.'
    else:
        help='Choose formation, density and role placement in the troop HUD. Hold the configured Move mouse button over open ground and drag to set facing. Wheel up removes a row (wider); wheel down adds a row (longer). Circle and Vanilla keep their geometry. Adjust density in the window only. Release issues one synchronized Move. Requires at least two selected units. Shift waypoints retain their existing behavior.'
    s,n=re.subn(r'(?m)^BugfixesAndQoL.EnableMoveFormationEnhancementsHelp=.*$',lambda m:'BugfixesAndQoL.EnableMoveFormationEnhancementsHelp='+help,s);assert n==1
    write(p,s)
for p in ['APIShared/UpdateToNewDLL.md','BugfixesAndQoL/UpdateToNewDLL.md']:
    s=read(p)
    s+='\nFormation rows follow-up (2026-10-08): internal protocol 6 adds ushort Rows. Wheel adjusts gesture-local rows; drag only sets the eight-sector facing. Width is validated against maximum rank width. Integer local coordinates precede rotation; exact reachable slots are reserved before obstacle fallback. Existing native selectors, permanent input/renderer hooks and installed SHCDESE 2.14.0 contracts are unchanged. No new public API or game-assembly access.\n'
    write(p,s)
