[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AssemblyPath,
    [Parameter(Mandatory)][string]$GameDir,
    [Parameter(Mandatory)][string]$ExtenderDir,
    [Parameter(Mandatory)][string]$ApiSharedDir
)
$ErrorActionPreference = 'Stop'
$probeDirectories = @((Split-Path -Parent $AssemblyPath), $ExtenderDir, $ApiSharedDir,
    (Join-Path $GameDir 'BepInEx/core'),
    (Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data/Managed'))
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = ([Reflection.AssemblyName]::new($eventArgs.Name)).Name + '.dll'
    foreach ($directory in $probeDirectories) {
        $candidate = Join-Path $directory $name
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return [Reflection.Assembly]::LoadFrom($candidate) }
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    $assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
    # Inspect emitted defaults without constructing a Noesis view model outside the game.
    [void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'BepInEx/core/Mono.Cecil.dll')))
    $metadata = [Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($AssemblyPath))
    try {
        $viewModel = $metadata.MainModule.Types | Where-Object FullName -eq 'ExtraFeatures.ExtraFeaturesViewModel'
        $constructor = $viewModel.Methods | Where-Object { $_.IsConstructor -and -not $_.IsStatic } | Select-Object -First 1
        foreach ($category in @('Walls','Gates','Towers','Keeps')) {
            $property = $viewModel.Properties | Where-Object Name -eq ('PlagueDamage' + $category)
            if (-not $property -or $property.PropertyType.FullName -ne 'System.Boolean' -or
                -not ($property.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'SyncHostOnlyAttribute' })) {
                throw "Missing host plague setting: $category"
            }
            $fieldRead = $property.GetMethod.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldfld' } | Select-Object -First 1
            if (-not $fieldRead) { throw "Plague setting getter no longer exposes its backing state: $category" }
            $writes = @($constructor.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq $fieldRead.Operand.Name })
            $expected = $category -ne 'Keeps'
            if ($expected -and ($writes.Count -ne 1 -or $writes[0].Previous.OpCode.Name -ne 'ldc.i4.1')) { throw "Wrong enabled plague default: $category" }
            if (-not $expected -and ($writes | Where-Object { $_.Previous.OpCode.Name -ne 'ldc.i4.0' })) { throw "Wrong disabled plague default: $category" }
        }
        Write-Output 'PASS: compiled plague host classifications and actual constructor defaults (Walls/Gates/Towers on, Keeps off).'
    } finally { $metadata.Dispose() }
    $extender = [Reflection.Assembly]::LoadFrom((Join-Path $ExtenderDir 'SHCDESE.dll'))
    $enum = $extender.GetType('SHCDESE.Interop.eChimps', $true)
    $type = $assembly.GetType('ExtraFeatures.HealerTargetsRuntime', $true)
    # Private names locate actual compiled production code, rather than prescribing
    # source text or switch layout. A rename changes this adapter, not the policy.
    $flags = [Reflection.BindingFlags]'NonPublic,Static'
    $civilian = $type.GetMethod('IsHumanCivilian', $flags)
    $siege = $type.GetMethod('IsSiegeEngine', $flags)
    if (-not $civilian -or -not $siege) { throw 'Classification test adapter needs updating.' }
    # Reviewed feature policy: other and newly added enum values remain excluded.
    # No dependency on enum declaration order, total count or unrelated members.
    $civilians = @('PEASANT','WOODCUTTER','FLETCHER','HUNTER','QUARRY_MASON','QUARRY_GRUNT',
        'PITCHMAN','FARMER_WHEAT','FARMER_HOPS','FARMER_APPLE','FARMER_CATTLE','MILLER',
        'BAKER','BREWER','POLETURNER','BLACKSMITH','ARMOURER','TANNER','PRIEST','HEALER',
        'DRUNKARD','INNKEEPER','TRADER','FIREMAN','LADY','JESTER','MOTHER','CHILD','JUGGLER','FIREEATER') |
        ForEach-Object { 'CHIMP_TYPE_' + $_ }
    $engines = @('CATAPULT','TREBUCHET','MANGONEL','SIEGE_TOWER','BATTERING_RAM',
        'PORTABLE_SHIELD','BALLISTA','ARAB_BALLISTA') | ForEach-Object { 'CHIMP_TYPE_' + $_ }
    foreach ($name in @($civilians) + @($engines)) {
        if (-not [Enum]::IsDefined($enum, $name)) { throw "Required classification type is missing: $name" }
    }
    $count = 0
    foreach ($value in [Enum]::GetValues($enum)) {
        $name = [Enum]::GetName($enum, $value)
        if ([bool]$civilian.Invoke($null, @($value)) -ne ($civilians -contains $name) -or
            [bool]$siege.Invoke($null, @($value)) -ne ($engines -contains $name)) {
            throw "Healer target policy mismatch: $name"
        }
        $count++
    }
    $viewModel = $assembly.GetType('ExtraFeatures.ExtraFeaturesViewModel', $true)
    foreach ($name in @('KeepBuildRange','HealCivilianTargets','HealSiegeTargets','EnableBuildingRepair')) {
        $property = $viewModel.GetProperty($name)
        if (-not $property -or -not @($property.GetCustomAttributesData() | Where-Object { $_.AttributeType.Name -eq 'SyncHostOnlyAttribute' }).Count) {
            throw "Missing compiled host authority declaration: $name"
        }
    }
    $references = @($assembly.GetReferencedAssemblies() | Where-Object { $_.Name -eq 'APIShared' })
    $selected = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $ApiSharedDir 'APIShared.dll'))
    if ($references.Count -ne 1 -or $references[0].Version -ne $selected.Version) {
        throw 'Compiled APIShared reference differs from the selected SDK.'
    }
    Write-Host "PASS: compiled production healer classification ($count enum values), host authority and selected SDK reference. No game or Unity callbacks executed."
} finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
}
