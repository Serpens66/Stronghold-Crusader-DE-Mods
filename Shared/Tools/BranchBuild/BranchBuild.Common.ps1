# Build orchestration only. Runtime/project inputs always come from the selected commit.
$ErrorActionPreference = 'Stop'
function Write-BranchMessage([string]$Message) {
    Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}' -f [DateTime]::Now, $Message)
}
function Write-BranchText([string]$Path, [string]$Text) {
    $textValue = $Text.Replace("`r`n", "`n").Replace("`n", "`r`n")
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path, $textValue, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($Path) -cne $textValue) { throw "Write verification failed: $Path" }
}
function Get-BranchHash([string]$Path) {
    $stream = [IO.File]::OpenRead($Path); $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose(); $stream.Dispose() }
}
function Invoke-BranchGit([string]$Root, [string[]]$Arguments) {
    $output = @(& git -C $Root @Arguments)
    if ($LASTEXITCODE -ne 0) { throw "Git failed in ${Root}: $($Arguments -join ' ')" }
    return $output
}
function Assert-BranchUnlinkedPath([string]$Path) {
    for ($part = [IO.Path]::GetFullPath($Path); $part; $part = [IO.Path]::GetDirectoryName($part)) {
        if ((Test-Path -LiteralPath $part) -and ([IO.File]::GetAttributes($part) -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Linked build/installation path: $part"
        }
    }
}
function Get-BranchChoice([string[]]$DriverArguments) {
    $choices = @()
    foreach ($argument in $DriverArguments) {
        if ($argument -match '^/branch:(.+)$') { $choices += $Matches[1].ToLowerInvariant() }
        elseif ($argument -ieq '/branch') { throw 'Use /branch:main or /branch:develop.' }
    }
    $choices = @($choices | Sort-Object -Unique)
    if ($choices.Count -gt 1) { throw 'Conflicting /branch arguments.' }
    if ($choices.Count -eq 0) {
        if ($DriverArguments -icontains '/nopause') { throw 'Automated builds require /branch:main or /branch:develop.' }
        Write-Host '1: main (release installation)'
        Write-Host '2: develop (separate _dev installation)'
        $answer = Read-Host 'Branch [1/main or 2/develop; empty cancels]'
        switch ($answer.ToLowerInvariant()) {
            { $_ -in @('1','main') } { $choices = @('main') }
            { $_ -in @('2','develop') } { $choices = @('develop') }
            default { throw 'Branch selection cancelled or invalid.' }
        }
    }
    if ($choices[0] -notin @('main','develop')) { throw 'Supported build branches: main, develop.' }
    return $choices[0]
}
function New-BranchSource([string]$Repository, [string]$Branch, [string]$Destination = '') {
    $commit = (Invoke-BranchGit $Repository @('rev-parse','--verify',"refs/heads/${Branch}^{commit}")) -join ''
    if ($commit -cnotmatch '^[0-9a-f]{40}$') { throw "Missing local branch: $Branch" }
    $base = Join-Path $Repository '.release-output/branch-builds'
    $root = [IO.Path]::GetFullPath((Join-Path $base ($Branch + '/' + $commit.Substring(0,12) + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))))
    if ($Destination) { $root = [IO.Path]::GetFullPath($Destination) }
    Assert-BranchUnlinkedPath $root
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($root))
    Write-BranchMessage "Preparing $Branch / $commit in $root"
    Invoke-BranchGit $Repository @('worktree','add','--quiet','--detach',$root,$commit) | Out-Host
    return [pscustomobject]@{ Root=$root; Branch=$Branch; Commit=$commit }
}
function New-LocalApiInspectionSource([string]$Repository, [string]$Destination) {
    # /apishared:dev explicitly opts into the already-built personal working package.
    # Give source preflight the same working inputs, rather than another branch's API.
    $branch = ((Invoke-BranchGit $Repository @('branch','--show-current')) -join '').Trim()
    if (-not $branch) { throw '/apishared:dev requires a named local APIShared branch.' }
    $selection = New-BranchSource $Repository $branch $Destination
    $files = @(Invoke-BranchGit $Repository @('-c','core.quotepath=false','ls-files','--cached','--others','--exclude-standard'))
    foreach ($relative in $files) {
        $from = [IO.Path]::GetFullPath((Join-Path $Repository $relative))
        $to = [IO.Path]::GetFullPath((Join-Path $Destination $relative))
        if (-not $from.StartsWith($Repository.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or
            -not $to.StartsWith($Destination.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Local SDK source path escapes its root.' }
        Assert-BranchUnlinkedPath $from; Assert-BranchUnlinkedPath $to
        if (Test-Path -LiteralPath $from -PathType Leaf) {
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to))
            Copy-Item -LiteralPath $from -Destination $to -Force
        } elseif (Test-Path -LiteralPath $to -PathType Leaf) { Remove-Item -LiteralPath $to -Force }
    }
    return $selection
}

function Convert-BranchBatch([string]$Text, [string]$Relative, [string]$Suffix) {
    # Adapt the branch's own driver, never copy uncommitted build/project logic into it.
    # Literal game targets include legacy cleanup and ExtendedData's pack/staging paths.
    if ($Text -match 'Test-SharedBoundaries\.ps1"\s*\r?\n') {
        $modDirectory = $Relative.Replace('\','/') -replace '/build\.bat$', ''
        $Text = [regex]::Replace($Text, '(Test-SharedBoundaries\.ps1")\s*\r?\n', ('$1 -ModDirectory "' + $modDirectory + '"' + "`r`n"))
    }
    if ($Suffix) {
        $Text = [regex]::Replace($Text, '(?i)%GAME_DIR%\\BepInEx\\plugins\\([^\\"\r\n]+)', {
            param($m)
            if ($m.Groups[1].Value -in @('000shcdese','APIShared_Serp') -or $m.Groups[1].Value.EndsWith('_dev')) { return $m.Value }
            return $m.Value + '_dev'
        })
        $Text = $Text.Replace('%GAME_DIR%\BepInEx\patchers\%PATCHER_NAME%', '%GAME_DIR%\BepInEx\patchers\StartupPerformanceDiagnostic_dev\%PATCHER_NAME%')
        $Text = $Text.Replace('if not exist "%GAME_DIR%\BepInEx\patchers\" mkdir "%GAME_DIR%\BepInEx\patchers"', 'if not exist "%GAME_DIR%\BepInEx\patchers\StartupPerformanceDiagnostic_dev\" mkdir "%GAME_DIR%\BepInEx\patchers\StartupPerformanceDiagnostic_dev"')
        # The optional trace configuration has a process-wide, GUID-based filename.
        # Preserve an existing main configuration instead of overwriting it from dev.
        $Text = $Text.Replace('    copy /Y "!CHAT10_TRACE_CONFIG!" "%GAME_DIR%\BepInEx\config\ActiveAIVDetector_Serp.cfg" >nul', @'
    if exist "%GAME_DIR%\BepInEx\config\ActiveAIVDetector_Serp.cfg" (
      fc /B "!CHAT10_TRACE_CONFIG!" "%GAME_DIR%\BepInEx\config\ActiveAIVDetector_Serp.cfg" >nul
      if errorlevel 1 (
        echo ERROR: Existing shared trace configuration preserved. Select the configuration manually after switching the installed mod.
        goto copy_failed
      )
    ) else (
      copy /Y "!CHAT10_TRACE_CONFIG!" "%GAME_DIR%\BepInEx\config\ActiveAIVDetector_Serp.cfg" >nul
    )
'@)
    }
    # Even older drivers which reference an installed SDK must use the selected package.
    $Text = [regex]::Replace($Text, '(?im)^(set "(?:API_SHARED_DIR|LOCAL_API_SHARED_DIR|SHCDE_API_SHARED_DIR)=[^\r\n]*")\r?$', {
        param($m)
        $name = [regex]::Match($m.Value, '(?i)set "([^=]+)=').Groups[1].Value
        return $m.Value + "`r`n" + 'if defined SHCDE_BRANCH_API_ROOT set "' + $name + '=%SHCDE_BRANCH_API_ROOT%\BepInEx\plugins\APIShared_Serp"'
    })
    # A few diagnostic projects unconditionally set ApiSharedDir in their project.
    # Only an MSBuild global property can override that; an environment variable cannot.
    $Text = [regex]::Replace($Text, '(?im)^("(?:%MSBUILD%|[^"\r\n]*MSBuild\.exe)"[^\r\n]*)(\r?)$', {
        param($m)
        if ($m.Groups[1].Value -match '/p:ApiSharedDir=') { return $m.Value }
        return $m.Groups[1].Value + ' %SHCDE_BRANCH_MSBUILD_API%' + $m.Groups[2].Value
    })
    # Existing no-install contracts are retained. Add it at the first installation
    # boundary of older drivers, after successful compilation and local packaging.
    if ($Text -notmatch '/noinstall') {
        $boundary = switch -Regex ($Relative.Replace('\','/')) {
            '^ExtremePowers/' { '(?m)^if exist "%GAME_PLUGIN_DIR%" \('; break }
            '^Helpers/ActiveAIVDetector/' { '(?m)^  echo Kopiere Plugin in den Spielordner'; break }
            '^Testmods/PopularityTest/' { '(?m)^powershell\.exe[^\r\n]*%~dp0install\.ps1'; break }
            default { '(?m)^\s*(?:if (?:not )?exist "[%!]?(?:GAME_PLUGIN_DIR|INSTALL)[%!][^\r\n]*|xcopy [^\r\n]*"%GAME_DIR%\\BepInEx\\plugins\\|xcopy [^\r\n]*"[%!](?:GAME_PLUGIN_DIR|INSTALL)[%!])' }
        }
        $match = [regex]::Match($Text, $boundary)
        if ($match.Success) {
            $action = if ($Relative.Replace('\','/') -eq 'ExtremePowers/build.bat') { 'goto done' } else { 'exit /b 0' }
            $Text = $Text.Insert($match.Index, ('if "%SHCDE_BRANCH_NOINSTALL%"=="1" ' + $action + "`r`n"))
        } elseif ($Text -match '(?i)(?:xcopy|copy|rmdir|mkdir).*%(?:GAME_PLUGIN_DIR|INSTALL|GAME_DIR)%') {
            throw "No safe /noinstall boundary for $Relative. Update the branch build adapter."
        }
    }
    return $Text
}

function Convert-BranchPowerShell([string]$Text, [string]$Relative, [string]$Suffix) {
    if (-not $Suffix) { return $Text }
    switch ($Relative.Replace('\','/')) {
        'tools/Build.ps1' {
            $Text = $Text.Replace("Join-Path `$game 'BepInEx\plugins\APIShared_Serp'", "Join-Path `$game 'BepInEx\plugins\APIShared_Serp_dev'")
            $Text = $Text.Replace("-ne 'APIShared_Serp')", "-ne 'APIShared_Serp_dev')")
            $Text = $Text.Replace("Join-Path `$game 'BepInEx/tools/APISharedWorkshopPublisher'", "Join-Path `$game 'BepInEx/tools/APISharedWorkshopPublisher_dev'")
        }
        'Testmods/OutpostTest/Build-OutpostTest.ps1' {
            $Text = $Text.Replace("Join-Path `$game 'BepInEx\plugins\OutpostTest_Serp'", "Join-Path `$game 'BepInEx\plugins\OutpostTest_Serp_dev'")
        }
        'Testmods/MoatMove/tests/Install-Package.ps1' {
            $Text = $Text.Replace("Join-Path `$gameRoot 'BepInEx\plugins\MoatMove_Serp'", "Join-Path `$gameRoot 'BepInEx\plugins\MoatMove_Serp_dev'")
            $Text = $Text.Replace("`$gameRoot + '\BepInEx\plugins\MoatMove_Serp'", "`$gameRoot + '\BepInEx\plugins\MoatMove_Serp_dev'")
        }
        'Testmods/DependencyBootstrapProbe/Build.ps1' {
            # The optional continuation helper binds a previously accepted startup to
            # fixed main paths. Do not let it touch those during a dev installation.
            $Text = $Text.Replace("`$ErrorActionPreference = 'Stop'", "`$ErrorActionPreference = 'Stop'`r`nif (`$LifecycleObservation -and -not `$NoInstall) { throw 'This committed lifecycle continuation uses fixed main installation paths; a develop continuation needs separate accepted evidence. Use /noinstall.' }")
            # Only rewrite installation targets; package sources and identity stay canonical.
            $Text = $Text.Replace('$target = Join-Path $game $relative', @'
$installRelative = $relative -replace '^(BepInEx\\plugins\\[^\\]+)', '${1}_dev'
        $installRelative = $installRelative -replace '^BepInEx\\patchers\\', 'BepInEx\patchers\DependencyBootstrapProbe_dev\'
        $target = Join-Path $game $installRelative
'@)
            # Observe builds must not remove/replace the main build's activation switches.
            $Text = $Text.Replace('if (-not $NoInstall -and -not $LifecycleObservation)', 'if (-not $NoInstall -and -not $LifecycleObservation -and -not $env:SHCDE_BRANCH_INSTALL_SUFFIX)')
            $Text = $Text.Replace("Join-Path `$game 'BepInEx\plugins\DependencyBootstrapProbeControl_Serp'", "Join-Path `$game 'BepInEx\plugins\DependencyBootstrapProbeControl_Serp_dev'")
        }
        default {
            # Post-install checks and PopularityTest's installer use absolute game paths.
            $Text = [regex]::Replace($Text, '(?i)([A-Z]:\\[^''"\r\n]*\\BepInEx\\plugins\\)([A-Za-z][A-Za-z0-9_]*_Serp)(?=[''"\\])', '${1}${2}_dev')
        }
    }
    return $Text
}

function Set-BranchBuildTools($Selection, [string]$ControllerRoot, [switch]$Api) {
    $suffix = if ($Selection.Branch -eq 'develop') { '_dev' } else { '' }
    $overrides = @()
    $tracked = @(Invoke-BranchGit $Selection.Root @('-c','core.quotepath=false','ls-files'))
    $installScripts = @('tools/Build.ps1','Testmods/OutpostTest/Build-OutpostTest.ps1','Testmods/MoatMove/tests/Install-Package.ps1',
        'Testmods/DependencyBootstrapProbe/Build.ps1','Testmods/PopularityTest/install.ps1',
        'Testmods/SavegameMissingMapFixTest/verify-installed.ps1','Testmods/PlagueHeightTest/verify-installed.ps1','Testmods/CrenelatedWallTest/verify-installed.ps1')
    foreach ($relative in $tracked) {
        if ($relative -match '^(_inspect|\.inspect|shcde-script-extender)/') { continue }
        $path = Join-Path $Selection.Root $relative
        if ($relative -match '(^|/)build\.bat$') {
            $text = Convert-BranchBatch ([IO.File]::ReadAllText($path)) $relative $suffix
        } elseif ($relative -in $installScripts) {
            $text = Convert-BranchPowerShell ([IO.File]::ReadAllText($path)) $relative $suffix
        } else { continue }
        if ($text -cne [IO.File]::ReadAllText($path)) {
            Write-BranchText $path $text
            $overrides += [pscustomobject]@{ Path=$relative; Sha256=(Get-BranchHash $path) }
        }
    }
    if (-not $Api) {
        # These three build-only tools understand isolated SDKs; no runtime files are overlaid.
        foreach ($relative in @('Shared/Tools/ApiSharedRepository/Select-ApiSharedBranch.ps1','Shared/Tools/ApiSharedRepository/ApiShared.Common.ps1')) {
            $from = Join-Path $ControllerRoot $relative
            if (Test-Path -LiteralPath (Join-Path $Selection.Root $relative)) {
                Write-BranchText (Join-Path $Selection.Root $relative) ([IO.File]::ReadAllText($from))
                $overrides += [pscustomobject]@{ Path=$relative; Sha256=(Get-BranchHash $from) }
            }
        }
    }
    return $overrides
}

function Invoke-SelectedBranchBuild {
    param([string]$Repository, [string]$Driver, [string]$Branch, [string[]]$DriverArguments, [string]$ControllerRoot, [switch]$Api, $SourceSelection = $null)
    $Repository = [IO.Path]::GetFullPath($Repository).TrimEnd('\','/')
    $driverPath = [IO.Path]::GetFullPath((Join-Path $Repository $Driver))
    if (-not $driverPath.StartsWith($Repository + '\', [StringComparison]::OrdinalIgnoreCase) -or $Driver -notmatch '(^|[/\\])build\.bat$') { throw 'Invalid build driver.' }
    $selection = if ($SourceSelection) { $SourceSelection } else { New-BranchSource $Repository $Branch }
    $selectedDriver = Join-Path $selection.Root $Driver
    if (-not (Test-Path -LiteralPath $selectedDriver -PathType Leaf)) { throw "Driver $Driver does not exist on $Branch ($($selection.Commit))." }
    $overrides = @(Set-BranchBuildTools $selection $ControllerRoot -Api:$Api)
    $oldEnvironment = @{}
    $names = @('SHCDE_BRANCH_SOURCE_ROOT','SHCDE_BRANCH_INSTALL_SUFFIX','SHCDE_BRANCH_NOINSTALL','SHCDE_BRANCH_API_ROOT','SHCDE_BRANCH_API_COMMIT',
        'SHCDE_API_SHARED_DIR','SHCDE_API_SHARED_BRANCH','SHCDE_API_SHARED_MAIN_SELECTION','SHCDE_BRANCH_NAME','SHCDE_BRANCH_COMMIT','SHCDE_BRANCH_MSBUILD_API',
        'SHCDE_GAME_DIR','SHCDESE_EXTENDER_DIR','SHCDE_MSBUILD')
    foreach ($name in $names) { $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
    $oldDirectory = (Get-Location).Path
    try {
        $env:SHCDE_BRANCH_SOURCE_ROOT = $selection.Root
        $env:SHCDE_BRANCH_NAME = $Branch; $env:SHCDE_BRANCH_COMMIT = $selection.Commit
        $env:SHCDE_BRANCH_INSTALL_SUFFIX = if ($Branch -eq 'develop') { '_dev' } else { '' }
        $env:SHCDE_BRANCH_NOINSTALL = if ($DriverArguments -icontains '/noinstall' -or $env:SHCDE_PREPARE_ONLY -eq '1') { '1' } else { '0' }
        $env:SHCDE_BRANCH_API_ROOT = $null; $env:SHCDE_BRANCH_API_COMMIT = $null
        $env:SHCDE_BRANCH_MSBUILD_API = $null
        $env:SHCDE_API_SHARED_MAIN_SELECTION = $null
        $sdk = $null
        $projectRoot = Split-Path -Parent $selectedDriver
        $usesApi = -not $Api -and (@(Get-ChildItem -LiteralPath $projectRoot -File -Filter '*.csproj' | Where-Object {
            [IO.File]::ReadAllText($_.FullName) -match '<Reference Include="APIShared(?:,|"|\s)'
        }).Count -gt 0)
        if ($usesApi) {
            if ($DriverArguments -icontains '/apishared') { throw 'Use /apishared:main, /apishared:develop or /apishared:dev.' }
            $apiChoices = @($DriverArguments | Where-Object { $_ -match '^/apishared:' } | ForEach-Object { $_.Substring(11).ToLowerInvariant() })
            if ($env:SHCDE_API_SHARED_BRANCH) { $apiChoices += $env:SHCDE_API_SHARED_BRANCH.ToLowerInvariant() }
            $apiChoices = @($apiChoices | Sort-Object -Unique)
            if ($apiChoices.Count -gt 1) { throw 'Conflicting APIShared branch requests.' }
            $apiBranch = if ($apiChoices.Count) { $apiChoices[0] } else { $Branch }
            $apiRepository = Join-Path $Repository 'APIShared'
            if ($apiBranch -in @('dev','local')) {
                $sdk = [pscustomobject]@{ Root=$apiRepository; Branch='dev'; Commit=((Invoke-BranchGit $apiRepository @('rev-parse','HEAD')) -join '');
                    GitStatus=@(Invoke-BranchGit $apiRepository @('status','--porcelain','--untracked-files=normal')) }
                $null = New-LocalApiInspectionSource $apiRepository (Join-Path $selection.Root 'APIShared')
            } elseif ($apiBranch -in @('main','develop')) {
                $apiController = Join-Path $apiRepository 'tools/BranchBuild/BranchBuild.Common.ps1'
                if (-not (Test-Path -LiteralPath $apiController)) { throw 'APIShared branch build tools are missing; update the standalone checkout.' }
                # SDK builds have no installation side effects and keep their own repository state.
                $apiSource = New-BranchSource $apiRepository $apiBranch (Join-Path $selection.Root 'APIShared')
                $sdk = Invoke-SelectedBranchBuild -Repository $apiRepository -Driver 'build.bat' -Branch $apiBranch -DriverArguments @('/nopause','/noinstall') -ControllerRoot $apiRepository -Api -SourceSelection $apiSource
            } else { throw "Unsupported APIShared source: $apiBranch" }
            $env:SHCDE_BRANCH_API_ROOT = $sdk.Root
            $env:SHCDE_BRANCH_API_COMMIT = $sdk.Commit
            $env:SHCDE_API_SHARED_DIR = Join-Path $sdk.Root 'BepInEx/plugins/APIShared_Serp'
            $env:SHCDE_BRANCH_MSBUILD_API = '"/p:ApiSharedDir=' + $env:SHCDE_API_SHARED_DIR + '"'
            $env:SHCDE_API_SHARED_BRANCH = $null
            . (Join-Path $sdk.Root 'tools/BuildProof.ps1')
            $null = Assert-ApiBuildProof $sdk.Root
            Write-BranchMessage "APIShared: $($sdk.Branch) / $($sdk.Commit) / $env:SHCDE_API_SHARED_DIR"
        }
        if (-not $Api -and -not (Test-Path -LiteralPath (Join-Path $selection.Root 'APIShared')) -and
            (Test-Path -LiteralPath (Join-Path $Repository 'APIShared/.git'))) {
            # Scoped preflight inspects APIShared sources even for non-consumers.
            $null = New-BranchSource (Join-Path $Repository 'APIShared') $Branch (Join-Path $selection.Root 'APIShared')
        }
        # Recover only machine paths (data) for a standalone APIShared build.
        if ($Api -and (Test-Path -LiteralPath (Join-Path $Repository '.local/build-proof.json'))) {
            $machine = [IO.File]::ReadAllText((Join-Path $Repository '.local/build-proof.json')) | ConvertFrom-Json
            foreach ($pair in @(@('SHCDE_GAME_DIR','GameDir'),@('SHCDESE_EXTENDER_DIR','ExtenderDir'),@('SHCDE_MSBUILD','MSBuild'))) {
                if (-not [Environment]::GetEnvironmentVariable($pair[0], 'Process') -and $machine.PSObject.Properties[$pair[1]]) {
                    [Environment]::SetEnvironmentVariable($pair[0], [string]$machine.($pair[1]), 'Process')
                }
            }
        }
        $suffix = $env:SHCDE_BRANCH_INSTALL_SUFFIX
        $targets = @()
        $manifestPath = Join-Path $projectRoot 'info.json'
        if (Test-Path -LiteralPath $manifestPath) {
            $guid = [string](([IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json).GUID)
            if ($guid) { $targets += 'BepInEx/plugins/' + $guid + $suffix }
        }
        if ($targets.Count -eq 0) {
            $packageRoot = Join-Path $projectRoot 'BepInEx/plugins'
            if (Test-Path -LiteralPath $packageRoot) {
                $targets += @(Get-ChildItem -LiteralPath $packageRoot -Directory | ForEach-Object { 'BepInEx/plugins/' + $_.Name + $suffix })
            }
        }
        if ($Driver -eq 'Testmods/StartupPerformanceDiagnostic/build.bat') {
            $targets += if ($suffix) { 'BepInEx/patchers/StartupPerformanceDiagnostic_dev/StartupPerformanceDiagnostic.Patcher.dll' } else { 'BepInEx/patchers/StartupPerformanceDiagnostic.Patcher.dll' }
        }
        if ($Driver -eq 'Testmods/DependencyBootstrapProbe/build.bat') {
            $targets += if ($suffix) { 'BepInEx/patchers/DependencyBootstrapProbe_dev/DependencyBootstrapProbe.Patcher.dll' } else { 'BepInEx/patchers/DependencyBootstrapProbe.Patcher.dll' }
            if ($DriverArguments -icontains '/correctiontrial') { $targets += 'BepInEx/plugins/DependencyBootstrapProbeControl_Serp' + $suffix }
        }
        if ($Api -and $Driver -eq 'build.bat') { $targets += 'BepInEx/tools/APISharedWorkshopPublisher' + $suffix }
        $gameDirectory = if ($env:SHCDE_GAME_DIR) { $env:SHCDE_GAME_DIR } else { 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition' }
        foreach ($target in $targets) {
            $absolute = [IO.Path]::GetFullPath((Join-Path $gameDirectory $target))
            if (-not $absolute.StartsWith([IO.Path]::GetFullPath($gameDirectory).TrimEnd('\') + '\BepInEx\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Installation target escapes BepInEx.' }
            Assert-BranchUnlinkedPath $absolute
        }
        Write-BranchMessage "Build: $Driver / $Branch / $($selection.Commit) / $($selection.Root)"
        foreach ($target in $targets) { Write-BranchMessage "Installation: $target" }
        # Run the selected BAT directly; preserve error codes and argument boundaries.
        Set-Location -LiteralPath $projectRoot
        $arguments = @($DriverArguments | Where-Object { $_ -notmatch '^/branch:' })
        if ($arguments -inotcontains '/nopause') { $arguments += '/nopause' }
        if ($env:SHCDE_BRANCH_NOINSTALL -eq '1' -and $arguments -inotcontains '/noinstall') { $arguments += '/noinstall' }
        & $selectedDriver @arguments | Out-Host
        $code = $LASTEXITCODE
        $record = [ordered]@{ SchemaVersion=1; Branch=$Branch; Commit=$selection.Commit; Driver=$Driver; SourceRoot=$selection.Root;
            ApiShared=$sdk; InstallationTargets=$targets; NoInstall=($env:SHCDE_BRANCH_NOINSTALL -eq '1'); ExitCode=$code;
            CreatedUtc=[DateTime]::UtcNow.ToString('o'); BuildToolOverrides=$overrides }
        $recordPath = $selection.Root + '.build.json'
        Write-BranchText $recordPath (($record | ConvertTo-Json -Depth 8) + "`r`n")
        Write-BranchMessage "Build record: $recordPath; exit code $code"
        if ($code -ne 0) {
            $failure = [InvalidOperationException]::new("Selected branch build failed with exit code $code. Evidence retained at $($selection.Root).")
            $failure.Data['BuildExitCode'] = $code
            throw $failure
        }
        return $selection
    } finally {
        Set-Location -LiteralPath $oldDirectory
        foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name], 'Process') }
    }
}
