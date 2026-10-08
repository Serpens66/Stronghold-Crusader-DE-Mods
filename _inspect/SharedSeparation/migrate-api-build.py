from pathlib import Path
root=Path.cwd()
path=root/'APIShared/build.bat'
t=path.read_text(encoding='utf-8-sig')
start=t.index('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\\_inspect\\FormationIntegration')
end=t.index('setlocal EnableExtensions',start)
t=t[:start]+t[end:]
t=t.replace('set "GAME_SCRIPT_EXTENDER_DIR=', 'if defined SHCDE_GAME_DIR set "GAME_DIR=%SHCDE_GAME_DIR%"\nset "GAME_SCRIPT_EXTENDER_DIR=',1)
t=t.replace('set "GAME_DIR=', 'if defined SHCDE_MSBUILD set "MSBUILD=%SHCDE_MSBUILD%"\nset "GAME_DIR=',1)
t=t.replace('set "NO_PAUSE=0"','set "NO_PAUSE=0"\nset "NO_INSTALL=0"\nfor %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"')
start=t.index('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\\_inspect\\Fixes124')
end=t.index('if exist "%LOCAL_PLUGIN_DIR%',start)
t=t[:start]+'''powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tools\\Validation\\Test-Standalone.ps1" -GameDir "%GAME_DIR%" -ExtenderDir "%EXTENDER_DIR%"
if errorlevel 1 goto build_failed
rem Additional SerpsMods integration checks exist only in the owning workspace.
if exist "%PROJECT_DIR%..\\Shared\\Tools\\Validation\\Test-SharedBoundaries.ps1" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\\Shared\\Tools\\Validation\\Test-UnitCommandSplit.ps1"
  if errorlevel 1 goto build_failed
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\\Shared\\Tools\\Validation\\Test-UnitAccess.ps1"
  if errorlevel 1 goto build_failed
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\\_inspect\\Fixes124Implementation\\Verify-Implementation.ps1"
  if errorlevel 1 goto build_failed
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\\_inspect\\AssassinGateClimb\\verify.ps1"
  if errorlevel 1 goto build_failed
)

'''+t[end:]
t=t.replace('%PROJECT_DIR%..\\_inspect\\APISharedTests', '%PROJECT_DIR%tests\\APISharedTests')
t=t.replace('%PROJECT_DIR%..\\_inspect\\LobbyModSettingsPresetTests', '%PROJECT_DIR%tests\\LobbyModSettingsPresetTests')
t=t.replace('%PROJECT_DIR%..\\_inspect\\APISharedPresetConsumerTests', '%PROJECT_DIR%tests\\PublicPresetConsumer')
marker='"%MSBUILD%" APIShared.csproj'
pos=t.index(marker)
t=t[:pos]+'''dotnet run --project "%PROJECT_DIR%tests\\UnitAccess.Tests\\UnitAccess.Tests.csproj" --configuration Release
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
if exist "%PROJECT_DIR%..\\Shared\\Tools\\Validation\\Test-SharedBoundaries.ps1" (
  for %%T in (APISharedTests LobbyModSettingsPresetTests) do (
    "%MSBUILD%" "%PROJECT_DIR%..\\_inspect\\%%T\\%%T.csproj" /t:Rebuild /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
    if errorlevel 1 goto build_failed_popd
    "%PROJECT_DIR%..\\_inspect\\%%T\\bin\\%%T.exe"
    if not "!ERRORLEVEL!"=="0" goto build_failed_popd
  )
)
'''+t[pos:]
pos=t.index('if exist "%GAME_PLUGIN_DIR%\\" rmdir')
t=t[:pos]+'''if "%NO_INSTALL%"=="1" (
  echo APIShared built and tested successfully; installation skipped.
  if "%NO_PAUSE%"=="0" pause
  exit /b 0
)
'''+t[pos:]
with path.open('w',encoding='utf-8',newline='') as f: f.write(t.replace('\r\n','\n').replace('\n','\r\n'))
# Normalize only newly created validation files, then verify byte-for-byte.
for p in (root/'APIShared/tools').rglob('*.ps1'):
    text=p.read_text(encoding='utf-8-sig').replace('\r\n','\n').replace('\n','\r\n')
    with p.open('w',encoding='utf-8',newline='') as f: f.write(text)
    assert p.read_bytes().decode('utf-8')==text
print('Build owns its tests and checks; workspace-only integration is optional; /noinstall supported.')
