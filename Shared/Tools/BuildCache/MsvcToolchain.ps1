# Shared native-build selection. Resolving or fingerprinting never invokes a compiler
# and never changes INCLUDE/LIB; Build-AssassinSearch owns those process settings.
function Get-MsvcToolchain {
    param([string]$MsvcRoot='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC',
        [string]$SdkRoot='C:\Program Files (x86)\Windows Kits\10')
    $compilerRoot = (Get-ChildItem -LiteralPath $MsvcRoot -Directory | Sort-Object { [version]$_.Name } -Descending | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'bin/Hostx64/x64/cl.exe') } | Select-Object -First 1).FullName
    if (-not $compilerRoot) { throw 'MSVC x64 compiler not found (Visual Studio 2022 Build Tools).' }
    $sdk = Get-ChildItem -LiteralPath (Join-Path $SdkRoot 'Include') -Directory | Sort-Object Name -Descending | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'ucrt/stdio.h') } | Select-Object -First 1
    if (-not $sdk) { throw 'Windows SDK not found.' }
    return [pscustomobject]@{CompilerRoot=$compilerRoot;SdkRoot=[IO.Path]::GetFullPath($SdkRoot);Sdk=$sdk;
        IncludeDirectories=@((Join-Path $compilerRoot 'include'),(Join-Path $sdk.FullName 'ucrt'),(Join-Path $sdk.FullName 'shared'),(Join-Path $sdk.FullName 'um'));
        LibraryDirectories=@((Join-Path $compilerRoot 'lib/x64'),(Join-Path $SdkRoot ('Lib/'+$sdk.Name+'/ucrt/x64')),(Join-Path $SdkRoot ('Lib/'+$sdk.Name+'/um/x64')))}
}
function Get-MsvcToolchainFiles([string]$Directory) {
    if(-not [IO.Directory]::Exists($Directory)){throw "Native toolchain input directory missing: $Directory"}
    for($part=[IO.Path]::GetFullPath($Directory);$part;$part=[IO.Path]::GetDirectoryName($part)) {
        if([IO.File]::GetAttributes($part)-band [IO.FileAttributes]::ReparsePoint){throw 'Linked native toolchain input.'}
    }
    foreach($entry in Get-ChildItem -LiteralPath $Directory -Force) {
        if($entry.Attributes-band [IO.FileAttributes]::ReparsePoint){throw 'Linked native toolchain input.'}
        if($entry.PSIsContainer){Get-MsvcToolchainFiles $entry.FullName}else{$entry.FullName}
    }
}
function Get-MsvcToolchainInputFingerprint($Toolchain) {
    $binaryRoot=Join-Path $Toolchain.CompilerRoot 'bin/Hostx64/x64'
    foreach($required in @('cl.exe','link.exe','c1xx.dll','c2.dll')) {
        if(-not [IO.File]::Exists((Join-Path $binaryRoot $required))){throw "Native toolchain input missing: $required"}
    }
    $rows=[Collections.Generic.List[string]]::new()
    $rows.Add('selection:'+($Toolchain | Select-Object CompilerRoot,SdkRoot,@{Name='SdkVersion';Expression={$_.Sdk.Name}},IncludeDirectories,LibraryDirectories | ConvertTo-Json -Depth 3 -Compress))
    $files=@(foreach($directory in (@($binaryRoot)+$Toolchain.IncludeDirectories+$Toolchain.LibraryDirectories)){Get-MsvcToolchainFiles $directory})
    foreach($file in $files | Sort-Object -Unique) {
        $stream=[IO.File]::OpenRead($file);$hash=[Security.Cryptography.SHA256]::Create()
        try {$rows.Add($file+':'+([BitConverter]::ToString($hash.ComputeHash($stream))).Replace('-','').ToLowerInvariant())}
        finally {$hash.Dispose();$stream.Dispose()}
    }
    $rows.Sort([StringComparer]::Ordinal);$hash=[Security.Cryptography.SHA256]::Create()
    try {return ([BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($rows -join "`n")))).Replace('-','').ToLowerInvariant()}
    finally {$hash.Dispose()}
}
