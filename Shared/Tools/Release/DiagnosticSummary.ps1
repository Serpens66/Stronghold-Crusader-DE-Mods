# Informational package inspection only. Unknown or enabled diagnostics never block a release.
function Write-PackageDiagnosticSummary {
    param([Parameter(Mandatory)][string]$Directory, [string]$CecilPath)
    Write-Host 'Diagnose im vorgesehenen Paket (Hinweis, keine Freigabesperre):' -ForegroundColor Cyan
    try {
        $Directory = [IO.Path]::GetFullPath($Directory).TrimEnd('\','/')
        if (-not ('Mono.Cecil.AssemblyDefinition' -as [type])) {
            if (-not $CecilPath) { throw 'Mono.Cecil nicht angegeben.' }
            # Load bytes without locking or modifying the installed dependency.
            [void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($CecilPath))
        }
        foreach ($dll in @(Get-ChildItem -LiteralPath $Directory -Recurse -File -Filter '*.dll')) {
            $assembly = $null
            $stream = $null
            try {
                $stream = [IO.MemoryStream]::new([IO.File]::ReadAllBytes($dll.FullName))
                $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($stream)
                $helper = $assembly.MainModule.Types | Where-Object FullName -eq 'Shared.DebugLogHelper' | Select-Object -First 1
                $state = 'unbekannt; keine gemeinsame Diagnosefreigabe erkannt'
                if ($helper) {
                    $initialize = $helper.Methods | Where-Object Name -eq 'InitializeDiagnostics' | Select-Object -First 1
                    if ($initialize -and $initialize.HasBody) {
                        $instructions = $initialize.Body.Instructions
                        # Recognize only the actual config binding's literal default, not a source assumption.
                        for ($i = 0; $i -lt $instructions.Count - 4; $i++) {
                            if ($instructions[$i].OpCode.Name -ceq 'ldstr' -and $instructions[$i].Operand -ceq 'Diagnostics' -and
                                $instructions[$i+1].OpCode.Name -ceq 'ldstr' -and $instructions[$i+1].Operand -ceq 'Enabled' -and
                                $instructions[$i+3].OpCode.Name -ceq 'ldstr' -and
                                $instructions[$i+4].OpCode.Name -eq 'callvirt' -and
                                [string]$instructions[$i+4].Operand -match 'BepInEx.Configuration.ConfigFile::Bind') {
                                if ($instructions[$i+2].OpCode.Name -ceq 'ldc.i4.0') { $state = 'standardmaessig AUS; lokales Opt-in moeglich' }
                                elseif ($instructions[$i+2].OpCode.Name -ceq 'ldc.i4.1') { $state = 'standardmaessig AN' }
                                break
                            }
                        }
                    }
                }
                $crash = $assembly.MainModule.Types | Where-Object FullName -eq 'Shared.CrashBreadcrumbDiagnostics' | Select-Object -First 1
                if ($crash -and $helper) {
                    $default = 'unbekannt'
                    $init = $helper.Methods | Where-Object Name -eq 'InitializeDiagnostics' | Select-Object -First 1
                    if ($init -and $init.HasBody) {
                        $il = $init.Body.Instructions
                        for ($i=0; $i -lt $il.Count-4; $i++) {
                            if ($il[$i].OpCode.Name -ceq 'ldstr' -and $il[$i].Operand -ceq 'Diagnostics' -and
                                $il[$i+1].OpCode.Name -ceq 'ldstr' -and $il[$i+1].Operand -ceq 'CrashBreadcrumbs' -and
                                $il[$i+4].OpCode.Name -ceq 'callvirt' -and [string]$il[$i+4].Operand -match 'ConfigFile::Bind') {
                                if ($il[$i+2].OpCode.Name -ceq 'ldc.i4.0') { $default='AUS' }
                                elseif ($il[$i+2].OpCode.Name -ceq 'ldc.i4.1') { $default='AN' }
                                break
                            }
                        }
                    }
                    Write-Host ('  ' + $dll.Name + ': CrashBreadcrumbs standardmaessig ' + $default)
                }
                $line = '  ' + $dll.FullName.Substring($Directory.Length).TrimStart('\','/') + ': ' + $state
                if ($state -ceq 'standardmaessig AN') { Write-Host ('WARNUNG: DIAGNOSE AKTIV! ' + $line) -ForegroundColor Yellow }
                else { Write-Host $line }
            } catch {
                Write-Host ('  ' + $dll.Name + ': Diagnosezustand unbekannt (' + $_.Exception.Message + ')')
            } finally {
                if ($assembly) { $assembly.Dispose() }
                if ($stream) { $stream.Dispose() }
            }
        }
        foreach ($cfg in @(Get-ChildItem -LiteralPath $Directory -Recurse -File -Filter '*.cfg')) {
            $section = ''
            foreach ($line in [IO.File]::ReadAllLines($cfg.FullName)) {
                if ($line -match '^\s*\[([^]]+)\]') { $section = $Matches[1] }
                elseif ($section -ieq 'Diagnostics' -and $line -match '^\s*(Enabled|CrashBreadcrumbs)\s*=\s*(.*)$') {
                    $key = $Matches[1]; $setting = $Matches[2].Trim()
                    if ($setting -ieq 'true') { Write-Host ('WARNUNG: DIAGNOSE AKTIV! Mitgelieferte Konfiguration: ' + $cfg.Name + ' / Diagnostics.' + $key + '=true') -ForegroundColor Yellow }
                    else { Write-Host ('  Mitgelieferte Konfiguration: ' + $cfg.Name + ' / Diagnostics.' + $key + '=' + $setting) }
                }
            }
        }
        Write-Host '  Lokale Benutzerkonfigurationen koennen den Paketstandard ueberschreiben. Spezialschalter grenzen Diagnose weiter ein.'
    } catch {
        Write-Host ('  Diagnosezusammenfassung nicht verfuegbar: ' + $_.Exception.Message)
    }
}
