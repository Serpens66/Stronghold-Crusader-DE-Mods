# Build orchestration: explicit source selection wins; automated default is the working tree.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
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
function Get-BranchTestSelection([string]$Driver, [string[]]$DriverArguments) {
    $modeArguments = @($DriverArguments | Where-Object { $_ -match '^/tests(?::|$)' })
    if ($Driver.Replace('\','/') -ine 'BugfixesAndQoL/build.bat') {
        if ($modeArguments.Count) { throw '/tests:full|quick|none is supported by BugfixesAndQoL/build.bat only.' }
        return $null
    }
    $modes = @($modeArguments | ForEach-Object {
        if ($_ -notmatch '^/tests:(full|quick|none)$') { throw 'Use /tests:full, /tests:quick or /tests:none.' }
        $Matches[1].ToLowerInvariant()
    } | Sort-Object -Unique)
    if ($modes.Count -gt 1) { throw 'Conflicting /tests arguments.' }
    $mode = if ($modes.Count) { $modes[0] } else { 'full' }
    $scopes = @($DriverArguments | Where-Object { $_ -match '^/checks(?::|$)' } | ForEach-Object {
        if ($_ -notmatch '^/checks:(notifications|gatehouse|foreignhud|settings)$') { throw 'Unknown /checks scope.' }
        $Matches[1].ToLowerInvariant()
    } | Sort-Object -Unique)
    if ($scopes.Count -gt 1 -or ($scopes.Count -and $mode -ne 'full')) { throw '/checks selects one feature scope and requires /tests:full.' }
    return [pscustomobject]@{ Mode=$mode; Scope=$(if ($scopes.Count) { $scopes[0] } else { 'all' }) }
}

function Get-BranchChoice([string[]]$DriverArguments) {
    $working = $DriverArguments -icontains '/workspace' -or $DriverArguments -icontains '/source:working'
    if (@($DriverArguments | Where-Object { $_ -match '^/source:' -and $_ -ine '/source:working' }).Count) { throw 'Supported source override: /source:working.' }
    if ($working) {
        if (@($DriverArguments | Where-Object { $_ -match '^/branch(?::|$)' }).Count) { throw 'Working sources and a branch commit cannot be selected together.' }
        Write-BranchMessage 'Build source: current working tree (explicit selection).'
        return 'working'
    }
    $choices = @()
    foreach ($argument in $DriverArguments) {
        if ($argument -match '^/branch:(.+)$') { $choices += $Matches[1].ToLowerInvariant() }
        elseif ($argument -ieq '/branch') { throw 'Use /branch:main or /branch:develop.' }
    }
    $choices = @($choices | Sort-Object -Unique)
    if ($choices.Count -gt 1) { throw 'Conflicting /branch arguments.' }
    if ($choices.Count -eq 0) {
        if ($DriverArguments -icontains '/nopause') { Write-BranchMessage 'Build source: current working tree (automated default; no source selected).'; return 'working' }
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
function Get-BranchInstallSuffix([string]$Branch, [string[]]$DriverArguments) {
    $choices = @($DriverArguments | Where-Object { $_ -match '^/install:' } | ForEach-Object { $_.Substring(9).ToLowerInvariant() } | Sort-Object -Unique)
    if ($DriverArguments -icontains '/install' -or $choices.Count -gt 1 -or ($choices.Count -and $choices[0] -notin @('dev','main'))) {
        throw 'Use one installation choice: /install:dev or /install:main.'
    }
    if ($choices.Count) { if ($choices[0] -eq 'dev') { return '_dev' }; return '' }
    if ($Branch -eq 'develop') { return '_dev' }
    return ''
}
function New-BranchSource([string]$Repository, [string]$Branch, [string]$Destination = '') {
    # $Branch may also be an exact commit, e.g. the one a cached SDK was built from.
    $reference = if ($Branch -eq 'working') { 'HEAD^{commit}' } elseif ($Branch -cmatch '^[0-9a-f]{40}$') { "${Branch}^{commit}" } else { "refs/heads/${Branch}^{commit}" }
    $commit = (Invoke-BranchGit $Repository @('rev-parse','--verify',$reference)) -join ''
    if ($commit -cnotmatch '^[0-9a-f]{40}$') { throw "Missing local branch: $Branch" }
    $root = if ($Destination) { [IO.Path]::GetFullPath($Destination) } else { New-ShortWorkDirectory -Root $Repository -Kind b -Name $Branch -Identity $commit }
    if ($Destination) { $root = [IO.Path]::GetFullPath($Destination) }
    Assert-BranchUnlinkedPath $root
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($root))
    Write-BranchMessage "Preparing $Branch / $commit in $root"
    Invoke-BranchGit $Repository @('config','core.longpaths','true') | Out-Null
    Invoke-BranchGit $Repository @('worktree','add','--quiet','--detach',$root,$commit) | Out-Host
    return [pscustomobject]@{ Root=$root; Branch=$Branch; Commit=$commit }
}
function Remove-BranchSource([string]$Repository, [string]$Root) {
    # A build copy holds the whole tracked workspace (~5 GB, mostly _inspect data). Its
    # inputs are commits or copies of the working tree, and the build record stays beside
    # it, so the copy itself is disposable. Remove only Git-registered copies through Git.
    $Repository = [IO.Path]::GetFullPath($Repository).TrimEnd('\','/')
    $Root = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    if (-not $Root.StartsWith($Repository + '\.work\b\', [StringComparison]::OrdinalIgnoreCase)) { throw "Refusing to remove a build copy outside .work\b: $Root" }
    if (-not (Test-Path -LiteralPath $Root)) { return }
    Assert-BranchUnlinkedPath $Root
    $nested = Join-Path $Root 'APIShared'
    if (Test-Path -LiteralPath (Join-Path $nested '.git') -PathType Leaf) {
        # The SDK copy belongs to the APIShared repository and must be unregistered there.
        $common = ((Invoke-BranchGit $nested @('rev-parse','--path-format=absolute','--git-common-dir')) -join '').Trim()
        Invoke-BranchGit (Split-Path -Parent $common) @('worktree','remove','--force',$nested) | Out-Null
    }
    Invoke-BranchGit $Repository @('worktree','remove','--force',$Root) | Out-Null
    # Git leaves directory junctions created by test fixtures. Delete the remaining
    # empty skeleton only; a link is removed as a link and never followed.
    if (Test-Path -LiteralPath $Root) { Remove-BranchEmptyTree $Root }
}
function Remove-BranchEmptyTree([string]$Path) {
    $item = [IO.DirectoryInfo]::new($Path)
    if (-not ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        if (@($item.GetFiles()).Count) { throw "Build copy still contains files: $Path" }
        foreach ($child in $item.GetDirectories()) { Remove-BranchEmptyTree $child.FullName }
    }
    [IO.Directory]::Delete($Path, $false)
}
function New-WorkingSource([string]$Repository, [string]$Destination = '') {
    $selection = New-BranchSource $Repository 'working' $Destination
    # The clean worktree already contains unchanged tracked files. Overlay only working changes.
    $workingFiles = @(Invoke-BranchGit $Repository @('-c','core.quotepath=false','diff','--name-only','--no-renames','HEAD')) + @(Invoke-BranchGit $Repository @('-c','core.quotepath=false','ls-files','--others','--exclude-standard'))
    foreach ($relative in @($workingFiles | Sort-Object -Unique)) {
        $from = [IO.Path]::GetFullPath((Join-Path $Repository $relative))
        $to = [IO.Path]::GetFullPath((Join-Path $selection.Root $relative))
        if (-not $from.StartsWith($Repository.TrimEnd('\','/') + '\', [StringComparison]::OrdinalIgnoreCase) -or
            -not $to.StartsWith($selection.Root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Working source escapes repository.' }
        Assert-BranchUnlinkedPath $from; Assert-BranchUnlinkedPath $to
        if (Test-Path -LiteralPath $from -PathType Leaf) {
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to))
            Copy-Item -LiteralPath $from -Destination $to -Force
        } elseif (Test-Path -LiteralPath $to -PathType Leaf) { Remove-Item -LiteralPath $to -Force }
    }
    $selection | Add-Member NoteProperty GitStatus @(Invoke-BranchGit $Repository @('status','--porcelain','--untracked-files=normal'))
    return $selection
}

function Get-BranchSourceDifferences([string]$Root) {
    # The record's commit identifies every unchanged tracked file. Hash only what differs
    # from it (working overlays, adapter rewrites, untracked inputs) instead of rehashing
    # the whole ~5 GB tree after every build.
    $paths = @(Invoke-BranchGit $Root @('-c','core.quotepath=false','diff','--name-only','--no-renames','HEAD')) +
        @(Invoke-BranchGit $Root @('-c','core.quotepath=false','ls-files','--others','--exclude-standard'))
    foreach ($relative in @($paths | Sort-Object -Unique)) {
        $file = Join-Path $Root $relative
        if (Test-Path -LiteralPath $file -PathType Leaf) { [ordered]@{ Path=$relative; Sha256=(Get-BranchHash $file) } }
        else { [ordered]@{ Path=$relative; Deleted=$true } }
    }
}
function Get-CachedApiSdk([string]$Repository, [string]$ApiRepository, [string]$Branch, [switch]$RequireExisting) {
    # APIShared's own build runs its complete suite and then attests exactly its inputs:
    # Assert-ApiBuildProof fingerprints sources, tests, tools, game/Extender references and
    # MSBuild. An unchanged SDK therefore needs no rebuild and no second test run per
    # consumer build; any changed input fails the proof and rebuilds the cache once.
    $commit = (Invoke-BranchGit $ApiRepository @('rev-parse','--verify',"refs/heads/${Branch}^{commit}")) -join ''
    if ($commit -cnotmatch '^[0-9a-f]{40}$') { throw "Missing local APIShared branch: $Branch" }
    $cache = [IO.Path]::GetFullPath((Join-Path $Repository ".work\c\apishared-$Branch"))
    Assert-BranchUnlinkedPath $cache
    if (Test-Path -LiteralPath $cache) {
        $reason = 'source commit changed'
        if ((Test-Path -LiteralPath (Join-Path $cache '.git') -PathType Leaf) -and
            ((Invoke-BranchGit $cache @('rev-parse','HEAD')) -join '') -ceq $commit) {
            try {
                . (Join-Path $cache 'tools/BuildProof.ps1')
                $proof = Assert-ApiBuildProof $cache
                # Explicit machine-path overrides select other references than the cached build.
                foreach ($pair in @(@('SHCDE_GAME_DIR','GameDir'),@('SHCDESE_EXTENDER_DIR','ExtenderDir'),@('SHCDE_MSBUILD','MSBuild'))) {
                    $requested = [Environment]::GetEnvironmentVariable($pair[0], 'Process')
                    $recorded = if ($proof.PSObject.Properties[$pair[1]]) { [string]$proof.($pair[1]) } else { '' }
                    if ($requested -and (-not $recorded -or
                        [IO.Path]::GetFullPath($requested).TrimEnd('\') -ine [IO.Path]::GetFullPath($recorded).TrimEnd('\'))) {
                        throw "$($pair[0]) differs from the cached build"
                    }
                }
                Write-BranchMessage "APIShared: reusing the verified $Branch SDK $commit from $cache (no rebuild or retest)."
                return [pscustomobject]@{ Root=$cache; Branch=$Branch; Commit=$commit; Cached=$true }
            } catch { $reason = $_.Exception.Message }
        }
        if ($RequireExisting) { throw "APIShared SDK is not reusable ($reason). /tests:none never rebuilds or tests the SDK; prepare the selected SDK with a normal build first." }
        Write-BranchMessage "APIShared SDK cache is not reusable ($reason); rebuilding it."
        Remove-CachedApiSdk $ApiRepository $cache
    }
    if ($RequireExisting) { throw 'APIShared SDK cache is missing. /tests:none never rebuilds or tests the SDK; prepare the selected SDK with a normal build first.' }
    $source = New-BranchSource $ApiRepository $Branch $cache
    if ($source.Commit -cne $commit) { Remove-CachedApiSdk $ApiRepository $cache; throw "APIShared $Branch moved during SDK preparation." }
    try {
        # SDK builds have no installation side effects and keep their own repository state.
        return Invoke-SelectedBranchBuild -Repository $ApiRepository -Driver 'build.bat' -Branch $Branch -DriverArguments @('/nopause','/noinstall') -ControllerRoot $ApiRepository -Api -SourceSelection $source
    } catch {
        # A failed build would never pass its proof; do not keep ~GBs of unusable output.
        try { Remove-CachedApiSdk $ApiRepository $cache } catch { Write-Warning "Failed SDK cache could not be removed: $($_.Exception.Message)" }
        throw
    }
}
function Remove-CachedApiSdk([string]$ApiRepository, [string]$Cache) {
    if (Test-Path -LiteralPath (Join-Path $Cache '.git') -PathType Leaf) {
        Invoke-BranchGit $ApiRepository @('worktree','remove','--force',$Cache) | Out-Null
    }
    Invoke-BranchGit $ApiRepository @('worktree','prune') | Out-Null
    if (Test-Path -LiteralPath $Cache) { Remove-BranchEmptyTree $Cache }
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
    $Text = [regex]::Replace($Text, '(?im)^(set "(?:API_SHARED_DIR|LOCAL_API_SHARED_DIR|SHCDE_API_SHARED_DIR)=[^\r\n]*")\r?(?:\n|$)', {
        param($m)
        $name = [regex]::Match($m.Value, '(?i)set "([^=]+)=').Groups[1].Value
        return $m.Groups[1].Value + "`r`n" + 'if defined SHCDE_BRANCH_API_ROOT set "' + $name + '=%SHCDE_BRANCH_API_ROOT%\BepInEx\plugins\APIShared_Serp"' + "`r`n"
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

function Set-BranchBuildTools($Selection, [string]$ControllerRoot, [switch]$Api, [string[]]$DriverArguments) {
    $suffix = Get-BranchInstallSuffix $Selection.Branch $DriverArguments
    $overrides = @()
    $tracked = @(Invoke-BranchGit $Selection.Root @('-c','core.quotepath=false','ls-files'))
    $installScripts = @('tools/Build.ps1','Testmods/OutpostTest/Build-OutpostTest.ps1','Testmods/MoatMove/tests/Install-Package.ps1',
        'Testmods/DependencyBootstrapProbe/Build.ps1','Testmods/PopularityTest/install.ps1',
        'Testmods/PlagueHeightTest/verify-installed.ps1','Testmods/CrenelatedWallTest/verify-installed.ps1')
    foreach ($relative in $tracked) {
        if ($relative -match '^(_inspect|\.inspect|shcde-script-extender)/') { continue }
        if ($relative -notmatch '(^|/)build\.bat$' -and $relative -notin $installScripts) { continue }
        $path = Join-Path $Selection.Root $relative
        # Working overlays preserve deletions, but ls-files still lists their HEAD entries.
        # Do not recreate or adapt retired drivers; committed-source reads remain strict.
        if ($Selection.Branch -eq 'working' -and -not [IO.File]::Exists($path)) { continue }
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
    $testSelection = Get-BranchTestSelection $Driver $DriverArguments
    if ($testSelection -and $testSelection.Mode -ne 'full' -and
        -not [IO.File]::ReadAllText($selectedDriver).Contains('SHCDE_TEST_MODES: full quick none')) {
        throw "The selected source does not support /tests:$($testSelection.Mode). Select /source:working or a branch containing the new driver."
    }
    $overrides = @(Set-BranchBuildTools $selection $ControllerRoot -Api:$Api -DriverArguments $DriverArguments)
    $oldEnvironment = @{}
    $names = @('SHCDE_BRANCH_SOURCE_ROOT','SHCDE_BRANCH_INSTALL_SUFFIX','SHCDE_BRANCH_NOINSTALL','SHCDE_BRANCH_API_ROOT','SHCDE_BRANCH_API_COMMIT',
        'SHCDE_API_SHARED_DIR','SHCDE_API_SHARED_BRANCH','SHCDE_API_SHARED_MAIN_SELECTION','SHCDE_BRANCH_NAME','SHCDE_BRANCH_COMMIT','SHCDE_BRANCH_MSBUILD_API',
        'SHCDE_GAME_DIR','SHCDESE_EXTENDER_DIR','SHCDE_MSBUILD','SHCDE_NATIVE_BASELINE_ROOT','SHCDE_DEVDIAG')
    foreach ($name in $names) { $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
    $oldDirectory = (Get-Location).Path
    # DEVDIAG is an explicit per-build opt-in; an inherited variable never enables it.
    $devDiag = -not $Api -and $DriverArguments -icontains '/devdiag'
    if ($devDiag -and $Branch -eq 'main') { throw 'DEVDIAG builds are not allowed from main.' }
    # Diagnostic sources may remain on every branch. Normal builds exclude them;
    # release/package gates inspect the compiled assemblies, not source presence.
    try {
        $env:SHCDE_DEVDIAG = if ($devDiag) { '1' } else { $null }
        if ($devDiag) { Write-BranchMessage 'DEVDIAG: development measurement build (never a release).' }
        $env:SHCDE_BRANCH_SOURCE_ROOT = $selection.Root
        $env:SHCDE_BRANCH_NAME = $Branch; $env:SHCDE_BRANCH_COMMIT = $selection.Commit
        $env:SHCDE_BRANCH_INSTALL_SUFFIX = Get-BranchInstallSuffix $Branch $DriverArguments
        $env:SHCDE_BRANCH_NOINSTALL = if ($DriverArguments -icontains '/noinstall' -or $env:SHCDE_PREPARE_ONLY -eq '1') { '1' } else { '0' }
        $env:SHCDE_BRANCH_API_ROOT = $null; $env:SHCDE_BRANCH_API_COMMIT = $null
        $env:SHCDE_BRANCH_MSBUILD_API = $null
        $env:SHCDE_API_SHARED_MAIN_SELECTION = $null
        if (-not $Api -and -not $env:SHCDE_NATIVE_BASELINE_ROOT) {
            $baselineRoot=Join-Path $ControllerRoot '_inspect/CrusaderDE-Native-Baseline'
            if (Test-Path -LiteralPath (Join-Path $baselineRoot 'CURRENT.json')) { $env:SHCDE_NATIVE_BASELINE_ROOT=$baselineRoot }
        }
        $sdk = $null
        $sdkWatch = [Diagnostics.Stopwatch]::StartNew()
        $sdkLock = $null
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
            $apiBranch = if ($apiChoices.Count) { $apiChoices[0] } elseif ($Branch -eq 'working') { 'dev' } else { $Branch }
            $apiRepository = Join-Path $Repository 'APIShared'
            if ($apiBranch -eq 'package') {
                if (-not $env:SHCDE_API_SHARED_DIR) { throw '/apishared:package requires SHCDE_API_SHARED_DIR.' }
                $apiPackage = [IO.Path]::GetFullPath($env:SHCDE_API_SHARED_DIR)
                $apiRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $apiPackage))
                if ($apiPackage -ine (Join-Path $apiRoot 'BepInEx/plugins/APIShared_Serp')) { throw 'Invalid explicit SDK package path.' }
                $sdk = [pscustomobject]@{ Root=$apiRoot; Branch='package'; Commit=((Invoke-BranchGit $apiRoot @('rev-parse','HEAD')) -join '') }
                . (Join-Path $apiRoot 'tools/BuildProof.ps1')
                $null = Assert-ApiBuildProof $apiRoot
                $null = New-WorkingSource $apiRoot (Join-Path $selection.Root 'APIShared')
            } elseif ($apiBranch -in @('dev','local')) {
                $sdk = [pscustomobject]@{ Root=$apiRepository; Branch='dev'; Commit=((Invoke-BranchGit $apiRepository @('rev-parse','HEAD')) -join '');
                    GitStatus=@(Invoke-BranchGit $apiRepository @('status','--porcelain','--untracked-files=normal')) }
                $null = New-LocalApiInspectionSource $apiRepository (Join-Path $selection.Root 'APIShared')
            } elseif ($apiBranch -in @('main','develop')) {
                $apiController = Join-Path $apiRepository 'tools/BranchBuild/BranchBuild.Common.ps1'
                if (-not (Test-Path -LiteralPath $apiController)) { throw 'APIShared branch build tools are missing; update the standalone checkout.' }
                # The cached SDK is shared by every consumer build of this branch. Serialize those
                # builds so none rebuilds it while another still compiles against it.
                $sdkLock = [Threading.Mutex]::new($false, 'Local\SHCDE-APIShared-SDK-' + $apiBranch)
                try {
                    if (-not $sdkLock.WaitOne(0)) { Write-BranchMessage "Waiting for another build that uses the APIShared $apiBranch SDK..."; [void]$sdkLock.WaitOne() }
                } catch [Threading.AbandonedMutexException] { }
                $sdk = Get-CachedApiSdk $Repository $apiRepository $apiBranch -RequireExisting:($testSelection -and $testSelection.Mode -eq 'none')
                # Scoped preflight inspects the same committed APIShared sources.
                $apiSource = New-BranchSource $apiRepository $sdk.Commit (Join-Path $selection.Root 'APIShared')
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
        $sdkWatch.Stop()
        Write-BranchMessage ('[BuildTiming] Category=sdk DurationMs={0}' -f $sdkWatch.ElapsedMilliseconds)
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
        $arguments = @($DriverArguments | Where-Object { $_ -notmatch '^/branch:' -and $_ -ine '/keepsource' })
        if ($arguments -inotcontains '/nopause') { $arguments += '/nopause' }
        if ($env:SHCDE_BRANCH_NOINSTALL -eq '1' -and $arguments -inotcontains '/noinstall') { $arguments += '/noinstall' }
        Assert-ToolPathBudget -Path $selectedDriver
        & $selectedDriver @arguments | Out-Host
        $code = $LASTEXITCODE
        $record = [ordered]@{ SchemaVersion=1; Branch=$Branch; Commit=$selection.Commit; Driver=$Driver; SourceRoot=$selection.Root;
            ApiShared=$sdk; InstallationTargets=$targets; NoInstall=($env:SHCDE_BRANCH_NOINSTALL -eq '1'); ExitCode=$code; DevDiagnostics=$devDiag;
            TestSelection=$testSelection; SdkPreparationMs=$sdkWatch.ElapsedMilliseconds;
            CreatedUtc=[DateTime]::UtcNow.ToString('o'); WorkingSource=($DriverArguments -icontains '/source:working'); SourceFiles=@(Get-BranchSourceDifferences $selection.Root); BuildToolOverrides=$overrides }
        $recordPath = $selection.Root + '.build.json'
        Write-BranchText $recordPath (($record | ConvertTo-Json -Depth 8) + "`r`n")
        Write-BranchMessage "Build record: $recordPath; exit code $code"
        if ($code -ne 0) {
            $failure = [InvalidOperationException]::new("Selected branch build failed with exit code $code. Build record: $recordPath.")
            $failure.Data['BuildExitCode'] = $code
            throw $failure
        }
        return $selection
    } finally {
        if ($sdkLock) { $sdkLock.ReleaseMutex(); $sdkLock.Dispose() }
        Set-Location -LiteralPath $oldDirectory
        foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name], 'Process') }
    }
}
