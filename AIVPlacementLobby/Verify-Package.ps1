param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,

    [string]$InstalledRoot
)

$ErrorActionPreference = 'Stop'

function Get-DotNetSha256([string]$Path) {
    $stream = $null
    $sha256 = $null
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        $hash = $sha256.ComputeHash($stream)
        return [System.BitConverter]::ToString($hash).Replace('-', '')
    }
    finally {
        if ($null -ne $sha256) {
            $sha256.Dispose()
        }
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

try {
    $packagePath = [System.IO.Path]::GetFullPath($PackageRoot).TrimEnd('\', '/')
    if (![System.IO.Directory]::Exists($packagePath)) {
        throw "Paketordner fehlt: $packagePath"
    }

    $expectedRootFiles = @(
        'AIVParser.Core.dll',
        'AIVParser.Core.pdb',
        'AIVPlacement.Core.dll',
        'AIVPlacement.Core.pdb',
        'AIVPlacementLobby.Core.dll',
        'AIVPlacementLobby.Core.pdb',
        'AIVPlacementLobby.dll',
        'AIVPlacementLobby.pdb',
        'MapParser.Core.dll',
        'MapParser.Core.pdb',
        'info.json'
    )
    $rootFiles = @([System.IO.Directory]::GetFiles($packagePath, '*', [System.IO.SearchOption]::TopDirectoryOnly))
    $actualRootNames = @($rootFiles | ForEach-Object { [System.IO.Path]::GetFileName($_) })
    $missingRootFiles = @($expectedRootFiles | Where-Object { $_ -notin $actualRootNames })
    $unexpectedRootFiles = @($actualRootNames | Where-Object { $_ -notin $expectedRootFiles })
    if ($missingRootFiles.Count -ne 0 -or $unexpectedRootFiles.Count -ne 0) {
        throw "Ungueltige Paketwurzel. Fehlend=[$($missingRootFiles -join ', ')], unerwartet=[$($unexpectedRootFiles -join ', ')]."
    }

    $rootDirectories = @([System.IO.Directory]::GetDirectories($packagePath, '*', [System.IO.SearchOption]::TopDirectoryOnly))
    $actualRootDirectoryNames = @($rootDirectories | ForEach-Object { [System.IO.Path]::GetFileName($_) })
    $expectedRootDirectoryNames = @('Locales', 'Patches', 'VanillaAIV')
    $missingRootDirectories = @($expectedRootDirectoryNames | Where-Object { $_ -notin $actualRootDirectoryNames })
    $unexpectedRootDirectories = @($actualRootDirectoryNames | Where-Object { $_ -notin $expectedRootDirectoryNames })
    if ($missingRootDirectories.Count -ne 0 -or $unexpectedRootDirectories.Count -ne 0) {
        throw "Ungueltige Paketordner. Fehlend=[$($missingRootDirectories -join ', ')], unerwartet=[$($unexpectedRootDirectories -join ', ')]."
    }

    $vanillaPath = [System.IO.Path]::Combine($packagePath, 'VanillaAIV')
    $vanillaFiles = @([System.IO.Directory]::GetFiles($vanillaPath, '*', [System.IO.SearchOption]::TopDirectoryOnly))
    $unexpectedAivFiles = @($vanillaFiles | Where-Object {
        ![string]::Equals(
            [System.IO.Path]::GetExtension($_),
            '.aivjson',
            [System.StringComparison]::OrdinalIgnoreCase)
    })
    if ($vanillaFiles.Count -eq 0 -or $unexpectedAivFiles.Count -ne 0) {
        throw "VanillaAIV muss mindestens eine und ausschliesslich .aivjson-Dateien enthalten; gefunden=$($vanillaFiles.Count), unerwartet=$($unexpectedAivFiles.Count)."
    }

    $localePath = [System.IO.Path]::Combine($packagePath, 'Locales')
    $localeFiles = @([System.IO.Directory]::GetFiles($localePath, '*.txt', [System.IO.SearchOption]::TopDirectoryOnly))
    if ($localeFiles.Count -ne 21) {
        throw "Locales muss genau 21 Sprachdateien enthalten; gefunden=$($localeFiles.Count)."
    }

    $patchPath = [System.IO.Path]::Combine(
        $packagePath,
        'Patches\Assets\GUI\XAMLResources\FRONT_Multiplayer_AISettings.xaml')
    if (![System.IO.File]::Exists($patchPath)) {
        throw "AIV-Auswahl-Patch fehlt: $patchPath"
    }
    $patchFiles = @([System.IO.Directory]::GetFiles(
        [System.IO.Path]::Combine($packagePath, 'Patches'),
        '*',
        [System.IO.SearchOption]::AllDirectories))
    if ($patchFiles.Count -ne 1) {
        throw "Patches muss genau den erwarteten AIV-Auswahl-Patch enthalten; gefunden=$($patchFiles.Count)."
    }
    $patchContent = [System.IO.File]::ReadAllText($patchPath)
    $requiredPatchFragments = @(
        'Background="#FF1D1710"',
        'BorderBrush="#FFF2D48A"',
        'Text="{Binding StatusToolTip}"',
        'BorderBrush="#FF382A18"'
    )
    foreach ($fragment in $requiredPatchFragments) {
        if (!$patchContent.Contains($fragment)) {
            throw "AIV-Auswahl-Patch enthaelt die erwartete deckende Tooltip-/Marker-Darstellung nicht: $fragment"
        }
    }

    $packageFiles = @([System.IO.Directory]::GetFiles($packagePath, '*', [System.IO.SearchOption]::AllDirectories))
    $expectedPackageFileCount =
        $expectedRootFiles.Count + $vanillaFiles.Count + $localeFiles.Count + $patchFiles.Count
    if ($packageFiles.Count -ne $expectedPackageFileCount) {
        throw "Das vollstaendige Paket enthaelt unerwartete oder fehlende Dateien; erwartet=$expectedPackageFileCount, gefunden=$($packageFiles.Count)."
    }

    if (![string]::IsNullOrWhiteSpace($InstalledRoot)) {
        $installedPath = [System.IO.Path]::GetFullPath($InstalledRoot).TrimEnd('\', '/')
        if (![System.IO.Directory]::Exists($installedPath)) {
            throw "Installationsordner fehlt: $installedPath"
        }

        $installedFiles = @([System.IO.Directory]::GetFiles($installedPath, '*', [System.IO.SearchOption]::AllDirectories))
        if ($installedFiles.Count -ne $packageFiles.Count) {
            throw "Installierte Dateianzahl stimmt nicht: Paket=$($packageFiles.Count), Installation=$($installedFiles.Count)."
        }

        foreach ($packageFile in $packageFiles) {
            $relativePath = $packageFile.Substring($packagePath.Length).TrimStart('\', '/')
            $installedFile = [System.IO.Path]::Combine($installedPath, $relativePath)
            if (![System.IO.File]::Exists($installedFile)) {
                throw "Installierte Datei fehlt: $relativePath"
            }

            # Direct .NET hashing avoids dependence on PowerShell module auto-loading.
            $packageHash = Get-DotNetSha256 $packageFile
            $installedHash = Get-DotNetSha256 $installedFile
            if (![string]::Equals($packageHash, $installedHash, [System.StringComparison]::Ordinal)) {
                throw "Installierte Datei weicht vom Paket ab: $relativePath"
            }
        }
    }

    Write-Output "Paketpruefung erfolgreich: $($packageFiles.Count) Dateien, davon $($vanillaFiles.Count) Vanilla-AIVJSON-Dateien und $($localeFiles.Count) Locales."
    exit 0
}
catch {
    Write-Error $_
    exit 1
}
