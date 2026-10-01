[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$hook = [IO.File]::ReadAllText((Join-Path $root 'src\WorkshopIdleDelayHook.cs'))
$native = [IO.File]::ReadAllText((Join-Path $root 'src\WorkshopIdleDelayNative.cs'))
$runtime = [IO.File]::ReadAllText((Join-Path $root 'src\BugfixesAndQoLRuntime.cs'))
$settings = [IO.File]::ReadAllText((Join-Path $root 'src\BugfixesAndQoLViewModel.cs'))
foreach ($required in @('private readonly HookTransaction transaction;', 'Volatile.Write(ref *(int*)enabledFlag',
    'Probe(imageBase + WorkshopIdleDelayNative.PoleRva)', 'Probe(imageBase + WorkshopIdleDelayNative.TannerRva)',
    'ValidateLayouts();', 'ValidateGenerator(imageBase, false)', 'ValidateGenerator(imageBase, true)',
    'WorkshopIdleDelayNative.ValidateInstructions(instructions', 'pole.Hook.DisplacedByteCount', 'tanner.Hook.DisplacedByteCount',
    'ResolveUnique(memory, signature, rva, true', 'candidate?.Dispose();')) {
    if (-not $hook.Contains($required)) { throw "Missing workshop hook contract: $required" }
}
if ($hook -match '\b(?:OnDestroy|OnDisable|OnApplicationQuit|StartCoroutine|Update|LateUpdate|FixedUpdate)\s*\(' -or
    $hook -match '(?:transaction|\.Hook)\??\.(?:Dispose|Undo|Disable|Enable)\s*\(' -or
    $hook -match 'AddContextHook|BeforeCallback|Marshal\.Write|VirtualProtect|CodePatch') {
    throw 'Workshop hook has a callback-wrapper, executable toggle or published teardown.'
}
if (-not $runtime.Contains('private static WorkshopIdleDelayHook processWorkshopIdleDelayHook;') -or
    -not $runtime.Contains('settings.EnableMod && settings.EnableWorkshopIdleDelayFix') -or
    -not $runtime.Contains('GameTimeManagerAPI.Instance.OnTick += OnWorkshopIdleTick')) {
    throw 'Workshop runtime rooting/activation/startup marker contract incomplete.'
}
if ($settings -notmatch '\[SyncHostOnly\]\s+public bool EnableWorkshopIdleDelayFix' -or
    -not $settings.Contains('private bool enableWorkshopIdleDelayFix;') -or
    -not $settings.Contains('EnableWorkshopIdleDelayFix = false;')) {
    throw 'Workshop setting must be host-synchronized and default/reset false.'
}
if ($native -match '\b(?:call|AddContextHook)\s*\(' -or
    $native -notmatch 'a\.cmp\(__word_ptr\[r10 \+ 0x956\], 1\); a\.je\(vanilla\)' -or
    -not $native.Contains('a.cmp(eax, -10000); a.jle(next); a.cmp(eax, 10000);') -or
    -not $native.Contains('a.AddInstruction(replay[0]); a.AddInstruction(replay[2]);') -or
    -not $native.Contains('foreach (Instruction instruction in replay)')) {
    throw 'Workshop native pause/readiness/Vanilla-replay contract incomplete.'
}
foreach ($locale in Get-ChildItem -LiteralPath (Join-Path $root 'Locales') -Filter '*.txt') {
    $text = [IO.File]::ReadAllText($locale.FullName)
    foreach ($key in @('EnableWorkshopIdleDelayFix', 'EnableWorkshopIdleDelayFixHelp')) {
        if ([regex]::Matches($text, '(?m)^BugfixesAndQoL\.' + $key + '=.+$').Count -ne 1) {
            throw "Missing/duplicate workshop locale key: $($locale.Name) $key"
        }
    }
}
$newFiles = @('src\WorkshopIdleDelayHook.cs', 'src\WorkshopIdleDelayNative.cs',
    'tests\WorkshopIdleDelay.Tests\Program.cs', 'tests\WorkshopIdleDelay.Tests\WorkshopIdleDelay.Tests.csproj',
    'Test-WorkshopIdleDelayPreflight.ps1')
foreach ($relative in $newFiles) {
    $text = [IO.File]::ReadAllText((Join-Path $root $relative))
    if ($text -match '(?<!\r)\n') { throw "Bare LF in workshop file: $relative" }
}
Write-Output 'PASS: workshop idle permanent native hook, synchronized default-off setting and locale preflight.'
