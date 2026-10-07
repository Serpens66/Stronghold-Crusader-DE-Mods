from pathlib import Path
import re,json
ROOT=Path(__file__).resolve().parents[2]
def write(p,text):
    text=text.replace('\r\n','\n').replace('\n','\r\n')
    p.write_bytes(text.encode('utf-8')); assert p.read_bytes().decode('utf-8')==text
paths=[ROOT/'APIShared/src/UnitAccess.cs',ROOT/'APIShared/APIShared.csproj',ROOT/'APIShared/src/APISharedPlugin.cs',ROOT/'Shared/Test-UnitAccess.ps1']
paths+=list((Path(__file__).parent/'Tests').glob('*.*'))
paths+=list(Path(__file__).parent.glob('*.py'))
for p in paths: write(p,p.read_text(encoding='utf-8-sig'))
# Assertions refer to the checked public API, not to the old raw SDK lookup spelling.
for rel in ['BugfixesAndQoL/tests/Program.cs','Testmods/SkinTest/tests/Program.cs','Testmods/EnemyGatePathfindingTest/tests/Program.cs','_inspect/BugfixesAndQoLNativeTests/Program.cs']:
    p=ROOT/rel;text=p.read_text(encoding='utf-8-sig')
    text=text.replace('"TryGetUnitById', '"UnitAccess.TryGetById')
    write(p,text)

inventory=json.loads((Path(__file__).parent/'inventory.json').read_text())
for key in inventory['projects']:
    p=(ROOT/key).parent/'build.bat'; text=p.read_text(encoding='utf-8-sig')
    # Validate every own call site before any runtime build. Keep existing driver tests intact.
    if 'Test-UnitAccess.ps1' not in text:
        depth=len(p.parent.relative_to(ROOT).parts)
        relative='..\\'*depth+'Shared\\Test-UnitAccess.ps1'
        insertion=f'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0{relative}"\nif errorlevel 1 exit /b 1\n'
        text=text.replace('setlocal EnableExtensions',insertion+'setlocal EnableExtensions',1)
        write(p,text)

# Newly dependent projects must also resolve APIShared's packed installation.
for rel in ['Helpers/HunterQueryTargetDiagnostic/build.bat','Testmods/FormationTest/build.bat']:
    p=ROOT/rel;text=p.read_text(encoding='utf-8-sig')
    marker='if defined SHCDESE_EXTENDER_DIR set "'
    pos=text.index('\n',text.index(marker))+1
    item='set "UNIT_ACCESS_API_DIR=%GAME_DIR%\\BepInEx\\plugins\\APIShared_Serp"\nif exist "%GAME_DIR%\\BepInEx\\plugins\\SerpsMods_Serp\\Infrastructure\\APIShared_Serp\\APIShared.dll" set "UNIT_ACCESS_API_DIR=%GAME_DIR%\\BepInEx\\plugins\\SerpsMods_Serp\\Infrastructure\\APIShared_Serp"\n'
    text=text[:pos]+item+text[pos:]
    lines=text.splitlines()
    lines=[line+' /p:ApiSharedDir="%UNIT_ACCESS_API_DIR%"' if '"%MSBUILD%"' in line and '/p:GameDir=' in line else line for line in lines]
    write(p,'\n'.join(lines)+'\n')

# Build inactive mods without enabling them in the game.
for rel in ['Helpers/HunterQueryTargetDiagnostic/build.bat','ImprovedHunters/build.bat']:
    p=ROOT/rel;text=p.read_text(encoding='utf-8-sig')
    text=text.replace('set "NO_PAUSE=0"','set "NO_PAUSE=0"\nset "NO_INSTALL=0"\nfor %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"',1)
    if 'HunterQuery' in rel:
        marker='if exist "%GAME_PLUGIN_DIR%\\" ('
        item='if "%NO_INSTALL%"=="1" (\n  echo Build and tests successful. Installation skipped.\n  if "%NO_PAUSE%"=="0" pause\n  exit /b 0\n)\n'
    else:
        marker='if "%BUILD_EXIT_CODE%"=="0" ('
        # Includes the complete local package; installation and its release marker remain below.
        item='if "%NO_INSTALL%"=="1" (\n  echo Build and tests finished. Installation skipped.\n  if "%NO_PAUSE%"=="0" pause\n  exit /b %BUILD_EXIT_CODE%\n)\n'
    assert marker in text,rel
    text=text.replace(marker,item+marker,1);write(p,text)

p=ROOT/'Testmods/OutpostTest/Build-OutpostTest.ps1';text=p.read_text(encoding='utf-8-sig')
text='param([switch]$NoInstall)\n'+text
marker="    $destination = Join-Path $game 'BepInEx\\plugins\\OutpostTest_Serp'"
assert marker in text
text=text.replace(marker,"    if ($NoInstall) { Write-Output 'OutpostTest build/tests successful; installation skipped.'; exit 0 }\n"+marker)
write(p,text)
p=ROOT/'Testmods/OutpostTest/build.bat';text=p.read_text(encoding='utf-8-sig')
text=text.replace('set "NOPAUSE=0"','set "NOPAUSE=0"\nset "INSTALL_ARGUMENT="\nfor %%A in (%*) do if /I "%%~A"=="/noinstall" set "INSTALL_ARGUMENT=-NoInstall"')
text=text.replace('"%OUTPOST_PROJECT%Build-OutpostTest.ps1"','"%OUTPOST_PROJECT%Build-OutpostTest.ps1" %INSTALL_ARGUMENT%');write(p,text)
print('Prepared CRLF, contract assertions, driver regression and non-installing builds.')
