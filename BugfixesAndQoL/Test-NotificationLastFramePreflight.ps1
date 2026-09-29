[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$gameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$managedDir = Join-Path $gameDir 'Stronghold Crusader Definitive Edition_Data\Managed'
$gameAssemblyPath = Join-Path $managedDir 'Assembly-CSharp.dll'
$noesisPath = Join-Path $managedDir 'Noesis.NoesisGUI.dll'
$cecilPath = Join-Path $gameDir 'BepInEx\core\Mono.Cecil.dll'
$expectedNoesisHash = '98476D3CA84AE0F2DCFBADDCC64B01A1F65474BD44402673FD6856D1B5347648'
$sha256 = [System.Security.Cryptography.SHA256]::Create()
try {
    $noesisStream = [IO.File]::OpenRead($noesisPath)
    try {
        $actualNoesisHash = [BitConverter]::ToString($sha256.ComputeHash($noesisStream)).Replace('-', '')
    }
    finally {
        $noesisStream.Dispose()
    }
}
finally {
    $sha256.Dispose()
}
if ($actualNoesisHash -cne $expectedNoesisHash) {
    throw 'Installed Noesis player differs from the audited ** URI and MediaEnded contract.'
}

[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($cecilPath))
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($gameAssemblyPath)
try {
    $sfx = @($assembly.MainModule.Types | Where-Object { $_.FullName -ceq 'SFXManager' })
    $hud = @($assembly.MainModule.Types | Where-Object { $_.FullName -ceq 'CrusaderDE.MainHUD' })
    $audio = @($assembly.MainModule.Types | Where-Object { $_.FullName -ceq 'MyAudioManager' })
    if ($sfx.Count -ne 1 -or $hud.Count -ne 1 -or $audio.Count -ne 1) {
        throw 'Installed game assembly does not contain the audited minimap video types.'
    }
    $play = @($sfx[0].Methods | Where-Object {
        $_.Name -ceq 'playBink' -and $_.IsPublic -and $_.ReturnType.FullName -ceq 'System.Void' -and
        $_.Parameters.Count -eq 3 -and $_.Parameters[0].ParameterType.FullName -ceq 'System.String' -and
        $_.Parameters[1].ParameterType.FullName -ceq 'System.Boolean' -and
        $_.Parameters[2].ParameterType.FullName -ceq 'System.Boolean'
    })
    $ended = @($hud[0].Methods | Where-Object {
        $_.Name -ceq 'RadarME_Ended' -and $_.IsPrivate -and $_.ReturnType.FullName -ceq 'System.Void' -and
        $_.Parameters.Count -eq 2 -and $_.Parameters[0].ParameterType.FullName -ceq 'System.Object' -and
        $_.Parameters[1].ParameterType.FullName -ceq 'Noesis.RoutedEventArgs'
    })
    $speech = @($audio[0].Methods | Where-Object {
        $_.Name -ceq 'isSpeechPlaying' -and $_.IsPublic -and $_.Parameters.Count -eq 1 -and
        $_.Parameters[0].ParameterType.FullName -ceq 'System.Int32' -and
        $_.ReturnType.FullName -ceq 'System.Boolean'
    })
    if ($play.Count -ne 1 -or $ended.Count -ne 1 -or $speech.Count -ne 1) {
        throw 'Installed game assembly method signature or visibility changed.'
    }
    foreach ($member in @('requestBinkPlaybackURI', 'requestBinkPlayState', 'binkIsPlaying', 'binkWaitForSpeech')) {
        $fields = @($sfx[0].Fields | Where-Object { $_.Name -ceq $member -and $_.IsPublic })
        if ($fields.Count -ne 1) { throw "SFXManager.$member is not public in the installed assembly." }
    }
    foreach ($member in @('instance')) {
        $fields = @($sfx[0].Fields | Where-Object { $_.Name -ceq $member -and $_.IsPublic -and $_.IsStatic })
        if ($fields.Count -ne 1) { throw "SFXManager.$member is not public static." }
    }
    $radar = @($hud[0].Fields | Where-Object { $_.Name -ceq 'RefRadarME' -and $_.IsPublic })
    if ($radar.Count -ne 1) { throw 'MainHUD.RefRadarME is not public.' }
}
finally {
    $assembly.Dispose()
}

$feature = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\NotificationLastFrameFeature.cs'))
if ($feature -match 'public\s+void\s+Dispose\s*\(' -or
    $feature -match '(?:videoEndedHook|playBinkHook)\s*\.\s*(?:Undo|Dispose)\s*\(' -or
    $feature -match '\b(?:Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') {
    throw 'Last-frame feature contains a published-hook teardown or MonoBehaviour callback.'
}
if (-not $feature.Contains('if (feature == null || !feature.Enabled)') -or
    -not $feature.Contains('originalVideoEnded(self, sender, args);') -or
    -not $feature.Contains('originalPlayBink(self, binkName, loop, waitForSpeech);') -or
    -not $feature.Contains('GameTimeManagerAPI.Instance.OnTick += OnGameTick;')) {
    throw 'Last-frame Vanilla fallback or persistent event path is incomplete.'
}

Write-Output 'Notification last-frame assembly, Noesis, lifecycle, and Vanilla-fallback preflight succeeded.'
