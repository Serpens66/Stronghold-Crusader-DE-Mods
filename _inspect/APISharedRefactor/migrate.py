"""One-time, bounded APIShared source migration; retained as an audit artifact."""
from pathlib import Path
import re, subprocess, json, hashlib

ROOT = Path(__file__).resolve().parents[2]
def read(p):
    return p.read_text(encoding='utf-8-sig')
def write(p, text):
    p.parent.mkdir(parents=True, exist_ok=True)
    data = text.replace('\r\n', '\n').replace('\r', '\n').replace('\n', '\r\n').encode('utf-8')
    p.write_bytes(data)
    assert p.read_bytes() == data
    assert not re.search(rb'(?<!\r)\n', data)

tracked = subprocess.check_output(['git', 'ls-files'], cwd=ROOT, text=True).splitlines()
status = subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True)
write(ROOT/'_inspect/APISharedRefactor/starting-state.txt', subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True)+status)
assert not [line for line in status.splitlines() if '_inspect/APISharedRefactor/' not in line], 'Inspect and preserve existing changes before this one-time migration.'

domains = {
    'ModSettings': ['DynamicPresetSettings', 'ModSettingsApplication', 'ModSettingsSearch', 'ModSettingsPresetDocuments', 'PresetLobbyModSettingsViewModel'],
    'GameModes': ['MissionModePolicy', 'GameplayModModePolicy'],
    'SerpsMods': ['GameplayFeatureModePolicy'],
    'Core': ['APISharedPlugin', 'ApiSharedRuntime', 'Contracts', 'NativeInfrastructure', 'UniquePatternSearch'],
    'Missions': ['MissionLifecycleContracts','MissionLifecycleState','MissionLifecycleCapability'],
    'Lobby': ['LobbyStateContracts','LobbyStateCapability','LobbyPreparationOverride'],
    'Players': ['PlayerDefeatContracts','PlayerDefeatState','PlayerDefeatCapability','PlayerPerspectiveAPI'],
    'Units': ['UnitAccess','LocalSelectionAPI','MarkedUnitSelectionAPI'],
    'Presentation': ['UnitHudContracts','UnitHudPresentationCapability','BriefingGoldContracts','BriefingGoldPresentationCapability'],
    'Buildings': ['BuildingRepairContracts','BuildingRepairCapability','GatehouseDistanceOriginCapability','GatehouseTimingCapability','GatehousePermanentRuntimeState','GatehouseAutomationNativeState','GatehouseDrawbridgeCoupling'],
    'Pathfinding': ['AssassinPathAPI','AssassinAttackControlAPI','AssassinAttackNativeContract','AssassinRouteHandoff','AssassinGateTransitionPolicy','AssassinEndpointEmitter','AssassinPathNativeDefinition','EnemyGatePathPolicyBridge','EnemyBridgeDiagnosticBridge','TemporaryGateRouteAcceptanceBridge','ElevatedMoatAiState'],
    'Diagnostics': ['AivBuildStepContracts','AivBuildStepCapability','AiBuildDiagnostic'],
    'Savegames': ['SavegameModSettings'],
}
types = {}
for domain in ['ModSettings','GameModes','SerpsMods']:
    for name in domains[domain]:
        text = read(ROOT/f'APIShared/src/{name}.cs')
        for type_name in re.findall(r'^    (?:public|internal)\s+(?:(?:static|sealed|abstract|readonly)\s+)*(?:class|struct|enum|interface)\s+(\w+)', text, re.M):
            types[type_name] = 'APIShared.' + domain

# The GUID whitelist is Serps policy; the profile type and evaluator are general.
policy = read(ROOT/'APIShared/src/GameplayModModePolicy.cs')
factory_start = policy.index('        private const GameplayModAllowedContext RegularContexts')
factory_end = policy.index('        /// <summary>IsAllowed', factory_start)
factory = policy[factory_start:factory_end]
create_start = policy.index('        private static GameplayModActivationProfile Create')
create_end = policy.index('        /// <summary>ResolveContext', create_start)
factory += policy[create_start:create_end]
policy = policy[:factory_start]+policy[factory_end:create_start]+policy[create_end:]
factory = factory.replace('return Create(modGuid, displayName);', 'return new GameplayModActivationProfile(modGuid, displayName, RegularContexts);')
factory = factory[:factory.index('        private static GameplayModActivationProfile Create')]
factory = factory.replace('GetProfile in the centralized mission policy contract.', 'Gets the established Serps gameplay profile. Unknown mod GUIDs are rejected; third-party mods construct their own profile.')
write(ROOT/'APIShared/src/SerpsMods/SerpsModProfiles.cs', 'using System;\nusing APIShared.GameModes;\n\nnamespace APIShared.SerpsMods\n{\n    /// <summary>Established permissions for Serps mods, separate from the optional general evaluator.</summary>\n    public static class SerpsModProfiles\n    {\n'+factory+'    }\n}\n')
types['GameplayModModePolicy'] = 'APIShared.GameModes'
types['SerpsModProfiles'] = 'APIShared.SerpsMods'
write(ROOT/'APIShared/src/GameplayModModePolicy.cs', policy)

all_paths = [ROOT/p for p in tracked if Path(p).suffix in {'.cs','.csproj','.ps1','.xaml','.md'}
             and not p.startswith(('shcde-script-extender/','HD sources/','.native-analysis/','.tools/'))
             and not any(x in Path(p).parts for x in ['BepInEx','obj','bin'])
             and Path(p).name != 'AGENTS.md'
             and (Path(p).suffix != '.md' or p.startswith('APIShared/'))]
mapping = {name+'.cs': domain for domain,names in domains.items() for name in names}
changed = []
for p in all_paths:
    old = text = read(p)
    if p.suffix == '.cs':
        if p.parent == ROOT/'APIShared/src' and p.stem in sum([domains[d] for d in ['ModSettings','GameModes','SerpsMods']], []):
            domain = mapping[p.name]
            text = text.replace('namespace Shared', 'namespace APIShared.'+domain)
        text = text.replace('GameplayModModePolicy.GetProfile', 'SerpsModProfiles.GetProfile')
        for name, ns in types.items():
            text = re.sub(r'\bShared\.'+re.escape(name)+r'\b', ns+'.'+name, text)
        imports = set(ns for name,ns in types.items() if re.search(r'\b'+re.escape(name)+r'\b', text))
        if p.parts[-3:-1] != ('APIShared','src') and 'namespace Shared' in text:
            pass # Local helpers retain Shared; the imports select APIShared-owned types.
        if p.is_relative_to(ROOT/'APIShared/src'):
            imports.add('Shared') # linked internal parser/logging/player-identity helpers
        # Import only at compilation-unit level, before any conditional imports.
        needed = [ns for ns in sorted(imports) if not re.search(r'^using '+re.escape(ns)+r';', text, re.M)]
        text = ''.join('using '+ns+';\n' for ns in needed)+text
    if p.suffix == '.xaml':
        # APIShared only owns the moved ModSettings types in these XAML namespaces.
        text = text.replace('clr-namespace:Shared;assembly=APIShared', 'clr-namespace:APIShared.ModSettings;assembly=APIShared')
    # Update source links and static tests, including Path.Combine inventories.
    for filename,domain in mapping.items():
        text = text.replace('src\\'+filename, 'src\\'+domain+'\\'+filename)
        text = text.replace('src/'+filename, 'src/'+domain+'/'+filename)
        text = text.replace('"src", "'+filename+'"', '"src", "'+domain+'", "'+filename+'"')
        text = text.replace('"src",\n                "'+filename+'"', '"src", "'+domain+'",\n                "'+filename+'"')
    if text != old:
        write(p,text)
        changed.append(str(p.relative_to(ROOT)))

for filename,domain in mapping.items():
    source = ROOT/'APIShared/src'/filename
    target = source.parent/domain/filename
    target.parent.mkdir(parents=True,exist_ok=True)
    source.rename(target)

# Split by existing type/member boundaries: executable statements are retained verbatim.
p = ROOT/'APIShared/src/ModSettings/PresetLobbyModSettingsViewModel.cs'
text = read(p)
header = text[:text.index('namespace APIShared.ModSettings')]
first_end = text.index('\nnamespace APIShared.ModSettings',text.index('namespace APIShared.ModSettings')+1)
first = text[text.index('namespace APIShared.ModSettings'):first_end]
write(p.parent/'PerPlayerLobbySettings.cs', header+first+'\n')
second = text[first_end+1:]
controller_start = second.index('        private sealed class PresetController')
controller_end = second.index('\n    }\n\n#if !API_SHARED_PRESET_TESTS', controller_start)
controller = second[controller_start:controller_end]
second = second[:controller_start]+second[controller_end:]
second = second.replace('public abstract class PresetLobbyModSettingsViewModel', 'public abstract partial class PresetLobbyModSettingsViewModel')
write(p.parent/'PresetLobbyModSettingsViewModel.Persistence.cs', header+'namespace APIShared.ModSettings\n{\n    public abstract partial class PresetLobbyModSettingsViewModel\n    {\n'+controller+'\n    }\n}\n')
registration_start = second.index('#if !API_SHARED_PRESET_TESTS\n    internal static class ModSettingsHorizontalFocusScrollGuard')
registration = second[registration_start:]
write(p.parent/'LobbyModSettingsPresetRegistration.cs', header+'namespace APIShared.ModSettings\n{\n'+registration)
second = second[:registration_start]+'}\n'
# Extract source discovery/search UI members into a partial class.
ui_start = second.index('#if !API_SHARED_PRESET_TESTS\n        public string System_PresetLoadText')
ui_end = second.index('#endif', ui_start)+len('#endif')
ui = second[ui_start:ui_end]
write(p.parent/'PresetLobbyModSettingsViewModel.Sources.cs',header+'namespace APIShared.ModSettings\n{\n    public abstract partial class PresetLobbyModSettingsViewModel\n    {\n'+ui+'\n    }\n}\n')
second = second[:ui_start]+second[ui_end:]
write(p,header+second)

new_files = ['PerPlayerLobbySettings.cs','PresetLobbyModSettingsViewModel.Persistence.cs','PresetLobbyModSettingsViewModel.Sources.cs','LobbyModSettingsPresetRegistration.cs']
for p in [ROOT/x for x in tracked if x.endswith('.csproj') and not x.startswith('shcde-script-extender/')]:
    text = read(p)
    match = re.search(r'<Compile Include="([^"]*src\\ModSettings\\)PresetLobbyModSettingsViewModel.cs"(?:\s*/>|>.*?</Compile>)',text,re.S)
    if match:
        additions = ''.join('    <Compile Include="'+match.group(1)+name+'" />\n' for name in new_files)
        text = text[:match.end()]+ '\n'+additions+text[match.end():]
        write(p,text)
        changed.append(str(p.relative_to(ROOT)))

p = ROOT/'APIShared/APIShared.csproj'
text = read(p).replace('<Compile Include="src\\Core\\Contracts.cs" />','<Compile Include="src\\Core\\Contracts.cs" />\n    <Compile Include="src\\Core\\ModApiClient.cs" />\n    <Compile Include="src\\SerpsMods\\SerpsModProfiles.cs" />')
write(p,text)
for p in [ROOT/x for x in tracked if x.endswith('.csproj') and not x.startswith('shcde-script-extender/')]:
    text = read(p)
    match = re.search(r'<Compile Include="([^"]*src\\GameModes\\)GameplayModModePolicy.cs"(?:\s*/>|>.*?</Compile>)',text,re.S)
    if match:
        prefix = match.group(1).replace('GameModes\\','SerpsMods\\')
        write(p,text[:match.end()]+'\n    <Compile Include="'+prefix+'SerpsModProfiles.cs" />'+text[match.end():])

p = ROOT/'APIShared/src/UnitCommands/UnitCommandPathAPI.cs'
write(p,read(p).replace('public static class UnitCommandPathAPI','internal static class UnitCommandPathAPI'))
write(ROOT/'_inspect/APISharedRefactor/migration-files.json',json.dumps(sorted(set(changed)),indent=2)+'\n')
print('Migrated',len(set(changed)),'consumer/source files and',len(mapping),'APIShared source paths.')
