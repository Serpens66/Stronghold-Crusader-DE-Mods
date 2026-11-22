[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$version = '2021.3.45f2'
$editor = if ($env:SERP_TRAILS_UNITY_EDITOR) { $env:SERP_TRAILS_UNITY_EDITOR } else {
    'E:\GameDev\Unity\2021.3.45f2\Editor\Unity.exe'
}
$source = Join-Path $PSScriptRoot 'shader'
$shader = Join-Path $source 'ProjectileTrails.shader'
$builder = Join-Path $source 'BuildShaderBundle.cs'
$package = Join-Path $PSScriptRoot 'obj\shader'
$cached = Join-Path $package 'projectiletrails-shader'
$stamp = Join-Path $package 'inputs.txt'
$inputs = $version + ' ' + ((Get-FileHash -LiteralPath @($shader, "$shader.meta", $builder, $PSCommandPath)).Hash -join ' ')
if ((Test-Path -LiteralPath $cached -PathType Leaf) -and (Test-Path -LiteralPath $stamp -PathType Leaf)) {
    $expected = $inputs + ' ' + (Get-FileHash -LiteralPath $cached).Hash
    if ([IO.File]::ReadAllText($stamp).Trim() -eq $expected) { return }
}
if (-not (Test-Path -LiteralPath $editor -PathType Leaf) -or
    (Get-Item -LiteralPath $editor).VersionInfo.ProductVersion -notlike "$version*") {
    throw "Install Unity $version or set SERP_TRAILS_UNITY_EDITOR to that version's Unity.exe."
}

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $root 'Shared/Tools/Paths/WorkPaths.ps1')
$work = New-ShortWorkDirectory -Root $root -Kind t -Name 'trail-shader' -Identity 'Build the shader for the installed player'
$project = Join-Path $work 'p'
$log = Join-Path $work 'Unity.log'
$success = $false
try {
    foreach ($directory in 'Assets/Editor','Packages','ProjectSettings') {
        [void][IO.Directory]::CreateDirectory((Join-Path $project $directory))
    }
    Copy-Item -LiteralPath $shader -Destination (Join-Path $project 'Assets/ProjectileTrails.shader')
    Copy-Item -LiteralPath "$shader.meta" -Destination (Join-Path $project 'Assets/ProjectileTrails.shader.meta')
    Copy-Item -LiteralPath $builder -Destination (Join-Path $project 'Assets/Editor/BuildShaderBundle.cs')
    $encoding = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText((Join-Path $project 'Packages/manifest.json'), '{"dependencies":{"com.unity.modules.assetbundle":"1.0.0"}}', $encoding)
    [IO.File]::WriteAllText((Join-Path $project 'ProjectSettings/ProjectVersion.txt'),
        "m_EditorVersion: $version`r`nm_EditorVersionWithRevision: $version (88f88f591b2e)`r`n", $encoding)
    $arguments = "-batchmode -nographics -quit -projectPath `"$project`" -buildTarget Win64 -executeMethod BuildShaderBundle.Build -logFile `"$log`""
    Write-Output "Unity shader build log $log"
    $proc = Start-Process -FilePath $editor -ArgumentList $arguments -WorkingDirectory $project -WindowStyle Hidden -PassThru -Wait
    $asset = Join-Path $project 'Bundles/projectiletrails-shader'
    if ($proc.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $asset -PathType Leaf)) {
        if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 35 }
        throw "Unity shader build failed with exit code $($proc.ExitCode)."
    }
    [void][IO.Directory]::CreateDirectory($package)
    Copy-Item -LiteralPath $asset -Destination $cached -Force
    [IO.File]::WriteAllText($stamp, $inputs + ' ' + (Get-FileHash -LiteralPath $cached).Hash, $encoding)
    $success = $true
}
finally {
    if ($success) { Remove-ShortWorkDirectory -Path $work }
}
