function Get-PluginDependencyDeclarations([string]$SourceText) {
    $constants = @{}
    foreach ($constant in [regex]::Matches($SourceText, '\bconst\s+string\s+(\w+)\s*=\s*"([^"\r\n]+)"')) {
        $constants[$constant.Groups[1].Value] = $constant.Groups[2].Value
    }
    foreach ($attribute in [regex]::Matches($SourceText, '\[BepInDependency\(\s*("[^"\r\n]+"|\w+)\s*(?:,\s*("[^"\r\n]+"|[^)]+))?\)\]')) {
        $guidToken = $attribute.Groups[1].Value
        $guid = if ($guidToken.StartsWith('"')) { $guidToken.Trim('"') } else { $constants[$guidToken] }
        if (-not $guid) { throw "Unresolved dependency GUID: $guidToken" }
        $argument = $attribute.Groups[2].Value.Trim()
        if ($argument -match '\bSoftDependency\b') { continue }
        $minimum = if ($argument.StartsWith('"')) { $argument.Trim('"') } elseif ($constants.ContainsKey($argument)) { $constants[$argument] } else { '' }
        [pscustomobject]@{ GUID = $guid; MinimumVersion = $minimum }
    }
}

function Assert-PluginDependencyMetadata([string]$SourceText, [object]$Manifest, [string]$Name) {
    $declared = @(Get-PluginDependencyDeclarations $SourceText)
    $metadata = @($Manifest.Dependencies | Where-Object { $null -ne $_ })
    $seen = @{}
    foreach ($dependency in $declared) {
        if ($seen.ContainsKey($dependency.GUID)) { throw "$Name declares duplicate BepInEx dependency $($dependency.GUID)." }
        $seen[$dependency.GUID] = $true
        if ($dependency.GUID -eq '000shcdese') {
            $minimum = [string]$Manifest.MinimumScriptExtenderVersion
        } else {
            $matches = @($metadata | Where-Object { [string]$_.GUID -ceq $dependency.GUID })
            if ($matches.Count -ne 1) { throw "$Name must declare dependency $($dependency.GUID) exactly once in info.json." }
            $minimum = [string]$matches[0].MinimumVersion
            $maximum = [string]$matches[0].MaximumVersion
            if ($maximum) {
                $parsedMaximum = $null
                $parsedMinimum = $null
                if (-not [version]::TryParse($maximum, [ref]$parsedMaximum) -or
                    ($minimum -and (-not [version]::TryParse($minimum, [ref]$parsedMinimum) -or $parsedMaximum -lt $parsedMinimum))) {
                    throw "$Name has an invalid dependency range for $($dependency.GUID)."
                }
            }
        }
        # These are minimum bounds, not equality checks against the installed version.
        if ($minimum -cne $dependency.MinimumVersion) {
            throw "$Name minimum for $($dependency.GUID) differs between info.json and BepInEx."
        }
        if ($minimum) {
            $parsed = $null
            if (-not [version]::TryParse($minimum, [ref]$parsed)) { throw "$Name has an invalid minimum: $minimum" }
        }
    }
    foreach ($dependency in $metadata) {
        if (-not $dependency.GUID -or -not $seen.ContainsKey([string]$dependency.GUID)) {
            throw "$Name info.json dependency lacks a corresponding hard BepInEx declaration: $($dependency.GUID)"
        }
    }
}
