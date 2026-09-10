function Get-SEChangeCategories([string[]]$Paths) {
    [ordered]@{
        Native = @($Paths | Where-Object { $_ -match '(^ReverseEngineering/structs/|/Detours/|/Interop/)' })
        ManagedApi = @($Paths | Where-Object { $_ -match '^src/SHCDESE\.BepInEx/(API|EventAPI|LUA|GameGlobals)/' })
        Assets = @($Paths | Where-Object { $_ -match '(^deps/(Override|Patches)/|Assets|XAML|\.semod)' })
        Packaging = @($Paths | Where-Object { $_ -match '(^deps/|\.csproj$|mod-types|asset-api)' })
        Documentation = @($Paths | Where-Object { $_ -match '(^docs/|README|CHANGELOG)' })
    }
}

function Test-SEGitIdentity([string]$Repository, [string]$Commit, [string]$Tree) {
    $actualCommit = (& git -C $Repository rev-parse HEAD).Trim()
    $actualTree = (& git -C $Repository rev-parse 'HEAD^{tree}').Trim()
    $status = (& git -C $Repository status --porcelain --untracked-files=no | Out-String).Trim()
    $LASTEXITCODE -eq 0 -and $actualCommit -eq $Commit -and $actualTree -eq $Tree -and -not $status
}

function Resolve-SEExtenderDirectory([string]$ExplicitPath, [string]$InstalledPath) {
    $selected = if ($ExplicitPath) { $ExplicitPath } else { $InstalledPath }
    if (-not (Test-Path -LiteralPath (Join-Path $selected 'SHCDESE.dll') -PathType Leaf)) {
        throw "SHCDESE.dll is missing at $selected. Install the target release or supply -ExtenderDir."
    }
    (Resolve-Path -LiteralPath $selected).Path
}

function Assert-SERuntimeModPreflight([object]$Mod, [string]$Workspace) {
    if (-not $Mod.Plugin) { return }

    $projectPath = Join-Path $Workspace $Mod.Project
    $projectRoot = Split-Path -Parent $projectPath
    $sources = [Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $projectRoot -Recurse -File -Filter '*.cs' | Where-Object {
        $_.FullName -notmatch '[\\/](BepInEx[\\/]plugins|bin|obj|tests|[^\\/]*\.Tests|\.inspect)[\\/]'
    }) {
        $sources.Add($file.FullName)
    }

    [xml]$project = [IO.File]::ReadAllText($projectPath)
    foreach ($compile in @($project.Project.ItemGroup.Compile)) {
        $include = [string]$compile.Include
        if (-not $include -or $include.IndexOfAny([char[]]'*?') -ge 0) { continue }
        $linkedPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $include))
        if ((Test-Path -LiteralPath $linkedPath -PathType Leaf) -and -not $sources.Contains($linkedPath)) {
            $sources.Add($linkedPath)
        }
    }

    $forbiddenJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility'
    $jsonHits = @($sources | Select-String -Pattern $forbiddenJson)
    $projectHits = @(Select-String -LiteralPath $projectPath -Pattern $forbiddenJson)
    if ($jsonHits -or $projectHits) {
        throw "$($Mod.Name): forbidden runtime JSON dependency or serializer found."
    }

    foreach ($sourcePath in $sources) {
        $text = [IO.File]::ReadAllText($sourcePath)
        foreach ($match in [regex]::Matches($text, '\b(OnDestroy|OnDisable|OnApplicationQuit)\s*\([^)]*\)\s*\{')) {
            $open = $text.IndexOf('{', $match.Index)
            $depth = 0
            $end = -1
            for ($index = $open; $index -lt $text.Length; $index++) {
                if ($text[$index] -eq '{') { $depth++ }
                elseif ($text[$index] -eq '}') {
                    $depth--
                    if ($depth -eq 0) { $end = $index; break }
                }
            }
            if ($end -lt 0) { throw "$($Mod.Name): unterminated Unity lifecycle method in $sourcePath." }
            $body = $text.Substring($open, $end - $open + 1)
            if ($body -match '\.Dispose\s*\(' -or $body -match 'DisposeRuntime\s*\(' -or $body -match '\.Stop\s*\(') {
                throw "$($Mod.Name): Unity lifecycle method reaches Dispose/Stop in $sourcePath."
            }
        }
    }
}

function Assert-SEManifestExtenderRange([object]$Manifest, [string]$TargetVersion, [string]$ModName) {
    $minimumText = [string]$Manifest.MinimumScriptExtenderVersion
    $maximumText = [string]$Manifest.MaximumScriptExtenderVersion
    $minimum = $null
    $maximum = $null
    $target = $null
    if (-not $minimumText -and -not $maximumText) { return }
    if ($minimumText -and -not [version]::TryParse($minimumText, [ref]$minimum)) {
        throw "$ModName has an invalid MinimumScriptExtenderVersion: '$minimumText'."
    }
    if (-not [version]::TryParse($TargetVersion, [ref]$target)) {
        throw "Invalid target Script Extender version: '$TargetVersion'."
    }
    if ($maximumText) {
        if (-not [version]::TryParse($maximumText, [ref]$maximum)) {
            throw "$ModName has an invalid MaximumScriptExtenderVersion: '$maximumText'."
        }
        if ($minimumText -and $maximum -lt $minimum) {
            throw "$ModName has a maximum Script Extender version below its minimum."
        }
        if ($target -gt $maximum) {
            throw "$ModName excludes target Script Extender $TargetVersion through MaximumScriptExtenderVersion $maximumText."
        }
    }
    if ($minimumText -and $target -lt $minimum) {
        throw "$ModName requires Script Extender $minimumText or newer, but the target is $TargetVersion."
    }
}

function Get-SEBuildOrder([object[]]$Mods) {
    $pending = @($Mods | Where-Object Plugin)
    $done = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $result = [Collections.Generic.List[object]]::new()
    while ($pending.Count) {
        $ready = @($pending | Where-Object { $deps=@($_.DependsOn); @($deps | Where-Object { -not $done.Contains([string]$_) }).Count -eq 0 } | Sort-Object BuildOrder,Name | Select-Object -First 1)
        if (-not $ready) { throw 'Mod dependency graph contains a cycle or an unknown dependency.' }
        foreach ($mod in $ready) { $result.Add($mod); $done.Add([string]$mod.Name) | Out-Null }
        $pending = @($pending | Where-Object { -not $done.Contains([string]$_.Name) })
    }
    @($result)
}

function Invoke-SECheckpointBuild(
    [object[]]$Mods,
    [string]$Workspace,
    [string]$LogDirectory,
    [hashtable]$State,
    [scriptblock]$SaveState,
    [scriptblock]$VerifyExtender
) {
    foreach ($mod in @(Get-SEBuildOrder $Mods)) {
        & $VerifyExtender
        if ($State.CompletedBuilds -contains $mod.Name) { continue }
        Assert-SERuntimeModPreflight $mod $Workspace
        Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')] Building $($mod.Name)..."
        $log = Join-Path $LogDirectory ($mod.Name + '.build.log')
        $output = @(& (Join-Path $Workspace $mod.BuildDriver) /nopause 2>&1)
        $exitCode = $LASTEXITCODE
        $logText = [regex]::Replace(($output | Out-String), '\r?\n', [Environment]::NewLine)
        if (-not $logText.EndsWith([Environment]::NewLine, [StringComparison]::Ordinal)) { $logText += [Environment]::NewLine }
        [IO.Directory]::CreateDirectory((Split-Path -Parent $log)) | Out-Null
        [IO.File]::WriteAllText($log, $logText, [Text.UTF8Encoding]::new($false))
        if ($exitCode -ne 0) { throw "$($mod.Name) build failed with exit code $exitCode. See $log" }
        $State.CompletedBuilds += $mod.Name
        & $SaveState $State
        Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')] Built $($mod.Name)."
    }
}
