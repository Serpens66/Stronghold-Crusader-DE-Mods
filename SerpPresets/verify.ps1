param(
    [Parameter(Mandatory = $true)][string]$PackageRoot,
    [string]$CompareRoot
)

$ErrorActionPreference = 'Stop'

try {
    $package = [IO.Path]::GetFullPath($PackageRoot)
    $expected = @(
        'Override\BuildingCosts_Serp\preset_ok.json',
        'Override\BuildingLimit_Serp\preset_low.json',
        'Override\BuildingLimit_Serp\preset_ok.json',
        'Override\ExtraFeatures_Serp\preset_ok.json',
        'Override\StartConditions_Serp\preset_ok.json',
        'Override\UnitCosts_Serp\preset_ok.json',
        'Override\UnitLimit_Serp\preset_ok.json'
    )

    $metadataPath = Join-Path $package 'info.json'
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    if ($metadata.GUID -cne 'SerpPresets_Serp' -or
        $metadata.Version -cne '1.0.0' -or
        $metadata.Manifest -ne 0 -or
        $metadata.AssetMode -cne 'Local' -or
        $metadata.NetworkMode -ne 0) {
        throw 'info.json contains unexpected SerpPresets metadata.'
    }

    $overrideRoot = Join-Path $package 'Override'
    $actual = @(Get-ChildItem -LiteralPath $overrideRoot -File -Recurse | ForEach-Object {
        $_.FullName.Substring($package.TrimEnd('\').Length + 1)
    })
    if ($actual.Count -ne $expected.Count) {
        throw "Expected $($expected.Count) preset files; found $($actual.Count)."
    }
    foreach ($relativePath in $actual) {
        if ($expected -cnotcontains $relativePath) {
            throw "Unexpected preset file: $relativePath"
        }
    }

    foreach ($relativePath in $expected) {
        $path = Join-Path $package $relativePath
        $file = Get-Item -LiteralPath $path
        if ($file.Length -le 0 -or $file.Length -gt 1MB) {
            throw "Preset file has an invalid size: $relativePath"
        }
        $doc = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        $target = Split-Path -Leaf (Split-Path -Parent $path)
        $id = $file.BaseName.Substring('preset_'.Length)
        if ($doc.schemaVersion -ne 1 -or $doc.id -cne $id -or
            $doc.targetGuid -cne $target -or [string]::IsNullOrWhiteSpace($doc.name)) {
            throw "Preset identity or schema mismatch: $relativePath"
        }
        $settings = @($doc.settings.PSObject.Properties)
        if ($settings.Count -lt 1 -or $settings.Count -gt 1024) {
            throw "Invalid settings count: $relativePath"
        }
        foreach ($setting in $settings) {
            $mode = $setting.Value.mode
            if ($mode -cnotin @('fixed', 'player', 'modDefault')) {
                throw "Invalid mode in $relativePath`: $($setting.Name)"
            }
            $hasValue = $setting.Value.PSObject.Properties.Name -ccontains 'value'
            if (($mode -ceq 'fixed' -and (!$hasValue -or $null -eq $setting.Value.value)) -or
                ($mode -cne 'fixed' -and $hasValue)) {
                throw "Invalid value in $relativePath`: $($setting.Name)"
            }
        }
    }

    foreach ($relativePath in @('info.json', 'build.bat', 'verify.ps1') + $expected) {
        $path = Join-Path $package $relativePath
        $content = [IO.File]::ReadAllText($path)
        if ([regex]::IsMatch($content, '(?<!\r)\n')) {
            throw "File contains bare LF line endings: $relativePath"
        }
    }

    if ($CompareRoot) {
        $compare = [IO.Path]::GetFullPath($CompareRoot)
        $sha256 = [Security.Cryptography.SHA256]::Create()
        foreach ($relativePath in @('info.json') + $expected) {
            $left = [IO.File]::ReadAllBytes((Join-Path $package $relativePath))
            $right = [IO.File]::ReadAllBytes((Join-Path $compare $relativePath))
            $leftHash = [Convert]::ToBase64String($sha256.ComputeHash($left))
            $rightHash = [Convert]::ToBase64String($sha256.ComputeHash($right))
            if ($leftHash -cne $rightHash) {
                throw "Installed file differs: $relativePath"
            }
        }
        $sha256.Dispose()
        $installedFiles = @(Get-ChildItem -LiteralPath (Join-Path $compare 'Override') -File -Recurse)
        if ($installedFiles.Count -ne $expected.Count) {
            throw 'Installed Override directory has unexpected files.'
        }
    }

    Write-Host "SerpPresets verified: $($expected.Count) presets for 6 target mods."
    exit 0
}
catch {
    Write-Error $_
    exit 1
}
