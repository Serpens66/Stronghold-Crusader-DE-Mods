[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$NormalAssembly,
    [Parameter(Mandatory)][string]$DiagnosticAssembly,
    [string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [string]$NativeLibrary
)
$ErrorActionPreference = 'Stop'
# This is artifact verification: Cecil reads metadata/IL without executing Unity or BepInEx.
# Exact ABI expectations below are the version-1 interop contract, not source-layout assertions.
$cecilPath = Join-Path $GameDir 'BepInEx/core/Mono.Cecil.dll'
if (-not (Test-Path -LiteralPath $cecilPath -PathType Leaf)) { throw 'Mono.Cecil.dll is required from the selected game.' }
# Read the configured local tooling assembly into memory; do not edit its downloaded-file metadata.
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($cecilPath))
$NormalAssembly = [IO.Path]::GetFullPath($NormalAssembly)
$DiagnosticAssembly = [IO.Path]::GetFullPath($DiagnosticAssembly)
if (-not $NativeLibrary) { $NativeLibrary = Join-Path ([IO.Path]::GetDirectoryName($DiagnosticAssembly)) 'CrashDiagnostics.Native.dll' }
$NativeLibrary = [IO.Path]::GetFullPath($NativeLibrary)
$mod = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manifest = Get-Content -LiteralPath (Join-Path $mod 'info.json') -Raw | ConvertFrom-Json
$script:checks = 0
function Check([bool]$Passed, [string]$Description) {
    if (-not $Passed) { throw "Managed artifact check failed: $Description" }
    $script:checks++
    Write-Host "PASS $Description"
}
function All-Types($Module) {
    $queue = [Collections.Generic.Queue[object]]::new()
    foreach ($type in $Module.Types) { $queue.Enqueue($type) }
    while ($queue.Count) {
        $type = $queue.Dequeue()
        $type
        foreach ($nested in $type.NestedTypes) { $queue.Enqueue($nested) }
    }
}
function Marker($Assembly) {
    @($Assembly.CustomAttributes | Where-Object {
        $_.AttributeType.FullName -eq 'System.Reflection.AssemblyMetadataAttribute' -and
        $_.ConstructorArguments.Count -eq 2 -and $_.ConstructorArguments[0].Value -eq 'SerpDevDiagnostics' -and
        $_.ConstructorArguments[1].Value -eq 'true'
    }).Count -gt 0
}
function Reachable-Calls($Entry, $Module) {
    $pending = [Collections.Generic.Queue[object]]::new()
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $pending.Enqueue($Entry)
    while ($pending.Count) {
        $method = $pending.Dequeue()
        if (-not $seen.Add($method.FullName) -or -not $method.HasBody) { continue }
        foreach ($instruction in $method.Body.Instructions) {
            $target = $instruction.Operand
            if ($target -isnot [Mono.Cecil.MethodReference]) { continue }
            # Tokens in the same module can be followed without loading foreign assemblies.
            $target
            if ($target.DeclaringType.Scope -eq $Module) {
                $definition = $target.Resolve()
                if ($null -ne $definition) { $pending.Enqueue($definition) }
            }
        }
    }
}
function Pe-Exports([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 256 -or $bytes.Length -gt 16MB -or [BitConverter]::ToUInt16($bytes,0) -ne 0x5A4D) { throw 'Invalid native PE input.' }
    $pe = [BitConverter]::ToInt32($bytes,0x3c)
    if ($pe -lt 64 -or $pe -gt $bytes.Length-264 -or [BitConverter]::ToUInt32($bytes,$pe) -ne 0x4550) { throw 'Invalid native PE header.' }
    if ([BitConverter]::ToUInt16($bytes,$pe+4) -ne 0x8664 -or [BitConverter]::ToUInt16($bytes,$pe+24) -ne 0x20b) { throw 'The native ABI requires a PE32+ x64 DLL.' }
    $sections = [BitConverter]::ToUInt16($bytes,$pe+6)
    $table = $pe+24+[BitConverter]::ToUInt16($bytes,$pe+20)
    if ($sections -gt 96 -or $table -gt $bytes.Length-$sections*40) { throw 'Invalid native section table.' }
    $rvaToOffset = {
        param([uint32]$Rva, [uint32]$Size)
        for ($i=0; $i -lt $sections; $i++) {
            $at=$table+$i*40
            [uint64]$base=[BitConverter]::ToUInt32($bytes,$at+12)
            [uint64]$rawSize=[BitConverter]::ToUInt32($bytes,$at+16)
            [uint64]$raw=[BitConverter]::ToUInt32($bytes,$at+20)
            if ([uint64]$Rva -ge $base -and [uint64]$Rva-$base -le $rawSize -and [uint64]$Size -le $rawSize-([uint64]$Rva-$base)) {
                [uint64]$offset=$raw+[uint64]$Rva-$base
                if ($offset -gt [uint64]$bytes.Length -or [uint64]$Size -gt [uint64]$bytes.Length-$offset) { throw 'Native RVA outside file.' }
                return [int]$offset
            }
        }
        throw 'Unmapped native export RVA.'
    }
    $exportRva=[BitConverter]::ToUInt32($bytes,$pe+24+112)
    $exportAt=& $rvaToOffset $exportRva 40
    $count=[BitConverter]::ToUInt32($bytes,$exportAt+24)
    if ($count -gt 4096) { throw 'Native export count exceeds the verification limit.' }
    $nameTable=& $rvaToOffset ([BitConverter]::ToUInt32($bytes,$exportAt+32)) ($count*4)
    $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for ($i=0; $i -lt $count; $i++) {
        $at=& $rvaToOffset ([BitConverter]::ToUInt32($bytes,$nameTable+$i*4)) 1
        $end=$at
        while ($end -lt $bytes.Length -and $end-$at -lt 512 -and $bytes[$end] -ne 0) { $end++ }
        if ($end -ge $bytes.Length -or $end-$at -ge 512) { throw 'Unterminated native export name.' }
        [void]$names.Add([Text.Encoding]::ASCII.GetString($bytes,$at,$end-$at))
    }
    return ,$names
}
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
foreach ($directory in @([IO.Path]::GetDirectoryName($NormalAssembly), [IO.Path]::GetDirectoryName($DiagnosticAssembly),
    (Join-Path $GameDir 'BepInEx/core'), (Join-Path $GameDir 'BepInEx/plugins/000shcdese'),
    (Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data/Managed'))) { $resolver.AddSearchDirectory($directory) }
$parameters = [Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver = $resolver
$parameters.ReadSymbols = $false
$normal = $null; $diagnostic = $null
try {
    $normal = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($NormalAssembly,$parameters)
    $diagnostic = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($DiagnosticAssembly,$parameters)
    $normalTypes = @(All-Types $normal.MainModule)
    $diagTypes = @(All-Types $diagnostic.MainModule)
    Check (-not (Marker $normal)) 'normal assembly has no development-diagnostic marker'
    Check (Marker $diagnostic) 'diagnostic assembly carries the release-rejection marker'
    Check (@($normalTypes | Where-Object { $_.FullName -eq 'Shared.DevDiagRuntime' }).Count -eq 0) 'normal assembly excludes the diagnostic helper runtime'
    Check (@($diagTypes | Where-Object { $_.FullName -eq 'Shared.DevDiagRuntime' }).Count -eq 1) 'diagnostic assembly includes the gated helper runtime'
    Check (@($normalTypes | ForEach-Object { $_.Methods } | Where-Object { $_.IsPInvokeImpl }).Count -eq 0) 'normal assembly contains no native imports'
    Check (@($normal.MainModule.ModuleReferences | Where-Object { $_.Name -eq 'CrashDiagnostics.Native.dll' }).Count -eq 0) 'normal assembly does not link the capture library'
    foreach ($assembly in @($normal,$diagnostic)) {
        $types = @(All-Types $assembly.MainModule)
        $plugin = @($types | Where-Object { $_.BaseType -and $_.BaseType.FullName -eq 'BepInEx.BaseUnityPlugin' })
        Check ($plugin.Count -eq 1) 'artifact declares exactly one BepInEx plugin'
        $identity = @($plugin[0].CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInPlugin' })
        Check ($identity.Count -eq 1 -and $identity[0].ConstructorArguments[0].Value -eq $manifest.GUID -and
            $identity[0].ConstructorArguments[2].Value -eq $manifest.Version) 'compiled plugin identity/version matches its manifest'
        Check ($assembly.Name.Version.ToString(3) -eq ([version]$manifest.Version).ToString(3)) 'assembly version matches the plugin package'
        $dependencies = @($plugin[0].CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInDependency' })
        foreach ($required in $manifest.Dependencies) {
            $matching = @($dependencies | Where-Object { $_.ConstructorArguments.Count -eq 2 -and
                $_.ConstructorArguments[0].Value -eq $required.GUID -and $_.ConstructorArguments[1].Type.FullName -eq 'System.String' -and
                $_.ConstructorArguments[1].Value -eq $required.MinimumVersion })
            Check ($matching.Count -eq 1) ('compiled dependency matches manifest: '+$required.GUID)
        }
        $extender = @($dependencies | Where-Object { $_.ConstructorArguments[0].Value -eq '000shcdese' -and
            $_.ConstructorArguments[1].Value -eq $manifest.MinimumScriptExtenderVersion })
        Check ($extender.Count -eq 1) 'compiled Script Extender minimum matches manifest'
        Check (@($assembly.MainModule.AssemblyReferences | Where-Object {
            $_.Name -in @('Newtonsoft.Json','System.Text.Json','System.Web.Extensions','System.Runtime.Serialization','Assembly-CSharp-publicized','Assembly-CSharp')
        }).Count -eq 0) 'artifact has no prohibited serializer or publicized/game assembly dependency'
        Check (@($plugin[0].Methods | Where-Object { $_.Name -in @('Update','LateUpdate','FixedUpdate','OnDestroy','OnDisable') }).Count -eq 0) 'plugin has no component-driven runtime loop or startup teardown'
    }
    $normalPlugin = @($normalTypes | Where-Object { $_.BaseType -and $_.BaseType.FullName -eq 'BepInEx.BaseUnityPlugin' })[0]
    $awake = @($normalPlugin.Methods | Where-Object { $_.Name -eq 'Awake' })
    Check ($awake.Count -eq 1 -and $awake[0].HasBody) 'normal plugin has a verifiable initialization body'
    $entryPoints = @($awake[0]) + @($normalPlugin.Methods | Where-Object { $_.Name -in @('.ctor','.cctor') }) +
        @($normal.MainModule.Types | Where-Object { $_.Name -eq '<Module>' } | ForEach-Object { $_.Methods } | Where-Object { $_.Name -eq '.cctor' })
    $calls = @(foreach ($entryPoint in $entryPoints) { Reachable-Calls $entryPoint $normal.MainModule })
    $activeCalls = @($calls | Where-Object {
        $_.DeclaringType.Namespace -like 'APIShared*' -or $_.DeclaringType.Namespace -like 'SHCDESE*' -or
        $_.DeclaringType.FullName -eq 'UnityEngine.Application' -or $_.DeclaringType.FullName -eq 'System.Diagnostics.Process' -or
        $_.DeclaringType.FullName -eq 'System.Runtime.InteropServices.Marshal' -or
        $_.DeclaringType.FullName -in @('System.IO.File','System.IO.Directory') -or
        $_.Name -like 'CD_*' -or $_.Name -in @('StartCoroutine','InitializeDiagnostics')
    })
    Check ($activeCalls.Count -eq 0) 'normal initialization call graph performs no capture, subscription, process launch or file mutation'

    $nativeImports = @($diagTypes | ForEach-Object { $_.Methods } | Where-Object {
        $_.IsPInvokeImpl -and $_.PInvokeInfo.Module.Name -eq 'CrashDiagnostics.Native.dll'
    })
    Check ($nativeImports.Count -gt 0) 'diagnostic assembly imports the native capture ABI'
    $expected = @{
        CD_GetAbiVersion=@('System.UInt32'); CD_Initialize=@('System.UInt32','System.String','System.String','System.String','System.String','System.String')
        CD_RegisterImage=@('System.UInt32','System.UInt64','System.UInt64','System.String','System.String')
        CD_PublishContext=@('System.UInt32','System.String'); CD_RecordEvent=@('System.Void','System.UInt32','System.Int64','System.Int64','System.Int64')
        CD_MarkShutdown=@('System.Void'); CD_GetStatus=@('System.UInt32'); CD_Bootstrap=@('System.Void','System.UInt32')
    }
    $exports = Pe-Exports $NativeLibrary
    foreach ($name in $expected.Keys | Sort-Object) {
        $methods = @($nativeImports | Where-Object { $_.PInvokeInfo.EntryPoint -eq $name })
        Check ($methods.Count -eq 1 -and $methods[0].IsPInvokeImpl) ('native ABI import exists: '+$name)
        $method = $methods[0]; $signature = $expected[$name]
        Check ($method.PInvokeInfo.Module.Name -eq 'CrashDiagnostics.Native.dll' -and
            $method.PInvokeInfo.EntryPoint -eq $name -and $method.PInvokeInfo.IsCallConvStdCall) ('native entry point and calling convention: '+$name)
        Check ($method.ReturnType.FullName -eq $signature[0] -and $method.Parameters.Count -eq $signature.Count-1) ('native ABI return/count: '+$name)
        for ($i=0; $i -lt $method.Parameters.Count; $i++) {
            Check ($method.Parameters[$i].ParameterType.FullName -eq $signature[$i+1]) ('native ABI argument '+$name+'['+$i+']')
        }
        Check ($exports.Contains($name)) ('production native DLL exports '+$name)
    }
    $initialize = @($nativeImports | Where-Object { $_.PInvokeInfo.EntryPoint -eq 'CD_Initialize' })[0]
    Check ($initialize.PInvokeInfo.IsCharSetUnicode) 'initialization paths use the UTF-16 native ABI'
    foreach ($name in @('CD_RegisterImage','CD_PublishContext')) {
        $method = @($nativeImports | Where-Object { $_.PInvokeInfo.EntryPoint -eq $name })[0]
        foreach ($parameter in $method.Parameters | Where-Object { $_.ParameterType.FullName -eq 'System.String' }) {
            Check ($parameter.HasMarshalInfo -and [int]$parameter.MarshalInfo.NativeType -eq 48) ('native UTF-8 marshal contract: '+$name+'.'+$parameter.Name)
        }
    }
    Write-Host ('PASS managed artifact verification: '+$script:checks+' checks; Unity code was not executed.')
} finally {
    if ($null -ne $normal) { $normal.Dispose() }
    if ($null -ne $diagnostic) { $diagnostic.Dispose() }
    $resolver.Dispose()
}
