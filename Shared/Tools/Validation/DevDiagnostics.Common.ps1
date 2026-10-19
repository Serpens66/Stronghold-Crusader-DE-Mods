# DEVDIAG release protection (see AGENTS/build-release.md, "Entwicklungsdiagnose").
# Source inventory: every remaining development measurement point in a mod and the shared
# runtime sources it can link. Binary gate: an assembly built with SERP_DEVDIAG carries
# AssemblyMetadata("SerpDevDiagnostics","true") and the type Shared.DevDiagRuntime.

$script:DevDiagHelperRelative = 'Shared\Runtime\Diagnostics\DevDiag.cs'

function Get-DevDiagnosticSources {
    param([Parameter(Mandatory)][string]$Root, [string]$ModDirectory)
    $Root = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $scopes = @()
    if ($ModDirectory) { $scopes += Join-Path $Root $ModDirectory }
    $scopes += @((Join-Path $Root 'Shared\Runtime'), (Join-Path $Root 'Shared\Adapters'))
    $helper = [IO.Path]::GetFullPath((Join-Path $Root $script:DevDiagHelperRelative))
    $results = New-Object System.Collections.Generic.List[object]
    foreach ($scope in $scopes) {
        if (-not (Test-Path -LiteralPath $scope -PathType Container)) { continue }
        foreach ($file in [IO.Directory]::EnumerateFiles($scope, '*.cs', [IO.SearchOption]::AllDirectories)) {
            $full = [IO.Path]::GetFullPath($file)
            if ($full -ieq $helper -or $full -match '[\\/](bin|obj)[\\/]') { continue }
            $relative = $full.Substring($Root.Length).TrimStart('\','/')
            if ($full -like '*.DevDiag.cs') { $results.Add([pscustomobject]@{ Path=$relative; Line=0; Text='development-only file' }); continue }
            $lines = [IO.File]::ReadAllLines($full)
            for ($index = 0; $index -lt $lines.Length; $index++) {
                # Partial Diag* hooks become no-ops without their *.DevDiag.cs part; list them for removal too.
                if ($lines[$index] -match '\bDevDiag\s*\.|\bSERP_DEVDIAG\b|\bpartial\s+void\s+Diag[A-Z]') {
                    $results.Add([pscustomobject]@{ Path=$relative; Line=$index + 1; Text=$lines[$index].Trim() })
                }
            }
        }
    }
    return $results.ToArray()
}

function Assert-NoDevDiagnosticSources {
    param([Parameter(Mandatory)][string]$Root, [string]$ModDirectory)
    $found = @(Get-DevDiagnosticSources -Root $Root -ModDirectory $ModDirectory)
    if ($found.Count -eq 0) { return }
    $sample = ($found | Select-Object -First 10 | ForEach-Object { '  ' + $_.Path + ':' + $_.Line + ' ' + $_.Text }) -join "`r`n"
    throw ("DEVDIAG measurement points must be removed before a main/release build ($($found.Count) found):`r`n" + $sample)
}

# Throws when any assembly below $Directory was compiled with SERP_DEVDIAG.
function Assert-NoDevDiagnosticAssemblies {
    param([Parameter(Mandatory)][string]$Directory, [string]$CecilPath)
    if (-not ('Mono.Cecil.AssemblyDefinition' -as [type])) {
        if (-not $CecilPath) { throw 'DEVDIAG gate: Mono.Cecil path missing.' }
        [void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($CecilPath))
    }
    $violations = New-Object System.Collections.Generic.List[string]
    foreach ($dll in @(Get-ChildItem -LiteralPath $Directory -Recurse -File -Filter '*.dll')) {
        $stream = $null; $assembly = $null
        try {
            $stream = [IO.MemoryStream]::new([IO.File]::ReadAllBytes($dll.FullName))
            try { $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($stream) }
            catch [BadImageFormatException] { continue }
            foreach ($attribute in $assembly.CustomAttributes) {
                if ($attribute.AttributeType.FullName -ceq 'System.Reflection.AssemblyMetadataAttribute' -and
                    $attribute.ConstructorArguments.Count -ge 1 -and [string]$attribute.ConstructorArguments[0].Value -ceq 'SerpDevDiagnostics') {
                    $violations.Add($dll.FullName + ': SerpDevDiagnostics marker')
                }
            }
            foreach ($module in $assembly.Modules) {
                foreach ($type in $module.GetTypes()) {
                    if ($type.Name -ceq 'DevDiagRuntime') { $violations.Add($dll.FullName + ': type ' + $type.FullName) }
                    foreach ($method in $type.Methods) {
                        if (-not $method.HasBody) { continue }
                        foreach ($instruction in $method.Body.Instructions) {
                            $operand = $instruction.Operand
                            if ($operand -is [Mono.Cecil.MethodReference] -and $operand.DeclaringType.Name -ceq 'DevDiag') {
                                $violations.Add($dll.FullName + ': call from ' + $type.FullName + '::' + $method.Name)
                                break
                            }
                        }
                    }
                }
            }
        } finally {
            if ($assembly) { $assembly.Dispose() }
            if ($stream) { $stream.Dispose() }
        }
    }
    if ($violations.Count) {
        throw ("DEVDIAG assemblies must never be released or packed:`r`n  " + (($violations | Select-Object -Unique) -join "`r`n  "))
    }
}
