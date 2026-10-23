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
    foreach ($entry in @(@('speechSource1','UnityEngine.AudioSource'), @('speechClip1','UnityEngine.AudioClip'),
        @('speechMode1','System.Int32'), @('speechPaused','System.Boolean'), @('ignoreSpeechMuting','System.Boolean'))) {
        $fields = @($audio[0].Fields | Where-Object { $_.Name -ceq $entry[0] -and -not $_.IsStatic -and $_.FieldType.FullName -ceq $entry[1] })
        if ($fields.Count -ne 1) { throw "Installed MyAudioManager.$($entry[0]) contract changed." }
    }
    $loadSpeech = @($audio[0].Methods | Where-Object {
        $_.Name -ceq 'LoadClip' -and $_.IsPrivate -and $_.ReturnType.FullName -ceq 'System.Void' -and
        (($_.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -ceq 'System.Int32,System.String,System.String,System.Boolean,System.Boolean'
    })
    $loadPath = @($audio[0].Methods | Where-Object {
        $_.Name -ceq 'LoadClip' -and $_.IsPrivate -and $_.Parameters.Count -eq 1 -and
        $_.Parameters[0].ParameterType.FullName -ceq 'System.String' -and
        $_.ReturnType.FullName -ceq 'System.Threading.Tasks.Task`1<UnityEngine.AudioClip>'
    })
    $close = @($hud[0].Methods | Where-Object { $_.Name -ceq 'RadarME_Ended' -and $_.IsPublic -and $_.Parameters.Count -eq 0 -and $_.ReturnType.FullName -ceq 'System.Void' })
    if ($loadSpeech.Count -ne 1 -or $loadPath.Count -ne 1 -or $close.Count -ne 1) {
        throw 'Installed speech-load or public video-close signature changed.'
    }
    $radar = @($hud[0].Fields | Where-Object { $_.Name -ceq 'RefRadarME' -and $_.IsPublic })
    if ($radar.Count -ne 1) { throw 'MainHUD.RefRadarME is not public.' }
    $monitor = @($audio[0].Methods | Where-Object { $_.Name -ceq 'MonitorLoadedSounds' -and $_.IsPrivate -and $_.Parameters.Count -eq 0 -and $_.ReturnType.FullName -ceq 'System.Void' })
    if ($monitor.Count -ne 1) { throw 'Diagnostic audio monitor contract changed.' }
}
finally {
    $assembly.Dispose()
}

# The consumer resolves registered mod video assets before applying **; never call
# this internal SE member directly through a publicized compile-time reference.
$extenderDir = Join-Path $gameDir 'BepInEx\plugins\000shcdese'
if ($env:SHCDESE_EXTENDER_DIR) { $extenderDir = $env:SHCDESE_EXTENDER_DIR }
$extender = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $extenderDir 'SHCDESE.dll'))
try {
    $asset = @($extender.MainModule.Types | Where-Object { $_.FullName -ceq 'SHCDESE.API.GameAssetManagerAPI' })
    if ($asset.Count -ne 1 -or -not $asset[0].IsPublic) { throw 'Installed SE asset manager contract changed.' }
    $resolver = @($asset[0].Methods | Where-Object {
        $_.Name -ceq 'TryResolveVideoPath' -and -not $_.IsStatic -and $_.ReturnType.FullName -ceq 'System.Boolean' -and
        (($_.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -ceq 'System.String,System.String&' -and $_.Parameters[1].IsOut
    })
    $getter = @($asset[0].Methods | Where-Object { $_.Name -ceq 'get_Instance' -and $_.IsPublic -and $_.IsStatic -and
        $_.Parameters.Count -eq 0 -and $_.ReturnType.FullName -ceq $asset[0].FullName })
    if ($resolver.Count -ne 1 -or $getter.Count -ne 1) { throw 'Installed SE video resolver signature or public Instance visibility changed.' }
} finally { $extender.Dispose() }

# Every private diagnostic read is reflection-only. Public direct calls must remain
# public in the installed assemblies; publicized build references prove nothing.
foreach ($spec in @(
    @('Noesis.NoesisGUI.dll','NoesisApp.MediaElement','_player','NoesisApp.MediaPlayer','field'),
    @('Noesis.NoesisGUI.dll','NoesisMediaPlayer','_videoPlayer','UnityEngine.Video.VideoPlayer','field'),
    @('Noesis.NoesisGUI.dll','NoesisMediaPlayer','OnMediaEnded','System.Void','UnityEngine.Video.VideoPlayer'),
    @('Noesis.NoesisGUI.dll','NoesisApp.MediaElement','OnMediaEnded','System.Void',''),
    @('Noesis.NoesisGUI.dll','Noesis.BaseComponent','getCPtr','System.Runtime.InteropServices.HandleRef','Noesis.BaseComponent'),
    @('Noesis.NoesisGUI.dll','NoesisApp.MediaElement','get_Position','System.TimeSpan',''),
    @('Noesis.NoesisGUI.dll','NoesisApp.MediaElement','get_NaturalDuration','Noesis.Duration',''),
    @('UnityEngine.VideoModule.dll','UnityEngine.Video.VideoPlayer','get_frame','System.Int64',''),
    @('UnityEngine.VideoModule.dll','UnityEngine.Video.VideoPlayer','get_frameCount','System.UInt64',''),
    @('UnityEngine.VideoModule.dll','UnityEngine.Video.VideoPlayer','get_time','System.Double',''),
    @('UnityEngine.VideoModule.dll','UnityEngine.Video.VideoPlayer','get_length','System.Double',''),
    @('UnityEngine.VideoModule.dll','UnityEngine.Video.VideoPlayer','get_isPrepared','System.Boolean',''),
    @('UnityEngine.VideoModule.dll','UnityEngine.Video.VideoPlayer','get_isPlaying','System.Boolean',''),
    @('UnityEngine.VideoModule.dll','UnityEngine.Video.VideoPlayer','get_isPaused','System.Boolean',''),
    @('UnityEngine.AudioModule.dll','UnityEngine.AudioSource','get_isPlaying','System.Boolean',''),
    @('UnityEngine.AudioModule.dll','UnityEngine.AudioSource','get_volume','System.Single',''),
    @('UnityEngine.AudioModule.dll','UnityEngine.AudioClip','get_loadState','UnityEngine.AudioDataLoadState',''),
    @('UnityEngine.AudioModule.dll','UnityEngine.AudioClip','get_length','System.Single',''),
    @('UnityEngine.CoreModule.dll','UnityEngine.Component','get_gameObject','UnityEngine.GameObject',''),
    @('UnityEngine.CoreModule.dll','UnityEngine.GameObject','get_activeInHierarchy','System.Boolean',''),
    @('UnityEngine.CoreModule.dll','UnityEngine.Time','get_frameCount','System.Int32',''),
    @('UnityEngine.CoreModule.dll','UnityEngine.Application','add_onBeforeRender','System.Void','UnityEngine.Events.UnityAction')
)) {
    $check = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managedDir $spec[0]))
    try {
        $type = @($check.MainModule.Types | Where-Object { $_.FullName -ceq $spec[1] })
        if ($type.Count -ne 1 -or -not $type[0].IsPublic) { throw "Diagnostic type contract changed: $($spec[1])" }
        if ($spec[4] -ceq 'field') {
            $found = @($type[0].Fields | Where-Object { $_.Name -ceq $spec[2] -and $_.IsPrivate -and $_.FieldType.FullName -ceq $spec[3] })
        } else {
            $found = @($type[0].Methods | Where-Object { $_.Name -ceq $spec[2] -and $_.ReturnType.FullName -ceq $spec[3] -and
                (($_.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -ceq $spec[4] -and
                (($_.Name -ceq 'OnMediaEnded' -and $_.IsPrivate) -or ($_.Name -cne 'OnMediaEnded' -and $_.IsPublic)) })
        }
        if ($found.Count -ne 1) { throw "Diagnostic installed member contract changed: $($spec[1]).$($spec[2])" }
        if ($spec[2] -ceq 'getCPtr' -and -not $found[0].IsStatic) { throw 'Noesis getCPtr must be public static.' }
    } finally { $check.Dispose() }
}

$feature = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\NotificationLastFrameFeature.cs'))
if ($feature -match 'public\s+void\s+Dispose\s*\(' -or
    $feature -match '(?:videoEndedHook|playBinkHook)\s*\.\s*(?:Undo|Dispose)\s*\(' -or
    $feature -match '\b(?:Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') {
    throw 'Last-frame feature contains a published-hook teardown or MonoBehaviour callback.'
}
if (-not $feature.Contains('PresentationOperation.GuiChecks') -or
    -not $feature.Contains('originalVideoEnded(self, sender, args);') -or
    -not $feature.Contains('originalPlayBink(self, binkName, loop, waitForSpeech);') -or
    -not $feature.Contains('GameTimeManagerAPI.Instance.OnTick += OnGameTick;')) {
    throw 'Last-frame Vanilla fallback or persistent event path is incomplete.'
}

$speechFeature = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\NotificationSpeechPlayback.cs'))
# Source guardrails only: fixed-deadline/end/latch behavior is executed below against
# the actual production sources with a controllable monotone clock.
if ($feature -match 'NotificationSpeechPlayback|OST_Message_Bar_Vis|addOSTEntry|DateTime|binkWaitForSpeech\s*=' -or
    -not $speechFeature.Contains('Interlocked.Increment(ref speechChannel1Generation)') -or
    -not $speechFeature.Contains('!IsCurrent(audio, generation)')) {
    throw 'Last-frame audio isolation, text independence or skip stale-load cancellation is incomplete.'
}
& dotnet run --project (Join-Path $PSScriptRoot 'tests\NotificationPlayback.Tests\NotificationPlayback.Tests.csproj') --configuration Release --property:SerpDevDiagnostics=false
if ($LASTEXITCODE -ne 0) { throw 'Notification playback behavior tests failed.' }
$testAssembly = Join-Path $PSScriptRoot 'tests\NotificationPlayback.Tests\bin\Release\net10.0\NotificationPlayback.Tests.dll'
$normal = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($testAssembly)
try {
    $types = @($normal.MainModule.Types | Where-Object { $_.FullName -in @('BugfixesAndQoL.NotificationLastFrameFeature','BugfixesAndQoL.NotificationSpeechPlayback') })
    foreach ($type in $types) {
        if (@($type.Methods | Where-Object { $_.Name -match '^(Diag|ObserveSpeech)' }).Count -ne 0 -or
            @($type.Fields | Where-Object { $_.Name -like 'diag*' }).Count -ne 0) { throw 'Normal notification artifact retained diagnostic work.' }
    }
} finally { $normal.Dispose() }
& dotnet run --project (Join-Path $PSScriptRoot 'tests\NotificationPlayback.Tests\NotificationPlayback.Tests.csproj') --configuration Release --property:SerpDevDiagnostics=true
if ($LASTEXITCODE -ne 0) { throw 'Notification diagnostic timeline tests failed.' }
Write-Output 'Notification last-frame assembly, Noesis, lifecycle, and Vanilla-fallback preflight succeeded.'
