Set-StrictMode -Version Latest

function Get-SCDEModManagerPackageId {
    param([Parameter(Mandatory)][string]$Guid)

    if ([string]::IsNullOrWhiteSpace($Guid)) { throw 'A non-empty plugin GUID is required.' }
    $bytes = [Text.Encoding]::UTF8.GetBytes($Guid.Trim().ToLowerInvariant())
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $hash = ([BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    } finally {
        $algorithm.Dispose()
    }
    return 'se-' + $hash.Substring(0, 32)
}

function Get-SCDEModManagerVersionBounds {
    param([Parameter(Mandatory)]$Info)

    $requirements = [System.Collections.Generic.List[object]]::new()
    $requirements.Add([PSCustomObject]@{
        MinimumVersion = $(if ($null -ne $Info.PSObject.Properties['MinimumScriptExtenderVersion']) { [string]$Info.MinimumScriptExtenderVersion } else { '' })
        MaximumVersion = $(if ($null -ne $Info.PSObject.Properties['MaximumScriptExtenderVersion']) { [string]$Info.MaximumScriptExtenderVersion } else { '' })
    })
    if ($null -ne $Info.PSObject.Properties['Dependencies'] -and $null -ne $Info.Dependencies) {
        foreach ($dependency in @($Info.Dependencies)) {
            if ($null -ne $dependency -and $null -ne $dependency.PSObject.Properties['GUID'] -and
                [string]$dependency.GUID -ieq '000shcdese') {
                $requirements.Add($dependency)
            }
        }
    }

    $minimum = ''
    $maximum = ''
    foreach ($requirement in $requirements) {
        $candidateMinimum = if ($null -ne $requirement.PSObject.Properties['MinimumVersion']) { [string]$requirement.MinimumVersion } else { '' }
        $candidateMaximum = if ($null -ne $requirement.PSObject.Properties['MaximumVersion']) { [string]$requirement.MaximumVersion } else { '' }
        if (-not [string]::IsNullOrWhiteSpace($candidateMinimum)) {
            $candidateMinimum = $candidateMinimum.Trim()
            if ($candidateMinimum -notmatch '^\d+(?:\.\d+)*(?:[-+][0-9A-Za-z.-]+)?$') { throw "Invalid Script Extender minimum version '$candidateMinimum'." }
            if ([string]::IsNullOrWhiteSpace($minimum) -or (Compare-SemanticVersion -Left $candidateMinimum -Right $minimum) -gt 0) { $minimum = $candidateMinimum }
        }
        if (-not [string]::IsNullOrWhiteSpace($candidateMaximum)) {
            $candidateMaximum = $candidateMaximum.Trim()
            if ($candidateMaximum -notmatch '^\d+(?:\.\d+)*(?:[-+][0-9A-Za-z.-]+)?$') { throw "Invalid Script Extender maximum version '$candidateMaximum'." }
            if ([string]::IsNullOrWhiteSpace($maximum) -or (Compare-SemanticVersion -Left $candidateMaximum -Right $maximum) -lt 0) { $maximum = $candidateMaximum }
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($minimum) -and -not [string]::IsNullOrWhiteSpace($maximum) -and
        (Compare-SemanticVersion -Left $minimum -Right $maximum) -gt 0) {
        throw "Inconsistent Script Extender version range $minimum - $maximum."
    }
    return [PSCustomObject]@{ MinimumVersion = $minimum; MaximumVersion = $maximum }
}

function New-SCDEModManagerManifest {
    param(
        [Parameter(Mandatory)]$Info,
        [Parameter(Mandatory)][string]$ApiSharedGuid,
        [switch]$ApiSharedConsumer
    )

    foreach ($property in @('GUID', 'Name', 'Version')) {
        if ($null -eq $Info.PSObject.Properties[$property] -or [string]::IsNullOrWhiteSpace([string]$Info.$property)) {
            throw "Plugin info.json is missing $property."
        }
    }
    $guid = ([string]$Info.GUID).Trim()
    if ($guid -match '[/\\]' -or $guid.Length -gt 128) { throw "Unsafe plugin GUID '$guid'." }
    if ($guid -in @('000shcdese', 'uuimgui', 'scdemultiplayercompatibility')) { throw "Reserved plugin GUID '$guid'." }
    $bounds = Get-SCDEModManagerVersionBounds -Info $Info
    $dependencies = [System.Collections.Generic.List[object]]::new()
    $dependencies.Add([ordered]@{ id = 'shcde-script-extender' })
    if ($ApiSharedConsumer) {
        $dependencies.Add([ordered]@{ id = Get-SCDEModManagerPackageId -Guid $ApiSharedGuid })
    }
    $versionCheckUrl = if ($null -ne $Info.PSObject.Properties['VersionCheckUrl'] -and $null -ne $Info.VersionCheckUrl) { [string]$Info.VersionCheckUrl } else { '' }
    return [ordered]@{
        id = Get-SCDEModManagerPackageId -Guid $guid
        name = ([string]$Info.Name).Trim()
        version = ([string]$Info.Version).Trim()
        author = $(if ($null -ne $Info.PSObject.Properties['Author'] -and $null -ne $Info.Author) { ([string]$Info.Author).Trim() } else { '' })
        description = $(if ($null -ne $Info.PSObject.Properties['Description'] -and $null -ne $Info.Description) { ([string]$Info.Description).Trim() } else { '' })
        gameVersion = ''
        dependencies = @($dependencies)
        scriptExtender = [ordered]@{
            guid = $guid
            versionCheckUrl = $versionCheckUrl
            minimumVersion = [string]$bounds.MinimumVersion
            maximumVersion = [string]$bounds.MaximumVersion
            metadataVersion = 2
        }
    }
}

function Test-SCDEModManagerPackage {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ExpectedGuid,
        [Parameter(Mandatory)][string]$ExpectedVersion,
        [string]$ApiSharedGuid,
        [switch]$ApiSharedConsumer
    )

    if ([IO.Path]::GetExtension($Path) -cne '.scdemod') { throw "Manager package must use the .scdemod extension: $Path" }
    Add-Type -AssemblyName System.IO.Compression
    $stream = [IO.File]::OpenRead($Path)
    $archive = $null
    try {
        $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read, $false)
        $fileEntries = @($archive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) })
        $normalized = @($fileEntries | ForEach-Object { $_.FullName.Replace('\', '/').TrimStart('/') })
        $duplicate = @($normalized | Group-Object { $_.ToLowerInvariant() } | Where-Object Count -gt 1)
        if ($duplicate.Count -gt 0) { throw "Manager package contains duplicate paths: $(@($duplicate.Name) -join ', ')" }
        foreach ($entryPath in $normalized) {
            if ([string]::IsNullOrWhiteSpace($entryPath) -or $entryPath.StartsWith('/') -or
                @($entryPath.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0) {
                throw "Manager package contains unsafe path '$entryPath'."
            }
        }
        $manifestEntries = @($fileEntries | Where-Object { $_.FullName.Replace('\', '/').TrimStart('/') -ceq 'manifest.json' })
        if ($manifestEntries.Count -ne 1) { throw 'Manager package must contain exactly one root manifest.json.' }
        $reader = [IO.StreamReader]::new($manifestEntries[0].Open(), [Text.UTF8Encoding]::new($false), $true)
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        $expectedId = Get-SCDEModManagerPackageId -Guid $ExpectedGuid
        if ([string]$manifest.id -cne $expectedId -or [string]$manifest.version -cne $ExpectedVersion -or
            [string]$manifest.scriptExtender.guid -cne $ExpectedGuid -or [int]$manifest.scriptExtender.metadataVersion -ne 2) {
            throw 'Manager package manifest identity does not match the authoritative info.json.'
        }
        $payloadPrefix = "payload/BepInEx/plugins/$ExpectedGuid/"
        $payloadFiles = @($normalized | Where-Object { $_.StartsWith($payloadPrefix, [StringComparison]::Ordinal) })
        if ($payloadFiles.Count -eq 0) { throw "Manager package contains no files below $payloadPrefix" }
        $outside = @($normalized | Where-Object { $_ -cne 'manifest.json' -and -not $_.StartsWith($payloadPrefix, [StringComparison]::Ordinal) })
        if ($outside.Count -gt 0) { throw "Manager package contains files outside its single plugin payload: $($outside -join ', ')" }
        $runtimeFiles = @($payloadFiles | Where-Object { $_ -match '(?i)(^|/)LobbyModSettings(/|$)' -or $_ -match '(?i)\.(msgpack|log|tmp)$' })
        if ($runtimeFiles.Count -gt 0) { throw "Manager package contains local runtime data: $($runtimeFiles -join ', ')" }
        $dependencies = @($manifest.dependencies)
        if (@($dependencies | Where-Object { [string]$_.id -ceq 'shcde-script-extender' }).Count -ne 1) {
            throw 'Manager package must declare exactly one Script Extender dependency.'
        }
        foreach ($dependency in $dependencies) {
            if ($null -ne $dependency.PSObject.Properties['version']) { throw "Manager dependency '$([string]$dependency.id)' must not use an exact version." }
        }
        if ($ApiSharedConsumer) {
            $apiId = Get-SCDEModManagerPackageId -Guid $ApiSharedGuid
            if (@($dependencies | Where-Object { [string]$_.id -ceq $apiId }).Count -ne 1) {
                throw "Manager package must depend on APIShared package $apiId."
            }
        } elseif (@($dependencies).Count -ne 1) {
            throw 'Non-consumer Manager package contains an unexpected dependency.'
        }
        return [PSCustomObject]@{
            Id = $expectedId
            Manifest = $manifest
            Files = $payloadFiles
            Sha256 = Get-Sha256Hex -Path $Path
            Size = (Get-Item -LiteralPath $Path).Length
        }
    } finally {
        if ($null -ne $archive) { $archive.Dispose() }
        $stream.Dispose()
    }
}

function New-SCDEModManagerPackage {
    param(
        [Parameter(Mandatory)][string]$PluginDirectory,
        [Parameter(Mandatory)]$Info,
        [Parameter(Mandatory)][string]$DestinationPath,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][string]$ApiSharedGuid,
        [switch]$ApiSharedConsumer
    )

    if (-not (Test-Path -LiteralPath $PluginDirectory -PathType Container)) { throw "Plugin package directory is missing: $PluginDirectory" }
    $guid = ([string]$Info.GUID).Trim()
    $stage = Join-Path $WorkingDirectory 'scdemod-stage'
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    [void](New-Item -ItemType Directory -Path $stage -Force)
    $payload = Join-Path $stage "payload\BepInEx\plugins\$guid"
    [void](New-Item -ItemType Directory -Path $payload -Force)
    foreach ($child in @(Get-ChildItem -LiteralPath $PluginDirectory -Force)) {
        Copy-Item -LiteralPath $child.FullName -Destination $payload -Recurse -Force
    }
    $manifest = New-SCDEModManagerManifest -Info $Info -ApiSharedGuid $ApiSharedGuid -ApiSharedConsumer:$ApiSharedConsumer
    Write-Utf8CrLfFile -Path (Join-Path $stage 'manifest.json') -Text ($manifest | ConvertTo-Json -Depth 8)
    $temporaryZip = $DestinationPath + '.zip'
    if (Test-Path -LiteralPath $temporaryZip) { Remove-Item -LiteralPath $temporaryZip -Force }
    if (Test-Path -LiteralPath $DestinationPath) { Remove-Item -LiteralPath $DestinationPath -Force }
    Compress-Archive -LiteralPath (Join-Path $stage 'manifest.json'), (Join-Path $stage 'payload') -DestinationPath $temporaryZip -CompressionLevel Optimal
    Move-Item -LiteralPath $temporaryZip -Destination $DestinationPath
    return Test-SCDEModManagerPackage -Path $DestinationPath -ExpectedGuid $guid -ExpectedVersion ([string]$Info.Version) `
        -ApiSharedGuid $ApiSharedGuid -ApiSharedConsumer:$ApiSharedConsumer
}
