$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'patcher\HookRuntime.cs'))
foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src'),(Join-Path $PSScriptRoot 'patcher') -Filter '*.cs' -Recurse) + @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.csproj') + @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs')) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match 'System\.Text\.Json|Newtonsoft|JavaScriptSerializer|System\.Web\.Extensions|DataContractJsonSerializer|JsonUtility|Assembly-CSharp|APIShared\.dll') { throw "Forbidden dependency: $($file.Name)" }
    if ($text -match '\b(OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(|\.(Undo|Dispose|Apply)\s*\(|-=') { throw "Unexpected lifecycle mutation: $($file.Name)" }
    if ($text -match '(?<!\r)\n') { throw "Non-CRLF: $($file.Name)" }
}
function Hash([string]$path) {
    $stream = [IO.File]::OpenRead($path); $sha = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') } finally { $sha.Dispose(); $stream.Dispose() }
}
foreach ($entry in @(@('CoreHash','BepInEx\core\BepInEx.dll'),@('ExtenderHash','BepInEx\plugins\000shcdese\SHCDESE.dll'))) {
    $expected = [regex]::Match($source,($entry[0] + ' = "([A-F0-9]{64})"')).Groups[1].Value
    if ((Hash (Join-Path $game $entry[1])) -cne $expected) { throw "Binary changed; audit required: $($entry[1])" }
}
[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\core\Mono.Cecil.dll'))) | Out-Null
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game 'BepInEx\core\BepInEx.dll'))
try {
    $type = $assembly.MainModule.Types | Where-Object FullName -eq 'BepInEx.Bootstrap.Chainloader'
    $method = $type.Methods | Where-Object Name -eq 'Start'
    $sort = @($method.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'TopologicalSort' })
    $add = @($method.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'UnityEngine.GameObject' -and $_.Operand.Name -eq 'AddComponent' })
    if ($sort.Count -ne 1 -or $add.Count -ne 1 -or $sort[0].Next.Operand.Name -ne 'ToList' -or $add[0].Previous.Operand.Name -ne 'GetType') { throw 'Insertion pattern mismatch' }
    $closure = @($type.NestedTypes | Where-Object { @($_.Fields | Where-Object Name -eq 'pluginsByGUID').Count -eq 1 })
    if ($closure.Count -ne 1) { throw 'Winner dictionary is ambiguous' }
    # Existing branches/exception boundaries must not skip the newly inserted probes.
    foreach ($target in @($add[0], $sort[0].Next.Next)) {
        foreach ($instruction in $method.Body.Instructions) {
            if ($instruction.Operand -is [Mono.Cecil.Cil.Instruction] -and $instruction.Operand -eq $target) { throw 'Unexpected branch to injection boundary' }
            if ($instruction.Operand -is [Mono.Cecil.Cil.Instruction[]] -and $instruction.Operand -contains $target) { throw 'Unexpected switch to injection boundary' }
        }
        foreach ($handler in $method.Body.ExceptionHandlers) {
            if (@($handler.TryStart,$handler.TryEnd,$handler.HandlerStart,$handler.HandlerEnd,$handler.FilterStart) -contains $target) { throw 'Unexpected exception boundary' }
        }
    }
} finally { $assembly.Dispose() }
Write-Host 'Probe preflight passed: exact binaries, insertion sites/control-flow boundaries, references, lifecycle, CRLF.'