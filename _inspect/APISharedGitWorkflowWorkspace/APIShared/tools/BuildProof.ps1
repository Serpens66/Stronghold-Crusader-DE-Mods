# Local build evidence only; no game or mod-workspace dependency is introduced.
function Initialize-ApiBuildEnvironment([string]$Root) {
    # Reuse local machine paths, not the old build's integrity decision. The build
    # still validates references and creates fresh evidence for its current inputs.
    $proofPath = Join-Path $Root '.local/build-proof.json'
    $proof = $null
    if (-not $env:SHCDE_GAME_DIR -and (Test-Path -LiteralPath $proofPath)) {
        try { $proof = [IO.File]::ReadAllText($proofPath) | ConvertFrom-Json }
        catch { throw 'Local build paths are unreadable. Set SHCDE_GAME_DIR explicitly or correct .local/build-proof.json.' }
        if ($proof.SchemaVersion -ne 1) { throw 'Unsupported local build paths. Set SHCDE_GAME_DIR explicitly.' }
    }
    $game = if ($env:SHCDE_GAME_DIR) { $env:SHCDE_GAME_DIR }
        elseif ($proof -and $proof.PSObject.Properties['GameDir']) { [string]$proof.GameDir }
        else { '' }
    if (-not $game) { throw 'Set SHCDE_GAME_DIR to your Stronghold Crusader Definitive Edition installation before the first build.' }
    $game = [IO.Path]::GetFullPath($game)
    if (-not (Test-Path -LiteralPath (Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll') -PathType Leaf)) {
        throw "The real installed Assembly-CSharp.dll was not found in SHCDE_GAME_DIR: $game"
    }
    $extender = if ($env:SHCDESE_EXTENDER_DIR) { $env:SHCDESE_EXTENDER_DIR }
        elseif ($proof -and $proof.PSObject.Properties['ExtenderDir'] -and $proof.ExtenderDir) { [string]$proof.ExtenderDir }
        else { Join-Path $game 'BepInEx/plugins/000shcdese' }
    $extender = [IO.Path]::GetFullPath($extender)
    if (-not (Test-Path -LiteralPath (Join-Path $extender 'SHCDESE.dll') -PathType Leaf)) {
        throw "Install Script Extender or set SHCDESE_EXTENDER_DIR. Missing SHCDESE.dll in: $extender"
    }
    # Process-local only: children inherit this without requiring Windows-wide settings.
    $env:SHCDE_GAME_DIR = $game
    $env:SHCDESE_EXTENDER_DIR = $extender
    if (-not $env:SHCDE_MSBUILD -and $proof -and $proof.PSObject.Properties['MSBuild'] -and $proof.MSBuild -and (Test-Path -LiteralPath $proof.MSBuild -PathType Leaf)) {
        $env:SHCDE_MSBUILD = [string]$proof.MSBuild
    }
}
function Get-ApiFileHash([string]$Path) {
    $stream=[IO.File]::OpenRead($Path); $sha=[Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose(); $stream.Dispose() }
}
function Get-ApiInputState([string]$Root, [string]$GameDir, [string]$ExtenderDir, [string]$MSBuild) {
    $Root=[IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $rows = [Collections.Generic.List[string]]::new()
    $files = @(Get-ChildItem -LiteralPath (Join-Path $Root 'src'),(Join-Path $Root 'Properties'),(Join-Path $Root 'Patches') -Recurse -File |
        Where-Object { $_.Extension -in @('.cs','.xaml') })
    $files += @(Get-ChildItem -LiteralPath $Root -File | Where-Object { $_.Name -match '^Directory\..*\.(props|targets)$|^APIShared\.csproj$|^info\.json$' })
    $files += @(Get-Item -LiteralPath (Join-Path $Root 'tools/Build.ps1'),(Join-Path $Root 'tools/BuildProof.ps1'),(Join-Path $Root 'tools/WorkshopPackage.ps1'))
    $files += @(Get-ChildItem -LiteralPath (Join-Path $Root 'Workshop') -Recurse -File)
    if ([IO.Directory]::Exists((Join-Path $Root 'Override'))) { $files += @(Get-ChildItem -LiteralPath (Join-Path $Root 'Override') -Recurse -File) }
    $files += @(Get-Item -LiteralPath (Join-Path $Root 'tools/Prepare-WorkshopPackage.ps1'),(Join-Path $Root 'tools/New-WorkshopBootstrap.ps1'))
    $files += @(Get-ChildItem -LiteralPath (Join-Path $Root 'tools/WorkshopUploader') -File | Where-Object { $_.Extension -in @('.cs','.csproj','.ps1','.bat','.sh','.config','.manifest') })
    $files += @(Get-Item -LiteralPath (Join-Path $Root 'tools/PublisherPackage.ps1'))
    foreach ($file in $files | Sort-Object FullName -Unique) {
        $relative = $file.FullName.Substring($Root.Length + 1).Replace('\','/')
        $rows.Add($relative + ':' + (Get-ApiFileHash $file.FullName))
    }
    if ([IO.File]::Exists((Join-Path $Root 'tools/WorkshopUploader/WorkshopUploader.csproj'))) {
        . (Join-Path $Root 'tools/WorkshopUploader/PublisherBuildProof.ps1')
        foreach ($inputFile in Get-WorkshopPublisherInputs $Root $GameDir) { $rows.Add('publisher/' + $inputFile.Path + ':' + $inputFile.Sha256) }
    }
    [xml]$project = [IO.File]::ReadAllText((Join-Path $Root 'APIShared.csproj'))
    foreach ($node in $project.SelectNodes('//*[local-name()="HintPath"]')) {
        $path = $node.InnerText.Replace('$(GameDir)',$GameDir).Replace('$(ExtenderDir)',$ExtenderDir)
        if ($path.Contains('$(')) { throw "Unresolved build reference: $path" }
        if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $Root $path }
        $name = $node.ParentNode.Include
        $rows.Add('reference/' + $name + ':' + (Get-ApiFileHash $path))
    }
    $rows.Add('MSBuild:' + (Get-ApiFileHash $MSBuild))
    # Ordinal order is stable across Windows PowerShell 5 and PowerShell 7 cultures.
    $rows.Sort([StringComparer]::Ordinal)
    $bytes = [Text.Encoding]::UTF8.GetBytes($rows -join "`n")
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $fingerprint = ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
    return [pscustomobject]@{ Fingerprint=$fingerprint; Inputs=$rows.ToArray() }
}
function Get-ApiPackageFiles([string]$Root) {
    $package = [IO.Path]::GetFullPath((Join-Path $Root 'BepInEx/plugins/APIShared_Serp'))
    return @(Get-ChildItem -LiteralPath $package -Recurse -File | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ Path=$_.FullName.Substring($package.Length+1).Replace('\','/'); Sha256=(Get-ApiFileHash $_.FullName) }
    })
}
function Write-ApiBuildProof([string]$Root, [string]$GameDir, [string]$ExtenderDir, [string]$MSBuild, $InitialState) {
    $final = Get-ApiInputState $Root $GameDir $ExtenderDir $MSBuild
    if ($InitialState.Fingerprint -ne $final.Fingerprint) { throw 'APIShared inputs changed during the build. Build again.' }
    $commit = (& git -C $Root rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0) { $commit = $null }
    $proof = [ordered]@{ SchemaVersion=1; Commit=$commit; CreatedUtc=[DateTime]::UtcNow.ToString('o'); GameDir=$GameDir; ExtenderDir=$ExtenderDir; MSBuild=$MSBuild; Fingerprint=$final.Fingerprint; Inputs=$final.Inputs; Files=(Get-ApiPackageFiles $Root); PublisherFiles=(Get-ApiPublisherProofFiles $Root) }
    $directory = Join-Path $Root '.local'
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    [IO.File]::WriteAllText((Join-Path $directory 'build-proof.json'),(($proof | ConvertTo-Json -Depth 6).Replace("`r`n","`n").Replace("`n","`r`n") + "`r`n"),[Text.UTF8Encoding]::new($false))
}
function Assert-ApiBuildProof([string]$Root, [switch]$Release) {
    $path = Join-Path $Root '.local/build-proof.json'
    if (-not (Test-Path -LiteralPath $path)) { throw 'APIShared build evidence is missing. Run APIShared/build.bat /nopause first.' }
    $proof = [IO.File]::ReadAllText($path) | ConvertFrom-Json
    if ($proof.SchemaVersion -ne 1) { throw 'Unsupported APIShared build evidence. Build APIShared again.' }
    $state = Get-ApiInputState $Root $proof.GameDir $proof.ExtenderDir $proof.MSBuild
    if ($state.Fingerprint -ne $proof.Fingerprint) { throw 'APIShared package is stale. Run APIShared/build.bat /nopause first.' }
    Assert-ApiPublisherEvidence $Root $proof
    $actual = @(Get-ApiPackageFiles $Root)
    $expected = @($proof.Files)
    if ($actual.Count -ne $expected.Count) { throw 'APIShared package contents changed after the build. Build APIShared again.' }
    foreach ($file in $expected) {
        $match = @($actual | Where-Object { $_.Path -ceq $file.Path -and $_.Sha256 -ceq $file.Sha256 })
        if ($match.Count -ne 1) { throw "APIShared package hash mismatch: $($file.Path). Build APIShared again." }
    }
    if ($Release) {
        $commit = (& git -C $Root rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0 -or -not $commit) { throw 'Cannot determine the reviewed APIShared source commit.' }
        $status = @(& git -C $Root status --porcelain --untracked-files=normal)
        if ($LASTEXITCODE -ne 0 -or $status.Count) { throw 'APIShared release requires a clean checkout.' }
        # A commit made after a tested build does not change its bytes. The input
        # fingerprint and complete package hashes were checked above; retain the
        # original provenance instead of demanding an identical commit identifier.
        if ($commit -cne $proof.Commit) {
            Write-Verbose "APIShared build inputs and package hashes match the clean checkout; build commit $($proof.Commit), current commit $commit."
        }
    }
    return $proof
}
function Get-ApiPublisherProofFiles([string]$Root) {
    $directory = [IO.Path]::GetFullPath((Join-Path $Root 'BepInEx/tools/APISharedWorkshopPublisher'))
    return @(Get-ChildItem -LiteralPath $directory -Recurse -File | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ Path=$_.FullName.Substring($directory.Length+1).Replace('\','/'); Sha256=(Get-ApiFileHash $_.FullName) }
    })
}
function Assert-ApiPublisherEvidence([string]$Root,$Proof) {
    $actual = @(Get-ApiPublisherProofFiles $Root)
    if (@($Proof.PublisherFiles).Count -ne $actual.Count -or $actual.Count -eq 0) { throw 'Missing or changed publisher build evidence.' }
    foreach ($file in $actual) {
        if (@($Proof.PublisherFiles | Where-Object { $_.Path -ceq $file.Path -and $_.Sha256 -ceq $file.Sha256 }).Count -ne 1) { throw "Publisher build hash mismatch: $($file.Path)" }
    }
}
