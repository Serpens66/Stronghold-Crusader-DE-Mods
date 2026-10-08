from pathlib import Path
import re, json
root=Path.cwd()
def read(p): return p.read_text(encoding='utf-8-sig')
def write(p,t):
    p.parent.mkdir(parents=True,exist_ok=True)
    data=t.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'); p.write_bytes(data); assert p.read_bytes()==data
for name,method in [('APISharedTests','TestWorkspaceIntegration'),('LobbyModSettingsPresetTests','TestWorkspacePublishedReleases')]:
    directory=root/'_inspect'/name
    source=read(directory/'WorkspaceIntegration.cs').replace('internal static partial class Program','internal static class Program').replace('static partial void '+method+'()', 'private static void '+method+'()')
    # The independent workspace harness is concerned only with its own consumers.
    source='\n'.join(line for line in source.splitlines() if not line.startswith('using APIShared') and not line.startswith('using CrusaderDE') and not line.startswith('using Iced') and not line.startswith('using MessagePack') and not line.startswith('using SHCDESE'))+'\n'
    pos=source.rfind('    }')
    helper=f'''        private static int Main()
        {{
            try {{ {method}(); Console.WriteLine("PASS: {name} workspace integration."); return 0; }}
            catch (Exception error) {{ Console.Error.WriteLine(error); return 1; }}
        }}
        private static void Assert(bool condition, string message)
        {{
            if (!condition) throw new InvalidOperationException(message);
        }}
'''
    if name=='APISharedTests':
        helper+='''        private static int Count(string value, string fragment)
        {
            int count=0, position=0;
            while ((position=value.IndexOf(fragment,position,StringComparison.Ordinal))>=0)
            { count++; position+=fragment.Length; }
            return count;
        }
'''
    source=source[:pos]+helper+source[pos:]
    write(directory/'WorkspaceIntegration.cs',source)
    write(directory/(name+'.csproj'),f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net481</TargetFramework>
    <AssemblyName>{name}</AssemblyName>
  </PropertyGroup>
</Project>
''')
write(root/'Shared/Tools/Validation/Test-APISharedConsumers.ps1',r'''[CmdletBinding()]
param([string]$GameDir, [string]$ExtenderDir)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
if (-not $GameDir) { $GameDir = $env:SHCDE_GAME_DIR }
if (-not $GameDir) { $GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition' }
if (-not $ExtenderDir) { $ExtenderDir = Join-Path $GameDir 'BepInEx\plugins\000shcdese' }
foreach ($script in @('Test-SharedBoundaries.ps1','Test-UnitCommandSplit.ps1','Test-UnitAccess.ps1')) {
    & (Join-Path $PSScriptRoot $script)
    if (-not $?) { throw "Consumer preflight failed: $script" }
}
foreach ($script in @('_inspect\Fixes124Implementation\Verify-Implementation.ps1','_inspect\AssassinGateClimb\verify.ps1')) {
    & (Join-Path $workspace $script)
    if (-not $?) { throw "Consumer compatibility check failed: $script" }
}
foreach ($suite in @('APISharedTests','LobbyModSettingsPresetTests')) {
    & dotnet run --project (Join-Path $workspace "_inspect\$suite\$suite.csproj") --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Workspace consumer tests failed: $suite" }
}
Write-Host 'PASS: APIShared workspace consumers and additional compatibility checks.'
''')
print('Workspace integration harnesses now own their checks and no longer compile APIShared test sources.')
