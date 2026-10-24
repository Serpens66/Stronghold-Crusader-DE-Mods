[CmdletBinding()]
param([string]$Workspace)
$ErrorActionPreference = 'Stop'
if (-not $Workspace) { $Workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')) }
. (Join-Path $PSScriptRoot 'BranchBuild.Common.ps1')
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
$fixture = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -Kind t -Name 'BranchBuild'
[void][IO.Directory]::CreateDirectory($fixture)
$repo = Join-Path $fixture 'repository'
$game = Join-Path $fixture 'game'
[void][IO.Directory]::CreateDirectory($repo)
function Assert-Case([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message }; $script:cases++ }
function Git-Fixture([string[]]$Arguments) { Invoke-BranchGit $repo $Arguments | Out-Null }
function Invoke-Fixture([string[]]$Arguments, [int]$Expected = 0, [string]$Driver = 'Fixture/build.bat') {
    $output = @(& (Join-Path $repo $Driver) @Arguments 2>&1)
    $code = $LASTEXITCODE
    Assert-Case ($code -eq $Expected) ("Fixture exit $code, expected ${Expected}: " + ($output -join "`n"))
    return $output
}
$cases = 0
# The fake SDK proof records no machine paths. Explicit overrides from the developer's
# shell would correctly mark every cached SDK as foreign, so the fixture runs without them.
foreach ($name in @('SHCDE_GAME_DIR','SHCDESE_EXTENDER_DIR','SHCDE_MSBUILD')) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }
try {
    Git-Fixture @('init','-b','main')
    Git-Fixture @('config','user.name','Branch build fixture')
    Git-Fixture @('config','user.email','fixture@example.invalid')
    Git-Fixture @('config','core.autocrlf','false')
    Write-BranchText (Join-Path $repo '.gitignore') ".work/`r`n.release-output/`r`n"
    Write-BranchText (Join-Path $repo 'Fixture/info.json') '{"GUID":"Fixture_Serp","Version":"1.0.0"}'
    Write-BranchText (Join-Path $repo 'Fixture/Fixture.csproj') '<Project><ItemGroup><Reference Include="APIShared" /></ItemGroup></Project>'
    $batch = @'
@echo off
setlocal EnableExtensions
set "GAME_DIR=FIXTURE_GAME"
set "PLUGIN_NAME=Fixture_Serp"
set "PROJECT_DIR=%~dp0"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
if exist "%~dp0..\Retired\build.bat" echo RETIRED_DRIVER_PRESENT
echo BUILD_SOURCE:
type "%~dp0source.txt"
echo BUILD_ARGUMENTS=%*
echo API_PACKAGE=%SHCDE_API_SHARED_DIR%
type "%SHCDE_API_SHARED_DIR%\APIShared.dll"
if exist "%~dp0fail.txt" exit /b 27
if not exist "%GAME_PLUGIN_DIR%\" mkdir "%GAME_PLUGIN_DIR%"
copy /Y "%~dp0source.txt" "%GAME_PLUGIN_DIR%\built.txt" >nul
if errorlevel 1 exit /b 1
exit /b 0
'@.Replace('FIXTURE_GAME',$game)
    Write-BranchText (Join-Path $repo 'Fixture/build.bat') $batch
    Write-BranchText (Join-Path $repo 'Retired/build.bat') "@echo off`r`nexit /b 99`r`n"
    Write-BranchText (Join-Path $repo 'Fixture/source.txt') "MAIN_COMMITTED`r`n"
    Git-Fixture @('add','.')
    Git-Fixture @('commit','-m','main fixture')
    Git-Fixture @('checkout','-b','develop')
    Write-BranchText (Join-Path $repo 'Fixture/source.txt') "DEVELOP_COMMITTED`r`n"
    Git-Fixture @('add','.')
    Git-Fixture @('commit','-m','develop fixture')
    # A removed tracked driver reproduces retirement before its deletion is committed.
    Remove-Item -LiteralPath (Join-Path $repo 'Retired/build.bat')
    Write-BranchText (Join-Path $repo 'Fixture/source.txt') "UNCOMMITTED_MUST_NOT_BUILD`r`n"
    # main builds also load the DEVDIAG release guard.
    foreach ($name in @('BranchBuild.Common.ps1','Invoke-BranchBuild.ps1','../Paths/WorkPaths.ps1','../Validation/DevDiagnostics.Common.ps1')) {
        Write-BranchText (Join-Path $repo ('Shared/Tools/BranchBuild/' + $name)) ([IO.File]::ReadAllText((Join-Path $PSScriptRoot $name)))
    }
    # Independent fake APIShared: no game binaries, compiler or network are used.
    $apiRepo = Join-Path $repo 'APIShared'
    [void][IO.Directory]::CreateDirectory($apiRepo)
    Invoke-BranchGit $apiRepo @('init','-b','main') | Out-Null
    Invoke-BranchGit $apiRepo @('config','user.name','Branch build fixture') | Out-Null
    Invoke-BranchGit $apiRepo @('config','user.email','fixture@example.invalid') | Out-Null
    Write-BranchText (Join-Path $apiRepo '.gitignore') "BepInEx/`r`n.work/`r`n.release-output/`r`n"
    Write-BranchText (Join-Path $apiRepo 'source.txt') 'API_MAIN'
    Write-BranchText (Join-Path $apiRepo 'build.bat') @'
@echo off
setlocal EnableExtensions
set "API_NOINSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "API_NOINSTALL=1"
if "%API_NOINSTALL%"=="0" exit /b 91
if not exist "%~dp0BepInEx\plugins\APIShared_Serp" mkdir "%~dp0BepInEx\plugins\APIShared_Serp"
copy /Y "%~dp0source.txt" "%~dp0BepInEx\plugins\APIShared_Serp\APIShared.dll" >nul
exit /b 0
'@
    Write-BranchText (Join-Path $apiRepo 'tools/BuildProof.ps1') @'
function Assert-ApiBuildProof([string]$Root) {
    if ([IO.File]::ReadAllText((Join-Path $Root 'source.txt')) -cne [IO.File]::ReadAllText((Join-Path $Root 'BepInEx/plugins/APIShared_Serp/APIShared.dll'))) { throw 'Fake SDK proof mismatch.' }
    return [pscustomobject]@{ Commit=(& git -C $Root rev-parse HEAD) }
}
'@
    Invoke-BranchGit $apiRepo @('add','.') | Out-Null
    Invoke-BranchGit $apiRepo @('commit','-m','API main fixture') | Out-Null
    Invoke-BranchGit $apiRepo @('checkout','-b','develop') | Out-Null
    Write-BranchText (Join-Path $apiRepo 'source.txt') 'API_DEVELOP'
    Invoke-BranchGit $apiRepo @('add','.') | Out-Null
    Invoke-BranchGit $apiRepo @('commit','-m','API develop fixture') | Out-Null
    foreach ($name in @('BranchBuild.Common.ps1','Invoke-BranchBuild.ps1','../Paths/WorkPaths.ps1')) {
        Write-BranchText (Join-Path $apiRepo ('tools/BranchBuild/' + $name)) ([IO.File]::ReadAllText((Join-Path $PSScriptRoot $name)))
    }
    $entry = [IO.File]::ReadAllText((Join-Path $Workspace 'BuildingCosts/build.bat'))
    $prefixEnd = $entry.IndexOf(":shcde_branch_selected`r`nendlocal`r`n")
    if ($prefixEnd -lt 0) { throw 'Missing production dispatcher prefix.' }
    $prefix = $entry.Substring(0,$prefixEnd + ":shcde_branch_selected`r`nendlocal`r`n".Length)
    $prefix = $prefix.Replace('BuildingCosts/build.bat','Fixture/build.bat')
    Write-BranchText (Join-Path $repo 'Fixture/build.bat') ($prefix + $batch + "`r`n")
    $mainDir = Join-Path $game 'BepInEx/plugins/Fixture_Serp'
    $devDir = Join-Path $game 'BepInEx/plugins/Fixture_Serp_dev'
    Write-BranchText (Join-Path $mainDir 'sentinel.txt') 'main sentinel'
    Write-BranchText (Join-Path $devDir 'sentinel.txt') 'dev sentinel'
    $output = Invoke-Fixture @('/branch:main','/nopause')
    Assert-Case (($output -join "`n") -match 'RETIRED_DRIVER_PRESENT') 'Committed source lost its historical driver'
    Assert-Case (($output -join "`n") -match 'MAIN_COMMITTED' -and ($output -join "`n") -notmatch 'UNCOMMITTED_MUST_NOT_BUILD') 'main source selection failed'
    Assert-Case (($output -join "`n") -match 'API_MAIN' -and ($output -join "`n") -notmatch 'API_DEVELOP') 'main did not select APIShared main'
    Assert-Case ([IO.File]::ReadAllText((Join-Path $mainDir 'built.txt')).Trim() -ceq 'MAIN_COMMITTED') 'main installation failed'
    Assert-Case (-not (Test-Path -LiteralPath (Join-Path $devDir 'built.txt'))) 'main touched develop installation'
    $output = Invoke-Fixture @('/branch:develop','/nopause')
    Assert-Case (($output -join "`n") -match 'DEVELOP_COMMITTED' -and ($output -join "`n") -notmatch 'UNCOMMITTED_MUST_NOT_BUILD') 'develop source selection failed'
    Assert-Case (($output -join "`n") -match 'API_DEVELOP') 'develop did not select APIShared develop'
    Assert-Case ([IO.File]::ReadAllText((Join-Path $devDir 'built.txt')).Trim() -ceq 'DEVELOP_COMMITTED') 'develop installation failed'
    Assert-Case ([IO.File]::ReadAllText((Join-Path $mainDir 'built.txt')).Trim() -ceq 'MAIN_COMMITTED') 'develop overwrote main'
    # Build copies (workspace and nested SDK sources) must not accumulate; records stay.
    # Only the one reusable SDK per APIShared branch remains, in .work/c.
    $cacheRoot = [IO.Path]::GetFullPath((Join-Path $repo '.work/c')) + '\'
    function Get-ApiCopies { @(Invoke-BranchGit $apiRepo @('worktree','list','--porcelain') | Where-Object { $_ -like 'worktree *' } |
        ForEach-Object { [IO.Path]::GetFullPath($_.Substring(9)) } | Where-Object { $_ -ine [IO.Path]::GetFullPath($apiRepo) }) }
    Assert-Case (@(Invoke-BranchGit $repo @('worktree','list','--porcelain') | Where-Object { $_ -like 'worktree *' }).Count -eq 1) 'build copies were left behind'
    Assert-Case (@(Get-ApiCopies | Where-Object { -not $_.StartsWith($cacheRoot, [StringComparison]::OrdinalIgnoreCase) }).Count -eq 0 -and @(Get-ApiCopies).Count -eq 2) 'SDK build copies were left behind'
    Assert-Case (@(Get-ChildItem -LiteralPath (Join-Path $repo '.work/b') -Recurse -Filter '*.build.json').Count -ge 2) 'build records were removed with the copies'
    $output = Invoke-Fixture @('/branch:develop','/nopause','/noinstall','/keepsource')
    Assert-Case (($output -join "`n") -notmatch 'BUILD_ARGUMENTS=.*?/keepsource') '/keepsource reached the build driver'
    Assert-Case (($output -join "`n") -match 'reusing the verified develop SDK' -and ($output -join "`n") -match 'API_DEVELOP') 'unchanged SDK was rebuilt'
    $kept = @(Get-ChildItem -LiteralPath (Join-Path $repo '.work/b/develop') -Directory)
    Assert-Case ($kept.Count -eq 1) '/keepsource did not keep exactly one build copy'
    Remove-BranchSource $repo $kept[0].FullName
    Assert-Case (-not (Test-Path -LiteralPath $kept[0].FullName) -and @(Get-ApiCopies).Count -eq 2) 'kept build copy could not be removed'
    # A changed SDK input invalidates the cache and rebuilds it exactly once.
    Write-BranchText (Join-Path $apiRepo 'source.txt') 'API_DEVELOP_CHANGED'
    Invoke-BranchGit $apiRepo @('commit','-am','API develop change') | Out-Null
    $output = Invoke-Fixture @('/branch:develop','/nopause','/noinstall')
    Assert-Case (($output -join "`n") -match 'not reusable' -and ($output -join "`n") -match 'API_DEVELOP_CHANGED') 'changed SDK was not rebuilt'
    $output = Invoke-Fixture @('/branch:develop','/nopause','/noinstall')
    Assert-Case (($output -join "`n") -match 'reusing the verified develop SDK' -and @(Get-ApiCopies).Count -eq 2) 'rebuilt SDK was not reused'
    # A damaged package fails its proof and is rebuilt instead of being used.
    Write-BranchText (Join-Path $cacheRoot 'apishared-develop/BepInEx/plugins/APIShared_Serp/APIShared.dll') 'TAMPERED'
    $output = Invoke-Fixture @('/branch:develop','/nopause','/noinstall')
    Assert-Case (($output -join "`n") -match 'not reusable' -and ($output -join "`n") -notmatch 'TAMPERED') 'damaged SDK cache was used'
    Write-BranchText (Join-Path $mainDir 'built.txt') 'KEEP_MAIN_NOINSTALL'
    Write-BranchText (Join-Path $devDir 'built.txt') 'KEEP_DEV_NOINSTALL'
    $beforeMain = Get-BranchHash (Join-Path $mainDir 'built.txt'); $beforeDev = Get-BranchHash (Join-Path $devDir 'built.txt')
    $null = Invoke-Fixture @('/branch:main','/nopause','/noinstall')
    $null = Invoke-Fixture @('/branch:develop','/nopause','/noinstall')
    Assert-Case ((Get-BranchHash (Join-Path $mainDir 'built.txt')) -ceq $beforeMain -and (Get-BranchHash (Join-Path $devDir 'built.txt')) -ceq $beforeDev) '/noinstall changed installations'
    $output = Invoke-Fixture @('/branch:develop','/apishared:main','/nopause','/noinstall','/fixture-extra')
    Assert-Case (($output -join "`n") -match 'API_MAIN' -and ($output -join "`n") -match 'BUILD_ARGUMENTS=.*?/fixture-extra') 'explicit SDK choice or argument forwarding failed'
    $null = Invoke-Fixture @('/branch:main','/apishared:main','/apishared:develop','/nopause') 1
    $null = Invoke-Fixture @('/branch:wrong','/nopause') 1
    $null = Invoke-Fixture @('/branch:main','/branch:develop','/nopause') 1
    Write-BranchText (Join-Path $repo 'Missing/build.bat') "@echo LOCAL_FALLBACK_MUST_NOT_RUN`r`n"
    $missingOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'Shared/Tools/BranchBuild/Invoke-BranchBuild.ps1') -Repository $repo -Driver 'Missing/build.bat' /branch:main /nopause 2>&1)
    Assert-Case ($LASTEXITCODE -eq 1 -and ($missingOutput -join "`n") -notmatch 'LOCAL_FALLBACK_MUST_NOT_RUN') 'Missing branch driver fell back to working sources'
    Invoke-BranchGit $apiRepo @('checkout','-b','dev/fixture') | Out-Null
    Write-BranchText (Join-Path $apiRepo 'source.txt') 'API_PERSONAL_DIRTY'
    Write-BranchText (Join-Path $apiRepo 'BepInEx/plugins/APIShared_Serp/APIShared.dll') 'API_PERSONAL_DIRTY'
    $output = Invoke-Fixture @('/branch:main','/apishared:dev','/nopause','/noinstall')
    Assert-Case (($output -join "`n") -match 'API_PERSONAL_DIRTY') 'explicit personal SDK was not retained'
    Assert-Case ([IO.File]::ReadAllText((Join-Path $apiRepo 'source.txt')) -ceq 'API_PERSONAL_DIRTY') 'personal SDK sources changed'
    # Working sources use current file contents; install and no-install remain independent.
    $workingOutput = Invoke-Fixture @('/nopause','/noinstall')
    Assert-Case (($workingOutput -join "`n") -notmatch 'RETIRED_DRIVER_PRESENT') 'Working source restored a deleted tracked driver'
    Assert-Case (-not (Test-Path -LiteralPath (Join-Path $repo 'Retired/build.bat'))) 'Build restored a retired driver in the developer checkout'
    Assert-Case (($workingOutput -join "`n") -match 'UNCOMMITTED_MUST_NOT_BUILD' -and ($workingOutput -join "`n") -match 'API_PERSONAL_DIRTY') 'Automatic working-source/default local SDK selection failed'
    Assert-Case ((Get-BranchHash (Join-Path $mainDir 'built.txt')) -ceq $beforeMain) 'Working /noinstall changed installation'
    $workingOutput = Invoke-Fixture @('/source:working','/nopause')
    Assert-Case ([IO.File]::ReadAllText((Join-Path $mainDir 'built.txt')).Trim() -ceq 'UNCOMMITTED_MUST_NOT_BUILD') 'Working source installation failed'
    $mainBeforeDev = Get-BranchHash (Join-Path $mainDir 'built.txt')
    $null = Invoke-Fixture @('/source:working','/install:dev','/nopause')
    Assert-Case ([IO.File]::ReadAllText((Join-Path $devDir 'built.txt')).Trim() -ceq 'UNCOMMITTED_MUST_NOT_BUILD') 'Explicit working dev installation lost working content'
    Assert-Case ((Get-BranchHash (Join-Path $mainDir 'built.txt')) -ceq $mainBeforeDev) 'Explicit working dev installation changed main'
    $beforeDev = Get-BranchHash (Join-Path $devDir 'built.txt')
    $null = Invoke-Fixture @('/source:working','/install:dev','/install:main','/nopause') 1
    $null = Invoke-Fixture @('/source:working','/install:unknown','/nopause') 1
    $null = Invoke-Fixture @('/source:working','/branch:main','/nopause') 1
    Write-BranchText (Join-Path $mainDir 'built.txt') 'KEEP_MAIN_NOINSTALL'
    $previousPrepareOnly = $env:SHCDE_PREPARE_ONLY
    try {
        $env:SHCDE_PREPARE_ONLY = '1'
        $null = Invoke-Fixture @('/branch:main','/nopause')
        Assert-Case ((Get-BranchHash (Join-Path $mainDir 'built.txt')) -ceq $beforeMain) 'inherited prepare-only mode installed files'
    } finally { $env:SHCDE_PREPARE_ONLY = $previousPrepareOnly }
    $previousPinned = $env:SHCDE_BRANCH_PINNED
    try {
        Write-BranchText (Join-Path $repo 'Fixture/build.bat') ($prefix + 'exit /b 99')
        $env:SHCDE_BRANCH_PINNED = ((Invoke-BranchGit $repo @('rev-parse','HEAD')) -join '')
        $null = Invoke-Fixture @('/nopause') 99
    } finally {
        $env:SHCDE_BRANCH_PINNED = $previousPinned
        Write-BranchText (Join-Path $repo 'Fixture/build.bat') ($prefix + $batch + "`r`n")
    }
    # Exercise the real Read-Host menu through a fresh PowerShell/BAT process.
    $start = [Diagnostics.ProcessStartInfo]::new()
    # Start the BAT through cmd.exe like a user does.
    $start.FileName = 'cmd.exe'
    $start.Arguments = '/d /c ""' + (Join-Path $repo 'Fixture\build.bat') + '""'
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
    try {
        # On a UTF-8 console, .NET Framework writes the encoding's BOM into the redirected
        # stdin pipe at Start(); the child then reads "<BOM>1", an invalid menu choice.
        $inputEncoding = [Console]::InputEncoding
        try { [Console]::InputEncoding = [Text.UTF8Encoding]::new($false); $null = $process.Start() }
        finally { [Console]::InputEncoding = $inputEncoding }
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.WriteLine('1'); $process.StandardInput.WriteLine('')
        $process.StandardInput.Close()
        if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Interactive branch selection timed out.' }
        $menuOutput = $stdout.Result + $stderr.Result
        Assert-Case ($process.ExitCode -eq 0 -and $menuOutput -match 'MAIN_COMMITTED' -and $menuOutput -match 'separate _dev installation') ('Interactive selection failed: ' + $menuOutput)
    } finally { $process.Dispose() }
    Assert-Case (((Invoke-BranchGit $repo @('branch','--show-current')) -join '') -ceq 'develop') 'working branch moved'
    Assert-Case ([IO.File]::ReadAllText((Join-Path $repo 'Fixture/source.txt')).Trim() -ceq 'UNCOMMITTED_MUST_NOT_BUILD') 'dirty working file changed'
    $records = @(Get-ChildItem -LiteralPath (Join-Path $repo '.work/b') -Recurse -Filter '*.build.json' | Where-Object {
        ([IO.File]::ReadAllText($_.FullName) | ConvertFrom-Json).Driver -eq 'Fixture/build.bat'
    })
    Assert-Case (@($records | Where-Object { ([IO.File]::ReadAllText($_.FullName) | ConvertFrom-Json).InstallationTargets -contains 'BepInEx/plugins/Fixture_Serp_dev' }).Count -gt 0) 'missing dev installation evidence'
    foreach ($file in $records) {
        $record = [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json
        Assert-Case ($record.Commit -match '^[0-9a-f]{40}$' -and $record.Branch -in @('main','develop','working') -and $record.ExitCode -eq 0) 'invalid build record'
    }
    # Failure and missing-driver handling never fall back to the working tree.
    Write-BranchText (Join-Path $repo 'Fixture/fail.txt') 'fail'
    Git-Fixture @('add','Fixture/fail.txt')
    Git-Fixture @('commit','-m','failure fixture')
    $null = Invoke-Fixture @('/branch:develop','/nopause') 27
    Assert-Case ((Get-BranchHash (Join-Path $devDir 'built.txt')) -ceq $beforeDev) 'failed build installed files'

    # Build-test modes are consumer choices. Reject ambiguity before copying sources,
    # preserve command arguments/exit codes, and never rebuild an SDK under /tests:none.
    $bfDriver = 'BugfixesAndQoL/build.bat'
    # Exercise the actual pruned enumeration against the old recurse-then-filter
    # selection: nested build/work trees must disappear, active siblings must remain.
    $boundaryTokens=$null; $boundaryErrors=$null
    $boundaryAst=[System.Management.Automation.Language.Parser]::ParseFile((Join-Path $Workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1'),[ref]$boundaryTokens,[ref]$boundaryErrors)
    $enumerator=$boundaryAst.Find({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-ActiveBoundaryFiles'},$true)
    . ([scriptblock]::Create($enumerator.Extent.Text))
    $tree=Join-Path $fixture 'boundary-tree'
    foreach ($relative in @('src/one.cs','src/nested/two.cs','other/three.cs','tests/input.txt','bin/generated.cs','obj/generated.cs','.work/a/retained.cs','src/BepInEx/generated.cs','_inspect/history.cs')) {
        Write-BranchText (Join-Path $tree $relative) 'fixture'
    }
    $oldSelection=@(Get-ChildItem -LiteralPath $tree -Recurse -File | Where-Object { $_.FullName.Substring($tree.Length) -notmatch '[\\/](bin|obj|BepInEx|_inspect|\.inspect|\.work|\.release-output)[\\/]' } | ForEach-Object FullName | Sort-Object)
    $newSelection=@(Get-ActiveBoundaryFiles $tree | ForEach-Object FullName | Sort-Object)
    Assert-Case ($oldSelection.Count -eq 4 -and ($oldSelection -join "`n") -ceq ($newSelection -join "`n")) 'pruned source enumeration lost active inputs or included ignored trees'
    Assert-Case ((Get-BranchTestSelection $bfDriver @()).Mode -eq 'full') 'default test selection is not full'
    foreach ($mode in @('full','quick','none')) {
        Assert-Case ((Get-BranchTestSelection $bfDriver @("/tests:$mode")).Mode -eq $mode) 'test mode parser lost a mode'
    }
    foreach ($arguments in @(@('/tests:bad'),@('/tests'),@('/tests:full','/tests:none'),@('/checks:bad'),@('/checks:settings','/checks:notifications'),@('/tests:quick','/checks:settings'),@('/tests:none','/checks:notifications'))) {
        $rejected=$false
        try { $null=Get-BranchTestSelection $bfDriver $arguments } catch { $rejected=$true }
        Assert-Case $rejected ('invalid test selection accepted: ' + ($arguments -join ' '))
    }
    Assert-Case ((Get-BranchTestSelection $bfDriver @('/tests:full','/checks:settings')).Scope -eq 'settings') 'existing scope lost'
    Assert-Case ((Get-BranchTestSelection $bfDriver @('/tests:full','/checks:coop')).Scope -eq 'coop') 'Coop validation scope lost'
    # Test the production BAT timer itself: wrapping a native test must preserve
    # signed Windows exception exit codes as well as ordinary failures.
    $productionDriver=[IO.File]::ReadAllText((Join-Path $Workspace $bfDriver))
    $timerBody=$productionDriver.Substring($productionDriver.LastIndexOf(":step_start`r`n",[StringComparison]::Ordinal))
    $timerDriver=Join-Path $fixture 'timer.bat'
    Write-BranchText $timerDriver ("@echo off`r`nsetlocal EnableDelayedExpansion`r`ncall :step_start tests exit-code-fixture`r`ncall :step_end %*`r`nexit /b %ERRORLEVEL%`r`n" + $timerBody)
    foreach ($expectedCode in @(0,27,-1073741819)) {
        $timerOutput=@(& $timerDriver $expectedCode)
        $timerCode=$LASTEXITCODE
        Assert-Case ($timerCode -eq $expectedCode -and ($timerOutput -join "`n") -match "DurationMs=\d+ ExitCode=$expectedCode") 'timing wrapper lost a test exit code'
    }
    $bfPrefix=$prefix.Replace('Fixture/build.bat',$bfDriver)
    Write-BranchText (Join-Path $repo $bfDriver) ($bfPrefix + "rem SHCDE_TEST_MODES: full quick none`r`n" + $batch)
    Write-BranchText (Join-Path $repo 'BugfixesAndQoL/source.txt') 'BF_WORKING'
    Write-BranchText (Join-Path $repo 'BugfixesAndQoL/Fixture.csproj') '<Project><ItemGroup><Reference Include="APIShared" /></ItemGroup></Project>'
    Write-BranchText (Join-Path $repo 'BugfixesAndQoL/info.json') '{"GUID":"Fixture_Serp","Version":"1.0.0"}'
    foreach ($mode in @('full','quick','none')) {
        $output=Invoke-Fixture @('/source:working','/apishared:dev','/noinstall','/nopause',"/tests:$mode") 0 $bfDriver
        Assert-Case (($output -join "`n") -match "BUILD_ARGUMENTS=.*?/tests:$mode") 'test mode argument did not reach the selected BAT'
        $latest=@(Get-ChildItem -LiteralPath (Join-Path $repo '.work/b/working') -Filter '*.build.json' | Sort-Object LastWriteTimeUtc -Descending)[0]
        $record=[IO.File]::ReadAllText($latest.FullName) | ConvertFrom-Json
        Assert-Case ($record.TestSelection.Mode -eq $mode) 'test mode missing from build record'
    }
    # An older source must fail explicitly, even with a new dispatcher available.
    Write-BranchText (Join-Path $repo $bfDriver) ($bfPrefix + $batch)
    $output=Invoke-Fixture @('/source:working','/apishared:dev','/noinstall','/nopause','/tests:none') 1 $bfDriver
    Assert-Case (($output -join "`n") -match 'does not support /tests:none' -and ($output -join "`n") -notmatch 'BUILD_SOURCE:') 'old driver silently ignored the requested test mode'
    Write-BranchText (Join-Path $repo $bfDriver) ($bfPrefix + "rem SHCDE_TEST_MODES: full quick none`r`n" + $batch)
    $output=Invoke-Fixture @('/source:working','/apishared:develop','/noinstall','/nopause','/tests:none') 0 $bfDriver
    Assert-Case (($output -join "`n") -match 'reusing the verified develop SDK') 'none failed to reuse an unchanged SDK'
    $cachedDll=Join-Path $cacheRoot 'apishared-develop/BepInEx/plugins/APIShared_Serp/APIShared.dll'
    $cachedBytes=[IO.File]::ReadAllBytes($cachedDll)
    try {
        Write-BranchText $cachedDll 'TAMPERED_NONE_MUST_PRESERVE'
        $cachedHash=Get-BranchHash $cachedDll
        $output=Invoke-Fixture @('/source:working','/apishared:develop','/noinstall','/nopause','/tests:none') 1 $bfDriver
        Assert-Case (($output -join "`n") -match '/tests:none never rebuilds' -and (Get-BranchHash $cachedDll) -ceq $cachedHash) 'none repaired or rebuilt an invalid SDK'
    } finally { [IO.File]::WriteAllBytes($cachedDll,$cachedBytes) }
    $absentRepo=Join-Path $fixture 'no-sdk-cache'
    [void][IO.Directory]::CreateDirectory($absentRepo)
    $rejected=$false
    try { $null=Get-CachedApiSdk $absentRepo $apiRepo 'develop' -RequireExisting } catch { $rejected=$_.Exception.Message -like '*cache is missing*' }
    Assert-Case ($rejected -and -not (Test-Path -LiteralPath (Join-Path $absentRepo '.work/c/apishared-develop'))) 'none created a missing SDK cache'
    Write-BranchText (Join-Path $repo 'BugfixesAndQoL/fail.txt') 'fail'
    $null=Invoke-Fixture @('/source:working','/apishared:dev','/noinstall','/nopause','/tests:quick') 27 $bfDriver

    # Adapt every currently maintained driver and both committed branch variants.
    $drivers = @(Invoke-BranchGit $Workspace @('ls-files','*build.bat') | Where-Object { $_ -notmatch '^(_inspect|shcde-script-extender)/' })
    foreach ($branch in @('main','develop')) {
        $available = @(Invoke-BranchGit $Workspace @('ls-tree','-r','--name-only',$branch))
        foreach ($relative in @($drivers | Where-Object { $_ -in $available })) {
            $original = @(Invoke-BranchGit $Workspace @('show',"${branch}:$relative"))
            $text = ($original -join "`r`n") + "`r`n"
            $adapted = Convert-BranchBatch $text $relative '_dev'
            Assert-Case ($adapted -notmatch '%GAME_DIR%\\BepInEx\\plugins\\(?!000shcdese|APIShared_Serp)([^\\"\r\n]+)(?<!_dev)(?:"|\\)') "Unsuffixed target: $branch/$relative"
        }
    }
    Write-Host "PASS: $cases branch, source, BAT, no-install, installation isolation and driver adaptation checks."
    Write-Host "Fixture evidence retained: $fixture"
} catch { Write-Host "Fixture evidence retained: $fixture"; throw }
