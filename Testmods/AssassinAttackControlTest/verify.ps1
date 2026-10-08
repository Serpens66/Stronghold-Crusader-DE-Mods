[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
function Get-Sha256([string]$Path) {
    $stream=[IO.File]::OpenRead($Path)
    $algorithm=[Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','') }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}

$workspace=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$mod=$PSScriptRoot
$roots=@((Join-Path $workspace 'APIShared'),(Join-Path $workspace 'BugfixesAndQoL'),$mod)
foreach($root in $roots) {
    $files=@(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
        $_.FullName -notmatch '\\(?:bin|obj|BepInEx|tests)\\' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.json','.xaml')
    })
    foreach($file in $files) {
        $source=[IO.File]::ReadAllText($file.FullName)
        if($source -match '(?<!\r)\n') { throw "Non-CRLF: $($file.FullName)" }
        if($file.Extension -in @('.cs','.csproj') -and $source -match 'System\.Text\.Json|Newtonsoft\.Json|JavaScriptSerializer|System\.Web\.Extensions|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json') { throw "Forbidden JSON dependency: $($file.FullName)" }
        if($file.Extension -eq '.cs' -and $source -match '\b(OnDestroy|OnDisable|OnApplicationQuit|StartCoroutine|LateUpdate|FixedUpdate)\s*\(') { throw "Lifecycle callback: $($file.FullName)" }
        if($file.Name -like '*Plugin.cs' -and $source -match '\b(Update|OnApplicationPause)\s*\(') { throw "Plugin runtime callback: $($file.FullName)" }
        if($file.Extension -eq '.xaml') {
            [xml]$xml=$source
            foreach($content in @($xml.SelectNodes("//*[local-name()='Content']"))) {
                if(@($content.ChildNodes | Where-Object NodeType -eq 'Element').Count -ne 1) { throw "XAML Content root: $($file.FullName)" }
            }
        }
    }
}
& (Join-Path $workspace 'Shared\Tools\Validation\Test-PermanentNativeRuntimePatches.ps1')
if(-not $?) { throw 'Workspace permanent-hook regression failed' }
$guard=[IO.File]::ReadAllText((Join-Path $workspace 'APIShared\src\Pathfinding\AssassinAttackControlAPI.cs'))
if($guard -match '\b(transaction|hook)\??\.Dispose\s*\(' -or $guard -match '\.(Enable|Disable|Undo)\s*\(' -or $guard -notmatch 'if \(hook == candidate\) throw;') { throw 'Published Assassin hook teardown' }
$project=[IO.File]::ReadAllText((Join-Path $mod 'AssassinAttackControlTest.csproj'))
if($project -match 'publicized') { throw 'Testmod must reference real Unity assembly' }
$native='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
if((Get-Sha256 $native) -ne 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2') { throw 'Native image changed' }
Write-Host 'PASS Assassin attack runtime JSON/lifecycle/permanent-hook/XAML/CRLF/native-hash contracts.'
