[CmdletBinding()]
param([switch]$SkipCompiler)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$projects = @('APIShared\APIShared.csproj', 'BugfixesAndQoL\BugfixesAndQoL.csproj', 'Testmods\MoatMove\MoatMove.csproj')
$jsonPattern = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
$callbackPattern = '\b(?:public|private|protected|internal)\s+(?:(?:static|override|virtual|async)\s+)*(?:void|IEnumerator|Task)\s+(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate)\s*\(\s*\)|\bStartCoroutine\s*\('
$count = 0
foreach ($relative in $projects) {
    $project = Join-Path $workspace $relative
    $directory = Split-Path -Parent $project
    [xml]$xml = [IO.File]::ReadAllText($project)
    $paths = @($project) + @($xml.Project.ItemGroup.Compile | Where-Object { $_.Include } | ForEach-Object { [IO.Path]::GetFullPath((Join-Path $directory $_.Include)) })
    foreach ($path in $paths | Select-Object -Unique) {
        $text = [IO.File]::ReadAllText($path)
        if ($text -match $jsonPattern) { throw "Forbidden runtime JSON dependency: $path" }
        if ($text -match $callbackPattern) { throw "Runtime component callback or teardown: $path" }
        if ($path -match '\\UnitCommands\\|\\MoatMove\\src\\') {
            if ($text -match '\b(?:CodePatch\.Write|VirtualProtect|FlushInstructionCache)\s*\(|\.Hook\.(?:Enable|Disable|Undo|Dispose)\s*\(') {
                throw "Executable hook mutation in shared command runtime: $path"
            }
            if ($text -match 'NativeMemoryManager\.WriteStub\s*\(' -and
                ([IO.Path]::GetFileName($path) -ne 'FastNativeKernel.cs' -or
                 [regex]::Matches($text, 'NativeMemoryManager\.WriteStub\s*\(').Count -ne 1)) {
                throw "Unexpected executable publication outside private FastNative constructor: $path"
            }
        }
        if ($text -match '(?<!\r)\n') { throw "Bare LF: $path" }
        $count++
    }
    foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File -Filter '*.xaml' | Where-Object { $_.FullName -notmatch '\\(?:bin|obj|BepInEx)\\' }) {
        [xml]$patch = [IO.File]::ReadAllText($file.FullName)
        foreach ($content in $patch.SelectNodes('//*[local-name()="Content"]')) {
            $elements = @($content.ChildNodes | Where-Object { $_.NodeType -eq [Xml.XmlNodeType]::Element })
            if ($elements.Count -ne 1) { throw "XAML Content requires exactly one root: $($file.FullName)" }
        }
    }
}
& (Join-Path $workspace 'Shared\Test-PermanentNativeRuntimePatches.ps1')
if (-not $SkipCompiler) {
    & dotnet run --project (Join-Path $workspace 'Shared\UnitCommandSourceChecks\UnitCommandSourceChecks.csproj') -- $workspace --real
    if ($LASTEXITCODE -ne 0) { throw 'Real installed game assembly source check failed.' }
}
Write-Output "PASS: shared command preflight ($count project/source entries), JSON, callbacks, CRLF, XAML, permanent hooks including MoatMove."
