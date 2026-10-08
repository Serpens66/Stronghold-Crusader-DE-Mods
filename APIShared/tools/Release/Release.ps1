[CmdletBinding(SupportsShouldProcess)]
param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$repository = 'SHCDE-APIShared/APIShared'
$identity = (& gh api user --jq .login).Trim()
if ($LASTEXITCODE -ne 0 -or -not $identity) { throw 'Sign in with gh auth login first.' }
$top = (& git -C $root rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($top) -ne $root) {
    throw 'Publish from the independent APIShared repository after exporting the reviewed workspace commit.'
}
$status = @(& git -C $root status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the repository Git state.' }
if ($status.Count -ne 0) { throw 'Commit the reviewed source changes before preparing a release.' }
$manifest = [IO.File]::ReadAllText((Join-Path $root 'info.json')) | ConvertFrom-Json
$version = [string]$manifest.Version
$parsed = $null
if (-not [version]::TryParse($version, [ref]$parsed)) { throw 'Invalid APIShared version.' }
$parsed = [version]::new($parsed.Major, $parsed.Minor, [Math]::Max(0, $parsed.Build), [Math]::Max(0, $parsed.Revision))
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not determine the reviewed source commit.' }
& gh api "repos/$repository/git/commits/$commit" --silent
if ($LASTEXITCODE -ne 0) { throw 'Push the reviewed source commit to the APIShared repository before preparing its release.' }
$tag = "APIShared/v$version"
& (Join-Path $root 'build.bat') /nopause /noinstall
if ($LASTEXITCODE -ne 0) { throw 'The release build or regression tests failed.' }
$package = Join-Path $root 'BepInEx\plugins\APIShared_Serp'
$dll = Join-Path $package 'APIShared.dll'
if ([Reflection.AssemblyName]::GetAssemblyName($dll).Version -ne $parsed) { throw 'Assembly and manifest versions differ.' }
$forbidden = @(Get-ChildItem -LiteralPath $package -Recurse -File | Where-Object {
    ($_.Extension -in @('.dll', '.exe') -and $_.Name -ne 'APIShared.dll') -or $_.Name -match '(?i)credential|token|secret'
})
if ($forbidden.Count -ne 0) { throw 'The package contains unexpected runtime dependencies or sensitive files.' }
$output = Join-Path $root '.local\release'
[IO.Directory]::CreateDirectory($output) | Out-Null
$staging = Join-Path $output ('APIShared-v' + $version + '-' + [Guid]::NewGuid().ToString('N'))
$pluginTarget = Join-Path $staging 'BepInEx\plugins\APIShared_Serp'
[IO.Directory]::CreateDirectory($pluginTarget) | Out-Null
Copy-Item -LiteralPath (Join-Path $package 'APIShared.dll') -Destination $pluginTarget
Copy-Item -LiteralPath (Join-Path $package 'APIShared.xml') -Destination $pluginTarget
Copy-Item -LiteralPath (Join-Path $package 'info.json') -Destination $pluginTarget
Copy-Item -LiteralPath (Join-Path $package 'Patches') -Destination $pluginTarget -Recurse
$asset = Join-Path $output "APIShared-v$version.zip"
if (Test-Path -LiteralPath $asset) { throw "Release asset already exists: $asset. Review it before choosing a new output." }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($staging, $asset)
$hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
$notes = Join-Path $staging 'release-notes.md'
$text = "APIShared $version`r`n`r`nSource commit: $commit`r`nZIP SHA-256: $hash`r`n`r`nRequires Script Extender $($manifest.MinimumScriptExtenderVersion) or newer. Install one central copy; consumer mods must not bundle APIShared.`r`n"
[IO.File]::WriteAllText($notes, $text, [Text.UTF8Encoding]::new($false))
$arguments = @('release', 'create', $tag, $asset, '--repo', $repository, '--target', $commit, '--title', "APIShared $version", '--notes-file', $notes)
if (-not $Publish) { $arguments += '--draft' }
if ($PSCmdlet.ShouldProcess("$repository / $tag", 'Create release with the validated APIShared asset')) {
    & gh @arguments
    if ($LASTEXITCODE -ne 0) { throw 'GitHub release creation failed; the validated local artifact was preserved.' }
}
