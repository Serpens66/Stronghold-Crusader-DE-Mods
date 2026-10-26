$ErrorActionPreference = 'Stop'
$mod = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sources = @(Get-ChildItem -LiteralPath (Join-Path $mod 'src') -Recurse -File -Filter '*.cs')
$text = ($sources | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`r`n"
# Limited source guards for accidental command/hook/Unity-loop additions, not a runtime proof.
if ($text -match '\b(Newtonsoft|JsonUtility|JavaScriptSerializer|DataContractJsonSerializer)\b|System\.Text\.Json') { throw 'Forbidden runtime serializer.' }
if ($text -match '\b(OnDestroy|OnDisable|LateUpdate|FixedUpdate|StartCoroutine)\s*\(|\bvoid\s+Update\s*\(\s*\)') { throw 'Unsafe plugin lifetime callback.' }
if ($text -match '\b(Marshal\.Write\w*|VirtualProtect|GetDelegateForFunctionPointer|HarmonyPatch|CreateHook|InstallHook|RegisterTraversalProvider|SubmitCommand)\s*\(') { throw 'Observer acquired a mutation/hook path.' }
[xml]$project = [IO.File]::ReadAllText((Join-Path $mod 'BetterPathfindingTest.csproj'))
foreach ($item in $project.SelectNodes('//*[local-name()="Compile"][@Include]')) { if ([string]$item.Include -match 'BugfixesAndQoL|MoatMove') { throw 'Production oracle leaked into runtime compilation.' } }
if ($text -notmatch 'Shared\.DependencyFreeJson\.Serialize' -or $text -notmatch 'Application\.onBeforeRender\s*\+=' -or $text -notmatch 'TryRegisterObserver\(') { throw 'Required persistence/lifecycle contracts missing.' }
Write-Host 'PASS: scoped serializer, passive source and persistent callback guards (limited static checks).'
