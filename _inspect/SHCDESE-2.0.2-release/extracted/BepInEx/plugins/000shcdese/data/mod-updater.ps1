param (
    [Parameter(Mandatory=$true)]
    [int]$GamePid,

    [Parameter(Mandatory=$true)]
    [string]$StagingDir,

    [Parameter(Mandatory=$true)]
    [string]$TargetDir,

    [Parameter(Mandatory=$true)]
    [string]$GameExe,

    [string]$DeleteManifest = "" 
)

# Wait for the game to close
Write-Host "Waiting for game process ($GamePid) to terminate..."
try {
    Wait-Process -Id $GamePid -ErrorAction Stop
}
catch {
    Write-Host "Process $GamePid is already gone or access denied."
}

# Brief pause to ensure OS releases file locks
Start-Sleep -Seconds 1

# Process Uninstalls
if (-not [string]::IsNullOrEmpty($DeleteManifest) -and (Test-Path $DeleteManifest)) {
    Write-Host "Processing Uninstalls from: $DeleteManifest"
    
    try {
        $pathsToDelete = Get-Content $DeleteManifest
        foreach ($path in $pathsToDelete) {
            # Skip empty lines
            if ([string]::IsNullOrWhiteSpace($path)) { continue }

            if (Test-Path $path) {
                Write-Host "Removing: $path"
                # -Recurse ensures directories are deleted, -Force handles read-only files
                # ErrorAction SilentlyContinue prevents the script from stopping if a file is locked or vanishes
                Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        Remove-Item -Path $DeleteManifest -Force -ErrorAction SilentlyContinue
    }
    catch {
        Write-Error "Error processing delete manifest: $_"
    }
}

# Apply Updates
if (Test-Path $StagingDir) {
    Write-Host "Applying Updates from: $StagingDir"
    Write-Host "Target: $TargetDir"
    
    # Recursively copy files. 
    # Logic: Get all files in staging, calculate their relative path, copy to target.
    Get-ChildItem -Path $StagingDir -Recurse -File | ForEach-Object {
        $relativePath = $_.FullName.Substring($StagingDir.Length)
        
        # Remove leading slash if present
        if ($relativePath.StartsWith("\") -or $relativePath.StartsWith("/")) {
            $relativePath = $relativePath.Substring(1)
        }

        $destPath = Join-Path $TargetDir $relativePath
        $destFolder = Split-Path $destPath -Parent

        if (!(Test-Path $destFolder)) {
            New-Item -ItemType Directory -Path $destFolder -Force | Out-Null
        }

        Write-Host "Updating: $relativePath"
        Copy-Item -Path $_.FullName -Destination $destPath -Force
    }

    Write-Host "Cleaning up Staging..."
    Remove-Item $StagingDir -Recurse -Force
}

# Restart Game
Write-Host "Restarting Game via Steam..."
Start-Process "steam://run/3024040"