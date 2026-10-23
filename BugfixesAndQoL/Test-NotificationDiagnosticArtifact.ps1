[CmdletBinding()]
param([Parameter(Mandatory)][string]$AssemblyPath, [switch]$DevDiag)
$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\core\Mono.Cecil.dll')))
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $AssemblyPath).Path)
try {
    $marked = @($assembly.CustomAttributes | Where-Object {
        $_.AttributeType.FullName -ceq 'System.Reflection.AssemblyMetadataAttribute' -and
        $_.ConstructorArguments[0].Value -ceq 'SerpDevDiagnostics' -and $_.ConstructorArguments[1].Value -ceq 'true'
    }).Count -eq 1
    if ($marked -ne [bool]$DevDiag) { throw 'Unexpected diagnostic assembly marker.' }
    $runtime = @($assembly.MainModule.Types | Where-Object { $_.FullName -ceq 'Shared.DevDiagRuntime' }).Count -eq 1
    if ($runtime -ne [bool]$DevDiag) { throw 'Unexpected diagnostic runtime presence.' }
    $feature = @($assembly.MainModule.Types | Where-Object { $_.FullName -ceq 'BugfixesAndQoL.NotificationLastFrameFeature' })[0]
    $speech = @($assembly.MainModule.Types | Where-Object { $_.FullName -ceq 'BugfixesAndQoL.NotificationSpeechPlayback' })[0]
    if (-not $feature -or -not $speech) { throw 'Notification production classes missing.' }
    if ($DevDiag) {
        foreach ($name in @('DiagBeforeRender','DiagMonitor','DiagPlayerEnd','DiagMediaEnd','ObserveSpeech','Summarize')) {
            if (@($feature.Methods | Where-Object { $_.Name -ceq $name }).Count -ne 1) { throw "Diagnostic method absent: $name" }
        }
        foreach ($name in @('DiagMonitor','DiagPlayerEnd','DiagMediaEnd')) {
            $method = @($feature.Methods | Where-Object { $_.Name -ceq $name })[0]
            $invokes = @($method.Body.Instructions | Where-Object { $_.OpCode.Name -ceq 'callvirt' -and $_.Operand.Name -ceq 'Invoke' })
            if ($invokes.Count -ne 1 -or @($method.Body.ExceptionHandlers | Where-Object { $_.HandlerType -eq 'Finally' }).Count -ne 1) {
                throw "Diagnostic wrapper must invoke its original exactly once with a finally: $name"
            }
        }
    } else {
        $ownedTypes = @($feature,$speech) + @($feature.NestedTypes) + @($speech.NestedTypes)
        foreach ($type in $ownedTypes) {
            if (@($type.Methods | Where-Object { $_.Name -match '^(Diag|ObserveSpeech|Summarize|Sample|AudioSnapshot)' }).Count -ne 0 -or
                @($type.Fields | Where-Object { $_.Name -like 'diag*' }).Count -ne 0 -or
                @($type.NestedTypes | Where-Object { $_.Name -in @('Observation','ObjectIdentity') }).Count -ne 0) {
                throw 'Normal assembly retained notification observer/hook/state.'
            }
            foreach ($method in $type.Methods | Where-Object { $_.HasBody }) {
                foreach ($instruction in $method.Body.Instructions) {
                    if ($instruction.Operand -is [Mono.Cecil.MethodReference] -and
                        ($instruction.Operand.Name -match '^Diag' -or $instruction.Operand.DeclaringType.FullName -ceq 'Shared.DevDiag')) {
                        throw 'Normal assembly retained a notification diagnostic call or its argument preparation.'
                    }
                    if ($instruction.OpCode.Name -ceq 'ldstr' -and $instruction.Operand -match '^(speech-invalidated|speech-stopped|speech-accepted|audio-load-pending|audio-load-return|audio-load-stale|audio-started|audio-load-failed|close-attempt|close-return|held|hud-end-enter)$') {
                        throw 'Normal assembly retained a notification diagnostic argument.'
                    }
                }
            }
        }
        if (@($assembly.MainModule.AssemblyReferences | Where-Object { $_.Name -ceq 'UnityEngine.VideoModule' }).Count -ne 0) {
            throw 'Normal assembly retained the diagnostic video dependency.'
        }
    }
    Write-Output "Notification artifact gate PASS: DevDiag=$([bool]$DevDiag), marker and compiled diagnostic boundary verified."
} finally { $assembly.Dispose() }
