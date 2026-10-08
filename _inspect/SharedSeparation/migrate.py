"""One-time, explicit path migration. Does not touch the Script Extender fork."""
from pathlib import Path
import subprocess, re, json, os

root = Path(__file__).resolve().parents[2]
audit = Path(__file__).resolve().parent
groups = {
    'Runtime/Diagnostics': ['DebugLogHelper', 'CrashBreadcrumbDiagnostics', 'CrashBreadcrumbRecorder'],
    'Runtime/Threading': ['UnityMainThreadDispatch', 'DeferredSnapshotRefreshRequest'],
    'Runtime/Persistence': ['AtomicFileReplacement', 'DependencyFreeJson'],
    'Runtime/Gameplay': ['ActivePlayerHelper', 'ActivePlayerKeepReadiness', 'GameProjectileSlotPolicy', 'GatehouseQueryUnitIdPolicy', 'PathDecisionAggregate', 'RecruitmentHookContext', 'RecruitmentRequestPolicy', 'SelectedChimpsSnapshotPolicy', 'GroundMovePreviewEligibility'],
    'Runtime/Native': ['GameBuildingFootprint', 'NativeBuildingCompoundGroup', 'NativePatternResolver', 'TemporaryPackedRouteInspection'],
    'Runtime/UI': ['AiSettingsHelpHover', 'LordPortraitPalette', 'NumericTextInput', 'ToolTipPresentation', 'TroopActionButtonLayout', 'TroopActionButtonLayoutPolicy'],
    'Runtime/Localization': ['SerpLocalization'],
    'Runtime/Workshop': ['WorkshopContentPaths', 'WorkshopUploadStaging'],
    'Adapters/APIShared': ['GameModeHelper', 'GameplaySessionLifecycle', 'GameplayModActivationGate', 'LobbyLifecycle'],
}
moves = {f'Shared/{name}.cs': f'Shared/{folder}/{name}.cs' for folder, names in groups.items() for name in names}
folders = {'Release': 'Tools/Release', 'Steam': 'Tools/Steam', 'ScriptExtenderUpdate': 'Tools/ScriptExtenderUpdate', 'DocumentationImages': 'Tools/Documentation', 'Threading.Tests': 'Tests/Threading.Tests', 'UnitCommandSourceChecks': 'Tools/Validation/UnitCommandSourceChecks'}
for old, new in folders.items():
    for path in (root/'Shared'/old).rglob('*'):
        if path.is_file() and not any(p in ('bin', 'obj') for p in path.parts):
            moves[path.relative_to(root).as_posix()] = 'Shared/'+new+'/'+path.relative_to(root/'Shared'/old).as_posix()
for name in ['Test-PermanentNativeRuntimePatches', 'Test-UnitAccess', 'Test-UnitCommandSplit']:
    moves[f'Shared/{name}.ps1'] = f'Shared/Tools/Validation/{name}.ps1'
moves['Shared/UnitCommandSplit-Validation.md'] = 'Shared/Docs/UnitCommandSplit-Validation.md'
for old, new in moves.items():
    assert (root/old).resolve().is_relative_to(root), old
    assert (root/new).resolve().is_relative_to(root), new
for old, new in folders.items():
    assert not (root/'Shared'/old).is_symlink(), old
    assert (root/'Shared'/new).resolve().is_relative_to(root), new

api_copies = {
    'AtomicFileReplacement': 'ModSettings/Internal/AtomicFileReplacement.cs',
    'UnityMainThreadDispatch': 'Core/Internal/UnityMainThreadDispatch.cs',
    'GroundMovePreviewEligibility': 'UnitCommands/Internal/GroundMovePreviewEligibility.cs',
    'DebugLogHelper': 'Core/Internal/DebugLogHelper.cs',
    'DependencyFreeJson': 'ModSettings/Internal/DependencyFreeJson.cs',
    'GameModeHelper': 'GameModes/Internal/PlayerIdentityHelper.cs',
    'GameplaySessionLifecycle': 'Missions/Internal/MissionEventRelay.cs',
    'ToolTipPresentation': 'ModSettings/Internal/ToolTipPresentation.cs',
    'NativePatternResolver': 'Core/Internal/NativePatternResolver.cs',
    'GameBuildingFootprint': 'Buildings/Internal/GameBuildingFootprint.cs',
    'TemporaryPackedRouteInspection': 'UnitCommands/Internal/TemporaryPackedRouteInspection.cs',
}

def read(path):
    return path.read_bytes().decode('utf-8-sig')

def write(path, text):
    expected = text.replace('\r\n', '\n').replace('\n', '\r\n')
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(expected.encode('utf-8'))
    assert read(path) == expected, path
    assert not re.search(r'(?<!\r)\n', read(path)), path

# Own API implementations, initialized from the audited source without behavior changes.
for name, target in api_copies.items():
    original = read(root/'Shared'/f'{name}.cs')
    text = original.replace('namespace Shared', 'namespace APIShared.Internal')
    text = ('// APIShared-owned implementation; independent of workspace Shared helpers.\n'
            '// Initial provenance: a7888900e, Shared/'+name+'.cs. No automatic synchronization.\n'+text)
    write(root/'APIShared/src'/target, text)

tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=root).decode().split('\0')
files = {root/p for p in tracked if p}
for base, dirs, names in os.walk(root):
    dirs[:] = [d for d in dirs if d not in {'.git', '.tools', '.native-analysis', 'bin', 'obj', 'packages', 'shcde-script-extender', '.release-output', 'x86_64', 'BepInEx'}]
    for name in names:
        if name.endswith(('.csproj', '.ps1', '.bat', '.cmd', '.props', '.targets')) or name == 'AGENTS.md':
            files.add(Path(base)/name)
files.update(root/'APIShared/src'/p for p in api_copies.values())

suffixes = {'.cs', '.csproj', '.ps1', '.bat', '.cmd', '.props', '.targets', '.md', '.json', '.yml', '.yaml', '.xaml', '.txt'}
folder_replacements = {'Shared/'+old: 'Shared/'+new for old, new in folders.items()}
replacements = dict(moves)
replacements.update(folder_replacements)
changed = []
for path in sorted(files):
    if not path.is_file() or path.suffix.lower() not in suffixes:
        continue
    rel = path.relative_to(root).as_posix()
    if any(p in {'bin', 'obj', 'BepInEx', 'shcde-script-extender'} for p in path.relative_to(root).parts):
        continue
    # Preserve immutable baseline/export provenance and previous one-time migration scripts.
    if rel.startswith(('_inspect/CrusaderDE-Native-Baseline/', '_inspect/APISharedRefactor/', '_inspect/SharedSeparation/')):
        continue
    try:
        original = read(path)
    except UnicodeDecodeError:
        continue
    text = original
    new_rel = moves.get(rel, rel)
    new_path = root/new_rel
    if path.suffix in {'.csproj', '.props', '.targets'}:
        def include(match):
            value = match.group(2)
            if '$(' in value or '*' in value:
                return match.group(0)
            absolute = (path.parent / value.replace('\\', '/')).resolve()
            try:
                target = absolute.relative_to(root).as_posix()
            except ValueError:
                return match.group(0)
            if rel == 'APIShared/APIShared.csproj' and target in moves and Path(target).stem in api_copies:
                target = 'APIShared/src/'+api_copies[Path(target).stem]
            else:
                target = moves.get(target, target)
            value = os.path.relpath(root/target, new_path.parent).replace('/', '\\')
            return match.group(1)+value+match.group(3)
        text = re.sub(r'((?:Include|Remove|Update)=")([^"]+)(")', include, text)
    for old, new in sorted(replacements.items(), key=lambda p: -len(p[0])):
        for separator in ['/', '\\\\', '\\']:
            text = text.replace(old.replace('/', separator), new.replace('/', separator))
    if rel.startswith('APIShared/') and path.suffix == '.cs':
        text = text.replace('using Shared;', 'using APIShared.Internal;')
        text = re.sub(r'\bShared\.(?=[A-Z])', 'APIShared.Internal.', text)
        # Preserve registration identity / replay ownership across this structural migration.
        text = text.replace('"APIShared.Internal.MissionEvents."', '"Shared.MissionEvents."')
    if rel.startswith('Shared/') and path.suffix == '.ps1':
        if rel.startswith(tuple('Shared/'+p+'/' for p in ('Release', 'Steam', 'ScriptExtenderUpdate', 'DocumentationImages'))):
            text = text.replace("Join-Path $PSScriptRoot '..\\..'", "Join-Path $PSScriptRoot '..\\..\\..'")
            text = text.replace('Split-Path -Parent (Split-Path -Parent $PSScriptRoot)', 'Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))')
            text = text.replace('Split-Path -Parent (Split-Path -Parent $scriptRoot)', 'Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $scriptRoot))')
        elif rel in moves and '/Tools/Validation/' in new_rel:
            text = text.replace('Split-Path -Parent $PSScriptRoot', "[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\\..\\..'))")
    if text != original or new_rel != rel:
        write(new_path, text)
        changed.append(new_rel)

# Relocate remaining files/directories (including local caches, without deleting them).
for old, new in folders.items():
    source, destination = root/'Shared'/old, root/'Shared'/new
    for path in source.rglob('*'):
        if path.is_file():
            target = destination/path.relative_to(source)
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists():
                path.unlink()  # Only the exact source already written/read-back above.
            else:
                path.rename(target)
    for path in sorted(source.rglob('*'), key=lambda p: -len(p.parts)):
        if path.is_dir() and not any(path.iterdir()): path.rmdir()
    if not any(source.iterdir()): source.rmdir()
for old, new in moves.items():
    source = root/old
    if source.exists():
        target = root/new
        if target.exists(): source.unlink()
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            source.rename(target)

# API test harnesses source-compile internals. Give them the API-owned implementations too.
# Keep their mod-side sources for explicit consumer/compatibility tests.
for project in sorted(files):
    new_project = root/moves.get(project.relative_to(root).as_posix(), project.relative_to(root).as_posix())
    if not new_project.exists() or project.suffix != '.csproj' or new_project == root/'APIShared/APIShared.csproj': continue
    text = read(new_project)
    if re.search(r'<Compile\s+Include="[^"]*APIShared[\\/]src[\\/]', text):
        extra = []
        for name, target in api_copies.items():
            if re.search(r'<Compile\s+Include="[^"]*[\\/]'+re.escape(name)+r'\.cs"', text):
                include_path = os.path.relpath(root/'APIShared/src'/target, new_project.parent).replace('/', '\\')
                if include_path not in text:
                    extra.append('    <Compile Include="'+include_path+'" />')
        if extra:
            text = text.replace('</Project>', '  <ItemGroup>\n'+'\n'.join(extra)+'\n  </ItemGroup>\n</Project>')
            write(new_project, text)
            changed.append(new_project.relative_to(root).as_posix())

write(audit/'migration-map.json', json.dumps({'moves': moves, 'apiCopies': api_copies, 'changed': sorted(set(changed))}, indent=2)+'\n')
print(f'Migrated {len(moves)} paths; updated {len(set(changed))} files; API owns {len(api_copies)} independent helpers.')
