from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
mod = root / 'Testmods/SpectatorEditorBuildTest'
source = mod / 'src/SpectatorEditorBuildTestPlugin.cs'
s = source.read_text(encoding='utf-8-sig')
usings = s[:s.index('namespace SpectatorEditorBuildTest')]
s = s.replace('public sealed class SpectatorEditorBuildTestPlugin', 'public sealed partial class SpectatorEditorBuildTestPlugin')
# Split one coherent existing runtime by responsibility; no duplicate implementation remains.
groups = {
    'Session': [('        private static long CurrentSessionId', '        private static void EnsureHumanControls')],
    'Hud': [('        private static void EnsureHumanControls', '        private static void AfterSetupModeDependantUI'),
            ('        private static void PositionDate', '        private static unsafe void TrackMoatTile')],
    'Native': [('        private static void OnNativeLibraryLoaded', '        private static long CurrentSessionId'),
               ('        private static void InstallVisualDetours', '        private static unsafe void OnBuildWall')],
    'Placement': [('        private static void AfterSetupModeDependantUI', '        private static void InstallVisualDetours'),
                  ('        private static unsafe void OnBuildWall', '        private static void PositionDate'),
                  ('        private static unsafe void TrackMoatTile', '        private static void LogOnce')]
}
parts = {}
for group, ranges in groups.items():
    parts[group] = ''.join(s[s.index(start):s.index(end)] for start,end in ranges)
cuts = sorted([(s.index(start),s.index(end)) for ranges in groups.values() for start,end in ranges], reverse=True)
for a,b in cuts:
    s = s[:a]+s[b:]
for group,body in parts.items():
    text = usings + 'namespace SpectatorEditorBuildTest\n{\n    public sealed partial class SpectatorEditorBuildTestPlugin\n    {\n' + body + '    }\n}\n'
    (mod/f'src/SpectatorEditorBuildTestPlugin.{group}.cs').write_text(text,encoding='utf-8',newline='\r\n')
source.write_text(s,encoding='utf-8',newline='\r\n')

project = mod/'SpectatorEditorBuildTest.csproj'
p=project.read_text(encoding='utf-8-sig')
p=p.replace('<Compile Include="src\\SpectatorEditorBuildTestPlugin.cs" />','<Compile Include="src\\*.cs" />\n    <Compile Include="..\\..\\Shared\\Runtime\\Localization\\SerpLocalization.cs"><Link>Shared\\SerpLocalization.cs</Link></Compile>')
p=p.replace('<Reference Include="System.Core" />','<Reference Include="System.Core" /><Reference Include="System.Xml" />')
p=p.replace('<Content Include="info.json">','<Content Include="Locales\\*.txt"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>\n    <Content Include="info.json">')
project.write_text(p,encoding='utf-8',newline='\r\n')
