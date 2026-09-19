function Get-SEChangeCategories([string[]]$Paths) {
    [ordered]@{
        NativeHookCandidates = @($Paths | Where-Object { $_ -match '^src/SHCDESE\.BepInEx/(Detours|NativeHooks)/.*\.cs$' })
        InteropContracts = @($Paths | Where-Object { $_ -match '^src/SHCDESE\.BepInEx/Interop/.*\.cs$' })
        SemanticHeaders = @($Paths | Where-Object { $_ -match '^ReverseEngineering/structs/.*\.h$' })
        NativeProjectArtifacts = @($Paths | Where-Object { $_ -match '^ReverseEngineering/structs/.*\.rcnet$' })
        ManagedApi = @($Paths | Where-Object { $_ -match '^src/SHCDESE\.BepInEx/(API|EventAPI|LUA|GameGlobals|Extensions)/' })
        Assets = @($Paths | Where-Object { $_ -match '(^deps/(Override|Patches)/|Assets|XAML|\.semod)' })
        Packaging = @($Paths | Where-Object { $_ -match '(^deps/|\.csproj$|mod-types|asset-api)' })
        Documentation = @($Paths | Where-Object { $_ -match '(^docs/|README|CHANGELOG)' })
    }
}

function Get-SEBaselinePreviousCommit([string]$IdentityPath, [string]$ExtenderRoot, [string]$TargetCommit) {
    if (-not (Test-Path -LiteralPath $IdentityPath -PathType Leaf)) {
        throw "Semantic baseline identity is missing: $IdentityPath"
    }
    $identity = Get-Content -Raw -LiteralPath $IdentityPath | ConvertFrom-Json
    $previousCommit = [string]$identity.scriptExtenderCommit
    if (-not $previousCommit) { throw 'Semantic baseline identity has no scriptExtenderCommit.' }
    & git -C $ExtenderRoot cat-file -e "$previousCommit^{commit}"
    if ($LASTEXITCODE -ne 0) { throw "Semantic baseline commit is unavailable in the extender repository: $previousCommit" }
    & git -C $ExtenderRoot merge-base --is-ancestor $previousCommit $TargetCommit
    if ($LASTEXITCODE -ne 0) { throw "Semantic baseline commit $previousCommit is not an ancestor of target $TargetCommit." }
    $previousCommit
}

function Get-SEBuildSelection([object[]]$Mods, [string[]]$AffectedNames, [string[]]$RequestedBuildNames) {
    $runtimeMods = @($Mods | Where-Object Plugin)
    $byName = @{}
    foreach ($mod in $runtimeMods) { $byName[[string]$mod.Name] = $mod }
    $selected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @($AffectedNames) + @($RequestedBuildNames)) {
        if (-not $name) { continue }
        if (-not $byName.ContainsKey([string]$name)) { throw "Impact review references an unknown active runtime mod: $name" }
        $selected.Add([string]$name) | Out-Null
    }

    $changed = $true
    while ($changed) {
        $changed = $false
        foreach ($name in @($selected)) {
            foreach ($dependency in @($byName[$name].DependsOn)) {
                if (-not $byName.ContainsKey([string]$dependency)) { throw "$name has an unknown active dependency: $dependency" }
                if ($selected.Add([string]$dependency)) { $changed = $true }
            }
        }
        foreach ($mod in $runtimeMods) {
            if (@($mod.DependsOn | Where-Object { $selected.Contains([string]$_) }).Count -gt 0 -and $selected.Add([string]$mod.Name)) {
                $changed = $true
            }
        }
    }
    @($runtimeMods | Where-Object { $selected.Contains([string]$_.Name) })
}

function Assert-SEImpactReview(
    [object]$Review,
    [string[]]$ChangedFiles,
    [object[]]$ActiveMods,
    [string]$BaselinePreviousCommit
) {
    if ($null -eq $Review) { throw 'Compatibility plan must contain ImpactReview.' }
    if ([string]$Review.BaselinePreviousCommit -ne $BaselinePreviousCommit) {
        throw "ImpactReview baseline commit does not match semantic provenance: expected $BaselinePreviousCommit."
    }

    $changedSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($path in $ChangedFiles) { $changedSet.Add([string]$path) | Out-Null }
    $classified = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($propertyName in @('CrusaderNativeFiles','ExternalNativeFiles','NonSemanticArtifacts')) {
        if ($null -eq $Review.PSObject.Properties[$propertyName]) { throw "ImpactReview is missing $propertyName." }
        foreach ($path in @($Review.$propertyName | ForEach-Object { [string]$_ })) {
            if (-not $changedSet.Contains($path)) { throw "ImpactReview classifies an unchanged file: $path" }
            if (-not $classified.Add($path)) { throw "ImpactReview classifies a file more than once: $path" }
        }
    }
    if ($null -eq $Review.PSObject.Properties['ChangedPublicContracts']) { throw 'ImpactReview is missing ChangedPublicContracts.' }
    foreach ($contract in @($Review.ChangedPublicContracts)) {
        if (-not [string]$contract.Path -or -not [string]$contract.Symbol -or -not [string]$contract.Compatibility) {
            throw 'ImpactReview contains an incomplete public-contract entry.'
        }
        if (-not $changedSet.Contains([string]$contract.Path)) { throw "ImpactReview public contract refers to an unchanged file: $($contract.Path)" }
    }

    $affected = @($Review.AffectedMods | ForEach-Object { [string]$_ })
    $requested = @($Review.BuildMods | ForEach-Object { [string]$_ })
    $selection = @(Get-SEBuildSelection $ActiveMods $affected $requested)
    $expectedNames = @($selection.Name | Sort-Object)
    $declaredNames = @($requested | Sort-Object -Unique)
    if (($expectedNames -join "`n") -cne ($declaredNames -join "`n")) {
        throw "ImpactReview BuildMods must equal the affected dependency closure. Expected: $($expectedNames -join ', ')."
    }
    $selection
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

function Assert-SENoGenericExtenderDependency([object]$Manifest, [string]$ModName) {
    $genericDependencies = @($Manifest.Dependencies | Where-Object {
        $null -ne $_ -and [string]$_.GUID -eq '000shcdese'
    })
    if ($genericDependencies.Count -gt 0) {
        throw "$ModName declares 000shcdese in info.json Dependencies. Use MinimumScriptExtenderVersion for the SerpsModsHost warning and a versioned BepInDependency for load protection."
    }
}

function Assert-SEMetadataMutationArguments(
    [bool]$HasCompatibilityPlan,
    [string]$VersionMode,
    [bool]$VersionsFileSpecified,
    [bool]$ChangelogSpecified
) {
    $legacyMutationRequested = $VersionMode -ne 'Existing' -or $VersionsFileSpecified -or $ChangelogSpecified
    if ($legacyMutationRequested) {
        throw 'Mod metadata mutations must be declared per mod in a compatibility plan; VersionMode, VersionsFile and Changelog cannot request bulk updates.'
    }
}

function Get-SEReleaseNativeHookFingerprint([string]$Workspace, [string[]]$Projects) {
    $rows = [Collections.Generic.List[string]]::new()
    $operationCount = 0
    $nativeMutationPattern = '\.(AddDetour|AddInline|AddContextHook)\s*\(|\bCodePatch\.Write\s*\('
    foreach ($project in @($Projects)) {
        $projectRoot = Join-Path $Workspace $project
        if (-not (Test-Path -LiteralPath $projectRoot -PathType Container)) {
            throw "Release project is missing: $project"
        }
        foreach ($file in Get-ChildItem -LiteralPath $projectRoot -Recurse -File -Filter '*.cs' | Where-Object {
            $_.FullName -notmatch '[\\/](BepInEx[\\/]plugins|bin|obj|tests|[^\\/]*\.Tests|\.inspect)[\\/]'
        }) {
            $text = [IO.File]::ReadAllText($file.FullName)
            $matches = [regex]::Matches($text, $nativeMutationPattern)
            if ($matches.Count -eq 0) { continue }
            $relative = $file.FullName.Substring($Workspace.Length).TrimStart([char[]]'\/').Replace('\', '/')
            $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $rows.Add("$relative|$hash")
            $operationCount += $matches.Count
        }
    }
    $orderedRows = @($rows | Sort-Object)
    $payload = ($orderedRows -join "`n") + "`n"
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($payload)
        $fingerprint = ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
    [pscustomobject]@{
        SourceFileCount = $orderedRows.Count
        OperationCount = $operationCount
        Fingerprint = $fingerprint
        Sources = @($orderedRows | ForEach-Object { ($_ -split '\|', 2)[0] })
    }
}

function Assert-SEReleaseHookAudit(
    [string]$AuditPath,
    [string]$Workspace,
    [string]$ExtenderRoot,
    [string]$NativePath
) {
    if (-not (Test-Path -LiteralPath $AuditPath -PathType Leaf)) {
        throw "Release hook audit is required for native Script Extender changes: $AuditPath"
    }
    $audit = Get-Content -Raw -LiteralPath $AuditPath | ConvertFrom-Json
    $releaseInventoryPath = Join-Path $Workspace 'Shared\Release\release-projects.json'
    $releaseProjects = @((Get-Content -Raw -LiteralPath $releaseInventoryPath | ConvertFrom-Json).Projects | ForEach-Object { [string]$_ })
    $auditedProjects = @($audit.ReleaseProjects | ForEach-Object { [string]$_ })
    if (($releaseProjects -join "`n") -cne ($auditedProjects -join "`n")) {
        throw 'Release hook audit project inventory is stale.'
    }

    $commit = (& git -C $ExtenderRoot rev-parse HEAD).Trim()
    $tree = (& git -C $ExtenderRoot rev-parse 'HEAD^{tree}').Trim()
    $nativeHash = (Get-FileHash -LiteralPath $NativePath -Algorithm SHA256).Hash
    if ($commit -ne [string]$audit.ExtenderCommit -or
        $tree -ne [string]$audit.ExtenderTree -or
        $nativeHash -ne [string]$audit.NativeHash) {
        throw 'Release hook audit identity does not match the current extender tree and native DLL.'
    }

    $fingerprint = Get-SEReleaseNativeHookFingerprint $Workspace $releaseProjects
    if ($fingerprint.SourceFileCount -ne [int]$audit.SourceFileCount -or
        $fingerprint.OperationCount -ne [int]$audit.OperationCount -or
        $fingerprint.Fingerprint -ne [string]$audit.SourceFingerprint) {
        throw 'Release native hook/patch sources changed after the compatibility audit; a new audit is required.'
    }
    if ([string]$audit.Result -ne 'Compatible' -or -not [bool]$audit.ExtenderChangesReviewed) {
        throw 'Release hook audit has no affirmative reviewed compatibility result.'
    }
    $allowedClassifications = @('CallSiteAfterOwnedDetour', 'NestedOriginalPath', 'DelegateCallThrough', 'ReferencedOwnedFunction', 'NoOverlap')
    foreach ($interaction in @($audit.ReviewedInteractions)) {
        if (-not [string]$interaction.Id -or [string]$interaction.Classification -notin $allowedClassifications -or -not [string]$interaction.Rationale) {
            throw 'Release hook audit contains an incomplete or unknown interaction classification.'
        }
    }
    if (@($audit.ReviewedInteractions).Count -eq 0) {
        throw 'Release hook audit contains no reviewed interactions.'
    }
    $fingerprint
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
