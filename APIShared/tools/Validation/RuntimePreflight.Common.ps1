function Assert-SERuntimeModPreflight([object]$Mod, [string]$Workspace) {
    if (-not $Mod.Plugin) { return }

    $projectPath = Join-Path $Workspace $Mod.Project
    $projectRoot = Split-Path -Parent $projectPath
    $sources = [Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $projectRoot -Recurse -File -Filter '*.cs' | Where-Object {
        $_.FullName -notmatch '[\\/](BepInEx[\\/]plugins|bin|obj|tests|[^\\/]*\.Tests|\.inspect)[\\/]'
    }) {
        $sources.Add($file.FullName)
    }

    [xml]$project = [IO.File]::ReadAllText($projectPath)
    foreach ($compile in $project.SelectNodes('//*[local-name()="Compile"][@Include]')) {
        $include = [string]$compile.Include
        if (-not $include -or $include.IndexOfAny([char[]]'*?') -ge 0) { continue }
        $linkedPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $include))
        if ((Test-Path -LiteralPath $linkedPath -PathType Leaf) -and -not $sources.Contains($linkedPath)) {
            $sources.Add($linkedPath)
        }
    }

    $forbiddenJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility'
    $jsonHits = @($sources | Select-String -Pattern $forbiddenJson)
    $projectHits = @(Select-String -LiteralPath $projectPath -Pattern $forbiddenJson)
    if ($jsonHits -or $projectHits) {
        throw "$($Mod.Name): forbidden runtime JSON dependency or serializer found."
    }

    foreach ($sourcePath in $sources) {
        $text = [IO.File]::ReadAllText($sourcePath)
        # BepInEx plugin components are destroyed during SHCDE startup cleanup.
        # An ordinary service Update method is not a Unity callback; only plugin
        # source files declaring BaseUnityPlugin are subject to this callback gate.
        if ($text -match '\bclass\s+\w+\s*:\s*(?:BepInEx\.)?BaseUnityPlugin\b' -and
            ($text -match '(?m)^\s*(?:(?:public|private|protected|internal|override|virtual|sealed|new|async)\s+)*(?:void|IEnumerator)\s+(?:Update|LateUpdate|FixedUpdate|OnGUI|OnRenderObject|OnPostRender)\s*\(' -or
             $text -match '\b(?:StartCoroutine|InvokeRepeating)\s*\(')) {
            throw "$($Mod.Name): plugin source depends on a short-lived MonoBehaviour callback: $sourcePath"
        }
        if ($text -match '\b(?:StartCoroutine|InvokeRepeating)\s*\(') {
            # Existing bounded loading-warning replacement runs on Vanilla's Director,
            # supplied by its hooked DelayShowDisconnect call, not the plugin component.
            # Its generation gate invalidates stale work after loading/session changes.
            $auditedDirectorDelay = $Mod.Name -eq 'CastlePlanner' -and
                $sourcePath.EndsWith('\src\FreeCastlePreviewRuntime.cs', [StringComparison]::OrdinalIgnoreCase) -and
                $text -match 'private void DelayShowDisconnectHook\(Director self\)' -and
                $text -match 'self\.StartCoroutine\(ShowLoadingWarningAfterDelay\(generation\)\)' -and
                [regex]::Matches($text, '\b(?:StartCoroutine|InvokeRepeating)\s*\(').Count -eq 1 -and
                $text -match 'loadingWarningGate\.IsCurrent\(generation, viewModel\.Show_MP_LoadingBlack\)'
            if (-not $auditedDirectorDelay) {
                throw "$($Mod.Name): long-lived MonoBehaviour scheduling requires an audited persistent publisher: $sourcePath"
            }
        }
        foreach ($match in [regex]::Matches($text, '\b(OnDestroy|OnDisable|OnApplicationQuit)\s*\([^)]*\)\s*\{')) {
            $open = $text.IndexOf('{', $match.Index)
            $depth = 0
            $end = -1
            for ($index = $open; $index -lt $text.Length; $index++) {
                if ($text[$index] -eq '{') { $depth++ }
                elseif ($text[$index] -eq '}') {
                    $depth--
                    if ($depth -eq 0) { $end = $index; break }
                }
            }
            if ($end -lt 0) { throw "$($Mod.Name): unterminated Unity lifecycle method in $sourcePath." }
            $body = $text.Substring($open, $end - $open + 1)
            if ($body -match '\.Dispose\s*\(' -or $body -match 'DisposeRuntime\s*\(' -or $body -match '\.Stop\s*\(') {
                throw "$($Mod.Name): Unity lifecycle method reaches Dispose/Stop in $sourcePath."
            }
        }
    }
}
