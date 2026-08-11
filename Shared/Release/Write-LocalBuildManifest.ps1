param([Parameter(Mandatory)][string]$ModName)

. (Join-Path $PSScriptRoot 'Release.Common.ps1')

try {
    $metadata = Get-PluginMetadata -ModName $ModName
    $extenderDir = Get-ExtenderDirectory -Metadata $metadata
    $files = @(Get-ChildItem -LiteralPath $metadata.PackageDir -File -Recurse | Sort-Object FullName)
    if ($files.Count -eq 0) { throw "Plugin package is empty: $($metadata.PackageDir)" }
    $runtimeFiles = @($files | Where-Object { $_.Extension -in @('.msgpack', '.log', '.tmp') -or $_.FullName -match '\\LobbyModSettings\\' })
    if ($runtimeFiles.Count -gt 0) { throw 'Plugin package contains local runtime data.' }
    $commitResult = Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', $metadata.Config.Root, 'rev-parse', 'HEAD')
    $commit = ($commitResult.Output -join '').Trim()
    $status = Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', $metadata.Config.Root, 'status', '--porcelain=v1', '--untracked-files=normal')
    $records = @(foreach ($file in $files) { Get-FileHashRecord -Path $file.FullName -BasePath $metadata.PackageDir })
    $manifest = [ordered]@{
        SchemaVersion = 1
        Kind = 'local-build'
        Attested = $false
        Repository = "https://github.com/$($metadata.Config.Repository)"
        Commit = $commit
        GitCleanAtManifest = ($status.Output.Count -eq 0)
        GitStatus = @($status.Output | ForEach-Object { [string]$_ })
        Mod = $ModName
        PluginGuid = [string]$metadata.Manifest.GUID
        Version = $metadata.Version
        CreatedUtc = [DateTime]::UtcNow.ToString('o')
        PackageDirectory = $metadata.PackageDir
        Files = $records
        Dependencies = @(Get-DependencyRecords -Metadata $metadata -ExtenderDir $extenderDir)
        TrustStatement = 'Local build record only. This file is not a GitHub release and is not independently attested.'
    }
    $outputDir = Join-Path $metadata.Config.Root ".release-output\local\$ModName"
    [void](New-Item -ItemType Directory -Path $outputDir -Force)
    $outputPath = Join-Path $outputDir 'latest.provenance.json'
    Write-Utf8CrLfFile -Path $outputPath -Text ($manifest | ConvertTo-Json -Depth 10)
    Write-Host "Lokaler Buildnachweis: $outputPath"
    exit 0
} catch {
    Write-Error $_
    exit 1
}
