$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$baseline = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'baseline.json') | ConvertFrom-Json
foreach ($relative in $baseline.Drivers) {
    $path = Join-Path $root $relative
    $original = [IO.File]::ReadAllText($path)
    $saved = [IO.File]::ReadAllText((Join-Path $PSScriptRoot ('before/' + $relative)))
    if ($original -cne $saved) { throw "Driver changed since snapshot: $relative" }
    $name = Split-Path -Leaf (Split-Path -Parent $relative)
    $prefix = @(
        '@echo off',
        'setlocal EnableExtensions',
        'set "BUILD_DRIVER_NOPAUSE=0"',
        'for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"',
        'set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"',
        ('echo [%date% %time%] START ' + $name),
        'cd /d "%~dp0"',
        'if errorlevel 1 goto :build_driver_directory_failed',
        'rem The outer driver owns the pause, including failures before compilation.',
        'call :build_driver_main %* /nopause',
        'set "BUILD_DRIVER_RESULT=%ERRORLEVEL%"',
        'cd /d "%BUILD_DRIVER_ORIGINAL_DIR%"',
        'echo [%date% %time%] END: exit code %BUILD_DRIVER_RESULT%',
        'if "%BUILD_DRIVER_NOPAUSE%"=="0" pause',
        'exit /b %BUILD_DRIVER_RESULT%',
        '',
        ':build_driver_directory_failed',
        'echo ERROR: Cannot enter the build directory "%~dp0".',
        'if "%BUILD_DRIVER_NOPAUSE%"=="0" pause',
        'exit /b 1',
        '',
        ':build_driver_main'
    )
    $body = [Collections.Generic.List[string]]::new()
    foreach ($line in ($original -split '\r?\n')) {
        if ($line -match '^\s*@echo off\s*$' -or $line -match '^\s*(?:if\s+.+\s+)?pause\s*$') { continue }
        $phase = $null
        if ($line -match '^\s*powershell(?:\.exe)?\b') {
            if ($line -match 'Test-SharedBoundaries') { $phase = 'Workspace source and runtime preflight' }
            elseif ($line -match 'Test-UnitAccess') { $phase = 'Unit access regression tests' }
            elseif ($line -match 'Get-Process') { $phase = 'Check that the game is closed' }
            else { $phase = 'PowerShell checks / build step' }
        } elseif ($line -match '^\s*(?:"%MSBUILD%"|dotnet\s+build)') { $phase = 'Compile projects' }
        elseif ($line -match '^\s*(?:dotnet\s+(?:run|test)|"%PROJECT_DIR%tests\\bin\\)') { $phase = 'Run tests' }
        elseif ($line -match '^\s*(?:xcopy|copy /Y)') { $phase = 'Copy package files' }
        if ($phase) { $body.Add('echo [%date% %time%] ' + $phase) }
        $body.Add($line)
    }
    $expected = (($prefix + $body.ToArray()) -join "`r`n").TrimEnd([char]13,[char]10) + "`r`n"
    [IO.File]::WriteAllText($path, $expected, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($path) -cne $expected -or $expected -match '(?<!\r)\n') { throw "Write verification failed: $relative" }
}
Write-Output "Updated $($baseline.Drivers.Count) build drivers with verified CRLF."
