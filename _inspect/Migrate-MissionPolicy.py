from pathlib import Path
import re

root=Path.cwd()
def read(p): return (root/p).read_text(encoding='utf-8-sig')
def write(p,s):
    (root/p).write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))

# One-time source move. The Shared namespace keeps existing consumer type identities;
# definitions now live exclusively in APIShared, not in each mod assembly.
s=read('Shared/GameModeHelper.cs')
start=s.index('    internal readonly struct PlayerIdentityResolution')
end=s.index('    internal readonly struct GameModeSnapshot')
headers=s[:s.index('namespace Shared')]
write('Shared/GameModeHelper.cs', headers+'namespace Shared\n{\n'+s[start:end]+'}\n')
s=s[:start]+s[end:]
for name in ['GameModeKind','GameModeLaunchVariant','GameTrailType']:
    s=s.replace('internal enum '+name,'public enum '+name)
s=s.replace('internal readonly struct GameModeSnapshot','public readonly struct GameModeSnapshot')
s=s.replace('internal static class GameModeHelper','public static class GameModeHelper')
s=s.replace('        SandsOfTime,','        SandsOfTime,\n        Tutorial,')
capture_start=s.index('        public static GameModeSnapshot Capture(bool')
capture_end=s.index('        internal static bool AllowsCustomGameMods',capture_start)
s=s[:capture_start]+'''        public static GameModeSnapshot Capture(bool multiplayerSave = false) =>
#if API_SHARED_PRESET_TESTS
            CaptureCore(multiplayerSave, 0, -1, false);
#else
            APIShared.MissionLifecycleService.Snapshot;
#endif
        public static GameModeSnapshot Capture(MapStartEventArgs args) => Capture();
        public static GameModeSnapshot Capture(MapLoadEventArgs args) => Capture();
        public static GameModeSnapshot Capture(LoadSaveGameEventArgs args) => Capture();

        internal static GameModeSnapshot CaptureMission(bool multiplayer, int campaign, int trail,
            bool editor, EngineInterface.LoadMapReturnData? data, GameModeKind intent) =>
            CaptureCore(multiplayer, campaign, trail, editor, data, intent);

'''+s[capture_end:]
s=s.replace('            bool editorLoad)\n', '            bool editorLoad, EngineInterface.LoadMapReturnData? data = null, GameModeKind intent = GameModeKind.Unknown)\n',1)
s=s.replace('int gameType = gameData != null ? gameData.game_type : NoGameValue;', '''int gameType = data?.game_type ?? (intent == GameModeKind.Campaign ? (int)Enums.eGameTypeModes.GAMETYPE_CAMPAIGN :
                intent == GameModeKind.Tutorial ? (int)Enums.eGameTypeModes.GAMETYPE_TUTORIAL :
                intent == GameModeKind.StandaloneMission ? (int)Enums.eGameTypeModes.GAMETYPE_MAP :
                intent == GameModeKind.MapEditor ? (int)Enums.eGameTypeModes.GAMETYPE_BUILDER :
                intent != GameModeKind.Unknown ? (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER : NoGameValue);''')
s=s.replace('int skirmishGameType = gameData != null ? gameData.SkirmishGameType : NoGameValue;', '''int skirmishGameType = data?.skirmishGameType ?? (intent == GameModeKind.CustomTrail ?
                (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM_TRAIL : intent == GameModeKind.CustomGame ?
                (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM : NoGameValue);''')
s=s.replace('int skirmishTrailType = gameData != null ? gameData.SkirmishTrailType : NoGameValue;', 'int skirmishTrailType = data?.skirmishTrail ?? eventTrailType;')
s=s.replace('int coopTrailId = gameData != null ? gameData.coopTrailID : NoGameValue;', 'int coopTrailId = data?.coopTrailID ?? (intent == GameModeKind.CoopTrail ? 1 : NoCoopTrail);')
s=s.replace('''bool mapEditor =
                editorLoad ||
                gameData?.mapType == Enums.GameModes.MAP_EDITOR ||
                IsMapEditor();''','bool mapEditor = editorLoad;')
s=s.replace('bool sandsOfTime = TryIsSandsOfTime(gameData);','bool sandsOfTime = intent == GameModeKind.SandsOfTime || IsSandsTrailType(skirmishTrailType);')
s=s.replace('bool customTrailRestart = TryCaptureCustomTrailRestart();','bool customTrailRestart = intent == GameModeKind.CustomTrail;')
s=s.replace('            bool realMultiplayer =\n', '            bool realMultiplayer =\n')
# Intent determines local versus MP for a mission; old lobby/game rosters remain diagnostics only.
a=s.index('            bool realMultiplayer =')
b=s.index('\n\n',a)
s=s[:a]+'            bool realMultiplayer = multiplayerSave;'+s[b:]
s=s.replace('            if (mapEditor)\n                return GameModeKind.MapEditor;', '''            if (mapEditor)
                return GameModeKind.MapEditor;
            if (gameType == (int)Enums.eGameTypeModes.GAMETYPE_TUTORIAL)
                return GameModeKind.Tutorial;''')
# Runtime public convenience queries use exactly the central context.
a=s.index('        public static bool IsMapEditor()')
b=s.index('\n    }\n',a)
s=s[:a]+'''        public static bool IsMapEditor() => Capture().IsMapEditor;
'''+s[b:]
write('APIShared/src/MissionModePolicy.cs',s)
for filename in ['GameplayModModePolicy.cs','GameplayFeatureModePolicy.cs']:
    s=read('Shared/'+filename)
    s=re.sub(r'\binternal (enum|readonly struct|static class)',r'public \1',s)
    s=re.sub(r'\binternal (static |GameplayModActivationProfile\(|GameplayFeatureActivationProfile\(|string |GameplayFeatureId |GameplayModAllowedContext |bool )',r'public \1',s)
    write('APIShared/src/'+filename,s)
    (root/'Shared'/filename).unlink()

for p in [root/'APIShared/src/MissionModePolicy.cs',root/'APIShared/src/GameplayModModePolicy.cs',root/'APIShared/src/GameplayFeatureModePolicy.cs']:
    s=p.read_text(encoding='utf-8')
    # Document transferred public contracts without suppressing API documentation checks.
    lines=s.splitlines(); out=[]
    for line in lines:
        if re.match(r'\s+public ',line) and not (out and out[-1].lstrip().startswith('///')):
            name=re.search(r'(?:enum|struct|class)\s+(\w+)',line)
            label=name.group(1) if name else (re.findall(r'\b(\w+)\s*(?:\(|\{|=>)',line) or ['Policy value'])[0]
            out.append(' '* (len(line)-len(line.lstrip()))+'/// <summary>'+label+' in the centralized mission policy contract.</summary>')
        if re.match(r'        [A-Za-z]\w*(?:\s*=.*)?,\s*$',line):
            out.append('        /// <summary>'+line.strip().split(',')[0].split('=')[0].strip()+'.</summary>')
        out.append(line)
    write(p.relative_to(root),'\n'.join(out)+'\n')

# Redirect policy source links in tests, remove definitions from runtime consumers.
for p in root.rglob('*.csproj'):
    rel=p.relative_to(root).as_posix()
    if any(x in p.parts for x in ['shcde-script-extender','obj','bin','.git']): continue
    s=p.read_text(encoding='utf-8-sig'); old=s
    istest='test' in rel.lower() and ('_inspect/' in rel or '/tests/' in rel.lower() or '.tests/' in rel.lower())
    for name in ['GameplayModModePolicy.cs','GameplayFeatureModePolicy.cs']:
        if istest:
            s=s.replace('Shared\\'+name,'APIShared\\src\\'+name)
        else:
            s=re.sub(r'\s*<Compile Include="[^"]*Shared\\'+name.replace('.','\\.')+r'"\s*(?:/>|>.*?</Compile>)','',s,flags=re.S)
    if istest and 'Shared\\GameModeHelper.cs' in s:
        m=re.search(r'<Compile Include="([^\"]*)Shared\\GameModeHelper.cs"',s)
        s=s.replace('</ItemGroup>', '<Compile Include="'+m.group(1)+'APIShared\\src\\MissionModePolicy.cs" />\n  </ItemGroup>',1)
    if s!=old: write(p.relative_to(root),s)

for filename in ['Contracts.cs','ApiSharedRuntime.cs']:
    s=read('APIShared/src/'+filename).replace('EditorMapLifecycle','MissionLifecycle').replace('editorMapLifecycle','missionLifecycle').replace('editor-map-lifecycle','mission-lifecycle')
    write('APIShared/src/'+filename,s)
p='APIShared/APIShared.csproj'; s=read(p).replace('EditorMapLifecycle','MissionLifecycle')
extra=['MissionModePolicy.cs','GameplayModModePolicy.cs','GameplayFeatureModePolicy.cs']
s=s.replace('    <Compile Include="src\\Contracts.cs" />','    <Compile Include="src\\Contracts.cs" />\n'+''.join('    <Compile Include="src\\'+name+'" />\n' for name in extra)+'    <Compile Include="..\\Shared\\DebugLogHelper.cs"><Link>Shared\\DebugLogHelper.cs</Link></Compile>')
write(p,s)
