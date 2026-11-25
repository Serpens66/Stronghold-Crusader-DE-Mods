[CmdletBinding()]
param([switch]$Verify)

$ErrorActionPreference = 'Stop'
$version = '2021.3.45f2'
$editor = if ($env:SERP_TRAILS_UNITY_EDITOR) { $env:SERP_TRAILS_UNITY_EDITOR } else {
    'E:\GameDev\Unity\2021.3.45f2\Editor\Unity.exe'
}
if (-not (Test-Path -LiteralPath $editor -PathType Leaf) -or
    (Get-Item -LiteralPath $editor).VersionInfo.ProductVersion -notlike "$version*") {
    throw "Install Unity $version or set SERP_TRAILS_UNITY_EDITOR to that version's Unity.exe."
}

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $root 'Shared/Tools/Paths/WorkPaths.ps1')
. (Join-Path $root 'Shared/Tools/Release/Release.Common.ps1')
$source = Join-Path $PSScriptRoot 'Shader'
$published = Join-Path $PSScriptRoot 'projectiletrails-shader'
$work = New-ShortWorkDirectory -Root $root -Kind t -Name 'trail-shader' -Identity 'Build projectile trails for the installed Unity player'
$project = Join-Path $work 'p'
$log = Join-Path $work 'Unity.log'
$success = $false
try {
    foreach ($directory in 'Assets/Editor','Packages','ProjectSettings') {
        [void][IO.Directory]::CreateDirectory((Join-Path $project $directory))
    }
    Copy-Item -LiteralPath (Join-Path $source 'ProjectileTrails.shader') -Destination (Join-Path $project 'Assets')
    Copy-Item -LiteralPath (Join-Path $source 'ProjectileTrails.shader.meta') -Destination (Join-Path $project 'Assets')
    Copy-Item -LiteralPath (Join-Path $source 'BuildShaderBundle.cs') -Destination (Join-Path $project 'Assets/Editor')
    $encoding = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText((Join-Path $project 'Packages/manifest.json'), '{"dependencies":{"com.unity.modules.assetbundle":"1.0.0"}}', $encoding)
    [IO.File]::WriteAllText((Join-Path $project 'ProjectSettings/ProjectVersion.txt'),
        "m_EditorVersion: $version`r`nm_EditorVersionWithRevision: $version (88f88f591b2e)`r`n", $encoding)
    $arguments = "-batchmode -nographics -quit -projectPath `"$project`" -buildTarget Win64 -executeMethod BuildShaderBundle.Build -logFile `"$log`""
    Write-Host "Unity shader build log: $log"
    # Wait for Unity's workers too, since they can still hold the temporary project open.
    $proc = Start-Process -FilePath $editor -ArgumentList $arguments -WorkingDirectory $project -WindowStyle Hidden -PassThru -Wait
    $exitCode = $proc.ExitCode
    $asset = Join-Path $project 'Bundles/projectiletrails-shader'
    if ($exitCode -ne 0 -or -not (Test-Path -LiteralPath $asset -PathType Leaf)) {
        if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 35 }
        throw "Unity shader build failed with exit code $exitCode. Log: $log"
    }
    $builtHash = Get-Sha256Hex -Path $asset
    if ($Verify) {
        if (-not [IO.File]::Exists($published) -or (Get-Sha256Hex -Path $published) -cne $builtHash) {
            throw "Compiled shader differs from the packaged source bundle. Rebuilt SHA-256: $builtHash. Build directory: $work"
        }
        Write-Host "PASS: Unity $version rebuilt the projectile shader byte-for-byte ($builtHash)."
    } else {
        Copy-Item -LiteralPath $asset -Destination $published -Force
        Write-Host "Updated projectiletrails-shader, SHA-256 $builtHash"
    }
    $success = $true
}
finally {
    if ($success) { Remove-ShortWorkDirectory -Path $work }
}
