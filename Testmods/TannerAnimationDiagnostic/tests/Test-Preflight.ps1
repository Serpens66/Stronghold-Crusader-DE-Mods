param([Parameter(Mandatory = $true)][string]$ProjectDir)

$ErrorActionPreference = 'Stop'
$checkedFiles = @(Get-ChildItem -LiteralPath $ProjectDir -Recurse -File |
    Where-Object { $_.Extension -in @('.cs', '.csproj', '.json', '.ps1', '.bat') -and
        $_.FullName -notmatch '[\\/](?:bin|obj|BepInEx)[\\/]' })
$forbidden = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|OnDestroy|OnDisable|OnApplicationQuit|StartCoroutine|\b(?:void|IEnumerator)\s+(?:Update|LateUpdate|FixedUpdate)\s*\('
$hookMutations = '\.Undo\s*\(|\.Dispose\s*\(|\.Apply\s*\(|\.Disable\s*\(|\.Enable\s*\(|CodePatch\.Write|Marshal\.Write|VirtualProtect'
foreach ($file in $checkedFiles) {
    $content = [IO.File]::ReadAllText($file.FullName)
    if ($file.Extension -in @('.cs', '.csproj') -and [regex]::IsMatch($content, $forbidden)) {
        throw "Forbidden JSON or Unity lifecycle pattern: $($file.FullName)"
    }
    $literalEscape = [string][char]92 + 'r' + [string][char]92 + 'n'
    if ($content.Contains($literalEscape)) { throw "Literal backslash-r/backslash-n sequence: $($file.FullName)" }
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    for ($index = 0; $index -lt $bytes.Length; $index++) {
        if ($bytes[$index] -eq 10 -and ($index -eq 0 -or $bytes[$index - 1] -ne 13)) {
            throw "Bare LF: $($file.FullName)"
        }
    }
}
$runtime = [IO.File]::ReadAllText((Join-Path $ProjectDir 'src\TannerAnimationDiagnosticRuntime.cs'))
$mutations = [regex]::Matches($runtime, $hookMutations)
if ($mutations.Count -ne 5) { throw "Unexpected hook mutation count in initialization rollback: $($mutations.Count)" }
if (-not $runtime.Contains('catch') -or -not $runtime.Contains('Only an initialization candidate')) {
    throw 'Hook rollback must remain confined to unpublished initialization candidates.'
}
if (([regex]::Matches($runtime, '\.Undo\s*\(')).Count -ne 2 -or
    ([regex]::Matches($runtime, '\.Dispose\s*\(')).Count -ne 3) {
    throw 'Unexpected hook or subscription release path.'
}
$native = [IO.File]::ReadAllText((Join-Path $ProjectDir 'src\NativeTannerFade.cs'))
if (([regex]::Matches($native, '\.Dispose\s*\(')).Count -ne 2 -or
    -not $native.Contains('Only this unpublished initialization candidate may be rolled back.') -or
    [regex]::IsMatch($native, '\.Undo\s*\(|\.Disable\s*\(|\.Apply\s*\(')) {
    throw 'Native detour rollback must remain confined to an unpublished candidate.'
}
if (-not $native.Contains('scheme != "Indirect"') -or
    -not $native.Contains('detour.DisplacedByteCount != 6') -or
    -not $native.Contains('patch[0] != 0xFF || patch[1] != 0x25') -or
    -not $native.Contains('GameTimeManagerAPI.Instance.OnTick += OnGameTick') -or
    ([regex]::Matches($native, 'GameTimeManagerAPI\.Instance\.OnTick\s*-=' )).Count -ne 1) {
    throw 'The native detour contract must fail closed to the audited indirect jump.'
}
$xamlFiles = @(Get-ChildItem -LiteralPath $ProjectDir -Filter '*.xaml' -Recurse -File)
if ($xamlFiles.Count -ne 0) { throw 'This diagnostic must not include XAML patches.' }
Write-Host 'TannerAnimationDiagnostic preflight passed: JSON/lifecycle/hook patterns and CRLF.'
