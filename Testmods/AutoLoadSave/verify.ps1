[CmdletBinding()]
param([Parameter(Mandatory)][string]$GameDir, [Parameter(Mandatory)][string]$AssemblyPath)
$ErrorActionPreference = 'Stop'
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'BepInEx/core/Mono.Cecil.dll')))
$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
foreach ($directory in @((Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data/Managed'), (Join-Path $GameDir 'BepInEx/core'), (Join-Path $GameDir 'BepInEx/plugins/APIShared_Serp'), (Join-Path $GameDir 'BepInEx/plugins/000shcdese'))) { $resolver.AddSearchDirectory($directory) }
$parameters = New-Object Mono.Cecil.ReaderParameters
$parameters.AssemblyResolver = $resolver
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($AssemblyPath, $parameters)
try {
    $checked = 0
    foreach ($member in $assembly.MainModule.GetMemberReferences()) {
        if ($member.DeclaringType.Scope.Name -ne 'Assembly-CSharp') { continue }
        $definition = $member.Resolve()
        if (-not $definition -or -not $definition.IsPublic) { throw "Nonpublic or unresolved game member: $member" }
        $type = $definition.DeclaringType
        while ($type) {
            if (-not $type.IsPublic -and -not $type.IsNestedPublic) { throw "Nonpublic declaring type: $type" }
            $type = $type.DeclaringType
        }
        $checked++
    }
    if ($checked -eq 0) { throw 'No game references were validated.' }
    Write-Host "PASS: $checked compiled game references resolved as public against the real installed assembly."
    $jsonReferences = @($assembly.MainModule.AssemblyReferences | Where-Object { $_.Name -match 'Newtonsoft|System.Text.Json|System.Web.Extensions|Serialization' })
    if ($jsonReferences.Count -gt 0) { throw 'Unexpected serializer dependency.' }
} finally { $assembly.Dispose(); $resolver.Dispose() }
