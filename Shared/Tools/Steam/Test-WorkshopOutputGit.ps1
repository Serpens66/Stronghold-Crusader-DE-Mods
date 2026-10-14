$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$fixture = Join-Path $workspace ('.inspect/workshop-output-git-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($fixture)
$checks = 0
function Check([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}
function Invoke-FixtureGit([string[]]$GitArguments) {
    $result = @(& git -C $fixture @GitArguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Fixture git failed: $($result -join [Environment]::NewLine)" }
    return $result
}
Copy-Item -LiteralPath (Join-Path $workspace '.gitignore') -Destination $fixture
$null = Invoke-FixtureGit @('init', '--quiet')
foreach ($relative in @('ExampleMod/source.cs', 'SerpsModsHost/info.json')) {
    $path = Join-Path $fixture $relative
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $path))
    [IO.File]::WriteAllText($path, 'original')
}
$null = Invoke-FixtureGit @('add', '--', '.gitignore', 'ExampleMod/source.cs', 'SerpsModsHost/info.json')
$null = Invoke-FixtureGit @('-c', 'user.name=Workshop Regression', '-c', 'user.email=workshop-test@example.invalid', '-c', 'commit.gpgsign=false', 'commit', '--quiet', '-m', 'Fixture inputs')
$source = Join-Path $fixture '.release-output/stage'
[void][IO.Directory]::CreateDirectory($source)
foreach ($target in @('APISharedWorkshop', 'SerpsMods')) {
    $destination = Join-Path $fixture $target
    foreach ($generation in 1..3) {
        [IO.File]::WriteAllText((Join-Path $source 'package.map'), "generation $generation")
        Publish-WorkshopFolder $source $destination $fixture
        Check ([IO.File]::ReadAllText((Join-Path $destination 'package.map')) -ceq "generation $generation") 'Published content differs.'
        Check (@(Invoke-FixtureGit @('status', '--porcelain=v1', '--untracked-files=normal')).Count -eq 0) 'Workshop publication dirtied the repository.'
    }
    $backups = @(Get-ChildItem -LiteralPath $fixture -Directory -Filter ($target + '.previous-*'))
    Check ($backups.Count -eq 2) 'Repeated publication did not retain both backups.'
    foreach ($suffix in @('.ready-', '.previous-')) {
        $relative = $target + $suffix + [guid]::NewGuid().ToString('N') + '/package.map'
        $path = Join-Path $fixture $relative
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $path))
        [IO.File]::WriteAllText($path, 'retained artifact')
        $null = Invoke-FixtureGit @('check-ignore', '--', $relative)
        $checks++
    }
}
Check (@(Invoke-FixtureGit @('status', '--porcelain=v1', '--untracked-files=normal')).Count -eq 0) 'Replacement artifacts dirtied the repository.'
foreach ($relative in @('ExampleMod/source.cs', 'SerpsModsHost/info.json')) {
    [IO.File]::WriteAllText((Join-Path $fixture $relative), 'changed')
    Check (@(Invoke-FixtureGit @('status', '--porcelain=v1', '--', $relative)).Count -eq 1) "Source change hidden: $relative"
}
$newSource = Join-Path $fixture 'SerpsModsHost/new-source.cs'
[IO.File]::WriteAllText($newSource, 'new source')
Check (@(Invoke-FixtureGit @('status', '--porcelain=v1', '--untracked-files=normal', '--', 'SerpsModsHost/new-source.cs')).Count -eq 1) 'New host source hidden.'
Write-Host "PASS: $checks Workshop output Git checks. Fixtures: $fixture"
