from pathlib import Path
import re
root = Path.cwd()
api = root / 'APIShared'
def read(p): return p.read_text(encoding='utf-8-sig')
def write(p,t):
    p.parent.mkdir(parents=True,exist_ok=True)
    with p.open('w',encoding='utf-8',newline='') as f: f.write(t.replace('\r\n','\n').replace('\n','\r\n'))

# Retain the audited interop checks; only resolve dependencies from the selected installation.
t=read(root/'_inspect/FormationIntegration/Verify-Interop.ps1')
t=t.replace('param()', "param([string]$GameDir, [string]$ExtenderDir)")
t=t.replace("$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\\..')).Path", "$apiRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\\..'))\nif (-not $GameDir) { $GameDir = $env:SHCDE_GAME_DIR }\nif (-not $GameDir) { $GameDir = 'E:\\ProgrammeE\\Steam\\steamapps\\common\\Stronghold Crusader Definitive Edition' }\nif (-not $ExtenderDir) { $ExtenderDir = Join-Path $GameDir 'BepInEx\\plugins\\000shcdese' }")
t=t.replace("Add-Type -Path (Join-Path $workspace '.tools\\AssetStudio-2.4.1-net10\\Mono.Cecil.dll')", "Add-Type -Path (Join-Path $GameDir 'BepInEx\\core\\Mono.Cecil.dll')")
t=re.sub(r"\$game = [^\n]+\n",'',t)
t=t.replace("(Join-Path $game 'BepInEx\\plugins\\000shcdese\\SHCDESE.dll')", "(Join-Path $ExtenderDir 'SHCDESE.dll')")
start=t.index('$sourceRoot =')
end=t.index('$consumed =',start)
t=t[:start]+"$source = [IO.File]::ReadAllText((Join-Path $apiRoot 'src\\UnitCommands\\FormationRuntime.cs')) + [IO.File]::ReadAllText((Join-Path $apiRoot 'src\\Units\\UnitAccess.cs'))\n"+t[end:]
write(api/'tools/Validation/Verify-Interop.ps1',t)

# Own-source permanent-hook checks use the exact same audited scanner as the workspace.
t=read(root/'Shared/Tools/Validation/Test-PermanentNativeRuntimePatches.ps1')
t=t[:t.index('$permanentManagedContracts =')]
t=t.replace("'..\\..\\..'", "'..\\..'")
t+='''if ($errors.Count -ne 0) { throw ($errors -join [Environment]::NewLine) }
Write-Host 'PASS: APIShared has no executable-memory toggles or published transaction teardown.'
'''
write(api/'tools/Validation/Test-PermanentHooks.ps1',t)

# Dependency-free unit-access fixture, without the workspace-wide inventory scan.
t=read(root/'_inspect/UnitIdAccess/Tests/Program.cs')
t=t[:t.index('static void Scan()')]+t[t.index('namespace SHCDESE.Interop.Enums'):]
t=t.replace('if (args.Contains("--scan")) { Scan(); return; }\n','')
for u in ['using Microsoft.CodeAnalysis;','using Microsoft.CodeAnalysis.CSharp;','using Microsoft.CodeAnalysis.CSharp.Syntax;','using System.Text.Json;','using System.Text.RegularExpressions;','using System.Xml.Linq;']:
    t=t.replace(u+'\n','')
write(api/'tests/UnitAccess.Tests/Program.cs',t)
write(api/'tests/UnitAccess.Tests/UnitAccess.Tests.csproj','''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup><Compile Include="..\\..\\src\\Units\\UnitAccess.cs" Link="UnitAccess.cs" /></ItemGroup>
</Project>
''')
print('Standalone interop, permanent-hook and UnitAccess checks prepared.')
