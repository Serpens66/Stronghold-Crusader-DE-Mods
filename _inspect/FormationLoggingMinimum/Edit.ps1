$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$files = @('src/FormationPreviewOverlay.cs','src/FormationMenuViewModel.cs','src/Formations/FormationRuntime.Diagnostics.cs','src/Formations/FormationRuntime.OrderEvents.cs','src/Formations/FormationRuntime.Preview.cs','src/Formations/FormationRuntime.Input.cs','src/Formations/FormationRuntime.Activation.cs','src/Formations/FormationRuntime.SelectionAndTargets.cs','src/Formations/FormationRuntime.EngineAndCameraHooks.cs','src/Formations/FormationRuntime.State.cs')
$markers = @('FORMATION_DRAG_ABORTED','FORMATION_RELEASE_CLAIMED','FORMATION_RELEASE_CONSUMED','FORMATION_ORDER_QUEUED','FORMATION_RELEASE_EVENT','FORMATION_ROWS_CHANGED','FORMATION_DRAG_START','FORMATION_PREVIEW_UPDATED','FORMATION_MENU_CHANGED','FORMATION_MENU_VISIBILITY')
foreach ($relative in $files) {
    $path = Join-Path $root ('BugfixesAndQoL/' + $relative)
    $text = [IO.File]::ReadAllText($path)
    # Remove complete logging statements, never their enclosing gameplay blocks.
    $text = [regex]::Replace($text, '(?ms)^\s*(?:Shared\.DebugLogHelper\.LogDebugLimited|LogDebugNoThrow)\([^;]*?\);\r?\n', [Text.RegularExpressions.MatchEvaluator]{param($match)
        foreach ($marker in $markers) { if ($match.Value.Contains($marker)) { return '' } }
        return $match.Value
    })
    if ($relative.EndsWith('FormationRuntime.Preview.cs')) {
        $text = $text.Replace('                    bool wasConfirmed = state.Authorization.IsConfirmed;' + "`r`n", '')
        $text = [regex]::Replace($text, '(?ms)^                    if \(allowed && !wasConfirmed\)\r?\n                        LogDebugNoThrow\([^;]*?\);\r?\n', '')
    }
    if ($relative.EndsWith('FormationRuntime.Input.cs')) {
        $text = $text.Replace('                LogTargetRejection(modeRejection, default);' + "`r`n", '')
        $text = [regex]::Replace($text, '(?m)^                if \(targetRejection != APIShared.Internal.GroundMovePreviewRejection.None\)\r?\n                    LogTargetRejection\(targetRejection, target\);\r?\n', '')
        $text = $text.Replace('out APIShared.Internal.GroundMovePreviewRejection targetRejection)', 'out _)')
    }
    if ($relative.EndsWith('FormationRuntime.SelectionAndTargets.cs')) {
        $text = [regex]::Replace($text, '(?ms)^        private void LogTargetRejection\(.*?^        }\r?\n\r?\n', '')
    }
    if ($relative.EndsWith('FormationRuntime.State.cs')) {
        $text = [regex]::Replace($text, '(?m)^        private readonly HashSet<string> loggedTargetRejections =\r?\n[^\r\n]*;\r?\n', '')
    }
    if ($relative.EndsWith('FormationRuntime.OrderEvents.cs')) {
        $begin = $text.IndexOf('                    LogDebugNoThrow(')
        $end = $text.IndexOf("                }`r`n                catch", $begin)
        if ($begin -lt 0 -or $end -lt $begin -or -not $text.Substring($begin,$end-$begin).Contains('FORMATION_ORDER_GEOMETRY')) { throw 'Order diagnostics boundaries not found' }
        $text = $text.Remove($begin, $end-$begin)
        $text = $text.Replace('                else' + "`r`n" + '                    LogDebugNoThrow(diagnostic);' + "`r`n", '')
    }
    if ($relative.EndsWith('FormationRuntime.Diagnostics.cs')) {
        $text = [regex]::Replace($text, '(?ms)^                    // These are once-per-command records,.*?^                        Shared.DebugLogHelper.LogDebugLimited\([^\r\n]*\);', '                    Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/src/Formations/FormationRuntime.Diagnostics.cs:60", message);')
    }
    if ($relative.EndsWith('FormationPreviewOverlay.cs')) {
        $text = [regex]::Replace($text, '(?ms)^                if \(renderedGeneration != current.Generation\).*?^                }\r?\n', '')
        foreach ($line in @('        private static int renderedGeneration;','                int visible = 0;','                int skipped = 0;','                        skipped++;','                    visible++;')) { $text = $text.Replace($line + "`r`n", '') }
    }
    $text = [regex]::Replace($text, '\r?\n', "`r`n")
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
    if (-not [string]::Equals($text,[IO.File]::ReadAllText($path),[StringComparison]::Ordinal)) { throw 'Write verification failed' }
}
